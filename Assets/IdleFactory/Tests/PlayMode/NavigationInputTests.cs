using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Domain;
using IdleFactory.Unity.Input;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode
{
    public sealed class NavigationInputTests : InputTestFixture
    {
        private string actionJson;
        private InputActionAsset actionAsset;
        private GameObject adapterObject;
        private NavigationInputAdapter adapter;
        private Mouse mouse;
        private Touchscreen touchscreen;
        private Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();
            var assetPath = Path.Combine(
                Application.dataPath,
                "IdleFactory/Unity/Input/FactoryNavigation.inputactions");
            Assert.That(File.Exists(assetPath), Is.True, "The committed navigation action asset must exist.");
            actionJson = File.ReadAllText(assetPath);
            Assert.That(actionJson, Does.Contain("\"name\": \"FactoryNavigation\""));
            actionAsset = InputActionAsset.FromJson(actionJson);

            mouse = InputSystem.AddDevice<Mouse>();
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            adapterObject = new GameObject("navigation-input-adapter-test");
            adapter = adapterObject.AddComponent<NavigationInputAdapter>();
            ConfigureAdapter();
            AssertBindingsResolved();
        }

        public override void TearDown()
        {
            try
            {
                if (adapterObject != null)
                {
                    Object.DestroyImmediate(adapterObject);
                }

                if (actionAsset != null)
                {
                    Object.DestroyImmediate(actionAsset);
                }
            }
            finally
            {
                base.TearDown();
            }
        }

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
        public void PanOnlyPointerNeverTapsButStillPansBeyondThreshold()
        {
            var interpreter = new NavigationGestureInterpreter();
            var intents = Capture(interpreter);

            interpreter.BeginPointer(-2, new Vector2(10f, 10f), false, false, false);
            interpreter.EndPointer(-2, new Vector2(15f, 10f));
            interpreter.BeginPointer(-2, new Vector2(10f, 10f), false, false, false);
            interpreter.MovePointer(-2, new Vector2(30f, 10f));
            interpreter.EndPointer(-2, new Vector2(30f, 10f));

            Assert.That(intents.Count, Is.EqualTo(1));
            Assert.That(intents[0].Kind, Is.EqualTo(NavigationIntentKind.Pan));
        }

        [Test]
        public void CommittedActionAssetHasRequiredMouseTouchAndCancelBindings()
        {
            Assert.That(actionJson, Does.Contain("\"path\": \"<Mouse>/leftButton\""));
            Assert.That(actionJson, Does.Contain("\"path\": \"<Mouse>/middleButton\""));
            Assert.That(actionJson, Does.Contain("\"path\": \"<Touchscreen>/touch0/press\""));
            Assert.That(actionJson, Does.Contain("\"path\": \"<Touchscreen>/touch1/press\""));
            Assert.That(actionJson, Does.Contain("\"path\": \"<Keyboard>/escape\""));
        }

        [Test]
        public void AdapterMiddleButtonIsPanOnlyForStationaryThresholdAndDrag()
        {
            var intents = Capture(adapter);

            QueueMouse(new Vector2(40f, 50f), middle: true);
            QueueMouse(new Vector2(45f, 50f), middle: true);
            QueueMouse(new Vector2(45f, 50f));
            Assert.That(intents, Is.Empty);

            QueueMouse(new Vector2(40f, 50f), middle: true);
            QueueMouse(new Vector2(70f, 50f), middle: true);
            QueueMouse(new Vector2(70f, 50f));

            Assert.That(intents.Count, Is.EqualTo(1));
            Assert.That(intents[0].Kind, Is.EqualTo(NavigationIntentKind.Pan));
            Assert.That(intents, Has.None.Matches<NavigationIntent>(intent =>
                intent.Kind == NavigationIntentKind.WorldTap));
        }

        [Test]
        public void AdapterSamplesFirstClickAndWheelCoordinatesAtCallbackBoundary()
        {
            var intents = Capture(adapter);
            var hitTestPositions = new List<Vector2>();
            var clickPosition = new Vector2(123f, 77f);
            ConfigureAdapter(position =>
            {
                hitTestPositions.Add(position);
                return position == clickPosition;
            });

            QueueMouse(clickPosition, left: true);
            QueueMouse(clickPosition);

            Assert.That(hitTestPositions, Does.Contain(clickPosition));
            Assert.That(intents, Is.Empty, "A first-event click beginning over UI must remain UI-owned.");

            var wheelPosition = new Vector2(321f, 222f);
            QueueMouse(wheelPosition, scroll: new Vector2(0f, 120f));
            var zoom = intents.Single(intent => intent.Kind == NavigationIntentKind.Zoom);
            Assert.That(zoom.Position, Is.EqualTo(wheelPosition));
        }

        [Test]
        public void AdapterSamplesTouchOwnershipFromSameTouchStateEvent()
        {
            var intents = Capture(adapter);
            var hitTestPositions = new List<Vector2>();
            var touchPosition = new Vector2(210f, 330f);
            ConfigureAdapter(position =>
            {
                hitTestPositions.Add(position);
                return true;
            });

            QueueTouch(11, InputTouchPhase.Began, touchPosition);
            QueueTouch(11, InputTouchPhase.Ended, touchPosition);

            Assert.That(hitTestPositions, Does.Contain(touchPosition));
            Assert.That(intents, Is.Empty);
        }

        [Test]
        public void AdapterConsumesSecondTouchPinchThroughEveryRelease()
        {
            var intents = Capture(adapter);
            ConfigureAdapter(_ => false);

            QueueTouch(21, InputTouchPhase.Began, new Vector2(20f, 20f));
            QueueTouch(22, InputTouchPhase.Began, new Vector2(120f, 20f));
            QueueTouch(22, InputTouchPhase.Moved, new Vector2(150f, 20f), new Vector2(30f, 0f));
            QueueTouch(21, InputTouchPhase.Ended, new Vector2(20f, 20f));
            QueueTouch(22, InputTouchPhase.Ended, new Vector2(150f, 20f));

            Assert.That(intents.Count(intent => intent.Kind == NavigationIntentKind.CancelWorldGesture), Is.EqualTo(1));
            Assert.That(intents.Count(intent => intent.Kind == NavigationIntentKind.Zoom), Is.GreaterThanOrEqualTo(1));
            Assert.That(intents, Has.None.Matches<NavigationIntent>(intent =>
                intent.Kind == NavigationIntentKind.WorldTap || intent.Kind == NavigationIntentKind.Pan));
        }

        [Test]
        public void AdapterCancelAndRepeatedRebindDoNotDuplicateCallbacks()
        {
            var intents = Capture(adapter);
            ConfigureAdapter(_ => false);
            ConfigureAdapter(_ => false);

            QueueMouse(new Vector2(80f, 90f), left: true);
            QueueKeyboard(Key.Escape);
            QueueMouse(new Vector2(80f, 90f));

            Assert.That(intents.Count(intent => intent.Kind == NavigationIntentKind.CancelWorldGesture), Is.EqualTo(1));
            Assert.That(intents, Has.None.Matches<NavigationIntent>(intent =>
                intent.Kind == NavigationIntentKind.WorldTap));

            intents.Clear();
            adapter.enabled = false;
            adapter.enabled = true;
            QueueMouse(new Vector2(100f, 110f), left: true);
            QueueMouse(new Vector2(100f, 110f));
            Assert.That(intents.Count(intent => intent.Kind == NavigationIntentKind.WorldTap), Is.EqualTo(1));
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
            yield return null;

            var intents = Capture(adapter.Interpreter);
            adapter.Interpreter.BeginPointer(0, Vector2.zero, false, true);
            adapterObject.SendMessage("OnApplicationFocus", false);
            Assert.That(adapter.Interpreter.HasActiveGesture, Is.False);

            adapter.Interpreter.BeginPointer(0, Vector2.zero, false, true);
            adapterObject.SendMessage("OnApplicationPause", true);
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
            Assert.That(intents.Count(intent => intent.Kind == NavigationIntentKind.CancelWorldGesture), Is.EqualTo(4));
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
            // T427: the pan limit is a half-cell margin around the factory extent at the integer scale (was a 1 unit padding).
            const int height = 600;
            var width = Mathf.RoundToInt(height * aspect);
            var texture = new RenderTexture(width, height, 16);
            var gameObject = new GameObject("factory-camera-test");
            try
            {
                var camera = gameObject.AddComponent<Camera>();
                camera.targetTexture = texture;
                gameObject.transform.position = new Vector3(0f, 0f, -10f);
                var controller = gameObject.AddComponent<FactoryCameraController>();
                controller.Configure(camera, new ArtScaleSettings(24, 24, 6));
                var columns = SlotGridLayout.ColumnsFor(width, height);
                controller.Fit(new ScaleLayout(width, height, 1f, new Rect(0f, 0f, width, height), columns));
                while (controller.ZoomInState == ZoomButtonState.Enabled)
                {
                    controller.ZoomFromVisibleControl(1);
                }

                controller.PanByWorldDelta(new Vector2(1000f, -1000f));
                var extent = SlotGridLayout.Extent(columns);
                var position = camera.transform.position;
                var tolerance = 1f / (controller.Scale * 24f);
                var halfWidth = camera.orthographicSize * camera.aspect;
                AssertAxisWithinOrCentered(
                    position.x,
                    extent.xMin - SlotGridLayout.HalfCellMargin + halfWidth,
                    extent.xMax + SlotGridLayout.HalfCellMargin - halfWidth,
                    extent.center.x,
                    tolerance);
                AssertAxisWithinOrCentered(
                    position.y,
                    extent.yMin - SlotGridLayout.HalfCellMargin + camera.orthographicSize,
                    extent.yMax + SlotGridLayout.HalfCellMargin - camera.orthographicSize,
                    extent.center.y,
                    tolerance);
                Assert.That(position.z, Is.EqualTo(-10f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ASwipeFarPastAPanLimitLeavesNoHiddenRemainderSoTheDragBackMovesAtOnce()
        {
            // Architect A5: the part of a swipe that a pan limit held back must not be paid off before the camera moves again.
            var texture = new RenderTexture(640, 480, 16);
            var gameObject = new GameObject("factory-camera-remainder-test");
            try
            {
                var camera = gameObject.AddComponent<Camera>();
                camera.targetTexture = texture;
                gameObject.transform.position = new Vector3(0f, 0f, -10f);
                var controller = gameObject.AddComponent<FactoryCameraController>();
                controller.Configure(camera, new ArtScaleSettings(24, 24, 6));
                controller.Fit(new ScaleLayout(640, 480, 1f, new Rect(0f, 0f, 640, 480), 3));
                while (controller.ZoomInState == ZoomButtonState.Enabled)
                {
                    controller.ZoomFromVisibleControl(1);
                }

                controller.PanByWorldDelta(new Vector2(500f, 500f));
                var atLimit = camera.transform.position;
                var pixel = 1f / (controller.Scale * 24f);

                controller.PanByWorldDelta(new Vector2(-pixel, -pixel));

                var moved = camera.transform.position;
                Assert.That(atLimit.x - moved.x, Is.GreaterThan(pixel * 0.5f), "x moved back by a device pixel at once");
                Assert.That(atLimit.y - moved.y, Is.GreaterThan(pixel * 0.5f), "y moved back by a device pixel at once");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void CameraZoomStepsWholeLevelsInstantlyAndStopsAtTheAllowedRange()
        {
            var texture = new RenderTexture(1080, 1920, 16);
            var gameObject = new GameObject("factory-camera-zoom-test");
            try
            {
                var camera = gameObject.AddComponent<Camera>();
                camera.targetTexture = texture;
                gameObject.transform.position = new Vector3(0f, 0f, -10f);
                var controller = gameObject.AddComponent<FactoryCameraController>();
                controller.Configure(camera, new ArtScaleSettings(24, 24, 6));
                controller.Fit(new ScaleLayout(1080, 1920, 2f, new Rect(0f, 0f, 1080, 1920), 3));
                var anchor = new Vector2(300f, 700f);
                var start = controller.Scale;
                Assert.That(controller.Model.LowerBound, Is.EqualTo(2), "1080 px at 2 px/pt: n_min = ceil(24 * 2 / 24)");

                controller.ZoomAtScreenPosition(1f, anchor);
                Assert.That(controller.Scale, Is.EqualTo(start + 1), "one wheel notch is one level, with no animation");
                Assert.That(camera.orthographicSize, Is.EqualTo(1920f / (2f * controller.Scale * 24f)).Within(1e-5f), "rule 1");

                for (var index = 0; index < 100; index++)
                {
                    controller.ZoomAtScreenPosition(1f, anchor);
                }

                Assert.That(controller.Scale, Is.EqualTo(6));
                Assert.That(controller.ZoomInState, Is.EqualTo(ZoomButtonState.DisabledAtLimit));
                for (var index = 0; index < 100; index++)
                {
                    controller.ZoomAtScreenPosition(-1f, anchor);
                }

                Assert.That(controller.Scale, Is.EqualTo(controller.Model.LowerBound));
                Assert.That(controller.ZoomOutState, Is.EqualTo(ZoomButtonState.DisabledAtLimit));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // T454: pinch preview and snap on gesture end (contracts/camera-scale.md rules 4 and 5)
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void APinchRaisesGesturalZoomIncrementsAndOneGestureEndOnTheFirstRelease()
        {
            var interpreter = new NavigationGestureInterpreter();
            var intents = Capture(interpreter);

            interpreter.BeginPointer(0, Vector2.zero, false, true);
            interpreter.BeginPointer(1, new Vector2(100f, 0f), false, true);
            interpreter.MovePointer(1, new Vector2(140f, 0f));
            interpreter.MovePointer(1, new Vector2(170f, 0f));
            interpreter.EndPointer(0, Vector2.zero);
            interpreter.MovePointer(1, new Vector2(400f, 0f));
            interpreter.EndPointer(1, new Vector2(400f, 0f));

            var zooms = intents.Where(intent => intent.Kind == NavigationIntentKind.Zoom).ToList();
            Assert.That(zooms, Has.Count.EqualTo(2));
            Assert.That(zooms, Has.All.Matches<NavigationIntent>(intent => intent.IsGesture), "pinch increments are previews");
            Assert.That(intents.Count(intent => intent.Kind == NavigationIntentKind.ZoomGestureEnded), Is.EqualTo(1));
            Assert.That(intents.Last().Kind, Is.EqualTo(NavigationIntentKind.ZoomGestureEnded), "nothing zooms after the gesture ended");
            Assert.That(interpreter.IsPinchConsumed, Is.False);
        }

        [Test]
        public void WheelAndButtonZoomAreSingleShotAndNeverPreviewsOrGestureEnds()
        {
            var interpreter = new NavigationGestureInterpreter();
            var intents = Capture(interpreter);

            interpreter.WheelZoom(new Vector2(10f, 10f), 120f, false);
            interpreter.VisibleZoom(new Vector2(10f, 10f), -1);

            Assert.That(intents.Select(intent => intent.Kind), Is.All.EqualTo(NavigationIntentKind.Zoom));
            Assert.That(intents, Has.None.Matches<NavigationIntent>(intent => intent.IsGesture));
        }

        [Test]
        public void AThirdWorldTouchCancelsThePinchAndNoGestureEndFollows()
        {
            var interpreter = new NavigationGestureInterpreter();
            var intents = Capture(interpreter);

            interpreter.BeginPointer(0, Vector2.zero, false, true);
            interpreter.BeginPointer(1, new Vector2(100f, 0f), false, true);
            interpreter.MovePointer(1, new Vector2(140f, 0f));
            var cancelsBefore = intents.Count(intent => intent.Kind == NavigationIntentKind.CancelWorldGesture);
            interpreter.BeginPointer(2, new Vector2(50f, 50f), false, true);
            interpreter.MovePointer(1, new Vector2(300f, 0f));
            interpreter.EndPointer(2, new Vector2(50f, 50f));
            interpreter.EndPointer(0, Vector2.zero);
            interpreter.EndPointer(1, new Vector2(300f, 0f));

            Assert.That(intents.Count(intent => intent.Kind == NavigationIntentKind.CancelWorldGesture), Is.EqualTo(cancelsBefore + 1));
            Assert.That(intents.Count(intent => intent.Kind == NavigationIntentKind.Zoom), Is.EqualTo(1), "no zoom after the cancel");
            Assert.That(intents, Has.None.Matches<NavigationIntent>(intent => intent.Kind == NavigationIntentKind.ZoomGestureEnded));
        }

        [Test]
        public void AnInterruptionInsideAPinchRaisesCancelAndNoGestureEnd()
        {
            var interpreter = new NavigationGestureInterpreter();
            var intents = Capture(interpreter);

            interpreter.BeginPointer(0, Vector2.zero, false, true);
            interpreter.BeginPointer(1, new Vector2(100f, 0f), false, true);
            interpreter.MovePointer(1, new Vector2(140f, 0f));
            interpreter.CancelAll();
            interpreter.EndPointer(0, Vector2.zero);
            interpreter.EndPointer(1, new Vector2(140f, 0f));

            Assert.That(intents.Last().Kind, Is.EqualTo(NavigationIntentKind.CancelWorldGesture));
            Assert.That(intents, Has.None.Matches<NavigationIntent>(intent => intent.Kind == NavigationIntentKind.ZoomGestureEnded));
        }

        [Test]
        public void ThePinchAdapterPathRaisesTheGestureEndThroughTheAdapter()
        {
            var intents = Capture(adapter);
            ConfigureAdapter(_ => false);

            QueueTouch(31, InputTouchPhase.Began, new Vector2(20f, 20f));
            QueueTouch(32, InputTouchPhase.Began, new Vector2(120f, 20f));
            QueueTouch(32, InputTouchPhase.Moved, new Vector2(170f, 20f), new Vector2(50f, 0f));
            QueueTouch(31, InputTouchPhase.Ended, new Vector2(20f, 20f));
            QueueTouch(32, InputTouchPhase.Ended, new Vector2(170f, 20f));

            Assert.That(intents.Count(intent => intent.Kind == NavigationIntentKind.ZoomGestureEnded), Is.EqualTo(1));
            Assert.That(intents.Where(intent => intent.Kind == NavigationIntentKind.Zoom), Has.All.Matches<NavigationIntent>(intent => intent.IsGesture));
        }

        private static FactoryCameraController NewPinchController(
            int width, int height, float pixelsPerPoint, ArtScaleSettings settings, out GameObject gameObject, out RenderTexture texture)
        {
            texture = new RenderTexture(width, height, 16);
            gameObject = new GameObject("factory-camera-pinch-test");
            var camera = gameObject.AddComponent<Camera>();
            camera.targetTexture = texture;
            gameObject.transform.position = new Vector3(0f, 0f, -10f);
            var controller = gameObject.AddComponent<FactoryCameraController>();
            controller.Configure(camera, settings);
            controller.Fit(new ScaleLayout(width, height, pixelsPerPoint, new Rect(0f, 0f, width, height), 3));
            return controller;
        }

        private static void ToMiddleLevel(FactoryCameraController controller)
        {
            for (var index = 0; index < 10; index++)
            {
                controller.ZoomFromVisibleControl(-1);
            }

            controller.ZoomFromVisibleControl(1);
        }

        private static void DisposePinch(GameObject gameObject, RenderTexture texture)
        {
            Object.DestroyImmediate(gameObject);
            texture.Release();
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void APinchPreviewIsContinuousHoldsTheAnchorAndSnapsToTheNearestLevelOnGestureEnd()
        {
            var controller = NewPinchController(1080, 1920, 2f, new ArtScaleSettings(24, 24, 6), out var gameObject, out var texture);
            try
            {
                var camera = gameObject.GetComponent<Camera>();
                var anchor = new Vector2(300f, 700f);
                for (var index = 0; index < 10; index++)
                {
                    controller.ZoomFromVisibleControl(-1);
                }

                controller.ZoomFromVisibleControl(1);
                var rest = controller.Scale;
                Assert.That(controller.Model.LevelCount, Is.GreaterThanOrEqualTo(3));
                var worldAtAnchor = controller.Model.ScreenToWorld(anchor, camera.transform.position, rest);

                controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.Zoom, anchor, Vector2.zero, 0.5f, true));
                controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.Zoom, anchor + new Vector2(5f, 5f), Vector2.zero, 0.3f, true));

                Assert.That(controller.PinchPreviewActive, Is.True);
                Assert.That(controller.Scale, Is.EqualTo(rest), "the integer scale at rest is unchanged during the preview");
                Assert.That(controller.DisplayScale, Is.GreaterThan(rest).And.LessThanOrEqualTo(controller.Model.UpperBound));
                Assert.That(camera.orthographicSize, Is.EqualTo(1920f / (2f * controller.DisplayScale * 24f)).Within(1e-5f));
                var held = controller.Model.ScreenToWorld(anchor, camera.transform.position, controller.DisplayScale);
                Assert.That(Vector2.Distance(held, worldAtAnchor), Is.LessThan(1e-3f), "the world point under the gesture anchor stays fixed");

                var preview = controller.DisplayScale;
                var expected = Mathf.Clamp(Mathf.FloorToInt(preview + 0.5f), controller.Model.LowerBound, controller.Model.UpperBound);
                var changes = 0;
                controller.ScaleChanged += () => changes++;
                controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.ZoomGestureEnded, anchor, Vector2.zero, 0f));

                Assert.That(controller.PinchPreviewActive, Is.False);
                Assert.That(controller.Scale, Is.EqualTo(expected), "snaps to the nearest allowed level");
                Assert.That(changes, Is.EqualTo(expected != rest ? 1 : 0));
                Assert.That(camera.orthographicSize, Is.EqualTo(1920f / (2f * expected * 24f)).Within(1e-5f), "rule 1 at rest");
                Assert.That(CameraScaleModel.IsOnPixelGrid(camera.transform.position.x, 1080, expected, 24), Is.True);
                Assert.That(CameraScaleModel.IsOnPixelGrid(camera.transform.position.y, 1920, expected, 24), Is.True);
                Assert.That(controller.DisplayScale, Is.EqualTo((float)expected), "the preview never persists at rest");
            }
            finally
            {
                DisposePinch(gameObject, texture);
            }
        }

        [Test]
        public void APinchPreviewIsClampedToTheAllowedRangeAndSnapsBackInside()
        {
            var controller = NewPinchController(1080, 1920, 2f, new ArtScaleSettings(24, 24, 6), out var gameObject, out var texture);
            try
            {
                var anchor = new Vector2(540f, 960f);
                for (var index = 0; index < 20; index++)
                {
                    controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.Zoom, anchor, Vector2.zero, 1.5f, true));
                }

                Assert.That(controller.DisplayScale, Is.EqualTo((float)controller.Model.UpperBound));
                controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.ZoomGestureEnded, anchor, Vector2.zero, 0f));
                Assert.That(controller.Scale, Is.EqualTo(controller.Model.UpperBound));

                for (var index = 0; index < 20; index++)
                {
                    controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.Zoom, anchor, Vector2.zero, -1.5f, true));
                }

                Assert.That(controller.DisplayScale, Is.EqualTo((float)controller.Model.LowerBound));
                controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.ZoomGestureEnded, anchor, Vector2.zero, 0f));
                Assert.That(controller.Scale, Is.EqualTo(controller.Model.LowerBound));
            }
            finally
            {
                DisposePinch(gameObject, texture);
            }
        }

        [Test]
        public void ASecondTouchOrAnInterruptionCancelsThePreviewAndRestoresTheViewBeforeThePinch()
        {
            var controller = NewPinchController(1080, 1920, 2f, new ArtScaleSettings(24, 24, 6), out var gameObject, out var texture);
            try
            {
                var camera = gameObject.GetComponent<Camera>();
                ToMiddleLevel(controller);
                var scale = controller.Scale;
                var size = camera.orthographicSize;
                var position = camera.transform.position;
                var changes = 0;
                controller.ScaleChanged += () => changes++;

                controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.Zoom, new Vector2(300f, 700f), Vector2.zero, 0.6f, true));
                Assert.That(controller.PinchPreviewActive, Is.True);
                Assert.That(camera.orthographicSize, Is.Not.EqualTo(size).Within(1e-6f));
                controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.CancelWorldGesture, Vector2.zero, Vector2.zero, 0f));

                Assert.That(controller.PinchPreviewActive, Is.False);
                Assert.That(controller.Scale, Is.EqualTo(scale));
                Assert.That(camera.orthographicSize, Is.EqualTo(size).Within(1e-6f));
                Assert.That(camera.transform.position, Is.EqualTo(position));
                Assert.That(changes, Is.Zero, "a cancel is not a scale change");

                controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.ZoomGestureEnded, Vector2.zero, Vector2.zero, 0f));
                Assert.That(controller.Scale, Is.EqualTo(scale), "a gesture end without a preview does nothing");
            }
            finally
            {
                DisposePinch(gameObject, texture);
            }
        }

        [Test]
        public void ALayoutChangeDuringAPinchCancelsItAndKeepsTheScaleThatStillFits()
        {
            var controller = NewPinchController(1080, 1920, 2f, new ArtScaleSettings(24, 24, 6), out var gameObject, out var texture);
            try
            {
                var camera = gameObject.GetComponent<Camera>();
                ToMiddleLevel(controller);
                var scale = controller.Scale;
                controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.Zoom, new Vector2(300f, 700f), Vector2.zero, 0.6f, true));
                Assert.That(controller.PinchPreviewActive, Is.True);

                controller.UpdateLayout(new ScaleLayout(1080, 1920, 2f, new Rect(0f, 100f, 1080, 1700), 3));

                Assert.That(controller.PinchPreviewActive, Is.False);
                Assert.That(controller.Scale, Is.EqualTo(scale), "the scale still fits, so it stays");
                Assert.That(camera.orthographicSize, Is.EqualTo(1920f / (2f * scale * 24f)).Within(1e-5f));
            }
            finally
            {
                DisposePinch(gameObject, texture);
            }
        }

        [Test]
        public void APinchHasNoEffectInASingleLevelRange()
        {
            var controller = NewPinchController(1080, 1920, 2f, new ArtScaleSettings(24, 24, 2), out var gameObject, out var texture);
            try
            {
                var camera = gameObject.GetComponent<Camera>();
                Assert.That(controller.Model.LevelCount, Is.EqualTo(1));
                Assert.That(controller.ZoomInState, Is.EqualTo(ZoomButtonState.Hidden));
                Assert.That(controller.ZoomOutState, Is.EqualTo(ZoomButtonState.Hidden));
                var size = camera.orthographicSize;

                controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.Zoom, new Vector2(300f, 700f), Vector2.zero, 0.8f, true));
                controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.ZoomGestureEnded, Vector2.zero, Vector2.zero, 0f));

                Assert.That(controller.PinchPreviewActive, Is.False);
                Assert.That(camera.orthographicSize, Is.EqualTo(size));
            }
            finally
            {
                DisposePinch(gameObject, texture);
            }
        }

        [Test]
        public void WheelNotchesAndButtonStepsNeverProduceAPreview()
        {
            var controller = NewPinchController(1080, 1920, 2f, new ArtScaleSettings(24, 24, 6), out var gameObject, out var texture);
            try
            {
                controller.HandleNavigationIntent(new NavigationIntent(NavigationIntentKind.Zoom, new Vector2(300f, 700f), Vector2.zero, 1f));
                Assert.That(controller.PinchPreviewActive, Is.False);
                Assert.That(controller.DisplayScale, Is.EqualTo((float)controller.Scale));
                controller.ZoomFromVisibleControl(1);
                Assert.That(controller.PinchPreviewActive, Is.False);
            }
            finally
            {
                DisposePinch(gameObject, texture);
            }
        }

        // T456: taps on the isometric lattice resolve through the footprint-only hit shape; the sorting rows draw the front later.
        private SlotGridView NewLatticeView(int columns)
        {
            var host = new GameObject("lattice-grid");
            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(new PresentationSpriteEntry[0]);
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            var view = host.AddComponent<SlotGridView>();
            view.Configure(catalog);
            view.SetColumns(columns);
            view.Render(Enumerable.Range(0, SlotGridLayout.SlotCount)
                .Select(i => new SlotVisual(i, SlotCondition.Empty, SlotGridModel.SlotEmptyKey, null, null, null, false, false, false, 0, "Slot " + (i + 1)))
                .ToArray());
            return view;
        }

        [Test]
        public void ATapOnEverySlotOfTheLatticeSelectsThatSlotAndNoPointHitsTwoSlots([Values(3, 4)] int columns)
        {
            var view = NewLatticeView(columns);
            try
            {
                for (var slot = 0; slot < SlotGridLayout.SlotCount; slot++)
                {
                    Assert.That(view.TryHitSlot(view.WorldRectOf(slot).center, out var hit), Is.True, "tap on slot " + (slot + 1));
                    Assert.That(hit, Is.EqualTo(slot));
                    // The plate diamond's four apexes (1 art px inside) tap the same slot.
                    var centre = view.WorldRectOf(slot).center;
                    foreach (var apex in new[] { new Vector2(34f, 0f), new Vector2(-34f, 0f), new Vector2(0f, 17f), new Vector2(0f, -17f) })
                    {
                        Assert.That(view.TryHitSlot(centre + apex / 24f, out hit) && hit == slot, Is.True, $"slot {slot + 1} apex {apex}");
                    }
                }

                // A point belongs to at most one slot: the seam between side-by-side plates and between rows resolves to exactly one of them,
                // and the dead zone between the 45 px hit heights (rows are 54 px apart) taps nothing.
                var left = view.WorldRectOf(0).center;
                var right = view.WorldRectOf(1).center;
                var seam = new Vector2((left.x + right.x) * 0.5f, left.y);
                Assert.That(view.TryHitSlot(seam, out var seamHit), Is.True);
                Assert.That(seamHit == 0 || seamHit == 1, Is.True);
                var below = view.WorldRectOf(columns).center;
                var gap = new Vector2(left.x, (left.y + below.y) * 0.5f);
                Assert.That(view.TryHitSlot(gap, out _), Is.False, "the strip between two rows is not a tap target");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void TheLatticeRowsDrawFrontRowsLaterAndEachSlotIsOneSortingGroup([Values(3, 4)] int columns)
        {
            var view = NewLatticeView(columns);
            try
            {
                for (var slot = 0; slot < SlotGridLayout.SlotCount; slot++)
                {
                    var group = view.transform.GetChild(slot).GetComponentInChildren<UnityEngine.Rendering.SortingGroup>();
                    Assert.That(group, Is.Not.Null, "slot " + (slot + 1));
                    Assert.That(group.sortingOrder, Is.EqualTo(slot / columns), "sorting order is the isometric row");
                    if (slot >= columns)
                    {
                        var behind = view.transform.GetChild(slot - columns).GetComponentInChildren<UnityEngine.Rendering.SortingGroup>();
                        Assert.That(group.sortingOrder, Is.GreaterThan(behind.sortingOrder), "the front row draws later");
                        Assert.That(view.FootprintPivotOf(slot).y, Is.LessThan(view.FootprintPivotOf(slot - columns).y), "and sits lower on the screen");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
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

        [Test]
        public void GridMapperRejectsNonFiniteExtremeOriginAndCenterResults()
        {
            var positive = new GridWorldMapper(float.MaxValue, new Vector2(float.MaxValue, 0f));
            var negative = new GridWorldMapper(float.MaxValue, new Vector2(-float.MaxValue, 0f));

            Assert.That(
                () => positive.CellOrigin(new GridPosition(1, 0)),
                Throws.TypeOf<System.OverflowException>());
            Assert.That(
                () => negative.CellOrigin(new GridPosition(-1, 0)),
                Throws.TypeOf<System.OverflowException>());
            Assert.That(
                () => positive.CellCenter(new GridPosition(int.MaxValue, 0)),
                Throws.TypeOf<System.OverflowException>());
        }

        private static List<NavigationIntent> Capture(NavigationGestureInterpreter interpreter)
        {
            var intents = new List<NavigationIntent>();
            interpreter.IntentRaised += intents.Add;
            return intents;
        }

        private static List<NavigationIntent> Capture(NavigationInputAdapter inputAdapter)
        {
            var intents = new List<NavigationIntent>();
            inputAdapter.IntentRaised += intents.Add;
            return intents;
        }

        private void QueueMouse(
            Vector2 position,
            bool left = false,
            bool middle = false,
            Vector2 scroll = default)
        {
            var state = new MouseState
            {
                position = position,
                scroll = scroll,
            };
            state = state.WithButton(MouseButton.Left, left);
            state = state.WithButton(MouseButton.Middle, middle);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
        }

        private void QueueTouch(
            int touchId,
            InputTouchPhase phase,
            Vector2 position,
            Vector2 delta = default)
        {
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = touchId,
                phase = phase,
                position = position,
                delta = delta,
                pressure = 1f,
            });
            InputSystem.Update();
        }

        private void ConfigureAdapter(System.Func<Vector2, bool> hitTest = null)
        {
            adapter.Configure(actionAsset, hitTest);
        }

        private void QueueKeyboard(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
        }

        private void AssertBindingsResolved()
        {
            var map = actionAsset.FindActionMap("Navigation", true);
            Assert.That(map.enabled, Is.True);
            Assert.That(map.FindAction("PrimaryPress", true).controls, Does.Contain(mouse.leftButton));
            Assert.That(map.FindAction("PanPress", true).controls, Does.Contain(mouse.middleButton));
            Assert.That(map.FindAction("Touch0Press", true).controls, Does.Contain(touchscreen.touches[0].press));
            Assert.That(map.FindAction("Touch1Press", true).controls, Does.Contain(touchscreen.touches[1].press));
            Assert.That(map.FindAction("Cancel", true).controls, Does.Contain(keyboard.escapeKey));
        }

        private static void AssertAxisWithinOrCentered(float value, float minimum, float maximum, float center, float tolerance)
        {
            if (minimum <= maximum)
            {
                Assert.That(value, Is.GreaterThanOrEqualTo(minimum - tolerance));
                Assert.That(value, Is.LessThanOrEqualTo(maximum + tolerance));
            }
            else
            {
                Assert.That(value, Is.EqualTo(center).Within(tolerance));
            }
        }
    }
}
