#if !(UNITY_ANDROID && !UNITY_EDITOR) && !(UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
namespace IdleFactory.Unity.Bootstrap
{
    public sealed partial class ReducedMotionSignal
    {
        /// <summary>Every other platform has no reduce-motion probe: "not reduced".</summary>
        private static bool ProbePlatform() => false;

        /// <summary>No probe exists here: the Settings sheet says the system setting is not available.</summary>
        public static bool PlatformAvailable => false;
    }
}
#endif
