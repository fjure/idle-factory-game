using System;
using System.Collections;
using System.IO;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T250 (owner decision 2026-10-09, FR-025, US5): on a desktop player and in editor Play Mode, losing window focus is not
    /// a departure. The run keeps simulating, nothing reconciles and no summary opens; offline credit applies only after a
    /// regular shutdown or an unclean exit and the next start. The phone and tablet class keeps the FR-037 behaviour, which
    /// the existing lifecycle suites cover (the assembly runs as a mobile platform by default, see
    /// <c>LifecyclePlatformTestDefault</c>); one case here pins the seam itself.
    /// </summary>
    public sealed class RunDesktopLifecycleTests
    {
        private const string MinerId = "machine.miner";
        private const string OreId = "iron.ore";

        private string root;
        private RunSceneClock clock;
        private RunSceneRun run;
        private IDisposable platform;

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

        [Test]
        public void APublishedPrestigeBypassesTheIntervalAndRestartsIt()
        {
            var rig = RunRig.Create(root, RunPlayerFixtures.ScaledCatalog(), RunPlayerFixtures.StartUtcMs);
            rig.ColdStartOk(null);
            new RunJourneyPolicy(rig).PlayUntilEligible();
            rig.SyncClock();
            var interval = rig.Persistence.MinimumAutosaveIntervalMs;
            Assert.That(interval, Is.GreaterThan(1_000L));
            Assert.That(rig.Persistence.SaveNow().IsSuccess, Is.True);
            Assert.That(rig.Persistence.Autosave().Value.AutosaveThrottled, Is.True);
            Assert.That(rig.Commands.ConfirmPrestige().IsSuccess, Is.True);

            // Inside the interval and with a prestige pending, an autosave skips silently without being a throttle.
            rig.Clock.UnixMs += interval / 2;
            var pending = rig.Persistence.Autosave();
            Assert.That(pending.Value.WasNoOp, Is.True);
            Assert.That(pending.Value.AutosaveThrottled, Is.False);

            var promoted = rig.Commands.PromotePrestige();
            Assert.That(promoted.IsSuccess, Is.True, promoted.ReasonCode);
            Assert.That(rig.Session.State.RunNumber, Is.EqualTo(2), "The prestige is not throttled (SEC-5).");

            // Past the interval of the earlier save, inside the interval of the published prestige.
            rig.Clock.UnixMs += interval / 2 + interval / 4;
            Assert.That(rig.Persistence.Autosave().Value.AutosaveThrottled, Is.True, "The published prestige restarted the interval.");
            rig.Clock.UnixMs += interval;
            var after = rig.Persistence.Autosave();
            Assert.That(after.IsSuccess, Is.True, after.ReasonCode);
            Assert.That(after.Value.AutosaveThrottled, Is.False);
            Assert.That(after.Value.WasNoOp, Is.False);
        }

#if UNITY_EDITOR
        private IEnumerator StartRun(bool mobile)
        {
            platform?.Dispose();
            platform = LifecyclePlatform.Override(mobile);
            run = new RunSceneRun(root, clock);
            yield return run.Start();
            Assert.That(run.Bootstrap.IsInitialized, Is.True, run.Bootstrap.InitializationError);
        }

        private CoreGameFoundationBootstrap Bootstrap => run.Bootstrap;

        private RunLifecycleAdapter Adapter => run.Bootstrap.RunLifecycle;

        private RunPersistence Persistence => Bootstrap.RunPersistence;

        private CheckpointId? ActiveId => Persistence.Status.ActiveCheckpointId;

        private void Elapse(long milliseconds) => clock.UtcNow = clock.UtcNow.AddMilliseconds(milliseconds);

        /// <summary>One focus loss followed by the regain that re-arms the next loss.</summary>
        private void LoseAndRegainFocus()
        {
            Send("OnApplicationFocus", false);
            Send("OnApplicationFocus", true);
        }

        private long Ore => Bootstrap.RunSession.State.Inventory.Units(OreId);

        private void BuildMiner()
        {
            var built = Bootstrap.RunCommands.Build(0, MinerId);
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
        }

        /// <summary>Delivers a Unity callback to every behaviour that handles it, like the engine does.</summary>
        private void Send(string callback, bool value)
        {
            Bootstrap.gameObject.SendMessage(callback, value);
            if (Bootstrap.RuntimeHost.gameObject != Bootstrap.gameObject)
            {
                Bootstrap.RuntimeHost.gameObject.SendMessage(callback, value);
            }
        }

        private void SendQuit()
        {
            Bootstrap.gameObject.SendMessage("OnApplicationQuit");
            if (Bootstrap.RuntimeHost.gameObject != Bootstrap.gameObject)
            {
                Bootstrap.RuntimeHost.gameObject.SendMessage("OnApplicationQuit");
            }
        }

        private void AssertNoOfflineReturn(int publishedResults)
        {
            Assert.That(publishedResults, Is.Zero, "No lifecycle result is published: no departure and no return.");
            Assert.That(Bootstrap.LastOfflineSummary, Is.Null);
            Assert.That(Bootstrap.OfflinePanel == null || !Bootstrap.OfflinePanel.IsOpen, Is.True, "No offline summary opens.");
            Assert.That(Adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.None));
        }

        [UnityTest]
        public IEnumerator DesktopFocusLossKeepsTheRunLiveAndRecordsNoDeparture()
        {
            yield return StartRun(mobile: false);
            BuildMiner();
            var published = 0;
            Adapter.ResultChanged += (result, kind) => published++;
            var session = Bootstrap.RunSession;
            clock.CumulativeActiveSeconds = 2d;
            yield return null;
            var ticksAtLoss = session.ActiveTicksSinceAnchor;
            Assert.That(ticksAtLoss, Is.GreaterThan(0L));

            Send("OnApplicationFocus", false);

            Assert.That(Bootstrap.RunPersistence.Lifecycle, Is.EqualTo(LifecycleState.Running), "Focus loss does not suspend.");
            Assert.That(Bootstrap.RuntimeHost.enabled, Is.True);
            Assert.That(Bootstrap.RuntimeHost.IsApplicationLifecycleAvailable, Is.True);

            // The run continues while the window is unfocused. A wall-clock delta of exactly the live-gap threshold is simulated live
            // (FR-032, T337); a delta above it is an offline return, which LiveGapHostTests covers.
            var oreAtLoss = Ore;
            clock.CumulativeActiveSeconds = 6d;
            clock.UtcNow = clock.UtcNow.AddMilliseconds(Persistence.MinimumAutosaveIntervalMs);
            yield return null;
            Assert.That(session.ActiveTicksSinceAnchor - ticksAtLoss, Is.EqualTo(4L * TimeSpan.TicksPerSecond),
                "The four unfocused seconds are simulated live.");
            Assert.That(Ore, Is.GreaterThan(oreAtLoss));

            // Minimizing reports a pause only when Run In Background is off; a pause that arrives anyway is ignored too.
            Send("OnApplicationPause", true);
            Assert.That(Bootstrap.RunPersistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            clock.CumulativeActiveSeconds = 7d;
            yield return null;
            Assert.That(session.ActiveTicksSinceAnchor - ticksAtLoss, Is.EqualTo(5L * TimeSpan.TicksPerSecond));
            Send("OnApplicationPause", false);

            var beforeReturn = RunSceneRun.Fingerprint(session);
            var activeBeforeReturn = Bootstrap.RunPersistence.Status.ActiveCheckpointId;
            Send("OnApplicationFocus", true);

            Assert.That(RunSceneRun.Fingerprint(session), Is.EqualTo(beforeReturn), "Regaining focus reconciles nothing.");
            Assert.That(Bootstrap.RunPersistence.Status.ActiveCheckpointId, Is.EqualTo(activeBeforeReturn));
            Assert.That(Bootstrap.RunPersistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            AssertNoOfflineReturn(published);
        }

        [UnityTest]
        public IEnumerator DesktopFocusLossWritesOnePlainSafetyCheckpointWithoutDepartureSemantics()
        {
            yield return StartRun(mobile: false);
            BuildMiner();
            clock.CumulativeActiveSeconds = 3d;
            yield return null;
            Assert.That(RunSceneRun.CaptureFiles(RunSceneRun.V2Directory(root)), Is.Empty, "Nothing is written before the first focus loss.");
            var published = 0;
            Adapter.ResultChanged += (result, kind) => published++;

            Send("OnApplicationFocus", false);
            Send("OnApplicationFocus", false);

            Assert.That(Adapter.SafetyCheckpointAttempts, Is.EqualTo(1), "A repeated focus-lost callback writes once.");
            Assert.That(Adapter.LastSafetyCheckpoint.IsSuccess, Is.True, Adapter.LastSafetyCheckpoint.ReasonCode);
            Assert.That(Bootstrap.RunPersistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            var store = new JsonRunCheckpointStore(root, Bootstrap.RunCatalog);
            var read = store.ReadActive();
            Assert.That(read.Succeeded, Is.True, read.Message);
            Assert.That(read.Checkpoint.Id, Is.EqualTo(Bootstrap.RunPersistence.Status.ActiveCheckpointId));
            Assert.That(read.Checkpoint.Run.Machines, Is.Not.Empty, "The checkpoint holds the live factory.");

            // T306: a new focus loss writes again once the minimum interval has passed (the throttle is covered below).
            clock.UtcNow = clock.UtcNow.AddMilliseconds(Persistence.MinimumAutosaveIntervalMs);
            var firstId = Bootstrap.RunPersistence.Status.ActiveCheckpointId;
            Send("OnApplicationFocus", true);
            Send("OnApplicationFocus", false);
            Assert.That(Adapter.SafetyCheckpointAttempts, Is.EqualTo(2), "Each new focus loss is attempted.");
            Assert.That(Bootstrap.RunPersistence.Status.ActiveCheckpointId, Is.Not.EqualTo(firstId), "A new focus loss writes again after the interval.");
            AssertNoOfflineReturn(published);
        }

        [UnityTest]
        public IEnumerator DesktopRegularShutdownRecordsTheDepartureAndTheNextStartCreditsTheIntervalOnce()
        {
            yield return StartRun(mobile: false);
            BuildMiner();
            clock.CumulativeActiveSeconds = 2d;
            yield return null;
            Send("OnApplicationFocus", false);
            clock.CumulativeActiveSeconds = 5d;
            yield return null;

            SendQuit();

            Assert.That(Bootstrap.RunPersistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended), "A regular shutdown records the departure.");
            var store = new JsonRunCheckpointStore(root, Bootstrap.RunCatalog);
            var read = store.ReadActive();
            Assert.That(read.Succeeded, Is.True, read.Message);
            Assert.That(read.Checkpoint.DepartureUtcMs, Is.GreaterThanOrEqualTo(clock.UnixMs));
            var oreAtShutdown = Ore;

            yield return run.Stop();
            clock.UtcNow = clock.UtcNow.AddMinutes(10);
            run = new RunSceneRun(root, clock);
            yield return run.Start();
            Assert.That(Bootstrap.IsInitialized, Is.True, Bootstrap.InitializationError);
            var oreAfterStart = Ore;
            Assert.That(oreAfterStart, Is.GreaterThan(oreAtShutdown), "The ten minutes after the shutdown are credited.");
            Assert.That(Bootstrap.LastOfflineSummary, Is.Not.Null);

            // Starting again at the same instant selects the durable result and credits nothing more.
            yield return run.Stop();
            run = new RunSceneRun(root, clock);
            yield return run.Start();
            Assert.That(Ore, Is.EqualTo(oreAfterStart), "The interval is credited exactly once.");
        }

        [UnityTest]
        public IEnumerator AnUncleanDesktopExitCreditsFromTheLastSafetyCheckpointOnce()
        {
            yield return StartRun(mobile: false);
            BuildMiner();
            clock.CumulativeActiveSeconds = 2d;
            yield return null;
            Send("OnApplicationFocus", false);
            var oreAtCheckpoint = Ore;
            Assert.That(Adapter.LastSafetyCheckpoint.IsSuccess, Is.True, Adapter.LastSafetyCheckpoint.ReasonCode);

            // The process dies without any quit callback: whatever ran after the checkpoint is lost, then credited as offline time.
            clock.CumulativeActiveSeconds = 4d;
            yield return null;
            yield return run.Stop();
            clock.UtcNow = clock.UtcNow.AddMinutes(10);
            run = new RunSceneRun(root, clock);
            yield return run.Start();
            Assert.That(Bootstrap.IsInitialized, Is.True, Bootstrap.InitializationError);
            var oreAfterStart = Ore;
            Assert.That(oreAfterStart, Is.GreaterThan(oreAtCheckpoint));
            Assert.That(Bootstrap.LastOfflineSummary, Is.Not.Null);

            yield return run.Stop();
            run = new RunSceneRun(root, clock);
            yield return run.Start();
            Assert.That(Ore, Is.EqualTo(oreAfterStart), "The interval is credited exactly once.");
        }

        [UnityTest]
        public IEnumerator TheMobileClassStillRecordsADepartureOnFocusLossAndReconcilesTheReturn()
        {
            yield return StartRun(mobile: true);
            Adapter.UseScreenConfigurationSource(
                () => new LifecycleScreenConfiguration(360, 640, ScreenOrientation.Portrait), () => 100d);
            BuildMiner();
            var oreBefore = Ore;

            Send("OnApplicationFocus", false);

            Assert.That(Bootstrap.RunPersistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended));
            Assert.That(Bootstrap.RuntimeHost.enabled, Is.False, "The host stops before the departure is recorded.");
            Assert.That(Adapter.SafetyCheckpointAttempts, Is.Zero, "A departure is not a safety checkpoint.");

            clock.UtcNow = clock.UtcNow.AddMinutes(10);
            Send("OnApplicationFocus", true);

            Assert.That(Adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.Return));
            Assert.That(Ore, Is.GreaterThan(oreBefore));
            Assert.That(Bootstrap.LastOfflineSummary, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator TheShippedContentSetsTheMinimumIntervalToTheLiveGapThreshold()
        {
            yield return StartRun(mobile: false);
            Assert.That(Bootstrap.RunCatalog.RunStart.LiveGapThresholdMs, Is.EqualTo(60_000L));
            Assert.That(Persistence.MinimumAutosaveIntervalMs, Is.EqualTo(60_000L), "FR-033: the interval is the FR-032 threshold.");
        }

        [UnityTest]
        public IEnumerator AFocusLossInsideTheMinimumIntervalIsSkippedAndReportedAndTheNextOneAfterItWrites()
        {
            yield return StartRun(mobile: false);
            BuildMiner();
            var interval = Persistence.MinimumAutosaveIntervalMs;
            var published = 0;
            Adapter.ResultChanged += (result, kind) => published++;
            clock.CumulativeActiveSeconds = 2d;
            yield return null;

            Send("OnApplicationFocus", false);
            Assert.That(Adapter.LastSafetyCheckpoint.IsSuccess, Is.True, Adapter.LastSafetyCheckpoint.ReasonCode);
            Assert.That(Adapter.LastSafetyCheckpoint.Value.AutosaveThrottled, Is.False);
            var written = ActiveId;
            var generation = new JsonRunCheckpointStore(root, Bootstrap.RunCatalog).ReadActive().Checkpoint.Generation;

            // Just inside the interval: skipped, observable, and nothing new is durable.
            Send("OnApplicationFocus", true);
            Elapse(interval - 1);
            Send("OnApplicationFocus", false);
            var skipped = Adapter.LastSafetyCheckpoint;
            Assert.That(skipped.IsSuccess, Is.True, skipped.ReasonCode);
            Assert.That(skipped.Value.AutosaveThrottled, Is.True, "A skipped checkpoint reports its reason.");
            Assert.That(skipped.Value.WasNoOp, Is.True);
            Assert.That(ActiveId, Is.EqualTo(written));
            Assert.That(new JsonRunCheckpointStore(root, Bootstrap.RunCatalog).ReadActive().Checkpoint.Generation, Is.EqualTo(generation));

            // A skipped attempt does not move the interval: exactly the interval after the write, the next loss writes.
            Send("OnApplicationFocus", true);
            Elapse(1);
            Send("OnApplicationFocus", false);
            Assert.That(Adapter.LastSafetyCheckpoint.Value.AutosaveThrottled, Is.False);
            Assert.That(ActiveId, Is.Not.EqualTo(written));
            Assert.That(Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            AssertNoOfflineReturn(published);
        }

        [UnityTest]
        public IEnumerator AClockThatMovedBackwardsNeverThrottlesTheSafetyCheckpoint()
        {
            yield return StartRun(mobile: false);
            BuildMiner();
            LoseAndRegainFocus();
            var written = ActiveId;

            Elapse(-3_600_000);
            Send("OnApplicationFocus", false);

            Assert.That(Adapter.LastSafetyCheckpoint.IsSuccess, Is.True, Adapter.LastSafetyCheckpoint.ReasonCode);
            Assert.That(Adapter.LastSafetyCheckpoint.Value.AutosaveThrottled, Is.False, "A skew can only cost a write, never block one.");
            Assert.That(ActiveId, Is.Not.EqualTo(written));
        }

        [UnityTest]
        public IEnumerator AnExplicitSaveBypassesTheIntervalAndRestartsIt()
        {
            yield return StartRun(mobile: false);
            BuildMiner();
            var interval = Persistence.MinimumAutosaveIntervalMs;
            LoseAndRegainFocus();

            Elapse(interval / 2);
            var before = ActiveId;
            var saved = Persistence.SaveNow();
            Assert.That(saved.IsSuccess, Is.True, saved.ReasonCode);
            Assert.That(saved.Value.WasNoOp, Is.False, "An explicit save is never throttled.");
            Assert.That(ActiveId, Is.Not.EqualTo(before));

            // Past the interval of the FIRST safety checkpoint but inside the interval of the explicit save.
            Elapse(interval / 2 + interval / 4);
            var afterSave = ActiveId;
            Send("OnApplicationFocus", false);
            Assert.That(Adapter.LastSafetyCheckpoint.Value.AutosaveThrottled, Is.True, "The explicit save restarted the interval.");
            Assert.That(ActiveId, Is.EqualTo(afterSave));

            Send("OnApplicationFocus", true);
            Elapse(interval);
            Send("OnApplicationFocus", false);
            Assert.That(Adapter.LastSafetyCheckpoint.Value.AutosaveThrottled, Is.False);
            Assert.That(ActiveId, Is.Not.EqualTo(afterSave));
        }

        [UnityTest]
        public IEnumerator AQuitInsideTheIntervalStillWritesTheDeparture()
        {
            yield return StartRun(mobile: false);
            BuildMiner();
            Send("OnApplicationFocus", false);
            var safety = ActiveId;
            Elapse(1_000);
            clock.CumulativeActiveSeconds = 3d;
            yield return null;

            SendQuit();

            Assert.That(Persistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended));
            Assert.That(Adapter.LastResult.IsSuccess, Is.True, Adapter.LastResult.ReasonCode);
            Assert.That(Adapter.LastResult.Value.WasNoOp, Is.False, "Quit is not throttled (SEC-5).");
            Assert.That(ActiveId, Is.Not.EqualTo(safety), "The departure checkpoint is a new generation.");
            var read = new JsonRunCheckpointStore(root, Bootstrap.RunCatalog).ReadActive();
            Assert.That(read.Succeeded, Is.True, read.Message);
            Assert.That(read.Checkpoint.Id, Is.EqualTo(ActiveId.Value));
        }

        [UnityTest]
        public IEnumerator AFocusLostAfterTheQuitWritesNothingAndKeepsTheDeparture()
        {
            yield return StartRun(mobile: false);
            BuildMiner();
            Send("OnApplicationFocus", false);
            var attempts = Adapter.SafetyCheckpointAttempts;
            Send("OnApplicationFocus", true);
            Elapse(Persistence.MinimumAutosaveIntervalMs);

            SendQuit();
            var departure = ActiveId;
            var files = RunSceneRun.CaptureFiles(RunSceneRun.V2Directory(root));

            // Unity delivers a final focus-lost during shutdown: even past the interval it must not write.
            Elapse(Persistence.MinimumAutosaveIntervalMs);
            Send("OnApplicationFocus", false);

            Assert.That(Adapter.SafetyCheckpointAttempts, Is.EqualTo(attempts), "No safety checkpoint is attempted after the quit.");
            Assert.That(Persistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended));
            Assert.That(ActiveId, Is.EqualTo(departure));
            Assert.That(RunSceneRun.CaptureFiles(RunSceneRun.V2Directory(root)).Keys, Is.EquivalentTo(files.Keys));
        }

        [UnityTest]
        public IEnumerator AFocusLostThenQuitKeepsTheDepartureAndTheQuitIsRecordedOnce()
        {
            yield return StartRun(mobile: false);
            BuildMiner();
            Send("OnApplicationFocus", false);
            SendQuit();
            var departure = ActiveId;
            SendQuit();
            Send("OnApplicationFocus", true);
            Send("OnApplicationFocus", false);

            Assert.That(Persistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended));
            Assert.That(ActiveId, Is.EqualTo(departure), "A repeated quit and later focus changes write nothing.");
        }

        [UnityTest]
        public IEnumerator AFailingSafetyWriteKeepsTheRunLivePublishesNothingAndTheNextSaveSucceeds()
        {
            var store = new SwitchableWriteStore();
            yield return StartRunWithStore(store);
            BuildMiner();
            var published = 0;
            Adapter.ResultChanged += (result, kind) => published++;
            clock.CumulativeActiveSeconds = 2d;
            yield return null;
            var fingerprint = RunSceneRun.Fingerprint(Bootstrap.RunSession);
            var before = ActiveId;

            foreach (var failure in new[] { WriteFailure.Reject, WriteFailure.IoError, WriteFailure.Unexpected })
            {
                store.Failure = failure;
                Adapter.NotifyFocusChanged(true);
                Adapter.NotifyFocusChanged(false);

                Assert.That(Adapter.LastSafetyCheckpoint.IsSuccess, Is.False, failure + ": the write failed.");
                Assert.That(Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running), failure + ": the run stays live.");
                Assert.That(Bootstrap.RuntimeHost.enabled, Is.True);
                Assert.That(ActiveId, Is.EqualTo(before), failure + ": nothing new became durable.");
                Assert.That(RunSceneRun.CaptureFiles(RunSceneRun.V2Directory(root)), Is.Empty, failure + ": no file was written.");
                Assert.That(RunSceneRun.Fingerprint(Bootstrap.RunSession), Is.EqualTo(fingerprint), failure + ": the run is untouched.");
                AssertNoOfflineReturn(published);
            }

            // A failed write must not start the interval: the very next attempt is not throttled and succeeds.
            store.Failure = WriteFailure.None;
            Adapter.NotifyFocusChanged(true);
            Adapter.NotifyFocusChanged(false);
            Assert.That(Adapter.LastSafetyCheckpoint.IsSuccess, Is.True, Adapter.LastSafetyCheckpoint.ReasonCode);
            Assert.That(Adapter.LastSafetyCheckpoint.Value.AutosaveThrottled, Is.False);
            Assert.That(ActiveId, Is.Not.EqualTo(before));
            Assert.That(new JsonRunCheckpointStore(root, Bootstrap.RunCatalog).ReadActive().Succeeded, Is.True);
            Assert.That(Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            AssertNoOfflineReturn(published);
        }

        [UnityTest]
        public IEnumerator AThrowingStoreDoesNotEscapeAnyLifecycleCallback()
        {
            var store = new SwitchableWriteStore();
            yield return StartRunWithStore(store);
            BuildMiner();

            store.Failure = WriteFailure.Unexpected;
            Assert.DoesNotThrow(() => Adapter.NotifyFocusChanged(false));
            Assert.DoesNotThrow(() => Adapter.NotifyPauseChanged(true));
            Assert.DoesNotThrow(() => SendQuit(), "SendMessage would log an error if the callback threw.");
            Assert.That(Persistence.Lifecycle, Is.Not.EqualTo(LifecycleState.Bootstrapping));
        }

        private IEnumerator StartRunWithStore(SwitchableWriteStore store)
        {
            platform?.Dispose();
            platform = LifecyclePlatform.Override(false);
            run = new RunSceneRun(root, clock);
            yield return run.Start(createRunStore: catalog =>
            {
                store.Inner = new JsonRunCheckpointStore(root, catalog);
                return store;
            });
            Assert.That(run.Bootstrap.IsInitialized, Is.True, run.Bootstrap.InitializationError);
        }

        private enum WriteFailure
        {
            None,
            Reject,
            IoError,
            Unexpected,
        }

        /// <summary>Forwards to the real store; <see cref="Failure"/> makes every promotion fail before anything is written.</summary>
        private sealed class SwitchableWriteStore : IRunCheckpointStore
        {
            public IRunCheckpointStore Inner { get; set; }
            public WriteFailure Failure { get; set; }

            public RunCheckpointReadResult ReadActive() => Inner.ReadActive();
            public RunCheckpointReadResult ReadPrevious() => Inner.ReadPrevious();
            public RunCheckpointReadResult Read(CheckpointId checkpointId) => Inner.Read(checkpointId);

            public CommandResult<CheckpointId> Promote(RunCheckpoint checkpoint)
            {
                switch (Failure)
                {
                    case WriteFailure.Reject:
                        return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "injected rejection");
                    case WriteFailure.IoError:
                        throw new IOException("injected disk failure");
                    case WriteFailure.Unexpected:
                        throw new NullReferenceException("injected unexpected failure");
                    default:
                        return Inner.Promote(checkpoint);
                }
            }

            public CommandResult<CheckpointId> RecoverPrevious(RunCheckpoint recoveryCandidate) => Inner.RecoverPrevious(recoveryCandidate);
            public CommandResult<long> NextSupersedeGeneration() => Inner.NextSupersedeGeneration();
            public CommandResult<CheckpointId> SupersedeDamaged(RunCheckpoint checkpoint) => Inner.SupersedeDamaged(checkpoint);
            public CommandResult<RunCheckpointPresence> ProbeFiles() => Inner.ProbeFiles();
        }


        [Test]
        public void ThePlatformSeamFollowsTheOverrideAndRestoresThePreviousValue()
        {
            var outer = LifecyclePlatform.IsMobile;
            using (LifecyclePlatform.Override(!outer))
            {
                Assert.That(LifecyclePlatform.IsMobile, Is.EqualTo(!outer));
                Assert.That(LifecyclePlatform.IsDesktop, Is.EqualTo(outer));
                using (LifecyclePlatform.Override(outer))
                {
                    Assert.That(LifecyclePlatform.IsMobile, Is.EqualTo(outer));
                }

                Assert.That(LifecyclePlatform.IsMobile, Is.EqualTo(!outer));
            }

            Assert.That(LifecyclePlatform.IsMobile, Is.EqualTo(outer));
        }
#else
        [Test]
        public void TheDesktopLifecycleTestsRunInTheEditor()
        {
            Assert.Ignore("The Feature 003 composition seam is available in the Editor only.");
        }
#endif
    }
}
