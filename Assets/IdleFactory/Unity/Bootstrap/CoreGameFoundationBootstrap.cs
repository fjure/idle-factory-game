using System;
using System.Collections.Generic;
using System.IO;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Input;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.Bootstrap
{
    /// <summary>
    /// Single composition root and state owner of the scene. Since Feature 003 it composes the Iron factory run: v2 content
    /// through the dispatcher, the Save V2 store, the cold-start source selection (V2, V1 import, fresh) and a
    /// <see cref="RunSession"/>. The Feature 001 runtime (<see cref="FactorySession"/>, placement) stays composed only for
    /// Editor scene tests that supply version 1 content, until T119 retires it with its tests.
    /// </summary>
    public sealed class CoreGameFoundationBootstrap : MonoBehaviour
    {
        private const int FactorySize = 16;
        private const float CellSize = 1f;

        [Header("Authored content")]
        [SerializeField] private TextAsset gameplayContent;
        [Tooltip("The version 1 development catalog. It is read only when a Save V1 checkpoint must be imported.")]
        [SerializeField] private TextAsset legacyImportContent;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private VisualTreeAsset visualTree;
        [SerializeField] private StyleSheet styleSheet;
        [SerializeField] private PresentationSpriteCatalog spriteCatalog = new PresentationSpriteCatalog();

        [Header("Scene references")]
        [SerializeField] private Camera factoryCamera;
        [SerializeField] private FactoryCameraController cameraController;
        [SerializeField] private NavigationInputAdapter navigationInput;
        [SerializeField] private PlacementInputController placementController;
        [SerializeField] private FactoryView factoryView;
        [SerializeField] private PlacementPreviewView previewView;
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private FactoryHudDocument hud;
        [SerializeField] private ApplicationLifecycleAdapter lifecycleAdapter;
        [SerializeField] private FactoryRuntimeHost runtimeHost;
        [SerializeField] private PersistencePresenter persistencePresenter;

        private PlacementCommands commands;
        private ProductionCommands productionCommands;
        private PersistenceCommands persistenceCommands;
        private DefinitionCatalog catalog;
        private GridWorldMapper mapper;
        private Rect observedSafeArea;
        private int observedWidth;
        private int observedHeight;
        private ScreenOrientation observedOrientation;
        private bool initialized;
        private bool initialFramingPending;
        private Rect observedUnobstructedArea;

        // Feature 003 run composition. The run is null until the cold-start source selection created it.
        private RunCatalog runCatalog;
        private RunSession runSession;
        private RunCommands runCommands;
        private RunPersistence runPersistence;
        private RunLifecycleAdapter runLifecycle;
        private bool runLifecycleSubscribed;
        private bool legacyRuntimeActive;
        private readonly List<TransientActionRegistration> transientActions = new List<TransientActionRegistration>();

        /// <summary>The one-line notice that a rotation cancelled an open transient action (UX-9).</summary>
        public const string RotationCancelledNotice = "Cancelled by rotation";

#if UNITY_EDITOR
        private static CompositionTestOverrides testOverrides;

        /// <summary>Scopes an isolated checkpoint store and deterministic clocks around an Editor scene test (Feature 001 runtime).</summary>
        public static IDisposable OverrideCompositionDependenciesForTests(
            Func<DefinitionCatalog, ICheckpointStore> createCheckpointStore,
            Func<DateTimeOffset> readUtcNow,
            Func<double> readCumulativeActiveSeconds,
            Func<byte[]> readContentBytes = null)
        {
            if (createCheckpointStore == null) throw new ArgumentNullException(nameof(createCheckpointStore));
            if (readUtcNow == null) throw new ArgumentNullException(nameof(readUtcNow));
            if (readCumulativeActiveSeconds == null) throw new ArgumentNullException(nameof(readCumulativeActiveSeconds));
            return InstallOverrides(new CompositionTestOverrides(
                createCheckpointStore,
                null,
                null,
                readUtcNow,
                readCumulativeActiveSeconds,
                readContentBytes));
        }

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
            return InstallOverrides(new CompositionTestOverrides(
                null,
                persistentDataRoot,
                createRunStore,
                readUtcNow,
                readCumulativeActiveSeconds,
                readContentBytes,
                readLegacyImportBytes));
        }

        private static IDisposable InstallOverrides(CompositionTestOverrides overrides)
        {
            if (testOverrides != null)
            {
                throw new InvalidOperationException("A composition test override is already active.");
            }

            testOverrides = overrides;
            return new CompositionTestOverrideScope(overrides);
        }
#endif

        public FactorySession Session { get; private set; }
        public ProductionCommands ProductionCommands => productionCommands;
        public PersistenceCommands PersistenceCommands => persistenceCommands;
        public FactoryView FactoryView => factoryView;
        public ApplicationLifecycleAdapter LifecycleAdapter => lifecycleAdapter;
        public FactoryRuntimeHost RuntimeHost => runtimeHost;
        public PersistencePresenter PersistencePresenter => persistencePresenter;
        public FactoryHudDocument Hud => hud;
        public PlacementInputController PlacementController => placementController;
        public NavigationInputAdapter NavigationInput => navigationInput;
        public Camera FactoryCamera => factoryCamera;
        public UIDocument UiDocument => uiDocument;
        public InputActionAsset InputActions => inputActions;
        public DefinitionCatalog Definitions { get; private set; }
        public PresentationSpriteCatalog SpriteCatalog => spriteCatalog;
        /// <summary>The Feature 003 run session, or null when start-up failed before one was created.</summary>
        public RunSession RunSession => runSession;
        public RunCommands RunCommands => runCommands;
        public RunPersistence RunPersistence => runPersistence;
        public RunCatalog RunCatalog => runCatalog;
        public RunLifecycleAdapter RunLifecycle => runLifecycle;

        /// <summary>The offline summary of the latest genuine return (never of a rotation, FR-036), or null.</summary>
        public RunOfflineSummary LastOfflineSummary { get; private set; }

        /// <summary>The stable code of the failure that disabled start-up, or empty. Static text only, never an exception message or a path.</summary>
        public string StartupDiagnosticCode { get; private set; } = string.Empty;

        /// <summary>True once the Save V2 store was created; false when start-up failed before checkpoint I/O.</summary>
        public bool CheckpointStoreCreated { get; private set; }

        public string InitializationError { get; private set; } = string.Empty;
        public bool IsInitialized => initialized;
        public bool InitialFramingApplied => initialized && !initialFramingPending;
        public FactorySnapshot Snapshot => commands?.GetFactorySnapshot();

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

#if UNITY_EDITOR
            if (testOverrides != null && testOverrides.IsFeature001Runtime)
            {
                return InitializeFeature001Runtime(contentBytes);
            }
#endif

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
            if (testOverrides != null)
            {
                if (testOverrides.ReadContentBytes != null)
                {
                    return testOverrides.ReadContentBytes();
                }

                if (testOverrides.IsFeature001Runtime)
                {
                    // Feature 001 scene tests keep their version 1 catalog, which is the packaged legacy import catalog. A scene
                    // that has no gameplay content assigned is still rejected as empty.
                    return gameplayContent == null || legacyImportContent == null ? null : legacyImportContent.bytes;
                }
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

                runSession = new RunSession(runCatalog, state, permanent, startMs);
                runCommands = new RunCommands(runSession);
                runPersistence = new RunPersistence(runSession, store, clock);
                var legacySource = new LazyLegacyCheckpointSource(CurrentPersistenceRoot(), ReadLegacyImportBytes);
                var coldStart = runtimeHost.MeasureResumeAndReconcile(() => runPersistence.ColdStart(legacySource));
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

            // The scene serializes the host disabled because the Feature 001 inspector toggled it. The run host gates itself
            // on whether any machine can produce or any timer is pending, so it stays enabled.
            runtimeHost.enabled = true;
            mapper = new GridWorldMapper(CellSize, Vector2.zero);
            var worldBounds = new Rect(0f, 0f, FactorySize * CellSize, FactorySize * CellSize);
            factoryCamera.orthographic = true;
            factoryCamera.transform.position = new Vector3(worldBounds.center.x, worldBounds.center.y, -10f);
            cameraController.Configure(factoryCamera, worldBounds, 2f, 12f, Vector2.one);
            cameraController.PanTo(worldBounds.center);

            // The Feature 001 placement runtime has no run to act on; the run surfaces arrive with T112 and later.
            legacyRuntimeActive = false;
            if (placementController != null) placementController.enabled = false;
            if (factoryView != null) factoryView.enabled = false;
            if (previewView != null) previewView.enabled = false;

            navigationInput.Configure(inputActions, IsOverRuntimeUi);
            navigationInput.IntentRaised -= RouteNavigationIntent;
            navigationInput.IntentRaised += RouteNavigationIntent;
            hud.ZoomRequested -= cameraController.ZoomFromVisibleControl;
            hud.ZoomRequested += cameraController.ZoomFromVisibleControl;

            if (runLifecycle == null)
            {
                runLifecycle = gameObject.AddComponent<RunLifecycleAdapter>();
                runLifecycle.Configure(
                    runPersistence,
                    PrepareForDeparture,
                    reconcile => runtimeHost.MeasureResumeAndReconcile(reconcile));
            }

            if (!runLifecycleSubscribed)
            {
                runLifecycle.ResultChanged += HandleRunLifecycleResultChanged;
                runLifecycleSubscribed = true;
            }

            if (coldStartOutcome != null && coldStartOutcome.OfflineSummary != null)
            {
                LastOfflineSummary = coldStartOutcome.OfflineSummary;
            }

            observedOrientation = Screen.orientation;
            initialized = true;
            initialFramingPending = false;
            InitializationError = string.Empty;
            StartupDiagnosticCode = string.Empty;
            ApplySafeAreaLayout(true);
            hud.SetState(FactoryHudState.Normal, "Iron factory run is ready.");
            hud.SetGameplayAvailability(false);
            placementController?.SetLifecycleAvailability(false);
            return true;
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

        private string CurrentPersistenceRoot()
        {
#if UNITY_EDITOR
            if (testOverrides != null && testOverrides.PersistentDataRoot != null)
            {
                return testOverrides.PersistentDataRoot;
            }
#endif
            return Application.persistentDataPath;
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

            return cancelled;
        }

        private void HandleRunLifecycleResultChanged(RunCommandResult<RunPersistenceOutcome> result, LifecycleReturnKind kind)
        {
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

#if UNITY_EDITOR
        private bool InitializeFeature001Runtime(byte[] contentBytes)
        {
            ContentLoadResult contentResult;
            try
            {
                contentResult = GameplayContentLoader.Load(contentBytes);
            }
            catch (Exception exception)
            {
                // Only the exception type is reported: messages can echo content values or paths.
                return DisableWithError(
                    $"content.internal: Gameplay content could not be processed ({exception.GetType().Name}).");
            }

            if (!contentResult.IsSuccess)
            {
                var diagnostic = contentResult.Diagnostics[0];
                return DisableWithError($"{diagnostic.Code}: {diagnostic.Message}");
            }

            if (!spriteCatalog.Validate())
            {
                return DisableWithError(spriteCatalog.Error);
            }

            foreach (var resource in contentResult.Catalog.Resources.Values)
            {
                if (resource.PresentationKey != null && !spriteCatalog.TryGet(resource.PresentationKey, out _))
                {
                    return DisableWithError(
                        $"presentation.key.unresolved: Resource '{resource.Id.Value}' presentation key '{resource.PresentationKey}' has no approved sprite.");
                }
            }

            foreach (var building in contentResult.Catalog.Buildings.Values)
            {
                if (!spriteCatalog.TryGet(building.PresentationKey, out _))
                {
                    return DisableWithError(
                        $"presentation.key.unresolved: Building '{building.Id.Value}' presentation key '{building.PresentationKey}' has no approved sprite.");
                }
            }

            legacyRuntimeActive = true;
            try
            {
                catalog = contentResult.Catalog;
                var bounds = new FactoryBounds(new GridPosition(0, 0), FactorySize, FactorySize);
                var emptyState = new FactoryState(bounds, catalog, Array.Empty<BuildingInstanceState>());
                var store = CreateCheckpointStore(catalog);
                Session = new FactorySession(emptyState, catalog, new DevelopmentUtcClock(), store);
                commands = new PlacementCommands(Session, new GuidBuildingInstanceIdSource());
                productionCommands = new ProductionCommands(Session);
                persistenceCommands = new PersistenceCommands(Session);
                runtimeHost.Configure(productionCommands, ReadCumulativeActiveSeconds);
                mapper = new GridWorldMapper(CellSize, Vector2.zero);
                var worldBounds = new Rect(0f, 0f, FactorySize * CellSize, FactorySize * CellSize);
                factoryCamera.orthographic = true;
                factoryCamera.transform.position = new Vector3(worldBounds.center.x, worldBounds.center.y, -10f);
                cameraController.Configure(factoryCamera, worldBounds, 2f, 12f, Vector2.one);
                cameraController.PanTo(worldBounds.center);

                factoryView.Configure(commands, catalog, spriteCatalog, mapper);
                previewView.Configure(spriteCatalog, mapper);
                placementController.Configure(
                    commands,
                    catalog,
                    factoryView,
                    previewView,
                    hud,
                    mapper,
                    ScreenToWorld,
                    navigationInput);

                navigationInput.Configure(inputActions, IsOverRuntimeUi);
                navigationInput.IntentRaised += RouteNavigationIntent;
                hud.ZoomRequested += cameraController.ZoomFromVisibleControl;
                lifecycleAdapter.ResultChanged += HandleLifecycleResultChanged;
                lifecycleAdapter.Configure(persistenceCommands, PrepareForDeparture);
                var coldStartHadNoCheckpoint = lifecycleAdapter.LastResult != null &&
                    lifecycleAdapter.LastResult.Succeeded && lifecycleAdapter.LastResult.Value != null &&
                    lifecycleAdapter.LastResult.Value.OfflineSummary != null &&
                    lifecycleAdapter.LastResult.Value.OfflineSummary.Outcome == OfflineReturnOutcome.NoCheckpoint;
                if (Session.LifecycleState == LifecycleState.Running && coldStartHadNoCheckpoint)
                {
                    var preparedScenario = persistenceCommands.ResetDevelopmentScenario();
                    if (!preparedScenario.Succeeded)
                    {
                        return DisableWithError(preparedScenario.Message);
                    }
                }

                if (Session.LifecycleState == LifecycleState.Running)
                {
                    // T061: cold-start reconciliation replaces the session's factory state after
                    // factoryView.Configure() already rendered the pre-reconciliation empty state. Render the
                    // resulting factory now so the first frame the player can act on already shows it.
                    factoryView.Refresh();
                }

                persistencePresenter.Configure(
                    persistenceCommands,
                    hud.Root,
                    () => Session.LifecycleState,
                    () => placementController.Cancel(),
                    () => navigationInput.Interpreter?.CancelAll(),
                    RefreshReplacedFactoryAndSelection,
                    () => lifecycleAdapter.LastResult != null && lifecycleAdapter.LastResult.Succeeded
                        ? lifecycleAdapter.LastResult.Value
                        : null,
                    ReadRetainedProductionProgress,
                    () => lifecycleAdapter.LastReturnKind == LifecycleReturnKind.ConfigurationChange);
                observedOrientation = Screen.orientation;
                initialized = true;
                initialFramingPending = true;
                ApplySafeAreaLayout(true);
                hud.SetState(FactoryHudState.Normal, "Development-only D001 factory is ready.");
                Definitions = catalog;
                return true;
            }
            catch (Exception exception)
            {
                return DisableWithError($"Development bootstrap failed ({exception.GetType().Name}).");
            }
        }
#endif

        public FactoryView RecreateFactoryView()
        {
            if (!initialized)
            {
                return null;
            }

            var selection = factoryView.SelectedInstanceId;
            var previous = factoryView.gameObject;
            var replacement = new GameObject("Factory View");
            replacement.transform.SetParent(transform, false);
            factoryView = replacement.AddComponent<FactoryView>();
            factoryView.Configure(commands, catalog, spriteCatalog, mapper);
            factoryView.Select(selection);
            placementController.Configure(
                commands,
                catalog,
                factoryView,
                previewView,
                hud,
                mapper,
                ScreenToWorld);
            Destroy(previous);
            return factoryView;
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
        }

        public void NotifyFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
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
            placementController?.Cancel();
            if (initialized && runSession != null)
            {
                CancelTransientActionsForRotation();
            }

            ApplySafeAreaLayout(true);
        }

        private void Update()
        {
            NotifyOrientationChanged(Screen.orientation);
            ApplySafeAreaLayout();
            ApplyUnobstructedViewport();
        }

        /// <summary>
        /// Keeps the camera's pan limits aligned with the screen area the HUD leaves uncovered and, once that
        /// area is first known, centres the placed buildings in it (T054). Only the camera changes; factory
        /// state is read, never mutated.
        /// </summary>
        private void ApplyUnobstructedViewport()
        {
            if (!initialized ||
                !hud.TryGetUnobstructedScreenRect(Screen.width, Screen.height, Screen.safeArea, out var visibleArea))
            {
                return;
            }

            if (visibleArea != observedUnobstructedArea)
            {
                observedUnobstructedArea = visibleArea;
                cameraController.SetUnobstructedScreenRect(visibleArea);
            }

            if (!initialFramingPending)
            {
                return;
            }

            initialFramingPending = false;
            if (commands == null)
            {
                return;
            }

            var snapshot = commands.GetFactorySnapshot();
            if (snapshot == null || snapshot.Occupancy.Count == 0)
            {
                return;
            }

            var content = Rect.zero;
            var first = true;
            foreach (var cell in snapshot.Occupancy.Keys)
            {
                var origin = mapper.CellOrigin(cell);
                var cellRect = new Rect(origin, new Vector2(mapper.CellSize, mapper.CellSize));
                content = first ? cellRect : Rect.MinMaxRect(
                    Mathf.Min(content.xMin, cellRect.xMin),
                    Mathf.Min(content.yMin, cellRect.yMin),
                    Mathf.Max(content.xMax, cellRect.xMax),
                    Mathf.Max(content.yMax, cellRect.yMax));
                first = false;
            }

            cameraController.FrameContent(content);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            NotifyFocusChanged(hasFocus);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                NotifyFocusChanged(false);
            }
        }

        private void OnApplicationQuit()
        {
            PrepareForDeparture();
        }

        private void OnDestroy()
        {
            if (runLifecycle != null && runLifecycleSubscribed)
            {
                runLifecycle.ResultChanged -= HandleRunLifecycleResultChanged;
                runLifecycleSubscribed = false;
            }

            if (lifecycleAdapter != null)
            {
                lifecycleAdapter.ResultChanged -= HandleLifecycleResultChanged;
            }

            if (navigationInput != null)
            {
                navigationInput.IntentRaised -= RouteNavigationIntent;
            }

            if (hud != null && cameraController != null)
            {
                hud.ZoomRequested -= cameraController.ZoomFromVisibleControl;
            }
        }

        private void RouteNavigationIntent(NavigationIntent intent)
        {
            switch (intent.Kind)
            {
                case NavigationIntentKind.Pan:
                case NavigationIntentKind.Zoom:
                    cameraController.HandleNavigationIntent(intent);
                    break;
                case NavigationIntentKind.WorldTap:
                    if (legacyRuntimeActive)
                    {
                        placementController.HandleNavigationIntent(intent);
                    }

                    break;
                case NavigationIntentKind.CancelWorldGesture:
                    placementController.Cancel();
                    break;
            }
        }

        private void PrepareForDeparture()
        {
            // The adapter invokes this synchronously before recording suspension. The host flushes its
            // final active interval, marks itself unavailable, and then disables its Update loop.
            runtimeHost?.PrepareForLifecycleDeparture();

            navigationInput?.Interpreter?.CancelAll();
            placementController?.Cancel();
        }

        private void HandleLifecycleResultChanged(CommandResult<PersistenceOperationResult> result)
        {
            if (result != null && result.Succeeded && Session != null &&
                Session.LifecycleState == LifecycleState.Running)
            {
                // A disabled MonoBehaviour can miss Unity's return callbacks. Reconcile its local
                // availability before the inspector decides whether the ready machine can re-enable it.
                runtimeHost?.SynchronizeAfterSuccessfulLifecycleReturn();
            }

            RefreshGameplayEntryPoints();
            persistencePresenter?.Refresh();
            GetComponent<MachineInspectorPresenter>()?.RefreshLifecycleGate();
        }

        private void RefreshReplacedFactoryAndSelection(FactorySnapshot snapshot)
        {
            if (Session != null && Session.LifecycleState == LifecycleState.Running)
            {
                // Successful presenter-driven recovery can publish Running without a lifecycle-adapter
                // result event. Restore host availability before refreshing the view/inspector can enable it.
                runtimeHost?.SynchronizeAfterSuccessfulLifecycleReturn();
            }

            RefreshGameplayEntryPoints();
            placementController?.Cancel();
            navigationInput?.Interpreter?.CancelAll();
            factoryView?.Refresh();
            GetComponent<MachineInspectorPresenter>()?.RefreshLifecycleGate();
        }

        private void RefreshGameplayEntryPoints()
        {
            var gameplayAvailable = Session != null && Session.LifecycleState == LifecycleState.Running;
            hud?.SetGameplayAvailability(gameplayAvailable);
            placementController?.SetLifecycleAvailability(gameplayAvailable);
        }

        private CommandResult<MachineInspectionSnapshot> ReadRetainedProductionProgress(FactorySnapshot snapshot)
        {
            var building = snapshot != null && snapshot.Buildings.Count > 0 ? snapshot.Buildings[0] : null;
            return building == null
                ? CommandResult<MachineInspectionSnapshot>.Rejected(
                    CommandOutcome.InvalidRequest,
                    "No machine is available for retained-progress inspection.")
                : productionCommands.GetMachineInspection(building.InstanceId);
        }

        private static ICheckpointStore CreateCheckpointStore(DefinitionCatalog definitionCatalog)
        {
#if UNITY_EDITOR
            if (testOverrides != null)
            {
                return testOverrides.CreateCheckpointStore(definitionCatalog);
            }
#endif
            return new JsonCheckpointStore(Application.persistentDataPath, definitionCatalog);
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
            var panel = uiDocument?.rootVisualElement?.panel;
            if (panel == null)
            {
                return false;
            }

            var panelPoint = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPoint.x, Screen.height - screenPoint.y));
            var picked = panel.Pick(panelPoint);
            // The document root covers the viewport, so it is not itself a UI hit. Any
            // authored descendant is a UI region, including passive panels and disabled controls.
            return picked != null && picked != uiDocument.rootVisualElement;
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
            Require(placementController, nameof(placementController), missing);
            Require(factoryView, nameof(factoryView), missing);
            Require(previewView, nameof(previewView), missing);
            Require(uiDocument, nameof(uiDocument), missing);
            Require(hud, nameof(hud), missing);
            Require(lifecycleAdapter, nameof(lifecycleAdapter), missing);
            Require(runtimeHost, nameof(runtimeHost), missing);
            Require(persistencePresenter, nameof(persistencePresenter), missing);
            error = missing.Count == 0
                ? string.Empty
                : "Core Game Foundation is disabled. Missing serialized references: " + string.Join(", ", missing) + ".";
            return missing.Count == 0;
        }

        private bool DisableWithError(string error)
        {
            InitializationError = string.IsNullOrWhiteSpace(error) ? "Development bootstrap is unavailable." : error;
            initialized = false;
            Definitions = null;
            catalog = null;
            placementController?.Cancel();
            if (placementController != null) placementController.enabled = false;
            if (navigationInput != null)
            {
                navigationInput.Interpreter?.CancelAll();
                navigationInput.enabled = false;
            }
            if (cameraController != null) cameraController.enabled = false;
            if (factoryView != null) factoryView.enabled = false;
            if (previewView != null) previewView.enabled = false;
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

        private sealed class GuidBuildingInstanceIdSource : IBuildingInstanceIdSource
        {
            public CommandResult<BuildingInstanceId> Next() =>
                CommandResult<BuildingInstanceId>.Success(new BuildingInstanceId(Guid.NewGuid().ToString("D")));
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
                // Player builds must use the Save V1-representable device clock (whole milliseconds).
                return DeviceClock.GetUtcNow();
            }
        }

#if UNITY_EDITOR
        private sealed class CompositionTestOverrides
        {
            public CompositionTestOverrides(
                Func<DefinitionCatalog, ICheckpointStore> createCheckpointStore,
                string persistentDataRoot,
                Func<RunCatalog, IRunCheckpointStore> createRunStore,
                Func<DateTimeOffset> readUtcNow,
                Func<double> readCumulativeActiveSeconds,
                Func<byte[]> readContentBytes,
                Func<byte[]> readLegacyImportBytes = null)
            {
                CreateCheckpointStore = createCheckpointStore;
                PersistentDataRoot = persistentDataRoot;
                CreateRunStore = createRunStore;
                ReadUtcNow = readUtcNow;
                ReadCumulativeActiveSeconds = readCumulativeActiveSeconds;
                ReadContentBytes = readContentBytes;
                ReadLegacyImportBytes = readLegacyImportBytes;
            }

            /// <summary>True for the Feature 001 overrides (a legacy checkpoint store); false for the Feature 003 run overrides.</summary>
            public bool IsFeature001Runtime => CreateCheckpointStore != null;
            public Func<DefinitionCatalog, ICheckpointStore> CreateCheckpointStore { get; }
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
