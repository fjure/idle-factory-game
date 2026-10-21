using System;
using System.Collections.Generic;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Run.Application
{
    /// <summary>How an offline reconcile ended (<see cref="RunOfflineSummary.Outcome"/>).</summary>
    public enum RunOfflineOutcome
    {
        /// <summary>The interval was valid and the mapped advance was applied (a zero-length interval included).</summary>
        Applied = 0,

        /// <summary>The return instant is before the departure. Zero credit; the run state is unchanged (R05).</summary>
        BackwardClock = 1,

        /// <summary>No return instant. Zero credit; the run state is unchanged (R05).</summary>
        MissingClock = 2,

        /// <summary>An instant lies outside the exact bound. Zero credit; the run state is unchanged.</summary>
        ClockUnrepresentable = 3,

        /// <summary>
        /// The simulator rejected the advance (see <see cref="RunOfflineSummary.SimulationResult"/>). The run state is
        /// unchanged. Retrying the same return would fail forever, so the persistence layer (T105, owner decision
        /// 2026-10-01) forfeits the credit: the durable return keeps the state as it was before the absence, advances
        /// the departure to the return instant and reports <see cref="RunOfflineSummary.Code"/> =
        /// <see cref="RunOfflineSummary.ValueLimitCode"/>.
        /// </summary>
        SimulationRejected = 4,
    }

    /// <summary>What happened to the active contract during the absence.</summary>
    public enum RunOfflineContractKind
    {
        /// <summary>No contract was active at departure.</summary>
        None = 0,

        /// <summary>The contract is still active at return (its deadline lies after the return, or it has none).</summary>
        StillActive = 1,

        /// <summary>The contract's deadline passed during the absence and it expired (no reward, count unchanged).</summary>
        Expired = 2,

        /// <summary>
        /// The contract had auto-deliver on and the simulator delivered it during the absence (T326): the reward was granted once and the offer's
        /// cooldown runs from the delivery instant. The reward totals and the delivery notice are on the summary (<see cref="RunOfflineSummary.AutoDelivered"/>, T327).
        /// </summary>
        AutoDelivered = 3,

        /// <summary>
        /// A production objective reached its quantity during the absence and the simulator completed it (T327): the reward was granted once and the
        /// offer's cooldown runs from the completion instant. No stock was consumed.
        /// </summary>
        ProduceCompleted = 4,
    }

    /// <summary>Contract outcome of an offline return: still active, expired at its deadline, delivered by auto-delivery, or completed by production (the automatic endings), with the reward the completion granted.</summary>
    public readonly struct RunOfflineContractOutcome
    {
        public RunOfflineContractKind Kind { get; }
        public string OfferId { get; }
        public long RequiredUnits { get; }

        /// <summary>The inclusive deadline (Unix ms) of the contract, when it had one.</summary>
        public long? DeadlineUtcMs { get; }

        /// <summary>The instant (Unix ms) the contract ended during the absence (the completion stamp, or <c>deadline + 1</c>); null while it is still active or none was active.</summary>
        public long? EndedUtcMs { get; }

        /// <summary>The Cash the completion granted (the auto-delivery or production reward); 0 for an expiry and for a contract still active.</summary>
        public long CashReward { get; }

        /// <summary>The RP the completion granted; 0 for an expiry and for a contract still active.</summary>
        public long RpReward { get; }

        /// <summary>The Items the ladder granted at the completion, in ordinal Item-ID order; empty when none was due.</summary>
        public IReadOnlyList<IdCount> ItemsGranted { get; }

        public RunOfflineContractOutcome(RunOfflineContractKind kind, string offerId, long requiredUnits, long? deadlineUtcMs)
            : this(kind, offerId, requiredUnits, deadlineUtcMs, null, 0, 0, null)
        {
        }

        public RunOfflineContractOutcome(RunOfflineContractKind kind, string offerId, long requiredUnits, long? deadlineUtcMs,
            long? endedUtcMs, long cashReward, long rpReward, IReadOnlyList<IdCount> itemsGranted)
        {
            Kind = kind;
            OfferId = offerId;
            RequiredUnits = requiredUnits;
            DeadlineUtcMs = deadlineUtcMs;
            EndedUtcMs = endedUtcMs;
            CashReward = cashReward;
            RpReward = rpReward;
            ItemsGranted = itemsGranted ?? Array.Empty<IdCount>();
        }
    }

    /// <summary>A resource whose stock changed during the return.</summary>
    public readonly struct RunResourceDelta
    {
        public string ResourceId { get; }
        public long Before { get; }
        public long After { get; }
        public long Delta => After - Before;

        public RunResourceDelta(string resourceId, long before, long after)
        {
            ResourceId = resourceId;
            Before = before;
            After = after;
        }
    }

    /// <summary>Units of a resource (a flow of the return: produced, consumed or sold).</summary>
    public readonly struct RunResourceUnits
    {
        public string ResourceId { get; }
        public long Units { get; }

        public RunResourceUnits(string resourceId, long units)
        {
            ResourceId = resourceId;
            Units = units;
        }
    }

    /// <summary>An Item effect that ended during the absence.</summary>
    public readonly struct RunExpiredEffect
    {
        public string SourceId { get; }
        public string EffectKind { get; }
        public string TargetKey { get; }

        /// <summary>The real instant (Unix ms) at which the effect ended.</summary>
        public long EndUtcMs { get; }

        public RunExpiredEffect(string sourceId, string effectKind, string targetKey, long endUtcMs)
        {
            SourceId = sourceId;
            EffectKind = effectKind;
            TargetKey = targetKey;
            EndUtcMs = endUtcMs;
        }
    }

    /// <summary>An active machine that cannot complete a unit at the return instant, and why.</summary>
    public readonly struct RunOfflineBlockedMachine
    {
        public Guid InstanceId { get; }
        public int SlotIndex { get; }
        public string MachineId { get; }

        /// <summary><see cref="MachineBlocker.MissingInput"/> or <see cref="MachineBlocker.StorageFull"/>.</summary>
        public MachineBlocker Blocker { get; }

        public RunOfflineBlockedMachine(Guid instanceId, int slotIndex, string machineId, MachineBlocker blocker)
        {
            InstanceId = instanceId;
            SlotIndex = slotIndex;
            MachineId = machineId;
            Blocker = blocker;
        }
    }

    /// <summary>
    /// What an offline return did (contracts/run-application.md, R05; GD-2: distinct from the legacy
    /// <c>OfflineReturnSummary</c>): measured, credited and effective durations; stock and Cash deltas; sales
    /// income; expired effects; contract outcomes; and limiting conditions. It is derived once per return by
    /// <see cref="OfflineSegmenter"/> from the states before and after the advance and is never re-derived
    /// (data-model.md); it is immutable to consumers.
    ///
    /// Durations: <see cref="MeasuredMs"/> is the real absence <c>r - d</c> (0 for an invalid clock),
    /// <see cref="CreditedTicks"/> is that capped at the policy cap, <see cref="EffectiveTicks"/> is the credited
    /// time at the policy efficiency, which is exactly the time production advanced. Ticks are 100 ns.
    /// </summary>
    public sealed class RunOfflineSummary
    {
        private static readonly IReadOnlyList<RunResourceDelta> NoDeltas = Array.Empty<RunResourceDelta>();
        private static readonly IReadOnlyList<RunResourceUnits> NoUnits = Array.Empty<RunResourceUnits>();
        private static readonly IReadOnlyList<RunExpiredEffect> NoEffects = Array.Empty<RunExpiredEffect>();
        private static readonly IReadOnlyList<RunOfflineBlockedMachine> NoBlocked = Array.Empty<RunOfflineBlockedMachine>();
        private static readonly IReadOnlyList<string> NoIds = Array.Empty<string>();

        /// <summary>
        /// The static code of a return whose offline credit was forfeited because it would have left the exact bound
        /// (<c>value.limit</c> inside the simulation). Stable; UI copy keys off it.
        /// </summary>
        public const string ValueLimitCode = "offline.value-limit";

        internal RunOfflineSummary() { }

        /// <summary>
        /// <see cref="ValueLimitCode"/> when the credit was forfeited (<see cref="Outcome"/> is
        /// <see cref="RunOfflineOutcome.SimulationRejected"/> by a bound overflow), otherwise null.
        /// </summary>
        public string Code =>
            Outcome == RunOfflineOutcome.SimulationRejected && SimulationResult == RunAdvanceResult.ArithmeticOverflow
                ? ValueLimitCode
                : null;

        /// <summary>True when the whole offline credit was forfeited: the state is as before the absence.</summary>
        public bool CreditForfeited => Code != null;

        /// <summary>
        /// How many contract-scoped entries the store dropped as content drift when it read the return's source checkpoint (Save V3, T335,
        /// <see cref="RunCheckpoint.DroppedContractEntries"/>); 0 when nothing was dropped.
        /// </summary>
        public int DroppedContractEntries { get; internal set; }

        /// <summary>The informational load code <see cref="RunPersistenceReasons.ContractDropped"/> when entries were dropped, otherwise null (never a failure).</summary>
        public string ContractDropCode => DroppedContractEntries > 0 ? RunPersistenceReasons.ContractDropped : null;

        public RunOfflineOutcome Outcome { get; internal set; }

        /// <summary>Set when <see cref="Outcome"/> is <see cref="RunOfflineOutcome.SimulationRejected"/>.</summary>
        public RunAdvanceResult SimulationResult { get; internal set; }

        /// <summary>True for a departure and return that form a valid interval (<c>r &gt;= d</c>, both representable).</summary>
        public bool HasValidInterval => Outcome == RunOfflineOutcome.Applied;

        /// <summary>
        /// True when a later retry can succeed: a missing, backward or unrepresentable clock. False for
        /// <see cref="RunOfflineOutcome.Applied"/> and <see cref="RunOfflineOutcome.SimulationRejected"/>.
        /// </summary>
        public bool IsRetryable =>
            Outcome == RunOfflineOutcome.MissingClock || Outcome == RunOfflineOutcome.BackwardClock ||
            Outcome == RunOfflineOutcome.ClockUnrepresentable;

        /// <summary>The policy's credited cap in ticks (8 h at revision 1), so the UI need not hard-code it.</summary>
        public long CapTicks { get; internal set; }

        /// <summary>Policy efficiency numerator (4 at revision 1).</summary>
        public long EfficiencyNumerator { get; internal set; }

        /// <summary>Policy efficiency denominator (5 at revision 1; 4/5 is 80 percent).</summary>
        public long EfficiencyDenominator { get; internal set; }

        public long DepartureUtcMs { get; internal set; }
        public long? ReturnUtcMs { get; internal set; }

        /// <summary>The real absence <c>r - d</c> in milliseconds.</summary>
        public long MeasuredMs { get; internal set; }

        /// <summary><c>min(max(r - d, 0), cap)</c> in ticks.</summary>
        public long CreditedTicks { get; internal set; }

        /// <summary><c>E(r)</c> in ticks: the time production advanced.</summary>
        public long EffectiveTicks { get; internal set; }

        /// <summary>Limiting condition: the absence exceeded the credited cap, so the excess earned nothing.</summary>
        public bool CapReached { get; internal set; }

        public long CashBefore { get; internal set; }
        public long CashAfter { get; internal set; }
        public long CashDelta => CashAfter - CashBefore;

        /// <summary>
        /// Cash earned by auto-sales during the return. Auto-sale and the reward of an auto-delivery (T326) or a production completion (T327)
        /// are the only sources of Cash offline, so this equals <see cref="CashDelta"/> without that reward.
        /// </summary>
        public long SalesIncome { get; internal set; }

        /// <summary>Resources whose stock changed, in inventory order.</summary>
        public IReadOnlyList<RunResourceDelta> StockDeltas { get; internal set; } = NoDeltas;

        /// <summary>Units added by machine completions, per resource, nonzero only.</summary>
        public IReadOnlyList<RunResourceUnits> ProducedUnits { get; internal set; } = NoUnits;

        /// <summary>Units taken by machine completions, per resource, nonzero only.</summary>
        public IReadOnlyList<RunResourceUnits> ConsumedUnits { get; internal set; } = NoUnits;

        /// <summary>Units removed by auto-sales, per resource, nonzero only.</summary>
        public IReadOnlyList<RunResourceUnits> SoldUnits { get; internal set; } = NoUnits;

        public long Completions { get; internal set; }

        /// <summary>Limiting condition: completions that exchanged fewer units than the batch factor (inputs or store, OD-9).</summary>
        public long PartialBatchCompletions { get; internal set; }

        /// <summary>Timestamps the simulator processed (diagnostic).</summary>
        public long ProcessedTimestamps { get; internal set; }

        /// <summary>Item effects that ended during the absence, in the state's effect order.</summary>
        public IReadOnlyList<RunExpiredEffect> ExpiredEffects { get; internal set; } = NoEffects;

        /// <summary>The Overdrive active phase ended during the absence (it was still set at departure and its end lies at or before the return).</summary>
        public bool OverdriveActiveEnded { get; internal set; }

        /// <summary>The Overdrive cooldown ended during the absence; Overdrive is ready again.</summary>
        public bool OverdriveCooldownEnded { get; internal set; }

        public RunOfflineContractOutcome Contract { get; internal set; }

        /// <summary>The active contract was auto-delivered during the absence (the delivery notice; reward totals below).</summary>
        public bool AutoDelivered => Contract.Kind == RunOfflineContractKind.AutoDelivered;

        /// <summary>The active production objective was completed during the absence (reward totals below).</summary>
        public bool ProduceCompleted => Contract.Kind == RunOfflineContractKind.ProduceCompleted;

        /// <summary>The active timed contract expired during the absence (no reward).</summary>
        public bool Expired => Contract.Kind == RunOfflineContractKind.Expired;

        /// <summary>The Cash the contract completion of this return granted (an auto-delivery or production reward); it is part of <see cref="CashDelta"/> and not of <see cref="SalesIncome"/>.</summary>
        public long ContractRewardCash => Contract.CashReward;

        /// <summary>The RP the contract completion of this return granted.</summary>
        public long ContractRewardRp => Contract.RpReward;

        /// <summary>The Items the contract completion of this return granted.</summary>
        public IReadOnlyList<IdCount> ContractRewardItems => Contract.ItemsGranted;

        /// <summary>IDs newly unlocked while the factory produced (resource-produced conditions, T140).</summary>
        public IReadOnlyList<string> NewUnlocks { get; internal set; } = NoIds;

        /// <summary>
        /// Limiting condition: active machines that cannot complete a unit at the return instant, because an input
        /// is short or the store is full. It describes the state at return, not everything that happened during the
        /// absence.
        /// </summary>
        public IReadOnlyList<RunOfflineBlockedMachine> BlockedMachinesAtReturn { get; internal set; } = NoBlocked;
    }
}
