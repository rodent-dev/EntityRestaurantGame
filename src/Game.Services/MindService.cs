using Game.Contracts;

namespace Game.Services;

/// <summary>
/// The connection operator: fact on one side, valuation on the other,
/// additive, axis-grouped. One formula run three ways — Feeling (over
/// incoming claims), Want (over the have/have-not counterfactual), and
/// Need (the operator turned inward). Generic across the ontology: anything
/// descriptor-bearing (item, person) is wantable by the identical formula.
///
/// This is also the v1 of the magnitude/projection function: sat(axis|have)
/// is a tanh over the competence-scaled descriptor pile the entity already
/// owns, so the projection reads only from preference + stat + inventory
/// state that already exists.
/// </summary>
public class MindService
{
    private const double BaselineStat = 10.0;

    /// <summary>
    /// Newly-surfaced wants worth bookmarking; below this, the object
    /// doesn't register as a desire at all.
    /// </summary>
    public const double DesireThreshold = 0.35;

    // ── Stat fold (§3): competence, the second orthogonal input ─────────

    /// <summary>
    /// How much larger a descriptor on this axis projects *in this body*.
    /// Preference is taste ("do I care"); this is competence ("does it do
    /// more in my hands"). Kept separate so a high-CON mobility-lover —
    /// competent at armor, doesn't want it — can exist.
    /// </summary>
    public double Competence(Entity e, string axis) => axis switch
    {
        Axes.Harm        => e.Stats.Strength / BaselineStat,
        Axes.Safety      => e.Stats.Constitution / BaselineStat,
        Axes.Nourishment => e.Stats.Constitution / BaselineStat,
        Axes.Craft       => e.Stats.Intelligence / BaselineStat,
        Axes.Commerce    => e.Stats.Charisma / BaselineStat,
        Axes.Beauty      => e.Stats.Charisma / BaselineStat,
        Axes.Glory       => e.Stats.Charisma / BaselineStat,
        _                => 1.0
    };

    // ── Run 1: Feeling — S_R(o) over incoming claims ─────────────────────

    /// <summary>Σ_axis w_R(axis) · (Σ claim valences on that axis).</summary>
    public double Feeling(Entity receiver, IEnumerable<Claim> claims) =>
        claims.GroupBy(c => c.Axis)
              .Sum(g => receiver.Mind.Weight(g.Key) * g.Sum(c => c.Valence));

    /// <summary>
    /// The same operator over everything already BELIEVED about an object:
    /// Σ_axis w_R(axis) · belief(object, axis). "Opinion of the creature" —
    /// do they have qualities this mind is looking for, per accumulated
    /// claims and personal bias. Squashed so wild belief piles saturate.
    /// </summary>
    public double Regard(Entity receiver, string objectId)
    {
        if (!receiver.Mind.Beliefs.TryGetValue(objectId, out var sheet)) return 0;
        double raw = sheet.Sum(kv => receiver.Mind.Weight(kv.Key) * kv.Value);
        return MindMath.Sat(raw * 0.25);
    }

    // ── Run 2: Want — the counterfactual ────────────────────────────────

    /// <summary>
    /// Everything the entity already sits on for this axis: own descriptors
    /// plus carried items, competence-scaled. The "have" baseline of the
    /// counterfactual.
    /// </summary>
    public double OwnedOnAxis(Entity e, string axis)
    {
        double total = e.SelfDescriptors.GetValueOrDefault(axis);
        foreach (var item in e.Inventory)
            total += item.Descriptors.GetValueOrDefault(axis);
        return total * Competence(e, axis);
    }

    /// <summary>
    /// want_R(o) = Σ_axis w_R(axis) · [sat(axis|have o) − sat(axis|¬have o)]
    /// for an object the receiver does NOT own. Satiation and class-feel are
    /// emergent: no delta on the flat part of the curve, no delta on axes
    /// with zero weight. The table is the character sheet.
    /// </summary>
    public double Want(Entity receiver, IReadOnlyDictionary<string, double> descriptors)
    {
        double total = 0;
        foreach (var (axis, valence) in descriptors)
        {
            double weight = receiver.Mind.Weight(axis);
            if (weight == 0) continue;

            double have = OwnedOnAxis(receiver, axis);
            double projected = valence * Competence(receiver, axis);
            total += weight * (MindMath.Sat(have + projected) - MindMath.Sat(have));
        }
        return total;
    }

    /// <summary>
    /// Want with the obsession pin honored: recompute is the rule, the
    /// pinned spike is the sanctioned exception (decaying on the tick wheel).
    /// </summary>
    public double EffectiveWant(Entity receiver, string objectId, IReadOnlyDictionary<string, double> descriptors)
    {
        double recomputed = Want(receiver, descriptors);
        double pinned = receiver.Mind.FindDesire(objectId)?.PinnedWant ?? 0;
        return Math.Max(recomputed, pinned);
    }

    /// <summary>
    /// The same counterfactual pointed the other way: how much satisfied
    /// preference the owner LOSES by giving the item up. The cost side of
    /// every exchange.
    /// </summary>
    public double KeepValue(Entity owner, IItem item)
    {
        double total = 0;
        foreach (var (axis, valence) in item.Descriptors)
        {
            double weight = owner.Mind.Weight(axis);
            if (weight == 0) continue;

            double have = OwnedOnAxis(owner, axis);   // includes the item
            double projected = valence * Competence(owner, axis);
            total += weight * (MindMath.Sat(have) - MindMath.Sat(have - projected));
        }
        return total;
    }

    /// <summary>
    /// Salience: how loudly an object's own descriptors argue for attention,
    /// regardless of who is listening. Gates what surfaces conversationally.
    /// </summary>
    public static double Salience(IReadOnlyDictionary<string, double> descriptors) =>
        descriptors.Sum(d => Math.Abs(d.Value));

    // ── Run 3: Need — the operator turned inward ─────────────────────────

    /// <summary>
    /// The actor emits its own state; axes where own descriptors fall short
    /// of own weights ARE needs. Need = want_R(self-improvement). Health and
    /// hunger fold in as emitted Safety/Nourishment descriptors.
    /// </summary>
    public IEnumerable<(string Axis, double Shortfall)> NeedsOf(Entity e)
    {
        foreach (var (axis, weight) in e.Mind.Preferences)
        {
            if (weight <= 0) continue;

            double state = axis switch
            {
                Axes.Nourishment => e.Needs.HungerRatio,
                Axes.Safety      => e.GetCurrentHealth() / e.GetMaxHealth(),
                Axes.Comfort     => e.Needs.MoodRatio,
                _ => MindMath.Sat(OwnedOnAxis(e, axis))   // material axes: what you hold is what you are
            };

            double shortfall = weight * (1.0 - Math.Clamp(state, 0, 1));
            if (shortfall > 0.01)
                yield return (axis, shortfall);
        }
    }
}
