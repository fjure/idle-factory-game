using System.Collections.Generic;

namespace IdleFactory.Unity.Content
{
    /// <summary>
    /// Root DTO for a gameplay content document, contract version 3
    /// (specs/005-economy-contracts/contracts/gameplay-content-v3.md, which evolves the v2 contract in place). Populated only by
    /// <see cref="RunContentReader"/> through a hand-written token walk (no reflection deserialization).
    /// The DTOs carry no semantic meaning: ranges, references and cross-family rules belong to
    /// <see cref="RunContentValidator"/>, and the domain <c>RunCatalog</c> is built from a validated graph later.
    /// Optional members (a contract's <c>durationMs</c> and <c>bonus</c>, effect and item targets, unlock <c>id</c>/<c>level</c>)
    /// are null when the member is absent; every other member is required by the reader.
    /// </summary>
    public sealed class RunContentDocumentDto
    {
        public int ContentSchemaVersion { get; set; }

        public int ContentRevision { get; set; }

        public RunStartDto RunStart { get; set; }

        public List<RunResourceDto> Resources { get; set; }

        public List<RunRecipeDto> Recipes { get; set; }

        public List<RunMachineDto> Machines { get; set; }

        public List<RunStorageLevelDto> StorageLevels { get; set; }

        public List<RunFactoryLevelDto> FactoryLevels { get; set; }

        public List<RunContractDto> Contracts { get; set; }

        public RunContractItemLadderDto ContractItemLadder { get; set; }

        public List<RunItemDto> Items { get; set; }

        public List<RunResearchDto> Research { get; set; }

        public RunOverdriveDto Overdrive { get; set; }

        public RunPrestigeDto Prestige { get; set; }

        public List<RunPrestigeMilestoneDto> Milestones { get; set; }

        public List<RunIndustryUpgradeDto> IndustryUpgrades { get; set; }

        public long ChainDominanceMarginPpm { get; set; }

        public List<RunOfflinePolicyDto> OfflinePolicies { get; set; }
    }

    public sealed class RunStartDto
    {
        public long StartingCash { get; set; }

        public List<string> InitialUnlocks { get; set; }

        public long RemovalRefundPpm { get; set; }

        public long MinCycleTicks { get; set; }

        public long MaxCombinedSpeedPpm { get; set; }

        public long MaxCombinedOutputFactor { get; set; }

        public long MaxRewardRatioPpm { get; set; }

        /// <summary>The integer sell steps in document order. A trailing <c>"all"</c> entry is carried by <see cref="SellAll"/>.</summary>
        public List<long> SellSteps { get; set; }

        /// <summary>True when the <c>sellSteps</c> array ends with the string <c>"all"</c>.</summary>
        public bool SellAll { get; set; }

        public long LiveGapThresholdMs { get; set; }

        public RunManualMiningDto ManualMining { get; set; }

        public long MinRunDurationMs { get; set; }

        public long UptimeMaxPpm { get; set; }
    }

    public sealed class RunManualMiningDto
    {
        public string ResourceId { get; set; }

        public long BaseYieldMilli { get; set; }

        public long MaxActivationsPerSecond { get; set; }

        public long StartWindowMs { get; set; }

        public long ReferenceMinerLevel { get; set; }

        public long MaxShareOfMinerPpm { get; set; }

        public long MiningAdvantageMaxPpm { get; set; }
    }

    /// <summary>Integer curve: v0 = Base, v(n+1) = round(v(n) x GrowthPpm / 10^6), <see cref="Rounding"/> is "ceil" or "floor".</summary>
    public sealed class RunCurveDto
    {
        public long Base { get; set; }

        public long GrowthPpm { get; set; }

        public string Rounding { get; set; }
    }

    /// <summary>
    /// Unlock condition. <see cref="Kind"/> is any string the reader accepted; the closed vocabulary is checked by the
    /// reader for the members a known kind requires and by the validator for the kind itself (<c>unlock.kind.unknown</c>).
    /// </summary>
    public sealed class RunUnlockDto
    {
        public string Kind { get; set; }

        /// <summary>Present for researchPurchased and resourceProduced.</summary>
        public string Id { get; set; }

        /// <summary>Present for factoryLevelAtLeast.</summary>
        public int? Level { get; set; }
    }

    public sealed class RunResourceDto
    {
        public string Id { get; set; }

        public string Label { get; set; }

        public string PresentationKey { get; set; }

        public long BasePrice { get; set; }

        public RunUnlockDto Unlock { get; set; }
    }

    public sealed class RunAmountDto
    {
        public string ResourceId { get; set; }

        public long Amount { get; set; }
    }

    public sealed class RunRecipeDto
    {
        public string Id { get; set; }

        public List<RunAmountDto> Inputs { get; set; }

        public List<RunAmountDto> Outputs { get; set; }

        public long DurationTicks { get; set; }
    }

    public sealed class RunMilestoneDto
    {
        public int Level { get; set; }

        public long SpeedPpm { get; set; }
    }

    public sealed class RunMachineDto
    {
        public string Id { get; set; }

        public string Label { get; set; }

        public string PresentationKey { get; set; }

        public string RecipeId { get; set; }

        public int MaxLevel { get; set; }

        public RunCurveDto Cost { get; set; }

        public RunBuildPriceDto BuildPrice { get; set; }

        public long SpeedGrowthPpm { get; set; }

        public List<RunMilestoneDto> Milestones { get; set; }

        public RunUnlockDto Unlock { get; set; }
    }

    public sealed class RunBuildPriceDto
    {
        public long Base { get; set; }

