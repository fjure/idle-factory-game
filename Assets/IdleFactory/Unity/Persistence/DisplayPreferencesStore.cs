using System;
using System.IO;
using System.Text;
using IdleFactory.Unity.Bootstrap;
using UnityEngine;

namespace IdleFactory.Unity.Persistence
{
    /// <summary>The player's reduced-motion choice (Feature 004 FR-020).</summary>
    public enum ReducedMotionPreference
    {
        FollowSystem = 0,
        On = 1,
        Off = 2,
    }

    /// <summary>Where the effective reduced-motion state came from.</summary>
    public enum ReducedMotionSource
    {
        Preference = 0,
        System = 1,
    }

    /// <summary>The effective reduced-motion state and its source, for the Settings live line.</summary>
    public readonly struct EffectiveReducedMotion
    {
        public EffectiveReducedMotion(bool reduced, ReducedMotionSource source)
        {
            Reduced = reduced;
            Source = source;
        }

        public bool Reduced { get; }
        public ReducedMotionSource Source { get; }
    }

    /// <summary>
    /// The reduced-motion resolver matrix: <c>On</c> reduces, <c>Off</c> does not, and <c>FollowSystem</c> asks the OS signal
    /// (a missing or failing signal means "not reduced"). It never throws.
    /// </summary>
    public static class ReducedMotionResolver
    {
        public static EffectiveReducedMotion Resolve(ReducedMotionPreference preference, IReducedMotionSignal signal)
        {
            switch (preference)
            {
                case ReducedMotionPreference.On:
                    return new EffectiveReducedMotion(true, ReducedMotionSource.Preference);
                case ReducedMotionPreference.Off:
                    return new EffectiveReducedMotion(false, ReducedMotionSource.Preference);
                default:
                    var reduced = false;
                    try
                    {
                        reduced = signal != null && signal.ShouldReduceMotion();
                    }
                    catch (Exception)
                    {
                        reduced = false;
                    }

                    return new EffectiveReducedMotion(reduced, ReducedMotionSource.System);
            }
        }
    }

    /// <summary>The file operations the store uses; replaceable in Edit Mode tests to inject failures.</summary>
    internal interface IDisplayPreferencesFileSystem
    {
        bool FileExists(string path);
        long FileLength(string path);
        byte[] ReadAll(string path, int maxBytes);
        void CreateDirectory(string path);
        void WriteTemp(string path, byte[] bytes);
        void ReplaceOrMove(string tempPath, string destinationPath);
        void DeleteQuietly(string path);
    }

    internal sealed class SystemDisplayPreferencesFileSystem : IDisplayPreferencesFileSystem
    {
        public static readonly SystemDisplayPreferencesFileSystem Instance = new SystemDisplayPreferencesFileSystem();

        public bool FileExists(string path) => File.Exists(path);

        public long FileLength(string path) => new FileInfo(path).Length;

        public byte[] ReadAll(string path, int maxBytes)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var buffer = new byte[maxBytes];
                var count = 0;
                while (count < maxBytes)
                {
                    var read = stream.Read(buffer, count, maxBytes - count);
                    if (read <= 0)
                    {
                        break;
                    }

                    count += read;
                }

                if (count == buffer.Length)
                {
                    return buffer;
                }

