using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>
    /// The scaffold's empty template for a panel that hosts its own empty box (T536, FR-023, SC-009): a marker, a title and a message in the shared
    /// <c>scaffold__state</c> card (Art Bible section 11), so Items and Contracts look like the hub's and the scaffold's empty state. The box keeps its
    /// own name and display toggling; this only gives it the shared classes and the two fixed children. The message label stays the panel's own.
    /// </summary>
    public static class ScaffoldEmpty
    {
        /// <summary>The fixed title of the empty template (the same words as the hub's empty state).</summary>
        public const string TitleText = "Nothing here yet";

        /// <summary>
        /// Gives <paramref name="box"/> the empty-state card classes. The caller names its own panel-specific card classes in <paramref name="dropClasses"/>
        /// (T537, T536 Architect: the template knows no panel).
        /// </summary>
        public static void Style(VisualElement box, params string[] dropClasses)
        {
            if (dropClasses != null)
            {
                for (var i = 0; i < dropClasses.Length; i++)
                {
                    box.RemoveFromClassList(dropClasses[i]);
                }
            }

            box.AddToClassList("scaffold__state");
            box.AddToClassList("scaffold__state--shown");
            box.AddToClassList("scaffold__state--empty");
        }

        public static Label Mark(string name)
        {
            var mark = new Label("i") { name = name, pickingMode = PickingMode.Ignore };
            mark.AddToClassList("scaffold__mark");
            return mark;
        }

        /// <summary>The title label of the template: <see cref="TitleText"/>, or the caller's own words (a locked state names the lock, T537).</summary>
        public static Label Title(string name, string text = null)
        {
            var title = new Label(text ?? TitleText) { name = name, pickingMode = PickingMode.Ignore };
            title.AddToClassList("scaffold__state-title");
            return title;
        }
    }
}
