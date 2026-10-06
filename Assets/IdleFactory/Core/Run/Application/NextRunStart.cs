using System;
using System.Collections.Generic;
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

        /// <summary>
        /// The single owner of the fresh-run defaults (T141, F6): Cash <see cref="StartingCash"/>, no Research Points, the
        /// storage level of <see cref="Compute"/>, factory level 1 and <c>RunStart.InitialUnlocks</c> plus any
        /// <paramref name="additionalUnlocks"/>. The V1 import (run 1, its three mapped IDs), the fresh run of a start and
        /// the prestige transition (run n + 1) all build their run here, so a default changes in one place.
        /// </summary>
        internal static RunState CreateRun(RunCatalog catalog, PermanentState permanent, Guid runId, int runNumber, Guid? previousRunId,
            IEnumerable<string> additionalUnlocks = null)
        {
            NextRunStart start = Compute(catalog, permanent);
            var unlocked = new List<string>(catalog.RunStart.InitialUnlocks);
            if (additionalUnlocks != null)
            {
                foreach (string id in additionalUnlocks)
                    if (!unlocked.Contains(id)) unlocked.Add(id);
            }

            return new RunState(catalog, runId, runNumber, previousRunId, start.StartingCash, 0, start.StorageLevel, 1, unlocked);
        }

        private static long SaturatingAdd(long a, long b) => CheckedMath.TryAdd(a, b, out long sum) ? sum : ExactBound.Value;

        private static long SaturatingMultiply(long a, long b) => CheckedMath.TryMultiply(a, b, out long product) ? product : ExactBound.Value;
    }
}
