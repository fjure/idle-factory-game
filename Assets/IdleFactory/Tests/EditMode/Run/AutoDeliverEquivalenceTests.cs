using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// T328 (issue #393, plan.md, Event order and determinism, equivalence cases (l) to (s); research F5-R05, F5-R17, F5-R18): the partition-invariance
    /// matrix of auto-delivery and the contract endings that the simulator drives. One advance and any live partition of it must give the identical
    /// state and the identical stamps, including cuts on the exact ticks of a completion and of the deadline boundary and a sub-millisecond phase
    /// carried by the session timeline. T326 (<see cref="AutoDeliverTests"/>) pins the unit behaviour; this file is the matrix.
    ///
    /// Every expected value is an independent oracle. It is derived by hand from the fixture (or a few lines of plain integer arithmetic on ticks)
    /// and never by calling the production function under test (<c>SalePricing.TryValue</c>, <c>TimelineMapping</c>, <c>ContractRewards</c>). The one
    /// timeline rule used by the oracles is the written spec: the stamp of an absolute tick <c>t</c> after the whole-millisecond anchor is
    /// <c>anchor + floor(t / 10 000)</c> live, and <c>origin + floor(t x 5 / 4 / 10 000)</c> offline at 4/5 efficiency. The expiry boundary of a
    /// contract is <c>deadline + 1 ms</c> (owner decision 2026-10-15), so the last tick of the deadline millisecond still delivers and the first tick of
    /// the next millisecond expires first.
    ///
    /// Fixture: one Miner whose cycle is a test parameter (in ticks, so a completion can be placed on any exact tick) makes 2 Iron Ore (base price 1) at
    /// every completion; contracts ask for Iron Ore. A delivery of 5 units at ratio 1.5 pays 7 Cash (floor of 7.5), of 2 units 3 Cash (exactly 3),
    /// a production objective of 6 units at ratio 1.2 pays 7 (floor of 7.2); every contract pays 2 RP and the ladder grants one Boost at run index 0.
    ///
    /// Deferred assertion (case (p), "a large live gap equals a quit and restart"): the live-gap reclassification (<c>RecordSuspension</c>, T337,
    /// after Save V3 T335) does not exist yet. <see cref="ALargeLiveGap_IsPartitionInvariantToday_AndItsOfflineRemapEqualsTheReconcile"/> therefore pins
    /// what holds today and is the base T337 builds on, and carries a clearly marked deferred section with the assertion T337 must add.
    /// </summary>
    public sealed class AutoDeliverEquivalenceTests
    {
        private const string Ore = ContractFixtures.Ore;
        private const string Price = "research.price";
        private const string Std = "contract.std";
        private const string Timed = "contract.timed";
        private const string Produce6 = "contract.produce6";
        private const string BonusOre = "contract.bonus-ore";
        private const long Second = ContractFixtures.Second;
        private const long TicksPerMs = 10_000;
        private const long StartUtcMs = ContractFixtures.StartUtcMs;
        private const long StdCooldownMs = 30_000;
        private const long TimedCooldownMs = 20_000;
        private const long BonusWindowMs = 90_000;

        /// <summary>A cycle that is not a whole number of milliseconds: every completion lands on another sub-millisecond phase.</summary>
        private const long OddCycle = 12_345_679;

        private static ContractOfferDef[] Offers(long timedDurationMs = 1_500)
        {
            var always = new UnlockCondition("always");
            ContractRewardDef[] rp = { new ContractRewardDef("rp", new Curve(2, 1_000_000, "floor")) };
            return new[]
            {
                // Delivery of 5 at ratio 1.5: 7 Cash.
                new ContractOfferDef(Std, "plate", Ore, new Curve(5, 1_000_000, "ceil"), null, rp, always, rewardRatioPpm: 1_500_000,
                    cooldownMs: StdCooldownMs),
                // Timed delivery of 2 at ratio 1.5: 3 Cash. One Miner completion pays it.
                new ContractOfferDef(Timed, "timedBar", Ore, new Curve(2, 1_000_000, "ceil"), timedDurationMs, rp, always, rewardRatioPpm: 1_500_000,
                    cooldownMs: TimedCooldownMs),
                // Production of 6 at ratio 1.2: 7 Cash.
                new ContractOfferDef(Produce6, "plate", Ore, new Curve(6, 1_000_000, "ceil"), null, rp, always, objective: "produce",
                    rewardRatioPpm: 1_200_000, cooldownMs: StdCooldownMs),
                // A bonus offer whose trigger is the first Iron Ore.
                new ContractOfferDef(BonusOre, "plate", Ore, new Curve(3, 1_000_000, "ceil"), null,
                    new[] { new ContractRewardDef("rp", new Curve(1, 1_000_000, "floor")) }, always, offerClass: "bonus", rewardRatioPpm: 1_800_000,
                    cooldownMs: 10_000, bonus: new BonusDef(new UnlockCondition("resourceProduced", Ore), BonusWindowMs)),
            };
        }

        /// <summary>A settled rig with the Miner cycle <paramref name="cycle"/> and, with <paramref name="priceLevels"/> above 0, a sale-price Research of 1.7 per level.</summary>
        private static RunCommandFixtures.Rig NewRig(long cycle, ContractOfferDef[] offers = null, long cash = 1_000, int priceLevels = 0)
        {
            RunCommandFixtures.Options o = ContractFixtures.Options(offers ?? Offers());
            o.Durations = new[] { cycle, 20_000_000L, 40_000_000L, 50_000_000L };
            if (priceLevels > 0)
                o.Research = new[]
                {
                    new ResearchDef(Price, "Price", priceLevels, new Curve(5, 1_500_000, "ceil"), new ResearchPrerequisite[0],
                        new EffectDef("salePrice", "allResources", null, 1_700_000)),
                };
            return ContractFixtures.Rig(null, cash, o);
        }

        /// <summary>
        /// A rig that has run <paramref name="phase"/> ticks (a sub-millisecond phase or a few milliseconds) before the Miner is placed and the
        /// offer is accepted, so the contract starts at a timeline instant with that phase carried in <c>ActiveTicksSinceAnchor</c>.
        /// </summary>
        private static RunCommandFixtures.Rig Started(long cycle, long phase, string offer, bool autoDeliver, bool autoSell = false, ContractOfferDef[] offers = null,
            int priceLevels = 0)
        {
            RunCommandFixtures.Rig rig = NewRig(cycle, offers, priceLevels: priceLevels);
            if (phase > 0) rig.Advance(phase);
            RunSimHarness.Add(rig.State, RunCommandFixtures.Miner, 0, 100);
            if (autoSell) Assert.That(rig.Commands.SetAutoSell(Ore, true).IsSuccess, Is.True);
            if (offer != null) ContractFixtures.Accept(rig, offer);
            if (autoDeliver) Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            Assert.That(rig.Session.ActiveTicksSinceAnchor, Is.EqualTo(phase));
            return rig;
        }

        private static ContractBoard Board(RunCommandFixtures.Rig rig) => rig.State.Contracts;

        private static long Stock(RunCommandFixtures.Rig rig) => rig.State.Inventory.Units(Ore);

        /// <summary>The live stamp of an absolute tick after the anchor, by the written rule: whole milliseconds of the tick count.</summary>
        private static long LiveStamp(long absoluteTicks) => StartUtcMs + absoluteTicks / TicksPerMs;

        /// <summary>
        /// Walks <paramref name="rig"/> over <paramref name="total"/> ticks cut at the given offsets (relative to the start of the walk) and calls
        /// <paramref name="check"/> after every part with the ticks elapsed so far.
        /// </summary>
        private static void Walk(RunCommandFixtures.Rig rig, long total, IEnumerable<long> cuts, Action<long> check)
        {
            long at = 0;
            foreach (long cut in cuts.Where(c => c > 0 && c < total).Distinct().OrderBy(c => c).Concat(new[] { total }))
            {
                rig.Advance(cut - at);
                at = cut;
                check?.Invoke(at);
            }
        }

        /// <summary>Cuts on the given completion ticks and their neighbours (the exact tick, one before, one after) plus some round numbers.</summary>
        private static List<long> EdgeCuts(params long[] edges)
        {
            var cuts = new List<long> { 1, 9_999, 10_000, 10_001, 1_000_000, 33_333_333 };
            foreach (long e in edges)
            {
                cuts.Add(e - 1);
                cuts.Add(e);
                cuts.Add(e + 1);
            }
            return cuts;
        }

        private static List<long> RandomCuts(Random rng, long total)
        {
            var cuts = new List<long>();
            for (int i = 1 + rng.Next(10); i > 0; i--) cuts.Add(1 + (long)(rng.NextDouble() * (total - 1)));
            return cuts;
        }

        /// <summary>The partitions every matrix case runs: one advance, the edge cuts, seeded random cuts with the edges, and uneven frames.</summary>
        private static IEnumerable<KeyValuePair<string, List<long>>> Partitions(long total, int seed, params long[] edges)
        {
            yield return new KeyValuePair<string, List<long>>("single", new List<long>());
            yield return new KeyValuePair<string, List<long>>("edges", EdgeCuts(edges));
            var rng = new Random(seed);
            for (int p = 0; p < 6; p++)
            {
                List<long> cuts = RandomCuts(rng, total);
                if (rng.Next(2) == 0) cuts.AddRange(EdgeCuts(edges));
                yield return new KeyValuePair<string, List<long>>("random" + p, cuts);
            }
            var frames = new List<long>();
            for (long t = 0, step = 16_667; t < total; t += step + (frames.Count % 5 == 0 ? 1 : 0) - (frames.Count % 5 == 1 ? 1 : 0)) frames.Add(t);
            yield return new KeyValuePair<string, List<long>>("frames", frames);
        }

        // ---- (l) auto-delivery in one advance against live partitions -------------------------------------------------------

        /// <summary>
        /// The stock crosses the required 5 mid-advance at the 3rd completion (6 ore). Oracle: after k completions (k = elapsed / cycle) the stock is
        /// 2k until the contract is delivered and 2k - 5 afterwards; the delivery pays 7 Cash, 2 RP and 1 Boost once, at the stamp of the 3rd
        /// completion tick; the cooldown is that stamp + 30 s. Checked after every part of every partition, for sub-millisecond phases of the start.
        /// </summary>
        [TestCase(0L, 20_000_000L)]
        [TestCase(7_777L, OddCycle)]
        [TestCase(9_999L, 10_000_001L)]
        [TestCase(25_001L, 19_999_999L)]
        public void TheStockCrossingMidAdvance_GivesTheSameStateAndStampsForEveryLivePartition(long phase, long cycle)
        {
            long total = 8 * cycle + 5;
            long third = 3 * cycle;
            long stamp = LiveStamp(phase + third);
            string expected = null;
            foreach (KeyValuePair<string, List<long>> partition in Partitions(total, (int)(phase % 1000 + cycle % 1000), cycle, 2 * cycle, third, 4 * cycle))
            {
                RunCommandFixtures.Rig rig = Started(cycle, phase, Std, autoDeliver: true);
                string label = "phase " + phase + " cycle " + cycle + " partition " + partition.Key;
                Walk(rig, total, partition.Value, elapsed =>
                {
                    long k = elapsed / cycle;
                    ContractBoard board = Board(rig);
                    if (k < 3)
                    {
                        Assert.That(board.Active, Is.Not.Null, label + " at " + elapsed + ": not delivered before the 3rd completion");
                        Assert.That(Stock(rig), Is.EqualTo(2 * k), label + " at " + elapsed);
                        Assert.That(rig.State.Cash, Is.EqualTo(1_000), label + " at " + elapsed);
                        Assert.That(board.CompletionCount(Std), Is.Zero, label + " at " + elapsed);
                    }
                    else
                    {
                        Assert.That(board.Active, Is.Null, label + " at " + elapsed + ": delivered at the 3rd completion tick");
                        Assert.That(Stock(rig), Is.EqualTo(2 * k - 5), label + " at " + elapsed + ": exactly 5 consumed, once");
                        Assert.That(rig.State.Cash, Is.EqualTo(1_000 + 7), label + " at " + elapsed + ": floor(5 x 1.5), once");
                        Assert.That(rig.State.Rp, Is.EqualTo(2), label + " at " + elapsed);
                        Assert.That(rig.Session.Permanent.ItemCount(ContractFixtures.Boost), Is.EqualTo(1), label + " at " + elapsed);
                        Assert.That(board.CompletionCount(Std), Is.EqualTo(1), label + " at " + elapsed);
                        Assert.That(board.CooldownReadyAtUtcMs(Std), Is.EqualTo(stamp + StdCooldownMs), label + " at " + elapsed + ": stamped at the delivery tick");
                        Assert.That(board.LastOutcome.EndedUtcMs, Is.EqualTo(stamp), label + " at " + elapsed);
                        Assert.That(board.LastOutcome.Kind, Is.EqualTo(ContractOutcomeKind.AutoDelivered), label + " at " + elapsed);
                    }
                });
                string digest = ContractFixtures.Digest(rig);
                if (expected == null) expected = digest;
                else Assert.That(digest, Is.EqualTo(expected), label);
            }
        }

        // ---- (l) the deadline tick +-1 and the sub-millisecond phase: live ---------------------------------------------------

        /// <summary>
        /// A timed contract of 1,500 ms accepted <paramref name="phase"/> ticks into the run: deadline <c>A + 1,500</c>, expiry boundary
        /// <c>E = (A - anchor + 1,501) x 10 000</c> ticks after the anchor. A Miner completion on the last tick of the deadline millisecond (<c>E - 1</c>)
        /// delivers; on the first tick of the next one (<c>E</c>) the boundary event comes first and the contract expires (F5-R05).
        /// Oracle values are fixed by hand: delivered pays 3 Cash, 2 RP, 1 Boost, cooldown from the deadline stamp; expired pays nothing, keeps the 2
        /// ore and the cooldown starts at <c>deadline + 1</c>. Every partition, including cuts on <c>E - 1</c>, <c>E</c> and <c>E + 1</c>, agrees.
        /// </summary>
        [TestCase(0L, false)]
        [TestCase(0L, true)]
        [TestCase(1L, false)]
        [TestCase(1L, true)]
        [TestCase(5_000L, false)]
        [TestCase(5_000L, true)]
        [TestCase(9_999L, false)]
        [TestCase(9_999L, true)]
        [TestCase(25_001L, false)]
        [TestCase(25_001L, true)]
        public void ACompletionOnTheLastTickOfTheDeadlineMillisecondDeliversAndOnTheFirstTickAfterItExpires_LiveForEveryPartition(long phase, bool onExpiryTick)
        {
            const long durationMs = 1_500;
            long accepted = StartUtcMs + phase / TicksPerMs;
            long deadline = accepted + durationMs;
            long expiryTick = (phase / TicksPerMs + durationMs + 1) * TicksPerMs; // first tick of deadline + 1 ms, after the anchor
            long completionTick = onExpiryTick ? expiryTick : expiryTick - 1;
            long cycle = completionTick - phase; // the Miner is placed (progress 0) at the phase
            long total = expiryTick + 5_000 - phase; // past the boundary, before the second completion

            string expected = null;
            foreach (KeyValuePair<string, List<long>> partition in Partitions(total, (int)(phase + 7), completionTick - phase, expiryTick - phase,
                         completionTick - phase - 1, completionTick - phase + 1))
            {
                RunCommandFixtures.Rig rig = Started(cycle, phase, Timed, autoDeliver: true);
                Assert.That(Board(rig).Active.DeadlineUtcMs, Is.EqualTo(deadline), "the oracle deadline");
                string label = "phase " + phase + (onExpiryTick ? " completion on the expiry tick" : " completion on the last deadline tick") + " " + partition.Key;
                Walk(rig, total, partition.Value, elapsed =>
                {
                    ContractBoard board = Board(rig);
                    long absolute = phase + elapsed;
                    if (absolute < completionTick) // nothing has happened yet, in particular not at E - 1
                    {
                        Assert.That(board.Active, Is.Not.Null, label + " at " + elapsed);
                        Assert.That(Stock(rig), Is.Zero, label + " at " + elapsed);
                        Assert.That(board.LastExpired.HasValue, Is.False, label + " at " + elapsed + ": no expiry before " + expiryTick);
                    }
                });
                ContractBoard end = Board(rig);
                Assert.That(end.Active, Is.Null, label);
                if (!onExpiryTick)
                {
                    Assert.That(end.LastExpired.HasValue, Is.False, label + ": delivered on the last deadline tick, never expired as well");
                    Assert.That(end.CompletionCount(Timed), Is.EqualTo(1), label);
                    Assert.That(rig.State.Cash, Is.EqualTo(1_000 + 3), label);
                    Assert.That(rig.State.Rp, Is.EqualTo(2), label);
                    Assert.That(rig.Session.Permanent.ItemCount(ContractFixtures.Boost), Is.EqualTo(1), label);
                    Assert.That(Stock(rig), Is.Zero, label + ": the 2 ore were consumed");
                    Assert.That(end.LastOutcome.Kind, Is.EqualTo(ContractOutcomeKind.AutoDelivered), label);
                    Assert.That(end.LastOutcome.EndedUtcMs, Is.EqualTo(deadline), label + ": the completion stamp is still the deadline millisecond");
                    Assert.That(end.CooldownReadyAtUtcMs(Timed), Is.EqualTo(deadline + TimedCooldownMs), label);
                }
                else
                {
                    Assert.That(end.LastExpired.HasValue, Is.True, label + ": the boundary event precedes the completion on the same tick");
                    Assert.That(end.LastExpired.Value.OfferId, Is.EqualTo(Timed), label);
                    Assert.That(end.LastExpired.Value.DeadlineUtcMs, Is.EqualTo(deadline), label);
                    Assert.That(end.CompletionCount(Timed), Is.Zero, label);
                    Assert.That(rig.State.Cash, Is.EqualTo(1_000), label + ": no reward");
                    Assert.That(rig.State.Rp, Is.Zero, label);
                    Assert.That(rig.Session.Permanent.ItemCount(ContractFixtures.Boost), Is.Zero, label);
                    Assert.That(Stock(rig), Is.EqualTo(2), label + ": the completion happened, nothing was delivered");
                    Assert.That(end.LastOutcome.Kind, Is.EqualTo(ContractOutcomeKind.Expired), label);
                    Assert.That(end.CooldownReadyAtUtcMs(Timed), Is.EqualTo(deadline + 1 + TimedCooldownMs), label + ": the cooldown starts at deadline + 1");
                }
                string digest = ContractFixtures.Digest(rig);
                if (expected == null) expected = digest;
                else Assert.That(digest, Is.EqualTo(expected), label);
            }
        }

        /// <summary>The same boundary one tick at a time around it: the state flips exactly between <c>E - 1</c> and <c>E</c> and nowhere else.</summary>
        [TestCase(0L)]
        [TestCase(3_333L)]
        [TestCase(9_999L)]
        public void TheDeliveryAndTheExpiryFlipExactlyAtTheBoundaryTick_OneTickAtATime(long phase)
        {
            const long durationMs = 1_500;
            long expiryTick = (phase / TicksPerMs + durationMs + 1) * TicksPerMs;
            long deadline = StartUtcMs + phase / TicksPerMs + durationMs;

            // A Miner on the last deadline tick: one tick short, nothing; the tick itself delivers.
            RunCommandFixtures.Rig deliver = Started(expiryTick - 1 - phase, phase, Timed, autoDeliver: true);
            deliver.Advance(expiryTick - 2 - phase);
            Assert.That(Board(deliver).Active, Is.Not.Null, "E - 2: pending");
            deliver.Advance(1);
            Assert.That(Stock(deliver), Is.Zero);
            Assert.That(Board(deliver).Active, Is.Null, "the completion at E - 1 delivered at that tick");
            Assert.That(Board(deliver).LastExpired.HasValue, Is.False);
            Assert.That(Board(deliver).CooldownReadyAtUtcMs(Timed), Is.EqualTo(deadline + TimedCooldownMs));

            // A Miner on the first tick of the next millisecond: expired at E, and the completion is not delivered.
            RunCommandFixtures.Rig expire = Started(expiryTick - phase, phase, Timed, autoDeliver: true);
            expire.Advance(expiryTick - 1 - phase);
            Assert.That(Board(expire).Active, Is.Not.Null, "E - 1: still active (the deadline millisecond)");
            expire.Advance(1);
            Assert.That(Board(expire).Active, Is.Null);
            Assert.That(Board(expire).LastExpired.HasValue, Is.True, "E: expired before the completion could deliver");
            Assert.That(Stock(expire), Is.EqualTo(2));
            Assert.That(expire.State.Cash, Is.EqualTo(1_000));
        }

        // ---- (l) the deadline boundary: offline ------------------------------------------------------------------------------

        /// <summary>
        /// The same two boundary cases through an offline return at 4/5 efficiency. The departure is the acceptance instant; real instant
        /// <c>b</c> maps to <c>(b - departure) x 8 000</c> effective ticks, so the expiry boundary <c>deadline + 1</c> is at <c>(D + 1) x 8 000</c> and the
        /// last tick of the deadline millisecond is <c>(D + 1) x 8 000 - 1</c> (a real instant of 1.25 ticks per effective tick, floored). The Miner
        /// completion is placed on those ticks and the outcome is hand-computed. The effective time ends 500 ms of real time after the boundary.
        /// </summary>
        [TestCase(1_000L, false)]
        [TestCase(1_000L, true)]
        [TestCase(1_001L, false)]
        [TestCase(1_001L, true)]
        [TestCase(2_500L, false)]
        [TestCase(2_500L, true)]
        public void AnOfflineCompletionOnTheLastTickOfTheDeadlineMillisecondDeliversAndOnTheFirstTickAfterItExpires(long durationMs, bool onExpiryTick)
        {
            long boundary = (durationMs + 1) * 8_000;
            long cycle = onExpiryTick ? boundary : boundary - 1;
            RunCommandFixtures.Rig rig = Started(cycle, 0, Timed, autoDeliver: true, offers: Offers(durationMs));
            long departure = rig.Session.TimelineUtcMs;
            long deadline = departure + durationMs;
            Assert.That(departure, Is.EqualTo(StartUtcMs));
            var segmenter = new OfflineSegmenter(rig.Catalog);

            RunOfflineSummary summary = segmenter.Reconcile(rig.State, rig.Session.Permanent, departure, departure + durationMs + 501);

            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(summary.EffectiveTicks, Is.EqualTo((durationMs + 501) * 8_000), "4/5 of the real time");
            ContractBoard board = Board(rig);
            Assert.That(board.Active, Is.Null);
            if (!onExpiryTick)
            {
                Assert.That(summary.Contract.Kind, Is.EqualTo(RunOfflineContractKind.AutoDelivered));
                Assert.That(summary.Contract.EndedUtcMs, Is.EqualTo(deadline), "the completion on the last deadline tick is stamped with the deadline millisecond");
                Assert.That(summary.Contract.CashReward, Is.EqualTo(3));
                Assert.That(rig.State.Cash, Is.EqualTo(1_000 + 3));
                Assert.That(Stock(rig), Is.Zero);
                Assert.That(board.LastExpired.HasValue, Is.False);
                Assert.That(board.CooldownReadyAtUtcMs(Timed), Is.EqualTo(deadline + TimedCooldownMs));
            }
            else
            {
                Assert.That(summary.Contract.Kind, Is.EqualTo(RunOfflineContractKind.Expired));
                Assert.That(summary.Contract.EndedUtcMs, Is.EqualTo(deadline + 1));
                Assert.That(summary.Contract.CashReward, Is.Zero);
                Assert.That(rig.State.Cash, Is.EqualTo(1_000));
                Assert.That(Stock(rig), Is.EqualTo(2), "the completion still happened");
                Assert.That(board.LastExpired.HasValue, Is.True);
                Assert.That(board.CooldownReadyAtUtcMs(Timed), Is.EqualTo(deadline + 1 + TimedCooldownMs));
            }
        }

        // ---- (m) a production objective across partitions -------------------------------------------------------------------

        /// <summary>
        /// A production objective of 6 units: every completion makes 2, so it completes at the 3rd. Oracle after k completions: progress 2k (k &lt; 3);
        /// at k &gt;= 3 the contract is complete, paid 7 Cash (floor of 7.2), 2 RP and 1 Boost once, nothing consumed (the stock is 2k), and the
        /// cooldown is the stamp of the 3rd completion tick + 30 s. Progress is read after every part of every partition.
        /// </summary>
        [TestCase(0L, 20_000_000L)]
        [TestCase(7_777L, OddCycle)]
        [TestCase(9_999L, 10_000_001L)]
        public void AProductionObjective_CountsAndCompletesIdenticallyForEveryLivePartition(long phase, long cycle)
        {
            long total = 6 * cycle + 3;
            long stamp = LiveStamp(phase + 3 * cycle);
            string expected = null;
            foreach (KeyValuePair<string, List<long>> partition in Partitions(total, (int)(cycle % 997 + phase), cycle, 2 * cycle, 3 * cycle))
            {
                RunCommandFixtures.Rig rig = Started(cycle, phase, Produce6, autoDeliver: false);
                string label = "phase " + phase + " cycle " + cycle + " partition " + partition.Key;
                Walk(rig, total, partition.Value, elapsed =>
                {
                    long k = elapsed / cycle;
                    ContractBoard board = Board(rig);
                    Assert.That(Stock(rig), Is.EqualTo(2 * k), label + " at " + elapsed + ": production is never consumed");
                    if (k < 3)
                    {
                        Assert.That(board.Active, Is.Not.Null, label + " at " + elapsed);
                        Assert.That(board.Active.Produced, Is.EqualTo(2 * k), label + " at " + elapsed + ": produced since acceptance");
                        Assert.That(rig.State.Cash, Is.EqualTo(1_000), label + " at " + elapsed);
                    }
                    else
                    {
                        Assert.That(board.Active, Is.Null, label + " at " + elapsed);
                        Assert.That(rig.State.Cash, Is.EqualTo(1_000 + 7), label + " at " + elapsed);
                        Assert.That(rig.State.Rp, Is.EqualTo(2), label + " at " + elapsed);
                        Assert.That(rig.Session.Permanent.ItemCount(ContractFixtures.Boost), Is.EqualTo(1), label + " at " + elapsed);
                        Assert.That(board.CompletionCount(Produce6), Is.EqualTo(1), label + " at " + elapsed);
                        Assert.That(board.LastOutcome.Kind, Is.EqualTo(ContractOutcomeKind.ProduceCompleted), label + " at " + elapsed);
                        Assert.That(board.LastOutcome.EndedUtcMs, Is.EqualTo(stamp), label + " at " + elapsed);
                        Assert.That(board.CooldownReadyAtUtcMs(Produce6), Is.EqualTo(stamp + StdCooldownMs), label + " at " + elapsed);
                    }
                });
                string digest = ContractFixtures.Digest(rig);
                if (expected == null) expected = digest;
                else Assert.That(digest, Is.EqualTo(expected), label);
            }
        }

        // ---- (n) the reserve with auto-sell on the same resource -----------------------------------------------------------------

        /// <summary>
        /// Iron Ore on auto-sell while a delivery contract of 5 is active. Oracle after k completions (stock made 2k):
        /// without auto-deliver the reserve keeps min(stock, 5), so the stock is 2k up to the 3rd completion and 5 afterwards, and 2k - 5 units were
        /// sold (1 at the 3rd, then 2 per completion) at 1 Cash each; with auto-deliver the contract is delivered at the 3rd completion (+7 Cash) and
        /// the 1 ore left is sold in the same settle, then every later completion sells its 2, so 2k - 5 units were sold as well and the stock is 0.
        /// Identical for every partition, with the cuts on the completion ticks.
        /// </summary>
        [TestCase(0L, 20_000_000L, false)]
        [TestCase(0L, 20_000_000L, true)]
        [TestCase(7_777L, OddCycle, false)]
        [TestCase(7_777L, OddCycle, true)]
        [TestCase(9_999L, 10_000_001L, true)]
        public void TheReserveAndTheRemainderSale_AreTheSameForEveryLivePartition(long phase, long cycle, bool autoDeliver)
        {
            long total = 8 * cycle + 1;
            string expected = null;
            foreach (KeyValuePair<string, List<long>> partition in Partitions(total, (int)(cycle % 991 + phase), cycle, 2 * cycle, 3 * cycle, 4 * cycle))
            {
                RunCommandFixtures.Rig rig = Started(cycle, phase, Std, autoDeliver, autoSell: true);
                string label = "phase " + phase + " cycle " + cycle + " autoDeliver " + autoDeliver + " partition " + partition.Key;
                Walk(rig, total, partition.Value, elapsed =>
                {
                    long k = elapsed / cycle;
                    long sold = k < 3 ? 0 : 2 * k - 5;
                    long delivered = autoDeliver && k >= 3 ? 7 : 0;
                    long stock = autoDeliver && k >= 3 ? 0 : (k < 3 ? 2 * k : 5);
                    Assert.That(Stock(rig), Is.EqualTo(stock), label + " at " + elapsed);
                    Assert.That(rig.State.Cash, Is.EqualTo(1_000 + sold + delivered), label + " at " + elapsed);
                    Assert.That(Board(rig).Active == null, Is.EqualTo(autoDeliver && k >= 3), label + " at " + elapsed);
                    Assert.That(Board(rig).CompletionCount(Std), Is.EqualTo(autoDeliver && k >= 3 ? 1 : 0), label + " at " + elapsed);
                });
                string digest = ContractFixtures.Digest(rig);
                if (expected == null) expected = digest;
                else Assert.That(digest, Is.EqualTo(expected), label);
            }
        }

        // ---- (o) identical cooldown and bonus stamps across live partitions ----------------------------------------------------

        /// <summary>
        /// A standing delivery (auto-deliver) and a bonus offer triggered by the first Iron Ore. Oracle stamps: the bonus window ends at the stamp of the
        /// 1st completion tick + 90 s and was opened exactly once; the standing offer's cooldown ends at the stamp of the 3rd completion tick + 30 s;
        /// neither stamp moves again for any partition (cooldowns and windows are lazy stamps, not boundary events).
        /// </summary>
        [TestCase(0L, 20_000_000L)]
        [TestCase(7_777L, OddCycle)]
        [TestCase(9_999L, 10_000_001L)]
        [TestCase(25_001L, 15_000_003L)]
        public void TheCooldownAndTheBonusWindowStamps_AreIdenticalForEveryLivePartition(long phase, long cycle)
        {
            long total = 7 * cycle + 11;
            long windowEnd = LiveStamp(phase + cycle) + BonusWindowMs;
            long ready = LiveStamp(phase + 3 * cycle) + StdCooldownMs;
            string expected = null;
            foreach (KeyValuePair<string, List<long>> partition in Partitions(total, (int)(cycle % 983 + phase), cycle, 3 * cycle))
            {
                RunCommandFixtures.Rig rig = Started(cycle, phase, Std, autoDeliver: true);
                string label = "phase " + phase + " cycle " + cycle + " partition " + partition.Key;
                Assert.That(Board(rig).BonusState(BonusOre).Triggered, Is.False, label + ": no Ore yet");
                Walk(rig, total, partition.Value, elapsed =>
                {
                    ContractBoard board = Board(rig);
                    BonusOfferState bonus = board.BonusState(BonusOre);
                    Assert.That(bonus.Triggered, Is.EqualTo(elapsed >= cycle), label + " at " + elapsed + ": fires at the first Ore tick");
                    if (bonus.Triggered) Assert.That(bonus.WindowEndUtcMs, Is.EqualTo(windowEnd), label + " at " + elapsed);
                    Assert.That(bonus.Consumed, Is.False, label + " at " + elapsed);
                    Assert.That(board.CooldownReadyAtUtcMs(Std), Is.EqualTo(elapsed >= 3 * cycle ? (long?)ready : null), label + " at " + elapsed);
                });
                string digest = ContractFixtures.Digest(rig);
                if (expected == null) expected = digest;
                else Assert.That(digest, Is.EqualTo(expected), label);
            }
        }

        // ---- (p) a large live gap -----------------------------------------------------------------------------------------------

        /// <summary>
        /// Case (p), a large live gap equals a quit and restart. T337 (live gap, after Save V3 T335) does not exist yet, so the gap reclassification is
        /// not available and this test pins what holds today; it is a passing test, not a skip.
        ///
        /// Pinned today: (1) a live gap of 90 s as one advance equals every live partition of it, with auto-delivery, so the reclassification is a pure
        /// re-mapping of the same advance; (2) the quit-and-restart side (<see cref="OfflineSegmenter.Reconcile"/>) equals the direct simulator advance
        /// with the offline mapping and the same effective ticks: the delivery is stamped with the mapped real instant (the 3rd completion at effective 6 s is
        /// real 7.5 s), the stock after is 3 and the Cash 7 more.
        ///
        /// DEFERRED (T337, not asserted here): a live frame whose wall gap is above the catalog threshold <c>LiveGapThresholdMs</c> must produce exactly the
        /// state of <c>Reconcile(state, permanent, departure, departure + gap)</c> for the same state, instead of advancing at 1/1. When T337 lands,
        /// replace the manual <c>Reconcile</c> call below with the session path (<c>RecordSuspension(atUtcMs)</c>, then a frame after the gap) and keep
        /// the asserted outcome unchanged.
        /// </summary>
        [Test]
        public void ALargeLiveGap_IsPartitionInvariantToday_AndItsOfflineRemapEqualsTheReconcile()
        {
            // (1) today: the live part. 90 s with the standard 2 s Miner: 45 completions, the 3rd delivers.
            const long gap = 90 * Second;
            RunCommandFixtures.Rig single = Started(20_000_000, 0, Std, autoDeliver: true);
            Walk(single, gap, new List<long>(), null);
            string expected = ContractFixtures.Digest(single);
            Assert.That(Board(single).Active, Is.Null);
            Assert.That(Stock(single), Is.EqualTo(2 * 45 - 5));
            Assert.That(single.State.Cash, Is.EqualTo(1_000 + 7));
            Assert.That(Board(single).CooldownReadyAtUtcMs(Std), Is.EqualTo(StartUtcMs + 6_000 + StdCooldownMs));
            foreach (KeyValuePair<string, List<long>> partition in Partitions(gap, 90, 6 * Second, 7 * Second))
            {
                RunCommandFixtures.Rig rig = Started(20_000_000, 0, Std, autoDeliver: true);
                Walk(rig, gap, partition.Value, null);
                Assert.That(ContractFixtures.Digest(rig), Is.EqualTo(expected), partition.Key);
            }

            // (2) today: the restart side. 10 s away at 4/5 is 8 s of effective time: completions at effective 2, 4, 6 and 8 s.
            RunCommandFixtures.Rig quit = Started(20_000_000, 0, Std, autoDeliver: true);
            RunCommandFixtures.Rig remapped = Started(20_000_000, 0, Std, autoDeliver: true);
            var segmenter = new OfflineSegmenter(quit.Catalog);
            RunOfflineSummary summary = segmenter.Reconcile(quit.State, quit.Session.Permanent, StartUtcMs, StartUtcMs + 10_000);
            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(summary.EffectiveTicks, Is.EqualTo(8 * Second));
            var sim = new RunSimulator(remapped.Catalog);
            TimelineMapping offline = TimelineMapping.Offline(remapped.Catalog.OfflinePolicy, StartUtcMs);
            Assert.That(sim.Advance(remapped.State, remapped.Session.Permanent, 8 * Second, offline, null), Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(ContractFixtures.Digest(remapped).Replace("now=" + remapped.Session.TimelineUtcMs, "now=").Split('\n').Take(12),
                Is.EqualTo(ContractFixtures.Digest(quit).Replace("now=" + quit.Session.TimelineUtcMs, "now=").Split('\n').Take(12)),
                "the segmenter advance is the mapped simulator advance");
            Assert.That(Board(quit).Active, Is.Null);
            Assert.That(Stock(quit), Is.EqualTo(3), "2, 4, 6 (5 delivered at effective 6 s), 3");
            Assert.That(quit.State.Cash, Is.EqualTo(1_000 + 7));
            Assert.That(Board(quit).CooldownReadyAtUtcMs(Std), Is.EqualTo(StartUtcMs + 7_500 + StdCooldownMs), "effective 6 s is real 7.5 s");
            Assert.That(summary.Contract.Kind, Is.EqualTo(RunOfflineContractKind.AutoDelivered));
            Assert.That(summary.Contract.EndedUtcMs, Is.EqualTo(StartUtcMs + 7_500));
        }

        // ---- (q) sale and valuation conservation, independent oracle, non-neutral stepped scenario ---------------------------------

        /// <summary>The sale price multipliers of the stepped Research (1.7 per level, folded once per level with a floor) by hand: 1.0, 1.7, 2.89, 4.913.</summary>
        private static readonly long[] PricePpm = { 1_000_000, 1_700_000, 2_890_000, 4_913_000 };

        /// <summary>The Cash of selling 2 Iron Ore (base price 1) in one sale at each level, by hand: 2, 3.4, 5.78 and 9.826 floored.</summary>
        private static readonly long[] PerSale = { 2, 3, 5, 9 };

        /// <summary>
        /// The Miner completes 2 Iron Ore every 2 s and auto-sell sells at every completion. One completion per step: exactly one sale of exactly 2
        /// units, paying the hand-computed floor of that single sale (2, 3, 5, 9 at the four price levels), the stock 0 afterwards and the Factory Value
        /// change equal to the Cash gain. The Research level changes between the blocks of five steps (a stepped, non-neutral scenario). 20 sales in all
        /// pay exactly 2 x 5 + 3 x 5 + 5 x 5 + 9 x 5 = 95.
        /// </summary>
        [Test]
        public void EveryAutoSaleIsOneSaleAtTheSteppedPrice_WithAnExactCountAndAnExactFloorPerSale()
        {
            RunCommandFixtures.Rig rig = NewRig(20_000_000, new ContractOfferDef[0], cash: 0, priceLevels: 3);
            RunSimHarness.Add(rig.State, RunCommandFixtures.Miner, 0, 100);
            Assert.That(rig.Commands.SetAutoSell(Ore, true).IsSuccess, Is.True);
            var sim = new RunSimulator(rig.Catalog);
            long at = 0;
            long totalCash = 0;
            for (int level = 0; level < 4; level++)
            {
                Assert.That(rig.State.TrySetResearchLevel(Price, level), Is.EqualTo(RunStateResult.Ok));
                for (int sale = 1; sale <= 5; sale++)
                {
                    long cashBefore = rig.State.Cash;
                    Assert.That(FactoryValuation.TryCompute(rig.Catalog, rig.State, rig.Session.Permanent, out FactoryValueBreakdown before), Is.True);
                    Assert.That(TimelineMapping.TryLiveAt(StartUtcMs, at, out TimelineMapping mapping), Is.True);
                    Assert.That(sim.Advance(rig.State, rig.Session.Permanent, 20_000_000, mapping, null), Is.EqualTo(RunAdvanceResult.Ok));
                    at += 20_000_000;
                    string step = "level " + level + " sale " + sale;
                    Assert.That(sim.LastCompletions, Is.EqualTo(1), step);
                    Assert.That(sim.LastProducedUnits(Ore), Is.EqualTo(2), step);
                    Assert.That(sim.LastSoldUnits(Ore), Is.EqualTo(2), step + ": one sale of the 2 units made");
                    Assert.That(rig.State.Cash - cashBefore, Is.EqualTo(PerSale[level]), step + ": the floor of one sale of 2 at " + PricePpm[level] + " ppm");
                    Assert.That(Stock(rig), Is.Zero, step);
                    Assert.That(FactoryValuation.TryCompute(rig.Catalog, rig.State, rig.Session.Permanent, out FactoryValueBreakdown after), Is.True);
                    Assert.That(after.StockValue, Is.Zero, step);
                    Assert.That(after.Total - before.Total, Is.EqualTo(PerSale[level]), step + ": the value changed by exactly what the sale paid");
                    totalCash += rig.State.Cash - cashBefore;
                }
            }
            Assert.That(totalCash, Is.EqualTo(95));
            Assert.That(rig.State.Cash, Is.EqualTo(95));
        }

        /// <summary>
        /// The same four blocks in 10 s advances (5 completions per advance), then in uneven frames: each block pays 5 x the per-sale floor, not the
        /// floor of one 10-unit lump (which would be 10, 17, 28, 49), whatever the partition. Hand totals per block: 10, 15, 25, 45.
        /// </summary>
        [Test]
        public void ACoarseAdvanceAndAnyPartitionPayTheSumOfTheSingleSales_NotTheFloorOfTheLump()
        {
            long[] block = { 10, 15, 25, 45 };
            long[] lump = { 10, 17, 28, 49 };
            foreach (bool frames in new[] { false, true })
            {
                RunCommandFixtures.Rig rig = NewRig(20_000_000, new ContractOfferDef[0], cash: 0, priceLevels: 3);
                RunSimHarness.Add(rig.State, RunCommandFixtures.Miner, 0, 100);
                Assert.That(rig.Commands.SetAutoSell(Ore, true).IsSuccess, Is.True);
                long expectedCash = 0;
                for (int level = 0; level < 4; level++)
                {
                    Assert.That(rig.State.TrySetResearchLevel(Price, level), Is.EqualTo(RunStateResult.Ok));
                    long before = rig.State.Cash;
                    if (!frames) rig.Advance(10 * Second);
                    else
                        for (int i = 0; i < 100; i++) rig.Advance(Second / 10 + (i % 7 == 0 ? 3 : 0) - (i % 7 == 1 ? 3 : 0));
                    Assert.That(rig.State.Cash - before, Is.EqualTo(block[level]), "level " + level + " frames " + frames);
                    if (level > 0) Assert.That(block[level], Is.Not.EqualTo(lump[level]), "the oracle distinguishes a lump");
                    expectedCash += block[level];
                    Assert.That(Stock(rig), Is.Zero);
                }
                Assert.That(rig.State.Cash, Is.EqualTo(expectedCash));
            }
        }

        /// <summary>
        /// Valuation and a real sale use one price (FR-002, SC-003). With auto-sell off at the top price level (4.913) the stock after k completions is 2k,
        /// worth floor(2k x 4.913) = 9, 19, 29, 39, 49 by hand, and a manual sale of all 10 units pays exactly the valuation, 49 (not the 45 of
        /// five separate sales), so a sale moves value from the stock to Cash without changing the Factory Value.
        /// </summary>
        [Test]
        public void TheValuationOfTheStockEqualsWhatSellingItAllPays_AtANonNeutralPrice()
        {
            long[] stockValue = { 9, 19, 29, 39, 49 };
            RunCommandFixtures.Rig rig = NewRig(20_000_000, new ContractOfferDef[0], cash: 0, priceLevels: 3);
            RunSimHarness.Add(rig.State, RunCommandFixtures.Miner, 0, 100);
            Assert.That(rig.State.TrySetResearchLevel(Price, 3), Is.EqualTo(RunStateResult.Ok));
            for (int k = 1; k <= 5; k++)
            {
                rig.Advance(20_000_000);
                Assert.That(Stock(rig), Is.EqualTo(2 * k));
                Assert.That(FactoryValuation.TryCompute(rig.Catalog, rig.State, rig.Session.Permanent, out FactoryValueBreakdown v), Is.True);
                Assert.That(v.StockValue, Is.EqualTo(stockValue[k - 1]), "stock " + 2 * k);
                Assert.That(v.Total, Is.EqualTo(stockValue[k - 1] + 10), "Cash 0 + the Miner's 10 invested + the stock (Cash is 0: nothing sold)");
            }
            RunCommandResult<SellResult> sale = rig.Commands.SellAll(Ore);
            Assert.That(sale.IsSuccess, Is.True, sale.ReasonCode);
            Assert.That(sale.Value.Units, Is.EqualTo(10));
            Assert.That(sale.Value.CashEarned, Is.EqualTo(49));
            Assert.That(rig.State.Cash, Is.EqualTo(49));
            Assert.That(FactoryValuation.TryCompute(rig.Catalog, rig.State, rig.Session.Permanent, out FactoryValueBreakdown after), Is.True);
            Assert.That(after.StockValue, Is.Zero);
            Assert.That(after.Total, Is.EqualTo(49 + 10), "the sale left the Factory Value unchanged");
        }

        // ---- (r) SetAutoDeliver delivers at t = 0 -------------------------------------------------------------------------------

        /// <summary>
        /// <c>SetAutoDeliver</c> on a stocked contract delivers in the settle of the command, with no time passing: the cooldown is the stamp of the
        /// command instant, for a command on any tick, and a partition of the time before and after gives the same state. On a timed contract the last
        /// tick of the deadline millisecond still delivers and on the first tick of the next the advance onto that tick has already expired the contract (boundary events come first), so the command is <c>contract.none-active</c> and nothing is delivered.
        /// </summary>
        [TestCase(0L)]
        [TestCase(1L)]
        [TestCase(7_777L)]
        [TestCase(9_999L)]
        [TestCase(10_000L)]
        [TestCase(19_999_999L)]
        public void SetAutoDeliverOnAStockedContractDeliversAtTimeZero_WhateverTheTickOfTheCommand(long ticksBefore)
        {
            RunCommandFixtures.Rig a = Started(20_000_000, 0, Std, autoDeliver: false);
            RunCommandFixtures.Rig b = Started(20_000_000, 0, Std, autoDeliver: false);
            // Two ways to the same instant: one advance, or several parts. The Miner never completes before 2 s, so the stock is set by hand.
            a.Advance(ticksBefore);
            for (long done = 0; done < ticksBefore; done += 4_999) b.Advance(Math.Min(4_999, ticksBefore - done));
            foreach (RunCommandFixtures.Rig rig in new[] { a, b })
            {
                RunSimHarness.Stock(rig.State, Ore, 5);
                long ticks = rig.Session.ActiveTicksSinceAnchor;
                Assert.That(ticks, Is.EqualTo(ticksBefore));
                Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
                ContractBoard board = Board(rig);
                Assert.That(board.Active, Is.Null, "delivered in the settle of the command");
                Assert.That(Stock(rig), Is.Zero);
                Assert.That(rig.State.Cash, Is.EqualTo(1_000 + 7));
                Assert.That(rig.Session.ActiveTicksSinceAnchor, Is.EqualTo(ticks), "t = 0: no ticks elapsed");
                Assert.That(board.LastOutcome.EndedUtcMs, Is.EqualTo(LiveStamp(ticksBefore)));
                Assert.That(board.CooldownReadyAtUtcMs(Std), Is.EqualTo(LiveStamp(ticksBefore) + StdCooldownMs));
            }
            Assert.That(ContractFixtures.Digest(a), Is.EqualTo(ContractFixtures.Digest(b)));
            a.Advance(5 * Second);
            b.Advance(5 * Second);
            Assert.That(ContractFixtures.Digest(a), Is.EqualTo(ContractFixtures.Digest(b)));
        }

        [TestCase(0L, true)]
        [TestCase(0L, false)]
        [TestCase(6_543L, true)]
        [TestCase(6_543L, false)]
        public void SetAutoDeliverOnTheLastTickOfTheDeadlineMillisecondDeliversAndOnTheFirstTickAfterItIsExpired(long phase, bool onExpiryTick)
        {
            const long durationMs = 1_500;
            long deadline = StartUtcMs + phase / TicksPerMs + durationMs;
            long expiryTick = (phase / TicksPerMs + durationMs + 1) * TicksPerMs;
            long commandTick = onExpiryTick ? expiryTick : expiryTick - 1;
            RunCommandFixtures.Rig rig = Started(2_000_000_000, phase, Timed, autoDeliver: false); // a Miner that does not complete in this test
            rig.Advance(commandTick - phase - 1);
            rig.Advance(1);
            Assert.That(rig.Session.ActiveTicksSinceAnchor, Is.EqualTo(commandTick));
            RunSimHarness.Stock(rig.State, Ore, 2);

            RunCommandResult<bool> r = rig.Commands.SetAutoDeliver(true);

            ContractBoard board = Board(rig);
            if (!onExpiryTick)
            {
                Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
                Assert.That(board.Active, Is.Null);
                Assert.That(rig.State.Cash, Is.EqualTo(1_000 + 3));
                Assert.That(board.LastOutcome.EndedUtcMs, Is.EqualTo(deadline), "still the deadline millisecond");
                Assert.That(board.CooldownReadyAtUtcMs(Timed), Is.EqualTo(deadline + TimedCooldownMs));
            }
            else
            {
                Assert.That(r.IsSuccess, Is.False);
                Assert.That(r.ReasonCode, Is.EqualTo(RunReasons.ContractNoneActive), "the advance onto tick E already expired it (boundary events come first)");
                Assert.That(board.Active, Is.Null);
                Assert.That(board.LastExpired.HasValue, Is.True);
                Assert.That(board.CooldownReadyAtUtcMs(Timed), Is.EqualTo(deadline + 1 + TimedCooldownMs));
                Assert.That(rig.State.Cash, Is.EqualTo(1_000));
                Assert.That(Stock(rig), Is.EqualTo(2));
            }
        }

        // ---- (s) a block retries ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// A delivery whose reward does not fit under the 10^18 bound (<c>value.limit</c>) is blocked without any change and retried at every instant. Oracle:
        /// Cash 10^18 - 6 with 7 ore stocked, auto-sell on and the reserve keeping 5, so the 2 units over it sell for 2 (Cash 10^18 - 4) and the reward 7
        /// would pass the bound. The state is identical for every partition, with cuts on every tick of interest, and when room appears (the player
        /// spends 100) the retry at the next instant delivers, stamped with the instant of that retry (the timeline instant of the command, whatever the
        /// following partition), paying 7 and leaving the Cash at 10^18 - 4 - 100 + 7.
        /// </summary>
        [TestCase(0L, 0L)]
        [TestCase(7_777L, 33_333_333L)]
        [TestCase(9_999L, 1L)]
        public void ABlockedAutoDeliveryIsRetriedAtTheNextInstant_ForEveryPartitionAfterTheRoomAppears(long phase, long blockedTicks)
        {
            var expected = new Dictionary<long, string>();
            foreach (long after in new[] { 0L, 1L, 5_000L, 19_999_999L, 40_000_000L })
            foreach (bool frames in new[] { false, true })
            {
                RunCommandFixtures.Rig rig = NewRig(2_000_000_000); // a Miner that does not complete in this test
                if (phase > 0) rig.Advance(phase);
                ContractFixtures.Accept(rig, Std);
                Assert.That(rig.Commands.SetAutoSell(Ore, true).IsSuccess, Is.True);
                Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
                Assert.That(rig.State.TryAddCash(ExactBound.Value - 1_000 - 6), Is.EqualTo(RunStateResult.Ok));
                RunSimHarness.Stock(rig.State, Ore, 7);
                string label = "phase " + phase + " blocked " + blockedTicks + " after " + after + " frames " + frames;

                if (!frames) rig.Advance(blockedTicks); // blocked for blockedTicks ticks, in one advance or in ten parts
                else for (int i = 0; i < 10; i++) rig.Advance(blockedTicks / 10 + (i == 9 ? blockedTicks % 10 : 0));
                Assert.That(Board(rig).Active, Is.Not.Null, label);
                Assert.That(Board(rig).Active.AutoDeliverBlocked, Is.True, label);
                Assert.That(Stock(rig), Is.EqualTo(5), label + ": the reserve kept the 5");
                Assert.That(rig.State.Cash, Is.EqualTo(ExactBound.Value - 4), label + ": the 2 over the reserve sold for 2");
                Assert.That(Board(rig).CompletionCount(Std), Is.Zero, label);

                Assert.That(rig.State.TrySpendCash(100), Is.EqualTo(RunStateResult.Ok));
                long retryStamp = rig.Session.TimelineUtcMs;
                if (frames) { rig.Advance(0); for (long done = 0; done < after; done += 7_001) rig.Advance(Math.Min(7_001, after - done)); }
                else rig.Advance(after);

                Assert.That(Board(rig).Active, Is.Null, label + ": the retry delivered");
                Assert.That(Stock(rig), Is.Zero, label);
                Assert.That(rig.State.Cash, Is.EqualTo(ExactBound.Value - 4 - 100 + 7), label);
                Assert.That(rig.State.Rp, Is.EqualTo(2), label);
                Assert.That(Board(rig).CompletionCount(Std), Is.EqualTo(1), label);
                Assert.That(Board(rig).LastOutcome.EndedUtcMs, Is.EqualTo(retryStamp), label + ": delivered at the first instant after the room appeared");
                Assert.That(Board(rig).CooldownReadyAtUtcMs(Std), Is.EqualTo(retryStamp + StdCooldownMs), label);
                // The partitions of the time after the room appeared end in the same state when they cover the same ticks.
                string digest = ContractFixtures.Digest(rig); // the same total ticks for both ways to cut them: one state per value of "after"
                if (!expected.TryGetValue(after, out string first)) expected[after] = digest;
                else Assert.That(digest, Is.EqualTo(first), label);
            }
        }

        /// <summary>
        /// A block that never gets room stays a block for every partition: nothing is delivered, nothing is consumed beyond the reserve, and the
        /// partitions give one state.
        /// </summary>
        [Test]
        public void AStayingBlockIsTheSameForEveryPartition()
        {
            string expected = null;
            const long total = 5 * Second;
            foreach (KeyValuePair<string, List<long>> partition in Partitions(total, 5, Second, 2 * Second))
            {
                RunCommandFixtures.Rig rig = NewRig(OddCycle);
                rig.Advance(7_777);
                RunSimHarness.Add(rig.State, RunCommandFixtures.Miner, 0, 100);
                ContractFixtures.Accept(rig, Std);
                Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
                Assert.That(rig.State.TryAddCash(ExactBound.Value - 1_000 - 1), Is.EqualTo(RunStateResult.Ok)); // Cash 10^18 - 1: the reward 7 never fits
                Walk(rig, total, partition.Value, null);
                // 5 s with a 1.2345679 s cycle is 4 completions (8 ore, no auto-sell): the 3rd made 6 ore, the delivery was blocked at every instant.
                Assert.That(Stock(rig), Is.EqualTo(8), partition.Key);
                Assert.That(Board(rig).Active.AutoDeliverBlocked, Is.True, partition.Key);
                Assert.That(rig.State.Cash, Is.EqualTo(ExactBound.Value - 1), partition.Key);
                Assert.That(Board(rig).CompletionCount(Std), Is.Zero, partition.Key);
                string digest = ContractFixtures.Digest(rig);
                if (expected == null) expected = digest;
                else Assert.That(digest, Is.EqualTo(expected), partition.Key);
            }
        }
    }
}
