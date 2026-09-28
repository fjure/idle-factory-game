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
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// Regenerates every Save V1 fixture under
    /// <c>Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV1/</c> (T070c) in memory from the
    /// same deterministic inputs used to produce them, and asserts each is byte-identical to
    /// the committed file. This runs in the normal EditMode suite as a reproducibility
    /// regression check: it proves the committed fixtures still match what the unchanged
    /// <see cref="SaveV1Mapper"/> and Core domain types (via
    /// <see cref="AcceptedFoundationCatalogFixture"/> and <see cref="FoundationFixtures"/>,
    /// not the ScriptableObject content path, so the check survives its retirement in T079)
    /// produce today.
    ///
    /// By default nothing on disk is written; only the in-memory bytes are compared against
    /// the committed files. Set the environment variable
    /// <c>IDLEFACTORY_REGENERATE_SAVEV1_FIXTURES=1</c> before running this test (see
    /// <c>Fixtures/SaveV1/README.md</c> for the exact command) to (re)write the committed
    /// files and <c>README.md</c> — the round-trip and byte-identity assertions still run
    /// afterward, against the file just written.
    /// </summary>
    public sealed class GenerateSaveV1FixturesTests
    {
        private const string OutputDirectoryRelativeToAssets = "IdleFactory/Tests/EditMode/Fixtures/SaveV1";
        private const string OutputDirectory = "Assets/" + OutputDirectoryRelativeToAssets;
        private const string RegenerateEnvironmentVariable = "IDLEFACTORY_REGENERATE_SAVEV1_FIXTURES";

        // origin/main HEAD this worktree branched from (PR #137 merge, see AGENTS.md gitStatus).
        private const string CaptureCommit = "51603f2";

        private static readonly DateTimeOffset DepartureUtc =
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private static readonly TimeSpan OfflineReturnInterval = TimeSpan.FromHours(8);

        private static bool RegenerateFixtures =>
            Environment.GetEnvironmentVariable(RegenerateEnvironmentVariable) == "1";

        // Application.dataPath is the project's Assets folder itself, so this resolves to the
        // real fixture directory in both the Editor and a built player, without depending on
        // the process's current working directory or any absolute machine path.
        private static string AbsoluteOutputDirectory =>
            Path.Combine(Application.dataPath, OutputDirectoryRelativeToAssets);

        [Test]
        public void GenerateFixtures()
        {
            if (RegenerateFixtures)
            {
                Directory.CreateDirectory(AbsoluteOutputDirectory);
            }

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

            if (RegenerateFixtures)
            {
                File.WriteAllText(
                    Path.Combine(AbsoluteOutputDirectory, "README.md"),
                    readme.ToString(),
                    new UTF8Encoding(false));

                // The scenario files above are written with raw System.IO, bypassing the asset
                // import pipeline; refresh so Unity generates their .meta files before the commit.
                AssetDatabase.Refresh();
            }
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
            readme.AppendLine(
                "By default, `GenerateSaveV1FixturesTests.GenerateFixtures` runs as a reproducibility " +
                "regression check in the normal EditMode suite: it regenerates all six fixtures in memory " +
                "and asserts each is byte-identical to the committed file, without writing anything to " +
                "disk. To intentionally regenerate the committed files (and this README) after a deliberate " +
                "change, set `IDLEFACTORY_REGENERATE_SAVEV1_FIXTURES=1` and run the test file directly:");
            readme.AppendLine();
            readme.AppendLine(
                "```powershell\n" +
                "$env:IDLEFACTORY_REGENERATE_SAVEV1_FIXTURES='1'; scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/GenerateSaveV1FixturesTests.cs\n" +
                "```");
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

            // Round-trip through Deserialize to prove the fixture is valid Save V1 content against its
            // own catalog before it is compared against (or written to) the committed file.
            var roundTrip = SaveV1Mapper.Deserialize(serialized.Value, checkpoint.CreateFactoryCopy().Catalog);
            Assert.That(roundTrip.Succeeded, Is.True, $"Round-trip failed for scenario '{title}': {roundTrip.Message}");

            var absolutePath = Path.Combine(AbsoluteOutputDirectory, fileName);
            if (RegenerateFixtures)
            {
                File.WriteAllBytes(absolutePath, serialized.Value);
            }

            Assert.That(
                File.Exists(absolutePath),
                Is.True,
                $"Committed fixture is missing: {fileName}. Set {RegenerateEnvironmentVariable}=1 and rerun to generate it.");
            var committedBytes = File.ReadAllBytes(absolutePath);
            Assert.That(
                committedBytes,
                Is.EqualTo(serialized.Value),
                $"Scenario '{title}' ({fileName}) is not byte-identical to its in-memory regeneration from the " +
                $"same deterministic inputs. If this content change is intentional, set " +
                $"{RegenerateEnvironmentVariable}=1 and rerun to update the committed fixture.");

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
