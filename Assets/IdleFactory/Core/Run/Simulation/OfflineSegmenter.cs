using System;
using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;

namespace IdleFactory.Run.Simulation
{
    /// <summary>
    /// Offline return of a run (research R05, FR-025, FR-026, GD-3). For departure <c>d</c>, return <c>r</c> and the
    /// catalog's offline policy (revision 1: 4/5 efficiency, eight-hour cap) the effective simulation time of a real
    /// instant <c>x</c> is
    ///
    /// <c>E(x) = floor(min(max(x - d, 0), cap) x 4 / 5)</c>, in ticks of 100 ns (a real millisecond is 10 000 ticks).
    ///
    /// Every real boundary <c>b</c> with <c>b &lt;= r</c> of the run's timers (Item effect ends, the Overdrive end
    /// and cooldown end, a contract's <c>deadline + 1 ms</c>) becomes a <see cref="BoundaryEvent"/> at
    /// <c>E(b)</c> with its real instant as <see cref="BoundaryEvent.UtcMs"/>; a boundary after <c>d + cap</c> maps
    /// to <c>E(cap)</c>, the end of production, because it has no production effect; a boundary already due at the
    /// departure (<c>b &lt;= d</c>, for example one that a load carried over) maps to 0 and is processed at the
    /// first timestamp. Boundaries that <c>E</c> floors onto one instant are processed together, in
    /// <c>(kind, id)</c> order, whatever their real order. Production advances by <c>E(r)</c> in total in one call
    /// of <see cref="RunSimulator.Advance"/>, the same entry point the online session uses.
    ///
    /// <c>E</c> telescopes, so the total effective time is the same whichever boundaries interrupt it (R05).
    ///
    /// The mapping handed to the simulator (<see cref="MappingFor"/>, T325) has the departure <c>d</c> as its origin: it
    /// belongs to simulation offset 0 and only decides whether Overdrive is active at the start (<c>d &lt; ActiveUntilUtcMs</c>);
    /// its ratio is the inverse of the efficiency, so <c>RealUtc(E(x)) = x</c> inside the cap. Every boundary before it is
    /// processed at offset 0, every other boundary lies after it, so it is the correct instant of the
    /// <see cref="RunSimulator.Advance"/> contract ("between the last boundary already processed and the next one").
    ///
    /// An invalid clock (missing, before the departure, or outside the exact bound) grants zero credit and leaves the
    /// run state untouched: nothing expires and nothing is un-expired; the caller blocks the return and retries
    /// (Feature 001 behaviour). A return at exactly the departure is valid with zero credit: boundaries already due
    /// are processed, nothing produces.
    ///
    /// An instance keeps a simulator and scratch buffers and is not thread-safe.
    /// </summary>
    public sealed class OfflineSegmenter
    {
        public const long TicksPerMs = 10_000;

        private const string OverdriveEventId = "overdrive";

        private readonly RunCatalog _catalog;
        private readonly RunOfflinePolicy _policy;
        private readonly RunSimulator _simulator;
        private readonly RunModifiers _modifiers;
        private readonly List<BoundaryEvent> _events = new List<BoundaryEvent>();

        public OfflineSegmenter(RunCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _policy = catalog.OfflinePolicy;
            _simulator = new RunSimulator(catalog);
            _modifiers = new RunModifiers(catalog);
        }

        /// <summary>The simulator used for the advance (its <c>Last*</c> counters describe the last return).</summary>
        public RunSimulator Simulator => _simulator;

