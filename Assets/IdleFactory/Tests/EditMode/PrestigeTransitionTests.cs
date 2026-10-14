using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Core.Numerics;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The in-memory prestige (T102, contracts/run-application.md, FR-020 - FR-022, FR-024, US4 scenarios 1, 2 and
    /// 4, QA-4): the IP table and the threshold boundary, the preview, the exact reset and preserve lists, unused
    /// Items surviving while active boosts do not, the new run's identity and Industry starting effects, cancelling,
    /// an active untimed contract, and the rejection of a second confirmation while one is pending. The durable
    /// part (T105, R09, FR-024, US4 scenario 3, QA-4) is at the end: the candidate is written, read back and verified
    /// before it is published, every protocol step failing once is followed by a retry and by a restart that end with
    /// either the old run and no award or the new run and exactly one award, and the pending rules hold.
    /// </summary>
    public sealed class PrestigeTransitionTests
    {
        private const string Boost = "item.boost";
        private const string Speed = "research.speed";
        private const string Plate = "contract.plate";
        private const string IndustryCash = "industry.cash";
        private const string IndustryStorage = "industry.storage";
        private const string IndustrySpeed = "industry.speed";

        private static RunCommandFixtures.Options Options() => new RunCommandFixtures.Options
        {
            Items = new[] { new ItemDef(Boost, 1, "Boost", "F003-ITEM-BOOST", 60_000, "factory", new string[0], new EffectDef("machineSpeed", "allMachines", null, 1_500_000)) },
            Offers = new[]
            {
                new ContractOfferDef(Plate, "plate", "iron.plate", new Curve(5, 1_010_000, "ceil"), null,
                    new[] { new ContractRewardDef("cash", new Curve(100, 1_010_000, "floor")) }, new UnlockCondition("always")),
            },
            Research = new[]
            {
                new ResearchDef(Speed, "Speed", 3, new Curve(5, 1_500_000, "ceil"), new ResearchPrerequisite[0],
                    new EffectDef("machineSpeed", "allMachines", null, 1_100_000)),
            },
            Industry = new[]
            {
                new IndustryUpgradeDef(IndustryCash, "Cash", 3, new Curve(2, 1_000_000, "ceil"), new EffectDef("startingCash", "none", null, 100)),
                new IndustryUpgradeDef(IndustryStorage, "Storage", 2, new Curve(2, 1_000_000, "ceil"), new EffectDef("startingStorageLevel", "none", null, 1)),
                new IndustryUpgradeDef(IndustrySpeed, "Speed", 3, new Curve(3, 1_500_000, "ceil"), new EffectDef("machineSpeed", "allMachines", null, 1_100_000)),
            },
        };

        private sealed class Game
        {
            public RunCommandFixtures.Rig Rig;
            public RunCommands Commands;
            public RunSession Session => Rig.Session;
            public RunState State => Session.State;
        }

        private static Game NewGame(long cash, RunCommandFixtures.Options o = null)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(o ?? Options(), cash);
            rig.Advance(0);
            var game = new Game { Rig = rig };
            game.Commands = new RunCommands(rig.Session, () => RunSimHarness.Id(rig.NextId++), () => RunSimHarness.Id(7777));
            return game;
        }

        private static string Fingerprint(Game g) =>
            RunCommandFixtures.Snapshot(g.State) + RunSimHarness.Fingerprint(g.State, g.Session.Permanent) +
            "ip=" + g.Session.Permanent.Ip + " count=" + g.Session.Permanent.PrestigeCount + " run=" + g.State.RunId + "#" + g.State.RunNumber +
            " timeline=" + g.Session.TimelineUtcMs + " pending=" + (g.Session.PendingTransition != null);

        private static PrestigePreview Preview(Game g)
        {
            RunCommandResult<PrestigePreview> r = g.Commands.GetPrestigePreview();
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            return r.Value;
        }

        // ---------------------------------------------------------------------------------------------------
        // IP table, threshold, preview
        // ---------------------------------------------------------------------------------------------------

        [TestCase(10_000_000L, 10L)]
        [TestCase(40_000_000L, 20L)]
        [TestCase(90_000_000L, 30L)]
        [TestCase(1_000_000_000L, 100L)]
        public void Preview_AwardsTheTableIp(long cash, long ip)
        {
            Game g = NewGame(cash);
            PrestigePreview p = Preview(g);
            Assert.That(p.FactoryValue.Total, Is.EqualTo(cash));
            Assert.That(p.IpAward, Is.EqualTo(ip));
            Assert.That(p.IpAfter, Is.EqualTo(ip));
            Assert.That(p.IsEligible, Is.True);
            Assert.That(p.Reason, Is.Null);
            Assert.That(p.MissingFactoryValue, Is.EqualTo(0));
        }

        [Test]
        public void Threshold_OneDollarBelowIsRejectedAndTheThresholdItselfIsAccepted()
        {
            Game below = NewGame(9_999_999);
            PrestigePreview p = Preview(below);
            Assert.That(p.IsEligible, Is.False);
            Assert.That(p.Reason, Is.EqualTo(RunReasons.PrestigeBelowThreshold));
            Assert.That(p.MissingFactoryValue, Is.EqualTo(1));
            Assert.That(p.IpAward, Is.EqualTo(9), "the preview still shows what the value would award");
            string before = Fingerprint(below);
            RunCommandResult<PrestigeConfirmation> rejected = below.Commands.ConfirmPrestige();
            Assert.That(rejected.IsSuccess, Is.False);
            Assert.That(rejected.ReasonCode, Is.EqualTo(RunReasons.PrestigeBelowThreshold));
            Assert.That(Fingerprint(below), Is.EqualTo(before), "a rejection changes nothing");
            Assert.That(below.Session.PendingTransition, Is.Null);

            Game exact = NewGame(10_000_000);
            Assert.That(Preview(exact).IsEligible, Is.True);
            Assert.That(exact.Commands.ConfirmPrestige().IsSuccess, Is.True);
        }

        [Test]
        public void Eligibility_IsRecheckedAtTheCommandInstant()
        {
            Game g = NewGame(10_000_000);
            Assert.That(Preview(g).IsEligible, Is.True);
            // The player spends value between the preview and the confirmation: a machine costs less than it is worth,
            // so a run just at the threshold drops below it after a build and a removal.
            Assert.That(g.Rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            Assert.That(g.Commands.Remove(g.State.Machines[0].InstanceId, true).IsSuccess, Is.True);
            Assert.That(Preview(g).IsEligible, Is.False);
            Assert.That(g.Commands.ConfirmPrestige().ReasonCode, Is.EqualTo(RunReasons.PrestigeBelowThreshold));
        }

        [Test]
        public void Preview_ReportsTheListsTheNextRunAndWhatIsLost()
        {
            Game g = NewGame(20_000_000);
            Assert.That(g.Session.Permanent.TryAddItems(Boost, 3), Is.EqualTo(RunStateResult.Ok));
            Assert.That(g.Commands.ActivateItem(Boost, null).IsSuccess, Is.True);
            Assert.That(g.Commands.AcceptContract(Plate).IsSuccess, Is.True);
            Assert.That(g.Session.Permanent.TrySetIndustryLevel(IndustryCash, 2), Is.EqualTo(RunStateResult.Ok));

            PrestigePreview p = Preview(g);

            Assert.That(p.Reset, Is.EqualTo(new[]
            {
                "cash", "stock", "machines", "priorities", "autoSell", "expansion", "contracts", "research", "researchPoints",
                "activeEffects", "overdrive", "unlocks",
            }));
            Assert.That(p.Preserve, Is.EqualTo(new[] { "influencePoints", "industryUpgrades", "items" }));
            Assert.That(p.PreservedItemCount, Is.EqualTo(2));
            Assert.That(p.ActiveEffectCount, Is.EqualTo(1));
            Assert.That(p.ActiveContractOfferId, Is.EqualTo(Plate));
            Assert.That(p.NextRunNumber, Is.EqualTo(2));
            Assert.That(p.NextRunStartingCash, Is.EqualTo(25 + 200));
            Assert.That(p.NextRunStorageLevel, Is.EqualTo(1));
            Assert.That(p.ThresholdFactoryValue, Is.EqualTo(10_000_000));
        }

        [Test]
        public void Preview_ChangesNothing_AndTheIndustrySnapshotUsesTheSameNextRunValues()
        {
            Game g = NewGame(12_000_000);
            Assert.That(g.Session.Permanent.TrySetIndustryLevel(IndustryCash, 3), Is.EqualTo(RunStateResult.Ok));
            Assert.That(g.Session.Permanent.TrySetIndustryLevel(IndustryStorage, 2), Is.EqualTo(RunStateResult.Ok));
            string before = Fingerprint(g);
            long structure = g.Session.StructureRevision, quantity = g.Session.QuantityRevision;

            PrestigePreview p = Preview(g);
            Preview(g);

            Assert.That(Fingerprint(g), Is.EqualTo(before));
            Assert.That(g.Session.StructureRevision, Is.EqualTo(structure));
            Assert.That(g.Session.QuantityRevision, Is.EqualTo(quantity));
            IndustrySnapshot industry = RunSnapshots.Industry(g.Session);
            Assert.That(industry.NextRunStartingCash, Is.EqualTo(25 + 300));
            Assert.That(industry.NextRunStorageLevel, Is.EqualTo(3));
            NextRunStart start = NextRunStart.Compute(g.Rig.Catalog, g.Session.Permanent);
            Assert.That(start.StartingCash, Is.EqualTo(industry.NextRunStartingCash));
            Assert.That(start.StorageLevel, Is.EqualTo(industry.NextRunStorageLevel));
            Assert.That(p.NextRunStartingCash, Is.EqualTo(start.StartingCash));
            Assert.That(p.NextRunStorageLevel, Is.EqualTo(start.StorageLevel));
        }

        [Test]
        public void NextRunStart_ClampsTheStorageLevelAndSaturatesTheCash()
        {
            var o = Options();
            o.Industry = new[]
            {
                new IndustryUpgradeDef(IndustryCash, "Cash", 3, new Curve(2, 1_000_000, "ceil"), new EffectDef("startingCash", "none", null, ExactBound.Value)),
                new IndustryUpgradeDef(IndustryStorage, "Storage", 9, new Curve(2, 1_000_000, "ceil"), new EffectDef("startingStorageLevel", "none", null, 1)),
            };
            Game g = NewGame(10, o);
            Assert.That(g.Session.Permanent.TrySetIndustryLevel(IndustryCash, 3), Is.EqualTo(RunStateResult.Ok));
            Assert.That(g.Session.Permanent.TrySetIndustryLevel(IndustryStorage, 9), Is.EqualTo(RunStateResult.Ok));
            NextRunStart start = NextRunStart.Compute(g.Rig.Catalog, g.Session.Permanent);
            Assert.That(start.StartingCash, Is.EqualTo(ExactBound.Value));
            Assert.That(start.StorageLevel, Is.EqualTo(5), "the catalog has five storage levels");
            Assert.That(NextRunStart.Compute(g.Rig.Catalog, null).StartingCash, Is.EqualTo(25));
            Assert.That(NextRunStart.Compute(g.Rig.Catalog, null).StorageLevel, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------------------------------------------
        // Confirm: the complete candidate
        // ---------------------------------------------------------------------------------------------------

        private static Game PlayedGame(out PermanentState oldPermanent, out Guid oldRunId)
        {
            Game g = NewGame(50_000_000);
            RunCommands c = g.Commands;
            Assert.That(g.Rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            Assert.That(g.Rig.Build(1, RunCommandFixtures.Smelter).IsSuccess, Is.True);
            Assert.That(c.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            Assert.That(c.UpgradeStorage().IsSuccess, Is.True);
            Assert.That(c.UpgradeFactory().IsSuccess, Is.True);
            Assert.That(g.State.TryAddRp(50), Is.EqualTo(RunStateResult.Ok));
            Assert.That(c.PurchaseResearch(Speed).IsSuccess, Is.True);
            Assert.That(g.Session.Permanent.TryAddItems(Boost, 3), Is.EqualTo(RunStateResult.Ok));
            Assert.That(c.ActivateItem(Boost, null).IsSuccess, Is.True);
            Assert.That(c.ActivateOverdrive().IsSuccess, Is.True);
            Assert.That(c.AcceptContract(Plate).IsSuccess, Is.True);
            Assert.That(g.Session.Permanent.TryAddIp(7), Is.EqualTo(RunStateResult.Ok));
            Assert.That(g.Session.Permanent.TrySetIndustryLevel(IndustryCash, 2), Is.EqualTo(RunStateResult.Ok));
            Assert.That(g.Session.Permanent.TrySetIndustryLevel(IndustryStorage, 1), Is.EqualTo(RunStateResult.Ok));
            Assert.That(g.Session.Permanent.TrySetIndustryLevel(IndustrySpeed, 1), Is.EqualTo(RunStateResult.Ok));
            g.Rig.Advance(30_000_000);
            oldPermanent = g.Session.Permanent;
            oldRunId = g.State.RunId;
            return g;
        }

        [Test]
        public void Confirm_BuildsTheCompleteCandidate_AndLeavesTheLiveRunUntouchedWhilePending()
        {
            Game g = PlayedGame(out PermanentState oldPermanent, out Guid oldRunId);
            RunState oldState = g.State;
            string before = Fingerprint(g);
            PrestigePreview preview = Preview(g);

            RunCommandResult<PrestigeConfirmation> r = g.Commands.ConfirmPrestige();

            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            PrestigeConfirmation c = r.Value;
            Assert.That(c.IpAwarded, Is.EqualTo(preview.IpAward));
            Assert.That(c.FactoryValue.Total, Is.EqualTo(preview.FactoryValue.Total));
            Assert.That(c.IpTotal, Is.EqualTo(7 + preview.IpAward));
            Assert.That(c.OldRunId, Is.EqualTo(oldRunId));
            Assert.That(c.NewRunId, Is.EqualTo(RunSimHarness.Id(7777)));
            Assert.That(c.NewRunNumber, Is.EqualTo(2));
            Assert.That(c.AbandonedContractOfferId, Is.EqualTo(Plate));
            Assert.That(c.DroppedEffectCount, Is.EqualTo(1));
            Assert.That(g.Session.PendingTransition, Is.SameAs(c.Candidate));
            Assert.That(g.Session.State, Is.SameAs(oldState));
            Assert.That(g.Session.Permanent, Is.SameAs(oldPermanent));
            Assert.That(Fingerprint(g).Replace("pending=True", "pending=False"), Is.EqualTo(before), "run, permanent state and timeline are untouched");
            Assert.That(oldPermanent.Ip, Is.EqualTo(7), "the award is in the candidate only");
            Assert.That(oldPermanent.PrestigeCount, Is.EqualTo(0));
        }

        [Test]
        public void Promote_PublishesTheFreshRun_KeepsTheItemsIndustryAndIp_AndResetsEverythingElse()
        {
            Game g = PlayedGame(out PermanentState oldPermanent, out Guid oldRunId);
            long timeline = g.Session.TimelineUtcMs;
            long ipBefore = oldPermanent.Ip;
            long award = Preview(g).IpAward;
            long structure = g.Session.StructureRevision, quantity = g.Session.QuantityRevision;
            Assert.That(g.Commands.ConfirmPrestige().IsSuccess, Is.True);

            RunCommandResult<Guid> promoted = g.Commands.PromotePrestige();

            Assert.That(promoted.IsSuccess, Is.True, promoted.ReasonCode);
            Assert.That(promoted.Value, Is.EqualTo(RunSimHarness.Id(7777)));
            RunState s = g.State;
            Assert.That(g.Session.PendingTransition, Is.Null);
            Assert.That(g.Session.IsLifecycleBlocked, Is.False);
            Assert.That(g.Session.StructureRevision, Is.GreaterThan(structure));
            Assert.That(g.Session.QuantityRevision, Is.GreaterThan(quantity));
            Assert.That(g.Session.TimelineUtcMs, Is.EqualTo(timeline), "the timeline never moves back");

            // Identity.
            Assert.That(s.RunId, Is.EqualTo(RunSimHarness.Id(7777)));
            Assert.That(s.RunId, Is.Not.EqualTo(oldRunId));
            Assert.That(s.PreviousRunId, Is.EqualTo(oldRunId));
            Assert.That(s.RunNumber, Is.EqualTo(2));

            // The Industry starting effects: 25 + 2 x 100 Cash, storage level 1 + 1.
            Assert.That(s.Cash, Is.EqualTo(225));
            Assert.That(s.StorageLevel, Is.EqualTo(2));
            Assert.That(s.Inventory.Capacity, Is.EqualTo(g.Rig.Catalog.StorageLevels[1].Capacity));
            Assert.That(s.StorageInvestedCash, Is.EqualTo(0));
            Assert.That(s.FactoryLevel, Is.EqualTo(1));
            Assert.That(s.FactoryInvestedCash, Is.EqualTo(0));

            // Reset: machines, priorities, stock, auto-sell, RP, Research, effects, Overdrive, contracts.
            Assert.That(s.Machines.Count, Is.EqualTo(0));
            Assert.That(s.Priority.Count, Is.EqualTo(0));
            Assert.That(s.Inventory.Total, Is.EqualTo(0));
            Assert.That(s.AutoSell, Is.Empty);
            Assert.That(s.Rp, Is.EqualTo(0));
            Assert.That(s.ResearchLevels(), Is.Empty);
            Assert.That(s.Effects.Count, Is.EqualTo(0));
            Assert.That(s.Overdrive.ActiveUntilUtcMs, Is.Null);
            Assert.That(s.Overdrive.CooldownUntilUtcMs, Is.Null);
            Assert.That(s.Contracts.Active, Is.Null);
            Assert.That(s.Contracts.LastExpired, Is.Null);
            Assert.That(s.Contracts.CompletionCounts(), Is.Empty);
            Assert.That(s.Contracts.RunCompletionTotal, Is.Zero);

            // Unlocks: the initial ones plus what one settle unlocks (T140), Overdrive included.
            foreach (string id in new[] { "iron.ore", "iron.bar", "iron.plate", "iron.gear", RunCommandFixtures.Miner, RunCommandFixtures.Smelter,
                RunCommandFixtures.Press, RunCommandFixtures.Assembler, Plate, RunState.OverdriveUnlockId })
                Assert.That(s.IsUnlocked(id), Is.True, id);
            Assert.That(s.Validate(), Is.EqualTo(RunStateResult.Ok));

            // Preserved: IP plus the award, the Industry levels and the unused Item stacks (3 owned, 1 spent).
            PermanentState p = g.Session.Permanent;
            Assert.That(p.Ip, Is.EqualTo(ipBefore + award));
            Assert.That(p.PrestigeCount, Is.EqualTo(1));
            Assert.That(p.ItemCount(Boost), Is.EqualTo(2));
            Assert.That(p.IndustryLevel(IndustryCash), Is.EqualTo(2));
            Assert.That(p.IndustryLevel(IndustryStorage), Is.EqualTo(1));
            Assert.That(p.IndustryLevel(IndustrySpeed), Is.EqualTo(1));
            Assert.That(s.RunNumber, Is.EqualTo(p.PrestigeCount + 1));

            // The new run works: the Industry speed still applies and the read models see the new state.
            Assert.That(g.Commands.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            Assert.That(RunSnapshots.Items(g.Session).TotalCount, Is.EqualTo(2));
            Assert.That(RunSnapshots.Industry(g.Session).Ip, Is.EqualTo(ipBefore + award));
        }

        [Test]
        public void ActiveBoostsAreLost_ButTheUnusedItemsSurvive_AndTheBoostCanBeUsedAgainAtOnce()
        {
            Game g = NewGame(20_000_000);
            Assert.That(g.Session.Permanent.TryAddItems(Boost, 2), Is.EqualTo(RunStateResult.Ok));
            Assert.That(g.Commands.ActivateItem(Boost, null).IsSuccess, Is.True);
            Assert.That(g.State.Effects.Count, Is.EqualTo(1));
            Assert.That(g.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(g.Commands.PromotePrestige().IsSuccess, Is.True);

            Assert.That(g.State.Effects.Count, Is.EqualTo(0));
            Assert.That(g.Session.Permanent.ItemCount(Boost), Is.EqualTo(1));
            Assert.That(g.Commands.ActivateItem(Boost, null).IsSuccess, Is.True, "no stale effect conflicts in the new run");
        }

        [Test]
        public void AnActiveUntimedContractIsDroppedWithoutRewardOrPenalty_AndItsStockStillCountsForTheValue()
        {
            Game g = NewGame(9_999_990);
            RunSimHarness.Stock(g.State, "iron.plate", 10);
            long value = Preview(g).FactoryValue.Total;
            Assert.That(value, Is.EqualTo(9_999_990 + 10 * 8));
            Assert.That(g.Commands.AcceptContract(Plate).IsSuccess, Is.True);
            Assert.That(g.State.Contracts.Active.DeadlineUtcMs, Is.Null, "the contract is untimed");
            Assert.That(Preview(g).FactoryValue.Total, Is.EqualTo(value), "accepting does not change the value");

            RunCommandResult<PrestigeConfirmation> r = g.Commands.ConfirmPrestige();
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            Assert.That(r.Value.AbandonedContractOfferId, Is.EqualTo(Plate));
            Assert.That(g.State.Contracts.Active, Is.Not.Null, "the live run still has it while pending");
            Assert.That(g.Commands.PromotePrestige().IsSuccess, Is.True);

            Assert.That(g.State.Contracts.Active, Is.Null);
            Assert.That(g.State.Contracts.CompletionCount(Plate), Is.EqualTo(0), "no completion is counted");
            Assert.That(g.State.Cash, Is.EqualTo(25), "no reward");
            Assert.That(g.State.Contracts.LastExpired, Is.Null, "and no expiry outcome");
            Assert.That(g.Commands.AcceptContract(Plate).IsSuccess, Is.True, "the offer is available again at its first quantity");
            Assert.That(g.State.Contracts.Active.RequiredUnits, Is.EqualTo(5));
        }

        [Test]
        public void AnExpiredContractOutcome_DoesNotSurviveThePrestige()
        {
            var o = Options();
            o.Offers = new[]
            {
                new ContractOfferDef("contract.timed-bar", "timedBar", "iron.bar", new Curve(10, 1_010_000, "ceil"), 60_000,
                    new[] { new ContractRewardDef("cash", new Curve(200, 1_000_000, "floor")) }, new UnlockCondition("always")),
            };
            Game g = NewGame(20_000_000, o);
            Assert.That(g.Commands.AcceptContract("contract.timed-bar").IsSuccess, Is.True);
            g.Rig.Advance(61_000L * RunSession.TicksPerMs);
            Assert.That(g.State.Contracts.LastExpired.HasValue, Is.True);

            Assert.That(g.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(g.Commands.PromotePrestige().IsSuccess, Is.True);
            Assert.That(g.State.Contracts.LastExpired, Is.Null);
        }

        // ---------------------------------------------------------------------------------------------------
        // Pending, second confirm, cancel
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void ASecondConfirmWhilePending_IsRejectedAsTransitionPending_AndAwardsNothingTwice()
        {
            Game g = NewGame(40_000_000);
            RunCommandResult<PrestigeConfirmation> first = g.Commands.ConfirmPrestige();
            Assert.That(first.IsSuccess, Is.True);
            PrestigeCandidate candidate = g.Session.PendingTransition;
            string before = Fingerprint(g);

            RunCommandResult<PrestigeConfirmation> second = g.Commands.ConfirmPrestige();

            Assert.That(second.IsSuccess, Is.False);
            Assert.That(second.ReasonCode, Is.EqualTo(RunReasons.TransitionPending));
            Assert.That(g.Session.PendingTransition, Is.SameAs(candidate), "the pending candidate is the first one");
            Assert.That(Fingerprint(g), Is.EqualTo(before));
            Assert.That(Preview(g).IsEligible, Is.False);
            Assert.That(Preview(g).Reason, Is.EqualTo(RunReasons.TransitionPending));

            Assert.That(g.Commands.PromotePrestige().IsSuccess, Is.True);
            Assert.That(g.Session.Permanent.Ip, Is.EqualTo(20), "one award only");
            Assert.That(g.Commands.PromotePrestige().ReasonCode, Is.EqualTo(RunReasons.CommandInvalid), "nothing is pending any more");
        }

        [Test]
        public void WhilePending_CommandsAndAdvancingAreHeldBack_AndPromotionReleasesThem()
        {
            Game g = NewGame(20_000_000);
            Assert.That(g.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(g.Session.IsLifecycleBlocked, Is.True);
            Assert.That(g.Commands.Build(0, RunCommandFixtures.Miner).ReasonCode, Is.EqualTo(RunReasons.LifecycleBlocked));
            Assert.That(g.Session.Advance(1).ReasonCode, Is.EqualTo(RunReasons.LifecycleBlocked));
            Assert.That(g.Commands.GetPrestigePreview().IsSuccess, Is.True, "reading stays possible");
            Assert.That(g.Commands.PromotePrestige().IsSuccess, Is.True);
            Assert.That(g.Session.IsLifecycleBlocked, Is.False);
            Assert.That(g.Session.Advance(1).IsSuccess, Is.True);
        }

        [Test]
        public void ConfirmationAfterAPromotion_IsAFreshDecision()
        {
            Game g = NewGame(20_000_000);
            Assert.That(g.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(g.Commands.PromotePrestige().IsSuccess, Is.True);
            Assert.That(g.Commands.ConfirmPrestige().ReasonCode, Is.EqualTo(RunReasons.PrestigeBelowThreshold), "the new run starts far below the threshold");
        }

        [Test]
        public void Cancel_ChangesNothing_AtEveryStage()
        {
            Game g = NewGame(30_000_000);
            Assert.That(g.Session.Permanent.TryAddItems(Boost, 2), Is.EqualTo(RunStateResult.Ok));
            Assert.That(g.Rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            Assert.That(g.Commands.AcceptContract(Plate).IsSuccess, Is.True);
            string before = Fingerprint(g);

            // Declining after the preview: the preview is read-only.
            Preview(g);
            Assert.That(Fingerprint(g), Is.EqualTo(before));
            Assert.That(g.Commands.CancelPendingTransition().Value, Is.False, "nothing was pending");
            Assert.That(Fingerprint(g), Is.EqualTo(before));

            // Cancelling a pending transition drops the candidate; the old run is exactly as it was.
            Assert.That(g.Commands.ConfirmPrestige().IsSuccess, Is.True);
            RunCommandResult<bool> cancelled = g.Commands.CancelPendingTransition();
            Assert.That(cancelled.IsSuccess && cancelled.Value, Is.True);
            Assert.That(Fingerprint(g), Is.EqualTo(before));
            Assert.That(g.Session.IsLifecycleBlocked, Is.False);
            Assert.That(g.Commands.SetActive(g.State.Machines[0].InstanceId, false).IsSuccess, Is.True, "commands work again");

            // And the prestige can be confirmed anew, awarding once.
            Assert.That(g.Commands.SetActive(g.State.Machines[0].InstanceId, true).IsSuccess, Is.True);
            Assert.That(g.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(g.Commands.PromotePrestige().IsSuccess, Is.True);
            Assert.That(g.Session.Permanent.PrestigeCount, Is.EqualTo(1));
            Assert.That(g.Session.Permanent.Ip, Is.GreaterThan(0));
        }

        [Test]
        public void Confirm_WithoutAPermanentState_IsRejected()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(Options(), 20_000_000);
            var session = new RunSession(rig.Catalog, rig.State, null, RunCommandFixtures.StartUtcMs);
            var commands = new RunCommands(session);
            Assert.That(commands.ConfirmPrestige().ReasonCode, Is.EqualTo(RunReasons.CommandInvalid));
            Assert.That(commands.GetPrestigePreview().ReasonCode, Is.EqualTo(RunReasons.CommandInvalid));
        }

        // ---------------------------------------------------------------------------------------------------
        // Durable transition (T105)
        // ---------------------------------------------------------------------------------------------------

        private bool _logToUnity;

        [SetUp]
        public void DisableDiagnostics()
        {
            _logToUnity = CheckpointDiagnostics.LogToUnity;
            CheckpointDiagnostics.LogToUnity = false;
        }

        [TearDown]
        public void RestoreDiagnostics()
        {
            CheckpointDiagnostics.LogToUnity = _logToUnity;
        }

        private static DurabilityRig ReadyToPrestige(string root = null, bool save = true)
        {
            DurabilityRig h = DurabilityRig.Create(root: root);
            h.Play();
            h.Sync();
            if (save) DurabilityRig.Ok(h.Persistence.SaveNow());
            return h;
        }

        private static PrestigeConfirmation Confirm(DurabilityRig h)
        {
            RunCommandResult<PrestigeConfirmation> confirmed = h.Commands.ConfirmPrestige();
            Assert.That(confirmed.IsSuccess, Is.True, confirmed.ReasonCode);
            return confirmed.Value;
        }

        /// <summary>The pending candidate becomes durable without the session learning of it (a crash between the two steps).</summary>
        private static RunCheckpoint PromoteBehindTheSessionsBack(DurabilityRig h, int checkpointSuffix)
        {
            PrestigeCandidate candidate = h.Session.PendingTransition;
            RunCheckpoint active = h.Active();
            var checkpoint = new RunCheckpoint(new CheckpointId(new Guid("55555555-5555-5555-5555-" + checkpointSuffix.ToString("D12"))),
                active.Generation + 1, Math.Max(candidate.ConfirmedUtcMs, active.DepartureUtcMs), h.Rig.Catalog.OfflinePolicy.Revision, null,
                h.Rig.Catalog.ContentRevision, null, candidate.Run, candidate.Permanent);
            Assert.That(h.Store.Promote(checkpoint).Succeeded, Is.True);
            return checkpoint;
        }

        private static void AssertOldRunNoAward(DurabilityRig h, Guid oldRun, long ip, string context)
        {
            Assert.That(h.Session.State.RunId, Is.EqualTo(oldRun), context);
            Assert.That(h.Session.State.RunNumber, Is.EqualTo(1), context);
            Assert.That(h.Session.Permanent.Ip, Is.EqualTo(ip), context + ": no award");
            Assert.That(h.Session.Permanent.PrestigeCount, Is.EqualTo(0), context);
        }

        private static void AssertNewRunOneAward(DurabilityRig h, Guid oldRun, long ip, long award, string context)
        {
            Assert.That(h.Session.State.RunNumber, Is.EqualTo(2), context);
            Assert.That(h.Session.State.PreviousRunId, Is.EqualTo(oldRun), context);
            Assert.That(h.Session.Permanent.Ip, Is.EqualTo(ip + award), context + ": exactly one award");
            Assert.That(h.Session.Permanent.PrestigeCount, Is.EqualTo(1), context);
            Assert.That(h.Session.PendingTransition, Is.Null, context);
        }

        // The sweeps run over a store that already holds a generation (an autosave precedes any prestige in play): a torn
        // FIRST generation is a corrupt store by the T138 rules (data without a valid manifest, SEC-1) for any write.
        [Test]
        public void EveryProtocolStepFailingOnce_ThenRetry_EndsWithTheNewRunAndExactlyOneAward()
        {
            const bool saveFirst = true;
            DurabilityRig clean = ReadyToPrestige(save: saveFirst);
            Confirm(clean);
            clean.Backend.ResetCounters();
            Assert.That(clean.Commands.PromotePrestige().IsSuccess, Is.True);
            int calls = clean.Backend.Calls;
            Assert.That(calls, Is.GreaterThan(10));
            var failures = new List<string>();

            for (int i = 0; i < calls; i++)
            {
                DurabilityRig h = ReadyToPrestige(save: saveFirst);
                Guid oldRun = h.Session.State.RunId;
                long ip = h.Session.Permanent.Ip;
                long award = Confirm(h).IpAwarded;
                h.Backend.ResetCounters();
                h.Backend.FailAtCall(i);
                RunCommandResult<Guid> promoted = h.Commands.PromotePrestige();
                h.Backend.ClearFault();

                if (!promoted.IsSuccess)
                {
                    // Nothing was published: the old run, the pending candidate and no award in memory.
                    if (h.Session.State.RunId != oldRun) failures.Add(i + ": the run changed although the promotion failed");
                    if (h.Session.PendingTransition == null) failures.Add(i + ": the candidate is gone");
                    if (h.Session.Permanent.Ip != ip) failures.Add(i + ": award in memory");
                    if (h.Persistence.Lifecycle != LifecycleState.Running) failures.Add(i + ": lifecycle " + h.Persistence.Lifecycle);
                    RunCommandResult<RunPersistenceOutcome> retried = h.Persistence.RetryPendingTransition();
                    if (!retried.IsSuccess) failures.Add(i + ": retry " + retried.ReasonCode);
                }

                if (h.Session.State.RunNumber != 2 || h.Session.State.PreviousRunId != oldRun) failures.Add(i + ": not the new run");
                if (h.Session.Permanent.Ip != ip + award) failures.Add(i + ": IP " + h.Session.Permanent.Ip + " instead of " + (ip + award));
                if (h.Session.Permanent.PrestigeCount != 1) failures.Add(i + ": prestige count");
                if (h.Session.PendingTransition != null) failures.Add(i + ": still pending");
                if (h.Session.IsLifecycleBlocked) failures.Add(i + ": blocked");
                RunCheckpointReadResult activeRead = h.Store.ReadActive();
                if (!activeRead.Succeeded)
                {
                    failures.Add(i + ": no durable active generation (" + activeRead.Outcome + "), first attempt " + promoted.ReasonCode + ", log " +
                                 string.Join(",", h.Backend.Log.Take(40)));
                    continue;
                }

                RunCheckpoint active = activeRead.Checkpoint;
                if (active.Run.RunId != h.Session.State.RunId || active.Permanent.Ip != ip + award) failures.Add(i + ": durable state differs");
                if (saveFirst && h.Previous().Run.RunId != oldRun) failures.Add(i + ": previous is not the old run");
                if (saveFirst && h.Previous().Permanent.Ip != ip) failures.Add(i + ": the old run's IP changed");
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        // T331 (QA warning 1): the threshold is derived from the persisted PrestigeCount, so a restart at count 1 reads the second rung.
        [Test]
        public void ARestartAtPrestigeCountOneReadsTheRisingThresholdOfIndexOne()
        {
            RunCommandFixtures.Options options = SaveV2Scenarios.Options();
            options.Prestige = new PrestigeDef(10_000_000, "sqrt", 10, 10_000_000, new ThresholdDef(10_000_000, 1_500_000, 3));
            DurabilityRig h = DurabilityRig.Create(options: options);
            h.Play();
            h.Sync();
            DurabilityRig.Ok(h.Persistence.SaveNow());
            Confirm(h);
            Assert.That(h.Commands.PromotePrestige().IsSuccess, Is.True);
            Assert.That(h.Session.Permanent.PrestigeCount, Is.EqualTo(1));

            DurabilityRig restarted = DurabilityRig.Restart(h, h.Clock.UnixMs, 900, options: options);
            DurabilityRig.Ok(restarted.Persistence.ResumeAndReconcile());
            Assert.That(restarted.Session.Permanent.PrestigeCount, Is.EqualTo(1), "the count is durable and counted once");

            Assert.That(PrestigeThreshold.TryForCount(restarted.Rig.Catalog.Prestige, 1, out long expected), Is.True);
            Assert.That(expected, Is.EqualTo(15_000_000));
            PrestigePreview preview = restarted.Commands.GetPrestigePreview().Value;
            Assert.That(preview.Threshold, Is.EqualTo(expected));
            Assert.That(preview.PrestigeIndex, Is.EqualTo(1));
            Assert.That(PrestigeThreshold.TryNextForCount(restarted.Rig.Catalog.Prestige, 1, out long next), Is.True);
            Assert.That(preview.NextThreshold, Is.EqualTo(next));
            Assert.That(restarted.Commands.ConfirmPrestige().ReasonCode, Is.EqualTo(RunReasons.PrestigeBelowThreshold), "the new run is far below 15,000,000");
        }

        [Test]
        public void EveryProtocolStepFailingOnce_ThenRestart_LoadsTheOldRunWithoutAwardOrTheNewRunWithOne()
        {
            const bool saveFirst = true;
            DurabilityRig clean = ReadyToPrestige(save: saveFirst);
            Confirm(clean);
            clean.Backend.ResetCounters();
            Assert.That(clean.Commands.PromotePrestige().IsSuccess, Is.True);
            int calls = clean.Backend.Calls;
            var failures = new List<string>();
            int oldRuns = 0;
            int newRuns = 0;

            for (int i = 0; i < calls; i++)
            {
                DurabilityRig h = ReadyToPrestige(save: saveFirst);
                Guid oldRun = h.Session.State.RunId;
                long ip = h.Session.Permanent.Ip;
                long award = Confirm(h).IpAwarded;
                h.Backend.ResetCounters();
                h.Backend.FailAtCall(i);
                h.Commands.PromotePrestige();
                h.Backend.ClearFault();

                // The process dies; a new one loads whatever is durable.
                DurabilityRig restarted = DurabilityRig.Restart(h, h.Clock.UnixMs, 700 + i);
                var resumed = restarted.Persistence.ResumeAndReconcile();
                if (!resumed.IsSuccess)
                {
                    failures.Add(i + ": restart " + resumed.ReasonCode);
                    continue;
                }

                bool old = restarted.Session.State.RunId == oldRun;
                if (old)
                {
                    oldRuns++;
                    if (restarted.Session.State.RunNumber != 1 || restarted.Session.Permanent.Ip != ip) failures.Add(i + ": old run with an award");
                }
                else
                {
                    newRuns++;
                    if (restarted.Session.State.RunNumber != 2 || restarted.Session.State.PreviousRunId != oldRun ||
                        restarted.Session.Permanent.Ip != ip + award || restarted.Session.Permanent.PrestigeCount != 1)
                        failures.Add(i + ": new run without exactly one award");
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
            Assert.That(oldRuns, Is.GreaterThan(0), "a failure before the promotion became visible keeps the old run");
            Assert.That(newRuns, Is.GreaterThan(0), "a failure after the promotion became visible keeps the new run");
        }

        [Test]
        public void ThePrestigeBeforeAnyCheckpoint_WritesTheFirstGeneration_AndAFailedFirstAttemptIsRetried()
        {
            DurabilityRig h = ReadyToPrestige(save: false);
            Guid oldRun = h.Session.State.RunId;
            long award = Confirm(h).IpAwarded;
            h.Backend.FailOnFirst("Create", CheckpointStorageFile.Pending);
            Assert.That(h.Commands.PromotePrestige().IsSuccess, Is.False);
            AssertOldRunNoAward(h, oldRun, 0, "after the failed write");
            Assert.That(h.Session.PendingTransition, Is.Not.Null);
            Assert.That(h.Backend.Files, Is.Empty, "nothing was written");

            DurabilityRig.Ok(h.Persistence.RetryPendingTransition());

            AssertNewRunOneAward(h, oldRun, 0, award, "retried");
            Assert.That(h.Active().Generation, Is.EqualTo(1));
            Assert.That(h.Active().Run.PreviousRunId, Is.EqualTo(oldRun));
        }

        [Test]
        public void ARetryAfterAVisiblePromotion_PublishesWithoutASecondAward_AndNothingIsWrittenAgain()
        {
            string root = RunCheckpointTestSupport.NewTempRoot();
            try
            {
                DurabilityRig h = ReadyToPrestige(root);
                Guid oldRun = h.Session.State.RunId;
                long ip = h.Session.Permanent.Ip;
                long award = Confirm(h).IpAwarded;
                RunCheckpoint durable = PromoteBehindTheSessionsBack(h, 1);
                AssertOldRunNoAward(h, oldRun, ip, "the session has not learned of the promotion");
                Dictionary<string, byte[]> files = ReadFiles(root);

                // previousRunId of the durable active generation equals the in-memory runId: the transition already happened.
                Assert.That(h.Active().Run.PreviousRunId, Is.EqualTo(oldRun));
                RunPersistenceOutcome published = DurabilityRig.Ok(h.Persistence.RetryPendingTransition());

                AssertNewRunOneAward(h, oldRun, ip, award, "published");
                Assert.That(published.CheckpointId, Is.EqualTo(durable.Id));
                Assert.That(published.Generation, Is.EqualTo(durable.Generation));
                Assert.That(published.StateReplaced, Is.True);
                Assert.That(ReadFiles(root), Is.EqualTo(files), "no second write and no second award");
                Assert.That(h.Session.State, Is.Not.SameAs(durable.Run), "a fresh read of the durable generation is installed, not the candidate");

                // A restart through the same real files loads the published run: still one award.
                DurabilityRig restarted = DurabilityRig.Restart(h, h.Clock.UnixMs, 50, root);
                DurabilityRig.Ok(restarted.Persistence.ResumeAndReconcile());
                AssertNewRunOneAward(restarted, oldRun, ip, award, "restarted");
                Assert.That(restarted.Active().Permanent.Ip, Is.EqualTo(ip + award));
            }
            finally
            {
                RunCheckpointTestSupport.DeleteQuietly(root);
            }
        }

        [Test]
        public void APromoteAfterAVisiblePromotion_ThroughTheRealStore_PublishesWithoutASecondAward()
        {
            // The in-memory run is the old one and the durable active generation is the promotion: the session that still
            // holds the candidate publishes it without a second award.
            string root = RunCheckpointTestSupport.NewTempRoot();
            try
            {
                DurabilityRig h = ReadyToPrestige(root);
                Guid oldRun = h.Session.State.RunId;
                long ip = h.Session.Permanent.Ip;
                long award = Confirm(h).IpAwarded;
                PromoteBehindTheSessionsBack(h, 2);

                RunCommandResult<Guid> promoted = h.Commands.PromotePrestige();

                Assert.That(promoted.IsSuccess, Is.True, promoted.ReasonCode);
                Assert.That(promoted.Value, Is.EqualTo(h.Session.State.RunId));
                AssertNewRunOneAward(h, oldRun, ip, award, "promote");
                Assert.That(h.Commands.PromotePrestige().ReasonCode, Is.EqualTo(RunReasons.CommandInvalid), "nothing is pending any more");
            }
            finally
            {
                RunCheckpointTestSupport.DeleteQuietly(root);
            }
        }

        [Test]
        public void ARestartAfterAVisiblePromotion_ThroughTheRealStore_LoadsTheNewRunWithExactlyOneAward()
        {
            string root = RunCheckpointTestSupport.NewTempRoot();
            try
            {
                DurabilityRig h = ReadyToPrestige(root);
                Guid oldRun = h.Session.State.RunId;
                long ip = h.Session.Permanent.Ip;
                long award = Confirm(h).IpAwarded;
                RunCheckpoint durable = PromoteBehindTheSessionsBack(h, 6);
                Dictionary<string, byte[]> files = ReadFiles(root);

                // A new process (new catalog, session and persistence) over the same real files: the old session is gone.
                DurabilityRig restarted = DurabilityRig.Restart(h, h.Clock.UnixMs, 80, root);
                RunPersistenceOutcome resumed = DurabilityRig.Ok(restarted.Persistence.ResumeAndReconcile());

                AssertNewRunOneAward(restarted, oldRun, ip, award, "restarted");
                Assert.That(resumed.OfflineSummary.CreditedTicks, Is.EqualTo(0), "same instant: nothing is credited");
                Assert.That(restarted.Active().ReconciledFromCheckpointId, Is.EqualTo(durable.Id));
                Assert.That(restarted.Active().Permanent.Ip, Is.EqualTo(ip + award));
                Assert.That(ReadFiles(root).Count, Is.EqualTo(files.Count));
            }
            finally
            {
                RunCheckpointTestSupport.DeleteQuietly(root);
            }
        }

        [Test]
        public void CancelAfterAPromotionWasIssued_FailsClosedWhenTheStoreCannotBeRead_AndTheRetryPublishesItOnce()
        {
            // The store makes the promotion durable and then fails (any step after the manifest), so the commit is rejected
            // while the new run is the durable active generation. A dead store then must not let the player cancel it.
            DurabilityRig clean = ReadyToPrestige();
            Confirm(clean);
            clean.Backend.ResetCounters();
            Assert.That(clean.Commands.PromotePrestige().IsSuccess, Is.True);
            int calls = clean.Backend.Calls;
            var failures = new List<string>();
            int durableAtFailure = 0;
            int failClosed = 0;

            for (int i = 0; i < calls; i++)
            {
                DurabilityRig h = ReadyToPrestige();
                Guid oldRun = h.Session.State.RunId;
                long ip = h.Session.Permanent.Ip;
                PrestigeConfirmation confirmed = Confirm(h);
                h.Backend.ResetCounters();
                h.Backend.FailAtCall(i);
                bool promoted = h.Commands.PromotePrestige().IsSuccess;
                if (promoted) continue;

                h.Backend.FailAll = true;
                RunCommandResult<bool> cancelled = h.Commands.CancelPendingTransition();
                h.Backend.ClearFault();
                if (cancelled.IsSuccess)
                {
                    // Only a failure before any promotion was issued (the history read) may cancel; nothing is durable then.
                    if (h.Active().Run.RunId == confirmed.NewRunId) failures.Add(i + ": cancelled although the promotion is durable");
                    continue;
                }

                failClosed++;
                if (cancelled.ReasonCode != RunPersistenceReasons.PersistenceFailed) failures.Add(i + ": cancel " + cancelled.ReasonCode);
                if (h.Session.PendingTransition == null) failures.Add(i + ": candidate dropped");
                if (h.Active().Run.RunId == confirmed.NewRunId) durableAtFailure++;

                RunCommandResult<RunPersistenceOutcome> retried = h.Persistence.RetryPendingTransition();
                if (!retried.IsSuccess) failures.Add(i + ": retry " + retried.ReasonCode);
                else if (h.Session.State.RunNumber != 2 || h.Session.Permanent.Ip != ip + confirmed.IpAwarded || h.Session.State.PreviousRunId != oldRun)
                    failures.Add(i + ": not exactly one award");
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
            Assert.That(failClosed, Is.GreaterThan(0));
            Assert.That(durableAtFailure, Is.GreaterThan(0), "some failures happen after the promotion became durable");
        }

        [Test]
        public void SaveNow_NeverWritesTheOldRunOverADurableSuccessor()
        {
            DurabilityRig h = ReadyToPrestige();
            Guid oldRun = h.Session.State.RunId;
            Confirm(h);
            RunCheckpoint successor = PromoteBehindTheSessionsBack(h, 7);
            // The player cancels while the store cannot be read (no promotion of ours was issued), so the old run is live
            // although a durable successor exists.
            h.Backend.FailAll = true;
            Assert.That(h.Commands.CancelPendingTransition().IsSuccess, Is.True);
            h.Backend.ClearFault();
            Assert.That(h.Session.State.RunId, Is.EqualTo(oldRun));

            DurabilityRig.Rejected(h.Persistence.SaveNow(), RunPersistenceReasons.TransitionCommitted);
            DurabilityRig.Rejected(h.Persistence.RecordSuspension(), RunPersistenceReasons.TransitionCommitted);

            Assert.That(h.Active().Id, Is.EqualTo(successor.Id), "the durable successor was not overwritten");
            Assert.That(h.Active().Permanent.PrestigeCount, Is.EqualTo(1));
        }

        [Test]
        public void ACancelAfterAReturnWhileThePromotionFailed_MeasuresTheOldRunsTimersAgainstRealTime()
        {
            DurabilityRig h = ReadyToPrestige();
            Assert.That(h.Commands.AcceptContract(SaveV2Scenarios.TimedBar).IsSuccess, Is.True);
            long deadline = h.Session.State.Contracts.Active.DeadlineUtcMs.Value;
            h.Sync();
            DurabilityRig.Ok(h.Persistence.SaveNow());
            Confirm(h);
            h.Backend.FailAll = true;
            Assert.That(h.Commands.PromotePrestige().IsSuccess, Is.False);
            DurabilityRig.Rejected(h.Persistence.RecordSuspension(), RunReasons.TransitionPending);
            h.Backend.ClearFault();

            long r = deadline + 3 * 3_600_000L;
            h.Clock.UnixMs = r;
            RunPersistenceOutcome returned = DurabilityRig.Ok(h.Persistence.ResumeAndReconcile());
            Assert.That(returned.TransitionPending, Is.True);
            Assert.That(h.Session.AnchorUtcMs, Is.EqualTo(r), "the anchor follows the captured return instant");
            Assert.That(h.Session.State.Contracts.Active, Is.Not.Null, "the old run was not touched by the return");

            Assert.That(h.Commands.CancelPendingTransition().Value, Is.True);
            Assert.That(h.Session.IsLifecycleBlocked, Is.False);
            Assert.That(h.Session.Advance(1).IsSuccess, Is.True);
            Assert.That(h.Session.State.Contracts.Active, Is.Null, "the deadline passed in real time: expired at the first advance");
            Assert.That(h.Session.State.Contracts.LastExpired, Is.Not.Null);
        }

        [Test]
        public void RecoverPreviousCheckpoint_WhilePending_IsRejected_AndTheCandidateIsKept()
        {
            DurabilityRig h = ReadyToPrestige();
            Confirm(h);
            PrestigeCandidate candidate = h.Session.PendingTransition;

            DurabilityRig.Rejected(h.Persistence.RecoverPreviousCheckpoint(), RunReasons.TransitionPending);

            Assert.That(h.Session.PendingTransition, Is.SameAs(candidate));
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
        }

        [Test]
        public void ASecondPrestige_RunTwoToThree_IsNotMistakenForAnAlreadyDurableTransition()
        {
            DurabilityRig h = ReadyToPrestige();
            Guid run1 = h.Session.State.RunId;
            long ip0 = h.Session.Permanent.Ip;
            long award1 = Confirm(h).IpAwarded;
            Assert.That(h.Commands.PromotePrestige().IsSuccess, Is.True);
            Guid run2 = h.Session.State.RunId;
            long ip1 = h.Session.Permanent.Ip;
            Assert.That(ip1, Is.EqualTo(ip0 + award1));
            RunCheckpoint afterFirst = h.Active();
            Assert.That(afterFirst.Run.PreviousRunId, Is.EqualTo(run1));

            // The second prestige: the durable active generation has previousRunId = run 1, which is not the live run 2.
            Assert.That(h.Session.State.TryAddCash(45_000_000), Is.EqualTo(RunStateResult.Ok));
            h.Sync();
            long award2 = Confirm(h).IpAwarded;
            Assert.That(award2, Is.GreaterThan(0));
            RunCommandResult<Guid> second = h.Commands.PromotePrestige();

            Assert.That(second.IsSuccess, Is.True, second.ReasonCode);
            Assert.That(h.Session.State.RunNumber, Is.EqualTo(3));
            Assert.That(h.Session.State.PreviousRunId, Is.EqualTo(run2));
            Assert.That(h.Session.Permanent.Ip, Is.EqualTo(ip1 + award2), "the second award is granted, not skipped");
            Assert.That(h.Session.Permanent.PrestigeCount, Is.EqualTo(2));
            RunCheckpoint active = h.Active();
            Assert.That(active.Generation, Is.EqualTo(afterFirst.Generation + 1), "a new generation was written");
            Assert.That(active.Run.RunNumber, Is.EqualTo(3));
            Assert.That(h.Previous().Run.RunNumber, Is.EqualTo(2));
        }

        [Test]
        public void APrestigeWithAnActiveTimedContract_DropsItDurably_AndItsDeadlineNeverExpiresInTheNewRun()
        {
            DurabilityRig h = ReadyToPrestige();
            Assert.That(h.Commands.AcceptContract(SaveV2Scenarios.TimedBar).IsSuccess, Is.True);
            long deadline = h.Session.State.Contracts.Active.DeadlineUtcMs.Value;
            h.Sync();
            DurabilityRig.Ok(h.Persistence.SaveNow());

            PrestigeConfirmation confirmed = Confirm(h);
            Assert.That(confirmed.AbandonedContractOfferId, Is.EqualTo(SaveV2Scenarios.TimedBar));
            Assert.That(h.Commands.PromotePrestige().IsSuccess, Is.True);

            Assert.That(h.Session.State.Contracts.Active, Is.Null);
            Assert.That(h.Session.State.Contracts.LastExpired, Is.Null);
            Assert.That(h.Active().Run.Contracts.Active, Is.Null, "the durable new run has no contract");

            // A return long after the old deadline claims no expiry for the new run.
            DurabilityRig.Ok(h.Persistence.RecordSuspension());
            h.Clock.UnixMs = deadline + 3_600_000L;
            RunPersistenceOutcome returned = DurabilityRig.Ok(h.Persistence.ResumeAndReconcile());
            Assert.That(returned.OfflineSummary.Contract.Kind, Is.EqualTo(RunOfflineContractKind.None));
            Assert.That(h.Session.State.Contracts.LastExpired, Is.Null);
            Assert.That(h.Session.State.Contracts.Active, Is.Null);
        }

        [Test]
        public void WhilePending_SaveNowIsRejected_AutosaveSkips_AndASuspensionAndReturnNeitherCreditNorAwardTwice()
        {
            DurabilityRig h = ReadyToPrestige();
            Guid oldRun = h.Session.State.RunId;
            long ip = h.Session.Permanent.Ip;
            long award = Confirm(h).IpAwarded;
            var files = ReadFilesOf(h);
            h.Backend.ResetCounters();

            DurabilityRig.Rejected(h.Persistence.SaveNow(), RunReasons.TransitionPending);
            Assert.That(DurabilityRig.Ok(h.Persistence.Autosave()).WasNoOp, Is.True);
            Assert.That(h.Backend.Calls, Is.EqualTo(0), "nothing touches the store while the candidate is pending");

            DurabilityRig.Rejected(h.Persistence.RecordSuspension(), RunReasons.TransitionPending);
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended));
            h.Clock.UnixMs += 2 * 3_600_000L;
            RunPersistenceOutcome returned = DurabilityRig.Ok(h.Persistence.ResumeAndReconcile());
            Assert.That(returned.TransitionPending, Is.True);
            Assert.That(returned.OfflineSummary, Is.Null, "the old run is not credited while its successor is pending");
            AssertOldRunNoAward(h, oldRun, ip, "after the return");
            Assert.That(h.Session.PendingTransition, Is.Not.Null);
            Assert.That(ReadFilesOf(h), Is.EqualTo(files), "suspension and return wrote nothing");

            DurabilityRig.Ok(h.Persistence.RetryPendingTransition());
            AssertNewRunOneAward(h, oldRun, ip, award, "retried");
        }

        [Test]
        public void ASuspensionAfterAVisiblePromotion_PublishesTheNewRun_AndTheReturnCreditsItExactlyOnceFromItsOwnDeparture()
        {
            DurabilityRig h = ReadyToPrestige();
            Guid oldRun = h.Session.State.RunId;
            long ip = h.Session.Permanent.Ip;
            long award = Confirm(h).IpAwarded;
            RunCheckpoint promotion = PromoteBehindTheSessionsBack(h, 3);

            // The suspension re-reads the durable generation first: a verified promotion is published.
            DurabilityRig.Ok(h.Persistence.RecordSuspension());
            AssertNewRunOneAward(h, oldRun, ip, award, "suspended");
            RunCheckpoint departure = h.Active();
            Assert.That(departure.Run.RunNumber, Is.EqualTo(2));
            Assert.That(departure.Generation, Is.EqualTo(promotion.Generation + 1), "the departure of the new run");

            long r = departure.DepartureUtcMs + 2 * 3_600_000L;
            h.Clock.UnixMs = r;
            RunPersistenceOutcome returned = DurabilityRig.Ok(h.Persistence.ResumeAndReconcile());
            string reference = DurabilityRig.ReferenceDump(h, departure.Id, r, out RunOfflineSummary expected);
            Assert.That(returned.OfflineSummary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(returned.OfflineSummary.DepartureUtcMs, Is.EqualTo(departure.DepartureUtcMs), "the departure of the new run, never the old run's");
            Assert.That(h.Dump(), Is.EqualTo(reference));
            AssertNewRunOneAward(h, oldRun, ip, award, "returned");
        }

        [Test]
        public void AReturnThatFindsTheVisiblePromotion_CreditsTheNewRunFromTheConfirmedInstant_ExactlyOnce()
        {
            DurabilityRig h = ReadyToPrestige();
            Guid oldRun = h.Session.State.RunId;
            long ip = h.Session.Permanent.Ip;
            long award = Confirm(h).IpAwarded;
            long confirmed = h.Session.PendingTransition.ConfirmedUtcMs;

            // The suspension cannot read the store, so it only suspends; the promotion becomes visible meanwhile.
            h.Backend.FailAll = true;
            DurabilityRig.Rejected(h.Persistence.RecordSuspension(), RunReasons.TransitionPending);
            h.Backend.ClearFault();
            RunCheckpoint promotion = PromoteBehindTheSessionsBack(h, 4);

            long r = confirmed + 3_600_000L;
            h.Clock.UnixMs = r;
            RunPersistenceOutcome returned = DurabilityRig.Ok(h.Persistence.ResumeAndReconcile());

            Assert.That(returned.OfflineSummary, Is.Not.Null);
            Assert.That(returned.OfflineSummary.DepartureUtcMs, Is.EqualTo(promotion.DepartureUtcMs));
            Assert.That(returned.OfflineSummary.MeasuredMs, Is.EqualTo(r - promotion.DepartureUtcMs));
            AssertNewRunOneAward(h, oldRun, ip, award, "returned");
            string reference = DurabilityRig.ReferenceDump(h, promotion.Id, r, out _);
            Assert.That(h.Dump(), Is.EqualTo(reference), "credited once");
            Assert.That(h.Active().ReconciledFromCheckpointId, Is.EqualTo(promotion.Id));
        }

        [Test]
        public void Cancel_AfterAFailedWrite_KeepsTheOldRun_DropsTheCandidate_AndTheNextSaveAndPrestigeWork()
        {
            DurabilityRig h = ReadyToPrestige();
            Guid oldRun = h.Session.State.RunId;
            long ip = h.Session.Permanent.Ip;
            Confirm(h);

            // The candidate is written but its promotion is interrupted: only the pending file exists.
            h.Backend.FailOnFirst("Flush", CheckpointStorageFile.Pending);
            Assert.That(h.Commands.PromotePrestige().IsSuccess, Is.False);
            Assert.That(h.Backend.FaultTriggered, Is.True);
            Assert.That(h.Backend.Files.ContainsKey(CheckpointStorageFile.Pending), Is.True, "a written, unpromoted candidate checkpoint");
            Assert.That(h.Active().Run.RunId, Is.EqualTo(oldRun));

            RunCommandResult<bool> cancelled = h.Commands.CancelPendingTransition();
            Assert.That(cancelled.IsSuccess && cancelled.Value, Is.True);
            Assert.That(h.Session.PendingTransition, Is.Null);
            AssertOldRunNoAward(h, oldRun, ip, "cancelled");
            Assert.That(h.Session.IsLifecycleBlocked, Is.False);

            // The unpromoted pending file does not stand in the way of the next write, which keeps the old run.
            h.Sync();
            DurabilityRig.Ok(h.Persistence.SaveNow());
            Assert.That(h.Active().Run.RunId, Is.EqualTo(oldRun));
            Assert.That(h.Active().Permanent.Ip, Is.EqualTo(ip));

            // A new confirmation is a fresh decision and awards once.
            long award = Confirm(h).IpAwarded;
            Assert.That(h.Commands.PromotePrestige().IsSuccess, Is.True);
            AssertNewRunOneAward(h, oldRun, ip, award, "second attempt");
        }

        [Test]
        public void Cancel_AfterADurablePromotion_IsRejectedAsCommitted_AndTheRetryPublishesIt()
        {
            DurabilityRig h = ReadyToPrestige();
            Guid oldRun = h.Session.State.RunId;
            long ip = h.Session.Permanent.Ip;
            long award = Confirm(h).IpAwarded;
            PromoteBehindTheSessionsBack(h, 5);

            RunCommandResult<bool> cancelled = h.Commands.CancelPendingTransition();

            Assert.That(cancelled.IsSuccess, Is.False);
            Assert.That(cancelled.ReasonCode, Is.EqualTo(RunPersistenceReasons.TransitionCommitted));
            Assert.That(h.Session.PendingTransition, Is.Not.Null, "the transition is still pending and retryable");
            DurabilityRig.Ok(h.Persistence.RetryPendingTransition());
            AssertNewRunOneAward(h, oldRun, ip, award, "retried");
        }

        [Test]
        public void Cancel_WhenTheStoreCannotBeRead_StillKeepsTheOldRun()
        {
            DurabilityRig h = ReadyToPrestige();
            Guid oldRun = h.Session.State.RunId;
            long ip = h.Session.Permanent.Ip;
            Confirm(h);
            h.Backend.FailAll = true;

            RunCommandResult<bool> cancelled = h.Commands.CancelPendingTransition();

            Assert.That(cancelled.IsSuccess && cancelled.Value, Is.True, "a dead store must not trap the player in the pending state");
            AssertOldRunNoAward(h, oldRun, ip, "cancelled");
        }

        [Test]
        public void PromotionThroughAnAttachedPersistence_AlwaysWritesFirst_AndOutsideRunningIsRefused()
        {
            DurabilityRig h = ReadyToPrestige();
            PrestigeConfirmation confirmed = Confirm(h);
            RunCheckpoint before = h.Active();
            long confirmedAt = confirmed.Candidate.ConfirmedUtcMs;

            Assert.That(h.Commands.PromotePrestige().IsSuccess, Is.True);

            Assert.That(h.Active().Generation, Is.EqualTo(before.Generation + 1), "the promotion wrote the candidate before it was published");
            Assert.That(h.Active().Run.RunId, Is.EqualTo(h.Session.State.RunId));
            Assert.That(h.Active().DepartureUtcMs, Is.EqualTo(Math.Max(confirmedAt, before.DepartureUtcMs)),
                "the departure of the new run is the confirmed instant (never earlier than the active generation)");
            Assert.That(h.Active().ReconciledFromCheckpointId, Is.Null, "a prestige is not a return");
            Assert.That(h.Session.State, Is.Not.SameAs(confirmed.Candidate.Run), "the candidate's own objects are not reused after the promotion");
            Assert.That(h.Session.State.Unlocked, Is.EqualTo(confirmed.Candidate.Run.Unlocked), "the install settled to the same fixed point of the unlock pass");
            Assert.That(h.Session.TimelineUtcMs, Is.GreaterThanOrEqualTo(confirmedAt), "the timeline never moves back");

            // Outside the running lifecycle a promotion is refused and the transition stays pending.
            DurabilityRig g = ReadyToPrestige();
            Confirm(g);
            DurabilityRig.Rejected(g.Persistence.RecordSuspension(), RunReasons.TransitionPending);
            RunCommandResult<Guid> refused = g.Commands.PromotePrestige();
            Assert.That(refused.IsSuccess, Is.False);
            Assert.That(refused.ReasonCode, Is.EqualTo(RunPersistenceReasons.LifecycleNoOp));
            Assert.That(g.Session.PendingTransition, Is.Not.Null);
        }

        [Test]
        public void RetryPendingTransition_WithNothingPending_IsRejected()
        {
            DurabilityRig h = ReadyToPrestige();
            DurabilityRig.Rejected(h.Persistence.RetryPendingTransition(), RunReasons.CommandInvalid);
            Assert.That(h.Persistence.CancelPendingTransition().Value, Is.False);

            DurabilityRig notResumed = DurabilityRig.Create(resume: false);
            DurabilityRig.Rejected(notResumed.Persistence.RetryPendingTransition(), RunPersistenceReasons.LifecycleNoOp);
        }

        private static Dictionary<string, byte[]> ReadFiles(string root)
        {
            string directory = RunCheckpointTestSupport.V2Directory(root);
            return Directory.GetFiles(directory).ToDictionary(Path.GetFileName, File.ReadAllBytes);
        }

        private static Dictionary<CheckpointStorageFile, byte[]> ReadFilesOf(DurabilityRig h) => h.Backend.Snapshot();

        [Test]
        public void EveryRunGetsANewId_FromTheDefaultSource()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(Options(), 20_000_000);
            var commands = new RunCommands(rig.Session);
            Guid first = rig.State.RunId;
            Assert.That(commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(commands.PromotePrestige().Value, Is.Not.EqualTo(first).And.Not.EqualTo(Guid.Empty));
            Assert.That(rig.Session.State.PreviousRunId, Is.EqualTo(first));
        }
    }
}
