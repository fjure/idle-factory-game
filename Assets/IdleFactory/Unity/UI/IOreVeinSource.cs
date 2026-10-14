namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// The outcome of one <see cref="IOreVeinSource.Mine"/> call, as the view needs it (T359). It mirrors the application's
    /// <c>MineResult</c> or its rejection code and carries no rule: the rate cap and the store check live in the command.
    /// </summary>
    public readonly struct OreVeinMineOutcome
    {
        public OreVeinMineOutcome(bool success, string reasonCode, long counted, long ignored, long blocked, long units, long stockAfter)
        {
            Success = success;
            ReasonCode = reasonCode;
            Counted = counted;
            Ignored = ignored;
            Blocked = blocked;
            Units = units;
            StockAfter = stockAfter;
        }

        /// <summary>Whether the command was applied. A success may still have <see cref="Counted"/> 0 when the rate cap is used up.</summary>
        public bool Success { get; }

        /// <summary>The stable rejection code (<c>storage.full</c>, <c>value.limit</c>, <c>lifecycle.blocked</c>, ...), or null on success.</summary>
        public string ReasonCode { get; }

        /// <summary>The activations that were credited.</summary>
        public long Counted { get; }

        /// <summary>The activations over the per-second rate cap (dropped silently, shown as the "max rate" cue).</summary>
        public long Ignored { get; }

        /// <summary>The activations within the cap that a full store or the numeric bound stopped.</summary>
        public long Blocked { get; }

        /// <summary>The whole units of Ore that moved into stock.</summary>
        public long Units { get; }

        /// <summary>The stock of the mined resource after the command.</summary>
        public long StockAfter { get; }
    }

    /// <summary>
    /// What the ore vein view reads and the one command it issues (T359, F5-R20 VEIN-12). The production implementation
    /// (<see cref="RunOreVeinSource"/>) adapts the run session and <c>RunCommands.Mine</c>; a test supplies its own source with an
    /// injected timeline. Everything is read-only except <see cref="Mine"/>.
    /// </summary>
    public interface IOreVeinSource
    {
        /// <summary>False when the content has no manual mining (legacy content): the vein then stays hidden.</summary>
        bool IsAvailable { get; }

        /// <summary>The mined resource's label, for example "Iron Ore".</summary>
        string ResourceLabel { get; }

        /// <summary>The content's rate cap in counted activations per second of run timeline. A hold paces itself at this rate.</summary>
        long ActivationsPerSecond { get; }

        /// <summary>The current effective yield of one activation in milli-Ore (100 is "+0.1").</summary>
        long YieldMilli { get; }

        /// <summary>The run timeline in milliseconds. A hold accrues on this clock, never on frame time.</summary>
        long TimelineMs { get; }

        /// <summary>An object that changes when the run state is replaced (a prestige or a new run), so the view drops its transient progress.</summary>
        object RunToken { get; }

        /// <summary>Whether the store has room for at least one more unit (the "Store full" cue clears when it does).</summary>
        bool HasRoom { get; }

        /// <summary>Whether the session is lifecycle-blocked (the lifecycle cue clears when it is not).</summary>
        bool IsLifecycleBlocked { get; }

        /// <summary>Issues <c>Mine(activations)</c>. The rate cap and every rejection are decided by the command.</summary>
        OreVeinMineOutcome Mine(long activations);
    }
}
