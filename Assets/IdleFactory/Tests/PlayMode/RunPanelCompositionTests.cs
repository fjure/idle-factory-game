using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T194: the panels of T113 to T118 mounted into the running game. The real authored scene is started against an isolated
    /// persistence root, and the tests tap the tabs and the slots the way a player does. They prove that every panel ID is
    /// registered (never the shell placeholder), that a panel follows the run, that a shell rebuild registers every panel exactly once,
    /// that a panel which cannot be built keeps its placeholder with a coded diagnostic, and that the start-up and offline surfaces
    /// appear at the points contracts/ui-surfaces.md names.
    /// </summary>
    public sealed class RunPanelCompositionTests
    {
        private const string MinerId = "machine.miner";

        private static readonly string[] AllPanelIds =
        {
            ShellPanelIds.Build, ShellPanelIds.Machine, ShellPanelIds.MachineCard, ShellPanelIds.Market, ShellPanelIds.Contracts,
            ShellPanelIds.Items, ShellPanelIds.Research, ShellPanelIds.Prestige, ShellPanelIds.Settings,
        };

        private static readonly ShellTab[] PanelTabs =
        {
            ShellTab.Market, ShellTab.Contracts, ShellTab.Items, ShellTab.Research, ShellTab.Prestige,
        };

        private string root;
        private RunSceneClock clock;
        private RunSceneRun run;

        [SetUp]
        public void CreateRoot()
        {
            root = RunSceneRun.NewRoot();
            clock = new RunSceneClock();
            run = null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (run != null)
            {
                yield return run.Stop();
            }

            RunSceneRun.DeleteRoot(root);
        }

#if UNITY_EDITOR
        private CoreGameFoundationBootstrap Bootstrap => run.Bootstrap;

        private IEnumerator StartRun(bool quiet = false, Func<RunCatalog, IRunCheckpointStore> createRunStore = null)
        {
            run = new RunSceneRun(root, clock);
            yield return run.Start(null, quiet, null, createRunStore);
        }

        private IEnumerator StartLiveRun()
        {
            yield return StartRun();
            Assert.That(Bootstrap.IsInitialized, Is.True, Bootstrap.InitializationError);
            Assert.That(Bootstrap.RunShell, Is.Not.Null);
            Assert.That(Bootstrap.PanelDiagnostics, Is.Empty);
        }

        private static void Activate(VisualElement target)
        {
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = target;
                target.SendEvent(submit);
            }
        }

        private static VisualElement Placeholder(RunShellDocument shell) => shell.Root.Q<VisualElement>("panel-placeholder");

        private static void AssertPanelShown(RunShellDocument shell, string id, string how)
        {
            Assert.That(shell.ActivePanelId, Is.EqualTo(id), how);
            Assert.That(shell.Panels.TryGet(id, out var panel), Is.True, how + ": registered");
            Assert.That(panel.Root.style.display.value, Is.EqualTo(DisplayStyle.Flex), how + ": the panel is displayed");
            Assert.That(shell.Root.Q<VisualElement>("panel-content").Contains(panel.Root), Is.True, how + ": mounted in the shell");
            Assert.That(Placeholder(shell).style.display.value, Is.EqualTo(DisplayStyle.None), how + ": never the placeholder");
        }

        private static int TransientActionCount(CoreGameFoundationBootstrap bootstrap)
        {
            var field = typeof(CoreGameFoundationBootstrap).GetField("transientActions", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return ((System.Collections.ICollection)field.GetValue(bootstrap)).Count;
        }

        /// <summary>Clears a serialized asset reference of the bootstrap, as a scene with a missing asset would have it.</summary>
        internal static void ClearAsset(CoreGameFoundationBootstrap bootstrap, string field)
        {
            var info = typeof(CoreGameFoundationBootstrap).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, field);
            info.SetValue(bootstrap, null);
        }

        // ---------------------------------------------------------------------------------------------
        // Every tab and slot tap opens its registered panel
        // ---------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryPanelIdIsRegisteredAfterAStartWithoutDiagnostics()
        {
            yield return StartLiveRun();
            var shell = Bootstrap.RunShell;

            foreach (var id in AllPanelIds)
            {
                Assert.That(shell.Panels.IsRegistered(id), Is.True, id);
            }

            Assert.That(shell.Panels.Count, Is.EqualTo(AllPanelIds.Length));
            Assert.That(Bootstrap.StartupPanel, Is.Null, "A live run carries no start-up surface.");
        }

        [UnityTest]
        public IEnumerator EveryTabTapOpensItsRegisteredPanelAndNeverThePlaceholder()
        {
            yield return StartLiveRun();
            var shell = Bootstrap.RunShell;

            foreach (var tab in PanelTabs)
            {
                Activate(shell.TabButton(tab));
                yield return null;
                AssertPanelShown(shell, ShellPanelIds.ForTab(tab), tab + " tab");
                Assert.That(shell.PlaceholderText, Does.Not.Contain("not available"), tab.ToString());
            }
        }

        [UnityTest]
        public IEnumerator EverySlotTapOpensTheBuildSheetOrTheMachinePanelAndNeverThePlaceholder()
        {
            yield return StartLiveRun();
            var shell = Bootstrap.RunShell;

            Assert.That(Bootstrap.ShellPresenter.HandleWorldTap(Bootstrap.SlotGrid.WorldRectOf(0).center), Is.True);
            yield return null;
            AssertPanelShown(shell, ShellPanelIds.Build, "empty slot");
            Assert.That(shell.SelectedSlot, Is.EqualTo(0));

            var built = Bootstrap.RunCommands.Build(0, MinerId);
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
            yield return null;
            yield return null;
            Assert.That(shell.IsCardOpen, Is.True, "the built slot hands over to the machine card (T534)");

            shell.CloseSheet();
            yield return null;
            Assert.That(Bootstrap.ShellPresenter.HandleWorldTap(Bootstrap.SlotGrid.WorldRectOf(0).center), Is.True);
            yield return null;
            Assert.That(shell.IsCardOpen, Is.True, "an occupied slot opens the machine card (T534)");

            Activate(shell.TabButton(ShellTab.Prestige));
            yield return null;
            AssertPanelShown(shell, ShellPanelIds.Prestige, "a tab after a slot sheet");
        }

        // ---------------------------------------------------------------------------------------------
        // A panel follows the run
        // ---------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AVisiblePanelRefreshesWhenTheRunChanges()
        {
            yield return StartLiveRun();
            var shell = Bootstrap.RunShell;
            var built = Bootstrap.RunCommands.Build(0, MinerId);
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
            Assert.That(Bootstrap.ShellPresenter.OpenSlot(0), Is.True);
            Assert.That(shell.OpenDetails(0), Is.True, "the Details view is the former machine panel (T534)");
            yield return null;
            yield return null;
            AssertPanelShown(shell, ShellPanelIds.Machine, "machine panel");

            string Observed() => string.Join("|",
                shell.Root.Q<Label>("machine-status")?.text,
                shell.Root.Q<Label>("machine-progress-text")?.text,
                shell.Root.Q<Label>("machine-last-batch")?.text,
                shell.Root.Q<VisualElement>("machine-progress-fill")?.style.width.value.value);

            var before = Observed();
            Assert.That(Bootstrap.RunSession.Advance(1500L * RunSession.TicksPerMs).IsSuccess, Is.True);
            yield return null;
            yield return null;
            var after = Observed();

            Assert.That(after, Is.Not.EqualTo(before), "The visible machine panel followed 1.5 seconds of production.");
        }

        // ---------------------------------------------------------------------------------------------
        // Rebuild and failure
        // ---------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AShellRebuildDisposesAndRegistersEveryPanelExactlyOnce()
        {
            yield return StartLiveRun();
            var shell = Bootstrap.RunShell;
            var before = AllPanelIds.ToDictionary(id => id, id => { shell.Panels.TryGet(id, out var panel); return panel; });
            var actionsBefore = TransientActionCount(Bootstrap);
            Assert.That(actionsBefore, Is.EqualTo(6), "The shell dialog plus Market, Contracts, Items, Research and Prestige register a rotation cancel.");

            Assert.That(shell.Build(), Is.True, shell.Error);
            Assert.That(Bootstrap.RecomposeRunPanels(), Is.True);
            yield return null;

            Assert.That(Bootstrap.PanelDiagnostics, Is.Empty);
            Assert.That(shell.Panels.Count, Is.EqualTo(AllPanelIds.Length), "Every ID is registered exactly once.");
            foreach (var id in AllPanelIds)
            {
                Assert.That(shell.Panels.TryGet(id, out var panel), Is.True, id);
                Assert.That(panel, Is.Not.SameAs(before[id]), id + ": the old panel was replaced, not kept beside the new one");
            }

            Assert.That(TransientActionCount(Bootstrap), Is.EqualTo(actionsBefore), "The old rotation registrations were disposed.");

            // A second recomposition changes nothing in the count either.
            Assert.That(Bootstrap.RecomposeRunPanels(), Is.True);
            Assert.That(shell.Panels.Count, Is.EqualTo(AllPanelIds.Length));
            Assert.That(TransientActionCount(Bootstrap), Is.EqualTo(actionsBefore));

            foreach (var tab in PanelTabs)
            {
                Activate(shell.TabButton(tab));
                yield return null;
                AssertPanelShown(shell, ShellPanelIds.ForTab(tab), "after the rebuild, " + tab);
            }
        }

        [UnityTest]
        public IEnumerator APanelThatCannotBeBuiltKeepsItsPlaceholderAndReportsACodedDiagnostic()
        {
            yield return StartLiveRun();
            var shell = Bootstrap.RunShell;
            ClearAsset(Bootstrap, "prestigeTree");

            Assert.That(Bootstrap.RecomposeRunPanels(), Is.True);
            yield return null;

            Assert.That(Bootstrap.PanelDiagnostics, Is.EqualTo(new[]
            {
                "panel.prestige.failed: The prestige panel could not be built (ArgumentNullException).",
            }));
            Assert.That(Bootstrap.PanelDiagnostics[0], Does.Not.Contain("tree").And.Not.Contain("Assets").And.Not.Contain("\\").And.Not.Contain("/"),
                "The diagnostic carries the exception type, never its message or a path.");
            Assert.That(shell.Panels.IsRegistered(ShellPanelIds.Prestige), Is.False);
            Assert.That(shell.Panels.Count, Is.EqualTo(AllPanelIds.Length - 1), "Nothing is published for the failed panel; the others stay.");

            Activate(shell.TabButton(ShellTab.Prestige));
            yield return null;
            Assert.That(shell.PlaceholderText, Does.Contain("not available"));
            Assert.That(Placeholder(shell).style.display.value, Is.EqualTo(DisplayStyle.Flex));

            Activate(shell.TabButton(ShellTab.Market));
            yield return null;
            AssertPanelShown(shell, ShellPanelIds.Market, "the other panels are unaffected");
        }

        // ---------------------------------------------------------------------------------------------
        // Start-up and offline surfaces
        // ---------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ABlockedImportShowsTheStartupSurfaceOnTheDocumentRootAndStartNewRunMountsEveryPanel()
        {
            RunSceneRun.WriteV1Checkpoint(root, 5, clock.UtcNow.AddHours(-2));
            yield return StartRun(quiet: true);
            var bootstrap = Bootstrap;

            Assert.That(bootstrap.IsInitialized, Is.False);
            Assert.That(bootstrap.RunShell, Is.Null, "A blocked start never composes the shell.");
            var startup = bootstrap.StartupPanel;
            Assert.That(startup, Is.Not.Null);
            Assert.That(startup.Kind, Is.EqualTo(StartupStatusKind.ImportBlocked));
            Assert.That(startup.BlockedCode, Is.EqualTo("import.slots.exceeded"));
            Assert.That(startup.Root.parent, Is.SameAs(bootstrap.UiDocument.rootVisualElement), "Mounted on the UIDocument root.");
            Assert.That(startup.IsStandalone, Is.True);
            Assert.That(startup.Root.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(startup.RetryButton.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(startup.StartNewRunButton.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            // A rotation cancels the confirmation without starting anything, and V1 stays offered.
            Activate(startup.StartNewRunButton);
            Assert.That(startup.IsConfirmationOpen, Is.True);
            Assert.That(bootstrap.CancelTransientActionsForRotation(), Is.GreaterThanOrEqualTo(1));
            Assert.That(startup.IsConfirmationOpen, Is.False);
            Assert.That(bootstrap.RunPersistence.BlockedImportCode, Is.EqualTo("import.slots.exceeded"));

            Activate(startup.StartNewRunButton);
            Assert.That(startup.IsConfirmationOpen, Is.True);
            Activate(startup.ConfirmButton);
            yield return null;
            yield return null;

            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            Assert.That(bootstrap.StartupPanel, Is.Null, "The surface is gone once the run is live.");
            Assert.That(startup.Root.parent, Is.Null, "Unmounted from the document root.");
            var shell = bootstrap.RunShell;
            Assert.That(shell, Is.Not.Null);
            Assert.That(bootstrap.PanelDiagnostics, Is.Empty);
            Assert.That(shell.Panels.Count, Is.EqualTo(AllPanelIds.Length));
            Activate(shell.TabButton(ShellTab.Contracts));
            yield return null;
            AssertPanelShown(shell, ShellPanelIds.Contracts, "after the recovery");
        }

        [UnityTest]
        public IEnumerator ASaveOfAnotherVersionBlocksTheStartWithTheReturnBlockedSurfaceAndNoDiscard()
        {
            yield return StartRun(quiet: true, createRunStore: catalog => new UnsupportedVersionStore());
            var bootstrap = Bootstrap;

            Assert.That(bootstrap.IsInitialized, Is.False);
            Assert.That(bootstrap.StartupDiagnosticCode, Is.EqualTo("return.blocked"));
            Assert.That(bootstrap.RunPersistence.Lifecycle, Is.EqualTo(LifecycleState.ReturnBlocked));
            var startup = bootstrap.StartupPanel;
            Assert.That(startup, Is.Not.Null);
            Assert.That(startup.Kind, Is.EqualTo(StartupStatusKind.ReturnBlocked));
            Assert.That(startup.TitleLabel.text, Is.EqualTo(StartupStatusPanel.ReturnBlockedTitle));
            Assert.That(startup.RetryButton.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(startup.DiscardButton.style.display.value, Is.EqualTo(DisplayStyle.None),
                "A save of another build is never offered for discard.");
            Assert.That(bootstrap.RunPersistence.CanDiscardDamagedSave, Is.False);

            Activate(startup.RetryButton);
            Assert.That(startup.Kind, Is.EqualTo(StartupStatusKind.ReturnBlocked), "Retry cannot help: the save is still another version's.");
            Assert.That(bootstrap.IsInitialized, Is.False);
            Assert.That(startup.FeedbackLabel.text, Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator AReturnOpensTheOfflineSummaryBelowTheDialogLayerAndAContinueClosesIt()
        {
            yield return StartLiveRun();
            var bootstrap = Bootstrap;
            var shell = bootstrap.RunShell;
            var adapter = bootstrap.RunLifecycle;
            var screen = new LifecycleScreenConfiguration(360, 640, ScreenOrientation.Portrait);
            var realtime = 100d;
            adapter.UseScreenConfigurationSource(() => screen, () => realtime);
            Assert.That(bootstrap.RunCommands.Build(0, MinerId).IsSuccess, Is.True);

            adapter.NotifyPauseChanged(true);
            clock.UtcNow = clock.UtcNow.AddMinutes(10);
            adapter.NotifyPauseChanged(false);

            var offline = bootstrap.OfflinePanel;
            Assert.That(offline, Is.Not.Null);
            Assert.That(offline.IsOpen, Is.True);
            Assert.That(offline.Kind, Is.EqualTo(OfflineSurfaceKind.Summary));
            Assert.That(offline.Summary, Is.SameAs(bootstrap.LastOfflineSummary));
            Assert.That(offline.Root.parent, Is.SameAs(shell.Root));
            var dialog = shell.Root.Q<VisualElement>("dialog-layer");
            Assert.That(shell.Root.IndexOf(offline.Root), Is.LessThan(shell.Root.IndexOf(dialog)), "Below the shell's confirmation dialog.");

            Activate(offline.ContinueButton);
            Assert.That(offline.IsOpen, Is.False);
            Assert.That(offline.Root.parent, Is.Null, "A closed summary leaves nothing mounted.");

            // A rotation is not a departure: no summary.
            realtime += 10d;
            screen = new LifecycleScreenConfiguration(640, 360, ScreenOrientation.LandscapeLeft);
            adapter.ObserveScreenConfiguration();
            adapter.NotifyFocusChanged(false);
            clock.UtcNow = clock.UtcNow.AddMilliseconds(250);
            realtime += 0.2d;
            adapter.NotifyFocusChanged(true);
            Assert.That(offline.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator AReturnThatCannotBeCompletedOpensTheBlockedSurfaceAndRetryResumesTheHost()
        {
            var store = new SwitchableStore();
            yield return StartRun(createRunStore: catalog =>
            {
                store.Inner = new JsonRunCheckpointStore(root, catalog);
                return store;
            });
            Assert.That(Bootstrap.IsInitialized, Is.True, Bootstrap.InitializationError);
            var bootstrap = Bootstrap;
            var adapter = bootstrap.RunLifecycle;
            adapter.UseScreenConfigurationSource(() => new LifecycleScreenConfiguration(360, 640, ScreenOrientation.Portrait), () => 100d);
            Assert.That(bootstrap.RunCommands.Build(0, MinerId).IsSuccess, Is.True);

            adapter.NotifyPauseChanged(true);
            Assert.That(adapter.LastResult.IsSuccess, Is.True, adapter.LastResult.ReasonCode);
            clock.UtcNow = clock.UtcNow.AddMinutes(10);
            store.FailPromote = true;
            adapter.NotifyPauseChanged(false);

            var offline = bootstrap.OfflinePanel;
            Assert.That(adapter.LastResult.IsSuccess, Is.False);
            Assert.That(bootstrap.RunPersistence.Lifecycle, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(offline.IsOpen, Is.True);
            Assert.That(offline.Kind, Is.EqualTo(OfflineSurfaceKind.Blocked));
            Assert.That(offline.RetryButton.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            Activate(offline.RetryButton);
            Assert.That(offline.Kind, Is.EqualTo(OfflineSurfaceKind.Blocked), "Still failing: the surface stays and says so.");
            Assert.That(offline.FeedbackLabel.text, Is.EqualTo(OfflineSummaryPanel.RetryFailedText));

            store.FailPromote = false;
            Activate(offline.RetryButton);
            Assert.That(bootstrap.RunPersistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(bootstrap.RuntimeHost.enabled, Is.True, "A successful retry resumes the host.");
            Assert.That(offline.Kind, Is.EqualTo(OfflineSurfaceKind.Summary));
            Assert.That(bootstrap.LastOfflineSummary, Is.SameAs(offline.Summary));
        }

        // ---------------------------------------------------------------------------------------------
        // Stores
        // ---------------------------------------------------------------------------------------------

        /// <summary>A store that holds a save written by another build: every read fails with the unsupported-version outcome.</summary>
        private sealed class UnsupportedVersionStore : IRunCheckpointStore
        {
            private const string Message = "The saved factory was written by another version of the game.";

            public RunCheckpointReadResult ReadActive() => RunCheckpointReadResult.Rejected(CommandOutcome.UnsupportedSaveVersion, Message);

            public RunCheckpointReadResult ReadPrevious() => RunCheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, Message);

            public RunCheckpointReadResult Read(CheckpointId checkpointId) =>
                RunCheckpointReadResult.Rejected(CommandOutcome.UnsupportedSaveVersion, Message);

            public CommandResult<CheckpointId> Promote(RunCheckpoint checkpoint) =>
                CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, Message);

            public CommandResult<CheckpointId> RecoverPrevious(RunCheckpoint recoveryCandidate) =>
                CommandResult<CheckpointId>.Rejected(CommandOutcome.NoCheckpoint, Message);

            public CommandResult<long> NextSupersedeGeneration() =>
                CommandResult<long>.Rejected(CommandOutcome.UnsupportedSaveVersion, Message);

            public CommandResult<CheckpointId> SupersedeDamaged(RunCheckpoint checkpoint) =>
                CommandResult<CheckpointId>.Rejected(CommandOutcome.UnsupportedSaveVersion, Message);

            public CommandResult<RunCheckpointPresence> ProbeFiles() =>
                CommandResult<RunCheckpointPresence>.Success(RunCheckpointPresence.PublishedFiles);
        }

        /// <summary>The real Save V2 store with one switch: while <see cref="FailPromote"/> is set every promotion fails.</summary>
        private sealed class SwitchableStore : IRunCheckpointStore
        {
            public IRunCheckpointStore Inner { get; set; }
            public bool FailPromote { get; set; }

            public RunCheckpointReadResult ReadActive() => Inner.ReadActive();

            public RunCheckpointReadResult ReadPrevious() => Inner.ReadPrevious();

            public RunCheckpointReadResult Read(CheckpointId checkpointId) => Inner.Read(checkpointId);

            public CommandResult<CheckpointId> Promote(RunCheckpoint checkpoint) => FailPromote
                ? CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "The store is unavailable.")
                : Inner.Promote(checkpoint);

            public CommandResult<CheckpointId> RecoverPrevious(RunCheckpoint recoveryCandidate) => Inner.RecoverPrevious(recoveryCandidate);

            public CommandResult<long> NextSupersedeGeneration() => Inner.NextSupersedeGeneration();

            public CommandResult<CheckpointId> SupersedeDamaged(RunCheckpoint checkpoint) => Inner.SupersedeDamaged(checkpoint);

            public CommandResult<RunCheckpointPresence> ProbeFiles() => Inner.ProbeFiles();
        }
#else
        [Test]
        public void TheAuthoredSceneCompositionTestsRunInTheEditor()
        {
            Assert.Ignore("The Feature 003 composition seam is available in the Editor only.");
        }
#endif
    }
}
