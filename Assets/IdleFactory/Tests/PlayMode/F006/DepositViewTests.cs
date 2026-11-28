using System.Linq;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T528 (Feature 006, FR-010a, FR-015, research R23, contracts/world-presentation.md section 2a): the ore-vein deposit. Placement on every test layout
    /// (a margin cell outside every hit shape, keep-out and the depot), the chip loop that follows the mining state on the shared clock, the static highlight
    /// under reduced motion, and a tap on the vein that selects nothing. The art is the flat-colour stand-ins of the deck rig at the real size and pivot.
    /// </summary>
    public sealed class DepositViewTests
    {
        private static readonly string[] LayoutNames = { "3x4", "4x3", TestLayouts.SixName, TestLayouts.TwentyName, TestLayouts.LName };

        private DeckTestRig rig;
        private PresentationSpriteCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            rig = new DeckTestRig();
            catalog = rig.Catalog();
        }

        [TearDown]
        public void TearDown() => rig.Dispose();

        private static FactoryWorldLayout Layout(string name)
        {
            switch (name)
            {
                case "3x4": return LatticeLayoutBuilder.ShippedForColumns(3);
                case "4x3": return LatticeLayoutBuilder.ShippedForColumns(4);
                default: return TestLayouts.ByName(name);
            }
        }

        private DepositView NewView(FactoryWorldLayout layout, out SlotGridView grid)
        {
            grid = rig.NewGrid(catalog, layout);
            var host = new GameObject("deposit-view");
            rig.Owned.Add(host);
            var view = host.AddComponent<DepositView>();
            view.Configure(catalog);
            view.Bind(grid);
            return view;
        }

        private static Rect BoundsRect(SpriteRenderer renderer) => Rect.MinMaxRect(renderer.bounds.min.x, renderer.bounds.min.y, renderer.bounds.max.x, renderer.bounds.max.y);

        // ---------------------------------------------------------------------------------------------------
        // Placement
        // ---------------------------------------------------------------------------------------------------

        [TestCaseSource(nameof(LayoutNames))]
        public void TheVeinSitsInItsMarginCellOutsideEveryHitShapeKeepOutAndTheDepot(string name)
        {
            var layout = Layout(name);
            var view = NewView(layout, out _);
            view.Present(false, 0, false);
            var rect = BoundsRect(view.RestRenderer);
            Assert.That(view.RestRenderer.enabled, Is.True, name);
            Assert.That((Vector2)view.RestRenderer.transform.localPosition, Is.EqualTo(layout.DepositAnchor), name + ": the pivot is the deposit cell pivot");
            var cell = layout.DeckTileRect(layout.DepositCell);
            Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(cell.xMin - 1e-3f), name);
            Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(cell.yMin - 1e-3f), name);
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(cell.xMax + 1e-3f), name);
            Assert.That(rect.yMax, Is.LessThanOrEqualTo(cell.yMax + 1e-3f), name + ": the 48 x 34 sprite lies inside its 70 x 54 margin cell");
            Assert.That(layout.DepositCell.x < 0 || layout.DepositCell.x >= layout.ColumnCount || layout.DepositCell.y < 0 || layout.DepositCell.y >= layout.RowCount, Is.True,
                name + ": a margin cell, not a lattice cell");
            Assert.That(layout.DepositCell, Is.Not.EqualTo(layout.DepotCell));
            Assert.That(FactoryWorldLayout.Overlaps(rect, layout.DepotRect), Is.False, name + ": not under the depot");
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var id = new BuildingLocationId(i);
                Assert.That(FactoryWorldLayout.Overlaps(rect, layout.LocationCellRectOf(id)), Is.False, name + ": outside the hit shape cell of " + id);
                for (var k = 0; k < FactoryWorldLayout.KeepOutCount; k++)
                {
                    Assert.That(FactoryWorldLayout.Overlaps(rect, layout.KeepOutOf(id, k)), Is.False, name + ": outside keep-out " + k + " of " + id);
                }
            }

            view.Present(true, 0, false);
            Assert.That(view.ChipRenderer.enabled, Is.True);
            Assert.That(BoundsRect(view.ChipRenderer), Is.EqualTo(rect), name + ": the chip loop shares the vein's anchor and canvas (R23)");
        }

        [Test]
        public void TheVeinFollowsALayoutChange()
        {
            var view = NewView(Layout("3x4"), out var grid);
            view.Present(false, 0, false);
            var first = (Vector2)view.RestRenderer.transform.localPosition;
            var next = Layout("4x3");
            grid.SetLayout(next);
            view.Present(false, 0, false);
            Assert.That((Vector2)view.RestRenderer.transform.localPosition, Is.EqualTo(next.DepositAnchor));
            Assert.That(first, Is.Not.EqualTo(next.DepositAnchor), "the two shipped layouts place the vein differently");
        }

        [Test]
        public void TheRestSpriteFollowsTheStage()
        {
            var view = NewView(Layout("3x4"), out _);
            for (var stage = 0; stage < WorldCompositionSettings.StageCount; stage++)
            {
                view.SetStage(stage);
                view.Present(false, 0, false);
                Assert.That(view.RestRenderer.sprite.name, Is.EqualTo("F006-DEPOSIT-REST-S" + (stage + 1)));
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Mining state
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheChipLoopPlaysWhileMiningIsInProgressOnTheSharedClockAndStopsWithIt()
        {
            var view = NewView(Layout("3x4"), out _);
            view.Present(false, 0, false);
            Assert.That(view.Mode, Is.EqualTo(DepositMode.Rest));
            Assert.That(view.ChipRenderer.enabled, Is.False, "no mining, no chip");

            var frames = new System.Collections.Generic.HashSet<int>();
            for (long t = 0; t < 1500; t += 16)
            {
                view.Present(true, t, false);
                Assert.That(view.Mode, Is.EqualTo(DepositMode.Chip));
                Assert.That(view.ChipRenderer.enabled, Is.True);
                Assert.That(view.RestRenderer.sprite.name, Is.EqualTo("F006-DEPOSIT-REST-S1"), "the chip loop plays over the rest vein");
                Assert.That(view.ChipRenderer.sprite.name, Does.StartWith(DepotLinkGeometry.VeinChipKey));
                Assert.That(view.ChipFrame, Is.EqualTo(PresentationAnimationClock.FrameAt(t, 8f, 6)), "floor(t * fps) mod 6 on the shared clock");
                frames.Add(view.ChipFrame);
            }

            Assert.That(frames, Has.Count.EqualTo(6), "all 6 frames of the chip loop play");
            Assert.That(view.ChipRenderer.sortingLayerName, Is.EqualTo(DepositView.EffectsSortingLayer));

            view.Present(false, 1600, false);
            Assert.That(view.Mode, Is.EqualTo(DepositMode.Rest), "mining ended: back to rest in the same frame");
            Assert.That(view.ChipRenderer.enabled, Is.False);
            Assert.That(view.ChipFrame, Is.Zero);
        }

        [Test]
        public void UnderReducedMotionAStaticHighlightReplacesTheChipLoop()
        {
            var view = NewView(Layout("3x4"), out _);
            for (long t = 0; t < 2000; t += 16)
            {
                view.Present(true, t, true);
                Assert.That(view.Mode, Is.EqualTo(DepositMode.Highlight));
                Assert.That(view.ChipRenderer.enabled, Is.False, "no chip loop under reduced motion at " + t);
                Assert.That(view.RestRenderer.sprite.name, Is.EqualTo(DepotLinkGeometry.DepositHighlightKey), "the static highlight shows instead");
                Assert.That(view.ChipFrame, Is.Zero);
            }

            view.Present(false, 2100, true);
            Assert.That(view.Mode, Is.EqualTo(DepositMode.Rest));
            Assert.That(view.RestRenderer.sprite.name, Is.EqualTo("F006-DEPOSIT-REST-S1"), "without mining the rest vein shows, also under reduced motion");
        }

        [Test]
        public void TheVeinChangesNoStateAndTheModeIsAFunctionOfTheInputsAlone()
        {
            var view = NewView(Layout("3x4"), out _);
            foreach (var mining in new[] { false, true })
            {
                foreach (var reduced in new[] { false, true })
                {
                    view.Present(mining, 123, reduced);
                    var expected = !mining ? DepositMode.Rest : reduced ? DepositMode.Highlight : DepositMode.Chip;
                    Assert.That(view.Mode, Is.EqualTo(expected), "mining " + mining + " reduced " + reduced);
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Not a hit target
        // ---------------------------------------------------------------------------------------------------

        [TestCaseSource(nameof(LayoutNames))]
        public void ATapOnTheVeinSelectsNothing(string name)
        {
            var layout = Layout(name);
            var view = NewView(layout, out var grid);
            view.Present(true, 0, false);
            var markerHost = new GameObject("markers");
            rig.Owned.Add(markerHost);
            var markers = markerHost.AddComponent<AttentionMarkerView>();
            markers.Configure(catalog, id => null);

            Assert.That(view.GetComponentsInChildren<Collider2D>(true), Is.Empty, "the vein has no collider");
            Assert.That(view.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(typeof(DepositView).GetMethods().Where(m => m.Name.Contains("Hit") || m.Name.Contains("Tap") || m.Name.Contains("Click")), Is.Empty, "the view has no hit test");

            var cell = layout.DeckTileRect(layout.DepositCell);
            var rect = BoundsRect(view.RestRenderer);
            var taps = 0;
            for (var x = rect.xMin; x <= rect.xMax; x += 2 / (float)SlotGridLayout.Density)
            {
                for (var y = rect.yMin; y <= rect.yMax; y += 2 / (float)SlotGridLayout.Density)
                {
                    var world = new Vector2(x, y);
                    taps++;
                    Assert.That(grid.TryHitSlot(world, out _), Is.False, name + ": no machine under the vein at " + world);
                    Assert.That(layout.TryHit(world, out _), Is.False);
                    Assert.That(WorldTapResolver.Resolve(layout, markers, world, out _), Is.EqualTo(WorldTapSource.None), name + ": the tap resolves to empty world at " + world);
                    Assert.That(Physics2D.OverlapPoint(world), Is.Null);
                }
            }

            Assert.That(taps, Is.GreaterThan(100), "the whole vein canvas was sampled");
            Assert.That(cell.Contains(new Vector2(rect.center.x, rect.center.y)), Is.True);
        }
    }
}
