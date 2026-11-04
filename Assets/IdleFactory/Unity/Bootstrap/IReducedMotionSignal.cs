using System;

namespace IdleFactory.Unity.Bootstrap
{
    /// <summary>
    /// The operating system's reduce-motion signal (Feature 004 FR-020, research R11). An implementation never throws and
    /// answers "not reduced" (false) whenever the platform call is unavailable or fails.
    /// </summary>
    public interface IReducedMotionSignal
    {
        bool ShouldReduceMotion();

        /// <summary>
        /// False when the platform has no reduce-motion probe, so "Follow system" cannot know the system setting (the Settings sheet
        /// then says so). Defaults to true: a test double or a custom signal is assumed to have a probe.
        /// </summary>
        bool Available => true;
    }

    /// <summary>
    /// The platform signal: one partial class whose probe lives in <c>ReducedMotionSignal.Android.cs</c>,
    /// <c>.Windows.cs</c> or <c>.Default.cs</c>, exactly one of which compiles for a given build target.
    /// </summary>
    public sealed partial class ReducedMotionSignal : IReducedMotionSignal
    {
        public bool Available => PlatformAvailable;

        public bool ShouldReduceMotion()
        {
            try
            {
                return ProbePlatform();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
