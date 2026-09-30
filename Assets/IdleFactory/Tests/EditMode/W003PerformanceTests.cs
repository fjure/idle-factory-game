using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// W003 performance of <see cref="RunSimulator"/> (T098, plan.md, Performance): zero <c>GC.Alloc</c> samples per
    /// warmed <c>Advance</c> on both W003 variants (PE-4; the counter counts samples, not bytes) and the informational
    /// eight-hour worst-case timing record that T122 reuses. Timings are Edit Mode and Mono, so no budget is
    /// asserted on them.
    /// </summary>
    public sealed class W003PerformanceTests
    {
        private static readonly IReadOnlyList<BoundaryEvent> NoEvents = new BoundaryEvent[0];

        /// <summary>E(8 h) at 4/5 efficiency: the effective production time of the worst-case absence, in ticks.</summary>
        private const long EightHourEffectiveTicks = 230_400_000_000L;

        [TestCase(W003Variant.Stress)]
        [TestCase(W003Variant.Reference)]
        [TestCase(W003Variant.Bound)]
        public void WarmedAdvance_HasZeroGcAllocSamples(W003Variant variant)
        {
            W003Scenario sc = W003Workload.Build(variant);
            var sim = new RunSimulator(sc.Catalog);
            long now = sc.StartUtcMs;

            // Warm-up: the staged buffers grow to the factory size, JIT compiles the paths, every resource flows.
            for (int i = 0; i < 40; i++)
                Assert.That(sim.Advance(sc.State, sc.Permanent, 10_000_000L, now, NoEvents), Is.EqualTo(RunAdvanceResult.Ok));

            var counts = new int[6];
            for (int i = 0; i < counts.Length; i++)
            {
                long ticks = i < 4 ? 10_000_000L : 600_000_000L;
                RunAdvanceResult result = RunAdvanceResult.InvalidArgument;
                counts[i] = GcAllocationCounter.Count(() => result = sim.Advance(sc.State, sc.Permanent, ticks, now, NoEvents));
                Assert.That(result, Is.EqualTo(RunAdvanceResult.Ok));
                Assert.That(sim.LastCompletions, Is.GreaterThan(0), sc.Name + ": the measured advance did real work");
            }

            Assert.That(counts, Is.All.Zero, sc.Name + ": GC.Alloc samples per warmed Advance (1 s x4, then 60 s x2)");
        }

        [TestCase(W003Variant.Stress)]
        [TestCase(W003Variant.Reference)]
        [TestCase(W003Variant.Bound)]
        public void WorkloadShape_IsTheDocumentedTwelveSlotFactory(W003Variant variant)
        {
            W003Scenario sc = W003Workload.Build(variant);

            Assert.That(sc.State.Machines.Count, Is.EqualTo(12));
            Assert.That(sc.State.SlotCount, Is.EqualTo(12));
            Assert.That(sc.State.Validate(), Is.EqualTo(RunStateResult.Ok));
            Assert.That(sc.State.Effects.Count, Is.EqualTo(1), "one Item effect");
            Assert.That(sc.State.Overdrive.IsActive(sc.StartUtcMs), Is.True, "Overdrive active");
            int miners = 0, smelters = 0, presses = 0, assemblers = 0;
            foreach (MachineInstance m in sc.State.Machines)
            {
                if (m.MachineId == "machine.miner") miners++;
                else if (m.MachineId == "machine.smelter") smelters++;
                else if (m.MachineId == "machine.press") presses++;
                else assemblers++;
            }
            Assert.That(new[] { miners, smelters, presses, assemblers }, Is.EqualTo(new[] { 3, 4, 3, 2 }));
            for (int i = 0; i < 12; i++)
                Assert.That(sc.State.Priority[i], Is.EqualTo(sc.State.Machines[i].InstanceId), "priorities upstream first");

            if (variant == W003Variant.Reference)
            {
                Assert.That(sc.State.AutoSell, Is.EqualTo(new[] { "iron.gear" }));
                Assert.That(sc.State.Inventory.Total * 2, Is.EqualTo(sc.State.Inventory.Capacity), "store at 50 %");
            }
            else if (variant == W003Variant.Stress)
            {
                Assert.That(sc.State.AutoSell.Count, Is.EqualTo(4), "auto-sell on every resource");
            }

            if (variant != W003Variant.Reference)
            {
                // Every recipe at the event bound: batch of at least 2 and a cycle of exactly minCycleTicks (OD-6).
                var sim = new RunSimulator(sc.Catalog);
                Assert.That(sim.Advance(sc.State, sc.Permanent, 0, sc.StartUtcMs, NoEvents), Is.EqualTo(RunAdvanceResult.Ok));
                var modifiers = new RunModifiers(sc.Catalog);
                foreach (MachineInstance m in sc.State.Machines)
                {
                    Assert.That(m.CycleDurationTicks, Is.EqualTo(sc.Catalog.RunStart.MinCycleTicks), m.MachineId);
                    Assert.That(modifiers.TryMachineSpeed(sc.State, sc.Permanent, m, true, null, out long speed), Is.True);
                    Assert.That(SpeedComposer.TryBatch(sc.Catalog.Recipes[sc.Catalog.Machines[m.MachineId].RecipeId].DurationTicks,
                        speed, sc.Catalog.RunStart.MinCycleTicks, out long k, out _), Is.True);
                    Assert.That(k, Is.GreaterThanOrEqualTo(2), m.MachineId + " batch");
                }
            }
        }

        [TestCase(W003Variant.Stress)]
        [TestCase(W003Variant.Reference)]
        [TestCase(W003Variant.Bound)]
        public void EightHourWorstCase_TimingRecord_IsInformational(W003Variant variant)
        {
            // Warm the JIT with a short run of another instance.
            W003Scenario warm = W003Workload.Build(variant);
            var warmSim = new RunSimulator(warm.Catalog);
            Assert.That(warmSim.Advance(warm.State, warm.Permanent, 600_000_000L, warm.StartUtcMs, NoEvents), Is.EqualTo(RunAdvanceResult.Ok));

            W003Scenario sc = W003Workload.Build(variant);
            var sim = new RunSimulator(sc.Catalog);
            long[] before = RunSimHarness.Units(sc.State);
            long cashBefore = sc.State.Cash;

            long start = Stopwatch.GetTimestamp();
            RunAdvanceResult result = sim.Advance(sc.State, sc.Permanent, EightHourEffectiveTicks, sc.StartUtcMs, NoEvents);
            double elapsedMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;

            Assert.That(result, Is.EqualTo(RunAdvanceResult.Ok));
            RunSimHarness.AssertFlow(sim, before, sc.State, sc.Name + " 8 h");
            Assert.That(sc.State.Validate(), Is.EqualTo(RunStateResult.Ok));
            Assert.That(sim.LastMaxPassesPerTimestamp, Is.LessThanOrEqualTo(13), "N + 1");
            if (variant == W003Variant.Bound)
                Assert.That(sim.LastCompletions, Is.EqualTo(12L * 230_400L), "12 machines x E(8 h) / minCycleTicks = 2,764,800 completions");

            double nsPerCompletion = elapsedMs * 1_000_000.0 / Math.Max(1, sim.LastCompletions);
            double nsPerTimestamp = elapsedMs * 1_000_000.0 / Math.Max(1, sim.LastTimestamps);
            TestContext.Out.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "W003-TIMING variant={0} horizon=8h-effective(2.304e11 ticks) completions={1} partialBatches={2} timestamps={3} passes={4} maxPassesPerTimestamp={5} elapsedMs={6:F1} nsPerCompletion={7:F0} nsPerTimestamp={8:F0} cashDelta={9} extrapolated2.76MCompletionsMs={10:F0} runtime=editor-mono",
                sc.Name, sim.LastCompletions, sim.LastPartialBatches, sim.LastTimestamps, sim.LastPasses, sim.LastMaxPassesPerTimestamp,
                elapsedMs, nsPerCompletion, nsPerTimestamp, sc.State.Cash - cashBefore, nsPerCompletion * 2_764_800.0 / 1_000_000.0));
        }
    }
}
