using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Persistence;

namespace IdleFactory.Tests.EditMode.Fixtures.SaveV3
{
    /// <summary>One W005-L factory: the catalog it belongs to, the live session at the departure and the written Save V3 bytes.</summary>
    internal sealed class W005LScenario
    {
        public RunCatalog Catalog;
        public RunSession Session;
        public long DepartureUtcMs;
        public byte[] Bytes;
    }

    /// <summary>
    /// The W005-L performance workload of T336 (plan.md, Performance; research.md F5-R14; spec.md SC-007): a Save V3 factory near the run-3
    /// threshold, built against the SHIPPED catalog (<see cref="ContentPath"/>) through the real <see cref="RunCommands"/> from a fresh run, then
    /// written through the real <see cref="SaveV2Mapper"/>. It is deliberately free of NUnit and Unity API, so a plain C# harness can run it.
    ///
    /// Shape, from the policy-r2 trace row <c>run3,75.33E</c> of <c>Fixtures/Balance/trace-r2.csv</c> (the eligibility instant of run 3):
    /// factory level 5 (12 slots), storage level 5, 3 Miners at level 66, 4 Smelters at level 63, 3 Presses at level 60 and 2 Assemblers at
    /// level 48, the Research levels of that row, Industry upgrades bought with the IP of two earlier prestiges, the run-3 completion counts of the
    /// trace (gear 51, plate 36, timed bar 52, bonus plate 1), auto-sell on the terminal resource Gear only (the policy's rule 4.3) and the Gear
    /// delivery contract accepted with auto-deliver on (the policy's rule 4.2). A gear contract at completion index 51 asks for 166 units, held
    /// back from auto-sell by the contract reserve, so the 8 h return exercises auto-sell, the reserve and auto-delivery together.
    ///
    /// Cash, RP and IP are not tuned by hand: the build runs twice, the first time with ample amounts to measure what the commands spend, the second
    /// with exactly that plus a small remainder, so every purchase is paid by a real command and the remainder is a recorded constant.
    /// The result depends on the shipped content, so a content retune (T345) changes the bytes and the regeneration rule applies (PF-16).
    /// </summary>
    internal static class W005LWorkload
    {
        public const string ContentPath = "Assets/IdleFactory/Content/Iron/iron-factory-content.json";
        public const string FileName = "01-w005-l.json";
        public const long StartUtcMs = 1_700_000_000_000L;
        public const int CheckpointNumber = 50;
        public const int RunNumber = 3;
        public const long PrestigeCount = 2;

        /// <summary>The Cash, RP and IP the build keeps after the purchases (the Cash of the trace row).</summary>
        public const long RemainderCash = 1_004;
        public const long RemainderRp = 7;
        public const long RemainderIp = 2;

        /// <summary>Seconds of production after the last command, so the machines are mid-cycle and the stock holds a chain mix.</summary>
        public const long SettleSeconds = 120;

        private const string Miner = "machine.miner";
        private const string Smelter = "machine.smelter";
        private const string Press = "machine.press";
        private const string Assembler = "machine.assembler";
        private const string GearContract = "contract.gear";

        public static readonly string[] Families = { Miner, Smelter, Press, Assembler };
        public static readonly int[] Counts = { 3, 4, 3, 2 };
        public static readonly int[] Levels = { 66, 63, 60, 48 };

        /// <summary>The Research levels of the trace row, in purchase order (prerequisites first).</summary>
        public static readonly KeyValuePair<string, int>[] Research =
        {
            new KeyValuePair<string, int>("research.miner-tuning", 3),
            new KeyValuePair<string, int>("research.smelter-tuning", 2),
            new KeyValuePair<string, int>("research.press-tuning", 2),
            new KeyValuePair<string, int>("research.assembler-tuning", 2),
            new KeyValuePair<string, int>("research.bargaining", 2),
            new KeyValuePair<string, int>("research.market-analysis", 2),
            new KeyValuePair<string, int>("research.tool-discount", 2),
            new KeyValuePair<string, int>("research.deep-veins", 1),
            new KeyValuePair<string, int>("research.warehouse-planning", 2),
        };

        /// <summary>The Industry levels the two earlier prestiges paid for (greedy by gain per IP, balance-policy-r2.md section 5).</summary>
        public static readonly KeyValuePair<string, int>[] Industry =
        {
            new KeyValuePair<string, int>("industry.calibration", 2),
            new KeyValuePair<string, int>("industry.market-contacts", 1),
            new KeyValuePair<string, int>("industry.toolmaker", 1),
        };

