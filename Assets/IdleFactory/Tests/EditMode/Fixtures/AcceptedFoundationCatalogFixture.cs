using System.Collections.Generic;
using IdleFactory.Domain;

namespace IdleFactory.Tests.EditMode.Fixtures
{
    /// <summary>
    /// Hand-written record of every value accepted into the ScriptableObject-authored
    /// development content (<c>Assets/IdleFactory/Content/Development/**</c>) while that
    /// path was still live, ahead of the JSON content authoring migration (002).
    ///
    /// Every value below was verified by reading the retired <c>.asset</c> YAML files
    /// (resources dev.input / dev.output, recipe D001, building D001, offline policy revision 1)
    /// and by a one-time field-by-field comparison against the catalog the retired builder
    /// produced from them (R09, T070). The ScriptableObject path was retired by T079; the
    /// packaged <c>development-catalog.json</c> is now the authored source, and
    /// <c>JsonDevelopmentContentTests</c> keeps it equal to this fixture.
    ///
    /// This fixture references no ScriptableObject type: it is built entirely from plain
    /// values and <c>IdleFactory.Domain</c> (Core) types.
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
