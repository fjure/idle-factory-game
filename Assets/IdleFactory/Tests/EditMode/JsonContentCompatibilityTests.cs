using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Simulation;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T077 (US2/US3, WU09): proves that every pre-migration Save V1 fixture (T070) still
    /// loads unmodified through the unchanged <see cref="SaveV1Mapper"/> once the runtime
    /// catalog is built from the JSON content path (<see cref="GameplayContentLoader"/>)
    /// instead of the retired ScriptableObject path, that simulation and offline-return
    /// results are unchanged, that checkpoints written after the migration remain Save V1,
    /// and that a JSON catalog missing an ID a checkpoint references is rejected through the
    /// existing Save V1 incompatibility path.
    ///
    /// Neither <see cref="SaveV1Mapper"/> nor any file under
    /// <c>Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV1/</c> is modified by this file.
    /// </summary>
    public sealed class JsonContentCompatibilityTests
    {
        private const string PackagedContentRelativeToAssets = "IdleFactory/Content/Development/development-catalog.json";
        private const string FixtureDirectoryRelativeToAssets = "IdleFactory/Tests/EditMode/Fixtures/SaveV1";

        private static readonly ResourceId InputId = new ResourceId(AcceptedFoundationCatalogFixture.InputResourceId);
        private static readonly ResourceId OutputId = new ResourceId(AcceptedFoundationCatalogFixture.OutputResourceId);
        private static readonly BuildingDefinitionId D001Id = new BuildingDefinitionId(AcceptedFoundationCatalogFixture.BuildingId);
        private static readonly BuildingDefinitionId W001Id = new BuildingDefinitionId(FoundationFixtures.W001BuildingId);

        private static readonly DateTimeOffset DepartureUtc =
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset ReturnUtc =
            new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

        private static string AbsoluteFixtureDirectory =>
            Path.Combine(Application.dataPath, FixtureDirectoryRelativeToAssets);

        private DefinitionCatalog packagedCatalog;
        private DefinitionCatalog w001JsonCatalog;

        [SetUp]
        public void LoadJsonCatalogs()
        {
            packagedCatalog = LoadPackagedCatalog();
            w001JsonCatalog = LoadW001JsonCatalog();
        }

        // ---- 1a. The packaged JSON catalog matches the accepted pre-migration catalog -----------

        [Test]
        public void PackagedJsonCatalogMatchesAcceptedFoundationCatalogFieldByField()
        {
            AssertCatalogsEqual(AcceptedFoundationCatalogFixture.CreateCatalog(), packagedCatalog);
        }

        [Test]
        public void W001JsonDocumentBuildsCatalogEqualToFoundationFixturesCreateW001FieldByField()
        {
            var expected = FoundationFixtures.CreateW001().Catalog;
            AssertCatalogsEqual(expected, w001JsonCatalog);
        }

        // ---- 1b/2. Every Save V1 fixture loads unmodified against the JSON-built catalog --------

        [Test]
        public void Scenario01EmptyFactoryLoadsUnmodifiedAgainstJsonCatalog()
        {
            var loaded = Deserialize("01-empty-factory.json", packagedCatalog);
            var factory = loaded.Checkpoint.CreateFactoryCopy();

            Assert.That(factory.Buildings.Count, Is.EqualTo(0));
            var policy = packagedCatalog.Resolve(loaded.Checkpoint.OfflinePolicyRevision);
            Assert.That(policy.Revision.Value, Is.EqualTo(1));
            Assert.That(policy.EfficiencyNumerator, Is.EqualTo(4));
            Assert.That(policy.EfficiencyDenominator, Is.EqualTo(5));
            Assert.That(policy.MaxCreditedTicks, Is.EqualTo(288000000000L));
        }

        [Test]
        public void Scenario02IdlePlacedD001LoadsUnmodifiedAgainstJsonCatalog()
        {
            var loaded = Deserialize("02-idle-placed-d001.json", packagedCatalog);
            var building = loaded.Checkpoint.CreateFactoryCopy().Buildings.Values.Single();

            Assert.That(building.Anchor, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(building.OrientationQuarterTurns, Is.EqualTo(0));
            Assert.That(building.IsActive, Is.True);
            Assert.That(building.InputBuffer.Quantity(InputId), Is.EqualTo(4));
            Assert.That(building.OutputBuffer.Quantity(OutputId), Is.EqualTo(0));
            Assert.That(building.Production.ProgressTicks, Is.EqualTo(0));
            Assert.That(building.GetProductionBlockers().Count, Is.EqualTo(0));
        }

        [Test]
        public void Scenario03MidCycleLoadsUnmodifiedAgainstJsonCatalog()
        {
            var loaded = Deserialize("03-d001-mid-cycle-partial-progress.json", packagedCatalog);
            var building = loaded.Checkpoint.CreateFactoryCopy().Buildings.Values.Single();

            Assert.That(building.InputBuffer.Quantity(InputId), Is.EqualTo(2));
            Assert.That(building.OutputBuffer.Quantity(OutputId), Is.EqualTo(0));
            Assert.That(building.Production.ProgressTicks, Is.EqualTo(25000000L));
            Assert.That(building.Production.ProgressTicks, Is.LessThan(AcceptedFoundationCatalogFixture.RecipeDurationTicks));
            Assert.That(building.IsActive, Is.True);
            Assert.That(building.GetProductionBlockers().Count, Is.EqualTo(0));
        }

        [Test]
        public void Scenario04BlockedOutputFullLoadsUnmodifiedAgainstJsonCatalog()
        {
            var loaded = Deserialize("04-d001-blocked-output-full.json", packagedCatalog);
            var building = loaded.Checkpoint.CreateFactoryCopy().Buildings.Values.Single();

            Assert.That(building.InputBuffer.Quantity(InputId), Is.EqualTo(4));
            Assert.That(building.OutputBuffer.Quantity(OutputId), Is.EqualTo(1));
            Assert.That(building.Production.ProgressTicks, Is.EqualTo(0));
            Assert.That(building.IsActive, Is.True);
            var blocker = building.GetProductionBlockers().Single();
            Assert.That(blocker.Kind, Is.EqualTo(ProductionBlockerKind.InsufficientOutputCapacity));
            Assert.That(blocker.ResourceId, Is.EqualTo((ResourceId?)OutputId));
            Assert.That(blocker.Required, Is.EqualTo(1));
            Assert.That(blocker.Available, Is.EqualTo(0));
        }

        [Test]
        public void Scenario05DepartureLoadsUnmodifiedAndOfflineReturnMatchesReadme()
        {
            var loaded = Deserialize("05-checkpoint-departure-offline-return.json", packagedCatalog);
            var departureBuilding = loaded.Checkpoint.CreateFactoryCopy().Buildings.Values.Single();

            // Unmodified pre-offline state matches scenario 02's idle placement.
            Assert.That(departureBuilding.InputBuffer.Quantity(InputId), Is.EqualTo(4));
            Assert.That(departureBuilding.OutputBuffer.Quantity(OutputId), Is.EqualTo(0));
            Assert.That(departureBuilding.Production.ProgressTicks, Is.EqualTo(0));
            Assert.That(departureBuilding.IsActive, Is.True);
            Assert.That(loaded.Checkpoint.DepartureUtc, Is.EqualTo(DepartureUtc));

            var reconciler = new OfflineReconciler();
            var candidateId = new CheckpointId(Guid.NewGuid());
            var result = reconciler.Reconcile(loaded.Checkpoint, ReturnUtc, candidateId);

            Assert.That(result.Status, Is.EqualTo(OfflineReconciliationStatus.CandidateReady));
            Assert.That(result.HasCandidate, Is.True);
            Assert.That(result.Summary.MeasuredTicks, Is.EqualTo(288000000000L));
            Assert.That(result.Summary.CreditedTicks, Is.EqualTo(288000000000L));
            Assert.That(result.Summary.EffectiveTicks, Is.EqualTo(230400000000L));

            var candidateBuilding = result.CandidateCheckpoint.CreateFactoryCopy().Buildings.Values.Single();
            Assert.That(candidateBuilding.InputBuffer.Quantity(InputId), Is.EqualTo(2));
            Assert.That(candidateBuilding.OutputBuffer.Quantity(OutputId), Is.EqualTo(1));
            Assert.That(candidateBuilding.Production.ProgressTicks, Is.EqualTo(0));
            Assert.That(candidateBuilding.IsActive, Is.True);
            var blocker = candidateBuilding.GetProductionBlockers().Single();
            Assert.That(blocker.Kind, Is.EqualTo(ProductionBlockerKind.InsufficientOutputCapacity));
            Assert.That(blocker.ResourceId, Is.EqualTo((ResourceId?)OutputId));
            Assert.That(blocker.Required, Is.EqualTo(1));
            Assert.That(blocker.Available, Is.EqualTo(0));
        }

        [Test]
        public void Scenario06W001LoadsUnmodifiedWithExactDeterministicWorkloadShapeAndClassifications()
        {
            var loaded = Deserialize("06-w001-scale-checkpoint.json", w001JsonCatalog);
            var factory = loaded.Checkpoint.CreateFactoryCopy();
            var buildings = factory.Buildings.Values.ToArray();

            Assert.That(buildings, Has.Length.EqualTo(FoundationFixtures.W001MachineCount));
            Assert.That(factory.Occupancy.Count, Is.EqualTo(4000));
            Assert.That(factory.Bounds, Is.EqualTo(new FactoryBounds(new GridPosition(0, 0), 100, 40)));
            Assert.That(buildings.Select(building => building.Id).Distinct().Count(), Is.EqualTo(buildings.Length));

            Assert.That(
                buildings.Take(FoundationFixtures.W001ActiveUnblockedCount).Count(IsUnblockedActive),
                Is.EqualTo(FoundationFixtures.W001ActiveUnblockedCount));
            Assert.That(
                buildings.Skip(FoundationFixtures.W001ActiveUnblockedCount)
                    .Take(FoundationFixtures.W001ActiveBlockedCount)
                    .Count(IsOutputBlockedActive),
                Is.EqualTo(FoundationFixtures.W001ActiveBlockedCount));
            Assert.That(
                buildings.Skip(FoundationFixtures.W001ActiveUnblockedCount + FoundationFixtures.W001ActiveBlockedCount)
                    .Count(IsInactiveOnly),
                Is.EqualTo(FoundationFixtures.W001InactiveCount));
            Assert.That(buildings, Has.All.Matches<BuildingInstanceState>(building =>
                building.InputBuffer.Quantity(InputId) == FoundationFixtures.W001InputQuantity &&
                building.Production.ProgressTicks == 0));
            Assert.That(buildings[0].Anchor, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(buildings[49].Anchor, Is.EqualTo(new GridPosition(98, 0)));
            Assert.That(buildings[50].Anchor, Is.EqualTo(new GridPosition(0, 2)));
            Assert.That(buildings[999].Anchor, Is.EqualTo(new GridPosition(98, 38)));
        }

        // ---- 2. Simulation and offline outcomes are unchanged -----------------------------------

        [Test]
        public void D001TwoCycleFromScenario02MatchesUnchangedSimulation()
        {
            // Mirrors ProductionSimulationTests.D001SecondCycleAfterOutputCollectionCompletesIdenticalAtomicExchange,
            // starting from the JSON-catalog load of scenario 02 instead of FoundationFixtures.CreateD001()
            // (the same values: input 4/4, output 0/1, progress 0, active).
            const long fullCycle = 50000000L;
            var loaded = Deserialize("02-idle-placed-d001.json", packagedCatalog);
            var initial = loaded.Checkpoint.CreateFactoryCopy();

            var first = ProductionSimulator.Advance(initial, fullCycle);
            Assert.That(first.IsSuccess, Is.True);
            AssertD001State(first.Candidate, input: 2, output: 1, progress: 0);

            var blocked = ProductionSimulator.Advance(first.Candidate, fullCycle);
            Assert.That(blocked.IsSuccess, Is.True);
            AssertD001State(blocked.Candidate, input: 2, output: 1, progress: 0);
            Assert.That(blocked.Summary.Buildings.Single().Blockers.Single().Kind,
                Is.EqualTo(ProductionBlockerKind.InsufficientOutputCapacity));

            var blockedBuilding = blocked.Candidate.Buildings.Values.Single();
            var removed = TryRemoveOutput(blockedBuilding, OutputId, 1);
            Assert.That(removed, Is.EqualTo(BufferOperationResult.Success));

            var second = ProductionSimulator.Advance(blocked.Candidate, fullCycle);
            Assert.That(second.IsSuccess, Is.True);
            AssertD001State(second.Candidate, input: 0, output: 1, progress: 0);
            Assert.That(
                second.Summary.Buildings.Single().Blockers.Select(blocker => blocker.Kind),
                Is.EquivalentTo(new[]
                {
                    ProductionBlockerKind.MissingInput,
                    ProductionBlockerKind.InsufficientOutputCapacity,
                }));
        }

        [Test]
        public void W001DeterminismMatchesIdenticallyAdvancedJsonBuiltAndFoundationFixturesCatalogs()
        {
            const long fullCycle = 50000000L;
            var loaded = Deserialize("06-w001-scale-checkpoint.json", w001JsonCatalog);
            var jsonFactory = loaded.Checkpoint.CreateFactoryCopy();
            var fixtureFactory = FoundationFixtures.CreateW001();

            var jsonAdvanced = ProductionSimulator.Advance(jsonFactory, fullCycle);
            var fixtureAdvanced = ProductionSimulator.Advance(fixtureFactory, fullCycle);

            Assert.That(jsonAdvanced.IsSuccess, Is.True);
            Assert.That(fixtureAdvanced.IsSuccess, Is.True);
            AssertEquivalentFactoryState(fixtureAdvanced.Candidate, jsonAdvanced.Candidate);

            var jsonCheckpoint = WrapInCheckpoint(jsonAdvanced.Candidate, "11111111-1111-1111-1111-111111119001");
            var fixtureCheckpoint = WrapInCheckpoint(fixtureAdvanced.Candidate, "11111111-1111-1111-1111-111111119001");
            var jsonBytes = SaveV1Mapper.Serialize(jsonCheckpoint);
            var fixtureBytes = SaveV1Mapper.Serialize(fixtureCheckpoint);
            Assert.That(jsonBytes.Succeeded, Is.True, jsonBytes.Message);
            Assert.That(fixtureBytes.Succeeded, Is.True, fixtureBytes.Message);
            Assert.That(jsonBytes.Value, Is.EqualTo(fixtureBytes.Value),
                "Advancing the same W001 workload from a JSON-built and a FoundationFixtures-built catalog " +
                "(equal field-by-field) must serialize identically.");
        }

        // ---- 3. Checkpoints written after the migration remain Save V1 and round-trip ----------

        [TestCase("01-empty-factory.json")]
        [TestCase("02-idle-placed-d001.json")]
        [TestCase("03-d001-mid-cycle-partial-progress.json")]
        [TestCase("04-d001-blocked-output-full.json")]
        [TestCase("05-checkpoint-departure-offline-return.json")]
        public void MigratedFixtureRoundTripsAndReserializesByteIdenticallyFromJsonCatalogLoad(string fileName)
        {
            AssertRoundTripsAndReserializesByteIdentically(fileName, packagedCatalog);
        }

        [Test]
        public void W001MigratedFixtureRoundTripsAndReserializesByteIdenticallyFromJsonCatalogLoad()
        {
            AssertRoundTripsAndReserializesByteIdentically("06-w001-scale-checkpoint.json", w001JsonCatalog);
        }

        // ---- 4. A JSON document lacking an ID a checkpoint references is rejected --------------

        [Test]
        public void JsonCatalogMissingReferencedBuildingIdRejectsScenario02CheckpointThroughSaveV1IncompatibilityPath()
        {
            var renamedCatalogBytes = Encoding.UTF8.GetBytes(BuildPackagedJsonWithRenamedD001BuildingId());
            var renamedLoad = GameplayContentLoader.Load(renamedCatalogBytes);
            Assert.That(renamedLoad.IsSuccess, Is.True,
                "The modified document (a renamed building ID) must still be a structurally and " +
                "semantically valid catalog on its own; only the reference from the existing checkpoint breaks.");
            var renamedCatalog = renamedLoad.Catalog;
            Assert.That(renamedCatalog.Buildings.ContainsKey(D001Id), Is.False,
                "The renamed catalog must no longer contain 'dev.machine.basic'.");

            var bytes = ReadFixtureBytes("02-idle-placed-d001.json");
            var result = SaveV1Mapper.Deserialize(bytes, renamedCatalog);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
        }

        // ---- helpers ------------------------------------------------------------------------------

        private static DefinitionCatalog LoadPackagedCatalog()
        {
            var bytes = File.ReadAllBytes(Path.Combine(Application.dataPath, PackagedContentRelativeToAssets));
            var loaded = GameplayContentLoader.Load(bytes);
            Assert.That(loaded.IsSuccess, Is.True,
                loaded.IsSuccess ? string.Empty : string.Join("; ", loaded.Diagnostics.Select(d => d.Code)));
            return loaded.Catalog;
        }

        private static DefinitionCatalog LoadW001JsonCatalog()
        {
            var bytes = Encoding.UTF8.GetBytes(BuildW001Json());
            var loaded = GameplayContentLoader.Load(bytes);
            Assert.That(loaded.IsSuccess, Is.True,
                loaded.IsSuccess ? string.Empty : string.Join("; ", loaded.Diagnostics.Select(d => d.Code)));
            return loaded.Catalog;
        }

        private static byte[] ReadFixtureBytes(string fileName) =>
            File.ReadAllBytes(Path.Combine(AbsoluteFixtureDirectory, fileName));

        private static CheckpointReadResult Deserialize(string fileName, DefinitionCatalog catalog)
        {
            var bytes = ReadFixtureBytes(fileName);
            var result = SaveV1Mapper.Deserialize(bytes, catalog);
            Assert.That(result.Succeeded, Is.True, $"{fileName}: {result.Message}");
            return result;
        }

        private static void AssertRoundTripsAndReserializesByteIdentically(string fileName, DefinitionCatalog catalog)
        {
            var bytes = ReadFixtureBytes(fileName);
            var loaded = SaveV1Mapper.Deserialize(bytes, catalog);
            Assert.That(loaded.Succeeded, Is.True, $"{fileName}: {loaded.Message}");

            var reserialized = SaveV1Mapper.Serialize(loaded.Checkpoint);
            Assert.That(reserialized.Succeeded, Is.True, $"{fileName}: {reserialized.Message}");
            Assert.That(reserialized.Value, Is.EqualTo(bytes),
                $"{fileName} did not re-serialize byte-identically from the JSON-catalog load.");

            var roundTrip = SaveV1Mapper.Deserialize(reserialized.Value, catalog);
            Assert.That(roundTrip.Succeeded, Is.True, $"{fileName}: {roundTrip.Message}");
            Assert.That(roundTrip.Checkpoint.Id, Is.EqualTo(loaded.Checkpoint.Id));
            Assert.That(roundTrip.Checkpoint.Generation, Is.EqualTo(loaded.Checkpoint.Generation));
            Assert.That(roundTrip.Checkpoint.DepartureUtc, Is.EqualTo(loaded.Checkpoint.DepartureUtc));
            Assert.That(roundTrip.Checkpoint.OfflinePolicyRevision, Is.EqualTo(loaded.Checkpoint.OfflinePolicyRevision));
            AssertEquivalentFactoryState(loaded.Checkpoint.CreateFactoryCopy(), roundTrip.Checkpoint.CreateFactoryCopy());
        }

        private static void AssertD001State(FactoryState factory, long input, long output, long progress)
        {
            var building = factory.Buildings.Values.Single();
            Assert.That(building.InputBuffer.Quantity(InputId), Is.EqualTo(input));
            Assert.That(building.OutputBuffer.Quantity(OutputId), Is.EqualTo(output));
            Assert.That(building.Production.ProgressTicks, Is.EqualTo(progress));
        }

        private static bool IsUnblockedActive(BuildingInstanceState building) =>
            building.IsActive && building.GetProductionBlockers().Count == 0;

        private static bool IsOutputBlockedActive(BuildingInstanceState building) =>
            building.IsActive && building.GetProductionBlockers().Count == 1 &&
            building.GetProductionBlockers()[0].Kind == ProductionBlockerKind.InsufficientOutputCapacity;

        private static bool IsInactiveOnly(BuildingInstanceState building) => !building.IsActive;

        private static BufferOperationResult TryRemoveOutput(BuildingInstanceState building, ResourceId id, long amount)
        {
            var method = typeof(ResourceBufferState).GetMethod(
                "TryRemove", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (BufferOperationResult)method.Invoke(building.OutputBuffer, new object[] { id, amount });
        }

        private static Checkpoint WrapInCheckpoint(FactoryState factory, string checkpointId)
        {
            return new Checkpoint(
                new CheckpointId(checkpointId),
                0,
                DepartureUtc,
                new OfflinePolicyRevision(DefinitionCatalog.SupportedOfflinePolicyRevision),
                null,
                factory);
        }

        private static void AssertEquivalentFactoryState(FactoryState expected, FactoryState actual)
        {
            var expectedBuildings = expected.Buildings.Values.OrderBy(building => building.Id.Value, StringComparer.Ordinal).ToArray();
            var actualBuildings = actual.Buildings.Values.OrderBy(building => building.Id.Value, StringComparer.Ordinal).ToArray();
            Assert.That(actualBuildings.Select(building => building.Id),
                Is.EqualTo(expectedBuildings.Select(building => building.Id)));
            Assert.That(actualBuildings.Select(building => building.DefinitionId),
                Is.EqualTo(expectedBuildings.Select(building => building.DefinitionId)));

            for (var index = 0; index < expectedBuildings.Length; index++)
            {
                var e = expectedBuildings[index];
                var a = actualBuildings[index];
                Assert.That(a.Anchor, Is.EqualTo(e.Anchor));
                Assert.That(a.OrientationQuarterTurns, Is.EqualTo(e.OrientationQuarterTurns));
                Assert.That(a.IsActive, Is.EqualTo(e.IsActive));
                Assert.That(a.Production.ProgressTicks, Is.EqualTo(e.Production.ProgressTicks));
                AssertBufferQuantitiesEqual(e.InputBuffer, a.InputBuffer);
                AssertBufferQuantitiesEqual(e.OutputBuffer, a.OutputBuffer);
                Assert.That(a.GetProductionBlockers().Select(b => b.Kind),
                    Is.EqualTo(e.GetProductionBlockers().Select(b => b.Kind)));
            }
        }

        private static void AssertBufferQuantitiesEqual(ResourceBufferState expected, ResourceBufferState actual)
        {
            if (expected == null || actual == null)
            {
                Assert.That(actual, Is.EqualTo(expected));
                return;
            }

            foreach (var resourceId in expected.Definition.Capacities.Keys)
            {
                Assert.That(actual.Quantity(resourceId), Is.EqualTo(expected.Quantity(resourceId)));
            }
        }

        private static void AssertCatalogsEqual(DefinitionCatalog expected, DefinitionCatalog actual)
        {
            Assert.That(actual.Resources.Keys, Is.EquivalentTo(expected.Resources.Keys));
            foreach (var id in expected.Resources.Keys)
            {
                var e = expected.Resources[id];
                var a = actual.Resources[id];
                Assert.That(a.DevelopmentLabel, Is.EqualTo(e.DevelopmentLabel), id.Value);
                Assert.That(a.PresentationKey, Is.EqualTo(e.PresentationKey), id.Value);
                Assert.That(a.IsDevelopmentOnly, Is.EqualTo(e.IsDevelopmentOnly), id.Value);
            }

            Assert.That(actual.Recipes.Keys, Is.EquivalentTo(expected.Recipes.Keys));
            foreach (var id in expected.Recipes.Keys)
            {
                var e = expected.Recipes[id];
                var a = actual.Recipes[id];
                Assert.That(a.DurationTicks, Is.EqualTo(e.DurationTicks), id.Value);
                Assert.That(a.IsDevelopmentOnly, Is.EqualTo(e.IsDevelopmentOnly), id.Value);
                AssertAmountsEqual(e.Inputs, a.Inputs);
                AssertAmountsEqual(e.Outputs, a.Outputs);
            }

            Assert.That(actual.Buildings.Keys, Is.EquivalentTo(expected.Buildings.Keys));
            foreach (var id in expected.Buildings.Keys)
            {
                var e = expected.Buildings[id];
                var a = actual.Buildings[id];
                Assert.That(a.Footprint, Is.EqualTo(e.Footprint), id.Value);
                Assert.That(a.AllowedOrientations, Is.EqualTo(e.AllowedOrientations), id.Value);
                Assert.That(a.RecipeId, Is.EqualTo(e.RecipeId), id.Value);
                Assert.That(a.PresentationKey, Is.EqualTo(e.PresentationKey), id.Value);
                Assert.That(a.IsDevelopmentOnly, Is.EqualTo(e.IsDevelopmentOnly), id.Value);
                AssertBufferDefinitionEqual(e.InputBuffer, a.InputBuffer, id.Value);
                AssertBufferDefinitionEqual(e.OutputBuffer, a.OutputBuffer, id.Value);
            }

            Assert.That(actual.OfflinePolicies.Keys, Is.EquivalentTo(expected.OfflinePolicies.Keys));
            foreach (var revision in expected.OfflinePolicies.Keys)
            {
                var e = expected.OfflinePolicies[revision];
                var a = actual.OfflinePolicies[revision];
                Assert.That(a.EfficiencyNumerator, Is.EqualTo(e.EfficiencyNumerator));
                Assert.That(a.EfficiencyDenominator, Is.EqualTo(e.EfficiencyDenominator));
                Assert.That(a.MaxCreditedTicks, Is.EqualTo(e.MaxCreditedTicks));
            }
        }

        private static void AssertAmountsEqual(
            System.Collections.Generic.IReadOnlyList<ResourceAmountDefinition> expected,
            System.Collections.Generic.IReadOnlyList<ResourceAmountDefinition> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            var expectedOrdered = expected.OrderBy(a => a.ResourceId.Value, StringComparer.Ordinal).ToArray();
            var actualOrdered = actual.OrderBy(a => a.ResourceId.Value, StringComparer.Ordinal).ToArray();
            for (var index = 0; index < expectedOrdered.Length; index++)
            {
                Assert.That(actualOrdered[index].ResourceId, Is.EqualTo(expectedOrdered[index].ResourceId));
                Assert.That(actualOrdered[index].Amount, Is.EqualTo(expectedOrdered[index].Amount));
            }
        }

        private static void AssertBufferDefinitionEqual(BufferDefinition expected, BufferDefinition actual, string context)
        {
            if (expected == null || actual == null)
            {
                Assert.That(actual, Is.EqualTo(expected), context);
                return;
            }

            Assert.That(actual.Role, Is.EqualTo(expected.Role), context);
            Assert.That(actual.Capacities.Keys, Is.EquivalentTo(expected.Capacities.Keys), context);
            foreach (var resourceId in expected.Capacities.Keys)
            {
                Assert.That(actual.Capacities[resourceId], Is.EqualTo(expected.Capacities[resourceId]), context);
            }
        }

        private static string BuildPackagedJsonWithRenamedD001BuildingId()
        {
            var path = Path.Combine(Application.dataPath, PackagedContentRelativeToAssets);
            var json = File.ReadAllText(path, new UTF8Encoding(false));
            // Rename only the building's own "id" member. Its recipeId ("dev.recipe.basic") and every
            // resource id are untouched, so the document remains a valid, self-consistent catalog; it
            // simply no longer defines "dev.machine.basic", which the committed scenario 02 checkpoint
            // references.
            return json.Replace(
                "\"id\": \"dev.machine.basic\",",
                "\"id\": \"dev.machine.basic.renamed\",");
        }

        private static string BuildW001Json()
        {
            return "{\n" +
                "  \"contentSchemaVersion\": 1,\n" +
                "  \"resources\": [\n" +
                "    { \"id\": \"dev.input\", \"developmentLabel\": \"Input\", \"isDevelopmentOnly\": true },\n" +
                "    { \"id\": \"dev.output\", \"developmentLabel\": \"Output\", \"presentationKey\": \"dev.output.sprite\", \"isDevelopmentOnly\": true }\n" +
                "  ],\n" +
                "  \"recipes\": [\n" +
                "    {\n" +
                "      \"id\": \"dev.recipe.basic\",\n" +
                "      \"inputs\": [ { \"resourceId\": \"dev.input\", \"amount\": 2 } ],\n" +
                "      \"outputs\": [ { \"resourceId\": \"dev.output\", \"amount\": 1 } ],\n" +
                "      \"durationTicks\": 50000000,\n" +
                "      \"isDevelopmentOnly\": true\n" +
                "    }\n" +
                "  ],\n" +
                "  \"buildings\": [\n" +
                "    {\n" +
                "      \"id\": \"test.machine.w001\",\n" +
                "      \"footprintWidth\": 2,\n" +
                "      \"footprintHeight\": 2,\n" +
                "      \"allowedOrientations\": [0],\n" +
                "      \"recipeId\": \"dev.recipe.basic\",\n" +
                "      \"inputBuffer\": [ { \"resourceId\": \"dev.input\", \"capacity\": 9216 } ],\n" +
                "      \"outputBuffer\": [ { \"resourceId\": \"dev.output\", \"capacity\": 4608 } ],\n" +
                "      \"presentationKey\": \"test.fixture.w001\",\n" +
                "      \"isDevelopmentOnly\": true\n" +
                "    }\n" +
                "  ],\n" +
                "  \"offlinePolicies\": [\n" +
                "    { \"revision\": 1, \"efficiencyNumerator\": 4, \"efficiencyDenominator\": 5, \"maxCreditedTicks\": 288000000000 }\n" +
                "  ]\n" +
                "}\n";
        }
    }
}
