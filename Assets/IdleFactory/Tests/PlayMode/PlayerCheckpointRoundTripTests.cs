using System;
using System.Collections.Generic;
using System.IO;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T052 player-level regression: exercises the production DeviceUtcClock, the real
    /// JsonCheckpointStore (System.IO backend) and SaveV1Mapper without CompositionTestOverrides or
    /// the Editor scene harness, so the same fixture also runs inside a Windows/Android test player.
    /// </summary>
    public sealed class PlayerCheckpointRoundTripTests
    {
        private static readonly ResourceId InputId = new ResourceId("t052.input");
        private static readonly ResourceId OutputId = new ResourceId("t052.output");
        private static readonly RecipeId RecipeId = new RecipeId("t052.recipe");
        private static readonly BuildingDefinitionId MachineId = new BuildingDefinitionId("t052.machine");

        private string root;
        private List<CheckpointDiagnostic> diagnostics;

        [SetUp]
        public void CreateIsolatedRoot()
        {
            root = Path.Combine(Application.temporaryCachePath, "t052-" + Guid.NewGuid().ToString("N"));
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
        public void DeviceUtcClockReadingsAreRepresentableInSaveV1()
        {
            for (var sample = 0; sample < 64; sample++)
            {
                var value = new DeviceUtcClock().GetUtcNow();
                Assert.That(value.Offset, Is.EqualTo(TimeSpan.Zero));
                Assert.That(value.UtcTicks % TimeSpan.TicksPerMillisecond, Is.Zero,
                    "Save V1 stores whole Unix milliseconds; the device clock must not produce sub-millisecond instants.");
            }
        }

        [Test]
        public void ManualSaveWithDeviceClockWritesAndRestoresRealCheckpoint()
        {
            var catalog = CreateCatalog();
            var session = CreateRunningSession(catalog, new DeviceUtcClock());
            var commands = new PersistenceCommands(session);

            var saved = commands.SaveNow();

            Assert.That(saved.Outcome, Is.EqualTo(CommandOutcome.Success), saved.Message + " " + DiagnosticText());
            Assert.That(diagnostics, Is.Empty, DiagnosticText());
            var directory = CheckpointDirectory();
            Assert.That(File.Exists(Path.Combine(directory, JsonCheckpointStore.DataAFileName)), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, JsonCheckpointStore.ManifestAFileName)), Is.True);

            // A fresh store models a relaunch: it must re-read and strictly validate the on-disk bytes.
            var relaunched = new JsonCheckpointStore(root, catalog).ReadActive();
            Assert.That(relaunched.Outcome, Is.EqualTo(CommandOutcome.Success), relaunched.Message);
            Assert.That(relaunched.Checkpoint.Id, Is.EqualTo(saved.Value.CheckpointId.Value));
            Assert.That(relaunched.Checkpoint.Generation, Is.EqualTo(saved.Value.Generation.Value));
            Assert.That(relaunched.Checkpoint.CreateFactoryCopy().Buildings.Count, Is.EqualTo(1));
        }

        [Test]
        public void SuspensionWritesDepartureCheckpointThatAFreshSessionRestores()
        {
            var catalog = CreateCatalog();
            var session = CreateRunningSession(catalog, new DeviceUtcClock());
            var commands = new PersistenceCommands(session);

            var departed = commands.RecordSuspension();

            Assert.That(departed.Outcome, Is.EqualTo(CommandOutcome.Success), departed.Message + " " + DiagnosticText());
            Assert.That(departed.Value.CheckpointId.HasValue, Is.True);

            var relaunchedStore = new JsonCheckpointStore(root, catalog);
            var active = relaunchedStore.ReadActive();
            Assert.That(active.Outcome, Is.EqualTo(CommandOutcome.Success), active.Message);
            Assert.That(active.Checkpoint.Id, Is.EqualTo(departed.Value.CheckpointId.Value));

            var relaunchedSession = new FactorySession(EmptyFactory(catalog), catalog, new DeviceUtcClock(), relaunchedStore);
            var relaunchedCommands = new PersistenceCommands(relaunchedSession);
            // Cold start mirrors ApplicationLifecycleAdapter: the first resume reconciles the active generation.
            var restored = relaunchedCommands.ResumeAndReconcile();
            Assert.That(restored.Outcome, Is.EqualTo(CommandOutcome.Success), restored.Message + " " + DiagnosticText());
            Assert.That(restored.Value.UsedFallback, Is.False);
            Assert.That(relaunchedSession.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(relaunchedSession.GetFactorySnapshot().Buildings.Count, Is.EqualTo(1));
            Assert.That(diagnostics, Is.Empty, DiagnosticText());
        }

        [Test]
        public void PreviousGenerationRecoveryUsesTheRealStore()
        {
            var catalog = CreateCatalog();
            var session = CreateRunningSession(catalog, new DeviceUtcClock());
            var commands = new PersistenceCommands(session);

            var first = commands.SaveNow();
            Assert.That(first.Outcome, Is.EqualTo(CommandOutcome.Success), first.Message + " " + DiagnosticText());
            var second = commands.SaveNow();
            Assert.That(second.Outcome, Is.EqualTo(CommandOutcome.Success), second.Message + " " + DiagnosticText());

            var recovered = commands.RecoverPreviousCheckpoint();

            Assert.That(recovered.Outcome, Is.EqualTo(CommandOutcome.Success), recovered.Message + " " + DiagnosticText());
            var active = new JsonCheckpointStore(root, catalog).ReadActive();
            Assert.That(active.Outcome, Is.EqualTo(CommandOutcome.Success), active.Message);
            Assert.That(active.Checkpoint.Generation, Is.GreaterThan(second.Value.Generation.Value));
            Assert.That(active.Checkpoint.ReconciledFromCheckpointId, Is.EqualTo(first.Value.CheckpointId));
            Assert.That(diagnostics, Is.Empty, DiagnosticText());
        }

        [Test]
        public void SubMillisecondDepartureIsDiagnosedWithoutPlayerData()
        {
            var catalog = CreateCatalog();
            var subMillisecond = new Checkpoint(
                new CheckpointId(Guid.NewGuid()), 1,
                new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero).AddTicks(1),
                new OfflinePolicyRevision(DefinitionCatalog.SupportedOfflinePolicyRevision),
                null, CreateFactory(catalog));
            var logToUnity = CheckpointDiagnostics.LogToUnity;
            CheckpointDiagnostics.LogToUnity = false;
            CommandResult<CheckpointId> result;
            try
            {
                result = new JsonCheckpointStore(root, catalog).Promote(subMillisecond);
            }
            finally
            {
                CheckpointDiagnostics.LogToUnity = logToUnity;
            }

            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(diagnostics.Count, Is.EqualTo(2), DiagnosticText());
            Assert.That(diagnostics[0].Operation, Is.EqualTo("Serialize"));
            Assert.That(diagnostics[0].ExceptionType, Is.EqualTo("SaveSemanticException"));
            Assert.That(diagnostics[0].Origin, Does.StartWith("GetUnixMillisecondsLossless:"));
            Assert.That(diagnostics[1].Operation, Is.EqualTo("Publish"));
            Assert.That(diagnostics[1].Stage, Is.EqualTo("Serialize"));
            foreach (var diagnostic in diagnostics)
            {
                var text = diagnostic.ToString();
                Assert.That(text, Does.Not.Contain(root));
                Assert.That(text, Does.Not.Contain(subMillisecond.Id.Value));
                Assert.That(text, Does.Not.Contain("t052."));
            }
        }

        private string CheckpointDirectory() => Path.Combine(root, "IdleFactory", "Checkpoints", "v1");

        private string DiagnosticText() => string.Join("; ", diagnostics.ConvertAll(item => item.ToString()));

        private FactorySession CreateRunningSession(DefinitionCatalog catalog, IUtcClock clock)
        {
            var created = DevelopmentSessionFactory.CreateRunning(
                CreateFactory(catalog), catalog, clock, new JsonCheckpointStore(root, catalog));
            Assert.That(created.Succeeded, Is.True, created.Message);
            return created.Value;
        }

        private static FactoryState EmptyFactory(DefinitionCatalog catalog) =>
            new FactoryState(new FactoryBounds(new GridPosition(0, 0), 16, 16), catalog, Array.Empty<BuildingInstanceState>());

        private static FactoryState CreateFactory(DefinitionCatalog catalog)
        {
            var definition = catalog.Resolve(MachineId);
            var building = new BuildingInstanceState(
                new BuildingInstanceId(Guid.NewGuid()),
                definition,
                new GridPosition(2, 3),
                0,
                true,
                new ResourceBufferState(definition.InputBuffer, Quantities(InputId, 4)),
                new ResourceBufferState(definition.OutputBuffer, Quantities(OutputId, 0)),
                new ProductionState(catalog.Resolve(RecipeId), 0));
            return new FactoryState(new FactoryBounds(new GridPosition(0, 0), 16, 16), catalog, new[] { building });
        }

        private static DefinitionCatalog CreateCatalog()
        {
            return new DefinitionCatalog(
                new[]
                {
                    new ResourceDefinition(InputId, "Input", null, true),
                    new ResourceDefinition(OutputId, "Output", null, true),
                },
                new[]
                {
                    new RecipeDefinition(
                        RecipeId,
                        new[] { new ResourceAmountDefinition(InputId, 2) },
                        new[] { new ResourceAmountDefinition(OutputId, 1) },
                        50000000L,
                        true),
                },
                new[]
                {
                    new BuildingDefinition(
                        MachineId,
                        new Footprint(2, 2),
                        new[] { 0 },
                        RecipeId,
                        new BufferDefinition(BufferRole.Input, Quantities(InputId, 4)),
                        new BufferDefinition(BufferRole.Output, Quantities(OutputId, 1)),
                        "t052.machine.sprite",
                        true),
                },
                new[]
                {
                    new OfflinePolicyDefinition(
                        new OfflinePolicyRevision(DefinitionCatalog.SupportedOfflinePolicyRevision),
                        DefinitionCatalog.SupportedEfficiencyNumerator,
                        DefinitionCatalog.SupportedEfficiencyDenominator,
                        DefinitionCatalog.SupportedMaxCreditedTicks),
                });
        }

        private static KeyValuePair<ResourceId, long>[] Quantities(ResourceId id, long value) =>
            new[] { new KeyValuePair<ResourceId, long>(id, value) };
    }
}
