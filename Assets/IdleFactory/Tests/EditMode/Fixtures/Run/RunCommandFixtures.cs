using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;

namespace IdleFactory.Tests.EditMode.Fixtures.Run
{
    /// <summary>
    /// Catalog, state and session builders for the T137 suites (RunCommandTests, RatesSnapshotTests): the literal
    /// FR-002 Iron loop with the five storage and five factory levels of the content sheet, optional speed
    /// milestones and short recipes (batches), and optional <c>upgradeCost</c> Research for the discount cases.
    /// </summary>
    internal static class RunCommandFixtures
    {
        public const long StartUtcMs = RunSimHarness.StartUtcMs;
        public const string Miner = "machine.miner";
        public const string Smelter = "machine.smelter";
        public const string Press = "machine.press";
        public const string Assembler = "machine.assembler";
        public const string MachineDiscount = "research.machine-discount";
        public const string ExpansionDiscount = "research.expansion-discount";

        public sealed class Options
        {
            /// <summary>Recipe durations in ticks: Miner, Smelter, Press, Assembler.</summary>
            public long[] Durations = { 20_000_000, 20_000_000, 40_000_000, 50_000_000 };
            public SpeedMilestone[] Milestones = new SpeedMilestone[0];
            public long RefundPpm = 500_000;
            public int MaxLevel = 25;
            public bool DiscountResearch;

            /// <summary>Unlock conditions by resource or machine ID (T140); an entry overrides the default of <see cref="NewRig"/>.</summary>
            public Dictionary<string, UnlockCondition> Unlocks;

            /// <summary>Contract offers and Items of the catalog (T100); empty by default.</summary>
            public ContractOfferDef[] Offers;
            public ItemDef[] Items;

            /// <summary>Additional Research and Industry upgrades of the catalog (T101); empty by default.</summary>
            public ResearchDef[] Research;
            public IndustryUpgradeDef[] Industry;

            /// <summary>The Overdrive definition (default: 30 s, 300 s cooldown, x2, always unlocked).</summary>
            public OverdriveDef Overdrive;

            /// <summary>When true the rig starts with Overdrive locked (its unlock is a factory level 5 condition unless <see cref="Overdrive"/> is set).</summary>
            public bool OverdriveLocked;
        }

        public static RunCatalog Catalog(Options o = null)
        {
            o = o ?? new Options();
            var always = new UnlockCondition("always");
            UnlockCondition Unlock(string id) => o.Unlocks != null && o.Unlocks.TryGetValue(id, out UnlockCondition c) ? c : always;
            var resources = new[]
            {
                new ResourceDef("iron.ore", "Iron Ore", "F003-RES-IRON-ORE", 1, Unlock("iron.ore")),
                new ResourceDef("iron.bar", "Iron Bar", "F003-RES-IRON-BAR", 3, Unlock("iron.bar")),
                new ResourceDef("iron.plate", "Iron Plate", "F003-RES-IRON-PLATE", 8, Unlock("iron.plate")),
                new ResourceDef("iron.gear", "Gear", "F003-RES-IRON-GEAR", 24, Unlock("iron.gear")),
            };
            var recipes = new[]
            {
                new MachineRecipe("recipe.mine-iron-ore", new ResourceAmount[0], new[] { new ResourceAmount("iron.ore", 2) }, o.Durations[0]),
                new MachineRecipe("recipe.smelt-iron-bar", new[] { new ResourceAmount("iron.ore", 2) }, new[] { new ResourceAmount("iron.bar", 1) }, o.Durations[1]),
                new MachineRecipe("recipe.press-iron-plate", new[] { new ResourceAmount("iron.bar", 2) }, new[] { new ResourceAmount("iron.plate", 1) }, o.Durations[2]),
                new MachineRecipe("recipe.assemble-iron-gear", new[] { new ResourceAmount("iron.plate", 2) }, new[] { new ResourceAmount("iron.gear", 1) }, o.Durations[3]),
            };
            var cost = new Curve(10, 1_150_000, "ceil");
            MachineDef Def(string id, string label, string recipe) =>
                new MachineDef(id, label, "F003-MACHINE-" + label.ToUpperInvariant(), recipe, o.MaxLevel, cost, 1_080_000, o.Milestones, Unlock(id));
            var machines = new[]
            {
                Def(Miner, "Miner", recipes[0].Id), Def(Smelter, "Smelter", recipes[1].Id),
                Def(Press, "Press", recipes[2].Id), Def(Assembler, "Assembler", recipes[3].Id),
            };
            var research = new List<ResearchDef>();
            if (o.DiscountResearch)
            {
                research.Add(new ResearchDef(MachineDiscount, "Tool Discount", 3, new Curve(5, 1_500_000, "ceil"), new ResearchPrerequisite[0],
                    new EffectDef("upgradeCost", "allMachines", null, 800_000)));
                research.Add(new ResearchDef(ExpansionDiscount, "Builder Discount", 3, new Curve(5, 1_500_000, "ceil"), new ResearchPrerequisite[0],
                    new EffectDef("upgradeCost", "expansion", null, 900_000)));
            }
            if (o.Research != null) research.AddRange(o.Research);
            return new RunCatalog(1, resources, recipes, machines,
                new[] { new StorageLevelDef(1, 1000, 0), new StorageLevelDef(2, 2000, 2500), new StorageLevelDef(3, 4000, 10000),
                    new StorageLevelDef(4, 8000, 40000), new StorageLevelDef(5, 16000, 160000) },
                new[] { new FactoryLevelDef(1, 4, 0), new FactoryLevelDef(2, 6, 5000), new FactoryLevelDef(3, 8, 20000),
                    new FactoryLevelDef(4, 10, 80000), new FactoryLevelDef(5, 12, 320000) },
                o.Offers ?? new ContractOfferDef[0], o.Items ?? new ItemDef[0], research, o.Industry ?? new IndustryUpgradeDef[0],
                o.Overdrive ?? new OverdriveDef(30000, 300000, 2_000_000, o.OverdriveLocked ? new UnlockCondition("factoryLevelAtLeast", null, 5) : always),
                new PrestigeDef(10_000_000, "sqrt", 10, 10_000_000),
                new RunStartDef(25, new[] { "iron.ore", Miner }, o.RefundPpm, 1_000_000, 100_000_000, 4, 2_000_000),
                new RunOfflinePolicy(1, 4, 5, 288_000_000_000L));
        }

