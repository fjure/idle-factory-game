using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IdleFactory.Run.Content
{
    public sealed class ResearchPrerequisite
    {
        public string ResearchId { get; }
        public int Level { get; }
        public ResearchPrerequisite(string researchId, int level)
        {
            ResearchId = ContentGuard.Id(researchId, nameof(researchId));
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            Level = level;
        }
    }

    public sealed class ResearchDef
    {
        public string Id { get; }
        public string Label { get; }
        public int MaxLevel { get; }
        public Curve RpCostCurve { get; }
        public IReadOnlyList<long> RpCostTable { get; }
        public IReadOnlyList<ResearchPrerequisite> Prerequisites { get; }
        public EffectDef Effect { get; }
        public ResearchDef(string id, string label, int maxLevel, Curve rpCostCurve,
            IEnumerable<ResearchPrerequisite> prerequisites, EffectDef effect)
        {
            Id = ContentGuard.Id(id, nameof(id)); Label = ContentGuard.Text(label, nameof(label));
            if (maxLevel < 1 || maxLevel > 1024) throw new ArgumentOutOfRangeException(nameof(maxLevel));
            MaxLevel = maxLevel; RpCostCurve = rpCostCurve ?? throw new ArgumentNullException(nameof(rpCostCurve));
            RpCostTable = new ReadOnlyCollection<long>(rpCostCurve.Precompute(maxLevel));
            Prerequisites = new ReadOnlyCollection<ResearchPrerequisite>(ContentGuard.List(prerequisites, nameof(prerequisites)));
            Effect = effect ?? throw new ArgumentNullException(nameof(effect));
            if (effect.Kind != "machineSpeed" && effect.Kind != "machineOutput" &&
                effect.Kind != "salePrice" && effect.Kind != "upgradeCost" && effect.Kind != "manualYield")
                throw new ArgumentException("effect.kind.unknown for Research", nameof(effect));
            if (effect.Kind == "machineOutput" && effect.TargetKind != "machine")
                throw new ArgumentException("effect.target.invalid for Research", nameof(effect));
        }
    }
}
