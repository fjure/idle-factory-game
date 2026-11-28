using System.Collections.Generic;
using IdleFactory.Run.Application;
using IdleFactory.Tests.PlayMode.F006;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.PlayMode.F007
{
    /// <summary>
    /// T610 (F007, UX-4, research R19 amendment, contracts/look-profile.md rule 7): art may exceed the location cell, and a tap still resolves through
    /// <see cref="WorldTapResolver"/> on the location hit shapes. On the dense layout (the 12 shipped locations at the F007 dense pitch, with and without bays)
    /// and for each required screen class at 100 % and 200 % text, the real slot view and the integer camera model, at the scale the model chooses, prove:
    /// <list type="bullet">
    /// <item><description>a tap on an opaque pixel of a machine (the plate diamond inside its footprint, which every machine paints) selects that machine, through the
    /// screen-pixel round trip of the camera, and a tap on a cell corner outside the diamond selects nothing;</description></item>
    /// <item><description>the selection frame draws above every machine, and its footprint stays inside the visible rectangle (above the open card on phoneP-card);</description></item>
    /// <item><description>the hit shapes are disjoint, their separation measured in points at <c>n_min</c> is not negative, and each is at least 44 pt;</description></item>
    /// <item><description>the cue anchor of each machine, also inside a bay, maps back to its owner and never to a neighbour.</description></item>
    /// </list>
    /// What the test does not claim: a tap on a pixel of a front machine's art that lies over a <i>neighbour's</i> hit shape resolves to that neighbour (art above the
    /// footprint never captures a tap, F004 T456). The depth of that overlap is measured in points at <c>n_min</c> and written to the test log (the last test), because the
    /// contract rule needs an owner decision on overhang art, not a guess in the resolver.
    /// </summary>
    public sealed class OverhangTapTests
    {
        private const int Art = SlotGridLayout.Density;

        // The screen classes of research R09 (FR-011, FR-011a). The unobstructed rectangle is modelled from the HUD bands and the card in points, scaled with the text
        // scale; the real shell geometry is the F006 BaselineMatrixTests' subject, and this test needs only the rectangle the camera model fits into.
        public readonly struct ScreenClass
        {
            public ScreenClass(string name, int width, int height, float pixelsPerPoint, bool card)
            {
                Name = name;
                Width = width;
                Height = height;
                PixelsPerPoint = pixelsPerPoint;
                Card = card;
            }

            public string Name { get; }
            public int Width { get; }
            public int Height { get; }
            public float PixelsPerPoint { get; }
            public bool Card { get; }
            public bool Landscape => Width > Height;

            public override string ToString() => Name;
        }

        private static readonly ScreenClass[] Classes =
        {
            new ScreenClass("emuP", 1080, 2400, 2.625f, false),
            new ScreenClass("emuL", 2400, 1080, 2.625f, false),
            new ScreenClass("tabL", 2560, 1600, 2f, false),
            new ScreenClass("phoneP-card", 720, 1280, 2f, true),
            new ScreenClass("phoneL640", 640, 360, 1f, false),
        };

        public static IEnumerable<TestCaseData> Cases()
        {
            foreach (var screen in Classes)
            {
                foreach (var text in new[] { 100, 200 })
                {
                    yield return new TestCaseData(screen, text, false).SetName($"{screen.Name}_text{text}_dense");
                    yield return new TestCaseData(screen, text, true).SetName($"{screen.Name}_text{text}_dense_bays");
                }
            }
        }

        private readonly DeckTestRig rig = new DeckTestRig();

        [TearDown]
        public void TearDown() => rig.Dispose();

        private static FactoryWorldLayout DenseLayout(ScreenClass screen, bool bays) =>
            DenseLayouts.Shipped(screen.Landscape ? 4 : 3, bays ? DenseLayouts.DenseBays : DenseLayouts.Dense);

        private static float TopBand(ScreenClass screen, int text) => Mathf.Round(48f * (text / 100f) * screen.PixelsPerPoint);

        // The rectangle the world may use (between the HUD bands, or above the open card), bottom-left origin, in physical pixels.
        private static Rect Visible(ScreenClass screen, int text)
        {
            var bottom = screen.Card
                ? Mathf.Min(screen.Height * 0.5f, Mathf.Round(280f * (text / 100f) * screen.PixelsPerPoint))
                : Mathf.Round(56f * (text / 100f) * screen.PixelsPerPoint);
            return new Rect(0f, bottom, screen.Width, Mathf.Max(1f, screen.Height - TopBand(screen, text) - bottom));
        }

        // The camera of a class: fit-all into the rectangle between the HUD bands. With a card the fit is made for the closed card (the card then opens over it).
        private static CameraScaleModel Fitted(ScreenClass screen, int text, FactoryWorldLayout layout, out Rect fitRect)
        {
            fitRect = screen.Card
                ? new Rect(0f, 0f, screen.Width, screen.Height - TopBand(screen, text))
                : Visible(screen, text);
            var model = new CameraScaleModel(new RunZoomSettings().ToScaleSettings());
            model.Fit(ScaleLayout.WithView(screen.Width, screen.Height, screen.PixelsPerPoint, fitRect, layout));
            return model;
        }

        private static bool OnScreen(Vector2 screen, ScreenClass c) => screen.x >= 0f && screen.y >= 0f && screen.x <= c.Width && screen.y <= c.Height;

        // The world point that a finger lands on: the world point rounded to a whole device pixel and back through the camera, as the input path does.
        private static Vector2 Tapped(CameraScaleModel model, Vector2 world, ScreenClass c, out bool onScreen)
        {
            var screen = model.WorldToScreen(world, model.Position, model.Scale);
            onScreen = OnScreen(screen, c);
            return model.ScreenToWorld(new Vector2(Mathf.Round(screen.x), Mathf.Round(screen.y)), model.Position, model.Scale);
        }

        private static IEnumerable<Vector2> OpaqueFootprintPoints(Rect footprint)
        {
            // The plate diamond of the footprint (70 x 35 art px): its centre, four points halfway to the apexes and four points inside the quadrants.
            var centre = footprint.center;
            var halfWidth = footprint.width * 0.5f;
            var halfHeight = footprint.height * 0.5f;
            yield return centre;
            yield return centre + new Vector2(halfWidth * 0.5f, 0f);
            yield return centre - new Vector2(halfWidth * 0.5f, 0f);
            yield return centre + new Vector2(0f, halfHeight * 0.5f);
            yield return centre - new Vector2(0f, halfHeight * 0.5f);
            yield return centre + new Vector2(halfWidth * 0.25f, halfHeight * 0.25f);
            yield return centre + new Vector2(-halfWidth * 0.25f, halfHeight * 0.25f);
            yield return centre + new Vector2(halfWidth * 0.25f, -halfHeight * 0.25f);
            yield return centre + new Vector2(-halfWidth * 0.25f, -halfHeight * 0.25f);
        }

        private static float Points(float world, int scale, int pixelsPerUnit, float pixelsPerPoint) => world * scale * pixelsPerUnit / pixelsPerPoint;

        // ---------------------------------------------------------------------------------------------- the tap

        [TestCaseSource(nameof(Cases))]
        public void ATapOnAnOpaquePixelOfAMachineSelectsThatMachineAndACellCornerSelectsNothing(ScreenClass screen, int text, bool bays)
        {
            var layout = DenseLayout(screen, bays);
            var model = Fitted(screen, text, layout, out _);
            var tested = 0;
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var id = new BuildingLocationId(i);
                foreach (var point in OpaqueFootprintPoints(layout.FootprintOf(id)))
                {
                    var tap = Tapped(model, point, screen, out var onScreen);
                    if (!onScreen)
                    {
                        Assert.That(model.BelowFloor, Is.True, $"{screen.Name} {text} %: location {i} is off screen although the camera is not below its readable floor");
                        continue;
                    }

                    var source = WorldTapResolver.Resolve(layout, null, tap, out var target);
                    Assert.That(source, Is.EqualTo(WorldTapSource.Machine), $"{screen.Name} {text} %: tap on the footprint of {i} at {point}");
                    Assert.That(target, Is.EqualTo(id), $"{screen.Name} {text} %: the tap selects its own machine");
                    tested++;
                }

                // The four corners of the lattice cell lie outside the hit diamond: nothing there.
                var cell = layout.LocationCellRectOf(id);
                var inset = 1f / Art;
                var corners = new[]
                {
                    new Vector2(cell.xMin + inset, cell.yMin + inset), new Vector2(cell.xMax - inset, cell.yMax - inset),
                    new Vector2(cell.xMin + inset, cell.yMax - inset), new Vector2(cell.xMax - inset, cell.yMin + inset),
                };
                foreach (var corner in corners)
                {
                    var tap = Tapped(model, corner, screen, out var onScreen);
                    if (onScreen)
                    {
                        Assert.That(WorldTapResolver.Resolve(layout, null, tap, out _), Is.EqualTo(WorldTapSource.None), $"{screen.Name} {text} %: the corner {corner} of cell {i} taps nothing");
                    }
                }
            }

            Assert.That(tested, Is.GreaterThan(0), screen.Name + ": at least one machine was on screen");
            if (!model.BelowFloor)
            {
                Assert.That(tested, Is.EqualTo(layout.LocationCount * 9), "every machine is on screen when the camera fits the factory");
            }
        }

        // ---------------------------------------------------------------------------------------------- the selection frame

        private static SlotVisual[] Visuals(int count, int selected)
        {
            var visuals = new SlotVisual[count];
            for (var i = 0; i < count; i++)
            {
                visuals[i] = new SlotVisual(
                    i, SlotCondition.Occupied, SlotGridModel.SlotOccupiedKey, DeckTestRig.MinerKey, SlotGridModel.BadgeKeyOf(MachineVisualState.Running),
                    MachineVisualState.Running.ToString(), false, false, i == selected, 0, "Slot " + (i + 1));
            }

            return visuals;
        }

        [TestCaseSource(nameof(Cases))]
        public void TheSelectionFrameStaysVisibleAboveEveryMachineAndInsideTheVisibleRectangle(ScreenClass screen, int text, bool bays)
        {
            var layout = DenseLayout(screen, bays);
            var model = Fitted(screen, text, layout, out var fitRect);
            var cardBand = Visible(screen, text);
            var grid = rig.NewGrid(rig.Catalog(), layout);
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var id = new BuildingLocationId(i);
                if (screen.Card)
                {
                    // The card opens: the camera keeps the selected machine inside the band above it.
                    model.OpenSheet(cardBand, layout.FootprintOf(id));
                }

                grid.SetProjectedPixelsPerCell(model.Scale * (float)Art);
                grid.Render(Visuals(layout.LocationCount, i));
                var frame = grid.RendererOf(i, SlotGridView.SlotRendererRole.Frame);
                Assert.That(frame, Is.Not.Null, $"location {i} has a frame renderer");
                Assert.That(frame.enabled && frame.sprite != null, Is.True, $"{screen.Name} {text} %: the frame of the selected location {i} is drawn");
                for (var j = 0; j < layout.LocationCount; j++)
                {
                    var machine = grid.RendererOf(j, SlotGridView.SlotRendererRole.Machine);
                    Assert.That(machine.sortingOrder, Is.LessThan(frame.sortingOrder), $"the frame of {i} draws above the machine art of {j}");
                }

                // Its footprint lies inside the visible rectangle at the camera's scale. Below the readable floor the factory itself does not fit: nothing to claim there.
                if (screen.Card || !model.BelowFloor)
                {
                    var footprint = layout.FootprintOf(id);
                    var low = model.WorldToScreen(footprint.min, model.Position, model.Scale);
                    var high = model.WorldToScreen(footprint.max, model.Position, model.Scale);
                    var area = screen.Card ? cardBand : fitRect;
                    Assert.That(low.x, Is.GreaterThanOrEqualTo(area.xMin - 1.1f), $"{screen.Name} {text} %: footprint {i} left");
                    Assert.That(high.x, Is.LessThanOrEqualTo(area.xMax + 1.1f), $"{screen.Name} {text} %: footprint {i} right");
                    Assert.That(low.y, Is.GreaterThanOrEqualTo(area.yMin - 1.1f), $"{screen.Name} {text} %: footprint {i} bottom");
                    Assert.That(high.y, Is.LessThanOrEqualTo(area.yMax + 1.1f), $"{screen.Name} {text} %: footprint {i} top");
                }

                if (screen.Card)
                {
                    model.CloseSheet(fitRect);
                }
            }
        }

        // ---------------------------------------------------------------------------------------------- separation in points at n_min

        [TestCaseSource(nameof(Cases))]
        public void TheHitShapesAreDisjointAndTheirSeparationAtNMinIsMeasuredInPoints(ScreenClass screen, int text, bool bays)
        {
            var layout = DenseLayout(screen, bays);
            var settings = new RunZoomSettings().ToScaleSettings();
            var nMin = CameraScaleModel.MinScale(settings, screen.PixelsPerPoint);
            var ppu = settings.ArtPixelsPerCell;

            var heightPoints = Points(layout.HitHeightWorld, nMin, ppu, screen.PixelsPerPoint);
            Assert.That(heightPoints, Is.GreaterThanOrEqualTo(layout.Pitch.MinHitPoints), $"{screen.Name}: the hit height at n_min = {nMin} is {heightPoints:0.0} pt");
            var widthPoints = Points(Mathf.Min(layout.CellWidth, SlotGridLayout.HitWidthPixels / (float)Art), nMin, ppu, screen.PixelsPerPoint);
            Assert.That(widthPoints, Is.GreaterThanOrEqualTo(layout.Pitch.MinHitPoints), $"{screen.Name}: the hit width at n_min is {widthPoints:0.0} pt");

            var smallest = float.MaxValue;
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var a = layout.HitShapeOf(new BuildingLocationId(i));
                for (var j = i + 1; j < layout.LocationCount; j++)
                {
                    var b = layout.HitShapeOf(new BuildingLocationId(j));
                    Assert.That(FactoryWorldLayout.Overlaps(a.Cell, b.Cell), Is.False, $"{screen.Name}: the cells of {i} and {j} overlap");

                    // The separation of two diamonds clipped to disjoint cells, along the axis of their neighbourhood: the gap between the clipped extents.
                    var dx = Mathf.Abs(a.Centre.x - b.Centre.x);
                    var dy = Mathf.Abs(a.Centre.y - b.Centre.y);
                    float gap;
                    if (dx < 1e-4f)
                    {
                        gap = dy - 2f * Mathf.Min(a.HalfHeight, layout.CellHeight * 0.5f);
                    }
                    else if (dy < 1e-4f)
                    {
                        gap = dx - 2f * Mathf.Min(a.HalfWidth, layout.CellWidth * 0.5f);
                    }
                    else
                    {
                        continue;
                    }

                    Assert.That(gap, Is.GreaterThanOrEqualTo(-1e-4f), $"{screen.Name}: the hit shapes of {i} and {j} overlap");
                    smallest = Mathf.Min(smallest, Points(gap, nMin, ppu, screen.PixelsPerPoint));
                }
            }

            TestContext.WriteLine($"{screen.Name} text {text} % {(bays ? "bays" : "plain")}: n_min {nMin}, hit {widthPoints:0.0} x {heightPoints:0.0} pt, smallest hit-shape separation {smallest:0.00} pt");
            Assert.That(smallest, Is.GreaterThanOrEqualTo(-1e-3f), "touching shapes have a separation of 0 pt (float rounding aside), never a negative one");
        }

        // ---------------------------------------------------------------------------------------------- cue anchors

        [TestCaseSource(nameof(Cases))]
        public void TheCueAnchorOfEveryMachineMapsToItsOwnerAlsoInsideABay(ScreenClass screen, int text, bool bays)
        {
            var layout = DenseLayout(screen, bays);
            var model = Fitted(screen, text, layout, out _);
            var groups = new HashSet<int>();
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var id = new BuildingLocationId(i);
                var anchor = layout.AnchorOf(id) + StateCueGeometry.BadgeAnchorOffset;
                Assert.That(layout.TryCellOwner(anchor, out var owner) && owner == id, Is.True, $"{screen.Name}: the badge anchor of {i} maps to {i}");
                var tapped = Tapped(model, anchor, screen, out var onScreen);
                if (onScreen)
                {
                    Assert.That(layout.TryCellOwner(tapped, out owner) && owner == id, Is.True, $"{screen.Name}: also after the pixel round trip of the camera");
                }

                groups.Add(layout.GroupOf(id));
            }

            Assert.That(groups.Count, Is.EqualTo(bays ? (layout.ColumnCount == 3 ? 8 : 6) : layout.LocationCount), "bays pair side-by-side machines; each keeps its own anchor");
        }

        // ---------------------------------------------------------------------------------------------- the overhang measurement

        [Test]
        public void TheDepthOfAFrontMachinesArtOverANeighboursHitShapeIsMeasuredInPointsAtNMin()
        {
            // The overhang the contract allows: the machine canvas reaches 49 art px above its pivot. At the F006 pitch the machine behind already starts its hit diamond
            // 34 px above the front pivot; at the dense pitch 26 px. That art is drawn over the neighbour's hit shape, and a tap there selects the neighbour (art above the
            // footprint never captures a tap). The numbers are the input of the owner decision on overhang art; the test fails only when the measurement is not finite.
            const int CanvasTopAbovePivotPixels = 49;
            var lines = new List<string>();
            foreach (var pitch in new[] { LayoutPitch.F006, DenseLayouts.Dense })
            {
                var layout = DenseLayouts.Shipped(3, pitch);
                var front = new BuildingLocationId(layout.LocationCount - 1);
                var behind = new BuildingLocationId(layout.LocationCount - 1 - layout.ColumnCount);
                var frontTop = layout.AnchorOf(front).y + CanvasTopAbovePivotPixels / (float)Art;
                var behindShape = layout.HitShapeOf(behind);
                var behindBottom = Mathf.Max(behindShape.Centre.y - behindShape.HalfHeight, behindShape.Cell.yMin);
                var depthWorld = frontTop - behindBottom;
                Assert.That(float.IsNaN(depthWorld) || float.IsInfinity(depthWorld), Is.False);
                foreach (var screen in Classes)
                {
                    var settings = new RunZoomSettings().ToScaleSettings();
                    var nMin = CameraScaleModel.MinScale(settings, screen.PixelsPerPoint);
                    lines.Add($"pitch {pitch.PitchWorld.y * Art:0} px, {screen.Name}: overhang over the neighbour's hit shape {depthWorld * Art:0.0} art px = {Points(depthWorld, nMin, settings.ArtPixelsPerCell, screen.PixelsPerPoint):0.0} pt at n_min {nMin}");
                }
            }

            TestContext.WriteLine(string.Join("\n", lines));
            Assert.That(lines, Is.Not.Empty);
        }
    }
}
