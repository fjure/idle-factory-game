using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Domain;
using IdleFactory.Unity.Input;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode
{
    public sealed class NavigationInputTests
    {
        [Test]
        public void MouseClickDragAndWheelProduceDistinctSemanticIntents()
        {
            var interpreter = new NavigationGestureInterpreter();
            var intents = Capture(interpreter);

            interpreter.BeginPointer(-1, new Vector2(20f, 30f), false, false);
            interpreter.EndPointer(-1, new Vector2(20f, 30f));
            interpreter.BeginPointer(-1, Vector2.zero, false, false);
            interpreter.MovePointer(-1, new Vector2(20f, 0f));
            interpreter.MovePointer(-1, new Vector2(25f, 5f));
            interpreter.EndPointer(-1, new Vector2(25f, 5f));
            interpreter.WheelZoom(new Vector2(40f, 50f), 120f, false);
            interpreter.WheelZoom(new Vector2(40f, 50f), 120f, true);

            Assert.That(intents.Count(intent => intent.Kind == NavigationIntentKind.WorldTap), Is.EqualTo(1));
            Assert.That(intents.Count(intent => intent.Kind == NavigationIntentKind.Pan), Is.EqualTo(2));
            Assert.That(intents.Single(intent => intent.Kind == NavigationIntentKind.Zoom).ZoomDelta, Is.EqualTo(1f));
        }

        [Test]
        public void OneTouchTapAndPanUseTheSameWorldSemanticsWithoutConfirmingGameplay()
        {
            var interpreter = new NavigationGestureInterpreter();
            var intents = Capture(interpreter);

            interpreter.BeginPointer(0, new Vector2(10f, 10f), false, true);
            interpreter.EndPointer(0, new Vector2(11f, 10f));
            interpreter.BeginPointer(0, Vector2.zero, false, true);
            interpreter.MovePointer(0, new Vector2(13f, 0f));
            interpreter.EndPointer(0, new Vector2(13f, 0f));

            Assert.That(intents.Select(intent => intent.Kind), Is.EqualTo(new[]
            {
                NavigationIntentKind.WorldTap,
                NavigationIntentKind.Pan,
            }));
        }

        [Test]
        public void PointerBeginningOnUiRemainsUiOwnedThroughRelease()
        {
            var interpreter = new NavigationGestureInterpreter();
            var intents = Capture(interpreter);

            interpreter.BeginPointer(0, Vector2.zero, true, true);
            interpreter.MovePointer(0, new Vector2(100f, 100f));
            interpreter.EndPointer(0, new Vector2(100f, 100f));

            Assert.That(intents, Is.Empty);
            Assert.That(interpreter.HasActiveGesture, Is.False);
        }

        [Test]
        public void SecondWorldTouchCancelsCandidateAndConsumesEveryPinchRelease()
        {
            var interpreter = new NavigationGestureInterpreter();
            var intents = Capture(interpreter);

            interpreter.BeginPointer(0, Vector2.zero, false, true);
            interpreter.BeginPointer(1, new Vector2(100f, 0f), false, true);
            interpreter.MovePointer(1, new Vector2(120f, 0f));
            interpreter.EndPointer(0, Vector2.zero);
            interpreter.BeginPointer(2, new Vector2(140f, 0f), false, true);
            interpreter.EndPointer(1, new Vector2(120f, 0f));
            interpreter.EndPointer(2, new Vector2(140f, 0f));

            Assert.That(intents.Count(intent => intent.Kind == NavigationIntentKind.CancelWorldGesture), Is.EqualTo(1));
            Assert.That(intents.Count(intent => intent.Kind == NavigationIntentKind.Zoom), Is.EqualTo(1));
            Assert.That(intents, Has.None.Matches<NavigationIntent>(intent =>
                intent.Kind == NavigationIntentKind.WorldTap || intent.Kind == NavigationIntentKind.Pan));
            Assert.That(interpreter.IsPinchConsumed, Is.False);

            interpreter.BeginPointer(3, Vector2.one, false, true);
            interpreter.EndPointer(3, Vector2.one);
            Assert.That(intents.Last().Kind, Is.EqualTo(NavigationIntentKind.WorldTap));
        }

        [UnityTest]
        public IEnumerator FocusOrientationAndDisableInterruptWorldGestures()
        {
            var gameObject = new GameObject("navigation-input-test");
            var adapter = gameObject.AddComponent<NavigationInputAdapter>();
            yield return null;

            var intents = Capture(adapter.Interpreter);
            adapter.Interpreter.BeginPointer(0, Vector2.zero, false, true);
            gameObject.SendMessage("OnApplicationFocus", false);
            Assert.That(adapter.Interpreter.HasActiveGesture, Is.False);

            adapter.Interpreter.BeginPointer(0, Vector2.zero, false, true);
            var changedOrientation = Screen.orientation == ScreenOrientation.Portrait
                ? ScreenOrientation.LandscapeLeft
                : ScreenOrientation.Portrait;
            adapter.NotifyOrientationChanged(changedOrientation);
            Assert.That(adapter.Interpreter.HasActiveGesture, Is.False);

            adapter.Interpreter.BeginPointer(0, Vector2.zero, false, true);
            adapter.enabled = false;
            Assert.That(adapter.Interpreter.HasActiveGesture, Is.False);
            Assert.That(intents.Count(intent => intent.Kind == NavigationIntentKind.CancelWorldGesture), Is.EqualTo(3));

            Object.Destroy(gameObject);
        }

        [Test]
        public void VisibleZoomEntryPointProducesTheSameSemanticZoomIntent()
        {
            var interpreter = new NavigationGestureInterpreter();
            var intents = Capture(interpreter);

            interpreter.VisibleZoom(new Vector2(100f, 200f), 1);
            interpreter.VisibleZoom(new Vector2(100f, 200f), -1);

            Assert.That(intents.Select(intent => intent.Kind), Is.All.EqualTo(NavigationIntentKind.Zoom));
            Assert.That(intents.Select(intent => intent.ZoomDelta), Is.EqualTo(new[] { 1f, -1f }));
        }

        [TestCase(360f / 640f)]
        [TestCase(640f / 360f)]
        [TestCase(390f / 844f)]
        [TestCase(844f / 390f)]
        [TestCase(768f / 1024f)]
        [TestCase(1024f / 768f)]
        [TestCase(1280f / 720f)]
        [TestCase(1920f / 1080f)]
        public void CameraPanClampsForRepresentativeViewportAspects(float aspect)
        {
            var gameObject = new GameObject("factory-camera-test");
            var camera = gameObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3f;
            gameObject.transform.position = new Vector3(0f, 0f, -10f);
            var controller = gameObject.AddComponent<FactoryCameraController>();
            var bounds = new Rect(-8f, -6f, 10f, 9f);
            var padding = Vector2.one;
            controller.Configure(camera, bounds, 1f, 8f, padding);

            var clamped = controller.ClampPosition(new Vector3(1000f, -1000f, -10f), aspect);
            AssertAxisWithinOrCentered(
                clamped.x,
                bounds.xMin - padding.x + camera.orthographicSize * aspect,
                bounds.xMax + padding.x - camera.orthographicSize * aspect,
                bounds.center.x);
            AssertAxisWithinOrCentered(
                clamped.y,
                bounds.yMin - padding.y + camera.orthographicSize,
                bounds.yMax + padding.y - camera.orthographicSize,
                bounds.center.y);
            Assert.That(clamped.z, Is.EqualTo(-10f));

            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void CameraZoomIsAnchoredAndClampedToReplaceableLimits()
        {
            var gameObject = new GameObject("factory-camera-zoom-test");
            var camera = gameObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            gameObject.transform.position = new Vector3(8f, 8f, -10f);
            var controller = gameObject.AddComponent<FactoryCameraController>();
            controller.Configure(camera, new Rect(0f, 0f, 16f, 16f), 2f, 12f, new Vector2(20f, 20f));
            var anchor = new Vector2(5f, 6f);
            var oldCenter = (Vector2)gameObject.transform.position;
            var oldSize = camera.orthographicSize;

            controller.ZoomAtWorldPoint(1f, anchor);

            var expectedCenter = anchor + (oldCenter - anchor) * (camera.orthographicSize / oldSize);
            Assert.That(gameObject.transform.position.x, Is.EqualTo(expectedCenter.x).Within(0.0001f));
            Assert.That(gameObject.transform.position.y, Is.EqualTo(expectedCenter.y).Within(0.0001f));

            for (var index = 0; index < 100; index++)
            {
                controller.ZoomAtWorldPoint(1f, anchor);
            }

            Assert.That(camera.orthographicSize, Is.EqualTo(2f).Within(0.0001f));
            for (var index = 0; index < 100; index++)
            {
                controller.ZoomAtWorldPoint(-1f, anchor);
            }

            Assert.That(camera.orthographicSize, Is.EqualTo(12f).Within(0.0001f));
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void GridMapperRoundTripsPositiveNegativeCentersAndExactBoundaries()
        {
            var mapper = new GridWorldMapper(2f, new Vector2(10f, -4f));
            var positions = new[]
            {
                new GridPosition(0, 0),
                new GridPosition(4, 7),
                new GridPosition(-1, -1),
                new GridPosition(-8, 3),
            };

            foreach (var position in positions)
            {
                Assert.That(mapper.WorldToGrid(mapper.CellCenter(position)), Is.EqualTo(position));
            }

            Assert.That(mapper.CellOrigin(new GridPosition(0, 0)), Is.EqualTo(new Vector2(10f, -4f)));
            Assert.That(mapper.WorldToGrid(new Vector2(10f, -4f)), Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(mapper.WorldToGrid(new Vector2(9.999f, -4.001f)), Is.EqualTo(new GridPosition(-1, -1)));
            Assert.That(mapper.WorldToGrid(new Vector2(12f, -2f)), Is.EqualTo(new GridPosition(1, 1)));
        }

        private static List<NavigationIntent> Capture(NavigationGestureInterpreter interpreter)
        {
            var intents = new List<NavigationIntent>();
            interpreter.IntentRaised += intents.Add;
            return intents;
        }

        private static void AssertAxisWithinOrCentered(float value, float minimum, float maximum, float center)
        {
            if (minimum <= maximum)
            {
                Assert.That(value, Is.GreaterThanOrEqualTo(minimum - 0.0001f));
                Assert.That(value, Is.LessThanOrEqualTo(maximum + 0.0001f));
            }
            else
            {
                Assert.That(value, Is.EqualTo(center).Within(0.0001f));
            }
        }
    }
}