                var result = new byte[count];
                Buffer.BlockCopy(buffer, 0, result, 0, count);
                return result;
            }
        }

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public void WriteTemp(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        public void ReplaceOrMove(string tempPath, string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                File.Replace(tempPath, destinationPath, null);
            }
            else
            {
                File.Move(tempPath, destinationPath);
            }
        }

        public void DeleteQuietly(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // A leftover temp file is harmless; the next write recreates it.
            }
        }
    }

    /// <summary>
    /// The display-preferences file (<c>IdleFactory/Preferences/display-v1.json</c>) under the persistence root. This is NOT a
    /// save: it holds one presentation choice, is never part of a checkpoint, and no checkpoint or session-log code enumerates
    /// or cleans its directory (the checkpoint and session-log files live under sibling directories). The file is
    /// <c>{ "version": 1, "reducedMotion": "followSystem" | "on" | "off" }</c>, read by a strict token walk (no reflection
    /// deserialization, IL2CPP-safe) with a 4 KB limit; anything else reads as <c>followSystem</c> and reports the code
    /// <c>preferences.display.invalid</c> once per store instance (one session). A missing file counts as invalid (contract). Writes go to a temp file in the same directory and are replaced natively, so a failure leaves the prior
    /// file unchanged while the choice still applies in memory. No method throws; diagnostics carry the fixed code only (never
    /// a path or a file's bytes).
    /// </summary>
    public sealed class DisplayPreferencesStore
    {
        public const string InvalidDiagnosticCode = "preferences.display.invalid";
        public const int MaxFileBytes = 4096;

        private const string ApplicationDirectory = "IdleFactory";
        private const string PreferencesDirectory = "Preferences";
        private const string FileName = "display-v1.json";
        private const string TempFileName = "display-v1.json.tmp";

        private readonly string directory;
        private readonly IDisplayPreferencesFileSystem files;
        private readonly Action<string> diagnostic;
        private bool invalidReported;
        private bool unsavedChoice;

        /// <param name="persistenceRoot">The resolved root, i.e. <c>PersistenceRoots.Resolve(persistentDataPath)</c>.</param>
        public DisplayPreferencesStore(string persistenceRoot)
            : this(persistenceRoot, SystemDisplayPreferencesFileSystem.Instance, null)
        {
        }

        internal DisplayPreferencesStore(string persistenceRoot, IDisplayPreferencesFileSystem fileSystem, Action<string> diagnosticSink)
        {
            if (persistenceRoot == null)
            {
                throw new ArgumentNullException(nameof(persistenceRoot));
            }

            directory = Path.Combine(persistenceRoot, ApplicationDirectory, PreferencesDirectory);
            files = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            diagnostic = diagnosticSink ?? DefaultDiagnostic;
            Preference = ReducedMotionPreference.FollowSystem;
        }

        /// <summary>The choice currently in effect in memory.</summary>
        public ReducedMotionPreference Preference { get; private set; }

        /// <summary>True when the last write failed and the in-memory choice is not on disk.</summary>
        public bool HasUnsavedChoice => unsavedChoice;

        /// <summary>The preference file's full path (for tests and tooling; never logged).</summary>
        public string FilePath => Path.Combine(directory, FileName);

        /// <summary>
        /// Reads the file and makes its content the in-memory choice. Missing, unreadable, oversized or invalid content reads as
        /// <c>followSystem</c>. A choice whose write failed stays in effect until a write succeeds. Never throws.
        /// </summary>
        public ReducedMotionPreference Reload()
        {
            if (unsavedChoice)
            {
                return Preference;
            }

            Preference = ReadFile();
            return Preference;
        }

        /// <summary>
        /// Applies the choice in memory and writes it atomically. Returns false when the write failed (the choice still
        /// applies and the prior file is unchanged). Never throws.
        /// </summary>
        public bool TrySave(ReducedMotionPreference preference)
        {
            Preference = preference;
            var tempPath = Path.Combine(directory, TempFileName);
            try
            {
                files.CreateDirectory(directory);
                files.WriteTemp(tempPath, Encoding.UTF8.GetBytes(Serialize(preference)));
                files.ReplaceOrMove(tempPath, FilePath);
                unsavedChoice = false;
                return true;
            }
            catch (Exception)
            {
                try
                {
                    files.DeleteQuietly(tempPath);
                }
                catch (Exception)
                {
                    // Cleanup of the temp file never changes the outcome.
                }

                unsavedChoice = true;
                return false;
            }
        }

        internal static string Serialize(ReducedMotionPreference preference) =>
            "{\"version\":1,\"reducedMotion\":\"" + Token(preference) + "\"}";

        private ReducedMotionPreference ReadFile()
        {
            try
            {
                var path = FilePath;
                if (!files.FileExists(path))
                {
                    return Invalid();
                }

                if (files.FileLength(path) > MaxFileBytes)
                {
                    return Invalid();
                }

                var bytes = files.ReadAll(path, MaxFileBytes + 1);
                if (bytes.Length > MaxFileBytes || !TryParse(bytes, out var parsed))
                {
                    return Invalid();
                }

                return parsed;
            }
            catch (Exception)
            {
                return Invalid();
            }
        }

        private ReducedMotionPreference Invalid()
        {
            if (!invalidReported)
            {
                invalidReported = true;
                try
                {
                    diagnostic(InvalidDiagnosticCode);
                }
                catch (Exception)
                {
                    // Diagnostics never change the outcome.
                }
            }

            return ReducedMotionPreference.FollowSystem;
        }

        private static void DefaultDiagnostic(string code) => Debug.LogWarning("[IdleFactory.Preferences] " + code);

        private static string Token(ReducedMotionPreference preference)
        {
            switch (preference)
            {
                case ReducedMotionPreference.On: return "on";
                case ReducedMotionPreference.Off: return "off";
                default: return "followSystem";
            }
        }

        /// <summary>The strict token walk: one object, members "version" (the number 1) and "reducedMotion" (a known string), once each.</summary>
        internal static bool TryParse(byte[] bytes, out ReducedMotionPreference preference)
        {
            preference = ReducedMotionPreference.FollowSystem;
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxFileBytes)
            {
                return false;
            }

            var i = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                i = 3;
            }

            SkipWhitespace(bytes, ref i);
            if (!Expect(bytes, ref i, (byte)'{'))
            {
                return false;
            }

            var haveVersion = false;
            var haveMotion = false;
            var parsed = ReducedMotionPreference.FollowSystem;

            while (true)
            {
                SkipWhitespace(bytes, ref i);
                if (!ReadString(bytes, ref i, out var keyStart, out var keyLength))
                {
                    return false;
                }

                SkipWhitespace(bytes, ref i);
                if (!Expect(bytes, ref i, (byte)':'))
                {
                    return false;
                }

                SkipWhitespace(bytes, ref i);
                if (Matches(bytes, keyStart, keyLength, "version"))
                {
                    if (haveVersion || i >= bytes.Length || bytes[i] != (byte)'1')
                    {
                        return false;
                    }

                    i++;
                    // Exactly the number 1: the next byte must end the token.
                    if (i < bytes.Length && !IsValueEnd(bytes[i]))
                    {
                        return false;
                    }

                    haveVersion = true;
                }
                else if (Matches(bytes, keyStart, keyLength, "reducedMotion"))
                {
                    if (haveMotion || !ReadString(bytes, ref i, out var valueStart, out var valueLength))
                    {
                        return false;
                    }

                    if (Matches(bytes, valueStart, valueLength, "followSystem")) parsed = ReducedMotionPreference.FollowSystem;
                    else if (Matches(bytes, valueStart, valueLength, "on")) parsed = ReducedMotionPreference.On;
                    else if (Matches(bytes, valueStart, valueLength, "off")) parsed = ReducedMotionPreference.Off;
                    else return false;
                    haveMotion = true;
                }
                else
                {
                    return false;
                }

                SkipWhitespace(bytes, ref i);
                if (i >= bytes.Length)
                {
                    return false;
                }

                if (bytes[i] == (byte)',')
                {
                    i++;
                    continue;
                }

                if (bytes[i] == (byte)'}')
                {
                    i++;
                    break;
                }

                return false;
            }

            SkipWhitespace(bytes, ref i);
            if (i != bytes.Length || !haveVersion || !haveMotion)
            {
                return false;
            }

            preference = parsed;
            return true;
        }

        private static bool IsValueEnd(byte b) => b == (byte)',' || b == (byte)'}' || IsWhitespace(b);

        private static bool IsWhitespace(byte b) => b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n';

        private static void SkipWhitespace(byte[] bytes, ref int i)
        {
            while (i < bytes.Length && IsWhitespace(bytes[i]))
            {
                i++;
            }
        }

        private static bool Expect(byte[] bytes, ref int i, byte expected)
        {
            if (i < bytes.Length && bytes[i] == expected)
            {
                i++;
                return true;
            }

            return false;
        }

        /// <summary>Reads a string with no escapes and no control characters; reports its content span.</summary>
        private static bool ReadString(byte[] bytes, ref int i, out int start, out int length)
        {
            start = 0;
            length = 0;
            if (i >= bytes.Length || bytes[i] != (byte)'"')
            {
                return false;
            }

            i++;
            start = i;
            while (i < bytes.Length)
            {
                var b = bytes[i];
                if (b == (byte)'"')
                {
                    length = i - start;
                    i++;
                    return true;
                }

                if (b == (byte)'\\' || b < 0x20)
                {
                    return false;
                }

                i++;
            }

            return false;
        }

        private static bool Matches(byte[] bytes, int start, int length, string expected)
        {
            if (length != expected.Length)
            {
                return false;
            }

            for (var k = 0; k < length; k++)
            {
                if (bytes[start + k] != (byte)expected[k])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
