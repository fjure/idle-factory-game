#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.MarketPanelTestSupport;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T333 (US5/US6, FR-018, F5-R07; QA-12 rows "Goal line" and "Prestige threshold"; contracts/ui-surfaces-f005.md): the goal line
    /// (<c>goals-next</c>) at the top of Market and the threshold line of the Prestige panel (<c>prestige-threshold</c>).
    /// Fixture: threshold 1,000 (legacy, no growth) and milestones at 25 %, 50 % and 75 % (250, 500, 750), so the Factory Value of the
    /// bare run is its Cash. Covers the next goal with the live percentage, the one-time notice (and that a seed, a hidden Market and a
    /// dip below a passed goal never notify), "Factory Sale available", threshold-only content, "All goals reached" (only with the Sale),
    /// the unavailable state, the real play time handed to the tracker, accessible names, exact values and focus, the layout at every
    /// screen class at 100 and 200 percent text, and the Prestige threshold at an index, with growth, and at the maximum index.
    /// </summary>
    public sealed class GoalsLinePanelTests
    {
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private RunSession session;
        private RunCommands commands;
        private MarketPanelHost host;

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
        }

        // ---------------------------------------------------------------------------------------------------
        // Fixtures
        // ---------------------------------------------------------------------------------------------------

        private static MilestoneDef[] Quarters() => new[] { new MilestoneDef(250_000), new MilestoneDef(500_000), new MilestoneDef(750_000) };

        private static PrestigeDef Flat(long threshold = 1_000) => new PrestigeDef(threshold, "sqrt", 10, 1_000);

        /// <summary>A run over the shipped content with the given prestige definition, milestones and prestige count, and this much Cash.</summary>
        private static RunSession GoalSession(out RunCommands runCommands, long cash, MilestoneDef[] milestones, PrestigeDef prestige,
            long prestigeCount = 0, IRunEventSink sink = null)
        {
            var shipped = RunShellFixtures.ShippedCatalog;
            var catalog = new RunCatalog(shipped.ContentRevision, shipped.Resources.Values, shipped.Recipes.Values, shipped.Machines.Values,
                shipped.StorageLevels, shipped.FactoryLevels, shipped.ContractOffers.Values, shipped.Items.Values, shipped.ResearchInContentOrder,
                shipped.IndustryUpgrades.Values, shipped.Overdrive, prestige, shipped.RunStart, shipped.OfflinePolicy, shipped.ContractItemLadder,
                milestones, shipped.ChainDominanceMarginPpm);
            var permanent = new PermanentState(catalog, 0, prestigeCount);
            var start = NextRunStart.Compute(catalog, permanent);
            var startMs = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var state = new RunState(catalog, Guid.NewGuid(), 1, null, cash, 0, start.StorageLevel, 1, catalog.RunStart.InitialUnlocks);
            Assert.That(new RunSimulator(catalog).Advance(state, permanent, 0, startMs, null), Is.EqualTo(RunAdvanceResult.Ok));
            var run = sink == null ? new RunSession(catalog, state, permanent, startMs) : new RunSession(catalog, state, permanent, startMs, sink);
            runCommands = new RunCommands(run);
            return run;
        }

        /// <summary>A new run of the same catalog and permanent state, as a load or a restart installs it, with this much Cash.</summary>
        private static RunState NewRunState(RunSession run, long cash)
        {
            var start = NextRunStart.Compute(run.Catalog, run.Permanent);
            var state = new RunState(run.Catalog, Guid.NewGuid(), 2, run.State.RunId, cash, 0, start.StorageLevel, 1, run.Catalog.RunStart.InitialUnlocks);
            Assert.That(new RunSimulator(run.Catalog).Advance(state, run.Permanent, 0, run.AnchorUtcMs, null), Is.EqualTo(RunAdvanceResult.Ok));
            return state;
        }

        private static long FactoryValueOf(RunSession run) =>
            FactoryValuation.TryCompute(run.Catalog, run.State, run.Permanent, out var breakdown) ? breakdown.Total : ExactBound.Value;

        /// <summary>Moves the Factory Value of the run to an exact value through the revision protocol of a command, as play does.</summary>
        private static void SetFactoryValue(RunSession run, long target)
        {
            var current = FactoryValueOf(run);
            if (target == current)
            {
                return;
            }

            run.BeginCommand();
            var result = target > current ? run.State.TryAddCash(target - current) : run.State.TrySpendCash(current - target);
            Assert.That(result, Is.EqualTo(RunStateResult.Ok));
            Assert.That(run.EndCommand(0, false).IsSuccess, Is.True);
            Assert.That(FactoryValueOf(run), Is.EqualTo(target), "the fixture reaches the exact Factory Value");
        }

        private IEnumerator Open(RunSession run, RunCommands runCommands, ShellScreen screen = null, int percent = 100, ShellDeviceProfile? profile = null)
        {
            session = run;
            commands = runCommands;
            rig = ShellRig.Create(screen ?? ShellScreens.All().First(), profile, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            host = new MarketPanelHost(session, commands, rig.Shell, Tree("InventoryRatesPanel"), Styles("InventoryRatesPanel"),
                Tree("PrioritiesPanel"), Styles("PrioritiesPanel"), Tree("ExpansionPanel"), Styles("ExpansionPanel"), Icon);
            host.Goals.RefreshIntervalSeconds = 0f;
            host.Inventory.RefreshIntervalSeconds = 0f;
            rig.Shell.Panels.Register(host);
            rig.Shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
        }

        private IEnumerator Change(long factoryValue)
        {
            SetFactoryValue(session, factoryValue);
            host.Refresh();
            yield return ShellRig.Settle();
        }

        private GoalsLinePanel Goals => host.Goals;

        private sealed class RecordingSink : IRunEventSink
        {
            public readonly List<RunMilestoneEvent> Milestones = new List<RunMilestoneEvent>();

            public void OnHeader(in RunHeaderEvent e) { }
            public void OnSample(in RunSampleEvent e) { }
            public void OnMilestone(in RunMilestoneEvent e) => Milestones.Add(e);
            public void OnSale(in RunSaleEvent e) { }
            public void OnMining(in RunMiningEvent e) { }
            public void OnContract(in RunContractEvent e) { }
            public void OnPrestige(in RunPrestigeEvent e) { }
            public void OnOfflineReturn(in RunOfflineReturnEvent e) { }
            public void OnBottleneck(in RunBottleneckEvent e) { }
            public void OnPurchase(in RunPurchaseEvent e) { }
            public void OnClock(in RunClockEvent e) { }
            public void OnLifecycle(in RunLifecycleEvent e) { }
        }

        // ---------------------------------------------------------------------------------------------------
        // Goal line: next goal
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AFreshRunShowsTheFirstMilestoneAsTheNextGoalWithItsAccessibleNameAndExactValue()
        {
            var run = GoalSession(out var runCommands, 100, Quarters(), Flat());
            yield return Open(run, runCommands);

            Assert.That(Goals.Root.parent, Is.SameAs(host.Root));
            Assert.That(host.Root.Children().First(), Is.SameAs(Goals.Root), "the goal line is the top of Market");
            Assert.That(Goals.NextLabel.name, Is.EqualTo("goals-next"));
            Assert.That(Goals.NextLabel.text, Is.EqualTo("Next goal $250: $100 of $250 (40 %)"));
            Assert.That(Goals.NextLabel.tooltip, Is.EqualTo("Next goal $250, 40 percent"), "accessible name pattern of the contract");
            Assert.That(Goals.ExactButton.tooltip, Is.EqualTo("Next goal $250, 40 percent, exact value"), "the focusable control carries the state-bearing name");
            Assert.That(Goals.IsNextShown, Is.True);
            Assert.That(Goals.IsAllReachedShown, Is.False);
            Assert.That(Goals.IsReasonShown, Is.False);
            Assert.That(Goals.ExactText, Does.Contain("Factory Value 100 of $250"));

            Goals.ExactButton.Focus();
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.DetailText, Is.EqualTo(Goals.ExactText), "the exact value shows in the shell detail line on focus");
            Assert.That(Goals.ExactButton.focusable, Is.True);
        }

        [UnityTest]
        public IEnumerator TheLivePercentageFollowsTheFactoryValueInsideAGoalWindowWithoutARebuild()
        {
            var run = GoalSession(out var runCommands, 100, Quarters(), Flat());
            yield return Open(run, runCommands);
            var rebuilds = Goals.Tracker.Rebuilds;
            Assert.That(Goals.NextLabel.text, Does.EndWith("(40 %)"));

            yield return Change(200);

            Assert.That(Goals.Tracker.Rebuilds, Is.EqualTo(rebuilds), "no goal was crossed, so the cached snapshot is reused");
            Assert.That(Goals.NextLabel.text, Is.EqualTo("Next goal $250: $200 of $250 (80 %)"), "live PercentAt, not the cached percentage");
            Assert.That(Goals.NextLabel.tooltip, Is.EqualTo("Next goal $250, 80 percent"));
            Assert.That(Goals.ExactButton.tooltip, Is.EqualTo("Next goal $250, 80 percent, exact value"), "the name follows the live percentage");
        }

        [UnityTest]
        public IEnumerator ReachingAMilestoneMakesTheNextOneTheGoalAndRaisesExactlyOneNotice()
        {
            var run = GoalSession(out var runCommands, 240, Quarters(), Flat());
            yield return Open(run, runCommands);
            Assert.That(rig.Shell.NoticeText, Is.Empty, "opening raises no notice");

            yield return Change(260);

            Assert.That(Goals.NextLabel.text, Is.EqualTo("Next goal $500: $260 of $500 (52 %)"));
            Assert.That(rig.Shell.NoticeText, Is.EqualTo("Goal reached: 1 of 3"));
            Assert.That(rig.Shell.NoticeKind, Is.EqualTo(ShellNoticeKind.Info));

            // A dip below the passed goal and a second crossing stay quiet: the notice is keyed on (run, milestone index).
            rig.Shell.ClearNotice();
            yield return Change(240);
            yield return Change(260);
            Assert.That(rig.Shell.NoticeText, Is.Empty, "a passed goal never notifies twice");
        }

        [UnityTest]
        public IEnumerator PassedGoalsAreSeededSoOpeningTheTabNeverNotifiesEvenForGoalsPassedWhileItWasHidden()
        {
            var run = GoalSession(out var runCommands, 600, Quarters(), Flat());
            yield return Open(run, runCommands);
            Assert.That(rig.Shell.NoticeText, Is.Empty, "a run already past two goals does not notify when it is shown (load or first open)");
            Assert.That(Goals.NextLabel.text, Is.EqualTo("Next goal $750: $600 of $750 (80 %)"));

            rig.Shell.SelectTab(ShellTab.Factory);
            yield return ShellRig.Settle();
            SetFactoryValue(session, 800);
            rig.Shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();

            Assert.That(rig.Shell.NoticeText, Is.Empty, "Market-only: a goal passed while another tab was open is seeded silently");
            Assert.That(Goals.NextLabel.text, Is.EqualTo("Next goal $1,000: $800 of $1,000 (80 %)"));
        }

        // ---------------------------------------------------------------------------------------------------
        // Factory Sale, all reached, threshold-only, unavailable
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator PassingEveryMilestoneBelowTheThresholdShowsTheThresholdAsTheGoalNotAllGoalsReached()
        {
            var run = GoalSession(out var runCommands, 800, Quarters(), Flat());
            yield return Open(run, runCommands);

            var goals = RunSnapshots.Goals(run);
            Assert.That(goals.AllReached, Is.True, "the snapshot flag alone is true here, which is why the panel must not render from it");
            Assert.That(goals.FactorySaleAvailable, Is.False);
            Assert.That(Goals.NextLabel.text, Is.EqualTo("Next goal $1,000: $800 of $1,000 (80 %)"));
            Assert.That(Goals.IsAllReachedShown, Is.False, "AllReached alone never renders \"All goals reached\"");
            Assert.That(Goals.ExactText, Does.Contain("Factory Sale threshold"));
        }

        [UnityTest]
        public IEnumerator TheThresholdShowsFactorySaleAvailableWithAllGoalsReachedAndNoticesOnce()
        {
            var run = GoalSession(out var runCommands, 990, Quarters(), Flat());
            yield return Open(run, runCommands);
            Assert.That(Goals.IsAllReachedShown, Is.False);

            yield return Change(1_000);

            Assert.That(Goals.NextLabel.text, Is.EqualTo(GoalsLinePanel.SaleText));
            Assert.That(Goals.NextLabel.tooltip, Is.EqualTo(GoalsLinePanel.SaleText));
            Assert.That(Goals.ExactButton.tooltip, Is.EqualTo(GoalsLinePanel.SaleText + ", exact value"));
            Assert.That(Goals.IsAllReachedShown, Is.True);
            Assert.That(Goals.AllReachedLabel.text, Is.EqualTo("All goals reached"));
            Assert.That(rig.Shell.NoticeText, Is.EqualTo(GoalsLinePanel.SaleText));

            rig.Shell.ClearNotice();
            yield return Change(2_000);
            Assert.That(Goals.NextLabel.text, Is.EqualTo(GoalsLinePanel.SaleText), "above the threshold the Sale stays available");
            Assert.That(rig.Shell.NoticeText, Is.Empty, "no second notice");
        }

        [UnityTest]
        public IEnumerator ContentWithoutMilestonesShowsTheThresholdAsTheOnlyGoalAndNeverAllGoalsReached()
        {
            var run = GoalSession(out var runCommands, 400, new MilestoneDef[0], Flat());
            yield return Open(run, runCommands);

            Assert.That(Goals.NextLabel.text, Is.EqualTo("Next goal $1,000: $400 of $1,000 (40 %)"));
            Assert.That(Goals.IsAllReachedShown, Is.False);

            yield return Change(1_000);

            Assert.That(Goals.NextLabel.text, Is.EqualTo(GoalsLinePanel.SaleText));
            Assert.That(Goals.IsAllReachedShown, Is.False, "with no milestones there is nothing to have reached before the Sale");
            Assert.That(rig.Shell.NoticeText, Is.EqualTo(GoalsLinePanel.SaleText));
        }

        [UnityTest]
        public IEnumerator AnUnderivableThresholdShowsTheReasonInsteadOfAGoal()
        {
            // 5 x 10^17 doubles per index: index 2 would exceed 10^18, so the goal line is unavailable (GoalsSnapshot.Available = false).
            var prestige = new PrestigeDef(1_000, "sqrt", 10, 1_000, new ThresholdDef(500_000_000_000_000_000L, 2_000_000, 5));
            var run = GoalSession(out var runCommands, 100, Quarters(), prestige, prestigeCount: 2);
            yield return Open(run, runCommands);

            Assert.That(RunSnapshots.Goals(run).Available, Is.False);
            Assert.That(Goals.IsNextShown, Is.False);
            Assert.That(Goals.ExactButton.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(Goals.IsReasonShown, Is.True);
            Assert.That(Goals.ReasonLabel.text, Is.EqualTo(GoalsLinePanel.UnavailableText));
            Assert.That(Goals.ExactButton.tooltip, Is.EqualTo("The goal line is not available for this run, exact value"));
            Assert.That(Goals.IsAllReachedShown, Is.False);
            Assert.That(rig.Shell.NoticeText, Is.Empty);
        }

        [UnityTest]
        public IEnumerator FocusMovesToAVisibleSiblingWhenTheGoalLineBecomesUnavailableWhileItsExactValueHasFocus()
        {
            // #398 T4 (shared focus rule): index 0 of 5e17 x 2^n is derivable; a loaded run at prestige count 2 is not (the same
            // definition as AnUnderivableThresholdShowsTheReasonInsteadOfAGoal), so the exact-value button is hidden under focus.
            var prestige = new PrestigeDef(1_000, "sqrt", 10, 1_000, new ThresholdDef(500_000_000_000_000_000L, 2_000_000, 5));
            var run = GoalSession(out var runCommands, 100, Quarters(), prestige, prestigeCount: 0);
            yield return Open(run, runCommands);
            Assert.That(RunSnapshots.Goals(run).Available, Is.True, "the fixture starts with a goal line");
            Goals.ExactButton.Focus();
            yield return ShellRig.Settle();
            Assert.That(rig.Root.focusController.focusedElement, Is.SameAs(Goals.ExactButton));

            run.ReplaceLoaded(NewRunState(run, 100), new PermanentState(run.Catalog, 0, 2), run.AnchorUtcMs);
            Assert.That(RunSnapshots.Goals(run).Available, Is.False, "the loaded run has no derivable goal");
            host.Refresh();
            yield return ShellRig.Settle();

            Assert.That(Goals.ExactButton.style.display.value, Is.EqualTo(DisplayStyle.None));
            var focused = rig.Root.focusController.focusedElement as VisualElement;
            Assert.That(focused, Is.Not.Null, "focus is not lost");
            Assert.That(focused, Is.Not.SameAs(Goals.ExactButton), "focus left the hidden control");
            Assert.That(RunShellLayoutAssertions.IsDisplayed(focused), Is.True, "focus sits on a displayed control");
            Assert.That(rig.Shell.GetFocusOrder(), Does.Contain(focused), "and on one in the focus order");
        }

        // ---------------------------------------------------------------------------------------------------
        // Run play time and a new run
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheRunPlayTimeReachesTheRun2EligibleEvent()
        {
            var sink = new RecordingSink();
            var run = GoalSession(out var runCommands, 900, Quarters(), Flat(), sink: sink);
            yield return Open(run, runCommands);
            Assert.That(sink.Milestones, Is.Empty, "the seed raises nothing");

            Assert.That(run.Advance(125L * 1000L * RunSession.TicksPerMs).IsSuccess, Is.True);
            yield return Change(1_000);

            var eligible = sink.Milestones.Where(e => e.Kind == RunMilestoneKind.Run2Eligible).ToList();
            Assert.That(eligible.Count, Is.EqualTo(1));
            Assert.That(eligible[0].PlaySeconds, Is.EqualTo(125), "the presenter passes the run's real play time, not 0");
        }

        [UnityTest]
        public IEnumerator AnInjectedPlayTimeSourceIsUsed()
        {
            var sink = new RecordingSink();
            var run = GoalSession(out var runCommands, 900, new MilestoneDef[0], Flat(), sink: sink);
            session = run;
            rig = ShellRig.Create(ShellScreens.All().First(), null, 100);
            rigs.Add(rig);
            yield return rig.Layout();
            var goals = new GoalsLinePanel(run, rig.Shell, Styles("InventoryRatesPanel"), Icon, () => 4_321);
            goals.RefreshIntervalSeconds = 0f;
            goals.OnShown(null);
            SetFactoryValue(run, 1_000);
            goals.Refresh();

            Assert.That(sink.Milestones.Single(e => e.Kind == RunMilestoneKind.Run2Eligible).PlaySeconds, Is.EqualTo(4_321));
        }

        [UnityTest]
        public IEnumerator TheSamePanelSeesANewRunReseedsSilentlyAndStillNoticesItsOwnGoals()
        {
            var run = GoalSession(out var runCommands, 990, Quarters(), Flat());
            yield return Open(run, runCommands);
            yield return Change(1_000);
            Assert.That(rig.Shell.NoticeText, Is.EqualTo(GoalsLinePanel.SaleText));
            rig.Shell.ClearNotice();
            Assert.That(Goals.NextLabel.text, Is.EqualTo(GoalsLinePanel.SaleText));
            var oldRun = run.State.RunId;

            // A load or restart installs another run in the same session (the same panel instance): it reads the new RunId and seeds from it.
            run.ReplaceLoaded(NewRunState(run, 600), run.Permanent, run.AnchorUtcMs);
            Assert.That(run.State.RunId, Is.Not.EqualTo(oldRun));
            host.Refresh();
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.NoticeText, Is.Empty, "the passed goals of the new run, and the Sale of the old one, never notify");
            Assert.That(Goals.NextLabel.text, Is.EqualTo("Next goal $750: $600 of $750 (80 %)"));
            Assert.That(Goals.IsAllReachedShown, Is.False);

            // The new run has its own one-time notices: crossing its third milestone and the threshold notify once each.
            yield return Change(760);
            Assert.That(rig.Shell.NoticeText, Is.EqualTo("Goal reached: 3 of 3"));
            rig.Shell.ClearNotice();
            yield return Change(1_000);
            Assert.That(rig.Shell.NoticeText, Is.EqualTo(GoalsLinePanel.SaleText), "the Sale of the new run notifies although the old one had");
        }

        // ---------------------------------------------------------------------------------------------------
        // Exact-value inspection: hover, tap-to-pin and long press
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator OnAPointerDeviceHoverShowsTheExactGoalValues()
        {
            var screen = ShellScreens.All().First();
            var run = GoalSession(out var runCommands, 123_456, Quarters(), Flat(999_999_999));
            yield return Open(run, runCommands, screen, 100, ShellDeviceProfile.Pointer(screen.Width, screen.Height, 96f));
            Send(Goals.ExactButton, PointerEnterEvent.GetPooled(), UnityEngine.UIElements.PointerType.mouse);
            yield return null;
            Assert.That(rig.Shell.DetailText, Is.EqualTo(Goals.ExactText));
            Assert.That(Goals.ExactText, Does.Contain("123,456").And.Contain("$249,999,999"));
        }

        [UnityTest]
        public IEnumerator OnATouchDeviceATapPinsAndALongPressPinsTheExactGoalValues()
        {
            var screen = ShellScreens.All().First();
            var run = GoalSession(out var runCommands, 100, Quarters(), Flat());
            yield return Open(run, runCommands, screen, 100, ShellDeviceProfile.Touch(screen.Width, screen.Height, 400f));
            Send(Goals.ExactButton, ClickEvent.GetPooled(), UnityEngine.UIElements.PointerType.touch);
            yield return null;
            Assert.That(rig.Shell.IsDetailPinned, Is.True);
            Assert.That(rig.Shell.DetailText, Is.EqualTo(Goals.ExactText));

            Send(Goals.ExactButton, ClickEvent.GetPooled(), UnityEngine.UIElements.PointerType.touch);
            yield return null;
            Assert.That(rig.Shell.IsDetailPinned, Is.False, "a second tap closes it");

            Send(Goals.ExactButton, PointerDownEvent.GetPooled(), UnityEngine.UIElements.PointerType.touch);
            yield return new WaitForSeconds(RunShellDocument.LongPressSeconds + 0.3f);
            Assert.That(rig.Shell.IsDetailPinned, Is.True, "a long press pins the exact value");
            Assert.That(rig.Shell.DetailText, Is.EqualTo(Goals.ExactText));
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout at every screen class, 100 and 200 percent text, with worst-case content
        // ---------------------------------------------------------------------------------------------------

        private static long WorstThreshold => 999_999_999;

        [UnityTest]
        public IEnumerator TheNextGoalLineFitsEveryScreenClassAtEveryTextScale(
            [ValueSource(typeof(MarketPanelTestSupport), nameof(Screens))] ShellScreen screen,
            [ValueSource(typeof(MarketPanelTestSupport), nameof(Percents))] int percent)
        {
            var run = GoalSession(out var runCommands, 123_456_789, Quarters(), Flat(WorstThreshold));
            yield return Open(run, runCommands, screen, percent);
            var where = $"{screen} @{percent}%";
            Assert.That(Goals.NextLabel.text, Does.StartWith("Next goal $"), where);
            // CheckLayout runs the shell layout assertions: 44 pt targets, text not squeezed, clipping contained, focus scrolled into view.
            yield return CheckLayout(rig, host, "goal line next goal");
            Assert.That(Goals.NextLabel.worldBound.width, Is.GreaterThan(0f), where);
        }

        [UnityTest]
        public IEnumerator TheFactorySaleLineAndTheReasonFitEveryScreenClassAtEveryTextScale(
            [ValueSource(typeof(MarketPanelTestSupport), nameof(Screens))] ShellScreen screen,
            [ValueSource(typeof(MarketPanelTestSupport), nameof(Percents))] int percent)
        {
            var run = GoalSession(out var runCommands, 1_000, Quarters(), Flat());
            yield return Open(run, runCommands, screen, percent);
            Assert.That(Goals.NextLabel.text, Is.EqualTo(GoalsLinePanel.SaleText));
            Assert.That(Goals.IsAllReachedShown, Is.True);
            yield return CheckLayout(rig, host, "goal line Factory Sale");
        }

        [UnityTest]
        public IEnumerator TheUnavailableReasonShowsTheWarningBesideItsTextAtEveryScreenClass(
            [ValueSource(typeof(MarketPanelTestSupport), nameof(Screens))] ShellScreen screen,
            [ValueSource(typeof(MarketPanelTestSupport), nameof(Percents))] int percent)
        {
            var prestige = new PrestigeDef(1_000, "sqrt", 10, 1_000, new ThresholdDef(500_000_000_000_000_000L, 2_000_000, 5));
            var run = GoalSession(out var runCommands, 100, Quarters(), prestige, prestigeCount: 2);
            yield return Open(run, runCommands, screen, percent);
            Assert.That(Goals.IsReasonShown, Is.True);
            ContractsPanelTestSupport.AssertIconBesideText(Goals.ReasonLabel, PanelIcons.Warning, $"{screen} @{percent}%");
            ContractsPanelTestSupport.AssertTextButtonsHaveNoChildren(Goals.Root, $"{screen} @{percent}%");
        }

        [UnityTest]
        public IEnumerator TheGoalLineIsTheFirstFocusableControlOfMarketAndKeepsItsFocusAcrossARefresh()
        {
            var run = GoalSession(out var runCommands, 100, Quarters(), Flat());
            yield return Open(run, runCommands);
            var order = rig.Shell.GetFocusOrder().Where(control => host.Root.Contains(control)).ToList();
            Assert.That(order.First(), Is.SameAs(Goals.ExactButton), "reading order: the goal line comes first");

            Goals.ExactButton.Focus();
            yield return Change(150);
            Assert.That(Goals.ExactButton.focusController.focusedElement, Is.SameAs(Goals.ExactButton), "updated in place, focus kept");
            Assert.That(Goals.NextLabel.text, Does.Contain("(60 %)"));
        }

        // ---------------------------------------------------------------------------------------------------
        // Prestige threshold (prestige-threshold)
        // ---------------------------------------------------------------------------------------------------

        private PrestigePresenter OpenPrestige(RunSession run, RunCommands runCommands, ShellScreen screen = null, int percent = 100, ShellDeviceProfile? profile = null)
        {
            session = run;
            commands = runCommands;
            rig = ShellRig.Create(screen ?? ShellScreens.All().First(), profile, percent);
            rigs.Add(rig);
            var panel = new PrestigePresenter(run, runCommands, Tree("PrestigePanel"), Styles("PrestigePanel"), Icon);
            rig.Shell.Panels.Register(panel);
            return panel;
        }

        private static PrestigeDef Rising() => new PrestigeDef(1_000_000, "sqrt", 10, 10_000_000, new ThresholdDef(1_000_000, 2_000_000, 3));

        [UnityTest]
        public IEnumerator ThePrestigePanelShowsTheCurrentAndNextThresholdAndTheIndex()
        {
            var run = GoalSession(out var runCommands, 500, new MilestoneDef[0], Rising(), prestigeCount: 1);
            var panel = OpenPrestige(run, runCommands);
            yield return rig.Layout();
            rig.Shell.SelectTab(ShellTab.Prestige);
            yield return ShellRig.Settle();

            var preview = runCommands.GetPrestigePreview().Value;
            Assert.That(preview.Threshold, Is.EqualTo(2_000_000));
            Assert.That(preview.NextThreshold, Is.EqualTo(4_000_000));
            Assert.That(preview.PrestigeIndex, Is.EqualTo(1));
            Assert.That(panel.ThresholdLabel.name, Is.EqualTo("prestige-threshold"));
            Assert.That(panel.ThresholdLabel.text, Is.EqualTo("Needed to sell: " + QuantityFormatter.Format(2_000_000) + ". Next sale needs " +
                QuantityFormatter.Format(4_000_000) + ". Factory Sale #2."));
            Assert.That(panel.ThresholdLabel.tooltip, Is.EqualTo("Needed to sell 2,000,000, next sale needs 4,000,000, Factory Sale #2"));
            Assert.That(PrestigePresenter.ThresholdExact(preview, run.Permanent.PrestigeCount), Does.Contain("4,000,000").And.Contain("Factory Sale #2"));
            Assert.That(preview.MissingFactoryValue, Is.EqualTo(2_000_000 - preview.FactoryValue.Total), "the missing value is measured against the current threshold");
            Assert.That(panel.MissingLabel.text, Does.Contain(QuantityFormatter.Format(preview.MissingFactoryValue)));
        }

        [UnityTest]
        public IEnumerator AtTheMaximumIndexTheNextThresholdIsTheSameValue()
        {
            // Index 3 is the maximum: the count keeps rising and so does the sale number (#8 after 7 prestiges); the threshold and the index stay put (D-K).
            var run = GoalSession(out var runCommands, 500, new MilestoneDef[0], Rising(), prestigeCount: 7);
            var panel = OpenPrestige(run, runCommands);
            yield return rig.Layout();
            rig.Shell.SelectTab(ShellTab.Prestige);
            yield return ShellRig.Settle();

            var preview = runCommands.GetPrestigePreview().Value;
            Assert.That(preview.PrestigeIndex, Is.EqualTo(3));
            Assert.That(preview.Threshold, Is.EqualTo(8_000_000));
            Assert.That(preview.NextThreshold, Is.EqualTo(preview.Threshold));
            Assert.That(panel.ThresholdLabel.text, Is.EqualTo("Needed to sell: " + QuantityFormatter.Format(8_000_000) + ". Next sale needs " +
                QuantityFormatter.Format(8_000_000) + ". Factory Sale #8."));
            Assert.That(panel.ThresholdLabel.tooltip, Does.EndWith("Factory Sale #8"), "the exact text keeps counting beyond the maximum index");
            Assert.That(PrestigePresenter.ThresholdText(preview, 8), Does.EndWith("Factory Sale #9."));
        }

        [UnityTest]
        public IEnumerator TheThresholdLineFitsEveryScreenClassAtEveryTextScale(
            [ValueSource(typeof(MarketPanelTestSupport), nameof(Screens))] ShellScreen screen,
            [ValueSource(typeof(MarketPanelTestSupport), nameof(Percents))] int percent)
        {
            var run = GoalSession(out var runCommands, 500, new MilestoneDef[0], Rising(), prestigeCount: 1);
            var panel = OpenPrestige(run, runCommands, screen, percent);
            yield return rig.Layout();
            rig.Shell.SelectTab(ShellTab.Prestige);
            yield return ShellRig.Settle();
            RunShellLayoutAssertions.AssertShellLayout(rig, "prestige threshold line");
            Assert.That(panel.ThresholdLabel.worldBound.width, Is.GreaterThan(0f), $"{screen} @{percent}%");
        }

        private static PrestigeDef WorstRising() =>
            new PrestigeDef(987_654_321_000L, "sqrt", 10, 10_000_000, new ThresholdDef(987_654_321_000L, 1_500_000, 5));

        [UnityTest]
        public IEnumerator HoverOnThePrestigeThresholdAndTheConfirmButtonShowTheExactNextThresholdAndIndex()
        {
            var screen = ShellScreens.All().First();
            var run = GoalSession(out var runCommands, 500, new MilestoneDef[0], WorstRising(), prestigeCount: 2);
            var panel = OpenPrestige(run, runCommands, screen, 100, ShellDeviceProfile.Pointer(screen.Width, screen.Height, 96f));
            yield return rig.Layout();
            rig.Shell.SelectTab(ShellTab.Prestige);
            yield return ShellRig.Settle();
            var preview = runCommands.GetPrestigePreview().Value;
            var current = QuantityFormatter.FormatExact(preview.Threshold);
            var next = QuantityFormatter.FormatExact(preview.NextThreshold);
            Assert.That(preview.PrestigeIndex, Is.EqualTo(2));
            Assert.That(next, Is.Not.EqualTo(current), "the fixture grows, so current and next differ");

            Send(panel.ThresholdLabel, PointerEnterEvent.GetPooled(), UnityEngine.UIElements.PointerType.mouse);
            yield return null;
            Assert.That(rig.Shell.DetailText, Does.Contain(current).And.Contain("next sale needs " + next).And.Contain("Factory Sale #3"));

            panel.ConfirmButton.Focus();
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.DetailText, Does.Contain(current + " needed").And.Contain("Next sale needs " + next).And.Contain("Factory Sale #3"),
                "the exact text of the Confirm control carries the next threshold and the index too");
        }

        [UnityTest]
        public IEnumerator OnATouchDeviceATapAndALongPressOnThePrestigeThresholdShowTheExactValues()
        {
            var screen = ShellScreens.All().First();
            var run = GoalSession(out var runCommands, 500, new MilestoneDef[0], WorstRising(), prestigeCount: 2);
            var panel = OpenPrestige(run, runCommands, screen, 100, ShellDeviceProfile.Touch(screen.Width, screen.Height, 400f));
            yield return rig.Layout();
            rig.Shell.SelectTab(ShellTab.Prestige);
            yield return ShellRig.Settle();
            var preview = runCommands.GetPrestigePreview().Value;
            var next = QuantityFormatter.FormatExact(preview.NextThreshold);

            Send(panel.ThresholdLabel, ClickEvent.GetPooled(), UnityEngine.UIElements.PointerType.touch);
            yield return null;
            Assert.That(rig.Shell.IsDetailPinned, Is.True);
            Assert.That(rig.Shell.DetailText, Does.Contain("next sale needs " + next).And.Contain("Factory Sale #3"));

            Send(panel.ThresholdLabel, ClickEvent.GetPooled(), UnityEngine.UIElements.PointerType.touch);
            yield return null;
            Assert.That(rig.Shell.IsDetailPinned, Is.False);
            Send(panel.ThresholdLabel, PointerDownEvent.GetPooled(), UnityEngine.UIElements.PointerType.touch);
            yield return new WaitForSeconds(RunShellDocument.LongPressSeconds + 0.3f);
            Assert.That(rig.Shell.IsDetailPinned, Is.True, "a long press pins it");
            Assert.That(rig.Shell.DetailText, Does.Contain("next sale needs " + next));
        }

        [UnityTest]
        public IEnumerator TheWorstCaseThresholdLineFitsEveryScreenClassAtEveryTextScale(
            [ValueSource(typeof(MarketPanelTestSupport), nameof(Screens))] ShellScreen screen,
            [ValueSource(typeof(MarketPanelTestSupport), nameof(Percents))] int percent)
        {
            // Index 4 of 5 at 987,654,321,000 x 1.5^n: 12-digit current and next values and the longest copy.
            var run = GoalSession(out var runCommands, 999_999, new MilestoneDef[0], WorstRising(), prestigeCount: 4);
            var panel = OpenPrestige(run, runCommands, screen, percent);
            yield return rig.Layout();
            rig.Shell.SelectTab(ShellTab.Prestige);
            yield return ShellRig.Settle();
            Assert.That(panel.ThresholdLabel.text, Does.Contain("Next sale needs").And.Contain("Factory Sale #5."), $"{screen} @{percent}%");
            RunShellLayoutAssertions.AssertShellLayout(rig, "prestige worst-case threshold line");
            Assert.That(panel.ThresholdLabel.worldBound.width, Is.GreaterThan(0f), $"{screen} @{percent}%");
        }
    }
}
#endif
