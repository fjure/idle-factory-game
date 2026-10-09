using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Unity.Bootstrap;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>What an <see cref="OfflineSummaryPanel"/> currently shows.</summary>
    public enum OfflineSurfaceKind
    {
        /// <summary>Nothing: no return was presented, or the summary was dismissed.</summary>
        None = 0,

        /// <summary>The summary of an offline return (<see cref="RunOfflineSummary"/>).</summary>
        Summary = 1,

        /// <summary>A return that could not be completed (an unavailable clock, a store failure); the saved factory is unchanged and Retry is offered.</summary>
        Blocked = 2,
    }

    /// <summary>
    /// The offline-return surface (T118, FR-025, FR-028, FR-032, US5 scenarios 1 and 3; contracts/ui-surfaces.md, Offline summary).
    /// It is not a tab panel: when the player returns it covers the shell as a modal card, shows what the absence did, and closes
    /// with Continue. It lists the measured, credited and effective durations, the Cash and stock deltas, sales, effects that
    /// expired, the contract outcome and the limiting conditions, all from the <see cref="RunOfflineSummary"/> the persistence layer
    /// derived once for the return. A short absence says "No production while away". A backward clock is a return that credited
    /// nothing and says why. A return the persistence layer could not complete (an unavailable clock leaves the lifecycle
    /// <c>ReturnBlocked</c>) shows the explanation and Retry, which calls the injected retry command.
    ///
    /// A runtime rotation is not a departure: <see cref="Present"/> creates no summary for <see cref="LifecycleReturnKind.ConfigurationChange"/>,
    /// and a summary that is already open stays as it is while the layout reflows (US5 scenario 4, FR-036).
    ///
    /// The panel reads the summary and the catalog's labels and mutates nothing; Retry goes through the application command it was
    /// given (normally <c>RunPersistence.RetryReturn</c>) and the result is reported through <see cref="RetryCompleted"/> so the
    /// composition can resume the runtime exactly as it does for a lifecycle return.
    /// </summary>
    public sealed class OfflineSummaryPanel : StatusSurface
    {
        public const string SurfaceName = "offline-summary";
        public const string HeadlineText = "Welcome back";
        public const string NoProductionText = "No production while away.";
        public const string ContinueLabel = "Continue";

        /// <summary>
        /// A genuine return whose measured absence is shorter than this opens no modal summary (T246, owner decision 2026-10-08, FR-033/UX-2).
        /// 60 s of measured real time; the single definition of the short-absence threshold.
        /// </summary>
        public const long ShortAbsenceThresholdMs = 60_000L;
        public const string RetryLabel = "Retry";
        public const string BackwardClockText =
            "The device clock shows a time earlier than when you left, so no offline production was credited. Your factory is as you left it.";
        public const string UnavailableClockText =
            "The device clock is unavailable, so no offline production was credited. Your factory is as you left it.";
        public const string ForfeitedText =
            "The offline credit was too large to apply, so none was credited. Your factory is as you left it.";
        public const string BlockedTitle = "The return could not be completed";
        public const string BlockedText =
            "Often the device clock is unavailable or wrong. Check the date and time, then retry. Your saved factory is unchanged and nothing was lost.";
        public const string RetryFailedText = "The return still could not be completed. Check the device date and time and try again.";

        private readonly RunCatalog catalog;
        private readonly Func<RunCommandResult<RunPersistenceOutcome>> retryReturn;
        private readonly List<Label> lines = new List<Label>();
        private readonly List<Label> sections = new List<Label>();
        private readonly Label feedback;
        private OfflineSurfaceKind kind;
        private RunOfflineSummary summary;
        private bool retrying;

        /// <summary>
        /// Builds the surface. <paramref name="catalog"/> supplies the labels of resources, machines, items and contracts (without it the
        /// IDs are shown); <paramref name="retryReturn"/> is the retry command, normally <c>persistence.RetryReturn</c>; <paramref name="styles"/> is
        /// <c>OfflineSummaryPanel.uss</c>.
        /// </summary>
        public OfflineSummaryPanel(RunCatalog catalog, Func<RunCommandResult<RunPersistenceOutcome>> retryReturn, StyleSheet styles)
            : base(SurfaceName, styles)
        {
            this.catalog = catalog;
            this.retryReturn = retryReturn;
            ContinueButton = RunShellDocument.CreateButton("offline-continue", ContinueLabel, true);
            RetryButton = RunShellDocument.CreateButton("offline-retry", RetryLabel, true);
            ContinueButton.clicked += OnContinue;
            RetryButton.clicked += OnRetry;
            Actions.Add(RetryButton);
            Actions.Add(ContinueButton);
            feedback = MakeLabel("offline-feedback", "status-line");
            feedback.AddToClassList("status-line--warning");
            SetDisplayed(feedback, false);
            SetDisplayed(RetryButton, false);
            SetDisplayed(ContinueButton, false);

            // Escape and Android Back close a summary like Continue. A blocked return stays until it is retried.
            Root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Root.RegisterCallback<NavigationCancelEvent>(OnNavigationCancel, TrickleDown.TrickleDown);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (IsOpen && kind == OfflineSurfaceKind.Summary && evt.keyCode == KeyCode.Escape)
            {
                evt.StopPropagation();
                Dismiss();
            }
        }

        private void OnNavigationCancel(NavigationCancelEvent evt)
        {
            if (IsOpen && kind == OfflineSurfaceKind.Summary)
            {
                evt.StopPropagation();
                Dismiss();
            }
        }

        public Button ContinueButton { get; }
        public Button RetryButton { get; }

        /// <summary>What the surface shows now.</summary>
        public OfflineSurfaceKind Kind => kind;

        /// <summary>The summary that is shown, or null.</summary>
        public RunOfflineSummary Summary => summary;

        /// <summary>The one-line result of the last Retry that did not complete the return.</summary>
        public Label FeedbackLabel => feedback;

        /// <summary>The text of every displayed line, in order (section headings and lines).</summary>
        public IReadOnlyList<string> LineTexts
        {
            get
            {
                var texts = new List<string>();
                foreach (var child in Content.Children())
                {
                    if (child is Label label && label.resolvedStyle.display != DisplayStyle.None && !string.IsNullOrEmpty(label.text))
                    {
                        texts.Add(label.text);
                    }
                }

                return texts;
            }
        }

        /// <summary>The line label with this name (names are listed in the tests), or null.</summary>
        public Label Line(string name) => Content.Q<Label>(name);

        /// <summary>Raised when the player closes the summary with Continue.</summary>
        public event Action Dismissed;

        /// <summary>
        /// Raised after Retry, with the command's result. On success the composition resumes the runtime the way it does for a lifecycle return
        /// (the application commands cannot reach the host); the surface then shows the summary of the completed return.
        /// </summary>
        public event Action<RunCommandResult<RunPersistenceOutcome>> RetryCompleted;

        // ------------------------------------------------------------------------------------------------------
        // Presenting a return
        // ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Presents the result of a lifecycle return and returns whether a surface was opened. A configuration change (a rotation) is not
        /// a departure and opens nothing; a departure result (<see cref="LifecycleReturnKind.None"/>) opens nothing; a rejected return
        /// opens the blocked explanation with Retry; a successful return opens the summary when the persistence layer derived one (a return
        /// with no departure, a save, or a reload has none).
        /// </summary>
        public bool Present(RunCommandResult<RunPersistenceOutcome> result, LifecycleReturnKind returnKind)
        {
            if (returnKind == LifecycleReturnKind.ConfigurationChange || returnKind == LifecycleReturnKind.None)
            {
                return false;
            }

            if (!result.IsSuccess)
            {
                ShowBlocked();
                return true;
            }

            var offline = result.Value?.OfflineSummary;
            if (offline == null)
            {
                return false;
            }

            LastGenuineSummary = offline;
            if (!ShouldOpenSummary(offline, result.Value.UsedFallback))
            {
                return false;
            }

            ShowSummary(offline);
            return true;
        }

        /// <summary>
        /// The summary of the last genuine return presented, whether or not it opened the modal (a short absence keeps it available here,
        /// the reconcile having run exactly once). Unlike <see cref="Summary"/> it is not cleared by Continue.
        /// </summary>
        public RunOfflineSummary LastGenuineSummary { get; private set; }

        /// <summary>
        /// Whether the summary of a genuine return opens the modal. False only for a clean, applied return that measured less than
        /// <see cref="ShortAbsenceThresholdMs"/> and had nothing to report: no FR-037 fallback, no expired effect or ended Overdrive phase,
        /// no expired contract. A backward or invalid clock, a forfeited credit and every return at or above the threshold open it.
        /// </summary>
        public static bool ShouldOpenSummary(RunOfflineSummary offline, bool usedFallback)
        {
            if (offline == null) throw new ArgumentNullException(nameof(offline));
            if (usedFallback || offline.Outcome != RunOfflineOutcome.Applied)
            {
                return true;
            }

            if (offline.MeasuredMs < 0 || offline.MeasuredMs >= ShortAbsenceThresholdMs)
            {
                return true;
            }

            return offline.ExpiredEffects.Count > 0 || offline.OverdriveActiveEnded || offline.OverdriveCooldownEnded ||
                   offline.Contract.Kind == RunOfflineContractKind.Expired;
        }

        /// <summary>Shows the summary of an offline return.</summary>
        public void ShowSummary(RunOfflineSummary offlineSummary)
        {
            if (offlineSummary == null) throw new ArgumentNullException(nameof(offlineSummary));
            summary = offlineSummary;
            kind = OfflineSurfaceKind.Summary;
            retrying = false;
            Rebuild();
            SetDisplayed(RetryButton, false);
            SetDisplayed(ContinueButton, true);
            OpenSurface(ContinueButton);
            Scroll.scrollOffset = Vector2.zero;
        }

        /// <summary>Shows the blocked-return explanation with Retry.</summary>
        public void ShowBlocked()
        {
            summary = null;
            kind = OfflineSurfaceKind.Blocked;
            retrying = false;
            Rebuild();
            SetDisplayed(RetryButton, retryReturn != null);
            SetDisplayed(ContinueButton, false);
            OpenSurface(retryReturn != null ? RetryButton : ContinueButton);
            Scroll.scrollOffset = Vector2.zero;
        }

        /// <summary>Closes the surface without any effect on the run. Raises <see cref="Dismissed"/> when something was shown.</summary>
        public void Dismiss()
        {
            if (!IsOpen)
            {
                return;
            }

            CloseSurface();
            summary = null;
            kind = OfflineSurfaceKind.None;
            Dismissed?.Invoke();
        }

        private void OnContinue() => Dismiss();

        private void OnRetry()
        {
            if (retrying || retryReturn == null)
            {
                return;
            }

            retrying = true;
            RunCommandResult<RunPersistenceOutcome> result;
            try
            {
                result = retryReturn();
            }
            finally
            {
                retrying = false;
            }

            if (result.IsSuccess)
            {
                RetryCompleted?.Invoke(result);
                if (!Present(result, LifecycleReturnKind.Return))
                {
                    // The return completed without a summary: nothing is left to show.
                    CloseSurface();
                    summary = null;
                    kind = OfflineSurfaceKind.None;
                }

                return;
            }

            RetryCompleted?.Invoke(result);
            SetText(feedback, RetryFailedText);
            SetDisplayed(feedback, true);
            FocusWhenReady(RetryButton);
        }

        // ------------------------------------------------------------------------------------------------------
        // Content
        // ------------------------------------------------------------------------------------------------------

        private void Rebuild()
        {
            Content.Clear();
            lines.Clear();
            sections.Clear();
            SetText(feedback, string.Empty);
            SetDisplayed(feedback, false);
            ClearNotice();

            if (kind == OfflineSurfaceKind.Blocked)
            {
                SetText(TitleLabel, BlockedTitle);
                AddLine("offline-blocked", BlockedText, "status-line");
                Content.Add(feedback);
                return;
            }

            SetText(TitleLabel, HeadlineText);
            var offline = summary;
            AddLine("offline-headline", Headline(offline), "status-line--lead");
            AddDurations(offline);
            if (offline.Outcome == RunOfflineOutcome.Applied)
            {
                if (!IsIdle(offline))
                {
                    AddEconomy(offline);
                }

                AddEvents(offline);
                AddLimits(offline);
            }
            else
            {
                AddLimits(offline);
            }

            Content.Add(feedback);
        }

        private static string Headline(RunOfflineSummary offline)
        {
            switch (offline.Outcome)
            {
                case RunOfflineOutcome.Applied:
                    return IsIdle(offline) ? NoProductionText : "Here is what your factory did while you were away.";
                case RunOfflineOutcome.BackwardClock:
                    return BackwardClockText;
                case RunOfflineOutcome.MissingClock:
                case RunOfflineOutcome.ClockUnrepresentable:
                    return UnavailableClockText;
                default:
                    return offline.CreditForfeited ? ForfeitedText : "The offline credit could not be applied. Your factory is as you left it.";
            }
        }

        /// <summary>A short absence or an idle factory: nothing was made, sold or changed.</summary>
        private static bool IsIdle(RunOfflineSummary offline) =>
            offline.Completions == 0 && offline.StockDeltas.Count == 0 && offline.CashDelta == 0 && offline.SalesIncome == 0 &&
            offline.ExpiredEffects.Count == 0 && !offline.OverdriveActiveEnded && !offline.OverdriveCooldownEnded &&
            offline.Contract.Kind != RunOfflineContractKind.Expired && offline.NewUnlocks.Count == 0;

        private void AddDurations(RunOfflineSummary offline)
        {
            AddSection("offline-time-title", "Time");
            AddLine("offline-measured", "Away for " + Duration(offline.MeasuredMs) + ".", "status-line");
            if (offline.Outcome != RunOfflineOutcome.Applied)
            {
                return;
            }

            var credited = offline.CreditedTicks / RunSession.TicksPerMs;
            AddLine("offline-credited", "Credited: " + Duration(credited) + ".", "status-line");
            var effective = offline.EffectiveTicks / RunSession.TicksPerMs;
            var percent = offline.EfficiencyDenominator <= 0 ? 0 : offline.EfficiencyNumerator * 100 / offline.EfficiencyDenominator;
            AddLine("offline-effective", "Production ran for " + Duration(effective) + " at " + percent.ToString(CultureInfo.InvariantCulture) + " % efficiency.", "status-line");
        }

        private void AddEconomy(RunOfflineSummary offline)
        {
            AddSection("offline-economy-title", "Cash and stock");
            var sign = offline.CashDelta >= 0 ? "+" : "-";
            AddLine(
                "offline-cash",
                "Cash " + sign + QuantityFormatter.FormatExact(Math.Abs(offline.CashDelta)) + " (from " + QuantityFormatter.FormatExact(offline.CashBefore) +
                " to " + QuantityFormatter.FormatExact(offline.CashAfter) + ").",
                "status-line");
            AddLine("offline-sales", "Sales income: " + QuantityFormatter.FormatExact(offline.SalesIncome) + " Cash.", "status-line");
            if (offline.SoldUnits.Count > 0)
            {
                AddLine("offline-sold", "Sold: " + Units(offline.SoldUnits) + ".", "status-line");
            }

            if (offline.ProducedUnits.Count > 0)
            {
                AddLine("offline-produced", "Produced: " + Units(offline.ProducedUnits) + ".", "status-line");
            }

            if (offline.ConsumedUnits.Count > 0)
            {
                AddLine("offline-consumed", "Used: " + Units(offline.ConsumedUnits) + ".", "status-line");
            }

            if (offline.Completions > 0)
            {
                AddLine("offline-completions", "Completed cycles: " + QuantityFormatter.FormatExact(offline.Completions) + ".", "status-line");
            }

            foreach (var delta in offline.StockDeltas)
            {
                var change = delta.Delta >= 0 ? "+" : "-";
                AddLine(
                    "offline-stock-" + delta.ResourceId,
                    ResourceLabel(delta.ResourceId) + ": " + QuantityFormatter.FormatExact(delta.Before) + " to " + QuantityFormatter.FormatExact(delta.After) +
                    " (" + change + QuantityFormatter.FormatExact(Math.Abs(delta.Delta)) + ").",
                    "status-line");
            }
        }

        private void AddEvents(RunOfflineSummary offline)
        {
            var any = offline.ExpiredEffects.Count > 0 || offline.OverdriveActiveEnded || offline.OverdriveCooldownEnded ||
                      offline.Contract.Kind != RunOfflineContractKind.None || offline.NewUnlocks.Count > 0;
            if (!any)
            {
                return;
            }

            AddSection("offline-events-title", "While you were away");
            for (var i = 0; i < offline.ExpiredEffects.Count; i++)
            {
                var effect = offline.ExpiredEffects[i];
                AddLine(
                    "offline-expired-" + i.ToString(CultureInfo.InvariantCulture),
                    "Item effect ended: " + ItemLabel(effect.SourceId) + " on " + TargetLabel(effect.TargetKey) + ".",
                    "status-line");
            }

            if (offline.OverdriveActiveEnded)
            {
                AddLine("offline-overdrive-ended", "Overdrive ended.", "status-line");
            }

            if (offline.OverdriveCooldownEnded)
            {
                AddLine("offline-overdrive-ready", "Overdrive is ready again.", "status-line");
            }

            if (offline.Contract.Kind == RunOfflineContractKind.Expired)
            {
                AddLine("offline-contract", "Contract expired: " + ContractText(offline.Contract) + ". No reward, no penalty.", "status-line");
            }
            else if (offline.Contract.Kind == RunOfflineContractKind.StillActive)
            {
                AddLine("offline-contract", "Contract still active: " + ContractText(offline.Contract) + ".", "status-line");
            }

            if (offline.NewUnlocks.Count > 0)
            {
                var names = new List<string>();
                foreach (var id in offline.NewUnlocks)
                {
                    names.Add(AnyLabel(id));
                }

                AddLine("offline-unlocks", "Newly unlocked: " + string.Join(", ", names) + ".", "status-line");
            }
        }

        private void AddLimits(RunOfflineSummary offline)
        {
            var texts = new List<KeyValuePair<string, string>>();
            if (offline.Outcome == RunOfflineOutcome.Applied && offline.CapReached)
            {
                texts.Add(new KeyValuePair<string, string>(
                    "offline-limit-cap",
                    "Offline credit is capped at " + Duration(offline.CapTicks / RunSession.TicksPerMs) + "; time beyond the cap earned nothing."));
            }

            if (offline.Outcome == RunOfflineOutcome.Applied && offline.PartialBatchCompletions > 0)
            {
                texts.Add(new KeyValuePair<string, string>(
                    "offline-limit-partial",
                    QuantityFormatter.FormatExact(offline.PartialBatchCompletions) + " cycle(s) made fewer units than a full batch because inputs or storage ran short."));
            }

            if (offline.Outcome == RunOfflineOutcome.Applied)
            {
                for (var i = 0; i < offline.BlockedMachinesAtReturn.Count; i++)
                {
                    var blocked = offline.BlockedMachinesAtReturn[i];
                    var reason = blocked.Blocker == MachineBlocker.StorageFull ? "the store is full" : "an input is missing";
                    texts.Add(new KeyValuePair<string, string>(
                        "offline-limit-blocked-" + i.ToString(CultureInfo.InvariantCulture),
                        "Slot " + (blocked.SlotIndex + 1).ToString(CultureInfo.InvariantCulture) + " " + MachineLabel(blocked.MachineId) + " is waiting: " + reason + "."));
                }
            }

            if (texts.Count == 0)
            {
                return;
            }

            AddSection("offline-limits-title", "Limits");
            foreach (var pair in texts)
            {
                AddLine(pair.Key, pair.Value, "status-line");
            }
        }

        private void AddSection(string name, string text)
        {
            var label = MakeLabel(name, "status-section");
            SetText(label, text);
            Content.Add(label);
            sections.Add(label);
        }

        private void AddLine(string name, string text, string className)
        {
            var label = MakeLabel(name, className);
            if (className == "status-line--lead")
            {
                label.AddToClassList("status-line");
            }

            SetText(label, text);
            Content.Add(label);
            lines.Add(label);
        }

        private static void SetText(Label label, string text)
        {
            label.text = text ?? string.Empty;
            label.style.minHeight = StyleKeyword.Null;
        }

        // ------------------------------------------------------------------------------------------------------
        // Copy
        // ------------------------------------------------------------------------------------------------------

        /// <summary>A duration in the largest two units: "8 h", "2 h 15 min", "5 min 10 s", "30 s".</summary>
        public static string Duration(long milliseconds)
        {
            if (milliseconds <= 0)
            {
                return "0 s";
            }

            if (milliseconds < 1000)
            {
                return "less than 1 s";
            }

            var totalSeconds = milliseconds / 1000;
            var hours = totalSeconds / 3600;
            var minutes = totalSeconds % 3600 / 60;
            var seconds = totalSeconds % 60;
            var text = new StringBuilder();
            if (hours > 0)
            {
                text.Append(QuantityFormatter.FormatExact(hours)).Append(" h");
                if (minutes > 0)
                {
                    text.Append(' ').Append(minutes.ToString(CultureInfo.InvariantCulture)).Append(" min");
                }
            }
            else if (minutes > 0)
            {
                text.Append(minutes.ToString(CultureInfo.InvariantCulture)).Append(" min");
                if (seconds > 0)
                {
                    text.Append(' ').Append(seconds.ToString(CultureInfo.InvariantCulture)).Append(" s");
                }
            }
            else
            {
                text.Append(seconds.ToString(CultureInfo.InvariantCulture)).Append(" s");
            }

            return text.ToString();
        }

        private string Units(IReadOnlyList<RunResourceUnits> units)
        {
            var parts = new List<string>(units.Count);
            foreach (var item in units)
            {
                parts.Add(QuantityFormatter.FormatExact(item.Units) + " " + ResourceLabel(item.ResourceId));
            }

            return string.Join(", ", parts);
        }

        private string ContractText(RunOfflineContractOutcome contract)
        {
            var resource = string.Empty;
            if (catalog != null && contract.OfferId != null && catalog.ContractOffers.TryGetValue(contract.OfferId, out var offer))
            {
                resource = ResourceLabel(offer.ResourceId);
            }

            return resource.Length == 0
                ? "deliver " + QuantityFormatter.FormatExact(contract.RequiredUnits)
                : "deliver " + QuantityFormatter.FormatExact(contract.RequiredUnits) + " " + resource;
        }

        private string ResourceLabel(string id) =>
            id != null && catalog != null && catalog.Resources.TryGetValue(id, out var def) ? def.Label : id ?? string.Empty;

        private string MachineLabel(string id) =>
            id != null && catalog != null && catalog.Machines.TryGetValue(id, out var def) ? def.Label : id ?? string.Empty;

        private string ItemLabel(string id) =>
            id != null && catalog != null && catalog.Items.TryGetValue(id, out var def) ? def.Label : id ?? string.Empty;

        private string TargetLabel(string targetKey)
        {
            if (string.IsNullOrEmpty(targetKey) || targetKey == "factory")
            {
                return "the factory";
            }

            return MachineLabel(targetKey);
        }

        private string AnyLabel(string id)
        {
            if (id == null || catalog == null)
            {
                return id ?? string.Empty;
            }

            if (catalog.Resources.TryGetValue(id, out var resource)) return resource.Label;
            if (catalog.Machines.TryGetValue(id, out var machine)) return machine.Label;
            if (catalog.Items.TryGetValue(id, out var item)) return item.Label;
            if (catalog.Research.TryGetValue(id, out var research)) return research.Label;
            if (catalog.ContractOffers.TryGetValue(id, out var offer)) return ResourceLabel(offer.ResourceId) + " contract";
            return id;
        }
    }
}
