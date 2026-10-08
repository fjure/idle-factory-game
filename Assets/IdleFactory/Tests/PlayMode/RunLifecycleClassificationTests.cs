#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// The return classification of <see cref="RunLifecycleAdapter"/> (FR-036, T060 emulator evidence). T119 retired the
    /// Feature 001 <c>ApplicationLifecycleAdapter</c> and its <c>LifecyclePersistenceTests</c>; these cases keep the same
    /// classification rules covered on the Save V2 run path: a rotation the player did not leave is a configuration change,
    /// a focus-only loss without a screen change and any paused departure are genuine returns.
    /// </summary>
    public sealed class RunLifecycleClassificationTests
    {
        private static readonly LifecycleScreenConfiguration Portrait =
            new LifecycleScreenConfiguration(1080, 2400, ScreenOrientation.Portrait);

        private static readonly LifecycleScreenConfiguration LandscapeLeft =
            new LifecycleScreenConfiguration(2400, 1080, ScreenOrientation.LandscapeLeft);

        private readonly List<Object> objects = new List<Object>();
        private string root;
        private StatusPersistenceRig rig;
        private RunLifecycleAdapter adapter;
        private LifecycleScreenConfiguration screen;
        private double realtimeSeconds;

        [SetUp]
        public void CreateRun()
        {
            root = OfflineSummaryPanelTestSupport.TempRoot();
            rig = StatusPersistenceRig.Create(root, new StatusTestClock()).Start();
            rig.BuildMiner();
            var host = new GameObject("run-lifecycle-classification");
            objects.Add(host);
            adapter = host.AddComponent<RunLifecycleAdapter>();
            screen = Portrait;
            realtimeSeconds = 100d;
            adapter.Configure(rig.Persistence, null);
            adapter.UseScreenConfigurationSource(() => screen, () => realtimeSeconds);
        }

        [TearDown]
        public void DeleteRun()
        {
            foreach (var item in objects)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            objects.Clear();
            RunSceneRun.DeleteRoot(root);
        }

        [Test]
        public void ARotationThatRemovesFocusIsAConfigurationChangeAndALateOrientationUpdateStartsNoSecondReturn()
        {
            // The surface size is already rotated when focus is lost, no pause is reported, and Screen.orientation follows
            // one or two frames after focus returns.
            screen = new LifecycleScreenConfiguration(LandscapeLeft.Width, LandscapeLeft.Height, Portrait.Orientation);
            realtimeSeconds += 0.5d;
            adapter.NotifyFocusChanged(false);
            Assert.That(adapter.LastResult.IsSuccess, Is.True, adapter.LastResult.ReasonCode);

            adapter.NotifyFocusChanged(true);
            Assert.That(adapter.LastResult.IsSuccess, Is.True, adapter.LastResult.ReasonCode);
            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.ConfigurationChange));
            Assert.That(rig.Persistence.Lifecycle, Is.EqualTo(IdleFactory.Core.Application.LifecycleState.Running));

            // Count only the publications of these two synchronous calls, so no Editor focus callback can interleave.
            var published = 0;
            adapter.ResultChanged += (result, kind) => published++;
            screen = LandscapeLeft;
            adapter.ObserveScreenConfiguration();
            adapter.NotifyFocusChanged(true);
            Assert.That(published, Is.Zero, "The late orientation update must not start a second return.");
        }

        [Test]
        public void AConfigurationChangeObservedJustBeforeTheFocusLossIsClassifiedAsARotation()
        {
            // Rotation-lock release: the running frame already saw the new size and orientation before the rotation removed focus.
            realtimeSeconds = 200d;
            screen = LandscapeLeft;
            adapter.ObserveScreenConfiguration();
            realtimeSeconds = 200.02d;
            adapter.NotifyFocusChanged(false);
            realtimeSeconds = 200.07d;
            adapter.NotifyFocusChanged(true);
            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.ConfigurationChange));

            realtimeSeconds = 200.07d + RunLifecycleAdapter.ConfigurationChangeWindowSeconds + 5d;
            adapter.NotifyFocusChanged(false);
            adapter.NotifyFocusChanged(true);
            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.Return),
                "A focus loss long after the last rotation is not attributed to it.");
        }

        [Test]
        public void AFocusOnlyLossWithoutAScreenChangeRemainsAGenuineReturn()
        {
            // Notification shade or system dialog: focus only, no pause, no screen change.
            realtimeSeconds = 300d;
            adapter.NotifyFocusChanged(false);
            realtimeSeconds = 302.8d;
            adapter.NotifyFocusChanged(true);

            Assert.That(adapter.LastResult.IsSuccess, Is.True, adapter.LastResult.ReasonCode);
            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.Return));
        }

        [Test]
        public void APausedDepartureIsAGenuineReturnEvenWhenTheScreenRotatedMeanwhile()
        {
            // HOME and power lock: the pause precedes the focus loss, and the return reverses it.
            adapter.NotifyPauseChanged(true);
            adapter.NotifyFocusChanged(false);
            screen = LandscapeLeft;
            adapter.NotifyFocusChanged(true);
            adapter.NotifyPauseChanged(false);
            Assert.That(adapter.LastResult.IsSuccess, Is.True, adapter.LastResult.ReasonCode);
            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.Return));

            // A rotation that removes focus first and then leaves the app is also a genuine return once a pause is reported.
            screen = new LifecycleScreenConfiguration(Portrait.Width, Portrait.Height, screen.Orientation);
            realtimeSeconds += 0.5d;
            adapter.NotifyFocusChanged(false);
            adapter.NotifyPauseChanged(true);
            adapter.NotifyPauseChanged(false);
            adapter.NotifyFocusChanged(true);
            Assert.That(adapter.LastResult.IsSuccess, Is.True, adapter.LastResult.ReasonCode);
            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.Return));
        }
    }
}
#endif
