using System.Collections.Generic;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T534 (P1-F2, OD-9b UI/UX C1, SC-002): the second camera extent. <see cref="FactoryWorldLayout.ViewBounds"/> adds the depot and the deposit to
    /// the content bounds; <see cref="ScaleLayout.ViewBounds"/> defaults to <see cref="ScaleLayout.Bounds"/>, so every existing layout behaves exactly
    /// as before. The model pans over the view extent always, and fits and centres over it only when that costs no integer scale.
    /// </summary>
    public sealed class ViewBoundsTests
    {
        private static readonly ArtScaleSettings Settings = new ArtScaleSettings(24, 24, 6);

        private static IEnumerable<FactoryWorldLayout> Layouts()
        {
            yield return LatticeLayoutBuilder.ShippedForColumns(3);
            yield return LatticeLayoutBuilder.ShippedForColumns(4);
            yield return Rows("six", WorldOrientation.Portrait, "###", "###");
            yield return Rows("twenty", WorldOrientation.Landscape, "####", "####", "####", "####", "####");
            yield return Rows("l-shape", WorldOrientation.Portrait, "#...", "#...", "#...", "####");
        }

        private static FactoryWorldLayout Rows(string id, WorldOrientation orientation, params string[] rows)
        {
            var cells = new List<Vector2Int>();
            for (var top = 0; top < rows.Length; top++)
            {
                for (var column = 0; column < rows[top].Length; column++)
                {
                    if (rows[top][column] == '#')
                    {
                        cells.Add(new Vector2Int(column, rows.Length - 1 - top));
                    }
                }
            }

            return new FactoryWorldLayout(id, cells, orientation);
        }

        private static bool Contains(Rect outer, Rect inner, float tolerance = 1e-4f) =>
            inner.xMin >= outer.xMin - tolerance && inner.yMin >= outer.yMin - tolerance && inner.xMax <= outer.xMax + tolerance && inner.yMax <= outer.yMax + tolerance;

        [Test]
        public void TheViewExtentOfALayoutHoldsTheContentTheDepotAndTheDeposit()
        {
            foreach (var layout in Layouts())
            {
                Assert.That(Contains(layout.ViewBounds, layout.Bounds), Is.True, layout.LayoutId + ": the content");
                Assert.That(Contains(layout.ViewBounds, layout.DepotRect), Is.True, layout.LayoutId + ": the depot");
                Assert.That(Contains(layout.ViewBounds, layout.DepositRect), Is.True, layout.LayoutId + ": the deposit");
                Assert.That(layout.ViewBounds.width, Is.GreaterThan(layout.Bounds.width), layout.LayoutId + ": they sit outside the content bounds");
            }
        }

        [Test]
        public void TheMachineRectHoldsTheFootprintAndStaysInsideTheContentBounds()
        {
            foreach (var layout in Layouts())
            {
                for (var i = 0; i < layout.LocationCount; i++)
                {
                    var id = new BuildingLocationId(i);
                    var machine = layout.MachineRectOf(id);
                    Assert.That(Contains(machine, layout.FootprintOf(id)), Is.True, layout.LayoutId + " location " + i + ": the plate is part of the machine rect");
                    Assert.That(Contains(layout.Bounds, machine), Is.True, layout.LayoutId + " location " + i + ": and the machine rect is inside the content bounds");
                    Assert.That(machine.height, Is.GreaterThan(layout.FootprintOf(id).height), layout.LayoutId + ": taller than the plate (it includes the badge band)");
                }
            }
        }

        [Test]
        public void ADefaultScaleLayoutHasNoSecondExtent()
        {
            var layout = LatticeLayoutBuilder.ShippedForColumns(3);
            var scale = new ScaleLayout(1080, 2400, 2.625f, new Rect(0f, 0f, 1080f, 2400f), layout);
            Assert.That(scale.ViewBounds, Is.EqualTo(scale.Bounds));
            var model = new CameraScaleModel(Settings);
            model.Fit(scale);
            Assert.That(model.Extent, Is.EqualTo(layout.Bounds));
            Assert.That(model.PanExtent, Is.EqualTo(layout.Bounds));
        }

        [Test]
        public void AViewExtentThatCostsNoScaleIsTheFitExtentAndTheDepotIsInView()
        {
            // A wide, short area: the content is height-limited, so the wider view extent fits at the same integer scale.
            var layout = LatticeLayoutBuilder.ShippedForColumns(4);
            var area = new Rect(0f, 0f, 4000f, 1000f);
            var plain = new CameraScaleModel(Settings);
            plain.Fit(new ScaleLayout(4000, 1000, 1f, area, layout));
            var view = new CameraScaleModel(Settings);
            view.Fit(ScaleLayout.WithView(4000, 1000, 1f, area, layout));

            Assert.That(view.Scale, Is.EqualTo(plain.Scale), "the view extent costs no integer scale");
            Assert.That(view.Extent, Is.EqualTo(layout.ViewBounds), "so it is the fit extent");
            foreach (var part in new[] { layout.Bounds, layout.DepotRect, layout.DepositRect })
            {
                var low = view.WorldToScreen(part.min, view.Position, view.Scale);
                var high = view.WorldToScreen(part.max, view.Position, view.Scale);
                Assert.That(low.x, Is.GreaterThanOrEqualTo(area.xMin - 1f), "left");
                Assert.That(high.x, Is.LessThanOrEqualTo(area.xMax + 1f), "right");
                Assert.That(low.y, Is.GreaterThanOrEqualTo(area.yMin - 1f), "bottom");
                Assert.That(high.y, Is.LessThanOrEqualTo(area.yMax + 1f), "top");
            }
        }

        [Test]
        public void AViewExtentThatCostsAScaleStepsDownToFrameTheDepotColumnWhileTheReadableFloorHolds()
        {
            // T558 (U-C1): a tall phone. The content fits by width at a larger scale than the wider view extent; fit-all now steps down to the view extent's scale
            // (the depot, the collector and the marker boxes in view) as long as that is not below the readable floor n_min.
            var layout = LatticeLayoutBuilder.ShippedForColumns(3);
            var area = new Rect(0f, 0f, 1080f, 2400f);
            var contentScale = CameraScaleModel.LargestFittingScale(area, layout.Bounds, 24, layout.Margin);
            var wholeViewScale = CameraScaleModel.LargestFittingScale(area, layout.ViewBounds, 24, layout.Margin);
            var viewScale = CameraScaleModel.LargestFittingScale(area, layout.FitBounds, 24, layout.Margin);
            var floor = CameraScaleModel.MinScale(Settings, 2.625f);
            TestContext.Out.WriteLine($"T558-FIT | 1080x2400@2.625 columns 3 | content n={contentScale} depot column n={viewScale} with the vein n={wholeViewScale} floor {floor}");
            Assert.That(wholeViewScale, Is.LessThan(contentScale), "the fixture: the whole view extent (with the vein) costs a scale");
            Assert.That(viewScale, Is.GreaterThanOrEqualTo(floor), "and the depot column still meets the readable floor");

            var view = new CameraScaleModel(Settings);
            view.Fit(ScaleLayout.WithView(1080, 2400, 2.625f, area, layout));
            Assert.That(view.Scale, Is.EqualTo(viewScale), "fit-all drops to the next integer scale that frames the depot column");
            Assert.That(view.Extent, Is.EqualTo(layout.FitBounds));
            Assert.That(view.PanExtent, Is.EqualTo(layout.ViewBounds), "the pan limits still cover the vein");
            foreach (var part in new[] { layout.Bounds, layout.DepotRect, AttentionModel.MarkerBoxAt(layout.DepotAnchor) })
            {
                var low = view.WorldToScreen(part.min, view.Position, view.Scale);
                var high = view.WorldToScreen(part.max, view.Position, view.Scale);
                Assert.That(low.x, Is.GreaterThanOrEqualTo(area.xMin - 1f), "left");
                Assert.That(high.x, Is.LessThanOrEqualTo(area.xMax + 1f), "right");
                Assert.That(low.y, Is.GreaterThanOrEqualTo(area.yMin - 1f), "bottom");
                Assert.That(high.y, Is.LessThanOrEqualTo(area.yMax + 1f), "top");
            }
        }

        [Test]
        public void AViewExtentThatWouldFallBelowTheReadableFloorFallsBackToTheContentForTheFitButStaysPannable()
        {
            // The floor is never traded for the depot: find a width at which the content fits at n_min but the depot column only below it.
            var layout = LatticeLayoutBuilder.ShippedForColumns(3);
            var floor = CameraScaleModel.MinScale(Settings, 2.625f);
            Rect area = default;
            var found = false;
            for (var width = 400f; width < 2400f && !found; width += 4f)
            {
                var candidate = new Rect(0f, 0f, width, 4000f);
                found = CameraScaleModel.LargestFittingScale(candidate, layout.Bounds, 24, layout.Margin) == floor
                    && CameraScaleModel.LargestFittingScale(candidate, layout.FitBounds, 24, layout.Margin) < floor;
                area = candidate;
            }

            Assert.That(found, Is.True, "the fixture exists");
            var plain = new CameraScaleModel(Settings);
            plain.Fit(new ScaleLayout((int)area.width, 4000, 2.625f, area, layout));
            var view = new CameraScaleModel(Settings);
            view.Fit(ScaleLayout.WithView((int)area.width, 4000, 2.625f, area, layout));

            Assert.That(view.Scale, Is.EqualTo(plain.Scale), "fit-all keeps the scale the content alone gives");
            Assert.That(view.Position, Is.EqualTo(plain.Position), "and the centring");
            Assert.That(view.Extent, Is.EqualTo(layout.Bounds));
            Assert.That(view.PanExtent, Is.EqualTo(layout.ViewBounds), "but the pan limits cover the depot and the vein");

            // The player can pan to the depot (right of the content) and to the vein (left of it).
            view.Pan(new Vector2(100f, 0f));
            var depot = view.WorldToScreen(layout.DepotRect.center, view.Position, view.Scale);
            Assert.That(depot.x, Is.InRange(0f, area.width), "panning right brings the depot into view");
            Assert.That(plain.Pan(new Vector2(100f, 0f)), Is.False, "without the view extent the content could not be panned at all at fit-all");
        }

        [Test]
        public void ASheetOpenAndCloseRestoresThePriorViewWithTheViewExtent()
        {
            foreach (var layout in Layouts())
            {
                var area = new Rect(0f, 0f, 1080f, 2400f);
                var model = new CameraScaleModel(Settings);
                model.Fit(ScaleLayout.WithView(1080, 2400, 2.625f, area, layout));
                var scale = model.Scale;
                var position = model.Position;
                var band = new Rect(0f, 800f, 1080f, 1600f);
                var machine = layout.MachineRectOf(new BuildingLocationId(layout.LocationCount - 1));

                model.OpenSheet(band, machine);
                var low = model.WorldToScreen(machine.min, model.Position, model.Scale);
                var high = model.WorldToScreen(machine.max, model.Position, model.Scale);
                Assert.That(low.y, Is.GreaterThanOrEqualTo(band.yMin - 1.5f), layout.LayoutId + ": the machine is above the card");
                Assert.That(high.y, Is.LessThanOrEqualTo(band.yMax + 1.5f), layout.LayoutId);
                Assert.That(low.x, Is.GreaterThanOrEqualTo(band.xMin - 1.5f), layout.LayoutId);
                Assert.That(high.x, Is.LessThanOrEqualTo(band.xMax + 1.5f), layout.LayoutId);

                model.CloseSheet(area);
                Assert.That(model.Scale, Is.EqualTo(scale), layout.LayoutId + ": the prior scale returns");
                Assert.That(model.Position.x, Is.EqualTo(position.x).Within(1e-3f), layout.LayoutId);
                Assert.That(model.Position.y, Is.EqualTo(position.y).Within(1e-3f), layout.LayoutId);
            }
        }

        [Test]
        public void ARotationRefitsWithTheNewViewExtent()
        {
            var layout = LatticeLayoutBuilder.ShippedForColumns(3);
            var landscape = LatticeLayoutBuilder.ShippedForColumns(4);
            var model = new CameraScaleModel(Settings);
            model.Fit(ScaleLayout.WithView(1080, 2400, 2.625f, new Rect(0f, 0f, 1080f, 2400f), layout));
            model.Fit(ScaleLayout.WithView(2400, 1080, 2.625f, new Rect(0f, 0f, 2400f, 1080f), landscape));

            Assert.That(model.PanExtent, Is.EqualTo(landscape.ViewBounds), "the new layout's view extent is in force");
            var content = landscape.Bounds;
            var low = model.WorldToScreen(content.min, model.Position, model.Scale);
            var high = model.WorldToScreen(content.max, model.Position, model.Scale);
            Assert.That(low.x, Is.GreaterThanOrEqualTo(-1f));
            Assert.That(high.x, Is.LessThanOrEqualTo(2401f));
        }
    }
}
