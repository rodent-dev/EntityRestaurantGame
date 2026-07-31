using EntityObjects.Ticks;

namespace EntityARestaurantGame.Simulation;

/// <summary>
/// The whole-world "everyone gets hungrier" beat (EventExamples §1).
/// Pure self-rescheduling multi-rate event — absent from every bucket it
/// isn't due in. WorldObligation: the world keeps stirring regardless of
/// who is watching.
/// </summary>
public sealed class HungerPulseEvent : IEvent
{
    public ushort FireTick { get; set; }
    public bool Cancelled { get; set; }
    public EventTimeline Timeline => EventTimeline.WorldObligation;

    private readonly EntityController _controller;

    public HungerPulseEvent(EntityController controller) => _controller = controller;

    public ushort? Fire(Clock clock, Wheel wheel)
    {
        _controller.OnHungerPulse();
        // Drift-free: next = scheduledTick + interval, not now + interval.
        return (ushort)(FireTick + EntityController.HungerPulseInterval);
    }
}
