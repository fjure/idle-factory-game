using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// T358 (F5-R19, F5-R20 MIN-1 ... MIN-10, FR-040 to FR-044, SC-011, D-O, D-P): manual mining. The milli-yield remainder, the
    /// per-second rate cap on the run timeline, partial counting at a full store that leaves the cap unused, the 10^18 boundaries, the
    /// lifecycle block, atomic rejections, the <c>manualYield</c> effect of Research and Industry with no dead level, mined Ore that
    /// stays out of the production feed (MIN-5), the aggregated event, and the content rules <c>manual.yield.bound</c> and
    /// <c>start.reachable</c> with negative cases and a first-Miner probe at <c>maxIndex</c>.
    ///
    /// Fixture: base yield 100 milli, cap 10 per second, so ten activations are exactly one Ore. The fixture store holds 1,000 units at
    /// level 1 and the Ore sells for 1 Cash.
    /// </summary>
    public sealed class ManualMiningTests
    {
        private const string Ore = "iron.ore";
        private const long Bound = ExactBound.Value;
        private const long Ms = RunSession.TicksPerMs;

        private static ManualMiningDef Mining(long baseYieldMilli = 100, long cap = 10) =>
            new ManualMiningDef(Ore, baseYieldMilli, cap, 60_000, 10, 1_500_000, 100_000);

        private static RunCommandFixtures.Rig NewRig(RunCommandFixtures.Options options = null, long cash = 100_000, long rp = 0, string[] lockedResources = null)
        {
            options = options ?? new RunCommandFixtures.Options();
            if (options.Mining == null) options.Mining = Mining();
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(options, cash, rp: rp, lockedResources: lockedResources);
            rig.Advance(0);
            return rig;
        }

        /// <summary>Moves the timeline to the start of the next timeline second (a whole multiple of 1000 ms).</summary>
        private static void NextSecond(RunCommandFixtures.Rig rig)
        {
            long rest = 1000 - rig.Session.TimelineUtcMs % 1000;
            rig.Advance(rest * Ms);
            Assert.That(rig.Session.TimelineUtcMs % 1000, Is.Zero);
        }

        private static string Snap(RunCommandFixtures.Rig rig) =>
            RunCommandFixtures.Snapshot(rig.State) +
            "mining=" + rig.State.MiningTimelineSecond + "/" + rig.State.MiningCountedInSecond + "/" + rig.State.MiningRemainderMilli +
            " timeline=" + rig.Session.TimelineUtcMs + " rev=" + rig.Session.StructureRevision + "/" + rig.Session.QuantityRevision;

        private static long Stock(RunCommandFixtures.Rig rig) => rig.State.Inventory.Units(Ore);

        private static void Fill(RunCommandFixtures.Rig rig, long units) =>
            Assert.That(rig.State.Inventory.TryMine(Ore, units), Is.EqualTo(RunStateResult.Ok));

        private static MineResult Mined(RunCommandFixtures.Rig rig, long activations)
        {
            RunCommandResult<MineResult> result = rig.Commands.Mine(activations);
            Assert.That(result.IsSuccess, Is.True, result.ReasonCode);
            return result.Value;
        }

        private static void AssertRejectedWithoutChange(RunCommandFixtures.Rig rig, long activations, string reason)
        {
            string before = Snap(rig);
            RunCommandResult<MineResult> result = rig.Commands.Mine(activations);
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ReasonCode, Is.EqualTo(reason));
            Assert.That(Snap(rig), Is.EqualTo(before), "a rejection changes no state, the rate counter and the remainder included");
        }

        private sealed class MiningSink : IRunEventSink
        {
            public readonly List<RunMiningEvent> Mining = new List<RunMiningEvent>();
            public int Others;
            public void OnHeader(in RunHeaderEvent e) => Others++;
            public void OnSample(in RunSampleEvent e) => Others++;
            public void OnMilestone(in RunMilestoneEvent e) => Others++;
            public void OnSale(in RunSaleEvent e) => Others++;
            public void OnMining(in RunMiningEvent e) => Mining.Add(e);
            public void OnContract(in RunContractEvent e) => Others++;
            public void OnPrestige(in RunPrestigeEvent e) => Others++;
            public void OnOfflineReturn(in RunOfflineReturnEvent e) => Others++;
            public void OnBottleneck(in RunBottleneckEvent e) => Others++;
            public void OnPurchase(in RunPurchaseEvent e) => Others++;
            public void OnClock(in RunClockEvent e) => Others++;
            public void OnLifecycle(in RunLifecycleEvent e) => Others++;
        }

        private sealed class ThrowingMiningSink : IRunEventSink
        {
            public void OnHeader(in RunHeaderEvent e) { }
            public void OnSample(in RunSampleEvent e) { }
            public void OnMilestone(in RunMilestoneEvent e) { }
            public void OnSale(in RunSaleEvent e) { }
            public void OnMining(in RunMiningEvent e) => throw new InvalidOperationException("sink failure");
            public void OnContract(in RunContractEvent e) { }
            public void OnPrestige(in RunPrestigeEvent e) { }
            public void OnOfflineReturn(in RunOfflineReturnEvent e) { }
            public void OnBottleneck(in RunBottleneckEvent e) { }
            public void OnPurchase(in RunPurchaseEvent e) { }
            public void OnClock(in RunClockEvent e) { }
            public void OnLifecycle(in RunLifecycleEvent e) { }
        }

        private static RunCommandFixtures.Rig WithSink(RunCommandFixtures.Rig rig, IRunEventSink sink)
        {
            rig.Session = new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog), RunCommandFixtures.StartUtcMs, sink);
            rig.Commands = new RunCommands(rig.Session, () => RunSimHarness.Id(rig.NextId++));
            return rig;
        }

        // ---- milli-yield remainder (D-P, MIN-1, MIN-2) -------------------------------------------------------

        [Test]
        public void TenActivationsOfOneHundredMilliGiveExactlyOneOre()
        {
            RunCommandFixtures.Rig rig = NewRig();

            MineResult nine = Mined(rig, 9);
            Assert.That(nine.Counted, Is.EqualTo(9));
            Assert.That(nine.Ignored, Is.Zero);
            Assert.That(nine.Blocked, Is.Zero);
            Assert.That(nine.Units, Is.Zero, "nine activations are 900 milli, not yet a whole Ore");
            Assert.That(Stock(rig), Is.Zero);
            Assert.That(rig.State.MiningRemainderMilli, Is.EqualTo(900));

            MineResult tenth = Mined(rig, 1);
            Assert.That(tenth.Units, Is.EqualTo(1));
            Assert.That(Stock(rig), Is.EqualTo(1));
            Assert.That(rig.State.MiningRemainderMilli, Is.Zero);
            Assert.That(tenth.StockAfter, Is.EqualTo(1));
        }

        [Test]
        public void TenActivationsInOneCallAreOneOreAndTheRemainderCarriesAcrossCalls()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Assert.That(Mined(rig, 10).Units, Is.EqualTo(1));
            Assert.That(Stock(rig), Is.EqualTo(1));

            // A yield of 350 milli: 3 x 350 = 1050 -> 1 Ore and 50 left; 3 x 350 + 50 = 1100 -> 1 Ore and 100 left.
            RunCommandFixtures.Rig odd = NewRig(new RunCommandFixtures.Options { Mining = Mining(350) });
            MineResult first = Mined(odd, 3);
            Assert.That(first.Units, Is.EqualTo(1));
            Assert.That(odd.State.MiningRemainderMilli, Is.EqualTo(50));
            NextSecond(odd);
            Assert.That(Mined(odd, 3).Units, Is.EqualTo(1));
            Assert.That(odd.State.MiningRemainderMilli, Is.EqualTo(100));
            Assert.That(Stock(odd), Is.EqualTo(2));
        }

        [Test]
        public void AMiningCallBelowOneOreStillSettlesAndPublishesNoQuantityChange()
        {
            RunCommandFixtures.Rig rig = NewRig();
            long quantity = rig.Session.QuantityRevision;
            Assert.That(Mined(rig, 1).Units, Is.Zero);
            Assert.That(rig.Session.QuantityRevision, Is.EqualTo(quantity), "no stock changed, so the quantity revision did not move");
            Assert.That(Mined(rig, 9).Units, Is.EqualTo(1));
            Assert.That(rig.Session.QuantityRevision, Is.GreaterThan(quantity));
        }

        // ---- rate cap on the run timeline (MIN-7) ------------------------------------------------------------

        [Test]
        public void FiftyActivationsInOneSecondCountTenAndIgnoreForty()
        {
            RunCommandFixtures.Rig rig = NewRig();
            NextSecond(rig);

            MineResult burst = Mined(rig, 50);
            Assert.That(burst.Counted, Is.EqualTo(10));
            Assert.That(burst.Ignored, Is.EqualTo(40));
            Assert.That(burst.Blocked, Is.Zero);
            Assert.That(burst.Units, Is.EqualTo(1));

            string before = Snap(rig);
            MineResult more = Mined(rig, 5);
            Assert.That(more.Counted, Is.Zero);
            Assert.That(more.Ignored, Is.EqualTo(5));
            Assert.That(more.Units, Is.Zero);
            Assert.That(Snap(rig), Is.EqualTo(before), "an all-ignored call changes nothing");
        }

        [Test]
        public void TheCapStartsFreshAtTheNextTimelineSecond()
        {
            RunCommandFixtures.Rig rig = NewRig();
            NextSecond(rig);
            Assert.That(Mined(rig, 10).Counted, Is.EqualTo(10));

            rig.Advance(999 * Ms);
            MineResult sameSecond = Mined(rig, 1);
            Assert.That(sameSecond.Counted, Is.Zero, "999 ms later is still the same timeline second");
            Assert.That(sameSecond.Ignored, Is.EqualTo(1));

            rig.Advance(1 * Ms);
            MineResult next = Mined(rig, 50);
            Assert.That(next.Counted, Is.EqualTo(10), "the cap across a second boundary");
            Assert.That(next.Ignored, Is.EqualTo(40));
            Assert.That(Stock(rig), Is.EqualTo(2));
        }

        [Test]
        public void PartOfTheCapUsedInOneCallLeavesTheRestForTheNextCallOfTheSameSecond()
        {
            RunCommandFixtures.Rig rig = NewRig();
            NextSecond(rig);
            Assert.That(Mined(rig, 6).Counted, Is.EqualTo(6));
            MineResult rest = Mined(rig, 6);
            Assert.That(rest.Counted, Is.EqualTo(4));
            Assert.That(rest.Ignored, Is.EqualTo(2));
            Assert.That(rig.State.MiningCountedInSecond, Is.EqualTo(10));
        }

        [Test]
        public void ALoadedStateStartsWithAFreshCounter()
        {
            RunCommandFixtures.Rig rig = NewRig();
            NextSecond(rig);
            Assert.That(Mined(rig, 10).Counted, Is.EqualTo(10));
            Assert.That(Mined(rig, 10).Counted, Is.Zero);

            // A load installs a new RunState; the transient counter and remainder are not part of the save.
            var loaded = new RunState(rig.Catalog, RunSimHarness.Id(9001), 1, null, 100_000, 0, 1, 1,
                new[] { "iron.ore", "iron.bar", "iron.plate", "iron.gear", RunCommandFixtures.Miner, RunCommandFixtures.Smelter, RunCommandFixtures.Press, RunCommandFixtures.Assembler });
            rig.Session.ReplaceLoaded(loaded, rig.Session.Permanent, rig.Session.TimelineUtcMs);
            Assert.That(rig.Session.State.MiningRemainderMilli, Is.Zero);
            Assert.That(rig.Session.State.MiningCountedInSecond, Is.Zero);
            Assert.That(Mined(rig, 10).Counted, Is.EqualTo(10), "at most one extra second of the cap after a load (MIN-7)");
        }

        // ---- partial counting at a full store (MIN-10) -------------------------------------------------------

        [Test]
        public void AFullStoreCountsOnlyTheActivationsWhoseWholeOreFitAndLeavesTheCapUnused()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Fill(rig, 1000);
            NextSecond(rig);

            MineResult partial = Mined(rig, 10);
            Assert.That(partial.Counted, Is.EqualTo(9), "nine activations are 900 milli: no whole Ore is needed");
            Assert.That(partial.Blocked, Is.EqualTo(1), "the tenth needs a whole Ore the full store cannot take");
            Assert.That(partial.Ignored, Is.Zero);
            Assert.That(partial.Units, Is.Zero);
            Assert.That(Stock(rig), Is.EqualTo(1000));
            Assert.That(rig.State.MiningRemainderMilli, Is.EqualTo(900));
            Assert.That(rig.State.MiningCountedInSecond, Is.EqualTo(9), "the blocked activation did not use the cap");

            AssertRejectedWithoutChange(rig, 1, RunReasons.StorageFull);
            Assert.That(rig.State.MiningCountedInSecond, Is.EqualTo(9), "a rejection does not use the cap either");

            // Making room lets the held activation through, and it is the last one of this second's cap.
            Assert.That(rig.Commands.Sell(Ore, 5).IsSuccess, Is.True);
            MineResult after = Mined(rig, 5);
            Assert.That(after.Counted, Is.EqualTo(1));
            Assert.That(after.Ignored, Is.EqualTo(4));
            Assert.That(after.Units, Is.EqualTo(1));
            Assert.That(rig.State.MiningCountedInSecond, Is.EqualTo(10));
        }

        [Test]
        public void ADescendingSearchCountsTheLargestFittingNumberOfActivations()
        {
            // 700 milli per activation, remainder 0, three free units: 5 activations = 3500 milli = 3 Ore fits; 6 = 4200 milli = 4 Ore does not.
            RunCommandFixtures.Rig rig = NewRig(new RunCommandFixtures.Options { Mining = Mining(700) });
            Fill(rig, 997);
            NextSecond(rig);
            MineResult result = Mined(rig, 10);
            Assert.That(result.Counted, Is.EqualTo(5));
            Assert.That(result.Blocked, Is.EqualTo(5));
            Assert.That(result.Units, Is.EqualTo(3));
            Assert.That(Stock(rig), Is.EqualTo(1000));
            Assert.That(rig.State.MiningRemainderMilli, Is.EqualTo(500));
        }

        [Test]
        public void ARejectedMineChangesNothingNotEvenTheRateCounter()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Fill(rig, 1000);
            NextSecond(rig);
            Mined(rig, 9);
            Assert.That(rig.State.MiningRemainderMilli, Is.EqualTo(900));

            AssertRejectedWithoutChange(rig, 1, RunReasons.StorageFull);
            AssertRejectedWithoutChange(rig, 50, RunReasons.StorageFull);
        }

        // ---- boundaries and lifecycle ------------------------------------------------------------------------

        [Test]
        public void ALifecycleBlockedSessionRejectsBeforeAnythingElse()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Mined(rig, 9);
            Fill(rig, 1000);
            rig.Session.SetLifecycleBlocked(true);
            AssertRejectedWithoutChange(rig, 1, RunReasons.LifecycleBlocked);
            AssertRejectedWithoutChange(rig, 0, RunReasons.LifecycleBlocked);

            rig.Session.SetLifecycleBlocked(false);
            Assert.That(rig.Commands.Mine(1).ReasonCode, Is.EqualTo(RunReasons.StorageFull), "the lifecycle check came first, the store check is next");
        }

        [Test]
        public void TheStoreMayFillToExactlyTenToTheEighteenAndNoFurther()
        {
            var storage = new[] { new StorageLevelDef(1, Bound, 0) };
            RunCommandFixtures.Rig rig = NewRig(new RunCommandFixtures.Options { Storage = storage });
            Fill(rig, Bound - 1);
            NextSecond(rig);

            MineResult last = Mined(rig, 10);
            Assert.That(last.Counted, Is.EqualTo(10));
            Assert.That(last.Units, Is.EqualTo(1));
            Assert.That(Stock(rig), Is.EqualTo(Bound));
            Assert.That(rig.State.Inventory.Free, Is.Zero);

            NextSecond(rig);
            MineResult none = Mined(rig, 10);
            Assert.That(none.Counted, Is.EqualTo(9));
            Assert.That(none.Blocked, Is.EqualTo(1));
            AssertRejectedWithoutChange(rig, 1, RunReasons.StorageFull);
            Assert.That(Stock(rig), Is.EqualTo(Bound));
        }

        [Test]
        public void AnOverflowingYieldCountsWhatStaysWithinTenToTheEighteenAndRejectsWhenNothingFits()
        {
            // 2.5 x 10^17 milli per activation: 3 activations are 7.5 x 10^17, the 4th would reach 10^18 exactly and the 5th passes it.
            var storage = new[] { new StorageLevelDef(1, Bound, 0) };
            RunCommandFixtures.Rig rig = NewRig(new RunCommandFixtures.Options { Mining = Mining(250_000_000_000_000_000L), Storage = storage });
            NextSecond(rig);
            MineResult result = Mined(rig, 10);
            Assert.That(result.Counted, Is.EqualTo(4), "4 x 2.5 x 10^17 = 10^18 is the largest checked sum");
            Assert.That(result.Blocked, Is.EqualTo(6));
            Assert.That(result.Units, Is.EqualTo(1_000_000_000_000_000L));

            // The remainder is 0 again, so one more activation fits; two do not.
            RunCommandFixtures.Rig second = NewRig(new RunCommandFixtures.Options { Mining = Mining(Bound), Storage = storage });
            NextSecond(second);
            MineResult one = Mined(second, 10);
            Assert.That(one.Counted, Is.EqualTo(1));
            Assert.That(one.Blocked, Is.EqualTo(9));
        }

        [Test]
        public void ASettleThatCouldPassTenToTheEighteenIsAValueLimitRejection()
        {
            // Cash is at the bound and Ore auto-sells for 1 Cash: the tenth activation would add an Ore that the settle sells past 10^18.
            RunCommandFixtures.Rig rig = NewRig(cash: Bound);
            Assert.That(rig.Commands.SetAutoSell(Ore, true).IsSuccess, Is.True);
            NextSecond(rig);

            MineResult partial = Mined(rig, 10);
            Assert.That(partial.Counted, Is.EqualTo(9));
            Assert.That(partial.Blocked, Is.EqualTo(1));
            Assert.That(rig.State.Cash, Is.EqualTo(Bound));
            Assert.That(rig.State.MiningCountedInSecond, Is.EqualTo(9));

            AssertRejectedWithoutChange(rig, 1, RunReasons.ValueLimit);
        }

        [Test]
        public void ContentWithoutManualMiningHasNoMining()
        {
            RunCommandFixtures.Options legacy = new RunCommandFixtures.Options();
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(legacy);
            rig.Advance(0);
            Assert.That(rig.Catalog.RunStart.ManualMining, Is.Null);
            AssertRejectedWithoutChange(rig, 1, RunReasons.CommandInvalid);
        }

        [Test]
        public void AnInvalidActivationCountAndALockedResourceAreRejectedWithoutChange()
        {
            RunCommandFixtures.Rig rig = NewRig();
            AssertRejectedWithoutChange(rig, 0, RunReasons.CommandInvalid);
            AssertRejectedWithoutChange(rig, -3, RunReasons.CommandInvalid);

            RunCommandFixtures.Rig locked = NewRig(lockedResources: new[] { Ore });
            Assert.That(locked.State.IsUnlocked(Ore), Is.False);
            AssertRejectedWithoutChange(locked, 1, RunReasons.ResourceLocked);
        }

        [Test]
        public void ADomainCapacityRejectionIsTheStorageFullCode()
        {
            // The #367 follow-up: every CapacityExceeded reads as storage.full, not as the generic command.invalid.
            Assert.That(RunReasons.From(RunStateResult.CapacityExceeded), Is.EqualTo(RunReasons.StorageFull));
            Assert.That(RunReasons.StorageFull, Is.EqualTo("storage.full"));
            Assert.That(RunReasons.All, Does.Not.Contain(RunReasons.StorageFull), "like build.price.changed it stays outside the 24 pinned codes");
        }

        // ---- the settle and the simulator ---------------------------------------------------------------------

        [Test]
        public void MinedOreIsSoldByAutoSellInTheSettleLikeAnyStock()
        {
            RunCommandFixtures.Rig rig = NewRig(cash: 0);
            Assert.That(rig.Commands.SetAutoSell(Ore, true).IsSuccess, Is.True);
            NextSecond(rig);
            MineResult result = Mined(rig, 10);
            Assert.That(result.Units, Is.EqualTo(1));
            Assert.That(rig.State.Cash, Is.EqualTo(1));
            Assert.That(Stock(rig), Is.Zero);
            Assert.That(result.StockAfter, Is.Zero, "the result reports the stock after the settle");
        }

        [Test]
        public void MinedOreDoesNotAdvanceAProductionObjectiveOrAnyCompletionRecord()
        {
            // MIN-5. Production-objective progress is counted from machine completions only (counted by T327, state ActiveContract.Produced since T322); Mine writes the stock directly.
            // Until then the observable feed is the contract board and the session's completion records, and mining touches neither.
            var always = new UnlockCondition("always");
            var o = new RunCommandFixtures.Options
            {
                Offers = new[]
                {
                    new ContractOfferDef("contract.mine-ore", "plate", Ore, new Curve(10, 1_000_000, "ceil"), null,
                        new[] { new ContractRewardDef("cash", new Curve(50, 1_000_000, "floor")) }, always, objective: "produce", rewardRatioPpm: 1_000_000),
                },
            };
            RunCommandFixtures.Rig rig = NewRig(o);
            Assert.That(rig.Commands.AcceptContract("contract.mine-ore").IsSuccess, Is.True);
            ActiveContract before = rig.State.Contracts.Active;
            long completions = rig.State.Contracts.CompletionCount("contract.mine-ore");

            NextSecond(rig);
            Assert.That(Mined(rig, 10).Units, Is.EqualTo(1));
            NextSecond(rig);
            Assert.That(Mined(rig, 10).Units, Is.EqualTo(1));

            Assert.That(rig.State.Contracts.Active, Is.SameAs(before), "the active production contract is untouched");
            Assert.That(rig.State.Contracts.Active.Produced, Is.Zero, "Mine leaves Produced unchanged (T322)");
            Assert.That(rig.State.Contracts.CompletionCount("contract.mine-ore"), Is.EqualTo(completions));
            Assert.That(rig.Session.TryGetLastCompletion(RunSimHarness.Id(1), out _, out _, out _), Is.False, "no machine completion was recorded");

            // The control: a Miner's own completions are the recorded production.
            Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            rig.Advance(30_000_000);
            Assert.That(rig.Session.TryGetLastCompletion(RunSimHarness.Id(1), out long units, out _, out _), Is.True);
            Assert.That(units, Is.GreaterThan(0));
        }

        [Test]
        public void TheSameCommandsGiveTheSameStateAndDoNotDependOnTheSink()
        {
            string Run(IRunEventSink sink)
            {
                RunCommandFixtures.Rig rig = NewRig();
                if (sink != null) WithSink(rig, sink);
                NextSecond(rig);
                Mined(rig, 50);
                Mined(rig, 3);
                rig.Advance(1000 * Ms);
                Mined(rig, 7);
                Fill(rig, 0);
                return Snap(rig);
            }

            string none = Run(null);
            Assert.That(Run(null), Is.EqualTo(none), "deterministic");
            Assert.That(Run(new MiningSink()), Is.EqualTo(none), "a recording sink changes nothing");
            string throwing = null;
            Assert.DoesNotThrow(() => throwing = Run(new ThrowingMiningSink()));
            Assert.That(throwing, Is.EqualTo(none), "a sink that throws changes nothing");
        }

        [Test]
        public void TheAggregatedEventCarriesTheNumbersOfOneCallAndAThrowingSinkIsCounted()
        {
            var sink = new MiningSink();
            RunCommandFixtures.Rig rig = WithSink(NewRig(), sink);
            NextSecond(rig);
            Mined(rig, 50);
            Assert.That(sink.Mining, Has.Count.EqualTo(1), "one record per call, never one per activation");
            RunMiningEvent e = sink.Mining[0];
            Assert.That(e.ResourceId, Is.EqualTo(Ore));
            Assert.That(e.Counted, Is.EqualTo(10));
            Assert.That(e.Ignored, Is.EqualTo(40));
            Assert.That(e.Blocked, Is.Zero);
            Assert.That(e.Ore, Is.EqualTo(1));

            Fill(rig, 999);
            NextSecond(rig);
            Assert.That(rig.Commands.Mine(10).IsSuccess, Is.True);
            Assert.That(rig.Commands.Mine(10).IsSuccess, Is.False);
            Assert.That(sink.Mining.Last().Blocked, Is.EqualTo(1), "a rejected call is logged with its blocked count");
            Assert.That(sink.Mining.Last().Counted, Is.Zero);

            RunCommandFixtures.Rig throwing = WithSink(NewRig(), new ThrowingMiningSink());
            NextSecond(throwing);
            Assert.That(throwing.Commands.Mine(10).IsSuccess, Is.True);
            Assert.That(throwing.Session.DroppedEvents, Is.EqualTo(1));
        }

        // ---- the manualYield effect (FR-043, MIN-1) ----------------------------------------------------------

        private static ResearchDef HandDrill() => new ResearchDef("research.hand-drill", "Hand Drill", 3, new Curve(1, 1_000_000, "ceil"), new ResearchPrerequisite[0],
            new EffectDef("manualYield", "none", null, 1_100_000));

        private static IndustryUpgradeDef DeepVeins(long decayPpm = 500_000) => new IndustryUpgradeDef("industry.deep-veins", "Deep Veins", 3, new Curve(1, 1_000_000, "ceil"),
            new EffectDef("manualYield", "none", null, 1_200_000), decayPpm);

        private static long Yield(RunCommandFixtures.Rig rig)
        {
            var modifiers = new RunModifiers(rig.Catalog);
            Assert.That(modifiers.TryManualYieldPpm(rig.State, rig.Session.Permanent, out long ppm), Is.True);
            Assert.That(RunModifiers.TryEffectiveMilliYield(100, ppm, out long yield), Is.True);
            return yield;
        }

        [Test]
        public void EveryResearchLevelOfManualYieldChangesTheEffectiveMilliYield()
        {
            RunCommandFixtures.Rig rig = NewRig(new RunCommandFixtures.Options { Research = new[] { HandDrill() } }, rp: 10);
            var yields = new List<long> { Yield(rig) };
            for (int level = 1; level <= 3; level++)
            {
                Assert.That(rig.Commands.PurchaseResearch("research.hand-drill").IsSuccess, Is.True);
                yields.Add(Yield(rig));
            }

            Assert.That(yields, Is.EqualTo(new long[] { 100, 110, 121, 133 }), "x1.1 per level with a floor per level: 1.1, 1.21, 1.331");
            for (int i = 1; i < yields.Count; i++) Assert.That(yields[i], Is.GreaterThan(yields[i - 1]), "no dead level");

            NextSecond(rig);
            MineResult mined = Mined(rig, 10);
            Assert.That(mined.Units, Is.EqualTo(1), "10 x 133 = 1330 milli");
            Assert.That(rig.State.MiningRemainderMilli, Is.EqualTo(330));
        }

        [Test]
        public void IndustryManualYieldDecaysAsAMultiplicativeKindAndComposesAfterResearch()
        {
            RunCommandFixtures.Rig rig = NewRig(new RunCommandFixtures.Options { Research = new[] { HandDrill() }, Industry = new[] { DeepVeins() } }, rp: 10);
            Assert.That(DeepVeins().ValueTable, Is.EqualTo(new long[] { 1_200_000, 1_100_000, 1_050_000 }), "delta 200000 decays by 0.5 per level, as machineSpeed does");
            Assert.That(IndustryValueTable.IsAdditive("manualYield"), Is.False);
            Assert.That(IndustryValueTable.GainPpm("manualYield", DeepVeins().ValueTable.ToArray(), 2), Is.EqualTo(100_000));

            rig.Session.Permanent.TryAddIp(10);
            var yields = new List<long> { Yield(rig) };
            for (int level = 1; level <= 3; level++)
            {
                Assert.That(rig.Commands.PurchaseIndustry("industry.deep-veins").IsSuccess, Is.True);
                yields.Add(Yield(rig));
            }

            Assert.That(yields, Is.EqualTo(new long[] { 100, 120, 132, 138 }), "1.2, then x1.1 = 1.32, then x1.05 = 1.386");

            for (int level = 1; level <= 3; level++) Assert.That(rig.Commands.PurchaseResearch("research.hand-drill").IsSuccess, Is.True);
            // Research 1.331 first, then Industry per level with a floor each: 1.5972, 1.75692, 1.844766.
            Assert.That(Yield(rig), Is.EqualTo(184));
        }

        [Test]
        public void TheManualYieldEffectKindHasItsOwnShapeRules()
        {
            Assert.That(new EffectDef("manualYield", "none", null, 1_000_001).Kind, Is.EqualTo("manualYield"));
            Assert.Throws<ArgumentException>(() => new EffectDef("manualYield", "allMachines", null, 1_100_000), "no target");
            Assert.Throws<ArgumentOutOfRangeException>(() => new EffectDef("manualYield", "none", null, 1_000_000), "a factor above 10^6");
            Assert.Throws<ArgumentException>(() => new ItemDef("item.x", 1, "X", "F003-ITEM-X", 1000, "factory", new string[0], new EffectDef("manualYield", "none", null, 1_100_000)),
                "an Item cannot carry it");
        }

        // ---- content rules (contentRevision >= 3) ------------------------------------------------------------

        private static IReadOnlyList<ContentDiagnostic> Validate(Action<JObject> mutate)
        {
            JObject document = RunContentExample.Valid();
            mutate?.Invoke(document);
            RunContentReadResult read = RunContentReader.Read(RunContentExample.Bytes(document));
            Assert.That(read.IsSuccess, Is.True, read.IsSuccess ? string.Empty : read.Diagnostic.Code + "@" + read.Diagnostic.Path);
            return RunContentValidator.Validate(read.Document);
        }

        /// <summary>Reads and validates; a reader failure is returned as the single diagnostic.</summary>
        private static IReadOnlyList<ContentDiagnostic> ReadOrValidate(Action<JObject> mutate)
        {
            JObject document = RunContentExample.Valid();
            mutate(document);
            RunContentReadResult read = RunContentReader.Read(RunContentExample.Bytes(document));
            return read.IsSuccess ? RunContentValidator.Validate(read.Document) : new[] { read.Diagnostic };
        }

        private static string Describe(IEnumerable<ContentDiagnostic> diagnostics) => string.Join(" | ", diagnostics.Select(d => d.Code + "@" + d.Path + ": " + d.Message));

        private static IEnumerable<ContentDiagnostic> Rule(IEnumerable<ContentDiagnostic> diagnostics, string code) => diagnostics.Where(d => d.Code == code);

        private static void AddManualYieldResearch(JObject d, long valuePerLevelPpm, int maxLevel = 3)
        {
            JObject research = (JObject)d["research"][0].DeepClone();
            research["id"] = "research.hand-drill";
            research["label"] = "Hand Drill";
            research["maxLevel"] = maxLevel;
            research["prerequisites"] = new JArray();
            research["effect"] = new JObject();
            research["effect"]["kind"] = "manualYield";
            research["effect"]["valuePerLevelPpm"] = valuePerLevelPpm;
            ((JArray)d["research"]).Add(research);
        }

        [Test]
        public void TheGuideExampleSatisfiesBothRules()
        {
            IReadOnlyList<ContentDiagnostic> diagnostics = Validate(null);
            Assert.That(diagnostics, Is.Empty, Describe(diagnostics));
            Assert.That(RunContentValidator.RuleCodes, Does.Contain("start.reachable").And.Contain("manual.yield.bound"));
        }

        [Test]
        public void AResearchAndAnIndustryManualYieldEffectAreReadValidatedAndBuiltIntoTheCatalog()
        {
            JObject document = RunContentExample.Valid();
            // Under the 100 % share cap (owner decision 2026-10-15) the composed yield of the two sources (x1.331 and x1.386) needs a smaller base yield than the
            // example's 25 milli: 15 milli x 1.845 = 27 milli x 10 = 0.27 Ore/s <= 0.3998 Ore/s of the reference Miner.
            document["runStart"]["manualMining"]["baseYieldMilli"] = 15;
            AddManualYieldResearch(document, 1_100_000);
            JObject industry = (JObject)document["industryUpgrades"][0].DeepClone();
            industry["id"] = "industry.deep-veins";
            industry["label"] = "Deep Veins";
            industry["effect"] = new JObject();
            industry["effect"]["kind"] = "manualYield";
            industry["effect"]["valuePerLevelPpm"] = 1_200_000;
            industry["decayPpm"] = 500_000;
            industry["maxLevel"] = 3;
            industry["practicalCeilingLevel"] = 3;
            industry["minGainPpm"] = 0;
            ((JArray)document["industryUpgrades"]).Add(industry);

            RunContentReadResult read = RunContentReader.Read(RunContentExample.Bytes(document));
            Assert.That(read.IsSuccess, Is.True);
            IReadOnlyList<ContentDiagnostic> diagnostics = RunContentValidator.Validate(read.Document);
            Assert.That(diagnostics, Is.Empty, Describe(diagnostics));
            RunCatalogLoadResult built = RunCatalogBuilder.Build(read.Document);
            Assert.That(built.IsSuccess, Is.True, Describe(built.Diagnostics));
            Assert.That(built.Catalog.Research["research.hand-drill"].Effect.Kind, Is.EqualTo("manualYield"));
            Assert.That(built.Catalog.Research["research.hand-drill"].Effect.TargetKind, Is.EqualTo("none"));
            Assert.That(built.Catalog.IndustryUpgrades["industry.deep-veins"].ValueTable, Is.EqualTo(new long[] { 1_200_000, 1_100_000, 1_050_000 }));
            Assert.That(built.Catalog.RunStart.ManualMining.BaseYieldMilli, Is.EqualTo(15));
        }

        [Test]
        public void ManualYieldEffectsOutsideTheirShapeAreRejectedWithTheEffectCodes()
        {
            IReadOnlyList<ContentDiagnostic> withTarget = ReadOrValidate(d =>
            {
                AddManualYieldResearch(d, 1_100_000);
                JObject target = new JObject();
                target["kind"] = "allMachines";
                ((JObject)d["research"][d["research"].Count() - 1]["effect"])["target"] = target;
            });
            Assert.That(withTarget.Any(x => x.Code == "member.unknown" && x.Path.EndsWith("/effect/target")), Is.True, Describe(withTarget));

            IReadOnlyList<ContentDiagnostic> notAboveOne = Validate(d => AddManualYieldResearch(d, 1_000_000));
            Assert.That(Rule(notAboveOne, "effect.value.out-of-range").Any(), Is.True, Describe(notAboveOne));

            IReadOnlyList<ContentDiagnostic> onItem = ReadOrValidate(d =>
            {
                d["items"][0]["effect"]["kind"] = "manualYield";
            });
            Assert.That(onItem.Any(x => x.Code == "effect.kind.unknown" || x.Code == "member.unknown" || x.Code == "value.out-of-range"), Is.True, Describe(onItem));
        }

        [Test]
        public void ManualYieldBoundComparesTheMaximumMiningRateWithTheReferenceMiner()
        {
            // Example: 25 milli x 10 per second = 0.25 Ore/s; the level-10 Miner (2 Ore per 20 s cycle, speed about x4) makes about 0.3998 Ore/s.
            Assert.That(Rule(Validate(d => d["runStart"]["manualMining"]["maxShareOfMinerPpm"] = 1_000_000), "manual.yield.bound"), Is.Empty, "0.25 <= 0.3998");
            IReadOnlyList<ContentDiagnostic> over = Validate(d => d["runStart"]["manualMining"]["maxShareOfMinerPpm"] = 500_000);
            ContentDiagnostic violation = Rule(over, "manual.yield.bound").Single();
            Assert.That(violation.Path, Is.EqualTo("/runStart/manualMining/maxShareOfMinerPpm"));

            // The bound is monotone in the share: exactly one threshold between pass and fail.
            long lastFail = 0;
            long firstPass = 1_000_000;
            for (long share = 0; share <= 1_000_000; share += 50_000)
            {
                bool fails = Rule(Validate(d => d["runStart"]["manualMining"]["maxShareOfMinerPpm"] = share), "manual.yield.bound").Any();
                if (fails) lastFail = share;
                else if (share < firstPass) firstPass = share;
            }

            Assert.That(firstPass, Is.GreaterThan(lastFail), "no pass below a fail");
            Assert.That(firstPass, Is.InRange(600_000, 700_000), "0.25 / 0.3998 = 0.625");

            // The cap scales the left side: 20 per second doubles the rate (0.5 Ore/s against 0.3998 at the largest share of 100 %), a yield of 15 milli
            // (0.3 Ore/s) fits again. Under the 100 % cap (owner decision 2026-10-15) the example no longer has room for 20 activations at 25 milli.
            Assert.That(Rule(Validate(d => { d["runStart"]["manualMining"]["maxActivationsPerSecond"] = 20; }), "manual.yield.bound").Any(), Is.True);
            Assert.That(Rule(Validate(d => { d["runStart"]["manualMining"]["maxActivationsPerSecond"] = 20; d["runStart"]["manualMining"]["baseYieldMilli"] = 15; }), "manual.yield.bound"), Is.Empty);

            // A reference level of 1 compares against a much slower Miner.
            Assert.That(Rule(Validate(d => d["runStart"]["manualMining"]["referenceMinerLevel"] = 1), "manual.yield.bound").Any(), Is.True);
        }

        private static void AddIndustrySpeed(JObject d, JObject target)
        {
            JObject industry = (JObject)d["industryUpgrades"][0].DeepClone();
            industry["id"] = "industry.deep-shafts";
            industry["label"] = "Deep Shafts";
            industry["maxLevel"] = 1;
            industry["decayPpm"] = 1_000_000;
            industry["practicalCeilingLevel"] = 1;
            industry["minGainPpm"] = 0;
            industry["effect"] = new JObject();
            industry["effect"]["kind"] = "machineSpeed";
            industry["effect"]["target"] = target;
            industry["effect"]["valuePerLevelPpm"] = 1_500_000;
            ((JArray)d["industryUpgrades"]).Add(industry);
        }

        [Test]
        public void ManualYieldBoundUsesTheMaximumIndustrySpeedOfTheReferenceMiner()
        {
            // Share 0.5: 0.25 Ore/s of mining against 0.5 x 0.3998 = 0.1999 Ore/s fails. An Industry speed x1.5 (one level) lifts the Miner to
            // about 0.5997 Ore/s, and 0.5 x 0.5997 = 0.2998 >= 0.25 passes: the pass comes from the Industry term only.
            Action<JObject> tight = d => d["runStart"]["manualMining"]["maxShareOfMinerPpm"] = 500_000;
            Assert.That(Rule(Validate(tight), "manual.yield.bound").Any(), Is.True, "without the upgrade the bound fails");

            JObject all = new JObject();
            all["kind"] = "allMachines";
            IReadOnlyList<ContentDiagnostic> allMachines = Validate(d => { tight(d); AddIndustrySpeed(d, all); });
            Assert.That(Rule(allMachines, "manual.yield.bound"), Is.Empty, "allMachines target: " + Describe(allMachines));

            JObject miner = new JObject();
            miner["kind"] = "machine";
            miner["id"] = "machine.miner";
            IReadOnlyList<ContentDiagnostic> byId = Validate(d => { tight(d); AddIndustrySpeed(d, miner); });
            Assert.That(Rule(byId, "manual.yield.bound"), Is.Empty, "Miner target: " + Describe(byId));

            JObject smelter = new JObject();
            smelter["kind"] = "machine";
            smelter["id"] = "machine.smelter";
            IReadOnlyList<ContentDiagnostic> other = Validate(d => { tight(d); AddIndustrySpeed(d, smelter); });
            Assert.That(Rule(other, "manual.yield.bound").Any(), Is.True, "an upgrade for another machine must not count: " + Describe(other));
        }

        [Test]
        public void ManualYieldBoundUsesTheMaximumResearchTable()
        {
            // Twenty levels of x1.2 would multiply the yield by 38: far above the share, though the level-1 yield is fine.
            IReadOnlyList<ContentDiagnostic> diagnostics = Validate(d => AddManualYieldResearch(d, 1_200_000, 20));
            Assert.That(Rule(diagnostics, "manual.yield.bound").Any(x => x.Path == "/runStart/manualMining/maxShareOfMinerPpm"), Is.True, Describe(diagnostics));
            // A gentle one stays inside it: 3 levels of x1.1 = 1.331 -> 33 milli x 10 = 0.33 Ore/s <= 0.3998 x 1.0.
            Assert.That(Rule(Validate(d => AddManualYieldResearch(d, 1_100_000)), "manual.yield.bound"), Is.Empty);
        }

        [Test]
        public void EveryPurchasableManualYieldLevelMustChangeTheEffectiveMilliYield()
        {
            // 25 milli: x1.04 is 26, x1.039999 is 25.99 and floors to 25, a dead level.
            Assert.That(Rule(Validate(d => AddManualYieldResearch(d, 1_040_000, 1)), "manual.yield.bound"), Is.Empty);
            IReadOnlyList<ContentDiagnostic> dead = Validate(d => AddManualYieldResearch(d, 1_039_999, 1));
            ContentDiagnostic level = Rule(dead, "manual.yield.bound").Single();
            Assert.That(level.Path, Does.StartWith("/research/").And.EndWith("/effect"));
            Assert.That(level.EntryId, Is.EqualTo("research.hand-drill"));

            // A later level can be the dead one: x1.04 is live at level 1 (26) but level 2 gives 27.04 -> 27 (live); with x1.02 the first is dead.
            Assert.That(Rule(Validate(d => AddManualYieldResearch(d, 1_020_000, 3)), "manual.yield.bound").Any(), Is.True);
        }

        [Test]
        public void ADecayingIndustryManualYieldHasADeadLevelWhenTheDecayedFactorFloorsAway()
        {
            // 25 milli: factors 1.08, 1.032, 1.0128 (delta 80000 x 0.4 per level). Level 1: 27. Level 2: 27.86 -> 27, dead.
            IReadOnlyList<ContentDiagnostic> diagnostics = Validate(d =>
            {
                JObject industry = (JObject)d["industryUpgrades"][0].DeepClone();
                industry["id"] = "industry.deep-veins";
                industry["effect"] = new JObject();
                industry["effect"]["kind"] = "manualYield";
                industry["effect"]["valuePerLevelPpm"] = 1_080_000;
                industry["decayPpm"] = 400_000;
                industry["maxLevel"] = 3;
                industry["practicalCeilingLevel"] = 0;
                industry["minGainPpm"] = 0;
                ((JArray)d["industryUpgrades"]).Add(industry);
            });
            ContentDiagnostic dead = Rule(diagnostics, "manual.yield.bound").Single();
            Assert.That(dead.Path, Does.StartWith("/industryUpgrades/").And.EndWith("/effect"));
            Assert.That(dead.Message, Does.Contain("Level 2"));
        }

        [Test]
        public void StartReachableNeedsTheFirstMinerAtEveryPrestigeIndexUpToMaxIndex()
        {
            // The example Miner costs 10 x 1.05^i at prestige index i (a ceiling once, floors per step), up to 17 at maxIndex 10; starting Cash is 25.
            // With no mining window the Cash alone must cover it.
            Assert.That(Rule(Validate(d => { d["runStart"]["startingCash"] = 17; d["runStart"]["manualMining"]["startWindowMs"] = 0; }), "start.reachable"), Is.Empty);
            IReadOnlyList<ContentDiagnostic> short16 = Validate(d => { d["runStart"]["startingCash"] = 16; d["runStart"]["manualMining"]["startWindowMs"] = 0; });
            ContentDiagnostic violation = Rule(short16, "start.reachable").Single();
            Assert.That(violation.Path, Is.EqualTo("/runStart/manualMining/startWindowMs"));
            Assert.That(violation.Message, Does.Contain("index 10"), "only the last index exceeds 16");

            // The mined Ore of the window is added: 25 milli x 10 x 8,000 ms / 10^6 = 2 Ore = 2 Cash.
            Assert.That(Rule(Validate(d => { d["runStart"]["startingCash"] = 15; d["runStart"]["manualMining"]["startWindowMs"] = 8_000; }), "start.reachable"), Is.Empty);
            Assert.That(Rule(Validate(d => { d["runStart"]["startingCash"] = 15; d["runStart"]["manualMining"]["startWindowMs"] = 7_999; }), "start.reachable").Any(), Is.True);
        }

        [Test]
        public void StartReachableDoesNotCapTheWindowOreByTheLevelOneStoreBecauseThePlayerCanSellAndMineAgain()
        {
            // Documents the current behaviour (#385 decision 2026-10-14, F1; open owner question): the rule is the literal F5-R19 formula. The window
            // Ore here is 25 x 10 x 60,000 / 10^6 = 15 units, more than a 10-unit level-1 store, and the content still passes: since T317 the player
            // can mine until the store is full, sell by hand and mine again within startWindowMs.
            IReadOnlyList<ContentDiagnostic> smallStore = Validate(d =>
            {
                d["storageLevels"][0]["capacity"] = 10;
                d["runStart"]["startingCash"] = 2; // the 15 Ore (15 Cash) must carry the Miner at index 0 (price 10) and at maxIndex (17)
            });
            Assert.That(Rule(smallStore, "start.reachable"), Is.Empty, Describe(smallStore));
            // Without the Ore the same Cash is short, so the pass comes from the window Ore only.
            Assert.That(Rule(Validate(d =>
            {
                d["storageLevels"][0]["capacity"] = 10;
                d["runStart"]["startingCash"] = 2;
                d["runStart"]["manualMining"]["startWindowMs"] = 0;
            }), "start.reachable").Any(), Is.True);
        }

        [Test]
        public void TheMiningRulesReturnSilentlyWithoutAUsableMinerBecauseOtherRulesRejectThatContent()
        {
            // Dependency, on purpose: manual.yield.bound and start.reachable skip when no input-free producer of the mined resource exists or
            // its buildPrice is unusable. They are not the guard for that content; these rules are.
            IReadOnlyList<ContentDiagnostic> noMiner = Validate(d => ((JArray)d["recipes"][1]["inputs"]).Add(new JObject { ["resourceId"] = "iron.bar", ["amount"] = 1 }));
            Assert.That(Rule(noMiner, "manual.yield.bound"), Is.Empty, Describe(noMiner));
            Assert.That(Rule(noMiner, "start.reachable"), Is.Empty, Describe(noMiner));
            Assert.That(Rule(noMiner, "unlock.unreachable").Any(), Is.True, "no Miner: the Ore-fed machines and contracts become unreachable (unlock.unreachable)");

            IReadOnlyList<ContentDiagnostic> capBelowBase = Validate(d =>
            {
                d["machines"][1]["buildPrice"]["cap"] = 0;
                d["runStart"]["startingCash"] = 0;
                d["runStart"]["manualMining"]["startWindowMs"] = 0;
            });
            Assert.That(Rule(capBelowBase, "start.reachable"), Is.Empty, "a Miner whose price is not usable is skipped, not priced");
            Assert.That(Rule(capBelowBase, "build.price.cap").Any(), Is.True, Describe(capBelowBase));

            IReadOnlyList<ContentDiagnostic> baseOutOfBound = Validate(d =>
            {
                d["machines"][1]["buildPrice"]["base"] = 1_000_000_000_001L;
                d["runStart"]["startingCash"] = 0;
                d["runStart"]["manualMining"]["startWindowMs"] = 0;
            });
            Assert.That(Rule(baseOutOfBound, "start.reachable"), Is.Empty);
            Assert.That(Rule(baseOutOfBound, "build.base.bound").Any(), Is.True, Describe(baseOutOfBound));
        }

        [Test]
        public void TheValidatorsPriceWalkAgreesWithBuildPricingAtEveryIndex()
        {
            // For several growths and caps, the first failing index the validator reports equals the first index where the runtime curve
            // (BuildPricing.TryCurve, owned 0) costs more than the funds.
            var variants = new[]
            {
                (growth: 1_050_000L, cap: 1000L, cash: 16L),
                (growth: 1_100_000L, cap: 1000L, cash: 20L),
                (growth: 1_300_000L, cap: 1000L, cash: 60L),
                (growth: 2_000_000L, cap: 12L, cash: 11L),
                (growth: 2_000_000L, cap: 12L, cash: 12L),
                (growth: 1_000_000L, cap: 1000L, cash: 10L),
            };
            foreach (var v in variants)
            {
                var def = new BuildPriceDef(10, 1_050_000, v.growth, v.cap);
                long expected = -1;
                for (long i = 0; i <= 10 && expected < 0; i++)
                {
                    Assert.That(BuildPricing.TryCurve(def, 0, i, out long price), Is.True);
                    if (price > v.cash) expected = i;
                }

                IReadOnlyList<ContentDiagnostic> diagnostics = Validate(d =>
                {
                    d["runStart"]["startingCash"] = v.cash;
                    d["runStart"]["manualMining"]["startWindowMs"] = 0;
                    d["machines"][1]["buildPrice"]["prestigeGrowthPpm"] = v.growth;
                    d["machines"][1]["buildPrice"]["cap"] = v.cap;
                });
                List<ContentDiagnostic> reported = Rule(diagnostics, "start.reachable").ToList();
                if (expected < 0) Assert.That(reported, Is.Empty, Describe(diagnostics));
                else
                {
                    Assert.That(reported, Has.Count.EqualTo(1), Describe(diagnostics));
                    Assert.That(reported[0].Message, Does.Contain("index " + expected + "."), "growth " + v.growth + " cap " + v.cap + " cash " + v.cash);
                }
            }
        }

        [Test]
        public void TheFirstMinerIsReachableAtMaxIndexWithTheGuideContent()
        {
            // The probe of MIN-4 on the Core types: starting Cash plus the sale of the start-window Ore covers the Miner at maxIndex.
            RunContentReadResult read = RunContentReader.Read(RunContentExample.Bytes(RunContentExample.Valid()));
            RunCatalogLoadResult built = RunCatalogBuilder.Build(read.Document);
            Assert.That(built.IsSuccess, Is.True, Describe(built.Diagnostics));
            RunCatalog catalog = built.Catalog;
            ManualMiningDef mining = catalog.RunStart.ManualMining;
            MachineDef miner = catalog.Machines.Values.Single(m => catalog.Recipes[m.RecipeId].Inputs.Count == 0);

            long oreInWindow = mining.BaseYieldMilli * mining.MaxActivationsPerSecond * mining.StartWindowMs / 1_000_000;
            Assert.That(SalePricing.TryValue(oreInWindow, catalog.Resources[mining.ResourceId].BasePrice, 1_000_000, out long income), Is.True);
            long funds = catalog.RunStart.StartingCash + income;
            for (long index = 0; index <= catalog.Prestige.Threshold.MaxIndex; index++)
            {
                Assert.That(BuildPricing.TryCurve(miner.BuildPrice, 0, index, out long price), Is.True);
                Assert.That(funds, Is.GreaterThanOrEqualTo(price), "prestige index " + index);
            }
        }

        [Test]
        public void TheSemanticMiningRulesApplyFromRevisionThreeOnly()
        {
            IReadOnlyList<ContentDiagnostic> revisionTwo = Validate(d =>
            {
                d["contentRevision"] = 2;
                d["runStart"]["startingCash"] = 0;
                d["runStart"]["manualMining"]["startWindowMs"] = 0;
                d["runStart"]["manualMining"]["maxShareOfMinerPpm"] = 1;
            });
            Assert.That(Rule(revisionTwo, "start.reachable"), Is.Empty);
            Assert.That(Rule(revisionTwo, "manual.yield.bound"), Is.Empty);

            IReadOnlyList<ContentDiagnostic> revisionThree = Validate(d =>
            {
                d["runStart"]["startingCash"] = 0;
                d["runStart"]["manualMining"]["startWindowMs"] = 0;
                d["runStart"]["manualMining"]["maxShareOfMinerPpm"] = 1;
            });
            Assert.That(Rule(revisionThree, "start.reachable").Any(), Is.True);
            Assert.That(Rule(revisionThree, "manual.yield.bound").Any(), Is.True);
        }

        [Test]
        public void ARuleSkipsWhatAnotherRuleAlreadyRejects()
        {
            // Out-of-range mining numbers are value.out-of-range only; the rules do not pile on.
            IReadOnlyList<ContentDiagnostic> diagnostics = Validate(d =>
            {
                d["runStart"]["manualMining"]["baseYieldMilli"] = 0;
                d["runStart"]["manualMining"]["maxActivationsPerSecond"] = 21;
            });
            Assert.That(diagnostics.Any(x => x.Code == "value.out-of-range"), Is.True);
            Assert.That(Rule(diagnostics, "manual.yield.bound"), Is.Empty);
            Assert.That(Rule(diagnostics, "start.reachable"), Is.Empty);

            // A maxIndex above 10,000 is the prestige.threshold.bound range check; the price walk is not attempted.
            IReadOnlyList<ContentDiagnostic> unbounded = Validate(d => d["prestige"]["threshold"]["maxIndex"] = 10_001);
            Assert.That(Rule(unbounded, "start.reachable"), Is.Empty);
        }

        [Test]
        public void TheSlowGrowthWalkAtTheLargestMaxIndexIsLinear()
        {
            // The #383 follow-up: BuildPricing's per-prestige loop is not clamped, so the validator walks the index once, at most 10,001 steps.
            var watch = System.Diagnostics.Stopwatch.StartNew();
            IReadOnlyList<ContentDiagnostic> diagnostics = Validate(d =>
            {
                d["prestige"]["threshold"]["maxIndex"] = 10_000;
                d["prestige"]["threshold"]["growthPpm"] = 1_000_000;
                d["machines"][1]["buildPrice"]["prestigeGrowthPpm"] = 1_000_001;
                d["machines"][1]["buildPrice"]["cap"] = 1_000_000_000_000L;
                d["runStart"]["startingCash"] = 0;
                d["runStart"]["manualMining"]["startWindowMs"] = 0;
            });
            watch.Stop();
            Assert.That(Rule(diagnostics, "start.reachable").Any(), Is.True, "the first Miner costs 10 and the funds are 0");
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(5000));
        }
    }
}
