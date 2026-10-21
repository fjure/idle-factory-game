using System.Collections;
using IdleFactory.DevTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.DevTools
{
    /// <summary>
    /// T361 (batch-A Play-Tester finding F7, FR-026): the overlay renders. The Windows development player logged "No Theme Style Sheet
    /// set to PanelSettings DevToolsPanelSettings" and showed neither the buttons nor the marker. These tests run the overlay on a
    /// screen panel and assert a theme style sheet, a non-zero layout of the row and every button, a visible marker with a non-zero
    /// layout and its accessible name at an accelerated factor, and the global 1 to 5 keys. Define-gated like the rest of this assembly.
    /// </summary>
    public sealed class DevToolsOverlayLayoutTests : InputTestFixture
    {
        // Yoga snaps layout to whole device pixels, so a 44 point target measures up to a pixel short at a panel scale below 1.
        private const float PixelSnap = 1f;

        private GameObject host;
        private DevToolsOverlay overlay;
        private DevTimeScale timeScale;
        private Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            timeScale = new DevTimeScale();
            host = new GameObject("DevTools Overlay Layout Test");
            overlay = DevToolsOverlay.Create(timeScale, host);
        }

        public override void TearDown()
        {
            try
            {
                Object.DestroyImmediate(host);
            }
            finally
            {
                base.TearDown();
            }
        }

        private static IEnumerator Settle()
        {
            yield return null;
            yield return null;
            yield return null;
        }

        [Test]
        public void ThePanelSettingsCarryAThemeStyleSheet()
        {
            Assert.That(overlay.Settings, Is.Not.Null);
            Assert.That(overlay.Settings.themeStyleSheet, Is.Not.Null, "a themeless panel does not render (F7)");
        }

        [UnityTest]
        public IEnumerator TheRowAndEveryButtonHaveANonZeroLayout()
        {
            yield return Settle();
            Assert.That(overlay.Root.panel, Is.Not.Null);
            Assert.That(overlay.Root.layout.width, Is.GreaterThan(0f));
            for (var i = 0; i <= DevTimeScale.MaxIndex; i++)
            {
                var bounds = overlay.ButtonAt(i).worldBound;
                Assert.That(bounds.width, Is.GreaterThanOrEqualTo(44f - PixelSnap), "button " + i);
                Assert.That(bounds.height, Is.GreaterThanOrEqualTo(44f - PixelSnap), "button " + i);
            }

            var panelBounds = overlay.Root.panel.visualTree.worldBound;
            Assert.That(overlay.ButtonAt(0).worldBound.yMax, Is.LessThanOrEqualTo(panelBounds.yMax + 0.5f), "the row sits inside the panel");
        }

        [UnityTest]
        public IEnumerator TheMarkerIsVisibleWithALayoutAndTheFactorInItsAccessibleNameWhenAccelerated()
        {
            yield return Settle();
            Assert.That(overlay.Marker.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "no marker at x1");

            timeScale.Select(2);
            yield return Settle();

            Assert.That(overlay.Marker.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(overlay.Marker.worldBound.width, Is.GreaterThan(0f));
            Assert.That(overlay.Marker.worldBound.height, Is.GreaterThan(0f));
            Assert.That(overlay.Marker.text, Is.EqualTo("ACCELERATED ×60"));
            Assert.That(overlay.MarkerAccessibleName, Does.Contain("60"));
        }

        [UnityTest]
        public IEnumerator DigitKeysOneToFiveSelectAFactorWithoutAnyFocus()
        {
            yield return Settle();
            Assert.That(overlay.Root.panel.focusController.focusedElement, Is.Null);

            Press(Key.Digit4);
            yield return null;
            Assert.That(timeScale.SelectedIndex, Is.EqualTo(3));
            Release(Key.Digit4);
            yield return null;

            Press(Key.Numpad5);
            yield return null;
            Assert.That(timeScale.SelectedIndex, Is.EqualTo(4));
            Release(Key.Numpad5);
            yield return null;

            Press(Key.Digit1);
            yield return null;
            Assert.That(timeScale.SelectedIndex, Is.EqualTo(0));
        }

        private static void SendKey(VisualElement target, KeyCode key)
        {
            using (var evt = KeyDownEvent.GetPooled('\0', key, EventModifiers.None))
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        private VisualElement Focused() => overlay.Root.panel.focusController.focusedElement as VisualElement;

        /// <summary>T340 (keyboard polish): Left and Right move focus along the five buttons, Home and End jump to the ends, and nothing wraps.</summary>
        [UnityTest]
        public IEnumerator ArrowKeysHomeAndEndMoveFocusAlongTheFiveButtonsWithoutWrapping()
        {
            yield return Settle();
            overlay.ButtonAt(1).Focus();
            yield return null;
            Assert.That(Focused(), Is.SameAs(overlay.ButtonAt(1)));

            SendKey(overlay.ButtonAt(1), KeyCode.RightArrow);
            Assert.That(Focused(), Is.SameAs(overlay.ButtonAt(2)));
            SendKey(overlay.ButtonAt(2), KeyCode.LeftArrow);
            SendKey(overlay.ButtonAt(1), KeyCode.LeftArrow);
            Assert.That(Focused(), Is.SameAs(overlay.ButtonAt(0)));
            SendKey(overlay.ButtonAt(0), KeyCode.LeftArrow);
            Assert.That(Focused(), Is.SameAs(overlay.ButtonAt(0)), "no wrap at the start");

            SendKey(overlay.ButtonAt(0), KeyCode.End);
            Assert.That(Focused(), Is.SameAs(overlay.ButtonAt(DevTimeScale.MaxIndex)));
            SendKey(overlay.ButtonAt(DevTimeScale.MaxIndex), KeyCode.RightArrow);
            Assert.That(Focused(), Is.SameAs(overlay.ButtonAt(DevTimeScale.MaxIndex)), "no wrap at the end");
            SendKey(overlay.ButtonAt(DevTimeScale.MaxIndex), KeyCode.Home);
            Assert.That(Focused(), Is.SameAs(overlay.ButtonAt(0)));
            Assert.That(timeScale.SelectedIndex, Is.EqualTo(0), "moving focus selects nothing");
        }

        [UnityTest]
        public IEnumerator TheButtonsAreInTheNaturalTabOrderAndTheSelectedFactorIsMarkedWithMoreThanColour()
        {
            yield return Settle();
            for (var i = 0; i <= DevTimeScale.MaxIndex; i++)
            {
                Assert.That(overlay.ButtonAt(i).focusable, Is.True);
                Assert.That(overlay.ButtonAt(i).tabIndex, Is.Zero, "a positive tab index would jump the queue ahead of the shell");
            }

            timeScale.Select(2);
            yield return Settle();

            Assert.That(overlay.ButtonAt(2).style.unityFontStyleAndWeight.value, Is.EqualTo(FontStyle.Bold));
            Assert.That(overlay.ButtonAt(2).style.borderTopWidth.value, Is.EqualTo(2f));
            Assert.That(overlay.ButtonAt(0).style.unityFontStyleAndWeight.value, Is.EqualTo(FontStyle.Normal));
            Assert.That(overlay.ButtonAt(0).style.borderTopWidth.value, Is.EqualTo(0f));
        }

        /// <summary>T340: without a shell the overlay sits at the top left, one HUD height below the top inset and inside the side insets.</summary>
        [UnityTest]
        public IEnumerator WithoutAShellTheOverlaySitsBelowTheTopInsetAndInsideTheSideInsets()
        {
            yield return Settle();
            overlay.AutoPlacement = false;
            var pixelsPerPoint = overlay.Root.panel.scaledPixelsPerPoint;
            // 640 x 360 with a 44 px cutout on both sides, a 24 px top inset and a 16 px bottom inset (Screen.safeArea has a bottom-left origin).
            overlay.RefreshPlacement(new Rect(44f, 16f, 552f, 320f), 640f, 360f);

            Assert.That(overlay.Root.style.left.value.value, Is.EqualTo(44f / pixelsPerPoint + DevToolsOverlay.EdgeMargin).Within(0.01f));
            Assert.That(overlay.Root.style.right.value.value, Is.EqualTo(44f / pixelsPerPoint + DevToolsOverlay.EdgeMargin).Within(0.01f));
            Assert.That(overlay.Root.style.top.value.value,
                Is.EqualTo(24f / pixelsPerPoint + DevToolsOverlay.FallbackHudHeight + DevToolsOverlay.EdgeMargin).Within(0.01f));
        }

        private void Press(Key key) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));

        private void Release(Key key) => InputSystem.QueueStateEvent(keyboard, new KeyboardState());
    }
}
