using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IdleFactory.Core.Application;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T110: the Save V2 lifecycle of the authored scene. A suspension writes a Save V2 checkpoint, a return reconciles
    /// the offline interval exactly once, an Android-style rotation (a focus-only departure that coincides with a screen
    /// change) creates no offline summary and no second reward, and a rotation cancels every open transient action
    /// without spending, placing or prestiging and shows the "Cancelled by rotation" notice (UX-9).
    /// </summary>
    public sealed class RunLifecycleTests
    {
        private const string MinerId = "machine.miner";
        private const string OreId = "iron.ore";

        private string root;
        private RunSceneClock clock;
        private RunSceneRun run;
        private LifecycleScreenConfiguration screen;
        private double realtime;

        [SetUp]
        public void CreateRoot()
        {
            root = RunSceneRun.NewRoot();
            clock = new RunSceneClock();
            run = null;
            screen = new LifecycleScreenConfiguration(360, 640, ScreenOrientation.Portrait);
            realtime = 100d;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (run != null)
            {
                yield return run.Stop();
            }

            RunSceneRun.DeleteRoot(root);
        }

#if UNITY_EDITOR
        private IEnumerator StartRun()
        {
            run = new RunSceneRun(root, clock);
            yield return run.Start();
            Assert.That(run.Bootstrap.IsInitialized, Is.True, run.Bootstrap.InitializationError);
            run.Bootstrap.RunLifecycle.UseScreenConfigurationSource(() => screen, () => realtime);
        }

        private CoreGameFoundationBootstrap Bootstrap => run.Bootstrap;

        private RunLifecycleAdapter Adapter => run.Bootstrap.RunLifecycle;

        private long Ore => Bootstrap.RunSession.State.Inventory.Units(OreId);

        private void BuildMiner()
        {
            var built = Bootstrap.RunCommands.Build(0, MinerId);
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
        }

        private void Rotate()
        {
            // Android reports a rotation as a focus loss with no pause, and the surface size changes around it.
            realtime += 10d;
            screen = new LifecycleScreenConfiguration(640, 360, ScreenOrientation.LandscapeLeft);
            Adapter.ObserveScreenConfiguration();
            Adapter.NotifyFocusChanged(false);
            clock.UtcNow = clock.UtcNow.AddMilliseconds(250);
            realtime += 0.2d;
            Adapter.NotifyFocusChanged(true);
        }

        [UnityTest]
        public IEnumerator ASuspensionWritesASaveV2CheckpointAndStopsTheHost()
        {
            yield return StartRun();
            BuildMiner();
            Assert.That(RunSceneRun.CaptureFiles(RunSceneRun.V2Directory(root)), Is.Empty, "Nothing is written before a departure.");
            var expected = RunSceneRun.Fingerprint(Bootstrap.RunSession);

            Adapter.NotifyPauseChanged(true);

            Assert.That(Adapter.LastResult.IsSuccess, Is.True, Adapter.LastResult.ReasonCode);
            Assert.That(Bootstrap.RunPersistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended));
            Assert.That(Bootstrap.RuntimeHost.enabled, Is.False, "The host stops before the departure is recorded.");
            Assert.That(RunSceneRun.CaptureFiles(RunSceneRun.V2Directory(root)), Is.Not.Empty);
            Assert.That(Directory.Exists(RunSceneRun.V1Directory(root)), Is.False, "Save V1 is never written.");

            var store = new JsonRunCheckpointStore(root, Bootstrap.RunCatalog);
            var read = store.ReadActive();
            Assert.That(read.Succeeded, Is.True, read.Message);
            Assert.That(read.Checkpoint.DepartureUtcMs, Is.EqualTo(clock.UnixMs));
            Assert.That(read.Checkpoint.Run.Machines.Select(machine => machine.MachineId), Is.EqualTo(new[] { MinerId }));
            Assert.That(read.Checkpoint.Run.RunId, Is.EqualTo(Bootstrap.RunSession.State.RunId));
            Assert.That(RunSceneRun.Fingerprint(Bootstrap.RunSession), Is.EqualTo(expected), "A departure changes no state.");
        }

        [UnityTest]
        public IEnumerator AReturnReconcilesTheOfflineIntervalExactlyOnce()
        {
            yield return StartRun();
            BuildMiner();
            var oreBefore = Ore;

            Adapter.NotifyPauseChanged(true);
            var departureCheckpoint = Bootstrap.RunPersistence.Status.ActiveCheckpointId;
            clock.UtcNow = clock.UtcNow.AddMinutes(10);
            Adapter.NotifyPauseChanged(false);

            Assert.That(Adapter.LastResult.IsSuccess, Is.True, Adapter.LastResult.ReasonCode);
            Assert.That(Adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.Return));
            Assert.That(Bootstrap.RunPersistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(Bootstrap.RuntimeHost.enabled, Is.True, "A successful return makes the host available again.");
            var oreAfterReturn = Ore;
            Assert.That(oreAfterReturn, Is.GreaterThan(oreBefore), "Ten minutes away credit Miner output.");
            Assert.That(Bootstrap.LastOfflineSummary, Is.Not.Null);
            var summary = Bootstrap.LastOfflineSummary;
            var afterReturn = RunSceneRun.Fingerprint(Bootstrap.RunSession);
            var activeAfterReturn = Bootstrap.RunPersistence.Status.ActiveCheckpointId;
            Assert.That(activeAfterReturn, Is.Not.EqualTo(departureCheckpoint), "The return promoted its own checkpoint.");

            // Duplicate callbacks of the same return reconcile nothing again.
            Adapter.NotifyPauseChanged(false);
            Adapter.NotifyFocusChanged(true);
            Assert.That(Ore, Is.EqualTo(oreAfterReturn));
            Assert.That(RunSceneRun.Fingerprint(Bootstrap.RunSession), Is.EqualTo(afterReturn));
            Assert.That(Bootstrap.RunPersistence.Status.ActiveCheckpointId, Is.EqualTo(activeAfterReturn));
            Assert.That(Bootstrap.LastOfflineSummary, Is.SameAs(summary));

            // A restart at the same instant selects the durable result and credits nothing more.
            yield return run.Stop();
            run = new RunSceneRun(root, clock);
            yield return run.Start();
            Assert.That(Bootstrap.IsInitialized, Is.True, Bootstrap.InitializationError);
            Assert.That(Ore, Is.EqualTo(oreAfterReturn));

            // A second absence credits only its own interval.
            Bootstrap.RunLifecycle.UseScreenConfigurationSource(() => screen, () => realtime);
            Adapter.NotifyPauseChanged(true);
            clock.UtcNow = clock.UtcNow.AddMinutes(5);
            Adapter.NotifyPauseChanged(false);
            var secondGain = Ore - oreAfterReturn;
            Assert.That(secondGain, Is.GreaterThan(0));
            Assert.That(secondGain, Is.LessThan(oreAfterReturn - oreBefore), "Five minutes credit less than the ten minutes before.");
        }

        [UnityTest]
        public IEnumerator ARotationWithoutDepartureCreatesNoOfflineSummaryAndNoSecondReward()
        {
            yield return StartRun();
            BuildMiner();
            var cash = Bootstrap.RunSession.State.Cash;
            var ore = Ore;
            Assert.That(Bootstrap.LastOfflineSummary, Is.Null);

            Rotate();

            Assert.That(Adapter.LastResult.IsSuccess, Is.True, Adapter.LastResult.ReasonCode);
            Assert.That(Adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.ConfigurationChange));
            Assert.That(Bootstrap.LastOfflineSummary, Is.Null, "A rotation the player did not leave through shows no offline summary.");
            Assert.That(Bootstrap.RunSession.State.Cash, Is.EqualTo(cash));
            Assert.That(Ore, Is.EqualTo(ore), "No reward is credited for the rotation.");
            Assert.That(Bootstrap.RunPersistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(Bootstrap.RuntimeHost.enabled, Is.True);
            var afterRotation = RunSceneRun.Fingerprint(Bootstrap.RunSession);

            // The duplicate focus callback of the same return does nothing.
            Adapter.NotifyFocusChanged(true);
            Assert.That(RunSceneRun.Fingerprint(Bootstrap.RunSession), Is.EqualTo(afterRotation));

            // A genuine absence after a rotation still reports its own summary once.
            realtime += 10d;
            Adapter.NotifyPauseChanged(true);
            clock.UtcNow = clock.UtcNow.AddMinutes(10);
            Adapter.NotifyPauseChanged(false);
            Assert.That(Adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.Return));
            Assert.That(Bootstrap.LastOfflineSummary, Is.Not.Null);
            Assert.That(Ore, Is.GreaterThan(ore));
        }

        [UnityTest]
        public IEnumerator ARotationCancelsTransientActionsWithoutSpendingPlacingOrPrestigingAndShowsTheNotice()
        {
            yield return StartRun();
            BuildMiner();
            var fingerprint = RunSceneRun.Fingerprint(Bootstrap.RunSession);
            var machines = Bootstrap.RunSession.State.Machines.Count;
            var cash = Bootstrap.RunSession.State.Cash;
            var ip = Bootstrap.RunSession.Permanent.Ip;

            var removeConfirmationOpen = true;
            var prestigeConfirmationOpen = true;
            var closedConfirmations = 0;
            using (Bootstrap.RegisterTransientAction(() =>
            {
                if (!removeConfirmationOpen) return false;
                removeConfirmationOpen = false;
                closedConfirmations++;
                return true;
            }))
            using (Bootstrap.RegisterTransientAction(() =>
            {
                if (!prestigeConfirmationOpen) return false;
                prestigeConfirmationOpen = false;
                closedConfirmations++;
                return true;
            }))
            {
                Rotate();

                Assert.That(removeConfirmationOpen, Is.False);
                Assert.That(prestigeConfirmationOpen, Is.False);
                Assert.That(closedConfirmations, Is.EqualTo(2));
                Assert.That(Bootstrap.Hud.Root.Q<Label>("status").text, Is.EqualTo("Cancelled by rotation"));
                Assert.That(Bootstrap.RunSession.State.Machines.Count, Is.EqualTo(machines), "Nothing was removed or placed.");
                Assert.That(Bootstrap.RunSession.State.Cash, Is.EqualTo(cash), "Nothing was spent.");
                Assert.That(Bootstrap.RunSession.State.RunNumber, Is.EqualTo(1), "Nothing was prestiged.");
                Assert.That(Bootstrap.RunSession.PendingTransition, Is.Null);
                Assert.That(Bootstrap.RunSession.Permanent.Ip, Is.EqualTo(ip));
                Assert.That(Bootstrap.LastOfflineSummary, Is.Null);

                // With nothing open the rotation shows no further notice.
                Bootstrap.Hud.SetState(FactoryHudState.Normal, "Iron factory run is ready.");
                Rotate();
                Assert.That(Bootstrap.Hud.Root.Q<Label>("status").text, Is.EqualTo("Iron factory run is ready."));
                Assert.That(closedConfirmations, Is.EqualTo(2));
            }

            // A live orientation change without any focus loss cancels the same way.
            var reopened = true;
            using (Bootstrap.RegisterTransientAction(() =>
            {
                if (!reopened) return false;
                reopened = false;
                return true;
            }))
            {
                Bootstrap.NotifyOrientationChanged(ScreenOrientation.LandscapeRight);
                Assert.That(reopened, Is.False);
                Assert.That(Bootstrap.Hud.Root.Q<Label>("status").text, Is.EqualTo("Cancelled by rotation"));
            }

            Assert.That(RunSceneRun.Fingerprint(Bootstrap.RunSession).Split('|').Take(3),
                Is.EqualTo(fingerprint.Split('|').Take(3)), "Run identity and Cash are unchanged.");
        }

        [UnityTest]
        public IEnumerator ADisposedTransientActionIsNotCancelled()
        {
            yield return StartRun();
            var called = 0;
            var registration = Bootstrap.RegisterTransientAction(() =>
            {
                called++;
                return true;
            });
            registration.Dispose();

            Assert.That(Bootstrap.CancelTransientActionsForRotation(), Is.Zero);
            Assert.That(called, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TheHostAdvancesOnlyWhileAMachineCanProduceOrATimerIsPending()
        {
            yield return StartRun();
            var session = Bootstrap.RunSession;
            Assert.That(Bootstrap.RuntimeHost.RunHasWork(), Is.False, "An empty factory has nothing to advance.");

            clock.CumulativeActiveSeconds = 5d;
            yield return null;
            Assert.That(session.ActiveTicksSinceAnchor, Is.Zero, "The idle interval is dropped, not simulated.");

            BuildMiner();
            Assert.That(Bootstrap.RuntimeHost.RunHasWork(), Is.True);
            clock.CumulativeActiveSeconds = 8d;
            yield return null;
            Assert.That(session.ActiveTicksSinceAnchor, Is.EqualTo(3L * TimeSpan.TicksPerSecond),
                "Only the interval since the build is advanced; the idle five seconds are not replayed.");
        }

        [UnityTest]
        public IEnumerator ColdStartAndReturnLogOneStaticTimingLineEach()
        {
            var timing = new Regex("^ResumeAndReconcile ms=[0-9]+$");
            LogAssert.Expect(LogType.Log, timing);
            yield return StartRun();

            LogAssert.Expect(LogType.Log, timing);
            Adapter.NotifyPauseChanged(true);
            clock.UtcNow = clock.UtcNow.AddMinutes(1);
            Adapter.NotifyPauseChanged(false);
            Assert.That(Adapter.LastResult.IsSuccess, Is.True, Adapter.LastResult.ReasonCode);
        }
#else
        [Test]
        public void TheAuthoredSceneLifecycleTestsRunInTheEditor()
        {
            Assert.Ignore("The Feature 003 composition seam is available in the Editor only.");
        }
#endif
    }
}
