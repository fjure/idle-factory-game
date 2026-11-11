using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Tests.EditMode.Fixtures.SaveV3;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T514 (Feature 006, research R08, SC-007): the sum of the per-advance completion deltas that <c>RunSession</c> surfaces equals the
    /// completions of an independent engine run (<see cref="RunSimulator.LastCompletions"/>) on the W003 workloads (batched, event-bound),
    /// on W005-L, and after a restore with an offline return. The oracle is a second, identically built state advanced by a bare
    /// <see cref="RunSimulator"/>, which never sees the session.
    /// </summary>
    public sealed class CompletedCyclesPropertyTests
    {
        private static readonly long[] W003Steps =
        {
            10_000_000, 33_000_000, 1_000_000, 57_000_000, 120_000_000, 7_000_000, 300_000_000, 1_000_000_000,
        };

        private static readonly long[] W005Steps = { 10_000_000, 33_000_000, 61_000_000, 600_000_000, 3_600_000_000L };

        private static long Total(RunSession session)
        {
            long total = 0;
            foreach (MachineInstance m in session.State.Machines) total += session.CompletedCyclesOf(m.InstanceId);
            return total;
        }

        private static long Delta(RunSession session, ref long previous)
        {
            long now = Total(session);
            long delta = now - previous;
            previous = now;
            return delta;
        }

        [TestCase(W003Variant.Stress)]
        [TestCase(W003Variant.Bound)]
        [TestCase(W003Variant.Reference)]
        public void TheDeltaSumEqualsTheEngineCompletionsOnW003(W003Variant variant)
        {
            W003Scenario live = W003Workload.Build(variant);
            W003Scenario reference = W003Workload.Build(variant);
            var session = new RunSession(live.Catalog, live.State, live.Permanent, live.StartUtcMs);
            var oracle = new SimRun(reference.Catalog, reference.State, reference.Permanent, reference.StartUtcMs);

            long previous = 0, deltas = 0, engine = 0;
            foreach (long step in W003Steps)
            {
                Assert.That(session.Advance(step).IsSuccess, Is.True);
                oracle.StepOk(step);
                long delta = Delta(session, ref previous);
                deltas += delta;
                engine += oracle.Sim.LastCompletions;
                Assert.That(delta, Is.EqualTo(oracle.Sim.LastCompletions), variant + " step " + step);
            }

            Assert.That(engine, Is.GreaterThan(0), "the workload completed cycles");
            Assert.That(deltas, Is.EqualTo(engine));
            Assert.That(SaveV2Scenarios.Dump(session.State, session.Permanent), Is.EqualTo(SaveV2Scenarios.Dump(reference.State, reference.Permanent)),
                "the oracle is faithful: both runs reached the same state");
        }

        [Test]
        public void ABatchedWorkloadCountsCyclesNotUnits()
        {
            W003Scenario live = W003Workload.Build(W003Variant.Stress);
            var session = new RunSession(live.Catalog, live.State, live.Permanent, live.StartUtcMs);
            Assert.That(session.Advance(60_000_000).IsSuccess, Is.True);
            var views = new MachineStateView[session.State.Machines.Count];
            session.CopyMachineStates(views);
            bool batched = false;
            long cycles = 0;
            for (int i = 0; i < views.Length; i++)
            {
                batched |= views[i].BatchFactor >= 2;
                cycles += views[i].CompletedCycles;
            }
            Assert.That(batched, Is.True, "W003-S runs at the event bound with a batch factor of at least 2");
            Assert.That(cycles, Is.GreaterThan(0));
            Assert.That(cycles, Is.EqualTo(Total(session)));
        }

        [Test]
        public void TheDeltaSumEqualsTheEngineCompletionsOnW005L()
        {
            RunCatalog catalog = W005LWorkload.LoadShippedCatalog();
            RunSession session = W005LWorkload.Build(catalog).Session;
            RunSession reference = W005LWorkload.Build(catalog).Session;
            var sim = new RunSimulator(catalog);
            long previous = Total(session), deltas = 0, engine = 0, cursor = reference.ActiveTicksSinceAnchor;
            Assert.That(previous, Is.GreaterThan(0), "the build advanced the session, so it has counted cycles");

            foreach (long step in W005Steps)
            {
                Assert.That(session.Advance(step).IsSuccess, Is.True);
                long delta = Delta(session, ref previous);
                deltas += delta;
                Assert.That(RunSimHarness.AdvanceAt(sim, reference.State, reference.Permanent, W005LWorkload.StartUtcMs, cursor, step),
                    Is.EqualTo(RunAdvanceResult.Ok));
                engine += sim.LastCompletions;
                cursor += step;
                Assert.That(delta, Is.EqualTo(sim.LastCompletions), "W005-L step " + step);
            }

            Assert.That(engine, Is.GreaterThan(0));
            Assert.That(deltas, Is.EqualTo(engine));
            Assert.That(SaveV2Scenarios.Dump(session.State, session.Permanent), Is.EqualTo(SaveV2Scenarios.Dump(reference.State, reference.Permanent)),
                "the oracle is faithful: both runs reached the same state");
        }

        [Test]
        public void AfterARestoreAndAnOfflineReturnTheDeltasAgainEqualTheEngineCompletions()
        {
            DurabilityRig live = DurabilityRig.Suspended();
            DurabilityRig reference = DurabilityRig.Suspended();
            Assert.That(live.Persistence.ResumeAndReconcile().IsSuccess, Is.True);
            Assert.That(reference.Persistence.ResumeAndReconcile().IsSuccess, Is.True);
            Assert.That(Total(live.Session), Is.Zero, "the restore starts empty; the offline completions are the summary's");

            var sim = new RunSimulator(reference.Rig.Catalog);
            long anchor = reference.Session.AnchorUtcMs;
            Assert.That(live.Session.AnchorUtcMs, Is.EqualTo(anchor));
            long previous = 0, deltas = 0, engine = 0, cursor = 0;
            foreach (long step in new long[] { 10_000_000, 25_000_000, 1_000_000, 400_000_000 })
            {
                Assert.That(live.Session.Advance(step).IsSuccess, Is.True);
                deltas += Delta(live.Session, ref previous);
                Assert.That(RunSimHarness.AdvanceAt(sim, reference.Session.State, reference.Session.Permanent, anchor, cursor, step),
                    Is.EqualTo(RunAdvanceResult.Ok));
                engine += sim.LastCompletions;
                cursor += step;
            }

            Assert.That(engine, Is.GreaterThan(0));
            Assert.That(deltas, Is.EqualTo(engine));
        }
    }
}
