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
    /// The in-memory prestige (T102, contracts/run-application.md, FR-020 - FR-022, FR-024, US4 scenarios 1, 2 and
    /// 4, QA-4): the IP table and the threshold boundary, the preview, the exact reset and preserve lists, unused
    /// Items surviving while active boosts do not, the new run's identity and Industry starting effects, cancelling,
    /// an active untimed contract, and the rejection of a second confirmation while one is pending. The durable
    /// part is T105.
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
