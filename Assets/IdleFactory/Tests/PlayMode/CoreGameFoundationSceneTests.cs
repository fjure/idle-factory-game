using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Input;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;
using InputMouseButton = UnityEngine.InputSystem.LowLevel.MouseButton;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace IdleFactory.Tests.PlayMode
{
    public sealed class CoreGameFoundationSceneTests : InputTestFixture
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private Scene loadedScene;
        private Mouse mouse;
        private Touchscreen touchscreen;
        private SceneCompositionTestScope compositionScope;
        private IsolatedCheckpointStore isolatedStore;
        private SceneCompositionTestClock fakeClock;

        public override void Setup()
        {
            base.Setup();
            mouse = InputSystem.AddDevice<Mouse>();
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            compositionScope = SceneCompositionTestScope.WithNoCheckpoint();
            isolatedStore = compositionScope.Store;
            fakeClock = compositionScope.Clock;
        }

        public override void TearDown()
        {
            base.TearDown();
            mouse = null;
            touchscreen = null;
        }

        [UnityTearDown]
        public IEnumerator UnloadDevelopmentScene()
        {
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(loadedScene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }

            loadedScene = default;
            compositionScope?.Dispose();
            compositionScope = null;
            isolatedStore = null;
            fakeClock = null;
        }

        [UnityTest]
        public IEnumerator AuthoredSceneUsesOneIsolatedCompositionAndReconcilesLifecycleBeforePublication()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The development scene is loaded directly by the Editor test harness.");
#else
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
            {
                yield return null;
            }
            yield return null;

            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            var scene = loadedScene;
            var rootObjects = scene.GetRootGameObjects();
            var bootstraps = rootObjects.SelectMany(gameObject => gameObject.GetComponentsInChildren<CoreGameFoundationBootstrap>(true)).ToArray();
            var lifecycleAdapters = rootObjects.SelectMany(gameObject => gameObject.GetComponentsInChildren<ApplicationLifecycleAdapter>(true)).ToArray();
            var persistencePresenters = rootObjects.SelectMany(gameObject => gameObject.GetComponentsInChildren<PersistencePresenter>(true)).ToArray();
            var runtimeHosts = rootObjects.SelectMany(gameObject => gameObject.GetComponentsInChildren<FactoryRuntimeHost>(true)).ToArray();
            var inspectors = rootObjects.SelectMany(gameObject => gameObject.GetComponentsInChildren<MachineInspectorPresenter>(true)).ToArray();
            Assert.That(bootstraps, Has.Length.EqualTo(1));
            Assert.That(lifecycleAdapters, Has.Length.EqualTo(1));
            Assert.That(persistencePresenters, Has.Length.EqualTo(1));
            Assert.That(runtimeHosts, Has.Length.EqualTo(1));
            Assert.That(inspectors, Has.Length.EqualTo(1));

            var bootstrap = bootstraps[0];
            var lifecycle = lifecycleAdapters[0];
            var persistence = persistencePresenters[0];
            var runtime = runtimeHosts[0];
            var inspector = inspectors[0];
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            Assert.That(bootstrap.Session, Is.Not.Null);
            Assert.That(bootstrap.Session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(lifecycle.LastResult, Is.Not.Null);
            Assert.That(lifecycle.LastResult.Succeeded, Is.True, lifecycle.LastResult.Message);
            Assert.That(lifecycle.LastResult.Value.UsedFallback, Is.False,
                "Selecting a cold-start active checkpoint is normal reconciliation, not an in-process fallback.");
            Assert.That(bootstrap.Snapshot.Buildings, Has.Count.EqualTo(1));
            Assert.That(lifecycle.LastResult.Value.OfflineSummary.Outcome, Is.EqualTo(OfflineReturnOutcome.NoCheckpoint));
            Assert.That(isolatedStore.ActiveReadCount, Is.GreaterThanOrEqualTo(1),
                "Cold-start reconciliation must consult only the isolated store.");
            Assert.That(isolatedStore.PromotionCount, Is.Zero);
            Assert.That(persistence.IsConfigured, Is.True, persistence.Error);
            Assert.That(bootstrap.PersistenceCommands, Is.Not.Null);
            Assert.That(bootstrap.ProductionCommands, Is.Not.Null);
            Assert.That(inspector.IsConfigured, Is.True, inspector.Error);
            Assert.That(runtime.enabled, Is.True, "The reconciled ready D001 machine should advance through the integrated runtime host.");

            var definitionId = new BuildingDefinitionId("dev.machine.basic");
            Assert.That(bootstrap.PlacementController.StartPlacement(definitionId, new GridPosition(3, 3)), Is.True);
            bootstrap.PlacementController.Confirm();
            var firstInstanceId = bootstrap.FactoryView.SelectedInstanceId.Value;
            Assert.That(bootstrap.PlacementController.StartPlacement(definitionId, new GridPosition(7, 7)), Is.True);
            bootstrap.PlacementController.Confirm();
            Assert.That(bootstrap.Snapshot.Buildings, Has.Count.EqualTo(3));
            persistence.SaveNow();
            Assert.That(isolatedStore.Active, Is.Not.Null);
            Assert.That(isolatedStore.Active.CreateFactoryCopy().Buildings, Has.Count.EqualTo(3),
                "The persistence presenter must save the live world placed through the scene's shared session.");

            bootstrap.FactoryView.SelectAt(new GridPosition(3, 3));
            Assert.That(bootstrap.FactoryView.SelectedInstanceId, Is.EqualTo(firstInstanceId));
            Assert.That(bootstrap.PlacementController.StartPlacement(definitionId, new GridPosition(10, 10)), Is.True);
            bootstrap.NavigationInput.Interpreter.BeginPointer(71, new Vector2(200f, 200f), false, true);
            Assert.That(bootstrap.NavigationInput.Interpreter.HasActiveGesture, Is.True);
            persistence.LoadDevelopmentCheckpoint();
            Assert.That(bootstrap.Session.GetFactorySnapshot().Buildings, Has.Count.EqualTo(3));
            Assert.That(bootstrap.FactoryView.SelectedInstanceId, Is.EqualTo(firstInstanceId),
                "A successful load must resolve selection again by persistent instance ID.");
            Assert.That(bootstrap.PlacementController.Mode, Is.EqualTo(PlacementInteractionMode.Browse));
            Assert.That(bootstrap.NavigationInput.Interpreter.HasActiveGesture, Is.False);
            Assert.That(bootstrap.FactoryView.RenderedRevision, Is.EqualTo(bootstrap.Snapshot.Revision));
            Assert.That(inspector.LastRenderedRevision, Is.EqualTo(bootstrap.Snapshot.Revision));

            bootstrap.FactoryView.SelectAt(new GridPosition(3, 3));
            Assert.That(bootstrap.PlacementController.StartPlacement(definitionId, new GridPosition(10, 10)), Is.True);
            bootstrap.NavigationInput.Interpreter.BeginPointer(72, new Vector2(240f, 240f), false, true);
            persistence.ResetDevelopmentScenario();
            Assert.That(bootstrap.Snapshot.Buildings, Has.Count.EqualTo(1));
            Assert.That(bootstrap.Snapshot.Buildings[0].InstanceId, Is.Not.EqualTo(firstInstanceId));
            Assert.That(bootstrap.FactoryView.SelectedInstanceId, Is.Null,
                "Reset replaces the world with a new persistent identity and must clear stale selection.");
            Assert.That(bootstrap.PlacementController.Mode, Is.EqualTo(PlacementInteractionMode.Browse));
            Assert.That(bootstrap.NavigationInput.Interpreter.HasActiveGesture, Is.False);
            Assert.That(isolatedStore.Active.CreateFactoryCopy().Buildings, Has.Count.EqualTo(3),
                "Reset is a live-world replacement and must not overwrite the saved generation.");

            bootstrap.FactoryView.SelectAt(new GridPosition(0, 0));
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(inspector.IsConfigured, Is.True, inspector.Error);
            Assert.That(runtime.enabled, Is.True, "The ready D001 machine should own active-time advancement while Running.");
            Assert.That(runtime.IsApplicationLifecycleAvailable, Is.True);

            fakeClock.CumulativeActiveSeconds = 1d;
            var d001InstanceId = bootstrap.Snapshot.Buildings.Single().InstanceId;
            InvokeHostLifecycleCallback(runtime, "OnApplicationFocus", false);
            InvokeHostLifecycleCallback(runtime, "OnApplicationPause", true);
            Assert.That(runtime.IsApplicationLifecycleAvailable, Is.False,
                "The authored host must stop active time on Unity focus-loss and pause callbacks before adapter departure.");
            lifecycle.NotifyFocusChanged(false);
            Assert.That(bootstrap.Session.LifecycleState, Is.EqualTo(LifecycleState.Suspended),
                "The lifecycle adapter must synchronously run its pre-departure hook before suspension is recorded.");
            Assert.That(runtime.enabled, Is.False);
            Assert.That(runtime.IsApplicationLifecycleAvailable, Is.False);
            Assert.That(isolatedStore.Active.CreateFactoryCopy().Buildings[d001InstanceId].Production.ProgressTicks,
                Is.EqualTo(TimeSpan.TicksPerSecond),
                "The suspension checkpoint must include the final active interval flushed by the adapter's pre-departure hook.");

            lifecycle.NotifyFocusChanged(true);
            Assert.That(lifecycle.LastResult.Succeeded, Is.True, lifecycle.LastResult.Message);
            Assert.That(bootstrap.Session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(runtime.enabled, Is.True,
                "A successful return must let the inspector re-enable the shared runtime host for runnable machines.");
            Assert.That(runtime.IsApplicationLifecycleAvailable, Is.True,
                "A successful adapter reconcile must synchronize availability even when the disabled host missed Unity's return callbacks.");
            fakeClock.CumulativeActiveSeconds = 1.5d;
            yield return null;
            yield return null;
            Assert.That(bootstrap.ProductionCommands.GetMachineInspection(d001InstanceId).Value.ProgressTicks,
                Is.EqualTo(TimeSpan.TicksPerSecond + TimeSpan.TicksPerSecond / 2),
                "The runtime host must advance again from a fresh post-return active-time boundary.");

            lifecycle.NotifyFocusChanged(false);
            Assert.That(bootstrap.Session.LifecycleState, Is.EqualTo(LifecycleState.Suspended));
            Assert.That(isolatedStore.Active.CreateFactoryCopy().Buildings[d001InstanceId].Production.ProgressTicks,
                Is.EqualTo(TimeSpan.TicksPerSecond + TimeSpan.TicksPerSecond / 2),
                "A second suspension must preserve progress advanced after the successful return.");

            isolatedStore.FailNextPromotion = true;
            lifecycle.NotifyFocusChanged(true);
            Assert.That(bootstrap.Session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(runtime.enabled, Is.False);
            Assert.That(runtime.IsApplicationLifecycleAvailable, Is.False,
                "A blocked return must not reset the runtime host's unavailable lifecycle state.");
            var controls = bootstrap.UiDocument.rootVisualElement;
            Assert.That(controls.Q<Button>("inspector-supply-input").enabledSelf, Is.False);
            Assert.That(controls.Q<Button>("inspector-toggle-active").enabledSelf, Is.False);
            var blockedRevision = bootstrap.Snapshot.Revision;
            inspector.SupplySelectedInput();
            inspector.ToggleSelectedActive();
            Assert.That(bootstrap.Snapshot.Revision, Is.EqualTo(blockedRevision),
                "Direct inspector calls must also honor ReturnBlocked rather than relying on disabled buttons.");
#endif
        }

        [UnityTest]
        public IEnumerator AuthoredSceneRunsPlacementInputAndViewRecreationWithoutChangingWorldOwnership()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The development scene is loaded directly by the Editor test harness.");
#else
            EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            while (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
            {
                yield return null;
            }
            yield return null;

            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            var scene = loadedScene;
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);

            var bootstraps = scene.GetRootGameObjects()
                .SelectMany(gameObject => gameObject.GetComponentsInChildren<CoreGameFoundationBootstrap>(true))
                .ToArray();
            Assert.That(bootstraps, Has.Length.EqualTo(1));

            var bootstrap = bootstraps[0];
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            Assert.That(bootstrap.Session, Is.Not.Null);
            Assert.That(bootstrap.Hud.State, Is.EqualTo(FactoryHudState.Normal));
            Assert.That(bootstrap.Session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(bootstrap.Snapshot.Buildings, Has.Count.EqualTo(1),
                "A no-checkpoint cold start keeps the prepared D001 scenario available with zero offline credit.");
            Assert.That(bootstrap.Snapshot.Buildings[0].DefinitionId.Value, Is.EqualTo("dev.machine.basic"));
            Assert.That(bootstrap.Snapshot.Occupancy, Has.Count.EqualTo(4));

            AssertWorldAndControlHitOwnership(bootstrap);

            var definitionId = new BuildingDefinitionId("dev.machine.basic");
            Assert.That(bootstrap.PlacementController.StartPlacement(definitionId, new GridPosition(3, 3)), Is.True);
            Assert.That(bootstrap.PlacementController.CanConfirm, Is.True, bootstrap.PlacementController.Feedback);
            bootstrap.PlacementController.Confirm();
            yield return null;

            var placed = bootstrap.Snapshot;
            Assert.That(placed.Buildings, Has.Count.EqualTo(2));
            Assert.That(placed.Occupancy, Has.Count.EqualTo(8));
            var placedInstanceId = bootstrap.FactoryView.SelectedInstanceId.Value;
            Assert.That(placed.Buildings.Any(building => building.InstanceId == placedInstanceId), Is.True);

            var revisionBeforeReject = placed.Revision;
            Assert.That(bootstrap.PlacementController.StartPlacement(definitionId, new GridPosition(4, 4)), Is.True);
            Assert.That(bootstrap.PlacementController.CanConfirm, Is.False);
            yield return null;
            Assert.That(bootstrap.Hud.ConfirmButton.enabledSelf, Is.False);
            Assert.That(bootstrap.Hud.FeedbackText, Does.Contain("overlaps an existing building"));
            var disabledConfirmPoint = ToScreenPoint(bootstrap.Hud.ConfirmButton.worldBound.center, PanelScale(bootstrap));
            Assert.That(bootstrap.NavigationInput.UiHitTest(disabledConfirmPoint), Is.True,
                "A disabled Confirm control must still own the pointer that begins over it.");
            bootstrap.PlacementController.Confirm();
            Assert.That(bootstrap.Snapshot.Revision, Is.EqualTo(revisionBeforeReject));
            Assert.That(bootstrap.Snapshot.Buildings, Has.Count.EqualTo(2));
            Assert.That(bootstrap.Snapshot.Occupancy, Has.Count.EqualTo(8));

            Assert.That(bootstrap.PlacementController.StartPlacement(definitionId, new GridPosition(15, 15)), Is.True);
            Assert.That(bootstrap.PlacementController.CanConfirm, Is.False);
            yield return null;
            Assert.That(bootstrap.Hud.FeedbackText, Does.Contain("inside the factory"));
            bootstrap.PlacementController.Confirm();
            Assert.That(bootstrap.Snapshot.Revision, Is.EqualTo(revisionBeforeReject));
            Assert.That(bootstrap.Snapshot.Buildings, Has.Count.EqualTo(2));
            Assert.That(bootstrap.Snapshot.Occupancy, Has.Count.EqualTo(8));

            bootstrap.PlacementController.Cancel();
            var buildingCellPoint = CellScreenPoint(bootstrap, 3, 3);
            QueueTouch(touchscreen, 11, InputTouchPhase.Began, buildingCellPoint);
            QueueTouch(touchscreen, 11, InputTouchPhase.Ended, buildingCellPoint);
            yield return null;
            Assert.That(bootstrap.FactoryView.SelectedInstanceId, Is.EqualTo(placedInstanceId),
                "A touch tap must select the placed building through the authored input adapter.");
            Assert.That(bootstrap.Hud.SelectionText, Does.Contain("dev.machine.basic"),
                "Touch selection must show the building inspection feedback.");

            var emptyCellPoint = CellScreenPoint(bootstrap, 8, 8);
            QueueTouch(touchscreen, 12, InputTouchPhase.Began, emptyCellPoint);
            QueueTouch(touchscreen, 12, InputTouchPhase.Ended, emptyCellPoint);
            yield return null;
            Assert.That(bootstrap.FactoryView.SelectedInstanceId, Is.Null);
            Assert.That(bootstrap.Hud.SelectionText, Does.Contain("No building selected"));
            Assert.That(bootstrap.Snapshot.Revision, Is.EqualTo(revisionBeforeReject));

            QueueMouse(mouse, buildingCellPoint, left: true);
            QueueMouse(mouse, buildingCellPoint);
            yield return null;
            Assert.That(bootstrap.FactoryView.SelectedInstanceId, Is.EqualTo(placedInstanceId),
                "A desktop pointer tap must select a building through the authored input adapter.");

            var viewportCenter = bootstrap.FactoryCamera.ViewportToScreenPoint(new Vector3(0.5f, 0.5f, 0f));
            var cameraCenter = new Vector2(viewportCenter.x, viewportCenter.y);
            var beforeWheel = bootstrap.FactoryCamera.orthographicSize;
            QueueMouse(mouse, cameraCenter, scroll: new Vector2(0f, 120f));
            Assert.That(bootstrap.FactoryCamera.orthographicSize, Is.Not.EqualTo(beforeWheel),
                "Desktop wheel input must zoom the authored factory camera.");

            var beforeMousePan = bootstrap.FactoryCamera.transform.position;
            QueueMouse(mouse, cameraCenter, middle: true);
            QueueMouse(mouse, cameraCenter + new Vector2(40f, 0f), middle: true);
            QueueMouse(mouse, cameraCenter + new Vector2(40f, 0f));
            yield return null;
            Assert.That(bootstrap.FactoryCamera.transform.position, Is.Not.EqualTo(beforeMousePan),
                "Desktop drag input must pan the authored factory camera.");

            viewportCenter = bootstrap.FactoryCamera.ViewportToScreenPoint(new Vector3(0.5f, 0.5f, 0f));
            cameraCenter = new Vector2(viewportCenter.x, viewportCenter.y);
            var beforeTouchPan = bootstrap.FactoryCamera.transform.position;
            QueueTouch(touchscreen, 12, InputTouchPhase.Began, cameraCenter);
            QueueTouch(touchscreen, 12, InputTouchPhase.Moved, cameraCenter - new Vector2(40f, 0f), new Vector2(-40f, 0f));
            QueueTouch(touchscreen, 12, InputTouchPhase.Ended, cameraCenter - new Vector2(40f, 0f));
            yield return null;
            Assert.That(bootstrap.FactoryCamera.transform.position, Is.Not.EqualTo(beforeTouchPan),
                "Touch drag input must pan the authored factory camera.");

            bootstrap.FactoryView.SelectAt(new GridPosition(3, 3));
            var selectedId = bootstrap.FactoryView.SelectedInstanceId;
            var beforeRecreation = bootstrap.Snapshot;
            var replacement = bootstrap.RecreateFactoryView();
            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement.SelectedInstanceId, Is.EqualTo(selectedId));
            Assert.That(bootstrap.Snapshot.Revision, Is.EqualTo(beforeRecreation.Revision));
            Assert.That(bootstrap.Snapshot.Buildings, Has.Count.EqualTo(beforeRecreation.Buildings.Count));
            Assert.That(bootstrap.Snapshot.Occupancy, Has.Count.EqualTo(beforeRecreation.Occupancy.Count));

            Assert.That(bootstrap.PlacementController.StartPlacement(definitionId, new GridPosition(5, 5)), Is.True);
            var beforeFocus = bootstrap.Snapshot.Revision;
            bootstrap.NotifyFocusChanged(false);
            Assert.That(bootstrap.PlacementController.Mode, Is.EqualTo(PlacementInteractionMode.Browse));
            Assert.That(bootstrap.Snapshot.Revision, Is.EqualTo(beforeFocus));

            Assert.That(bootstrap.PlacementController.StartPlacement(definitionId, new GridPosition(6, 6)), Is.True);
            var beforeOrientation = bootstrap.Snapshot.Revision;
            var changedOrientation = Screen.orientation == ScreenOrientation.LandscapeLeft
                ? ScreenOrientation.LandscapeRight
                : ScreenOrientation.LandscapeLeft;
            bootstrap.NotifyOrientationChanged(changedOrientation);
            Assert.That(bootstrap.PlacementController.Mode, Is.EqualTo(PlacementInteractionMode.Browse));
            Assert.That(bootstrap.Snapshot.Revision, Is.EqualTo(beforeOrientation));
#endif
        }

        [UnityTest]
        public IEnumerator MissingSerializedReferencesDisableBootstrapWithActionableError()
        {
            var logger = Debug.unityLogger;
            var originalHandler = logger.logHandler;
            var recorder = new RecordingLogHandler();
            GameObject gameObject = null;
            logger.logHandler = recorder;
            try
            {
                gameObject = new GameObject("Invalid Core Game Foundation Bootstrap");
                var bootstrap = gameObject.AddComponent<CoreGameFoundationBootstrap>();

                Assert.That(bootstrap.IsInitialized, Is.False);
                Assert.That(bootstrap.enabled, Is.False);
                Assert.That(bootstrap.InitializationError, Does.Contain("Missing serialized references"));
                Assert.That(recorder.Errors, Has.Some.Contains("Core Game Foundation is disabled"));
            }
            finally
            {
                logger.logHandler = originalHandler;
                if (gameObject != null)
                {
                    Object.Destroy(gameObject);
                }
            }

            yield return null;
        }

        private static void AssertWorldAndControlHitOwnership(CoreGameFoundationBootstrap bootstrap)
        {
            var panelRoot = bootstrap.UiDocument.rootVisualElement;
            var panel = panelRoot.panel;
            Assert.That(panel, Is.Not.Null);

            var scale = panel.scaledPixelsPerPoint;
            var blankWorldPoint = ToScreenPoint(panelRoot.worldBound.center, scale);
            Assert.That(bootstrap.NavigationInput.UiHitTest(blankWorldPoint), Is.False,
                "Blank world space must remain available for pan, zoom, selection, and placement.");

            var informationPanel = panelRoot.Q<VisualElement>("information-panel");
            var informationPoint = ToScreenPoint(informationPanel.worldBound.center, scale);
            Assert.That(bootstrap.NavigationInput.UiHitTest(informationPoint), Is.True,
                "The opaque information panel must own pointer input.");

            var buttonPoint = ToScreenPoint(bootstrap.Hud.BuildButton.worldBound.center, scale);
            Assert.That(bootstrap.NavigationInput.UiHitTest(buttonPoint), Is.True,
                "Visible HUD controls must retain UI pointer ownership.");
        }

        private static Vector2 ToScreenPoint(Vector2 panelPoint, float scale)
        {
            return new Vector2(panelPoint.x * scale, Screen.height - panelPoint.y * scale);
        }

        private static float PanelScale(CoreGameFoundationBootstrap bootstrap) =>
            bootstrap.UiDocument.rootVisualElement.panel.scaledPixelsPerPoint;

        private static Vector2 CellScreenPoint(CoreGameFoundationBootstrap bootstrap, int x, int y)
        {
            var worldPoint = bootstrap.FactoryCamera.WorldToScreenPoint(new Vector3(x + 0.5f, y + 0.5f, 0f));
            return new Vector2(worldPoint.x, worldPoint.y);
        }

        private static void QueueMouse(
            Mouse device,
            Vector2 position,
            bool left = false,
            bool middle = false,
            Vector2 scroll = default)
        {
            var state = new MouseState { position = position, scroll = scroll };
            state = state.WithButton(InputMouseButton.Left, left);
            state = state.WithButton(InputMouseButton.Middle, middle);
            InputSystem.QueueStateEvent(device, state);
            InputSystem.Update();
        }

        private static void QueueTouch(
            Touchscreen device,
            int touchId,
            InputTouchPhase phase,
            Vector2 position,
            Vector2 delta = default)
        {
            InputSystem.QueueStateEvent(device, new TouchState
            {
                touchId = touchId,
                phase = phase,
                position = position,
                delta = delta,
                pressure = 1f,
            });
            InputSystem.Update();
        }

        private sealed class RecordingLogHandler : ILogHandler
        {
            public readonly List<string> Errors = new List<string>();

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                if (logType == LogType.Error)
                {
                    Errors.Add(string.Format(format, args));
                }
            }

            public void LogException(System.Exception exception, Object context)
            {
                Errors.Add(exception.ToString());
            }
        }

        private static void InvokeHostLifecycleCallback(FactoryRuntimeHost host, string callbackName, object value)
        {
            var callback = typeof(FactoryRuntimeHost).GetMethod(
                callbackName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(callback, Is.Not.Null, $"Expected Unity lifecycle callback '{callbackName}' on the authored runtime host.");
            callback.Invoke(host, new[] { value });
        }

    }
}
