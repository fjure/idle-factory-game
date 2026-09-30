using System;
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
    /// The read models of T137 (contracts/run-application.md, contracts/ui-surfaces.md, FR-002, FR-011, UX-8, UX-10,
    /// OD-9): the 62.5 % Press/Assembler bottleneck in <see cref="RatesSnapshot"/>, every blocker with its numbers and
    /// corrective actions (auto-sell named as the cause), the last completed <c>k'</c> of <c>k</c> with its limit,
    /// three significant digits, and <see cref="RunFactorySnapshot"/> and <see cref="EconomySnapshot"/>.
    /// </summary>
    public sealed class RatesSnapshotTests
    {
        private const string Miner = RunCommandFixtures.Miner;
        private const string Smelter = RunCommandFixtures.Smelter;
        private const string Press = RunCommandFixtures.Press;
        private const string Assembler = RunCommandFixtures.Assembler;

        private static RunCommandFixtures.Rig Rig(RunCommandFixtures.Options o = null, long cash = 100_000, int storageLevel = 1,
            string[] unlockedMachines = null) => RunCommandFixtures.NewRig(o, cash, storageLevel, 1, unlockedMachines);

        private static Guid BuildOk(RunCommandFixtures.Rig rig, int slot, string machineId)
        {
            RunCommandResult<BuildResult> r = rig.Commands.Build(slot, machineId);
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            return r.Value.InstanceId;
        }

        private static ResourceRates Resource(RatesSnapshot s, string id) => s.Resources.Single(r => r.ResourceId == id);

        private static MachineDiagnosis Machine(RatesSnapshot s, Guid id) => s.Machines.Single(m => m.InstanceId == id);

        private static MachineBlockerInfo Blocker(MachineDiagnosis m, MachineBlocker kind) => m.Blockers.Single(b => b.Kind == kind);

        // ------------------------------------------------------------------------------------------------
        // FR-002: the first bottleneck is visible
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void OnePress_SuppliesSixtyTwoPointFivePercentOfOneAssemblersPlateDemand()
        {
            var rig = Rig();
            Guid press = BuildOk(rig, 0, Press);
            Guid assembler = BuildOk(rig, 1, Assembler);

            RatesSnapshot snapshot = RunSnapshots.Rates(rig.Session);

            ResourceRates plate = Resource(snapshot, "iron.plate");
            Assert.That(plate.ProductionPerMinuteMicro, Is.EqualTo(15_000_000), "1 Plate per 4 s");
            Assert.That(plate.ConsumptionPerMinuteMicro, Is.EqualTo(24_000_000), "2 Plates per 5 s");
            Assert.That(plate.NetPerMinuteMicro, Is.EqualTo(-9_000_000));
            Assert.That(plate.HasDemand, Is.True);
            Assert.That(plate.SupplyCoveragePpm, Is.EqualTo(625_000), "62.5 %");
            Assert.That(plate.ProductionText, Is.EqualTo("15"));
            Assert.That(plate.ConsumptionText, Is.EqualTo("24"));
            Assert.That(plate.NetText, Is.EqualTo("-9"));

            ResourceRates bar = Resource(snapshot, "iron.bar");
            Assert.That(bar.ConsumptionPerMinuteMicro, Is.EqualTo(30_000_000), "the Press takes 2 Bars per 4 s");
            Assert.That(bar.ProductionPerMinuteMicro, Is.Zero);
            ResourceRates gear = Resource(snapshot, "iron.gear");
            Assert.That(gear.ProductionPerMinuteMicro, Is.EqualTo(12_000_000), "1 Gear per 5 s");
            Assert.That(gear.HasDemand, Is.False);
            Assert.That(gear.SupplyCoveragePpm, Is.Zero);

            // The blockers explain the shortage and offer choices without picking one.
            MachineBlockerInfo missing = Blocker(Machine(snapshot, assembler), MachineBlocker.MissingInput);
            Assert.That(missing.ResourceId, Is.EqualTo("iron.plate"));
            Assert.That(missing.Required, Is.EqualTo(2));
            Assert.That(missing.Available, Is.Zero);
            Assert.That(missing.AutoSold, Is.False);
            Assert.That(missing.Actions.Select(a => (a.Kind, a.MachineId)),
                Is.EqualTo(new[] { (CorrectiveActionKind.BuildProducer, Press) }));
            Assert.That(Machine(snapshot, press).PrimaryBlocker, Is.EqualTo(MachineBlocker.MissingInput));
        }

        [Test]
        public void ASecondPress_CoversTheAssemblersDemand()
        {
            var rig = Rig();
            BuildOk(rig, 0, Press);
            BuildOk(rig, 1, Press);
            BuildOk(rig, 2, Assembler);

            ResourceRates plate = Resource(RunSnapshots.Rates(rig.Session), "iron.plate");

            Assert.That(plate.ProductionPerMinuteMicro, Is.EqualTo(30_000_000));
            Assert.That(plate.NetPerMinuteMicro, Is.EqualTo(6_000_000));
            Assert.That(plate.SupplyCoveragePpm, Is.EqualTo(1_250_000), "125 %: the bottleneck is gone");
        }

        [Test]
        public void AnInactiveMachine_ContributesNoRate_AndOffersToActivate()
        {
            var rig = Rig();
            Guid miner = BuildOk(rig, 0, Miner);
            Assert.That(Resource(RunSnapshots.Rates(rig.Session), "iron.ore").ProductionPerMinuteMicro, Is.EqualTo(60_000_000));

            Assert.That(rig.Commands.SetActive(miner, false).IsSuccess, Is.True);
            RatesSnapshot snapshot = RunSnapshots.Rates(rig.Session);

            Assert.That(Resource(snapshot, "iron.ore").ProductionPerMinuteMicro, Is.Zero);
            MachineDiagnosis diagnosis = Machine(snapshot, miner);
            Assert.That(diagnosis.Phase, Is.EqualTo(MachinePhase.Paused));
            Assert.That(diagnosis.PrimaryBlocker, Is.EqualTo(MachineBlocker.Inactive));
            MachineBlockerInfo inactive = Blocker(diagnosis, MachineBlocker.Inactive);
            Assert.That(inactive.Actions.Single().Kind, Is.EqualTo(CorrectiveActionKind.ActivateMachine));
            Assert.That(inactive.Actions.Single().InstanceId, Is.EqualTo(miner));
        }

        // ------------------------------------------------------------------------------------------------
        // UX-10: auto-sell named as the cause of a missing input
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void AMissingInput_NamesAutoSellAsTheCause_AndOffersTheToggle()
        {
            var rig = Rig();
            BuildOk(rig, 0, Miner);
            BuildOk(rig, 1, Smelter);
            Guid press = BuildOk(rig, 2, Press);
            Assert.That(rig.Commands.SetAutoSell("iron.bar", true).IsSuccess, Is.True);
            rig.Advance(100_000_000);

            Assert.That(rig.State.Inventory.Units("iron.bar"), Is.Zero, "every Bar is sold the moment it exists");
            RatesSnapshot snapshot = RunSnapshots.Rates(rig.Session);
            MachineBlockerInfo missing = Blocker(Machine(snapshot, press), MachineBlocker.MissingInput);
            Assert.That(missing.ResourceId, Is.EqualTo("iron.bar"));
            Assert.That(missing.Required, Is.EqualTo(2));
            Assert.That(missing.Available, Is.Zero);
            Assert.That(missing.AutoSold, Is.True, "auto-sell of Bars is why the Press starves");
            Assert.That(missing.Actions.Any(a => a.Kind == CorrectiveActionKind.DisableAutoSell && a.ResourceId == "iron.bar"), Is.True);
            Assert.That(missing.Actions.Any(a => a.Kind == CorrectiveActionKind.BuildProducer && a.MachineId == Smelter), Is.True);
            Assert.That(Resource(snapshot, "iron.bar").AutoSold, Is.True);

            // Following the offered action removes the cause: the Bars stay, and the shortage is a plain one.
            Assert.That(rig.Commands.SetAutoSell("iron.bar", false).IsSuccess, Is.True);
            RunSimHarness.Stock(rig.State, "iron.bar", 1);
            MachineBlockerInfo plain = Blocker(Machine(RunSnapshots.Rates(rig.Session), press), MachineBlocker.MissingInput);
            Assert.That(plain.AutoSold, Is.False);
            Assert.That(plain.Available, Is.EqualTo(1));
            Assert.That(plain.Required, Is.EqualTo(2));
        }

        [Test]
        public void AShortInput_ReportsTheAmountOneRecipeUnitNeedsAndTheStockThatIsThere()
        {
            var rig = Rig();
            Guid smelter = BuildOk(rig, 0, Smelter);
            RunSimHarness.Stock(rig.State, "iron.ore", 1);

            MachineBlockerInfo missing = Blocker(Machine(RunSnapshots.Rates(rig.Session), smelter), MachineBlocker.MissingInput);

            Assert.That(missing.ResourceId, Is.EqualTo("iron.ore"));
            Assert.That(missing.Available, Is.EqualTo(1));
            Assert.That(missing.Required, Is.EqualTo(2));
            Assert.That(missing.AutoSold, Is.False);
        }

        // ------------------------------------------------------------------------------------------------
        // Store: StorageFull only when not even one recipe unit fits (US1 scenario 4, OD-9)
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void StorageFull_IsReportedOnlyWhenNoRecipeUnitFits_WithNetRequiredAndFree()
        {
            var rig = Rig();
            Guid miner = BuildOk(rig, 0, Miner);
            Guid smelter = BuildOk(rig, 1, Smelter);
            RunSimHarness.Stock(rig.State, "iron.ore", 999);

            RatesSnapshot snapshot = RunSnapshots.Rates(rig.Session);

            MachineDiagnosis blockedMiner = Machine(snapshot, miner);
            MachineBlockerInfo full = Blocker(blockedMiner, MachineBlocker.StorageFull);
            Assert.That(full.NetRequired, Is.EqualTo(2), "a Miner adds 2 units per recipe unit");
            Assert.That(full.Free, Is.EqualTo(1));
            Assert.That(blockedMiner.PrimaryBlocker, Is.EqualTo(MachineBlocker.StorageFull));
            Assert.That(blockedMiner.Phase, Is.EqualTo(MachinePhase.Paused));
            Assert.That(full.Actions.Select(a => a.Kind),
                Is.EqualTo(new[] { CorrectiveActionKind.EnableAutoSell, CorrectiveActionKind.UpgradeStorage }));
            Assert.That(full.Actions.First().ResourceId, Is.EqualTo("iron.ore"));

            MachineDiagnosis smelting = Machine(snapshot, smelter);
            Assert.That(smelting.Blockers, Is.Empty, "2 Ore become 1 Bar: net -1 fits even in a full store");
            Assert.That(smelting.Phase, Is.EqualTo(MachinePhase.Running));
            Assert.That(snapshot.StoreUsed, Is.EqualTo(999));
            Assert.That(snapshot.StoreCapacity, Is.EqualTo(1000));
            Assert.That(snapshot.StoreFree, Is.EqualTo(1));
            Assert.That(snapshot.StoreUsagePpm, Is.EqualTo(999_000));
        }

        [Test]
        public void AFullStore_LetsTheNetFittingConversionFinish_WhileAnInputFreeMinerStaysBlocked()
        {
            var rig = Rig();
            Guid miner = BuildOk(rig, 0, Miner);
            BuildOk(rig, 1, Smelter);
            RunSimHarness.Stock(rig.State, "iron.ore", 1000);

            rig.Advance(20_000_000);

            Assert.That(rig.State.Inventory.Units("iron.bar"), Is.EqualTo(1));
            Assert.That(rig.State.Inventory.Units("iron.ore"), Is.EqualTo(998));
            Assert.That(rig.State.FindMachine(miner).ProgressTicks, Is.Zero, "the Miner never ran");
            RatesSnapshot snapshot = RunSnapshots.Rates(rig.Session);
            Assert.That(Blocker(Machine(snapshot, miner), MachineBlocker.StorageFull).Free, Is.EqualTo(1));
        }

        [Test]
        public void AMinerWithRoomForOneUnit_HasNoBlocker()
        {
            var rig = Rig();
            Guid miner = BuildOk(rig, 0, Miner);
            RunSimHarness.Stock(rig.State, "iron.ore", 998);

            Assert.That(Machine(RunSnapshots.Rates(rig.Session), miner).Blockers, Is.Empty);
        }

        // ------------------------------------------------------------------------------------------------
        // OD-9: the last completed k' of k with its limit
        // ------------------------------------------------------------------------------------------------

        private static RunCommandFixtures.Options Batched() => new RunCommandFixtures.Options
        {
            Durations = new long[] { 1_000_000, 1_000_000, 40_000_000, 50_000_000 },
            Milestones = new[] { new SpeedMilestone(2, 3_000_000) },
        };

        [Test]
        public void APartialBatch_ReportsKPrimeOfK_WithTheLimitingInput()
        {
            var rig = Rig(Batched());
            Guid smelter = BuildOk(rig, 0, Smelter);
            UpgradeResult upgraded = rig.Commands.Upgrade(smelter).Value;
            Assert.That(upgraded.Rates.BatchFactor, Is.EqualTo(4), "3.24x speed on a 0.1 s recipe batches 4 units");
            Assert.That(upgraded.Rates.CycleTicks, Is.EqualTo(1_234_568));

            RatesSnapshot before = RunSnapshots.Rates(rig.Session);
            Assert.That(Machine(before, smelter).LastCompletion.HasCompleted, Is.False, "nothing has completed yet");
            MachineRates rates = Machine(before, smelter).Rates;
            Assert.That(rates.Inputs.Single().UnitsPerCycle, Is.EqualTo(8));
            Assert.That(rates.Outputs.Single().UnitsPerCycle, Is.EqualTo(4));
            MulDiv.MulDivFloor(8, 600_000_000_000_000L, 1_234_568, out long expectedInputRate);
            Assert.That(rates.Inputs.Single().PerMinuteMicro, Is.EqualTo(expectedInputRate), "batches are included in the rates");

            RunSimHarness.Stock(rig.State, "iron.ore", 3);
            rig.Advance(1_234_568);

            RatesSnapshot after = RunSnapshots.Rates(rig.Session);
            MachineCompletion last = Machine(after, smelter).LastCompletion;
            Assert.That(last.HasCompleted, Is.True);
            Assert.That(last.Units, Is.EqualTo(1), "3 Ore feed one recipe unit");
            Assert.That(last.BatchFactor, Is.EqualTo(4));
            Assert.That(last.IsPartial, Is.True);
            Assert.That(last.LimitKind, Is.EqualTo(CompletionLimitKind.Input));
            Assert.That(last.LimitResourceId, Is.EqualTo("iron.ore"));
            Assert.That(rig.State.Inventory.Units("iron.bar"), Is.EqualTo(1));
            MachineBlockerInfo missing = Blocker(Machine(after, smelter), MachineBlocker.MissingInput);
            Assert.That(missing.Available, Is.EqualTo(1));
        }

        [Test]
        public void APartialBatch_ReportsTheStoreWhenSpaceLimitsIt()
        {
            var rig = Rig(Batched());
            Guid miner = BuildOk(rig, 0, Miner);
            Assert.That(rig.Commands.Upgrade(miner).Value.Rates.BatchFactor, Is.EqualTo(4));
            RunSimHarness.Stock(rig.State, "iron.gear", 995);

            rig.Advance(1_234_568);

            MachineCompletion last = Machine(RunSnapshots.Rates(rig.Session), miner).LastCompletion;
            Assert.That(last.HasCompleted, Is.True);
            Assert.That(last.Units, Is.EqualTo(2), "5 free units hold 2 recipe units of 2 Ore");
            Assert.That(last.BatchFactor, Is.EqualTo(4));
            Assert.That(last.IsPartial, Is.True);
            Assert.That(last.LimitKind, Is.EqualTo(CompletionLimitKind.Store));
            Assert.That(last.LimitResourceId, Is.Null);
            Assert.That(rig.State.Inventory.Units("iron.ore"), Is.EqualTo(4));
        }

        [Test]
        public void AFullBatch_IsNotPartial_AndTheLastCompletionSurvivesLaterAdvances()
        {
            var rig = Rig(Batched());
            Guid miner = BuildOk(rig, 0, Miner);
            rig.Commands.Upgrade(miner);
            rig.Advance(1_234_568);

            MachineCompletion full = Machine(RunSnapshots.Rates(rig.Session), miner).LastCompletion;
            Assert.That(full.HasCompleted, Is.True);
            Assert.That(full.Units, Is.EqualTo(4));
            Assert.That(full.IsPartial, Is.False);
            Assert.That(full.LimitKind, Is.EqualTo(CompletionLimitKind.None));

            // An advance without a completion of this machine keeps the record; removing it forgets it.
            rig.Advance(1000);
            Assert.That(Machine(RunSnapshots.Rates(rig.Session), miner).LastCompletion.HasCompleted, Is.True);
            Assert.That(rig.Commands.Remove(miner, true).IsSuccess, Is.True);
            Assert.That(rig.Session.TryGetLastCompletion(miner, out _, out _, out _), Is.False);
        }

        // ------------------------------------------------------------------------------------------------
        // Three significant digits (R06, UX-8)
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void RateFormat_FloorsToThreeSignificantDigits()
        {
            var cases = new (long micro, string text)[]
            {
                (0, "0"), (15_000_000, "15"), (24_000_000, "24"), (-9_000_000, "-9"), (4_687_500, "4.68"),
                (62_500, "0.0625"), (999_999, "0.999"), (1, "0.000001"), (10_000_000, "10"), (999_999_999, "999"),
                (1_234_000_000, "1.23K"), (123_456_789_000_000, "123M"), (1_000_000_000_000_000_000, "1T"),
                (-4_687_500, "-4.68"),
            };
            foreach ((long micro, string text) c in cases)
                Assert.That(RateFormat.Format(c.micro), Is.EqualTo(c.text), "micro " + c.micro);
        }

        [Test]
        public void TheRateText_NeverOverstatesTheExactRate()
        {
            for (long micro = 1; micro < 2_000_000_000_000L; micro = micro * 7 / 3 + 11)
            {
                string text = RateFormat.Format(micro);
                double shown = Parse(text);
                Assert.That(shown, Is.LessThanOrEqualTo(micro / 1e6 + 1e-9), text + " for " + micro);
                Assert.That(shown, Is.GreaterThan(micro / 1e6 * 0.99 - 1e-9), text + " for " + micro);
            }
        }

        private static double Parse(string text)
        {
            double sign = 1;
            if (text.StartsWith("-")) { sign = -1; text = text.Substring(1); }
            double scale = 1;
            string[] suffixes = { "K", "M", "B", "T", "Qa", "Qi" };
            for (int i = suffixes.Length - 1; i >= 0; i--)
            {
                if (!text.EndsWith(suffixes[i])) continue;
                text = text.Substring(0, text.Length - suffixes[i].Length);
                scale = Math.Pow(1000, i + 1);
                break;
            }
            return sign * scale * double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------------------------------------
        // RunFactorySnapshot (GD-2) and EconomySnapshot
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void FactorySnapshot_ListsSlots_Machines_AndTheLockedSlotsWithTheirUnlockLevel()
        {
            var rig = Rig(cash: 1000);
            Guid a = BuildOk(rig, 1, Miner);
            Guid b = BuildOk(rig, 3, Smelter);
            Assert.That(rig.Commands.ReorderPriority(new[] { b, a }).IsSuccess, Is.True);

            RunFactorySnapshot snapshot = RunSnapshots.Factory(rig.Session);

            Assert.That(snapshot.Cash, Is.EqualTo(rig.State.Cash));
            Assert.That(snapshot.FactoryLevel, Is.EqualTo(1));
            Assert.That(snapshot.SlotCount, Is.EqualTo(4));
            Assert.That(snapshot.Slots, Has.Count.EqualTo(12), "the grid shows every slot up to the last factory level");
            Assert.That(snapshot.Slots.Take(4).Select(s => s.Status),
                Is.EqualTo(new[] { SlotStatus.Empty, SlotStatus.Occupied, SlotStatus.Empty, SlotStatus.Occupied }));
            Assert.That(snapshot.Slots.Skip(4).Select(s => s.UnlocksAtFactoryLevel), Is.EqualTo(new[] { 2, 2, 3, 3, 4, 4, 5, 5 }));
            Assert.That(snapshot.Slots.Skip(4).All(s => s.Status == SlotStatus.Locked), Is.True);
            Assert.That(snapshot.Machines.Select(m => m.InstanceId), Is.EqualTo(new[] { a, b }), "machines in slot order");
            Assert.That(snapshot.Slots[1].MachineIndex, Is.EqualTo(0));
            Assert.That(snapshot.Slots[3].MachineIndex, Is.EqualTo(1));
            Assert.That(snapshot.Priority, Is.EqualTo(new[] { b, a }));
            Assert.That(snapshot.Machines[0].PriorityPosition, Is.EqualTo(1));
            Assert.That(snapshot.StructureRevision, Is.EqualTo(rig.Session.StructureRevision));
            Assert.That(snapshot.QuantityRevision, Is.EqualTo(rig.Session.QuantityRevision));
        }

        [Test]
        public void FactorySnapshot_ShowsBuildCostsUnlocksAndTheReasonForEachType()
        {
            var rig = Rig(cash: 5, unlockedMachines: new[] { Miner });

            RunFactorySnapshot snapshot = RunSnapshots.Factory(rig.Session);

            Assert.That(snapshot.MachineTypes.Select(t => t.MachineId), Is.EqualTo(new[] { Assembler, Miner, Press, Smelter }), "ordinal ID order");
            MachineTypeSnapshot miner = snapshot.MachineTypes.Single(t => t.MachineId == Miner);
            Assert.That(miner.Unlocked, Is.True);
            Assert.That(miner.BuildCost, Is.EqualTo(10));
            Assert.That(miner.Affordable, Is.False);
            Assert.That(miner.RejectionReason, Is.EqualTo(RunReasons.CashInsufficient));
            Assert.That(miner.Level1Rates.Outputs.Single().PerMinuteText, Is.EqualTo("60"));
            MachineTypeSnapshot smelter = snapshot.MachineTypes.Single(t => t.MachineId == Smelter);
            Assert.That(smelter.Unlocked, Is.False);
            Assert.That(smelter.RejectionReason, Is.EqualTo(RunReasons.MachineLocked));
            // T140: an "always" condition would unlock at the first settle; the rig keeps a type locked behind the last factory level.
            Assert.That(smelter.UnlockKind, Is.EqualTo("factoryLevelAtLeast"));
            Assert.That(smelter.UnlockLevel, Is.EqualTo(5));

            var rich = RunSnapshots.Factory(Rig(cash: 500, unlockedMachines: new[] { Miner }).Session);
            Assert.That(rich.MachineTypes.Single(t => t.MachineId == Miner).RejectionReason, Is.Null);
            Assert.That(rich.MachineTypes.Single(t => t.MachineId == Miner).Affordable, Is.True);
        }

        [Test]
        public void FactorySnapshot_ShowsTheRefundTheFactoryValueChangeAndTheNextUpgradeResult()
        {
            var options = new RunCommandFixtures.Options { Milestones = new[] { new SpeedMilestone(3, 2_000_000) } };
            var rig = Rig(options, cash: 1000);
            Guid id = BuildOk(rig, 0, Miner);
            rig.Commands.Upgrade(id);
            MachineDef def = rig.Catalog.Machines[Miner];
            long invested = def.CostTable[0] + def.CostTable[1];

            MachineSnapshot m = RunSnapshots.Factory(rig.Session).Machines.Single();

            Assert.That(m.Level, Is.EqualTo(2));
            Assert.That(m.InvestedCash, Is.EqualTo(invested));
            Assert.That(m.RefundOnRemove, Is.EqualTo(invested / 2));
            Assert.That(m.FactoryValueDeltaOnRemove, Is.EqualTo(invested / 2 - invested));
            Assert.That(m.FactoryValueDeltaOnRemove, Is.LessThan(0));
            UpgradePreview next = m.NextUpgrade;
            Assert.That(next.Available, Is.True);
            Assert.That(next.NewLevel, Is.EqualTo(3));
            Assert.That(next.Cost, Is.EqualTo(def.CostTable[2]));
            Assert.That(next.Affordable, Is.True);
            Assert.That(next.RejectionReason, Is.Null);
            Assert.That(next.ReachesMilestone, Is.True);
            Assert.That(next.MilestoneLevel, Is.EqualTo(3));
            Assert.That(next.MilestoneMultiplierPpm, Is.EqualTo(2_000_000));
            Assert.That(next.NewRates.Outputs.Single().PerMinuteMicro, Is.GreaterThan(m.Rates.Outputs.Single().PerMinuteMicro * 2));

            // The preview is what the command then does.
            UpgradeResult done = rig.Commands.Upgrade(id).Value;
            Assert.That(done.CostPaid, Is.EqualTo(next.Cost));
            Assert.That(done.Rates.Outputs.Single().PerMinuteMicro, Is.EqualTo(next.NewRates.Outputs.Single().PerMinuteMicro));
            Assert.That(done.MilestoneReached, Is.EqualTo(next.ReachesMilestone));
        }

        [Test]
        public void FactorySnapshot_AtTheMaximumLevelOffersNoUpgrade_AndExpansionTracksShowTheirNextLevel()
        {
            var rig = Rig(new RunCommandFixtures.Options { MaxLevel = 1 }, cash: 3000);
            BuildOk(rig, 0, Miner);

            RunFactorySnapshot snapshot = RunSnapshots.Factory(rig.Session);

            UpgradePreview none = snapshot.Machines.Single().NextUpgrade;
            Assert.That(none.Available, Is.False);
            Assert.That(none.RejectionReason, Is.EqualTo(RunReasons.LevelMax));
            Assert.That(snapshot.Storage.Level, Is.EqualTo(1));
            Assert.That(snapshot.Storage.MaxLevel, Is.EqualTo(5));
            Assert.That(snapshot.Storage.CurrentValue, Is.EqualTo(1000));
            Assert.That(snapshot.Storage.NextValue, Is.EqualTo(2000));
            Assert.That(snapshot.Storage.NextCost, Is.EqualTo(2500));
            Assert.That(snapshot.Storage.Affordable, Is.True);
            Assert.That(snapshot.Factory.CurrentValue, Is.EqualTo(4));
            Assert.That(snapshot.Factory.NextValue, Is.EqualTo(6));
            Assert.That(snapshot.Factory.NextCost, Is.EqualTo(5000));
            Assert.That(snapshot.Factory.Affordable, Is.False);
            Assert.That(snapshot.Factory.RejectionReason, Is.EqualTo(RunReasons.CashInsufficient));

            var maxed = RunCommandFixtures.NewRig(null, 0, 5, 5);
            RunFactorySnapshot last = RunSnapshots.Factory(maxed.Session);
            Assert.That(last.Storage.HasNext, Is.False);
            Assert.That(last.Storage.RejectionReason, Is.EqualTo(RunReasons.LevelMax));
            Assert.That(last.Factory.HasNext, Is.False);
            Assert.That(last.Slots.All(s => s.Status != SlotStatus.Locked), Is.True);
        }

        [Test]
        public void ASnapshot_IsImmutable_AndDoesNotFollowLaterCommands()
        {
            var rig = Rig();
            Guid id = BuildOk(rig, 0, Miner);
            RunFactorySnapshot before = RunSnapshots.Factory(rig.Session);
            RatesSnapshot ratesBefore = RunSnapshots.Rates(rig.Session);

            rig.Commands.Upgrade(id);
            rig.Commands.SetActive(id, false);

            Assert.That(before.Machines.Single().Level, Is.EqualTo(1));
            Assert.That(before.Machines.Single().IsActive, Is.True);
            Assert.That(Resource(ratesBefore, "iron.ore").ProductionPerMinuteMicro, Is.EqualTo(60_000_000));
            Assert.That(before.StructureRevision, Is.LessThan(rig.Session.StructureRevision));
        }

        [Test]
        public void EconomySnapshot_ListsPricesStockValueAndTheAutoSellSettings()
        {
            var rig = Rig(cash: 77);
            RunSimHarness.Stock(rig.State, "iron.plate", 5);
            Assert.That(rig.Commands.SetAutoSell("iron.gear", true).IsSuccess, Is.True);

            EconomySnapshot snapshot = RunSnapshots.Economy(rig.Session);

            Assert.That(snapshot.Cash, Is.EqualTo(77));
            Assert.That(snapshot.Resources.Select(r => r.ResourceId), Is.EqualTo(new[] { "iron.bar", "iron.gear", "iron.ore", "iron.plate" }));
            ResourceEconomy plate = snapshot.Resources.Single(r => r.ResourceId == "iron.plate");
            Assert.That(plate.BasePrice, Is.EqualTo(8));
            Assert.That(plate.SalePricePpm, Is.EqualTo(1_000_000));
            Assert.That(plate.UnitPrice, Is.EqualTo(8));
            Assert.That(plate.Units, Is.EqualTo(5));
            Assert.That(plate.StockValue, Is.EqualTo(40));
            Assert.That(plate.AutoSell, Is.False);
            Assert.That(plate.PriceValid, Is.True);
            Assert.That(snapshot.Resources.Single(r => r.ResourceId == "iron.gear").AutoSell, Is.True);
            Assert.That(snapshot.Resources.All(r => r.CanToggleAutoSell), Is.True);
        }

        [Test]
        public void EconomySnapshot_DisablesTheAutoSellToggleOfALockedResource_AndRatesHideIt()
        {
            var rig = RunCommandFixtures.NewRig(null, 100, 1, 1, null, new[] { "iron.gear" });

            ResourceEconomy gear = RunSnapshots.Economy(rig.Session).Resources.Single(r => r.ResourceId == "iron.gear");
            Assert.That(gear.Unlocked, Is.False);
            Assert.That(gear.CanToggleAutoSell, Is.False);
            Assert.That(RunSnapshots.Rates(rig.Session).Resources.Any(r => r.ResourceId == "iron.gear"), Is.False, "locked resources are hidden");
        }
    }
}
