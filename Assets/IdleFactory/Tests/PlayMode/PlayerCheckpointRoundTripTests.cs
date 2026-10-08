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
    /// T119 retired the Feature 001 persistence commands; the Save V1 files these cases write are the legacy
    /// format the run imports read-only, so the store round trip and the device-clock rules stay covered here.
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
        public void DeviceUtcClockReturnsZeroOffsetUtcTime()
        {
            var before = DateTimeOffset.UtcNow;
            var value = new DeviceUtcClock().GetUtcNow();
            var after = DateTimeOffset.UtcNow;

            Assert.That(value.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(value, Is.GreaterThanOrEqualTo(DeviceUtcClock.FloorToMilliseconds(before)));
            Assert.That(value, Is.LessThanOrEqualTo(after));
        }

        [Test]
        public void DeviceClockCheckpointPromotedThroughTheRealStoreIsReadBackByAFreshStore()
        {
            var catalog = CreateCatalog();
            var checkpoint = new Checkpoint(
                new CheckpointId(Guid.NewGuid()), 1,
                new DeviceUtcClock().GetUtcNow(),
                new OfflinePolicyRevision(DefinitionCatalog.SupportedOfflinePolicyRevision),
                null, CreateFactory(catalog));

            var promoted = new JsonCheckpointStore(root, catalog).Promote(checkpoint);

            Assert.That(promoted.Outcome, Is.EqualTo(CommandOutcome.Success), promoted.Message + " " + DiagnosticText());
            Assert.That(diagnostics, Is.Empty, DiagnosticText());
            var directory = CheckpointDirectory();
            Assert.That(File.Exists(Path.Combine(directory, JsonCheckpointStore.DataAFileName)), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, JsonCheckpointStore.ManifestAFileName)), Is.True);

            // A fresh store models a relaunch: it must re-read and strictly validate the on-disk bytes.
            var relaunched = new JsonCheckpointStore(root, catalog).ReadActive();
            Assert.That(relaunched.Outcome, Is.EqualTo(CommandOutcome.Success), relaunched.Message);
            Assert.That(relaunched.Checkpoint.Id, Is.EqualTo(checkpoint.Id));
            Assert.That(relaunched.Checkpoint.Generation, Is.EqualTo(1L));
            Assert.That(relaunched.Checkpoint.DepartureUtc, Is.EqualTo(checkpoint.DepartureUtc));
            Assert.That(relaunched.Checkpoint.CreateFactoryCopy().Buildings.Count, Is.EqualTo(1));
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