        /// <summary>The run-3 completion counts of the trace (<c>delivered.*</c>), restored through the board's load path.</summary>
        public static readonly KeyValuePair<string, long>[] Completions =
        {
            new KeyValuePair<string, long>("contract.bonus-plate", 1),
            new KeyValuePair<string, long>("contract.gear", 51),
            new KeyValuePair<string, long>("contract.plate", 36),
            new KeyValuePair<string, long>("contract.timed-bar", 52),
        };

        public static Guid Id(int n) => new Guid(n, 0, 0, new byte[8]);

        public static Guid CheckpointId() => new Guid("22222222-2222-2222-2222-" + CheckpointNumber.ToString("D12"));

        /// <summary>The shipped catalog exactly as the player builds it (reader, then <see cref="RunCatalogBuilder"/>).</summary>
        public static RunCatalog LoadShippedCatalog(string contentPath = ContentPath)
        {
            RunContentReadResult read = RunContentReader.Read(File.ReadAllBytes(contentPath));
            if (!read.IsSuccess) throw new InvalidOperationException("The shipped content was not read: " + read.Diagnostic.Code + " " + read.Diagnostic.Path);
            RunCatalogLoadResult built = RunCatalogBuilder.Build(read.Document);
            if (!built.IsSuccess)
                throw new InvalidOperationException("The shipped content was not built: " + string.Join("; ", built.Diagnostics.Select(d => d.Code + " " + d.Path)));
            return built.Catalog;
        }

        private static T Ok<T>(RunCommandResult<T> result, string what)
        {
            if (!result.IsSuccess) throw new InvalidOperationException(what + " was rejected: " + result.ReasonCode);
            return result.Value;
        }

        /// <summary>Builds the scenario against <paramref name="catalog"/> and writes its Save V3 bytes.</summary>
        public static W005LScenario Build(RunCatalog catalog)
        {
            // Pass 1 measures the spend with ample amounts; pass 2 starts with exactly the spend plus the remainder.
            const long Ample = 1_000_000_000_000_000L;
            Assemble(catalog, Ample, Ample, 1_000, out long cash1, out long rp1, out long ip1);
            long spentCash = Ample - cash1, spentRp = Ample - rp1, spentIp = 1_000 - ip1;
            RunSession session = Assemble(catalog, spentCash + RemainderCash, spentRp + RemainderRp, spentIp + RemainderIp,
                out long cash, out long rp, out long ip);
            if (cash != RemainderCash || rp != RemainderRp || ip != RemainderIp)
                throw new InvalidOperationException("The second pass did not leave the planned remainder: " + cash + "/" + rp + "/" + ip);

            RunState state = session.State;
            if (state.Validate() != RunStateResult.Ok) throw new InvalidOperationException("The built state is not valid.");

            var meta = new SaveV2Meta(CheckpointId(), 0, session.TimelineUtcMs, catalog.OfflinePolicy.Revision, null, catalog.ContentRevision, null);
            SaveV2WriteResult written = SaveV2Mapper.Serialize(meta, state, session.Permanent);
            if (!written.IsSuccess) throw new InvalidOperationException("The W005-L state was not written: " + written.Code);
            return new W005LScenario { Catalog = catalog, Session = session, DepartureUtcMs = session.TimelineUtcMs, Bytes = written.Bytes };
        }

