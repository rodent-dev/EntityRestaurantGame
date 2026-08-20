using System.Numerics;
using Engine.Spatial;
using Game.Contracts;

namespace Game.Services;

/// <summary>
/// The nitty-gritty of entity handling: candidate building, weighing,
/// selection, needs, mood fallout, and movement. The controller decides
/// WHEN any of this happens; this service knows HOW. It holds no game
/// state of its own beyond randomness and the world geometry.
/// </summary>
public class EntityService
{
    // Distance-into-decision: weight lost per reach-length of gap to a target,
    // and the floor it can't sink past (distance discourages, never forbids).
    public const float ProximityFalloffPerReach = 2f;
    public const int ProximityPenaltyCap = 8;

    private readonly Random _rng;
    private readonly Vector3 _worldMin;
    private readonly Vector3 _worldMax;

    public LocationRegistry? Locations { get; }

    /// <summary>
    /// Pass an <paramref name="rng"/> to make a run reproducible. This service
    /// owns selection and item picking, so it is the largest single source of
    /// run-to-run variance — without a seeded stream here, two runs of the same
    /// world diverge and no balance change can be measured against the noise.
    /// </summary>
    public EntityService(LocationRegistry? locations = null, Random? rng = null)
    {
        Locations = locations;
        _rng = rng ?? new Random();
        if (locations is { All.Count: > 0 })
        {
            _worldMin = locations.All.Select(l => l.Min).Aggregate(Vector3.Min);
            _worldMax = locations.All.Select(l => l.Max).Aggregate(Vector3.Max);
        }
        else
        {
            _worldMin = new Vector3(float.MinValue);
            _worldMax = new Vector3(float.MaxValue);
        }
    }

    // ── Decision pipeline ───────────────────────────────────────────────

