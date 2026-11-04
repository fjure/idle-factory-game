using System;
using System.Collections.Generic;
using System.Globalization;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>What the Prestige surface shows (contracts/ui-surfaces.md, Prestige).</summary>
    public enum PrestigePanelState
    {
        /// <summary>Eligible: Confirm is enabled.</summary>
        Ready = 0,

        /// <summary>Below the threshold: Confirm is disabled and the missing Factory Value is shown.</summary>
        BelowThreshold = 1,

        /// <summary>The durable transition (Save V2) is being written: "Saving\u2026".</summary>
        Saving = 2,

        /// <summary>The durable transition failed: the old run is unchanged and Retry is offered.</summary>
        Failed = 3,

        /// <summary>The preview could not be read (value limit or no permanent state).</summary>
        Unavailable = 4,
    }

    /// <summary>
    /// The Prestige tab ("Factory Sale", T117, FR-020 to FR-022, FR-024, FR-032, FR-033, US4 scenarios 1 to 3). It shows the Factory Value
    /// breakdown, the IP preview, the missing value below the threshold, the reset and preserve lists, Confirm and Cancel, "Saving\u2026"
    /// during the durable transition and, on failure, "Factory Sale was not completed - your run is unchanged" with Retry. The panel reads
    /// <see cref="RunCommands.GetPrestigePreview"/> and mutates only through <see cref="RunCommands"/>: <c>ConfirmPrestige</c> builds the
    /// pending candidate, <c>PromotePrestige</c> is the durable promotion (and, when repeated, the idempotent retry, R09) and
    /// <c>CancelPendingTransition</c> drops a pending candidate. It holds no economy rule. Confirm opens the shell's shared confirmation
    /// dialog (UX-2), which a rotation cancels without confirming anything.
    /// </summary>
    public sealed class PrestigePresenter : IShellPanel
    {
        public const string PanelTitle = "Factory Sale";
        public const string SavingText = "Saving\u2026";
        public const string FailureText = "Factory Sale was not completed \u2014 your run is unchanged";
        public const string ConfirmTitle = "Sell the factory?";

        /// <summary>The delay between the "Saving\u2026" frame and the durable write, so the player sees the state before the write blocks the frame.</summary>
        public const long SavingDelayMs = 40;

        private readonly RunCommands commands;
        private readonly RunSession session;
        private readonly Label status;
        private readonly Label valueCash;
        private readonly Label valueStock;
        private readonly Label valueMachines;
        private readonly Label valueExpansion;
        private readonly Label valueTotal;
        private readonly Label threshold;
        private readonly Label ipAward;
        private readonly Label ipAfter;
        private readonly Label missing;
        private readonly VisualElement industryCard;
        private readonly Label saleBanner;
        private readonly Label industryLine;
        private string saleBannerText = string.Empty;
        private readonly Func<string, Sprite> icons;
        private readonly VisualElement resetCard;
        private readonly VisualElement resetList;
        private readonly VisualElement preserveList;
        private readonly VisualElement savingCard;
        private readonly VisualElement failureCard;
        private readonly Label failure;
        private readonly Label failureDetail;
        private readonly Label feedback;
        private readonly List<Label> resetRows = new List<Label>();
        private readonly List<Label> preserveRows = new List<Label>();
        private IDisposable exactValue;
        private IDisposable thresholdExactValue;
        private RunShellDocument attachedShell;
        private RunShellDocument shell;
        private PrestigePreview preview;
        private PrestigePanelState state = PrestigePanelState.Unavailable;
        private string failureCode;
        private bool confirmOpen;
        private bool forceRefresh = true;
        private long keyStructure = -1;
        private long keyQuantity = -1;

        /// <summary>
        /// Builds the panel from <c>PrestigePanel.uxml</c> (<paramref name="tree"/>) and <c>PrestigePanel.uss</c> (<paramref name="styles"/>).
        /// Mount it with <c>ShellPanelRegistry.Register</c>; it needs no scene wiring.
        /// </summary>
        public PrestigePresenter(RunSession session, RunCommands commands, VisualTreeAsset tree, StyleSheet styles = null,
            Func<string, Sprite> icons = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.icons = icons;
            if (tree == null) throw new ArgumentNullException(nameof(tree));

            var container = tree.Instantiate();
            var root = container.Q<VisualElement>("prestige-panel") ?? container;
            root.RemoveFromHierarchy();
            if (styles != null)
            {
                root.styleSheets.Add(styles);
            }

            Root = root;
            status = Required<Label>("prestige-status");
            valueCash = Required<Label>("prestige-value-cash");
            valueStock = Required<Label>("prestige-value-stock");
            valueMachines = Required<Label>("prestige-value-machines");
            valueExpansion = Required<Label>("prestige-value-expansion");
            valueTotal = Required<Label>("prestige-value-total");
            threshold = Required<Label>("prestige-threshold");
            ipAward = Required<Label>("prestige-ip-award");
            ipAfter = Required<Label>("prestige-ip-after");
            missing = Required<Label>("prestige-missing");
            industryCard = Required<VisualElement>("prestige-industry-card");
            saleBanner = Required<Label>("prestige-sale-banner");
            industryLine = Required<Label>("prestige-industry-line");
            IndustryButton = Required<Button>("prestige-industry-button");
            IndustryButton.clicked += OnIndustryClicked;
            resetCard = Required<VisualElement>("prestige-reset-card");
            resetList = Required<VisualElement>("prestige-reset-list");
            preserveList = Required<VisualElement>("prestige-preserve-list");
            savingCard = Required<VisualElement>("prestige-saving-card");
            failureCard = Required<VisualElement>("prestige-failure-card");
            failure = Required<Label>("prestige-failure");
            failureDetail = Required<Label>("prestige-failure-detail");
            feedback = Required<Label>("prestige-feedback");
            ConfirmButton = Required<Button>("prestige-confirm");
            RetryButton = Required<Button>("prestige-retry");
            CancelButton = Required<Button>("prestige-cancel");
            SavingLabel = Required<Label>("prestige-saving");
            ConfirmButton.clicked += OnConfirmClicked;
            RetryButton.clicked += OnRetryClicked;
            CancelButton.clicked += OnCancelClicked;
            PanelIcons.Mark(missing, icons, PanelIcons.Lock);
            PanelIcons.Mark(failure, icons, PanelIcons.Warning);
            PanelIcons.Mark(feedback, icons, PanelIcons.Warning);
            PanelIcons.Mark(valueTotal, icons, PanelIcons.FactoryValue);
            PanelIcons.Mark(ipAward, icons, PanelIcons.IndustryPoints);

            // A wrapped label inside a bounded scroll view is measured at most as tall as the viewport (wiki: UI Toolkit HUD layout
            // pitfalls), so each label's minimum height is set to its wrapped height by a geometry handler.
            foreach (var label in root.Query<Label>().ToList())
            {
                label.RegisterCallback<GeometryChangedEvent>(FitWrapped);
            }
        }

        public string Id => ShellPanelIds.Prestige;
        public string Title => PanelTitle;
        public VisualElement Root { get; }

        public Button ConfirmButton { get; }
        public Button RetryButton { get; }
        public Button CancelButton { get; }

        /// <summary><c>prestige-industry-button</c>: "Spend N IP in Industry", opens Research scrolled to Industry (R-01).</summary>
        public Button IndustryButton { get; }
        public Label SavingLabel { get; }

        /// <summary>The post-sale banner ("Factory Sale complete: +N IP.") shown in the Industry card until the panel is left or the next sale starts.</summary>
        public Label SaleBannerLabel => saleBanner;
        public Label IndustryLineLabel => industryLine;
        public VisualElement IndustryCard => industryCard;

        public PrestigePanelState State => state;
        public PrestigePreview Preview => preview;

        /// <summary>The reason code of the last failed promotion (for example <c>persistence.failed</c>), or null.</summary>
        public string FailureCode => failureCode;

        public bool IsConfirmationOpen => confirmOpen;

        public Label StatusLabel => status;
        public Label ValueCashLabel => valueCash;
        public Label ValueStockLabel => valueStock;
        public Label ValueMachinesLabel => valueMachines;
        public Label ValueExpansionLabel => valueExpansion;
        public Label ValueTotalLabel => valueTotal;
        public Label ThresholdLabel => threshold;
        public Label IpAwardLabel => ipAward;
        public Label IpAfterLabel => ipAfter;
        public Label MissingLabel => missing;
        public VisualElement SavingCard => savingCard;
        public VisualElement FailureCard => failureCard;
        public Label FailureLabel => failure;
        public Label FailureDetailLabel => failureDetail;
        public Label FeedbackLabel => feedback;
        public IReadOnlyList<Label> ResetRows => resetRows;
        public IReadOnlyList<Label> PreserveRows => preserveRows;

        public void OnShown(ShellPanelContext context)
        {
            shell = context?.Shell;
            if (shell != null && !ReferenceEquals(attachedShell, shell))
            {
                exactValue?.Dispose();
                thresholdExactValue?.Dispose();
                attachedShell = shell;
                exactValue = shell.AttachExactValue(ConfirmButton, ExactValueText);
                thresholdExactValue = shell.AttachExactValue(threshold, ThresholdExactText);
            }

            ClearFeedback();
            Invalidate();
            Refresh();
        }

        public void OnHidden()
        {
            ClearFeedback();
            saleBannerText = string.Empty;
        }

        /// <summary>Forces the next <see cref="Refresh"/> to redraw (for a state change that bumped no revision).</summary>
        public void Invalidate() => forceRefresh = true;

        /// <summary>
        /// Redraws when the structure or the quantities changed. While "Saving\u2026" shows, the panel holds its state until the write
        /// returns. A pending transition that this panel did not start (for example one left by a failed attempt) shows as failed with
        /// Retry, because repeating the promotion is idempotent (R09).
        /// </summary>
        public void Refresh()
        {
            if (state == PrestigePanelState.Saving)
            {
                return;
            }

            if (!forceRefresh && session.StructureRevision == keyStructure && session.QuantityRevision == keyQuantity)
            {
                return;
            }

            forceRefresh = false;
            keyStructure = session.StructureRevision;
            keyQuantity = session.QuantityRevision;
            Render();
        }

        /// <summary>
        /// Cancels an open Prestige confirmation because of a rotation, without prestiging (UX-2, UX-9). Register it with the
        /// bootstrap's transient actions. Returns whether something was open.
        /// </summary>
        public bool CancelForRotation()
        {
            if (!confirmOpen || shell == null)
            {
                return false;
            }

            return shell.CancelConfirmationForRotation();
        }

        // ---------------------------------------------------------------------------------------------------
        // Rendering
        // ---------------------------------------------------------------------------------------------------

        private void Render()
        {
            var result = commands.GetPrestigePreview();
            if (!result.IsSuccess)
            {
                preview = null;
                state = PrestigePanelState.Unavailable;
                RenderUnavailable(result.ReasonCode);
                return;
            }

            preview = result.Value;
            if (session.PendingTransition != null)
            {
                state = PrestigePanelState.Failed;
            }
            else
            {
                state = preview.IsEligible ? PrestigePanelState.Ready : PrestigePanelState.BelowThreshold;
                failureCode = null;
            }

            RenderPreview(preview);
            RenderState();
            RenderIndustryPointer();
        }

        // R-01: the sale's reward is spent in Industry, which lives under the Research graph. While IP is unspent the panel says so and offers
        // a button that opens Research at Industry; right after a sale the same card carries the "complete" banner.
        private void RenderIndustryPointer()
        {
            var ip = session.Permanent == null ? 0L : session.Permanent.Ip;
            var banner = saleBannerText.Length > 0;
            var show = (ip > 0 || banner) && state != PrestigePanelState.Saving;
            SetDisplayed(industryCard, show);
            SetText(saleBanner, saleBannerText);
            SetDisplayed(saleBanner, banner);
            SetText(industryLine, ip > 0
                ? "You have " + QuantityFormatter.Format(ip) + " IP to spend. Industry upgrades are permanent and make every run start stronger."
                : "No IP to spend right now.");
            IndustryButton.text = ip > 0 ? "Spend " + QuantityFormatter.Format(ip) + " IP in Industry" : "Open Industry";
        }

        private void OnIndustryClicked()
        {
            ClearFeedback();
            if (shell == null)
            {
                return;
            }

            shell.OpenTabAtSection(ShellTab.Research, ShellSections.Industry);
        }

        private void RenderUnavailable(string reasonCode)
        {
            SetText(status, PanelTitle);
            SetText(valueCash, string.Empty);
            SetText(valueStock, string.Empty);
            SetText(valueMachines, string.Empty);
            SetText(valueExpansion, string.Empty);
            SetText(valueTotal, string.Empty);
            SetText(threshold, string.Empty);
            SetText(ipAward, string.Empty);
            SetText(ipAfter, string.Empty);
            SetText(missing, RunRejectionCopy.For(reasonCode));
            PanelIcons.Mark(missing, icons, PanelIcons.Warning);
            SetDisplayed(missing, true);
            SetDisplayed(savingCard, false);
            SetDisplayed(failureCard, false);
            SetDisplayed(RetryButton, false);
            RunShellDocument.SetSoftDisabled(ConfirmButton, true);
            SetDisplayed(ConfirmButton, true);
            SetDisplayed(industryCard, false);
        }

        private void RenderPreview(PrestigePreview data)
        {
            var value = data.FactoryValue;
            SetText(valueCash, "Cash: " + QuantityFormatter.Format(value.Cash));
            SetText(valueStock, "Stock value: " + QuantityFormatter.Format(value.StockValue));
            SetText(valueMachines, "Machines: " + QuantityFormatter.Format(value.MachineInvestedCash));
            SetText(valueExpansion, "Expansion: " + QuantityFormatter.Format(value.ExpansionInvestedCash));
            SetText(valueTotal, "Total Factory Value: " + QuantityFormatter.Format(value.Total));
            SetText(threshold, ThresholdText(data, PrestigeCount));
            threshold.tooltip = ThresholdExact(data, PrestigeCount);
            SetText(ipAward, "Sale award: +" + QuantityFormatter.Format(data.IpAward) + " IP");
            SetText(ipAfter, "You have " + QuantityFormatter.Format(data.CurrentIp) + " IP, so " +
                QuantityFormatter.Format(data.IpAfter) + " IP after the sale. Run " +
                data.NextRunNumber.ToString(CultureInfo.InvariantCulture) + " starts with " +
                MarketPanelParts.Money(data.NextRunStartingCash) + ".");
            FillList(resetList, resetRows, data.Reset, key => ResetCopy(key, data));
            FillList(preserveList, preserveRows, data.Preserve, key => PreserveCopy(key, data));
        }

        private void RenderState()
        {
            var saving = state == PrestigePanelState.Saving;
            var failed = state == PrestigePanelState.Failed;
            var below = state == PrestigePanelState.BelowThreshold;
            SetText(status, saving ? SavingText : failed ? FailureText : below ? "Not ready to sell yet" : "Ready to sell");
            SetDisplayed(savingCard, saving);
            SetDisplayed(failureCard, failed);
            if (failed)
            {
                SetText(failure, FailureText);
                SetText(failureDetail, FailureDetail(failureCode));
            }

            SetDisplayed(missing, below && preview != null);
            if (below && preview != null)
            {
                SetText(missing, "Missing Factory Value: " + QuantityFormatter.Format(preview.MissingFactoryValue));
                PanelIcons.Mark(missing, icons, PanelIcons.Lock);
            }

            SetDisplayed(ConfirmButton, !failed);
            RunShellDocument.SetSoftDisabled(ConfirmButton, below || saving || preview == null);
            ConfirmButton.text = "Sell the factory";
            SetDisplayed(RetryButton, failed);
            RunShellDocument.SetSoftDisabled(RetryButton, saving);
            RunShellDocument.SetSoftDisabled(CancelButton, saving);
        }

        private static void FillList(VisualElement host, List<Label> rows, IReadOnlyList<string> keys, Func<string, string> copy)
        {
            if (rows.Count == keys.Count)
            {
                for (var i = 0; i < keys.Count; i++)
                {
                    SetText(rows[i], copy(keys[i]));
                }

                return;
            }

            host.Clear();
            rows.Clear();
            for (var i = 0; i < keys.Count; i++)
            {
                var row = new Label { name = host.name + "-" + keys[i] };
                row.RegisterCallback<GeometryChangedEvent>(FitWrapped);
                row.text = copy(keys[i]);
                rows.Add(row);
                host.Add(row);
            }
        }

        private static string ResetCopy(string key, PrestigePreview data)
        {
            switch (key)
            {
                case PrestigeTransition.ResetCash: return "Cash";
                case PrestigeTransition.ResetStock: return "Stored resources";
                case PrestigeTransition.ResetMachines: return "Machines and their levels";
                case PrestigeTransition.ResetPriorities: return "Machine priority order";
                case PrestigeTransition.ResetAutoSell: return "Auto-sell settings";
                case PrestigeTransition.ResetExpansion: return "Storage and factory upgrades";
                case PrestigeTransition.ResetContracts:
                    return data.ActiveContractOfferId != null ? "Contracts, including the active one (no reward, no penalty)" : "Contracts and their progress";
                case PrestigeTransition.ResetResearch: return "Research levels";
                case PrestigeTransition.ResetResearchPoints: return "Research points";
                case PrestigeTransition.ResetEffects:
                    return data.ActiveEffectCount > 0
                        ? "Running Item effects (" + data.ActiveEffectCount.ToString(CultureInfo.InvariantCulture) + " now)"
                        : "Running Item effects";
                case PrestigeTransition.ResetOverdrive: return "Overdrive timers";
                case PrestigeTransition.ResetUnlocks: return "Unlocks (they start again)";
                default: return key;
            }
        }

        private static string PreserveCopy(string key, PrestigePreview data)
        {
            switch (key)
            {
                case PrestigeTransition.PreserveIp: return "Influence Points, plus the sale award";
                case PrestigeTransition.PreserveIndustry: return "Industry upgrades";
                case PrestigeTransition.PreserveItems:
                    return "Items (" + QuantityFormatter.Format(data.PreservedItemCount) + " owned)";
                default: return key;
            }
        }

        private static string FailureDetail(string code)
        {
            switch (code)
            {
                case RunPersistenceReasons.TransitionCommitted:
                    return "The sale was already saved. Retry to finish it.";
                case RunReasons.TransitionPending:
                    return "The sale is waiting to be saved. Retry to continue, or cancel to keep playing.";
                default:
                    return "The next run could not be saved. Your factory, Cash and Influence Points are as they were. Retry, or cancel to keep playing.";
            }
        }

        /// <summary>The number of the sale the player is working toward: completed prestiges + 1 (the first sale reads 1). It keeps rising beyond the maximum threshold index.</summary>
        private long PrestigeCount => session.Permanent == null ? 0 : session.Permanent.PrestigeCount;

        private static string SaleNumber(long prestigeCount) => (prestigeCount < long.MaxValue ? prestigeCount + 1 : prestigeCount).ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// <c>prestige-threshold</c> (T333, FR-018): the current threshold, the next one (the same value once the maximum index is
        /// reached or when the content has no growth) and the threshold index, all read from <see cref="PrestigePreview"/>.
        /// </summary>
        public static string ThresholdText(PrestigePreview data, long prestigeCount) =>
            "Needed to sell: " + QuantityFormatter.Format(data.Threshold) + ". Next sale needs " + QuantityFormatter.Format(data.NextThreshold) +
            ". Factory Sale #" + SaleNumber(prestigeCount) + ".";

        public static string ThresholdExact(PrestigePreview data, long prestigeCount) =>
            "Needed to sell " + QuantityFormatter.FormatExact(data.Threshold) + ", next sale needs " + QuantityFormatter.FormatExact(data.NextThreshold) +
            ", Factory Sale #" + SaleNumber(prestigeCount);

        /// <summary>The exact-value text of the <c>prestige-threshold</c> line itself (hover, focus-less tap and long press on the line).</summary>
        private string ThresholdExactText() => preview == null ? string.Empty : ThresholdExact(preview, PrestigeCount) + ".";

        private string ExactValueText()
        {
            var data = preview;
            if (data == null)
            {
                return string.Empty;
            }

            return "Factory Value " + QuantityFormatter.FormatExact(data.FactoryValue.Total) + " of " +
                QuantityFormatter.FormatExact(data.Threshold) + " needed. Next sale needs " +
                QuantityFormatter.FormatExact(data.NextThreshold) + ". Factory Sale #" + SaleNumber(PrestigeCount) + ". Award " +
                QuantityFormatter.FormatExact(data.IpAward) + " IP.";
        }

        // ---------------------------------------------------------------------------------------------------
        // Commands
        // ---------------------------------------------------------------------------------------------------

        private void OnConfirmClicked()
        {
            ClearFeedback();
            if (state == PrestigePanelState.Saving)
            {
                return;
            }

            if (preview == null || !preview.IsEligible)
            {
                // Disabled with its reason visible; the command would re-validate and reject the same way.
                var reason = preview?.Reason ?? RunReasons.CommandInvalid;
                ShowFeedback(RunRejectionCopy.For(reason));
                return;
            }

            if (shell == null)
            {
                return;
            }

            confirmOpen = true;
            var opened = shell.RequestConfirmation(new ConfirmationRequest
            {
                Title = ConfirmTitle,
                Message = ConfirmMessage(preview),
                ConfirmLabel = "Sell the factory",
                CancelLabel = "Keep playing",
                Destructive = true,
                Invoker = ConfirmButton,
                OnConfirm = ConfirmSale,
                OnCancel = reason => confirmOpen = false,
            });
            if (!opened)
            {
                confirmOpen = false;
            }
        }

        private static string ConfirmMessage(PrestigePreview data) =>
            "Sell the factory for +" + QuantityFormatter.Format(data.IpAward) + " Influence Points. The run resets and the next run starts with " +
            MarketPanelParts.Money(data.NextRunStartingCash) + ". Items and Industry upgrades stay.";

        private void ConfirmSale()
        {
            confirmOpen = false;
            var confirmed = commands.ConfirmPrestige();
            if (!confirmed.IsSuccess)
            {
                ShowFeedback(RunRejectionCopy.For(confirmed.ReasonCode));
                Invalidate();
                Refresh();
                return;
            }

            BeginSaving();
        }

        private void OnRetryClicked()
        {
            ClearFeedback();
            if (state == PrestigePanelState.Saving || session.PendingTransition == null)
            {
                if (session.PendingTransition == null)
                {
                    Invalidate();
                    Refresh();
                }

                return;
            }

            BeginSaving();
        }

        private void OnCancelClicked()
        {
            ClearFeedback();
            if (state == PrestigePanelState.Saving)
            {
                return;
            }

            if (session.PendingTransition != null)
            {
                var cancelled = commands.CancelPendingTransition();
                if (!cancelled.IsSuccess)
                {
                    failureCode = cancelled.ReasonCode;
                    ShowFeedback(cancelled.ReasonCode == RunPersistenceReasons.TransitionCommitted
                        ? "The sale was already saved, so it cannot be cancelled. Retry to finish it."
                        : "Cancelling could not be confirmed. Retry the sale, or try again.");
                    Invalidate();
                    Refresh();
                    return;
                }

                shell?.ShowNotice("Factory Sale cancelled. Your run is unchanged.", ShellNoticeKind.Info);
                Invalidate();
                Refresh();
                shell?.FocusWhenReady(ConfirmButton);
                return;
            }

            shell?.CloseSheet();
        }

        private void BeginSaving()
        {
            state = PrestigePanelState.Saving;
            failureCode = null;
            saleBannerText = string.Empty;
            RenderState();
            SetDisplayed(industryCard, false);
            if (Root.panel == null)
            {
                Promote();
                return;
            }

            // Draw "Saving\u2026" first; the durable write then runs on the next scheduler tick.
            Root.schedule.Execute(Promote).StartingIn(SavingDelayMs);
        }

        private void Promote()
        {
            if (state != PrestigePanelState.Saving)
            {
                return;
            }

            var promoted = commands.PromotePrestige();
            state = PrestigePanelState.Unavailable;
            if (promoted.IsSuccess)
            {
                var award = preview?.IpAward ?? 0;
                failureCode = null;
                saleBannerText = "Factory Sale complete: +" + QuantityFormatter.Format(award) + " IP.";
                Invalidate();
                Refresh();
                shell?.ShowNotice("Factory Sale complete: +" + QuantityFormatter.Format(award) + " Influence Points.", ShellNoticeKind.Info);
                shell?.FocusWhenReady(CancelButton);
                return;
            }

            failureCode = promoted.ReasonCode;
            Invalidate();
            Refresh();
            state = session.PendingTransition != null ? PrestigePanelState.Failed : state;
            RenderState();
            shell?.FocusWhenReady(RetryButton);
        }

        // ---------------------------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------------------------

        private void ShowFeedback(string text)
        {
            SetText(feedback, text);
            SetDisplayed(feedback, true);
        }

        private void ClearFeedback()
        {
            SetText(feedback, string.Empty);
            SetDisplayed(feedback, false);
        }

        private T Required<T>(string name) where T : VisualElement
        {
            var element = Root.Q<T>(name);
            return element ?? throw new InvalidOperationException("PrestigePanel.uxml has no '" + name + "'.");
        }

        private static void SetDisplayed(VisualElement element, bool displayed) =>
            element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;

        private static void SetText(Label label, string text)
        {
            if (label.text == text)
            {
                return;
            }

            label.text = text;
            label.style.minHeight = StyleKeyword.Null;
        }

        private static void FitWrapped(GeometryChangedEvent evt)
        {
            var label = (Label)evt.target;
            var width = label.contentRect.width;
            if (width <= 0f || string.IsNullOrEmpty(label.text))
            {
                return;
            }

            var needed = label.MeasureTextSize(label.text, width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined).y;
            if (PanelIcons.HasIcon(label))
            {
                needed = Mathf.Max(needed, PanelIcons.IconSize);
            }

            var current = label.style.minHeight.keyword == StyleKeyword.Undefined ? label.style.minHeight.value.value : 0f;
            if (Mathf.Abs(needed - current) > 0.5f)
            {
                label.style.minHeight = needed;
            }
        }
    }
}
