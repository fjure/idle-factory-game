using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// T332 (FR-018, F5-R07, PF-8): the goal line. Fixture: threshold 1,000 (legacy, no growth) and milestones at 25 %, 50 % and 75 %
    /// (250, 500, 750). The Factory Value of the bare rig is its Cash. Covers the next goal and its index, the target, the floored
    /// percentage, "Factory Sale available", "all reached", threshold-only content, the rising threshold, the floor of each milestone,
    /// skipped invalid milestones, an underivable threshold, the cache keyed on goal crossing (up and down), and the Run2Eligible event.
    /// </summary>
    public sealed class GoalsSnapshotTests
    {
        private static MilestoneDef[] Quarters() => new[] { new MilestoneDef(250_000), new MilestoneDef(500_000), new MilestoneDef(750_000) };

        private static RunCommandFixtures.Options Opts(MilestoneDef[] milestones, PrestigeDef prestige = null) => new RunCommandFixtures.Options
        {
            Prestige = prestige ?? new PrestigeDef(1_000, "sqrt", 10, 1_000),
            PrestigeMilestones = milestones,
        };

        private static RunSession NewSession(RunCommandFixtures.Options options, long cash, long prestigeCount = 0, IRunEventSink sink = null)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(options, cash);
            return new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog, 0, prestigeCount), RunCommandFixtures.StartUtcMs,
                sink ?? NoOpRunEventSink.Instance);
        }

        /// <summary>Sets Cash through the revision protocol of a command, so the session revisions move as in play.</summary>
        private static void SetCash(RunSession session, long cash)
        {
            long current = session.State.Cash;
            if (cash == current) return;
            session.BeginCommand();
            Assert.That(cash > current ? session.State.TryAddCash(cash - current) : session.State.TrySpendCash(current - cash), Is.EqualTo(RunStateResult.Ok));
            Assert.That(session.EndCommand(0, false).IsSuccess, Is.True);
        }

        private static GoalsSnapshot GoalsAt(long cash, MilestoneDef[] milestones = null, PrestigeDef prestige = null, long prestigeCount = 0)
        {
            RunSession session = NewSession(Opts(milestones, prestige), cash, prestigeCount);
            GoalsSnapshot snapshot = RunSnapshots.Goals(session);
            Assert.That(snapshot.FactoryValue, Is.EqualTo(RunCommandFixtures.FactoryValue(session.State)), "the snapshot reads the Factory Value of the run");
            return snapshot;
        }

        [Test]
        public void AFreshRunShowsTheFirstMilestoneAsTheNextGoal()
        {
            GoalsSnapshot g = GoalsAt(0, Quarters());
            Assert.That(g.Available, Is.True);
            Assert.That(g.Next, Is.Zero);
            Assert.That(g.NextIsThreshold, Is.False);
            Assert.That(g.Target, Is.EqualTo(250));
            Assert.That(g.Reached, Is.False);
            Assert.That(g.PercentFloored, Is.Zero);
            Assert.That(g.FactorySaleAvailable, Is.False);
            Assert.That(g.AllReached, Is.False);
            Assert.That(g.MilestoneCount, Is.EqualTo(3));
            Assert.That(g.ReachedMilestones, Is.Zero);
            Assert.That(g.Threshold, Is.EqualTo(1_000));
        }

        [TestCase(1L, 0)]
        [TestCase(124L, 49)]
        [TestCase(125L, 50)]
        [TestCase(249L, 99)]
        public void ThePercentageIsFlooredAndNeverRoundedUp(long cash, int expected)
        {
            GoalsSnapshot g = GoalsAt(cash, Quarters());
            Assert.That(g.Next, Is.Zero);
            Assert.That(g.PercentFloored, Is.EqualTo(expected));
            Assert.That(g.PercentAt(cash), Is.EqualTo(expected), "the hot read agrees with the built value");
        }

        [Test]
        public void ReachingAMilestoneMakesTheNextOneTheGoalAtTheExactValue()
        {
            Assert.That(GoalsAt(249, Quarters()).Next, Is.Zero);
            GoalsSnapshot atFirst = GoalsAt(250, Quarters());
            Assert.That(atFirst.Next, Is.EqualTo(1));
            Assert.That(atFirst.Target, Is.EqualTo(500));
            Assert.That(atFirst.ReachedMilestones, Is.EqualTo(1));
            Assert.That(atFirst.PercentFloored, Is.EqualTo(50));
            Assert.That(atFirst.Reached, Is.False, "a passed milestone is replaced by the next goal, not shown as reached");

            GoalsSnapshot atSecond = GoalsAt(500, Quarters());
            Assert.That(atSecond.Next, Is.EqualTo(2));
            Assert.That(atSecond.Target, Is.EqualTo(750));
            Assert.That(atSecond.ReachedMilestones, Is.EqualTo(2));
        }

        [Test]
        public void PassingTwoMilestonesAtOnceSkipsToTheThird()
        {
            GoalsSnapshot g = GoalsAt(600, Quarters());
            Assert.That(g.Next, Is.EqualTo(2));
            Assert.That(g.Target, Is.EqualTo(750));
            Assert.That(g.ReachedMilestones, Is.EqualTo(2));
            Assert.That(g.AllReached, Is.False);
        }

        [Test]
        public void AfterTheLastMilestoneTheThresholdIsTheGoalAndAllMilestonesAreReached()
        {
            GoalsSnapshot g = GoalsAt(750, Quarters());
            Assert.That(g.Next, Is.EqualTo(3).And.EqualTo(g.MilestoneCount));
            Assert.That(g.NextIsThreshold, Is.True);
            Assert.That(g.Target, Is.EqualTo(1_000));
            Assert.That(g.Reached, Is.False);
            Assert.That(g.AllReached, Is.True);
            Assert.That(g.FactorySaleAvailable, Is.False);
            Assert.That(g.ReachedMilestones, Is.EqualTo(3));
            Assert.That(g.PercentFloored, Is.EqualTo(75));
        }

        [TestCase(1_000L)]
        [TestCase(1_001L)]
        [TestCase(5_000L)]
        public void AtOrAboveTheThresholdFactorySaleIsAvailableAndTheGoalStaysTheThreshold(long cash)
        {
            GoalsSnapshot g = GoalsAt(cash, Quarters());
            Assert.That(g.FactorySaleAvailable, Is.True);
            Assert.That(g.Reached, Is.True);
            Assert.That(g.AllReached, Is.True);
            Assert.That(g.NextIsThreshold, Is.True);
            Assert.That(g.Target, Is.EqualTo(1_000));
            Assert.That(g.PercentFloored, Is.EqualTo(100));
        }

        [Test]
        public void JustBelowTheThresholdFactorySaleIsNotAvailable()
        {
            GoalsSnapshot g = GoalsAt(999, Quarters());
            Assert.That(g.FactorySaleAvailable, Is.False);
            Assert.That(g.Reached, Is.False);
            Assert.That(g.PercentFloored, Is.EqualTo(99));
        }

        [Test]
        public void ContentWithoutMilestonesHasTheThresholdAsTheOnlyGoal()
        {
            GoalsSnapshot g = GoalsAt(400, new MilestoneDef[0]);
            Assert.That(g.MilestoneCount, Is.Zero);
            Assert.That(g.Next, Is.Zero);
            Assert.That(g.NextIsThreshold, Is.True);
            Assert.That(g.Target, Is.EqualTo(1_000));
            Assert.That(g.PercentFloored, Is.EqualTo(40));
            Assert.That(g.Reached, Is.False);
            Assert.That(g.FactorySaleAvailable, Is.False);

            GoalsSnapshot at = GoalsAt(1_000, new MilestoneDef[0]);
            Assert.That(at.Reached, Is.True);
            Assert.That(at.FactorySaleAvailable, Is.True);
        }

        [Test]
        public void TheThresholdRisesWithThePrestigeCountAndStaysAtMaxIndex()
        {
            var growing = new PrestigeDef(1_000, "sqrt", 10, 1_000, new ThresholdDef(1_000, 2_000_000, 5));
            var halves = new[] { new MilestoneDef(250_000), new MilestoneDef(500_000), new MilestoneDef(750_000) };
            long[] expected = { 1_000, 2_000, 4_000, 8_000, 16_000, 32_000, 32_000, 32_000 };
            for (int count = 0; count < expected.Length; count++)
            {
                GoalsSnapshot g = GoalsAt(0, halves, growing, count);
                Assert.That(g.Threshold, Is.EqualTo(expected[count]), "prestige count " + count);
                Assert.That(g.Target, Is.EqualTo(expected[count] / 4), "the first milestone scales with the threshold");
            }

            GoalsSnapshot near = GoalsAt(3_999, halves, growing, 2);
            Assert.That(near.Next, Is.EqualTo(3));
            Assert.That(near.NextIsThreshold, Is.True);
            Assert.That(near.Target, Is.EqualTo(4_000));
            Assert.That(near.PercentFloored, Is.EqualTo(99));
        }

        [Test]
        public void EachMilestoneIsTheFlooredFractionOfTheThreshold()
        {
            var prestige = new PrestigeDef(1_001, "sqrt", 10, 1_001);
            GoalsSnapshot g = GoalsAt(0, new[] { new MilestoneDef(333_333), new MilestoneDef(666_667) }, prestige);
            Assert.That(g.Target, Is.EqualTo(333), "floor(1,001 x 0.333333)");
            GoalsSnapshot second = GoalsAt(333, new[] { new MilestoneDef(333_333), new MilestoneDef(666_667) }, prestige);
            Assert.That(second.Target, Is.EqualTo(667), "floor(1,001 x 0.666667)");
        }

        [Test]
        public void MilestonesThatAreNotStrictlyAscendingPositiveAndBelowTheThresholdAreSkipped()
        {
            var prestige = new PrestigeDef(10, "sqrt", 10, 10);
            // 10,000 ppm of 10 floors to 0 (skipped), 100,000 is 1, 150,000 floors to 1 again (skipped), 999,999 is 9, 1,000,000 is the threshold (skipped).
            var raw = new[] { new MilestoneDef(10_000), new MilestoneDef(100_000), new MilestoneDef(150_000), new MilestoneDef(999_999), new MilestoneDef(1_000_000) };
            GoalsSnapshot g = GoalsAt(0, raw, prestige);
            Assert.That(g.MilestoneCount, Is.EqualTo(2));
            Assert.That(g.Target, Is.EqualTo(1));
            Assert.That(GoalsAt(1, raw, prestige).Target, Is.EqualTo(9));
            GoalsSnapshot last = GoalsAt(9, raw, prestige);
            Assert.That(last.NextIsThreshold, Is.True);
            Assert.That(last.Target, Is.EqualTo(10));
        }

        [Test]
        public void AThresholdAbove10Pow18MakesTheSnapshotUnavailableWithoutAGoal()
        {
            var prestige = new PrestigeDef(1_000, "sqrt", 10, 1_000, new ThresholdDef(ExactBound.Value, 2_000_000, 5));
            GoalsSnapshot g = GoalsAt(10, Quarters(), prestige, 1);
            Assert.That(g.Available, Is.False);
            Assert.That(g.Target, Is.Zero);
            Assert.That(g.Reached, Is.False);
            Assert.That(g.FactorySaleAvailable, Is.False);
            Assert.That(g.PercentFloored, Is.Zero);
            Assert.That(g.PercentAt(10), Is.Zero);
        }

        [Test]
        public void TheFactoryValueAtTheExactBoundDoesNotOverflowThePercentage()
        {
            var prestige = new PrestigeDef(1_000, "sqrt", 10, 1_000, new ThresholdDef(ExactBound.Value, 1_000_000, 5));
            GoalsSnapshot g = GoalsAt(ExactBound.Value - 1, new MilestoneDef[0], prestige);
            Assert.That(g.Target, Is.EqualTo(ExactBound.Value));
            Assert.That(g.PercentFloored, Is.EqualTo(99), "floor(100 x (10^18 - 1) / 10^18) in 128-bit math");
            Assert.That(g.PercentAt(ExactBound.Value), Is.EqualTo(100));
        }

        // ------------------------------------------------------------------------------------------------
        // The cache keyed on goal crossing (PF-8)
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void ReadsWithUnchangedRevisionsReturnTheSameInstance()
        {
            RunSession session = NewSession(Opts(Quarters()), 10);
            var tracker = new GoalsTracker();
            GoalsSnapshot first = tracker.Current(session, 0);
            Assert.That(tracker.Current(session, 0), Is.SameAs(first));
            Assert.That(tracker.Rebuilds, Is.EqualTo(1));
        }

        [Test]
        public void FactoryValueChangesInsideTheSameGoalRangeNeverRebuildTheSnapshot()
        {
            RunSession session = NewSession(Opts(Quarters()), 10);
            var tracker = new GoalsTracker();
            GoalsSnapshot first = tracker.Current(session, 0);
            for (long cash = 11; cash < 250; cash += 7)
            {
                SetCash(session, cash);
                Assert.That(tracker.Current(session, 0), Is.SameAs(first), "cash " + cash);
            }

            Assert.That(tracker.Rebuilds, Is.EqualTo(1));
            Assert.That(first.PercentFloored, Is.EqualTo(4), "the cached value is as of the build; live text uses PercentAt");
            Assert.That(first.PercentAt(session.State.Cash), Is.EqualTo((int)(session.State.Cash * 100 / 250)));
        }

        [Test]
        public void CrossingAGoalRebuildsOnceAndTheNextGoalReplacesIt()
        {
            RunSession session = NewSession(Opts(Quarters()), 10);
            var tracker = new GoalsTracker();
            GoalsSnapshot before = tracker.Current(session, 0);
            SetCash(session, 249);
            Assert.That(tracker.Current(session, 0), Is.SameAs(before));
            SetCash(session, 250);
            GoalsSnapshot after = tracker.Current(session, 0);
            Assert.That(after, Is.Not.SameAs(before));
            Assert.That(after.Next, Is.EqualTo(1));
            Assert.That(after.Target, Is.EqualTo(500));
            Assert.That(tracker.Rebuilds, Is.EqualTo(2));
            SetCash(session, 260);
            Assert.That(tracker.Current(session, 0), Is.SameAs(after));
        }

        [Test]
        public void ADropBelowThePassedGoalRebuildsSoTheGoalLineFollowsTheValueDown()
        {
            RunSession session = NewSession(Opts(Quarters()), 300);
            var tracker = new GoalsTracker();
            Assert.That(tracker.Current(session, 0).Next, Is.EqualTo(1));
            SetCash(session, 249);
            GoalsSnapshot down = tracker.Current(session, 0);
            Assert.That(down.Next, Is.Zero);
            Assert.That(down.Target, Is.EqualTo(250));
            Assert.That(tracker.Rebuilds, Is.EqualTo(2));
        }

        [Test]
        public void ThePrestigeCountOfTheSessionIsPartOfTheCacheKey()
        {
            var growing = new PrestigeDef(1_000, "sqrt", 10, 1_000, new ThresholdDef(1_000, 2_000_000, 5));
            RunSession session = NewSession(Opts(new MilestoneDef[0], growing), 10);
            var tracker = new GoalsTracker();
            Assert.That(tracker.Current(session, 0).Target, Is.EqualTo(1_000));
            Assert.That(session.Permanent.TryIncrementPrestige(), Is.EqualTo(RunStateResult.Ok));
            session.BeginCommand();
            Assert.That(session.EndCommand(0, true).IsSuccess, Is.True);
            GoalsSnapshot next = tracker.Current(session, 0);
            Assert.That(next.Target, Is.EqualTo(2_000));
            Assert.That(tracker.Rebuilds, Is.EqualTo(2));
        }

        // ------------------------------------------------------------------------------------------------
        // The milestone event
        // ------------------------------------------------------------------------------------------------

        private sealed class MilestoneSink : IRunEventSink
        {
            public readonly List<RunMilestoneEvent> Milestones = new List<RunMilestoneEvent>();
            public int Others;
            public void OnHeader(in RunHeaderEvent e) => Others++;
            public void OnSample(in RunSampleEvent e) => Others++;
            public void OnMilestone(in RunMilestoneEvent e) => Milestones.Add(e);
            public void OnSale(in RunSaleEvent e) => Others++;
            public void OnMining(in RunMiningEvent e) => Others++;
            public void OnContract(in RunContractEvent e) => Others++;
            public void OnPrestige(in RunPrestigeEvent e) => Others++;
            public void OnOfflineReturn(in RunOfflineReturnEvent e) => Others++;
            public void OnBottleneck(in RunBottleneckEvent e) => Others++;
            public void OnPurchase(in RunPurchaseEvent e) => Others++;
            public void OnClock(in RunClockEvent e) => Others++;
            public void OnLifecycle(in RunLifecycleEvent e) => Others++;
        }

        [Test]
        public void CrossingTheThresholdInTheFirstRunRaisesRun2EligibleOnceWithThePlayTime()
        {
            var sink = new MilestoneSink();
            RunSession session = NewSession(Opts(Quarters()), 10, 0, sink);
            var tracker = new GoalsTracker();
            tracker.Current(session, 5);
            SetCash(session, 800);
            tracker.Current(session, 60);
            Assert.That(sink.Milestones, Is.Empty, "passing content milestones raises nothing; only eligibility is a logged milestone");

            SetCash(session, 1_000);
            tracker.Current(session, 90);
            Assert.That(sink.Milestones.Count, Is.EqualTo(1));
            Assert.That(sink.Milestones[0].Kind, Is.EqualTo(RunMilestoneKind.Run2Eligible));
            Assert.That(sink.Milestones[0].PlaySeconds, Is.EqualTo(90));

            SetCash(session, 900);
            tracker.Current(session, 100);
            SetCash(session, 1_200);
            tracker.Current(session, 110);
            Assert.That(sink.Milestones.Count, Is.EqualTo(1), "one event per run, however often the value crosses");
            Assert.That(sink.Others, Is.Zero);
        }

        [Test]
        public void ATrackerSeededAtOrAboveTheThresholdRaisesNothing()
        {
            var sink = new MilestoneSink();
            RunSession session = NewSession(Opts(Quarters()), 5_000, 0, sink);
            var tracker = new GoalsTracker();
            Assert.That(tracker.Current(session, 10).FactorySaleAvailable, Is.True);
            SetCash(session, 6_000);
            tracker.Current(session, 20);
            SetCash(session, 100);
            tracker.Current(session, 30);
            SetCash(session, 7_000);
            tracker.Current(session, 40);
            Assert.That(sink.Milestones, Is.Empty, "a load does not re-notify, and a seeded run counts as already announced");
        }

        [Test]
        public void ALaterRunDoesNotRaiseRun2Eligible()
        {
            var sink = new MilestoneSink();
            RunSession session = NewSession(Opts(Quarters()), 10, 1, sink);
            var tracker = new GoalsTracker();
            tracker.Current(session, 0);
            SetCash(session, 2_000);
            Assert.That(tracker.Current(session, 1).FactorySaleAvailable, Is.True);
            Assert.That(sink.Milestones, Is.Empty);
        }

        [Test]
        public void AFailingSinkNeverBreaksTheGoalLineOrTheSimulation()
        {
            RunSession session = NewSession(Opts(Quarters()), 10, 0, new ThrowingMilestoneSink());
            var tracker = new GoalsTracker();
            tracker.Current(session, 0);
            SetCash(session, 1_000);
            GoalsSnapshot g = null;
            Assert.DoesNotThrow(() => g = tracker.Current(session, 1));
            Assert.That(g.FactorySaleAvailable, Is.True);
            Assert.That(session.DroppedEvents, Is.EqualTo(1));
            Assert.That(session.State.Cash, Is.EqualTo(1_000));
        }

        private sealed class ThrowingMilestoneSink : IRunEventSink
        {
            public void OnHeader(in RunHeaderEvent e) { }
            public void OnSample(in RunSampleEvent e) { }
            public void OnMilestone(in RunMilestoneEvent e) => throw new System.InvalidOperationException("sink failure");
            public void OnSale(in RunSaleEvent e) { }
            public void OnMining(in RunMiningEvent e) { }
            public void OnContract(in RunContractEvent e) { }
            public void OnPrestige(in RunPrestigeEvent e) { }
            public void OnOfflineReturn(in RunOfflineReturnEvent e) { }
            public void OnBottleneck(in RunBottleneckEvent e) { }
            public void OnPurchase(in RunPurchaseEvent e) { }
            public void OnClock(in RunClockEvent e) { }
            public void OnLifecycle(in RunLifecycleEvent e) { }
        }
    }
}
