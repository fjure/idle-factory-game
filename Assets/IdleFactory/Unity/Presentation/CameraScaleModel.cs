using System;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>The Art Bible inputs of the integer camera scale (contracts/camera-scale.md, data-model CameraScaleSettings).</summary>
    public readonly struct ArtScaleSettings
    {
        public ArtScaleSettings(int artPixelsPerCell, int readableCellPoints, int maxScale)
        {
            ArtPixelsPerCell = Math.Max(1, artPixelsPerCell);
            ReadableCellPoints = Math.Max(1, readableCellPoints);
            MaxScale = Math.Max(1, maxScale);
        }

        /// <summary>D: art pixels per cell (the sprite pixels per unit).</summary>
        public int ArtPixelsPerCell { get; }

        /// <summary>The smallest cell size in UI points at which machines stay identifiable (initial 24, F003 Z24).</summary>
        public int ReadableCellPoints { get; }

        /// <summary>The largest allowed integer scale (initial 6).</summary>
        public int MaxScale { get; }
    }

    /// <summary>The screen facts a fit needs. All pixel values are physical pixels; the rectangle has a bottom-left origin.</summary>
    public readonly struct ScaleLayout
    {
        public ScaleLayout(int pixelWidth, int pixelHeight, float pixelsPerPoint, Rect unobstructedRect, int columns)
        {
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            PixelsPerPoint = pixelsPerPoint > 0f && !float.IsNaN(pixelsPerPoint) && !float.IsInfinity(pixelsPerPoint) ? pixelsPerPoint : 1f;
            UnobstructedRect = unobstructedRect;
            Columns = Math.Max(1, columns);
        }

        public int PixelWidth { get; }
        public int PixelHeight { get; }
        public float PixelsPerPoint { get; }
        public Rect UnobstructedRect { get; }
        public int Columns { get; }

        /// <summary>The identity of a layout for the once-per-layout log: the rectangle changes with a sheet, the screen does not.</summary>
        internal string Key => $"{PixelWidth}x{PixelHeight}@{PixelsPerPoint:0.####}/{Columns}";

        internal ScaleLayout WithRect(Rect rect) => new ScaleLayout(PixelWidth, PixelHeight, PixelsPerPoint, rect, Columns);
    }

    /// <summary>What the model did when a bottom sheet opened (rule 7).</summary>
    public enum SheetOutcome
    {
        Unchanged = 0,
        Panned = 1,
        SteppedDown = 2,
        TopAligned = 3,
    }

    /// <summary>The state of a zoom button (rule 4).</summary>
    public enum ZoomButtonState
    {
        Hidden = 0,
        Enabled = 1,
        DisabledAtLimit = 2,
    }

    /// <summary>
    /// The integer camera scale of Feature 004 as pure arithmetic: no camera, scene or MonoBehaviour. The controller reads
    /// <see cref="Scale"/> and <see cref="Position"/> and writes them to the camera (T427). Scale n is device pixels per
    /// art pixel; the camera maps world point P to screen pixel C + (P - cam) * n * D with C the screen centre.
    /// </summary>
    public sealed class CameraScaleModel
    {
        public const string BelowFloorCode = "presentation.scale.belowFloor";
        private const float Epsilon = 1e-4f;

        /// <summary>
        /// Owner decision 2026-10-28: a floor miss under one percent is not a violation. n_min = ceil(exact - 0.01), so a
        /// 1280x720 phone at 320 dpi (2.0046 px/pt, exact 2.0046 at D = 24) keeps n_min 2 instead of 3.
        /// </summary>
        public const float MinScaleTolerance = 0.01f;

        /// <summary>
        /// One cell of pan slack added to the relaxed pan limits while a sheet is open: the half-cell margin on each side of
        /// the slot footprint, so the footprint itself (not its margin) can reach the band edge.
        /// </summary>
        private const float SheetRelaxSlackWorld = 2f * SlotGridLayout.HalfCellMargin;

        private readonly Action<string> log;
        private ScaleLayout layout;
        private bool hasLayout;
        private string loggedLayoutKey;
        private int scale = 1;
        private Vector2 position;
        private int lowerBound = 1;
        private int fitScale = 1;
        private bool belowFloor;

        // Gesture (rules 4 and 5).
        private bool gestureActive;
        private float previewScale;
        private Vector2 gestureStartPosition;
        private int gestureStartScale;
        private Vector2 gestureAnchorScreen;
        private Vector2 gestureAnchorWorld;

        // Sheet (rule 7).
        private bool sheetOpen;
        private int sheetPriorScale;
        private Vector2 sheetPriorPosition;
        private bool sheetTouchedByPlayer;
        private float sheetRelaxWorld;
        private float sheetFullHeight;

        public CameraScaleModel(ArtScaleSettings settings, Action<string> log = null)
        {
            Settings = settings;
            this.log = log;
        }

        public ArtScaleSettings Settings { get; }

        public bool HasLayout => hasLayout;
        public ScaleLayout Layout => layout;

        /// <summary>The integer scale at rest (device pixels per art pixel).</summary>
        public int Scale => scale;

        /// <summary>The camera position in world units at rest, on the device pixel grid.</summary>
        public Vector2 Position => position;

        /// <summary>n_fit of the last fit: the largest scale in the allowed range at which the factory fits the rectangle.</summary>
        public int FitScale => fitScale;

        /// <summary>True when the runtime degrade is active: the floor could not be met and n is the largest that fits.</summary>
        public bool BelowFloor => belowFloor;

        /// <summary>The smallest allowed scale for the current layout (n_min, or the degraded fit).</summary>
        public int LowerBound => lowerBound;

        public int UpperBound => Settings.MaxScale;
        public int LevelCount => Math.Max(1, UpperBound - lowerBound + 1);
        public bool IsSingleLevel => LevelCount <= 1;
        public bool GestureActive => gestureActive;
        public bool SheetOpen => sheetOpen;

        // ---------------------------------------------------------------- pure functions (rules 1-3)

        /// <summary>Rule 2: n_min = ceil(readableCellPoints * pxPerPoint / D - 0.01), at least 1 (1 % tolerance, owner decision 2026-10-28).</summary>
        public static int MinScale(ArtScaleSettings settings, float pixelsPerPoint)
        {
            var exact = settings.ReadableCellPoints * pixelsPerPoint / settings.ArtPixelsPerCell;
            return Math.Max(1, (int)Math.Ceiling(exact - MinScaleTolerance));
        }

        /// <summary>Rule 1: orthographicSize = pixelHeight / (2 * n * D).</summary>
        public static float OrthographicSize(int pixelHeight, int scale, int artPixelsPerCell) =>
            pixelHeight / (2f * scale * artPixelsPerCell);

        /// <summary>
        /// Rules 1 and 2: snaps a camera coordinate to the device pixel grid (a multiple of 1 / (n * D)). An odd pixel extent
        /// puts the screen centre on a pixel centre, so the grid is offset by half a device pixel; that keeps every art pixel edge
        /// on a device pixel edge (the contract words this as half an art pixel, which is the same for n = 1).
        /// </summary>
        public static float SnapCoordinate(float value, int pixelExtent, int scale, int artPixelsPerCell)
        {
            var k = (double)scale * artPixelsPerCell;
            var half = (pixelExtent & 1) == 1 ? 0.5 : 0.0;
            return (float)((Math.Round(value * k - half, MidpointRounding.AwayFromZero) + half) / k);
        }

        /// <summary>Whether a camera coordinate is on the pixel grid of <see cref="SnapCoordinate"/>.</summary>
        public static bool IsOnPixelGrid(float value, int pixelExtent, int scale, int artPixelsPerCell)
        {
            var k = (double)scale * artPixelsPerCell;
            var half = (pixelExtent & 1) == 1 ? 0.5 : 0.0;
            var units = value * k - half;
            return Math.Abs(units - Math.Round(units)) < 2e-2;
        }

        /// <summary>Rule 3: the largest n at which the factory extent plus a half-cell margin fits the rectangle (0 when not even n = 1 fits).</summary>
        public static int LargestFittingScale(Rect rect, int columns, int artPixelsPerCell)
        {
            var extent = SlotGridLayout.Extent(columns);
            var widthCells = extent.width + 2f * SlotGridLayout.HalfCellMargin;
            var heightCells = extent.height + 2f * SlotGridLayout.HalfCellMargin;
            var byWidth = rect.width / (widthCells * artPixelsPerCell);
            var byHeight = rect.height / (heightCells * artPixelsPerCell);
            return (int)Math.Floor(Math.Min(byWidth, byHeight) + Epsilon);
        }

        /// <summary>The share of the rectangle not covered by the factory extent at a scale: the tighter of the two axes (0..1).</summary>
        public static float MarginFraction(Rect rect, int columns, int scale, int artPixelsPerCell)
        {
            var extent = SlotGridLayout.Extent(columns);
            var k = scale * artPixelsPerCell;
            var width = rect.width <= 0f ? 0f : 1f - extent.width * k / rect.width;
            var height = rect.height <= 0f ? 0f : 1f - extent.height * k / rect.height;
            return Math.Min(width, height);
        }

        /// <summary>The size in UI points of a slot footprint (2 cells) at a scale; the hit area must be at least 44 points.</summary>
        public static float FootprintPoints(int scale, int artPixelsPerCell, float pixelsPerPoint) =>
            SlotGridLayout.SlotSize * scale * artPixelsPerCell / pixelsPerPoint;

        // ---------------------------------------------------------------- layout and fit (rules 2, 3, 5)

        /// <summary>
        /// Fits the factory for a layout: computes the allowed range, picks n_fit (or the runtime degrade), and centres the
        /// factory in the rectangle. Call it once for the first resolved layout. A fit ends an open sheet without restoring its
        /// prior view: the fit replaces that view (the caller re-opens the sheet when its slot is still selected).
        /// </summary>
        public void Fit(ScaleLayout newLayout)
        {
            CancelGesture();
            sheetOpen = false;
            ApplyLayout(newLayout);
            scale = fitScale;
            position = Constrain(CenteredPosition(scale), scale);
            LogBelowFloorOnce();
        }

        /// <summary>
        /// A rotation, resize or HUD change: re-fits only when the current scale no longer fits (it left the allowed range, or it
        /// was a fit-all view and the factory no longer fits the rectangle); otherwise the scale stays and the position is only
        /// re-clamped. Returns true when the scale changed.
        /// </summary>
        public bool OnLayoutChanged(ScaleLayout newLayout)
        {
            if (!hasLayout)
            {
                Fit(newLayout);
                return true;
            }

            CancelGesture();
            var previousScale = scale;
            var wasFitAll = scale <= fitScale;
            ApplyLayout(newLayout);
            var largest = Math.Max(1, LargestFittingScale(newLayout.UnobstructedRect, newLayout.Columns, Settings.ArtPixelsPerCell));
            var outOfRange = scale < lowerBound || scale > UpperBound;
            if (outOfRange || (wasFitAll && scale > largest))
            {
                scale = fitScale;
                position = CenteredPosition(scale);
            }

            position = Constrain(position, scale);
            LogBelowFloorOnce();
            return scale != previousScale;
        }

        private void ApplyLayout(ScaleLayout newLayout)
        {
            layout = newLayout;
            hasLayout = true;
            var minimum = MinScale(Settings, newLayout.PixelsPerPoint);
            var largest = LargestFittingScale(newLayout.UnobstructedRect, newLayout.Columns, Settings.ArtPixelsPerCell);
            if (largest >= minimum && minimum <= Settings.MaxScale)
            {
                belowFloor = false;
                lowerBound = minimum;
                fitScale = Math.Min(largest, Settings.MaxScale);
            }
            else
            {
                // Runtime degrade: the largest n >= 1 that fits, the floor ignored, never a fractional scale.
                belowFloor = true;
                fitScale = Math.Max(1, Math.Min(largest, Settings.MaxScale));
                lowerBound = fitScale;
            }
        }

        private void LogBelowFloorOnce()
        {
            if (!belowFloor)
            {
                loggedLayoutKey = null;
                return;
            }

            if (loggedLayoutKey == layout.Key)
            {
                return;
            }

            loggedLayoutKey = layout.Key;
            log?.Invoke($"{BelowFloorCode}: n={fitScale} below n_min={MinScale(Settings, layout.PixelsPerPoint)} at {layout.PixelWidth}x{layout.PixelHeight} px, {layout.PixelsPerPoint:0.###} px/pt");
        }

        // ---------------------------------------------------------------- geometry helpers

        private float K(int n) => n * (float)Settings.ArtPixelsPerCell;

        private Vector2 ScreenCentre => new Vector2(layout.PixelWidth * 0.5f, layout.PixelHeight * 0.5f);

        /// <summary>The camera position that centres the factory extent in the rectangle at scale n (not yet snapped).</summary>
        private Vector2 CenteredPosition(int n)
        {
            var offset = (layout.UnobstructedRect.center - ScreenCentre) / K(n);
            return SlotGridLayout.Extent(layout.Columns).center - offset;
        }

        /// <summary>The world point under a screen pixel for a camera position and a (possibly fractional) scale.</summary>
        public Vector2 ScreenToWorld(Vector2 screen, Vector2 camera, float n) =>
            camera + (screen - ScreenCentre) / (n * Settings.ArtPixelsPerCell);

        public Vector2 WorldToScreen(Vector2 world, Vector2 camera, float n) =>
            ScreenCentre + (world - camera) * (n * Settings.ArtPixelsPerCell);

        /// <summary>Rule 6: clamps a camera position so the rectangle never shows more than a half-cell margin beyond the extent, then snaps it.</summary>
        private Vector2 Constrain(Vector2 camera, int n)
        {
            var rect = layout.UnobstructedRect;
            var relax = sheetOpen ? sheetRelaxWorld : 0f;
            var extent = SlotGridLayout.Extent(layout.Columns);
            var k = K(n);
            var rectCentreOffset = (rect.center - ScreenCentre) / k;
            var halfSpan = new Vector2(rect.width, rect.height) / (2f * k);
            var margin = SlotGridLayout.HalfCellMargin + relax;
            var centre = camera + rectCentreOffset;
            centre.x = ClampAxis(centre.x, extent.xMin - margin + halfSpan.x, extent.xMax + margin - halfSpan.x, extent.center.x);
            centre.y = ClampAxis(centre.y, extent.yMin - margin + halfSpan.y, extent.yMax + margin - halfSpan.y, extent.center.y);
            var result = centre - rectCentreOffset;
            return new Vector2(
                SnapCoordinate(result.x, layout.PixelWidth, n, Settings.ArtPixelsPerCell),
                SnapCoordinate(result.y, layout.PixelHeight, n, Settings.ArtPixelsPerCell));
        }

        private static float ClampAxis(float value, float low, float high, float centre) =>
            low > high ? centre : Math.Min(Math.Max(value, low), high);

        // ---------------------------------------------------------------- pan (rule 6)

        /// <summary>Pans by a world delta; the result is clamped to the pan limits and snapped. Returns true when the position changed.</summary>
        public bool Pan(Vector2 worldDelta)
        {
            if (!hasLayout || gestureActive)
            {
                return false;
            }

            var next = Constrain(position + worldDelta, scale);
            var changed = next != position;
            position = next;
            if (changed && sheetOpen)
            {
                sheetTouchedByPlayer = true;
            }

            return changed;
        }

        // ---------------------------------------------------------------- single-shot zoom (rule 4)

        public ZoomButtonState ZoomInState => IsSingleLevel ? ZoomButtonState.Hidden : scale < UpperBound ? ZoomButtonState.Enabled : ZoomButtonState.DisabledAtLimit;

        public ZoomButtonState ZoomOutState => IsSingleLevel ? ZoomButtonState.Hidden : scale > lowerBound ? ZoomButtonState.Enabled : ZoomButtonState.DisabledAtLimit;

        /// <summary>One wheel notch or one button press: steps one level about a screen anchor, with no preview. Returns true when the scale changed.</summary>
        public bool StepZoom(int direction, Vector2 anchorScreen)
        {
            if (!hasLayout || direction == 0 || gestureActive)
            {
                return false;
            }

            var target = Math.Min(Math.Max(scale + Math.Sign(direction), lowerBound), UpperBound);
            return SetScaleAboutAnchor(target, anchorScreen);
        }

        /// <summary>Zooms to the nearest allowed scale with the anchor held. Used by a pinch that snaps directly (T427 until T454).</summary>
        public bool SnapZoomTo(float continuousScale, Vector2 anchorScreen)
        {
            if (!hasLayout || IsSingleLevel)
            {
                return false;
            }

            return SetScaleAboutAnchor(NearestAllowed(continuousScale), anchorScreen);
        }

        private bool SetScaleAboutAnchor(int target, Vector2 anchorScreen)
        {
            if (target == scale)
            {
                return false;
            }

            var world = ScreenToWorld(anchorScreen, position, scale);
            var camera = world - (anchorScreen - ScreenCentre) / K(target);
            scale = target;
            position = Constrain(camera, scale);
            if (sheetOpen)
            {
                sheetTouchedByPlayer = true;
            }

            return true;
        }

        /// <summary>Rule 4: the nearest allowed scale to a continuous value; a tie goes to the larger.</summary>
        public int NearestAllowed(float continuousScale)
        {
            var rounded = (int)Math.Floor(continuousScale + 0.5f);
            return Math.Min(Math.Max(rounded, lowerBound), UpperBound);
        }

        // ---------------------------------------------------------------- pinch (rules 4 and 5)

        /// <summary>Starts or continues a pinch with a continuous preview. Has no effect in a single-level range.</summary>
        public void PreviewZoom(float continuousScale, Vector2 anchorScreen)
        {
            if (!hasLayout || IsSingleLevel)
            {
                return;
            }

            if (!gestureActive)
            {
                gestureActive = true;
                gestureStartScale = scale;
                gestureStartPosition = position;
                gestureAnchorScreen = anchorScreen;
                gestureAnchorWorld = ScreenToWorld(anchorScreen, position, scale);
            }

            previewScale = Math.Min(Math.Max(continuousScale, lowerBound), UpperBound);
        }

        /// <summary>The continuous preview scale while a pinch runs; otherwise the rest scale.</summary>
        public float DisplayScale => gestureActive ? previewScale : scale;

        /// <summary>The unsnapped camera position of the preview (anchor held); the controller shows it while the gesture runs.</summary>
        public Vector2 PreviewPosition =>
            gestureActive ? gestureAnchorWorld - (gestureAnchorScreen - ScreenCentre) / (previewScale * Settings.ArtPixelsPerCell) : position;

        /// <summary>Gesture end: snaps instantly to the nearest allowed scale (tie toward the larger) with the anchor held. The preview never persists.</summary>
        public bool EndGesture()
        {
            if (!gestureActive)
            {
                return false;
            }

            gestureActive = false;
            var target = NearestAllowed(previewScale);
            var camera = gestureAnchorWorld - (gestureAnchorScreen - ScreenCentre) / K(target);
            var changed = target != gestureStartScale;
            scale = target;
            position = Constrain(camera, scale);
            if (sheetOpen)
            {
                sheetTouchedByPlayer = true;
            }

            return changed;
        }

        /// <summary>Rule 5: a second world touch, rotation or layout change cancels the gesture and restores the pre-gesture state.</summary>
        public void CancelGesture()
        {
            if (!gestureActive)
            {
                return;
            }

            gestureActive = false;
            scale = gestureStartScale;
            position = gestureStartPosition;
        }

        // ---------------------------------------------------------------- bottom sheet (rule 7)

        /// <summary>
        /// A sheet opened: bandRect is the world band it leaves (screen pixels). The selected slot is first panned into the band
        /// (limits relaxed by the covered height), then the scale steps down to the largest n at or above n_min at which it fits.
        /// In a single-level range, or when no level fits, the slot footprint is aligned to the band top edge.
        /// </summary>
        public SheetOutcome OpenSheet(Rect bandRect, Rect slotWorldRect)
        {
            if (!hasLayout)
            {
                return SheetOutcome.Unchanged;
            }

            CancelGesture();
            var fullHeight = layout.UnobstructedRect.height;
            if (!sheetOpen)
            {
                sheetPriorScale = scale;
                sheetPriorPosition = position;
                sheetTouchedByPlayer = false;
                sheetFullHeight = fullHeight;
            }
            else
            {
                // The rectangle now holds a band; the unobstructed height is the one the first open saw.
                fullHeight = Math.Max(sheetFullHeight, bandRect.height);
            }

            sheetOpen = true;
            layout = layout.WithRect(bandRect);
            sheetRelaxWorld = Math.Max(0f, fullHeight - bandRect.height) / K(Math.Max(1, lowerBound)) + SheetRelaxSlackWorld;

            // Step 1: pan into the band at the current scale.
            if (FitsBand(slotWorldRect, scale, bandRect))
            {
                return PanSlotIntoBand(slotWorldRect, bandRect) ? SheetOutcome.Panned : SheetOutcome.Unchanged;
            }

            // Step 2: the largest n at or above n_min at which the slot fits the band.
            for (var n = scale - 1; n >= lowerBound; n--)
            {
                if (FitsBand(slotWorldRect, n, bandRect))
                {
                    scale = n;
                    PanSlotIntoBand(slotWorldRect, bandRect);
                    return SheetOutcome.SteppedDown;
                }
            }

            // Step 3 (single level, or no level fits): the footprint top edge on the band top edge, centred horizontally.
            scale = Math.Max(lowerBound, 1);
            AlignTop(slotWorldRect, bandRect);
            return SheetOutcome.TopAligned;
        }

        /// <summary>The sheet closed: the prior scale and position return unless the player zoomed or panned meanwhile.</summary>
        public void CloseSheet(Rect fullRect)
        {
            if (!sheetOpen)
            {
                return;
            }

            sheetOpen = false;
            layout = layout.WithRect(fullRect);
            if (!sheetTouchedByPlayer)
            {
                // The layout may have changed while the sheet was open: the restored scale stays inside the allowed range and
                // at or below the largest scale that fits (#520 follow-up 1).
                var restoredLargest = Math.Max(1, LargestFittingScale(fullRect, layout.Columns, Settings.ArtPixelsPerCell));
                var restored = Math.Min(Math.Max(sheetPriorScale, lowerBound), UpperBound);
                restored = Math.Min(restored, Math.Max(restoredLargest, lowerBound));
                if (restored == sheetPriorScale)
                {
                    scale = restored;
                    position = Constrain(sheetPriorPosition, scale);
                }
                else
                {
                    scale = restored;
                    position = Constrain(CenteredPosition(scale), scale);
                }

                return;
            }

            var largest = Math.Max(1, LargestFittingScale(fullRect, layout.Columns, Settings.ArtPixelsPerCell));
            if (scale <= fitScale && scale > largest)
            {
                scale = fitScale;
                position = CenteredPosition(scale);
            }

            position = Constrain(position, scale);
        }

        private bool FitsBand(Rect slotWorld, int n, Rect band)
        {
            var k = K(n);
            return slotWorld.width * k <= band.width + Epsilon && slotWorld.height * k <= band.height + Epsilon;
        }

        private bool PanSlotIntoBand(Rect slotWorld, Rect band)
        {
            var before = position;
            var low = WorldToScreen(slotWorld.min, position, scale);
            var high = WorldToScreen(slotWorld.max, position, scale);
            var dx = 0f;
            var dy = 0f;
            if (low.x < band.xMin)
            {
                dx = band.xMin - low.x;
            }
            else if (high.x > band.xMax)
            {
                dx = band.xMax - high.x;
            }

            if (low.y < band.yMin)
            {
                dy = band.yMin - low.y;
            }
            else if (high.y > band.yMax)
            {
                dy = band.yMax - high.y;
            }

            // Moving the camera by -d / k moves the content by +d on screen.
            position = Constrain(position - new Vector2(dx, dy) / K(scale), scale);
            return position != before;
        }

        private void AlignTop(Rect slotWorld, Rect band)
        {
            var k = K(scale);
            var target = new Vector2(band.center.x, band.yMax - slotWorld.height * k * 0.5f);
            position = Constrain(slotWorld.center - (target - ScreenCentre) / k, scale);
        }
    }
}
