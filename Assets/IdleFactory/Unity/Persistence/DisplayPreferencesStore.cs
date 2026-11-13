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
    /// The display-preferences files under the persistence root (<c>IdleFactory/Preferences/</c>). This is NOT a save: it holds
    /// presentation choices, is never part of a checkpoint, and no checkpoint or session-log code enumerates or cleans its directory
    /// (those files live under sibling directories).
    /// <para>
    /// Format v2 (Feature 006 T538, research R20) is the file the store writes:
    /// <c>display-v2.json = { "version": 2, "reducedMotion": "followSystem" | "on" | "off", "showZoomButtons": true | false }</c>.
    /// Read order: when <c>display-v2.json</c> exists it is read by the strict v2 parser, and invalid content (malformed, unknown
    /// or missing member, duplicate key, wrong type, other version, trailing data, over 4096 bytes) yields the defaults plus the
    /// once-per-session diagnostic with no fallback to v1. When it is absent, <c>display-v1.json</c> (<c>{ "version": 1,
    /// "reducedMotion": ... }</c>, parser unchanged, <c>showZoomButtons</c> rejected) is read and <c>showZoomButtons</c> is false. A
    /// missing v1 file with no v2 file counts as invalid (diagnostic, defaults), as in Feature 004. Writes go to
    /// <c>display-v2.json.tmp</c> in the same directory and are replaced natively; the v1 file is left in place, and a stale temp
    /// file is never read. A downgrade to an older build reads v1 and loses the zoom choice (accepted, R20).
    /// </para>
    /// Both parsers are strict token walks (no reflection deserialization, IL2CPP-safe) over at most 4 KB read into a bounded buffer.
    /// The invalid code <c>preferences.display.invalid</c> is reported once per store instance (one session). The file path is
    /// fixed under the persistence root; no input chooses it. If a write fails the prior file is unchanged while the choice still
    /// applies in memory. No method throws; diagnostics carry the fixed code only (never a path or a file's bytes).
    /// </summary>
    public sealed class DisplayPreferencesStore
    {
        public const string InvalidDiagnosticCode = "preferences.display.invalid";
        public const int MaxFileBytes = 4096;

        /// <summary>
        /// The pause before the single retry of the atomic replace after a sharing or lock violation. It blocks the calling (main)
        /// thread for 40 ms on purpose: a choice is a rare tap, the wait is far below one dropped frame budget of a user-visible
        /// stall, and an asynchronous retry would let the sheet show a stale "Not saved" or "saved" state. Only that failure retries.
        /// </summary>
        public const int RetryDelayMilliseconds = 40;

        private const string ApplicationDirectory = "IdleFactory";
        private const string PreferencesDirectory = "Preferences";
        private const string V1FileName = "display-v1.json";
        private const string FileName = "display-v2.json";
        private const string TempFileName = "display-v2.json.tmp";

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

        /// <summary>
        /// Whether the player chose to show the zoom buttons (default false; a v1 file always reads as false). The store only keeps
        /// the choice; the shell decides where the zoom buttons appear.
        /// </summary>
        public bool ShowZoomButtons { get; private set; }

        /// <summary>
        /// Raised by <see cref="TrySave"/> after the choice was applied in memory and the write finished (successfully or not), so a
        /// subscriber such as the bootstrap re-resolves the effective reduced-motion state without a doc-comment convention. Not raised
        /// by <see cref="Reload"/>. A throwing subscriber never changes the outcome.
        /// </summary>
        public event Action Changed;

        /// <summary>True when the last write failed and the in-memory choice is not on disk.</summary>
        public bool HasUnsavedChoice => unsavedChoice;

        /// <summary>The v2 preference file's full path, the one the store writes (for tests and tooling; never logged).</summary>
        public string FilePath => Path.Combine(directory, FileName);

        /// <summary>The v1 preference file's full path, read only while the v2 file is absent (for tests and tooling; never logged).</summary>
        public string V1FilePath => Path.Combine(directory, V1FileName);

        /// <summary>The temp file the write goes through before the atomic replace (for tests and tooling; never logged).</summary>
        public string TempFilePath => Path.Combine(directory, TempFileName);

        /// <summary>
        /// Reads the file and makes its content the in-memory choice. Missing, unreadable, oversized or invalid content reads as
        /// <c>followSystem</c> and hidden zoom buttons. A choice whose write failed stays in effect until a write succeeds. Never throws.
        /// </summary>
        public ReducedMotionPreference Reload()
        {
            if (unsavedChoice)
            {
                return Preference;
            }

            ReadFiles();
            return Preference;
        }

        /// <summary>
        /// Applies the choice in memory and writes it atomically. Returns false when the write failed (the choice still
        /// applies and the prior file is unchanged). Never throws.
        /// </summary>
        public bool TrySave(ReducedMotionPreference preference)
        {
            Preference = preference;
            return Commit();
        }

        /// <summary>Applies the zoom-buttons choice in memory and writes it atomically, as <see cref="TrySave"/> does. Never throws.</summary>
        public bool TrySaveShowZoomButtons(bool show)
        {
            ShowZoomButtons = show;
            return Commit();
        }

        private bool Commit()
        {
            var saved = WriteAtomically(Preference, ShowZoomButtons);
            unsavedChoice = !saved;
            try
            {
                Changed?.Invoke();
            }
            catch (Exception)
            {
                // A subscriber never changes the outcome of the write.
            }

            return saved;
        }

        private bool WriteAtomically(ReducedMotionPreference preference, bool showZoomButtons)
        {
            var tempPath = Path.Combine(directory, TempFileName);
            try
            {
                files.CreateDirectory(directory);
                files.WriteTemp(tempPath, Encoding.UTF8.GetBytes(Serialize(preference, showZoomButtons)));
                try
                {
                    files.ReplaceOrMove(tempPath, FilePath);
                }
                catch (IOException exception) when (IsSharingOrLockViolation(exception))
                {
                    // One bounded retry: right after a fresh write, Windows indexers or antivirus can hold the file for a moment,
                    // and a transient lock must not show "Not saved". A second failure is a real failure. Any other IOException
                    // (disk full, missing path, access denied) is not transient and falls through to the failure path unretried.
                    System.Threading.Thread.Sleep(RetryDelayMilliseconds);
                    files.ReplaceOrMove(tempPath, FilePath);
                }

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

                return false;
            }
        }

        private const int ErrorSharingViolation = unchecked((int)0x80070020);
        private const int ErrorLockViolation = unchecked((int)0x80070021);

        /// <summary>The Windows sharing (ERROR_SHARING_VIOLATION) and lock (ERROR_LOCK_VIOLATION) HResults of an <see cref="IOException"/>.</summary>
        internal static bool IsSharingOrLockViolation(IOException exception) =>
            exception.HResult == ErrorSharingViolation || exception.HResult == ErrorLockViolation;

        internal static string Serialize(ReducedMotionPreference preference, bool showZoomButtons) =>
            "{\"version\":2,\"reducedMotion\":\"" + Token(preference) + "\",\"showZoomButtons\":" + (showZoomButtons ? "true" : "false") + "}";

        private void ReadFiles()
        {
            try
            {
                var v2Path = FilePath;
                if (files.FileExists(v2Path))
                {
                    // Once v2 exists it is the only source: invalid content is never rescued by the v1 file.
                    if (TryReadBounded(v2Path, out var v2Bytes) && TryParseV2(v2Bytes, out var motion, out var zoom))
                    {
                        Preference = motion;
                        ShowZoomButtons = zoom;
                        return;
                    }

                    UseDefaults();
                    return;
                }

                var v1Path = V1FilePath;
                if (files.FileExists(v1Path) && TryReadBounded(v1Path, out var v1Bytes) && TryParse(v1Bytes, out var v1Motion))
                {
                    Preference = v1Motion;
                    ShowZoomButtons = false;
                    return;
                }

                UseDefaults();
            }
            catch (Exception)
            {
                UseDefaults();
            }
        }

        /// <summary>Reads a file of at most <see cref="MaxFileBytes"/> without ever loading more than one byte past the cap.</summary>
        private bool TryReadBounded(string path, out byte[] bytes)
        {
            bytes = null;
            if (files.FileLength(path) > MaxFileBytes)
            {
                return false;
            }

            var read = files.ReadAll(path, MaxFileBytes + 1);
            if (read.Length > MaxFileBytes)
            {
                return false;
            }

            bytes = read;
            return true;
        }

        private void UseDefaults()
        {
            Preference = ReducedMotionPreference.FollowSystem;
            ShowZoomButtons = false;
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

        /// <summary>The v1 strict token walk (unchanged): one object, members "version" (the number 1) and "reducedMotion" (a known string), once each. <c>showZoomButtons</c> is an unknown member here and is rejected.</summary>
        internal static bool TryParse(byte[] bytes, out ReducedMotionPreference preference) =>
            TryWalk(bytes, (byte)'1', false, out preference, out _);

        /// <summary>The v2 strict token walk: members "version" (the number 2), "reducedMotion" and "showZoomButtons" (true or false), once each.</summary>
        internal static bool TryParseV2(byte[] bytes, out ReducedMotionPreference preference, out bool showZoomButtons) =>
            TryWalk(bytes, (byte)'2', true, out preference, out showZoomButtons);

        private static bool TryWalk(byte[] bytes, byte versionDigit, bool v2, out ReducedMotionPreference preference, out bool showZoomButtons)
        {
            preference = ReducedMotionPreference.FollowSystem;
            showZoomButtons = false;
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
            var haveZoom = false;
            var parsed = ReducedMotionPreference.FollowSystem;
            var parsedZoom = false;

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
                    if (haveVersion || i >= bytes.Length || bytes[i] != versionDigit)
                    {
                        return false;
                    }

                    i++;
                    // Exactly the one-digit version number: the next byte must end the token.
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
                else if (v2 && Matches(bytes, keyStart, keyLength, "showZoomButtons"))
                {
                    if (haveZoom || !ReadBool(bytes, ref i, out parsedZoom))
                    {
                        return false;
                    }

                    haveZoom = true;
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
            if (i != bytes.Length || !haveVersion || !haveMotion || (v2 && !haveZoom))
            {
                return false;
            }

            preference = parsed;
            showZoomButtons = parsedZoom;
            return true;
        }

        /// <summary>Reads the literal true or false; the next byte must end the token.</summary>
        private static bool ReadBool(byte[] bytes, ref int i, out bool value)
        {
            value = false;
            string literal;
            if (i < bytes.Length && bytes[i] == (byte)'t')
            {
                literal = "true";
            }
            else if (i < bytes.Length && bytes[i] == (byte)'f')
            {
                literal = "false";
            }
            else
            {
                return false;
            }

            if (i + literal.Length > bytes.Length || !Matches(bytes, i, literal.Length, literal))
            {
                return false;
            }

            i += literal.Length;
            if (i < bytes.Length && !IsValueEnd(bytes[i]))
            {
                return false;
            }

            value = literal.Length == 4;
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
