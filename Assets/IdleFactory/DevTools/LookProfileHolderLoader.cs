using System.Collections;
using UnityEngine;

namespace IdleFactory.DevTools
{
    /// <summary>
    /// Runs <see cref="LookProfileHolder.LoadAndRegister"/> once after the first scene loaded (T609): the holder scene cannot load at
    /// <c>BeforeSceneLoad</c>, so the source stays not-ready (and the shipped default stays in use, silently) until the holder scene has been read.
    /// </summary>
    public sealed class LookProfileHolderLoader : MonoBehaviour
    {
        public DevLookProfileSource Source { get; set; }

        private IEnumerator Start()
        {
            if (Source != null)
            {
                yield return LookProfileHolder.LoadAndRegister(Source);
            }
        }
    }
}
