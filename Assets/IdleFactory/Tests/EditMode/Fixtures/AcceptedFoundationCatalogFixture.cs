using System.Collections.Generic;
using IdleFactory.Domain;

namespace IdleFactory.Tests.EditMode.Fixtures
{
    /// <summary>
    /// Hand-written record of every value accepted into the ScriptableObject-authored
    /// development content (<c>Assets/IdleFactory/Content/Development/**</c>) while that
    /// path was still live, ahead of the JSON content authoring migration (002).
    ///
    /// Every value below was verified by reading the corresponding <c>.asset</c> YAML file:
    ///   - Resources/dev.input.asset, Resources/dev.output.asset
    ///   - Recipes/D001.asset
    ///   - Buildings/D001.asset
    ///   - OfflinePolicies/revision-1.asset
    ///
    /// This fixture references no ScriptableObject type: it is built entirely from plain
    /// values and <c>IdleFactory.Domain</c> (Core) types, independent of
    /// <c>DefinitionCatalogBuilder</c>. <see cref="EditMode.PreMigrationCaptureTests"/>
    /// asserts that <see cref="CreateCatalog"/> equals, field by field, the catalog the
    /// builder produces from the live ScriptableObject asset. That test is the one-time
    /// reviewable verification (R09) and is deleted by T079 once the JSON path replaces
    /// the ScriptableObject path.
    /// </summary>
    public static class AcceptedFoundationCatalogFixture
    {
        // Resources/dev.input.asset
        public const string InputResourceId = "dev.input";
        public const string InputDevelopmentLabel = "Input";
        public const string InputPresentationKey = "F001-RESOURCE-DEV-INPUT";
        public const bool InputIsDevelopmentOnly = true;

        // Resources/dev.output.asset
        public const string OutputResourceId = "dev.output";
        public const string OutputDevelopmentLabel = "Output";
        public const string OutputPresentationKey = "F001-RESOURCE-DEV-OUTPUT";
        public const bool OutputIsDevelopmentOnly = true;

        // Recipes/D001.asset (id: dev.recipe.basic)
        public const string RecipeId = "dev.recipe.basic";
        public const long RecipeInputAmount = 2L;
        public const long RecipeOutputAmount = 1L;
        public const long RecipeDurationTicks = 50000000L;
        public const bool RecipeIsDevelopmentOnly = true;

        // Buildings/D001.asset (id: dev.machine.basic)
        public const string BuildingId = "dev.machine.basic";
        public const int FootprintWidth = 2;
        public const int FootprintHeight = 2;
        public static readonly int[] AllowedOrientations = { 0 };
        public const long InputBufferCapacity = 4L;
        public const long OutputBufferCapacity = 1L;
        public const string BuildingPresentationKey = "F001-MACHINE-D001-NORMAL";
        public const bool BuildingIsDevelopmentOnly = true;

        // OfflinePolicies/revision-1.asset
        public const int OfflinePolicyRevisionValue = 1;
        public const long OfflineEfficiencyNumerator = 4L;
        public const long OfflineEfficiencyDenominator = 5L;
        public const long OfflineMaxCreditedTicks = 288000000000L;

        public static readonly ResourceId Input = new ResourceId(InputResourceId);
        public static readonly ResourceId Output = new ResourceId(OutputResourceId);
        public static readonly RecipeId Recipe = new RecipeId(RecipeId);
        public static readonly BuildingDefinitionId Building = new BuildingDefinitionId(BuildingId);
        public static readonly OfflinePolicyRevision OfflineRevision = new OfflinePolicyRevision(OfflinePolicyRevisionValue);

        /// <summary>
        /// Builds the accepted catalog directly from the constants above, using only
        /// plain <c>IdleFactory.Domain</c> types. Independent of any asset-loading or
        /// builder code path.
        /// </summary>
        public static DefinitionCatalog CreateCatalog()
        {
            var inputResource = new ResourceDefinition(
                Input,
                InputDevelopmentLabel,
                InputPresentationKey,
                InputIsDevelopmentOnly);
            var outputResource = new ResourceDefinition(
                Output,
                OutputDevelopmentLabel,
                OutputPresentationKey,
                OutputIsDevelopmentOnly);

            var recipe = new RecipeDefinition(
                Recipe,
                new[] { new ResourceAmountDefinition(Input, RecipeInputAmount) },
                new[] { new ResourceAmountDefinition(Output, RecipeOutputAmount) },
                RecipeDurationTicks,
                RecipeIsDevelopmentOnly);

            var building = new BuildingDefinition(
                Building,
                new Footprint(FootprintWidth, FootprintHeight),
                AllowedOrientations,
                Recipe,
                new BufferDefinition(
                    BufferRole.Input,
                    new[] { new KeyValuePair<ResourceId, long>(Input, InputBufferCapacity) }),
                new BufferDefinition(
                    BufferRole.Output,
                    new[] { new KeyValuePair<ResourceId, long>(Output, OutputBufferCapacity) }),
                BuildingPresentationKey,
                BuildingIsDevelopmentOnly);

            var offlinePolicy = new OfflinePolicyDefinition(
                OfflineRevision,
                OfflineEfficiencyNumerator,
                OfflineEfficiencyDenominator,
                OfflineMaxCreditedTicks);

            return new DefinitionCatalog(
                new[] { inputResource, outputResource },
                new[] { recipe },
                new[] { building },
                new[] { offlinePolicy });
        }
    }
}
