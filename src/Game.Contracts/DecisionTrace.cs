namespace Game.Contracts;

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
public readonly record struct DecisionTrace(
    int Tick,
    Entity Actor,
    IReadOnlyList<ActionCandidate> Candidates,
    ActionCandidate Selected,
    bool Deferred);
