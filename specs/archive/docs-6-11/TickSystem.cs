using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

// =====================================================================
//  HEADLESS TICK-SYSTEM SKELETON
//  No game logic. Just the timing substrate: a clock that ticks (and can
//  be paused / "unticked"), a flat timer wheel, queueable IEvents, an
//  accumulator-driven fixed-step loop, struct render cues, lazy-cancel
//  interrupts, and two reference events.
//
//  Build:  dotnet new console  ->  replace Program.cs with this  ->  dotnet run
// =====================================================================

namespace TickSim
{
    // -----------------------------------------------------------------
    //  CLOCK
    //  A ushort that wraps on overflow. Overflow IS the mechanism, not a
    //  bug to guard against. Total time is never stored absolutely; the
    //  wheel and the wrap-aware comparison below are all we need.
    // -----------------------------------------------------------------
    public sealed class Clock
    {
        public ushort Tick { get; private set; }

        // Advance one tick. The wrap from 65535 -> 0 happens for free.
        public void Advance() => Tick++;

        // Wrap-aware "is target due relative to now?" using signed-offset
        // serial arithmetic. Treats the nearer half of the range as the
        // future and the other half as the past. HARD INVARIANT: nothing
        // may be scheduled more than 32767 ticks ahead, or future/past
        // become ambiguous.
        public static bool IsDue(ushort target, ushort now)
            => (short)(target - now) <= 0;
    }

    // -----------------------------------------------------------------
    //  RENDER CUE  (value type -> no heap allocation in the hot path)
    //  The only "output" of this headless build. A cue carries the four
    //  anchors the renderer needs: origin, destination, start tick, end
    //  tick. Position at any moment is a pure lerp of these against the
    //  clock -- frame-rate independent, cannot drift.
    // -----------------------------------------------------------------
    public readonly struct RenderCue
    {
        public readonly string Tag;      // which visual (e.g. "fireball_c")
        public readonly Vector3 Origin;  // resampled per-segment if origin moves
        public readonly Vector3 Dest;    // constant for a point-and-click target
        public readonly ushort StartTick;
        public readonly ushort EndTick;

        public RenderCue(string tag, Vector3 origin, Vector3 dest, ushort start, ushort end)
        {
            Tag = tag; Origin = origin; Dest = dest; StartTick = start; EndTick = end;
        }

        // The whole position formula. t = how far through the segment we
        // are, derived purely from the clock vs the two tick anchors.
        // Linear here; run t through an easing fn later for arcs/accel
        // without touching timing.
        public Vector3 PositionAt(ushort now)
        {
            int span = (ushort)(EndTick - StartTick);
            if (span <= 0) return Dest;
            int elapsed = (ushort)(now - StartTick);
            float t = Math.Clamp(elapsed / (float)span, 0f, 1f);
            return Vector3.Lerp(Origin, Dest, t);
        }
    }

    // Where cues land in a headless app. Swap this for a real renderer
    // later; the sim never knows the difference.
    public static class CueSink
    {
        public static void Emit(in RenderCue c, ushort now)
        {
            Vector3 p = c.PositionAt(now);
            Console.WriteLine(
                $"    [cue t={now,5}] {c.Tag,-12} pos=({p.X,6:0.00},{p.Y,6:0.00},{p.Z,6:0.00})  " +
                $"({c.StartTick}->{c.EndTick})");
        }
    }

    // -----------------------------------------------------------------
    //  IEVENT  (the only tickable interface; events ARE the tickables)
    // -----------------------------------------------------------------
    public interface IEvent
    {
        // Set when scheduled into the wheel.
        ushort FireTick { get; set; }

        // Lazy-cancellation interrupt: a cancelled event still reaches the
        // bucket but no-ops on Fire. Cheapest possible interrupt; nothing
        // has mutated state yet, so there is nothing to undo.
        bool Cancelled { get; set; }

        // Run when due. Returns the next absolute fire tick to reschedule,
        // or null for a one-shot. May enqueue follow-up events via the wheel.
        ushort? Fire(Clock clock, Wheel wheel);
    }

    // -----------------------------------------------------------------
    //  WHEEL  (flat 65536-slot timer wheel)
    //  Advancing is O(1): step the index, drain that one bucket. Idle
    //  ticks are free. Index wrap == tick wrap, automatically.
    // -----------------------------------------------------------------
    public sealed class Wheel
    {
        public const int Size = 65536;               // one slot per ushort value
        public const int MaxHorizon = 32767;          // half-range scheduling cap

        private readonly List<IEvent>[] _slots = new List<IEvent>[Size];
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

        // Schedule relative to now.
        public void ScheduleIn(IEvent e, int ticksAhead)
            => Schedule(e, (ushort)(_clock.Tick + ticksAhead));

        // Process exactly the current tick's bucket. Insertion order within
        // the bucket is preserved -> deterministic tie-break for same-tick
        // collisions (reproducible saves/replays later).
        public void ProcessCurrent()
        {
            var bucket = _slots[_clock.Tick];
            if (bucket == null || bucket.Count == 0) return;

            // Snapshot count: events firing now may schedule into future
            // buckets, but must not extend this bucket mid-drain.
            int n = bucket.Count;
            for (int i = 0; i < n; i++)
            {
                IEvent e = bucket[i];
                if (e.Cancelled) continue;            // interrupt: silent no-op
                ushort? next = e.Fire(_clock, this);
                if (next.HasValue) Schedule(e, next.Value);
            }
            bucket.Clear();                            // reuse the list; no realloc
        }
    }

    // -----------------------------------------------------------------
    //  LOOP  (accumulator: real time in, fixed sim ticks out)
    //  Pause / "untick" = stop feeding the accumulator. Speed = scale the
    //  feed. The sim only ever advances in whole uniform ticks.
    // -----------------------------------------------------------------
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

