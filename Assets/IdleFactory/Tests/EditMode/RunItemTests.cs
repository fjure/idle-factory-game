using System;
using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// ActivateItem and the ItemsSnapshot (T101, contracts/run-application.md, FR-016, FR-017, US3 scenarios 1-2,
    /// OD-1): every tier is obtainable (contract reward) and usable, target validation, conflict rejection that keeps
    /// the Item, exact expiry, progress rescale, multiplicative composition with Overdrive and Research, the content
    /// bounds, and atomic rejection with the structural flags.
    ///
    /// Fixture Items: item.t1-boost (tier 1, factory, machineSpeed x1.5, 60 s), item.t2-drill (tier 2, machine
    /// types miner and smelter, machineOutput x2, 120 s), item.t3-turbo (tier 3, miner and smelter, machineSpeed x2,
    /// 180 s). The miner cycle at speed 1 is 20,000,000 ticks.
    /// </summary>
    public sealed class RunItemTests
    {
        private const string Boost = "item.t1-boost";
        private const string Drill = "item.t2-drill";
        private const string Turbo = "item.t3-turbo";
        private const string OutputResearch = "research.miner-output";
        private const long Ms = RunSession.TicksPerMs;
        private const long MinerCycle = 20_000_000;

        private static ItemDef[] Items() => new[]
        {
            new ItemDef(Boost, 1, "Boost", "F003-ITEM-BOOST", 60_000, "factory", new string[0],
                new EffectDef("machineSpeed", "allMachines", null, 1_500_000)),
            new ItemDef(Drill, 2, "Drill", "F003-ITEM-DRILL", 120_000, "machineType", new[] { RunCommandFixtures.Miner, RunCommandFixtures.Smelter },
                new EffectDef("machineOutput", "machineType", null, 2)),
            new ItemDef(Turbo, 3, "Turbo", "F003-ITEM-TURBO", 180_000, "machineType", new[] { RunCommandFixtures.Miner, RunCommandFixtures.Smelter },
                new EffectDef("machineSpeed", "allMachines", null, 2_000_000)),
        };

        private static ContractOfferDef[] Offers()
        {
            var always = new UnlockCondition("always");
            ContractRewardDef Rp() => new ContractRewardDef("rp", new Curve(1, 1_000_000, "floor"));
            return new[]
            {
                new ContractOfferDef("contract.plate", "plate", "iron.plate", new Curve(2, 1_010_000, "ceil"), null, new[] { Rp() }, always, rewardRatioPpm: 1_500_000),
                new ContractOfferDef("contract.gear", "gear", "iron.gear", new Curve(2, 1_000_000, "ceil"), null, new[] { Rp() }, always, rewardRatioPpm: 1_500_000),
                new ContractOfferDef("contract.timed-bar", "timedBar", "iron.bar", new Curve(2, 1_010_000, "ceil"), 60_000, new[] { Rp() }, always, rewardRatioPpm: 1_500_000),
            };
        }

        /// <summary>T320: the Items come from the ladder by run-wide completion index, so the first three completions of the run grant one tier each.</summary>
        private static ContractItemLadder Ladder() => new ContractItemLadder(
            new[] { new ContractItemLadderEntry(0, Boost), new ContractItemLadderEntry(1, Drill), new ContractItemLadderEntry(2, Turbo) }, 3);

        private static RunCommandFixtures.Rig Rig(RunCommandFixtures.Options o = null, long cash = 100_000, bool withMiner = false)
        {
            o = o ?? new RunCommandFixtures.Options();
            o.Items = Items();
            o.Offers = Offers();
            o.Ladder = Ladder();
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(o, cash);
            rig.Advance(0); // the first settle unlocks the offers and Overdrive (T140)
            if (withMiner) Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            return rig;
        }

        private static void Grant(RunCommandFixtures.Rig rig, string itemId, long count = 1) =>
            Assert.That(rig.Session.Permanent.TryAddItems(itemId, count), Is.EqualTo(RunStateResult.Ok));

        private static long Owned(RunCommandFixtures.Rig rig, string itemId) => rig.Session.Permanent.ItemCount(itemId);

        private static MachineInstance Miner(RunCommandFixtures.Rig rig) => rig.State.FindMachine(rig.Id(1));

        private static ItemActivationResult Activate(RunCommandFixtures.Rig rig, string itemId, string target = null)
        {
            RunCommandResult<ItemActivationResult> r = rig.Commands.ActivateItem(itemId, target);
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            return r.Value;
        }

        private static string Full(RunCommandFixtures.Rig rig)
        {
            RunState s = rig.State;
            return RunCommandFixtures.Snapshot(s) + "cash=" + s.Cash + " rp=" + s.Rp +
                " items=" + string.Join(",", rig.Session.Permanent.ItemStacks().Select(i => i.Id + ":" + i.Count)) +
                " effects=" + string.Join(",", s.Effects.Effects.Select(e => e.SourceId + "/" + e.ConflictKey.TargetKey + "/" + e.StartUtcMs + "/" + e.EndUtcMs)) +
                " tl=" + rig.Session.ActiveTicksSinceAnchor;
        }

        private static void AssertRejected<T>(RunCommandFixtures.Rig rig, Func<RunCommandResult<T>> command, string code)
        {
            string before = Full(rig);
            long structure = rig.Session.StructureRevision, quantity = rig.Session.QuantityRevision;

            RunCommandResult<T> result = command();

            Assert.That(result.IsSuccess, Is.False, "expected " + code);
            Assert.That(result.Outcome, Is.EqualTo(RunCommandOutcome.Rejected));
            Assert.That(result.ReasonCode, Is.EqualTo(code));
            Assert.That(result.Value, Is.EqualTo(default(T)), "a rejection carries no value");
            Assert.That(Full(rig), Is.EqualTo(before), "a rejection never changes state");
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure));
            Assert.That(rig.Session.QuantityRevision, Is.EqualTo(quantity));
        }

        private static long Speed(RunCommandFixtures.Rig rig, MachineInstance m, bool overdrive = false)
        {
            Assert.That(new RunModifiers(rig.Catalog).TryMachineSpeed(rig.State, rig.Session.Permanent, m, overdrive, null, out long speed), Is.True);
            return speed;
        }

        private static long OutputFactor(RunCommandFixtures.Rig rig, MachineInstance m)
        {
            Assert.That(new RunModifiers(rig.Catalog).TryOutputFactor(rig.State, m, null, out long factor), Is.True);
            return factor;
        }

        // ------------------------------------------------------------------------------------------------
        // Every tier is obtainable and usable
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void EveryTier_IsObtainedFromAContractReward_AndActivates()
        {
            var rig = Rig(withMiner: true);
            var rewards = new[] { ("contract.plate", "iron.plate", 2L, Boost, (string)null), ("contract.gear", "iron.gear", 2L, Drill, RunCommandFixtures.Miner),
                ("contract.timed-bar", "iron.bar", 2L, Turbo, RunCommandFixtures.Smelter) };
            foreach (var (offer, resource, units, item, target) in rewards)
            {
                Assert.That(rig.Commands.AcceptContract(offer).IsSuccess, Is.True, offer);
                RunSimHarness.Stock(rig.State, resource, units);
                RunCommandResult<ContractDeliveryResult> delivered = rig.Commands.DeliverContract();
                Assert.That(delivered.IsSuccess, Is.True, delivered.ReasonCode);
                Assert.That(Owned(rig, item), Is.EqualTo(1), "the tier's Item is granted by its contract");

                ItemActivationResult r = Activate(rig, item, target);

                Assert.That(Owned(rig, item), Is.Zero, "exactly one Item is consumed");
                Assert.That(r.RemainingCount, Is.Zero);
                Assert.That(r.ItemId, Is.EqualTo(item));
                Assert.That(r.StartUtcMs, Is.EqualTo(rig.Session.TimelineUtcMs));
                Assert.That(r.EndUtcMs - r.StartUtcMs, Is.EqualTo(rig.Catalog.Items[item].DurationMs));
            }
            Assert.That(rig.State.Effects.Count, Is.EqualTo(3));
            Assert.That(new[] { 1, 2, 3 }, Is.EqualTo(rig.State.Effects.Effects.Select(e => rig.Catalog.Items[e.SourceId].Tier).OrderBy(t => t)));
        }

        [Test]
        public void Activate_ConsumesExactlyOne_AndAddsTheEffectOverTheItemDuration()
        {
            var rig = Rig();
            Grant(rig, Boost, 3);
            long structure = rig.Session.StructureRevision;

            ItemActivationResult r = Activate(rig, Boost);

            Assert.That(Owned(rig, Boost), Is.EqualTo(2));
            Assert.That(r.RemainingCount, Is.EqualTo(2));
            Assert.That(r.EffectKind, Is.EqualTo("machineSpeed"));
            Assert.That(r.TargetKey, Is.EqualTo(RunCommands.FactoryTargetKey));
            ActiveEffect e = rig.State.Effects.Effects.Single();
            Assert.That(e.SourceId, Is.EqualTo(Boost));
            Assert.That(e.StartUtcMs, Is.EqualTo(RunCommandFixtures.StartUtcMs));
            Assert.That(e.EndUtcMs, Is.EqualTo(RunCommandFixtures.StartUtcMs + 60_000));
            Assert.That(rig.Session.StructureRevision, Is.GreaterThan(structure), "an activation is a structural change");
        }

        [Test]
        public void Activate_RescalesProgress_KeepingTheCompletedFraction()
        {
            var rig = Rig(withMiner: true);
            Grant(rig, Boost);
            rig.Advance(10_000_000); // half of the 20,000,000 tick cycle
            MachineInstance miner = Miner(rig);
            Assert.That(miner.ProgressTicks, Is.EqualTo(10_000_000));

            Activate(rig, Boost);

            Assert.That(miner.CycleDurationTicks, Is.EqualTo(13_333_334), "ceil(20,000,000 x 10^6 / 1,500,000)");
            Assert.That(miner.ProgressTicks, Is.EqualTo(6_666_667), "floor(10,000,000 x 13,333,334 / 20,000,000)");
        }

        // ------------------------------------------------------------------------------------------------
        // Rejections
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Activate_WithoutTheItem_IsItemNone_ForAKnownAndAnUnknownItem()
        {
            var rig = Rig();

            AssertRejected(rig, () => rig.Commands.ActivateItem(Boost, null), RunReasons.ItemNone);
            AssertRejected(rig, () => rig.Commands.ActivateItem("item.unknown", null), RunReasons.ItemNone);
            AssertRejected(rig, () => rig.Commands.ActivateItem(null, null), RunReasons.ItemNone);
        }

        [Test]
        public void Activate_ATargetOutsideTheContract_IsItemTargetInvalid_AndKeepsTheItem()
        {
            var rig = Rig(withMiner: true);
            Grant(rig, Drill);
            Grant(rig, Boost);

            AssertRejected(rig, () => rig.Commands.ActivateItem(Drill, null), RunReasons.ItemTargetInvalid);
            AssertRejected(rig, () => rig.Commands.ActivateItem(Drill, ""), RunReasons.ItemTargetInvalid);
            AssertRejected(rig, () => rig.Commands.ActivateItem(Drill, RunCommandFixtures.Press), RunReasons.ItemTargetInvalid);
            AssertRejected(rig, () => rig.Commands.ActivateItem(Drill, "machine.unknown"), RunReasons.ItemTargetInvalid);
            AssertRejected(rig, () => rig.Commands.ActivateItem(Drill, rig.Id(1).ToString()), RunReasons.ItemTargetInvalid);
            AssertRejected(rig, () => rig.Commands.ActivateItem(Drill, RunCommandFixtures.Miner.ToUpperInvariant()), RunReasons.ItemTargetInvalid);
            AssertRejected(rig, () => rig.Commands.ActivateItem(Boost, RunCommandFixtures.Miner), RunReasons.ItemTargetInvalid);
            Assert.That(Owned(rig, Drill), Is.EqualTo(1));
            Assert.That(Owned(rig, Boost), Is.EqualTo(1));
        }

        [Test]
        public void Activate_WhileTheSameConflictKeyRuns_IsEffectConflict_AndKeepsTheItem()
        {
            var rig = Rig();
            Grant(rig, Boost, 2);
            Grant(rig, Drill, 2);
            Activate(rig, Boost);
            Activate(rig, Drill, RunCommandFixtures.Miner);

            AssertRejected(rig, () => rig.Commands.ActivateItem(Boost, null), RunReasons.EffectConflict);
            AssertRejected(rig, () => rig.Commands.ActivateItem(Drill, RunCommandFixtures.Miner), RunReasons.EffectConflict);
            Assert.That(Owned(rig, Boost), Is.EqualTo(1), "the conflicting Item is kept");
            Assert.That(Owned(rig, Drill), Is.EqualTo(1));
            Assert.That(rig.State.Effects.Count, Is.EqualTo(2));
        }

        [Test]
        public void Activate_TheSameItemOnAnotherMachineType_HasAnotherConflictKey()
        {
            var rig = Rig();
            Grant(rig, Drill, 2);

            Activate(rig, Drill, RunCommandFixtures.Miner);
            Activate(rig, Drill, RunCommandFixtures.Smelter);

            Assert.That(rig.State.Effects.Effects.Select(e => e.ConflictKey.TargetKey).OrderBy(k => k, StringComparer.Ordinal),
                Is.EqualTo(new[] { RunCommandFixtures.Miner, RunCommandFixtures.Smelter }));
        }

        [Test]
        public void Activate_ADifferentEffectCategoryOnTheSameTarget_IsNotAConflict()
        {
            var rig = Rig();
            Grant(rig, Drill);
            Grant(rig, Turbo);

            Activate(rig, Drill, RunCommandFixtures.Miner);
            Activate(rig, Turbo, RunCommandFixtures.Miner);

            Assert.That(rig.State.Effects.Count, Is.EqualTo(2), "machineOutput and machineSpeed on the miner do not share a key");
        }

        [Test]
        public void Activate_LifecycleBlocked_IsRejectedFirst_WithoutAnyChange()
        {
            var rig = Rig();
            Grant(rig, Boost);
            rig.Session.SetLifecycleBlocked(true);

            AssertRejected(rig, () => rig.Commands.ActivateItem(Boost, null), RunReasons.LifecycleBlocked);
            AssertRejected(rig, () => rig.Commands.ActivateItem("item.unknown", "x"), RunReasons.LifecycleBlocked);
            Assert.That(Owned(rig, Boost), Is.EqualTo(1));
        }

        [Test]
        public void Activate_WhenTheEndWouldExceedTheBound_IsValueLimit_AndKeepsTheItem()
        {
            var rig = Rig();
            var late = new RunSession(rig.Catalog, rig.State, rig.Session.Permanent, ExactBound.Value - 30_000);
            var commands = new RunCommands(late);
            Grant(rig, Boost);
            string before = Full(rig);

            RunCommandResult<ItemActivationResult> r = commands.ActivateItem(Boost, null);

            Assert.That(r.IsSuccess, Is.False);
            Assert.That(r.ReasonCode, Is.EqualTo(RunReasons.ValueLimit));
            Assert.That(Full(rig), Is.EqualTo(before));
            Assert.That(late.StructureRevision, Is.Zero);
            Assert.That(late.QuantityRevision, Is.Zero);
        }

        [Test]
        public void Activate_WhenTheComposedSpeedCannotBeComputed_IsValueLimit_AtomicallyAfterTheTrial()
        {
            // A research whose two levels overflow the checked speed fold, and a machine to derive it for.
            var o = new RunCommandFixtures.Options
            {
                Research = new[]
                {
                    new ResearchDef("research.huge", "Huge", 3, new Curve(1, 1_000_000, "ceil"), new ResearchPrerequisite[0],
                        new EffectDef("machineSpeed", "allMachines", null, 100_000_000_000_000L)),
                },
            };
            var rig = Rig(o, withMiner: true);
            rig.State.TryAddRp(10);
            Assert.That(rig.Commands.PurchaseResearch("research.huge").IsSuccess, Is.True);
            Assert.That(rig.State.TrySetResearchLevel("research.huge", 2), Is.EqualTo(RunStateResult.Ok));
            Grant(rig, Boost);

            AssertRejected(rig, () => rig.Commands.ActivateItem(Boost, null), RunReasons.ValueLimit);
            Assert.That(rig.State.Effects.Count, Is.Zero, "the trial effect is undone");
        }

        [Test]
        public void Rejected_Activation_LeavesAStaleEffectUntouched_AfterAClockRebind()
        {
            var rig = Rig();
            Grant(rig, Boost, 3);
            Activate(rig, Boost);
            Assert.That(rig.Session.RebindAnchor(RunCommandFixtures.StartUtcMs + 100_000).IsSuccess, Is.True);
            Assert.That(rig.State.Effects.Count, Is.EqualTo(1), "the over effect is still listed: no settle has run");
            rig.Session.SetLifecycleBlocked(true);

            AssertRejected(rig, () => rig.Commands.ActivateItem(Boost, null), RunReasons.LifecycleBlocked);

            rig.Session.SetLifecycleBlocked(false);
            ItemActivationResult r = Activate(rig, Boost);
            Assert.That(r.StartUtcMs, Is.EqualTo(RunCommandFixtures.StartUtcMs + 100_000), "an applied command replaces the over effect");
            Assert.That(rig.State.Effects.Effects.Single().EndUtcMs, Is.EqualTo(r.EndUtcMs));
        }

        [Test]
        public void ValueLimit_Activation_LeavesAStaleEffectUntouched_AfterAClockRebind()
        {
            var o = new RunCommandFixtures.Options
            {
                Research = new[]
                {
                    new ResearchDef("research.huge", "Huge", 3, new Curve(1, 1_000_000, "ceil"), new ResearchPrerequisite[0],
                        new EffectDef("machineSpeed", "allMachines", null, 100_000_000_000_000L)),
                },
            };
            var rig = Rig(o, withMiner: true);
            rig.State.TryAddRp(10);
            Assert.That(rig.Commands.PurchaseResearch("research.huge").IsSuccess, Is.True);
            Grant(rig, Boost, 2);
            Activate(rig, Boost);
            Assert.That(rig.Session.RebindAnchor(RunCommandFixtures.StartUtcMs + 100_000).IsSuccess, Is.True);
            Assert.That(rig.State.TrySetResearchLevel("research.huge", 2), Is.EqualTo(RunStateResult.Ok));

            AssertRejected(rig, () => rig.Commands.ActivateItem(Boost, null), RunReasons.ValueLimit);
            Assert.That(rig.State.Effects.Count, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------------------------------------
        // Expiry at the exact instant
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Effect_IsActiveUntilTheLastMillisecond_AndEndsAtTheExactInstant()
        {
            var rig = Rig(withMiner: true);
            Grant(rig, Boost, 2);
            Activate(rig, Boost);
            MachineInstance miner = Miner(rig);
            long boostedCycle = miner.CycleDurationTicks;
            Assert.That(boostedCycle, Is.EqualTo(13_333_334));

            rig.Advance(60_000 * Ms - 1 * Ms);
            Assert.That(rig.Session.TimelineUtcMs, Is.EqualTo(RunCommandFixtures.StartUtcMs + 59_999));
            Assert.That(rig.State.Effects.Count, Is.EqualTo(1), "one millisecond before the end the effect still runs");
            Assert.That(miner.CycleDurationTicks, Is.EqualTo(boostedCycle));
            AssertRejected(rig, () => rig.Commands.ActivateItem(Boost, null), RunReasons.EffectConflict);

            rig.Advance(1 * Ms);
            Assert.That(rig.Session.TimelineUtcMs, Is.EqualTo(RunCommandFixtures.StartUtcMs + 60_000));
            Assert.That(rig.State.Effects.Count, Is.Zero, "the interval is [t, t + duration): the instant t + duration is outside");
            Assert.That(Speed(rig, miner), Is.EqualTo(1_000_000));
            Assert.That(Activate(rig, Boost).StartUtcMs, Is.EqualTo(RunCommandFixtures.StartUtcMs + 60_000), "the Item can be used again at that instant");
        }

        [Test]
        public void Effect_ProductionUsesTheBoostedCycleOnlyInsideTheInterval()
        {
            var rig = Rig(withMiner: true);
            Grant(rig, Boost);
            Activate(rig, Boost);
            long ore = rig.State.Inventory.Units("iron.ore");

            rig.Advance(60_000 * Ms);
            long boosted = rig.State.Inventory.Units("iron.ore") - ore;
            ore = rig.State.Inventory.Units("iron.ore");
            rig.Advance(60_000 * Ms);
            long normal = rig.State.Inventory.Units("iron.ore") - ore;

            Assert.That(normal, Is.EqualTo(60_000 * Ms / MinerCycle * 2), "30 cycles of 2 ore");
            Assert.That(boosted, Is.GreaterThan(normal));
        }

        // ------------------------------------------------------------------------------------------------
        // Composition and bounds
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Effects_OfDifferentCategories_AndOverdrive_ComposeMultiplicatively()
        {
            var rig = Rig(withMiner: true);
            Grant(rig, Boost);
            Grant(rig, Turbo);
            MachineInstance miner = Miner(rig);

            Activate(rig, Boost);
            Assert.That(Speed(rig, miner), Is.EqualTo(1_500_000));
            Activate(rig, Turbo, RunCommandFixtures.Miner);
            Assert.That(Speed(rig, miner), Is.EqualTo(3_000_000), "1.5 x 2");
            Assert.That(rig.Commands.ActivateOverdrive().IsSuccess, Is.True);

            Assert.That(Speed(rig, miner, true), Is.EqualTo(6_000_000), "1.5 x 2 x Overdrive 2");
            Assert.That(miner.CycleDurationTicks, Is.EqualTo(3_333_334), "ceil(20,000,000 x 10^6 / 6,000,000)");
        }

        [Test]
        public void TurboOnTheMinerType_LeavesOtherMachineTypesAtTheirOwnSpeed()
        {
            var rig = Rig();
            Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            Assert.That(rig.Build(1, RunCommandFixtures.Smelter).IsSuccess, Is.True);
            Grant(rig, Turbo);

            Activate(rig, Turbo, RunCommandFixtures.Smelter);

            Assert.That(Speed(rig, rig.State.FindMachine(rig.Id(1))), Is.EqualTo(1_000_000));
            Assert.That(Speed(rig, rig.State.FindMachine(rig.Id(2))), Is.EqualTo(2_000_000), "a machine-type Item hits every machine of the chosen type only (OD-1)");
        }

        [Test]
        public void OutputFactor_ComposesItemAndResearchPerLevel_AndClampsToTheContentBound()
        {
            var o = new RunCommandFixtures.Options
            {
                Research = new[]
                {
                    new ResearchDef(OutputResearch, "Miner Output", 3, new Curve(1, 1_000_000, "ceil"), new ResearchPrerequisite[0],
                        new EffectDef("machineOutput", "machine", RunCommandFixtures.Miner, 2)),
                },
            };
            var rig = Rig(o, withMiner: true);
            MachineInstance miner = Miner(rig);
            Grant(rig, Drill);
            rig.State.TryAddRp(10);
            Assert.That(OutputFactor(rig, miner), Is.EqualTo(1));

            Activate(rig, Drill, RunCommandFixtures.Miner);
            Assert.That(OutputFactor(rig, miner), Is.EqualTo(2));
            Assert.That(rig.Commands.PurchaseResearch(OutputResearch).IsSuccess, Is.True);
            Assert.That(OutputFactor(rig, miner), Is.EqualTo(4), "item 2 x research level 1 (2)");
            Assert.That(rig.Commands.PurchaseResearch(OutputResearch).IsSuccess, Is.True);
            Assert.That(OutputFactor(rig, miner), Is.EqualTo(4), "2 x 2 x 2 = 8 clamps to maxCombinedOutputFactor 4");
        }

        [Test]
        public void SpeedComposition_ClampsToMaxCombinedSpeed()
        {
            var o = new RunCommandFixtures.Options { Overdrive = new OverdriveDef(30_000, 300_000, 60_000_000, new UnlockCondition("always")) };
            var rig = Rig(o, withMiner: true);
            MachineInstance miner = Miner(rig);
            Grant(rig, Boost);
            Grant(rig, Turbo);
            Activate(rig, Boost);
            Activate(rig, Turbo, RunCommandFixtures.Miner);

            Assert.That(rig.Commands.ActivateOverdrive().IsSuccess, Is.True);

            Assert.That(Speed(rig, miner, true), Is.EqualTo(100_000_000), "1.5 x 2 x 60 = 180 clamps to maxCombinedSpeedPpm 100");
            Assert.That(miner.CycleDurationTicks, Is.EqualTo(1_000_000), "the batch factor absorbs the speed beyond the minimum cycle");
        }

        // ------------------------------------------------------------------------------------------------
        // ItemsSnapshot
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void ItemsSnapshot_EmptyState_ListsTheDefinitionsByTier_WithZeroCounts()
        {
            var rig = Rig();

            ItemsSnapshot s = RunSnapshots.Items(rig.Session);

            Assert.That(s.TotalCount, Is.Zero, "the 'No Items yet' empty state");
            Assert.That(s.Stacks.Select(x => x.ItemId), Is.EqualTo(new[] { Boost, Drill, Turbo }));
            Assert.That(s.Stacks.All(x => x.Count == 0 && !x.CanActivate && x.ActivateReason == RunReasons.ItemNone), Is.True);
            Assert.That(s.Active, Is.Empty);
        }

        [Test]
        public void ItemsSnapshot_ShowsTargetsConflictsAndCountdowns()
        {
            var rig = Rig();
            Grant(rig, Drill, 2);
            Grant(rig, Boost);
            Activate(rig, Drill, RunCommandFixtures.Miner);
            rig.Advance(20_000 * Ms);

            ItemsSnapshot s = RunSnapshots.Items(rig.Session);

            ItemStackSnapshot drill = s.Stacks.Single(x => x.ItemId == Drill);
            Assert.That(drill.Count, Is.EqualTo(1));
            Assert.That(drill.Targets.Select(t => (t.Target, t.Available, t.Reason)), Is.EqualTo(new[]
            {
                (RunCommandFixtures.Miner, false, RunReasons.EffectConflict), (RunCommandFixtures.Smelter, true, (string)null),
            }));
            Assert.That(drill.CanActivate, Is.True);
            ItemStackSnapshot boost = s.Stacks.Single(x => x.ItemId == Boost);
            Assert.That(boost.Targets.Single().Target, Is.Null);
            Assert.That(boost.Targets.Single().TargetKey, Is.EqualTo(RunCommands.FactoryTargetKey));
            Assert.That(boost.CanActivate, Is.True);
            ActiveEffectSnapshot active = s.Active.Single();
            Assert.That(active.ItemId, Is.EqualTo(Drill));
            Assert.That(active.TargetKey, Is.EqualTo(RunCommandFixtures.Miner));
            Assert.That(active.RemainingMs, Is.EqualTo(100_000), "120 s minus the 20 s advanced");
            Assert.That(s.TotalCount, Is.EqualTo(2));
        }

        [Test]
        public void ItemsSnapshot_EveryTargetInConflict_DisablesActivationWithTheReason()
        {
            var rig = Rig();
            Grant(rig, Drill, 3);
            Activate(rig, Drill, RunCommandFixtures.Miner);
            Activate(rig, Drill, RunCommandFixtures.Smelter);

            ItemStackSnapshot drill = RunSnapshots.Items(rig.Session).Stacks.Single(x => x.ItemId == Drill);

            Assert.That(drill.CanActivate, Is.False);
            Assert.That(drill.ActivateReason, Is.EqualTo(RunReasons.EffectConflict));
        }
    }
}
