#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T367 (Q4, QA-12): one touch and one pointer case per surface. A case runs the same scenario under a touch device profile (a tap pins the exact value, no
    /// hover) and under a pointer device profile (hover shows the exact value, no pin), sending the click with the matching pointer type.
    /// </summary>
    internal static class InputModeSupport
    {
        public const float MinimumTarget = 43.5f;

        public static ShellDeviceProfile ProfileFor(ShellScreen screen, bool touch) =>
            touch ? ShellDeviceProfile.Touch(screen.Width, screen.Height, 400f) : ShellDeviceProfile.Pointer(screen.Width, screen.Height, 96f);

        public static string PointerOf(bool touch) => touch ? "touch" : "mouse";

        /// <summary>A tap (touch) or a click (mouse) on the control: a press and a release at its center, which the button's own manipulator turns into its click.</summary>
        public static IEnumerator Tap(VisualElement control, bool touch)
        {
            // The release only clicks when the pointer is over the control, so a control below the fold is scrolled into view first.
            IdleFactory.Unity.UI.RunShellDocument.ScrollIntoView(control);
            yield return ShellRig.Settle();
            var center = control.worldBound.center;
            Pointer<PointerMoveEvent>(control, EventType.MouseMove, center, touch);
            Pointer<PointerDownEvent>(control, EventType.MouseDown, center, touch);
            Pointer<PointerUpEvent>(control, EventType.MouseUp, center, touch);
        }

        private static void Pointer<T>(VisualElement target, EventType type, Vector2 position, bool touch) where T : PointerEventBase<T>, new()
        {
            var systemEvent = new Event { type = type, mousePosition = position, button = 0 };
            var evt = PointerEventBase<T>.GetPooled(systemEvent);
            if (touch)
            {
                // Touch pointers have ids of 1 and up; the mouse is 0.
                var property = typeof(PointerEventBase<T>).GetProperty("pointerId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                property?.GetSetMethod(true)?.Invoke(evt, new object[] { 1 });
            }

            MarketPanelTestSupport.Send(target, evt, PointerOf(touch));
        }

        /// <summary>The mouse moving onto the control.</summary>
        public static void Hover(VisualElement control) => MarketPanelTestSupport.Send(control, PointerEnterEvent.GetPooled(), "mouse");

        public static void AssertTargets(IEnumerable<VisualElement> controls, string where)
        {
            foreach (var control in controls)
            {
                Assert.That(control.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget), where + ": " + control.name + " is at least 44 pt tall");
                Assert.That(control.worldBound.width, Is.GreaterThanOrEqualTo(MinimumTarget), where + ": " + control.name + " is at least 44 pt wide");
            }
        }
    }
}
#endif
