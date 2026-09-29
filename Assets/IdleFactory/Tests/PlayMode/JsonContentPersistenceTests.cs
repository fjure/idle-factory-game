using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T077 (US2/US3, WU09): exercises Save V1 compatibility and the offline-return
    /// incompatibility path through the real application boundary (
    /// <see cref="PersistenceCommands"/> and the real <see cref="JsonCheckpointStore"/>
    /// System.IO backend on a temporary root), using a catalog built from the JSON content
    /// path instead of the retired ScriptableObject path. Companion to the EditMode file
    /// <c>JsonContentCompatibilityTests.cs</c>, which covers the Save V1 fixtures and
    /// simulation directly. Neither <see cref="SaveV1Mapper"/> nor any Save V1 fixture file
    /// is modified by this file.
    /// </summary>
    public sealed class JsonContentPersistenceTests
    {
        private const string PackagedContentRelativeToAssets = "IdleFactory/Content/Development/development-catalog.json";

        private static readonly ResourceId InputId = new ResourceId("dev.input");
        private static readonly ResourceId OutputId = new ResourceId("dev.output");
        private static readonly RecipeId RecipeId = new RecipeId("dev.recipe.basic");
        private static readonly BuildingDefinitionId D001Id = new BuildingDefinitionId("dev.machine.basic");

        private static readonly DateTimeOffset DepartureUtc =
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly TimeSpan OfflineInterval = TimeSpan.FromHours(8);

        private string root;
        private List<CheckpointDiagnostic> diagnostics;

        [SetUp]
        public void CreateIsolatedRoot()
        {
            root = Path.Combine(Application.temporaryCachePath, "t077-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            diagnostics = new List<CheckpointDiagnostic>();
            CheckpointDiagnostics.Reported += diagnostics.Add;
        }

        [TearDown]
        public void DeleteIsolatedRoot()
        {
            CheckpointDiagnostics.Reported -= diagnostics.Add;
            if (root != null && Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void JsonCatalogCheckpointSavedThroughRealStoreIsReadBackByAFreshStoreUnmodified()
        {
            var catalog = LoadPackagedCatalog();
            var session = CreateRunningSession(catalog, new TestUtcClock(DepartureUtc), CreateScenario02Factory(catalog));
            var commands = new PersistenceCommands(session);

            var saved = commands.SaveNow();
            Assert.That(saved.Outcome, Is.EqualTo(CommandOutcome.Success), saved.Message + " " + DiagnosticText());
            Assert.That(diagnostics, Is.Empty, DiagnosticText());

            var relaunched = new JsonCheckpointStore(root, catalog).ReadActive();
            Assert.That(relaunched.Outcome, Is.EqualTo(CommandOutcome.Success), relaunched.Message);
            var building = relaunched.Checkpoint.CreateFactoryCopy().Buildings.Values.Single();
            Assert.That(building.InputBuffer.Quantity(InputId), Is.EqualTo(4));
            Assert.That(building.OutputBuffer.Quantity(OutputId), Is.EqualTo(0));
            Assert.That(building.Production.ProgressTicks, Is.EqualTo(0));
            Assert.That(building.IsActive, Is.True);
        }

        [Test]
        public void OfflineContinuationThroughRealApplicationPathMatchesScenario05Expectation()
        {
            var catalog = LoadPackagedCatalog();
            var clock = new TestUtcClock(DepartureUtc);
            var factory = CreateScenario02Factory(catalog);
            var instanceId = factory.Buildings.Values.Single().Id;
            var store = new JsonCheckpointStore(root, catalog);
            var session = CreateRunningSession(catalog, clock, factory, store);
            var commands = new PersistenceCommands(session);

            var departed = commands.RecordSuspension();
            Assert.That(departed.Outcome, Is.EqualTo(CommandOutcome.Success), departed.Message + " " + DiagnosticText());

            // A fresh session models a relaunch after the offline interval elapses.
            clock.Now = DepartureUtc + OfflineInterval;
            var relaunchedStore = new JsonCheckpointStore(root, catalog);
            var emptyFactory = new FactoryState(new FactoryBounds(new GridPosition(0, 0), 16, 16), catalog, Array.Empty<BuildingInstanceState>());
            var relaunchedSession = new FactorySession(emptyFactory, catalog, clock, relaunchedStore);
            var relaunchedCommands = new PersistenceCommands(relaunchedSession);

            var restored = relaunchedCommands.ResumeAndReconcile();
            Assert.That(restored.Outcome, Is.EqualTo(CommandOutcome.Success), restored.Message + " " + DiagnosticText());
            Assert.That(restored.Value.UsedFallback, Is.False);
            Assert.That(relaunchedSession.LifecycleState, Is.EqualTo(LifecycleState.Running));

            var summary = restored.Value.OfflineSummary;
            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.MeasuredTicks, Is.EqualTo(288000000000L));
            Assert.That(summary.CreditedTicks, Is.EqualTo(288000000000L));
            Assert.That(summary.EffectiveTicks, Is.EqualTo(230400000000L));

            var inspection = relaunchedSession.GetMachineInspection(instanceId);
            Assert.That(inspection.Succeeded, Is.True, inspection.Message);
            Assert.That(inspection.Value.Inputs.Single(q => q.ResourceId == InputId).Quantity, Is.EqualTo(2));
            Assert.That(inspection.Value.Outputs.Single(q => q.ResourceId == OutputId).Quantity, Is.EqualTo(1));
            Assert.That(inspection.Value.ProgressTicks, Is.EqualTo(0));
            Assert.That(inspection.Value.IsActive, Is.True);
            Assert.That(inspection.Value.Blockers.Single().Kind, Is.EqualTo(ProductionBlockerKind.InsufficientOutputCapacity));
            Assert.That(diagnostics, Is.Empty, DiagnosticText());
        }

        [Test]
        public void JsonCatalogMissingReferencedBuildingIdRejectsExistingCheckpointWithoutFileOrLiveStateChange()
        {
            var catalog = LoadPackagedCatalog();
            var session = CreateRunningSession(catalog, new TestUtcClock(DepartureUtc), CreateScenario02Factory(catalog));
            var commands = new PersistenceCommands(session);

            var saved = commands.SaveNow();
            Assert.That(saved.Outcome, Is.EqualTo(CommandOutcome.Success), saved.Message + " " + DiagnosticText());
            var before = CaptureCheckpointFiles();
            Assert.That(before, Is.Not.Empty);

            var brokenCatalog = LoadPackagedCatalogWithRenamedD001BuildingId();
            Assert.That(brokenCatalog.Buildings.ContainsKey(D001Id), Is.False,
                "The modified catalog must no longer define 'dev.machine.basic'.");

            // Reading directly through a fresh store constructed against the incompatible catalog
            // exercises the same Save V1 incompatibility path SaveV1Mapper.Deserialize uses.
            var brokenStore = new JsonCheckpointStore(root, brokenCatalog);
            var readResult = brokenStore.ReadActive();
            Assert.That(readResult.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint), readResult.Message);
            AssertCheckpointFilesUnchanged(before);

            // The same rejection surfaces through PersistenceCommands without mutating live state.
            // LoadDevelopmentCheckpoint is only admitted from the Running lifecycle state, so the
            // session is brought up through DevelopmentSessionFactory.CreateRunning exactly like the
            // real bootstrap does, using an empty starting factory (independent of the rejected read).
            var brokenFactory = new FactoryState(new FactoryBounds(new GridPosition(0, 0), 16, 16), brokenCatalog, Array.Empty<BuildingInstanceState>());
            var brokenSession = CreateRunningSession(brokenCatalog, new TestUtcClock(DepartureUtc), brokenFactory, brokenStore);
            var brokenCommands = new PersistenceCommands(brokenSession);
            var brokenLoad = brokenCommands.LoadDevelopmentCheckpoint();

            Assert.That(brokenLoad.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint), brokenLoad.Message);
            Assert.That(brokenSession.GetFactorySnapshot().Buildings.Count, Is.EqualTo(0),
                "A rejected load must not mutate live session state.");
            AssertCheckpointFilesUnchanged(before);
        }

        private string DiagnosticText() => string.Join("; ", diagnostics.ConvertAll(item => item.ToString()));

        private static FactorySession CreateRunningSession(
            DefinitionCatalog catalog, IUtcClock clock, FactoryState factory, ICheckpointStore store = null)
        {
            var created = DevelopmentSessionFactory.CreateRunning(factory, catalog, clock, store);
            Assert.That(created.Succeeded, Is.True, created.Message);
            return created.Value;
        }

        private FactorySession CreateRunningSession(DefinitionCatalog catalog, IUtcClock clock, FactoryState factory)
        {
            return CreateRunningSession(catalog, clock, factory, new JsonCheckpointStore(root, catalog));
        }

        private static FactoryState CreateScenario02Factory(DefinitionCatalog catalog)
        {
            var definition = catalog.Resolve(D001Id);
            var recipe = catalog.Resolve(RecipeId);
            var building = new BuildingInstanceState(
                new BuildingInstanceId(new Guid("22222222-2222-2222-2222-222222220001")),
                definition,
                new GridPosition(0, 0),
                0,
                true,
                new ResourceBufferState(definition.InputBuffer, new[] { new KeyValuePair<ResourceId, long>(InputId, 4L) }),
                new ResourceBufferState(definition.OutputBuffer, new[] { new KeyValuePair<ResourceId, long>(OutputId, 0L) }),
                new ProductionState(recipe, 0));
            return new FactoryState(new FactoryBounds(new GridPosition(0, 0), 16, 16), catalog, new[] { building });
        }

        private static DefinitionCatalog LoadPackagedCatalog()
        {
            var bytes = ReadPackagedContentBytes();
            var loaded = GameplayContentLoader.Load(bytes);
            Assert.That(loaded.IsSuccess, Is.True,
                loaded.IsSuccess ? string.Empty : string.Join("; ", loaded.Diagnostics.Select(d => d.Code)));
            return loaded.Catalog;
        }

        private static DefinitionCatalog LoadPackagedCatalogWithRenamedD001BuildingId()
        {
            var text = Encoding.UTF8.GetString(ReadPackagedContentBytes());
            var renamed = text.Replace(
                "\"id\": \"dev.machine.basic\",",
                "\"id\": \"dev.machine.basic.renamed\",");
            var loaded = GameplayContentLoader.Load(Encoding.UTF8.GetBytes(renamed));
            Assert.That(loaded.IsSuccess, Is.True,
                loaded.IsSuccess ? string.Empty : string.Join("; ", loaded.Diagnostics.Select(d => d.Code)));
            return loaded.Catalog;
        }

        // Application.dataPath is the project's Assets folder itself in the Editor (both EditMode
        // and Editor-hosted PlayMode), so this resolves to the real packaged content file without
        // any UnityEditor dependency (the PlayMode assembly also compiles for player builds).
        private static byte[] ReadPackagedContentBytes() =>
            File.ReadAllBytes(Path.Combine(Application.dataPath, PackagedContentRelativeToAssets));

        private Dictionary<string, byte[]> CaptureCheckpointFiles()
        {
            var directory = Path.Combine(root, "IdleFactory", "Checkpoints", "v1");
            return Directory.Exists(directory)
                ? Directory.GetFiles(directory).ToDictionary(Path.GetFileName, File.ReadAllBytes, StringComparer.Ordinal)
                : new Dictionary<string, byte[]>();
        }

        private void AssertCheckpointFilesUnchanged(Dictionary<string, byte[]> before)
        {
            var after = CaptureCheckpointFiles();
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
            foreach (var pair in before)
            {
                Assert.That(after[pair.Key], Is.EqualTo(pair.Value), pair.Key);
            }
        }

        private sealed class TestUtcClock : IUtcClock
        {
            public TestUtcClock(DateTimeOffset now)
            {
                Now = now;
            }

            public DateTimeOffset Now { get; set; }

            public DateTimeOffset GetUtcNow() => Now;
        }
    }
}
