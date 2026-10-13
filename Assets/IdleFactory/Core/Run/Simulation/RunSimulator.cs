using System;
using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;

namespace IdleFactory.Run.Simulation
{
    /// <summary>
    /// The discrete-event run simulator (research R01, R03, R05; OD-2, OD-6, OD-9). One entry point,
    /// <see cref="Advance"/>, is shared by the online session and offline segmentation (GD-3); the per-timestamp
    /// rules live in exactly one procedure, <c>ProcessTimestamp</c>.
    ///
    /// Staging: an advance works on private staged buffers (a copy of the inventory, Cash, and per machine progress,
    /// cycle duration, batch, output factor and running flag, plus the expiry decisions). Nothing of the
    /// <see cref="RunState"/> changes while the advance runs. On success the staged result is published once; a
    /// checked overflow anywhere rejects the whole advance and the state stays untouched. After the buffers have
    /// grown to the size of the factory, an advance allocates nothing.
    ///
    /// Unlocks (T140, research R01 "Unlocks"): every processed timestamp ends with an unlock pass that evaluates the
    /// conditions of the still locked resources, machine types, contract offers and Overdrive, after completions and
    /// sales and before the run predicate. A newly unlocked resource is allowed in the staged inventory at once, so a
    /// machine that waited for it completes at the same instant; completions, sales and the unlock pass repeat until
    /// nothing unlocks, so chains resolve at one instant. Unlocks are staged like everything else and published with
    /// the commit, or dropped with a rejected advance.
    ///
    /// The next event is found by a linear scan of the machines and the next supplied boundary; there is no heap.
    /// An instance keeps scratch buffers and is not thread-safe.
    /// </summary>
    public sealed class RunSimulator
    {
        private static readonly IReadOnlyList<BoundaryEvent> NoEvents = Array.Empty<BoundaryEvent>();

        private readonly RunCatalog _catalog;
        private readonly RunModifiers _modifiers;

        // Unlock candidates (T140): fixed at construction in a hash-independent order (resources, machine types,
        // contract offers, each in ordinal ID order, then Overdrive).
        private const int KindAlways = 0;
        private const int KindResearch = 1;
        private const int KindFactoryLevel = 2;
        private const int KindProduced = 3;
        private readonly int _candCount;
        private readonly string[] _candId;
        private readonly int[] _candKind;
        private readonly string[] _candRef;
        private readonly int[] _candLevel;
        private readonly bool[] _candResource;

        // Staged unlock state.
        private readonly bool[] _candLocked;
        private readonly int[] _newUnlock;
        private readonly int[] _newResource;
        private int _lockedCount;
        private int _newCount;
        private bool[] _producedSeen = new bool[0];
        private bool _producedDirty;

        // Staged run state.
        private RunState _state;
        private PermanentState _permanent;
        private GlobalInventory _stock;
        private long _cash;
        private int _machines;
        private MachineInstance[] _machine = new MachineInstance[0];
        private MachineRecipe[] _recipe = new MachineRecipe[0];
        private int[] _order = new int[0];
        private long[] _progress = new long[0];
        private long[] _cycle = new long[0];
        private long[] _batch = new long[0];
        private long[] _outputFactor = new long[0];
        private bool[] _active = new bool[0];
        private bool[] _allowed = new bool[0];
        private bool[] _running = new bool[0];
        private Guid[] _lastId = new Guid[0];
        private bool[] _completedNow = new bool[0];
        private long[] _lastUnits = new long[0];
        private long[] _lastBatch = new long[0];
        private int[] _lastLimit = new int[0];
        private bool _completionsValid;
        private int _sellCount;
        private string[] _sellId = new string[0];
        private long[] _sellBase = new long[0];
        private long[] _sellPpm = new long[0];
        private bool[] _effectExpired = new bool[0];
        private int[] _sellIndex = new int[0];
        // T360 (Option 1): per machine, the staged recipe resolved to inventory positions and amounts.
        private RecipeIndex[] _index = new RecipeIndex[0];
        private long[] _produced = new long[0];
        private long[] _consumed = new long[0];
        private long[] _sold = new long[0];
        private bool _overdriveActive;
        private bool _effectsExpired;
        private long _effectCutoffUtcMs;
        private bool _cooldownEnded;
        private long _cooldownUtcMs;
        private bool _contractExpired;
        private long _contractUtcMs;
        private bool _configDirty;
        private IReadOnlyList<BoundaryEvent> _events;
        private int _eventIndex;
        private RunAdvanceResult _fault;

