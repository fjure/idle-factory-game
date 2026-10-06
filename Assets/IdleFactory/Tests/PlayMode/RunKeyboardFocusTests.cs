using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T112 (UX-2, UX-8, UX-9): keyboard operation of the run shell. The shell traversal reaches every control, the left and
    /// right arrows move along the tab bar, Enter and Space activate, the shared confirmation dialog traps focus, opens on Cancel,
    /// returns focus to the invoking control and treats Escape and Android Back as Cancel, a rotation cancel returns focus too,
    /// the focus ring is visible, every target is at least 44 x 44 points, and a compact number shows its exact value on focus,
    /// tap and long press.
    /// </summary>
    public sealed class RunKeyboardFocusTests
    {
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;

        public static IEnumerable<ShellScreen> Screens()
        {
            yield return ShellScreens.All().First(screen => screen.Name.StartsWith("phone-portrait 360", StringComparison.Ordinal));
            yield return ShellScreens.All().First(screen => screen.Name.StartsWith("phone-landscape 640", StringComparison.Ordinal));
            yield return ShellScreens.All().First(screen => screen.Name.StartsWith("tablet-portrait", StringComparison.Ordinal));
            yield return ShellScreens.All().First(screen => screen.Name.StartsWith("desktop 1280", StringComparison.Ordinal));
        }

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

        private IEnumerator Open(ShellScreen screen)
        {
            rig = ShellRig.Create(screen);
            rigs.Add(rig);
            yield return rig.Layout();
        }

        private static void Send(VisualElement target, EventBase evt)
        {
            using (evt)
            {
                // Pointer events need an explicit target; keyboard and navigation events are retargeted to the focused element.
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        private static void Key(VisualElement target, KeyCode key, bool shift = false)
        {
            var character = key == KeyCode.Tab ? '\t' : key == KeyCode.Escape ? '\u001b' : '\0';
            Send(target, KeyDownEvent.GetPooled(character, key, shift ? EventModifiers.Shift : EventModifiers.None));
        }

        private static void Submit(VisualElement target) => Send(target, NavigationSubmitEvent.GetPooled());

        private static VisualElement Focused(VisualElement any) => any.focusController?.focusedElement as VisualElement;

        private ConfirmationRequest OpenDialog(VisualElement invoker, List<string> log, bool destructive = true)
        {
            var request = new ConfirmationRequest
            {
                Title = "Remove the machine?",
                Message = "You get half of the Cash back.",
                ConfirmLabel = "Remove",
                Destructive = destructive,
                Invoker = invoker,
                OnConfirm = () => log.Add("confirm"),
                OnCancel = reason => log.Add("cancel:" + reason),
            };
            Assert.That(rig.Shell.RequestConfirmation(request), Is.True);
            return request;
        }

        // ---------------------------------------------------------------------------------------------------
        // Shell traversal
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryShellControlIsReachableByFocusAndTheTraversalOrderCoversThem([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            shell.SetRp(10, true);
            shell.Panels.Register(new TestShellPanel(ShellPanelIds.Market, "Market", 3, false));
            shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();

            var order = shell.GetFocusOrder().ToList();
            var names = order.Select(element => element.name).ToList();
            foreach (var expected in new[] { "cash", "rp", "store", "overdrive", "zoom-in", "zoom-out", "tab-factory", "tab-market", "tab-contracts", "tab-items", "tab-research", "tab-prestige", "test-panel-button-0", "test-panel-button-2" })
            {
                Assert.That(names, Does.Contain(expected), $"{screen}: '{expected}' is in the traversal order");
            }

            if (!shell.IsSidePanelLayout)
            {
                Assert.That(names, Does.Contain("panel-close"), $"{screen}: the sheet can be closed from the keyboard");
            }

            foreach (var element in order)
            {
                element.Focus();
                yield return null;
                Assert.That(Focused(element), Is.SameAs(element), $"{screen}: '{element.name}' did not take focus");
            }

            // Tab (the Next navigation of the input system) walks the same controls.
            order[0].Focus();
            yield return null;
            var visited = new HashSet<VisualElement> { order[0] };
            for (var i = 0; i < order.Count + 2; i++)
            {
                Send(Focused(order[0]) ?? order[0], NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next));
                yield return null;
                var now = Focused(order[0]);
                if (now != null)
                {
                    visited.Add(now);
                }
            }

            var missed = order.Where(element => !visited.Contains(element)).Select(element => element.name).ToList();
            Assert.That(missed, Is.Empty, $"{screen}: Tab never reached {string.Join(", ", missed)}");
        }

        [UnityTest]
        public IEnumerator EnterAndSpaceActivateATabAndTheSelectedTabIsMarkedBeyondColor([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            var market = shell.TabButton(ShellTab.Market);
            market.Focus();
            yield return null;

            // Enter and Space reach a Button as the input system's Submit navigation event.
            Submit(market);
            yield return ShellRig.Settle();
            Assert.That(shell.ActiveTab, Is.EqualTo(ShellTab.Market));
            Assert.That(shell.IsSheetOpen, Is.True);
            Assert.That(market.ClassListContains("run-tab--selected"), Is.True, "the selected tab is marked");
            Assert.That(market.resolvedStyle.borderTopWidth, Is.GreaterThanOrEqualTo(3f), "the selected tab has an accent bar, not color alone");
            Assert.That(market.Q<Label>().resolvedStyle.unityFontStyleAndWeight, Is.EqualTo(FontStyle.Bold), "and a bold label");
            Assert.That(shell.TabButton(ShellTab.Factory).ClassListContains("run-tab--selected"), Is.False);

            if (!shell.IsSidePanelLayout)
            {
                Submit(market);
                yield return ShellRig.Settle();
                Assert.That(shell.IsSheetOpen, Is.False, "activating the open tab again closes its sheet on a phone");
                Assert.That(shell.ActiveTab, Is.EqualTo(ShellTab.Factory));
            }

            shell.TabButton(ShellTab.Prestige).Focus();
            yield return null;
            Submit(shell.TabButton(ShellTab.Prestige));
            yield return ShellRig.Settle();
            Assert.That(shell.ActiveTab, Is.EqualTo(ShellTab.Prestige));
        }

        [UnityTest]
        public IEnumerator LeftAndRightArrowsMoveAlongTheTabBar([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            var tabs = Enum.GetValues(typeof(ShellTab)).Cast<ShellTab>().Select(shell.TabButton).ToArray();
            tabs[0].Focus();
            yield return null;

            for (var i = 1; i < tabs.Length; i++)
            {
                Key(Focused(tabs[0]), KeyCode.RightArrow);
                yield return null;
                Assert.That(Focused(tabs[0]), Is.SameAs(tabs[i]), $"{screen}: Right moves from tab {i} to tab {i + 1}");
            }

            Key(Focused(tabs[0]), KeyCode.RightArrow);
            yield return null;
            Assert.That(Focused(tabs[0]), Is.SameAs(tabs[5]), "the last tab stops the arrow");

            for (var i = tabs.Length - 2; i >= 0; i--)
            {
                Key(Focused(tabs[0]), KeyCode.LeftArrow);
                yield return null;
                Assert.That(Focused(tabs[0]), Is.SameAs(tabs[i]), $"{screen}: Left moves back to tab {i + 1}");
            }

            Key(Focused(tabs[0]), KeyCode.LeftArrow);
            yield return null;
            Assert.That(Focused(tabs[0]), Is.SameAs(tabs[0]), "the first tab stops the arrow");

            // The navigation events of the input system move the same way.
            Send(tabs[0], NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Right));
            yield return null;
            Assert.That(Focused(tabs[0]), Is.SameAs(tabs[1]));
            Send(tabs[1], NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Left));
            yield return null;
            Assert.That(Focused(tabs[0]), Is.SameAs(tabs[0]));
        }

        // ---------------------------------------------------------------------------------------------------
        // Shared confirmation dialog
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheDialogOpensOnCancelTrapsFocusAndWrapsWithTab([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            var invoker = shell.TabButton(ShellTab.Prestige);
            invoker.Focus();
            var log = new List<string>();
            OpenDialog(invoker, log);
            yield return ShellRig.Settle();

            Assert.That(shell.IsConfirmationOpen, Is.True);
            Assert.That(Focused(invoker), Is.SameAs(shell.DialogCancelButton), $"{screen}: initial focus is on Cancel");
            Assert.That(shell.GetFocusOrder().Select(element => element.name), Is.EqualTo(new[] { "dialog-cancel", "dialog-confirm" }), "only the dialog is in the traversal order");

            Key(shell.DialogCancelButton, KeyCode.Tab);
            yield return null;
            Assert.That(Focused(invoker), Is.SameAs(shell.DialogConfirmButton), "Tab moves to Confirm");
            Key(shell.DialogConfirmButton, KeyCode.Tab);
            yield return null;
            Assert.That(Focused(invoker), Is.SameAs(shell.DialogCancelButton), "Tab wraps from the last control to the first");
            Key(shell.DialogCancelButton, KeyCode.Tab, shift: true);
            yield return null;
            Assert.That(Focused(invoker), Is.SameAs(shell.DialogConfirmButton), "Shift+Tab wraps backwards");

            Send(shell.DialogConfirmButton, NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next));
            yield return null;
            Assert.That(Focused(invoker), Is.SameAs(shell.DialogCancelButton), "the Next navigation event is trapped too");

            // Focus moved outside the dialog by any means returns to it.
            shell.TabButton(ShellTab.Market).Focus();
            yield return ShellRig.Settle();
            Assert.That(shell.GetFocusOrder().Contains(Focused(invoker)), Is.True, "focus never rests outside the open dialog");
            Assert.That(Focused(invoker), Is.SameAs(shell.DialogCancelButton), "and returns to Cancel");
            Assert.That(log, Is.Empty, "nothing was confirmed or cancelled by moving focus");
        }

        [UnityTest]
        public IEnumerator EscapeAndAndroidBackAreCancelAndFocusReturnsToTheInvokingControl([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            shell.Panels.Register(new TestShellPanel(ShellPanelIds.Prestige, "Prestige", 3, false));
            shell.SelectTab(ShellTab.Prestige);
            yield return ShellRig.Settle();
            var invoker = rig.Root.Q<Button>("test-panel-button-1");
            invoker.Focus();
            yield return null;

            var log = new List<string>();
            OpenDialog(invoker, log);
            yield return ShellRig.Settle();
            Key(Focused(invoker), KeyCode.Escape);
            yield return ShellRig.Settle();
            Assert.That(shell.IsConfirmationOpen, Is.False, "Escape closes the dialog");
            Assert.That(log, Is.EqualTo(new[] { "cancel:Back" }), "Escape is Cancel and never Confirm");
            Assert.That(Focused(invoker), Is.SameAs(invoker), $"{screen}: focus returns to the invoking control");

            // Android Back arrives as Escape in the input system; HandleBack is the shell's single entry for it.
            log.Clear();
            OpenDialog(invoker, log);
            yield return ShellRig.Settle();
            Assert.That(shell.HandleBack(), Is.True);
            yield return ShellRig.Settle();
            Assert.That(shell.IsConfirmationOpen, Is.False);
            Assert.That(log, Is.EqualTo(new[] { "cancel:Back" }));
            Assert.That(Focused(invoker), Is.SameAs(invoker), "focus returns after Android Back");

            // With no dialog open, Back closes the sheet, and focus lands on the Factory tab instead of a hidden control.
            Assert.That(shell.HandleBack(), Is.True);
            yield return ShellRig.Settle();
            Assert.That(shell.ActiveTab, Is.EqualTo(ShellTab.Factory));
            Assert.That(shell.IsSheetOpen, Is.False);
            Assert.That(Focused(invoker), Is.SameAs(shell.TabButton(ShellTab.Factory)), "focus is not left on a closed sheet");
            yield return null;
            Assert.That(shell.HandleBack(), Is.False, "with nothing open, Back is left to the platform");
        }

        [UnityTest]
        public IEnumerator ConfirmRunsTheActionOnceAndReturnsFocus([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            var invoker = shell.TabButton(ShellTab.Items);
            invoker.Focus();
            var log = new List<string>();
            OpenDialog(invoker, log, destructive: false);
            yield return ShellRig.Settle();
            Assert.That(shell.DialogConfirmButton.ClassListContains("run-button--primary"), Is.True);
            Assert.That(shell.DialogConfirmButton.ClassListContains("run-button--destructive"), Is.False);

            shell.DialogConfirmButton.Focus();
            yield return null;
            Submit(shell.DialogConfirmButton);
            yield return ShellRig.Settle();
            Assert.That(log, Is.EqualTo(new[] { "confirm" }), "Confirm acts exactly once");
            Assert.That(shell.IsConfirmationOpen, Is.False);
            Assert.That(Focused(invoker), Is.SameAs(invoker), "focus returns to the invoking control after Confirm");

            Submit(shell.DialogConfirmButton);
            yield return null;
            Assert.That(log, Is.EqualTo(new[] { "confirm" }), "a closed dialog cannot confirm again");
        }

        [UnityTest]
        public IEnumerator ADestructiveDialogUsesTheDangerFillAndTheWarningIcon([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            OpenDialog(shell.TabButton(ShellTab.Prestige), new List<string>(), destructive: true);
            yield return ShellRig.Settle();

            Assert.That(shell.DialogConfirmButton.ClassListContains("run-button--destructive"), Is.True);
            var fill = shell.DialogConfirmButton.resolvedStyle.backgroundColor;
            Assert.That(ColorUtility.ToHtmlStringRGB(fill), Is.EqualTo("F87171"), "Art Bible 9.2: destructive confirm uses --danger");
            Assert.That(ColorUtility.ToHtmlStringRGB(shell.DialogConfirmButton.resolvedStyle.color), Is.EqualTo("0B1020"), "with the --on-accent label");
            var icon = shell.Root.Q<VisualElement>("dialog-icon");
            Assert.That(icon.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(icon.resolvedStyle.backgroundImage.sprite != null || icon.resolvedStyle.backgroundImage.texture != null, Is.True, "the warning icon accompanies the colour");
        }

        [UnityTest]
        public IEnumerator ARotationCancelClosesTheDialogWithTheNoticeAndReturnsFocus([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            shell.Panels.Register(new TestShellPanel(ShellPanelIds.Contracts, "Contracts", 3, false));
            shell.SelectTab(ShellTab.Contracts);
            yield return ShellRig.Settle();
            var invoker = rig.Root.Q<Button>("test-panel-button-0");
            invoker.Focus();
            yield return null;
            var log = new List<string>();
            OpenDialog(invoker, log);
            yield return ShellRig.Settle();

            Assert.That(shell.CancelConfirmationForRotation(), Is.True);
            yield return ShellRig.Settle();
            Assert.That(log, Is.EqualTo(new[] { "cancel:Rotation" }), "a rotation cancels and never confirms");
            Assert.That(shell.NoticeText, Is.EqualTo("Cancelled by rotation"));
            Assert.That(Focused(invoker), Is.SameAs(invoker), $"{screen}: focus returns to the invoking control after a rotation cancel");
            Assert.That(shell.CancelConfirmationForRotation(), Is.False, "nothing is left to cancel");
        }

        [UnityTest]
        public IEnumerator ASecondConfirmationReplacesTheFirstWithoutConfirmingIt()
        {
            yield return Open(ShellScreens.All().First());
            var shell = rig.Shell;
            var log = new List<string>();
            OpenDialog(shell.TabButton(ShellTab.Items), log);
            shell.RequestConfirmation(new ConfirmationRequest { Title = "Second", Message = "second", Invoker = shell.TabButton(ShellTab.Research), OnCancel = reason => log.Add("second:" + reason) });
            yield return ShellRig.Settle();

            Assert.That(log, Is.EqualTo(new[] { "cancel:Replaced" }), "the first request ends as Replaced, never as Confirmed");
            Assert.That(shell.IsConfirmationOpen, Is.True);
            Assert.That(Focused(shell.Root), Is.SameAs(shell.DialogCancelButton));
        }

        // ---------------------------------------------------------------------------------------------------
        // Visible focus and targets
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryFocusedControlShowsTheTwoPartFocusRingAndEveryTargetIsAtLeast44Points([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            shell.SetRp(10, true);
            shell.Panels.Register(new TestShellPanel(ShellPanelIds.Research, "Research", 3, false));
            shell.SelectTab(ShellTab.Research);
            yield return ShellRig.Settle();

            foreach (var control in shell.GetFocusOrder().OfType<Button>())
            {
                control.Focus();
                yield return null;
                yield return null;
                Assert.That(Focused(control), Is.SameAs(control), $"{screen}: {control.name}");
                Assert.That(control.ClassListContains("run-control--focused"), Is.True, $"{screen}: {control.name} is marked focused");
                var ring = shell.FocusRing;
                var inner = shell.Root.Q<VisualElement>("focus-ring-inner");
                Assert.That(shell.FocusRingTarget, Is.SameAs(control), $"{screen}: the ring surrounds {control.name}");
                Assert.That(ring.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), $"{screen}: {control.name} ring is drawn");
                Assert.That(ColorUtility.ToHtmlStringRGB(ring.resolvedStyle.borderTopColor), Is.EqualTo("F8FAFC"), $"{screen}: {control.name} has the outer --text-strong ring");
                Assert.That(ring.resolvedStyle.borderTopWidth, Is.EqualTo(2f), $"{screen}: {control.name} outer ring is 2 pt");
                Assert.That(ColorUtility.ToHtmlStringRGB(inner.resolvedStyle.borderTopColor), Is.EqualTo("0B1020"), $"{screen}: {control.name} inner ring is --on-accent");
                Assert.That(inner.resolvedStyle.borderTopWidth, Is.EqualTo(2f), $"{screen}: {control.name} inner ring is 2 pt");
                var around = control.worldBound;
                AssertWithin(ring.worldBound, around, $"{screen}: the ring encloses {control.name}");
                Assert.That(ring.worldBound.xMin, Is.EqualTo(around.xMin - 4f).Within(1f), $"{screen}: {control.name} ring starts 4 pt outside the edge (2 pt inner + 2 pt outer)");
                Assert.That(ring.worldBound.yMax, Is.EqualTo(around.yMax + 4f).Within(1f), $"{screen}: {control.name} ring ends 4 pt outside the edge");
                Assert.That(control.worldBound.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{screen}: {control.name} width");
                Assert.That(control.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{screen}: {control.name} height");
            }

            // A control that loses focus loses its ring.
            var first = shell.CashButton;
            first.Focus();
            yield return null;
            shell.StoreButton.Focus();
            yield return null;
            Assert.That(first.ClassListContains("run-control--focused"), Is.False);
            shell.TabButton(ShellTab.Market).Blur();
            shell.StoreButton.Blur();
            yield return null;
            Assert.That(shell.FocusRing.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "the ring is hidden when nothing is focused");
        }

        [UnityTest]
        public IEnumerator ADisabledControlStaysFocusableAndKeepsItsReasonVisible([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            shell.SetOverdrive(RunShellFixtures.OverdriveStatusIn(IdleFactory.Run.Application.OverdriveStage.CoolingDown));
            yield return ShellRig.Settle();

            var overdrive = shell.OverdriveButton;
            Assert.That(RunShellDocument.IsSoftDisabled(overdrive), Is.True);
            Assert.That(overdrive.Q<Label>().text, Does.StartWith("Cool"), "the caption states why: cooling down, with the time left");
            Assert.That(shell.OverdriveReason, Does.Contain("cooling down"));
            overdrive.Focus();
            yield return null;
            Assert.That(Focused(overdrive), Is.SameAs(overdrive), "a disabled control stays focusable so its reason can be read");

            var requested = 0;
            shell.OverdriveRequested += () => requested++;
            Submit(overdrive);
            yield return null;
            Assert.That(requested, Is.EqualTo(0), "activation of a disabled control is rejected");
            Assert.That(shell.NoticeText, Does.Contain("cooling down"), "with the reason next to the control that was used");

            shell.SetOverdrive(RunShellFixtures.OverdriveStatusIn(IdleFactory.Run.Application.OverdriveStage.Ready));
            Assert.That(RunShellDocument.IsSoftDisabled(overdrive), Is.False);
            Submit(overdrive);
            yield return null;
            Assert.That(requested, Is.EqualTo(1), "a ready control activates once");
        }

        // ---------------------------------------------------------------------------------------------------
        // Exact-value detail line (UX-8)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ACompactNumberShowsItsExactValueOnFocusTapAndLongPress([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            shell.SetCash(1_234_567_890L);
            yield return ShellRig.Settle();
            Assert.That(shell.CashButton.Q<Label>("cash-value").text, Is.EqualTo("1.23B"), "compact display is floored (R06)");
            Assert.That(shell.DetailText, Is.Empty);

            // Focus shows the exact value and blur hides it.
            shell.CashButton.Focus();
            yield return null;
            Assert.That(shell.DetailText, Is.EqualTo("Cash: 1,234,567,890"));
            shell.StoreButton.Focus();
            yield return null;
            Assert.That(shell.DetailText, Does.StartWith("Store:"), "the detail line follows focus");
            shell.RpButton.Focus();
            shell.CashButton.Focus();
            yield return null;
            shell.TabButton(ShellTab.Market).Focus();
            yield return null;
            Assert.That(shell.DetailText, Is.Empty, "blur hides the exact value when nothing is pinned");

            // A tap (touch) pins the line open, a second tap closes it, and the line follows value changes.
            Send(shell.CashButton, ClickEvent.GetPooled());
            yield return null;
            Assert.That(shell.IsDetailPinned, Is.True);
            Assert.That(shell.DetailText, Is.EqualTo("Cash: 1,234,567,890"));
            shell.SetCash(2_000_000_000L);
            Assert.That(shell.DetailText, Is.EqualTo("Cash: 2,000,000,000"), "the pinned line updates with the number");
            shell.TabButton(ShellTab.Market).Focus();
            yield return null;
            Assert.That(shell.IsDetailPinned, Is.True, "focus leaving does not close a pinned line");
            Send(shell.CashButton, ClickEvent.GetPooled());
            yield return null;
            Assert.That(shell.IsDetailPinned, Is.False);
            Assert.That(shell.DetailText, Is.Empty);

            // Escape (and Back) unpin before they close anything else.
            Send(shell.CashButton, ClickEvent.GetPooled());
            yield return null;
            Assert.That(shell.IsDetailPinned, Is.True);
            Assert.That(shell.HandleBack(), Is.True);
            Assert.That(shell.IsDetailPinned, Is.False);
            yield return null;

            // A 500 ms press (long press) shows it too, on this element that is neither draggable nor part of the world.
            Send(shell.CashButton, PointerDownEvent.GetPooled());
            yield return null;
            Assert.That(shell.IsDetailPinned, Is.False, "not before 500 ms");
            var waited = 0f;
            while (waited < 0.8f && !shell.IsDetailPinned)
            {
                waited += Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.02f;
                yield return new WaitForSecondsRealtime(0.05f);
            }

            Assert.That(shell.IsDetailPinned, Is.True, "a long press shows the exact value");
            Assert.That(shell.DetailText, Is.EqualTo("Cash: 2,000,000,000"));
        }

        [UnityTest]
        public IEnumerator AShortPressDoesNotLongPressAndTheStoreShowsItsExactUsage()
        {
            yield return Open(ShellScreens.All().First());
            var shell = rig.Shell;
            shell.SetStore(1_234_567, 5_000_000);
            Assert.That(shell.StoreButton.Q<Label>("store-value").text, Is.EqualTo("24%"));
            Send(shell.CashButton, PointerDownEvent.GetPooled());
            Send(shell.CashButton, PointerUpEvent.GetPooled());
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.That(shell.IsDetailPinned, Is.False, "releasing before 500 ms cancels the long press");

            shell.StoreButton.Focus();
            yield return null;
            Assert.That(shell.DetailText, Is.EqualTo("Store: 1,234,567 of 5,000,000 units (24%)"));
            shell.SetStore(5_000_000, 5_000_000);
            Assert.That(shell.StoreButton.Q<Label>("store-value").text, Is.EqualTo("FULL"), "a full store says so in text, not only in color");
            Assert.That(shell.StoreButton.ClassListContains("run-stat--full"), Is.True);
        }
    }
}
