using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Input;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode
{
    public sealed class NavigationPlacementTests
    {
        private const long FiveSecondCycleTicks = 50000000L;

        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<Object> transientObjects = new List<Object>();

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

            foreach (var item in transientObjects)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            objects.Clear();
            transientObjects.Clear();
        }

        [Test]
        public void ExplicitCatalogRequiresEveryExactApprovedSpriteAndRejectsWrongMapping()
        {
            var catalog = CreateSpriteCatalog();

            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            Assert.That(catalog.Count, Is.EqualTo(21));
            Assert.That(catalog.Get(F001SpriteCatalog.D001Normal).name, Is.EqualTo("f001-machine-d001-normal-v01"));

            var wrong = new F001SpriteCatalog();
            var mappings = LoadMappings();
            mappings[3] = new F001SpriteEntry(F001SpriteCatalog.D001Normal, mappings[4].Sprite);
            wrong.Configure(mappings);

            Assert.That(wrong.IsValid, Is.False);
            Assert.That(wrong.Error, Does.Contain("wrong approved sprite"));
            Assert.That(() => wrong.Get(F001SpriteCatalog.D001Normal), Throws.TypeOf<InvalidOperationException>());
        }

        [UnityTest]
        public IEnumerator PlacementFlowConfirmsExactlyOnceAndInvalidOrCancelNeverConsumesAnId()
        {
            var catalog = CreateDefinitions();
            var state = new FactoryState(new FactoryBounds(new GridPosition(0, 0), 8, 8), catalog, Array.Empty<BuildingInstanceState>());
            var ids = new CountingIdSource();
            var session = CreateRunningSession(state);
            var commands = new PlacementCommands(session, ids);
            var setup = CreatePresentation(commands, catalog);
            var d001 = new BuildingDefinitionId("dev.machine.basic");

            Assert.That(setup.Controller.StartPlacement(d001, new GridPosition(1, 1)), Is.True);
            Assert.That(setup.Preview.IsValid, Is.True);
            Assert.That(setup.Preview.Cells.Count, Is.EqualTo(4));
            Assert.That(setup.Hud.ConfirmButton.enabledSelf, Is.True);
            setup.Controller.Confirm();

            Assert.That(ids.Calls, Is.EqualTo(1));
            Assert.That(commands.GetFactorySnapshot().Buildings.Count, Is.EqualTo(1));
            Assert.That(commands.GetFactorySnapshot().Occupancy.Count, Is.EqualTo(4));
            Assert.That(setup.Controller.Mode, Is.EqualTo(PlacementInteractionMode.Browse));
            Assert.That(setup.View.SelectedInstanceId, Is.EqualTo(commands.GetFactorySnapshot().Buildings.Single().InstanceId));

            Assert.That(setup.Controller.StartPlacement(d001, new GridPosition(1, 1)), Is.True);
            Assert.That(setup.Preview.IsValid, Is.False);
            Assert.That(setup.Preview.Feedback, Does.Contain("overlaps"));
            Assert.That(setup.Hud.ConfirmButton.enabledSelf, Is.False);
            setup.Controller.Confirm();
            Assert.That(ids.Calls, Is.EqualTo(1));
            Assert.That(commands.GetFactorySnapshot().Buildings.Count, Is.EqualTo(1));

            setup.Controller.HandleNavigationIntent(new NavigationIntent(
                NavigationIntentKind.WorldTap,
                new Vector2(7.2f, 7.2f),
                Vector2.zero,
                0f));
            Assert.That(setup.Preview.IsValid, Is.False);
            Assert.That(setup.Preview.Cells.Count, Is.EqualTo(4), "The whole rejected footprint remains visible.");
            Assert.That(setup.Preview.Feedback, Does.Contain("Complete footprint"));
            setup.Controller.Cancel();
            Assert.That(ids.Calls, Is.EqualTo(1));
            Assert.That(commands.GetFactorySnapshot().Buildings.Count, Is.EqualTo(1));

            yield return null;
            Assert.That(setup.Preview.Renderers, Is.Empty);
        }

        [Test]
        public void SemanticPanPinchCancelNeverMovesOrConfirmsAndBrowseTapSelectsOrClears()
        {
            var catalog = CreateDefinitions();
            var building = CreateBuilding(catalog, 1, new GridPosition(0, 0), true, 2, 0);
            var state = new FactoryState(new FactoryBounds(new GridPosition(0, 0), 8, 8), catalog, new[] { building });
            var ids = new CountingIdSource();
            var commands = new PlacementCommands(CreateRunningSession(state), ids);
            var setup = CreatePresentation(commands, catalog);

            setup.Controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.WorldTap, new Vector2(0.5f, 0.5f), Vector2.zero, 0f));
            Assert.That(setup.View.SelectedInstanceId, Is.EqualTo(building.Id));
            setup.Controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.WorldTap, new Vector2(6.5f, 6.5f), Vector2.zero, 0f));
            Assert.That(setup.View.SelectedInstanceId, Is.Null);

            setup.Controller.StartPlacement(new BuildingDefinitionId("dev.machine.basic"), new GridPosition(3, 3));
            var anchor = setup.Controller.PreviewAnchor;
            setup.Controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.Pan, Vector2.zero, Vector2.one, 0f));
            setup.Controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.Zoom, Vector2.zero, Vector2.zero, 1f));
            setup.Controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.CancelWorldGesture, Vector2.zero, Vector2.zero, 0f));

            Assert.That(setup.Controller.PreviewAnchor, Is.EqualTo(anchor));
            Assert.That(ids.Calls, Is.Zero, "No world gesture is a hidden placement confirmation.");
        }

        [UnityTest]
        public IEnumerator FactoryViewUsesStableIdsBadgesSortingAndCleansStaleSelectionAndObjects()
        {
            var catalog = CreateDefinitions();
            var buildings = new[]
            {
                CreateBuilding(catalog, 1, new GridPosition(0, 0), true, 2, 0),
                CreateBuilding(catalog, 2, new GridPosition(3, 0), true, 0, 0),
                CreateBuilding(catalog, 3, new GridPosition(0, 3), true, 2, 1),
                CreateBuilding(catalog, 4, new GridPosition(3, 3), false, 2, 0),
            };
            var state = new FactoryState(new FactoryBounds(new GridPosition(0, 0), 8, 8), catalog, buildings);
            var session = CreateRunningSession(state);
            var commands = new PlacementCommands(session, new CountingIdSource());
            var setup = CreatePresentation(commands, catalog);

            Assert.That(setup.View.RenderedBuildingCount, Is.EqualTo(4));
            Assert.That(setup.View.GetMachineRenderer(buildings[0].Id).sortingOrder, Is.EqualTo(FactoryView.MachineSortingOrder));
            AssertBadge(setup.View, buildings[0].Id, F001SpriteCatalog.BadgeProducing);
            AssertBadge(setup.View, buildings[1].Id, F001SpriteCatalog.BadgeMissingInput);
            AssertBadge(setup.View, buildings[2].Id, F001SpriteCatalog.BadgeFullOutput);
            AssertBadge(setup.View, buildings[3].Id, F001SpriteCatalog.BadgeInactive);

            setup.View.Select(buildings[0].Id);
            Assert.That(setup.View.GetMachineRenderer(buildings[0].Id).sprite.name, Is.EqualTo("f001-machine-d001-selected-v01"));
            AssertBadge(setup.View, buildings[0].Id, F001SpriteCatalog.BadgeSelected);
            var revisionBefore = commands.GetFactorySnapshot().Revision;

            ReplaceState(session, new FactoryState(state.Bounds, catalog, buildings.Skip(1)));
            setup.View.Refresh();
            Assert.That(setup.View.SelectedInstanceId, Is.Null);
            Assert.That(setup.Hud.SelectionText, Does.Contain("no longer available"));
            Assert.That(setup.View.RenderedBuildingCount, Is.EqualTo(3));
            Assert.That(commands.GetFactorySnapshot().Revision, Is.EqualTo(revisionBefore + 1));

            yield return null;
            Assert.That(setup.View.GetComponentsInChildren<SpriteRenderer>(true)
                .Count(renderer => renderer.sortingOrder == FactoryView.MachineSortingOrder), Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator WorldBadgesAndSelectionSummaryFollowSimulationStateChangesWithoutReselection()
        {
            // T062 / T057-F1: a machine that finishes its cycle and becomes blocked must update its world badge and
            // the Browse summary on the next frame, for every visible machine, without a new selection or rebuild.
            var catalog = CreateDefinitions();
            var selected = CreateBuilding(catalog, 1, new GridPosition(0, 0), true, 2, 0);
            var other = CreateBuilding(catalog, 2, new GridPosition(3, 0), true, 4, 0);
            var state = new FactoryState(new FactoryBounds(new GridPosition(0, 0), 8, 8), catalog, new[] { selected, other });
            var session = CreateRunningSession(state);
            var commands = new PlacementCommands(session, new CountingIdSource());
            var production = new ProductionCommands(session);
            var setup = CreatePresentation(commands, catalog);
            setup.View.Select(selected.Id);
            yield return null;

            AssertBadge(setup.View, selected.Id, F001SpriteCatalog.BadgeProducing);
            AssertBadge(setup.View, other.Id, F001SpriteCatalog.BadgeProducing);
            Assert.That(setup.Hud.SelectionText, Does.Contain("| Active |").And.Contain("Blockers 0"));
            var machineBefore = setup.View.GetMachineRenderer(selected.Id);
            var renderedRevision = setup.View.RenderedRevision;

            var advanced = production.AdvanceSimulation(FiveSecondCycleTicks);
            Assert.That(advanced.Succeeded, Is.True, advanced.Message);
            yield return null;

            // The selected machine consumed its only two inputs and filled its one output slot.
            AssertBadge(setup.View, selected.Id, F001SpriteCatalog.BadgeMissingInput);
            AssertNoBadge(setup.View, selected.Id, F001SpriteCatalog.BadgeProducing);
            AssertBadge(setup.View, selected.Id, F001SpriteCatalog.BadgeSelected);
            // The unselected machine still has input but its output is full.
            AssertBadge(setup.View, other.Id, F001SpriteCatalog.BadgeFullOutput);
            AssertNoBadge(setup.View, other.Id, F001SpriteCatalog.BadgeProducing);
            Assert.That(setup.Hud.SelectionText, Does.Contain("| Active |").And.Contain("Blockers 2"));
            Assert.That(setup.View.SelectedInstanceId, Is.EqualTo(selected.Id));
            Assert.That(setup.View.GetMachineRenderer(selected.Id), Is.SameAs(machineBefore), "The view was not rebuilt.");
            Assert.That(setup.View.RenderedRevision, Is.EqualTo(renderedRevision), "The view was not rebuilt.");

            Assert.That(production.SetBuildingActive(other.Id, false).Succeeded, Is.True);
            yield return null;
            AssertBadge(setup.View, other.Id, F001SpriteCatalog.BadgeInactive);
            Assert.That(setup.Hud.SelectionText, Does.Contain("Blockers 2"), "Another machine's change keeps the summary.");

            Assert.That(production.SetBuildingActive(selected.Id, false).Succeeded, Is.True);
            yield return null;
            AssertBadge(setup.View, selected.Id, F001SpriteCatalog.BadgeInactive);
            Assert.That(setup.Hud.SelectionText, Does.Contain("| Inactive |").And.Contain("Blockers 3"));

            Assert.That(production.SetBuildingActive(selected.Id, true).Succeeded, Is.True);
            Assert.That(production.AddResource(selected.Id, BufferRole.Input, new ResourceId("dev.input"), 2).Succeeded, Is.True);
            Assert.That(production.RemoveResource(selected.Id, BufferRole.Output, new ResourceId("dev.output"), 1).Succeeded, Is.True);
            yield return null;
            AssertBadge(setup.View, selected.Id, F001SpriteCatalog.BadgeProducing);
            Assert.That(setup.Hud.SelectionText, Does.Contain("| Active |").And.Contain("Blockers 0"));
            Assert.That(setup.View.RenderedRevision, Is.EqualTo(renderedRevision), "No step rebuilt the view.");
        }

        [UnityTest]
        public IEnumerator PresentedStateSyncSkipsUnchangedW001ScaleFactoryWithoutAllocating()
        {
            // W001 workload shape (T062): 1000 D001-footprint machines, 500 producing, 250 output-blocked and
            // 250 inactive. Steady production changes the revision every tick but no presented state, so the view
            // sync must neither change a badge nor allocate managed memory.
            const long inputCapacity = 9216L;
            const long outputCapacity = 4608L;
            var catalog = CreateDefinitions(inputCapacity, outputCapacity);
            var buildings = new BuildingInstanceState[1000];
            for (var index = 0; index < buildings.Length; index++)
            {
                var active = index < 750;
                var outputBlocked = index >= 500 && index < 750;
                buildings[index] = CreateBuilding(
                    catalog,
                    index + 1,
                    new GridPosition((index % 50) * 2, (index / 50) * 2),
                    active,
                    inputCapacity,
                    outputBlocked ? outputCapacity : 0);
            }

            var state = new FactoryState(new FactoryBounds(new GridPosition(0, 0), 100, 40), catalog, buildings);
            var session = CreateRunningSession(state);
            var commands = new PlacementCommands(session, new CountingIdSource());
            var production = new ProductionCommands(session);
            var setup = CreatePresentation(commands, catalog);
            setup.View.Select(buildings[0].Id);
            Assert.That(setup.View.RenderedBuildingCount, Is.EqualTo(1000));
            AssertBadge(setup.View, buildings[0].Id, F001SpriteCatalog.BadgeProducing);
            AssertBadge(setup.View, buildings[600].Id, F001SpriteCatalog.BadgeFullOutput);
            AssertBadge(setup.View, buildings[900].Id, F001SpriteCatalog.BadgeInactive);
            var selectionEvents = 0;
            setup.View.SelectionChanged += _ => selectionEvents++;

            const long tickTicks = TimeSpan.TicksPerMillisecond * 100L;
            for (var warmup = 0; warmup < 5; warmup++)
            {
                Assert.That(production.AdvanceSimulation(tickTicks).Succeeded, Is.True);
                Assert.That(setup.View.SyncPresentedStates(), Is.False);
            }

            var samples = new long[100];
            for (var sample = 0; sample < samples.Length; sample++)
            {
                Assert.That(production.AdvanceSimulation(tickTicks).Succeeded, Is.True);
                var changed = true;
                samples[sample] = GcAllocationCounter.Count(() => changed = setup.View.SyncPresentedStates());
                Assert.That(changed, Is.False);
            }

            Assert.That(samples, Has.All.EqualTo(0L), "The per-tick view sync allocates no managed memory.");
            Assert.That(selectionEvents, Is.Zero, "An unchanged selected machine does not re-raise the summary.");
            AssertBadge(setup.View, buildings[0].Id, F001SpriteCatalog.BadgeProducing);
            AssertBadge(setup.View, buildings[600].Id, F001SpriteCatalog.BadgeFullOutput);
            AssertBadge(setup.View, buildings[900].Id, F001SpriteCatalog.BadgeInactive);
            yield return null;
        }

        [Test]
        public void PreviewBadgeUsesCompleteOrientedFootprintBoundsAndAuthoritativeCells()
        {
            var catalog = CreateDefinitions();
            var state = new FactoryState(new FactoryBounds(new GridPosition(-8, -6), 10, 9), catalog, Array.Empty<BuildingInstanceState>());
            var commands = new PlacementCommands(CreateRunningSession(state), new CountingIdSource());
            var setup = CreatePresentation(commands, catalog, new Vector2(8f, 6f));
            var d001 = new BuildingDefinitionId("dev.machine.basic");
            var alternate = new BuildingDefinitionId("test.machine.alternate-footprint");
            var anchor = new GridPosition(-7, -5);

            Assert.That(setup.Controller.StartPlacement(d001, anchor, 0), Is.True);
            AssertPreview(setup.Preview, new Vector2(2f, 2f), new[]
            {
                new GridPosition(-7, -5),
                new GridPosition(-6, -5),
                new GridPosition(-7, -4),
                new GridPosition(-6, -4),
            });

            Assert.That(setup.Controller.StartPlacement(alternate, anchor, 0), Is.True);
            AssertPreview(setup.Preview, new Vector2(2.5f, 1.5f), new[]
            {
                new GridPosition(-7, -5),
                new GridPosition(-6, -5),
                new GridPosition(-5, -5),
            });

            Assert.That(setup.Controller.StartPlacement(alternate, anchor, 1), Is.True);
            AssertPreview(setup.Preview, new Vector2(1.5f, 2.5f), new[]
            {
                new GridPosition(-7, -5),
                new GridPosition(-7, -4),
                new GridPosition(-7, -3),
            });
            Assert.That(setup.Preview.Renderers[0].sprite.name, Is.EqualTo("f001-ghost-alt-3x1-valid-v01"));
        }

        private static void AssertPreview(
            PlacementPreviewView preview,
            Vector2 expectedBadgePosition,
            IReadOnlyCollection<GridPosition> expectedCells)
        {
            Assert.That(preview.Cells, Is.EquivalentTo(expectedCells));
            Assert.That(preview.Renderers[0].sortingOrder, Is.EqualTo(FactoryView.PreviewSortingOrder));
            Assert.That(preview.Renderers[1].sortingOrder, Is.EqualTo(FactoryView.BadgeSortingOrder));
            Assert.That(preview.Renderers[1].transform.position.x, Is.EqualTo(expectedBadgePosition.x).Within(0.001f));
            Assert.That(preview.Renderers[1].transform.position.y, Is.EqualTo(expectedBadgePosition.y).Within(0.001f));
            Assert.That(preview.Renderers[1].transform.position.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void UnsupportedOrientationRendersInvalidFeedbackWithoutMutation()
        {
            var catalog = CreateDefinitions();
            var state = new FactoryState(
                new FactoryBounds(new GridPosition(0, 0), 8, 8),
                catalog,
                Array.Empty<BuildingInstanceState>());
            var ids = new CountingIdSource();
            var commands = new PlacementCommands(CreateRunningSession(state), ids);
            var setup = CreatePresentation(commands, catalog);
            var revision = commands.GetFactorySnapshot().Revision;
            var started = false;

            Assert.That(
                () => started = setup.Controller.StartPlacement(
                    new BuildingDefinitionId("dev.machine.basic"),
                    new GridPosition(1, 1),
                    1),
                Throws.Nothing);

            Assert.That(started, Is.True);
            Assert.That(setup.Preview.IsVisible, Is.True);
            Assert.That(setup.Preview.IsValid, Is.False);
            Assert.That(setup.Preview.Feedback, Does.Contain("Orientation is not supported"));
            Assert.That(setup.Preview.Cells, Is.Empty, "No fallback cells may be presented as authoritative.");
            Assert.That(setup.Preview.Renderers, Has.Count.EqualTo(2));
            Assert.That(setup.Preview.Renderers[0].sprite.name, Is.EqualTo("f001-ghost-d001-invalid-v01"));
            Assert.That(setup.Preview.Renderers[0].sortingOrder, Is.EqualTo(FactoryView.PreviewSortingOrder));
            Assert.That(setup.Preview.Renderers[0].transform.position.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(setup.Preview.Renderers[0].transform.position.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(setup.Preview.Renderers[0].transform.eulerAngles.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(setup.Preview.Renderers[1].sprite.name, Is.EqualTo("f001-badge-placement-invalid-v01"));
            Assert.That(setup.Preview.Renderers[1].sortingOrder, Is.EqualTo(FactoryView.BadgeSortingOrder));
            Assert.That(setup.Hud.ConfirmButton.enabledSelf, Is.False);

            setup.Controller.Confirm();

            var snapshot = commands.GetFactorySnapshot();
            Assert.That(ids.Calls, Is.Zero);
            Assert.That(snapshot.Revision, Is.EqualTo(revision));
            Assert.That(snapshot.Buildings, Is.Empty);
            Assert.That(snapshot.Occupancy, Is.Empty);
        }

        [Test]
        public void ConfirmedOrientedAlternateMatchesAuthoritativeCellsAndRemainsSelected()
        {
            var catalog = CreateDefinitions();
            var state = new FactoryState(
                new FactoryBounds(new GridPosition(-8, -6), 10, 9),
                catalog,
                Array.Empty<BuildingInstanceState>());
            var commands = new PlacementCommands(CreateRunningSession(state), new CountingIdSource());
            var setup = CreatePresentation(commands, catalog, new Vector2(8f, 6f));
            var definitionId = new BuildingDefinitionId("test.machine.alternate-footprint");

            setup.Controller.StartPlacement(definitionId, new GridPosition(-7, -5), 1);
            setup.Controller.Confirm();

            var snapshot = commands.GetFactorySnapshot();
            var building = snapshot.Buildings.Single();
            var renderer = setup.View.GetMachineRenderer(building.InstanceId);
            Assert.That(snapshot.Occupancy.Keys, Is.EquivalentTo(new[]
            {
                new GridPosition(-7, -5),
                new GridPosition(-7, -4),
                new GridPosition(-7, -3),
            }));
            Assert.That(building.OrientationQuarterTurns, Is.EqualTo(1));
            Assert.That(renderer.transform.position.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(renderer.transform.position.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(renderer.transform.position.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(renderer.transform.eulerAngles.z, Is.EqualTo(90f).Within(0.001f));
            Assert.That(renderer.bounds.min.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(renderer.bounds.max.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(renderer.bounds.min.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(renderer.bounds.max.y, Is.EqualTo(4f).Within(0.001f));
            Assert.That(setup.View.SelectedInstanceId, Is.EqualTo(building.InstanceId));
            Assert.That(renderer.sprite.name, Is.EqualTo("f001-machine-alt-3x1-selected-v01"));
            Assert.That(setup.View.GetBadgeRenderers(building.InstanceId).Select(item => item.transform.position.x),
                Is.All.EqualTo(1.5f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator HudUsesAttachedAuthoritativeAssetsResponsiveClassesAndNavigationCallbacks()
        {
            var setup = CreateHud();
            yield return null;
            yield return null;

            Assert.That(setup.Hud.IsReady, Is.True, setup.Hud.Error);
            Assert.That(setup.Hud.Root.panel, Is.Not.Null);
            Assert.That(setup.Document.rootVisualElement.Q<VisualElement>("factory-hud"), Is.SameAs(setup.Hud.Root));
            Assert.That(setup.Hud.Root.styleSheets.Contains(setup.StyleSheet), Is.True);

            var layouts = new[]
            {
                (360, 640, FactoryHudLayout.CompactPortrait, "factory-hud--compact-portrait", FlexDirection.Column),
                (640, 360, FactoryHudLayout.CompactLandscape, "factory-hud--compact-landscape", FlexDirection.Column),
                (390, 844, FactoryHudLayout.CompactPortrait, "factory-hud--compact-portrait", FlexDirection.Column),
                (844, 390, FactoryHudLayout.CompactLandscape, "factory-hud--compact-landscape", FlexDirection.Column),
                (768, 1024, FactoryHudLayout.TabletPortrait, "factory-hud--tablet-portrait", FlexDirection.Column),
                (1024, 768, FactoryHudLayout.TabletLandscape, "factory-hud--tablet-landscape", FlexDirection.Row),
                (1280, 720, FactoryHudLayout.Desktop, "factory-hud--desktop", FlexDirection.Row),
                (1920, 1080, FactoryHudLayout.Desktop, "factory-hud--desktop", FlexDirection.Row),
            };
            foreach (var item in layouts)
            {
                var simulatedSafeArea = new Rect(17f, 23f, item.Item1 - 42f, item.Item2 - 62f);
                setup.Hud.ApplyLayout(item.Item1, item.Item2, simulatedSafeArea);
                yield return null;
                Assert.That(setup.Hud.Layout, Is.EqualTo(item.Item3));
                Assert.That(setup.Hud.Root.ClassListContains(item.Item4), Is.True);
                Assert.That(setup.Hud.Root.resolvedStyle.flexDirection, Is.EqualTo(item.Item5));
                Assert.That(setup.Hud.Root.resolvedStyle.paddingLeft, Is.EqualTo(17f).Within(0.01f));
                Assert.That(setup.Hud.Root.resolvedStyle.paddingRight, Is.EqualTo(25f).Within(0.01f));
                Assert.That(setup.Hud.Root.resolvedStyle.paddingTop, Is.EqualTo(39f).Within(0.01f));
                Assert.That(setup.Hud.Root.resolvedStyle.paddingBottom, Is.EqualTo(23f).Within(0.01f));
            }

            var controls = setup.Hud.Root.Query<Button>(className: "factory-hud__control").ToList();
            Assert.That(controls.Count, Is.GreaterThanOrEqualTo(14),
                "The target-size matrix must include factory, inspector, and persistence controls.");
            foreach (var button in controls)
            {
                Assert.That(button.focusable, Is.True);
                Assert.That(button.resolvedStyle.minWidth.value, Is.GreaterThanOrEqualTo(44f));
                Assert.That(button.resolvedStyle.minHeight.value, Is.GreaterThanOrEqualTo(44f));
                Assert.That(button.ClassListContains("factory-hud__control"), Is.True);
                Assert.That(button.text, Is.Not.Empty);
            }

            var buildCount = 0;
            setup.Hud.BuildRequested += () => buildCount++;
            setup.Hud.BuildButton.Focus();
            Assert.That(setup.Hud.BuildButton.panel.focusController.focusedElement, Is.SameAs(setup.Hud.BuildButton));
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                setup.Hud.BuildButton.SendEvent(submit);
            }
            Assert.That(buildCount, Is.EqualTo(1), "Attached navigation submit must activate exactly once.");

            Assert.That(setup.Hud.Configure(setup.Document, setup.Tree, setup.StyleSheet), Is.True);
            yield return null;
            setup.Hud.BuildButton.Focus();
            Assert.That(setup.Hud.BuildButton.panel.focusController.focusedElement, Is.SameAs(setup.Hud.BuildButton));
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                setup.Hud.BuildButton.SendEvent(submit);
            }
            Assert.That(buildCount, Is.EqualTo(2), "Rebuild must unbind old callbacks and avoid duplicate activation.");

            var confirmCount = 0;
            var cancelCount = 0;
            var zoomDirections = new List<int>();
            setup.Hud.ConfirmRequested += () => confirmCount++;
            setup.Hud.CancelRequested += () => cancelCount++;
            setup.Hud.ZoomRequested += zoomDirections.Add;
            setup.Hud.SetPlacementMode(true, "Placement preview is valid.");
            FocusAndSubmit(setup.Hud.ConfirmButton);
            FocusAndSubmit(setup.Hud.CancelButton);
            FocusAndSubmit(setup.Hud.ZoomInButton);
            FocusAndSubmit(setup.Hud.ZoomOutButton);
            Assert.That(confirmCount, Is.EqualTo(1), "Keyboard submit must activate visible Confirm exactly once.");
            Assert.That(cancelCount, Is.EqualTo(1), "Keyboard submit must activate visible Cancel exactly once.");
            Assert.That(zoomDirections, Is.EqualTo(new[] { 1, -1 }),
                "Keyboard submit must activate both visible zoom controls with their intended direction.");

            var resetButton = setup.Hud.Root.Q<Button>("persistence-reset");
            var reloadButton = setup.Hud.Root.Q<Button>("persistence-reload");
            Assert.That(resetButton, Is.Not.Null);
            Assert.That(reloadButton, Is.Not.Null);
            resetButton.SetEnabled(true);
            reloadButton.SetEnabled(true);
            var resetClickCount = 0;
            var reloadClickCount = 0;
            resetButton.clicked += () => resetClickCount++;
            reloadButton.clicked += () => reloadClickCount++;
            FocusAndSubmit(resetButton);
            FocusAndSubmit(reloadButton);
            Assert.That(resetClickCount, Is.EqualTo(1),
                "Navigation submit on the actual persistence Reset button must invoke its click handler exactly once.");
            Assert.That(reloadClickCount, Is.EqualTo(1),
                "Navigation submit on the actual persistence Reload button must invoke its click handler exactly once.");

            setup.Hud.SetPlacementMode(false, "Cannot place: footprint overlaps an existing building.");
            Assert.That(setup.Hud.State, Is.EqualTo(FactoryHudState.Disabled));
            Assert.That(setup.Hud.Root.ClassListContains("factory-hud--disabled"), Is.True);
            Assert.That(setup.Hud.ConfirmButton.enabledSelf, Is.False);
            Assert.That(setup.Hud.FeedbackText, Does.Contain("overlaps"));
            Assert.That(setup.Hud.Root.Q<Label>("feedback").text, Does.Contain("overlaps"),
                "Disabled feedback must remain understandable as text without relying on color.");
            setup.Hud.SetState(FactoryHudState.Error, "Approved sprite mapping is unavailable.");
            Assert.That(setup.Hud.State, Is.EqualTo(FactoryHudState.Error));
            Assert.That(setup.Hud.Root.ClassListContains("factory-hud--error"), Is.True);
            Assert.That(setup.Hud.Root.Q<Label>("status").text, Does.Contain("unavailable"),
                "Error state must be announced in text without relying on color.");
            setup.Hud.Recover("Recovered and ready.");
            Assert.That(setup.Hud.State, Is.EqualTo(FactoryHudState.Normal));
            Assert.That(setup.Hud.BuildButton.enabledSelf, Is.True);
            Assert.That(setup.Hud.FeedbackText, Does.Contain("Recovered"));
            Assert.That(setup.Hud.Root.Q<Label>("status").text, Does.Contain("Recovered"));

            setup.Hud.SetState(FactoryHudState.Empty, "Factory is empty. Use Build to place a development machine.");
            Assert.That(setup.Hud.State, Is.EqualTo(FactoryHudState.Empty));
            Assert.That(setup.Hud.Root.ClassListContains("factory-hud--empty"), Is.True);
            Assert.That(setup.Hud.Root.Q<Label>("status").text, Does.Contain("empty"));
            Assert.That(setup.Hud.Root.Q<Label>("feedback").text, Does.Contain("empty"));
            Assert.That(setup.Hud.BuildButton.enabledSelf, Is.True,
                "The empty state must explain itself in text and leave Build available.");

            Assert.That(setup.Hud.Root.Q<Label>("feedback").style.display.value, Is.EqualTo(DisplayStyle.None),
                "A state message must be drawn once, on the status line, not repeated by the feedback label.");
            Assert.That(setup.Hud.Root.Q<Label>("status").style.display.value, Is.Not.EqualTo(DisplayStyle.None));
            setup.Hud.SetPlacementMode(false, "Cannot place: footprint overlaps an existing building.");
            Assert.That(setup.Hud.Root.Q<Label>("feedback").style.display.value, Is.EqualTo(DisplayStyle.Flex),
                "Placement feedback that differs from the status line must stay visible.");
        }

        private static void FocusAndSubmit(Button button)
        {
            Assert.That(button, Is.Not.Null);
            button.Focus();
            Assert.That(button.panel.focusController.focusedElement, Is.SameAs(button));
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                button.SendEvent(submit);
            }
        }

        [UnityTest]
        public IEnumerator HudMissingAssetsProducesAttachedDisabledErrorState()
        {
            var setup = CreateHud();
            Assert.That(setup.Hud.Configure(setup.Document, null, setup.StyleSheet), Is.False);
            yield return null;

            Assert.That(setup.Hud.State, Is.EqualTo(FactoryHudState.Error));
            Assert.That(setup.Hud.IsReady, Is.False);
            Assert.That(setup.Hud.Root.panel, Is.Not.Null);
            Assert.That(setup.Hud.Root.enabledSelf, Is.False);
            Assert.That(setup.Hud.FeedbackText, Does.Contain("explicit UIDocument, UXML, and USS"));
        }

        private PresentationSetup CreatePresentation(
            PlacementCommands commands,
            DefinitionCatalog definitions,
            Vector2? origin = null)
        {
            var sprites = CreateSpriteCatalog();
            var mapper = new GridWorldMapper(1f, origin ?? Vector2.zero);
            var view = NewObject("factory-view").AddComponent<FactoryView>();
            var preview = NewObject("placement-preview").AddComponent<PlacementPreviewView>();
            var hudSetup = CreateHud("factory-hud");
            var hud = hudSetup.Hud;
            var controller = NewObject("placement-controller").AddComponent<PlacementInputController>();
            preview.Configure(sprites, mapper);
            view.Configure(commands, definitions, sprites, mapper);
            controller.Configure(commands, definitions, view, preview, hud, mapper, point => point);
            return new PresentationSetup(view, preview, hud, controller);
        }

        private HudSetup CreateHud(string name = "hud-layout")
        {
            var gameObject = NewObject(name);
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            transientObjects.Add(panelSettings);
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var hud = gameObject.AddComponent<FactoryHudDocument>();
            var tree = LoadAsset<VisualTreeAsset>("Assets/IdleFactory/Unity/UI/CoreGameFoundation.uxml");
            var styles = LoadAsset<StyleSheet>("Assets/IdleFactory/Unity/UI/CoreGameFoundation.uss");
            Assert.That(hud.Configure(document, tree, styles), Is.True, hud.Error);
            return new HudSetup(hud, document, tree, styles);
        }

        private F001SpriteCatalog CreateSpriteCatalog()
        {
            var catalog = new F001SpriteCatalog();
            catalog.Configure(LoadMappings());
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            return catalog;
        }

        private List<F001SpriteEntry> LoadMappings()
        {
            return new List<F001SpriteEntry>
            {
                Entry(F001SpriteCatalog.GridTile, "f001-grid-tile-1x1-v01.png"),
                Entry(F001SpriteCatalog.BoundaryEdge, "f001-grid-boundary-edge-v01.png"),
                Entry(F001SpriteCatalog.BoundaryCorner, "f001-grid-boundary-corner-v01.png"),
                Entry(F001SpriteCatalog.D001Normal, "f001-machine-d001-normal-v01.png"),
                Entry(F001SpriteCatalog.D001Selected, "f001-machine-d001-selected-v01.png"),
                Entry(F001SpriteCatalog.AlternateNormal, "f001-machine-alt-3x1-normal-v01.png"),
                Entry(F001SpriteCatalog.AlternateSelected, "f001-machine-alt-3x1-selected-v01.png"),
                Entry(F001SpriteCatalog.D001GhostValid, "f001-ghost-d001-valid-v01.png"),
                Entry(F001SpriteCatalog.D001GhostInvalid, "f001-ghost-d001-invalid-v01.png"),
                Entry(F001SpriteCatalog.AlternateGhostValid, "f001-ghost-alt-3x1-valid-v01.png"),
                Entry(F001SpriteCatalog.AlternateGhostInvalid, "f001-ghost-alt-3x1-invalid-v01.png"),
                Entry(F001SpriteCatalog.ResourceInput, "f001-resource-dev-input-v01.png"),
                Entry(F001SpriteCatalog.ResourceOutput, "f001-resource-dev-output-v01.png"),
                Entry(F001SpriteCatalog.BadgeNormal, "f001-badge-normal-v01.png"),
                Entry(F001SpriteCatalog.BadgeSelected, "f001-badge-selected-v01.png"),
                Entry(F001SpriteCatalog.BadgePlacementValid, "f001-badge-placement-valid-v01.png"),
                Entry(F001SpriteCatalog.BadgePlacementInvalid, "f001-badge-placement-invalid-v01.png"),
                Entry(F001SpriteCatalog.BadgeProducing, "f001-badge-producing-v01.png"),
                Entry(F001SpriteCatalog.BadgeMissingInput, "f001-badge-missing-input-v01.png"),
                Entry(F001SpriteCatalog.BadgeFullOutput, "f001-badge-full-output-v01.png"),
                Entry(F001SpriteCatalog.BadgeInactive, "f001-badge-inactive-v01.png"),
            };
        }

        private F001SpriteEntry Entry(string key, string fileName)
        {
            const string root = "Assets/IdleFactory/Art/Development/F001/Imported/";
            return new F001SpriteEntry(key, LoadAsset<Sprite>(root + fileName));
        }

        private static T LoadAsset<T>(string path) where T : Object
        {
            var editorType = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            Assert.That(editorType, Is.Not.Null);
            var loadMethod = editorType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "LoadAssetAtPath" && method.IsGenericMethodDefinition);
            var asset = (T)loadMethod.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { path });
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        private static DefinitionCatalog CreateDefinitions(long inputCapacity = 4, long outputCapacity = 1)
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
            var d001 = new BuildingDefinition(
                new BuildingDefinitionId("dev.machine.basic"),
                new Footprint(2, 2),
                new[] { 0 },
                recipeId,
                new BufferDefinition(BufferRole.Input, Quantities(input, inputCapacity)),
                new BufferDefinition(BufferRole.Output, Quantities(output, outputCapacity)),
                F001SpriteCatalog.D001Normal,
                true);
            var alternate = new BuildingDefinition(
                new BuildingDefinitionId("test.machine.alternate-footprint"),
                new Footprint(3, 1),
                new[] { 0, 1 },
                null,
                null,
                null,
                F001SpriteCatalog.AlternateNormal,
                true);
            return new DefinitionCatalog(
                new[]
                {
                    new ResourceDefinition(input, "Input", F001SpriteCatalog.ResourceInput, true),
                    new ResourceDefinition(output, "Output", F001SpriteCatalog.ResourceOutput, true),
                },
                new[] { recipe },
                new[] { d001, alternate },
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
            bool active,
            long inputQuantity,
            long outputQuantity)
        {
            var definition = catalog.Resolve(new BuildingDefinitionId("dev.machine.basic"));
            var recipe = catalog.Resolve(new RecipeId("dev.recipe.basic"));
            var input = new ResourceId("dev.input");
            var output = new ResourceId("dev.output");
            return new BuildingInstanceState(
                new BuildingInstanceId(new Guid(id, 0, 0, new byte[8])),
                definition,
                anchor,
                0,
                active,
                new ResourceBufferState(definition.InputBuffer, Quantities(input, inputQuantity)),
                new ResourceBufferState(definition.OutputBuffer, Quantities(output, outputQuantity)),
                new ProductionState(recipe, 0));
        }

        private static KeyValuePair<ResourceId, long>[] Quantities(ResourceId id, long quantity) =>
            new[] { new KeyValuePair<ResourceId, long>(id, quantity) };

        private static FactorySession CreateRunningSession(FactoryState state)
        {
            var session = new FactorySession(state, state.Catalog, new TestClock(), new TestStore());
            Transition(session, LifecycleState.Loading);
            Transition(session, LifecycleState.Running);
            return session;
        }

        private static void Transition(FactorySession session, LifecycleState state)
        {
            var method = typeof(FactorySession).GetMethod("TransitionTo", BindingFlags.Instance | BindingFlags.NonPublic);
            var result = (CommandResult)method.Invoke(session, new object[] { state });
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static void ReplaceState(FactorySession session, FactoryState state)
        {
            var method = typeof(FactorySession).GetMethod("ReplaceState", BindingFlags.Instance | BindingFlags.NonPublic);
            var result = (CommandResult)method.Invoke(session, new object[] { state });
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static void AssertBadge(FactoryView view, BuildingInstanceId id, string key)
        {
            Assert.That(view.GetBadgeRenderers(id).Select(renderer => renderer.sprite),
                Has.Some.Matches<Sprite>(sprite => sprite.name == SpriteName(key)));
            Assert.That(view.GetBadgeRenderers(id),
                Has.All.Matches<SpriteRenderer>(renderer => renderer.sortingOrder == FactoryView.BadgeSortingOrder));
        }

        private static void AssertNoBadge(FactoryView view, BuildingInstanceId id, string key)
        {
            Assert.That(view.GetBadgeRenderers(id).Select(renderer => renderer.sprite.name),
                Has.None.EqualTo(SpriteName(key)));
        }

        private static string SpriteName(string key) => key.ToLowerInvariant().Replace("f001-", "f001-") + "-v01";

        private GameObject NewObject(string name)
        {
            var gameObject = new GameObject(name);
            objects.Add(gameObject);
            return gameObject;
        }

        private readonly struct PresentationSetup
        {
            public PresentationSetup(
                FactoryView view,
                PlacementPreviewView preview,
                FactoryHudDocument hud,
                PlacementInputController controller)
            {
                View = view;
                Preview = preview;
                Hud = hud;
                Controller = controller;
            }

            public FactoryView View { get; }
            public PlacementPreviewView Preview { get; }
            public FactoryHudDocument Hud { get; }
            public PlacementInputController Controller { get; }
        }

        private readonly struct HudSetup
        {
            public HudSetup(FactoryHudDocument hud, UIDocument document, VisualTreeAsset tree, StyleSheet styleSheet)
            {
                Hud = hud;
                Document = document;
                Tree = tree;
                StyleSheet = styleSheet;
            }

            public FactoryHudDocument Hud { get; }
            public UIDocument Document { get; }
            public VisualTreeAsset Tree { get; }
            public StyleSheet StyleSheet { get; }
        }

        private sealed class CountingIdSource : IBuildingInstanceIdSource
        {
            public int Calls { get; private set; }

            public CommandResult<BuildingInstanceId> Next()
            {
                Calls++;
                return CommandResult<BuildingInstanceId>.Success(
                    new BuildingInstanceId(new Guid(100 + Calls, 0, 0, new byte[8])));
            }
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
