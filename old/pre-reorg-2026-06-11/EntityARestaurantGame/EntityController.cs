using System.Numerics;
using EntityARestaurantGame.Simulation;
using EntityObjects;
using EntityObjects.Interfaces;
using EntityObjects.Spatial;
using EntityObjects.Ticks;

namespace EntityARestaurantGame;

public record DecisionRecord(string ActionName, string TargetName, string PrimaryFactor);

/// <summary>
/// Owns the clock, the timer wheel, and the entities. Nothing here polls or
/// compares ticks — every behavior is an event that fires by arriving at its
/// bucket (hunger pulses, per-entity decisions). The controller is also the
/// system the events announce to: it applies mood fallout, sweeps deaths,
/// and keeps the signal log the UI reads.
/// </summary>
public class EntityController
{
    private static readonly Random _rng = new();

    // ── Pacing ──────────────────────────────────────────────────────────
    public const double TicksPerSecond = 2;        // real-time auto-run speed
    public const int HungerPulseInterval = 10;     // ticks between hunger beats
    public const int HungerPerPulse = 5;
    public const double StarvationDamage = 3;      // HP lost per pulse at 0 hunger
    public const int DecisionInterval = 5;         // ticks between an entity's decisions
    public const float InteractionRange = 3f;      // max distance to act on a target
    public const float MoveSpeedPerDecision = 2.5f;
    private const float WanderStep = 1.0f;
    public const int StrangerResistDivisor = 2;    // non-favored pawns heed influence at 1/2

    private const int SignalLogCap = 50;

    private readonly Clock _clock = new();
    private readonly Wheel _wheel;
    private readonly SimLoop _loop;

    private readonly Dictionary<string, EntityDecisionEvent> _decisionEvents = new();
    private readonly Dictionary<string, Queue<DecisionRecord>> _decisionHistory = new();
    private readonly List<GameSignal> _signals = new();

    private readonly Vector3 _worldMin;
    private readonly Vector3 _worldMax;

    public List<Entity> ActiveEntities { get; } = new();
    public List<Entity> InactiveEntities { get; } = new();

    public int CurrentTick => _clock.Tick;
    public IReadOnlyList<GameSignal> Signals => _signals;
    public LocationRegistry? Locations { get; }

    /// <summary>
    /// The pawn the player-narrator acts through. Strangers (everyone else)
    /// heed influence at reduced strength. Null until the intro picks one.
    /// </summary>
    public string? FavoredEntityId { get; set; }
    public Entity? FavoredEntity => ActiveEntities.FirstOrDefault(e => e.ID == FavoredEntityId);

    public EntityController(LocationRegistry? locations = null)
    {
        Locations = locations;
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

        _wheel = new Wheel(_clock);
        _loop = new SimLoop(_clock, _wheel, TicksPerSecond);
        _wheel.ScheduleIn(new HungerPulseEvent(this), HungerPulseInterval);
    }

    /// <summary>Most specific registered location containing the entity, or "?".</summary>
    public string LocationTag(Entity entity) =>
        Locations?.FindLocation(entity.Position)?.Tag ?? "?";

    // ── Time ────────────────────────────────────────────────────────────

    /// <summary>Feed real elapsed seconds to the sim. Returns ticks advanced.</summary>
    public int Pump(double realDeltaSeconds) => _loop.Pump(realDeltaSeconds);

    /// <summary>
    /// Manual step: advance time until at least one event actually fires.
    /// Idle ticks are free, so this skips straight to the next beat.
    /// </summary>
    public void AdvanceToNextBeat()
    {
        for (int i = 0; i < Wheel.MaxHorizon; i++)
        {
            int fired = _wheel.ProcessCurrent();
            _clock.Advance();
            if (fired > 0) return;
        }
    }

    // ── Entities ────────────────────────────────────────────────────────

    public IReadOnlyList<DecisionRecord> GetDecisionHistory(string entityId)
    {
        _decisionHistory.TryGetValue(entityId, out var q);
        return q?.ToList() ?? [];
    }

    public void AddEntity(Entity entity, bool active = true)
    {
        if (active) ActiveEntities.Add(entity);
        else InactiveEntities.Add(entity);
        _decisionHistory[entity.ID] = new Queue<DecisionRecord>();
        if (active) ScheduleDecisions(entity);
    }

    public void MakeActive(Entity entity)
    {
        if (!InactiveEntities.Remove(entity))
            throw new InvalidOperationException($"Entity '{entity.DisplayName}' is not inactive.");
        ActiveEntities.Add(entity);
        ScheduleDecisions(entity);
    }

    public void MakeInactive(Entity entity)
    {
        if (!ActiveEntities.Remove(entity))
            throw new InvalidOperationException($"Entity '{entity.DisplayName}' is not active.");
        InactiveEntities.Add(entity);

        // Lazy-cancel: the event still reaches its bucket but no-ops on Fire.
        if (_decisionEvents.Remove(entity.ID, out var ev))
            ev.Cancelled = true;
    }

    private void ScheduleDecisions(Entity entity)
    {
        var ev = new EntityDecisionEvent(this, entity, DecisionInterval);
        _decisionEvents[entity.ID] = ev;
        // Stagger first decisions so entities don't act in lockstep.
        _wheel.ScheduleIn(ev, 1 + _decisionEvents.Count % DecisionInterval);
    }

    // ── Event callbacks ─────────────────────────────────────────────────

    internal void OnHungerPulse()
    {
        foreach (var entity in ActiveEntities.ToList())
        {
            entity.Needs.TickHunger(HungerPerPulse);
            if (entity.Needs.Hunger == 0)
            {
                entity.TakeDamage(StarvationDamage);
                Announce($"{entity.DisplayName} is starving (-{StarvationDamage:0} HP)");
            }
        }
        SweepDeaths();
    }

