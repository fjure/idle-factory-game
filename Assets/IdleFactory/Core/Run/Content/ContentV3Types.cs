using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IdleFactory.Run.Content
{
    // Schema v3 content records (specs/005-economy-contracts/data-model.md > Content). Immutable, engine-free, all numbers long.
    // They are inert in T312: no rule, simulator, command, reader, validator or snapshot reads them yet. Semantic ranges
    // (the data-model Validation column) are validator rule codes of a later task, so constructors only check structure.

    /// <summary>Hand-mining parameters of the run start (D-O, D-P). All values are carried as given.</summary>
    public sealed class ManualMiningDef
    {
        public string ResourceId { get; }
        public long BaseYieldMilli { get; }
        public long MaxActivationsPerSecond { get; }
        public long StartWindowMs { get; }
        public long ReferenceMinerLevel { get; }
        public long MaxShareOfMinerPpm { get; }
        public long MiningAdvantageMaxPpm { get; }
        public ManualMiningDef(string resourceId, long baseYieldMilli, long maxActivationsPerSecond, long startWindowMs,
            long referenceMinerLevel, long maxShareOfMinerPpm, long miningAdvantageMaxPpm)
        {
            ResourceId = ContentGuard.Id(resourceId, nameof(resourceId));
            BaseYieldMilli = ContentGuard.Quantity(baseYieldMilli, nameof(baseYieldMilli));
            MaxActivationsPerSecond = ContentGuard.Quantity(maxActivationsPerSecond, nameof(maxActivationsPerSecond));
            StartWindowMs = ContentGuard.Quantity(startWindowMs, nameof(startWindowMs));
            ReferenceMinerLevel = ContentGuard.Quantity(referenceMinerLevel, nameof(referenceMinerLevel));
            MaxShareOfMinerPpm = ContentGuard.Quantity(maxShareOfMinerPpm, nameof(maxShareOfMinerPpm));
            MiningAdvantageMaxPpm = ContentGuard.Quantity(miningAdvantageMaxPpm, nameof(miningAdvantageMaxPpm));
        }
    }

    /// <summary>Bonus-class contract trigger and its availability window.</summary>
    public sealed class BonusDef
    {
        public UnlockCondition Trigger { get; }
        public long WindowMs { get; }
        public BonusDef(UnlockCondition trigger, long windowMs)
        {
            Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
            WindowMs = ContentGuard.Quantity(windowMs, nameof(windowMs));
        }
    }

    /// <summary>Machine build price parameters: base, per-owned and per-prestige growth in ppm, and a cap.</summary>
    public sealed class BuildPriceDef
    {
        public long Base { get; }
        public long OwnedGrowthPpm { get; }
        public long PrestigeGrowthPpm { get; }
        public long Cap { get; }
        public BuildPriceDef(long @base, long ownedGrowthPpm, long prestigeGrowthPpm, long cap)
        {
            Base = ContentGuard.Quantity(@base, nameof(@base));
            OwnedGrowthPpm = ContentGuard.Quantity(ownedGrowthPpm, nameof(ownedGrowthPpm));
            PrestigeGrowthPpm = ContentGuard.Quantity(prestigeGrowthPpm, nameof(prestigeGrowthPpm));
            Cap = ContentGuard.Quantity(cap, nameof(cap));
        }
    }

    /// <summary>Prestige threshold growth: Base, GrowthPpm per prestige index, index clamped at MaxIndex.</summary>
    public sealed class ThresholdDef
    {
        public long Base { get; }
        public long GrowthPpm { get; }
        public long MaxIndex { get; }
        public ThresholdDef(long @base, long growthPpm, long maxIndex)
        {
            Base = ContentGuard.Quantity(@base, nameof(@base));
            GrowthPpm = ContentGuard.Quantity(growthPpm, nameof(growthPpm));
            MaxIndex = ContentGuard.Quantity(maxIndex, nameof(maxIndex));
        }
    }

    /// <summary>Prestige milestone at a fraction (ppm) of the threshold.</summary>
    public sealed class MilestoneDef
    {
        public long FractionPpm { get; }
        public MilestoneDef(long fractionPpm)
        {
            FractionPpm = ContentGuard.Quantity(fractionPpm, nameof(fractionPpm));
        }
    }

    /// <summary>Catalog-level contract Item ladder: parallel completion Indices and ItemIds, plus the per-run cap.</summary>
    public sealed class ContractItemLadder
    {
        public IReadOnlyList<long> Indices { get; }
        public IReadOnlyList<string> ItemIds { get; }
        public long PerRunCap { get; }
        public ContractItemLadder(IEnumerable<long> indices, IEnumerable<string> itemIds, long perRunCap)
        {
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            if (itemIds == null) throw new ArgumentNullException(nameof(itemIds));
            var indexList = new List<long>(indices);
            var idList = new List<string>();
            foreach (string id in itemIds) idList.Add(ContentGuard.Id(id, nameof(itemIds)));
            if (indexList.Count != idList.Count) throw new ArgumentException("Ladder indices and item IDs must have equal length.", nameof(itemIds));
            if (indexList.Count > 1024) throw new ArgumentException("Too many ladder entries.", nameof(indices));
            foreach (long index in indexList) ContentGuard.Quantity(index, nameof(indices));
            Indices = new ReadOnlyCollection<long>(indexList);
            ItemIds = new ReadOnlyCollection<string>(idList);
            PerRunCap = ContentGuard.Quantity(perRunCap, nameof(perRunCap));
        }
    }
}
