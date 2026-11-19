using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI.Panels;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// The hub part of the run shell (T535, FR-020 to FR-022, research R11, OD-9b): the "Office" entry of the persistent layer with its attention dot, and
    /// the open, toggle and Overdrive hand-over the hub cards use. It lives in its own file so <c>RunShellDocument.cs</c> does not grow further (T534
    /// Architect finding 3); the hub's logic is in <see cref="HubPanel"/>. The shell only wires: it opens the hub as the one sheet, through the
    /// existing card close and <see cref="CloseSheet"/> paths, and holds no rule.
    /// </summary>
    public sealed partial class RunShellDocument
    {
        /// <summary>The bullet glyph of the hub entry's attention dot: a shape, so the dot never rests on colour alone.</summary>
        public const string DotGlyph = "•";

        private VisualElement hubEntryIcon;
        private Label hubEntryDot;
        private Label hubEntryLabel;

        /// <summary>The system-attention state of the hub: the feeds write it, the hub entry dot and the hub cards show it. It outlives a shell rebuild.</summary>
        public HubAttention Attention { get; } = new HubAttention();

        /// <summary>The hub entry of the persistent layer (bottom row, right column or strip, see <see cref="PersistentLayer"/>): the icon and the visible word "Office" (never the icon alone), with the attention dot.</summary>
        public Button HubEntryButton { get; private set; }

        /// <summary>The visible text of the hub entry ("Office").</summary>
        public string HubEntryText => hubEntryLabel != null ? hubEntryLabel.text : string.Empty;

        /// <summary>True while the attention dot of the hub entry shows.</summary>
        public bool IsHubDotShown => hubEntryDot != null && hubEntryDot.style.display == DisplayStyle.Flex;

        /// <summary>True while the hub is the open sheet or side-panel view.</summary>
        public bool IsHubOpen => sheetOpen && activePanelId == ShellPanelIds.Hub;

        // The hub, the Industry view and Settings are system views that Back and Close pop in one step (the back order: hub, system view or Settings).
        private bool IsHubFamilyOpen => sheetOpen && (activePanelId == ShellPanelIds.Hub || activePanelId == ShellPanelIds.Industry);

        // U-3: the system view a hub card opened; closing it returns focus to the Office entry (the invoker).
        private string hubInvokedPanelId;

        /// <summary>Called by the hub after a card opened a system view, so that closing the view returns focus to the entry.</summary>
        public void NoteOpenedFromHub() => hubInvokedPanelId = activePanelId;

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

        // The hub entry dot shows any system flag (Contracts, Research, Prestige, Overdrive).
        private void OnAttentionChanged()
        {
            if (HubEntryButton == null)
            {
                return;
            }

            var any = Attention.Any;
            hubEntryDot.text = any ? DotGlyph : string.Empty;
            hubEntryDot.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            HubEntryButton.tooltip = any
                ? HubCards.HubLabel + ", " + Attention.Count + (Attention.Count == 1 ? " needs attention" : " need attention")
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
