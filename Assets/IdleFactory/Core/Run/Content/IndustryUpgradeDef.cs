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
        public IndustryUpgradeDef(string id, string label, int maxLevel, Curve ipCostCurve, EffectDef effect)
        {
            Id = ContentGuard.Id(id, nameof(id)); Label = ContentGuard.Text(label, nameof(label));
            if (maxLevel < 1 || maxLevel > 1024) throw new ArgumentOutOfRangeException(nameof(maxLevel));
            MaxLevel = maxLevel; IpCostCurve = ipCostCurve ?? throw new ArgumentNullException(nameof(ipCostCurve));
            IpCostTable = new ReadOnlyCollection<long>(ipCostCurve.Precompute(maxLevel));
            Effect = effect ?? throw new ArgumentNullException(nameof(effect));
        }
    }
}
