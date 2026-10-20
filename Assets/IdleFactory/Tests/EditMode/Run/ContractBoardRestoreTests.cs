using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;
using static IdleFactory.Tests.EditMode.Fixtures.Run.ContractFixtures;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// The public restore surface of <see cref="ContractBoard"/> for Save V3 (T334, issue #399 follow-ups of T320, T321, T322 and T327): the
    /// enumerations the writer persists and the <c>TryRestore*</c> setters the mapper calls. Each setter validates against the catalog and its
    /// bounds, changes nothing when rejected, and counts a change in <see cref="ContractBoard.Version"/>; <see cref="RunState.Validate"/> checks the
    /// board invariants.
    /// </summary>
    public sealed class ContractBoardRestoreTests
    {
        private static ContractBoard NewBoard() => new ContractBoard(RunCommandFixtures.Catalog(Options()));

        private static void Ok(RunStateResult result) => Assert.That(result, Is.EqualTo(RunStateResult.Ok));

        private static ContractBoard WithActive(string offerId, long required = 5)
        {
            ContractBoard board = NewBoard();
            Ok(board.TrySetActive(new ActiveContract(offerId, StartUtcMs, null, required)));
            return board;
        }

        // ---- cooldowns ----------------------------------------------------------------------------------------------------

        [Test]
        public void TryRestoreCooldown_ValidatesTheOfferAndTheBound_AndIsEnumeratedInOfferOrder()
        {
            ContractBoard board = NewBoard();
            long version = board.Version;
            Assert.That(board.TryRestoreCooldown("contract.unknown", 5), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(board.TryRestoreCooldown(null, 5), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(board.TryRestoreCooldown(Deliver, -1), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(board.TryRestoreCooldown(Deliver, ExactBound.Value + 1), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(board.Version, Is.EqualTo(version), "a rejection changes nothing");
            Assert.That(board.CooldownStamps(), Is.Empty);

            Ok(board.TryRestoreCooldown(Produce, StartUtcMs + 9));
            Ok(board.TryRestoreCooldown(Deliver, ExactBound.Value));
            Ok(board.TryRestoreCooldown(BonusAlways, 0));
            Assert.That(board.Version, Is.EqualTo(version + 3));
            Assert.That(board.CooldownStamps().Select(c => c.OfferId + "@" + c.ReadyAtUtcMs),
                Is.EqualTo(new[] { BonusAlways + "@0", Deliver + "@" + ExactBound.Value, Produce + "@" + (StartUtcMs + 9) }), "ordinal offer order");
            Assert.That(board.CooldownRemainingMs(Produce, StartUtcMs), Is.EqualTo(9));
            Assert.That(board.CooldownRemainingMs(BonusAlways, StartUtcMs), Is.Zero, "a past stamp is ready");
        }

        // ---- bonus state --------------------------------------------------------------------------------------------------

        [Test]
        public void TryRestoreBonus_AcceptsBonusOffersOnly_OncePerOffer_WithinTheBound()
        {
            ContractBoard board = NewBoard();
            long version = board.Version;
            Assert.That(board.TryRestoreBonus(Deliver, StartUtcMs, false), Is.EqualTo(RunStateResult.InvalidArgument), "a standing offer has no bonus state");
            Assert.That(board.TryRestoreBonus("contract.unknown", StartUtcMs, false), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(board.TryRestoreBonus(BonusAlways, -1, false), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(board.TryRestoreBonus(BonusAlways, ExactBound.Value + 1, true), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(board.Version, Is.EqualTo(version));

            Ok(board.TryRestoreBonus(BonusOre, StartUtcMs + 1_000, false));
            Ok(board.TryRestoreBonus(BonusAlways, StartUtcMs + 500, true));
            Assert.That(board.TryRestoreBonus(BonusOre, StartUtcMs + 2_000, true), Is.EqualTo(RunStateResult.InvalidArgument), "restored once");
            Assert.That(board.Version, Is.EqualTo(version + 2));
            Assert.That(board.BonusOffers().Select(b => b.OfferId + "@" + b.WindowEndUtcMs + "/" + b.Consumed),
                Is.EqualTo(new[] { BonusAlways + "@" + (StartUtcMs + 500) + "/True", BonusOre + "@" + (StartUtcMs + 1_000) + "/False" }));

            Assert.That(board.IsBonusAvailable(BonusOre, StartUtcMs + 999), Is.True, "triggered, unused, inside its window");
            Assert.That(board.IsBonusAvailable(BonusOre, StartUtcMs + 1_000), Is.False, "the window ended");
            Assert.That(board.IsBonusAvailable(BonusAlways, StartUtcMs), Is.False, "the single use is spent");
            Assert.That(board.BonusState(BonusAlways).Triggered, Is.True);
        }

        // ---- production count and auto-deliver ----------------------------------------------------------------------------

        [Test]
        public void TryRestoreProduced_NeedsAnActiveProductionContract_AndStaysWithinZeroAndRequired()
        {
            Assert.That(NewBoard().TryRestoreProduced(0), Is.EqualTo(RunStateResult.ContractNoneActive));

            ContractBoard delivery = WithActive(Deliver);
            Assert.That(delivery.TryRestoreProduced(1), Is.EqualTo(RunStateResult.InvalidArgument), "a delivery objective has no production count");
            Ok(delivery.TryRestoreProduced(0));

            ContractBoard production = WithActive(Produce, 5);
            long version = production.Version;
            Assert.That(production.TryRestoreProduced(-1), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(production.TryRestoreProduced(6), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(production.Version, Is.EqualTo(version));
            Assert.That(production.Active.Produced, Is.Zero);
            Ok(production.TryRestoreProduced(3));
            Assert.That(production.Active.Produced, Is.EqualTo(3));
            Ok(production.TryRestoreProduced(5));
            Assert.That(production.Active.Produced, Is.EqualTo(5), "equal to required is valid (completes at the first settle)");
            Assert.That(production.Version, Is.EqualTo(version + 2));
        }

        [Test]
        public void TryRestoreAutoDeliver_IsOnlyOnForADeliveryObjective()
        {
            Assert.That(NewBoard().TryRestoreAutoDeliver(true), Is.EqualTo(RunStateResult.ContractNoneActive));
            ContractBoard production = WithActive(Produce);
            Assert.That(production.TryRestoreAutoDeliver(true), Is.EqualTo(RunStateResult.InvalidArgument));
            Ok(production.TryRestoreAutoDeliver(false));
            Assert.That(production.Active.AutoDeliver, Is.False);

            ContractBoard delivery = WithActive(Deliver);
            long version = delivery.Version;
            Ok(delivery.TryRestoreAutoDeliver(true));
            Assert.That(delivery.Active.AutoDeliver, Is.True);
            Assert.That(delivery.Active.AutoDeliverBlocked, Is.False);
            Assert.That(delivery.Version, Is.EqualTo(version + 1));
        }

        [Test]
        public void TrySetActive_RejectsAProductionCountAboveTheRequiredQuantity()
        {
            ContractBoard board = NewBoard();
            Assert.That(board.TrySetActive(new ActiveContract(Produce, StartUtcMs, null, 5, 6)), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(board.Active, Is.Null);
            Ok(board.TrySetActive(new ActiveContract(Produce, StartUtcMs, null, 5, 5)));
        }

        // ---- the run-wide total (SEC-8) -----------------------------------------------------------------------------------

        [Test]
        public void TryRestoreRunCompletionTotal_KeepsTheMaximumOfSavedAndDerived_AndIsNeverLowered()
        {
            ContractBoard board = NewBoard();
            Ok(board.TrySetCompletionCount(Deliver, 3));
            Assert.That(board.RunCompletionTotal, Is.EqualTo(3), "derived from the counts (the V2 rule)");
            Ok(board.TryRestoreRunCompletionTotal(7));
            Assert.That(board.RunCompletionTotal, Is.EqualTo(7));
            Ok(board.TryRestoreRunCompletionTotal(2));
            Assert.That(board.RunCompletionTotal, Is.EqualTo(7), "never lowered");
            Assert.That(board.TryRestoreRunCompletionTotal(-1), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(board.TryRestoreRunCompletionTotal(ExactBound.Value + 1), Is.EqualTo(RunStateResult.ValueLimit));
            Assert.That(board.RunCompletionTotal, Is.EqualTo(7));

            // A count dropped as content drift (here: never restored) does not lower the persisted total.
            ContractBoard drifted = NewBoard();
            Ok(drifted.TryRestoreRunCompletionTotal(3));
            Assert.That(drifted.RunCompletionTotal, Is.EqualTo(3));
            Assert.That(drifted.CompletionCounts(), Is.Empty);
        }

        // ---- RunState.Validate covers the board ---------------------------------------------------------------------------

        [Test]
        public void Validate_ChecksTheBoardInvariants()
        {
            RunCommandFixtures.Rig rig = Rig();
            Assert.That(rig.State.Validate(), Is.EqualTo(RunStateResult.Ok));
            Accept(rig, Produce);
            Assert.That(rig.State.Validate(), Is.EqualTo(RunStateResult.Ok));
            Board(rig).Active.Produced = Board(rig).Active.RequiredUnits + 1; // only reachable by a defect; the restore paths reject it
            Assert.That(rig.State.Validate(), Is.EqualTo(RunStateResult.InvariantViolated));
            Board(rig).Active.Produced = 0;
            Board(rig).Active.AutoDeliver = true; // auto-deliver on a production objective
            Assert.That(rig.State.Validate(), Is.EqualTo(RunStateResult.InvariantViolated));
            Board(rig).Active.AutoDeliver = false;
            Assert.That(rig.State.Validate(), Is.EqualTo(RunStateResult.Ok));
        }

        [Test]
        public void TheEnumerationsReflectTheLiveBoard()
        {
            RunCommandFixtures.Rig rig = Rig();
            ContractBoard board = Board(rig);
            Assert.That(board.BonusOffers().Select(b => b.OfferId), Is.EqualTo(new[] { BonusAlways }), "the always-trigger fired at the first settle");
            Assert.That(board.CooldownStamps(), Is.Empty);
            Accept(rig, Deliver);
            RunSimHarness.Stock(rig.State, Ore, 5);
            long now = rig.Session.TimelineUtcMs;
            Assert.That(rig.Commands.DeliverContract().IsSuccess, Is.True);
            Assert.That(board.CooldownStamps().Single().OfferId, Is.EqualTo(Deliver));
            Assert.That(board.CooldownStamps().Single().ReadyAtUtcMs, Is.EqualTo(now + CooldownMs));
            Assert.That(board.CooldownStamps().Single().ReadyAtUtcMs, Is.EqualTo(board.CooldownReadyAtUtcMs(Deliver)));
        }
    }
}
