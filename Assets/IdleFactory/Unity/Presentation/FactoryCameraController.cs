using System;
using IdleFactory.Unity.Input;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// Owns replaceable development camera limits. It changes only the orthographic camera transform.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FactoryCameraController : MonoBehaviour
    {
        [SerializeField] private Camera factoryCamera;
        [SerializeField, Min(0.01f)] private float minimumOrthographicSize = 2f;
        [SerializeField, Min(0.01f)] private float maximumOrthographicSize = 12f;
        [SerializeField, Min(0.01f)] private float zoomSensitivity = 0.35f;
        [SerializeField] private Vector2 panPadding = new Vector2(1f, 1f);
        [SerializeField] private Rect factoryWorldBounds = new Rect(0f, 0f, 16f, 16f);

        public float MinimumOrthographicSize => minimumOrthographicSize;
        public float MaximumOrthographicSize => maximumOrthographicSize;
        public Rect FactoryWorldBounds => factoryWorldBounds;

        public void Configure(
            Camera camera,
            Rect worldBounds,
            float minimumSize,
            float maximumSize,
            Vector2 padding)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (!camera.orthographic)
            {
                throw new ArgumentException("Factory camera must be orthographic.", nameof(camera));
            }

            ValidateBounds(worldBounds);
            ValidateSizes(minimumSize, maximumSize);
            ValidatePadding(padding);

            factoryCamera = camera;
            factoryWorldBounds = worldBounds;
            minimumOrthographicSize = minimumSize;
            maximumOrthographicSize = maximumSize;
            panPadding = padding;
            factoryCamera.orthographicSize = Mathf.Clamp(
                factoryCamera.orthographicSize,
                minimumOrthographicSize,
                maximumOrthographicSize);
            ClampCurrentPosition();
        }

        public void HandleNavigationIntent(NavigationIntent intent)
        {
            switch (intent.Kind)
            {
                case NavigationIntentKind.Pan:
                    PanByScreenDelta(intent.Delta);
                    break;
                case NavigationIntentKind.Zoom:
                    ZoomAtScreenPosition(intent.ZoomDelta, intent.Position);
                    break;
            }
        }

        public void PanByScreenDelta(Vector2 screenDelta)
        {
            EnsureCamera();
            var pixelRect = factoryCamera.pixelRect;
            if (pixelRect.width <= 0f || pixelRect.height <= 0f)
            {
                return;
            }

            var worldHeight = factoryCamera.orthographicSize * 2f;
            var worldWidth = worldHeight * factoryCamera.aspect;
            var worldDelta = new Vector2(
                -screenDelta.x * worldWidth / pixelRect.width,
                -screenDelta.y * worldHeight / pixelRect.height);
            PanByWorldDelta(worldDelta);
        }

        public void PanByWorldDelta(Vector2 worldDelta)
        {
            EnsureCamera();
            var requested = factoryCamera.transform.position + new Vector3(worldDelta.x, worldDelta.y, 0f);
            factoryCamera.transform.position = ClampPosition(requested, factoryCamera.aspect);
        }

        public void PanTo(Vector2 worldCenter)
        {
            EnsureCamera();
            var current = factoryCamera.transform.position;
            factoryCamera.transform.position = ClampPosition(
                new Vector3(worldCenter.x, worldCenter.y, current.z),
                factoryCamera.aspect);
        }

        public void ZoomAtScreenPosition(float zoomDelta, Vector2 screenPosition)
        {
            EnsureCamera();
            var worldPoint = factoryCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, -factoryCamera.transform.position.z));
            ZoomAtWorldPoint(zoomDelta, worldPoint);
        }

        public void ZoomAtWorldPoint(float zoomDelta, Vector2 worldAnchor)
        {
            EnsureCamera();
            if (float.IsNaN(zoomDelta) || float.IsInfinity(zoomDelta))
            {
                throw new ArgumentOutOfRangeException(nameof(zoomDelta));
            }

            var oldSize = factoryCamera.orthographicSize;
            var newSize = Mathf.Clamp(
                oldSize * Mathf.Exp(-zoomDelta * zoomSensitivity),
                minimumOrthographicSize,
                maximumOrthographicSize);
            if (Mathf.Approximately(oldSize, newSize))
            {
                ClampCurrentPosition();
                return;
            }

            var cameraPosition = factoryCamera.transform.position;
            var scale = newSize / oldSize;
            var anchoredCenter = worldAnchor + ((Vector2)cameraPosition - worldAnchor) * scale;
            factoryCamera.orthographicSize = newSize;
            factoryCamera.transform.position = ClampPosition(
                new Vector3(anchoredCenter.x, anchoredCenter.y, cameraPosition.z),
                factoryCamera.aspect);
        }

        public void ZoomFromVisibleControl(int direction)
        {
            if (direction == 0)
            {
                return;
            }

            EnsureCamera();
            ZoomAtScreenPosition(
                Math.Sign(direction),
                new Vector2(factoryCamera.pixelRect.center.x, factoryCamera.pixelRect.center.y));
        }

        public Vector3 ClampPosition(Vector3 requested, float viewportAspect)
        {
            EnsureCamera();
            if (float.IsNaN(viewportAspect) || float.IsInfinity(viewportAspect) || viewportAspect <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(viewportAspect));
            }

            var halfHeight = factoryCamera.orthographicSize;
            var halfWidth = halfHeight * viewportAspect;
            var minimumX = factoryWorldBounds.xMin - panPadding.x + halfWidth;
            var maximumX = factoryWorldBounds.xMax + panPadding.x - halfWidth;
            var minimumY = factoryWorldBounds.yMin - panPadding.y + halfHeight;
            var maximumY = factoryWorldBounds.yMax + panPadding.y - halfHeight;

            return new Vector3(
                ClampOrCenter(requested.x, minimumX, maximumX, factoryWorldBounds.center.x),
                ClampOrCenter(requested.y, minimumY, maximumY, factoryWorldBounds.center.y),
                requested.z);
        }

        private void Awake()
        {
            EnsureCamera();
            factoryCamera.orthographic = true;
            factoryCamera.orthographicSize = Mathf.Clamp(
                factoryCamera.orthographicSize,
                minimumOrthographicSize,
                maximumOrthographicSize);
            ClampCurrentPosition();
        }

        private void OnValidate()
        {
            minimumOrthographicSize = Mathf.Max(0.01f, minimumOrthographicSize);
            maximumOrthographicSize = Mathf.Max(minimumOrthographicSize, maximumOrthographicSize);
            zoomSensitivity = Mathf.Max(0.01f, zoomSensitivity);
            panPadding = Vector2.Max(Vector2.zero, panPadding);
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

        private void ClampCurrentPosition()
        {
            factoryCamera.transform.position = ClampPosition(factoryCamera.transform.position, factoryCamera.aspect);
        }

        private static float ClampOrCenter(float requested, float minimum, float maximum, float center)
        {
            return minimum <= maximum ? Mathf.Clamp(requested, minimum, maximum) : center;
        }

        private static void ValidateBounds(Rect bounds)
        {
            if (bounds.width <= 0f || bounds.height <= 0f ||
                !IsFinite(bounds.xMin) || !IsFinite(bounds.yMin) ||
                !IsFinite(bounds.xMax) || !IsFinite(bounds.yMax))
            {
                throw new ArgumentOutOfRangeException(nameof(bounds));
            }
        }

        private static void ValidateSizes(float minimum, float maximum)
        {
            if (!IsFinite(minimum) || !IsFinite(maximum) || minimum <= 0f || maximum < minimum)
            {
                throw new ArgumentOutOfRangeException(nameof(minimum));
            }
        }

        private static void ValidatePadding(Vector2 padding)
        {
            if (!IsFinite(padding.x) || !IsFinite(padding.y) || padding.x < 0f || padding.y < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(padding));
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
