using System.Collections;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T528 (Feature 006, FR-013, FR-015, FR-029, research R07, contracts/world-presentation.md section 2): the depot and its links in the world. The code under
    /// test is the real <see cref="DepotLinkView"/> and <see cref="DepotLinkModel"/>; the art is the flat-colour stand-ins of the deck rig (real sizes, pivots and
    /// frame counts). Trunks are static, a branch exists for an occupied location only, a pip runs on a branch only while its machine is Running, the depot shows
    /// its fill and stage, and reduced motion shows the static variants.
    /// </summary>
    public sealed class DepotLinkViewTests
    {
        private DeckTestRig rig;
        private PresentationSpriteCatalog catalog;
        private SlotGridView grid;
        private FactoryDeckView deck;
        private DepotLinkView view;
        private DepotLinkModel model;
        private FactoryWorldLayout layout;

        [SetUp]
        public void SetUp()
        {
            rig = new DeckTestRig();
            catalog = rig.Catalog();
            layout = LatticeLayoutBuilder.ShippedForColumns(3);
            grid = rig.NewGrid(catalog, layout);
            deck = rig.NewDeck(catalog, rig.Settings(), grid);
            model = new DepotLinkModel();
            view = NewView(grid);
        }

        [TearDown]
        public void TearDown() => rig.Dispose();

        private DepotLinkView NewView(SlotGridView bindTo)
        {
            var host = new GameObject("depot-link-view");
            rig.Owned.Add(host);
            var result = host.AddComponent<DepotLinkView>();
            result.Configure(catalog);
            result.Bind(bindTo);
            return result;
        }

        private static MachineStateView Row(int slot, MachineBlocker blocker = MachineBlocker.None, MachinePhase phase = MachinePhase.Running, bool active = true) =>
            new MachineStateView(slot, 0, 1, active, phase, blocker, 1, 0);

        private static MachineStateView[] Running(int count) => Enumerable.Range(0, count).Select(i => Row(i)).ToArray();

        private void Present(MachineStateView[] rows, long milliseconds, bool reduced = false, long used = 0, long capacity = 100)
        {
            model.Refresh(layout.LocationCount, rows, rows.Length, null, used, capacity);
            view.Present(model, milliseconds, reduced);
        }

        private string SpriteNameAt(Vector2Int cell)
        {
            var sprite = view.Tilemap.GetSprite(new Vector3Int(cell.x, cell.y, 0));
            return sprite == null ? null : sprite.name;
        }

        private SpriteRenderer[] EnabledPips() => view.PipRenderers.Where(p => p.enabled).ToArray();

        // ---------------------------------------------------------------------------------------------------
        // The link layer is the third Tilemap
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheLinkLayerIsTheThirdTilemapOfTheWorldOnTheLinksLayerAndSharesTheSlotMaterial()
        {
            Assert.That(view.HasLinkArt, Is.True);
            var tilemaps = Object.FindObjectsByType<TilemapRenderer>(FindObjectsSortMode.None);
            Assert.That(tilemaps, Has.Length.EqualTo(3), "base, deck and links: the budget of three Tilemap renderers (Art Bible 2.2a)");
            Assert.That(view.TilemapRenderer.sortingLayerName, Is.EqualTo(DepotLinkView.LinksSortingLayer));
            Assert.That(view.TilemapRenderer.sharedMaterial, Is.SameAs(deck.SlotMaterial), "a link pixel is lit and filtered like a slot pixel");
            Assert.That(SortingLayer.GetLayerValueFromName("WorldDeck"), Is.LessThan(SortingLayer.GetLayerValueFromName(DepotLinkView.LinksSortingLayer)));
            Assert.That(SortingLayer.GetLayerValueFromName(DepotLinkView.LinksSortingLayer), Is.LessThan(SortingLayer.GetLayerValueFromName("Default")), "links draw under the machines");
            Assert.That(view.Grid.cellSize, Is.EqualTo(FactoryDeckView.CellSize));
            var expectedUp = FactoryDeckView.GridOrigin.y + DepotLinkGeometry.TileCentreAbovePivotPixels / (float)SlotGridLayout.Density;
            Assert.That(view.Tilemap.transform.position.y, Is.EqualTo(expectedUp).Within(1e-4f), "tiles centre 12 art px above their cell pivot");
        }

        [Test]
        public void TheTilemapHoldsExactlyTheTilesOfThePlan()
        {
            Assert.That(DeckTestRig.TileCount(view.Tilemap), Is.EqualTo(view.Plan.Tiles.Count));
            foreach (var tile in view.Plan.Tiles)
            {
                var expected = tile.Kind == LinkTileKind.Junction ? "F006-LINK-JUNCTION" : "F006-LINK-TRUNK";
                Assert.That(SpriteNameAt(tile.Cell), Is.EqualTo(expected), tile.Cell.ToString());
            }

            // The centre of a tile is the cell pivot plus 12 art px, in world space.
            var cell = view.Plan.Tiles[0].Cell;
            var centre = DepotLinkGeometry.TileCentre(cell);
            var world = view.Tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            Assert.That(world.x * SlotGridLayout.Density, Is.EqualTo(centre.x).Within(1e-3f));
            Assert.That(world.y * SlotGridLayout.Density, Is.EqualTo(centre.y).Within(1e-3f));
        }

        [Test]
        public void ADepotOnTheLeftDrawsAMirroredJunction()
        {
            var settings = Object.Instantiate(rig.Settings());
            rig.Owned.Add(settings);
            settings.depotRulePortrait = new MarginCellRule(MarginSide.Left, 0);
            settings.depositRule = new MarginCellRule(MarginSide.Right, -1);
            var cells = new System.Collections.Generic.List<Vector2Int>();
            for (var row = 0; row < 2; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    cells.Add(new Vector2Int(column, row));
                }
            }

            var left = new FactoryWorldLayout("left-depot", cells, WorldOrientation.Portrait, settings);
            Assert.That(left.DepotOnRight, Is.False);
            view.SetLayout(left);
            foreach (var tile in view.Plan.Tiles.Where(t => t.Kind == LinkTileKind.Junction))
            {
                var drawn = view.Tilemap.GetTile<Tile>(new Vector3Int(tile.Cell.x, tile.Cell.y, 0));
                Assert.That(drawn, Is.Not.Null);
                Assert.That(drawn.sprite.name, Is.EqualTo("F006-LINK-JUNCTION"));
                Assert.That(drawn.transform.m00, Is.EqualTo(-1f).Within(1e-4f), "the junction is flipped horizontally");
                Assert.That(drawn.transform.m11, Is.EqualTo(1f).Within(1e-4f));
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Trunks are static, branches follow occupation
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TrunksNeverChangeAndABranchShowsOnTheCellOfEveryOccupiedLocationOnly()
        {
            var before = view.Plan.Tiles.ToDictionary(t => t.Cell, t => SpriteNameAt(t.Cell));
            var rows = Running(8);
            Present(rows, 0);
            for (var location = 0; location < layout.LocationCount; location++)
            {
                var cell = layout.CellOf(new BuildingLocationId(location));
                var expected = location < 8 ? DepotLinkGeometry.BranchKey : "F006-LINK-TRUNK";
                Assert.That(SpriteNameAt(cell), Is.EqualTo(expected), "location " + location);
                Assert.That(view.TileKeyAt(location), Is.EqualTo(expected));
            }

            foreach (var tile in view.Plan.Tiles.Where(t => t.Location < 0))
            {
                Assert.That(SpriteNameAt(tile.Cell), Is.EqualTo(before[tile.Cell]), "a trunk piece or junction without a location never changes: " + tile.Cell);
            }

            Assert.That(DeckTestRig.TileCount(view.Tilemap), Is.EqualTo(view.Plan.Tiles.Count), "branches replace trunk pieces, they add no tile");

            // A machine removed takes its branch with it; the trunk piece returns.
            Present(Running(7), 16);
            Assert.That(SpriteNameAt(layout.CellOf(new BuildingLocationId(7))), Is.EqualTo("F006-LINK-TRUNK"));
            Assert.That(SpriteNameAt(layout.CellOf(new BuildingLocationId(6))), Is.EqualTo(DepotLinkGeometry.BranchKey));
        }

        [Test]
        public void ABranchSurvivesAMachineThatStopsRunning()
        {
            var rows = new[] { Row(0), Row(1, MachineBlocker.MissingInput, MachinePhase.Paused), Row(2, MachineBlocker.None, MachinePhase.Paused, false) };
            Present(rows, 0);
            for (var location = 0; location < 3; location++)
            {
                Assert.That(view.TileKeyAt(location), Is.EqualTo(DepotLinkGeometry.BranchKey), "occupied: the branch stays whatever the state");
            }

            Assert.That(EnabledPips(), Has.Length.EqualTo(1), "only the Running machine has a pip");
        }

        // ---------------------------------------------------------------------------------------------------
        // Pips
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void PipsRunOnBranchesOnlyOneForEveryRunningMachineAndNoneOnATrunk()
        {
            var rows = new[] { Row(0), Row(1), Row(2, MachineBlocker.StorageFull, MachinePhase.Paused), Row(3) };
            for (long t = 0; t < 2400; t += 37)
            {
                Present(rows, t);
                var pips = EnabledPips();
                Assert.That(pips, Has.Length.EqualTo(3), "one pip per Running machine at " + t);
                foreach (var pip in pips)
                {
                    var x = Mathf.RoundToInt(pip.transform.localPosition.x * SlotGridLayout.Density);
                    var y = Mathf.RoundToInt(pip.transform.localPosition.y * SlotGridLayout.Density);
                    var location = Enumerable.Range(0, layout.LocationCount).Single(i => view.Plan.PipX(i) == x && y >= view.Plan.PipStartY(i) && y <= view.Plan.PipEndY(i));
                    Assert.That(model.BranchOf(location), Is.EqualTo(BranchState.Running), "the pip belongs to a Running machine");
                    Assert.That(y, Is.InRange(view.Plan.PipStartY(location), view.Plan.PipEndY(location)), "the pip is on the branch, never past the trunk lane");
                    Assert.That(pip.sortingLayerName, Is.EqualTo("Overlay"));
                    Assert.That(pip.sortingOrder, Is.LessThan(SlotGridView.FrameOrder), "a pip passes under every cue");
                    Assert.That(Mathf.Approximately(pip.transform.localPosition.x * SlotGridLayout.Density, x), Is.True, "whole art pixels (rule P3)");
                    Assert.That(Mathf.Approximately(pip.transform.localPosition.y * SlotGridLayout.Density, y), Is.True, "whole art pixels (rule P3)");
                }
            }
        }

        [UnityTest]
        public IEnumerator APipStopsWithinOneFrameOfABlock()
        {
            var running = Running(4);
            Present(running, 100);
            yield return null;
            Assert.That(EnabledPips(), Has.Length.EqualTo(4));
            var frameOfBlock = Time.frameCount;

            // The machine of slot 2 becomes blocked in this frame; the very next present (the next frame) draws no pip for it.
            var blocked = running.ToArray();
            blocked[2] = Row(2, MachineBlocker.MissingInput, MachinePhase.Paused);
            yield return null;
            Present(blocked, 116);
            Assert.That(Time.frameCount - frameOfBlock, Is.LessThanOrEqualTo(1), "one frame after the block");
            Assert.That(EnabledPips(), Has.Length.EqualTo(3), "the blocked machine's pip is gone at once");
            Assert.That(view.PipRenderers[2].enabled, Is.False);
            Assert.That(model.BranchOf(2), Is.EqualTo(BranchState.Idle));

            // Every kind of block stops it, and a store full or a removed machine too.
            foreach (var stopped in new[]
            {
                Row(1, MachineBlocker.StorageFull, MachinePhase.Paused),
                Row(1, MachineBlocker.Inactive, MachinePhase.Paused, false),
                Row(1, MachineBlocker.None, MachinePhase.Waiting),
            })
            {
                var rows = running.ToArray();
                rows[1] = stopped;
                Present(rows, 132);
                Assert.That(view.PipRenderers[1].enabled, Is.False, stopped.Blocker + "/" + stopped.Phase);
            }

            Present(running.Take(1).ToArray(), 148);
            Assert.That(EnabledPips(), Has.Length.EqualTo(1), "a removed machine loses its pip in the same frame");
        }

        [Test]
        public void ThePipCrossesTheBranchOnceEverySixHundredMillisecondsAtNormalPaceAndRestartsAtTheRearEdge()
        {
            var rows = Running(1);
            Present(rows, 0);
            var pip = view.PipRenderers[0];
            var start = view.Plan.PipStartY(0);
            var end = view.Plan.PipEndY(0);
            Assert.That(Mathf.RoundToInt(pip.transform.localPosition.y * SlotGridLayout.Density), Is.EqualTo(start), "a pip starts on the plate rear edge");
            Present(rows, 599);
            Assert.That(Mathf.RoundToInt(pip.transform.localPosition.y * SlotGridLayout.Density), Is.EqualTo(end), "and has reached the trunk lane just before 600 ms");
            Present(rows, 600);
            Assert.That(Mathf.RoundToInt(pip.transform.localPosition.y * SlotGridLayout.Density), Is.EqualTo(start), "then it starts again");
            Present(rows, 6000 + 300);
            var middle = Mathf.RoundToInt(pip.transform.localPosition.y * SlotGridLayout.Density);
            Assert.That(middle, Is.InRange(start + (end - start) / 2 - 1, start + (end - start) / 2 + 1), "the period repeats");
        }

        [Test]
        public void ThePipFrameFollowsTheSharedClockAtTheSpriteRate()
        {
            Present(Running(1), 0);
            var first = view.PipRenderers[0].sprite.name;
            Present(Running(1), 1000);
            var later = view.PipRenderers[0].sprite.name;
            Assert.That(first, Does.StartWith(DepotLinkGeometry.PipKey));
            Assert.That(later, Does.StartWith(DepotLinkGeometry.PipKey));
            Assert.That(first, Is.Not.EqualTo(later), "the 6-frame pip loop advances on the clock");
        }

        // ---------------------------------------------------------------------------------------------------
        // Reduced motion
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void UnderReducedMotionThereAreNoPipsAndARunningBranchShowsTheStaticActiveTile()
        {
            var rows = new[] { Row(0), Row(1, MachineBlocker.MissingInput, MachinePhase.Paused) };
            for (long t = 0; t < 1500; t += 50)
            {
                Present(rows, t, true);
                Assert.That(EnabledPips(), Is.Empty, "no pip is drawn under reduced motion at " + t);
            }

            Assert.That(SpriteNameAt(layout.CellOf(new BuildingLocationId(0))), Is.EqualTo(DepotLinkGeometry.BranchActiveKey), "the Running branch shows the static active tile");
            Assert.That(SpriteNameAt(layout.CellOf(new BuildingLocationId(1))), Is.EqualTo(DepotLinkGeometry.BranchKey), "a stopped machine keeps the plain branch");
            Assert.That(SpriteNameAt(layout.CellOf(new BuildingLocationId(2))), Is.EqualTo("F006-LINK-TRUNK"));

            // Back to normal motion: the plain branch and the pip return.
            Present(rows, 2000, false);
            Assert.That(SpriteNameAt(layout.CellOf(new BuildingLocationId(0))), Is.EqualTo(DepotLinkGeometry.BranchKey));
            Assert.That(EnabledPips(), Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator NoPixelOfTheLinkLayerMovesUnderReducedMotion()
        {
            // The tilemap and the depot are identical in every frame, and there is no pip: nothing decorative moves.
            var rows = Running(8);
            Present(rows, 0, true, 40);
            yield return null;
            var snapshot = view.Plan.Tiles.Select(t => SpriteNameAt(t.Cell)).Concat(new[] { view.ShownDepotKey }).ToArray();
            for (var frame = 1; frame <= 120; frame++)
            {
                Present(rows, frame * 16L, true, 40);
                Assert.That(EnabledPips(), Is.Empty);
                Assert.That(view.Plan.Tiles.Select(t => SpriteNameAt(t.Cell)).Concat(new[] { view.ShownDepotKey }).ToArray(), Is.EqualTo(snapshot), "frame " + frame);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Depot
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheDepotShowsTheFillStateTimesTheStageVariantAndTheFullState()
        {
            var rows = Running(2);
            for (var stage = 0; stage < WorldCompositionSettings.StageCount; stage++)
            {
                view.SetStage(stage);
                foreach (var (used, state) in new[] { (0L, "EMPTY"), (30L, "LOW"), (60L, "HIGH"), (80L, "FULL"), (100L, "FULL") })
                {
                    Present(rows, 0, false, used, 100);
                    Assert.That(view.DepotRenderer.enabled, Is.True);
                    Assert.That(view.DepotRenderer.sprite.name, Is.EqualTo("F006-DEPOT-S" + (stage + 1) + "-" + state), "stage " + stage + " used " + used);
                }
            }

            view.SetStage(0);
            var blocked = new[] { Row(0), Row(1, MachineBlocker.StorageFull, MachinePhase.Paused) };
            Present(blocked, 0, false, 10, 100);
            Assert.That(view.DepotRenderer.sprite.name, Is.EqualTo("F006-DEPOT-S1-FULL"), "a machine that cannot store its output shows the full depot");
        }

        [Test]
        public void TheDepotSitsOnTheDepotAnchorAboveTheLinksAndBelowTheMachines()
        {
            Present(Running(2), 0);
            var depot = view.DepotRenderer;
            Assert.That((Vector2)depot.transform.localPosition, Is.EqualTo(layout.DepotAnchor));
            Assert.That(depot.sortingLayerName, Is.EqualTo(DepotLinkView.LinksSortingLayer));
            Assert.That(depot.sortingOrder, Is.GreaterThan(view.TilemapRenderer.sortingOrder), "above the link tiles");
            Assert.That(depot.sortingLayerID, Is.Not.EqualTo(SortingLayer.NameToID("Default")), "the plates and machines on the Default layer draw over it");
            var worldRect = new Rect(depot.transform.position.x - 40 / 24f, depot.transform.position.y - 20 / 24f, 80 / 24f, 76 / 24f);
            Assert.That(depot.bounds.min.x, Is.EqualTo(worldRect.xMin).Within(1e-3f));
            Assert.That(depot.bounds.min.y, Is.EqualTo(worldRect.yMin).Within(1e-3f));
            Assert.That(depot.sharedMaterial, Is.SameAs(deck.SlotMaterial));
        }

        [Test]
        public void ALayoutChangeRedrawsTheLinksForTheNewLayout()
        {
            Present(Running(8), 0);
            var six = IdleFactoryTestLayouts.Six();
            view.SetLayout(six);
            layout = six;
            Assert.That(DeckTestRig.TileCount(view.Tilemap), Is.EqualTo(view.Plan.Tiles.Count));
            Assert.That(view.Plan.LocationCount, Is.EqualTo(6));
            Present(Running(4), 0);
            for (var location = 0; location < 6; location++)
            {
                Assert.That(view.TileKeyAt(location), Is.EqualTo(location < 4 ? DepotLinkGeometry.BranchKey : "F006-LINK-TRUNK"), "location " + location);
            }

            Assert.That(EnabledPips(), Has.Length.EqualTo(4), "the pips of the old layout are gone");
        }

        [Test]
        public void AViewWithoutTheLinkArtDrawsNothingAndRaisesNoError()
        {
            var empty = new PresentationSpriteCatalog();
            empty.Configure(new System.Collections.Generic.List<PresentationSpriteEntry>(), new System.Collections.Generic.List<PresentationSpriteRequirement>());
            var host = new GameObject("no-art");
            rig.Owned.Add(host);
            var bare = host.AddComponent<DepotLinkView>();
            bare.Configure(empty);
            bare.Bind(grid);
            Assert.That(bare.HasLinkArt, Is.False);
            model.Refresh(layout.LocationCount, Running(3), 3, null, 10, 100);
            Assert.DoesNotThrow(() => bare.Present(model, 0, false));
            Assert.That(DeckTestRig.TileCount(bare.Tilemap), Is.Zero);
            Assert.That(bare.DepotRenderer.enabled, Is.False);
            Assert.That(bare.PipRenderers.Where(p => p.enabled), Is.Empty);
        }

        private static class IdleFactoryTestLayouts
        {
            public static FactoryWorldLayout Six()
            {
                var cells = new System.Collections.Generic.List<Vector2Int>();
                for (var row = 0; row < 2; row++)
                {
                    for (var column = 0; column < 3; column++)
                    {
                        cells.Add(new Vector2Int(column, row));
                    }
                }

                return new FactoryWorldLayout("six", cells, WorldOrientation.Portrait);
            }
        }
    }
}
