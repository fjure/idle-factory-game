using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using IdleFactory.Unity.Presentation.Look;

namespace IdleFactory.DevTools
{
    /// <summary>
    /// The development look-profile source (T609, contracts/look-profile.md rule 5, ARCH-6, ARCH-11). <see cref="DevToolsRuntime"/> creates it at
    /// <c>BeforeSceneLoad</c> and assigns it to <c>CoreGameFoundationBootstrap.DefaultLookProfileSource</c>, so it exists before the bootstrap composes the world.
    /// It holds the prototype profile assets that the dev-only holder scene supplies and the selection, which persists across runs in a small dev-only
    /// file under the preferences directory. That file is not a save and not the display-preferences file; no release build has this class or reads it.
    /// An id that no held profile has is passed on unchanged: the resolver falls back to the shipped default and logs it (ARCH-11).
    /// </summary>
    public sealed class DevLookProfileSource : ILookProfileSource
    {
        public const string FileName = "look-profile-dev.txt";
        public const int MaxFileBytes = 128;

        private readonly string filePath;
        private readonly List<LookProfile> profiles = new List<LookProfile>();
        private string selectedId;
        private bool ready;

        /// <param name="persistenceRoot">The resolved persistence root (<c>PersistenceRoots.Resolve(persistentDataPath)</c>).</param>
        public DevLookProfileSource(string persistenceRoot)
        {
            if (persistenceRoot == null)
            {
                throw new ArgumentNullException(nameof(persistenceRoot));
            }

            filePath = PathFor(persistenceRoot);
            selectedId = ReadPersisted();
        }

        public event Action Changed;

        public static string PathFor(string persistenceRoot) => Path.Combine(persistenceRoot, "IdleFactory", "Preferences", FileName);

        public string FilePath => filePath;

        public bool IsReady => ready;

        public string SelectedProfileId => selectedId;

        /// <summary>The prototype profiles the holder scene supplied.</summary>
        public IReadOnlyList<LookProfile> Profiles => profiles;

        public bool TryGetProfile(string id, out LookProfile profile)
        {
            for (var i = 0; i < profiles.Count; i++)
            {
                if (string.Equals(profiles[i].Id, id, StringComparison.Ordinal))
                {
                    profile = profiles[i];
                    return true;
                }
            }

            profile = null;
            return false;
        }

        /// <summary>Adds the profiles of the holder scene (an id once; the first wins) and marks the source ready.</summary>
        public void Register(IEnumerable<LookProfile> found)
        {
            if (found != null)
            {
                foreach (var profile in found)
                {
                    if (profile != null && !TryGetProfile(profile.Id, out _))
                    {
                        profiles.Add(profile);
                    }
                }
            }

            ready = true;
            Changed?.Invoke();
        }

        /// <summary>Marks the source ready with no further profile (no holder scene in this build).</summary>
        public void MarkReady() => Register(null);

        /// <summary>Selects a profile by id (the shipped default's id or empty selects the shipped default), persists the choice and raises <see cref="Changed"/>. Never throws.</summary>
        public void Select(string id)
        {
            var next = id ?? string.Empty;
            if (string.Equals(next, selectedId ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            selectedId = next;
            Persist(next);
            Changed?.Invoke();
        }

        private string ReadPersisted()
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }

                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var buffer = new byte[MaxFileBytes];
                    var count = 0;
                    while (count < buffer.Length)
                    {
                        var read = stream.Read(buffer, count, buffer.Length - count);
                        if (read <= 0)
                        {
                            break;
                        }

                        count += read;
                    }

                    var text = Encoding.UTF8.GetString(buffer, 0, count);
                    var end = text.IndexOfAny(new[] { '\r', '\n' });
                    return (end >= 0 ? text.Substring(0, end) : text).Trim();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void Persist(string id)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllText(filePath, id, new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // The choice still applies in memory; a dev tool never fails on a preference write.
            }
        }
    }
}
