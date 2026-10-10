namespace IdleFactory.Unity.Bootstrap
{
    /// <summary>
    /// Null-default development seam (F5-R11, FR-026): a source of the simulation time scale. Production builds never register
    /// one, so <see cref="FactoryRuntimeHost"/> then advances by the real active-time delta exactly as before. Only the
    /// <c>IdleFactory.DevTools</c> assembly (define <c>IDLEFACTORY_DEV</c>) implements it.
    /// </summary>
    public interface ITimeScaleSource
    {
        /// <summary>
        /// The requested multiplier of the real frame delta. <see cref="double.PositiveInfinity"/> means Max, which is the
        /// S_max clamp of <see cref="FactoryRuntimeHost.MaxSimulatedSecondsPerFrame"/>. A value that is not a positive number
        /// counts as 1.
        /// </summary>
        double Scale { get; }

        /// <summary>
        /// Called by the host after each scaled advance with the multiplier that was actually applied (simulated delta divided
        /// by real delta, after the clamp), so the overlay can report what Max really delivers.
        /// </summary>
        void ReportEffectiveMultiplier(double effectiveMultiplier);
    }
}
