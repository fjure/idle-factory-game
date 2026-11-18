using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI.Panels;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI
{
    /// <summary>The build card's copy in one place (world-facing words follow reviews/world-copy.md: "location", never "slot").</summary>
    public static class BuildCardCopy
    {
        public const string Confirm = "Confirm";
        public const string Cancel = "Cancel";
        public const string NoTypes = "No machine types are unlocked yet.";
        public const string NothingAffordable = "You cannot afford a machine yet. Earn Cash, then build here.";
        public const string LocationTaken = "That location is no longer free.";
        public const string LocationLocked = "That location is not unlocked yet.";

        /// <summary>"Selected: Assembler at location 5" (the location is shown from 1).</summary>
        public static string Selected(string typeLabel, int slot) => "Selected: " + typeLabel + " at location " + (slot + 1);

        /// <summary>"Build at location 5", the heading while no type is selected.</summary>
        public static string Heading(int slot) => "Build at location " + (slot + 1);

        /// <summary>"Need $12 more": the shortfall line and the disabled reason of Confirm (FR-033).</summary>
        public static string Need(long cost, long cash) => "Need $" + QuantityFormatter.Format(Math.Max(0L, cost - cash)) + " more";
    }

    /// <summary>
    /// The docked build card (T539, FR-032 to FR-034, research R13): it replaces the build sheet of an empty location. It lists the machine types with icon, cost
    /// and affordability from the same <see cref="RunSnapshots.Factory"/> data the build sheet used, previews the selected type as a ghost at the location through
    /// <see cref="IBuildPreview"/> (the world draws it, frame 0 at 60 percent alpha) with the line "Selected: &lt;type&gt; at location N", and builds only through
    /// <see cref="RunCommands.Build"/>. Confirm and Cancel sit side by side. A shortfall disables Confirm and shows "Need $X more" directly below it, so a touch
    /// user reads the reason without a tooltip. A success plays the build dust at the location, the foundation appears in the world, and the card hands over to the
    /// new machine's card. The card keeps no gameplay rule: availability, price and rejections all come from the application layer.
    /// </summary>
    public sealed class BuildCard : IShellPanel
    {
        /// <summary>The control that takes keyboard focus when the shell opens the card for the keyboard: the first machine type.</summary>
        public const string FirstFocusName = "build-card-type-0";

        private sealed class Row
        {
            public string MachineId;
            public Button Button;
            public VisualElement Icon;
            public Label Name;
            public Label Cost;
            public Label Status;
            public bool Unlocked;
            public long ExpectedPrice;
            public string Reason = string.Empty;
        }

        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly Func<string, Sprite> icons;
        private readonly MachineFeedback feedback;
        private readonly Func<IBuildPreview> previewSource;
        private readonly Func<string, string> presentationKeyOf;
        private readonly VisualElement root;
        private readonly Label selectedLabel;
        private readonly Label hintLabel;
        private readonly ScrollView types;
        private readonly Label reasonLabel;
        private readonly Label feedbackLabel;
        private readonly List<Row> rows = new List<Row>();
        private readonly List<int> available = new List<int>();
        private readonly List<int> shownAvailable = new List<int>();
        private ShellPanelContext context;
        private RunFactorySnapshot factory;
        private string selectedMachineId;
        private string shownPreviewKey;
        private int shownPreviewSlot = -1;
        private int slot = -1;
        private long structureRevision = -1;
        private long quantityRevision = -1;
        private bool dirty = true;

        public BuildCard(
            RunSession session,
            RunCommands commands,
            VisualTreeAsset tree,
            StyleSheet styles,
            StyleSheet scaffoldStyles = null,
            Func<string, Sprite> icons = null,
            MachineFeedback feedback = null,
            Func<IBuildPreview> previewSource = null,
            Func<string, string> presentationKeyOf = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            if (tree == null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            this.icons = icons;
            this.feedback = feedback ?? new MachineFeedback(null);
            this.previewSource = previewSource;
            this.presentationKeyOf = presentationKeyOf ?? (_ => string.Empty);
            var instance = tree.Instantiate();
            root = instance.Q<VisualElement>("build-card") ?? throw new InvalidOperationException("BuildCard.uxml has no build-card.");
            root.name = "build-card";
            if (scaffoldStyles != null)
            {
                root.styleSheets.Add(scaffoldStyles);
            }

            if (styles != null)
            {
                root.styleSheets.Add(styles);
            }

            selectedLabel = Require<Label>("build-card-selected");
            hintLabel = Require<Label>("build-card-hint");
            types = Require<ScrollView>("build-card-types");
            reasonLabel = Require<Label>("build-card-reason");
            feedbackLabel = Require<Label>("build-card-feedback");
            ConfirmButton = Require<Button>("build-card-confirm");
            CancelButton = Require<Button>("build-card-cancel");
            ConfirmButton.clicked += OnConfirmClicked;
            CancelButton.clicked += OnCancelClicked;
            CancelButton.tooltip = BuildCardCopy.Cancel;
            hintLabel.style.display = DisplayStyle.None;
            reasonLabel.style.display = DisplayStyle.None;
            feedbackLabel.style.display = DisplayStyle.None;
            PanelIcons.Mark(feedbackLabel, icons, PanelIcons.Warning);
            PanelScaffold.SetDisabled(ConfirmButton, false);
        }

        public string Id => ShellPanelIds.BuildCard;

        public string Title => "Build";

        public VisualElement Root => root;

        /// <summary>Confirm builds the selected type at the location. Soft-disabled (focusable, the reason shows below it) when it cannot.</summary>
        public Button ConfirmButton { get; }

        /// <summary>Cancel closes the card without building. It sits beside Confirm.</summary>
        public Button CancelButton { get; }

        /// <summary>The line "Selected: &lt;type&gt; at location N" (the heading while no type is selected).</summary>
        public Label SelectedLabel => selectedLabel;

        /// <summary>The empty-state line (no unlocked type, or nothing affordable); hidden when there is nothing to say.</summary>
        public Label HintLabel => hintLabel;

        /// <summary>The reason line directly below the actions ("Need $X more", a taken location); hidden when Confirm is enabled.</summary>
        public Label ReasonLabel => reasonLabel;

        /// <summary>The error line of a build the domain rejected; hidden when there is none.</summary>
        public Label FeedbackLabel => feedbackLabel;

        /// <summary>The scrolling list of machine types.</summary>
        public ScrollView Types => types;

        /// <summary>The location the card builds at, or -1 while it is hidden.</summary>
        public int Slot => slot;

        /// <summary>The id of the selected machine type, or null.</summary>
        public string SelectedMachineId => selectedMachineId;

        /// <summary>The type buttons in catalog order, for tests and the keyboard order.</summary>
        public IReadOnlyList<Button> TypeButtons
        {
            get
            {
                var result = new List<Button>(rows.Count);
                foreach (var row in rows)
                {
                    result.Add(row.Button);
                }

                return result;
            }
        }

        /// <summary>The type button of a machine id, or null.</summary>
        public Button TypeButton(string machineId)
        {
            foreach (var row in rows)
            {
                if (row.MachineId == machineId)
                {
                    return row.Button;
                }
            }

            return null;
        }

        private T Require<T>(string name) where T : VisualElement =>
            root.Q<T>(name) ?? throw new InvalidOperationException("BuildCard.uxml has no " + name + ".");

        public void OnShown(ShellPanelContext shown)
        {
            // A retarget (the player taps another free location while the card is open) keeps the selected type; a fresh open picks one again.
            var retarget = slot >= 0;
            context = shown;
            slot = shown.SlotIndex;
            if (!retarget)
            {
                selectedMachineId = null;
            }

            SetFeedback(string.Empty);
            dirty = true;
            Refresh();
        }

        public void OnHidden()
        {
            slot = -1;
            context = null;
            selectedMachineId = null;
            ClearWorldPreview();
        }

        /// <summary>Per-frame update from the shell: the card follows Cash and the revisions (affordability changes while it is open).</summary>
        public void Refresh()
        {
            if (slot < 0)
            {
                return;
            }

            if (!dirty && structureRevision == session.StructureRevision && quantityRevision == session.QuantityRevision)
            {
                return;
            }

            dirty = false;
            structureRevision = session.StructureRevision;
            quantityRevision = session.QuantityRevision;
            factory = RunSnapshots.Factory(session);
            Render();
        }

        private void Render()
        {
            var snapshot = factory;
            var typeSnapshots = snapshot.MachineTypes;
            while (rows.Count < typeSnapshots.Count)
            {
                rows.Add(CreateRow(rows.Count));
            }

            var slotStatus = slot < snapshot.Slots.Count ? snapshot.Slots[slot].Status : SlotStatus.Locked;
            var slotFree = slotStatus == SlotStatus.Empty;
            CollectAvailable(snapshot);

            var anyUnlocked = false;
            var anyAffordable = false;
            for (var i = 0; i < typeSnapshots.Count; i++)
            {
                anyUnlocked |= typeSnapshots[i].Unlocked;
                anyAffordable |= typeSnapshots[i].Unlocked && typeSnapshots[i].Affordable;
            }

            var selectedType = ResolveSelection(typeSnapshots);
            for (var i = 0; i < typeSnapshots.Count; i++)
            {
                RenderRow(rows[i], typeSnapshots[i], snapshot.Cash, ReferenceEquals(typeSnapshots[i], selectedType));
            }

            RenderSelected(selectedType, slotFree);
            RenderActions(selectedType, snapshot.Cash, slotStatus);
            RenderHint(anyUnlocked, anyAffordable);
            RenderWorld(selectedType, slotFree);
        }

        // The selected type: the player's choice while it is still unlocked, otherwise the first affordable type, otherwise the first unlocked one (it previews and
        // shows its shortfall), otherwise none.
        private MachineTypeSnapshot ResolveSelection(IReadOnlyList<MachineTypeSnapshot> typeSnapshots)
        {
            MachineTypeSnapshot firstAffordable = null;
            MachineTypeSnapshot firstUnlocked = null;
            for (var i = 0; i < typeSnapshots.Count; i++)
            {
                var type = typeSnapshots[i];
                if (!type.Unlocked)
                {
                    continue;
                }

                if (type.MachineId == selectedMachineId)
                {
                    return type;
                }

                firstUnlocked = firstUnlocked ?? type;
                if (type.Affordable && firstAffordable == null)
                {
                    firstAffordable = type;
                }
            }

            var chosen = firstAffordable ?? firstUnlocked;
            selectedMachineId = chosen?.MachineId;
            return chosen;
        }

        private void RenderRow(Row row, MachineTypeSnapshot type, long cash, bool selected)
        {
            row.MachineId = type.MachineId;
            row.Unlocked = type.Unlocked;
            row.ExpectedPrice = type.BuildCost;
            var priceKnown = type.RejectionReason != RunReasons.ValueLimit;
            row.Name.text = type.Label;
            row.Cost.text = priceKnown ? "Cost: " + MarketPanelParts.Money(type.BuildCost) : "Cost: too large";
            string status;
            string reason;
            if (!type.Unlocked)
            {
                reason = SlotSheetText.UnlockReason(session, type);
                status = reason;
            }
            else if (!priceKnown)
            {
                reason = RunRejectionCopy.For(RunReasons.ValueLimit);
                status = reason;
            }
            else if (!type.Affordable)
            {
                reason = BuildCardCopy.Need(type.BuildCost, cash);
                status = reason;
            }
            else
            {
                reason = string.Empty;
                status = "Affordable";
            }

            row.Reason = type.Unlocked ? string.Empty : reason;
            row.Status.text = status;
            row.Status.EnableInClassList("bcard__status--ok", type.Unlocked && priceKnown && type.Affordable);
            row.Status.EnableInClassList("bcard__status--short", !(type.Unlocked && priceKnown && type.Affordable));
            row.Button.EnableInClassList("bcard__type--selected", selected);
            row.Button.EnableInClassList("bcard__type--locked", !type.Unlocked);
            RunShellDocument.SetSoftDisabled(row.Button, !type.Unlocked);
            var sprite = icons != null ? icons(presentationKeyOf(type.MachineId)) : null;
            row.Icon.style.backgroundImage = sprite == null ? new StyleBackground(StyleKeyword.None) : new StyleBackground(sprite);
            var priceName = priceKnown ? ", " + MarketPanelParts.MoneyExact(type.BuildCost) : string.Empty;
            row.Button.tooltip = type.Label + priceName + ", owned " + type.OwnedOfType + ", " + status + (selected ? ", selected" : string.Empty);
        }

        private void RenderSelected(MachineTypeSnapshot selectedType, bool slotFree)
        {
            if (selectedType == null)
            {
                selectedLabel.text = BuildCardCopy.Heading(slot);
            }
            else
            {
                selectedLabel.text = BuildCardCopy.Selected(selectedType.Label, slot);
            }

            // The free locations are outlined in the world; the count is the same information in words for a screen reader.
            selectedLabel.tooltip = selectedLabel.text + ". " + available.Count + (available.Count == 1 ? " free location is outlined." : " free locations are outlined.");
        }

        private void RenderActions(MachineTypeSnapshot selectedType, long cash, SlotStatus slotStatus)
        {
            string reason = null;
            if (slotStatus == SlotStatus.Occupied)
            {
                reason = BuildCardCopy.LocationTaken;
            }
            else if (slotStatus == SlotStatus.Locked)
            {
                reason = BuildCardCopy.LocationLocked;
            }
            else if (selectedType == null)
            {
                reason = BuildCardCopy.NoTypes;
            }
            else if (selectedType.RejectionReason == RunReasons.ValueLimit)
            {
                reason = RunRejectionCopy.For(RunReasons.ValueLimit);
            }
            else if (!selectedType.Affordable)
            {
                reason = BuildCardCopy.Need(selectedType.BuildCost, cash);
            }

            ConfirmButton.text = BuildCardCopy.Confirm;
            // The reason sits directly below the actions as a visible line (touch users do not hover), and also rides in the tooltip, the accessible description.
            PanelScaffold.SetDisabled(ConfirmButton, reason != null, reason, reasonLabel);
            if (reason == null)
            {
                ConfirmButton.tooltip = BuildCardCopy.Confirm + ": " + BuildCardCopy.Selected(selectedType.Label, slot) + " for " +
                    MarketPanelParts.MoneyExact(selectedType.BuildCost);
            }
        }

        private void RenderHint(bool anyUnlocked, bool anyAffordable)
        {
            string hint = null;
            if (!anyUnlocked)
            {
                hint = BuildCardCopy.NoTypes;
            }
            else if (!anyAffordable)
            {
                hint = BuildCardCopy.NothingAffordable;
            }

            hintLabel.text = hint ?? string.Empty;
            hintLabel.style.display = hint == null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // ------------------------------------------------------------------------------------------------------
        // World preview (FR-033): the available locations are outlined, the selected type is a ghost at the location
        // ------------------------------------------------------------------------------------------------------

        private void CollectAvailable(RunFactorySnapshot snapshot)
        {
            available.Clear();
            for (var i = 0; i < snapshot.Slots.Count; i++)
            {
                if (snapshot.Slots[i].Status == SlotStatus.Empty)
                {
                    available.Add(i);
                }
            }
        }

        private void RenderWorld(MachineTypeSnapshot selectedType, bool slotFree)
        {
            var preview = previewSource?.Invoke();
            if (preview == null)
            {
                return;
            }

            if (!SameLocations(available, shownAvailable))
            {
                shownAvailable.Clear();
                shownAvailable.AddRange(available);
                preview.ShowAvailable(shownAvailable);
            }

            var key = selectedType != null && slotFree ? presentationKeyOf(selectedType.MachineId) : null;
            if (string.IsNullOrEmpty(key))
            {
                key = null;
            }

            var previewSlot = key == null ? -1 : slot;
            if (previewSlot != shownPreviewSlot || key != shownPreviewKey)
            {
                shownPreviewSlot = previewSlot;
                shownPreviewKey = key;
                preview.ShowPreview(previewSlot, key);
            }
        }

        private void ClearWorldPreview()
        {
            shownAvailable.Clear();
            shownPreviewSlot = -1;
            shownPreviewKey = null;
            previewSource?.Invoke()?.ClearBuildPreview();
        }

        private static bool SameLocations(List<int> a, List<int> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        // ------------------------------------------------------------------------------------------------------
        // Actions (a rejection shows inline and changes no state)
        // ------------------------------------------------------------------------------------------------------

        private Row CreateRow(int index)
        {
            var row = new Row
            {
                Button = new Button { name = "build-card-type-" + index, focusable = true },
                Icon = new VisualElement { name = "build-card-icon-" + index, pickingMode = PickingMode.Ignore },
                Name = new Label { name = "build-card-name-" + index, pickingMode = PickingMode.Ignore },
                Cost = new Label { name = "build-card-cost-" + index, pickingMode = PickingMode.Ignore },
                Status = new Label { name = "build-card-status-" + index, pickingMode = PickingMode.Ignore },
            };
            row.Button.AddToClassList("run-control");
            row.Button.AddToClassList(RunShellDocument.ScaffoldButtonClass);
            row.Button.AddToClassList("bcard__type");
            row.Icon.AddToClassList("bcard__icon");
            row.Name.AddToClassList("bcard__name");
            row.Cost.AddToClassList("bcard__line");
            row.Status.AddToClassList("bcard__line");
            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("bcard__text");
            text.Add(row.Name);
            text.Add(row.Cost);
            text.Add(row.Status);
            row.Button.Add(row.Icon);
            row.Button.Add(text);
            row.Button.clicked += () => OnTypeClicked(row);
            types.Add(row.Button);
            return row;
        }

        private void OnTypeClicked(Row row)
        {
            if (slot < 0 || row.MachineId == null)
            {
                return;
            }

            SetFeedback(string.Empty);
            if (!row.Unlocked)
            {
                // A locked type cannot be selected: its reason shows in the error line (the row carries it too, so nothing is tooltip-only).
                SetFeedback(row.Reason);
                return;
            }

            selectedMachineId = row.MachineId;
            dirty = true;
            Refresh();
        }

        private void OnConfirmClicked()
        {
            if (slot < 0 || selectedMachineId == null)
            {
                return;
            }

            SetFeedback(string.Empty);
            if (PanelScaffold.IsDisabled(ConfirmButton))
            {
                // Soft-disabled: the reason already shows below the button. Nothing is sent and nothing changes.
                return;
            }

            Row selected = null;
            foreach (var row in rows)
            {
                if (row.MachineId == selectedMachineId)
                {
                    selected = row;
                }
            }

            if (selected == null)
            {
                return;
            }

            var shell = context?.Shell;
            var builtAt = slot;
            var hadFocus = shell != null && root.panel?.focusController?.focusedElement is VisualElement focused && root.Contains(focused);
            // The UI passes the price it shows, so a price that moved since the last draw rejects instead of charging.
            var result = commands.Build(builtAt, selectedMachineId, selected.ExpectedPrice);
            if (!result.IsSuccess)
            {
                dirty = true;
                Refresh();
                SetFeedback(result.ReasonCode == RunReasons.BuildPriceChanged
                    ? "Price changed to " + MarketPanelParts.MoneyExact(selected.ExpectedPrice) + ". You were not charged."
                    : RunRejectionCopy.For(result.ReasonCode));
                return;
            }

            // Success (FR-034): the dust plays at the location (the static highlight on the new machine's card under reduced motion or without a sink), the foundation is
            // in the world already, and the card hands over to the machine card of the new machine, with focus when it was in this card.
            var staticResponse = false;
            feedback.Play(MachineResponseKind.BuildDust, builtAt, () => staticResponse = true);
            if (shell == null)
            {
                return;
            }

            if (shell.OpenMachineCard(builtAt, hadFocus))
            {
                if (staticResponse)
                {
                    (shell.Card as MachineCard)?.ShowHighlight();
                }
            }
            else
            {
                shell.CloseBuildCard(hadFocus);
            }
        }

        private void OnCancelClicked() => context?.Shell.CloseBuildCard();

        private void SetFeedback(string text)
        {
            feedbackLabel.text = text ?? string.Empty;
            feedbackLabel.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
