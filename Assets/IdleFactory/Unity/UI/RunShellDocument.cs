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
        Lock = 3,
    }

    /// <summary>
    /// The responsive shell of the Feature 003 run screen (T112): the top strip (Cash, RP, store usage, Overdrive), the six
    /// tabs, the bottom sheet or side panel that panels mount into, the shared confirmation dialog, the exact-value detail
    /// line and the notice line. It clones <c>FactoryRun.uxml</c>, owns no gameplay rule and reads state only through
    /// values handed to it by <see cref="RunShellPresenter"/>. T113 to T118 add panels through <see cref="Panels"/>
    /// without editing it.
    /// </summary>
    public sealed partial class RunShellDocument : MonoBehaviour, IShellBackFrames
    {
        /// <summary>The one-line notice that a rotation cancelled an open transient action (UX-9).</summary>
        public const string RotationCancelledNotice = "Cancelled by rotation";

        /// <summary>Long-press duration of the number detail line (UI surfaces contract, Numbers).</summary>
        public const float LongPressSeconds = 0.5f;

        /// <summary>How long a transient notice stays before it clears on its own (T192, F-1).</summary>
        public const float DefaultNoticeSeconds = 6f;

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
        private VisualElement noticeBar;
        private VisualElement noticeIcon;
        private VisualElement worldHint;
        private IVisualElementScheduledItem noticeTimer;
        private bool noticeSticky;
        private int lockNoticeSlot = -1;
        private bool emptyHintWanted;
        private Label cashValue;
        private Label rpValue;
        private Label storeValue;
        private Label overdriveCaption;
        private VisualElement storeBarFill;
        private VisualElement overdriveIcon;
        private VisualElement zoomColumn;
        private Label zoomHint;
        private IVisualElementScheduledItem zoomHintTimer;
        private bool zoomInAtLimit;
        private bool zoomOutAtLimit;
        private int zoomHintDirection;
        private bool zoomStateKnown;
        private bool zoomInShown;
        private bool zoomOutShown;
        private OreVeinControl oreVein;
        private bool oreVeinWanted;
        private FactoryWorldLayout worldLayout;
        private bool hasLastLayoutSize;
        private float lastLayoutWidth;
        private float lastLayoutHeight;
        private Label placeholderTitle;
        private Label placeholderReason;
        private VisualElement placeholder;
        private VisualElement focusRing;
        private VisualElement ringTarget;

        // T534: the docked machine card (one instance, registered as ShellPanelIds.MachineCard), its details hand-over and the keyboard
        // stop of the world locations.
        private IShellPanel cardPanel;
        private VisualElement cardMountedRoot;
        private bool cardOpen;
        private int cardSlot = -1;
        private bool detailsFromCard;
        private bool cardBandPending;
        private int cardBandFrame;
        private float safeHeightPoints;
        private VisualElement locationStop;
        private readonly List<int> locationOrder = new List<int>();
        private int locationCursor;
        private int lastLocationMoveFrame = -1;
        private int lastLocationMoveDirection;
        private int lastLocationActivateFrame = -1;

        private bool built;
        private bool gameplayAvailable = true;
        private string unavailableReason = string.Empty;
        private readonly ShellSheetHost sheetHost = new ShellSheetHost();
        private bool sheetOpen { get => sheetHost.IsOpen; set => sheetHost.IsOpen = value; }
        private bool settingsOpen { get => sheetHost.IsSettingsOpen; set => sheetHost.IsSettingsOpen = value; }
        private ShellTab activeTab = ShellTab.Factory;
        private string activePanelId;
        private IShellPanel activePanel;
        private int selectedSlot = -1;
        private readonly ShellNavigationStack navigationStack = new ShellNavigationStack();
        private int lastMoveFrame = -1;
        private int lastMoveDirection;
        private long cash;
        private long rp;
        private long storeUsed;
        private long storeCapacity;
        private OverdriveStatus overdriveStatus;
        private Label contractsBadge;
        private Label researchBadge;
        private long industryPoints;
        private Label contractsTabLabel;
        private int contractsDeliverable;
        private ContractsBadgeMode contractsBadgeMode = ContractsBadgeMode.None;

        // Device profile and text scale (T191).
        private ShellDeviceProfile deviceProfile = ShellDeviceProfile.Default;
        private ShellScaleDecision scaleDecision = ShellPanelScale.Decide(ShellDeviceProfile.Default, 1f);
        private ShellPanelScale.SavedScale savedScale;
        private float managedPixelsPerPoint;
        private float textScale = ShellTextScale.MinimumFactor;
        private bool hasLayoutInputs;
        private float layoutScreenWidth;
        private float layoutScreenHeight;
        private Rect layoutSafeArea;
        private float nextProbeTime;
        private float nextFontScaleTime;

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

        /// <summary>
        /// Whether the shell reads the device profile and the system font scale itself (at build and then twice a second). It is on
        /// for the game; a test turns it off before <see cref="Configure"/> to apply its own profile and text scale.
        /// </summary>
        public bool AutoProbeDevice { get; set; } = true;

        /// <summary>The device profile in force: primary input, screen size, DPI and orientation (UI surfaces contract, Global).</summary>
        public ShellDeviceProfile DeviceProfile => deviceProfile;

        /// <summary>How the panel scale was decided for the device profile (kept, or raised for the 7 mm touch target).</summary>
        public ShellScaleDecision ScaleDecision => scaleDecision;

        /// <summary>The text-scale factor in force, 1.0 to 2.0.</summary>
        public float TextScale => textScale;

        /// <summary>The text-scale step in force: 100, 110, ... 200 (the <c>run-text-scale--NNN</c> class on the shell root).</summary>
        public int TextScalePercent => ShellTextScale.Percent(textScale);

        /// <summary>The panel mount registry (T113 to T118 register their panels here).</summary>
        public ShellPanelRegistry Panels => registry;

        public VisualElement Root => root;
        public FactoryHudLayout Layout { get; private set; } = FactoryHudLayout.CompactPortrait;
        public string Error { get; private set; } = "The run shell has not been configured.";
        public bool IsReady => built && string.IsNullOrEmpty(Error) && root != null;
        public bool IsSidePanelLayout => FactoryHudDocument.HasSidePanel(Layout);

        /// <summary>True while a sheet is open on a phone, or while the factory side panel shows a selection on tablet and desktop.</summary>
        public bool IsSheetOpen => sheetOpen;

        /// <summary>True while the Settings sheet (T433) is the open sheet. Its own flag: Settings has no tab and no slot.</summary>
        public bool IsSettingsOpen => settingsOpen;

        /// <summary>The gear in the top strip that opens and closes the Settings sheet (T433, OD-8).</summary>
        public Button SettingsButton { get; private set; }

        /// <summary>The accessible name of the keyboard stop that stands for the world locations.</summary>
        public const string LocationStopName = "Locations. Arrow keys move between locations, Enter opens one.";

        /// <summary>
        /// Raised when the keyboard activates a location (Enter or Space on the location stop) with the location's slot index. The presenter
        /// opens the build card, the machine card or the lock notice, as a tap does.
        /// </summary>
        public event Action<int> LocationActivated;

        /// <summary>
        /// Gives the screen rectangle (panel points, top-left origin) of a location so the focus ring of the location stop can surround it. Set
        /// by the bootstrap, which owns the camera; unset, the stop sits at the corner of the world column.
        /// </summary>
        public Func<int, Rect?> LocationRectProvider { get; set; }

        /// <summary>True while the machine card is the open frame (it is hidden while its Details view shows).</summary>
        public bool IsCardOpen => cardOpen;

        /// <summary>The slot the card shows, or -1.</summary>
        public int CardSlot => cardOpen ? cardSlot : -1;

        /// <summary>True while the Details view of a machine, opened from the card, is the open sheet (closing it returns to the card).</summary>
        public bool IsDetailsFromCard => detailsFromCard;

        /// <summary>True when a machine card is registered; without one, a machine tap opens the machine panel directly as before.</summary>
        public bool HasMachineCard => registry.IsRegistered(ShellPanelIds.MachineCard);

        /// <summary>The root of the machine card, or null when none is registered.</summary>
        public VisualElement CardRoot => cardPanel?.Root;

        /// <summary>The registered machine card, or null.</summary>
        public IShellPanel Card => cardPanel;

        /// <summary>True from the moment a card opens (or the layout changes under it) until its band has been laid out: the camera waits for it.</summary>
        public bool IsCardBandPending => cardBandPending;

        /// <summary>The keyboard stop that stands for the world locations (focusable; arrow keys move its cursor).</summary>
        public VisualElement LocationStop => locationStop;

        /// <summary>The slot the location cursor is on, or -1 when no order is set.</summary>
        public int LocationCursorSlot => locationOrder.Count == 0 ? -1 : locationOrder[Mathf.Clamp(locationCursor, 0, locationOrder.Count - 1)];

        /// <summary>The keyboard order of the locations (slot indices, by SortKeyOf then column).</summary>
        public IReadOnlyList<int> LocationOrder => locationOrder;

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

        /// <summary>The ore vein overlay's input and feedback (T359). It is hidden until a mining source is bound.</summary>
        public OreVeinControl OreVein => oreVein;
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

        /// <summary>The seconds a transient notice stays before it clears itself; a test shortens it.</summary>
        public float NoticeSeconds { get; set; } = DefaultNoticeSeconds;

        /// <summary>The kind of the notice shown, or null when none is.</summary>
        public ShellNoticeKind? NoticeKind { get; private set; }

        /// <summary>The slot whose locked-slot notice is shown, or -1.</summary>
        public int LockNoticeSlot => lockNoticeSlot;

        /// <summary>Whether the phone's empty-grid hint ("Build a Miner") is shown.</summary>
        public bool IsEmptyHintShown => worldHint != null && worldHint.style.display == DisplayStyle.Flex;

        /// <summary>The small glyph that marks a strip value as pinnable, or null for an element that has none.</summary>
        public VisualElement PinHintOf(VisualElement host) => host?.Q<VisualElement>(host.name + "-pin-hint");

        public VisualElement NoticeBar => noticeBar;
        public VisualElement NoticeIconElement => noticeIcon;
        public VisualElement WorldHint => worldHint;
        public VisualElement DetailBar => detailLine;
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

            var existingChildren = documentRoot.childCount;
            visualTree.CloneTree(documentRoot);
            root = documentRoot.Q<VisualElement>("run-shell");
            if (root == null)
            {
                // Take the clone out again, so a retry starts from the untouched document.
                while (documentRoot.childCount > existingChildren)
                {
                    documentRoot.RemoveAt(documentRoot.childCount - 1);
                }

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
            noticeBar = root.Q<VisualElement>("notice");
            noticeLabel = root.Q<Label>("notice-text");
            noticeIcon = root.Q<VisualElement>("notice-icon");
            worldHint = root.Q<VisualElement>("world-hint");
            cashValue = root.Q<Label>("cash-value");
            rpValue = root.Q<Label>("rp-value");
            storeValue = root.Q<Label>("store-value");
            overdriveCaption = root.Q<Label>("overdrive-caption");
            storeBarFill = root.Q<VisualElement>("store-bar-fill");
            overdriveIcon = root.Q<VisualElement>("overdrive-icon");
            zoomColumn = root.Q<VisualElement>("zoom-column");
            zoomHint = root.Q<Label>("zoom-hint");
            oreVein = OreVeinControl.TryCreate(root.Q<VisualElement>(OreVeinControl.RootName));
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
            SettingsButton = root.Q<Button>("settings");
            ZoomInButton = root.Q<Button>("zoom-in");
            ZoomOutButton = root.Q<Button>("zoom-out");
            CloseButton = root.Q<Button>("panel-close");
            DialogCancelButton = root.Q<Button>("dialog-cancel");
            DialogConfirmButton = root.Q<Button>("dialog-confirm");
            for (var i = 0; i < Tabs.Length; i++)
            {
                tabButtons[i] = root.Q<Button>("tab-" + TabNames[i].ToLowerInvariant());
            }

            contractsBadge = root.Q<Label>("tab-contracts-badge");
            researchBadge = root.Q<Label>("tab-research-badge");
            contractsTabLabel = tabButtons[(int)ShellTab.Contracts]?.Q<Label>(className: "run-tab__label");

            if (body == null || main == null || worldColumn == null || topStrip == null || panelHost == null ||
                panelHeaderSlot == null || panelTabsSlot == null || tabBar == null || panelContent == null ||
                panelScroll == null || panelTitle == null || detailLine == null || noticeLabel == null || noticeBar == null || noticeIcon == null || worldHint == null ||
                cashValue == null || rpValue == null || storeValue == null || overdriveCaption == null ||
                storeBarFill == null || overdriveIcon == null || zoomColumn == null || oreVein == null || dialogLayer == null ||
                dialogCard == null || dialogIcon == null || focusRing == null || dialogTitle == null || dialogMessage == null ||
                CashButton == null || RpButton == null || StoreButton == null || OverdriveButton == null || SettingsButton == null ||
                ZoomInButton == null || ZoomOutButton == null || CloseButton == null ||
                DialogCancelButton == null || DialogConfirmButton == null || Array.IndexOf(tabButtons, null) >= 0 ||
                contractsBadge == null || contractsTabLabel == null)
            {
                return Fail("The run shell UXML is malformed or missing required named elements.");
            }

            if (!QueryHub())
            {
                return Fail("The run shell UXML is malformed or missing the hub entry.");
            }

            root.styleSheets.Add(styleSheet);
            oreVeinWanted = false;
            hasLastLayoutSize = false;
            oreVein.Root.style.display = DisplayStyle.None;
            oreVein.SetUnavailable(gameplayAvailable ? null : unavailableReason);
            ApplyTextScale(textScale);
            BuildPlaceholder();
            BindCallbacks();
            BuildLocationStop();
            ApplyIcons();
            built = true;
            Error = string.Empty;
            SetCash(0);
            SetRp(0, false);
            SetStore(0, 0);
            SetOverdrive(null);
            UpdateTabSelection();
            ApplyContractsBadge();
            ShowPanel(null, null, -1);
            RebindCard();
            if (AutoProbeDevice)
            {
                ProbeNow(true);
            }

            return true;
        }

        // ------------------------------------------------------------------------------------------------------
        // Device profile and text scale (T191)
        // ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Sets the device profile. The profile decides touch behaviour (tap-to-pin, long press, hover) and, for a touch device
        /// with a known DPI, raises the panel scale so a 44 pt control is at least 7 mm. A pointer device and an unknown DPI keep
        /// the authored scale; the decision is recorded in <see cref="ScaleDecision"/>. The layout class stays chosen by
        /// panel-point size, so a raised scale can change it.
        /// </summary>
        public ShellScaleDecision ApplyDeviceProfile(ShellDeviceProfile profile)
        {
            deviceProfile = profile;
            var settings = uiDocument != null ? uiDocument.panelSettings : null;
            RestorePanelScale();
            var authored = ShellPanelScale.AuthoredPixelsPerPoint(settings, profile.ScreenWidthPx, profile.ScreenHeightPx, profile.Dpi);
            var decision = ShellPanelScale.Decide(profile, authored);
            var previous = managedPixelsPerPoint;
            savedScale = ShellPanelScale.Apply(settings, decision);
            managedPixelsPerPoint = settings != null && decision.Raised ? decision.EffectivePixelsPerPoint : 0f;
            scaleDecision = decision;
            if (!Mathf.Approximately(previous, managedPixelsPerPoint))
            {
                ReapplyLayout();
            }

            return decision;
        }

        /// <summary>
        /// Sets the shell's text-scale factor (clamped to 1.0 to 2.0, applied in 10 percent steps). Panels size their text with the
        /// shell's font variables, so they follow without code.
        /// </summary>
        public void ApplyTextScale(float factor)
        {
            textScale = ShellTextScale.Clamp(factor);
            if (root == null)
            {
                return;
            }

            var active = ShellTextScale.ClassName(ShellTextScale.Percent(textScale));
            foreach (var step in ShellTextScale.Steps())
            {
                var name = ShellTextScale.ClassName(step);
                root.EnableInClassList(name, name == active);
            }

            ApplyCardCap();
            PlaceHubEntry();
        }

        private void ProbeNow(bool includeFontScale)
        {
            var profile = ShellDeviceProfile.Probe();
            if (!profile.Equals(deviceProfile) || !built)
            {
                var decision = ApplyDeviceProfile(profile);
                if (decision.Reason == ShellScaleReason.UnknownDpiFallback)
                {
                    Debug.Log("Run shell device profile: DPI is unknown on a touch device, so the authored panel scale stays (" + profile + ").");
                }
            }

            if (includeFontScale)
            {
                ApplyTextScale(ShellTextScale.ProbeSystemFactor());
            }
        }

        private void RestorePanelScale()
        {
            ShellPanelScale.Restore(savedScale);
            savedScale = default;
            managedPixelsPerPoint = 0f;
        }

        private void ReapplyLayout()
        {
            if (hasLayoutInputs && IsReady)
            {
                ApplyScreenLayout(layoutScreenWidth, layoutScreenHeight, layoutSafeArea);
            }
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

            hasLayoutInputs = true;
            layoutScreenWidth = screenWidth;
            layoutScreenHeight = screenHeight;
            layoutSafeArea = screenSafeArea;
            // A scale the shell raised itself is in force before the panel reports it (the panel updates a frame later).
            var pixelsPerPoint = managedPixelsPerPoint > 0f ? managedPixelsPerPoint : (root.panel != null ? root.panel.scaledPixelsPerPoint : 1f);
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

            // A rotation (or any resize) ends an ore vein hold: the finger is no longer where it was (FR-042).
            if (hasLastLayoutSize && (Mathf.Abs(width - lastLayoutWidth) > 0.5f || Mathf.Abs(height - lastLayoutHeight) > 0.5f))
            {
                oreVein.CancelHold(OreVeinHoldEnd.Rotation);
            }

            var sizeChanged = hasLastLayoutSize && (Mathf.Abs(width - lastLayoutWidth) > 0.5f || Mathf.Abs(height - lastLayoutHeight) > 0.5f);
            hasLastLayoutSize = true;
            lastLayoutWidth = width;
            lastLayoutHeight = height;
            safeHeightPoints = Mathf.Max(0f, safeArea.height);
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

            PlaceHubEntry();

            // The card docks in the side panel on tablet and desktop and as a band or column in the main row on phones; the camera waits for
            // the new band after a resize or rotation (the band is known before the reframe).
            MountCard();
            ApplyCardCap();
            if (cardOpen && (sizeChanged || previous != Layout))
            {
                MarkCardBandPending();
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
        /// The world layout the unobstructed rectangle is cut for (set by the bootstrap). Unset, or when its orientation differs from the
        /// panel's (a rotation frame), the F004 lattice for the panel's aspect is used, as before.
        /// </summary>
        public FactoryWorldLayout WorldLayout
        {
            get => worldLayout;
            set => worldLayout = value;
        }

        private FactoryWorldLayout WorldLayoutFor(Rect panelBounds)
        {
            var orientation = LatticeLayoutBuilder.OrientationFor(panelBounds.width, panelBounds.height);
            return worldLayout != null && worldLayout.Orientation == orientation ? worldLayout : LatticeLayoutBuilder.Shipped(orientation);
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

            if (cardBandPending)
            {
                // The card was just opened or resized: until its band is laid out the rectangle is the old one, and reframing on it would move
                // the camera twice. A few frames at most, then the layout is taken as it is.
                if (Time.frameCount - cardBandFrame < 4)
                {
                    return false;
                }

                cardBandPending = false;
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
            foreach (var bar in new[] { detailLine, noticeBar, worldHint })
            {
                // The notice, detail and hint bars overlay the first slot row, so the camera frames below them (T192, F-8).
                if (bar != null && bar.resolvedStyle.display != DisplayStyle.None && bar.worldBound.height > 0f && !float.IsNaN(bar.worldBound.yMax))
                {
                    topPoint = Mathf.Max(topPoint, bar.worldBound.yMax);
                }
            }

            if (HubEntryOnItsOwnRow && HubEntryButton.resolvedStyle.display != DisplayStyle.None && !float.IsNaN(HubEntryButton.worldBound.yMax) &&
                bounds.yMax - Mathf.Max(topPoint, HubEntryButton.worldBound.yMax) >= MinWorldBelowHubRow)
            {
                // The phone portrait's entry row is chrome like a notice bar: the camera frames below it while the world keeps a usable height.
                topPoint = Mathf.Max(topPoint, HubEntryButton.worldBound.yMax);
            }

            var leftPoint = zoom.width > 0f ? Mathf.Max(bounds.xMin, zoom.xMax) : bounds.xMin;
            var bottomPoint = bounds.yMax;
            if (HubEntryOverlay && HubEntryButton.resolvedStyle.display != DisplayStyle.None && !float.IsNaN(HubEntryButton.worldBound.yMin) && !float.IsNaN(HubEntryButton.worldBound.xMax))
            {
                // U-1 (C1): the landscape overlay never covers the framed world. As for the ore vein, the rectangle either ends above the entry or starts
                // right of it, whichever leaves the larger fitting scale (the larger area breaks a tie).
                var entry = HubEntryButton.worldBound;
                var above = entry.yMin > topPoint ? Rect.MinMaxRect(leftPoint, topPoint, bounds.xMax, Mathf.Min(bottomPoint, entry.yMin)) : Rect.zero;
                var beside = entry.xMax < bounds.xMax ? Rect.MinMaxRect(Mathf.Max(leftPoint, entry.xMax), topPoint, bounds.xMax, bottomPoint) : Rect.zero;
                var entryLayout = WorldLayoutFor(root.panel.visualTree.worldBound);
                var aboveScale = FittingScale(above, entryLayout, pixelsPerPoint);
                var besideScale = FittingScale(beside, entryLayout, pixelsPerPoint);
                if (beside.width > 0f && (besideScale > aboveScale || (besideScale == aboveScale && beside.width * beside.height > above.width * above.height)))
                {
                    leftPoint = beside.xMin;
                }
                else if (above.width > 0f)
                {
                    bottomPoint = above.yMax;
                }
            }

            var available = Rect.MinMaxRect(leftPoint, topPoint, bounds.xMax, bottomPoint);
            if (oreVeinWanted && IsDisplayed(oreVein.Root))
            {
                // The ore vein overlay sits in the bottom-right corner: the slots are framed outside it (VEIN-1). T463: the cut is
                // corner-aware. The rectangle either ends above the vein (full width) or left of it (full height); the cut that
                // leaves the larger fitting scale wins, the larger area breaks a tie.
                var vein = oreVein.Root.worldBound;
                if (!float.IsNaN(vein.yMin) && !float.IsNaN(vein.xMin) && vein.width > 0f && vein.height > 0f)
                {
                    var above = vein.yMin > topPoint ? Rect.MinMaxRect(leftPoint, topPoint, bounds.xMax, Mathf.Min(bottomPoint, vein.yMin)) : Rect.zero;
                    var beside = vein.xMin > leftPoint ? Rect.MinMaxRect(leftPoint, topPoint, Mathf.Min(bounds.xMax, vein.xMin), bottomPoint) : Rect.zero;
                    var panelBounds = root.panel.visualTree.worldBound;
                    var layout = WorldLayoutFor(panelBounds);
                    var aboveScale = FittingScale(above, layout, pixelsPerPoint);
                    var besideScale = FittingScale(beside, layout, pixelsPerPoint);
                    if (above.width <= 0f && beside.width <= 0f)
                    {
                        // The vein lies outside the world area: nothing to cut.
                    }
                    else if (beside.width > 0f && (besideScale > aboveScale || (besideScale == aboveScale && beside.width * beside.height > above.width * above.height)))
                    {
                        available = beside;
                    }
                    else if (above.width > 0f)
                    {
                        available = above;
                    }
                }
            }

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

        private static int FittingScale(Rect points, FactoryWorldLayout layout, float pixelsPerPoint)
        {
            if (points.width <= 0f || points.height <= 0f)
            {
                return -1;
            }

            var pixels = new Rect(0f, 0f, points.width * pixelsPerPoint, points.height * pixelsPerPoint);
            return CameraScaleModel.LargestFittingScale(pixels, layout.Bounds, SlotGridLayout.Density, layout.Margin);
        }

        // ------------------------------------------------------------------------------------------------------
        // Contracts tab badge (T330, ui-surfaces-f005.md tab-contracts-badge)
        // ------------------------------------------------------------------------------------------------------

        /// <summary>How the Contracts tab shows the number of deliverable contracts.</summary>
        public enum ContractsBadgeMode
        {
            /// <summary>Nothing is deliverable: no badge, the tab reads "Contracts".</summary>
            None = 0,

            /// <summary>"Contracts (n)" (n above 9 reads "9+"; the accessible name carries the exact number).</summary>
            Count = 1,

            /// <summary>"Contracts !" when the count does not fit the tab.</summary>
            Bang = 2,

            /// <summary>The compact fallback: a "•" dot in the corner of the tab.</summary>
            Dot = 3,
        }

        /// <summary>The visible text of the Contracts tab label: "Contracts", "Contracts (n)" or "Contracts !".</summary>
        public string ContractsTabText => contractsTabLabel != null ? contractsTabLabel.text : string.Empty;

        /// <summary>The compact badge glyph (a bullet).</summary>
        public const string DotGlyph = "•";

        /// <summary>The badge text element of the Contracts tab (<c>tab-contracts-badge</c>).</summary>
        public Label ContractsBadge => contractsBadge;

        /// <summary>The number of deliverable contracts the badge was last given.</summary>
        public int ContractsDeliverable => contractsDeliverable;

        /// <summary>The form the badge currently takes (None while nothing is deliverable).</summary>
        public ContractsBadgeMode ContractsBadgeForm => contractsBadgeMode;

        /// <summary>
        /// Sets the number of contracts that can be delivered now (computed by the caller from the contracts snapshot; the shell holds no rule).
        /// 0 clears the badge. The form is chosen by measuring the tab: "Contracts (n)", else "Contracts !", else the "•" dot. The accessible
        /// name (the tooltip) is "Contracts, n deliverable", or "Contracts" when cleared.
        /// </summary>
        public void SetContractsBadge(int deliverable)
        {
            if (!EnsureReady())
            {
                return;
            }

            contractsDeliverable = Math.Max(0, deliverable);
            ApplyContractsBadge();
        }

        /// <summary>The "•" dot on the Research tab while unspent Influence Points exist (R-01); the tab's tooltip is its accessible name.</summary>
        public Label ResearchBadge => researchBadge;

        /// <summary>The unspent IP the Research tab indicator was last given.</summary>
        public long IndustryPoints => industryPoints;

        /// <summary>
        /// Sets the unspent Influence Points (the caller reads them from the permanent state; the shell holds no rule). Above 0 the Research tab
        /// shows a dot and its accessible name reads "Research, N IP to spend in Industry"; 0 clears both.
        /// </summary>
        public void SetIndustryPoints(long ip)
        {
            if (!built)
            {
                return;
            }

            industryPoints = Math.Max(0L, ip);
            var button = tabButtons[(int)ShellTab.Research];
            var any = industryPoints > 0;
            if (researchBadge != null)
            {
                researchBadge.text = any ? DotGlyph : string.Empty;
                researchBadge.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            }

            button.tooltip = any ? "Research, " + QuantityFormatter.Format(industryPoints) + " IP to spend in Industry" : "Research";
        }

        private static string ContractsBadgeCount(int deliverable) =>
            deliverable > 9 ? "9+" : deliverable.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private void ApplyContractsBadge()
        {
            if (!built || contractsBadge == null || contractsTabLabel == null)
            {
                return;
            }

            var button = tabButtons[(int)ShellTab.Contracts];
            if (contractsDeliverable <= 0)
            {
                contractsBadgeMode = ContractsBadgeMode.None;
                contractsBadge.text = string.Empty;
                contractsBadge.style.display = DisplayStyle.None;
                contractsTabLabel.text = "Contracts";
                button.tooltip = "Contracts";
                return;
            }

            var countText = "(" + ContractsBadgeCount(contractsDeliverable) + ")";
            var mode = ContractsBadgeMode.Dot;
            var available = button.contentRect.width;
            if (float.IsNaN(available) || available <= 0f)
            {
                // Not laid out yet: the short form fits every width; the geometry callback refines it.
                mode = ContractsBadgeMode.Count;
            }
            else
            {
                available -= button.resolvedStyle.paddingLeft + button.resolvedStyle.paddingRight + 10f;
                if (MeasureTabText("Contracts " + countText) <= available)
                {
                    mode = ContractsBadgeMode.Count;
                }
                else if (MeasureTabText("Contracts !") <= available)
                {
                    mode = ContractsBadgeMode.Bang;
                }
            }

            contractsBadgeMode = mode;
            contractsTabLabel.text = mode == ContractsBadgeMode.Count ? "Contracts " + countText : mode == ContractsBadgeMode.Bang ? "Contracts !" : "Contracts";
            contractsBadge.text = mode == ContractsBadgeMode.Dot ? DotGlyph : string.Empty;
            contractsBadge.style.display = mode == ContractsBadgeMode.Dot ? DisplayStyle.Flex : DisplayStyle.None;
            button.tooltip = "Contracts, " + contractsDeliverable.ToString(System.Globalization.CultureInfo.InvariantCulture) + " deliverable";
        }

        private float MeasureTabText(string text) =>
            contractsTabLabel.MeasureTextSize(text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x;

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
            CashButton.tooltip = "Cash " + cashValue.text;
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
            StoreButton.tooltip = "Store " + (full ? "full" : percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + " percent used");
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
            // The accessible name leads with the control's name, then its state and the reason (F9: the icon alone never names it).
            OverdriveButton.tooltip = "Overdrive " + caption + ". " + reason;
            overdriveReason = reason;
        }

        private string overdriveReason = string.Empty;

        /// <summary>The player-readable reason of the Overdrive control's current state.</summary>
        public string OverdriveReason => overdriveReason;

        // ------------------------------------------------------------------------------------------------------
        // Notice, availability, state
        // ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Shows a one-line notice with the icon of its kind (info, warning for warnings and rejections, lock). A transient notice
        /// clears after <see cref="NoticeSeconds"/>, on the next press, on a sheet or tab change and when the caller reports it
        /// is no longer true; a <paramref name="sticky"/> notice (a standing condition such as an unavailable factory) clears
        /// only through <see cref="ClearNotice"/>.
        /// </summary>
        public void ShowNotice(string text, ShellNoticeKind kind = ShellNoticeKind.Info, bool sticky = false)
        {
            if (!EnsureReady())
            {
                return;
            }

            noticeTimer?.Pause();
            noticeTimer = null;
            lockNoticeSlot = -1;
            var empty = string.IsNullOrEmpty(text);
            noticeLabel.text = text ?? string.Empty;
            noticeBar.style.display = empty ? DisplayStyle.None : DisplayStyle.Flex;
            noticeBar.EnableInClassList("run-notice--error", !empty && kind == ShellNoticeKind.Error);
            noticeBar.EnableInClassList("run-notice--warning", !empty && kind == ShellNoticeKind.Warning);
            noticeBar.EnableInClassList("run-notice--lock", !empty && kind == ShellNoticeKind.Lock);
            noticeSticky = !empty && sticky;
            NoticeKind = empty ? (ShellNoticeKind?)null : kind;
            if (empty)
            {
                return;
            }

            string iconKey;
            string tint;
            switch (kind)
            {
                case ShellNoticeKind.Warning:
                case ShellNoticeKind.Error:
                    iconKey = "F003-UI-ICON-WARNING";
                    tint = kind == ShellNoticeKind.Error ? "run-icon--tint-danger" : "run-icon--tint-warning";
                    break;
                case ShellNoticeKind.Lock:
                    iconKey = "F003-UI-ICON-LOCK";
                    tint = "run-icon--tint-muted";
                    break;
                default:
                    iconKey = "F003-UI-ICON-INFO";
                    tint = "run-icon--tint-accent";
                    break;
            }

            SetIcon(noticeIcon, iconKey);
            foreach (var name in new[] { "run-icon--tint-danger", "run-icon--tint-warning", "run-icon--tint-muted", "run-icon--tint-accent" })
            {
                noticeIcon.EnableInClassList(name, name == tint);
            }

            if (!noticeSticky && NoticeSeconds > 0f)
            {
                var shown = noticeLabel.text;
                noticeTimer = noticeBar.schedule.Execute(() =>
                {
                    if (!noticeSticky && noticeLabel.text == shown)
                    {
                        ClearNotice();
                    }
                }).StartingIn((long)(NoticeSeconds * 1000f));
            }
        }

        /// <summary>Clears the notice, including a sticky one. While the factory is unavailable its standing notice returns.</summary>
        public void ClearNotice()
        {
            ShowNotice(string.Empty);
            if (IsReady && !gameplayAvailable && !string.IsNullOrEmpty(unavailableReason))
            {
                ShowNotice(unavailableReason, ShellNoticeKind.Warning, true);
            }
        }

        /// <summary>Clears a transient notice (a press, a sheet or tab change); a standing condition stays.</summary>
        public void ClearTransientNotice()
        {
            if (IsReady && !noticeSticky && !string.IsNullOrEmpty(noticeLabel.text))
            {
                ClearNotice();
            }
        }

        /// <summary>
        /// A press on the world (a tap or the start of a pan): a pinned detail line unpins and a transient notice clears, as a
        /// press on the UI elsewhere does.
        /// </summary>
        public void NotifyWorldPress()
        {
            if (!IsReady)
            {
                return;
            }

            if (pinnedHost != null)
            {
                Unpin();
            }

            ClearTransientNotice();
        }

        /// <summary>
        /// Clears the locked-slot notice once the slot it names is no longer locked (the factory level rose). The presenter
        /// calls it with the slot's current condition after every render.
        /// </summary>
        public void ReconcileLockNotice(int slot, SlotCondition condition)
        {
            if (lockNoticeSlot >= 0 && lockNoticeSlot == slot && condition != SlotCondition.Locked)
            {
                ClearNotice();
            }
        }

        /// <summary>
        /// Tells the shell whether the grid has no machine yet. On a phone with the sheet closed it then shows the "Build a
        /// Miner" hint of the slot grid's empty state; tablet and desktop show the guidance in the permanent panel.
        /// </summary>
        public void SetEmptyHint(bool gridEmpty)
        {
            emptyHintWanted = gridEmpty;
            UpdateEmptyHint();
        }

        private void UpdateEmptyHint()
        {
            if (worldHint == null)
            {
                return;
            }

            var show = IsReady && emptyHintWanted && !sheetOpen && !IsSidePanelLayout;
            worldHint.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Gates gameplay panels while the factory is not available (a blocked return, a pending transition).</summary>
        public void SetGameplayAvailable(bool available, string reason = null)
        {
            var previousReason = unavailableReason;
            gameplayAvailable = available;
            unavailableReason = available ? string.Empty : (reason ?? RunRejectionCopy.For(RunReasons.LifecycleBlocked));
            if (!EnsureReady())
            {
                return;
            }

            root.EnableInClassList("run-shell--unavailable", !available);
            oreVein.SetUnavailable(available ? null : unavailableReason);
            if (!available)
            {
                ShowNotice(unavailableReason, ShellNoticeKind.Warning, true);
            }
            else if (!string.IsNullOrEmpty(previousReason) && NoticeText == previousReason)
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

            ClearTransientNotice();
            DismissCard(false);
            detailsFromCard = false;
            SetSelectedSlot(-1);
            activeTab = tab;
            sheetOpen = true;
            ShowPanel(ShellPanelIds.ForTab(tab), TabNames[(int)tab], -1);
            UpdateSheetState();
        }

        /// <summary>
        /// Selects a tab and scrolls one of its panel's sections to the top of the sheet (R-01: the Prestige panel opens Research at Industry).
        /// The panel is laid out a frame or two after it is shown, so the reveal is repeated until the layout holds, and the last pass moves
        /// focus into the section so the control that was pressed does not leave focus behind.
        /// </summary>
        public void OpenTabAtSection(ShellTab tab, string sectionId)
        {
            if (!EnsureReady())
            {
                return;
            }

            SelectTab(tab);
            if (!(activePanel is IShellSectionPanel section))
            {
                return;
            }

            section.RevealSection(sectionId, false);
            foreach (var delay in new long[] { 0, 40, 120 })
            {
                var last = delay == 120;
                root.schedule.Execute(() =>
                {
                    if (ReferenceEquals(activePanel, section))
                    {
                        section.RevealSection(sectionId, last);
                    }
                }).ExecuteLater(delay);
            }
        }

        /// <summary>
        /// Opens the Settings sheet (T433). It replaces an open tab or slot sheet: there is one sheet host. Does nothing while the
        /// shell is not ready. When no Settings panel is registered the sheet opens with the placeholder under the title "Settings"
        /// (it does not check the registry).
        /// </summary>
        public void OpenSettings()
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
            ShowPanel(ShellPanelIds.Settings, SettingsName, -1);
            UpdateSheetState();
        }

        /// <summary>Closes the Settings sheet and returns focus to the gear. Does nothing when it is not open.</summary>
        public void CloseSettings()
        {
            if (!EnsureReady() || !settingsOpen)
            {
                return;
            }

            var fromHub = hubInvokedPanelId != null && hubInvokedPanelId == activePanelId;
            CloseSheet(false);
            FocusWhenReady(fromHub ? HubEntryButton : SettingsButton);
        }

        private void ToggleSettings()
        {
            if (settingsOpen)
            {
                CloseSettings();
            }
            else
            {
                OpenSettings();
            }
        }

        /// <summary>
        /// Opens the build sheet (an empty slot) or the machine panel (an occupied slot). A locked slot opens nothing and
        /// shows the level that unlocks it. Returns whether a sheet opened.
        /// </summary>
        public bool OpenSlot(int slot, SlotCondition condition, int unlockLevel = 0, bool focusCard = false)
        {
            if (!EnsureReady())
            {
                return false;
            }

            if (condition == SlotCondition.Locked)
            {
                ShowNotice("Slot " + (slot + 1) + " unlocks at factory level " + unlockLevel + ".", ShellNoticeKind.Lock);
                lockNoticeSlot = slot;
                return false;
            }

            if (condition == SlotCondition.Occupied && HasMachineCard)
            {
                // T534: a machine opens the docked card; its Details view is the former machine panel.
                return OpenMachineCard(slot, focusCard);
            }

            ClearTransientNotice();
            DismissCard(false);
            detailsFromCard = false;
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
            if (cardOpen && slot == cardSlot && condition != SlotCondition.Occupied)
            {
                // A removed machine closes the card and says so (FR-016, U-9). The card also checks its own instance every refresh.
                CloseMachineCard(true, MachineCardCopy.Gone);
                return;
            }

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
            if (id == ShellPanelIds.Machine && activePanelId == ShellPanelIds.Build && HasMachineCard)
            {
                // A machine was built at the selected location: the build sheet hands over to the new machine's card.
                OpenMachineCard(slot);
                return;
            }

            if (id != activePanelId)
            {
                detailsFromCard = false;
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
            var wasSettings = settingsOpen;
            var wasHub = IsHubFamilyOpen || (hubInvokedPanelId != null && hubInvokedPanelId == activePanelId);
            hubInvokedPanelId = null;
            sheetOpen = false;
            detailsFromCard = false;
            activeTab = ShellTab.Factory;
            ClearTransientNotice();
            DismissCard(false);
            SetSelectedSlot(-1);
            ShowPanel(null, null, -1);
            UpdateSheetState();
            if (returnFocusToTab && focusWasInside)
            {
                FocusWhenReady(wasHub ? HubEntryButton : wasSettings ? SettingsButton : TabButton(ShellTab.Factory));
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

            return navigationStack.HandleBack(this, Time.frameCount, Application.isPlaying);
        }

        // The back frames the navigation stack pops (R11): the policy lives in ShellNavigationStack, the actions stay here.
        bool IShellBackFrames.HasDialog => dialogRequest != null;
        bool IShellBackFrames.HasPinnedValue => pinnedHost != null;
        bool IShellBackFrames.IsSettingsOpen => settingsOpen;
        bool IShellBackFrames.HasClosableSheet => sheetHost.IsBackClosable(selectedSlot >= 0, activeTab != ShellTab.Factory || IsHubFamilyOpen);
        void IShellBackFrames.PopDialog() => CloseDialog(ConfirmationCloseReason.Back);
        void IShellBackFrames.PopPinnedValue() => Unpin();
        void IShellBackFrames.PopSettings() => CloseSettings();
        void IShellBackFrames.PopSheet() => CloseSheetOrDetails();
        bool IShellBackFrames.HasCard => cardOpen;
        void IShellBackFrames.PopCard() => CloseMachineCard();

        /// <summary>The shown panel changed because the run read model changed: lets a visible panel refresh itself.</summary>
        public void RefreshActivePanel() => activePanel?.Refresh();

        // ------------------------------------------------------------------------------------------------------
        // Machine card (T534, FR-016 to FR-019, research R10)
        // ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Opens the docked machine card for an occupied slot, or switches it to another slot. The card is exclusive with every sheet: an open
        /// sheet, tab panel or Settings closes first, and the selection is the slot (the camera keeps it in the band the card leaves).
        /// Returns false when no card is registered.
        /// </summary>
        public bool OpenMachineCard(int slot, bool focusCard = false)
        {
            if (!EnsureReady() || !HasMachineCard || slot < 0)
            {
                return false;
            }

            ClearTransientNotice();
            if (sheetOpen)
            {
                sheetOpen = false;
                detailsFromCard = false;
                activeTab = ShellTab.Factory;
                ShowPanel(null, null, -1);
            }

            var wasOpen = cardOpen;
            RebindCard(false);
            activeTab = ShellTab.Factory;
            cardOpen = true;
            cardSlot = slot;
            SetSelectedSlot(slot);
            MountCard();
            ApplyCardCap();
            root.EnableInClassList("run-shell--card-open", true);
            if (!wasOpen)
            {
                MarkCardBandPending();
            }

            cardPanel.OnShown(new ShellPanelContext(this, slot));
            UpdateSheetState();
            if (focusCard && cardOpen)
            {
                FocusWhenReady(cardPanel.Root.Q<Button>("machine-card-upgrade"));
            }

            return true;
        }

        /// <summary>
        /// Closes the machine card and clears the selection (the camera restores the prior view). With a <paramref name="notice"/> the shell
        /// shows it. When focus was inside the card it returns to the location that was selected (the keyboard stop, with its cursor there).
        /// </summary>
        public void CloseMachineCard(bool restoreFocus = true, string notice = null)
        {
            if (!EnsureReady() || !cardOpen)
            {
                return;
            }

            var slot = cardSlot;
            var focused = root.panel?.focusController?.focusedElement as VisualElement;
            var focusWasInside = focused != null && cardPanel != null && cardPanel.Root.Contains(focused);
            DismissCard(true);
            if (!string.IsNullOrEmpty(notice))
            {
                ShowNotice(notice, ShellNoticeKind.Info);
            }

            UpdateSheetState();
            if (restoreFocus && focusWasInside)
            {
                FocusLocation(slot);
            }
        }

        /// <summary>
        /// Opens the Details view of the card's machine: the former machine panel in the sheet. The card hides, the selection stays, and closing
        /// the sheet (Close, Back, Escape) returns to the card with focus on its Details control.
        /// </summary>
        public bool OpenDetails(int slot)
        {
            if (!EnsureReady() || slot < 0)
            {
                return false;
            }

            var fromCard = cardOpen;
            DismissCard(false);
            ClearTransientNotice();
            activeTab = ShellTab.Factory;
            sheetOpen = true;
            SetSelectedSlot(slot);
            ShowPanel(ShellPanelIds.Machine, "Details \u00B7 Location " + (slot + 1), slot);
            detailsFromCard = fromCard;
            UpdateSheetState();
            if (fromCard)
            {
                FocusWhenReady(activePanel?.Root.Q<Button>() ?? CloseButton);
            }

            return true;
        }

        /// <summary>Close for a sheet: the Details view returns to the card it was opened from; any other sheet closes as <see cref="CloseSheet"/>.</summary>
        public void CloseSheetOrDetails()
        {
            if (!EnsureReady())
            {
                return;
            }

            if (detailsFromCard && sheetOpen && selectedSlot >= 0 && activePanelId == ShellPanelIds.Machine && HasMachineCard)
            {
                var slot = selectedSlot;
                detailsFromCard = false;
                sheetOpen = false;
                activeTab = ShellTab.Factory;
                ClearTransientNotice();
                ShowPanel(null, null, -1);
                UpdateSheetState();
                if (OpenMachineCard(slot))
                {
                    FocusWhenReady(cardPanel.Root.Q<Button>("machine-card-details"));
                }
                else
                {
                    CloseSheet();
                }

                return;
            }

            CloseSheet();
        }

        /// <summary>A session was replaced or the player returned from offline: the card forgets its model baselines (P2-F12); with <paramref name="suppressDeltas"/> the next refresh reports no delta.</summary>
        public void ResetCardModels(bool suppressDeltas)
        {
            (cardPanel as IMachineCardModels)?.ResetModels(suppressDeltas);
        }

        /// <summary>Per-frame update of the open card (the card refreshes its model every frame).</summary>
        public void RefreshCard()
        {
            if (cardOpen)
            {
                cardPanel?.Refresh();
            }
        }

        // Hides the card without touching the selection (a sheet or another selection takes over), or with clearing it.
        private void DismissCard(bool clearSelection)
        {
            if (!cardOpen)
            {
                return;
            }

            cardOpen = false;
            cardSlot = -1;
            cardBandPending = false;
            cardPanel?.OnHidden();
            root?.EnableInClassList("run-shell--card-open", false);
            if (clearSelection)
            {
                SetSelectedSlot(-1);
            }
        }

        // Re-reads the registered card (a session replace registers a new one) and mounts it. A card that was open for the old session closes: a
        // world replacement clears the stack.
        private void RebindCard(bool closeOpen = true)
        {
            if (root == null)
            {
                return;
            }

            registry.TryGet(ShellPanelIds.MachineCard, out var registered);
            if (ReferenceEquals(registered, cardPanel))
            {
                return;
            }

            if (closeOpen && cardOpen)
            {
                DismissCard(true);
            }

            cardMountedRoot?.RemoveFromHierarchy();
            cardMountedRoot = null;
            cardPanel = registered;
            MountCard();
            ApplyCardCap();
        }

        private void MountCard()
        {
            if (cardPanel == null || root == null || panelHost == null || main == null)
            {
                return;
            }

            var element = cardPanel.Root;
            var parent = IsSidePanelLayout ? panelHost : main;
            if (element.parent != parent)
            {
                element.RemoveFromHierarchy();
                if (parent == panelHost)
                {
                    parent.Insert(parent.IndexOf(panelScroll), element);
                }
                else
                {
                    parent.Add(element);
                }
            }

            if (!ReferenceEquals(cardMountedRoot, element))
            {
                cardMountedRoot = element;
                element.RegisterCallback<GeometryChangedEvent>(OnCardGeometry);
            }
        }

        private void OnCardGeometry(GeometryChangedEvent evt)
        {
            if (cardBandPending && cardOpen && cardMountedRoot != null && cardMountedRoot.layout.height > 0f)
            {
                cardBandPending = false;
            }
        }

        private void MarkCardBandPending()
        {
            cardBandPending = true;
            cardBandFrame = Time.frameCount;
        }

        /// <summary>
        /// The band the card may take in phone portrait: at most 30 percent of the safe height at 100 to 140 percent text, and a hard cap of 45
        /// percent above that (FR-016, OD-14). Other classes take their column or the side panel and are not capped.
        /// </summary>
        public static float CardBandFraction(int textScalePercent) => textScalePercent <= 140 ? 0.30f : 0.45f;

        private void ApplyCardCap()
        {
            if (cardMountedRoot == null)
            {
                return;
            }

            if (Layout == FactoryHudLayout.CompactPortrait && safeHeightPoints > 0f)
            {
                cardMountedRoot.style.maxHeight = safeHeightPoints * CardBandFraction(TextScalePercent);
            }
            else
            {
                cardMountedRoot.style.maxHeight = StyleKeyword.Null;
            }
        }

        // ------------------------------------------------------------------------------------------------------
        // Keyboard stop of the world locations (T534, R10: persistent layer, locations by SortKeyOf, then the card)
        // ------------------------------------------------------------------------------------------------------

        private void BuildLocationStop()
        {
            locationStop = new VisualElement { name = "location-stop", focusable = true, pickingMode = PickingMode.Ignore, tooltip = LocationStopName };
            locationStop.AddToClassList("run-control");
            locationStop.AddToClassList("run-location-stop");
            var style = locationStop.style;
            style.position = Position.Absolute;
            style.left = 0f;
            style.top = 0f;
            style.width = 48f;
            style.height = 48f;
            style.backgroundColor = Color.clear;
            style.borderLeftWidth = 0f;
            style.borderRightWidth = 0f;
            style.borderTopWidth = 0f;
            style.borderBottomWidth = 0f;
            worldColumn.Add(locationStop);
            locationStop.RegisterCallback<KeyDownEvent>(OnLocationKeyDown, TrickleDown.TrickleDown);
            locationStop.RegisterCallback<NavigationMoveEvent>(OnLocationNavigationMove, TrickleDown.TrickleDown);
            locationStop.RegisterCallback<NavigationSubmitEvent>(OnLocationSubmit, TrickleDown.TrickleDown);
            locationStop.RegisterCallback<FocusInEvent>(evt => PositionLocationStop());
        }

        /// <summary>Sets the keyboard order of the locations (slot indices). The cursor stays on its slot when that slot is still in the order.</summary>
        public void SetLocationOrder(IReadOnlyList<int> order)
        {
            var current = LocationCursorSlot;
            locationOrder.Clear();
            if (order != null)
            {
                for (var i = 0; i < order.Count; i++)
                {
                    locationOrder.Add(order[i]);
                }
            }

            var index = current >= 0 ? locationOrder.IndexOf(current) : -1;
            locationCursor = index >= 0 ? index : 0;
        }

        /// <summary>Moves the cursor to a slot and focuses the stop: used when focus returns to a location (the card closed).</summary>
        public void FocusLocation(int slot)
        {
            if (locationStop == null)
            {
                return;
            }

            var index = locationOrder.IndexOf(slot);
            if (index >= 0)
            {
                locationCursor = index;
            }

            PositionLocationStop();
            FocusWhenReady(locationStop);
        }

        /// <summary>Re-places the stop on the cursor's location (the camera moved). The bootstrap calls it every frame while the stop has focus.</summary>
        public void RefreshLocationStop()
        {
            if (locationStop != null && locationStop.focusController?.focusedElement == locationStop)
            {
                PositionLocationStop();
            }
        }

        private void PositionLocationStop()
        {
            if (locationStop == null || worldColumn == null)
            {
                return;
            }

            var slot = LocationCursorSlot;
            var style = locationStop.style;
            var rect = slot >= 0 && LocationRectProvider != null ? LocationRectProvider(slot) : null;
            if (rect.HasValue && !float.IsNaN(worldColumn.worldBound.x))
            {
                var origin = worldColumn.worldBound;
                var width = Mathf.Max(48f, rect.Value.width);
                var height = Mathf.Max(48f, rect.Value.height);
                style.left = rect.Value.center.x - width * 0.5f - origin.xMin;
                style.top = rect.Value.center.y - height * 0.5f - origin.yMin;
                style.width = width;
                style.height = height;
            }
            else
            {
                style.left = 0f;
                style.top = 0f;
                style.width = 48f;
                style.height = 48f;
            }

            if (ringTarget == locationStop)
            {
                PositionFocusRing();
            }
        }

        private void MoveLocationCursor(int direction, bool toEnd = false)
        {
            if (locationOrder.Count == 0)
            {
                return;
            }

            // The input system raises a key event and a navigation event for one arrow press: act on the first only.
            if (!toEnd && lastLocationMoveFrame == Time.frameCount && lastLocationMoveDirection == direction && Application.isPlaying)
            {
                return;
            }

            lastLocationMoveFrame = Time.frameCount;
            lastLocationMoveDirection = direction;
            locationCursor = toEnd
                ? (direction < 0 ? 0 : locationOrder.Count - 1)
                : Mathf.Clamp(locationCursor + direction, 0, locationOrder.Count - 1);
            PositionLocationStop();
        }

        private void ActivateLocation()
        {
            if (locationOrder.Count == 0)
            {
                return;
            }

            if (lastLocationActivateFrame == Time.frameCount && Application.isPlaying)
            {
                return;
            }

            lastLocationActivateFrame = Time.frameCount;
            LocationActivated?.Invoke(LocationCursorSlot);
        }

        private void OnLocationKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.LeftArrow:
                case KeyCode.UpArrow:
                    MoveLocationCursor(-1);
                    break;
                case KeyCode.RightArrow:
                case KeyCode.DownArrow:
                    MoveLocationCursor(1);
                    break;
                case KeyCode.Home:
                    MoveLocationCursor(-1, true);
                    break;
                case KeyCode.End:
                    MoveLocationCursor(1, true);
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    ActivateLocation();
                    break;
                default:
                    return;
            }

            evt.StopPropagation();
            locationStop.focusController?.IgnoreEvent(evt);
        }

        private void OnLocationNavigationMove(NavigationMoveEvent evt)
        {
            switch (evt.direction)
            {
                case NavigationMoveEvent.Direction.Left:
                case NavigationMoveEvent.Direction.Up:
                    MoveLocationCursor(-1);
                    break;
                case NavigationMoveEvent.Direction.Right:
                case NavigationMoveEvent.Direction.Down:
                    MoveLocationCursor(1);
                    break;
                default:
                    return;
            }

            evt.StopPropagation();
            locationStop.focusController?.IgnoreEvent(evt);
        }

        private void OnLocationSubmit(NavigationSubmitEvent evt)
        {
            ActivateLocation();
            evt.StopPropagation();
        }

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
            settingsOpen = id == ShellPanelIds.Settings;
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
            UpdateEmptyHint();
            UpdateOreVeinVisibility();
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
            SetIcon(DialogConfirmButton, request.Destructive ? "F003-UI-ICON-WARNING" : null);
            dialogIcon.EnableInClassList("run-icon--tint-danger", request.Destructive);
            dialogIcon.EnableInClassList("run-icon--tint-accent", !request.Destructive);
            dialogLayer.style.display = DisplayStyle.Flex;
            oreVein.SetDialogOpen(true);
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
            oreVein.SetDialogOpen(false);
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
                // Hover shows the exact value only on a device whose primary input is a pointer (device profile).
                if (evt.pointerType == UnityEngine.UIElements.PointerType.mouse && owner.deviceProfile.ShowsHover)
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

            /// <summary>
            /// Tap-to-pin and long press belong to touch: the device profile enables them for every press, and on a pointer
            /// device only a touch or pen press (a hybrid screen) uses them, never a mouse button.
            /// </summary>
            private bool IsTouchGesture(string pointerType) =>
                owner.deviceProfile.PinsOnTapAndLongPress || pointerType != UnityEngine.UIElements.PointerType.mouse;

            private void OnPointerDown(PointerDownEvent evt)
            {
                longPressFired = false;
                CancelLongPress();
                if (!IsTouchGesture(evt.pointerType))
                {
                    return;
                }

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

                if (!IsTouchGesture(evt.pointerType))
                {
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

        /// <summary>
        /// The shared focus rule for a control that is about to be hidden (contracts/ui-surfaces-f005.md, Controls): when the control, or
        /// something inside it, holds focus, focus moves to the next visible control in the focus order, or the previous one when it was
        /// the last. Call it before hiding, while the control is still in the focus order. Returns whether focus moved (T362, #398 T4).
        /// </summary>
        public bool MoveFocusBeforeHiding(VisualElement control)
        {
            var focused = control?.focusController?.focusedElement as VisualElement;
            if (focused == null || (focused != control && !control.Contains(focused)))
            {
                return false;
            }

            var order = GetFocusOrder();
            var at = -1;
            for (var i = 0; i < order.Count && at < 0; i++)
            {
                if (order[i] == focused)
                {
                    at = i;
                }
            }

            VisualElement target = null;
            for (var i = at + 1; at >= 0 && i < order.Count && target == null; i++)
            {
                if (order[i] != control && !control.Contains(order[i]))
                {
                    target = order[i];
                }
            }

            for (var i = at - 1; at >= 0 && i >= 0 && target == null; i--)
            {
                if (order[i] != control && !control.Contains(order[i]))
                {
                    target = order[i];
                }
            }

            if (target == null)
            {
                return false;
            }

            FocusWhenReady(target);
            return true;
        }

        /// <summary>Gives an element focus as soon as it can take it (its style may not be resolved the frame it was shown).</summary>
        public void FocusWhenReady(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            // A newer request supersedes an older one that is still retrying.
            focusRetry?.Pause();
            focusRetry = null;
            FocusRetryPending = false;

            var initial = element.focusController?.focusedElement;
            element.Focus();
            if (element.focusController?.focusedElement == element)
            {
                return;
            }

            // The retry stops as soon as it is done, the element is gone, or focus has moved to some other element since the
            // request (the player clicked or tabbed elsewhere): taking focus back then would be a focus steal (F8, T362).
            var attempts = 0;
            IVisualElementScheduledItem item = null;
            item = element.schedule.Execute(() =>
            {
                attempts++;
                var current = element.focusController?.focusedElement;
                var movedElsewhere = current != null && current != element && current != initial;
                if (current == element || movedElsewhere || element.panel == null || attempts > 12)
                {
                    item.Pause();
                    if (focusRetry == item)
                    {
                        focusRetry = null;
                        FocusRetryPending = false;
                    }

                    return;
                }

                element.Focus();
                if (element.focusController?.focusedElement == element)
                {
                    item.Pause();
                    if (focusRetry == item)
                    {
                        focusRetry = null;
                        FocusRetryPending = false;
                    }
                }
            }).Every(16);
            focusRetry = item;
            focusRetryElement = element;
            FocusRetryPending = true;
        }

        private IVisualElementScheduledItem focusRetry;
        private VisualElement focusRetryElement;
        private bool focusRetryFlag;

        /// <summary>True while a <see cref="FocusWhenReady"/> request is still retrying (tests read it to prove the retry stopped). An element that left its panel can no longer be retried.</summary>
        public bool FocusRetryPending
        {
            get => focusRetryFlag && focusRetryElement != null && focusRetryElement.panel != null;
            private set => focusRetryFlag = value;
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
            ScrollIntoView(target);
        }

        /// <summary>
        /// Scrolls the nearest scroll view whose content holds <paramref name="target"/>. <see cref="ScrollView.ScrollTo"/> throws
        /// an <see cref="ArgumentException"/> for an element outside the content container, and a scroll view's own scroll bars,
        /// sliders and drag containers are its children but not its content (F8, T362). Such a target has nothing to scroll to,
        /// so it is skipped; an outer scroll view that does hold it can still scroll.
        /// </summary>
        public static bool ScrollIntoView(VisualElement target)
        {
            for (var parent = target?.parent; parent != null; parent = parent.parent)
            {
                if (parent is ScrollView scroll)
                {
                    var content = scroll.contentContainer;
                    if (content != null && target != content && content.Contains(target))
                    {
                        scroll.ScrollTo(target);
                        return true;
                    }
                }
            }

            return false;
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
            // The ring is drawn outside the control; on a control at the panel edge the outer part would fall off screen, so the
            // ring is clamped to the panel and lies over the control's own edge on that side (T192, F-3).
            var left = Mathf.Max(bounds.xMin - ringWidth, origin.xMin);
            var top = Mathf.Max(bounds.yMin - ringWidth, origin.yMin);
            var right = Mathf.Min(bounds.xMax + ringWidth, origin.xMax);
            var bottom = Mathf.Min(bounds.yMax + ringWidth, origin.yMax);
            focusRing.style.display = DisplayStyle.Flex;
            focusRing.style.left = left - origin.xMin;
            focusRing.style.top = top - origin.yMin;
            focusRing.style.width = Mathf.Max(0f, right - left);
            focusRing.style.height = Mathf.Max(0f, bottom - top);
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
            var sprite = key == null ? null : iconResolver(key);
            element.style.backgroundImage = sprite == null ? new StyleBackground(StyleKeyword.None) : new StyleBackground(sprite);
        }

        /// <summary>The icon key of the gear in the top strip (T441: the restyled glyph replaces the hand-drawn USS gear).</summary>
        public const string SettingsIconKey = "F004-UI-ICON-SETTINGS";

        /// <summary>True when the gear shows the restyled sprite; false while it still shows the F003 USS gear (no sprite resolver).</summary>
        public bool SettingsUsesSprite { get; private set; }

        private void ApplySettingsGlyph(VisualElement glyph)
        {
            if (glyph == null)
            {
                return;
            }

            SetIcon(glyph, SettingsIconKey);
            SettingsUsesSprite = glyph.style.backgroundImage.value.sprite != null;
            glyph.EnableInClassList("run-gear__icon--sprite", SettingsUsesSprite);
        }

        private void ApplyIcons()
        {
            SetIcon(root.Q<VisualElement>("cash-icon"), "F003-UI-ICON-CASH");
            SetIcon(root.Q<VisualElement>("rp-icon"), "F003-UI-ICON-RP");
            SetIcon(root.Q<VisualElement>("store-icon"), "F003-UI-ICON-STORAGE");
            SetIcon(root.Q<VisualElement>("panel-close-icon"), "F003-UI-ICON-CLOSE");
            SetIcon(root.Q<VisualElement>("world-hint-icon"), "F003-UI-ICON-BUILD");
            ApplySettingsGlyph(root.Q<VisualElement>("settings-icon"));
            ApplyHubIcon();
            foreach (var host in new VisualElement[] { CashButton, RpButton, StoreButton })
            {
                SetIcon(PinHintOf(host), "F003-UI-ICON-INFO");
            }

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

            tabButtons[(int)ShellTab.Contracts].RegisterCallback<GeometryChangedEvent>(evt => ApplyContractsBadge());
            tabBar.RegisterCallback<KeyDownEvent>(OnTabKeyDown);
            tabBar.RegisterCallback<NavigationMoveEvent>(OnTabNavigationMove);
            CloseButton.clicked += () => CloseSheetOrDetails();
            CloseButton.tooltip = "Close";
            ZoomInButton.clicked += () => OnZoomClicked(1);
            ZoomOutButton.clicked += () => OnZoomClicked(-1);
            OverdriveButton.clicked += OnOverdriveClicked;
            SettingsButton.clicked += ToggleSettings;
            SettingsButton.tooltip = SettingsName;
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
            BindHub();
        }

        /// <summary>The accessible name and tooltip of the gear.</summary>
        public const string SettingsName = "Settings";

        /// <summary>The reason shown when the zoom-in button is activated at the largest level (owner decision B1, 2026-11-03).</summary>
        public const string ZoomMaximumReason = "Maximum zoom";

        /// <summary>The reason shown when the zoom-out button is activated at the smallest level.</summary>
        public const string ZoomMinimumReason = "Minimum zoom";

        /// <summary>The accessible name and tooltip of the zoom-in button away from the limit.</summary>
        public const string ZoomInName = "Zoom in";

        /// <summary>The accessible name and tooltip of the zoom-out button away from the limit.</summary>
        public const string ZoomOutName = "Zoom out";

        /// <summary>The accessible name and tooltip of the zoom-in button at the maximum zoom (the limit state is part of the name).</summary>
        public const string ZoomInAtLimitName = "Zoom in, maximum zoom reached";

        /// <summary>The accessible name and tooltip of the zoom-out button at the minimum zoom.</summary>
        public const string ZoomOutAtLimitName = "Zoom out, minimum zoom reached";

        /// <summary>How long the zoom limit hint stays before it clears on its own.</summary>
        public const float ZoomHintSeconds = 3f;

        /// <summary>The zoom limit hint element beside the zoom column (empty and not displayed when no hint shows).</summary>
        public Label ZoomHint => zoomHint;

        /// <summary>The reason text currently shown beside the zoom column, or empty.</summary>
        public string ZoomHintText => zoomHint != null ? zoomHint.text : string.Empty;

        /// <summary>
        /// Applies the camera's zoom range to the buttons (contracts/camera-scale.md rule 4): a button is hidden when the range has a single
        /// level; at an end of a longer range it keeps the soft-disabled look and its tooltip, stays focusable, and activating it by tap, click or
        /// Enter shows the reason instead of zooming. The hint clears when the button leaves its limit.
        /// </summary>
        public void SetZoomButtonStates(bool zoomInVisible, bool zoomInAtLimit, bool zoomOutVisible, bool zoomOutAtLimit)
        {
            if (!EnsureReady())
            {
                return;
            }

            if (zoomStateKnown && this.zoomInAtLimit == (zoomInVisible && zoomInAtLimit) && this.zoomOutAtLimit == (zoomOutVisible && zoomOutAtLimit) &&
                zoomInShown == zoomInVisible && zoomOutShown == zoomOutVisible)
            {
                return;
            }

            zoomStateKnown = true;
            zoomInShown = zoomInVisible;
            zoomOutShown = zoomOutVisible;
            this.zoomInAtLimit = zoomInVisible && zoomInAtLimit;
            this.zoomOutAtLimit = zoomOutVisible && zoomOutAtLimit;
            ApplyZoomButton(ZoomInButton, zoomInVisible, this.zoomInAtLimit, ZoomInName, ZoomInAtLimitName);
            ApplyZoomButton(ZoomOutButton, zoomOutVisible, this.zoomOutAtLimit, ZoomOutName, ZoomOutAtLimitName);
            if (zoomHint != null && zoomHintDirection != 0 &&
                ((zoomHintDirection > 0 && !this.zoomInAtLimit) || (zoomHintDirection < 0 && !this.zoomOutAtLimit)))
            {
                ClearZoomHint();
            }
        }

        private static void ApplyZoomButton(Button button, bool visible, bool atLimit, string name, string nameAtLimit)
        {
            button.style.display = visible ? new StyleEnum<DisplayStyle>(StyleKeyword.Null) : (StyleEnum<DisplayStyle>)DisplayStyle.None;
            SetSoftDisabled(button, atLimit);
            button.tooltip = atLimit ? nameAtLimit : name;
        }

        private void OnZoomClicked(int direction)
        {
            var atLimit = direction > 0 ? zoomInAtLimit : zoomOutAtLimit;
            if (atLimit)
            {
                ShowZoomHint(direction);
                return;
            }

            ClearZoomHint();
            ZoomRequested?.Invoke(direction);
        }

        private void ShowZoomHint(int direction)
        {
            var reason = direction > 0 ? ZoomMaximumReason : ZoomMinimumReason;
            if (zoomHint == null)
            {
                ShowNotice(reason, ShellNoticeKind.Info);
                return;
            }

            zoomHintTimer?.Pause();
            zoomHintDirection = direction;
            zoomHint.text = reason;
            zoomHint.style.display = DisplayStyle.Flex;
            zoomHintTimer = zoomHint.schedule.Execute(ClearZoomHint).StartingIn((long)(ZoomHintSeconds * 1000f));
        }

        private void ClearZoomHint()
        {
            zoomHintTimer?.Pause();
            zoomHintTimer = null;
            zoomHintDirection = 0;
            if (zoomHint != null)
            {
                zoomHint.text = string.Empty;
                zoomHint.style.display = StyleKeyword.Null;
            }
        }

        private void OnRootPointerDown(PointerDownEvent evt)
        {
            // A press elsewhere than on a zoom button clears the zoom limit hint.
            if (!(evt.target is VisualElement zoomTarget && (zoomTarget == ZoomInButton || zoomTarget == ZoomOutButton)))
            {
                ClearZoomHint();
            }

            // A press elsewhere clears a transient notice (it was about an earlier interaction).
            ClearTransientNotice();

            // A press elsewhere closes a pinned detail line.
            if (pinnedHost != null && !(evt.target is VisualElement target && (pinnedHost == target || pinnedHost.Contains(target))))
            {
                Unpin();
            }
        }

        private void OnRegistryChanged(string id)
        {
            if (IsReady && id == ShellPanelIds.MachineCard)
            {
                RebindCard();
                return;
            }

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

            if (IsReady && AutoProbeDevice)
            {
                var now = Time.unscaledTime;
                if (now >= nextProbeTime)
                {
                    nextProbeTime = now + 0.5f;
                    ProbeNow(now >= nextFontScaleTime);
                    if (now >= nextFontScaleTime)
                    {
                        nextFontScaleTime = now + 2f;
                    }
                }
            }

            if (IsReady && ringTarget != null)
            {
                // The focused control can move (a scroll, a layout change); the ring follows it.
                PositionFocusRing();
            }

            if (IsReady)
            {
                oreVein.Tick();
            }
        }

        private void OnApplicationFocus(bool hasFocus) => NotifyApplicationFocusChanged(hasFocus);

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                NotifyApplicationFocusChanged(false);
            }
        }

        /// <summary>Tells the shell the application gained or lost focus. Losing it ends an ore vein hold (FR-042).</summary>
        public void NotifyApplicationFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
            {
                oreVein?.CancelHold(OreVeinHoldEnd.FocusLoss);
            }
        }

        // ------------------------------------------------------------------------------------------------------
        // Ore vein (T359)
        // ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Binds the mining source to the ore vein overlay. The vein shows only when the source reports manual mining (legacy
        /// content has none) and no phone sheet covers it.
        /// </summary>
        public void BindOreVein(IOreVeinSource source)
        {
            if (!IsReady)
            {
                return;
            }

            oreVein.Bind(source);
            oreVeinWanted = source != null && source.IsAvailable;
            UpdateOreVeinVisibility();
        }

        /// <summary>Removes the mining source: the vein hides and a hold ends.</summary>
        public void UnbindOreVein() => BindOreVein(null);

        private void UpdateOreVeinVisibility()
        {
            if (oreVein == null)
            {
                return;
            }

            // A phone sheet covers the world: the vein then leaves the view and the focus order, and a hold ends. The side panel of
            // tablet and desktop sits beside the world, so the vein stays.
            var show = oreVeinWanted && !(sheetOpen && !IsSidePanelLayout);
            var shown = oreVein.Root.style.display != DisplayStyle.None;
            if (show == shown)
            {
                return;
            }

            oreVein.Root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show)
            {
                oreVein.CancelHold(OreVeinHoldEnd.Hidden);
                var focused = root?.panel?.focusController?.focusedElement as VisualElement;
                if (focused != null && oreVein.Root.Contains(focused))
                {
                    oreVein.Root.Blur();
                    FocusWhenReady(TabButton(activeTab));
                }
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
            DisposeHub();
            oreVein?.Dispose();
            oreVein = null;
            oreVeinWanted = false;
            activePanel?.OnHidden();
            activePanel = null;
            if (cardOpen)
            {
                cardPanel?.OnHidden();
            }

            cardOpen = false;
            cardSlot = -1;
            detailsFromCard = false;
            cardBandPending = false;
            cardMountedRoot?.RemoveFromHierarchy();
            cardMountedRoot = null;
            cardPanel = null;
            locationStop = null;
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

            RestorePanelScale();
            noticeTimer?.Pause();
            noticeTimer = null;
            root?.RemoveFromHierarchy();
            root = null;
            built = false;
            var pendingRequest = dialogRequest;
            dialogRequest = null;
            dialogInvoker = null;
            detailHost = null;
            pinnedHost = null;
            selectedSlot = -1;
            sheetHost.Reset();
            activePanelId = null;
            activeTab = ShellTab.Factory;
            lockNoticeSlot = -1;
            noticeSticky = false;
            NoticeKind = null;

            // A panel waiting for a confirmation result is told it was cancelled, never left hanging (T192, F-7).
            if (pendingRequest != null)
            {
                try
                {
                    pendingRequest.OnCancel?.Invoke(ConfirmationCloseReason.Cancelled);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
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
