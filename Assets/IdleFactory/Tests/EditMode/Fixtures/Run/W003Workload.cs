using System;
using System.Collections.Generic;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;

namespace IdleFactory.Tests.EditMode.Fixtures.Run
{
    /// <summary>The W003 variants of plan.md, Performance. <see cref="Bound"/> is an addition described below.</summary>
    public enum W003Variant
    {
        /// <summary>W003-S: every recipe at the event bound (batch of at least 2, cycle = minCycleTicks) and auto-sell on every resource.</summary>
        Stress,

        /// <summary>W003-R: the provisional 120-minute reference state, auto-sell on Gear only, store at 50 %.</summary>
        Reference,

        /// <summary>
        /// W003-B (not in plan.md): the same event-bound machines as <see cref="Stress"/> with auto-sell on Gear only and
        /// balanced batches, so that all twelve machines complete every 0.1 s and the theoretical bound of
        /// 12 x E(8 h) / minCycleTicks completions is actually reached. With auto-sell on every resource the
        /// intermediate stocks are sold at each timestamp, which starves every machine but the Miners.
        /// </summary>
        Bound,
    }

    /// <summary>One W003 factory: content, run state, permanent state and the real instant of simulation offset 0.</summary>
    public sealed class W003Scenario
    {
        public string Name { get; set; }
        public W003Variant Variant { get; set; }
        public RunCatalog Catalog { get; set; }
        public RunState State { get; set; }
        public PermanentState Permanent { get; set; }
        public long StartUtcMs { get; set; }
    }

    /// <summary>
    /// The shared workload builder of T098, reused by T104, T122 and T124 (QA-7). Twelve slots: 3 Miners, 4
    /// Smelters, 3 Presses and 2 Assemblers in priority order (upstream first), one Item effect and Overdrive
    /// active for longer than the eight-hour worst case, so that no boundary interrupts a measurement. The
    /// content is test-local and provisional: the committed Save V2 fixtures of T104 replace the reference shape.
    /// </summary>
    public static class W003Workload
    {
        public const long StartUtcMs = 1_700_000_000_000L;
        private const long TenHoursMs = 10L * 3600 * 1000;

        public static W003Scenario Build(W003Variant variant)
        {
            return variant == W003Variant.Reference ? BuildReference() : BuildEventBound(variant);
        }

        // Machine counts and slot order (also the priority order).
        private static readonly string[] Types =
        {
            "machine.miner", "machine.miner", "machine.miner",
            "machine.smelter", "machine.smelter", "machine.smelter", "machine.smelter",
            "machine.press", "machine.press", "machine.press",
            "machine.assembler", "machine.assembler",
        };

        private static RunCatalog Catalog(long[] level2SpeedPpm, int maxLevel, long growthPpm, IReadOnlyList<SpeedMilestone> milestones,
            long itemFactorPpm, long maxCombinedSpeedPpm, long storageCapacity)
        {
            var always = new UnlockCondition("always");
            var resources = new[]
            {
                new ResourceDef("iron.ore", "Iron Ore", "F003-RES-IRON-ORE", 1, always),
                new ResourceDef("iron.bar", "Iron Bar", "F003-RES-IRON-BAR", 3, always),
                new ResourceDef("iron.plate", "Iron Plate", "F003-RES-IRON-PLATE", 8, always),
                new ResourceDef("iron.gear", "Gear", "F003-RES-IRON-GEAR", 24, always),
            };
            var recipes = new[]
            {
                new MachineRecipe("recipe.mine-iron-ore", new ResourceAmount[0], new[] { new ResourceAmount("iron.ore", 2) }, 20_000_000),
                new MachineRecipe("recipe.smelt-iron-bar", new[] { new ResourceAmount("iron.ore", 2) }, new[] { new ResourceAmount("iron.bar", 1) }, 20_000_000),
                new MachineRecipe("recipe.press-iron-plate", new[] { new ResourceAmount("iron.bar", 2) }, new[] { new ResourceAmount("iron.plate", 1) }, 40_000_000),
                new MachineRecipe("recipe.assemble-iron-gear", new[] { new ResourceAmount("iron.plate", 2) }, new[] { new ResourceAmount("iron.gear", 1) }, 50_000_000),
            };
            var cost = new Curve(10, 1_150_000, "ceil");
            string[] ids = { "machine.miner", "machine.smelter", "machine.press", "machine.assembler" };
            string[] labels = { "Miner", "Smelter", "Press", "Assembler" };
            var machines = new MachineDef[4];
            for (int i = 0; i < 4; i++)
            {
                // With a level-2 speed table entry equal to the growth (no milestones), a speed of growth ppm at level 2.
                long growth = level2SpeedPpm != null ? level2SpeedPpm[i] : growthPpm;
                machines[i] = new MachineDef(ids[i], labels[i], "F003-MACHINE-" + labels[i].ToUpperInvariant(), recipes[i].Id,
                    maxLevel, cost, growth, milestones ?? new SpeedMilestone[0], always);
            }
            var items = new[]
            {
                new ItemDef("item.boost", 1, "Boost", "F003-ITEM-BOOST", TenHoursMs, "factory", new string[0],
                    new EffectDef("machineSpeed", "allMachines", null, itemFactorPpm)),
            };
            return IdleFactory.Tests.EditMode.Run.TestCatalogBuilder.Create().WithResources(resources).WithRecipes(recipes).WithMachines(machines)
                .WithStorageLevels(
                new[] { new StorageLevelDef(1, 1000, 0), new StorageLevelDef(2, storageCapacity, 2500) })
                .WithFactoryLevels(new[] { new FactoryLevelDef(1, 12, 0) })
                .WithItems(items)
                .WithOverdrive(new OverdriveDef(TenHoursMs, 300_000, 2_000_000, always))
                .WithPrestige(new PrestigeDef(10_000_000, "sqrt", 10, 10_000_000))
                .WithRunStart(new RunStartDef(25, new[] { "iron.ore", "machine.miner" }, 500_000, 1_000_000,
                    maxCombinedSpeedPpm, 4, 2_000_000))
                .Build();
        }

