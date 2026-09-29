using System;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;

namespace IdleFactory.Run.Domain
{
    /// <summary>
    /// The accepted contract. <c>RequiredUnits</c> is fixed at acceptance and
    /// <c>DeadlineUtcMs = AcceptedUtcMs + DurationMs</c> (absent for untimed contracts).
    /// </summary>
    public sealed class ActiveContract
    {
        public string OfferId { get; }
        public long AcceptedUtcMs { get; }
        public long? DeadlineUtcMs { get; }
        public long RequiredUnits { get; }

        public ActiveContract(string offerId, long acceptedUtcMs, long? deadlineUtcMs, long requiredUnits)
        {
            if (string.IsNullOrEmpty(offerId)) throw new ArgumentException("Offer ID is required.", nameof(offerId));
            if (acceptedUtcMs < 0) throw new ArgumentOutOfRangeException(nameof(acceptedUtcMs));
            if (deadlineUtcMs.HasValue && deadlineUtcMs.Value < acceptedUtcMs) throw new ArgumentOutOfRangeException(nameof(deadlineUtcMs));
            if (requiredUnits < 1 || requiredUnits > ExactBound.Value) throw new ArgumentOutOfRangeException(nameof(requiredUnits));
            OfferId = offerId;
            AcceptedUtcMs = acceptedUtcMs;
            DeadlineUtcMs = deadlineUtcMs;
            RequiredUnits = requiredUnits;
        }
    }

    /// <summary>Per-offer completion counts (ordinal offer-ID order) and at most one active contract.</summary>
    public sealed class ContractBoard
    {
        private readonly RunCatalog _catalog;
        private readonly OrdinalCounts _completions = new OrdinalCounts();

        public ActiveContract Active { get; private set; }

        public ContractBoard(RunCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public long CompletionCount(string offerId) => _completions.Get(offerId);

        /// <summary>Non-zero completion counts in ordinal offer-ID order.</summary>
        public IdCount[] CompletionCounts() => _completions.ToArray();

        /// <summary>Restores a persisted count (load path); rejects unknown offers and out-of-range values.</summary>
        public RunStateResult TrySetCompletionCount(string offerId, long count)
        {
            if (offerId == null || !_catalog.ContractOffers.ContainsKey(offerId)) return RunStateResult.InvalidArgument;
            if (!ExactBound.Contains(count)) return RunStateResult.ValueLimit;
            _completions.Set(offerId, count, true);
            return RunStateResult.Ok;
        }

        /// <summary>Restores a persisted active contract (load path).</summary>
        public RunStateResult TrySetActive(ActiveContract active)
        {
            if (active == null || !_catalog.ContractOffers.ContainsKey(active.OfferId)) return RunStateResult.InvalidArgument;
            Active = active;
            return RunStateResult.Ok;
        }

        public RunStateResult TryAccept(string offerId, long acceptedUtcMs)
        {
            if (offerId == null || acceptedUtcMs < 0 || !_catalog.ContractOffers.TryGetValue(offerId, out ContractOfferDef offer))
                return RunStateResult.InvalidArgument;
            if (Active != null) return RunStateResult.ContractActiveExists;
            long count = _completions.Get(offerId);
            if (count >= offer.QuantityTable.Count) return RunStateResult.ValueLimit;
            long required = offer.QuantityTable[(int)count];
            long? deadline = null;
            if (offer.DurationMs.HasValue)
            {
                if (!CheckedMath.TryAdd(acceptedUtcMs, offer.DurationMs.Value, out long end)) return RunStateResult.ValueLimit;
                deadline = end;
            }
            if (required < 1) return RunStateResult.InvalidArgument;
            Active = new ActiveContract(offerId, acceptedUtcMs, deadline, required);
            return RunStateResult.Ok;
        }

        /// <summary>Counts the delivery: completion count + 1 and the active contract cleared; nothing changes when rejected.</summary>
        public RunStateResult TryCompleteActive(long nowUtcMs)
        {
            if (Active == null) return RunStateResult.ContractNoneActive;
            if (Active.DeadlineUtcMs.HasValue && nowUtcMs > Active.DeadlineUtcMs.Value) return RunStateResult.ContractExpired;
            if (!CheckedMath.TryAdd(_completions.Get(Active.OfferId), 1, out long next)) return RunStateResult.ValueLimit;
            _completions.Set(Active.OfferId, next, false);
            Active = null;
            return RunStateResult.Ok;
        }

        /// <summary>Abandons an untimed contract with no reward and no count change (OD-4).</summary>
        public RunStateResult TryAbandon()
        {
            if (Active == null) return RunStateResult.ContractNoneActive;
            if (Active.DeadlineUtcMs.HasValue) return RunStateResult.InvalidArgument;
            Active = null;
            return RunStateResult.Ok;
        }

        /// <summary>Clears a timed contract whose deadline passed (<c>now &gt; deadline</c>); the count is unchanged.</summary>
        public bool TryExpire(long nowUtcMs)
        {
            if (Active == null || !Active.DeadlineUtcMs.HasValue || nowUtcMs <= Active.DeadlineUtcMs.Value) return false;
            Active = null;
            return true;
        }
    }
}
