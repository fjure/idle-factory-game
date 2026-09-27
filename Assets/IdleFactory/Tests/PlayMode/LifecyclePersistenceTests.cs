using System;
using System.Collections;
using System.Collections.Generic;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace IdleFactory.Tests.PlayMode
{
    public sealed class LifecyclePersistenceTests
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private Scene loadedScene;
        private SceneCompositionTestScope compositionScope;
        private CoreGameFoundationBootstrap bootstrap;
        private GameObject adapterObject;
        private ApplicationLifecycleAdapter adapter;
        private FactorySession session;
        private PersistenceCommands persistenceCommands;
        private ProductionCommands productionCommands;
        private TestUtcClock clock;
        private TestCheckpointStore store;
        private GameObject presenterObject;
        private LifecycleScreenConfiguration screen;
        private double realtimeSeconds;

        [UnitySetUp]
        public IEnumerator LoadDevelopmentCatalog()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The development catalog is loaded through its Editor scene harness.");
            yield break;
#else
            compositionScope = SceneCompositionTestScope.WithSeededEmptyCheckpoint();
            EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            while (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
            {
                yield return null;
            }

            yield return null;
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            var rootObjects = loadedScene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                bootstrap = root.GetComponentInChildren<CoreGameFoundationBootstrap>(true);
                if (bootstrap != null)
                {
                    break;
                }
            }

            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
#endif
        }

        [UnityTearDown]
        public IEnumerator UnloadDevelopmentCatalog()
        {
            if (presenterObject != null)
            {
                UnityEngine.Object.Destroy(presenterObject);
            }

            if (adapterObject != null)
            {
                UnityEngine.Object.Destroy(adapterObject);
                yield return null;
            }

            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(loadedScene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }

            loadedScene = default(Scene);
            bootstrap = null;
            adapterObject = null;
            presenterObject = null;
            adapter = null;
            session = null;
            persistenceCommands = null;
            productionCommands = null;
            clock = null;
            store = null;
            compositionScope?.Dispose();
            compositionScope = null;
        }

        [Test]
        public void DeviceUtcClockReturnsZeroOffsetUtcTime()
        {
            var before = DateTimeOffset.UtcNow;
            var value = new DeviceUtcClock().GetUtcNow();
            var after = DateTimeOffset.UtcNow;

            Assert.That(value.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(value.UtcTicks % TimeSpan.TicksPerMillisecond, Is.Zero);
            Assert.That(value, Is.GreaterThanOrEqualTo(DeviceUtcClock.FloorToMilliseconds(before)));
            Assert.That(value, Is.LessThanOrEqualTo(after));
        }

        [UnityTest]
        public IEnumerator ColdStartUsesActiveCheckpointWithoutFallback()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);

            Assert.That(adapter.LastResult, Is.Not.Null);
            Assert.That(adapter.LastResult.Succeeded, Is.True, adapter.LastResult.Message);
            Assert.That(adapter.LastResult.Value.UsedFallback, Is.False);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));

            yield break;
        }

        [UnityTest]
        public IEnumerator PauseAndFocusCallbacksBindSuccessfulDepartureAndReconcileOnce()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);
            var promotionsAtStart = store.PromoteCount;
            store.ReadCheckpointIds.Clear();
            clock.Now = Epoch.AddMinutes(15);

            adapterObject.SendMessage("OnApplicationFocus", false);
            Assert.That(adapter.LastResult.Succeeded, Is.True, adapter.LastResult.Message);
            var departureId = adapter.LastResult.Value.CheckpointId;
            Assert.That(departureId.HasValue, Is.True);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Suspended));
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 1));

            var whileSuspended = productionCommands.AdvanceSimulation(TimeSpan.TicksPerSecond);
            Assert.That(whileSuspended.Outcome, Is.EqualTo(CommandOutcome.LifecycleNoOp));

            adapterObject.SendMessage("OnApplicationPause", true);
            adapterObject.SendMessage("OnApplicationFocus", true);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Suspended));
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 1));

            clock.Now = Epoch.AddMinutes(20);
            adapterObject.SendMessage("OnApplicationPause", false);
            Assert.That(adapter.LastResult.Succeeded, Is.True, adapter.LastResult.Message);
            Assert.That(adapter.LastResult.Value.UsedFallback, Is.False);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(store.ReadCheckpointIds, Does.Contain(departureId.Value));
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 2));

            adapterObject.SendMessage("OnApplicationPause", false);
            adapterObject.SendMessage("OnApplicationFocus", true);
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 2),
                "Duplicate return callbacks must not save or reconcile a second time.");

            yield break;
        }

        [UnityTest]
        public IEnumerator FailedSuspensionUsesOnlyTheExplicitFr037Fallback()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);
            var fallbackCheckpointId = store.Active.Id;
            store.ReadCheckpointIds.Clear();
            store.FailNextPromotion = true;
            clock.Now = Epoch.AddMinutes(15);

            adapterObject.SendMessage("OnApplicationPause", true);
            Assert.That(adapter.LastResult.Succeeded, Is.False);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Suspended));

            clock.Now = Epoch.AddMinutes(20);
            adapterObject.SendMessage("OnApplicationPause", false);

            Assert.That(adapter.LastResult.Succeeded, Is.True, adapter.LastResult.Message);
            Assert.That(adapter.LastResult.Value.UsedFallback, Is.True);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(store.ReadCheckpointIds, Is.Empty,
                "A failed departure write must use the last active checkpoint, not a fabricated departure ID.");
            Assert.That(store.Active.ReconciledFromCheckpointId, Is.EqualTo(fallbackCheckpointId));

            yield break;
        }

        [UnityTest]
        public IEnumerator FailedReturnBlocksGameplayAndDuplicateCallbacksDoNotRetryIt()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);
            clock.Now = Epoch.AddMinutes(15);
            adapterObject.SendMessage("OnApplicationPause", true);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Suspended));

            store.FailNextPromotion = true;
            clock.Now = Epoch.AddMinutes(20);
            adapterObject.SendMessage("OnApplicationPause", false);

            Assert.That(adapter.LastResult.Succeeded, Is.False);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));
            var blockedAdvance = productionCommands.AdvanceSimulation(TimeSpan.TicksPerSecond);
            Assert.That(blockedAdvance.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));

            var promotionsAfterBlockedReturn = store.PromoteCount;
            adapterObject.SendMessage("OnApplicationPause", false);
            adapterObject.SendMessage("OnApplicationFocus", true);
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAfterBlockedReturn),
                "A duplicate platform callback must not silently retry an unresolved return.");
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));

            yield break;
        }

        [UnityTest]
        public IEnumerator QuitAfterPauseRecordsOnlyOneDepartureAndNeverResumes()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);
            var promotionsAtStart = store.PromoteCount;
            clock.Now = Epoch.AddMinutes(15);

            adapterObject.SendMessage("OnApplicationPause", true);
            adapterObject.SendMessage("OnApplicationQuit");
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 1));

            adapterObject.SendMessage("OnApplicationPause", false);
            adapterObject.SendMessage("OnApplicationFocus", true);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Suspended));
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 1));

            yield break;
        }

        [UnityTest]
        public IEnumerator RotationReturnKeepsCheckpointAccountingAndIsClassifiedAsConfigurationChange()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);
            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.ColdStart));
            var promotionsAtStart = store.PromoteCount;
            store.ReadCheckpointIds.Clear();
            clock.Now = Epoch.AddMinutes(15);

            // T060 emulator evidence: the surface size is already rotated when focus is lost, no pause is
            // reported, and Screen.orientation follows one or two frames after focus returns.
            RotateWithoutUpdatingOrientation(LandscapeLeftSize);
            Assert.That(adapter.LastResult.Succeeded, Is.True, adapter.LastResult.Message);
            var departureId = adapter.LastResult.Value.CheckpointId;
            Assert.That(departureId.HasValue, Is.True);
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 1),
                "A rotation still records the FR-037 departure checkpoint.");

            clock.Now = Epoch.AddMinutes(15).AddMilliseconds(300);
            adapter.NotifyFocusChanged(true);

            Assert.That(adapter.LastResult.Succeeded, Is.True, adapter.LastResult.Message);
            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.ConfigurationChange));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(store.ReadCheckpointIds, Does.Contain(departureId.Value),
                "The rotation return reconciles from its own departure checkpoint exactly like any return.");
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 2));
            Assert.That(adapter.LastResult.Value.OfflineSummary, Is.Not.Null);
            Assert.That(adapter.LastResult.Value.OfflineSummary.MeasuredTicks,
                Is.EqualTo(TimeSpan.FromMilliseconds(300).Ticks));

            screen = LandscapeLeft;
            adapter.ObserveScreenConfiguration();
            adapter.NotifyFocusChanged(true);
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 2),
                "The late orientation update must not start a second return.");

            yield break;
        }

        [UnityTest]
        public IEnumerator ConfigurationChangeObservedJustBeforeFocusLossIsClassifiedAsRotation()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);

            // Emulator evidence (rotation-lock release): the running frame already saw the new size and
            // orientation before the same rotation removed focus.
            realtimeSeconds = 200d;
            screen = LandscapeLeft;
            adapter.ObserveScreenConfiguration();
            realtimeSeconds = 200.02d;
            adapter.NotifyFocusChanged(false);
            realtimeSeconds = 200.07d;
            adapter.NotifyFocusChanged(true);
            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.ConfigurationChange));

            realtimeSeconds = 200.07d + ApplicationLifecycleAdapter.ConfigurationChangeWindowSeconds + 5d;
            adapter.NotifyFocusChanged(false);
            adapter.NotifyFocusChanged(true);
            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.Return),
                "A focus loss long after the last rotation is not attributed to it.");

            yield break;
        }

        [UnityTest]
        public IEnumerator FocusOnlyLossWithoutScreenChangeRemainsAGenuineReturn()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);
            realtimeSeconds = 300d;

            // Notification shade or system dialog: focus only, no pause, no screen change.
            adapter.NotifyFocusChanged(false);
            realtimeSeconds = 302.8d;
            adapter.NotifyFocusChanged(true);

            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.Return));
            yield break;
        }

        [UnityTest]
        public IEnumerator PausedDepartureIsAGenuineReturnEvenWhenTheScreenRotatedMeanwhile()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);
            var promotionsAtStart = store.PromoteCount;

            // Emulator evidence for HOME and power lock: pause precedes the focus loss, and return reverses it.
            adapterObject.SendMessage("OnApplicationPause", true);
            adapter.NotifyFocusChanged(false);
            screen = LandscapeLeft;
            adapter.NotifyFocusChanged(true);
            adapterObject.SendMessage("OnApplicationPause", false);

            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.Return));
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 2));

            // A rotation that removes focus first and then leaves the app (for example HOME during the rotation)
            // is also a genuine return once a pause is reported.
            RotateWithoutUpdatingOrientation(PortraitSize);
            adapterObject.SendMessage("OnApplicationPause", true);
            adapterObject.SendMessage("OnApplicationPause", false);
            adapter.NotifyFocusChanged(true);

            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.Return));
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 4));
            yield break;
        }

        [UnityTest]
        public IEnumerator RotationKeepsTheEarlierGenuineSummaryAndGenuineReturnsStillReplaceIt()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);
            var summaryLabel = CreatePresenterForTestAdapter().Q<Label>("persistence-summary");
            Assert.That(summaryLabel.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex),
                "The cold-start return keeps its FR-036 feedback.");
            Assert.That(summaryLabel.text, Does.Contain("Measured: 0 s"));

            clock.Now = Epoch.AddMinutes(5);
            adapterObject.SendMessage("OnApplicationPause", true);
            Assert.That(summaryLabel.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            clock.Now = Epoch.AddMinutes(10);
            adapterObject.SendMessage("OnApplicationPause", false);
            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.Return));
            Assert.That(summaryLabel.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(summaryLabel.text, Does.Contain("Measured: 5 m 00 s"));
            var genuineSummaryText = summaryLabel.text;

            clock.Now = Epoch.AddMinutes(12);
            RotateWithoutUpdatingOrientation(LandscapeLeftSize);
            clock.Now = Epoch.AddMinutes(12).AddMilliseconds(360);
            adapter.NotifyFocusChanged(true);

            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.ConfigurationChange));
            Assert.That(persistenceCommands.GetLastOfflineSummary().MeasuredTicks,
                Is.EqualTo(TimeSpan.FromMilliseconds(360).Ticks),
                "Offline accounting of the rotation return is unchanged.");
            Assert.That(summaryLabel.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(summaryLabel.text, Is.EqualTo(genuineSummaryText),
                "A rotation must neither replace nor remove the summary of an earlier genuine return.");

            clock.Now = Epoch.AddMinutes(20);
            adapterObject.SendMessage("OnApplicationPause", true);
            clock.Now = Epoch.AddMinutes(27);
            adapterObject.SendMessage("OnApplicationPause", false);
            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.Return));
            Assert.That(summaryLabel.text, Does.Contain("Measured: 7 m 00 s"),
                "A genuine return after a rotation shows its own summary.");

            yield break;
        }

        [UnityTest]
        public IEnumerator RotationWithoutAnEarlierSummaryShowsNoSummary()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);
            var summaryLabel = CreatePresenterForTestAdapter().Q<Label>("persistence-summary");
            Assert.That(persistenceCommands.ResetDevelopmentScenario().Succeeded, Is.True);
            testPresenter.Refresh();
            Assert.That(summaryLabel.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

            clock.Now = Epoch.AddMinutes(3);
            RotateWithoutUpdatingOrientation(LandscapeLeftSize);
            clock.Now = Epoch.AddMinutes(3).AddMilliseconds(40);
            adapter.NotifyFocusChanged(true);

            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.ConfigurationChange));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(summaryLabel.resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                "A rotation the player never left must not show Durable offline return feedback.");
            Assert.That(summaryLabel.text, Is.Empty);

            yield break;
        }

        [UnityTest]
        public IEnumerator RotationReturnThatUsedTheFallbackStillShowsItsWarning()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);
            var summaryLabel = CreatePresenterForTestAdapter().Q<Label>("persistence-summary");
            store.FailNextPromotion = true;
            clock.Now = Epoch.AddMinutes(4);
            RotateWithoutUpdatingOrientation(LandscapeLeftSize);
            Assert.That(adapter.LastResult.Succeeded, Is.False);

            adapter.NotifyFocusChanged(true);

            Assert.That(adapter.LastReturnKind, Is.EqualTo(LifecycleReturnKind.ConfigurationChange));
            Assert.That(adapter.LastResult.Value.UsedFallback, Is.True);
            Assert.That(summaryLabel.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(summaryLabel.text, Does.Contain("fallback used"),
                "Lost unsaved changes are never hidden, even after a rotation.");

            yield break;
        }

        private static readonly LifecycleScreenConfiguration Portrait =
            new LifecycleScreenConfiguration(1080, 2400, ScreenOrientation.Portrait);

        private static readonly LifecycleScreenConfiguration LandscapeLeft =
            new LifecycleScreenConfiguration(2400, 1080, ScreenOrientation.LandscapeLeft);

        private static readonly Vector2Int LandscapeLeftSize = new Vector2Int(2400, 1080);
        private static readonly Vector2Int PortraitSize = new Vector2Int(1080, 2400);

        private PersistencePresenter testPresenter;

        private void RotateWithoutUpdatingOrientation(Vector2Int size)
        {
            screen = new LifecycleScreenConfiguration(size.x, size.y, screen.Orientation);
            realtimeSeconds += 0.5d;
            adapter.NotifyFocusChanged(false);
        }

        private VisualElement CreatePresenterForTestAdapter()
        {
            var root = bootstrap.UiDocument.rootVisualElement;
            presenterObject = new GameObject("Lifecycle Persistence Presenter Test");
            testPresenter = presenterObject.AddComponent<PersistencePresenter>();
            testPresenter.Configure(
                persistenceCommands,
                root,
                () => session.LifecycleState,
                () => { },
                () => { },
                _ => { },
                () => adapter.LastResult != null && adapter.LastResult.Succeeded ? adapter.LastResult.Value : null,
                null,
                () => adapter.LastReturnKind == LifecycleReturnKind.ConfigurationChange);
            adapter.ResultChanged += _ => testPresenter.Refresh();
            return root;
        }

        private void CreateLifecycleSystem(bool seedActiveCheckpoint)
        {
            var catalog = bootstrap.Session.Catalog;
            var bounds = new FactoryBounds(new GridPosition(0, 0), 16, 16);
            var initialFactory = new FactoryState(bounds, catalog, Array.Empty<BuildingInstanceState>());
            clock = new TestUtcClock(Epoch);
            store = new TestCheckpointStore();

            if (seedActiveCheckpoint)
            {
                store.SeedActive(new Checkpoint(
                    new CheckpointId(new Guid(1, 0, 0, new byte[8])),
                    0,
                    Epoch,
                    new OfflinePolicyRevision(DefinitionCatalog.SupportedOfflinePolicyRevision),
                    null,
                    initialFactory));
            }

            session = new FactorySession(initialFactory, catalog, clock, store);
            persistenceCommands = new PersistenceCommands(session);
            productionCommands = new ProductionCommands(session);
            adapterObject = new GameObject("Application Lifecycle Adapter Test");
            adapter = adapterObject.AddComponent<ApplicationLifecycleAdapter>();
            screen = Portrait;
            realtimeSeconds = 100d;
            adapter.UseScreenConfigurationSource(() => screen, () => realtimeSeconds);
            adapter.Configure(persistenceCommands);
        }

        private sealed class TestUtcClock : IUtcClock
        {
            public TestUtcClock(DateTimeOffset now)
            {
                Now = now;
            }

            public DateTimeOffset Now { get; set; }

            public DateTimeOffset GetUtcNow() => Now;
        }

        private sealed class TestCheckpointStore : ICheckpointStore
        {
            public Checkpoint Active { get; private set; }
            public Checkpoint Previous { get; private set; }
            public int PromoteCount { get; private set; }
            public bool FailNextPromotion { get; set; }
            public List<CheckpointId> ReadCheckpointIds { get; } = new List<CheckpointId>();

            public void SeedActive(Checkpoint checkpoint)
            {
                Active = checkpoint;
            }

            public CheckpointReadResult ReadActive() => Active == null
                ? CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "No test checkpoint is active.")
                : CheckpointReadResult.Success(Active);

            public CheckpointReadResult ReadPrevious() => Previous == null
                ? CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "No test checkpoint is previous.")
                : CheckpointReadResult.Success(Previous);

            public CheckpointReadResult Read(CheckpointId checkpointId)
            {
                ReadCheckpointIds.Add(checkpointId);
                if (Active != null && Active.Id == checkpointId)
                {
                    return CheckpointReadResult.Success(Active);
                }

                if (Previous != null && Previous.Id == checkpointId)
                {
                    return CheckpointReadResult.Success(Previous);
                }

                return CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "The test checkpoint was not found.");
            }

            public CommandResult<CheckpointId> Promote(Checkpoint checkpoint)
            {
                PromoteCount++;
                if (FailNextPromotion)
                {
                    FailNextPromotion = false;
                    return CommandResult<CheckpointId>.Rejected(
                        CommandOutcome.PersistenceFailed,
                        "The test checkpoint promotion failed.");
                }

                if (Active != null && checkpoint.Generation <= Active.Generation)
                {
                    return CommandResult<CheckpointId>.Rejected(
                        CommandOutcome.InvalidCheckpoint,
                        "The test checkpoint generation did not advance.");
                }

                Previous = Active;
                Active = checkpoint;
                return CommandResult<CheckpointId>.Success(checkpoint.Id);
            }

            public CommandResult<CheckpointId> RecoverPrevious(Checkpoint recoveryCandidate) =>
                CommandResult<CheckpointId>.Rejected(
                    CommandOutcome.NoCheckpoint,
                    "Previous-checkpoint recovery is not used by these lifecycle tests.");
        }
    }
}
