using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace IdleFactory.DevTools
{
    /// <summary>
    /// The development overlay (FR-026, ui-surfaces-f005 rows <c>dev-timescale</c> and <c>dev-accelerated-marker</c>): five
    /// keyboard-operable buttons (x1, x10, x60, x600, Max) and a persistent "ACCELERATED xN" marker while N is not 1. It is
    /// composed in code at runtime so no scene or prefab changes, and it exists only in builds with <c>IDLEFACTORY_DEV</c>.
    /// </summary>
    public sealed class DevToolsOverlay : MonoBehaviour
    {
        public const string RootName = "dev-timescale";
        public const string MarkerName = "dev-accelerated-marker";
        private const float TargetSize = 44f;

        private DevTimeScale timeScale;
        private UIDocument document;
        private PanelSettings panelSettings;
        private VisualElement root;
        private Label marker;
        private readonly Button[] buttons = new Button[DevTimeScale.MaxIndex + 1];

        public DevTimeScale TimeScale => timeScale;

        public VisualElement Root => root;

        public Label Marker => marker;

        public Button ButtonAt(int index) => buttons[index];

        /// <summary>The overlay's panel settings (carries a runtime-created theme style sheet, F7), or null before it is built.</summary>
        public PanelSettings Settings => panelSettings;

        /// <summary>The accessible name of the marker, "Accelerated, factor N" (ui-surfaces-f005), or empty when hidden.</summary>
        public string MarkerAccessibleName => markerAccessibleName;

        private string markerAccessibleName = string.Empty;

        public static DevToolsOverlay Create(DevTimeScale timeScale, GameObject host)
        {
            if (timeScale == null)
            {
                throw new ArgumentNullException(nameof(timeScale));
            }

            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            var overlay = host.AddComponent<DevToolsOverlay>();
            overlay.timeScale = timeScale;
            overlay.Build();
            return overlay;
        }

        private void Build()
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "DevToolsPanelSettings";
            panelSettings.sortingOrder = 10000f;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(360, 640);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            // F7 (batch-A Play-Tester): a PanelSettings without a theme style sheet logs "No Theme Style Sheet set to PanelSettings ...,
            // UI will not render properly" and the overlay never appeared in the Windows development player. The shell's authored
            // PanelSettings asset cannot be referenced from this define-gated assembly (no Resources or serialized reference, and the
            // asset is not part of it), so the overlay gets an empty runtime theme and carries every style it needs inline.
            panelSettings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();

            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var visual = document.rootVisualElement;
            visual.pickingMode = PickingMode.Ignore;
            visual.style.position = Position.Absolute;
            visual.style.left = 0;
            visual.style.right = 0;
            visual.style.top = 0;
            visual.style.bottom = 0;

            root = new VisualElement { name = RootName };
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.right = 0;
            root.style.bottom = 0;
            root.style.flexDirection = FlexDirection.Column;
            root.style.alignItems = Align.Center;
            root.style.paddingBottom = 8;
            visual.Add(root);

            marker = new Label { name = MarkerName };
            marker.pickingMode = PickingMode.Ignore;
            marker.style.backgroundColor = new Color(0.85f, 0.15f, 0.1f, 0.92f);
            marker.style.color = Color.white;
            marker.style.fontSize = 14;
            marker.style.unityTextAlign = TextAnchor.MiddleCenter;
            marker.style.marginBottom = 4;
            marker.style.unityFontStyleAndWeight = FontStyle.Bold;
            marker.style.paddingLeft = 8;
            marker.style.paddingRight = 8;
            marker.style.paddingTop = 2;
            marker.style.paddingBottom = 2;
            root.Add(marker);

            var row = new VisualElement { name = "dev-timescale-row" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.backgroundColor = new Color(0f, 0f, 0f, 0.7f);
            row.RegisterCallback<KeyDownEvent>(OnRowKeyDown);
            root.Add(row);

            for (var i = 0; i <= DevTimeScale.MaxIndex; i++)
            {
                var index = i;
                var button = new Button(() => timeScale.Select(index)) { name = RootName + "-" + DevTimeScale.LabelOf(i) };
                button.text = DevTimeScale.LabelOf(i);
                button.focusable = true;
                button.tabIndex = i + 1;
                button.style.minWidth = TargetSize;
                button.style.minHeight = TargetSize;
                button.style.fontSize = 16;
                button.style.unityTextAlign = TextAnchor.MiddleCenter;
                button.style.marginLeft = 2;
                button.style.marginRight = 2;
                row.Add(button);
                buttons[i] = button;
            }

            timeScale.Changed += Refresh;
            Refresh();
        }

        /// <summary>
        /// 1 to 5 (top row or keypad) select a factor from anywhere. The row handler below only sees keys while a button has focus,
        /// and nothing focuses the overlay in a player, so the keys did nothing (F7).
        /// </summary>
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || timeScale == null)
            {
                return;
            }

            for (var i = 0; i <= DevTimeScale.MaxIndex; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                {
                    timeScale.Select(i);
                    return;
                }
            }
        }

        private void OnRowKeyDown(KeyDownEvent evt)
        {
            var index = evt.keyCode - KeyCode.Alpha1;
            if (index < 0 || index > DevTimeScale.MaxIndex)
            {
                index = evt.keyCode - KeyCode.Keypad1;
            }

            if (index >= 0 && index <= DevTimeScale.MaxIndex)
            {
                timeScale.Select(index);
                evt.StopPropagation();
            }
        }

        private void Refresh()
        {
            if (marker == null)
            {
                return;
            }

            var factor = timeScale.DisplayFactor;
            marker.style.display = timeScale.IsAccelerated ? DisplayStyle.Flex : DisplayStyle.None;
            marker.text = "ACCELERATED ×" + factor;
            // UI Toolkit in Unity 6000.6 has no per-element accessibility properties, so the accessible name is kept as a
            // property and mirrored into the tooltip until F004 maps the shell to the platform accessibility hierarchy.
            markerAccessibleName = timeScale.IsAccelerated ? "Accelerated, factor " + factor : string.Empty;
            marker.tooltip = markerAccessibleName;
            for (var i = 0; i < buttons.Length; i++)
            {
                var selected = i == timeScale.SelectedIndex;
                buttons[i].tooltip = "Time scale " + DevTimeScale.LabelOf(i) + (selected ? ", selected" : string.Empty);
                buttons[i].style.backgroundColor = selected ? new Color(0.2f, 0.5f, 0.9f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f);
                buttons[i].style.color = Color.white;
            }
        }

        private void OnDestroy()
        {
            if (timeScale != null)
            {
                timeScale.Changed -= Refresh;
            }

            if (panelSettings != null)
            {
                if (panelSettings.themeStyleSheet != null)
                {
                    Destroy(panelSettings.themeStyleSheet);
                }

                Destroy(panelSettings);
            }
        }
    }
}
