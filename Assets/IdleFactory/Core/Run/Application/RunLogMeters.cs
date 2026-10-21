using System;

namespace IdleFactory.Run.Application
{
    /// <summary>
    /// Session-log measurements that need history (T364). Derived and never persisted; they only READ the run (after an applied operation) and never
    /// feed back into it. For one currency (Cash, RP or IP) it keeps the suffix minima of the values the session observed, as a small increasing list of
    /// (timeline ms, value): the latest observation below a cost is always in the list, so "how long has this cost been affordable" is the time of the first
    /// kept observation at or above it. The list is a fixed array (no allocation); when it is full, the interior element whose neighbours are closest
    /// relative to its age is dropped, so the resolution stays proportional to the age (the error is a few percent of the answer).
    /// </summary>
    internal sealed class AffordabilityTrace
    {
        private const int Capacity = 32;

        private readonly long[] _time = new long[Capacity];
        private readonly long[] _value = new long[Capacity];
        private int _count;
        private bool _hasOrigin;
        private long _originMs;

        public void Reset()
        {
            _count = 0;
            _hasOrigin = false;
        }

        public void Observe(long timeMs, long value)
        {
            if (!_hasOrigin)
            {
                _hasOrigin = true;
                _originMs = timeMs;
            }

            if (_count > 0 && timeMs < _time[_count - 1]) timeMs = _time[_count - 1];
            while (_count > 0 && _value[_count - 1] > value) _count--;
            if (_count > 0 && _value[_count - 1] == value) return; // an unchanged value keeps its older observation: the crossing is the earliest one
            if (_count == Capacity) DropOne(timeMs);
            _time[_count] = timeMs;
            _value[_count] = value;
            _count++;
        }

        private void DropOne(long nowMs)
        {
            int best = 1;
            double bestScore = double.MaxValue;
            for (int i = 1; i < _count - 1; i++)
            {
                double score = (_time[i + 1] - _time[i - 1] + 1) / (double)(nowMs - _time[i] + 1000);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            for (int i = best; i < _count - 1; i++)
            {
                _time[i] = _time[i + 1];
                _value[i] = _value[i + 1];
            }

            _count--;
        }

        /// <summary>Whole seconds from the first observation at or above <paramref name="cost"/> (after the latest one below it) to <paramref name="nowMs"/>; 0 when unknown.</summary>
        public long SecondsSinceAffordable(long nowMs, long cost)
        {
            if (!_hasOrigin) return 0;
            int below = -1;
            for (int i = _count - 1; i >= 0 && below < 0; i--)
                if (_value[i] < cost) below = i;
            long since = below < 0 ? _originMs : below + 1 < _count ? _time[below + 1] : nowMs;
            long ms = nowMs - since;
            return ms <= 0 ? 0 : ms / 1000L;
        }
    }

    /// <summary>
    /// The per-session meters behind the Purchase, Prestige and Bottleneck records (T364): three <see cref="AffordabilityTrace"/>s, the Factory Sale
    /// eligibility instant and the machine-wait accumulators per resource. Owned by a <see cref="RunSession"/>; main thread only.
    /// </summary>
    internal sealed class RunLogMeters
    {
        public const long TicksPerSecond = 10_000_000L;

        public readonly AffordabilityTrace Cash = new AffordabilityTrace();
        public readonly AffordabilityTrace Rp = new AffordabilityTrace();
        public readonly AffordabilityTrace Ip = new AffordabilityTrace();

        /// <summary>The timeline instant the Factory Sale first counted as available in this run, or -1.</summary>
        public long EligibleSinceMs = -1;

        public readonly long[] MissingInputTicks;
        public readonly long[] StoreFullTicks;

        public RunLogMeters(int resourceCount)
        {
            MissingInputTicks = new long[resourceCount];
            StoreFullTicks = new long[resourceCount];
        }

        public void ResetRun()
        {
            Cash.Reset();
            Rp.Reset();
            EligibleSinceMs = -1;
            Array.Clear(MissingInputTicks, 0, MissingInputTicks.Length);
            Array.Clear(StoreFullTicks, 0, StoreFullTicks.Length);
        }

        public static void Add(long[] target, int index, long ticks)
        {
            long current = target[index];
            target[index] = current > long.MaxValue - ticks ? long.MaxValue : current + ticks;
        }
    }
}
