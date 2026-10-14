using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// T325 (F5-R05, AR-1, AR2-1, PF-6): <see cref="TimelineMapping"/> is the one tick-to-UTC rule. These tests pin the
    /// single floor rule against a BigInteger oracle, the carry of the sub-millisecond phase (any live partition gives the
    /// same stamps), the offline mapping as the exact inverse of <c>E</c> inside the cap, the live stamps of
    /// <see cref="RunSession"/> and the bit-identity of the legacy and the mapping overload of
    /// <see cref="RunSimulator.Advance(RunState, PermanentState, long, TimelineMapping, IReadOnlyList{BoundaryEvent})"/>
    /// on the W003 workloads (the committed W003 fixtures and their SHA-256 hashes stay green in <c>SaveV2FixtureTests</c>).
    /// The mapping is a value type that no completion touches: it is built per advance and read per stamp (PF-6).
    /// </summary>
    public sealed class TimelineMappingTests
    {
        private const long TicksPerMs = 10_000;
        private const long Hour = 3_600_000L;
        private const long CapMs = 8 * Hour;
        private const long Start = RunSimHarness.StartUtcMs;
        private static readonly BigInteger Bound = new BigInteger(ExactBound.Value);

        /// <summary>One floor over the exact rational: floor((phase x den + t x num) / (den x 10 000)) added to the origin.</summary>
        private static BigInteger Oracle(long origin, long phase, long num, long den, long t) =>
            origin + BigInteger.Divide(new BigInteger(phase) * den + new BigInteger(t) * num, new BigInteger(den) * TicksPerMs);

        // ------------------------------------------------------------------------------------------------
        // The floor rule
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void LiveMappingStampsWholeMillisecondsAndCarriesThePhase()
        {
            TimelineMapping live = TimelineMapping.Live(Start);
            Assert.That(live.RealUtcMs(0), Is.EqualTo(Start));
            Assert.That(live.RealUtcMs(9_999), Is.EqualTo(Start));
            Assert.That(live.RealUtcMs(10_000), Is.EqualTo(Start + 1));
            Assert.That(live.RealUtcMs(25_000), Is.EqualTo(Start + 2));

            TimelineMapping phased = TimelineMapping.Live(Start, 9_999);
            Assert.That(phased.RealUtcMs(0), Is.EqualTo(Start));
            Assert.That(phased.RealUtcMs(1), Is.EqualTo(Start + 1), "one more tick crosses the millisecond");
            Assert.That(phased.RealUtcMs(10_001), Is.EqualTo(Start + 2));
        }

        [Test]
        public void OfflineRatioFloorsOnceOverTheExactRational()
        {
            // 5/4: 3 ticks are 3.75 real ticks, 8 ticks are 10; a millisecond needs 8 000 simulated ticks.
            var offline = new TimelineMapping(Start, 0, 5, 4);
            Assert.That(offline.RealUtcMs(7_999), Is.EqualTo(Start), "9 998.75 real ticks");
            Assert.That(offline.RealUtcMs(8_000), Is.EqualTo(Start + 1), "exactly 10 000 real ticks");
            Assert.That(offline.RealUtcMs(15_999), Is.EqualTo(Start + 1), "19 998.75 real ticks");
            Assert.That(offline.RealUtcMs(16_000), Is.EqualTo(Start + 2));

            // With a phase, the fraction of the ratio and the phase floor together, not one after the other.
            var phased = new TimelineMapping(Start, 9_998, 5, 4);
            Assert.That(phased.RealUtcMs(1), Is.EqualTo(Start), "9 998 + 1.25");
            Assert.That(phased.RealUtcMs(2), Is.EqualTo(Start + 1), "9 998 + 2.5");
        }

        [Test]
        public void StampsMatchTheBigIntegerOracleOnSeededOperands()
        {
            var rng = new Random(325);
            long[][] ratios = { new long[] { 1, 1 }, new long[] { 5, 4 }, new long[] { 3, 7 }, new long[] { 7, 3 }, new long[] { 10, 10 }, new long[] { 1, 1_000_003 }, new long[] { 999_983, 1 } };
            int representable = 0, rejected = 0;
            for (int i = 0; i < 20_000; i++)
            {
                long[] ratio = ratios[rng.Next(ratios.Length)];
                long origin = rng.Next(3) == 0 ? ExactBound.Value - rng.Next(0, 1_000_000) : Start + rng.Next(0, 1_000_000);
                long phase = rng.Next(0, (int)TicksPerMs);
                long t;
                switch (rng.Next(4))
                {
                    case 0: t = rng.Next(0, 100_000); break;
                    case 1: t = ((long)rng.Next() << 20) + rng.Next(); break;
                    case 2: t = 230_400_000_000L + rng.Next(-5, 5); break;
                    default: t = ExactBound.Value - rng.Next(0, 1000); break;
                }

                var mapping = new TimelineMapping(origin, phase, ratio[0], ratio[1]);
                BigInteger expected = Oracle(origin, phase, ratio[0], ratio[1], t);
                // The scaled tick count must stay a quantity as well (it is added to the phase before the division).
                BigInteger scaled = BigInteger.Divide(new BigInteger(t) * ratio[0], new BigInteger(ratio[1]));
                bool fits = expected <= Bound && scaled + phase <= Bound;
                bool ok = mapping.TryRealUtcMs(t, out long stamp);
                Assert.That(ok, Is.EqualTo(fits), $"origin={origin} phase={phase} {ratio[0]}/{ratio[1]} t={t}");
                if (fits)
                {
                    Assert.That(new BigInteger(stamp), Is.EqualTo(expected), $"origin={origin} phase={phase} {ratio[0]}/{ratio[1]} t={t}");
                    representable++;
                }
                else
                {
                    Assert.That(stamp, Is.Zero, "a rejected stamp leaves 0");
                    rejected++;
                }
            }
            Assert.That(representable, Is.GreaterThan(5_000));
            Assert.That(rejected, Is.GreaterThan(100), "the operand set reaches the bound");
        }

        [Test]
        public void RejectionsAndValidation()
        {
            Assert.That(default(TimelineMapping).IsValid, Is.False);
            Assert.That(default(TimelineMapping).TryRealUtcMs(0, out long zero), Is.False);
            Assert.That(zero, Is.Zero);
            Assert.That(TimelineMapping.Live(Start).IsValid, Is.True);
            Assert.That(TimelineMapping.Live(Start).TryRealUtcMs(-1, out _), Is.False, "negative ticks");
            Assert.That(TimelineMapping.Live(ExactBound.Value).TryRealUtcMs(TicksPerMs, out _), Is.False, "origin plus one ms leaves the bound");
            Assert.That(TimelineMapping.Live(ExactBound.Value).TryRealUtcMs(TicksPerMs - 1, out long edge), Is.True);
            Assert.That(edge, Is.EqualTo(ExactBound.Value));
            Assert.Throws<OverflowException>(() => TimelineMapping.Live(ExactBound.Value).RealUtcMs(TicksPerMs));

            Assert.Throws<ArgumentOutOfRangeException>(() => new TimelineMapping(Start, -1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimelineMapping(Start, TicksPerMs, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimelineMapping(Start, 0, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimelineMapping(Start, 0, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimelineMapping(Start, 0, -1, 1));
            Assert.That(TimelineMapping.TryLiveAt(ExactBound.Value, TicksPerMs, out TimelineMapping none), Is.False);
            Assert.That(none.IsValid, Is.False);
            Assert.That(TimelineMapping.TryLiveAt(Start, -1, out _), Is.False);
        }

        [Test]
        public void TheMappingIsAnAllocationFreeValueWithoutReferences()
        {
            Type type = typeof(TimelineMapping);
            Assert.That(type.IsValueType, Is.True);
            foreach (FieldInfo f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                Assert.That(f.FieldType, Is.EqualTo(typeof(long)), f.Name + ": the mapping holds integers only");
            Assert.That(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), Has.All.Matches<FieldInfo>(f => f.IsInitOnly), "immutable");

            long sum = 0;
            TimelineMapping m = TimelineMapping.Live(Start, 77);
            int allocations = GcAllocationCounter.Count(() =>
            {
                for (int i = 0; i < 1000; i++) sum += m.RealUtcMs(i * 12_345L);
            });
            Assert.That(allocations, Is.Zero);
            Assert.That(sum, Is.GreaterThan(0));
        }

        [Test]
        public void EqualityFollowsTheFourFields()
        {
            Assert.That(new TimelineMapping(Start, 1, 5, 4), Is.EqualTo(new TimelineMapping(Start, 1, 5, 4)));
            Assert.That(new TimelineMapping(Start, 1, 5, 4).GetHashCode(), Is.EqualTo(new TimelineMapping(Start, 1, 5, 4).GetHashCode()));
            Assert.That(new TimelineMapping(Start, 1, 5, 4), Is.Not.EqualTo(new TimelineMapping(Start, 2, 5, 4)));
            Assert.That(new TimelineMapping(Start, 1, 5, 4), Is.Not.EqualTo(new TimelineMapping(Start + 1, 1, 5, 4)));
            Assert.That(new TimelineMapping(Start, 1, 5, 4), Is.Not.EqualTo(new TimelineMapping(Start, 1, 4, 4)));
        }

        // ------------------------------------------------------------------------------------------------
        // Live stamps
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void AnyLivePartitionGivesTheSameStamps()
        {
            var rng = new Random(3250);
            for (int round = 0; round < 2_000; round++)
            {
                long anchor = Start + rng.Next(0, 1_000_000);
                long cut = rng.Next(0, 200_000_000);
                long t = rng.Next(0, 200_000_000);
                TimelineMapping whole = TimelineMapping.Live(anchor);
                Assert.That(TimelineMapping.TryLiveAt(anchor, cut, out TimelineMapping rebased), Is.True);
                Assert.That(rebased.OriginUtcMs, Is.EqualTo(anchor + cut / TicksPerMs));
                Assert.That(rebased.OriginTickPhase, Is.EqualTo(cut % TicksPerMs));
                Assert.That(rebased.RealUtcMs(t), Is.EqualTo(whole.RealUtcMs(cut + t)), $"anchor={anchor} cut={cut} t={t}");
            }
        }

        [Test]
        public void SessionTimelineIsTheLiveMappingStamp()
        {
            W003Scenario sc = W003Workload.Build(W003Variant.Reference);
            var session = new RunSession(sc.Catalog, sc.State, sc.Permanent, sc.StartUtcMs);
            Assert.That(session.TimelineUtcMs, Is.EqualTo(sc.StartUtcMs));
            Assert.That(session.Timeline, Is.EqualTo(TimelineMapping.Live(sc.StartUtcMs)));

            Assert.That(session.Advance(15_000).IsSuccess, Is.True);
            Assert.That(session.TimelineUtcMs, Is.EqualTo(sc.StartUtcMs + 1), "1.5 ms floors to 1");
            Assert.That(session.Timeline, Is.EqualTo(TimelineMapping.Live(sc.StartUtcMs + 1, 5_000)), "the phase is carried");
            Assert.That(session.Advance(5_000).IsSuccess, Is.True);
            Assert.That(session.TimelineUtcMs, Is.EqualTo(sc.StartUtcMs + 2));
            Assert.That(session.Timeline, Is.EqualTo(TimelineMapping.Live(sc.StartUtcMs + 2, 0)));
            Assert.That(session.Timeline.RealUtcMs(0), Is.EqualTo(session.TimelineUtcMs));
            Assert.That(session.Timeline.RealUtcMs(30_000), Is.EqualTo(session.TimelineUtcMs + 3), "a stamp ahead of the session");

            // A re-anchor restarts the mapping at the new anchor; a backward clock keeps the timeline.
            long later = sc.StartUtcMs + 5_000;
            Assert.That(session.RebindAnchor(later).IsSuccess, Is.True);
            Assert.That(session.AnchorUtcMs, Is.EqualTo(later));
            Assert.That(session.TimelineUtcMs, Is.EqualTo(later));
            Assert.That(session.Timeline, Is.EqualTo(TimelineMapping.Live(later)));
            Assert.That(session.RebindAnchor(sc.StartUtcMs).IsSuccess, Is.True);
            Assert.That(session.TimelineUtcMs, Is.EqualTo(later), "the timeline never moves back");
        }

        [Test]
        public void SessionPartitionsGiveTheSameStampsAndTheSameState()
        {
            const long total = 123_456_789L;
            string expected = null;
            string expectedHash = null;
            foreach (int seed in new[] { 0, 1, 2, 3 })
            {
                W003Scenario sc = W003Workload.Build(W003Variant.Reference);
                var session = new RunSession(sc.Catalog, sc.State, sc.Permanent, sc.StartUtcMs);
                var rng = new Random(seed);
                long left = total;
                long advanced = 0;
                while (left > 0)
                {
                    long step = seed == 0 ? left : Math.Min(left, rng.Next(1, seed == 1 ? 40_000 : 5_000_000));
                    Assert.That(session.Advance(step).IsSuccess, Is.True);
                    left -= step;
                    advanced += step;
                    Assert.That(session.ActiveTicksSinceAnchor, Is.EqualTo(advanced));
                    Assert.That(session.TimelineUtcMs, Is.EqualTo(sc.StartUtcMs + advanced / TicksPerMs));
                    Assert.That(session.Timeline.OriginTickPhase, Is.EqualTo(advanced % TicksPerMs));
                }
                string fingerprint = RunSimHarness.Fingerprint(sc.State, sc.Permanent);
                string hash = Sha256(fingerprint);
                if (expected == null)
                {
                    expected = fingerprint;
                    expectedHash = hash;
                }
                Assert.That(hash, Is.EqualTo(expectedHash), "seed " + seed + ": a live partition changes neither the state nor its hash");
                Assert.That(fingerprint, Is.EqualTo(expected));
            }
        }

        // ------------------------------------------------------------------------------------------------
        // Offline stamps
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void OfflineMappingIsTheExactInverseOfEInsideTheCap()
        {
            W003Scenario sc = W003Workload.Build(W003Variant.Bound);
            var segmenter = new OfflineSegmenter(sc.Catalog);
            long d = sc.StartUtcMs;
            TimelineMapping mapping = segmenter.MappingFor(d);
            Assert.That(mapping, Is.EqualTo(new TimelineMapping(d, 0, 5, 4)), "the inverse of 4/5");
            Assert.That(mapping, Is.EqualTo(TimelineMapping.Offline(sc.Catalog.OfflinePolicy, d)));

            var rng = new Random(32500);
            for (int i = 0; i < 20_000; i++)
            {
                long x = d + rng.Next(1, (int)Math.Min(int.MaxValue, CapMs + 1));
                Assert.That(mapping.RealUtcMs(segmenter.EffectiveTicks(d, x)), Is.EqualTo(x), "RealUtc(E(x)) = x for x - d = " + (x - d));
            }
            Assert.That(mapping.RealUtcMs(segmenter.EffectiveTicks(d, d)), Is.EqualTo(d), "at the departure");
            Assert.That(mapping.RealUtcMs(segmenter.EffectiveTicks(d, d - 5)), Is.EqualTo(d), "before the departure E is 0");
            Assert.That(mapping.RealUtcMs(segmenter.EffectiveTicks(d, d + CapMs)), Is.EqualTo(d + CapMs), "at the cap");
            Assert.That(mapping.RealUtcMs(segmenter.EffectiveTicks(d, d + CapMs + Hour)), Is.EqualTo(d + CapMs), "beyond the cap production ends at the cap");
            Assert.That(mapping.RealUtcMs(230_400_000_000L), Is.EqualTo(d + CapMs), "E(8 h) in ticks");
        }

        [Test]
        public void OfflineBoundaryEventsCarryTheStampOfTheirMappedTick()
        {
            W003Scenario sc = W003Workload.Build(W003Variant.Bound);
            sc.State.Effects.TryRemove(new ConflictKey("machineSpeed", "factory"));
            long d = sc.StartUtcMs;
            // One effect inside the window, one past the cap (it maps to the end of production).
            Assert.That(sc.State.TryAddEffect(new ActiveEffect("item.boost", new ConflictKey("machineSpeed", "factory"), d - Hour, d + Hour + 1)), Is.EqualTo(RunStateResult.Ok));
            var segmenter = new OfflineSegmenter(sc.Catalog);
            TimelineMapping mapping = segmenter.MappingFor(d);

            long r = d + 3 * Hour;
            IReadOnlyList<BoundaryEvent> events = segmenter.MapBoundaries(sc.State, d, r);
            Assert.That(events.Count, Is.GreaterThanOrEqualTo(1));
            bool sawEffect = false;
            foreach (BoundaryEvent e in events)
            {
                if (e.Kind == BoundaryKind.EffectExpiry && e.Id == "item.boost")
                {
                    sawEffect = true;
                    Assert.That(e.UtcMs, Is.EqualTo(d + Hour + 1));
                    Assert.That(e.SimTicks, Is.EqualTo(segmenter.EffectiveTicks(d, e.UtcMs)));
                }
                if (e.SimTicks > 0) Assert.That(mapping.RealUtcMs(e.SimTicks), Is.EqualTo(e.UtcMs), "the live stamp of the mapped tick is the boundary instant");
            }
            Assert.That(sawEffect, Is.True);

            // A boundary past the cap keeps its true instant; its tick is the end of production, whose stamp is the cap.
            IReadOnlyList<BoundaryEvent> late = segmenter.MapBoundaries(sc.State, d, d + CapMs + 2 * Hour);
            foreach (BoundaryEvent e in late)
            {
                if (e.UtcMs > d + CapMs)
                {
                    Assert.That(e.SimTicks, Is.EqualTo(230_400_000_000L));
                    Assert.That(mapping.RealUtcMs(e.SimTicks), Is.EqualTo(d + CapMs));
                }
            }
        }

        [Test]
        public void ReconcileThroughTheMappingEqualsTheLegacyAdvanceOfTheMappedEvents()
        {
            W003Scenario viaSegmenter = TimedBound();
            W003Scenario viaLegacy = TimedBound();
            long d = viaSegmenter.StartUtcMs;
            long r = d + 2 * Hour + 12_345;

            RunOfflineSummary summary = new OfflineSegmenter(viaSegmenter.Catalog).Reconcile(viaSegmenter.State, viaSegmenter.Permanent, d, r);
            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(summary.ExpiredEffects.Count, Is.EqualTo(1), "the effect ends inside the window");

            var segmenter = new OfflineSegmenter(viaLegacy.Catalog);
            var events = new List<BoundaryEvent>(segmenter.MapBoundaries(viaLegacy.State, d, r));
            Assert.That(new RunSimulator(viaLegacy.Catalog).Advance(viaLegacy.State, viaLegacy.Permanent, segmenter.EffectiveTicks(d, r), d, events),
                Is.EqualTo(RunAdvanceResult.Ok));

            string a = RunSimHarness.Fingerprint(viaSegmenter.State, viaSegmenter.Permanent);
            string b = RunSimHarness.Fingerprint(viaLegacy.State, viaLegacy.Permanent);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(Sha256(a), Is.EqualTo(Sha256(b)));
        }

        private static W003Scenario TimedBound()
        {
            W003Scenario sc = W003Workload.Build(W003Variant.Bound);
            sc.State.Effects.TryRemove(new ConflictKey("machineSpeed", "factory"));
            Assert.That(sc.State.TryAddEffect(new ActiveEffect("item.boost", new ConflictKey("machineSpeed", "factory"), sc.StartUtcMs - Hour, sc.StartUtcMs + Hour)),
                Is.EqualTo(RunStateResult.Ok));
            return sc;
        }

        // ------------------------------------------------------------------------------------------------
        // The Advance signature
        // ------------------------------------------------------------------------------------------------

        [TestCase(W003Variant.Bound)]
        [TestCase(W003Variant.Stress)]
        [TestCase(W003Variant.Reference)]
        public void LegacyAndMappingOverloadsAreBitIdenticalOnTheW003Workloads(W003Variant variant)
        {
            W003Scenario legacy = W003Workload.Build(variant);
            W003Scenario mapped = W003Workload.Build(variant);
            var simLegacy = new RunSimulator(legacy.Catalog);
            var simMapped = new RunSimulator(mapped.Catalog);
            // About four minutes of production in uneven steps; the live mapping carries the phase of every step.
            long[] steps = { 1_800_000_000L, 12_345, 170_000_001L, 1, 9_999, 10_000, 600_000_000L };
            long now = 0;
            foreach (long step in steps)
            {
                long originMs = legacy.StartUtcMs + now / TicksPerMs;
                IReadOnlyList<BoundaryEvent> noEvents = new BoundaryEvent[0];
                Assert.That(simLegacy.Advance(legacy.State, legacy.Permanent, step, originMs, noEvents), Is.EqualTo(RunAdvanceResult.Ok));
                Assert.That(TimelineMapping.TryLiveAt(mapped.StartUtcMs, now, out TimelineMapping mapping), Is.True);
                Assert.That(mapping.OriginUtcMs, Is.EqualTo(originMs));
                Assert.That(simMapped.Advance(mapped.State, mapped.Permanent, step, mapping, noEvents), Is.EqualTo(RunAdvanceResult.Ok));
                now += step;

                Assert.That(simMapped.LastCompletions, Is.EqualTo(simLegacy.LastCompletions));
                Assert.That(simMapped.LastTimestamps, Is.EqualTo(simLegacy.LastTimestamps));
                Assert.That(simMapped.LastPasses, Is.EqualTo(simLegacy.LastPasses));
                Assert.That(simMapped.LastMaxPassesPerTimestamp, Is.EqualTo(simLegacy.LastMaxPassesPerTimestamp));
            }
            Assert.That(simMapped.LastCompletions + simLegacy.LastCompletions, Is.GreaterThan(0));

            string a = RunSimHarness.Fingerprint(legacy.State, legacy.Permanent);
            string b = RunSimHarness.Fingerprint(mapped.State, mapped.Permanent);
            Assert.That(b, Is.EqualTo(a), legacy.Name);
            Assert.That(Sha256(b), Is.EqualTo(Sha256(a)), legacy.Name + " state hash");
        }

        [Test]
        public void TheLegacyOverloadIsTheLiveMappingAtThatInstant()
        {
            // An Overdrive that is active at the start only until a later boundary: the origin decides, the phase does not.
            W003Scenario a = W003Workload.Build(W003Variant.Bound);
            W003Scenario b = W003Workload.Build(W003Variant.Bound);
            long overdriveEnd = a.State.Overdrive.ActiveUntilUtcMs.Value;
            Assert.That(new RunSimulator(a.Catalog).Advance(a.State, a.Permanent, 50_000_000, overdriveEnd, null), Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(new RunSimulator(b.Catalog).Advance(b.State, b.Permanent, 50_000_000, TimelineMapping.Live(overdriveEnd), null), Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(RunSimHarness.Fingerprint(b.State, b.Permanent), Is.EqualTo(RunSimHarness.Fingerprint(a.State, a.Permanent)));

            // The phase never changes the simulation (only the stamps).
            W003Scenario c = W003Workload.Build(W003Variant.Bound);
            Assert.That(new RunSimulator(c.Catalog).Advance(c.State, c.Permanent, 50_000_000, TimelineMapping.Live(overdriveEnd, 9_999), null), Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(RunSimHarness.Fingerprint(c.State, c.Permanent), Is.EqualTo(RunSimHarness.Fingerprint(a.State, a.Permanent)));
        }

        [Test]
        public void AnInvalidMappingIsRejectedWithoutChangingTheState()
        {
            W003Scenario sc = W003Workload.Build(W003Variant.Reference);
            string before = RunSimHarness.Fingerprint(sc.State, sc.Permanent);
            var sim = new RunSimulator(sc.Catalog);
            Assert.That(sim.Advance(sc.State, sc.Permanent, 10_000_000, default(TimelineMapping), null), Is.EqualTo(RunAdvanceResult.InvalidArgument));
            Assert.That(sim.LastCompletions, Is.Zero);
            Assert.That(RunSimHarness.Fingerprint(sc.State, sc.Permanent), Is.EqualTo(before));
            Assert.That(sim.Advance(sc.State, sc.Permanent, -1, TimelineMapping.Live(sc.StartUtcMs), null), Is.EqualTo(RunAdvanceResult.InvalidArgument));
            Assert.That(RunSimHarness.Fingerprint(sc.State, sc.Permanent), Is.EqualTo(before));
        }

        [Test]
        public void WarmedAdvanceWithAMappingHasZeroGcAllocSamples()
        {
            W003Scenario sc = W003Workload.Build(W003Variant.Bound);
            var sim = new RunSimulator(sc.Catalog);
            IReadOnlyList<BoundaryEvent> noEvents = new BoundaryEvent[0];
            TimelineMapping mapping = TimelineMapping.Live(sc.StartUtcMs);
            for (int i = 0; i < 40; i++)
                Assert.That(sim.Advance(sc.State, sc.Permanent, 10_000_000L, mapping, noEvents), Is.EqualTo(RunAdvanceResult.Ok));

            for (int i = 0; i < 4; i++)
            {
                RunAdvanceResult result = RunAdvanceResult.InvalidArgument;
                int samples = GcAllocationCounter.Count(() => result = sim.Advance(sc.State, sc.Permanent, 10_000_000L, mapping, noEvents));
                Assert.That(result, Is.EqualTo(RunAdvanceResult.Ok));
                Assert.That(sim.LastCompletions, Is.GreaterThan(0));
                Assert.That(samples, Is.Zero, "the mapping adds no allocation to a warmed advance");
            }
        }

        private static string Sha256(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte x in hash) sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
