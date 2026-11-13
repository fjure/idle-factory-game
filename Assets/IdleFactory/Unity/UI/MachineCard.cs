using System;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI.Panels;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI
{
    /// <summary>What a successful machine action answers with (FR-034): a restrained response on the machine.</summary>
    public enum MachineResponseKind
    {
        /// <summary>A successful upgrade: the UpgradeGlint of the F006 FX family.</summary>
        UpgradeGlint = 0,

        /// <summary>A successful switch-on: a restrained response (the Spark of the FX family stands in until the art pass names another).</summary>
        SwitchOn = 1,
    }

    /// <summary>
    /// The world side of a machine response. T529's <c>EffectPool</c> implements it (the pool owns spawning, coalescing and the flash caps), so the
    /// card never draws an effect itself. Until a sink is wired, and always under reduced motion, the response is the static highlight.
    /// </summary>
    public interface IMachineEffects
    {
        /// <summary>Spawns the effect of <paramref name="kind"/> at the machine of <paramref name="location"/>.</summary>
        void Spawn(MachineResponseKind kind, int location);
    }

    /// <summary>
    /// The response policy of the machine surfaces (FR-034, research R12): a world effect through the sink when motion is on and a sink is wired,
    /// otherwise the static one-second highlight the caller shows. No effect ever spawns under reduced motion.
    /// </summary>
    public sealed class MachineFeedback
    {
        private readonly Func<bool> reducedMotion;

        public MachineFeedback(Func<bool> reducedMotion, IMachineEffects effects = null)
        {
            this.reducedMotion = reducedMotion;
            Effects = effects;
        }

        /// <summary>The world effect sink. Late-bound: T529 sets it after the panels were composed, and every card and Details view sharing this object sees it.</summary>
        public IMachineEffects Effects { get; set; }

        /// <summary>True while the effective reduced-motion state is on.</summary>
        public bool Reduced => reducedMotion != null && reducedMotion();

        /// <summary>The number of responses played, for tests.</summary>
        public int Responses { get; private set; }

        /// <summary>The kind of the last response, or null.</summary>
        public MachineResponseKind? LastKind { get; private set; }

        /// <summary>True when the last response went to the world effect sink (false: the static highlight).</summary>
        public bool LastWasWorldEffect { get; private set; }

        /// <summary>Plays a response. <paramref name="showStatic"/> shows the static highlight and is called when no world effect plays.</summary>
        public void Play(MachineResponseKind kind, int location, Action showStatic)
        {
            Responses++;
            LastKind = kind;
            var sink = Effects;
            if (sink != null && !Reduced)
            {
                LastWasWorldEffect = true;
                sink.Spawn(kind, location);
                return;
            }

            LastWasWorldEffect = false;
            showStatic?.Invoke();
        }
    }

    /// <summary>What the shell asks of a card whose models must be reset (a session replace or an offline return), without knowing the card type.</summary>
    public interface IMachineCardModels
    {
        void ResetModels(bool offlineReturn = false);
    }

    /// <summary>The card's copy in one place (world-facing words follow reviews/world-copy.md: "location", never "slot", "plate" or "bay").</summary>
    public static class MachineCardCopy
    {
        public const string Running = "Running";
        public const string Waiting = "Waiting";
        public const string Paused = "Paused";
        public const string SwitchedOff = "Switched off";
        public const string StoreFull = "Store is full";
        public const string WaitingCause = "The finished cycle cannot be delivered yet";
        public const string SwitchedOffCause = "Switch it on in Details";
        public const string ShortOnInput = "Short on an input";
        public const string Gone = "That machine is gone.";
        public const string MaxLevel = "Maximum level";
        public const string CloseName = "Close";
        public const string DetailsName = "Details";

        /// <summary>The state line of a visual state: the state in words, the cause goes on its own line.</summary>
        public static string StateLine(MachineVisualState state)
        {
            switch (state)
            {
                case MachineVisualState.Running: return Running;
                case MachineVisualState.Waiting: return Waiting;
                case MachineVisualState.Inactive: return SwitchedOff;
                default: return Paused;
            }
        }
    }

    /// <summary>
    /// The machine card (T534, FR-016 to FR-019, research R10): one docked instance that replaces the sheet a machine tap used to open. It shows
    /// the name and level, the state line and Upgrade (cost and shortfall inside the button) in the first view, and below it the cause line
    /// with its icon and the Details chevron. Upgrade happens in place and focus stays on it. The card changes state only through
    /// <see cref="RunCommands"/> and keeps no gameplay rule. The shell docks it (bottom band, side column or side panel) and the camera keeps the
    /// selected machine in view (<c>OpenSheet</c>); Details opens the former machine panel, which keeps every capability of it.
    /// <para>
    /// Models (P2-F10, P2-F12, P2-F13): the card owns one <see cref="MachinePresentationModel"/> and reads the limiting resource from it, never
    /// deriving it again; <see cref="AttentionModel.ShortOnCopy"/> is the copy. The model is refreshed every frame from cached snapshots (a skipped
    /// refresh never repeats a delta), and <see cref="ResetModels"/> runs on a session replace.
    /// </para>
    /// </summary>
    public sealed class MachineCard : IShellPanel, IMachineCardModels
    {
        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly Func<string, Sprite> icons;
        private readonly MachineFeedback feedback;
        private readonly Func<FactoryWorldLayout> layoutSource;
        private readonly VisualElement root;
        private readonly Label nameLabel;
        private readonly Label stateLabel;
        private readonly Label inlineLabel;
        private readonly Label causeLabel;
        private readonly ScrollView more;
        private readonly MachinePresentationModel model = new MachinePresentationModel();
        private MachineStateView[] states = new MachineStateView[32];
        private ShellPanelContext context;
        private RunFactorySnapshot factory;
        private RatesSnapshot rates;
        private MachineSnapshot machine;
        private Guid shownInstance;
        private int slot = -1;
        private long structureRevision = -1;
        private long quantityRevision = -1;
        private bool dirty = true;
        private bool suppressNext;
        private IVisualElementScheduledItem highlightTimer;

        public MachineCard(
            RunSession session,
            RunCommands commands,
            VisualTreeAsset tree,
            StyleSheet styles,
            StyleSheet scaffoldStyles = null,
            Func<string, Sprite> icons = null,
            MachineFeedback feedback = null,
            Func<FactoryWorldLayout> layoutSource = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            if (tree == null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            this.icons = icons;
            this.feedback = feedback ?? new MachineFeedback(null);
            this.layoutSource = layoutSource;
            var instance = tree.Instantiate();
            root = instance.Q<VisualElement>("machine-card") ?? throw new InvalidOperationException("MachineCard.uxml has no machine-card.");
            root.name = "machine-card";
            if (scaffoldStyles != null)
            {
                root.styleSheets.Add(scaffoldStyles);
            }

            if (styles != null)
            {
                root.styleSheets.Add(styles);
            }

            nameLabel = Require<Label>("machine-card-name");
            stateLabel = Require<Label>("machine-card-state");
            inlineLabel = Require<Label>("machine-card-inline");
            causeLabel = Require<Label>("machine-card-cause");
            more = Require<ScrollView>("machine-card-more");
            UpgradeButton = Require<Button>("machine-card-upgrade");
            DetailsButton = Require<Button>("machine-card-details");
            CloseButton = Require<Button>("machine-card-close");
            UpgradeButton.clicked += OnUpgradeClicked;
            DetailsButton.clicked += OnDetailsClicked;
            CloseButton.clicked += OnCloseClicked;
            DetailsButton.tooltip = MachineCardCopy.DetailsName;
            CloseButton.tooltip = MachineCardCopy.CloseName;
            var closeSprite = icons != null ? icons("F003-UI-ICON-CLOSE") : null;
            if (closeSprite != null)
            {
                CloseButton.style.backgroundImage = new StyleBackground(closeSprite);
                CloseButton.text = string.Empty;
            }
            else
            {
                CloseButton.text = MachineCardCopy.CloseName;
            }

            inlineLabel.style.display = DisplayStyle.None;
            PanelScaffold.SetDisabled(UpgradeButton, false);
        }

        public string Id => ShellPanelIds.MachineCard;

        public string Title => "Machine";

        public VisualElement Root => root;

        public Button UpgradeButton { get; }

        public Button DetailsButton { get; }

        public Button CloseButton { get; }

        /// <summary>The name and level line.</summary>
        public Label NameLabel => nameLabel;

        /// <summary>The state line.</summary>
        public Label StateLabel => stateLabel;

        /// <summary>The cause line (empty and hidden when there is no cause).</summary>
        public Label CauseLabel => causeLabel;

        /// <summary>The inline line under Upgrade: a stale action or a rejection (empty and hidden when none).</summary>
        public Label InlineLabel => inlineLabel;

        /// <summary>The scrolling part below Upgrade (cause line and Details).</summary>
        public ScrollView More => more;

        /// <summary>The slot shown, or -1 while the card is hidden.</summary>
        public int Slot => slot;

        /// <summary>The instance id of the machine the card shows, or <see cref="Guid.Empty"/>.</summary>
        public Guid ShownInstance => shownInstance;

        /// <summary>The model the card owns, for tests and the owner of the world effects.</summary>
        public MachinePresentationModel Model => model;

        /// <summary>The seconds the static highlight stays (FR-034: one second); a test shortens it.</summary>
        public float HighlightSeconds { get; set; } = 1f;

        /// <summary>True while the static highlight shows.</summary>
        public bool IsHighlighted => root.ClassListContains("mcard--highlight");

        /// <summary>The visual state the model reports for the shown machine (Running when nothing is shown).</summary>
        public MachineVisualState VisualState { get; private set; }

        private T Require<T>(string name) where T : VisualElement =>
            root.Q<T>(name) ?? throw new InvalidOperationException("MachineCard.uxml has no " + name + ".");

        public void OnShown(ShellPanelContext shown)
        {
            context = shown;
            slot = shown.SlotIndex;
            shownInstance = Guid.Empty;
            ClearInline();
            dirty = true;
            Refresh();
        }

        public void OnHidden()
        {
            slot = -1;
            context = null;
            shownInstance = Guid.Empty;
            highlightTimer?.Pause();
            highlightTimer = null;
            root.RemoveFromClassList("mcard--highlight");
        }

        /// <summary>
        /// A session was replaced (restore, prestige, new run): the baselines of the model are forgotten so the first refresh reports no delta
        /// (P2-F12). Pass <paramref name="offlineReturn"/> for the refresh that follows an offline return: it reports no delta either.
        /// </summary>
        public void ResetModels(bool offlineReturn = false)
        {
            model.ResetBaseline();
            suppressNext = offlineReturn;
            dirty = true;
            structureRevision = -1;
            quantityRevision = -1;
        }

        /// <summary>Per-frame update. The model refreshes every frame from the cached snapshots (P2-F10); the snapshots rebuild when a revision changed.</summary>
        public void Refresh()
        {
            if (slot < 0)
            {
                return;
            }

            var changed = dirty || structureRevision != session.StructureRevision || quantityRevision != session.QuantityRevision;
            if (changed)
            {
                dirty = false;
                structureRevision = session.StructureRevision;
                quantityRevision = session.QuantityRevision;
                factory = RunSnapshots.Factory(session);
                rates = RunSnapshots.Rates(session);
            }

            RefreshModel();
            if (changed)
            {
                Render();
            }
        }

        private void RefreshModel()
        {
            if (factory == null)
            {
                return;
            }

            var count = session.CopyMachineStates(states);
            if (count > states.Length)
            {
                states = new MachineStateView[count];
                count = session.CopyMachineStates(states);
            }

            var layout = layoutSource?.Invoke();
            if (layout == null)
            {
                // No world layout yet: the model stays idle and the card falls back to the machine's own state (no guessed layout).
                return;
            }

            model.Refresh(layout, factory, states, count, rates, suppressNext);
            suppressNext = false;
        }

        private void Render()
        {
            machine = null;
            if (slot >= 0 && slot < factory.Slots.Count && factory.Slots[slot].Status == SlotStatus.Occupied)
            {
                machine = factory.Machines[factory.Slots[slot].MachineIndex];
            }

            if (machine == null || (shownInstance != Guid.Empty && machine.InstanceId != shownInstance))
            {
                // A removed or replaced machine closes the card and says so (FR-016, U-9).
                CloseGone();
                return;
            }

            shownInstance = machine.InstanceId;
            var presentation = model.Get(new BuildingLocationId(slot));
            var state = presentation.IsOccupied ? presentation.VisualState : SlotGridModel.StateFor(machine.IsActive, machine.Blocker, machine.Phase);
            VisualState = state;
            nameLabel.text = machine.Label + " - level " + machine.Level;
            stateLabel.text = MachineCardCopy.StateLine(state);
            RenderUpgrade(machine, factory.Cash);
            RenderCause(state, presentation);
        }

        private void CloseGone()
        {
            var shell = context?.Shell;
            if (shell == null)
            {
                return;
            }

            shell.CloseMachineCard(true, MachineCardCopy.Gone);
        }

        private void RenderUpgrade(MachineSnapshot current, long cash)
        {
            var next = current.NextUpgrade;
            if (next == null || !next.Available)
            {
                UpgradeButton.text = MachineCardCopy.MaxLevel;
                PanelScaffold.SetDisabled(UpgradeButton, true, RunRejectionCopy.For(RunReasons.LevelMax), inlineLabel);
                return;
            }

            if (next.Affordable)
            {
                UpgradeButton.text = "Upgrade: " + MarketPanelParts.Money(next.Cost);
                PanelScaffold.SetDisabled(UpgradeButton, false, null, null);
                if (inlineLabel.text.StartsWith("Need $", StringComparison.Ordinal))
                {
                    ClearInline();
                }

                return;
            }

            // Cost and shortfall inside the button; the reason is also the tooltip (the accessible description) and the inline line.
            var shortfall = Math.Max(0L, next.Cost - cash);
            UpgradeButton.text = "Upgrade: " + MarketPanelParts.Money(next.Cost) + "\nNeed $" + QuantityFormatter.Format(shortfall);
            // T535 (A-4, U-A6): the shortfall shows once, inside the button (FR-016), which is also its visible reason for touch users (P2-F19). The inline
            // line is for the rejection a press answers with, so no second line moves Details and the controls below Upgrade when the shortfall appears.
            PanelScaffold.SetDisabled(UpgradeButton, true, "Need $" + QuantityFormatter.Format(shortfall) + " more", null);
        }

        private void RenderCause(MachineVisualState state, MachinePresentation presentation)
        {
            string text = null;
            string iconKey = null;
            var resourceIcon = false;
            switch (state)
            {
                case MachineVisualState.MissingInput:
                {
                    // P2-F13: the limiting resource and the "Short on" copy come from the models, nothing is derived here.
                    var resource = presentation.LimitingResourceId;
                    if (resource != null)
                    {
                        text = AttentionModel.ShortOnCopy(SlotSheetText.ResourceLabel(session, resource));
                        if (session.Catalog.Resources.TryGetValue(resource, out var def))
                        {
                            iconKey = def.PresentationKey;
                            resourceIcon = true;
                        }
                    }
                    else
                    {
                        text = MachineCardCopy.ShortOnInput;
                    }

                    break;
                }

                case MachineVisualState.StorageFull:
                    text = MachineCardCopy.StoreFull;
                    iconKey = "F003-UI-ICON-STORAGE";
                    break;
                case MachineVisualState.Waiting:
                    text = MachineCardCopy.WaitingCause;
                    iconKey = PanelIcons.Timer;
                    break;
                case MachineVisualState.Inactive:
                    text = MachineCardCopy.SwitchedOffCause;
                    iconKey = PanelIcons.Info;
                    break;
            }

            causeLabel.text = text ?? string.Empty;
            causeLabel.style.display = text == null ? DisplayStyle.None : DisplayStyle.Flex;
            var sprite = iconKey != null && icons != null ? icons(iconKey) : null;
            causeLabel.style.backgroundImage = sprite == null ? new StyleBackground(StyleKeyword.None) : new StyleBackground(sprite);
            causeLabel.EnableInClassList("mcard__cause--icon", sprite != null);
            causeLabel.EnableInClassList("run-icon--tint-text", sprite != null && !resourceIcon);
        }

        // ------------------------------------------------------------------------------------------------------
        // Actions (each rejection shows inline and changes no state)
        // ------------------------------------------------------------------------------------------------------

        private void ClearInline()
        {
            inlineLabel.text = string.Empty;
            inlineLabel.style.display = DisplayStyle.None;
        }

        private void ShowInline(string text)
        {
            inlineLabel.text = text;
            inlineLabel.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnUpgradeClicked()
        {
            if (machine == null || slot < 0)
            {
                return;
            }

            ClearInline();
            // The card holds the instance it showed. A removed or replaced machine is a stale action: the command rejects it, nothing executes,
            // and the rejection copy shows inline (U-9).
            var result = commands.Upgrade(machine.InstanceId);
            if (result.IsSuccess)
            {
                feedback.Play(MachineResponseKind.UpgradeGlint, slot, ShowHighlight);
            }
            else
            {
                ShowInline(RunRejectionCopy.For(result.ReasonCode));
            }

            // In place: the card refreshes and focus stays on the Upgrade control (it is never rebuilt).
            dirty = true;
            Refresh();
        }

        private void OnDetailsClicked()
        {
            if (context == null || slot < 0)
            {
                return;
            }

            context.Shell.OpenDetails(slot);
        }

        private void OnCloseClicked()
        {
            context?.Shell.CloseMachineCard();
        }

        /// <summary>Shows the static response (FR-034, C5): the highlight class for <see cref="HighlightSeconds"/>. Nothing moves.</summary>
        public void ShowHighlight()
        {
            highlightTimer?.Pause();
            root.AddToClassList("mcard--highlight");
            highlightTimer = root.schedule.Execute(() =>
            {
                root.RemoveFromClassList("mcard--highlight");
                highlightTimer = null;
            });
            highlightTimer.ExecuteLater((long)(HighlightSeconds * 1000f));
        }
    }
}
