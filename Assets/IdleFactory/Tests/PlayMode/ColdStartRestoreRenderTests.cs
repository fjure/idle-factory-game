using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T061: a cold start that restores an existing checkpoint must render the restored buildings before the
    /// first frame the player can act on, with no tap required. Regression coverage for the bug where
    /// <see cref="CoreGameFoundationBootstrap"/> refreshed <c>FactoryView</c> after cold-start reconciliation
    /// only on the no-checkpoint fallback path, leaving a restored factory invisible until the first world tap.
    ///
    /// Unlike <see cref="CoreGameFoundationSceneTests"/> (which always seeds an in-memory
    /// <see cref="IsolatedCheckpointStore"/> via <see cref="CoreGameFoundationBootstrap.OverrideCompositionDependenciesForTests"/>),
    /// this fixture writes the checkpoint through a real, on-disk <see cref="JsonCheckpointStore"/> pointed at an
    /// isolated temp directory, then composes the scene against a fresh store instance over the same directory,
    /// exactly like relaunching the player process against a checkpoint the previous run wrote.
    ///
    /// This derives from <see cref="InputTestFixture"/> (as <see cref="CoreGameFoundationSceneTests"/> does) purely
    /// to inherit its Input System save/restore-around-the-test behavior, even though this fixture adds no test
    /// devices of its own: composing the authored scene still exercises the real Input System (the scene's own
    /// action maps and devices), and running that additive scene load/unload without the fixture's usual
    /// save/restore was found to leave state that made an unrelated, later-running touch-input test
    /// (<c>CoreGameFoundationSceneTests.AuthoredSceneRunsPlacementInputAndViewRecreationWithoutChangingWorldOwnership</c>)
    /// fail intermittently (a stale selection after tapping an empty cell).
    /// </summary>
    public sealed class ColdStartRestoreRenderTests : InputTestFixture
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private static readonly DateTimeOffset DepartureUtc = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        private static readonly GridPosition FirstAnchor = new GridPosition(2, 2);
        private static readonly GridPosition SecondAnchor = new GridPosition(9, 9);
        private static readonly BuildingInstanceId FirstBuildingId = new BuildingInstanceId(new Guid(1, 0, 0, new byte[8]));
        private static readonly BuildingInstanceId SecondBuildingId = new BuildingInstanceId(new Guid(2, 0, 0, new byte[8]));

        private Scene loadedScene;
        private string root;
        private IDisposable compositionOverride;

        public override void Setup()
        {
            base.Setup();
            root = Path.Combine(Application.temporaryCachePath, "t061-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        public override void TearDown()
        {
            base.TearDown();
            if (root != null && Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
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
            compositionOverride?.Dispose();
            compositionOverride = null;
        }

        [UnityTest]
        public IEnumerator ColdStartWithRealPersistedCheckpointRendersBuildingsBeforeFirstInput()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The development scene is loaded directly by the Editor test harness.");
            yield break;
#else
            compositionOverride = CoreGameFoundationBootstrap.OverrideCompositionDependenciesForTests(
                catalog =>
                {
                    // Write a real checkpoint to disk through the production JsonCheckpointStore, matching the
                    // exact catalog the bootstrap just built from its authored development content.
                    var seedingStore = new JsonCheckpointStore(root, catalog);
                    var factory = new FactoryState(
                        new FactoryBounds(new GridPosition(0, 0), 16, 16),
                        catalog,
                        new[]
                        {
                            CreateBuilding(catalog, FirstBuildingId, FirstAnchor),
                            CreateBuilding(catalog, SecondBuildingId, SecondAnchor),
                        });
                    var checkpoint = new Checkpoint(
                        new CheckpointId(Guid.NewGuid()),
                        1L,
                        DepartureUtc,
                        new OfflinePolicyRevision(DefinitionCatalog.SupportedOfflinePolicyRevision),
                        null,
                        factory);
                    var promoted = seedingStore.Promote(checkpoint);
                    Assert.That(promoted.Succeeded, Is.True, promoted.Message);

                    // A genuine cold start re-reads through a fresh store instance over the same directory,
                    // exactly like relaunching the player process rather than reusing the writer's in-memory state.
                    return new JsonCheckpointStore(root, catalog);
                },
                () => DepartureUtc,
                () => 0d);

            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
            {
                yield return null;
            }
            yield return null;

            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            var scene = loadedScene;
            var rootObjects = scene.GetRootGameObjects();
            var bootstrap = rootObjects
                .SelectMany(gameObject => gameObject.GetComponentsInChildren<CoreGameFoundationBootstrap>(true))
                .Single();
            var lifecycle = rootObjects
                .SelectMany(gameObject => gameObject.GetComponentsInChildren<ApplicationLifecycleAdapter>(true))
                .Single();

            // No world tap, click or navigation intent has occurred at this point: bootstrap.Initialize() ran
            // synchronously in Awake() during scene load. T061 requires the restored factory to already be
            // rendered here, before any input the player could produce.
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            Assert.That(bootstrap.Session, Is.Not.Null);
            Assert.That(bootstrap.Session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(lifecycle.LastResult, Is.Not.Null);
            Assert.That(lifecycle.LastResult.Succeeded, Is.True, lifecycle.LastResult.Message);
            Assert.That(lifecycle.LastResult.Value.OfflineSummary, Is.Not.Null);
            Assert.That(lifecycle.LastResult.Value.OfflineSummary.Outcome, Is.Not.EqualTo(OfflineReturnOutcome.NoCheckpoint),
                "The seeded real checkpoint must be the one reconciled at cold start, not the no-checkpoint fallback.");
            Assert.That(bootstrap.Snapshot, Is.Not.Null);
            Assert.That(bootstrap.Snapshot.Buildings, Has.Count.EqualTo(2),
                "The restored session must contain both buildings persisted in the on-disk checkpoint.");

            var factoryView = bootstrap.FactoryView;
            Assert.That(factoryView.RenderedRevision, Is.EqualTo(bootstrap.Snapshot.Revision),
                "The view must already be rendered at the session's current (restored) revision, not the pre-reconciliation empty state.");
            Assert.That(factoryView.RenderedBuildingCount, Is.EqualTo(2),
                "T061: restored buildings must render before the first frame the player can act on, without any tap.");
            Assert.That(factoryView.GetMachineRenderer(FirstBuildingId), Is.Not.Null,
                "The first restored building must have a rendered machine sprite without any input.");
            Assert.That(factoryView.GetMachineRenderer(SecondBuildingId), Is.Not.Null,
                "The second restored building must have a rendered machine sprite without any input.");

            // Selection resolution against the already-rendered, restored state: this is the FIRST player
            // interaction in the test, and it must resolve against buildings the view rendered before any input.
            SelectionResolution capturedResolution = null;
            factoryView.SelectionChanged += resolution => capturedResolution = resolution;
            factoryView.SelectAt(FirstAnchor);

            Assert.That(factoryView.SelectedInstanceId, Is.EqualTo(FirstBuildingId));
            Assert.That(capturedResolution, Is.Not.Null);
            Assert.That(capturedResolution.Kind, Is.EqualTo(SelectionResolutionKind.Selected),
                "Selecting a restored, already-rendered building must resolve it, not report it missing.");
            Assert.That(factoryView.RenderedBuildingCount, Is.EqualTo(2),
                "Selection must not change which buildings are rendered.");
#endif
        }

        private static BuildingInstanceState CreateBuilding(DefinitionCatalog catalog, BuildingInstanceId id, GridPosition anchor)
        {
            var definition = catalog.Resolve(new BuildingDefinitionId("dev.machine.basic"));
            var recipe = catalog.Resolve(new RecipeId("dev.recipe.basic"));
            return new BuildingInstanceState(
                id,
                definition,
                anchor,
                0,
                true,
                new ResourceBufferState(definition.InputBuffer, Quantities(new ResourceId("dev.input"), 4)),
                new ResourceBufferState(definition.OutputBuffer, Quantities(new ResourceId("dev.output"), 0)),
                new ProductionState(recipe, 0));
        }

        private static KeyValuePair<ResourceId, long>[] Quantities(ResourceId id, long quantity) =>
            new[] { new KeyValuePair<ResourceId, long>(id, quantity) };
    }
}
