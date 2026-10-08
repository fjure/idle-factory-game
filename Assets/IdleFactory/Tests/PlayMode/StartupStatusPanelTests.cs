#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IdleFactory.Core.Application;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.OfflineSummaryPanelTestSupport;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T118 (FR-028, FR-032, US5 scenarios 3 and 4, UX-2, UX-3, UX-6, OD-3): the start-up and import status surface in its importing, blocked-import,
    /// retry and recovery states, driven only through <c>RunPersistence</c>; the Start new run confirmation with its focus behaviour (opens on Cancel,
    /// traps focus, Escape cancels, focus returns to the invoker); the rotation that cancels a pending confirmation while V1 stays offered; and the
    /// worst-case layout at every screen class (rotated forms included) at 100 and 200 percent text scale. A blocked start has no shell, so the
    /// surface is mounted into a bare UIDocument; it is also proved under the shell, where it uses the shell's shared dialog.
    /// </summary>
    public sealed class StartupStatusPanelTests
    {
        private const string SlotsExceeded = "import.slots.exceeded";

        private readonly List<OfflineSummaryPanelTestSupport.StandaloneRig> standaloneRigs = new List<OfflineSummaryPanelTestSupport.StandaloneRig>();
        private readonly List<ShellRig> shellRigs = new List<ShellRig>();
        private StatusTestClock clock;
        private string root;
        private StatusPersistenceRig run;
        private StartupStatusPanel panel;
        private OfflineSummaryPanelTestSupport.StandaloneRig standalone;
        private ShellRig shellRig;
        private readonly List<RunCommandResult<RunPersistenceOutcome>> completed = new List<RunCommandResult<RunPersistenceOutcome>>();

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

        [SetUp]
        public void SetUp()
        {
            clock = new StatusTestClock();
            root = TempRoot();
            completed.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            panel?.Unmount();
            foreach (var item in standaloneRigs)
            {
                item.Dispose();
            }

            foreach (var item in shellRigs)
            {
                item.Dispose();
            }

            standaloneRigs.Clear();
            shellRigs.Clear();
            RunSceneRun.DeleteRoot(root);
            root = null;
            run = null;
            panel = null;
            standalone = null;
            shellRig = null;
        }

        // ---------------------------------------------------------------------------------------------------
        // Setup through the application seams
        // ---------------------------------------------------------------------------------------------------

        private StatusPersistenceRig NewRun() => run = StatusPersistenceRig.Create(root, clock);

        private IEnumerator OpenStandalone(ShellScreen screen, int percent = 100)
        {
            standalone = new OfflineSummaryPanelTestSupport.StandaloneRig(screen);
            standaloneRigs.Add(standalone);
            panel = new StartupStatusPanel(run.Persistence, run.Legacy, Styles("StartupStatusPanel.uss"));
            panel.Completed += completed.Add;
            yield return standalone.Mount(panel, percent);
        }

        private IEnumerator OpenUnderShell(ShellScreen screen, int percent = 100)
        {
            shellRig = ShellRig.Create(screen, null, percent);
            shellRigs.Add(shellRig);
            yield return shellRig.Layout();
            panel = new StartupStatusPanel(run.Persistence, run.Legacy, Styles("StartupStatusPanel.uss"));
            panel.Completed += completed.Add;
            panel.Mount(shellRig.Root, shellRig.Shell);
            yield return Frames();
        }

        private VisualElement FocusRoot => shellRig != null ? shellRig.Root : standalone.Root;

        private Rect Safe => shellRig != null ? shellRig.SafeRect() : standalone.SafeRect();

        /// <summary>A V1 save that cannot be imported: five machines exceed the four slots of factory level 1.</summary>
        private string BlockImportBySlots()
        {
            RunSceneRun.WriteV1Checkpoint(root, 5, clock.Now.AddHours(-2));
            NewRun();
            var started = run.Persistence.ColdStart(run.Legacy);
            Assert.That(started.IsSuccess, Is.False);
            Assert.That(started.ReasonCode, Is.EqualTo(SlotsExceeded));
            Assert.That(run.Persistence.BlockedImportCode, Is.EqualTo(SlotsExceeded));
            Assert.That(run.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Loading));
            return SlotsExceeded;
        }

        private static Dictionary<string, byte[]> Files(string directory) => RunSceneRun.CaptureFiles(directory);

        private static void AssertSameFiles(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after, string what)
        {
            Assert.That(after.Keys.OrderBy(key => key), Is.EqualTo(before.Keys.OrderBy(key => key)), what + ": the same files");
            foreach (var pair in before)
            {
                Assert.That(after[pair.Key], Is.EqualTo(pair.Value), what + ": " + pair.Key + " is byte-identical");
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Importing
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ImportingShowsTheMessageWithASpinner([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            NewRun();
            yield return OpenStandalone(screen);

            panel.ShowImporting();
            yield return Frames();

            Assert.That(panel.IsOpen, Is.True);
            Assert.That(panel.Kind, Is.EqualTo(StartupStatusKind.Importing));
            Assert.That(panel.TitleLabel.text, Is.EqualTo("Importing your development save…"));
            Assert.That(IsDisplayed(panel.Spinner), Is.True);
            foreach (var button in new[] { panel.RetryButton, panel.StartNewRunButton, panel.UsePreviousButton, panel.DiscardButton })
            {
                Assert.That(IsDisplayed(button), Is.False, button.name + " is not offered while importing");
            }

            var before = panel.Spinner.style.rotate.value.angle.value;
            yield return new WaitForSeconds(0.35f);
            Assert.That(panel.IsSpinning, Is.True);
            Assert.That(panel.Spinner.style.rotate.value.angle.value, Is.Not.EqualTo(before), "the spinner turns");
        }

        [UnityTest]
        public IEnumerator RefreshShowsImportingBeforeTheColdStartAndNothingOnceTheRunIsLive()
        {
            NewRun();
            yield return OpenStandalone(ShellScreens.All().First());

            Assert.That(panel.Refresh(), Is.EqualTo(StartupStatusKind.Importing), "the lifecycle has not started");
            Assert.That(panel.IsOpen, Is.True);

            run.Start();
            Assert.That(panel.Refresh(), Is.EqualTo(StartupStatusKind.Hidden));
            yield return Frames();
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(IsDisplayed(panel.Root), Is.False);
            Assert.That(panel.IsSpinning, Is.False, "the spinner stops when the surface closes");
        }

        // ---------------------------------------------------------------------------------------------------
        // Import blocked (OD-3)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ABlockedImportShowsTheFailedRuleWithRetryAndStartNewRun([Values("slots", "clock", "invalid")] string scenario)
        {
            string expectedCode;
            switch (scenario)
            {
                case "slots":
                    expectedCode = BlockImportBySlots();
                    break;
                case "clock":
                    RunSceneRun.WriteV1Checkpoint(root, 1, clock.Now.AddHours(-2));
                    clock.Fail = true;
                    NewRun();
                    run.Persistence.ColdStart(run.Legacy);
                    expectedCode = V1ImportCodes.Clock;
                    break;
                default:
                    // A published V1 checkpoint whose data is torn (the manifest stays): unreadable, never a silent fresh run.
                    RunSceneRun.WriteV1Checkpoint(root, 1, clock.Now.AddHours(-2));
                    foreach (var file in Directory.GetFiles(RunSceneRun.V1Directory(root), "*.data-*.json"))
                    {
                        File.WriteAllBytes(file, new byte[] { 8, 9 });
                    }

                    NewRun();
                    run.Persistence.ColdStart(run.Legacy);
                    expectedCode = V1ImportCodes.Invalid;
                    break;
            }

            Assert.That(run.Persistence.BlockedImportCode, Is.EqualTo(expectedCode));
            yield return OpenStandalone(ShellScreens.All().First());
            Assert.That(panel.Refresh(), Is.EqualTo(StartupStatusKind.ImportBlocked));
            yield return Frames();

            Assert.That(panel.TitleLabel.text, Is.EqualTo(StartupStatusPanel.ImportBlockedTitle));
            Assert.That(panel.BlockedCode, Is.EqualTo(expectedCode));
            Assert.That(panel.MessageLabel.text, Is.EqualTo(StartupStatusPanel.ImportCopy(expectedCode)), "the failed rule's copy");
            Assert.That(panel.HelpLabel.text, Is.EqualTo(StartupStatusPanel.ImportBlockedHelp));
            Assert.That(IsDisplayed(panel.Spinner), Is.False);
            Assert.That(IsDisplayed(panel.RetryButton), Is.True);
            Assert.That(IsDisplayed(panel.StartNewRunButton), Is.True);
            Assert.That(IsDisplayed(panel.UsePreviousButton), Is.False);
            Assert.That(IsDisplayed(panel.DiscardButton), Is.False);
            Assert.That(Focused(FocusRoot), Is.SameAs(panel.RetryButton));
        }

        [Test]
        public void EveryImportFailureCodeHasItsOwnCopy()
        {
            var copies = V1ImportCodes.All.Select(StartupStatusPanel.ImportCopy).ToList();
            Assert.That(copies, Has.None.Empty);
            Assert.That(copies.Distinct().Count(), Is.EqualTo(V1ImportCodes.All.Count), "each failed rule says its own thing");
            Assert.That(StartupStatusPanel.ImportCopy("something.else"), Is.Not.Empty, "an unknown code still has copy");
        }

        [UnityTest]
        public IEnumerator RetryOfAStillBlockedImportChangesNothingAndSaysSo()
        {
            BlockImportBySlots();
            var v1Before = Files(RunSceneRun.V1Directory(root));
            yield return OpenStandalone(ShellScreens.All().First());
            panel.Refresh();
            yield return Frames();

            Submit(panel.RetryButton);
            yield return Frames();

            Assert.That(panel.Kind, Is.EqualTo(StartupStatusKind.ImportBlocked));
            Assert.That(panel.FeedbackLabel.text, Is.EqualTo(StartupStatusPanel.StillBlockedText));
            Assert.That(IsDisplayed(panel.FeedbackLabel), Is.True);
            Assert.That(completed, Is.Empty);
            Assert.That(run.Persistence.BlockedImportCode, Is.EqualTo(SlotsExceeded));
            Assert.That(Files(RunSceneRun.V2Directory(root)), Is.Empty, "a blocked import publishes nothing");
            AssertSameFiles(v1Before, Files(RunSceneRun.V1Directory(root)), "V1");
        }

        [UnityTest]
        public IEnumerator RetryImportsOnceTheClockIsBack()
        {
            RunSceneRun.WriteV1Checkpoint(root, 1, clock.Now.AddHours(-2));
            var v1Before = Files(RunSceneRun.V1Directory(root));
            clock.Fail = true;
            NewRun();
            run.Persistence.ColdStart(run.Legacy);
            yield return OpenStandalone(ShellScreens.All().First());
            panel.Refresh();
            Assert.That(panel.BlockedCode, Is.EqualTo(V1ImportCodes.Clock));

            clock.Fail = false;
            Submit(panel.RetryButton);
            yield return Frames();

            Assert.That(completed.Count, Is.EqualTo(1), "the composition is told so it can finish the start");
            Assert.That(completed[0].Value.Imported, Is.True);
            Assert.That(run.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(panel.Kind, Is.EqualTo(StartupStatusKind.Hidden));
            Assert.That(run.Session.State.Machines.Count, Is.EqualTo(1), "the V1 machine was imported");
            Assert.That(Files(RunSceneRun.V2Directory(root)), Is.Not.Empty);
            AssertSameFiles(v1Before, Files(RunSceneRun.V1Directory(root)), "V1");
        }

        // ---------------------------------------------------------------------------------------------------
        // Start new run: the confirmation (UX-2) and its commands (OD-3, QA-8)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator StartNewRunAsksForConfirmationThatOpensOnCancelAndTrapsFocus()
        {
            BlockImportBySlots();
            yield return OpenStandalone(ShellScreens.All().First());
            panel.Refresh();
            yield return Frames();
            panel.StartNewRunButton.Focus();

            Submit(panel.StartNewRunButton);
            yield return Frames(4);

            Assert.That(panel.IsConfirmationOpen, Is.True);
            Assert.That(IsDisplayed(panel.ConfirmLayer), Is.True);
            Assert.That(panel.ConfirmTitleLabel.text, Is.EqualTo(StartupStatusPanel.StartNewRunConfirmTitle));
            Assert.That(panel.ConfirmMessageLabel.text, Is.EqualTo(StartupStatusPanel.StartNewRunConfirmMessage));
            Assert.That(panel.ConfirmMessageLabel.text, Does.Contain("no longer offered for import"));
            Assert.That(panel.ConfirmButton.text, Is.EqualTo("Start new run"));
            Assert.That(panel.ConfirmButton.ClassListContains("run-button--destructive"), Is.True, "the confirm uses the warning look");
            Assert.That(Focused(FocusRoot), Is.SameAs(panel.ConfirmCancelButton), "the dialog opens with focus on Cancel (UX-2)");
            Assert.That(run.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Loading), "nothing ran yet");

            // The trap: Tab moves between the two buttons and never leaves the dialog; the surface behind is inert.
            Key(Focused(FocusRoot), KeyCode.Tab);
            yield return Frames();
            Assert.That(Focused(FocusRoot), Is.SameAs(panel.ConfirmButton));
            Key(Focused(FocusRoot), KeyCode.Tab);
            yield return Frames();
            Assert.That(Focused(FocusRoot), Is.SameAs(panel.ConfirmCancelButton));
            Key(Focused(FocusRoot), KeyCode.Tab, EventModifiers.Shift);
            yield return Frames();
            Assert.That(Focused(FocusRoot), Is.SameAs(panel.ConfirmButton));
            Assert.That(panel.Card.enabledSelf, Is.False, "the card behind the dialog is inert");
            panel.RetryButton.Focus();
            yield return Frames(4);
            Assert.That(IsDialogControl(Focused(FocusRoot)), Is.True, "focus cannot leave the open dialog");
        }

        private bool IsDialogControl(VisualElement element) =>
            element == panel.ConfirmButton || element == panel.ConfirmCancelButton;

        [UnityTest]
        public IEnumerator EscapeAndCancelCloseTheConfirmationWithoutStartingAndReturnFocusToStartNewRun()
        {
            BlockImportBySlots();
            var v1Before = Files(RunSceneRun.V1Directory(root));
            yield return OpenStandalone(ShellScreens.All().First());
            panel.Refresh();
            yield return Frames();

            foreach (var how in new[] { "escape", "cancel" })
            {
                panel.StartNewRunButton.Focus();
                Submit(panel.StartNewRunButton);
                yield return Frames(4);
                Assert.That(panel.IsConfirmationOpen, Is.True, how);

                if (how == "escape")
                {
                    Key(Focused(FocusRoot), KeyCode.Escape);
                }
                else
                {
                    Submit(panel.ConfirmCancelButton);
                }

                yield return Frames(4);

                Assert.That(panel.IsConfirmationOpen, Is.False, how);
                Assert.That(IsDisplayed(panel.ConfirmLayer), Is.False, how);
                Assert.That(panel.Card.enabledSelf, Is.True, how + ": the card is active again");
                Assert.That(Focused(FocusRoot), Is.SameAs(panel.StartNewRunButton), how + ": focus returns to the invoking control (UX-2)");
                Assert.That(panel.Kind, Is.EqualTo(StartupStatusKind.ImportBlocked), how);
                Assert.That(run.Persistence.BlockedImportCode, Is.EqualTo(SlotsExceeded), how + ": V1 is still offered");
                Assert.That(Files(RunSceneRun.V2Directory(root)), Is.Empty, how + ": nothing was published");
                AssertSameFiles(v1Before, Files(RunSceneRun.V1Directory(root)), "V1");
            }

            Assert.That(completed, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ConfirmingStartsAFreshRunLeavesV1UntouchedAndOffersItNoMore()
        {
            BlockImportBySlots();
            var v1Before = Files(RunSceneRun.V1Directory(root));
            yield return OpenStandalone(ShellScreens.All().First());
            panel.Refresh();
            yield return Frames();
            panel.StartNewRunButton.Focus();
            Submit(panel.StartNewRunButton);
            yield return Frames(4);

            Submit(panel.ConfirmButton);
            yield return Frames();

            Assert.That(completed.Count, Is.EqualTo(1), "the composition is told so it can finish the start");
            Assert.That(completed[0].IsSuccess, Is.True);
            Assert.That(run.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(run.Persistence.BlockedImportCode, Is.Null);
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(panel.IsConfirmationOpen, Is.False);
            Assert.That(run.Session.State.Machines, Is.Empty, "a fresh run, not the V1 factory");
            Assert.That(Files(RunSceneRun.V2Directory(root)), Is.Not.Empty, "the fresh run was promoted before it was published");
            AssertSameFiles(v1Before, Files(RunSceneRun.V1Directory(root)), "V1");

            // A restart selects the V2 checkpoint, so V1 is not offered again (QA-8).
            var restarted = StatusPersistenceRig.Create(root, clock);
            var started = restarted.Persistence.ColdStart(restarted.Legacy);
            Assert.That(started.IsSuccess, Is.True, started.ReasonCode);
            Assert.That(restarted.Persistence.BlockedImportCode, Is.Null);
            Assert.That(started.Value.Imported, Is.False);
            AssertSameFiles(v1Before, Files(RunSceneRun.V1Directory(root)), "V1 after the restart");
        }

        // ---------------------------------------------------------------------------------------------------
        // Rotation cancels the pending confirmation (UX-6, UX-9) and V1 stays offered
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ARotationCancelsThePendingStartNewRunAndV1StaysOffered([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            BlockImportBySlots();
            var v1Before = Files(RunSceneRun.V1Directory(root));
            yield return OpenStandalone(screen);
            panel.Refresh();
            yield return Frames();
            panel.StartNewRunButton.Focus();
            Submit(panel.StartNewRunButton);
            yield return Frames(4);
            Assert.That(panel.IsConfirmationOpen, Is.True);

            Assert.That(panel.CancelForRotation(), Is.True);
            var rotated = ShellScreens.Rotated(screen);
            standalone.Apply(panel, rotated);
            yield return Frames(4);

            Assert.That(panel.IsConfirmationOpen, Is.False);
            Assert.That(IsDisplayed(panel.ConfirmLayer), Is.False);
            Assert.That(panel.NoticeLabel.text, Is.EqualTo(RunShellDocument.RotationCancelledNotice));
            Assert.That(IsDisplayed(panel.NoticeLabel), Is.True, "a one-line notice says what happened (UX-9)");
            Assert.That(Focused(FocusRoot), Is.SameAs(panel.StartNewRunButton), "focus returns to the invoking control after a rotation cancel");
            Assert.That(panel.Kind, Is.EqualTo(StartupStatusKind.ImportBlocked));
            Assert.That(panel.BlockedCode, Is.EqualTo(SlotsExceeded), "V1 stays offered");
            Assert.That(run.Persistence.BlockedImportCode, Is.EqualTo(SlotsExceeded));
            Assert.That(run.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Loading), "nothing was started");
            Assert.That(Files(RunSceneRun.V2Directory(root)), Is.Empty, "nothing was promoted");
            AssertSameFiles(v1Before, Files(RunSceneRun.V1Directory(root)), "V1");
            Assert.That(completed, Is.Empty);
            Assert.That(panel.CancelForRotation(), Is.False, "nothing is left to cancel");
            Assert.That(IsDisplayed(panel.StartNewRunButton), Is.True);

            // Start new run is still available and still asks again.
            Submit(panel.StartNewRunButton);
            yield return Frames(4);
            Assert.That(panel.IsConfirmationOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator UnderTheShellTheConfirmationIsTheShellDialogAndARotationCancelsIt([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            BlockImportBySlots();
            var v1Before = Files(RunSceneRun.V1Directory(root));
            yield return OpenUnderShell(screen);
            panel.Refresh();
            yield return Frames();
            Assert.That(shellRig.Root.IndexOf(panel.Root), Is.LessThan(shellRig.Root.IndexOf(shellRig.Shell.DialogLayer)), "the shell dialog is drawn above the surface");

            panel.StartNewRunButton.Focus();
            Submit(panel.StartNewRunButton);
            yield return Frames(6);

            Assert.That(shellRig.Shell.IsConfirmationOpen, Is.True);
            Assert.That(panel.IsConfirmationOpen, Is.True);
            Assert.That(IsDisplayed(panel.ConfirmLayer), Is.False, "the surface does not draw its own dialog under a shell");
            Assert.That(Focused(shellRig.Root), Is.SameAs(shellRig.Shell.DialogCancelButton), "the shell dialog opens with focus on Cancel and the surface does not take it away");
            AssertWithin(shellRig.SafeRect(), shellRig.Shell.DialogCard.worldBound, "the shell dialog stays in the safe area");

            // Escape is Cancel; focus returns to Start new run.
            shellRig.Shell.HandleBack();
            yield return Frames(4);
            Assert.That(shellRig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(panel.IsConfirmationOpen, Is.False);
            Assert.That(Focused(shellRig.Root), Is.SameAs(panel.StartNewRunButton));
            Assert.That(run.Persistence.BlockedImportCode, Is.EqualTo(SlotsExceeded));

            // A rotation cancels it, with the shell's notice, and V1 stays offered.
            Submit(panel.StartNewRunButton);
            yield return Frames(4);
            Assert.That(shellRig.Shell.IsConfirmationOpen, Is.True);
            Assert.That(panel.CancelForRotation(), Is.True);
            yield return shellRig.Layout(ShellScreens.Rotated(screen));
            Assert.That(shellRig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(shellRig.Shell.NoticeText, Is.EqualTo(RunShellDocument.RotationCancelledNotice));
            Assert.That(Focused(shellRig.Root), Is.SameAs(panel.StartNewRunButton));
            Assert.That(run.Persistence.BlockedImportCode, Is.EqualTo(SlotsExceeded));
            Assert.That(Files(RunSceneRun.V2Directory(root)), Is.Empty);
            AssertSameFiles(v1Before, Files(RunSceneRun.V1Directory(root)), "V1");

            // Confirming in the shell dialog starts the fresh run.
            Submit(panel.StartNewRunButton);
            yield return Frames(4);
            Submit(shellRig.Shell.DialogConfirmButton);
            yield return Frames();
            Assert.That(completed.Count, Is.EqualTo(1));
            Assert.That(run.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            AssertSameFiles(v1Before, Files(RunSceneRun.V1Directory(root)), "V1 after Start new run");
        }

        // ---------------------------------------------------------------------------------------------------
        // Retry of a transient failure, and the Feature 001 recovery of a corrupt V2 checkpoint
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ATransientStorageFailureOffersRetryOnlyAndRetryStartsTheRun()
        {
            NewRun();
            run.Store.FailReads = true;
            var blocked = run.Persistence.ColdStart(run.Legacy);
            Assert.That(blocked.IsSuccess, Is.False);
            Assert.That(run.Persistence.ColdStartRetryRequired, Is.True);
            yield return OpenStandalone(ShellScreens.All().First());
            Assert.That(panel.Refresh(), Is.EqualTo(StartupStatusKind.StartRetry));
            yield return Frames();

            Assert.That(panel.TitleLabel.text, Is.EqualTo(StartupStatusPanel.StartRetryTitle));
            Assert.That(panel.MessageLabel.text, Is.EqualTo(StartupStatusPanel.StartRetryText));
            Assert.That(IsDisplayed(panel.RetryButton), Is.True);
            Assert.That(IsDisplayed(panel.StartNewRunButton), Is.False, "nothing was decided, so there is no import to give up");
            Assert.That(IsDisplayed(panel.DiscardButton), Is.False);

            Submit(panel.RetryButton);
            yield return Frames();
            Assert.That(panel.Kind, Is.EqualTo(StartupStatusKind.StartRetry));
            Assert.That(IsDisplayed(panel.FeedbackLabel), Is.True);

            run.Store.FailReads = false;
            Submit(panel.RetryButton);
            yield return Frames();
            Assert.That(completed.Count, Is.EqualTo(1));
            Assert.That(run.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(panel.IsOpen, Is.False);
        }

        /// <summary>One saved generation holding a Miner whose data file is torn and whose manifests are gone: the damaged sole save.</summary>
        private void TearTheOnlySave()
        {
            var first = NewRun().Start();
            first.BuildMiner();
            Assert.That(first.Persistence.SaveNow().IsSuccess, Is.True);
            var data = Directory.GetFiles(RunSceneRun.V2Directory(root), "*.data-*.json").Single();
            File.WriteAllBytes(data, new byte[] { 0x7B, 0x01, 0x02 });
            foreach (var manifest in Directory.GetFiles(RunSceneRun.V2Directory(root), "*.manifest-*.bin"))
            {
                File.Delete(manifest);
            }
        }

        [UnityTest]
        public IEnumerator ADamagedSoleSaveOffersRetryPreviousSaveAndAConfirmedDiscard()
        {
            TearTheOnlySave();
            var v2Before = Files(RunSceneRun.V2Directory(root));
            NewRun();
            var blocked = run.Persistence.ColdStart(run.Legacy);
            Assert.That(blocked.IsSuccess, Is.False);
            Assert.That(run.Persistence.Lifecycle, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(run.Persistence.BlockedImportCode, Is.Null, "a corrupt V2 never takes the import path (SEC-1)");
            yield return OpenStandalone(ShellScreens.All().First());
            Assert.That(panel.Refresh(), Is.EqualTo(StartupStatusKind.ReturnBlocked));
            yield return Frames();

            Assert.That(panel.TitleLabel.text, Is.EqualTo(StartupStatusPanel.ReturnBlockedTitle));
            Assert.That(panel.MessageLabel.text, Is.EqualTo(run.Persistence.Status.Message), "the application's own static copy");
            Assert.That(IsDisplayed(panel.RetryButton), Is.True);
            Assert.That(IsDisplayed(panel.UsePreviousButton), Is.True);
            Assert.That(IsDisplayed(panel.DiscardButton), Is.True, "the damaged sole save may be discarded");
            Assert.That(IsDisplayed(panel.StartNewRunButton), Is.False, "Start new run is for a blocked import");

            // Retry and Use previous change nothing: there is no valid generation.
            Submit(panel.RetryButton);
            yield return Frames();
            Assert.That(panel.Kind, Is.EqualTo(StartupStatusKind.ReturnBlocked));
            Assert.That(IsDisplayed(panel.FeedbackLabel), Is.True);
            Submit(panel.UsePreviousButton);
            yield return Frames();
            Assert.That(panel.FeedbackLabel.text, Does.StartWith("There is no previous save").Or.StartWith("The previous save could not be used"));
            Assert.That(completed, Is.Empty);
            AssertSameFiles(v2Before, Files(RunSceneRun.V2Directory(root)), "V2 stays as found until the player decides");

            // The discard asks first, in words that say the damaged save is discarded; Cancel changes nothing.
            panel.DiscardButton.Focus();
            Submit(panel.DiscardButton);
            yield return Frames(4);
            Assert.That(panel.IsConfirmationOpen, Is.True);
            Assert.That(panel.ConfirmTitleLabel.text, Is.EqualTo(StartupStatusPanel.DiscardConfirmTitle));
            Assert.That(panel.ConfirmMessageLabel.text, Does.Contain("damaged saved factory is discarded"));
            Assert.That(Focused(FocusRoot), Is.SameAs(panel.ConfirmCancelButton));
            Key(Focused(FocusRoot), KeyCode.Escape);
            yield return Frames(4);
            Assert.That(Focused(FocusRoot), Is.SameAs(panel.DiscardButton));
            AssertSameFiles(v2Before, Files(RunSceneRun.V2Directory(root)), "V2 after Cancel");

            Submit(panel.DiscardButton);
            yield return Frames(4);
            Submit(panel.ConfirmButton);
            yield return Frames();
            Assert.That(completed.Count, Is.EqualTo(1));
            Assert.That(run.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(run.Session.State.Machines, Is.Empty, "a fresh run replaced the damaged save");
            Assert.That(panel.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator ATornActiveGenerationWithAValidPreviousOneIsNotRecoverableByTheApplicationAndNothingIsDiscarded()
        {
            var first = NewRun().Start();
            first.BuildMiner();
            Assert.That(first.Persistence.SaveNow().IsSuccess, Is.True);
            Assert.That(first.Persistence.SaveNow().IsSuccess, Is.True);

            // Tear the data file of the newest generation; its manifest and the previous generation stay intact.
            var files = Directory.GetFiles(RunSceneRun.V2Directory(root), "*.data-*.json");
            Assert.That(files.Length, Is.EqualTo(2));
            var newest = files.OrderByDescending(file => long.Parse(Regex.Match(File.ReadAllText(file), @"""generation""\s*:\s*(\d+)").Groups[1].Value)).First();
            File.WriteAllBytes(newest, new byte[] { 0x7B, 0x01, 0x02 });
            var v2Before = Files(RunSceneRun.V2Directory(root));

            NewRun();
            var blocked = run.Persistence.ColdStart(run.Legacy);
            Assert.That(blocked.IsSuccess, Is.False);
            Assert.That(run.Persistence.Lifecycle, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(run.Persistence.CanDiscardDamagedSave, Is.False, "a valid previous generation exists, so nothing may be discarded (SEC-1)");
            yield return OpenStandalone(ShellScreens.All().First());
            panel.Refresh();
            yield return Frames();
            Assert.That(IsDisplayed(panel.DiscardButton), Is.False, "the surface never offers what the application refuses");
            Assert.That(IsDisplayed(panel.RetryButton), Is.True);
            Assert.That(IsDisplayed(panel.UsePreviousButton), Is.True);

            // RecoverPreviousCheckpoint needs a readable active generation, so it is rejected; the surface says so and changes nothing.
            Submit(panel.UsePreviousButton);
            yield return Frames();
            Assert.That(panel.FeedbackLabel.text, Does.StartWith("The previous save could not be used"));
            Assert.That(completed, Is.Empty);
            Assert.That(run.Persistence.Lifecycle, Is.EqualTo(LifecycleState.ReturnBlocked));
            AssertSameFiles(v2Before, Files(RunSceneRun.V2Directory(root)), "V2 stays as found");
        }

        [UnityTest]
        public IEnumerator UsePreviousRestoresTheGenerationBeforeWhenTheActiveOneIsReadable()
        {
            var live = NewRun().Start();
            Assert.That(live.Persistence.SaveNow().IsSuccess, Is.True);
            live.BuildMiner();
            Assert.That(live.Persistence.SaveNow().IsSuccess, Is.True);
            live.Away(TimeSpan.FromHours(1), clockFailsOnReturn: true);
            Assert.That(live.Persistence.Lifecycle, Is.EqualTo(LifecycleState.ReturnBlocked));
            yield return OpenStandalone(ShellScreens.All().First());
            Assert.That(panel.Refresh(), Is.EqualTo(StartupStatusKind.ReturnBlocked));
            yield return Frames();

            clock.Fail = false;
            Submit(panel.UsePreviousButton);
            yield return Frames();

            Assert.That(completed.Count, Is.EqualTo(1), "the composition is told so it can finish the start");
            Assert.That(run.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(run.Persistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.Recovered));
            Assert.That(panel.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator ADiscardIsOfferedOnlyWhenTheApplicationAllowsIt()
        {
            // A return blocked after the run was live: the in-memory run is newer than any durable state, so no discard.
            var live = NewRun().Start();
            live.BuildMiner();
            live.Away(TimeSpan.FromHours(1), clockFailsOnReturn: true);
            Assert.That(live.Persistence.Lifecycle, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(live.Persistence.CanDiscardDamagedSave, Is.False);
            yield return OpenStandalone(ShellScreens.All().First());
            Assert.That(panel.Refresh(), Is.EqualTo(StartupStatusKind.ReturnBlocked));
            yield return Frames();

            Assert.That(IsDisplayed(panel.DiscardButton), Is.False);
            Assert.That(IsDisplayed(panel.RetryButton), Is.True);
            Assert.That(panel.HelpLabel.text, Does.Contain("never overwritten"));

            clock.Fail = false;
            Submit(panel.RetryButton);
            yield return Frames();
            Assert.That(completed.Count, Is.EqualTo(1));
            Assert.That(run.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout (UX-3, F-4): worst-case content at every screen class (rotated forms included) at 100 and 200 percent
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheImportingStateStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            NewRun();
            yield return OpenStandalone(screen, percent);
            panel.ShowImporting();
            yield return Frames();

            yield return AssertImportingLayout(screen, percent);
        }

        private IEnumerator AssertImportingLayout(ShellScreen screen, int percent)
        {
            var context = $"{screen} @{percent}% [importing]";
            AssertWithin(Safe, panel.Card.worldBound, context + ": the card stays inside the safe area");
            AssertTextAndTargets(panel.Root, Safe, context, MinimumFontSize * percent / 100f);
            AssertWithin(panel.Card.worldBound, panel.Spinner.worldBound, context + ": the spinner stays in the card");
            Assert.That(Intersects(panel.Spinner.worldBound, panel.TitleLabel.worldBound), Is.False, context + ": the spinner overlaps the title");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheLongestBlockedImportStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            // The longest copy (the clock) plus the feedback of a failed retry.
            RunSceneRun.WriteV1Checkpoint(root, 1, clock.Now.AddHours(-2));
            clock.Fail = true;
            NewRun();
            run.Persistence.ColdStart(run.Legacy);
            yield return OpenStandalone(screen, percent);
            panel.Refresh();
            Submit(panel.RetryButton);
            yield return Frames();
            Assert.That(IsDisplayed(panel.FeedbackLabel), Is.True);

            yield return AssertSurfaceLayout(panel, Safe, screen, "blocked import", percent);
        }

        [UnityTest]
        public IEnumerator TheRecoveryStateWithEveryActionStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            TearTheOnlySave();
            NewRun();
            run.Persistence.ColdStart(run.Legacy);
            yield return OpenStandalone(screen, percent);
            panel.Refresh();
            Submit(panel.UsePreviousButton);
            yield return Frames();
            Assert.That(IsDisplayed(panel.FeedbackLabel), Is.True);
            Assert.That(IsDisplayed(panel.DiscardButton), Is.True);

            yield return AssertSurfaceLayout(panel, Safe, screen, "recovery with four actions", percent);
        }

        [UnityTest]
        public IEnumerator TheStartNewRunConfirmationStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            BlockImportBySlots();
            yield return OpenStandalone(screen, percent);
            panel.Refresh();
            yield return Frames();
            panel.StartNewRunButton.Focus();
            Submit(panel.StartNewRunButton);
            yield return Frames(4);
            Assert.That(panel.IsConfirmationOpen, Is.True);

            var context = $"{screen} @{percent}% [start new run confirmation]";
            var card = panel.ConfirmCard.worldBound;
            AssertWithin(Safe, card, context + ": the dialog card stays inside the safe area");
            AssertWithin(card, panel.ConfirmCancelButton.worldBound, context + ": Cancel stays in the card");
            AssertWithin(card, panel.ConfirmButton.worldBound, context + ": Confirm stays in the card");
            Assert.That(Intersects(panel.ConfirmCancelButton.worldBound, panel.ConfirmButton.worldBound), Is.False, context + ": the buttons overlap");
            AssertTextAndTargets(panel.ConfirmLayer, Safe, context, MinimumFontSize * percent / 100f);
            Assert.That(Focused(FocusRoot), Is.SameAs(panel.ConfirmCancelButton), context + ": opens on Cancel");

            // The message scrolls to its end inside the card.
            var scroll = panel.ConfirmLayer.Q<ScrollView>("startup-confirm-scroll");
            var viewport = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport");
            var range = Mathf.Max(0f, scroll.contentContainer.layout.height - viewport.layout.height);
            scroll.scrollOffset = new Vector2(0f, range);
            yield return Frames();
            AssertWithin(viewport.worldBound, panel.ConfirmMessageLabel.worldBound, context + ": the end of the message is reachable");
            AssertScrollContentClipped(scroll, context);

            // A rotation cancels it from here, at every screen class.
            Assert.That(panel.CancelForRotation(), Is.True);
            yield return Frames(4);
            Assert.That(Focused(FocusRoot), Is.SameAs(panel.StartNewRunButton), context + ": focus returns after a rotation cancel");
        }
    }
}
#endif
