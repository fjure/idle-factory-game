using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// Unlock evaluation (T140, FR-013, research "Unlocks" owner decision of 2026-09-30): the four condition kinds, every
    /// family (resources, machine types, contract offers, Overdrive), chains that resolve at one instant, the command
    /// settle, monotonic growth, equivalence of online, offline and random partitions and a tick-by-tick oracle, atomic
    /// rejection, the StructureRevision bump and the allocation-free warm advance.
    /// </summary>
    public sealed class RunUnlockTests
    {
        private const long Second = RunSimHarness.TicksPerSecond;
        private const long MinerCycle = 20_000_000;
        private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        private static readonly IReadOnlyList<BoundaryEvent> NoEvents = new BoundaryEvent[0];

        // ---------------------------------------------------------------------------------------------
        // Builders
        // ---------------------------------------------------------------------------------------------

        private static UnlockCondition Research(string id) => new UnlockCondition("researchPurchased", id);
        private static UnlockCondition Level(int level) => new UnlockCondition("factoryLevelAtLeast", null, level);
        private static UnlockCondition Produced(string id) => new UnlockCondition("resourceProduced", id);

        private static RunCatalog Catalog(Dictionary<string, UnlockCondition> unlocks, bool lab = false, int factoryLevels = 3) =>
            RunSimHarness.Catalog(new RunSimHarness.CatalogOptions { Unlocks = unlocks, LabRecipes = lab, FactoryLevels = factoryLevels });

        private static SimScenario Scenario(RunCatalog catalog, RunState state) =>
            new SimScenario { Catalog = catalog, State = state, Permanent = new PermanentState(catalog) };

        // ---------------------------------------------------------------------------------------------
        // The four kinds, in every family
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void Always_UnlocksEveryFamilyAtTheFirstTimestamp_InAHashIndependentOrder()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState s = RunSimHarness.NewStateWith(catalog, new[] { "iron.ore", "machine.miner" });
            var run = new SimRun(catalog, s);

            run.StepOk(0);

            Assert.That(s.Unlocked, Is.EqualTo(new[]
            {
                "iron.ore", "machine.miner",
                "iron.bar", "iron.gear", "iron.plate",
                "machine.assembler", "machine.press", "machine.smelter",
                "contract.timed-bar", RunState.OverdriveUnlockId,
            }), "resources, machine types, offers and Overdrive, each family in ordinal ID order");
            Assert.That(run.Sim.LastUnlockCount, Is.EqualTo(8));
            foreach (string id in RunSimHarness.AllResources) Assert.That(s.Inventory.IsAllowed(id), Is.True, id + " is allowed in the inventory");
            Assert.That(s.Unlocked, Has.None.StartsWith("research."), "Research has no unlock condition: it is never evaluated");
        }

        [Test]
        public void ResearchPurchased_HoldsFromLevelOne_ForResourcesMachinesAndOverdrive()
        {
            RunCatalog catalog = Catalog(new Dictionary<string, UnlockCondition>
            {
                ["iron.bar"] = Research("research.a-smelter"),
                ["machine.smelter"] = Research("research.a-smelter"),
                [RunState.OverdriveUnlockId] = Research("research.b-all"),
            });
            RunState s = RunSimHarness.NewStateWith(catalog, RunSimHarness.AllUnlockedExcept(catalog, "iron.bar", "machine.smelter", RunState.OverdriveUnlockId));
            var run = new SimRun(catalog, s);

            run.StepOk(0);
            Assert.That(run.Sim.LastUnlockCount, Is.EqualTo(0), "level 0 does not hold");
            Assert.That(s.IsUnlocked("iron.bar") || s.IsUnlocked("machine.smelter") || s.IsUnlocked(RunState.OverdriveUnlockId), Is.False);

            Assert.That(s.TrySetResearchLevel("research.a-smelter", 1), Is.EqualTo(RunStateResult.Ok));
            run.StepOk(0);

            Assert.That(s.IsUnlocked("iron.bar"), Is.True);
            Assert.That(s.IsUnlocked("machine.smelter"), Is.True);
            Assert.That(s.Inventory.IsAllowed("iron.bar"), Is.True);
            Assert.That(s.IsUnlocked(RunState.OverdriveUnlockId), Is.False, "another Research");

            Assert.That(s.TrySetResearchLevel("research.b-all", 3), Is.EqualTo(RunStateResult.Ok));
            run.StepOk(1);
            Assert.That(s.IsUnlocked(RunState.OverdriveUnlockId), Is.True);
        }

        [Test]
        public void FactoryLevelAtLeast_HoldsAtTheLevelAndAbove()
        {
            RunCatalog catalog = Catalog(new Dictionary<string, UnlockCondition>
            {
                ["iron.gear"] = Level(2),
                ["machine.assembler"] = Level(3),
                ["contract.timed-bar"] = Level(2),
            });
            string[] locked = { "iron.gear", "machine.assembler", "contract.timed-bar" };

            RunState level1 = RunSimHarness.NewStateWith(catalog, RunSimHarness.AllUnlockedExcept(catalog, locked), factoryLevel: 1);
            new SimRun(catalog, level1).StepOk(0);
            Assert.That(locked.Any(level1.IsUnlocked), Is.False, "level 1 opens nothing");

            RunState level2 = RunSimHarness.NewStateWith(catalog, RunSimHarness.AllUnlockedExcept(catalog, locked), factoryLevel: 2);
            new SimRun(catalog, level2).StepOk(0);
            Assert.That(level2.IsUnlocked("iron.gear"), Is.True);
            Assert.That(level2.IsUnlocked("contract.timed-bar"), Is.True);
            Assert.That(level2.IsUnlocked("machine.assembler"), Is.False, "level 3 is still ahead");

            RunState level3 = RunSimHarness.NewStateWith(catalog, RunSimHarness.AllUnlockedExcept(catalog, locked), factoryLevel: 3);
            new SimRun(catalog, level3).StepOk(0);
            Assert.That(locked.All(level3.IsUnlocked), Is.True, "a higher level satisfies a lower requirement");
        }

        [Test]
        public void ResourceProduced_HoldsFromTheFirstCompletionThatAddsUnits_AndNotFromStockAlone()
        {
            RunCatalog catalog = Catalog(new Dictionary<string, UnlockCondition>
            {
                ["iron.bar"] = Produced("iron.ore"),
                ["machine.smelter"] = Produced("iron.ore"),
            });
            string[] locked = { "iron.bar", "machine.smelter" };

            RunState s = RunSimHarness.NewStateWith(catalog, RunSimHarness.AllUnlockedExcept(catalog, locked));
            RunSimHarness.Add(s, "machine.miner", 0, 1);
            var run = new SimRun(catalog, s);
            run.StepOk(MinerCycle - 1);
            Assert.That(locked.Any(s.IsUnlocked), Is.False, "the first Miner cycle has not completed");
            run.StepOk(1);
            Assert.That(locked.All(s.IsUnlocked), Is.True, "unlocked at the instant of the first completion");
            Assert.That(s.Inventory.IsAllowed("iron.bar"), Is.True);
            Assert.That(run.Sim.LastUnlockCount, Is.EqualTo(2));

            // Stock that was imported is not produced: no completion, no unlock.
            RunState stocked = RunSimHarness.NewStateWith(catalog, RunSimHarness.AllUnlockedExcept(catalog, locked));
            RunSimHarness.Stock(stocked, "iron.ore", 50);
            new SimRun(catalog, stocked).StepOk(30 * Second);
            Assert.That(locked.Any(stocked.IsUnlocked), Is.False);
        }

        [Test]
        public void ResourceProduced_CountsOnlyCompletionsOfTheCurrentRun()
        {
            // Produced flags live in the advance and reach the state only as unlocks: a fresh run state (prestige) starts
            // with the initial set and needs its own first completion.
            RunCatalog catalog = Catalog(new Dictionary<string, UnlockCondition> { ["iron.bar"] = Produced("iron.ore") });
            RunState first = RunSimHarness.NewStateWith(catalog, RunSimHarness.AllUnlockedExcept(catalog, "iron.bar"));
            RunSimHarness.Add(first, "machine.miner", 0, 1);
            new SimRun(catalog, first).StepOk(MinerCycle);
            Assert.That(first.IsUnlocked("iron.bar"), Is.True);

            RunState second = RunSimHarness.NewStateWith(catalog, RunSimHarness.AllUnlockedExcept(catalog, "iron.bar"));
            new SimRun(catalog, second).StepOk(0);
            Assert.That(second.IsUnlocked("iron.bar"), Is.False);
        }

        // ---------------------------------------------------------------------------------------------
        // Chains resolve at one instant
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void ChainedUnlocks_ResolveAtOneInstant_AndAWaitingMachineUsesTheNewResourceAtOnce()
        {
            // Bar opens at once (level 1); the Multiplier waits at full progress for it, makes 3 Bars from 1 Ore, which
            // opens Plate; the Press waits at full progress for Plate, makes one, which opens Gear.
            RunCatalog catalog = Catalog(new Dictionary<string, UnlockCondition>
            {
                ["iron.bar"] = Level(1),
                ["iron.plate"] = Produced("iron.bar"),
                ["iron.gear"] = Produced("iron.plate"),
            }, lab: true);
            RunState s = RunSimHarness.NewStateWith(catalog, RunSimHarness.AllUnlockedExcept(catalog, "iron.bar", "iron.plate", "iron.gear"));
            RunSimHarness.Add(s, "machine.multiplier", 0, 1, progress: 20_000_000);
            RunSimHarness.Add(s, "machine.press", 1, 2, progress: 40_000_000);
            RunSimHarness.Add(s, "machine.assembler", 2, 3, progress: 50_000_000);
            RunSimHarness.Stock(s, "iron.ore", 5);
            var run = new SimRun(catalog, s);

            run.StepOk(0);

            Assert.That(run.Sim.LastTimestamps, Is.EqualTo(1), "one processed instant");
            Assert.That(s.Unlocked.Skip(s.Unlocked.Count - 3), Is.EqualTo(new[] { "iron.bar", "iron.plate", "iron.gear" }), "in the order they resolve");
            Assert.That(run.Sim.LastUnlockCount, Is.EqualTo(3));
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(4));
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(1), "3 made, 2 pressed");
            Assert.That(s.Inventory.Units("iron.plate"), Is.EqualTo(1));
            Assert.That(s.Inventory.Units("iron.gear"), Is.EqualTo(0), "the Assembler needs two Plates");
            Assert.That(run.Machine(1).ProgressTicks, Is.EqualTo(0), "the Multiplier restarted");
            Assert.That(run.Machine(2).ProgressTicks, Is.EqualTo(0), "the Press restarted");
            Assert.That(run.Sim.LastProducedUnits("iron.bar"), Is.EqualTo(3), "flow accounting follows the inventory reshape");
            Assert.That(run.Sim.LastConsumedUnits("iron.bar"), Is.EqualTo(2));
            Assert.That(run.Sim.LastProducedUnits("iron.plate"), Is.EqualTo(1));

            // The result is a fixed point: another instant at the same time changes nothing.
            string fingerprint = RunSimHarness.Fingerprint(s);
            run.StepOk(0);
            Assert.That(RunSimHarness.Fingerprint(s), Is.EqualTo(fingerprint));
            Assert.That(run.Sim.LastUnlockCount, Is.EqualTo(0));
        }

        [Test]
        public void UnlockedResource_IsAllowedAtTheInstantOfTheUnlock_InThePublishedInventory()
        {
            RunCatalog catalog = Catalog(new Dictionary<string, UnlockCondition> { ["iron.bar"] = Produced("iron.ore") });
            RunState s = RunSimHarness.NewStateWith(catalog, RunSimHarness.AllUnlockedExcept(catalog, "iron.bar"));
            RunSimHarness.Add(s, "machine.miner", 0, 1);
            RunSimHarness.Add(s, "machine.smelter", 1, 2);
            var run = new SimRun(catalog, s);
            Assert.That(s.Inventory.IsAllowed("iron.bar"), Is.False);

            // Miner completes at 2 s, the Smelter (it waits for the locked Bar) starts at that instant.
            run.StepOk(MinerCycle);
            Assert.That(s.Inventory.IsAllowed("iron.bar"), Is.True);
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(2));

            run.StepOk(20_000_000);
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(1), "the Smelter completed one cycle after the unlock");
        }

        // ---------------------------------------------------------------------------------------------
        // Command settle and StructureRevision
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void UpgradeFactory_UnlocksAtTheCommandSettle_ThroughFactoryLevelAtLeast()
        {
            var options = new RunCommandFixtures.Options
            {
                Unlocks = new Dictionary<string, UnlockCondition>
                {
                    [RunCommandFixtures.Press] = Level(2),
                    ["iron.plate"] = Level(2),
                    ["iron.gear"] = Produced("iron.plate"),
                },
            };
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(options, cash: 100_000,
                unlockedMachines: new[] { RunCommandFixtures.Miner, RunCommandFixtures.Smelter },
                lockedResources: new[] { "iron.plate", "iron.gear" });
            Assert.That(rig.Commands.Build(2, RunCommandFixtures.Press).ReasonCode, Is.EqualTo(RunReasons.MachineLocked));
            Assert.That(rig.State.Inventory.IsAllowed("iron.plate"), Is.False);
            long structure = rig.Session.StructureRevision;

            var upgrade = rig.Commands.UpgradeFactory();

            Assert.That(upgrade.IsSuccess, Is.True, upgrade.ReasonCode);
            Assert.That(rig.State.FactoryLevel, Is.EqualTo(2));
            Assert.That(rig.State.IsUnlocked(RunCommandFixtures.Press), Is.True, "unlocked at the settle of the command");
            Assert.That(rig.State.IsUnlocked("iron.plate"), Is.True);
            Assert.That(rig.State.Inventory.IsAllowed("iron.plate"), Is.True);
            Assert.That(rig.State.IsUnlocked("iron.gear"), Is.False, "no Plate has been produced");
            Assert.That(rig.Session.StructureRevision, Is.GreaterThan(structure));
            Assert.That(rig.Commands.Build(2, RunCommandFixtures.Press).IsSuccess, Is.True, "the build sheet sees the unlock at once");
        }

        [Test]
        public void StructureRevision_BumpsWhenTheUnlockedSetGrows_AndOnlyThen()
        {
            var options = new RunCommandFixtures.Options
            {
                DiscountResearch = true,
                Unlocks = new Dictionary<string, UnlockCondition> { [RunCommandFixtures.Smelter] = Research(RunCommandFixtures.MachineDiscount) },
            };
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(options, unlockedMachines: new[] { RunCommandFixtures.Miner, RunCommandFixtures.Press, RunCommandFixtures.Assembler });
            Assert.That(rig.Session.Advance(10_000).IsSuccess, Is.True);
            long structure = rig.Session.StructureRevision;

            Assert.That(rig.Session.Advance(10_000).IsSuccess, Is.True);
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure), "nothing unlocked, nothing structural");

            Assert.That(rig.State.TrySetResearchLevel(RunCommandFixtures.MachineDiscount, 1), Is.EqualTo(RunStateResult.Ok));
            Assert.That(rig.Session.Advance(10_000).IsSuccess, Is.True);
            Assert.That(rig.State.IsUnlocked(RunCommandFixtures.Smelter), Is.True);
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure + 1), "the unlocked set grew");

            Assert.That(rig.Session.Advance(10_000).IsSuccess, Is.True);
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure + 1), "and it does not bump again");
        }

        [Test]
        public void StructureRevision_BumpsForAnUnlockByProduction()
        {
            var options = new RunCommandFixtures.Options
            {
                Unlocks = new Dictionary<string, UnlockCondition> { ["iron.bar"] = Produced("iron.ore") },
            };
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(options, lockedResources: new[] { "iron.bar", "iron.plate", "iron.gear" });
            Assert.That(rig.Commands.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            long structure = rig.Session.StructureRevision;
            long quantity = rig.Session.QuantityRevision;

            Assert.That(rig.Session.Advance(19_000_000).IsSuccess, Is.True);
            Assert.That(rig.State.IsUnlocked("iron.bar"), Is.False);
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure));
            Assert.That(rig.Session.Advance(1_000_000).IsSuccess, Is.True);
            Assert.That(rig.State.IsUnlocked("iron.bar"), Is.True);
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure + 1));
            Assert.That(rig.Session.QuantityRevision, Is.GreaterThan(quantity));
        }

        // ---------------------------------------------------------------------------------------------
        // Monotonic
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void TheUnlockedSet_OnlyGrows_EvenWhenTheConditionStopsHolding()
        {
            RunCatalog catalog = Catalog(new Dictionary<string, UnlockCondition>
            {
                ["iron.bar"] = Research("research.a-smelter"),
                ["machine.press"] = Level(2),
            });
            RunState s = RunSimHarness.NewStateWith(catalog, RunSimHarness.AllUnlockedExcept(catalog, "iron.bar", "machine.press"), factoryLevel: 2);
            Assert.That(s.TrySetResearchLevel("research.a-smelter", 2), Is.EqualTo(RunStateResult.Ok));
            var run = new SimRun(catalog, s);
            run.StepOk(0);
            Assert.That(s.IsUnlocked("iron.bar") && s.IsUnlocked("machine.press"), Is.True);
            string[] unlocked = s.Unlocked.ToArray();

            Assert.That(s.TrySetResearchLevel("research.a-smelter", 0), Is.EqualTo(RunStateResult.Ok));
            run.StepOk(10 * Second);

            Assert.That(s.Unlocked, Is.EqualTo(unlocked));
            Assert.That(s.Inventory.IsAllowed("iron.bar"), Is.True);
        }

        [Test]
        public void UnreachableConditions_StayLocked_WithoutAnInventedRule()
        {
            // Gear opens only after a Gear was produced, but the Assembler cannot complete while the Gear is locked.
            RunCatalog catalog = Catalog(new Dictionary<string, UnlockCondition> { ["iron.gear"] = Produced("iron.gear") });
            RunState s = RunSimHarness.NewStateWith(catalog, RunSimHarness.AllUnlockedExcept(catalog, "iron.gear"));
            RunSimHarness.Add(s, "machine.assembler", 0, 1, progress: 50_000_000);
            RunSimHarness.Stock(s, "iron.plate", 10);
            var run = new SimRun(catalog, s);

            run.StepOk(100 * Second);

            Assert.That(s.IsUnlocked("iron.gear"), Is.False);
            Assert.That(s.Inventory.Units("iron.plate"), Is.EqualTo(10));
            Assert.That(run.Sim.LastCompletions, Is.EqualTo(0));
        }

        // ---------------------------------------------------------------------------------------------
        // Atomic rejection
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void AnOverflowingAdvance_ThatWouldHaveUnlocked_RejectsAtomically()
        {
            RunCatalog catalog = Catalog(new Dictionary<string, UnlockCondition>
            {
                ["iron.bar"] = Produced("iron.ore"),
                ["machine.smelter"] = Produced("iron.ore"),
            });
            RunState s = RunSimHarness.NewStateWith(catalog, RunSimHarness.AllUnlockedExcept(catalog, "iron.bar", "machine.smelter"), ExactBound.Value - 20);
            RunSimHarness.Add(s, "machine.miner", 0, 1);
            Assert.That(s.TrySetAutoSell("iron.ore", true), Is.EqualTo(RunStateResult.Ok));
            var run = new SimRun(catalog, s);
            string before = RunSimHarness.Fingerprint(s);

            // The first completion (2 s) would unlock; the eleventh sale (22 s) overflows Cash.
            Assert.That(run.Step(30 * Second), Is.EqualTo(RunAdvanceResult.ArithmeticOverflow));

            Assert.That(RunSimHarness.Fingerprint(s), Is.EqualTo(before), "nothing was published, unlocks included");
            Assert.That(s.IsUnlocked("iron.bar") || s.IsUnlocked("machine.smelter"), Is.False);
            Assert.That(s.Inventory.IsAllowed("iron.bar"), Is.False);
            Assert.That(run.Sim.LastUnlockCount, Is.EqualTo(0));

            // The same simulator then runs a smaller advance from the untouched state and unlocks.
            run.StepOk(5 * Second);
            Assert.That(s.IsUnlocked("iron.bar") && s.IsUnlocked("machine.smelter"), Is.True);
            Assert.That(s.Inventory.IsAllowed("iron.bar"), Is.True);
        }

        // ---------------------------------------------------------------------------------------------
        // Equivalence: partitions, offline segmentation, definition order and the oracle
        // ---------------------------------------------------------------------------------------------

        private static string Run(SimScenario sc, long total, IEnumerable<long> cuts = null)
        {
            var sim = new RunSimulator(sc.Catalog);
            RunSimHarness.RunCut(sim, sc, total, cuts ?? Enumerable.Empty<long>());
            return RunSimHarness.Fingerprint(sc.State, sc.Permanent);
        }

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

        [Test]
        public void RandomPartitions_UnlockTheSameSetInTheSameOrder_AsOneAdvance()
        {
            int unlockedSomething = 0, fullyUnlocked = 0;
            foreach (int seed in Seeds)
            {
                SimScenario single = SimScenario.Random(seed, false, 0, false, unlocks: true);
                string expected = Run(single, 40 * Second);
                if (single.State.Unlocked.Count > 3) unlockedSomething++;
                if (single.State.IsUnlocked("iron.gear")) fullyUnlocked++;
                var rng = new Random(seed * 7919 + 13);
                for (int p = 0; p < 4; p++)
                {
                    SimScenario partitioned = SimScenario.Random(seed, false, 0, false, unlocks: true);
                    List<long> cuts = Cuts(partitioned, 40 * Second, rng);
                    Assert.That(Run(partitioned, 40 * Second, cuts), Is.EqualTo(expected),
                        "seed " + seed + " partition " + p + " cuts [" + string.Join(", ", cuts) + "]");
                }
            }
            Assert.That(unlockedSomething, Is.GreaterThanOrEqualTo(Seeds.Length - 1), "the seeds unlock things");
            Assert.That(fullyUnlocked, Is.GreaterThan(0), "and some run the whole Ore, Bar, Plate, Gear chain");
        }

        [Test]
        public void EverySplit_AroundTheUnlockInstant_GivesTheSameStateAndTheSameInstant()
        {
            SimScenario Build()
            {
                RunCatalog catalog = Catalog(new Dictionary<string, UnlockCondition>
                {
                    ["iron.bar"] = Produced("iron.ore"),
                    ["machine.smelter"] = Produced("iron.ore"),
                    ["iron.plate"] = Produced("iron.bar"),
                });
                RunState s = RunSimHarness.NewStateWith(catalog, RunSimHarness.AllUnlockedExcept(catalog, "iron.bar", "machine.smelter", "iron.plate"));
                RunSimHarness.Add(s, "machine.miner", 0, 1);
                return Scenario(catalog, s);
            }

            var rng = new Random(77);
            foreach (long total in new[] { MinerCycle - 1, MinerCycle, MinerCycle + 1, 3 * MinerCycle + 7 })
            {
                string expected = Run(Build(), total);
                bool unlocked = total >= MinerCycle;
                SimScenario reference = Build();
                Run(reference, total);
                Assert.That(reference.State.IsUnlocked("iron.bar"), Is.EqualTo(unlocked), "total " + total);
                for (int p = 0; p < 6; p++)
                {
                    var cuts = Enumerable.Range(0, 1 + rng.Next(6)).Select(_ => 1 + (long)(rng.NextDouble() * (total - 1)))
                        .Concat(new[] { MinerCycle - 1, MinerCycle, MinerCycle + 1 }).Where(c => c > 0 && c < total).ToList();
                    Assert.That(Run(Build(), total, cuts), Is.EqualTo(expected), "total " + total + " cuts [" + string.Join(", ", cuts) + "]");
                }
                SimScenario stepped = Build();
                Assert.That(Run(stepped, total, Enumerable.Range(1, (int)Math.Min(total / 1_000_000, 60)).Select(i => i * 1_000_000L)), Is.EqualTo(expected),
                    "1/10 s steps, total " + total);
            }
        }

        [Test]
        public void TheUnlockedSet_GrowsMonotonically_AcrossEveryStepOfARun()
        {
            foreach (int seed in Seeds)
            {
                SimScenario sc = SimScenario.Random(seed, false, 0, false, unlocks: true);
                var sim = new RunSimulator(sc.Catalog);
                string[] previous = sc.State.Unlocked.ToArray();
                for (int step = 0; step < 40; step++)
                {
                    Assert.That(RunSimHarness.AdvanceAt(sim, sc.State, sc.Permanent, sc.StartUtcMs, step * Second, Second), Is.EqualTo(RunAdvanceResult.Ok));
                    string[] now = sc.State.Unlocked.ToArray();
                    Assert.That(now.Take(previous.Length), Is.EqualTo(previous), "seed " + seed + " step " + step + ": earlier unlocks keep their place");
                    previous = now;
                }
            }
        }

        [Test]
        public void DefinitionOrderPermutations_GiveTheIdenticalUnlockedList()
        {
            foreach (int seed in Seeds.Take(8))
            {
                string expected = Run(SimScenario.Random(seed, false, 0, false, unlocks: true), 40 * Second);
                foreach (int orderSeed in new[] { 11, 12, 13 })
                    Assert.That(Run(SimScenario.Random(seed, false, orderSeed, false, unlocks: true), 40 * Second), Is.EqualTo(expected),
                        "seed " + seed + " definition order " + orderSeed);
            }
        }

        private const long OfflineCapTicks = 288_000_000_000L;

        private static long E(long ticksSinceDeparture) => Math.Min(Math.Max(ticksSinceDeparture, 0), OfflineCapTicks) * 4 / 5;

        private static List<BoundaryEvent> MappedEvents(RunState state, long departureUtcMs, long realTicks)
        {
            var events = new List<BoundaryEvent>();
            foreach (BoundaryEvent real in RunSimHarness.Events(state, departureUtcMs, 0, long.MaxValue / 4))
            {
                if (real.SimTicks > realTicks) continue;
                events.Add(new BoundaryEvent(E(real.SimTicks), real.Kind, real.Id, real.UtcMs));
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
            long realNow = sc.StartUtcMs;
            foreach (long cut in cuts)
            {
                var segment = all.Where(e => at == 0 ? e.SimTicks <= cut : e.SimTicks > at && e.SimTicks <= cut).ToList();
                var rebased = segment.Select(e => new BoundaryEvent(e.SimTicks - at, e.Kind, e.Id, e.UtcMs)).ToList();
                Assert.That(sim.Advance(sc.State, sc.Permanent, cut - at, realNow, rebased), Is.EqualTo(RunAdvanceResult.Ok));
                foreach (BoundaryEvent e in segment) realNow = Math.Max(realNow, e.UtcMs);
                at = cut;
            }
            return RunSimHarness.Fingerprint(sc.State, sc.Permanent);
        }

        [Test]
        public void OfflineSegmentation_UnlocksAtTheSameSimulatedInstant_AsTheOnlineAdvance()
        {
            const long realTicks = 60 * Second;
            foreach (int seed in Seeds)
            {
                string online = OfflineOnline(SimScenario.Random(seed, false, 0, false, unlocks: true), realTicks);
                string segmented = OfflineSegmented(SimScenario.Random(seed, false, 0, false, unlocks: true), realTicks, 7 * Second);
                Assert.That(segmented, Is.EqualTo(online), "seed " + seed);
            }
        }

        [Test]
        public void TheSimulator_MatchesTheTickByTickOracle_OnUnlockScenarios()
        {
            const long total = 5_000_000;
            int unlocks = 0;
            foreach (int seed in Seeds)
            {
                SimScenario forSim = SimScenario.Random(seed, true, 0, false, unlocks: true);
                SimScenario forOracle = SimScenario.Random(seed, true, 0, false, unlocks: true);
                var sim = new RunSimulator(forSim.Catalog);
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
                unlocks += sim.LastUnlockCount;
            }
            Assert.That(unlocks, Is.GreaterThan(Seeds.Length), "the oracle seeds unlock things");
        }

        // ---------------------------------------------------------------------------------------------
        // Allocation
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void WarmedAdvance_HasZeroGcAllocSamples_WhileConditionsStayLocked()
        {
            RunCatalog catalog = Catalog(new Dictionary<string, UnlockCondition>
            {
                ["iron.gear"] = Produced("iron.gear"),
                ["machine.assembler"] = Research("research.a-smelter"),
                ["machine.press"] = Level(3),
            });
            RunState s = RunSimHarness.NewStateWith(catalog, RunSimHarness.AllUnlockedExcept(catalog, "iron.gear", "machine.assembler", "machine.press"), 1_000);
            RunSimHarness.Add(s, "machine.miner", 0, 1);
            RunSimHarness.Add(s, "machine.miner", 1, 2);
            RunSimHarness.Add(s, "machine.smelter", 2, 3);
            RunSimHarness.Add(s, "machine.press", 3, 4);
            Assert.That(s.TrySetAutoSell("iron.plate", true), Is.EqualTo(RunStateResult.Ok));
            var sim = new RunSimulator(catalog);
            long now = RunSimHarness.StartUtcMs;

            for (int i = 0; i < 40; i++)
                Assert.That(sim.Advance(s, null, 10_000_000L, now, NoEvents), Is.EqualTo(RunAdvanceResult.Ok));
            int locked = s.Unlocked.Count;

            var counts = new int[6];
            for (int i = 0; i < counts.Length; i++)
            {
                long ticks = i < 4 ? 30_000_000L : 600_000_000L;
                RunAdvanceResult result = RunAdvanceResult.InvalidArgument;
                counts[i] = GcAllocationCounter.Count(() => result = sim.Advance(s, null, ticks, now, NoEvents));
                Assert.That(result, Is.EqualTo(RunAdvanceResult.Ok));
                Assert.That(sim.LastCompletions, Is.GreaterThan(0), "advance " + i + " did real work");
                Assert.That(sim.LastUnlockCount, Is.EqualTo(0));
            }

            Assert.That(counts, Is.All.Zero, "GC.Alloc samples per warmed Advance with locked candidates and nothing new unlocking");
            Assert.That(s.Unlocked.Count, Is.EqualTo(locked));
            Assert.That(s.IsUnlocked("iron.gear"), Is.False);
        }

        [Test]
        public void WarmedSessionAdvance_HasZeroGcAllocSamples_WhileConditionsStayLocked()
        {
            var options = new RunCommandFixtures.Options
            {
                DiscountResearch = true,
                Unlocks = new Dictionary<string, UnlockCondition>
                {
                    [RunCommandFixtures.Assembler] = Research(RunCommandFixtures.MachineDiscount),
                    ["iron.gear"] = Produced("iron.gear"),
                },
            };
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(options, cash: 100_000,
                unlockedMachines: new[] { RunCommandFixtures.Miner, RunCommandFixtures.Smelter, RunCommandFixtures.Press },
                lockedResources: new[] { "iron.gear" });
            Assert.That(rig.Commands.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            Assert.That(rig.Commands.Build(1, RunCommandFixtures.Smelter).IsSuccess, Is.True);
            for (int i = 0; i < 40; i++) rig.Advance(10_000_000L);
            int count = rig.State.Unlocked.Count;

            var counts = new int[4];
            for (int i = 0; i < counts.Length; i++)
                counts[i] = GcAllocationCounter.Count(() => rig.Session.Advance(10_000_000L));

            Assert.That(counts, Is.All.Zero);
            Assert.That(rig.State.Unlocked.Count, Is.EqualTo(count));
        }
    }
}
