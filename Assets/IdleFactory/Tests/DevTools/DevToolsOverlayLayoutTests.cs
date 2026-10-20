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

        private void Press(Key key) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));

        private void Release(Key key) => InputSystem.QueueStateEvent(keyboard, new KeyboardState());
    }
}
