using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>
    /// The Expansion surface (FR-007, FR-035; contracts/ui-surfaces.md): the storage track and the factory track, each with its
    /// level, what the next level gives (capacity or slots) and what it costs. At the maximum level the upgrade is disabled
    /// with its reason visible. An unaffordable upgrade is disabled with the reason visible before the tap, and the command
    /// still re-validates on activation. The panel reads <see cref="RunFactorySnapshot"/> and mutates only through
    /// <see cref="RunCommands.UpgradeStorage"/> and <see cref="RunCommands.UpgradeFactory"/>.
    /// </summary>
    public sealed class ExpansionPanel : IShellPanel
    {
        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly RunShellDocument shell;
        private readonly VisualElement root;
        private readonly VisualElement cards;
        private readonly Track storage;
        private readonly Track factory;
        private readonly List<IDisposable> exactHandles = new List<IDisposable>();
        private long structureRevision = -1;
        private long lastCash = long.MinValue;

        private sealed class Track
        {
            public string Key;
            public VisualElement Card;
            public Label Level;
            public Button Value;
            public Button Cost;
            public Button Upgrade;
            public Label Status;
            public Label Reason;
            public string ValueExact = string.Empty;
            public string CostExact = string.Empty;
        }

        public ExpansionPanel(
            RunSession session,
            RunCommands commands,
            RunShellDocument shell,
            VisualTreeAsset tree,
            StyleSheet styles,
            Func<string, Sprite> icons = null,
            string panelId = ShellPanelIds.Market)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
            Id = panelId;
            root = MarketPanelParts.CreateRoot(tree, styles, "expansion-panel", "exp-panel");
            MarketPanelParts.Ensure<Label>(root, "expansion-heading", "exp-heading").text = "Expansion";
            cards = MarketPanelParts.Ensure<VisualElement>(root, "expansion-cards", "exp-cards");
            storage = BuildTrack("storage", "Storage", "Upgrade storage", () => commands.UpgradeStorage(), "F003-UI-ICON-STORAGE", icons);
            factory = BuildTrack("factory", "Factory", "Expand factory", () => commands.UpgradeFactory(), "F003-UI-ICON-EXPAND-FACTORY", icons);
        }

        public string Id { get; }
        public string Title => "Expansion";
        public VisualElement Root => root;

        public Label StorageLevel => storage.Level;
        public Button StorageValue => storage.Value;
        public Button StorageCost => storage.Cost;
        public Button StorageUpgrade => storage.Upgrade;
        public Label StorageStatus => storage.Status;
        public Label StorageReason => storage.Reason;
        public Label FactoryLevel => factory.Level;
        public Button FactoryValue => factory.Value;
        public Button FactoryCost => factory.Cost;
        public Button FactoryUpgrade => factory.Upgrade;
        public Label FactoryStatus => factory.Status;
        public Label FactoryReason => factory.Reason;

        public void OnShown(ShellPanelContext context)
        {
            storage.Status.text = string.Empty;
            factory.Status.text = string.Empty;
            Render(true);
        }

        public void Refresh()
        {
            // Affordability follows Cash and the structure only, so stock changes alone never rebuild the snapshot.
            var cash = session.ReadCash();
            if (session.StructureRevision != structureRevision || cash != lastCash)
            {
                Render(false);
            }
        }

        public void OnHidden()
        {
            storage.Reason.text = string.Empty;
            factory.Reason.text = string.Empty;
        }

        public void Render(bool force)
        {
            structureRevision = session.StructureRevision;
            lastCash = session.ReadCash();
            var snapshot = RunSnapshots.Factory(session);
            UpdateTrack(storage, snapshot.Storage, "Capacity", "units");
            UpdateTrack(factory, snapshot.Factory, "Slots", "slots");
        }

        private Track BuildTrack(string key, string title, string upgradeText, Func<RunCommandResult<ExpansionResult>> upgrade, string iconKey, Func<string, Sprite> icons)
        {
            var track = new Track { Key = key };
            track.Card = new VisualElement { name = "expansion-" + key };
            track.Card.AddToClassList("exp-card");
            var heading = MarketPanelParts.NewLabel("expansion-" + key + "-title", "exp-card__title", title);
            if (icons != null)
            {
                MarketPanelParts.SetIcon(heading, icons, iconKey);
                heading.AddToClassList("exp-card__title--icon");
            }

            track.Card.Add(heading);
            track.Level = MarketPanelParts.NewLabel("expansion-" + key + "-level", "exp-card__level");
            track.Card.Add(track.Level);
            track.Value = AddLine(track, key == "storage" ? "Capacity now and next" : "Slots now and next", "expansion-" + key + "-value", () => track.ValueExact);
            track.Cost = AddLine(track, "Next level costs", "expansion-" + key + "-cost", () => track.CostExact);
            track.Upgrade = RunShellDocument.CreateButton("expansion-" + key + "-upgrade", upgradeText, true);
            track.Upgrade.AddToClassList("exp-upgrade");
            track.Upgrade.clicked += () => OnUpgrade(track, upgrade);
            track.Card.Add(track.Upgrade);
            track.Reason = MarketPanelParts.NewLabel("expansion-" + key + "-reason", "exp-caption");
            track.Reason.AddToClassList("exp-caption--reason");
            track.Card.Add(track.Reason);
            track.Status = MarketPanelParts.NewLabel("expansion-" + key + "-status", "exp-caption");
            track.Card.Add(track.Status);
            cards.Add(track.Card);
            return track;
        }

        private Button AddLine(Track track, string caption, string buttonName, Func<string> exact)
        {
            var line = new VisualElement();
            line.AddToClassList("exp-line");
            line.Add(MarketPanelParts.NewLabel(buttonName + "-caption", "exp-line__caption", caption));
            var button = MarketPanelParts.NewValueButton(buttonName, "exp-value");
            line.Add(button);
            track.Card.Add(line);
            exactHandles.Add(shell.AttachExactValue(button, exact));
            return button;
        }

        private static void UpdateTrack(Track track, ExpansionSnapshot expansion, string valueName, string unit)
        {
            track.Level.text = "Level " + expansion.Level + " of " + expansion.MaxLevel;
            var now = QuantityFormatter.Format(expansion.CurrentValue);
            var nowExact = QuantityFormatter.FormatExact(expansion.CurrentValue);
            if (expansion.HasNext)
            {
                track.Value.text = now + " > " + QuantityFormatter.Format(expansion.NextValue);
                track.ValueExact = valueName + " " + nowExact + " " + unit + ", next level " + QuantityFormatter.FormatExact(expansion.NextValue) + " " + unit;
                track.Cost.text = MarketPanelParts.Money(expansion.NextCost);
                track.CostExact = MarketPanelParts.MoneyExact(expansion.NextCost) + " Cash";
            }
            else
            {
                track.Value.text = now + " (max)";
                track.ValueExact = valueName + " " + nowExact + " " + unit + ", the maximum level";
                track.Cost.text = "-";
                track.CostExact = "There is no next level.";
            }

            // Disabled before the tap, with the reason visible; the command still re-validates when activated.
            var unavailable = !expansion.HasNext || !expansion.Affordable;
            RunShellDocument.SetSoftDisabled(track.Upgrade, unavailable);
            track.Reason.text = expansion.RejectionReason == null ? string.Empty : RunRejectionCopy.For(expansion.RejectionReason);
            MarketPanelParts.SetVisible(track.Reason, expansion.RejectionReason != null);
        }

        private void OnUpgrade(Track track, Func<RunCommandResult<ExpansionResult>> upgrade)
        {
            var result = upgrade();
            if (result.IsSuccess)
            {
                track.Status.text = track.Key == "storage"
                    ? "Storage is now level " + result.Value.NewLevel + ": capacity " + QuantityFormatter.Format(result.Value.NewValue) + "."
                    : "The factory is now level " + result.Value.NewLevel + ": " + QuantityFormatter.Format(result.Value.NewValue) + " slots.";
                Render(true);
            }
            else
            {
                // The state is unchanged; the rejection copy shows next to the control that was used.
                track.Status.text = string.Empty;
                Render(true);
                track.Reason.text = RunRejectionCopy.For(result.ReasonCode);
                MarketPanelParts.SetVisible(track.Reason, true);
            }
        }
    }
}
