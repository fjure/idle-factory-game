using System;
using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Run.Application
{
    /// <summary>
    /// The live run (contracts/run-application.md, research R01, R05, R14): it owns the run state, the simulator,
    /// the run timeline and the change revisions.
    ///
    /// Timeline (R05): <c>timelineUtc = anchorUtc + activeTicksSinceAnchor</c>, with the anchor in Unix
    /// milliseconds and the active time in ticks of 100 ns (10 000 ticks per millisecond). The timeline instant is
    /// the whole-millisecond part; the sub-millisecond remainder stays in <see cref="ActiveTicksSinceAnchor"/>, so a
    /// split of one advance into several yields the same timeline. The timeline never moves backwards.
    ///
    /// Boundaries: effect ends, Overdrive end, Overdrive cooldown end and a contract's <c>deadline + 1 ms</c> are
    /// derived from the state's own timers and handed to <see cref="RunSimulator.Advance"/> as simulation-time events
    /// (offset from the timeline instant); a boundary already due is emitted at offset 0.
    ///
    /// Revisions (R14): <see cref="StructureRevision"/> changes when a structural fact changes (machines, levels,
    /// unlocks, settings, contracts, effects, Overdrive status) and <see cref="QuantityRevision"/> when a quantity
    /// changes (stock, Cash, RP, progress). Quantities are compared exactly against a baseline taken before the
    /// operation; a rejected operation changes neither.
    ///
    /// Commands (T137) wrap their mutation in <see cref="BeginCommand"/> and <see cref="EndCommand{T}"/>; the
    /// latter settles (steps 3-5 of R01: auto-sales, then the run predicate at the command timestamp). Not
    /// thread-safe; a warmed <see cref="Advance"/> and the hot reads allocate nothing.
    /// </summary>
    public sealed class RunSession
    {
        /// <summary>Simulation and timeline ticks (100 ns) per millisecond.</summary>
        public const long TicksPerMs = 10_000;

        private const string OverdriveEventId = "overdrive";

        private readonly RunCatalog _catalog;
        private readonly RunSimulator _simulator;
        private readonly RunModifiers _modifiers;
        private readonly List<BoundaryEvent> _events = new List<BoundaryEvent>();
        private readonly Dictionary<string, int> _typeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly string[] _typeIds;

        // Change detection: an exact quantity baseline and a structural token.
        private long[] _quantityBase = new long[0];
        private long[] _quantityNow = new long[0];
        private int _quantityBaseLength;
        private int _tokEffects;
        private int _tokUnlocked;
        private bool _tokOverdriveActive;
        private long _tokOverdriveUntil;
        private long _tokOverdriveCooldown;
        private ActiveContract _tokContract;
        private long _tokContractVersion;
        private bool _baselineTaken;

        // Machine read model, rebuilt lazily after every operation.
        private MachineStateView[] _views = new MachineStateView[0];
        private int _viewCount;
        private bool _viewsDirty = true;

        private bool _lifecycleBlocked;

        // Last completion per machine (RatesSnapshot, OD-9). Derived, never persisted: a machine that has not
        // completed since the session started has none. Preallocated for the largest factory, so recording a
        // completion allocates nothing unless the factory outgrows it.
        private Guid[] _completionId = new Guid[16];
        private long[] _completionUnits = new long[16];
        private long[] _completionBatch = new long[16];
        private int[] _completionLimit = new int[16];
        private int _completionCount;

        private readonly GuardedRunEventSink _logSink;

        public RunSession(RunCatalog catalog, RunState state, PermanentState permanent, long anchorUtcMs)
            : this(catalog, state, permanent, anchorUtcMs, null)
        {
        }

        /// <param name="eventSink">
        /// The optional session-log sink (T307). It is fixed here and never replaced; null means the no-op sink.
        /// Raise sites use <see cref="Events"/>, which is never null and never throws.
        /// </param>
        public RunSession(RunCatalog catalog, RunState state, PermanentState permanent, long anchorUtcMs, IRunEventSink eventSink)
        {
            _logSink = new GuardedRunEventSink(eventSink);
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            State = state ?? throw new ArgumentNullException(nameof(state));
            if (!ReferenceEquals(state.Catalog, catalog)) throw new ArgumentException("The state belongs to another catalog.", nameof(state));
            if (!ExactBound.Contains(anchorUtcMs)) throw new ArgumentOutOfRangeException(nameof(anchorUtcMs));
            Permanent = permanent;
            AnchorUtcMs = anchorUtcMs;
            _simulator = new RunSimulator(catalog);
            _modifiers = new RunModifiers(catalog);
            _typeIds = new string[catalog.Machines.Count];
            int n = 0;
            foreach (string id in catalog.Machines.Keys) _typeIds[n++] = id;
            Array.Sort(_typeIds, StringComparer.Ordinal);
            for (int i = 0; i < _typeIds.Length; i++) _typeIndex[_typeIds[i]] = i;
        }

        public RunCatalog Catalog => _catalog;

        /// <summary>
        /// The session-log port, guarded: raise events here without a null check. A sink that throws is ignored
        /// (the event is counted in <see cref="DroppedEvents"/>), so the log can never alter simulation, save or timing.
        /// </summary>
        public IRunEventSink Events => _logSink;

        /// <summary>The number of events a failing sink lost (the Lifecycle record's <c>droppedEvents</c>).</summary>
        public int DroppedEvents => _logSink.DroppedEvents;

        /// <summary>The live run state. Mutate it only through commands that end in <see cref="EndCommand{T}"/>.</summary>
        public RunState State { get; private set; }

        /// <summary>The permanent (cross-run) state; may be null when the catalog has none.</summary>
        public PermanentState Permanent { get; private set; }

        // The live mapping at the anchor (T325): origin the anchor, phase 0, 1/1. The active ticks since the anchor are
        // its simulated ticks, so the session timeline is this mapping evaluated at ActiveTicksSinceAnchor.
        private TimelineMapping _anchorMapping;

        public long AnchorUtcMs
        {
            get => _anchorMapping.OriginUtcMs;
            private set => _anchorMapping = TimelineMapping.Live(value);
        }

        public long ActiveTicksSinceAnchor { get; private set; }

        /// <summary>
        /// The run timeline instant in Unix milliseconds: <c>anchor + floor(activeTicks / 10 000)</c>, the stamp of the live
        /// <see cref="TimelineMapping"/> (T325, one floor rule shared with the offline return and the simulator). It is
        /// evaluated per read, never per completion (PF-6); the anchor and the active ticks are bounded by
        /// <see cref="Advance"/> and the anchor setters, so the stamp is always representable.
        /// </summary>
        public long TimelineUtcMs => _anchorMapping.RealUtcMs(ActiveTicksSinceAnchor);

        /// <summary>
        /// The live mapping at the current timeline instant (origin the whole millisecond of <see cref="TimelineUtcMs"/>,
        /// sub-millisecond phase carried forward from <see cref="ActiveTicksSinceAnchor"/>). Simulated ticks of an advance
        /// count from here, so any partition of live time yields the same stamps. Built once per advance.
        /// </summary>
        public TimelineMapping Timeline
        {
            get
            {
                TimelineMapping.TryLiveAt(AnchorUtcMs, ActiveTicksSinceAnchor, out TimelineMapping mapping);
                return mapping;
            }
        }

        public long StructureRevision { get; private set; }

        public long QuantityRevision { get; private set; }

        /// <summary>
        /// True while a lifecycle transition holds commands and advancing back (lifecycle.blocked): the flag set by
        /// <see cref="SetLifecycleBlocked"/> or a pending prestige transition (T102), which must not let the old run
        /// change under its candidate.
        /// </summary>
        public bool IsLifecycleBlocked => _lifecycleBlocked || PendingTransition != null;

        /// <summary>The prestige candidate awaiting promotion, or null (R09, QA-4). See <see cref="PrestigeCandidate"/>.</summary>
        public PrestigeCandidate PendingTransition { get; private set; }

        internal void SetPendingTransition(PrestigeCandidate candidate) => PendingTransition = candidate;

        /// <summary>
        /// The durable coordinator of the pending transition, set by <see cref="RunPersistence"/> when it attaches to the
        /// session (T105). With it, <see cref="RunCommands.PromotePrestige"/> and <see cref="RunCommands.CancelPendingTransition"/>
        /// go through the durable protocol (write, read back, verify, publish), so the UI cannot publish a prestige that was
        /// never written. Without it (a session without persistence, as in domain tests) they act in memory only.
        /// </summary>
        internal IPendingTransitionCoordinator TransitionCoordinator { get; set; }

        /// <summary>
        /// Publishes a pending candidate as the live run: the state and permanent state are replaced, the timeline
        /// is re-anchored at the confirmation instant with the active time back at 0 (the timeline never moves back),
        /// per-run derived data (last completions) is forgotten and both revisions move. The candidate is already
        /// settled, so no simulation runs here. Clears the pending transition.
        /// </summary>
        internal void PublishPendingTransition()
        {
            PrestigeCandidate candidate = PendingTransition;
            State = candidate.Run;
            Permanent = candidate.Permanent;
            long timeline = TimelineUtcMs;
            AnchorUtcMs = candidate.ConfirmedUtcMs > timeline ? candidate.ConfirmedUtcMs : timeline;
            ActiveTicksSinceAnchor = 0;
            _completionCount = 0;
            _baselineTaken = false;
            PendingTransition = null;
            QuantityRevision++;
            StructureRevision++;
            _viewsDirty = true;
        }

        /// <summary>
        /// Installs a durable checkpoint's state as the live run (T138, persistence load and recovery): the state and
        /// permanent state are replaced, the timeline is re-anchored at <paramref name="anchorUtcMs"/> with the active
        /// time back at 0, per-run derived data is forgotten, a pending prestige candidate is dropped (the durable
        /// generation is the authority, R09) and both revisions move. A load may move the timeline back, because the
        /// state it installs is older. Nothing is simulated here; the caller settles afterwards (load rule 2).
        /// </summary>
        internal void ReplaceLoaded(RunState state, PermanentState permanent, long anchorUtcMs)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!ReferenceEquals(state.Catalog, _catalog)) throw new ArgumentException("The state belongs to another catalog.", nameof(state));
            if (!ExactBound.Contains(anchorUtcMs)) throw new ArgumentOutOfRangeException(nameof(anchorUtcMs));
            State = state;
            Permanent = permanent;
            AnchorUtcMs = anchorUtcMs;
            ActiveTicksSinceAnchor = 0;
            _completionCount = 0;
            _baselineTaken = false;
            PendingTransition = null;
            QuantityRevision++;
            StructureRevision++;
            _viewsDirty = true;
        }

        public void SetLifecycleBlocked(bool blocked) => _lifecycleBlocked = blocked;

        /// <summary>
        /// Copies the last completions an offline return recorded (<see cref="OfflineSegmenter"/> runs its own simulator,
        /// T103 handoff 5) for machines of the live run that have no completion recorded yet, so the rates read model
        /// shows them after a return. Derived data, never persisted.
        /// </summary>
        internal void AdoptLastCompletions(IRunReturnSummarySource source)
        {
            if (source == null) return;
            IReadOnlyList<MachineInstance> machines = State.Machines;
            for (int m = 0; m < machines.Count; m++)
            {
                Guid id = machines[m].InstanceId;
                bool known = false;
                for (int i = 0; i < _completionCount && !known; i++) known = _completionId[i] == id;
                if (known || !source.TryGetLastCompletion(id, out long units, out long batch, out int limit)) continue;
                StoreCompletion(id, units, batch, limit);
            }
        }

        public int MachineTypeCount => _typeIds.Length;

        /// <summary>The machine ID at a <see cref="MachineStateView.MachineTypeIndex"/>.</summary>
        public string MachineTypeId(int index) => _typeIds[index];

        // ---------------------------------------------------------------------------------------------------
        // Timeline
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Re-binds the anchor at a return or a load (R05): a later instant becomes the timeline and the active
        /// time restarts at 0. An instant at or before the current timeline (a backward clock) keeps the timeline,
        /// which never moves back. Returns the timeline instant; rejects an instant outside the exact bound.
        /// </summary>
        public RunCommandResult<long> RebindAnchor(long anchorUtcMs)
        {
            if (!ExactBound.Contains(anchorUtcMs)) return RunCommandResult.Reject<long>(RunReasons.ValueLimit);
            if (anchorUtcMs > TimelineUtcMs)
            {
                AnchorUtcMs = anchorUtcMs;
                ActiveTicksSinceAnchor = 0;
                _viewsDirty = true;
            }
            return RunCommandResult.Success(TimelineUtcMs);
        }

        /// <summary>
        /// Advances the run by <paramref name="activeTicks"/> ticks of active time. Returns the new timeline
        /// instant. Rejected, with nothing changed, for negative ticks (command.invalid), a blocked lifecycle
        /// (lifecycle.blocked) and any checked overflow of the timeline or the simulation (value.limit).
        /// </summary>
        public RunCommandResult<long> Advance(long activeTicks)
        {
            if (IsLifecycleBlocked) return RunCommandResult.Reject<long>(RunReasons.LifecycleBlocked);
            if (activeTicks < 0) return RunCommandResult.Reject<long>(RunReasons.CommandInvalid);
            if (!CheckedMath.TryAdd(ActiveTicksSinceAnchor, activeTicks, out long newTicks) ||
                !CheckedMath.TryAdd(AnchorUtcMs, newTicks / TicksPerMs, out _))
                return RunCommandResult.Reject<long>(RunReasons.ValueLimit);

            TakeBaseline();
            RunAdvanceResult result = RunSimulation(activeTicks);
            _baselineTaken = false;
            if (result != RunAdvanceResult.Ok) return RunCommandResult.Reject<long>(RunReasons.From(result));
            RecordCompletions();
            ActiveTicksSinceAnchor = newTicks;
            Publish(false, true);
            return RunCommandResult.Success(TimelineUtcMs);
        }

        // ---------------------------------------------------------------------------------------------------
        // Command support (T137)
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Takes the revision baselines. A command calls this after it validated its preconditions and before it
        /// mutates the state (the mutation itself is the command's own checked transaction).
        /// </summary>
        public void BeginCommand() => TakeBaseline();

        /// <summary>
        /// Settles the session at the command timestamp (auto-sales first, then completions and the run
        /// predicate; R01 steps 3-5) without advancing time, publishes the revisions and returns the command
        /// result. <paramref name="structural"/> states that the command changed a structural fact; quantity
        /// changes are detected exactly. If the settle overflows, the result is value.limit; the command's own
        /// mutation has then already been applied, so a command must not let a settle overflow happen after it
        /// mutated (validate the sale income first).
        /// </summary>
        public RunCommandResult<T> EndCommand<T>(T value, bool structural)
        {
            bool tracked = _baselineTaken;
            RunAdvanceResult result = RunSimulation(0);
            _baselineTaken = false;
            if (result == RunAdvanceResult.Ok) RecordCompletions();
            Publish(structural, tracked);
            return result == RunAdvanceResult.Ok
                ? RunCommandResult.Success(value)
                : RunCommandResult.Reject<T>(RunReasons.From(result));
        }

        // ---------------------------------------------------------------------------------------------------
        // Last completions
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// The last completion this session saw of a machine (OD-9): units <c>k'</c> of the batch factor
        /// <c>k</c> of that cycle, and the limit (<see cref="RunSimulator.LimitNone"/>,
        /// <see cref="RunSimulator.LimitStore"/> or the index of the limiting recipe input). False when the machine
        /// has not completed since the session started.
        /// </summary>
        public bool TryGetLastCompletion(Guid instanceId, out long units, out long batchFactor, out int limit)
        {
            for (int i = 0; i < _completionCount; i++)
            {
                if (_completionId[i] != instanceId) continue;
                units = _completionUnits[i];
                batchFactor = _completionBatch[i];
                limit = _completionLimit[i];
                return true;
            }
            units = 0;
            batchFactor = 0;
            limit = RunSimulator.LimitNone;
            return false;
        }

        private void RecordCompletions()
        {
            // Forget removed machines, then take the completions of the advance that just ended.
            int kept = 0;
            for (int i = 0; i < _completionCount; i++)
            {
                if (State.FindMachine(_completionId[i]) == null) continue;
                if (kept != i)
                {
                    _completionId[kept] = _completionId[i];
                    _completionUnits[kept] = _completionUnits[i];
                    _completionBatch[kept] = _completionBatch[i];
                    _completionLimit[kept] = _completionLimit[i];
                }
                kept++;
            }
            _completionCount = kept;

            IReadOnlyList<MachineInstance> machines = State.Machines;
            for (int m = 0; m < machines.Count; m++)
            {
                Guid id = machines[m].InstanceId;
                if (!_simulator.TryGetLastCompletion(id, out long units, out long batch, out int limit)) continue;
                StoreCompletion(id, units, batch, limit);
            }
        }

        private void StoreCompletion(Guid id, long units, long batch, int limit)
        {
            int at = -1;
            for (int i = 0; i < _completionCount && at < 0; i++)
                if (_completionId[i] == id) at = i;
            if (at < 0)
            {
                if (_completionCount == _completionId.Length)
                {
                    Array.Resize(ref _completionId, _completionCount * 2);
                    Array.Resize(ref _completionUnits, _completionCount * 2);
                    Array.Resize(ref _completionBatch, _completionCount * 2);
                    Array.Resize(ref _completionLimit, _completionCount * 2);
                }
                at = _completionCount++;
                _completionId[at] = id;
            }
            _completionUnits[at] = units;
            _completionBatch[at] = batch;
            _completionLimit[at] = limit;
        }

        // ---------------------------------------------------------------------------------------------------
        // Simulation glue
        // ---------------------------------------------------------------------------------------------------

        private RunAdvanceResult RunSimulation(long ticks)
        {
            BuildEvents(ticks);
            return _simulator.Advance(State, Permanent, ticks, Timeline, _events);
        }

        /// <summary>
        /// Derives the boundary events of the state's timers: an offset from the timeline instant in simulation
        /// ticks (one timeline tick is one simulation tick online), clamped to 0 when already due, and only those
        /// inside the advance. The list is insertion-sorted into (SimTicks, Kind, Id) order and reused.
        /// </summary>
        private void BuildEvents(long ticks)
        {
            _events.Clear();
            IReadOnlyList<ActiveEffect> effects = State.Effects.Effects;
            for (int i = 0; i < effects.Count; i++)
                AddEvent(BoundaryKind.EffectExpiry, effects[i].SourceId, effects[i].EndUtcMs, ticks);

            OverdriveState overdrive = State.Overdrive;
            if (overdrive.ActiveUntilUtcMs.HasValue)
            {
                AddEvent(BoundaryKind.OverdriveEnd, OverdriveEventId, overdrive.ActiveUntilUtcMs.Value, ticks);
                AddEvent(BoundaryKind.OverdriveCooldownEnd, OverdriveEventId, overdrive.CooldownUntilUtcMs.Value, ticks);
            }

            ActiveContract contract = State.Contracts.Active;
            if (contract != null && contract.DeadlineUtcMs.HasValue)
                AddEvent(BoundaryKind.ContractExpiry, contract.OfferId, contract.DeadlineUtcMs.Value + 1, ticks);
        }

        private void AddEvent(BoundaryKind kind, string id, long utcMs, long ticks)
        {
            long offset = 0;
            if (utcMs > AnchorUtcMs)
            {
                // A boundary too far away to represent lies beyond any advance.
                if (!CheckedMath.TryMultiply(utcMs - AnchorUtcMs, TicksPerMs, out long timelineTick)) return;
                offset = timelineTick - ActiveTicksSinceAnchor;
                if (offset < 0) offset = 0;
            }
            if (offset > ticks) return;
            var e = new BoundaryEvent(offset, kind, id, utcMs);
            int at = _events.Count;
            _events.Add(e);
            while (at > 0 && BoundaryEvent.Compare(_events[at - 1], e) > 0)
            {
                _events[at] = _events[at - 1];
                at--;
            }
            _events[at] = e;
        }

        // ---------------------------------------------------------------------------------------------------
        // Revisions
        // ---------------------------------------------------------------------------------------------------

        private void TakeBaseline()
        {
            _quantityBaseLength = CaptureQuantities(ref _quantityBase);
            _tokEffects = State.Effects.Count;
            _tokUnlocked = State.Unlocked.Count;
            OverdriveState od = State.Overdrive;
            _tokOverdriveActive = od.IsActive(TimelineUtcMs);
            _tokOverdriveUntil = od.ActiveUntilUtcMs ?? -1;
            _tokOverdriveCooldown = od.CooldownUntilUtcMs ?? -1;
            _tokContract = State.Contracts.Active;
            _tokContractVersion = State.Contracts.Version;
            _baselineTaken = true;
        }

        /// <summary>Cash, RP, every stock unit and every machine's progress and cycle, into a reusable buffer.</summary>
        private int CaptureQuantities(ref long[] buffer)
        {
            GlobalInventory inventory = State.Inventory;
            IReadOnlyList<MachineInstance> machines = State.Machines;
            int need = 2 + inventory.Count + 2 * machines.Count;
            if (buffer.Length < need) buffer = new long[need];
            int n = 0;
            buffer[n++] = State.Cash;
            buffer[n++] = State.Rp;
            for (int i = 0; i < inventory.Count; i++) buffer[n++] = inventory.UnitsAt(i);
            for (int i = 0; i < machines.Count; i++)
            {
                buffer[n++] = machines[i].ProgressTicks;
                buffer[n++] = machines[i].CycleDurationTicks;
            }
            return n;
        }

        /// <summary>
        /// Bumps the revisions after an applied operation. Without a baseline (<paramref name="tracked"/> false)
        /// the change is unknown and both revisions move.
        /// </summary>
        private void Publish(bool structural, bool tracked)
        {
            if (tracked)
            {
                int length = CaptureQuantities(ref _quantityNow);
                bool changed = length != _quantityBaseLength;
                for (int i = 0; !changed && i < length; i++) changed = _quantityNow[i] != _quantityBase[i];
                if (changed) QuantityRevision++;
            }
            else
            {
                QuantityRevision++;
            }

            OverdriveState od = State.Overdrive;
            if (structural || !tracked ||
                _tokEffects != State.Effects.Count ||
                _tokUnlocked != State.Unlocked.Count ||
                _tokOverdriveActive != od.IsActive(TimelineUtcMs) ||
                _tokOverdriveUntil != (od.ActiveUntilUtcMs ?? -1) ||
                _tokOverdriveCooldown != (od.CooldownUntilUtcMs ?? -1) ||
                !ReferenceEquals(_tokContract, State.Contracts.Active) ||
                _tokContractVersion != State.Contracts.Version)
                StructureRevision++;
            _viewsDirty = true;
        }

        // ---------------------------------------------------------------------------------------------------
        // Hot reads (allocation-free once the buffers have grown)
        // ---------------------------------------------------------------------------------------------------

        public long ReadCash() => State.Cash;

        public long ReadRp() => State.Rp;

        /// <summary>Number of resources in the inventory; the layout of <see cref="CopyInventory(long[])"/>.</summary>
        public int InventoryCount => State.Inventory.Count;

        /// <summary>The resource ID of an inventory index (the inventory's fixed order).</summary>
        public string InventoryId(int index) => State.Inventory.IdAt(index);

        /// <summary>Copies the stock units by inventory index; returns the number of entries in the inventory.</summary>
        public int CopyInventory(long[] destination) => CopyInventory(new Span<long>(destination));

        public int CopyInventory(Span<long> destination)
        {
            GlobalInventory inventory = State.Inventory;
            int count = inventory.Count;
            int n = count < destination.Length ? count : destination.Length;
            for (int i = 0; i < n; i++) destination[i] = inventory.UnitsAt(i);
            return count;
        }

        /// <summary>
        /// Copies the machine states in slot order; returns the number of machines (more than the destination
        /// length means the copy was truncated).
        /// </summary>
        public int CopyMachineStates(MachineStateView[] destination) => CopyMachineStates(new Span<MachineStateView>(destination));

        public int CopyMachineStates(Span<MachineStateView> destination)
        {
            if (_viewsDirty) RefreshViews();
            int n = _viewCount < destination.Length ? _viewCount : destination.Length;
            for (int i = 0; i < n; i++) destination[i] = _views[i];
            return _viewCount;
        }

        /// <summary>
        /// Rebuilds the machine read model at the timeline instant from the composed speed, the recipe and the
        /// stock (the run predicate of R01 step 5). A checked overflow while deriving leaves that machine's batch
        /// factor 0 and its blocker empty; the simulator has already rejected any advance that would do so.
        /// </summary>
        private void RefreshViews()
        {
            IReadOnlyList<MachineInstance> machines = State.Machines;
            int count = machines.Count;
            if (_views.Length < count) _views = new MachineStateView[count];
            bool overdrive = State.Overdrive.IsActive(TimelineUtcMs);
            GlobalInventory stock = State.Inventory;
            for (int i = 0; i < count; i++)
            {
                MachineInstance m = machines[i];
                _typeIndex.TryGetValue(m.MachineId, out int typeIndex);
                long batch = 0;
                MachineBlocker blocker = MachineBlocker.None;
                long units = 0;
                bool derived = false;
                if (_catalog.Machines.TryGetValue(m.MachineId, out MachineDef def) &&
                    _catalog.Recipes.TryGetValue(def.RecipeId, out MachineRecipe recipe) &&
                    _modifiers.TryMachineSpeed(State, Permanent, m, overdrive, null, out long speed) &&
                    SpeedComposer.TryBatch(recipe.DurationTicks, speed, _catalog.RunStart.MinCycleTicks, out batch, out _) &&
                    _modifiers.TryOutputFactor(State, m, null, out long factor))
                {
                    derived = true;
                    bool allowed = true;
                    for (int r = 0; r < recipe.Inputs.Count && allowed; r++) allowed = stock.IsAllowed(recipe.Inputs[r].ResourceId);
                    for (int r = 0; r < recipe.Outputs.Count && allowed; r++) allowed = stock.IsAllowed(recipe.Outputs[r].ResourceId);
                    if (allowed && !SpeedComposer.TryCompletedUnits(batch, recipe, factor, stock, out units)) derived = false;
                    if (derived && units < 1)
                    {
                        bool missing = !allowed;
                        for (int r = 0; r < recipe.Inputs.Count && !missing; r++)
                            missing = stock.Units(recipe.Inputs[r].ResourceId) < recipe.Inputs[r].Amount;
                        blocker = missing ? MachineBlocker.MissingInput : MachineBlocker.StorageFull;
                    }
                }
                if (!derived)
                {
                    batch = 0;
                    blocker = MachineBlocker.None;
                }

                MachinePhase phase;
                if (!m.IsActive)
                {
                    phase = MachinePhase.Paused;
                    blocker = MachineBlocker.Inactive;
                }
                else if (!derived || units >= 1)
                {
                    phase = m.IsWaiting ? MachinePhase.Waiting : MachinePhase.Running;
                }
                else
                {
                    phase = m.IsWaiting ? MachinePhase.Waiting : MachinePhase.Paused;
                }

                long ppm = 1_000_000;
                if (!m.IsWaiting) MulDiv.MulDivFloor(m.ProgressTicks, 1_000_000, m.CycleDurationTicks, out ppm);
                _views[i] = new MachineStateView(m.SlotIndex, typeIndex, m.Level, m.IsActive, phase, blocker, batch, ppm);
            }
            _viewCount = count;
            _viewsDirty = false;
        }
    }
}
