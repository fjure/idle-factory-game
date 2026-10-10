using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Run.Application
{
    // -------------------------------------------------------------------------------------------------------
    // RunFactorySnapshot (GD-2)
    // -------------------------------------------------------------------------------------------------------

    public enum SlotStatus
    {
        Empty = 0,
        Occupied = 1,

        /// <summary>Beyond the current factory level; <see cref="SlotSnapshot.UnlocksAtFactoryLevel"/> names the level that opens it.</summary>
        Locked = 2,
    }

    public sealed class SlotSnapshot
    {
        public int Index { get; }
        public SlotStatus Status { get; }

        /// <summary>Index into <see cref="RunFactorySnapshot.Machines"/> for an occupied slot, otherwise -1.</summary>
        public int MachineIndex { get; }

        /// <summary>The factory level that opens a locked slot, otherwise 0.</summary>
        public int UnlocksAtFactoryLevel { get; }

        internal SlotSnapshot(int index, SlotStatus status, int machineIndex, int unlocksAtFactoryLevel)
        {
            Index = index;
            Status = status;
            MachineIndex = machineIndex;
            UnlocksAtFactoryLevel = unlocksAtFactoryLevel;
        }
    }

    /// <summary>The result an upgrade would have (FR-009, US1 scenario 5), shown before the tap.</summary>
    public sealed class UpgradePreview
    {
        /// <summary>False at the maximum level.</summary>
        public bool Available { get; }
        public int NewLevel { get; }
        public long Cost { get; }
        public bool Affordable { get; }

        /// <summary><see cref="RunReasons.LevelMax"/>, <see cref="RunReasons.CashInsufficient"/> or null.</summary>
        public string RejectionReason { get; }

        public MachineRates NewRates { get; }
        public bool ReachesMilestone { get; }
        public int MilestoneLevel { get; }
        public long MilestoneMultiplierPpm { get; }

        internal UpgradePreview(bool available, int newLevel, long cost, bool affordable, string rejectionReason,
            MachineRates newRates, bool reachesMilestone, int milestoneLevel, long milestoneMultiplierPpm)
        {
            Available = available;
            NewLevel = newLevel;
            Cost = cost;
            Affordable = affordable;
            RejectionReason = rejectionReason;
            NewRates = newRates;
            ReachesMilestone = reachesMilestone;
            MilestoneLevel = milestoneLevel;
            MilestoneMultiplierPpm = milestoneMultiplierPpm;
        }
    }

    public sealed class MachineSnapshot
    {
        public Guid InstanceId { get; }
        public string MachineId { get; }
        public string Label { get; }
        public int TypeIndex { get; }
        public int SlotIndex { get; }
        public int Level { get; }
        public int MaxLevel { get; }
        public bool IsActive { get; }
        public MachinePhase Phase { get; }
        public MachineBlocker Blocker { get; }
        public long ProgressPpm { get; }

        /// <summary>Position in the priority order, 0 first.</summary>
        public int PriorityPosition { get; }

        /// <summary>The Cash paid for the machine and its upgrades.</summary>
        public long InvestedCash { get; }

        /// <summary>What Remove would refund now: <c>floor(investedCash x refundPpm / 10^6)</c>.</summary>
        public long RefundOnRemove { get; }

        /// <summary>The Factory Value change of removing it, <c>refund - investedCash</c> (never positive; ECO-7).</summary>
        public long FactoryValueDeltaOnRemove { get; }

        /// <summary>The steady-state flows now, or null when the composed speed overflowed.</summary>
        public MachineRates Rates { get; }

        public UpgradePreview NextUpgrade { get; }

        internal MachineSnapshot(Guid instanceId, MachineDef def, int typeIndex, MachineInstance machine, MachineStateView view,
            int priorityPosition, long refund, MachineRates rates, UpgradePreview nextUpgrade)
        {
            InstanceId = instanceId;
            MachineId = def.Id;
            Label = def.Label;
            TypeIndex = typeIndex;
            SlotIndex = machine.SlotIndex;
            Level = machine.Level;
            MaxLevel = machine.MaxLevel;
            IsActive = machine.IsActive;
            Phase = view.Phase;
            Blocker = view.Blocker;
            ProgressPpm = view.ProgressPpm;
            PriorityPosition = priorityPosition;
            InvestedCash = machine.InvestedCash;
            RefundOnRemove = refund;
            FactoryValueDeltaOnRemove = refund - machine.InvestedCash;
            Rates = rates;
            NextUpgrade = nextUpgrade;
        }
    }

    /// <summary>A machine type as the build sheet shows it: unlocked or not, its cost, and what it makes at level 1.</summary>
    public sealed class MachineTypeSnapshot
    {
        public string MachineId { get; }
        public string Label { get; }
        public int TypeIndex { get; }
        public bool Unlocked { get; }

        /// <summary>The unlock condition kind (<c>always</c>, <c>researchPurchased</c>, <c>factoryLevelAtLeast</c>, <c>resourceProduced</c>) for a locked type's copy.</summary>
        public string UnlockKind { get; }
        public string UnlockId { get; }
        public int UnlockLevel { get; }

        /// <summary>The level-1 cost after the <c>upgradeCost</c> modifiers.</summary>
        public long BuildCost { get; }
        public bool Affordable { get; }

        /// <summary><see cref="RunReasons.MachineLocked"/>, <see cref="RunReasons.CashInsufficient"/> or null (slot rules aside).</summary>
        public string RejectionReason { get; }

        public MachineRates Level1Rates { get; }

        internal MachineTypeSnapshot(MachineDef def, int typeIndex, bool unlocked, long buildCost, bool affordable,
            string rejectionReason, MachineRates rates)
        {
            MachineId = def.Id;
            Label = def.Label;
            TypeIndex = typeIndex;
            Unlocked = unlocked;
            UnlockKind = def.Unlock.Kind;
            UnlockId = def.Unlock.Id;
            UnlockLevel = def.Unlock.Level;
            BuildCost = buildCost;
            Affordable = affordable;
            RejectionReason = rejectionReason;
            Level1Rates = rates;
        }
    }

    /// <summary>One expansion track (storage or factory): its level, what the next level gives and what it costs.</summary>
    public sealed class ExpansionSnapshot
    {
        public int Level { get; }
        public int MaxLevel { get; }

        /// <summary>Storage capacity or slot count now.</summary>
        public long CurrentValue { get; }
        public bool HasNext { get; }

        /// <summary>Capacity or slot count of the next level (0 at the maximum).</summary>
        public long NextValue { get; }

        /// <summary>The next level's cost after the <c>upgradeCost</c> modifiers (0 at the maximum).</summary>
        public long NextCost { get; }
        public bool Affordable { get; }
        public long InvestedCash { get; }

        /// <summary><see cref="RunReasons.LevelMax"/>, <see cref="RunReasons.CashInsufficient"/> or null.</summary>
        public string RejectionReason { get; }

        internal ExpansionSnapshot(int level, int maxLevel, long currentValue, bool hasNext, long nextValue, long nextCost,
            bool affordable, long investedCash, string rejectionReason)
        {
            Level = level;
            MaxLevel = maxLevel;
            CurrentValue = currentValue;
            HasNext = hasNext;
            NextValue = nextValue;
            NextCost = nextCost;
            Affordable = affordable;
            InvestedCash = investedCash;
            RejectionReason = rejectionReason;
        }
    }

    /// <summary>
    /// Slots, machines, costs, next-upgrade results and unlocks (contracts/run-application.md; GD-2: the
    /// <c>Run</c> prefix avoids the legacy <c>FactorySnapshot</c> until R16). Immutable; built on demand.
    /// </summary>
    public sealed class RunFactorySnapshot
    {
        public long StructureRevision { get; }
        public long QuantityRevision { get; }
        public long Cash { get; }
        public int FactoryLevel { get; }
        public int SlotCount { get; }
        public IReadOnlyList<SlotSnapshot> Slots { get; }

        /// <summary>The machines in slot order.</summary>
        public IReadOnlyList<MachineSnapshot> Machines { get; }

        /// <summary>The priority order, highest first.</summary>
        public IReadOnlyList<Guid> Priority { get; }

        /// <summary>Every machine type of the catalog, in the order of <see cref="RunSession.MachineTypeId"/>.</summary>
        public IReadOnlyList<MachineTypeSnapshot> MachineTypes { get; }
        public ExpansionSnapshot Storage { get; }
        public ExpansionSnapshot Factory { get; }

        internal RunFactorySnapshot(long structureRevision, long quantityRevision, long cash, int factoryLevel, int slotCount,
            SlotSnapshot[] slots, MachineSnapshot[] machines, Guid[] priority, MachineTypeSnapshot[] types,
            ExpansionSnapshot storage, ExpansionSnapshot factory)
        {
            StructureRevision = structureRevision;
            QuantityRevision = quantityRevision;
            Cash = cash;
            FactoryLevel = factoryLevel;
            SlotCount = slotCount;
            Slots = new ReadOnlyCollection<SlotSnapshot>(slots);
            Machines = new ReadOnlyCollection<MachineSnapshot>(machines);
            Priority = new ReadOnlyCollection<Guid>(priority);
            MachineTypes = new ReadOnlyCollection<MachineTypeSnapshot>(types);
            Storage = storage;
            Factory = factory;
        }
    }

    public static partial class RunSnapshots
    {
        public static RunFactorySnapshot Factory(RunSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            RunCatalog catalog = session.Catalog;
            RunState state = session.State;
            PermanentState permanent = session.Permanent;
            var modifiers = new RunModifiers(catalog);
            bool overdrive = state.Overdrive.IsActive(session.TimelineUtcMs);

            int count = state.Machines.Count;
            var views = new MachineStateView[count];
            session.CopyMachineStates(views);

            var machines = new MachineSnapshot[count];
            for (int i = 0; i < count; i++)
            {
                MachineInstance m = state.Machines[i];
                MachineDef def = catalog.Machines[m.MachineId];
                long refund = 0;
                MulDiv.MulDivFloor(m.InvestedCash, catalog.RunStart.RemovalRefundPpm, 1_000_000, out refund);
                MachineRates rates = RunDerivation.TryDerive(catalog, modifiers, state, permanent, m, overdrive, out RunDerivation.Derived d)
                    ? RunDerivation.Rates(d)
                    : null;
                UpgradePreview next = PreviewUpgrade(catalog, modifiers, state, permanent, m, def, overdrive);
                machines[i] = new MachineSnapshot(m.InstanceId, def, TypeIndexOf(session, m.MachineId), m, views[i],
                    state.Priority.IndexOf(m.InstanceId), refund, rates, next);
            }

            int maxSlots = 0;
            for (int i = 0; i < catalog.FactoryLevels.Count; i++)
                if (catalog.FactoryLevels[i].SlotCount > maxSlots) maxSlots = catalog.FactoryLevels[i].SlotCount;
            var slots = new SlotSnapshot[maxSlots];
            for (int s = 0; s < maxSlots; s++)
            {
                if (s < state.SlotCount)
                {
                    int at = -1;
                    for (int i = 0; i < count; i++)
                        if (state.Machines[i].SlotIndex == s) at = i;
                    slots[s] = new SlotSnapshot(s, at >= 0 ? SlotStatus.Occupied : SlotStatus.Empty, at, 0);
                }
                else
                {
                    int level = 0;
                    for (int l = 0; l < catalog.FactoryLevels.Count && level == 0; l++)
                        if (catalog.FactoryLevels[l].SlotCount > s) level = catalog.FactoryLevels[l].Level;
                    slots[s] = new SlotSnapshot(s, SlotStatus.Locked, -1, level);
                }
            }

            var types = new MachineTypeSnapshot[session.MachineTypeCount];
            for (int t = 0; t < types.Length; t++)
            {
                MachineDef def = catalog.Machines[session.MachineTypeId(t)];
                bool unlocked = state.IsUnlocked(def.Id);
                long cost = 0;
                bool costOk = modifiers.TryUpgradeCost(state, permanent, def.CostTable[0], def.Id, out cost);
                var probe = new MachineInstance(new Guid(1, 0, 0, new byte[8]), def, 0, 1, true, 0,
                    catalog.Recipes[def.RecipeId].DurationTicks, 0);
                MachineRates rates = RunDerivation.TryDerive(catalog, modifiers, state, permanent, probe, overdrive, out RunDerivation.Derived d)
                    ? RunDerivation.Rates(d)
                    : null;
                string reason = !unlocked ? RunReasons.MachineLocked : !costOk ? RunReasons.ValueLimit : state.Cash < cost ? RunReasons.CashInsufficient : null;
                types[t] = new MachineTypeSnapshot(def, t, unlocked, cost, costOk && state.Cash >= cost, reason, rates);
            }

            ExpansionSnapshot storage = StorageTrack(catalog, modifiers, state, permanent);
            ExpansionSnapshot factory = FactoryTrack(catalog, modifiers, state, permanent);
            return new RunFactorySnapshot(session.StructureRevision, session.QuantityRevision, state.Cash, state.FactoryLevel,
                state.SlotCount, slots, machines, state.Priority.ToArray(), types, storage, factory);
        }

        private static int TypeIndexOf(RunSession session, string machineId)
        {
            for (int i = 0; i < session.MachineTypeCount; i++)
                if (string.Equals(session.MachineTypeId(i), machineId, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static UpgradePreview PreviewUpgrade(RunCatalog catalog, RunModifiers modifiers, RunState state,
            PermanentState permanent, MachineInstance m, MachineDef def, bool overdrive)
        {
            if (m.Level >= m.MaxLevel)
                return new UpgradePreview(false, m.Level, 0, false, RunReasons.LevelMax, null, false, 0, 0);
            int newLevel = m.Level + 1;
            bool costOk = modifiers.TryUpgradeCost(state, permanent, def.CostTable[m.Level], def.Id, out long cost);
            var next = new MachineInstance(m.InstanceId, def, m.SlotIndex, newLevel, m.IsActive, 0,
                catalog.Recipes[def.RecipeId].DurationTicks, 0);
            MachineRates rates = RunDerivation.TryDerive(catalog, modifiers, state, permanent, next, overdrive, out RunDerivation.Derived d)
                ? RunDerivation.Rates(d)
                : null;
            SpeedMilestone milestone = RunDerivation.MilestoneAt(def, newLevel);
            bool affordable = costOk && state.Cash >= cost;
            string reason = !costOk ? RunReasons.ValueLimit : state.Cash < cost ? RunReasons.CashInsufficient : null;
            return new UpgradePreview(true, newLevel, cost, affordable, reason, rates, milestone != null,
                milestone?.Level ?? 0, milestone?.SpeedPpm ?? 0);
        }

        private static ExpansionSnapshot StorageTrack(RunCatalog catalog, RunModifiers modifiers, RunState state, PermanentState permanent)
        {
            int level = state.StorageLevel;
            int max = catalog.StorageLevels.Count;
            long current = catalog.StorageLevels[level - 1].Capacity;
            if (level >= max)
                return new ExpansionSnapshot(level, max, current, false, 0, 0, false, state.StorageInvestedCash, RunReasons.LevelMax);
            StorageLevelDef next = catalog.StorageLevels[level];
            bool ok = modifiers.TryUpgradeCost(state, permanent, next.Cost, null, out long cost);
            return new ExpansionSnapshot(level, max, current, true, next.Capacity, cost, ok && state.Cash >= cost, state.StorageInvestedCash,
                !ok ? RunReasons.ValueLimit : state.Cash < cost ? RunReasons.CashInsufficient : null);
        }

        private static ExpansionSnapshot FactoryTrack(RunCatalog catalog, RunModifiers modifiers, RunState state, PermanentState permanent)
        {
            int level = state.FactoryLevel;
            int max = catalog.FactoryLevels.Count;
            long current = catalog.FactoryLevels[level - 1].SlotCount;
            if (level >= max)
                return new ExpansionSnapshot(level, max, current, false, 0, 0, false, state.FactoryInvestedCash, RunReasons.LevelMax);
            FactoryLevelDef next = catalog.FactoryLevels[level];
            bool ok = modifiers.TryUpgradeCost(state, permanent, next.Cost, null, out long cost);
            return new ExpansionSnapshot(level, max, current, true, next.SlotCount, cost, ok && state.Cash >= cost, state.FactoryInvestedCash,
                !ok ? RunReasons.ValueLimit : state.Cash < cost ? RunReasons.CashInsufficient : null);
        }
    }
}
