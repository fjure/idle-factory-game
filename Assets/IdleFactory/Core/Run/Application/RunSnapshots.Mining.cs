using System;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Run.Application
{
    /// <summary>
    /// The read model of manual mining for the ore vein (T359, F5-R20 VEIN-5, VEIN-8, VEIN-11, scope decision #386). Every value is
    /// read from the same sources <see cref="RunCommands.Mine"/> uses: the effective milli-yield from the run modifiers, the rate counter
    /// and the milli remainder from the run state. Nothing is computed here that the command does not compute.
    /// </summary>
    public sealed class MiningSnapshot
    {
        /// <summary>The mined resource (content-defined, for example "iron.ore").</summary>
        public string ResourceId { get; }

        /// <summary>
        /// The effective yield of one counted activation in milli-Ore: <c>max(1, floor(baseYieldMilli x composed ppm / 10^6))</c>, the
        /// value <c>Mine</c> adds to the remainder. 0 when the composition passes the numeric bound (<c>Mine</c> then rejects with value.limit).
        /// </summary>
        public long EffectiveMilliYield { get; }

        /// <summary>The mining remainder below one Ore, 0 to 999 milli-Ore (transient run state, never saved).</summary>
        public long RemainderMilli { get; }

        /// <summary>The content's rate cap in counted activations per second of run timeline.</summary>
        public long CapPerSecond { get; }

        /// <summary>The activations already counted in the current timeline second (0 when the counter belongs to another second).</summary>
        public long CountedInCurrentSecond { get; }

        /// <summary>The activations the cap still allows in the current timeline second.</summary>
        public long CapRemaining => CapPerSecond > CountedInCurrentSecond ? CapPerSecond - CountedInCurrentSecond : 0;

        public long StructureRevision { get; }
        public long QuantityRevision { get; }
        public long TimelineUtcMs { get; }

        internal MiningSnapshot(string resourceId, long effectiveMilliYield, long remainderMilli, long capPerSecond, long countedInCurrentSecond,
            long structureRevision, long quantityRevision, long timelineUtcMs)
        {
            ResourceId = resourceId;
            EffectiveMilliYield = effectiveMilliYield;
            RemainderMilli = remainderMilli;
            CapPerSecond = capPerSecond;
            CountedInCurrentSecond = countedInCurrentSecond;
            StructureRevision = structureRevision;
            QuantityRevision = quantityRevision;
            TimelineUtcMs = timelineUtcMs;
        }
    }

    public static partial class RunSnapshots
    {
        /// <summary>
        /// The manual mining read model, or null when the content has no <c>runStart.manualMining</c> (legacy content). It reads state only.
        /// </summary>
        public static MiningSnapshot Mining(RunSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var mining = session.Catalog.RunStart.ManualMining;
            if (mining == null) return null;
            var state = session.State;
            long milliYield;
            if (!new RunModifiers(session.Catalog).TryManualYieldPpm(state, session.Permanent, out long composedPpm) ||
                !RunModifiers.TryEffectiveMilliYield(mining.BaseYieldMilli, composedPpm, out milliYield))
                milliYield = 0;
            long second = session.TimelineUtcMs / 1000;
            long used = state.MiningTimelineSecond == second ? state.MiningCountedInSecond : 0;
            return new MiningSnapshot(mining.ResourceId, milliYield, state.MiningRemainderMilli, mining.MaxActivationsPerSecond, used,
                session.StructureRevision, session.QuantityRevision, session.TimelineUtcMs);
        }
    }
}
