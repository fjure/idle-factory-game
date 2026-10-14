using System;
using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;

namespace IdleFactory.Run.Domain
{
    /// <summary>
    /// The run-scoped state and its cross-object invariants. Machines are kept in ascending slot order, the
    /// unlock and auto-sell sets in the order documented on their members, and every mutator validates before
    /// it changes anything (a non-<see cref="RunStateResult.Ok"/> result means no change). Engine-free.
    /// </summary>
    public sealed class RunState
    {
        private readonly RunCatalog _catalog;
        private readonly List<MachineInstance> _machines = new List<MachineInstance>();
        private readonly List<string> _unlocked = new List<string>();
        private readonly List<string> _autoSell = new List<string>();
        private readonly OrdinalCounts _researchLevels = new OrdinalCounts();

        /// <summary>
        /// The ID under which the Overdrive feature appears in <see cref="Unlocked"/> (T140). Overdrive is a single
        /// feature without a definition ID of its own; its <c>OverdriveDef.Unlock</c> condition is evaluated like
        /// every other and the result is stored under this reserved ID.
        /// </summary>
        public const string OverdriveUnlockId = "overdrive";

        public Guid RunId { get; }
        public int RunNumber { get; }
        public Guid? PreviousRunId { get; }
        public long Cash { get; private set; }
        public long Rp { get; private set; }
        public int StorageLevel { get; private set; }
        public long StorageInvestedCash { get; private set; }
        public int FactoryLevel { get; private set; }
        public long FactoryInvestedCash { get; private set; }
        public GlobalInventory Inventory { get; }
        public PriorityOrder Priority { get; } = new PriorityOrder();
        public ContractBoard Contracts { get; }
        public ActiveEffectSet Effects { get; } = new ActiveEffectSet();
        public OverdriveState Overdrive { get; }

        public RunState(RunCatalog catalog, Guid runId, int runNumber, Guid? previousRunId, long cash, long rp,
            int storageLevel, int factoryLevel, IEnumerable<string> unlocked)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (runId == Guid.Empty) throw new ArgumentException("Run ID is required.", nameof(runId));
            if (runNumber < 1) throw new ArgumentOutOfRangeException(nameof(runNumber));
            if ((runNumber == 1) == previousRunId.HasValue || (previousRunId.HasValue && previousRunId.Value == Guid.Empty))
                throw new ArgumentException("PreviousRunId is absent exactly when RunNumber is 1.", nameof(previousRunId));
            if (!ExactBound.Contains(cash)) throw new ArgumentOutOfRangeException(nameof(cash));
            if (!ExactBound.Contains(rp)) throw new ArgumentOutOfRangeException(nameof(rp));
            if (storageLevel < 1 || storageLevel > catalog.StorageLevels.Count) throw new ArgumentOutOfRangeException(nameof(storageLevel));
            if (factoryLevel < 1 || factoryLevel > catalog.FactoryLevels.Count) throw new ArgumentOutOfRangeException(nameof(factoryLevel));
            if (unlocked == null) throw new ArgumentNullException(nameof(unlocked));
            RunId = runId;
            RunNumber = runNumber;
            PreviousRunId = previousRunId;
            Cash = cash;
            Rp = rp;
            StorageLevel = storageLevel;
            FactoryLevel = factoryLevel;
            var resourceIds = new List<string>();
            foreach (string id in unlocked)
            {
                if (id == null || !IsKnownId(id)) throw new ArgumentException("Unknown unlock ID.", nameof(unlocked));
                if (_unlocked.Contains(id)) throw new ArgumentException("Duplicate unlock ID.", nameof(unlocked));
                _unlocked.Add(id);
                if (catalog.Resources.ContainsKey(id)) resourceIds.Add(id);
            }
            Inventory = new GlobalInventory(resourceIds, catalog.StorageLevels[storageLevel - 1].Capacity);
            Contracts = new ContractBoard(catalog);
            Overdrive = new OverdriveState(catalog.Overdrive);
        }