        public RunSimulator(RunCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _modifiers = new RunModifiers(catalog);

            int total = catalog.Resources.Count + catalog.Machines.Count + catalog.ContractOffers.Count + 1;
            _candId = new string[total];
            _candKind = new int[total];
            _candRef = new string[total];
            _candLevel = new int[total];
            _candResource = new bool[total];
            _candLocked = new bool[total];
            _newUnlock = new int[total];
            _newResource = new int[total];
            int n = 0;
            n = AddCandidates(n, catalog.Resources, r => r.Unlock, true);
            n = AddCandidates(n, catalog.Machines, m => m.Unlock, false);
            n = AddCandidates(n, catalog.ContractOffers, o => o.Unlock, false);
            SetCandidate(n++, RunState.OverdriveUnlockId, catalog.Overdrive.Unlock, false);
            _candCount = n;
        }

        private int AddCandidates<T>(int n, IReadOnlyDictionary<string, T> defs, Func<T, UnlockCondition> unlock, bool resource)
        {
            var ids = new List<string>(defs.Keys);
            ids.Sort(StringComparer.Ordinal);
            for (int i = 0; i < ids.Count; i++) SetCandidate(n++, ids[i], unlock(defs[ids[i]]), resource);
            return n;
        }

        private void SetCandidate(int at, string id, UnlockCondition condition, bool resource)
        {
            _candId[at] = id;
            _candRef[at] = condition.Id;
            _candLevel[at] = condition.Level;
            _candResource[at] = resource;
            switch (condition.Kind)
            {
                case "researchPurchased": _candKind[at] = KindResearch; break;
                case "factoryLevelAtLeast": _candKind[at] = KindFactoryLevel; break;
                case "resourceProduced": _candKind[at] = KindProduced; break;
                default: _candKind[at] = KindAlways; break;
            }
        }

        /// <summary>Completed machine cycles (exchanges) of the last advance.</summary>
        public long LastCompletions { get; private set; }

        /// <summary>Completions of the last advance that exchanged fewer units than the batch factor (OD-9).</summary>
        public long LastPartialBatches { get; private set; }

        /// <summary>Timestamps processed by the last advance (start, every event and the end included).</summary>
        public long LastTimestamps { get; private set; }

        /// <summary>IDs newly unlocked by the last successful advance (resources, machine types, offers, Overdrive).</summary>
        public int LastUnlockCount { get; private set; }

        /// <summary>Completion-and-sale passes over all timestamps of the last advance.</summary>
        public long LastPasses { get; private set; }

        /// <summary>
        /// Test-only counter (PE-7): the largest number of passes any single timestamp of the last advance took.
        /// OD-2 bounds it by the machine count plus one.
        /// </summary>
        public int LastMaxPassesPerTimestamp { get; private set; }

        /// <summary>
        /// Test-only flow accounting of the last advance (PE-7, QA-3): units of a resource added by completions,
        /// removed by completions and removed by auto-sales. They are summed from the recipe amounts, never read back
        /// from the inventory, so <c>stock change = produced - consumed - sold</c> checks the exchange arithmetic.
        /// </summary>
        public long LastProducedUnits(string resourceId) => Flow(_produced, resourceId);

        public long LastConsumedUnits(string resourceId) => Flow(_consumed, resourceId);

        public long LastSoldUnits(string resourceId) => Flow(_sold, resourceId);

        /// <summary>The batch of a completion was not limited (a full batch).</summary>
        public const int LimitNone = -1;

        /// <summary>The store limited a partial batch (<c>k_cap</c> was the minimum).</summary>
        public const int LimitStore = -2;

