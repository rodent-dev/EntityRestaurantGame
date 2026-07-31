namespace Engine.Ticks;

/// <summary>
/// The simulation clock: a ushort that wraps on overflow. Overflow IS the
/// mechanism, not a bug to guard against. Total time is never stored
/// absolutely; the wheel and the wrap-aware comparison below are all we need.
/// </summary>
public sealed class Clock
{
    public ushort Tick { get; private set; }

    /// <summary>Advance one tick. The wrap from 65535 → 0 happens for free.</summary>
    public void Advance() => Tick++;

    /// <summary>
    /// Wrap-aware "is target due relative to now?" using signed-offset serial
    /// arithmetic. Treats the nearer half of the range as the future and the
    /// other half as the past. HARD INVARIANT: nothing may be scheduled more
    /// than 32767 ticks ahead, or future/past become ambiguous.
    /// </summary>
    public static bool IsDue(ushort target, ushort now)
        => (short)(target - now) <= 0;
}