        // Feed elapsed real time; run as many fixed ticks as it buys.
        public void Pump(double realDeltaSeconds)
        {
            if (Paused) return;                  // not fed -> sim frozen ("unticked")
            _accumulator += realDeltaSeconds;
            while (_accumulator >= _tickSeconds)
            {
                _wheel.ProcessCurrent();          // fire everything due this tick
                _clock.Advance();                 // then move time forward
                _accumulator -= _tickSeconds;
            }
            // A real renderer would draw here using leftover _accumulator
            // as an interpolation fraction. Headless: cues already printed.
        }
    }

    // =================================================================
    //  REFERENCE EVENT 1 — HUNGER PULSE
    //  Pure self-rescheduling multi-rate event. No cue. Demonstrates that
    //  a slow system is simply absent from 99 of every 100 buckets.
    //  Reschedules off scheduled-time + interval (drift-free).
    // =================================================================
    public sealed class HungerPulse : IEvent
    {
        public ushort FireTick { get; set; }
        public bool Cancelled { get; set; }
        private const int Interval = 100;
        private int _count;

        public ushort? Fire(Clock clock, Wheel wheel)
        {
            _count++;
            Console.WriteLine($"[tick {clock.Tick,5}] HUNGER pulse #{_count} (every {Interval})");
            // Drift-free: next = scheduledTick + interval, not now + interval.
            return (ushort)(FireTick + Interval);
        }
    }

    // =================================================================
    //  REFERENCE EVENT 2 — CHANNEL
    //  The "1-tick truth event + 96-tick cosmetic channel" pattern.
    //  - One authoritative IMPACT event on a single fixed tick (sim truth).
    //  - A chain of re-anchor cues across the channel: each segment reads
    //    the CURRENT origin (caster may move) and lerps to the FIXED dest.
    //    Moving origin handled by resampling -> each segment is a clean
    //    fixed-anchor lerp that cannot desync.
    //  Cancellable mid-channel via the lazy flag (interrupt).
    // =================================================================
    public sealed class ChannelImpact : IEvent
    {
        public ushort FireTick { get; set; }
        public bool Cancelled { get; set; }
        private readonly Vector3 _dest;

        public ChannelImpact(Vector3 dest) => _dest = dest;

        public ushort? Fire(Clock clock, Wheel wheel)
        {
            Console.WriteLine($"[tick {clock.Tick,5}] *** IMPACT at " +
                $"({_dest.X},{_dest.Y},{_dest.Z}) -- authoritative, single tick ***");
            return null; // one-shot
        }
    }

    // Emits one re-anchor cue, then schedules the next segment until the
    // channel ends. Holds a live reference to the (possibly moving) caster.
    public sealed class ChannelSegment : IEvent
    {
        public ushort FireTick { get; set; }
        public bool Cancelled { get; set; }

        private readonly Func<Vector3> _liveOrigin;  // resampled each segment
        private readonly Vector3 _dest;
        private readonly ushort _start;
        private readonly ushort _end;
        private readonly int _step;

        public ChannelSegment(Func<Vector3> liveOrigin, Vector3 dest,
                              ushort start, ushort end, int step)
        {
            _liveOrigin = liveOrigin; _dest = dest;
            _start = start; _end = end; _step = step;
        }

        public ushort? Fire(Clock clock, Wheel wheel)
        {
            // Resample origin NOW -> this segment is a fixed-anchor lerp
            // even though the caster moves over the channel's life.
            var cue = new RenderCue("fireball_c", _liveOrigin(), _dest, _start, _end);
            CueSink.Emit(cue, clock.Tick);

            ushort next = (ushort)(clock.Tick + _step);
            if (Clock.IsDue(_end, next)) return null;   // channel finished
            // Reschedule the same segment object for the next re-anchor.
            return next;
        }
    }

    // =================================================================
    //  DEMO DRIVER
    // =================================================================
    public static class Program
    {
        public static void Main()
        {
            var clock = new Clock();
            var wheel = new Wheel(clock);
            var loop  = new SimLoop(clock, wheel, ticksPerSecond: 1000); // fast for demo

            // A moving caster: origin drifts +X each tick. The channel will
            // resample this live, so segments re-anchor automatically.
            Vector3 casterPos = new Vector3(0, 0, 0);
            Func<Vector3> liveOrigin = () => casterPos;

            // Schedule the hunger pulse (multi-rate, no cue).
            wheel.ScheduleIn(new HungerPulse(), 100);

            // Schedule a channel: starts ~tick 10, lasts 96 ticks, re-anchors
            // every 16 ticks, impact lands on the final tick (sim truth).
            ushort chStart = 10, chEnd = (ushort)(10 + 96);
            var dest = new Vector3(50, 0, 20);
            wheel.Schedule(new ChannelSegment(liveOrigin, dest, chStart, chEnd, step: 16), chStart);
            wheel.Schedule(new ChannelImpact(dest), chEnd);

            // Run ~300 ticks of wall-clock-paced simulation.
            Console.WriteLine("=== sim start ===");
            const double frame = 0.005; // pretend 5ms real frames
            for (int f = 0; f < 70; f++)
            {
                loop.Pump(frame);
                casterPos.X += 0.5f * (float)(frame * 1000); // caster drifts
                Thread.Sleep(1);
            }
            Console.WriteLine("=== sim end ===");

            // To see an INTERRUPT: set the ChannelSegment's .Cancelled = true
            // at any point (keep a reference to it) and remaining re-anchor
            // cues silently stop -- nothing to undo, because nothing mutated.
        }
    }
}
