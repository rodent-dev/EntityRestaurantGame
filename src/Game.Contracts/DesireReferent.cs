namespace Game.Contracts;

/// <summary>
/// "Remember what I want" stores the referent, never the score: a bookmark
/// saying *this object is live for me*. Magnitude is recomputed every read,
/// so a rogue who finds a better dagger stops wanting the old one with no
/// cleanup. The one sanctioned exception is PinnedWant — the obsession spike —
/// which an IEvent on the tick wheel decays back to zero.
/// </summary>
public class DesireReferent
{
    public string ObjectId { get; init; } = string.Empty;
    public string ObjectName { get; init; } = string.Empty;
    public int FirstSurfacedTick { get; init; }
    public string Context { get; init; } = string.Empty;

    /// <summary>
    /// Obsession override: while &gt; 0, effective want is at least this value
    /// regardless of recompute. Set by the rare obsession gate, decayed by
    /// WantSpikeEvent. Zero means the rule (stateless recompute) is in force.
    /// </summary>
    public double PinnedWant { get; set; }
}
