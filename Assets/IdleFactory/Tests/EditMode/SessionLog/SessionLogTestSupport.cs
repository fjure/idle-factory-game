using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;

namespace IdleFactory.Tests.EditMode.SessionLog
{
    /// <summary>A recording setting with injectable failures.</summary>
    internal sealed class FakePrefs : ISessionLogPrefs
    {
        public bool Value = true;
        public bool FailGet;
        public bool FailSet;
        public int Sets;

        public bool Enabled
        {
            get
            {
                if (FailGet) throw new InvalidOperationException("prefs get");
                return Value;
            }
            set
            {
                if (FailSet) throw new InvalidOperationException("prefs set");
                Value = value;
                Sets++;
            }
        }
    }

    internal sealed class IdSet : ISessionLogContentIds
    {
        private readonly HashSet<string> ids;

        public IdSet(params string[] ids) => this.ids = new HashSet<string>(ids, StringComparer.Ordinal);

        public bool Contains(string id) => id != null && ids.Contains(id);
    }

    /// <summary>A time-scale source of a development build: reports its own display factor.</summary>
    internal sealed class FlagScale : ITimeScaleSource, ISessionLogTimeScale
    {
        public long Factor = 1;
        public double Scale { get; set; } = 1d;
        public long DisplayFactor => Factor;
        public void ReportEffectiveMultiplier(double effectiveMultiplier) { }
    }

    /// <summary>A development source like DevTools: a selected step plus an effective factor that can jitter inside the step (T366).</summary>
    internal sealed class StepScale : ITimeScaleSource, ISessionLogTimeScale, ISessionLogTimeScaleStep
    {
        public long Factor = 1;
        public int Step;
        public double Scale { get; set; } = 1d;
        public long DisplayFactor => Factor;
        public int SelectedStep => Step;
        public void ReportEffectiveMultiplier(double effectiveMultiplier) { }
    }

    /// <summary>A time-scale source that is not a flag source (only the host's <see cref="ITimeScaleSource.Scale"/>).</summary>
    internal sealed class PlainScale : ITimeScaleSource
    {
        public double Scale { get; set; } = 1d;
        public void ReportEffectiveMultiplier(double effectiveMultiplier) { }
    }

    /// <summary>
    /// An in-memory file layer with a call log and fault injection by operation name ("Append", "Truncate", "ReadAll", "WriteNew", "Move", "Delete",
    /// "EnsureDirectory", "List", "Length"). Never touches the disk. Thread safe, so the serialization test can hammer it.
    /// </summary>
    internal sealed class InMemorySessionLogFiles : ISessionLogFiles
    {
        private readonly object gate = new object();
        private readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> written = new Dictionary<string, long>(StringComparer.Ordinal);
        private long tick;

        public readonly List<string> Log = new List<string>();
        public bool FailAll;
        public string FailOperation;
        public Exception Failure = new IOException("injected failure");
        public int AppendCalls;

        public byte[] Bytes(string path)
        {
            lock (gate) return files.TryGetValue(path, out byte[] data) ? (byte[])data.Clone() : null;
        }

        public void Put(string path, byte[] data)
        {
            lock (gate)
            {
                files[path] = (byte[])data.Clone();
                written[path] = ++tick;
            }
        }

        public bool Has(string path)
        {
            lock (gate) return files.ContainsKey(path);
        }

        public string[] Paths(string directory)
        {
            lock (gate) return files.Keys.Where(p => string.Equals(Path.GetDirectoryName(p), directory, StringComparison.Ordinal)).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        }

        private void Check(string operation, string path)
        {
            lock (gate) Log.Add(operation + " " + Path.GetFileName(path));
            if (FailAll || string.Equals(FailOperation, operation, StringComparison.Ordinal)) throw Failure;
        }

        public void EnsureDirectory(string directory) => Check("EnsureDirectory", directory);

        public bool Exists(string path)
        {
            lock (gate) return files.ContainsKey(path);
        }

