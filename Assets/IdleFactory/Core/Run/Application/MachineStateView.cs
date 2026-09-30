namespace IdleFactory.Run.Application
{
    /// <summary>What a machine is doing at the timeline instant (contract read model).</summary>
    public enum MachinePhase
    {
        /// <summary>Active, progress below the cycle, and at least one recipe unit fits.</summary>
        Running = 0,

        /// <summary>Not progressing: inactive, or active with no unit that fits.</summary>
        Paused = 1,

        /// <summary>Progress equals the cycle duration and the exchange cannot complete (k' = 0).</summary>
        Waiting = 2,
    }

    /// <summary>Why a machine does not run (empty for a running machine).</summary>
    public enum MachineBlocker
    {
        None = 0,
        Inactive = 1,

        /// <summary>An input is short (or a resource of the recipe is not unlocked).</summary>
        MissingInput = 2,

        /// <summary>Inputs suffice but not even one recipe unit fits in the store.</summary>
        StorageFull = 3,
    }

    /// <summary>
    /// One row of the allocation-free machine read model. <see cref="MachineTypeIndex"/> is the position of the
    /// machine ID in the catalog's machine IDs sorted ordinally (<see cref="RunSession.MachineTypeId"/>).
    /// <see cref="BatchFactor"/> is the batch factor k of the current composed speed; <see cref="ProgressPpm"/> is
    /// <c>floor(progress x 10^6 / cycle)</c>, 10^6 for a waiting machine.
    /// </summary>
    public readonly struct MachineStateView
    {
        public int SlotIndex { get; }
        public int MachineTypeIndex { get; }
        public int Level { get; }
        public bool IsActive { get; }
        public MachinePhase Phase { get; }
        public MachineBlocker Blocker { get; }
        public long BatchFactor { get; }
        public long ProgressPpm { get; }

        public MachineStateView(int slotIndex, int machineTypeIndex, int level, bool isActive, MachinePhase phase,
            MachineBlocker blocker, long batchFactor, long progressPpm)
        {
            SlotIndex = slotIndex;
            MachineTypeIndex = machineTypeIndex;
            Level = level;
            IsActive = isActive;
            Phase = phase;
            Blocker = blocker;
            BatchFactor = batchFactor;
            ProgressPpm = progressPpm;
        }
    }
}
