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
    /// <summary>
    /// T063: the machine inspector's poll in the authored scene. Before T063 each poll built three full factory
    /// snapshots and, while no machine could produce, one full factory copy per machine (T040-F1). The scene is
    /// seeded with the W001 workload shape (1000 D001 machines: 500 producing, 250 output-blocked, 250 inactive).
    /// </summary>
    public sealed class InspectorPollingTests
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private const int W001MachineCount = 1000;
        private const long TickTicks = TimeSpan.TicksPerMillisecond * 100L;

        private Scene loadedScene;
        private SceneCompositionTestScope compositionScope;
        private CoreGameFoundationBootstrap bootstrap;
        private MachineInspectorPresenter presenter;
        private ProductionCommands commands;

        [UnityTearDown]
        public IEnumerator UnloadInspectorScene()
        {
            yield return UnloadScene();
        }

        [UnityTest]
        public IEnumerator W001PollAllocatesNothingWithUnchangedRevisionOrNoSelection()
        {
            yield return LoadScene(CreateW001Shape(W001MachineCount));
            Action poll = () => presenter.PollInspection();
            Assert.That(bootstrap.FactoryView.SelectedInstanceId.HasValue, Is.False);
            GcAllocationCounter.Count(poll);

            var unchanged = SamplePolls(poll, 20);
            Assert.That(unchanged, Has.All.EqualTo(0), "No selection, unchanged revision.");
            Assert.That(bootstrap.RuntimeHost.enabled, Is.True, "500 machines can produce.");

            var revisionChanges = new int[20];
            for (var sample = 0; sample < revisionChanges.Length; sample++)
            {
                Assert.That(commands.AdvanceActiveTime(TickTicks).Succeeded, Is.True);
                revisionChanges[sample] = GcAllocationCounter.Count(poll);
                Assert.That(presenter.LastRenderedRevision, Is.EqualTo(commands.CurrentRevision));
            }

            Assert.That(revisionChanges, Has.All.EqualTo(0), "No selection, changed revision.");

            var selected = commands.GetMachineInspection(FirstMachineId()).Value.InstanceId;
            bootstrap.FactoryView.Select(selected);
            Assert.That(presenter.LastRenderedRevision, Is.EqualTo(commands.CurrentRevision));
            GcAllocationCounter.Count(poll);
            Assert.That(SamplePolls(poll, 20), Has.All.EqualTo(0), "Selected machine, unchanged revision.");
            Assert.That(presenter.PollInspection(), Is.False, "An unchanged revision does not re-render.");
        }

        [UnityTest]
        public IEnumerator RevisionChangeReRendersOnlyTheSelectedMachineIndependentOfMachineCount()
        {
            yield return LoadScene(CreateW001Shape(1));
            var singleMachine = MeasureSelectedRevisionChangePoll();
            yield return UnloadScene();

            yield return LoadScene(CreateW001Shape(W001MachineCount));
            var w001 = MeasureSelectedRevisionChangePoll();

            Debug.Log($"T063 selected revision-change poll allocations: 1 machine={singleMachine}, {W001MachineCount} machines={w001}");
            Assert.That(singleMachine, Is.GreaterThan(0), "Re-rendering the inspector allocates its own strings.");
            Assert.That(w001, Is.EqualTo(singleMachine),
                "A revision change costs the same at 1 and 1000 machines: only the selected machine is inspected.");
        }

        [UnityTest]
        public IEnumerator PollEnablesTheRuntimeHostOnlyWhileAMachineCanProduce()
        {
            yield return LoadScene(catalog => new[]
            {
                CreateMachine(catalog, 1, new GridPosition(0, 0), active: true, input: 4, output: 0),
                CreateMachine(catalog, 2, new GridPosition(4, 0), active: false, input: 4, output: 0),
            });
            var producing = FirstMachineId();
            var host = bootstrap.RuntimeHost;

            presenter.PollInspection();
            Assert.That(host.enabled, Is.True, "One machine can produce.");

            Assert.That(commands.SetBuildingActive(producing, false).Succeeded, Is.True);
            presenter.PollInspection();
            Assert.That(host.enabled, Is.False, "No active machine: the host stops creating no-op revisions.");

            Assert.That(commands.SetBuildingActive(producing, true).Succeeded, Is.True);
            presenter.PollInspection();
            Assert.That(host.enabled, Is.True, "Reactivated.");

            Assert.That(commands.AdvanceActiveTime(TickTicks * 50L).Succeeded, Is.True);
            presenter.PollInspection();
            Assert.That(host.enabled, Is.False, "The completed cycle filled the output buffer.");

            Assert.That(commands.RemoveResource(
                producing, BufferRole.Output, new ResourceId("dev.output"), 1L).Succeeded, Is.True);
            presenter.PollInspection();
            Assert.That(host.enabled, Is.True, "Output freed with two inputs left.");
        }

        [UnityTest]
        public IEnumerator SelectedInspectionStillRefreshesLiveAfterEachRevisionChange()
        {
            yield return LoadScene(CreateW001Shape(W001MachineCount));
            var selected = FirstMachineId();
            bootstrap.FactoryView.Select(selected);
            var progress = bootstrap.UiDocument.rootVisualElement.Q<Label>("inspector-progress");
            var state = bootstrap.UiDocument.rootVisualElement.Q<Label>("inspector-state");
            Assert.That(progress.text, Does.Contain("0.00 / 5.00 s"));
            Assert.That(state.text, Does.Contain("State: Producing"));

            Assert.That(commands.AdvanceActiveTime(TickTicks * 10L).Succeeded, Is.True);
            Assert.That(presenter.PollInspection(), Is.True);
            Assert.That(progress.text, Does.Contain("1.00 / 5.00 s"));
            Assert.That(presenter.LastRenderedRevision, Is.EqualTo(commands.CurrentRevision));

            Assert.That(commands.AdvanceActiveTime(TickTicks * 40L).Succeeded, Is.True);
            Assert.That(presenter.PollInspection(), Is.True);
            Assert.That(state.text, Does.Contain("State: Blocked"), "The finished cycle filled the output.");

            // The same refresh happens through the scene's own timed Update, without a direct poll call.
            Assert.That(commands.RemoveResource(
                selected, BufferRole.Output, new ResourceId("dev.output"), 1L).Succeeded, Is.True);
            yield return new WaitForSecondsRealtime(0.15f);
            yield return null;
            Assert.That(state.text, Does.Contain("State: Producing"));
            Assert.That(presenter.LastRenderedRevision, Is.EqualTo(commands.CurrentRevision));
        }

        private int MeasureSelectedRevisionChangePoll()
        {
            var selected = FirstMachineId();
            bootstrap.FactoryView.Select(selected);
            Action poll = () => presenter.PollInspection();
            for (var warmup = 0; warmup < 3; warmup++)
            {
                Assert.That(commands.AdvanceActiveTime(TickTicks).Succeeded, Is.True);
                GcAllocationCounter.Count(poll);
            }

            Assert.That(commands.AdvanceActiveTime(TickTicks).Succeeded, Is.True);
            var allocations = GcAllocationCounter.Count(poll);
            Assert.That(presenter.LastRenderedRevision, Is.EqualTo(commands.CurrentRevision),
                "The selected inspection re-rendered for the new revision.");
            return allocations;
        }

        private static int[] SamplePolls(Action poll, int count)
        {
            var samples = new int[count];
            for (var sample = 0; sample < samples.Length; sample++)
            {
                samples[sample] = GcAllocationCounter.Count(poll);
            }

            return samples;
        }

        private BuildingInstanceId FirstMachineId() => bootstrap.Snapshot.Buildings[0].InstanceId;

        private IEnumerator LoadScene(Func<DefinitionCatalog, IEnumerable<BuildingInstanceState>> createBuildings)
        {
#if !UNITY_EDITOR
            Assert.Ignore("The development scene is loaded directly by the Editor test harness.");
            yield break;
#else
            compositionScope = SceneCompositionTestScope.WithSeededCheckpoint(catalog => new FactoryState(
                new FactoryBounds(new GridPosition(0, 0), 100, 40),
                catalog,
                createBuildings(catalog)));
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
            Assert.That(bootstrap.Session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            presenter = loadedScene.GetRootGameObjects()
                .SelectMany(gameObject => gameObject.GetComponentsInChildren<MachineInspectorPresenter>(true))
                .Single();
            Assert.That(presenter.IsConfigured, Is.True, presenter.Error);
            commands = bootstrap.ProductionCommands;
#endif
        }

        private IEnumerator UnloadScene()
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
            bootstrap = null;
            presenter = null;
            commands = null;
            compositionScope?.Dispose();
            compositionScope = null;
        }

        /// <summary>W001 shape: half producing, a quarter output-blocked, the rest inactive; one machine produces.</summary>
        private static Func<DefinitionCatalog, IEnumerable<BuildingInstanceState>> CreateW001Shape(int count) =>
            catalog => Enumerable.Range(0, count).Select(index =>
            {
                var producing = index < Math.Max(1, count / 2);
                var outputBlocked = !producing && index < count * 3 / 4;
                return CreateMachine(
                    catalog,
                    index + 1,
                    new GridPosition((index % 50) * 2, (index / 50) * 2),
                    active: producing || outputBlocked,
                    input: 4,
                    output: outputBlocked ? 1 : 0);
            });

        private static BuildingInstanceState CreateMachine(
            DefinitionCatalog catalog,
            int id,
            GridPosition anchor,
            bool active,
            long input,
            long output)
        {
            var definition = catalog.Resolve(new BuildingDefinitionId("dev.machine.basic"));
            var recipe = catalog.Resolve(new RecipeId("dev.recipe.basic"));
            return new BuildingInstanceState(
                new BuildingInstanceId(new Guid(id, 0, 0, new byte[8])),
                definition,
                anchor,
                0,
                active,
                new ResourceBufferState(definition.InputBuffer, Quantity("dev.input", input)),
                new ResourceBufferState(definition.OutputBuffer, Quantity("dev.output", output)),
                new ProductionState(recipe, 0));
        }

        private static KeyValuePair<ResourceId, long>[] Quantity(string resourceId, long quantity) =>
            new[] { new KeyValuePair<ResourceId, long>(new ResourceId(resourceId), quantity) };
    }
}
