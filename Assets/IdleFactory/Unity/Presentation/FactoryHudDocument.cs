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

    /// <summary>Programmatic UI Toolkit HUD. Scene/bootstrap ownership remains with T017.</summary>
    public sealed class FactoryHudDocument : MonoBehaviour
    {
        private const float MinimumTarget = 44f;
        private VisualElement root;
        private VisualElement actionRow;
        private VisualElement informationPanel;
        private Label modeLabel;
        private Label selectionLabel;
        private Label feedbackLabel;
        private Label statusLabel;

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
        public FactoryHudState State { get; private set; }
        public string FeedbackText => feedbackLabel?.text ?? string.Empty;
        public string SelectionText => selectionLabel?.text ?? string.Empty;

        public void Build()
        {
            DisposeUi();
            root = new VisualElement { name = "factory-hud" };
            root.focusable = false;
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.right = 0;
            root.style.top = 0;
            root.style.bottom = 0;
            root.style.flexDirection = FlexDirection.Column;
            root.style.justifyContent = Justify.SpaceBetween;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            informationPanel = new VisualElement { name = "information-panel" };
            informationPanel.style.maxWidth = 420;
            modeLabel = Label("mode", "Browse mode");
            selectionLabel = Label("selection", "No building selected. Tap a building to inspect it.");
            feedbackLabel = Label("feedback", "Ready.");
            statusLabel = Label("status", "Development placeholder UI");
            informationPanel.Add(modeLabel);
            informationPanel.Add(selectionLabel);
            informationPanel.Add(feedbackLabel);
            informationPanel.Add(statusLabel);
            root.Add(informationPanel);

            actionRow = new VisualElement { name = "actions" };
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.flexWrap = Wrap.Wrap;
            BuildButton = Button("build", "Build", () => BuildRequested?.Invoke());
            ConfirmButton = Button("confirm", "Confirm placement", () => ConfirmRequested?.Invoke());
            CancelButton = Button("cancel", "Cancel placement", () => CancelRequested?.Invoke());
            ZoomOutButton = Button("zoom-out", "Zoom -", () => ZoomRequested?.Invoke(-1));
            ZoomInButton = Button("zoom-in", "Zoom +", () => ZoomRequested?.Invoke(1));
            actionRow.Add(BuildButton);
            actionRow.Add(ConfirmButton);
            actionRow.Add(CancelButton);
            actionRow.Add(ZoomOutButton);
            actionRow.Add(ZoomInButton);
            root.Add(actionRow);

            var document = GetComponent<UIDocument>();
            document?.rootVisualElement.Add(root);
            SetBrowseMode();
            SetState(FactoryHudState.Empty, "Factory is empty or nothing is selected.");
        }

        public void ApplyLayout(float width, float height, Rect safeArea)
        {
            if (root == null || width <= 0f || height <= 0f)
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

            root.style.paddingLeft = Mathf.Max(8f, safeArea.xMin);
            root.style.paddingRight = Mathf.Max(8f, width - safeArea.xMax);
            root.style.paddingBottom = Mathf.Max(8f, safeArea.yMin);
            root.style.paddingTop = Mathf.Max(8f, height - safeArea.yMax);
            var wide = Layout == FactoryHudLayout.Desktop || Layout == FactoryHudLayout.TabletLandscape;
            root.style.flexDirection = wide ? FlexDirection.Row : FlexDirection.Column;
            actionRow.style.alignSelf = wide ? Align.FlexEnd : Align.Stretch;
            informationPanel.style.maxWidth = wide ? 420f : width;
        }

        public void SetBrowseMode()
        {
            EnsureBuilt();
            modeLabel.text = "Browse mode";
            BuildButton.style.display = DisplayStyle.Flex;
            ConfirmButton.style.display = DisplayStyle.None;
            CancelButton.style.display = DisplayStyle.None;
            ConfirmButton.SetEnabled(false);
        }

        public void SetPlacementMode(bool canConfirm, string feedback)
        {
            EnsureBuilt();
            modeLabel.text = "Placement preview mode";
            BuildButton.style.display = DisplayStyle.None;
            ConfirmButton.style.display = DisplayStyle.Flex;
            CancelButton.style.display = DisplayStyle.Flex;
            ConfirmButton.SetEnabled(canConfirm);
            feedbackLabel.text = feedback ?? string.Empty;
            State = canConfirm ? FactoryHudState.Normal : FactoryHudState.Disabled;
        }

        public void ShowSelection(SelectionResolution resolution)
        {
            EnsureBuilt();
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
            EnsureBuilt();
            State = state;
            statusLabel.text = message ?? string.Empty;
            feedbackLabel.text = message ?? string.Empty;
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

        private void EnsureBuilt()
        {
            if (root == null)
            {
                Build();
            }
        }

        private static Label Label(string name, string text)
        {
            var label = new Label(text) { name = name };
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static Button Button(string name, string text, Action clicked)
        {
            var button = new Button(clicked) { name = name, text = text, focusable = true };
            button.style.minWidth = MinimumTarget;
            button.style.minHeight = MinimumTarget;
            button.style.marginRight = 4;
            button.style.marginTop = 4;
            return button;
        }

        private void OnDestroy()
        {
            DisposeUi();
        }

        private void DisposeUi()
        {
            root?.RemoveFromHierarchy();
            root = null;
        }
    }
}
