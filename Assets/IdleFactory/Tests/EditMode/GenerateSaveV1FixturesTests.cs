using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEditor;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// One-off, explicit generator for the Save V1 fixture files committed under
    /// <c>Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV1/</c> (T070c). Not part of the
    /// regular suite: it is marked <see cref="ExplicitAttribute"/> so it never runs in CI,
    /// and exists only so the committed files can be reproduced byte-for-byte from the
    /// unchanged <see cref="SaveV1Mapper"/> and Core domain types.
    ///
    /// The committed <c>.json</c> files (and their <c>README.md</c>) are the actual
    /// fixtures used by future tests; this file only documents and reproduces how they
    /// were produced, while the ScriptableObject content path was still live.
    /// </summary>
    [Explicit("One-off generator for the committed Save V1 fixtures; run manually to regenerate them.")]
    public sealed class GenerateSaveV1FixturesTests
    {
        private const string OutputDirectory = "Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV1";

        // origin/main HEAD this worktree branched from (PR #137 merge, see AGENTS.md gitStatus).
        private const string CaptureCommit = "51603f2";

        private static readonly DateTimeOffset DepartureUtc =
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private static readonly TimeSpan OfflineReturnInterval = TimeSpan.FromHours(8);

        [Test]
        public void GenerateFixtures()
        {
            Directory.CreateDirectory(OutputDirectory);

            var readme = new StringBuilder();
            AppendHeader(readme);

            WriteScenario(
                readme,
                "01-empty-factory.json",
                "Empty factory",
                "An empty factory with no buildings, at generation 0, departure UTC " +
                    FormatUtc(DepartureUtc) + ".",
                "Loading yields a FactoryState with zero buildings (FactoryState.Buildings.Count == 0) " +
                    "and Checkpoint.OfflinePolicyRevision resolves to the accepted revision 1 " +
                    "(4/5 efficiency, maxCreditedTicks 288000000000). No offline reconciliation is exercised.",
                BuildCheckpoint("11111111-1111-1111-1111-111111110001", 0, DepartureUtc, null, EmptyFactory()));

            WriteScenario(
                readme,
                "02-idle-placed-d001.json",
                "Idle placed D001",
                "A single freshly placed D001 (dev.machine.basic) at anchor (0,0), orientation 0: " +
                    "input buffer full (4/4 dev.input), output buffer empty (0/1 dev.output), " +
                    "production progress 0, active.",
                "Loading yields one building at Anchor (0,0), OrientationQuarterTurns 0, IsActive true, " +
                    "InputBuffer.Quantity(dev.input) == 4, OutputBuffer.Quantity(dev.output) == 0, " +
                    "Production.ProgressTicks == 0, GetProductionBlockers().Count == 0.",
                BuildCheckpoint(
                    "11111111-1111-1111-1111-111111110002",
                    0,
                    DepartureUtc,
                    null,
                    SingleD001(inputQuantity: 4, outputQuantity: 0, progressTicks: 0, isActive: true)));

            WriteScenario(
                readme,
                "03-d001-mid-cycle-partial-progress.json",
                "D001 mid-cycle with partial progress",
                "The same placed D001, mid-cycle: input partially consumed (2/4 dev.input), output still " +
                    "empty (0/1 dev.output), production progress at half the recipe duration " +
                    "(25000000/50000000 ticks), active.",
                "Loading yields InputBuffer.Quantity(dev.input) == 2, OutputBuffer.Quantity(dev.output) == 0, " +
                    "Production.ProgressTicks == 25000000 (< RecipeDefinition.DurationTicks 50000000), " +
                    "IsActive true, GetProductionBlockers().Count == 0 (2 >= recipe input amount 2, and the " +
                    "output buffer has free capacity for the recipe's output amount 1).",
                BuildCheckpoint(
                    "11111111-1111-1111-1111-111111110003",
                    0,
                    DepartureUtc,
                    null,
                    SingleD001(inputQuantity: 2, outputQuantity: 0, progressTicks: 25000000, isActive: true)));

            WriteScenario(
                readme,
                "04-d001-blocked-output-full.json",
                "D001 blocked with output full",
                "A placed D001 with input ready (4/4 dev.input) but its single-slot output buffer already " +
                    "full (1/1 dev.output), production progress 0, active.",
                "Loading yields InputBuffer.Quantity(dev.input) == 4, OutputBuffer.Quantity(dev.output) == 1 " +
                    "(at capacity), Production.ProgressTicks == 0, IsActive true; GetProductionBlockers() " +
                    "returns exactly one blocker: Kind InsufficientOutputCapacity, ResourceId dev.output, " +
                    "Required 1, Available 0.",
                BuildCheckpoint(
                    "11111111-1111-1111-1111-111111110004",
                    0,
                    DepartureUtc,
                    null,
                    SingleD001(inputQuantity: 4, outputQuantity: 1, progressTicks: 0, isActive: true)));

            var departureFactory = SingleD001(inputQuantity: 4, outputQuantity: 0, progressTicks: 0, isActive: true);
            var departureCheckpoint = BuildCheckpoint(
                "11111111-1111-1111-1111-111111110005",
                0,
                DepartureUtc,
                null,
                departureFactory);
            var offlineOutcome = ComputeOfflineOutcome(departureCheckpoint, OfflineReturnInterval);
            WriteScenario(
                readme,
                "05-checkpoint-departure-offline-return.json",
                "Checkpoint with a departure time for offline return",
                "The same idle placed D001 (input 4/4, output 0/1, progress 0, active), checkpointed at " +
                    "departure UTC " + FormatUtc(DepartureUtc) + ", for exercising an offline return.",
                offlineOutcome,
                departureCheckpoint);

            WriteScenario(
                readme,
                "06-w001-scale-checkpoint.json",
                "W001-scale checkpoint",
                "The FoundationFixtures W001 workload: " + FoundationFixtures.W001MachineCount +
                    " test.machine.w001 buildings on the accepted dev.* recipe/resources (" +
                    FoundationFixtures.W001ActiveUnblockedCount + " active unblocked, " +
                    FoundationFixtures.W001ActiveBlockedCount + " active output-blocked, " +
                    FoundationFixtures.W001InactiveCount + " inactive), each with input quantity " +
                    FoundationFixtures.W001InputQuantity + " and output capacity " +
                    FoundationFixtures.W001OutputCapacity + ", at generation 0, departure UTC " +
                    FormatUtc(DepartureUtc) + ".",
                "Loading yields " + FoundationFixtures.W001MachineCount + " buildings with exactly the " +
                    "classification FoundationFixtures.CreateW001 produces; see " +
                    "FoundationFixtures.W001HasExactDeterministicWorkloadShapeAndClassifications for the " +
                    "complete field-by-field assertion (occupancy count 4000, bounds (0,0) 100x40, per-index " +
                    "anchor formula, and the unblocked/output-blocked/inactive counts and blocker kinds above).",
                BuildCheckpoint(
                    "11111111-1111-1111-1111-111111110006",
                    0,
                    DepartureUtc,
                    null,
                    FoundationFixtures.CreateW001()));

            File.WriteAllText(
                Path.Combine(OutputDirectory, "README.md"),
                readme.ToString(),
                new UTF8Encoding(false));

            // The scenario files above are written with raw System.IO, bypassing the asset
            // import pipeline; refresh so Unity generates their .meta files before the commit.
            AssetDatabase.Refresh();
        }

        private static void AppendHeader(StringBuilder readme)
        {
            readme.AppendLine("# Save V1 pre-migration fixtures");
            readme.AppendLine();
            readme.AppendLine(
                "Captured at commit `" + CaptureCommit + "` (origin/main, PR #137 merge) by " +
                "`GenerateSaveV1FixturesTests.GenerateFixtures` (T070c), while the ScriptableObject " +
                "development content path was still live. Re-running that test reproduces these files " +
                "byte-for-byte from the unchanged `SaveV1Mapper` and Core domain types, and re-asserts the " +
                "offline-return expectation below via the unchanged `OfflineReconciler`.");
            readme.AppendLine();
            readme.AppendLine(
                "Every scenario below (other than 06) uses the accepted `dev.*` catalog captured in " +
                "`AcceptedFoundationCatalogFixture`: resources `dev.input`/`dev.output`, recipe " +
                "`dev.recipe.basic` (2 dev.input -> 1 dev.output, durationTicks 50000000), building " +
                "`dev.machine.basic` (D001, footprint 2x2, orientations [0], input capacity 4, output " +
                "capacity 1), and offline policy revision 1 (4/5 efficiency, maxCreditedTicks 288000000000). " +
                "Scenario 06 uses `FoundationFixtures.CreateW001`, which builds its own self-consistent " +
                "catalog with the same `dev.*` resource/recipe IDs and values plus a distinct " +
                "`test.machine.w001` building.");
            readme.AppendLine();
            readme.AppendLine(
                "All factories use bounds origin (0,0), width 16, height 16, except scenario 06 (100x40, " +
                "from FoundationFixtures.CreateW001).");
            readme.AppendLine();
        }

        private static void WriteScenario(
            StringBuilder readme,
            string fileName,
            string title,
            string scenario,
            string expectedOutcome,
            Checkpoint checkpoint)
        {
            var serialized = SaveV1Mapper.Serialize(checkpoint);
            Assert.That(serialized.Succeeded, Is.True, $"Failed to serialize scenario '{title}': {serialized.Message}");

            var path = Path.Combine(OutputDirectory, fileName);
            File.WriteAllBytes(path, serialized.Value);

            // Round-trip through Deserialize to prove the fixture is valid Save V1 content against its
            // own catalog before it is committed.
            var roundTrip = SaveV1Mapper.Deserialize(serialized.Value, checkpoint.CreateFactoryCopy().Catalog);
            Assert.That(roundTrip.Succeeded, Is.True, $"Round-trip failed for scenario '{title}': {roundTrip.Message}");

            readme.AppendLine("## " + fileName);
            readme.AppendLine();
            readme.AppendLine("**" + title + "**");
            readme.AppendLine();
            readme.AppendLine("Scenario: " + scenario);
            readme.AppendLine();
            readme.AppendLine("Expected outcome when loaded: " + expectedOutcome);
            readme.AppendLine();
        }

        private static string ComputeOfflineOutcome(Checkpoint departureCheckpoint, TimeSpan interval)
        {
            var returnUtc = departureCheckpoint.DepartureUtc + interval;
            var reconciler = new OfflineReconciler();
            var candidateId = new CheckpointId("11111111-1111-1111-1111-111111110105");
            var result = reconciler.Reconcile(departureCheckpoint, returnUtc, candidateId);

            Assert.That(result.Status, Is.EqualTo(OfflineReconciliationStatus.CandidateReady));
            Assert.That(result.HasCandidate, Is.True);

            var summary = result.Summary;
            var candidateBuilding = result.CandidateCheckpoint.CreateFactoryCopy().Buildings.Values.Single();
            var blockers = candidateBuilding.GetProductionBlockers();
            var blockerText = blockers.Count == 0
                ? "no production blockers"
                : string.Join(
                    "; ",
                    blockers.Select(blocker =>
                        blocker.ResourceId.HasValue
                            ? $"{blocker.Kind} on {blocker.ResourceId.Value.Value} (required {blocker.Required}, available {blocker.Available})"
                            : $"{blocker.Kind}"));

            return "Applying OfflineReconciler.Reconcile with returnUtc " + FormatUtc(returnUtc) +
                " (an offline interval of " + interval + ") against this checkpoint yields Status " +
                "CandidateReady with MeasuredTicks " + summary.MeasuredTicks + ", CreditedTicks " +
                summary.CreditedTicks + " (capped at the offline policy's maxCreditedTicks " +
                "288000000000, exactly reached by this 8-hour interval), EffectiveTicks " +
                summary.EffectiveTicks + " (CreditedTicks * 4 / 5). The resulting candidate checkpoint's " +
                "single building has InputBuffer.Quantity(dev.input) == " +
                candidateBuilding.InputBuffer.Quantity(AcceptedFoundationCatalogFixture.Input) +
                ", OutputBuffer.Quantity(dev.output) == " +
                candidateBuilding.OutputBuffer.Quantity(AcceptedFoundationCatalogFixture.Output) +
                ", Production.ProgressTicks == " + candidateBuilding.Production.ProgressTicks +
                ", IsActive == " + candidateBuilding.IsActive + ", and " + blockerText + ".";
        }

        private static Checkpoint BuildCheckpoint(
            string checkpointId,
            long generation,
            DateTimeOffset departureUtc,
            string reconciledFromCheckpointId,
            FactoryState factory)
        {
            return new Checkpoint(
                new CheckpointId(checkpointId),
                generation,
                departureUtc,
                new OfflinePolicyRevision(DefinitionCatalog.SupportedOfflinePolicyRevision),
                reconciledFromCheckpointId == null ? (CheckpointId?)null : new CheckpointId(reconciledFromCheckpointId),
                factory);
        }

        private static FactoryState EmptyFactory()
        {
            return new FactoryState(
                new FactoryBounds(new GridPosition(0, 0), 16, 16),
                AcceptedFoundationCatalogFixture.CreateCatalog(),
                Array.Empty<BuildingInstanceState>());
        }

        private static FactoryState SingleD001(
            long inputQuantity,
            long outputQuantity,
            long progressTicks,
            bool isActive)
        {
            var catalog = AcceptedFoundationCatalogFixture.CreateCatalog();
            var definition = catalog.Resolve(AcceptedFoundationCatalogFixture.Building);
            var recipe = catalog.Resolve(AcceptedFoundationCatalogFixture.Recipe);

            var building = new BuildingInstanceState(
                new BuildingInstanceId("22222222-2222-2222-2222-222222220001"),
                definition,
                new GridPosition(0, 0),
                0,
                isActive,
                new ResourceBufferState(
                    definition.InputBuffer,
                    new[] { new KeyValuePair<ResourceId, long>(AcceptedFoundationCatalogFixture.Input, inputQuantity) }),
                new ResourceBufferState(
                    definition.OutputBuffer,
                    new[] { new KeyValuePair<ResourceId, long>(AcceptedFoundationCatalogFixture.Output, outputQuantity) }),
                new ProductionState(recipe, progressTicks));

            return new FactoryState(
                new FactoryBounds(new GridPosition(0, 0), 16, 16),
                catalog,
                new[] { building });
        }

        private static string FormatUtc(DateTimeOffset value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }
    }
}
