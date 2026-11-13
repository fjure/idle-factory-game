using System;
using System.Collections.Generic;
using System.Globalization;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>The eight systems the hub ("Office", OD-9b) lists, in card order (FR-020, research R11).</summary>
    public enum HubCard
    {
        Market = 0,
        Contracts = 1,
        Items = 2,
        Research = 3,
        Industry = 4,
        Overdrive = 5,
        Prestige = 6,
        Settings = 7,
    }

    /// <summary>The fixed facts of the cards: title, the registry panel the card opens, the icon, and whether the card may carry an attention flag (A-6).</summary>
    public static class HubCards
    {
        public const int Count = 8;

        /// <summary>The hub's label (H-02, OD-9b). It is never "Systems".</summary>
        public const string HubLabel = "Office";

        /// <summary>
        /// The icon key of the hub entry: ONE swappable reference (P2-F9, P2-F22). The Art Director approved a new "Office" glyph (T554,
        /// <c>art-source/f006/ui/f006-ui-office</c>) that is not yet imported into the runtime sprite catalog (the scene catalog registration of the F006
        /// rows is T527/A-13), so the contracts glyph stands in. To switch, change this one constant to the imported key; nothing else changes. The hub
        /// icon must stay distinguishable from the Contracts card icon once the new glyph lands: <see cref="IconKeyOf"/> keeps the contracts glyph for the card.
        /// </summary>
        public const string EntryIconKey = "F003-UI-ICON-TAB-CONTRACTS";

        public static string TitleOf(HubCard card) => card.ToString();

        public static string IconKeyOf(HubCard card)
        {
            switch (card)
            {
                case HubCard.Market: return "F003-UI-ICON-TAB-MARKET";
                case HubCard.Contracts: return "F003-UI-ICON-TAB-CONTRACTS";
                case HubCard.Items: return "F003-UI-ICON-TAB-ITEMS";
                case HubCard.Research: return "F003-UI-ICON-TAB-RESEARCH";
                case HubCard.Industry: return "F003-UI-ICON-INDUSTRY";
                case HubCard.Overdrive: return "F003-UI-ICON-OVERDRIVE-READY";
                case HubCard.Prestige: return "F003-UI-ICON-TAB-PRESTIGE";
                default: return RunShellDocument.SettingsIconKey;
            }
        }

        /// <summary>The registry id of the panel the card opens (the Overdrive card activates instead and has none).</summary>
        public static string PanelIdOf(HubCard card)
        {
            switch (card)
            {
                case HubCard.Market: return ShellPanelIds.Market;
                case HubCard.Contracts: return ShellPanelIds.Contracts;
                case HubCard.Items: return ShellPanelIds.Items;
                case HubCard.Research: return ShellPanelIds.Research;
                case HubCard.Industry: return ShellPanelIds.Industry;
                case HubCard.Prestige: return ShellPanelIds.Prestige;
                case HubCard.Settings: return ShellPanelIds.Settings;
                default: return null;
            }
        }

        /// <summary>Only Contracts, Research, Prestige and Overdrive expose an attention condition (A-6); Market, Items, Industry and Settings never carry a flag.</summary>
        public static bool CanFlag(HubCard card) =>
            card == HubCard.Contracts || card == HubCard.Research || card == HubCard.Prestige || card == HubCard.Overdrive;
    }

    /// <summary>
    /// The system-attention state the hub entry dot and the card flags show (A-6, R09: system attention only). The feeds write it every frame (it
    /// is read on the factory screen, with the hub closed); the hub only draws it. Plain C#, no UI Toolkit dependency.
    /// </summary>
    public sealed class HubAttention
    {
        private readonly bool[] flags = new bool[HubCards.Count];
        private int contractsDeliverable;

        /// <summary>Raised when any flag or the deliverable contract count changed.</summary>
        public event Action Changed;

        /// <summary>The number of contracts that can be delivered now (the Contracts card flag is on while it is above 0).</summary>
        public int ContractsDeliverable => contractsDeliverable;

        public bool IsFlagged(HubCard card) => flags[(int)card];

        /// <summary>True when any card carries a flag: the hub entry shows its dot.</summary>
        public bool Any
        {
            get
            {
                for (var i = 0; i < flags.Length; i++)
                {
                    if (flags[i])
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>The number of flagged cards.</summary>
        public int Count
        {
            get
            {
                var count = 0;
                for (var i = 0; i < flags.Length; i++)
                {
                    count += flags[i] ? 1 : 0;
                }

                return count;
            }
        }

        /// <summary>Sets the Contracts flag from the deliverable count (the re-pointed <c>ContractsShellFeed</c> output, T535).</summary>
        public void SetContracts(int deliverable)
        {
            deliverable = Math.Max(0, deliverable);
            if (deliverable == contractsDeliverable && flags[(int)HubCard.Contracts] == deliverable > 0)
            {
                return;
            }

            contractsDeliverable = deliverable;
            flags[(int)HubCard.Contracts] = deliverable > 0;
            Changed?.Invoke();
        }

        /// <summary>Sets the flag of Research, Prestige or Overdrive. A card without an attention condition ignores it (no flag).</summary>
        public void Set(HubCard card, bool flagged)
        {
            if (!HubCards.CanFlag(card) || card == HubCard.Contracts || flags[(int)card] == flagged)
            {
                return;
            }

            flags[(int)card] = flagged;
            Changed?.Invoke();
        }

        /// <summary>Clears every flag (a session replace).</summary>
        public void Clear()
        {
            if (!Any && contractsDeliverable == 0)
            {
                return;
            }

            Array.Clear(flags, 0, flags.Length);
            contractsDeliverable = 0;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Feeds <see cref="HubAttention"/> for Research (a node can be bought, or unspent IP waits in Industry), Prestige (eligible) and Overdrive
    /// (ready). It reads existing state only (no new rule, A-6) and only when the run changed, at most every <see cref="ReadSeconds"/>.
    /// Contracts has its own feed (<see cref="ContractsShellFeed"/>). T536 (T535 Architect F3): the per-frame path is allocation-free (a revision
    /// compare), and so are the Research and Overdrive reads (they walk the catalog and the state directly instead of building snapshots). Only the
    /// Prestige preview allocates, because the domain call returns a new object; it is re-read when the structure changed or the timeline second
    /// moved, so at most once per second.
    /// </summary>
    public sealed class HubAttentionFeed
    {
        /// <summary>The most often the snapshots are re-read, in real seconds.</summary>
        public const float ReadSeconds = 0.25f;

        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly HubAttention attention;
        private long keyStructure = -1;
        private long keyQuantity = -1;
        private long keySecond = -1;
        private long prestigeStructure = -1;
        private long prestigeSecond = -1;
        private bool prestigeEligible;
        private float lastReadTime = float.NegativeInfinity;

        public HubAttentionFeed(RunSession session, RunCommands commands, HubAttention attention)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.attention = attention ?? throw new ArgumentNullException(nameof(attention));
        }

        /// <summary>Forces the next <see cref="Update"/> to read again (a session replace).</summary>
        public void Invalidate()
        {
            keyStructure = -1;
            prestigeStructure = -1;
            lastReadTime = float.NegativeInfinity;
        }

        public void Update(float realtime)
        {
            var second = session.TimelineUtcMs / 1000L;
            var unchanged = keyStructure == session.StructureRevision && keyQuantity == session.QuantityRevision && keySecond == second;
            if (unchanged || (keyStructure >= 0 && realtime - lastReadTime < ReadSeconds))
            {
                return;
            }

            lastReadTime = realtime;
            keyStructure = session.StructureRevision;
            keyQuantity = session.QuantityRevision;
            keySecond = second;
            Read();
        }

        private void Read()
        {
            var research = AnyResearchAffordable();

            // Unspent Influence Points wait in Industry, which the Research view hosts (R-01): they keep the Research flag (the former tab dot).
            if (!research && session.Permanent != null && session.Permanent.Ip > 0)
            {
                research = true;
            }

            attention.Set(HubCard.Research, research);
            var second = keySecond;
            if (prestigeStructure != keyStructure || prestigeSecond != second)
            {
                prestigeStructure = keyStructure;
                prestigeSecond = second;
                var prestige = commands.GetPrestigePreview();
                prestigeEligible = prestige.IsSuccess && prestige.Value != null && prestige.Value.IsEligible;
            }

            attention.Set(HubCard.Prestige, prestigeEligible);
            attention.Set(HubCard.Overdrive, OverdriveReady());
        }

        /// <summary>The rule of <c>RunSnapshots.Research</c> (CanPurchase) without building the snapshot: not at max, prerequisites met, RP covers the cost.</summary>
        private bool AnyResearchAffordable()
        {
            var state = session.State;
            var content = session.Catalog.ResearchInContentOrder;
            for (var i = 0; i < content.Count; i++)
            {
                var def = content[i];
                var level = (int)state.ResearchLevel(def.Id);
                if (level >= def.MaxLevel)
                {
                    continue;
                }

                var met = true;
                for (var p = 0; p < def.Prerequisites.Count && met; p++)
                {
                    var pre = def.Prerequisites[p];
                    met = state.ResearchLevel(pre.ResearchId) >= pre.Level;
                }

                if (met && state.Rp >= def.RpCostTable[level])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The rule of <c>RunSnapshots.Overdrive</c> (Ready) without building the snapshot: unlocked, neither active nor cooling down.</summary>
        private bool OverdriveReady()
        {
            var state = session.State;
            var now = session.TimelineUtcMs;
            return state.IsUnlocked(IdleFactory.Run.Domain.RunState.OverdriveUnlockId) && !state.Overdrive.IsActive(now) && !state.Overdrive.IsCoolingDown(now);
        }
    }

    /// <summary>
    /// The hub ("Office", FR-020 to FR-022, research R11, OD-9b): a card for each system with an icon, a one-line status and, for Contracts, Research,
    /// Prestige and Overdrive only, an attention flag. It is a registered shell panel, so the shell shows it in the bottom sheet (phone portrait) or
    /// the side panel (other classes) like Settings, and the system views open through <see cref="ShellPanelRegistry"/> unchanged. It reads the run
    /// through snapshots and changes no state: Overdrive activates through the shell's own control path. Built on the scaffold's classes and rows
    /// (P2-F16: the scaffold root is a full-screen fill, so the hub overrides it with <c>.scaffold.hub</c> like the machine card). Rows are 48 dp
    /// (C4); a disabled row shows its reason as text (P2-F19); nothing animates (C5). The shell holds the Back and Close.
    /// </summary>
    public sealed class HubPanel : IShellPanel
    {
        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly Func<string, Sprite> icons;
        private readonly VisualElement root;
        private readonly VisualElement list;
        private readonly VisualElement state;
        private readonly Label stateMark;
        private readonly Label stateTitle;
        private readonly Label stateMessage;
        private readonly Button stateRetry;
        private readonly Row[] rows = new Row[HubCards.Count];
        private ShellPanelContext context;
        private long keyStructure = -1;
        private long keyQuantity = -1;
        private long keySecond = -1;
        private int keyAttention = -1;
        private bool visible;
        private bool failed;

        public HubPanel(RunSession session, RunCommands commands, VisualTreeAsset tree, StyleSheet styles, StyleSheet scaffoldStyles = null,
            Func<string, Sprite> icons = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            if (tree == null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            this.icons = icons;
            var instance = tree.Instantiate();
            root = instance.Q<VisualElement>("hub-panel") ?? throw new InvalidOperationException("HubPanel.uxml has no hub-panel.");
            if (scaffoldStyles != null)
            {
                root.styleSheets.Add(scaffoldStyles);
            }

            if (styles != null)
            {
                root.styleSheets.Add(styles);
            }

            list = Require<VisualElement>("hub-list");
            state = Require<VisualElement>("hub-state");
            stateMark = Require<Label>("hub-state-mark");
            stateTitle = Require<Label>("hub-state-title");
            stateMessage = Require<Label>("hub-state-message");
            stateRetry = Require<Button>("hub-state-retry");
            stateRetry.clicked += () => Refresh(true);
            stateRetry.tooltip = "Retry";
            for (var i = 0; i < rows.Length; i++)
            {
                rows[i] = new Row(this, (HubCard)i);
                list.Add(rows[i].Root);
            }

            ShowState(null);
        }

        public string Id => ShellPanelIds.Hub;

        public string Title => HubCards.HubLabel;

        public VisualElement Root => root;

        /// <summary>The scaffold state of the hub: the empty or error template shown instead of the cards, or null while the cards show.</summary>
        public string StateKind { get; private set; }

        /// <summary>The card button of a system.</summary>
        public Button CardButton(HubCard card) => rows[(int)card].Button;

        /// <summary>The visible status line of a card.</summary>
        public string StatusOf(HubCard card) => rows[(int)card].Status.text;

        /// <summary>True when the card shows its attention flag.</summary>
        public bool IsFlagShown(HubCard card) => rows[(int)card].Flag.style.display == DisplayStyle.Flex;

        /// <summary>The reason line shown under a disabled card, or empty.</summary>
        public string ReasonOf(HubCard card) => rows[(int)card].Reason.text;

        public Button RetryButton => stateRetry;

        public Label StateTitleLabel => stateTitle;

        /// <summary>Forces a failure for the error state (a test seam: the snapshot read throws in production only on a defect).</summary>
        public Func<bool> FailureProbe { get; set; }

        public void OnShown(ShellPanelContext shown)
        {
            context = shown;
            visible = true;
            Refresh(true);
        }

        public void OnHidden()
        {
            visible = false;
        }

        public void Refresh() => Refresh(false);

        private void Refresh(bool force)
        {
            if (!visible || context == null)
            {
                return;
            }

            var second = session.TimelineUtcMs / 1000L;
            var attention = context.Shell.Attention;
            var attentionKey = attention.Count * 1000 + attention.ContractsDeliverable;
            if (!force && !failed && keyStructure == session.StructureRevision && keyQuantity == session.QuantityRevision && keySecond == second &&
                keyAttention == attentionKey)
            {
                return;
            }

            keyStructure = session.StructureRevision;
            keyQuantity = session.QuantityRevision;
            keySecond = second;
            keyAttention = attentionKey;
            try
            {
                if (FailureProbe != null && FailureProbe())
                {
                    throw new InvalidOperationException("hub read failed");
                }

                var available = 0;
                for (var i = 0; i < rows.Length; i++)
                {
                    available += rows[i].Update(context.Shell, attention) ? 1 : 0;
                }

                failed = false;
                if (available == 0)
                {
                    ShowState("empty");
                }
                else
                {
                    ShowState(null);
                }
            }
            catch (Exception)
            {
                failed = true;
                ShowState("error");
            }
        }

        private void ShowState(string kind)
        {
            StateKind = kind;
            var showState = kind != null;
            state.style.display = showState ? DisplayStyle.Flex : DisplayStyle.None;
            list.style.display = showState ? DisplayStyle.None : DisplayStyle.Flex;
            state.EnableInClassList("scaffold__state--shown", showState);
            state.EnableInClassList("scaffold__state--empty", kind == "empty");
            state.EnableInClassList("scaffold__state--error", kind == "error");
            if (kind == "error")
            {
                stateMark.text = "!";
                stateTitle.text = "Something went wrong";
                stateMessage.text = "The Office could not read the factory. Try again.";
                stateRetry.style.display = DisplayStyle.Flex;
                stateRetry.focusable = true;
            }
            else
            {
                stateMark.text = "i";
                stateTitle.text = "Nothing here yet";
                stateMessage.text = "No system is available right now.";
                stateRetry.style.display = DisplayStyle.None;
                stateRetry.focusable = false;
            }
        }

        private T Require<T>(string name) where T : VisualElement =>
            root.Q<T>(name) ?? throw new InvalidOperationException("HubPanel.uxml has no " + name + ".");

        /// <summary>Opens the system of a card as a press does: the registry view, Settings, or the Overdrive activation. Returns whether it acted.</summary>
        public bool Activate(HubCard card)
        {
            var row = rows[(int)card];
            if (PanelScaffold.IsDisabled(row.Button) || context == null)
            {
                return false;
            }

            var shell = context.Shell;
            switch (card)
            {
                case HubCard.Market: shell.SelectTab(ShellTab.Market); break;
                case HubCard.Contracts: shell.SelectTab(ShellTab.Contracts); break;
                case HubCard.Items: shell.SelectTab(ShellTab.Items); break;
                case HubCard.Research: shell.SelectTab(ShellTab.Research); break;
                case HubCard.Prestige: shell.SelectTab(ShellTab.Prestige); break;
                case HubCard.Industry: shell.OpenIndustry(); break;
                case HubCard.Settings: shell.OpenSettings(); break;
                default:
                    shell.ActivateOverdrive();
                    keyAttention = -1;
                    break;
            }

            if (card != HubCard.Overdrive)
            {
                shell.NoteOpenedFromHub();
            }

            return true;
        }

        private static string Mmss(long ms)
        {
            var total = Math.Max(0L, (ms + 999L) / 1000L);
            return (total / 60).ToString(CultureInfo.InvariantCulture) + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private sealed class Row
        {
            private readonly HubPanel owner;
            private readonly HubCard card;

            public Row(HubPanel owner, HubCard card)
            {
                this.owner = owner;
                this.card = card;
                Root = new VisualElement { name = "hub-card-" + card.ToString().ToLowerInvariant() + "-row" };
                Root.AddToClassList("hub__row");
                Button = new Button { name = "hub-card-" + card.ToString().ToLowerInvariant(), focusable = true };
                Button.AddToClassList("run-control");
                Button.AddToClassList(PanelScaffold.RowClass);
                Button.AddToClassList("hub__card");
                Icon = new VisualElement { name = Button.name + "-icon", pickingMode = PickingMode.Ignore };
                Icon.AddToClassList("run-icon");
                Icon.AddToClassList("run-icon--tint-text");
                var sprite = owner.icons?.Invoke(HubCards.IconKeyOf(card));
                if (sprite != null)
                {
                    Icon.style.backgroundImage = new StyleBackground(sprite);
                }

                var body = new VisualElement { pickingMode = PickingMode.Ignore };
                body.AddToClassList("hub__body");
                Title = new Label(HubCards.TitleOf(card)) { name = Button.name + "-title", pickingMode = PickingMode.Ignore };
                Title.AddToClassList("hub__title");
                Status = new Label { name = Button.name + "-status", pickingMode = PickingMode.Ignore };
                Status.AddToClassList("hub__status");
                body.Add(Title);
                body.Add(Status);
                Flag = new Label("• Attention") { name = Button.name + "-flag", pickingMode = PickingMode.Ignore };
                Flag.AddToClassList("hub__flag");
                Flag.style.display = DisplayStyle.None;
                Button.Add(Icon);
                Button.Add(body);
                Button.Add(Flag);
                Button.clicked += () => owner.Activate(card);
                Reason = new Label { name = Button.name + "-reason" };
                Reason.AddToClassList("scaffold__reason");
                Reason.style.display = DisplayStyle.None;
                Root.Add(Button);
                Root.Add(Reason);
            }

            public VisualElement Root { get; }

            public Button Button { get; }

            public VisualElement Icon { get; }

            public Label Title { get; }

            public Label Status { get; }

            public Label Flag { get; }

            public Label Reason { get; }

            /// <summary>Redraws the card. Returns whether the card opens a registered system view (Overdrive has none and returns false).</summary>
            public bool Update(RunShellDocument shell, HubAttention attention)
            {
                var session = owner.session;
                var panelId = HubCards.PanelIdOf(card);
                var registered = panelId == null || shell.Panels.IsRegistered(panelId);
                string status;
                string reason = null;
                switch (card)
                {
                    case HubCard.Market:
                    {
                        var inventory = session.State.Inventory;
                        var percent = inventory.Capacity <= 0 ? 0 : (int)Math.Min(100L, inventory.Total * 100L / inventory.Capacity);
                        status = "Store " + percent.ToString(CultureInfo.InvariantCulture) + "% full";
                        break;
                    }

                    case HubCard.Contracts:
                        status = attention.ContractsDeliverable > 0
                            ? attention.ContractsDeliverable.ToString(CultureInfo.InvariantCulture) + " ready to deliver"
                            : "Nothing ready to deliver";
                        break;
                    case HubCard.Items:
                        status = QuantityFormatter.Format(RunSnapshots.Items(session).TotalCount) + " owned";
                        break;
                    case HubCard.Research:
                        status = "RP " + QuantityFormatter.Format(session.ReadRp()) + (attention.IsFlagged(HubCard.Research) ? ", something to buy" : string.Empty);
                        break;
                    case HubCard.Industry:
                        status = "IP " + QuantityFormatter.Format(session.Permanent == null ? 0L : session.Permanent.Ip);
                        break;
                    case HubCard.Overdrive:
                    {
                        var overdrive = RunSnapshots.Overdrive(session);
                        switch (overdrive.State)
                        {
                            case OverdriveStage.Ready: status = "Ready"; break;
                            case OverdriveStage.Active: status = "On, " + Mmss(overdrive.RemainingMs) + " left"; reason = "Overdrive is already on"; break;
                            case OverdriveStage.CoolingDown: status = "Cooling down, " + Mmss(overdrive.RemainingMs); reason = "Overdrive is cooling down"; break;
                            default:
                                // T536 (T535 Product Owner note): a locked card states the condition, in the shared unlock wording.
                                status = "Locked";
                                reason = "Overdrive unlocks after " + UnlockCopy.After(session.Catalog, overdrive.Unlock) + ".";
                                break;
                        }

                        break;
                    }

                    case HubCard.Prestige:
                    {
                        var preview = owner.commands.GetPrestigePreview();
                        var eligible = preview.IsSuccess && preview.Value != null && preview.Value.IsEligible;
                        // T536 (T535 Product Owner note): a card that is not ready says what it waits for, by the plain Factory Value wording of the panel.
                        status = eligible ? "Ready" : preview.IsSuccess && preview.Value != null
                            ? "Not ready yet: needs Factory Value " + QuantityFormatter.Format(preview.Value.Threshold)
                            : "Not ready yet";
                        break;
                    }

                    default:
                        status = "Display and comfort";
                        break;
                }

                if (!registered)
                {
                    status = "Not available right now";
                    reason = "This system could not be opened";
                }

                Status.text = status;
                var flagged = HubCards.CanFlag(card) && attention.IsFlagged(card);
                Flag.style.display = flagged ? DisplayStyle.Flex : DisplayStyle.None;
                var disabled = !registered || reason != null;
                PanelScaffold.SetDisabled(Button, disabled, reason, Reason);
                // The accessible name (tooltip) of a card: its title, status and flag; a disabled card appends its reason (SetDisabled wrote it).
                if (!disabled)
                {
                    Button.tooltip = HubCards.TitleOf(card) + ". " + status + (flagged ? ". Needs attention" : string.Empty);
                }
                else
                {
                    Button.tooltip = HubCards.TitleOf(card) + ". " + status + (flagged ? ". Needs attention" : string.Empty) + ". Unavailable: " + reason;
                }

                // Overdrive opens no registry view, so it never makes the hub non-empty on its own.
                return panelId != null && registered;
            }
        }
    }
}
