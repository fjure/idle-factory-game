using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace IdleFactory.Unity.Persistence
{
    /// <summary>
    /// The per-device recording setting of the session log (FR-031). It is not part of the save. <see cref="PlayerPrefsSessionLogPrefs"/>
    /// is the product implementation; tests inject a fake.
    /// </summary>
    public interface ISessionLogPrefs
    {
        /// <summary>Whether recording is on. On by default.</summary>
        bool Enabled { get; set; }
    }

    /// <summary><c>PlayerPrefs</c> key <see cref="Key"/>, default on. The writer reads it once and caches the value, so the per-event path never touches <c>PlayerPrefs</c>.</summary>
    public sealed class PlayerPrefsSessionLogPrefs : ISessionLogPrefs
    {
        public const string Key = "IdleFactory.SessionLog.Enabled";

        public bool Enabled
        {
            get => PlayerPrefs.GetInt(Key, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }

    /// <summary>
    /// The injected file layer of the session log (contracts/session-log.md, Test seams). Every member may throw; the writer and the export
    /// isolate every call. Paths are absolute and built by the writer. Tests inject a failing or in-memory implementation.
    /// </summary>
    public interface ISessionLogFiles
    {
        void EnsureDirectory(string directory);

        bool Exists(string path);

        /// <summary>The length in bytes, or -1 when the file does not exist.</summary>
        long Length(string path);

        /// <summary>The last write time in UTC ticks, or 0 when the file does not exist.</summary>
        long LastWriteUtcTicks(string path);

        /// <summary>Appends <paramref name="count"/> bytes, creating the file when needed.</summary>
        void Append(string path, byte[] buffer, int offset, int count);

        /// <summary>Creates the file empty, or empties it.</summary>
        void Truncate(string path);

        /// <summary>All bytes, or null when the file does not exist.</summary>
        byte[] ReadAll(string path);

        /// <summary>The last byte of the file, or -1 when it does not exist or is empty. Reads one byte, never the whole file.</summary>
        int ReadLastByte(string path);

        /// <summary>Creates or replaces the file with exactly <paramref name="count"/> bytes.</summary>
        void WriteNew(string path, byte[] buffer, int count);

        /// <summary>Renames <paramref name="from"/> to <paramref name="to"/>; fails when <paramref name="to"/> exists.</summary>
        void Move(string from, string to);

        void Delete(string path);

        /// <summary>The full paths of the files directly inside <paramref name="directory"/> whose name matches <paramref name="searchPattern"/>; empty when the directory does not exist.</summary>
        string[] List(string directory, string searchPattern);
    }

    /// <summary>The <see cref="System.IO"/> implementation of <see cref="ISessionLogFiles"/>. Appends flush to the OS but do not fsync (the CRC line format tolerates a torn last line).</summary>
    public sealed class SessionLogFileSystem : ISessionLogFiles
    {
        public void EnsureDirectory(string directory) => Directory.CreateDirectory(directory);

        public bool Exists(string path) => File.Exists(path);

        public long Length(string path)
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : -1L;
        }

        public long LastWriteUtcTicks(string path)
        {
            var info = new FileInfo(path);
            return info.Exists ? info.LastWriteTimeUtc.Ticks : 0L;
        }

        public void Append(string path, byte[] buffer, int offset, int count)
        {
            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                stream.Write(buffer, offset, count);
                stream.Flush();
            }
        }

        public void Truncate(string path)
        {
            using (new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
            }
        }

        public byte[] ReadAll(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;

        public int ReadLastByte(string path)
        {
            if (!File.Exists(path)) return -1;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length == 0) return -1;
                stream.Seek(-1, SeekOrigin.End);
                return stream.ReadByte();
            }
        }

        public void WriteNew(string path, byte[] buffer, int count)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(buffer, 0, count);
                stream.Flush();
            }
        }

        public void Move(string from, string to) => File.Move(from, to);

        public void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        public string[] List(string directory, string searchPattern) =>
            Directory.Exists(directory) ? Directory.GetFiles(directory, searchPattern) : new string[0];
    }

    /// <summary>
    /// The membership test of content IDs (S3). A writer given one drops any record whose ID it does not contain; the strict ID pattern
    /// applies in every case.
    /// </summary>
    public interface ISessionLogContentIds
    {
        bool Contains(string id);
    }

    /// <summary>
    /// The small flag interface of an accelerated session (D-I, #370): a time-scale source that implements it reports the factor the
    /// session log writes into the header. <c>IdleFactory.DevTools</c> implements it; <c>IdleFactory.Unity</c> never references DevTools.
    /// </summary>
    public interface ISessionLogTimeScale
    {
        /// <summary>The whole-number factor N of "ACCELERATED xN"; 1 is real time. Any value is clamped to [1, <see cref="int.MaxValue"/>] by the writer.</summary>
        long DisplayFactor { get; }
    }

    /// <summary>
    /// Optional companion of <see cref="ISessionLogTimeScale"/> (T366, F-B4): the selected time-scale step (x1, x10, x60, x600, Max as 0 to 4).
    /// The writer appends a further Header only when this changes, not when the effective factor of Max jitters. A source without it is
    /// compared by factor.
    /// </summary>
    public interface ISessionLogTimeScaleStep
    {
        /// <summary>The selected step; any change of it is a deliberate change of the time scale.</summary>
        int SelectedStep { get; }
    }
}
