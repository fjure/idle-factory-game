using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
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
    /// T191 (FR-032, Art Bible 9.3, F-4): the shell-owned text scale from 100 to 200 percent. At every screen class (with the
    /// rotated forms) and at 100, 150 and 200 percent, the shell at rest, with the worst-case top strip, notice and pinned
    /// detail line, with a worst-case sheet, and with the worst-case confirmation dialog has no overlap, no clipped or squeezed
    /// label, a visible focused control and 44 pt targets, and 12 pt (scaled) text at the minimum.
    /// </summary>
    public sealed class RunTextScaleTests
    {
        private const long Huge = 9_000_000_000_000_000_000L;

        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<int> Percents() => new[] { 100, 150, 200 };

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

        private IEnumerator Open([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
        }

        // ---------------------------------------------------------------------------------------------------
        // The factor
        // ---------------------------------------------------------------------------------------------------

        [TestCase(0.5f, 1f, 100)]
        [TestCase(1f, 1f, 100)]
        [TestCase(1.04f, 1.04f, 100)]
        [TestCase(1.06f, 1.06f, 110)]
        [TestCase(1.3f, 1.3f, 130)]
        [TestCase(1.5f, 1.5f, 150)]
        [TestCase(2f, 2f, 200)]
        [TestCase(3.4f, 2f, 200)]
        [TestCase(float.NaN, 1f, 100)]
        [TestCase(float.PositiveInfinity, 1f, 100)]
        [TestCase(-2f, 1f, 100)]
        public void TheFactoryClampsToOneToTwoAndRoundsToTenPercentSteps(float input, float clamped, int percent)
        {
            Assert.That(ShellTextScale.Clamp(input), Is.EqualTo(clamped).Within(0.0001f));
            Assert.That(ShellTextScale.Percent(input), Is.EqualTo(percent));
            Assert.That(ShellTextScale.ClassName(percent), Is.EqualTo("run-text-scale--" + percent));
        }

        [Test]
        public void ThereIsOneStyleClassPerTenPercentStepFromOneHundredToTwoHundred()
        {
            Assert.That(ShellTextScale.Steps(), Is.EqualTo(new[] { 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200 }));
        }

        [Test]
        public void OutsideAnAndroidDeviceTheSystemFactorIsOneHundredPercent()
        {
            Assert.That(ShellTextScale.ProbeSystemFactor(), Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator TheShellStartsAtOneHundredPercentAndAppliesOneClassAtATime()
        {
            yield return Open(ShellScreens.All().First(), 100);
            var shell = rig.Shell;
            Assert.That(shell.TextScalePercent, Is.EqualTo(100));
            Assert.That(shell.Root.ClassListContains("run-text-scale--100"), Is.True);

            shell.ApplyTextScale(1.5f);
            Assert.That(shell.TextScale, Is.EqualTo(1.5f));
            Assert.That(shell.Root.ClassListContains("run-text-scale--150"), Is.True);
            Assert.That(shell.Root.ClassListContains("run-text-scale--100"), Is.False);

            shell.ApplyTextScale(9f);
            Assert.That(shell.TextScalePercent, Is.EqualTo(200), "a factor above two is clamped to 200 percent");
            Assert.That(shell.Root.GetClasses().Count(name => name.StartsWith("run-text-scale--")), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TheScaleSurvivesARebuildOfTheShell()
        {
            yield return Open(ShellScreens.All().First(), 150);
            Assert.That(rig.Shell.Build(), Is.True);
            yield return rig.Layout();
            Assert.That(rig.Shell.TextScalePercent, Is.EqualTo(150));
            Assert.That(rig.Shell.Root.ClassListContains("run-text-scale--150"), Is.True);
        }

        [UnityTest]
        public IEnumerator TextFollowsTheFactorAndTheCaptionMinimumHoldsAtOneHundredPercent([ValueSource(nameof(Steps))] int percent)
        {
            yield return Open(ShellScreens.All().First(), percent);
            yield return ShellRig.Settle();
            var shell = rig.Shell;
            var expected = percent / 100f;
            var tabLabel = shell.TabButton(ShellTab.Contracts).Q<Label>(className: "run-tab__label");
            Assert.That(tabLabel.resolvedStyle.fontSize, Is.EqualTo(12f * expected).Within(0.51f), "tab labels are captions");
            Assert.That(shell.CashButton.Q<Label>("cash-value").resolvedStyle.fontSize, Is.EqualTo(16f * expected).Within(0.51f), "HUD values");
            Assert.That(shell.Root.Q<Label>("detail-line").resolvedStyle.fontSize, Is.EqualTo(14f * expected).Within(0.51f), "body text");
            if (percent == 100)
            {
                Assert.That(tabLabel.resolvedStyle.fontSize, Is.EqualTo(12f), "the 12 pt minimum holds at 100 percent");
            }
        }

        public static IEnumerable<int> Steps() => ShellTextScale.Steps();

        // ---------------------------------------------------------------------------------------------------
        // Layout at every screen class
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheShellAtRestHasNoOverlapAndNoClippedLabel([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            AssertShellLayout(rig, "closed shell");
        }

        [UnityTest]
        public IEnumerator TheWorstCaseStripNoticeAndPinnedDetailLineStayContained([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            shell.SetCash(Huge);
            shell.SetRp(Huge, true);
            shell.SetStore(Huge, Huge);
            shell.SetOverdrive(RunShellFixtures.OverdriveStatusIn(OverdriveStage.CoolingDown));
            shell.ShowNotice(RunShellDocument.RotationCancelledNotice, ShellNoticeKind.Warning);
            shell.CashButton.Focus();
            yield return ShellRig.Settle();

            AssertShellLayout(rig, "worst-case strip, notice and detail line");
            var stripControls = new List<VisualElement> { shell.CashButton, shell.RpButton, shell.StoreButton };
            if (!shell.IsSidePanelLayout)
            {
                stripControls.Add(shell.OverdriveButton);
            }

            foreach (var control in stripControls)
            {
                Assert.That(IsDisplayed(control), Is.True, control.name);
                AssertWithin(shell.TopStrip.worldBound, control.worldBound, $"{screen} @{percent}%: {control.name} stays in the top strip");
            }

            shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
            AssertShellLayout(rig, "worst-case strip with an open sheet");
        }

        [UnityTest]
        public IEnumerator AWorstCaseSheetScrollsAndEveryFocusedControlIsVisible([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            var panel = new TestShellPanel(ShellPanelIds.Items, "Items", 16, true, true);
            shell.Panels.Register(panel);
            shell.SelectTab(ShellTab.Items);
            yield return ShellRig.Settle();
            AssertShellLayout(rig, "open worst-case sheet");

            var scroll = shell.PanelScroll;
            var viewport = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport");
            var range = Mathf.Max(0f, scroll.contentContainer.layout.height - viewport.layout.height);
            if (percent >= 150)
            {
                Assert.That(range, Is.GreaterThan(0f), $"{screen} @{percent}%: the worst-case content scrolls");
            }

            foreach (var offset in new[] { 0f, 0.5f, 1f })
            {
                scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, range * offset);
                yield return ShellRig.Settle();
                AssertScrollContentClipped(scroll, $"{screen} @{percent}% scrolled to {offset:P0}");
            }

            // The last control is reached through scrolling alone: focus brings it into view.
            var safe = rig.SafeRect();
            foreach (var button in new[] { panel.Buttons.Last(), panel.Buttons.First(), panel.Buttons[panel.Buttons.Count / 2] })
            {
                scroll.scrollOffset = new Vector2(0f, button == panel.Buttons.Last() ? 0f : float.MaxValue);
                yield return ShellRig.Settle();
                button.Focus();
                yield return ShellRig.Settle();
                Assert.That(button.focusController.focusedElement, Is.SameAs(button), $"{screen} @{percent}%: '{button.name}' did not take focus");
                AssertWithin(viewport.worldBound, button.worldBound, $"{screen} @{percent}%: focused '{button.name}' is not scrolled into view");
                AssertWithin(safe, button.worldBound, $"{screen} @{percent}%: focused '{button.name}' leaves the safe area");
            }

            foreach (var control in new VisualElement[] { shell.CashButton, shell.TabButton(ShellTab.Prestige), shell.TabButton(ShellTab.Contracts), shell.ZoomInButton })
            {
                control.Focus();
                yield return ShellRig.Settle();
                Assert.That(control.focusController.focusedElement, Is.SameAs(control), $"{screen} @{percent}%: {control.name}");
                AssertWithin(safe, control.worldBound, $"{screen} @{percent}%: focused {control.name} leaves the safe area");
                Assert.That(control.ClassListContains("run-control--focused"), Is.True, $"{screen} @{percent}%: {control.name} shows its focus ring");
            }

            AssertShellLayout(rig, "worst-case sheet after focus changes");
        }

        [UnityTest]
        public IEnumerator TheConfirmationDialogWithWorstCaseTextStaysContained([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            var invoker = shell.TabButton(ShellTab.Prestige);
            shell.SelectTab(ShellTab.Prestige);
            Assert.That(shell.RequestConfirmation(new ConfirmationRequest
            {
                Title = "Sell the factory and start run 12?",
                Message = string.Concat(Enumerable.Repeat("Selling resets Cash, stock, machines and expansion. Industry points, Research and Industry upgrades are kept. ", 12)),
                ConfirmLabel = "Sell and restart",
                CancelLabel = "Keep playing",
                Destructive = true,
                Invoker = invoker,
            }), Is.True);
            yield return ShellRig.Settle();

            var safe = rig.SafeRect();
            var card = shell.DialogCard.worldBound;
            AssertWithin(safe, card, $"{screen} @{percent}%: the dialog card stays inside the safe area");
            AssertWithin(card, shell.DialogCancelButton.worldBound, $"{screen} @{percent}%: Cancel stays in the card");
            AssertWithin(card, shell.DialogConfirmButton.worldBound, $"{screen} @{percent}%: Confirm stays in the card");
            Assert.That(Intersects(shell.DialogCancelButton.worldBound, shell.DialogConfirmButton.worldBound), Is.False, $"{screen} @{percent}%: the two buttons do not overlap");
            Assert.That(shell.DialogCancelButton.focusController.focusedElement, Is.SameAs(shell.DialogCancelButton), "the dialog opens on Cancel");
            AssertTextAndTargets(shell.DialogLayer, safe, $"{screen} @{percent}% dialog", MinimumFontSize * percent / 100f);
        }

        [UnityTest]
        public IEnumerator ChangingTheScaleWithASheetOpenKeepsTheLayoutContained([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, 100);
            var shell = rig.Shell;
            shell.Panels.Register(new TestShellPanel(ShellPanelIds.Market, "Market", 16));
            shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
            AssertShellLayout(rig, "before the change");

            shell.ApplyTextScale(percent / 100f);
            rig.SetTextScalePercent(percent);
            yield return ShellRig.Settle();
            Assert.That(shell.TextScalePercent, Is.EqualTo(percent));
            AssertShellLayout(rig, "after a live scale change with an open sheet");
        }

        [UnityTest]
        public IEnumerator TheTabBarWrapsIntoRowsFromOneHundredTwentyPercentAndKeepsOneRowAtOneHundred([ValueSource(nameof(PhoneScreens))] ShellScreen screen)
        {
            yield return Open(screen, 100);
            Assert.That(rig.Shell.TabBar.worldBound.height, Is.LessThan(2f * 56f), $"{screen}: one row at 100 percent");

            rig.Shell.ApplyTextScale(2f);
            rig.SetTextScalePercent(200);
            yield return ShellRig.Settle();
            var tabs = Enumerable.Range(0, 6).Select(index => rig.Shell.TabButton((ShellTab)index)).ToList();
            Assert.That(tabs.Select(tab => Mathf.Round(tab.worldBound.yMin)).Distinct().Count(), Is.GreaterThan(1), $"{screen}: the six tabs wrap into rows at 200 percent");
            AssertShellLayout(rig, "wrapped tab bar");
        }

        public static IEnumerable<ShellScreen> PhoneScreens() => ShellScreens.Phones();
    }
}
