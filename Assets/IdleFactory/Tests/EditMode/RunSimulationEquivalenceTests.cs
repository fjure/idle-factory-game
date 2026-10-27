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

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// Determinism of <see cref="RunSimulator"/> (SC-002, plan.md, Event order and determinism, cases a-k):
    /// one large advance against random partitions (with cuts at event instants), definition-order and build-order
    /// permutations, offline-mapped segmentation against the online advance through the same entry point, a
    /// tick-by-tick oracle on fixed seeds, per-step stock conservation and the N+1 pass bound. Every
    /// randomized case names its seed in the failure message.
    /// </summary>
    public sealed class RunSimulationEquivalenceTests
    {
        private const long Second = RunSimHarness.TicksPerSecond;
        private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

        // ---------------------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------------------

        private static string Run(SimScenario sc, long total, IEnumerable<long> cuts = null)
        {
            var sim = new RunSimulator(sc.Catalog);
            RunSimHarness.RunCut(sim, sc, total, cuts ?? Enumerable.Empty<long>());
            return RunSimHarness.Fingerprint(sc.State, sc.Permanent);
        }

        /// <summary>Random cuts plus every event instant of the scenario (and its neighbours) chosen at random.</summary>
        private static List<long> Cuts(SimScenario sc, long total, Random rng)
        {
            var cuts = new List<long>();
            int random = 1 + rng.Next(10);
            for (int i = 0; i < random; i++) cuts.Add(1 + (long)(rng.NextDouble() * (total - 1)));
            foreach (BoundaryEvent e in RunSimHarness.Events(sc.State, sc.StartUtcMs, 0, total))
            {
                if (rng.Next(3) == 0) continue;
                cuts.Add(e.SimTicks);
                if (rng.Next(3) == 0) cuts.Add(e.SimTicks - 1);
                if (rng.Next(3) == 0) cuts.Add(e.SimTicks + 1);
            }
            return cuts.Where(c => c > 0 && c < total).Distinct().OrderBy(c => c).ToList();
        }

        private static string Describe(IEnumerable<long> cuts) => "cuts [" + string.Join(", ", cuts) + "]";

        /// <summary>Single advance against several random partitions of the same scenario factory.</summary>
        private static void AssertPartitionEquivalence(Func<SimScenario> build, long total, string name, int partitions = 4, int rngSeed = 0)
        {
            SimScenario single = build();
            var sim = new RunSimulator(single.Catalog);
            long[] before = RunSimHarness.Units(single.State);
            RunSimHarness.RunCut(sim, single, total, Enumerable.Empty<long>());
            RunSimHarness.AssertFlow(sim, before, single.State, name + " single");
            Assert.That(sim.LastMaxPassesPerTimestamp, Is.LessThanOrEqualTo(single.State.Machines.Count + 1), name + ": passes per timestamp");
            string expected = RunSimHarness.Fingerprint(single.State, single.Permanent);

            var rng = new Random(rngSeed * 7919 + 13);
            for (int p = 0; p < partitions; p++)
            {
                SimScenario partitioned = build();
                List<long> cuts = Cuts(partitioned, total, rng);
                string actual = Run(partitioned, total, cuts);
                Assert.That(actual, Is.EqualTo(expected), name + " partition " + p + " " + Describe(cuts));
            }
        }

        // ---------------------------------------------------------------------------------------------
        // (a) single advance against random partitions, including cuts at event instants
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void CaseA_SingleAdvance_EqualsRandomPartitions_IncludingCutsAtEventInstants()
        {
            foreach (int seed in Seeds)
                AssertPartitionEquivalence(() => SimScenario.Random(seed, false, 0, false), 40 * Second, "seed " + seed, 4, seed);
        }

        [Test]
        public void CaseA_TinyAndOffByOnePartitions_AroundEveryEventInstant()
        {
            foreach (int seed in new[] { 3, 5, 8 })
            {
                SimScenario single = SimScenario.Random(seed, false, 0, false);
                const long total = 30 * Second;
                string expected = Run(SimScenario.Random(seed, false, 0, false), total);
                var events = RunSimHarness.Events(single.State, single.StartUtcMs, 0, total).Select(e => e.SimTicks).Distinct().ToList();
                var cuts = events.SelectMany(t => new[] { t - 1, t, t + 1 }).ToList();
                Assert.That(Run(SimScenario.Random(seed, false, 0, false), total, cuts), Is.EqualTo(expected), "seed " + seed + " " + Describe(cuts));
            }
        }

        // ---------------------------------------------------------------------------------------------
        // (b) definition-order permutations (what a JSON member and entry permutation becomes after reading)
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void CaseB_CatalogDefinitionOrderPermutations_GiveTheIdenticalState()
        {
            const long total = 40 * Second;
            foreach (int seed in Seeds.Take(8))
            {
                string expected = Run(SimScenario.Random(seed, false, 0, false), total);
                foreach (int orderSeed in new[] { 11, 12, 13 })
                    Assert.That(Run(SimScenario.Random(seed, false, orderSeed, false), total), Is.EqualTo(expected),
                        "seed " + seed + " definition order " + orderSeed);
            }
        }

        // ---------------------------------------------------------------------------------------------
        // (c) priority alone orders contention: build order and slots never matter
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void CaseC_BuildOrderAndSlotPermutations_GiveTheIdenticalState_KeyedByInstance()
        {
            const long total = 40 * Second;
            foreach (int seed in Seeds.Take(8))
            {
                SimScenario reference = SimScenario.Random(seed, false, 0, false);
                Run(reference, total);
                foreach (int orderSeed in new[] { 5, 6, 7 })
                {
                    // Machines built in another order into other slots, the same priority by instance.
                    SimScenario permuted = SimScenario.Random(seed, false, orderSeed, true);
                    Run(permuted, total);
                    // The slots differ by construction: every other field must match.
                    Assert.That(RunSimHarness.Fingerprint(permuted.State, permuted.Permanent, false),
                        Is.EqualTo(RunSimHarness.Fingerprint(reference.State, reference.Permanent, false)),
                        "seed " + seed + " build/slot permutation " + orderSeed);
                }
            }
        }

        [Test]
        public void CaseC_ChangingThePriorityChangesWhoWinsContention()
        {
            RunCatalog catalog = RunSimHarness.Catalog(new RunSimHarness.CatalogOptions { StorageCapacity = 2 });
            var outcomes = new HashSet<string>();
            foreach (int[] order in new[] { new[] { 1, 2, 3 }, new[] { 3, 2, 1 }, new[] { 2, 3, 1 } })
            {
                RunState s = RunSimHarness.NewState(catalog);
                for (int i = 1; i <= 3; i++) RunSimHarness.Add(s, "machine.miner", i - 1, i, progress: 20_000_000);
                RunSimHarness.Reorder(s, order);
                var run = new SimRun(catalog, s);
                run.StepOk(0);
                int winner = Enumerable.Range(1, 3).Single(i => run.Machine(i).ProgressTicks == 0);
                Assert.That(winner, Is.EqualTo(order[0]));
                outcomes.Add(winner.ToString());
            }
            Assert.That(outcomes.Count, Is.EqualTo(3));
        }

        // ---------------------------------------------------------------------------------------------
        // (d) offline segmented against online, both through RunSimulator.Advance
        // ---------------------------------------------------------------------------------------------

        private const long OfflineCapTicks = 288_000_000_000L;

        /// <summary>E(x) of research R05 in ticks: floor(min(max(x, 0), cap) x 4 / 5).</summary>
        private static long E(long ticksSinceDeparture) => Math.Min(Math.Max(ticksSinceDeparture, 0), OfflineCapTicks) * 4 / 5;

        /// <summary>The state's boundaries, every real boundary mapped through E and ordered by (kind, id).</summary>
        private static List<BoundaryEvent> MappedEvents(RunState state, long departureUtcMs, long realTicks)
        {
            var events = new List<BoundaryEvent>();
            foreach (BoundaryEvent real in RunSimHarness.Events(state, departureUtcMs, 0, long.MaxValue / 4))
            {
                long abs = real.SimTicks;
                if (abs > realTicks) continue;
                events.Add(new BoundaryEvent(E(abs), real.Kind, real.Id, real.UtcMs));
            }
            events.Sort((a, b) => BoundaryEvent.Compare(a, b));
            return events;
        }

        private static string OfflineOnline(SimScenario sc, long realTicks)
        {
            List<BoundaryEvent> events = MappedEvents(sc.State, sc.StartUtcMs, realTicks);
            var sim = new RunSimulator(sc.Catalog);
            Assert.That(sim.Advance(sc.State, sc.Permanent, E(realTicks), sc.StartUtcMs, events), Is.EqualTo(RunAdvanceResult.Ok));
            return RunSimHarness.Fingerprint(sc.State, sc.Permanent);
        }

        private static string OfflineSegmented(SimScenario sc, long realTicks, long extraCutEvery)
        {
            List<BoundaryEvent> all = MappedEvents(sc.State, sc.StartUtcMs, realTicks);
            long total = E(realTicks);
            var cuts = all.Select(e => e.SimTicks).Concat(Enumerable.Range(1, (int)(total / extraCutEvery)).Select(i => i * extraCutEvery))
                .Where(c => c > 0 && c < total).Distinct().OrderBy(c => c).Concat(new[] { total }).ToList();
            var sim = new RunSimulator(sc.Catalog);
            long at = 0;
            // The start instant only decides whether Overdrive is active at the segment start. Overdrive can only
            // change at a boundary, so the real instant of the last processed boundary is a correct value.
            long realNow = sc.StartUtcMs;
            foreach (long cut in cuts)
            {
                // Boundaries collapse onto the same simulation instant when E floors them together; every one of
                // them belongs to the segment that ends there.
                var segment = all.Where(e => (at == 0 ? e.SimTicks <= cut : e.SimTicks > at && e.SimTicks <= cut)).ToList();
                var rebased = segment.Select(e => new BoundaryEvent(e.SimTicks - at, e.Kind, e.Id, e.UtcMs)).ToList();
                Assert.That(sim.Advance(sc.State, sc.Permanent, cut - at, realNow, rebased), Is.EqualTo(RunAdvanceResult.Ok));
                foreach (BoundaryEvent e in segment) realNow = Math.Max(realNow, e.UtcMs);
                at = cut;
            }
            return RunSimHarness.Fingerprint(sc.State, sc.Permanent);
        }

        [Test]
        public void CaseD_OfflineSegmentedEqualsOnline_ThroughTheSameEntryPoint()
        {
            const long realTicks = 60 * Second;
            foreach (int seed in Seeds)
            {
                string online = OfflineOnline(SimScenario.Random(seed, false, 0, false), realTicks);
                string segmented = OfflineSegmented(SimScenario.Random(seed, false, 0, false), realTicks, 7 * Second);
                Assert.That(segmented, Is.EqualTo(online), "seed " + seed);
            }
        }

        [Test]
        public void CaseD_EIsMonotoneAndTelescopes()
        {
            var rng = new Random(4);
            for (int trial = 0; trial < 200; trial++)
            {
                var boundaries = Enumerable.Range(0, 1 + rng.Next(6)).Select(_ => (long)(rng.NextDouble() * 4e11)).OrderBy(x => x).ToList();
                long r = boundaries.Last() + rng.Next(1000);
                long sum = 0, previous = 0;
                foreach (long b in boundaries.Concat(new[] { r })) { sum += E(b) - E(previous); previous = b; }
                Assert.That(sum, Is.EqualTo(E(r)), "E telescopes, trial " + trial);
            }
        }

        [Test]
        public void CaseD_BoundaryBeyondTheCapIsProcessedAtTheEndOfProduction()
        {
            const long nineHoursMs = 9L * 3600 * 1000;
            RunCatalog catalog = RunSimHarness.Catalog(new RunSimHarness.CatalogOptions
            {
                BoostItemDurationMs = nineHoursMs,
                StorageCapacity = 1_000_000,
            });
            RunState s = RunSimHarness.NewState(catalog);
            RunSimHarness.Add(s, "machine.miner", 0, 1);
            s.TrySetAutoSell("iron.ore", true);
            s.TryAddEffect(new ActiveEffect("item.boost", new ConflictKey("machineSpeed", "factory"), RunSimHarness.StartUtcMs, RunSimHarness.StartUtcMs + nineHoursMs));
            long twelveHours = 12L * 3600 * Second;

            List<BoundaryEvent> events = MappedEvents(s, RunSimHarness.StartUtcMs, twelveHours);
            Assert.That(events.Single().SimTicks, Is.EqualTo(E(OfflineCapTicks)), "the 9 h boundary lies beyond the 8 h cap and maps to the end of production");
            var sim = new RunSimulator(catalog);
            Assert.That(sim.Advance(s, null, E(twelveHours), RunSimHarness.StartUtcMs, events), Is.EqualTo(RunAdvanceResult.Ok));

            Assert.That(s.Effects.Count, Is.EqualTo(0));
            Assert.That(E(twelveHours), Is.EqualTo(230_400_000_000L), "at most 6.4 h of production");
            // Every completion sold 2 Ore at $1 with the boost active for the whole credited time.
            Assert.That(s.Cash, Is.EqualTo(25 + sim.LastCompletions * 2));
            Assert.That(sim.LastCompletions, Is.EqualTo(230_400_000_000L / 13_333_334L), "cycle ceil(2e7 / 1.5) throughout");
        }

        // ---------------------------------------------------------------------------------------------
        // (e) the literal FR-002 chain, its bottleneck and any time partition
        // ---------------------------------------------------------------------------------------------

        private static SimScenario Chain()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState s = RunSimHarness.NewState(catalog);
            RunSimHarness.Add(s, "machine.miner", 0, 1);
            RunSimHarness.Add(s, "machine.smelter", 1, 2);
            RunSimHarness.Add(s, "machine.press", 2, 3);
            RunSimHarness.Add(s, "machine.assembler", 3, 4);
            s.TrySetAutoSell("iron.gear", true);
            return new SimScenario { Catalog = catalog, State = s, Permanent = new PermanentState(catalog) };
        }

        [Test]
        public void CaseE_LiteralChain_EveryPartitionGivesTheSameState_AndTheBottleneckIsVisible()
        {
            AssertPartitionEquivalence(Chain, 800 * Second, "FR-002 chain", 8, 5);
            SimScenario sc = Chain();
            Run(sc, 800 * Second);
            Assert.That(sc.State.Cash, Is.EqualTo(25 + 98 * 24), "one Gear per 8 s: the Assembler is busy 5 s in 8 (62.5 %)");
            // One-second steps produce the same state.
            SimScenario stepped = Chain();
            var sim = new RunSimulator(stepped.Catalog);
            RunSimHarness.RunCut(sim, stepped, 800 * Second, Enumerable.Range(1, 799).Select(i => i * Second));
            Assert.That(RunSimHarness.Fingerprint(stepped.State), Is.EqualTo(RunSimHarness.Fingerprint(sc.State)));
        }

        // ---------------------------------------------------------------------------------------------
        // (f) a full store: the net-fitting Smelter completes, the input-free Miner stays blocked
        // ---------------------------------------------------------------------------------------------

        private static SimScenario FullStore()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState s = RunSimHarness.NewState(catalog);
            RunSimHarness.Add(s, "machine.miner", 0, 1, progress: 20_000_000);
            RunSimHarness.Add(s, "machine.smelter", 1, 2, progress: 20_000_000);
            RunSimHarness.Add(s, "machine.press", 2, 3, progress: 30_000_000);
            RunSimHarness.Stock(s, "iron.ore", 1000);
            return new SimScenario { Catalog = catalog, State = s, Permanent = new PermanentState(catalog) };
        }

        [Test]
        public void CaseF_FullStore_EveryPartitionGivesTheSameState()
        {
            AssertPartitionEquivalence(FullStore, 90 * Second, "full store", 8, 6);
        }

        // ---------------------------------------------------------------------------------------------
        // (g) auto-sell starving a downstream machine (US2)
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void CaseG_AutoSellOfTheInput_StarvesTheSmelter_UntilItIsSwitchedOff()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState s = RunSimHarness.NewState(catalog);
            RunSimHarness.Add(s, "machine.miner", 0, 1);
            RunSimHarness.Add(s, "machine.smelter", 1, 2);
            s.TrySetAutoSell("iron.ore", true);
            var run = new SimRun(catalog, s);

            run.StepOk(60 * Second);

            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(0), "the Ore is sold at every timestamp before the Smelter can run");
            Assert.That(run.Machine(2).ProgressTicks, Is.EqualTo(0));
            Assert.That(s.Cash, Is.EqualTo(25 + 30 * 2));

            s.TrySetAutoSell("iron.ore", false);
            run.StepOk(10 * Second);
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(4), "the Smelter completes at 4, 6, 8 and 10 s");
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(2));
            AssertPartitionEqualsSingleOnAutoSellToggle();
        }

        private static void AssertPartitionEqualsSingleOnAutoSellToggle()
        {
            string Play(bool partitioned)
            {
                RunCatalog catalog = RunSimHarness.Catalog();
                RunState s = RunSimHarness.NewState(catalog);
                RunSimHarness.Add(s, "machine.miner", 0, 1);
                RunSimHarness.Add(s, "machine.smelter", 1, 2);
                s.TrySetAutoSell("iron.ore", true);
                var run = new SimRun(catalog, s);
                if (partitioned) for (int i = 0; i < 60; i++) run.StepOk(Second); else run.StepOk(60 * Second);
                s.TrySetAutoSell("iron.ore", false);
                if (partitioned) for (int i = 0; i < 100; i++) run.StepOk(Second / 10); else run.StepOk(10 * Second);
                return RunSimHarness.Fingerprint(s);
            }
            Assert.That(Play(true), Is.EqualTo(Play(false)));
        }

        // ---------------------------------------------------------------------------------------------
        // (h) mid-cycle upgrades, Research, Items and Overdrive keep the fraction on every partition
        // ---------------------------------------------------------------------------------------------

        private static SimScenario MidCycle()
        {
            RunCatalog catalog = RunSimHarness.Catalog(new RunSimHarness.CatalogOptions { BoostItemDurationMs = 20_000, DrillItemDurationMs = 12_000 });
            RunState s = RunSimHarness.NewState(catalog);
            var permanent = new PermanentState(catalog);
            RunSimHarness.Add(s, "machine.miner", 0, 1, progress: 7_000_000);
            RunSimHarness.Add(s, "machine.smelter", 1, 2, progress: 3_000_000);
            RunSimHarness.Add(s, "machine.press", 2, 3, progress: 21_000_000);
            RunSimHarness.Add(s, "machine.assembler", 3, 4, progress: 9_000_000);
            RunSimHarness.Stock(s, "iron.ore", 20);
            RunSimHarness.Stock(s, "iron.bar", 8);
            RunSimHarness.Stock(s, "iron.plate", 4);
            s.TrySetAutoSell("iron.gear", true);
            long start = RunSimHarness.StartUtcMs;
            s.TryAddEffect(new ActiveEffect("item.boost", new ConflictKey("machineSpeed", "factory"), start, start + 20_000));
            s.TryAddEffect(new ActiveEffect("item.drill", new ConflictKey("machineOutput", "machine.miner"), start, start + 12_000));
            s.Overdrive.TryActivate(start - 1_500);
            s.TrySetResearchLevel("research.a-smelter", 2);
            s.TrySetResearchLevel("research.b-all", 1);
            permanent.TrySetIndustryLevel("industry.a-speed", 1);
            return new SimScenario { Catalog = catalog, State = s, Permanent = permanent };
        }

        [Test]
        public void CaseH_MidCycleChanges_EveryPartitionGivesTheSameState()
        {
            AssertPartitionEquivalence(MidCycle, 60 * Second, "mid-cycle changes", 10, 8);
            // The single advance really crossed the boundaries: the effects are gone and Overdrive is cooling down.
            SimScenario sc = MidCycle();
            Run(sc, 60 * Second);
            Assert.That(sc.State.Effects.Count, Is.EqualTo(0));
            Assert.That(sc.State.Overdrive.ActiveUntilUtcMs, Is.Not.Null);
        }

        // ---------------------------------------------------------------------------------------------
        // (i) tick-by-tick oracle on fixed seeds, with conservation and the N+1 bound
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void CaseI_SimulatorMatchesTheTickByTickOracle_OnFixedSeeds()
        {
            const long total = 5_000_000;
            long partialBatches = 0, completions = 0;
            foreach (int seed in Seeds)
            {
                SimScenario forSim = SimScenario.Random(seed, true, 0, false);
                SimScenario forOracle = SimScenario.Random(seed, true, 0, false);
                var sim = new RunSimulator(forSim.Catalog);
                long[] before = RunSimHarness.Units(forSim.State);
                Assert.That(RunSimHarness.AdvanceAt(sim, forSim.State, forSim.Permanent, forSim.StartUtcMs, 0, total), Is.EqualTo(RunAdvanceResult.Ok), "seed " + seed);
                var oracle = new TickOracle(forOracle);
                oracle.Run(total);

                Assert.That(RunSimHarness.Fingerprint(forSim.State, forSim.Permanent), Is.EqualTo(RunSimHarness.Fingerprint(forOracle.State, forOracle.Permanent)), "seed " + seed);
                foreach (string id in RunSimHarness.AllResources)
                {
                    Assert.That(sim.LastProducedUnits(id), Is.EqualTo(oracle.ProducedUnits(id)), "seed " + seed + " produced " + id);
                    Assert.That(sim.LastConsumedUnits(id), Is.EqualTo(oracle.ConsumedUnits(id)), "seed " + seed + " consumed " + id);
                    Assert.That(sim.LastSoldUnits(id), Is.EqualTo(oracle.SoldUnits(id)), "seed " + seed + " sold " + id);
                }
                Assert.That(sim.LastCompletions, Is.EqualTo(oracle.Completions), "seed " + seed + " completions");
                RunSimHarness.AssertFlow(sim, before, forSim.State, "seed " + seed);
                Assert.That(sim.LastMaxPassesPerTimestamp, Is.LessThanOrEqualTo(forSim.State.Machines.Count + 1), "seed " + seed);
                Assert.That(oracle.MaxPasses, Is.LessThanOrEqualTo(forSim.State.Machines.Count + 1), "seed " + seed + " oracle");
                partialBatches += sim.LastPartialBatches;
                completions += sim.LastCompletions;
            }
            Assert.That(completions, Is.GreaterThan(20), "the seeds exercise real production");
            Assert.That(partialBatches, Is.GreaterThan(0), "the seeds exercise partial batches (OD-9)");
        }

        [Test]
        public void CaseI_OracleSeedsAlsoAgreeWithRandomPartitionsOfTheSimulator()
        {
            const long total = 5_000_000;
            foreach (int seed in Seeds)
            {
                SimScenario a = SimScenario.Random(seed, true, 0, false);
                SimScenario b = SimScenario.Random(seed, true, 0, false);
                var rng = new Random(seed);
                List<long> cuts = Cuts(b, total, rng);
                Assert.That(Run(b, total, cuts), Is.EqualTo(Run(a, total)), "seed " + seed + " " + Describe(cuts));
            }
        }

        // ---------------------------------------------------------------------------------------------
        // (j) several producers contending for capacity, and an expiry and a completion at the same instant
        // ---------------------------------------------------------------------------------------------

        private static SimScenario Contention(int[] priority)
        {
            RunCatalog catalog = RunSimHarness.Catalog(new RunSimHarness.CatalogOptions { StorageCapacity = 14 });
            RunState s = RunSimHarness.NewState(catalog);
            RunSimHarness.Add(s, "machine.miner", 0, 1);
            RunSimHarness.Add(s, "machine.miner", 1, 2, level: 2);
            RunSimHarness.Add(s, "machine.miner", 2, 3, level: 3);
            RunSimHarness.Add(s, "machine.smelter", 3, 4);
            RunSimHarness.Add(s, "machine.press", 4, 5);
            RunSimHarness.Reorder(s, priority);
            s.TrySetAutoSell("iron.plate", true);
            return new SimScenario { Catalog = catalog, State = s, Permanent = new PermanentState(catalog) };
        }

        [Test]
        public void CaseJ_ProducersContendForCapacity_EveryPartitionGivesTheSameState()
        {
            foreach (int[] priority in new[] { new[] { 1, 2, 3, 4, 5 }, new[] { 5, 4, 3, 2, 1 }, new[] { 3, 5, 1, 4, 2 } })
                AssertPartitionEquivalence(() => Contention(priority), 90 * Second, "contention " + string.Join(",", priority), 6, priority[0]);
        }

        [TestCase(2000)]
        [TestCase(1999)]
        [TestCase(2001)]
        public void CaseJ_ExpiryAndCompletionAtTheSameInstant_EveryCutAroundItGivesTheSameState(int drillDurationMs)
        {
            SimScenario Build()
            {
                RunCatalog catalog = RunSimHarness.Catalog(new RunSimHarness.CatalogOptions { DrillItemDurationMs = drillDurationMs });
                RunState s = RunSimHarness.NewState(catalog);
                RunSimHarness.Add(s, "machine.miner", 0, 1);
                RunSimHarness.Add(s, "machine.smelter", 1, 2);
                s.TryAddEffect(new ActiveEffect("item.drill", new ConflictKey("machineOutput", "machine.miner"), RunSimHarness.StartUtcMs, RunSimHarness.StartUtcMs + drillDurationMs));
                return new SimScenario { Catalog = catalog, State = s, Permanent = new PermanentState(catalog) };
            }
            const long total = 20 * Second;
            string expected = Run(Build(), total);
            long instant = drillDurationMs * RunSimHarness.TicksPerMs;
            foreach (long[] cuts in new[]
            {
                new[] { instant }, new[] { instant - 1 }, new[] { instant + 1 }, new[] { instant - 1, instant, instant + 1 },
                new[] { 2 * Second - 1, 2 * Second, 2 * Second + 1 },
            })
                Assert.That(Run(Build(), total, cuts), Is.EqualTo(expected), "drill " + drillDurationMs + " ms " + Describe(cuts));
        }

        // ---------------------------------------------------------------------------------------------
        // (k) batched cycles beyond the event bound and partial batches
        // ---------------------------------------------------------------------------------------------

        private static SimScenario Batched(int variant)
        {
            RunCatalog catalog = RunSimHarness.Catalog(new RunSimHarness.CatalogOptions
            {
                SpeedGrowthPpm = 100_000_000,
                MaxLevel = 2,
                StorageCapacity = variant == 0 ? 60 : 400,
                MaxCombinedSpeedPpm = 1_000_000_000L,
                LabRecipes = true,
            });
            RunState s = RunSimHarness.NewState(catalog);
            RunSimHarness.Add(s, "machine.miner", 0, 1, level: 2);
            RunSimHarness.Add(s, "machine.multiplier", 1, 2, level: 2);
            RunSimHarness.Add(s, "machine.smelter", 2, 3, level: 2);
            RunSimHarness.Add(s, "machine.alloy", 3, 4, level: 2);
            RunSimHarness.Add(s, "machine.press", 4, 5, level: 1);
            if (variant == 1) s.TrySetAutoSell("iron.plate", true);
            RunSimHarness.Stock(s, "iron.ore", 7);
            return new SimScenario { Catalog = catalog, State = s, Permanent = new PermanentState(catalog) };
        }

        [Test]
        public void CaseK_BatchedAndPartialBatches_ConserveStock_AndEveryPartitionGivesTheSameState()
        {
            foreach (int variant in new[] { 0, 1 })
            {
                SimScenario single = Batched(variant);
                var sim = new RunSimulator(single.Catalog);
                long[] before = RunSimHarness.Units(single.State);
                RunSimHarness.RunCut(sim, single, 30 * Second, Enumerable.Empty<long>());
                RunSimHarness.AssertFlow(sim, before, single.State, "batched variant " + variant);
                Assert.That(sim.LastPartialBatches, Is.GreaterThan(0), "variant " + variant + " completes partial batches");
                Assert.That(sim.LastCompletions, Is.GreaterThan(20));
                AssertPartitionEquivalence(() => Batched(variant), 30 * Second, "batched variant " + variant, 6, variant + 1);
            }
        }

        [Test]
        public void CaseK_PerStepConservation_HoldsForEveryStepOfAFineGrainedRun()
        {
            SimScenario sc = Batched(0);
            var run = new SimRun(sc.Catalog, sc.State);
            for (int step = 0; step < 300; step++)
            {
                long[] before = RunSimHarness.Units(sc.State);
                run.StepOk(100_000 + step * 3_331);
                RunSimHarness.AssertFlow(run.Sim, before, sc.State, "step " + step);
                Assert.That(sc.State.Validate(), Is.EqualTo(RunStateResult.Ok), "invariants after step " + step);
            }
        }

        // ---------------------------------------------------------------------------------------------
        // (q) sale and valuation conservation per step (T316, F5-R04): one sale price for auto-sell and Factory Value
        // ---------------------------------------------------------------------------------------------

        private static SimScenario SellBar()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState s = RunSimHarness.NewState(catalog);
            RunSimHarness.Add(s, "machine.miner", 0, 1);
            RunSimHarness.Add(s, "machine.smelter", 1, 2);
            RunSimHarness.Stock(s, "iron.ore", 40);
            s.TrySetAutoSell("iron.bar", true);
            return new SimScenario { Catalog = catalog, State = s, Permanent = new PermanentState(catalog) };
        }

        [Test]
        public void CaseQ_SaleAndValuationConservation_HoldsForEveryStep()
        {
            foreach (Func<SimScenario> build in new Func<SimScenario>[] { Chain, SellBar })
            {
                SimScenario sc = build();
                var run = new SimRun(sc.Catalog, sc.State, sc.Permanent, sc.StartUtcMs);
                var modifiers = new RunModifiers(sc.Catalog);
                long sold = 0;
                for (int step = 0; step < 200; step++)
                {
                    string context = "step " + step;
                    Assert.That(FactoryValuation.TryCompute(sc.Catalog, sc.State, sc.Permanent, out FactoryValueBreakdown before), Is.True);
                    long cash = sc.State.Cash;
                    long[] units = RunSimHarness.Units(sc.State);
                    run.StepOk(1_000_000 + step * 77_777);
                    RunSimHarness.AssertFlow(run.Sim, units, sc.State, context);

                    // Cash moves only by sales; each sale is SalePricing of the sold units, floored once per sale, so the step total is at most the
                    // price of all sold units at once and below it by less than 1 per sale. At the neutral multiplier it is exact.
                    long exact = 0, soldUnits = 0, sales = 0;
                    foreach (ResourceDef resource in sc.Catalog.Resources.Values)
                    {
                        long n = run.Sim.LastSoldUnits(resource.Id);
                        if (n == 0) continue;
                        Assert.That(modifiers.TrySalePricePpm(sc.State, sc.Permanent, resource.Id, out long ppm), Is.True);
                        Assert.That(SalePricing.TryValue(n, resource.BasePrice, ppm, out long value), Is.True);
                        exact += value;
                        soldUnits += n;
                        sales += run.Sim.LastTimestamps;
                        if (ppm == SpeedComposer.Ppm) Assert.That(value, Is.EqualTo(n * resource.BasePrice), context + ": neutral multiplier");
                    }
                    long gained = sc.State.Cash - cash;
                    Assert.That(gained, Is.LessThanOrEqualTo(exact), context + ": cash gain at most the one-sale price");
                    Assert.That(gained, Is.GreaterThanOrEqualTo(exact - sales), context + ": floor loss below 1 per sale");
                    sold += soldUnits;

                    // Valuation uses the same function: the stock is worth what selling it now would pay, and with the neutral multiplier a sale
                    // moves value from stock to Cash without changing the Factory Value (it changes only by produced minus consumed at base price).
                    Assert.That(FactoryValuation.TryCompute(sc.Catalog, sc.State, sc.Permanent, out FactoryValueBreakdown after), Is.True);
                    long stockValue = 0, flow = 0;
                    foreach (ResourceDef resource in sc.Catalog.Resources.Values)
                    {
                        Assert.That(modifiers.TrySalePricePpm(sc.State, sc.Permanent, resource.Id, out long ppm), Is.True);
                        Assert.That(SalePricing.TryValue(sc.State.Inventory.Units(resource.Id), resource.BasePrice, ppm, out long value), Is.True);
                        stockValue += value;
                        flow += (run.Sim.LastProducedUnits(resource.Id) - run.Sim.LastConsumedUnits(resource.Id)) * resource.BasePrice;
                    }
                    Assert.That(after.StockValue, Is.EqualTo(stockValue), context + ": valuation equals SalePricing of the stock");
                    Assert.That(after.Total - before.Total, Is.EqualTo(flow), context + ": sales are value neutral, only net production changes the value");
                }
                Assert.That(sold, Is.GreaterThan(0), "the scenario sells");
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Invariants over random scenarios
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void RandomScenarios_KeepEveryInvariant_AndNeverExceedNPlusOnePasses()
        {
            foreach (int seed in Seeds)
            {
                SimScenario sc = SimScenario.Random(seed, false, 0, false);
                var run = new SimRun(sc.Catalog, sc.State, sc.Permanent, sc.StartUtcMs);
                for (int step = 0; step < 8; step++)
                {
                    long[] before = RunSimHarness.Units(sc.State);
                    run.StepOk(5 * Second);
                    Assert.That(sc.State.Validate(), Is.EqualTo(RunStateResult.Ok), "seed " + seed + " step " + step);
                    Assert.That(run.Sim.LastMaxPassesPerTimestamp, Is.LessThanOrEqualTo(sc.State.Machines.Count + 1), "seed " + seed + " step " + step);
                    RunSimHarness.AssertFlow(run.Sim, before, sc.State, "seed " + seed + " step " + step);
                }
            }
        }
    }
}
