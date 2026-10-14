using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Units produced toward a production objective since acceptance (T322, data-model.md <c>ActiveContract.Produced</c>). Owned by the
        /// <see cref="ContractBoard"/>: 0 at acceptance, only ever 0 for a delivery objective, cleared with the contract and reset with the run.
        /// Nothing counts into it before T327; trades and mining never touch it (MIN-5).
        /// </summary>
        internal long Produced { get; set; }

        public ActiveContract(string offerId, long acceptedUtcMs, long? deadlineUtcMs, long requiredUnits)
            : this(offerId, acceptedUtcMs, deadlineUtcMs, requiredUnits, 0)
        {
        }

        /// <summary>Restores production progress (the Save V3 load path, T334); <paramref name="produced"/> is within [0, 10^18].</summary>
        internal ActiveContract(string offerId, long acceptedUtcMs, long? deadlineUtcMs, long requiredUnits, long produced)
        {
            if (produced < 0 || produced > ExactBound.Value) throw new ArgumentOutOfRangeException(nameof(produced));
            Produced = produced;
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

    /// <summary>
    /// The bonus-offer state of one bonus offer (data-model.md <c>BonusOfferState</c>): whether the content trigger fired, the UTC instant the
    /// window ends and whether the single use of the run is spent. All three are default (false, 0, false) until the trigger fires.
    /// </summary>
    internal readonly struct BonusOfferState
    {
        public bool Triggered { get; }
        public long WindowEndUtcMs { get; }
        public bool Consumed { get; }

        public BonusOfferState(bool triggered, long windowEndUtcMs, bool consumed)
        {
            Triggered = triggered;
            WindowEndUtcMs = windowEndUtcMs;
            Consumed = consumed;
        }
    }

    /// <summary>
    /// Per-offer completion counts (ordinal offer-ID order), the run-wide completion total and at most one active contract.
    ///
    /// State ownership (T320): <see cref="RunCompletionTotal"/> is the run-wide completion index of the contract Item ladder. It counts every
    /// completed contract of every offer and every path (delivery now, production objectives and auto-delivery later) because the one place
    /// that counts a completion, <see cref="TryCompleteActive"/>, advances it. It is never lowered, it is at least the sum of the
    /// per-offer counts, and it is not a separate persisted member yet: a load derives it from the restored counts
    /// (<see cref="TrySetCompletionCount"/>), exactly the V2 rule of the Save V3 contract. A new run starts at 0.
    ///
    /// State ownership (T321): the board also owns the run-scoped offer cooldowns and the bonus-offer state, both in memory only until the
    /// Save V3 task (T334) persists them. A cooldown is a UTC stamp <c>readyAt = end + CooldownMs</c> started by EVERY ending (delivery,
    /// abandonment, expiry; FR-005) at the instant the contract ended: the command's timeline instant for a delivery or an abandonment, and
    /// <c>deadline + 1</c> for an expiry (the instant the simulator expires it, the same in a live advance and in an offline return, so the stamp
    /// is partition-invariant). Cooldowns and bonus windows are not boundary events: nothing is scheduled, and availability is evaluated
    /// lazily against a timeline instant when a snapshot or <c>AcceptContract</c> asks (<see cref="CooldownRemainingMs"/>,
    /// <see cref="IsBonusAvailable"/>). The storage is a fixed array per offer (ordinal offer-ID order), so the expiry on the allocation-free
    /// advance path allocates nothing. A new run builds a new board, which is the FR-007a reset (cooldowns and bonus state start empty).
    /// <see cref="Version"/> counts every contract-state change; the session folds it into its publish tokens.
    /// </summary>
    public sealed class ContractBoard
    {
        private readonly RunCatalog _catalog;
        private readonly OrdinalCounts _completions = new OrdinalCounts();
        private long _runCompletionTotal;

        // Per offer, indexed by the ordinal position of the offer ID. _readyAt is -1 while the offer never had a cooldown.
        private readonly Dictionary<string, int> _offerIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly long[] _readyAt;
        private readonly bool[] _bonusTriggered;
        private readonly long[] _bonusWindowEnd;
        private readonly bool[] _bonusConsumed;

        public ActiveContract Active { get; private set; }

        /// <summary>
        /// Incremented on every contract-state change (accept, completion, abandonment, expiry, cooldown start, bonus trigger or use, and a
        /// restore). Part of the session publish tokens, so a presenter refreshes on any of them; 0 for a new run.
        /// </summary>
        public long Version { get; private set; }

        /// <summary>The last contract that expired, or null; cleared by the next acceptance. Not persisted.</summary>
        public ExpiredContract? LastExpired { get; private set; }

        public ContractBoard(RunCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            var ids = new List<string>(_catalog.ContractOffers.Keys);
            ids.Sort(StringComparer.Ordinal);
            for (int i = 0; i < ids.Count; i++) _offerIndex[ids[i]] = i;
            _readyAt = new long[ids.Count];
            for (int i = 0; i < _readyAt.Length; i++) _readyAt[i] = -1;
            _bonusTriggered = new bool[ids.Count];
            _bonusWindowEnd = new long[ids.Count];
            _bonusConsumed = new bool[ids.Count];
        }

        // ---------------------------------------------------------------------------------------------------
        // Cooldowns and bonus offers (T321)
        // ---------------------------------------------------------------------------------------------------

        /// <summary>The UTC instant the offer can be accepted again, or null when it never had a cooldown. Derived from the stamp, not from "now".</summary>
        internal long? CooldownReadyAtUtcMs(string offerId)
        {
            if (offerId == null || !_offerIndex.TryGetValue(offerId, out int i) || _readyAt[i] < 0) return null;
            return _readyAt[i];
        }

        /// <summary>Milliseconds until the offer's cooldown ends at <paramref name="nowUtcMs"/> (0 when none runs or it has elapsed; ready exactly at <c>readyAt</c>).</summary>
        internal long CooldownRemainingMs(string offerId, long nowUtcMs)
        {
            long? readyAt = CooldownReadyAtUtcMs(offerId);
            return readyAt.HasValue && readyAt.Value > nowUtcMs ? readyAt.Value - nowUtcMs : 0;
        }

        /// <summary>The bonus state of an offer; all-default for a standing or unknown offer.</summary>
        internal BonusOfferState BonusState(string offerId)
        {
            if (offerId == null || !_offerIndex.TryGetValue(offerId, out int i)) return default;
            return new BonusOfferState(_bonusTriggered[i], _bonusWindowEnd[i], _bonusConsumed[i]);
        }

        /// <summary>
        /// Milliseconds the bonus window of a triggered bonus offer stays open at <paramref name="nowUtcMs"/> (0 once it has ended); null for a
        /// standing offer and for a bonus offer whose trigger has not fired. The window is open while <c>now &lt; windowEnd</c>.
        /// </summary>
        internal long? BonusWindowRemainingMs(string offerId, long nowUtcMs)
        {
            BonusOfferState state = BonusState(offerId);
            if (!state.Triggered) return null;
            return state.WindowEndUtcMs > nowUtcMs ? state.WindowEndUtcMs - nowUtcMs : 0;
        }

        /// <summary>True for a standing offer; for a bonus offer when it is triggered, unused and inside its window at <paramref name="nowUtcMs"/>.</summary>
        internal bool IsBonusAvailable(string offerId, long nowUtcMs)
        {
            if (offerId == null || !_catalog.ContractOffers.TryGetValue(offerId, out ContractOfferDef offer)) return false;
            if (offer.Class != "bonus") return true;
            BonusOfferState state = BonusState(offerId);
            return state.Triggered && !state.Consumed && nowUtcMs < state.WindowEndUtcMs;
        }

        /// <summary>
        /// Records the bonus trigger of a bonus offer once per run: <c>Triggered</c> and the window end <c>trigger + WindowMs</c>, nothing else (no
        /// event is scheduled). The trigger itself (the unlock pass) is a later task. Rejected without change for a standing or unknown offer, a
        /// second trigger, or a window end past the exact bound.
        /// </summary>
        internal RunStateResult TryTriggerBonus(string offerId, long triggerUtcMs)
        {
            if (offerId == null || triggerUtcMs < 0 || !_offerIndex.TryGetValue(offerId, out int i) ||
                !_catalog.ContractOffers.TryGetValue(offerId, out ContractOfferDef offer) || offer.Class != "bonus" || offer.Bonus == null)
                return RunStateResult.InvalidArgument;
            if (_bonusTriggered[i]) return RunStateResult.InvalidArgument;
            if (!CheckedMath.TryAdd(triggerUtcMs, offer.Bonus.WindowMs, out long end)) return RunStateResult.ValueLimit;
            _bonusTriggered[i] = true;
            _bonusWindowEnd[i] = end;
            Version++;
            return RunStateResult.Ok;
        }

        /// <summary>
        /// Every ending starts the offer's cooldown at the end instant and spends a bonus offer's single use. A stamp past the exact bound
        /// saturates at the bound (never ready); the command paths reject such an ending beforehand with value.limit.
        /// </summary>
        private void EndContract(ActiveContract ended, long endUtcMs)
        {
            int i = _offerIndex[ended.OfferId];
            ContractOfferDef offer = _catalog.ContractOffers[ended.OfferId];
            if (offer.CooldownMs > 0)
                _readyAt[i] = CheckedMath.TryAdd(endUtcMs, offer.CooldownMs, out long readyAt) ? readyAt : ExactBound.Value;
            else
                _readyAt[i] = -1;
            if (offer.Class == "bonus" && _bonusTriggered[i]) _bonusConsumed[i] = true;
            Active = null;
            Version++;
        }

        private bool CooldownFits(string offerId, long endUtcMs) =>
            CheckedMath.TryAdd(endUtcMs, _catalog.ContractOffers[offerId].CooldownMs, out _);

        public long CompletionCount(string offerId) => _completions.Get(offerId);

        /// <summary>
        /// The run-wide number of completed contracts, the index at which the next completion is looked up on the Item ladder (the
        /// pre-increment index of its reward). At least the sum of <see cref="CompletionCounts"/>; reset with the run.
        /// </summary>
        public long RunCompletionTotal => _runCompletionTotal;

        /// <summary>Non-zero completion counts in ordinal offer-ID order.</summary>
        public IdCount[] CompletionCounts() => _completions.ToArray();

        /// <summary>Restores a persisted count (load path); rejects unknown offers and out-of-range values.</summary>
        public RunStateResult TrySetCompletionCount(string offerId, long count)
        {
            if (offerId == null || !_catalog.ContractOffers.ContainsKey(offerId)) return RunStateResult.InvalidArgument;
            if (!ExactBound.Contains(count)) return RunStateResult.ValueLimit;
            long previous = _completions.Get(offerId);
            _completions.Set(offerId, count, true);
            // The run-wide total follows the counts: it grows by the added completions and never shrinks (a lowered or removed count keeps
            // the Items already granted from being granted again). Overflow rejects the restore, and nothing changes.
            if (count > previous)
            {
                if (!CheckedMath.TryAdd(_runCompletionTotal, count - previous, out long total))
                {
                    _completions.Set(offerId, previous, true);
                    return RunStateResult.ValueLimit;
                }
                _runCompletionTotal = total;
            }
            Version++;
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
            // Production progress exists for a production objective only.
            if (active.Produced != 0 && offer.Objective != "produce") return RunStateResult.InvalidArgument;
            if (active.DeadlineUtcMs.HasValue && !IsTimedSpan(active.AcceptedUtcMs, active.DeadlineUtcMs.Value, false))
                return RunStateResult.InvalidArgument;
            Active = active;
            LastExpired = null;
            Version++;
            return RunStateResult.Ok;
        }

        /// <summary>True when <c>0 &lt; end - start &lt;= MaxTimedDurationMs</c> (or <c>0 &lt;=</c> when <paramref name="allowZero"/>) without overflow.</summary>
        internal static bool IsTimedSpan(long start, long end, bool allowZero)
        {
            if (start < 0 || end < 0 || start > ExactBound.Value || end > ExactBound.Value) return false;
            long span = end - start;
            return (allowZero ? span >= 0 : span > 0) && span <= RunLimits.MaxTimedDurationMs;
        }

        internal RunStateResult TryAccept(string offerId, long acceptedUtcMs)
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
            // A production objective starts at Produced = 0 (the constructor default); the capacity check is a delivery-objective rule in AcceptBlock.
            Active = new ActiveContract(offerId, acceptedUtcMs, deadline, required);
            LastExpired = null;
            Version++;
            return RunStateResult.Ok;
        }

        /// <summary>Validates <see cref="TryCompleteActive"/> without changing anything.</summary>
        public RunStateResult CheckComplete(long nowUtcMs)
        {
            if (Active == null) return RunStateResult.ContractNoneActive;
            if (Active.DeadlineUtcMs.HasValue && nowUtcMs > Active.DeadlineUtcMs.Value) return RunStateResult.ContractExpired;
            if (!CheckedMath.TryAdd(_completions.Get(Active.OfferId), 1, out _) || !CheckedMath.TryAdd(_runCompletionTotal, 1, out _))
                return RunStateResult.ValueLimit;
            // The cooldown stamp this ending starts must fit, so a completion is never half applied.
            if (!CooldownFits(Active.OfferId, nowUtcMs)) return RunStateResult.ValueLimit;
            return RunStateResult.Ok;
        }

        /// <summary>Counts the completion: completion count + 1, run-wide total + 1 and the active contract cleared; nothing changes when rejected.</summary>
        internal RunStateResult TryCompleteActive(long nowUtcMs)
        {
            RunStateResult check = CheckComplete(nowUtcMs);
            if (check != RunStateResult.Ok) return check;
            _completions.Set(Active.OfferId, _completions.Get(Active.OfferId) + 1, false);
            _runCompletionTotal++;
            EndContract(Active, nowUtcMs);
            return RunStateResult.Ok;
        }

        /// <summary>Validates <see cref="TryAbandon"/> without changing anything.</summary>
        internal RunStateResult CheckAbandon(long nowUtcMs)
        {
            if (Active == null) return RunStateResult.ContractNoneActive;
            if (Active.DeadlineUtcMs.HasValue) return RunStateResult.InvalidArgument;
            if (!CooldownFits(Active.OfferId, nowUtcMs)) return RunStateResult.ValueLimit;
            return RunStateResult.Ok;
        }

        /// <summary>Abandons an untimed contract with no reward and no count change (OD-4); the offer's cooldown starts at <paramref name="nowUtcMs"/> (FR-005).</summary>
        internal RunStateResult TryAbandon(long nowUtcMs)
        {
            RunStateResult check = CheckAbandon(nowUtcMs);
            if (check != RunStateResult.Ok) return check;
            EndContract(Active, nowUtcMs);
            return RunStateResult.Ok;
        }

        /// <summary>Clears a timed contract whose deadline passed (<c>now &gt; deadline</c>); the count is unchanged.</summary>
        internal bool TryExpire(long nowUtcMs)
        {
            if (Active == null || !Active.DeadlineUtcMs.HasValue || nowUtcMs <= Active.DeadlineUtcMs.Value) return false;
            LastExpired = new ExpiredContract(Active.OfferId, Active.RequiredUnits, Active.DeadlineUtcMs.Value);
            // The contract ended at the first millisecond past its inclusive deadline, which is the instant of the expiry event in a live
            // advance and in an offline return alike, so the cooldown stamp does not depend on when the expiry was processed.
            EndContract(Active, Active.DeadlineUtcMs.Value + 1);
            return true;
        }
    }
}
