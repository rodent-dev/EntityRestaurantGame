using Game.Contracts;
using Engine.Ticks;

namespace EntityARestaurantGame.Simulation;

/// <summary>
/// One entity's decision cadence: every interval it weighs its candidates
/// and acts (or idles). SelfMaintenance: if its owner dies or goes inactive
/// the controller lazy-cancels it — the event reaches its bucket and no-ops.
/// </summary>
public sealed class EntityDecisionEvent : IEvent
{
    public ushort FireTick { get; set; }
    public bool Cancelled { get; set; }

    private readonly EntityController _controller;
    private readonly Entity _entity;
    private readonly int _interval;

    public EntityDecisionEvent(EntityController controller, Entity entity, int interval)
    {
        _controller = controller;
        _entity = entity;
        _interval = interval;
    }

    public ushort? Fire(Clock clock, Wheel wheel)
    {
        if (!_entity.IsAlive) return null;

        _controller.ActOnce(_entity);

        // Acting may have retired the owner (e.g. it died to a counter-sweep).
        if (Cancelled) return null;
        // Drift-free: anchor on the scheduled tick, not on now.
        return (ushort)(FireTick + _interval);
    }
}
