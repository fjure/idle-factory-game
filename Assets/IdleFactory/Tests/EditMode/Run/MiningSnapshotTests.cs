using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.UI;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// T359 scope decision (#386): the read-only <see cref="RunSnapshots.Mining"/> model for the ore vein. It is null without manual
    /// mining, and every field agrees with what <see cref="RunCommands.Mine"/> does: the effective milli-yield (with Research levels),
    /// the remainder, the cap and the activations counted in the current timeline second. The view adapter reads the same values.
    /// </summary>
    public sealed class MiningSnapshotTests
    {
        private const string Ore = "iron.ore";
        private const long Ms = RunSession.TicksPerMs;

        private static ManualMiningDef Mining() => new ManualMiningDef(Ore, 100, 10, 60_000, 10, 1_500_000, 100_000);

        private static ResearchDef HandDrill() => new ResearchDef("research.hand-drill", "Hand Drill", 3, new Curve(1, 1_000_000, "ceil"), new ResearchPrerequisite[0],
            new EffectDef("manualYield", "none", null, 1_100_000));

        private static RunCommandFixtures.Rig NewRig(bool withMining = true, ResearchDef[] research = null, long rp = 0)
        {
            var options = new RunCommandFixtures.Options { Mining = withMining ? Mining() : null, Research = research };
            var rig = RunCommandFixtures.NewRig(options, 100_000, rp: rp);
            rig.Advance(0);
            return rig;
        }

        private static void NextSecond(RunCommandFixtures.Rig rig)
        {
            rig.Advance((1000 - rig.Session.TimelineUtcMs % 1000) * Ms);
            Assert.That(rig.Session.TimelineUtcMs % 1000, Is.Zero);
        }

        [Test]
        public void ContentWithoutManualMiningHasNoSnapshotAndNoVeinSource()
        {
            var rig = NewRig(withMining: false);
            Assert.That(RunSnapshots.Mining(rig.Session), Is.Null);
            Assert.That(new RunOreVeinSource(rig.Session, rig.Commands).IsAvailable, Is.False);
        }

        [Test]
        public void ARunStartsWithTheBaseYieldAnEmptyRemainderAndTheWholeCap()
        {
            var rig = NewRig();
            var snapshot = RunSnapshots.Mining(rig.Session);
            Assert.That(snapshot.ResourceId, Is.EqualTo(Ore));
            Assert.That(snapshot.EffectiveMilliYield, Is.EqualTo(100));
            Assert.That(snapshot.RemainderMilli, Is.Zero);
            Assert.That(snapshot.CapPerSecond, Is.EqualTo(10));
            Assert.That(snapshot.CountedInCurrentSecond, Is.Zero);
            Assert.That(snapshot.CapRemaining, Is.EqualTo(10));
        }

        [Test]
        public void TheSnapshotFollowsTheMineResultsRemainderCounterAndSecond()
        {
            var rig = NewRig();
            NextSecond(rig);
            var first = rig.Commands.Mine(3);
            Assert.That(first.IsSuccess, Is.True);
            var snapshot = RunSnapshots.Mining(rig.Session);
            Assert.That(snapshot.CountedInCurrentSecond, Is.EqualTo(first.Value.Counted).And.EqualTo(3));
            Assert.That(snapshot.RemainderMilli, Is.EqualTo(300));
            Assert.That(snapshot.CapRemaining, Is.EqualTo(7));

            var second = rig.Commands.Mine(10);
            Assert.That(second.Value.Counted, Is.EqualTo(7));
            Assert.That(second.Value.Ignored, Is.EqualTo(3));
            Assert.That(second.Value.Units, Is.EqualTo(1), "300 + 7 x 100 = 1,000 milli");
            snapshot = RunSnapshots.Mining(rig.Session);
            Assert.That(snapshot.RemainderMilli, Is.Zero);
            Assert.That(snapshot.CountedInCurrentSecond, Is.EqualTo(10));
            Assert.That(snapshot.CapRemaining, Is.Zero);
            Assert.That(snapshot.RemainderMilli, Is.EqualTo(rig.State.MiningRemainderMilli));

            NextSecond(rig);
            snapshot = RunSnapshots.Mining(rig.Session);
            Assert.That(snapshot.CountedInCurrentSecond, Is.Zero, "the counter of another second counts as 0, as Mine reads it");
            Assert.That(snapshot.CapRemaining, Is.EqualTo(10));
        }

        [Test]
        public void ARejectedMineChangesNothingInTheSnapshot()
        {
            var rig = NewRig();
            NextSecond(rig);
            Assert.That(rig.Commands.Mine(9).IsSuccess, Is.True);
            Assert.That(rig.State.Inventory.TryMine(Ore, rig.State.Inventory.Free), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok));
            var before = RunSnapshots.Mining(rig.Session);
            var rejected = rig.Commands.Mine(1);
            Assert.That(rejected.IsSuccess, Is.False);
            Assert.That(rejected.ReasonCode, Is.EqualTo(RunReasons.StorageFull));
            var after = RunSnapshots.Mining(rig.Session);
            Assert.That(after.RemainderMilli, Is.EqualTo(before.RemainderMilli).And.EqualTo(900));
            Assert.That(after.CountedInCurrentSecond, Is.EqualTo(before.CountedInCurrentSecond).And.EqualTo(9));
        }

        [Test]
        public void EveryManualYieldResearchLevelShowsInTheSnapshotAndMatchesWhatMineCredits()
        {
            var rig = NewRig(research: new[] { HandDrill() }, rp: 10);
            var source = new RunOreVeinSource(rig.Session, rig.Commands);
            var yields = new List<long> { RunSnapshots.Mining(rig.Session).EffectiveMilliYield };
            Assert.That(source.YieldMilli, Is.EqualTo(100));
            for (var level = 1; level <= 3; level++)
            {
                Assert.That(rig.Commands.PurchaseResearch("research.hand-drill").IsSuccess, Is.True);
                yields.Add(RunSnapshots.Mining(rig.Session).EffectiveMilliYield);
                Assert.That(source.YieldMilli, Is.EqualTo(yields[level]), "the view source follows an upgrade");
            }

            Assert.That(yields, Is.EqualTo(new long[] { 100, 110, 121, 133 }), "x1.1 per level with a floor per level");

            NextSecond(rig);
            var mined = rig.Commands.Mine(10);
            Assert.That(mined.Value.Units, Is.EqualTo(1), "10 x 133 = 1,330 milli");
            Assert.That(RunSnapshots.Mining(rig.Session).RemainderMilli, Is.EqualTo(330));
            Assert.That(source.RemainderMilli, Is.EqualTo(330));
        }

        [Test]
        public void TheSourceMinesThroughTheCommandAndReportsItsOutcome()
        {
            var rig = NewRig();
            NextSecond(rig);
            var source = new RunOreVeinSource(rig.Session, rig.Commands);
            var outcome = source.Mine(12);
            Assert.That(outcome.Success, Is.True);
            Assert.That(outcome.Counted, Is.EqualTo(10));
            Assert.That(outcome.Ignored, Is.EqualTo(2));
            Assert.That(outcome.Units, Is.EqualTo(1));
            Assert.That(source.ActivationsPerSecond, Is.EqualTo(10));
            Assert.That(source.ResourceLabel, Is.Not.Empty);
            Assert.That(source.Mine(0).ReasonCode, Is.EqualTo(RunReasons.CommandInvalid));
        }
    }
}
