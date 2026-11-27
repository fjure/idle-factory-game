using System;
using System.Collections;
using System.Collections.Generic;
using IdleFactory.Unity.Presentation.Look;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleFactory.DevTools
{
    /// <summary>
    /// The holder of the prototype look-profile references (T609, ARCH-1). It sits in the dev-only scene <c>F007LookProfiles</c>, which
    /// <c>PlatformBuildCommands.Run</c> adds to the build only for <c>IDLEFACTORY_DEV</c> builds. Prototype profiles and their art are therefore
    /// referenced from nothing a release build contains: there is no <c>Resources</c> path and no StreamingAssets entry. This type lives in the
    /// DevTools assembly, so a release build does not even compile it.
    /// </summary>
    public sealed class LookProfileHolder : MonoBehaviour
    {
        public const string SceneName = "F007LookProfiles";

        [SerializeField] private LookProfile[] profiles = Array.Empty<LookProfile>();

        public IReadOnlyList<LookProfile> Profiles => profiles ?? Array.Empty<LookProfile>();

        /// <summary>Gives the holder its profiles (tests and the scene builder).</summary>
        public void SetProfiles(LookProfile[] value) => profiles = value ?? Array.Empty<LookProfile>();

        /// <summary>Registers the profiles of every holder in the loaded scenes with <paramref name="source"/> and marks it ready.</summary>
        public static void RegisterLoaded(DevLookProfileSource source)
        {
            var found = new List<LookProfile>();
            foreach (var holder in FindObjectsByType<LookProfileHolder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                found.AddRange(holder.Profiles);
            }

            source.Register(found);
        }

        /// <summary>
        /// Loads the holder scene additively when this build has it, registers its profiles, and otherwise marks the source ready with none. The
        /// editor's Play Mode has the scene only when it is in the build settings, so a plain editor run reaches the second branch.
        /// </summary>
        public static IEnumerator LoadAndRegister(DevLookProfileSource source)
        {
            if (!Application.CanStreamedLevelBeLoaded(SceneName))
            {
                source.MarkReady();
                yield break;
            }

            var load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            while (load != null && !load.isDone)
            {
                yield return null;
            }

            RegisterLoaded(source);
        }
    }
}
