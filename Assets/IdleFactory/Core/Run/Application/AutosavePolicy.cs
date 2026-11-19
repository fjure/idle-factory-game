namespace IdleFactory.Run.Application
{
    /// <summary>The result of one <see cref="RunPersistence.AutosaveWhenDue"/> tick (T559). A value type, so a tick that writes nothing allocates nothing.</summary>
    public enum AutosaveTickOutcome
    {
        /// <summary>Nothing was written: not running, a prestige pending, nothing changed, or the cadence has not elapsed.</summary>
        NotDue,

        /// <summary>A new generation became durable.</summary>
        Written,

        /// <summary>The write failed; the previous checkpoint is untouched and <see cref="RunPersistence.Status"/> reports the failure.</summary>
        Failed,
    }

    /// <summary>Why <see cref="AutosavePolicy.Decide"/> allows a write (T559).</summary>
    internal enum AutosaveReason
    {
        /// <summary>No write is due.</summary>
        None,

        /// <summary>The periodic interval elapsed and the run changed since the last checkpoint.</summary>
        Periodic,

        /// <summary>A structural change of the factory (build, upgrade, factory level, contract, unlock) is not yet durable and the action spacing elapsed.</summary>
        Action,
    }

    /// <summary>
    /// The cadence of the timer-driven autosave (T559, FR-003): pure arithmetic on milliseconds, with no clock, store or run state, so
    /// the throttle is tested on plain numbers. <see cref="RunPersistence.AutosaveWhenDue"/> owns the state it decides on and the write.
    /// <list type="bullet">
    /// <item>The run is written at most every <see cref="IntervalMs"/> while it changes (production alone moves the quantities).</item>
    /// <item>A structural change (<see cref="RunSession.StructureRevision"/>: build, upgrade, factory level, contract, unlock) is written as soon as
    /// <see cref="ActionSpacingMs"/> has elapsed since the previous write, so a burst of actions costs one write per spacing and never a storm.</item>
    /// <item>After a failed write the next attempt waits the full interval, whatever the trigger, so a broken store is not hit every frame.</item>
    /// <item>A clock that moved backwards never blocks a write (like <see cref="RunPersistence.Autosave"/>), so the guard only reduces writes.</item>
    /// </list>
    /// </summary>
    internal sealed class AutosavePolicy
    {
        /// <summary>The default periodic interval, 30 s (owner decision 2026-11-18, T559). The class default is the source of truth.</summary>
        public const long DefaultIntervalMs = 30_000;

        /// <summary>The default minimum spacing between two writes caused by factory changes, 2 s.</summary>
        public const long DefaultActionSpacingMs = 2_000;

        public AutosavePolicy()
            : this(DefaultIntervalMs, DefaultActionSpacingMs)
        {
        }

        public AutosavePolicy(long intervalMs, long actionSpacingMs)
        {
            IntervalMs = intervalMs < 1 ? DefaultIntervalMs : intervalMs;
            ActionSpacingMs = actionSpacingMs < 1 ? DefaultActionSpacingMs : actionSpacingMs;
            if (ActionSpacingMs > IntervalMs) ActionSpacingMs = IntervalMs;
        }

        public long IntervalMs { get; }

        public long ActionSpacingMs { get; }

        /// <summary>
        /// Decides one tick. <paramref name="elapsedMs"/> is the time since the previous write attempt (negative for a clock that moved
        /// backwards); <paramref name="structuralChange"/> and <paramref name="anyChange"/> say whether the run differs from its latest
        /// durable state; <paramref name="previousAttemptFailed"/> is true when that attempt did not become durable.
        /// </summary>
        public AutosaveReason Decide(long elapsedMs, bool structuralChange, bool anyChange, bool previousAttemptFailed)
        {
            if (!anyChange && !structuralChange) return AutosaveReason.None;
            if (elapsedMs < 0) return structuralChange ? AutosaveReason.Action : AutosaveReason.Periodic;
            if (elapsedMs >= IntervalMs) return structuralChange ? AutosaveReason.Action : AutosaveReason.Periodic;
            if (structuralChange && !previousAttemptFailed && elapsedMs >= ActionSpacingMs) return AutosaveReason.Action;
            return AutosaveReason.None;
        }
    }
}
