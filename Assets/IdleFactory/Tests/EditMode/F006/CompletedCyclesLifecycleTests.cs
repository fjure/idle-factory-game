using System;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T514 (Feature 006, research R08, OD-11, FR-001, FR-002): the lifecycle of the session-owned <c>CompletedCycles</c> read value.
    /// +1 per completed cycle (also for a partial batch, never the batch factor), exposed only after a successful commit, pruned on
    /// removal, reset on replace, and empty on construction, restore and prestige.
    /// </summary>
    public sealed class CompletedCyclesLifecycleTests
    {
        private const string Miner = RunCommandFixtures.Miner;
        private const long Second = 10_000_000;

        private static Guid BuildOk(RunCommandFixtures.Rig rig, int slot, string machine)
        {
            RunCommandResult<BuildResult> built = rig.Commands.Build(slot, machine);
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
            return built.Value.InstanceId;
        }

        private static long ViewCycles(RunSession session, Guid id)
        {
            var views = new MachineStateView[session.State.Machines.Count];
            int n = session.CopyMachineStates(views);
            Assert.That(n, Is.EqualTo(views.Length));
            for (int i = 0; i < n; i++)
                if (session.State.Machines[i].InstanceId == id) return views[i].CompletedCycles;
            return -1;
        }

        private static long Total(RunSession session)
        {
            long total = 0;
            foreach (MachineInstance m in session.State.Machines) total += session.CompletedCyclesOf(m.InstanceId);
            return total;
        }

        private static RunCommandFixtures.Options Batched() => new RunCommandFixtures.Options
        {
            Durations = new long[] { 1_000_000, 1_000_000, 40_000_000, 50_000_000 },
            Milestones = new[] { new SpeedMilestone(2, 3_000_000) },
        };

        [Test]
        public void TheViewConstructorParameterDefaultsToZero()
        {
            var view = new MachineStateView(0, 0, 1, true, MachinePhase.Running, MachineBlocker.None, 1, 0);
            Assert.That(view.CompletedCycles, Is.Zero);
            Assert.That(new MachineStateView(0, 0, 1, true, MachinePhase.Running, MachineBlocker.None, 1, 0, 7).CompletedCycles, Is.EqualTo(7));
        }

        [Test]
        public void ANewSessionAndAFreshMachineStartAtZero()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig();
            Assert.That(rig.Session.CompletedCyclesOf(Guid.NewGuid()), Is.Zero);
            Guid miner = BuildOk(rig, 0, Miner);
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.Zero);
            Assert.That(ViewCycles(rig.Session, miner), Is.Zero);
        }

        [Test]
        public void EveryCompletedCycleCountsOnceAndMachinesCountIndependently()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig();
            Guid miner = BuildOk(rig, 0, Miner);
            Guid second = BuildOk(rig, 1, Miner);

            rig.Advance(Second); // a 2 s cycle: nothing completed yet
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.Zero);

            rig.Advance(Second); // the first completion at 2 s
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.EqualTo(1));
            Assert.That(rig.Session.CompletedCyclesOf(second), Is.EqualTo(1));

            rig.Advance(8 * Second); // 10 s in all: five completions each, two Ore per cycle
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.EqualTo(5));
            Assert.That(rig.Session.CompletedCyclesOf(second), Is.EqualTo(5));
            Assert.That(rig.State.Inventory.Units("iron.ore"), Is.EqualTo(20), "ten cycles of two Ore: the count is cycles, not units");
            Assert.That(ViewCycles(rig.Session, miner), Is.EqualTo(5));
            Assert.That(ViewCycles(rig.Session, second), Is.EqualTo(5));
        }

        [Test]
        public void ACommandSettleDoesNotCountACompletionTwice()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig();
            Guid miner = BuildOk(rig, 0, Miner);
            rig.Advance(2 * Second);
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.EqualTo(1));
            Assert.That(rig.Commands.SetAutoSell("iron.ore", false).IsSuccess, Is.True);
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.EqualTo(1));
            Assert.That(rig.State.Inventory.Units("iron.ore"), Is.EqualTo(2));
        }

        [Test]
        public void AFullBatchCountsOneCycleNotTheBatchFactor()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(Batched());
            Guid miner = BuildOk(rig, 0, Miner);
            Assert.That(rig.Commands.Upgrade(miner).Value.Rates.BatchFactor, Is.EqualTo(4));
            rig.Advance(1_234_568);
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.EqualTo(1), "one cycle exchanged four recipe units");
            Assert.That(rig.State.Inventory.Units("iron.ore"), Is.EqualTo(8));
        }

        [Test]
        public void APartialBatchCountsOneCycle()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(Batched());
            Guid miner = BuildOk(rig, 0, Miner);
            Assert.That(rig.Commands.Upgrade(miner).Value.Rates.BatchFactor, Is.EqualTo(4));
            RunSimHarness.Stock(rig.State, "iron.gear", 995);
            rig.Advance(1_234_568);
            Assert.That(rig.State.Inventory.Units("iron.ore"), Is.EqualTo(4), "a partial batch of two recipe units");
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.EqualTo(1));
        }

        [Test]
        public void AFaultedAdvanceExposesNoCompletion()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig();
            RunSimHarness.Add(rig.State, Miner, 0, 100);
            Guid miner = RunSimHarness.Id(100);
            var sim = new RunSimulator(rig.Catalog) { PassBudgetOverride = 1 };
            long start = RunCommandFixtures.StartUtcMs;

            RunAdvanceResult faulted = sim.Advance(rig.State, rig.Session.Permanent, 3 * Second, TimelineMapping.Live(start),
                RunSimHarness.Events(rig.State, start, 0, 3 * Second));
            Assert.That(faulted, Is.EqualTo(RunAdvanceResult.ArithmeticOverflow));
            Assert.That(sim.TryGetCompletedCount(miner, out int none), Is.False);
            Assert.That(none, Is.Zero, "a faulted advance exposes 0");

            sim.PassBudgetOverride = 0;
            Assert.That(sim.Advance(rig.State, rig.Session.Permanent, 3 * Second, TimelineMapping.Live(start),
                RunSimHarness.Events(rig.State, start, 0, 3 * Second)), Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(sim.TryGetCompletedCount(miner, out int one), Is.True);
            Assert.That(one, Is.EqualTo(1));
            Assert.That(sim.LastCompletions, Is.EqualTo(1));

            // A rejected advance afterwards exposes 0 again: the value never carries over between advances.
            Assert.That(sim.Advance(rig.State, rig.Session.Permanent, -1, TimelineMapping.Live(start), null), Is.EqualTo(RunAdvanceResult.InvalidArgument));
            Assert.That(sim.TryGetCompletedCount(miner, out int rejected), Is.False);
            Assert.That(rejected, Is.Zero);
        }

        [Test]
        public void ARejectedSessionAdvanceLeavesTheCountUntouched()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig();
            Guid miner = BuildOk(rig, 0, Miner);
            rig.Advance(4 * Second);
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.EqualTo(2));
            Assert.That(rig.Session.Advance(-1).IsSuccess, Is.False);
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.EqualTo(2));
            Assert.That(ViewCycles(rig.Session, miner), Is.EqualTo(2));
        }

        [Test]
        public void ARemovedMachineIsPrunedAndARebuiltInstanceStartsAgainAtZero()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig();
            Guid miner = BuildOk(rig, 0, Miner);
            Guid keep = BuildOk(rig, 1, Miner);
            rig.Advance(4 * Second);
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.EqualTo(2));

            Assert.That(rig.Commands.Remove(miner, true).IsSuccess, Is.True);
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.Zero, "pruned on removal");
            Assert.That(rig.Session.CompletedCyclesOf(keep), Is.EqualTo(2), "the other machine is untouched");

            rig.NextId = 1; // build the same instance ID again
            Guid again = BuildOk(rig, 0, Miner);
            Assert.That(again, Is.EqualTo(miner));
            Assert.That(rig.Session.CompletedCyclesOf(again), Is.Zero);
            rig.Advance(2 * Second);
            Assert.That(rig.Session.CompletedCyclesOf(again), Is.EqualTo(1));
            Assert.That(rig.Session.CompletedCyclesOf(keep), Is.EqualTo(3));
        }

        [Test]
        public void ReplacingTheStateResetsTheCountEvenForTheSameInstanceId()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig();
            Guid miner = BuildOk(rig, 0, Miner);
            rig.Advance(6 * Second);
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.EqualTo(3));

            var loaded = new RunState(rig.Catalog, RunSimHarness.Id(9001), 1, null, 100_000, 0, 1, 1,
                new[] { "iron.ore", "iron.bar", "iron.plate", "iron.gear", Miner });
            RunSimHarness.Add(loaded, Miner, 0, 1); // the same instance ID as the built machine
            rig.Session.ReplaceLoaded(loaded, rig.Session.Permanent, rig.Session.TimelineUtcMs);

            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.Zero, "a restore starts from an empty map");
            Assert.That(ViewCycles(rig.Session, miner), Is.Zero);
            rig.Advance(2 * Second);
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.EqualTo(1), "only completions after the restore count");
        }

        [Test]
        public void APrestigeStartsTheNewRunAtZero()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(cash: 10_000_000);
            Guid miner = BuildOk(rig, 0, Miner);
            rig.Advance(4 * Second);
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.EqualTo(2));

            Assert.That(rig.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.EqualTo(2), "a pending candidate changes nothing in the live run");
            Assert.That(rig.Commands.PromotePrestige().IsSuccess, Is.True);
            Assert.That(rig.Session.CompletedCyclesOf(miner), Is.Zero, "empty after the prestige");

            rig.NextId = 1;
            Guid next = BuildOk(rig, 0, Miner);
            Assert.That(next, Is.EqualTo(miner), "the same instance ID in the new run");
            Assert.That(rig.Session.CompletedCyclesOf(next), Is.Zero);
            rig.Advance(2 * Second);
            Assert.That(rig.Session.CompletedCyclesOf(next), Is.EqualTo(1));
        }

        [Test]
        public void AReturnRestoresAnEmptyMapAndTheOfflineCompletionsAreNotCounted()
        {
            DurabilityRig h = DurabilityRig.Suspended();
            Assert.That(Total(h.Session), Is.GreaterThan(0), "the played session counted completions");

            RunPersistenceOutcome returned = h.Persistence.ResumeAndReconcile().Value;
            Assert.That(returned.OfflineSummary.Completions, Is.GreaterThan(0), "the absence completed cycles");
            Assert.That(Total(h.Session), Is.Zero, "offline completions are not presentation deltas; the map is empty after the restore");

            h.Rig.Advance(10 * Second);
            Assert.That(Total(h.Session), Is.GreaterThan(0), "live completions count again");
        }
    }
}
