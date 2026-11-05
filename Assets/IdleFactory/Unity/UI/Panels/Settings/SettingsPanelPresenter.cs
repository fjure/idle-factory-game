using System;
using IdleFactory.Unity.Persistence;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>
    /// The Settings sheet (T433, OD-8, FR-020, FR-020a): one single-choice radio group "Follow system / On / Off" for reduced motion,
    /// a live line naming the effective state and its source, and an inline "Not saved" note when the last write failed. The sheet
    /// decides nothing about motion: it writes the choice into the <see cref="DisplayPreferencesStore"/> (whose <c>Changed</c> event
    /// makes the bootstrap re-resolve the effective state) and shows the cached effective value. It re-reads the file and
    /// re-resolves once when it opens, never per frame.
    /// </summary>
    public sealed class SettingsPanelPresenter : IShellPanel
    {
        public const string RootName = "settings-panel";
        public const string FollowName = "settings-radio-follow";
        public const string OnName = "settings-radio-on";
        public const string OffName = "settings-radio-off";
        public const string GroupLabelText = "Reduced motion";
        public const string SelectedSuffix = "(selected)";
        public const string NotSavedText = "Not saved. Choose again.";

        private static readonly ReducedMotionPreference[] Order =
            { ReducedMotionPreference.FollowSystem, ReducedMotionPreference.On, ReducedMotionPreference.Off };

        private static readonly string[] Names = { FollowName, OnName, OffName };
        private static readonly string[] Labels = { "Follow system", "On", "Off" };

        private readonly DisplayPreferencesStore store;
        private readonly Func<EffectiveReducedMotion> refreshEffective;
        private readonly Func<EffectiveReducedMotion> currentEffective;
        private readonly Func<bool> systemAvailable;
        private readonly Func<string, Sprite> icons;
        private readonly VisualElement root;
        private readonly VisualElement radioGroup;
        private readonly Button[] radios = new Button[3];
        private readonly Label[] radioLabels = new Label[3];
        private readonly Label[] radioSuffixes = new Label[3];
        private readonly Label effectiveLine;
        private readonly Label notSaved;

        /// <param name="refreshEffective">Re-resolves the effective state (file and OS signal I/O); called when the sheet opens.</param>
        /// <param name="currentEffective">The cached effective state; called after a change, once the store's event has refreshed it.</param>
        /// <param name="systemAvailable">Whether the platform has an OS reduce-motion signal.</param>
        public SettingsPanelPresenter(
            DisplayPreferencesStore store,
            Func<EffectiveReducedMotion> refreshEffective,
            Func<EffectiveReducedMotion> currentEffective,
            Func<bool> systemAvailable,
            VisualTreeAsset tree,
            StyleSheet styles,
            Func<string, Sprite> icons = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.refreshEffective = refreshEffective ?? throw new ArgumentNullException(nameof(refreshEffective));
            this.currentEffective = currentEffective ?? throw new ArgumentNullException(nameof(currentEffective));
            this.systemAvailable = systemAvailable ?? (() => true);
            this.icons = icons;
            root = MarketPanelParts.CreateRoot(tree, styles, RootName, "set-panel");
            var card = MarketPanelParts.Ensure<VisualElement>(root, "settings-card", "set-card");
            // The "Not saved" note comes first (T475): when it shows, a failed save is the first thing in the sheet, above the fold on every phone class.
            notSaved = MarketPanelParts.Ensure<Label>(root, "settings-not-saved", "set-not-saved", card);
            var groupLabel = MarketPanelParts.Ensure<Label>(root, "settings-group-label", "set-group-label", card);
            groupLabel.text = GroupLabelText;
            // The live state sits above the radios (T475, T455 F1), so it is visible without scrolling on a phone. The sheet title is the shell's panel header; the sheet adds no second one.
            effectiveLine = MarketPanelParts.Ensure<Label>(root, "settings-effective", "set-effective", card);
            radioGroup = MarketPanelParts.Ensure<VisualElement>(root, "settings-radios", "set-radios", card);
            radioGroup.tooltip = GroupLabelText;
            for (var i = 0; i < radios.Length; i++)
            {
                BuildRadio(i);
            }

            PanelIcons.Mark(notSaved, icons, PanelIcons.Warning);
            Render(currentEffective());
        }

        public string Id => ShellPanelIds.Settings;
        public string Title => "Settings";
        public VisualElement Root => root;

        public Label GroupLabel => root.Q<Label>("settings-group-label");
        public VisualElement RadioGroup => radioGroup;
        public Button FollowRadio => radios[0];
        public Button OnRadio => radios[1];
        public Button OffRadio => radios[2];
        /// <summary>The "(selected)" word of a radio; hidden on the unselected radios and in narrow landscape columns at large text.</summary>
        public Label SelectedSuffixOf(Button radio) => radio.Q<Label>(radio.name + "-suffix");
        public Label EffectiveLine => effectiveLine;
        public Label NotSavedNote => notSaved;

        /// <summary>The radio that carries the selected choice.</summary>
        public ReducedMotionPreference Selected => store.Preference;

        public void OnShown(ShellPanelContext context)
        {
            // Lazy re-read when the sheet opens (contract): the file may have changed while the game was in the background.
            store.Reload();
            Render(refreshEffective());
        }

        public void Refresh()
        {
            // The run read model changed; nothing here depends on it.
        }

        public void OnHidden()
        {
        }

        /// <summary>Applies a choice as a tap on its radio does: writes it, then shows the effective state the store's event produced.</summary>
        public void Choose(ReducedMotionPreference preference)
        {
            store.TrySave(preference);
            Render(currentEffective());
        }

        private void BuildRadio(int index)
        {
            var button = new Button { name = Names[index], focusable = true };
            button.AddToClassList("run-control");
            button.AddToClassList("set-radio");
            var mark = new VisualElement { name = Names[index] + "-mark", pickingMode = PickingMode.Ignore };
            mark.AddToClassList("set-radio__mark");
            var label = new Label(Labels[index]) { name = Names[index] + "-label", pickingMode = PickingMode.Ignore };
            label.AddToClassList("set-radio__label");
            // The word "(selected)" is a separate label so a narrow column can drop it (T475, T455 F3); the filled mark and the border
            // still show the selection without colour, and the tooltip names it.
            var suffix = new Label(SelectedSuffix) { name = Names[index] + "-suffix", pickingMode = PickingMode.Ignore };
            suffix.AddToClassList("set-radio__suffix");
            button.Add(mark);
            button.Add(label);
            button.Add(suffix);
            var choice = Order[index];
            button.clicked += () => Choose(choice);
            button.RegisterCallback<KeyDownEvent>(evt => OnRadioKey(evt, index));
            radios[index] = button;
            radioLabels[index] = label;
            radioSuffixes[index] = suffix;
            radioGroup.Add(button);
        }

        private void OnRadioKey(KeyDownEvent evt, int index)
        {
            var step = 0;
            if (evt.keyCode == KeyCode.DownArrow || evt.keyCode == KeyCode.RightArrow)
            {
                step = 1;
            }
            else if (evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.LeftArrow)
            {
                step = -1;
            }

            if (step == 0)
            {
                return;
            }

            // Arrow keys move the choice within the group and take focus with it, as a native radio group does.
            var next = (index + step + radios.Length) % radios.Length;
            evt.StopPropagation();
            Choose(Order[next]);
            radios[next].Focus();
        }

        private void Render(EffectiveReducedMotion effective)
        {
            var preference = store.Preference;
            for (var i = 0; i < radios.Length; i++)
            {
                var selected = Order[i] == preference;
                radios[i].EnableInClassList("set-radio--selected", selected);
                radioLabels[i].text = Labels[i];
                radios[i].tooltip = GroupLabelText + ": " + Labels[i] + (selected ? ", selected" : string.Empty);
            }

            effectiveLine.text = EffectiveText(preference, effective, systemAvailable());
            var unsaved = store.HasUnsavedChoice;
            notSaved.text = unsaved ? NotSavedText : string.Empty;
            MarketPanelParts.SetVisible(notSaved, unsaved);
        }

        /// <summary>The live line of the contract's state table. Text only, never colour alone.</summary>
        public static string EffectiveText(ReducedMotionPreference preference, EffectiveReducedMotion effective, bool systemAvailable)
        {
            if (preference == ReducedMotionPreference.FollowSystem)
            {
                return systemAvailable
                    ? "Following system: reduced motion " + (effective.Reduced ? "on" : "off")
                    : "Following system: not available, animation plays";
            }

            return "Reduced motion " + (preference == ReducedMotionPreference.On ? "on" : "off") + " (your choice)";
        }
    }
}
