using System;
using IdleFactory.Unity.Presentation;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// The persistent layer of the run shell, extracted from <see cref="RunShellDocument"/> (T537, A-11, contracts/ui-surfaces-f006.md section 1): the
    /// elements that stay on the factory screen with no panel open, where each one lives for a screen class, the production attention indicator and
    /// the rule that decides when the zoom buttons show (FR-008a). It is plain C# over the cloned tree: it holds no gameplay rule and no state beyond
    /// what it shows.
    /// <list type="bullet">
    /// <item>Phone portrait: the top strip (Cash, Store, production attention, gear) and a bottom action row (hub entry, Overdrive).</item>
    /// <item>Phone landscape: no top bar. The strip is the left column; the action bar, with the gear, is the right column.</item>
    /// <item>Tablet and desktop: the strip above the world with the hub entry before the gear; Overdrive in the side panel header.</item>
    /// </list>
    /// None of these places is inside the world column on a phone, so the persistent layer never covers the framed world; on tablet and desktop the
    /// camera rectangle starts below the strip.
    /// </summary>
    internal sealed class PersistentLayer
    {
        private readonly VisualElement strip;
        private readonly VisualElement actionBar;
        private readonly Button hubEntry;
        private readonly Button overdrive;
        private readonly Button gear;
        private readonly Button attention;
        private readonly VisualElement zoomColumn;
        private readonly VisualElement oreVeinRoot;
        private readonly VisualElement attentionIcon;
        private readonly Label attentionCount;
        private readonly Label attentionMore;
        private int attentionSlot = -1;

        public PersistentLayer(VisualElement root)
        {
            strip = root.Q<VisualElement>("top-strip");
            actionBar = root.Q<VisualElement>("action-bar");
            hubEntry = root.Q<Button>("hub-entry");
            overdrive = root.Q<Button>("overdrive");
            gear = root.Q<Button>("settings");
            attention = root.Q<Button>("attention");
            zoomColumn = root.Q<VisualElement>("zoom-column");
            oreVeinRoot = root.Q<VisualElement>("factory-ore-vein");
            attentionIcon = root.Q<VisualElement>("attention-icon");
            attentionCount = root.Q<Label>("attention-count");
            attentionMore = root.Q<Label>("attention-more");
        }

        /// <summary>True when every element the layer needs was found in the UXML.</summary>
        public bool IsComplete => strip != null && actionBar != null && hubEntry != null && overdrive != null && gear != null && attention != null &&
            attentionIcon != null && attentionCount != null && attentionMore != null;

        public VisualElement ActionBar => actionBar;
        public Button AttentionButton => attention;

        /// <summary>The location the production attention indicator selects on a tap (the first affected one), or -1 while it is hidden.</summary>
        public int AttentionSlot => attentionSlot;

        public bool IsAttentionShown => attention != null && attention.style.display == DisplayStyle.Flex;

        public string AttentionCountText => attentionCount != null ? attentionCount.text : string.Empty;

        public string AttentionMoreText => attentionMore != null ? attentionMore.text : string.Empty;

        /// <summary>
        /// Moves the persistent controls to their home for the screen class. Idempotent: an element already where it belongs is not touched, so focus
        /// and scroll state survive a layout pass that changes nothing. Order matters: the gear moves first, because the hub entry on the side-panel
        /// classes is inserted before it.
        /// </summary>
        public void Place(FactoryHudLayout layout, VisualElement body, VisualElement worldColumn, VisualElement panelHeaderSlot)
        {
            var side = FactoryHudDocument.HasSidePanel(layout);
            var stripParent = side ? worldColumn : body;
            if (strip.parent != stripParent)
            {
                stripParent.Insert(0, strip);
            }

            var gearParent = layout == FactoryHudLayout.CompactLandscape ? actionBar : strip;
            if (gear.parent != gearParent)
            {
                gearParent.Add(gear);
            }

            if (side)
            {
                if (hubEntry.parent != strip)
                {
                    strip.Insert(strip.IndexOf(gear), hubEntry);
                }
            }
            else if (hubEntry.parent != actionBar)
            {
                actionBar.Insert(0, hubEntry);
            }

            // T537 fix round (OD-9b C1): on a phone in landscape the zoom column folds into the right column (it would otherwise cut the world band beside the
            // strip column); everywhere else it is the bottom-left control of the world column.
            if (layout == FactoryHudLayout.CompactLandscape)
            {
                if (zoomColumn.parent != actionBar)
                {
                    actionBar.Insert(actionBar.IndexOf(gear), zoomColumn);
                }
            }
            else if (zoomColumn.parent != worldColumn)
            {
                worldColumn.Insert(oreVeinRoot != null && oreVeinRoot.parent == worldColumn ? worldColumn.IndexOf(oreVeinRoot) : worldColumn.childCount, zoomColumn);
            }

            var overdriveParent = side ? panelHeaderSlot : actionBar;
            if (overdrive.parent != overdriveParent)
            {
                if (overdriveParent == actionBar)
                {
                    actionBar.Insert(actionBar.IndexOf(hubEntry) + 1, overdrive);
                }
                else
                {
                    overdriveParent.Add(overdrive);
                }
            }
        }

        /// <summary>
        /// The zoom buttons are visible on pointer profiles, and on touch profiles only with the "Show zoom buttons" preference (FR-008a). Pinch is the
        /// default on touch; the buttons are the non-gesture path for accessibility.
        /// </summary>
        public static bool ZoomButtonsAllowed(ShellDeviceProfile profile, bool showZoomButtonsPreference) => !profile.IsTouch || showZoomButtonsPreference;

        /// <summary>
        /// Shows the production attention indicator, or hides it for an invisible strip (no shortage, no store full). The indicator is one control: the
        /// resource icon, the count of affected machines and "+N" for further shortage resources; its accessible name carries the cause in words.
        /// </summary>
        public void SetProductionAttention(AttentionStrip model, string causeText, Action<VisualElement> applyIcon)
        {
            if (!model.IsVisible)
            {
                attentionSlot = -1;
                attention.style.display = DisplayStyle.None;
                attentionCount.text = string.Empty;
                attentionMore.text = string.Empty;
                attention.tooltip = string.Empty;
                return;
            }

            attentionSlot = model.FirstAffected.Value;
            attention.style.display = DisplayStyle.Flex;
            attentionCount.text = model.AffectedCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            attentionMore.text = model.MoreCount > 0 ? "+" + model.MoreCount.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
            attentionMore.style.display = model.MoreCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            applyIcon?.Invoke(attentionIcon);
            var machines = model.AffectedCount == 1 ? " machine" : " machines";
            attention.tooltip = causeText + ", " + model.AffectedCount.ToString(System.Globalization.CultureInfo.InvariantCulture) + machines +
                (model.MoreCount > 0 ? ", " + model.MoreCount.ToString(System.Globalization.CultureInfo.InvariantCulture) + " more causes" : string.Empty) +
                ". Select the first one.";
        }
    }
}
