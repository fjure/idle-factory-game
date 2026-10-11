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

    /// <summary>One Item ladder entry: the run-wide completion Index at which ItemId is granted.</summary>
    public sealed class ContractItemLadderEntry
    {
        public long Index { get; }
        public string ItemId { get; }
        public ContractItemLadderEntry(long index, string itemId)
        {
            Index = ContentGuard.Quantity(index, nameof(index));
            ItemId = ContentGuard.Id(itemId, nameof(itemId));
        }
    }

    /// <summary>Catalog-level contract Item ladder: entries (index, item) plus the per-run cap. Ordering and ranges are validator rules.</summary>
    public sealed class ContractItemLadder
    {
        public IReadOnlyList<ContractItemLadderEntry> Entries { get; }
        public long PerRunCap { get; }
        public ContractItemLadder(IEnumerable<ContractItemLadderEntry> entries, long perRunCap)
        {
            Entries = new ReadOnlyCollection<ContractItemLadderEntry>(ContentGuard.List(entries, nameof(entries), 0, 1024));
            PerRunCap = ContentGuard.Quantity(perRunCap, nameof(perRunCap));
        }
    }
}
