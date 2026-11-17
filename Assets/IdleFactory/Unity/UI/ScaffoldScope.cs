using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// The scaffold scope of a hosted surface, extracted from <see cref="RunShellDocument"/> (T537, A-11, T536 Architect): the scope class a panel root, the
    /// confirmation dialog and the notice line carry, the scaffold button class every run control in that scope takes (the 48 pt floor and the one disabled
    /// look), and the soft-disabled state. It is plain UI Toolkit class work and holds no rule.
    /// <para>
    /// A surface is marked once when it mounts (<see cref="Mark"/>). A button a panel adds to it later is still covered: <see cref="Mark"/> watches the
    /// scope root for layout changes (a new child changes the layout) and marks the buttons that appeared, and <see cref="SetSoftDisabled"/> marks the
    /// control it disables when that control is inside a scope.
    /// </para>
    /// </summary>
    public static class ScaffoldScope
    {
        /// <summary>The scope class of a scaffold surface: a descendant button takes the scaffold's 48 pt floor and disabled look.</summary>
        public const string ScopeClass = "scaffold-scope";

        public const string ButtonClass = "scaffold-button";

        public const string DisabledClass = "scaffold-button--disabled";

        /// <summary>The soft-disabled class of every run control (the control stays focusable; the command behind it rejects with a reason).</summary>
        public const string SoftDisabledClass = "run-control--soft-disabled";

        private const string WatchedClass = "scaffold-scope--watched";

        /// <summary>
        /// Makes a hosted surface (a panel root, the dialog card, the notice line) a scaffold surface (FR-023, SC-009): adds the scope class and the
        /// scaffold button class to every run control button in it that is not a scaffold row, and keeps doing so for buttons added later. Idempotent.
        /// </summary>
        public static void Mark(VisualElement root)
        {
            if (root == null)
            {
                return;
            }

            root.AddToClassList(ScopeClass);
            root.Query<Button>().ForEach(MarkButton);
            if (!root.ClassListContains(WatchedClass))
            {
                root.AddToClassList(WatchedClass);
                root.RegisterCallback<GeometryChangedEvent>(evt => ((VisualElement)evt.currentTarget).Query<Button>().ForEach(MarkButton));
            }
        }

        /// <summary>True when the element, or one of its ancestors, is a scaffold surface.</summary>
        public static bool IsInScope(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current.ClassListContains(ScopeClass))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Gives one button the scaffold button class when it is a run control (not a scaffold row) and is not marked yet.</summary>
        public static void MarkButton(Button button)
        {
            if (button.ClassListContains("run-control") && !button.ClassListContains(PanelScaffold.RowClass) && !button.ClassListContains(ButtonClass))
            {
                button.AddToClassList(ButtonClass);
                if (button.ClassListContains(SoftDisabledClass))
                {
                    button.AddToClassList(DisabledClass);
                }
            }
        }

        /// <summary>
        /// Marks a control as disabled with its reason shown elsewhere: it stays focusable (Art Bible 9.4), looks disabled and the command behind it must
        /// reject activation with the reason. A real <c>SetEnabled(false)</c> removes focus. A scaffold button, or a run control inside a scaffold scope
        /// (also one that was added after the scope was marked), also takes the scaffold's one disabled look (shape, text and contrast).
        /// </summary>
        public static void SetSoftDisabled(VisualElement control, bool disabled)
        {
            control.EnableInClassList(SoftDisabledClass, disabled);
            if (control is Button button && IsInScope(control))
            {
                MarkButton(button);
            }

            if (control.ClassListContains(ButtonClass))
            {
                control.EnableInClassList(DisabledClass, disabled);
            }
        }

        public static bool IsSoftDisabled(VisualElement control) => control.ClassListContains(SoftDisabledClass);
    }
}
