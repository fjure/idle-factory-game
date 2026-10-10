using System;
using System.IO;

namespace IdleFactory.Unity.Bootstrap
{
    /// <summary>
    /// The root directory under which checkpoints, the session log and exports live. Builds compiled with
    /// <c>IDLEFACTORY_DEV</c> use <c>IdleFactory-Dev</c> beneath the persistent data path (D-N, SEC-13) so a development build
    /// can never read or overwrite a player's real save. The switch is a compile-time define inside this assembly: there is no
    /// reference to <c>IdleFactory.DevTools</c>, and a release build compiles only the normal branch.
    /// </summary>
    public static class PersistenceRoots
    {
        /// <summary>Directory name of the development save root.</summary>
        public const string DevelopmentDirectoryName = "IdleFactory-Dev";

        /// <summary>True when this assembly was compiled with <c>IDLEFACTORY_DEV</c>.</summary>
        public static bool IsDevelopmentRoot
        {
            get
            {
#if IDLEFACTORY_DEV
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Maps the persistent data path to the root this build must use.</summary>
        public static string Resolve(string persistentDataPath)
        {
            if (persistentDataPath == null)
            {
                throw new ArgumentNullException(nameof(persistentDataPath));
            }

#if IDLEFACTORY_DEV
            return Path.Combine(persistentDataPath, DevelopmentDirectoryName);
#else
            return persistentDataPath;
#endif
        }
    }
}
