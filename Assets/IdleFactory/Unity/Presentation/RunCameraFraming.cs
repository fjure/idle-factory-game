using System;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// Frames the 12-slot grid for the run screen: derives the camera's zoom limits from the replaceable pixels-per-cell
    /// settings, fits all slots into the world area the shell leaves uncovered, and keeps a selected slot in view above an
    /// open sheet. It only moves and zooms the camera; no gameplay state is read or written.
    /// </summary>
    public sealed class RunCameraFraming
    {
        private readonly Camera camera;
        private readonly FactoryCameraController controller;
        private readonly RunZoomSettings zoom;

        public RunCameraFraming(Camera camera, FactoryCameraController controller, RunZoomSettings zoom)
        {
            this.camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            this.controller = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            this.zoom = zoom ?? throw new ArgumentNullException(nameof(zoom));
        }

        public RunZoomSettings Zoom => zoom;

        /// <summary>The projected size of one cell in panel points at the camera's current zoom.</summary>
        public static float PixelsPerCell(Camera camera, float pixelsPerPoint)
        {
            if (camera == null || camera.orthographicSize <= 0f || pixelsPerPoint <= 0f)
            {
                return 0f;
            }

            return camera.pixelHeight / pixelsPerPoint / (2f * camera.orthographicSize);
        }

        /// <summary>Sets the pan bounds to the grid extent and the orthographic limits from the pixels-per-cell limits.</summary>
        public void ConfigureLimits(int columns, float pixelsPerPoint)
        {
            var heightPoints = Mathf.Max(1f, camera.pixelHeight / SafeScale(pixelsPerPoint));
            var minimumSize = heightPoints / (2f * zoom.maximumPixelsPerCell);
            var maximumSize = heightPoints / (2f * zoom.minimumPixelsPerCell);
            camera.orthographic = true;
            controller.Configure(camera, SlotGridLayout.Extent(columns), minimumSize, maximumSize, new Vector2(1f, 1f));
        }

        /// <summary>
        /// Zooms so the whole grid plus a half-cell margin fits <paramref name="visibleScreenArea"/> (screen pixels, bottom-left
        /// origin), never below the fit-all floor, then centres the grid in that area. Returns the resulting pixels per cell.
        /// </summary>
        public float FitAll(Rect visibleScreenArea, int columns, float pixelsPerPoint)
        {
            var scale = SafeScale(pixelsPerPoint);
            var fit = SlotGridLayout.FitAllPixelsPerCell(visibleScreenArea.width / scale, visibleScreenArea.height / scale, columns);
            var ppc = zoom.Clamp(Mathf.Max(fit, zoom.fitAllFloorPixelsPerCell));
            camera.orthographicSize = camera.pixelHeight / scale / (2f * ppc);
            controller.SetUnobstructedScreenRect(visibleScreenArea);
            controller.FrameContent(SlotGridLayout.Extent(columns));
            return ppc;
        }

        /// <summary>Moves the camera so a slot sits at the centre of the area the shell leaves uncovered (zoom unchanged).</summary>
        public void FrameSlot(int slot, int columns)
        {
            controller.FrameContent(SlotGridLayout.SlotRect(slot, columns));
        }

        private static float SafeScale(float pixelsPerPoint) =>
            pixelsPerPoint <= 0f || float.IsNaN(pixelsPerPoint) || float.IsInfinity(pixelsPerPoint) ? 1f : pixelsPerPoint;
    }
}
