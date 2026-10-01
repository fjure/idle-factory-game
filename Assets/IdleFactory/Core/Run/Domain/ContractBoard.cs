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

    /// <summary>
    /// The outcome of a timed contract that ran past its deadline (data-model.md: "outcome expired"). Derived, never
    /// persisted: it lives from the expiry until the next acceptance so the outcome card can show it.
    /// A value type, so the expiry on the allocation-free advance path allocates nothing.
    /// </summary>
    public readonly struct ExpiredContract
    {
        public string OfferId { get; }
        public long RequiredUnits { get; }
        public long DeadlineUtcMs { get; }

        public ExpiredContract(string offerId, long requiredUnits, long deadlineUtcMs)
        {
            OfferId = offerId;
            RequiredUnits = requiredUnits;
            DeadlineUtcMs = deadlineUtcMs;
        }
    }

    /// <summary>Per-offer completion counts (ordinal offer-ID order) and at most one active contract.</summary>
    public sealed class ContractBoard
    {
        private readonly RunCatalog _catalog;
        private readonly OrdinalCounts _completions = new OrdinalCounts();

        public ActiveContract Active { get; private set; }

        /// <summary>The last contract that expired, or null; cleared by the next acceptance. Not persisted.</summary>
        public ExpiredContract? LastExpired { get; private set; }

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
            if (active == null || !_catalog.ContractOffers.TryGetValue(active.OfferId, out ContractOfferDef offer))
                return RunStateResult.InvalidArgument;
            // The deadline is present exactly for timed offers. The persisted instants are authoritative (owner decision 2026-10-01):
            // only 0 < deadline - accepted <= MaxTimedDurationMs is checked, so a content retune stays save-compatible.
            if (offer.DurationMs.HasValue != active.DeadlineUtcMs.HasValue) return RunStateResult.InvalidArgument;
            if (active.DeadlineUtcMs.HasValue && !IsTimedSpan(active.AcceptedUtcMs, active.DeadlineUtcMs.Value, false))
                return RunStateResult.InvalidArgument;
            Active = active;
            LastExpired = null;
            return RunStateResult.Ok;
        }

        /// <summary>True when <c>0 &lt; end - start &lt;= MaxTimedDurationMs</c> (or <c>0 &lt;=</c> when <paramref name="allowZero"/>) without overflow.</summary>
        internal static bool IsTimedSpan(long start, long end, bool allowZero)
        {
            if (start < 0 || end < 0 || start > ExactBound.Value || end > ExactBound.Value) return false;
            long span = end - start;
            return (allowZero ? span >= 0 : span > 0) && span <= RunLimits.MaxTimedDurationMs;
        }

        public RunStateResult TryAccept(string offerId, long acceptedUtcMs)
        {
            if (offerId == null || acceptedUtcMs < 0 || !_catalog.ContractOffers.TryGetValue(offerId, out ContractOfferDef offer))
                return RunStateResult.InvalidArgument;
            if (Active != null) return RunStateResult.ContractActiveExists;
            long count = _completions.Get(offerId);
            if (count >= offer.QuantityTable.Count) return RunStateResult.InvalidArgument;
            long required = offer.QuantityTable[(int)count];
            long? deadline = null;
            if (offer.DurationMs.HasValue)
            {
                if (!CheckedMath.TryAdd(acceptedUtcMs, offer.DurationMs.Value, out long end)) return RunStateResult.ValueLimit;
                deadline = end;
            }
            if (required < 1) return RunStateResult.InvalidArgument;
            Active = new ActiveContract(offerId, acceptedUtcMs, deadline, required);
            LastExpired = null;
            return RunStateResult.Ok;
        }

        /// <summary>Validates <see cref="TryCompleteActive"/> without changing anything.</summary>
        public RunStateResult CheckComplete(long nowUtcMs)
        {
            if (Active == null) return RunStateResult.ContractNoneActive;
            if (Active.DeadlineUtcMs.HasValue && nowUtcMs > Active.DeadlineUtcMs.Value) return RunStateResult.ContractExpired;
            if (!CheckedMath.TryAdd(_completions.Get(Active.OfferId), 1, out _)) return RunStateResult.ValueLimit;
            return RunStateResult.Ok;
        }

        /// <summary>Counts the delivery: completion count + 1 and the active contract cleared; nothing changes when rejected.</summary>
        public RunStateResult TryCompleteActive(long nowUtcMs)
        {
            RunStateResult check = CheckComplete(nowUtcMs);
            if (check != RunStateResult.Ok) return check;
            _completions.Set(Active.OfferId, _completions.Get(Active.OfferId) + 1, false);
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
            LastExpired = new ExpiredContract(Active.OfferId, Active.RequiredUnits, Active.DeadlineUtcMs.Value);
            Active = null;
            return true;
        }
    }
}