        public RunCatalog Catalog => _catalog;
        public int SlotCount => _catalog.FactoryLevels[FactoryLevel - 1].SlotCount;

        /// <summary>Machines in ascending slot order.</summary>
        public IReadOnlyList<MachineInstance> Machines => _machines;

        /// <summary>Unlocked IDs in unlock order.</summary>
        public IReadOnlyList<string> Unlocked => _unlocked;

        /// <summary>Auto-sell resource IDs in ordinal order.</summary>
        public IReadOnlyList<string> AutoSell => _autoSell;

        public bool IsUnlocked(string id) => id != null && _unlocked.Contains(id);
        public bool IsAutoSell(string resourceId) => resourceId != null && _autoSell.Contains(resourceId);
        public long ResearchLevel(string researchId) => _researchLevels.Get(researchId);
        public IdCount[] ResearchLevels() => _researchLevels.ToArray();

        public MachineInstance FindMachine(Guid instanceId)
        {
            for (int i = 0; i < _machines.Count; i++)
                if (_machines[i].InstanceId == instanceId) return _machines[i];
            return null;
        }

        public MachineInstance MachineAtSlot(int slotIndex)
        {
            for (int i = 0; i < _machines.Count; i++)
                if (_machines[i].SlotIndex == slotIndex) return _machines[i];
            return null;
        }

        /// <summary>Unlocks a known ID (monotonic; idempotent). A resource ID also becomes allowed in the inventory.</summary>
        internal RunStateResult TryUnlock(string id)
        {
            if (id == null || !IsKnownId(id)) return RunStateResult.InvalidArgument;
            if (_unlocked.Contains(id)) return RunStateResult.Ok;
            _unlocked.Add(id);
            if (_catalog.Resources.ContainsKey(id)) Inventory.Allow(id);
            return RunStateResult.Ok;
        }

        public RunStateResult TrySetAutoSell(string resourceId, bool enabled)
        {
            if (resourceId == null || !_catalog.Resources.ContainsKey(resourceId) || !_unlocked.Contains(resourceId))
                return RunStateResult.ResourceLocked;
            int at = 0;
            while (at < _autoSell.Count && string.CompareOrdinal(_autoSell[at], resourceId) < 0) at++;
            bool present = at < _autoSell.Count && string.Equals(_autoSell[at], resourceId, StringComparison.Ordinal);
            if (enabled && !present) _autoSell.Insert(at, resourceId);
            else if (!enabled && present) _autoSell.RemoveAt(at);
            return RunStateResult.Ok;
        }

        internal RunStateResult TryAddCash(long amount)
        {
            if (amount < 0) return RunStateResult.InvalidArgument;
            if (!CheckedMath.TryAdd(Cash, amount, out long next)) return RunStateResult.ValueLimit;
            Cash = next;
            return RunStateResult.Ok;
        }

        internal RunStateResult TrySpendCash(long amount)
        {
            if (amount < 0) return RunStateResult.InvalidArgument;
            if (Cash < amount) return RunStateResult.CashInsufficient;
            Cash -= amount;
            return RunStateResult.Ok;
        }

        internal RunStateResult TryAddRp(long amount)
        {
            if (amount < 0) return RunStateResult.InvalidArgument;
            if (!CheckedMath.TryAdd(Rp, amount, out long next)) return RunStateResult.ValueLimit;
            Rp = next;
            return RunStateResult.Ok;
        }

        /// <summary>Validates adding a machine without changing anything.</summary>
        private RunStateResult CheckAddMachine(MachineInstance machine)
        {
            if (machine == null || !_catalog.Machines.TryGetValue(machine.MachineId, out MachineDef def))
                return RunStateResult.InvalidArgument;
            // Level and MaxLevel are checked against the catalog definition. Whether the type is unlocked is an
            // application rule (a loaded state lists its own unlocks).
            if (machine.MaxLevel != def.MaxLevel || machine.Level > def.MaxLevel) return RunStateResult.InvalidArgument;
            if (machine.SlotIndex >= SlotCount) return RunStateResult.SlotUnavailable;
            if (MachineAtSlot(machine.SlotIndex) != null) return RunStateResult.SlotOccupied;
            if (FindMachine(machine.InstanceId) != null) return RunStateResult.DuplicateInstance;
            return RunStateResult.Ok;
        }