        private static W003Scenario Populate(RunCatalog catalog, W003Variant variant, string name, Func<int, int> levelOf, int storageLevel)
        {
            var unlocked = new List<string>(RunSimHarness.AllResources);
            foreach (string id in new[] { "machine.miner", "machine.smelter", "machine.press", "machine.assembler" }) unlocked.Add(id);
            var state = new RunState(catalog, RunSimHarness.Id(9000), 1, null, 1_000, 0, storageLevel, 1, unlocked);
            for (int slot = 0; slot < Types.Length; slot++)
            {
                MachineDef def = catalog.Machines[Types[slot]];
                long cycle = catalog.Recipes[def.RecipeId].DurationTicks;
                state.TryAddMachine(new MachineInstance(RunSimHarness.Id(200 + slot), def, slot, levelOf(slot), true, 0, cycle, 100));
            }
            state.TryAddEffect(new ActiveEffect("item.boost", new ConflictKey("machineSpeed", "factory"), StartUtcMs, StartUtcMs + TenHoursMs));
            state.Overdrive.TryActivate(StartUtcMs);
            return new W003Scenario
            {
                Name = name,
                Variant = variant,
                Catalog = catalog,
                State = state,
                Permanent = new PermanentState(catalog),
                StartUtcMs = StartUtcMs,
            };
        }

        /// <summary>
        /// Stress and Bound. Level-2 machines with speed tables 4.0e7, 3.0e7, 4.0e7 and 3.75e7 ppm compose (Item x2,
        /// Overdrive x2) to 1.6e8, 1.2e8, 1.6e8 and 1.5e8 ppm, which for durations of 2, 2, 4 and 5 s gives batches
        /// of 8, 6, 4 and 3 units at a cycle of exactly 1,000,000 ticks (OD-6). The unit flow is balanced: 3 Miners
        /// make 48 Ore per cycle, 4 Smelters use 48 and make 24 Bars, 3 Presses use 24 and make 12 Plates, and 2
        /// Assemblers use 12 and make 6 Gears.
        /// </summary>
        private static W003Scenario BuildEventBound(W003Variant variant)
        {
            RunCatalog catalog = Catalog(new long[] { 40_000_000, 30_000_000, 40_000_000, 37_500_000 }, 2, 1_080_000, null,
                2_000_000, 1_000_000_000L, 16_000);
            W003Scenario sc = Populate(catalog, variant, variant == W003Variant.Stress ? "W003-S" : "W003-B", slot => 2, 2);
            if (variant == W003Variant.Stress)
            {
                foreach (string id in RunSimHarness.AllResources) sc.State.TrySetAutoSell(id, true);
            }
            else
            {
                sc.State.TrySetAutoSell("iron.gear", true);
                // A small seed lets every stage run from the first cycle.
                sc.State.Inventory.TryImport(new[]
                {
                    new ResourceAmount("iron.ore", 96), new ResourceAmount("iron.bar", 48), new ResourceAmount("iron.plate", 24),
                });
            }
            return sc;
        }

        /// <summary>
        /// W003-R: a provisional reference shape. Miners at level 12, Smelters at 8, Presses at 6 and Assemblers at 4, growth 1.08 with
        /// milestones x2 at 10 and x2 at 25, a factory Item x1.2, Overdrive x2, storage capacity 4,000 at 50 %.
        /// </summary>
        private static W003Scenario BuildReference()
        {
            RunCatalog catalog = Catalog(null, 25, 1_080_000,
                new[] { new SpeedMilestone(10, 2_000_000), new SpeedMilestone(25, 2_000_000) },
                1_200_000, 100_000_000L, 4_000);
            W003Scenario sc = Populate(catalog, W003Variant.Reference, "W003-R",
                slot => slot < 3 ? 12 : slot < 7 ? 8 : slot < 10 ? 6 : 4, 2);
            RunState state = sc.State;
            state.TrySetAutoSell("iron.gear", true);
            // Store at 50 %: 2,000 of 4,000 units.
            state.Inventory.TryImport(new[]
            {
                new ResourceAmount("iron.ore", 500), new ResourceAmount("iron.bar", 600),
                new ResourceAmount("iron.plate", 500), new ResourceAmount("iron.gear", 400),
            });
            return sc;
        }
    }
}
