namespace EntityObjects.Ticks;

/// <summary>
/// Flat 65536-slot timer wheel. Advancing is O(1): step the index, drain
/// that one bucket. Idle ticks are free. Index wrap == tick wrap, automatically.
/// </summary>
public sealed class Wheel
{
    public const int Size = 65536;                // one slot per ushort value
    public const int MaxHorizon = 32767;          // half-range scheduling cap

    private readonly List<IEvent>?[] _slots = new List<IEvent>?[Size];
    private readonly Clock _clock;

    public Wheel(Clock clock) => _clock = clock;

    public void Schedule(IEvent e, ushort fireTick)
    {
        // Guard the one invariant that breaks silently if violated.
        int ahead = (ushort)(fireTick - _clock.Tick);
        if (ahead > MaxHorizon)
            throw new ArgumentException(
                $"Cannot schedule {ahead} ticks ahead; max horizon is {MaxHorizon}. " +
                "Re-arm in hops instead.");

        e.FireTick = fireTick;
        (_slots[fireTick] ??= new List<IEvent>()).Add(e);
    }

    /// <summary>Schedule relative to now.</summary>
    public void ScheduleIn(IEvent e, int ticksAhead)
        => Schedule(e, (ushort)(_clock.Tick + ticksAhead));

    /// <summary>
    /// Process exactly the current tick's bucket. Insertion order within the
    /// bucket is preserved → deterministic tie-break for same-tick collisions.
    /// Returns the number of events that actually fired (cancelled events
    /// no-op silently and don't count).
    /// </summary>
    public int ProcessCurrent()
    {
        var bucket = _slots[_clock.Tick];
        if (bucket == null || bucket.Count == 0) return 0;

        // Snapshot count: events firing now may schedule into future
        // buckets, but must not extend this bucket mid-drain.
        int n = bucket.Count;
        int fired = 0;
        for (int i = 0; i < n; i++)
        {
            IEvent e = bucket[i];
            if (e.Cancelled) continue;            // interrupt: silent no-op
            fired++;
            ushort? next = e.Fire(_clock, this);
            if (next.HasValue && !e.Cancelled) Schedule(e, next.Value);
        }
        bucket.Clear();                            // reuse the list; no realloc
        return fired;
    }
}
