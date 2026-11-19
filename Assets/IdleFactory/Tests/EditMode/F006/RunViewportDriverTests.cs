using System.Collections.Generic;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T558 (#771, batch-C gate fixes): the depot column is part of the closed-state camera fit (U-C1) and a rotation under an open card re-fits the world when the card
    /// closes (C-F2). The framing decision runs through the real <see cref="FactoryCameraController"/>, fed with the per-frame facts the bootstrap reads.
    /// </summary>
    public sealed class RunViewportDriverTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in objects)
            {
                Object.DestroyImmediate(item);
            }

            objects.Clear();
        }

        private FactoryCameraController NewController()
        {
            var host = new GameObject("viewport-driver-camera");
            objects.Add(host);
            host.transform.position = new Vector3(0f, 0f, -10f);
            var camera = host.AddComponent<Camera>();
            camera.orthographic = true;
            var controller = host.AddComponent<FactoryCameraController>();
            controller.Configure(camera, new RunZoomSettings().ToScaleSettings(), _ => { });
            return controller;
        }

        private static ScaleLayout Screen(int width, int height, float pixelsPerPoint, Rect area, FactoryWorldLayout layout) =>
            ScaleLayout.WithView(width, height, pixelsPerPoint, area, layout);

        private static bool Inside(Rect area, Rect screen, float tolerance = 1.5f) =>
            screen.xMin >= area.xMin - tolerance && screen.yMin >= area.yMin - tolerance && screen.xMax <= area.xMax + tolerance && screen.yMax <= area.yMax + tolerance;

        private static Rect OnScreen(FactoryCameraController controller, Rect world)
        {
            var model = controller.Model;
            var low = model.WorldToScreen(world.min, model.Position, model.Scale);
            var high = model.WorldToScreen(world.max, model.Position, model.Scale);
            return Rect.MinMaxRect(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y), Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y));
        }

        [Test]
        public void ARotationUnderAnOpenCardThenBackRefitsTheWorldToTheNewScreen([Values(100, 200)] int percent)
        {
            var controller = NewController();
            var driver = new RunViewportDriver(controller);
            var portrait = LatticeLayoutBuilder.ShippedForColumns(3);
            var landscape = LatticeLayoutBuilder.ShippedForColumns(4);

            // Phone portrait, then the same phone in landscape (2400x1080 at 2.625 px/pt): the card takes the bottom band in portrait and a column in landscape.
            var portraitClosed = new Rect(0f, 520f, 1080f, 1500f);
            var portraitBand = new Rect(0f, 1000f, 1080f, 1020f);
            var landscapeClosed = new Rect(120f, 0f, 2160f, 1000f);
            var landscapeBand = new Rect(120f, 0f, 1300f, 1000f);
            if (percent == 200)
            {
                portraitBand = new Rect(0f, 1200f, 1080f, 820f);
                landscapeBand = new Rect(120f, 0f, 1000f, 1000f);
            }

            driver.FramingPending = true;
            driver.Apply(Screen(1080, 2400, 2.625f, portraitClosed, portrait), portraitClosed, false, default);
            driver.NotifySelectionChanged();
            driver.Apply(Screen(1080, 2400, 2.625f, portraitBand, portrait), portraitBand, true, portrait.MachineRectOf(new BuildingLocationId(0)));
            Assert.That(controller.SheetOpen, Is.True, "the card is open");

            // The rotation: the bootstrap asks for a fit of the whole world, and the shell's rectangle is the card's band of the new screen.
            driver.FramingPending = true;
            driver.Apply(Screen(2400, 1080, 2.625f, landscapeBand, landscape), landscapeBand, true, landscape.MachineRectOf(new BuildingLocationId(0)));
            driver.Apply(Screen(2400, 1080, 2.625f, landscapeBand, landscape), landscapeBand, true, landscape.MachineRectOf(new BuildingLocationId(0)));
            Assert.That(controller.SheetOpen, Is.True, "a rotation keeps the card");

            // Back: the selection ends, the closed rectangle of the new screen returns. The factory must be in view and framed as a fresh fit, not as the old screen's view.
            driver.NotifySelectionChanged();
            driver.Apply(Screen(2400, 1080, 2.625f, landscapeClosed, landscape), landscapeClosed, false, default);
            driver.Apply(Screen(2400, 1080, 2.625f, landscapeClosed, landscape), landscapeClosed, false, default);

            Assert.That(controller.SheetOpen, Is.False, "the card is closed");
            Assert.That(Inside(landscapeClosed, OnScreen(controller, landscape.Bounds)), Is.True,
                "the whole factory is inside the closed rectangle after Back: " + OnScreen(controller, landscape.Bounds) + " vs " + landscapeClosed);

            var fresh = new CameraScaleModel(new RunZoomSettings().ToScaleSettings());
            fresh.Fit(Screen(2400, 1080, 2.625f, landscapeClosed, landscape));
            Assert.That(controller.Scale, Is.EqualTo(fresh.Scale), "the same scale a fresh fit gives");
            Assert.That(controller.Model.Position.x, Is.EqualTo(fresh.Position.x).Within(1e-3f), "and the same centring");
            Assert.That(controller.Model.Position.y, Is.EqualTo(fresh.Position.y).Within(1e-3f));
        }

        private static readonly Rect PortraitClosed = new Rect(0f, 520f, 1080f, 1500f);
        private static readonly Rect PortraitBand = new Rect(0f, 1000f, 1080f, 1020f);
        private static readonly Rect LandscapeClosed = new Rect(120f, 0f, 2160f, 1000f);
        private static readonly Rect LandscapeBand = new Rect(120f, 0f, 1300f, 1000f);

        private void AssertIsFreshFit(FactoryCameraController controller, FactoryWorldLayout layout, int w, int h, Rect closed, string where)
        {
            Assert.That(controller.SheetOpen, Is.False, where);
            var fresh = new CameraScaleModel(new RunZoomSettings().ToScaleSettings());
            fresh.Fit(Screen(w, h, 2.625f, closed, layout));
            Assert.That(controller.Scale, Is.EqualTo(fresh.Scale), where + ": scale");
            Assert.That(controller.Model.Position.x, Is.EqualTo(fresh.Position.x).Within(1e-3f), where + ": x");
            Assert.That(controller.Model.Position.y, Is.EqualTo(fresh.Position.y).Within(1e-3f), where + ": y");
        }

        [Test]
        public void ARotationThereAndBackUnderAnOpenCardThenBackIsAFreshFit([Values(0, 5)] int slot)
        {
            var controller = NewController();
            var driver = new RunViewportDriver(controller);
            var a = LatticeLayoutBuilder.ShippedForColumns(3);
            var b = LatticeLayoutBuilder.ShippedForColumns(4);
            driver.FramingPending = true;
            driver.Apply(Screen(1080, 2400, 2.625f, PortraitClosed, a), PortraitClosed, false, default);
            driver.NotifySelectionChanged();
            driver.Apply(Screen(1080, 2400, 2.625f, PortraitBand, a), PortraitBand, true, a.MachineRectOf(new BuildingLocationId(slot)));
            driver.FramingPending = true;
            driver.Apply(Screen(2400, 1080, 2.625f, LandscapeBand, b), LandscapeBand, true, b.MachineRectOf(new BuildingLocationId(slot)));
            driver.Apply(Screen(2400, 1080, 2.625f, LandscapeBand, b), LandscapeBand, true, b.MachineRectOf(new BuildingLocationId(slot)));
            driver.FramingPending = true;
            driver.Apply(Screen(1080, 2400, 2.625f, PortraitBand, a), PortraitBand, true, a.MachineRectOf(new BuildingLocationId(slot)));
            driver.Apply(Screen(1080, 2400, 2.625f, PortraitBand, a), PortraitBand, true, a.MachineRectOf(new BuildingLocationId(slot)));
            driver.NotifySelectionChanged();
            driver.Apply(Screen(1080, 2400, 2.625f, PortraitClosed, a), PortraitClosed, false, default);
            driver.Apply(Screen(1080, 2400, 2.625f, PortraitClosed, a), PortraitClosed, false, default);
            AssertIsFreshFit(controller, a, 1080, 2400, PortraitClosed, "A to B to A, then Back");
        }

        [Test]
        public void AFitAllWhileACardIsOpenThenBackIsAFreshFit()
        {
            var controller = NewController();
            var driver = new RunViewportDriver(controller);
            var a = LatticeLayoutBuilder.ShippedForColumns(3);
            driver.FramingPending = true;
            driver.Apply(Screen(1080, 2400, 2.625f, PortraitClosed, a), PortraitClosed, false, default);
            driver.NotifySelectionChanged();
            driver.Apply(Screen(1080, 2400, 2.625f, PortraitBand, a), PortraitBand, true, a.MachineRectOf(new BuildingLocationId(0)));
            driver.FramingPending = true; // fitAll or a settings change, same screen
            driver.Apply(Screen(1080, 2400, 2.625f, PortraitBand, a), PortraitBand, true, a.MachineRectOf(new BuildingLocationId(0)));
            driver.Apply(Screen(1080, 2400, 2.625f, PortraitBand, a), PortraitBand, true, a.MachineRectOf(new BuildingLocationId(0)));
            driver.NotifySelectionChanged();
            driver.Apply(Screen(1080, 2400, 2.625f, PortraitClosed, a), PortraitClosed, false, default);
            driver.Apply(Screen(1080, 2400, 2.625f, PortraitClosed, a), PortraitClosed, false, default);
            AssertIsFreshFit(controller, a, 1080, 2400, PortraitClosed, "fitAll under a card, then Back");
        }

        [Test]
        public void BackWithoutARotationStillReturnsThePriorView()
        {
            var controller = NewController();
            var driver = new RunViewportDriver(controller);
            var layout = LatticeLayoutBuilder.ShippedForColumns(3);
            var closed = new Rect(0f, 520f, 1080f, 1500f);
            var band = new Rect(0f, 1000f, 1080f, 1020f);

            driver.FramingPending = true;
            driver.Apply(Screen(1080, 2400, 2.625f, closed, layout), closed, false, default);
            var scale = controller.Scale;
            var position = controller.Model.Position;
            driver.NotifySelectionChanged();
            driver.Apply(Screen(1080, 2400, 2.625f, band, layout), band, true, layout.MachineRectOf(new BuildingLocationId(layout.LocationCount - 1)));
            driver.NotifySelectionChanged();
            driver.Apply(Screen(1080, 2400, 2.625f, closed, layout), closed, false, default);

            Assert.That(controller.SheetOpen, Is.False);
            Assert.That(controller.Scale, Is.EqualTo(scale), "the prior scale returns");
            Assert.That(controller.Model.Position.x, Is.EqualTo(position.x).Within(1e-3f), "and the prior position");
            Assert.That(controller.Model.Position.y, Is.EqualTo(position.y).Within(1e-3f));
        }

        [Test]
        public void TheDepotColumnIsPartOfTheFitExtent()
        {
            foreach (var layout in new[] { LatticeLayoutBuilder.ShippedForColumns(3), LatticeLayoutBuilder.ShippedForColumns(4) })
            {
                var view = layout.FitBounds;
                Assert.That(Inside(layout.ViewBounds, view, 1e-4f), Is.True, layout.LayoutId + ": the view extent holds the fit extent");
                Assert.That(Inside(layout.ViewBounds, layout.DepositRect, 1e-4f), Is.True, layout.LayoutId + ": and the vein");
                Assert.That(Inside(view, layout.DepositRect, 1e-4f) && !Inside(layout.Bounds, layout.DepositRect, 1e-4f), Is.False, layout.LayoutId + ": the vein is not part of the depot column fit");
                Assert.That(Inside(view, layout.DepotRect, 1e-4f), Is.True, layout.LayoutId + ": the depot sprite");
                Assert.That(Inside(view, AttentionModel.MarkerBoxAt(layout.DepotAnchor), 1e-4f), Is.True, layout.LayoutId + ": the store-full marker box");
                for (var row = 0; row < layout.RowCount; row++)
                {
                    var trunk = layout.TrunkOf(row);
                    if (trunk.IsEmpty)
                    {
                        continue;
                    }

                    Assert.That(Inside(view, AttentionModel.MarkerBoxAt(trunk.Points[1]), 1e-4f), Is.True, layout.LayoutId + ": the shortage marker box of row " + row);
                    Assert.That(trunk.Points[1].x, Is.EqualTo(layout.DepotAnchor.x).Within(1e-4f), "the collector sits on the depot column");
                    Assert.That(view.yMax, Is.GreaterThanOrEqualTo(trunk.Points[1].y), layout.LayoutId + ": the collector top");
                    Assert.That(view.yMin, Is.LessThanOrEqualTo(trunk.Points[1].y), layout.LayoutId + ": the collector bottom");
                }
            }
        }
    }
}
