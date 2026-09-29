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
        public RunStateResult TryUnlock(string id)
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

        public RunStateResult TryAddCash(long amount)
        {
            if (amount < 0) return RunStateResult.InvalidArgument;
            if (!CheckedMath.TryAdd(Cash, amount, out long next)) return RunStateResult.ValueLimit;
            Cash = next;
            return RunStateResult.Ok;
        }

        public RunStateResult TrySpendCash(long amount)
        {
            if (amount < 0) return RunStateResult.InvalidArgument;
            if (Cash < amount) return RunStateResult.CashInsufficient;
            Cash -= amount;
            return RunStateResult.Ok;
        }

        public RunStateResult TryAddRp(long amount)
        {
            if (amount < 0) return RunStateResult.InvalidArgument;
            if (!CheckedMath.TryAdd(Rp, amount, out long next)) return RunStateResult.ValueLimit;
            Rp = next;
            return RunStateResult.Ok;
        }

        /// <summary>Adds a machine: known type, slot within SlotCount and free, unique instance ID; appended to the priority.</summary>
        public RunStateResult TryAddMachine(MachineInstance machine)
        {
            if (machine == null || !_catalog.Machines.ContainsKey(machine.MachineId)) return RunStateResult.InvalidArgument;
            if (machine.SlotIndex >= SlotCount) return RunStateResult.SlotUnavailable;
            if (MachineAtSlot(machine.SlotIndex) != null) return RunStateResult.SlotOccupied;
            if (FindMachine(machine.InstanceId) != null) return RunStateResult.DuplicateInstance;
            int at = 0;
            while (at < _machines.Count && _machines[at].SlotIndex < machine.SlotIndex) at++;
            RunStateResult appended = Priority.TryAppend(machine.InstanceId);
            if (appended != RunStateResult.Ok) return appended;
            _machines.Insert(at, machine);
            return RunStateResult.Ok;
        }

        /// <summary>Removes a machine, frees its slot and closes its priority position; the caller handles the refund.</summary>
        public RunStateResult TryRemoveMachine(Guid instanceId)
        {
            for (int i = 0; i < _machines.Count; i++)
            {
                if (_machines[i].InstanceId != instanceId) continue;
                RunStateResult removed = Priority.TryRemove(instanceId);
                if (removed != RunStateResult.Ok) return removed;
                _machines.RemoveAt(i);
                return RunStateResult.Ok;
            }
            return RunStateResult.UnknownInstance;
        }

        /// <summary>Replaces the priority order; must be an exact permutation of the current machine IDs.</summary>
        public RunStateResult TryReorderPriority(IReadOnlyList<Guid> permutation) => Priority.TryReorder(permutation);

        public RunStateResult TryUpgradeStorage()
        {
            if (StorageLevel >= _catalog.StorageLevels.Count) return RunStateResult.LevelMax;
            StorageLevelDef next = _catalog.StorageLevels[StorageLevel];
            if (Cash < next.Cost) return RunStateResult.CashInsufficient;
            if (!CheckedMath.TryAdd(StorageInvestedCash, next.Cost, out long invested)) return RunStateResult.ValueLimit;
            RunStateResult capacity = Inventory.TrySetCapacity(next.Capacity);
            if (capacity != RunStateResult.Ok) return capacity;
            Cash -= next.Cost;
            StorageInvestedCash = invested;
            StorageLevel++;
            return RunStateResult.Ok;
        }

        public RunStateResult TryUpgradeFactory()
        {
            if (FactoryLevel >= _catalog.FactoryLevels.Count) return RunStateResult.LevelMax;
            FactoryLevelDef next = _catalog.FactoryLevels[FactoryLevel];
            if (Cash < next.Cost) return RunStateResult.CashInsufficient;
            if (!CheckedMath.TryAdd(FactoryInvestedCash, next.Cost, out long invested)) return RunStateResult.ValueLimit;
            Cash -= next.Cost;
            FactoryInvestedCash = invested;
            FactoryLevel++;
            return RunStateResult.Ok;
        }

        /// <summary>Spends RP and raises a research level by one, as one step. Prerequisites are an application rule.</summary>
        public RunStateResult TryPurchaseResearch(string researchId, long rpCost)
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
            if (level < 0 || level > def.MaxLevel) return RunStateResult.LevelMax;
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
            }
            return RunStateResult.Ok;
        }

        private bool IsKnownId(string id)
        {
            return _catalog.Resources.ContainsKey(id) || _catalog.Machines.ContainsKey(id) ||
                _catalog.ContractOffers.ContainsKey(id) || _catalog.Research.ContainsKey(id) || _catalog.Items.ContainsKey(id);
        }
    }
}
