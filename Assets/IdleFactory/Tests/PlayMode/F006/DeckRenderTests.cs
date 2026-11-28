using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T527 (Feature 006, contracts/world-presentation.md section 1): the deck as the engine draws it. Sorting layers and orders (the base and deck
    /// layers sit under the slots, no prop or edge piece above the overlay), material parity of the Tilemap renderers and every deck sprite with the
    /// slot sprites, the deck and hatch tiles per unlock state, and the follow-through of layout, stage and condition changes. The art is the stand-in
    /// of <see cref="DeckTestRig"/> (registry-sized flat colours), so the tests also run in the Windows test player.
    /// </summary>
    public sealed class DeckRenderTests
    {
        private DeckTestRig rig;

        [SetUp]
        public void SetUp() => rig = new DeckTestRig();

        [TearDown]
        public void TearDown() => rig.Dispose();

        private static SlotCondition Mixed(int i) => i == 0 ? SlotCondition.Occupied : i == 1 ? SlotCondition.Empty : SlotCondition.Locked;

        private (SlotGridView grid, FactoryDeckView deck, PresentationSpriteCatalog catalog) Compose(FactoryWorldLayout layout, Func<int, SlotCondition> condition = null)
        {
            var catalog = rig.Catalog();
            var grid = rig.NewGrid(catalog, layout);
            var deck = rig.NewDeck(catalog, rig.Settings(), grid);
            grid.Render(DeckTestRig.Frame(layout.LocationCount, condition ?? Mixed));
            return (grid, deck, catalog);
        }

        [Test]
        public void TheDeckDrawsABaseTileUnderEveryDeckCellAndADeckOrHatchTileOnEveryLocation()
        {
            var layout = LatticeLayoutBuilder.ShippedForColumns(3);
            var (grid, deck, catalog) = Compose(layout);
            Assert.That(deck.HasArt, Is.True);
            Assert.That(deck.Grid.cellLayout, Is.EqualTo(GridLayout.CellLayout.Rectangle), "a rectangle grid, not isometric");
            Assert.That(deck.Grid.cellSize.x, Is.EqualTo(70f / 24f).Within(1e-6f));
            Assert.That(deck.Grid.cellSize.y, Is.EqualTo(54f / 24f).Within(1e-6f));
            catalog.TryGet(FactoryDeckView.BaseTileKey, out var baseSprite);
            catalog.TryGet(FactoryDeckView.DeckTileKey, out var deckSprite);
            catalog.TryGet(FactoryDeckView.HatchTileKey, out var hatchSprite);
            foreach (var cell in layout.DeckCells)
            {
                Assert.That(deck.BaseTilemap.GetSprite(new Vector3Int(cell.x, cell.y, 0)), Is.SameAs(baseSprite), "base tile at " + cell);
            }

            Assert.That(DeckTestRig.TileCount(deck.BaseTilemap), Is.EqualTo(deck.Plan.BaseCells.Count));
            Assert.That(DeckTestRig.TileCount(deck.DeckTilemap), Is.EqualTo(layout.LocationCount), "one deck or hatch tile per location");
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var cell = layout.CellOf(new BuildingLocationId(i));
                var expected = Mixed(i) == SlotCondition.Locked ? hatchSprite : deckSprite;
                Assert.That(deck.DeckTilemap.GetSprite(new Vector3Int(cell.x, cell.y, 0)), Is.SameAs(expected), "location " + i);
            }

            // A tile centre is the footprint pivot of its lattice cell, in the same local space as the slots.
            var probe = new Vector3Int(1, 2, 0);
            var centre = deck.Grid.transform.TransformPoint(deck.Grid.GetCellCenterLocal(probe));
            Assert.That(Vector2.Distance(centre, layout.PivotOfCell(new Vector2Int(1, 2))), Is.LessThan(1e-4f), "cell (1, 2) is centred on its slot pivot");
            Assert.That(grid.ActiveRendererCount, Is.GreaterThan(0));
        }

        [Test]
        public void SortingLayersPutBaseAndDeckUnderTheSlotsAndNoPropOrEdgeIsAboveTheOverlay()
        {
            var layout = LatticeLayoutBuilder.ShippedForColumns(4);
            var (grid, deck, _) = Compose(layout);
            deck.SetStage(2);
            var baseLayer = SortingLayer.GetLayerValueFromName(FactoryDeckView.BaseSortingLayer);
            var deckLayer = SortingLayer.GetLayerValueFromName(FactoryDeckView.DeckSortingLayer);
            var defaultLayer = SortingLayer.GetLayerValueFromName("Default");
            var overlayLayer = SortingLayer.GetLayerValueFromName(SlotGridView.OverlaySortingLayer);
            Assert.That(baseLayer, Is.LessThan(deckLayer));
            Assert.That(deckLayer, Is.LessThan(defaultLayer));
            Assert.That(defaultLayer, Is.LessThan(overlayLayer));

            Assert.That(SortingLayer.GetLayerValueFromID(deck.BaseRenderer.sortingLayerID), Is.EqualTo(baseLayer));
            Assert.That(SortingLayer.GetLayerValueFromID(deck.DeckRenderer.sortingLayerID), Is.EqualTo(deckLayer));
            Assert.That(deck.PropRenderers.Count(r => r.enabled), Is.EqualTo(deck.Plan.Props.Count));
            Assert.That(deck.Plan.Props.Count, Is.GreaterThan(0));
            Assert.That(deck.EdgeRenderers.Count(r => r.enabled), Is.EqualTo(deck.Plan.Edges.Count));
            foreach (var renderer in deck.PropRenderers.Concat(deck.EdgeRenderers).Where(r => r.enabled))
            {
                var value = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID);
                Assert.That(value, Is.EqualTo(deckLayer), renderer.name + " is on the deck layer");
                Assert.That(value, Is.LessThan(overlayLayer), renderer.name + " is never above the overlay");
                Assert.That(value, Is.LessThan(defaultLayer), renderer.name + " draws under every plate and machine");
            }

            foreach (var slotRenderer in grid.GetComponentsInChildren<SpriteRenderer>().Where(r => r.enabled))
            {
                var value = SortingLayer.GetLayerValueFromID(slotRenderer.sortingLayerID);
                Assert.That(value, Is.GreaterThan(deckLayer), "every slot renderer sits above the deck layer");
            }

            // Front rows draw later (a lower lattice row gets a higher order), and edge pieces draw after props, apex last.
            var orders = deck.Plan.Props.Select((p, i) => (p, order: deck.PropRenderers[i].sortingOrder)).ToList();
            foreach (var a in orders)
            {
                foreach (var b in orders.Where(x => x.p.Cell.y < a.p.Cell.y))
                {
                    Assert.That(b.order, Is.GreaterThanOrEqualTo(a.order), "a front row draws after the row behind it");
                }
            }

            Assert.That(deck.EdgeRenderers.Where(r => r.enabled).Min(r => r.sortingOrder), Is.GreaterThan(deck.PropRenderers.Where(r => r.enabled).Max(r => r.sortingOrder)));
        }

        [Test]
        public void TheTilemapRenderersAndEveryDeckSpriteUseTheSlotSpriteMaterial()
        {
            var (grid, deck, _) = Compose(LatticeLayoutBuilder.ShippedForColumns(3));
            var slotMaterial = grid.GetComponentsInChildren<SpriteRenderer>(true).First().sharedMaterial;
            Assert.That(slotMaterial, Is.Not.Null);
            Assert.That(deck.BaseRenderer.sharedMaterial, Is.SameAs(slotMaterial), "material parity: the base Tilemap");
            Assert.That(deck.DeckRenderer.sharedMaterial, Is.SameAs(slotMaterial), "material parity: the deck Tilemap");
            Assert.That(deck.SlotMaterial, Is.SameAs(slotMaterial));
            foreach (var renderer in deck.PropRenderers.Concat(deck.EdgeRenderers))
            {
                Assert.That(renderer.sharedMaterial, Is.SameAs(slotMaterial), renderer.name);
            }

            Assert.That(deck.GetComponentsInChildren<TilemapRenderer>(true).Length, Is.EqualTo(2), "world-layer budget: base and deck (the link layer is T528)");
        }

        [Test]
        public void StageConditionAndLayoutChangesAreFollowed()
        {
            var layout = LatticeLayoutBuilder.ShippedForColumns(3);
            var (grid, deck, catalog) = Compose(layout);
            Assert.That(deck.EdgeRenderers.Where(r => r.enabled).All(r => r.sprite.name.StartsWith("F006-EDGE-RAILING", StringComparison.Ordinal)), Is.True, "stage 1 draws railings only");
            deck.SetStage(1);
            Assert.That(deck.EdgeRenderers.Where(r => r.enabled).All(r => r.sprite.name.StartsWith("F006-EDGE-WALL", StringComparison.Ordinal)), Is.True);
            deck.SetStage(2);
            Assert.That(deck.EdgeRenderers.Where(r => r.enabled).All(r => r.sprite.name.StartsWith("F006-EDGE-GANTRY", StringComparison.Ordinal)), Is.True);
            deck.SetStage(0);

            catalog.TryGet(FactoryDeckView.DeckTileKey, out var deckSprite);
            catalog.TryGet(FactoryDeckView.HatchTileKey, out var hatchSprite);
            var cell5 = layout.CellOf(new BuildingLocationId(5));
            Assert.That(deck.DeckTilemap.GetSprite(new Vector3Int(cell5.x, cell5.y, 0)), Is.SameAs(hatchSprite));
            grid.Render(DeckTestRig.Frame(12, i => i <= 5 ? SlotCondition.Empty : SlotCondition.Locked));
            Assert.That(deck.DeckTilemap.GetSprite(new Vector3Int(cell5.x, cell5.y, 0)), Is.SameAs(deckSprite), "an unlocked location shows the deck, not the hatch");

            var before = deck.Plan;
            grid.Render(DeckTestRig.Frame(12, i => i <= 5 ? SlotCondition.Empty : SlotCondition.Locked));
            Assert.That(deck.Plan, Is.SameAs(before), "an unchanged frame does not re-plan");

            grid.SetLayout(LatticeLayoutBuilder.ShippedForColumns(4));
            Assert.That(deck.Plan.BaseCells.Count, Is.Not.EqualTo(before.BaseCells.Count));
            Assert.That(DeckTestRig.TileCount(deck.BaseTilemap), Is.EqualTo(deck.Plan.BaseCells.Count));
            Assert.That(deck.Plan.LocationCells[0], Is.EqualTo(grid.Layout.CellOf(new BuildingLocationId(0))));
        }

        [Test]
        public void ADevelopmentCatalogWithoutTheWorldArtDrawsNoDeckAndRaisesNoError()
        {
            var empty = new PresentationSpriteCatalog();
            empty.Configure(new PresentationSpriteEntry[0]);
            var grid = rig.NewGrid(rig.Catalog(), LatticeLayoutBuilder.ShippedForColumns(3));
            var host = new GameObject("deck-no-art");
            rig.Owned.Add(host);
            var deck = host.AddComponent<FactoryDeckView>();
            deck.Configure(empty, rig.Settings());
            deck.Bind(grid);
            grid.Render(DeckTestRig.Frame(12, Mixed));
            Assert.That(deck.HasArt, Is.False);
            Assert.That(DeckTestRig.TileCount(deck.BaseTilemap), Is.EqualTo(0));
            Assert.That(deck.PropRenderers.Count(r => r.enabled) + deck.EdgeRenderers.Count(r => r.enabled), Is.EqualTo(0));
        }

