using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using static IdleFactory.Tests.PlayMode.MarketPanelTestSupport;
#endif
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

#if UNITY_EDITOR
namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T362 (batch-A Play-Tester F8 and F9; owner decisions 2026-10-15 on #417 and #382; QA-12 "Shell focus" and "Top strip labels").
    /// F8: focus that lands on a scroll bar (a child of the sheet ScrollView outside its content container) used to make
    /// <c>RunShellDocument.OnFocusIn</c> throw an ArgumentException from <see cref="ScrollView.ScrollTo"/>; the guard skips such a
    /// target and <see cref="RunShellDocument.FocusWhenReady"/> stops retrying once focus has moved elsewhere. F9: the top strip says
    /// "Cash", "Store" and "Overdrive" in visible text and in the accessible name (the tooltip) at every screen class at 100 and 200
    /// percent text. The console stays free of the exception (Unity fails a test on a logged exception).
    /// </summary>
    public sealed class RunShellFocusAndLabelsTests
    {
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

        private IEnumerator Open(ShellScreen screen, int percent = 100)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
        }

        private static VisualElement Focused(VisualElement any) => any.focusController?.focusedElement as VisualElement;

        /// <summary>The elements a click on a scroll bar can focus: the scrollers, their sliders, draggers and arrow buttons.</summary>
        private static List<VisualElement> ScrollBarParts(ScrollView scroll)
        {
            var parts = new List<VisualElement>();
            foreach (var scroller in new[] { scroll.verticalScroller, scroll.horizontalScroller })
            {
                parts.Add(scroller);
                parts.Add(scroller.slider);
                parts.Add(scroller.lowButton);
                parts.Add(scroller.highButton);
                parts.Add(scroller.slider.Q<VisualElement>(className: "unity-base-slider__dragger"));
            }

            // A hidden scroller leaves the hierarchy, so it has no panel to send events through.
            return parts.Where(part => part != null && part.panel != null).ToList();
        }

        // ---------------------------------------------------------------------------------------------------
        // F8: the focus-in scroll guard
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ScrollIntoViewSkipsTheScrollBarsOfAScrollViewAndStillScrollsItsContent()
        {
            yield return Open(ShellScreens.All().First());
            var scroll = new ScrollView();
            scroll.style.height = 100f;
            for (var i = 0; i < 30; i++)
            {
                scroll.Add(new Button { name = "content-" + i, text = "Row " + i });
            }

            rig.Root.Add(scroll);
            yield return ShellRig.Settle();

            foreach (var part in ScrollBarParts(scroll))
            {
                var scrolled = true;
                Assert.DoesNotThrow(() => scrolled = RunShellDocument.ScrollIntoView(part), part.name + " is a scroll bar part, not content");
                Assert.That(scrolled, Is.False, part.name + " has nothing to scroll to");
            }

            Assert.That(RunShellDocument.ScrollIntoView(scroll.contentContainer), Is.False, "the content container itself is not scrolled to");
            Assert.That(RunShellDocument.ScrollIntoView(scroll.Q<Button>("content-29")), Is.True, "a content row still scrolls into view");
            Assert.That(RunShellDocument.ScrollIntoView(null), Is.False);
            // An ArgumentException from the guard would be logged as an exception and fail this test (Unity fails on logged exceptions).
        }

        [UnityTest]
        public IEnumerator ATabSwitchFollowedByScrollBarFocusDoesNotThrowAtEveryScreenClass(
            [ValueSource(typeof(MarketPanelTestSupport), nameof(Screens))] ShellScreen screen,
            [ValueSource(typeof(MarketPanelTestSupport), nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            shell.Panels.Register(new TestShellPanel(ShellPanelIds.Market, "Market", 20));
            shell.Panels.Register(new TestShellPanel(ShellPanelIds.Contracts, "Contracts", 20));

            // The report trigger: a tab switch, then clicks on the sheet scroll bar.
            shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
            shell.SelectTab(ShellTab.Contracts);
            yield return ShellRig.Settle();
            shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();

            var scroll = shell.PanelScroll;
            foreach (var part in ScrollBarParts(scroll))
            {
                // A real focus change sends a FocusInEvent through the shell root; the guard itself must also accept the part.
                if (part.focusable)
                {
                    part.Focus();
                }

                Assert.DoesNotThrow(() => RunShellDocument.ScrollIntoView(part), part.name);
                yield return null;
            }

            // The real controls: focusing the slider the way a click does, then a content control, must both work.
            scroll.verticalScroller.slider.Focus();
            yield return ShellRig.Settle();
            var first = shell.GetFocusOrder().First(control => scroll.contentContainer.Contains(control));
            first.Focus();
            yield return ShellRig.Settle();
            Assert.That(Focused(rig.Root), Is.SameAs(first), $"{screen} @{percent}%: focus still moves to sheet content after the scroll bar");
            AssertShellLayout(rig, "after scroll bar focus");
            // An ArgumentException from the guard would be logged as an exception and fail this test (Unity fails on logged exceptions).
        }

        // ---------------------------------------------------------------------------------------------------
        // F8: FocusWhenReady stops retrying once focus has moved elsewhere
        // ---------------------------------------------------------------------------------------------------

        private Button Added(string name, bool displayed = true)
        {
            var button = RunShellDocument.CreateButton(name, name);
            button.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
            rig.Root.Add(button);
            return button;
        }

        [UnityTest]
        public IEnumerator FocusWhenReadyStopsRetryingOnceFocusHasMovedElsewhere()
        {
            yield return Open(ShellScreens.All().First());
            var late = Added("late-control", false);
            var other = Added("other-control");
            yield return ShellRig.Settle();

            rig.Shell.FocusWhenReady(late);
            Assert.That(rig.Shell.FocusRetryPending, Is.True, "an element that cannot take focus yet keeps retrying");

            // The player moves focus elsewhere before the element becomes ready.
            other.Focus();
            late.style.display = DisplayStyle.Flex;
            yield return new WaitForSeconds(0.4f);

            Assert.That(Focused(rig.Root), Is.SameAs(other), "the retry did not take focus back");
            Assert.That(rig.Shell.FocusRetryPending, Is.False, "the retry stopped");
        }

        [UnityTest]
        public IEnumerator FocusWhenReadyStillFocusesAnElementThatBecomesReadyWhenNothingElseTookFocus()
        {
            yield return Open(ShellScreens.All().First());
            var late = Added("late-control", false);
            yield return ShellRig.Settle();

            rig.Shell.FocusWhenReady(late);
            Assert.That(rig.Shell.FocusRetryPending, Is.True);
            late.style.display = DisplayStyle.Flex;
            yield return new WaitForSeconds(0.4f);

            Assert.That(Focused(rig.Root), Is.SameAs(late));
            Assert.That(rig.Shell.FocusRetryPending, Is.False, "the retry ends once the element has focus");
        }

        [UnityTest]
        public IEnumerator ANewFocusWhenReadyRequestSupersedesAnOlderOne()
        {
            yield return Open(ShellScreens.All().First());
            var first = Added("first-late", false);
            var second = Added("second-late", false);
            yield return ShellRig.Settle();

            rig.Shell.FocusWhenReady(first);
            rig.Shell.FocusWhenReady(second);
            first.style.display = DisplayStyle.Flex;
            second.style.display = DisplayStyle.Flex;
            yield return new WaitForSeconds(0.4f);

            Assert.That(Focused(rig.Root), Is.SameAs(second), "the latest request wins");
        }

        [UnityTest]
        public IEnumerator FocusWhenReadyStopsWhenTheElementLeavesThePanel()
        {
            yield return Open(ShellScreens.All().First());
            var late = Added("late-control", false);
            yield return ShellRig.Settle();

            rig.Shell.FocusWhenReady(late);
            late.RemoveFromHierarchy();
            yield return new WaitForSeconds(0.1f);
            Assert.That(rig.Shell.FocusRetryPending, Is.False, "a removed element is no longer retried");
        }

        // ---------------------------------------------------------------------------------------------------
        // #398 T4, the shared focus rule: a control that is about to be hidden gives its focus to a visible sibling
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator MoveFocusBeforeHidingMovesFocusToTheNextVisibleControlAndOtherwiseThePreviousOne()
        {
            yield return Open(ShellScreens.All().First());
            var before = Added("before-control");
            var hiding = Added("hiding-control");
            var after = Added("after-control");
            yield return ShellRig.Settle();

            hiding.Focus();
            Assert.That(rig.Shell.MoveFocusBeforeHiding(hiding), Is.True);
            hiding.style.display = DisplayStyle.None;
            yield return ShellRig.Settle();
            Assert.That(Focused(rig.Root), Is.SameAs(after), "the next control in the focus order takes it");

            // Without a later control, the previous one does.
            var last = rig.Shell.GetFocusOrder().Last();
            last.Focus();
            Assert.That(rig.Shell.MoveFocusBeforeHiding(last), Is.True);
            yield return ShellRig.Settle();
            Assert.That(Focused(rig.Root), Is.Not.SameAs(last));
            Assert.That(Focused(rig.Root), Is.Not.Null);

            // A control that does not hold focus changes nothing.
            before.Focus();
            Assert.That(rig.Shell.MoveFocusBeforeHiding(after), Is.False);
            Assert.That(Focused(rig.Root), Is.SameAs(before));
        }

        // ---------------------------------------------------------------------------------------------------
        // F9: "Cash", "Store" and "Overdrive" in visible text and accessible name
        // ---------------------------------------------------------------------------------------------------

        private static void AssertCaption(VisualElement cell, string captionName, string text, string where)
        {
            var caption = cell.Q<Label>(captionName);
            Assert.That(caption, Is.Not.Null, where + ": " + captionName);
            Assert.That(caption.text, Is.EqualTo(text), where);
            Assert.That(IsDisplayed(caption), Is.True, where + ": " + captionName + " is displayed");
            Assert.That(caption.worldBound.width, Is.GreaterThan(0f), where + ": " + captionName + " has a width");
            Assert.That(caption.resolvedStyle.fontSize, Is.GreaterThanOrEqualTo(MinimumFontSize - Tolerance), where + ": " + captionName + " keeps the minimum text size");
        }

        [UnityTest]
        public IEnumerator TheTopStripNamesCashStoreAndOverdriveInTextAndInTheAccessibleName(
            [ValueSource(typeof(MarketPanelTestSupport), nameof(Screens))] ShellScreen screen,
            [ValueSource(typeof(MarketPanelTestSupport), nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            var where = $"{screen} @{percent}%";
            shell.SetCash(1234);
            shell.SetRp(56, true);
            shell.SetStore(120, 500);
            shell.SetOverdrive(RunShellFixtures.OverdriveStatusIn(OverdriveStage.Ready));
            yield return ShellRig.Settle();

            AssertCaption(shell.CashButton, "cash-caption", "Cash", where);
            AssertCaption(shell.StoreButton, "store-caption", "Store", where);
            AssertCaption(shell.OverdriveButton, "overdrive-name", "Overdrive", where);
            Assert.That(shell.OverdriveButton.Q<Label>("overdrive-caption").text, Is.EqualTo("Ready"), where + ": the state stays beside the name");
            Assert.That(shell.CashButton.Q<Label>("cash-value").text, Is.EqualTo(QuantityFormatter.Format(1234)), where + ": the value is unchanged");
            Assert.That(shell.StoreButton.Q<Label>("store-value").text, Is.EqualTo("24%"), where);

            Assert.That(shell.CashButton.tooltip, Is.EqualTo("Cash " + QuantityFormatter.Format(1234)), where + ": accessible name of Cash");
            Assert.That(shell.StoreButton.tooltip, Is.EqualTo("Store 24 percent used"), where + ": accessible name of the store bar");
            Assert.That(shell.OverdriveButton.tooltip, Does.StartWith("Overdrive Ready."), where + ": accessible name of Overdrive");

            // Each caption stays inside its own control, and the shell layout (overlap, safe area, 44 pt targets) still holds.
            AssertWithin(shell.CashButton.worldBound, shell.CashButton.Q<Label>("cash-caption").worldBound, where + ": Cash caption inside its control");
            AssertWithin(shell.StoreButton.worldBound, shell.StoreButton.Q<Label>("store-caption").worldBound, where + ": Store caption inside its control");
            AssertWithin(shell.OverdriveButton.worldBound, shell.OverdriveButton.Q<Label>("overdrive-name").worldBound, where + ": Overdrive name inside its control");
            AssertShellLayout(rig, "top strip with captions");
        }

        [UnityTest]
        public IEnumerator TheOverdriveNameAndTheStoreNameFollowEveryStateAndKeepTheAccessibleNameCurrent(
            [ValueSource(typeof(MarketPanelTestSupport), nameof(Percents))] int percent)
        {
            yield return Open(ShellScreens.All().First(), percent);
            var shell = rig.Shell;
            var states = new[]
            {
                new KeyValuePair<OverdriveStage, string>(OverdriveStage.Locked, "Locked"),
                new KeyValuePair<OverdriveStage, string>(OverdriveStage.Ready, "Ready"),
                new KeyValuePair<OverdriveStage, string>(OverdriveStage.Active, "On "),
                new KeyValuePair<OverdriveStage, string>(OverdriveStage.CoolingDown, "Cool "),
            };
            foreach (var state in states)
            {
                shell.SetOverdrive(state.Key == OverdriveStage.Locked ? null : RunShellFixtures.OverdriveStatusIn(state.Key));
                yield return ShellRig.Settle();
                var caption = shell.OverdriveButton.Q<Label>("overdrive-caption").text;
                Assert.That(caption, Does.StartWith(state.Value), state.Key.ToString());
                Assert.That(shell.OverdriveButton.Q<Label>("overdrive-name").text, Is.EqualTo("Overdrive"), state.Key + ": the name stays");
                Assert.That(IsDisplayed(shell.OverdriveButton.Q<Label>("overdrive-name")), Is.True, state.Key.ToString());
                Assert.That(shell.OverdriveButton.tooltip, Does.StartWith("Overdrive " + caption + "."), state.Key.ToString());
                Assert.That(shell.OverdriveButton.tooltip, Does.EndWith(shell.OverdriveReason), state.Key + ": the reason follows the state");
            }

            shell.SetStore(500, 500);
            yield return ShellRig.Settle();
            Assert.That(shell.StoreButton.Q<Label>("store-value").text, Is.EqualTo("FULL"));
            Assert.That(shell.StoreButton.Q<Label>("store-caption").text, Is.EqualTo("Store"), "a full store keeps its name");
            Assert.That(shell.StoreButton.tooltip, Is.EqualTo("Store full"));
        }

        [UnityTest]
        public IEnumerator TheCaptionedTopStripKeepsItsControlsOnOneRowAtTheWorstCaseOnPhones(
            [ValueSource(typeof(ShellScreens), nameof(ShellScreens.Phones))] ShellScreen screen)
        {
            yield return Open(screen, 100);
            var shell = rig.Shell;
            shell.SetCash(9_000_000_000_000_000_000L);
            shell.SetRp(9_000_000_000_000_000_000L, true);
            shell.SetStore(9_000_000_000_000_000_000L, 9_000_000_000_000_000_000L);
            shell.SetOverdrive(RunShellFixtures.OverdriveStatusIn(OverdriveStage.CoolingDown));
            yield return ShellRig.Settle();

            Assert.That(shell.TopStrip.worldBound.height, Is.LessThanOrEqualTo(56f), $"{screen}: the captions stack over the values, so the strip stays one row");
            foreach (var cell in new VisualElement[] { shell.CashButton, shell.StoreButton, shell.OverdriveButton })
            {
                AssertWithin(shell.TopStrip.worldBound, cell.worldBound, $"{screen}: {cell.name} stays in the top strip");
            }

            AssertShellLayout(rig, "worst-case captioned top strip");
        }
    }
}
#endif