        /// <summary>
        /// The last completion of a machine inside the last successful advance (RatesSnapshot, OD-9): the units
        /// <c>k'</c> exchanged, the batch factor <c>k</c> of that cycle and what limited a partial batch, either the
        /// index into the recipe's inputs of the limiting input, <see cref="LimitStore"/> for the store, or
        /// <see cref="LimitNone"/> for a full batch. When inputs and store limit equally the input is named. False
        /// when the machine did not complete in that advance (or the advance was rejected).
        /// </summary>
        public bool TryGetLastCompletion(Guid instanceId, out long units, out long batchFactor, out int limit)
        {
            units = 0;
            batchFactor = 0;
            limit = LimitNone;
            if (!_completionsValid) return false;
            for (int i = 0; i < _machines; i++)
            {
                if (_lastId[i] != instanceId) continue;
                if (!_completedNow[i]) return false;
                units = _lastUnits[i];
                batchFactor = _lastBatch[i];
                limit = _lastLimit[i];
                return true;
            }
            return false;
        }

        private long Flow(long[] flow, string resourceId)
        {
            if (_stock == null) return 0;
            for (int i = 0; i < _stock.Count && i < flow.Length; i++)
                if (string.Equals(_stock.IdAt(i), resourceId, StringComparison.Ordinal)) return flow[i];
            return 0;
        }

