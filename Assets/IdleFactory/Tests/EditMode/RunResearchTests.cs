using System;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// PurchaseResearch and the ResearchSnapshot (T101, contracts/run-application.md, FR-018, FR-019, US3 scenario 3,
    /// OD-8): prerequisites, level cap, RP as one checked spend, the effect applied at once with a rescale, per-level
    /// application in ordinal order, the unlock evaluation at the command settle (T140 handoff), and atomic rejection.
    ///
    /// Fixture Research: research.speed (machineSpeed x1.25, 3 levels, cost 2, 3, 5 RP by Curve(2, 1.5, ceil)),
    /// research.deep (requires speed level 2; salePrice x1.1, 2 levels), research.miner-output (machineOutput x2 on the
    /// miner, 3 levels, 1 RP each), research.huge (a speed factor that overflows at level 2).
    /// </summary>
    public sealed class RunResearchTests
    {
        private const string Speed = "research.speed";
        private const string Deep = "research.deep";
        private const string MinerOutput = "research.miner-output";
        private const string Huge = "research.huge";

        private static ResearchDef[] Research() => new[]
        {
            new ResearchDef(Speed, "Speed", 3, new Curve(2, 1_500_000, "ceil"), new ResearchPrerequisite[0],
                new EffectDef("machineSpeed", "allMachines", null, 1_250_000)),
            new ResearchDef(Deep, "Deep", 2, new Curve(4, 1_000_000, "ceil"), new[] { new ResearchPrerequisite(Speed, 2) },
                new EffectDef("salePrice", "allResources", null, 1_100_000)),
            new ResearchDef(MinerOutput, "Miner Output", 3, new Curve(1, 1_000_000, "ceil"), new ResearchPrerequisite[0],
                new EffectDef("machineOutput", "machine", RunCommandFixtures.Miner, 2)),
            new ResearchDef(Huge, "Huge", 3, new Curve(1, 1_000_000, "ceil"), new ResearchPrerequisite[0],
                new EffectDef("machineSpeed", "allMachines", null, 100_000_000_000_000L)),
        };

        private static RunCommandFixtures.Rig Rig(long rp = 100, bool withMiner = false, bool discount = false, RunCommandFixtures.Options o = null)
        {
            o = o ?? new RunCommandFixtures.Options();
            o.Research = Research();
            o.DiscountResearch |= discount;
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(o, 100_000, rp: rp);
            rig.Advance(0);
            if (withMiner) Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            return rig;
        }

        private static ResearchPurchaseResult Buy(RunCommandFixtures.Rig rig, string id)
        {
            RunCommandResult<ResearchPurchaseResult> r = rig.Commands.PurchaseResearch(id);
            Assert.That(r.IsSuccess, Is.True, id + ": " + r.ReasonCode);
            return r.Value;
        }

        private static MachineInstance Miner(RunCommandFixtures.Rig rig) => rig.State.FindMachine(rig.Id(1));

        private static string Full(RunCommandFixtures.Rig rig) =>
            RunCommandFixtures.Snapshot(rig.State) + "cash=" + rig.State.Cash + " rp=" + rig.State.Rp + " tl=" + rig.Session.ActiveTicksSinceAnchor;

        private static void AssertRejected<T>(RunCommandFixtures.Rig rig, Func<RunCommandResult<T>> command, string code)
        {
            string before = Full(rig);
            long structure = rig.Session.StructureRevision, quantity = rig.Session.QuantityRevision;

            RunCommandResult<T> result = command();

            Assert.That(result.IsSuccess, Is.False, "expected " + code);
            Assert.That(result.ReasonCode, Is.EqualTo(code));
            Assert.That(result.Value, Is.EqualTo(default(T)));
            Assert.That(Full(rig), Is.EqualTo(before), "a rejection never changes state");
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure));
            Assert.That(rig.Session.QuantityRevision, Is.EqualTo(quantity));
        }

        // ------------------------------------------------------------------------------------------------
        // Purchase
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Purchase_SpendsTheLevelCost_RaisesTheLevel_AndReportsTheNext()
        {
            var rig = Rig(rp: 20);
            long structure = rig.Session.StructureRevision, quantity = rig.Session.QuantityRevision;

            ResearchPurchaseResult r = Buy(rig, Speed);

            Assert.That(r.NewLevel, Is.EqualTo(1));
            Assert.That(r.RpPaid, Is.EqualTo(2));
            Assert.That(r.HasNext, Is.True);
            Assert.That(r.NextCost, Is.EqualTo(3), "ceil(2 x 1.5)");
            Assert.That(rig.State.Rp, Is.EqualTo(18));
            Assert.That(rig.State.ResearchLevel(Speed), Is.EqualTo(1));
            Assert.That(rig.Session.StructureRevision, Is.GreaterThan(structure));
            Assert.That(rig.Session.QuantityRevision, Is.GreaterThan(quantity), "RP changed");

            Buy(rig, Speed);
            ResearchPurchaseResult last = Buy(rig, Speed);
            Assert.That(last.NewLevel, Is.EqualTo(3));
            Assert.That(last.RpPaid, Is.EqualTo(5), "ceil(3 x 1.5)");
            Assert.That(last.HasNext, Is.False);
            Assert.That(last.NextCost, Is.Zero);
            Assert.That(rig.State.Rp, Is.EqualTo(20 - 2 - 3 - 5));
        }

        [Test]
        public void Purchase_AppliesTheEffectImmediately_WithARescaleKeepingTheFraction()
        {
            var rig = Rig(withMiner: true);
            rig.Advance(10_000_000);
            MachineInstance miner = Miner(rig);
            Assert.That(miner.ProgressTicks, Is.EqualTo(10_000_000));

            Buy(rig, Speed);

            Assert.That(miner.CycleDurationTicks, Is.EqualTo(16_000_000), "20,000,000 / 1.25");
            Assert.That(miner.ProgressTicks, Is.EqualTo(8_000_000), "half of the new cycle");
        }

        [Test]
        public void Purchase_AppliesTheValuePerLevel_InCompoundedSteps()
        {
            var rig = Rig(withMiner: true);
            MachineInstance miner = Miner(rig);

            Buy(rig, Speed);
            Buy(rig, Speed);

            Assert.That(miner.CycleDurationTicks, Is.EqualTo(12_800_000), "x1.25 twice = 1.5625: ceil(20,000,000 x 10^6 / 1,562,500)");
            Buy(rig, Speed);
            Assert.That(miner.CycleDurationTicks, Is.EqualTo(10_240_000), "x1.25 three times");
            new RunModifiers(rig.Catalog).TryMachineSpeed(rig.State, rig.Session.Permanent, miner, false, null, out long speed);
            Assert.That(speed, Is.EqualTo(1_953_125), "1,000,000 folded three times with floor at each step");
        }

        [Test]
        public void Purchase_OutputResearch_AppliesOncePerLevel_AndClampsToTheBound()
        {
            var rig = Rig(withMiner: true);
            MachineInstance miner = Miner(rig);
            var modifiers = new RunModifiers(rig.Catalog);
            long Factor() { Assert.That(modifiers.TryOutputFactor(rig.State, miner, null, out long f), Is.True); return f; }

            Assert.That(Factor(), Is.EqualTo(1));
            Buy(rig, MinerOutput);
            Assert.That(Factor(), Is.EqualTo(2));
            Buy(rig, MinerOutput);
            Assert.That(Factor(), Is.EqualTo(4));
            Buy(rig, MinerOutput);
            Assert.That(Factor(), Is.EqualTo(4), "2^3 = 8 clamps to maxCombinedOutputFactor 4");

            long ore = rig.State.Inventory.Units("iron.ore");
            rig.Advance(RunSimHarness.TicksPerSecond * 2);
            Assert.That(rig.State.Inventory.Units("iron.ore") - ore, Is.EqualTo(2 * 4), "one cycle of 2 ore at the clamped factor");
        }

        [Test]
        public void Purchase_UpgradeCostResearch_LowersTheNextBuildCost()
        {
            var rig = Rig(discount: true);
            Buy(rig, RunCommandFixtures.MachineDiscount);

            RunCommandResult<BuildResult> built = rig.Build(0, RunCommandFixtures.Miner);

            Assert.That(built.IsSuccess, Is.True);
            Assert.That(built.Value.CostPaid, Is.EqualTo(8), "the 10 Cash miner at x0.8");
        }

        [Test]
        public void Purchase_EndsInTheSettle_SoAResearchPurchasedUnlockHoldsAtOnce()
        {
            var o = new RunCommandFixtures.Options
            {
                Unlocks = new System.Collections.Generic.Dictionary<string, UnlockCondition>
                {
                    [RunCommandFixtures.Press] = new UnlockCondition("researchPurchased", Speed),
                },
            };
            RunCommandFixtures.Rig rig;
            o.Research = Research();
            rig = RunCommandFixtures.NewRig(o, 100_000, unlockedMachines: new[] { RunCommandFixtures.Miner, RunCommandFixtures.Smelter, RunCommandFixtures.Assembler }, rp: 50);
            rig.Advance(0);
            Assert.That(rig.State.IsUnlocked(RunCommandFixtures.Press), Is.False);
            Assert.That(rig.Build(0, RunCommandFixtures.Press).ReasonCode, Is.EqualTo(RunReasons.MachineLocked));

            Buy(rig, Speed);

            Assert.That(rig.State.IsUnlocked(RunCommandFixtures.Press), Is.True, "the command settle evaluates researchPurchased");
            Assert.That(rig.Build(0, RunCommandFixtures.Press).IsSuccess, Is.True);
        }

        // ------------------------------------------------------------------------------------------------
        // Rejections
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Purchase_WithAMissingPrerequisite_IsResearchPrerequisite()
        {
            var rig = Rig();

            AssertRejected(rig, () => rig.Commands.PurchaseResearch(Deep), RunReasons.ResearchPrerequisite);
            Buy(rig, Speed);
            AssertRejected(rig, () => rig.Commands.PurchaseResearch(Deep), RunReasons.ResearchPrerequisite);
            Buy(rig, Speed);

            Assert.That(Buy(rig, Deep).NewLevel, Is.EqualTo(1), "speed level 2 meets the prerequisite");
        }

        [Test]
        public void Purchase_AtTheMaximumLevel_IsLevelMax_WithoutSpendingRp()
        {
            var rig = Rig();
            Buy(rig, MinerOutput);
            Buy(rig, MinerOutput);
            Buy(rig, MinerOutput);

            AssertRejected(rig, () => rig.Commands.PurchaseResearch(MinerOutput), RunReasons.LevelMax);
        }

        [Test]
        public void Purchase_WithTooLittleRp_IsRpInsufficient_AtTheBoundaryOfTheCost()
        {
            var rig = Rig(rp: 1);

            AssertRejected(rig, () => rig.Commands.PurchaseResearch(Speed), RunReasons.RpInsufficient);
            rig.State.TryAddRp(1);
            Assert.That(rig.Commands.PurchaseResearch(Speed).IsSuccess, Is.True, "exactly the cost is enough");
            Assert.That(rig.State.Rp, Is.Zero);
        }

        [Test]
        public void Purchase_UnknownResearch_IsCommandInvalid()
        {
            var rig = Rig();

            AssertRejected(rig, () => rig.Commands.PurchaseResearch("research.unknown"), RunReasons.CommandInvalid);
            AssertRejected(rig, () => rig.Commands.PurchaseResearch(null), RunReasons.CommandInvalid);
        }

        [Test]
        public void Purchase_LifecycleBlocked_IsRejectedFirst()
        {
            var rig = Rig();
            rig.Session.SetLifecycleBlocked(true);

            AssertRejected(rig, () => rig.Commands.PurchaseResearch(Speed), RunReasons.LifecycleBlocked);
            AssertRejected(rig, () => rig.Commands.PurchaseResearch("research.unknown"), RunReasons.LifecycleBlocked);
        }

        [Test]
        public void Purchase_WhenTheComposedSpeedOverflows_IsValueLimit_AndTheTrialIsUndone()
        {
            var rig = Rig(withMiner: true);
            Buy(rig, Huge);

            AssertRejected(rig, () => rig.Commands.PurchaseResearch(Huge), RunReasons.ValueLimit);
            Assert.That(rig.State.ResearchLevel(Huge), Is.EqualTo(1), "the level and the RP are as before");
        }

        // ------------------------------------------------------------------------------------------------
        // ResearchSnapshot
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void ResearchSnapshot_OrdersByDepth_AndReportsReasonsInTheCommandsOrder()
        {
            var rig = Rig(rp: 2);
            Buy(rig, Speed);

            ResearchSnapshot s = RunSnapshots.Research(rig.Session);

            Assert.That(s.Rp, Is.Zero);
            Assert.That(s.Nodes.Select(n => n.ResearchId), Is.EqualTo(new[] { Huge, MinerOutput, Speed, Deep }), "depth 0 by ordinal ID, then depth 1");
            ResearchNodeSnapshot speed = s.Nodes.Single(n => n.ResearchId == Speed);
            Assert.That((speed.Level, speed.MaxLevel, speed.NextCost), Is.EqualTo((1, 3, 3L)));
            Assert.That(speed.CanPurchase, Is.False);
            Assert.That(speed.PurchaseReason, Is.EqualTo(RunReasons.RpInsufficient));
            Assert.That(speed.EffectKind, Is.EqualTo("machineSpeed"));
            ResearchNodeSnapshot deep = s.Nodes.Single(n => n.ResearchId == Deep);
            Assert.That(deep.Depth, Is.EqualTo(1));
            Assert.That(deep.PurchaseReason, Is.EqualTo(RunReasons.ResearchPrerequisite));
            Assert.That(deep.Prerequisites.Single().Met, Is.False);
            Assert.That(deep.Prerequisites.Single().ResearchId, Is.EqualTo(Speed));
        }

        [Test]
        public void ResearchSnapshot_AtMaxLevelAndAffordable_ReportsTheEnabledAndTheCappedState()
        {
            var rig = Rig(rp: 100);
            Buy(rig, MinerOutput);
            Buy(rig, MinerOutput);
            Buy(rig, MinerOutput);

            ResearchSnapshot s = RunSnapshots.Research(rig.Session);

            ResearchNodeSnapshot output = s.Nodes.Single(n => n.ResearchId == MinerOutput);
            Assert.That(output.CanPurchase, Is.False);
            Assert.That(output.PurchaseReason, Is.EqualTo(RunReasons.LevelMax));
            Assert.That(output.NextCost, Is.Zero);
            Assert.That(s.Nodes.Single(n => n.ResearchId == Speed).CanPurchase, Is.True);
            Assert.That(s.Nodes.Single(n => n.ResearchId == Speed).PurchaseReason, Is.Null);
        }
    }
}
