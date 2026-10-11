using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
        /// <summary>v3, inert. Per-level decay in ppm; legacy default 0.</summary>
        public long DecayPpm { get; }
        /// <summary>v3, inert. Practical ceiling level; legacy default 0 (unset).</summary>
        public long PracticalCeilingLevel { get; }
        /// <summary>v3, inert. Minimum gain in ppm; legacy default 0.</summary>
        public long MinGainPpm { get; }
        public IndustryUpgradeDef(string id, string label, int maxLevel, Curve ipCostCurve, EffectDef effect,
            long decayPpm = 0, long practicalCeilingLevel = 0, long minGainPpm = 0)
        {
            DecayPpm = ContentGuard.Quantity(decayPpm, nameof(decayPpm));
            PracticalCeilingLevel = ContentGuard.Quantity(practicalCeilingLevel, nameof(practicalCeilingLevel));
            MinGainPpm = ContentGuard.Quantity(minGainPpm, nameof(minGainPpm));
            Id = ContentGuard.Id(id, nameof(id)); Label = ContentGuard.Text(label, nameof(label));
            if (maxLevel < 1 || maxLevel > 1024) throw new ArgumentOutOfRangeException(nameof(maxLevel));
            MaxLevel = maxLevel; IpCostCurve = ipCostCurve ?? throw new ArgumentNullException(nameof(ipCostCurve));
            IpCostTable = new ReadOnlyCollection<long>(ipCostCurve.Precompute(maxLevel));
            Effect = effect ?? throw new ArgumentNullException(nameof(effect));
            if (effect.Kind == "machineOutput")
                throw new ArgumentException("effect.kind.unknown for Industry", nameof(effect));
        }
    }
}
