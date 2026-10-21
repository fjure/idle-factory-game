using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// T340 (owner decision F1 of #395, QA-12 "short-stock expiry line"): the expired-contract snapshot carries <c>AutoDeliverWasOn</c>, so the
    /// Contracts panel reads it instead of panel memory. The flag is captured from <c>ActiveContract.AutoDeliver</c> at the expiry. That setting is
    /// the one Save V3 already persists as <c>activeContract.autoDeliver</c> (validated fail-closed on load, FR-034), so a contract that expires
    /// after a restart or during an offline absence carries the flag without any new save member. The expired outcome itself stays unpersisted
    /// (<c>ContractBoard.LastExpired</c>, data-model.md), so it does not survive a restart once it exists.
    /// Fixture: no machine, so the stock stays short; one timed offer of 5 Iron Ore that lasts 6,000 ms.
    /// </summary>
    public sealed class ExpiredAutoDeliverFlagTests
    {
        private const string Ore = "iron.ore";
        private const string Offer = "contract.timed";
        private const long DurationMs = 6_000;
        private const long Second = 10_000_000;
        private const long StartUtcMs = RunSimHarness.StartUtcMs;

        private static RunCommandFixtures.Rig NewRig()
        {
            var always = new UnlockCondition("always");
            ContractRewardDef[] rp = { new ContractRewardDef("rp", new Curve(2, 1_000_000, "floor")) };
            var options = new RunCommandFixtures.Options
            {
                Offers = new[]
                {
                    new ContractOfferDef(Offer, "timedBar", Ore, new Curve(5, 1_010_000, "ceil"), DurationMs, rp, always,
                        rewardRatioPpm: 1_500_000, cooldownMs: 20_000),
                },
                Ladder = new ContractItemLadder(new[] { new ContractItemLadderEntry(0, "item.boost") }, 10),
                Items = new[]
                {
                    new ItemDef("item.boost", 1, "Boost", "F003-ITEM-BOOST", 60_000, "factory", new string[0],
                        new EffectDef("machineSpeed", "allMachines", null, 1_500_000)),
                },
            };
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(options, 1_000);
            rig.Advance(0); // the first settle unlocks the offer
            return rig;
        }

        private static RunCommandFixtures.Rig Accepted(bool autoDeliver)
        {
            RunCommandFixtures.Rig rig = NewRig();
            Assert.That(rig.Commands.AcceptContract(Offer).IsSuccess, Is.True);
            if (autoDeliver) Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            return rig;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ALiveExpiryCarriesTheAutoDeliverSettingItHadAtTheDeadline(bool autoDeliver)
        {
            RunCommandFixtures.Rig rig = Accepted(autoDeliver);

            rig.Advance(7 * Second);

            ContractBoard board = rig.State.Contracts;
            Assert.That(board.Active, Is.Null, "the contract expired and the setting went with it");
            Assert.That(board.LastExpired.HasValue, Is.True);
            Assert.That(board.LastExpired.Value.AutoDeliverWasOn, Is.EqualTo(autoDeliver));
            ExpiredContractSnapshot snapshot = RunSnapshots.Contracts(rig.Session).LastExpired;
            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot.AutoDeliverWasOn, Is.EqualTo(autoDeliver), "the panel reads the snapshot, not its own memory");
            Assert.That(snapshot.OfferId, Is.EqualTo(Offer));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AContractRestoredByALoadCarriesTheRestoredSettingWhenItExpiresAfterTheRestart(bool autoDeliver)
        {
            // The load path of Save V3: TrySetActive for the persisted contract, then TryRestoreAutoDeliver for activeContract.autoDeliver.
            RunCommandFixtures.Rig rig = NewRig();
            ContractBoard board = rig.State.Contracts;
            Assert.That(board.TrySetActive(new ActiveContract(Offer, StartUtcMs, StartUtcMs + DurationMs, 5)), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.TryRestoreAutoDeliver(autoDeliver), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.LastExpired.HasValue, Is.False, "nothing expired yet; the expired outcome is not part of a save");

            rig.Advance(7 * Second);

            Assert.That(board.Active, Is.Null);
            Assert.That(board.LastExpired.Value.AutoDeliverWasOn, Is.EqualTo(autoDeliver));
            Assert.That(RunSnapshots.Contracts(rig.Session).LastExpired.AutoDeliverWasOn, Is.EqualTo(autoDeliver));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AnOfflineReturnThatExpiresTheContractCarriesTheSetting(bool autoDeliver)
        {
            RunCommandFixtures.Rig rig = Accepted(autoDeliver);
            long departure = rig.Session.TimelineUtcMs;
            var segmenter = new OfflineSegmenter(rig.Catalog);

            RunOfflineSummary summary = segmenter.Reconcile(rig.State, rig.Session.Permanent, departure, departure + DurationMs + 501);

            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(summary.Contract.Kind, Is.EqualTo(RunOfflineContractKind.Expired));
            ContractBoard board = rig.State.Contracts;
            Assert.That(board.Active, Is.Null);
            Assert.That(board.LastExpired.HasValue, Is.True);
            Assert.That(board.LastExpired.Value.AutoDeliverWasOn, Is.EqualTo(autoDeliver));
        }

        [Test]
        public void TheFlagDefaultsToOffForAnExpiredContractBuiltWithoutIt()
        {
            Assert.That(new ExpiredContract(Offer, 5, StartUtcMs + DurationMs).AutoDeliverWasOn, Is.False);
            Assert.That(new ExpiredContract(Offer, 5, StartUtcMs + DurationMs, true).AutoDeliverWasOn, Is.True);
        }
    }
}
