using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

#if UNITY_EDITOR
using UnityEditor;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T113 (FR-009, FR-011, US1 scenarios 2 and 5, UX-2, UX-3, UX-10, F-4): the machine panel of an occupied slot. Normal, empty,
    /// disabled and error states through the application seams; keyboard activation; the Remove confirmation (refund, Factory Value
    /// change, focus behaviour, rotation cancel); the auto-sell blocker copy and its toggle; 44 pt targets; the worst-case layout
    /// at every screen class, rotation and 200 % text scale.
    /// </summary>
    public sealed class MachinePanelTests
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

        private static ShellScreen PhoneScreen() =>
            ShellScreens.All().First(screen => screen.Name.StartsWith("phone-portrait 390", StringComparison.Ordinal));

        private IEnumerator Open(ShellScreen screen, int percent, long cash, bool unlockEverything = false, int factoryLevel = 1)
        {
            session = RunShellFixtures.NewSession(out commands, factoryLevel, cash, unlockEverything);
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelRoot + "MachinePanel.uxml");
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(PanelRoot + "MachinePanel.uss");
            Assert.That(tree, Is.Not.Null);
            Assert.That(styles, Is.Not.Null);
            panel = new MachinePanelPresenter(session, commands, tree, styles, ContractsPanelTestSupport.Icon);
            rig.Shell.Panels.Register(panel);
            yield return rig.Layout();
        }

        private IEnumerator ShowSlot(int slot, SlotCondition condition = SlotCondition.Occupied)
        {
            Assert.That(rig.Shell.OpenSlot(slot, condition), Is.True);
            yield return ShellRig.Settle();
        }

        private Guid Build(int slot, string machineId)
        {
            var result = commands.Build(slot, machineId);
            Assert.That(result.IsSuccess, Is.True, result.ReasonCode);
            return result.Value.InstanceId;
        }

        private string Text(string name) => panel.Root.Q<Label>(name).text;

        private bool Shown(string name) => panel.Root.Q<VisualElement>(name).style.display != DisplayStyle.None;

        private static void Send(VisualElement target, EventBase evt)
        {
            using (evt)
            {
                evt.target = target;
                target.SendEvent(evt);
            }
        }

        private static void Press(VisualElement control) => Send(control, NavigationSubmitEvent.GetPooled());

        private MachineSnapshot MachineAt(int slot) => RunSnapshots.Factory(session).Machines.First(machine => machine.SlotIndex == slot);

        /// <summary>A Smelter whose Iron Ore is sold as fast as a Miner makes it: the missing input is caused by auto-sell (UX-10).</summary>
        private void BuildAutoSoldSmelterRun()
        {
            Build(0, "machine.miner");
            Build(1, "machine.smelter");
            Assert.That(commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            Assert.That(session.Advance(25_000L * Millisecond).IsSuccess, Is.True);
        }

        // ---------------------------------------------------------------------------------------------------
        // Normal
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ThePanelShowsLevelRateProgressAndTheNextUpgrade()
        {
            yield return Open(PhoneScreen(), 100, 200);
            Build(0, "machine.miner");
            yield return ShowSlot(0);

            Assert.That(rig.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Machine));
            Assert.That(Text("machine-name"), Is.EqualTo("Miner - level 1 of 100"));
            Assert.That(Text("machine-status"), Is.EqualTo("Status: Running"));
            Assert.That(Text("machine-rate-out"), Does.StartWith("Makes: Iron Ore ").And.Contain("/min"));
            Assert.That(Text("machine-rate-in"), Is.EqualTo("Uses: nothing"));
            Assert.That(Shown("machine-batch"), Is.False, "no batch below the event bound");
            Assert.That(Text("machine-progress-text"), Is.EqualTo("Cycle progress: 0%"));
            Assert.That(Text("machine-upgrade-text"), Does.StartWith("Level 2: cost ").And.Contain("Cash").And.Contain("Makes: Iron Ore"));
            Assert.That(Text("machine-upgrade-text"), Does.Not.Contain("Milestone"));
            Assert.That(panel.UpgradeButton.text, Is.EqualTo("Upgrade to level 2"));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.UpgradeButton), Is.False);
            Assert.That(panel.ActiveButton.text, Is.EqualTo("Switch off"));
            Assert.That(panel.RemoveButton.text, Is.EqualTo("Remove"));
            Assert.That(panel.Root.Q<Label>("machine-empty").style.display, Is.EqualTo(new StyleEnum<DisplayStyle>(DisplayStyle.None)));
        }

        [UnityTest]
        public IEnumerator CycleProgressFollowsTheSimulationWithoutAFullRebuild()
        {
            yield return Open(PhoneScreen(), 100, 200);
            Build(0, "machine.miner");
            yield return ShowSlot(0);
            Assert.That(session.Advance(1_000L * Millisecond).IsSuccess, Is.True);
            panel.Refresh();
            Assert.That(Text("machine-progress-text"), Is.EqualTo("Cycle progress: 50%"));
            var fill = panel.Root.Q<VisualElement>("machine-progress-fill");
            Assert.That(fill.style.width.value.value, Is.EqualTo(50f).Within(0.5f));
            Assert.That(fill.style.width.value.unit, Is.EqualTo(LengthUnit.Percent));
        }

        [UnityTest]
        public IEnumerator AMilestoneUpgradeNamesTheMilestone()
        {
            yield return Open(PhoneScreen(), 100, 1_000_000L);
            var id = Build(0, "machine.miner");
            for (var level = 2; level <= 9; level++)
            {
                Assert.That(commands.Upgrade(id).IsSuccess, Is.True);
            }

            yield return ShowSlot(0);
            Assert.That(Text("machine-upgrade-text"), Does.Contain("Milestone at level 10: speed x2."));
        }

        [UnityTest]
        public IEnumerator UpgradeThroughTheKeyboardRaisesTheLevelAndChargesTheCost()
        {
            yield return Open(PhoneScreen(), 100, 200);
            Build(0, "machine.miner");
            yield return ShowSlot(0);
            var cashBefore = session.ReadCash();
            panel.UpgradeButton.Focus();
            yield return null;
            Press(panel.UpgradeButton.focusController.focusedElement as VisualElement);
            Assert.That(MachineAt(0).Level, Is.EqualTo(2));
            Assert.That(session.ReadCash(), Is.LessThan(cashBefore));
            Assert.That(Text("machine-name"), Is.EqualTo("Miner - level 2 of 100"));
            Assert.That(Shown("machine-upgrade-feedback"), Is.False);
        }

        [UnityTest]
        public IEnumerator TheActiveToggleSwitchesTheMachineOffAndOnAndKeepsProgress()
        {
            yield return Open(PhoneScreen(), 100, 200);
            var id = Build(0, "machine.miner");
            yield return ShowSlot(0);
            Assert.That(session.Advance(500L * Millisecond).IsSuccess, Is.True);
            panel.Refresh();
            Press(panel.ActiveButton);
            Assert.That(MachineAt(0).IsActive, Is.False);
            Assert.That(panel.ActiveButton.text, Is.EqualTo("Switch on"));
            Assert.That(Text("machine-status"), Is.EqualTo("Status: Switched off"));
            Assert.That(Text("machine-blocker-text-0"), Is.EqualTo("This machine is switched off."));
            Assert.That(Text("machine-progress-text"), Is.EqualTo("Cycle progress: 25%"), "progress is kept");

            Assert.That(panel.BlockerButtons.Count, Is.EqualTo(1));
            Assert.That(panel.BlockerButtons[0].text, Is.EqualTo("Switch on"));
            Press(panel.BlockerButtons[0]);
            Assert.That(MachineAt(0).IsActive, Is.True, "the corrective action switches the machine on");
            Assert.That(Text("machine-status"), Is.EqualTo("Status: Running"));
            Assert.That(panel.BlockerButtons.Count, Is.EqualTo(0));
            Assert.That(MachineAt(0).InstanceId, Is.EqualTo(id));
        }

        // ---------------------------------------------------------------------------------------------------
        // Disabled and error
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AnUnaffordableUpgradeIsDisabledWithItsReasonAndPressingItRejectsNextToTheControl()
        {
            yield return Open(PhoneScreen(), 100, RunShellFixtures.MinerCost + 5);
            Build(0, "machine.miner");
            yield return ShowSlot(0);

            Assert.That(RunShellDocument.IsSoftDisabled(panel.UpgradeButton), Is.True);
            Assert.That(panel.UpgradeButton.focusable, Is.True, "a disabled-looking control stays focusable");
            Assert.That(Shown("machine-upgrade-reason"), Is.True);
            Assert.That(Text("machine-upgrade-reason"), Does.StartWith("Not enough Cash. Need "));

            Press(panel.UpgradeButton);
            Assert.That(Shown("machine-upgrade-feedback"), Is.True);
            Assert.That(Text("machine-upgrade-feedback"), Is.EqualTo(RunRejectionCopy.For(RunReasons.CashInsufficient)));
            Assert.That(MachineAt(0).Level, Is.EqualTo(1), "a rejection changes nothing");
            Assert.That(session.ReadCash(), Is.EqualTo(5), "the Cash left after the Miner is below the upgrade cost");
        }

        [UnityTest]
        public IEnumerator RejectionTextsShowTheWarningBesideThemAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent, RunShellFixtures.MinerCost + 5);
            Build(0, "machine.miner");
            yield return ShowSlot(0);
            var where = $"{screen} @{percent}%";

            ContractsPanelTestSupport.AssertIconBesideText(panel.Root.Q<Label>("machine-upgrade-reason"), PanelIcons.Warning, where + " upgrade reason");
            Press(panel.UpgradeButton);
            yield return ShellRig.Settle();
            ContractsPanelTestSupport.AssertIconBesideText(panel.Root.Q<Label>("machine-upgrade-feedback"), PanelIcons.Warning, where + " upgrade rejection");
            ContractsPanelTestSupport.AssertTextButtonsHaveNoChildren(panel.Root, where);
            AssertShellLayout(rig, "machine panel with warning icons");
        }

        [UnityTest]
        public IEnumerator AtTheMaximumLevelTheUpgradeIsDisabledAndTheBatchSizeIsShown()
        {
            yield return Open(PhoneScreen(), 100, 1_000_000_000_000L);
            var id = Build(0, "machine.miner");
            for (var level = 2; level <= 100; level++)
            {
                Assert.That(commands.Upgrade(id).IsSuccess, Is.True, "level " + level);
            }

            yield return ShowSlot(0);
            Assert.That(Text("machine-name"), Is.EqualTo("Miner - level 100 of 100"));
            Assert.That(Text("machine-upgrade-text"), Is.EqualTo("Maximum level reached."));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.UpgradeButton), Is.True);
            Assert.That(Text("machine-upgrade-reason"), Is.EqualTo(RunRejectionCopy.For(RunReasons.LevelMax)));
            Assert.That(Shown("machine-batch"), Is.True, "the effective rate beyond the event bound shows its batch size");
            Assert.That(Text("machine-batch"), Does.StartWith("Batches of ").And.Contain("per cycle"));

            Press(panel.UpgradeButton);
            Assert.That(Text("machine-upgrade-feedback"), Is.EqualTo(RunRejectionCopy.For(RunReasons.LevelMax)));
            Assert.That(MachineAt(0).Level, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator ASlotWithoutAMachineShowsTheEmptyStateAndNoControlActs()
        {
            yield return Open(PhoneScreen(), 100, 200);
            yield return ShowSlot(2);
            Assert.That(panel.Root.Q<Label>("machine-empty").style.display, Is.EqualTo(new StyleEnum<DisplayStyle>(DisplayStyle.Flex)));
            Assert.That(Text("machine-empty"), Is.EqualTo("There is no machine in this slot."));
            Assert.That(panel.Root.Q<VisualElement>("machine-body").style.display, Is.EqualTo(new StyleEnum<DisplayStyle>(DisplayStyle.None)));
            Press(panel.UpgradeButton);
            Press(panel.RemoveButton);
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(session.ReadCash(), Is.EqualTo(200));
        }

        // ---------------------------------------------------------------------------------------------------
        // UX-10: a missing input caused by auto-sell
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AMissingInputCausedByAutoSellNamesItAndOffersTheToggle()
        {
            yield return Open(PhoneScreen(), 100, 5_000, true);
            BuildAutoSoldSmelterRun();
            yield return ShowSlot(1);

            Assert.That(Text("machine-name"), Does.StartWith("Smelter"));
            var blocker = Text("machine-blocker-text-0");
            Assert.That(blocker, Does.Contain("Missing Iron Ore"));
            Assert.That(blocker, Does.Contain("auto-sell is selling it"));
            Assert.That(panel.BlockerButtons.Count, Is.EqualTo(1));
            Assert.That(panel.BlockerButtons[0].text, Is.EqualTo("Stop auto-selling Iron Ore"));

            Press(panel.BlockerButtons[0]);
            var economy = RunSnapshots.Economy(session).Resources.First(resource => resource.ResourceId == "iron.ore");
            Assert.That(economy.AutoSell, Is.False, "the toggle stops auto-selling Iron Ore");
            Assert.That(Text("machine-blocker-text-0"), Does.Not.Contain("auto-sell"), "the blocker no longer blames auto-sell");
            Assert.That(Text("machine-blocker-text-0"), Does.StartWith("Missing Iron Ore: needs"));
        }

        [UnityTest]
        public IEnumerator AMissingInputWithoutAutoSellOffersABuildHintForTheProducer()
        {
            yield return Open(PhoneScreen(), 100, 5_000, true);
            Build(1, "machine.smelter");
            yield return ShowSlot(1);
            Assert.That(Text("machine-blocker-text-0"), Does.StartWith("Missing Iron Ore: needs"));
            Assert.That(Text("machine-blocker-text-0"), Does.Not.Contain("auto-sell"));
            var hint = panel.Root.Q<Label>("machine-blocker-hint-0");
            Assert.That(hint, Is.Not.Null);
            Assert.That(hint.text, Is.EqualTo("Build a Miner to make Iron Ore."));
        }

        // ---------------------------------------------------------------------------------------------------
        // T249 (FR-032, SC-007): a state change never moves a control (the store oscillating at 100 %)
        // ---------------------------------------------------------------------------------------------------

        private bool StorageFullNow()
        {
            var machine = MachineAt(0);
            var diagnosis = RunSnapshots.Rates(session).Machines.First(item => item.InstanceId == machine.InstanceId);
            return diagnosis.Blockers.Any(blocker => blocker.Kind == MachineBlocker.StorageFull);
        }

        private void FillTheStore()
        {
            Assert.That(commands.SetAutoSell("iron.ore", false).IsSuccess, Is.True);
            for (var i = 0; i < 400 && !StorageFullNow(); i++)
            {
                Assert.That(session.Advance(25_000L * Millisecond).IsSuccess, Is.True);
            }

            Assert.That(StorageFullNow(), Is.True, "the Miner fills the store");
        }

        private void EmptyTheStore()
        {
            Assert.That(commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            Assert.That(session.Advance(1_000L * Millisecond).IsSuccess, Is.True);
            Assert.That(commands.SetAutoSell("iron.ore", false).IsSuccess, Is.True);
            Assert.That(StorageFullNow(), Is.False, "selling made room");
        }

        private static readonly string[] LayoutProbes =
        {
            "machine-body", "machine-name", "machine-status", "machine-rate-out", "machine-rate-in", "machine-progress-bar", "machine-progress-text",
            "machine-last-batch", "machine-blockers", "machine-upgrade-section", "machine-upgrade", "machine-active", "machine-remove",
        };

        private Dictionary<string, Rect> LayoutRects()
        {
            var rects = new Dictionary<string, Rect>();
            foreach (var name in LayoutProbes)
            {
                var element = panel.Root.Q<VisualElement>(name);
                rects[name] = element.resolvedStyle.display == DisplayStyle.None ? Rect.zero : element.worldBound;
            }

            rects["panel-root"] = panel.Root.worldBound;
            return rects;
        }

        private static string Delta(Dictionary<string, Rect> before, Dictionary<string, Rect> after)
        {
            var text = new System.Text.StringBuilder();
            foreach (var pair in before)
            {
                var now = after[pair.Key];
                if (Mathf.Abs(now.y - pair.Value.y) > 0.5f || Mathf.Abs(now.height - pair.Value.height) > 0.5f || Mathf.Abs(now.x - pair.Value.x) > 0.5f || Mathf.Abs(now.width - pair.Value.width) > 0.5f)
                {
                    text.Append(pair.Key).Append(" y ").Append(pair.Value.y).Append("->").Append(now.y).Append(" h ").Append(pair.Value.height).Append("->").Append(now.height).Append("; ");
                }
            }

            return text.ToString();
        }

        [UnityTest]
        public IEnumerator AStoreOscillatingAtFullNeverMovesAControl([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent, 5_000, true);
            Build(0, "machine.miner");
            FillTheStore();
            yield return ShowSlot(0);
            panel.Refresh();
            yield return ShellRig.Settle();
            Assert.That(Text("machine-blocker-text-0"), Does.StartWith("Storage is full"));
            var full = LayoutRects();
            var fullText = Text("machine-blocker-text-0");

            for (var round = 0; round < 3; round++)
            {
                EmptyTheStore();
                panel.Refresh();
                yield return ShellRig.Settle();
                Assert.That(panel.Root.Q<VisualElement>("machine-blocker-text-0"), Is.Null.Or.Property("text").Not.StartWith("Storage is full"));
                var producing = LayoutRects();
                var producingDelta = Delta(full, producing);
                if (producingDelta.Length > 0)
                {
                    Debug.Log($"T249 layout delta full->producing {screen} @{percent}%: {producingDelta}");
                }

                Assert.That(producingDelta, Is.Empty, $"{screen} @{percent}%: producing moved a control (round {round})");

                FillTheStore();
                panel.Refresh();
                yield return ShellRig.Settle();
                var again = LayoutRects();
                var againDelta = Delta(full, again);
                if (againDelta.Length > 0)
                {
                    Debug.Log($"T249 layout delta producing->full {screen} @{percent}%: {againDelta}");
                }

                Assert.That(againDelta, Is.Empty, $"{screen} @{percent}%: Storage full moved a control (round {round})");
                Assert.That(Text("machine-blocker-text-0"), Does.StartWith("Storage is full"), "the blocker text still matches the snapshot");
                Assert.That(Text("machine-blocker-text-0"), Is.Not.Empty.And.Not.Null);
                Assert.That(Text("machine-blocker-text-0").Length, Is.EqualTo(fullText.Length).Within(8));
                Assert.That(panel.BlockerButtons.Select(button => button.text), Does.Contain("Upgrade storage"), "the non-color cue and its corrective action stay");
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Remove behind the shell's confirmation dialog (UX-2, UX-9)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator RemoveAsksFirstShowingTheRefundAndTheFactoryValueChangeAndOpensOnCancel()
        {
            yield return Open(PhoneScreen(), 100, 200);
            Build(0, "machine.miner");
            yield return ShowSlot(0);
            var machine = MachineAt(0);
            Assert.That(machine.RefundOnRemove, Is.EqualTo(RunShellFixtures.MinerCost / 2), "half of the invested Cash (removalRefundPpm 500,000)");
            panel.RemoveButton.Focus();
            yield return null;

            Press(panel.RemoveButton);
            yield return ShellRig.Settle();
            var shell = rig.Shell;
            Assert.That(shell.IsConfirmationOpen, Is.True);
            Assert.That(MachineAt(0).Level, Is.EqualTo(1), "nothing is removed before the confirmation");
            var message = shell.DialogLayer.Q<Label>(className: "run-dialog__message").text;
            Assert.That(message, Does.Contain("You get " + QuantityFormatter.Format(machine.RefundOnRemove) + " Cash back"));
            Assert.That(message, Does.Contain("Factory Value changes by -" + QuantityFormatter.Format(-machine.FactoryValueDeltaOnRemove)));
            Assert.That(shell.DialogCancelButton.focusController.focusedElement, Is.SameAs(shell.DialogCancelButton), "the dialog opens on Cancel");
        }

        [UnityTest]
        public IEnumerator CancellingReturnsFocusToRemoveAndKeepsTheMachine()
        {
            yield return Open(PhoneScreen(), 100, 200);
            Build(0, "machine.miner");
            yield return ShowSlot(0);
            var cash = session.ReadCash();
            panel.RemoveButton.Focus();
            yield return null;
            Press(panel.RemoveButton);
            yield return ShellRig.Settle();

            Press(rig.Shell.DialogCancelButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(panel.RemoveButton.focusController.focusedElement, Is.SameAs(panel.RemoveButton), "focus returns to the invoking Remove button");
            Assert.That(RunSnapshots.Factory(session).Machines.Count, Is.EqualTo(1));
            Assert.That(session.ReadCash(), Is.EqualTo(cash));
        }

        [UnityTest]
        public IEnumerator EscapeAndAndroidBackCancelTheRemoveConfirmation()
        {
            yield return Open(PhoneScreen(), 100, 200);
            Build(0, "machine.miner");
            yield return ShowSlot(0);
            panel.RemoveButton.Focus();
            yield return null;
            Press(panel.RemoveButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.HandleBack(), Is.True);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(panel.RemoveButton.focusController.focusedElement, Is.SameAs(panel.RemoveButton));
            Assert.That(RunSnapshots.Factory(session).Machines.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ARotationCancelsTheRemoveWithoutRemovingAndReturnsFocus()
        {
            yield return Open(PhoneScreen(), 100, 200);
            Build(0, "machine.miner");
            yield return ShowSlot(0);
            panel.RemoveButton.Focus();
            yield return null;
            Press(panel.RemoveButton);
            yield return ShellRig.Settle();

            Assert.That(rig.Shell.CancelConfirmationForRotation(), Is.True);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.NoticeText, Is.EqualTo(RunShellDocument.RotationCancelledNotice));
            Assert.That(RunSnapshots.Factory(session).Machines.Count, Is.EqualTo(1), "a rotation never confirms");
            Assert.That(panel.RemoveButton.focusController.focusedElement, Is.SameAs(panel.RemoveButton));
        }

        [UnityTest]
        public IEnumerator ConfirmingRemovesTheMachineRefundsAndFreesTheSlot()
        {
            yield return Open(PhoneScreen(), 100, 200);
            Build(0, "machine.miner");
            yield return ShowSlot(0);
            var cash = session.ReadCash();
            Press(panel.RemoveButton);
            yield return ShellRig.Settle();
            Press(rig.Shell.DialogConfirmButton);
            yield return ShellRig.Settle();

            Assert.That(RunSnapshots.Factory(session).Machines.Count, Is.EqualTo(0));
            Assert.That(session.ReadCash(), Is.EqualTo(cash + RunShellFixtures.MinerCost / 2), "half of the invested Cash comes back");
            Assert.That(RunSnapshots.Factory(session).Slots[0].Status, Is.EqualTo(SlotStatus.Empty));
            Assert.That(rig.Shell.NoticeText, Does.Contain("Removed Miner"));
            Assert.That(panel.Root.Q<Label>("machine-empty").style.display, Is.EqualTo(new StyleEnum<DisplayStyle>(DisplayStyle.Flex)));
        }

        [UnityTest]
        public IEnumerator ARemoveThatTheCommandRejectsShowsItsReasonNextToRemove()
        {
            yield return Open(PhoneScreen(), 100, 200);
            var id = Build(0, "machine.miner");
            yield return ShowSlot(0);
            Press(panel.RemoveButton);
            yield return ShellRig.Settle();
            // The machine disappears while the dialog is open (another route removed it).
            Assert.That(commands.Remove(id, true).IsSuccess, Is.True);
            Press(rig.Shell.DialogConfirmButton);
            yield return ShellRig.Settle();
            Assert.That(Shown("machine-remove-feedback") || panel.Root.Q<VisualElement>("machine-body").style.display == DisplayStyle.None, Is.True);
            Assert.That(RunSnapshots.Factory(session).Machines.Count, Is.EqualTo(0));
        }

        // ---------------------------------------------------------------------------------------------------
        // Keyboard and targets
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryPanelControlIsInTheFocusOrderAndTakesFocus()
        {
            yield return Open(PhoneScreen(), 100, 5_000, true);
            BuildAutoSoldSmelterRun();
            yield return ShowSlot(1);
            var order = rig.Shell.GetFocusOrder().ToList();
            var controls = new List<Button> { panel.UpgradeButton, panel.ActiveButton, panel.RemoveButton };
            controls.AddRange(panel.BlockerButtons);
            foreach (var control in controls)
            {
                Assert.That(order, Does.Contain(control), control.name);
                control.Focus();
                yield return null;
                Assert.That(control.focusController.focusedElement, Is.SameAs(control), control.name);
            }
        }

        [UnityTest]
        public IEnumerator EveryActionableControlIsAtLeast44PointsAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen, 100, 5_000, true);
            BuildAutoSoldSmelterRun();
            yield return ShowSlot(1);
            var buttons = new List<Button> { panel.UpgradeButton, panel.ActiveButton, panel.RemoveButton };
            buttons.AddRange(panel.BlockerButtons);
            foreach (var button in buttons)
            {
                Assert.That(button.worldBound.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{screen}: {button.name} width");
                Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{screen}: {button.name} height");
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout: worst-case content at every screen class (UX-3, F-4)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheWorstCasePanelIsContainedAtEveryScreenClassAndTextScale([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            // Cash for the Miner and the Smelter plus one: the Smelter upgrade stays unaffordable, so the cash rejection can be shown.
            yield return Open(screen, percent, RunShellFixtures.MinerCost + RunShellFixtures.BuildCost("machine.smelter") + 1, true);
            BuildAutoSoldSmelterRun();
            // The 25 s of auto-sold Ore earn more than a cheap upgrade costs: spend until the next Smelter upgrade is unaffordable.
            var smelterId = MachineAt(1).InstanceId;
            while (commands.Upgrade(smelterId).IsSuccess)
            {
            }

            yield return ShowSlot(1);

            // Worst case: an auto-sell blocker with its action, an unaffordable-upgrade reason and every rejection shown.
            Press(panel.UpgradeButton);
            Assert.That(Shown("machine-upgrade-feedback"), Is.True, "the cash rejection is shown next to Upgrade");
            commands.SetActive(MachineAt(1).InstanceId, false);
            panel.Refresh();
            yield return ShellRig.Settle();
            AssertShellLayout(rig, "machine panel with blockers and rejections");

            var scroll = rig.Shell.PanelScroll;
            var viewport = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport");
            var range = Mathf.Max(0f, scroll.contentContainer.layout.height - viewport.layout.height);
            foreach (var offset in new[] { 0f, 0.5f, 1f })
            {
                scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, range * offset);
                yield return ShellRig.Settle();
                AssertScrollContentClipped(scroll, $"{screen} @{percent}% machine panel scrolled to {offset:P0}");
            }

            var buttons = new List<Button> { panel.UpgradeButton, panel.ActiveButton, panel.RemoveButton };
            buttons.AddRange(panel.BlockerButtons);
            var safe = rig.SafeRect();
            foreach (var button in new[] { buttons.Last(), buttons.First(), buttons[buttons.Count / 2] })
            {
                scroll.scrollOffset = new Vector2(0f, button == buttons.Last() ? 0f : float.MaxValue);
                yield return ShellRig.Settle();
                button.Focus();
                yield return ShellRig.Settle();
                Assert.That(button.focusController.focusedElement, Is.SameAs(button), $"{screen} @{percent}%: '{button.name}' did not take focus");
                AssertWithin(viewport.worldBound, button.worldBound, $"{screen} @{percent}%: focused '{button.name}' is not scrolled into view");
                AssertWithin(safe, button.worldBound, $"{screen} @{percent}%: focused '{button.name}' leaves the safe area");
            }

            if (percent >= 200)
            {
                Assert.That(range, Is.GreaterThan(0f), $"{screen}: the machine panel scrolls at 200 %");
            }
        }

        [UnityTest]
        public IEnumerator TheRemoveDialogWithTheFullMessageStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent, 5_000);
            Build(0, "machine.miner");
            yield return ShowSlot(0);
            panel.RemoveButton.Focus();
            yield return null;
            Press(panel.RemoveButton);
            yield return ShellRig.Settle();
            var shell = rig.Shell;
            var safe = rig.SafeRect();
            var card = shell.DialogCard.worldBound;
            AssertWithin(safe, card, $"{screen} @{percent}%: the dialog card stays inside the safe area");
            AssertWithin(card, shell.DialogCancelButton.worldBound, $"{screen} @{percent}%: Cancel stays in the card");
            AssertWithin(card, shell.DialogConfirmButton.worldBound, $"{screen} @{percent}%: Confirm stays in the card");
            AssertTextAndTargets(shell.DialogLayer, safe, $"{screen} @{percent}% remove dialog", MinimumFontSize * percent / 100f);
        }
    }
}
#endif
