using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Domain;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Input;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace IdleFactory.Tests.PlayMode
{
    public sealed class CoreGameFoundationSceneTests
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private Scene loadedScene;

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
            bootstrap.PlacementController.Confirm();
            Assert.That(bootstrap.Snapshot.Revision, Is.EqualTo(revisionBeforeReject));
            Assert.That(bootstrap.Snapshot.Buildings, Has.Count.EqualTo(1));
            Assert.That(bootstrap.Snapshot.Occupancy, Has.Count.EqualTo(4));

            Assert.That(bootstrap.PlacementController.StartPlacement(definitionId, new GridPosition(15, 15)), Is.True);
            Assert.That(bootstrap.PlacementController.CanConfirm, Is.False);
            bootstrap.PlacementController.Confirm();
            Assert.That(bootstrap.Snapshot.Revision, Is.EqualTo(revisionBeforeReject));
            Assert.That(bootstrap.Snapshot.Buildings, Has.Count.EqualTo(1));
            Assert.That(bootstrap.Snapshot.Occupancy, Has.Count.EqualTo(4));

            bootstrap.FactoryView.SelectAt(new GridPosition(8, 8));
            Assert.That(bootstrap.FactoryView.SelectedInstanceId, Is.Null);
            Assert.That(bootstrap.Hud.SelectionText, Does.Contain("No building selected"));
            Assert.That(bootstrap.Snapshot.Revision, Is.EqualTo(revisionBeforeReject));

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

            var buttonPoint = ToScreenPoint(bootstrap.Hud.BuildButton.worldBound.center, scale);
            Assert.That(bootstrap.NavigationInput.UiHitTest(buttonPoint), Is.True,
                "Visible HUD controls must retain UI pointer ownership.");
        }

        private static Vector2 ToScreenPoint(Vector2 panelPoint, float scale)
        {
            return new Vector2(panelPoint.x * scale, Screen.height - panelPoint.y * scale);
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
