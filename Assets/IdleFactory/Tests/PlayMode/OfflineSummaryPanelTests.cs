#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Bootstrap;
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
    /// T118 (FR-025, FR-028, FR-032, US5 scenarios 1, 3 and 4, UX-3): the offline summary in its normal, short-absence, backward-clock, forfeited
    /// and blocked states, Continue and Retry through the application seams, focus behaviour, a rotation that is not a departure, and the
    /// worst-case layout at every screen class (rotations included) and at 100 and 200 percent text scale, under the shell and outside it.
    /// </summary>
    public sealed class OfflineSummaryPanelTests
    {
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private readonly List<OfflineSummaryPanelTestSupport.StandaloneRig> standaloneRigs = new List<OfflineSummaryPanelTestSupport.StandaloneRig>();
        private readonly List<GameObject> objects = new List<GameObject>();
        private ShellRig rig;
        private OfflineSummaryPanel panel;
        private RunCatalog catalog;
        private string root;

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
            panel?.Unmount();
            foreach (var item in objects)
            {
                if (item != null)
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }

            foreach (var item in rigs)
            {
                item.Dispose();
            }

            foreach (var item in standaloneRigs)
            {
                item.Dispose();
            }

            objects.Clear();
            rigs.Clear();
            standaloneRigs.Clear();
            RunSceneRun.DeleteRoot(root);
            root = null;
            rig = null;
            panel = null;
        }

        private IEnumerator OpenShell(ShellScreen screen, int percent = 100, Func<RunCommandResult<RunPersistenceOutcome>> retry = null, RunCatalog withCatalog = null)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            catalog = withCatalog ?? RunShellFixtures.NewSession(out _, unlockEverything: true).Catalog;
            panel = new OfflineSummaryPanel(catalog, retry, Styles("OfflineSummaryPanel.uss"));
            panel.Mount(rig.Root, rig.Shell);
            yield return Frames();
        }

        private StatusPersistenceRig NewPersistence(StatusTestClock clock)
        {
            root = TempRoot();
            return StatusPersistenceRig.Create(root, clock);
        }

        private string Label(string resourceId) => catalog.Resources[resourceId].Label;

        // ---------------------------------------------------------------------------------------------------
        // Normal
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheSummaryListsDurationsCashStockSalesEffectsContractAndLimits([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return OpenShell(screen);
            var button = rig.Shell.CashButton;
            button.Focus();
            panel.ShowSummary(NormalReturn());
            yield return Frames();

            Assert.That(panel.IsOpen, Is.True);
            Assert.That(panel.Kind, Is.EqualTo(OfflineSurfaceKind.Summary));
            Assert.That(panel.TitleLabel.text, Is.EqualTo("Welcome back"));
            Assert.That(panel.Line("offline-measured").text, Is.EqualTo("Away for 9 h 30 min."));
            Assert.That(panel.Line("offline-credited").text, Is.EqualTo("Credited: 8 h."));
            Assert.That(panel.Line("offline-effective").text, Is.EqualTo("Production ran for 6 h 24 min at 80 % efficiency."));
            Assert.That(panel.Line("offline-cash").text, Is.EqualTo("Cash +1,280 (from 100 to 1,380)."));
            Assert.That(panel.Line("offline-sales").text, Is.EqualTo("Sales income: 1,280 Cash."));
            Assert.That(panel.Line("offline-sold").text, Is.EqualTo("Sold: 128 " + Label("iron.bar") + "."));
            Assert.That(panel.Line("offline-produced").text, Is.EqualTo("Produced: 400 " + Label("iron.ore") + "."));
            Assert.That(panel.Line("offline-consumed").text, Is.EqualTo("Used: 360 " + Label("iron.ore") + "."));
            Assert.That(panel.Line("offline-stock-iron.ore").text, Is.EqualTo(Label("iron.ore") + ": 12 to 52 (+40)."));
            Assert.That(panel.Line("offline-stock-iron.bar").text, Is.EqualTo(Label("iron.bar") + ": 40 to 10 (-30)."));
            Assert.That(panel.Line("offline-expired-0").text, Is.EqualTo("Item effect ended: " + catalog.Items["item.flux-catalyst"].Label + " on the factory."));
            Assert.That(panel.Line("offline-overdrive-ended").text, Is.EqualTo("Overdrive ended."));
            Assert.That(panel.Line("offline-contract").text, Does.StartWith("Contract expired: deliver 100 ").And.EndWith("No reward, no penalty."));
            Assert.That(panel.Line("offline-limit-cap").text, Is.EqualTo("Offline credit is capped at 8 h; time beyond the cap earned nothing."));
            Assert.That(panel.Line("offline-limit-partial").text, Does.StartWith("3 cycle(s) made fewer units than a full batch"));
            Assert.That(panel.Line("offline-limit-blocked-0").text, Is.EqualTo("Slot 2 " + catalog.Machines["machine.smelter"].Label + " is waiting: an input is missing."));
            Assert.That(panel.Line("offline-headline").text, Does.Not.Contain("No production"));

            Assert.That(Focused(rig.Root), Is.SameAs(panel.ContinueButton), "the surface opens with focus on Continue");
            Assert.That(panel.ContinueButton.ClassListContains("run-control--focused"), Is.True);
            Assert.That(IsDisplayed(panel.RetryButton), Is.False, "a summary has nothing to retry");
        }

        [UnityTest]
        public IEnumerator AShortAbsenceSaysNoProductionWhileAway()
        {
            yield return OpenShell(ShellScreens.All().First());
            panel.ShowSummary(ShortAbsence());
            yield return Frames();

            Assert.That(panel.Line("offline-headline").text, Is.EqualTo("No production while away."));
            Assert.That(panel.Line("offline-measured").text, Is.EqualTo("Away for 45 s."));
            Assert.That(panel.Line("offline-credited").text, Is.EqualTo("Credited: 45 s."));
            Assert.That(panel.Line("offline-economy-title"), Is.Null, "no cash or stock section for an idle return");
            Assert.That(IsDisplayed(panel.ContinueButton), Is.True);
        }

        [UnityTest]
        public IEnumerator ABackwardClockExplainsThatNothingWasCredited()
        {
            yield return OpenShell(ShellScreens.All().First());
            panel.ShowSummary(NewSummary((nameof(RunOfflineSummary.Outcome), RunOfflineOutcome.BackwardClock)));
            yield return Frames();

            Assert.That(panel.Line("offline-headline").text, Is.EqualTo(OfflineSummaryPanel.BackwardClockText));
            Assert.That(panel.Line("offline-credited"), Is.Null, "no credit is claimed");
            Assert.That(panel.Line("offline-economy-title"), Is.Null);
            Assert.That(IsDisplayed(panel.ContinueButton), Is.True);
        }

        [UnityTest]
        public IEnumerator AForfeitedCreditSaysThatTheFactoryIsAsYouLeftIt()
        {
            yield return OpenShell(ShellScreens.All().First());
            var summary = NewSummary(
                (nameof(RunOfflineSummary.Outcome), RunOfflineOutcome.SimulationRejected),
                (nameof(RunOfflineSummary.SimulationResult), RunAdvanceResult.ArithmeticOverflow),
                (nameof(RunOfflineSummary.MeasuredMs), 5L * 3_600_000L));
            Assert.That(summary.CreditForfeited, Is.True);
            panel.ShowSummary(summary);
            yield return Frames();

            Assert.That(panel.Line("offline-headline").text, Is.EqualTo(OfflineSummaryPanel.ForfeitedText));
            Assert.That(panel.Line("offline-measured").text, Is.EqualTo("Away for 5 h."));
        }

        // ---------------------------------------------------------------------------------------------------
        // Continue, focus (UX-2)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ContinueClosesTheSurfaceAndReturnsFocusToTheControlThatHadIt()
        {
            yield return OpenShell(ShellScreens.All().First());
            var dismissed = 0;
            panel.Dismissed += () => dismissed++;
            rig.Shell.CashButton.Focus();
            panel.ShowSummary(ShortAbsence());
            yield return Frames();
            Assert.That(Focused(rig.Root), Is.SameAs(panel.ContinueButton));

            Submit(panel.ContinueButton);
            yield return Frames();

            Assert.That(panel.IsOpen, Is.False);
            Assert.That(panel.Kind, Is.EqualTo(OfflineSurfaceKind.None));
            Assert.That(dismissed, Is.EqualTo(1));
            Assert.That(IsDisplayed(panel.Root), Is.False);
            Assert.That(Focused(rig.Root), Is.SameAs(rig.Shell.CashButton), "focus returns to the control that had it");
        }

        [UnityTest]
        public IEnumerator EscapeAndAndroidBackCloseASummary()
        {
            yield return OpenShell(ShellScreens.All().First());
            panel.ShowSummary(ShortAbsence());
            yield return Frames();

            Key(Focused(rig.Root), KeyCode.Escape);
            yield return Frames();

            Assert.That(panel.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator FocusStaysInsideTheOpenSurface()
        {
            yield return OpenShell(ShellScreens.All().First());
            panel.ShowSummary(NormalReturn());
            yield return Frames();

            rig.Shell.TabButton(ShellTab.Market).Focus();
            yield return Frames(4);

            Assert.That(Focused(rig.Root), Is.SameAs(panel.ContinueButton), "focus that lands behind the surface goes back into it");
        }

        // ---------------------------------------------------------------------------------------------------
        // The application seams (US5 scenarios 1, 3 and 4)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ARealReturnShowsItsSummaryAndARotationWithoutDepartureShowsNone()
        {
            var clock = new StatusTestClock();
            var persistence = NewPersistence(clock).Start();
            persistence.BuildMiner();
            yield return OpenShell(ShellScreens.All().First(), retry: persistence.Persistence.RetryReturn, withCatalog: persistence.Catalog);

            var adapter = Adapter(persistence, out var screenConfiguration, out var realtime);
            var kinds = new List<LifecycleReturnKind>();
            var presented = new List<bool>();
            adapter.ResultChanged += (result, kind) =>
            {
                kinds.Add(kind);
                presented.Add(panel.Present(result, kind));
            };

            // A runtime rotation: the screen configuration changes and the app loses focus for a moment (FR-036).
            screenConfiguration[0] = new LifecycleScreenConfiguration(640, 360, ScreenOrientation.LandscapeLeft);
            adapter.ObserveScreenConfiguration();
            adapter.NotifyFocusChanged(false);
            adapter.NotifyFocusChanged(true);
            yield return Frames();

            Assert.That(kinds.Last(), Is.EqualTo(LifecycleReturnKind.ConfigurationChange));
            Assert.That(presented.Last(), Is.False, "a rotation creates no summary");
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(panel.Kind, Is.EqualTo(OfflineSurfaceKind.None));

            // A saved checkpoint, a reload and a departure result are not returns either.
            Assert.That(panel.Present(persistence.Persistence.SaveNow(), LifecycleReturnKind.Return), Is.False, "a result without a summary opens nothing");
            Assert.That(panel.Present(persistence.Persistence.SaveNow(), LifecycleReturnKind.None), Is.False);

            // A real departure and a return after three hours: the summary shows.
            realtime[0] += 10.0;
            adapter.NotifyPauseChanged(true);
            clock.Now += TimeSpan.FromHours(3);
            adapter.NotifyPauseChanged(false);
            yield return Frames();

            Assert.That(kinds.Last(), Is.EqualTo(LifecycleReturnKind.Return));
            Assert.That(presented.Last(), Is.True);
            Assert.That(panel.Kind, Is.EqualTo(OfflineSurfaceKind.Summary));
            Assert.That(panel.Line("offline-measured").text, Is.EqualTo("Away for 3 h."));
            Assert.That(panel.Line("offline-completions"), Is.Not.Null, "the Miner worked while the player was away");
            Assert.That(panel.Summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
        }

        [UnityTest]
        public IEnumerator AnOpenSummaryStaysWhileTheScreenRotates()
        {
            yield return OpenShell(ShellScreens.All().First());
            panel.ShowSummary(NormalReturn());
            yield return Frames();

            var rotated = ShellScreens.Rotated(rig.Screen);
            yield return rig.Layout(rotated);
            panel.SyncSafeArea();
            yield return Frames();

            Assert.That(panel.IsOpen, Is.True, "a rotation is not a departure and does not close or recreate the summary");
            Assert.That(panel.Kind, Is.EqualTo(OfflineSurfaceKind.Summary));
        }

        [UnityTest]
        public IEnumerator AnUnavailableClockShowsTheExplanationAndRetryCompletesTheReturn()
        {
            var clock = new StatusTestClock();
            var persistence = NewPersistence(clock).Start();
            persistence.BuildMiner();
            yield return OpenShell(ShellScreens.All().First(), retry: persistence.Persistence.RetryReturn, withCatalog: persistence.Catalog);

            var results = new List<RunCommandResult<RunPersistenceOutcome>>();
            panel.RetryCompleted += results.Add;
            var blocked = persistence.Away(TimeSpan.FromHours(3), clockFailsOnReturn: true);
            Assert.That(blocked.IsSuccess, Is.False);
            Assert.That(persistence.Persistence.Lifecycle, Is.EqualTo(IdleFactory.Core.Application.LifecycleState.ReturnBlocked));

            Assert.That(panel.Present(blocked, LifecycleReturnKind.Return), Is.True);
            yield return Frames();
            Assert.That(panel.Kind, Is.EqualTo(OfflineSurfaceKind.Blocked));
            Assert.That(panel.TitleLabel.text, Is.EqualTo(OfflineSummaryPanel.BlockedTitle));
            Assert.That(panel.Line("offline-blocked").text, Is.EqualTo(OfflineSummaryPanel.BlockedText));
            Assert.That(IsDisplayed(panel.RetryButton), Is.True);
            Assert.That(IsDisplayed(panel.ContinueButton), Is.False, "a blocked return stays until it is retried");
            Assert.That(Focused(rig.Root), Is.SameAs(panel.RetryButton));

            // Retry while the clock is still unavailable: the explanation stays and says so; nothing was changed.
            Submit(panel.RetryButton);
            yield return Frames();
            Assert.That(panel.Kind, Is.EqualTo(OfflineSurfaceKind.Blocked));
            Assert.That(panel.FeedbackLabel.text, Is.EqualTo(OfflineSummaryPanel.RetryFailedText));
            Assert.That(IsDisplayed(panel.FeedbackLabel), Is.True);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].IsSuccess, Is.False);
            Assert.That(persistence.Persistence.Lifecycle, Is.EqualTo(IdleFactory.Core.Application.LifecycleState.ReturnBlocked));

            // The clock is back: Retry completes the return exactly once and the summary shows.
            clock.Fail = false;
            Submit(panel.RetryButton);
            yield return Frames();
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[1].IsSuccess, Is.True);
            Assert.That(persistence.Persistence.Lifecycle, Is.EqualTo(IdleFactory.Core.Application.LifecycleState.Running));
            Assert.That(panel.Kind, Is.EqualTo(OfflineSurfaceKind.Summary));
            Assert.That(panel.Line("offline-measured").text, Is.EqualTo("Away for 3 h."));
            Assert.That(IsDisplayed(panel.ContinueButton), Is.True);
            Assert.That(IsDisplayed(panel.RetryButton), Is.False);
        }

        // ---------------------------------------------------------------------------------------------------
        // Text scale and mounting
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator UnderTheShellTheSurfaceFollowsTheShellTextScale([ValueSource(nameof(Percents))] int percent)
        {
            yield return OpenShell(ShellScreens.All().First(), percent);
            panel.ShowSummary(NormalReturn());
            yield return Frames();

            Assert.That(panel.IsStandalone, Is.False);
            Assert.That(panel.Root.ClassListContains(StatusSurface.StandaloneClass), Is.False);
            Assert.That(panel.Line("offline-measured").resolvedStyle.fontSize, Is.EqualTo(14f * percent / 100f).Within(0.01f));
            Assert.That(panel.TitleLabel.resolvedStyle.fontSize, Is.EqualTo(16f * percent / 100f).Within(0.01f));
            Assert.That(panel.Root.parent, Is.SameAs(rig.Root));
            Assert.That(rig.Root.IndexOf(panel.Root), Is.LessThan(rig.Root.IndexOf(rig.Shell.DialogLayer)), "the shell's dialog layer stays on top of the surface");
        }

        [UnityTest]
        public IEnumerator OutsideTheShellTheSurfaceSuppliesItsOwnPaletteAndTextScale([ValueSource(nameof(Percents))] int percent)
        {
            var screen = ShellScreens.All().First();
            var standalone = new OfflineSummaryPanelTestSupport.StandaloneRig(screen);
            standaloneRigs.Add(standalone);
            catalog = RunShellFixtures.NewSession(out _, unlockEverything: true).Catalog;
            panel = new OfflineSummaryPanel(catalog, null, Styles("OfflineSummaryPanel.uss"));
            yield return standalone.Mount(panel, percent);
            panel.ShowSummary(NormalReturn());
            yield return Frames();

            Assert.That(panel.IsStandalone, Is.True);
            Assert.That(panel.StandaloneTextScalePercent, Is.EqualTo(percent));
            Assert.That(panel.Root.ClassListContains("run-text-scale--" + percent), Is.True);
            Assert.That(panel.Line("offline-measured").resolvedStyle.fontSize, Is.EqualTo(14f * percent / 100f).Within(0.01f));
            Assert.That(panel.Line("offline-measured").resolvedStyle.color.a, Is.GreaterThan(0.9f), "the standalone palette defines the text colour");
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout (UX-3, F-4): worst-case content at every screen class (rotated forms included) at 100 and 200 percent
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheWorstCaseSummaryStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return OpenShell(screen, percent);
            panel.ShowSummary(WorstCase(catalog));
            panel.SyncSafeArea();
            yield return Frames();

            yield return AssertSurfaceLayout(panel, rig.SafeRect(), screen, "worst-case summary", percent);
            if (percent >= 200)
            {
                var viewport = panel.Scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport");
                var range = panel.Scroll.contentContainer.layout.height - viewport.layout.height;
                Assert.That(range, Is.GreaterThan(0f), $"{screen} @{percent}%: the worst-case summary scrolls");
            }
        }

        [UnityTest]
        public IEnumerator TheBlockedExplanationStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return OpenShell(screen, percent, retry: () => RunCommandResult.Reject<RunPersistenceOutcome>(RunPersistenceReasons.ReturnBlocked));
            panel.ShowBlocked();
            Submit(panel.RetryButton);
            panel.SyncSafeArea();
            yield return Frames();

            Assert.That(IsDisplayed(panel.FeedbackLabel), Is.True, "the longest state: explanation plus the failed retry");
            yield return AssertSurfaceLayout(panel, rig.SafeRect(), screen, "blocked return", percent);
        }

        [UnityTest]
        public IEnumerator TheWorstCaseSummaryStaysContainedOutsideTheShell([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            var standalone = new OfflineSummaryPanelTestSupport.StandaloneRig(screen);
            standaloneRigs.Add(standalone);
            catalog = RunShellFixtures.NewSession(out _, unlockEverything: true).Catalog;
            panel = new OfflineSummaryPanel(catalog, null, Styles("OfflineSummaryPanel.uss"));
            yield return standalone.Mount(panel, 200);
            panel.ShowSummary(WorstCase(catalog));
            yield return Frames();

            yield return AssertSurfaceLayout(panel, standalone.SafeRect(), screen, "standalone worst-case summary", 200);
        }

        // ---------------------------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------------------------

        private RunLifecycleAdapter Adapter(StatusPersistenceRig persistence, out LifecycleScreenConfiguration[] screen, out double[] realtime)
        {
            var host = new GameObject("status-lifecycle-adapter");
            objects.Add(host);
            var adapter = host.AddComponent<RunLifecycleAdapter>();
            adapter.Configure(persistence.Persistence, null);
            var screenBox = new[] { new LifecycleScreenConfiguration(360, 640, ScreenOrientation.Portrait) };
            var timeBox = new[] { 100.0 };
            adapter.UseScreenConfigurationSource(() => screenBox[0], () => timeBox[0]);
            screen = screenBox;
            realtime = timeBox;
            return adapter;
        }
    }
}
#endif