#if UNITY_EDITOR
        private RunSceneRun sceneRun;
        private string sceneRoot;

        [UnityTearDown]
        public IEnumerator StopTheScene()
        {
            if (sceneRun != null)
            {
                yield return sceneRun.Stop();
                sceneRun = null;
            }

            if (sceneRoot != null)
            {
                RunSceneRun.DeleteRoot(sceneRoot);
                sceneRoot = null;
            }
        }

        [UnityTest]
        public IEnumerator TheShippedSceneComposesTheDeckWithTheAuthoredArtAndTheComposition()
        {
            sceneRoot = RunSceneRun.NewRoot();
            sceneRun = new RunSceneRun(sceneRoot, new RunSceneClock());
            yield return sceneRun.Start();
            yield return null;
            yield return null;
            var bootstrap = sceneRun.Bootstrap;
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            var deck = bootstrap.DeckView;
            Assert.That(deck, Is.Not.Null, "the bootstrap composes the deck under the slots");
            Assert.That(deck.HasArt, Is.True, "the scene catalog registers the F006 ground tiles");
            Assert.That(deck.Plan, Is.Not.Null);
            Assert.That(deck.Plan.BaseCells.Count, Is.GreaterThanOrEqualTo(bootstrap.SlotGrid.Layout.DeckCells.Count));
            Assert.That(DeckTestRig.TileCount(deck.DeckTilemap), Is.EqualTo(12));
            Assert.That(LatticeLayoutBuilder.ActiveSettings, Is.Not.Null, "P1-F1: the authored composition feeds the shipped layouts");
            // T540 (owner decision 2026-11-18): the first factory is Early with an unlit margin, so the shipped start carries no ring props; the authored density places
            // them once the ring is lit (stage 1 here, on an all-built factory), and none ever stands over the dark base.
            Assert.That(deck.Plan.Dressed, Is.True);
            Assert.That(deck.Plan.Props.All(p => deck.Plan.LitRingCells.Contains(p.Cell) || deck.Plan.LocationCells.Contains(p.Cell)), Is.True);
            var lit = DeckPlanner.Build(bootstrap.SlotGrid.Layout, Enumerable.Repeat(SlotCondition.Occupied, bootstrap.SlotGrid.Layout.LocationCount).ToArray(), 1,
                LatticeLayoutBuilder.ActiveSettings, new DeckDressing(0));
            Assert.That(lit.Props, Is.Not.Empty, "the authored stage-1 density places props in the lit margin ring");
            var slotMaterial = bootstrap.SlotGrid.GetComponentsInChildren<SpriteRenderer>(true).First().sharedMaterial;
            Assert.That(deck.BaseRenderer.sharedMaterial, Is.SameAs(slotMaterial));
        }
#endif
    }
}
