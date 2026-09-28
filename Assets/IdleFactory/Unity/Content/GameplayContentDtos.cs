using System.Collections.Generic;

namespace IdleFactory.Unity.Content
{
    /// <summary>
    /// Root DTO for a gameplay content document (contract v1). Populated only by
    /// <see cref="GameplayContentReader"/>. Fields mirror
    /// specs/002-json-content-authoring/data-model.md "DTOs" exactly; no semantic
    /// meaning is attached here (see T073 for identity/reference/domain validation).
    /// </summary>
    public sealed class GameplayContentDocumentDto
    {
        public int ContentSchemaVersion { get; set; }

        public List<ResourceDto> Resources { get; set; }

        public List<RecipeDto> Recipes { get; set; }

        public List<BuildingDto> Buildings { get; set; }

        public List<OfflinePolicyDto> OfflinePolicies { get; set; }
    }

    public sealed class ResourceDto
    {
        public string Id { get; set; }

        public string DevelopmentLabel { get; set; }

        /// <summary>Optional. Omitted means "no presentation key". Never null when present.</summary>
        public string PresentationKey { get; set; }

        public bool IsDevelopmentOnly { get; set; }
    }

    public sealed class RecipeDto
    {
        public string Id { get; set; }

        public List<ResourceAmountDto> Inputs { get; set; }

        public List<ResourceAmountDto> Outputs { get; set; }

        public long DurationTicks { get; set; }

        public bool IsDevelopmentOnly { get; set; }
    }

    public sealed class ResourceAmountDto
    {
        public string ResourceId { get; set; }

        public long Amount { get; set; }
    }

    public sealed class BuildingDto
    {
        public string Id { get; set; }

        public int FootprintWidth { get; set; }

        public int FootprintHeight { get; set; }

        public List<int> AllowedOrientations { get; set; }

        /// <summary>Optional. Omitted means the building has no recipe.</summary>
        public string RecipeId { get; set; }

        /// <summary>Optional. A null/omitted buffer means "no buffer" (SO's hasInputBuffer: false).</summary>
        public List<ResourceCapacityDto> InputBuffer { get; set; }

        /// <summary>Optional. A null/omitted buffer means "no buffer".</summary>
        public List<ResourceCapacityDto> OutputBuffer { get; set; }

        /// <summary>
        /// Required (parent correction, 2026-09-28): BuildingDefinition uses
        /// DefinitionGuard.RequiredText (Definitions.cs:276), so an omitted key is
        /// rejected structurally with member.missing. Unlike ResourceDto.PresentationKey,
        /// this field is never optional.
        /// </summary>
        public string PresentationKey { get; set; }

        public bool IsDevelopmentOnly { get; set; }
    }

    public sealed class ResourceCapacityDto
    {
        public string ResourceId { get; set; }

        public long Capacity { get; set; }
    }

    public sealed class OfflinePolicyDto
    {
        public int Revision { get; set; }

        public long EfficiencyNumerator { get; set; }

        public long EfficiencyDenominator { get; set; }

        public long MaxCreditedTicks { get; set; }
    }
}
