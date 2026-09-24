using System.Collections;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace IdleFactory.Tests.PlayMode
{
    public sealed class FactoryRuntimeHostTests
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";

        private Scene loadedScene;
        private GameObject hostObject;
        private FactoryRuntimeHost runtimeHost;
        private ProductionCommands commands;
        private BuildingInstanceId buildingId;
        private double currentActiveSeconds;

        [UnitySetUp]
        public IEnumerator LoadRunningFactory()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The development scene is loaded directly by the Editor test harness.");
            yield break;
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
            var bootstraps = loadedScene.GetRootGameObjects()
                .SelectMany(gameObject => gameObject.GetComponentsInChildren<CoreGameFoundationBootstrap>(true))
                .ToArray();
            Assert.That(bootstraps, Has.Length.EqualTo(1));
            Assert.That(bootstraps[0].IsInitialized, Is.True, bootstraps[0].InitializationError);

            var bootstrap = bootstraps[0];
            Assert.That(bootstrap.PlacementController.StartPlacement(
                new BuildingDefinitionId("dev.machine.basic"), new GridPosition(1, 1)), Is.True);
            bootstrap.PlacementController.Confirm();

            var placedBuilding = bootstrap.Snapshot.Buildings.Single();
            buildingId = placedBuilding.InstanceId;
            commands = new ProductionCommands(bootstrap.Session);
            var supplied = commands.AddResource(
                buildingId,
                BufferRole.Input,
                new ResourceId("dev.input"),
                2L);
            Assert.That(supplied.Succeeded, Is.True, supplied.Message);
            var activated = commands.SetBuildingActive(buildingId, true);
            Assert.That(activated.Succeeded, Is.True, activated.Message);

            currentActiveSeconds = 100d;
            hostObject = new GameObject("Factory Runtime Host Test");
            runtimeHost = hostObject.AddComponent<FactoryRuntimeHost>();
            runtimeHost.Configure(commands, () => currentActiveSeconds);
            yield return null;
#endif
        }

        [UnityTearDown]
        public IEnumerator UnloadRunningFactory()
        {
            if (hostObject != null)
            {
                Object.Destroy(hostObject);
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
            hostObject = null;
            runtimeHost = null;
            commands = null;
        }

        [UnityTest]
        public IEnumerator CumulativeBoundaryConversionRetainsSubtickTimeAcrossFrames()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The development scene is loaded directly by the Editor test harness.");
            yield break;
#else
            for (var sample = 1; sample <= 10; sample++)
            {
                currentActiveSeconds = 100d + sample * 0.00000006d;
                yield return null;
                yield return null;
            }

            Assert.That(GetProgressTicks(), Is.EqualTo(6L),
                "Ten 0.6-tick frame intervals must become six cumulative 100-ns Core ticks.");

            currentActiveSeconds = 102.5d;
            yield return null;
            yield return null;
            Assert.That(GetProgressTicks(), Is.EqualTo(25_000_000L),
                "The cumulative active-time boundary must not drift after fractional frame intervals.");
#endif
        }

        [UnityTest]
        public IEnumerator PauseAndFocusLossExcludeBackgroundTimeAndResumeFromFreshBoundaries()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The development scene is loaded directly by the Editor test harness.");
            yield break;
#else
            currentActiveSeconds = 101d;
            yield return null;
            yield return null;
            Assert.That(GetProgressTicks(), Is.EqualTo(10_000_000L));

            hostObject.SendMessage("OnApplicationPause", true);
            currentActiveSeconds = 1001d;
            yield return null;
            yield return null;
            Assert.That(GetProgressTicks(), Is.EqualTo(10_000_000L), "Paused time must not advance production.");

            hostObject.SendMessage("OnApplicationPause", false);
            currentActiveSeconds = 1001.25d;
            yield return null;
            yield return null;
            Assert.That(GetProgressTicks(), Is.EqualTo(12_500_000L), "Resume must begin at the return boundary.");

            hostObject.SendMessage("OnApplicationFocus", false);
            currentActiveSeconds = 2001.5d;
            yield return null;
            yield return null;
            Assert.That(GetProgressTicks(), Is.EqualTo(12_500_000L), "Focus loss must stop active advancement.");

            hostObject.SendMessage("OnApplicationPause", false);
            currentActiveSeconds = 2002d;
            yield return null;
            yield return null;
            Assert.That(GetProgressTicks(), Is.EqualTo(12_500_000L),
                "A pause callback must not resume while focus is still lost.");

            hostObject.SendMessage("OnApplicationFocus", true);
            currentActiveSeconds = 2002.25d;
            yield return null;
            yield return null;
            Assert.That(GetProgressTicks(), Is.EqualTo(15_000_000L),
                "Focus return must start a fresh active-time segment without background credit.");
#endif
        }

        [UnityTest]
        public IEnumerator DisabledHostDoesNotCreditTimeBeforeItIsEnabledAgain()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The development scene is loaded directly by the Editor test harness.");
            yield break;
#else
            currentActiveSeconds = 100.5d;
            yield return null;
            yield return null;
            Assert.That(GetProgressTicks(), Is.EqualTo(5_000_000L));

            runtimeHost.enabled = false;
            currentActiveSeconds = 900d;
            yield return null;
            Assert.That(GetProgressTicks(), Is.EqualTo(5_000_000L));

            runtimeHost.enabled = true;
            currentActiveSeconds = 900.25d;
            yield return null;
            yield return null;
            Assert.That(GetProgressTicks(), Is.EqualTo(7_500_000L),
                "Re-enabling the host must establish a new cumulative boundary.");
#endif
        }

        private long GetProgressTicks()
        {
            var inspection = commands.GetMachineInspection(buildingId);
            Assert.That(inspection.Succeeded, Is.True, inspection.Message);
            return inspection.Value.ProgressTicks;
        }
    }
}
