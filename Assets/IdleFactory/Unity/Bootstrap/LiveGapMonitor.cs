using System;
using IdleFactory.Core.Numerics;

namespace IdleFactory.Unity.Bootstrap
{
    /// <summary>How one observed UTC frame relates to the previous one (FR-032).</summary>
    public enum LiveGapVerdict
    {
        /// <summary>The wall delta is at most the threshold (a delta equal to the threshold is simulated live), or no previous frame is known.</summary>
        Live,

        /// <summary>The wall delta is strictly above the threshold: a departure at the previous frame and a standard offline return.</summary>
        Gap,

        /// <summary>The wall clock moved backwards. Nothing is credited and the live simulation carries on; later deltas run from the new clock.</summary>
        BackwardClock,
    }

    /// <summary>
    /// The live-gap decision of <see cref="FactoryRuntimeHost"/> (T337, FR-032, F5-R10), kept free of scenes and of the engine clock so
    /// it is tested on plain milliseconds. It sees only UTC reads, so the real wall delta decides: a development time scale
    /// multiplies the simulated delta after this check and can never turn a frame into a gap. The threshold is the one shared value
    /// <see cref="IdleFactory.Run.Application.RunPersistence.LiveGapThresholdMs"/> (the autosave interval reads the same).
    /// </summary>
    public sealed class LiveGapMonitor
    {
        private long previousUtcMs;
        private bool hasPrevious;

        public LiveGapMonitor(long thresholdMs)
        {
            if (thresholdMs < 1) throw new ArgumentOutOfRangeException(nameof(thresholdMs));
            ThresholdMs = thresholdMs;
        }

        /// <summary>The threshold in milliseconds; a wall delta strictly above it is a gap.</summary>
        public long ThresholdMs { get; }

        /// <summary>The UTC read of the latest observed frame, valid while <see cref="HasPrevious"/>.</summary>
        public long PreviousUtcMs => previousUtcMs;

        public bool HasPrevious => hasPrevious;

        /// <summary>Starts a new reference without a verdict: the start of an active segment, where an earlier absence is not this check's.</summary>
        public void Reset(long utcMs)
        {
            previousUtcMs = utcMs;
            hasPrevious = true;
        }

        /// <summary>Forgets the reference (an unreadable clock): the next observation starts a new one.</summary>
        public void Clear() => hasPrevious = false;

        /// <summary>
        /// Classifies <paramref name="utcMs"/> against the previous frame and makes it the new reference. On <see cref="LiveGapVerdict.Gap"/>,
        /// <paramref name="previousFrameUtcMs"/> is the departure instant to hand to <c>RecordSuspension(atUtcMs)</c>.
        /// </summary>
        public LiveGapVerdict Observe(long utcMs, out long previousFrameUtcMs)
        {
            previousFrameUtcMs = previousUtcMs;
            if (!hasPrevious)
            {
                Reset(utcMs);
                return LiveGapVerdict.Live;
            }

            long previous = previousUtcMs;
            previousUtcMs = utcMs;
            if (utcMs < previous) return LiveGapVerdict.BackwardClock;

            // A distance that does not fit a long is certainly above any threshold.
            if (!CheckedMath.TrySubtract(utcMs, previous, out long deltaMs)) return LiveGapVerdict.Gap;
            return deltaMs > ThresholdMs ? LiveGapVerdict.Gap : LiveGapVerdict.Live;
        }
    }
}
