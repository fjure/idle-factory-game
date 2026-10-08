using System;
using System.Collections.Generic;
using System.Globalization;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>
    /// The Items tab (T115, FR-017, FR-018, FR-032, US3 scenarios 1 and 2; contracts/ui-surfaces.md, Items). It shows the owned Item
    /// stacks by tier with their counts, the activation flow (Activate, then a target choice by machine type, then the shell's
    /// confirmation), the running effects with a countdown that ticks once per second, and the reason of an invalid or conflicting
    /// activation while the Item is kept. A rotation cancels the target choice and the confirmation without consuming anything.
    /// The panel reads <see cref="RunSnapshots.Items"/> and mutates only through <see cref="RunCommands.ActivateItem"/>.
    /// </summary>
    public sealed class ItemsPanel : IShellPanel
    {
        public const string PanelTitle = "Items";
        public const string EmptyText = "No Items yet — earn them from contracts.";

        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly Func<string, Sprite> icons;
        private readonly VisualElement emptyBox;
        private readonly Label emptyLabel;
        private readonly VisualElement effectsSection;
        private readonly VisualElement effectsList;
        private readonly List<Label> effectLabels = new List<Label>();
        private readonly VisualElement stacksSection;
        private readonly Dictionary<string, StackRow> rows = new Dictionary<string, StackRow>(StringComparer.Ordinal);
        private readonly List<StackRow> rowOrder = new List<StackRow>();
        private RunShellDocument shell;
        private long keyStructure = -1;
        private long keyQuantity = -1;
        private long keySecond = -1;
        private bool forceRefresh = true;
        private string pendingItemId;
        private bool confirmOpen;
        private ItemsSnapshot snapshot;

        /// <summary>Builds the panel. <paramref name="styles"/> is <c>ItemsPanel.uss</c>; <paramref name="icons"/> resolves a presentation key to a sprite (optional).</summary>
        public ItemsPanel(RunSession session, RunCommands commands, StyleSheet styles = null, Func<string, Sprite> icons = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.icons = icons;
            var root = new VisualElement { name = "items-panel" };
            root.AddToClassList("items-panel");
            if (styles != null)
            {
                root.styleSheets.Add(styles);
            }

            Root = root;

            // A wrapped label is wrapped in a container (wiki: UI Toolkit HUD layout pitfalls), or a bounded scroll view clamps its height.
            emptyBox = new VisualElement { name = "items-empty-box" };
            emptyBox.AddToClassList("items-card");
            emptyLabel = MakeText("items-empty", "items-line");
            emptyLabel.text = EmptyText;
            WrappedLabelFit.Attach(emptyLabel);
            emptyBox.Add(emptyLabel);
            root.Add(emptyBox);

            effectsSection = new VisualElement { name = "items-effects" };
            var effectsHeader = MakeText("items-effects-header", "items-section-title");
            effectsHeader.text = "Active effects";
            effectsList = new VisualElement { name = "items-effects-list" };
            effectsSection.Add(effectsHeader);
            effectsSection.Add(effectsList);
            root.Add(effectsSection);

            stacksSection = new VisualElement { name = "items-stacks" };
            var stacksHeader = MakeText("items-stacks-header", "items-section-title");
            stacksHeader.text = "Your Items";
            stacksSection.Add(stacksHeader);
            root.Add(stacksSection);

            foreach (var stack in RunSnapshots.Items(session).Stacks)
            {
                var row = new StackRow(stack, this);
                rows.Add(stack.ItemId, row);
                rowOrder.Add(row);
                stacksSection.Add(row.Card);
            }
        }

        public string Id => ShellPanelIds.Items;
        public string Title => PanelTitle;
        public VisualElement Root { get; }

        /// <summary>The latest snapshot the panel drew from, or null before the first refresh.</summary>
        public ItemsSnapshot Snapshot => snapshot;

        public Label EmptyLabel => emptyLabel;
        public VisualElement EffectsSection => effectsSection;
        public VisualElement StacksSection => stacksSection;
        public IReadOnlyList<Label> EffectLabels => effectLabels;

        /// <summary>The Item whose target choice is open, or null.</summary>
        public string PendingItemId => pendingItemId;

        /// <summary>True while the activation confirmation is open.</summary>
        public bool IsConfirmationOpen => confirmOpen;

        public VisualElement StackCard(string itemId) => rows[itemId].Card;
        public Label StackTitle(string itemId) => rows[itemId].Title;
        public Label StackEffect(string itemId) => rows[itemId].Effect;
        public Label StackReason(string itemId) => rows[itemId].Reason;
        public Button ActivateButton(string itemId) => rows[itemId].Activate;
        public VisualElement ChoiceContainer(string itemId) => rows[itemId].Choice;
        public Label ChoiceFeedback(string itemId) => rows[itemId].Feedback;
        public Button CancelChoiceButton(string itemId) => rows[itemId].CancelChoice;
        public IReadOnlyList<Button> TargetButtons(string itemId) => rows[itemId].TargetButtons;
        public Label TargetReason(string itemId, int index) => rows[itemId].TargetReasons[index];

        public void OnShown(ShellPanelContext context)
        {
            shell = context?.Shell;
            Invalidate();
            Refresh();
        }

        public void OnHidden()
        {
            // A pending target choice is not carried over a tab change: nothing was consumed, so closing it is lossless.
            CloseChoice(returnFocus: false);
        }

        /// <summary>Forces the next <see cref="Refresh"/> to redraw (for a state change that bumped no revision).</summary>
        public void Invalidate() => forceRefresh = true;

        /// <summary>Redraws when the structure, the quantities or the timeline second changed; the shell calls it every frame while visible.</summary>
        public void Refresh()
        {
            var second = session.TimelineUtcMs / 1000L;
            if (!forceRefresh && session.StructureRevision == keyStructure && session.QuantityRevision == keyQuantity && second == keySecond)
            {
                return;
            }

            forceRefresh = false;
            keyStructure = session.StructureRevision;
            keyQuantity = session.QuantityRevision;
            keySecond = second;
            Render(RunSnapshots.Items(session));
        }

        /// <summary>
        /// Cancels the target choice and the activation confirmation because of a rotation, without consuming the Item (UX-5, UX-9).
        /// Register it with the bootstrap's transient actions. Focus returns to the Activate control that started the flow.
        /// Returns whether something was open.
        /// </summary>
        public bool CancelForRotation()
        {
            var cancelled = false;
            if (confirmOpen && shell != null)
            {
                cancelled = shell.CancelConfirmationForRotation();
            }

            if (pendingItemId != null)
            {
                var row = rows[pendingItemId];
                CloseChoice(returnFocus: false);
                shell?.ShowNotice(RunShellDocument.RotationCancelledNotice);
                shell?.FocusWhenReady(row.Activate);
                cancelled = true;
            }

            return cancelled;
        }

        // ---------------------------------------------------------------------------------------------------
        // Rendering
        // ---------------------------------------------------------------------------------------------------

        private void Render(ItemsSnapshot data)
        {
            snapshot = data;
            SetDisplayed(emptyBox, data.TotalCount == 0);
            SetDisplayed(stacksSection, data.TotalCount > 0);

            SetDisplayed(effectsSection, data.Active.Count > 0);
            while (effectLabels.Count < data.Active.Count)
            {
                var label = MakeText("items-effect-" + effectLabels.Count, "items-line");
                effectLabels.Add(label);
                effectsList.Add(label);
            }

            for (var i = 0; i < effectLabels.Count; i++)
            {
                var visible = i < data.Active.Count;
                SetDisplayed(effectLabels[i], visible);
                if (visible)
                {
                    var effect = data.Active[i];
                    effectLabels[i].text = effect.Label + " on " + TargetLabel(effect.TargetKey) + ": " +
                        EffectDescription(effect.EffectKind, ValueOfItem(effect.ItemId)) + ", " + RunRejectionCopy.Countdown(effect.RemainingMs) + " left";
                }
            }

            for (var i = 0; i < data.Stacks.Count; i++)
            {
                rows[data.Stacks[i].ItemId].Render(data.Stacks[i], pendingItemId == data.Stacks[i].ItemId);
            }

            if (pendingItemId != null && rows[pendingItemId].LastCount < 1)
            {
                CloseChoice(returnFocus: false);
            }
        }

        private long ValueOfItem(string itemId) =>
            itemId != null && session.Catalog.Items.TryGetValue(itemId, out var item) ? item.Effect.Value : 0;

        private string TargetLabel(string targetKey)
        {
            if (string.IsNullOrEmpty(targetKey) || targetKey == RunCommands.FactoryTargetKey)
            {
                return "the whole factory";
            }

            return session.Catalog.Machines.TryGetValue(targetKey, out var machine) ? machine.Label : targetKey;
        }

        /// <summary>"Output x2" or "Speed x1.4": the effect of an Item, from its integer value (a factor or parts per million).</summary>
        public static string EffectDescription(string effectKind, long value)
        {
            switch (effectKind)
            {
                case "machineOutput": return "Output x" + value.ToString(CultureInfo.InvariantCulture);
                case "machineSpeed": return "Speed x" + Ppm(value);
                case "salePrice": return "Sale price x" + Ppm(value);
                case "upgradeCost": return "Upgrade cost x" + Ppm(value);
                default: return effectKind;
            }
        }

        private static string Ppm(long ppm)
        {
            var whole = ppm / 1_000_000L;
            var hundredths = (ppm % 1_000_000L) / 10_000L;
            if (hundredths == 0)
            {
                return whole.ToString(CultureInfo.InvariantCulture);
            }

            var text = whole.ToString(CultureInfo.InvariantCulture) + "." + hundredths.ToString("00", CultureInfo.InvariantCulture);
            return text.EndsWith("0", StringComparison.Ordinal) ? text.Substring(0, text.Length - 1) : text;
        }

        private string TargetButtonText(ItemStackSnapshot stack, int index)
        {
            var target = stack.Targets[index].Target;
            return target == null ? "Use on the whole factory" : "Use on " + TargetLabel(target);
        }

        // ---------------------------------------------------------------------------------------------------
        // Activation flow
        // ---------------------------------------------------------------------------------------------------

        private void OnActivate(StackRow row)
        {
            row.ClearFeedback();
            if (pendingItemId == row.ItemId)
            {
                CloseChoice(returnFocus: true);
                return;
            }

            CloseChoice(returnFocus: false);
            pendingItemId = row.ItemId;
            Invalidate();
            Refresh();
            var first = row.FirstAvailableTarget();
            shell?.FocusWhenReady((VisualElement)first ?? row.CancelChoice);
        }

        private void OnCancelChoice(StackRow row)
        {
            CloseChoice(returnFocus: false);
            shell?.FocusWhenReady(row.Activate);
        }

        private void OnTarget(StackRow row, int index)
        {
            row.ClearFeedback();
            var stack = row.Stack;
            var choice = stack.Targets[index];
            var current = CurrentTarget(stack.ItemId, index) ?? choice;
            if (!choice.Available || !current.Available)
            {
                // T196: a target the panel shows as unavailable never reaches ActivateItem, so an Item cannot be consumed without the
                // confirmation. A target that became available meanwhile is only re-rendered; the player then chooses it on purpose.
                Invalidate();
                Refresh();
                if (!current.Available)
                {
                    row.ShowFeedback(RunRejectionCopy.For(current.Reason));
                }

                return;
            }

            if (shell == null)
            {
                Activate(row, index);
                return;
            }

            confirmOpen = true;
            var target = row.TargetButtons[index];
            var opened = shell.RequestConfirmation(new ConfirmationRequest
            {
                Title = "Activate " + stack.Label + "?",
                Message = "Use 1 " + stack.Label + " on " + TargetLabel(choice.TargetKey) + ". " +
                    EffectDescription(stack.EffectKind, stack.EffectValue) + " for " + RunRejectionCopy.Countdown(stack.DurationMs) +
                    ". The Item is used up.",
                ConfirmLabel = "Activate",
                CancelLabel = "Cancel",
                Destructive = false,
                Invoker = target,
                OnConfirm = () =>
                {
                    confirmOpen = false;
                    Activate(row, index);
                },
                OnCancel = reason => confirmOpen = false,
            });
            if (!opened)
            {
                confirmOpen = false;
            }
        }

        /// <summary>The target choice as the run reports it now, or null when the stack or the index is gone.</summary>
        private ItemTargetChoice? CurrentTarget(string itemId, int index)
        {
            var fresh = RunSnapshots.Items(session);
            for (var i = 0; i < fresh.Stacks.Count; i++)
            {
                if (fresh.Stacks[i].ItemId == itemId)
                {
                    return index < fresh.Stacks[i].Targets.Count ? fresh.Stacks[i].Targets[index] : null;
                }
            }

            return null;
        }

        private void Activate(StackRow row, int index)
        {
            var stack = row.Stack;
            var choice = stack.Targets[index];
            var result = commands.ActivateItem(stack.ItemId, choice.Target);
            if (!result.IsSuccess)
            {
                // The Item is kept: the choice stays open and shows why.
                row.ShowFeedback(RunRejectionCopy.For(result.ReasonCode));
                Invalidate();
                Refresh();
                return;
            }

            var activated = result.Value;
            CloseChoice(returnFocus: false);
            shell?.ShowNotice(stack.Label + " is active on " + TargetLabel(activated.TargetKey) + " for " +
                RunRejectionCopy.Countdown(activated.EndUtcMs - activated.StartUtcMs) + ".", ShellNoticeKind.Info);
            Invalidate();
            Refresh();
            if (shell != null)
            {
                // The stack may be gone (the last Item was used), so focus the Activate control when it is still shown, else the tab.
                var next = rows[stack.ItemId].Activate;
                shell.FocusWhenReady(next.style.display == DisplayStyle.None || rows[stack.ItemId].Card.style.display == DisplayStyle.None
                    ? shell.TabButton(ShellTab.Items)
                    : next);
            }
        }

        private void CloseChoice(bool returnFocus)
        {
            if (pendingItemId == null)
            {
                return;
            }

            var row = rows[pendingItemId];
            pendingItemId = null;
            row.ClearFeedback();
            row.SetChoiceOpen(false);
            if (returnFocus && shell != null)
            {
                shell.FocusWhenReady(row.Activate);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Element helpers
        // ---------------------------------------------------------------------------------------------------

        private static Label MakeText(string name, string className)
        {
            var label = new Label { name = name };
            label.AddToClassList(className);
            return label;
        }

        private static void SetDisplayed(VisualElement element, bool displayed) =>
            element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;

        private sealed class StackRow
        {
            private readonly ItemsPanel owner;
            private readonly VisualElement icon;
            private readonly List<Button> targetButtons = new List<Button>();
            private readonly List<Label> targetReasons = new List<Label>();

            public StackRow(ItemStackSnapshot stack, ItemsPanel owner)
            {
                this.owner = owner;
                Stack = stack;
                ItemId = stack.ItemId;
                Card = new VisualElement { name = "items-stack-" + stack.ItemId };
                Card.AddToClassList("items-card");
                var head = new VisualElement { name = "items-head-" + stack.ItemId };
                head.AddToClassList("items-head");
                icon = new VisualElement { name = "items-icon-" + stack.ItemId, pickingMode = PickingMode.Ignore };
                icon.AddToClassList("items-icon");
                var sprite = owner.icons?.Invoke(stack.PresentationKey);
                if (sprite != null)
                {
                    icon.style.backgroundImage = new StyleBackground(sprite);
                }
                else
                {
                    SetDisplayed(icon, false);
                }

                var info = new VisualElement { name = "items-info-" + stack.ItemId };
                info.AddToClassList("items-info");
                Title = MakeText("items-title-" + stack.ItemId, "items-title");
                Tier = MakeText("items-tier-" + stack.ItemId, "items-hint");
                Effect = MakeText("items-effect-text-" + stack.ItemId, "items-line");
                Reason = MakeText("items-reason-" + stack.ItemId, "items-hint");
                PanelIcons.Mark(Reason, owner.icons, PanelIcons.Warning);
                info.Add(Title);
                info.Add(Tier);
                info.Add(Effect);
                info.Add(Reason);
                head.Add(icon);
                head.Add(info);
                Card.Add(head);

                Activate = RunShellDocument.CreateButton("items-activate-" + stack.ItemId, "Activate", true);
                Activate.clicked += () => owner.OnActivate(this);
                Card.Add(Activate);

                Choice = new VisualElement { name = "items-choice-" + stack.ItemId };
                Choice.AddToClassList("items-choice");
                var prompt = MakeText("items-choice-prompt-" + stack.ItemId, "items-line");
                prompt.text = stack.TargetKind == "factory" ? "Use it on the factory?" : "Choose a machine type for " + stack.Label + ":";
                Choice.Add(prompt);
                for (var i = 0; i < stack.Targets.Count; i++)
                {
                    var index = i;
                    var button = RunShellDocument.CreateButton("items-target-" + stack.ItemId + "-" + i, owner.TargetButtonText(stack, i));
                    button.clicked += () => owner.OnTarget(this, index);
                    var reason = MakeText("items-target-reason-" + stack.ItemId + "-" + i, "items-hint");
                    PanelIcons.Mark(reason, owner.icons, PanelIcons.Warning);
                    targetButtons.Add(button);
                    targetReasons.Add(reason);
                    Choice.Add(button);
                    Choice.Add(reason);
                }

                CancelChoice = RunShellDocument.CreateButton("items-cancel-" + stack.ItemId, "Cancel");
                CancelChoice.clicked += () => owner.OnCancelChoice(this);
                Feedback = MakeText("items-feedback-" + stack.ItemId, "items-feedback");
                PanelIcons.Mark(Feedback, owner.icons, PanelIcons.Warning);
                Choice.Add(CancelChoice);
                Choice.Add(Feedback);
                Card.Add(Choice);
                SetDisplayed(Choice, false);
                SetDisplayed(Feedback, false);
                SetDisplayed(Card, false);
            }

            public string ItemId { get; }
            public ItemStackSnapshot Stack { get; private set; }
            public long LastCount { get; private set; }
            public VisualElement Card { get; }
            public Label Title { get; }
            public Label Tier { get; }
            public Label Effect { get; }
            public Label Reason { get; }
            public Button Activate { get; }
            public VisualElement Choice { get; }
            public Button CancelChoice { get; }
            public Label Feedback { get; }
            public IReadOnlyList<Button> TargetButtons => targetButtons;
            public IReadOnlyList<Label> TargetReasons => targetReasons;

            public void Render(ItemStackSnapshot stack, bool choosing)
            {
                Stack = stack;
                LastCount = stack.Count;
                SetDisplayed(Card, stack.Count > 0);
                Title.text = stack.Label + " x" + QuantityFormatter.Format(stack.Count);
                Tier.text = "Tier " + stack.Tier.ToString(CultureInfo.InvariantCulture);
                Effect.text = EffectDescription(stack.EffectKind, stack.EffectValue) + " for " + RunRejectionCopy.Countdown(stack.DurationMs);
                var reason = stack.Count > 0 && !stack.CanActivate ? RunRejectionCopy.For(stack.ActivateReason) : string.Empty;
                Reason.text = reason;
                SetDisplayed(Reason, reason.Length > 0);
                RunShellDocument.SetSoftDisabled(Activate, stack.Count > 0 && !stack.CanActivate);
                for (var i = 0; i < targetButtons.Count && i < stack.Targets.Count; i++)
                {
                    var target = stack.Targets[i];
                    RunShellDocument.SetSoftDisabled(targetButtons[i], !target.Available);
                    var text = target.Available ? string.Empty : RunRejectionCopy.For(target.Reason);
                    targetReasons[i].text = text;
                    SetDisplayed(targetReasons[i], text.Length > 0);
                }

                SetChoiceOpen(choosing && stack.Count > 0);
            }

            public void SetChoiceOpen(bool open)
            {
                SetDisplayed(Choice, open);
                Card.EnableInClassList("items-card--choosing", open);
                Activate.text = open ? "Close" : "Activate";
            }

            public Button FirstAvailableTarget()
            {
                for (var i = 0; i < targetButtons.Count && i < Stack.Targets.Count; i++)
                {
                    if (Stack.Targets[i].Available)
                    {
                        return targetButtons[i];
                    }
                }

                return null;
            }

            public void ShowFeedback(string text)
            {
                Feedback.text = text;
                SetDisplayed(Feedback, text.Length > 0);
            }

            public void ClearFeedback() => ShowFeedback(string.Empty);
        }
    }
}
