using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IdleFactory.Run.Content
{
    public sealed class RunStartDef
    {
        public long StartingCash { get; }
        public IReadOnlyList<string> InitialUnlocks { get; }
        public long RemovalRefundPpm { get; }
        public long MinCycleTicks { get; }
        public long MaxCombinedSpeedPpm { get; }
        public long MaxCombinedOutputFactor { get; }
        public long MaxRewardRatioPpm { get; }
        /// <summary>v3, inert. Ascending positive sell step sizes; legacy default empty.</summary>
        public IReadOnlyList<long> SellSteps { get; }
        /// <summary>v3, inert. Whether an "all" sell step exists; legacy default false.</summary>
        public bool SellAll { get; }
        /// <summary>v3, inert. Live gap threshold in ms; legacy default null (unset). Consumers use their own fallback while it is null.</summary>
        public long? LiveGapThresholdMs { get; }
        /// <summary>v3, inert. Hand-mining parameters; legacy default null.</summary>
        public ManualMiningDef ManualMining { get; }
        /// <summary>v3, inert. Minimum run duration in ms for the ladder uptime rule; legacy default 0.</summary>
        public long MinRunDurationMs { get; }
        /// <summary>v3, inert. Ladder uptime maximum in ppm; legacy default 0.</summary>
        public long UptimeMaxPpm { get; }
        public RunStartDef(long startingCash, IEnumerable<string> initialUnlocks, long removalRefundPpm,
            long minCycleTicks, long maxCombinedSpeedPpm, long maxCombinedOutputFactor, long maxRewardRatioPpm,
            IEnumerable<long> sellSteps = null, bool sellAll = false, long? liveGapThresholdMs = null, ManualMiningDef manualMining = null,
            long minRunDurationMs = 0, long uptimeMaxPpm = 0)
        {
            var steps = new List<long>();
            if (sellSteps != null) foreach (long step in sellSteps)
            {
                steps.Add(ContentGuard.Quantity(step, nameof(sellSteps)));
                if (steps.Count > 64) throw new ArgumentException("Too many sell steps.", nameof(sellSteps));
            }
            SellSteps = new ReadOnlyCollection<long>(steps); SellAll = sellAll;
            LiveGapThresholdMs = liveGapThresholdMs.HasValue ? ContentGuard.Quantity(liveGapThresholdMs.Value, nameof(liveGapThresholdMs)) : (long?)null;
            ManualMining = manualMining;
            MinRunDurationMs = ContentGuard.Quantity(minRunDurationMs, nameof(minRunDurationMs));
            UptimeMaxPpm = ContentGuard.Quantity(uptimeMaxPpm, nameof(uptimeMaxPpm));
            StartingCash = ContentGuard.Quantity(startingCash, nameof(startingCash));
            InitialUnlocks = new ReadOnlyCollection<string>(ContentGuard.Ids(initialUnlocks, nameof(initialUnlocks)));
            if (removalRefundPpm < 0 || removalRefundPpm > 999_999) throw new ArgumentOutOfRangeException(nameof(removalRefundPpm), "runstart.refund.out-of-range");
            if (minCycleTicks < 1_000_000 || minCycleTicks > 1_000_000_000_000L) throw new ArgumentOutOfRangeException(nameof(minCycleTicks), "runstart.min-cycle.invalid");
            if (maxCombinedSpeedPpm < Curve.Ppm || maxCombinedSpeedPpm > 1_000_000_000_000L) throw new ArgumentOutOfRangeException(nameof(maxCombinedSpeedPpm), "runstart.speed-bound.invalid");
            if (maxCombinedOutputFactor < 1 || maxCombinedOutputFactor > 1_000_000) throw new ArgumentOutOfRangeException(nameof(maxCombinedOutputFactor), "runstart.output-bound.invalid");
            if (maxRewardRatioPpm < Curve.Ppm || maxRewardRatioPpm > IdleFactory.Core.Numerics.ExactBound.Value) throw new ArgumentOutOfRangeException(nameof(maxRewardRatioPpm), "runstart.reward-ratio.invalid");
            RemovalRefundPpm = removalRefundPpm; MinCycleTicks = minCycleTicks;
            MaxCombinedSpeedPpm = maxCombinedSpeedPpm; MaxCombinedOutputFactor = maxCombinedOutputFactor;
            MaxRewardRatioPpm = maxRewardRatioPpm;
        }
    }
}
