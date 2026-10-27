using System;
using System.Globalization;
using System.IO;
using System.Security;

namespace IdleFactory.Unity.Persistence
{
    public enum SessionLogExportStatus { Exported, NothingToExport, Failed }

    /// <summary>The reason categories of a failed export (contracts/session-log.md, Export 6). Never an exception message or a path.</summary>
    public enum SessionLogExportFailure { None, NoSpace, NoPermission, Unknown }

    /// <summary>
    /// The outcome of <see cref="SessionLogExport.Export"/>. <see cref="FilePath"/> and <see cref="DirectoryPath"/> are for the screen only (SEC-2):
    /// they never go into the export, the log, a lifecycle record or <c>PlayerPrefs</c>.
    /// </summary>
    public readonly struct SessionLogExportResult
    {
        public SessionLogExportResult(SessionLogExportStatus status, SessionLogExportFailure failure, string filePath, string directoryPath, int lines)
        {
            Status = status;
            Failure = failure;
            FilePath = filePath;
            DirectoryPath = directoryPath;
            Lines = lines;
        }

        public SessionLogExportStatus Status { get; }
        public SessionLogExportFailure Failure { get; }

        /// <summary>The exported file; null unless <see cref="Status"/> is <see cref="SessionLogExportStatus.Exported"/>.</summary>
        public string FilePath { get; }

        /// <summary>The folder of the exports, shown on every platform (D-M); on Android the file is retrieved with <c>adb pull</c>.</summary>
        public string DirectoryPath { get; }

        public int Lines { get; }

        public bool IsExported => Status == SessionLogExportStatus.Exported;
    }

    /// <summary>
    /// Exports the session log (FR-031, D-M): the valid lines of both segments in order, written to a temp file inside
    /// <c>Exports/</c> and then renamed to <c>session-log-&lt;yyyyMMddTHHmmssZ&gt;.jsonl</c>, so no partial export is ever published. At most
    /// <see cref="MaxExportFiles"/> exports are kept; the oldest are removed after a successful export. A torn or CRC-failing line is skipped. An
    /// empty log writes nothing. A failure changes nothing (the temp file is removed) and can be retried. There is no share action.
    /// </summary>
    public sealed class SessionLogExport
    {
        public const int MaxExportFiles = 10;
        public const string FilePrefix = "session-log-";
        public const string FileExtension = ".jsonl";
        public const string TempExtension = ".tmp";

        private readonly SessionLogWriter _writer;
        private readonly ISessionLogFiles _files;
        private readonly Func<DateTime> _utcNow;

        public SessionLogExport(SessionLogWriter writer, ISessionLogFiles files, Func<DateTime> utcNow = null)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        /// <summary>Exports the log. Never throws; the result carries the outcome.</summary>
        public SessionLogExportResult Export()
        {
            string directory = _writer.ExportDirectoryPath;
            string temp = null;
            try
            {
                byte[] data = null;
                int length = 0;
                int lines = 0;
                _writer.WithFlushedSegments(paths =>
                {
                    Collect(paths, out data, out length, out lines);
                    return true;
                });

                if (lines == 0) return new SessionLogExportResult(SessionLogExportStatus.NothingToExport, SessionLogExportFailure.None, null, directory, 0);

                _files.EnsureDirectory(directory);
                string stamp = _utcNow().ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
                string final = UniquePath(directory, stamp);
                temp = final + TempExtension;
                _files.WriteNew(temp, data, length);
                _files.Move(temp, final);
                temp = null;

                Prune(directory);
                return new SessionLogExportResult(SessionLogExportStatus.Exported, SessionLogExportFailure.None, final, directory, lines);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                if (temp != null)
                {
                    try { _files.Delete(temp); }
                    catch (Exception cleanup) when (!(cleanup is OutOfMemoryException)) { }
                }

                return new SessionLogExportResult(SessionLogExportStatus.Failed, Classify(ex), null, directory, 0);
            }
        }

        /// <summary>The reason category of a failure: no space on the device, no permission, or anything else.</summary>
        public static SessionLogExportFailure Classify(Exception exception)
        {
            if (exception is UnauthorizedAccessException || exception is SecurityException) return SessionLogExportFailure.NoPermission;
            if (exception is IOException io)
            {
                // ERROR_HANDLE_DISK_FULL (0x27) and ERROR_DISK_FULL (0x70) on Windows; ENOSPC (28) from errno on Android and other Unix.
                int code = io.HResult & 0xFFFF;
                if (code == 0x27 || code == 0x70 || code == 28) return SessionLogExportFailure.NoSpace;
            }

            return SessionLogExportFailure.Unknown;
        }

        // Concatenates the valid lines of the segments (older first), each ending in a newline.
        private void Collect(string[] paths, out byte[] data, out int length, out int lines)
        {
            var contents = new byte[paths.Length][];
            int capacity = 0;
            for (int i = 0; i < paths.Length; i++)
            {
                contents[i] = _files.ReadAll(paths[i]);
                if (contents[i] != null) capacity += contents[i].Length + 1;
            }

            data = new byte[capacity];
            length = 0;
            lines = 0;
            for (int i = 0; i < contents.Length; i++)
            {
                byte[] bytes = contents[i];
                if (bytes == null) continue;
                int start = 0;
                while (start < bytes.Length)
                {
                    int end = start;
                    while (end < bytes.Length && bytes[end] != (byte)'\n') end++;
                    int lineLength = end - start;
                    if (SessionLogLines.Validate(bytes, start, lineLength))
                    {
                        Buffer.BlockCopy(bytes, start, data, length, lineLength);
                        length += lineLength;
                        data[length++] = (byte)'\n';
                        lines++;
                    }

                    start = end + 1;
                }
            }
        }

        private string UniquePath(string directory, string stamp)
        {
            string path = Path.Combine(directory, FilePrefix + stamp + FileExtension);
            for (int n = 1; _files.Exists(path) && n < 100; n++) path = Path.Combine(directory, FilePrefix + stamp + "-" + n + FileExtension);
            return path;
        }

        // After a successful export only: removes leftover temp files and the oldest exports beyond MaxExportFiles. Failures here never fail the export.
        private void Prune(string directory)
        {
            try
            {
                string[] leftovers = _files.List(directory, FilePrefix + "*" + TempExtension);
                for (int i = 0; i < leftovers.Length; i++) _files.Delete(leftovers[i]);

                string[] exports = _files.List(directory, FilePrefix + "*" + FileExtension);
                if (exports.Length <= MaxExportFiles) return;

                var written = new long[exports.Length];
                for (int i = 0; i < exports.Length; i++) written[i] = _files.LastWriteUtcTicks(exports[i]);
                for (int i = 1; i < exports.Length; i++)
                {
                    long key = written[i];
                    string path = exports[i];
                    int j = i - 1;
                    while (j >= 0 && (written[j] > key || (written[j] == key && string.CompareOrdinal(exports[j], path) > 0)))
                    {
                        written[j + 1] = written[j];
                        exports[j + 1] = exports[j];
                        j--;
                    }

                    written[j + 1] = key;
                    exports[j + 1] = path;
                }

                for (int i = 0; i < exports.Length - MaxExportFiles; i++) _files.Delete(exports[i]);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
            }
        }
    }
}
