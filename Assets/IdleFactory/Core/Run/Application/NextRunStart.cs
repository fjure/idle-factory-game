using System;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;

namespace IdleFactory.Run.Application
{
    /// <summary>
    /// What the Industry upgrades change about the start of the next run (R12 "starts again stronger"). This is the
    /// one place the formula lives: <see cref="RunSnapshots.Industry"/> reports it and the prestige transition
    /// starts the new run from it (T101 handoff, T102).
    ///
    /// <c>StartingCash = RunStart.StartingCash + sum(valuePerLevel x level)</c> over the <c>startingCash</c> upgrades;
    /// <c>StorageLevel = 1 + sum(valuePerLevel x level)</c> over the <c>startingStorageLevel</c> upgrades, clamped to
    /// the number of storage levels. Both saturate at the exact bound instead of overflowing. Without a
    /// <see cref="PermanentState"/> every level is 0.
    /// </summary>
    public readonly struct NextRunStart
    {
        public long StartingCash { get; }
        public int StorageLevel { get; }

        private NextRunStart(long startingCash, int storageLevel)
        {
            StartingCash = startingCash;
            StorageLevel = storageLevel;
        }

        public static NextRunStart Compute(RunCatalog catalog, PermanentState permanent)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            long cash = catalog.RunStart.StartingCash;
            long storage = 1;
            foreach (IndustryUpgradeDef def in catalog.IndustryUpgrades.Values)
            {
                long level = permanent == null ? 0 : permanent.IndustryLevel(def.Id);
                if (def.Effect.Kind == "startingCash") cash = SaturatingAdd(cash, SaturatingMultiply(def.Effect.Value, level));
                else if (def.Effect.Kind == "startingStorageLevel") storage = SaturatingAdd(storage, SaturatingMultiply(def.Effect.Value, level));
            }
            return new NextRunStart(cash, (int)Math.Min(storage, catalog.StorageLevels.Count));
        }

        private static long SaturatingAdd(long a, long b) => CheckedMath.TryAdd(a, b, out long sum) ? sum : ExactBound.Value;

        private static long SaturatingMultiply(long a, long b) => CheckedMath.TryMultiply(a, b, out long product) ? product : ExactBound.Value;
    }
}
