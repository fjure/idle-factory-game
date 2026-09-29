using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IdleFactory.Core.Numerics;

namespace IdleFactory.Run.Content
{
    public sealed class SpeedMilestone
    {
        public int Level { get; }
        public long SpeedPpm { get; }
        public SpeedMilestone(int level, long speedPpm)
        {
            if (level < 2) throw new ArgumentOutOfRangeException(nameof(level));
            if (speedPpm <= Curve.Ppm || speedPpm > ExactBound.Value) throw new ArgumentOutOfRangeException(nameof(speedPpm));
            Level = level; SpeedPpm = speedPpm;
        }
    }

    public sealed class MachineDef
    {
        public string Id { get; }
        public string Label { get; }
        public string PresentationKey { get; }
        public string RecipeId { get; }
        public int MaxLevel { get; }
        public Curve Cost { get; }
        public long SpeedGrowthPpm { get; }
        public IReadOnlyList<SpeedMilestone> Milestones { get; }
        public IReadOnlyList<long> CostTable { get; }
        public IReadOnlyList<long> SpeedTable { get; }
        public UnlockCondition Unlock { get; }

        public MachineDef(string id, string label, string presentationKey, string recipeId, int maxLevel,
            Curve cost, long speedGrowthPpm, IEnumerable<SpeedMilestone> milestones, UnlockCondition unlock)
        {
            Id = ContentGuard.Id(id, nameof(id));
            Label = ContentGuard.Text(label, nameof(label));
            PresentationKey = ContentGuard.Text(presentationKey, nameof(presentationKey));
            RecipeId = ContentGuard.Id(recipeId, nameof(recipeId));
            if (maxLevel < 1 || maxLevel > 1024) throw new ArgumentOutOfRangeException(nameof(maxLevel));
            MaxLevel = maxLevel;
            Cost = cost ?? throw new ArgumentNullException(nameof(cost));
            if (speedGrowthPpm < Curve.Ppm || speedGrowthPpm > ExactBound.Value) throw new ArgumentOutOfRangeException(nameof(speedGrowthPpm));
            SpeedGrowthPpm = speedGrowthPpm;
            Unlock = unlock ?? throw new ArgumentNullException(nameof(unlock));
            var milestonesList = ContentGuard.List(milestones, nameof(milestones));
            int previous = 1;
            foreach (var milestone in milestonesList)
            {
                if (milestone.Level <= previous || milestone.Level > maxLevel)
                    throw new ArgumentException("machine.milestone.level-invalid", nameof(milestones));
                previous = milestone.Level;
            }
            Milestones = new ReadOnlyCollection<SpeedMilestone>(milestonesList);
            CostTable = new ReadOnlyCollection<long>(cost.Precompute(maxLevel));
            var speeds = new long[maxLevel + 1];
            speeds[1] = Curve.Ppm;
            long baseSpeed = Curve.Ppm;
            for (int level = 2; level <= maxLevel; level++)
            {
                if (!MulDiv.MulDivFloor(baseSpeed, speedGrowthPpm, Curve.Ppm, out baseSpeed))
                    throw new ArgumentException("curve.overflow", nameof(speedGrowthPpm));
                long speed = baseSpeed;
                for (int i = 0; i < milestonesList.Count && milestonesList[i].Level <= level; i++)
                {
                    if (!MulDiv.MulDivFloor(speed, milestonesList[i].SpeedPpm, Curve.Ppm, out speed))
                        throw new ArgumentException("curve.overflow", nameof(milestones));
                }
                speeds[level] = speed;
            }
            SpeedTable = new ReadOnlyCollection<long>(speeds);
        }
    }
}