    public List<ActionCandidate> BuildCandidates(Entity actor, IReadOnlyList<Entity> activeEntities)
    {
        var candidates = new List<ActionCandidate>();

        foreach (var action in actor.Actions.GetAllActions())
        {
            if (!action.CanActorUse(actor))
                continue;

            foreach (var target in activeEntities)
            {
                if (!action.CanTarget(actor, target))
                    continue;

                var candidate = new ActionCandidate(action, target);
                if (action.BaseWeight != 0)
                    candidate.Weights.Add(new ActionWeight { Weight = action.BaseWeight, Source = ActionWeightSource.Base });
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    // Relationship weighting routes by action nature, never action names.
    private static readonly string[] HostileKeywords = ["Aggressive", "Violent", "Offensive"];
    private static readonly string[] WarmKeywords = ["Social", "Supportive", "Diplomatic"];

    public void ApplyBehaviorWeights(Entity actor, List<ActionCandidate> candidates)
    {
        var relationshipWeights = candidates
            .Select(c => c.Target.ID)
            .Distinct()
            .Where(id => actor.EntityRelationships.ContainsKey(id))
            .ToDictionary(id => id, id => actor.EntityRelationships[id]);

        foreach (var candidate in candidates)
        {
            // Sum keyword-matched behavior weights.
            // Each behavior contributes: affect * scalar * matchCount
            int behaviorWeight = 0;
            foreach (var behavior in actor.Behaviors)
            {
                int matches = behavior.MatchCount(candidate.Action.Keywords);
                if (matches > 0)
                    behaviorWeight += behavior.Affect * behavior.Scalar * matches;
            }

            if (behaviorWeight != 0)
                candidate.Weights.Add(new ActionWeight { Weight = behaviorWeight, Source = ActionWeightSource.Behaviors });

            // Relationship: keyword-aware and superlinear (rel²/8) — distaste
            // is a mood, hatred is a motive. Positive feeling favors warm
            // actions and suppresses hostile ones toward that target;
            // negative feeling does the reverse. Mild scores round to zero
            // here on purpose: they still color dialog (trust reads raw rel),
            // they just don't start fights.
            if (relationshipWeights.TryGetValue(candidate.Target.ID, out var rel) && rel != 0)
            {
                // Clamp before squaring: stored grudges may deepen without
                // bound (combat keeps souring them), but the TEMPTATION
                // saturates — otherwise the curve feeds back through
                // rel-damaging effects into an unwinnable murder spiral
                // (soak-verified 2026-07-18).
                int clamped = Math.Clamp(rel, -10, 10);
                int magnitude = clamped * clamped / 8;
                if (magnitude == 0) continue;

                bool hostile = candidate.Action.Keywords.Overlaps(HostileKeywords);
                bool warm = candidate.Action.Keywords.Overlaps(WarmKeywords);
                int weight = rel > 0
                    ? (warm ? magnitude : 0) - (hostile ? magnitude : 0)
                    : (hostile ? magnitude : 0) - (warm ? magnitude : 0);

                if (weight != 0)
                    candidate.Weights.Add(new ActionWeight { Weight = weight, Source = ActionWeightSource.Relationship });
            }
        }
    }

    public void ApplyStatusWeights(Entity actor, List<ActionCandidate> candidates) =>
        StatusWeightCalculator.Apply(actor, candidates);

    /// <summary>
    /// Does this candidate answer the whisper? Both named parts must agree,
    /// and an unset part is a wildcard: action-only nudges whoever the sim
    /// picks, target-only nudges anything aimed at that entity. The
    /// controller reuses this to decide whether a landed action counts as
    /// influenced, so the two can never drift apart.
    /// </summary>
    public static bool MatchesInfluence(ActionCandidate candidate, Influence influence) =>
        (string.IsNullOrEmpty(influence.TargetActionName)
            || candidate.Action.ActionName == influence.TargetActionName)
        && (influence.TargetEntityId is null
            || candidate.Target.ID == influence.TargetEntityId);

    public void ApplyInfluence(Entity entity, List<ActionCandidate> candidates, bool stranger, int strangerResistDivisor)
    {
        if (entity.PendingInfluence is not { } influence) return;

        // Low mood reduces how much the entity listens to the player
        int effectiveStrength = (int)(influence.Strength * entity.Needs.MoodRatio);

        // The narrator acts THROUGH a favored pawn; strangers only half-listen.
        if (stranger)
            effectiveStrength /= strangerResistDivisor;

        if (effectiveStrength <= 0) return;

        foreach (var candidate in candidates.Where(c => MatchesInfluence(c, influence)))
        {
            // Influence biases, it doesn't command: a nudge toward an action
            // no behavior endorses is out of character and meets resistance.
            bool inCharacter = candidate.Weights.Any(w => w.Source == ActionWeightSource.Behaviors && w.Weight > 0);
            int strength = inCharacter ? effectiveStrength : Math.Max(1, effectiveStrength / 2);

            candidate.Weights.Add(new ActionWeight
            {
                Weight = strength,
                Source = ActionWeightSource.PlayerInfluence
            });
        }
    }

    public ActionCandidate? SelectWeighted(List<ActionCandidate> candidates)
    {
        if (candidates.Count == 0) return null;

        int total = candidates.Sum(c => Math.Max(1, c.TotalWeight));
        int roll = _rng.Next(0, total);
        int cumulative = 0;

        foreach (var candidate in candidates)
        {
            cumulative += Math.Max(1, candidate.TotalWeight);
            if (roll < cumulative) return candidate;
        }

        return candidates.Last();
    }

    /// <summary>Picks a random viable item for the selected action, or null.</summary>
    public IItem? SelectItem(Entity actor, ActionCandidate selected)
    {
        var items = selected.Action.GetViableItems(actor, selected.Target);
        return items.Count > 0 ? items[_rng.Next(items.Count)] : null;
    }

    // ── Needs ───────────────────────────────────────────────────────────

    /// <summary>Decays hunger; returns true when the entity is starving (hunger hit 0).</summary>
    public bool ApplyHunger(Entity entity, int amount)
    {
        entity.Needs.TickHunger(amount);
        return entity.Needs.Hunger == 0;
    }

    /// <summary>Mood fallout of an executed action, keyed off action keywords.</summary>
    public void ApplyMoodFallout(Entity actor, ActionCandidate selected)
    {
        var keywords = selected.Action.Keywords;
        bool isSelfTargeted = actor.ID == selected.Target.ID;

        // Violent actions hurt the target's mood heavily
        if (keywords.Contains("Violent"))
            selected.Target.Needs.AdjustMood(-10);
        // Aggressive but non-violent (e.g. Grapple) is less harsh
        else if (keywords.Contains("Aggressive"))
            selected.Target.Needs.AdjustMood(-6);

        // Supportive actions lift the target's mood
        if (keywords.Contains("Supportive"))
            selected.Target.Needs.AdjustMood(4);

        // Diplomatic actions are a mild positive for the target
        if (keywords.Contains("Diplomatic"))
            selected.Target.Needs.AdjustMood(3);

        // Greedy self-targeted actions (eating) satisfy the actor
        if (isSelfTargeted && keywords.Contains("Greedy"))
            actor.Needs.AdjustMood(3);
    }

    // ── Spatial ─────────────────────────────────────────────────────────

    /// <summary>Center-to-center distance at which a can act on b: a's reach
    /// measured between footprints, so big bodies don't lose effective range.</summary>
    public static float ReachDistance(Entity a, Entity b) =>
        a.InteractionRadius + a.PhysicsRadius + b.PhysicsRadius;

    /// <summary>Ground covered in one decision beat at average Agility.</summary>
    public const float BaseMoveSpeed = 2.5f;

    /// <summary>Marks an action that closes distance itself, so the reach gate
    /// leaves it alone instead of substituting a step for it.</summary>
    public const string MovementKeyword = "Mobile";

    /// <summary>
    /// How fast this entity closes a gap, derived from Agility rather than
    /// stored — one source of truth, and approach time becomes a readable
    /// consequence of a stat instead of a hidden global. Agility 10 (the
    /// default roll) reproduces the old constant exactly. Floored so a
    /// clumsy entity is slow, never immobile.
    /// </summary>
    public static float MoveSpeed(Entity entity) =>
        Math.Max(0.5f, BaseMoveSpeed * entity.Stats.Agility / 10f);

    public bool IsInReach(Entity a, Entity b) =>
        Vector3.Distance(a.Position, b.Position) <= ReachDistance(a, b);

    /// <summary>
    /// Situation weighting: how far a target is weighs into whether they're
    /// worth bothering with. Zero inside reach, then a gentle per-reach-length
    /// falloff — deliberately never a wall: a strong enough desire still sends
    /// the entity walking across the map.
    /// </summary>
    public void ApplyProximityWeights(Entity actor, List<ActionCandidate> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.Target.ID == actor.ID) continue;

            float gap = Vector3.Distance(actor.Position, candidate.Target.Position)
                      - ReachDistance(actor, candidate.Target);
            if (gap <= 0) continue;

            int penalty = Math.Min(ProximityPenaltyCap,
                (int)MathF.Ceiling(gap / actor.InteractionRadius * ProximityFalloffPerReach));
            candidate.Weights.Add(new ActionWeight { Weight = -penalty, Source = ActionWeightSource.Situation });
        }
    }

    /// <summary>
    /// Steps the entity toward the target, stopping just inside the given
    /// range. Returns the remaining distance after the move.
    /// </summary>
    public float MoveToward(Entity entity, Entity target, float speed, float stopRange)
    {
        var delta = target.Position - entity.Position;
        float dist = delta.Length();
        if (dist <= 0.01f) return dist;

        float step = Math.Min(speed, Math.Max(0.1f, dist - stopRange + 0.5f));
        entity.Position += Vector3.Normalize(delta) * step;
        ClampToWorld(entity);

        return Vector3.Distance(entity.Position, target.Position);
    }

    public void Wander(Entity entity, float step)
    {
        var dir = new Vector3((float)(_rng.NextDouble() * 2 - 1), (float)(_rng.NextDouble() * 2 - 1), 0);
        if (dir.LengthSquared() < 0.01f) return;
        entity.Position += Vector3.Normalize(dir) * step;
        ClampToWorld(entity);
    }

    /// <summary>
    /// Personal space: no two entities occupy the same spot. One relaxation
    /// pass pushes each overlapping pair apart (half the overlap each) in the
    /// floor plane. Close interactions stay close — reach is far larger than
    /// any footprint — but nobody envelops anybody.
    /// </summary>
    public void ResolveOverlaps(IReadOnlyList<Entity> entities)
    {
        for (int i = 0; i < entities.Count; i++)
            for (int j = i + 1; j < entities.Count; j++)
            {
                var a = entities[i];
                var b = entities[j];
                if (a.Position.Z != b.Position.Z) continue; // different floor layers

                float minDist = a.PhysicsRadius + b.PhysicsRadius;
                var delta = b.Position - a.Position;
                delta.Z = 0;
                float dist = delta.Length();
                if (dist >= minDist) continue;

                // Perfectly coincident pair: split along a random direction.
                var dir = dist < 0.001f
                    ? new Vector3((float)(_rng.NextDouble() * 2 - 1), (float)(_rng.NextDouble() * 2 - 1), 0)
                    : delta / dist;
                if (dir.LengthSquared() < 0.0001f) dir = Vector3.UnitX;
                dir = Vector3.Normalize(dir);

                float push = (minDist - dist) / 2f;
                a.Position -= dir * push;
                b.Position += dir * push;
                ClampToWorld(a);
                ClampToWorld(b);
            }
    }

    /// <summary>Most specific registered location containing the entity, or "?".</summary>
    public string LocationTag(Entity entity) =>
        Locations?.FindLocation(entity.Position)?.Tag ?? "?";

    private void ClampToWorld(Entity entity) =>
        entity.Position = Vector3.Clamp(entity.Position, _worldMin, _worldMax);
}
