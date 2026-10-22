using System;
using System.IO;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Core.Numerics;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T313 (issue #375): the V1 import still works on the schema-version-3 catalog. <see cref="V1ImportTests"/> imports over the
    /// test-local run catalog; these tests import the committed accepted V1 fixtures over the SHIPPED catalog, built the way the player does
    /// (<see cref="GameplayContentDispatcher"/> over <c>iron-factory-content.json</c>, with the transitional reward adapter), through the
    /// real <see cref="RunPersistence"/> cold start. V1 is only read through the write-denying fake, and its directory hash stays equal.
    /// </summary>
    public sealed class V1ImportOnV3CatalogTests
    {
        private const string ContentPath = "Assets/IdleFactory/Content/Iron/iron-factory-content.json";
        private const string FixtureDirectoryRelativeToAssets = "IdleFactory/Tests/EditMode/Fixtures/SaveV1";

        private static readonly long DepartureMs = V1ImportFixtures.DepartureUtc.ToUnixTimeMilliseconds();

        private bool _logToUnity;

        [SetUp]
        public void SetUp()
        {
            _logToUnity = CheckpointDiagnostics.LogToUnity;
            CheckpointDiagnostics.LogToUnity = false;
        }

        [TearDown]
        public void TearDown()
        {
            CheckpointDiagnostics.LogToUnity = _logToUnity;
        }

        private sealed class Env
        {
            public ScriptedBackend V2;
            public WriteDenyingV1Directory V1Dir;
            public string V1Hash;
            public DefinitionCatalog LegacyCatalog;
            public FakeLegacySource Legacy;
            public RunCatalog Catalog;
            public RunSession Session;
            public JsonRunCheckpointStore Store;
            public RunPersistence Persistence;
        }

        private static RunCatalog ShippedCatalog()
        {
            var loaded = GameplayContentDispatcher.Load(File.ReadAllBytes(ContentPath));
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Diagnostics.Select(d => d.Code + " " + d.Path)));
            Assert.That(loaded.SchemaVersion, Is.EqualTo(3), "the import target is the schema-version-3 catalog");
            return loaded.RunCatalog;
        }

        private static Env NewEnv(string fixture, ScriptedBackend v2 = null, WriteDenyingV1Directory v1 = null, DefinitionCatalog legacyCatalog = null, int firstId = 1)
        {
            RunCatalog catalog = ShippedCatalog();
            var env = new Env { Catalog = catalog, V2 = v2 ?? new ScriptedBackend("v2") };
            if (fixture != null)
            {
                env.LegacyCatalog = AcceptedFoundationCatalogFixture.CreateCatalog();
                byte[] bytes = File.ReadAllBytes(Path.Combine(Application.dataPath, FixtureDirectoryRelativeToAssets, fixture));
                var seed = new ScriptedBackend("v1-seed");
                var decoded = SaveV1Mapper.Deserialize(bytes, env.LegacyCatalog);
                Assert.That(decoded.Succeeded, Is.True, fixture);
                Assert.That(new JsonCheckpointStore("memory-root", env.LegacyCatalog, seed).Promote(decoded.Checkpoint).Succeeded, Is.True);
                env.V1Dir = new WriteDenyingV1Directory(seed.Files);
            }
            else
            {
                env.LegacyCatalog = legacyCatalog;
                env.V1Dir = v1;
            }

            env.V1Hash = env.V1Dir.DirectoryHash();
            env.Legacy = new FakeLegacySource(new SaveV1CheckpointSource(env.V1Dir, env.LegacyCatalog));

            var permanent = new PermanentState(catalog);
            NextRunStart start = NextRunStart.Compute(catalog, permanent);
            var state = new RunState(catalog, Guid.NewGuid(), 1, null, start.StartingCash, 0, start.StorageLevel, 1, catalog.RunStart.InitialUnlocks);
            Assert.That(new RunSimulator(catalog).Advance(state, permanent, 0, DepartureMs, null), Is.EqualTo(RunAdvanceResult.Ok));
            env.Session = new RunSession(catalog, state, permanent, DepartureMs);
            env.Store = new JsonRunCheckpointStore("memory-root", catalog, env.V2);
            env.Persistence = new RunPersistence(
                env.Session, env.Store, new FixedUtcClock(DepartureMs), new SequenceCheckpointIds(firstId), new OfflineRunReturnReconciler(catalog));
            return env;
        }

        [TestCase("01-empty-factory.json", 0, 0L)]
        [TestCase("02-idle-placed-d001.json", 1, 4L)]
        [TestCase("03-d001-mid-cycle-partial-progress.json", 1, 2L)]
        [TestCase("05-checkpoint-departure-offline-return.json", 1, 4L)]
        public void AcceptedV1Fixtures_ImportExactlyOnceOverTheShippedV3Catalog(string fixture, int machines, long ore)
        {
            Env env = NewEnv(fixture);

            var result = env.Persistence.ColdStart(env.Legacy);

            Assert.That(result.IsSuccess, Is.True, result.ReasonCode + " " + env.Persistence.BlockedImportCode);
            Assert.That(result.Value.Imported, Is.True);
            Assert.That(result.Value.StateReplaced, Is.True);
            Assert.That(env.Persistence.BlockedImportCode, Is.Null);
            Assert.That(env.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(env.Legacy.Reads, Is.EqualTo(1), "V1 is read once.");

            RunState run = env.Session.State;
            Assert.That(run.Machines.Count, Is.EqualTo(machines));
            Assert.That(run.Machines.All(m => m.MachineId == V1Import.SmelterMachineId && m.Level == 1 && m.IsActive), Is.True);
            Assert.That(run.Inventory.Units("iron.ore"), Is.EqualTo(ore), "The development Ore became Iron Ore.");
            Assert.That(run.Cash, Is.EqualTo(env.Catalog.RunStart.StartingCash), "fresh-run defaults from the v3 run start");
            Assert.That(run.RunNumber, Is.EqualTo(1));
            Assert.That(run.Unlocked, Is.SupersetOf(new[] { "iron.ore", "machine.miner", "iron.bar", V1Import.SmelterMachineId }));
            Assert.That(run.IsUnlocked("iron.plate"), Is.False, "Only the mapped IDs are opened by the import.");

            RunCheckpoint durable = env.Store.ReadActive().Checkpoint;
            Assert.That(durable.Import, Is.Not.Null);
            Assert.That(durable.Import.SourceSchemaVersion, Is.EqualTo(1));
            Assert.That(env.V1Dir.WriteAttempts, Is.Zero, "No V1 file may be opened for writing.");
            Assert.That(env.V1Dir.DirectoryHash(), Is.EqualTo(env.V1Hash), "The whole V1 directory stays byte-identical.");
        }

        [Test]
        public void ARestartOverTheShippedV3Catalog_SelectsTheImportedV2AndNeverReadsV1Again()
        {
            Env first = NewEnv("02-idle-placed-d001.json");
            Assert.That(first.Persistence.ColdStart(first.Legacy).IsSuccess, Is.True);

            Env restarted = NewEnv(null, first.V2, first.V1Dir, first.LegacyCatalog, firstId: 100);
            var again = restarted.Persistence.ColdStart(restarted.Legacy);

            Assert.That(again.IsSuccess, Is.True, again.ReasonCode);
            Assert.That(again.Value.Imported, Is.False, "V1 is not imported a second time.");
            Assert.That(restarted.Session.State.Machines.Count, Is.EqualTo(1));
            Assert.That(restarted.Session.State.RunId, Is.EqualTo(first.Session.State.RunId));
            Assert.That(first.V1Dir.WriteAttempts, Is.Zero);
            Assert.That(first.V1Dir.DirectoryHash(), Is.EqualTo(first.V1Hash));
        }

        [Test]
        public void TheImportedRunSavesAndLoadsWithTheV3CatalogContentRevision()
        {
            Env env = NewEnv("02-idle-placed-d001.json");
            Assert.That(env.Persistence.ColdStart(env.Legacy).IsSuccess, Is.True);

            RunCheckpoint durable = env.Store.ReadActive().Checkpoint;

            Assert.That(env.Catalog.ContentRevision, Is.EqualTo(3), "the shipped catalog is the tuned revision 3 (T345); a V1 import carries no content revision of its own");
            Assert.That(durable.Run.RunNumber, Is.EqualTo(1));
            Assert.That(durable.Run.Machines.Count, Is.EqualTo(1));
        }
    }
}
