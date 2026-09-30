using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
    /// <see cref="OfflineSegmenter"/> and <see cref="RunOfflineSummary"/> (T103, research R05, FR-025, FR-026, US5
    /// scenario 1, US2 scenario 3, QA-6, contracts/run-application.md). The oracle is test-local: its own
    /// <c>E</c>, its own boundary mapping and plain <see cref="RunSimulator.Advance"/> calls, so the tests check
    /// the segmenter against the contract text and not against itself.
    /// </summary>
    public sealed class OfflineSegmentationTests
    {
        private const long D = RunSimHarness.StartUtcMs;
        private const long TicksPerMs = 10_000;
        private const long Hour = 3_600_000L;
        private const long CapMs = 8 * Hour;
        private const long CapTicks = CapMs * TicksPerMs;
        private const long EffectiveCap = 230_400_000_000L;

        private static readonly ConflictKey FactorySpeed = new ConflictKey("machineSpeed", "factory");
        private static readonly ConflictKey MinerOutput = new ConflictKey("machineOutput", "machine.miner");

        // ------------------------------------------------------------------------------------------------
        // Oracle
        // ------------------------------------------------------------------------------------------------

        /// <summary>E(x) of R05 in ticks, from the formula text: floor(min(max(x - d, 0), cap) x 4 / 5).</summary>
        private static long E(long d, long x) => Math.Min(Math.Max(x - d, 0), CapMs) * TicksPerMs * 4 / 5;

        private static List<BoundaryEvent> OracleEvents(RunState state, long d, long r)
        {
            var list = new List<BoundaryEvent>();
            void Add(BoundaryKind kind, string id, long utc)
            {
                if (utc > r) return;
                list.Add(new BoundaryEvent(utc <= d ? 0 : E(d, utc), kind, id, utc));
            }
            foreach (ActiveEffect e in state.Effects.Effects) Add(BoundaryKind.EffectExpiry, e.SourceId, e.EndUtcMs);
            if (state.Overdrive.ActiveUntilUtcMs.HasValue)
            {
                Add(BoundaryKind.OverdriveEnd, "overdrive", state.Overdrive.ActiveUntilUtcMs.Value);
                Add(BoundaryKind.OverdriveCooldownEnd, "overdrive", state.Overdrive.CooldownUntilUtcMs.Value);
            }
            if (state.Contracts.Active != null && state.Contracts.Active.DeadlineUtcMs.HasValue)
                Add(BoundaryKind.ContractExpiry, state.Contracts.Active.OfferId, state.Contracts.Active.DeadlineUtcMs.Value + 1);
            return list.OrderBy(e => e.SimTicks).ThenBy(e => e.Kind).ThenBy(e => e.Id, StringComparer.Ordinal).ThenBy(e => e.UtcMs).ToList();
        }

        /// <summary>Unsegmented: one Advance over E(r) ticks with the mapped events.</summary>
        private static string OracleSingle(RunCatalog catalog, RunState state, PermanentState permanent, long d, long r)
        {
            var sim = new RunSimulator(catalog);
            Assert.That(sim.Advance(state, permanent, E(d, r), d, OracleEvents(state, d, r)), Is.EqualTo(RunAdvanceResult.Ok));
            return RunSimHarness.Fingerprint(state, permanent);
        }

        /// <summary>Segmented: one Advance per segment, cut at every event and every <paramref name="extraCut"/> ticks.</summary>
        private static string OracleSegmented(RunCatalog catalog, RunState state, PermanentState permanent, long d, long r, long extraCut)
        {
            List<BoundaryEvent> all = OracleEvents(state, d, r);
            long total = E(d, r);
            var cuts = all.Select(e => e.SimTicks)
                .Concat(Enumerable.Range(1, (int)(total / extraCut)).Select(i => i * extraCut))
                .Where(c => c > 0 && c < total).Distinct().OrderBy(c => c).Concat(new[] { total }).ToList();
            var sim = new RunSimulator(catalog);
            long at = 0;
            long realNow = d;
            foreach (long cut in cuts)
            {
                var segment = all.Where(e => at == 0 ? e.SimTicks <= cut : e.SimTicks > at && e.SimTicks <= cut).ToList();
                var rebased = segment.Select(e => new BoundaryEvent(e.SimTicks - at, e.Kind, e.Id, e.UtcMs)).ToList();
                Assert.That(sim.Advance(state, permanent, cut - at, realNow, rebased), Is.EqualTo(RunAdvanceResult.Ok));
                foreach (BoundaryEvent e in segment) realNow = Math.Max(realNow, e.UtcMs);
                at = cut;
            }
            return RunSimHarness.Fingerprint(state, permanent);
        }

        // ------------------------------------------------------------------------------------------------
        // Builders
        // ------------------------------------------------------------------------------------------------

        /// <summary>Real offsets (ms from the departure) of each timer's boundary; null leaves the timer absent.</summary>
        private sealed class Timers
        {
            public long? BoostEnd;
            public long? DrillEnd;
            public long? OverdriveEnd;
            public long? ContractExpiry;
        }

        private sealed class Rig
        {
            public RunCatalog Catalog;
            public RunState State;
            public PermanentState Permanent;
        }

        /// <summary>
        /// A factory with Item, Overdrive and contract timers at the given offsets. With <paramref name="online"/>
        /// every offset and duration is scaled by 4/5, which is what E does to a real duration; offsets that are
        /// multiples of 5 ms then land on whole milliseconds of the online timeline.
        /// </summary>
        private static Rig Build(Timers t, bool online = false, bool contention = false, long storage = 1000)
        {
            long S(long ms) => online ? ms * 4 / 5 : ms;
            RunCatalog catalog = RunSimHarness.Catalog(new RunSimHarness.CatalogOptions
            {
                StorageCapacity = storage,
                BoostItemDurationMs = S(60_000),
                DrillItemDurationMs = S(60_000),
                OverdriveDurationMs = S(30_000),
                OverdriveCooldownMs = S(300_000),
                TimedContractDurationMs = S(180_000),
            });
            RunState s = RunSimHarness.NewState(catalog, 25);
            if (contention)
            {
                RunSimHarness.Add(s, "machine.miner", 0, 1);
                RunSimHarness.Add(s, "machine.miner", 1, 2);
                RunSimHarness.Add(s, "machine.smelter", 2, 3);
                RunSimHarness.Reorder(s, 2, 1, 3);
            }
            else
            {
                RunSimHarness.Add(s, "machine.miner", 0, 1);
                RunSimHarness.Add(s, "machine.miner", 1, 2);
                RunSimHarness.Add(s, "machine.smelter", 2, 3);
                RunSimHarness.Add(s, "machine.press", 3, 4);
                RunSimHarness.Add(s, "machine.assembler", 4, 5);
                Assert.That(s.TrySetAutoSell("iron.gear", true), Is.EqualTo(RunStateResult.Ok));
            }
            if (t.BoostEnd.HasValue)
            {
                long end = D + S(t.BoostEnd.Value);
                Assert.That(s.TryAddEffect(new ActiveEffect("item.boost", FactorySpeed, end - S(60_000), end)), Is.EqualTo(RunStateResult.Ok));
            }
            if (t.DrillEnd.HasValue)
            {
                long end = D + S(t.DrillEnd.Value);
                Assert.That(s.TryAddEffect(new ActiveEffect("item.drill", MinerOutput, end - S(60_000), end)), Is.EqualTo(RunStateResult.Ok));
            }
            if (t.OverdriveEnd.HasValue)
            {
                long active = D + S(t.OverdriveEnd.Value);
                Assert.That(s.Overdrive.TryRestore(active, active + S(300_000)), Is.EqualTo(RunStateResult.Ok));
            }
            if (t.ContractExpiry.HasValue)
            {
                long deadline = D + S(t.ContractExpiry.Value) - 1;
                Assert.That(s.Contracts.TrySetActive(new ActiveContract("contract.timed-bar", deadline - S(180_000), deadline, 40)), Is.EqualTo(RunStateResult.Ok));
            }
            return new Rig { Catalog = catalog, State = s, Permanent = new PermanentState(catalog) };
        }

        private static RunOfflineSummary Reconcile(Rig rig, long r) =>
            new OfflineSegmenter(rig.Catalog).Reconcile(rig.State, rig.Permanent, D, r);

        private static string Print(Rig rig) => RunSimHarness.Fingerprint(rig.State, rig.Permanent);

        // ------------------------------------------------------------------------------------------------
        // E
        // ------------------------------------------------------------------------------------------------

        [TestCase(0L, 0L)]
        [TestCase(1L, 8_000L)]
        [TestCase(5L, 40_000L)]
        [TestCase(Hour, 28_800_000_000L)]
        [TestCase(CapMs - 1, 230_399_992_000L)]
        [TestCase(CapMs, EffectiveCap)]
        [TestCase(CapMs + 1, EffectiveCap)]
        [TestCase(30 * Hour, EffectiveCap)]
        [TestCase(900_000_000_000_000L, EffectiveCap)]
        public void E_IsFloorOfMinMaxTimesFourFifths(long absenceMs, long expectedTicks)
        {
            RunOfflinePolicy policy = RunSimHarness.Catalog().OfflinePolicy;
            Assert.That(OfflineSegmenter.EffectiveTicks(policy, D, D + absenceMs), Is.EqualTo(expectedTicks));
            Assert.That(OfflineSegmenter.EffectiveTicks(policy, D, D + absenceMs), Is.EqualTo(E(D, D + absenceMs)));
            long credited = Math.Min(absenceMs, CapMs) * TicksPerMs;
            Assert.That(OfflineSegmenter.CreditedTicks(policy, D, D + absenceMs), Is.EqualTo(credited));
        }

        [Test]
        public void E_BeforeTheDeparture_IsZero_AndTelescopes()
        {
            RunOfflinePolicy policy = RunSimHarness.Catalog().OfflinePolicy;
            Assert.That(OfflineSegmenter.EffectiveTicks(policy, D, D - 5), Is.EqualTo(0));
            var rng = new Random(11);
            for (int trial = 0; trial < 200; trial++)
            {
                var bs = Enumerable.Range(0, 1 + rng.Next(6)).Select(_ => D + rng.Next(0, 12 * (int)Hour)).OrderBy(x => x).ToList();
                long sum = 0, previous = D;
                foreach (long b in bs)
                {
                    sum += OfflineSegmenter.EffectiveTicks(policy, D, b) - OfflineSegmenter.EffectiveTicks(policy, D, previous);
                    previous = b;
                }
                Assert.That(sum, Is.EqualTo(OfflineSegmenter.EffectiveTicks(policy, D, bs.Last())), "trial " + trial);
            }
        }

        // ------------------------------------------------------------------------------------------------
        // Absences with timers inside the credited window and after the cap
        // ------------------------------------------------------------------------------------------------

        private static IEnumerable<TestCaseData> Absences()
        {
            // name, absence, boost end, drill end, overdrive active end (cooldown end +5 min), contract expiry
            yield return new TestCaseData(1 * Hour, false, 20 * 60_000L, 25 * 60_000L, 10 * 60_000L, 30 * 60_000L).SetName("1h_timersInsideTheWindow");
            yield return new TestCaseData(8 * Hour, false, 3 * Hour, 3 * Hour + 1, 1 * Hour, 5 * Hour).SetName("8h_timersInsideTheWindow");
            yield return new TestCaseData(30 * Hour, false, 2 * Hour, 2 * Hour + 1, 3 * Hour, 4 * Hour).SetName("30h_timersInsideTheWindow");
            yield return new TestCaseData(30 * Hour, true, 9 * Hour, 12 * Hour, 10 * Hour, 20 * Hour).SetName("30h_timersAfterTheCap");
        }

        [TestCaseSource(nameof(Absences))]
        public void Absence_MapsEveryBoundary_AndEqualsTheUnsegmentedAndSegmentedAdvance(
            long absenceMs, bool afterCap, long boost, long drill, long overdrive, long contract)
        {
            var t = new Timers { BoostEnd = boost, DrillEnd = drill, OverdriveEnd = overdrive, ContractExpiry = contract };
            long r = D + absenceMs;
            long total = E(D, r);

            // The mapped events, from the formula.
            Rig probe = Build(t);
            IReadOnlyList<BoundaryEvent> events = new OfflineSegmenter(probe.Catalog).MapBoundaries(probe.State, D, r);
            var expected = new[]
            {
                new BoundaryEvent(E(D, D + boost), BoundaryKind.EffectExpiry, "item.boost", D + boost),
                new BoundaryEvent(E(D, D + drill), BoundaryKind.EffectExpiry, "item.drill", D + drill),
                new BoundaryEvent(E(D, D + overdrive), BoundaryKind.OverdriveEnd, "overdrive", D + overdrive),
                new BoundaryEvent(E(D, D + overdrive + 300_000), BoundaryKind.OverdriveCooldownEnd, "overdrive", D + overdrive + 300_000),
                new BoundaryEvent(E(D, D + contract), BoundaryKind.ContractExpiry, "contract.timed-bar", D + contract),
            }.OrderBy(e => e.SimTicks).ThenBy(e => e.Kind).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
            Assert.That(events.Count, Is.EqualTo(5));
            for (int i = 0; i < 5; i++)
            {
                Assert.That(events[i].SimTicks, Is.EqualTo(expected[i].SimTicks), "event " + i);
                Assert.That(events[i].Kind, Is.EqualTo(expected[i].Kind), "event " + i);
                Assert.That(events[i].Id, Is.EqualTo(expected[i].Id), "event " + i);
                Assert.That(events[i].UtcMs, Is.EqualTo(expected[i].UtcMs), "event " + i);
            }
            if (afterCap)
            {
                foreach (BoundaryEvent e in events)
                    Assert.That(e.SimTicks, Is.EqualTo(EffectiveCap), "a boundary beyond d + cap is processed at the end of production");
            }

            // Segmenter against the unsegmented and the segmented oracle.
            Rig segmenter = Build(t);
            RunOfflineSummary summary = Reconcile(segmenter, r);
            Rig single = Build(t);
            string unsegmented = OracleSingle(single.Catalog, single.State, single.Permanent, D, r);
            Rig cut = Build(t);
            string segmented = OracleSegmented(cut.Catalog, cut.State, cut.Permanent, D, r, 13 * RunSimHarness.TicksPerSecond);
            Assert.That(Print(segmenter), Is.EqualTo(unsegmented));
            Assert.That(segmented, Is.EqualTo(unsegmented), "E telescopes: any segmentation gives the same state");

            // Summary.
            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(summary.HasValidInterval, Is.True);
            Assert.That(summary.MeasuredMs, Is.EqualTo(absenceMs));
            Assert.That(summary.CreditedTicks, Is.EqualTo(Math.Min(absenceMs, CapMs) * TicksPerMs));
            Assert.That(summary.EffectiveTicks, Is.EqualTo(total));
            Assert.That(summary.CapReached, Is.EqualTo(absenceMs > CapMs));
            Assert.That(summary.ExpiredEffects.Select(e => e.SourceId).ToArray(), Is.EqualTo(new[] { "item.boost", "item.drill" }));
            Assert.That(summary.ExpiredEffects[0].EndUtcMs, Is.EqualTo(D + boost));
            Assert.That(summary.OverdriveActiveEnded, Is.True);
            Assert.That(summary.OverdriveCooldownEnded, Is.True);
            Assert.That(summary.Contract.Kind, Is.EqualTo(RunOfflineContractKind.Expired));
            Assert.That(summary.Contract.OfferId, Is.EqualTo("contract.timed-bar"));
            Assert.That(summary.Contract.DeadlineUtcMs, Is.EqualTo(D + contract - 1));
            Assert.That(segmenter.State.Effects.Count, Is.EqualTo(0));
            Assert.That(segmenter.State.Overdrive.ActiveUntilUtcMs, Is.Null);
            Assert.That(segmenter.State.Contracts.Active, Is.Null);
            Assert.That(summary.SalesIncome, Is.EqualTo(summary.CashDelta));
            Assert.That(segmenter.State.Validate(), Is.EqualTo(RunStateResult.Ok));
        }

        [Test]
        public void BoundariesAfterTheReturn_AreNotProcessed_AndStayInTheState()
        {
            var t = new Timers { BoostEnd = 2 * Hour, OverdriveEnd = 2 * Hour, ContractExpiry = 2 * Hour };
            Rig rig = Build(t);
            RunOfflineSummary summary = Reconcile(rig, D + Hour);
            Assert.That(summary.ExpiredEffects, Is.Empty);
            Assert.That(rig.State.Effects.Count, Is.EqualTo(1));
            Assert.That(rig.State.Overdrive.ActiveUntilUtcMs, Is.EqualTo(D + 2 * Hour));
            Assert.That(rig.State.Contracts.Active, Is.Not.Null);
            Assert.That(summary.Contract.Kind, Is.EqualTo(RunOfflineContractKind.StillActive));
            Assert.That(summary.OverdriveActiveEnded, Is.False);
            Assert.That(summary.OverdriveCooldownEnded, Is.False);
            Rig oracle = Build(t);
            Assert.That(Print(rig), Is.EqualTo(OracleSingle(oracle.Catalog, oracle.State, oracle.Permanent, D, D + Hour)));
        }

        // ------------------------------------------------------------------------------------------------
        // Production advances by E(r) in total
        // ------------------------------------------------------------------------------------------------

        [TestCase(Hour)]
        [TestCase(CapMs)]
        [TestCase(30 * Hour)]
        public void Production_AdvancesByExactlyE_OfTheReturn(long absenceMs)
        {
            RunCatalog catalog = RunSimHarness.Catalog(new RunSimHarness.CatalogOptions { StorageCapacity = 1_000_000 });
            RunState s = RunSimHarness.NewState(catalog, 25);
            MachineInstance miner = RunSimHarness.Add(s, "machine.miner", 0, 1);
            s.TrySetAutoSell("iron.ore", true);
            long e = E(D, D + absenceMs);
            const long cycle = 20_000_000;

            RunOfflineSummary summary = new OfflineSegmenter(catalog).Reconcile(s, new PermanentState(catalog), D, D + absenceMs);

            Assert.That(summary.EffectiveTicks, Is.EqualTo(e));
            Assert.That(summary.Completions, Is.EqualTo(e / cycle));
            Assert.That(miner.ProgressTicks, Is.EqualTo(e % cycle), "the sub-cycle remainder is the remaining effective time");
            Assert.That(s.Cash, Is.EqualTo(25 + 2 * (e / cycle)), "a Miner sells 2 Ore per completion at $1");
            Assert.That(summary.SalesIncome, Is.EqualTo(2 * (e / cycle)));
            Assert.That(e, Is.LessThanOrEqualTo(EffectiveCap), "at most 6.4 h of productive time");
        }

        [Test]
        public void ReturnAtTheDeparture_IsValid_WithZeroCredit_AndProcessesTheBoundariesAlreadyDue()
        {
            // An effect that ended before the departure (a load carried it over) expires at offset 0; nothing produces.
            var t = new Timers { BoostEnd = -5_000, DrillEnd = 1_000 };
            Rig rig = Build(t);
            MachineInstance miner = rig.State.Machines[0];
            long progressBefore = miner.ProgressTicks;
            long cashBefore = rig.State.Cash;

            RunOfflineSummary summary = Reconcile(rig, D);

            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(summary.MeasuredMs, Is.EqualTo(0));
            Assert.That(summary.CreditedTicks, Is.EqualTo(0));
            Assert.That(summary.EffectiveTicks, Is.EqualTo(0));
            Assert.That(summary.ExpiredEffects, Is.Empty, "the boost ended before d: processed at 0 but not claimed");
            Assert.That(rig.State.Effects.Count, Is.EqualTo(1), "the drill ends 1 s after the departure and the return is at the departure");
            Assert.That(miner.ProgressTicks, Is.EqualTo(progressBefore));
            Assert.That(rig.State.Cash, Is.EqualTo(cashBefore));
            Assert.That(summary.Completions, Is.EqualTo(0));
        }

        [Test]
        public void LoadCarriedTimersThatEndedBeforeTheDeparture_AreProcessedButNotClaimed()
        {
            // Effects and Overdrive that ended before d (a load carried them over), a contract still ahead.
            var t = new Timers { BoostEnd = -5_000, DrillEnd = 20 * 60_000, OverdriveEnd = -Hour, ContractExpiry = 30 * 60_000 };
            Rig rig = Build(t);
            Assert.That(rig.State.Overdrive.CooldownUntilUtcMs, Is.EqualTo(D - Hour + 300_000), "the cooldown also ended before d");

            RunOfflineSummary summary = Reconcile(rig, D + Hour);

            Assert.That(rig.State.Effects.Count, Is.EqualTo(0), "the state is still processed");
            Assert.That(rig.State.Overdrive.ActiveUntilUtcMs, Is.Null);
            Assert.That(summary.ExpiredEffects.Select(e => e.SourceId).ToArray(), Is.EqualTo(new[] { "item.drill" }), "item.boost ended before d");
            Assert.That(summary.OverdriveActiveEnded, Is.False);
            Assert.That(summary.OverdriveCooldownEnded, Is.False);
            Assert.That(summary.Contract.Kind, Is.EqualTo(RunOfflineContractKind.Expired));

            // Overdrive whose active phase ended before d but whose cooldown ends during the absence.
            Rig mixed = Build(new Timers { OverdriveEnd = -60_000 });
            Assert.That(mixed.State.Overdrive.CooldownUntilUtcMs, Is.EqualTo(D + 240_000));
            RunOfflineSummary m = Reconcile(mixed, D + Hour);
            Assert.That(m.OverdriveActiveEnded, Is.False);
            Assert.That(m.OverdriveCooldownEnded, Is.True);
            Assert.That(mixed.State.Overdrive.ActiveUntilUtcMs, Is.Null);

            // Same state as the oracle.
            Rig a = Build(t);
            Rig o = Build(t);
            Reconcile(a, D + Hour);
            Assert.That(Print(a), Is.EqualTo(OracleSingle(o.Catalog, o.State, o.Permanent, D, D + Hour)));
        }

        [TestCase(RunOfflineOutcome.Applied, false)]
        [TestCase(RunOfflineOutcome.SimulationRejected, false)]
        [TestCase(RunOfflineOutcome.MissingClock, true)]
        [TestCase(RunOfflineOutcome.BackwardClock, true)]
        [TestCase(RunOfflineOutcome.ClockUnrepresentable, true)]
        public void IsRetryable_ForEveryOutcome(RunOfflineOutcome outcome, bool retryable)
        {
            Rig rig = Build(new Timers());
            var segmenter = new OfflineSegmenter(rig.Catalog);
            RunOfflineSummary summary;
            switch (outcome)
            {
                case RunOfflineOutcome.Applied: summary = segmenter.Reconcile(rig.State, rig.Permanent, D, D + 1000); break;
                case RunOfflineOutcome.MissingClock: summary = segmenter.Reconcile(rig.State, rig.Permanent, D, null); break;
                case RunOfflineOutcome.BackwardClock: summary = segmenter.Reconcile(rig.State, rig.Permanent, D, D - 1); break;
                case RunOfflineOutcome.ClockUnrepresentable: summary = segmenter.Reconcile(rig.State, rig.Permanent, D, -1); break;
                default:
                    RunState foreign = RunSimHarness.NewState(RunSimHarness.Catalog(), 25);
                    summary = segmenter.Reconcile(foreign, null, D, D + 1000);
                    break;
            }
            Assert.That(summary.Outcome, Is.EqualTo(outcome));
            Assert.That(summary.IsRetryable, Is.EqualTo(retryable));
        }

        [Test]
        public void Summary_ExposesTheCapAndTheEfficiency_ForEveryOutcome()
        {
            Rig rig = Build(new Timers());
            var segmenter = new OfflineSegmenter(rig.Catalog);
            foreach (RunOfflineSummary s in new[]
            {
                segmenter.Reconcile(rig.State, rig.Permanent, D, D + 1000),
                segmenter.Reconcile(rig.State, rig.Permanent, D, null),
                segmenter.Reconcile(rig.State, rig.Permanent, D, D - 1),
            })
            {
                Assert.That(s.CapTicks, Is.EqualTo(CapTicks));
                Assert.That(s.EfficiencyNumerator, Is.EqualTo(4));
                Assert.That(s.EfficiencyDenominator, Is.EqualTo(5));
            }
        }

        [Test]
        public void ReturnAtTheCap_AndJustPastIt()
        {
            var t = new Timers { BoostEnd = CapMs, ContractExpiry = CapMs + 1 };

            Rig atCap = Build(t);
            RunOfflineSummary a = Reconcile(atCap, D + CapMs);
            Assert.That(a.EffectiveTicks, Is.EqualTo(EffectiveCap));
            Assert.That(a.CreditedTicks, Is.EqualTo(CapTicks));
            Assert.That(a.CapReached, Is.False, "an absence of exactly eight hours is fully credited");
            Assert.That(a.ExpiredEffects.Count, Is.EqualTo(1), "the effect ends at r exactly");
            Assert.That(a.Contract.Kind, Is.EqualTo(RunOfflineContractKind.StillActive), "the contract expires 1 ms after r");

            Rig past = Build(t);
            RunOfflineSummary p = Reconcile(past, D + CapMs + 1);
            Assert.That(p.EffectiveTicks, Is.EqualTo(EffectiveCap));
            Assert.That(p.CapReached, Is.True);
            Assert.That(p.Contract.Kind, Is.EqualTo(RunOfflineContractKind.Expired));

            Rig oracle = Build(t);
            Assert.That(Print(past), Is.EqualTo(OracleSingle(oracle.Catalog, oracle.State, oracle.Permanent, D, D + CapMs + 1)));
        }

        // ------------------------------------------------------------------------------------------------
        // Boundaries exactly at the edges of E
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Boundary_AtTheReturn_IsProcessed_OneMillisecondLaterIsNot()
        {
            long r = D + 90_000;
            Rig at = Build(new Timers { BoostEnd = 90_000, ContractExpiry = 90_000 });
            IReadOnlyList<BoundaryEvent> events = new OfflineSegmenter(at.Catalog).MapBoundaries(at.State, D, r);
            Assert.That(events.Select(e => e.Kind).ToArray(), Is.EqualTo(new[] { BoundaryKind.EffectExpiry, BoundaryKind.ContractExpiry }));
            Assert.That(events.All(e => e.SimTicks == E(D, r)), Is.True);
            RunOfflineSummary a = Reconcile(at, r);
            Assert.That(a.ExpiredEffects.Count, Is.EqualTo(1));
            Assert.That(a.Contract.Kind, Is.EqualTo(RunOfflineContractKind.Expired));

            Rig later = Build(new Timers { BoostEnd = 90_001, ContractExpiry = 90_001 });
            Assert.That(new OfflineSegmenter(later.Catalog).MapBoundaries(later.State, D, r), Is.Empty);
            RunOfflineSummary b = Reconcile(later, r);
            Assert.That(b.ExpiredEffects, Is.Empty);
            Assert.That(b.Contract.Kind, Is.EqualTo(RunOfflineContractKind.StillActive));
        }

        [Test]
        public void ContractDeadline_IsInclusive_AlsoOffline()
        {
            // deadline == r: a delivery at r would still succeed, so the contract has not expired (it expires at r + 1 ms).
            Rig rig = Build(new Timers { ContractExpiry = 90_001 });
            Assert.That(rig.State.Contracts.Active.DeadlineUtcMs, Is.EqualTo(D + 90_000));
            RunOfflineSummary summary = Reconcile(rig, D + 90_000);
            Assert.That(summary.Contract.Kind, Is.EqualTo(RunOfflineContractKind.StillActive));
            Assert.That(rig.State.Contracts.CheckComplete(D + 90_000), Is.EqualTo(RunStateResult.Ok), "a delivery at the deadline instant succeeds");
        }

        [Test]
        public void Boundary_AtTheDeparture_IsDueAtZero_OneMillisecondLaterIsAtE()
        {
            long r = D + Hour;
            Rig rig = Build(new Timers { BoostEnd = 0, DrillEnd = 1 });
            IReadOnlyList<BoundaryEvent> events = new OfflineSegmenter(rig.Catalog).MapBoundaries(rig.State, D, r);
            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[0].Id, Is.EqualTo("item.boost"));
            Assert.That(events[0].SimTicks, Is.EqualTo(0));
            Assert.That(events[1].Id, Is.EqualTo("item.drill"));
            Assert.That(events[1].SimTicks, Is.EqualTo(8_000), "E(1 ms) = 10 000 ticks x 4 / 5");
        }

        [Test]
        public void Boundary_AtTheCap_AndBeyond_MapToTheEndOfProduction()
        {
            long r = D + 12 * Hour;
            Rig rig = Build(new Timers { BoostEnd = CapMs - 1, DrillEnd = CapMs, OverdriveEnd = CapMs + 1, ContractExpiry = 12 * Hour });
            IReadOnlyList<BoundaryEvent> events = new OfflineSegmenter(rig.Catalog).MapBoundaries(rig.State, D, r);
            Assert.That(events.Count, Is.EqualTo(5));
            Assert.That(events.Single(e => e.Id == "item.boost").SimTicks, Is.EqualTo(EffectiveCap - 8_000));
            foreach (BoundaryEvent e in events.Where(e => e.Id != "item.boost"))
                Assert.That(e.SimTicks, Is.EqualTo(EffectiveCap), e.Kind + " " + e.Id);
        }

        // ------------------------------------------------------------------------------------------------
        // Boundaries that collapse onto one effective instant
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void CollapsedBoundaries_AreOrderedByKindThenId_WhateverTheirRealOrder()
        {
            long r = D + 30 * Hour;
            // Five boundaries at 9, 10, 11 (Overdrive end/cooldown) and 20 hours all lie beyond the cap and collapse.
            var t = new Timers { BoostEnd = 20 * Hour, DrillEnd = 9 * Hour, OverdriveEnd = 10 * Hour, ContractExpiry = 11 * Hour };
            Rig rig = Build(t);
            IReadOnlyList<BoundaryEvent> events = new OfflineSegmenter(rig.Catalog).MapBoundaries(rig.State, D, r);
            Assert.That(events.Select(e => e.SimTicks).Distinct().ToArray(), Is.EqualTo(new[] { EffectiveCap }));
            Assert.That(events.Select(e => e.Kind + ":" + e.Id).ToArray(), Is.EqualTo(new[]
            {
                "EffectExpiry:item.boost", "EffectExpiry:item.drill", "OverdriveEnd:overdrive",
                "OverdriveCooldownEnd:overdrive", "ContractExpiry:contract.timed-bar",
            }));

            // Before the departure they collapse onto 0 in the same order.
            var stale = new Timers { BoostEnd = -3 * Hour, DrillEnd = -4 * Hour, OverdriveEnd = -5 * Hour, ContractExpiry = -2 * Hour };
            Rig old = Build(stale);
            IReadOnlyList<BoundaryEvent> zero = new OfflineSegmenter(old.Catalog).MapBoundaries(old.State, D, D + 1000);
            Assert.That(zero.Select(e => e.SimTicks).Distinct().ToArray(), Is.EqualTo(new[] { 0L }));
            Assert.That(zero.Select(e => e.Kind + ":" + e.Id).ToArray(), Is.EqualTo(new[]
            {
                "EffectExpiry:item.boost", "EffectExpiry:item.drill", "OverdriveEnd:overdrive",
                "OverdriveCooldownEnd:overdrive", "ContractExpiry:contract.timed-bar",
            }));
            RunOfflineSummary summary = Reconcile(old, D + 1000);
            Assert.That(summary.ExpiredEffects, Is.Empty, "both ended before d: processed at 0, not claimed");
            Assert.That(old.State.Effects.Count, Is.EqualTo(0));
            Assert.That(summary.Contract.Kind, Is.EqualTo(RunOfflineContractKind.Expired));
            Assert.That(old.State.Overdrive.ActiveUntilUtcMs, Is.Null);
        }

        [Test]
        public void SimultaneousRealBoundaries_OfDifferentKinds_ResolveInKindOrder()
        {
            long b = 100_000;
            Rig rig = Build(new Timers { BoostEnd = b, DrillEnd = b, OverdriveEnd = b, ContractExpiry = b });
            IReadOnlyList<BoundaryEvent> events = new OfflineSegmenter(rig.Catalog).MapBoundaries(rig.State, D, D + Hour);
            // One instant: effects, Overdrive end, contract (kind order); the cooldown end lies 5 minutes later.
            Assert.That(events.Select(e => e.Kind).ToArray(), Is.EqualTo(new[]
            {
                BoundaryKind.EffectExpiry, BoundaryKind.EffectExpiry, BoundaryKind.OverdriveEnd, BoundaryKind.ContractExpiry,
                BoundaryKind.OverdriveCooldownEnd,
            }));
            for (int i = 1; i < events.Count; i++)
                Assert.That(BoundaryEvent.Compare(events[i - 1], events[i]), Is.LessThanOrEqualTo(0), "the list is in (SimTicks, Kind, Id) order");
        }

        // ------------------------------------------------------------------------------------------------
        // Invalid clocks
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void BackwardClock_GrantsZeroCredit_AndChangesNothing()
        {
            var t = new Timers { BoostEnd = 10 * 60_000, OverdriveEnd = 5 * 60_000, ContractExpiry = 20 * 60_000 };
            Rig rig = Build(t);
            string before = Print(rig);

            RunOfflineSummary summary = Reconcile(rig, D - 1);

            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.BackwardClock));
            Assert.That(summary.HasValidInterval, Is.False);
            Assert.That(summary.MeasuredMs, Is.EqualTo(0));
            Assert.That(summary.CreditedTicks, Is.EqualTo(0));
            Assert.That(summary.EffectiveTicks, Is.EqualTo(0));
            Assert.That(summary.Completions, Is.EqualTo(0));
            Assert.That(summary.StockDeltas, Is.Empty);
            Assert.That(Print(rig), Is.EqualTo(before), "no production, no expiry");
        }

        [Test]
        public void BackwardClock_AfterAnExpiredReturn_NeverUnExpiresAnything()
        {
            var t = new Timers { BoostEnd = 10 * 60_000, OverdriveEnd = 5 * 60_000, ContractExpiry = 20 * 60_000 };
            Rig rig = Build(t);
            Assert.That(Reconcile(rig, D + Hour).Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(rig.State.Effects.Count, Is.EqualTo(0));
            Assert.That(rig.State.Contracts.Active, Is.Null);
            string after = Print(rig);

            // The next departure is the previous return (nondecreasing); a clock that then reads earlier is backward.
            RunOfflineSummary summary = new OfflineSegmenter(rig.Catalog).Reconcile(rig.State, rig.Permanent, D + Hour, D + 10 * 60_000);

            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.BackwardClock));
            Assert.That(Print(rig), Is.EqualTo(after));
            Assert.That(rig.State.Effects.Count, Is.EqualTo(0));
            Assert.That(rig.State.Contracts.Active, Is.Null);
            Assert.That(rig.State.Overdrive.ActiveUntilUtcMs, Is.Null);
        }

        [Test]
        public void MissingClock_GrantsZeroCredit_AndChangesNothing()
        {
            var t = new Timers { BoostEnd = 10 * 60_000, OverdriveEnd = 5 * 60_000, ContractExpiry = 20 * 60_000 };
            Rig rig = Build(t);
            string before = Print(rig);

            RunOfflineSummary summary = new OfflineSegmenter(rig.Catalog).Reconcile(rig.State, rig.Permanent, D, null);

            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.MissingClock));
            Assert.That(summary.ReturnUtcMs, Is.Null);
            Assert.That(summary.CreditedTicks, Is.EqualTo(0));
            Assert.That(summary.EffectiveTicks, Is.EqualTo(0));
            Assert.That(Print(rig), Is.EqualTo(before));
        }

        [TestCase(-1L)]
        [TestCase(long.MaxValue)]
        public void UnrepresentableClock_GrantsZeroCredit_AndChangesNothing(long returnUtcMs)
        {
            Rig rig = Build(new Timers { BoostEnd = 10 * 60_000 });
            string before = Print(rig);
            RunOfflineSummary summary = Reconcile(rig, returnUtcMs);
            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.ClockUnrepresentable));
            Assert.That(Print(rig), Is.EqualTo(before));
        }

        // ------------------------------------------------------------------------------------------------
        // Segmented equals online over the same effective ticks
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Offline_EqualsTheOnlineSession_OverTheSameEffectiveTicks()
        {
            // Offsets in multiples of 5 ms: E maps them to whole milliseconds (x 4 / 5), so an online run whose
            // timers already sit at the mapped instants crosses exactly the same boundaries at the same ticks.
            var t = new Timers { BoostEnd = 20 * 60_000, DrillEnd = 25 * 60_000, OverdriveEnd = 10 * 60_000, ContractExpiry = 30 * 60_000 };
            long absence = Hour;
            Rig offline = Build(t);
            Reconcile(offline, D + absence);

            Rig online = Build(t, online: true);
            var session = new RunSession(online.Catalog, online.State, online.Permanent, D);
            RunCommandResult<long> advanced = session.Advance(E(D, D + absence));
            Assert.That(advanced.IsSuccess, Is.True, advanced.ReasonCode);
            Assert.That(session.TimelineUtcMs, Is.EqualTo(D + absence * 4 / 5));

            Assert.That(Print(offline), Is.EqualTo(Print(online)));
            Assert.That(offline.State.Effects.Count, Is.EqualTo(0));
            Assert.That(offline.State.Contracts.Active, Is.Null);
        }

        [Test]
        public void Offline_CapacityContention_EqualsOnline_QA6()
        {
            // Two Miners and a Smelter compete for a five-unit store, in priority order Miner 2, Miner 1, Smelter.
            var t = new Timers { BoostEnd = 20 * 60_000, OverdriveEnd = 10 * 60_000 };
            Rig offline = Build(t, contention: true, storage: 5);
            RunOfflineSummary summary = Reconcile(offline, D + Hour);

            // Online through the session over the same effective ticks, with the timers at the mapped instants.
            Rig online = Build(t, online: true, contention: true, storage: 5);
            var session = new RunSession(online.Catalog, online.State, online.Permanent, D);
            Assert.That(session.Advance(E(D, D + Hour)).IsSuccess, Is.True);
            Assert.That(Print(offline), Is.EqualTo(Print(online)));

            // And plain, without any timer: one Advance over E(r).
            Rig a = Build(new Timers(), contention: true, storage: 5);
            Rig b = Build(new Timers(), contention: true, storage: 5);
            new OfflineSegmenter(a.Catalog).Reconcile(a.State, a.Permanent, D, D + 30 * Hour);
            Assert.That(new RunSimulator(b.Catalog).Advance(b.State, b.Permanent, EffectiveCap, D, null), Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(Print(a), Is.EqualTo(Print(b)));
            Assert.That(a.State.Inventory.Total, Is.LessThanOrEqualTo(5));
            Assert.That(summary.PartialBatchCompletions, Is.GreaterThanOrEqualTo(0));
        }

        private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

        [Test]
        public void RandomFactories_SegmenterEqualsUnsegmentedAndSegmentedOracle()
        {
            foreach (int seed in Seeds)
            {
                foreach (bool unlocks in new[] { false, true })
                {
                    long r = D + 60_000;
                    SimScenario s1 = SimScenario.Random(seed, false, 0, false, unlocks);
                    RunOfflineSummary summary = new OfflineSegmenter(s1.Catalog).Reconcile(s1.State, s1.Permanent, D, r);
                    SimScenario s2 = SimScenario.Random(seed, false, 0, false, unlocks);
                    string single = OracleSingle(s2.Catalog, s2.State, s2.Permanent, D, r);
                    SimScenario s3 = SimScenario.Random(seed, false, 0, false, unlocks);
                    string cut = OracleSegmented(s3.Catalog, s3.State, s3.Permanent, D, r, 3 * RunSimHarness.TicksPerSecond);
                    string msg = "seed " + seed + " unlocks " + unlocks;
                    Assert.That(RunSimHarness.Fingerprint(s1.State, s1.Permanent), Is.EqualTo(single), msg);
                    Assert.That(cut, Is.EqualTo(single), msg);
                    Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied), msg);
                }
            }
        }

        [Test]
        public void RandomFactories_TwelveHourAbsence_SegmenterEqualsOracle_AndTheSummaryConservesStock()
        {
            foreach (int seed in new[] { 1, 2, 3, 4, 5, 6 })
            {
                long r = D + 12 * Hour;
                SimScenario s1 = SimScenario.Random(seed, false, 0, false);
                long[] before = RunSimHarness.Units(s1.State);
                long cashBefore = s1.State.Cash;
                RunOfflineSummary summary = new OfflineSegmenter(s1.Catalog).Reconcile(s1.State, s1.Permanent, D, r);
                SimScenario s2 = SimScenario.Random(seed, false, 0, false);
                string single = OracleSingle(s2.Catalog, s2.State, s2.Permanent, D, r);
                string msg = "seed " + seed;
                Assert.That(RunSimHarness.Fingerprint(s1.State, s1.Permanent), Is.EqualTo(single), msg);
                Assert.That(summary.CapReached, Is.True, msg);
                Assert.That(summary.EffectiveTicks, Is.EqualTo(EffectiveCap), msg);
                Assert.That(summary.CashDelta, Is.EqualTo(s1.State.Cash - cashBefore), msg);
                Assert.That(summary.SalesIncome, Is.EqualTo(summary.CashDelta), msg);

                // stock change = produced - consumed - sold, per resource, from the summary alone.
                for (int i = 0; i < s1.State.Inventory.Count; i++)
                {
                    string id = s1.State.Inventory.IdAt(i);
                    long p = summary.ProducedUnits.Where(x => x.ResourceId == id).Sum(x => x.Units);
                    long c = summary.ConsumedUnits.Where(x => x.ResourceId == id).Sum(x => x.Units);
                    long sold = summary.SoldUnits.Where(x => x.ResourceId == id).Sum(x => x.Units);
                    Assert.That(s1.State.Inventory.UnitsAt(i) - before[i], Is.EqualTo(p - c - sold), msg + " " + id);
                    long delta = summary.StockDeltas.Where(x => x.ResourceId == id).Sum(x => x.Delta);
                    Assert.That(delta, Is.EqualTo(s1.State.Inventory.UnitsAt(i) - before[i]), msg + " " + id);
                }
            }
        }

        // ------------------------------------------------------------------------------------------------
        // Summary facts
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Summary_ReportsSalesStockAndLimitingConditions()
        {
            // A Miner alone into a 10-unit store without auto-sell fills the store: Storage full, partial batch not needed.
            RunCatalog catalog = RunSimHarness.Catalog(new RunSimHarness.CatalogOptions { StorageCapacity = 10 });
            RunState full = RunSimHarness.NewState(catalog, 25);
            RunSimHarness.Add(full, "machine.miner", 0, 1);
            RunSimHarness.Add(full, "machine.smelter", 1, 2);
            RunSimHarness.Add(full, "machine.press", 2, 3);
            RunOfflineSummary summary = new OfflineSegmenter(catalog).Reconcile(full, new PermanentState(catalog), D, D + Hour);

            Assert.That(summary.CashDelta, Is.EqualTo(0));
            Assert.That(summary.SalesIncome, Is.EqualTo(0));
            Assert.That(full.Inventory.Free, Is.LessThan(2), "the Miner (net +2) no longer fits");
            Assert.That(summary.StockDeltas.Sum(x => x.Delta), Is.GreaterThan(0));
            Assert.That(summary.BlockedMachinesAtReturn.Single(b => b.MachineId == "machine.miner").Blocker, Is.EqualTo(MachineBlocker.StorageFull));
            Assert.That(summary.BlockedMachinesAtReturn.Any(b => b.MachineId == "machine.press" && b.Blocker == MachineBlocker.MissingInput), Is.True);

            // With auto-sell the income is the sold units at the base price.
            RunState sells = RunSimHarness.NewState(catalog, 25);
            RunSimHarness.Add(sells, "machine.miner", 0, 1);
            sells.TrySetAutoSell("iron.ore", true);
            RunOfflineSummary income = new OfflineSegmenter(catalog).Reconcile(sells, new PermanentState(catalog), D, D + Hour);
            long soldUnits = income.SoldUnits.Single(x => x.ResourceId == "iron.ore").Units;
            Assert.That(soldUnits, Is.GreaterThan(0));
            Assert.That(income.SalesIncome, Is.EqualTo(soldUnits), "Ore sells at $1");
            Assert.That(sells.Cash, Is.EqualTo(25 + soldUnits));
            Assert.That(income.CashBefore, Is.EqualTo(25));
            Assert.That(income.CashAfter, Is.EqualTo(sells.Cash));
            Assert.That(income.BlockedMachinesAtReturn, Is.Empty);
        }

        [Test]
        public void Summary_ReportsNewUnlocksFromProductionDuringTheAbsence()
        {
            RunCatalog catalog = RunSimHarness.Catalog(new RunSimHarness.CatalogOptions
            {
                Unlocks = new Dictionary<string, UnlockCondition>
                {
                    ["iron.bar"] = new UnlockCondition("resourceProduced", "iron.ore"),
                },
            });
            RunState s = RunSimHarness.NewStateWith(catalog, new[] { "iron.ore", "machine.miner", "machine.smelter" }, 25);
            RunSimHarness.Add(s, "machine.miner", 0, 1);
            RunSimHarness.Add(s, "machine.smelter", 1, 2);

            RunOfflineSummary summary = new OfflineSegmenter(catalog).Reconcile(s, new PermanentState(catalog), D, D + Hour);

            Assert.That(summary.NewUnlocks, Does.Contain("iron.bar"));
            Assert.That(s.Inventory.IsAllowed("iron.bar"), Is.True);
            Assert.That(summary.StockDeltas.Any(x => x.ResourceId == "iron.bar" && x.Before == 0), Is.True, "a resource allowed by the return starts from 0");
        }

        [Test]
        public void Rejected_Advance_ChangesNothing_AndIsReported()
        {
            // A state of another catalog instance is rejected by the simulator as an invalid argument.
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState s = RunSimHarness.NewState(RunSimHarness.Catalog(), 25);
            RunSimHarness.Add(s, "machine.miner", 0, 1);
            string before = RunSimHarness.Fingerprint(s);
            RunOfflineSummary summary = new OfflineSegmenter(catalog).Reconcile(s, null, D, D + Hour);
            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.SimulationRejected));
            Assert.That(summary.SimulationResult, Is.EqualTo(RunAdvanceResult.InvalidArgument));
            Assert.That(summary.EffectiveTicks, Is.EqualTo(0));
            Assert.That(RunSimHarness.Fingerprint(s), Is.EqualTo(before));
        }

        // ------------------------------------------------------------------------------------------------
        // Worst case, measure only
        // ------------------------------------------------------------------------------------------------

        [TestCase(W003Variant.Bound)]
        [TestCase(W003Variant.Reference)]
        public void EightHourWorstCase_TimingRecord_IsMeasureOnly(W003Variant variant)
        {
            // Warm the JIT with a short return of another instance.
            W003Scenario warm = W003Workload.Build(variant);
            new OfflineSegmenter(warm.Catalog).Reconcile(warm.State, warm.Permanent, warm.StartUtcMs, warm.StartUtcMs + 60_000);

            W003Scenario sc = W003Workload.Build(variant);
            var segmenter = new OfflineSegmenter(sc.Catalog);
            long start = Stopwatch.GetTimestamp();
            RunOfflineSummary summary = segmenter.Reconcile(sc.State, sc.Permanent, sc.StartUtcMs, sc.StartUtcMs + CapMs);
            double elapsedMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;

            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(summary.EffectiveTicks, Is.EqualTo(EffectiveCap));
            Assert.That(summary.CreditedTicks, Is.EqualTo(CapTicks));
            Assert.That(sc.State.Validate(), Is.EqualTo(RunStateResult.Ok));
            if (variant == W003Variant.Bound)
                Assert.That(summary.Completions, Is.EqualTo(12L * 230_400L), "12 machines x E(8 h) / minCycleTicks");
            TestContext.Out.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "OFFLINE-8H-TIMING variant={0} absence=8h effective=6.4h completions={1} partialBatches={2} timestamps={3} elapsedMs={4:F1} nsPerCompletion={5:F0} cashDelta={6} runtime=editor-mono measure-only",
                sc.Name, summary.Completions, summary.PartialBatchCompletions, summary.ProcessedTimestamps, elapsedMs,
                elapsedMs * 1_000_000.0 / Math.Max(1, summary.Completions), summary.CashDelta));
        }

        [Test]
        public void EightHourWorstCase_WithBoundariesInsideTheWindow_IsMeasureOnly()
        {
            // The W003-B factory with an Item, Overdrive and a contract ending inside the window: every boundary
            // splits the advance, and the state equals the unsegmented oracle.
            W003Scenario sc = W003Workload.Build(W003Variant.Bound);
            sc.State.Effects.TryRemove(new ConflictKey("machineSpeed", "factory"));
            long end = sc.StartUtcMs + 3 * Hour;
            Assert.That(sc.State.TryAddEffect(new ActiveEffect("item.boost", new ConflictKey("machineSpeed", "factory"), end - 10L * Hour, end)), Is.EqualTo(RunStateResult.Ok));
            long r = sc.StartUtcMs + CapMs;

            W003Scenario oracle = W003Workload.Build(W003Variant.Bound);
            oracle.State.Effects.TryRemove(new ConflictKey("machineSpeed", "factory"));
            Assert.That(oracle.State.TryAddEffect(new ActiveEffect("item.boost", new ConflictKey("machineSpeed", "factory"), end - 10L * Hour, end)), Is.EqualTo(RunStateResult.Ok));

            long start = Stopwatch.GetTimestamp();
            RunOfflineSummary summary = new OfflineSegmenter(sc.Catalog).Reconcile(sc.State, sc.Permanent, sc.StartUtcMs, r);
            double elapsedMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            string expected = OracleSingle(oracle.Catalog, oracle.State, oracle.Permanent, sc.StartUtcMs, r);

            Assert.That(RunSimHarness.Fingerprint(sc.State, sc.Permanent), Is.EqualTo(expected));
            Assert.That(summary.ExpiredEffects.Count, Is.EqualTo(1));
            TestContext.Out.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "OFFLINE-8H-TIMING variant=W003-B+boundary completions={0} timestamps={1} elapsedMs={2:F1} runtime=editor-mono measure-only",
                summary.Completions, summary.ProcessedTimestamps, elapsedMs));
        }
    }
}
