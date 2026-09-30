using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The factory commands of <see cref="RunCommands"/> (T137, contracts/run-application.md, FR-006, FR-008, FR-009,
    /// FR-012, FR-013, US1 scenarios 1, 2, 3 and 5, ECO-7): every rejection code leaves the state and both revisions
    /// unchanged, the refund and Factory Value arithmetic under an <c>upgradeCost</c> discount, replacement as remove
    /// then build, upgrade mid-cycle and at a milestone, and the three cross-cutting rules of the T099 review: the
    /// settle pre-check (a command never returns <c>value.limit</c> after it mutated), the structural flag of every
    /// command, and <c>lifecycle.blocked</c>.
    /// </summary>
    public sealed class RunCommandTests
    {
        private const string Miner = RunCommandFixtures.Miner;
        private const string Smelter = RunCommandFixtures.Smelter;
        private const string Press = RunCommandFixtures.Press;
        private const string Assembler = RunCommandFixtures.Assembler;

        private static RunCommandFixtures.Rig Rig(RunCommandFixtures.Options o = null, long cash = 100_000, int storageLevel = 1,
            int factoryLevel = 1, string[] unlockedMachines = null, string[] lockedResources = null) =>
            RunCommandFixtures.NewRig(o, cash, storageLevel, factoryLevel, unlockedMachines, lockedResources);

        private static Guid BuildOk(RunCommandFixtures.Rig rig, int slot, string machineId)
        {
            RunCommandResult<BuildResult> r = rig.Commands.Build(slot, machineId);
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            return r.Value.InstanceId;
        }

        /// <summary>Runs a command that must be rejected with <paramref name="code"/> and changes nothing at all.</summary>
        private static void AssertRejected<T>(RunCommandFixtures.Rig rig, Func<RunCommandResult<T>> command, string code)
        {
            string before = RunCommandFixtures.Snapshot(rig.State);
            long structure = rig.Session.StructureRevision, quantity = rig.Session.QuantityRevision;
            long ticks = rig.Session.ActiveTicksSinceAnchor;

            RunCommandResult<T> result = command();

            Assert.That(result.IsSuccess, Is.False, "expected " + code);
            Assert.That(result.Outcome, Is.EqualTo(RunCommandOutcome.Rejected));
            Assert.That(result.ReasonCode, Is.EqualTo(code));
            Assert.That(result.Value, Is.EqualTo(default(T)), "a rejection carries no value");
            Assert.That(RunCommandFixtures.Snapshot(rig.State), Is.EqualTo(before), "a rejection never changes state");
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure));
            Assert.That(rig.Session.QuantityRevision, Is.EqualTo(quantity));
            Assert.That(rig.Session.ActiveTicksSinceAnchor, Is.EqualTo(ticks));
        }

        // ------------------------------------------------------------------------------------------------
        // Build (US1 scenarios 1 and 2)
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Build_ChargesTheCostOnce_AndCreatesALevelOneMachineThatJoinsThePriorityLast()
        {
            var rig = Rig(cash: 100);
            MachineDef miner = rig.Catalog.Machines[Miner];
            long cost = miner.CostTable[0];

            RunCommandResult<BuildResult> first = rig.Commands.Build(2, Miner);

            Assert.That(first.IsSuccess, Is.True, first.ReasonCode);
            Assert.That(first.Value.CostPaid, Is.EqualTo(cost));
            Assert.That(first.Value.SlotIndex, Is.EqualTo(2));
            Assert.That(first.Value.NextUpgradeCost, Is.EqualTo(miner.CostTable[1]));
            Assert.That(rig.State.Cash, Is.EqualTo(100 - cost), "charged once");
            MachineInstance m = rig.State.MachineAtSlot(2);
            Assert.That(m.InstanceId, Is.EqualTo(first.Value.InstanceId));
            Assert.That(m.Level, Is.EqualTo(1));
            Assert.That(m.IsActive, Is.True);
            Assert.That(m.ProgressTicks, Is.Zero);
            Assert.That(m.InvestedCash, Is.EqualTo(cost), "investedCash records what was paid");

            // A duplicate type in another slot remains possible; it joins the priority last.
            Guid second = BuildOk(rig, 0, Miner);
            Assert.That(rig.State.Priority.Ids, Is.EqualTo(new[] { first.Value.InstanceId, second }));
            Assert.That(rig.State.Machines.Select(x => x.SlotIndex), Is.EqualTo(new[] { 0, 2 }), "kept in slot order");
            Assert.That(rig.State.Cash, Is.EqualTo(100 - 2 * cost));
        }

        [Test]
        public void Build_ReportsTheLevelOneRatePerMinute()
        {
            var rig = Rig();
            BuildResult r = rig.Commands.Build(0, Miner).Value;

            Assert.That(r.Rates.BatchFactor, Is.EqualTo(1));
            Assert.That(r.Rates.CycleTicks, Is.EqualTo(20_000_000));
            Assert.That(r.Rates.Inputs, Is.Empty);
            Assert.That(r.Rates.Outputs.Single().ResourceId, Is.EqualTo("iron.ore"));
            Assert.That(r.Rates.Outputs.Single().PerMinuteMicro, Is.EqualTo(60_000_000), "2 Ore per 2 s is 60 per minute");
            Assert.That(r.Rates.Outputs.Single().PerMinuteText, Is.EqualTo("60"));
        }

        [Test]
        public void Build_RejectsWithTheNamedReason_AndChangesNothing()
        {
            var rig = Rig(cash: 100, unlockedMachines: new[] { Miner, Smelter });
            BuildOk(rig, 1, Miner);

            AssertRejected(rig, () => rig.Commands.Build(1, Smelter), RunReasons.SlotOccupied);
            AssertRejected(rig, () => rig.Commands.Build(4, Smelter), RunReasons.SlotUnavailable);
            AssertRejected(rig, () => rig.Commands.Build(-1, Smelter), RunReasons.SlotUnavailable);
            AssertRejected(rig, () => rig.Commands.Build(0, Press), RunReasons.MachineLocked);
            AssertRejected(rig, () => rig.Commands.Build(0, "machine.unknown"), RunReasons.MachineLocked);
            AssertRejected(rig, () => rig.Commands.Build(0, null), RunReasons.MachineLocked);

            var poor = Rig(cash: rig.Catalog.Machines[Miner].CostTable[0] - 1);
            AssertRejected(poor, () => poor.Commands.Build(0, Miner), RunReasons.CashInsufficient);
        }

        [Test]
        public void Build_WhenTheFactoryLevelOpensMoreSlots_AcceptsThem()
        {
            var rig = Rig(cash: 100_000);
            AssertRejected(rig, () => rig.Commands.Build(4, Miner), RunReasons.SlotUnavailable);
            Assert.That(rig.Commands.UpgradeFactory().IsSuccess, Is.True);
            Assert.That(rig.Commands.Build(4, Miner).IsSuccess, Is.True);
            Assert.That(rig.Commands.Build(5, Miner).IsSuccess, Is.True);
            AssertRejected(rig, () => rig.Commands.Build(6, Miner), RunReasons.SlotUnavailable);
        }

        // ------------------------------------------------------------------------------------------------
        // Upgrade (US1 scenario 5)
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Upgrade_ChargesTheCost_RaisesTheLevel_AndReportsTheNewRateAndTheNextCost()
        {
            var rig = Rig(cash: 1000);
            MachineDef miner = rig.Catalog.Machines[Miner];
            Guid id = BuildOk(rig, 0, Miner);
            long cash = rig.State.Cash;

            RunCommandResult<UpgradeResult> r = rig.Commands.Upgrade(id);

            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            Assert.That(r.Value.OldLevel, Is.EqualTo(1));
            Assert.That(r.Value.NewLevel, Is.EqualTo(2));
            Assert.That(r.Value.CostPaid, Is.EqualTo(miner.CostTable[1]));
            Assert.That(rig.State.Cash, Is.EqualTo(cash - miner.CostTable[1]));
            Assert.That(rig.State.FindMachine(id).Level, Is.EqualTo(2));
            Assert.That(rig.State.FindMachine(id).InvestedCash, Is.EqualTo(miner.CostTable[0] + miner.CostTable[1]));
            Assert.That(r.Value.HasNextUpgrade, Is.True);
            Assert.That(r.Value.NextCost, Is.EqualTo(miner.CostTable[2]));
            Assert.That(r.Value.MilestoneReached, Is.False);

            long cycle = ExpectedCycle(20_000_000, miner.SpeedTable[2]);
            Assert.That(r.Value.Rates.CycleTicks, Is.EqualTo(cycle));
            Assert.That(r.Value.Rates.CycleTicks, Is.EqualTo(18_518_519), "20,000,000 / 1.08 rounded up");
            Assert.That(r.Value.Rates.Outputs.Single().PerMinuteMicro, Is.EqualTo(ExpectedRate(2, cycle)));
            Assert.That(rig.State.FindMachine(id).CycleDurationTicks, Is.EqualTo(cycle));
        }

        [Test]
        public void Upgrade_MidCycle_KeepsTheCompletedFraction()
        {
            var rig = Rig(cash: 1000);
            Guid id = BuildOk(rig, 0, Miner);
            rig.Advance(10_000_000);
            MachineInstance m = rig.State.FindMachine(id);
            Assert.That(m.ProgressTicks, Is.EqualTo(10_000_000), "half of the 2 s cycle");

            RunCommandResult<UpgradeResult> r = rig.Commands.Upgrade(id);

            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            long newCycle = r.Value.Rates.CycleTicks;
            Assert.That(m.CycleDurationTicks, Is.EqualTo(newCycle));
            MulDiv.MulDivFloor(10_000_000, newCycle, 20_000_000, out long expected);
            Assert.That(m.ProgressTicks, Is.EqualTo(expected), "p' = floor(p x D' / D)");
            Assert.That(m.ProgressTicks, Is.EqualTo(9_259_259));
        }

        [Test]
        public void Upgrade_AtAMilestone_ReportsTheMilestoneOnce_AndTheNextOne()
        {
            var options = new RunCommandFixtures.Options { Milestones = new[] { new SpeedMilestone(3, 2_000_000) } };
            var rig = Rig(options, cash: 10_000);
            MachineDef miner = rig.Catalog.Machines[Miner];
            Guid id = BuildOk(rig, 0, Miner);

            UpgradeResult toTwo = rig.Commands.Upgrade(id).Value;
            Assert.That(toTwo.MilestoneReached, Is.False);
            Assert.That(toTwo.NextReachesMilestone, Is.True, "level 3 is the milestone the next upgrade reaches");

            UpgradeResult toThree = rig.Commands.Upgrade(id).Value;
            Assert.That(toThree.NewLevel, Is.EqualTo(3));
            Assert.That(toThree.MilestoneReached, Is.True);
            Assert.That(toThree.MilestoneLevel, Is.EqualTo(3));
            Assert.That(toThree.MilestoneMultiplierPpm, Is.EqualTo(2_000_000));
            Assert.That(toThree.NextReachesMilestone, Is.False);
            Assert.That(toThree.NextCost, Is.EqualTo(miner.CostTable[3]));
            long cycle = ExpectedCycle(20_000_000, miner.SpeedTable[3]);
            Assert.That(toThree.Rates.CycleTicks, Is.EqualTo(cycle));
            Assert.That(toThree.Rates.Outputs.Single().PerMinuteMicro, Is.GreaterThan(toTwo.Rates.Outputs.Single().PerMinuteMicro * 2),
                "the milestone doubles the rate on top of the level growth");

            UpgradeResult toFour = rig.Commands.Upgrade(id).Value;
            Assert.That(toFour.MilestoneReached, Is.False, "a milestone is reported once, at its level");
        }

        [Test]
        public void Upgrade_AtTheLastLevel_HasNoNextCost()
        {
            var rig = Rig(new RunCommandFixtures.Options { MaxLevel = 2 }, cash: 1000);
            Guid id = BuildOk(rig, 0, Miner);
            UpgradeResult r = rig.Commands.Upgrade(id).Value;
            Assert.That(r.HasNextUpgrade, Is.False);
            Assert.That(r.NextCost, Is.Zero);
            AssertRejected(rig, () => rig.Commands.Upgrade(id), RunReasons.LevelMax);
        }

        [Test]
        public void Upgrade_RejectsWithTheNamedReason_AndChangesNothing()
        {
            // Cash for the build (10) and one Cash short of the upgrade (12).
            var rig = Rig(cash: 10 + 11);
            Guid id = BuildOk(rig, 0, Miner);
            AssertRejected(rig, () => rig.Commands.Upgrade(id), RunReasons.CashInsufficient);
            AssertRejected(rig, () => rig.Commands.Upgrade(rig.Id(77)), RunReasons.CommandInvalid);
        }

        // ------------------------------------------------------------------------------------------------
        // Remove and replace (FR-009, ECO-7)
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Remove_RefundsFloorOfInvestedTimesRefundPpm_FreesTheSlot_AndClosesThePriority()
        {
            var options = new RunCommandFixtures.Options { RefundPpm = 333_333 };
            var rig = Rig(options, cash: 1000);
            MachineDef miner = rig.Catalog.Machines[Miner];
            Guid a = BuildOk(rig, 0, Miner);
            Guid b = BuildOk(rig, 1, Miner);
            Guid c = BuildOk(rig, 2, Miner);
            Assert.That(rig.Commands.Upgrade(b).IsSuccess, Is.True);
            rig.Advance(5_000_000);
            long invested = miner.CostTable[0] + miner.CostTable[1];
            MulDiv.MulDivFloor(invested, 333_333, 1_000_000, out long refund);
            Assert.That(refund, Is.EqualTo(7), "floor(22 x 0.333333)");
            long cash = rig.State.Cash;

            RunCommandResult<RemoveResult> r = rig.Commands.Remove(b, true);

            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            Assert.That(r.Value.Refund, Is.EqualTo(refund));
            Assert.That(r.Value.InvestedCash, Is.EqualTo(invested));
            Assert.That(r.Value.FactoryValueDelta, Is.EqualTo(refund - invested));
            Assert.That(r.Value.SlotIndex, Is.EqualTo(1));
            Assert.That(rig.State.Cash, Is.EqualTo(cash + refund));
            Assert.That(rig.State.FindMachine(b), Is.Null, "the machine and its progress are gone");
            Assert.That(rig.State.MachineAtSlot(1), Is.Null, "the slot is free");
            Assert.That(rig.State.Priority.Ids, Is.EqualTo(new[] { a, c }), "the priority position is closed");
            Assert.That(rig.Commands.Build(1, Smelter).IsSuccess, Is.True, "the freed slot can be built again");
        }

        [Test]
        public void Remove_RejectsWithoutConfirmation_AndForAnUnknownInstance()
        {
            var rig = Rig();
            Guid id = BuildOk(rig, 0, Miner);
            AssertRejected(rig, () => rig.Commands.Remove(id, false), RunReasons.ConfirmationRequired);
            AssertRejected(rig, () => rig.Commands.Remove(rig.Id(77), true), RunReasons.CommandInvalid);
            Assert.That(rig.State.FindMachine(id), Is.Not.Null);
        }

        [Test]
        public void BuildThenRemove_AlwaysLowersFactoryValue_AlsoUnderAnUpgradeCostDiscount()
        {
            foreach (long refundPpm in new long[] { 0, 250_000, 500_000, 999_999 })
            {
                for (int level = 0; level <= 3; level++)
                {
                    var options = new RunCommandFixtures.Options { DiscountResearch = true, RefundPpm = refundPpm };
                    var rig = Rig(options, cash: 1000);
                    Assert.That(rig.State.TrySetResearchLevel(RunCommandFixtures.MachineDiscount, level), Is.EqualTo(RunStateResult.Ok));
                    long before = RunCommandFixtures.FactoryValue(rig.State);

                    BuildResult built = rig.Commands.Build(0, Miner).Value;
                    Assert.That(RunCommandFixtures.FactoryValue(rig.State), Is.EqualTo(before), "building only moves Cash into the machine");
                    Assert.That(built.CostPaid, Is.EqualTo(ExpectedDiscountedCost(10, level)));
                    RemoveResult removed = rig.Commands.Remove(built.InstanceId, true).Value;

                    string context = "refundPpm " + refundPpm + ", discount level " + level;
                    Assert.That(removed.Refund, Is.LessThan(built.CostPaid), context);
                    Assert.That(RunCommandFixtures.FactoryValue(rig.State), Is.LessThan(before), context);
                    Assert.That(before - RunCommandFixtures.FactoryValue(rig.State), Is.EqualTo(built.CostPaid - removed.Refund), context);
                }
            }
        }

        [Test]
        public void Discount_AppliesToBuildUpgradeAndExpansion_WithRoundingUp()
        {
            var options = new RunCommandFixtures.Options { DiscountResearch = true };
            var rig = Rig(options, cash: 100_000);
            rig.State.TrySetResearchLevel(RunCommandFixtures.MachineDiscount, 1);
            rig.State.TrySetResearchLevel(RunCommandFixtures.ExpansionDiscount, 1);
            MachineDef miner = rig.Catalog.Machines[Miner];

            BuildResult built = rig.Commands.Build(0, Miner).Value;
            Assert.That(built.CostPaid, Is.EqualTo(8), "ceil(10 x 0.8)");
            Assert.That(built.NextUpgradeCost, Is.EqualTo(10), "ceil(12 x 0.8) = ceil(9.6)");
            UpgradeResult upgraded = rig.Commands.Upgrade(built.InstanceId).Value;
            Assert.That(upgraded.CostPaid, Is.EqualTo(10));
            Assert.That(rig.State.FindMachine(built.InstanceId).InvestedCash, Is.EqualTo(18), "what was actually paid");

            ExpansionResult storage = rig.Commands.UpgradeStorage().Value;
            Assert.That(storage.CostPaid, Is.EqualTo(2250), "the expansion discount lowers the storage cost");
            Assert.That(storage.NewValue, Is.EqualTo(2000));
            Assert.That(storage.NextCost, Is.EqualTo(9000));
            Assert.That(rig.State.StorageInvestedCash, Is.EqualTo(2250));
            ExpansionResult factory = rig.Commands.UpgradeFactory().Value;
            Assert.That(factory.CostPaid, Is.EqualTo(4500), "and the factory cost");
            Assert.That(factory.NewValue, Is.EqualTo(6));
            Assert.That(rig.State.FactoryInvestedCash, Is.EqualTo(4500));
            Assert.That(miner.CostTable[0], Is.EqualTo(10), "the catalog table itself is untouched");
        }

        [Test]
        public void Replace_IsRemoveThenBuild_RefundFirst_ThenTheNewCost()
        {
            var rig = Rig(cash: 1000);
            Guid miner = BuildOk(rig, 0, Miner);
            Guid other = BuildOk(rig, 1, Smelter);
            long cash = rig.State.Cash;
            long cost = rig.Catalog.Machines[Press].CostTable[0];

            RemoveResult removed = rig.Commands.Remove(miner, true).Value;
            BuildResult built = rig.Commands.Build(0, Press).Value;

            Assert.That(rig.State.Cash, Is.EqualTo(cash + removed.Refund - cost));
            Assert.That(rig.State.MachineAtSlot(0).MachineId, Is.EqualTo(Press));
            Assert.That(built.InstanceId, Is.Not.EqualTo(miner));
            Assert.That(rig.State.Priority.Ids, Is.EqualTo(new[] { other, built.InstanceId }), "the replacement joins the priority last");
        }

        // ------------------------------------------------------------------------------------------------
        // SetActive, ReorderPriority, SetAutoSell, expansion
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void SetActive_TogglesTheMachine_AndKeepsItsProgress()
        {
            var rig = Rig();
            Guid id = BuildOk(rig, 0, Miner);
            rig.Advance(5_000_000);

            Assert.That(rig.Commands.SetActive(id, false).Value, Is.False);
            MachineInstance m = rig.State.FindMachine(id);
            Assert.That(m.IsActive, Is.False);
            rig.Advance(5_000_000);
            Assert.That(m.ProgressTicks, Is.EqualTo(5_000_000), "an inactive machine does not progress and keeps its progress");

            Assert.That(rig.Commands.SetActive(id, true).Value, Is.True);
            rig.Advance(5_000_000);
            Assert.That(m.ProgressTicks, Is.EqualTo(10_000_000));
            AssertRejected(rig, () => rig.Commands.SetActive(rig.Id(77), true), RunReasons.CommandInvalid);
        }

        [Test]
        public void ReorderPriority_ReplacesTheOrder_AndTheOrderDecidesACompetitionForScarceInput()
        {
            foreach (bool secondFirst in new[] { false, true })
            {
                var rig = Rig();
                Guid a = BuildOk(rig, 0, Smelter);
                Guid b = BuildOk(rig, 1, Smelter);
                RunSimHarness.Stock(rig.State, "iron.ore", 2);
                Guid[] order = secondFirst ? new[] { b, a } : new[] { a, b };
                Assert.That(rig.Commands.ReorderPriority(order).IsSuccess, Is.True);
                Assert.That(rig.State.Priority.Ids, Is.EqualTo(order));

                rig.Advance(20_000_000);

                Assert.That(rig.State.Inventory.Units("iron.bar"), Is.EqualTo(1), "2 Ore feed exactly one Smelter");
                MachineInstance winner = rig.State.FindMachine(order[0]);
                MachineInstance loser = rig.State.FindMachine(order[1]);
                Assert.That(winner.ProgressTicks, Is.Zero, "the first in the saved priority completed");
                Assert.That(loser.IsWaiting, Is.True, "the other waits at full progress, whatever its slot");
            }
        }

        [Test]
        public void ReorderPriority_RejectsANonPermutation_AndChangesNothing()
        {
            var rig = Rig();
            Guid a = BuildOk(rig, 0, Miner);
            Guid b = BuildOk(rig, 1, Miner);
            AssertRejected(rig, () => rig.Commands.ReorderPriority(null), RunReasons.PriorityNotPermutation);
            AssertRejected(rig, () => rig.Commands.ReorderPriority(new[] { a }), RunReasons.PriorityNotPermutation);
            AssertRejected(rig, () => rig.Commands.ReorderPriority(new[] { a, a }), RunReasons.PriorityNotPermutation);
            AssertRejected(rig, () => rig.Commands.ReorderPriority(new[] { a, rig.Id(77) }), RunReasons.PriorityNotPermutation);
            AssertRejected(rig, () => rig.Commands.ReorderPriority(new[] { a, b, b }), RunReasons.PriorityNotPermutation);
        }

        [Test]
        public void SetAutoSell_ChangesOnlyTheSetting_ThenSellsTheStockAtOnce()
        {
            var rig = Rig(cash: 50);
            RunSimHarness.Stock(rig.State, "iron.bar", 4);
            Assert.That(rig.Commands.SetAutoSell("iron.bar", true).Value, Is.True);
            Assert.That(rig.State.IsAutoSell("iron.bar"), Is.True);
            Assert.That(rig.State.Inventory.Units("iron.bar"), Is.Zero, "the settle sells the whole stock");
            Assert.That(rig.State.Cash, Is.EqualTo(50 + 4 * 3));

            Assert.That(rig.Commands.SetAutoSell("iron.bar", false).Value, Is.False);
            Assert.That(rig.State.IsAutoSell("iron.bar"), Is.False);
            Assert.That(rig.State.Cash, Is.EqualTo(62));
        }

        [Test]
        public void SetAutoSell_RejectsALockedOrUnknownResource()
        {
            var rig = Rig(lockedResources: new[] { "iron.gear" });
            AssertRejected(rig, () => rig.Commands.SetAutoSell("iron.gear", true), RunReasons.ResourceLocked);
            AssertRejected(rig, () => rig.Commands.SetAutoSell("resource.unknown", true), RunReasons.ResourceLocked);
            AssertRejected(rig, () => rig.Commands.SetAutoSell(null, true), RunReasons.ResourceLocked);
        }

        [Test]
        public void UpgradeStorage_AndFactory_RaiseTheLevels_AndRejectAtTheEndOrWithoutCash()
        {
            var rig = Rig(cash: 100_000);
            ExpansionResult storage = rig.Commands.UpgradeStorage().Value;
            Assert.That(storage.NewLevel, Is.EqualTo(2));
            Assert.That(storage.NewValue, Is.EqualTo(2000));
            Assert.That(storage.CostPaid, Is.EqualTo(2500));
            Assert.That(storage.NextCost, Is.EqualTo(10_000));
            Assert.That(rig.State.Inventory.Capacity, Is.EqualTo(2000));
            Assert.That(rig.State.StorageInvestedCash, Is.EqualTo(2500));

            ExpansionResult factory = rig.Commands.UpgradeFactory().Value;
            Assert.That(factory.NewLevel, Is.EqualTo(2));
            Assert.That(factory.NewValue, Is.EqualTo(6));
            Assert.That(factory.CostPaid, Is.EqualTo(5000));
            Assert.That(rig.State.SlotCount, Is.EqualTo(6));
            Assert.That(rig.State.FactoryInvestedCash, Is.EqualTo(5000));

            var maxed = Rig(cash: 1_000_000, storageLevel: 5, factoryLevel: 5);
            AssertRejected(maxed, () => maxed.Commands.UpgradeStorage(), RunReasons.LevelMax);
            AssertRejected(maxed, () => maxed.Commands.UpgradeFactory(), RunReasons.LevelMax);

            var poor = Rig(cash: 2499);
            AssertRejected(poor, () => poor.Commands.UpgradeStorage(), RunReasons.CashInsufficient);
            AssertRejected(poor, () => poor.Commands.UpgradeFactory(), RunReasons.CashInsufficient);
        }

        // ------------------------------------------------------------------------------------------------
        // Rule: lifecycle.blocked (T099 review, item 3)
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void EveryCommand_RejectsWithLifecycleBlocked_AndChangesNothing()
        {
            var rig = Rig(cash: 100_000);
            Guid a = BuildOk(rig, 0, Miner);
            Guid b = BuildOk(rig, 1, Miner);
            rig.Session.SetLifecycleBlocked(true);

            AssertRejected(rig, () => rig.Commands.Build(2, Miner), RunReasons.LifecycleBlocked);
            AssertRejected(rig, () => rig.Commands.Upgrade(a), RunReasons.LifecycleBlocked);
            AssertRejected(rig, () => rig.Commands.Remove(a, true), RunReasons.LifecycleBlocked);
            AssertRejected(rig, () => rig.Commands.SetActive(a, false), RunReasons.LifecycleBlocked);
            AssertRejected(rig, () => rig.Commands.ReorderPriority(new[] { b, a }), RunReasons.LifecycleBlocked);
            AssertRejected(rig, () => rig.Commands.SetAutoSell("iron.ore", true), RunReasons.LifecycleBlocked);
            AssertRejected(rig, () => rig.Commands.UpgradeStorage(), RunReasons.LifecycleBlocked);
            AssertRejected(rig, () => rig.Commands.UpgradeFactory(), RunReasons.LifecycleBlocked);
            // The lifecycle check comes first, before any other reason.
            AssertRejected(rig, () => rig.Commands.Build(99, "machine.unknown"), RunReasons.LifecycleBlocked);
            AssertRejected(rig, () => rig.Commands.Remove(rig.Id(77), false), RunReasons.LifecycleBlocked);

            rig.Session.SetLifecycleBlocked(false);
            Assert.That(rig.Commands.Upgrade(a).IsSuccess, Is.True, "commands work again once the lifecycle is released");
        }

        // ------------------------------------------------------------------------------------------------
        // Rule: the structural flag of every command (T099 review, item 2)
        // ------------------------------------------------------------------------------------------------

        private static void AssertRevisions(RunCommandFixtures.Rig rig, Action command, long structure, long quantity, string context)
        {
            long s = rig.Session.StructureRevision, q = rig.Session.QuantityRevision;
            command();
            Assert.That(rig.Session.StructureRevision - s, Is.EqualTo(structure), context + ": StructureRevision");
            Assert.That(rig.Session.QuantityRevision - q, Is.EqualTo(quantity), context + ": QuantityRevision");
        }

        [Test]
        public void EveryCommand_ReportsStructureAndQuantityChangesCorrectly()
        {
            var rig = Rig(cash: 100_000);
            Guid a = default, b = default;

            AssertRevisions(rig, () => a = rig.Commands.Build(0, Miner).Value.InstanceId, 1, 1, "Build");
            AssertRevisions(rig, () => b = rig.Commands.Build(1, Smelter).Value.InstanceId, 1, 1, "Build a second machine");
            AssertRevisions(rig, () => Assert.That(rig.Commands.Upgrade(a).IsSuccess, Is.True), 1, 1, "Upgrade (level, Cash and cycle)");

            AssertRevisions(rig, () => rig.Commands.SetActive(b, false), 1, 0, "SetActive changes the active flag");
            AssertRevisions(rig, () => rig.Commands.SetActive(b, false), 0, 0, "SetActive to the same value changes nothing");
            AssertRevisions(rig, () => rig.Commands.SetActive(b, true), 1, 0, "SetActive back on");

            AssertRevisions(rig, () => rig.Commands.ReorderPriority(new[] { b, a }), 1, 0, "ReorderPriority changes the order");
            AssertRevisions(rig, () => rig.Commands.ReorderPriority(new[] { b, a }), 0, 0, "ReorderPriority to the same order changes nothing");

            AssertRevisions(rig, () => rig.Commands.SetAutoSell("iron.ore", true), 1, 0, "SetAutoSell without stock is structural only");
            AssertRevisions(rig, () => rig.Commands.SetAutoSell("iron.ore", true), 0, 0, "SetAutoSell to the same value changes nothing");
            RunSimHarness.Stock(rig.State, "iron.bar", 5);
            AssertRevisions(rig, () => rig.Commands.SetAutoSell("iron.bar", true), 1, 1, "SetAutoSell with stock also sells");

            AssertRevisions(rig, () => rig.Commands.UpgradeStorage(), 1, 1, "UpgradeStorage");
            AssertRevisions(rig, () => rig.Commands.UpgradeFactory(), 1, 1, "UpgradeFactory");
            AssertRevisions(rig, () => rig.Commands.Remove(a, true), 1, 1, "Remove");
        }

        // ------------------------------------------------------------------------------------------------
        // Rule: a rejection never changes state, so the settle is pre-checked (T099 review, item 1)
        // ------------------------------------------------------------------------------------------------

        private static RunCommandFixtures.Rig NearBound(long headroom)
        {
            var rig = Rig(cash: ExactBound.Value - headroom);
            return rig;
        }

        [Test]
        public void SetAutoSell_NearTheCashBound_RejectsBeforeMutating_AndAcceptsTheExactFit()
        {
            var tight = NearBound(5);
            RunSimHarness.Stock(tight.State, "iron.ore", 10);
            AssertRejected(tight, () => tight.Commands.SetAutoSell("iron.ore", true), RunReasons.ValueLimit);
            Assert.That(tight.State.IsAutoSell("iron.ore"), Is.False, "the setting was not applied");
            Assert.That(tight.State.Inventory.Units("iron.ore"), Is.EqualTo(10), "nothing was sold");

            var exact = NearBound(10);
            RunSimHarness.Stock(exact.State, "iron.ore", 10);
            Assert.That(exact.Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            Assert.That(exact.State.Cash, Is.EqualTo(ExactBound.Value), "10 Ore sell for exactly the headroom");
            Assert.That(exact.State.Inventory.Units("iron.ore"), Is.Zero);
        }

        [Test]
        public void Build_NearTheCashBound_WithAnAutoSoldStock_RejectsBeforeCharging()
        {
            // The state is handed in with stock of an auto-sell resource, so the settle after the command sells it.
            // Cash after the build is bound - 20 (cost 10): 21 Ore overflow, 20 Ore fit exactly.
            var tight = NearBound(10);
            tight.State.TrySetAutoSell("iron.ore", true);
            RunSimHarness.Stock(tight.State, "iron.ore", 21);
            AssertRejected(tight, () => tight.Commands.Build(0, Miner), RunReasons.ValueLimit);
            Assert.That(tight.State.Machines, Is.Empty, "no machine was built");
            Assert.That(tight.State.Cash, Is.EqualTo(ExactBound.Value - 10), "nothing was charged");

            var exact = NearBound(10);
            exact.State.TrySetAutoSell("iron.ore", true);
            RunSimHarness.Stock(exact.State, "iron.ore", 20);
            Assert.That(exact.Commands.Build(0, Miner).IsSuccess, Is.True);
            Assert.That(exact.State.Cash, Is.EqualTo(ExactBound.Value));
        }

        [Test]
        public void SetActive_OfAWaitingMachine_NearTheCashBound_RejectsBeforeSwitchingOn()
        {
            RunCommandFixtures.Rig Make(long headroom)
            {
                var rig = NearBound(headroom);
                Assert.That(rig.State.TrySetAutoSell("iron.bar", true), Is.EqualTo(RunStateResult.Ok));
                RunSimHarness.Add(rig.State, Smelter, 0, 1, progress: long.MaxValue, active: false);
                RunSimHarness.Stock(rig.State, "iron.ore", 2);
                Assert.That(rig.State.Machines[0].IsWaiting, Is.True);
                return rig;
            }

            // Switching on completes the waiting cycle at once: 2 Ore become 1 Bar, sold for $3.
            var tight = Make(2);
            AssertRejected(tight, () => tight.Commands.SetActive(RunSimHarness.Id(1), true), RunReasons.ValueLimit);
            Assert.That(tight.State.Machines[0].IsActive, Is.False);
            Assert.That(tight.State.Inventory.Units("iron.ore"), Is.EqualTo(2));

            var exact = Make(3);
            Assert.That(exact.Commands.SetActive(RunSimHarness.Id(1), true).IsSuccess, Is.True);
            Assert.That(exact.State.Cash, Is.EqualTo(ExactBound.Value));
            Assert.That(exact.State.Inventory.Units("iron.ore"), Is.Zero);
        }

        [Test]
        public void Remove_WhoseRefundWouldOverflowCash_Rejects_AndKeepsTheMachine()
        {
            var tight = NearBound(0);
            RunSimHarness.Add(tight.State, Miner, 0, 1);
            AssertRejected(tight, () => tight.Commands.Remove(RunSimHarness.Id(1), true), RunReasons.ValueLimit);
            Assert.That(tight.State.Machines, Has.Count.EqualTo(1));

            var exact = NearBound(5);
            RunSimHarness.Add(exact.State, Miner, 0, 1);
            Assert.That(exact.Commands.Remove(RunSimHarness.Id(1), true).IsSuccess, Is.True, "the harness machine holds 10, refund 5");
            Assert.That(exact.State.Cash, Is.EqualTo(ExactBound.Value));
        }

        /// <summary>
        /// Sweeps Cash from the bound downwards over a state with an auto-sell stock and a waiting machine, for every
        /// command: whatever the headroom, a command either succeeds or is rejected with the state exactly as before.
        /// A settle overflow after a mutation would show as a rejection with a changed state.
        /// </summary>
        [Test]
        public void EveryCommand_IsAtomic_AtEveryHeadroomNearTheCashBound()
        {
            var commands = new Dictionary<string, Func<RunCommandFixtures.Rig, RunCommandResult<int>>>
            {
                ["Build"] = rig => Wrap(rig.Commands.Build(2, Miner)),
                ["Upgrade waiting"] = rig => Wrap(rig.Commands.Upgrade(RunSimHarness.Id(1))),
                ["Upgrade other"] = rig => Wrap(rig.Commands.Upgrade(RunSimHarness.Id(2))),
                ["Remove waiting"] = rig => Wrap(rig.Commands.Remove(RunSimHarness.Id(1), true)),
                ["Remove other"] = rig => Wrap(rig.Commands.Remove(RunSimHarness.Id(2), true)),
                ["SetActive on"] = rig => Wrap(rig.Commands.SetActive(RunSimHarness.Id(1), true)),
                ["SetActive off"] = rig => Wrap(rig.Commands.SetActive(RunSimHarness.Id(2), false)),
                ["Reorder"] = rig => rig.Commands.ReorderPriority(new[] { RunSimHarness.Id(2), RunSimHarness.Id(1) }),
                ["AutoSell plate on"] = rig => Wrap(rig.Commands.SetAutoSell("iron.plate", true)),
                ["AutoSell ore off"] = rig => Wrap(rig.Commands.SetAutoSell("iron.ore", false)),
                ["UpgradeStorage"] = rig => Wrap(rig.Commands.UpgradeStorage()),
                ["UpgradeFactory"] = rig => Wrap(rig.Commands.UpgradeFactory()),
            };

            foreach (KeyValuePair<string, Func<RunCommandFixtures.Rig, RunCommandResult<int>>> command in commands)
            {
                for (long headroom = 0; headroom <= 40; headroom++)
                {
                    var rig = NearBound(headroom);
                    rig.State.TrySetAutoSell("iron.ore", true);
                    rig.State.TrySetAutoSell("iron.bar", true);
                    RunSimHarness.Add(rig.State, Smelter, 0, 1, progress: long.MaxValue, active: false);
                    RunSimHarness.Add(rig.State, Miner, 1, 2);
                    RunSimHarness.Stock(rig.State, "iron.ore", 6);
                    RunSimHarness.Stock(rig.State, "iron.plate", 3);
                    string before = RunCommandFixtures.Snapshot(rig.State);
                    long structure = rig.Session.StructureRevision, quantity = rig.Session.QuantityRevision;

                    RunCommandResult<int> result = command.Value(rig);

                    string context = command.Key + " at headroom " + headroom;
                    if (result.IsSuccess)
                    {
                        Assert.That(rig.State.Validate(), Is.EqualTo(RunStateResult.Ok), context);
                        Assert.That(rig.State.Cash, Is.LessThanOrEqualTo(ExactBound.Value), context);
                    }
                    else
                    {
                        Assert.That(RunCommandFixtures.Snapshot(rig.State), Is.EqualTo(before), context + ": a rejection never changes state");
                        Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure), context);
                        Assert.That(rig.Session.QuantityRevision, Is.EqualTo(quantity), context);
                    }
                }
            }
        }

        private static RunCommandResult<int> Wrap<T>(RunCommandResult<T> r) =>
            r.IsSuccess ? RunCommandResult.Success(0) : RunCommandResult.Reject<int>(r.ReasonCode);

        // ------------------------------------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------------------------------------

        private static long ExpectedCycle(long durationTicks, long speedPpm)
        {
            Assert.That(MulDiv.MulDivCeil(durationTicks, 1_000_000, speedPpm, out long cycle), Is.True);
            return cycle;
        }

        private static long ExpectedRate(long unitsPerCycle, long cycleTicks)
        {
            Assert.That(MulDiv.MulDivFloor(unitsPerCycle, 600_000_000_000_000L, cycleTicks, out long rate), Is.True);
            return rate;
        }

        private static long ExpectedDiscountedCost(long cost, int levels)
        {
            for (int i = 0; i < levels; i++)
            {
                Assert.That(MulDiv.MulDivCeil(cost, 800_000, 1_000_000, out cost), Is.True);
            }
            return cost;
        }
    }
}
