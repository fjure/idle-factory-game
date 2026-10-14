using System;
using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// Adapts the run session and <see cref="RunCommands.Mine"/> to the ore vein view (T359). Mining goes only through the
    /// command, which owns the rate cap, the store check and the yield. The effective milli-yield is shown on the label, and the
    /// application layer does not expose it, so it is derived here from public catalog and level data with the same fold as the
    /// command (<c>floor(base x composed ppm / 10^6)</c>, at least 1; Research then Industry in ordinal ID order, floor per level).
    /// It is display-only: the command alone decides what is credited, and a test pins this fold to the credited Ore.
    /// </summary>
    public sealed class RunOreVeinSource : IOreVeinSource
    {
        private const int MaxEffectLevels = 1024;
        private const long Ppm = 1_000_000L;

        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly ManualMiningDef mining;
        private readonly string resourceLabel;
        private readonly ResearchDef[] research;
        private readonly IndustryUpgradeDef[] industry;
        private long yieldRevision = -1;
        private object yieldState;
        private long yieldMilli;

        public RunOreVeinSource(RunSession session, RunCommands commands)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            var catalog = session.Catalog;
            mining = catalog.RunStart.ManualMining;
            resourceLabel = mining != null && catalog.Resources.TryGetValue(mining.ResourceId, out var resource) ? resource.Label : "Ore";
            research = Sorted(catalog.Research.Values, definition => definition.Effect.Kind == "manualYield", definition => definition.Id);
            industry = Sorted(catalog.IndustryUpgrades.Values, definition => definition.Effect.Kind == "manualYield", definition => definition.Id);
        }

        public bool IsAvailable => mining != null;

        public string ResourceLabel => resourceLabel;

        public long ActivationsPerSecond => mining == null ? 1L : Math.Max(1L, mining.MaxActivationsPerSecond);

        public long TimelineMs => session.TimelineUtcMs;

        public object RunToken => session.State;

        public bool HasRoom => session.State.Inventory.Free > 0;

        public bool IsLifecycleBlocked => session.IsLifecycleBlocked;

        public long YieldMilli
        {
            get
            {
                if (mining == null)
                {
                    return 0L;
                }

                // Levels move the structure revision (Research, Industry, prestige); a replaced run state resets the cache too.
                if (yieldRevision != session.StructureRevision || !ReferenceEquals(yieldState, session.State))
                {
                    yieldRevision = session.StructureRevision;
                    yieldState = session.State;
                    yieldMilli = ComputeYield();
                }

                return yieldMilli;
            }
        }

        public OreVeinMineOutcome Mine(long activations)
        {
            var result = commands.Mine(activations);
            if (!result.IsSuccess)
            {
                return new OreVeinMineOutcome(false, result.ReasonCode, 0, 0, 0, 0, 0);
            }

            var mined = result.Value;
            return new OreVeinMineOutcome(true, null, mined.Counted, mined.Ignored, mined.Blocked, mined.Units, mined.StockAfter);
        }

        private long ComputeYield()
        {
            var state = session.State;
            var permanent = session.Permanent;
            var composed = Ppm;
            foreach (var definition in research)
            {
                if (!FoldLevels(ref composed, definition.Effect.Value, state.ResearchLevel(definition.Id)))
                {
                    return mining.BaseYieldMilli;
                }
            }

            if (permanent != null)
            {
                foreach (var definition in industry)
                {
                    if (!FoldIndustry(ref composed, definition, permanent.IndustryLevel(definition.Id)))
                    {
                        return mining.BaseYieldMilli;
                    }
                }
            }

            if (!MulDiv.MulDivFloor(mining.BaseYieldMilli, composed, Ppm, out var milli))
            {
                return mining.BaseYieldMilli;
            }

            return Math.Max(1L, milli);
        }

        private static bool FoldLevels(ref long composed, long factorPpm, long levels)
        {
            if (levels > MaxEffectLevels)
            {
                levels = MaxEffectLevels;
            }

            for (long level = 0; level < levels; level++)
            {
                if (!MulDiv.MulDivFloor(composed, factorPpm, Ppm, out composed))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool FoldIndustry(ref long composed, IndustryUpgradeDef definition, long levels)
        {
            if (!definition.IsDecayed)
            {
                return FoldLevels(ref composed, definition.Effect.Value, levels);
            }

            var table = definition.ValueTable;
            levels = Math.Min(Math.Min(levels, MaxEffectLevels), table.Count);
            for (var level = 0; level < levels; level++)
            {
                if (!MulDiv.MulDivFloor(composed, table[level], Ppm, out composed))
                {
                    return false;
                }
            }

            return true;
        }

        private static T[] Sorted<T>(IEnumerable<T> source, Func<T, bool> keep, Func<T, string> id)
        {
            var list = new List<T>();
            foreach (var item in source)
            {
                if (keep(item))
                {
                    list.Add(item);
                }
            }

            list.Sort((a, b) => string.CompareOrdinal(id(a), id(b)));
            return list.ToArray();
        }
    }
}
