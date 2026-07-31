using Engine.Ticks;
using Game.Contracts;

namespace EntityARestaurantGame.Simulation;

/// <summary>
/// The amplifier-with-decay, finally spent: an obsession pins a desire's
/// want high regardless of recompute, and this buff-as-IEvent walks it back
/// down the tick wheel until the rule (stateless recompute) is in force
/// again. Deliberately the only accrual in the desire system.
/// </summary>
public sealed class WantSpikeEvent : IEvent
{
    public const int DecayInterval = 10;      // ticks between decay steps
    public const double DecayFactor = 0.65;   // pinned want retained per step
    public const double FadeFloor = 0.3;      // below this the pin releases

    public ushort FireTick { get; set; }
    public bool Cancelled { get; set; }

    private readonly EntityController _controller;
    private readonly Entity _owner;
    private readonly DesireReferent _referent;

    public WantSpikeEvent(EntityController controller, Entity owner, DesireReferent referent)
    {
        _controller = controller;
        _owner = owner;
        _referent = referent;
    }

    public ushort? Fire(Clock clock, Wheel wheel)
    {
        _referent.PinnedWant *= DecayFactor;

        if (_referent.PinnedWant < FadeFloor)
        {
            _referent.PinnedWant = 0;
            _controller.Announce($"{_owner.DisplayName}'s obsession with the {_referent.ObjectName} fades", _owner);
            return null;
        }

        return (ushort)(FireTick + DecayInterval);
    }
}
