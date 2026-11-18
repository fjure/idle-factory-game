#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T540 (Feature 006, FR-035, research R14, contracts/presentation-model.md section 5): the milestone banner. It is non-blocking (input outside its own box is
    /// never captured), lasts at most 2.5 s, is dismissed by a tap or by Back and Escape without moving focus, is static under reduced motion, carries a screen-reader text,
    /// stays readable at 200 % text on every screen class, and never covers Cash, Overdrive or the camera rectangle.
    /// </summary>
    public sealed class MilestoneBannerTests
    {
        private const string Main = "Level 3. 2 new locations open.";
        private const string Extra = "Deck extended.";
        private ShellRig rig;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        [TearDown]
        public void TearDown()
        {
            rig?.Dispose();
            rig = null;
        }

        private IEnumerator Make(ShellScreen screen, int percent)
        {
            rig = ShellRig.Create(screen, null, percent);
            yield return rig.Layout(screen);
        }

        private MilestoneBanner Banner => rig.Shell.MilestoneBanner;

        [UnityTest]
        public IEnumerator TheBannerLivesInTheWorldColumnAndShowsNothingUntilAMilestoneArrives()
        {
            yield return Make(ShellScreens.All().First(s => s.Name.StartsWith("phone-portrait 390")), 100);
            Assert.That(Banner, Is.Not.Null, "the shell attaches its banner");
            Assert.That(Banner.IsShown, Is.False, "no milestone, no banner");
            Assert.That(Banner.Root.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            rig.Shell.TickMilestoneBanner(1f);
            Assert.That(Banner.IsShown, Is.False);
            rig.Shell.ShowMilestoneBanner(string.Empty, string.Empty, false);
            Assert.That(Banner.IsShown, Is.False, "an empty text shows nothing");
        }

        [UnityTest]
        public IEnumerator TheBannerLastsAtMostTwoAndAHalfSecondsAndFadesInOnlyInNormalMotion()
        {
            yield return Make(ShellScreens.All().First(s => s.Name.StartsWith("phone-portrait 390")), 100);
            rig.Shell.ShowMilestoneBanner(Main, Extra, false);
            Assert.That(Banner.IsShown, Is.True);
            Assert.That(Banner.Root.style.opacity.value, Is.EqualTo(0f), "normal motion starts transparent");
            rig.Shell.TickMilestoneBanner(0.075f);
            Assert.That(Banner.Root.style.opacity.value, Is.InRange(0.01f, 0.99f), "and fades in");
            rig.Shell.TickMilestoneBanner(0.2f);
            Assert.That(Banner.Root.style.opacity.value, Is.EqualTo(1f));
            rig.Shell.TickMilestoneBanner(2.0f);
            Assert.That(Banner.IsShown, Is.True, "2.275 s: still up");
            rig.Shell.TickMilestoneBanner(0.3f);
            Assert.That(Banner.IsShown, Is.False, "gone by 2.5 s");
            Assert.That(MilestoneBanner.MaxSeconds, Is.EqualTo(2.5f));

            rig.Shell.ShowMilestoneBanner(Main, string.Empty, true);
            Assert.That(Banner.Root.style.opacity.value, Is.EqualTo(1f), "reduced motion: static, no fade");
            Assert.That(Banner.SecondaryLabel.resolvedStyle.display, Is.EqualTo(DisplayStyle.None).Or.EqualTo(DisplayStyle.Flex));
            rig.Shell.TickMilestoneBanner(0.05f);
            Assert.That(Banner.Root.style.opacity.value, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator ATapOnTheBannerDismissesItAndBackOrEscapeDoesWithoutMovingFocus()
        {
            yield return Make(ShellScreens.All().First(s => s.Name.StartsWith("phone-portrait 390")), 100);
            var closed = 0;
            Banner.Closed += () => closed++;
            rig.Shell.CashButton.Focus();
            var focused = rig.Root.panel.focusController.focusedElement;
            Assert.That(focused, Is.Not.Null);

            rig.Shell.ShowMilestoneBanner(Main, Extra, false);
            yield return ShellRig.Settle();
            Assert.That(Banner.Root.focusable, Is.False, "the banner never takes focus");
            Assert.That(rig.Root.panel.focusController.focusedElement, Is.SameAs(focused), "showing it moves no focus");
            var down = PointerDownEvent.GetPooled();
            down.target = Banner.Root;
            Banner.Root.SendEvent(down);
            Assert.That(Banner.IsShown, Is.False, "a tap dismisses it");
            Assert.That(rig.Root.panel.focusController.focusedElement, Is.SameAs(focused), "a tap moves no focus");
            Assert.That(closed, Is.EqualTo(1));

            rig.Shell.ShowMilestoneBanner(Main, Extra, false);
            yield return null;
            Assert.That(rig.Shell.HandleBack(), Is.True, "Back dismisses the banner");
            Assert.That(Banner.IsShown, Is.False);
            Assert.That(rig.Root.panel.focusController.focusedElement, Is.SameAs(focused), "dismissing it moves no focus");
            yield return null;
            Assert.That(rig.Shell.HandleBack(), Is.False, "with the banner gone Back has nothing left to pop");
        }

        [UnityTest]
        public IEnumerator BackDismissesOnlyTheBannerAndLeavesAnOpenDialogAlone()
        {
            yield return Make(ShellScreens.All().First(s => s.Name.StartsWith("phone-portrait 390")), 100);
            Assert.That(rig.Shell.RequestConfirmation(new ConfirmationRequest { Title = "Confirm", Message = "Sure?" }), Is.True);
            rig.Shell.ShowMilestoneBanner(Main, string.Empty, false);
            yield return null;
            Assert.That(rig.Shell.HandleBack(), Is.True);
            Assert.That(Banner.IsShown, Is.False, "the banner pops first");
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True, "the dialog stays: one frame per press");
        }

        [UnityTest]
        public IEnumerator TheBannerCapturesNoInputOutsideItsOwnBoxAndCarriesAScreenReaderText()
        {
            yield return Make(ShellScreens.All().First(s => s.Name.StartsWith("phone-portrait 390")), 100);
            rig.Shell.ShowMilestoneBanner(Main, Extra, false);
            yield return ShellRig.Settle();
            Assert.That(Banner.Root.pickingMode, Is.EqualTo(PickingMode.Position));
            Assert.That(Banner.PrimaryLabel.pickingMode, Is.EqualTo(PickingMode.Ignore));
            var box = Banner.Root.worldBound;
            Assert.That(box.width, Is.GreaterThan(0f));

            // Nothing outside the box resolves to the banner: a point just below it and a point to the left of it.
            var below = rig.Root.panel.Pick(new Vector2(box.center.x, box.yMax + 6f));
            Assert.That(below == Banner.Root || (below != null && Banner.Root.Contains(below)), Is.False, "a point below the banner is not the banner");
            Assert.That(Banner.AccessibleText, Is.EqualTo(Main + " " + Extra));
            Assert.That(Banner.Root.tooltip, Is.EqualTo(Main + " " + Extra));
            Assert.That(Banner.PrimaryLabel.text, Is.EqualTo(Main));
            Assert.That(Banner.SecondaryLabel.text, Is.EqualTo(Extra));
        }

        [UnityTest]
        public IEnumerator TheBannerIsReadableAndCoversNeitherCashNorOverdriveNorTheCameraRectOnEveryClassAtOneAndTwoHundredPercent(
            [ValueSource(nameof(Screens))] ShellScreen screen,
            [Values(100, 200)] int percent)
        {
            yield return Make(screen, percent);
            rig.Shell.ShowMilestoneBanner(Main, Extra, false);
            rig.Shell.TickMilestoneBanner(0.3f);
            yield return ShellRig.Settle();
            var where = screen.Name + " at " + percent + " %";
            var box = Banner.Root.worldBound;
            var panel = rig.Root.panel.visualTree.worldBound;
            Assert.That(Banner.IsShown, Is.True, where);
            Assert.That(box.height, Is.GreaterThanOrEqualTo(43.5f), where + ": at least 44 pt, a dismiss target");
            Assert.That(box.xMin, Is.GreaterThanOrEqualTo(panel.xMin - 0.5f), where);
            Assert.That(box.xMax, Is.LessThanOrEqualTo(panel.xMax + 0.5f), where);
            Assert.That(box.yMax, Is.LessThanOrEqualTo(panel.yMax + 0.5f), where);

            // The text is not truncated: each label is as tall as its wrapped text needs (a clipped label would be shorter than its content).
            foreach (var label in new[] { Banner.PrimaryLabel, Banner.SecondaryLabel })
            {
                var preferred = label.MeasureTextSize(label.text, label.resolvedStyle.width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined);
                Assert.That(label.worldBound.height, Is.GreaterThanOrEqualTo(preferred.y - 1f), where + ": " + label.name + " shows all its lines");
            }

            foreach (var other in new VisualElement[] { rig.Shell.CashButton, rig.Shell.OverdriveButton })
            {
                var o = other.worldBound;
                if (o.width > 0f && o.height > 0f)
                {
                    Assert.That(box.Overlaps(o), Is.False, where + ": the banner does not cover " + other.name);
                }
            }

            Assert.That(rig.Shell.TryGetUnobstructedScreenRect(screen.Height, out var camera), Is.True, where);
            var cameraTopInPanel = screen.Height - (camera.y + camera.height);
            Assert.That(box.yMax, Is.LessThanOrEqualTo(cameraTopInPanel + 0.5f), where + ": the shell frames the camera below the banner, so it covers no framed world");
        }
    }
}
#endif
