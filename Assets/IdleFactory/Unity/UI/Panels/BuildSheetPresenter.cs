using System;
using System.Collections.Generic;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Unity.UI.Panels;
using UnityEngine;
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

        /// <summary>"Not enough Cash. Need $40 more." for a short Cash balance; the plain rejection copy otherwise.</summary>
        public static string CashShortfall(long cost, long cash)
        {
            var missing = cost - cash;
            return missing > 0
                ? RunRejectionCopy.For(RunReasons.CashInsufficient) + " Need $" + QuantityFormatter.Format(missing) + " more."
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
    /// The Art Bible 9.4 lock and warning icons beside a requirement or a rejection text (T196, FR-032, contracts/ui-surfaces.md
    /// Global): the icon is the label's own background image, drawn at the left of the text inside a left padding, so it shows and
    /// hides with the text, never stands alone, and the label keeps no child element (a label that has children is not measured as
    /// text). A text button never gets an icon element either; an icon on a button is its background image. Only catalog sprites are
    /// used; without a resolver or a sprite the label keeps its plain text.
    /// </summary>
    public static class PanelIcons
    {
        public const string Lock = "F003-UI-ICON-LOCK";
        public const string Warning = "F003-UI-ICON-WARNING";
        public const string Info = "F003-UI-ICON-INFO";
        public const string Timer = "F003-UI-ICON-TIMER";
        public const string Offline = "F003-UI-ICON-OFFLINE";
        public const string FactoryValue = "F003-UI-ICON-FV";
        public const string IndustryPoints = "F003-UI-ICON-IP";
        public const string Industry = "F003-UI-ICON-INDUSTRY";

        /// <summary>Icon size in panel points; the label is at least this tall and its text starts <see cref="Gutter"/> from the left.</summary>
        public const float IconSize = 24f;

        public const float Gutter = 30f;

        private const string TintDanger = "run-icon--tint-danger";
        private const string TintMuted = "run-icon--tint-muted";
        private const string TintText = "run-icon--tint-text";

        /// <summary>Whether the label currently carries an icon.</summary>
        public static bool HasIcon(Label label) => label != null && label.style.backgroundImage.value.sprite != null;

        /// <summary>Sets or clears the icon of <paramref name="label"/>: <see cref="Lock"/> tinted muted, <see cref="Warning"/> tinted danger; a null key clears it.</summary>
        public static void Mark(Label label, Func<string, Sprite> icons, string key)
        {
            if (label == null)
            {
                return;
            }

            var sprite = icons == null || key == null ? null : icons(key);
            if (sprite == null)
            {
                label.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                label.style.paddingLeft = StyleKeyword.Null;
                label.style.minHeight = StyleKeyword.Null;
                label.EnableInClassList(TintDanger, false);
                label.EnableInClassList(TintMuted, false);
                label.EnableInClassList(TintText, false);
                return;
            }

            label.style.backgroundImage = new StyleBackground(sprite);
            label.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left, 0f);
            label.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top, 0f);
            label.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            label.style.backgroundSize = new BackgroundSize(new Length(IconSize, LengthUnit.Pixel), new Length(IconSize, LengthUnit.Pixel));
            var warning = key == Warning;
            var muted = key == Lock || key == Info;
            label.EnableInClassList(TintDanger, warning);
            label.EnableInClassList(TintMuted, muted);
            label.EnableInClassList(TintText, !warning && !muted);
            label.style.paddingLeft = Gutter;
            label.style.minHeight = IconSize;
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
            public Button Price;
            public Label Owned;
            public Label Capped;
            public IDisposable PriceExact;
            public string ExactText = string.Empty;
            public long ExpectedPrice;
            public Label Makes;
            public Label Uses;
            public Label Reason;
            public Button Build;
            public Label Feedback;
        }

        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly Func<string, Sprite> icons;
        private readonly VisualElement root;
        private readonly Label emptyLabel;
        private readonly Label noteLabel;
        private readonly VisualElement list;
        private readonly List<Card> cards = new List<Card>();
        private RunShellDocument shell;
        private int slot = -1;
        private long structureRevision = -1;
        private long quantityRevision = -1;
        private bool dirty = true;

        public BuildSheetPresenter(RunSession session, RunCommands commands, VisualTreeAsset tree, StyleSheet styles, Func<string, Sprite> icons = null)
        {
            this.icons = icons;
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
            PanelIcons.Mark(noteLabel, icons, PanelIcons.Warning);
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
            if (!ReferenceEquals(shell, context.Shell))
            {
                // The exact-value detail line belongs to one shell; bind every price to the shell that shows the sheet.
                foreach (var existing in cards)
                {
                    existing.PriceExact?.Dispose();
                    existing.PriceExact = null;
                }

                shell = context.Shell;
                foreach (var existing in cards)
                {
                    BindExact(existing);
                }
            }

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
            PanelIcons.Mark(noteLabel, icons, slotState == SlotStatus.Locked ? PanelIcons.Lock : PanelIcons.Warning);

            var anyUnlocked = false;
            for (var i = 0; i < types.Count; i++)
            {
                var type = types[i];
                anyUnlocked |= type.Unlocked;
                var card = cards[i];
                card.MachineId = type.MachineId;
                card.Name.text = type.Label;
                // F005 build price (US4): the price the Build command is passed as expectedPrice, the owned count and the cap flag
                // all come from the factory snapshot; nothing is computed here.
                var priceKnown = type.RejectionReason != RunReasons.ValueLimit;
                card.ExpectedPrice = type.BuildCost;
                card.Price.name = "build-price-" + type.MachineId;
                card.Price.text = priceKnown ? "Cost: $" + QuantityFormatter.Format(type.BuildCost) : "Cost: too large";
                card.Owned.text = "Owned: " + type.OwnedOfType;
                card.Capped.text = "Capped: the price does not rise further.";
                card.Capped.style.display = type.PriceCapped ? DisplayStyle.Flex : DisplayStyle.None;
                card.ExactText = priceKnown
                    ? type.Label + " costs $" + QuantityFormatter.FormatExact(type.BuildCost) + ", owned " + type.OwnedOfType + (type.PriceCapped ? ", price capped" : string.Empty)
                    : type.Label + ": " + RunRejectionCopy.For(RunReasons.ValueLimit);
                card.Makes.text = "Makes: " + SlotSheetText.Flow(session, type.Level1Rates?.Outputs);
                card.Uses.text = "Uses: " + SlotSheetText.Flow(session, type.Level1Rates?.Inputs);
                string reason;
                if (!type.Unlocked)
                {
                    reason = SlotSheetText.UnlockReason(session, type);
                }
                else if (type.RejectionReason == RunReasons.ValueLimit)
                {
                    reason = RunRejectionCopy.For(RunReasons.ValueLimit);
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
                PanelIcons.Mark(card.Reason, icons, type.Unlocked ? PanelIcons.Warning : PanelIcons.Lock);
                card.Build.text = "Build " + type.Label;
                var disabled = !type.Unlocked || !type.Affordable || slotState != SlotStatus.Empty;
                // Accessible name (ui-surfaces-f005.md): "Build Assembler, $69, owned 3" plus the reason when unavailable.
                var priceName = priceKnown ? ", " + MarketPanelParts.MoneyExact(type.BuildCost) : string.Empty;
                var why = string.IsNullOrEmpty(reason) ? slotReason : reason;
                var reasonName = disabled ? ", unavailable" + (string.IsNullOrEmpty(why) ? string.Empty : ": " + why) : string.Empty;
                card.Build.tooltip = "Build " + type.Label + priceName + ", owned " + type.OwnedOfType + (type.PriceCapped ? ", price capped" : string.Empty) + reasonName;
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
                Price = MarketPanelParts.NewValueButton("build-price-" + index, "bsheet__price"),
                Owned = new Label { name = "build-owned-" + index },
                Capped = new Label { name = "build-capped-" + index },
                Makes = new Label { name = "build-makes-" + index },
                Uses = new Label { name = "build-uses-" + index },
                Reason = new Label { name = "build-reason-" + index },
                Build = RunShellDocument.CreateButton("build-button-" + index, "Build", true),
                Feedback = new Label { name = "build-feedback-" + index },
            };
            card.Container.AddToClassList("bsheet__card");
            card.Name.AddToClassList("bsheet__name");
            card.Owned.AddToClassList("bsheet__line");
            card.Capped.AddToClassList("bsheet__line");
            card.Capped.AddToClassList("bsheet__capped");
            card.Capped.style.display = DisplayStyle.None;
            card.Makes.AddToClassList("bsheet__line");
            card.Uses.AddToClassList("bsheet__line");
            card.Reason.AddToClassList("bsheet__reason-line");
            card.Build.AddToClassList("bsheet__button");
            card.Feedback.AddToClassList("bsheet__feedback");
            card.Reason.style.display = DisplayStyle.None;
            card.Feedback.style.display = DisplayStyle.None;
            PanelIcons.Mark(card.Reason, icons, PanelIcons.Warning);
            PanelIcons.Mark(card.Feedback, icons, PanelIcons.Warning);
            card.Container.Add(card.Name);
            card.Container.Add(card.Price);
            card.Container.Add(card.Owned);
            card.Container.Add(card.Capped);
            card.Container.Add(card.Makes);
            card.Container.Add(card.Uses);
            card.Container.Add(card.Reason);
            card.Container.Add(card.Build);
            card.Container.Add(card.Feedback);
            card.Build.clicked += () => OnBuildClicked(card);
            BindExact(card);
            list.Add(card.Container);
            return card;
        }

        private void BindExact(Card card)
        {
            if (shell != null && card.PriceExact == null)
            {
                card.PriceExact = shell.AttachExactValue(card.Price, () => card.ExactText);
            }
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

            // The UI always passes the price it shows, so a price that moved since the last draw rejects instead of charging.
            var result = commands.Build(slot, card.MachineId, card.ExpectedPrice);
            dirty = true;
            if (!result.IsSuccess && result.ReasonCode == RunReasons.BuildPriceChanged)
            {
                // Recovery: redraw with the current price and say so; nothing was charged.
                Refresh();
                SlotSheetText.SetText(card.Feedback, "Price changed to " + MarketPanelParts.MoneyExact(card.ExpectedPrice) + ". You were not charged.");
                return;
            }

            if (!result.IsSuccess)
            {
                // The rejection copy sits next to the control that was used; no state changed.
                SlotSheetText.SetText(card.Feedback, RunRejectionCopy.For(result.ReasonCode));
            }

            Refresh();
        }
    }
}
