using Game.Contracts;

namespace Game.Services;

/// <summary>
/// Is anything worth stopping for?
/// </summary>
/// <remarks>
/// The other half of the decision, once an entity can be in the middle of
/// something. The weight pipeline answers "what appeals to me right now"; this
/// answers "does it appeal enough to walk out on what I am already doing".
/// <para>
/// Pure function of the actor's state and its incumbent activity, keyword-matched
/// like everything else and holding no state of its own — the same contract
/// <see cref="StatusWeightCalculator"/> works under. It rolls no dice: the caller
/// compares <see cref="Commitment.Total"/> against the challenger's weight and
/// takes the larger. See <see cref="Commitment"/> for why that is a comparison
/// and not a candidate.
/// </para>
/// </remarks>
public static class CommitmentCalculator
{
    /// <summary>Flat unwillingness to reopen a settled question.</summary>
    public const int Baseline = 4;

    /// <summary>
    /// Being properly into something and not wanting to break off.
    /// </summary>
    /// <remarks>
    /// Not sunk cost — partial credit means the beats already worked are already
    /// banked and walking out forfeits nothing earned. What finishing buys is the
    /// performance score, and this term is that pull made legible.
    /// <para>
    /// It has to be big relative to the pipeline's mean weights (Behaviors ~6.5,
    /// Situation ~9.3, measured 2026-08-09) because the question is re-asked
    /// every beat of the duration. A term that only bites near the end leaves the
    /// first half defended by nothing at all.
    /// </para>
    /// </remarks>
    public const int Momentum = 18;

    /// <summary>How far in before momentum is at full strength.</summary>
    public const double SettledBy = 0.25;

    /// <summary>
    /// Reluctance to reopen a question you only just settled, and what it sheds
    /// each beat.
    /// </summary>
    /// <remarks>
    /// Momentum is weakest exactly where it is needed most — on the first beat,
    /// where nothing has been invested yet — and that gap is a measurable
    /// failure mode, not a theoretical one: committing to a shift and walking
    /// out of it one beat later produced runs scored at 0.00, which cost the
    /// actor a whole beat and bought nothing. This covers the gap from the other
    /// end and decays out as momentum ramps in, so the defence is roughly flat
    /// across the run instead of a valley at the start.
    /// </remarks>
    public const int SettlingPeak = 10;
    public const int SettlingDecayPerBeat = 3;

    /// <summary>Extra pull once the end is in sight, and where that starts.</summary>
    public const int NearlyThere = 6;
    public const double NearlyThereFrom = 0.75;

    /// <summary>
    /// The drag a need in real trouble puts on carrying on, at its worst.
    /// </summary>
    /// <remarks>
    /// Scaled by how far past <see cref="StatusWeightCalculator"/>'s critical
    /// line the worst need has fallen, so it is silent in the comfortable middle
    /// and only argues once something is genuinely wrong. This is what lets a
    /// starving worker walk off a shift — without it, momentum would keep them
    /// at the bench until they died at it.
    /// </remarks>
    public const int DistressMax = 18;
    public const double DistressBelow = 0.25;

    /// <summary>Being spoken to is a prod. Consumed by answering, like the deposit itself.</summary>
    public const int AddressedBy = 6;

    /// <summary>How hard a whisper the current activity does not answer pulls you off it.</summary>
    public const int WhisperPull = 10;

    /// <summary>
    /// Weigh the case for staying on task. <paramref name="stranger"/> halves the
    /// player's pull exactly as the influence pipeline does, so belonging means
    /// the same thing in both places.
    /// </summary>
    public static Commitment Weigh(
        Entity actor, IAction action, Activity activity, int currentTick,
        int strangerResistDivisor, bool stranger)
    {
        var terms = new List<CommitmentTerm>
        {
            new(Baseline, "already doing something"),
        };

        // Ramps in over the first quarter and then holds. Flat rather than
        // rising, because what is being defended against is N independent
        // chances to wander off, not one decision at the midpoint.
        double done = activity.FractionElapsed(action.Duration);
        int momentum = (int)Math.Round(Math.Min(1, done / SettledBy) * Momentum);
        if (momentum > 0) terms.Add(new(momentum, "in the middle of it"));

        int settling = SettlingPeak - activity.Elapsed * SettlingDecayPerBeat;
        if (settling > 0) terms.Add(new(settling, "only just started"));

        if (done > NearlyThereFrom) terms.Add(new(NearlyThere, "nearly finished"));

        // The worst-off need speaks, not the sum of them: it takes one thing
        // being badly wrong to walk out, and three things being mildly wrong is
        // not the same as one crisis.
        double worst = Math.Min(actor.Needs.HungerRatio,
                       Math.Min(actor.Needs.MoodRatio, HealthRatio(actor)));
        if (worst < DistressBelow)
        {
            int drag = (int)Math.Round((DistressBelow - worst) / DistressBelow * DistressMax);
            if (drag > 0) terms.Add(new(-drag, "something is badly wrong"));
        }

        // Someone put a claim in front of you and it has not gone stale. The
        // dialog pipeline already pays a decaying pull to ANSWER; this is the
        // matching pull to stop what you are doing so you can.
        if (actor.Mind.Deposited is not null &&
            currentTick - actor.Mind.DepositedTick <= DialogService.DepositStaleTicks)
        {
            terms.Add(new(-AddressedBy, "someone is talking to you"));
        }

        // A whisper the current activity does not already answer. Matching
        // whispers are silent here on purpose — the player asking for what you
        // are already doing is agreement, not interruption.
        if (actor.PendingInfluence is { } influence &&
            !AnswersWhisper(action, activity, influence))
        {
            int pull = stranger ? WhisperPull / Math.Max(1, strangerResistDivisor) : WhisperPull;
            if (pull > 0) terms.Add(new(-pull, "you said something"));
        }

        return new Commitment { Terms = terms };
    }

    /// <summary>
    /// Does what the actor is already doing satisfy the whisper? Mirrors
    /// <see cref="EntityService.MatchesInfluence"/>'s wildcard rules against an
    /// activity instead of a candidate, so the two cannot disagree about what
    /// counts as obeying.
    /// </summary>
    public static bool AnswersWhisper(IAction action, Activity activity, Influence influence)
    {
        bool actionOk = ActionTaxonomy.Answers(action, influence.Keywords);
        bool targetOk = influence.TargetEntityId is null
                        || influence.TargetEntityId == activity.TargetId;
        return actionOk && targetOk;
    }

    private static double HealthRatio(Entity actor) =>
        actor.GetMaxHealth() <= 0 ? 1 : actor.GetCurrentHealth() / actor.GetMaxHealth();
}
