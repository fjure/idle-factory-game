using System;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// Adapts the run session and <see cref="RunCommands.Mine"/> to the ore vein view (T359). Mining goes only through the command, which
    /// owns the rate cap, the store check and the yield. The effective milli-yield and the remainder come from the application's
    /// <see cref="RunSnapshots.Mining"/> read model, so the view derives no economy value itself.
    /// </summary>
    public sealed class RunOreVeinSource : IOreVeinSource
    {
        private readonly RunSession session;
        private readonly RunCommands commands;
        private readonly ManualMiningDef mining;
        private readonly string resourceLabel;
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
        }

        public bool IsAvailable => mining != null;

        public string ResourceLabel => resourceLabel;

        public long ActivationsPerSecond => mining == null ? 1L : Math.Max(1L, mining.MaxActivationsPerSecond);

        public long TimelineMs => session.TimelineUtcMs;

        public object RunToken => session.State;

        public bool HasRoom => session.State.Inventory.Free > 0;

        public bool IsLifecycleBlocked => session.IsLifecycleBlocked;

        /// <summary>The remainder toward the next Ore. It is read after a click and on a run change, not every frame (the snapshot allocates).</summary>
        public long RemainderMilli => RunSnapshots.Mining(session)?.RemainderMilli ?? 0L;

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
                    yieldMilli = RunSnapshots.Mining(session)?.EffectiveMilliYield ?? 0L;
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
    }
}
