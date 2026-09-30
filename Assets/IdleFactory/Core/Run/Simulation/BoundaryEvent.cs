using System;

namespace IdleFactory.Run.Simulation
{
    /// <summary>
    /// Kind of a real-time boundary. The numeric order is the tie-break of <see cref="BoundaryEvent"/> ordering
    /// (kind, then ID) and follows the expiry order of R01: effects, Overdrive, contracts.
    /// </summary>
    public enum BoundaryKind
    {
        /// <summary>An Item effect ends (<c>EndUtcMs</c>). The ID is the effect's source (Item) ID.</summary>
        EffectExpiry = 0,

        /// <summary>The active phase of Overdrive ends (<c>ActiveUntilUtcMs</c>).</summary>
        OverdriveEnd = 1,

        /// <summary>The Overdrive cooldown ends (<c>CooldownUntilUtcMs</c>); Overdrive returns to ready.</summary>
        OverdriveCooldownEnd = 2,

        /// <summary>A timed contract expires. <see cref="BoundaryEvent.UtcMs"/> is <c>deadlineUtc + 1</c> (R05).</summary>
        ContractExpiry = 3,
    }

    /// <summary>
    /// A caller-supplied real-time boundary in simulation time (R05, GD-3). <see cref="SimTicks"/> is the offset in
    /// simulation ticks from the start of the advance (online: the timeline offset of the boundary; offline: the
    /// boundary mapped through <c>E</c>). <see cref="UtcMs"/> is the boundary's real instant in Unix milliseconds
    /// (the instant an expiry rule compares against). Events are supplied ordered by
    /// <c>(SimTicks, Kind, Id)</c> with ordinal ID comparison.
    /// </summary>
    public readonly struct BoundaryEvent
    {
        public long SimTicks { get; }
        public BoundaryKind Kind { get; }
        public string Id { get; }
        public long UtcMs { get; }

        public BoundaryEvent(long simTicks, BoundaryKind kind, string id, long utcMs)
        {
            SimTicks = simTicks;
            Kind = kind;
            Id = id ?? string.Empty;
            UtcMs = utcMs;
        }

        /// <summary>The required order of a supplied event list: <c>(SimTicks, Kind, Id)</c>, ordinal.</summary>
        public static int Compare(in BoundaryEvent a, in BoundaryEvent b)
        {
            if (a.SimTicks != b.SimTicks) return a.SimTicks < b.SimTicks ? -1 : 1;
            if (a.Kind != b.Kind) return a.Kind < b.Kind ? -1 : 1;
            return string.CompareOrdinal(a.Id, b.Id);
        }
    }

    /// <summary>Outcome of <see cref="RunSimulator.Advance"/>. Anything other than <see cref="Ok"/> changed no state.</summary>
    public enum RunAdvanceResult
    {
        Ok = 0,

        /// <summary>A precondition failed (negative ticks, unordered events, a state that breaks its invariants).</summary>
        InvalidArgument,

        /// <summary>A checked value would leave its bound; the whole advance is rejected (value.limit).</summary>
        ArithmeticOverflow,
    }
}
