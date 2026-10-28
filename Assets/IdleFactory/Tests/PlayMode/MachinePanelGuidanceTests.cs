using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T365 (playtest F-B3, FR-011, US1/US3): the machine panel shows blocked guidance and fix buttons only for the selected
    /// machine's current blocked reason. A running machine shows none, and switching the selection from a blocked machine to a
    /// running one clears the previous slot's text and buttons. Every screen class at 100 % and 200 % text.
    /// </summary>
    public sealed class MachinePanelGuidanceTests
    {
        private const string PanelRoot = "Assets/IdleFactory/Unity/UI/Panels/";
        private const long Millisecond = RunSession.TicksPerMs;
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private RunSession session;
        private RunCommands commands;
        private MachinePanelPresenter panel;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<int> Percents() => new[] { 100, 200 };

        [TearDown]
        public void TearDown()
        {
            foreach (var item in rigs)
            {
                item.Dispose();
            }

            rigs.Clear();
            rig = null;
            panel = null;
        }

        private IEnumerator Open(ShellScreen screen, int percent)
        {
            session = RunShellFixtures.NewSession(out commands, 1, 5_000, true);
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelRoot + "MachinePanel.uxml");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelRoot + "MachinePanel.uss");
            panel = new MachinePanelPresenter(session, commands, tree, styles, ContractsPanelTestSupport.Icon);
            rig.Shell.Panels.Register(panel);
            yield return rig.Layout();
        }

        private IEnumerator ShowSlot(int slot)
        {
            Assert.That(rig.Shell.OpenSlot(slot, SlotCondition.Occupied), Is.True);
            yield return ShellRig.Settle();
            panel.Refresh();
        }

        private void Build(int slot, string machineId)
        {
            var result = commands.Build(slot, machineId);
            Assert.That(result.IsSuccess, Is.True, result.ReasonCode);
        }

        private MachineSnapshot MachineAt(int slot) => RunSnapshots.Factory(session).Machines.First(machine => machine.SlotIndex == slot);

        private void AssertNoGuidance(string context)
        {
            Assert.That(panel.Root.Q<VisualElement>("machine-blocker-text-0"), Is.Null, context + ": no blocked text");
            Assert.That(panel.BlockerButtons.Count, Is.EqualTo(0), context + ": no fix buttons");
            Assert.That(panel.Root.Q<VisualElement>("machine-blockers").childCount, Is.EqualTo(0), context + ": no guidance rows");
        }

        [UnityTest]
        public IEnumerator SelectingARunningMachineAfterABlockedOneClearsTheMissingInputGuidance(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            Build(0, "machine.miner");
            Build(1, "machine.smelter");
            yield return ShowSlot(1);
            Assert.That(panel.Root.Q<Label>("machine-blocker-text-0").text, Does.StartWith("Missing Iron Ore"), "the blocked Smelter explains itself");

            yield return ShowSlot(0);
            Assert.That(MachineAt(0).Phase, Is.EqualTo(MachinePhase.Running));
            AssertNoGuidance($"{screen} @{percent}%: running Miner after blocked Smelter");

            yield return ShowSlot(1);
            Assert.That(panel.Root.Q<Label>("machine-blocker-text-0").text, Does.StartWith("Missing Iron Ore"), "the blocked Smelter explains itself again");
        }

        [UnityTest]
        public IEnumerator TheBuildAMinerHintAppearsOnlyWhileNoProducerOfTheResourceIsOwned(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            Build(1, "machine.smelter");
            yield return ShowSlot(1);
            var ore = session.Catalog.Resources["iron.ore"].Label;
            var miner = session.Catalog.Machines["machine.miner"].Label;
            Assert.That(panel.Root.Q<Label>("machine-blocker-text-0").text, Does.StartWith("Missing " + ore));
            Assert.That(panel.Root.Q<Label>("machine-blocker-hint-0")?.text, Is.EqualTo("Build a " + miner + " to make " + ore + "."), "no Miner stands yet");

            Build(0, "machine.miner");
            panel.Refresh();
            yield return ShellRig.Settle();
            panel.Refresh();
            Assert.That(panel.Root.Q<Label>("machine-blocker-text-0").text, Does.StartWith("Missing " + ore), $"{screen} @{percent}%: the reason stays");
            Assert.That(panel.Root.Q<Label>("machine-blocker-hint-0"), Is.Null, $"{screen} @{percent}%: a Miner is owned, so no 'Build a Miner' hint");
        }

        // FR-030 expected target: Upgrade if visible and enabled, otherwise the sheet's close control.
        private VisualElement ExpectedStableTarget() =>
            panel.UpgradeButton.resolvedStyle.display != DisplayStyle.None && !RunShellDocument.IsSoftDisabled(panel.UpgradeButton)
                ? (VisualElement)panel.UpgradeButton
                : rig.Shell.CloseButton;

        private void AssertFocusStable(VisualElement expected, string context)
        {
            var focused = ContractsPanelTestSupport.Focused(rig.Shell.Root);
            Assert.That(focused, Is.SameAs(expected), context + ": focus on the FR-030 target");
            Assert.That(focused.panel, Is.Not.Null, context + ": attached");
            Assert.That(focused.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None), context + ": visible");
            Assert.That(focused.focusable, Is.True, context + ": focusable");
            Assert.That(focused, Is.Not.SameAs(panel.ActiveButton), context + ": never the Active toggle");
        }

        private static bool IsAutoSellFix(Button button) => button.text.StartsWith("Stop auto-selling", StringComparison.Ordinal);

        private IEnumerator FocusAutoSellFix(bool build = true)
        {
            if (build)
            {
                Build(1, "machine.smelter");
            }

            Assert.That(commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            yield return ShowSlot(1);
            var fix = panel.BlockerButtons.FirstOrDefault(IsAutoSellFix);
            Assert.That(fix, Is.Not.Null, "auto-sell is selling the Ore the Smelter needs");
            fix.Focus();
            Assert.That(ContractsPanelTestSupport.Focused(rig.Shell.Root), Is.SameAs(fix));
            ContractsPanelTestSupport.Submit(fix);
            yield return ShellRig.Settle();
            Assert.That(panel.BlockerButtons.Any(IsAutoSellFix), Is.False, "the fix removed its own row");
        }

        private IEnumerator OpenWithCash(long cash)
        {
            session = RunShellFixtures.NewSession(out commands, 1, cash, true);
            rig = ShellRig.Create(ShellScreens.All().First(), null, 100);
            rigs.Add(rig);
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelRoot + "MachinePanel.uxml");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelRoot + "MachinePanel.uss");
            panel = new MachinePanelPresenter(session, commands, tree, styles, ContractsPanelTestSupport.Icon);
            rig.Shell.Panels.Register(panel);
            yield return rig.Layout();
        }

        /// <summary>
        /// T450 re-baseline (FR-030, R-20). Old expectation: after a fix button disappears, focus is on ActiveButton (the Switch off toggle,
        /// which repeated Enter would flip). New: Upgrade if visible and enabled, else the sheet's close control; never the Active toggle.
        /// </summary>
        [UnityTest]
        public IEnumerator UsingAFixButtonMovesFocusToTheFr030TargetInsteadOfLosingIt(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            yield return FocusAutoSellFix();
            AssertFocusStable(ExpectedStableTarget(), $"{screen} @{percent}%");
        }

        [UnityTest]
        public IEnumerator FocusAfterAFixGoesToTheCloseControlWhenUpgradeIsNotEnabled()
        {
            yield return OpenWithCash(5_000);
            Build(1, "machine.smelter");
            // Drain the cash so the upgrade of the selected Smelter is unaffordable (soft-disabled).
            var drain = 0;
            while (drain++ < 200 && commands.Upgrade(MachineAt(1).InstanceId).IsSuccess)
            {
            }

            yield return FocusAutoSellFix(false);
            Assert.That(RunShellDocument.IsSoftDisabled(panel.UpgradeButton), Is.True, "Upgrade is disabled");
            AssertFocusStable(rig.Shell.CloseButton, "unaffordable Upgrade");
        }

        [UnityTest]
        public IEnumerator FocusAtMaxLevelGoesToTheCloseControl()
        {
            yield return OpenWithCash(1_000_000_000_000_000L);
            Build(1, "machine.smelter");
            var guard = 0;
            while (guard++ < 200 && commands.Upgrade(MachineAt(1).InstanceId).IsSuccess)
            {
            }

            Assert.That(MachineAt(1).Level, Is.EqualTo(MachineAt(1).MaxLevel), "the Smelter is at max level");
            Assert.That(commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            yield return ShowSlot(1);
            var fix = panel.BlockerButtons.First(IsAutoSellFix);
            fix.Focus();
            ContractsPanelTestSupport.Submit(fix);
            yield return ShellRig.Settle();
            Assert.That(RunShellDocument.IsSoftDisabled(panel.UpgradeButton), Is.True, "Upgrade is disabled at max level");
            AssertFocusStable(rig.Shell.CloseButton, "max level");
        }

        [UnityTest]
        public IEnumerator FocusAfterGuidanceClearsGoesToTheFr030TargetAndRepeatedEnterNeverFlipsActive()
        {
            yield return Open(ShellScreens.All().First(), 100);
            Build(1, "machine.smelter");
            Assert.That(commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            yield return ShowSlot(1);
            var fix = panel.BlockerButtons.First(IsAutoSellFix);
            fix.Focus();
            // The guidance changes without the fix button: auto-sell is switched off elsewhere, so the fix row is rebuilt without it.
            Assert.That(commands.SetAutoSell("iron.ore", false).IsSuccess, Is.True);
            panel.Refresh();
            yield return ShellRig.Settle();
            Assert.That(panel.BlockerButtons.Count, Is.EqualTo(0), "the guidance cleared");
            AssertFocusStable(ExpectedStableTarget(), "guidance cleared");
            Assert.That(MachineAt(1).IsActive, Is.True);
            for (var i = 0; i < 3; i++)
            {
                ContractsPanelTestSupport.Submit(ContractsPanelTestSupport.Focused(rig.Shell.Root));
                yield return ShellRig.Settle();
                Assert.That(MachineAt(1).IsActive, Is.True, "repeated Enter never switches the machine off");
            }
        }

        [UnityTest]
        public IEnumerator FocusWhenTheSelectionEmptiesGoesToAVisibleShellControl()
        {
            yield return Open(ShellScreens.All().First(), 100);
            Build(1, "machine.smelter");
            yield return ShowSlot(1);
            panel.ActiveButton.Focus();
            Assert.That(commands.Remove(MachineAt(1).InstanceId, true).IsSuccess, Is.True);
            yield return ShellRig.Settle();
            panel.Refresh();
            yield return ShellRig.Settle();
            var focused = ContractsPanelTestSupport.Focused(rig.Shell.Root);
            Assert.That(focused, Is.Not.Null, "focus is not lost");
            Assert.That(focused, Is.Not.SameAs(panel.ActiveButton));
            Assert.That(focused.panel, Is.Not.Null);
            Assert.That(focused.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(focused == rig.Shell.CloseButton || focused == rig.Shell.TabButton(ShellTab.Factory), Is.True, "the shell default target");
        }

        [UnityTest]
        public IEnumerator TheHintIsDecidedByResourceNotByMachineTypeAndReturnsWhenTheProducerIsRemoved()
        {
            yield return Open(ShellScreens.All().First(), 100);
            Build(1, "machine.smelter");
            Build(2, "machine.smelter");
            yield return ShowSlot(1);
            var ore = session.Catalog.Resources["iron.ore"].Label;
            Assert.That(panel.Root.Q<Label>("machine-blocker-hint-0"), Is.Not.Null, "two Smelters make no Ore, so the hint shows");

            Build(0, "machine.miner");
            panel.Refresh();
            yield return ShellRig.Settle();
            panel.Refresh();
            Assert.That(panel.Root.Q<Label>("machine-blocker-hint-0"), Is.Null, "of three machines one matches the resource: hint hidden");

            Assert.That(commands.Remove(MachineAt(0).InstanceId, true).IsSuccess, Is.True);
            yield return ShellRig.Settle();
            panel.Refresh();
            yield return ShellRig.Settle();
            panel.Refresh();
            Assert.That(panel.Root.Q<Label>("machine-blocker-hint-0")?.text, Does.Contain(ore), "the hint returns after the producer is removed");
        }

        /// <summary>
        /// The shipped Iron content with a second output (Iron Ore) added to the Smelter recipe, edited as text the way BuildPricePanelTests
        /// edits the Miner price. A byproduct of the lowest tier keeps the content loader's chain-dominance validation satisfied.
        /// </summary>
        private static byte[] MultiOutputSmelterContent()
        {
            var text = System.Text.Encoding.UTF8.GetString(RunSceneRun.IronBytes());
            var pattern = new System.Text.RegularExpressions.Regex(
                @"(""id"": ""recipe\.smelt-iron-bar"",\s*""inputs"": \[[^\]]*\],\s*""outputs"": \[\s*\{[^}]*\})");
            Assert.That(pattern.IsMatch(text), Is.True, "the Smelter recipe is in the expected shape");
            var bytes = System.Text.Encoding.UTF8.GetBytes(pattern.Replace(text, "$1, { \"resourceId\": \"iron.ore\", \"amount\": 1 }", 1));
            var probe = IdleFactory.Unity.Content.GameplayContentDispatcher.Load(bytes);
            Assert.That(probe.IsSuccess, Is.True, string.Join(" | ", probe.Diagnostics.Select(d => d.Code + " " + d.Message).Distinct()));
            return bytes;
        }

        [UnityTest]
        public IEnumerator AMachineTypeWithSeveralOutputsHidesTheHintForItsSecondOutputOnly()
        {
            session = RunShellFixtures.NewSession(out commands, 1, 5_000, true, MultiOutputSmelterContent());
            rig = ShellRig.Create(ShellScreens.All().First(), null, 100);
            rigs.Add(rig);
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelRoot + "MachinePanel.uxml");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelRoot + "MachinePanel.uss");
            panel = new MachinePanelPresenter(session, commands, tree, styles, ContractsPanelTestSupport.Icon);
            rig.Shell.Panels.Register(panel);
            yield return rig.Layout();
            var smelterDef = session.Catalog.Machines["machine.smelter"];
            Assert.That(session.Catalog.Recipes[smelterDef.RecipeId].Outputs.Select(output => output.ResourceId),
                Is.EquivalentTo(new[] { "iron.bar", "iron.ore" }), "the Smelter has several outputs");

            Build(1, "machine.assembler");
            yield return ShowSlot(1);
            Assert.That(panel.Root.Q<Label>("machine-blocker-hint-0"), Is.Not.Null, "the Assembler misses Iron Plates and nothing makes them yet");

            Build(0, "machine.smelter");
            yield return ShowSlot(0);
            panel.Refresh();
            Assert.That(panel.Root.Q<Label>("machine-blocker-text-0")?.text, Does.StartWith("Missing Iron Ore"), "the lone Smelter misses Ore");
            Assert.That(panel.Root.Q<Label>("machine-blocker-hint-0"), Is.Null, "Iron Ore is the Smelter's second output, so no hint to build a producer");

            yield return ShowSlot(1);
            panel.Refresh();
            Assert.That(panel.Root.Q<Label>("machine-blocker-text-0"), Is.Not.Null, "the Assembler still explains itself");
            Assert.That(panel.Root.Q<Label>("machine-blocker-hint-0"), Is.Not.Null, "the Smelter does not make Iron Plates: the hint stays");
        }

        [UnityTest]
        public IEnumerator SelectingARunningMachineAfterAStorageFullOneClearsTheStorageGuidance(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            Build(0, "machine.miner");
            Build(1, "machine.smelter");
            Assert.That(commands.SetAutoSell("iron.ore", false).IsSuccess, Is.True);
            for (var i = 0; i < 400 && !StorageFullFor(0); i++)
            {
                Assert.That(session.Advance(25_000L * Millisecond).IsSuccess, Is.True);
            }

            Assert.That(StorageFullFor(0), Is.True, "the Miner fills the store");
            yield return ShowSlot(0);
            Assert.That(panel.Root.Q<Label>("machine-blocker-text-0").text, Does.StartWith("Storage is full"));

            yield return ShowSlot(1);
            if (MachineAt(1).Phase == MachinePhase.Running)
            {
                AssertNoGuidance($"{screen} @{percent}%: Smelter selected after full Miner (phase {MachineAt(1).Phase})");
            }
            else
            {
                // On revision 3 the Smelter cycles in 0.8 s against the Miner's 2 s, so at this instant it often waits for Ore and is not running. A machine
                // that is not running owns its guidance: the panel shows the Smelter's own blockers, never the Miner's "Storage is full" left over.
                var own = RunSnapshots.Rates(session).Machines.First(item => item.InstanceId == MachineAt(1).InstanceId).Blockers.Select(blocker => blocker.Kind).ToList();
                var text = panel.Root.Q<Label>("machine-blocker-text-0")?.text;
                Assert.That(own, Is.Not.Empty, $"{screen} @{percent}%: a Smelter that is not running has its own reason (phase {MachineAt(1).Phase})");
                Assert.That(text, Is.Not.Null, $"{screen} @{percent}%: the Smelter explains itself");
                Assert.That(text.StartsWith("Storage is full", StringComparison.Ordinal), Is.EqualTo(own.Contains(MachineBlocker.StorageFull)),
                    $"{screen} @{percent}%: the guidance is the Smelter's own, not the Miner's (\"{text}\")");
            }
        }

        [UnityTest]
        public IEnumerator ARunningMachineNeverShowsBlockedGuidanceWhileItsCycleRuns(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            Build(0, "machine.miner");
            Build(1, "machine.smelter");
            yield return ShowSlot(1);
            for (var step = 0; step < 40; step++)
            {
                Assert.That(session.Advance(1_000L * Millisecond).IsSuccess, Is.True);
                panel.Refresh();
                if (MachineAt(1).Phase == MachinePhase.Running)
                {
                    AssertNoGuidance($"{screen} @{percent}%: running Smelter at step {step}");
                }
                else
                {
                    // Not running: whatever the panel shows is covered by MachinePanelTests.
                }
            }
        }

        private bool StorageFullFor(int slot)
        {
            var machine = MachineAt(slot);
            var diagnosis = RunSnapshots.Rates(session).Machines.First(item => item.InstanceId == machine.InstanceId);
            return diagnosis.Blockers.Any(blocker => blocker.Kind == MachineBlocker.StorageFull);
        }
    }
}
#endif