        // ---------------------------------------------------------------------------------------------------
        // The mapping E
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// The credited time in ticks: <c>min(max(x - d, 0), cap)</c> for the real instant <paramref name="utcMs"/>
        /// and departure <paramref name="departureUtcMs"/> (Unix ms). Never overflows: a distance that exceeds the
        /// cap is answered without multiplying.
        /// </summary>
        public static long CreditedTicks(RunOfflinePolicy policy, long departureUtcMs, long utcMs)
        {
            if (utcMs <= departureUtcMs) return 0;
            long capMs = policy.MaxCreditedTicks / TicksPerMs;
            // The subtraction cannot overflow for two instants inside the exact bound; a saturated one is beyond the cap.
            if (!CheckedMath.TrySubtract(utcMs, departureUtcMs, out long distanceMs) || distanceMs > capMs) return policy.MaxCreditedTicks;
            long ticks = distanceMs * TicksPerMs;
            return ticks > policy.MaxCreditedTicks ? policy.MaxCreditedTicks : ticks;
        }

        /// <summary><c>E(x)</c> in ticks for the real instant <paramref name="utcMs"/> (see the type remarks).</summary>
        public static long EffectiveTicks(RunOfflinePolicy policy, long departureUtcMs, long utcMs)
        {
            long credited = CreditedTicks(policy, departureUtcMs, utcMs);
            // credited <= cap (2.88e11) and the policy is 4/5, so the product stays far inside the range.
            MulDiv.MulDivFloor(credited, policy.EfficiencyNumerator, policy.EfficiencyDenominator, out long effective);
            return effective;
        }

        public long CreditedTicks(long departureUtcMs, long utcMs) => CreditedTicks(_policy, departureUtcMs, utcMs);

        /// <summary>
        /// The tick-to-UTC mapping of the offline segment that starts at <paramref name="departureUtcMs"/> (T325,
        /// F5-R05): origin the departure, phase 0, real-per-sim the inverse of the policy's efficiency (<c>5/4</c>). It is
        /// the inverse of <see cref="EffectiveTicks(long, long)"/>: for a real instant <c>x</c> inside the cap,
        /// <c>RealUtc(E(x)) = x</c>, so the real stamps of an offline return agree with the boundary instants
        /// <see cref="MapBoundaries"/> supplies. Built once per return and handed to <see cref="RunSimulator"/>.
        /// </summary>
        public TimelineMapping MappingFor(long departureUtcMs) => TimelineMapping.Offline(_policy, departureUtcMs);

        public long EffectiveTicks(long departureUtcMs, long utcMs) => EffectiveTicks(_policy, departureUtcMs, utcMs);

        // ---------------------------------------------------------------------------------------------------
        // Boundary events
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// The boundary events of the state's timers for an absence from <paramref name="departureUtcMs"/> to
        /// <paramref name="returnUtcMs"/>: every boundary <c>b &lt;= r</c> at <c>E(b)</c> (0 when <c>b &lt;= d</c>),
        /// ordered by <c>(SimTicks, Kind, Id)</c>. The list is reused by the next call.
        /// </summary>
        public IReadOnlyList<BoundaryEvent> MapBoundaries(RunState state, long departureUtcMs, long returnUtcMs)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            _events.Clear();
            IReadOnlyList<ActiveEffect> effects = state.Effects.Effects;
            for (int i = 0; i < effects.Count; i++)
                Add(BoundaryKind.EffectExpiry, effects[i].SourceId, effects[i].EndUtcMs, departureUtcMs, returnUtcMs);

            OverdriveState overdrive = state.Overdrive;
            if (overdrive.ActiveUntilUtcMs.HasValue)
            {
                Add(BoundaryKind.OverdriveEnd, OverdriveEventId, overdrive.ActiveUntilUtcMs.Value, departureUtcMs, returnUtcMs);
                Add(BoundaryKind.OverdriveCooldownEnd, OverdriveEventId, overdrive.CooldownUntilUtcMs.Value, departureUtcMs, returnUtcMs);
            }

            ActiveContract contract = state.Contracts.Active;
            if (contract != null && contract.DeadlineUtcMs.HasValue &&
                CheckedMath.TryAdd(contract.DeadlineUtcMs.Value, 1, out long expiry))
                Add(BoundaryKind.ContractExpiry, contract.OfferId, expiry, departureUtcMs, returnUtcMs);
            return _events;
        }

