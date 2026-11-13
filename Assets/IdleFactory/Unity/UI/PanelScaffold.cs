using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI
{
    /// <summary>The kind of a scaffold notice. Each kind has its own word, bar colour and corner shape, never colour alone.</summary>
    public enum ScaffoldNoticeKind
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>The view the scaffold body shows.</summary>
    public enum ScaffoldViewState
    {
        Content,
        Empty,
        Loading,
        Error,
    }

    /// <summary>
    /// The shared panel scaffold (F006 T533; FR-005, FR-023; research R11; Art Bible section 11): a header with Back, primary and
    /// secondary button classes, one disabled look, empty, loading and error templates with a recovery action, and a notice
    /// layer above the body and any sheet mounted in it. Panels that adopt it (T534 to T537) put their content into
    /// <see cref="Body"/> and read no other layout from it. It uses only the F003 chrome tokens and the <c>--run-fs-*</c> font
    /// variables of the shell, so it must be mounted inside the run shell's root. It has no animation (UI/UX C5), its Back and
    /// action buttons are 48 pt targets (UI/UX C4), and it holds no gameplay rule (it changes no simulation state).
    /// </summary>
    public sealed class PanelScaffold
    {
        /// <summary>The asset paths the scaffold loads from; panel code receives the assets by injection like the other panels.</summary>
        public const string UxmlPath = "Assets/IdleFactory/Unity/UI/PanelScaffold.uxml";

        public const string StylePath = "Assets/IdleFactory/Unity/UI/PanelScaffold.uss";

        /// <summary>The most notices shown at once; a newer one pushes the oldest out.</summary>
        public const int MaxNotices = 3;

        private const string ButtonClass = "scaffold-button";
        private const string PrimaryClass = "scaffold-button--primary";
        private const string SecondaryClass = "scaffold-button--secondary";
        private const string DisabledClass = "scaffold-button--disabled";
        private const string StateShown = "scaffold__state--shown";

        private readonly VisualElement root;
        private readonly Button back;
        private readonly Label title;
        private readonly ScrollView body;
        private readonly VisualElement notices;
        private readonly ScrollView stateScroll;
        private readonly VisualElement state;
        private readonly Label stateMark;
        private readonly Label stateTitle;
        private readonly Label stateMessage;
        private readonly Button statePrimary;
        private readonly Button stateSecondary;
        private readonly Action onBack;
        private Action stateAction;

        public PanelScaffold(VisualTreeAsset tree, StyleSheet styles, string headerTitle, Action onBack)
        {
            if (tree == null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            var instance = tree.Instantiate();
            root = instance.Q<VisualElement>("scaffold-root") ?? throw new InvalidOperationException("PanelScaffold.uxml has no scaffold-root.");
            if (styles != null)
            {
                root.styleSheets.Add(styles);
            }

            this.onBack = onBack;
            back = Require<Button>("scaffold-back");
            title = Require<Label>("scaffold-title");
            body = Require<ScrollView>("scaffold-body");
            notices = Require<VisualElement>("scaffold-notices");
            stateScroll = Require<ScrollView>("scaffold-state-scroll");
            state = Require<VisualElement>("scaffold-state");
            stateMark = Require<Label>("scaffold-state-mark");
            stateTitle = Require<Label>("scaffold-state-title");
            stateMessage = Require<Label>("scaffold-state-message");
            statePrimary = Require<Button>("scaffold-state-primary");
            stateSecondary = Require<Button>("scaffold-state-secondary");
            title.text = headerTitle ?? string.Empty;
            back.tooltip = "Back";
            back.clicked += () => this.onBack?.Invoke();
            statePrimary.clicked += OnPrimary;
            stateSecondary.clicked += () => this.onBack?.Invoke();
            State = ScaffoldViewState.Content;
            ApplyState();
        }

        public VisualElement Root => root;

        public VisualElement Header => root.Q<VisualElement>("scaffold-header");

        public Button BackButton => back;

        public Label TitleLabel => title;

        /// <summary>The scroll view the panel fills. Its content container is where panel elements go.</summary>
        public VisualElement Body => body.contentContainer;

        public ScrollView BodyScroll => body;

        /// <summary>The layer above the body for notices; it is the last child of the stage, so it also draws above a sheet mounted there.</summary>
        public VisualElement NoticeLayer => notices;

        public VisualElement StateCard => state;

        /// <summary>The scroll view that holds the state card, so a long message at 200 % text scrolls instead of clipping.</summary>
        public ScrollView StateScroll => stateScroll;

        public Label StateTitle => stateTitle;

        public Label StateMessage => stateMessage;

        /// <summary>The primary state action (Retry for the error state, the caller's action for the empty state); hidden when absent.</summary>
        public Button StatePrimaryButton => statePrimary;

        /// <summary>The secondary state action: Back, so every non-content state has a way out.</summary>
        public Button StateSecondaryButton => stateSecondary;

        public ScaffoldViewState State { get; private set; }

        public IReadOnlyList<VisualElement> Notices
        {
            get
            {
                var list = new List<VisualElement>();
                foreach (var child in notices.Children())
                {
                    list.Add(child);
                }

                return list;
            }
        }

        public string Title
        {
            get => title.text;
            set => title.text = value ?? string.Empty;
        }

        /// <summary>Shows or hides the header Back (the hub root has nothing to go back to). A hidden Back leaves the focus order.</summary>
        public bool BackVisible
        {
            get => back.style.display != DisplayStyle.None;
            set
            {
                back.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                back.focusable = value;
            }
        }

        /// <summary>Shows the panel content and hides the state card.</summary>
        public void ShowContent()
        {
            State = ScaffoldViewState.Content;
            stateAction = null;
            ApplyState();
        }

        /// <summary>Shows the empty template: what is missing, and an optional action; Back is always offered.</summary>
        public void ShowEmpty(string heading, string message, string actionText = null, Action action = null)
        {
            State = ScaffoldViewState.Empty;
            stateAction = action;
            SetState("i", heading, message, actionText);
            ApplyState();
        }

        /// <summary>Shows the loading template: a static bar and a message; Back is offered so the wait can be left.</summary>
        public void ShowLoading(string message)
        {
            State = ScaffoldViewState.Loading;
            stateAction = null;
            SetState("...", "Loading", message, null);
            ApplyState();
        }

        /// <summary>Shows the error template with Retry as the primary recovery and Back as the secondary.</summary>
        public void ShowError(string message, Action retryAction)
        {
            State = ScaffoldViewState.Error;
            stateAction = retryAction;
            SetState("!", "Something went wrong", message, retryAction == null ? null : "Retry");
            ApplyState();
        }

        /// <summary>Layers a notice above the body (and above any sheet) with a dismiss button; returns its text label.</summary>
        public Label ShowNotice(string text, ScaffoldNoticeKind kind = ScaffoldNoticeKind.Info)
        {
            while (notices.childCount >= MaxNotices)
            {
                notices.RemoveAt(0);
            }

            var row = new VisualElement { name = "scaffold-notice" };
            row.AddToClassList("scaffold__notice");
            row.AddToClassList("scaffold__notice--" + kind.ToString().ToLowerInvariant());
            var label = new Label((kind == ScaffoldNoticeKind.Info ? "Notice: " : kind + ": ") + text) { name = "scaffold-notice-text" };
            label.AddToClassList("scaffold__notice-text");
            row.Add(label);
            var dismiss = CreateButton("scaffold-notice-dismiss", "Dismiss", false);
            dismiss.tooltip = "Dismiss notice";
            dismiss.clicked += () => row.RemoveFromHierarchy();
            row.Add(dismiss);
            notices.Add(row);
            return label;
        }

        public void ClearNotices() => notices.Clear();

        /// <summary>Creates a scaffold button (a run control, so the shell focus ring and 44 pt floor apply; the scaffold raises it to 48 pt).</summary>
        public static Button CreateButton(string name, string text, bool primary)
        {
            var button = new Button { name = name, text = text, focusable = true };
            button.AddToClassList("run-control");
            button.AddToClassList(ButtonClass);
            button.AddToClassList(primary ? PrimaryClass : SecondaryClass);
            return button;
        }

        public static bool IsDisabled(Button button) => button != null && button.ClassListContains(DisabledClass);

        /// <summary>
        /// Applies or clears the one disabled look. The button stays focusable and still raises its click, like every soft-disabled
        /// control of the run panels, so the caller checks <see cref="IsDisabled"/> and shows the rejection. The reason goes to the
        /// optional <paramref name="reasonLabel"/> and into the button's tooltip, which is its accessible description.
        /// </summary>
        public static void SetDisabled(Button button, bool disabled, string reason = null, Label reasonLabel = null)
        {
            if (button == null)
            {
                return;
            }

            button.EnableInClassList(DisabledClass, disabled);
            button.tooltip = disabled && !string.IsNullOrEmpty(reason) ? button.text + ". Unavailable: " + reason : string.Empty;
            if (reasonLabel != null)
            {
                reasonLabel.AddToClassList("scaffold__reason");
                reasonLabel.text = disabled && !string.IsNullOrEmpty(reason) ? reason : string.Empty;
                reasonLabel.style.display = string.IsNullOrEmpty(reasonLabel.text) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private void OnPrimary() => stateAction?.Invoke();

        private void SetState(string mark, string heading, string message, string actionText)
        {
            stateMark.text = mark;
            stateTitle.text = heading ?? string.Empty;
            stateMessage.text = message ?? string.Empty;
            statePrimary.text = actionText ?? string.Empty;
            statePrimary.tooltip = actionText ?? string.Empty;
            stateSecondary.text = "Back";
            stateSecondary.tooltip = "Back";
        }

        private void ApplyState()
        {
            var showState = State != ScaffoldViewState.Content;
            state.EnableInClassList(StateShown, showState);
            state.EnableInClassList("scaffold__state--empty", State == ScaffoldViewState.Empty);
            state.EnableInClassList("scaffold__state--loading", State == ScaffoldViewState.Loading);
            state.EnableInClassList("scaffold__state--error", State == ScaffoldViewState.Error);
            body.style.display = showState ? DisplayStyle.None : DisplayStyle.Flex;
            stateScroll.style.display = showState ? DisplayStyle.Flex : DisplayStyle.None;
            var hasPrimary = showState && !string.IsNullOrEmpty(statePrimary.text);
            statePrimary.style.display = hasPrimary ? DisplayStyle.Flex : DisplayStyle.None;
            statePrimary.focusable = hasPrimary;
            stateSecondary.style.display = showState ? DisplayStyle.Flex : DisplayStyle.None;
            stateSecondary.focusable = showState;
        }

        private T Require<T>(string name) where T : VisualElement =>
            root.Q<T>(name) ?? throw new InvalidOperationException("PanelScaffold.uxml has no " + name + ".");
    }
}
