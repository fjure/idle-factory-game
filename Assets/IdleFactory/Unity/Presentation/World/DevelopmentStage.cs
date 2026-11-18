using System;
using IdleFactory.Run.Application;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// How developed the factory looks (T540, FR-036, research R15, data-model.md <c>DevelopmentStageValue</c>): the stage (0 Early, 1 Partial, 2 Established)
    /// and two secondary density bins, each 0 to 2. A presentation classification, derived only from the current run's tracked progress, never saved.
    /// </summary>
    public readonly struct DevelopmentStageValue : IEquatable<DevelopmentStageValue>
    {
        public DevelopmentStageValue(int stage, int activityDensity, int levelDensity)
        {
            Stage = stage;
            ActivityDensity = activityDensity;
            LevelDensity = levelDensity;
        }

        /// <summary>0 Early (Open deck), 1 Partial (Walled yard), 2 Established (Gantry line).</summary>
        public int Stage { get; }

        /// <summary>Active machines over unlocked locations, in 3 bins (0 to 2).</summary>
        public int ActivityDensity { get; }

        /// <summary>Mean machine level over the maximum machine level, in 3 bins (0 to 2).</summary>
        public int LevelDensity { get; }

        /// <summary>The two secondary bins together (0 to 4): how much extra prop density the stage gets.</summary>
        public int DensityBin => ActivityDensity + LevelDensity;

        public bool Equals(DevelopmentStageValue other) =>
            Stage == other.Stage && ActivityDensity == other.ActivityDensity && LevelDensity == other.LevelDensity;

        public override bool Equals(object obj) => obj is DevelopmentStageValue other && Equals(other);

        public override int GetHashCode() => Stage * 9 + ActivityDensity * 3 + LevelDensity;

        public override string ToString() => "stage " + Stage + " (activity " + ActivityDensity + ", level " + LevelDensity + ")";
    }

    /// <summary>
    /// The development stage rule (R15, [G-4]): <c>t &lt;= 0 gives Early; otherwise clamp(floor(3 * u / t), 0, 2)</c>, where <c>u</c> is the number of unlocked
    /// locations at the current factory level and <c>t</c> the number of locations at the maximum factory level. With 12 locations the steps fall at 4 and 8
    /// [AD-12]. Pure integer arithmetic, so the result is deterministic. <b>The inputs are the current run's own values; the prestige count is not an input</b>
    /// (a new run starts again from its own factory level, owner decision, FR-036). Nothing here allocates.
    /// </summary>
    public static class DevelopmentStage
    {
        public const int StageCount = 3;
        public const int BinCount = 3;

        /// <summary>The stage of <paramref name="unlocked"/> of <paramref name="total"/> locations.</summary>
        public static int StageOf(int unlocked, int total)
        {
            if (total <= 0)
            {
                return 0;
            }

            // 3 * u cannot overflow for any realistic location count; widen anyway so a huge test value stays exact.
            var stage = (int)(3L * unlocked / total);
            return stage < 0 ? 0 : stage > StageCount - 1 ? StageCount - 1 : stage;
        }

        /// <summary>A share <c>numerator / denominator</c> in 3 bins: <c>clamp(floor(3 * numerator / denominator), 0, 2)</c>, and 0 when the denominator is not positive.</summary>
        public static int BinOf(long numerator, long denominator)
        {
            if (denominator <= 0 || numerator <= 0)
            {
                return 0;
            }

            var bin = 3L * numerator / denominator;
            return bin > BinCount - 1 ? BinCount - 1 : (int)bin;
        }

        /// <summary>The value for explicit counts: locations, active machines and the sum of machine levels over the sum of their maximum levels.</summary>
        public static DevelopmentStageValue Evaluate(int unlockedLocations, int totalLocations, int activeMachines, long levelSum, long maxLevelSum) =>
            new DevelopmentStageValue(
                StageOf(unlockedLocations, totalLocations),
                BinOf(activeMachines, unlockedLocations),
                BinOf(levelSum, maxLevelSum));

        /// <summary>
        /// The value of a factory snapshot: <c>u</c> is <see cref="RunFactorySnapshot.SlotCount"/> (the unlocked locations of the current factory level) and
        /// <c>t</c> the location count of the snapshot (the slots at the maximum level). The snapshot carries no prestige count and none is read.
        /// </summary>
        public static DevelopmentStageValue Evaluate(RunFactorySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var machines = snapshot.Machines;
            var active = 0;
            long levels = 0;
            long maxLevels = 0;
            for (var i = 0; i < machines.Count; i++)
            {
                var machine = machines[i];
                active += machine.IsActive ? 1 : 0;
                levels += machine.Level;
                maxLevels += machine.MaxLevel;
            }

            return Evaluate(snapshot.SlotCount, snapshot.Slots.Count, active, levels, maxLevels);
        }
    }
}
