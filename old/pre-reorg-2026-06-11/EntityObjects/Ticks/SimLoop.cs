namespace EntityObjects.Ticks;

/// <summary>
/// Accumulator loop: real time in, fixed sim ticks out. Pause / "untick" =
/// stop feeding the accumulator. Speed = scale the feed. The sim only ever
/// advances in whole uniform ticks.
/// </summary>
public sealed class SimLoop
{
    private readonly Clock _clock;
    private readonly Wheel _wheel;
    private readonly double _tickSeconds;   // real seconds per sim tick
    private double _accumulator;

    public bool Paused { get; set; }

    public SimLoop(Clock clock, Wheel wheel, double ticksPerSecond)
    {
        _clock = clock; _wheel = wheel;
        _tickSeconds = 1.0 / ticksPerSecond;
    }

    /// <summary>
    /// Feed elapsed real time; run as many fixed ticks as it buys.
    /// Returns how many ticks were advanced (0 when paused or starved).
    /// </summary>
    public int Pump(double realDeltaSeconds)
    {
        if (Paused) return 0;                // not fed → sim frozen ("unticked")
        _accumulator += realDeltaSeconds;
        int advanced = 0;
        while (_accumulator >= _tickSeconds)
        {
            _wheel.ProcessCurrent();          // fire everything due this tick
            _clock.Advance();                 // then move time forward
            _accumulator -= _tickSeconds;
            advanced++;
        }
        return advanced;
    }
}
