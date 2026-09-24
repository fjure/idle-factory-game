using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Bootstrap;
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
    public sealed class PersistenceUiTests
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private Scene loadedScene;
        private CoreGameFoundationBootstrap bootstrap;
        private GameObject presenterObject;
        private PersistencePresenter presenter;
        private FactorySession session;
        private PersistenceCommands commands;
        private FakeClock clock;
        private FakeCheckpointStore store;
        private PersistenceOperationResult latestLifecycleResult;
        private FactorySnapshot refreshedFactory;
        private MachineInspectionSnapshot refreshedInspection;
        private BuildingInstanceId? selectedInstanceId;
        private LifecycleState? lifecycleOverride;
        private int cancelPreviewCount;
        private int cancelPointerCount;
        private int factoryRefreshCount;

        [UnitySetUp]
        public IEnumerator LoadHudAndConfigureIsolatedPersistence()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The development scene is loaded through the Editor test harness.");
            yield break;
#else
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
            {
                yield return null;
            }

            yield return null;
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            bootstrap = loadedScene.GetRootGameObjects()
                .SelectMany(gameObject => gameObject.GetComponentsInChildren<CoreGameFoundationBootstrap>(true))
                .Single();
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);

            var initial = new FactoryState(
                new FactoryBounds(new GridPosition(0, 0), 16, 16),
                bootstrap.Session.Catalog,
                Array.Empty<BuildingInstanceState>());
            clock = new FakeClock(Epoch);
            store = new FakeCheckpointStore();
            var sessionResult = DevelopmentSessionFactory.CreateRunning(initial, initial.Catalog, clock, store);
            Assert.That(sessionResult.Succeeded, Is.True, sessionResult.Message);
            session = sessionResult.Value;
            commands = new PersistenceCommands(session, new FakeCheckpointIdSource());

            presenterObject = new GameObject("Persistence Presenter Test");
            presenter = presenterObject.AddComponent<PersistencePresenter>();
            presenter.Configure(
                commands,
                bootstrap.UiDocument.rootVisualElement,
                () => lifecycleOverride ?? session.LifecycleState,
                () => cancelPreviewCount++,
                () => cancelPointerCount++,
                RefreshFactoryAndSelection,
                () => latestLifecycleResult,
                ReadRetainedProductionProgress);