        public long OwnedGrowthPpm { get; set; }

        public long PrestigeGrowthPpm { get; set; }

        public long Cap { get; set; }
    }

    public sealed class RunStorageLevelDto
    {
        public int Level { get; set; }

        public long Capacity { get; set; }

        public long Cost { get; set; }
    }

    public sealed class RunFactoryLevelDto
    {
        public int Level { get; set; }

        public long Slots { get; set; }

        public long Cost { get; set; }
    }

    /// <summary>
    /// Contract offer (schema v3). <see cref="Objective"/> and <see cref="Class"/> are any strings the reader accepted; the closed
    /// vocabularies are checked by <see cref="RunContentValidator"/> (<c>contract.objective.unknown</c>, <c>contract.class.unknown</c>)
    /// before a catalog is built. The v2 <c>kind</c>, the Cash reward curve and the Item rewards are gone: Cash derives from
    /// <see cref="RewardRatioPpm"/> and Items from the catalog <c>contractItemLadder</c>.
    /// </summary>
    public sealed class RunContractDto
    {
        public string Id { get; set; }

        public string Objective { get; set; }

        public string Class { get; set; }

        public string ResourceId { get; set; }

        public RunCurveDto Quantity { get; set; }

        /// <summary>Optional. A present value makes the offer timed.</summary>
        public long? DurationMs { get; set; }

        public long RewardRatioPpm { get; set; }

        public long CooldownMs { get; set; }

        /// <summary>The RP reward curve by completion count.</summary>
        public RunCurveDto RpReward { get; set; }

        /// <summary>Optional. Present exactly for bonus-class offers (rule <c>contract.bonus.fields</c>, a later task).</summary>
        public RunBonusDto Bonus { get; set; }

        public RunUnlockDto Unlock { get; set; }
    }

    public sealed class RunBonusDto
    {
        public RunUnlockDto Trigger { get; set; }

        public long WindowMs { get; set; }
    }

    public sealed class RunContractItemLadderEntryDto
    {
        public long Index { get; set; }

        public string ItemId { get; set; }
    }

    public sealed class RunContractItemLadderDto
    {
        public List<RunContractItemLadderEntryDto> Entries { get; set; }

        public long PerRunCap { get; set; }
    }

    public sealed class RunItemTargetDto
    {
        public string Kind { get; set; }

        /// <summary>Present for machineType.</summary>
        public List<string> AllowedMachineIds { get; set; }
    }

    public sealed class RunEffectTargetDto
    {
        public string Kind { get; set; }

        /// <summary>Present for machine and resource only.</summary>
        public string Id { get; set; }
    }

    /// <summary>
    /// Effect. The value lives in exactly one JSON member whose name depends on the kind and source
    /// (<c>speedPpm</c> for Item speed, <c>valuePerLevelPpm</c>, <c>factor</c>, <c>valuePerLevel</c>);
    /// <see cref="ValueMember"/> records which one was written.
    /// </summary>
    public sealed class RunEffectDto
    {
        public string Kind { get; set; }

        /// <summary>Null for Item effects and for kinds without a target.</summary>
        public RunEffectTargetDto Target { get; set; }

        public string ValueMember { get; set; }

        public long Value { get; set; }
    }

    public sealed class RunItemDto
    {
        public string Id { get; set; }

        public int Tier { get; set; }

        public string Label { get; set; }

        public string PresentationKey { get; set; }

        public long DurationMs { get; set; }

        public RunItemTargetDto Target { get; set; }

        public RunEffectDto Effect { get; set; }
    }

    public sealed class RunPrerequisiteDto
    {
        /// <summary>Research ID. The JSON member is <c>id</c>.</summary>
        public string Id { get; set; }

        public int Level { get; set; }
    }

    public sealed class RunResearchDto
    {
        public string Id { get; set; }

        public string Label { get; set; }

        public int MaxLevel { get; set; }

        public RunCurveDto RpCost { get; set; }

        public List<RunPrerequisiteDto> Prerequisites { get; set; }

        public RunEffectDto Effect { get; set; }
    }

    public sealed class RunOverdriveDto
    {
        public long DurationMs { get; set; }

        public long CooldownMs { get; set; }

        public long SpeedPpm { get; set; }

        public RunUnlockDto Unlock { get; set; }
    }

    public sealed class RunIpCurveDto
    {
        public string Kind { get; set; }

        public long Coefficient { get; set; }

        public long Divisor { get; set; }
    }

    public sealed class RunThresholdDto
    {
        public long Base { get; set; }

        public long GrowthPpm { get; set; }

        public long MaxIndex { get; set; }
    }

    public sealed class RunPrestigeDto
    {
        public RunThresholdDto Threshold { get; set; }

        public RunIpCurveDto IpCurve { get; set; }
    }

    public sealed class RunPrestigeMilestoneDto
    {
        public long FractionPpm { get; set; }
    }

    public sealed class RunIndustryUpgradeDto
    {
        public string Id { get; set; }

        public string Label { get; set; }

        public int MaxLevel { get; set; }

        public RunCurveDto IpCost { get; set; }

        public long DecayPpm { get; set; }

        public long PracticalCeilingLevel { get; set; }

        public long MinGainPpm { get; set; }

        public RunEffectDto Effect { get; set; }
    }

    public sealed class RunOfflinePolicyDto
    {
        public int Revision { get; set; }

        public long EfficiencyNumerator { get; set; }

        public long EfficiencyDenominator { get; set; }

        public long MaxCreditedTicks { get; set; }
    }
}
