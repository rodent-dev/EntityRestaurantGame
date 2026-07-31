namespace Engine.Ticks;

// NOTE: an EventTimeline enum (SelfMaintenance/WorldObligation/ActiveDuration,
// tagging how events relate to dormancy — see specs/docs-6-11/EventExamples.md)
// lived here until 2026-07-17. Nothing consumed it; removed until the dormancy
// layer actually lands.

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

    /// <summary>
    /// Run when due. Returns the next absolute fire tick to reschedule,
    /// or null for a one-shot. May enqueue follow-up events via the wheel.
    /// </summary>
    ushort? Fire(Clock clock, Wheel wheel);
}
