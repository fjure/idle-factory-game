namespace IdleFactory.Unity.Presentation
{
    /// <summary>What a successful machine action answers with (FR-034): a restrained response on the machine.</summary>
    public enum MachineResponseKind
    {
        /// <summary>A successful upgrade: the UpgradeGlint of the F006 FX family.</summary>
        UpgradeGlint = 0,

        /// <summary>A successful switch-on: a restrained response (the Spark of the FX family stands in until the art pass names another).</summary>
        SwitchOn = 1,
    }

    /// <summary>
    /// The world side of a machine response (moved from the UI layer in T529, A-11, so the dependency points from the UI to the presentation). The
    /// <see cref="EffectPool"/> implements it: the pool owns spawning, coalescing and the flash caps, so the card never draws an effect itself. Until a sink is wired, and always under reduced motion, the response is the static highlight.
    /// </summary>
    public interface IMachineEffects
    {
        /// <summary>Spawns the effect of <paramref name="kind"/> at the machine of <paramref name="location"/>.</summary>
        void Spawn(MachineResponseKind kind, int location);
    }
}
