using System;
using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;

namespace IdleFactory.Run.Application
{
    /// <summary>Result of <see cref="RunCommands.AcceptContract"/>: what the accepted contract asks and when it ends.</summary>
    public readonly struct ContractAcceptResult
    {
        public string OfferId { get; }

        /// <summary>The completion count of the offer in this run; the index of the reward and quantity curves.</summary>
        public long CompletionIndex { get; }
        public long RequiredUnits { get; }
        public long AcceptedUtcMs { get; }

        /// <summary>The inclusive deadline (<c>timeline + durationMs</c>) of a timed contract, otherwise null.</summary>
        public long? DeadlineUtcMs { get; }

        public ContractAcceptResult(string offerId, long completionIndex, long requiredUnits, long acceptedUtcMs, long? deadlineUtcMs)
        {
            OfferId = offerId;
            CompletionIndex = completionIndex;
            RequiredUnits = requiredUnits;
            AcceptedUtcMs = acceptedUtcMs;
            DeadlineUtcMs = deadlineUtcMs;
        }
    }

    /// <summary>Result of <see cref="RunCommands.DeliverContract"/>: what was consumed and the one grant that was made.</summary>
    public readonly struct ContractDeliveryResult
    {
        public string OfferId { get; }
        public long UnitsConsumed { get; }
        public long CashReward { get; }
        public long RpReward { get; }

        /// <summary>The Items granted at this completion index, in ordinal Item-ID order; empty when none was due.</summary>
        public IReadOnlyList<IdCount> ItemsGranted { get; }

        /// <summary>The completion count after the delivery (the index the next acceptance scales by).</summary>
        public long NewCompletionCount { get; }

        public ContractDeliveryResult(string offerId, long unitsConsumed, long cashReward, long rpReward, IReadOnlyList<IdCount> itemsGranted,
            long newCompletionCount)
        {
            OfferId = offerId;
            UnitsConsumed = unitsConsumed;
            CashReward = cashReward;
            RpReward = rpReward;
            ItemsGranted = itemsGranted;
            NewCompletionCount = newCompletionCount;
        }
    }

    /// <summary>Result of <see cref="RunCommands.AbandonContract"/>.</summary>
    public readonly struct ContractAbandonResult
    {
        public string OfferId { get; }

        /// <summary>The completion count of the offer, which abandoning does not change (OD-4).</summary>
        public long CompletionCount { get; }

        public ContractAbandonResult(string offerId, long completionCount)
        {
            OfferId = offerId;
            CompletionCount = completionCount;
        }
    }