        private static RunSession Assemble(RunCatalog catalog, long cash, long rp, long ip, out long cashLeft, out long rpLeft, out long ipLeft)
        {
            var permanent = new PermanentState(catalog, ip, PrestigeCount);
            var unlocked = new List<string>(catalog.RunStart.InitialUnlocks);
            var state = new RunState(catalog, Id(9003), RunNumber, Id(9002), cash, rp, 1, 1, unlocked);
            if (new RunSimulator(catalog).Advance(state, permanent, 0, StartUtcMs, null) != RunAdvanceResult.Ok)
                throw new InvalidOperationException("The fresh run did not settle.");
            var session = new RunSession(catalog, state, permanent, StartUtcMs);
            int nextInstance = 200;
            var commands = new RunCommands(session, () => Id(nextInstance++), () => Id(7777));

            // Industry first (permanent), then the run: factory and storage, Miners, Research, the rest of the chain, levels.
            foreach (var u in Industry)
                for (int level = 0; level < u.Value; level++) Ok(commands.PurchaseIndustry(u.Key), "PurchaseIndustry " + u.Key);
            for (int i = 0; i < 4; i++) Ok(commands.UpgradeFactory(), "UpgradeFactory");
            for (int i = 0; i < 4; i++) Ok(commands.UpgradeStorage(), "UpgradeStorage");

            // Build in slot order, so that the priority order is the slot order (upstream first). The first Miners produce Iron Ore before
            // anything else is built, which unlocks the Smelter (resourceProduced).
            var ids = new List<Guid>();
            int slot = 0;
            for (int family = 0; family < Families.Length; family++)
            {
                if (family == 1) Ok(session.Advance(21_000_000), "Advance");
                for (int n = 0; n < Counts[family]; n++) ids.Add(Ok(commands.Build(slot++, Families[family]), "Build " + Families[family]).InstanceId);
            }

            foreach (var r in Research)
                for (int level = 0; level < r.Value; level++) Ok(commands.PurchaseResearch(r.Key), "PurchaseResearch " + r.Key);

            slot = 0;
            for (int family = 0; family < Families.Length; family++)
            {
                for (int n = 0; n < Counts[family]; n++, slot++)
                {
                    Guid id = ids[slot];
                    while (state.FindMachine(id).Level < Levels[family]) Ok(commands.Upgrade(id), "Upgrade " + Families[family]);
                }
            }

            foreach (var c in Completions)
                if (state.Contracts.TrySetCompletionCount(c.Key, c.Value) != RunStateResult.Ok)
                    throw new InvalidOperationException("The completion count of " + c.Key + " was not restored.");

            // The policy's rules 4.3 and 4.2: auto-sell on the terminal resource only; the Gear contract accepted with auto-deliver.
            Ok(commands.SetAutoSell("iron.gear", true), "SetAutoSell iron.gear");
            cashLeft = state.Cash;
            rpLeft = state.Rp;
            ipLeft = permanent.Ip;

            // Production after the last purchase: the machines run mid-cycle, the chain fills the stock and Gear is produced, which unlocks the Gear
            // contract. The contract is accepted at the departure instant, so the 8 h return starts with it active and auto-deliver on.
            Ok(session.Advance(SettleSeconds * 10_000_000L), "Advance");
            if (!state.IsUnlocked(GearContract)) throw new InvalidOperationException("No Gear was produced in the settle time.");
            Ok(commands.AcceptContract(GearContract), "AcceptContract");
            Ok(commands.SetAutoDeliver(true), "SetAutoDeliver");
            if (state.Machines.Count != Counts.Sum()) throw new InvalidOperationException("Unexpected machine count.");
            return session;
        }

        private static string N(long value) => value.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '_');

