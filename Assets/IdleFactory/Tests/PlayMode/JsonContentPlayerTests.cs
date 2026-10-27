using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T076 (Issue #139) player-level regression for the JSON content authoring migration
    /// (002). Proves FR-013 (definitions are built only from the packaged content document,
    /// in the editor and in built players), SC-004 (invalid content never opens the
    /// checkpoint store and leaves its files byte-identical) and SC-008 (cold-load timing is
    /// measured and recorded) using the real production types, without the Editor-only
    /// composition seam <c>CoreGameFoundationBootstrap.OverrideRunCompositionForTests</c>
    /// (unavailable outside <c>UNITY_EDITOR</c>; see <see cref="PlayerCheckpointRoundTripTests"/>
    /// and the retired <c>PreMigrationCatalogBuildBaselineTests</c> for the same constraint).
    ///
    /// Two duplications are unavoidable and are called out explicitly rather than hidden:
    ///
    /// 1. <c>Assets/IdleFactory/Tests/EditMode/IdleFactory.EditModeTests.asmdef</c> declares
    ///    <c>"includePlatforms": ["Editor"]</c>, so that assembly (and the
    ///    <c>AcceptedFoundationCatalogFixture</c> it contains) is excluded from a player build.
    ///    <c>IdleFactory.PlayModeTests.asmdef</c> has no such restriction and does not reference
    ///    <c>IdleFactory.EditModeTests</c>, so this player-safe assembly cannot reference that
    ///    fixture. <see cref="Accepted"/> below duplicates its literal values by hand (verified
    ///    against both the fixture and the packaged
    ///    <c>Assets/IdleFactory/Content/Development/development-catalog.json</c> document).
    /// 2. Assets/ paths are not readable in a built player (no <c>UnityEditor.AssetDatabase</c>,
    ///    no <c>File.ReadAllBytes</c> against the source tree). <see cref="PackagedContentJson"/>
    ///    and <see cref="T070CheckpointJson"/> embed byte-for-byte copies of
    ///    <c>development-catalog.json</c> and
    ///    <c>Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV1/02-idle-placed-d001.json</c> as
    ///    string literals so the same bytes are available at player run time.
    /// </summary>
    public sealed class JsonContentPlayerTests
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";

        // Byte-for-byte copy of Assets/IdleFactory/Content/Development/development-catalog.json
        // (the TextAsset the authored scene packages as CoreGameFoundationBootstrap.gameplayContent).
        private const string PackagedContentJson =
@"{
  ""contentSchemaVersion"": 1,
  ""resources"": [
    {
      ""id"": ""dev.input"",
      ""developmentLabel"": ""Input"",
      ""presentationKey"": ""F001-RESOURCE-DEV-INPUT"",
      ""isDevelopmentOnly"": true
    },
    {
      ""id"": ""dev.output"",
      ""developmentLabel"": ""Output"",
      ""presentationKey"": ""F001-RESOURCE-DEV-OUTPUT"",
      ""isDevelopmentOnly"": true
    }
  ],
  ""recipes"": [
    {
      ""id"": ""dev.recipe.basic"",
      ""inputs"": [
        { ""resourceId"": ""dev.input"", ""amount"": 2 }
      ],
      ""outputs"": [
        { ""resourceId"": ""dev.output"", ""amount"": 1 }
      ],
      ""durationTicks"": 50000000,
      ""isDevelopmentOnly"": true
    }
  ],
  ""buildings"": [
    {
      ""id"": ""dev.machine.basic"",
      ""footprintWidth"": 2,
      ""footprintHeight"": 2,
      ""allowedOrientations"": [0],
      ""recipeId"": ""dev.recipe.basic"",
      ""inputBuffer"": [
        { ""resourceId"": ""dev.input"", ""capacity"": 4 }
      ],
      ""outputBuffer"": [
        { ""resourceId"": ""dev.output"", ""capacity"": 1 }
      ],
      ""presentationKey"": ""F001-MACHINE-D001-NORMAL"",
      ""isDevelopmentOnly"": true
    }
  ],
  ""offlinePolicies"": [
    {
      ""revision"": 1,
      ""efficiencyNumerator"": 4,
      ""efficiencyDenominator"": 5,
      ""maxCreditedTicks"": 288000000000
    }
  ]
}
";

        // Byte-for-byte copy of Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV1/02-idle-placed-d001.json (T070).
        private const string T070CheckpointJson =
