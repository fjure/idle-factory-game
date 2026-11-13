using System;
using IdleFactory.Unity.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>
    /// The shared frame of the two full-screen status surfaces of T118, <see cref="OfflineSummaryPanel"/> (the offline return) and
    /// <see cref="StartupStatusPanel"/> (the start-up and import status). They are not tab panels: they cover the screen like
    /// the shell's confirmation dialog and are mounted into a host element with <see cref="Mount"/>, either the shell root (the
    /// offline summary) or the UIDocument root when the shell does not exist yet (a blocked start-up). The frame is a scrim, a
    /// card with a title, a scrolling body and a pinned action row; it traps focus while it is open and returns focus to the
    /// control that had it, and it respects the safe area (contracts/ui-surfaces.md, Global).
    ///
    /// Under the shell the surface inherits the shell's colour and font-size variables (<c>--run-fs-*</c>, the text scale of
    /// T191). Outside the shell it carries <see cref="StandaloneClass"/>, which defines the same variables, and it applies the
    /// system font scale itself with the same <c>run-text-scale--NNN</c> classes.
    /// </summary>
    public abstract class StatusSurface
    {
        /// <summary>The class a surface that is mounted outside the run shell carries (it defines the variables the shell would).</summary>
        public const string StandaloneClass = "status-standalone";

        private const string ShellClass = "run-shell";
        private const long SafeAreaSyncMilliseconds = 100;

        private readonly VisualElement root;
        private readonly VisualElement card;
        private readonly Label title;
        private readonly Label notice;
        private readonly ScrollView scroll;
        private readonly VisualElement content;
        private readonly VisualElement actions;
        private IVisualElementScheduledItem safeAreaSync;
        private IVisualElementScheduledItem noticeTimer;
        private VisualElement returnFocusTo;
        private VisualElement initialFocus;
        private bool open;
        private bool refocusPending;
        private bool standalone;
        private RunShellDocument shell;
        private int appliedTextScale;

        protected StatusSurface(string name, StyleSheet styles)
        {
            root = new VisualElement { name = name };
            root.AddToClassList("status-surface");
            // T536 (FR-023, SC-009): the surface is a scaffold surface (its buttons take the 48 pt floor and the one disabled look).
            root.AddToClassList(RunShellDocument.ScaffoldScopeClass);
            root.style.display = DisplayStyle.None;
            if (styles != null)
            {
                root.styleSheets.Add(styles);
            }

            card = new VisualElement { name = name + "-card" };
            card.AddToClassList("status-card");
            root.Add(card);

            title = new Label { name = name + "-title" };
            title.AddToClassList("status-title");
            card.Add(title);
            WrapFit(title);

            notice = new Label { name = name + "-notice" };
            notice.AddToClassList("status-notice");
            notice.style.display = DisplayStyle.None;
            card.Add(notice);
            WrapFit(notice);

            scroll = new ScrollView(ScrollViewMode.Vertical) { name = name + "-scroll" };
            scroll.AddToClassList("status-scroll");
            card.Add(scroll);

            content = new VisualElement { name = name + "-content" };
            content.AddToClassList("status-content");
            scroll.Add(content);

            actions = new VisualElement { name = name + "-actions" };
            actions.AddToClassList("status-actions");
            card.Add(actions);

            root.RegisterCallback<FocusInEvent>(OnFocusIn);
            root.RegisterCallback<FocusOutEvent>(OnFocusOut);
            root.RegisterCallback<GeometryChangedEvent>(_ => SyncSafeArea());
        }

        /// <summary>The overlay element: it fills its host, and it is hidden until the surface is shown.</summary>
        public VisualElement Root => root;

        public VisualElement Card => card;
        public Label TitleLabel => title;
        public Label NoticeLabel => notice;
        public ScrollView Scroll => scroll;
        public VisualElement Content => content;
        public VisualElement Actions => actions;

        /// <summary>True while the surface is shown.</summary>
        public bool IsOpen => open;

        /// <summary>
        /// Lets focus leave the surface while a dialog outside it (the shell's shared confirmation) is open; the dialog traps focus itself.
        /// </summary>
        protected bool TrapSuspended { get; set; }

        /// <summary>The shell the surface is mounted under, or null for a surface mounted outside it.</summary>
        public RunShellDocument Shell => shell;

        /// <summary>True when the surface supplies its own palette and text scale because no shell is its ancestor.</summary>
        public bool IsStandalone => standalone;

        /// <summary>
        /// Mounts the surface as the last child of <paramref name="host"/>. Pass the shell as well when the host is the shell root:
        /// the surface then follows the shell's safe-area insets. A surface mounted outside the shell defines its own variables.
        /// </summary>
        public void Mount(VisualElement host, RunShellDocument shellDocument = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            Unmount();
            shell = shellDocument;
            standalone = shellDocument == null && !HasShellAncestor(host);
            root.EnableInClassList(StandaloneClass, standalone);
            var dialogLayer = shellDocument?.DialogLayer;
            if (dialogLayer != null && dialogLayer.parent == host)
            {
                // Under the shell the surface sits below the shell's confirmation dialog, which must stay on top of it.
                host.Insert(host.IndexOf(dialogLayer), root);
            }
            else
            {
                host.Add(root);
            }

            ApplySystemTextScale();
            safeAreaSync?.Pause();
            safeAreaSync = root.schedule.Execute(SyncSafeArea).Every(SafeAreaSyncMilliseconds);
            SyncSafeArea();
        }

        /// <summary>Removes the surface from its host. An open surface closes first.</summary>
        public void Unmount()
        {
            if (open)
            {
                CloseSurface(false);
            }

            safeAreaSync?.Pause();
            safeAreaSync = null;
            root.RemoveFromHierarchy();
            shell = null;
        }

        /// <summary>
        /// Sets the safe-area insets in panel points. Under the shell they follow the shell's own insets and need no call; a
        /// surface mounted outside the shell gets them from the host once the screen layout is known.
        /// </summary>
        public void ApplySafeInsets(float left, float top, float right, float bottom)
        {
            root.style.paddingLeft = Mathf.Max(0f, left);
            root.style.paddingTop = Mathf.Max(0f, top);
            root.style.paddingRight = Mathf.Max(0f, right);
            root.style.paddingBottom = Mathf.Max(0f, bottom);
        }

        /// <summary>Copies the shell's safe-area insets (a no-op outside the shell). Runs on a 100 ms timer and on a geometry change.</summary>
        public void SyncSafeArea()
        {
            var body = shell?.Root?.Q<VisualElement>("run-body");
            if (body == null)
            {
                return;
            }

            var style = body.resolvedStyle;
            ApplySafeInsets(style.paddingLeft, style.paddingTop, style.paddingRight, style.paddingBottom);
        }

        /// <summary>Applies the Android system font scale to a surface that is mounted outside the shell (the shell does it for itself).</summary>
        public void ApplySystemTextScale() => ApplyTextScalePercent(ShellTextScale.Percent(ShellTextScale.ProbeSystemFactor()));

        /// <summary>Applies one text-scale step (100 to 200) to a surface that is mounted outside the shell.</summary>
        public void ApplyTextScalePercent(int percent)
        {
            if (!standalone)
            {
                return;
            }

            appliedTextScale = ShellTextScale.Percent(percent / 100f);
            var active = ShellTextScale.ClassName(appliedTextScale);
            foreach (var step in ShellTextScale.Steps())
            {
                var name = ShellTextScale.ClassName(step);
                root.EnableInClassList(name, name == active);
            }
        }

        /// <summary>The text-scale step of a surface mounted outside the shell (0 until one was applied).</summary>
        public int StandaloneTextScalePercent => appliedTextScale;

        // ------------------------------------------------------------------------------------------------------
        // Showing and hiding
        // ------------------------------------------------------------------------------------------------------

        /// <summary>Shows the surface and gives <paramref name="focusTarget"/> focus; the control that had focus gets it back on close.</summary>
        protected void OpenSurface(VisualElement focusTarget)
        {
            if (!open)
            {
                returnFocusTo = root.panel?.focusController?.focusedElement as VisualElement;
                if (standalone && !textScaleOverridden)
                {
                    ApplySystemTextScale();
                }
            }

            open = true;
            initialFocus = focusTarget;
            root.style.display = DisplayStyle.Flex;
            FocusWhenReady(focusTarget);
        }

        private bool textScaleOverridden;

        /// <summary>Pins the text-scale step of a surface mounted outside the shell, so the system font scale no longer replaces it (tests, previews).</summary>
        public void OverrideTextScalePercent(int percent)
        {
            textScaleOverridden = true;
            ApplyTextScalePercent(percent);
        }

        protected void CloseSurface(bool restoreFocus = true)
        {
            if (!open)
            {
                return;
            }

            open = false;
            root.style.display = DisplayStyle.None;
            ClearNotice();
            var target = returnFocusTo;
            returnFocusTo = null;
            initialFocus = null;
            if (restoreFocus && target != null && target.panel == root.panel && target.enabledInHierarchy)
            {
                FocusWhenReady(target);
            }
        }

        /// <summary>Gives an element focus as soon as it can take it (its style may not be resolved the frame it was shown).</summary>
        protected static void FocusWhenReady(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            element.Focus();
            if (element.focusController?.focusedElement == element)
            {
                return;
            }

            var attempts = 0;
            IVisualElementScheduledItem item = null;
            item = element.schedule.Execute(() =>
            {
                attempts++;
                element.Focus();
                if (element.focusController?.focusedElement == element || attempts >= 12)
                {
                    item.Pause();
                }
            }).Every(16);
        }

        /// <summary>Shows a one-line notice under the title; it hides itself after <paramref name="seconds"/>.</summary>
        protected void ShowNotice(string text, float seconds = 6f)
        {
            notice.text = text ?? string.Empty;
            notice.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            noticeTimer?.Pause();
            noticeTimer = null;
            if (!string.IsNullOrEmpty(text) && seconds > 0f)
            {
                noticeTimer = notice.schedule.Execute(ClearNotice);
                noticeTimer.ExecuteLater((long)(seconds * 1000f));
            }
        }

        protected void ClearNotice()
        {
            noticeTimer?.Pause();
            noticeTimer = null;
            notice.text = string.Empty;
            notice.style.display = DisplayStyle.None;
        }

        // ------------------------------------------------------------------------------------------------------
        // Focus: ring class, scroll into view, trap
        // ------------------------------------------------------------------------------------------------------

        private void OnFocusIn(FocusInEvent evt)
        {
            if (!(evt.target is VisualElement target))
            {
                return;
            }

            if (target.ClassListContains("run-control"))
            {
                target.AddToClassList("run-control--focused");
            }

            // A clipped control scrolls into view when it receives focus (UI surfaces contract, Controls).
            for (var parent = target.parent; parent != null; parent = parent.parent)
            {
                if (parent is ScrollView parentScroll)
                {
                    parentScroll.ScrollTo(target);
                    break;
                }
            }
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            (evt.target as VisualElement)?.RemoveFromClassList("run-control--focused");
            if (!open || TrapSuspended || !(evt.relatedTarget is VisualElement next) || root.Contains(next) || refocusPending)
            {
                return;
            }

            // The trap: focus that moves outside the open surface goes back into it.
            refocusPending = true;
            root.schedule.Execute(() =>
            {
                refocusPending = false;
                if (open && initialFocus != null)
                {
                    initialFocus.Focus();
                }
            });
        }

        // ------------------------------------------------------------------------------------------------------
        // Element helpers
        // ------------------------------------------------------------------------------------------------------

        protected static Label MakeLabel(string name, string className)
        {
            var label = new Label { name = name };
            label.AddToClassList(className);
            WrapFit(label);
            return label;
        }

        protected static void SetDisplayed(VisualElement element, bool displayed) =>
            element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;

        private static bool HasShellAncestor(VisualElement host)
        {
            for (var current = host; current != null; current = current.parent)
            {
                if (current.ClassListContains(ShellClass))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A wrapped label whose only measure happened with a viewport as its height limit is not measured again, so its tail can be
        /// clipped in a short card. The label's minimum height is set to its real wrapped height (wiki: UI Toolkit HUD layout
        /// pitfalls); the change re-runs this handler until it is stable.
        /// </summary>
        protected static void WrapFit(Label label) => label.RegisterCallback<GeometryChangedEvent>(OnLabelGeometry);

        private static void OnLabelGeometry(GeometryChangedEvent evt)
        {
            var label = (Label)evt.target;
            var width = label.contentRect.width;
            if (width <= 0f || string.IsNullOrEmpty(label.text))
            {
                return;
            }

            var needed = label.MeasureTextSize(label.text, width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined).y;
            var current = label.style.minHeight.keyword == StyleKeyword.Undefined ? label.style.minHeight.value.value : 0f;
            if (Mathf.Abs(needed - current) > 0.5f)
            {
                label.style.minHeight = needed;
            }
        }
    }
}