    internal void ActOnce(Entity entity)
    {
        var candidates = entity.BuildCandidates(ActiveEntities);
        if (candidates.Count == 0) return;

        entity.ApplyBehaviorWeights(candidates);
        entity.ApplyStatusWeights(candidates);
        InjectPlayerInfluence(entity, candidates);

        var selected = WeightedSelect(candidates);
        if (selected == null) return;

        // Spatial gate: desire is global, reach is local. A far target turns
        // this beat into movement; the intent (influence included) persists
        // until the entity is close enough to actually act.
        if (selected.Target.ID != entity.ID &&
            Vector3.Distance(entity.Position, selected.Target.Position) > InteractionRange)
        {
            MoveToward(entity, selected.Target);
            RecordDecision(entity, "Approach", selected.Target.DisplayName, "Distance");
            return;
        }

        bool influenced = entity.PendingInfluence is { } inf
                       && inf.TargetActionName == selected.Action.ActionName;
        entity.PendingInfluence = null;   // consumed once an action actually lands

        var items = selected.Action.GetViableItems(entity, selected.Target);
        var item = items.Count > 0 ? items[_rng.Next(items.Count)] : null;
        if (item == null && selected.Action.RequiresItem) return;

        selected.Action.Execute(entity, selected.Target, item);
        if (selected.Action.ActionName == "Idle")
            Wander(entity);

        ApplyMoodEvents(entity, selected);
        RecordDecision(entity, selected);
        AnnounceDecision(entity, selected, item, influenced);
        SweepDeaths();
    }

    // ── Movement ────────────────────────────────────────────────────────

    private void MoveToward(Entity entity, Entity target)
    {
        var delta = target.Position - entity.Position;
        float dist = delta.Length();
        if (dist <= 0.01f) return;

        // Stop just inside interaction range rather than on top of the target.
        float step = Math.Min(MoveSpeedPerDecision, Math.Max(0.1f, dist - InteractionRange + 0.5f));
        entity.Position += Vector3.Normalize(delta) * step;
        ClampToWorld(entity);

        float remaining = Vector3.Distance(entity.Position, target.Position);
        Announce($"{entity.DisplayName} moves toward {target.DisplayName} ({remaining:0.0}m away)");
    }

    private void Wander(Entity entity)
    {
        var dir = new Vector3((float)(_rng.NextDouble() * 2 - 1), (float)(_rng.NextDouble() * 2 - 1), 0);
        if (dir.LengthSquared() < 0.01f) return;
        entity.Position += Vector3.Normalize(dir) * WanderStep;
        ClampToWorld(entity);
    }

    private void ClampToWorld(Entity entity) =>
        entity.Position = Vector3.Clamp(entity.Position, _worldMin, _worldMax);

    private void SweepDeaths()
    {
        foreach (var dead in ActiveEntities.Where(e => !e.IsAlive).ToList())
        {
            MakeInactive(dead);   // also lazy-cancels its decision event
            Announce($"{dead.DisplayName} has died");
        }
    }

    // ── Decision pipeline ───────────────────────────────────────────────

    private void InjectPlayerInfluence(Entity entity, List<ActionCandidate> candidates)
    {
        if (entity.PendingInfluence is not { } influence) return;

        // Low mood reduces how much the entity listens to the player
        int effectiveStrength = (int)(influence.Strength * entity.Needs.MoodRatio);

        // The narrator acts THROUGH a favored pawn; strangers only half-listen.
        if (FavoredEntityId != null && entity.ID != FavoredEntityId)
            effectiveStrength /= StrangerResistDivisor;

        if (effectiveStrength <= 0) return;

        foreach (var candidate in candidates.Where(c => c.Action.ActionName == influence.TargetActionName))
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

    private static void ApplyMoodEvents(Entity actor, ActionCandidate selected)
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

    private void RecordDecision(Entity entity, ActionCandidate selected)
    {
        var primaryFactor = selected.Weights
            .OrderByDescending(w => w.Weight)
            .FirstOrDefault();

        RecordDecision(entity,
            selected.Action.ActionName,
            selected.Target.DisplayName,
            primaryFactor?.Source.ToString() ?? "None");
    }

    private void RecordDecision(Entity entity, string actionName, string targetName, string factor)
    {
        var record = new DecisionRecord(actionName, targetName, factor);

        if (!_decisionHistory.TryGetValue(entity.ID, out var history))
        {
            history = new Queue<DecisionRecord>();
            _decisionHistory[entity.ID] = history;
        }

        history.Enqueue(record);
        if (history.Count > 3) history.Dequeue();
    }

    private void AnnounceDecision(Entity actor, ActionCandidate selected, IItem? item, bool influenced)
    {
        if (selected.Action.ActionName == "Idle") return;   // resting isn't news

        bool self = actor.ID == selected.Target.ID;
        string target = self ? "" : $" → {selected.Target.DisplayName}";
        string with = item != null ? $" ({item.ItemName})" : "";
        string nudge = influenced ? "  [influenced]" : "";
        Announce($"{actor.DisplayName}: {selected.Action.ActionName}{target}{with}{nudge}");
    }

    internal void Announce(string message)
    {
        _signals.Add(new GameSignal(CurrentTick, message));
        if (_signals.Count > SignalLogCap) _signals.RemoveAt(0);
    }

    private static ActionCandidate? WeightedSelect(List<ActionCandidate> candidates)
    {
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
}
