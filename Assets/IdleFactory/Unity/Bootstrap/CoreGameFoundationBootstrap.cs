using System;
using System.Collections.Generic;
using System.IO;
using IdleFactory.Core.Application;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Input;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.Bootstrap
{
    /// <summary>
    /// Single composition root and state owner of the scene. Since Feature 003 it composes the Iron factory run: v2 content
    /// through the dispatcher, the Save V2 store, the cold-start source selection (V2, V1 import, fresh) and a
    /// <see cref="RunSession"/>. It is the only runtime: T119 retired the Feature 001 placement runtime and its HUD panels
    /// (R16). The version 1 development catalog is read only by the read-only Save V1 import; the Feature 001 V1 writer is
    /// never composed (GD-1).
    /// </summary>
    public sealed class CoreGameFoundationBootstrap : MonoBehaviour
    {
        [Header("Authored content")]
        [SerializeField] private TextAsset gameplayContent;
        [Tooltip("The version 1 development catalog. It is read only when a Save V1 checkpoint must be imported.")]
        [SerializeField] private TextAsset legacyImportContent;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private VisualTreeAsset visualTree;
        [SerializeField] private StyleSheet styleSheet;
        [SerializeField] private PresentationSpriteCatalog spriteCatalog = new PresentationSpriteCatalog();

        [Header("Run shell (Feature 003)")]
        [SerializeField] private VisualTreeAsset runShellTree;
        [SerializeField] private StyleSheet runShellStyles;
        [Tooltip("Replaceable zoom limits in pixels per cell (Art Bible section 8.1). Feature 004 changes these values only.")]
        [SerializeField] private RunZoomSettings zoomSettings = new RunZoomSettings();

        [Header("Run panels (Feature 003, T194)")]
        [Tooltip("UXML and USS of the panels T113 to T118 mounted into the run shell. A missing asset leaves that panel's ID on the shell placeholder with a coded diagnostic.")]
        [SerializeField] private VisualTreeAsset buildSheetTree;
        [SerializeField] private StyleSheet buildSheetStyles;
        [SerializeField] private VisualTreeAsset machinePanelTree;
        [SerializeField] private StyleSheet machinePanelStyles;
        [Tooltip("T534: the docked machine card (UXML, USS) and the shared PanelScaffold USS it reuses. A missing asset leaves a machine tap on the Details view with a coded diagnostic.")]
        [SerializeField] private VisualTreeAsset machineCardTree;
        [SerializeField] private StyleSheet machineCardStyles;
        [SerializeField] private StyleSheet panelScaffoldStyles;
        [SerializeField] private VisualTreeAsset inventoryRatesTree;
        [SerializeField] private StyleSheet inventoryRatesStyles;
        [SerializeField] private VisualTreeAsset prioritiesTree;
        [SerializeField] private StyleSheet prioritiesStyles;
        [SerializeField] private VisualTreeAsset expansionTree;
        [SerializeField] private StyleSheet expansionStyles;
        [SerializeField] private StyleSheet contractsStyles;
        [SerializeField] private StyleSheet itemsStyles;
        [SerializeField] private StyleSheet industryStyles;
        [SerializeField] private StyleSheet researchStyles;
        [SerializeField] private VisualTreeAsset prestigeTree;
        [SerializeField] private StyleSheet prestigeStyles;
        [SerializeField] private StyleSheet offlineSummaryStyles;
        [SerializeField] private StyleSheet startupStatusStyles;
        [SerializeField] private VisualTreeAsset settingsTree;
        [SerializeField] private StyleSheet settingsStyles;

        [Header("Scene references")]
        [SerializeField] private Camera factoryCamera;
        [SerializeField] private FactoryCameraController cameraController;
        [SerializeField] private NavigationInputAdapter navigationInput;
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private FactoryHudDocument hud;
        [SerializeField] private FactoryRuntimeHost runtimeHost;

        private Rect observedSafeArea;
        private int observedWidth;
        private int observedHeight;
        private ScreenOrientation observedOrientation;
        private bool initialized;
        private ScaleLayout observedLayout;
        private bool hasObservedLayout;
        private Rect lastClosedRect;
        private string cameraLayoutId;

        // Feature 003 run composition. The run is null until the cold-start source selection created it.
        private RunCatalog runCatalog;
        private RunSession runSession;
        private SessionLogRuntime sessionLog; // T338: the session-log sink, sampler and driver; a failure never gates start-up
        private bool sessionLogAttached;
        private RunCommands runCommands;
        private RunPersistence runPersistence;
        private RunLifecycleAdapter runLifecycle;
        private bool runLifecycleSubscribed;
        private readonly List<TransientActionRegistration> transientActions = new List<TransientActionRegistration>();
        private RunShellDocument runShell;
        private string runShellError = string.Empty;
        private RunShellPresenter shellPresenter;
        private SlotGridView slotGrid;
        private RunCameraFraming framing;
        private IDisposable dialogRotationRegistration;
        private bool runFramingPending;
        private int selectedSlot = -1;
        private bool selectionChanged;
        private bool shellAvailabilityKnown;
        private bool shellAvailable;

        // T194 panel composition. The handles belong to the bootstrap, not to the panels: a recomposition or OnDestroy disposes them.
        private readonly List<IDisposable> panelHandles = new List<IDisposable>();
        private readonly List<string> panelDiagnostics = new List<string>();
        private OfflineSummaryPanel offlinePanel;
        private StartupStatusPanel startupPanel;
        private IDisposable startupRotationRegistration;
        private IVisualElementScheduledItem startupPoll;
        private ILegacyCheckpointSource legacySource;

        /// <summary>The one-line notice that a rotation cancelled an open transient action (UX-9).</summary>
        public const string RotationCancelledNotice = "Cancelled by rotation";

#if UNITY_EDITOR
        private static CompositionTestOverrides testOverrides;

        /// <summary>
        /// Scopes a persistence root and deterministic clocks around an Editor scene test of the Feature 003 run. The
        /// bootstrap then builds the real Save V2 store and the read-only Save V1 source under <paramref name="persistentDataRoot"/>
        /// (or the store of <paramref name="createRunStore"/>) instead of <c>Application.persistentDataPath</c>.
        /// <paramref name="readContentBytes"/> replaces the serialized gameplay content when it is not null.
        /// </summary>
        public static IDisposable OverrideRunCompositionForTests(
            string persistentDataRoot,
            Func<DateTimeOffset> readUtcNow,
            Func<double> readCumulativeActiveSeconds,
            Func<byte[]> readContentBytes = null,
            Func<RunCatalog, IRunCheckpointStore> createRunStore = null,
            Func<byte[]> readLegacyImportBytes = null)
        {
            if (string.IsNullOrWhiteSpace(persistentDataRoot)) throw new ArgumentException("A persistence root is required.", nameof(persistentDataRoot));
            if (readUtcNow == null) throw new ArgumentNullException(nameof(readUtcNow));
            if (readCumulativeActiveSeconds == null) throw new ArgumentNullException(nameof(readCumulativeActiveSeconds));
            if (testOverrides != null)
            {
                throw new InvalidOperationException("A composition test override is already active.");
            }

            var overrides = new CompositionTestOverrides(
                persistentDataRoot,
                createRunStore,
                readUtcNow,
                readCumulativeActiveSeconds,
                readContentBytes,
                readLegacyImportBytes);
            testOverrides = overrides;
            return new CompositionTestOverrideScope(overrides);
        }
#endif

        public FactoryRuntimeHost RuntimeHost => runtimeHost;

        /// <summary>The start-up status HUD: it reports a start-up failure and is hidden while the run shell is live.</summary>
        public FactoryHudDocument Hud => hud;
        public NavigationInputAdapter NavigationInput => navigationInput;
        public Camera FactoryCamera => factoryCamera;
        public UIDocument UiDocument => uiDocument;
        public InputActionAsset InputActions => inputActions;
        public PresentationSpriteCatalog SpriteCatalog => spriteCatalog;
        /// <summary>The Feature 003 run session, or null when start-up failed before one was created.</summary>
        public RunSession RunSession => runSession;
        public RunCommands RunCommands => runCommands;
        public RunPersistence RunPersistence => runPersistence;
        public RunCatalog RunCatalog => runCatalog;
        public RunLifecycleAdapter RunLifecycle => runLifecycle;

        /// <summary>
        /// The session log's controls (T340: toggle, clear, export), or null when the log could not be created. The writer's own serialization
        /// stays the only path to its files; the export path is for the screen only and is never logged.
        /// </summary>
        public ISessionLogControls SessionLog => sessionLog;

        /// <summary>The responsive run shell (T112), or null before a run is live. Panels mount through <see cref="RunShellDocument.Panels"/>.</summary>
        public RunShellDocument RunShell => runShell;

        /// <summary>
        /// The coded diagnostics ("panel.&lt;id&gt;.failed: ...") of panels that could not be built or mounted by the latest composition.
        /// Static text and the exception type only, never an exception message or a path. A panel listed here keeps the shell placeholder.
        /// </summary>
        public IReadOnlyList<string> PanelDiagnostics => panelDiagnostics;

        /// <summary>The offline-return surface (T118) mounted under the shell, or null before a run is live.</summary>
        public OfflineSummaryPanel OfflinePanel => offlinePanel;

        /// <summary>The start-up and import status surface (T118), or null before the run persistence exists.</summary>
        public StartupStatusPanel StartupPanel => startupPanel;

        /// <summary>The 12-slot world grid (T112), or null before a run is live.</summary>
        public SlotGridView SlotGrid => slotGrid;

        /// <summary>The presenter that binds the run session to the shell and the grid, or null before a run is live.</summary>
        public RunShellPresenter ShellPresenter => shellPresenter;

        /// <summary>The replaceable zoom limits in pixels per cell.</summary>
        public RunZoomSettings ZoomSettings => zoomSettings;

        /// <summary>The projected size of one cell in panel points at the current zoom (Art Bible section 8.1).</summary>
        public float CurrentPixelsPerCell
        {
            get
            {
                return RunCameraFraming.PixelsPerCell(factoryCamera, PixelsPerPoint());
            }
        }

        /// <summary>The offline summary of the latest genuine return (never of a rotation, FR-036), or null.</summary>
        public RunOfflineSummary LastOfflineSummary
        {
            get => lastOfflineSummary;
            private set
            {
                // T338 (W5, #392): every genuine return reaches the session log once, from its summary.
                bool isNew = value != null && !ReferenceEquals(value, lastOfflineSummary);
                lastOfflineSummary = value;
                if (isNew) SessionLogRaiser.RaiseOfflineReturn(runSession, value, runLifecycle != null && runLifecycle.IsLiveGapReturn);
            }
        }

        private RunOfflineSummary lastOfflineSummary;

        /// <summary>The stable code of the failure that disabled start-up, or empty. Static text only, never an exception message or a path.</summary>
        public string StartupDiagnosticCode { get; private set; } = string.Empty;

        /// <summary>True once the Save V2 store was created; false when start-up failed before checkpoint I/O.</summary>
        public bool CheckpointStoreCreated { get; private set; }

        public string InitializationError { get; private set; } = string.Empty;
        public bool IsInitialized => initialized;

        /// <summary>Average frame-rate target of the players (owner decision 2026-09-28). Production does not depend on it (FR-017).</summary>
        public const int TargetFrameRate = 60;

        /// <summary>
        /// Applies the same explicit 60 FPS cap on Android and Windows players. Unity caps Android at 30 FPS by default,
        /// and VSync takes precedence over targetFrameRate on desktop, so players disable VSync. The Editor keeps its
        /// quality level unchanged because a Play Mode change to QualitySettings would leak into ProjectSettings.
        /// </summary>
        public static void ApplyFrameRateTarget()
        {
#if !UNITY_EDITOR
            QualitySettings.vSyncCount = 0;
#endif
            Application.targetFrameRate = TargetFrameRate;
        }

        private void Awake()
        {
            ApplyFrameRateTarget();
            Initialize();
        }

        /// <summary>
        /// Start-up order (T110): non-content reference validation, HUD readiness and layout, the content bytes, the
        /// presentation catalog, the version 2 content load through the dispatcher with presentation-key resolution
        /// (<c>presentation.key.unresolved</c>), then the Save V2 store, the cold-start source selection (V2, V1 import,
        /// fresh; the legacy catalog is read only when a V1 import is needed) and finally the run session. Every failure
        /// before the store is created is reported through <see cref="DisableWithError"/> with a coded diagnostic and no
        /// checkpoint I/O. Diagnostics never contain an exception message or a path.
        /// </summary>
        public bool Initialize()
        {
            if (initialized)
            {
                return true;
            }

            if (!ValidateReferences(out var error))
            {
                return DisableWithError(error);
            }

            if (!hud.Configure(uiDocument, visualTree, styleSheet))
            {
                return DisableWithError(hud.Error);
            }

            ApplySafeAreaLayout(true);

            byte[] contentBytes;
            try
            {
                contentBytes = ReadContentBytes();
            }
            catch (Exception exception)
            {
                // Only the exception type is reported: messages of I/O exceptions can carry file-system paths.
                return DisableWithCode(
                    "content.unavailable",
                    $"Packaged gameplay content could not be read ({exception.GetType().Name}).");
            }

            if (!spriteCatalog.Validate())
            {
                return DisableWithCode("presentation.catalog.invalid", spriteCatalog.Error);
            }

            GameplayContentLoadResult loaded;
            try
            {
                loaded = GameplayContentDispatcher.Load(contentBytes, new SpriteCatalogKeyResolver(spriteCatalog));
            }
            catch (Exception exception)
            {
                // Only the exception type is reported: messages can echo content values or paths.
                return DisableWithCode(
                    "content.internal",
                    $"Gameplay content could not be processed ({exception.GetType().Name}).");
            }

            if (loaded.SchemaVersion == 1)
            {
                // A version 1 document only serves the Save V1 import; it is never accepted as the gameplay content.
                return DisableWithCode(
                    "content.version.unsupported",
                    "The gameplay content must be a version 2 document.");
            }

            if (!loaded.IsSuccess)
            {
                var diagnostic = loaded.Diagnostics[0];
                return DisableWithCode(diagnostic.Code, diagnostic.Message);
            }

            return InitializeRunRuntime(loaded.RunCatalog);
        }

        private byte[] ReadContentBytes()
        {
#if UNITY_EDITOR
            if (testOverrides != null && testOverrides.ReadContentBytes != null)
            {
                return testOverrides.ReadContentBytes();
            }
#endif
            return gameplayContent == null ? null : gameplayContent.bytes;
        }

        private bool InitializeRunRuntime(RunCatalog loadedCatalog)
        {
            IRunCheckpointStore store;
            try
            {
                runCatalog = loadedCatalog;
                store = CreateRunCheckpointStore(loadedCatalog);
                CheckpointStoreCreated = true;
            }
            catch (Exception exception)
            {
                return DisableWithCode(
                    "persistence.failed",
                    $"Checkpoint storage is unavailable ({exception.GetType().Name}).");
            }

            try
            {
                var clock = new DevelopmentUtcClock();
                var startMs = clock.GetUtcNow().ToUnixTimeMilliseconds();
                var permanent = new PermanentState(runCatalog);
                var start = NextRunStart.Compute(runCatalog, permanent);
                var state = new RunState(
                    runCatalog,
                    Guid.NewGuid(),
                    1,
                    null,
                    start.StartingCash,
                    0,
                    start.StorageLevel,
                    1,
                    runCatalog.RunStart.InitialUnlocks);
                if (new RunSimulator(runCatalog).Advance(state, permanent, 0, startMs, null) != RunAdvanceResult.Ok)
                {
                    return DisableWithCode("run.start.invalid", "The run could not be started from the content.");
                }

                sessionLog = TryCreateSessionLog();
                runSession = new RunSession(runCatalog, state, permanent, startMs, sessionLog?.Sink);
                runCommands = new RunCommands(runSession);
                runPersistence = new RunPersistence(runSession, store, clock);
                legacySource = new LazyLegacyCheckpointSource(CurrentPersistenceRoot(), ReadLegacyImportBytes);

                // T118: the start-up status surface covers the screen from the cold start until the run is live.
                ComposeStartupPanel();
                startupPanel?.ShowImporting();
                var coldStart = runtimeHost.MeasureResumeAndReconcile(() => runPersistence.ColdStart(legacySource));
                startupPanel?.Refresh();
                if (!coldStart.IsSuccess)
                {
                    return DisableWithCode(
                        runPersistence.BlockedImportCode ?? coldStart.ReasonCode,
                        StartupBlockedMessage(runPersistence.BlockedImportCode ?? coldStart.ReasonCode));
                }

                return CompleteRunStartup(coldStart.Value);
            }
            catch (Exception exception)
            {
                return DisableWithCode(
                    "bootstrap.failed",
                    $"Run bootstrap failed ({exception.GetType().Name}).");
            }
        }

        /// <summary>
        /// Finishes start-up once the persistence lifecycle is <see cref="LifecycleState.Running"/>: after the cold start, or
        /// after a recovery action (Start new run, discard of a damaged save) succeeded on a blocked start. The recovery UI
        /// of T118 calls this after its command. Returns whether the run is now live.
        /// </summary>
        public bool ResumeStartupAfterRecovery()
        {
            if (initialized || runPersistence == null || runPersistence.Lifecycle != LifecycleState.Running)
            {
                return initialized;
            }

            try
            {
                return CompleteRunStartup(null);
            }
            catch (Exception exception)
            {
                return DisableWithCode("bootstrap.failed", $"Run bootstrap failed ({exception.GetType().Name}).");
            }
        }

        private bool CompleteRunStartup(RunPersistenceOutcome coldStartOutcome)
        {
            if (runPersistence.Lifecycle != LifecycleState.Running)
            {
                return DisableWithCode("return.blocked", StartupBlockedMessage("return.blocked"));
            }

            // A recovery can re-enter here after DisableWithError turned the scene behaviours off.
            enabled = true;
            if (cameraController != null) cameraController.enabled = true;
            if (navigationInput != null) navigationInput.enabled = true;

            runtimeHost.ConfigureRun(runSession, ReadCumulativeActiveSeconds);
            if (sessionLog != null && !sessionLogAttached)
            {
                sessionLogAttached = true;
                sessionLog.Attach(runSession, transform);
            }

            // The scene serializes the host disabled. The run host gates itself on whether any machine can produce or any
            // timer is pending, so it stays enabled.
            runtimeHost.enabled = true;
            if (!ComposeRunShell())
            {
                return DisableWithCode("shell.unavailable", !string.IsNullOrEmpty(runShellError) ? runShellError : "The run shell could not be composed.");
            }

            navigationInput.Configure(inputActions, IsOverRuntimeUi);
            navigationInput.IntentRaised -= RouteNavigationIntent;
            navigationInput.IntentRaised += RouteNavigationIntent;

            if (runLifecycle == null)
            {
                runLifecycle = gameObject.AddComponent<RunLifecycleAdapter>();
                runLifecycle.Configure(
                    runPersistence,
                    PrepareForDeparture,
                    reconcile => runtimeHost.MeasureResumeAndReconcile(reconcile),
                    () => runtimeHost?.FlushActiveInterval());
            }

            // T337 (FR-032, F5-R10): the host reads UTC explicitly and sends a gap above the shared threshold to the lifecycle adapter, which
            // records the departure at the previous frame and reconciles like a quit and restart.
            runtimeHost.ConfigureLiveGap(new DevelopmentUtcClock(), runPersistence.LiveGapThresholdMs, runLifecycle.ReconcileLiveGap);

            if (!runLifecycleSubscribed)
            {
                runLifecycle.ResultChanged += HandleRunLifecycleResultChanged;
                runLifecycleSubscribed = true;
            }

            if (coldStartOutcome != null && coldStartOutcome.OfflineSummary != null)
            {
                LastOfflineSummary = coldStartOutcome.OfflineSummary;
                ShowOfflineSummary(coldStartOutcome.OfflineSummary);
            }

            observedOrientation = Screen.orientation;
            initialized = true;
            InitializationError = string.Empty;
            StartupDiagnosticCode = string.Empty;
            ApplySafeAreaLayout(true);
            hud.SetState(FactoryHudState.Normal, "Iron factory run is ready.");
            hud.SetDisplayed(false);
            ConfigureRunCamera(true);
            shellPresenter.Invalidate();
            shellPresenter.Refresh();

            // The run is live: the start-up surface has done its work and goes with its timers and rotation registration.
            DisposeStartupPanel();
            return true;
        }

        /// <summary>
        /// Creates the run shell, the slot grid and their presenter once (a recovery re-enters start-up), then shows the shell
        /// instead of the start-up status HUD. The shell and grid are created at runtime from the serialized UXML, USS and sprite
        /// catalog, like the lifecycle adapter, so the scene stays unchanged apart from those references.
        /// </summary>
        private bool ComposeRunShell()
        {
            if (runShellTree == null || runShellStyles == null)
            {
                return false;
            }

            if (runShell == null)
            {
                // Compose into locals and publish only a complete shell: a failed Configure leaves nothing behind, so a start-up
                // retry composes again instead of finding a half-built shell (T192, F-7).
                var shell = gameObject.AddComponent<RunShellDocument>();
                if (!shell.Configure(uiDocument, runShellTree, runShellStyles, ResolveIcon))
                {
                    runShellError = shell.Error;
                    Destroy(shell);
                    return false;
                }

                var gridObject = new GameObject("Slot Grid");
                gridObject.transform.SetParent(transform, false);
                var grid = gridObject.AddComponent<SlotGridView>();
                grid.Configure(spriteCatalog);
                runShell = shell;
                slotGrid = grid;
                RefreshReducedMotion();
                runShellError = string.Empty;
                runShell.ZoomRequested += cameraController.ZoomFromVisibleControl;
                runShell.SlotSelectionChanged += HandleShellSlotSelection;
                dialogRotationRegistration = RegisterTransientAction(runShell.CancelConfirmationForRotation);
                runShell.LocationRectProvider = LocationScreenRect;
                shellPresenter = new RunShellPresenter(runSession, runCommands, runShell, slotGrid, PresentationKeyOf);
                ComposeRunPanels();
            }

            runShell.SetDisplayed(true);
            runShell.SetGameplayAvailable(runPersistence.Lifecycle == LifecycleState.Running);
            shellAvailabilityKnown = true;
            shellAvailable = runPersistence.Lifecycle == LifecycleState.Running;
            ApplySafeAreaLayout(true);
            return true;
        }

        // One shared response policy for the card and the Details view; its sink is late-bound (T529 wires the pool after the panels exist).
        private MachineFeedback machineFeedback;

        /// <summary>T529 hands the world effect pool here; until then the card answers with the static highlight (FR-034).</summary>
        public void UseMachineEffects(IMachineEffects effects) => MachineResponses.Effects = effects;

        /// <summary>The shared machine response policy (world effect through the sink, static highlight otherwise).</summary>
        public MachineFeedback MachineResponses => machineFeedback ?? (machineFeedback = new MachineFeedback(() => CurrentReducedMotion.Reduced));

        // The location stop's ring surrounds the machine on screen: the world rectangle of the location through the camera, in panel points.
        private Rect? LocationScreenRect(int slot)
        {
            if (slotGrid == null || factoryCamera == null || cameraController == null || !cameraController.HasLayout)
            {
                return null;
            }

            var layout = slotGrid.Layout;
            if (slot < 0 || slot >= layout.LocationCount)
            {
                return null;
            }

            var world = layout.MachineRectOf(new BuildingLocationId(slot));
            var low = factoryCamera.WorldToScreenPoint(new Vector3(world.xMin, world.yMin, 0f));
            var high = factoryCamera.WorldToScreenPoint(new Vector3(world.xMax, world.yMax, 0f));
            var perPoint = PixelsPerPoint();
            var panelHeight = factoryCamera.pixelHeight / perPoint;
            return Rect.MinMaxRect(low.x / perPoint, panelHeight - high.y / perPoint, high.x / perPoint, panelHeight - low.y / perPoint);
        }

        private Sprite ResolveIcon(string key) => spriteCatalog.TryGet(key, out var sprite) ? sprite : null;

        // ------------------------------------------------------------------------------------------------------
        // Panel composition (T194)
        // ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Disposes every panel registration and builds, registers and mounts the panels of T113 to T118 again. Call it after the
        /// shell was rebuilt (<see cref="RunShellDocument.Build"/>): each panel is registered exactly once, a panel that fails keeps
        /// the shell placeholder and a coded entry in <see cref="PanelDiagnostics"/>. Returns false when no run shell is live.
        /// </summary>
        public bool RecomposeRunPanels()
        {
            if (runShell == null || !runShell.IsReady || runSession == null)
            {
                return false;
            }

            ComposeRunPanels();
            shellPresenter?.Invalidate();
            runShell.RefreshActivePanel();
            return true;
        }

        private void ComposeRunPanels()
        {
            DisposePanelHandles();
            panelDiagnostics.Clear();
            Func<string, Sprite> icons = ResolveIcon;

            TryComposePanel(ShellPanelIds.Build,
                () => new BuildSheetPresenter(runSession, runCommands, buildSheetTree, buildSheetStyles, icons));
            // T534: the card and the Details view answer a successful upgrade or switch-on through one policy (a world effect through the sink once
            // T529 wires the pool, otherwise and always under reduced motion the static one-second highlight, FR-034).
            var machineFeedback = MachineResponses;
            TryComposePanel(ShellPanelIds.Machine,
                () => new MachinePanelPresenter(runSession, runCommands, machinePanelTree, machinePanelStyles, icons, machineFeedback));
            TryComposePanel(ShellPanelIds.MachineCard,
                () => new MachineCard(runSession, runCommands, machineCardTree, machineCardStyles, panelScaffoldStyles, icons, machineFeedback,
                    () => slotGrid != null ? slotGrid.Layout : null));
            TryComposePanel(ShellPanelIds.Market,
                () => new MarketPanelHost(runSession, runCommands, runShell, inventoryRatesTree, inventoryRatesStyles,
                    prioritiesTree, prioritiesStyles, expansionTree, expansionStyles, icons, sessionLog),
                panel => ((MarketPanelHost)panel).CancelForRotation);
            TryComposePanel(ShellPanelIds.Contracts,
                () => new ContractsPanel(runSession, runCommands, contractsStyles, icons),
                panel => ((ContractsPanel)panel).CancelForRotation);
            TryComposePanel(ShellPanelIds.Items,
                () => new ItemsPanel(runSession, runCommands, itemsStyles, icons),
                panel => ((ItemsPanel)panel).CancelForRotation);
            TryComposePanel(ShellPanelIds.Research,
                () => new ResearchPanel(runSession, runCommands, researchStyles, icons,
                    new IndustryPanel(runSession, runCommands, industryStyles, icons)),
                panel => ((ResearchPanel)panel).CancelForRotation);
            TryComposePanel(ShellPanelIds.Settings,
                () => new SettingsPanelPresenter(DisplayPreferences, RefreshReducedMotion, () => CurrentReducedMotion,
                    () => SystemReducedMotionAvailable, settingsTree, settingsStyles, icons));
            TryComposePanel(ShellPanelIds.Prestige,
                () => new PrestigePresenter(runSession, runCommands, prestigeTree, prestigeStyles, icons),
                panel => ((PrestigePresenter)panel).CancelForRotation);
            ComposeOfflinePanel();
        }

        /// <summary>
        /// Builds one panel and registers it. A panel is registered only when it was built completely; a failure is reported with a
        /// coded diagnostic that names the panel and the exception type, never the message or a path (T192, SEC-5), and the ID keeps
        /// the shell's "not available" placeholder.
        /// </summary>
        private void TryComposePanel(string id, Func<IShellPanel> create, Func<IShellPanel, Func<bool>> cancelForRotation = null)
        {
            try
            {
                var panel = create();
                var registration = runShell.Panels.Register(panel);
                panelHandles.Add(registration);
                if (cancelForRotation != null)
                {
                    panelHandles.Add(RegisterTransientAction(cancelForRotation(panel)));
                }
            }
            catch (Exception exception)
            {
                RecordPanelFailure(id, exception);
            }
        }

        private void RecordPanelFailure(string id, Exception exception)
        {
            var diagnostic = $"panel.{id}.failed: The {id} panel could not be built ({exception.GetType().Name}).";
            panelDiagnostics.Add(diagnostic);
            Debug.LogWarning(diagnostic, this);
        }

        private void DisposePanelHandles()
        {
            // Reverse order: the rotation registrations go before the panel registrations they belong to.
            for (var i = panelHandles.Count - 1; i >= 0; i--)
            {
                panelHandles[i].Dispose();
            }

            panelHandles.Clear();
        }

        /// <summary>
        /// Builds the offline-return surface once. It is mounted under the shell (below the shell's confirmation dialog) only while it
        /// shows something, so a quiet run carries no timer of the surface. A recomposition after a shell rebuild mounts it again when
        /// it was mounted.
        /// </summary>
        private void ComposeOfflinePanel()
        {
            try
            {
                if (offlinePanel == null)
                {
                    offlinePanel = new OfflineSummaryPanel(runCatalog, runPersistence.RetryReturn, offlineSummaryStyles, ResolveIcon);
                    offlinePanel.RetryCompleted += HandleOfflineRetryCompleted;
                    offlinePanel.Dismissed += HandleOfflineDismissed;
                }
                else if (offlinePanel.Root.parent != null)
                {
                    MountOfflinePanel(true);
                }
            }
            catch (Exception exception)
            {
                offlinePanel = null;
                RecordPanelFailure("offline", exception);
            }
        }

        private void MountOfflinePanel(bool force = false)
        {
            if (force || offlinePanel.Root.parent != runShell.Root)
            {
                offlinePanel.Mount(runShell.Root, runShell);
            }
        }

        /// <summary>Opens the offline surface for a lifecycle result (the surface itself ignores departures and rotations).</summary>
        private void PresentOfflineReturn(RunCommandResult<RunPersistenceOutcome> result, LifecycleReturnKind kind)
        {
            if (offlinePanel == null || runShell == null || kind == LifecycleReturnKind.None || kind == LifecycleReturnKind.ConfigurationChange)
            {
                return;
            }

            MountOfflinePanel();
            if (!offlinePanel.Present(result, kind) && !offlinePanel.IsOpen)
            {
                offlinePanel.Unmount();
            }
        }

        private void ShowOfflineSummary(RunOfflineSummary summary)
        {
            if (offlinePanel == null || runShell == null || summary == null)
            {
                return;
            }

            MountOfflinePanel();
            offlinePanel.ShowSummary(summary);
        }

        private void HandleOfflineDismissed()
        {
            if (offlinePanel != null && !offlinePanel.IsOpen)
            {
                offlinePanel.Unmount();
            }
        }

        private void HandleOfflineRetryCompleted(RunCommandResult<RunPersistenceOutcome> result)
        {
            if (result.IsSuccess && runPersistence != null && runPersistence.Lifecycle == LifecycleState.Running)
            {
                // The application commands cannot reach the host: resume it the way a lifecycle return does.
                runtimeHost?.SynchronizeAfterSuccessfulLifecycleReturn();
                if (runtimeHost != null)
                {
                    runtimeHost.enabled = true;
                }

                if (result.Value != null && result.Value.OfflineSummary != null)
                {
                    LastOfflineSummary = result.Value.OfflineSummary;
                }

                shellPresenter?.Invalidate();
            }
        }

        /// <summary>
        /// Builds the start-up and import status surface on the UIDocument root, where it exists before the shell does (a blocked
        /// start never composes the shell). A second start-up after a failed one replaces the surface of the first.
        /// </summary>
        private void ComposeStartupPanel()
        {
            DisposeStartupPanel();
            try
            {
                var host = uiDocument.rootVisualElement;
                var panel = new StartupStatusPanel(runPersistence, legacySource, startupStatusStyles, ResolveIcon, () => CurrentReducedMotion.Reduced);
                panel.Mount(host);
                startupPanel = panel;
                startupPanel.Completed += HandleStartupCompleted;
                startupRotationRegistration = RegisterTransientAction(startupPanel.CancelForRotation);
                ApplyStartupSafeInsets();

                // A blocked start disables this behaviour, so Update does not run: poll the orientation and the safe area while the
                // surface is the only UI (a rotation cancels an open confirmation, UX-9).
                startupPoll = panel.Root.schedule.Execute(PollStartupSurface).Every(250);
            }
            catch (Exception exception)
            {
                startupPanel = null;
                RecordPanelFailure("startup", exception);
            }
        }

        private void DisposeStartupPanel()
        {
            startupPoll?.Pause();
            startupPoll = null;
            startupRotationRegistration?.Dispose();
            startupRotationRegistration = null;
            if (startupPanel != null)
            {
                startupPanel.Completed -= HandleStartupCompleted;
                startupPanel.Unmount();
                startupPanel = null;
            }
        }

        private void PollStartupSurface()
        {
            if (initialized || startupPanel == null)
            {
                startupPoll?.Pause();
                return;
            }

            NotifyOrientationChanged(Screen.orientation);
            ApplyStartupSafeInsets();
        }

        private void HandleStartupCompleted(RunCommandResult<RunPersistenceOutcome> result)
        {
            if (!ResumeStartupAfterRecovery())
            {
                return;
            }

            if (result.Value != null && result.Value.OfflineSummary != null)
            {
                LastOfflineSummary = result.Value.OfflineSummary;
                ShowOfflineSummary(result.Value.OfflineSummary);
            }

            if (result.Value != null && result.Value.SelectedDurableCheckpoint)
            {
                // The player's choice was replaced by a durable checkpoint: say so once the shell exists.
                runShell?.ShowNotice(StartupStatusPanel.SelectedDurableText);
            }
        }

        /// <summary>Gives the start-up surface the safe-area insets in panel points; under the shell it needs none.</summary>
        private void ApplyStartupSafeInsets()
        {
            var panel = uiDocument?.rootVisualElement?.panel;
            if (startupPanel == null || panel == null)
            {
                return;
            }

            var scale = panel.scaledPixelsPerPoint;
            if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale))
            {
                scale = 1f;
            }

            var safe = Screen.safeArea;
            startupPanel.ApplySafeInsets(
                safe.x / scale,
                (Screen.height - (safe.y + safe.height)) / scale,
                (Screen.width - (safe.x + safe.width)) / scale,
                safe.y / scale);
        }

        private string PresentationKeyOf(string machineId) =>
            runCatalog.Machines.TryGetValue(machineId, out var machine) ? machine.PresentationKey : string.Empty;

        private float PixelsPerPoint()
        {
            var scale = runShell != null && runShell.Root?.panel != null ? runShell.Root.panel.scaledPixelsPerPoint : 1f;
            return scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale) ? 1f : scale;
        }

        /// <summary>
        /// Sets the world layout by screen aspect and binds the camera controller to the Art Bible scale inputs
        /// (<see cref="RunZoomSettings"/>, never literals in camera or layout code). A new layout, new settings or
        /// <paramref name="fitAll"/> (rotation) defer the fit-all until the shell layout has resolved.
        /// </summary>
        private void ConfigureRunCamera(bool fitAll)
        {
            framing = framing ?? new RunCameraFraming(zoomSettings);
            var worldLayout = LatticeLayoutBuilder.Shipped(Screen.width, Screen.height);
            slotGrid.SetLayout(worldLayout);
            if (runShell != null)
            {
                runShell.WorldLayout = worldLayout;
            }

            var settings = framing.ScaleSettings;
            var model = cameraController.Model;
            if (model == null || model.Settings.ArtPixelsPerCell != settings.ArtPixelsPerCell ||
                model.Settings.ReadableCellPoints != settings.ReadableCellPoints || model.Settings.MaxScale != settings.MaxScale)
            {
                cameraController.Configure(factoryCamera, settings);
                hasObservedLayout = false;
                runFramingPending = true;
            }

            if (worldLayout.LayoutId != cameraLayoutId)
            {
                cameraLayoutId = worldLayout.LayoutId;
                runFramingPending = true;
            }

            if (fitAll)
            {
                runFramingPending = true;
            }
        }

        private void HandleShellSlotSelection(int slot)
        {
            // The sheet that a selection opens leaves a world band after the layout resolves; the viewport step keeps the selected
            // slot in that band on every layout change while the selection lasts (rule 7).
            selectedSlot = slot;
            selectionChanged = true;
        }

        private static string StartupBlockedMessage(string code)
        {
            switch (code)
            {
                case V1ImportCodes.Invalid: return "The saved development factory could not be read.";
                case V1ImportCodes.Clock: return "The device clock is unavailable, so the saved development factory cannot be imported yet.";
                case V1ImportCodes.IdUnsupported: return "The saved development factory contains content that cannot be imported.";
                case V1ImportCodes.Arithmetic: return "The saved development factory is too large to import.";
                case V1ImportCodes.SlotsExceeded: return "The saved development factory has more machines than the first factory level holds.";
                case V1ImportCodes.CapacityExceeded: return "The saved development factory holds more stock than the first store level holds.";
                case V1ImportCodes.Persistence: return "The imported factory could not be saved.";
                case RunPersistenceReasons.ReturnBlocked: return "The saved factory could not be loaded. Retry or start a new run.";
                case RunPersistenceReasons.InvalidCheckpoint: return "The saved factory is damaged.";
                case RunPersistenceReasons.UnsupportedVersion: return "The saved factory was written by another version of the game.";
                case RunPersistenceReasons.PersistenceFailed: return "Checkpoint storage is unavailable.";
                default: return "The factory could not be started.";
            }
        }

        private bool DisableWithCode(string code, string message)
        {
            StartupDiagnosticCode = string.IsNullOrWhiteSpace(code) ? "bootstrap.failed" : code;
            return DisableWithError($"{StartupDiagnosticCode}: {message}");
        }

        private SessionLogRuntime TryCreateSessionLog()
        {
            try
            {
                return SessionLogRuntime.Create(CurrentPersistenceRoot(), runCatalog, () => runtimeHost != null ? runtimeHost.TimeScaleSource : null);
            }
            catch (Exception)
            {
                return null; // a log failure never blocks start-up (FR-030)
            }
        }

        private string CurrentPersistenceRoot()
        {
#if UNITY_EDITOR
            if (testOverrides != null && testOverrides.PersistentDataRoot != null)
            {
                return testOverrides.PersistentDataRoot;
            }
#endif
            return PersistenceRoots.Resolve(Application.persistentDataPath);
        }

        private byte[] ReadLegacyImportBytes()
        {
#if UNITY_EDITOR
            if (testOverrides != null && testOverrides.ReadLegacyImportBytes != null)
            {
                return testOverrides.ReadLegacyImportBytes();
            }
#endif
            return legacyImportContent == null ? null : legacyImportContent.bytes;
        }

        private IRunCheckpointStore CreateRunCheckpointStore(RunCatalog catalogToStore)
        {
#if UNITY_EDITOR
            if (testOverrides != null && testOverrides.CreateRunStore != null)
            {
                return testOverrides.CreateRunStore(catalogToStore);
            }
#endif
            return new JsonRunCheckpointStore(CurrentPersistenceRoot(), catalogToStore);
        }

        /// <summary>
        /// Registers a transient action (an open confirmation, a drag-to-reorder or a pending selection) that a rotation
        /// cancels without spending, placing or confirming anything (UX-9). The callback cancels the action and returns
        /// whether one was open. Dispose the registration when the surface is destroyed.
        /// </summary>
        public IDisposable RegisterTransientAction(Func<bool> cancelIfOpen)
        {
            if (cancelIfOpen == null)
            {
                throw new ArgumentNullException(nameof(cancelIfOpen));
            }

            var registration = new TransientActionRegistration(this, cancelIfOpen);
            transientActions.Add(registration);
            return registration;
        }

        /// <summary>
        /// Cancels every open transient action and, when at least one was open, shows the one-line
        /// <see cref="RotationCancelledNotice"/>. Returns the number of cancelled actions. A rotation never spends, places
        /// or prestiges: the cancel callbacks only close surfaces.
        /// </summary>
        public int CancelTransientActionsForRotation()
        {
            var cancelled = 0;
            var snapshot = transientActions.ToArray();
            foreach (var registration in snapshot)
            {
                if (registration.Cancel())
                {
                    cancelled++;
                }
            }

            if (cancelled > 0 && hud != null)
            {
                hud.SetState(FactoryHudState.Normal, RotationCancelledNotice);
            }

            if (cancelled > 0)
            {
                runShell?.ShowNotice(RotationCancelledNotice);
            }

            return cancelled;
        }

        private void HandleRunLifecycleResultChanged(RunCommandResult<RunPersistenceOutcome> result, LifecycleReturnKind kind)
        {
            // T118: a genuine return opens the offline summary, a return that could not be completed opens the blocked explanation
            // with Retry. A rotation and a departure open nothing (the panel decides by kind, FR-036).
            PresentOfflineReturn(result, kind);

            if (result.IsSuccess && runPersistence != null && runPersistence.Lifecycle == LifecycleState.Running)
            {
                // The departure disabled the host; a successful return makes it available again.
                runtimeHost?.SynchronizeAfterSuccessfulLifecycleReturn();
                if (runtimeHost != null)
                {
                    runtimeHost.enabled = true;
                }

                if (kind == LifecycleReturnKind.ConfigurationChange)
                {
                    // A rotation is not a departure the player made: no summary is shown, and the notice says what happened.
                    CancelTransientActionsForRotation();
                }
                else if (kind == LifecycleReturnKind.Return && result.Value != null && result.Value.OfflineSummary != null)
                {
                    // P2-F12: the first refresh after an offline return reports no completion delta.
                    runShell?.ResetCardModels(true);
                    LastOfflineSummary = result.Value.OfflineSummary;
                }
            }
        }

        private sealed class TransientActionRegistration : IDisposable
        {
            private readonly CoreGameFoundationBootstrap owner;
            private readonly Func<bool> cancelIfOpen;

            public TransientActionRegistration(CoreGameFoundationBootstrap owner, Func<bool> cancelIfOpen)
            {
                this.owner = owner;
                this.cancelIfOpen = cancelIfOpen;
            }

            public bool Cancel() => cancelIfOpen();

            public void Dispose() => owner?.transientActions.Remove(this);
        }

        private sealed class SpriteCatalogKeyResolver : IPresentationKeyResolver
        {
            private readonly PresentationSpriteCatalog sprites;

            public SpriteCatalogKeyResolver(PresentationSpriteCatalog sprites) => this.sprites = sprites;

            public bool IsResolved(string presentationKey) => sprites.TryGet(presentationKey, out _);
        }

        /// <summary>
        /// The read-only Save V1 source that loads the legacy import catalog only when a V1 save exists (FR-028): without a
        /// <c>v1/</c> directory nothing is loaded and the V1 save counts as absent. A legacy catalog that cannot be loaded
        /// while a V1 save exists makes the import fail with <c>import.v1.invalid</c> instead of starting a fresh run.
        /// </summary>
        private sealed class LazyLegacyCheckpointSource : ILegacyCheckpointSource
        {
            private readonly string root;
            private readonly Func<byte[]> readLegacyBytes;
            private SaveV1CheckpointSource source;

            public LazyLegacyCheckpointSource(string root, Func<byte[]> readLegacyBytes)
            {
                this.root = root;
                this.readLegacyBytes = readLegacyBytes;
            }

            public string Description => SaveV1CheckpointSource.SourceDescription;

            public bool TryReadActive(out Checkpoint checkpoint, out CommandOutcome outcome)
            {
                checkpoint = null;
                if (source == null)
                {
                    try
                    {
                        if (!Directory.Exists(SaveV1CheckpointFiles.Layout.DirectoryFor(root)))
                        {
                            outcome = CommandOutcome.NoCheckpoint;
                            return false;
                        }

                        var legacy = GameplayContentLoader.Load(readLegacyBytes());
                        if (!legacy.IsSuccess)
                        {
                            outcome = CommandOutcome.InvalidCheckpoint;
                            return false;
                        }

                        source = new SaveV1CheckpointSource(root, legacy.Catalog);
                    }
                    catch (Exception)
                    {
                        outcome = CommandOutcome.PersistenceFailed;
                        return false;
                    }
                }

                return source.TryReadActive(out checkpoint, out outcome);
            }
        }

        public void ApplySafeAreaLayout(bool force = false)
        {
            if (!initialized && !force)
            {
                return;
            }

            var safeArea = Screen.safeArea;
            if (!force && observedWidth == Screen.width && observedHeight == Screen.height && observedSafeArea == safeArea)
            {
                return;
            }

            observedWidth = Screen.width;
            observedHeight = Screen.height;
            observedSafeArea = safeArea;
            hud?.ApplyScreenLayout(Screen.width, Screen.height, safeArea);
            runShell?.ApplyScreenLayout(Screen.width, Screen.height, safeArea);
            ApplyStartupSafeInsets();
            if (initialized && runShell != null && runShell.IsReady)
            {
                ConfigureRunCamera(false);
            }
        }

        private DisplayPreferencesStore displayPreferences;

        /// <summary>The display-preferences store (T432), created on first use under the persistence root. Not a save.</summary>
        public DisplayPreferencesStore DisplayPreferences
        {
            get
            {
                if (displayPreferences == null)
                {
                    displayPreferences = new DisplayPreferencesStore(CurrentPersistenceRoot());
                    // A preference change re-resolves the effective state through the store's event (T433), not by convention.
                    displayPreferences.Changed += OnDisplayPreferenceChanged;
                }

                return displayPreferences;
            }
        }

        private void OnDisplayPreferenceChanged() => RefreshReducedMotion();

        public void NotifyFocusChanged(bool hasFocus)
        {
            // Focus return only triggers a re-read of the display preferences (T432); the store owns all preference logic.
            if (hasFocus)
            {
                displayPreferences?.Reload();
                RefreshReducedMotion();
            }

            // Losing focus is a departure only on a phone or tablet (T250); a desktop player keeps the run live.
            if (!hasFocus && LifecyclePlatform.IsMobile)
            {
                PrepareForDeparture();
            }
        }

        public void NotifyOrientationChanged(ScreenOrientation orientation)
        {
            if (orientation == observedOrientation)
            {
                return;
            }

            observedOrientation = orientation;
            navigationInput?.NotifyOrientationChanged(orientation);
            if ((initialized || startupPanel != null) && runSession != null)
            {
                CancelTransientActionsForRotation();
            }

            ApplySafeAreaLayout(true);
            if (initialized && runShell != null && runShell.IsReady)
            {
                // A rotation changes the grid orientation: fit all 12 slots again once the layout has resolved.
                ConfigureRunCamera(true);
            }
        }

        private void Update()
        {
            NotifyOrientationChanged(Screen.orientation);
            ApplySafeAreaLayout();
            if (initialized && runShell != null && runShell.IsReady)
            {
                UpdateRunShell();
                ApplyRunViewport();
                animationClock.Drive((long)(Time.unscaledTimeAsDouble * 1000.0), slotGrid);
            }
        }

        private readonly PresentationAnimationClock animationClock = new PresentationAnimationClock();
        private IReducedMotionSignal reducedMotionSignal;
        private EffectiveReducedMotion cachedEffective;
        private bool hasCachedEffective;

        /// <summary>
        /// The bootstrap owns the OS signal, created lazily on first use. A test replaces it here; production never calls this.
        /// </summary>
        public void UseReducedMotionSignal(IReducedMotionSignal signal)
        {
            reducedMotionSignal = signal;
        }

        /// <summary>False when the platform has no reduce-motion probe, so "Follow system" cannot know the system setting.</summary>
        public bool SystemReducedMotionAvailable
        {
            get
            {
                reducedMotionSignal = reducedMotionSignal ?? new ReducedMotionSignal();
                return reducedMotionSignal.Available;
            }
        }

        /// <summary>
        /// The effective reduced-motion state as last resolved. Cached: it is recomputed only at start-up, on focus gain, when the
        /// Settings sheet opens and when the preference changes, never per frame (FR-026), because resolving "Follow system" is a
        /// JNI round trip on Android.
        /// </summary>
        public EffectiveReducedMotion CurrentReducedMotion => hasCachedEffective ? cachedEffective : RefreshReducedMotion();

        /// <summary>The shared presentation clock (T431): one tick per update drives every animated entry of the slot grid.</summary>
        public PresentationAnimationClock AnimationClock => animationClock;

        /// <summary>
        /// Resolves the effective reduced-motion state from the stored preference and the OS signal and applies it to the clock, so the
        /// loops freeze from the next frame (FR-020). Called at start-up and on focus gain; the Settings sheet (T433) calls it after a change.
        /// </summary>
        public EffectiveReducedMotion RefreshReducedMotion()
        {
            try
            {
                var store = DisplayPreferences;
                if (!preferencesRead)
                {
                    preferencesRead = true;
                    store.Reload();
                }

                reducedMotionSignal = reducedMotionSignal ?? new ReducedMotionSignal();
                var effective = ReducedMotionResolver.Resolve(store.Preference, reducedMotionSignal);
                animationClock.SetReducedMotion(effective.Reduced);
                cachedEffective = effective;
                hasCachedEffective = true;
                return effective;
            }
            catch (Exception)
            {
                // A preference failure never blocks start-up and never flips the state: the last resolved value stays (false if never resolved).
                return new EffectiveReducedMotion(animationClock.ReducedMotion, ReducedMotionSource.System);
            }
        }

        private bool preferencesRead;

        private void UpdateRunShell()
        {
            var available = runPersistence != null && runPersistence.Lifecycle == LifecycleState.Running;
            if (!shellAvailabilityKnown || available != shellAvailable)
            {
                shellAvailabilityKnown = true;
                shellAvailable = available;
                runShell.SetGameplayAvailable(available);
            }

            slotGrid.SetProjectedPixelsPerCell(CurrentPixelsPerCell);
            if (cameraController.HasLayout)
            {
                // Hidden in a single-level range; at the ends of a longer range the button stays reachable and says why it does nothing (rule 4).
                runShell.SetZoomButtonStates(
                    cameraController.ZoomInState != ZoomButtonState.Hidden,
                    cameraController.ZoomInState == ZoomButtonState.DisabledAtLimit,
                    cameraController.ZoomOutState != ZoomButtonState.Hidden,
                    cameraController.ZoomOutState == ZoomButtonState.DisabledAtLimit);
            }

            shellPresenter.Refresh();
        }

        /// <summary>
        /// Run-mode camera framing: hands the controller the real HUD-aware rectangle and panel scale. The first resolved rectangle
        /// (and a rotation or layout change) fits the whole layout once; later layout changes re-fit only when the scale no longer fits;
        /// a selection keeps its slot in the band above (or beside) an open sheet. A rectangle that has not resolved yet does nothing.
        /// </summary>
        private void ApplyRunViewport()
        {
            if (!runShell.TryGetUnobstructedScreenRect(Screen.height, out var visibleArea))
            {
                return;
            }

            var layout = RunCameraFraming.Layout(factoryCamera, slotGrid.Layout, PixelsPerPoint(), visibleArea);
            var fit = runFramingPending || !cameraController.HasLayout;
            var structural = hasObservedLayout && (observedLayout.PixelWidth != layout.PixelWidth || observedLayout.PixelHeight != layout.PixelHeight ||
                observedLayout.LayoutId != layout.LayoutId || !Mathf.Approximately(observedLayout.PixelsPerPoint, layout.PixelsPerPoint));
            var changed = !hasObservedLayout || structural || observedLayout.UnobstructedRect != visibleArea;
            var wantSheet = selectedSlot >= 0;
            var closedNow = false;

            if (fit)
            {
                runFramingPending = false;
                cameraController.Fit(layout);
                changed = true;
            }
            else if (!wantSheet && cameraController.SheetOpen)
            {
                // The prior view returns at once; the rectangle that is still the band is not remembered as the closed one.
                cameraController.CloseSheet(lastClosedRect);
                closedNow = true;
            }
            else if (changed && structural && wantSheet)
            {
                // The model's sheet band carries the old screen size: fit again, the selection then reopens its sheet below.
                cameraController.Fit(layout);
            }
            else if (changed && !wantSheet)
            {
                cameraController.UpdateLayout(layout);
            }

            if (wantSheet && (changed || selectionChanged || !cameraController.SheetOpen))
            {
                // SC-002: the whole machine (plate and badge band) stays in the band the card or sheet leaves, not only its plate.
                cameraController.OpenSheet(visibleArea, framing.FrameMachine(selectedSlot, slotGrid.Layout));
            }

            selectionChanged = false;
            observedLayout = layout;
            hasObservedLayout = true;
            if (!wantSheet && !closedNow && !cameraController.SheetOpen)
            {
                lastClosedRect = visibleArea;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            NotifyFocusChanged(hasFocus);
        }

        private void OnApplicationPause(bool paused)
        {
            animationClock.SetSuspended(paused);
            if (paused)
            {
                NotifyFocusChanged(false);
            }
        }

        private void OnApplicationQuit()
        {
            // T337 (T306 QA observation 1, SEC-9): a quit callback never lets an exception reach the engine.
            try
            {
                PrepareForDeparture();
            }
            catch (Exception exception)
            {
                RunLifecycleAdapter.LogContained("Bootstrap quit handling failed and was contained", exception);
            }
        }

        private void OnDestroy()
        {
            if (displayPreferences != null)
            {
                displayPreferences.Changed -= OnDisplayPreferenceChanged;
            }

            if (runLifecycle != null && runLifecycleSubscribed)
            {
                runLifecycle.ResultChanged -= HandleRunLifecycleResultChanged;
                runLifecycleSubscribed = false;
            }

            if (navigationInput != null)
            {
                navigationInput.IntentRaised -= RouteNavigationIntent;
            }

            if (runShell != null)
            {
                runShell.SlotSelectionChanged -= HandleShellSlotSelection;
                if (cameraController != null)
                {
                    runShell.ZoomRequested -= cameraController.ZoomFromVisibleControl;
                }
            }

            dialogRotationRegistration?.Dispose();
            DisposePanelHandles();
            DisposeStartupPanel();
            if (offlinePanel != null)
            {
                offlinePanel.RetryCompleted -= HandleOfflineRetryCompleted;
                offlinePanel.Dismissed -= HandleOfflineDismissed;
                offlinePanel.Unmount();
                offlinePanel = null;
            }

            shellPresenter?.Dispose();
        }

        private void RouteNavigationIntent(NavigationIntent intent)
        {
            switch (intent.Kind)
            {
                case NavigationIntentKind.Pan:
                    // A press on the world unpins the exact-value line and clears a transient notice (T192, F-5).
                    runShell?.NotifyWorldPress();
                    cameraController.HandleNavigationIntent(intent);
                    break;
                case NavigationIntentKind.Zoom:
                case NavigationIntentKind.ZoomGestureEnded:
                case NavigationIntentKind.CancelWorldGesture:
                    // A pinch previews continuously and snaps on its end; a second touch or an interruption cancels it (camera-scale rules 4 and 5).
                    cameraController.HandleNavigationIntent(intent);
                    break;
                case NavigationIntentKind.WorldTap:
                    runShell?.NotifyWorldPress();
                    if (shellPresenter != null)
                    {
                        // A slot tap opens the build sheet (empty) or the machine panel (occupied) (T112).
                        shellPresenter.HandleWorldTap(ScreenToWorld(intent.Position));
                    }

                    break;
            }
        }

        private void PrepareForDeparture()
        {
            // The adapter invokes this synchronously before recording suspension. The host flushes its
            // final active interval, marks itself unavailable, and then disables its Update loop.
            runtimeHost?.PrepareForLifecycleDeparture();

            navigationInput?.Interpreter?.CancelAll();
        }

        private static double ReadCumulativeActiveSeconds()
        {
#if UNITY_EDITOR
            if (testOverrides != null)
            {
                return testOverrides.ReadCumulativeActiveSeconds();
            }
#endif
            return Time.realtimeSinceStartupAsDouble;
        }

        private Vector2 ScreenToWorld(Vector2 screenPoint)
        {
            var world = factoryCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, -factoryCamera.transform.position.z));
            return new Vector2(world.x, world.y);
        }

        private bool IsOverRuntimeUi(Vector2 screenPoint)
        {
            // Every screen panel counts, not only the shell's: a separate overlay panel consumes the press too (T366).
            return RuntimePanelHitTest.IsOverUi(screenPoint, uiDocument);
        }

        private bool ValidateReferences(out string error)
        {
            var missing = new List<string>();
            Require(inputActions, nameof(inputActions), missing);
            Require(visualTree, nameof(visualTree), missing);
            Require(styleSheet, nameof(styleSheet), missing);
            Require(factoryCamera, nameof(factoryCamera), missing);
            Require(cameraController, nameof(cameraController), missing);
            Require(navigationInput, nameof(navigationInput), missing);
            Require(uiDocument, nameof(uiDocument), missing);
            Require(hud, nameof(hud), missing);
            Require(runtimeHost, nameof(runtimeHost), missing);
            error = missing.Count == 0
                ? string.Empty
                : "Core Game Foundation is disabled. Missing serialized references: " + string.Join(", ", missing) + ".";
            return missing.Count == 0;
        }

        private bool DisableWithError(string error)
        {
            InitializationError = string.IsNullOrWhiteSpace(error) ? "Development bootstrap is unavailable." : error;
            initialized = false;
            if (navigationInput != null)
            {
                navigationInput.Interpreter?.CancelAll();
                navigationInput.enabled = false;
            }
            if (cameraController != null) cameraController.enabled = false;
            runShell?.SetDisplayed(false);
            hud?.SetDisplayed(true);
            hud?.SetState(FactoryHudState.Error, InitializationError);
            Debug.LogError(InitializationError, this);
            enabled = false;
            return false;
        }

        private static void Require(UnityEngine.Object value, string name, ICollection<string> missing)
        {
            if (value == null)
            {
                missing.Add(name);
            }
        }

        private sealed class DevelopmentUtcClock : IUtcClock
        {
            private static readonly DeviceUtcClock DeviceClock = new DeviceUtcClock();

            public DateTimeOffset GetUtcNow()
            {
#if UNITY_EDITOR
                if (testOverrides != null)
                {
                    return testOverrides.ReadUtcNow();
                }
#endif
                // Player builds use the device clock in whole milliseconds, which every checkpoint format represents.
                return DeviceClock.GetUtcNow();
            }
        }