"{\"schemaVersion\":1,\"checkpointId\":\"11111111-1111-1111-1111-111111110002\",\"generation\":0," +
"\"departureUtcUnixMilliseconds\":1767225600000,\"offlinePolicyRevision\":1,\"reconciledFromCheckpointId\":null," +
"\"factory\":{\"originX\":0,\"originY\":0,\"width\":16,\"height\":16,\"buildings\":[{\"instanceId\":" +
"\"22222222-2222-2222-2222-222222220001\",\"definitionId\":\"dev.machine.basic\",\"anchorX\":0,\"anchorY\":0," +
"\"orientationQuarterTurns\":0,\"isActive\":true,\"inputQuantities\":[{\"resourceId\":\"dev.input\",\"quantity\":4}]," +
"\"outputQuantities\":[{\"resourceId\":\"dev.output\",\"quantity\":0}],\"production\":{\"recipeId\":\"dev.recipe.basic\"," +
"\"progressTicks\":0}}]}}";

        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        // ---- (a) FR-013: packaged content, in a built player, builds the accepted catalog ----

        [Category("StandalonePlayer")]
        [Test]
        public void EmbeddedPackagedContentBuildsCatalogEqualToAcceptedFixture()
        {
            var result = GameplayContentLoader.Load(StrictUtf8.GetBytes(PackagedContentJson));

            Assert.That(result.IsSuccess, Is.True, Describe(result.Diagnostics));
            AssertCatalogEqual(result.Catalog, Accepted.CreateCatalog());
        }

        [Category("StandalonePlayer")]
        [UnityTest]
        public IEnumerator PackagedSceneContentInBuiltPlayerBuildsCatalogEqualToAcceptedFixture()
        {
            // Cold-start hygiene: a leftover checkpoint from a previous run of this same
            // reference-machine test player must not change whether Initialize() succeeds.
            var checkpointDirectory = Path.Combine(Application.persistentDataPath, "IdleFactory", "Checkpoints", "v1");
            var runCheckpointDirectory = Path.Combine(Application.persistentDataPath, "IdleFactory", "Checkpoints", "v2");
            DeleteDirectoryIfExists(checkpointDirectory);
            DeleteDirectoryIfExists(runCheckpointDirectory);

            var loadOperation = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Assert.That(loadOperation, Is.Not.Null, $"Could not start loading {ScenePath}.");
            while (!loadOperation.isDone) yield return null;
            yield return null;

            var scene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);

            CoreGameFoundationBootstrap bootstrap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                bootstrap = root.GetComponentInChildren<CoreGameFoundationBootstrap>(true);
                if (bootstrap != null) break;
            }

            Exception failure = null;
            try
            {
                Assert.That(bootstrap, Is.Not.Null, "CoreGameFoundationBootstrap was not found in the loaded scene.");
                Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
                // Feature 003 (T110): the scene packages the version 2 Iron document as its gameplay content and the
                // version 1 development catalog as the legacy import catalog. The start-up built a run from the former.
                Assert.That(bootstrap.RunCatalog, Is.Not.Null);
                Assert.That(bootstrap.RunSession, Is.Not.Null);

                // Independent proof that the *packaged* bytes (not just the resulting catalog)
                // are what the loaders actually built from. There is no public accessor for the private
                // serialized fields, so this reads them with reflection - a minimal, read-only path added by
                // this test file only; no production code changed.
                var packagedRun = ReadPackagedAsset(bootstrap, "gameplayContent");
                var runFromPackagedBytes = GameplayContentDispatcher.Load(packagedRun.bytes);
                Assert.That(runFromPackagedBytes.RunCatalog, Is.Not.Null, Describe(runFromPackagedBytes.Diagnostics));

                var packagedLegacy = ReadPackagedAsset(bootstrap, "legacyImportContent");
                var fromPackagedBytes = GameplayContentLoader.Load(packagedLegacy.bytes);
                Assert.That(fromPackagedBytes.IsSuccess, Is.True, Describe(fromPackagedBytes.Diagnostics));
                AssertCatalogEqual(fromPackagedBytes.Catalog, Accepted.CreateCatalog());
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            var unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone) yield return null;
            DeleteDirectoryIfExists(checkpointDirectory);
            DeleteDirectoryIfExists(runCheckpointDirectory);

            if (failure != null)
            {
                throw failure;
            }
        }

        private static TextAsset ReadPackagedAsset(CoreGameFoundationBootstrap bootstrap, string fieldName)
        {
            var field = typeof(CoreGameFoundationBootstrap).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"CoreGameFoundationBootstrap no longer declares a `{fieldName}` field.");
            var asset = (TextAsset)field.GetValue(bootstrap);
            Assert.That(asset, Is.Not.Null, $"The scene's packaged {fieldName} TextAsset was not resolved.");
            return asset;
        }

        // ---- (b) SC-004: corrupted content is rejected before the checkpoint store opens ----

        [Category("StandalonePlayer")]
        [Test]
        public void CorruptedContentIsRejectedAndRealCheckpointStoreStaysUntouched()
        {
            var catalog = Accepted.CreateCatalog();
            var root = Path.Combine(Application.temporaryCachePath, "t076-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                // Seed a real T070 checkpoint through the real JsonCheckpointStore (System.IO backend).
                var decoded = SaveV1Mapper.Deserialize(StrictUtf8.GetBytes(T070CheckpointJson), catalog);
                Assert.That(decoded.Succeeded, Is.True, decoded.Message);
                var store = new JsonCheckpointStore(root, catalog);
                var promoted = store.Promote(decoded.Checkpoint);
                Assert.That(promoted.Succeeded, Is.True, promoted.Message);

                var before = CaptureFiles(root);
                Assert.That(before, Is.Not.Empty, "The seeded checkpoint did not create any files.");

                var validJson = PackagedContentJson;
                var cases = new[]
                {
                    new CorruptedCase(
                        "truncated",
                        StrictUtf8.GetBytes(validJson.Substring(0, validJson.Length / 2)),
                        "json.malformed"),
                    new CorruptedCase(
                        "BOM-prefixed",
                        new byte[] { 0xEF, 0xBB, 0xBF }.Concat(StrictUtf8.GetBytes(validJson)).ToArray(),
                        "content.encoding"),
                    new CorruptedCase(
                        "unknown member",
                        StrictUtf8.GetBytes(validJson.Replace(
                            "\"contentSchemaVersion\": 1,",
                            "\"contentSchemaVersion\": 1, \"unknown\": true,")),
                        "member.unknown"),
                    new CorruptedCase(
                        "unresolved reference",
                        StrictUtf8.GetBytes(validJson.Replace(
                            "\"resourceId\": \"dev.input\", \"amount\": 2",
                            "\"resourceId\": \"missing.input\", \"amount\": 2")),
                        "ref.resource.unknown"),
                };

                foreach (var corrupted in cases)
                {
                    // GameplayContentLoader.Load has no dependency on ICheckpointStore at all: this
                    // call cannot reach `store` or the files under `root`. The before/after byte
                    // comparison below is the outside evidence that the rejection path never touched
                    // the real checkpoint files, matching SC-004's "byte-identical" requirement.
                    var result = GameplayContentLoader.Load(corrupted.Bytes);
                    Assert.That(result.IsSuccess, Is.False, corrupted.Name);
                    Assert.That(result.Diagnostics[0].Code, Is.EqualTo(corrupted.ExpectedCode), corrupted.Name);
                }

                var after = CaptureFiles(root);
                Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
                foreach (var pair in before)
                {
                    Assert.That(after[pair.Key], Is.EqualTo(pair.Value), pair.Key);
                }

                // The seeded checkpoint is still valid and readable through the same real store.
                var readBack = store.ReadActive();
                Assert.That(readBack.Succeeded, Is.True, readBack.Message);
                Assert.That(readBack.Checkpoint.Id, Is.EqualTo(decoded.Checkpoint.Id));
                Assert.That(readBack.Checkpoint.Generation, Is.EqualTo(decoded.Checkpoint.Generation));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        // ---- (c) SC-008: cold-load timing is measured and recorded (guard is measure-only) ----

        [Category("StandalonePlayer")]
        [Test]
        public void MedianGameplayContentLoaderLoadTimeIsMeasured()
        {
            const int SampleCount = 5;
            var bytes = StrictUtf8.GetBytes(PackagedContentJson);
            var stopwatch = new System.Diagnostics.Stopwatch();
            var sampleTicks = new long[SampleCount];
            for (var index = 0; index < SampleCount; index++)
            {
                stopwatch.Restart();
                var result = GameplayContentLoader.Load(bytes);
                stopwatch.Stop();
                Assert.That(result.IsSuccess, Is.True, Describe(result.Diagnostics));
                sampleTicks[index] = stopwatch.ElapsedTicks;
            }

            var sortedTicks = sampleTicks.OrderBy(sample => sample).ToArray();
            var medianTicks = sortedTicks[SampleCount / 2];
            var frequency = System.Diagnostics.Stopwatch.Frequency;
            var medianMilliseconds = medianTicks * 1000.0 / frequency;
            var sampleMilliseconds = sortedTicks
                .Select(sample => (sample * 1000.0 / frequency).ToString("F4"))
                .ToArray();

            Debug.Log(
                "SC-008 post-migration measurement: median GameplayContentLoader.Load over " +
                SampleCount + " samples = " + medianMilliseconds.ToString("F4") +
                " ms (sorted samples ms: " + string.Join(", ", sampleMilliseconds) +
                "); 100 ms guard is measure-only (R10, clarification 2026-09-28).");

            // The measurement itself is the deliverable (recorded in
            // specs/002-json-content-authoring/reviews/json-content-load-measurement.md); this
            // assertion only guards against a corrupted or negative reading.
            Assert.That(medianMilliseconds, Is.GreaterThanOrEqualTo(0.0));
        }

        // ---- shared helpers -----------------------------------------------------------------

        private static void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }

        private static Dictionary<string, byte[]> CaptureFiles(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path.Substring(root.Length), File.ReadAllBytes, StringComparer.Ordinal);

        private static string Describe(IEnumerable<ContentDiagnostic> diagnostics) =>
            string.Join("; ", diagnostics.Select(diagnostic => $"{diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}"));

        private static void AssertCatalogEqual(DefinitionCatalog actual, DefinitionCatalog expected)
        {
            Assert.That(actual.Resources.Count, Is.EqualTo(expected.Resources.Count));
            Assert.That(actual.Recipes.Count, Is.EqualTo(expected.Recipes.Count));
            Assert.That(actual.Buildings.Count, Is.EqualTo(expected.Buildings.Count));
            Assert.That(actual.OfflinePolicies.Count, Is.EqualTo(expected.OfflinePolicies.Count));

            AssertResourceEqual(actual.Resolve(Accepted.Input), expected.Resolve(Accepted.Input));
            AssertResourceEqual(actual.Resolve(Accepted.Output), expected.Resolve(Accepted.Output));
            AssertRecipeEqual(actual.Resolve(Accepted.Recipe), expected.Resolve(Accepted.Recipe));
            AssertBuildingEqual(actual.Resolve(Accepted.Building), expected.Resolve(Accepted.Building));
            AssertPolicyEqual(actual.Resolve(Accepted.OfflineRevision), expected.Resolve(Accepted.OfflineRevision));
        }

        private static void AssertResourceEqual(ResourceDefinition actual, ResourceDefinition expected)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.DevelopmentLabel, Is.EqualTo(expected.DevelopmentLabel));
            Assert.That(actual.PresentationKey, Is.EqualTo(expected.PresentationKey));
            Assert.That(actual.IsDevelopmentOnly, Is.EqualTo(expected.IsDevelopmentOnly));
        }

        private static void AssertRecipeEqual(RecipeDefinition actual, RecipeDefinition expected)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.DurationTicks, Is.EqualTo(expected.DurationTicks));
            Assert.That(actual.IsDevelopmentOnly, Is.EqualTo(expected.IsDevelopmentOnly));
            AssertAmountsEqual(actual.Inputs, expected.Inputs);
            AssertAmountsEqual(actual.Outputs, expected.Outputs);
        }

        private static void AssertAmountsEqual(
            IReadOnlyList<ResourceAmountDefinition> actual,
            IReadOnlyList<ResourceAmountDefinition> expected)
        {
            Assert.That(actual, Has.Count.EqualTo(expected.Count));
            for (var index = 0; index < expected.Count; index++)
            {
                Assert.That(actual[index].ResourceId, Is.EqualTo(expected[index].ResourceId));
                Assert.That(actual[index].Amount, Is.EqualTo(expected[index].Amount));
            }
        }

        private static void AssertBuildingEqual(BuildingDefinition actual, BuildingDefinition expected)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.Footprint, Is.EqualTo(expected.Footprint));
            Assert.That(actual.AllowedOrientations, Is.EqualTo(expected.AllowedOrientations));
            Assert.That(actual.RecipeId, Is.EqualTo(expected.RecipeId));
            Assert.That(actual.PresentationKey, Is.EqualTo(expected.PresentationKey));
            Assert.That(actual.IsDevelopmentOnly, Is.EqualTo(expected.IsDevelopmentOnly));
            AssertBufferEqual(actual.InputBuffer, expected.InputBuffer);
            AssertBufferEqual(actual.OutputBuffer, expected.OutputBuffer);
        }

        private static void AssertBufferEqual(BufferDefinition actual, BufferDefinition expected)
        {
            if (expected == null)
            {
                Assert.That(actual, Is.Null);
                return;
            }

            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.Role, Is.EqualTo(expected.Role));
            Assert.That(actual.Capacities.Count, Is.EqualTo(expected.Capacities.Count));
            foreach (var pair in expected.Capacities)
            {
                Assert.That(actual.Capacities[pair.Key], Is.EqualTo(pair.Value));
            }
        }

        private static void AssertPolicyEqual(OfflinePolicyDefinition actual, OfflinePolicyDefinition expected)
        {
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(actual.EfficiencyNumerator, Is.EqualTo(expected.EfficiencyNumerator));
            Assert.That(actual.EfficiencyDenominator, Is.EqualTo(expected.EfficiencyDenominator));
            Assert.That(actual.MaxCreditedTicks, Is.EqualTo(expected.MaxCreditedTicks));
        }

        private sealed class CorruptedCase
        {
            public CorruptedCase(string name, byte[] bytes, string expectedCode)
            {
                Name = name;
                Bytes = bytes;
                ExpectedCode = expectedCode;
            }

            public string Name { get; }
            public byte[] Bytes { get; }
            public string ExpectedCode { get; }
        }

        /// <summary>
        /// Duplicate, by hand, of
        /// Assets/IdleFactory/Tests/EditMode/Fixtures/AcceptedFoundationCatalogFixture.cs
        /// (see the type-level remarks above for why the original cannot be referenced from a
        /// player-safe assembly). Every value below was cross-checked against that fixture and
        /// against Assets/IdleFactory/Content/Development/development-catalog.json.
        /// </summary>
        private static class Accepted
        {
            public const string InputResourceId = "dev.input";
            public const string InputDevelopmentLabel = "Input";
            public const string InputPresentationKey = "F001-RESOURCE-DEV-INPUT";
            public const bool InputIsDevelopmentOnly = true;

            public const string OutputResourceId = "dev.output";
            public const string OutputDevelopmentLabel = "Output";
            public const string OutputPresentationKey = "F001-RESOURCE-DEV-OUTPUT";
            public const bool OutputIsDevelopmentOnly = true;

            public const string RecipeId = "dev.recipe.basic";
            public const long RecipeInputAmount = 2L;
            public const long RecipeOutputAmount = 1L;
            public const long RecipeDurationTicks = 50000000L;
            public const bool RecipeIsDevelopmentOnly = true;

            public const string BuildingId = "dev.machine.basic";
            public const int FootprintWidth = 2;
            public const int FootprintHeight = 2;
            public static readonly int[] AllowedOrientations = { 0 };
            public const long InputBufferCapacity = 4L;
            public const long OutputBufferCapacity = 1L;
            public const string BuildingPresentationKey = "F001-MACHINE-D001-NORMAL";
            public const bool BuildingIsDevelopmentOnly = true;

            public const int OfflinePolicyRevisionValue = 1;
            public const long OfflineEfficiencyNumerator = 4L;
            public const long OfflineEfficiencyDenominator = 5L;
            public const long OfflineMaxCreditedTicks = 288000000000L;

            public static readonly ResourceId Input = new ResourceId(InputResourceId);
            public static readonly ResourceId Output = new ResourceId(OutputResourceId);
            public static readonly RecipeId Recipe = new RecipeId(RecipeId);
            public static readonly BuildingDefinitionId Building = new BuildingDefinitionId(BuildingId);
            public static readonly OfflinePolicyRevision OfflineRevision = new OfflinePolicyRevision(OfflinePolicyRevisionValue);

            public static DefinitionCatalog CreateCatalog()
            {
                var inputResource = new ResourceDefinition(Input, InputDevelopmentLabel, InputPresentationKey, InputIsDevelopmentOnly);
                var outputResource = new ResourceDefinition(Output, OutputDevelopmentLabel, OutputPresentationKey, OutputIsDevelopmentOnly);

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
                    new BufferDefinition(BufferRole.Input, new[] { new KeyValuePair<ResourceId, long>(Input, InputBufferCapacity) }),
                    new BufferDefinition(BufferRole.Output, new[] { new KeyValuePair<ResourceId, long>(Output, OutputBufferCapacity) }),
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
}