        private void Add(BoundaryKind kind, string id, long boundaryUtcMs, long departureUtcMs, long returnUtcMs)
        {
            if (boundaryUtcMs > returnUtcMs) return;
            long simTicks = boundaryUtcMs <= departureUtcMs ? 0 : EffectiveTicks(_policy, departureUtcMs, boundaryUtcMs);
            var e = new BoundaryEvent(simTicks, kind, id, boundaryUtcMs);
            // Insertion into (SimTicks, Kind, Id) order; ties (equal keys) are kept in insertion order after the real
            // instant, so the list is a pure function of the state.
            int at = _events.Count;
            _events.Add(e);
            while (at > 0 && Precedes(e, _events[at - 1]))
            {
                _events[at] = _events[at - 1];
                at--;
            }
            _events[at] = e;
        }

        private static bool Precedes(in BoundaryEvent a, in BoundaryEvent b)
        {
            int c = BoundaryEvent.Compare(a, b);
            return c != 0 ? c < 0 : a.UtcMs < b.UtcMs;
        }

        // ---------------------------------------------------------------------------------------------------
        // Return
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Applies an offline return to <paramref name="state"/> (and reads Industry levels from
        /// <paramref name="permanent"/>, which may be null when the catalog has none): the mapped boundaries and
        /// <c>E(r)</c> ticks go to one <see cref="RunSimulator.Advance"/> call. Returns the summary. When the clock is
        /// invalid or the simulator rejects the advance, the state is unchanged and the summary says why.
        /// </summary>
        /// <param name="departureUtcMs">The persisted departure instant (Unix ms).</param>
        /// <param name="returnUtcMs">The captured return instant (Unix ms), or null when the clock is missing.</param>
        public RunOfflineSummary Reconcile(RunState state, PermanentState permanent, long departureUtcMs, long? returnUtcMs)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var summary = new RunOfflineSummary
            {
                DepartureUtcMs = departureUtcMs,
                ReturnUtcMs = returnUtcMs,
                CapTicks = _policy.MaxCreditedTicks,
                EfficiencyNumerator = _policy.EfficiencyNumerator,
                EfficiencyDenominator = _policy.EfficiencyDenominator,
                CashBefore = state.Cash,
                CashAfter = state.Cash,
                Contract = new RunOfflineContractOutcome(RunOfflineContractKind.None, null, 0, null),
            };
            if (!returnUtcMs.HasValue)
            {
                summary.Outcome = RunOfflineOutcome.MissingClock;
                return summary;
            }
            long r = returnUtcMs.Value;
            if (!ExactBound.Contains(departureUtcMs) || !ExactBound.Contains(r))
            {
                summary.Outcome = RunOfflineOutcome.ClockUnrepresentable;
                return summary;
            }
            if (r < departureUtcMs)
            {
                // A backward clock: zero credit and no processing of any kind. The timeline never moves back, so
                // nothing that has expired is un-expired, and nothing that is due is processed at an instant that
                // is earlier than the departure.
                summary.Outcome = RunOfflineOutcome.BackwardClock;
                return summary;
            }

            long credited = CreditedTicks(_policy, departureUtcMs, r);
            long effective = EffectiveTicks(_policy, departureUtcMs, r);
            summary.MeasuredMs = r - departureUtcMs;
            summary.CreditedTicks = credited;
            summary.EffectiveTicks = effective;
            summary.CapReached = credited < CheckedTicks(summary.MeasuredMs);

            // Before-state facts.
            int resourceCount = state.Inventory.Count;
            var stockIds = new string[resourceCount];
            var stockBefore = new long[resourceCount];
            for (int i = 0; i < resourceCount; i++)
            {
                stockIds[i] = state.Inventory.IdAt(i);
                stockBefore[i] = state.Inventory.UnitsAt(i);
            }
            var effectsBefore = new ActiveEffect[state.Effects.Count];
            for (int i = 0; i < effectsBefore.Length; i++) effectsBefore[i] = state.Effects.Effects[i];
            long? activeUntil = state.Overdrive.ActiveUntilUtcMs;
            long? cooldownUntil = state.Overdrive.CooldownUntilUtcMs;
            ActiveContract contractBefore = state.Contracts.Active;
            var unlockedBefore = new HashSet<string>(state.Unlocked, StringComparer.Ordinal);

            IReadOnlyList<BoundaryEvent> events = MapBoundaries(state, departureUtcMs, r);
            RunAdvanceResult result = _simulator.Advance(state, permanent, effective, MappingFor(departureUtcMs), events);
            if (result != RunAdvanceResult.Ok)
            {
                summary.Outcome = RunOfflineOutcome.SimulationRejected;
                summary.SimulationResult = result;
                summary.MeasuredMs = 0;
                summary.CreditedTicks = 0;
                summary.EffectiveTicks = 0;
                summary.CapReached = false;
                return summary;
            }

            summary.Outcome = RunOfflineOutcome.Applied;
            summary.CashAfter = state.Cash;
            summary.SalesIncome = state.Cash - summary.CashBefore;
            summary.Completions = _simulator.LastCompletions;
            summary.PartialBatchCompletions = _simulator.LastPartialBatches;
            summary.ProcessedTimestamps = _simulator.LastTimestamps;

            // Stock and flows, in the inventory order of the returned state (an unlock may have allowed a resource).
            var deltas = new List<RunResourceDelta>();
            var produced = new List<RunResourceUnits>();
            var consumed = new List<RunResourceUnits>();
            var sold = new List<RunResourceUnits>();
            GlobalInventory inventory = state.Inventory;
            for (int i = 0; i < inventory.Count; i++)
            {
                string id = inventory.IdAt(i);
                long before = 0;
                for (int b = 0; b < stockIds.Length; b++)
                    if (string.Equals(stockIds[b], id, StringComparison.Ordinal)) before = stockBefore[b];
                long after = inventory.UnitsAt(i);
                if (after != before) deltas.Add(new RunResourceDelta(id, before, after));
                long p = _simulator.LastProducedUnits(id);
                long c = _simulator.LastConsumedUnits(id);
                long s = _simulator.LastSoldUnits(id);
                if (p != 0) produced.Add(new RunResourceUnits(id, p));
                if (c != 0) consumed.Add(new RunResourceUnits(id, c));
                if (s != 0) sold.Add(new RunResourceUnits(id, s));
            }
            summary.StockDeltas = deltas.ToArray();
            summary.ProducedUnits = produced.ToArray();
            summary.ConsumedUnits = consumed.ToArray();
            summary.SoldUnits = sold.ToArray();

            // Expired effects: the effect objects that are no longer in the set.
            var expired = new List<RunExpiredEffect>();
            for (int i = 0; i < effectsBefore.Length; i++)
            {
                bool kept = false;
                for (int k = 0; k < state.Effects.Count && !kept; k++)
                    kept = ReferenceEquals(state.Effects.Effects[k], effectsBefore[i]);
                // An effect that ended at or before the departure was already due then (a load carried it over): it is
                // still processed at offset 0, but the return did not end it, so the summary does not claim it.
                if (!kept && effectsBefore[i].EndUtcMs > departureUtcMs)
                    expired.Add(new RunExpiredEffect(effectsBefore[i].SourceId, effectsBefore[i].ConflictKey.EffectKind,
                        effectsBefore[i].ConflictKey.TargetKey, effectsBefore[i].EndUtcMs));
            }
            summary.ExpiredEffects = expired.ToArray();

            // Only boundaries inside (d, r] happened during the absence; earlier ones are processed but not claimed.
            summary.OverdriveActiveEnded = activeUntil.HasValue && activeUntil.Value > departureUtcMs && activeUntil.Value <= r;
            summary.OverdriveCooldownEnded = cooldownUntil.HasValue && cooldownUntil.Value > departureUtcMs &&
                !state.Overdrive.CooldownUntilUtcMs.HasValue;

            // Contract outcome: the active contract before against after (ContractBoard.LastExpired is sticky and
            // says nothing about this return).
            if (contractBefore != null)
            {
                bool gone = state.Contracts.Active == null;
                summary.Contract = new RunOfflineContractOutcome(
                    gone ? RunOfflineContractKind.Expired : RunOfflineContractKind.StillActive,
                    contractBefore.OfferId, contractBefore.RequiredUnits, contractBefore.DeadlineUtcMs);
            }

            var unlocks = new List<string>();
            for (int i = 0; i < state.Unlocked.Count; i++)
                if (!unlockedBefore.Contains(state.Unlocked[i])) unlocks.Add(state.Unlocked[i]);
            summary.NewUnlocks = unlocks.ToArray();

            summary.BlockedMachinesAtReturn = BlockedMachines(state, permanent, r);
            return summary;
        }

