using System;
using IdleFactory.Unity.Input;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// The sole writer of the factory camera (contracts/camera-scale.md): it applies the integer scale and the pixel-snapped position
    /// that <see cref="CameraScaleModel"/> decides, as <c>orthographicSize = pixelHeight / (2 n D)</c> and a camera position on the
    /// device pixel grid. Zoom is a whole-number level: wheel notches and zoom buttons step one level, a pinch snaps to the nearest
    /// allowed level on gesture end. A pinch (<see cref="NavigationIntent.IsGesture"/>) shows a continuous preview with the anchor held
    /// until <see cref="NavigationIntentKind.ZoomGestureEnded"/> snaps it instantly; a second world touch, a layout change or a sheet
    /// cancels the preview. Every change is instant at rest; nothing is saved.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FactoryCameraController : MonoBehaviour
    {
        /// <summary>A zoom intent at least this large is one wheel notch (or button press) and steps one level.</summary>
        public const float StepIntentThreshold = 0.95f;

        private const float PinchSensitivity = 0.35f;
        private const float PinchIdleSeconds = 0.3f;

        [SerializeField] private Camera factoryCamera;

        private CameraScaleModel model;
        private Action<string> log;
        private Vector2 panRemainder;
        private float pinchScale;
        private bool pinchPreviewActive;
        private float pinchPreviewScale;
        private float lastPinchTime = float.NegativeInfinity;

        /// <summary>The scale model, or null before <see cref="Configure"/>.</summary>
        public CameraScaleModel Model => model;

        /// <summary>True once a layout has been fitted; no pan or zoom applies before.</summary>
        public bool HasLayout => model != null && model.HasLayout;

        /// <summary>The integer scale at rest (device pixels per art pixel), or 0 before the first fit.</summary>
        public int Scale => HasLayout ? model.Scale : 0;

        public ZoomButtonState ZoomInState => HasLayout ? model.ZoomInState : ZoomButtonState.Hidden;

        public ZoomButtonState ZoomOutState => HasLayout ? model.ZoomOutState : ZoomButtonState.Hidden;

        public bool SheetOpen => model != null && model.SheetOpen;

        /// <summary>True while a pinch preview runs (rule 4); the scale at rest is unchanged until the gesture ends.</summary>
        public bool PinchPreviewActive => pinchPreviewActive;

        /// <summary>The continuous scale on screen: the pinch preview while one runs, otherwise the integer scale at rest.</summary>
        public float DisplayScale => HasLayout ? (pinchPreviewActive ? pinchPreviewScale : model.Scale) : 0f;

        /// <summary>Raised after the camera was written with a different scale (a zoom, a fit or a sheet step).</summary>
        public event Action ScaleChanged;

        /// <summary>Binds the camera and the Art Bible inputs. The camera does not move until the first <see cref="Fit"/>.</summary>
        public void Configure(Camera camera, ArtScaleSettings settings, Action<string> logSink = null)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            factoryCamera = camera;
            factoryCamera.orthographic = true;
            log = logSink ?? (message => UnityEngine.Debug.LogWarning(message));
            model = new CameraScaleModel(settings, log);
            panRemainder = Vector2.zero;
            lastPinchTime = float.NegativeInfinity;
            pinchPreviewActive = false;
        }

        /// <summary>Fits the factory for a layout (first resolved layout, rotation): the largest allowed scale that fits, centred.</summary>
        public void Fit(ScaleLayout layout)
        {
            EnsureModel();
            var before = model.HasLayout ? model.Scale : 0;
            pinchPreviewActive = false;
            model.Fit(layout);
            panRemainder = Vector2.zero;
            Write(before);
        }

        /// <summary>A resize or HUD change: re-fits only when the current scale no longer fits (rule 5).</summary>
        public void UpdateLayout(ScaleLayout layout)
        {
            EnsureModel();
            var before = model.HasLayout ? model.Scale : 0;
            panRemainder = Vector2.zero;
            pinchPreviewActive = false;
            model.OnLayoutChanged(layout);
            Write(before);
        }

        /// <summary>A bottom sheet or side panel opened over <paramref name="band"/> (screen pixels): keeps the slot in view (rule 7).</summary>
        public SheetOutcome OpenSheet(Rect band, Rect slotWorldRect)
        {
            EnsureModel();
            var before = Scale;
            panRemainder = Vector2.zero;
            CancelPinch();
            var outcome = model.OpenSheet(band, slotWorldRect);
            Write(before);
            return outcome;
        }

        /// <summary>The sheet closed: the prior view returns unless the player moved meanwhile (rule 7).</summary>
        public void CloseSheet(Rect fullRect)
        {
            EnsureModel();
            var before = Scale;
            panRemainder = Vector2.zero;
            CancelPinch();
            model.CloseSheet(fullRect);
            Write(before);
        }

        public void HandleNavigationIntent(NavigationIntent intent)
        {
            switch (intent.Kind)
            {
                case NavigationIntentKind.Pan:
                    PanByScreenDelta(intent.Delta);
                    break;
                case NavigationIntentKind.Zoom:
                    if (intent.IsGesture)
                    {
                        PreviewPinch(intent.ZoomDelta, intent.Position);
                    }
                    else
                    {
                        ZoomAtScreenPosition(intent.ZoomDelta, intent.Position);
                    }

                    break;
                case NavigationIntentKind.ZoomGestureEnded:
                    EndPinch();
                    break;
                case NavigationIntentKind.CancelWorldGesture:
                    CancelPinch();
                    break;
            }
        }

        /// <summary>
        /// One pinch increment: moves the continuous preview scale (clamped to the allowed range) with the world point under the
        /// first gesture position held. The rest scale and the pan limits do not change until <see cref="EndPinch"/>. No effect in
        /// a single-level range.
        /// </summary>
        public void PreviewPinch(float zoomDelta, Vector2 screenPosition)
        {
            if (float.IsNaN(zoomDelta) || float.IsInfinity(zoomDelta))
            {
                throw new ArgumentOutOfRangeException(nameof(zoomDelta));
            }

            if (!HasLayout || zoomDelta == 0f || model.LevelCount <= 1)
            {
                return;
            }

            if (!pinchPreviewActive)
            {
                pinchPreviewActive = true;
                pinchPreviewScale = model.Scale;
                panRemainder = Vector2.zero;
            }

            pinchPreviewScale = Mathf.Clamp(pinchPreviewScale * Mathf.Exp(zoomDelta * PinchSensitivity), model.LowerBound, model.UpperBound);
            model.PreviewZoom(pinchPreviewScale, screenPosition);
            WritePreview();
        }

        /// <summary>Gesture end (rule 4): the preview snaps instantly to the nearest allowed level (a tie goes to the larger), anchor held.</summary>
        public void EndPinch()
        {
            if (!pinchPreviewActive)
            {
                return;
            }

            pinchPreviewActive = false;
            var before = model.Scale;
            model.EndGesture();
            Write(before);
        }

        /// <summary>Rule 5: a second world touch, an interruption or a sheet cancels the preview and restores the view before the pinch.</summary>
        public void CancelPinch()
        {
            if (!pinchPreviewActive)
            {
                return;
            }

            pinchPreviewActive = false;
            var before = model.Scale;
            model.CancelGesture();
            Write(before);
        }

        /// <summary>Pans by a finger or pointer delta in screen pixels; the content follows the pointer. Sub-pixel deltas accumulate.</summary>
        public void PanByScreenDelta(Vector2 screenDelta)
        {
            if (!HasLayout)
            {
                return;
            }

            var k = model.Scale * (float)model.Settings.ArtPixelsPerCell;
            PanByWorldDelta(new Vector2(-screenDelta.x / k, -screenDelta.y / k));
        }

        public void PanByWorldDelta(Vector2 worldDelta)
        {
            if (!HasLayout || pinchPreviewActive)
            {
                return;
            }

            var total = panRemainder + worldDelta;
            var before = model.Position;
            model.Pan(total);
            var rest = total - (model.Position - before);

            // Architect A5: only a sub-pixel remainder survives. Whatever a pan limit held back (a fast swipe past the edge) is dropped
            // per axis, so dragging back moves the camera at once instead of first paying off a hidden excess.
            var step = 1f / (model.Scale * (float)model.Settings.ArtPixelsPerCell);
            panRemainder = new Vector2(Mathf.Abs(rest.x) < step ? rest.x : 0f, Mathf.Abs(rest.y) < step ? rest.y : 0f);
            Write(model.Scale);
        }

        /// <summary>
        /// A wheel notch or button press (an intent of at least <see cref="StepIntentThreshold"/>) steps one level about the screen position; a
        /// smaller pinch increment accumulates continuously and snaps to the nearest allowed level at once.
        /// </summary>
        public void ZoomAtScreenPosition(float zoomDelta, Vector2 screenPosition)
        {
            if (float.IsNaN(zoomDelta) || float.IsInfinity(zoomDelta))
            {
                throw new ArgumentOutOfRangeException(nameof(zoomDelta));
            }

            if (!HasLayout || zoomDelta == 0f || pinchPreviewActive)
            {
                return;
            }

            var before = model.Scale;
            panRemainder = Vector2.zero;
            if (Mathf.Abs(zoomDelta) >= StepIntentThreshold)
            {
                lastPinchTime = float.NegativeInfinity;
                model.StepZoom(zoomDelta > 0f ? 1 : -1, screenPosition);
            }
            else
            {
                var now = Time.unscaledTime;
                if (now - lastPinchTime > PinchIdleSeconds)
                {
                    pinchScale = model.Scale;
                }

                lastPinchTime = now;
                pinchScale = Mathf.Clamp(pinchScale * Mathf.Exp(zoomDelta * PinchSensitivity), model.LowerBound, model.UpperBound);
                model.SnapZoomTo(pinchScale, screenPosition);
            }

            Write(before);
        }

        /// <summary>The visible zoom controls: one level in or out about the centre of the unobstructed area.</summary>
        public void ZoomFromVisibleControl(int direction)
        {
            if (direction == 0 || !HasLayout)
            {
                return;
            }

            CancelPinch();
            var before = model.Scale;
            panRemainder = Vector2.zero;
            model.StepZoom(direction, model.Layout.UnobstructedRect.center);
            Write(before);
        }

        /// <summary>The preview write: a fractional orthographic size and the anchor-held position, never kept at rest.</summary>
        private void WritePreview()
        {
            var layout = model.Layout;
            factoryCamera.orthographic = true;
            factoryCamera.orthographicSize = layout.PixelHeight / (2f * pinchPreviewScale * model.Settings.ArtPixelsPerCell);
            var preview = model.PreviewPosition;
            factoryCamera.transform.position = new Vector3(preview.x, preview.y, factoryCamera.transform.position.z);
        }

        private void Awake()
        {
            EnsureCamera();
            factoryCamera.orthographic = true;
        }

        private void EnsureCamera()
        {
            if (factoryCamera == null)
            {
                factoryCamera = GetComponent<Camera>();
            }

            if (factoryCamera == null)
            {
                throw new InvalidOperationException("FactoryCameraController requires a Camera.");
            }
        }

        private void EnsureModel()
        {
            EnsureCamera();
            if (model == null)
            {
                throw new InvalidOperationException("FactoryCameraController is not configured.");
            }
        }

        /// <summary>The only place that writes the camera: the orthographic size of rule 1 and the pixel-snapped position.</summary>
        private void Write(int scaleBefore)
        {
            if (!HasLayout)
            {
                return;
            }

            var layout = model.Layout;
            factoryCamera.orthographic = true;
            factoryCamera.orthographicSize = CameraScaleModel.OrthographicSize(layout.PixelHeight, model.Scale, model.Settings.ArtPixelsPerCell);
            var z = factoryCamera.transform.position.z;
            factoryCamera.transform.position = new Vector3(model.Position.x, model.Position.y, z);
            if (model.Scale != scaleBefore)
            {
                ScaleChanged?.Invoke();
            }
        }
    }
}
