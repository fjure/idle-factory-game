using System;
using System.Collections;
using IdleFactory.Core.Application;
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

            // The run continues while the window is unfocused, and an offline-length wall-clock gap is not credited as one.
            var oreAtLoss = Ore;
            clock.CumulativeActiveSeconds = 6d;
            clock.UtcNow = clock.UtcNow.AddMinutes(10);
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

            Send("OnApplicationFocus", true);
            Send("OnApplicationFocus", false);
            Assert.That(Adapter.SafetyCheckpointAttempts, Is.EqualTo(2), "Each new focus loss writes again.");
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
