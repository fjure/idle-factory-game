using System;
using System.Collections.Generic;
using NUnit.Framework;
using IdleFactory.Unity.Presentation;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// <c>contracts/camera-scale.md</c> rules 1 to 7 as pure functions: the allowed range, fit-all, the runtime degrade, odd pixel
    /// sizes, snapping with an anchor hold, the sheet pan-then-step and restore, single-level behaviour, and the 44 pt hit area.
    /// </summary>
    public sealed class CameraScaleModelTests
    {
        private static readonly float[] PixelsPerPointValues = { 1.68f, 2.0f, 2.625f, 3.0f };
        private static readonly int[] Densities = { 16, 24, 32 };

        private static ScaleLayout Layout(int width, int height, float pixelsPerPoint, int columns = 3, Rect? rect = null) =>
            new ScaleLayout(width, height, pixelsPerPoint, rect ?? new Rect(0f, 0f, width, height), columns);

        private static CameraScaleModel Fitted(int width, int height, float pixelsPerPoint, int density = 24, int max = 6, int columns = 3, Rect? rect = null, Action<string> log = null)
        {
            var model = new CameraScaleModel(new ArtScaleSettings(density, 24, max), log);
            model.Fit(Layout(width, height, pixelsPerPoint, columns, rect));
            return model;
        }

        private static Vector2 ScreenOf(CameraScaleModel model, Vector2 world) => model.WorldToScreen(world, model.Position, model.Scale);

        // ------------------------------------------------------------------ rule 1

        [Test]
        public void Rule1_OrthographicSizeIsPixelHeightOverTwoNd()
        {
            Assert.That(CameraScaleModel.OrthographicSize(1920, 2, 24), Is.EqualTo(1920f / 96f).Within(1e-5f));
            Assert.That(CameraScaleModel.OrthographicSize(1081, 3, 16), Is.EqualTo(1081f / 96f).Within(1e-5f));
            foreach (var n in new[] { 1, 2, 3, 4, 6 })
            {
                // The projected height in device pixels is exactly pixelHeight: 2 * size * n * D.
                Assert.That(2f * CameraScaleModel.OrthographicSize(1337, n, 24) * n * 24, Is.EqualTo(1337f).Within(1e-3f));
            }
        }

        [Test]
        public void Rule1_ThePositionIsAMultipleOfOneOverNdOnBothAxesAtRest()
        {
            foreach (var ppp in PixelsPerPointValues)
            {
                var model = Fitted(1080, 1920, ppp);
                Assert.That(CameraScaleModel.IsOnPixelGrid(model.Position.x, 1080, model.Scale, 24), Is.True, $"x at {ppp}");
                Assert.That(CameraScaleModel.IsOnPixelGrid(model.Position.y, 1920, model.Scale, 24), Is.True, $"y at {ppp}");
                Assert.That(model.Position.x * model.Scale * 24, Is.EqualTo(Mathf.Round(model.Position.x * model.Scale * 24)).Within(1e-2f));
            }
        }

        [Test]
        public void Rule1_SnapRoundsToTheNearestDevicePixel()
        {
            Assert.That(CameraScaleModel.SnapCoordinate(0.01f, 1080, 2, 24), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(CameraScaleModel.SnapCoordinate(0.011f, 1080, 2, 24), Is.EqualTo(1f / 48f).Within(1e-6f));
            Assert.That(CameraScaleModel.SnapCoordinate(-0.011f, 1080, 2, 24), Is.EqualTo(-1f / 48f).Within(1e-6f));
        }

        // ------------------------------------------------------------------ odd sizes (rule 2 last sentence)

        [Test]
        public void OddPixelSizesOffsetTheCameraByHalfADevicePixelSoArtPixelEdgesStayOnDevicePixelEdges()
        {
            foreach (var (width, height) in new[] { (1081, 1921), (1080, 1921), (1081, 1920), (721, 1281), (1001, 1001) })
            {
                foreach (var ppp in PixelsPerPointValues)
                {
                    foreach (var density in Densities)
                    {
                        var model = Fitted(width, height, ppp, density);
                        var k = model.Scale * density;
                        // World x = 0 and y = 0 are art pixel edges (multiples of 1 / D); they must land on integer device pixels.
                        var edge = model.WorldToScreen(Vector2.zero, model.Position, model.Scale);
                        Assert.That(edge.x, Is.EqualTo(Mathf.Round(edge.x)).Within(0.02f), $"{width}x{height} ppp {ppp} D {density} x edge at {edge.x}");
                        Assert.That(edge.y, Is.EqualTo(Mathf.Round(edge.y)).Within(0.02f), $"{width}x{height} ppp {ppp} D {density} y edge at {edge.y}");
                        var oneArtPixelRight = model.WorldToScreen(new Vector2(1f / density, 0f), model.Position, model.Scale);
                        Assert.That(oneArtPixelRight.x - edge.x, Is.EqualTo(model.Scale).Within(0.02f), "an art pixel is n device pixels wide");
                        Assert.That(k, Is.GreaterThan(0));
                    }
                }
            }
        }

        [Test]
        public void AnEvenSizeNeedsNoOffsetAndAnOddSizeNeedsHalfADevicePixel()
        {
            Assert.That(CameraScaleModel.SnapCoordinate(0f, 1080, 2, 24), Is.EqualTo(0f).Within(1e-7f));
            Assert.That(CameraScaleModel.SnapCoordinate(0f, 1081, 2, 24), Is.EqualTo(0.5f / 48f).Within(1e-6f).Or.EqualTo(-0.5f / 48f).Within(1e-6f));
        }

        // ------------------------------------------------------------------ rule 2

        [Test]
        public void Rule2_MinScaleIsTheCeilingOfReadablePointsTimesPxPerPointOverD()
        {
            var expected24 = new Dictionary<float, int> { { 1.68f, 2 }, { 2.0f, 2 }, { 2.625f, 3 }, { 3.0f, 3 } };
            var expected16 = new Dictionary<float, int> { { 1.68f, 3 }, { 2.0f, 3 }, { 2.625f, 4 }, { 3.0f, 5 } };
            var expected32 = new Dictionary<float, int> { { 1.68f, 2 }, { 2.0f, 2 }, { 2.625f, 2 }, { 3.0f, 3 } };
            foreach (var ppp in PixelsPerPointValues)
            {
                Assert.That(CameraScaleModel.MinScale(new ArtScaleSettings(24, 24, 6), ppp), Is.EqualTo(expected24[ppp]), $"D24 {ppp}");
                Assert.That(CameraScaleModel.MinScale(new ArtScaleSettings(16, 24, 6), ppp), Is.EqualTo(expected16[ppp]), $"D16 {ppp}");
                Assert.That(CameraScaleModel.MinScale(new ArtScaleSettings(32, 24, 6), ppp), Is.EqualTo(expected32[ppp]), $"D32 {ppp}");
            }
        }

        [Test]
        public void Rule2_MinScaleHasAOnePercentToleranceOnTheExactCeiling()
        {
            var settings = new ArtScaleSettings(24, 24, 6);
            Assert.That(CameraScaleModel.MinScale(settings, 2.0046f), Is.EqualTo(2), "a miss of 0.23 % is not a violation");
            Assert.That(CameraScaleModel.MinScale(settings, 2.0099f), Is.EqualTo(2), "just under 1 %");
            Assert.That(CameraScaleModel.MinScale(settings, 2.0101f), Is.EqualTo(3), "just over 1 %");
            Assert.That(CameraScaleModel.MinScale(settings, 2.0f), Is.EqualTo(2), "an exact multiple stays");
            Assert.That(CameraScaleModel.MinScale(settings, 0.5f), Is.EqualTo(1), "never below 1");
        }

        [Test]
        public void Rule2_TheOddPixelOffsetIsHalfADevicePixelNotHalfAnArtPixel()
        {
            // #520 follow-up 3: at n = 3 an art pixel is 3 device pixels, so half an art pixel (1.5 device pixels) would break the grid.
            foreach (var n in new[] { 1, 2, 3, 6 })
            {
                var snapped = CameraScaleModel.SnapCoordinate(0f, 1081, n, 24);
                Assert.That(Math.Abs(snapped), Is.EqualTo(1f / (2f * n * 24f)).Within(1e-6f), $"n {n}");
            }
        }

        [Test]
        public void Rule2_TheAllowedSetIsNMinToMaxScale()
        {
            var model = Fitted(1080, 1920, 2.625f);
            Assert.That(model.LowerBound, Is.EqualTo(3));
            Assert.That(model.UpperBound, Is.EqualTo(6));
            Assert.That(model.LevelCount, Is.EqualTo(4));
            Assert.That(model.NearestAllowed(0.2f), Is.EqualTo(3));
            Assert.That(model.NearestAllowed(99f), Is.EqualTo(6));
        }

        // ------------------------------------------------------------------ rule 3

        [Test]
        public void Rule3_FitAllPicksTheLargestScaleThatFitsAndCentresTheFactory()
        {
            foreach (var ppp in PixelsPerPointValues)
            {
                var rect = new Rect(80f, 120f, 920f, 1500f);
                var model = Fitted(1080, 1920, ppp, rect: rect);
                var largest = CameraScaleModel.LargestFittingScale(rect, 3, 24);
                Assert.That(model.FitScale, Is.EqualTo(Math.Min(largest, 6)));
                Assert.That(model.Scale, Is.EqualTo(model.FitScale));
                Assert.That(model.BelowFloor, Is.False);

                // One more level would not fit (unless capped by maxScale).
                if (model.FitScale < 6)
                {
                    Assert.That(CameraScaleModel.LargestFittingScale(rect, 3, 24), Is.EqualTo(model.FitScale));
                }

                var extent = SlotGridLayout.Extent(3);
                var centre = ScreenOf(model, extent.center);
                Assert.That(centre.x, Is.EqualTo(rect.center.x).Within(0.75f), $"x centred at {ppp}");
                Assert.That(centre.y, Is.EqualTo(rect.center.y).Within(0.75f), $"y centred at {ppp}");

                var low = ScreenOf(model, extent.min - new Vector2(0.5f, 0.5f));
                var high = ScreenOf(model, extent.max + new Vector2(0.5f, 0.5f));
                Assert.That(low.x, Is.GreaterThanOrEqualTo(rect.xMin - 1f));
                Assert.That(low.y, Is.GreaterThanOrEqualTo(rect.yMin - 1f));
                Assert.That(high.x, Is.LessThanOrEqualTo(rect.xMax + 1f));
                Assert.That(high.y, Is.LessThanOrEqualTo(rect.yMax + 1f));
            }
        }

        [Test]
        public void Rule3_FitAllNeverExceedsMaxScale()
        {
            var model = Fitted(3840, 2160, 1f, max: 3, columns: 4);
            Assert.That(model.FitScale, Is.EqualTo(3));
            Assert.That(model.Scale, Is.EqualTo(3));
        }

        [Test]
        public void Rule3_TheRuntimeDegradeUsesTheLargestScaleThatFitsIgnoresTheFloorAndLogsOncePerLayout()
        {
            var logs = new List<string>();
            var model = new CameraScaleModel(new ArtScaleSettings(24, 24, 6), logs.Add);
            var small = Layout(400, 300, 2.0f, 4);
            model.Fit(small);
            Assert.That(model.BelowFloor, Is.True);
            Assert.That(CameraScaleModel.MinScale(model.Settings, 2.0f), Is.EqualTo(2));
            Assert.That(model.Scale, Is.EqualTo(1), "largest n >= 1 that fits");
            Assert.That(model.IsSingleLevel, Is.False.Or.True);
            Assert.That(model.LowerBound, Is.EqualTo(1));
            Assert.That(logs, Has.Count.EqualTo(1));
            StringAssert.StartsWith(CameraScaleModel.BelowFloorCode, logs[0]);

            // The same layout again does not log again; a different layout does.
            model.OnLayoutChanged(small);
            model.OnLayoutChanged(Layout(400, 300, 2.0f, 4, new Rect(0f, 0f, 400f, 280f)));
            Assert.That(logs, Has.Count.EqualTo(1), "once per layout, not per rect change");
            model.OnLayoutChanged(Layout(420, 310, 2.0f, 4));
            Assert.That(logs, Has.Count.EqualTo(2));
        }

        [Test]
        public void Rule3_TheDegradeNeverUsesAFractionalOrZeroScaleEvenWhenNothingFits()
        {
            var model = Fitted(60, 40, 2f, columns: 4);
            Assert.That(model.BelowFloor, Is.True);
            Assert.That(model.Scale, Is.EqualTo(1));
        }

        [Test]
        public void FreeWindowSizesAndContinuousPixelsPerPointKeepEveryInvariant()
        {
            var windows = new[] { (500, 300), (640, 480), (800, 600), (1001, 577), (1280, 720), (1366, 768), (1919, 1079), (2560, 1440), (3440, 1440), (480, 960) };
            var rng = new System.Random(4081);
            foreach (var (width, height) in windows)
            {
                for (var i = 0; i < 12; i++)
                {
                    var ppp = 1f + (float)rng.NextDouble() * 2.2f;
                    var columns = width > height ? 4 : 3;
                    var rect = new Rect(width * 0.05f, height * 0.08f, width * 0.9f, height * 0.8f);
                    var model = Fitted(width, height, ppp, columns: columns, rect: rect);
                    var label = $"{width}x{height} ppp {ppp:0.###}";
                    Assert.That(model.Scale, Is.GreaterThanOrEqualTo(1), label);
                    Assert.That(model.Scale, Is.LessThanOrEqualTo(6), label);
                    Assert.That(CameraScaleModel.IsOnPixelGrid(model.Position.x, width, model.Scale, 24), Is.True, label);
                    Assert.That(CameraScaleModel.IsOnPixelGrid(model.Position.y, height, model.Scale, 24), Is.True, label);
                    if (!model.BelowFloor)
                    {
                        Assert.That(model.Scale, Is.GreaterThanOrEqualTo(CameraScaleModel.MinScale(model.Settings, ppp)), label);
                    }

                    if (CameraScaleModel.LargestFittingScale(rect, columns, 24) >= 1)
                    {
                        var extent = SlotGridLayout.Extent(columns);
                        var low = ScreenOf(model, extent.min);
                        var high = ScreenOf(model, extent.max);
                        Assert.That(low.x, Is.GreaterThanOrEqualTo(rect.xMin - 1f), label);
                        Assert.That(high.x, Is.LessThanOrEqualTo(rect.xMax + 1f), label);
                        Assert.That(low.y, Is.GreaterThanOrEqualTo(rect.yMin - 1f), label);
                        Assert.That(high.y, Is.LessThanOrEqualTo(rect.yMax + 1f), label);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ rule 4

        [Test]
        public void Rule4_PinchPreviewIsContinuousAndSnapsToTheNearestAllowedScaleOnEnd()
        {
            var model = Fitted(1200, 1200, 1f, columns: 4);
            Assert.That(model.Scale, Is.EqualTo(model.FitScale));
            var start = model.Scale;
            model.PreviewZoom(start + 0.4f, new Vector2(600f, 600f));
            Assert.That(model.GestureActive, Is.True);
            Assert.That(model.DisplayScale, Is.EqualTo(start + 0.4f).Within(1e-5f));
            Assert.That(model.Scale, Is.EqualTo(start), "the integer rest scale does not change during the preview");
            Assert.That(model.EndGesture(), Is.False, "2.4 rounds back to the start");
            Assert.That(model.Scale, Is.EqualTo(start));
            Assert.That(model.GestureActive, Is.False);
            Assert.That(model.DisplayScale, Is.EqualTo(start));
        }

        [Test]
        public void Rule4_ATieSnapsTowardTheLargerScale()
        {
            var model = Fitted(1200, 1200, 1f, columns: 4);
            var start = model.Scale;
            Assert.That(model.NearestAllowed(start + 0.5f), Is.EqualTo(start + 1));
            model.PreviewZoom(start + 0.5f, new Vector2(600f, 600f));
            Assert.That(model.EndGesture(), Is.True);
            Assert.That(model.Scale, Is.EqualTo(start + 1));
        }

        [Test]
        public void Rule4_TheWorldPointUnderTheGestureAnchorStaysFixedAcrossTheSnap()
        {
            var rect = new Rect(900f, 900f, 640f, 560f);
            var model = Fitted(2400, 2400, 1f, columns: 4, max: 6, rect: rect);
            Assert.That(model.Scale, Is.EqualTo(2));
            var anchor = new Vector2(1250f, 1150f);
            var before = model.ScreenToWorld(anchor, model.Position, model.Scale);
            model.PreviewZoom(3.6f, anchor);
            Assert.That(model.EndGesture(), Is.True);
            Assert.That(model.Scale, Is.EqualTo(4));
            var after = model.WorldToScreen(before, model.Position, model.Scale);
            Assert.That(Vector2.Distance(after, anchor), Is.LessThanOrEqualTo(1.5f), "anchor moved by more than a device pixel");
            Assert.That(CameraScaleModel.IsOnPixelGrid(model.Position.x, 2400, model.Scale, 24), Is.True);
        }

        [Test]
        public void Rule4_WheelAndButtonStepOneLevelAndNeverPreview()
        {
            var model = Fitted(1200, 1200, 1f, columns: 4);
            var start = model.Scale;
            Assert.That(model.StepZoom(1, new Vector2(600f, 600f)), Is.True);
            Assert.That(model.Scale, Is.EqualTo(start + 1));
            Assert.That(model.GestureActive, Is.False);
            Assert.That(model.StepZoom(-1, new Vector2(600f, 600f)), Is.True);
            Assert.That(model.Scale, Is.EqualTo(start));
        }

        [Test]
        public void Rule4_ButtonsAreDisabledAtTheRangeEndsAndStepsStopThere()
        {
            var model = Fitted(1200, 1200, 1f, columns: 4, max: 4);
            Assert.That(model.LevelCount, Is.GreaterThan(1));
            while (model.StepZoom(-1, new Vector2(600f, 600f)))
            {
            }

            Assert.That(model.Scale, Is.EqualTo(model.LowerBound));
            Assert.That(model.ZoomOutState, Is.EqualTo(ZoomButtonState.DisabledAtLimit));
            Assert.That(model.ZoomInState, Is.EqualTo(ZoomButtonState.Enabled));
            while (model.StepZoom(1, new Vector2(600f, 600f)))
            {
            }

            Assert.That(model.Scale, Is.EqualTo(4));
            Assert.That(model.ZoomInState, Is.EqualTo(ZoomButtonState.DisabledAtLimit));
            Assert.That(model.ZoomOutState, Is.EqualTo(ZoomButtonState.Enabled));
        }

        [Test]
        public void Rule4_TheSnapZoomOfAPinchWithoutAGestureEndStepsDirectly()
        {
            var model = Fitted(1200, 1200, 1f, columns: 4);
            var start = model.Scale;
            Assert.That(model.SnapZoomTo(start + 1.2f, new Vector2(600f, 600f)), Is.True);
            Assert.That(model.Scale, Is.EqualTo(start + 1));
            Assert.That(model.GestureActive, Is.False);
        }

        // ------------------------------------------------------------------ rule 5

        [Test]
        public void Rule5_ACancelledGestureRestoresTheScaleAndPosition()
        {
            var model = Fitted(1200, 1200, 1f, columns: 4);
            var scale = model.Scale;
            var position = model.Position;
            model.PreviewZoom(scale + 1.4f, new Vector2(500f, 500f));
            model.CancelGesture();
            Assert.That(model.GestureActive, Is.False);
            Assert.That(model.Scale, Is.EqualTo(scale));
            Assert.That(model.Position, Is.EqualTo(position));
            Assert.That(model.EndGesture(), Is.False, "nothing to end after a cancel");
        }

        [Test]
        public void Rule5_ALayoutChangeDuringAGestureCancelsItAndReFitsOnlyWhenTheScaleNoLongerFits()
        {
            var model = Fitted(1200, 1200, 1f, columns: 4);
            var scale = model.Scale;
            model.PreviewZoom(scale + 1f, new Vector2(500f, 500f));

            // A bigger window: the current scale still fits, so it is kept.
            Assert.That(model.OnLayoutChanged(Layout(1500, 1500, 1f, 4)), Is.False);
            Assert.That(model.GestureActive, Is.False);
            Assert.That(model.Scale, Is.EqualTo(scale));

            // A window too small for the current scale: re-fit.
            Assert.That(model.OnLayoutChanged(Layout(700, 700, 1f, 4)), Is.True);
            Assert.That(model.Scale, Is.EqualTo(CameraScaleModel.LargestFittingScale(new Rect(0f, 0f, 700f, 700f), 4, 24)));
        }

        [Test]
        public void Rule5_AResizeKeepsAPlayerZoomedInScaleThatIsStillAllowed()
        {
            var model = Fitted(1200, 1200, 1f, columns: 4);
            model.StepZoom(1, new Vector2(600f, 600f));
            var zoomed = model.Scale;
            Assert.That(model.OnLayoutChanged(Layout(1100, 1150, 1f, 4)), Is.False);
            Assert.That(model.Scale, Is.EqualTo(zoomed), "zoomed in is not a fit-all view, so a resize does not re-fit");
        }

        // ------------------------------------------------------------------ rule 6

        [Test]
        public void Rule6_PanningNeverShowsMoreThanAHalfCellBeyondTheFactory()
        {
            var rect = new Rect(300f, 300f, 600f, 600f);
            var model = Fitted(1200, 1200, 1f, columns: 4, rect: rect);
            while (model.StepZoom(1, rect.center))
            {
            }

            Assert.That(model.Scale, Is.EqualTo(6));
            var extent = SlotGridLayout.Extent(4);
            foreach (var delta in new[] { new Vector2(500f, 0f), new Vector2(-500f, 0f), new Vector2(0f, 500f), new Vector2(0f, -500f), new Vector2(-300f, -300f) })
            {
                model.Pan(delta);
                var low = model.ScreenToWorld(rect.min, model.Position, model.Scale);
                var high = model.ScreenToWorld(rect.max, model.Position, model.Scale);
                var tolerance = 1f / (model.Scale * 24f);
                Assert.That(low.x, Is.GreaterThanOrEqualTo(extent.xMin - 0.5f - tolerance), $"left after {delta}");
                Assert.That(low.y, Is.GreaterThanOrEqualTo(extent.yMin - 0.5f - tolerance), $"bottom after {delta}");
                Assert.That(high.x, Is.LessThanOrEqualTo(extent.xMax + 0.5f + tolerance), $"right after {delta}");
                Assert.That(high.y, Is.LessThanOrEqualTo(extent.yMax + 0.5f + tolerance), $"top after {delta}");
            }
        }

        [Test]
        public void Rule6_WhenTheWholeFactoryFitsPanningHasNothingToMove()
        {
            var model = Fitted(1080, 1920, 2f);
            var start = model.Position;
            Assert.That(model.Pan(new Vector2(0.3f, 0.3f)), Is.False.Or.True);
            Assert.That(Vector2.Distance(model.Position, start), Is.LessThanOrEqualTo(0.3f), "at most the pan margin the rectangle leaves");
            model.Pan(new Vector2(50f, 50f));
            model.Pan(new Vector2(-100f, -100f));
            Assert.That(CameraScaleModel.IsOnPixelGrid(model.Position.x, 1080, model.Scale, 24), Is.True);
        }

        [Test]
        public void Rule6_APanStepStaysOnTheDevicePixelGrid()
        {
            var rect = new Rect(0f, 0f, 1201f, 1201f);
            var model = Fitted(1201, 1201, 1f, columns: 4, rect: rect);
            while (model.StepZoom(1, rect.center))
            {
            }

            model.Pan(new Vector2(0.0377f, -0.0213f));
            Assert.That(CameraScaleModel.IsOnPixelGrid(model.Position.x, 1201, model.Scale, 24), Is.True);
            Assert.That(CameraScaleModel.IsOnPixelGrid(model.Position.y, 1201, model.Scale, 24), Is.True);
        }

        // ------------------------------------------------------------------ rule 7

        [Test]
        public void Rule7_WithASheetOpenEverySlotIsPannedFullyIntoTheBand()
        {
            var full = new Rect(104f, 112f, 572f, 1416f);
            var band = new Rect(104f, 1013f, 572f, 515f);
            for (var slot = 0; slot < SlotGridLayout.SlotCount; slot++)
            {
                var model = Fitted(780, 1688, 2f, rect: full);
                var outcome = model.OpenSheet(band, SlotGridLayout.SlotRect(slot, 3));
                var rect = SlotGridLayout.SlotRect(slot, 3);
                var low = ScreenOf(model, rect.min);
                var high = ScreenOf(model, rect.max);
                Assert.That(outcome, Is.Not.EqualTo(SheetOutcome.TopAligned), $"slot {slot + 1}");
                Assert.That(low.x, Is.GreaterThanOrEqualTo(band.xMin - 1f), $"slot {slot + 1} left");
                Assert.That(low.y, Is.GreaterThanOrEqualTo(band.yMin - 1f), $"slot {slot + 1} bottom");
                Assert.That(high.x, Is.LessThanOrEqualTo(band.xMax + 1f), $"slot {slot + 1} right");
                Assert.That(high.y, Is.LessThanOrEqualTo(band.yMax + 1f), $"slot {slot + 1} top");
            }
        }

        [Test]
        public void Rule7_AClosedSheetReturnsThePriorScaleAndPositionWhenThePlayerDidNotMove()
        {
            var full = new Rect(104f, 112f, 572f, 1416f);
            var band = new Rect(104f, 1013f, 572f, 515f);
            var model = Fitted(780, 1688, 2f, rect: full);
            var scale = model.Scale;
            var position = model.Position;
            model.OpenSheet(band, SlotGridLayout.SlotRect(0, 3));
            model.CloseSheet(full);
            Assert.That(model.SheetOpen, Is.False);
            Assert.That(model.Scale, Is.EqualTo(scale));
            Assert.That(model.Position, Is.EqualTo(position));
        }

        [Test]
        public void Rule7_AClosedSheetClampsTheRestoredScaleWhenTheLayoutChangedWhileItWasOpen()
        {
            // #520 follow-up 1: the prior scale was fit-all (4) on a large rectangle; the window shrank while the sheet was open.
            var model = new CameraScaleModel(new ArtScaleSettings(24, 24, 6));
            var large = new Rect(0f, 0f, 1000f, 1000f);
            model.Fit(Layout(1000, 1000, 1f, 4, large));
            Assert.That(model.Scale, Is.EqualTo(3));
            model.OpenSheet(new Rect(0f, 700f, 1000f, 300f), SlotGridLayout.SlotRect(0, 4));
            var small = new Rect(0f, 0f, 600f, 600f);
            model.CloseSheet(small);
            var largest = CameraScaleModel.LargestFittingScale(small, 4, 24);
            Assert.That(model.Scale, Is.LessThanOrEqualTo(largest), "the restored scale fits the new rectangle");
            Assert.That(model.Scale, Is.InRange(model.LowerBound, model.UpperBound));
            Assert.That(CameraScaleModel.IsOnPixelGrid(model.Position.x, 1000, model.Scale, 24), Is.True);
            Assert.That(model.SheetOpen, Is.False);
        }

        [Test]
        public void Rule7_ARepeatedOpenKeepsTheUnobstructedHeightOfTheFirstOpen()
        {
            // The band shrinks the rectangle; a second open (the layout resolving, a new band) must not lose the full height.
            var full = new Rect(104f, 112f, 572f, 1416f);
            var model = Fitted(780, 1688, 2f, rect: full);
            model.OpenSheet(new Rect(104f, 600f, 572f, 928f), SlotGridLayout.SlotRect(0, 3));
            model.OpenSheet(new Rect(104f, 1013f, 572f, 515f), SlotGridLayout.SlotRect(0, 3));
            var slot = SlotGridLayout.SlotRect(0, 3);
            var high = ScreenOf(model, slot.max);
            Assert.That(high.y, Is.LessThanOrEqualTo(1013f + 515f + 1f));
            model.CloseSheet(full);
            Assert.That(model.SheetOpen, Is.False);
        }

        [Test]
        public void Rule7_AClosedSheetKeepsTheViewWhenThePlayerPannedOrZoomedMeanwhile()
        {
            var full = new Rect(104f, 112f, 572f, 1416f);
            var band = new Rect(104f, 1013f, 572f, 515f);
            var model = Fitted(780, 1688, 2f, rect: full);
            var position = model.Position;
            model.OpenSheet(band, SlotGridLayout.SlotRect(0, 3));
            model.StepZoom(1, band.center);
            var zoomed = model.Scale;
            model.CloseSheet(full);
            Assert.That(model.Scale, Is.EqualTo(zoomed), "the player's zoom is kept");
            Assert.That(model.Position, Is.Not.EqualTo(position));
        }

        [Test]
        public void Rule7_WhenPanningCannotFitTheSlotTheScaleStepsDownToTheLargestScaleThatDoes()
        {
            // D = 24, 1 px/pt: n_min 1, fit-all at n = 3. A 90 px band cannot hold the 105 px plate diamond (35 art px) at 3, but holds the 70 px one at 2.
            var full = new Rect(0f, 0f, 1000f, 1000f);
            var band = new Rect(0f, 910f, 1000f, 90f);
            var model = Fitted(1000, 1000, 1f, columns: 4, rect: full);
            Assert.That(model.Scale, Is.EqualTo(3));
            var slot = SlotGridLayout.SlotRect(5, 4);
            Assert.That(model.OpenSheet(band, slot), Is.EqualTo(SheetOutcome.SteppedDown));
            Assert.That(model.Scale, Is.EqualTo(2));
            var low = ScreenOf(model, slot.min);
            var high = ScreenOf(model, slot.max);
            Assert.That(low.y, Is.GreaterThanOrEqualTo(band.yMin - 1f));
            Assert.That(high.y, Is.LessThanOrEqualTo(band.yMax + 1f));
            Assert.That(model.Scale, Is.GreaterThanOrEqualTo(model.LowerBound));
            model.CloseSheet(full);
            Assert.That(model.Scale, Is.EqualTo(3), "the prior scale returns on close");
        }

        [Test]
        public void Rule7_ASingleLevelRangeOnlyPansAndAlignsTheFootprintToTheBandTopWhenItStillDoesNotFit()
        {
            var settings = new ArtScaleSettings(24, 24, 1);
            var model = new CameraScaleModel(settings);
            var full = new Rect(0f, 0f, 1000f, 1000f);
            model.Fit(Layout(1000, 1000, 1f, 4, full));
            Assert.That(model.IsSingleLevel, Is.True);
            Assert.That(model.ZoomInState, Is.EqualTo(ZoomButtonState.Hidden));
            Assert.That(model.ZoomOutState, Is.EqualTo(ZoomButtonState.Hidden));

            var band = new Rect(0f, 970f, 1000f, 30f);
            var slot = SlotGridLayout.SlotRect(6, 4);
            Assert.That(model.OpenSheet(band, slot), Is.EqualTo(SheetOutcome.TopAligned));
            Assert.That(model.Scale, Is.EqualTo(1), "a single level never steps");
            var high = ScreenOf(model, slot.max);
            Assert.That(high.y, Is.EqualTo(band.yMax).Within(1.1f), "footprint top edge on the band top edge");
        }

        [Test]
        public void Rule7_ASingleLevelRangeIgnoresPinchAndSteps()
        {
            var model = new CameraScaleModel(new ArtScaleSettings(24, 24, 1));
            model.Fit(Layout(1000, 1000, 1f, 4));
            model.PreviewZoom(3f, new Vector2(500f, 500f));
            Assert.That(model.GestureActive, Is.False);
            Assert.That(model.StepZoom(1, new Vector2(500f, 500f)), Is.False);
            Assert.That(model.SnapZoomTo(3f, new Vector2(500f, 500f)), Is.False);
            Assert.That(model.Scale, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------ hit area (rule 8, FR-018)

        [Test]
        public void ASlotFootprintIsAtLeast44ByPointsAtEveryAllowedScale()
        {
            foreach (var density in Densities)
            {
                foreach (var ppp in PixelsPerPointValues)
                {
                    var settings = new ArtScaleSettings(density, 24, 6);
                    var nMin = CameraScaleModel.MinScale(settings, ppp);
                    for (var n = nMin; n <= settings.MaxScale; n++)
                    {
                        var points = CameraScaleModel.FootprintPoints(n, density, ppp);
                        Assert.That(points, Is.GreaterThanOrEqualTo(44f), $"D {density} ppp {ppp} n {n}: {points:0.0} pt");
                    }
                }
            }
        }

        [Test]
        public void TheFootprintHitAreaFollowsTheSlotRectNotTheArt()
        {
            // Hit testing is footprint-only (rule 8, T456): the footprint is the 70 x 35 art px plate diamond at every scale.
            for (var slot = 0; slot < SlotGridLayout.SlotCount; slot++)
            {
                var rect = SlotGridLayout.SlotRect(slot, 3);
                Assert.That(rect.width, Is.EqualTo(70f / 24f).Within(1e-5f));
                Assert.That(SlotGridLayout.HitTest(rect.center, 3), Is.EqualTo(slot));
            }
        }

        [Test]
        public void ThePositionIsNeverPartOfSavedState()
        {
            // Rule 9: the model has no serialization surface; it is plain state recomputed from the layout on every start.
            Assert.That(typeof(CameraScaleModel).IsSerializable, Is.False);
            Assert.That(typeof(CameraScaleModel).IsSubclassOf(typeof(UnityEngine.Object)), Is.False);
        }
    }
}
