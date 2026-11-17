using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T537 (T536 Architect MAJOR): the hub attention feed no longer re-implements the Research "can be bought" rule and the Overdrive "ready" rule. Both
    /// predicates live next to the snapshot builders (<see cref="RunSnapshots.AnyResearchAffordable"/>, <see cref="RunSnapshots.OverdriveReady"/>) and the
    /// builders use the same code, so a drift is impossible by construction. These tests prove the parity across every state anyway, including the maximum
    /// level where the cost table has no entry to read.
    /// </summary>
    public sealed class RunSnapshotPredicateParityTests
    {
        private const long Ms = RunSession.TicksPerMs;
        private const string Speed = "research.speed";
        private const string Deep = "research.deep";

        private static ResearchDef[] Research() => new[]
        {
            new ResearchDef(Speed, "Speed", 2, new Curve(2, 1_500_000, "ceil"), new ResearchPrerequisite[0],
                new EffectDef("machineSpeed", "allMachines", null, 1_250_000)),
            new ResearchDef(Deep, "Deep", 1, new Curve(4, 1_000_000, "ceil"), new[] { new ResearchPrerequisite(Speed, 2) },
                new EffectDef("salePrice", "allResources", null, 1_100_000)),
        };

        private static RunCommandFixtures.Rig ResearchRig(long rp)
        {
            var options = new RunCommandFixtures.Options { Research = Research() };
            var rig = RunCommandFixtures.NewRig(options, 100_000, rp: rp);
            rig.Advance(0);
            return rig;
        }

        private static void AssertResearchParity(RunCommandFixtures.Rig rig, string state)
        {
            var snapshot = RunSnapshots.Research(rig.Session);
            Assert.That(RunSnapshots.AnyResearchAffordable(rig.Session), Is.EqualTo(snapshot.Nodes.Any(node => node.CanPurchase)), state + ": the feed predicate equals the snapshot");
            foreach (var node in snapshot.Nodes)
            {
                var def = rig.Session.Catalog.Research[node.ResearchId];
                Assert.That(RunSnapshots.CanPurchaseResearch(rig.Session.State, def), Is.EqualTo(node.CanPurchase), state + ": " + node.ResearchId);
            }
        }

        [Test]
        public void TheResearchPredicateEqualsTheSnapshotAtEveryStepIncludingTheMaximumLevel()
        {
            var poor = ResearchRig(rp: 0);
            AssertResearchParity(poor, "no RP");
            Assert.That(RunSnapshots.AnyResearchAffordable(poor.Session), Is.False);

            // Enough RP to buy everything, one purchase at a time, until every node is at its maximum level (no cost entry to read there).
            var rig = ResearchRig(rp: 1_000);
            AssertResearchParity(rig, "rich");
            Assert.That(RunSnapshots.AnyResearchAffordable(rig.Session), Is.True);
            for (var step = 0; step < 3; step++)
            {
                var next = RunSnapshots.Research(rig.Session).Nodes.First(node => node.CanPurchase);
                Assert.That(rig.Commands.PurchaseResearch(next.ResearchId).IsSuccess, Is.True, next.ResearchId);
                AssertResearchParity(rig, "after purchase " + (step + 1));
            }

            Assert.That(RunSnapshots.Research(rig.Session).Nodes.All(node => node.Level == node.MaxLevel), Is.True, "everything is at its maximum level");
            Assert.That(RunSnapshots.AnyResearchAffordable(rig.Session), Is.False, "nothing to buy at the maximum level, and no read past the cost table");
        }

        [Test]
        public void ThePrerequisiteGatesBothTheSnapshotAndThePredicate()
        {
            var rig = ResearchRig(rp: 3);
            var deep = rig.Session.Catalog.Research[Deep];
            Assert.That(RunSnapshots.CanPurchaseResearch(rig.Session.State, deep), Is.False, "Deep needs Speed level 2 first");
            AssertResearchParity(rig, "prerequisite open");
            Assert.That(rig.Commands.PurchaseResearch(Deep).IsSuccess, Is.False, "the command rejects what the predicate rejects");
        }

        [Test]
        public void TheOverdrivePredicateEqualsTheSnapshotInEveryStage()
        {
            var locked = new RunCommandFixtures.Options { OverdriveLocked = true };
            var lockedRig = RunCommandFixtures.NewRig(locked, 100_000);
            lockedRig.Advance(0);
            AssertOverdriveParity(lockedRig, "locked", OverdriveStage.Locked);

            var rig = RunCommandFixtures.NewRig(new RunCommandFixtures.Options(), 100_000);
            rig.Advance(0);
            AssertOverdriveParity(rig, "ready", OverdriveStage.Ready);

            Assert.That(rig.Commands.ActivateOverdrive().IsSuccess, Is.True);
            rig.Advance(10_000 * Ms);
            AssertOverdriveParity(rig, "active", OverdriveStage.Active);

            rig.Advance(20_000 * Ms);
            AssertOverdriveParity(rig, "cooling down", OverdriveStage.CoolingDown);

            rig.Advance(300_000 * Ms);
            AssertOverdriveParity(rig, "ready again", OverdriveStage.Ready);
        }

        private static void AssertOverdriveParity(RunCommandFixtures.Rig rig, string state, OverdriveStage expected)
        {
            var status = RunSnapshots.Overdrive(rig.Session);
            Assert.That(status.State, Is.EqualTo(expected), state);
            Assert.That(RunSnapshots.OverdriveStageAt(rig.Session.State, rig.Session.TimelineUtcMs), Is.EqualTo(status.State), state + ": one stage rule");
            Assert.That(RunSnapshots.OverdriveReady(rig.Session), Is.EqualTo(status.State == OverdriveStage.Ready), state + ": the feed predicate equals the snapshot");
        }
    }
}
