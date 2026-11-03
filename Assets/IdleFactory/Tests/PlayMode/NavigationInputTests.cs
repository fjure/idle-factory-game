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