        /// <summary>A live run with its commands and a deterministic instance-ID source (1, 2, 3, ...).</summary>
        public sealed class Rig
        {
            public RunCatalog Catalog;
            public RunState State;
            public RunSession Session;
            public RunCommands Commands;
            public int NextId = 1;

            public RunCommandResult<BuildResult> Build(int slot, string machineId) => Commands.Build(slot, machineId);

            public Guid Id(int n) => RunSimHarness.Id(n);

            public void Advance(long ticks)
            {
                RunCommandResult<long> r = Session.Advance(ticks);
                if (!r.IsSuccess) throw new InvalidOperationException("Advance rejected: " + r.ReasonCode);
            }
        }

        /// <param name="unlockedMachines">Machine IDs unlocked at the start (default: all four); resources are always unlocked unless listed in <paramref name="lockedResources"/>.</param>
        public static Rig NewRig(Options o = null, long cash = 100_000, int storageLevel = 1, int factoryLevel = 1,
            string[] unlockedMachines = null, string[] lockedResources = null, long rp = 0)
        {
            // An entry the rig starts locked keeps a real condition that does not hold at the start (factory level 5,
            // the last one), so the unlock pass of T140 leaves it locked until a test opens it.
            o = o ?? new Options();
            var conditions = new Dictionary<string, UnlockCondition>(o.Unlocks ?? new Dictionary<string, UnlockCondition>());
            var levelFive = new UnlockCondition("factoryLevelAtLeast", null, 5);
            foreach (string id in new[] { "iron.ore", "iron.bar", "iron.plate", "iron.gear" })
                if (lockedResources != null && lockedResources.Contains(id) && !conditions.ContainsKey(id)) conditions[id] = levelFive;
            foreach (string id in new[] { Miner, Smelter, Press, Assembler })
                if (unlockedMachines != null && !unlockedMachines.Contains(id) && !conditions.ContainsKey(id)) conditions[id] = levelFive;
            o.Unlocks = conditions;
            RunCatalog catalog = Catalog(o);
            var unlocked = new List<string>();
            foreach (string r in new[] { "iron.ore", "iron.bar", "iron.plate", "iron.gear" })
                if (lockedResources == null || !lockedResources.Contains(r)) unlocked.Add(r);
            unlocked.AddRange(unlockedMachines ?? new[] { Miner, Smelter, Press, Assembler });
            if (!o.OverdriveLocked) unlocked.Add(RunState.OverdriveUnlockId);
            var state = new RunState(catalog, RunSimHarness.Id(9000), 1, null, cash, rp, storageLevel, factoryLevel, unlocked);
            var rig = new Rig { Catalog = catalog, State = state };
            rig.Session = new RunSession(catalog, state, new PermanentState(catalog), StartUtcMs);
            rig.Commands = new RunCommands(rig.Session, () => RunSimHarness.Id(rig.NextId++));
            return rig;
        }

        /// <summary>
        /// The run state as text including the members <see cref="RunSimHarness.Fingerprint"/> does not print:
        /// unlocks, auto-sell, expansion levels and invested amounts, and the research levels.
        /// </summary>
        public static string Snapshot(RunState s)
        {
            var b = new StringBuilder(RunSimHarness.Fingerprint(s));
            b.Append("unlocked=").Append(string.Join(",", s.Unlocked)).Append('\n');
            b.Append("autosell=").Append(string.Join(",", s.AutoSell)).Append('\n');
            b.Append("storage=").Append(s.StorageLevel).Append('/').Append(s.StorageInvestedCash)
                .Append(" factory=").Append(s.FactoryLevel).Append('/').Append(s.FactoryInvestedCash).Append('\n');
            b.Append("research=").Append(string.Join(",", s.ResearchLevels().Select(r => r.Id + ":" + r.Count))).Append('\n');
            return b.ToString();
        }

        /// <summary>The Factory Value parts that the factory commands move: Cash, invested machines, invested expansion, stock at base prices.</summary>
        public static long FactoryValue(RunState s)
        {
            long value = s.Cash + s.StorageInvestedCash + s.FactoryInvestedCash;
            foreach (MachineInstance m in s.Machines) value += m.InvestedCash;
            for (int i = 0; i < s.Inventory.Count; i++)
                value += s.Inventory.UnitsAt(i) * s.Catalog.Resources[s.Inventory.IdAt(i)].BasePrice;
            return value;
        }
    }
}
