using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Run.Application
{
    // -------------------------------------------------------------------------------------------------------
    // ContractsSnapshot (FR-012, FR-014, ui-surfaces.md Contracts)
    // -------------------------------------------------------------------------------------------------------

    /// <summary>One reward of an offer at the completion index it would be granted at (or that was granted).</summary>
    public readonly struct ContractRewardView
    {
        /// <summary><c>cash</c>, <c>rp</c> or <c>item</c>.</summary>
        public string Kind { get; }

        /// <summary>The Cash or RP amount, or the Item count due at this index.</summary>
        public long Amount { get; }

        /// <summary>The Item ID of an <c>item</c> reward, otherwise null.</summary>
        public string ItemId { get; }

        public ContractRewardView(string kind, long amount, string itemId)
        {
            Kind = kind;
            Amount = amount;
            ItemId = itemId;
        }
    }

    /// <summary>An offer of the catalog: what accepting it now would ask and give, and whether it can be accepted.</summary>
    public sealed class ContractOfferSnapshot
    {
        public string OfferId { get; }

        /// <summary><c>plate</c>, <c>gear</c> or <c>timedBar</c>.</summary>
        public string Kind { get; }
        public string ResourceId { get; }
        public bool Unlocked { get; }

        /// <summary>The condition that unlocks the offer, for the "Contracts unlock after ..." copy.</summary>
        public UnlockCondition Unlock { get; }

        /// <summary>Completions of this offer in the current run; the scaling index.</summary>
        public long CompletionCount { get; }

        /// <summary>The units the next acceptance would fix; 0 when the curve is exhausted.</summary>
        public long RequiredUnits { get; }

        /// <summary>The duration of a timed offer, otherwise null.</summary>
        public long? DurationMs { get; }

        /// <summary>The Cash and RP of the next completion and the Items due at that index (none when the curve is exhausted).</summary>
        public IReadOnlyList<ContractRewardView> Rewards { get; }

        /// <summary>Enabled only when the offer is unlocked, its curve has a next value and no contract is active.</summary>
        public bool CanAccept { get; }

        /// <summary><c>contract.active-exists</c> while another contract is active, <c>command.invalid</c> for a locked or exhausted offer, otherwise null.</summary>
        public string AcceptReason { get; }

        internal ContractOfferSnapshot(ContractOfferDef def, bool unlocked, long completionCount, long requiredUnits,
            ContractRewardView[] rewards, bool canAccept, string acceptReason)
        {
            OfferId = def.Id;
            Kind = def.Kind;
            ResourceId = def.ResourceId;
            Unlocked = unlocked;
            Unlock = def.Unlock;
            CompletionCount = completionCount;
            RequiredUnits = requiredUnits;
            DurationMs = def.DurationMs;
            Rewards = new ReadOnlyCollection<ContractRewardView>(rewards);
            CanAccept = canAccept;
            AcceptReason = acceptReason;
        }
    }

    /// <summary>The accepted contract with its progress against stock and its countdown.</summary>
    public sealed class ActiveContractSnapshot
    {
        public string OfferId { get; }
        public string Kind { get; }
        public string ResourceId { get; }
        public long RequiredUnits { get; }

        /// <summary>The stock of the contract resource right now.</summary>
        public long StockUnits { get; }

        /// <summary>The units still missing, <c>max(0, required - stock)</c>.</summary>
        public long MissingUnits { get; }
        public long AcceptedUtcMs { get; }

        /// <summary>The inclusive deadline of a timed contract, otherwise null.</summary>
        public long? DeadlineUtcMs { get; }

        /// <summary>Milliseconds to the deadline at the timeline (0 once passed); null for an untimed contract.</summary>
        public long? RemainingMs { get; }

        /// <summary>The scaling index of this contract: the completions of the offer when it was accepted.</summary>
        public long CompletionIndex { get; }
        public IReadOnlyList<ContractRewardView> Rewards { get; }
        public bool CanDeliver { get; }

        /// <summary><c>contract.expired</c> or <c>stock.insufficient</c> when delivery is disabled, otherwise null.</summary>
        public string DeliverReason { get; }

        /// <summary>Only an untimed contract can be abandoned (OD-4).</summary>
        public bool CanAbandon { get; }

        internal ActiveContractSnapshot(ContractOfferDef def, ActiveContract active, long stockUnits, long remainingMs, long completionIndex,
            ContractRewardView[] rewards, bool canDeliver, string deliverReason)
        {
            OfferId = def.Id;
            Kind = def.Kind;
            ResourceId = def.ResourceId;
            RequiredUnits = active.RequiredUnits;
            StockUnits = stockUnits;
            MissingUnits = stockUnits >= active.RequiredUnits ? 0 : active.RequiredUnits - stockUnits;
            AcceptedUtcMs = active.AcceptedUtcMs;
            DeadlineUtcMs = active.DeadlineUtcMs;
            RemainingMs = active.DeadlineUtcMs.HasValue ? remainingMs : (long?)null;
            CompletionIndex = completionIndex;
            Rewards = new ReadOnlyCollection<ContractRewardView>(rewards);
            CanDeliver = canDeliver;
            DeliverReason = deliverReason;
            CanAbandon = !active.DeadlineUtcMs.HasValue;
        }
    }

    /// <summary>The kept outcome of a timed contract that expired: no reward, no penalty, count unchanged.</summary>
    public sealed class ExpiredContractSnapshot
    {
        public string OfferId { get; }
        public string Kind { get; }
        public long RequiredUnits { get; }
        public long DeadlineUtcMs { get; }

        internal ExpiredContractSnapshot(ContractOfferDef def, ExpiredContract expired)
        {
            OfferId = def.Id;
            Kind = def.Kind;
            RequiredUnits = expired.RequiredUnits;
            DeadlineUtcMs = expired.DeadlineUtcMs;
        }
    }

    /// <summary>Offers, the active contract and the last expired outcome (which stays until the next acceptance).</summary>
    public sealed class ContractsSnapshot
    {
        public long StructureRevision { get; }
        public long QuantityRevision { get; }
        public long TimelineUtcMs { get; }

        /// <summary>Every offer of the catalog in ordinal ID order.</summary>
        public IReadOnlyList<ContractOfferSnapshot> Offers { get; }

        /// <summary>The active contract, or null.</summary>
        public ActiveContractSnapshot Active { get; }

        /// <summary>The expired outcome for the outcome card, or null.</summary>
        public ExpiredContractSnapshot LastExpired { get; }

        internal ContractsSnapshot(long structureRevision, long quantityRevision, long timelineUtcMs, ContractOfferSnapshot[] offers,
            ActiveContractSnapshot active, ExpiredContractSnapshot lastExpired)
        {
            StructureRevision = structureRevision;
            QuantityRevision = quantityRevision;
            TimelineUtcMs = timelineUtcMs;
            Offers = new ReadOnlyCollection<ContractOfferSnapshot>(offers);
            Active = active;
            LastExpired = lastExpired;
        }
    }

    public static partial class RunSnapshots
    {
        public static ContractsSnapshot Contracts(RunSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            RunCatalog catalog = session.Catalog;
            RunState state = session.State;
            ContractBoard board = state.Contracts;
            var modifiers = new RunModifiers(catalog);
            var ids = new List<string>(catalog.ContractOffers.Keys);
            ids.Sort(StringComparer.Ordinal);

            var offers = new ContractOfferSnapshot[ids.Count];
            for (int i = 0; i < offers.Length; i++)
            {
                ContractOfferDef def = catalog.ContractOffers[ids[i]];
                bool unlocked = state.IsUnlocked(def.Id);
                long count = board.CompletionCount(def.Id);
                bool hasNext = count < def.QuantityTable.Count;
                string reason = !unlocked || !hasNext ? RunReasons.CommandInvalid : board.Active != null ? RunReasons.ContractActiveExists : null;
                offers[i] = new ContractOfferSnapshot(def, unlocked, count, hasNext ? def.QuantityTable[(int)count] : 0,
                    hasNext ? RewardViews(session, modifiers, def, def.QuantityTable[(int)count], count) : new ContractRewardView[0], reason == null, reason);
            }

            ActiveContractSnapshot active = null;
            ActiveContract a = board.Active;
            if (a != null)
            {
                ContractOfferDef def = catalog.ContractOffers[a.OfferId];
                long timeline = session.TimelineUtcMs;
                long stock = state.Inventory.Units(def.ResourceId);
                bool expired = a.DeadlineUtcMs.HasValue && timeline > a.DeadlineUtcMs.Value;
                bool enough = stock >= a.RequiredUnits;
                long remaining = a.DeadlineUtcMs.HasValue && !expired ? a.DeadlineUtcMs.Value - timeline : 0;
                long index = board.CompletionCount(def.Id);
                active = new ActiveContractSnapshot(def, a, stock, remaining, index, index < def.QuantityTable.Count ? RewardViews(session, modifiers, def, a.RequiredUnits, index) : new ContractRewardView[0],
                    !expired && enough, expired ? RunReasons.ContractExpired : enough ? null : RunReasons.StockInsufficient);
            }

            ExpiredContractSnapshot lastExpired = board.LastExpired.HasValue
                ? new ExpiredContractSnapshot(catalog.ContractOffers[board.LastExpired.Value.OfferId], board.LastExpired.Value)
                : null;
            return new ContractsSnapshot(session.StructureRevision, session.QuantityRevision, session.TimelineUtcMs, offers, active, lastExpired);
        }

        /// <summary>
        /// What completing <paramref name="def"/> with <paramref name="requiredUnits"/> would grant right now (T320, <see cref="ContractRewards"/>):
        /// the Cash at the current sale-price modifier of its resource, the RP at the offer's own completion count
        /// <paramref name="count"/>, then the Items the ladder grants at the current run-wide completion index. A reward that cannot be computed
        /// (a value past 10^18) is left out, as the delivery would be rejected with value.limit.
        /// </summary>
        private static ContractRewardView[] RewardViews(RunSession session, RunModifiers modifiers, ContractOfferDef def, long requiredUnits, long count)
        {
            var list = new List<ContractRewardView>();
            RunState state = session.State;
            if (modifiers.TrySalePricePpm(state, session.Permanent, def.ResourceId, out long ppm) &&
                ContractRewards.TryCash(requiredUnits, session.Catalog.Resources[def.ResourceId].BasePrice, ppm, def.RewardRatioPpm, out long cash))
                list.Add(new ContractRewardView("cash", cash, null));
            if (ContractRewards.TryRp(def, count, out long rp)) list.Add(new ContractRewardView("rp", rp, null));
            foreach (IdCount item in ContractRewards.DueItems(session.Catalog.ContractItemLadder, state.Contracts.RunCompletionTotal))
                list.Add(new ContractRewardView("item", item.Count, item.Id));
            return list.ToArray();
        }
    }
}
