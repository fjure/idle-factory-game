#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T117 (FR-020 to FR-022, FR-024, FR-032, FR-033, US4 scenarios 1 to 3, UX-2, UX-3): the Prestige surface below the threshold and
    /// eligible, Cancel, "Saving", the durable-transition failure with Retry (reached through a checkpoint store that fails its
    /// promotion), the cancel on rotation, the confirmation's focus behaviour and the worst-case layout at every screen class and at
    /// 100 and 200 percent text scale.
    /// </summary>
    public sealed class PrestigePanelTests
    {
        private const string PanelRoot = "Assets/IdleFactory/Unity/UI/Panels/";
        private const long Threshold = 10_000_000;
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private readonly List<string> tempRoots = new List<string>();
        private ShellRig rig;
        private RunSession session;
        private RunCommands commands;
        private PrestigePresenter panel;
        private FlakyStore store;
        private RunPersistence persistence;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<int> Percents() => new[] { 100, 200 };

        public static IEnumerable<ShellScreen> Representative()
        {
            var all = ShellScreens.All().ToList();
            yield return all.First(screen => screen.Name.StartsWith("phone-portrait 360", StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("phone-landscape 640", StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("tablet-portrait", StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("desktop 1280", StringComparison.Ordinal));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in rigs)
            {
                item.Dispose();
            }

            rigs.Clear();
            foreach (var path in tempRoots)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                }
                catch (IOException)
                {
                }
            }

            tempRoots.Clear();
            rig = null;
            panel = null;
            store = null;
            persistence = null;
        }

        /// <summary>Opens the shell with the Prestige tab, a session with this much Cash and, optionally, a durable persistence over a failing store.</summary>
        private IEnumerator Open(ShellScreen screen, long cash, int percent = 100, bool durable = false, bool worstCase = false)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            session = RunShellFixtures.NewSession(out commands, factoryLevel: 1, cash: cash, unlockEverything: true);
            if (worstCase)
            {
                Assert.That(session.State.TryUnlock("contract.plate"), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok));
                Assert.That(commands.AcceptContract("contract.plate").IsSuccess, Is.True);
                Assert.That(session.Permanent.TryAddItems("item.flux-catalyst", 999_999), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok));
            }

            if (durable)
            {
                var root = Path.Combine(Application.temporaryCachePath, "prestige-panel-" + Guid.NewGuid().ToString("N"));
                tempRoots.Add(root);
                store = new FlakyStore(new JsonRunCheckpointStore(root, session.Catalog));
                persistence = new RunPersistence(session, store, new SessionClock(session));
                var resumed = persistence.ResumeAndReconcile();
                Assert.That(resumed.IsSuccess, Is.True, resumed.ReasonCode);
            }

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelRoot + "PrestigePanel.uxml");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelRoot + "PrestigePanel.uss");
            Assert.That(tree, Is.Not.Null);
            Assert.That(styles, Is.Not.Null);
            panel = new PrestigePresenter(session, commands, tree, styles, ContractsPanelTestSupport.Icon);
            rig.Shell.Panels.Register(panel);
            rig.Shell.SelectTab(ShellTab.Prestige);
            yield return ShellRig.Settle();
        }

        private static bool Shown(VisualElement element) => IsDisplayed(element);

        private static void Submit(VisualElement target)
        {
            using (var evt = NavigationSubmitEvent.GetPooled())
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        private static VisualElement Focused(VisualElement any) => any.focusController?.focusedElement as VisualElement;

        private IEnumerator WaitWhileSaving()
        {
            for (var i = 0; i < 300 && panel.State == PrestigePanelState.Saving; i++)
            {
                yield return null;
            }

            Assert.That(panel.State, Is.Not.EqualTo(PrestigePanelState.Saving), "the durable write finished");
            yield return ShellRig.Settle();
        }

        private static string DialogText(RunShellDocument shell) =>
            string.Join(" | ", shell.DialogCard.Query<Label>().ToList().Select(label => label.text));

        // ---------------------------------------------------------------------------------------------------
        // Below the threshold (US4 scenario 3, FR-021)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator BelowTheThresholdTheMissingValueShowsAndConfirmIsDisabled([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen, 25);
            Assert.That(rig.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Prestige));
            Assert.That(panel.Root.parent, Is.SameAs(rig.Shell.PanelContent));
            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.BelowThreshold));

            var preview = commands.GetPrestigePreview().Value;
            Assert.That(preview.IsEligible, Is.False);
            Assert.That(preview.MissingFactoryValue, Is.GreaterThan(0));
            Assert.That(Shown(panel.MissingLabel), Is.True);
            Assert.That(panel.MissingLabel.text, Is.EqualTo("Missing Factory Value: " + QuantityFormatter.Format(preview.MissingFactoryValue)));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.ConfirmButton), Is.True);
            Assert.That(Shown(panel.RetryButton), Is.False);
            Assert.That(Shown(panel.SavingCard), Is.False);
            Assert.That(Shown(panel.FailureCard), Is.False);

            // The breakdown and the preview of the IP are shown below the threshold too.
            Assert.That(panel.ValueCashLabel.text, Is.EqualTo("Cash: 25"));
            Assert.That(panel.ValueStockLabel.text, Does.StartWith("Stock value: "));
            Assert.That(panel.ValueMachinesLabel.text, Does.StartWith("Machines: "));
            Assert.That(panel.ValueExpansionLabel.text, Does.StartWith("Expansion: "));
            Assert.That(panel.ValueTotalLabel.text, Does.Contain(QuantityFormatter.Format(preview.FactoryValue.Total)));
            Assert.That(panel.IpAwardLabel.text, Does.Contain("+" + QuantityFormatter.Format(preview.IpAward) + " IP"));
            Assert.That(panel.ResetRows.Count, Is.EqualTo(PrestigeTransition.ResetList.Count));
            Assert.That(panel.PreserveRows.Count, Is.EqualTo(PrestigeTransition.PreserveList.Count));
            Assert.That(panel.ResetRows.Select(row => row.text), Has.None.Empty);
            Assert.That(panel.PreserveRows.Select(row => row.text), Has.None.Empty);
        }

        [UnityTest]
        public IEnumerator ActivatingADisabledConfirmShowsTheReasonAndOpensNothing()
        {
            yield return Open(ShellScreens.All().First(), 25);
            var runId = session.State.RunId;
            Submit(panel.ConfirmButton);
            yield return ShellRig.Settle();

            Assert.That(rig.Shell.IsConfirmationOpen, Is.False, "no dialog below the threshold");
            Assert.That(session.PendingTransition, Is.Null);
            Assert.That(session.State.RunId, Is.EqualTo(runId));
            Assert.That(Shown(panel.FeedbackLabel), Is.True);
            Assert.That(panel.FeedbackLabel.text, Is.EqualTo(RunRejectionCopy.For(RunReasons.PrestigeBelowThreshold)));
            Assert.That(Focused(panel.ConfirmButton), Is.Null.Or.SameAs(panel.ConfirmButton));
        }

        // ---------------------------------------------------------------------------------------------------
        // Eligible (US4 scenarios 1 and 2, FR-020 to FR-022)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EligibleShowsTheBreakdownTheIpPreviewAndBothListsWithConfirmEnabled([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen, 12_345_678);
            var preview = commands.GetPrestigePreview().Value;
            Assert.That(preview.IsEligible, Is.True);
            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.Ready));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.ConfirmButton), Is.False);
            Assert.That(Shown(panel.MissingLabel), Is.False);
            Assert.That(panel.StatusLabel.text, Is.EqualTo("Ready to sell"));
            Assert.That(panel.ValueCashLabel.text, Is.EqualTo("Cash: " + QuantityFormatter.Format(12_345_678)));
            Assert.That(panel.ThresholdLabel.text, Is.EqualTo(PrestigePresenter.ThresholdText(preview, session.Permanent.PrestigeCount)));
            Assert.That(panel.ThresholdLabel.text, Does.StartWith("Needed to sell: " + QuantityFormatter.Format(Threshold)));
            Assert.That(preview.Threshold, Is.EqualTo(Threshold));
            Assert.That(panel.IpAwardLabel.text, Is.EqualTo("Sale award: +" + QuantityFormatter.Format(preview.IpAward) + " IP"));
            Assert.That(preview.IpAward, Is.GreaterThan(0));
            Assert.That(panel.IpAfterLabel.text, Does.Contain(QuantityFormatter.Format(preview.IpAfter) + " IP after the sale"));

            Assert.That(panel.ResetRows.Select(row => row.text), Does.Contain("Cash").And.Contain("Machines and their levels").And.Contain("Unlocks (they start again)"));
            Assert.That(panel.PreserveRows.Select(row => row.text), Has.Some.StartsWith("Influence Points").And.Some.StartsWith("Industry upgrades").And.Some.StartsWith("Items"));
            Assert.That(Shown(panel.CancelButton), Is.True);
            Assert.That(Shown(panel.RetryButton), Is.False);
        }

        [UnityTest]
        public IEnumerator ConfirmingSellsTheFactoryThroughSavingAndStartsTheNextRun()
        {
            yield return Open(ShellScreens.All().First(), 12_345_678);
            var oldRun = session.State.RunId;
            var award = commands.GetPrestigePreview().Value.IpAward;

            Submit(panel.ConfirmButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);
            Assert.That(session.PendingTransition, Is.Null, "opening the dialog confirms nothing");
            Submit(rig.Shell.DialogConfirmButton);

            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.Saving), "Saving shows while the transition is durable");
            Assert.That(panel.SavingLabel.text, Is.EqualTo(PrestigePresenter.SavingText));
            Assert.That(panel.SavingLabel.text, Does.StartWith("Saving"));
            Assert.That(panel.SavingCard.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(panel.StatusLabel.text, Is.EqualTo(PrestigePresenter.SavingText));
            Assert.That(session.PendingTransition, Is.Not.Null);
            Assert.That(RunShellDocument.IsSoftDisabled(panel.ConfirmButton), Is.True, "a second confirmation is not possible while saving");

            yield return WaitWhileSaving();
            Assert.That(session.PendingTransition, Is.Null);
            Assert.That(session.State.RunNumber, Is.EqualTo(2));
            Assert.That(session.State.PreviousRunId, Is.EqualTo(oldRun));
            Assert.That(session.Permanent.Ip, Is.EqualTo(award));
            Assert.That(session.Permanent.PrestigeCount, Is.EqualTo(1));
            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.BelowThreshold), "the new run starts below the threshold");
            Assert.That(rig.Shell.NoticeText, Does.Contain("Factory Sale complete"));
            Assert.That(Shown(panel.SavingCard), Is.False);
        }

        [UnityTest]
        public IEnumerator DecliningTheConfirmationPrestigesNothing()
        {
            yield return Open(ShellScreens.All().First(), 12_345_678);
            var oldRun = session.State.RunId;
            Submit(panel.ConfirmButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);
            Assert.That(panel.IsConfirmationOpen, Is.True);

            Submit(rig.Shell.DialogCancelButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(panel.IsConfirmationOpen, Is.False);
            Assert.That(session.PendingTransition, Is.Null);
            Assert.That(session.State.RunId, Is.EqualTo(oldRun));
            Assert.That(session.Permanent.Ip, Is.EqualTo(0));
            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.Ready));
        }

        // ---------------------------------------------------------------------------------------------------
        // Focus (UX-2) and rotation (FR-033, UX-9)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheConfirmationOpensOnCancelTrapsFocusAndReturnsFocusToConfirm([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen, 12_345_678);
            var shell = rig.Shell;
            panel.ConfirmButton.Focus();
            yield return ShellRig.Settle();
            Submit(panel.ConfirmButton);
            yield return ShellRig.Settle();

            Assert.That(shell.IsConfirmationOpen, Is.True);
            Assert.That(DialogText(shell), Does.Contain(PrestigePresenter.ConfirmTitle).And.Contain("Influence Points"));
            Assert.That(Focused(shell.Root), Is.SameAs(shell.DialogCancelButton), "the dialog opens with focus on Cancel");
            Assert.That(shell.DialogConfirmButton.ClassListContains("run-button--destructive"), Is.True);

            // The trap: focus sent to the panel's control goes back into the dialog.
            panel.CancelButton.Focus();
            yield return ShellRig.Settle();
            Assert.That(shell.DialogCard.Contains(Focused(shell.Root)), Is.True, "focus stays inside the dialog");

            // Android Back is Cancel and never Confirm; focus returns to the invoking Confirm button.
            Assert.That(shell.HandleBack(), Is.True);
            yield return ShellRig.Settle();
            Assert.That(shell.IsConfirmationOpen, Is.False);
            Assert.That(session.PendingTransition, Is.Null);
            Assert.That(Focused(shell.Root), Is.SameAs(panel.ConfirmButton), "focus returns to the invoking control");
        }

        [UnityTest]
        public IEnumerator EscapeCancelsTheConfirmation()
        {
            yield return Open(ShellScreens.All().First(), 12_345_678);
            Submit(panel.ConfirmButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);
            var focused = Focused(rig.Shell.Root);
            using (var evt = KeyDownEvent.GetPooled('\u001b', KeyCode.Escape, EventModifiers.None))
            {
                evt.target = focused;
                focused.SendEvent(evt);
            }

            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(session.PendingTransition, Is.Null);
            Assert.That(session.Permanent.PrestigeCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ARotationCancelsTheConfirmationWithoutPrestigingAndNotesIt([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen, 12_345_678);
            var oldRun = session.State.RunId;
            Submit(panel.ConfirmButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);

            Assert.That(panel.CancelForRotation(), Is.True);
            yield return rig.Layout(ShellScreens.Rotated(screen));
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(panel.IsConfirmationOpen, Is.False);
            Assert.That(session.PendingTransition, Is.Null, "a rotation never confirms the prestige");
            Assert.That(session.State.RunId, Is.EqualTo(oldRun));
            Assert.That(session.State.RunNumber, Is.EqualTo(1));
            Assert.That(session.Permanent.Ip, Is.EqualTo(0));
            Assert.That(rig.Shell.NoticeText, Does.Contain("Cancelled by rotation"));
            Assert.That(Focused(rig.Shell.Root), Is.SameAs(panel.ConfirmButton), "focus returns to the invoking control");
            Assert.That(panel.CancelForRotation(), Is.False, "nothing is open any more");
            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.Ready));
        }

        // ---------------------------------------------------------------------------------------------------
        // Failure and retry (FR-024, US4 scenario 2): the durable promotion fails through the store, not through a panel switch
        // ---------------------------------------------------------------------------------------------------

        private IEnumerator ConfirmAndSave()
        {
            Submit(panel.ConfirmButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);
            Submit(rig.Shell.DialogConfirmButton);
            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.Saving));
            yield return WaitWhileSaving();
        }

        [UnityTest]
        public IEnumerator AFailedDurableTransitionShowsTheMessageKeepsTheRunAndRetryCompletesIt([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen, 12_345_678, durable: true);
            var oldRun = session.State.RunId;
            var award = commands.GetPrestigePreview().Value.IpAward;
            store.FailPromote = true;

            yield return ConfirmAndSave();
            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.Failed));
            Assert.That(panel.FailureCode, Is.Not.Null);
            Assert.That(panel.FailureLabel.text, Is.EqualTo(PrestigePresenter.FailureText));
            Assert.That(panel.FailureLabel.text, Does.StartWith("Factory Sale was not completed").And.EndWith("your run is unchanged"));
            Assert.That(panel.StatusLabel.text, Is.EqualTo(panel.FailureLabel.text));
            Assert.That(Shown(panel.FailureCard), Is.True);
            Assert.That(Shown(panel.RetryButton), Is.True);
            Assert.That(Shown(panel.ConfirmButton), Is.False, "Retry replaces Confirm");
            Assert.That(Shown(panel.SavingCard), Is.False);
            Assert.That(session.State.RunId, Is.EqualTo(oldRun), "the old run is unchanged");
            Assert.That(session.State.RunNumber, Is.EqualTo(1));
            Assert.That(session.Permanent.Ip, Is.EqualTo(0), "no award in memory");
            Assert.That(session.PendingTransition, Is.Not.Null, "the transition stays retryable");
            Assert.That(Focused(rig.Shell.Root), Is.SameAs(panel.RetryButton), "focus moves to Retry");

            // Retry while the store still fails: the same failure state again, nothing awarded.
            Submit(panel.RetryButton);
            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.Saving));
            yield return WaitWhileSaving();
            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.Failed));
            Assert.That(session.Permanent.Ip, Is.EqualTo(0));

            // The store recovers: Retry completes the transition once.
            store.FailPromote = false;
            Submit(panel.RetryButton);
            yield return WaitWhileSaving();
            Assert.That(session.PendingTransition, Is.Null);
            Assert.That(session.State.RunNumber, Is.EqualTo(2));
            Assert.That(session.State.PreviousRunId, Is.EqualTo(oldRun));
            Assert.That(session.Permanent.Ip, Is.EqualTo(award), "exactly one award");
            Assert.That(session.Permanent.PrestigeCount, Is.EqualTo(1));
            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.BelowThreshold));
            Assert.That(Shown(panel.FailureCard), Is.False);
            Assert.That(Shown(panel.RetryButton), Is.False);
            Assert.That(rig.Shell.NoticeText, Does.Contain("Factory Sale complete"));
        }

        [UnityTest]
        public IEnumerator CancellingAfterAFailureKeepsTheOldRunAndLeavesTheSale()
        {
            yield return Open(ShellScreens.All().First(), 12_345_678, durable: true);
            var oldRun = session.State.RunId;
            store.FailPromote = true;
            yield return ConfirmAndSave();
            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.Failed));

            Submit(panel.CancelButton);
            yield return ShellRig.Settle();
            Assert.That(session.PendingTransition, Is.Null);
            Assert.That(session.State.RunId, Is.EqualTo(oldRun));
            Assert.That(session.Permanent.Ip, Is.EqualTo(0));
            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.Ready));
            Assert.That(Shown(panel.FailureCard), Is.False);
            Assert.That(Shown(panel.ConfirmButton), Is.True);
            Assert.That(session.Advance(1).IsSuccess, Is.True, "commands resume after the cancel");
        }

        // ---------------------------------------------------------------------------------------------------
        // Worst-case layout (UX-3, F-4)
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// The shell layout, the clipped scroll content at the top, the middle and the bottom, and a focused first, middle and last
        /// control scrolled into view inside the sheet and the safe area. At 150 percent and more the content must scroll.
        /// </summary>
        private IEnumerator AssertPanelLayout(string state, int percent)
        {
            var shell = rig.Shell;
            var screen = rig.Screen;
            yield return ShellRig.Settle();
            AssertShellLayout(rig, state);

            var scroll = shell.PanelScroll;
            var viewport = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport");
            var range = Mathf.Max(0f, scroll.contentContainer.layout.height - viewport.layout.height);
            if (percent >= 150)
            {
                Assert.That(range, Is.GreaterThan(0f), $"{screen} @{percent}% [{state}]: the worst-case content scrolls");
            }

            foreach (var offset in new[] { 0f, 0.5f, 1f })
            {
                scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, range * offset);
                yield return ShellRig.Settle();
                AssertScrollContentClipped(scroll, $"{screen} @{percent}% [{state}] scrolled to {offset:P0}");
            }

            var buttons = panel.Root.Query<Button>().ToList().Where(button => IsDisplayed(button)).ToList();
            Assert.That(buttons.Count, Is.GreaterThan(0), $"{screen} @{percent}% [{state}]: the panel has controls");
            var safe = rig.SafeRect();
            foreach (var button in new[] { buttons.Last(), buttons.First(), buttons[buttons.Count / 2] })
            {
                scroll.scrollOffset = new Vector2(0f, button == buttons.Last() ? 0f : float.MaxValue);
                yield return ShellRig.Settle();
                button.Focus();
                yield return ShellRig.Settle();
                Assert.That(Focused(button), Is.SameAs(button), $"{screen} @{percent}% [{state}]: '{button.name}' did not take focus");
                AssertWithin(viewport.worldBound, button.worldBound, $"{screen} @{percent}% [{state}]: focused '{button.name}' is not scrolled into view");
                AssertWithin(safe, button.worldBound, $"{screen} @{percent}% [{state}]: focused '{button.name}' leaves the safe area");
                Assert.That(button.ClassListContains("run-control--focused"), Is.True, $"{screen} @{percent}% [{state}]: '{button.name}' shows its focus ring");
            }

            AssertShellLayout(rig, state + " after focus changes");
        }

        [UnityTest]
        public IEnumerator TheEligibleWorstCaseContentFitsEveryScreenClassAtEveryTextScale([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, 900_000_000_000_000_000L, percent, worstCase: true);
            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.Ready));
            Assert.That(panel.ResetRows.Select(row => row.text), Has.Some.Contain("no reward, no penalty"));
            yield return AssertPanelLayout("prestige eligible worst case", percent);

            // The confirmation dialog stays inside the safe area with its two buttons apart.
            var shell = rig.Shell;
            panel.ConfirmButton.Focus();
            Submit(panel.ConfirmButton);
            yield return ShellRig.Settle();
            Assert.That(shell.IsConfirmationOpen, Is.True);
            var safe = rig.SafeRect();
            var card = shell.DialogCard.worldBound;
            AssertWithin(safe, card, $"{screen} @{percent}%: the dialog card stays inside the safe area");
            AssertWithin(card, shell.DialogCancelButton.worldBound, $"{screen} @{percent}%: Cancel stays in the card");
            AssertWithin(card, shell.DialogConfirmButton.worldBound, $"{screen} @{percent}%: Confirm stays in the card");
            AssertTextAndTargets(shell.DialogLayer, safe, $"{screen} @{percent}% dialog", MinimumFontSize * percent / 100f);
            Submit(shell.DialogCancelButton);
        }

        [UnityTest]
        public IEnumerator TheBelowThresholdContentFitsEveryScreenClassAtEveryTextScale([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, 25, percent);
            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.BelowThreshold));
            yield return AssertPanelLayout("prestige below threshold", percent);
        }

        [UnityTest]
        public IEnumerator TheMissingValueShowsTheLockAndARejectionTheWarningBesideTheirTextAtEveryScreenClass(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, 25, percent);
            var where = $"{screen} @{percent}%";
            ContractsPanelTestSupport.AssertIconBesideText(panel.MissingLabel, PanelIcons.Lock, where + " missing value");

            Submit(panel.ConfirmButton);
            yield return ShellRig.Settle();
            ContractsPanelTestSupport.AssertIconBesideText(panel.FeedbackLabel, PanelIcons.Warning, where + " rejection");
            ContractsPanelTestSupport.AssertTextButtonsHaveNoChildren(panel.Root, where);
            AssertShellLayout(rig, "prestige below threshold with icons");
        }

        [UnityTest]
        public IEnumerator TheFailedSaleShowsTheWarningBesideItsTextAtEveryTextScale([ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(ShellScreens.All().First(), 900_000_000_000_000_000L, percent, durable: true, worstCase: true);
            store.FailPromote = true;
            yield return ConfirmAndSave();
            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.Failed));
            ContractsPanelTestSupport.AssertIconBesideText(panel.FailureLabel, PanelIcons.Warning, $"@{percent}% failure");
            ContractsPanelTestSupport.AssertTextButtonsHaveNoChildren(panel.Root, $"@{percent}%");
        }

        [UnityTest]
        public IEnumerator TheFailureStateFitsEveryScreenClassAtEveryTextScale([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, 900_000_000_000_000_000L, percent, durable: true, worstCase: true);
            store.FailPromote = true;
            yield return ConfirmAndSave();
            Assert.That(panel.State, Is.EqualTo(PrestigePanelState.Failed));
            yield return AssertPanelLayout("prestige failure", percent);
        }

        // ---------------------------------------------------------------------------------------------------
        // Test doubles
        // ---------------------------------------------------------------------------------------------------

        /// <summary>The run's own timeline as the wall clock, so a departure equals the timeline instant.</summary>
        private sealed class SessionClock : IUtcClock
        {
            private readonly RunSession owner;

            public SessionClock(RunSession owner) => this.owner = owner;

            public DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(owner.TimelineUtcMs);
        }

        /// <summary>The real Save V2 store over a temporary folder whose promotion can be made to fail (the failed durable transition).</summary>
        private sealed class FlakyStore : IRunCheckpointStore
        {
            private readonly IRunCheckpointStore inner;

            public FlakyStore(IRunCheckpointStore inner) => this.inner = inner;

            public bool FailPromote { get; set; }

            public RunCheckpointReadResult ReadActive() => inner.ReadActive();

            public RunCheckpointReadResult ReadPrevious() => inner.ReadPrevious();

            public RunCheckpointReadResult Read(CheckpointId checkpointId) => inner.Read(checkpointId);

            public CommandResult<CheckpointId> Promote(RunCheckpoint checkpoint) =>
                FailPromote
                    ? CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "The test store refuses to promote.")
                    : inner.Promote(checkpoint);

            public CommandResult<CheckpointId> RecoverPrevious(RunCheckpoint recoveryCandidate) => inner.RecoverPrevious(recoveryCandidate);

            public CommandResult<long> NextSupersedeGeneration() => inner.NextSupersedeGeneration();

            public CommandResult<CheckpointId> SupersedeDamaged(RunCheckpoint checkpoint) => inner.SupersedeDamaged(checkpoint);

            public CommandResult<RunCheckpointPresence> ProbeFiles() => inner.ProbeFiles();
        }
    }
}
#endif