    /// <summary>
    /// The contract commands of User Story 2 (contracts/run-application.md): AcceptContract, DeliverContract and
    /// AbandonContract. They follow the rules of <see cref="RunCommands"/>: lifecycle first, validation, the
    /// <see cref="SettleFits"/> pre-check before any mutation, then <see cref="RunSession.BeginCommand"/>, one
    /// atomic domain transaction and <see cref="RunSession.EndCommand{T}"/>.
    ///
    /// Interpretations where the contract text is silent (each reported to the coordinator):
    /// (1) an offer that is unknown or not unlocked has no contract code, so it is <c>command.invalid</c> (the T137
    /// convention; the UI never offers it, see <c>ContractOfferSnapshot.Unlocked</c>); (2) abandoning a timed
    /// contract is <c>command.invalid</c> for the same reason (the UI hides Abandon for it); (3) once the simulator has
    /// expired a contract (deadline + 1 ms), the expired outcome is kept on <see cref="ContractBoard.LastExpired"/>
    /// until the next acceptance, and a delivery in that state is <c>contract.expired</c> (not
    /// <c>contract.none-active</c>); the same code is returned while a contract is still active but the timeline
    /// already passed its deadline (a re-bound clock, before the next settle); (4) since T320 the reward is
    /// <see cref="ContractRewards"/>: Cash from the offer's ratio and the sale-price modifier read at the delivery instant, RP from the
    /// offer's curve, and the Items the catalog ladder grants at the pre-increment run-wide completion index (every offer counts).
    ///
    /// SetAutoDeliver (T326, FR-010) belongs to the same family: it turns the simulator's auto-delivery of the active delivery contract on or off
    /// and settles. The settle that follows runs the simulator's order (completions, auto-delivery, auto-sales with the reserve) at the command
    /// instant, so a stocked contract is delivered at t = 0. While auto-deliver is on, every other command's settle pre-check
    /// (<see cref="SettleFits"/>) also counts the reward Cash of that delivery, so no command can be followed by a settle overflow.
    /// </summary>
    public sealed partial class RunCommands
    {
        // ---------------------------------------------------------------------------------------------------
        // AcceptContract
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Accepts an unlocked offer while no contract is active. Rejected with command.invalid (unknown or locked
        /// offer, or an exhausted curve), contract.active-exists, contract.cooldown (the offer's last contract ended less than its cooldown ago,
        /// evaluated lazily at the timeline), contract.bonus.unavailable (a bonus offer outside its window or already used),
        /// contract.capacity (a delivery offer asks more than the current storage capacity) or value.limit, in this order; a rejection changes
        /// nothing, the board <see cref="ContractBoard.Version"/> included. Fixes <c>requiredUnits</c> at the
        /// offer's current completion count and, for a timed offer, the deadline <c>timeline + durationMs</c>.
        /// Clears a kept expired outcome.
        /// </summary>
        public RunCommandResult<ContractAcceptResult> AcceptContract(string offerId)
        {
            if (_session.IsLifecycleBlocked) return Reject<ContractAcceptResult>(RunReasons.LifecycleBlocked);
            RunState state = State;
            if (offerId == null || !_catalog.ContractOffers.TryGetValue(offerId, out ContractOfferDef offer) || !state.IsUnlocked(offerId))
                return Reject<ContractAcceptResult>(RunReasons.CommandInvalid);
            ContractBoard board = state.Contracts;
            long count = board.CompletionCount(offerId);
            long now = _session.TimelineUtcMs;
            string blocked = AcceptBlock(state, offer, true, now);
            if (blocked != null) return Reject<ContractAcceptResult>(blocked);
            if (offer.DurationMs.HasValue && !CheckedMath.TryAdd(now, offer.DurationMs.Value, out _))
                return Reject<ContractAcceptResult>(RunReasons.ValueLimit);
            // T327: the settle that follows can complete a production objective at once (the waiting machines may already output the quantity),
            // which grants its reward Cash before the auto-sales; no contract is active yet, so the pre-check is told about the offer being accepted.
            long required = offer.QuantityTable[(int)count];
            if (!SettleFits(state.Cash, null, null, null, null, accepting: offer, acceptingRequired: required))
                return Reject<ContractAcceptResult>(RunReasons.ValueLimit);

            _session.BeginCommand();
            RunStateResult accepted = board.TryAccept(offerId, now);
            if (accepted != RunStateResult.Ok) return Reject<ContractAcceptResult>(RunReasons.From(accepted));
            ActiveContract active = board.Active;
            return _session.EndCommand(new ContractAcceptResult(offerId, count, active.RequiredUnits, active.AcceptedUtcMs, active.DeadlineUtcMs), true);
        }

        /// <summary>
        /// The reason an offer cannot be accepted at <paramref name="nowUtcMs"/>, or null when it can: the one place the command and the snapshot
        /// (<c>CanAccept</c>, <c>AcceptReason</c>) agree on. Order: command.invalid (locked or exhausted curve), contract.active-exists,
        /// contract.cooldown, contract.bonus.unavailable, contract.capacity (a delivery offer's required quantity above the current capacity); the command
        /// then continues with value.limit checks. Normative order (#388, 2026-10-14), see contracts/run-application-f005.md.
        /// </summary>
        internal static string AcceptBlock(RunState state, ContractOfferDef offer, bool unlocked, long nowUtcMs)
        {
            ContractBoard board = state.Contracts;
            long count = board.CompletionCount(offer.Id);
            bool hasNext = count < offer.QuantityTable.Count;
            if (!unlocked || !hasNext) return RunReasons.CommandInvalid;
            if (board.Active != null) return RunReasons.ContractActiveExists;
            if (board.CooldownRemainingMs(offer.Id, nowUtcMs) > 0) return RunReasons.ContractCooldown;
            if (!board.IsBonusAvailable(offer.Id, nowUtcMs)) return RunReasons.ContractBonusUnavailable;
            if (offer.IsDelivery && offer.QuantityTable[(int)count] > state.Inventory.Capacity) return RunReasons.ContractCapacity;
            return null;
        }

        // ---------------------------------------------------------------------------------------------------
        // DeliverContract
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Delivers the active delivery-objective contract from stock. Rejected with contract.none-active, command.invalid (a production objective), contract.expired (the
        /// deadline is inclusive: <c>timeline &lt;= deadline</c> delivers), stock.insufficient or value.limit.
        /// Consumes exactly <c>requiredUnits</c>, grants the Cash and RP rewards and any Item the ladder grants at the run-wide
        /// completion index once, and increments the completion count and the run-wide total, in one checked transaction
        /// (<see cref="RunState.TryDeliverContract"/>). The pre-check covers the reward Cash and RP and the income of
        /// the auto-sales in the settle that follows (less the delivered stock), so a rejection never follows a
        /// mutation. A second delivery finds no active contract, so a reward cannot be granted twice.
        /// </summary>
        public RunCommandResult<ContractDeliveryResult> DeliverContract()
        {
            if (_session.IsLifecycleBlocked) return Reject<ContractDeliveryResult>(RunReasons.LifecycleBlocked);
            RunState state = State;
            ContractBoard board = state.Contracts;
            ActiveContract active = board.Active;
            if (active == null)
                return Reject<ContractDeliveryResult>(board.LastExpired != null ? RunReasons.ContractExpired : RunReasons.ContractNoneActive);
            // A production objective is never delivered from stock (T322): command.invalid, before any deadline or stock check; the snapshot
            // never offers the command for it.
            ContractOfferDef offer = _catalog.ContractOffers[active.OfferId];
            if (!offer.IsDelivery) return Reject<ContractDeliveryResult>(RunReasons.CommandInvalid);
            RunStateResult check = board.CheckComplete(_session.TimelineUtcMs);
            if (check != RunStateResult.Ok) return Reject<ContractDeliveryResult>(RunReasons.From(check));

            long count = board.CompletionCount(active.OfferId);
            if (count >= offer.QuantityTable.Count) return Reject<ContractDeliveryResult>(RunReasons.CommandInvalid);
            check = state.Inventory.CheckRemove(offer.ResourceId, active.RequiredUnits);
            if (check != RunStateResult.Ok) return Reject<ContractDeliveryResult>(RunReasons.From(check));

            // The sale-price modifier is read now, at the completion instant (FR-009, ECO-2), and the same value is used for the pre-check
            // and the commit; the required units were fixed at acceptance.
            if (!_modifiers.TrySalePricePpm(state, Permanent, offer.ResourceId, out long salePricePpm))
                return Reject<ContractDeliveryResult>(RunReasons.ValueLimit);
            check = ContractRewards.CheckGrant(state, Permanent, salePricePpm, out ContractRewardPlan plan);
            if (check != RunStateResult.Ok) return Reject<ContractDeliveryResult>(RunReasons.From(check));
            if (!CheckedMath.TryAdd(state.Cash, plan.Cash, out long cashAfter) ||
                !SettleFits(cashAfter, null, null, null, null, offer.ResourceId, active.RequiredUnits, contractEnds: true))
                return Reject<ContractDeliveryResult>(RunReasons.ValueLimit);
            IdCount[] items = ContractRewards.DueItems(_catalog.ContractItemLadder, plan.RunIndex);

            string offerId = active.OfferId;
            long required = active.RequiredUnits;
            _session.BeginCommand();
            check = state.TryDeliverContract(_session.TimelineUtcMs, Permanent, salePricePpm, out long cashReward, out long rpReward);
            if (check != RunStateResult.Ok) return Reject<ContractDeliveryResult>(RunReasons.From(check));
            return _session.EndCommand(new ContractDeliveryResult(offerId, required, cashReward, rpReward, items, count + 1), true);
        }

        // ---------------------------------------------------------------------------------------------------
        // AbandonContract
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Abandons the active untimed contract with no reward and no penalty; the completion count is unchanged
        /// (OD-4). The UI confirms first; the offer's cooldown starts now (FR-005). Rejected with contract.none-active, or
        /// command.invalid for a timed contract (which only expires), or value.limit when the cooldown stamp would pass 10^18.
        /// </summary>
        public RunCommandResult<ContractAbandonResult> AbandonContract()
        {
            if (_session.IsLifecycleBlocked) return Reject<ContractAbandonResult>(RunReasons.LifecycleBlocked);
            RunState state = State;
            ContractBoard board = state.Contracts;
            ActiveContract active = board.Active;
            if (active == null) return Reject<ContractAbandonResult>(RunReasons.ContractNoneActive);
            if (active.DeadlineUtcMs.HasValue) return Reject<ContractAbandonResult>(RunReasons.CommandInvalid);
            long now = _session.TimelineUtcMs;
            RunStateResult check = board.CheckAbandon(now);
            if (check != RunStateResult.Ok) return Reject<ContractAbandonResult>(RunReasons.From(check));
            if (!SettleFits(state.Cash, null, null, null, null, contractEnds: true)) return Reject<ContractAbandonResult>(RunReasons.ValueLimit);

            string offerId = active.OfferId;
            long count = board.CompletionCount(offerId);
            _session.BeginCommand();
            RunStateResult abandoned = board.TryAbandon(now);
            if (abandoned != RunStateResult.Ok) return Reject<ContractAbandonResult>(RunReasons.From(abandoned));
            return _session.EndCommand(new ContractAbandonResult(offerId, count), true);
        }

        // ---------------------------------------------------------------------------------------------------
        // SetAutoDeliver
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Turns auto-delivery of the active delivery-objective contract on or off (T326, FR-010; off by default, cleared by every ending).
        /// Rejected with lifecycle.blocked, contract.none-active (no contract is active), command.invalid (a production objective, which completes by
        /// production only and whose card never offers the toggle), contract.expired (turning it on after the timeline passed the deadline) or, for
        /// <c>true</c>, value.limit: the same worst-case pre-check as <see cref="DeliverContract"/> (<see cref="ContractRewards.CheckGrant"/> for the
        /// reward Cash, RP and Items, then <see cref="SettleFits"/> with the reward Cash and the stock the delivery would consume), so the settle that follows
        /// can never overflow after the flag changed. A rejection changes nothing. Then it settles: when the stock already holds the required
        /// units, the simulator delivers the contract at this very instant (t = 0 of the settle), granting what <see cref="DeliverContract"/> would
        /// grant, and only the remainder of the stock is auto-sold. The value is the setting that was requested; whether the contract was delivered
        /// by the settle is read from the board and the snapshot (the contract is then gone and the offer's cooldown runs).
        /// </summary>
        public RunCommandResult<bool> SetAutoDeliver(bool enabled)
        {
            if (_session.IsLifecycleBlocked) return Reject<bool>(RunReasons.LifecycleBlocked);
            RunState state = State;
            ContractBoard board = state.Contracts;
            ActiveContract active = board.Active;
            if (active == null) return Reject<bool>(RunReasons.ContractNoneActive);
            ContractOfferDef offer = _catalog.ContractOffers[active.OfferId];
            if (!offer.IsDelivery) return Reject<bool>(RunReasons.CommandInvalid);
            if (enabled)
            {
                if (active.DeadlineUtcMs.HasValue && _session.TimelineUtcMs > active.DeadlineUtcMs.Value) return Reject<bool>(RunReasons.ContractExpired);
                if (!_modifiers.TrySalePricePpm(state, Permanent, offer.ResourceId, out long salePricePpm))
                    return Reject<bool>(RunReasons.ValueLimit);
                RunStateResult check = ContractRewards.CheckGrant(state, Permanent, salePricePpm, out ContractRewardPlan plan);
                if (check != RunStateResult.Ok) return Reject<bool>(RunReasons.From(check));
                long consumed = Math.Min(state.Inventory.Units(offer.ResourceId), active.RequiredUnits);
                if (!CheckedMath.TryAdd(state.Cash, plan.Cash, out long cashAfter) ||
                    !SettleFits(cashAfter, null, null, null, null, offer.ResourceId, consumed, contractEnds: true))
                    return Reject<bool>(RunReasons.ValueLimit);
            }
            else if (!SettleFits(state.Cash, null, null, null, null, contractEnds: true))
            {
                return Reject<bool>(RunReasons.ValueLimit);
            }

            _session.BeginCommand();
            RunStateResult set = board.TrySetAutoDeliver(enabled);
            if (set != RunStateResult.Ok) return Reject<bool>(RunReasons.From(set));
            return _session.EndCommand(enabled, true);
        }
    }
}
