using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Fixtures.Run
{
    /// <summary>
    /// Shared helpers of the T098 suites: catalog and state builders, a complete state fingerprint, boundary-event
    /// construction from the state's timers, partitioned advancing and the tick-by-tick reference oracle.
    /// </summary>
    internal static class RunSimHarness
    {
        public const long TicksPerMs = 10_000;
        public const long TicksPerSecond = 10_000_000;
        public const long StartUtcMs = 1_700_000_000_000L;
        public static readonly string[] AllResources = { "iron.ore", "iron.bar", "iron.plate", "iron.gear" };

        public static Guid Id(int n) => new Guid(n, 0, 0, new byte[8]);

        // ------------------------------------------------------------------------------------------------
        // Catalogs
        // ------------------------------------------------------------------------------------------------

        /// <summary>Options of <see cref="Catalog"/>; every value has the literal FR-002 loop as its default.</summary>
        public sealed class CatalogOptions
        {
            public long[] RecipeDurations = { 20_000_000, 20_000_000, 40_000_000, 50_000_000 };
            public long SpeedGrowthPpm = 1_080_000;
            public int MaxLevel = 25;
            public SpeedMilestone[] Milestones = new SpeedMilestone[0];
            public long StorageCapacity = 1000;
            public long MaxCombinedSpeedPpm = 100_000_000;
            public long MaxCombinedOutputFactor = 4;
            public long MinCycleTicks = 1_000_000;
            public long BoostItemDurationMs = 60_000;
            public long DrillItemDurationMs = 60_000;
            public long OverdriveDurationMs = 30_000;
            public long OverdriveCooldownMs = 300_000;
            public long TimedContractDurationMs = 180_000;
            public bool LabRecipes;
            public bool ShuffleOrder;
            public int OrderSeed;

            /// <summary>Unlock conditions by ID (resource, machine type, contract offer or "overdrive"); any other ID is <c>always</c> (T140).</summary>
            public Dictionary<string, UnlockCondition> Unlocks;

            /// <summary>Number of factory levels (level 1 has 12 slots and costs 0, later levels 12 slots and 5,000 x level).</summary>
            public int FactoryLevels = 1;

            /// <summary>Contract offers added after the timed Bar contract (T327: production objectives and bonus offers); null (default) adds none.</summary>
            public ContractOfferDef[] ExtraOffers;
        }

        /// <summary>
        /// The FR-002 loop plus optional extras: a factory-wide speed Item, a Miner output Item, two Research
        /// entries and two Industry entries whose speed factors do not commute under flooring, a sale-price
        /// Research, a timed Bar contract and (with <see cref="CatalogOptions.LabRecipes"/>) the Alloy and
        /// Multiplier machines used by the partial-batch cases. Definition order can be shuffled: the runtime must
        /// not depend on catalog enumeration order.
        /// </summary>
        public static RunCatalog Catalog(CatalogOptions o = null)
        {
            o = o ?? new CatalogOptions();
            var always = new UnlockCondition("always");
            UnlockCondition Unlock(string id) => o.Unlocks != null && o.Unlocks.TryGetValue(id, out UnlockCondition c) ? c : always;
            var resources = new List<ResourceDef>
            {
                new ResourceDef("iron.ore", "Iron Ore", "F003-RES-IRON-ORE", 1, Unlock("iron.ore")),
                new ResourceDef("iron.bar", "Iron Bar", "F003-RES-IRON-BAR", 3, Unlock("iron.bar")),
                new ResourceDef("iron.plate", "Iron Plate", "F003-RES-IRON-PLATE", 8, Unlock("iron.plate")),
                new ResourceDef("iron.gear", "Gear", "F003-RES-IRON-GEAR", 24, Unlock("iron.gear")),
            };
            var recipes = new List<MachineRecipe>
            {
                new MachineRecipe("recipe.mine-iron-ore", new ResourceAmount[0], new[] { new ResourceAmount("iron.ore", 2) }, o.RecipeDurations[0]),
                new MachineRecipe("recipe.smelt-iron-bar", new[] { new ResourceAmount("iron.ore", 2) }, new[] { new ResourceAmount("iron.bar", 1) }, o.RecipeDurations[1]),
                new MachineRecipe("recipe.press-iron-plate", new[] { new ResourceAmount("iron.bar", 2) }, new[] { new ResourceAmount("iron.plate", 1) }, o.RecipeDurations[2]),
                new MachineRecipe("recipe.assemble-iron-gear", new[] { new ResourceAmount("iron.plate", 2) }, new[] { new ResourceAmount("iron.gear", 1) }, o.RecipeDurations[3]),
            };
            if (o.LabRecipes)
            {
                recipes.Add(new MachineRecipe("recipe.lab-alloy",
                    new[] { new ResourceAmount("iron.ore", 1), new ResourceAmount("iron.bar", 2) },
                    new[] { new ResourceAmount("iron.plate", 4) }, 20_000_000));
                recipes.Add(new MachineRecipe("recipe.lab-multiplier",
                    new[] { new ResourceAmount("iron.ore", 1) },
                    new[] { new ResourceAmount("iron.bar", 3) }, 20_000_000));
            }
            var cost = new Curve(10, 1_150_000, "ceil");
            MachineDef Machine(string id, string label, string recipeId) =>
                new MachineDef(id, label, "F003-MACHINE-" + label.ToUpperInvariant(), recipeId, o.MaxLevel, cost,
                    o.SpeedGrowthPpm, o.Milestones, Unlock(id));
            var machines = new List<MachineDef>
            {
                Machine("machine.miner", "Miner", recipes[0].Id),
                Machine("machine.smelter", "Smelter", recipes[1].Id),
                Machine("machine.press", "Press", recipes[2].Id),
                Machine("machine.assembler", "Assembler", recipes[3].Id),
            };
            if (o.LabRecipes)
            {
                machines.Add(Machine("machine.alloy", "Alloy", "recipe.lab-alloy"));
                machines.Add(Machine("machine.multiplier", "Multiplier", "recipe.lab-multiplier"));
            }
            var items = new List<ItemDef>
            {
                new ItemDef("item.boost", 1, "Boost", "F003-ITEM-BOOST", o.BoostItemDurationMs, "factory", new string[0],
                    new EffectDef("machineSpeed", "allMachines", null, 1_500_000)),
                new ItemDef("item.drill", 1, "Drill", "F003-ITEM-DRILL", o.DrillItemDurationMs, "machineType",
                    new[] { "machine.miner" }, new EffectDef("machineOutput", "machineType", null, 2)),
            };
            var research = new List<ResearchDef>
            {
                new ResearchDef("research.a-smelter", "A", 4, new Curve(5, 1_500_000, "ceil"), new ResearchPrerequisite[0],
                    new EffectDef("machineSpeed", "machine", "machine.smelter", 1_100_000)),
                new ResearchDef("research.b-all", "B", 4, new Curve(5, 1_500_000, "ceil"), new ResearchPrerequisite[0],
                    new EffectDef("machineSpeed", "allMachines", null, 1_070_000)),
                new ResearchDef("research.c-price", "C", 3, new Curve(5, 1_500_000, "ceil"), new ResearchPrerequisite[0],
                    new EffectDef("salePrice", "allResources", null, 1_130_000)),
                new ResearchDef("research.d-price-gear", "D", 3, new Curve(5, 1_500_000, "ceil"), new ResearchPrerequisite[0],
                    new EffectDef("salePrice", "resource", "iron.gear", 1_070_000)),
            };
            var industry = new List<IndustryUpgradeDef>
            {
                new IndustryUpgradeDef("industry.a-speed", "A", 4, new Curve(5, 1_500_000, "ceil"),
                    new EffectDef("machineSpeed", "allMachines", null, 1_050_000)),
                new IndustryUpgradeDef("industry.b-price", "B", 4, new Curve(5, 1_500_000, "ceil"),
                    new EffectDef("salePrice", "allResources", null, 1_060_000)),
            };
            var offers = new List<ContractOfferDef>
            {
                new ContractOfferDef("contract.timed-bar", "timedBar", "iron.bar", new Curve(40, 1_010_000, "ceil"),
                    o.TimedContractDurationMs, new[] { new ContractRewardDef("cash", new Curve(200, 1_000_000, "floor")) }, Unlock("contract.timed-bar")),
            };
            if (o.ExtraOffers != null) offers.AddRange(o.ExtraOffers);
            if (o.ShuffleOrder)
            {
                var rng = new Random(o.OrderSeed);
                Shuffle(resources, rng); Shuffle(recipes, rng); Shuffle(machines, rng);
                Shuffle(items, rng); Shuffle(research, rng); Shuffle(industry, rng);
            }
            return IdleFactory.Tests.EditMode.Run.TestCatalogBuilder.Create().WithResources(resources).WithRecipes(recipes).WithMachines(machines)
                .WithStorageLevels(
                new[] { new StorageLevelDef(1, o.StorageCapacity, 0), new StorageLevelDef(2, o.StorageCapacity * 2, 2500) })
                .WithFactoryLevels(
                Enumerable.Range(1, o.FactoryLevels).Select(l => new FactoryLevelDef(l, 12, l == 1 ? 0 : 5000L * l)).ToArray())
                .WithContractOffers(offers).WithItems(items).WithResearch(research).WithIndustryUpgrades(industry)
                .WithOverdrive(new OverdriveDef(o.OverdriveDurationMs, o.OverdriveCooldownMs, 2_000_000, Unlock(RunState.OverdriveUnlockId)))
                .WithPrestige(new PrestigeDef(10_000_000, "sqrt", 10, 10_000_000))
                .WithRunStart(new RunStartDef(25, new[] { "iron.ore", "machine.miner" }, 500_000, o.MinCycleTicks,
                    o.MaxCombinedSpeedPpm, o.MaxCombinedOutputFactor, 2_000_000))
                .Build();
        }

        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }

        // ------------------------------------------------------------------------------------------------
        // State builders
        // ------------------------------------------------------------------------------------------------

        public static RunState NewState(RunCatalog catalog, long cash = 25, int storageLevel = 1)
        {
            // Everything the run can unlock is unlocked, so a test starts at the fixed point of the unlock pass (T140).
            return new RunState(catalog, Id(9000), 1, null, cash, 0, storageLevel, 1, AllUnlockedExcept(catalog));
        }

        /// <summary>A fresh run whose unlocked set is exactly <paramref name="unlocked"/> (the unlock tests, T140).</summary>
        public static RunState NewStateWith(RunCatalog catalog, IEnumerable<string> unlocked, long cash = 25,
            int storageLevel = 1, int factoryLevel = 1)
        {
            return new RunState(catalog, Id(9000), 1, null, cash, 0, storageLevel, factoryLevel, unlocked);
        }

        /// <summary>Every unlockable ID of the catalog except <paramref name="locked"/>: resources, machine types, offers, Overdrive.</summary>
        public static List<string> AllUnlockedExcept(RunCatalog catalog, params string[] locked)
        {
            var all = new List<string>(AllResources);
            foreach (string id in catalog.Machines.Keys.OrderBy(k => k, StringComparer.Ordinal)) all.Add(id);
            foreach (string id in catalog.ContractOffers.Keys.OrderBy(k => k, StringComparer.Ordinal)) all.Add(id);
            all.Add(RunState.OverdriveUnlockId);
            all.RemoveAll(id => locked.Contains(id));
            return all;
        }

        /// <summary>
        /// Adds a machine of the given type at <paramref name="slot"/>. The stored cycle is the recipe duration
        /// (the level-1 duration); a higher level or a modifier is aligned by the first timestamp of an advance.
        /// </summary>
        public static MachineInstance Add(RunState state, string machineId, int slot, int instance, int level = 1,
            long progress = 0, bool active = true)
        {
            MachineDef def = state.Catalog.Machines[machineId];
            long cycle = state.Catalog.Recipes[def.RecipeId].DurationTicks;
            var machine = new MachineInstance(Id(instance), def, slot, level, active, Math.Min(progress, cycle), cycle, 10);
            Assert.That(state.TryAddMachine(machine), Is.EqualTo(RunStateResult.Ok));
            return machine;
        }

        public static void Stock(RunState state, string resourceId, long units)
        {
            Assert.That(state.Inventory.TryImport(new[] { new ResourceAmount(resourceId, units) }), Is.EqualTo(RunStateResult.Ok));
        }

        public static void Reorder(RunState state, params int[] instances)
        {
            Assert.That(state.TryReorderPriority(instances.Select(Id).ToArray()), Is.EqualTo(RunStateResult.Ok));
        }

        // ------------------------------------------------------------------------------------------------
        // Events and advancing
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// The state's timers as boundary events for an advance that starts <paramref name="advanceStartTicks"/>
        /// after the scenario start and lasts <paramref name="advanceTicks"/>: every boundary in
        /// <c>(start, start + ticks]</c> (and one at the scenario start itself when the advance starts there),
        /// rebased to the advance and ordered by (SimTicks, Kind, Id). One simulation tick is one timeline tick.
        /// </summary>
        public static List<BoundaryEvent> Events(RunState state, long scenarioStartUtcMs, long advanceStartTicks, long advanceTicks)
        {
            var all = new List<BoundaryEvent>();
            void Add(BoundaryKind kind, string id, long utcMs)
            {
                long abs = (utcMs - scenarioStartUtcMs) * TicksPerMs;
                bool inside = advanceStartTicks == 0 ? abs <= advanceTicks : abs > advanceStartTicks && abs <= advanceStartTicks + advanceTicks;
                if (inside) all.Add(new BoundaryEvent(Math.Max(0, abs - advanceStartTicks), kind, id, utcMs));
            }
            foreach (ActiveEffect e in state.Effects.Effects) Add(BoundaryKind.EffectExpiry, e.SourceId, e.EndUtcMs);
            if (state.Overdrive.ActiveUntilUtcMs.HasValue)
            {
                Add(BoundaryKind.OverdriveEnd, "overdrive", state.Overdrive.ActiveUntilUtcMs.Value);
                Add(BoundaryKind.OverdriveCooldownEnd, "overdrive", state.Overdrive.CooldownUntilUtcMs.Value);
            }
            if (state.Contracts.Active != null && state.Contracts.Active.DeadlineUtcMs.HasValue)
                Add(BoundaryKind.ContractExpiry, state.Contracts.Active.OfferId, state.Contracts.Active.DeadlineUtcMs.Value + 1);
            all.Sort((a, b) => BoundaryEvent.Compare(a, b));
            return all;
        }

        /// <summary>One advance from <paramref name="atTicks"/> of the scenario; events come from the state's timers.</summary>
        public static RunAdvanceResult AdvanceAt(RunSimulator sim, RunState state, PermanentState permanent,
            long scenarioStartUtcMs, long atTicks, long ticks)
        {
            List<BoundaryEvent> events = Events(state, scenarioStartUtcMs, atTicks, ticks);
            return sim.Advance(state, permanent, ticks, scenarioStartUtcMs + atTicks / TicksPerMs, events);
        }

        /// <summary>Advances the scenario to <paramref name="total"/> ticks in one call or cut at the given instants.</summary>
        public static void RunCut(RunSimulator sim, SimScenario sc, long total, IEnumerable<long> cuts)
        {
            long at = 0;
            foreach (long cut in cuts.Where(c => c > 0 && c < total).Distinct().OrderBy(c => c).Concat(new[] { total }))
            {
                Assert.That(AdvanceAt(sim, sc.State, sc.Permanent, sc.StartUtcMs, at, cut - at), Is.EqualTo(RunAdvanceResult.Ok));
                at = cut;
            }
        }

        // ------------------------------------------------------------------------------------------------
        // Fingerprint
        // ------------------------------------------------------------------------------------------------

        /// <summary>The complete run state as text, so a failing comparison shows the difference.</summary>
        public static string Fingerprint(RunState s, PermanentState p = null, bool includeSlots = true)
        {
            var b = new StringBuilder();
            b.Append("cash=").Append(s.Cash).Append(" rp=").Append(s.Rp).Append('\n');
            b.Append("stock=");
            for (int i = 0; i < s.Inventory.Count; i++) b.Append(s.Inventory.IdAt(i)).Append(':').Append(s.Inventory.UnitsAt(i)).Append(' ');
            b.Append("total=").Append(s.Inventory.Total).Append(" cap=").Append(s.Inventory.Capacity).Append('\n');
            b.Append("unlocked=").Append(string.Join(",", s.Unlocked)).Append('\n');
            var machines = s.Machines.Select(m =>
                m.InstanceId.ToString("N") + " " + m.MachineId + (includeSlots ? " slot" + m.SlotIndex : string.Empty) +
                " L" + m.Level + " active=" + m.IsActive + " p=" + m.ProgressTicks + " d=" + m.CycleDurationTicks + " inv=" + m.InvestedCash);
            foreach (string line in includeSlots ? machines : machines.OrderBy(x => x, StringComparer.Ordinal)) b.Append(line).Append('\n');
            b.Append("priority=").Append(string.Join(",", s.Priority.Ids.Select(g => g.ToString("N")))).Append('\n');
            foreach (ActiveEffect e in s.Effects.Effects)
                b.Append("effect ").Append(e.SourceId).Append(' ').Append(e.ConflictKey.EffectKind).Append('/').Append(e.ConflictKey.TargetKey)
                    .Append(' ').Append(e.StartUtcMs).Append("..").Append(e.EndUtcMs).Append('\n');
            b.Append("overdrive=").Append(s.Overdrive.ActiveUntilUtcMs).Append('/').Append(s.Overdrive.CooldownUntilUtcMs).Append('\n');
            b.Append("contract=");
            if (s.Contracts.Active != null)
                b.Append(s.Contracts.Active.OfferId).Append(' ').Append(s.Contracts.Active.DeadlineUtcMs).Append(' ').Append(s.Contracts.Active.RequiredUnits);
            b.Append(" completions=").Append(string.Join(",", s.Contracts.CompletionCounts().Select(c => c.Id + ":" + c.Count))).Append('\n');
            if (p != null) b.Append("ip=").Append(p.Ip).Append('\n');
            return b.ToString();
        }

        public static long[] Units(RunState state)
        {
            var units = new long[state.Inventory.Count];
            for (int i = 0; i < units.Length; i++) units[i] = state.Inventory.UnitsAt(i);
            return units;
        }

        /// <summary>Asserts that <c>stock change = produced - consumed - sold</c> for every resource of the last advance.</summary>
        public static void AssertFlow(RunSimulator sim, long[] before, RunState after, string context)
        {
            for (int i = 0; i < after.Inventory.Count; i++)
            {
                string id = after.Inventory.IdAt(i);
                long expected = before[i] + sim.LastProducedUnits(id) - sim.LastConsumedUnits(id) - sim.LastSoldUnits(id);
                Assert.That(after.Inventory.UnitsAt(i), Is.EqualTo(expected), context + ": conservation of " + id);
            }
        }

        // ------------------------------------------------------------------------------------------------
        // Random scenarios
        // ------------------------------------------------------------------------------------------------
    }

    /// <summary>A catalog, a run state, the permanent state and the real instant of simulation offset 0.</summary>
    internal sealed class SimScenario
    {
        public RunCatalog Catalog;
        public RunState State;
        public PermanentState Permanent;
        public long StartUtcMs = RunSimHarness.StartUtcMs;
        public int Seed;

        /// <summary>
        /// A deterministic random factory. <paramref name="small"/> keeps every cycle within a few 10^6 ticks so
        /// that a tick-by-tick oracle stays affordable. <paramref name="orderSeed"/> shuffles the catalog
        /// definition order and, with <paramref name="shuffleBuildOrder"/>, the order machines are built in and
        /// their slots, while the resulting priority order (by instance) stays the same.
        /// </summary>
        public static SimScenario Random(int seed, bool small, int orderSeed, bool shuffleBuildOrder, bool unlocks = false,
            ContractOfferDef[] extraOffers = null)
        {
            var rng = new Random(seed);
            var o = new RunSimHarness.CatalogOptions
            {
                ExtraOffers = extraOffers,
                StorageCapacity = small ? 8 + rng.Next(30) : 40 + rng.Next(560),
                SpeedGrowthPpm = 1_000_000 + rng.Next(1_100_000),
                MaxLevel = 4,
                MaxCombinedSpeedPpm = 1_000_000_000_000L,
                MaxCombinedOutputFactor = 8,
                ShuffleOrder = orderSeed != 0,
                OrderSeed = orderSeed,
                LabRecipes = true,
            };
            if (unlocks)
            {
                // Chained conditions of every kind (T140): Bar after the first Ore, Plate after the first Bar, Gear after
                // the first Plate or a Research; machine types, the offer and Overdrive follow the same rules.
                o.FactoryLevels = 2;
                o.Unlocks = new Dictionary<string, UnlockCondition>
                {
                    ["iron.bar"] = new UnlockCondition("resourceProduced", "iron.ore"),
                    ["iron.plate"] = new UnlockCondition("resourceProduced", "iron.bar"),
                    ["iron.gear"] = seed % 2 == 0 ? new UnlockCondition("resourceProduced", "iron.plate") : new UnlockCondition("researchPurchased", "research.b-all"),
                    ["machine.smelter"] = new UnlockCondition("resourceProduced", "iron.ore"),
                    ["machine.press"] = new UnlockCondition("researchPurchased", "research.c-price"),
                    ["machine.assembler"] = new UnlockCondition("factoryLevelAtLeast", null, 2),
                    ["machine.multiplier"] = new UnlockCondition("resourceProduced", "iron.bar"),
                    ["contract.timed-bar"] = new UnlockCondition("resourceProduced", "iron.bar"),
                    [RunState.OverdriveUnlockId] = new UnlockCondition("researchPurchased", "research.a-smelter"),
                };
            }
            if (small)
            {
                o.RecipeDurations = new[] { 1_000_000L + rng.Next(1_200_000), 1_000_000L + rng.Next(1_200_000),
                    1_000_000L + rng.Next(1_200_000), 1_000_000L + rng.Next(1_200_000) };
                o.BoostItemDurationMs = 200 + rng.Next(400);
                o.DrillItemDurationMs = 200 + rng.Next(400);
                o.OverdriveDurationMs = 300 + rng.Next(300);
                o.OverdriveCooldownMs = 100 + rng.Next(200);
                o.TimedContractDurationMs = 300 + rng.Next(300);
            }
            else
            {
                o.RecipeDurations = new[] { 10_000_000L + rng.Next(30_000_000), 10_000_000L + rng.Next(30_000_000),
                    20_000_000L + rng.Next(40_000_000), 30_000_000L + rng.Next(40_000_000) };
                o.BoostItemDurationMs = 3_000 + rng.Next(30_000);
                o.DrillItemDurationMs = 3_000 + rng.Next(30_000);
                o.OverdriveDurationMs = 5_000 + rng.Next(25_000);
                o.OverdriveCooldownMs = 5_000 + rng.Next(20_000);
                o.TimedContractDurationMs = 5_000 + rng.Next(40_000);
            }
            RunCatalog catalog = RunSimHarness.Catalog(o);
            RunState state = unlocks
                ? RunSimHarness.NewStateWith(catalog, new[] { "iron.ore", "machine.miner", "machine.alloy" }, 25)
                : RunSimHarness.NewState(catalog, 25);
            var permanent = new PermanentState(catalog);
            long start = RunSimHarness.StartUtcMs;

            string[] types = { "machine.miner", "machine.smelter", "machine.press", "machine.assembler", "machine.alloy", "machine.multiplier" };
            int count = small ? 2 + rng.Next(4) : 3 + rng.Next(7);
            var plan = new List<(string type, int level, int instance, long fraction, bool active)>();
            for (int i = 0; i < count; i++)
                plan.Add((types[rng.Next(types.Length)], 1 + rng.Next(4), 100 + i, rng.Next(1000), rng.Next(8) != 0));
            // Unlock scenarios always start with one active Miner, the only source of the first Ore (T140).
            if (unlocks) plan[0] = ("machine.miner", plan[0].level, plan[0].instance, plan[0].fraction, true);
            // The build order and the slots may differ; the instance identities and the priority order do not.
            var slots = Enumerable.Range(0, 12).ToList();
            var buildOrder = Enumerable.Range(0, count).ToList();
            if (shuffleBuildOrder)
            {
                var shuffle = new Random(seed * 31 + orderSeed + 7);
                for (int i = slots.Count - 1; i > 0; i--) { int j = shuffle.Next(i + 1); int t = slots[i]; slots[i] = slots[j]; slots[j] = t; }
                for (int i = buildOrder.Count - 1; i > 0; i--) { int j = shuffle.Next(i + 1); int t = buildOrder[i]; buildOrder[i] = buildOrder[j]; buildOrder[j] = t; }
            }
            foreach (int i in buildOrder)
            {
                MachineDef def = catalog.Machines[plan[i].type];
                long cycle = catalog.Recipes[def.RecipeId].DurationTicks;
                long progress = cycle * plan[i].fraction / 1000;
                var m = new MachineInstance(RunSimHarness.Id(plan[i].instance), def, slots[i], plan[i].level, plan[i].active, progress, cycle, 10);
                Assert.That(state.TryAddMachine(m), Is.EqualTo(RunStateResult.Ok));
            }
            var priority = plan.Select(x => x.instance).OrderBy(x => rng.Next()).Select(RunSimHarness.Id).ToArray();
            Assert.That(state.TryReorderPriority(priority), Is.EqualTo(RunStateResult.Ok));

            long room = state.Inventory.Free;
            foreach (string id in RunSimHarness.AllResources)
            {
                if (unlocks && !state.Inventory.IsAllowed(id)) continue;
                long units = Math.Min(room, rng.Next(0, (int)Math.Min(60, room + 1)));
                if (units > 0) RunSimHarness.Stock(state, id, units);
                room -= units;
            }
            foreach (string id in RunSimHarness.AllResources)
            {
                if (unlocks && !state.Inventory.IsAllowed(id)) continue;
                if (rng.Next(3) == 0) Assert.That(state.TrySetAutoSell(id, true), Is.EqualTo(RunStateResult.Ok));
            }
            if (rng.Next(2) == 0)
                Assert.That(state.TryAddEffect(new ActiveEffect("item.boost", new ConflictKey("machineSpeed", "factory"), start, start + o.BoostItemDurationMs)), Is.EqualTo(RunStateResult.Ok));
            if (rng.Next(2) == 0)
                Assert.That(state.TryAddEffect(new ActiveEffect("item.drill", new ConflictKey("machineOutput", "machine.miner"), start, start + o.DrillItemDurationMs)), Is.EqualTo(RunStateResult.Ok));
            if (rng.Next(2) == 0)
                Assert.That(state.Overdrive.TryActivate(start - rng.Next((int)Math.Min(o.OverdriveDurationMs - 1, 4_000))), Is.EqualTo(RunStateResult.Ok));
            if (rng.Next(2) == 0)
            {
                long accepted = start - rng.Next(1000);
                Assert.That(state.Contracts.TrySetActive(new ActiveContract("contract.timed-bar", accepted, accepted + o.TimedContractDurationMs, 40)), Is.EqualTo(RunStateResult.Ok));
            }
            foreach (string id in new[] { "research.a-smelter", "research.b-all", "research.c-price", "research.d-price-gear" })
                Assert.That(state.TrySetResearchLevel(id, rng.Next(0, 3)), Is.EqualTo(RunStateResult.Ok));
            foreach (string id in new[] { "industry.a-speed", "industry.b-price" })
                Assert.That(permanent.TrySetIndustryLevel(id, rng.Next(0, 3)), Is.EqualTo(RunStateResult.Ok));
            return new SimScenario { Catalog = catalog, State = state, Permanent = permanent, StartUtcMs = start, Seed = seed };
        }
    }

    /// <summary>A simulator, a state and a position on the scenario timeline for tests that step and issue commands.</summary>
    internal sealed class SimRun
    {
        public readonly RunSimulator Sim;
        public readonly RunState State;
        public readonly PermanentState Permanent;
        public readonly long StartUtcMs;
        public long At;

        public SimRun(RunCatalog catalog, RunState state, PermanentState permanent = null, long startUtcMs = RunSimHarness.StartUtcMs)
        {
            Sim = new RunSimulator(catalog);
            State = state;
            Permanent = permanent;
            StartUtcMs = startUtcMs;
        }

        /// <summary>The real instant of the current position, floored to a millisecond.</summary>
        public long NowUtcMs => StartUtcMs + At / RunSimHarness.TicksPerMs;

        public RunAdvanceResult Step(long ticks)
        {
            RunAdvanceResult result = RunSimHarness.AdvanceAt(Sim, State, Permanent, StartUtcMs, At, ticks);
            if (result == RunAdvanceResult.Ok) At += ticks;
            return result;
        }

        public void StepOk(long ticks) => Assert.That(Step(ticks), Is.EqualTo(RunAdvanceResult.Ok));

        public MachineInstance Machine(int instance) => State.FindMachine(RunSimHarness.Id(instance));
    }

    /// <summary>
    /// A deliberately naive reference: one tick at a time, machines progress by one tick, and the full ordered
    /// procedure runs whenever a machine is due or a boundary falls on that tick. It shares only the pure
    /// arithmetic (<see cref="SpeedComposer"/> and <see cref="RunModifiers"/>) with the simulator and drives the
    /// public state mutators directly (no staging, no event skipping).
    /// </summary>
    internal sealed class TickOracle
    {
        private readonly SimScenario _sc;
        private readonly RunState _s;
        private readonly RunModifiers _mods;
        private readonly int _n;
        private readonly bool[] _running;
        private readonly long[] _batch;
        private readonly long[] _factor;
        private readonly bool[] _expired;
        private bool _overdrive;
        public long Completions;
        public long Produced(int resource) => _produced[resource];
        private readonly long[] _produced = new long[4];
        private readonly long[] _consumed = new long[4];
        private readonly long[] _sold = new long[4];
        public int MaxPasses;
        private readonly List<(string id, UnlockCondition condition)> _candidates = new List<(string, UnlockCondition)>();

        public TickOracle(SimScenario sc)
        {
            _sc = sc;
            _s = sc.State;
            _mods = new RunModifiers(sc.Catalog);
            _n = _s.Machines.Count;
            _running = new bool[_n];
            _batch = new long[_n];
            _factor = new long[_n];
            _expired = new bool[_s.Effects.Count];
            // The unlock rule (T140), written out naively: every locked condition is evaluated whenever the ordered
            // procedure runs, in the order resources, machine types, contract offers (each by ordinal ID), Overdrive.
            RunCatalog catalog = sc.Catalog;
            foreach (string id in catalog.Resources.Keys.OrderBy(k => k, StringComparer.Ordinal)) _candidates.Add((id, catalog.Resources[id].Unlock));
            foreach (string id in catalog.Machines.Keys.OrderBy(k => k, StringComparer.Ordinal)) _candidates.Add((id, catalog.Machines[id].Unlock));
            foreach (string id in catalog.ContractOffers.Keys.OrderBy(k => k, StringComparer.Ordinal)) _candidates.Add((id, catalog.ContractOffers[id].Unlock));
            _candidates.Add((RunState.OverdriveUnlockId, catalog.Overdrive.Unlock));
        }

        public long ProducedUnits(string id) => _produced[Array.IndexOf(RunSimHarness.AllResources, id)];
        public long ConsumedUnits(string id) => _consumed[Array.IndexOf(RunSimHarness.AllResources, id)];
        public long SoldUnits(string id) => _sold[Array.IndexOf(RunSimHarness.AllResources, id)];

        public void Run(long totalTicks)
        {
            List<BoundaryEvent> events = RunSimHarness.Events(_s, _sc.StartUtcMs, 0, totalTicks);
            int next = 0;
            long effectCutoff = long.MinValue, cooldownUtc = 0, contractUtc = 0;
            bool cooldown = false, contract = false;
            _overdrive = _s.Overdrive.IsActive(_sc.StartUtcMs);

            bool ApplyEventsUpTo(long tick)
            {
                bool any = false;
                while (next < events.Count && events[next].SimTicks <= tick)
                {
                    BoundaryEvent e = events[next++];
                    any = true;
                    switch (e.Kind)
                    {
                        case BoundaryKind.EffectExpiry:
                            for (int i = 0; i < _s.Effects.Effects.Count; i++)
                                if (_s.Effects.Effects[i].EndUtcMs <= e.UtcMs) _expired[i] = true;
                            effectCutoff = Math.Max(effectCutoff, e.UtcMs);
                            break;
                        case BoundaryKind.OverdriveEnd: _overdrive = false; break;
                        case BoundaryKind.OverdriveCooldownEnd: _overdrive = false; cooldown = true; cooldownUtc = e.UtcMs; break;
                        case BoundaryKind.ContractExpiry: contract = true; contractUtc = e.UtcMs; break;
                    }
                }
                return any;
            }

            ApplyEventsUpTo(0);
            Derive();
            SellAll();
            Procedure();
            for (long tick = 1; tick <= totalTicks; tick++)
            {
                bool due = false;
                for (int i = 0; i < _n; i++)
                {
                    if (!_running[i]) continue;
                    MachineInstance m = _s.Machines[i];
                    if (m.TrySetProgress(m.ProgressTicks + 1) != RunStateResult.Ok) throw new InvalidOperationException("oracle progress overflow at tick " + tick);
                    if (m.ProgressTicks == m.CycleDurationTicks) due = true;
                }
                bool boundary = ApplyEventsUpTo(tick);
                if (boundary) Derive();
                if (boundary || due) Procedure();
                if ((tick & 0x3FFF) == 0) AssertPredicateIsStable(tick);
            }
            if (effectCutoff != long.MinValue) _s.Effects.RemoveExpired(effectCutoff);
            if (cooldown) _s.Overdrive.ClearElapsed(cooldownUtc);
            if (contract) _s.Contracts.TryExpire(contractUtc);
        }

        private void Derive()
        {
            for (int i = 0; i < _n; i++)
            {
                MachineInstance m = _s.Machines[i];
                MachineRecipe r = _s.Catalog.Recipes[_s.Catalog.Machines[m.MachineId].RecipeId];
                Assert.That(_mods.TryMachineSpeed(_s, _sc.Permanent, m, _overdrive, _expired, out long speed), Is.True);
                Assert.That(SpeedComposer.TryBatch(r.DurationTicks, speed, _s.Catalog.RunStart.MinCycleTicks, out long k, out long d), Is.True);
                Assert.That(_mods.TryOutputFactor(_s, m, _expired, out long f), Is.True);
                if (d != m.CycleDurationTicks) Assert.That(m.TryRescale(d), Is.EqualTo(RunStateResult.Ok));
                _batch[i] = k;
                _factor[i] = f;
            }
        }

        private long UnitsFor(int i)
        {
            MachineInstance m = _s.Machines[i];
            MachineRecipe r = _s.Catalog.Recipes[_s.Catalog.Machines[m.MachineId].RecipeId];
            foreach (ResourceAmount a in r.Inputs) if (!_s.Inventory.IsAllowed(a.ResourceId)) return 0;
            foreach (ResourceAmount a in r.Outputs) if (!_s.Inventory.IsAllowed(a.ResourceId)) return 0;
            Assert.That(SpeedComposer.TryCompletedUnits(_batch[i], r, _factor[i], _s.Inventory, out long units), Is.True);
            return units;
        }

        private void Procedure()
        {
            int passes = 0;
            do
            {
                while (true)
                {
                passes++;
                int completed = 0;
                foreach (Guid id in _s.Priority.Ids)
                {
                    int i = IndexOf(id);
                    MachineInstance m = _s.Machines[i];
                    if (!m.IsActive || m.ProgressTicks != m.CycleDurationTicks) continue;
                    long units = UnitsFor(i);
                    if (units < 1) continue;
                    MachineRecipe r = _s.Catalog.Recipes[_s.Catalog.Machines[m.MachineId].RecipeId];
                    Assert.That(_s.Inventory.TryExchange(r.Inputs, r.Outputs, units, _factor[i]), Is.EqualTo(RunStateResult.Ok));
                    foreach (ResourceAmount a in r.Inputs) _consumed[Array.IndexOf(RunSimHarness.AllResources, a.ResourceId)] += units * a.Amount;
                    foreach (ResourceAmount a in r.Outputs) _produced[Array.IndexOf(RunSimHarness.AllResources, a.ResourceId)] += units * a.Amount * _factor[i];
                    Assert.That(m.TryStartCycle(m.CycleDurationTicks), Is.EqualTo(RunStateResult.Ok));
                    completed++;
                    Completions++;
                }
                SellAll();
                if (completed == 0) break;
                }
            } while (UnlockRound());
            if (passes > MaxPasses) MaxPasses = passes;
            for (int i = 0; i < _n; i++)
            {
                MachineInstance m = _s.Machines[i];
                _running[i] = m.IsActive && m.ProgressTicks < m.CycleDurationTicks && UnitsFor(i) >= 1;
            }
        }

        private bool UnlockRound()
        {
            bool any = false;
            foreach ((string id, UnlockCondition c) in _candidates)
            {
                if (_s.IsUnlocked(id)) continue;
                bool holds;
                switch (c.Kind)
                {
                    case "researchPurchased": holds = _s.ResearchLevel(c.Id) >= 1; break;
                    case "factoryLevelAtLeast": holds = _s.FactoryLevel >= c.Level; break;
                    case "resourceProduced": holds = _produced[Array.IndexOf(RunSimHarness.AllResources, c.Id)] > 0; break;
                    default: holds = true; break;
                }
                if (!holds) continue;
                Assert.That(_s.TryUnlock(id), Is.EqualTo(RunStateResult.Ok));
                any = true;
            }
            return any;
        }

        private void SellAll()
        {
            foreach (string res in _s.AutoSell.ToArray())
            {
                long units = _s.Inventory.Units(res);
                if (units < 1) continue;
                Assert.That(_mods.TrySalePricePpm(_s, _sc.Permanent, res, out long ppm), Is.True);
                Assert.That(MulDiv.MulDivFloor(units * _s.Catalog.Resources[res].BasePrice, ppm, 1_000_000, out long credit), Is.True);
                Assert.That(_s.TrySell(res, units, credit), Is.EqualTo(RunStateResult.Ok));
                _sold[Array.IndexOf(RunSimHarness.AllResources, res)] += units;
            }
        }

        private void AssertPredicateIsStable(long tick)
        {
            for (int i = 0; i < _n; i++)
            {
                MachineInstance m = _s.Machines[i];
                bool run = m.IsActive && m.ProgressTicks < m.CycleDurationTicks && UnitsFor(i) >= 1;
                Assert.That(_running[i], Is.EqualTo(run), "oracle predicate drifted at tick " + tick + " machine " + i);
            }
        }

        private int IndexOf(Guid id)
        {
            for (int i = 0; i < _n; i++) if (_s.Machines[i].InstanceId == id) return i;
            throw new InvalidOperationException("machine missing");
        }
    }
}
