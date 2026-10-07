using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI
{
    /// <summary>How a notice line reads: plain information, a warning, or the copy of a rejected command.</summary>
    public enum ShellNoticeKind
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    /// <summary>
    /// The responsive shell of the Feature 003 run screen (T112): the top strip (Cash, RP, store usage, Overdrive), the six
    /// tabs, the bottom sheet or side panel that panels mount into, the shared confirmation dialog, the exact-value detail
    /// line and the notice line. It clones <c>FactoryRun.uxml</c>, owns no gameplay rule and reads state only through
    /// values handed to it by <see cref="RunShellPresenter"/>. T113 to T118 add panels through <see cref="Panels"/>
    /// without editing it.
    /// </summary>
    public sealed class RunShellDocument : MonoBehaviour
    {
        /// <summary>The one-line notice that a rotation cancelled an open transient action (UX-9).</summary>
        public const string RotationCancelledNotice = "Cancelled by rotation";

        /// <summary>Long-press duration of the number detail line (UI surfaces contract, Numbers).</summary>
        public const float LongPressSeconds = 0.5f;

        private static readonly ShellTab[] Tabs =
        {
            ShellTab.Factory, ShellTab.Market, ShellTab.Contracts, ShellTab.Items, ShellTab.Research, ShellTab.Prestige,
        };

        private static readonly string[] TabNames = { "Factory", "Market", "Contracts", "Items", "Research", "Prestige" };

        private static readonly string[] TabIconKeys =
        {
            "F003-UI-ICON-TAB-FACTORY", "F003-UI-ICON-TAB-MARKET", "F003-UI-ICON-TAB-CONTRACTS",
            "F003-UI-ICON-TAB-ITEMS", "F003-UI-ICON-TAB-RESEARCH", "F003-UI-ICON-TAB-PRESTIGE",
        };

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset visualTree;
        [SerializeField] private StyleSheet styleSheet;

        private readonly ShellPanelRegistry registry = new ShellPanelRegistry();
        private readonly Button[] tabButtons = new Button[6];
        private readonly List<IDisposable> exactValueHandles = new List<IDisposable>();
        private Func<string, Sprite> iconResolver = key => null;

        private VisualElement root;
        private VisualElement body;
        private VisualElement main;
        private VisualElement worldColumn;
        private VisualElement topStrip;
        private VisualElement panelHost;
        private VisualElement panelHeaderSlot;
        private VisualElement panelTabsSlot;
        private VisualElement tabBar;
        private VisualElement panelContent;
        private ScrollView panelScroll;
        private VisualElement dialogLayer;
        private VisualElement dialogCard;
        private VisualElement dialogIcon;
        private Label dialogTitle;
        private Label dialogMessage;
        private Label panelTitle;
        private Label detailLine;
        private Label noticeLabel;
        private Label cashValue;
        private Label rpValue;
        private Label storeValue;
        private Label overdriveCaption;
        private VisualElement storeBarFill;
        private VisualElement overdriveIcon;
        private VisualElement zoomColumn;
        private Label placeholderTitle;
        private Label placeholderReason;
        private VisualElement placeholder;
        private VisualElement focusRing;
        private VisualElement ringTarget;

        private bool built;
        private bool gameplayAvailable = true;
        private string unavailableReason = string.Empty;
        private bool sheetOpen;
        private ShellTab activeTab = ShellTab.Factory;
        private string activePanelId;
        private IShellPanel activePanel;
        private int selectedSlot = -1;
        private int lastBackFrame = -1;
        private int lastMoveFrame = -1;
        private int lastMoveDirection;
        private long cash;
        private long rp;
        private long storeUsed;
        private long storeCapacity;
        private OverdriveStatus overdriveStatus;

        // Number detail line.
        private VisualElement detailHost;
        private VisualElement pinnedHost;
        private Func<string> detailText;
        private readonly Dictionary<VisualElement, Func<string>> exactTexts = new Dictionary<VisualElement, Func<string>>();

        // Confirmation dialog.
        private ConfirmationRequest dialogRequest;
        private VisualElement dialogInvoker;

        public event Action<int> ZoomRequested;
        public event Action<int> SlotSelectionChanged;
        public event Action OverdriveRequested;

        /// <summary>Raised when the sheet or side panel shows another panel (or none), with the panel ID or null.</summary>
        public event Action<string> ActivePanelChanged;

        /// <summary>The panel mount registry (T113 to T118 register their panels here).</summary>
        public ShellPanelRegistry Panels => registry;

        public VisualElement Root => root;
        public FactoryHudLayout Layout { get; private set; } = FactoryHudLayout.CompactPortrait;
        public string Error { get; private set; } = "The run shell has not been configured.";
        public bool IsReady => built && string.IsNullOrEmpty(Error) && root != null;
        public bool IsSidePanelLayout => FactoryHudDocument.HasSidePanel(Layout);

        /// <summary>True while a sheet is open on a phone, or while the factory side panel shows a selection on tablet and desktop.</summary>
        public bool IsSheetOpen => sheetOpen;

        public ShellTab ActiveTab => activeTab;
        public string ActivePanelId => activePanelId;
        public int SelectedSlot => selectedSlot;
        public bool IsGameplayAvailable => gameplayAvailable;
        public bool IsConfirmationOpen => dialogRequest != null;

        public Button CashButton { get; private set; }
        public Button RpButton { get; private set; }
        public Button StoreButton { get; private set; }
        public Button OverdriveButton { get; private set; }
        public Button ZoomInButton { get; private set; }
        public Button ZoomOutButton { get; private set; }
        public Button CloseButton { get; private set; }
        public Button DialogCancelButton { get; private set; }
        public Button DialogConfirmButton { get; private set; }
        public VisualElement TopStrip => topStrip;
        public VisualElement PanelHost => panelHost;
        public VisualElement TabBar => tabBar;
        public VisualElement WorldColumn => worldColumn;
        public VisualElement PanelContent => panelContent;
        public ScrollView PanelScroll => panelScroll;
        public VisualElement DialogLayer => dialogLayer;
        public VisualElement DialogCard => dialogCard;

        /// <summary>The overlay that draws the two-part focus ring around <see cref="FocusRingTarget"/> (Art Bible 9.2).</summary>
        public VisualElement FocusRing => focusRing;

        /// <summary>The control the focus ring currently surrounds, or null.</summary>
        public VisualElement FocusRingTarget => ringTarget;
        public string NoticeText => noticeLabel?.text ?? string.Empty;
        public string DetailText => detailLine?.text ?? string.Empty;
        public bool IsDetailPinned => pinnedHost != null;
        public string PlaceholderText => placeholderTitle?.text ?? string.Empty;
        public string StatusText => cashValue == null ? string.Empty : $"{cashValue.text}|{rpValue.text}|{storeValue.text}|{overdriveCaption.text}";

        public Button TabButton(ShellTab tab) => tabButtons[(int)tab];

        public bool Configure(UIDocument document, VisualTreeAsset tree, StyleSheet styles, Func<string, Sprite> icons = null)
        {
            uiDocument = document;
            visualTree = tree;
            styleSheet = styles;
            iconResolver = icons ?? (key => null);
            return Build();
        }

        public bool Build()
        {
            Dispose();
            if (uiDocument == null || visualTree == null || styleSheet == null)
            {
                return Fail("The run shell requires explicit UIDocument, UXML and USS references.");
            }

            var documentRoot = uiDocument.rootVisualElement;
            if (documentRoot == null)
            {
                return Fail("The run shell UIDocument root is unavailable.");
            }

            // The UIDocument root resolves to zero height; pin it to the panel (wiki: UI Toolkit HUD layout pitfalls).
            documentRoot.style.position = Position.Absolute;
            documentRoot.style.left = 0f;
            documentRoot.style.top = 0f;
            documentRoot.style.right = 0f;
            documentRoot.style.bottom = 0f;

            visualTree.CloneTree(documentRoot);
            root = documentRoot.Q<VisualElement>("run-shell");
            if (root == null)
            {
                return Fail("The run shell UXML is missing its root element.");
            }

            body = root.Q<VisualElement>("run-body");
            main = root.Q<VisualElement>("run-main");
            worldColumn = root.Q<VisualElement>("world-column");
            topStrip = root.Q<VisualElement>("top-strip");
            panelHost = root.Q<VisualElement>("panel-host");
            panelHeaderSlot = root.Q<VisualElement>("panel-header-slot");
            panelTabsSlot = root.Q<VisualElement>("panel-tabs-slot");
            tabBar = root.Q<VisualElement>("tab-bar");
            panelContent = root.Q<VisualElement>("panel-content");
            panelScroll = root.Q<ScrollView>("panel-scroll");
            panelTitle = root.Q<Label>("panel-title");
            detailLine = root.Q<Label>("detail-line");
            noticeLabel = root.Q<Label>("notice");
            cashValue = root.Q<Label>("cash-value");
            rpValue = root.Q<Label>("rp-value");
            storeValue = root.Q<Label>("store-value");
            overdriveCaption = root.Q<Label>("overdrive-caption");
            storeBarFill = root.Q<VisualElement>("store-bar-fill");
            overdriveIcon = root.Q<VisualElement>("overdrive-icon");
            zoomColumn = root.Q<VisualElement>("zoom-column");
            dialogLayer = root.Q<VisualElement>("dialog-layer");
            focusRing = root.Q<VisualElement>("focus-ring");
            dialogCard = root.Q<VisualElement>("dialog");
            dialogIcon = root.Q<VisualElement>("dialog-icon");
            dialogTitle = root.Q<Label>("dialog-title");
            dialogMessage = root.Q<Label>("dialog-message");
            CashButton = root.Q<Button>("cash");
            RpButton = root.Q<Button>("rp");
            StoreButton = root.Q<Button>("store");
            OverdriveButton = root.Q<Button>("overdrive");
            ZoomInButton = root.Q<Button>("zoom-in");
            ZoomOutButton = root.Q<Button>("zoom-out");
            CloseButton = root.Q<Button>("panel-close");
            DialogCancelButton = root.Q<Button>("dialog-cancel");
            DialogConfirmButton = root.Q<Button>("dialog-confirm");
            for (var i = 0; i < Tabs.Length; i++)
            {
                tabButtons[i] = root.Q<Button>("tab-" + TabNames[i].ToLowerInvariant());
            }

            if (body == null || main == null || worldColumn == null || topStrip == null || panelHost == null ||
                panelHeaderSlot == null || panelTabsSlot == null || tabBar == null || panelContent == null ||
                panelScroll == null || panelTitle == null || detailLine == null || noticeLabel == null ||
                cashValue == null || rpValue == null || storeValue == null || overdriveCaption == null ||
                storeBarFill == null || overdriveIcon == null || zoomColumn == null || dialogLayer == null ||
                dialogCard == null || dialogIcon == null || focusRing == null || dialogTitle == null || dialogMessage == null ||
                CashButton == null || RpButton == null || StoreButton == null || OverdriveButton == null ||
                ZoomInButton == null || ZoomOutButton == null || CloseButton == null ||
                DialogCancelButton == null || DialogConfirmButton == null || Array.IndexOf(tabButtons, null) >= 0)
            {
                return Fail("The run shell UXML is malformed or missing required named elements.");
            }

            root.styleSheets.Add(styleSheet);
            BuildPlaceholder();
            BindCallbacks();
            ApplyIcons();
            built = true;
            Error = string.Empty;
            SetCash(0);
            SetRp(0, false);
            SetStore(0, 0);
            SetOverdrive(null);
            UpdateTabSelection();
            ShowPanel(null, null, -1);
            return true;
        }

        // ------------------------------------------------------------------------------------------------------
        // Layout
        // ------------------------------------------------------------------------------------------------------

        /// <summary>Applies the responsive layout from screen-pixel inputs, converting with the panel's pixels per point.</summary>
        public void ApplyScreenLayout(float screenWidth, float screenHeight, Rect screenSafeArea)
        {
            if (!IsReady || screenWidth <= 0f || screenHeight <= 0f)
            {
                return;
            }

            var pixelsPerPoint = root.panel != null ? root.panel.scaledPixelsPerPoint : 1f;
            if (pixelsPerPoint <= 0f || float.IsNaN(pixelsPerPoint) || float.IsInfinity(pixelsPerPoint))
            {
                pixelsPerPoint = 1f;
            }

            ApplyLayout(
                screenWidth / pixelsPerPoint,
                screenHeight / pixelsPerPoint,
                new Rect(
                    screenSafeArea.x / pixelsPerPoint,
                    screenSafeArea.y / pixelsPerPoint,
                    screenSafeArea.width / pixelsPerPoint,
                    screenSafeArea.height / pixelsPerPoint));
        }

        /// <summary>Applies the responsive layout from panel-point viewport and safe-area values (bottom-left origin).</summary>
        public void ApplyLayout(float width, float height, Rect safeArea)
        {
            if (!IsReady || width <= 0f || height <= 0f)
            {
                return;
            }

            var focused = root.panel?.focusController?.focusedElement as VisualElement;
            var previous = Layout;
            Layout = FactoryHudDocument.Classify(width, height);
            foreach (var layout in (FactoryHudLayout[])Enum.GetValues(typeof(FactoryHudLayout)))
            {
                root.RemoveFromClassList(FactoryHudDocument.LayoutClassName(layout));
            }

            root.AddToClassList(FactoryHudDocument.LayoutClassName(Layout));
            var left = Mathf.Max(0f, safeArea.xMin);
            var right = Mathf.Max(0f, width - safeArea.xMax);
            var bottom = Mathf.Max(0f, safeArea.yMin);
            var top = Mathf.Max(0f, height - safeArea.yMax);
            foreach (var element in new[] { body, dialogLayer })
            {
                element.style.paddingLeft = left;
                element.style.paddingRight = right;
                element.style.paddingBottom = bottom;
                element.style.paddingTop = top;
            }

            // The tab bar and the Overdrive control move between the phone and side-panel positions (UX-1).
            var sidePanel = FactoryHudDocument.HasSidePanel(Layout);
            var tabParent = sidePanel ? panelTabsSlot : body;
            if (tabBar.parent != tabParent)
            {
                tabParent.Add(tabBar);
            }

            // On a phone the top strip spans the whole width above the sheet row, so a side sheet in landscape never squeezes it;
            // on tablet and desktop it sits above the world, beside the full-height side panel.
            var stripParent = sidePanel ? worldColumn : body;
            if (topStrip.parent != stripParent)
            {
                stripParent.Insert(0, topStrip);
            }

            var overdriveParent = sidePanel ? panelHeaderSlot : topStrip;
            if (OverdriveButton.parent != overdriveParent)
            {
                overdriveParent.Add(OverdriveButton);
            }

            if (previous != Layout)
            {
                UpdateSheetState();
                if (focused != null && focused.panel == root.panel && focused.focusController?.focusedElement != focused)
                {
                    FocusWhenReady(focused);
                }
            }
        }

        /// <summary>
        /// The screen-pixel rectangle (bottom-left origin) of the world area the shell leaves uncovered: the world column below the
        /// top strip and right of the zoom controls. A sheet or side panel is already outside the world column. Returns false
        /// until the layout has resolved.
        /// </summary>
        public bool TryGetUnobstructedScreenRect(float screenHeight, out Rect rect)
        {
            rect = default;
            if (!IsReady || root.panel == null || screenHeight <= 0f)
            {
                return false;
            }

            var bounds = worldColumn.worldBound;
            var strip = topStrip.worldBound;
            var zoom = zoomColumn.worldBound;
            if (float.IsNaN(bounds.width) || bounds.width <= 0f || bounds.height <= 0f || float.IsNaN(strip.height))
            {
                return false;
            }

            var pixelsPerPoint = root.panel.scaledPixelsPerPoint;
            if (pixelsPerPoint <= 0f || float.IsNaN(pixelsPerPoint) || float.IsInfinity(pixelsPerPoint))
            {
                return false;
            }

            var topPoint = Mathf.Max(bounds.yMin, strip.yMax);
            var leftPoint = zoom.width > 0f ? Mathf.Max(bounds.xMin, zoom.xMax) : bounds.xMin;
            var available = Rect.MinMaxRect(leftPoint, topPoint, bounds.xMax, bounds.yMax);
            if (available.width <= 0f || available.height <= 0f)
            {
                return false;
            }

            rect = new Rect(
                available.x * pixelsPerPoint,
                screenHeight - available.yMax * pixelsPerPoint,
                available.width * pixelsPerPoint,
                available.height * pixelsPerPoint);
            return true;
        }

        // ------------------------------------------------------------------------------------------------------
        // Top strip values
        // ------------------------------------------------------------------------------------------------------

        public void SetCash(long value)
        {
            if (!EnsureReady())
            {
                return;
            }

            cash = value;
            cashValue.text = QuantityFormatter.Format(value);
            RefreshDetailFor(CashButton);
        }

        /// <summary>Sets RP; the RP cell is hidden until the player has unlocked RP (UX-1).</summary>
        public void SetRp(long value, bool unlocked)
        {
            if (!EnsureReady())
            {
                return;
            }

            rp = value;
            rpValue.text = QuantityFormatter.Format(value);
            RpButton.style.display = unlocked ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshDetailFor(RpButton);
        }

        public void SetStore(long used, long capacity)
        {
            if (!EnsureReady())
            {
                return;
            }

            storeUsed = used;
            storeCapacity = capacity;
            var percent = StorePercent(used, capacity);
            var full = capacity > 0 && used >= capacity;
            storeValue.text = full ? "FULL" : percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
            storeBarFill.style.width = new Length(percent, LengthUnit.Percent);
            StoreButton.EnableInClassList("run-stat--full", full);
            RefreshDetailFor(StoreButton);
        }

        private static int StorePercent(long used, long capacity)
        {
            if (capacity <= 0 || used <= 0)
            {
                return 0;
            }

            var percent = (int)Math.Min(100m, Math.Floor(used * 100m / capacity));
            return Math.Max(0, percent);
        }

        /// <summary>Shows the Overdrive control state: Locked, Ready, Active (remaining) or Cooling down (remaining).</summary>
        public void SetOverdrive(OverdriveStatus status)
        {
            if (!EnsureReady())
            {
                return;
            }

            overdriveStatus = status;
            string caption;
            string iconKey;
            var active = false;
            var soft = true;
            string reason;
            if (status == null || status.State == OverdriveStage.Locked)
            {
                caption = "Locked";
                iconKey = "F003-UI-ICON-OVERDRIVE-LOCKED";
                reason = RunRejectionCopy.OverdriveLocked(status);
            }
            else if (status.State == OverdriveStage.Active)
            {
                caption = "On " + RunRejectionCopy.Countdown(status.RemainingMs);
                iconKey = "F003-UI-ICON-OVERDRIVE-ACTIVE";
                active = true;
                reason = RunRejectionCopy.For(RunReasons.OverdriveActive) + " " + RunRejectionCopy.Countdown(status.RemainingMs) + " left.";
            }
            else if (status.State == OverdriveStage.CoolingDown)
            {
                caption = "Cool " + RunRejectionCopy.Countdown(status.RemainingMs);
                iconKey = "F003-UI-ICON-OVERDRIVE-COOLDOWN";
                reason = RunRejectionCopy.For(RunReasons.OverdriveCoolingDown) + " Ready in " + RunRejectionCopy.Countdown(status.RemainingMs) + ".";
            }
            else
            {
                caption = "Ready";
                iconKey = "F003-UI-ICON-OVERDRIVE-READY";
                soft = false;
                reason = "Overdrive is ready.";
            }

            overdriveCaption.text = caption;
            SetIcon(overdriveIcon, iconKey);
            OverdriveButton.EnableInClassList("run-overdrive--active", active);
            SetSoftDisabled(OverdriveButton, soft);
            OverdriveButton.tooltip = reason;
            overdriveReason = reason;
        }

        private string overdriveReason = string.Empty;

        /// <summary>The player-readable reason of the Overdrive control's current state.</summary>
        public string OverdriveReason => overdriveReason;

        // ------------------------------------------------------------------------------------------------------
        // Notice, availability, state
        // ------------------------------------------------------------------------------------------------------

        public void ShowNotice(string text, ShellNoticeKind kind = ShellNoticeKind.Info)
        {
            if (!EnsureReady())
            {
                return;
            }

            noticeLabel.text = text ?? string.Empty;
            noticeLabel.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            noticeLabel.EnableInClassList("run-notice--error", kind == ShellNoticeKind.Error);
            noticeLabel.EnableInClassList("run-notice--warning", kind == ShellNoticeKind.Warning);
        }

        public void ClearNotice() => ShowNotice(string.Empty);

        /// <summary>Gates gameplay panels while the factory is not available (a blocked return, a pending transition).</summary>
        public void SetGameplayAvailable(bool available, string reason = null)
        {
            gameplayAvailable = available;
            unavailableReason = available ? string.Empty : (reason ?? RunRejectionCopy.For(RunReasons.LifecycleBlocked));
            if (!EnsureReady())
            {
                return;
            }

            root.EnableInClassList("run-shell--unavailable", !available);
            if (!available)
            {
                ShowNotice(unavailableReason, ShellNoticeKind.Warning);
            }
            else if (NoticeText == unavailableReason)
            {
                ClearNotice();
            }

            RefreshPlaceholder();
        }

        /// <summary>Shows or hides the shell as a whole (the Feature 001 HUD shows a start-up failure instead).</summary>
        public void SetDisplayed(bool displayed)
        {
            if (root != null)
            {
                root.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // ------------------------------------------------------------------------------------------------------
        // Tabs, sheets and panels
        // ------------------------------------------------------------------------------------------------------

        /// <summary>Selects a tab. Factory closes the sheet on a phone; the other tabs open their panel.</summary>
        public void SelectTab(ShellTab tab)
        {
            if (!EnsureReady())
            {
                return;
            }

            if (tab == ShellTab.Factory)
            {
                CloseSheet(false);
                return;
            }

            SetSelectedSlot(-1);
            activeTab = tab;
            sheetOpen = true;
            ShowPanel(ShellPanelIds.ForTab(tab), TabNames[(int)tab], -1);
            UpdateSheetState();
        }

        /// <summary>
        /// Opens the build sheet (an empty slot) or the machine panel (an occupied slot). A locked slot opens nothing and
        /// shows the level that unlocks it. Returns whether a sheet opened.
        /// </summary>
        public bool OpenSlot(int slot, SlotCondition condition, int unlockLevel = 0)
        {
            if (!EnsureReady())
            {
                return false;
            }

            if (condition == SlotCondition.Locked)
            {
                ShowNotice("Slot " + (slot + 1) + " unlocks at factory level " + unlockLevel + ".", ShellNoticeKind.Info);
                return false;
            }

            activeTab = ShellTab.Factory;
            sheetOpen = true;
            SetSelectedSlot(slot);
            ShowPanel(
                condition == SlotCondition.Empty ? ShellPanelIds.Build : ShellPanelIds.Machine,
                (condition == SlotCondition.Empty ? "Build" : "Machine") + " · Slot " + (slot + 1),
                slot);
            UpdateSheetState();
            return true;
        }

        /// <summary>
        /// Re-routes an open slot sheet after the slot changed (a machine was built or removed): the build sheet becomes the
        /// machine panel and the reverse. Does nothing when the slot is not selected or the route is unchanged.
        /// </summary>
        public void UpdateSlotRoute(int slot, SlotCondition condition)
        {
            if (!IsReady || slot != selectedSlot || !sheetOpen || activeTab != ShellTab.Factory)
            {
                return;
            }

            if (condition == SlotCondition.Locked)
            {
                CloseSheet(false);
                return;
            }

            var id = condition == SlotCondition.Empty ? ShellPanelIds.Build : ShellPanelIds.Machine;
            if (id != activePanelId)
            {
                ShowPanel(id, (condition == SlotCondition.Empty ? "Build" : "Machine") + " · Slot " + (slot + 1), slot);
            }
        }

        /// <summary>
        /// Closes the sheet and clears the slot selection. Focus moves to the Factory tab when it was inside the closed
        /// sheet, so it is never left on a hidden control.
        /// </summary>
        public void CloseSheet(bool returnFocusToTab = true)
        {
            if (!EnsureReady())
            {
                return;
            }

            var focused = root.panel?.focusController?.focusedElement as VisualElement;
            var focusWasInside = focused != null && panelHost.Contains(focused) && !tabBar.Contains(focused);
            sheetOpen = false;
            activeTab = ShellTab.Factory;
            SetSelectedSlot(-1);
            ShowPanel(null, null, -1);
            UpdateSheetState();
            if (returnFocusToTab && focusWasInside)
            {
                FocusWhenReady(TabButton(ShellTab.Factory));
            }
        }

        /// <summary>
        /// Handles Escape and Android Back: Cancel for an open confirmation, then the detail line, then the sheet. Returns
        /// whether the press was consumed. Idempotent within a frame, so a key event and the input poll never act twice.
        /// </summary>
        public bool HandleBack()
        {
            if (!IsReady)
            {
                return false;
            }

            if (lastBackFrame == Time.frameCount && Application.isPlaying)
            {
                return true;
            }

            bool consumed;
            if (dialogRequest != null)
            {
                CloseDialog(ConfirmationCloseReason.Back);
                consumed = true;
            }
            else if (pinnedHost != null)
            {
                Unpin();
                consumed = true;
            }
            else if (sheetOpen && (selectedSlot >= 0 || activeTab != ShellTab.Factory))
            {
                CloseSheet();
                consumed = true;
            }
            else
            {
                consumed = false;
            }

            if (consumed)
            {
                lastBackFrame = Time.frameCount;
            }

            return consumed;
        }

        /// <summary>The shown panel changed because the run read model changed: lets a visible panel refresh itself.</summary>
        public void RefreshActivePanel() => activePanel?.Refresh();

        private void SetSelectedSlot(int slot)
        {
            if (selectedSlot == slot)
            {
                return;
            }

            selectedSlot = slot;
            SlotSelectionChanged?.Invoke(slot);
        }

        private void ShowPanel(string id, string title, int slot)
        {
            if (activePanel != null)
            {
                activePanel.OnHidden();
                activePanel.Root.style.display = DisplayStyle.None;
                activePanel = null;
            }

            activePanelId = id;
            foreach (var child in panelContent.Children())
            {
                if (child != placeholder)
                {
                    child.style.display = DisplayStyle.None;
                }
            }

            if (id != null && registry.TryGet(id, out var panel))
            {
                if (panel.Root.parent != panelContent)
                {
                    panelContent.Add(panel.Root);
                }

                panel.Root.style.display = DisplayStyle.Flex;
                placeholder.style.display = DisplayStyle.None;
                panelTitle.text = string.IsNullOrEmpty(title) ? panel.Title : title;
                activePanel = panel;
                panel.OnShown(new ShellPanelContext(this, slot));
            }
            else
            {
                panelTitle.text = string.IsNullOrEmpty(title) ? "Factory" : title;
                RefreshPlaceholder();
                placeholder.style.display = DisplayStyle.Flex;
            }

            UpdateTabSelection();
            ActivePanelChanged?.Invoke(id);
        }

        private void RefreshPlaceholder()
        {
            if (placeholderTitle == null)
            {
                return;
            }

            string text;
            string reason = string.Empty;
            if (!gameplayAvailable)
            {
                text = "Not available right now.";
                reason = unavailableReason;
            }
            else if (activePanelId == null)
            {
                text = "Tap an empty slot to build, or a machine to inspect it.";
            }
            else if (activePanelId == ShellPanelIds.Build)
            {
                text = "The build sheet for slot " + (selectedSlot + 1) + " is not available yet.";
            }
            else if (activePanelId == ShellPanelIds.Machine)
            {
                text = "The machine panel for slot " + (selectedSlot + 1) + " is not available yet.";
            }
            else
            {
                text = "This panel is not available yet.";
            }

            placeholderTitle.text = text;
            placeholderReason.text = reason;
            placeholderReason.style.display = string.IsNullOrEmpty(reason) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void BuildPlaceholder()
        {
            placeholder = new VisualElement { name = "panel-placeholder" };
            placeholderTitle = new Label { name = "panel-placeholder-text" };
            placeholderTitle.AddToClassList("run-panel__empty");
            placeholderReason = new Label { name = "panel-placeholder-reason" };
            placeholderReason.AddToClassList("run-panel__reason");
            placeholder.Add(placeholderTitle);
            placeholder.Add(placeholderReason);
            panelContent.Add(placeholder);
        }

        private void UpdateSheetState()
        {
            // On a phone the sheet is closed until a tab or slot opens it; the side panel of tablet and desktop is always there.
            root.EnableInClassList("run-shell--sheet-open", sheetOpen);
        }

        private void UpdateTabSelection()
        {
            for (var i = 0; i < Tabs.Length; i++)
            {
                tabButtons[i].EnableInClassList("run-tab--selected", Tabs[i] == activeTab);
            }
        }

        private void OnTabClicked(ShellTab tab)
        {
            if (!IsSidePanelLayout && tab != ShellTab.Factory && tab == activeTab && sheetOpen)
            {
                // Tapping the open phone tab again closes its sheet.
                CloseSheet();
                return;
            }

            if (tab == ShellTab.Factory && selectedSlot < 0 && !sheetOpen && IsSidePanelLayout)
            {
                ShowPanel(null, null, -1);
                return;
            }

            SelectTab(tab);
        }

        // ------------------------------------------------------------------------------------------------------
        // Shared confirmation dialog (UX-2, UX-9)
        // ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Opens the shared confirmation dialog. It traps focus, opens with focus on Cancel, treats Escape and Android Back as
        /// Cancel and returns focus to <see cref="ConfirmationRequest.Invoker"/> when it closes. A second request closes the
        /// first as <see cref="ConfirmationCloseReason.Replaced"/>.
        /// </summary>
        public bool RequestConfirmation(ConfirmationRequest request)
        {
            if (!EnsureReady() || request == null)
            {
                return false;
            }

            if (dialogRequest != null)
            {
                CloseDialog(ConfirmationCloseReason.Replaced, restoreFocus: false);
            }

            dialogRequest = request;
            dialogInvoker = request.Invoker ?? root.panel?.focusController?.focusedElement as VisualElement;
            dialogTitle.text = request.Title ?? string.Empty;
            dialogMessage.text = request.Message ?? string.Empty;
            dialogMessage.style.minHeight = StyleKeyword.Null;
            DialogConfirmButton.text = request.ConfirmLabel ?? "Confirm";
            DialogCancelButton.text = request.CancelLabel ?? "Cancel";
            DialogConfirmButton.EnableInClassList("run-button--destructive", request.Destructive);
            DialogConfirmButton.EnableInClassList("run-button--primary", !request.Destructive);
            SetIcon(dialogIcon, request.Destructive ? "F003-UI-ICON-WARNING" : "F003-UI-ICON-INFO");
            dialogIcon.EnableInClassList("run-icon--tint-danger", request.Destructive);
            dialogIcon.EnableInClassList("run-icon--tint-accent", !request.Destructive);
            dialogLayer.style.display = DisplayStyle.Flex;
            ResetDialogScroll();
            FocusWhenReady(DialogCancelButton);
            return true;
        }

        /// <summary>
        /// A wrapped label directly inside a bounded scroll view is measured at most as tall as the viewport, so the tail of a long
        /// message could never be scrolled to. The label's minimum height is set to its real wrapped height, which makes the
        /// scroll extent right; the change re-runs this handler until the height is stable.
        /// </summary>
        private void OnDialogMessageGeometry(GeometryChangedEvent evt)
        {
            var width = dialogMessage.contentRect.width;
            if (width <= 0f || string.IsNullOrEmpty(dialogMessage.text))
            {
                return;
            }

            var needed = dialogMessage.MeasureTextSize(dialogMessage.text, width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined).y;
            var current = dialogMessage.style.minHeight.keyword == StyleKeyword.Undefined ? dialogMessage.style.minHeight.value.value : 0f;
            if (Mathf.Abs(needed - current) > 0.5f)
            {
                dialogMessage.style.minHeight = needed;
            }
        }

        private void ResetDialogScroll()
        {
            var scroll = dialogLayer.Q<ScrollView>("dialog-scroll");
            if (scroll != null)
            {
                scroll.scrollOffset = Vector2.zero;
            }
        }

        /// <summary>Cancels an open confirmation because of a rotation, without confirming anything (UX-9). Returns whether one was open.</summary>
        public bool CancelConfirmationForRotation()
        {
            if (dialogRequest == null)
            {
                return false;
            }

            CloseDialog(ConfirmationCloseReason.Rotation);
            ShowNotice(RotationCancelledNotice);
            return true;
        }

        private void CloseDialog(ConfirmationCloseReason reason, bool restoreFocus = true)
        {
            var request = dialogRequest;
            if (request == null)
            {
                return;
            }

            var invoker = dialogInvoker;
            dialogRequest = null;
            dialogInvoker = null;
            dialogLayer.style.display = DisplayStyle.None;
            if (restoreFocus)
            {
                if (invoker != null && invoker.panel == root.panel && invoker.enabledInHierarchy && IsDisplayed(invoker))
                {
                    FocusWhenReady(invoker);
                }
                else
                {
                    FocusWhenReady(TabButton(activeTab));
                }
            }

            if (reason == ConfirmationCloseReason.Confirmed)
            {
                request.OnConfirm?.Invoke();
            }
            else
            {
                request.OnCancel?.Invoke(reason);
            }
        }

        private void OnDialogKeyDown(KeyDownEvent evt)
        {
            if (dialogRequest == null)
            {
                return;
            }

            if (evt.keyCode == KeyCode.Escape)
            {
                HandleBack();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Tab)
            {
                MoveDialogFocus(evt.shiftKey ? -1 : 1);
                evt.StopPropagation();
                root.panel?.focusController?.IgnoreEvent(evt);
            }
        }

        private void OnDialogNavigationMove(NavigationMoveEvent evt)
        {
            if (dialogRequest == null)
            {
                return;
            }

            switch (evt.direction)
            {
                case NavigationMoveEvent.Direction.Next:
                case NavigationMoveEvent.Direction.Right:
                case NavigationMoveEvent.Direction.Down:
                    MoveDialogFocus(1);
                    break;
                case NavigationMoveEvent.Direction.Previous:
                case NavigationMoveEvent.Direction.Left:
                case NavigationMoveEvent.Direction.Up:
                    MoveDialogFocus(-1);
                    break;
            }

            evt.StopPropagation();
            root.panel?.focusController?.IgnoreEvent(evt);
        }

        private void OnDialogFocusOut(FocusOutEvent evt)
        {
            if (dialogRequest == null)
            {
                return;
            }

            // Focus must never leave the dialog while it is open (the trap): put it back when it moves outside.
            if (evt.relatedTarget is VisualElement next && (dialogCard.Contains(next) || next == dialogCard))
            {
                return;
            }

            RefocusDialogDeferred();
        }

        /// <summary>
        /// Puts focus back into the open dialog after the current focus change finished: changing focus inside a focus event is
        /// ignored while that change is still being committed, so the correction runs on the next scheduler tick.
        /// </summary>
        private void RefocusDialogDeferred()
        {
            root.schedule.Execute(() =>
            {
                if (dialogRequest == null)
                {
                    return;
                }

                var focused = root.panel?.focusController?.focusedElement as VisualElement;
                if (focused == null || !dialogCard.Contains(focused))
                {
                    DialogCancelButton.Focus();
                }
            });
        }

        private void MoveDialogFocus(int direction)
        {
            if (lastMoveFrame == Time.frameCount && lastMoveDirection == direction && Application.isPlaying)
            {
                return;
            }

            lastMoveFrame = Time.frameCount;
            lastMoveDirection = direction;
            var order = new List<VisualElement> { DialogCancelButton, DialogConfirmButton };
            var focused = root.panel?.focusController?.focusedElement as VisualElement;
            var index = order.IndexOf(focused);
            var next = index < 0 ? 0 : (index + direction + order.Count) % order.Count;
            order[next].Focus();
        }

        // ------------------------------------------------------------------------------------------------------
        // Exact-value detail line (UX-8)
        // ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Makes a compact number show its exact value in the shell's detail line: on focus and on mouse hover, on a tap that
        /// pins it open (touch), and on a 500 ms long press. Use it only on elements that are neither draggable nor part of
        /// the world, so the long press never competes with pan or drag-to-reorder. Dispose the handle to detach.
        /// </summary>
        public IDisposable AttachExactValue(VisualElement host, Func<string> exactText)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (exactText == null) throw new ArgumentNullException(nameof(exactText));
            var handle = new ExactValueBinding(this, host, exactText);
            exactValueHandles.Add(handle);
            return handle;
        }

        private void RefreshDetailFor(VisualElement host)
        {
            if (detailHost == host && detailLine != null)
            {
                detailLine.text = detailText?.Invoke() ?? string.Empty;
            }
        }

        private void ShowDetail(VisualElement host, Func<string> text)
        {
            detailHost = host;
            detailText = text;
            detailLine.text = text();
            detailLine.style.display = DisplayStyle.Flex;
        }

        private void HideDetail(VisualElement host)
        {
            if (detailHost != host || pinnedHost == host)
            {
                return;
            }

            ClearDetail();
        }

        private void ClearDetail()
        {
            detailHost = null;
            detailText = null;
            detailLine.text = string.Empty;
            detailLine.style.display = DisplayStyle.None;
        }

        private void Unpin()
        {
            pinnedHost = null;
            ClearDetail();
        }

        private sealed class ExactValueBinding : IDisposable
        {
            private readonly RunShellDocument owner;
            private readonly VisualElement host;
            private readonly Func<string> text;
            private IVisualElementScheduledItem longPress;
            private bool longPressFired;
            private bool disposed;

            public ExactValueBinding(RunShellDocument owner, VisualElement host, Func<string> text)
            {
                this.owner = owner;
                this.host = host;
                this.text = text;
                host.RegisterCallback<FocusInEvent>(OnFocusIn);
                host.RegisterCallback<FocusOutEvent>(OnFocusOut);
                host.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
                host.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
                host.RegisterCallback<PointerDownEvent>(OnPointerDown);
                host.RegisterCallback<PointerUpEvent>(OnPointerUp);
                host.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
                host.RegisterCallback<ClickEvent>(OnClick);
                owner.exactTexts[host] = text;
            }

            private void OnFocusIn(FocusInEvent evt) => owner.ShowDetail(host, text);

            private void OnFocusOut(FocusOutEvent evt) => owner.HideDetail(host);

            private void OnPointerEnter(PointerEnterEvent evt)
            {
                if (evt.pointerType == UnityEngine.UIElements.PointerType.mouse)
                {
                    owner.ShowDetail(host, text);
                }
            }

            private void OnPointerLeave(PointerLeaveEvent evt)
            {
                CancelLongPress();
                if (evt.pointerType == UnityEngine.UIElements.PointerType.mouse)
                {
                    owner.HideDetail(host);
                }
            }

            private void OnPointerDown(PointerDownEvent evt)
            {
                longPressFired = false;
                CancelLongPress();
                longPress = host.schedule.Execute(() =>
                {
                    longPressFired = true;
                    owner.pinnedHost = host;
                    owner.ShowDetail(host, text);
                }).StartingIn((long)(LongPressSeconds * 1000f));
            }

            private void OnPointerUp(PointerUpEvent evt) => CancelLongPress();

            private void OnPointerCancel(PointerCancelEvent evt) => CancelLongPress();

            private void OnClick(ClickEvent evt)
            {
                CancelLongPress();
                if (longPressFired)
                {
                    longPressFired = false;
                    return;
                }

                // A tap pins the detail line open, and a second tap closes it (touch has no hover or focus ring).
                if (owner.pinnedHost == host)
                {
                    owner.Unpin();
                }
                else
                {
                    owner.pinnedHost = host;
                    owner.ShowDetail(host, text);
                }
            }

            private void CancelLongPress()
            {
                longPress?.Pause();
                longPress = null;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                CancelLongPress();
                host.UnregisterCallback<FocusInEvent>(OnFocusIn);
                host.UnregisterCallback<FocusOutEvent>(OnFocusOut);
                host.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
                host.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
                host.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                host.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                host.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
                host.UnregisterCallback<ClickEvent>(OnClick);
                owner.exactTexts.Remove(host);
                if (owner.pinnedHost == host)
                {
                    owner.pinnedHost = null;
                }

                if (owner.detailHost == host)
                {
                    owner.ClearDetail();
                }

                owner.exactValueHandles.Remove(this);
            }
        }

        // ------------------------------------------------------------------------------------------------------
        // Controls, focus and icons
        // ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Marks a control as disabled with its reason shown elsewhere: it stays focusable (Art Bible 9.4), looks disabled and
        /// the command behind it must reject activation with the reason. A real <c>SetEnabled(false)</c> removes focus.
        /// </summary>
        public static void SetSoftDisabled(VisualElement control, bool disabled)
        {
            control.EnableInClassList("run-control--soft-disabled", disabled);
        }

        public static bool IsSoftDisabled(VisualElement control) => control.ClassListContains("run-control--soft-disabled");

        /// <summary>The focusable, enabled, displayed controls in tab order, restricted to the dialog while it is open.</summary>
        public IReadOnlyList<VisualElement> GetFocusOrder()
        {
            var result = new List<VisualElement>();
            if (!IsReady)
            {
                return result;
            }

            Collect(dialogRequest != null ? dialogLayer : root, result);
            return result;
        }

        private static void Collect(VisualElement element, List<VisualElement> result)
        {
            if (element.resolvedStyle.display == DisplayStyle.None || element.style.display == DisplayStyle.None)
            {
                return;
            }

            if (element.focusable && element.enabledInHierarchy && element.canGrabFocus && element.tabIndex >= 0)
            {
                result.Add(element);
            }

            foreach (var child in element.Children())
            {
                Collect(child, result);
            }
        }

        /// <summary>Gives an element focus as soon as it can take it (its style may not be resolved the frame it was shown).</summary>
        public void FocusWhenReady(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            element.Focus();
            if (element.focusController?.focusedElement == element)
            {
                return;
            }

            var attempts = 0;
            IVisualElementScheduledItem item = null;
            item = element.schedule.Execute(() =>
            {
                attempts++;
                element.Focus();
                if (element.focusController?.focusedElement == element || attempts >= 12)
                {
                    item.Pause();
                }
            }).Every(16);
        }

        private static bool IsDisplayed(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnFocusIn(FocusInEvent evt)
        {
            if (!(evt.target is VisualElement target))
            {
                return;
            }

            if (dialogRequest != null && !dialogCard.Contains(target))
            {
                // The trap: focus that lands outside the open dialog goes back into it.
                RefocusDialogDeferred();
            }

            if (target.ClassListContains("run-control"))
            {
                target.AddToClassList("run-control--focused");
                ringTarget = target;
                PositionFocusRing();
            }

            // A clipped control scrolls into view when it receives focus (UI surfaces contract, Controls).
            for (var parent = target.parent; parent != null; parent = parent.parent)
            {
                if (parent is ScrollView scroll)
                {
                    scroll.ScrollTo(target);
                    break;
                }
            }
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            var target = evt.target as VisualElement;
            target?.RemoveFromClassList("run-control--focused");
            if (target != null && target == ringTarget)
            {
                ringTarget = null;
                focusRing.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Places the focus ring around the focused control: the 2 pt inner ring touches the control edge and the 2 pt outer ring
        /// lies outside it. The ring hides while its control is scrolled out of its viewport.
        /// </summary>
        private void PositionFocusRing()
        {
            if (ringTarget == null || ringTarget.panel == null || focusRing.panel == null)
            {
                focusRing.style.display = DisplayStyle.None;
                return;
            }

            var bounds = ringTarget.worldBound;
            if (float.IsNaN(bounds.width) || bounds.width <= 0f || bounds.height <= 0f || !IsDisplayed(ringTarget) || !IsInsideScrollViewport(ringTarget))
            {
                focusRing.style.display = DisplayStyle.None;
                return;
            }

            const float ringWidth = 4f;
            var origin = focusRing.parent.worldBound;
            focusRing.style.display = DisplayStyle.Flex;
            focusRing.style.left = bounds.xMin - origin.xMin - ringWidth;
            focusRing.style.top = bounds.yMin - origin.yMin - ringWidth;
            focusRing.style.width = bounds.width + 2f * ringWidth;
            focusRing.style.height = bounds.height + 2f * ringWidth;
        }

        private static bool IsInsideScrollViewport(VisualElement element)
        {
            for (var parent = element.parent; parent != null; parent = parent.parent)
            {
                if (parent is ScrollView scroll)
                {
                    var viewport = scroll.contentViewport.worldBound;
                    var center = element.worldBound.center;
                    return viewport.Contains(center);
                }
            }

            return true;
        }

        /// <summary>Creates a shell-styled button for a panel: 44 pt minimum, secondary look, visible focus ring.</summary>
        public static Button CreateButton(string name, string text, bool primary = false)
        {
            var button = new Button { name = name, text = text, focusable = true };
            button.AddToClassList("run-control");
            button.AddToClassList("run-button");
            button.AddToClassList(primary ? "run-button--primary" : "run-button--secondary");
            return button;
        }

        private void SetIcon(VisualElement element, string key)
        {
            var sprite = iconResolver(key);
            element.style.backgroundImage = sprite == null ? new StyleBackground(StyleKeyword.None) : new StyleBackground(sprite);
        }

        private void ApplyIcons()
        {
            SetIcon(root.Q<VisualElement>("cash-icon"), "F003-UI-ICON-CASH");
            SetIcon(root.Q<VisualElement>("rp-icon"), "F003-UI-ICON-RP");
            SetIcon(root.Q<VisualElement>("store-icon"), "F003-UI-ICON-STORAGE");
            SetIcon(root.Q<VisualElement>("panel-close-icon"), "F003-UI-ICON-CLOSE");
            for (var i = 0; i < Tabs.Length; i++)
            {
                SetIcon(root.Q<VisualElement>("tab-" + TabNames[i].ToLowerInvariant() + "-icon"), TabIconKeys[i]);
            }
        }

        // ------------------------------------------------------------------------------------------------------
        // Wiring
        // ------------------------------------------------------------------------------------------------------

        private void BindCallbacks()
        {
            for (var i = 0; i < Tabs.Length; i++)
            {
                var tab = Tabs[i];
                tabButtons[i].clicked += () => OnTabClicked(tab);
            }

            tabBar.RegisterCallback<KeyDownEvent>(OnTabKeyDown);
            tabBar.RegisterCallback<NavigationMoveEvent>(OnTabNavigationMove);
            CloseButton.clicked += () => CloseSheet();
            ZoomInButton.clicked += () => ZoomRequested?.Invoke(1);
            ZoomOutButton.clicked += () => ZoomRequested?.Invoke(-1);
            OverdriveButton.clicked += OnOverdriveClicked;
            DialogCancelButton.clicked += () => CloseDialog(ConfirmationCloseReason.Cancelled);
            DialogConfirmButton.clicked += () => CloseDialog(ConfirmationCloseReason.Confirmed);
            dialogLayer.RegisterCallback<KeyDownEvent>(OnDialogKeyDown, TrickleDown.TrickleDown);
            dialogLayer.RegisterCallback<NavigationMoveEvent>(OnDialogNavigationMove, TrickleDown.TrickleDown);
            dialogLayer.RegisterCallback<FocusOutEvent>(OnDialogFocusOut);
            dialogMessage.RegisterCallback<GeometryChangedEvent>(OnDialogMessageGeometry);
            root.RegisterCallback<FocusInEvent>(OnFocusIn);
            root.RegisterCallback<FocusOutEvent>(OnFocusOut);
            root.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
            AttachExactValue(CashButton, () => "Cash: " + QuantityFormatter.FormatExact(cash));
            AttachExactValue(RpButton, () => "Research points: " + QuantityFormatter.FormatExact(rp));
            AttachExactValue(StoreButton, () => storeCapacity > 0
                ? "Store: " + QuantityFormatter.FormatExact(storeUsed) + " of " + QuantityFormatter.FormatExact(storeCapacity) +
                  " units (" + StorePercent(storeUsed, storeCapacity) + "%)"
                : "Store: no capacity");
            registry.Changed += OnRegistryChanged;
        }

        private void OnRootPointerDown(PointerDownEvent evt)
        {
            // A press elsewhere closes a pinned detail line.
            if (pinnedHost != null && !(evt.target is VisualElement target && (pinnedHost == target || pinnedHost.Contains(target))))
            {
                Unpin();
            }
        }

        private void OnRegistryChanged(string id)
        {
            if (!IsReady || id != activePanelId)
            {
                return;
            }

            ShowPanel(activePanelId, panelTitle.text, selectedSlot);
        }

        private void OnOverdriveClicked()
        {
            if (IsSoftDisabled(OverdriveButton))
            {
                ShowNotice(overdriveReason, ShellNoticeKind.Warning);
                return;
            }

            OverdriveRequested?.Invoke();
        }

        private void OnTabKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.LeftArrow)
            {
                MoveTabFocus(-1);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.RightArrow)
            {
                MoveTabFocus(1);
                evt.StopPropagation();
            }
        }

        private void OnTabNavigationMove(NavigationMoveEvent evt)
        {
            if (evt.direction == NavigationMoveEvent.Direction.Left)
            {
                MoveTabFocus(-1);
                evt.StopPropagation();
            }
            else if (evt.direction == NavigationMoveEvent.Direction.Right)
            {
                MoveTabFocus(1);
                evt.StopPropagation();
            }
        }

        private void MoveTabFocus(int direction)
        {
            // The input system raises a key event and a navigation event for one arrow press: act on the first only.
            if (lastMoveFrame == Time.frameCount && lastMoveDirection == direction && Application.isPlaying)
            {
                return;
            }

            lastMoveFrame = Time.frameCount;
            lastMoveDirection = direction;
            var focused = root.panel?.focusController?.focusedElement as VisualElement;
            var index = Array.IndexOf(tabButtons, focused as Button);
            if (index < 0)
            {
                return;
            }

            var next = Mathf.Clamp(index + direction, 0, tabButtons.Length - 1);
            tabButtons[next].Focus();
        }

        private void Update()
        {
            // Android Back arrives as Escape in the Input System. UI Toolkit also sees Escape as a key event; HandleBack is
            // idempotent within a frame, so whichever path runs first acts and the other is ignored.
            var keyboard = Keyboard.current;
            if (IsReady && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                HandleBack();
            }

            if (IsReady && ringTarget != null)
            {
                // The focused control can move (a scroll, a layout change); the ring follows it.
                PositionFocusRing();
            }
        }

        private bool EnsureReady()
        {
            return IsReady;
        }

        private bool Fail(string message)
        {
            Dispose();
            Error = message;
            return false;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void Dispose()
        {
            foreach (var handle in exactValueHandles.ToArray())
            {
                handle.Dispose();
            }

            exactValueHandles.Clear();
            exactTexts.Clear();
            registry.Changed -= OnRegistryChanged;
            activePanel?.OnHidden();
            activePanel = null;
            if (root != null && panelContent != null)
            {
                // Mounted panel roots belong to their panels: take them out before the shell tree is dropped.
                foreach (var child in panelContent.Children().ToArrayCopy())
                {
                    if (child != placeholder)
                    {
                        child.RemoveFromHierarchy();
                    }
                }

            }

            root?.RemoveFromHierarchy();
            root = null;
            built = false;
            dialogRequest = null;
            dialogInvoker = null;
            detailHost = null;
            pinnedHost = null;
            selectedSlot = -1;
            sheetOpen = false;
            activePanelId = null;
            activeTab = ShellTab.Factory;
        }
    }

    internal static class VisualElementEnumerableExtensions
    {
        public static VisualElement[] ToArrayCopy(this IEnumerable<VisualElement> source)
        {
            return new List<VisualElement>(source).ToArray();
        }
    }
}