        private void CommitAddMachine(MachineInstance machine)
        {
            int at = 0;
            while (at < _machines.Count && _machines[at].SlotIndex < machine.SlotIndex) at++;
            Priority.TryAppend(machine.InstanceId);
            _machines.Insert(at, machine);
        }

        /// <summary>Adds a machine without charging (load path): valid catalog type and level, free slot within SlotCount, unique instance ID; appended to the priority.</summary>
        public RunStateResult TryAddMachine(MachineInstance machine)
        {
            RunStateResult check = CheckAddMachine(machine);
            if (check != RunStateResult.Ok) return check;
            CommitAddMachine(machine);
            return RunStateResult.Ok;
        }

        /// <summary>Build: charges <paramref name="cost"/> once and adds the machine (its InvestedCash must equal the cost), atomically.</summary>
        internal RunStateResult TryBuildMachine(MachineInstance machine, long cost)
        {
            RunStateResult check = CheckAddMachine(machine);
            if (check != RunStateResult.Ok) return check;
            if (cost < 0 || machine.InvestedCash != cost) return RunStateResult.InvalidArgument;
            if (Cash < cost) return RunStateResult.CashInsufficient;
            Cash -= cost;
            CommitAddMachine(machine);
            return RunStateResult.Ok;
        }

        /// <summary>Upgrade: charges the cost, levels the machine and rescales its progress, atomically.</summary>
        internal RunStateResult TryUpgradeMachine(Guid instanceId, long cost, long newCycleDurationTicks)
        {
            MachineInstance machine = FindMachine(instanceId);
            if (machine == null) return RunStateResult.UnknownInstance;
            if (cost < 0) return RunStateResult.InvalidArgument;
            RunStateResult check = machine.CheckUpgrade(cost, newCycleDurationTicks);
            if (check != RunStateResult.Ok) return check;
            if (Cash < cost) return RunStateResult.CashInsufficient;
            Cash -= cost;
            machine.TryUpgrade(cost, newCycleDurationTicks);
            return RunStateResult.Ok;
        }

        /// <summary>
        /// Removes a machine, frees its slot, closes its priority position and refunds
        /// <c>floor(investedCash x RemovalRefundPpm / 10^6)</c>, atomically (a cash overflow rejects everything).
        /// </summary>
        internal RunStateResult TryRemoveMachine(Guid instanceId, out long refund)
        {
            refund = 0;
            for (int i = 0; i < _machines.Count; i++)
            {
                if (_machines[i].InstanceId != instanceId) continue;
                if (!MulDiv.MulDivFloor(_machines[i].InvestedCash, _catalog.RunStart.RemovalRefundPpm, 1_000_000, out long amount) ||
                    !CheckedMath.TryAdd(Cash, amount, out long cash))
                    return RunStateResult.ValueLimit;
                Priority.TryRemove(instanceId);
                _machines.RemoveAt(i);
                Cash = cash;
                refund = amount;
                return RunStateResult.Ok;
            }
            return RunStateResult.UnknownInstance;
        }

        // ---- manual mining (T358, D-O, D-P) -------------------------------------------------------------------
        // The rate counter and the milli-remainder are transient command bookkeeping: they are never persisted, never read by the
        // simulator, and are fresh on every new or loaded RunState (a load grants at most one extra second of the cap, MIN-7).

        /// <summary>The run-timeline second (<c>TimelineUtcMs / 1000</c>) the counter belongs to; -1 before the first counted activation.</summary>
        internal long MiningTimelineSecond { get; private set; } = -1;

        /// <summary>The activations counted in <see cref="MiningTimelineSecond"/> (the rate cap uses it).</summary>
        internal long MiningCountedInSecond { get; private set; }

