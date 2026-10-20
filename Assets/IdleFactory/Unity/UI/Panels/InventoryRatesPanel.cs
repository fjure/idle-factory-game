using System;
using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>
    /// The Inventory and rates surface (FR-006, FR-011, FR-012, FR-035; contracts/ui-surfaces.md). Per unlocked resource it
    /// shows the stock, the net rate per minute, the sale price and the auto-sell toggle, and it shows the store usage as a
    /// bar with a number. Locked resources are not listed. Compact numbers are floored by <see cref="QuantityFormatter"/> and
    /// give their exact value through the shell detail line (UX-8). The panel reads the run only through
    /// <see cref="RunSnapshots"/> and changes the auto-sell setting only through <see cref="RunCommands.SetAutoSell"/>; a
    /// rejection (for example <c>value.limit</c>) shows its copy beside the toggle and the state stays unchanged.
    /// </summary>
    public sealed partial class InventoryRatesPanel : IShellPanel
    {
        public const string EmptyText = "Nothing stored yet";

        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly RunShellDocument shell;
        private readonly Func<string, Sprite> icons;
        private readonly VisualElement root;
        private readonly Button storeValue;
        private readonly VisualElement storeSection;
        private readonly VisualElement storeFill;
        private readonly Label storeFull;
        private readonly Label emptyLabel;
        private readonly VisualElement list;
        private readonly Label reasonLabel;
        private readonly Dictionary<string, Row> rows = new Dictionary<string, Row>(StringComparer.Ordinal);
        private readonly List<string> rowOrder = new List<string>();
        private readonly Dictionary<string, LockedRow> lockedRows = new Dictionary<string, LockedRow>(StringComparer.Ordinal);
        private readonly List<string> lockedOrder = new List<string>();
        private readonly List<IDisposable> exactHandles = new List<IDisposable>();
        private string storeExact = string.Empty;
        private long structureRevision = -1;
        private long quantityRevision = -1;
        private float lastRender = float.NegativeInfinity;

        /// <summary>A locked resource: its name and the lock line, with no stock, price or sale controls (T362, #382).</summary>
        private sealed class LockedRow
        {
            public VisualElement Root;
            public Label Title;
            public Label Hint;
        }

        private sealed class Row
        {
            public string ResourceId;
            public VisualElement Root;
            public Label Title;
            public Button Stock;
            public Button Net;
            public Button Price;
            public Button AutoSell;
            public Label Reason;
            public Label Limit;
            public VisualElement SellBlock;
            public Button SellStep;
            public Button Sell;
            public Label SellHint;
            public Label SellReason;
            public Button SellResult;
            public string SellResultExact = string.Empty;
            public string StockExact = string.Empty;
            public string NetExact = string.Empty;
            public string PriceExact = string.Empty;
            public bool AutoSellOn;
        }

        public InventoryRatesPanel(
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
            this.icons = icons;
            Id = panelId;
            root = MarketPanelParts.CreateRoot(tree, styles, "inventory-rates-panel", "inv-panel");
            MarketPanelParts.Ensure<Label>(root, "inventory-heading", "inv-heading").text = "Inventory and rates";
            storeSection = MarketPanelParts.Ensure<VisualElement>(root, "inventory-store", "inv-store");
            storeValue = MarketPanelParts.Ensure<Button>(storeSection, "inventory-store-value", "inv-value");
            storeValue.AddToClassList("run-control");
            storeValue.AddToClassList("inv-value");
            var bar = MarketPanelParts.Ensure<VisualElement>(storeSection, "inventory-store-bar", "inv-store__bar");
            storeFill = MarketPanelParts.Ensure<VisualElement>(bar, "inventory-store-fill", "inv-store__fill");
            storeFull = MarketPanelParts.Ensure<Label>(storeSection, "inventory-store-full", "inv-text");
            storeFull.text = "The store is full. Machines that cannot add to it wait.";
            emptyLabel = MarketPanelParts.Ensure<Label>(root, "inventory-empty", "inv-text");
            emptyLabel.text = EmptyText;
            list = MarketPanelParts.Ensure<VisualElement>(root, "inventory-list", "inv-list");
            reasonLabel = MarketPanelParts.Ensure<Label>(root, "inventory-reason", "inv-caption");
            exactHandles.Add(shell.AttachExactValue(storeValue, () => storeExact));
        }

        public string Id { get; }
        public string Title => "Inventory and rates";
        public VisualElement Root => root;

        /// <summary>The unlocked resource IDs that have a full row now, in display order. Locked resources are in <see cref="LockedResources"/>.</summary>
        public IReadOnlyList<string> ListedResources => rowOrder;

        /// <summary>The locked resource IDs shown as "Locked" rows after the unlocked ones (T362, #382).</summary>
        public IReadOnlyList<string> LockedResources => lockedOrder;

        /// <summary>The minimum time between two renders caused by a stock or rate change alone (default 0.25 s; tests set 0).</summary>
        public float RefreshIntervalSeconds { get; set; } = 0.25f;

        public bool IsEmptyShown => emptyLabel.style.display != DisplayStyle.None;
        public Button StoreValue => storeValue;
        public string ReasonText => reasonLabel.text;

        public Button StockButton(string resourceId) => rows.TryGetValue(resourceId, out var row) ? row.Stock : null;
        public Button NetButton(string resourceId) => rows.TryGetValue(resourceId, out var row) ? row.Net : null;
        public Button PriceButton(string resourceId) => rows.TryGetValue(resourceId, out var row) ? row.Price : null;
        public Button AutoSellButton(string resourceId) => rows.TryGetValue(resourceId, out var row) ? row.AutoSell : null;
        public Label RowReason(string resourceId) => rows.TryGetValue(resourceId, out var row) ? row.Reason : null;
        public Label RowLimit(string resourceId) => rows.TryGetValue(resourceId, out var row) ? row.Limit : null;

        public void OnShown(ShellPanelContext context) => Render(true);

        public void Refresh()
        {
            // A structural change renders at once; stock and rate changes are coalesced to the refresh interval so a fast simulation
            // never rebuilds the snapshots every frame.
            if (session.StructureRevision != structureRevision ||
                (session.QuantityRevision != quantityRevision && Time.realtimeSinceStartup - lastRender >= RefreshIntervalSeconds))
            {
                Render(false);
            }
        }

        public void OnHidden()
        {
            SetReason(reasonLabel, string.Empty);
            foreach (var row in rows.Values)
            {
                SetReason(row.Reason, string.Empty);
                MarketPanelParts.SetVisible(row.Reason, false);
                ClearSellFeedback(row);
            }
        }

        /// <summary>Sets a rejection text and its warning icon together (T243, FR-032): an empty text clears the icon.</summary>
        private void SetReason(Label label, string text)
        {
            label.text = text;
            PanelIcons.Mark(label, icons, string.IsNullOrEmpty(text) ? null : PanelIcons.Warning);
        }

        /// <summary>Re-reads the run and updates the surface in place, so a focused control keeps its focus.</summary>
        public void Render(bool force)
        {
            structureRevision = session.StructureRevision;
            quantityRevision = session.QuantityRevision;
            lastRender = Time.realtimeSinceStartup;
            var rates = RunSnapshots.Rates(session);
            var economy = RunSnapshots.Economy(session);
            var byId = new Dictionary<string, ResourceEconomy>(StringComparer.Ordinal);
            for (var i = 0; i < economy.Resources.Count; i++)
            {
                byId[economy.Resources[i].ResourceId] = economy.Resources[i];
            }

            var visible = new List<ResourceRates>();
            for (var i = 0; i < rates.Resources.Count; i++)
            {
                // Full rows are for unlocked resources only (the rates snapshot already lists unlocked resources only; the economy confirms it).
                if (byId.TryGetValue(rates.Resources[i].ResourceId, out var eco) && eco.Unlocked)
                {
                    visible.Add(rates.Resources[i]);
                }
            }

            // A locked resource keeps a short "Locked" row with its unlock condition instead of disappearing (contracts/ui-surfaces-f005.md, Sell row).
            var locked = new List<string>();
            for (var i = 0; i < economy.Resources.Count; i++)
            {
                if (!economy.Resources[i].Unlocked)
                {
                    locked.Add(economy.Resources[i].ResourceId);
                }
            }

            SyncRows(visible, locked, byId);
            for (var i = 0; i < visible.Count; i++)
            {
                UpdateRow(rows[visible[i].ResourceId], visible[i], byId[visible[i].ResourceId]);
                UpdateSell(rows[visible[i].ResourceId], byId[visible[i].ResourceId], economy);
            }

            UpdateStore(rates);
            MarketPanelParts.SetVisible(emptyLabel, rates.StoreUsed == 0);
        }

        private void SyncRows(List<ResourceRates> visible, List<string> locked, Dictionary<string, ResourceEconomy> byId)
        {
            var same = visible.Count == rowOrder.Count && locked.Count == lockedOrder.Count;
            for (var i = 0; same && i < visible.Count; i++)
            {
                same = string.Equals(visible[i].ResourceId, rowOrder[i], StringComparison.Ordinal);
            }

            for (var i = 0; same && i < locked.Count; i++)
            {
                same = string.Equals(locked[i], lockedOrder[i], StringComparison.Ordinal);
            }

            if (same)
            {
                return;
            }

            MarketPanelParts.DisposeAll(exactHandles);
            exactHandles.Add(shell.AttachExactValue(storeValue, () => storeExact));
            list.Clear();
            rows.Clear();
            rowOrder.Clear();
            lockedRows.Clear();
            lockedOrder.Clear();
            for (var i = 0; i < visible.Count; i++)
            {
                var id = visible[i].ResourceId;
                var row = BuildRow(id, byId[id].Label);
                rows[id] = row;
                rowOrder.Add(id);
                list.Add(row.Root);
            }

            for (var i = 0; i < locked.Count; i++)
            {
                var id = locked[i];
                var row = BuildLockedRow(id, byId[id].Label);
                lockedRows[id] = row;
                lockedOrder.Add(id);
                list.Add(row.Root);
            }
        }

        private LockedRow BuildLockedRow(string id, string label)
        {
            var row = new LockedRow { Root = new VisualElement { name = "inventory-locked-" + id } };
            row.Root.AddToClassList("inv-row");
            row.Root.AddToClassList("inv-row--locked");
            row.Title = MarketPanelParts.NewLabel("inventory-title-" + id, "inv-row__title", label);
            row.Root.Add(row.Title);
            // The same name as the sale state line of an unlocked row, so one accessor serves both (SellHint).
            row.Hint = MarketPanelParts.NewLabel("inventory-sell-hint-" + id, "inv-caption");
            row.Hint.AddToClassList("inv-caption--locked");
            row.Hint.text = UnlockCopy.LockedLine(session.Catalog, id);
            row.Hint.tooltip = label + ". " + row.Hint.text;
            PanelIcons.Mark(row.Hint, icons, PanelIcons.Lock);
            row.Root.Add(row.Hint);
            return row;
        }

        private Row BuildRow(string id, string label)
        {
            var row = new Row { ResourceId = id };
            row.Root = new VisualElement { name = "inventory-row-" + id };
            row.Root.AddToClassList("inv-row");
            row.Title = MarketPanelParts.NewLabel("inventory-title-" + id, "inv-row__title", label);
            if (icons != null && session.Catalog.Resources.TryGetValue(id, out var def))
            {
                // The resource glyph sits at the left edge of the title (an Art Bible resource icon, never generated here).
                MarketPanelParts.SetIcon(row.Title, icons, def.PresentationKey);
                row.Title.AddToClassList("inv-row__title--icon");
            }

            row.Root.Add(row.Title);

            row.Stock = AddLine(row, "Stock", "inventory-stock-" + id, () => row.StockExact);
            row.Net = AddLine(row, "Net rate", "inventory-net-" + id, () => row.NetExact);
            row.Price = AddLine(row, "Sells for", "inventory-price-" + id, () => row.PriceExact);

            BuildSellControls(row, label);

            row.AutoSell = RunShellDocument.CreateButton("inventory-autosell-" + id, string.Empty);
            row.AutoSell.AddToClassList("inv-autosell");
            row.AutoSell.clicked += () => OnAutoSell(row);
            row.Root.Add(row.AutoSell);
            AddSellFeedback(row);

            row.Reason = MarketPanelParts.NewLabel("inventory-reason-" + id, "inv-caption");
            MarketPanelParts.SetVisible(row.Reason, false);
            row.Root.Add(row.Reason);
            row.Limit = MarketPanelParts.NewLabel("inventory-limit-" + id, "inv-caption");
            row.Limit.AddToClassList("inv-caption--limit");
            MarketPanelParts.SetVisible(row.Limit, false);
            row.Root.Add(row.Limit);
            return row;
        }

        private Button AddLine(Row row, string caption, string buttonName, Func<string> exact)
        {
            var line = new VisualElement();
            line.AddToClassList("inv-line");
            line.Add(MarketPanelParts.NewLabel(buttonName + "-caption", "inv-line__caption", caption));
            var button = MarketPanelParts.NewValueButton(buttonName, "inv-value");
            line.Add(button);
            row.Root.Add(line);
            exactHandles.Add(shell.AttachExactValue(button, exact));
            return button;
        }

        private void UpdateRow(Row row, ResourceRates rates, ResourceEconomy economy)
        {
            row.StockExact = QuantityFormatter.FormatExact(rates.Units) + " units in the store";
            row.Stock.text = QuantityFormatter.Format(rates.Units);

            var net = rates.NetPerMinuteMicro;
            row.NetExact = MarketPanelParts.FormatExactRate(net, true) + " per minute (production " +
                MarketPanelParts.FormatExactRate(rates.ProductionPerMinuteMicro, false) + ", use " +
                MarketPanelParts.FormatExactRate(rates.ConsumptionPerMinuteMicro, false) + ")";
            row.Net.text = (net > 0 ? "+" : string.Empty) + rates.NetText + " /min";

            row.PriceExact = economy.PriceValid
                ? MarketPanelParts.MoneyExact(economy.UnitPrice) + " per unit; the whole stock is worth " + MarketPanelParts.MoneyExact(economy.StockValue)
                : RunRejectionCopy.For(RunReasons.ValueLimit);
            row.Price.text = economy.PriceValid ? MarketPanelParts.Money(economy.UnitPrice) + " each" : "-";

            row.AutoSellOn = economy.AutoSell;
            row.AutoSell.text = "Auto-sell: " + (economy.AutoSell ? "On" : "Off");
            row.AutoSell.EnableInClassList("inv-autosell--on", economy.AutoSell);
            RunShellDocument.SetSoftDisabled(row.AutoSell, !economy.CanToggleAutoSell);

            var overflow = rates.Units >= ExactBound.Value || Math.Abs(rates.ProductionPerMinuteMicro) >= ExactBound.Value ||
                Math.Abs(rates.ConsumptionPerMinuteMicro) >= ExactBound.Value || !economy.PriceValid;
            // The overflow caption is the value.limit rejection copy that contracts/ui-surfaces.md names for this surface, so it carries the warning.
            SetReason(row.Limit, overflow ? RunRejectionCopy.For(RunReasons.ValueLimit) : string.Empty);
            MarketPanelParts.SetVisible(row.Limit, overflow);
        }

        private void UpdateStore(RatesSnapshot rates)
        {
            storeExact = QuantityFormatter.FormatExact(rates.StoreUsed) + " of " + QuantityFormatter.FormatExact(rates.StoreCapacity) + " units stored";
            storeValue.text = "Store " + QuantityFormatter.Format(rates.StoreUsed) + " / " + QuantityFormatter.Format(rates.StoreCapacity);
            storeFill.style.width = Length.Percent(Mathf.Clamp(rates.StoreUsagePpm / 10_000f, 0f, 100f));
            var full = rates.StoreCapacity > 0 && rates.StoreUsed >= rates.StoreCapacity;
            storeSection.EnableInClassList("inv-store--full", full);
            MarketPanelParts.SetVisible(storeFull, full);
        }

        private void OnAutoSell(Row row)
        {
            var result = commands.SetAutoSell(row.ResourceId, !row.AutoSellOn);
            if (result.IsSuccess)
            {
                SetReason(row.Reason, string.Empty);
                MarketPanelParts.SetVisible(row.Reason, false);
            }
            else
            {
                // A rejection leaves the setting unchanged and says why next to the control that was used (value.limit included).
                SetReason(row.Reason, RunRejectionCopy.For(result.ReasonCode));
                MarketPanelParts.SetVisible(row.Reason, true);
            }

            Render(true);
        }
    }
}
