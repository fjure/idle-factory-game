using System;
using System.Collections.Generic;
using System.Globalization;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>
    /// The Industry surface (T116, FR-023, FR-032; contracts/ui-surfaces.md, Industry): the IP balance and the permanent upgrades with
    /// their level, cost and effect. A starting effect (starting Cash or storage level) carries the "Applies next run" tag, because it
    /// changes only the next run start (R12), and the surface says what the next run starts with. An upgrade that is unaffordable or at
    /// its maximum is shown disabled with its reason before the tap; the command still re-validates it.
    /// The panel reads <see cref="RunSnapshots.Industry"/> and mutates only through <see cref="RunCommands.PurchaseIndustry"/>.
    /// <para>
    /// The shell has six tabs and no Industry tab, so the surface mounts as a section: <see cref="ResearchPanel"/> hosts it under its
    /// graph (pass it to the constructor), or the composition root may add <see cref="Root"/> to another panel such as Prestige. Standing
    /// alone it registers as <see cref="PanelId"/> (the Research panel ID by default).
    /// </para>
    /// </summary>
    public sealed class IndustryPanel : IShellPanel
    {
        public const string PanelTitle = "Industry";
        public const string AppliesNextRunTag = "Applies next run";

        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly Func<string, Sprite> icons;
        private readonly Label ipLabel;
        private readonly Label nextRunLabel;
        private readonly Label hintLabel;
        private readonly Dictionary<string, UpgradeRow> rows = new Dictionary<string, UpgradeRow>(StringComparer.Ordinal);
        private readonly List<UpgradeRow> rowOrder = new List<UpgradeRow>();
        private RunShellDocument shell;
        private IndustrySnapshot snapshot;
        private long keyStructure = -1;
        private long keyQuantity = -1;
        private bool forceRefresh = true;

        /// <summary>Builds the surface. <paramref name="styles"/> is <c>IndustryPanel.uss</c>; <paramref name="panelId"/> is the shell panel ID when it is registered on its own.</summary>
        public IndustryPanel(RunSession session, RunCommands commands, StyleSheet styles = null, Func<string, Sprite> icons = null,
            string panelId = ShellPanelIds.Research)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.icons = icons;
            PanelId = panelId;
            var root = new VisualElement { name = "industry-panel" };
            root.AddToClassList("industry-panel");
            if (styles != null)
            {
                root.styleSheets.Add(styles);
            }

            Root = root;

            var title = MakeText("industry-section-title", "industry-section-title");
            title.text = PanelTitle;
            root.Add(title);

            var header = new VisualElement { name = "industry-header" };
            header.AddToClassList("industry-card");
            ipLabel = MakeText("industry-ip", "industry-value");
            nextRunLabel = MakeText("industry-next-run", "industry-line");
            hintLabel = MakeText("industry-hint", "industry-hint");
            hintLabel.text = "Industry points (IP) come from selling your factory. Upgrades last across runs.";
            header.Add(ipLabel);
            header.Add(nextRunLabel);
            header.Add(hintLabel);
            root.Add(header);

            foreach (var upgrade in RunSnapshots.Industry(session).Upgrades)
            {
                var row = new UpgradeRow(upgrade, this);
                rows.Add(upgrade.UpgradeId, row);
                rowOrder.Add(row);
                root.Add(row.Card);
            }
        }

        /// <summary>The shell panel ID this panel registers as when it stands alone.</summary>
        public string PanelId { get; }

        public string Id => PanelId;
        public string Title => PanelTitle;
        public VisualElement Root { get; }

        public IndustrySnapshot Snapshot => snapshot;
        public Label IpLabel => ipLabel;
        public Label NextRunLabel => nextRunLabel;

        /// <summary>The upgrade IDs in the order the surface shows them (ordinal ID order).</summary>
        public IReadOnlyList<string> UpgradeOrder
        {
            get
            {
                var list = new List<string>(rowOrder.Count);
                foreach (var row in rowOrder)
                {
                    list.Add(row.UpgradeId);
                }

                return list;
            }
        }

        public VisualElement Card(string upgradeId) => rows[upgradeId].Card;
        public Label TitleLabel(string upgradeId) => rows[upgradeId].Title;
        public Label LevelLabel(string upgradeId) => rows[upgradeId].Level;
        public Label EffectLabel(string upgradeId) => rows[upgradeId].Effect;
        public Label CostLabel(string upgradeId) => rows[upgradeId].Cost;
        public Label TagLabel(string upgradeId) => rows[upgradeId].Tag;
        public Label ReasonLabel(string upgradeId) => rows[upgradeId].Reason;
        public Label FeedbackLabel(string upgradeId) => rows[upgradeId].Feedback;
        public Button BuyButton(string upgradeId) => rows[upgradeId].Buy;

        public void OnShown(ShellPanelContext context)
        {
            shell = context?.Shell;
            Invalidate();
            Refresh();
        }

        public void OnHidden()
        {
        }

        public void Invalidate() => forceRefresh = true;

        /// <summary>Redraws when the structure or the quantities changed; the shell calls it every frame while visible.</summary>
        public void Refresh()
        {
            if (!forceRefresh && session.StructureRevision == keyStructure && session.QuantityRevision == keyQuantity)
            {
                return;
            }

            forceRefresh = false;
            keyStructure = session.StructureRevision;
            keyQuantity = session.QuantityRevision;
            Render(RunSnapshots.Industry(session));
        }

        /// <summary>Industry has no pending selection or confirmation, so a rotation has nothing to cancel. Returns false.</summary>
        public bool CancelForRotation() => false;

        private void Render(IndustrySnapshot data)
        {
            snapshot = data;
            ipLabel.text = "Industry points: " + QuantityFormatter.Format(data.Ip) + " IP";
            nextRunLabel.text = "Next run starts with " + QuantityFormatter.Format(data.NextRunStartingCash) + " Cash and storage level " +
                data.NextRunStorageLevel.ToString(CultureInfo.InvariantCulture) + ".";
            foreach (var upgrade in data.Upgrades)
            {
                rows[upgrade.UpgradeId].Render(upgrade);
            }
        }

        private void OnBuy(UpgradeRow row)
        {
            row.ClearFeedback();
            var result = commands.PurchaseIndustry(row.UpgradeId);
            if (!result.IsSuccess)
            {
                row.ShowFeedback(RunRejectionCopy.For(result.ReasonCode));
                Invalidate();
                Refresh();
                return;
            }

            var bought = result.Value;
            Invalidate();
            Refresh();
            shell?.ShowNotice(row.Label + " is now level " + bought.NewLevel.ToString(CultureInfo.InvariantCulture) +
                (bought.AppliesNextRun ? ". It applies next run." : "."), ShellNoticeKind.Info);
        }

        private static Label MakeText(string name, string className)
        {
            var label = new Label { name = name };
            label.AddToClassList(className);
            return label;
        }

        private static void SetDisplayed(VisualElement element, bool displayed) =>
            element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;

        private sealed class UpgradeRow
        {
            private readonly IndustryPanel owner;

            public UpgradeRow(IndustryUpgradeSnapshot upgrade, IndustryPanel owner)
            {
                this.owner = owner;
                UpgradeId = upgrade.UpgradeId;
                Label = upgrade.Label;
                Card = new VisualElement { name = "industry-upgrade-" + upgrade.UpgradeId };
                Card.AddToClassList("industry-card");
                Title = MakeText("industry-title-" + upgrade.UpgradeId, "industry-title");
                Level = MakeText("industry-level-" + upgrade.UpgradeId, "industry-line");
                Effect = MakeText("industry-effect-" + upgrade.UpgradeId, "industry-line");
                Cost = MakeText("industry-cost-" + upgrade.UpgradeId, "industry-line");
                Tag = MakeText("industry-tag-" + upgrade.UpgradeId, "industry-tag");
                Tag.text = AppliesNextRunTag;
                Reason = MakeText("industry-reason-" + upgrade.UpgradeId, "industry-hint");
                Feedback = MakeText("industry-feedback-" + upgrade.UpgradeId, "industry-feedback");
                Buy = RunShellDocument.CreateButton("industry-buy-" + upgrade.UpgradeId, "Upgrade", true);
                Buy.clicked += () => owner.OnBuy(this);
                Card.Add(Title);
                Card.Add(Level);
                Card.Add(Effect);
                Card.Add(Tag);
                Card.Add(Cost);
                Card.Add(Reason);
                Card.Add(Buy);
                Card.Add(Feedback);
                SetDisplayed(Tag, upgrade.AppliesNextRun);
                SetDisplayed(Reason, false);
                SetDisplayed(Feedback, false);
            }

            public string UpgradeId { get; }
            public string Label { get; }
            public VisualElement Card { get; }
            public Label Title { get; }
            public Label Level { get; }
            public Label Effect { get; }
            public Label Cost { get; }
            public Label Tag { get; }
            public Label Reason { get; }
            public Label Feedback { get; }
            public Button Buy { get; }

            public void Render(IndustryUpgradeSnapshot upgrade)
            {
                var catalog = owner.session.Catalog;
                Title.text = upgrade.Label;
                Level.text = "Level " + upgrade.Level.ToString(CultureInfo.InvariantCulture) + " of " + upgrade.MaxLevel.ToString(CultureInfo.InvariantCulture);
                Effect.text = ResearchEffectCopy.Describe(catalog, upgrade.EffectKind, upgrade.TargetKind, upgrade.TargetId, upgrade.EffectValue);
                var atMax = upgrade.Level >= upgrade.MaxLevel;
                Cost.text = atMax ? "Fully upgraded." : "Next level: " + QuantityFormatter.Format(upgrade.NextCost) + " IP";
                Buy.text = atMax ? "Max level" : "Upgrade for " + QuantityFormatter.Format(upgrade.NextCost) + " IP";
                var reason = upgrade.CanPurchase ? string.Empty : RunRejectionCopy.For(upgrade.PurchaseReason);
                Reason.text = reason;
                SetDisplayed(Reason, reason.Length > 0);
                RunShellDocument.SetSoftDisabled(Buy, !upgrade.CanPurchase);
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
