using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// T559 (FR-003; owner decision 2026-11-18): the timer-driven autosave. <see cref="AutosavePolicy"/> is tested on plain milliseconds;
    /// <see cref="RunPersistence.AutosaveWhenDue"/> on the real persistence over the scripted store (<see cref="DurabilityRig"/>): the
    /// cadence and the action throttle, the lifecycle gating (no checkpoint of a half-initialised session), the failure path (the previous
    /// checkpoint stays, the cadence backs off), the offline accounting after a kill (credited once, from the last checkpoint) and the
    /// allocation-free steady path. The scene wiring and the kill without a pause callback are in the Play Mode <c>RunAutosaveTests</c>.
    /// </summary>
    public sealed class AutosaveTests
    {
        private const long Interval = AutosavePolicy.DefaultIntervalMs;
        private const long Spacing = AutosavePolicy.DefaultActionSpacingMs;

        // ---- the policy on plain numbers ---------------------------------------------------------------------------------------------

        [Test]
        public void TheDefaultsAreThirtySecondsAndTwoSeconds()
        {
            var policy = new AutosavePolicy();
            Assert.That(policy.IntervalMs, Is.EqualTo(30_000));
            Assert.That(policy.ActionSpacingMs, Is.EqualTo(2_000));
            Assert.That(new AutosavePolicy(0, -5).IntervalMs, Is.EqualTo(30_000), "an invalid interval falls back to the default");
            Assert.That(new AutosavePolicy(1_000, 60_000).ActionSpacingMs, Is.EqualTo(1_000), "the spacing never exceeds the interval");
        }

        [TestCase(Interval - 1, false)]
        [TestCase(Interval, true)]
        [TestCase(Interval + 1, true)]
        public void AQuantityOnlyChangeIsWrittenOnlyOnceTheIntervalHasElapsed(long elapsed, bool due) =>
            Assert.That(new AutosavePolicy().Decide(elapsed, false, true, false), Is.EqualTo(due ? AutosaveReason.Periodic : AutosaveReason.None));

        [TestCase(Spacing - 1, false)]
        [TestCase(Spacing, true)]
        [TestCase(Interval, true)]
        public void AStructuralChangeIsWrittenOnceTheSpacingHasElapsed(long elapsed, bool due) =>
            Assert.That(new AutosavePolicy().Decide(elapsed, true, true, false), Is.EqualTo(due ? AutosaveReason.Action : AutosaveReason.None));

        [Test]
        public void NothingChangedWritesNothingHoweverLongItIsBeen() =>
            Assert.That(new AutosavePolicy().Decide(10 * Interval, false, false, false), Is.EqualTo(AutosaveReason.None));

        [Test]
        public void AfterAFailedWriteEverythingWaitsTheFullInterval()
        {
            var policy = new AutosavePolicy();
            Assert.That(policy.Decide(Spacing, true, true, true), Is.EqualTo(AutosaveReason.None), "an action does not retry a failing store early");
            Assert.That(policy.Decide(Interval - 1, true, true, true), Is.EqualTo(AutosaveReason.None));
            Assert.That(policy.Decide(Interval, true, true, true), Is.EqualTo(AutosaveReason.Action));
        }

        [Test]
        public void ABackwardsClockNeverBlocksAWrite()
        {
            var policy = new AutosavePolicy();
            Assert.That(policy.Decide(-1, false, true, false), Is.EqualTo(AutosaveReason.Periodic));
            Assert.That(policy.Decide(-3_600_000, true, true, true), Is.EqualTo(AutosaveReason.Action));
            Assert.That(policy.Decide(-1, false, false, false), Is.EqualTo(AutosaveReason.None), "but an unchanged run still writes nothing");
        }

        // ---- the cadence on the real persistence -----------------------------------------------------------------------------------

        [Test]
        public void TheCadenceRunsFromTheLatestWrite_AndTheWriteIsDueOneIntervalAfterIt()
        {
            DurabilityRig h = Live();
            long generation = h.Active().Generation;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue));

            Step(h, Interval - 1);
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue));
            Step(h, 1);
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.Written));
            Assert.That(h.Active().Generation, Is.EqualTo(generation + 1));
        }

        [Test]
        public void WithoutAnyWriteInThisProcessTheFirstTickOnlyStartsTheCadence()
        {
            // A restart whose return installed a generation but wrote none of its own (the return candidate is promoted by the return path).
            DurabilityRig suspended = DurabilityRig.Suspended();
            long returnMs = suspended.Clock.UnixMs;
            DurabilityRig h = DurabilityRig.Restart(suspended, returnMs, 500);
            h.Persistence.AutosaveExecutor = work => work();
            DurabilityRig.Ok(h.Persistence.ResumeAndReconcile());
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            h.Rig.Advance(10_000_000);
            h.Backend.ResetCounters();

            h.Clock.UnixMs = returnMs + 5 * Interval;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue), "the first tick of a live run anchors; a run does not write at once");
            h.Clock.UnixMs += Interval - 1;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue));
            Assert.That(h.Backend.Calls, Is.EqualTo(0));
            h.Clock.UnixMs += 1;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.Written));
        }

        [Test]
        public void TheCheckpointIsAnOrdinaryGeneration_SameFilesSameFormatAndVersion()
        {
            DurabilityRig reference = Live();
            DurabilityRig.Ok(reference.Persistence.SaveNow());
            DurabilityRig h = Live();
            Tick(h);
            Step(h, Interval);
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.Written));

            Assert.That(h.Backend.Files.Keys.OrderBy(k => k.ToString()), Is.EqualTo(reference.Backend.Files.Keys.OrderBy(k => k.ToString())), "the same slot files");
            Assert.That(h.Active().Generation, Is.EqualTo(reference.Active().Generation), "the generation protocol is the SaveNow one");
            Assert.That(h.Active().OfflinePolicyRevision, Is.EqualTo(reference.Active().OfflinePolicyRevision));
            Assert.That(h.Active().ContentRevision, Is.EqualTo(reference.Active().ContentRevision));
            Assert.That(h.Active().ReconciledFromCheckpointId, Is.Null, "a periodic checkpoint is no return");
            long first = h.Active().Generation;
            Step(h, Interval);
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.Written));
            Assert.That(h.Active().Generation, Is.EqualTo(first + 1));
            Assert.That(h.Previous().Generation, Is.EqualTo(first), "the previous generation is retained");
        }

        [Test]
        public void AnUnchangedRunWritesNothing_NoStorageCallAtAll()
        {
            DurabilityRig h = Live();
            Tick(h);
            Step(h, Interval);
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.Written));
            h.Backend.ResetCounters();

            // The clock runs, the run does not change (no Advance, no command): nothing is dirty.
            for (int i = 0; i < 10; i++)
            {
                h.Clock.UnixMs += Interval;
                Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue));
            }

            Assert.That(h.Backend.Calls, Is.EqualTo(0));
        }

        [Test]
        public void AFactoryChangeIsWrittenAfterTheSpacing_NotBefore_AndABurstCostsOneWritePerSpacing()
        {
            DurabilityRig h = LiveWithCheckpoint(out long t0);
            long generation = h.Active().Generation;
            h.Backend.ResetCounters();

            h.Clock.UnixMs = t0 + 100;
            Assert.That(h.Rig.Build(2, RunCommandFixtures.Press).IsSuccess, Is.True);
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue), "inside the spacing since the last write");
            h.Clock.UnixMs = t0 + Spacing - 1;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue));
            Assert.That(h.Backend.Calls, Is.EqualTo(0));

            // A burst of actions while the write is pending: one write at the spacing holds all of them.
            Assert.That(h.Rig.Build(3, RunCommandFixtures.Assembler).IsSuccess, Is.True);
            h.Clock.UnixMs = t0 + Spacing;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.Written), "the action trigger, well before the interval");
            Assert.That(h.Active().Run.Machines.Count, Is.EqualTo(4), "the checkpoint holds both builds");
            Assert.That(h.Active().Generation, Is.EqualTo(generation + 1));

            // The next action waits the spacing again: never more often than once per spacing.
            Assert.That(h.Commands.UpgradeStorage().IsSuccess, Is.True);
            h.Clock.UnixMs = t0 + Spacing + Spacing - 1;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue));
            h.Clock.UnixMs = t0 + 2 * Spacing;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.Written));
            Assert.That(h.Active().Generation, Is.EqualTo(generation + 2));
        }

        [Test]
        public void ProductionAloneIsOnlyWrittenPeriodically()
        {
            DurabilityRig h = LiveWithCheckpoint(out long t0);
            long structure = h.Session.StructureRevision;

            Step(h, Spacing);
            Assert.That(h.Session.QuantityRevision, Is.Not.EqualTo(0), "the fixture produces");
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue), "no structural change: the action trigger does not fire");
            Step(h, Interval - Spacing - 1);
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue));
            Step(h, 1);
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.Written));
            Assert.That(h.Clock.UnixMs, Is.EqualTo(t0 + Interval));
            Assert.That(h.Session.StructureRevision, Is.EqualTo(structure));
        }

        [Test]
        public void TheBeforeWriteHookRunsOnlyWhenAWriteIsDue_AndMayCancelIt()
        {
            DurabilityRig h = Live();
            int calls = 0;
            Assert.That(Tick(h, () => calls++), Is.EqualTo(AutosaveTickOutcome.NotDue));
            Step(h, Interval - 1);
            Assert.That(Tick(h, () => calls++), Is.EqualTo(AutosaveTickOutcome.NotDue));
            Assert.That(calls, Is.EqualTo(0), "not due: the host is not asked to flush");

            Step(h, 1);
            Assert.That(Tick(h, () => calls++), Is.EqualTo(AutosaveTickOutcome.Written));
            Assert.That(calls, Is.EqualTo(1));

            // A preparation that leaves the running state (a reconciled live gap, a departure) cancels the write.
            Step(h, Interval);
            h.Backend.ResetCounters();
            Assert.That(Tick(h, () => DurabilityRig.Ok(h.Persistence.RecordSuspension())), Is.EqualTo(AutosaveTickOutcome.NotDue));
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended));
        }

        [Test]
        public void AThrowingPreparationBacksTheCadenceOffInsteadOfRepeatingEveryFrame()
        {
            DurabilityRig h = Live();
            Tick(h);
            Step(h, Interval);
            int calls = 0;
            Assert.Throws<InvalidOperationException>(() => h.Persistence.AutosaveWhenDue(() =>
            {
                calls++;
                throw new InvalidOperationException("injected");
            }));
            Assert.That(h.Persistence.AutosaveWhenDue(() => calls++), Is.EqualTo(AutosaveTickOutcome.NotDue), "the next frame does not retry");
            Assert.That(calls, Is.EqualTo(1));
            Step(h, Interval);
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.Written));
        }

        // ---- lifecycle gating (no checkpoint of a half-initialised session) ---------------------------------------------------------

        [Test]
        public void NothingIsWrittenBeforeTheColdStartHasFinished()
        {
            DurabilityRig h = DurabilityRig.Create(resume: false);
            Assert.That(h.Persistence.Lifecycle, Is.Not.EqualTo(LifecycleState.Running));
            h.Backend.ResetCounters();
            for (int i = 0; i < 5; i++)
            {
                h.Clock.UnixMs += Interval;
                Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue));
            }

            Assert.That(h.Backend.Files, Is.Empty, "no file of a session that never reached Running");
            Assert.That(h.Backend.Calls, Is.EqualTo(0));
        }

        [Test]
        public void NothingIsWrittenWhileSuspended_OrWhileAPrestigeIsPending()
        {
            DurabilityRig h = LiveWithCheckpoint(out _);
            Assert.That(h.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(h.Session.PendingTransition, Is.Not.Null);
            var before = h.Backend.Snapshot();
            h.Backend.ResetCounters();
            h.Clock.UnixMs += 10 * Interval;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue), "the transition owns the checkpoint");
            Assert.That(h.Backend.Calls, Is.EqualTo(0));
            foreach (var pair in before) Assert.That(h.Backend.Files[pair.Key], Is.EqualTo(pair.Value));
        }

        [Test]
        public void ASuspendedRunWritesNothing_AndTheCadenceRestartsAfterTheReturn()
        {
            DurabilityRig h = LiveWithCheckpoint(out _);
            Step(h, 5_000);
            DurabilityRig.Ok(h.Persistence.RecordSuspension());
            int calls = h.Backend.Calls;
            h.Clock.UnixMs += 10 * Interval;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue));
            Assert.That(h.Backend.Calls, Is.EqualTo(calls));

            DurabilityRig.Ok(h.Persistence.ResumeAndReconcile());
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            long generation = h.Active().Generation;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue), "a clean installed return has nothing to save");
            Assert.That(h.Active().Generation, Is.EqualTo(generation));
        }

        [Test]
        public void ATickAfterAWallClockJumpAboveTheLiveGapThresholdWritesNothing_TheHostsGapPathDecidesFirst()
        {
            DurabilityRig h = LiveWithCheckpoint(out long t0);
            Step(h, 10_000);
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue));
            h.Backend.ResetCounters();

            h.Clock.UnixMs += h.Persistence.LiveGapThresholdMs + 1;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue), "the sleep's tick");
            Assert.That(h.Backend.Calls, Is.EqualTo(0));
            Assert.That(h.Active().DepartureUtcMs, Is.EqualTo(t0), "the checkpoint still holds the departure of before the gap");

            h.Clock.UnixMs += 1_000;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.Written), "the next ordinary tick writes");
        }

        // ---- the failure path ------------------------------------------------------------------------------------------------------

        [Test]
        public void AFailedWriteKeepsThePreviousCheckpoint_ReportsThroughTheStatus_AndTheNextAttemptWaitsAFullInterval()
        {
            DurabilityRig h = LiveWithCheckpoint(out long t0);
            RunCheckpoint kept = h.Active();
            Step(h, Interval);

            h.Backend.FailAll = true;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.Failed));
            Assert.That(h.Persistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.Failure), "the existing failure route");
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running), "the run stays live");

            h.Backend.ResetCounters();
            h.Clock.UnixMs += Spacing;
            Assert.That(h.Rig.Build(2, RunCommandFixtures.Press).IsSuccess, Is.True, "a factory change while the store is broken");
            h.Clock.UnixMs += Spacing;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue), "not hammered: not even an action retries early");
            h.Clock.UnixMs += Interval - 2 * Spacing - 1;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue));
            Assert.That(h.Backend.Calls, Is.EqualTo(0));

            h.Backend.ClearFault();
            RunCheckpoint stillActive = h.Active();
            Assert.That(stillActive.Id, Is.EqualTo(kept.Id), "the previous checkpoint stays the active one");
            Assert.That(stillActive.Generation, Is.EqualTo(kept.Generation));
            Assert.That(stillActive.DepartureUtcMs, Is.EqualTo(t0));

            h.Clock.UnixMs += 1;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.Written), "recovers after the interval");
            Assert.That(h.Persistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.Ready));
            Assert.That(h.Active().Generation, Is.EqualTo(kept.Generation + 1));
            Assert.That(h.Active().Run.Machines.Count, Is.EqualTo(3), "it holds everything built meanwhile");
        }

        [Test]
        public void AFailureAtEveryBackendCallOfAnAutosave_NeverLeavesAnUnreadableActiveCheckpoint()
        {
            DurabilityRig clean = LiveWithCheckpoint(out _);
            Step(clean, Interval);
            clean.Backend.ResetCounters();
            Assert.That(Tick(clean), Is.EqualTo(AutosaveTickOutcome.Written));
            int calls = clean.Backend.Calls;
            Assert.That(calls, Is.GreaterThan(5));

            for (int i = 0; i < calls; i++)
            {
                DurabilityRig h = LiveWithCheckpoint(out _);
                long generation = h.Active().Generation;
                Step(h, Interval);
                h.Backend.ResetCounters();
                h.Backend.FailAtCall(i);
                AutosaveTickOutcome outcome = Tick(h);
                h.Backend.ClearFault();
                var active = h.Store.ReadActive();
                Assert.That(active.Succeeded, Is.True, "call " + i + ": the active checkpoint stays readable");
                Assert.That(active.Checkpoint.Generation, Is.EqualTo(generation).Or.EqualTo(generation + 1),
                    "call " + i + " (" + outcome + "): either the old or the new generation, never a torn one");
                Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running), "call " + i);
            }
        }

        // ---- offline accounting after a kill ---------------------------------------------------------------------------------------

        [Test]
        public void AfterAKill_TheReturnCreditsFromTheLastPeriodicCheckpointOnce_NeverTwice()
        {
            DurabilityRig h = Live();
            Tick(h);
            Step(h, Interval);
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.Written));
            RunCheckpoint periodic = h.Active();
            Assert.That(periodic.DepartureUtcMs, Is.EqualTo(h.Clock.UnixMs), "the kill point is the checkpoint instant");

            // Twenty more seconds are played, then the process is killed (no pause, no quit): that part is lost from the state.
            Step(h, 20_000);
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.NotDue));
            long returnMs = h.Clock.UnixMs + 3_600_000;
            string reference = DurabilityRig.ReferenceDump(h, periodic.Id, returnMs, out RunOfflineSummary expected);

            DurabilityRig restarted = DurabilityRig.Restart(h, returnMs, 500);
            RunPersistenceOutcome returned = DurabilityRig.Ok(restarted.Persistence.ResumeAndReconcile());

            Assert.That(returned.OfflineSummary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(returned.OfflineSummary.DepartureUtcMs, Is.EqualTo(periodic.DepartureUtcMs), "credited from the periodic checkpoint");
            Assert.That(returned.OfflineSummary.MeasuredMs, Is.EqualTo(returnMs - periodic.DepartureUtcMs), "the lost 20 s are inside the credited absence");
            Assert.That(returned.OfflineSummary.CreditedTicks, Is.EqualTo(expected.CreditedTicks));
            Assert.That(restarted.Dump(), Is.EqualTo(reference), "exactly the segmenter result over that checkpoint: nothing counted twice");

            // The return became a durable generation linked to the periodic checkpoint: a second start at the same instant credits nothing more.
            Assert.That(restarted.Active().ReconciledFromCheckpointId, Is.EqualTo(periodic.Id));
            string afterReturn = restarted.Dump();
            DurabilityRig again = DurabilityRig.Restart(restarted, returnMs, 600);
            RunPersistenceOutcome zero = DurabilityRig.Ok(again.Persistence.ResumeAndReconcile());
            Assert.That(zero.OfflineSummary.CreditedTicks, Is.EqualTo(0));
            Assert.That(again.Dump(), Is.EqualTo(afterReturn));
        }

        [Test]
        public void ARunWithPeriodicCheckpointsEndsInTheSameStateAsOneWithout_WhenKilledAtTheSameInstant()
        {
            // Without autosave the kill point is the start (or the last departure); with it the kill point is closer. The offline credit is
            // capped by the offline policy, so beyond the cap the two runs differ only by what the cap hides: here the absence is short
            // enough that the credit of the closer kill point equals what live play would have produced.
            DurabilityRig withAutosave = Live();
            Tick(withAutosave);
            Step(withAutosave, Interval);
            Assert.That(Tick(withAutosave), Is.EqualTo(AutosaveTickOutcome.Written));
            long killMs = withAutosave.Clock.UnixMs;

            DurabilityRig plain = Live();
            Step(plain, Interval);
            DurabilityRig.Ok(plain.Persistence.SaveNow());

            Assert.That(withAutosave.Clock.UnixMs, Is.EqualTo(plain.Clock.UnixMs));
            Assert.That(withAutosave.Dump(), Is.EqualTo(plain.Dump()), "the autosave changes no simulated state");
            Assert.That(withAutosave.Active().DepartureUtcMs, Is.EqualTo(killMs));
        }

        // ---- the background write --------------------------------------------------------------------------------------------------

        // Holds each started write until the test runs it, so a test can act while the write is in flight.
        private sealed class ManualExecutor
        {
            public readonly List<Action> Queued = new List<Action>();

            public void Execute(Action work) => Queued.Add(work);

            public void RunAll()
            {
                foreach (Action work in Queued.ToArray()) work();
                Queued.Clear();
            }
        }

        [Test]
        public void TheMainThreadTouchesNoFileWhenItStartsAWrite_AndAppliesTheResultOnALaterTick()
        {
            DurabilityRig h = LiveWithCheckpoint(out long t0);
            var executor = new ManualExecutor();
            h.Persistence.AutosaveExecutor = executor.Execute;
            long generation = h.Active().Generation;
            Step(h, Interval);
            h.Backend.ResetCounters();

            Assert.That(h.Persistence.AutosaveWhenDue(), Is.EqualTo(AutosaveTickOutcome.NotDue), "the write is started, its result is not known yet");
            Assert.That(h.Persistence.AutosaveInFlight, Is.True);
            Assert.That(executor.Queued.Count, Is.EqualTo(1));
            Assert.That(h.Backend.Calls, Is.EqualTo(0), "serialization only: no read, no write, no flush on the calling thread");

            // While it is in flight nothing else is started, however much changes.
            Assert.That(h.Rig.Build(2, RunCommandFixtures.Press).IsSuccess, Is.True);
            h.Clock.UnixMs += 10 * Spacing;
            Assert.That(h.Persistence.AutosaveWhenDue(), Is.EqualTo(AutosaveTickOutcome.NotDue));
            Assert.That(executor.Queued.Count, Is.EqualTo(1));
            Assert.That(h.Backend.Calls, Is.EqualTo(0));

            executor.RunAll();
            Assert.That(h.Active().Generation, Is.EqualTo(generation + 1), "the worker published the generation of the captured run");
            Assert.That(h.Active().Run.Machines.Count, Is.EqualTo(2), "the checkpoint is the run as captured at the tick, not the build made afterwards");
            Assert.That(h.Persistence.Status.ActiveCheckpointId, Is.Not.EqualTo(h.Active().Id), "not applied on the main thread yet");
            Assert.That(h.Persistence.AutosaveWhenDue(), Is.EqualTo(AutosaveTickOutcome.Written));
            Assert.That(h.Persistence.AutosaveInFlight, Is.False);
            Assert.That(h.Persistence.Status.ActiveCheckpointId, Is.EqualTo(h.Active().Id));
            Assert.That(h.Persistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.Ready));
            Assert.That(h.Active().DepartureUtcMs, Is.EqualTo(t0 + Interval), "the departure is the capture instant");

            // The build made while the write was in flight is a pending change: it is written at the next opportunity, once.
            h.Persistence.AutosaveExecutor = work => work();
            h.Clock.UnixMs += Spacing;
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.Written));
            Assert.That(h.Active().Run.Machines.Count, Is.EqualTo(3));
        }

        [Test]
        public void ACommandJoinsTheWriteInFlightFirst_SoGenerationsAndStatusFollowTheSequentialOrder()
        {
            DurabilityRig h = LiveWithCheckpoint(out _);
            long generation = h.Active().Generation;
            using (var release = new ManualResetEventSlim(false))
            {
                var workers = new List<Thread>();
                h.Persistence.AutosaveExecutor = work =>
                {
                    var thread = new Thread(() =>
                    {
                        release.Wait();
                        work();
                    });
                    workers.Add(thread);
                    thread.Start();
                };

                Step(h, Interval);
                Assert.That(h.Persistence.AutosaveWhenDue(), Is.EqualTo(AutosaveTickOutcome.NotDue));
                Assert.That(h.Persistence.AutosaveInFlight, Is.True);

                // The departure checkpoint of a pause is requested while the autosave has not even started its file IO.
                Task.Delay(150).ContinueWith(_ => release.Set());
                DurabilityRig.Ok(h.Persistence.RecordSuspension());

                Assert.That(h.Persistence.AutosaveInFlight, Is.False, "joined and applied before the command ran");
                Assert.That(h.Active().Generation, Is.EqualTo(generation + 2), "autosave first, departure second: never the same generation twice");
                Assert.That(h.Previous().Generation, Is.EqualTo(generation + 1));
                Assert.That(h.Persistence.Status.ActiveCheckpointId, Is.EqualTo(h.Active().Id));
                foreach (Thread worker in workers) Assert.That(worker.Join(5_000), Is.True);

                // And the return reads that departure, not the older autosave.
                DurabilityRig.Ok(h.Persistence.ResumeAndReconcile());
                Assert.That(h.Active().ReconciledFromCheckpointId, Is.Not.Null);
            }
        }

        [Test]
        public void AKillWhileTheWriteIsInFlight_LeavesThePreviousCheckpointIntact()
        {
            DurabilityRig h = LiveWithCheckpoint(out long t0);
            var executor = new ManualExecutor();
            h.Persistence.AutosaveExecutor = executor.Execute;
            RunCheckpoint kept = h.Active();
            Step(h, Interval);
            Assert.That(h.Persistence.AutosaveWhenDue(), Is.EqualTo(AutosaveTickOutcome.NotDue));
            Assert.That(executor.Queued.Count, Is.EqualTo(1), "started, never run: the process dies first");

            long returnMs = h.Clock.UnixMs + 3_600_000;
            DurabilityRig restarted = DurabilityRig.Restart(h, returnMs, 700);
            RunPersistenceOutcome returned = DurabilityRig.Ok(restarted.Persistence.ResumeAndReconcile());
            Assert.That(returned.OfflineSummary.DepartureUtcMs, Is.EqualTo(t0), "the last complete checkpoint");
            Assert.That(restarted.Active().ReconciledFromCheckpointId, Is.EqualTo(kept.Id));
        }

        [Test]
        public void AFailureOfTheBackgroundWrite_IsReportedOnTheNextTick_AndTheNextWriteReadsTheStoreAgain()
        {
            DurabilityRig h = LiveWithCheckpoint(out _);
            RunCheckpoint kept = h.Active();
            Step(h, Interval);
            h.Backend.FailAll = true;
            Assert.That(h.Persistence.AutosaveWhenDue(), Is.EqualTo(AutosaveTickOutcome.NotDue), "inline: failed already, not yet reported");
            Assert.That(h.Persistence.Status.Kind, Is.Not.EqualTo(PersistenceStatusKind.Failure));
            Assert.That(h.Persistence.AutosaveWhenDue(), Is.EqualTo(AutosaveTickOutcome.Failed));
            Assert.That(h.Persistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.Failure));
            h.Backend.ClearFault();
            Assert.That(h.Active().Id, Is.EqualTo(kept.Id));

            // The cadence backs off a full interval; the retry then writes through the synchronous path (it reads the store for the generation).
            h.Clock.UnixMs += Interval - 1;
            Assert.That(h.Persistence.AutosaveWhenDue(), Is.EqualTo(AutosaveTickOutcome.NotDue));
            h.Clock.UnixMs += 1;
            h.Backend.ResetCounters();
            Assert.That(h.Persistence.AutosaveWhenDue(), Is.EqualTo(AutosaveTickOutcome.Written), "same tick: synchronous");
            Assert.That(h.Backend.Calls, Is.GreaterThan(0));
            Assert.That(h.Active().Generation, Is.EqualTo(kept.Generation + 1));
        }

        [Test]
        public void AStoreWithoutTheBackgroundCapability_WritesSynchronously_WithTheSameResult()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(SaveV2Scenarios.Options(), 50_000_000);
            var backend = new ScriptedBackend();
            var inner = new IdleFactory.Unity.Persistence.JsonRunCheckpointStore("memory-root", rig.Catalog, backend);
            var clock = new FixedUtcClock(rig.Session.TimelineUtcMs);
            var persistence = new RunPersistence(rig.Session, new ForwardingStore(inner), clock, new SequenceCheckpointIds(), new OfflineRunReturnReconciler(rig.Catalog));
            DurabilityRig.Ok(persistence.ResumeAndReconcile());
            Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            DurabilityRig.Ok(persistence.SaveNow());
            long generation = inner.ReadActive().Checkpoint.Generation;

            Assert.That(persistence.AutosaveWhenDue(), Is.EqualTo(AutosaveTickOutcome.NotDue));
            clock.UnixMs += Interval;
            Assert.That(rig.Build(1, RunCommandFixtures.Smelter).IsSuccess, Is.True);
            Assert.That(persistence.AutosaveWhenDue(), Is.EqualTo(AutosaveTickOutcome.Written), "the result is known on the same tick");
            Assert.That(persistence.AutosaveInFlight, Is.False);
            Assert.That(inner.ReadActive().Checkpoint.Generation, Is.EqualTo(generation + 1));
        }

        [Test]
        public void ManyWritesOnARealThreadWhileTheRunKeepsChanging_AllLandInOrder_AndTheStoreStaysReadable()
        {
            string root = RunCheckpointTestSupport.NewTempRoot();
            try
            {
                RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(SaveV2Scenarios.Options(), 50_000_000);
                var store = new IdleFactory.Unity.Persistence.JsonRunCheckpointStore(root, rig.Catalog);
                var clock = new FixedUtcClock(rig.Session.TimelineUtcMs);
                var persistence = new RunPersistence(rig.Session, store, clock, new SequenceCheckpointIds(), new OfflineRunReturnReconciler(rig.Catalog));
                DurabilityRig.Ok(persistence.ResumeAndReconcile());
                Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
                Assert.That(rig.Build(1, RunCommandFixtures.Smelter).IsSuccess, Is.True);
                DurabilityRig.Ok(persistence.SaveNow());
                long first = store.ReadActive().Checkpoint.Generation;
                int written = 0;

                for (int round = 0; round < 12; round++)
                {
                    clock.UnixMs += Interval;
                    rig.Advance(Interval * RunSession.TicksPerMs);
                    var timeout = DateTime.UtcNow.AddSeconds(20);
                    AutosaveTickOutcome outcome = persistence.AutosaveWhenDue();
                    while (outcome == AutosaveTickOutcome.NotDue && persistence.AutosaveInFlight && DateTime.UtcNow < timeout)
                    {
                        // The main thread keeps simulating while the worker publishes the captured bytes.
                        rig.Advance(10_000);
                        Thread.Sleep(1);
                        outcome = persistence.AutosaveWhenDue();
                    }

                    Assert.That(outcome, Is.Not.EqualTo(AutosaveTickOutcome.Failed), "round " + round);
                    if (outcome == AutosaveTickOutcome.Written) written++;
                }

                Assert.That(written, Is.EqualTo(12));
                RunCheckpointReadResult active = store.ReadActive();
                Assert.That(active.Succeeded, Is.True, active.Message);
                Assert.That(active.Checkpoint.Generation, Is.EqualTo(first + 12));
                Assert.That(store.ReadPrevious().Checkpoint.Generation, Is.EqualTo(first + 11));
                Assert.That(persistence.Status.ActiveCheckpointId, Is.EqualTo(active.Checkpoint.Id));
                Assert.That(persistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.Ready));
            }
            finally
            {
                RunCheckpointTestSupport.DeleteQuietly(root);
            }
        }

        // Forwards to a store but does not offer the background capability.
        private sealed class ForwardingStore : IRunCheckpointStore
        {
            private readonly IRunCheckpointStore inner;

            public ForwardingStore(IRunCheckpointStore inner) => this.inner = inner;

            public RunCheckpointReadResult ReadActive() => inner.ReadActive();
            public RunCheckpointReadResult ReadPrevious() => inner.ReadPrevious();
            public RunCheckpointReadResult Read(CheckpointId checkpointId) => inner.Read(checkpointId);
            public CommandResult<CheckpointId> Promote(RunCheckpoint checkpoint) => inner.Promote(checkpoint);
            public CommandResult<CheckpointId> RecoverPrevious(RunCheckpoint recoveryCandidate) => inner.RecoverPrevious(recoveryCandidate);
            public CommandResult<long> NextSupersedeGeneration() => inner.NextSupersedeGeneration();
            public CommandResult<CheckpointId> SupersedeDamaged(RunCheckpoint checkpoint) => inner.SupersedeDamaged(checkpoint);
            public CommandResult<RunCheckpointPresence> ProbeFiles() => inner.ProbeFiles();
        }

        // ---- the steady path allocates nothing -------------------------------------------------------------------------------------

        [Test]
        public void ATickThatWritesNothingAllocatesNothing()
        {
            DurabilityRig h = LiveWithCheckpoint(out long t0);
            Action noFlush = null;

            // Warm the path (JIT, first reads), then count the allocations of 5,000 ticks of each kind of non-writing frame.
            for (int i = 0; i < 100; i++) h.Persistence.AutosaveWhenDue(noFlush);

            h.Clock.UnixMs = t0 + 1_000;
            int clean = GcAllocationCounter.Count(() =>
            {
                for (int i = 0; i < 5_000; i++) h.Persistence.AutosaveWhenDue(noFlush);
            });
            Assert.That(clean, Is.EqualTo(0), "a clean run, interval not elapsed");

            Step(h, 1_000);
            Assert.That(h.Session.QuantityRevision, Is.Not.EqualTo(0));
            h.Clock.UnixMs = t0 + 2_000 + 5_000;
            int dirty = GcAllocationCounter.Count(() =>
            {
                for (int i = 0; i < 5_000; i++) h.Persistence.AutosaveWhenDue(noFlush);
            });
            Assert.That(dirty, Is.EqualTo(0), "a changing run, interval not elapsed");
        }

        // ---- helpers ---------------------------------------------------------------------------------------------------------------

        // A live, played run whose clock sits at its timeline, Running, with a clean durable baseline written by hand at that instant (the
        // cadence runs from the latest write, whoever made it).
        private static DurabilityRig Live()
        {
            DurabilityRig h = DurabilityRig.Create();
            // The play of DurabilityRig.Play without Overdrive: its end and cooldown are structural facts that would fire the action trigger.
            Assert.That(h.Rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            Assert.That(h.Rig.Build(1, RunCommandFixtures.Smelter).IsSuccess, Is.True);
            Assert.That(h.Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            Assert.That(h.Commands.UpgradeStorage().IsSuccess, Is.True);
            h.Rig.Advance(13_000_000);
            h.Sync();
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            DurabilityRig.Ok(h.Persistence.SaveNow());
            h.Persistence.AutosaveExecutor = work => work();
            return h;
        }

        // A live run with one periodic checkpoint written at the clock instant <paramref name="t0"/> and a clean state.
        private static DurabilityRig LiveWithCheckpoint(out long t0)
        {
            DurabilityRig h = Live();
            Tick(h);
            Step(h, Interval);
            Assert.That(Tick(h), Is.EqualTo(AutosaveTickOutcome.Written));
            t0 = h.Clock.UnixMs;
            return h;
        }

        private static AutosaveTickOutcome Tick(DurabilityRig h, Action beforeWrite = null)
        {
            // With the inline executor a background write has already finished when the tick that started it returns; the next tick applies it.
            AutosaveTickOutcome outcome = h.Persistence.AutosaveWhenDue(beforeWrite);
            if (outcome == AutosaveTickOutcome.NotDue && h.Persistence.AutosaveInFlight) outcome = h.Persistence.AutosaveWhenDue(beforeWrite);
            return outcome;
        }

        // Real time passes: the clock and the simulation move together (the host's live frames).
        private static void Step(DurabilityRig h, long milliseconds)
        {
            h.Clock.UnixMs += milliseconds;
            h.Rig.Advance(milliseconds * RunSession.TicksPerMs);
        }
    }
}
