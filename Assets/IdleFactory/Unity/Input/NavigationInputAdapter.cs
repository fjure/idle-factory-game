using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IdleFactory.Unity.Input
{
    /// <summary>
    /// Input System boundary for mouse and the first two touch contacts. It emits navigation intents only.
    /// The UI layer supplies a hit test so pointer ownership is decided once, when each pointer begins.
    /// </summary>
    public sealed class NavigationInputAdapter : MonoBehaviour
    {
        private const int MousePointerId = -1;
        private const int PrimaryTouchPointerId = 0;
        private const int SecondaryTouchPointerId = 1;

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private float dragThresholdPixels = NavigationGestureInterpreter.DefaultDragThresholdPixels;
        [SerializeField] private float pinchPixelsPerZoomUnit = NavigationGestureInterpreter.DefaultPinchPixelsPerZoomUnit;

        private InputActionMap navigationMap;
        private InputAction pointerPosition;
        private InputAction primaryPress;
        private InputAction panPress;
        private InputAction scrollZoom;
        private InputAction touchZeroPosition;
        private InputAction touchZeroPress;
        private InputAction touchOnePosition;
        private InputAction touchOnePress;
        private InputAction cancel;
        private Vector2 mousePosition;
        private Vector2 touchZeroCurrentPosition;
        private Vector2 touchOneCurrentPosition;
        private ScreenOrientation observedOrientation;

        public NavigationGestureInterpreter Interpreter { get; private set; }
        public Func<Vector2, bool> UiHitTest { get; set; }

        public event Action<NavigationIntent> IntentRaised;

        public void Configure(InputActionAsset actions, Func<Vector2, bool> uiHitTest = null)
        {
            if (isActiveAndEnabled)
            {
                UnbindActions();
            }

            inputActions = actions;
            UiHitTest = uiHitTest;

            if (isActiveAndEnabled)
            {
                BindActions();
            }
        }

        public void ZoomFromVisibleControl(int direction)
        {
            EnsureInterpreter();
            Interpreter.VisibleZoom(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), direction);
        }

        public void NotifyOrientationChanged(ScreenOrientation orientation)
        {
            if (orientation == observedOrientation)
            {
                return;
            }

            observedOrientation = orientation;
            Interpreter.CancelAll();
        }

        private void OnEnable()
        {
            observedOrientation = Screen.orientation;
            EnsureInterpreter();
            BindActions();
        }

        private void Update()
        {
            NotifyOrientationChanged(Screen.orientation);
        }

        private void OnDisable()
        {
            if (Interpreter != null)
            {
                Interpreter.CancelAll();
            }

            UnbindActions();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && Interpreter != null)
            {
                Interpreter.CancelAll();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && Interpreter != null)
            {
                Interpreter.CancelAll();
            }
        }

        private void EnsureInterpreter()
        {
            if (Interpreter != null)
            {
                return;
            }

            Interpreter = new NavigationGestureInterpreter(dragThresholdPixels, pinchPixelsPerZoomUnit);
            Interpreter.IntentRaised += intent => IntentRaised?.Invoke(intent);
        }

        private void BindActions()
        {
            if (inputActions == null)
            {
                return;
            }

            navigationMap = inputActions.FindActionMap("Navigation", true);
            pointerPosition = navigationMap.FindAction("PointerPosition", true);
            primaryPress = navigationMap.FindAction("PrimaryPress", true);
            panPress = navigationMap.FindAction("PanPress", true);
            scrollZoom = navigationMap.FindAction("ScrollZoom", true);
            touchZeroPosition = navigationMap.FindAction("Touch0Position", true);
            touchZeroPress = navigationMap.FindAction("Touch0Press", true);
            touchOnePosition = navigationMap.FindAction("Touch1Position", true);
            touchOnePress = navigationMap.FindAction("Touch1Press", true);
            cancel = navigationMap.FindAction("Cancel", true);

            pointerPosition.performed += OnPointerPosition;
            primaryPress.performed += OnPrimaryPress;
            primaryPress.canceled += OnPrimaryPress;
            panPress.performed += OnPrimaryPress;
            panPress.canceled += OnPrimaryPress;
            scrollZoom.performed += OnScrollZoom;
            touchZeroPosition.performed += OnTouchZeroPosition;
            touchZeroPress.performed += OnTouchZeroPress;
            touchZeroPress.canceled += OnTouchZeroPress;
            touchOnePosition.performed += OnTouchOnePosition;
            touchOnePress.performed += OnTouchOnePress;
            touchOnePress.canceled += OnTouchOnePress;
            cancel.performed += OnCancel;
            navigationMap.Enable();
        }

        private void UnbindActions()
        {
            if (navigationMap == null)
            {
                return;
            }

            navigationMap.Disable();
            pointerPosition.performed -= OnPointerPosition;
            primaryPress.performed -= OnPrimaryPress;
            primaryPress.canceled -= OnPrimaryPress;
            panPress.performed -= OnPrimaryPress;
            panPress.canceled -= OnPrimaryPress;
            scrollZoom.performed -= OnScrollZoom;
            touchZeroPosition.performed -= OnTouchZeroPosition;
            touchZeroPress.performed -= OnTouchZeroPress;
            touchZeroPress.canceled -= OnTouchZeroPress;
            touchOnePosition.performed -= OnTouchOnePosition;
            touchOnePress.performed -= OnTouchOnePress;
            touchOnePress.canceled -= OnTouchOnePress;
            cancel.performed -= OnCancel;
            navigationMap = null;
        }

        private void OnPointerPosition(InputAction.CallbackContext context)
        {
            mousePosition = context.ReadValue<Vector2>();
            if (IsMousePressed())
            {
                Interpreter.MovePointer(MousePointerId, mousePosition);
            }
        }

        private void OnPrimaryPress(InputAction.CallbackContext context)
        {
            if (context.ReadValueAsButton())
            {
                if (!Interpreter.HasActiveGesture)
                {
                    Interpreter.BeginPointer(MousePointerId, mousePosition, IsOverUi(mousePosition), false);
                }
            }
            else if (!IsMousePressed())
            {
                Interpreter.EndPointer(MousePointerId, mousePosition);
            }
        }

        private void OnScrollZoom(InputAction.CallbackContext context)
        {
            Interpreter.WheelZoom(mousePosition, context.ReadValue<Vector2>().y, IsOverUi(mousePosition));
        }

        private void OnTouchZeroPosition(InputAction.CallbackContext context)
        {
            touchZeroCurrentPosition = context.ReadValue<Vector2>();
            if (touchZeroPress != null && touchZeroPress.IsPressed())
            {
                Interpreter.MovePointer(PrimaryTouchPointerId, touchZeroCurrentPosition);
            }
        }

        private void OnTouchZeroPress(InputAction.CallbackContext context)
        {
            HandleTouchPress(context, PrimaryTouchPointerId, touchZeroCurrentPosition);
        }

        private void OnTouchOnePosition(InputAction.CallbackContext context)
        {
            touchOneCurrentPosition = context.ReadValue<Vector2>();
            if (touchOnePress != null && touchOnePress.IsPressed())
            {
                Interpreter.MovePointer(SecondaryTouchPointerId, touchOneCurrentPosition);
            }
        }

        private void OnTouchOnePress(InputAction.CallbackContext context)
        {
            HandleTouchPress(context, SecondaryTouchPointerId, touchOneCurrentPosition);
        }

        private void HandleTouchPress(InputAction.CallbackContext context, int pointerId, Vector2 position)
        {
            if (context.ReadValueAsButton())
            {
                Interpreter.BeginPointer(pointerId, position, IsOverUi(position), true);
            }
            else
            {
                Interpreter.EndPointer(pointerId, position);
            }
        }

        private void OnCancel(InputAction.CallbackContext context)
        {
            Interpreter.CancelAll();
        }

        private bool IsOverUi(Vector2 screenPosition)
        {
            return UiHitTest != null && UiHitTest(screenPosition);
        }

        private bool IsMousePressed()
        {
            return (primaryPress != null && primaryPress.IsPressed()) ||
                   (panPress != null && panPress.IsPressed());
        }
    }
}
