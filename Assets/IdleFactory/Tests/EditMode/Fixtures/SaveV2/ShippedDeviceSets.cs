using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Persistence;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>One shipped-catalog device scenario: the live run state, the catalog it belongs to and the checkpoint identity.</summary>
    internal sealed class ShippedDeviceScenario
    {
        public string Name;
        public string Directory;
        public int CheckpointNumber;
        public RunCatalog Catalog;
        public RunSession Session;
        public long DepartureUtcMs;

        /// <summary>What the set is (one sentence for the README table).</summary>
        public string Summary;
    }

    /// <summary>
    /// The device checkpoint sets built against the SHIPPED catalog (T245): the catalog the player builds with
    /// <see cref="RunCatalogBuilder"/> from the FROZEN revision 2 catalog (T313: a byte copy of the shipped content at schema version 3 with the
    /// revision 2 values, <c>Fixtures/SaveV2/FrozenCatalog/iron-factory-content-r2.json</c>, pinned by SHA-256 in
    /// <c>DeviceSetShippedCatalogTests</c>), so that a later content revision never invalidates the committed sets. Never the
    /// test-local catalog of <see cref="W003Workload"/>. The first emulator run (T124, #206) was blocked because the
    /// player rejects the W003 sets (machines in slots 4 to 11 at factory level 1, which has four slots in the shipped
    /// content). Both states below are produced through the real <see cref="RunCommands"/> from a fresh run, so every
    /// unlock, slot, level and invested amount is one the shipped content can actually reach, and are written through
    /// the real <see cref="JsonRunCheckpointStore"/> like <see cref="SaveV2DeviceSets"/>. Their departure lies far in
    /// the past (November 2023), so every cold start reconciles the capped eight hours.
    /// </summary>
    internal static class ShippedDeviceSets
    {
        public const string ContentPath = "Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV2/FrozenCatalog/iron-factory-content-r2.json";
        public const string W003SShippedDirectory = "device/w003-s-shipped";
        public const string PrestigeReadyDirectory = "device/prestige-ready";

        public static readonly string[] Files = SaveV2DeviceSets.W003SFiles;

        private const string Miner = "machine.miner";
        private const string Smelter = "machine.smelter";
        private const string Press = "machine.press";
        private const string Assembler = "machine.assembler";
        private const string OverclockCore = "item.overclock-core";

        /// <summary>The frozen revision 2 catalog exactly as the player cold start builds shipped content (reader, then <see cref="RunCatalogBuilder"/>).</summary>
        public static RunCatalog LoadShippedCatalog()
        {
            RunContentReadResult read = RunContentReader.Read(File.ReadAllBytes(ContentPath));
            if (!read.IsSuccess) throw new InvalidOperationException("The shipped content was not read: " + Describe(new[] { read.Diagnostic }));
            RunCatalogLoadResult built = RunCatalogBuilder.Build(read.Document);
            if (!built.IsSuccess) throw new InvalidOperationException("The shipped content was not built: " + Describe(built.Diagnostics));
            return built.Catalog;
        }

        private static string Describe(IEnumerable<ContentDiagnostic> diagnostics) =>
            string.Join("; ", diagnostics.Select(d => d.Code + " " + d.Path));

        // ------------------------------------------------------------------------------------------------------
        // The two states
        // ------------------------------------------------------------------------------------------------------

        private sealed class Plan
        {
            public string Name;
            public string Directory;
            public int CheckpointNumber;
            public string Summary;
            public long StartingCash;
            public int FactoryExpansions;
            public int StorageExpansions;
            public int[] Counts;
            public int[] Levels;
            public bool AutoSellEverything;
            public bool ItemAndOverdrive;
            public long FinalAdvanceTicks;
        }

        private static readonly string[] Families = { Miner, Smelter, Press, Assembler };

        /// <summary>W003-S intent on the shipped content: 12 slots (3 Miners, 4 Smelters, 3 Presses, 2 Assemblers), auto-sell on every resource, one Item effect and Overdrive active.</summary>
        public static ShippedDeviceScenario BuildW003SShipped() => Build(new Plan
        {
            Name = "W003-S shipped",
            Directory = W003SShippedDirectory,
            CheckpointNumber = 30,
            Summary = "W003-S intent on the shipped catalog",
            StartingCash = 5_000_000,
            FactoryExpansions = 4,
            StorageExpansions = 4,
            Counts = new[] { 3, 4, 3, 2 },
            Levels = new[] { 25, 25, 30, 33 },
            AutoSellEverything = true,
            ItemAndOverdrive = true,
            FinalAdvanceTicks = 83_000_000,
        });

        /// <summary>A mid-run whose Factory Value meets the Sale threshold, so that the Prestige confirmation can open.</summary>
        public static ShippedDeviceScenario BuildPrestigeReady() => Build(new Plan
        {
            Name = "Prestige ready",
            Directory = PrestigeReadyDirectory,
            CheckpointNumber = 31,
            Summary = "a mid-run that meets the Sale threshold",
            StartingCash = 10_000_000,
            FactoryExpansions = 3,
            StorageExpansions = 3,
            Counts = new[] { 2, 3, 3, 2 },
            Levels = new[] { 40, 40, 40, 40 },
            AutoSellEverything = false,
            ItemAndOverdrive = false,
            FinalAdvanceTicks = 127_000_000,
        });

        private static T Ok<T>(RunCommandResult<T> result, string what)
        {
            if (!result.IsSuccess) throw new InvalidOperationException(what + " was rejected: " + result.ReasonCode);
            return result.Value;
        }

        private static ShippedDeviceScenario Build(Plan plan)
        {
            RunCatalog catalog = LoadShippedCatalog();
            long start = W003Workload.StartUtcMs;
            var permanent = new PermanentState(catalog);
            var state = new RunState(catalog, RunSimHarness.Id(9000), 1, null, plan.StartingCash, 0, 1, 1, catalog.RunStart.InitialUnlocks);
            if (new RunSimulator(catalog).Advance(state, permanent, 0, start, null) != RunAdvanceResult.Ok)
                throw new InvalidOperationException("The fresh run did not settle.");
            var session = new RunSession(catalog, state, permanent, start);
            int nextInstance = 200;
            var commands = new RunCommands(session, () => RunSimHarness.Id(nextInstance++), () => RunSimHarness.Id(7777));

            for (int i = 0; i < plan.FactoryExpansions; i++) Ok(commands.UpgradeFactory(), "UpgradeFactory");
            for (int i = 0; i < plan.StorageExpansions; i++) Ok(commands.UpgradeStorage(), "UpgradeStorage");

            // Build in slot order, so that the priority order is the slot order (upstream first). The first Miners
            // produce Iron Ore before anything else is built, which unlocks the Smelter (resourceProduced).
            var ids = new List<Guid>();
            int slot = 0;
            for (int family = 0; family < Families.Length; family++)
            {
                if (family == 1) Ok(session.Advance(21_000_000), "Advance");
                for (int n = 0; n < plan.Counts[family]; n++)
                {
                    ids.Add(Ok(commands.Build(slot++, Families[family]), "Build " + Families[family]).InstanceId);
                }
            }

            slot = 0;
            for (int family = 0; family < Families.Length; family++)
            {
                for (int n = 0; n < plan.Counts[family]; n++, slot++)
                {
                    Guid id = ids[slot];
                    while (state.FindMachine(id).Level < plan.Levels[family]) Ok(commands.Upgrade(id), "Upgrade " + Families[family]);
                }
            }

            if (plan.AutoSellEverything)
            {
                foreach (string resource in new[] { "iron.bar", "iron.gear", "iron.ore", "iron.plate" })
                    Ok(commands.SetAutoSell(resource, true), "SetAutoSell " + resource);
            }
            else
            {
                Ok(commands.SetAutoSell("iron.gear", true), "SetAutoSell iron.gear");
            }

            if (plan.ItemAndOverdrive)
            {
                if (permanent.TryAddItems(OverclockCore, 1) != RunStateResult.Ok) throw new InvalidOperationException("The Item was not granted.");
                Ok(commands.ActivateItem(OverclockCore, null), "ActivateItem");
                Ok(commands.ActivateOverdrive(), "ActivateOverdrive");
            }

            Ok(session.Advance(plan.FinalAdvanceTicks), "Advance");
            if (state.Validate() != RunStateResult.Ok) throw new InvalidOperationException("The built state is not valid.");
            if (state.Machines.Count != plan.Counts.Sum()) throw new InvalidOperationException("Unexpected machine count.");
            if (plan.ItemAndOverdrive && (state.Effects.Count != 1 || !state.Overdrive.IsActive(session.TimelineUtcMs)))
                throw new InvalidOperationException("The Item effect and Overdrive must be active at the departure.");

            return new ShippedDeviceScenario
            {
                Name = plan.Name,
                Directory = plan.Directory,
                CheckpointNumber = plan.CheckpointNumber,
                Catalog = catalog,
                Session = session,
                DepartureUtcMs = session.TimelineUtcMs,
                Summary = plan.Summary,
            };
        }

        public static FactoryValueBreakdown FactoryValueOf(ShippedDeviceScenario scenario)
        {
            if (!FactoryValuation.TryCompute(scenario.Catalog, scenario.Session.State, scenario.Session.Permanent, out FactoryValueBreakdown value))
                throw new InvalidOperationException("The Factory Value overflowed.");
            return value;
        }

        // ------------------------------------------------------------------------------------------------------
        // Files
        // ------------------------------------------------------------------------------------------------------

        /// <summary>The checkpoint written by the store into a temporary root; returns the published files and the pending file.</summary>
        public static List<SaveV2DeviceFile> BuildFiles(ShippedDeviceScenario scenario, out byte[] pendingBytes)
        {
            var checkpoint = new RunCheckpoint(
                new CheckpointId(SaveV2Scenarios.Uuid(scenario.CheckpointNumber)),
                0,
                scenario.DepartureUtcMs,
                scenario.Catalog.OfflinePolicy.Revision,
                null,
                scenario.Catalog.ContentRevision,
                null,
                scenario.Session.State,
                scenario.Session.Permanent);
            string root = RunCheckpointTestSupport.NewTempRoot();
            try
            {
                // The committed sets are Save V2 regression fixtures (T334): the internal seam writes V2 through the real protocol.
                var store = new JsonRunCheckpointStore(root, scenario.Catalog, SaveV2Mapper.LegacySchemaVersion);
                var promoted = store.Promote(checkpoint);
                if (!promoted.Succeeded) throw new InvalidOperationException("The " + scenario.Name + " checkpoint was not promoted: " + promoted.Message);
                string directory = RunCheckpointTestSupport.V2Directory(root);
                var names = System.IO.Directory.GetFiles(directory).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
                var expected = new[]
                {
                    SaveV2CheckpointFiles.DataAFileName, SaveV2CheckpointFiles.ManifestAFileName, SaveV2CheckpointFiles.PendingFileName,
                }.OrderBy(n => n, StringComparer.Ordinal).ToArray();
                if (!names.SequenceEqual(expected)) throw new InvalidOperationException("Unexpected files written by the store.");
                pendingBytes = File.ReadAllBytes(Path.Combine(directory, SaveV2CheckpointFiles.PendingFileName));
                return Files.Select(name => new SaveV2DeviceFile { Name = name, Bytes = File.ReadAllBytes(Path.Combine(directory, name)) }).ToList();
            }
            finally
            {
                RunCheckpointTestSupport.DeleteQuietly(root);
            }
        }

        public static List<SaveV2DeviceFile> BuildFiles(ShippedDeviceScenario scenario) => BuildFiles(scenario, out _);

        /// <summary>Writes the set into <paramref name="fixturesDirectory"/>/<see cref="ShippedDeviceScenario.Directory"/> (regeneration only).</summary>
        public static void Write(string fixturesDirectory, ShippedDeviceScenario scenario)
        {
            string directory = Path.Combine(fixturesDirectory, scenario.Directory);
            System.IO.Directory.CreateDirectory(directory);
            foreach (SaveV2DeviceFile file in BuildFiles(scenario)) File.WriteAllBytes(Path.Combine(directory, file.Name), file.Bytes);
        }

        // ------------------------------------------------------------------------------------------------------
        // README
        // ------------------------------------------------------------------------------------------------------

        private static string N(long value) => value.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '_');

        private static string Families_(ShippedDeviceScenario scenario)
        {
            RunState state = scenario.Session.State;
            var parts = new List<string>();
            foreach (string family in Families)
            {
                var machines = state.Machines.Where(m => m.MachineId == family).ToList();
                if (machines.Count == 0) continue;
                parts.Add(machines.Count + " x " + family.Substring("machine.".Length) + " level " + machines[0].Level + " (cycle " +
                          string.Join("/", machines.Select(m => N(m.CycleDurationTicks)).Distinct()) + " ticks)");
            }

            return string.Join(", ", parts);
        }

        /// <summary>The rows of the device table and the explanation of the shipped-catalog sets (appended to <see cref="SaveV2DeviceSets.ReadmeSection"/>).</summary>
        public static string ReadmeRowsAndNotes()
        {
            var rows = new StringBuilder();
            var notes = new StringBuilder();
            foreach (ShippedDeviceScenario scenario in new[] { BuildW003SShipped(), BuildPrestigeReady() })
            {
                FactoryValueBreakdown value = FactoryValueOf(scenario);
                RunState state = scenario.Session.State;
                string what = scenario.Name == "Prestige ready"
                    ? "Prestige ready (shipped catalog): a mid-run whose Factory Value " + N(value.Total) + " meets the Sale threshold " +
                      N(scenario.Catalog.Prestige.ThresholdFactoryValue) + ", so the Prestige confirmation can open; "
                    : "W003-S intent on the shipped catalog: twelve slots, every machine family producing, auto-sell on every resource, the Item effect `" +
                      OverclockCore + "` and Overdrive active; ";
                string scenarioText = what + "checkpoint `" + SaveV2Scenarios.Uuid(scenario.CheckpointNumber).ToString("D") + "`, generation 0, departure `" +
                                      scenario.DepartureUtcMs + "` (far in the past, so a cold start reconciles the capped eight hours), ";
                foreach (SaveV2DeviceFile file in BuildFiles(scenario))
                {
                    string detail = file.Name == SaveV2CheckpointFiles.DataAFileName
                        ? "data slot A."
                        : "manifest slot A (sequence 1, no previous generation, binds the data file by SHA-256).";
                    rows.Append("| `" + scenario.Directory + "/" + file.Name + "` | " + scenarioText + detail + " | `" + SaveV2Scenarios.Sha256(file.Bytes) + "` |\n");
                }

                notes.Append("- `" + scenario.Directory + "`: factory level " + state.FactoryLevel + " (" + state.SlotCount + " slots), storage level " + state.StorageLevel +
                             " (capacity " + N(state.Inventory.Capacity) + "), " + state.Machines.Count + " machines: " + Families_(scenario) +
                             "; auto-sell on " + string.Join(", ", state.AutoSell) + "; " + state.Effects.Count + " Item effect(s), Overdrive " +
                             (state.Overdrive.IsActive(scenario.DepartureUtcMs) ? "active" : "inactive") + "; Cash " + N(state.Cash) + ", stock value " +
                             N(value.StockValue) + ", Factory Value " + N(value.Total) + " (threshold " + N(scenario.Catalog.Prestige.ThresholdFactoryValue) + ").\n");
            }

            var text = new StringBuilder();
            text.Append(rows);
            text.Append("\n### Shipped-catalog sets (T245)\n\n");
            text.Append("`device/w003-s` above is generated against the test-local catalog of `W003Workload` (Presses and Assemblers in slots 7 to 11 at factory level 1) ");
            text.Append("and is kept unchanged for the byte-identity test; the shipped content has four slots at factory level 1 and unlocks the Assembler at level 3, so the real ");
            text.Append("player rejects it (`save.v2.invariant`, then `return.blocked`; `DeviceSetShippedCatalogTests` pins this). The two sets below are built against the catalog ");
            text.Append("the player builds with `RunCatalogBuilder` from the frozen revision 2 catalog `Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV2/FrozenCatalog/iron-factory-content-r2.json` (a byte copy of the shipped content at schema version 3, contentRevision 2, taken by T313 so that later content revisions do not invalidate these sets), through the real ");
            text.Append("`RunCommands` from a fresh run, and written through `JsonRunCheckpointStore`. `DeviceSetShippedCatalogTests` regenerates them byte for byte (drift fails, ");
            text.Append("never skips, OPS-5; `IDLEFACTORY_REGENERATE_SAVEV2_FIXTURES=1` rewrites the files, and `SaveV2FixtureTests` rewrites this README) and loads them through the player cold-start path.\n\n");
            text.Append("Composition:\n\n");
            text.Append(notes);
            text.Append("\nDeviations of `device/w003-s-shipped` from W003-S (`06-w003-s-stress.json`), each forced by the shipped content:\n\n");
            text.Append("- Catalog: the shipped catalog instead of the test-local one (different Items, machine cost curves, speed growth and milestones, storage and factory tables).\n");
            RunState w = BuildW003SShipped().Session.State;
            RunCatalog catalogW = w.Catalog;
            text.Append("- Factory level " + w.FactoryLevel + " (" + w.SlotCount + " slots) reached with " + (w.FactoryLevel - 1) + " `UpgradeFactory` commands (invested " +
                        N(w.FactoryInvestedCash) + ") instead of factory level 1 with twelve slots; storage level " + w.StorageLevel + " (capacity " +
                        N(w.Inventory.Capacity) + ", invested " + N(w.StorageInvestedCash) + ") instead of storage level 2.\n");
            text.Append("- Machine levels 25 (Miner, Smelter), 30 (Press) and 33 (Assembler) instead of level 2 with test speed tables. Speed comes from the shipped growth 1.08 per level and the milestones x2 at 10 and 25, which puts the cycles at or near the event bound (`minCycleTicks` 1_000_000) while the Item and Overdrive are active; the exact cycles are listed above.\n");
            text.Append("- The Item effect is `" + OverclockCore + "` (factory, machineSpeed, " + (catalogW.Items[OverclockCore].DurationMs / 1000) + " s) and Overdrive lasts " +
                        (catalogW.Overdrive.DurationMs / 1000) + " s (shipped values) instead of x2 effects for ten hours. Both expire a few minutes into the eight offline hours, so the reconcile has boundaries that W003-S avoids; this is the shipped content, not a choice.\n");
            text.Append("- The state is the result of commands from a fresh run (Cash 5_000_000 at the start), so the unlock order, invested Cash and a few seconds of production (mid-cycle machines) are the ones the shipped rules produce; W003-S was constructed directly. The inventory holds what the production left (auto-sell empties it).\n");
            text.Append("\nDeviations of `device/prestige-ready`: it is not a W003 variant. Starting Cash 10_000_000 spent through the commands keeps the Factory Value at the threshold (Cash and invested amounts both count), so the Factory Value only meets it because of the production of the first seconds; the machines are level 40, auto-sell is on Gear only, no Item effect and no Overdrive are active.\n");
            return text.ToString();
        }
    }
}