        /// <summary>The milli-Ore that has not yet moved a whole Ore into stock, 0 ... 999.</summary>
        internal long MiningRemainderMilli { get; private set; }

        /// <summary>
        /// Mining: adds <paramref name="ore"/> to the stock of <paramref name="resourceId"/> and replaces the counter and the remainder, in
        /// one step. Nothing changes (counter included) when the stock add is rejected.
        /// </summary>
        internal RunStateResult TryMine(string resourceId, long ore, long timelineSecond, long countedInSecond, long remainderMilli)
        {
            if (timelineSecond < 0 || countedInSecond < 0 || remainderMilli < 0 || remainderMilli > 999) return RunStateResult.InvalidArgument;
            RunStateResult result = Inventory.TryMine(resourceId, ore);
            if (result != RunStateResult.Ok) return result;
            MiningTimelineSecond = timelineSecond;
            MiningCountedInSecond = countedInSecond;
            MiningRemainderMilli = remainderMilli;
            return RunStateResult.Ok;
        }

        /// <summary>Sale: removes stock and credits Cash in one step; nothing changes when either step would fail.</summary>
        internal RunStateResult TrySell(string resourceId, long units, long cashCredit)
        {
            if (cashCredit < 0) return RunStateResult.InvalidArgument;
            RunStateResult check = Inventory.CheckRemove(resourceId, units);
            if (check != RunStateResult.Ok) return check;
            if (!CheckedMath.TryAdd(Cash, cashCredit, out long cash)) return RunStateResult.ValueLimit;
            Inventory.TrySell(resourceId, units);
            Cash = cash;
            return RunStateResult.Ok;
        }

        /// <summary>
        /// Delivery: consumes the required units, grants the reward of <see cref="ContractRewards"/> (Cash from the offer's ratio at
        /// <paramref name="salePricePpm"/>, the sale-price modifier of the offer's resource read at this completion instant; RP from the offer's
        /// curve; the Items the ladder grants at the pre-increment run-wide index, into <paramref name="permanent"/>, required only when an
        /// Item is due) and counts the completion, all validated before anything changes. A non-<see cref="RunStateResult.Ok"/> result means no
        /// change.
        /// </summary>
        internal RunStateResult TryDeliverContract(long nowUtcMs, PermanentState permanent, long salePricePpm, out long cashReward, out long rpReward)
        {
            cashReward = 0;
            rpReward = 0;
            RunStateResult check = Contracts.CheckComplete(nowUtcMs);
            if (check != RunStateResult.Ok) return check;
            ActiveContract active = Contracts.Active;
            ContractOfferDef offer = _catalog.ContractOffers[active.OfferId];
            if (offer.Objective != "deliver") return RunStateResult.InvalidArgument; // a production objective completes by production only (T327)
            if (Contracts.CompletionCount(active.OfferId) >= ContractRewards.IndexLimit) return RunStateResult.InvalidArgument;
            check = Inventory.CheckRemove(offer.ResourceId, active.RequiredUnits);
            if (check != RunStateResult.Ok) return check;
            check = ContractRewards.CheckGrant(this, permanent, salePricePpm, out ContractRewardPlan plan);
            if (check != RunStateResult.Ok) return check;

            Inventory.TryDeliver(offer.ResourceId, active.RequiredUnits);
            ContractRewards.Grant(this, permanent, plan);
            Contracts.TryCompleteActive(nowUtcMs);
            cashReward = plan.Cash;
            rpReward = plan.Rp;
            return RunStateResult.Ok;
        }

        /// <summary>
        /// Adds an active Item effect: known Item, <c>0 &lt; End - Start &lt;= MaxTimedDurationMs</c> (the persisted instants are authoritative,
        /// owner decision 2026-10-01), one effect per conflict key.
        /// </summary>
        public RunStateResult TryAddEffect(ActiveEffect effect)
        {
            if (effect == null || !_catalog.Items.TryGetValue(effect.SourceId, out ItemDef item)) return RunStateResult.InvalidArgument;
            if (!ContractBoard.IsTimedSpan(effect.StartUtcMs, effect.EndUtcMs, false)) return RunStateResult.InvalidArgument;
            return Effects.TryAdd(effect);
        }

