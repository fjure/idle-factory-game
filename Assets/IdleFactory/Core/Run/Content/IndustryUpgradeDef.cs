using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IdleFactory.Core.Numerics;

namespace IdleFactory.Run.Content
{
    public sealed class IndustryUpgradeDef
    {
        public string Id { get; }
        public string Label { get; }
        public int MaxLevel { get; }
        public Curve IpCostCurve { get; }
        public IReadOnlyList<long> IpCostTable { get; }
        public EffectDef Effect { get; }
        /// <summary>Per-level decay in ppm (T331); 0 (the legacy default) and 10^6 both mean no decay.</summary>
        public long DecayPpm { get; }
        /// <summary>Practical ceiling level (validated by <c>industry.ceiling.level</c>); legacy default 0 (unset).</summary>
        public long PracticalCeilingLevel { get; }
        /// <summary>Minimum marginal gain in ppm that defines the practical ceiling; legacy default 0.</summary>
        public long MinGainPpm { get; }
        /// <summary>
        /// The derived per-level value table (<see cref="IndustryValueTable"/>), one entry per level, never persisted: the ppm factor of the level for
        /// factor kinds, the added amount for the additive kinds. Without decay every entry is <c>Effect.Value</c>.
        /// </summary>
        public IReadOnlyList<long> ValueTable { get; }
        /// <summary>True when decay makes the levels differ; false keeps the legacy uniform fold of <c>Effect.Value</c> per level.</summary>
        public bool IsDecayed { get; }
        public IndustryUpgradeDef(string id, string label, int maxLevel, Curve ipCostCurve, EffectDef effect,
            long decayPpm = 0, long practicalCeilingLevel = 0, long minGainPpm = 0)
        {
            DecayPpm = ContentGuard.Quantity(decayPpm, nameof(decayPpm));
            if (decayPpm > IndustryValueTable.Ppm) throw new ArgumentOutOfRangeException(nameof(decayPpm));
            PracticalCeilingLevel = ContentGuard.Quantity(practicalCeilingLevel, nameof(practicalCeilingLevel));
            MinGainPpm = ContentGuard.Quantity(minGainPpm, nameof(minGainPpm));
            Id = ContentGuard.Id(id, nameof(id)); Label = ContentGuard.Text(label, nameof(label));
            if (maxLevel < 1 || maxLevel > 1024) throw new ArgumentOutOfRangeException(nameof(maxLevel));
            MaxLevel = maxLevel; IpCostCurve = ipCostCurve ?? throw new ArgumentNullException(nameof(ipCostCurve));
            IpCostTable = new ReadOnlyCollection<long>(ipCostCurve.Precompute(maxLevel));
            Effect = effect ?? throw new ArgumentNullException(nameof(effect));
            if (effect.Kind == "machineOutput")
                throw new ArgumentException("effect.kind.unknown for Industry", nameof(effect));
            if (!IndustryValueTable.TryBuild(effect.Kind, effect.Value, DecayPpm, maxLevel, out long[] table))
                throw new ArgumentException("curve.overflow", nameof(decayPpm));
            ValueTable = new ReadOnlyCollection<long>(table);
            IsDecayed = IndustryValueTable.IsDecaying(DecayPpm);
        }

        /// <summary>
        /// The summed amount of an additive effect at <paramref name="level"/> (levels 1 ... level of <see cref="ValueTable"/>), saturating at 10^18.
        /// Without decay this is the legacy <c>value x level</c>.
        /// </summary>
        public long CumulativeAmount(long level)
        {
            if (level <= 0) return 0;
            if (!IsDecayed)
                return CheckedMath.TryMultiply(Effect.Value, level, out long product) ? product : ExactBound.Value;
            long sum = 0;
            long count = level < ValueTable.Count ? level : ValueTable.Count;
            for (int i = 0; i < count; i++)
                if (!CheckedMath.TryAdd(sum, ValueTable[i], out sum) || sum > ExactBound.Value) return ExactBound.Value;
            return sum;
        }
    }
}
