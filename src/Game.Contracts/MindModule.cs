namespace Game.Contracts;

/// <summary>
/// The interiority sheet: what an entity values (valuation atoms), what it
/// currently believes (accumulated claims), what it has bookmarked as wanted,
/// and the character-stable traits that gate interpretation. Like the rest of
/// Contracts this holds state only; MindService/DialogService do the math.
/// </summary>
public class MindModule
{
    /// <summary>The valuation side of the operator: (axis, weight). The receiver's sheet.</summary>
    public Dictionary<string, double> Preferences { get; set; } = new();

    /// <summary>
    /// Accumulated accepted claims: objectId → axis → summed valence.
    /// "But since March" needs zero connective structure — claims just sum.
    /// </summary>
    public Dictionary<string, Dictionary<string, double>> Beliefs { get; } = new();

    /// <summary>Persistent want bookmarks; magnitudes recomputed on read.</summary>
    public List<DesireReferent> Desires { get; } = new();

    // ── Character-stable traits (the predictable half of the threshold) ──

    /// <summary>0..1 — scales belief-mismatch into doubt. High = hard to convince.</summary>
    public double Skepticism { get; set; } = 0.5;

    /// <summary>0..1 — gates the rare obsession spike. High = possessable by shiny things.</summary>
    public double Impulsiveness { get; set; } = 0.2;

    /// <summary>0..1 — appetite for investing in relationships (the introvert
    /// dial). Scales the affinity pull toward befriending/taming/gifting; it
    /// never gates REPLYING — introverts answer, they just rarely seek.</summary>
    public double Sociability { get; set; } = 0.5;

    // ── Situation variable (the deposited claim) ─────────────────────────

    /// <summary>
    /// The last utterance addressed to this entity. Not dialog state — a
    /// deposited claim whose response-pull decays until other candidates win
    /// and the conversation ends by attrition. Consumed when replied to.
    /// </summary>
    public Utterance? Deposited { get; set; }
    public int DepositedTick { get; set; }

    // ── Trivial self-accessors (no math) ─────────────────────────────────

    public double Weight(string axis) => Preferences.GetValueOrDefault(axis);

    public double Belief(string objectId, string axis) =>
        Beliefs.TryGetValue(objectId, out var sheet) ? sheet.GetValueOrDefault(axis) : 0.0;

    public void NudgeBelief(string objectId, string axis, double delta)
    {
        if (!Beliefs.TryGetValue(objectId, out var sheet))
            Beliefs[objectId] = sheet = new Dictionary<string, double>();
        sheet[axis] = sheet.GetValueOrDefault(axis) + delta;
    }

    public DesireReferent? FindDesire(string objectId) =>
        Desires.FirstOrDefault(d => d.ObjectId == objectId);
}