        /// <summary>Restores the persisted invested-Cash totals of the expansion tracks (load path).</summary>
        public RunStateResult TryRestoreInvestment(long storageInvestedCash, long factoryInvestedCash)
        {
            if (!ExactBound.Contains(storageInvestedCash) || !ExactBound.Contains(factoryInvestedCash)) return RunStateResult.ValueLimit;
            StorageInvestedCash = storageInvestedCash;
            FactoryInvestedCash = factoryInvestedCash;
            return RunStateResult.Ok;
        }

        /// <summary>Replaces the priority order; must be an exact permutation of the current machine IDs.</summary>
        public RunStateResult TryReorderPriority(IReadOnlyList<Guid> permutation) => Priority.TryReorder(permutation);

        /// <summary>Upgrades the storage to the next level at the catalog cost.</summary>
        internal RunStateResult TryUpgradeStorage()
        {
            if (StorageLevel >= _catalog.StorageLevels.Count) return RunStateResult.LevelMax;
            return TryUpgradeStorage(_catalog.StorageLevels[StorageLevel].Cost);
        }

        /// <summary>
        /// Upgrades the storage to the next level and charges <paramref name="cost"/>, the catalog cost after the
        /// <c>upgradeCost</c> modifiers (contracts/run-application.md, UpgradeStorage). The cost is added to the
        /// invested amount. Atomic.
        /// </summary>
        internal RunStateResult TryUpgradeStorage(long cost)
        {
            if (StorageLevel >= _catalog.StorageLevels.Count) return RunStateResult.LevelMax;
            if (cost < 0) return RunStateResult.InvalidArgument;
            StorageLevelDef next = _catalog.StorageLevels[StorageLevel];
            if (Cash < cost) return RunStateResult.CashInsufficient;
            if (!CheckedMath.TryAdd(StorageInvestedCash, cost, out long invested)) return RunStateResult.ValueLimit;
            RunStateResult capacity = Inventory.TrySetCapacity(next.Capacity);
            if (capacity != RunStateResult.Ok) return capacity;
            Cash -= cost;
            StorageInvestedCash = invested;
            StorageLevel++;
            return RunStateResult.Ok;
        }

        /// <summary>Upgrades the factory to the next level at the catalog cost.</summary>
        internal RunStateResult TryUpgradeFactory()
        {
            if (FactoryLevel >= _catalog.FactoryLevels.Count) return RunStateResult.LevelMax;
            return TryUpgradeFactory(_catalog.FactoryLevels[FactoryLevel].Cost);
        }

        /// <summary>
        /// Upgrades the factory to the next level and charges <paramref name="cost"/>, the catalog cost after the
        /// <c>upgradeCost</c> modifiers. The cost is added to the invested amount. Atomic.
        /// </summary>
        internal RunStateResult TryUpgradeFactory(long cost)
        {
            if (FactoryLevel >= _catalog.FactoryLevels.Count) return RunStateResult.LevelMax;
            if (cost < 0) return RunStateResult.InvalidArgument;
            if (Cash < cost) return RunStateResult.CashInsufficient;
            if (!CheckedMath.TryAdd(FactoryInvestedCash, cost, out long invested)) return RunStateResult.ValueLimit;
            Cash -= cost;
            FactoryInvestedCash = invested;
            FactoryLevel++;
            return RunStateResult.Ok;
        }

        /// <summary>Spends RP and raises a research level by one, as one step. Prerequisites are an application rule.</summary>
        internal RunStateResult TryPurchaseResearch(string researchId, long rpCost)
        {
            if (researchId == null || rpCost < 0 || !_catalog.Research.TryGetValue(researchId, out ResearchDef def))
                return RunStateResult.InvalidArgument;
            long level = _researchLevels.Get(researchId);
            if (level >= def.MaxLevel) return RunStateResult.LevelMax;
            if (Rp < rpCost) return RunStateResult.RpInsufficient;
            Rp -= rpCost;
            _researchLevels.Set(researchId, level + 1, false);
            return RunStateResult.Ok;
        }

