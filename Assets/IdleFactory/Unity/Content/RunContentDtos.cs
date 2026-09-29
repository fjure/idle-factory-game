using System.Collections.Generic;

namespace IdleFactory.Unity.Content
{
    /// <summary>
    /// Root DTO for a gameplay content document, contract version 2
    /// (specs/003-first-factory-prestige/contracts/gameplay-content-v2.md). Populated only by
    /// <see cref="RunContentReader"/> through a hand-written token walk (no reflection deserialization).
    /// The DTOs carry no semantic meaning: ranges, references and cross-family rules belong to
    /// <see cref="RunContentValidator"/>, and the domain <c>RunCatalog</c> is built from a validated graph later.
    /// Optional members (<c>durationMs</c>, effect and item targets, unlock <c>id</c>/<c>level</c>) are null when
    /// the member is absent; every other member is required by the reader.
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

        public List<RunItemDto> Items { get; set; }

        public List<RunResearchDto> Research { get; set; }

        public RunOverdriveDto Overdrive { get; set; }

        public RunPrestigeDto Prestige { get; set; }

        public List<RunIndustryUpgradeDto> IndustryUpgrades { get; set; }

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

        public long SpeedGrowthPpm { get; set; }

        public List<RunMilestoneDto> Milestones { get; set; }

        public RunUnlockDto Unlock { get; set; }
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

    /// <summary>Contract reward. cash and rp carry a <see cref="Curve"/>; item carries <see cref="ItemId"/> and completion indices.</summary>
    public sealed class RunContractRewardDto
    {
        public string Kind { get; set; }

        public RunCurveDto Curve { get; set; }

        public string ItemId { get; set; }

        public List<int> OnCompletionIndices { get; set; }
    }

    public sealed class RunContractDto
    {
        public string Id { get; set; }

        public string Kind { get; set; }

        public string ResourceId { get; set; }

        public RunCurveDto Quantity { get; set; }

        /// <summary>Optional. Required exactly for timedBar (validator).</summary>
        public long? DurationMs { get; set; }

        public List<RunContractRewardDto> Rewards { get; set; }

        public RunUnlockDto Unlock { get; set; }
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

        /// <summary>Present for machine and resource (and tolerated by the reader for expansion).</summary>
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

    public sealed class RunPrestigeDto
    {
        public long ThresholdFactoryValue { get; set; }

        public RunIpCurveDto IpCurve { get; set; }
    }

    public sealed class RunIndustryUpgradeDto
    {
        public string Id { get; set; }

        public string Label { get; set; }

        public int MaxLevel { get; set; }

        public RunCurveDto IpCost { get; set; }

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
