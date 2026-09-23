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
            ConfirmButton.SetEnabled(false);
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
            ConfirmButton.SetEnabled(canConfirm);
            feedbackLabel.text = feedback ?? string.Empty;
            SetStateClasses(canConfirm ? FactoryHudState.Normal : FactoryHudState.Disabled);
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
            SetStateClasses(state);
            var enabled = state != FactoryHudState.Disabled && state != FactoryHudState.Error;
            BuildButton.SetEnabled(enabled);
            if (!enabled)
            {
                ConfirmButton.SetEnabled(false);
            }
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
