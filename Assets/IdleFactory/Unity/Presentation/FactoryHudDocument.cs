using System;
using IdleFactory.Core.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.Presentation
{
    public enum FactoryHudLayout
    {
        CompactPortrait,
        CompactLandscape,
        TabletPortrait,
        TabletLandscape,
        Desktop,
    }

    public enum FactoryHudState
    {
        Normal,
        Empty,
        Disabled,
        Error,
    }

    /// <summary>Clones and binds the authoritative Core Game Foundation UI Toolkit assets.</summary>
    public sealed class FactoryHudDocument : MonoBehaviour
    {
        private static readonly string[] LayoutClasses =
        {
            "factory-hud--compact-portrait",
            "factory-hud--compact-landscape",
            "factory-hud--tablet-portrait",
            "factory-hud--tablet-landscape",
            "factory-hud--desktop",
        };

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset visualTree;
        [SerializeField] private StyleSheet styleSheet;

        private VisualElement root;
        private Label modeLabel;
        private Label selectionLabel;
        private Label feedbackLabel;
        private Label statusLabel;
        private bool callbacksBound;
        private bool gameplayAvailable = true;
        private bool placementCanConfirm;

        public event Action BuildRequested;
        public event Action ConfirmRequested;
        public event Action CancelRequested;
        public event Action<int> ZoomRequested;

        public Button BuildButton { get; private set; }
        public Button ConfirmButton { get; private set; }
        public Button CancelButton { get; private set; }
        public Button ZoomInButton { get; private set; }
        public Button ZoomOutButton { get; private set; }
        public VisualElement Root => root;
        public FactoryHudLayout Layout { get; private set; }
        public FactoryHudState State { get; private set; } = FactoryHudState.Error;
        public string Error { get; private set; } = "HUD assets have not been configured.";
        public string FeedbackText => feedbackLabel?.text ?? Error;
        public string SelectionText => selectionLabel?.text ?? string.Empty;
        public bool IsReady => string.IsNullOrEmpty(Error) && root != null;

        public bool Configure(UIDocument document, VisualTreeAsset tree, StyleSheet styles)
        {
            uiDocument = document;
            visualTree = tree;
            styleSheet = styles;
            return Build();
        }

        public bool Build()
        {
            DisposeUi();
            if (uiDocument == null || visualTree == null || styleSheet == null)
            {
                return Fail("HUD requires explicit UIDocument, UXML, and USS references.");
            }

            var documentRoot = uiDocument.rootVisualElement;
            if (documentRoot == null)
            {
                return Fail("HUD UIDocument root is unavailable.");
            }

            // The UIDocument root is not sized by the panel on its own (it resolves to zero height),
            // so pin it to the panel. Otherwise the absolutely positioned HUD collapses to its padding and
            // every HUD card is drawn on top of the others (T053 / F2).
            documentRoot.style.position = Position.Absolute;
            documentRoot.style.left = 0f;
            documentRoot.style.top = 0f;
            documentRoot.style.right = 0f;
            documentRoot.style.bottom = 0f;

            visualTree.CloneTree(documentRoot);
            root = documentRoot.Q<VisualElement>("factory-hud");
            modeLabel = root?.Q<Label>("mode");
            selectionLabel = root?.Q<Label>("selection");
            feedbackLabel = root?.Q<Label>("feedback");
            statusLabel = root?.Q<Label>("status");
            BuildButton = root?.Q<Button>("build");
            ConfirmButton = root?.Q<Button>("confirm");
            CancelButton = root?.Q<Button>("cancel");
            ZoomOutButton = root?.Q<Button>("zoom-out");
            ZoomInButton = root?.Q<Button>("zoom-in");

            if (root == null || modeLabel == null || selectionLabel == null || feedbackLabel == null ||
                statusLabel == null || BuildButton == null || ConfirmButton == null || CancelButton == null ||
                ZoomOutButton == null || ZoomInButton == null)
            {
                return Fail("HUD UXML is malformed or missing required named elements.");
            }

            // The HUD spans the whole viewport; only authored child regions should own pointer input.
            // Keep the full-screen root transparent while allowing opaque panels and disabled controls
            // to retain ownership when a gesture begins over them.
            root.pickingMode = PickingMode.Ignore;

            root.styleSheets.Add(styleSheet);
            BindCallbacks();
            Error = string.Empty;
            SetBrowseMode();
            SetState(FactoryHudState.Empty, "Factory is empty or nothing is selected.");
            return true;
        }

        /// <summary>
        /// Applies the responsive layout from screen-pixel inputs (<see cref="Screen.width"/>,
        /// <see cref="Screen.height"/> and <see cref="Screen.safeArea"/>). The panel renders in points, so
        /// pixels are converted with the panel's resolved pixels-per-point before the screen class and
        /// safe-area insets are chosen (for example 4.375 px per point when Screen.dpi reports 420).
        /// </summary>
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

        /// <summary>Applies the responsive layout from panel-point viewport and safe-area values.</summary>
        public void ApplyLayout(float width, float height, Rect safeArea)
        {
            if (!IsReady || width <= 0f || height <= 0f)
            {
                return;
            }

            var landscape = width > height;
            if (width >= 1100f)
            {
                Layout = FactoryHudLayout.Desktop;
            }
            else if (Mathf.Min(width, height) >= 700f)
            {
                Layout = landscape ? FactoryHudLayout.TabletLandscape : FactoryHudLayout.TabletPortrait;
            }
            else
            {
                Layout = landscape ? FactoryHudLayout.CompactLandscape : FactoryHudLayout.CompactPortrait;
            }

            foreach (var className in LayoutClasses)
            {
                root.RemoveFromClassList(className);
            }

            root.AddToClassList(LayoutClass(Layout));
            root.style.paddingLeft = Mathf.Max(8f, safeArea.xMin);
            root.style.paddingRight = Mathf.Max(8f, width - safeArea.xMax);
            root.style.paddingBottom = Mathf.Max(8f, safeArea.yMin);
            root.style.paddingTop = Mathf.Max(8f, height - safeArea.yMax);
        }

        public void SetBrowseMode()
        {
            if (!EnsureReady())
            {
                return;
            }

            modeLabel.text = "Browse mode";
            BuildButton.style.display = DisplayStyle.Flex;
            ConfirmButton.style.display = DisplayStyle.None;
            CancelButton.style.display = DisplayStyle.None;
            placementCanConfirm = false;
            ApplyGameplayAvailability();
        }

        public void SetPlacementMode(bool canConfirm, string feedback)
        {
            if (!EnsureReady())
            {
                return;
            }

            modeLabel.text = "Placement preview mode";
            BuildButton.style.display = DisplayStyle.None;
            ConfirmButton.style.display = DisplayStyle.Flex;
            CancelButton.style.display = DisplayStyle.Flex;
            placementCanConfirm = canConfirm;
            feedbackLabel.text = feedback ?? string.Empty;
            HideRepeatedFeedback();
            SetStateClasses(canConfirm ? FactoryHudState.Normal : FactoryHudState.Disabled);
            ApplyGameplayAvailability();
        }

        public void ShowSelection(SelectionResolution resolution)
        {
            if (!EnsureReady())
            {
                return;
            }

            if (resolution == null || resolution.Kind == SelectionResolutionKind.Empty)
            {
                selectionLabel.text = "No building selected. Tap a building to inspect it.";
                return;
            }

            if (resolution.Kind == SelectionResolutionKind.Missing)
            {
                selectionLabel.text = "Selected building is no longer available.";
                return;
            }

            var inspection = resolution.Inspection;
            selectionLabel.text =
                $"Selected {inspection.DefinitionId.Value} ({inspection.InstanceId.Value}) | " +
                $"{(inspection.IsActive ? "Active" : "Inactive")} | " +
                $"Progress {inspection.ProgressTicks}/{inspection.DurationTicks} | " +
                $"Blockers {inspection.Blockers.Count}";
        }

        public void SetState(FactoryHudState state, string message)
        {
            if (!EnsureReady())
            {
                return;
            }

            statusLabel.text = message ?? string.Empty;
            feedbackLabel.text = message ?? string.Empty;
            HideRepeatedFeedback();
            SetStateClasses(state);
            ApplyGameplayAvailability();
        }

        /// <summary>Shows feedback only when it adds text beyond the status line, so a state message is never drawn twice.</summary>
        private void HideRepeatedFeedback()
        {
            var repeatsStatus = string.IsNullOrEmpty(feedbackLabel.text) ||
                string.Equals(feedbackLabel.text, statusLabel.text, StringComparison.Ordinal);
            feedbackLabel.style.display = repeatsStatus ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>Gates gameplay controls while lifecycle reconciliation is unavailable.</summary>
        public void SetGameplayAvailability(bool available)
        {
            gameplayAvailable = available;
            ApplyGameplayAvailability();
        }

        public void Recover(string message)
        {
            SetState(FactoryHudState.Normal, message);
            SetBrowseMode();
        }

        private bool EnsureReady()
        {
            if (IsReady)
            {
                return true;
            }

            State = FactoryHudState.Error;
            return false;
        }

        private bool Fail(string message)
        {
            DisposeUi();
            Error = message;
            State = FactoryHudState.Error;
            if (uiDocument != null)
            {
                root = new VisualElement { name = "factory-hud-error" };
                feedbackLabel = new Label(message) { name = "feedback" };
                statusLabel = new Label("HUD unavailable") { name = "status" };
                root.SetEnabled(false);
                root.Add(feedbackLabel);
                root.Add(statusLabel);
                uiDocument.rootVisualElement.Add(root);
            }

            return false;
        }

        private void SetStateClasses(FactoryHudState state)
        {
            State = state;
            root.EnableInClassList("factory-hud--disabled", state == FactoryHudState.Disabled);
            root.EnableInClassList("factory-hud--error", state == FactoryHudState.Error);
            root.EnableInClassList("factory-hud--empty", state == FactoryHudState.Empty);
        }

        private void ApplyGameplayAvailability()
        {
            if (BuildButton == null || ConfirmButton == null)
            {
                return;
            }

            var stateAllowsGameplay = State != FactoryHudState.Disabled && State != FactoryHudState.Error;
            BuildButton.SetEnabled(gameplayAvailable && stateAllowsGameplay);
            ConfirmButton.SetEnabled(gameplayAvailable && placementCanConfirm && stateAllowsGameplay);
        }

        private void BindCallbacks()
        {
            if (callbacksBound)
            {
                return;
            }

            BuildButton.clicked += RaiseBuild;
            ConfirmButton.clicked += RaiseConfirm;
            CancelButton.clicked += RaiseCancel;
            ZoomOutButton.clicked += RaiseZoomOut;
            ZoomInButton.clicked += RaiseZoomIn;
            callbacksBound = true;
        }

        private void UnbindCallbacks()
        {
            if (!callbacksBound)
            {
                return;
            }

            BuildButton.clicked -= RaiseBuild;
            ConfirmButton.clicked -= RaiseConfirm;
            CancelButton.clicked -= RaiseCancel;
            ZoomOutButton.clicked -= RaiseZoomOut;
            ZoomInButton.clicked -= RaiseZoomIn;
            callbacksBound = false;
        }

        private void RaiseBuild() => BuildRequested?.Invoke();
        private void RaiseConfirm() => ConfirmRequested?.Invoke();
        private void RaiseCancel() => CancelRequested?.Invoke();
        private void RaiseZoomOut() => ZoomRequested?.Invoke(-1);
        private void RaiseZoomIn() => ZoomRequested?.Invoke(1);

        private void OnDestroy()
        {
            DisposeUi();
        }

        private void DisposeUi()
        {
            UnbindCallbacks();
            root?.RemoveFromHierarchy();
            root = null;
            modeLabel = null;
            selectionLabel = null;
            feedbackLabel = null;
            statusLabel = null;
            BuildButton = null;
            ConfirmButton = null;
            CancelButton = null;
            ZoomOutButton = null;
            ZoomInButton = null;
        }

        private static string LayoutClass(FactoryHudLayout layout)
        {
            switch (layout)
            {
                case FactoryHudLayout.CompactLandscape:
                    return "factory-hud--compact-landscape";
                case FactoryHudLayout.TabletPortrait:
                    return "factory-hud--tablet-portrait";
                case FactoryHudLayout.TabletLandscape:
                    return "factory-hud--tablet-landscape";
                case FactoryHudLayout.Desktop:
                    return "factory-hud--desktop";
                default:
                    return "factory-hud--compact-portrait";
            }
        }
    }
}
