using System.Collections;
using IdleFactory.DevTools;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.DevTools
{
    /// <summary>
    /// T366 (batch-B Play-Tester F-B2): the overlay buttons passed the press to the slot beneath. The world input path asks
    /// <see cref="RuntimePanelHitTest"/> whether a press is over UI; the shell's own panel was the only one it knew, so the separate overlay
    /// panel was invisible to it. These tests run the overlay on a screen panel and assert that a screen point on a button, on the row and on the
    /// shown marker counts as UI, that a point outside the overlay does not (the world outside stays usable), that a press on a button does not
    /// bubble above the overlay, and that the placement holds still under the pointer. Define-gated like the rest of this assembly.
    /// </summary>
    public sealed class DevToolsOverlayPointerTests
    {
        private GameObject host;
        private DevToolsOverlay overlay;
        private DevTimeScale timeScale;

        [SetUp]
        public void SetUp()
        {
            timeScale = new DevTimeScale();
            host = new GameObject("DevTools Overlay Pointer Test");
            overlay = DevToolsOverlay.Create(timeScale, host);
            overlay.AutoPlacement = false;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        private static IEnumerator Settle()
        {
            yield return null;
            yield return null;
            yield return null;
        }

        // The screen point (pixels, origin bottom left) of the centre of an element of the overlay panel.
        private Vector2 ScreenCentre(VisualElement element)
        {
            var scale = overlay.Root.panel.scaledPixelsPerPoint;
            var centre = element.worldBound.center;
            return new Vector2(centre.x * scale, Screen.height - centre.y * scale);
        }

        [UnityTest]
        public IEnumerator APointOnAButtonOrTheRowCountsAsUiAndTheWorldOutsideDoesNot()
        {
            yield return Settle();
            for (var i = 0; i <= DevTimeScale.MaxIndex; i++)
            {
                Assert.That(RuntimePanelHitTest.IsOverUi(ScreenCentre(overlay.ButtonAt(i)), null), Is.True, "button " + i);
            }

            Assert.That(RuntimePanelHitTest.IsOverUi(new Vector2(Screen.width - 4f, 4f), null), Is.False, "the bottom right corner is the world");
            Assert.That(RuntimePanelHitTest.IsOverUi(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), null), Is.False, "the centre is the world");
        }

        [UnityTest]
        public IEnumerator TheShownMarkerCountsAsUiAndAHiddenOneIsNotPicked()
        {
            yield return Settle();
            timeScale.Select(1);
            yield return Settle();
            Assert.That(overlay.Marker.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(RuntimePanelHitTest.IsOverUi(ScreenCentre(overlay.Marker), null), Is.True, "the marker takes the press");

            var panelPoint = overlay.Marker.worldBound.center;
            timeScale.Select(0);
            yield return Settle();
            Assert.That(overlay.Root.panel.Pick(panelPoint), Is.Not.SameAs(overlay.Marker), "no marker at x1, so it consumes nothing");
        }

        [UnityTest]
        public IEnumerator APressOnAButtonDoesNotBubbleAboveTheOverlayRow()
        {
            yield return Settle();
            var presses = 0;
            overlay.Root.RegisterCallback<PointerDownEvent>(evt => presses++);
            var button = overlay.ButtonAt(3);
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = button.worldBound.center }))
            {
                down.target = button;
                button.SendEvent(down);
            }

            Assert.That(presses, Is.EqualTo(0), "the row consumes the press; nothing above the overlay sees it");
        }

        [UnityTest]
        public IEnumerator ThePlacementHoldsStillWhileThePointerIsOnTheOverlay()
        {
            yield return Settle();
            var safe = new Rect(0f, 0f, Screen.width, Screen.height);
            overlay.RefreshPlacement(safe, Screen.width, Screen.height);
            var before = overlay.Root.style.top.value.value;

            using (var enter = PointerEnterEvent.GetPooled(new Event { type = EventType.MouseEnterWindow }))
            {
                enter.target = overlay.Row;
                overlay.Row.SendEvent(enter);
            }

            overlay.RefreshPlacement(new Rect(0f, 0f, Screen.width, Screen.height - 120f), Screen.width, Screen.height);
            Assert.That(overlay.Root.style.top.value.value, Is.EqualTo(before), "no jump under the pointer");

            using (var leave = PointerLeaveEvent.GetPooled(new Event { type = EventType.MouseLeaveWindow }))
            {
                leave.target = overlay.Row;
                overlay.Row.SendEvent(leave);
            }

            overlay.RefreshPlacement(new Rect(0f, 0f, Screen.width, Screen.height - 120f), Screen.width, Screen.height);
            Assert.That(overlay.Root.style.top.value.value, Is.GreaterThan(before), "after the pointer leaves, the placement follows");
        }
    }
}
