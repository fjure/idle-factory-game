using System;
using System.IO;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T512 (Feature 006, FR-037, SC-011, research R04): the <see cref="FactoryWorldLayout"/> seam reproduces the F004 lattice exactly. For
    /// the 12 shipped locations in 3 columns (portrait) and 4 columns (landscape), <see cref="FactoryWorldLayout.Bounds"/>, anchors,
    /// footprints, hit shapes, sorting rows, the closed-form hit test, and <see cref="CameraScaleModel.LargestFittingScale"/> per screen class
    /// equal the values <see cref="SlotGridLayout"/> gave before the migration and the values T505 exported to
    /// <c>tools/art-pipeline/f006/lattice-geometry.json</c>. The values written in this file are the F004 figures, so a change to either
    /// side fails here.
    /// </summary>
    public sealed class WorldLayoutParityTests
    {
        private const string GeometryPath = "tools/art-pipeline/f006/lattice-geometry.json";
        private const int Art = SlotGridLayout.Density;
        private const int MaxScale = 6;
        private const int Count = LatticeLayoutBuilder.ShippedLocationCount;

        private static FactoryWorldLayout Build(int columns) =>
            new LatticeLayoutBuilder(Count, columns == 4 ? WorldOrientation.Landscape : WorldOrientation.Portrait).Build();

        [TestCase(3, 210f, 216f)]
        [TestCase(4, 280f, 162f)]
        public void BoundsIsTheF004ExtentInArtPixelsAndTheMarginStaysSeparate(int columns, float widthPx, float heightPx)
        {
            var layout = Build(columns);
            Assert.That(layout.Bounds, Is.EqualTo(SlotGridLayout.Extent(columns)), "Bounds is today's Extent, bit for bit");
            Assert.That(layout.Bounds.xMin, Is.EqualTo(0f));
            Assert.That(layout.Bounds.yMin, Is.EqualTo(0f));
            Assert.That(layout.Bounds.width * Art, Is.EqualTo(widthPx).Within(1e-3f), "the F004 figure, written down");
            Assert.That(layout.Bounds.height * Art, Is.EqualTo(heightPx).Within(1e-3f), "the F004 figure, written down");
            Assert.That(layout.Margin, Is.EqualTo(SlotGridLayout.HalfCellMargin), "the fit margin is the half cell the camera model always added");
            Assert.That(layout.LocationCount, Is.EqualTo(Count));
            Assert.That(layout.LayoutId, Is.EqualTo("lattice-12x" + columns));
        }

        [TestCase(3)]
        [TestCase(4)]
        public void EveryLocationGeometryEqualsTheF004Functions(int columns)
        {
            var layout = Build(columns);
            for (var i = 0; i < Count; i++)
            {
                var id = new BuildingLocationId(i);
                Assert.That(layout.AnchorOf(id), Is.EqualTo(SlotGridLayout.PivotOf(i, columns)), "anchor " + i);
                Assert.That(layout.PlateCentreOf(id), Is.EqualTo(SlotGridLayout.PlateCentreOf(i, columns)), "plate centre " + i);
                Assert.That(layout.FootprintOf(id), Is.EqualTo(SlotGridLayout.SlotRect(i, columns)), "footprint " + i);
                Assert.That(layout.SortKeyOf(id), Is.EqualTo(SlotGridLayout.RowOf(i, columns)), "sort key " + i);
                var shape = layout.HitShapeOf(id);
                Assert.That(shape.Centre, Is.EqualTo(SlotGridLayout.PlateCentreOf(i, columns)));
                Assert.That(shape.HalfWidth * Art, Is.EqualTo(SlotGridLayout.HitWidthPixels * 0.5f).Within(1e-4f));
                Assert.That(shape.HalfHeight * Art, Is.EqualTo(SlotGridLayout.HitHeightPixels * 0.5f).Within(1e-4f));
            }
        }

        [TestCase(3)]
        [TestCase(4)]
        public void TryHitEqualsTheF004HitTestOnADensePointGrid(int columns)
        {
            var layout = Build(columns);
            var extent = SlotGridLayout.Extent(columns);
            const float step = 1f / (2f * Art);
            var points = 0;
            var hits = 0;
            for (var y = extent.yMin - 1f; y <= extent.yMax + 1f; y += step)
            {
                for (var x = extent.xMin - 1f; x <= extent.xMax + 1f; x += step)
                {
                    var world = new Vector2(x, y);
                    var expected = SlotGridLayout.HitTest(world, columns);
                    var found = layout.TryHit(world, out var id);
                    points++;
                    if (found != (expected >= 0) || (found && id.Value != expected))
                    {
                        Assert.Fail($"({x}, {y}): F004 {expected}, layout {(found ? id.Value : -1)}");
                    }

                    hits += found ? 1 : 0;
                }
            }

            Assert.That(points, Is.GreaterThan(100000));
            Assert.That(hits, Is.GreaterThan(points / 10), "a real share of the points hits a location");
        }

        [Test]
        public void TheShippedLayoutsAreCachedAndUseTheScreenAspect()
        {
            Assert.That(LatticeLayoutBuilder.Shipped(WorldOrientation.Portrait), Is.SameAs(LatticeLayoutBuilder.Shipped(360f, 640f)));
            Assert.That(LatticeLayoutBuilder.Shipped(WorldOrientation.Landscape), Is.SameAs(LatticeLayoutBuilder.Shipped(640f, 360f)));
            Assert.That(LatticeLayoutBuilder.Shipped(800f, 800f).Orientation, Is.EqualTo(WorldOrientation.Portrait), "a square screen is portrait, as in SlotGridLayout.ColumnsFor");
            Assert.That(LatticeLayoutBuilder.ColumnsFor(WorldOrientation.Portrait), Is.EqualTo(SlotGridLayout.ColumnsFor(360f, 640f)));
            Assert.That(LatticeLayoutBuilder.ColumnsFor(WorldOrientation.Landscape), Is.EqualTo(SlotGridLayout.ColumnsFor(640f, 360f)));
        }

        // ------------------------------------------------------------------------------------------------ the T505 export

        [Serializable]
        private sealed class DiamondJson
        {
            public float[] centre;
            public float halfWidth;
            public float halfHeight;
        }

        [Serializable]
        private sealed class SlotJson
        {
            public int index;
            public float[] pivotPx;
            public float[] plateRectPx;
            public DiamondJson hitDiamondPx;
        }

        [Serializable]
        private sealed class LayoutJson
        {
            public int columns;
            public int rows;
            public float[] extentPx;
            public SlotJson[] slots;
        }

        [Serializable]
        private sealed class ScreenJson
        {
            public string name;
            public int columns;
            public float[] unobstructedPx;
            public int nMin;
            public int nFit;
        }

        [Serializable]
        private sealed class GeometryJson
        {
            public int slotCount;
            public LayoutJson[] layouts;
            public ScreenJson[] pointScreens;
            public ScreenJson[] devices;
        }

        private static GeometryJson LoadGeometry()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", GeometryPath));
            Assert.That(File.Exists(path), Is.True, GeometryPath + " exists (T505)");
            var geometry = JsonUtility.FromJson<GeometryJson>(File.ReadAllText(path));
            Assert.That(geometry, Is.Not.Null);
            return geometry;
        }

        [Test]
        public void TheLayoutMatchesTheT505LatticeGeometryJson()
        {
            var geometry = LoadGeometry();
            Assert.That(geometry.slotCount, Is.EqualTo(Count));
            Assert.That(geometry.layouts, Has.Length.EqualTo(2));
            foreach (var expected in geometry.layouts)
            {
                var layout = Build(expected.columns);
                Assert.That(layout.RowCount, Is.EqualTo(expected.rows), "rows at " + expected.columns + " columns");
                Assert.That(layout.Bounds.width * Art, Is.EqualTo(expected.extentPx[0]).Within(0.01f), "extent width at " + expected.columns);
                Assert.That(layout.Bounds.height * Art, Is.EqualTo(expected.extentPx[1]).Within(0.01f), "extent height at " + expected.columns);
                Assert.That(expected.slots, Has.Length.EqualTo(Count));
                foreach (var slot in expected.slots)
                {
                    var id = new BuildingLocationId(slot.index);
                    var anchor = layout.AnchorOf(id) * Art;
                    Assert.That(anchor.x, Is.EqualTo(slot.pivotPx[0]).Within(0.01f), $"pivot x of {slot.index} at {expected.columns}");
                    Assert.That(anchor.y, Is.EqualTo(slot.pivotPx[1]).Within(0.01f), $"pivot y of {slot.index} at {expected.columns}");
                    var plate = layout.FootprintOf(id);
                    Assert.That(plate.x * Art, Is.EqualTo(slot.plateRectPx[0]).Within(0.01f), $"plate x of {slot.index}");
                    Assert.That(plate.y * Art, Is.EqualTo(slot.plateRectPx[1]).Within(0.01f), $"plate y of {slot.index}");
                    Assert.That(plate.width * Art, Is.EqualTo(slot.plateRectPx[2]).Within(0.01f), $"plate width of {slot.index}");
                    Assert.That(plate.height * Art, Is.EqualTo(slot.plateRectPx[3]).Within(0.01f), $"plate height of {slot.index}");
                    var shape = layout.HitShapeOf(id);
                    Assert.That(shape.Centre.x * Art, Is.EqualTo(slot.hitDiamondPx.centre[0]).Within(0.01f), $"hit centre x of {slot.index}");
                    Assert.That(shape.Centre.y * Art, Is.EqualTo(slot.hitDiamondPx.centre[1]).Within(0.01f), $"hit centre y of {slot.index}");
                    Assert.That(shape.HalfWidth * Art, Is.EqualTo(slot.hitDiamondPx.halfWidth).Within(0.01f), $"hit half width of {slot.index}");
                    Assert.That(shape.HalfHeight * Art, Is.EqualTo(slot.hitDiamondPx.halfHeight).Within(0.01f), $"hit half height of {slot.index}");
                }
            }
        }

        [Test]
        public void LargestFittingScaleMatchesTheT505FitPerScreenClass()
        {
            var geometry = LoadGeometry();
            var screens = 0;
            foreach (var group in new[] { geometry.pointScreens, geometry.devices })
            {
                Assert.That(group, Is.Not.Null.And.Not.Empty);
                foreach (var screen in group)
                {
                    var layout = Build(screen.columns);
                    var rect = new Rect(screen.unobstructedPx[0], screen.unobstructedPx[1], screen.unobstructedPx[2], screen.unobstructedPx[3]);
                    var largest = CameraScaleModel.LargestFittingScale(rect, layout.Bounds, Art, layout.Margin);
                    Assert.That(Math.Min(largest, MaxScale), Is.EqualTo(screen.nFit), screen.name + ": n_fit through the layout seam");
                    Assert.That(largest, Is.EqualTo(CameraScaleModel.LargestFittingScale(rect, layout.Bounds, Art)), screen.name + ": the default margin is the layout margin");
                    screens++;
                }
            }

            Assert.That(screens, Is.GreaterThanOrEqualTo(20), "every screen class of the table is covered");
        }

        [Test]
        public void TheScaleModelFitsAndClampsThroughTheLayoutLikeTheF004ColumnPath()
        {
            foreach (var columns in new[] { 3, 4 })
            {
                var layout = Build(columns);
                var rect = new Rect(0f, 0f, 1200f, 1200f);
                var model = new CameraScaleModel(new ArtScaleSettings(Art, 24, MaxScale), null);
                model.Fit(new ScaleLayout(1200, 1200, 1f, rect, layout));
                var expected = Math.Min(CameraScaleModel.LargestFittingScale(rect, SlotGridLayout.Extent(columns), Art, SlotGridLayout.HalfCellMargin), MaxScale);
                Assert.That(model.FitScale, Is.EqualTo(expected), "columns " + columns);
                Assert.That(model.Layout.LayoutId, Is.EqualTo(layout.LayoutId));
                Assert.That(model.Layout.Bounds, Is.EqualTo(SlotGridLayout.Extent(columns)));
                Assert.That(model.Layout.Margin, Is.EqualTo(SlotGridLayout.HalfCellMargin));
            }
        }
    }
}
