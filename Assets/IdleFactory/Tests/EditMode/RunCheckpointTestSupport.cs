using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Persistence;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>A clock a test moves by hand (Unix milliseconds).</summary>
    internal sealed class FixedUtcClock : IUtcClock
    {
        public FixedUtcClock(long unixMs) => UnixMs = unixMs;

        public long UnixMs { get; set; }
        public bool Fail { get; set; }

        public DateTimeOffset GetUtcNow()
        {
            if (Fail) throw new InvalidOperationException("Injected clock failure.");
            return DateTimeOffset.FromUnixTimeMilliseconds(UnixMs);
        }
    }

    /// <summary>Checkpoint IDs 3333...0001, 0002, ... so that identities are readable and reproducible.</summary>
    internal sealed class SequenceCheckpointIds : ICheckpointIdSource
    {
        private int next;

        public SequenceCheckpointIds(int first = 1) => next = first;

        public CommandResult<CheckpointId> Next() =>
            CommandResult<CheckpointId>.Success(new CheckpointId(new Guid("33333333-3333-3333-3333-" + (next++).ToString("D12"))));
    }

    /// <summary>
    /// An in-memory slot store that can fail or corrupt any single backend call by its position in the call sequence,
    /// so a test can fail every step of the generation protocol (create, write, flush, read back, manifest, verify) one by one.
    /// </summary>
    internal sealed class ScriptedBackend : ICheckpointStorageBackend
    {
        private int failAtCall = -1;
        private int corruptReadAtCall = -1;
        private string failOnFirst;
        private CheckpointStorageFile? open;

        public ScriptedBackend(string name = "scripted")
        {
            StorageIdentity = name + "-" + Guid.NewGuid().ToString("N");
        }

        public string StorageIdentity { get; }
        public Dictionary<CheckpointStorageFile, byte[]> Files { get; private set; } = new Dictionary<CheckpointStorageFile, byte[]>();
        public List<string> Log { get; } = new List<string>();
        public int Calls { get; private set; }
        public bool FailAll { get; set; }
        public bool FaultTriggered { get; private set; }

        public void FailAtCall(int index)
        {
            failAtCall = index;
            corruptReadAtCall = -1;
            FaultTriggered = false;
        }

        public void CorruptReadAtCall(int index)
        {
            corruptReadAtCall = index;
            failAtCall = -1;
            FaultTriggered = false;
        }

        /// <summary>Fails the first call of one operation on one slot, e.g. <c>Create</c> on <c>Pending</c>.</summary>
        public void FailOnFirst(string operation, CheckpointStorageFile file)
        {
            failOnFirst = operation + ":" + file;
            FaultTriggered = false;
        }

        public void ClearFault()
        {
            failOnFirst = null;
            failAtCall = -1;
            corruptReadAtCall = -1;
            FailAll = false;
        }

        public void ResetCounters()
        {
            Calls = 0;
            Log.Clear();
            FaultTriggered = false;
        }

        public Dictionary<CheckpointStorageFile, byte[]> Snapshot() =>
            Files.ToDictionary(pair => pair.Key, pair => (byte[])pair.Value.Clone());

        public void Restore(Dictionary<CheckpointStorageFile, byte[]> files)
        {
            Files = files.ToDictionary(pair => pair.Key, pair => (byte[])pair.Value.Clone());
            open = null;
        }

        public bool Exists(CheckpointStorageFile file)
        {
            Enter("Exists", file);
            return Files.ContainsKey(file);
        }

        public long GetLength(CheckpointStorageFile file)
        {
            Enter("GetLength", file);
            return Files[file].Length;
        }

        public byte[] ReadExactly(CheckpointStorageFile file, int expectedLength)
        {
            bool corrupt = Enter("ReadExactly", file);
            var bytes = (byte[])Files[file].Clone();
            if (corrupt && bytes.Length > 0) bytes[bytes.Length / 2] ^= 0x55;
            return bytes;
        }

        public void Create(CheckpointStorageFile file)
        {
            Enter("Create", file);
            open = file;
            Files[file] = new byte[0];
        }

        public void Write(CheckpointStorageFile file, byte[] bytes)
        {
            Enter("Write", file);
            if (open != file) throw new InvalidOperationException("Slot not open.");
            Files[file] = Files[file].Concat(bytes).ToArray();
        }

        public void Flush(CheckpointStorageFile file) => Enter("Flush", file);

        public void FlushExisting(CheckpointStorageFile file) => Enter("FlushExisting", file);

        public void Close(CheckpointStorageFile file)
        {
            Enter("Close", file);
            open = null;
        }

        // True when this call is the injected corrupt read; throws when it is the injected failure.
        private bool Enter(string operation, CheckpointStorageFile file)
        {
            Log.Add(operation + ":" + file);
            int index = Calls++;
            if (failOnFirst != null && failOnFirst == operation + ":" + file)
            {
                failOnFirst = null;
                FaultTriggered = true;
                throw new IOException("Injected " + operation + ":" + file + " failure.");
            }

            if (FailAll || index == failAtCall)
            {
                FaultTriggered = true;
                throw new IOException("Injected " + operation + ":" + file + " failure.");
            }

            if (index == corruptReadAtCall && operation == "ReadExactly")
            {
                FaultTriggered = true;
                return true;
            }

            return false;
        }
    }

    internal static class RunCheckpointTestSupport
    {
        public static string NewTempRoot()
        {
            string path = Path.Combine(Path.GetTempPath(), "idlefactory-t138-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        public static void DeleteQuietly(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public static string V2Directory(string root) => SaveV2CheckpointFiles.Layout.DirectoryFor(root);

        /// <summary>A fixture as a checkpoint built from fresh state objects, exactly as the store would read it.</summary>
        public static RunCheckpoint CheckpointOf(SaveV2Fixture fixture)
        {
            var decoded = new SaveV2CheckpointCodec(fixture.Catalog).Deserialize(fixture.Bytes);
            if (!decoded.Succeeded) throw new InvalidOperationException("Fixture does not decode: " + fixture.FileName + " " + decoded.Message);
            return decoded.Checkpoint;
        }

        public static byte[] BytesOf(RunCheckpoint checkpoint, SaveV2Fixture like)
        {
            var written = new SaveV2CheckpointCodec(like.Catalog).Serialize(checkpoint);
            if (!written.Succeeded) throw new InvalidOperationException("Checkpoint does not serialize: " + written.Message);
            return written.Value;
        }
    }
}
