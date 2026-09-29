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
        public RunStartDef(long startingCash, IEnumerable<string> initialUnlocks, long removalRefundPpm,
            long minCycleTicks, long maxCombinedSpeedPpm, long maxCombinedOutputFactor, long maxRewardRatioPpm)
        {
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
