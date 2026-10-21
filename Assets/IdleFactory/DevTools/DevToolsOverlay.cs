using System;
using IdleFactory.Unity.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace IdleFactory.DevTools
{
    /// <summary>
    /// The development overlay (FR-026, ui-surfaces-f005 rows <c>dev-timescale</c> and <c>dev-accelerated-marker</c>): five
    /// keyboard-operable buttons (x1, x10, x60, x600, Max) and a persistent "ACCELERATED xN" marker while N is not 1. It is
    /// composed in code at runtime so no scene or prefab changes, and it exists only in builds with <c>IDLEFACTORY_DEV</c>.
    /// Placement (T340): the overlay sits at the top left, directly below the top strip of the shell (the HUD) and inside the safe area, so it
    /// never covers the ore vein (bottom right), the zoom buttons (bottom left), the tab bar or the side panel; it sits below the detail line,
    /// the notice line and the world hint while any of them is shown (T344), so the transient notice stays readable, and it only covers
    /// the world, which is not a control. It follows the real bottom edge of the strip (text scale, wrapped HUD) every
    /// <see cref="PlacementSeconds"/>. Keyboard: Tab reaches the five buttons in order, Left and Right (also Home and End) move between them,
    /// Space and Enter select, and 1 to 5 select from anywhere. The selected factor is shown in bold with a border, not by colour alone.
    /// </summary>
    public sealed class DevToolsOverlay : MonoBehaviour
    {
        public const string RootName = "dev-timescale";
        public const string MarkerName = "dev-accelerated-marker";
        private const float TargetSize = 44f;

        /// <summary>Gap to the safe-area edge and to the HUD strip, in overlay points.</summary>
        public const float EdgeMargin = 4f;

        /// <summary>The strip height assumed (in overlay points) when no shell is live to measure.</summary>
        public const float FallbackHudHeight = 56f;

        /// <summary>The gap between the "ACCELERATED xN" marker and the time-scale row, in overlay points (T344).</summary>
        public const float MarkerGap = 4f;

        /// <summary>How often <see cref="RefreshPlacement"/> runs on its own.</summary>
        public const float PlacementSeconds = 0.5f;

        private float nextPlacementTime;
        private VisualElement visualRoot;
        private VisualElement row;

        private DevTimeScale timeScale;
        private UIDocument document;
        private PanelSettings panelSettings;
        private VisualElement root;
        private Label marker;
        private readonly Button[] buttons = new Button[DevTimeScale.MaxIndex + 1];

        public DevTimeScale TimeScale => timeScale;

        public VisualElement Root => root;

        public Label Marker => marker;

        /// <summary>The row of the five factor buttons (<c>dev-timescale-row</c>).</summary>
        public VisualElement Row => row;

        /// <summary>When false the overlay never places itself from <c>Screen</c> (tests drive <see cref="RefreshPlacement"/> with a declared screen).</summary>
        public bool AutoPlacement { get; set; } = true;

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
            visualRoot = visual;
            visual.pickingMode = PickingMode.Ignore;
            visual.style.position = Position.Absolute;
            visual.style.left = 0;
            visual.style.right = 0;
            visual.style.top = 0;
            visual.style.bottom = 0;

            root = new VisualElement { name = RootName };
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = EdgeMargin;
            root.style.right = EdgeMargin;
            root.style.top = FallbackHudHeight + EdgeMargin;
            root.style.flexDirection = FlexDirection.Column;
            root.style.alignItems = Align.FlexStart;
            visual.Add(root);

            marker = new Label { name = MarkerName };
            marker.pickingMode = PickingMode.Ignore;
            marker.style.backgroundColor = new Color(0.85f, 0.15f, 0.1f, 0.92f);
            marker.style.color = Color.white;
            marker.style.fontSize = 14;
            marker.style.unityTextAlign = TextAnchor.MiddleCenter;
            marker.style.marginBottom = 0;
            marker.style.unityFontStyleAndWeight = FontStyle.Bold;
            marker.style.paddingLeft = 8;
            marker.style.paddingRight = 8;
            marker.style.paddingTop = 2;
            marker.style.paddingBottom = 2;
            root.Add(marker);

            row = new VisualElement { name = "dev-timescale-row", tooltip = "Time scale, keys 1 to 5" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = MarkerGap;
            row.style.backgroundColor = new Color(0f, 0f, 0f, 0.7f);
            row.RegisterCallback<KeyDownEvent>(OnRowKeyDown);
            root.Add(row);

            for (var i = 0; i <= DevTimeScale.MaxIndex; i++)
            {
                var index = i;
                var button = new Button(() => timeScale.Select(index)) { name = RootName + "-" + DevTimeScale.LabelOf(i) };
                button.text = DevTimeScale.LabelOf(i);
                button.focusable = true;
                button.tabIndex = 0;
                button.style.minWidth = TargetSize;
                button.style.minHeight = TargetSize;
                button.style.fontSize = 16;
                button.style.unityTextAlign = TextAnchor.MiddleCenter;
                button.style.marginLeft = 2;
                button.style.marginRight = 2;
                button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 0;
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
            if (AutoPlacement && Time.unscaledTime >= nextPlacementTime)
            {
                nextPlacementTime = Time.unscaledTime + PlacementSeconds;
                RefreshPlacement(Screen.safeArea, Screen.width, Screen.height);
            }

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

        /// <summary>
        /// Places the overlay at the top left of the safe area, directly below the top strip of the shell (its real bottom edge when a shell is
        /// live, otherwise <see cref="FallbackHudHeight"/> below the top inset). The safe area is in screen pixels with a bottom-left origin, as
        /// <c>Screen.safeArea</c> reports it; the result is in the own panel points of the overlay.
        /// </summary>
        public void RefreshPlacement(Rect safeAreaPx, float screenWidthPx, float screenHeightPx)
        {
            if (root == null)
            {
                return;
            }

            var pixelsPerPoint = visualRoot != null && visualRoot.panel != null && visualRoot.panel.scaledPixelsPerPoint > 0f ? visualRoot.panel.scaledPixelsPerPoint : 1f;
            var left = Mathf.Max(0f, safeAreaPx.xMin) / pixelsPerPoint;
            var right = Mathf.Max(0f, screenWidthPx - safeAreaPx.xMax) / pixelsPerPoint;
            var topInset = Mathf.Max(0f, screenHeightPx - safeAreaPx.yMax) / pixelsPerPoint;
            var hudBottom = topInset + FallbackHudHeight;
            var stripBottomPx = ShellStripBottomPixels();
            if (!float.IsNaN(stripBottomPx))
            {
                hudBottom = Mathf.Max(topInset, stripBottomPx / pixelsPerPoint);
            }

            // The detail line, the transient notice and the world hint sit under the strip; stay below whichever is shown (T344).
            var barsBottomPx = ShellBarsBottomPixels();
            if (!float.IsNaN(barsBottomPx))
            {
                hudBottom = Mathf.Max(hudBottom, barsBottomPx / pixelsPerPoint);
            }

            root.style.left = left + EdgeMargin;
            root.style.right = right + EdgeMargin;
            root.style.top = hudBottom + EdgeMargin;
        }

        // The bottom edge of the top strip of the live shell in screen pixels from the top, or NaN when there is no laid-out shell.
        private static float ShellStripBottomPixels()
        {
            var shell = FindFirstObjectByType<RunShellDocument>();
            var strip = shell == null ? null : shell.TopStrip;
            if (strip == null || strip.panel == null)
            {
                return float.NaN;
            }

            var bounds = strip.worldBound;
            if (float.IsNaN(bounds.yMax) || bounds.height <= 0f)
            {
                return float.NaN;
            }

            return bounds.yMax * strip.panel.scaledPixelsPerPoint;
        }

        // The lowest bottom edge, in screen pixels from the top, of the shown detail line, notice and world hint of the live shell, or NaN when none is shown.
        private static float ShellBarsBottomPixels()
        {
            var shell = FindFirstObjectByType<RunShellDocument>();
            if (shell == null)
            {
                return float.NaN;
            }

            var bottom = float.NaN;
            foreach (var bar in new[] { shell.DetailBar, shell.NoticeBar, shell.WorldHint })
            {
                if (bar == null || bar.panel == null || bar.resolvedStyle.display == DisplayStyle.None)
                {
                    continue;
                }

                var bounds = bar.worldBound;
                if (float.IsNaN(bounds.yMax) || bounds.height <= 0f)
                {
                    continue;
                }

                var px = bounds.yMax * bar.panel.scaledPixelsPerPoint;
                bottom = float.IsNaN(bottom) ? px : Mathf.Max(bottom, px);
            }

            return bottom;
        }

        private void OnRowKeyDown(KeyDownEvent evt)
        {
            if (MoveFocus(evt))
            {
                return;
            }

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

        // Left and Right move focus along the five buttons, Home and End jump to the ends (no wrap, so the order stays predictable).
        private bool MoveFocus(KeyDownEvent evt)
        {
            int target;
            var focused = FocusedIndex();
            switch (evt.keyCode)
            {
                case KeyCode.LeftArrow: target = Mathf.Max(0, (focused < 0 ? 0 : focused) - 1); break;
                case KeyCode.RightArrow: target = Mathf.Min(DevTimeScale.MaxIndex, (focused < 0 ? -1 : focused) + 1); break;
                case KeyCode.Home: target = 0; break;
                case KeyCode.End: target = DevTimeScale.MaxIndex; break;
                default: return false;
            }

            buttons[target].Focus();
            evt.StopPropagation();
            return true;
        }

        private int FocusedIndex()
        {
            var focused = row != null && row.panel != null && row.panel.focusController != null ? row.panel.focusController.focusedElement : null;
            for (var i = 0; i < buttons.Length; i++)
            {
                if (ReferenceEquals(buttons[i], focused))
                {
                    return i;
                }
            }

            return -1;
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
                // Not by colour alone: the selected factor is bold with a white border.
                buttons[i].style.unityFontStyleAndWeight = selected ? FontStyle.Bold : FontStyle.Normal;
                var border = selected ? 2f : 0f;
                buttons[i].style.borderTopWidth = buttons[i].style.borderBottomWidth = buttons[i].style.borderLeftWidth = buttons[i].style.borderRightWidth = border;
                buttons[i].style.borderTopColor = buttons[i].style.borderBottomColor = buttons[i].style.borderLeftColor = buttons[i].style.borderRightColor = Color.white;
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