        public long Length(string path)
        {
            Check("Length", path);
            lock (gate) return files.TryGetValue(path, out byte[] data) ? data.Length : -1L;
        }

        public long LastWriteUtcTicks(string path)
        {
            lock (gate) return written.TryGetValue(path, out long ticks) ? ticks : 0L;
        }

        public void Append(string path, byte[] buffer, int offset, int count)
        {
            Check("Append", path);
            lock (gate)
            {
                AppendCalls++;
                byte[] old = files.TryGetValue(path, out byte[] data) ? data : new byte[0];
                var next = new byte[old.Length + count];
                Buffer.BlockCopy(old, 0, next, 0, old.Length);
                Buffer.BlockCopy(buffer, offset, next, old.Length, count);
                files[path] = next;
                written[path] = ++tick;
            }
        }

        public void Truncate(string path)
        {
            Check("Truncate", path);
            lock (gate)
            {
                files[path] = new byte[0];
                written[path] = ++tick;
            }
        }

        public byte[] ReadAll(string path)
        {
            Check("ReadAll", path);
            lock (gate) return files.TryGetValue(path, out byte[] data) ? (byte[])data.Clone() : null;
        }

        public int ReadLastByte(string path)
        {
            Check("ReadLastByte", path);
            lock (gate) return files.TryGetValue(path, out byte[] data) && data.Length > 0 ? data[data.Length - 1] : -1;
        }

        public void WriteNew(string path, byte[] buffer, int count)
        {
            Check("WriteNew", path);
            lock (gate)
            {
                var copy = new byte[count];
                Buffer.BlockCopy(buffer, 0, copy, 0, count);
                files[path] = copy;
                written[path] = ++tick;
            }
        }

        public void Move(string from, string to)
        {
            Check("Move", from);
            lock (gate)
            {
                if (!files.ContainsKey(from)) throw new FileNotFoundException("missing");
                if (files.ContainsKey(to)) throw new IOException("exists");
                files[to] = files[from];
                written[to] = ++tick;
                files.Remove(from);
                written.Remove(from);
            }
        }

        public void Delete(string path)
        {
            Check("Delete", path);
            lock (gate)
            {
                files.Remove(path);
                written.Remove(path);
            }
        }

        public string[] List(string directory, string searchPattern)
        {
            Check("List", directory);
            int star = searchPattern.IndexOf('*');
            string prefix = searchPattern.Substring(0, star);
            string suffix = searchPattern.Substring(star + 1);
            lock (gate)
            {
                return files.Keys.Where(p =>
                {
                    string name = Path.GetFileName(p);
                    return string.Equals(Path.GetDirectoryName(p), directory, StringComparison.Ordinal) &&
                           name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(suffix, StringComparison.Ordinal);
                }).ToArray();
            }
        }
    }

    /// <summary>A writer wired to in-memory files, a hand-moved clock and a background runner the test controls.</summary>
    internal sealed class Harness
    {
        public static readonly string[] CatalogIds =
        {
            "iron.ore", "iron.bar", "iron.plate", "iron.gear", "machine.miner", "machine.smelter", "contract.deliver", "contract.produce",
            "industry.cash", "industry.speed", "item.boost", "research.speed", "storage", "factory",
        };

        public InMemorySessionLogFiles Files = new InMemorySessionLogFiles();
        public readonly FakePrefs Prefs = new FakePrefs();
        public readonly List<Action> Queue = new List<Action>();
        public bool RunInline;
        public long NowMs = 10_000_000L;
        public SessionLogWriter Writer;
        public readonly string Root = Path.Combine(Path.GetTempPath(), "session-log-in-memory-root");