#endif
        }

        [UnityTearDown]
        public IEnumerator UnloadHud()
        {
            if (presenterObject != null)
            {
                UnityEngine.Object.Destroy(presenterObject);
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

            loadedScene = default;
            bootstrap = null;
            presenterObject = null;
            presenter = null;
            session = null;
            commands = null;
            clock = null;
            store = null;
            latestLifecycleResult = null;
            refreshedFactory = null;
            refreshedInspection = null;
            selectedInstanceId = null;
            lifecycleOverride = null;
            cancelPreviewCount = 0;
            cancelPointerCount = 0;
            factoryRefreshCount = 0;
        }

        [Test]
        public void NoSaveAndFailedLoadKeepTheLiveFactoryAndSelectionIntact()
        {
            var reset = commands.ResetDevelopmentScenario();
            Assert.That(reset.Succeeded, Is.True, reset.Message);
            selectedInstanceId = reset.Value.Factory.Buildings.Single().InstanceId;
            var revisionBefore = session.GetFactorySnapshot().Revision;
            var root = bootstrap.UiDocument.rootVisualElement;

            Assert.That(root.Q<Label>("persistence-status").text, Does.Contain("NO SAVE"));
            Assert.That(root.Q<Button>("persistence-load").enabledSelf, Is.True);
            Assert.That(root.Q<Button>("persistence-recover").enabledSelf, Is.False);

            presenter.LoadDevelopmentCheckpoint();

            Assert.That(root.Q<Label>("persistence-feedback").text, Does.Contain("No saved factory"));
            Assert.That(session.GetFactorySnapshot().Revision, Is.EqualTo(revisionBefore));
            Assert.That(selectedInstanceId, Is.EqualTo(reset.Value.Factory.Buildings.Single().InstanceId));
            Assert.That(cancelPreviewCount, Is.Zero);
            Assert.That(cancelPointerCount, Is.Zero);
            Assert.That(factoryRefreshCount, Is.Zero);
            Assert.That(root.Q<Label>("persistence-summary").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void IncompleteHudPresenterCanBeDestroyedWithoutNullReferenceErrors()
        {
            var partialHud = new VisualElement();
            var saveButton = new Button { name = "persistence-save" };
            partialHud.Add(saveButton);

            var partialPresenterObject = new GameObject("Partial Persistence Presenter Test");
            try
            {
                var partialPresenter = partialPresenterObject.AddComponent<PersistencePresenter>();
                partialPresenter.Configure(
                    commands,
                    partialHud,
                    () => session.LifecycleState,
                    () => { },
                    () => { },
                    _ => { });

                Assert.That(partialPresenter.IsConfigured, Is.False);
                Assert.That(partialPresenter.Error, Does.Contain("HUD layout is incomplete"));
                Assert.That(saveButton.enabledSelf, Is.False);

                UnityEngine.Object.DestroyImmediate(partialPresenterObject);
                partialPresenterObject = null;
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (partialPresenterObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(partialPresenterObject);
                }
            }
        }

        [Test]
        public void SuccessfulLoadRefreshesFromLoadedSnapshotAndResolvesOrClearsSelection()
        {
            var firstScenario = commands.ResetDevelopmentScenario();
            Assert.That(firstScenario.Succeeded, Is.True, firstScenario.Message);
            var savedMachineId = firstScenario.Value.Factory.Buildings.Single().InstanceId;
            presenter.SaveNow();
            Assert.That(store.Active, Is.Not.Null);

            var replacementScenario = commands.ResetDevelopmentScenario();
            Assert.That(replacementScenario.Succeeded, Is.True, replacementScenario.Message);
            var liveMachineId = replacementScenario.Value.Factory.Buildings.Single().InstanceId;
            Assert.That(liveMachineId, Is.Not.EqualTo(savedMachineId));

            // A pending selection can refer to an instance that disappears in the current world but
            // exists in the checkpoint being loaded. The post-load callback must query the new session.
            selectedInstanceId = savedMachineId;
            presenter.LoadDevelopmentCheckpoint();

            Assert.That(cancelPreviewCount, Is.EqualTo(1));
            Assert.That(cancelPointerCount, Is.EqualTo(1));
            Assert.That(factoryRefreshCount, Is.EqualTo(1));
            Assert.That(refreshedFactory.Buildings.Single().InstanceId, Is.EqualTo(savedMachineId));
            Assert.That(selectedInstanceId, Is.EqualTo(savedMachineId));
            Assert.That(refreshedInspection, Is.Not.Null);
            Assert.That(refreshedInspection.InstanceId, Is.EqualTo(savedMachineId));
            Assert.That(refreshedInspection.Revision, Is.EqualTo(refreshedFactory.Revision));
            Assert.That(refreshedInspection.Inputs.Single().Quantity, Is.EqualTo(4));

            // A selection that is absent from the restored snapshot is cleared by the same refresh path.
            selectedInstanceId = liveMachineId;
            presenter.LoadDevelopmentCheckpoint();
            Assert.That(cancelPreviewCount, Is.EqualTo(2));
            Assert.That(cancelPointerCount, Is.EqualTo(2));
            Assert.That(factoryRefreshCount, Is.EqualTo(2));
            Assert.That(refreshedFactory.Buildings.Single().InstanceId, Is.EqualTo(savedMachineId));
            Assert.That(selectedInstanceId, Is.Null);
            Assert.That(refreshedInspection, Is.Null);
        }

        [Test]
        public void ScenarioResetChangesLiveD001AndPreservesSavedGeneration()
        {
            var first = commands.ResetDevelopmentScenario();
            Assert.That(first.Succeeded, Is.True, first.Message);
            presenter.SaveNow();
            var savedId = store.Active.Id;
            var savedGeneration = store.Active.Generation;

            presenter.ResetDevelopmentScenario();

            Assert.That(session.GetFactorySnapshot().Buildings.Count, Is.EqualTo(1));
            Assert.That(store.Active.Id, Is.EqualTo(savedId));
            Assert.That(store.Active.Generation, Is.EqualTo(savedGeneration));
            Assert.That(cancelPreviewCount, Is.EqualTo(1));
            Assert.That(cancelPointerCount, Is.EqualTo(1));
            Assert.That(refreshedFactory.Buildings.Count, Is.EqualTo(1));
            Assert.That(bootstrap.UiDocument.rootVisualElement.Q<Label>("persistence-feedback").text,
                Does.Contain("saved checkpoint is unchanged"));
        }

        [Test]
        public void PersistenceFailuresDoNotExposePathOrSaveContent()
        {
            Assert.That(commands.ResetDevelopmentScenario().Succeeded, Is.True);
            presenter.SaveNow();
            var previousId = store.Active.Id;
            var revisionBefore = session.GetFactorySnapshot().Revision;
            store.FailNextPromotion = true;
            store.FailureMessage = "C:\\private\\factory-checkpoint.json { \"factory\": \"raw save\" }";

            presenter.SaveNow();

            var feedback = bootstrap.UiDocument.rootVisualElement.Q<Label>("persistence-feedback").text;
            var status = bootstrap.UiDocument.rootVisualElement.Q<Label>("persistence-status").text;
            Assert.That(feedback, Does.Contain("Save failed"));
            Assert.That(feedback, Does.Not.Contain("C:\\private"));
            Assert.That(feedback, Does.Not.Contain("raw save"));
            Assert.That(feedback, Does.Not.Contain("{"));
            Assert.That(feedback, Does.Not.Contain("}"));
            Assert.That(status, Does.Not.Contain("C:\\private"));
            Assert.That(store.Active.Id, Is.EqualTo(previousId));
            Assert.That(session.GetFactorySnapshot().Revision, Is.EqualTo(revisionBefore));
            Assert.That(cancelPreviewCount, Is.Zero);
            Assert.That(cancelPointerCount, Is.Zero);
            Assert.That(factoryRefreshCount, Is.Zero);
        }

        [Test]
        public void ReturnSummaryShowsMeasuredCreditedEffectiveActualDeltasLimitsAndFallback()
        {
            Assert.That(commands.ResetDevelopmentScenario().Succeeded, Is.True);
            presenter.SaveNow();
            clock.Now = Epoch;
            store.FailNextPromotion = true;

            var failedDeparture = commands.RecordSuspension();
            Assert.That(failedDeparture.Succeeded, Is.False);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Suspended));

            clock.Now = Epoch.AddSeconds(6.25);
            var resumed = commands.ResumeAndReconcile();
            Assert.That(resumed.Succeeded, Is.True, resumed.Message);
            latestLifecycleResult = resumed.Value;
            presenter.Refresh();

            var summary = bootstrap.UiDocument.rootVisualElement.Q<Label>("persistence-summary");
            Assert.That(summary.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(summary.text, Does.Contain("Measured: 6.25 s"));
            Assert.That(summary.text, Does.Contain("Credited: 6.25 s"));
            Assert.That(summary.text, Does.Contain("Effective: 5 s"));
            Assert.That(summary.text, Does.Contain("dev.input -2"));
            Assert.That(summary.text, Does.Contain("dev.output +1"));
            Assert.That(summary.text, Does.Contain("output capacity reached for dev.output"));
            Assert.That(summary.text, Does.Contain("fallback used"));
            Assert.That(summary.text, Does.Contain("later unsaved changes were not restored"));
            Assert.That(summary.text, Does.Not.Contain("factory-checkpoint.json"));
            Assert.That(summary.text, Does.Not.Contain("{"));
            Assert.That(summary.text, Does.Not.Contain("}"));
        }

        [Test]
        public void OfflineSummaryReportsRetainedProgressFromTheDurableResultSnapshot()
        {
            Assert.That(commands.ResetDevelopmentScenario().Succeeded, Is.True);
            var production = new ProductionCommands(session);
            var startingFactory = session.GetFactorySnapshot();
            var startingInstanceId = startingFactory.Buildings.Single().InstanceId;
            var beforeAdvance = session.GetMachineInspection(startingInstanceId);
            Assert.That(beforeAdvance.Succeeded, Is.True);
            Assert.That(beforeAdvance.Value.IsActive, Is.True);
            Assert.That(beforeAdvance.Value.Blockers, Is.Empty);
            Assert.That(beforeAdvance.Value.Inputs.Single().Quantity, Is.EqualTo(4));
            Assert.That(beforeAdvance.Value.Outputs.Single().RequiredOrFreeCapacity, Is.EqualTo(1));

            var advanced = production.AdvanceSimulation(TimeSpan.TicksPerSecond);
            Assert.That(advanced.Succeeded, Is.True, advanced.Message);
            var startedProgress = session.GetMachineInspection(advanced.Value.Factory.Buildings.Single().InstanceId);
            Assert.That(startedProgress.Succeeded, Is.True);
            Assert.That(startedProgress.Value.ProgressTicks, Is.EqualTo(TimeSpan.TicksPerSecond));

            presenter.SaveNow();
            Assert.That(commands.RecordSuspension().Succeeded, Is.True);
            clock.Now = Epoch.AddSeconds(1.25);
            var resumed = commands.ResumeAndReconcile();
            Assert.That(resumed.Succeeded, Is.True, resumed.Message);
            latestLifecycleResult = resumed.Value;
            presenter.Refresh();

            var retainedInspection = session.GetMachineInspection(resumed.Value.Factory.Buildings.Single().InstanceId);
            Assert.That(retainedInspection.Succeeded, Is.True);
            Assert.That(retainedInspection.Value.ProgressTicks, Is.EqualTo(2 * TimeSpan.TicksPerSecond));
            var summary = bootstrap.UiDocument.rootVisualElement.Q<Label>("persistence-summary");
            Assert.That(summary.text, Does.Contain("Retained production progress:"));
            Assert.That(summary.text, Does.Contain("dev.machine.basic"));
            Assert.That(summary.text, Does.Contain("2 s of 5 s"));
        }

        [UnityTest]
        public IEnumerator LoadButtonShowsLoadingBeforeReadingAndApplyingTheSavedFactory()
        {
            Assert.That(commands.ResetDevelopmentScenario().Succeeded, Is.True);
            presenter.SaveNow();
            var savedInstanceId = store.Active.CreateFactoryCopy().Buildings.Single().Key;
            Assert.That(commands.ResetDevelopmentScenario().Succeeded, Is.True);

            var root = bootstrap.UiDocument.rootVisualElement;
            var load = root.Q<Button>("persistence-load");
            var activeReadsBefore = store.ActiveReadCount;
            load.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                load.SendEvent(submit);
            }

            Assert.That(root.Q<Label>("persistence-status").text, Does.Contain("LOADING"));
            Assert.That(root.Q<Label>("persistence-feedback").text, Does.Contain("Loading saved factory"));
            Assert.That(load.enabledSelf, Is.False);
            Assert.That(store.ActiveReadCount, Is.EqualTo(activeReadsBefore),
                "The synchronous checkpoint read must wait until after the pending state can render.");
            Assert.That(root.Q<Label>("persistence-summary").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

            yield return null;
            yield return null;

            Assert.That(store.ActiveReadCount, Is.GreaterThan(activeReadsBefore));
            Assert.That(session.GetFactorySnapshot().Buildings.Single().InstanceId, Is.EqualTo(savedInstanceId));
            Assert.That(root.Q<Label>("persistence-feedback").text, Does.Contain("Saved factory loaded"));
            Assert.That(load.enabledSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator RecoveryButtonShowsLoadingBeforeReadingAndPromotingThePreviousGeneration()
        {
            Assert.That(commands.ResetDevelopmentScenario().Succeeded, Is.True);
            presenter.SaveNow();
            Assert.That(commands.ResetDevelopmentScenario().Succeeded, Is.True);
            presenter.SaveNow();
            Assert.That(store.Previous, Is.Not.Null);

            var root = bootstrap.UiDocument.rootVisualElement;
            var recover = root.Q<Button>("persistence-recover");
            Assert.That(recover.enabledSelf, Is.True);
            var previousReadsBefore = store.PreviousReadCount;
            recover.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                recover.SendEvent(submit);
            }

            Assert.That(root.Q<Label>("persistence-status").text, Does.Contain("LOADING"));
            Assert.That(root.Q<Label>("persistence-feedback").text, Does.Contain("Recovering previous saved factory"));
            Assert.That(recover.enabledSelf, Is.False);
            Assert.That(store.PreviousReadCount, Is.EqualTo(previousReadsBefore),
                "Previous-generation recovery must wait until after the pending state can render.");

            yield return null;
            yield return null;

            Assert.That(store.PreviousReadCount, Is.GreaterThan(previousReadsBefore));
            Assert.That(commands.GetPersistenceStatus().Status, Is.EqualTo(PersistenceStatusKind.Recovered));
            Assert.That(root.Q<Label>("persistence-status").text, Does.Contain("RECOVERED"));
            Assert.That(root.Q<Label>("persistence-feedback").text, Does.Contain("Previous saved factory recovered"));
            Assert.That(recover.enabledSelf, Is.True,
                "A newer complete previous generation remains explicitly available after recovery.");
        }

        [Test]
        public void BackwardButRepresentableTimeShowsDurableZeroCreditFeedback()
        {
            Assert.That(commands.ResetDevelopmentScenario().Succeeded, Is.True);
            clock.Now = Epoch.AddMinutes(5);
            presenter.SaveNow();
            Assert.That(commands.RecordSuspension().Succeeded, Is.True);
            clock.Now = Epoch;

            var resumed = commands.ResumeAndReconcile();
            Assert.That(resumed.Succeeded, Is.True, resumed.Message);
            latestLifecycleResult = resumed.Value;
            presenter.Refresh();

            var summary = bootstrap.UiDocument.rootVisualElement.Q<Label>("persistence-summary");
            Assert.That(summary.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(summary.text, Does.Contain("Measured: 0 s"));
            Assert.That(summary.text, Does.Contain("Credited: 0 s"));
            Assert.That(summary.text, Does.Contain("Effective: 0 s"));
            Assert.That(summary.text, Does.Contain("device time was backward"));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(commands.GetPersistenceStatus().Status, Is.EqualTo(PersistenceStatusKind.Ready));
        }

        [Test]
        public void SuspendedReturnShowsWaitingWithoutPublishingAnOfflineResult()
        {
            Assert.That(commands.ResetDevelopmentScenario().Succeeded, Is.True);
            presenter.SaveNow();
            Assert.That(commands.RecordSuspension().Succeeded, Is.True);

            presenter.Refresh();

            var root = bootstrap.UiDocument.rootVisualElement;
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Suspended));
            Assert.That(root.Q<Label>("persistence-status").text, Does.Contain("WAITING"));
            Assert.That(root.Q<Button>("persistence-save").enabledSelf, Is.False);
            Assert.That(root.Q<Button>("persistence-load").enabledSelf, Is.False);
            Assert.That(root.Q<Label>("persistence-summary").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ReconcilingLifecycleShowsPendingAndHidesAnyEarlierDurableSummary()
        {
            Assert.That(commands.ResetDevelopmentScenario().Succeeded, Is.True);
            presenter.SaveNow();
            Assert.That(commands.RecordSuspension().Succeeded, Is.True);
            clock.Now = Epoch.AddSeconds(1);
            var resumed = commands.ResumeAndReconcile();
            Assert.That(resumed.Succeeded, Is.True, resumed.Message);
            latestLifecycleResult = resumed.Value;
            presenter.Refresh();

            var root = bootstrap.UiDocument.rootVisualElement;
            Assert.That(root.Q<Label>("persistence-summary").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));

            lifecycleOverride = LifecycleState.Reconciling;
            presenter.Refresh();

            Assert.That(root.Q<Label>("persistence-status").text, Does.Contain("RETURN PENDING"));
            Assert.That(root.Q<Label>("persistence-summary").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(root.Q<Button>("persistence-save").enabledSelf, Is.False);
            Assert.That(root.Q<Button>("persistence-load").enabledSelf, Is.False);
            Assert.That(root.Q<Button>("persistence-retry").enabledSelf, Is.False);
        }

        [Test]
        public void ReturnBlockedShowsRecoveryActionsWithoutSpeculativeSummaryUntilRetrySucceeds()
        {
            Assert.That(commands.ResetDevelopmentScenario().Succeeded, Is.True);
            presenter.SaveNow();
            Assert.That(commands.RecordSuspension().Succeeded, Is.True);
            store.FailNextPromotion = true;
            clock.Now = Epoch.AddMinutes(1);

            var blocked = commands.ResumeAndReconcile();
            Assert.That(blocked.Succeeded, Is.False);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));
            latestLifecycleResult = null;
            presenter.Refresh();

            var root = bootstrap.UiDocument.rootVisualElement;
            Assert.That(root.Q<Label>("persistence-status").text, Does.Contain("RETURN BLOCKED"));
            Assert.That(root.Q<Button>("persistence-save").enabledSelf, Is.False);
            Assert.That(root.Q<Button>("persistence-load").enabledSelf, Is.False);
            Assert.That(root.Q<Button>("persistence-retry").enabledSelf, Is.True);
            Assert.That(root.Q<Button>("persistence-reload").enabledSelf, Is.True);
            Assert.That(root.Q<Button>("persistence-recover").enabledSelf, Is.True,
                "Recovery is exposed only because a previous complete generation is advertised.");
            Assert.That(root.Q<Label>("persistence-summary").resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                "A calculated but unpromoted candidate is never presented as an offline result.");

            presenter.RetryReturn();

            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(root.Q<Label>("persistence-summary").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Button>("persistence-retry").enabledSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator RetryButtonShowsPendingBeforePromotionThenCompletesTheReturn()
        {
            Assert.That(commands.ResetDevelopmentScenario().Succeeded, Is.True);
            presenter.SaveNow();
            Assert.That(commands.RecordSuspension().Succeeded, Is.True);
            store.FailNextPromotion = true;
            clock.Now = Epoch.AddMinutes(1);

            var blocked = commands.ResumeAndReconcile();
            Assert.That(blocked.Succeeded, Is.False);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));
            latestLifecycleResult = null;
            presenter.Refresh();

            var root = bootstrap.UiDocument.rootVisualElement;
            var retry = root.Q<Button>("persistence-retry");
            Assert.That(retry.enabledSelf, Is.True);
            Assert.That(retry.focusable, Is.True);
            var promotionsBeforeRetry = store.PromotionCount;

            // Activate the visible Button through its keyboard/navigation submit path,
            // rather than invoking the presenter API directly.
            retry.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                retry.SendEvent(submit);
            }

            Assert.That(root.Q<Label>("persistence-status").text, Does.Contain("RETURN PENDING"));
            Assert.That(root.Q<Label>("persistence-feedback").text, Does.Contain("Applying offline return"));
            Assert.That(root.Q<Button>("persistence-save").enabledSelf, Is.False);
            Assert.That(root.Q<Button>("persistence-load").enabledSelf, Is.False);
            Assert.That(root.Q<Button>("persistence-reset").enabledSelf, Is.False);
            Assert.That(retry.enabledSelf, Is.False);
            Assert.That(root.Q<Button>("persistence-reload").enabledSelf, Is.False);
            Assert.That(root.Q<Button>("persistence-recover").enabledSelf, Is.False);
            Assert.That(store.PromotionCount, Is.EqualTo(promotionsBeforeRetry),
                "The pending retry state must render before the synchronous checkpoint promotion starts.");

            yield return null;
            yield return null;

            Assert.That(store.PromotionCount, Is.GreaterThan(promotionsBeforeRetry));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(root.Q<Label>("persistence-feedback").text, Does.Contain("Offline return completed and saved"));
            Assert.That(root.Q<Label>("persistence-summary").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(commands.GetPersistenceStatus().Status, Is.EqualTo(PersistenceStatusKind.Ready));
        }

        [Test]
        public void PreviousGenerationRecoveryIsAnExplicitAvailableAction()
        {
            Assert.That(commands.ResetDevelopmentScenario().Succeeded, Is.True);
            presenter.SaveNow();
            var firstId = store.Active.Id;
            Assert.That(commands.ResetDevelopmentScenario().Succeeded, Is.True);
            presenter.SaveNow();
            Assert.That(store.Previous.Id, Is.EqualTo(firstId));
            Assert.That(bootstrap.UiDocument.rootVisualElement.Q<Button>("persistence-recover").enabledSelf, Is.True);

            presenter.RecoverPreviousCheckpoint();

            Assert.That(commands.GetPersistenceStatus().Status, Is.EqualTo(PersistenceStatusKind.Recovered));
            Assert.That(store.Active.ReconciledFromCheckpointId, Is.EqualTo(firstId));
            Assert.That(cancelPreviewCount, Is.EqualTo(1));
            Assert.That(cancelPointerCount, Is.EqualTo(1));
            Assert.That(factoryRefreshCount, Is.EqualTo(1));
            Assert.That(bootstrap.UiDocument.rootVisualElement.Q<Label>("persistence-status").text,
                Does.Contain("RECOVERED"));
        }

        [UnityTest]
        public IEnumerator KeyboardSavePresentsItsInProgressStateBeforeTheDurableCommandRuns()
        {
            var root = bootstrap.UiDocument.rootVisualElement;
            var save = root.Q<Button>("persistence-save");
            Assert.That(save.focusable, Is.True);
            save.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                save.SendEvent(submit);
            }

            Assert.That(root.Q<Label>("persistence-status").text, Does.Contain("SAVING"));
            Assert.That(save.enabledSelf, Is.False);
            yield return null;
            yield return null;

            Assert.That(store.Active, Is.Not.Null);
            Assert.That(root.Q<Label>("persistence-status").text, Does.Contain("READY"));
            Assert.That(root.Q<Label>("persistence-feedback").text, Does.Contain("Checkpoint saved"));
        }

        [UnityTest]
        public IEnumerator ControlsRemainFocusableAndScrollableAcrossSupportedViewportClasses()
        {
            var documentRoot = bootstrap.UiDocument.rootVisualElement;
            var originalWidth = documentRoot.style.width;
            var originalHeight = documentRoot.style.height;
            var cases = new[]
            {
                (360, 640), (390, 844), (768, 1024),
                (640, 360), (844, 390), (1024, 768),
                (1280, 720), (1920, 1080),
            };

            foreach (var viewport in cases)
            {
                documentRoot.style.width = viewport.Item1;
                documentRoot.style.height = viewport.Item2;
                bootstrap.Hud.ApplyLayout(
                    viewport.Item1,
                    viewport.Item2,
                    new Rect(18f, 14f, viewport.Item1 - 36f, viewport.Item2 - 28f));
                yield return null;
                yield return null;

                var scroll = bootstrap.UiDocument.rootVisualElement.Q<ScrollView>("inspector-scroll");
                var viewportElement = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport");
                foreach (var name in new[]
                         {
                             "persistence-save", "persistence-load", "persistence-reset",
                             "persistence-retry", "persistence-reload", "persistence-recover",
                         })
                {
                    var button = bootstrap.UiDocument.rootVisualElement.Q<Button>(name);
                    Assert.That(button, Is.Not.Null, name);
                    Assert.That(button.focusable, Is.True, name);
                    Assert.That(button.text, Is.Not.Empty, name);
                    Assert.That(button.resolvedStyle.minWidth.value, Is.GreaterThanOrEqualTo(44f), name);
                    Assert.That(button.resolvedStyle.minHeight.value, Is.GreaterThanOrEqualTo(44f), name);
                    scroll.ScrollTo(button);
                    yield return null;
                    yield return null;
                    AssertRectWithin(viewportElement.worldBound, button.worldBound,
                        $"{name} remains reachable at {viewport.Item1}x{viewport.Item2}.");
                }
            }

            documentRoot.style.width = originalWidth;
            documentRoot.style.height = originalHeight;
            bootstrap.ApplySafeAreaLayout(true);
            yield return null;
        }

        private void RefreshFactoryAndSelection(FactorySnapshot snapshot)
        {
            factoryRefreshCount++;
            refreshedFactory = snapshot;
            refreshedInspection = null;
            if (!selectedInstanceId.HasValue || !snapshot.Buildings.Any(item => item.InstanceId == selectedInstanceId.Value))
            {
                selectedInstanceId = null;
                return;
            }

            var inspection = session.GetMachineInspection(selectedInstanceId.Value);
            if (inspection.Succeeded && inspection.Value.Revision == snapshot.Revision)
            {
                refreshedInspection = inspection.Value;
            }
            else
            {
                selectedInstanceId = null;
            }
        }

        private CommandResult<MachineInspectionSnapshot> ReadRetainedProductionProgress(FactorySnapshot resultFactory)
        {
            var candidate = resultFactory.Buildings.FirstOrDefault();
            return candidate == null
                ? CommandResult<MachineInspectionSnapshot>.Rejected(CommandOutcome.NotFound, "No result machine is available.")
                : session.GetMachineInspection(candidate.InstanceId);
        }

        private static void AssertRectWithin(Rect outer, Rect inner, string message)
        {
            const float tolerance = 1f;
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - tolerance), message + " (left)");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - tolerance), message + " (top)");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + tolerance), message + " (right)");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + tolerance), message + " (bottom)");
        }

        private sealed class FakeClock : IUtcClock
        {
            public FakeClock(DateTimeOffset now)
            {
                Now = now;
            }

            public DateTimeOffset Now { get; set; }
            public DateTimeOffset GetUtcNow() => Now;
        }

        private sealed class FakeCheckpointIdSource : ICheckpointIdSource
        {
            private int next = 100;

            public CommandResult<CheckpointId> Next() =>
                CommandResult<CheckpointId>.Success(new CheckpointId(new Guid(next++, 0, 0, new byte[8])));
        }

        private sealed class FakeCheckpointStore : ICheckpointStore
        {
            public Checkpoint Active { get; private set; }
            public Checkpoint Previous { get; private set; }
            public int ActiveReadCount { get; private set; }
            public int PreviousReadCount { get; private set; }
            public int PromotionCount { get; private set; }
            public bool FailNextPromotion { get; set; }
            public string FailureMessage { get; set; } = "The isolated fake store rejected the write.";

            public CheckpointReadResult ReadActive()
            {
                ActiveReadCount++;
                return Active == null
                    ? CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "No isolated checkpoint exists.")
                    : CheckpointReadResult.Success(Clone(Active));
            }

            public CheckpointReadResult ReadPrevious()
            {
                PreviousReadCount++;
                return Previous == null
                    ? CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "No previous isolated checkpoint exists.")
                    : CheckpointReadResult.Success(Clone(Previous));
            }

            public CheckpointReadResult Read(CheckpointId checkpointId)
            {
                if (Active != null && Active.Id == checkpointId) return CheckpointReadResult.Success(Clone(Active));
                if (Previous != null && Previous.Id == checkpointId) return CheckpointReadResult.Success(Clone(Previous));
                return CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "The isolated checkpoint was not found.");
            }

            public CommandResult<CheckpointId> Promote(Checkpoint checkpoint)
            {
                PromotionCount++;
                if (FailNextPromotion)
                {
                    FailNextPromotion = false;
                    return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, FailureMessage);
                }

                if (Active != null && Active.Id == checkpoint.Id && Active.Generation == checkpoint.Generation)
                {
                    return CommandResult<CheckpointId>.Success(checkpoint.Id);
                }

                if (Active != null && checkpoint.Generation <= Active.Generation)
                {
                    return CommandResult<CheckpointId>.Rejected(CommandOutcome.InvalidCheckpoint, "Generation did not advance.");
                }

                Previous = Active == null ? null : Clone(Active);
                Active = Clone(checkpoint);
                return CommandResult<CheckpointId>.Success(checkpoint.Id);
            }

            public CommandResult<CheckpointId> RecoverPrevious(Checkpoint recoveryCandidate)
            {
                if (Previous == null)
                {
                    return CommandResult<CheckpointId>.Rejected(CommandOutcome.NoCheckpoint, "No previous generation is available.");
                }

                if (Active != null && Active.Id == recoveryCandidate.Id && Active.Generation == recoveryCandidate.Generation)
                {
                    return CommandResult<CheckpointId>.Success(recoveryCandidate.Id);
                }

                if (Active == null || recoveryCandidate.ReconciledFromCheckpointId != Previous.Id ||
                    recoveryCandidate.Generation <= Active.Generation)
                {
                    return CommandResult<CheckpointId>.Rejected(CommandOutcome.InvalidCheckpoint, "Recovery candidate did not match the isolated previous generation.");
                }

                var oldActive = Active;
                Active = Clone(recoveryCandidate);
                Previous = Clone(oldActive);
                return CommandResult<CheckpointId>.Success(recoveryCandidate.Id);
            }

            private static Checkpoint Clone(Checkpoint source) => new Checkpoint(
                source.Id,
                source.Generation,
                source.DepartureUtc,
                source.OfflinePolicyRevision,
                source.ReconciledFromCheckpointId,
                source.CreateFactoryCopy());
        }
    }
}
