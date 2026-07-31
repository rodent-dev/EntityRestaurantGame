namespace EntityObjects.Ticks;

/// <summary>
/// Routes how an event relates to dormancy (see EventExamples.md):
/// SelfMaintenance is droppable and re-derivable, WorldObligation persists
/// through dormancy, ActiveDuration only matters while the owner is live.
/// Not yet consumed by the wheel — it tags intent for the dormancy layer.
/// </summary>
public enum EventTimeline
{
    SelfMaintenance,
    WorldObligation,
    ActiveDuration
}

/// <summary>
/// The only tickable interface; events ARE the tickables. The tick is the
/// trigger — events fire by arriving at their bucket, never by comparing
/// tick numbers.
/// </summary>
public interface IEvent
{
    /// <summary>Set when scheduled into the wheel.</summary>
    ushort FireTick { get; set; }

    /// <summary>
    /// Lazy-cancellation interrupt: a cancelled event still reaches the
    /// bucket but no-ops on Fire. Cheapest possible interrupt; nothing has
    /// mutated state yet, so there is nothing to undo.
    /// </summary>
    bool Cancelled { get; set; }

    EventTimeline Timeline { get; }

    /// <summary>
    /// Run when due. Returns the next absolute fire tick to reschedule,
    /// or null for a one-shot. May enqueue follow-up events via the wheel.
    /// </summary>
    ushort? Fire(Clock clock, Wheel wheel);
}
