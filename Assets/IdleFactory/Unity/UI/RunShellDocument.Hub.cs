using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI.Panels;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// The hub part of the run shell (T535, FR-020 to FR-022, research R11, OD-9b): the "Office" entry in the top strip with its attention dot, and
    /// the open, toggle and Overdrive hand-over the hub cards use. It lives in its own file so <c>RunShellDocument.cs</c> does not grow further (T534
    /// Architect finding 3); the hub's logic is in <see cref="HubPanel"/>. The shell only wires: it opens the hub as the one sheet, through the
    /// existing card close and <see cref="CloseSheet"/> paths, and holds no rule.
    /// </summary>
    public sealed partial class RunShellDocument
    {
        private VisualElement hubEntryIcon;
        private Label hubEntryDot;
        private Label hubEntryLabel;

        /// <summary>The system-attention state of the hub: the feeds write it, the hub entry dot and the hub cards show it. It outlives a shell rebuild.</summary>
        public HubAttention Attention { get; } = new HubAttention();

        /// <summary>The hub entry in the top strip: the icon and the visible word "Office" (never the icon alone), with the attention dot.</summary>
        public Button HubEntryButton { get; private set; }

        /// <summary>The visible text of the hub entry ("Office").</summary>
        public string HubEntryText => hubEntryLabel != null ? hubEntryLabel.text : string.Empty;

        /// <summary>True while the attention dot of the hub entry shows.</summary>
        public bool IsHubDotShown => hubEntryDot != null && hubEntryDot.style.display == DisplayStyle.Flex;

        /// <summary>True while the hub is the open sheet or side-panel view.</summary>
        public bool IsHubOpen => sheetOpen && activePanelId == ShellPanelIds.Hub;

        // The hub, the Industry view and Settings are system views that Back and Close pop in one step (the back order: hub, system view or Settings).
        private bool IsHubFamilyOpen => sheetOpen && (activePanelId == ShellPanelIds.Hub || activePanelId == ShellPanelIds.Industry);

        /// <summary>
        /// True while the entry sits on a row of its own at the top of the world column (phone portrait): the camera frames below it like a notice bar.
        /// </summary>
        public bool HubEntryOnItsOwnRow => HubEntryButton != null && worldColumn != null && HubEntryButton.parent == worldColumn && !hubEntryOverlay;

        /// <summary>The least world height, in points, that must remain for the camera below the entry row; below it the row is not cut from the camera's rectangle.</summary>
        public const float MinWorldBelowHubRow = 120f;

        private bool hubEntryOverlay;

        // Where the entry lives (T537 rebuilds the persistent layer; this keeps every T534 and F005 layout invariant meanwhile):
        // - phone portrait: a 48 dp row of its own under the strip, because the strip's four controls already fill 360 pt at the worst-case values;
        // - phone landscape from 120 percent text: an overlay at the bottom of the world column, beside the zoom column, because the strip wraps there and a
        //   fifth control would add a line that the machine card (Upgrade in the first view at 640x360 and 200 percent) cannot give up;
        // - everything else: in the strip, before the gear.
        private void PlaceHubEntry()
        {
            if (HubEntryButton == null || worldColumn == null)
            {
                return;
            }

            var ownRow = Layout == FactoryHudLayout.CompactPortrait;
            var overlay = Layout == FactoryHudLayout.CompactLandscape && TextScalePercent >= 120;
            var home = ownRow || overlay ? worldColumn : topStrip;
            if (HubEntryButton.parent != home)
            {
                if (home == worldColumn)
                {
                    worldColumn.Insert(0, HubEntryButton);
                }
                else
                {
                    topStrip.Insert(topStrip.IndexOf(SettingsButton), HubEntryButton);
                }
            }

            hubEntryOverlay = overlay;
            HubEntryButton.EnableInClassList("run-hub-entry--row", ownRow);
            HubEntryButton.EnableInClassList("run-hub-entry--overlay", overlay);
        }

        private bool QueryHub()
        {
            HubEntryButton = root.Q<Button>("hub-entry");
            hubEntryIcon = root.Q<VisualElement>("hub-entry-icon");
            hubEntryLabel = root.Q<Label>("hub-entry-label");
            hubEntryDot = root.Q<Label>("hub-entry-dot");
            return HubEntryButton != null && hubEntryIcon != null && hubEntryLabel != null && hubEntryDot != null;
        }

        private void ApplyHubIcon() => SetIcon(hubEntryIcon, HubCards.EntryIconKey);

        private void BindHub()
        {
            HubEntryButton.clicked += ToggleHub;
            Attention.Changed += OnAttentionChanged;
            OnAttentionChanged();
        }

        private void DisposeHub()
        {
            Attention.Changed -= OnAttentionChanged;
        }

        // The contracts count still drives the tab badge until T537 removes the tab bar; the hub entry dot shows any system flag.
        private void OnAttentionChanged()
        {
            if (HubEntryButton == null)
            {
                return;
            }

            contractsDeliverable = Attention.ContractsDeliverable;
            ApplyContractsBadge();
            var any = Attention.Any;
            hubEntryDot.text = any ? DotGlyph : string.Empty;
            hubEntryDot.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            HubEntryButton.tooltip = any
                ? HubCards.HubLabel + ", " + Attention.Count + (Attention.Count == 1 ? " system needs attention" : " systems need attention")
                : HubCards.HubLabel;
        }

        private void ToggleHub()
        {
            if (IsHubOpen)
            {
                CloseSheet();
            }
            else
            {
                OpenHub();
            }
        }

        /// <summary>
        /// Opens the hub as the one sheet (phone portrait: the bottom sheet; other classes: the side panel). It is exclusive with the machine card and
        /// the build sheet: an open card closes through <see cref="CloseMachineCard"/> and any other sheet through <see cref="CloseSheet"/>, so the
        /// selection clears and the camera restores exactly as for those paths. Focus moves to the first card. Does nothing while the shell is not ready.
        /// </summary>
        public void OpenHub()
        {
            if (!EnsureReady())
            {
                return;
            }

            if (cardOpen)
            {
                CloseMachineCard(false);
            }

            if (sheetOpen)
            {
                CloseSheet(false);
            }

            activeTab = ShellTab.Factory;
            sheetOpen = true;
            ShowPanel(ShellPanelIds.Hub, HubCards.HubLabel, -1);
            UpdateSheetState();
            if (activePanel is HubPanel hub)
            {
                FocusWhenReady(hub.CardButton(HubCard.Market));
            }
        }

        /// <summary>Opens the Industry system view (the additive registry id) as the one sheet, replacing the hub or any other sheet.</summary>
        public void OpenIndustry()
        {
            if (!EnsureReady())
            {
                return;
            }

            ClearTransientNotice();
            DismissCard(false);
            detailsFromCard = false;
            SetSelectedSlot(-1);
            activeTab = ShellTab.Factory;
            sheetOpen = true;
            ShowPanel(ShellPanelIds.Industry, "Industry", -1);
            UpdateSheetState();
        }

        /// <summary>Activates Overdrive as the strip control does (the same soft-disabled check, notice and request event).</summary>
        public void ActivateOverdrive() => OnOverdriveClicked();
    }
}