        private int IndexOfResource(string resourceId)
        {
            for (int i = 0; i < _stock.Count; i++)
                if (string.Equals(_stock.IdAt(i), resourceId, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>
        /// Advances <paramref name="state"/> by <paramref name="ticks"/> simulation ticks (100 ns) and publishes the
        /// result once.
        /// </summary>
        /// <param name="permanent">Source of Industry levels; may be null when the catalog has none.</param>
        /// <param name="startUtcMs">
        /// A real instant (Unix ms) that belongs to simulation offset 0. It only decides whether Overdrive is active
        /// at the start of the advance (<c>now &lt; ActiveUntilUtcMs</c>). Overdrive changes only at a boundary
        /// event, so any real instant between the last boundary already processed and the next one is correct;
        /// online that is the timeline instant, offline the instant of the last processed boundary or the departure.
        /// </param>
        /// <param name="events">
        /// Real-time boundaries in simulation time, ordered by <c>(SimTicks, Kind, Id)</c>. Every event with
        /// <c>0 &lt;= SimTicks &lt;= ticks</c> is processed by this call, including one exactly at the end (an
        /// instant that ends an advance belongs to that advance); later events are ignored. Expiry is idempotent, so
        /// an event given to two adjacent advances at their shared instant does no harm.
        /// </param>
        public RunAdvanceResult Advance(RunState state, PermanentState permanent, long ticks, long startUtcMs,
            IReadOnlyList<BoundaryEvent> events)
        {
            LastCompletions = 0;
            LastPartialBatches = 0;
            LastTimestamps = 0;
            LastUnlockCount = 0;
            LastPasses = 0;
            LastMaxPassesPerTimestamp = 0;
            _completionsValid = false;
            if (state == null || !ReferenceEquals(state.Catalog, _catalog) || ticks < 0) return RunAdvanceResult.InvalidArgument;
            _events = events ?? NoEvents;
            if (!EventsAreOrdered())
            {
                _events = null;
                return RunAdvanceResult.InvalidArgument;
            }

            _fault = RunAdvanceResult.Ok;
            if (!Stage(state, permanent, startUtcMs)) return Finish(_fault);

            long now = 0;
            _eventIndex = 0;
            _configDirty = true;
            if (!ProcessTimestamp(0, true)) return Finish(_fault);
            while (now < ticks)
            {
                long step = ticks - now;
                if (_eventIndex < _events.Count)
                {
                    long untilEvent = _events[_eventIndex].SimTicks - now;
                    if (untilEvent < step) step = untilEvent;
                }
                for (int i = 0; i < _machines; i++)
                {
                    if (!_running[i]) continue;
                    long remaining = _cycle[i] - _progress[i];
                    if (remaining < step) step = remaining;
                }
                for (int i = 0; i < _machines; i++)
                    if (_running[i]) _progress[i] += step;
                now += step;
                if (!ProcessTimestamp(now, false)) return Finish(_fault);
            }

            Commit();
            LastUnlockCount = _newCount;
            _completionsValid = true;
            return Finish(RunAdvanceResult.Ok);
        }

        private RunAdvanceResult Finish(RunAdvanceResult result)
        {
            _state = null;
            _permanent = null;
            _events = null;
            return result;
        }

        private bool EventsAreOrdered()
        {
            for (int i = 0; i < _events.Count; i++)
            {
                BoundaryEvent e = _events[i];
                if (e.SimTicks < 0 || e.Kind < BoundaryKind.EffectExpiry || e.Kind > BoundaryKind.ContractExpiry) return false;
                if (i > 0 && BoundaryEvent.Compare(_events[i - 1], e) > 0) return false;
            }
            return true;
        }

        private bool Fail(RunAdvanceResult fault)
        {
            _fault = fault;
            return false;
        }

        // ---------------------------------------------------------------------------------------------------
        // Staging and publishing
        // ---------------------------------------------------------------------------------------------------

        private bool Stage(RunState state, PermanentState permanent, long startUtcMs)
        {
            _state = state;
            _permanent = permanent;
            int n = state.Machines.Count;
            if (state.Priority.Count != n) return Fail(RunAdvanceResult.InvalidArgument);
            if (_machine.Length < n) GrowMachineBuffers(n);
            _machines = n;

            for (int i = 0; i < n; i++)
            {
                MachineInstance m = state.Machines[i];
                _machine[i] = m;
                _lastId[i] = m.InstanceId;
                _completedNow[i] = false;
                _progress[i] = m.ProgressTicks;
                _cycle[i] = m.CycleDurationTicks;
                _active[i] = m.IsActive;
                _running[i] = false;
                if (!_catalog.Machines.TryGetValue(m.MachineId, out MachineDef def) ||
                    !_catalog.Recipes.TryGetValue(def.RecipeId, out MachineRecipe recipe))
                    return Fail(RunAdvanceResult.InvalidArgument);
                _recipe[i] = recipe;
            }

            // Priority position -> machine index. The order never changes during an advance.
            for (int o = 0; o < n; o++)
            {
                Guid id = state.Priority[o];
                int at = -1;
                for (int i = 0; i < n; i++)
                    if (_machine[i].InstanceId == id) { at = i; break; }
                if (at < 0) return Fail(RunAdvanceResult.InvalidArgument);
                _order[o] = at;
            }

            // The staged inventory is reused while the shape (capacity and allowed resources) matches.
            if (_stock == null || !_stock.HasSameShape(state.Inventory))
            {
                var ids = new string[state.Inventory.Count];
                for (int i = 0; i < ids.Length; i++) ids[i] = state.Inventory.IdAt(i);
                _stock = new GlobalInventory(ids, state.Inventory.Capacity);
            }
            _stock.CopyUnitsFrom(state.Inventory);
            _cash = state.Cash;

            if (_produced.Length != _stock.Count)
            {
                _produced = new long[_stock.Count];
                _consumed = new long[_stock.Count];
                _sold = new long[_stock.Count];
            }
            if (_producedSeen.Length != _stock.Count) _producedSeen = new bool[_stock.Count];
            for (int i = 0; i < _produced.Length; i++)
            {
                _produced[i] = 0;
                _consumed[i] = 0;
                _sold[i] = 0;
                _producedSeen[i] = false;
            }
            if (_index.Length < n) _index = new RecipeIndex[n];
            RebuildIndexes();

            // Unlock candidates that are still locked in this run (T140).
            _lockedCount = 0;
            _newCount = 0;
            _producedDirty = false;
            for (int k = 0; k < _candCount; k++)
            {
                _candLocked[k] = !state.IsUnlocked(_candId[k]);
                if (_candLocked[k]) _lockedCount++;
            }

            // Auto-sell resources, in ordinal order, with their base price and effective price multiplier.
            IReadOnlyList<string> autoSell = state.AutoSell;
            _sellCount = autoSell.Count;
            if (_sellId.Length < _sellCount)
            {
                _sellId = new string[_sellCount];
                _sellBase = new long[_sellCount];
                _sellPpm = new long[_sellCount];
                _sellIndex = new int[_sellCount];
            }
            for (int s = 0; s < _sellCount; s++)
            {
                if (!_catalog.Resources.TryGetValue(autoSell[s], out ResourceDef resource))
                    return Fail(RunAdvanceResult.InvalidArgument);
                _sellId[s] = autoSell[s];
                _sellIndex[s] = IndexOfResource(autoSell[s]);
                _sellBase[s] = resource.BasePrice;
                if (!_modifiers.TrySalePricePpm(state, permanent, autoSell[s], out _sellPpm[s]))
                    return Fail(RunAdvanceResult.ArithmeticOverflow);
            }

            // Expiry decisions are staged as well.
            int effects = state.Effects.Count;
            if (_effectExpired.Length < effects) _effectExpired = new bool[effects];
            for (int i = 0; i < effects; i++) _effectExpired[i] = false;
            _effectsExpired = false;
            _effectCutoffUtcMs = long.MinValue;
            _cooldownEnded = false;
            _contractExpired = false;
            _overdriveActive = state.Overdrive.IsActive(startUtcMs);
            return true;
        }

        /// <summary>
        /// Maps the recipe resources of every machine to inventory indexes and decides which machines may complete. A
        /// recipe that names a resource the run has not unlocked cannot complete (its exchange would be rejected as
        /// ResourceLocked); the machine stays blocked instead of failing the advance.
        /// </summary>
        private void RebuildIndexes()
        {
            for (int i = 0; i < _machines; i++)
            {
                MachineRecipe recipe = _recipe[i];
                if (_index[i] == null) _index[i] = new RecipeIndex();
                _index[i].Rebuild(recipe, _stock);
                bool allowed = true;
                for (int r = 0; r < recipe.Inputs.Count && allowed; r++) allowed = _stock.IsAllowed(recipe.Inputs[r].ResourceId);
                for (int r = 0; r < recipe.Outputs.Count && allowed; r++) allowed = _stock.IsAllowed(recipe.Outputs[r].ResourceId);
                _allowed[i] = allowed;
            }
        }

        private bool ConditionHolds(int k)
        {
            switch (_candKind[k])
            {
                case KindResearch: return _state.ResearchLevel(_candRef[k]) >= 1;
                case KindFactoryLevel: return _state.FactoryLevel >= _candLevel[k];
                case KindProduced:
                {
                    int at = IndexOfResource(_candRef[k]);
                    return at >= 0 && _producedSeen[at];
                }
                default: return true;
            }
        }

        /// <summary>
        /// Evaluates every still locked candidate in the fixed candidate order and unlocks those whose condition
        /// holds (staged). <paramref name="all"/> is true at the first timestamp of an advance. Later timestamps can
        /// only change a <c>resourceProduced</c> condition (Research levels and the factory level do not move inside
        /// an advance), so they evaluate only after a resource was produced for the first time. Returns whether
        /// anything unlocked.
        /// </summary>
        private bool UnlockPass(bool all)
        {
            bool evaluate = all || _producedDirty;
            _producedDirty = false;
            if (!evaluate || _lockedCount == 0) return false;
            int before = _newCount;
            int resources = 0;
            for (int k = 0; k < _candCount; k++)
            {
                if (!_candLocked[k] || !ConditionHolds(k)) continue;
                _candLocked[k] = false;
                _lockedCount--;
                _newUnlock[_newCount++] = k;
                if (_candResource[k]) _newResource[resources++] = k;
            }
            if (_newCount == before) return false;
            if (resources > 0) AllowStaged(resources);
            return true;
        }

        /// <summary>
        /// Allows newly unlocked resources in the staged inventory (its shape then equals the shape the published
        /// inventory gets when <see cref="Commit"/> unlocks them) and re-maps everything indexed by inventory
        /// position. Unlocks are rare, so this may allocate; a pass that unlocks nothing does not.
        /// </summary>
        private void AllowStaged(int resources)
        {
            int oldCount = _stock.Count;
            var oldIds = new string[oldCount];
            for (int i = 0; i < oldCount; i++) oldIds[i] = _stock.IdAt(i);
            for (int j = 0; j < resources; j++) _stock.Allow(_candId[_newResource[j]]);
            int count = _stock.Count;
            if (count == oldCount) return;
            _produced = Remap(_produced, oldIds);
            _consumed = Remap(_consumed, oldIds);
            _sold = Remap(_sold, oldIds);
            var seen = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int old = Array.IndexOf(oldIds, _stock.IdAt(i));
                seen[i] = old >= 0 && _producedSeen[old];
            }
            _producedSeen = seen;
            for (int s = 0; s < _sellCount; s++) _sellIndex[s] = IndexOfResource(_sellId[s]);
            RebuildIndexes();
        }

        private long[] Remap(long[] values, string[] oldIds)
        {
            var next = new long[_stock.Count];
            for (int i = 0; i < next.Length; i++)
            {
                int old = Array.IndexOf(oldIds, _stock.IdAt(i));
                if (old >= 0) next[i] = values[old];
            }
            return next;
        }

        private void GrowMachineBuffers(int n)
        {
            _machine = new MachineInstance[n];
            _recipe = new MachineRecipe[n];
            _order = new int[n];
            _progress = new long[n];
            _cycle = new long[n];
            _batch = new long[n];
            _outputFactor = new long[n];
            _active = new bool[n];
            _allowed = new bool[n];
            _running = new bool[n];
            _lastId = new Guid[n];
            _completedNow = new bool[n];
            _lastUnits = new long[n];
            _lastBatch = new long[n];
            _lastLimit = new int[n];
        }

        /// <summary>Publishes the staged result once. Every step is a checked domain operation that cannot fail here.</summary>
        private void Commit()
        {
            RunState state = _state;
            // Unlocks first: a resource becomes allowed in the published inventory, which gives it the shape of the
            // staged copy, and then the units are copied.
            for (int k = 0; k < _newCount; k++) state.TryUnlock(_candId[_newUnlock[k]]);
            state.Inventory.CopyUnitsFrom(_stock);
            if (_cash != state.Cash) state.TryAddCash(_cash - state.Cash);
            for (int i = 0; i < _machines; i++)
            {
                MachineInstance m = _machine[i];
                if (m.ProgressTicks == _progress[i] && m.CycleDurationTicks == _cycle[i]) continue;
                m.TryStartCycle(_cycle[i]);
                m.TrySetProgress(_progress[i]);
            }
            if (_effectsExpired) state.Effects.RemoveExpired(_effectCutoffUtcMs);
            if (_cooldownEnded) state.Overdrive.ClearElapsed(_cooldownUtcMs);
            if (_contractExpired) state.Contracts.TryExpire(_contractUtcMs);
            for (int i = 0; i < _machines; i++) _machine[i] = null;
        }

        // ---------------------------------------------------------------------------------------------------
        // ProcessTimestamp: the one ordered procedure (R01)
        // ---------------------------------------------------------------------------------------------------

        private bool ProcessTimestamp(long now, bool settle)
        {
            LastTimestamps++;

            // 1. Expire every effect, Overdrive state and contract deadline whose boundary is due; then re-derive
            //    the effective cycle of every machine and rescale its progress once (R03).
            while (_eventIndex < _events.Count && _events[_eventIndex].SimTicks <= now)
            {
                ApplyBoundary(_events[_eventIndex]);
                _eventIndex++;
            }
            if (_configDirty)
            {
                if (!RefreshDerived()) return false;
                _configDirty = false;
            }

            // Settle (first timestamp of an advance only): a state handed over by a command may hold stock of an
            // auto-sell resource (auto-sell just enabled, a delivery, an import). The command rule of R01 settles
            // with the sales first (steps 3-5). At every later timestamp this stock is already zero, so the sale is
            // skipped there; this also keeps the pass count at most machines + 1 (OD-2).
            if (settle && !AutoSell()) return false;

            // 2-4. Completions in priority order, then auto-sales; repeat (OD-2) until a pass completes nothing.
            //      A completed machine restarts at progress 0, so it completes at most once per timestamp and
            //      there are at most machines + 1 passes (plus one per unlock round, see 4b).
            // 4b. Unlock pass (T140): after completions and sales, before the run predicate. When it unlocks
            //      something, completions and sales run again (a machine may have waited for a newly allowed
            //      resource, and a completion may satisfy another condition) until nothing unlocks, so chains
            //      resolve at this instant and the state after a timestamp is a fixed point of all three.
            int passes = 0;
            bool evaluateAll = settle;
            while (true)
            {
                while (true)
                {
                    passes++;
                    int completed = CompletionPass();
                    if (completed < 0) return false;
                    if (!AutoSell()) return false;
                    if (completed == 0) break;
                }
                bool unlocked = UnlockPass(evaluateAll);
                evaluateAll = false;
                if (!unlocked) break;
            }
            LastPasses += passes;
            if (passes > LastMaxPassesPerTimestamp) LastMaxPassesPerTimestamp = passes;

            // 5. Run predicate: a machine with progress below its cycle runs until the next event exactly when it
            //    is active and at least one recipe unit's inputs are in stock and its net exchange fits.
            for (int i = 0; i < _machines; i++)
            {
                bool run = false;
                if (_active[i] && _progress[i] < _cycle[i])
                {
                    if (!UnitsThatFit(i, out long units)) return false;
                    run = units >= 1;
                }
                _running[i] = run;
            }
            return true;
        }

        private void ApplyBoundary(in BoundaryEvent e)
        {
            switch (e.Kind)
            {
                case BoundaryKind.EffectExpiry:
                {
                    IReadOnlyList<ActiveEffect> effects = _state.Effects.Effects;
                    for (int i = 0; i < effects.Count; i++)
                    {
                        if (_effectExpired[i] || effects[i].EndUtcMs > e.UtcMs) continue;
                        _effectExpired[i] = true;
                        _configDirty = true;
                    }
                    _effectsExpired = true;
                    if (e.UtcMs > _effectCutoffUtcMs) _effectCutoffUtcMs = e.UtcMs;
                    break;
                }
                case BoundaryKind.OverdriveEnd:
                    if (_overdriveActive) _configDirty = true;
                    _overdriveActive = false;
                    break;
                case BoundaryKind.OverdriveCooldownEnd:
                    if (_overdriveActive) _configDirty = true;
                    _overdriveActive = false;
                    _cooldownEnded = true;
                    _cooldownUtcMs = e.UtcMs;
                    break;
                case BoundaryKind.ContractExpiry:
                    _contractExpired = true;
                    _contractUtcMs = e.UtcMs;
                    break;
            }
        }

        /// <summary>
        /// Derives speed, batch factor, cycle duration and output factor of every machine from the staged
        /// modifiers and rescales progress where the cycle duration changed (<c>p' = floor(p x D' / D)</c>; a waiting
        /// machine stays waiting). Runs at the start of an advance and after a boundary that changed a modifier.
        /// </summary>
        private bool RefreshDerived()
        {
            for (int i = 0; i < _machines; i++)
            {
                MachineInstance m = _machine[i];
                if (!_modifiers.TryMachineSpeed(_state, _permanent, m, _overdriveActive, _effectExpired, out long speed) ||
                    !SpeedComposer.TryBatch(_recipe[i].DurationTicks, speed, _catalog.RunStart.MinCycleTicks,
                        out long batch, out long cycle) ||
                    !_modifiers.TryOutputFactor(_state, m, _effectExpired, out long factor))
                    return Fail(RunAdvanceResult.ArithmeticOverflow);
                if (cycle != _cycle[i])
                {
                    if (!SpeedComposer.TryRescaleProgress(_progress[i], _cycle[i], cycle, out long progress))
                        return Fail(RunAdvanceResult.ArithmeticOverflow);
                    _progress[i] = progress;
                    _cycle[i] = cycle;
                }
                _batch[i] = batch;
                _outputFactor[i] = factor;
                _index[i].SetFactor(batch, factor);
            }
            return true;
        }

        /// <summary>
        /// The units <c>k'</c> a cycle of machine <paramref name="i"/> would complete now (OD-9), 0 when the
        /// recipe touches a locked resource. False on a checked overflow.
        /// </summary>
        private bool UnitsThatFit(int i, out long units)
        {
            units = 0;
            if (!_allowed[i]) return true;
            if (!_index[i].TryFit(_batch[i], _stock, out units))
                return Fail(RunAdvanceResult.ArithmeticOverflow);
            return true;
        }

        /// <summary>
        /// Completes every due machine (progress equals cycle) in the player's priority order. Each completion is
        /// one atomic exchange of <c>k'</c> units; <c>k' = 0</c> leaves the machine waiting at full progress. Returns
        /// the number of completions, or -1 after a fault.
        /// </summary>
        private int CompletionPass()
        {
            int completed = 0;
            for (int o = 0; o < _machines; o++)
            {
                int i = _order[o];
                if (!_active[i] || _progress[i] != _cycle[i]) continue;
                if (!UnitsThatFit(i, out long units)) return -1;
                if (units < 1) continue;
                RecipeIndex index = _index[i];
                int limit = units < _batch[i] ? LimitOf(i, units) : LimitNone;
                RunStateResult exchange = _stock.TryExchangeIndexed(index, units, _outputFactor[i]);
                if (exchange != RunStateResult.Ok)
                {
                    Fail(exchange == RunStateResult.ValueLimit ? RunAdvanceResult.ArithmeticOverflow : RunAdvanceResult.InvalidArgument);
                    return -1;
                }
                int[] inIndex = index.InIndex;
                long[] inAmount = index.InAmount;
                for (int r = 0; r < inIndex.Length; r++) _consumed[inIndex[r]] += units * inAmount[r];
                int[] outIndex = index.OutIndex;
                long[] outAmount = index.OutAmount;
                for (int r = 0; r < outIndex.Length; r++)
                {
                    int at = outIndex[r];
                    _produced[at] += units * outAmount[r] * _outputFactor[i];
                    if (!_producedSeen[at])
                    {
                        _producedSeen[at] = true;
                        _producedDirty = true;
                    }
                }
                _completedNow[i] = true;
                _lastUnits[i] = units;
                _lastBatch[i] = _batch[i];
                _lastLimit[i] = limit;
                _progress[i] = 0;
                completed++;
                LastCompletions++;
                if (units < _batch[i]) LastPartialBatches++;
            }
            return completed;
        }

        /// <summary>
        /// What limited a partial batch of <paramref name="units"/> (fewer than the batch) units of machine <paramref name="i"/>,
        /// read before the exchange: the input with the smallest <c>floor(stock / amount)</c> (the first on a tie)
        /// when it equals the completed units, otherwise the store.
        /// </summary>
        private int LimitOf(int i, long units)
        {
            RecipeIndex index = _index[i];
            for (int r = 0; r < index.InIndex.Length; r++)
                if (_stock.UnitsAt(index.InIndex[r]) / index.InAmount[r] == units) return r;
            return LimitStore;
        }

        /// <summary>Sells the whole stock of every auto-sell resource at the effective price.</summary>
        private bool AutoSell()
        {
            for (int s = 0; s < _sellCount; s++)
            {
                int at = _sellIndex[s];
                long units = at >= 0 ? _stock.UnitsAt(at) : 0;
                if (units < 1) continue;
                if (!CheckedMath.TryMultiply(units, _sellBase[s], out long gross) ||
                    !MulDiv.MulDivFloor(gross, _sellPpm[s], SpeedComposer.Ppm, out long credit) ||
                    !CheckedMath.TryAdd(_cash, credit, out long cash))
                    return Fail(RunAdvanceResult.ArithmeticOverflow);
                if (_stock.TrySell(_sellId[s], units) != RunStateResult.Ok) return Fail(RunAdvanceResult.InvalidArgument);
                _cash = cash;
                _sold[_sellIndex[s]] += units;
            }
            return true;
        }
    }
}
