namespace Game.Contracts;

public interface IAction
{
    string ActionName { get; }

    /// <summary>
    /// Semantic tags that classify what this action *is*.  Used by behaviors
    /// and status calculations to match actions generically instead of by name.
    /// </summary>
    HashSet<string> Keywords { get; }

    /// <summary>
    /// When true, Execute throws if item is null.
    /// When false, Execute may run without any item (item is used as an optional enhancer).
    /// </summary>
    bool RequiresItem { get; }

    /// <summary>
    /// Flat weight every candidate of this action starts with, before
    /// behaviors/status/influence are applied. Zero for most actions.
    /// </summary>
    int BaseWeight => 0;

    /// <summary>
    /// How many decision beats committing to this occupies. 1 — the default —
    /// is a verb that begins and ends in the same beat, which is what every
    /// action was before long-running ones existed.
    /// </summary>
    /// <remarks>
    /// Beats, not ticks. A beat is the unit at which an entity re-asks what it
    /// is doing (<c>EntityController.DecisionInterval</c>), so a duration
    /// counted in beats is a duration counted in decisions — which is the
    /// number that actually matters, because it is how many chances the actor
    /// gets to wander off.
    /// <para>
    /// Duration is wall-clock and fixed: a shift ends when the shift ends.
    /// How WELL it went is <see cref="Activity.PerformanceAgainst"/>, and that
    /// is a separate question — a starving cook still finishes their shift,
    /// just badly. Letting poor focus stretch the duration instead would mean
    /// actions that never end.
    /// </para>
    /// </remarks>
    int Duration => 1;

    /// <summary>
    /// One beat of being in the middle of this — the commute, the drain, the
    /// grind. Runs every beat the actor stays on task, whether or not the run
    /// ever finishes, and is never refunded.
    /// </summary>
    /// <remarks>
    /// Return false for a beat that kept the actor busy without moving the work
    /// forward — walking to the workplace is the standard case. The beat still
    /// elapses; it just banks no progress, so a long commute shows up as a
    /// worse shift and a smaller payout. That asymmetry (time spent is spent,
    /// time earned has to be earned) is the entire reason a long action has a
    /// cost worth weighing against abandoning it.
    /// </remarks>
    bool Sustain(Entity user, Entity target, IItem? item) => true;

    /// <summary>
    /// Settle a run of this action and pay it out. <paramref name="performance"/>
    /// is 0..1 — focused beats over beats asked for.
    /// </summary>
    /// <remarks>
    /// Rewards only; whatever <see cref="Sustain"/> spent is already gone.
    /// The default routes to <see cref="Execute"/> and ignores performance
    /// entirely, so every instantaneous verb behaves exactly as it always has
    /// and only actions that actually take time need to think about this.
    /// </remarks>
    void Resolve(Entity user, Entity target, IItem? item, double performance) =>
        Execute(user, target, item);

    List<IItem> GetViableItems(Entity user, Entity target);
    void Execute(Entity user, Entity target, IItem? item);
    bool CanActorUse(Entity user);
    bool CanTarget(Entity user, Entity target);
}
