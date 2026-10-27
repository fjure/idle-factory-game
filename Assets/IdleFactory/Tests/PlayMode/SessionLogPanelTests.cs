#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.MarketPanelTestSupport;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T340 (US8, FR-028 to FR-031, D-M, D-I; QA-12 row "Session-log section"; contracts/ui-surfaces-f005.md): the fourth Market section
    /// (<c>session-log-section</c>). A fake <see cref="ISessionLogControls"/> stands in for the writer, so the tests assert what the section asks of the
    /// log and what it shows, never a file. Covers the section order and names, the toggle (on and off, with its accessible name), the export (path
    /// notice, the persistent "Last export" line that survives a refresh and a tab change, nothing to export, each failure category with a retry),
    /// the clear confirmation (cancel keeps the log, confirm clears, a failure shows its reason, a rotation cancels), the unavailable log, the focus
    /// order, that no path reaches the Unity console, the selectable line on a desktop player only, and the layout at every screen class at 100 and
    /// 200 percent text.
    /// </summary>
    public sealed class SessionLogPanelTests
    {
        private const string Directory = "/data/IdleFactory/SessionLog/Exports";
        private const string FilePath = Directory + "/session-log-20261021T123456Z.jsonl";
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private MarketPanelHost host;
        private FakeLog log;

        private sealed class FakeLog : ISessionLogControls
        {
            public bool Enabled { get; private set; } = true;
            public int SetCalls;
            public int ClearCalls;
            public int ExportCalls;
            public bool ClearResult = true;
            public SessionLogExportResult NextExport = Ok(FilePath);
            public SessionLogExportResult LastExport { get; private set; }

            public void SetEnabled(bool enabled)
            {
                SetCalls++;
                Enabled = enabled;
            }

            public bool Clear()
            {
                ClearCalls++;
                return ClearResult;
            }

            public SessionLogExportResult Export()
            {
                ExportCalls++;
                var result = NextExport;
                if (result.Status == SessionLogExportStatus.Exported)
                {
                    LastExport = result;
                }

                return result;
            }
        }

        private static SessionLogExportResult Ok(string path, int lines = 12) =>
            new SessionLogExportResult(SessionLogExportStatus.Exported, SessionLogExportFailure.None, path, Directory, lines);

        private static SessionLogExportResult Failed(SessionLogExportFailure failure) =>
            new SessionLogExportResult(SessionLogExportStatus.Failed, failure, null, Directory, 0);

        private static SessionLogExportResult Empty() =>
            new SessionLogExportResult(SessionLogExportStatus.NothingToExport, SessionLogExportFailure.None, null, Directory, 0);

        [TearDown]
        public void TearDown()
        {
            foreach (var item in rigs)
            {
                item.Dispose();
            }

            rigs.Clear();
            rig = null;
            host = null;
            log = null;
        }

        private SessionLogPanel Panel => host.SessionLog;

        private IEnumerator Open(FakeLog controls, ShellScreen screen = null, int percent = 100, IdleFactory.Unity.Presentation.ShellDeviceProfile? profile = null)
        {
            log = controls;
            var session = RunShellFixtures.NewSession(out var commands, 1, 25);
            rig = ShellRig.Create(screen ?? ShellScreens.All().First(), profile, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            host = new MarketPanelHost(session, commands, rig.Shell, Tree("InventoryRatesPanel"), Styles("InventoryRatesPanel"),
                Tree("PrioritiesPanel"), Styles("PrioritiesPanel"), Tree("ExpansionPanel"), Styles("ExpansionPanel"), Icon, controls);
            rig.Shell.Panels.Register(host);
            rig.Shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
        }

        private IEnumerator Press(VisualElement control)
        {
            ContractsPanelTestSupport.Submit(control);
            yield return ShellRig.Settle();
        }

        private static bool Shown(VisualElement element) => IsDisplayed(element);

        // ---------------------------------------------------------------------------------------------------
        // Structure and names
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheSectionIsTheFourthAfterExpansionWithTheContractNames()
        {
            yield return Open(new FakeLog());

            var order = host.Root.Children().ToList();
            Assert.That(order.Last(), Is.SameAs(Panel.Root), "the session-log section closes Market");
            Assert.That(order[order.Count - 2], Is.SameAs(host.Expansion.Root), "right after Expansion (UX-6)");
            Assert.That(Panel.Root.name, Is.EqualTo("session-log-section"));
            Assert.That(Panel.Toggle.name, Is.EqualTo("session-log-toggle"));
            Assert.That(Panel.Export.name, Is.EqualTo("session-log-export"));
            Assert.That(Panel.Clear.name, Is.EqualTo("session-log-clear"));
            Assert.That(Panel.LastExport.name, Is.EqualTo("session-log-last-export"));
            Assert.That(Panel.Root.Q<Label>("session-log-copy").text, Does.Contain("stays on this device"), "the copy states where the log stays");
            Assert.That(Panel.Id, Is.EqualTo(ShellPanelIds.Market));
        }

        [UnityTest]
        public IEnumerator TheToggleSwitchesRecordingWithItsAccessibleNameAndAnOffNote()
        {
            yield return Open(new FakeLog());
            Assert.That(Panel.Toggle.text, Is.EqualTo("Session log: On"));
            Assert.That(Panel.Toggle.tooltip, Is.EqualTo("Session log, on"));
            Assert.That(Shown(Panel.OffNoteLabel), Is.False);

            yield return Press(Panel.Toggle);

            Assert.That(log.Enabled, Is.False);
            Assert.That(log.SetCalls, Is.EqualTo(1));
            Assert.That(Panel.Toggle.text, Is.EqualTo("Session log: Off"));
            Assert.That(Panel.Toggle.tooltip, Is.EqualTo("Session log, off"));
            Assert.That(Shown(Panel.OffNoteLabel), Is.True);
            Assert.That(Panel.OffNoteLabel.text, Is.EqualTo(SessionLogPanel.OffNote));

            yield return Press(Panel.Toggle);

            Assert.That(log.Enabled, Is.True);
            Assert.That(Panel.Toggle.tooltip, Is.EqualTo("Session log, on"));
            Assert.That(Shown(Panel.OffNoteLabel), Is.False);
        }

        [UnityTest]
        public IEnumerator TheSessionLogWorksWithATouchTapAndWithAPointerClick([Values(true, false)] bool touch)
        {
            var screen = ShellScreens.All().First();
            yield return Open(new FakeLog(), screen, 100, InputModeSupport.ProfileFor(screen, touch));
            var where = touch ? "touch" : "pointer";
            InputModeSupport.AssertTargets(new VisualElement[] { Panel.Toggle, Panel.Export, Panel.Clear }, where);

            // The toggle: its state is in the text and the name, never in a hover.
            yield return InputModeSupport.Tap(Panel.Toggle, touch);
            yield return ShellRig.Settle();
            Assert.That(log.Enabled, Is.False, where + ": the press switched recording off");
            Assert.That(Panel.Toggle.text, Is.EqualTo("Session log: Off"));
            Assert.That(Panel.Toggle.tooltip, Is.EqualTo("Session log, off"));
            Assert.That(Shown(Panel.OffNoteLabel), Is.True);

        }

        [UnityTest]
        public IEnumerator TheSectionFollowsALogThatWasSwitchedElsewhereOnTheNextRefresh()
        {
            yield return Open(new FakeLog());
            log.SetEnabled(false);

            host.Refresh();
            yield return ShellRig.Settle();

            Assert.That(Panel.Toggle.text, Is.EqualTo("Session log: Off"));
            Assert.That(Shown(Panel.OffNoteLabel), Is.True);
        }

        // ---------------------------------------------------------------------------------------------------
        // Export and the last export line
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AnExportShowsANoticeAndAPersistentLastExportLineThatSurvivesARefreshAndATabChange()
        {
            yield return Open(new FakeLog());
            Assert.That(Panel.LastExport.text, Is.EqualTo("Last export: none yet"));
            Assert.That(Panel.Export.tooltip, Is.EqualTo("Export session log"));

            yield return Press(Panel.Export);

            Assert.That(log.ExportCalls, Is.EqualTo(1));
            Assert.That(rig.Shell.NoticeText, Is.EqualTo("Session log exported."));
            Assert.That(Panel.LastExport.text, Is.EqualTo("Last export: " + FilePath));
            Assert.That(Panel.Status.text, Does.Contain("12 lines").And.Contain("stays on this device"));
            Assert.That(Shown(Panel.Reason), Is.False);

            host.Refresh();
            rig.Shell.SelectTab(ShellTab.Contracts);
            yield return ShellRig.Settle();
            rig.Shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();

            Assert.That(Panel.LastExport.text, Is.EqualTo("Last export: " + FilePath), "the line is persistent, not a transient notice");
        }

        [UnityTest]
        public IEnumerator ARebuiltPanelShowsTheLastExportOfTheControlsAndNothingElse()
        {
            yield return Open(new FakeLog());
            yield return Press(Panel.Export);

            var rebuilt = new SessionLogPanel(log, rig.Shell, Styles("ExpansionPanel"), Icon);

            Assert.That(rebuilt.LastExport.text, Is.EqualTo("Last export: " + FilePath), "a recompose of the panels keeps the line (held by the controls, in memory)");
            var fresh = new SessionLogPanel(new FakeLog(), rig.Shell, Styles("ExpansionPanel"), Icon);
            Assert.That(fresh.LastExport.text, Is.EqualTo("Last export: none yet"));
        }

        [UnityTest]
        public IEnumerator TheLastExportLineIsSelectableOnADesktopPlayerOnlyAndAMobileOneShowsHowToCopyIt()
        {
            yield return Open(new FakeLog());
            yield return Press(Panel.Export);

            // The suite runs as a mobile platform by default (LifecyclePlatformTestDefault).
            Assert.That(Panel.LastExport.selection.isSelectable, Is.False);
            Assert.That(Shown(Panel.PullHintLabel), Is.True, "on a device the file is copied with adb pull");

            using (LifecyclePlatform.Override(false))
            {
                host.Refresh();
                yield return ShellRig.Settle();
                Assert.That(Panel.LastExport.selection.isSelectable, Is.True, "selectable on Windows (D-M)");
                Assert.That(Shown(Panel.PullHintLabel), Is.False);
                Assert.That(Panel.LastExport.focusable, Is.True, "reachable by keyboard so it can be selected");
            }
        }

        [UnityTest]
        public IEnumerator TheLastExportLineIsAnInertLeftAlignedCaptionNotAButtonLook()
        {
            yield return Open(new FakeLog());
            yield return Press(Panel.Export);

            var line = Panel.LastExport;
            Assert.That(line.ClassListContains("run-control"), Is.False, "it is not a control: no bordered, centred button look (T344)");
            Assert.That(line.ClassListContains(SessionLogPanel.LastExportClass), Is.True);
            Assert.That(line.ClassListContains("exp-caption"), Is.True);
            Assert.That(line.resolvedStyle.unityTextAlign, Is.EqualTo(TextAnchor.UpperLeft), "left-aligned");
            Assert.That(line.resolvedStyle.borderLeftWidth, Is.EqualTo(0f).Within(0.01f), "no button border while unfocused");
            Assert.That(line.resolvedStyle.whiteSpace, Is.EqualTo(WhiteSpace.Normal), "wrapped");
            Assert.That(line is Button, Is.False);
        }

        [UnityTest]
        public IEnumerator SwitchingTheLogOffShowsOneMessageNotTwo()
        {
            yield return Open(new FakeLog());

            yield return Press(Panel.Toggle);

            Assert.That(Shown(Panel.OffNoteLabel), Is.True, "the persistent note explains the state");
            Assert.That(Shown(Panel.Status), Is.False, "the status does not repeat it (T344)");
            Assert.That(Panel.Status.text, Is.Empty);

            yield return Press(Panel.Toggle);

            Assert.That(Shown(Panel.OffNoteLabel), Is.False);
            Assert.That(Panel.Status.text, Is.EqualTo("Session log is on."));
        }

        [UnityTest]
        public IEnumerator TheSectionHeadingCarriesAnIconLikeTheOtherMarketSections()
        {
            yield return Open(new FakeLog());

            var heading = Panel.Root.Q<Label>("session-log-heading");
            Assert.That(heading.style.backgroundImage.value.sprite, Is.Not.Null, "the heading shows an icon");
            Assert.That(heading.ClassListContains("exp-card__title--icon"), Is.True, "with room for it, as the Expansion card titles have");
            Assert.That(host.Expansion.Root.Q<Label>("expansion-storage-title").style.backgroundImage.value.sprite, Is.Not.Null, "same treatment as the siblings");
        }

        [UnityTest]
        public IEnumerator NothingToExportShowsItsReasonAndKeepsTheLastExportLine()
        {
            yield return Open(new FakeLog());
            yield return Press(Panel.Export);
            log.NextExport = Empty();

            yield return Press(Panel.Export);

            Assert.That(Shown(Panel.Reason), Is.True);
            Assert.That(Panel.Reason.text, Is.EqualTo(SessionLogPanel.NothingToExport));
            Assert.That(Panel.LastExport.text, Is.EqualTo("Last export: " + FilePath));
            Assert.That(Panel.Export.text, Is.EqualTo("Export session log"), "nothing to export is no failure, so there is no retry label");
        }

        [UnityTest]
        public IEnumerator EachFailureCategoryShowsItsReasonAndARetryThatClearsOnSuccess(
            [Values(SessionLogExportFailure.NoSpace, SessionLogExportFailure.NoPermission, SessionLogExportFailure.Unknown)] SessionLogExportFailure failure)
        {
            yield return Open(new FakeLog());
            log.NextExport = Failed(failure);
            Panel.Export.Focus();
            yield return ShellRig.Settle();

            yield return Press(Panel.Export);

            var expected = failure == SessionLogExportFailure.NoSpace ? SessionLogPanel.NoSpaceReason
                : failure == SessionLogExportFailure.NoPermission ? SessionLogPanel.NoPermissionReason
                : SessionLogPanel.UnknownFailureReason;
            Assert.That(Shown(Panel.Reason), Is.True);
            Assert.That(Panel.Reason.text, Is.EqualTo(expected));
            Assert.That(Panel.Export.text, Is.EqualTo("Retry export"));
            Assert.That(Panel.Export.tooltip, Is.EqualTo("Retry export"));
            Assert.That(Panel.LastExport.text, Is.EqualTo("Last export: none yet"), "a failed export changes nothing");
            Assert.That(ContractsPanelTestSupport.Focused(Panel.Export), Is.SameAs(Panel.Export), "focus stays on the control that was used");
            Assert.That(rig.Shell.NoticeText, Is.Empty, "a failure is shown next to its control, not as a success notice");

            log.NextExport = Ok(FilePath);
            yield return Press(Panel.Export);

            Assert.That(Shown(Panel.Reason), Is.False);
            Assert.That(Panel.Export.text, Is.EqualTo("Export session log"));
            Assert.That(Panel.LastExport.text, Is.EqualTo("Last export: " + FilePath));
            Assert.That(log.ExportCalls, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator NoPathOrFailureDetailReachesTheUnityConsole()
        {
            yield return Open(new FakeLog());
            var seen = new List<string>();
            void Capture(string condition, string stackTrace, LogType type) => seen.Add(condition);
            Application.logMessageReceived += Capture;
            try
            {
                yield return Press(Panel.Export);
                log.NextExport = Failed(SessionLogExportFailure.Unknown);
                yield return Press(Panel.Export);
                yield return Press(Panel.Toggle);
                host.Refresh();
                yield return ShellRig.Settle();
            }
            finally
            {
                Application.logMessageReceived -= Capture;
            }

            Assert.That(seen.Any(line => line.Contains("session-log-2026") || line.Contains(Directory)), Is.False, "the path is shown, never logged");
        }

        // ---------------------------------------------------------------------------------------------------
        // Clear
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ClearAsksFirstAndCancelKeepsTheLogAndReturnsFocus()
        {
            yield return Open(new FakeLog());
            Panel.Clear.Focus();
            yield return ShellRig.Settle();

            yield return Press(Panel.Clear);

            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);
            Assert.That(Panel.IsConfirmationOpen, Is.True);
            Assert.That(ContractsPanelTestSupport.DialogText(rig.Shell), Does.Contain("Clear the session log? This cannot be undone"));
            Assert.That(rig.Shell.DialogConfirmButton.text, Is.EqualTo(SessionLogPanel.ClearConfirmLabel));
            Assert.That(log.ClearCalls, Is.Zero, "nothing is cleared before the answer");
            Assert.That(ContractsPanelTestSupport.Focused(rig.Shell.DialogCancelButton), Is.SameAs(rig.Shell.DialogCancelButton), "the dialog opens on Cancel");

            yield return Press(rig.Shell.DialogCancelButton);

            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(Panel.IsConfirmationOpen, Is.False);
            Assert.That(log.ClearCalls, Is.Zero);
            Assert.That(ContractsPanelTestSupport.Focused(Panel.Clear), Is.SameAs(Panel.Clear), "focus returns to the invoker");
        }

        [UnityTest]
        public IEnumerator ConfirmingClearsTheLogOnceAndSaysSo()
        {
            yield return Open(new FakeLog());
            yield return Press(Panel.Clear);

            yield return Press(rig.Shell.DialogConfirmButton);

            Assert.That(log.ClearCalls, Is.EqualTo(1));
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(Panel.Status.text, Is.EqualTo(SessionLogPanel.ClearedStatus));
            Assert.That(Shown(Panel.Reason), Is.False);
        }

        [UnityTest]
        public IEnumerator AClearThatFailsShowsItsReasonNextToTheControl()
        {
            yield return Open(new FakeLog { ClearResult = false });
            yield return Press(Panel.Clear);

            yield return Press(rig.Shell.DialogConfirmButton);

            Assert.That(log.ClearCalls, Is.EqualTo(1));
            Assert.That(Panel.Reason.text, Is.EqualTo(SessionLogPanel.ClearFailedReason));
            Assert.That(Shown(Panel.Reason), Is.True);
            Assert.That(Panel.Status.text, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ARotationCancelsTheClearConfirmationWithoutClearingAndTheHostRoutesIt()
        {
            yield return Open(new FakeLog());
            yield return Press(Panel.Clear);
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);

            Assert.That(host.CancelForRotation(), Is.True);
            yield return ShellRig.Settle();

            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(rig.Shell.NoticeText, Is.EqualTo(RunShellDocument.RotationCancelledNotice));
            Assert.That(log.ClearCalls, Is.Zero, "a rotation clears nothing");
            Assert.That(Panel.IsConfirmationOpen, Is.False);
            Assert.That(host.CancelForRotation(), Is.False, "nothing left to cancel");
        }

        // ---------------------------------------------------------------------------------------------------
        // Unavailable log, focus
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WithoutALogTheControlsStayFocusableSoftDisabledWithTheReasonVisible()
        {
            yield return Open(null);

            foreach (var control in new VisualElement[] { Panel.Toggle, Panel.Export, Panel.Clear })
            {
                Assert.That(control.focusable, Is.True, control.name);
                Assert.That(RunShellDocument.IsSoftDisabled(control), Is.True, control.name);
            }

            Assert.That(Shown(Panel.Reason), Is.True);
            Assert.That(Panel.Reason.text, Is.EqualTo(SessionLogPanel.UnavailableReason));
            Assert.That(Panel.Export.tooltip, Does.Contain("Export session log").And.Contain(SessionLogPanel.UnavailableReason));
            Assert.That(Panel.Toggle.text, Is.EqualTo("Session log: Off"));

            Panel.Export.Focus();
            yield return Press(Panel.Export);
            yield return Press(Panel.Clear);
            yield return Press(Panel.Toggle);

            Assert.That(rig.Shell.IsConfirmationOpen, Is.False, "Clear opens no confirmation without a log");
            Assert.That(Panel.Reason.text, Is.EqualTo(SessionLogPanel.UnavailableReason));
            Assert.That(Panel.LastExport.text, Is.EqualTo("Last export: none yet"));
        }

        [UnityTest]
        public IEnumerator TheFocusOrderIsToggleThenExportThenClearAndOnDesktopTheLastExportLine()
        {
            yield return Open(new FakeLog());

            var order = rig.Shell.GetFocusOrder().ToList();
            var toggle = order.IndexOf(Panel.Toggle);
            var export = order.IndexOf(Panel.Export);
            var clear = order.IndexOf(Panel.Clear);
            Assert.That(toggle, Is.GreaterThanOrEqualTo(0));
            Assert.That(export, Is.GreaterThan(toggle));
            Assert.That(clear, Is.GreaterThan(export));
            Assert.That(order.IndexOf(Panel.LastExport), Is.EqualTo(-1), "a line that cannot be selected (mobile) is no focus stop");

            using (LifecyclePlatform.Override(false))
            {
                host.Refresh();
                yield return ShellRig.Settle();
                order = rig.Shell.GetFocusOrder().ToList();
                Assert.That(order.IndexOf(Panel.LastExport), Is.GreaterThan(order.IndexOf(Panel.Clear)), "on desktop the line is the last stop, so it can be selected by keyboard");
            }
            Assert.That(order.IndexOf(host.Expansion.StorageUpgrade), Is.LessThan(toggle), "the section comes after Expansion in the focus order");
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout at every screen class and text scale
        // ---------------------------------------------------------------------------------------------------

        private static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        private static IEnumerable<int> Percents() => new[] { 100, 200 };

        private static readonly string LongPath = "/storage/emulated/0/Android/data/com.example.idlefactory/files/IdleFactory/SessionLog/Exports/session-log-20261021T123456Z-99.jsonl";

        [UnityTest]
        public IEnumerator TheWorstCaseSectionFitsAtEveryScreenClassAndTextScale([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(new FakeLog { NextExport = Ok(LongPath, 123_456) }, screen, percent);
            yield return Press(Panel.Export);
            log.NextExport = Failed(SessionLogExportFailure.NoPermission);
            yield return Press(Panel.Export);
            Assert.That(Shown(Panel.Reason), Is.True);
            Assert.That(Panel.LastExport.text, Is.EqualTo("Last export: " + LongPath));

            yield return CheckLayout(rig, host, "session log section with a long path and a failure");

            rig.Shell.PanelScroll.ScrollTo(Panel.Toggle);
            yield return ShellRig.Settle();
            foreach (var button in new[] { Panel.Toggle, Panel.Export, Panel.Clear })
            {
                Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), screen + " @" + percent + "% " + button.name + " is a 44 pt target");
            }
        }

        [UnityTest]
        public IEnumerator TheClearConfirmationFitsAtEveryScreenClassAndTextScale([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(new FakeLog(), screen, percent);
            yield return Press(Panel.Clear);
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);

            ContractsPanelTestSupport.AssertDialogLayout(rig, "clear the session log", percent);

            yield return Press(rig.Shell.DialogCancelButton);
        }

        [UnityTest]
        public IEnumerator TheUnavailableSectionFitsAtEveryScreenClassAndTextScale([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(null, screen, percent);

            yield return CheckLayout(rig, host, "session log section without a log");
        }
    }
}
#endif
