using System;
using System.Collections.Generic;
using System.Text;
using IdleFactory.Run.Application;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// Shared player-readable text of the two slot sheets (build sheet and machine panel). Pure formatting of read-model values:
    /// no rule lives here, every number comes from the application read models.
    /// </summary>
    public static class SlotSheetText
    {
        public static string ResourceLabel(RunSession session, string resourceId)
        {
            if (resourceId != null && session.Catalog.Resources.TryGetValue(resourceId, out var def))
            {
                return def.Label;
            }

            return resourceId ?? string.Empty;
        }

        /// <summary>"Iron Ore 5/min, Iron Bar 2.5/min", or "nothing" for an empty list. Rates are the floored three-digit display (R06).</summary>
        public static string Flow(RunSession session, IReadOnlyList<ResourceRate> rates)
        {
            if (rates == null || rates.Count == 0)
            {
                return "nothing";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < rates.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(ResourceLabel(session, rates[i].ResourceId)).Append(' ').Append(rates[i].PerMinuteText).Append("/min");
            }

            return builder.ToString();
        }

        /// <summary>A speed multiplier in ppm as "x2" or "x1.5" (two decimals at most, floored).</summary>
        public static string Multiplier(long ppm)
        {
            var whole = ppm / 1_000_000L;
            var hundredths = ppm % 1_000_000L / 10_000L;
            if (hundredths == 0)
            {
                return "x" + whole;
            }

            var fraction = hundredths.ToString("00", System.Globalization.CultureInfo.InvariantCulture).TrimEnd('0');
            return "x" + whole + "." + fraction;
        }

        /// <summary>Why a machine type is locked, from its unlock condition.</summary>
        public static string UnlockReason(RunSession session, MachineTypeSnapshot type)
        {
            switch (type.UnlockKind)
            {
                case "factoryLevelAtLeast": return "Unlocks at factory level " + type.UnlockLevel + ".";
                case "researchPurchased": return "Unlocks with a Research.";
                case "resourceProduced": return "Unlocks once " + ResourceLabel(session, type.UnlockId) + " has been produced.";
                default: return RunRejectionCopy.For(RunReasons.MachineLocked);
            }
        }

        /// <summary>"Not enough Cash. Need 40 more." for a short Cash balance; the plain rejection copy otherwise.</summary>
        public static string CashShortfall(long cost, long cash)
        {
            var missing = cost - cash;
            return missing > 0
                ? RunRejectionCopy.For(RunReasons.CashInsufficient) + " Need " + QuantityFormatter.Format(missing) + " more."
                : RunRejectionCopy.For(RunReasons.CashInsufficient);
        }

        public static void SetText(Label label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }

            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }

    /// <summary>
    /// The build sheet of an empty slot (T113, FR-009, US1 scenario 1): every machine type with its cost, rate, inputs and outputs.
    /// Duplicates are allowed, so a type stays buildable after one was built. A locked or unaffordable type has a disabled-looking
    /// button that stays focusable, shows its reason next to it, and still sends the command, which rejects with its reason code.
    /// The presenter reads only <see cref="RunSnapshots.Factory"/> and mutates only through <see cref="RunCommands.Build"/>.
    /// </summary>
    public sealed class BuildSheetPresenter : IShellPanel
    {
        private sealed class Card
        {
            public string MachineId;
            public VisualElement Container;
            public Label Name;
            public Label Cost;
            public Label Makes;
            public Label Uses;
            public Label Reason;
            public Button Build;
            public Label Feedback;
        }

        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly VisualElement root;
        private readonly Label emptyLabel;
        private readonly Label noteLabel;
        private readonly VisualElement list;
        private readonly List<Card> cards = new List<Card>();
        private int slot = -1;
        private long structureRevision = -1;
        private long quantityRevision = -1;
        private bool dirty = true;

        public BuildSheetPresenter(RunSession session, RunCommands commands, VisualTreeAsset tree, StyleSheet styles)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            if (tree == null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            root = tree.Instantiate();
            root.name = "build-sheet-root";
            root.style.flexShrink = 0;
            if (styles != null)
            {
                root.styleSheets.Add(styles);
            }

            emptyLabel = root.Q<Label>("build-sheet-empty");
            noteLabel = root.Q<Label>("build-sheet-note");
            list = root.Q<VisualElement>("build-sheet-list");
        }

        public string Id => ShellPanelIds.Build;
        public string Title => "Build";
        public VisualElement Root => root;

        /// <summary>The slot the sheet builds into, or -1 while it is hidden.</summary>
        public int Slot => slot;

        /// <summary>The build buttons in catalog order, for tests and the keyboard order.</summary>
        public IReadOnlyList<Button> BuildButtons
        {
            get
            {
                var result = new List<Button>(cards.Count);
                foreach (var card in cards)
                {
                    result.Add(card.Build);
                }

                return result;
            }
        }

        public void OnShown(ShellPanelContext context)
        {
            slot = context.SlotIndex;
            foreach (var card in cards)
            {
                SlotSheetText.SetText(card.Feedback, string.Empty);
            }

            dirty = true;
            Refresh();
        }

        public void OnHidden()
        {
            slot = -1;
        }

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
            Render(RunSnapshots.Factory(session));
        }

        private void Render(RunFactorySnapshot snapshot)
        {
            var types = snapshot.MachineTypes;
            while (cards.Count < types.Count)
            {
                cards.Add(CreateCard(cards.Count));
            }

            var slotState = slot < snapshot.Slots.Count ? snapshot.Slots[slot].Status : SlotStatus.Locked;
            var slotReason = slotState == SlotStatus.Occupied
                ? RunRejectionCopy.For(RunReasons.SlotOccupied)
                : slotState == SlotStatus.Locked ? RunRejectionCopy.For(RunReasons.SlotUnavailable) : string.Empty;
            SlotSheetText.SetText(noteLabel, slotReason);

            var anyUnlocked = false;
            for (var i = 0; i < types.Count; i++)
            {
                var type = types[i];
                anyUnlocked |= type.Unlocked;
                var card = cards[i];
                card.MachineId = type.MachineId;
                card.Name.text = type.Label;
                card.Cost.text = "Cost: " + QuantityFormatter.Format(type.BuildCost) + " Cash";
                card.Makes.text = "Makes: " + SlotSheetText.Flow(session, type.Level1Rates?.Outputs);
                card.Uses.text = "Uses: " + SlotSheetText.Flow(session, type.Level1Rates?.Inputs);
                string reason;
                if (!type.Unlocked)
                {
                    reason = SlotSheetText.UnlockReason(session, type);
                }
                else if (!type.Affordable)
                {
                    reason = SlotSheetText.CashShortfall(type.BuildCost, snapshot.Cash);
                }
                else
                {
                    reason = string.Empty;
                }

                SlotSheetText.SetText(card.Reason, reason);
                card.Build.text = "Build " + type.Label;
                var disabled = !type.Unlocked || !type.Affordable || slotState != SlotStatus.Empty;
                RunShellDocument.SetSoftDisabled(card.Build, disabled);
                card.Container.EnableInClassList("bsheet__card--disabled", disabled);
            }

            emptyLabel.style.display = anyUnlocked ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private Card CreateCard(int index)
        {
            var card = new Card
            {
                Container = new VisualElement { name = "build-card-" + index },
                Name = new Label { name = "build-name-" + index },
                Cost = new Label { name = "build-cost-" + index },
                Makes = new Label { name = "build-makes-" + index },
                Uses = new Label { name = "build-uses-" + index },
                Reason = new Label { name = "build-reason-" + index },
                Build = RunShellDocument.CreateButton("build-button-" + index, "Build", true),
                Feedback = new Label { name = "build-feedback-" + index },
            };
            card.Container.AddToClassList("bsheet__card");
            card.Name.AddToClassList("bsheet__name");
            card.Cost.AddToClassList("bsheet__line");
            card.Makes.AddToClassList("bsheet__line");
            card.Uses.AddToClassList("bsheet__line");
            card.Reason.AddToClassList("bsheet__reason-line");
            card.Build.AddToClassList("bsheet__button");
            card.Feedback.AddToClassList("bsheet__feedback");
            card.Reason.style.display = DisplayStyle.None;
            card.Feedback.style.display = DisplayStyle.None;
            card.Container.Add(card.Name);
            card.Container.Add(card.Cost);
            card.Container.Add(card.Makes);
            card.Container.Add(card.Uses);
            card.Container.Add(card.Reason);
            card.Container.Add(card.Build);
            card.Container.Add(card.Feedback);
            card.Build.clicked += () => OnBuildClicked(card);
            list.Add(card.Container);
            return card;
        }

        private void OnBuildClicked(Card card)
        {
            if (slot < 0 || card.MachineId == null)
            {
                return;
            }

            foreach (var other in cards)
            {
                SlotSheetText.SetText(other.Feedback, string.Empty);
            }

            var result = commands.Build(slot, card.MachineId);
            if (!result.IsSuccess)
            {
                // The rejection copy sits next to the control that was used; no state changed.
                SlotSheetText.SetText(card.Feedback, RunRejectionCopy.For(result.ReasonCode));
            }

            dirty = true;
            Refresh();
        }
    }
}
