namespace Game.Contracts;

/// <summary>
/// Something an entity is in the MIDDLE of. An <see cref="IAction"/> is the
/// authored verb; this is one run of it in progress.
/// </summary>
/// <remarks>
/// The def/instance split, same as items: the verb is shared and stateless, the
/// run is plain data belonging to one entity. Nothing here is polymorphic, so a
/// save file only ever needs <c>(actionName, targetId, elapsed, progress)</c>.
/// <para>
/// <see cref="Elapsed"/> and <see cref="Progress"/> are deliberately two
/// numbers. Elapsed is wall-clock — how many beats this ate, however well or
/// badly. Progress is how many of those beats actually counted, and it rises by
/// the actor's focus rather than by 1. Eight beats at half focus and four beats
/// at full focus land in the same place; that equivalence is the whole model.
/// Their ratio is what an action gets PAID on.
/// </para>
/// </remarks>
public sealed class Activity
{
    /// <summary>The verb, resolved against the actor's own <see cref="ActionModule"/>.</summary>
    public required string ActionName { get; init; }

    /// <summary>Who it is aimed at. Self-targeted actions name their own actor.</summary>
    public required string TargetId { get; init; }

    /// <summary>Tick this began — for elapsed time, and for the ledger.</summary>
    public required int StartedTick { get; init; }

    /// <summary>The item committed to this run, held for its whole duration.</summary>
    public IItem? Item { get; init; }

    /// <summary>
    /// Whether a player whisper is what started this. A fact about the run, not
    /// about the beat that ends it — a long action settles beats after the
    /// nudge that caused it was consumed, and the announcement still has to be
    /// able to say the narrator asked for this.
    /// </summary>
    public bool Influenced { get; init; }

    /// <summary>Beats spent so far, counted whether or not they went anywhere.</summary>
    public int Elapsed { get; set; }

    /// <summary>Accrued focused beats — the numerator of the score.</summary>
    public double Progress { get; set; }

    /// <summary>
    /// How well this went: focused beats over the beats the action asked for.
    /// </summary>
    /// <remarks>
    /// 1.0 is a full shift worked properly. 0.75 is walking out three quarters
    /// of the way through — or staying the whole time while starving. Both are
    /// worth three quarters, and neither is worth nothing. Partial credit is
    /// what makes a long action a commitment rather than a gamble.
    /// </remarks>
    public double PerformanceAgainst(int duration) =>
        duration <= 0 ? 1 : Math.Clamp(Progress / duration, 0, 1);

    /// <summary>How far through the wall clock this is, 0..1.</summary>
    public double FractionElapsed(int duration) =>
        duration <= 0 ? 1 : Math.Clamp(Elapsed / (double)duration, 0, 1);

    public bool IsComplete(int duration) => Elapsed >= duration;
}
