using System.Collections;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

#if UNITY_EDITOR
namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T433 carry-forwards (#546): the bootstrap owns the lazy OS signal and a cached effective state, follows the preference store's
    /// <c>Changed</c> event (no convention), suspends the clock on pause, and mounts the Settings panel.
    /// </summary>
    public sealed class ReducedMotionBootstrapTests
    {
        private string root;
        private RunSceneClock clock;
        private RunSceneRun run;

        [SetUp]
        public void SetUp()
        {
            root = RunSceneRun.NewRoot();
            clock = new RunSceneClock();
            run = null;
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

        private sealed class CountingSignal : IReducedMotionSignal
        {
            public bool Reduce;
            public int Calls;
            public bool IsAvailable = true;

            public bool Available => IsAvailable;

            public bool ShouldReduceMotion()
            {
                Calls++;
                return Reduce;
            }
        }

        [UnityTest]
        public IEnumerator ChangingThePreferenceMakesTheAnimationClockFollowAndPauseSuspendsIt()
        {
            run = new RunSceneRun(root, clock);
            yield return run.Start();
            var bootstrap = run.Bootstrap;
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            var signal = new CountingSignal();
            bootstrap.UseReducedMotionSignal(signal);
            var store = bootstrap.DisplayPreferences;

            store.TrySave(ReducedMotionPreference.On);
            Assert.That(bootstrap.AnimationClock.ReducedMotion, Is.True, "the store's Changed event re-resolved the state");
            Assert.That(bootstrap.CurrentReducedMotion.Source, Is.EqualTo(ReducedMotionSource.Preference));

            store.TrySave(ReducedMotionPreference.Off);
            Assert.That(bootstrap.AnimationClock.ReducedMotion, Is.False);

            signal.Reduce = true;
            store.TrySave(ReducedMotionPreference.FollowSystem);
            Assert.That(bootstrap.AnimationClock.ReducedMotion, Is.True, "Follow system asks the OS signal");
            Assert.That(bootstrap.CurrentReducedMotion.Source, Is.EqualTo(ReducedMotionSource.System));

            var calls = signal.Calls;
            for (var i = 0; i < 120; i++)
            {
                var current = bootstrap.CurrentReducedMotion;
                Assert.That(current.Reduced, Is.True);
                yield return null;
            }

            Assert.That(signal.Calls, Is.EqualTo(calls), "the effective value is cached: no OS read per frame (FR-026)");

            bootstrap.SendMessage("OnApplicationPause", true);
            Assert.That(bootstrap.AnimationClock.Suspended, Is.True);
            bootstrap.SendMessage("OnApplicationPause", false);
            Assert.That(bootstrap.AnimationClock.Suspended, Is.False);
        }

        [UnityTest]
        public IEnumerator TheSystemSignalAvailabilityComesFromTheSignalsAvailableMember()
        {
            run = new RunSceneRun(root, clock);
            yield return run.Start();
            var bootstrap = run.Bootstrap;
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            var signal = new CountingSignal { IsAvailable = false };
            bootstrap.UseReducedMotionSignal(signal);
            Assert.That(bootstrap.SystemReducedMotionAvailable, Is.False, "a signal that reports itself unavailable is not asked for a platform type");
            signal.IsAvailable = true;
            Assert.That(bootstrap.SystemReducedMotionAvailable, Is.True);
            Assert.That(new ReducedMotionSignal().Available, Is.EqualTo(ReducedMotionSignal.PlatformAvailable));
        }

        [UnityTest]
        public IEnumerator DestroyingTheBootstrapUnsubscribesFromTheStoresChangedEvent()
        {
            run = new RunSceneRun(root, clock);
            yield return run.Start();
            var bootstrap = run.Bootstrap;
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            var signal = new CountingSignal();
            bootstrap.UseReducedMotionSignal(signal);
            var store = bootstrap.DisplayPreferences;
            store.TrySave(ReducedMotionPreference.FollowSystem);
            var calls = signal.Calls;
            Assert.That(calls, Is.GreaterThan(0), "while the bootstrap lives, a change re-resolves through the OS signal");

            yield return run.Stop();
            run = null;
            yield return null;

            store.TrySave(ReducedMotionPreference.On);
            store.TrySave(ReducedMotionPreference.FollowSystem);
            Assert.That(signal.Calls, Is.EqualTo(calls), "a destroyed bootstrap no longer reacts to the store (OnDestroy unsubscribed)");
        }

        [UnityTest]
        public IEnumerator TheSettingsPanelIsMountedAndTheGearOpensIt()
        {
            run = new RunSceneRun(root, clock);
            yield return run.Start();
            var bootstrap = run.Bootstrap;
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            Assert.That(bootstrap.PanelDiagnostics, Is.Empty);
            Assert.That(bootstrap.RunShell.Panels.IsRegistered(ShellPanelIds.Settings), Is.True);
            bootstrap.RunShell.OpenSettings();
            yield return null;
            Assert.That(bootstrap.RunShell.IsSettingsOpen, Is.True);
            Assert.That(bootstrap.RunShell.Root.Q<Button>("settings-radio-follow"), Is.Not.Null);
            Assert.That(bootstrap.RunShell.HandleBack(), Is.True);
        }
    }
}
#endif
