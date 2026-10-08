using System;
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

    /// <summary>
    /// Clones and binds the start-up status HUD (<c>CoreGameFoundation.uxml</c>). It reports a start-up failure with its coded
    /// status text and owns the screen-class thresholds the run shell reuses. T119 retired its Feature 001 panels (placement
    /// actions, machine inspector, Save and return); the run shell owns every gameplay control.
    /// </summary>
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
        private Label feedbackLabel;
        private Label statusLabel;

        public VisualElement Root => root;
        public FactoryHudLayout Layout { get; private set; }
        public FactoryHudState State { get; private set; } = FactoryHudState.Error;
        public string Error { get; private set; } = "HUD assets have not been configured.";
        public string FeedbackText => feedbackLabel?.text ?? Error;
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
            feedbackLabel = root?.Q<Label>("feedback");
            statusLabel = root?.Q<Label>("status");

            if (root == null || feedbackLabel == null || statusLabel == null)
            {
                return Fail("HUD UXML is malformed or missing required named elements.");
            }

            // The HUD spans the whole viewport; only its authored status panel owns pointer input.
            root.pickingMode = PickingMode.Ignore;

            root.styleSheets.Add(styleSheet);
            Error = string.Empty;
            SetState(FactoryHudState.Empty, "The factory is starting.");
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

            Layout = Classify(width, height);

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

        /// <summary>
        /// The screen class of a viewport in panel points (the T036 thresholds). The run shell (T112) reuses it, so both
        /// screens classify a device identically.
        /// </summary>
        public static FactoryHudLayout Classify(float width, float height)
        {
            var landscape = width > height;
            if (width >= 1100f)
            {
                return FactoryHudLayout.Desktop;
            }

            if (Mathf.Min(width, height) >= 700f)
            {
                return landscape ? FactoryHudLayout.TabletLandscape : FactoryHudLayout.TabletPortrait;
            }

            return landscape ? FactoryHudLayout.CompactLandscape : FactoryHudLayout.CompactPortrait;
        }

        /// <summary>The USS class that selects a screen class (<c>factory-hud--compact-portrait</c> and so on).</summary>
        public static string LayoutClassName(FactoryHudLayout layout) => LayoutClass(layout);

        /// <summary>Whether a screen class has the side panel (tablet and desktop) instead of the phone's bottom tab bar.</summary>
        public static bool HasSidePanel(FactoryHudLayout layout) =>
            layout == FactoryHudLayout.TabletPortrait || layout == FactoryHudLayout.TabletLandscape || layout == FactoryHudLayout.Desktop;

        /// <summary>
        /// Shows or hides the HUD. The run shell (T112) replaces it while a run is live, and the HUD is shown again for a
        /// start-up failure, which only it can report.
        /// </summary>
        public void SetDisplayed(bool displayed)
        {
            if (root != null)
            {
                root.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
            }
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
        }

        /// <summary>Shows feedback only when it adds text beyond the status line, so a state message is never drawn twice.</summary>
        private void HideRepeatedFeedback()
        {
            var repeatsStatus = string.IsNullOrEmpty(feedbackLabel.text) ||
                string.Equals(feedbackLabel.text, statusLabel.text, StringComparison.Ordinal);
            feedbackLabel.style.display = repeatsStatus ? DisplayStyle.None : DisplayStyle.Flex;
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

        private void OnDestroy()
        {
            DisposeUi();
        }

        private void DisposeUi()
        {
            root?.RemoveFromHierarchy();
            root = null;
            feedbackLabel = null;
            statusLabel = null;
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
