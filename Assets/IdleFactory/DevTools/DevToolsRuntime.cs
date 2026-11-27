using IdleFactory.Unity.Bootstrap;
using UnityEngine;

namespace IdleFactory.DevTools
{
    /// <summary>
    /// Registers the development time scale as the default <see cref="ITimeScaleSource"/> and shows the overlay (F5-R11).
    /// This assembly exists only when <c>IDLEFACTORY_DEV</c> is defined, and nothing references it.
    /// </summary>
    public static class DevToolsRuntime
    {
        public static DevTimeScale TimeScale { get; private set; }

        /// <summary>The development look-profile source (T609), or null before <c>BeforeSceneLoad</c> ran.</summary>
        public static DevLookProfileSource LookSource { get; private set; }

        /// <summary>
        /// T609 (ARCH-6): injects the look-profile source before the scene loads, mirroring <c>DefaultTimeScaleSource</c>, because the bootstrap resolves its
        /// look profile in <c>Awake</c>, which runs before any <c>AfterSceneLoad</c> method. The prototype profiles arrive later with the holder scene.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeLookProfileSource()
        {
            var source = new DevLookProfileSource(PersistenceRoots.Resolve(Application.persistentDataPath));
            LookSource = source;
            CoreGameFoundationBootstrap.DefaultLookProfileSource = source;
        }

        /// <summary>The single overlay host object, or null before initialization.</summary>
        public static GameObject Host { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            // With domain reload disabled this runs on every Enter Play Mode: destroy a previous overlay so at most one exists.
            if (Host != null)
            {
                Object.Destroy(Host);
            }

            var timeScale = new DevTimeScale();
            TimeScale = timeScale;
            FactoryRuntimeHost.DefaultTimeScaleSource = timeScale;

            var host = new GameObject("IdleFactory DevTools");
            Host = host;
            Object.DontDestroyOnLoad(host);
            DevToolsOverlay.Create(timeScale, host, LookSource);
            if (LookSource != null)
            {
                host.AddComponent<LookProfileHolderLoader>().Source = LookSource;
            }
        }
    }
}
