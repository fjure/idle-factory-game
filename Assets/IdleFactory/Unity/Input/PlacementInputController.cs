using System;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IdleFactory.Unity.Input
{
    public enum PlacementInteractionMode
    {
        Browse,
        PlacementPreview,
    }

    public sealed class PlacementInputController : MonoBehaviour
    {
        private PlacementCommands commands;
        private DefinitionCatalog definitions;
        private FactoryView factoryView;
        private PlacementPreviewView previewView;
        private FactoryHudDocument hud;
        private NavigationInputAdapter navigation;
        private GridWorldMapper mapper;
        private Func<Vector2, Vector2> screenToWorld;
        private BuildingDefinitionId placementDefinitionId;
        private GridPosition anchor;
        private int orientationQuarterTurns;
        private PlacementPreview preview;
        private bool confirmInProgress;
        private bool subscribed;

        public event Action<PlacementInteractionMode> ModeChanged;
        public event Action<CommandResult<PlacementCommandResult>> PlacementCompleted;

        public PlacementInteractionMode Mode { get; private set; } = PlacementInteractionMode.Browse;
        public bool CanConfirm => Mode == PlacementInteractionMode.PlacementPreview && preview?.IsValid == true && !confirmInProgress;
        public GridPosition PreviewAnchor => anchor;
        public string Feedback => previewView?.Feedback ?? string.Empty;

        public void Configure(
            PlacementCommands placementCommands,
            DefinitionCatalog definitionCatalog,
            FactoryView view,
            PlacementPreviewView placementPreview,
            FactoryHudDocument hudDocument,
            GridWorldMapper gridMapper,
            Func<Vector2, Vector2> screenPointToWorld,
            NavigationInputAdapter navigationInput = null)
        {
            Unsubscribe();
            commands = placementCommands ?? throw new ArgumentNullException(nameof(placementCommands));
            definitions = definitionCatalog ?? throw new ArgumentNullException(nameof(definitionCatalog));
            factoryView = view ?? throw new ArgumentNullException(nameof(view));
            previewView = placementPreview ?? throw new ArgumentNullException(nameof(placementPreview));
            hud = hudDocument ?? throw new ArgumentNullException(nameof(hudDocument));
            mapper = gridMapper ?? throw new ArgumentNullException(nameof(gridMapper));
            screenToWorld = screenPointToWorld ?? throw new ArgumentNullException(nameof(screenPointToWorld));
            navigation = navigationInput;

            Subscribe();
            EnterBrowse();
            if (!string.IsNullOrEmpty(factoryView.Error))
            {
                OnViewError(factoryView.Error);
            }
        }

        public bool StartPlacement(BuildingDefinitionId definitionId, GridPosition initialAnchor, int orientation = 0)
        {
            if (commands == null || !definitions.Buildings.TryGetValue(definitionId, out _))
            {
                hud?.SetState(FactoryHudState.Error, "Building definition is unavailable. Choose another building.");
                return false;
            }

            placementDefinitionId = definitionId;
            anchor = initialAnchor;
            orientationQuarterTurns = orientation;
            Mode = PlacementInteractionMode.PlacementPreview;
            UpdatePreview();
            ModeChanged?.Invoke(Mode);
            return true;
        }

        public void HandleNavigationIntent(NavigationIntent intent)
        {
            if (intent.Kind != NavigationIntentKind.WorldTap)
            {
                return;
            }

            var cell = mapper.WorldToGrid(screenToWorld(intent.Position));
            if (Mode == PlacementInteractionMode.Browse)
            {
                factoryView.SelectAt(cell);
            }
            else
            {
                anchor = cell;
                UpdatePreview();
            }
        }

        public void Confirm()
        {
            if (!CanConfirm)
            {
                hud?.SetPlacementMode(false, previewView?.Feedback ?? "Placement is not valid.");
                return;
            }

            preview = commands.GetPlacementPreview(placementDefinitionId, anchor, orientationQuarterTurns);
            if (!preview.IsValid)
            {
                previewView.Show(definitions.Resolve(placementDefinitionId), anchor, orientationQuarterTurns, preview);
                hud.SetPlacementMode(false, previewView.Feedback);
                return;
            }

            confirmInProgress = true;
            var result = commands.PlaceBuilding(placementDefinitionId, anchor, orientationQuarterTurns);
            confirmInProgress = false;
            PlacementCompleted?.Invoke(result);
            if (!result.Succeeded)
            {
                UpdatePreview();
                hud.SetPlacementMode(false, string.IsNullOrEmpty(result.Message) ? "Placement failed." : result.Message);
                return;
            }

            previewView.Hide("Placement confirmed.");
            Mode = PlacementInteractionMode.Browse;
            factoryView.Select(result.Value.InstanceId);
            hud.SetBrowseMode();
            hud.SetState(FactoryHudState.Normal, "Building placed and selected.");
            ModeChanged?.Invoke(Mode);
        }

        public void Cancel()
        {
            if (Mode != PlacementInteractionMode.PlacementPreview)
            {
                return;
            }

            previewView.Hide("Placement cancelled. No changes were made.");
            EnterBrowse();
        }

        private void Update()
        {
            if (Mode == PlacementInteractionMode.PlacementPreview &&
                Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cancel();
            }
        }

        private void UpdatePreview()
        {
            var definition = definitions.Resolve(placementDefinitionId);
            preview = commands.GetPlacementPreview(placementDefinitionId, anchor, orientationQuarterTurns);
            previewView.Show(definition, anchor, orientationQuarterTurns, preview);
            hud.SetPlacementMode(preview.IsValid, previewView.Feedback);
        }

        private void EnterBrowse()
        {
            Mode = PlacementInteractionMode.Browse;
            preview = null;
            confirmInProgress = false;
            previewView?.Hide();
            hud?.SetBrowseMode();
            if (hud != null && commands != null)
            {
                var empty = commands.GetFactorySnapshot().Buildings.Count == 0;
                hud.SetState(
                    empty ? FactoryHudState.Empty : FactoryHudState.Normal,
                    empty ? "Factory is empty. Use Build to place a development machine." : "Browse and select a building.");
            }
            ModeChanged?.Invoke(Mode);
        }

        private void OnBuildRequested()
        {
            foreach (var definition in definitions.Buildings.Values)
            {
                StartPlacement(definition.Id, definitions.Buildings.Count > 0
                    ? commands.GetFactorySnapshot().Bounds.Origin
                    : new GridPosition(0, 0));
                return;
            }

            hud.SetState(FactoryHudState.Empty, "No development building definitions are available.");
        }

        private void OnZoomRequested(int direction)
        {
            navigation?.ZoomFromVisibleControl(direction);
        }

        private void OnViewError(string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                hud.SetState(FactoryHudState.Error, error);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnEnable()
        {
            if (commands != null)
            {
                Subscribe();
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (hud != null)
            {
                hud.BuildRequested -= OnBuildRequested;
                hud.ConfirmRequested -= Confirm;
                hud.CancelRequested -= Cancel;
                hud.ZoomRequested -= OnZoomRequested;
            }

            if (factoryView != null)
            {
                if (hud != null)
                {
                    factoryView.SelectionChanged -= hud.ShowSelection;
                }
                factoryView.ErrorChanged -= OnViewError;
            }

            if (navigation != null)
            {
                navigation.IntentRaised -= HandleNavigationIntent;
            }

            subscribed = false;
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            hud.BuildRequested += OnBuildRequested;
            hud.ConfirmRequested += Confirm;
            hud.CancelRequested += Cancel;
            hud.ZoomRequested += OnZoomRequested;
            factoryView.SelectionChanged += hud.ShowSelection;
            factoryView.ErrorChanged += OnViewError;
            if (navigation != null)
            {
                navigation.IntentRaised += HandleNavigationIntent;
            }

            subscribed = true;
        }
    }
}
