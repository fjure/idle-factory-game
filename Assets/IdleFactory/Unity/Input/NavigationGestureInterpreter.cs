using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleFactory.Unity.Input
{
    public enum NavigationIntentKind
    {
        WorldTap,
        Pan,
        Zoom,
        CancelWorldGesture,
    }

    public readonly struct NavigationIntent
    {
        public NavigationIntent(NavigationIntentKind kind, Vector2 position, Vector2 delta, float zoomDelta)
        {
            Kind = kind;
            Position = position;
            Delta = delta;
            ZoomDelta = zoomDelta;
        }

        public NavigationIntentKind Kind { get; }
        public Vector2 Position { get; }
        public Vector2 Delta { get; }
        public float ZoomDelta { get; }
    }

    /// <summary>
    /// Converts pointer lifetimes into navigation-only intents. UI ownership is fixed at pointer-down,
    /// and every pointer participating in a pinch stays consumed until the final participant releases.
    /// </summary>
    public sealed class NavigationGestureInterpreter
    {
        public const float DefaultDragThresholdPixels = 12f;
        public const float DefaultPinchPixelsPerZoomUnit = 100f;
        public const float DefaultWheelUnitsPerZoomUnit = 120f;

        private readonly Dictionary<int, PointerState> pointers = new Dictionary<int, PointerState>();
        private readonly HashSet<int> pinchParticipants = new HashSet<int>();
        private readonly float dragThresholdSquared;
        private readonly float pinchPixelsPerZoomUnit;
        private bool pinchConsumed;
        private float previousPinchDistance;

        public NavigationGestureInterpreter(
            float dragThresholdPixels = DefaultDragThresholdPixels,
            float pinchPixelsPerZoomUnit = DefaultPinchPixelsPerZoomUnit)
        {
            if (!IsFinitePositive(dragThresholdPixels))
            {
                throw new ArgumentOutOfRangeException(nameof(dragThresholdPixels));
            }

            if (!IsFinitePositive(pinchPixelsPerZoomUnit))
            {
                throw new ArgumentOutOfRangeException(nameof(pinchPixelsPerZoomUnit));
            }

            dragThresholdSquared = dragThresholdPixels * dragThresholdPixels;
            this.pinchPixelsPerZoomUnit = pinchPixelsPerZoomUnit;
        }

        public event Action<NavigationIntent> IntentRaised;

        public bool HasActiveGesture => pointers.Count > 0;
        public bool IsPinchConsumed => pinchConsumed;

        public void BeginPointer(
            int pointerId,
            Vector2 position,
            bool beganOverUi,
            bool isTouch,
            bool canTap = true)
        {
            if (pointers.ContainsKey(pointerId))
            {
                CancelAll();
            }

            var state = new PointerState(position, beganOverUi, isTouch, canTap);
            pointers.Add(pointerId, state);

            if (pinchConsumed)
            {
                if (isTouch && !beganOverUi)
                {
                    pinchParticipants.Add(pointerId);
                    state.IsConsumed = true;
                }

                return;
            }

            if (!isTouch || beganOverUi)
            {
                return;
            }

            var otherTouchId = FindOtherWorldTouch(pointerId);
            if (!otherTouchId.HasValue)
            {
                return;
            }

            pinchConsumed = true;
            pinchParticipants.Add(otherTouchId.Value);
            pinchParticipants.Add(pointerId);
            pointers[otherTouchId.Value].IsConsumed = true;
            state.IsConsumed = true;
            previousPinchDistance = Vector2.Distance(pointers[otherTouchId.Value].Position, position);
            Raise(NavigationIntentKind.CancelWorldGesture, position, Vector2.zero, 0f);
        }

        public void MovePointer(int pointerId, Vector2 position)
        {
            if (!pointers.TryGetValue(pointerId, out var state))
            {
                return;
            }

            var previousPosition = state.Position;
            state.Position = position;

            if (state.BeganOverUi)
            {
                return;
            }

            if (state.IsConsumed || pinchConsumed)
            {
                EmitPinchIfPossible();
                return;
            }

            if (!state.IsPanning)
            {
                if ((position - state.StartPosition).sqrMagnitude <= dragThresholdSquared)
                {
                    return;
                }

                state.IsPanning = true;
                Raise(NavigationIntentKind.Pan, position, position - state.StartPosition, 0f);
                return;
            }

            Raise(NavigationIntentKind.Pan, position, position - previousPosition, 0f);
        }

        public void EndPointer(int pointerId, Vector2 position)
        {
            if (!pointers.TryGetValue(pointerId, out var state))
            {
                return;
            }

            pointers.Remove(pointerId);
            if (pinchParticipants.Remove(pointerId))
            {
                if (pinchParticipants.Count == 0)
                {
                    pinchConsumed = false;
                    previousPinchDistance = 0f;
                }

                return;
            }

            if (state.CanTap && !state.BeganOverUi && !state.IsConsumed && !state.IsPanning && !pinchConsumed)
            {
                Raise(NavigationIntentKind.WorldTap, position, Vector2.zero, 0f);
            }
        }

        public void WheelZoom(Vector2 position, float wheelDelta, bool overUi)
        {
            if (overUi || !IsFinite(wheelDelta) || Mathf.Approximately(wheelDelta, 0f))
            {
                return;
            }

            Raise(NavigationIntentKind.Zoom, position, Vector2.zero, wheelDelta / DefaultWheelUnitsPerZoomUnit);
        }

        public void VisibleZoom(Vector2 position, int direction)
        {
            if (direction == 0)
            {
                return;
            }

            Raise(NavigationIntentKind.Zoom, position, Vector2.zero, Math.Sign(direction));
        }

        public void CancelAll()
        {
            var hadWorldGesture = false;
            foreach (var pointer in pointers.Values)
            {
                hadWorldGesture |= !pointer.BeganOverUi;
            }

            pointers.Clear();
            pinchParticipants.Clear();
            pinchConsumed = false;
            previousPinchDistance = 0f;

            if (hadWorldGesture)
            {
                Raise(NavigationIntentKind.CancelWorldGesture, Vector2.zero, Vector2.zero, 0f);
            }
        }

        private int? FindOtherWorldTouch(int pointerId)
        {
            foreach (var pair in pointers)
            {
                if (pair.Key != pointerId && pair.Value.IsTouch && !pair.Value.BeganOverUi)
                {
                    return pair.Key;
                }
            }

            return null;
        }

        private void EmitPinchIfPossible()
        {
            PointerState first = null;
            PointerState second = null;
            foreach (var participant in pinchParticipants)
            {
                if (!pointers.TryGetValue(participant, out var state))
                {
                    continue;
                }

                if (first == null)
                {
                    first = state;
                }
                else
                {
                    second = state;
                    break;
                }
            }

            if (first == null || second == null)
            {
                return;
            }

            var distance = Vector2.Distance(first.Position, second.Position);
            var distanceDelta = distance - previousPinchDistance;
            previousPinchDistance = distance;
            if (Mathf.Approximately(distanceDelta, 0f))
            {
                return;
            }

            Raise(
                NavigationIntentKind.Zoom,
                (first.Position + second.Position) * 0.5f,
                Vector2.zero,
                distanceDelta / pinchPixelsPerZoomUnit);
        }

        private void Raise(NavigationIntentKind kind, Vector2 position, Vector2 delta, float zoomDelta)
        {
            IntentRaised?.Invoke(new NavigationIntent(kind, position, delta, zoomDelta));
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private sealed class PointerState
        {
            public PointerState(Vector2 position, bool beganOverUi, bool isTouch, bool canTap)
            {
                StartPosition = position;
                Position = position;
                BeganOverUi = beganOverUi;
                IsTouch = isTouch;
                CanTap = canTap;
            }

            public Vector2 StartPosition { get; }
            public Vector2 Position { get; set; }
            public bool BeganOverUi { get; }
            public bool IsTouch { get; }
            public bool CanTap { get; }
            public bool IsPanning { get; set; }
            public bool IsConsumed { get; set; }
        }
    }
}
