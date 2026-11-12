using System;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>The allowed integer scales for a panel scale: <c>[Min, Max]</c> (contracts/camera-scale.md rule 2).</summary>
    public readonly struct ScaleRange
    {
        public ScaleRange(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public int Min { get; }
        public int Max { get; }
        public int LevelCount => Math.Max(1, Max - Min + 1);
    }

    /// <summary>
    /// The geometry of the run screen framing as pure functions of the Art Bible inputs and the screen facts: the allowed scale range,
    /// the fit-all scale of the world layout, the world rectangle of a slot, and the camera position that centres content in the area
    /// the shell leaves uncovered. It writes nothing: <see cref="FactoryCameraController"/> (through <see cref="CameraScaleModel"/>)
    /// is the only code that moves or sizes the camera (contracts/camera-scale.md, T427).
    /// </summary>
    public sealed class RunCameraFraming
    {
        private readonly RunZoomSettings zoom;

        public RunCameraFraming(RunZoomSettings zoom)
        {
            this.zoom = zoom ?? throw new ArgumentNullException(nameof(zoom));
        }

        public RunZoomSettings Zoom => zoom;

        public ArtScaleSettings ScaleSettings => zoom.ToScaleSettings();

        /// <summary>The projected size of one cell in panel points at the camera's current zoom.</summary>
        public static float PixelsPerCell(Camera camera, float pixelsPerPoint)
        {
            if (camera == null || camera.orthographicSize <= 0f || pixelsPerPoint <= 0f)
            {
                return 0f;
            }

            return camera.pixelHeight / pixelsPerPoint / (2f * camera.orthographicSize);
        }

        /// <summary>The screen facts a fit needs for a camera, the world layout, the panel scale and the unobstructed rectangle.</summary>
        public static ScaleLayout Layout(Camera camera, FactoryWorldLayout worldLayout, float pixelsPerPoint, Rect visibleScreenArea) =>
            new ScaleLayout(camera.pixelWidth, camera.pixelHeight, pixelsPerPoint, visibleScreenArea, worldLayout);

        /// <summary>The allowed scales <c>[n_min, maxScale]</c> for a panel scale (rule 2, with the 1 % tolerance).</summary>
        public ScaleRange ConfigureLimits(float pixelsPerPoint)
        {
            var settings = ScaleSettings;
            return new ScaleRange(CameraScaleModel.MinScale(settings, SafeScale(pixelsPerPoint)), settings.MaxScale);
        }

        /// <summary>
        /// Rule 3: the largest scale in the allowed range at which the grid plus a half-cell margin fits <paramref name="visibleScreenArea"/>
        /// (screen pixels, bottom-left origin). When not even <c>n_min</c> fits, the runtime degrade returns the largest scale of at
        /// least 1 that fits (never fractional).
        /// </summary>
        public int FitAll(Rect visibleScreenArea, FactoryWorldLayout worldLayout, float pixelsPerPoint)
        {
            var range = ConfigureLimits(pixelsPerPoint);
            var largest = CameraScaleModel.LargestFittingScale(visibleScreenArea, worldLayout.Bounds, zoom.artPixelsPerCell, worldLayout.Margin);
            if (largest >= range.Min && range.Min <= range.Max)
            {
                return Math.Min(largest, range.Max);
            }

            return Math.Max(1, Math.Min(largest, range.Max));
        }

        /// <summary>The world rectangle of a slot: the footprint a sheet has to keep in view.</summary>
        public Rect FrameSlot(int slot, FactoryWorldLayout worldLayout) => worldLayout.FootprintOf(new BuildingLocationId(slot));

        /// <summary>The world rectangle of the whole grid.</summary>
        public Rect FrameContent(FactoryWorldLayout worldLayout) => worldLayout.Bounds;

        /// <summary>
        /// The camera position (world units, not yet snapped to the pixel grid) that puts the centre of <paramref name="content"/> at the
        /// centre of <paramref name="visibleScreenArea"/> for a scale on a screen of <paramref name="screenPixels"/>.
        /// </summary>
        public Vector2 FrameContent(Rect content, Rect visibleScreenArea, Vector2 screenPixels, int scale)
        {
            var k = Math.Max(1, scale) * (float)zoom.artPixelsPerCell;
            return content.center - (visibleScreenArea.center - screenPixels * 0.5f) / k;
        }

        private static float SafeScale(float pixelsPerPoint) =>
            pixelsPerPoint <= 0f || float.IsNaN(pixelsPerPoint) || float.IsInfinity(pixelsPerPoint) ? 1f : pixelsPerPoint;
    }
}
