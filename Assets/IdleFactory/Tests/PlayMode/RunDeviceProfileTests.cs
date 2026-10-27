using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T191 (F-10, FR-032, SC-007): the device profile of the run shell. Primary input (touch or pointer), known and unknown
    /// DPI, the 7 mm guarantee for a 44 pt control on a touch device with a known DPI (1080x2400 at 420 dpi in both
    /// orientations, with the resulting layout class), the unchanged scale of a pointer device and of an unknown DPI, and the
    /// touch-versus-pointer behaviour of the exact-value detail line.
    /// </summary>
    public sealed class RunDeviceProfileTests
    {
        private const int PhoneWidth = 1080;
        private const int PhoneHeight = 2400;
        private const float PhoneDpi = 420f;

        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;

        [TearDown]
        public void TearDown()
        {
            foreach (var item in rigs)
            {
                item.Dispose();
            }

            rigs.Clear();
            rig = null;
        }

        private static ShellScreen DeviceScreen(string name, int width, int height, FactoryHudLayout expected) =>
            new ShellScreen(name, width, height, 0, 0, 0, 0, expected);

        private static void SetPointerType(EventBase evt, string pointerType)
        {
            for (var type = evt.GetType(); type != null; type = type.BaseType)
            {
                var property = type.GetProperty("pointerType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                var setter = property?.GetSetMethod(true);
                if (setter != null)
                {
                    setter.Invoke(evt, new object[] { pointerType });
                    return;
                }
            }

            Assert.Fail("The event has no settable pointerType.");
        }

        private static void Send(VisualElement target, EventBase evt, string pointerType)
        {
            using (evt)
            {
                SetPointerType(evt, pointerType);
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // The value
        // ---------------------------------------------------------------------------------------------------

        [TestCase(true, false, false, ShellPrimaryInput.Touch)]
        [TestCase(true, true, true, ShellPrimaryInput.Touch)]
        [TestCase(false, false, true, ShellPrimaryInput.Pointer)]
        [TestCase(false, true, true, ShellPrimaryInput.Pointer)]
        [TestCase(false, true, false, ShellPrimaryInput.Touch)]
        [TestCase(false, false, false, ShellPrimaryInput.Pointer)]
        public void ThePrimaryInputIsTouchOnAMobilePlatformOrATouchScreenWithoutMouseOrKeyboard(
            bool mobile, bool touchscreen, bool mouseOrKeyboard, ShellPrimaryInput expected)
        {
            Assert.That(ShellDeviceProfile.Detect(mobile, touchscreen, mouseOrKeyboard), Is.EqualTo(expected));
        }

        [Test]
        public void TheProfileHoldsTheScreenDpiAndOrientationAndKnowsThePhysicalSizeOnlyWithADpi()
        {
            var portrait = ShellDeviceProfile.Touch(PhoneWidth, PhoneHeight, PhoneDpi);
            Assert.That(portrait.Orientation, Is.EqualTo(ShellOrientation.Portrait));
            Assert.That(portrait.HasKnownDpi, Is.True);
            Assert.That(portrait.PhysicalWidthMillimeters, Is.EqualTo(1080f / 420f * 25.4f).Within(0.01f));
            Assert.That(portrait.PhysicalHeightMillimeters, Is.EqualTo(2400f / 420f * 25.4f).Within(0.01f));

            var landscape = ShellDeviceProfile.Touch(PhoneHeight, PhoneWidth, PhoneDpi);
            Assert.That(landscape.Orientation, Is.EqualTo(ShellOrientation.Landscape));

            foreach (var unknown in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                var profile = ShellDeviceProfile.Touch(PhoneWidth, PhoneHeight, unknown);
                Assert.That(profile.HasKnownDpi, Is.False, $"dpi {unknown} is unknown");
                Assert.That(profile.PhysicalWidthMillimeters, Is.EqualTo(0f));
                Assert.That(profile.RequiredPixelsPerPoint, Is.EqualTo(0f), "no requirement without a DPI");
            }
        }

        [Test]
        public void OnlyATouchDeviceHasTapToPinAndLongPressAndOnlyAPointerDeviceHasHover()
        {
            var touch = ShellDeviceProfile.Touch(PhoneWidth, PhoneHeight, PhoneDpi);
            var pointer = ShellDeviceProfile.Pointer(1920, 1080, 96f);
            Assert.That(touch.PinsOnTapAndLongPress, Is.True);
            Assert.That(touch.ShowsHover, Is.False);
            Assert.That(pointer.PinsOnTapAndLongPress, Is.False);
            Assert.That(pointer.ShowsHover, Is.True);
        }

        [Test]
        public void ThePointerDeviceNeverRequiresAScaleAndTheProbeReadsTheRunningDevice()
        {
            Assert.That(ShellDeviceProfile.Pointer(PhoneWidth, PhoneHeight, PhoneDpi).RequiredPixelsPerPoint, Is.EqualTo(0f));
            var probed = ShellDeviceProfile.Probe();
            Assert.That(probed.ScreenWidthPx, Is.EqualTo(Screen.width));
            Assert.That(probed.ScreenHeightPx, Is.EqualTo(Screen.height));
        }

        // ---------------------------------------------------------------------------------------------------
        // The panel scale decision
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void AKnownDpiTouchDeviceBelowSevenMillimetersIsRaisedToExactlyTheRequiredScale()
        {
            var profile = ShellDeviceProfile.Touch(PhoneWidth, PhoneHeight, PhoneDpi);
            var decision = ShellPanelScale.Decide(profile, 1.68f);
            Assert.That(decision.Reason, Is.EqualTo(ShellScaleReason.RaisedForTouchTarget));
            Assert.That(decision.Raised, Is.True);
            Assert.That(decision.EffectivePixelsPerPoint, Is.EqualTo(PhoneDpi * 7f / 25.4f / 44f).Within(0.001f));
            Assert.That(profile.MillimetersFor(44f, decision.EffectivePixelsPerPoint), Is.GreaterThanOrEqualTo(7f));
            Assert.That(profile.MillimetersFor(44f, 1.68f), Is.LessThan(5f), "today's scale is about 4.5 mm at 420 dpi (F-10)");
        }

        [Test]
        public void ATouchDeviceWhoseAuthoredScaleIsLargeEnoughKeepsIt()
        {
            var tablet = ShellDeviceProfile.Touch(1600, 2560, 280f);
            var decision = ShellPanelScale.Decide(tablet, 2.1f);
            Assert.That(decision.Reason, Is.EqualTo(ShellScaleReason.AuthoredScaleLargeEnough));
            Assert.That(decision.EffectivePixelsPerPoint, Is.EqualTo(2.1f));
            Assert.That(tablet.MillimetersFor(44f, 2.1f), Is.GreaterThanOrEqualTo(7f));
        }

        [Test]
        public void APointerDeviceAndAnUnknownDpiKeepTheAuthoredScaleAndTheFallbackIsRecorded()
        {
            var pointer = ShellPanelScale.Decide(ShellDeviceProfile.Pointer(1920, 1080, 96f), 1.5f);
            Assert.That(pointer.Reason, Is.EqualTo(ShellScaleReason.PointerDevice));
            Assert.That(pointer.Raised, Is.False);
            Assert.That(pointer.EffectivePixelsPerPoint, Is.EqualTo(1.5f));

            var unknown = ShellPanelScale.Decide(ShellDeviceProfile.Touch(PhoneWidth, PhoneHeight, 0f), 1.68f);
            Assert.That(unknown.Reason, Is.EqualTo(ShellScaleReason.UnknownDpiFallback), "recorded: unknown DPI on a touch device");
            Assert.That(unknown.Raised, Is.False);
            Assert.That(unknown.EffectivePixelsPerPoint, Is.EqualTo(1.68f));
        }

        [Test]
        public void TheAuthoredScaleOfTheScaleWithScreenSizeSettingsMatchesTheTaskBaseline()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            try
            {
                settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                settings.referenceResolution = new Vector2Int(1280, 720);
                settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                settings.match = 0.5f;
                Assert.That(ShellPanelScale.AuthoredPixelsPerPoint(settings, PhoneWidth, PhoneHeight, 0f), Is.EqualTo(2.0885f).Within(0.001f), "portrait");
                Assert.That(ShellPanelScale.AuthoredPixelsPerPoint(settings, PhoneHeight, PhoneWidth, 0f), Is.EqualTo(1.6875f).Within(0.001f), "landscape");
                Assert.That(ShellPanelScale.AuthoredPixelsPerPoint(settings, 1920, 1080, 0f), Is.EqualTo(1.5f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ARaiseSwitchesTheLiveSettingsToAConstantScaleAndRestoreBringsTheAuthoredValuesBack()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            try
            {
                settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                settings.scale = 1f;
                var decision = ShellPanelScale.Decide(ShellDeviceProfile.Touch(PhoneWidth, PhoneHeight, PhoneDpi), 1.68f);
                var saved = ShellPanelScale.Apply(settings, decision);
                Assert.That(settings.scaleMode, Is.EqualTo(PanelScaleMode.ConstantPixelSize));
                Assert.That(settings.scale, Is.EqualTo(decision.EffectivePixelsPerPoint));
                ShellPanelScale.Restore(saved);
                Assert.That(settings.scaleMode, Is.EqualTo(PanelScaleMode.ScaleWithScreenSize));
                Assert.That(settings.scale, Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

#if UNITY_EDITOR
        // ---------------------------------------------------------------------------------------------------
        // 1080x2400 at 420 dpi in a real panel with the authored settings
        // ---------------------------------------------------------------------------------------------------

        public static IEnumerable<ShellScreen> PhoneOrientations()
        {
            yield return DeviceScreen("portrait 1080x2400", PhoneWidth, PhoneHeight, FactoryHudLayout.CompactPortrait);
            yield return DeviceScreen("landscape 2400x1080", PhoneHeight, PhoneWidth, FactoryHudLayout.CompactLandscape);
        }

        [UnityTest]
        public IEnumerator ATouchPhoneAtFourHundredTwentyDpiGivesEveryControlAtLeastSevenMillimetersAndAPhoneLayoutClass(
            [ValueSource(nameof(PhoneOrientations))] ShellScreen device)
        {
            var width = device.Width;
            var height = device.Height;
            var expected = device.Expected;
            var profile = ShellDeviceProfile.Touch(width, height, PhoneDpi);
            rig = ShellRig.CreateAuthored(device, profile);
            rigs.Add(rig);
            yield return rig.Layout();
            var shell = rig.Shell;
            shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();

            Assert.That(shell.ScaleDecision.Reason, Is.EqualTo(ShellScaleReason.RaisedForTouchTarget), shell.ScaleDecision.ToString());
            var pixelsPerPoint = rig.PixelsPerPoint;
            Assert.That(pixelsPerPoint, Is.EqualTo(profile.RequiredPixelsPerPoint).Within(0.01f), "the panel really uses the raised scale");
            Assert.That(profile.MillimetersFor(44f, pixelsPerPoint), Is.GreaterThanOrEqualTo(7f), "44 pt is at least 7 mm");

            // Measured on screen: a 44 pt control is 44 pt tall in the panel, which is at least 7 mm physically.
            foreach (var control in new VisualElement[] { shell.ZoomInButton, shell.CloseButton, shell.TabButton(ShellTab.Market), shell.CashButton })
            {
                var minimum = Mathf.Min(control.worldBound.width, control.worldBound.height);
                Assert.That(minimum, Is.GreaterThanOrEqualTo(44f - Tolerance), control.name);
                Assert.That(profile.MillimetersFor(minimum, pixelsPerPoint), Is.GreaterThanOrEqualTo(7f - 0.05f), $"{control.name} is {minimum} pt");
            }

            Assert.That(shell.Layout, Is.EqualTo(expected), $"the layout class follows the panel-point size ({width / pixelsPerPoint:0}x{height / pixelsPerPoint:0} pt)");
            Assert.That(FactoryHudDocument.HasSidePanel(shell.Layout), Is.False, "a 420 dpi phone keeps the phone layout in both orientations");
            AssertShellLayout(rig, "1080x2400 at 420 dpi");
        }

        [UnityTest]
        public IEnumerator APointerDeviceAndAnUnknownDpiKeepTodaysScaleSoALandscapePhoneGetsTheDesktopLayout()
        {
            var pointer = ShellDeviceProfile.Pointer(PhoneHeight, PhoneWidth, PhoneDpi);
            rig = ShellRig.CreateAuthored(DeviceScreen("pointer 2400x1080", PhoneHeight, PhoneWidth, FactoryHudLayout.Desktop), pointer);
            rigs.Add(rig);
            yield return rig.Layout();
            Assert.That(rig.Shell.ScaleDecision.Reason, Is.EqualTo(ShellScaleReason.PointerDevice));
            Assert.That(rig.PixelsPerPoint, Is.EqualTo(1.6875f).Within(0.01f), "the authored scale is kept");
            Assert.That(rig.Shell.Layout, Is.EqualTo(FactoryHudLayout.Desktop), "chosen by panel-point size, as the owner decided");
            AssertShellLayout(rig, "pointer device, authored scale");

            rig.Dispose();
            rigs.Clear();
            var unknown = ShellDeviceProfile.Touch(PhoneHeight, PhoneWidth, 0f);
            rig = ShellRig.CreateAuthored(DeviceScreen("unknown dpi 2400x1080", PhoneHeight, PhoneWidth, FactoryHudLayout.Desktop), unknown);
            rigs.Add(rig);
            yield return rig.Layout();
            Assert.That(rig.Shell.ScaleDecision.Reason, Is.EqualTo(ShellScaleReason.UnknownDpiFallback));
            Assert.That(rig.PixelsPerPoint, Is.EqualTo(1.6875f).Within(0.01f), "unknown DPI falls back to the authored scale");
            Assert.That(rig.Shell.Layout, Is.EqualTo(FactoryHudLayout.Desktop));
        }

        [UnityTest]
        public IEnumerator ChangingTheProfileRaisesAndRestoresThePanelScaleAndRelayoutsTheShell()
        {
            var screen = DeviceScreen("phone 1080x2400", PhoneWidth, PhoneHeight, FactoryHudLayout.CompactPortrait);
            rig = ShellRig.CreateAuthored(screen, ShellDeviceProfile.Pointer(PhoneWidth, PhoneHeight, PhoneDpi));
            rigs.Add(rig);
            yield return rig.Layout();
            Assert.That(rig.PixelsPerPoint, Is.EqualTo(2.0885f).Within(0.01f), "initial pointer scale");

            rig.Shell.ApplyDeviceProfile(ShellDeviceProfile.Touch(PhoneWidth, PhoneHeight, PhoneDpi));
            yield return ShellRig.Settle();
            Assert.That(rig.PixelsPerPoint, Is.GreaterThan(2.6f), "raised for touch");
            Assert.That(rig.Shell.Layout, Is.EqualTo(FactoryHudLayout.CompactPortrait));

            rig.Shell.ApplyDeviceProfile(ShellDeviceProfile.Pointer(PhoneWidth, PhoneHeight, PhoneDpi));
            yield return ShellRig.Settle();
            Assert.That(rig.PixelsPerPoint, Is.EqualTo(2.0885f).Within(0.01f), "the authored scale comes back");
        }
#endif

        // ---------------------------------------------------------------------------------------------------
        // Touch behaviour follows the profile, not the layout class
        // ---------------------------------------------------------------------------------------------------

        private IEnumerator Open(ShellDeviceProfile profile, ShellScreen screen)
        {
            rig = ShellRig.Create(screen, profile, 100);
            rigs.Add(rig);
            yield return rig.Layout();
            rig.Shell.SetCash(1_234_567_890L);
            yield return ShellRig.Settle();
        }

        [UnityTest]
        public IEnumerator OnATouchDeviceATapPinsTheExactValueAndHoverShowsNothingOnEveryLayoutClass([ValueSource(nameof(AllScreens))] ShellScreen screen)
        {
            yield return Open(ShellDeviceProfile.Touch(screen.Width, screen.Height, PhoneDpi), screen);
            var shell = rig.Shell;

            Send(shell.CashButton, PointerEnterEvent.GetPooled(), UnityEngine.UIElements.PointerType.mouse);
            yield return null;
            Assert.That(shell.DetailText, Is.Empty, "a touch device has no hover");

            Send(shell.CashButton, ClickEvent.GetPooled(), UnityEngine.UIElements.PointerType.touch);
            yield return null;
            Assert.That(shell.IsDetailPinned, Is.True, "a tap pins the exact value");
            Assert.That(shell.DetailText, Is.EqualTo("Cash: 1,234,567,890"));
            Send(shell.CashButton, ClickEvent.GetPooled(), UnityEngine.UIElements.PointerType.touch);
            yield return null;
            Assert.That(shell.IsDetailPinned, Is.False, "a second tap unpins it");
        }

        [UnityTest]
        public IEnumerator OnATouchDeviceALongPressShowsTheExactValue()
        {
            var screen = ShellScreens.All().First();
            yield return Open(ShellDeviceProfile.Touch(screen.Width, screen.Height, PhoneDpi), screen);
            var shell = rig.Shell;
            Send(shell.CashButton, PointerDownEvent.GetPooled(), UnityEngine.UIElements.PointerType.touch);
            var waited = 0f;
            while (waited < 0.9f && !shell.IsDetailPinned)
            {
                waited += 0.05f;
                yield return new WaitForSecondsRealtime(0.05f);
            }

            Assert.That(shell.IsDetailPinned, Is.True, "a 500 ms press shows the exact value on touch");
        }

        [UnityTest]
        public IEnumerator OnAPointerDeviceHoverShowsTheExactValueAndAMouseClickOrPressNeverPinsOnEveryLayoutClass([ValueSource(nameof(AllScreens))] ShellScreen screen)
        {
            yield return Open(ShellDeviceProfile.Pointer(screen.Width, screen.Height, 96f), screen);
            var shell = rig.Shell;

            Send(shell.CashButton, PointerEnterEvent.GetPooled(), UnityEngine.UIElements.PointerType.mouse);
            yield return null;
            Assert.That(shell.DetailText, Is.EqualTo("Cash: 1,234,567,890"), "hover shows the exact value with a pointer");
            Send(shell.CashButton, PointerLeaveEvent.GetPooled(), UnityEngine.UIElements.PointerType.mouse);
            yield return null;
            Assert.That(shell.DetailText, Is.Empty, "leaving hides it");

            Send(shell.CashButton, ClickEvent.GetPooled(), UnityEngine.UIElements.PointerType.mouse);
            yield return null;
            Assert.That(shell.IsDetailPinned, Is.False, "a mouse click does not pin");

            Send(shell.CashButton, PointerDownEvent.GetPooled(), UnityEngine.UIElements.PointerType.mouse);
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.That(shell.IsDetailPinned, Is.False, "a mouse press never long-presses");

            // Focus still shows the value for keyboard users, whatever the device.
            shell.CashButton.Focus();
            yield return null;
            Assert.That(shell.DetailText, Is.EqualTo("Cash: 1,234,567,890"));
        }

        [UnityTest]
        public IEnumerator ATouchOrPenPressOnAPointerDeviceStillPinsSoAHybridScreenWorks()
        {
            var screen = ShellScreens.All().First();
            yield return Open(ShellDeviceProfile.Pointer(screen.Width, screen.Height, 96f), screen);
            Send(rig.Shell.CashButton, ClickEvent.GetPooled(), UnityEngine.UIElements.PointerType.touch);
            yield return null;
            Assert.That(rig.Shell.IsDetailPinned, Is.True);
        }

        public static IEnumerable<ShellScreen> AllScreens() => ShellScreens.All();
    }
}
