using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class RunStateTests
    {
        private static Guid Id(int n) => new Guid(n, 0, 0, new byte[8]);

        private static readonly string[] AllResources = { "iron.ore", "iron.bar", "iron.plate", "iron.gear" };

        private static RunCatalog ProgressionCatalog()
        {
            var b = RunTestCatalogs.IronLoop();
            var always = new UnlockCondition("always");
            var offers = new[]
            {
                new ContractOfferDef("contract.plate", "plate", "iron.plate", new Curve(5, 1_010_000, "ceil"), null,
                    new[] { new ContractRewardDef("cash", new Curve(100, 1_000_000, "floor")) }, always),
                new ContractOfferDef("contract.timed-bar", "timedBar", "iron.bar", new Curve(40, 1_010_000, "ceil"), 180_000,
                    new[] { new ContractRewardDef("cash", new Curve(200, 1_000_000, "floor")) }, always),
            };
            var items = new[]
            {
                new ItemDef("item.b-drill", 1, "B", "K", 60_000, "machineType", new[] { "machine.miner" },
                    new EffectDef("machineOutput", "machineType", null, 2)),
                new ItemDef("item.a-boost", 1, "A", "K", 60_000, "factory", new string[0],
                    new EffectDef("machineSpeed", "allMachines", null, 1_500_000)),
            };
            var research = new[]
            {
                new ResearchDef("research.smelter-speed", "S", 2, new Curve(5, 1_500_000, "ceil"), new ResearchPrerequisite[0],
                    new EffectDef("machineSpeed", "machine", "machine.smelter", 1_100_000)),
            };
            var industry = new[]
            {
                new IndustryUpgradeDef("industry.cash", "C", 2, new Curve(5, 1_500_000, "ceil"),
                    new EffectDef("startingCash", "none", null, 500)),
            };
            return IdleFactory.Tests.EditMode.Run.TestCatalogBuilder.From(b).WithResources(b.Resources.Values.OrderBy(r => r.Id, StringComparer.Ordinal))
                .WithContractOffers(offers).WithItems(items).WithResearch(research).WithIndustryUpgrades(industry).Build();
        }

        private static RunState NewState(RunCatalog catalog = null, long cash = 25, int factoryLevel = 1, int storageLevel = 1)
        {
            catalog = catalog ?? RunTestCatalogs.IronLoop();
            return new RunState(catalog, Id(1), 1, null, cash, 0, storageLevel, factoryLevel, AllResources.Concat(new[] { "machine.miner" }));
        }

        private static MachineInstance Machine(RunState state, string machineId, int slot, int instance, int level = 1,
            long progress = 0, long cycle = 20_000_000)
        {
            return new MachineInstance(Id(instance), state.Catalog.Machines[machineId], slot, level, true, progress, cycle, 10);
        }

        private static long[] Snapshot(GlobalInventory inventory)
        {
            var values = new long[inventory.Count + 2];
            for (int i = 0; i < inventory.Count; i++) values[i] = inventory.UnitsAt(i);
            values[inventory.Count] = inventory.Total;
            values[inventory.Count + 1] = inventory.Capacity;
            return values;
        }

        // ---------------------------------------------------------------- GlobalInventory

        [Test]
        public void InventoryEntriesAreInOrdinalIdOrderAndRejectDuplicates()
        {
            var inventory = new GlobalInventory(new[] { "iron.ore", "iron.gear", "iron.bar" }, 10);
            Assert.That(Enumerable.Range(0, inventory.Count).Select(inventory.IdAt),
                Is.EqualTo(new[] { "iron.bar", "iron.gear", "iron.ore" }));
            Assert.That(() => new GlobalInventory(new[] { "a", "a" }, 10), Throws.ArgumentException);
            Assert.That(() => new GlobalInventory(new[] { "a" }, 0), Throws.InstanceOf<ArgumentOutOfRangeException>());

            var state = NewState();
            Assert.That(state.TryUnlock("machine.press"), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Inventory.IdAt(0), Is.EqualTo("iron.bar"));
        }

        [Test]
        public void ExchangeMovesInputsAndOutputsAtomicallyWithTheOutputFactor()
        {
            var recipe = RunTestCatalogs.IronLoop().Recipes["recipe.smelt-iron-bar"];
            var inventory = new GlobalInventory(AllResources, 100);
            Assert.That(inventory.TryImport(new[] { new ResourceAmount("iron.ore", 10) }), Is.EqualTo(RunStateResult.Ok));
            Assert.That(inventory.TryExchange(recipe.Inputs, recipe.Outputs, 3, 2), Is.EqualTo(RunStateResult.Ok));
            Assert.That(inventory.Units("iron.ore"), Is.EqualTo(4));
            Assert.That(inventory.Units("iron.bar"), Is.EqualTo(6));
            Assert.That(inventory.Total, Is.EqualTo(10));
        }

        [Test]
        public void ExchangeRejectsWithoutPartialChange()
        {
            var catalog = RunTestCatalogs.IronLoop();
            var smelt = catalog.Recipes["recipe.smelt-iron-bar"];
            var mine = catalog.Recipes["recipe.mine-iron-ore"];
            var inventory = new GlobalInventory(new[] { "iron.ore", "iron.bar" }, 10);
            inventory.TryImport(new[] { new ResourceAmount("iron.ore", 8) });
            long[] before = Snapshot(inventory);

            // Capacity: 8 + 10 > 10.
            Assert.That(inventory.TryExchange(mine.Inputs, mine.Outputs, 5, 1), Is.EqualTo(RunStateResult.CapacityExceeded));
            Assert.That(Snapshot(inventory), Is.EqualTo(before));
            // Stock: 5 x 2 ore inputs > 8.
            Assert.That(inventory.TryExchange(smelt.Inputs, smelt.Outputs, 5, 1), Is.EqualTo(RunStateResult.StockInsufficient));
            Assert.That(Snapshot(inventory), Is.EqualTo(before));
            // A locked output resource: the inputs must not have been consumed.
            var toPlate = new[] { new ResourceAmount("iron.plate", 1) };
            Assert.That(inventory.TryExchange(smelt.Inputs, toPlate, 1, 1), Is.EqualTo(RunStateResult.ResourceLocked));
            Assert.That(Snapshot(inventory), Is.EqualTo(before));
            // A locked input resource.
            var plateIn = new[] { new ResourceAmount("iron.plate", 1) };
            Assert.That(inventory.TryExchange(plateIn, smelt.Outputs, 1, 1), Is.EqualTo(RunStateResult.ResourceLocked));
            Assert.That(Snapshot(inventory), Is.EqualTo(before));
            // Invalid arguments.
            Assert.That(inventory.TryExchange(smelt.Inputs, smelt.Outputs, 0, 1), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(inventory.TryExchange(smelt.Inputs, smelt.Outputs, 1, 0), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(Snapshot(inventory), Is.EqualTo(before));
        }

        [Test]
        public void ExchangeRejectsCheckedOverflowAtomically()
        {
            var inventory = new GlobalInventory(AllResources, ExactBound.Value);
            inventory.TryImport(new[] { new ResourceAmount("iron.ore", 5) });
            long[] before = Snapshot(inventory);
            var huge = new[] { new ResourceAmount("iron.bar", ExactBound.Value) };
            var one = new[] { new ResourceAmount("iron.ore", 1) };
            Assert.That(inventory.TryExchange(one, huge, 2, 1), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(inventory.TryExchange(one, huge, 1, 2), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(Snapshot(inventory), Is.EqualTo(before));
            Assert.That(inventory.TryImport(huge), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(Snapshot(inventory), Is.EqualTo(before));
        }

        [Test]
        public void SaleAndDeliveryRemoveStockOrRejectUnchanged()
        {
            var inventory = new GlobalInventory(new[] { "iron.ore", "iron.bar" }, 100);
            inventory.TryImport(new[] { new ResourceAmount("iron.ore", 10), new ResourceAmount("iron.bar", 4) });
            long[] before = Snapshot(inventory);

            Assert.That(inventory.TrySell("iron.ore", 11), Is.EqualTo(RunStateResult.StockInsufficient));
            Assert.That(inventory.TryDeliver("iron.bar", 5), Is.EqualTo(RunStateResult.StockInsufficient));
            Assert.That(inventory.TrySell("iron.gear", 1), Is.EqualTo(RunStateResult.ResourceLocked));
            Assert.That(inventory.TrySell("iron.ore", 0), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(inventory.TrySell("iron.ore", -1), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(inventory.TrySell("iron.ore", ExactBound.Value + 1), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(Snapshot(inventory), Is.EqualTo(before));

            Assert.That(inventory.TrySell("iron.ore", 10), Is.EqualTo(RunStateResult.Ok));
            Assert.That(inventory.TryDeliver("iron.bar", 4), Is.EqualTo(RunStateResult.Ok));
            Assert.That(inventory.Total, Is.EqualTo(0));
        }

        [Test]
        public void ImportIsAtomicForTheWholeList()
        {
            var inventory = new GlobalInventory(new[] { "iron.ore", "iron.bar" }, 10);
            inventory.TryImport(new[] { new ResourceAmount("iron.ore", 8) });
            long[] before = Snapshot(inventory);

            Assert.That(inventory.TryImport(new[] { new ResourceAmount("iron.bar", 1), new ResourceAmount("iron.plate", 1) }),
                Is.EqualTo(RunStateResult.ResourceLocked));
            Assert.That(inventory.TryImport(new[] { new ResourceAmount("iron.bar", 1), new ResourceAmount("iron.ore", 2) }),
                Is.EqualTo(RunStateResult.CapacityExceeded));
            Assert.That(Snapshot(inventory), Is.EqualTo(before));
            Assert.That(inventory.TryImport(new[] { new ResourceAmount("iron.bar", 1), new ResourceAmount("iron.ore", 1) }),
                Is.EqualTo(RunStateResult.Ok));
            Assert.That(inventory.Total, Is.EqualTo(10));
        }

        [Test]
        public void StorageUpgradeRaisesCapacityAndKeepsStock()
        {
            var state = NewState(cash: 2500);
            state.Inventory.TryImport(new[] { new ResourceAmount("iron.ore", 900) });
            Assert.That(state.TryUpgradeStorage(), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Inventory.Capacity, Is.EqualTo(2000));
            Assert.That(state.Cash, Is.EqualTo(0));
            Assert.That(state.StorageInvestedCash, Is.EqualTo(2500));
            Assert.That(state.Inventory.Free, Is.EqualTo(1100));
        }

        // ---------------------------------------------------------------- MachineInstance

        [Test]
        public void MachineConstructionEnforcesProgressLevelAndDurationBounds()
        {
            var def = RunTestCatalogs.IronLoop().Machines["machine.miner"];
            Assert.That(() => new MachineInstance(Id(1), def, 0, 1, true, 21, 20, 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new MachineInstance(Id(1), def, 0, 1, true, -1, 20, 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new MachineInstance(Id(1), def, 0, 0, true, 0, 20, 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new MachineInstance(Id(1), def, 0, 26, true, 0, 20, 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new MachineInstance(Id(1), def, 0, 1, true, 0, 0, 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new MachineInstance(Id(1), def, -1, 1, true, 0, 20, 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new MachineInstance(Guid.Empty, def, 0, 1, true, 0, 20, 0), Throws.ArgumentException);
            var waiting = new MachineInstance(Id(1), def, 0, 1, true, 20, 20, 0);
            Assert.That(waiting.IsWaiting, Is.True);
        }

        [Test]
        public void MachineProgressStaysWithinZeroAndCycleDuration()
        {
            var state = NewState();
            var m = Machine(state, "machine.miner", 0, 10, progress: 5, cycle: 20);
            Assert.That(m.TrySetProgress(21), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(m.TrySetProgress(-1), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(m.ProgressTicks, Is.EqualTo(5));
            Assert.That(m.TrySetProgress(20), Is.EqualTo(RunStateResult.Ok));
            Assert.That(m.IsWaiting, Is.True);
            Assert.That(m.TryStartCycle(0), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(m.ProgressTicks, Is.EqualTo(20));
            Assert.That(m.TryStartCycle(30), Is.EqualTo(RunStateResult.Ok));
            Assert.That(m.ProgressTicks, Is.EqualTo(0));
            Assert.That(m.CycleDurationTicks, Is.EqualTo(30));
        }

        [Test]
        public void MachineRescalePreservesTheFractionAndKeepsWaitingMachinesWaiting()
        {
            var state = NewState();
            var m = Machine(state, "machine.miner", 0, 10, progress: 5, cycle: 10);
            Assert.That(m.TryRescale(20), Is.EqualTo(RunStateResult.Ok));
            Assert.That(m.ProgressTicks, Is.EqualTo(10));
            Assert.That(m.CycleDurationTicks, Is.EqualTo(20));

            var waiting = Machine(state, "machine.miner", 1, 11, progress: 10, cycle: 10);
            Assert.That(waiting.TryRescale(4), Is.EqualTo(RunStateResult.Ok));
            Assert.That(waiting.IsWaiting, Is.True);
            Assert.That(waiting.ProgressTicks, Is.EqualTo(4));

            Assert.That(waiting.TryRescale(ExactBound.Value + 1), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(waiting.CycleDurationTicks, Is.EqualTo(4));
            Assert.That(waiting.ProgressTicks, Is.EqualTo(4));
        }

        [Test]
        public void MachineUpgradeIsAllOrNothing()
        {
            var state = NewState();
            var m = Machine(state, "machine.miner", 0, 10, progress: 5, cycle: 10);
            Assert.That(m.TryUpgrade(15, 5), Is.EqualTo(RunStateResult.Ok));
            Assert.That(m.Level, Is.EqualTo(2));
            Assert.That(m.InvestedCash, Is.EqualTo(25));
            Assert.That(m.CycleDurationTicks, Is.EqualTo(5));
            Assert.That(m.ProgressTicks, Is.EqualTo(2));

            // Overflow of the invested cash: nothing changes.
            Assert.That(m.TryUpgrade(ExactBound.Value, 5), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(m.Level, Is.EqualTo(2));
            Assert.That(m.InvestedCash, Is.EqualTo(25));
            Assert.That(m.ProgressTicks, Is.EqualTo(2));
            // An invalid new duration: nothing changes.
            Assert.That(m.TryUpgrade(1, 0), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(m.Level, Is.EqualTo(2));
            Assert.That(m.InvestedCash, Is.EqualTo(25));

            var max = Machine(state, "machine.miner", 1, 11, level: 25);
            Assert.That(max.TryUpgrade(1, 1), Is.EqualTo(RunStateResult.LevelMax));
            Assert.That(max.Level, Is.EqualTo(25));
            Assert.That(max.InvestedCash, Is.EqualTo(10));
        }

        // ---------------------------------------------------------------- RunState and PriorityOrder

        [Test]
        public void RunStateConstructionEnforcesIdentityAndUnlockInvariants()
        {
            var catalog = RunTestCatalogs.IronLoop();
            var unlocked = new[] { "iron.ore" };
            Assert.That(() => new RunState(catalog, Id(1), 1, Id(0), 0, 0, 1, 1, unlocked), Throws.ArgumentException);
            Assert.That(() => new RunState(catalog, Id(1), 2, null, 0, 0, 1, 1, unlocked), Throws.ArgumentException);
            Assert.That(() => new RunState(catalog, Id(1), 0, null, 0, 0, 1, 1, unlocked), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new RunState(catalog, Guid.Empty, 1, null, 0, 0, 1, 1, unlocked), Throws.ArgumentException);
            Assert.That(() => new RunState(catalog, Id(1), 1, null, -1, 0, 1, 1, unlocked), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new RunState(catalog, Id(1), 1, null, 0, 0, 6, 1, unlocked), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new RunState(catalog, Id(1), 1, null, 0, 0, 1, 0, unlocked), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new RunState(catalog, Id(1), 1, null, 0, 0, 1, 1, new[] { "nope" }), Throws.ArgumentException);
            Assert.That(() => new RunState(catalog, Id(1), 1, null, 0, 0, 1, 1, new[] { "iron.ore", "iron.ore" }), Throws.ArgumentException);
            var second = new RunState(catalog, Id(2), 2, Id(1), 0, 0, 1, 1, unlocked);
            Assert.That(second.PreviousRunId, Is.EqualTo(Id(1)));
        }

        [Test]
        public void OnlyUnlockedResourcesAppearInTheInventory()
        {
            var state = new RunState(RunTestCatalogs.IronLoop(), Id(1), 1, null, 0, 0, 1, 1, new[] { "iron.ore", "machine.miner" });
            Assert.That(state.Inventory.IsAllowed("iron.ore"), Is.True);
            Assert.That(state.Inventory.IsAllowed("iron.bar"), Is.False);
            Assert.That(state.Inventory.IsAllowed("machine.miner"), Is.False);
            Assert.That(state.Inventory.TryImport(new[] { new ResourceAmount("iron.bar", 1) }), Is.EqualTo(RunStateResult.ResourceLocked));
            Assert.That(state.TrySetAutoSell("iron.bar", true), Is.EqualTo(RunStateResult.ResourceLocked));
            Assert.That(state.TrySetAutoSell("machine.miner", true), Is.EqualTo(RunStateResult.ResourceLocked));
            Assert.That(state.TryUnlock("iron.bar"), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.TryUnlock("iron.bar"), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Unlocked, Is.EqualTo(new[] { "iron.ore", "machine.miner", "iron.bar" }));
            Assert.That(state.Inventory.TryImport(new[] { new ResourceAmount("iron.bar", 1) }), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.TryUnlock("unknown"), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(state.Validate(), Is.EqualTo(RunStateResult.Ok));
        }

        [Test]
        public void AutoSellIsAnOrdinalOrderedSet()
        {
            var state = NewState();
            Assert.That(state.TrySetAutoSell("iron.ore", true), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.TrySetAutoSell("iron.bar", true), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.TrySetAutoSell("iron.bar", true), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.AutoSell, Is.EqualTo(new[] { "iron.bar", "iron.ore" }));
            Assert.That(state.TrySetAutoSell("iron.bar", false), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.AutoSell, Is.EqualTo(new[] { "iron.ore" }));
            Assert.That(state.IsAutoSell("iron.ore"), Is.True);
        }

        [Test]
        public void MachineSlotsAreUniqueWithinSlotCountAndRejectedAtomically()
        {
            var state = NewState();
            Assert.That(state.SlotCount, Is.EqualTo(4));
            var first = Machine(state, "machine.miner", 2, 10);
            Assert.That(state.TryAddMachine(first), Is.EqualTo(RunStateResult.Ok));

            Assert.That(state.TryAddMachine(Machine(state, "machine.smelter", 2, 11)), Is.EqualTo(RunStateResult.SlotOccupied));
            Assert.That(state.TryAddMachine(Machine(state, "machine.smelter", 4, 12)), Is.EqualTo(RunStateResult.SlotUnavailable));
            Assert.That(state.TryAddMachine(Machine(state, "machine.smelter", 0, 10)), Is.EqualTo(RunStateResult.DuplicateInstance));
            var foreign = new MachineDef("machine.other", "O", "P", "recipe.mine-iron-ore", 2, new Curve(1, 1_000_000, "ceil"),
                1_000_000, new SpeedMilestone[0], new UnlockCondition("always"));
            Assert.That(state.TryAddMachine(new MachineInstance(Id(13), foreign, 0, 1, true, 0, 10, 0)), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(state.TryAddMachine(null), Is.EqualTo(RunStateResult.InvalidArgument));

            Assert.That(state.Machines.Count, Is.EqualTo(1));
            Assert.That(state.Priority.ToArray(), Is.EqualTo(new[] { Id(10) }));

            // Machines iterate by slot; the priority keeps build order.
            Assert.That(state.TryAddMachine(Machine(state, "machine.smelter", 0, 14)), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.TryAddMachine(Machine(state, "machine.press", 3, 15)), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Machines.Select(m => m.SlotIndex), Is.EqualTo(new[] { 0, 2, 3 }));
            Assert.That(state.Priority.ToArray(), Is.EqualTo(new[] { Id(10), Id(14), Id(15) }));
            Assert.That(state.Validate(), Is.EqualTo(RunStateResult.Ok));
        }

        [Test]
        public void PriorityIsAPermutationOfTheMachinesAndReorderIsAllOrNothing()
        {
            var state = NewState();
            state.TryAddMachine(Machine(state, "machine.miner", 0, 10));
            state.TryAddMachine(Machine(state, "machine.smelter", 1, 11));
            state.TryAddMachine(Machine(state, "machine.press", 2, 12));
            Guid[] original = state.Priority.ToArray();

            Assert.That(state.TryReorderPriority(new[] { Id(12), Id(10) }), Is.EqualTo(RunStateResult.NotPermutation));
            Assert.That(state.TryReorderPriority(new[] { Id(12), Id(10), Id(10) }), Is.EqualTo(RunStateResult.NotPermutation));
            Assert.That(state.TryReorderPriority(new[] { Id(12), Id(10), Id(99) }), Is.EqualTo(RunStateResult.NotPermutation));
            Assert.That(state.TryReorderPriority(new[] { Id(12), Id(11), Id(10), Id(10) }), Is.EqualTo(RunStateResult.NotPermutation));
            Assert.That(state.TryReorderPriority(null), Is.EqualTo(RunStateResult.NotPermutation));
            Assert.That(state.Priority.ToArray(), Is.EqualTo(original));

            Assert.That(state.TryReorderPriority(new[] { Id(12), Id(10), Id(11) }), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Priority.ToArray(), Is.EqualTo(new[] { Id(12), Id(10), Id(11) }));
            // Slot order is unaffected by the priority.
            Assert.That(state.Machines.Select(m => m.InstanceId), Is.EqualTo(new[] { Id(10), Id(11), Id(12) }));
        }

        [Test]
        public void RemovingAMachineFreesItsSlotAndClosesItsPriorityPosition()
        {
            var state = NewState();
            state.TryAddMachine(Machine(state, "machine.miner", 0, 10));
            state.TryAddMachine(Machine(state, "machine.smelter", 1, 11));
            state.TryAddMachine(Machine(state, "machine.press", 2, 12));
            Assert.That(state.TryRemoveMachine(Id(99), out _), Is.EqualTo(RunStateResult.UnknownInstance));
            Assert.That(state.Machines.Count, Is.EqualTo(3));

            Assert.That(state.TryRemoveMachine(Id(11), out long refund), Is.EqualTo(RunStateResult.Ok));
            Assert.That(refund, Is.EqualTo(5), "floor(10 x 500,000 / 10^6)");
            Assert.That(state.Priority.ToArray(), Is.EqualTo(new[] { Id(10), Id(12) }));
            Assert.That(state.MachineAtSlot(1), Is.Null);
            Assert.That(state.TryAddMachine(Machine(state, "machine.assembler", 1, 13)), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Priority.ToArray(), Is.EqualTo(new[] { Id(10), Id(12), Id(13) }));
            Assert.That(state.Validate(), Is.EqualTo(RunStateResult.Ok));
        }

        [Test]
        public void ValidateDetectsAnInvariantBrokenThroughTheMachineObject()
        {
            var state = NewState();
            var m = Machine(state, "machine.miner", 0, 10);
            state.TryAddMachine(m);
            Assert.That(state.Validate(), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.FindMachine(Id(10)), Is.SameAs(m));
            Assert.That(state.FindMachine(Id(11)), Is.Null);
        }

        [Test]
        public void CashRpAndExpansionMutationsAreCheckedAndAtomic()
        {
            var state = NewState(cash: 100);
            Assert.That(state.TrySpendCash(101), Is.EqualTo(RunStateResult.CashInsufficient));
            Assert.That(state.TrySpendCash(-1), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(state.TryAddCash(ExactBound.Value), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(state.TryAddRp(ExactBound.Value), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.TryAddRp(1), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(state.Cash, Is.EqualTo(100));

            Assert.That(state.TryUpgradeFactory(), Is.EqualTo(RunStateResult.CashInsufficient));
            Assert.That(state.TryUpgradeStorage(), Is.EqualTo(RunStateResult.CashInsufficient));
            Assert.That(state.FactoryLevel, Is.EqualTo(1));
            Assert.That(state.StorageLevel, Is.EqualTo(1));
            Assert.That(state.Cash, Is.EqualTo(100));

            var rich = NewState(cash: 5000);
            Assert.That(rich.TryUpgradeFactory(), Is.EqualTo(RunStateResult.Ok));
            Assert.That(rich.SlotCount, Is.EqualTo(6));
            Assert.That(rich.Cash, Is.EqualTo(0));
            Assert.That(rich.FactoryInvestedCash, Is.EqualTo(5000));
            var top = NewState(cash: 1, factoryLevel: 5, storageLevel: 5);
            Assert.That(top.TryUpgradeFactory(), Is.EqualTo(RunStateResult.LevelMax));
            Assert.That(top.TryUpgradeStorage(), Is.EqualTo(RunStateResult.LevelMax));
        }

        [Test]
        public void ResearchPurchaseSpendsRpAndStopsAtMaxLevel()
        {
            var state = NewState(ProgressionCatalog());
            state.TryAddRp(12);
            Assert.That(state.TryPurchaseResearch("research.smelter-speed", 5), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.TryPurchaseResearch("research.unknown", 5), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(state.TryPurchaseResearch("research.smelter-speed", 8), Is.EqualTo(RunStateResult.RpInsufficient));
            Assert.That(state.Rp, Is.EqualTo(7));
            Assert.That(state.TryPurchaseResearch("research.smelter-speed", 7), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.TryPurchaseResearch("research.smelter-speed", 0), Is.EqualTo(RunStateResult.LevelMax));
            Assert.That(state.ResearchLevel("research.smelter-speed"), Is.EqualTo(2));
            Assert.That(state.Rp, Is.EqualTo(0));
            Assert.That(state.TrySetResearchLevel("research.smelter-speed", 3), Is.EqualTo(RunStateResult.LevelMax));
            Assert.That(state.TrySetResearchLevel("research.smelter-speed", -1), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(state.ResearchLevel("research.smelter-speed"), Is.EqualTo(2));
            Assert.That(state.ResearchLevels().Select(l => l.Id), Is.EqualTo(new[] { "research.smelter-speed" }));
        }

        // ---------------------------------------------------------------- ActiveEffectSet and Overdrive

        [Test]
        public void AtMostOneEffectPerConflictKeyAndAFixedOrder()
        {
            var effects = new ActiveEffectSet();
            var speedAll = new ConflictKey("machineSpeed", "allMachines");
            var outputMiner = new ConflictKey("machineOutput", "machineType:machine.miner");
            Assert.That(effects.TryAdd(new ActiveEffect("item.b-drill", outputMiner, 0, 60_000)), Is.EqualTo(RunStateResult.Ok));
            Assert.That(effects.TryAdd(new ActiveEffect("item.a-boost", speedAll, 0, 60_000)), Is.EqualTo(RunStateResult.Ok));
            Assert.That(effects.TryAdd(new ActiveEffect("item.c-other", speedAll, 10, 70_000)), Is.EqualTo(RunStateResult.EffectConflict));
            Assert.That(effects.TryAdd(new ActiveEffect("item.b-drill", outputMiner, 10, 70_000)), Is.EqualTo(RunStateResult.EffectConflict));
            Assert.That(effects.TryAdd(null), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(effects.Count, Is.EqualTo(2));
            Assert.That(effects.Effects.Select(e => e.SourceId), Is.EqualTo(new[] { "item.a-boost", "item.b-drill" }));

            Assert.That(effects.RemoveExpired(59_999), Is.EqualTo(0));
            Assert.That(effects.RemoveExpired(60_000), Is.EqualTo(2));
            Assert.That(effects.Count, Is.EqualTo(0));
            Assert.That(effects.TryAdd(new ActiveEffect("item.c-other", speedAll, 60_000, 70_000)), Is.EqualTo(RunStateResult.Ok));

            Assert.That(() => new ActiveEffect("i", speedAll, 5, 5), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new ConflictKey("", "x"), Throws.ArgumentException);
        }

        [Test]
        public void OverdriveKeepsCooldownMinusActiveEqualToCooldownMs()
        {
            var def = RunTestCatalogs.IronLoop().Overdrive;
            var od = new OverdriveState(def);
            Assert.That(od.TryActivate(1_000), Is.EqualTo(RunStateResult.Ok));
            Assert.That(od.ActiveUntilUtcMs, Is.EqualTo(1_000 + def.DurationMs));
            Assert.That(od.CooldownUntilUtcMs.Value - od.ActiveUntilUtcMs.Value, Is.EqualTo(def.CooldownMs));

            long active = od.ActiveUntilUtcMs.Value;
            long cooldown = od.CooldownUntilUtcMs.Value;
            Assert.That(od.TryActivate(active - 1), Is.EqualTo(RunStateResult.OverdriveActive));
            Assert.That(od.TryActivate(active), Is.EqualTo(RunStateResult.OverdriveCoolingDown));
            Assert.That(od.TryActivate(cooldown - 1), Is.EqualTo(RunStateResult.OverdriveCoolingDown));
            Assert.That(od.ActiveUntilUtcMs, Is.EqualTo(active));
            Assert.That(od.CooldownUntilUtcMs, Is.EqualTo(cooldown));
            Assert.That(od.IsActive(active - 1), Is.True);
            Assert.That(od.IsActive(active), Is.False);
            Assert.That(od.IsCoolingDown(active), Is.True);
            Assert.That(od.ClearElapsed(cooldown - 1), Is.False);
            Assert.That(od.TryActivate(cooldown), Is.EqualTo(RunStateResult.Ok));
            Assert.That(od.ActiveUntilUtcMs, Is.EqualTo(cooldown + def.DurationMs));

            // Persisted instants are authoritative (owner decision 2026-10-01): only 0 <= span <= MaxTimedDurationMs holds.
            Assert.That(() => new OverdriveState(def, 10, 10 + def.CooldownMs + 1), Throws.Nothing);
            Assert.That(() => new OverdriveState(def, 10, 10 + RunLimits.MaxTimedDurationMs + 1), Throws.ArgumentException);
            Assert.That(() => new OverdriveState(def, 10, 9), Throws.ArgumentException);
            Assert.That(() => new OverdriveState(def, 10, null), Throws.ArgumentException);
            Assert.That(new OverdriveState(def, 10, 10 + def.CooldownMs).IsCoolingDown(11), Is.True);
            Assert.That(od.TryActivate(-1), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(new OverdriveState(def).TryActivate(long.MaxValue), Is.EqualTo(RunStateResult.ValueLimit));
        }

        // ---------------------------------------------------------------- ContractBoard

        [Test]
        public void ContractsFixRequiredUnitsAndDeadlineAtAcceptance()
        {
            var state = NewState(ProgressionCatalog());
            var board = state.Contracts;
            Assert.That(board.TryAccept("contract.timed-bar", 5_000), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.Active.RequiredUnits, Is.EqualTo(40));
            Assert.That(board.Active.DeadlineUtcMs, Is.EqualTo(185_000));
            Assert.That(board.TryAccept("contract.plate", 5_000), Is.EqualTo(RunStateResult.ContractActiveExists));
            Assert.That(board.TryAccept("contract.missing", 5_000), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(board.TryAbandon(), Is.EqualTo(RunStateResult.InvalidArgument), "timed contracts cannot be abandoned");
            Assert.That(board.Active, Is.Not.Null);

            Assert.That(board.TryCompleteActive(185_001), Is.EqualTo(RunStateResult.ContractExpired));
            Assert.That(board.Active, Is.Not.Null);
            Assert.That(board.TryCompleteActive(185_000), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.Active, Is.Null);
            Assert.That(board.CompletionCount("contract.timed-bar"), Is.EqualTo(1));
            Assert.That(board.TryCompleteActive(0), Is.EqualTo(RunStateResult.ContractNoneActive));

            // The next acceptance uses the next curve value (ceil(40 x 1.01) = 41).
            Assert.That(board.TryAccept("contract.timed-bar", 200_000), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.Active.RequiredUnits, Is.EqualTo(41));
            Assert.That(board.TryExpire(200_000 + 180_000), Is.False);
            Assert.That(board.TryExpire(200_000 + 180_001), Is.True);
            Assert.That(board.CompletionCount("contract.timed-bar"), Is.EqualTo(1), "expiry leaves the count unchanged");
            Assert.That(board.TryExpire(999_999_999), Is.False);
        }

        [Test]
        public void UntimedContractsCanBeAbandonedWithoutCountChange()
        {
            var board = NewState(ProgressionCatalog()).Contracts;
            Assert.That(board.TryAbandon(), Is.EqualTo(RunStateResult.ContractNoneActive));
            Assert.That(board.TryAccept("contract.plate", 0), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.Active.DeadlineUtcMs, Is.Null);
            Assert.That(board.Active.RequiredUnits, Is.EqualTo(5));
            Assert.That(board.TryAbandon(), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.CompletionCount("contract.plate"), Is.EqualTo(0));
            Assert.That(board.CompletionCounts(), Is.Empty);
            Assert.That(board.TrySetCompletionCount("contract.plate", 1_024), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.TryAccept("contract.plate", 0), Is.EqualTo(RunStateResult.InvalidArgument), "beyond the 1,024-entry table");
            Assert.That(board.Active, Is.Null);
            Assert.That(board.TrySetCompletionCount("contract.missing", 1), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(board.TrySetCompletionCount("contract.plate", -1), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(board.TryAccept("contract.plate", 0), Is.EqualTo(RunStateResult.InvalidArgument));
        }

        // ---------------------------------------------------------------- PermanentState

        [Test]
        public void PermanentStateSpendsIpAndKeepsSortedStacksAtomically()
        {
            var permanent = new PermanentState(ProgressionCatalog());
            Assert.That(permanent.TryAddIp(12), Is.EqualTo(RunStateResult.Ok));
            Assert.That(permanent.TryAddIp(ExactBound.Value), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(permanent.Ip, Is.EqualTo(12));

            Assert.That(permanent.TryPurchaseIndustry("industry.cash", 13), Is.EqualTo(RunStateResult.IpInsufficient));
            Assert.That(permanent.TryPurchaseIndustry("industry.missing", 1), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(permanent.TryPurchaseIndustry("industry.cash", 5), Is.EqualTo(RunStateResult.Ok));
            Assert.That(permanent.TryPurchaseIndustry("industry.cash", 7), Is.EqualTo(RunStateResult.Ok));
            Assert.That(permanent.TryPurchaseIndustry("industry.cash", 0), Is.EqualTo(RunStateResult.LevelMax));
            Assert.That(permanent.IndustryLevel("industry.cash"), Is.EqualTo(2));
            Assert.That(permanent.Ip, Is.EqualTo(0));

            Assert.That(permanent.TryConsumeItem("item.b-drill"), Is.EqualTo(RunStateResult.ItemNone));
            Assert.That(permanent.TryAddItems("item.b-drill", 2), Is.EqualTo(RunStateResult.Ok));
            Assert.That(permanent.TryAddItems("item.a-boost", 1), Is.EqualTo(RunStateResult.Ok));
            Assert.That(permanent.TryAddItems("item.unknown", 1), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(permanent.TryAddItems("item.a-boost", 0), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(permanent.TryAddItems("item.a-boost", ExactBound.Value), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(permanent.ItemStacks().Select(s => s.Id), Is.EqualTo(new[] { "item.a-boost", "item.b-drill" }));
            Assert.That(permanent.TryConsumeItem("item.a-boost"), Is.EqualTo(RunStateResult.Ok));
            Assert.That(permanent.ItemStacks().Select(s => s.Id), Is.EqualTo(new[] { "item.b-drill" }));
            Assert.That(permanent.ItemCount("item.b-drill"), Is.EqualTo(2));
            Assert.That(permanent.TryIncrementPrestige(), Is.EqualTo(RunStateResult.Ok));
            Assert.That(permanent.PrestigeCount, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- Review fixes (A1-A6)

        private static long[] StateSnapshot(RunState state)
        {
            var list = new List<long>(Snapshot(state.Inventory));
            list.Add(state.Cash);
            list.Add(state.Rp);
            list.Add(state.Machines.Count);
            list.Add(state.Priority.Count);
            foreach (var m in state.Machines)
            {
                list.Add(m.Level); list.Add(m.ProgressTicks); list.Add(m.CycleDurationTicks); list.Add(m.InvestedCash);
            }
            return list.ToArray();
        }

        [Test]
        public void A1_EveryPersistedFieldIsRestorableAndRevalidated()
        {
            var catalog = ProgressionCatalog();
            var od = catalog.Overdrive;
            var state = new RunState(catalog, Id(2), 3, Id(1), 777, 55, 2, 2, AllResources.Concat(new[] { "machine.miner", "contract.plate", "research.smelter-speed" }));
            Assert.That(state.TryRestoreInvestment(2500, 5000), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.StorageInvestedCash, Is.EqualTo(2500));
            Assert.That(state.FactoryInvestedCash, Is.EqualTo(5000));
            Assert.That(state.TryRestoreInvestment(-1, 0), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(state.TryRestoreInvestment(0, ExactBound.Value + 1), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(state.StorageInvestedCash, Is.EqualTo(2500));

            Assert.That(state.Overdrive.TryRestore(1_000, 1_000 + od.CooldownMs), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Overdrive.ActiveUntilUtcMs, Is.EqualTo(1_000));
            Assert.That(state.Overdrive.TryRestore(1_000, 1_001 + od.CooldownMs), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Overdrive.TryRestore(1_000, 1_001 + RunLimits.MaxTimedDurationMs), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(state.Overdrive.TryRestore(1_000, 999), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(state.Overdrive.TryRestore(1_000, null), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(state.Overdrive.TryRestore(-5, -5 + od.CooldownMs), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(state.Overdrive.CooldownUntilUtcMs, Is.EqualTo(1_001 + od.CooldownMs));
            Assert.That(state.Overdrive.TryRestore(null, null), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Overdrive.TryRestore(1_000, 1_000 + od.CooldownMs), Is.EqualTo(RunStateResult.Ok));

            Assert.That(state.TryAddEffect(new ActiveEffect("item.b-drill", new ConflictKey("machineOutput", "machineType:machine.miner"), 100, 60_100)), Is.EqualTo(RunStateResult.Ok));
            // The persisted end is authoritative (owner decision 2026-10-01): a retuned duration loads, a span above the cap does not.
            Assert.That(state.TryAddEffect(new ActiveEffect("item.a-boost", new ConflictKey("machineSpeed", "all"), 100, 60_101)), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.TryAddEffect(new ActiveEffect("item.zz-long", new ConflictKey("machineSpeed", "other"), 100, 100 + RunLimits.MaxTimedDurationMs + 1)), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(state.TryAddEffect(new ActiveEffect("item.zzz", new ConflictKey("machineSpeed", "all"), 100, 60_100)), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(state.TrySetResearchLevel("research.smelter-speed", 1), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Contracts.TrySetCompletionCount("contract.plate", 2), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Contracts.TrySetActive(new ActiveContract("contract.plate", 10, null, 7)), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.TrySetAutoSell("iron.ore", true), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Inventory.TryImport(new[] { new ResourceAmount("iron.ore", 30) }), Is.EqualTo(RunStateResult.Ok));
            var miner = new MachineInstance(Id(50), catalog.Machines["machine.miner"], 5, 3, true, 10, 20, 99);
            Assert.That(state.TryAddMachine(miner), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Validate(), Is.EqualTo(RunStateResult.Ok));

            // Contract restore: the deadline is present exactly for timed offers and the persisted span is authoritative within the cap.
            Assert.That(state.Contracts.TrySetActive(new ActiveContract("contract.plate", 10, 20, 7)), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(state.Contracts.TrySetActive(new ActiveContract("contract.timed-bar", 10, 10 + 180_000, 7)), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Contracts.TrySetActive(new ActiveContract("contract.timed-bar", 10, 11 + 180_000, 7)), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Contracts.TrySetActive(new ActiveContract("contract.timed-bar", 10, 10, 7)), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(state.Contracts.TrySetActive(new ActiveContract("contract.timed-bar", 10, 10 + RunLimits.MaxTimedDurationMs + 1, 7)), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(state.Contracts.TrySetActive(new ActiveContract("contract.timed-bar", 10, null, 7)), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(state.Validate(), Is.EqualTo(RunStateResult.Ok));
        }

        [Test]
        public void A1_PermanentIndustryLevelsAreRestorable()
        {
            var catalog = ProgressionCatalog();
            var state = NewState(catalog);
            Assert.That(state.Validate(), Is.EqualTo(RunStateResult.Ok));
            var permanent = new PermanentState(catalog);
            Assert.That(permanent.TrySetIndustryLevel("industry.cash", 2), Is.EqualTo(RunStateResult.Ok));
            Assert.That(permanent.IndustryLevel("industry.cash"), Is.EqualTo(2));
            Assert.That(permanent.TrySetIndustryLevel("industry.cash", 3), Is.EqualTo(RunStateResult.LevelMax));
            Assert.That(permanent.TrySetIndustryLevel("industry.cash", -1), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(permanent.TrySetIndustryLevel("industry.nope", 1), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(permanent.IndustryLevel("industry.cash"), Is.EqualTo(2));
            Assert.That(permanent.TrySetIndustryLevel("industry.cash", 0), Is.EqualTo(RunStateResult.Ok));
            Assert.That(permanent.IndustryLevels(), Is.Empty);
        }

        [Test]
        public void A2_PriorityMembershipCannotBeChangedFromOutsideTheState()
        {
            // PriorityOrder.TryAppend, TryRemove and TryReorder are internal. From outside Core, reordering goes only through RunState.TryReorderPriority and the ReorderPriority command.
            var t = typeof(PriorityOrder);
            var any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            Assert.That(t.GetMethod("TryAppend", any), Is.Not.Null);
            Assert.That(t.GetMethod("TryAppend", any).IsPublic, Is.False);
            Assert.That(t.GetMethod("TryRemove", any).IsPublic, Is.False);
            var reorder = t.GetMethod("TryReorder", any);
            Assert.That(reorder, Is.Not.Null);
            Assert.That(reorder.IsPublic, Is.False);
            Assert.That(reorder.IsAssembly, Is.True);
            Assert.That(typeof(GlobalInventory).GetMethod("Allow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public), Is.Null);
            Assert.That(typeof(GlobalInventory).GetMethod("TrySetCapacity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public), Is.Null);
        }

        [Test]
        public void A3_SellIsAtomicAcrossStockAndCash()
        {
            var state = NewState(cash: 100);
            state.Inventory.TryImport(new[] { new ResourceAmount("iron.ore", 10) });
            Assert.That(state.TryAddCash(ExactBound.Value - 100), Is.EqualTo(RunStateResult.Ok));
            long[] before = StateSnapshot(state);
            // Second step (cash credit) overflows: stock must stay.
            Assert.That(state.TrySell("iron.ore", 5, 1), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(StateSnapshot(state), Is.EqualTo(before));
            Assert.That(state.TrySell("iron.ore", 11, 0), Is.EqualTo(RunStateResult.StockInsufficient));
            Assert.That(state.TrySell("iron.ore", 1, -1), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(StateSnapshot(state), Is.EqualTo(before));

            var ok = NewState(cash: 100);
            ok.Inventory.TryImport(new[] { new ResourceAmount("iron.ore", 10) });
            Assert.That(ok.TrySell("iron.ore", 4, 7), Is.EqualTo(RunStateResult.Ok));
            Assert.That(ok.Inventory.Units("iron.ore"), Is.EqualTo(6));
            Assert.That(ok.Cash, Is.EqualTo(107));
        }

        [Test]
        public void A3_BuildUpgradeAndRemoveAreAtomic()
        {
            var state = NewState(cash: 100);
            var machine = new MachineInstance(Id(10), state.Catalog.Machines["machine.miner"], 0, 1, true, 0, 20, 10);
            long[] before = StateSnapshot(state);
            Assert.That(state.TryBuildMachine(machine, 11), Is.EqualTo(RunStateResult.InvalidArgument), "investedCash must equal the cost");
            var pricey = new MachineInstance(Id(10), state.Catalog.Machines["machine.miner"], 0, 1, true, 0, 20, 101);
            Assert.That(state.TryBuildMachine(pricey, 101), Is.EqualTo(RunStateResult.CashInsufficient));
            var badSlot = new MachineInstance(Id(11), state.Catalog.Machines["machine.miner"], 9, 1, true, 0, 20, 10);
            Assert.That(state.TryBuildMachine(badSlot, 10), Is.EqualTo(RunStateResult.SlotUnavailable));
            Assert.That(StateSnapshot(state), Is.EqualTo(before));

            Assert.That(state.TryBuildMachine(machine, 10), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Cash, Is.EqualTo(90));
            Assert.That(state.Priority.ToArray(), Is.EqualTo(new[] { Id(10) }));
            Assert.That(state.TryBuildMachine(new MachineInstance(Id(12), state.Catalog.Machines["machine.miner"], 0, 1, true, 0, 20, 10), 10),
                Is.EqualTo(RunStateResult.SlotOccupied));
            Assert.That(state.Cash, Is.EqualTo(90));

            // Upgrade: cash short, invalid duration and overflow leave cash, level and progress unchanged.
            machine.TrySetProgress(10);
            before = StateSnapshot(state);
            Assert.That(state.TryUpgradeMachine(Id(10), 91, 10), Is.EqualTo(RunStateResult.CashInsufficient));
            Assert.That(state.TryUpgradeMachine(Id(10), 10, 0), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(state.TryUpgradeMachine(Id(99), 10, 10), Is.EqualTo(RunStateResult.UnknownInstance));
            Assert.That(StateSnapshot(state), Is.EqualTo(before));
            Assert.That(state.TryUpgradeMachine(Id(10), 10, 10), Is.EqualTo(RunStateResult.Ok));
            Assert.That(state.Cash, Is.EqualTo(80));
            Assert.That(machine.Level, Is.EqualTo(2));
            Assert.That(machine.InvestedCash, Is.EqualTo(20));
            Assert.That(machine.ProgressTicks, Is.EqualTo(5));

            // Remove: refund = floor(20 x 500,000 / 10^6) = 10; a cash overflow rejects the whole removal.
            var rich = NewState(cash: 100);
            rich.TryBuildMachine(new MachineInstance(Id(20), rich.Catalog.Machines["machine.miner"], 0, 1, true, 0, 20, 10), 10);
            Assert.That(rich.TryRemoveMachine(Id(20), out long none), Is.EqualTo(RunStateResult.Ok));
            Assert.That(none, Is.EqualTo(5));
            Assert.That(rich.Cash, Is.EqualTo(95));
            Assert.That(rich.Machines, Is.Empty);
            Assert.That(rich.Priority.Count, Is.EqualTo(0));

            var capped = NewState(cash: 100);
            capped.TryBuildMachine(new MachineInstance(Id(21), capped.Catalog.Machines["machine.miner"], 0, 1, true, 0, 20, 10), 10);
            capped.TryAddCash(ExactBound.Value - 90);
            capped.TryAddCash(0);
            Assert.That(capped.Cash, Is.EqualTo(ExactBound.Value));
            long[] atCap = StateSnapshot(capped);
            Assert.That(capped.TryRemoveMachine(Id(21), out long refund), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(refund, Is.EqualTo(0));
            Assert.That(StateSnapshot(capped), Is.EqualTo(atCap));
            Assert.That(capped.MachineAtSlot(0), Is.Not.Null);
        }

        [Test]
        public void A3_DeliveryGrantsRewardsAndFailsAtomically()
        {
            var catalog = ProgressionCatalog();
            // Rebuild the offers with cash + rp + an item reward at completion index 0.
            var always = new UnlockCondition("always");
            var offer = new ContractOfferDef("contract.plate", "plate", "iron.plate", new Curve(5, 1_010_000, "ceil"), null,
                new[]
                {
                    new ContractRewardDef("cash", new Curve(100, 1_000_000, "floor")),
                    new ContractRewardDef("rp", new Curve(3, 1_000_000, "floor")),
                    new ContractRewardDef("item", null, "item.b-drill", new[] { 0 }),
                }, always);
            var withItem = IdleFactory.Tests.EditMode.Run.TestCatalogBuilder.From(catalog).WithRevision(1)
                .WithResources(catalog.Resources.Values.OrderBy(r => r.Id, StringComparer.Ordinal))
                .WithContractOffers(new[] { offer }).Build();

            var state = NewState(withItem, cash: 0);
            var permanent = new PermanentState(withItem);
            Assert.That(state.TryDeliverContract(0, permanent, out _, out _), Is.EqualTo(RunStateResult.ContractNoneActive));
            state.Contracts.TryAccept("contract.plate", 0);
            state.Inventory.TryImport(new[] { new ResourceAmount("iron.plate", 4) });
            Assert.That(state.TryDeliverContract(0, permanent, out _, out _), Is.EqualTo(RunStateResult.StockInsufficient));
            state.Inventory.TryImport(new[] { new ResourceAmount("iron.plate", 2) });
            Assert.That(state.TryDeliverContract(0, null, out _, out _), Is.EqualTo(RunStateResult.InvalidArgument), "an Item is due");

            // Second step fails (cash overflow): stock, contract, RP and items stay.
            state.TryAddCash(ExactBound.Value);
            long[] before = StateSnapshot(state);
            Assert.That(state.TryDeliverContract(0, permanent, out long c0, out long r0), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That((c0, r0), Is.EqualTo((0L, 0L)));
            Assert.That(StateSnapshot(state), Is.EqualTo(before));
            Assert.That(state.Contracts.Active, Is.Not.Null);
            Assert.That(state.Contracts.CompletionCount("contract.plate"), Is.EqualTo(0));
            Assert.That(permanent.ItemCount("item.b-drill"), Is.EqualTo(0));

            var good = NewState(withItem, cash: 0);
            good.Contracts.TryAccept("contract.plate", 0);
            good.Inventory.TryImport(new[] { new ResourceAmount("iron.plate", 6) });
            Assert.That(good.TryDeliverContract(0, permanent, out long cash, out long rp), Is.EqualTo(RunStateResult.Ok));
            Assert.That((cash, rp), Is.EqualTo((100L, 3L)));
            Assert.That(good.Cash, Is.EqualTo(100));
            Assert.That(good.Rp, Is.EqualTo(3));
            Assert.That(good.Inventory.Units("iron.plate"), Is.EqualTo(1));
            Assert.That(good.Contracts.Active, Is.Null);
            Assert.That(good.Contracts.CompletionCount("contract.plate"), Is.EqualTo(1));
            Assert.That(permanent.ItemCount("item.b-drill"), Is.EqualTo(1));

            // Completion index 1 has no item reward, so no permanent state is needed.
            good.Contracts.TryAccept("contract.plate", 0);
            good.Inventory.TryImport(new[] { new ResourceAmount("iron.plate", 10) });
            Assert.That(good.TryDeliverContract(0, null, out _, out _), Is.EqualTo(RunStateResult.Ok));
            Assert.That(permanent.ItemCount("item.b-drill"), Is.EqualTo(1));
        }

        [Test]
        public void A4_InventoryRejectsDuplicateIdsWithinOneSide()
        {
            var inventory = new GlobalInventory(AllResources, 100);
            inventory.TryImport(new[] { new ResourceAmount("iron.ore", 10) });
            long[] before = Snapshot(inventory);
            var dup = new[] { new ResourceAmount("iron.ore", 1), new ResourceAmount("iron.ore", 2) };
            Assert.That(inventory.TryImport(dup), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(inventory.TryExchange(dup, new[] { new ResourceAmount("iron.bar", 1) }, 1, 1), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(Snapshot(inventory), Is.EqualTo(before));
            // The same resource on both sides nets out.
            var same = new[] { new ResourceAmount("iron.ore", 2) };
            var more = new[] { new ResourceAmount("iron.ore", 3) };
            Assert.That(inventory.TryExchange(same, more, 2, 1), Is.EqualTo(RunStateResult.Ok));
            Assert.That(inventory.Units("iron.ore"), Is.EqualTo(12));
            Assert.That(inventory.Total, Is.EqualTo(12));
            Assert.That(inventory.TryExchange(more, same, 5, 1), Is.EqualTo(RunStateResult.StockInsufficient));
        }

        [Test]
        public void A4_WarmHotPathsDoNotAllocate()
        {
            var catalog = RunTestCatalogs.IronLoop();
            var smelt = catalog.Recipes["recipe.smelt-iron-bar"];
            var sellInventory = new GlobalInventory(AllResources, 1_000_000);
            var effects = new ActiveEffectSet();
            var speedKey = new ConflictKey("machineSpeed", "all");
            var machine = new MachineInstance(Id(1), catalog.Machines["machine.smelter"], 0, 1, true, 10, 20, 0);
            var research = new[] { new SpeedFactor(1_100_000, 3) };
            var items = new long[] { 1_500_000 };
            var effect = new ActiveEffect("item.a", speedKey, 0, 10);
            var seed = new[] { new ResourceAmount("iron.ore", 1_000) };
            sellInventory.TryImport(seed);

            void Round()
            {
                sellInventory.TryExchange(smelt.Inputs, smelt.Outputs, 2, 2);
                sellInventory.TrySell("iron.bar", 1);
                sellInventory.TryDeliver("iron.bar", 1);
                sellInventory.TryExchange(smelt.Outputs, smelt.Inputs, 1, 1);
                SpeedComposer.TryCompose(1_080_000, research, research, items, 2_000_000, 100_000_000, out long speed);
                SpeedComposer.TryBatch(20_000_000, speed, 1_000_000, out long k, out long cycle);
                SpeedComposer.TryCompletedUnits(k, smelt, 1, sellInventory, out _);
                SpeedComposer.TryRescaleProgress(5, 10, 7, out _);
                SpeedComposer.TryComposeOutputFactor(items, 4, out _);
                machine.TryRescale(40);
                machine.TryRescale(20);
                machine.TrySetProgress(3);
                effects.TryAdd(effect);
                effects.RemoveExpired(10);
            }

            for (int i = 0; i < 20; i++) Round();
            var counts = new int[5];
            for (int i = 0; i < counts.Length; i++) counts[i] = Fixtures.GcAllocationCounter.Count(() => { for (int n = 0; n < 50; n++) Round(); });
            Assert.That(counts, Is.All.Zero, "warm exchange, sale, composition, batch and expiry paths must not allocate");
        }

        [Test]
        public void A6_LevelCapAndMachineLevelAgainstTheCatalog()
        {
            Assert.That(SpeedComposer.TryCompose(Ppm, new[] { new SpeedFactor(1_000_001, 1025) }, null, null, Ppm, 100 * Ppm, out long s), Is.False);
            Assert.That(s, Is.EqualTo(0));
            Assert.That(SpeedComposer.TryCompose(Ppm, new[] { new SpeedFactor(1_000_001, 1024) }, null, null, Ppm, 100 * Ppm, out _), Is.True);

            var state = NewState();
            var miner = state.Catalog.Machines["machine.miner"];
            var other = new MachineDef("machine.miner", "M", "P", "recipe.mine-iron-ore", 10, new Curve(1, 1_000_000, "ceil"),
                1_000_000, new SpeedMilestone[0], new UnlockCondition("always"));
            Assert.That(state.TryAddMachine(new MachineInstance(Id(1), other, 0, 1, true, 0, 10, 0)), Is.EqualTo(RunStateResult.InvalidArgument),
                "MaxLevel disagrees with the catalog definition");
            Assert.That(state.TryAddMachine(new MachineInstance(Id(1), miner, 0, 25, true, 0, 10, 0)), Is.EqualTo(RunStateResult.Ok));
        }

        private const long Ppm = 1_000_000;
    }
}
