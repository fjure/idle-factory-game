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
    /// T322 (issue #389, FR-004..FR-007a): the production-objective state (<see cref="ActiveContract.Produced"/>, owned by the
    /// <see cref="ContractBoard"/>) and its command paths. Accepting starts it at 0 and skips the delivery-only capacity check, delivering it is
    /// <c>command.invalid</c>, abandoning it goes through the shared ending and starts the cooldown, trade and mining never advance it, and a
    /// prestige resets it. Counting production and completing the objective are T327 (Run/ProduceCompletionTests); here the counter only moves where a test sets it by hand.
    /// </summary>
    public sealed class ProduceObjectiveTests
    {
        private const string Produce = "contract.produce-plate";
        private const string ProduceOverCapacity = "contract.produce-over";
        private const string ProduceOther = "contract.produce-other";
        private const string DeliverOverCapacity = "contract.deliver-over";
        private const string Plate = "iron.plate";
        private const long CooldownMs = 20_000;
        private const long Ms = RunSession.TicksPerMs;

        private static ContractOfferDef[] Offers()
        {
            var always = new UnlockCondition("always");
            ContractRewardDef[] rp = { new ContractRewardDef("rp", new Curve(1, 1_000_000, "floor")) };
            return new[]
            {
                new ContractOfferDef(Produce, "plate", Plate, new Curve(20, 1_000_000, "ceil"), null, rp, always, objective: "produce",
                    rewardRatioPpm: 1_200_000, cooldownMs: CooldownMs),
                // 1,001 is above the storage capacity of level 1 (1,000): a delivery offer of this size is rejected, a production offer is not.
                new ContractOfferDef(ProduceOverCapacity, "plate", Plate, new Curve(1_001, 1_000_000, "ceil"), null, rp, always, objective: "produce",
                    rewardRatioPpm: 1_200_000),
                new ContractOfferDef(ProduceOther, "plate", Plate, new Curve(5, 1_000_000, "ceil"), null, rp, always, objective: "produce",
                    rewardRatioPpm: 1_200_000, cooldownMs: CooldownMs),
                new ContractOfferDef(DeliverOverCapacity, "plate", Plate, new Curve(1_001, 1_000_000, "ceil"), null, rp, always, rewardRatioPpm: 1_000_000),
            };
        }

        private static RunCommandFixtures.Rig NewRig(long cash = 1_000)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(new RunCommandFixtures.Options { Offers = Offers() }, cash);
            rig.Advance(0); // the first settle unlocks the offers
            return rig;
        }

        private static ActiveContractSnapshot ActiveSnapshot(RunCommandFixtures.Rig rig) => RunSnapshots.Contracts(rig.Session).Active;

        // ---- AcceptContract -------------------------------------------------------------------------------------

        [Test]
        public void AcceptingAProductionObjectiveStartsProducedAtZeroAndFixesTheRequiredUnits()
        {
            RunCommandFixtures.Rig rig = NewRig();
            RunCommandResult<ContractAcceptResult> r = rig.Commands.AcceptContract(Produce);
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            Assert.That(r.Value.RequiredUnits, Is.EqualTo(20));
            ActiveContract active = rig.State.Contracts.Active;
            Assert.That(active.OfferId, Is.EqualTo(Produce));
            Assert.That(active.Produced, Is.Zero);
            ActiveContractSnapshot snapshot = ActiveSnapshot(rig);
            Assert.That(snapshot.Objective, Is.EqualTo("produce"));
            Assert.That(snapshot.Produced, Is.Zero);
            Assert.That(snapshot.RequiredUnits, Is.EqualTo(20));
        }

        [Test]
        public void TheCapacityCheckAppliesToDeliveryObjectivesOnly()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Assert.That(rig.State.Inventory.Capacity, Is.EqualTo(1_000));
            Assert.That(rig.Commands.AcceptContract(DeliverOverCapacity).ReasonCode, Is.EqualTo(RunReasons.ContractCapacity));
            Assert.That(RunSnapshots.Contracts(rig.Session).Offers.Single(o => o.OfferId == ProduceOverCapacity).CanAccept, Is.True);

            RunCommandResult<ContractAcceptResult> r = rig.Commands.AcceptContract(ProduceOverCapacity);
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            Assert.That(rig.State.Contracts.Active.RequiredUnits, Is.EqualTo(1_001));
            Assert.That(rig.State.Contracts.Active.Produced, Is.Zero);
        }

        [Test]
        public void AProductionObjectiveFollowsTheNormativeAcceptOrder()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Assert.That(rig.Commands.AcceptContract(Produce).IsSuccess, Is.True);
            Assert.That(rig.Commands.AcceptContract(ProduceOther).ReasonCode, Is.EqualTo(RunReasons.ContractActiveExists));
            Assert.That(rig.Commands.AbandonContract().IsSuccess, Is.True);
            Assert.That(rig.Commands.AcceptContract(Produce).ReasonCode, Is.EqualTo(RunReasons.ContractCooldown), "the cooldown of the ended contract");
            Assert.That(rig.Commands.AcceptContract(ProduceOther).IsSuccess, Is.True, "another offer has its own cooldown");
            Assert.That(rig.Commands.AbandonContract().IsSuccess, Is.True);
            Assert.That(rig.Commands.AcceptContract("contract.unknown").ReasonCode, Is.EqualTo(RunReasons.CommandInvalid));
        }

        // ---- DeliverContract ------------------------------------------------------------------------------------

        [Test]
        public void DeliveringAProductionObjectiveIsCommandInvalidAndChangesNothing()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Assert.That(rig.Commands.AcceptContract(Produce).IsSuccess, Is.True);
            RunSimHarness.Stock(rig.State, Plate, 500); // far more than required: the stock is never the objective
            string before = ContractCooldownTests.Digest(rig);

            Assert.That(rig.Commands.DeliverContract().ReasonCode, Is.EqualTo(RunReasons.CommandInvalid));

            Assert.That(ContractCooldownTests.Digest(rig), Is.EqualTo(before));
            Assert.That(rig.State.Contracts.Active, Is.Not.Null);
            Assert.That(rig.State.Contracts.CompletionCount(Produce), Is.Zero);
            Assert.That(rig.State.Inventory.Units(Plate), Is.EqualTo(500));
            ActiveContractSnapshot snapshot = ActiveSnapshot(rig);
            Assert.That(snapshot.CanDeliver, Is.False, "the snapshot never offers Deliver for a production objective");
            Assert.That(snapshot.DeliverReason, Is.Null);
        }

        [Test]
        public void TheDomainDeliveryRejectsAProductionObjectiveToo()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Assert.That(rig.Commands.AcceptContract(Produce).IsSuccess, Is.True);
            RunSimHarness.Stock(rig.State, Plate, 500);
            RunStateResult r = rig.State.TryDeliverContract(rig.Session.TimelineUtcMs, rig.Session.Permanent, 1_000_000, out long cash, out long rp);
            Assert.That(r, Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(cash, Is.Zero);
            Assert.That(rp, Is.Zero);
            Assert.That(rig.State.Inventory.Units(Plate), Is.EqualTo(500));
            Assert.That(rig.State.Contracts.Active, Is.Not.Null);
        }

        // ---- trade never advances the objective ----------------------------------------------------------------

        [Test]
        public void TradeNeverAdvancesOrCompletesAProductionObjective()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Assert.That(rig.Commands.AcceptContract(Produce).IsSuccess, Is.True);
            ActiveContract active = rig.State.Contracts.Active;
            RunSimHarness.Stock(rig.State, Plate, 100);

            Assert.That(rig.Commands.Sell(Plate, 30).IsSuccess, Is.True);
            Assert.That(rig.Commands.SellAll(Plate).IsSuccess, Is.True);
            RunSimHarness.Stock(rig.State, Plate, 100); // stock above the required 20
            Assert.That(rig.Commands.SetAutoSell(Plate, true).IsSuccess, Is.True);
            rig.Advance(5_000 * Ms);

            Assert.That(rig.State.Contracts.Active, Is.SameAs(active), "still active: stock and sales are not production");
            Assert.That(active.Produced, Is.Zero);
            Assert.That(rig.State.Contracts.CompletionCount(Produce), Is.Zero);
            Assert.That(rig.State.Contracts.RunCompletionTotal, Is.Zero);
            Assert.That(ActiveSnapshot(rig).Produced, Is.Zero);
        }

        // ---- AbandonContract ------------------------------------------------------------------------------------

        [Test]
        public void AbandoningAProductionObjectiveEndsItThroughTheSharedPathAndStartsTheCooldown()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Assert.That(rig.Commands.AcceptContract(Produce).IsSuccess, Is.True);
            long now = rig.Session.TimelineUtcMs;
            long version = rig.State.Contracts.Version;

            RunCommandResult<ContractAbandonResult> r = rig.Commands.AbandonContract();
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            Assert.That(r.Value.CompletionCount, Is.Zero, "abandoning changes no count (OD-4)");

            ContractBoard board = rig.State.Contracts;
            Assert.That(board.Active, Is.Null);
            Assert.That(board.CooldownReadyAtUtcMs(Produce), Is.EqualTo(now + CooldownMs));
            Assert.That(board.CompletionCount(Produce), Is.Zero);
            Assert.That(board.RunCompletionTotal, Is.Zero);
            Assert.That(board.Version, Is.GreaterThan(version));
            Assert.That(rig.Commands.AcceptContract(Produce).ReasonCode, Is.EqualTo(RunReasons.ContractCooldown));

            rig.Advance((CooldownMs - 1) * Ms);
            Assert.That(rig.Commands.AcceptContract(Produce).ReasonCode, Is.EqualTo(RunReasons.ContractCooldown));
            rig.Advance(1 * Ms);
            Assert.That(rig.Commands.AcceptContract(Produce).IsSuccess, Is.True, "ready exactly at readyAt");
            Assert.That(rig.State.Contracts.Active.Produced, Is.Zero, "a fresh objective starts at 0");
        }

        // ---- state restore guard --------------------------------------------------------------------------------

        [Test]
        public void ProducedIsValidatedOnConstructionAndOnRestore()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ActiveContract(Produce, 0, null, 20, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ActiveContract(Produce, 0, null, 20, ExactBound.Value + 1));
            RunCommandFixtures.Rig rig = NewRig();
            ContractBoard board = rig.State.Contracts;
            Assert.That(board.TrySetActive(new ActiveContract(DeliverOverCapacity, 0, null, 7, 3)), Is.EqualTo(RunStateResult.InvalidArgument),
                "a delivery objective has no production progress");
            Assert.That(board.Active, Is.Null);
            Assert.That(board.TrySetActive(new ActiveContract(Produce, 0, null, 20, 3)), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.Active.Produced, Is.EqualTo(3));
        }

        // ---- FR-007a: the prestige reset (QA-8) -----------------------------------------------------------------

        /// <summary>
        /// The produced row of the QA-8 reset cases (ContractCooldownTests holds the cooldown, bonus, active and, since T326, auto-deliver rows). A
        /// new <see cref="RunState"/> builds a new board, so every row resets by the same construction.
        /// </summary>
        [Test]
        public void APrestigeResetsTheProductionProgressAndTheCooldownOfAProductionObjective()
        {
            RunCommandFixtures.Rig rig = NewRig(cash: 10_000_000);
            ContractBoard oldBoard = rig.State.Contracts;
            Assert.That(rig.Commands.AcceptContract(Produce).IsSuccess, Is.True);
            Assert.That(oldBoard.TryCountProduced(7, -1, 0, 0, 0), Is.EqualTo(RunStateResult.Ok)); // progress, counted through the board
            Assert.That(rig.Commands.AbandonContract().IsSuccess, Is.True); // cooldown running
            Assert.That(rig.Commands.AcceptContract(ProduceOther).IsSuccess, Is.True);
            ActiveContract oldActive = oldBoard.Active;
            Assert.That(oldBoard.TryCountProduced(4, -1, 0, 0, 0), Is.EqualTo(RunStateResult.Ok));

            Assert.That(rig.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(rig.Commands.PromotePrestige().IsSuccess, Is.True);
            rig.Session.Advance(0);

            ContractBoard board = rig.Session.State.Contracts;
            Assert.That(board, Is.Not.SameAs(oldBoard));
            Assert.That(board.Active, Is.Null, "the production objective and its progress do not carry over");
            Assert.That(board.CooldownReadyAtUtcMs(Produce), Is.Null);
            Assert.That(rig.Commands.AcceptContract(Produce).IsSuccess, Is.True, "no cooldown in the new run");
            Assert.That(rig.Session.State.Contracts.Active.Produced, Is.Zero);
            Assert.That(oldActive.Produced, Is.EqualTo(4), "the ended run is not touched");
        }
    }
}
