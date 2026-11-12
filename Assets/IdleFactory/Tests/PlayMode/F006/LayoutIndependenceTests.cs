using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T512 (Feature 006, FR-037, FR-038, SC-011, contracts/world-presentation.md section 4): the real slot view, the real camera
    /// controller, the hit test and the selection frame all consume <see cref="FactoryWorldLayout"/>, so a layout of 6, 20 or an L shape of
    /// 7 locations works without any code change and without a 12-slot assumption: the view builds one slot per location at the layout's
    /// anchors and sort rows, a tap resolves to the right id and to nothing on an empty lattice cell, fit-all and the pan limits follow the
    /// layout's bounds and margin, and a sheet keeps the selected location in view.
    /// </summary>
    public sealed class LayoutIndependenceTests
    {
        private const int Art = SlotGridLayout.Density;
        private const int Width = 1280;
        private const int Height = 960;

        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            for (var i = owned.Count - 1; i >= 0; i--)
            {
                var item = owned[i];
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            owned.Clear();
        }

        private SlotGridView NewView(FactoryWorldLayout layout)
        {
            var host = new GameObject("layout-independence-grid");
            owned.Add(host);
            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(new PresentationSpriteEntry[0]);
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            var view = host.AddComponent<SlotGridView>();
            view.SetLayout(layout);
            view.Configure(catalog);
            return view;
        }

        private static SlotVisual[] Visuals(int count, int selected = -1) =>
            Enumerable.Range(0, count)
                .Select(i => new SlotVisual(i, SlotCondition.Empty, SlotGridModel.SlotEmptyKey, null, null, null, false, false, i == selected, 0, "Location " + (i + 1)))
                .ToArray();

        private FactoryCameraController NewController(out Camera camera)
        {
            var texture = new RenderTexture(Width, Height, 16);
            owned.Add(texture);
            var gameObject = new GameObject("layout-independence-camera");
            owned.Add(gameObject);
            gameObject.transform.position = new Vector3(0f, 0f, -10f);
            camera = gameObject.AddComponent<Camera>();
            camera.targetTexture = texture;
            var controller = gameObject.AddComponent<FactoryCameraController>();
            controller.Configure(camera, new RunZoomSettings().ToScaleSettings());
            return controller;
        }

        private static (Vector2 Low, Vector2 High) ScreenRectOf(Camera camera, Rect world)
        {
            var low = camera.WorldToScreenPoint(new Vector3(world.xMin, world.yMin, 0f));
            var high = camera.WorldToScreenPoint(new Vector3(world.xMax, world.yMax, 0f));
            return (new Vector2(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y)), new Vector2(Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y)));
        }

        // ------------------------------------------------------------------------------------------------ the real view

        [Test]
        public void TheViewBuildsOneSlotPerLocationAtTheLayoutAnchors([ValueSource(typeof(TestLayouts), nameof(TestLayouts.Names))] string name)
        {
            var layout = TestLayouts.ByName(name);
            var view = NewView(layout);
            Assert.That(view.Layout, Is.SameAs(layout));
            view.Render(Visuals(layout.LocationCount));

            Assert.That(view.RenderersOf(layout.LocationCount - 1), Is.Not.Empty, "the last location has a slot");
            Assert.That(view.RenderersOf(layout.LocationCount), Is.Empty, "no slot exists beyond the layout (the view is not fixed at 12)");
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var id = new BuildingLocationId(i);
                var slot = view.transform.Find("Slot " + (i + 1));
                Assert.That(slot, Is.Not.Null, "slot object " + i);
                Assert.That((Vector2)slot.localPosition, Is.EqualTo(layout.AnchorOf(id)), "the slot sits on the layout anchor " + i);
                Assert.That(view.FootprintPivotOf(i), Is.EqualTo(layout.AnchorOf(id)));
                Assert.That(view.WorldRectOf(i), Is.EqualTo(layout.FootprintOf(id)));
                Assert.That(view.RowOf(i), Is.EqualTo(layout.SortKeyOf(id)));
                Assert.That(view.RendererOf(i, SlotGridView.SlotRendererRole.Frame).sortingOrder, Is.EqualTo(SlotGridView.FrameOrder + layout.SortKeyOf(id)), "the sort row follows the layout");
            }
        }

        [Test]
        public void ATapResolvesToTheRightLocationAndAnEmptyCellOrTheMarginTapsNothing([ValueSource(typeof(TestLayouts), nameof(TestLayouts.Names))] string name)
        {
            var layout = TestLayouts.ByName(name);
            var view = NewView(layout);
            view.Render(Visuals(layout.LocationCount));
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var centre = view.WorldRectOf(i).center;
                Assert.That(view.TryHitSlot(centre, out var hit), Is.True, "tap on location " + i);
                Assert.That(hit, Is.EqualTo(i));
                foreach (var apex in new[] { new Vector2(34f, 0f), new Vector2(-34f, 0f), new Vector2(0f, 17f), new Vector2(0f, -17f) })
                {
                    Assert.That(view.TryHitSlot(centre + apex / Art, out hit) && hit == i, Is.True, $"location {i} apex {apex}");
                }
            }

            Assert.That(view.TryHitSlot(FactoryWorldLayout.PlateCentreOfCell(layout.DepotCell), out _), Is.False, "the depot cell is not a tap target");
            Assert.That(view.TryHitSlot(FactoryWorldLayout.PlateCentreOfCell(layout.DepositCell), out _), Is.False, "the deposit cell is not a tap target");
            Assert.That(view.TryHitSlot(new Vector2(-50f, -50f), out _), Is.False);
            if (name == TestLayouts.LName)
            {
                // The L shape leaves the upper right of its bounding grid empty: a tap there hits nothing.
                Assert.That(view.TryHitSlot(FactoryWorldLayout.PlateCentreOfCell(new Vector2Int(2, 3)), out _), Is.False, "an empty lattice cell inside the bounds");
            }
        }

        [Test]
        public void TheSelectionFrameFollowsATapOnAnyLayout([ValueSource(typeof(TestLayouts), nameof(TestLayouts.Names))] string name)
        {
            var layout = TestLayouts.ByName(name);
            var view = NewView(layout);
            var selected = layout.LocationCount - 1;
            Assert.That(view.TryHitSlot(view.WorldRectOf(selected).center, out var tapped), Is.True);
            view.Render(Visuals(layout.LocationCount, tapped));
            Assert.That(view.Visuals[tapped].Selected, Is.True);
            Assert.That(view.Visuals.Count(visual => visual.Selected), Is.EqualTo(1));
            Assert.That(view.Visuals.Count, Is.EqualTo(layout.LocationCount));
            var frame = view.RendererOf(tapped, SlotGridView.SlotRendererRole.Frame);
            Assert.That(frame, Is.Not.Null);
            for (var i = 0; i < layout.LocationCount; i++)
            {
                if (i != tapped)
                {
                    Assert.That(view.RendererOf(i, SlotGridView.SlotRendererRole.Frame).enabled, Is.False, "only the tapped location shows a frame");
                }
            }
        }

        [Test]
        public void TheViewFollowsALayoutSwitchAndKeepsTheShippedLayoutByDefault()
        {
            var shipped = LatticeLayoutBuilder.Shipped(WorldOrientation.Portrait);
            var view = NewView(shipped);
            Assert.That(view.RenderersOf(LatticeLayoutBuilder.ShippedLocationCount - 1), Is.Not.Empty);
            Assert.That(view.RenderersOf(LatticeLayoutBuilder.ShippedLocationCount), Is.Empty);

            var twenty = TestLayouts.Twenty();
            view.SetLayout(twenty);
            Assert.That(view.RenderersOf(19), Is.Not.Empty);
            Assert.That(view.RenderersOf(20), Is.Empty);
            view.Render(Visuals(20));
            Assert.That(view.TryHitSlot(twenty.PlateCentreOf(new BuildingLocationId(19)), out var hit) && hit == 19, Is.True);

            var six = TestLayouts.Six();
            view.SetLayout(six);
            Assert.That(view.RenderersOf(6), Is.Empty, "slots beyond the new layout are gone");
            view.Render(Visuals(6));
            Assert.That(view.TryHitSlot(six.PlateCentreOf(new BuildingLocationId(5)), out hit) && hit == 5, Is.True);
            Assert.That(view.TryHitSlot(twenty.PlateCentreOf(new BuildingLocationId(19)), out _), Is.False, "a point of the old layout no longer hits");

            view.SetLayout(LatticeLayoutBuilder.Shipped(WorldOrientation.Landscape));
            Assert.That(view.RenderersOf(LatticeLayoutBuilder.ShippedLocationCount - 1), Is.Not.Empty);
            Assert.That(view.RenderersOf(LatticeLayoutBuilder.ShippedLocationCount), Is.Empty);
        }

        // ------------------------------------------------------------------------------------------------ the real camera

        [Test]
        public void FitAllShowsTheWholeLayoutAtTheLargestFittingScale([ValueSource(typeof(TestLayouts), nameof(TestLayouts.Names))] string name)
        {
            var layout = TestLayouts.ByName(name);
            var controller = NewController(out var camera);
            var rect = new Rect(0f, 0f, Width, Height);
            controller.Fit(new ScaleLayout(Width, Height, 1f, rect, layout));

            var settings = new RunZoomSettings();
            var largest = CameraScaleModel.LargestFittingScale(rect, layout.Bounds, settings.artPixelsPerCell, layout.Margin);
            Assert.That(largest, Is.GreaterThanOrEqualTo(1), "the test screen shows the layout");
            Assert.That(controller.Scale, Is.EqualTo(Math.Min(largest, settings.maxScale)), "the scale comes from the layout bounds and margin");
            Assert.That(controller.Model.Layout.LayoutId, Is.EqualTo(layout.LayoutId));
            Assert.That(controller.Model.Layout.Bounds, Is.EqualTo(layout.Bounds));

            var (low, high) = ScreenRectOf(camera, layout.Bounds);
            Assert.That(low.x, Is.GreaterThanOrEqualTo(-0.5f), name + ": left");
            Assert.That(low.y, Is.GreaterThanOrEqualTo(-0.5f), name + ": bottom");
            Assert.That(high.x, Is.LessThanOrEqualTo(Width + 0.5f), name + ": right");
            Assert.That(high.y, Is.LessThanOrEqualTo(Height + 0.5f), name + ": top");
        }

        [Test]
        public void ThePanLimitIsTheLayoutBoundsPlusItsMargin([ValueSource(typeof(TestLayouts), nameof(TestLayouts.Names))] string name)
        {
            var layout = TestLayouts.ByName(name);
            var controller = NewController(out var camera);
            controller.Fit(new ScaleLayout(Width, Height, 1f, new Rect(0f, 0f, Width, Height), layout));
            while (controller.ZoomInState == ZoomButtonState.Enabled)
            {
                controller.ZoomFromVisibleControl(1);
            }

            controller.PanByWorldDelta(new Vector2(1000f, -1000f));
            var bounds = layout.Bounds;
            var position = camera.transform.position;
            var tolerance = 1f / (controller.Scale * Art);
            var halfWidth = camera.orthographicSize * camera.aspect;
            AssertWithinOrCentered(position.x, bounds.xMin - layout.Margin + halfWidth, bounds.xMax + layout.Margin - halfWidth, bounds.center.x, tolerance, name + " x");
            AssertWithinOrCentered(position.y, bounds.yMin - layout.Margin + camera.orthographicSize, bounds.yMax + layout.Margin - camera.orthographicSize, bounds.center.y, tolerance, name + " y");
        }

        private static void AssertWithinOrCentered(float value, float low, float high, float centre, float tolerance, string context)
        {
            if (low > high)
            {
                Assert.That(value, Is.EqualTo(centre).Within(tolerance), context + " (centred: the view is larger than the layout)");
            }
            else
            {
                Assert.That(value, Is.InRange(low - tolerance, high + tolerance), context);
            }
        }

        [Test]
        public void ASheetKeepsEveryLocationOfTheLayoutInView([ValueSource(typeof(TestLayouts), nameof(TestLayouts.Names))] string name)
        {
            var layout = TestLayouts.ByName(name);
            var controller = NewController(out var camera);
            var framing = new RunCameraFraming(new RunZoomSettings());
            var closed = new Rect(0f, 0f, Width, Height);
            var band = new Rect(0f, Height * 0.45f, Width, Height * 0.55f);
            controller.Fit(RunCameraFraming.Layout(camera, layout, 1f, closed));
            for (var i = 0; i < layout.LocationCount; i++)
            {
                controller.OpenSheet(band, framing.FrameSlot(i, layout));
                var (low, high) = ScreenRectOf(camera, layout.FootprintOf(new BuildingLocationId(i)));
                Assert.That(low.x, Is.GreaterThanOrEqualTo(band.xMin - 1.5f), $"{name}: location {i} left of the band");
                Assert.That(low.y, Is.GreaterThanOrEqualTo(band.yMin - 1.5f), $"{name}: location {i} below the band");
                Assert.That(high.x, Is.LessThanOrEqualTo(band.xMax + 1.5f), $"{name}: location {i} right of the band");
                Assert.That(high.y, Is.LessThanOrEqualTo(band.yMax + 1.5f), $"{name}: location {i} above the band");
                controller.CloseSheet(closed);
            }
        }

        [Test]
        public void AChangedLayoutRefitsThroughTheLayoutIdAndNeverThroughAColumnCount()
        {
            var controller = NewController(out _);
            var rect = new Rect(0f, 0f, Width, Height);
            var six = TestLayouts.Six();
            var twenty = TestLayouts.Twenty();
            controller.Fit(new ScaleLayout(Width, Height, 1f, rect, six));
            var sixScale = controller.Scale;
            Assert.That(controller.Model.Layout.LayoutId, Is.EqualTo(six.LayoutId));

            controller.UpdateLayout(new ScaleLayout(Width, Height, 1f, rect, twenty));
            Assert.That(controller.Model.Layout.LayoutId, Is.EqualTo(twenty.LayoutId));
            Assert.That(controller.Model.Layout.Bounds, Is.EqualTo(twenty.Bounds));
            var largestTwenty = CameraScaleModel.LargestFittingScale(rect, twenty.Bounds, Art, twenty.Margin);
            Assert.That(controller.Scale, Is.LessThanOrEqualTo(Math.Max(1, largestTwenty)), "a fit-all view stays within what the bigger layout allows");
            Assert.That(controller.Scale, Is.LessThanOrEqualTo(sixScale));
        }
    }
}