        /// <summary>The README of <c>Fixtures/SaveV3</c> (regenerated byte for byte by <c>W005LFixtureTests</c>).</summary>
        public static string Readme(W005LScenario scenario)
        {
            RunState state = scenario.Session.State;
            PermanentState permanent = scenario.Session.Permanent;
            ActiveContract active = state.Contracts.Active;
            var families = new List<string>();
            foreach (string family in Families)
            {
                var machines = state.Machines.Where(m => m.MachineId == family).ToList();
                families.Add(machines.Count + " x " + family.Substring("machine.".Length) + " level " + machines[0].Level + " (cycle " +
                             string.Join("/", machines.Select(m => N(m.CycleDurationTicks)).Distinct()) + " ticks)");
            }

            var stock = new List<string>();
            for (int i = 0; i < state.Inventory.Count; i++) stock.Add(state.Inventory.IdAt(i) + " " + N(state.Inventory.UnitsAt(i)));
            var b = new StringBuilder();
            b.Append("# Save V3 fixtures\n\n");
            b.Append("Generated by `W005LFixtureTests.CommittedFixture_IsByteIdenticalToItsInMemoryRegeneration` (T336, issue #401) from `W005LWorkload`, the unchanged Core run domain, ");
            b.Append("the shipped catalog (`" + ContentPath + "`) and `SaveV2Mapper` (which writes Save V3). The test regenerates the file and this README in memory and compares them ");
            b.Append("byte for byte with the committed copies. Drift fails the test; it never skips (OPS-5). To regenerate deliberately, set ");
            b.Append("`IDLEFACTORY_REGENERATE_SAVEV3_FIXTURES=1` and run the test file directly:\n\n");
            b.Append("```powershell\n");
            b.Append("$env:IDLEFACTORY_REGENERATE_SAVEV3_FIXTURES='1'; scripts/test-unity.ps1 -TestFile Assets/IdleFactory/Tests/EditMode/W005LFixtureTests.cs\n");
            b.Append("```\n\n");
            b.Append("Hashes are SHA-256 over the file bytes. The StandalonePlayer copy is `Assets/IdleFactory/Tests/PlayMode/Fixtures/SaveV3/Resources/W005PlayerFixtures/" + FileName + "`; ");
            b.Append("`SaveV3PlayerFixtureParityTests` proves it is byte-identical. The W005-L content revision is the shipped one, so a content retune (T345, step 16) changes ");
            b.Append("the bytes: regenerate, copy to the player folder, and record both hashes and both timings in `specs/005-economy-contracts/reviews/w005l-measurement.md` (PF-16).\n\n");
            b.Append("| File | Scenario | Bytes | SHA-256 |\n|---|---|---|---|\n");
            b.Append("| `" + FileName + "` | W005-L: a run-3 factory near the Sale threshold (policy-r2 trace row `run3,75.33E`) on the shipped catalog, 12 slots, the chain mix, the Gear contract ");
            b.Append("active with auto-deliver on and auto-sell on Gear. | " + N(scenario.Bytes.Length) + " | `" + Sha256(scenario.Bytes) + "` |\n\n");
            b.Append("## Composition of `" + FileName + "`\n\n");
            b.Append("- Run " + state.RunNumber + " (previous run set), prestige count " + permanent.PrestigeCount + ", IP " + permanent.Ip + ", Industry " +
                     string.Join(", ", permanent.IndustryLevels().Select(l => l.Id + " " + l.Count)) + ".\n");
            b.Append("- Factory level " + state.FactoryLevel + " (" + state.SlotCount + " slots), storage level " + state.StorageLevel + " (capacity " + N(state.Inventory.Capacity) +
                     "), " + state.Machines.Count + " machines: " + string.Join(", ", families) + ".\n");
            b.Append("- Research " + string.Join(", ", state.ResearchLevels().Select(l => l.Id.Substring("research.".Length) + " " + l.Count)) + ".\n");
            b.Append("- Cash " + N(state.Cash) + ", RP " + N(state.Rp) + ", stock " + string.Join(", ", stock) + "; auto-sell on " + string.Join(", ", state.AutoSell) + ".\n");
            b.Append("- Completions " + string.Join(", ", state.Contracts.CompletionCounts().Select(c => c.Id + " " + c.Count)) + " (run-wide total " +
                     state.Contracts.RunCompletionTotal + "); active contract `" + active.OfferId + "` requiring " + N(active.RequiredUnits) + " units, auto-deliver " +
                     (active.AutoDeliver ? "on" : "off") + ".\n");
            b.Append("- Checkpoint `" + CheckpointId().ToString("D") + "`, generation 0, departure `" + scenario.DepartureUtcMs +
                     "` (far in the past, so a cold start reconciles the capped eight hours).\n\n");
            b.Append("## How the state is built\n\n");
            b.Append("`W005LWorkload.Build` runs the real `RunCommands` from a fresh run 3 against the shipped catalog: Industry purchases, four factory and four storage expansions, ");
            b.Append("Miners, Smelters, Presses and Assemblers in slot order, the Research levels, the machine levels (66, 63, 60 and 48, the maxima of the trace row), the run-3 completion counts ");
            b.Append("through the board's load path, auto-sell on Gear (the policy's terminal-resource rule 4.3), then " + SettleSeconds + " s of production so that Gear is produced, ");
            b.Append("then `AcceptContract` of the Gear contract and `SetAutoDeliver(true)` (rule 4.2). The build runs twice: the first pass measures what the commands spend, the second starts with ");
            b.Append("exactly that plus a remainder (before the settle) of Cash " + N(RemainderCash) + ", RP " + RemainderRp + " and IP " + RemainderIp + ", so no resource is tuned by hand.\n");
            return b.ToString();
        }

        public static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
            }
        }
    }
}
