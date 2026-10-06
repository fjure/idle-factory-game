using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode
{
    public sealed class ViewRecreationTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in objects)
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }

            objects.Clear();
        }

        [UnityTest]
        public IEnumerator RebuildIsIdempotentAndRecreationReResolvesOrClearsSelection()
        {
            var catalog = CreateDefinitions();
            var building = CreateBuilding(catalog, 1, new GridPosition(1, 1), 25000000L);
            var session = CreateRunningSession(new FactoryState(
                new FactoryBounds(new GridPosition(0, 0), 8, 8),
                catalog,
                new[] { building }));
            var commands = new PlacementCommands(session, new FixedIdSource());
            var sprites = CreateSpriteCatalog();
            var mapper = new GridWorldMapper(1f, Vector2.zero);
            var setup = CreateRegistry(commands, catalog, sprites, mapper, "idempotent-view");
            setup.Registry.CurrentView.Select(building.Id);
            yield return null;

            var originalState = CaptureSemanticState(session);
            var originalRevision = commands.GetFactorySnapshot().Revision;
            var originalView = setup.Registry.CurrentView;
            var originalFingerprint = RenderFingerprint(originalView);
            Assert.That(originalView.SelectedInstanceId, Is.EqualTo(building.Id));

            for (var refresh = 0; refresh < 4; refresh++)
            {
                Assert.That(setup.Registry.Rebuild(), Is.SameAs(originalView));
                yield return null;
            }

            var rebuiltState = CaptureSemanticState(session);
            Assert.That(rebuiltState.Equals(originalState), Is.True,
                "A full view refresh must not mutate authoritative factory state.");
            Assert.That(rebuiltState.Checksum, Is.EqualTo(originalState.Checksum));
            Assert.That(commands.GetFactorySnapshot().Revision, Is.EqualTo(originalRevision));
            Assert.That(originalView.SelectedInstanceId, Is.EqualTo(building.Id));
            Assert.That(originalView.RenderedBuildingCount, Is.EqualTo(1));
            Assert.That(RenderFingerprint(originalView), Is.EqualTo(originalFingerprint));

            var replacedState = new FactoryState(
                new FactoryBounds(new GridPosition(0, 0), 8, 8),
                catalog,
                Array.Empty<BuildingInstanceState>());
            ReplaceState(session, replacedState);
            var staleView = setup.Registry.CurrentView;
            var emptyView = setup.Registry.Recreate();
            Assert.That(emptyView, Is.Not.SameAs(staleView));
            Assert.That(setup.Registry.CurrentView, Is.SameAs(emptyView));
            Assert.That(emptyView.SelectedInstanceId, Is.Null,
                "Recreation must clear a selected identity that no longer resolves in the current snapshot.");
            Assert.That(emptyView.RenderedBuildingCount, Is.Zero);
            yield return null;
            Assert.That(staleView == null, Is.True, "The previous Unity view must be destroyed after replacement.");
        }

        [UnityTest]
        public IEnumerator VariedRefreshAndDestroyRecreateSchedulesPreserveSemanticSimulationState()
        {
            var catalog = CreateDefinitions();
            var building = CreateBuilding(catalog, 7, new GridPosition(2, 2), 15000000L);
            var initialState = new FactoryState(
                new FactoryBounds(new GridPosition(0, 0), 8, 8),
                catalog,
                new[] { building });
            var frequentSession = CreateRunningSession(initialState);
            var sparseSession = CreateRunningSession(initialState);
            var frequentProduction = new ProductionCommands(frequentSession);
            var sparseProduction = new ProductionCommands(sparseSession);
            var frequentPlacement = new PlacementCommands(frequentSession, new FixedIdSource());
            var sparsePlacement = new PlacementCommands(sparseSession, new FixedIdSource());
            var sprites = CreateSpriteCatalog();
            var mapper = new GridWorldMapper(1f, Vector2.zero);
            var frequent = CreateRegistry(frequentPlacement, catalog, sprites, mapper, "frequent-view");
            var sparse = CreateRegistry(sparsePlacement, catalog, sprites, mapper, "sparse-view");
            frequent.Registry.CurrentView.Select(building.Id);
            sparse.Registry.CurrentView.Select(building.Id);

            var elapsedIntervals = new[] { 5000000L, 10000000L, 25000000L, 15000000L, 45000000L };
            var refreshCounts = new[] { 1, 4, 0, 3, 2 };
            GameObject replacedFrequentView = null;
            GameObject replacedSparseView = null;

            for (var interval = 0; interval < elapsedIntervals.Length; interval++)
            {
                for (var refresh = 0; refresh < refreshCounts[interval]; refresh++)
                {
                    frequent.Registry.Rebuild();
                    yield return null;
                }

                Assert.That(frequentProduction.AdvanceSimulation(elapsedIntervals[interval]).Succeeded, Is.True);
                Assert.That(sparseProduction.AdvanceSimulation(elapsedIntervals[interval]).Succeeded, Is.True);

                if (interval == 1 || interval == 3)
                {
                var old = frequent.Registry.CurrentView;
                var oldObject = old.gameObject;
                frequent.Registry.Recreate();
                if (interval == 1)
                {
                    replacedFrequentView = oldObject;
                    }

                    yield return null;
                    Assert.That(old == null, Is.True);
                }

                if (interval == 2)
                {
                    sparse.Registry.Rebuild();
                    yield return null;
                }
            }

            var beforeSparseRecreation = sparse.Registry.CurrentView;
            var oldSparseObject = beforeSparseRecreation.gameObject;
            sparse.Registry.Recreate();
            replacedSparseView = oldSparseObject;
            yield return null;

            Assert.That(replacedFrequentView == null, Is.True);
            Assert.That(replacedSparseView == null, Is.True);
            Assert.That(frequent.Registry.CurrentView.SelectedInstanceId, Is.EqualTo(building.Id));
            Assert.That(sparse.Registry.CurrentView.SelectedInstanceId, Is.EqualTo(building.Id));
            Assert.That(frequent.Registry.CurrentView.RenderedBuildingCount, Is.EqualTo(1));
            Assert.That(sparse.Registry.CurrentView.RenderedBuildingCount, Is.EqualTo(1));

            var frequentState = CaptureSemanticState(frequentSession);
            var sparseState = CaptureSemanticState(sparseSession);
            Assert.That(frequentState.Equals(sparseState), Is.True,
                "Changing presentation refresh/recreation cadence must preserve complete semantic state.");
            Assert.That(frequentState.Checksum, Is.EqualTo(sparseState.Checksum));
            Assert.That(frequentPlacement.GetFactorySnapshot().Revision,
                Is.EqualTo(sparsePlacement.GetFactorySnapshot().Revision));

            frequent.Registry.Rebuild();
            sparse.Registry.Rebuild();
            yield return null;
            Assert.That(RenderFingerprint(frequent.Registry.CurrentView),
                Is.EqualTo(RenderFingerprint(sparse.Registry.CurrentView)));
            Assert.That(CaptureSemanticState(frequentSession).Equals(frequentState), Is.True);
            Assert.That(CaptureSemanticState(sparseSession).Equals(sparseState), Is.True);
        }

        private RegistrySetup CreateRegistry(
            PlacementCommands commands,
            DefinitionCatalog catalog,
            PresentationSpriteCatalog sprites,
            GridWorldMapper mapper,
            string name)
        {
            var parent = NewObject(name);
            FactoryView CreateView()
            {
                var viewObject = NewObject(name + "-instance");
                viewObject.transform.SetParent(parent.transform, false);
                return viewObject.AddComponent<FactoryView>();
            }

            void ConfigureView(FactoryView view) => view.Configure(commands, catalog, sprites, mapper);
            void DestroyView(FactoryView view)
            {
                if (view != null)
                {
                    Object.Destroy(view.gameObject);
                }
            }

            var initialView = CreateView();
            ConfigureView(initialView);
            var registry = new FactoryViewRegistry(
                CreateView,
                ConfigureView,
                DestroyView,
                view => view.SelectedInstanceId,
                (view, selectedId) => view.Select(selectedId),
                initialView);
            return new RegistrySetup(registry);
        }

        private GameObject NewObject(string name)
        {
            var gameObject = new GameObject(name);
            objects.Add(gameObject);
            return gameObject;
        }

        private static DefinitionCatalog CreateDefinitions()
        {
            var input = new ResourceId("dev.input");
            var output = new ResourceId("dev.output");
            var recipeId = new RecipeId("dev.recipe.basic");
            var recipe = new RecipeDefinition(
                recipeId,
                new[] { new ResourceAmountDefinition(input, 2) },
                new[] { new ResourceAmountDefinition(output, 1) },
                50000000L,
                true);
            var machine = new BuildingDefinition(
                new BuildingDefinitionId("dev.machine.basic"),
                new Footprint(2, 2),
                new[] { 0 },
                recipeId,
                new BufferDefinition(BufferRole.Input, Quantities(input, 4)),
                new BufferDefinition(BufferRole.Output, Quantities(output, 1)),
                PresentationSpriteCatalog.D001Normal,
                true);
            return new DefinitionCatalog(
                new[]
                {
                    new ResourceDefinition(input, "Input", PresentationSpriteCatalog.ResourceInput, true),
                    new ResourceDefinition(output, "Output", PresentationSpriteCatalog.ResourceOutput, true),
                },
                new[] { recipe },
                new[] { machine },
                new[]
                {
                    new OfflinePolicyDefinition(
                        new OfflinePolicyRevision(1),
                        DefinitionCatalog.SupportedEfficiencyNumerator,
                        DefinitionCatalog.SupportedEfficiencyDenominator,
                        DefinitionCatalog.SupportedMaxCreditedTicks),
                });
        }

        private static BuildingInstanceState CreateBuilding(
            DefinitionCatalog catalog,
            int id,
            GridPosition anchor,
            long progressTicks)
        {
            var definition = catalog.Resolve(new BuildingDefinitionId("dev.machine.basic"));
            var recipe = catalog.Resolve(new RecipeId("dev.recipe.basic"));
            return new BuildingInstanceState(
                Id(id),
                definition,
                anchor,
                0,
                true,
                new ResourceBufferState(definition.InputBuffer, Quantities(new ResourceId("dev.input"), 4)),
                new ResourceBufferState(definition.OutputBuffer, Quantities(new ResourceId("dev.output"), 0)),
                new ProductionState(recipe, progressTicks));
        }

        private static KeyValuePair<ResourceId, long>[] Quantities(ResourceId id, long quantity) =>
            new[] { new KeyValuePair<ResourceId, long>(id, quantity) };

        private static BuildingInstanceId Id(int value) =>
            new BuildingInstanceId(new Guid(value, 0, 0, new byte[8]));

        private static SemanticFactorySnapshot CaptureSemanticState(FactorySession session)
        {
            var captureState = typeof(FactorySession).GetMethod(
                "CreateStateCopy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(captureState, Is.Not.Null);
            return SemanticFactorySnapshot.Capture((FactoryState)captureState.Invoke(session, null));
        }

        private static FactorySession CreateRunningSession(FactoryState state)
        {
            var session = new FactorySession(state, state.Catalog, new TestClock(), new TestStore());
            Transition(session, LifecycleState.Loading);
            Transition(session, LifecycleState.Running);
            return session;
        }

        private static void Transition(FactorySession session, LifecycleState state)
        {
            var transition = typeof(FactorySession).GetMethod(
                "TransitionTo",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var result = (CommandResult)transition.Invoke(session, new object[] { state });
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static void ReplaceState(FactorySession session, FactoryState state)
        {
            var replace = typeof(FactorySession).GetMethod(
                "ReplaceState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var result = (CommandResult)replace.Invoke(session, new object[] { state });
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static string RenderFingerprint(FactoryView view)
        {
            return string.Join("|", view.GetComponentsInChildren<SpriteRenderer>(true)
                .Select(renderer => string.Join(":",
                    renderer.sprite.name,
                    renderer.sortingOrder.ToString(CultureInfo.InvariantCulture),
                    renderer.transform.position.x.ToString("R", CultureInfo.InvariantCulture),
                    renderer.transform.position.y.ToString("R", CultureInfo.InvariantCulture),
                    renderer.transform.eulerAngles.z.ToString("R", CultureInfo.InvariantCulture)))
                .OrderBy(value => value, StringComparer.Ordinal));
        }

        private static PresentationSpriteCatalog CreateSpriteCatalog()
        {
            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(new List<PresentationSpriteEntry>
            {
                Entry(PresentationSpriteCatalog.GridTile, "f001-grid-tile-1x1-v01.png"),
                Entry(PresentationSpriteCatalog.BoundaryEdge, "f001-grid-boundary-edge-v01.png"),
                Entry(PresentationSpriteCatalog.BoundaryCorner, "f001-grid-boundary-corner-v01.png"),
                Entry(PresentationSpriteCatalog.D001Normal, "f001-machine-d001-normal-v01.png"),
                Entry(PresentationSpriteCatalog.D001Selected, "f001-machine-d001-selected-v01.png"),
                Entry(PresentationSpriteCatalog.AlternateNormal, "f001-machine-alt-3x1-normal-v01.png"),
                Entry(PresentationSpriteCatalog.AlternateSelected, "f001-machine-alt-3x1-selected-v01.png"),
                Entry(PresentationSpriteCatalog.D001GhostValid, "f001-ghost-d001-valid-v01.png"),
                Entry(PresentationSpriteCatalog.D001GhostInvalid, "f001-ghost-d001-invalid-v01.png"),
                Entry(PresentationSpriteCatalog.AlternateGhostValid, "f001-ghost-alt-3x1-valid-v01.png"),
                Entry(PresentationSpriteCatalog.AlternateGhostInvalid, "f001-ghost-alt-3x1-invalid-v01.png"),
                Entry(PresentationSpriteCatalog.ResourceInput, "f001-resource-dev-input-v01.png"),
                Entry(PresentationSpriteCatalog.ResourceOutput, "f001-resource-dev-output-v01.png"),
                Entry(PresentationSpriteCatalog.BadgeNormal, "f001-badge-normal-v01.png"),
                Entry(PresentationSpriteCatalog.BadgeSelected, "f001-badge-selected-v01.png"),
                Entry(PresentationSpriteCatalog.BadgePlacementValid, "f001-badge-placement-valid-v01.png"),
                Entry(PresentationSpriteCatalog.BadgePlacementInvalid, "f001-badge-placement-invalid-v01.png"),
                Entry(PresentationSpriteCatalog.BadgeProducing, "f001-badge-producing-v01.png"),
                Entry(PresentationSpriteCatalog.BadgeMissingInput, "f001-badge-missing-input-v01.png"),
                Entry(PresentationSpriteCatalog.BadgeFullOutput, "f001-badge-full-output-v01.png"),
                Entry(PresentationSpriteCatalog.BadgeInactive, "f001-badge-inactive-v01.png"),
            });
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            return catalog;
        }

        private static PresentationSpriteEntry Entry(string key, string fileName)
        {
            const string root = "Assets/IdleFactory/Art/Development/F001/Imported/";
            var editorType = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            Assert.That(editorType, Is.Not.Null);
            var loadMethod = editorType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "LoadAssetAtPath" && method.IsGenericMethodDefinition);
            var sprite = (Sprite)loadMethod.MakeGenericMethod(typeof(Sprite)).Invoke(null, new object[] { root + fileName });
            Assert.That(sprite, Is.Not.Null, root + fileName);
            return new PresentationSpriteEntry(key, sprite);
        }

        private readonly struct RegistrySetup
        {
            public RegistrySetup(FactoryViewRegistry registry)
            {
                Registry = registry;
            }

            public FactoryViewRegistry Registry { get; }
        }

        private sealed class FixedIdSource : IBuildingInstanceIdSource
        {
            public CommandResult<BuildingInstanceId> Next() => CommandResult<BuildingInstanceId>.Success(Id(99));
        }

        private sealed class TestClock : IUtcClock
        {
            public DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
        }

        private sealed class TestStore : ICheckpointStore
        {
            public CheckpointReadResult ReadActive() => CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "none");
            public CheckpointReadResult ReadPrevious() => CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "none");
            public CheckpointReadResult Read(CheckpointId checkpointId) => CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "none");
            public CommandResult<CheckpointId> Promote(Checkpoint checkpoint) => CommandResult<CheckpointId>.Success(checkpoint.Id);
            public CommandResult<CheckpointId> RecoverPrevious(Checkpoint recoveryCandidate) => CommandResult<CheckpointId>.Success(recoveryCandidate.Id);
        }
    }
}
