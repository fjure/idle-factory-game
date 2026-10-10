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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            var timeScale = new DevTimeScale();
            TimeScale = timeScale;
            FactoryRuntimeHost.DefaultTimeScaleSource = timeScale;

            var host = new GameObject("IdleFactory DevTools");
            Object.DontDestroyOnLoad(host);
            DevToolsOverlay.Create(timeScale, host);
        }
    }
}
