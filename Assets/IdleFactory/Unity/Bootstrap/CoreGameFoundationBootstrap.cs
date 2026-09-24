using System;
using System.Collections.Generic;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Input;
using IdleFactory.Unity.Presentation;
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

        private PlacementCommands commands;
        private DefinitionCatalog catalog;
        private GridWorldMapper mapper;
        private Rect observedSafeArea;
        private int observedWidth;
        private int observedHeight;
        private ScreenOrientation observedOrientation;
        private bool initialized;

        public FactorySession Session { get; private set; }
        public FactoryView FactoryView => factoryView;
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
                var sessionResult = DevelopmentSessionFactory.CreateRunning(
                    emptyState,
                    catalog,
                    new DevelopmentUtcClock(),
                    new NoOpDevelopmentCheckpointStore());
                if (!sessionResult.Succeeded)
                {
                    return DisableWithError(sessionResult.Message);
                }

                Session = sessionResult.Value;
                commands = new PlacementCommands(Session, new GuidBuildingInstanceIdSource());
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
                    ScreenToWorld);

                navigationInput.Configure(inputActions, IsOverRuntimeUi);
                navigationInput.IntentRaised += RouteNavigationIntent;
                hud.ZoomRequested += cameraController.ZoomFromVisibleControl;
                observedOrientation = Screen.orientation;
                initialized = true;
                ApplySafeAreaLayout(true);
                hud.SetState(FactoryHudState.Empty, "Development-only D001 factory. Select Build to place a machine.");
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
                navigationInput?.Interpreter?.CancelAll();
                placementController?.Cancel();
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

        private void OnDestroy()
        {
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
            public DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
        }

        private sealed class NoOpDevelopmentCheckpointStore : ICheckpointStore
        {
            private const string Message = "Persistence is not enabled in the US1 development scene.";

            public CheckpointReadResult ReadActive() => CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, Message);
            public CheckpointReadResult ReadPrevious() => CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, Message);
            public CheckpointReadResult Read(CheckpointId checkpointId) => CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, Message);
            public CommandResult<CheckpointId> Promote(Checkpoint checkpoint) =>
                CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, Message);
            public CommandResult<CheckpointId> RecoverPrevious(Checkpoint recoveryCandidate) =>
                CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, Message);
        }
    }
}
