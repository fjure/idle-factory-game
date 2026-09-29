using System;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;

namespace IdleFactory.Run.Domain
{
    /// <summary>
    /// A built machine. Invariants: <c>1 &lt;= Level &lt;= MaxLevel</c>, <c>CycleDurationTicks &gt;= 1</c> and
    /// <c>0 &lt;= ProgressTicks &lt;= CycleDurationTicks</c> (equality means waiting). There are no buffers.
    /// Every mutator validates first and is all-or-nothing. Slot uniqueness and priority membership belong to
    /// <see cref="RunState"/>.
    /// </summary>
    public sealed class MachineInstance
    {
        public Guid InstanceId { get; }
        public string MachineId { get; }
        public int MaxLevel { get; }
        public int SlotIndex { get; }
        public int Level { get; private set; }
        public bool IsActive { get; private set; }
        public long ProgressTicks { get; private set; }
        public long CycleDurationTicks { get; private set; }
        public long InvestedCash { get; private set; }

        public MachineInstance(Guid instanceId, MachineDef machine, int slotIndex, int level, bool isActive,
            long progressTicks, long cycleDurationTicks, long investedCash)
        {
            if (instanceId == Guid.Empty) throw new ArgumentException("Instance ID is required.", nameof(instanceId));
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            if (slotIndex < 0) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            if (level < 1 || level > machine.MaxLevel) throw new ArgumentOutOfRangeException(nameof(level));
            if (cycleDurationTicks < 1 || cycleDurationTicks > ExactBound.Value) throw new ArgumentOutOfRangeException(nameof(cycleDurationTicks));
            if (progressTicks < 0 || progressTicks > cycleDurationTicks) throw new ArgumentOutOfRangeException(nameof(progressTicks));
            if (!ExactBound.Contains(investedCash)) throw new ArgumentOutOfRangeException(nameof(investedCash));
            InstanceId = instanceId;
            MachineId = machine.Id;
            MaxLevel = machine.MaxLevel;
            SlotIndex = slotIndex;
            Level = level;
            IsActive = isActive;
            ProgressTicks = progressTicks;
            CycleDurationTicks = cycleDurationTicks;
            InvestedCash = investedCash;
        }

        public bool IsWaiting => ProgressTicks == CycleDurationTicks;

        public void SetActive(bool active) => IsActive = active;

        /// <summary>Sets the progress; rejected outside <c>0..CycleDurationTicks</c>.</summary>
        public RunStateResult TrySetProgress(long progressTicks)
        {
            if (progressTicks < 0 || progressTicks > CycleDurationTicks) return RunStateResult.InvalidArgument;
            ProgressTicks = progressTicks;
            return RunStateResult.Ok;
        }

        /// <summary>Starts a new cycle after a completion: progress 0 with the given (re-derived) duration.</summary>
        public RunStateResult TryStartCycle(long cycleDurationTicks)
        {
            if (cycleDurationTicks < 1 || cycleDurationTicks > ExactBound.Value) return RunStateResult.InvalidArgument;
            ProgressTicks = 0;
            CycleDurationTicks = cycleDurationTicks;
            return RunStateResult.Ok;
        }

        /// <summary>R03 rescale to a new cycle duration, preserving the completed fraction.</summary>
        public RunStateResult TryRescale(long newCycleDurationTicks)
        {
            if (!SpeedComposer.TryRescaleProgress(ProgressTicks, CycleDurationTicks, newCycleDurationTicks, out long progress))
                return RunStateResult.ValueLimit;
            ProgressTicks = progress;
            CycleDurationTicks = newCycleDurationTicks;
            return RunStateResult.Ok;
        }

        /// <summary>Level + 1, cost added to the invested amount, and the R03 rescale, as one step.</summary>
        public RunStateResult TryUpgrade(long cost, long newCycleDurationTicks)
        {
            if (Level >= MaxLevel) return RunStateResult.LevelMax;
            if (!CheckedMath.TryAdd(InvestedCash, cost, out long invested)) return RunStateResult.ValueLimit;
            if (!SpeedComposer.TryRescaleProgress(ProgressTicks, CycleDurationTicks, newCycleDurationTicks, out long progress))
                return RunStateResult.ValueLimit;
            InvestedCash = invested;
            ProgressTicks = progress;
            CycleDurationTicks = newCycleDurationTicks;
            Level++;
            return RunStateResult.Ok;
        }
    }
}
