using System;
using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI.Panels
{
    /// <summary>
    /// The manual sale controls of every inventory row (User Story 1, T319; contracts/ui-surfaces-f005.md): the step selector
    /// <c>inventory-sell-step-&lt;resourceId&gt;</c> and the Sell button <c>inventory-sell-&lt;resourceId&gt;</c>. The view holds no
    /// economy rule: the availability of a step is the application preview (<see cref="ResourceEconomy.SellPreview"/>), the sale is
    /// <see cref="RunCommands.Sell"/> or <see cref="RunCommands.SellAll"/>, and every rejection shows its
    /// <see cref="RunRejectionCopy"/> text beside the Sell button. The preview does not run the settle pre-check, so a step shown
    /// available can still be rejected with <c>value.limit</c> near the Cash limit; that rejection is shown like any other.
    /// </summary>
    public sealed partial class InventoryRatesPanel
    {
        public const string NothingToSellText = "Nothing to sell";
        public const string LockedText = "Locked";
        public const string AllStepText = "All";

        private readonly Dictionary<string, long> selectedSteps = new Dictionary<string, long>(StringComparer.Ordinal);
        private bool confirmOpen;

        /// <summary>The step selector of a row (cycles through the catalog's steps and All on activation), or null.</summary>
        public Button SellStepButton(string resourceId) => rows.TryGetValue(resourceId, out var row) ? row.SellStep : null;

        public Button SellButton(string resourceId) => rows.TryGetValue(resourceId, out var row) ? row.Sell : null;

        /// <summary>
        /// The state line beside Sell: "Nothing to sell", "Need N more" or the preview rejection copy; empty when the step is available.
        /// For a locked resource it is the lock line "Locked: unlocks after ..." of its locked row.
        /// </summary>
        public Label SellHint(string resourceId) =>
            rows.TryGetValue(resourceId, out var row) ? row.SellHint : lockedRows.TryGetValue(resourceId, out var locked) ? locked.Hint : null;

        /// <summary>The rejection copy of the last Sell tap that was rejected (with its warning icon).</summary>
        public Label SellReason(string resourceId) => rows.TryGetValue(resourceId, out var row) ? row.SellReason : null;

        /// <summary>The "Sold N X: +$Y" result of the last sale; its exact value is "N × $p = $Y".</summary>
        public Button SellResult(string resourceId) => rows.TryGetValue(resourceId, out var row) ? row.SellResult : null;

        /// <summary>The step currently chosen for a resource (<see cref="ResourceEconomy.SellAllStep"/> for All).</summary>
        public long SelectedStep(string resourceId) => selectedSteps.TryGetValue(resourceId, out var step) ? step : FirstStep();

        /// <summary>
        /// Cancels an open "Contract will be N short" confirmation because of a rotation, without selling (UX-9). Register it with
        /// the bootstrap's transient actions (through <see cref="MarketPanelHost.CancelForRotation"/>). Returns whether one was open.
        /// </summary>
        public bool CancelForRotation()
        {
            if (!confirmOpen)
            {
                return false;
            }

            return shell.CancelConfirmationForRotation();
        }

        /// <summary>What a sell step shows: availability, the state line and the reason used in the accessible name.</summary>
        public readonly struct SellView
        {
            public bool Available { get; }

            /// <summary>The visible state line; empty when available.</summary>
            public string Hint { get; }

            /// <summary>The short reason in the accessible name ("not enough stock"); empty when available.</summary>
            public string NameReason { get; }

            public bool IsLocked { get; }

            public SellView(bool available, string hint, string nameReason, bool isLocked = false)
            {
                Available = available;
                Hint = hint ?? string.Empty;
                NameReason = nameReason ?? string.Empty;
                IsLocked = isLocked;
            }
        }

        /// <summary>Maps a resource row and one preview to what the controls show (T319 states). Pure, so tests can drive every state.</summary>
        public static SellView Evaluate(ResourceEconomy economy, SellPreviewEntry preview)
        {
            if (!economy.Unlocked)
            {
                return new SellView(false, LockedText, "locked", true);
            }

            var reason = preview.UnavailableReason;
            if (reason == null)
            {
                return new SellView(true, string.Empty, string.Empty);
            }

            switch (reason)
            {
                case RunReasons.StockInsufficient:
                    return economy.Units < 1 || preview.Step == ResourceEconomy.SellAllStep
                        ? new SellView(false, NothingToSellText, "nothing to sell")
                        : new SellView(false, "Need " + QuantityFormatter.Format(Math.Max(1L, preview.Step - economy.Units)) + " more", "not enough stock");
                case RunReasons.ResourceLocked:
                    return new SellView(false, LockedText, "locked", true);
                case RunReasons.LifecycleBlocked:
                    return new SellView(false, RunRejectionCopy.For(reason), "factory not available");
                case RunReasons.ValueLimit:
                    return new SellView(false, RunRejectionCopy.For(reason), "number too large");
                default:
                    return new SellView(false, RunRejectionCopy.For(reason), "not available");
            }
        }

        /// <summary>The accessible name of the Sell button ("Sell 10 Iron Plate, available" / "..., unavailable: not enough stock").</summary>
        public static string SellName(string label, SellPreviewEntry preview, SellView view)
        {
            var amount = preview.Step == ResourceEconomy.SellAllStep ? "all" : QuantityFormatter.Format(preview.Step);
            var subject = preview.Step == ResourceEconomy.SellAllStep && preview.Units > 0
                ? "all " + QuantityFormatter.Format(preview.Units)
                : amount;
            return "Sell " + subject + " " + label + (view.Available ? ", available" : ", unavailable: " + view.NameReason);
        }

        /// <summary>The accessible name of the step selector ("Sell amount, 10").</summary>
        public static string StepName(long step) => "Sell amount, " + (step == ResourceEconomy.SellAllStep ? "all" : QuantityFormatter.Format(step));

        private long FirstStep()
        {
            var economy = RunSnapshots.Economy(session);
            return economy.SellSteps.Count > 0 ? economy.SellSteps[0] : ResourceEconomy.SellAllStep;
        }

        private void BuildSellControls(Row row, string label)
        {
            var id = row.ResourceId;
            row.SellBlock = new VisualElement { name = "inventory-sell-block-" + id };
            row.SellBlock.AddToClassList("inv-sell");

            var controls = new VisualElement { name = "inventory-sell-controls-" + id };
            controls.AddToClassList("inv-sell__controls");
            row.SellStep = RunShellDocument.CreateButton("inventory-sell-step-" + id, string.Empty);
            row.SellStep.AddToClassList("inv-sell__button");
            row.SellStep.clicked += () => OnStepCycle(row);
            controls.Add(row.SellStep);
            row.Sell = RunShellDocument.CreateButton("inventory-sell-" + id, string.Empty, true);
            row.Sell.AddToClassList("inv-sell__button");
            row.Sell.clicked += () => OnSell(row);
            controls.Add(row.Sell);
            row.SellBlock.Add(controls);

            row.SellHint = MarketPanelParts.NewLabel("inventory-sell-hint-" + id, "inv-caption");
            MarketPanelParts.SetVisible(row.SellHint, false);
            row.SellBlock.Add(row.SellHint);
            row.SellReason = MarketPanelParts.NewLabel("inventory-sell-reason-" + id, "inv-caption");
            row.SellReason.AddToClassList("inv-caption--limit");
            MarketPanelParts.SetVisible(row.SellReason, false);
            row.SellBlock.Add(row.SellReason);
            row.Root.Add(row.SellBlock);
        }

        /// <summary>The last sale's result sits after Auto-sell so the per-row focus order stays step, Sell, Auto-sell.</summary>
        private void AddSellFeedback(Row row)
        {
            row.SellResult = MarketPanelParts.NewValueButton("inventory-sell-result-" + row.ResourceId, "inv-value");
            row.SellResult.AddToClassList("inv-sell__result");
            MarketPanelParts.SetVisible(row.SellResult, false);
            row.Root.Add(row.SellResult);
            exactHandles.Add(shell.AttachExactValue(row.SellResult, () => row.SellResultExact));
        }

        private void ClearSellFeedback(Row row)
        {
            SetReason(row.SellReason, string.Empty);
            MarketPanelParts.SetVisible(row.SellReason, false);
            row.SellResult.text = string.Empty;
            row.SellResultExact = string.Empty;
            MarketPanelParts.SetVisible(row.SellResult, false);
        }

        private void UpdateSell(Row row, ResourceEconomy economy, EconomySnapshot snapshot)
        {
            var steps = BuildStepList(snapshot);
            MarketPanelParts.SetVisible(row.SellBlock, steps.Count > 0);
            if (steps.Count == 0)
            {
                return;
            }

            var step = selectedSteps.TryGetValue(row.ResourceId, out var remembered) ? remembered : steps[0];
            if (!steps.Contains(step))
            {
                // A remembered step the catalog no longer offers falls back to the first step; a valid step is never changed here.
                step = steps[0];
                selectedSteps[row.ResourceId] = step;
            }

            var preview = economy.SellPreview(step);
            var view = Evaluate(economy, preview);

            row.SellStep.text = "Amount: " + (step == ResourceEconomy.SellAllStep ? AllStepText : QuantityFormatter.Format(step));
            row.SellStep.tooltip = StepName(step);

            row.Sell.text = step == ResourceEconomy.SellAllStep ? "Sell all" : "Sell " + QuantityFormatter.Format(step);
            row.Sell.tooltip = SellName(economy.Label, preview, view);
            RunShellDocument.SetSoftDisabled(row.Sell, !view.Available);

            row.SellHint.text = view.Hint;
            PanelIcons.Mark(row.SellHint, icons, view.IsLocked ? PanelIcons.Lock : null);
            MarketPanelParts.SetVisible(row.SellHint, view.Hint.Length > 0);
        }

        private static List<long> BuildStepList(EconomySnapshot snapshot)
        {
            var steps = new List<long>(snapshot.SellSteps.Count + 1);
            for (var i = 0; i < snapshot.SellSteps.Count; i++)
            {
                steps.Add(snapshot.SellSteps[i]);
            }

            if (snapshot.HasSellAll)
            {
                steps.Add(ResourceEconomy.SellAllStep);
            }

            return steps;
        }

        private void OnStepCycle(Row row)
        {
            var steps = BuildStepList(RunSnapshots.Economy(session));
            if (steps.Count == 0)
            {
                return;
            }

            var index = steps.IndexOf(selectedSteps.TryGetValue(row.ResourceId, out var current) ? current : steps[0]);
            selectedSteps[row.ResourceId] = steps[(index + 1) % steps.Count];
            SetReason(row.SellReason, string.Empty);
            MarketPanelParts.SetVisible(row.SellReason, false);
            Render(true);
            shell.FocusWhenReady(rows.TryGetValue(row.ResourceId, out var after) ? after.SellStep : null);
        }

        private void OnSell(Row row)
        {
            var step = SelectedStep(row.ResourceId);
            if (step == ResourceEconomy.SellAllStep && TryContractShortfall(row.ResourceId, out var shortBy))
            {
                ConfirmSellAll(row, shortBy);
                return;
            }

            Execute(row, step);
        }

        /// <summary>
        /// "All" asks for confirmation only when it would leave an active delivery contract for this resource short (FR-003). Selling
        /// the whole stock leaves none, so a contract that is deliverable now falls short by its required units.
        /// </summary>
        private bool TryContractShortfall(string resourceId, out long shortBy)
        {
            shortBy = 0;
            var active = RunSnapshots.Contracts(session).Active;
            if (active == null || !active.CanDeliver || !string.Equals(active.ResourceId, resourceId, StringComparison.Ordinal) ||
                !session.Catalog.ContractOffers.TryGetValue(active.OfferId, out var offer) || offer.Objective != "deliver")
            {
                return false;
            }

            if (session.State.Inventory.Units(resourceId) < 1 || active.RequiredUnits < 1)
            {
                return false;
            }

            shortBy = active.RequiredUnits;
            return true;
        }

        private void ConfirmSellAll(Row row, long shortBy)
        {
            var label = row.Title.text;
            confirmOpen = true;
            var opened = shell.RequestConfirmation(new ConfirmationRequest
            {
                Title = "Sell all " + label + "?",
                Message = "Contract will be " + QuantityFormatter.Format(shortBy) + " short. Selling all " + label +
                    " leaves none for your active contract.",
                ConfirmLabel = "Sell all",
                CancelLabel = "Keep stock",
                Invoker = row.Sell,
                OnConfirm = () =>
                {
                    confirmOpen = false;
                    Execute(row, ResourceEconomy.SellAllStep);
                },
                OnCancel = reason => confirmOpen = false,
            });
            if (!opened)
            {
                confirmOpen = false;
            }
        }

        private void Execute(Row row, long step)
        {
            var id = row.ResourceId;
            var before = RunSnapshots.Economy(session);
            ResourceEconomy economy = null;
            for (var i = 0; i < before.Resources.Count; i++)
            {
                if (string.Equals(before.Resources[i].ResourceId, id, StringComparison.Ordinal))
                {
                    economy = before.Resources[i];
                }
            }

            var result = step == ResourceEconomy.SellAllStep ? commands.SellAll(id) : commands.Sell(id, step);
            if (result.IsSuccess && economy != null)
            {
                var sold = result.Value;
                var label = economy.Label;
                var text = "Sold " + QuantityFormatter.Format(sold.Units) + " " + label + ": +" + MarketPanelParts.Money(sold.CashEarned);
                row.SellResult.text = text;
                row.SellResult.tooltip = text;
                row.SellResultExact = ExactSaleLine(economy, sold.Units, sold.CashEarned);
                MarketPanelParts.SetVisible(row.SellResult, true);
                SetReason(row.SellReason, string.Empty);
                MarketPanelParts.SetVisible(row.SellReason, false);
                shell.ShowNotice(text, ShellNoticeKind.Info);
            }
            else
            {
                // A rejection (including a stale tap or value.limit) changes nothing and says why beside the control that was used.
                SetReason(row.SellReason, RunRejectionCopy.For(result.ReasonCode));
                MarketPanelParts.SetVisible(row.SellReason, true);
            }

            Render(true);
            // Rows are updated in place, so the Sell button normally keeps its focus; this covers a rebuilt row and the dialog return.
            shell.FocusWhenReady(rows.TryGetValue(id, out var current) ? current.Sell : null);
        }

        /// <summary>The exact value "N × $p = $Y": exact units, the exact unit price and the exact Cash (rounded down when the product has a fraction).</summary>
        public static string ExactSaleLine(ResourceEconomy economy, long units, long cash)
        {
            var total = MarketPanelParts.MoneyExact(cash);
            var count = QuantityFormatter.FormatExact(units);
            if (!economy.PriceValid || !CheckedMath.TryMultiply(economy.BasePrice, economy.SalePricePpm, out var micro))
            {
                return count + " × " + MarketPanelParts.MoneyExact(economy.UnitPrice) + " = " + total;
            }

            var line = count + " × $" + MarketPanelParts.FormatExactRate(micro, false) + " = " + total;
            try
            {
                if (((decimal)units * micro) % 1_000_000m != 0m)
                {
                    line += " (rounded down)";
                }
            }
            catch (OverflowException)
            {
                // A product beyond decimal range is far past the exact bound; the line stays without the rounding note.
            }

            return line;
        }
    }
}
