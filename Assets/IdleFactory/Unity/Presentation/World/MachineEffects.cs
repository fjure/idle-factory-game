namespace IdleFactory.Unity.Presentation
{
    /// <summary>What a successful machine action answers with (FR-034): a restrained response on the machine.</summary>
    public enum MachineResponseKind
    {
        /// <summary>A successful upgrade: the UpgradeGlint of the F006 FX family.</summary>
        UpgradeGlint = 0,

        /// <summary>A successful switch-on: a restrained response (the Spark of the FX family stands in until the art pass names another).</summary>
        SwitchOn = 1,

        /// <summary>A successful build (T539, FR-034): the dust of the new foundation, spawned at the built location through the same seam.</summary>
        BuildDust = 2,
    }

    /// <summary>
    /// The world side of a machine response (moved from the UI layer in T529, A-11, so the dependency points from the UI to the presentation). The
    /// <see cref="EffectPool"/> implements it: the pool owns spawning, coalescing and the flash caps, so the card never draws an effect itself. Until a sink is wired, and always under reduced motion, the response is the static highlight.
    /// </summary>
    public interface IMachineEffects
    {
        /// <summary>Spawns the effect of <paramref name="kind"/> at the machine of <paramref name="location"/>.</summary>
        void Spawn(MachineResponseKind kind, int location);

        /// <summary>
        /// Whether the world can show <paramref name="kind"/> now (its optional FX row is in the catalog). False sends the card to the static highlight outside reduced motion too,
        /// so a missing row never makes the response silent (C-47, T560). A sink that cannot tell says true.
        /// </summary>
        bool CanSpawn(MachineResponseKind kind) => true;
    }

    /// <summary>
    /// The world side of the build card (T539, FR-032, FR-033, research R13): the highlight of the available locations and the ghost preview of the selected
    /// machine type. <see cref="SlotGridView"/> implements it, so the card never draws a sprite itself.
    /// </summary>
    public interface IBuildPreview
    {
        /// <summary>Outlines the locations a machine can be built at (an empty list clears the highlight).</summary>
        void ShowAvailable(System.Collections.Generic.IReadOnlyList<int> locations);

        /// <summary>Shows the ghost of the machine body <paramref name="machineKey"/> (frame 0, 60 % alpha) at the empty <paramref name="location"/>.</summary>
        void ShowPreview(int location, string machineKey);

        /// <summary>Clears the highlight and the ghost.</summary>
        void ClearBuildPreview();
    }
}
