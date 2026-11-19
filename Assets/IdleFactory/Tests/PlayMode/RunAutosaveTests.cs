using System.Collections;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T559 (FR-003; owner decision 2026-11-18): the timer-driven autosave in the authored scene. A force-stop used to lose everything since
    /// the last departure unless the player pressed HOME first. Here the process dies without any pause, focus or quit callback and the next
    /// start restores the last periodic checkpoint and credits the absence from it, once. The cadence arithmetic and the lifecycle gating
    /// are in the Edit Mode <c>AutosaveTests</c>; this file covers the scene wiring (the lifecycle adapter ticks it), a failing store and the
    /// allocation-free steady frame.
    /// </summary>
    public sealed class RunAutosaveTests
    {
        private const string MinerId = "machine.miner";
        private const string OreId = "iron.ore";

        private string root;
        private RunSceneClock clock;
        private RunSceneRun run;
        private System.IDisposable platform;

        [SetUp]
        public void CreateRoot()
        {
            root = RunSceneRun.NewRoot();
            clock = new RunSceneClock();
            run = null;
            platform = null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (run != null)
            {
                yield return run.Stop();
            }

            platform?.Dispose();
            platform = null;
            RunSceneRun.DeleteRoot(root);
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator AKillWithoutAnyPauseCallback_RestoresTheLastPeriodicCheckpointAndCreditsTheAbsenceOnce([Values(false, true)] bool mobile)
        {
            yield return StartRun(mobile, null);
            BuildMiner();
            Assert.That(Adapter.AutosaveEnabled, Is.True, "the timer-driven autosave is on by default");
            clock.CumulativeActiveSeconds = 1d;
            yield return null;
            Assert.That(RunSceneRun.CaptureFiles(RunSceneRun.V2Directory(root)), Is.Empty, "the first frame only starts the cadence: nothing is written");

            // Thirty seconds of play with no focus change, no pause and no quit.
            Elapse(AutosavePolicy.DefaultIntervalMs);
            clock.CumulativeActiveSeconds = 31d;
            yield return null;
            yield return WaitForWrites(1);
            Assert.That(Adapter.AutosaveWrites, Is.EqualTo(1), "the periodic checkpoint was written");
            Assert.That(Adapter.SafetyCheckpointAttempts, Is.Zero, "no focus-loss checkpoint was involved");
            Assert.That(Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            var written = new JsonRunCheckpointStore(root, Bootstrap.RunCatalog).ReadActive();
            Assert.That(written.Succeeded, Is.True, written.Message);
            Assert.That(written.Checkpoint.Id, Is.EqualTo(Persistence.Status.ActiveCheckpointId));
            Assert.That(written.Checkpoint.Run.Machines, Is.Not.Empty, "the checkpoint holds the factory");
            var departure = written.Checkpoint.DepartureUtcMs;
            var oreAtCheckpoint = Ore;
            Assert.That(oreAtCheckpoint, Is.GreaterThan(0L), "the flush before the write brought the run up to date");

            // The second write continues from the generation the first one made, so it takes the background path: the main thread only
            // captures and serializes, a pool thread publishes, a later frame applies the result.
            Assert.That(Persistence.BackgroundWritesStarted, Is.Zero, "the first write of a fresh run is the synchronous one");
            Elapse(AutosavePolicy.DefaultIntervalMs);
            clock.CumulativeActiveSeconds = 61d;
            yield return null;
            var inFlightFrames = Persistence.AutosaveInFlight ? 1 : 0;
            yield return WaitForWrites(2);
            Assert.That(Adapter.AutosaveWrites, Is.EqualTo(2), "the second periodic checkpoint was written");
            Assert.That(Persistence.BackgroundWritesStarted, Is.EqualTo(1), "and it went through the background path (in flight at the first look: " + inFlightFrames + ")");
            Assert.That(Persistence.AutosaveInFlight, Is.False);
            written = new JsonRunCheckpointStore(root, Bootstrap.RunCatalog).ReadActive();
            Assert.That(written.Succeeded, Is.True, written.Message);
            Assert.That(written.Checkpoint.Id, Is.EqualTo(Persistence.Status.ActiveCheckpointId));
            Assert.That(written.Checkpoint.DepartureUtcMs, Is.GreaterThan(departure));
            departure = written.Checkpoint.DepartureUtcMs;
            oreAtCheckpoint = Ore;

            // Five more seconds are played and lost with the process: the next write is not due yet.
            Elapse(5_000);
            clock.CumulativeActiveSeconds = 66d;
            yield return null;
            Assert.That(Adapter.AutosaveWrites, Is.EqualTo(2));

            // The kill: no OnApplicationPause, no OnApplicationFocus, no OnApplicationQuit.
            yield return run.Stop();
            clock.UtcNow = clock.UtcNow.AddMinutes(10);
            var returnMs = clock.UnixMs;
            run = new RunSceneRun(root, clock);
            yield return run.Start();
            Assert.That(Bootstrap.IsInitialized, Is.True, Bootstrap.InitializationError);
            var summary = Bootstrap.LastOfflineSummary;
            Assert.That(summary, Is.Not.Null, "the absence is credited as an offline return");
            Assert.That(summary.DepartureUtcMs, Is.EqualTo(departure), "from the last (second, background) periodic checkpoint");
            Assert.That(summary.MeasuredMs, Is.EqualTo(returnMs - departure), "the unsaved five seconds are inside the credited absence");
            var oreAfterStart = Ore;
            Assert.That(oreAfterStart, Is.GreaterThan(oreAtCheckpoint));
            Assert.That(Bootstrap.RunSession.State.Machines, Is.Not.Empty, "the factory is restored");

            // The return is durable and linked to that checkpoint: starting again at the same instant credits nothing more.
            yield return run.Stop();
            run = new RunSceneRun(root, clock);
            yield return run.Start();
            Assert.That(Ore, Is.EqualTo(oreAfterStart), "credited exactly once");
        }

        [UnityTest]
        public IEnumerator ABuildIsWrittenAfterTheActionSpacingWithoutWaitingForTheInterval()
        {
            yield return StartRun(false, null);
            clock.CumulativeActiveSeconds = 1d;
            yield return null;
            Assert.That(Adapter.AutosaveWrites, Is.Zero);

            BuildMiner();
            Elapse(AutosavePolicy.DefaultActionSpacingMs - 1);
            yield return null;
            Assert.That(Adapter.AutosaveWrites, Is.Zero, "inside the spacing");
            Elapse(1);
            yield return null;
            yield return WaitForWrites(1);
            Assert.That(Adapter.AutosaveWrites, Is.EqualTo(1), "the action trigger fired long before the interval");
            Assert.That(new JsonRunCheckpointStore(root, Bootstrap.RunCatalog).ReadActive().Checkpoint.Run.Machines, Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator AFailingStoreKeepsTheRunLive_IsNotHammered_AndTheNextIntervalSucceeds()
        {
            var store = new SwitchableStore();
            yield return StartRun(false, store);
            BuildMiner();
            clock.CumulativeActiveSeconds = 1d;
            yield return null;
            var before = Persistence.Status.ActiveCheckpointId;

            store.Fail = true;
            Elapse(AutosavePolicy.DefaultIntervalMs);
            clock.CumulativeActiveSeconds = 31d;
            yield return null;
            Assert.That(Adapter.AutosaveFailures, Is.EqualTo(1));
            Assert.That(Adapter.LastAutosaveOutcome, Is.EqualTo(AutosaveTickOutcome.Failed));
            Assert.That(Persistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.Failure), "reported through the existing failure route");
            Assert.That(Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(Persistence.Status.ActiveCheckpointId, Is.EqualTo(before));
            var attempts = store.PromoteCalls;

            for (var frame = 0; frame < 5; frame++)
            {
                Elapse(1_000);
                clock.CumulativeActiveSeconds += 1d;
                yield return null;
            }

            Assert.That(store.PromoteCalls, Is.EqualTo(attempts), "no retry inside the interval");
            Assert.That(Adapter.AutosaveFailures, Is.EqualTo(1));

            store.Fail = false;
            Elapse(AutosavePolicy.DefaultIntervalMs);
            clock.CumulativeActiveSeconds += 30d;
            yield return null;
            Assert.That(Adapter.AutosaveWrites, Is.EqualTo(1));
            Assert.That(Persistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.Ready));
            Assert.That(Persistence.Status.ActiveCheckpointId, Is.Not.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator ASteadyFrameThatWritesNothingAllocatesNothing()
        {
            yield return StartRun(false, null);
            BuildMiner();
            clock.CumulativeActiveSeconds = 1d;
            yield return null;
            for (var i = 0; i < 50; i++)
            {
                Adapter.TickAutosave();
            }

            var allocations = GcAllocationCounter.Count(() =>
            {
                for (var i = 0; i < 2_000; i++)
                {
                    Adapter.TickAutosave();
                }
            });
            Assert.That(allocations, Is.Zero, "SC-008: the autosave tick of a frame that does not write costs one clock read");
            Assert.That(Adapter.AutosaveWrites, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AutosaveWritesNothingWhileASuspensionIsRecorded()
        {
            yield return StartRun(true, null);
            BuildMiner();
            clock.CumulativeActiveSeconds = 1d;
            yield return null;
            Adapter.NotifyPauseChanged(true);
            Assert.That(Persistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended));
            var departure = Persistence.Status.ActiveCheckpointId;

            Elapse(AutosavePolicy.DefaultIntervalMs * 2);
            yield return null;
            yield return null;

            Assert.That(Adapter.AutosaveWrites, Is.Zero, "the departure checkpoint owns the state while suspended");
            Assert.That(Persistence.Status.ActiveCheckpointId, Is.EqualTo(departure));
        }

        private IEnumerator StartRun(bool mobile, IRunCheckpointStore forwarding)
        {
            platform?.Dispose();
            platform = LifecyclePlatform.Override(mobile);
            run = new RunSceneRun(root, clock);
            if (forwarding is SwitchableStore switchable)
            {
                yield return run.Start(createRunStore: catalog =>
                {
                    switchable.Inner = new JsonRunCheckpointStore(root, catalog);
                    return switchable;
                });
            }
            else
            {
                yield return run.Start();
            }

            Assert.That(run.Bootstrap.IsInitialized, Is.True, run.Bootstrap.InitializationError);
        }

        // Waits for the result of an autosave write to be applied: the first write of a run is synchronous, later ones run their file IO on a
        // pool thread and a later frame's tick applies the result.
        private IEnumerator WaitForWrites(int expected)
        {
            var deadline = UnityEngine.Time.realtimeSinceStartup + 10f;
            while (Adapter.AutosaveWrites < expected && UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private CoreGameFoundationBootstrap Bootstrap => run.Bootstrap;

        private RunLifecycleAdapter Adapter => run.Bootstrap.RunLifecycle;

        private RunPersistence Persistence => Bootstrap.RunPersistence;

        private long Ore => Bootstrap.RunSession.State.Inventory.Units(OreId);

        private void Elapse(long milliseconds) => clock.UtcNow = clock.UtcNow.AddMilliseconds(milliseconds);

        private void BuildMiner()
        {
            var built = Bootstrap.RunCommands.Build(0, MinerId);
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
        }

        /// <summary>Forwards to the real store; <see cref="Fail"/> makes every promotion fail before anything is written.</summary>
        private sealed class SwitchableStore : IRunCheckpointStore
        {
            public IRunCheckpointStore Inner { get; set; }
            public bool Fail { get; set; }
            public int PromoteCalls { get; private set; }

            public RunCheckpointReadResult ReadActive() => Inner.ReadActive();
            public RunCheckpointReadResult ReadPrevious() => Inner.ReadPrevious();
            public RunCheckpointReadResult Read(CheckpointId checkpointId) => Inner.Read(checkpointId);

            public CommandResult<CheckpointId> Promote(RunCheckpoint checkpoint)
            {
                PromoteCalls++;
                return Fail
                    ? CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "injected rejection")
                    : Inner.Promote(checkpoint);
            }

            public CommandResult<CheckpointId> RecoverPrevious(RunCheckpoint recoveryCandidate) => Inner.RecoverPrevious(recoveryCandidate);
            public CommandResult<long> NextSupersedeGeneration() => Inner.NextSupersedeGeneration();
            public CommandResult<CheckpointId> SupersedeDamaged(RunCheckpoint checkpoint) => Inner.SupersedeDamaged(checkpoint);
            public CommandResult<RunCheckpointPresence> ProbeFiles() => Inner.ProbeFiles();
        }
#else
        [Test]
        public void TheAutosaveSceneTestsRunInTheEditor()
        {
            Assert.Ignore("The Feature 003 composition seam is available in the Editor only.");
        }
#endif
    }
}
