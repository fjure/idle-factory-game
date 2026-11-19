using System;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// The milestone banner (T540, FR-035, research R14, contracts/ui-surfaces-f006.md): a non-blocking overlay at the top of the world column, below the strip and the notice bars. It shows
    /// for at most <see cref="MaxSeconds"/>, a tap on it or Back and Escape dismiss it, and it never takes focus (it is not focusable), so a machine card keeps the focus
    /// it had. It takes no layout space and is not part of <c>TryGetUnobstructedScreenRect</c>, so showing and hiding it never reframes the camera;
    /// the shell places it below the strip, so it never covers Cash, Overdrive or the status band. Under reduced motion it is static (it appears and goes without the fade).
    /// <para>
    /// Only the banner's own box takes pointer input; everything around it passes through (<c>picking-mode</c> Ignore on the parent chain, Position on the banner), so
    /// input outside the banner is never captured. The element is time-driven by <see cref="Advance"/> (the shell calls it every frame), which makes the lifetime
    /// deterministic and testable. The copy comes from <see cref="MilestoneCopy"/>; the screen-reader text is the label text and the tooltip.
    /// </para>
    /// </summary>
    public sealed class MilestoneBanner
    {
        public const string ElementName = "milestone-banner";

        /// <summary>The longest a banner stays (R14).</summary>
        public const float MaxSeconds = 2.5f;

        /// <summary>The fade-in of normal motion.</summary>
        public const float FadeInSeconds = 0.15f;

        private readonly VisualElement root;
        private readonly Label primary;
        private readonly Label secondary;
        private float elapsed;
        private bool reduced;
        private string fullText = string.Empty;
        private bool opaque;
        private bool primaryOnly;
        private bool hasExtra;

        private MilestoneBanner(VisualElement bannerRoot, Label primaryLabel, Label secondaryLabel)
        {
            root = bannerRoot;
            primary = primaryLabel;
            secondary = secondaryLabel;
        }

        /// <summary>Raised when the banner closes, by timeout, tap or Back.</summary>
        public event Action Closed;

        public VisualElement Root => root;
        public Label PrimaryLabel => primary;
        public Label SecondaryLabel => secondary;

        /// <summary>True while the banner shows.</summary>
        public bool IsShown { get; private set; }

        /// <summary>The text a screen reader reads: the main line and, when there is one, the second line.</summary>
        public string AccessibleText => fullText;

        /// <summary>
        /// T558 (C-F1, U-C4): on compact height or large text only the main line shows, so the banner stays short and does not cover the top machine row. The second line
        /// (B-04 and the others) stays in <see cref="AccessibleText"/>, the tooltip and the offline summary. Takes effect at once for a banner that is showing.
        /// </summary>
        public bool PrimaryOnly
        {
            get => primaryOnly;
            set
            {
                primaryOnly = value;
                secondary.style.display = hasExtra && !primaryOnly ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>Seconds the banner has been showing.</summary>
        public float Elapsed => elapsed;

        /// <summary>Builds the banner and inserts it after <paramref name="after"/> in the world column.</summary>
        public static MilestoneBanner Attach(VisualElement worldColumn, VisualElement after)
        {
            if (worldColumn == null) throw new ArgumentNullException(nameof(worldColumn));
            var bannerRoot = new VisualElement { name = ElementName };
            bannerRoot.AddToClassList("run-milestone");
            bannerRoot.focusable = false;
            bannerRoot.pickingMode = PickingMode.Position;
            bannerRoot.style.display = DisplayStyle.None;
            var primaryLabel = new Label { name = ElementName + "-text", pickingMode = PickingMode.Ignore };
            primaryLabel.AddToClassList("run-milestone__text");
            var secondaryLabel = new Label { name = ElementName + "-extra", pickingMode = PickingMode.Ignore };
            secondaryLabel.AddToClassList("run-milestone__text");
            secondaryLabel.AddToClassList("run-milestone__text--extra");
            bannerRoot.Add(primaryLabel);
            bannerRoot.Add(secondaryLabel);
            var index = after != null && after.parent == worldColumn ? worldColumn.IndexOf(after) + 1 : worldColumn.childCount;
            worldColumn.Insert(index, bannerRoot);
            var banner = new MilestoneBanner(bannerRoot, primaryLabel, secondaryLabel);
            bannerRoot.RegisterCallback<PointerDownEvent>(banner.OnPointerDown);
            return banner;
        }

        /// <summary>Shows the banner (replacing one that is showing). An empty main line shows nothing.</summary>
        public void Show(string main, string extra, bool reducedMotion)
        {
            if (string.IsNullOrEmpty(main))
            {
                Hide(false);
                return;
            }

            elapsed = 0f;
            reduced = reducedMotion;
            primary.text = main;
            hasExtra = !string.IsNullOrEmpty(extra);
            secondary.text = hasExtra ? extra : string.Empty;
            secondary.style.display = hasExtra && !primaryOnly ? DisplayStyle.Flex : DisplayStyle.None;
            fullText = hasExtra ? main + " " + extra : main;
            root.tooltip = fullText;
            opaque = reducedMotion;
            root.style.opacity = reducedMotion ? 1f : 0f;
            root.style.display = DisplayStyle.Flex;
            IsShown = true;
        }

        /// <summary>Advances the lifetime by <paramref name="seconds"/>; the banner closes at <see cref="MaxSeconds"/>.</summary>
        public void Advance(float seconds)
        {
            if (!IsShown || seconds <= 0f)
            {
                return;
            }

            elapsed += seconds;
            if (elapsed >= MaxSeconds)
            {
                Hide(true);
                return;
            }

            if (!reduced && elapsed < FadeInSeconds)
            {
                root.style.opacity = elapsed / FadeInSeconds;
            }
            else if (!opaque)
            {
                opaque = true;
                root.style.opacity = 1f;
            }
        }

        /// <summary>Closes the banner. Returns whether one was showing.</summary>
        public bool Dismiss() => Hide(true);

        private bool Hide(bool raise)
        {
            if (!IsShown)
            {
                return false;
            }

            IsShown = false;
            hasExtra = false;
            root.style.display = DisplayStyle.None;
            primary.text = string.Empty;
            secondary.text = string.Empty;
            fullText = string.Empty;
            root.tooltip = string.Empty;
            if (raise)
            {
                Closed?.Invoke();
            }

            return true;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (IsShown)
            {
                // PreventDefault keeps the pointer-down focus change away: tapping the banner must not blur the card's focused control.
                evt.PreventDefault();
                evt.StopPropagation();
                Dismiss();
            }
        }
    }
}
