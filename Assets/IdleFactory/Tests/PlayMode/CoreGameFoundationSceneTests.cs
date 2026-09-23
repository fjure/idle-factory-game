using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Domain;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Input;
using IdleFactory.Unity.Presentation;
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

        public override void Setup()
        {
            base.Setup();
            mouse = InputSystem.AddDevice<Mouse>();
            touchscreen = InputSystem.AddDevice<Touchscreen>();
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
            Assert.That(bootstrap.Hud.State, Is.EqualTo(FactoryHudState.Empty));
            Assert.That(bootstrap.Snapshot.Buildings, Is.Empty);
            Assert.That(bootstrap.Snapshot.Occupancy, Is.Empty);

            AssertWorldAndControlHitOwnership(bootstrap);

            var definitionId = new BuildingDefinitionId("dev.machine.basic");
            Assert.That(bootstrap.PlacementController.StartPlacement(definitionId, new GridPosition(1, 1)), Is.True);
            Assert.That(bootstrap.PlacementController.CanConfirm, Is.True, bootstrap.PlacementController.Feedback);
            bootstrap.PlacementController.Confirm();
            yield return null;

            var placed = bootstrap.Snapshot;
            Assert.That(placed.Buildings, Has.Count.EqualTo(1));
            Assert.That(placed.Occupancy, Has.Count.EqualTo(4));
            Assert.That(bootstrap.FactoryView.SelectedInstanceId, Is.EqualTo(placed.Buildings[0].InstanceId));

            var revisionBeforeReject = placed.Revision;
            Assert.That(bootstrap.PlacementController.StartPlacement(definitionId, new GridPosition(2, 2)), Is.True);
            Assert.That(bootstrap.PlacementController.CanConfirm, Is.False);
            yield return null;
            Assert.That(bootstrap.Hud.ConfirmButton.enabledSelf, Is.False);
            Assert.That(bootstrap.Hud.FeedbackText, Does.Contain("overlaps an existing building"));
            var disabledConfirmPoint = ToScreenPoint(bootstrap.Hud.ConfirmButton.worldBound.center, PanelScale(bootstrap));
            Assert.That(bootstrap.NavigationInput.UiHitTest(disabledConfirmPoint), Is.True,
                "A disabled Confirm control must still own the pointer that begins over it.");
            bootstrap.PlacementController.Confirm();
            Assert.That(bootstrap.Snapshot.Revision, Is.EqualTo(revisionBeforeReject));
            Assert.That(bootstrap.Snapshot.Buildings, Has.Count.EqualTo(1));
            Assert.That(bootstrap.Snapshot.Occupancy, Has.Count.EqualTo(4));

            Assert.That(bootstrap.PlacementController.StartPlacement(definitionId, new GridPosition(15, 15)), Is.True);
            Assert.That(bootstrap.PlacementController.CanConfirm, Is.False);
            yield return null;
            Assert.That(bootstrap.Hud.FeedbackText, Does.Contain("inside the factory"));
            bootstrap.PlacementController.Confirm();
            Assert.That(bootstrap.Snapshot.Revision, Is.EqualTo(revisionBeforeReject));
            Assert.That(bootstrap.Snapshot.Buildings, Has.Count.EqualTo(1));
            Assert.That(bootstrap.Snapshot.Occupancy, Has.Count.EqualTo(4));

            bootstrap.PlacementController.Cancel();
            var buildingCellPoint = CellScreenPoint(bootstrap, 1, 1);
            QueueTouch(touchscreen, 11, InputTouchPhase.Began, buildingCellPoint);
            QueueTouch(touchscreen, 11, InputTouchPhase.Ended, buildingCellPoint);
            yield return null;
            Assert.That(bootstrap.FactoryView.SelectedInstanceId, Is.EqualTo(placed.Buildings[0].InstanceId),
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
            Assert.That(bootstrap.FactoryView.SelectedInstanceId, Is.EqualTo(placed.Buildings[0].InstanceId),
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

            bootstrap.FactoryView.SelectAt(new GridPosition(1, 1));
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
    }
}
