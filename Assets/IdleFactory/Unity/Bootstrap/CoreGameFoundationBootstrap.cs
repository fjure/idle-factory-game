using System;
using System.Collections.Generic;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
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
    /// <summary>Single development-only composition root and state owner for the US1 scene.</summary>
    public sealed class CoreGameFoundationBootstrap : MonoBehaviour
    {
        private const int FactorySize = 16;
        private const float CellSize = 1f;

        [Header("Authored content")]
        [SerializeField] private DefinitionCatalogAsset developmentCatalog;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private VisualTreeAsset visualTree;
        [SerializeField] private StyleSheet styleSheet;
        [SerializeField] private F001SpriteCatalog spriteCatalog = new F001SpriteCatalog();

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

#if UNITY_EDITOR
        private static CompositionTestOverrides testOverrides;

        /// <summary>Scopes an isolated checkpoint store and deterministic clocks around an Editor scene test.</summary>
        public static IDisposable OverrideCompositionDependenciesForTests(
            Func<DefinitionCatalog, ICheckpointStore> createCheckpointStore,
            Func<DateTimeOffset> readUtcNow,
            Func<double> readCumulativeActiveSeconds)
        {
            if (createCheckpointStore == null) throw new ArgumentNullException(nameof(createCheckpointStore));
            if (readUtcNow == null) throw new ArgumentNullException(nameof(readUtcNow));
            if (readCumulativeActiveSeconds == null) throw new ArgumentNullException(nameof(readCumulativeActiveSeconds));
            if (testOverrides != null)
            {
                throw new InvalidOperationException("A composition test override is already active.");
            }

            var overrides = new CompositionTestOverrides(
                createCheckpointStore,
                readUtcNow,
                readCumulativeActiveSeconds);
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
        public DefinitionCatalogAsset DevelopmentCatalog => developmentCatalog;
        public F001SpriteCatalog SpriteCatalog => spriteCatalog;
        public string InitializationError { get; private set; } = string.Empty;
        public bool IsInitialized => initialized;
        public FactorySnapshot Snapshot => commands?.GetFactorySnapshot();

        private void Awake()
        {
            Initialize();
        }

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

            try
            {
                catalog = DefinitionCatalogBuilder.Build(developmentCatalog);
                var bounds = new FactoryBounds(new GridPosition(0, 0), FactorySize, FactorySize);
                var emptyState = new FactoryState(bounds, catalog, Array.Empty<BuildingInstanceState>());
                var store = CreateCheckpointStore(catalog);
                Session = new FactorySession(emptyState, catalog, new DevelopmentUtcClock(), store);
                commands = new PlacementCommands(Session, new GuidBuildingInstanceIdSource());
                productionCommands = new ProductionCommands(Session);
                persistenceCommands = new PersistenceCommands(Session);
                runtimeHost.Configure(productionCommands, ReadCumulativeActiveSeconds);
                mapper = new GridWorldMapper(CellSize, Vector2.zero);
                if (!spriteCatalog.Validate())
                {
                    return DisableWithError(spriteCatalog.Error);
                }

                var worldBounds = new Rect(0f, 0f, FactorySize * CellSize, FactorySize * CellSize);
                factoryCamera.orthographic = true;
                factoryCamera.transform.position = new Vector3(worldBounds.center.x, worldBounds.center.y, -10f);
                cameraController.Configure(factoryCamera, worldBounds, 2f, 12f, Vector2.one);
                cameraController.PanTo(worldBounds.center);

                if (!hud.Configure(uiDocument, visualTree, styleSheet))
                {
                    return DisableWithError(hud.Error);
                }

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
                    ReadRetainedProductionProgress);
                observedOrientation = Screen.orientation;
                initialized = true;
                ApplySafeAreaLayout(true);
                hud.SetState(FactoryHudState.Normal, "Development-only D001 factory is ready.");
                return true;
            }
            catch (Exception exception)
            {
                return DisableWithError($"Development bootstrap failed: {exception.Message}");
            }
        }

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
            hud?.ApplyLayout(Screen.width, Screen.height, safeArea);
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
            ApplySafeAreaLayout(true);
        }

        private void Update()
        {
            NotifyOrientationChanged(Screen.orientation);
            ApplySafeAreaLayout();
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
                    placementController.HandleNavigationIntent(intent);
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

            placementController?.Cancel();
            navigationInput?.Interpreter?.CancelAll();
            factoryView?.Refresh();
            GetComponent<MachineInspectorPresenter>()?.RefreshLifecycleGate();
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
            Require(developmentCatalog, nameof(developmentCatalog), missing);
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
            public DateTimeOffset GetUtcNow()
            {
#if UNITY_EDITOR
                if (testOverrides != null)
                {
                    return testOverrides.ReadUtcNow();
                }
#endif
                return DateTimeOffset.UtcNow;
            }
        }

#if UNITY_EDITOR
        private sealed class CompositionTestOverrides
        {
            public CompositionTestOverrides(
                Func<DefinitionCatalog, ICheckpointStore> createCheckpointStore,
                Func<DateTimeOffset> readUtcNow,
                Func<double> readCumulativeActiveSeconds)
            {
                CreateCheckpointStore = createCheckpointStore;
                ReadUtcNow = readUtcNow;
                ReadCumulativeActiveSeconds = readCumulativeActiveSeconds;
            }

            public Func<DefinitionCatalog, ICheckpointStore> CreateCheckpointStore { get; }
            public Func<DateTimeOffset> ReadUtcNow { get; }
            public Func<double> ReadCumulativeActiveSeconds { get; }
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
