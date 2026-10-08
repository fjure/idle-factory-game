using System;
using System.Collections.Generic;
using System.Text;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// The machine panel of an occupied slot (T113, FR-009, FR-011, US1 scenarios 2 and 5, UX-10): level, effective rate with the
    /// batch size beyond the event bound, cycle progress, blockers with corrective actions, the next upgrade, the active toggle
    /// and Remove behind the shell's confirmation dialog. It reads <see cref="RunSnapshots.Factory"/>, <see cref="RunSnapshots.Rates"/>
    /// and the allocation-free machine states, and mutates only through <see cref="RunCommands"/>. Every rejected command shows its
    /// reason code's copy next to the control that was used and changes no state.
    /// </summary>
    public sealed class MachinePanelPresenter : IShellPanel
    {
        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly Func<string, Sprite> icons;
        private readonly VisualElement root;
        private readonly Label emptyLabel;
        private readonly VisualElement body;
        private readonly Label nameLabel;
        private readonly Label statusLabel;
        private readonly Label rateOutLabel;
        private readonly Label rateInLabel;
        private readonly Label batchLabel;
        private readonly VisualElement progressFill;
        private readonly Label progressText;
        private readonly Label lastBatchLabel;
        private readonly VisualElement blockers;
        private readonly Label blockerFeedback;
        private readonly Label upgradeText;
        private readonly Label upgradeReason;
        private readonly Label upgradeFeedback;
        private readonly Label activeFeedback;
        private readonly Label removeFeedback;
        private MachineStateView[] states = new MachineStateView[64];
        private ShellPanelContext context;
        private int slot = -1;
        private long structureRevision = -1;
        private long quantityRevision = -1;
        private bool dirty = true;
        private string blockerSignature = string.Empty;
        private MachineSnapshot machine;

        public MachinePanelPresenter(RunSession session, RunCommands commands, VisualTreeAsset tree, StyleSheet styles, Func<string, Sprite> icons = null)
        {
            this.icons = icons;
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            if (tree == null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            root = tree.Instantiate();
            root.name = "machine-panel-root";
            root.style.flexShrink = 0;
            if (styles != null)
            {
                root.styleSheets.Add(styles);
            }

            emptyLabel = root.Q<Label>("machine-empty");
            body = root.Q<VisualElement>("machine-body");
            nameLabel = root.Q<Label>("machine-name");
            statusLabel = root.Q<Label>("machine-status");
            rateOutLabel = root.Q<Label>("machine-rate-out");
            rateInLabel = root.Q<Label>("machine-rate-in");
            batchLabel = root.Q<Label>("machine-batch");
            progressFill = root.Q<VisualElement>("machine-progress-fill");
            progressText = root.Q<Label>("machine-progress-text");
            lastBatchLabel = root.Q<Label>("machine-last-batch");
            blockers = root.Q<VisualElement>("machine-blockers");
            blockerFeedback = root.Q<Label>("machine-blocker-feedback");
            upgradeText = root.Q<Label>("machine-upgrade-text");
            UpgradeButton = root.Q<Button>("machine-upgrade");
            upgradeReason = root.Q<Label>("machine-upgrade-reason");
            upgradeFeedback = root.Q<Label>("machine-upgrade-feedback");
            ActiveButton = root.Q<Button>("machine-active");
            activeFeedback = root.Q<Label>("machine-active-feedback");
            RemoveButton = root.Q<Button>("machine-remove");
            removeFeedback = root.Q<Label>("machine-remove-feedback");
            UpgradeButton.clicked += OnUpgradeClicked;
            ActiveButton.clicked += OnActiveClicked;
            RemoveButton.clicked += OnRemoveClicked;
            foreach (var rejection in new[] { blockerFeedback, upgradeFeedback, activeFeedback, removeFeedback, upgradeReason })
            {
                PanelIcons.Mark(rejection, icons, PanelIcons.Warning);
            }

            foreach (var feedback in new[] { blockerFeedback, upgradeFeedback, activeFeedback, removeFeedback, upgradeReason, lastBatchLabel, batchLabel })
            {
                feedback.style.display = DisplayStyle.None;
            }
        }

        public string Id => ShellPanelIds.Machine;
        public string Title => "Machine";
        public VisualElement Root => root;

        public Button UpgradeButton { get; }
        public Button ActiveButton { get; }
        public Button RemoveButton { get; }

        /// <summary>The slot shown, or -1 while the panel is hidden.</summary>
        public int Slot => slot;

        /// <summary>The corrective-action buttons currently shown with the blockers.</summary>
        public IReadOnlyList<Button> BlockerButtons => blockers.Query<Button>().ToList();

        public void OnShown(ShellPanelContext shown)
        {
            context = shown;
            slot = shown.SlotIndex;
            ClearFeedback();
            dirty = true;
            blockerSignature = string.Empty;
            Refresh();
        }

        public void OnHidden()
        {
            slot = -1;
            context = null;
        }

        public void Refresh()
        {
            if (slot < 0)
            {
                return;
            }

            if (dirty || structureRevision != session.StructureRevision || quantityRevision != session.QuantityRevision)
            {
                dirty = false;
                structureRevision = session.StructureRevision;
                quantityRevision = session.QuantityRevision;
                RenderStatic();
            }

            UpdateProgress();
        }

        // ------------------------------------------------------------------------------------------------------
        // Rendering
        // ------------------------------------------------------------------------------------------------------

        private void RenderStatic()
        {
            var snapshot = RunSnapshots.Factory(session);
            machine = null;
            if (slot < snapshot.Slots.Count && snapshot.Slots[slot].Status == SlotStatus.Occupied)
            {
                machine = snapshot.Machines[snapshot.Slots[slot].MachineIndex];
            }

            emptyLabel.style.display = machine == null ? DisplayStyle.Flex : DisplayStyle.None;
            body.style.display = machine == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (machine == null)
            {
                return;
            }

            nameLabel.text = machine.Label + " - level " + machine.Level + " of " + machine.MaxLevel;
            var rates = machine.Rates;
            SlotSheetText.SetText(rateOutLabel, "Makes: " + SlotSheetText.Flow(session, rates?.Outputs));
            SlotSheetText.SetText(rateInLabel, "Uses: " + SlotSheetText.Flow(session, rates?.Inputs));
            if (rates != null && rates.BatchFactor > 1)
            {
                SlotSheetText.SetText(
                    batchLabel,
                    "Batches of " + QuantityFormatter.Format(rates.BatchFactor) + " per cycle: the machine is faster than the shortest cycle, so each cycle makes more.");
            }
            else
            {
                SlotSheetText.SetText(batchLabel, string.Empty);
            }

            RenderStatus(machine.Phase, machine.IsActive);
            var rateSnapshot = RunSnapshots.Rates(session);
            MachineDiagnosis diagnosis = null;
            for (var i = 0; i < rateSnapshot.Machines.Count; i++)
            {
                if (rateSnapshot.Machines[i].InstanceId == machine.InstanceId)
                {
                    diagnosis = rateSnapshot.Machines[i];
                    break;
                }
            }

            RenderLastBatch(diagnosis);
            RenderBlockers(diagnosis);
            RenderUpgrade(machine, snapshot.Cash);
            ActiveButton.text = machine.IsActive ? "Switch off" : "Switch on";
            RemoveButton.text = "Remove";
        }

        private void RenderStatus(MachinePhase phase, bool active)
        {
            string text;
            if (!active)
            {
                text = "Switched off";
            }
            else
            {
                switch (phase)
                {
                    case MachinePhase.Running: text = "Running"; break;
                    case MachinePhase.Waiting: text = "Waiting: the finished cycle cannot be delivered yet"; break;
                    default: text = "Paused: the machine cannot run now"; break;
                }
            }

            statusLabel.text = "Status: " + text;
        }

        private void RenderLastBatch(MachineDiagnosis diagnosis)
        {
            var last = diagnosis?.LastCompletion ?? default;
            if (last.IsPartial)
            {
                var limit = last.LimitKind == CompletionLimitKind.Input
                    ? SlotSheetText.ResourceLabel(session, last.LimitResourceId) + " ran short"
                    : last.LimitKind == CompletionLimitKind.Store ? "the store was full" : "a limit";
                SlotSheetText.SetText(
                    lastBatchLabel,
                    "Last batch: " + QuantityFormatter.Format(last.Units) + " of " + QuantityFormatter.Format(last.BatchFactor) + " because " + limit + ".");
            }
            else
            {
                SlotSheetText.SetText(lastBatchLabel, string.Empty);
            }
        }

        private void RenderBlockers(MachineDiagnosis diagnosis)
        {
            var infos = diagnosis?.Blockers;
            var builder = new StringBuilder();
            if (infos != null)
            {
                for (var i = 0; i < infos.Count; i++)
                {
                    var info = infos[i];
                    builder.Append(info.Kind).Append('|').Append(info.ResourceId).Append('|').Append(info.AutoSold)
                        .Append('|').Append(info.Required).Append('|').Append(info.Available).Append('|').Append(info.NetRequired)
                        .Append('|').Append(info.Free);
                    for (var a = 0; a < info.Actions.Count; a++)
                    {
                        var action = info.Actions[a];
                        builder.Append('|').Append((int)action.Kind).Append(':').Append(action.ResourceId).Append(':').Append(action.MachineId);
                    }

                    builder.Append(';');
                }
            }

            var signature = builder.ToString();
            if (signature == blockerSignature)
            {
                return;
            }

            blockerSignature = signature;
            blockers.Clear();
            if (infos == null)
            {
                return;
            }

            for (var i = 0; i < infos.Count; i++)
            {
                blockers.Add(CreateBlocker(infos[i], i));
            }
        }

        private VisualElement CreateBlocker(MachineBlockerInfo info, int index)
        {
            var row = new VisualElement { name = "machine-blocker-" + index };
            row.AddToClassList("mpanel__blocker");
            var resource = SlotSheetText.ResourceLabel(session, info.ResourceId);
            string text;
            switch (info.Kind)
            {
                case MachineBlocker.Inactive:
                    text = "This machine is switched off.";
                    break;
                case MachineBlocker.MissingInput when info.AutoSold:
                    // UX-10: the missing input is caused by auto-sell, so say so and offer the toggle.
                    text = "Missing " + resource + ": auto-sell is selling it before this machine can use it (needs " +
                        QuantityFormatter.Format(info.Required) + ", has " + QuantityFormatter.Format(info.Available) + ").";
                    break;
                case MachineBlocker.MissingInput:
                    text = "Missing " + resource + ": needs " + QuantityFormatter.Format(info.Required) + ", has " +
                        QuantityFormatter.Format(info.Available) + ".";
                    break;
                case MachineBlocker.StorageFull:
                    text = "Storage is full: one cycle needs room for " + QuantityFormatter.Format(info.NetRequired) + " more units, only " +
                        QuantityFormatter.Format(info.Free) + " free.";
                    break;
                default:
                    text = "The machine is blocked.";
                    break;
            }

            var label = new Label(text) { name = "machine-blocker-text-" + index };
            label.AddToClassList("mpanel__blocker-text");
            row.Add(label);
            for (var a = 0; a < info.Actions.Count; a++)
            {
                var action = info.Actions[a];
                var actionResource = SlotSheetText.ResourceLabel(session, action.ResourceId);
                if (action.Kind == CorrectiveActionKind.BuildProducer)
                {
                    var machineLabel = action.MachineId != null && session.Catalog.Machines.TryGetValue(action.MachineId, out var def) ? def.Label : "machine";
                    var hint = new Label("Build a " + machineLabel + " to make " + actionResource + ".") { name = "machine-blocker-hint-" + index };
                    hint.AddToClassList("mpanel__blocker-hint");
                    row.Add(hint);
                    continue;
                }

                string buttonText;
                switch (action.Kind)
                {
                    case CorrectiveActionKind.ActivateMachine: buttonText = "Switch on"; break;
                    case CorrectiveActionKind.DisableAutoSell: buttonText = "Stop auto-selling " + actionResource; break;
                    case CorrectiveActionKind.EnableAutoSell: buttonText = "Auto-sell " + actionResource; break;
                    case CorrectiveActionKind.UpgradeStorage: buttonText = "Upgrade storage"; break;
                    default: buttonText = "Fix"; break;
                }

                var button = RunShellDocument.CreateButton("machine-fix-" + index + "-" + a, buttonText);
                button.AddToClassList("mpanel__button");
                var captured = action;
                button.clicked += () => OnCorrectiveAction(captured);
                row.Add(button);
            }

            return row;
        }

        private void RenderUpgrade(MachineSnapshot current, long cash)
        {
            var next = current.NextUpgrade;
            if (next == null || !next.Available)
            {
                upgradeText.text = "Maximum level reached.";
                UpgradeButton.text = "Upgrade";
                RunShellDocument.SetSoftDisabled(UpgradeButton, true);
                SlotSheetText.SetText(upgradeReason, RunRejectionCopy.For(RunReasons.LevelMax));
                return;
            }

            var builder = new StringBuilder();
            builder.Append("Level ").Append(next.NewLevel).Append(": cost ").Append(QuantityFormatter.Format(next.Cost)).Append(" Cash. Makes: ")
                .Append(SlotSheetText.Flow(session, next.NewRates?.Outputs)).Append('.');
            if (next.ReachesMilestone)
            {
                builder.Append(" Milestone at level ").Append(next.MilestoneLevel).Append(": speed ")
                    .Append(SlotSheetText.Multiplier(next.MilestoneMultiplierPpm)).Append('.');
            }

            upgradeText.text = builder.ToString();
            UpgradeButton.text = "Upgrade to level " + next.NewLevel;
            RunShellDocument.SetSoftDisabled(UpgradeButton, !next.Affordable);
            SlotSheetText.SetText(upgradeReason, next.Affordable ? string.Empty : SlotSheetText.CashShortfall(next.Cost, cash));
        }

        private void UpdateProgress()
        {
            if (machine == null)
            {
                return;
            }

            var count = session.CopyMachineStates(states);
            if (count > states.Length)
            {
                states = new MachineStateView[count];
                count = session.CopyMachineStates(states);
            }

            for (var i = 0; i < count; i++)
            {
                if (states[i].SlotIndex != slot)
                {
                    continue;
                }

                var ppm = Math.Max(0L, Math.Min(1_000_000L, states[i].ProgressPpm));
                var percent = ppm / 10_000L;
                progressFill.style.width = Length.Percent(ppm / 10_000f);
                var text = "Cycle progress: " + percent + "%";
                if (progressText.text != text)
                {
                    progressText.text = text;
                }

                var status = states[i];
                if (status.Phase != machine.Phase || status.IsActive != machine.IsActive || status.Level != machine.Level || status.Blocker != machine.Blocker)
                {
                    dirty = true;
                }

                return;
            }
        }

        // ------------------------------------------------------------------------------------------------------
        // Commands (each shows its rejection next to the control that was used)
        // ------------------------------------------------------------------------------------------------------

        private void ClearFeedback()
        {
            foreach (var label in new[] { blockerFeedback, upgradeFeedback, activeFeedback, removeFeedback })
            {
                SlotSheetText.SetText(label, string.Empty);
            }
        }

        private void Finish()
        {
            dirty = true;
            Refresh();
        }

        private void OnUpgradeClicked()
        {
            if (machine == null)
            {
                return;
            }

            ClearFeedback();
            var result = commands.Upgrade(machine.InstanceId);
            if (!result.IsSuccess)
            {
                SlotSheetText.SetText(upgradeFeedback, RunRejectionCopy.For(result.ReasonCode));
            }

            Finish();
        }

        private void OnActiveClicked()
        {
            if (machine == null)
            {
                return;
            }

            ClearFeedback();
            var result = commands.SetActive(machine.InstanceId, !machine.IsActive);
            if (!result.IsSuccess)
            {
                SlotSheetText.SetText(activeFeedback, RunRejectionCopy.For(result.ReasonCode));
            }

            Finish();
        }

        private void OnCorrectiveAction(CorrectiveAction action)
        {
            ClearFeedback();
            string reason = null;
            switch (action.Kind)
            {
                case CorrectiveActionKind.ActivateMachine:
                {
                    var result = commands.SetActive(action.InstanceId == Guid.Empty && machine != null ? machine.InstanceId : action.InstanceId, true);
                    reason = result.IsSuccess ? null : result.ReasonCode;
                    break;
                }

                case CorrectiveActionKind.DisableAutoSell:
                case CorrectiveActionKind.EnableAutoSell:
                {
                    var result = commands.SetAutoSell(action.ResourceId, action.Kind == CorrectiveActionKind.EnableAutoSell);
                    reason = result.IsSuccess ? null : result.ReasonCode;
                    break;
                }

                case CorrectiveActionKind.UpgradeStorage:
                {
                    var result = commands.UpgradeStorage();
                    reason = result.IsSuccess ? null : result.ReasonCode;
                    break;
                }
            }

            if (reason != null)
            {
                SlotSheetText.SetText(blockerFeedback, RunRejectionCopy.For(reason));
            }

            Finish();
        }

        private void OnRemoveClicked()
        {
            if (machine == null || context == null)
            {
                return;
            }

            ClearFeedback();
            var target = machine;
            var refund = QuantityFormatter.Format(target.RefundOnRemove);
            var change = QuantityFormatter.Format(-target.FactoryValueDeltaOnRemove);
            var opened = context.Shell.RequestConfirmation(new ConfirmationRequest
            {
                Title = "Remove " + target.Label + "?",
                Message = "You get " + refund + " Cash back. Factory Value changes by -" + change + ". The machine and its cycle progress are lost.",
                ConfirmLabel = "Remove",
                CancelLabel = "Keep machine",
                Destructive = true,
                Invoker = RemoveButton,
                OnConfirm = () => ConfirmRemove(target),
            });
            if (!opened)
            {
                SlotSheetText.SetText(removeFeedback, RunRejectionCopy.For(RunReasons.LifecycleBlocked));
            }
        }

        private void ConfirmRemove(MachineSnapshot target)
        {
            var result = commands.Remove(target.InstanceId, true);
            if (result.IsSuccess)
            {
                context?.Shell.ShowNotice(
                    "Removed " + target.Label + ". Refunded " + QuantityFormatter.Format(result.Value.Refund) + " Cash.",
                    ShellNoticeKind.Info);
            }
            else
            {
                SlotSheetText.SetText(removeFeedback, RunRejectionCopy.For(result.ReasonCode));
            }

            Finish();
        }
    }
}
