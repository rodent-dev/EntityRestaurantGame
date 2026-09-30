namespace Game.Contracts;

/// <summary>
/// What became of a beat.
/// </summary>
/// <remarks>
/// The census question "what did entities spend their beats on" is really two
/// questions — what they WANTED and what they got to do about it — and a bare
/// "deferred" flag collapses the second one into a yes/no. Walking, standing
/// empty-handed and staying on task are three different diagnoses with three
/// different fixes, and only the outcome distinguishes them.
/// </remarks>
public enum BeatOutcome
{
    /// <summary>The winner was committed to and the beat was spent on it.</summary>
    Acted,

    /// <summary>Target out of reach: the beat became a step toward them.</summary>
    Approached,

    /// <summary>The winner needed an item the actor turned out not to hold.</summary>
    NoItem,

    /// <summary>The beat went to the activity already in progress.</summary>
    CarriedOn,
}

/// <summary>
/// Everything the weight pipeline considered on one beat, handed out after the
/// choice is made. Balance work needs the candidates that LOST as much as the
/// one that won — "Attack was picked" says nothing without "…out of 26 options
/// carrying this much mass".
///
/// Read-only by contract: an observer inspects, it never writes. The sim does
/// not know whether anyone is listening, and behaves identically either way.
/// </summary>
/// <param name="Tick">Sim tick the beat fired on.</param>
/// <param name="Actor">Who decided.</param>
/// <param name="Candidates">Every legal (action, target) pair, weights attached.</param>
/// <param name="Selected">The pair that won the weighted roll.</param>
/// <param name="Deferred">
/// True when the winning pair did NOT execute this beat — the target was out of
/// reach (the beat became a step toward them) or the action needed an item the
/// actor turned out not to have. <paramref name="Selected"/> then describes the
/// INTENT rather than what happened, and counting the two apart is how you spot
/// a population that spends its beats walking instead of acting.
/// </param>
/// <param name="Outcome">
/// The same fact with its reason attached. <paramref name="Deferred"/> is the
/// two-valued view of this, kept because the aggregate probe only ever asks the
/// yes/no.
/// </param>
/// <param name="Commitment">
/// The incumbent activity's case for being left alone, when one was weighed
/// this beat — null when the actor was idle or the pipeline landed on what they
/// were already doing (agreement is not an interruption, so nothing is priced).
/// Present with <see cref="BeatOutcome.CarriedOn"/> it is the case that WON;
/// present with any other outcome it is the case that LOST, which is the shape
/// walking out of a job takes in the record.
/// </param>
/// <param name="Item">The item the action was performed with, if any.</param>
public readonly record struct DecisionTrace(
    int Tick,
    Entity Actor,
    IReadOnlyList<ActionCandidate> Candidates,
    ActionCandidate Selected,
    bool Deferred,
    BeatOutcome Outcome = BeatOutcome.Acted,
    Commitment? Commitment = null,
    IItem? Item = null);
