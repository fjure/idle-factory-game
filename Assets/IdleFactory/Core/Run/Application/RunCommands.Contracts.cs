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
    /// already passed its deadline (a re-bound clock, before the next settle); (4) an Item reward is granted when
    /// the completion index of the delivery is one of its <c>onCompletionIndices</c>, once per matching reward entry.
    /// </summary>
    public sealed partial class RunCommands
    {
        // ---------------------------------------------------------------------------------------------------
        // AcceptContract
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Accepts an unlocked offer while no contract is active. Rejected with command.invalid (unknown or locked
        /// offer, or an exhausted curve), contract.active-exists or value.limit. Fixes <c>requiredUnits</c> at the
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
            if (board.Active != null) return Reject<ContractAcceptResult>(RunReasons.ContractActiveExists);
            long count = board.CompletionCount(offerId);
            if (count >= offer.QuantityTable.Count) return Reject<ContractAcceptResult>(RunReasons.CommandInvalid);
            long now = _session.TimelineUtcMs;
            if (offer.DurationMs.HasValue && !CheckedMath.TryAdd(now, offer.DurationMs.Value, out _))
                return Reject<ContractAcceptResult>(RunReasons.ValueLimit);
            if (!SettleFits(state.Cash, null, null, null, null)) return Reject<ContractAcceptResult>(RunReasons.ValueLimit);

            _session.BeginCommand();
            RunStateResult accepted = board.TryAccept(offerId, now);
            if (accepted != RunStateResult.Ok) return Reject<ContractAcceptResult>(RunReasons.From(accepted));
            ActiveContract active = board.Active;
            return _session.EndCommand(new ContractAcceptResult(offerId, count, active.RequiredUnits, active.AcceptedUtcMs, active.DeadlineUtcMs), true);
        }

        // ---------------------------------------------------------------------------------------------------
        // DeliverContract
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Delivers the active contract from stock. Rejected with contract.none-active, contract.expired (the
        /// deadline is inclusive: <c>timeline &lt;= deadline</c> delivers), stock.insufficient or value.limit.
        /// Consumes exactly <c>requiredUnits</c>, grants the Cash and RP rewards and any Item due at the completion
        /// index once, and increments the completion count, in one checked transaction
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
            RunStateResult check = board.CheckComplete(_session.TimelineUtcMs);
            if (check != RunStateResult.Ok) return Reject<ContractDeliveryResult>(RunReasons.From(check));

            ContractOfferDef offer = _catalog.ContractOffers[active.OfferId];
            long count = board.CompletionCount(active.OfferId);
            if (count >= offer.QuantityTable.Count) return Reject<ContractDeliveryResult>(RunReasons.CommandInvalid);
            check = state.Inventory.CheckRemove(offer.ResourceId, active.RequiredUnits);
            if (check != RunStateResult.Ok) return Reject<ContractDeliveryResult>(RunReasons.From(check));

            long cash = 0, rp = 0;
            for (int i = 0; i < offer.Rewards.Count; i++)
            {
                ContractRewardDef reward = offer.Rewards[i];
                if (reward.Kind == "cash" && !CheckedMath.TryAdd(cash, reward.ValueTable[(int)count], out cash))
                    return Reject<ContractDeliveryResult>(RunReasons.ValueLimit);
                if (reward.Kind == "rp" && !CheckedMath.TryAdd(rp, reward.ValueTable[(int)count], out rp))
                    return Reject<ContractDeliveryResult>(RunReasons.ValueLimit);
            }
            if (!CheckedMath.TryAdd(state.Cash, cash, out long cashAfter) || !CheckedMath.TryAdd(state.Rp, rp, out _) ||
                !SettleFits(cashAfter, null, null, null, null, offer.ResourceId, active.RequiredUnits))
                return Reject<ContractDeliveryResult>(RunReasons.ValueLimit);
            IdCount[] items = DueItems(offer, count);
            for (int i = 0; i < items.Length; i++)
            {
                if (Permanent == null) return Reject<ContractDeliveryResult>(RunReasons.CommandInvalid);
                check = Permanent.CheckAddItems(items[i].Id, items[i].Count);
                if (check != RunStateResult.Ok) return Reject<ContractDeliveryResult>(RunReasons.From(check));
            }

            string offerId = active.OfferId;
            long required = active.RequiredUnits;
            _session.BeginCommand();
            check = state.TryDeliverContract(_session.TimelineUtcMs, Permanent, out long cashReward, out long rpReward);
            if (check != RunStateResult.Ok) return Reject<ContractDeliveryResult>(RunReasons.From(check));
            return _session.EndCommand(new ContractDeliveryResult(offerId, required, cashReward, rpReward, items, count + 1), true);
        }

        // ---------------------------------------------------------------------------------------------------
        // AbandonContract
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Abandons the active untimed contract with no reward and no penalty; the completion count is unchanged
        /// (OD-4). The UI confirms first. Rejected with contract.none-active, or command.invalid for a timed
        /// contract (which only expires).
        /// </summary>
        public RunCommandResult<ContractAbandonResult> AbandonContract()
        {
            if (_session.IsLifecycleBlocked) return Reject<ContractAbandonResult>(RunReasons.LifecycleBlocked);
            RunState state = State;
            ContractBoard board = state.Contracts;
            ActiveContract active = board.Active;
            if (active == null) return Reject<ContractAbandonResult>(RunReasons.ContractNoneActive);
            if (active.DeadlineUtcMs.HasValue) return Reject<ContractAbandonResult>(RunReasons.CommandInvalid);
            if (!SettleFits(state.Cash, null, null, null, null)) return Reject<ContractAbandonResult>(RunReasons.ValueLimit);

            string offerId = active.OfferId;
            long count = board.CompletionCount(offerId);
            _session.BeginCommand();
            RunStateResult abandoned = board.TryAbandon();
            if (abandoned != RunStateResult.Ok) return Reject<ContractAbandonResult>(RunReasons.From(abandoned));
            return _session.EndCommand(new ContractAbandonResult(offerId, count), true);
        }

        /// <summary>The Items granted at <paramref name="completionIndex"/>, one count per matching reward entry, in ordinal Item-ID order.</summary>
        internal static IdCount[] DueItems(ContractOfferDef offer, long completionIndex)
        {
            var ids = new List<string>();
            for (int i = 0; i < offer.Rewards.Count; i++)
            {
                ContractRewardDef reward = offer.Rewards[i];
                if (reward.Kind != "item" || !IsDueAt(reward, completionIndex) || ids.Contains(reward.ItemId)) continue;
                ids.Add(reward.ItemId);
            }
            ids.Sort(StringComparer.Ordinal);
            var due = new IdCount[ids.Count];
            for (int n = 0; n < due.Length; n++)
            {
                long units = 0;
                for (int i = 0; i < offer.Rewards.Count; i++)
                    if (offer.Rewards[i].ItemId == ids[n] && IsDueAt(offer.Rewards[i], completionIndex)) units++;
                due[n] = new IdCount(ids[n], units);
            }
            return due;
        }

        internal static bool IsDueAt(ContractRewardDef reward, long completionIndex)
        {
            if (reward.Kind != "item") return false;
            for (int i = 0; i < reward.OnCompletionIndices.Count; i++)
                if (reward.OnCompletionIndices[i] == completionIndex) return true;
            return false;
        }
    }
}
