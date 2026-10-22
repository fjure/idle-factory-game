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
