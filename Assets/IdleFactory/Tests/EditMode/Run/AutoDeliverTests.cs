using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IdleFactory.Core.Numerics;
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
    /// T326 (issue #391, F5-R05, FR-010..FR-012, D-L, PF-1, AR2-4): auto-delivery inside the simulator, the scratch commit, the reserve of every active
    /// delivery contract, the blocked state and the <c>SetAutoDeliver</c> command. The full partition-invariance matrix (equivalence cases (l) to (q))
    /// is T328; this file holds the unit behaviour and the two pinned cases (r) <c>SetAutoDeliver</c> delivers at t = 0 and (s) a block retries.
    ///
    /// Fixture: a Miner makes 2 Iron Ore every 2 s (completions at 2 s, 4 s, 6 s, ...); every offer asks for Iron Ore (base price 1) so the numbers stay
    /// small: 5 units, ratio 1.5 gives a Cash reward of 7 (floor of 5 x 1.5), RP 2 and the ladder grants Boost at run index 0. The untimed offer has a
    /// 30 s cooldown; the timed offers last 6,000 ms and 5,999 ms, so the 3rd completion at 6 s is on the inclusive deadline for the first and on its
    /// expiry boundary (deadline + 1 ms) for the second.
    /// </summary>
    public sealed class AutoDeliverTests
    {
        private const string Ore = "iron.ore";
        private const string Gear = "iron.gear";
        private const string Boost = "item.boost";
        private const string OreOffer = "contract.ore";
        private const string OreShort = "contract.ore-short";
        private const string OreTight = "contract.ore-tight";
        private const string FreeOffer = "contract.free";
        private const string ProduceOffer = "contract.produce";
        private const long CooldownMs = 30_000;
        private const long ShortCooldownMs = 20_000;
        private const long Second = 10_000_000;
        private const long StartUtcMs = RunSimHarness.StartUtcMs;

        private static RunCommandFixtures.Rig Rig(long cash = 1_000)
        {
            var always = new UnlockCondition("always");
            ContractRewardDef[] rp = { new ContractRewardDef("rp", new Curve(2, 1_000_000, "floor")) };
            var o = new RunCommandFixtures.Options
            {
                Offers = new[]
                {
                    new ContractOfferDef(OreOffer, "plate", Ore, new Curve(5, 1_010_000, "ceil"), null, rp, always,
                        rewardRatioPpm: 1_500_000, cooldownMs: CooldownMs),
                    new ContractOfferDef(OreShort, "timedBar", Ore, new Curve(5, 1_010_000, "ceil"), 6_000, rp, always,
                        rewardRatioPpm: 1_500_000, cooldownMs: ShortCooldownMs),
                    new ContractOfferDef(OreTight, "timedBar", Ore, new Curve(5, 1_010_000, "ceil"), 5_999, rp, always,
                        rewardRatioPpm: 1_500_000, cooldownMs: ShortCooldownMs),
                    new ContractOfferDef(FreeOffer, "plate", Ore, new Curve(4, 1_010_000, "ceil"), null, rp, always, rewardRatioPpm: 1_000_000),
                    new ContractOfferDef(ProduceOffer, "plate", Ore, new Curve(20, 1_000_000, "ceil"), null, rp, always, objective: "produce",
                        rewardRatioPpm: 1_200_000),
                },
                Ladder = new ContractItemLadder(new[] { new ContractItemLadderEntry(0, Boost) }, 10),
                Items = new[]
                {
                    new ItemDef(Boost, 1, "Boost", "F003-ITEM-BOOST", 60_000, "factory", new string[0],
                        new EffectDef("machineSpeed", "allMachines", null, 1_500_000)),
                },
            };
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(o, cash);
            rig.Advance(0); // the first settle unlocks the offers
            return rig;
        }

        /// <summary>A run with one Miner at slot 0, optionally selling Iron Ore automatically, an accepted offer and optionally auto-deliver on.</summary>
        private static RunCommandFixtures.Rig MiningRig(string offer, bool autoDeliver, bool autoSellOre = false)
        {
            RunCommandFixtures.Rig rig = Rig();
            RunSimHarness.Add(rig.State, RunCommandFixtures.Miner, 0, 100);
            if (autoSellOre) Assert.That(rig.Commands.SetAutoSell(Ore, true).IsSuccess, Is.True);
            Accept(rig, offer);
            if (autoDeliver) Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            return rig;
        }

        /// <summary>Auto-deliver on for an untimed offer, Iron Ore on auto-sell, cash and stock set so that the reward of 7 cannot be granted (value.limit).</summary>
        private static RunCommandFixtures.Rig BlockedRig()
        {
            RunCommandFixtures.Rig rig = Rig(cash: 100);
            Accept(rig, OreOffer);
            Assert.That(rig.Commands.SetAutoSell(Ore, true).IsSuccess, Is.True);
            Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            Assert.That(rig.State.TryAddCash(ExactBound.Value - 106), Is.EqualTo(RunStateResult.Ok)); // Cash = 10^18 - 6
            RunSimHarness.Stock(rig.State, Ore, 7); // 7 ore: the reserve keeps 5, auto-sell takes 2 (Cash 10^18 - 4), the reward 7 would pass 10^18
            return rig;
        }

        private static void Accept(RunCommandFixtures.Rig rig, string offerId)
        {
            RunCommandResult<ContractAcceptResult> r = rig.Commands.AcceptContract(offerId);
            Assert.That(r.IsSuccess, Is.True, offerId + ": " + r.ReasonCode);
        }

        private static ContractBoard Board(RunCommandFixtures.Rig rig) => rig.State.Contracts;

        private static long Stock(RunCommandFixtures.Rig rig, string resourceId) => rig.State.Inventory.Units(resourceId);

        private static long Items(RunCommandFixtures.Rig rig) => rig.Session.Permanent.ItemCount(Boost);

        /// <summary>Everything an advance can change about the run and its contracts, as text.</summary>
        private static string Digest(RunCommandFixtures.Rig rig)
        {
            RunState s = rig.State;
            ContractBoard b = s.Contracts;
            var sb = new StringBuilder(RunSimHarness.Fingerprint(s, rig.Session.Permanent));
            sb.Append("items=").Append(Items(rig)).Append(" total=").Append(b.RunCompletionTotal).Append('\n');
            foreach (string id in rig.Catalog.ContractOffers.Keys.OrderBy(k => k, StringComparer.Ordinal))
                sb.Append(id).Append(" n=").Append(b.CompletionCount(id)).Append(" ready=").Append(b.CooldownReadyAtUtcMs(id)).Append('\n');
            sb.Append("active=").Append(b.Active == null ? "-" : b.Active.OfferId + "/auto=" + b.Active.AutoDeliver + "/blocked=" + b.Active.AutoDeliverBlocked).Append('\n');
            sb.Append("expired=").Append(b.LastExpired.HasValue ? b.LastExpired.Value.OfferId + "@" + b.LastExpired.Value.DeadlineUtcMs : "-").Append('\n');
            sb.Append("now=").Append(rig.Session.TimelineUtcMs).Append(" ticks=").Append(rig.Session.ActiveTicksSinceAnchor);
            return sb.ToString();
        }

        /// <summary>Advances <paramref name="total"/> ticks in one call (no cuts) or cut at the given offsets.</summary>
        private static void AdvanceCut(RunCommandFixtures.Rig rig, long total, params long[] cuts)
        {
            long at = 0;
            foreach (long cut in cuts.Where(c => c > 0 && c < total).Distinct().OrderBy(c => c).Concat(new[] { total }))
            {
                rig.Advance(cut - at);
                at = cut;
            }
        }

        private static readonly long[] BoundaryCuts =
        {
            1, 10_000, 19_999_999, 20_000_000, 33_333_333, 59_999_999, 60_000_000, 60_000_001, 60_009_999, 60_010_000, 60_010_001, 65_000_001,
        };

        // ---- (r) SetAutoDeliver delivers at t = 0 ---------------------------------------------------------------------

        [Test]
        public void SetAutoDeliverOnAStockedContractDeliversAtTimeZero()
        {
            RunCommandFixtures.Rig rig = Rig();
            Accept(rig, OreOffer);
            RunSimHarness.Stock(rig.State, Ore, 5);
            long ticks = rig.Session.ActiveTicksSinceAnchor;
            long now = rig.Session.TimelineUtcMs;

            RunCommandResult<bool> r = rig.Commands.SetAutoDeliver(true);

            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            Assert.That(r.Value, Is.True);
            ContractBoard board = Board(rig);
            Assert.That(board.Active, Is.Null, "the settle of the command delivered it");
            Assert.That(Stock(rig, Ore), Is.Zero);
            Assert.That(rig.State.Cash, Is.EqualTo(1_000 + 7));
            Assert.That(rig.State.Rp, Is.EqualTo(2));
            Assert.That(Items(rig), Is.EqualTo(1), "the ladder grants Boost at run index 0");
            Assert.That(board.CompletionCount(OreOffer), Is.EqualTo(1));
            Assert.That(board.RunCompletionTotal, Is.EqualTo(1));
            Assert.That(board.CooldownReadyAtUtcMs(OreOffer), Is.EqualTo(now + CooldownMs), "the cooldown starts at the command instant");
            Assert.That(rig.Session.ActiveTicksSinceAnchor, Is.EqualTo(ticks), "no time passed: t = 0");
            Assert.That(rig.Session.TimelineUtcMs, Is.EqualTo(now));
            Assert.That(RunSnapshots.Contracts(rig.Session).Active, Is.Null);
        }

        [Test]
        public void SetAutoDeliverSellsOnlyTheRemainderOfTheStock()
        {
            RunCommandFixtures.Rig rig = Rig();
            Assert.That(rig.Commands.SetAutoSell(Ore, true).IsSuccess, Is.True);
            Accept(rig, OreOffer);
            RunSimHarness.Stock(rig.State, Ore, 12); // arrives without a settle, as a completion does between two settles

            Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);

            Assert.That(Board(rig).Active, Is.Null);
            Assert.That(Stock(rig, Ore), Is.Zero, "5 delivered, the 7 left over sold");
            Assert.That(rig.State.Cash, Is.EqualTo(1_000 + 7 * 1 + 7), "7 sold at 1 plus the reward of 7");
        }

        [Test]
        public void AnAutoDeliveryGrantsExactlyWhatTheManualDeliveryGrants()
        {
            RunCommandFixtures.Rig manual = Rig();
            Accept(manual, OreOffer);
            RunSimHarness.Stock(manual.State, Ore, 5);
            RunCommandResult<ContractDeliveryResult> delivered = manual.Commands.DeliverContract();
            Assert.That(delivered.IsSuccess, Is.True, delivered.ReasonCode);

            RunCommandFixtures.Rig auto = Rig();
            Accept(auto, OreOffer);
            RunSimHarness.Stock(auto.State, Ore, 5);
            Assert.That(auto.Commands.SetAutoDeliver(true).IsSuccess, Is.True);

            Assert.That(delivered.Value.CashReward, Is.EqualTo(7));
            Assert.That(auto.State.Cash, Is.EqualTo(manual.State.Cash));
            Assert.That(auto.State.Rp, Is.EqualTo(manual.State.Rp));
            Assert.That(Items(auto), Is.EqualTo(Items(manual)));
            Assert.That(Board(auto).CompletionCount(OreOffer), Is.EqualTo(Board(manual).CompletionCount(OreOffer)));
            Assert.That(Board(auto).RunCompletionTotal, Is.EqualTo(Board(manual).RunCompletionTotal));
            Assert.That(Board(auto).CooldownReadyAtUtcMs(OreOffer), Is.EqualTo(Board(manual).CooldownReadyAtUtcMs(OreOffer)));
            Assert.That(Stock(auto, Ore), Is.EqualTo(Stock(manual, Ore)));
            Assert.That(Board(auto).Active, Is.Null);
            Assert.That(Board(manual).Active, Is.Null);
        }

        [Test]
        public void WithAutoDeliverOffTheContractIsNeverDeliveredByTheSimulator()
        {
            RunCommandFixtures.Rig rig = MiningRig(OreOffer, autoDeliver: false);
            rig.Advance(10 * Second);
            Assert.That(Board(rig).Active, Is.Not.Null, "behaviour equals Feature 003: only the player delivers");
            Assert.That(Stock(rig, Ore), Is.EqualTo(10), "five completions of 2 ore, none delivered, none sold");
            Assert.That(Board(rig).CompletionCount(OreOffer), Is.Zero);
            Assert.That(rig.State.Cash, Is.EqualTo(1_000));
        }

        // ---- delivery inside an advance -------------------------------------------------------------------------------

        [Test]
        public void TheStockCrossingMidAdvanceIsDeliveredAtTheCompletionTick()
        {
            RunCommandFixtures.Rig rig = MiningRig(OreOffer, autoDeliver: true);
            Assert.That(Board(rig).Active.AutoDeliver, Is.True);

            rig.Advance(6 * Second - 1);
            Assert.That(Board(rig).Active, Is.Not.Null, "two completions: 4 ore, one tick short of the third");
            Assert.That(Stock(rig, Ore), Is.EqualTo(4));

            rig.Advance(1);
            ContractBoard board = Board(rig);
            Assert.That(board.Active, Is.Null, "the third completion made 6 ore at 6 s and the contract was delivered at that tick");
            Assert.That(Stock(rig, Ore), Is.EqualTo(1), "exactly the required 5 were consumed");
            Assert.That(rig.State.Cash, Is.EqualTo(1_000 + 7));
            Assert.That(rig.State.Rp, Is.EqualTo(2));
            Assert.That(Items(rig), Is.EqualTo(1));
            Assert.That(board.CompletionCount(OreOffer), Is.EqualTo(1));
            Assert.That(board.CooldownReadyAtUtcMs(OreOffer), Is.EqualTo(StartUtcMs + 6_000 + CooldownMs), "stamped with the real instant of the delivery tick");
            Assert.That(board.LastExpired, Is.Null);
        }

        [Test]
        public void ADeliveryIsTheSameForOneAdvanceAndForAnyPartition()
        {
            RunCommandFixtures.Rig single = MiningRig(OreOffer, autoDeliver: true);
            AdvanceCut(single, 10 * Second);
            string expected = Digest(single);
            Assert.That(Board(single).Active, Is.Null);
            Assert.That(Stock(single, Ore), Is.EqualTo(5), "6 - 5 + 2 + 2");

            RunCommandFixtures.Rig cut = MiningRig(OreOffer, autoDeliver: true);
            AdvanceCut(cut, 10 * Second, BoundaryCuts);
            Assert.That(Digest(cut), Is.EqualTo(expected));

            RunCommandFixtures.Rig frames = MiningRig(OreOffer, autoDeliver: true);
            for (int i = 0; i < 1000; i++) frames.Advance(10 * Second / 1000 + (i % 7 == 0 ? 1 : 0) - (i % 7 == 1 ? 1 : 0));
            Assert.That(frames.Session.ActiveTicksSinceAnchor, Is.EqualTo(single.Session.ActiveTicksSinceAnchor));
            Assert.That(Digest(frames), Is.EqualTo(expected));
        }

        // ---- the deadline boundary --------------------------------------------------------------------------------------

        [TestCase(OreShort, true)]
        [TestCase(OreTight, false)]
        public void TheDeadlineBoundaryIsOneMillisecondAfterTheInclusiveDeadline(string offer, bool delivered)
        {
            RunCommandFixtures.Rig single = MiningRig(offer, autoDeliver: true);
            AdvanceCut(single, 7 * Second);
            ContractBoard board = Board(single);
            if (delivered)
            {
                // The deadline is accepted + 6,000 ms, inclusive; the third completion is at 6,000 ms exactly, before the expiry boundary at 6,001 ms.
                Assert.That(board.Active, Is.Null);
                Assert.That(board.LastExpired, Is.Null, "delivered, never expired as well");
                Assert.That(board.CompletionCount(offer), Is.EqualTo(1));
                Assert.That(single.State.Cash, Is.EqualTo(1_000 + 7));
                Assert.That(Stock(single, Ore), Is.EqualTo(1));
                Assert.That(board.CooldownReadyAtUtcMs(offer), Is.EqualTo(StartUtcMs + 6_000 + ShortCooldownMs));
            }
            else
            {
                // The deadline is accepted + 5,999 ms; its expiry boundary, 6,000 ms, is the tick of the third completion. Boundary events come first.
                Assert.That(board.Active, Is.Null);
                Assert.That(board.LastExpired.HasValue, Is.True, "expired at the boundary, before the completion could deliver");
                Assert.That(board.LastExpired.Value.OfferId, Is.EqualTo(offer));
                Assert.That(board.CompletionCount(offer), Is.Zero, "no reward");
                Assert.That(single.State.Cash, Is.EqualTo(1_000));
                Assert.That(Stock(single, Ore), Is.EqualTo(6), "the completion still happened, nothing was delivered or reserved");
                Assert.That(board.CooldownReadyAtUtcMs(offer), Is.EqualTo(StartUtcMs + 5_999 + 1 + ShortCooldownMs), "the cooldown starts at deadline + 1");
            }

            RunCommandFixtures.Rig cut = MiningRig(offer, autoDeliver: true);
            AdvanceCut(cut, 7 * Second, BoundaryCuts);
            Assert.That(Digest(cut), Is.EqualTo(Digest(single)));
        }

        // ---- the reserve (D-L) ------------------------------------------------------------------------------------------

        [Test]
        public void AutoSellKeepsTheRequiredStockWhileADeliveryContractIsActive_WithOrWithoutAutoDeliver()
        {
            RunCommandFixtures.Rig rig = MiningRig(OreOffer, autoDeliver: false, autoSellOre: true);
            RunSimulator sim = new RunSimulator(rig.Catalog);
            long[] before = RunSimHarness.Units(rig.State);

            RunAdvanceResult result = sim.Advance(rig.State, rig.Session.Permanent, 10 * Second, TimelineMapping.Live(StartUtcMs),
                RunSimHarness.Events(rig.State, StartUtcMs, 0, 10 * Second));

            Assert.That(result, Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(Stock(rig, Ore), Is.EqualTo(5), "min(stock, required) is kept; 2, 4, then 6 ore: 1 sold, 7 ore: 2 sold, 7 ore: 2 sold");
            Assert.That(rig.State.Cash, Is.EqualTo(1_000 + 5));
            Assert.That(sim.LastSoldUnits(Ore), Is.EqualTo(5));
            Assert.That(sim.LastProducedUnits(Ore), Is.EqualTo(10));
            Assert.That(sim.LastDeliveredUnits(Ore), Is.Zero);
            Assert.That(Board(rig).Active, Is.Not.Null, "a reserve never delivers by itself");
            AssertFlow(sim, before, rig.State);
        }

        [Test]
        public void WithAutoSellOnAndAutoDeliverOnOnlyTheRemainderIsSold()
        {
            RunCommandFixtures.Rig rig = MiningRig(OreOffer, autoDeliver: true, autoSellOre: true);
            var sim = new RunSimulator(rig.Catalog);
            long[] before = RunSimHarness.Units(rig.State);

            RunAdvanceResult result = sim.Advance(rig.State, rig.Session.Permanent, 10 * Second, TimelineMapping.Live(StartUtcMs),
                RunSimHarness.Events(rig.State, StartUtcMs, 0, 10 * Second));

            Assert.That(result, Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(sim.LastAutoDelivered, Is.True);
            Assert.That(sim.LastAutoDeliveredCash, Is.EqualTo(7));
            Assert.That(sim.LastAutoDeliveredRp, Is.EqualTo(2));
            Assert.That(sim.LastAutoDeliveredUtcMs, Is.EqualTo(StartUtcMs + 6_000));
            Assert.That(sim.LastDeliveredUnits(Ore), Is.EqualTo(5));
            Assert.That(sim.LastProducedUnits(Ore), Is.EqualTo(10));
            Assert.That(sim.LastSoldUnits(Ore), Is.EqualTo(5), "1 left after the delivery at 6 s, then 2 and 2 once the contract is gone");
            Assert.That(Stock(rig, Ore), Is.Zero);
            Assert.That(rig.State.Cash, Is.EqualTo(1_000 + 7 + 5));
            Assert.That(Board(rig).Active, Is.Null);
            Assert.That(sim.LastMaxPassesPerTimestamp, Is.LessThanOrEqualTo(rig.State.Machines.Count + 2));
            AssertFlow(sim, before, rig.State);
        }

        [Test]
        public void AProductionObjectiveReservesNothing()
        {
            RunCommandFixtures.Rig rig = Rig();
            Accept(rig, ProduceOffer);
            Assert.That(rig.Commands.SetAutoSell(Ore, true).IsSuccess, Is.True);
            RunSimHarness.Stock(rig.State, Ore, 10);

            rig.Advance(0);

            Assert.That(Stock(rig, Ore), Is.Zero, "a production objective is never delivered from stock, so no stock is reserved for it");
            Assert.That(rig.State.Cash, Is.EqualTo(1_000 + 10));
        }

        [Test]
        public void AnExpiryReleasesTheReserveAtTheSameTimestamp()
        {
            RunCommandFixtures.Rig rig = Rig();
            Assert.That(rig.Commands.SetAutoSell(Ore, true).IsSuccess, Is.True);
            Accept(rig, OreShort);
            RunSimHarness.Stock(rig.State, Ore, 10);
            rig.Advance(0);
            Assert.That(Stock(rig, Ore), Is.EqualTo(5), "reserved while the contract is active");

            rig.Advance(7 * Second); // the expiry boundary is 6,001 ms

            Assert.That(Board(rig).LastExpired.HasValue, Is.True);
            Assert.That(Stock(rig, Ore), Is.Zero, "the released stock was sold at the expiry instant, not left behind");
            Assert.That(rig.State.Cash, Is.EqualTo(1_000 + 5 + 5));
            Assert.That(Board(rig).CooldownReadyAtUtcMs(OreShort), Is.EqualTo(StartUtcMs + 6_001 + ShortCooldownMs));
        }

        // ---- (s) a block retries ----------------------------------------------------------------------------------------

        [Test]
        public void AValueLimitBlocksTheAutoDeliveryWithoutChange_AndTheNextInstantRetries()
        {
            RunCommandFixtures.Rig rig = BlockedRig();
            ContractBoard board = Board(rig);
            long version = board.Version;

            rig.Advance(0);

            Assert.That(board.Active, Is.Not.Null, "the contract stays active");
            Assert.That(board.Active.AutoDeliver, Is.True, "with the flag on");
            Assert.That(board.Active.AutoDeliverBlocked, Is.True, "and the blocked state set");
            Assert.That(Stock(rig, Ore), Is.EqualTo(5), "nothing consumed; the reserve kept the stock from auto-sell (the 2 over it were sold)");
            Assert.That(rig.State.Cash, Is.EqualTo(ExactBound.Value - 4));
            Assert.That(rig.State.Rp, Is.Zero, "nothing granted");
            Assert.That(Items(rig), Is.Zero);
            Assert.That(board.CompletionCount(OreOffer), Is.Zero);
            Assert.That(board.Version, Is.EqualTo(version + 1), "the blocked state is a contract-state change");
            ActiveContractSnapshot snapshot = RunSnapshots.Contracts(rig.Session).Active;
            Assert.That(snapshot.AutoDeliver, Is.True);
            Assert.That(snapshot.AutoDeliverBlocked, Is.True, "the snapshot shows the blocked state");
            Assert.That(snapshot.CanDeliver, Is.True, "the stock is there");

            // Every later instant retries and stays blocked; the state does not change again, so no further change is announced.
            long structure = rig.Session.StructureRevision;
            rig.Advance(Second);
            rig.Advance(1);
            Assert.That(board.Active.AutoDeliverBlocked, Is.True);
            Assert.That(board.Version, Is.EqualTo(version + 1), "an unchanged blocked state does not count again");
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure), "and does not refresh presenters");
            Assert.That(Stock(rig, Ore), Is.EqualTo(5));

            // Room appears (the player spent Cash): the next instant delivers.
            Assert.That(rig.State.TrySpendCash(100), Is.EqualTo(RunStateResult.Ok));
            rig.Advance(1);
            Assert.That(board.Active, Is.Null, "the retry delivered the contract");
            Assert.That(Stock(rig, Ore), Is.Zero);
            Assert.That(rig.State.Cash, Is.EqualTo(ExactBound.Value - 4 - 100 + 7));
            Assert.That(rig.State.Rp, Is.EqualTo(2));
            Assert.That(Items(rig), Is.EqualTo(1));
            Assert.That(board.CompletionCount(OreOffer), Is.EqualTo(1));
            Assert.That(board.Version, Is.GreaterThan(version + 1));
            Assert.That(RunSnapshots.Contracts(rig.Session).Active, Is.Null, "the blocked state ended with the contract");
        }

        [Test]
        public void TheBlockedStateIsReportedOnceOnAChangeAndNeverOnARepeat()
        {
            RunCommandFixtures.Rig rig = BlockedRig();
            var sim = new RunSimulator(rig.Catalog);
            TimelineMapping mapping = TimelineMapping.Live(StartUtcMs);

            Assert.That(sim.Advance(rig.State, rig.Session.Permanent, 0, mapping, null), Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(sim.LastAutoDelivered, Is.False);
            Assert.That(sim.LastAutoDeliverBlocked, Is.True);
            Assert.That(sim.LastAutoDeliverBlockedChanged, Is.True, "false to true: a state change, the session event fires");

            Assert.That(sim.Advance(rig.State, rig.Session.Permanent, Second, mapping, null), Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(sim.LastAutoDeliverBlocked, Is.True, "retried at every instant, still blocked");
            Assert.That(sim.LastAutoDeliverBlockedChanged, Is.False, "true to true: no repeat of the event");

            Assert.That(rig.State.TrySpendCash(100), Is.EqualTo(RunStateResult.Ok));
            Assert.That(sim.Advance(rig.State, rig.Session.Permanent, 0, mapping, null), Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(sim.LastAutoDelivered, Is.True);
            Assert.That(sim.LastAutoDeliverBlocked, Is.False);
        }

        [Test]
        public void ABlockedAutoDeliveryIsTheSameForOneAdvanceAndForPartitions()
        {
            RunCommandFixtures.Rig single = BlockedRig();
            single.Advance(3 * Second);
            RunCommandFixtures.Rig cut = BlockedRig();
            AdvanceCut(cut, 3 * Second, 1, 10_000, 1_000_000, 29_999_999);
            Assert.That(Digest(cut), Is.EqualTo(Digest(single)));
            Assert.That(Board(single).Active.AutoDeliverBlocked, Is.True);
        }

        // ---- SetAutoDeliver: rejections and the flag ----------------------------------------------------------------------

        [Test]
        public void SetAutoDeliverRejectsWithoutAnActiveContract()
        {
            RunCommandFixtures.Rig rig = Rig();
            string before = ContractCooldownTests.Digest(rig);
            Assert.That(rig.Commands.SetAutoDeliver(true).ReasonCode, Is.EqualTo(RunReasons.ContractNoneActive));
            Assert.That(rig.Commands.SetAutoDeliver(false).ReasonCode, Is.EqualTo(RunReasons.ContractNoneActive));
            Assert.That(ContractCooldownTests.Digest(rig), Is.EqualTo(before));
        }

        [Test]
        public void SetAutoDeliverIsCommandInvalidForAProductionObjective()
        {
            RunCommandFixtures.Rig rig = Rig();
            Accept(rig, ProduceOffer);
            string before = ContractCooldownTests.Digest(rig);

            Assert.That(rig.Commands.SetAutoDeliver(true).ReasonCode, Is.EqualTo(RunReasons.CommandInvalid));
            Assert.That(rig.Commands.SetAutoDeliver(false).ReasonCode, Is.EqualTo(RunReasons.CommandInvalid));

            Assert.That(ContractCooldownTests.Digest(rig), Is.EqualTo(before));
            ActiveContractSnapshot snapshot = RunSnapshots.Contracts(rig.Session).Active;
            Assert.That(snapshot.CanAutoDeliver, Is.False, "the card never offers the toggle for a production objective");
            Assert.That(snapshot.AutoDeliver, Is.False);
        }

        [Test]
        public void SetAutoDeliverIsRejectedWhileTheLifecycleBlocks()
        {
            RunCommandFixtures.Rig rig = Rig();
            Accept(rig, OreOffer);
            rig.Session.SetLifecycleBlocked(true);
            string before = ContractCooldownTests.Digest(rig);
            Assert.That(rig.Commands.SetAutoDeliver(true).ReasonCode, Is.EqualTo(RunReasons.LifecycleBlocked));
            Assert.That(ContractCooldownTests.Digest(rig), Is.EqualTo(before));
            rig.Session.SetLifecycleBlocked(false);
            Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
        }

        [Test]
        public void SetAutoDeliverOnTheWorstCasePreCheckRejectsWithValueLimitAndChangesNothing()
        {
            RunCommandFixtures.Rig rig = Rig(cash: 100);
            Accept(rig, OreOffer);
            Assert.That(rig.State.TryAddCash(ExactBound.Value - 103), Is.EqualTo(RunStateResult.Ok)); // Cash = 10^18 - 3: the reward of 7 does not fit
            string before = ContractCooldownTests.Digest(rig);

            RunCommandResult<bool> r = rig.Commands.SetAutoDeliver(true);

            Assert.That(r.IsSuccess, Is.False);
            Assert.That(r.ReasonCode, Is.EqualTo(RunReasons.ValueLimit));
            Assert.That(ContractCooldownTests.Digest(rig), Is.EqualTo(before));
            Assert.That(Board(rig).Active.AutoDeliver, Is.False);
            Assert.That(rig.Commands.SetAutoDeliver(false).IsSuccess, Is.True, "turning it off needs no reward check");
        }

        [Test]
        public void SetAutoDeliverTrueAfterTheDeadlineHasPassedOnTheTimelineIsContractExpired()
        {
            RunCommandFixtures.Rig rig = Rig();
            Accept(rig, OreShort);
            Assert.That(rig.Session.RebindAnchor(StartUtcMs + 20_000).IsSuccess, Is.True); // a re-bound clock: the timeline passed the deadline, no settle yet
            Assert.That(Board(rig).Active, Is.Not.Null);
            string before = ContractCooldownTests.Digest(rig);

            Assert.That(rig.Commands.SetAutoDeliver(true).ReasonCode, Is.EqualTo(RunReasons.ContractExpired));
            Assert.That(ContractCooldownTests.Digest(rig), Is.EqualTo(before));
        }

        [Test]
        public void TheSettingIsOffByDefault_CountsOnlyChanges_AndIsClearedByEveryEnding()
        {
            RunCommandFixtures.Rig rig = Rig();
            Accept(rig, FreeOffer);
            ContractBoard board = Board(rig);
            ActiveContractSnapshot snapshot = RunSnapshots.Contracts(rig.Session).Active;
            Assert.That(board.Active.AutoDeliver, Is.False);
            Assert.That(snapshot.AutoDeliver, Is.False);
            Assert.That(snapshot.CanAutoDeliver, Is.True);
            Assert.That(snapshot.AutoDeliverBlocked, Is.False);

            long version = board.Version;
            Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            Assert.That(board.Active.AutoDeliver, Is.True);
            Assert.That(board.Version, Is.EqualTo(version + 1));
            Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            Assert.That(board.Version, Is.EqualTo(version + 1), "setting the value it already has changes nothing");
            Assert.That(rig.Commands.SetAutoDeliver(false).IsSuccess, Is.True);
            Assert.That(board.Active.AutoDeliver, Is.False);
            Assert.That(board.Version, Is.EqualTo(version + 2));

            // Abandoning clears it with the contract; a re-accepted offer starts with it off.
            Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            Assert.That(rig.Commands.AbandonContract().IsSuccess, Is.True);
            Accept(rig, FreeOffer);
            Assert.That(board.Active.AutoDeliver, Is.False, "it does not carry over after an abandonment");

            // An automatic delivery clears it too.
            Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            RunSimHarness.Stock(rig.State, Ore, 5);
            rig.Advance(0);
            Assert.That(board.Active, Is.Null, "the Free contract asks 4 (an abandonment does not count) and was delivered at the settle");
            Accept(rig, FreeOffer);
            Assert.That(board.Active.AutoDeliver, Is.False, "it does not carry over after an auto-delivery");
            Assert.That(rig.State.Contracts.CompletionCount(FreeOffer), Is.EqualTo(1));
        }

        // ---- the settle pre-check of the other commands -----------------------------------------------------------------

        [Test]
        public void WhileAutoDeliverIsOnEveryCommandPreCheckCountsTheRewardOfTheDeliveryItsSettleCanMake()
        {
            // Cash = 10^18 - 485: the sale of 20 Gears (480) fits, the same sale plus the reward of 7 does not.
            RunCommandFixtures.Rig on = Rig(cash: 100);
            Accept(on, OreOffer);
            Assert.That(on.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            Assert.That(on.State.TryAddCash(ExactBound.Value - 585), Is.EqualTo(RunStateResult.Ok));
            RunSimHarness.Stock(on.State, Gear, 20);
            string before = ContractCooldownTests.Digest(on);

            Assert.That(on.Commands.SetAutoSell(Gear, true).ReasonCode, Is.EqualTo(RunReasons.ValueLimit), "the settle could also deliver and add 7");
            Assert.That(ContractCooldownTests.Digest(on), Is.EqualTo(before));

            RunCommandFixtures.Rig off = Rig(cash: 100);
            Accept(off, OreOffer);
            Assert.That(off.State.TryAddCash(ExactBound.Value - 585), Is.EqualTo(RunStateResult.Ok));
            RunSimHarness.Stock(off.State, Gear, 20);
            Assert.That(off.Commands.SetAutoSell(Gear, true).IsSuccess, Is.True, "without auto-deliver nothing can be delivered");
            Assert.That(off.State.Cash, Is.EqualTo(ExactBound.Value - 5));
        }

        [Test]
        public void TheSettleBoundOfACommandHonoursTheReserve()
        {
            // 8 ore on auto-sell with 5 reserved: only 3 are sold, so Cash 10^18 - 3 reaches exactly 10^18. Without the reserve the bound would fail.
            RunCommandFixtures.Rig rig = Rig(cash: 100);
            Accept(rig, OreOffer);
            Assert.That(rig.State.TryAddCash(ExactBound.Value - 103), Is.EqualTo(RunStateResult.Ok));
            RunSimHarness.Stock(rig.State, Ore, 8);

            RunCommandResult<bool> r = rig.Commands.SetAutoSell(Ore, true);

            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            Assert.That(rig.State.Cash, Is.EqualTo(ExactBound.Value));
            Assert.That(Stock(rig, Ore), Is.EqualTo(5));
        }

        // ---- offline ------------------------------------------------------------------------------------------------------

        [Test]
        public void AnOfflineReturnAutoDeliversAtTheMappedRealInstantAndReportsIt()
        {
            RunCommandFixtures.Rig rig = MiningRig(OreOffer, autoDeliver: true);
            var segmenter = new OfflineSegmenter(rig.Catalog);

            RunOfflineSummary summary = segmenter.Reconcile(rig.State, rig.Session.Permanent, StartUtcMs, StartUtcMs + 10_000);

            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(summary.EffectiveTicks, Is.EqualTo(8 * Second), "10 s away at 4/5");
            Assert.That(Board(rig).Active, Is.Null);
            Assert.That(summary.Contract.Kind, Is.EqualTo(RunOfflineContractKind.AutoDelivered), "delivered, not expired");
            Assert.That(summary.Contract.OfferId, Is.EqualTo(OreOffer));
            Assert.That(Stock(rig, Ore), Is.EqualTo(3), "2, 4, 6 (5 delivered at simulated 6 s), 3");
            Assert.That(Board(rig).CooldownReadyAtUtcMs(OreOffer), Is.EqualTo(StartUtcMs + 7_500 + CooldownMs), "simulated 6 s is real 7.5 s");
            Assert.That(rig.State.Cash, Is.EqualTo(1_000 + 7));
            Assert.That(summary.CashDelta, Is.EqualTo(7));
            Assert.That(summary.SalesIncome, Is.Zero, "a contract reward is not a sale");
            Assert.That(Items(rig), Is.EqualTo(1));
        }

        // ---- PF-1: the pass budget ----------------------------------------------------------------------------------------

        [Test]
        public void ASettleLoopBeyondItsPassBudgetFailsClosedWithADiagnosticAndPublishesNothing()
        {
            RunCommandFixtures.Rig rig = Rig();
            RunSimHarness.Add(rig.State, RunCommandFixtures.Miner, 0, 100);
            var sim = new RunSimulator(rig.Catalog) { PassBudgetOverride = 1 };
            string before = RunSimHarness.Fingerprint(rig.State, rig.Session.Permanent);

            RunAdvanceResult result = sim.Advance(rig.State, rig.Session.Permanent, 3 * Second, TimelineMapping.Live(StartUtcMs),
                RunSimHarness.Events(rig.State, StartUtcMs, 0, 3 * Second));

            Assert.That(result, Is.EqualTo(RunAdvanceResult.ArithmeticOverflow), "value.limit style safety");
            Assert.That(RunReasons.From(result), Is.EqualTo(RunReasons.ValueLimit));
            Assert.That(sim.LastPassBudgetExceeded, Is.True);
            Assert.That(sim.LastPassBudget, Is.EqualTo(1));
            Assert.That(sim.LastPassBudgetTicks, Is.EqualTo(2 * Second), "the first completion needs a second pass");
            Assert.That(RunSimHarness.Fingerprint(rig.State, rig.Session.Permanent), Is.EqualTo(before), "no partial commit");

            // The real budget, machines + 2, is never reached by a legal settle, with or without a delivery.
            sim.PassBudgetOverride = 0;
            result = sim.Advance(rig.State, rig.Session.Permanent, 3 * Second, TimelineMapping.Live(StartUtcMs),
                RunSimHarness.Events(rig.State, StartUtcMs, 0, 3 * Second));
            Assert.That(result, Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(sim.LastPassBudgetExceeded, Is.False);
            Assert.That(sim.LastPassBudget, Is.Zero);
            Assert.That(sim.LastMaxPassesPerTimestamp, Is.LessThanOrEqualTo(rig.State.Machines.Count + 1));
        }

        [Test]
        public void TheDeliveryPassKeepsTheSettleWithinMachinesPlusTwo()
        {
            RunCommandFixtures.Rig rig = MiningRig(OreOffer, autoDeliver: true, autoSellOre: true);
            var sim = new RunSimulator(rig.Catalog);

            Assert.That(sim.Advance(rig.State, rig.Session.Permanent, 10 * Second, TimelineMapping.Live(StartUtcMs),
                RunSimHarness.Events(rig.State, StartUtcMs, 0, 10 * Second)), Is.EqualTo(RunAdvanceResult.Ok));

            Assert.That(sim.LastAutoDelivered, Is.True);
            Assert.That(sim.LastMaxPassesPerTimestamp, Is.GreaterThanOrEqualTo(2), "the delivering timestamp: completion and delivery, then the quiet pass");
            Assert.That(sim.LastMaxPassesPerTimestamp, Is.LessThanOrEqualTo(rig.State.Machines.Count + 2));
        }

        // ---- PF-2, PF-7: the hot path allocates nothing -------------------------------------------------------------------

        [Test]
        public void AWarmedAdvanceWithABlockedAutoDeliveryAndAReserveAllocatesNothing()
        {
            // The reward of 2 RP cannot be granted (RP is one below 10^18), so every pass of every timestamp retries the delivery (an O(1) check), while
            // the reserve keeps 5 ore from the auto-sale of the surplus.
            RunCommandFixtures.Rig rig = MiningRig(OreOffer, autoDeliver: true, autoSellOre: true);
            Assert.That(rig.State.TryAddRp(ExactBound.Value - 1), Is.EqualTo(RunStateResult.Ok));
            var sim = new RunSimulator(rig.Catalog);
            TimelineMapping mapping = TimelineMapping.Live(StartUtcMs);
            for (int i = 0; i < 30; i++)
                Assert.That(sim.Advance(rig.State, rig.Session.Permanent, Second, mapping, null), Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(Board(rig).Active.AutoDeliverBlocked, Is.True);
            Assert.That(Stock(rig, Ore), Is.EqualTo(5));

            var counts = new int[6];
            RunAdvanceResult result = RunAdvanceResult.Ok;
            for (int i = 0; i < counts.Length; i++)
                counts[i] = GcAllocationCounter.Count(() => result = sim.Advance(rig.State, rig.Session.Permanent, Second, mapping, null));

            Assert.That(result, Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(counts, Is.All.Zero, "GC.Alloc samples per warmed advance");
            Assert.That(Board(rig).Active.AutoDeliverBlocked, Is.True);
        }

        // ---- the legacy overload ------------------------------------------------------------------------------------------

        [Test]
        public void TheLegacyAdvanceOverloadDeliversLikeTheMappingOverload()
        {
            RunCommandFixtures.Rig a = MiningRig(OreOffer, autoDeliver: true);
            RunCommandFixtures.Rig b = MiningRig(OreOffer, autoDeliver: true);
            var simA = new RunSimulator(a.Catalog);
            var simB = new RunSimulator(b.Catalog);

            Assert.That(simA.Advance(a.State, a.Session.Permanent, 10 * Second, StartUtcMs, RunSimHarness.Events(a.State, StartUtcMs, 0, 10 * Second)),
                Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(simB.Advance(b.State, b.Session.Permanent, 10 * Second, TimelineMapping.Live(StartUtcMs),
                RunSimHarness.Events(b.State, StartUtcMs, 0, 10 * Second)), Is.EqualTo(RunAdvanceResult.Ok));

            Assert.That(simA.LastAutoDelivered, Is.True);
            Assert.That(Digest(a), Is.EqualTo(Digest(b)));
        }

        // ---- random scenarios: one advance against random partitions ---------------------------------------------------------

        /// <summary>
        /// The seeded random factories of the determinism suites with their timed Bar contract replaced by one that has auto-deliver on (three
        /// in four seeds) and a random requirement, so a run mixes deliveries, expiries (the offer is a 5 to 45 s contract), the reserve against random
        /// auto-sell sets and, with <paramref name="unlocks"/>, unlock chains. Every advance of a partition uses the carried live mapping, as the
        /// session does; one advance and any partition must give the identical state, and every advance keeps the stock conservation
        /// (produced - consumed - sold - delivered). The full matrix with the real stamps is T328.
        /// </summary>
        private static SimScenario RandomScenario(int seed, bool unlocks)
        {
            SimScenario sc = SimScenario.Random(seed, false, 0, false, unlocks);
            var rng = new Random(seed * 17 + 1);
            long accepted = sc.StartUtcMs - rng.Next(1000);
            long duration = sc.Catalog.ContractOffers["contract.timed-bar"].DurationMs.Value;
            long required = 3 + rng.Next(30);
            bool auto = rng.Next(4) != 0;
            Assert.That(sc.State.Contracts.TrySetActive(new ActiveContract("contract.timed-bar", accepted, accepted + duration, required, 0, auto)),
                Is.EqualTo(RunStateResult.Ok));
            return sc;
        }

        private static string Run(SimScenario sc, long total, IEnumerable<long> cuts, bool checkFlow, out int delivered)
        {
            var sim = new RunSimulator(sc.Catalog);
            long at = 0;
            delivered = 0;
            foreach (long cut in cuts.Where(c => c > 0 && c < total).Distinct().OrderBy(c => c).Concat(new[] { total }))
            {
                Assert.That(TimelineMapping.TryLiveAt(sc.StartUtcMs, at, out TimelineMapping mapping), Is.True);
                long[] before = RunSimHarness.Units(sc.State);
                Assert.That(sim.Advance(sc.State, sc.Permanent, cut - at, mapping, RunSimHarness.Events(sc.State, sc.StartUtcMs, at, cut - at)),
                    Is.EqualTo(RunAdvanceResult.Ok));
                if (sim.LastAutoDelivered) delivered++;
                if (checkFlow) AssertFlow(sim, before, sc.State);
                at = cut;
            }
            ContractBoard board = sc.State.Contracts;
            return RunSimHarness.Fingerprint(sc.State, sc.Permanent) + "ready=" + board.CooldownReadyAtUtcMs("contract.timed-bar") +
                " total=" + board.RunCompletionTotal + " expired=" + board.LastExpired.HasValue +
                " auto=" + (board.Active == null ? "-" : board.Active.AutoDeliver + "/" + board.Active.AutoDeliverBlocked);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RandomScenariosGiveTheSameStateForOneAdvanceAndForRandomPartitions(bool unlocks)
        {
            const long total = 40 * Second;
            int deliveredCases = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                // The flow check indexes the stock by inventory position, which an unlock inside the advance shifts; it runs without unlocks.
                string expected = Run(RandomScenario(seed, unlocks), total, new long[0], !unlocks, out int delivered);
                if (delivered > 0) deliveredCases++;
                var rng = new Random(seed * 31);
                for (int p = 0; p < 3; p++)
                {
                    SimScenario sc = RandomScenario(seed, unlocks);
                    var cuts = new List<long>();
                    for (int i = 1 + rng.Next(8); i > 0; i--) cuts.Add(1 + (long)(rng.NextDouble() * (total - 1)));
                    foreach (BoundaryEvent e in RunSimHarness.Events(sc.State, sc.StartUtcMs, 0, total))
                    {
                        cuts.Add(e.SimTicks);
                        cuts.Add(e.SimTicks - 1);
                        cuts.Add(e.SimTicks + 1);
                    }
                    Assert.That(Run(sc, total, cuts, false, out _), Is.EqualTo(expected),
                        "seed " + seed + " unlocks " + unlocks + " partition " + p + " cuts " + string.Join(",", cuts.OrderBy(c => c)));
                }
            }
            Assert.That(deliveredCases, Is.GreaterThan(3), "the seeds must reach the delivery path");
        }

        // ---- helpers ------------------------------------------------------------------------------------------------------

        /// <summary><c>stock change = produced - consumed - sold - delivered</c> for every resource of the last advance (the flow accounting with deliveries).</summary>
        private static void AssertFlow(RunSimulator sim, long[] before, RunState after)
        {
            for (int i = 0; i < after.Inventory.Count; i++)
            {
                string id = after.Inventory.IdAt(i);
                long expected = before[i] + sim.LastProducedUnits(id) - sim.LastConsumedUnits(id) - sim.LastSoldUnits(id) - sim.LastDeliveredUnits(id);
                Assert.That(after.Inventory.UnitsAt(i), Is.EqualTo(expected), "conservation of " + id);
            }
        }
    }
}
