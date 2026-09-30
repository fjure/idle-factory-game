using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// Hand-computed cases of <see cref="RunSimulator"/> (T098): the literal FR-002 chain, priority contention,
    /// full stores, OD-2 retries, waiting machines, mid-cycle changes that preserve the completed fraction, batched
    /// cycles (OD-6), partial batches (OD-9), expiry ordering, overflow atomicity and argument checks. The
    /// randomized, partition and oracle checks live in <see cref="RunSimulationEquivalenceTests"/>.
    /// </summary>
    public sealed class RunSimulatorTests
    {
        private const long Second = RunSimHarness.TicksPerSecond;

        private static SimRun Start(RunCatalog catalog, out RunState state, long cash = 25, int storageLevel = 1)
        {
            state = RunSimHarness.NewState(catalog, cash, storageLevel);
            return new SimRun(catalog, state);
        }

        private static long[] Units(RunState s) => Enumerable.Range(0, s.Inventory.Count).Select(i => s.Inventory.UnitsAt(i)).ToArray();

        /// <summary>Stock change equals what the completions added minus what they consumed and the sales removed.</summary>
        private static void AssertFlow(RunSimulator sim, long[] before, RunState after, string context)
        {
            for (int i = 0; i < after.Inventory.Count; i++)
            {
                string id = after.Inventory.IdAt(i);
                long expected = before[i] + sim.LastProducedUnits(id) - sim.LastConsumedUnits(id) - sim.LastSoldUnits(id);
                Assert.That(after.Inventory.UnitsAt(i), Is.EqualTo(expected), context + ": conservation of " + id);
            }
        }

        // ---------------------------------------------------------------------------------------------
        // FR-002 chain
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void LiteralFr002Chain_Miner_Smelter_Press_Assembler_MatchesTheHandTrace()
        {
            SimRun run = Start(RunSimHarness.Catalog(), out RunState s);
            RunSimHarness.Add(s, "machine.miner", 0, 1);
            RunSimHarness.Add(s, "machine.smelter", 1, 2);
            RunSimHarness.Add(s, "machine.press", 2, 3);
            RunSimHarness.Add(s, "machine.assembler", 3, 4);
            s.TrySetAutoSell("iron.gear", true);
            long[] before = Units(s);

            run.StepOk(19 * Second);

            // Miner completes every 2 s from 2 s, the Smelter from 4 s, the Press at 10, 14, 18 s, the Assembler
            // at 19 s (two Plates present since 14 s). One Gear is sold: 25 + 24.
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(2));
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(2));
            Assert.That(s.Inventory.Units("iron.plate"), Is.EqualTo(1));
            Assert.That(s.Inventory.Units("iron.gear"), Is.EqualTo(0));
            Assert.That(s.Cash, Is.EqualTo(49));
            Assert.That(run.Machine(1).ProgressTicks, Is.EqualTo(Second));
            Assert.That(run.Machine(2).ProgressTicks, Is.EqualTo(Second));
            Assert.That(run.Machine(3).ProgressTicks, Is.EqualTo(Second));
            Assert.That(run.Machine(4).ProgressTicks, Is.EqualTo(0));
            Assert.That(run.Sim.LastMaxPassesPerTimestamp, Is.LessThanOrEqualTo(5));
            AssertFlow(run.Sim, before, s, "FR-002 chain");
        }

        [Test]
        public void LiteralFr002Chain_AssemblerBottleneckIsFiveEighthsOfItsCapacity()
        {
            SimRun run = Start(RunSimHarness.Catalog(), out RunState s);
            RunSimHarness.Add(s, "machine.miner", 0, 1);
            RunSimHarness.Add(s, "machine.smelter", 1, 2);
            RunSimHarness.Add(s, "machine.press", 2, 3);
            RunSimHarness.Add(s, "machine.assembler", 3, 4);
            s.TrySetAutoSell("iron.gear", true);
            long gears = 0;

            for (int i = 0; i < 800; i++)
            {
                run.StepOk(Second);
                gears += run.Sim.LastSoldUnits("iron.gear");
            }

            // Plates arrive every 4 s from 10 s (0.25 per second); a Gear needs two, so the Assembler completes at
            // 19 s and then every 8 s, busy 5 s of every 8 s: 62.5 %.
            Assert.That(gears, Is.EqualTo(98), "Gears at 19, 27, ..., 795 s");
            Assert.That(s.Cash, Is.EqualTo(25 + 98 * 24));
        }

        // ---------------------------------------------------------------------------------------------
        // Priority, full store, OD-2
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void Priority_DecidesContention_RegardlessOfSlotOrBuildOrder()
        {
            foreach (bool priorityFirstIsInstance1 in new[] { true, false })
            foreach (bool instance1InLowSlot in new[] { true, false })
            foreach (bool instance1BuiltFirst in new[] { true, false })
            {
                SimRun run = Start(RunSimHarness.Catalog(), out RunState s);
                int slot1 = instance1InLowSlot ? 0 : 5, slot2 = instance1InLowSlot ? 5 : 0;
                if (instance1BuiltFirst)
                {
                    RunSimHarness.Add(s, "machine.smelter", slot1, 1, progress: 20_000_000);
                    RunSimHarness.Add(s, "machine.smelter", slot2, 2, progress: 20_000_000);
                }
                else
                {
                    RunSimHarness.Add(s, "machine.smelter", slot2, 2, progress: 20_000_000);
                    RunSimHarness.Add(s, "machine.smelter", slot1, 1, progress: 20_000_000);
                }
                if (priorityFirstIsInstance1) RunSimHarness.Reorder(s, 1, 2); else RunSimHarness.Reorder(s, 2, 1);
                RunSimHarness.Stock(s, "iron.ore", 2);

                run.StepOk(0);

                int winner = priorityFirstIsInstance1 ? 1 : 2, loser = 3 - winner;
                string context = $"priority {(priorityFirstIsInstance1 ? "1,2" : "2,1")} slot1={slot1} built1First={instance1BuiltFirst}";
                Assert.That(run.Machine(winner).ProgressTicks, Is.EqualTo(0), context + ": winner restarts");
                Assert.That(run.Machine(loser).ProgressTicks, Is.EqualTo(20_000_000), context + ": loser waits with full progress");
                Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(1), context);
                Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(0), context);
            }
        }

        [Test]
        public void FullStore_NetFittingSmelterCompletes_InputFreeMinerStaysBlocked()
        {
            SimRun run = Start(RunSimHarness.Catalog(), out RunState s);
            RunSimHarness.Add(s, "machine.miner", 0, 1, progress: 20_000_000);
            RunSimHarness.Add(s, "machine.smelter", 1, 2, progress: 20_000_000);
            RunSimHarness.Stock(s, "iron.ore", 1000);
            Assert.That(s.Inventory.Free, Is.EqualTo(0));
            long[] before = Units(s);

            run.StepOk(0);

            // The Smelter's exchange (-2 Ore, +1 Bar) nets -1 and fits at a full store; the Miner (+2) does not fit
            // in the single free unit and waits with full progress.
            Assert.That(run.Machine(2).ProgressTicks, Is.EqualTo(0));
            Assert.That(run.Machine(1).ProgressTicks, Is.EqualTo(20_000_000));
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(998));
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(1));
            Assert.That(s.Inventory.Total, Is.EqualTo(999));
            AssertFlow(run.Sim, before, s, "full store");

            // Two seconds later the Smelter has freed a second unit and the Miner completes in the retry pass.
            before = Units(s);
            run.StepOk(2 * Second);
            Assert.That(run.Machine(1).ProgressTicks, Is.EqualTo(0));
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(998));
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(2));
            Assert.That(s.Inventory.Total, Is.EqualTo(1000));
            Assert.That(run.Sim.LastMaxPassesPerTimestamp, Is.EqualTo(3));
            AssertFlow(run.Sim, before, s, "unblocked");
        }

        [Test]
        public void Od2_AutoSaleFreesCapacityAndTheBlockedMachineCompletesInTheSameTimestamp()
        {
            SimRun run = Start(RunSimHarness.Catalog(), out RunState s);
            RunSimHarness.Add(s, "machine.miner", 0, 1, progress: 20_000_000);
            RunSimHarness.Add(s, "machine.smelter", 1, 2, progress: 20_000_000);
            RunSimHarness.Stock(s, "iron.ore", 1000);
            s.TrySetAutoSell("iron.bar", true);

            run.StepOk(0);

            // Pass 1: Miner blocked, Smelter completes, the Bar is sold (+3). Pass 2: two units are free and the
            // Miner completes. Pass 3 completes nothing.
            Assert.That(run.Machine(1).ProgressTicks, Is.EqualTo(0));
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(1000));
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(0));
            Assert.That(s.Cash, Is.EqualTo(28));
            Assert.That(run.Sim.LastMaxPassesPerTimestamp, Is.EqualTo(3));
            Assert.That(run.Sim.LastCompletions, Is.EqualTo(2));
        }

        [Test]
        public void WaitingSmelterAheadOfTheMinerCompletesInTheRetryPass()
        {
            SimRun run = Start(RunSimHarness.Catalog(), out RunState s);
            RunSimHarness.Add(s, "machine.smelter", 0, 1, progress: 20_000_000);
            RunSimHarness.Add(s, "machine.miner", 1, 2, progress: 20_000_000);

            run.StepOk(0);

            Assert.That(run.Machine(1).ProgressTicks, Is.EqualTo(0));
            Assert.That(run.Machine(2).ProgressTicks, Is.EqualTo(0));
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(0));
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(1));
            Assert.That(run.Sim.LastMaxPassesPerTimestamp, Is.EqualTo(3));
        }

        [Test]
        public void ReversedChain_NeedsExactlyMachinesPlusOnePasses()
        {
            SimRun run = Start(RunSimHarness.Catalog(), out RunState s);
            RunSimHarness.Add(s, "machine.miner", 0, 1, progress: 20_000_000);
            RunSimHarness.Add(s, "machine.smelter", 1, 2, progress: 20_000_000);
            RunSimHarness.Add(s, "machine.press", 2, 3, progress: 40_000_000);
            RunSimHarness.Add(s, "machine.assembler", 3, 4, progress: 50_000_000);
            RunSimHarness.Reorder(s, 4, 3, 2, 1);
            RunSimHarness.Stock(s, "iron.bar", 1);
            RunSimHarness.Stock(s, "iron.plate", 1);

            run.StepOk(0);

            // Only the Miner can complete in pass 1; each later pass unlocks the next machine up the chain.
            Assert.That(run.Sim.LastCompletions, Is.EqualTo(4));
            Assert.That(run.Sim.LastMaxPassesPerTimestamp, Is.EqualTo(5), "N + 1");
            Assert.That(s.Inventory.Units("iron.gear"), Is.EqualTo(1));
        }

        [Test]
        public void PausedMachineKeepsItsProgress_WithoutBacklog()
        {
            SimRun run = Start(RunSimHarness.Catalog(), out RunState s);
            RunSimHarness.Add(s, "machine.smelter", 0, 1, progress: 5_000_000);

            run.StepOk(60 * Second);
            Assert.That(run.Machine(1).ProgressTicks, Is.EqualTo(5_000_000), "no input: paused, progress kept");
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(0));

            RunSimHarness.Stock(s, "iron.ore", 100);
            run.StepOk(15_000_000 - 1);
            Assert.That(run.Machine(1).ProgressTicks, Is.EqualTo(20_000_000 - 1));
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(0));
            run.StepOk(1);
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(1), "one cycle completes, not a backlog of 30");
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(98));
        }

        [Test]
        public void InactiveMachineNeitherProgressesNorCompletes()
        {
            SimRun run = Start(RunSimHarness.Catalog(), out RunState s);
            MachineInstance miner = RunSimHarness.Add(s, "machine.miner", 0, 1, progress: 1_000_000, active: false);
            RunSimHarness.Add(s, "machine.miner", 1, 2, progress: 20_000_000, active: false);

            run.StepOk(30 * Second);

            Assert.That(miner.ProgressTicks, Is.EqualTo(1_000_000));
            Assert.That(run.Machine(2).ProgressTicks, Is.EqualTo(20_000_000), "a waiting inactive machine does not complete");
            Assert.That(s.Inventory.Total, Is.EqualTo(0));
            miner.SetActive(true);
            run.StepOk(19_000_000);
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(2));
        }

        [Test]
        public void RecipeWithALockedResourceStaysWaiting()
        {
            // T140: a resource missing from the unlocked set is now unlocked by the simulator when its condition holds,
            // so the Gear needs a condition that does not hold (Research at level 0) to stay locked.
            RunCatalog catalog = RunSimHarness.Catalog(new RunSimHarness.CatalogOptions
            {
                Unlocks = new Dictionary<string, UnlockCondition> { ["iron.gear"] = new UnlockCondition("researchPurchased", "research.a-smelter") },
            });
            var unlocked = new[] { "iron.ore", "iron.bar", "iron.plate", "machine.assembler" };
            var s = new RunState(catalog, RunSimHarness.Id(9000), 1, null, 25, 0, 1, 1, unlocked);
            s.TryAddMachine(new MachineInstance(RunSimHarness.Id(1), catalog.Machines["machine.assembler"], 0, 1, true, 50_000_000, 50_000_000, 0));
            RunSimHarness.Stock(s, "iron.plate", 10);
            var run = new SimRun(catalog, s);

            run.StepOk(10 * Second);

            Assert.That(run.Machine(1).ProgressTicks, Is.EqualTo(50_000_000), "the Gear is locked: the exchange cannot happen");
            Assert.That(s.Inventory.Units("iron.plate"), Is.EqualTo(10));
        }

        // ---------------------------------------------------------------------------------------------
        // Mid-cycle changes keep the completed fraction
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void MidCycleUpgrade_PreservesTheFractionAndCompletesOnTheRescaledSchedule()
        {
            SimRun run = Start(RunSimHarness.Catalog(), out RunState s);
            MachineInstance miner = RunSimHarness.Add(s, "machine.miner", 0, 1, progress: 10_000_000);
            // Level 2 speed 1.08x: cycle ceil(2e7 x 1e6 / 1.08e6) = 18,518,519; progress floor(1e7 x 18,518,519 / 2e7).
            Assert.That(s.TryUpgradeMachine(miner.InstanceId, 12, 18_518_519), Is.EqualTo(RunStateResult.Ok));
            Assert.That(miner.ProgressTicks, Is.EqualTo(9_259_259));

            run.StepOk(9_259_259);
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(0));
            Assert.That(miner.ProgressTicks, Is.EqualTo(18_518_518));
            run.StepOk(1);
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(2));
            Assert.That(miner.ProgressTicks, Is.EqualTo(0));
            Assert.That(miner.CycleDurationTicks, Is.EqualTo(18_518_519));
        }

        [Test]
        public void MidCycleResearch_IsAlignedAtTheFirstTimestampWithTheFractionPreserved()
        {
            SimRun run = Start(RunSimHarness.Catalog(), out RunState s);
            MachineInstance smelter = RunSimHarness.Add(s, "machine.smelter", 0, 1, progress: 10_000_000);
            RunSimHarness.Stock(s, "iron.ore", 100);
            Assert.That(s.TrySetResearchLevel("research.a-smelter", 1), Is.EqualTo(RunStateResult.Ok));

            run.StepOk(0);

            // Speed 1.1x: cycle ceil(2e7 x 1e6 / 1.1e6) = 18,181,819; progress floor(1e7 x 18,181,819 / 2e7).
            Assert.That(smelter.CycleDurationTicks, Is.EqualTo(18_181_819));
            Assert.That(smelter.ProgressTicks, Is.EqualTo(9_090_909));
        }

        [Test]
        public void MidCycleOverdrive_ActivatesAndEnds_WithTheFractionPreservedBothWays()
        {
            SimRun run = Start(RunSimHarness.Catalog(), out RunState s);
            MachineInstance miner = RunSimHarness.Add(s, "machine.miner", 0, 1, progress: 10_000_000);
            Assert.That(s.Overdrive.TryActivate(run.NowUtcMs), Is.EqualTo(RunStateResult.Ok));

            run.StepOk(0);
            Assert.That(miner.CycleDurationTicks, Is.EqualTo(10_000_000), "Overdrive x2 halves the cycle");
            Assert.That(miner.ProgressTicks, Is.EqualTo(5_000_000), "half a cycle stays half a cycle");

            // The 30 s window ends at 3e8 ticks. Completions at 5e6 + n x 1e7: n = 0..29, 30 in total.
            run.StepOk(30 * Second);
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(60));
            Assert.That(miner.CycleDurationTicks, Is.EqualTo(20_000_000));
            Assert.That(miner.ProgressTicks, Is.EqualTo(10_000_000), "5e6 of 1e7 rescaled to 1e7 of 2e7");
            Assert.That(s.Overdrive.ActiveUntilUtcMs, Is.Not.Null, "cooling down");

            run.StepOk(300 * Second);
            Assert.That(s.Overdrive.ActiveUntilUtcMs, Is.Null, "the cooldown boundary returns Overdrive to ready");
            Assert.That(s.Overdrive.CooldownUntilUtcMs, Is.Null);
        }

        [Test]
        public void MidCycleItemEffect_StartsAndExpires_WithTheFractionPreservedBothWays()
        {
            SimRun run = Start(RunSimHarness.Catalog(), out RunState s);
            MachineInstance miner = RunSimHarness.Add(s, "machine.miner", 0, 1, progress: 10_000_000);
            Assert.That(s.TryAddEffect(new ActiveEffect("item.boost", new ConflictKey("machineSpeed", "factory"), run.NowUtcMs, run.NowUtcMs + 60_000)),
                Is.EqualTo(RunStateResult.Ok));

            run.StepOk(0);
            // Speed 1.5x: cycle ceil(2e7 / 1.5) = 13,333,334; progress floor(1e7 x 13,333,334 / 2e7) = 6,666,667.
            Assert.That(miner.CycleDurationTicks, Is.EqualTo(13_333_334));
            Assert.That(miner.ProgressTicks, Is.EqualTo(6_666_667));

            run.StepOk(60 * Second);
            // First completion after 6,666,667 ticks, then one per 13,333,334: 1 + floor(593,333,333 / 13,333,334) = 45.
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(90));
            Assert.That(s.Effects.Count, Is.EqualTo(0), "the effect is removed at its end instant");
            Assert.That(miner.CycleDurationTicks, Is.EqualTo(20_000_000));
            long lastCompletion = 6_666_667 + 44 * 13_333_334L;
            long into = 60 * Second - lastCompletion;
            Assert.That(miner.ProgressTicks, Is.EqualTo(into * 20_000_000L / 13_333_334L));
        }

        [Test]
        public void SpeedModifiers_FoldInTheDocumentedOrder_ResearchIndustryItemsOverdrive()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState s = RunSimHarness.NewState(catalog);
            var permanent = new PermanentState(catalog);
            MachineInstance smelter = RunSimHarness.Add(s, "machine.smelter", 0, 1, level: 2);
            s.TrySetResearchLevel("research.a-smelter", 1);
            s.TrySetResearchLevel("research.b-all", 1);
            permanent.TrySetIndustryLevel("industry.a-speed", 1);
            s.TryAddEffect(new ActiveEffect("item.boost", new ConflictKey("machineSpeed", "factory"), 0, 60_000));

            Assert.That(new RunModifiers(catalog).TryMachineSpeed(s, permanent, smelter, true, null, out long speed), Is.True);

            // 1.08e6 -> x1.10 = 1,188,000 -> x1.07 = 1,271,160 -> x1.05 = 1,334,718 -> x1.5 = 2,002,077 -> x2.
            Assert.That(speed, Is.EqualTo(4_004_154));
        }

        [Test]
        public void AutoSale_UsesTheEffectivePrice_FlooredPerSale()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            RunState s = RunSimHarness.NewState(catalog);
            var permanent = new PermanentState(catalog);
            s.TrySetResearchLevel("research.c-price", 2);
            permanent.TrySetIndustryLevel("industry.b-price", 1);
            RunSimHarness.Stock(s, "iron.ore", 1000);
            s.TrySetAutoSell("iron.ore", true);
            var run = new SimRun(catalog, s, permanent);

            run.StepOk(0);

            // 1e6 x 1.13 = 1,130,000; x 1.13 = 1,276,900; x 1.06 = 1,353,514 ppm; floor(1000 x 1,353,514 / 1e6) = 1,353.
            Assert.That(s.Cash, Is.EqualTo(25 + 1353));
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(0));
        }

        // ---------------------------------------------------------------------------------------------
        // Batched cycles (OD-6)
        // ---------------------------------------------------------------------------------------------

        private static RunCatalog BatchCatalog(long levelTwoSpeedPpm, long capacity = 1000)
        {
            return RunSimHarness.Catalog(new RunSimHarness.CatalogOptions
            {
                StorageCapacity = capacity,
                SpeedGrowthPpm = levelTwoSpeedPpm,
                MaxLevel = 2,
                MaxCombinedSpeedPpm = 1_000_000_000L,
                LabRecipes = true,
            });
        }

        [Test]
        public void BatchedCycle_RateFollowsTheFullMultiplier_ExactCase()
        {
            RunCatalog catalog = BatchCatalog(50_000_000);
            SimRun run = Start(catalog, out RunState s);
            MachineInstance miner = RunSimHarness.Add(s, "machine.miner", 0, 1, level: 2);

            run.StepOk(12_000_000);

            // Speed x50, 2 s recipe: k = ceil(2.5) = 3 units per cycle, cycle 3 x 2e7 / 50 = 1,200,000 ticks.
            Assert.That(miner.CycleDurationTicks, Is.EqualTo(1_200_000));
            Assert.That(run.Sim.LastCompletions, Is.EqualTo(10));
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(10 * 3 * 2));
        }

        [Test]
        public void BatchedCycle_RateWithinOneTickPerCycle_OfTheIdealRate()
        {
            RunCatalog catalog = BatchCatalog(50_000_001, 100_000);
            SimRun run = Start(catalog, out RunState s);
            MachineInstance miner = RunSimHarness.Add(s, "machine.miner", 0, 1, level: 2);
            const long cycles = 1000;

            run.StepOk(cycles * 1_200_000);

            // k = 3; cycle = ceil(3 x 2e13 / 50,000,001) = 1,200,000 (the exact quotient is 1,199,999.976).
            Assert.That(miner.CycleDurationTicks, Is.EqualTo(1_200_000));
            long units = s.Inventory.Units("iron.ore") / 2;
            Assert.That(units, Is.EqualTo(cycles * 3));
            // The rate is never overstated and lags by less than one tick of ideal production per cycle.
            long ideal = cycles * 1_200_000 * 50_000_001L;
            long actual = units * 20_000_000L * 1_000_000L;
            Assert.That(actual, Is.LessThanOrEqualTo(ideal), "never overstated");
            Assert.That(ideal - actual, Is.LessThan(cycles * 50_000_001L), "less than one tick per cycle");
        }

        // ---------------------------------------------------------------------------------------------
        // Partial batches (OD-9)
        // ---------------------------------------------------------------------------------------------

        /// <summary>Level-2 machines with speed x100: k = 5 and a cycle of 1,000,000 ticks.</summary>
        private static SimRun PartialBatchRun(out RunState s, long cash = 25) => Start(BatchCatalog(100_000_000), out s, cash);

        private static MachineInstance DueBatchMachine(RunState s, string type, int instance, int slot = 0)
        {
            return RunSimHarness.Add(s, type, slot, instance, level: 2, progress: 20_000_000);
        }

        [Test]
        public void PartialBatch_LimitedByAnInput_CompletesTheLargestFittingUnits()
        {
            SimRun run = PartialBatchRun(out RunState s);
            MachineInstance alloy = DueBatchMachine(s, "machine.alloy", 1);
            RunSimHarness.Stock(s, "iron.ore", 3);
            RunSimHarness.Stock(s, "iron.bar", 20);
            long[] before = Units(s);

            run.StepOk(0);

            Assert.That(alloy.CycleDurationTicks, Is.EqualTo(1_000_000));
            // k = 5, k_in = min(3 / 1, 20 / 2) = 3: 3 units, remainder dropped.
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(0));
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(14));
            Assert.That(s.Inventory.Units("iron.plate"), Is.EqualTo(12));
            Assert.That(alloy.ProgressTicks, Is.EqualTo(0));
            AssertFlow(run.Sim, before, s, "input-limited batch");
        }

        [Test]
        public void PartialBatch_LimitedByTheOtherInput()
        {
            SimRun run = PartialBatchRun(out RunState s);
            DueBatchMachine(s, "machine.alloy", 1);
            RunSimHarness.Stock(s, "iron.ore", 20);
            RunSimHarness.Stock(s, "iron.bar", 5);
            long[] before = Units(s);

            run.StepOk(0);

            // k_in = min(20 / 1, 5 / 2) = 2.
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(18));
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(1));
            Assert.That(s.Inventory.Units("iron.plate"), Is.EqualTo(8));
            AssertFlow(run.Sim, before, s, "second-input-limited batch");
        }

        [Test]
        public void PartialBatch_LimitedByCapacity()
        {
            SimRun run = PartialBatchRun(out RunState s);
            DueBatchMachine(s, "machine.alloy", 1);
            RunSimHarness.Stock(s, "iron.ore", 20);
            RunSimHarness.Stock(s, "iron.bar", 20);
            RunSimHarness.Stock(s, "iron.gear", 958);
            Assert.That(s.Inventory.Free, Is.EqualTo(2));
            long[] before = Units(s);

            run.StepOk(0);

            // Net per unit = 4 - (1 + 2) = +1: k_cap = 2.
            Assert.That(s.Inventory.Units("iron.plate"), Is.EqualTo(8));
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(18));
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(16));
            Assert.That(s.Inventory.Total, Is.EqualTo(1000));
            AssertFlow(run.Sim, before, s, "capacity-limited batch");
        }

        [Test]
        public void PartialBatch_LimitedByInputAndCapacityTogether()
        {
            SimRun run = PartialBatchRun(out RunState s);
            DueBatchMachine(s, "machine.alloy", 1);
            RunSimHarness.Stock(s, "iron.ore", 3);
            RunSimHarness.Stock(s, "iron.bar", 20);
            RunSimHarness.Stock(s, "iron.gear", 974);
            Assert.That(s.Inventory.Free, Is.EqualTo(3));
            long[] before = Units(s);

            run.StepOk(0);

            // k_in = 3 and k_cap = 3 tie; k = 5.
            Assert.That(s.Inventory.Units("iron.plate"), Is.EqualTo(12));
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(0));
            Assert.That(s.Inventory.Total, Is.EqualTo(1000));
            AssertFlow(run.Sim, before, s, "tied limits");
        }

        [Test]
        public void PartialBatch_InputAndCapacityDifferent_TheSmallerWins()
        {
            SimRun run = PartialBatchRun(out RunState s);
            DueBatchMachine(s, "machine.multiplier", 1);
            RunSimHarness.Stock(s, "iron.ore", 4);
            RunSimHarness.Stock(s, "iron.gear", 990);
            Assert.That(s.Inventory.Free, Is.EqualTo(6));
            long[] before = Units(s);

            run.StepOk(0);

            // Net +2 per unit: k_cap = 3, k_in = 4, k = 5: k' = 3.
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(1));
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(9));
            Assert.That(s.Inventory.Total, Is.EqualTo(1000));
            AssertFlow(run.Sim, before, s, "capacity below input");
        }

        [Test]
        public void PartialBatch_InputFreeMiner_IsLimitedByCapacityOnly()
        {
            SimRun run = PartialBatchRun(out RunState s);
            DueBatchMachine(s, "machine.miner", 1);
            RunSimHarness.Stock(s, "iron.gear", 993);
            Assert.That(s.Inventory.Free, Is.EqualTo(7));

            run.StepOk(0);

            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(6), "k_cap = floor(7 / 2) = 3 units of 2 Ore");
        }

        [Test]
        public void PartialBatch_ZeroUnitsWaits_AndKeepsFullProgress()
        {
            SimRun run = PartialBatchRun(out RunState s);
            MachineInstance alloy = DueBatchMachine(s, "machine.alloy", 1);
            RunSimHarness.Stock(s, "iron.bar", 20);
            long[] before = Units(s);

            run.StepOk(10 * Second);

            Assert.That(alloy.ProgressTicks, Is.EqualTo(alloy.CycleDurationTicks), "no Ore: waiting");
            Assert.That(run.Sim.LastCompletions, Is.EqualTo(0));
            AssertFlow(run.Sim, before, s, "k' = 0");
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(20));
        }

        [Test]
        public void PartialBatch_RemainderIsDropped_TheNextCycleStartsWithTheFullBatch()
        {
            SimRun run = PartialBatchRun(out RunState s);
            MachineInstance alloy = DueBatchMachine(s, "machine.alloy", 1);
            RunSimHarness.Stock(s, "iron.ore", 3);
            RunSimHarness.Stock(s, "iron.bar", 20);
            run.StepOk(0);
            Assert.That(s.Inventory.Units("iron.plate"), Is.EqualTo(12));

            RunSimHarness.Stock(s, "iron.ore", 100);
            RunSimHarness.Stock(s, "iron.bar", 100);
            run.StepOk(alloy.CycleDurationTicks);

            // A fresh full batch of 5: +20 Plates, nothing carried over from the dropped remainder of 2.
            Assert.That(s.Inventory.Units("iron.plate"), Is.EqualTo(12 + 20));
        }

        [Test]
        public void PartialBatch_CapacityLimited_CompletesInTheRetryPassAfterASale()
        {
            SimRun run = PartialBatchRun(out RunState s);
            MachineInstance multiplier = DueBatchMachine(s, "machine.multiplier", 1, 0);
            RunSimHarness.Add(s, "machine.assembler", 1, 2, level: 1, progress: 50_000_000);
            RunSimHarness.Stock(s, "iron.ore", 10);
            RunSimHarness.Stock(s, "iron.plate", 989);
            s.TrySetAutoSell("iron.gear", true);
            Assert.That(s.Inventory.Free, Is.EqualTo(1));
            long[] before = Units(s);

            run.StepOk(0);

            // Pass 1: the Multiplier (first in priority) finds k_cap = floor(1 / 2) = 0 and waits; the Assembler
            // turns 2 Plates into a Gear, which is sold (+24): 3 units are free. Pass 2: k_cap = 1 < k_in = 10 and
            // < k = 5: one unit completes. Pass 3 completes nothing.
            Assert.That(multiplier.ProgressTicks, Is.EqualTo(0));
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(9));
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(3));
            Assert.That(s.Inventory.Units("iron.plate"), Is.EqualTo(987));
            Assert.That(s.Inventory.Units("iron.gear"), Is.EqualTo(0));
            Assert.That(s.Cash, Is.EqualTo(25 + 24));
            Assert.That(run.Sim.LastMaxPassesPerTimestamp, Is.EqualTo(3));
            Assert.That(run.Sim.LastPartialBatches, Is.EqualTo(1));
            AssertFlow(run.Sim, before, s, "retry pass partial batch");
        }

        [Test]
        public void PartialBatch_InputLimited_CompletesInTheRetryPassAfterASale()
        {
            SimRun run = PartialBatchRun(out RunState s);
            MachineInstance multiplier = DueBatchMachine(s, "machine.multiplier", 1, 0);
            RunSimHarness.Add(s, "machine.assembler", 1, 2, level: 2, progress: 50_000_000);
            RunSimHarness.Stock(s, "iron.ore", 1);
            RunSimHarness.Stock(s, "iron.plate", 4);
            RunSimHarness.Stock(s, "iron.bar", 994);
            s.TrySetAutoSell("iron.gear", true);
            Assert.That(s.Inventory.Free, Is.EqualTo(1));

            run.StepOk(0);

            // The level-2 Assembler (k = 2) turns 4 Plates into 2 Gears, sold for 48: 5 units are free. The waiting
            // Multiplier then has k_cap = 2 but only 1 Ore: k' = 1 of k = 5.
            Assert.That(multiplier.ProgressTicks, Is.EqualTo(0));
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(0));
            Assert.That(s.Inventory.Units("iron.bar"), Is.EqualTo(997));
            Assert.That(s.Cash, Is.EqualTo(25 + 48));
        }

        [Test]
        public void PartialBatch_ARunningMachineKeepsRunningWhileOneUnitFits()
        {
            SimRun run = PartialBatchRun(out RunState s);
            MachineInstance alloy = RunSimHarness.Add(s, "machine.alloy", 0, 1, level: 2, progress: 0);
            RunSimHarness.Stock(s, "iron.ore", 1);
            RunSimHarness.Stock(s, "iron.bar", 2);

            run.StepOk(500_000);
            Assert.That(alloy.ProgressTicks, Is.EqualTo(500_000), "one recipe unit is in stock: the predicate holds");
            run.StepOk(500_000);
            Assert.That(s.Inventory.Units("iron.plate"), Is.EqualTo(4), "k' = 1");
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(0));
        }

        // ---------------------------------------------------------------------------------------------
        // Expiry ordering
        // ---------------------------------------------------------------------------------------------

        [TestCase(2000, 2, TestName = "EffectExpiringAtTheCompletionInstant_CompletesWithoutItsFactor")]
        [TestCase(1999, 2, TestName = "EffectExpiringOneMillisecondBefore_CompletesWithoutItsFactor")]
        [TestCase(2001, 4, TestName = "EffectStillActiveAtTheCompletionInstant_CompletesWithItsFactor")]
        public void EffectExpiry_IsProcessedBeforeTheCompletionAtTheSameInstant(int drillDurationMs, long expectedOre)
        {
            RunCatalog catalog = RunSimHarness.Catalog(new RunSimHarness.CatalogOptions { DrillItemDurationMs = drillDurationMs });
            SimRun run = Start(catalog, out RunState s);
            RunSimHarness.Add(s, "machine.miner", 0, 1);
            Assert.That(s.TryAddEffect(new ActiveEffect("item.drill", new ConflictKey("machineOutput", "machine.miner"), run.NowUtcMs, run.NowUtcMs + drillDurationMs)),
                Is.EqualTo(RunStateResult.Ok));

            run.StepOk(2 * Second);

            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(expectedOre));
        }

        [Test]
        public void ContractDeadline_IsInclusive_ExpiryIsOneMillisecondLater()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            SimRun run = Start(catalog, out RunState s);
            long deadline = run.NowUtcMs + 180_000;
            Assert.That(s.Contracts.TrySetActive(new ActiveContract("contract.timed-bar", run.NowUtcMs, deadline, 40)), Is.EqualTo(RunStateResult.Ok));

            run.StepOk(180_000 * RunSimHarness.TicksPerMs);

            Assert.That(run.NowUtcMs, Is.EqualTo(deadline));
            Assert.That(s.Contracts.Active, Is.Not.Null, "a delivery at the deadline instant is still valid");
            Assert.That(s.Contracts.CheckComplete(deadline), Is.EqualTo(RunStateResult.Ok));

            run.StepOk(RunSimHarness.TicksPerMs);

            Assert.That(s.Contracts.Active, Is.Null, "expired at deadline + 1 ms");
            Assert.That(s.Contracts.CompletionCount("contract.timed-bar"), Is.EqualTo(0), "expiry does not count as a completion");
        }

        // ---------------------------------------------------------------------------------------------
        // Contention for capacity
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void SeveralProducers_ContendForCapacity_InPriorityOrderOnly()
        {
            RunCatalog catalog = RunSimHarness.Catalog(new RunSimHarness.CatalogOptions { StorageCapacity = 8 });
            foreach (int[] order in new[] { new[] { 1, 2, 3, 4, 5 }, new[] { 5, 4, 3, 2, 1 }, new[] { 3, 1, 5, 2, 4 } })
            {
                SimRun run = Start(catalog, out RunState s);
                for (int i = 1; i <= 5; i++) RunSimHarness.Add(s, "machine.miner", 5 - i, i, progress: 20_000_000);
                RunSimHarness.Reorder(s, order);

                run.StepOk(0);

                // Room for four 2-Ore completions: the first four in priority order complete, the fifth waits.
                for (int position = 0; position < 5; position++)
                {
                    long expected = position < 4 ? 0 : 20_000_000;
                    Assert.That(run.Machine(order[position]).ProgressTicks, Is.EqualTo(expected),
                        "order " + string.Join(",", order) + " position " + position);
                }
                Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(8));
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Atomic rejection and argument checks
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void Overflow_AtTheFirstTimestamp_RejectsTheWholeAdvance()
        {
            SimRun run = Start(RunSimHarness.Catalog(), out RunState s, ExactBound.Value - 10);
            RunSimHarness.Add(s, "machine.miner", 0, 1, progress: 5_000_000);
            RunSimHarness.Stock(s, "iron.gear", 1);
            s.TrySetAutoSell("iron.gear", true);
            string before = RunSimHarness.Fingerprint(s);

            Assert.That(run.Step(30 * Second), Is.EqualTo(RunAdvanceResult.ArithmeticOverflow));

            Assert.That(RunSimHarness.Fingerprint(s), Is.EqualTo(before), "nothing was published");
        }

        [Test]
        public void Overflow_LateInTheAdvance_RejectsTheWholeAdvance_AndALaterSmallerAdvanceStillWorks()
        {
            SimRun run = Start(RunSimHarness.Catalog(), out RunState s, ExactBound.Value - 20);
            RunSimHarness.Add(s, "machine.miner", 0, 1);
            s.TrySetAutoSell("iron.ore", true);
            string before = RunSimHarness.Fingerprint(s);

            // One Miner cycle sells 2 Ore for $2: the 11th sale would exceed the bound.
            Assert.That(run.Step(30 * Second), Is.EqualTo(RunAdvanceResult.ArithmeticOverflow));
            Assert.That(RunSimHarness.Fingerprint(s), Is.EqualTo(before), "ten sales were staged, none published");
            Assert.That(run.Sim.LastCompletions, Is.GreaterThanOrEqualTo(10), "the overflow happened mid-advance");

            Assert.That(run.Step(20 * Second), Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(s.Cash, Is.EqualTo(ExactBound.Value));
        }

        [Test]
        public void InvalidArguments_ChangeNothing()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            SimRun run = Start(catalog, out RunState s);
            RunSimHarness.Add(s, "machine.miner", 0, 1, progress: 5_000_000);
            string before = RunSimHarness.Fingerprint(s);
            var unordered = new List<BoundaryEvent>
            {
                new BoundaryEvent(50, BoundaryKind.EffectExpiry, "b", 0),
                new BoundaryEvent(10, BoundaryKind.EffectExpiry, "a", 0),
            };
            var sameTickWrongKindOrder = new List<BoundaryEvent>
            {
                new BoundaryEvent(10, BoundaryKind.ContractExpiry, "a", 0),
                new BoundaryEvent(10, BoundaryKind.EffectExpiry, "a", 0),
            };
            var sameTickWrongIdOrder = new List<BoundaryEvent>
            {
                new BoundaryEvent(10, BoundaryKind.EffectExpiry, "b", 0),
                new BoundaryEvent(10, BoundaryKind.EffectExpiry, "a", 0),
            };

            Assert.That(run.Sim.Advance(s, null, -1, RunSimHarness.StartUtcMs, null), Is.EqualTo(RunAdvanceResult.InvalidArgument));
            Assert.That(run.Sim.Advance(s, null, 100, RunSimHarness.StartUtcMs, unordered), Is.EqualTo(RunAdvanceResult.InvalidArgument));
            Assert.That(run.Sim.Advance(s, null, 100, RunSimHarness.StartUtcMs, sameTickWrongKindOrder), Is.EqualTo(RunAdvanceResult.InvalidArgument));
            Assert.That(run.Sim.Advance(s, null, 100, RunSimHarness.StartUtcMs, sameTickWrongIdOrder), Is.EqualTo(RunAdvanceResult.InvalidArgument));
            Assert.That(run.Sim.Advance(null, null, 100, RunSimHarness.StartUtcMs, null), Is.EqualTo(RunAdvanceResult.InvalidArgument));
            var foreign = new RunSimulator(RunSimHarness.Catalog());
            Assert.That(foreign.Advance(s, null, 100, RunSimHarness.StartUtcMs, null), Is.EqualTo(RunAdvanceResult.InvalidArgument));

            Assert.That(RunSimHarness.Fingerprint(s), Is.EqualTo(before));
        }

        [Test]
        public void EventsAfterTheEndOfTheAdvanceAreIgnored_AndEventsAtTheEndAreProcessed()
        {
            RunCatalog catalog = RunSimHarness.Catalog();
            SimRun run = Start(catalog, out RunState s);
            s.TryAddEffect(new ActiveEffect("item.boost", new ConflictKey("machineSpeed", "factory"), run.NowUtcMs, run.NowUtcMs + 60_000));
            long end = run.NowUtcMs + 60_000;
            var events = new List<BoundaryEvent> { new BoundaryEvent(600_000_000, BoundaryKind.EffectExpiry, "item.boost", end) };

            Assert.That(run.Sim.Advance(s, null, 599_999_999, run.NowUtcMs, events), Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(s.Effects.Count, Is.EqualTo(1), "an event after the end waits for a later advance");
            Assert.That(run.Sim.Advance(s, null, 600_000_000, run.NowUtcMs, events), Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(s.Effects.Count, Is.EqualTo(0), "an event exactly at the end belongs to this advance");
        }

        [Test]
        public void ZeroTickAdvance_SettlesTheStateAndIsIdempotent()
        {
            SimRun run = Start(RunSimHarness.Catalog(), out RunState s);
            RunSimHarness.Add(s, "machine.miner", 0, 1, progress: 20_000_000);
            RunSimHarness.Stock(s, "iron.gear", 3);
            s.TrySetAutoSell("iron.gear", true);

            run.StepOk(0);
            string once = RunSimHarness.Fingerprint(s);
            run.StepOk(0);

            Assert.That(RunSimHarness.Fingerprint(s), Is.EqualTo(once));
            Assert.That(s.Cash, Is.EqualTo(25 + 72), "the whole stock was sold at the settle");
            Assert.That(s.Inventory.Units("iron.ore"), Is.EqualTo(2));
        }

        [Test]
        public void ARunStateWithoutMachines_AdvancesAsANoOp()
        {
            SimRun run = Start(RunSimHarness.Catalog(), out RunState s);
            RunSimHarness.Stock(s, "iron.ore", 5);
            string before = RunSimHarness.Fingerprint(s);

            run.StepOk(100 * Second);

            Assert.That(RunSimHarness.Fingerprint(s), Is.EqualTo(before));
            Assert.That(run.Sim.LastTimestamps, Is.EqualTo(2), "the start and the end");
        }
    }
}