#if UNITY_EDITOR
        private sealed class CompositionTestOverrides
        {
            public CompositionTestOverrides(
                string persistentDataRoot,
                Func<RunCatalog, IRunCheckpointStore> createRunStore,
                Func<DateTimeOffset> readUtcNow,
                Func<double> readCumulativeActiveSeconds,
                Func<byte[]> readContentBytes,
                Func<byte[]> readLegacyImportBytes)
            {
                PersistentDataRoot = persistentDataRoot;
                CreateRunStore = createRunStore;
                ReadUtcNow = readUtcNow;
                ReadCumulativeActiveSeconds = readCumulativeActiveSeconds;
                ReadContentBytes = readContentBytes;
                ReadLegacyImportBytes = readLegacyImportBytes;
            }

            public string PersistentDataRoot { get; }
            public Func<RunCatalog, IRunCheckpointStore> CreateRunStore { get; }
            public Func<DateTimeOffset> ReadUtcNow { get; }
            public Func<double> ReadCumulativeActiveSeconds { get; }
            public Func<byte[]> ReadContentBytes { get; }
            public Func<byte[]> ReadLegacyImportBytes { get; }
        }

        private sealed class CompositionTestOverrideScope : IDisposable
        {
            private readonly CompositionTestOverrides overrides;
            private bool disposed;

            public CompositionTestOverrideScope(CompositionTestOverrides overrides)
            {
                this.overrides = overrides;
            }

            public void Dispose()
            {
                if (disposed) return;
                if (!ReferenceEquals(testOverrides, overrides))
                {
                    throw new InvalidOperationException("The composition test override changed before the scope ended.");
                }

                testOverrides = null;
                disposed = true;
            }
        }
#endif
    }
}
