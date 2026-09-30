using System;
using System.Collections.Generic;
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

        /// <summary>The simulator rejected the advance (see <see cref="RunOfflineSummary.SimulationResult"/>). The run state is unchanged.</summary>
        SimulationRejected = 4,
    }

    /// <summary>What happened to the active timed contract during the absence.</summary>
    public enum RunOfflineContractKind
    {
        /// <summary>No contract was active at departure.</summary>
        None = 0,

        /// <summary>The contract is still active at return (its deadline lies after the return, or it has none).</summary>
        StillActive = 1,

        /// <summary>The contract's deadline passed during the absence and it expired (no reward, count unchanged).</summary>
        Expired = 2,
    }

    /// <summary>Contract outcome of an offline return. Deliveries are commands, so offline the only change is expiry.</summary>
    public readonly struct RunOfflineContractOutcome
    {
        public RunOfflineContractKind Kind { get; }
        public string OfferId { get; }
        public long RequiredUnits { get; }

        /// <summary>The inclusive deadline (Unix ms) of the contract, when it had one.</summary>
        public long? DeadlineUtcMs { get; }

        public RunOfflineContractOutcome(RunOfflineContractKind kind, string offerId, long requiredUnits, long? deadlineUtcMs)
        {
            Kind = kind;
            OfferId = offerId;
            RequiredUnits = requiredUnits;
            DeadlineUtcMs = deadlineUtcMs;
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

        internal RunOfflineSummary() { }

        public RunOfflineOutcome Outcome { get; internal set; }

        /// <summary>Set when <see cref="Outcome"/> is <see cref="RunOfflineOutcome.SimulationRejected"/>.</summary>
        public RunAdvanceResult SimulationResult { get; internal set; }

        /// <summary>True for a departure and return that form a valid interval (<c>r &gt;= d</c>, both representable).</summary>
        public bool HasValidInterval => Outcome == RunOfflineOutcome.Applied;

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
        /// Cash earned by auto-sales during the return. Offline auto-sale is the only source of Cash (deliveries and
        /// purchases are commands), so this equals <see cref="CashDelta"/>.
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