        /// <summary>Restores a persisted research level (load path).</summary>
        public RunStateResult TrySetResearchLevel(string researchId, long level)
        {
            if (researchId == null || !_catalog.Research.TryGetValue(researchId, out ResearchDef def)) return RunStateResult.InvalidArgument;
            if (level < 0) return RunStateResult.InvalidArgument;
            if (level > def.MaxLevel) return RunStateResult.LevelMax;
            _researchLevels.Set(researchId, level, true);
            return RunStateResult.Ok;
        }

        /// <summary>
        /// Checks every invariant and returns <see cref="RunStateResult.Ok"/> or
        /// <see cref="RunStateResult.InvariantViolated"/> (for a mapper that builds state from persisted parts).
        /// </summary>
        public RunStateResult Validate()
        {
            if (Inventory.Total > Inventory.Capacity) return RunStateResult.InvariantViolated;
            long sum = 0;
            for (int i = 0; i < Inventory.Count; i++)
            {
                if (Inventory.UnitsAt(i) < 0 || !_unlocked.Contains(Inventory.IdAt(i))) return RunStateResult.InvariantViolated;
                sum += Inventory.UnitsAt(i);
            }
            if (sum != Inventory.Total || Inventory.Capacity != _catalog.StorageLevels[StorageLevel - 1].Capacity)
                return RunStateResult.InvariantViolated;
            if (Priority.Count != _machines.Count) return RunStateResult.InvariantViolated;
            for (int i = 0; i < _machines.Count; i++)
            {
                MachineInstance m = _machines[i];
                if (m.SlotIndex >= SlotCount || (i > 0 && _machines[i - 1].SlotIndex >= m.SlotIndex)) return RunStateResult.InvariantViolated;
                if (!Priority.Contains(m.InstanceId)) return RunStateResult.InvariantViolated;
                if (m.ProgressTicks < 0 || m.ProgressTicks > m.CycleDurationTicks) return RunStateResult.InvariantViolated;
                if (!_catalog.Machines.TryGetValue(m.MachineId, out MachineDef def) || m.MaxLevel != def.MaxLevel ||
                    m.Level < 1 || m.Level > def.MaxLevel)
                    return RunStateResult.InvariantViolated;
            }
            if (!ExactBound.Contains(Cash) || !ExactBound.Contains(Rp) || !ExactBound.Contains(StorageInvestedCash) ||
                !ExactBound.Contains(FactoryInvestedCash))
                return RunStateResult.InvariantViolated;
            if (!Overdrive.IsConsistent) return RunStateResult.InvariantViolated;
            for (int i = 0; i < _autoSell.Count; i++)
                if (!_catalog.Resources.ContainsKey(_autoSell[i]) || !_unlocked.Contains(_autoSell[i])) return RunStateResult.InvariantViolated;
            for (int i = 0; i < _researchLevels.Count; i++)
                if (_researchLevels.ValueAt(i) > _catalog.Research[_researchLevels.KeyAt(i)].MaxLevel) return RunStateResult.InvariantViolated;
            for (int i = 0; i < Effects.Count; i++)
            {
                ActiveEffect e = Effects.Effects[i];
                if (!_catalog.Items.ContainsKey(e.SourceId) || !ContractBoard.IsTimedSpan(e.StartUtcMs, e.EndUtcMs, false))
                    return RunStateResult.InvariantViolated;
            }
            return RunStateResult.Ok;
        }

        private bool IsKnownId(string id)
        {
            return string.Equals(id, OverdriveUnlockId, StringComparison.Ordinal) || _catalog.Resources.ContainsKey(id) || _catalog.Machines.ContainsKey(id) ||
                _catalog.ContractOffers.ContainsKey(id) || _catalog.Research.ContainsKey(id) || _catalog.Items.ContainsKey(id);
        }
    }
}