        public static Harness Create(Action<SessionLogOptions> tweak = null, bool withIds = true, InMemorySessionLogFiles files = null)
        {
            var h = new Harness();
            if (files != null) h.Files = files;
            var options = new SessionLogOptions
            {
                Root = h.Root,
                Prefs = h.Prefs,
                Files = h.Files,
                ClockMs = () => h.NowMs,
                Background = job =>
                {
                    if (h.RunInline) job();
                    else h.Queue.Add(job);
                },
                ContentIds = withIds ? new IdSet(CatalogIds) : null,
                FillRandom = bytes =>
                {
                    for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i + 1);
                },
                BuildKind = RunBuildKind.Editor,
                PlatformClass = RunPlatformClass.Desktop,
                ContentRevision = 3,
            };
            tweak?.Invoke(options);
            h.Writer = new SessionLogWriter(options);
            return h;
        }

        public string Segment(int index) => Path.Combine(Writer.LogDirectoryPath, SessionLogWriter.SegmentName(index));

        /// <summary>The records of the lines of one segment whose CRC holds, in order.</summary>
        public List<Dictionary<string, object>> Records(int segment) => Parse(Files.Bytes(Segment(segment)));

        /// <summary>The valid records of both segments, the older segment first.</summary>
        public List<Dictionary<string, object>> AllRecords()
        {
            var result = new List<Dictionary<string, object>>();
            int first = Files.LastWriteUtcTicks(Segment(0)) <= Files.LastWriteUtcTicks(Segment(1)) ? 0 : 1;
            result.AddRange(Records(first));
            result.AddRange(Records(1 - first));
            return result;
        }

        public static List<Dictionary<string, object>> Parse(byte[] bytes)
        {
            var records = new List<Dictionary<string, object>>();
            if (bytes == null) return records;
            int start = 0;
            while (start < bytes.Length)
            {
                int end = start;
                while (end < bytes.Length && bytes[end] != (byte)'\n') end++;
                if (SessionLogLines.Validate(bytes, start, end - start))
                    records.Add(MiniJson.ParseObject(Encoding.ASCII.GetString(bytes, start, end - start)));
                start = end + 1;
            }

            return records;
        }

        public static List<Dictionary<string, object>> OfType(IEnumerable<Dictionary<string, object>> records, string type) =>
            records.Where(r => (string)r["t"] == type).ToList();

        public void Flush() => Writer.Flush();
    }

    /// <summary>A minimal JSON reader for the lines of the session log: objects, arrays, strings without escapes, integers and booleans. Anything else throws.</summary>
    internal static class MiniJson
    {
        public static Dictionary<string, object> ParseObject(string text)
        {
            int i = 0;
            object value = Value(text, ref i);
            if (i != text.Length) throw new FormatException("trailing text");
            return (Dictionary<string, object>)value;
        }

        private static object Value(string s, ref int i)
        {
            if (i >= s.Length) throw new FormatException("end");
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            int start = i;
            if (c == '-') i++;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
            if (i == start || (i == start + 1 && c == '-')) throw new FormatException("number at " + start);
            return long.Parse(s.Substring(start, i - start), System.Globalization.CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, object> Obj(string s, ref int i)
        {
            var map = new Dictionary<string, object>(StringComparer.Ordinal);
            i++;
            if (s[i] == '}') { i++; return map; }
            while (true)
            {
                string key = Str(s, ref i);
                if (s[i++] != ':') throw new FormatException("colon");
                if (map.ContainsKey(key)) throw new FormatException("duplicate member " + key);
                map[key] = Value(s, ref i);
                char next = s[i++];
                if (next == '}') return map;
                if (next != ',') throw new FormatException("comma");
            }
        }

        private static List<object> Arr(string s, ref int i)
        {
            var list = new List<object>();
            i++;
            if (s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(Value(s, ref i));
                char next = s[i++];
                if (next == ']') return list;
                if (next != ',') throw new FormatException("comma");
            }
        }

        private static string Str(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("quote");
            int start = ++i;
            while (s[i] != '"')
            {
                if (s[i] == '\\') throw new FormatException("escape sequences are never written");
                i++;
            }

            string value = s.Substring(start, i - start);
            i++;
            return value;
        }
    }
}
