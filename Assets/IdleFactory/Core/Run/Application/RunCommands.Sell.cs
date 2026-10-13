using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;

namespace IdleFactory.Run.Application
{
    /// <summary>The outcome of <see cref="RunCommands.Sell"/> and <see cref="RunCommands.SellAll"/> (T317).</summary>
    public readonly struct SellResult
    {
        public string ResourceId { get; }

        /// <summary>The units removed from the stock.</summary>
        public long Units { get; }

        /// <summary>The Cash the sale paid, <c>SalePricing.Value(Units, basePrice, salePricePpm)</c>.</summary>
        public long CashEarned { get; }

        /// <summary>Cash after the sale and its settle.</summary>
        public long CashAfter { get; }

        public SellResult(string resourceId, long units, long cashEarned, long cashAfter)
        {
            ResourceId = resourceId;
            Units = units;
            CashEarned = cashEarned;
            CashAfter = cashAfter;
        }
    }

    /// <summary>
    /// The manual sale commands of User Story 1 (contracts/run-application-f005.md, F5-R04, T317): Sell and SellAll.
    /// They follow the F003 order: lifecycle, validation, the settle pre-check, one checked transaction, settle.
    ///
    /// Validation order (each rejection leaves every piece of state, both revisions and the event log untouched):
    /// lifecycle.blocked, resource.locked, sell.quantity-invalid, stock.insufficient, value.limit.
    /// The price is <see cref="SalePricing"/>, the same function the simulator's auto-sell, the Factory Value and the
    /// contract reward use, so selling the whole stock changes Factory Value by 0 (FR-002) and no sale raises it.
    ///
    /// State ownership: the commands own nothing; stock and Cash change in <see cref="RunState"/> through
    /// <c>TrySell</c>. Nothing is persisted by the command itself (the save is the run state, unchanged in shape).
    /// The sale event (<see cref="RunSaleEvent"/>) is built from values already computed, after the transaction, and
    /// raised through the guarded sink only; it never allocates inside the simulator and cannot alter the result.
    /// </summary>
    public sealed partial class RunCommands
    {
        private readonly struct Unit { }

        /// <summary>
        /// Sells <paramref name="quantity"/> units of an unlocked resource at the effective sale price. Rejected with
        /// resource.locked (unknown or locked), sell.quantity-invalid (below 1), stock.insufficient (more than the
        /// stock at the command instant) or value.limit (the price, the income or Cash would pass 10^18, or the
        /// settle that follows could). The settle pre-check subtracts the sold stock.
        /// </summary>
        public RunCommandResult<SellResult> Sell(string resourceId, long quantity)
        {
            if (_session.IsLifecycleBlocked) return Reject<SellResult>(RunReasons.LifecycleBlocked);
            if (!TryUnlockedResource(resourceId, out ResourceDef resource)) return Reject<SellResult>(RunReasons.ResourceLocked);
            if (quantity < 1) return Reject<SellResult>(RunReasons.SellQuantityInvalid);
            if (quantity > State.Inventory.Units(resource.Id)) return Reject<SellResult>(RunReasons.StockInsufficient);
            return SellValidated(resource, quantity);
        }

        /// <summary>
        /// Sells the whole stock of an unlocked resource: <see cref="Sell"/> with the quantity taken at the command
        /// instant. Rejected with resource.locked, stock.insufficient (stock 0) or value.limit.
        /// </summary>
        public RunCommandResult<SellResult> SellAll(string resourceId)
        {
            if (_session.IsLifecycleBlocked) return Reject<SellResult>(RunReasons.LifecycleBlocked);
            if (!TryUnlockedResource(resourceId, out ResourceDef resource)) return Reject<SellResult>(RunReasons.ResourceLocked);
            long stock = State.Inventory.Units(resource.Id);
            if (stock < 1) return Reject<SellResult>(RunReasons.StockInsufficient);
            return SellValidated(resource, stock);
        }

        private bool TryUnlockedResource(string resourceId, out ResourceDef resource)
        {
            resource = null;
            return resourceId != null && _catalog.Resources.TryGetValue(resourceId, out resource) && State.IsUnlocked(resourceId);
        }

        private RunCommandResult<SellResult> SellValidated(ResourceDef resource, long quantity)
        {
            RunState state = State;
            if (!_modifiers.TrySalePricePpm(state, Permanent, resource.Id, out long ppm) ||
                !SalePricing.TryValue(quantity, resource.BasePrice, ppm, out long income) ||
                !CheckedMath.TryAdd(state.Cash, income, out long cashAfter) ||
                !ExactBound.Contains(cashAfter) ||
                !SettleFits(cashAfter, null, null, null, null, resource.Id, quantity))
                return Reject<SellResult>(RunReasons.ValueLimit);

            _session.BeginCommand();
            RunStateResult sold = state.TrySell(resource.Id, quantity, income);
            if (sold != RunStateResult.Ok) return Reject<SellResult>(RunReasons.From(sold));
            // Build the result after the settle: auto-sell of leftover stock may have added Cash, and CashAfter is "after the sale and its settle".
            RunCommandResult<Unit> settled = _session.EndCommand(default(Unit), false);
            if (!settled.IsSuccess) return Reject<SellResult>(settled.ReasonCode);
            _session.Events.OnSale(new RunSaleEvent(resource.Id, quantity, income, true));
            return RunCommandResult.Success(new SellResult(resource.Id, quantity, income, State.Cash));
        }
    }
}