        private long CheckedTicks(long ms) =>
            ms > _policy.MaxCreditedTicks / TicksPerMs ? long.MaxValue : ms * TicksPerMs;

        /// <summary>
        /// Active machines that cannot complete one recipe unit at the return instant (the run predicate of R01
        /// step 5, as the machine read model of <see cref="RunSession"/> derives it): a missing input or a locked
        /// resource is <see cref="MachineBlocker.MissingInput"/>, an input-complete machine whose net exchange does
        /// not fit is <see cref="MachineBlocker.StorageFull"/>. A machine whose speed cannot be derived is skipped.
        /// </summary>
        private RunOfflineBlockedMachine[] BlockedMachines(RunState state, PermanentState permanent, long atUtcMs)
        {
            var blocked = new List<RunOfflineBlockedMachine>();
            bool overdrive = state.Overdrive.IsActive(atUtcMs);
            GlobalInventory stock = state.Inventory;
            IReadOnlyList<MachineInstance> machines = state.Machines;
            for (int i = 0; i < machines.Count; i++)
            {
                MachineInstance m = machines[i];
                if (!m.IsActive) continue;
                if (!_catalog.Machines.TryGetValue(m.MachineId, out MachineDef def) ||
                    !_catalog.Recipes.TryGetValue(def.RecipeId, out MachineRecipe recipe) ||
                    !_modifiers.TryMachineSpeed(state, permanent, m, overdrive, null, out long speed) ||
                    !SpeedComposer.TryBatch(recipe.DurationTicks, speed, _catalog.RunStart.MinCycleTicks, out long batch, out _) ||
                    !_modifiers.TryOutputFactor(state, m, null, out long factor))
                    continue;
                bool allowed = true;
                for (int r = 0; r < recipe.Inputs.Count && allowed; r++) allowed = stock.IsAllowed(recipe.Inputs[r].ResourceId);
                for (int r = 0; r < recipe.Outputs.Count && allowed; r++) allowed = stock.IsAllowed(recipe.Outputs[r].ResourceId);
                long units = 0;
                if (allowed && !SpeedComposer.TryCompletedUnits(batch, recipe, factor, stock, out units)) continue;
                if (units >= 1) continue;
                bool missing = !allowed;
                for (int r = 0; r < recipe.Inputs.Count && !missing; r++)
                    missing = stock.Units(recipe.Inputs[r].ResourceId) < recipe.Inputs[r].Amount;
                blocked.Add(new RunOfflineBlockedMachine(m.InstanceId, m.SlotIndex, m.MachineId,
                    missing ? MachineBlocker.MissingInput : MachineBlocker.StorageFull));
            }
            return blocked.ToArray();
        }
    }
}
