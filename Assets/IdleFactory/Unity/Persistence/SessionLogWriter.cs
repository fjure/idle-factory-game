using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Bootstrap;

namespace IdleFactory.Unity.Persistence
{
    /// <summary>The seams and settings of a <see cref="SessionLogWriter"/> (contracts/session-log.md, Test seams). Everything but <see cref="Root"/> has a product default.</summary>
    public sealed class SessionLogOptions
    {
        /// <summary>The save root (<c>PersistenceRoots.Resolve</c>); the log lives in <c>&lt;Root&gt;/IdleFactory/SessionLog/</c>, exports in <c>&lt;Root&gt;/IdleFactory/Exports/</c>.</summary>
        public string Root { get; set; }

        /// <summary>The recording setting. Default: <see cref="PlayerPrefsSessionLogPrefs"/>.</summary>
        public ISessionLogPrefs Prefs { get; set; }

        /// <summary>The file layer. Default: <see cref="SessionLogFileSystem"/>.</summary>
        public ISessionLogFiles Files { get; set; }

        /// <summary>A monotonic millisecond clock for the 30 s flush interval and the per-minute roll-up. Default: <see cref="System.Diagnostics.Stopwatch"/>.</summary>
        public Func<long> ClockMs { get; set; }

        /// <summary>Runs a flush job off the calling thread. Default: the thread pool. A test injects an inline runner.</summary>
        public Action<Action> Background { get; set; }

        /// <summary>The catalog's ID set (S3); null checks the strict pattern only.</summary>
        public ISessionLogContentIds ContentIds { get; set; }

        /// <summary>The host's time-scale source (null in a release build and when the host has none). Read for the header only (D-I).</summary>
        public Func<ITimeScaleSource> TimeScale { get; set; }

        public RunBuildKind BuildKind { get; set; } = RunBuildKind.Release;

        public RunPlatformClass PlatformClass { get; set; } = RunPlatformClass.Desktop;

        public int ContentRevision { get; set; } = 1;

        /// <summary>Fills 16 bytes with random data. Default: <see cref="RandomNumberGenerator"/> (SEC-16). A test injects fixed bytes.</summary>
        public Action<byte[]> FillRandom { get; set; }
    }

    /// <summary>
    /// The Unity adapter of the session log (T338, F5-R12, FR-028 to FR-031, contracts/session-log.md): the <see cref="IRunEventSink"/> a
    /// <see cref="RunSession"/> is always constructed with. It never alters simulation, save or timing: every method catches everything it can
    /// throw, counts a <see cref="DroppedEvents"/> instead, and returns nothing.
    ///
    /// Path of an event: validate and serialize by hand into a fixed buffer under one gate, append to an in-memory ring (oldest lines dropped
    /// first) and return. No allocation, no I/O, no <c>PlayerPrefs</c> read. Sales and mining are rolled up per resource and minute in fixed
    /// slots. Flushing drains the ring into two rotating ~128 KB segments of CRC-32 JSON lines: at lifecycle points (synchronously, on the
    /// lifecycle callback) and otherwise at most every 30 s through <see cref="Tick"/>, on a background thread.
    ///
    /// Locking: <c>_ioGate</c> serializes everything that touches the files (flush, clear, toggle, export); <c>_gate</c> guards the in-memory
    /// state and is the only lock the event path takes. The order is always <c>_ioGate</c> then <c>_gate</c>.
    ///
    /// Header: a snapshot at session start (A3). A development build whose time-scale factor changes mid-session appends a further Header record
    /// of the same session (D-I), so an accelerated stretch is never read as real time. Recording off: events return without a count (A1).
    /// </summary>
    public sealed class SessionLogWriter : IRunEventSink
    {
        public const int SegmentCapacityBytes = 128 * 1024;
        public const int RingCapacityBytes = 64 * 1024;
        public const int MaxBufferedLines = 512;
        public const long FlushIntervalMs = 30_000L;
        public const int MaxRollupResources = 16;
        public const string ApplicationDirectory = "IdleFactory";
        public const string LogDirectory = "SessionLog";
        public const string ExportDirectory = "Exports";

        private const long MillisecondsPerMinute = 60_000L;

        private readonly object _gate = new object();
        private readonly object _ioGate = new object();
        private readonly ISessionLogPrefs _prefs;
        private readonly ISessionLogFiles _files;
        private readonly Func<long> _clock;
        private readonly Action<Action> _background;
        private readonly ISessionLogContentIds _ids;
        private readonly Func<ITimeScaleSource> _timeScale;
        private readonly RunBuildKind _buildKind;
        private readonly RunPlatformClass _platformClass;
        private readonly int _contentRevision;
        private readonly string _logDirectory;
        private readonly string _exportDirectory;
        private readonly string[] _segmentPaths;
        private readonly Action _flushJob;
        private readonly SessionLogLineBuilder _line = new SessionLogLineBuilder();

        // In-memory ring of whole lines (guarded by _gate).
        private readonly byte[] _ring = new byte[RingCapacityBytes];
        private readonly int[] _lineLengths = new int[MaxBufferedLines];
        private int _ringHead;
        private int _ringBytes;
        private int _lineHead;
        private int _lineCount;

        // Drained copy handed to the file layer (guarded by _ioGate).
        private readonly byte[] _flushBuffer = new byte[RingCapacityBytes];

        // Per resource and minute roll-ups (guarded by _gate).
        private readonly string[] _saleIds = new string[MaxRollupResources];
        private readonly bool[] _saleManual = new bool[MaxRollupResources];
        private readonly long[] _saleUnits = new long[MaxRollupResources];
        private readonly long[] _saleCash = new long[MaxRollupResources];
        private int _saleCount;
        private long _saleMinute;
        private readonly string[] _mineIds = new string[MaxRollupResources];
        private readonly long[] _mineCounted = new long[MaxRollupResources];
        private readonly long[] _mineIgnored = new long[MaxRollupResources];
        private readonly long[] _mineBlocked = new long[MaxRollupResources];
        private readonly long[] _mineOre = new long[MaxRollupResources];
        private int _mineCount;
        private long _mineMinute;

        private readonly ulong _sessionHigh;
        private readonly ulong _sessionLow;
        private bool _enabled;
        private bool _headerWritten;
        private int _headerFactor = 1;
        private volatile bool _dirty;
        private int _milestoneSerial;
        private long _dropped;
        private long _lastFlushMs;
        private int _flushQueued;

        // Segment state (guarded by _ioGate).
        private bool _opened;
        private int _activeSegment;
        private long _activeLength;

        public SessionLogWriter(SessionLogOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(options.Root)) throw new ArgumentException("The session log needs a root directory.", nameof(options));

            _prefs = options.Prefs ?? new PlayerPrefsSessionLogPrefs();
            _files = options.Files ?? new SessionLogFileSystem();
            _clock = options.ClockMs ?? MonotonicMilliseconds;
            _background = options.Background ?? RunOnThreadPool;
            _ids = options.ContentIds;
            _timeScale = options.TimeScale;
            _buildKind = options.BuildKind;
            _platformClass = options.PlatformClass;
            _contentRevision = options.ContentRevision < 1 ? 1 : options.ContentRevision;

            string application = Path.Combine(options.Root, ApplicationDirectory);
            _logDirectory = Path.Combine(application, LogDirectory);
            _exportDirectory = Path.Combine(application, ExportDirectory);
            _segmentPaths = new[] { Path.Combine(_logDirectory, SegmentName(0)), Path.Combine(_logDirectory, SegmentName(1)) };
            _flushJob = BackgroundFlush;

            bool enabled = true;
            try { enabled = _prefs.Enabled; }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { enabled = true; }
            _enabled = enabled;

            var bytes = new byte[16];
            try
            {
                if (options.FillRandom != null) options.FillRandom(bytes);
                else using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                // No random source: an all-zero ID, never one derived from device data (SEC-16).
                bytes = new byte[16];
                _dropped++;
            }

            _sessionHigh = ReadBigEndian(bytes, 0);
            _sessionLow = ReadBigEndian(bytes, 8);
            _lastFlushMs = SafeClock();
        }

        public static string SegmentName(int index) => "segment-" + index + ".jsonl";

        /// <summary>Whether recording is on.</summary>
        public bool Enabled
        {
            get { lock (_gate) return _enabled; }
        }

        /// <summary>The events and lines this writer lost: failed serialization or ID validation, ring overflow, failed file writes. Thread safe.</summary>
        public long DroppedEvents => Interlocked.Read(ref _dropped);

        /// <summary>The session's 128 random bits.</summary>
        public RunSessionId SessionId => new RunSessionId(_sessionHigh, _sessionLow);

        /// <summary>Counts every Milestone record; the sampler compares it to take a sample at a milestone.</summary>
        public int MilestoneSerial => Volatile.Read(ref _milestoneSerial);

        /// <summary>The directory of the two segments.</summary>
        public string LogDirectoryPath => _logDirectory;

        /// <summary>The directory of the exports. Shown on screen only (SEC-2).</summary>
        public string ExportDirectoryPath => _exportDirectory;

        // -----------------------------------------------------------------------------------------------------
        // IRunEventSink
        // -----------------------------------------------------------------------------------------------------

        public void OnHeader(in RunHeaderEvent e)
        {
            try
            {
                lock (_gate)
                {
                    if (!_enabled) return;
                    int factor = e.TimeScale < 1 ? 1 : e.TimeScale;
                    WriteHeaderRecord(e.Schema, e.ContentRevision, e.BuildKind, e.PlatformClass, factor, e.SessionId.High, e.SessionId.Low);
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { Drop(); }
        }

        public void OnSample(in RunSampleEvent e)
        {
            try
            {
                lock (_gate)
                {
                    if (!_enabled) return;
                    NoteTimeScale();
                    _line.Begin("sample");
                    _line.Number("playSeconds", e.PlaySeconds);
                    _line.Number("factoryValue", e.FactoryValue);
                    _line.Number("cash", e.Cash);
                    _line.BeginArray("machines");
                    var machines = e.Machines;
                    int count = machines == null ? 0 : machines.Count;
                    for (int i = 0; i < count; i++)
                    {
                        RunMachineCount m = machines[i];
                        _line.BeginElement();
                        _line.Id("machineId", m.MachineId, _ids);
                        _line.Number("level", m.Level);
                        _line.Number("count", m.Count);
                        _line.EndElement();
                    }

                    _line.EndArray();
                    _line.Number("storageLevel", e.StorageLevel);
                    _line.Number("factoryLevel", e.FactoryLevel);
                    Commit();
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { Drop(); }
        }

        public void OnMilestone(in RunMilestoneEvent e)
        {
            try
            {
                lock (_gate)
                {
                    if (!_enabled) return;
                    NoteTimeScale();
                    _line.Begin("milestone");
                    _line.Token("kind", SessionLogTokens.Milestone(e.Kind));
                    _line.Number("playSeconds", e.PlaySeconds);
                    if (Commit()) Interlocked.Increment(ref _milestoneSerial);
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { Drop(); }
        }

        public void OnSale(in RunSaleEvent e)
        {
            try
            {
                lock (_gate)
                {
                    if (!_enabled) return;
                    NoteTimeScale();
                    RollSales(e.ResourceId, e.Units, e.Cash, e.Manual);
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { Drop(); }
        }

        public void OnMining(in RunMiningEvent e)
        {
            try
            {
                lock (_gate)
                {
                    if (!_enabled) return;
                    NoteTimeScale();
                    RollMining(e.ResourceId, e.Counted, e.Ignored, e.Blocked, e.Ore);
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { Drop(); }
        }

        public void OnContract(in RunContractEvent e)
        {
            try
            {
                lock (_gate)
                {
                    if (!_enabled) return;
                    NoteTimeScale();
                    _line.Begin("contract");
                    _line.Token("event", SessionLogTokens.Contract(e.Kind));
                    _line.Id("offerId", e.OfferId, _ids);
                    _line.Number("index", e.Index);
                    _line.Number("required", e.Required);
                    _line.Number("reward", e.Reward);
                    _line.Number("ratioPpm", e.RatioPpm);
                    Commit();
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { Drop(); }
        }

        public void OnPrestige(in RunPrestigeEvent e)
        {
            try
            {
                lock (_gate)
                {
                    if (!_enabled) return;
                    NoteTimeScale();
                    _line.Begin("prestige");
                    _line.Number("factoryValue", e.FactoryValue);
                    _line.Number("threshold", e.Threshold);
                    _line.Number("ip", e.Ip);
                    _line.Number("secondsSinceEligible", e.SecondsSinceEligible);
                    _line.BeginArray("industryPurchases");
                    var purchases = e.IndustryPurchases;
                    int count = purchases == null ? 0 : purchases.Count;
                    for (int i = 0; i < count; i++)
                    {
                        RunIndustryLevel p = purchases[i];
                        _line.BeginElement();
                        _line.Id("industryId", p.IndustryId, _ids);
                        _line.Number("level", p.Level);
                        _line.EndElement();
                    }

                    _line.EndArray();
                    Commit();
                }

                Flush(); // a lifecycle point (T364): the prestige is on disk before the next frame
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { Drop(); }
        }

        public void OnOfflineReturn(in RunOfflineReturnEvent e)
        {
            try
            {
                lock (_gate)
                {
                    if (!_enabled) return;
                    NoteTimeScale();
                    _line.Begin("offlineReturn");
                    _line.Number("absenceMs", e.AbsenceMs);
                    _line.Number("creditedMs", e.CreditedMs);
                    _line.Bool("capHit", e.CapHit);
                    _line.Number("completions", e.Completions);
                    _line.Number("storeFullStallSeconds", e.StoreFullStallSeconds);
                    _line.Number("soldUnits", e.SoldUnits);
                    _line.Number("cashDelta", e.CashDelta);
                    _line.Bool("reclassifiedLiveGap", e.ReclassifiedLiveGap);
                    Commit();
                }

                Flush(); // a lifecycle point (T364, T338 QA W1): the return and the records raised before it are on disk at once
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { Drop(); }
        }

        public void OnBottleneck(in RunBottleneckEvent e)
        {
            try
            {
                lock (_gate)
                {
                    if (!_enabled) return;
                    NoteTimeScale();
                    _line.Begin("bottleneck");
                    _line.Id("resourceId", e.ResourceId, _ids);
                    _line.Number("missingInputSeconds", e.MissingInputSeconds);
                    _line.Number("storeFullSeconds", e.StoreFullSeconds);
                    Commit();
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { Drop(); }
        }

        public void OnPurchase(in RunPurchaseEvent e)
        {
            try
            {
                lock (_gate)
                {
                    if (!_enabled) return;
                    NoteTimeScale();
                    _line.Begin("purchase");
                    _line.Token("kind", SessionLogTokens.Purchase(e.Kind));
                    _line.Id("targetId", e.TargetId, _ids);
                    _line.Number("level", e.Level);
                    _line.Number("cost", e.Cost);
                    _line.Number("secondsSinceAffordable", e.SecondsSinceAffordable);
                    Commit();
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { Drop(); }
        }

        public void OnClock(in RunClockEvent e)
        {
            try
            {
                lock (_gate)
                {
                    if (!_enabled) return;
                    NoteTimeScale();
                    _line.Begin("clock");
                    _line.Token("kind", SessionLogTokens.Clock(e.Kind));
                    _line.Number("deltaMs", e.DeltaMs);
                    _line.Bool("reclassified", e.Reclassified);
                    Commit();
                }

                if (e.Kind == RunClockKind.LiveGap) Flush(); // the end of a live-gap return (T364)
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { Drop(); }
        }

        /// <summary>
        /// A lifecycle record, then a synchronous flush: start, focus-loss checkpoint and quit are lifecycle points (FR-028). The record's
        /// <c>droppedEvents</c> is the session guard's count plus this writer's own drops. <see cref="RunLifecycleKind.Start"/> also writes the Header
        /// when this session has none yet.
        /// </summary>
        public void OnLifecycle(in RunLifecycleEvent e) => RecordLifecycle(e.Kind, e.DroppedEvents);

        // -----------------------------------------------------------------------------------------------------
        // Lifecycle, toggle, clear
        // -----------------------------------------------------------------------------------------------------

        /// <summary>Starts the session: opens the segments, writes the Header and the start record, and flushes. Never throws.</summary>
        public void Start() => RecordLifecycle(RunLifecycleKind.Start, 0);

        /// <summary>Writes a lifecycle record and flushes synchronously (call it from the lifecycle callback). Never throws.</summary>
        public void RecordLifecycle(RunLifecycleKind kind, int sessionDroppedEvents)
        {
            try
            {
                lock (_ioGate)
                {
                    lock (_gate)
                    {
                        if (!_enabled) return;
                        if (kind == RunLifecycleKind.Start && !_headerWritten) WriteHeaderRecord(SessionLogLines.SchemaVersion, _contentRevision, _buildKind, _platformClass, ResolveTimeScale(), _sessionHigh, _sessionLow);
                        WriteLifecycleRecord(kind, sessionDroppedEvents);
                    }

                    FlushLocked();
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { Drop(); }
        }

        /// <summary>
        /// Switches recording on or off and persists the setting. Turning it off writes a <c>logToggled</c> record first and flushes, so the log
        /// shows where recording stopped; turning it on writes one after it starts. Serialized with flush and clear. Never throws.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            try
            {
                lock (_ioGate)
                {
                    bool was;
                    lock (_gate) was = _enabled;
                    if (was == enabled) return;

                    if (!enabled)
                    {
                        lock (_gate) WriteLifecycleRecord(RunLifecycleKind.LogToggled, 0);
                        FlushLocked();
                        lock (_gate) _enabled = false;
                    }
                    else
                    {
                        lock (_gate)
                        {
                            _enabled = true;
                            if (!_headerWritten) WriteHeaderRecord(SessionLogLines.SchemaVersion, _contentRevision, _buildKind, _platformClass, ResolveTimeScale(), _sessionHigh, _sessionLow);
                            WriteLifecycleRecord(RunLifecycleKind.LogToggled, 0);
                        }

                        FlushLocked();
                    }

                    try { _prefs.Enabled = enabled; }
                    catch (Exception ex) when (!(ex is OutOfMemoryException)) { Drop(); }
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { Drop(); }
        }

        /// <summary>
        /// Truncates both segments and discards what is buffered. While recording is on it then writes the session's Header and a
        /// <c>cleared</c> lifecycle record, so the log still starts with its context; while it is off nothing is recorded. Returns false when the
        /// files could not be truncated (counted as a drop; the buffer is cleared either way). Serialized with flush, toggle and export.
        /// </summary>
        public bool Clear()
        {
            try
            {
                lock (_ioGate)
                {
                    lock (_gate)
                    {
                        ResetRing();
                        ResetRollups();
                    }

                    _files.EnsureDirectory(_logDirectory);
                    _files.Truncate(_segmentPaths[0]);
                    _files.Truncate(_segmentPaths[1]);
                    _activeSegment = 0;
                    _activeLength = 0;
                    _opened = true;

                    bool on;
                    lock (_gate)
                    {
                        on = _enabled;
                        if (on)
                        {
                            WriteHeaderRecord(SessionLogLines.SchemaVersion, _contentRevision, _buildKind, _platformClass, ResolveTimeScale(), _sessionHigh, _sessionLow);
                            WriteLifecycleRecord(RunLifecycleKind.Cleared, 0);
                        }
                    }

                    if (on) FlushLocked();
                    return true;
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                Drop();
                return false;
            }
        }

        // -----------------------------------------------------------------------------------------------------
        // Flush
        // -----------------------------------------------------------------------------------------------------

        /// <summary>Flushes synchronously. For lifecycle points; the periodic flush goes through <see cref="Tick"/>. Never throws.</summary>
        public void Flush()
        {
            try
            {
                lock (_ioGate) FlushLocked();
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { Drop(); }
        }

        /// <summary>
        /// Call once per frame: compares the clock and, when something is buffered and 30 s have passed since the last flush, queues one flush on
        /// the background runner. Costs two reads and a comparison otherwise. Never throws.
        /// </summary>
        public void Tick()
        {
            try
            {
                if (!_dirty) return;
                long now = _clock();
                if (now - Volatile.Read(ref _lastFlushMs) < FlushIntervalMs) return;
                if (Interlocked.CompareExchange(ref _flushQueued, 1, 0) != 0) return;
                try { _background(_flushJob); }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    Interlocked.Exchange(ref _flushQueued, 0);
                    Drop();
                }
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { Drop(); }
        }

        private void BackgroundFlush()
        {
            try
            {
                Flush();
            }
            finally
            {
                Interlocked.Exchange(ref _flushQueued, 0);
            }
        }

        // Caller holds _ioGate.
        private void FlushLocked()
        {
            int bytes;
            int lines;
            lock (_gate)
            {
                EmitRollups();
                lines = _lineCount;
                bytes = DrainRing();
                _dirty = false;
            }

            Volatile.Write(ref _lastFlushMs, SafeClock());
            if (bytes == 0) return;

            try
            {
                WriteSegments(bytes);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                Interlocked.Add(ref _dropped, lines);
            }
        }

        // Caller holds _ioGate.
        private void WriteSegments(int bytes)
        {
            if (!_opened) OpenSegments();
            if (_activeLength + bytes > SegmentCapacityBytes)
            {
                int other = 1 - _activeSegment;
                _files.Truncate(_segmentPaths[other]);
                _activeSegment = other;
                _activeLength = 0;
            }

            _files.Append(_segmentPaths[_activeSegment], _flushBuffer, 0, bytes);
            _activeLength += bytes;
        }

        // Caller holds _ioGate. Picks the newer segment as the active one, and ends a torn last line with a newline so the next record starts clean.
        private void OpenSegments()
        {
            _files.EnsureDirectory(_logDirectory);
            long length0 = _files.Length(_segmentPaths[0]);
            long length1 = _files.Length(_segmentPaths[1]);
            int active;
            if (length0 < 0 && length1 < 0) active = 0;
            else if (length1 < 0) active = 0;
            else if (length0 < 0) active = 1;
            else active = _files.LastWriteUtcTicks(_segmentPaths[1]) > _files.LastWriteUtcTicks(_segmentPaths[0]) ? 1 : 0;

            long length = active == 0 ? length0 : length1;
            if (length < 0) length = 0;
            if (length > 0)
            {
                // Only the last byte decides whether the tail is torn (T364): the segment is never read whole at start-up.
                int last = _files.ReadLastByte(_segmentPaths[active]);
                if (last >= 0 && last != (byte)'\n')
                {
                    _files.Append(_segmentPaths[active], NewlineBytes, 0, 1);
                    length++;
                }
            }

            _activeSegment = active;
            _activeLength = length;
            _opened = true;
        }

        private static readonly byte[] NewlineBytes = { (byte)'\n' };

        // -----------------------------------------------------------------------------------------------------
        // Export support (SessionLogExport)
        // -----------------------------------------------------------------------------------------------------

        /// <summary>
        /// Runs <paramref name="read"/> with the files quiescent: buffered lines are flushed first and no flush, clear or toggle runs until it
        /// returns. <paramref name="read"/> receives the segment paths from the older to the newer one.
        /// </summary>
        internal T WithFlushedSegments<T>(Func<string[], T> read)
        {
            lock (_ioGate)
            {
                FlushLocked();
                if (!_opened) OpenSegments();
                string[] ordered = _activeSegment == 0
                    ? new[] { _segmentPaths[1], _segmentPaths[0] }
                    : new[] { _segmentPaths[0], _segmentPaths[1] };
                return read(ordered);
            }
        }

        // -----------------------------------------------------------------------------------------------------
        // Records and ring (callers hold _gate)
        // -----------------------------------------------------------------------------------------------------

        private void WriteHeaderRecord(int schema, int contentRevision, RunBuildKind buildKind, RunPlatformClass platformClass, int timeScale, ulong high, ulong low)
        {
            _line.Begin("header");
            _line.Number("schema", schema);
            _line.Number("contentRevision", contentRevision);
            _line.Token("buildKind", SessionLogTokens.BuildKind(buildKind));
            _line.Token("platformClass", SessionLogTokens.PlatformClass(platformClass));
            _line.Number("timeScale", timeScale);
            _line.SessionId("sessionId", high, low);
            if (Commit())
            {
                _headerWritten = true;
                _headerFactor = timeScale;
            }
        }

        private void WriteLifecycleRecord(RunLifecycleKind kind, int sessionDroppedEvents)
        {
            long total = (long)Math.Max(0, sessionDroppedEvents) + Interlocked.Read(ref _dropped);
            _line.Begin("lifecycle");
            _line.Token("event", SessionLogTokens.Lifecycle(kind));
            _line.Number("droppedEvents", total > int.MaxValue ? int.MaxValue : total);
            Commit();
        }

        /// <summary>Appends a header when a development build's time-scale factor changed since the last one (D-I). A release build has no source and skips at once.</summary>
        private void NoteTimeScale()
        {
            if (_timeScale == null || !_headerWritten) return;
            int factor = ResolveTimeScale();
            if (factor == _headerFactor) return;
            WriteHeaderRecord(SessionLogLines.SchemaVersion, _contentRevision, _buildKind, _platformClass, factor, _sessionHigh, _sessionLow);
        }

        private int ResolveTimeScale()
        {
            if (_timeScale == null) return 1;
            try
            {
                ITimeScaleSource source = _timeScale();
                if (source == null) return 1;
                long factor;
                if (source is ISessionLogTimeScale flag)
                {
                    factor = flag.DisplayFactor;
                }
                else
                {
                    double scale = source.Scale;
                    if (double.IsNaN(scale) || scale < 1d) factor = 1;
                    else if (scale >= int.MaxValue) factor = int.MaxValue; // also the Max factor (+infinity): never cast
                    else factor = (long)Math.Round(scale);
                }

                return factor < 1 ? 1 : factor > int.MaxValue ? int.MaxValue : (int)factor;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return 1;
            }
        }

        /// <summary>Finishes the line in <see cref="_line"/> and buffers it. Returns false (and counts a drop) when the line was invalid.</summary>
        private bool Commit()
        {
            if (!_line.End())
            {
                Drop();
                return false;
            }

            AppendToRing(_line.Buffer, _line.Length);
            _dirty = true;
            return true;
        }

        private void AppendToRing(byte[] source, int length)
        {
            if (length > RingCapacityBytes)
            {
                Drop();
                return;
            }

            while (_ringBytes + length > RingCapacityBytes || _lineCount == MaxBufferedLines)
            {
                int oldest = _lineLengths[_lineHead];
                _lineHead = (_lineHead + 1) % MaxBufferedLines;
                _lineCount--;
                _ringHead = (_ringHead + oldest) % RingCapacityBytes;
                _ringBytes -= oldest;
                Drop();
            }

            int tail = (_ringHead + _ringBytes) % RingCapacityBytes;
            int first = Math.Min(length, RingCapacityBytes - tail);
            Buffer.BlockCopy(source, 0, _ring, tail, first);
            if (first < length) Buffer.BlockCopy(source, first, _ring, 0, length - first);
            _ringBytes += length;
            _lineLengths[(_lineHead + _lineCount) % MaxBufferedLines] = length;
            _lineCount++;
        }

        /// <summary>Moves the ring's lines into <see cref="_flushBuffer"/> in order and empties the ring. Returns the byte count.</summary>
        private int DrainRing()
        {
            int bytes = _ringBytes;
            if (bytes > 0)
            {
                int first = Math.Min(bytes, RingCapacityBytes - _ringHead);
                Buffer.BlockCopy(_ring, _ringHead, _flushBuffer, 0, first);
                if (first < bytes) Buffer.BlockCopy(_ring, 0, _flushBuffer, first, bytes - first);
            }

            ResetRing();
            return bytes;
        }

        private void ResetRing()
        {
            _ringHead = 0;
            _ringBytes = 0;
            _lineHead = 0;
            _lineCount = 0;
        }

        // -----------------------------------------------------------------------------------------------------
        // Per resource and minute roll-ups (callers hold _gate)
        // -----------------------------------------------------------------------------------------------------

        private bool ValidId(string id)
        {
            if (SessionLogIds.IsValid(id) && (_ids == null || _ids.Contains(id))) return true;
            Drop();
            return false;
        }

        private void RollSales(string resourceId, long units, long cash, bool manual)
        {
            if (!ValidId(resourceId)) return;
            long minute = SafeClock() / MillisecondsPerMinute;
            if (_saleCount > 0 && minute != _saleMinute) EmitSales();
            if (_saleCount == 0) _saleMinute = minute;

            int slot = -1;
            for (int i = 0; i < _saleCount && slot < 0; i++)
                if (_saleManual[i] == manual && string.Equals(_saleIds[i], resourceId, StringComparison.Ordinal)) slot = i;
            if (slot < 0)
            {
                if (_saleCount == MaxRollupResources)
                {
                    Drop();
                    return;
                }

                slot = _saleCount++;
                _saleIds[slot] = resourceId;
                _saleManual[slot] = manual;
                _saleUnits[slot] = 0;
                _saleCash[slot] = 0;
            }

            _saleUnits[slot] = SaturatingAdd(_saleUnits[slot], units);
            _saleCash[slot] = SaturatingAdd(_saleCash[slot], cash);
            _dirty = true;
        }

        private void RollMining(string resourceId, long counted, long ignored, long blocked, long ore)
        {
            if (!ValidId(resourceId)) return;
            long minute = SafeClock() / MillisecondsPerMinute;
            if (_mineCount > 0 && minute != _mineMinute) EmitMining();
            if (_mineCount == 0) _mineMinute = minute;

            int slot = -1;
            for (int i = 0; i < _mineCount && slot < 0; i++)
                if (string.Equals(_mineIds[i], resourceId, StringComparison.Ordinal)) slot = i;
            if (slot < 0)
            {
                if (_mineCount == MaxRollupResources)
                {
                    Drop();
                    return;
                }

                slot = _mineCount++;
                _mineIds[slot] = resourceId;
                _mineCounted[slot] = 0;
                _mineIgnored[slot] = 0;
                _mineBlocked[slot] = 0;
                _mineOre[slot] = 0;
            }

            _mineCounted[slot] = SaturatingAdd(_mineCounted[slot], counted);
            _mineIgnored[slot] = SaturatingAdd(_mineIgnored[slot], ignored);
            _mineBlocked[slot] = SaturatingAdd(_mineBlocked[slot], blocked);
            _mineOre[slot] = SaturatingAdd(_mineOre[slot], ore);
            _dirty = true;
        }

        private void EmitRollups()
        {
            EmitSales();
            EmitMining();
        }

        private void EmitSales()
        {
            for (int i = 0; i < _saleCount; i++)
            {
                _line.Begin("sale");
                _line.Id("resourceId", _saleIds[i], null);
                _line.Number("units", _saleUnits[i]);
                _line.Number("cash", _saleCash[i]);
                _line.Bool("manual", _saleManual[i]);
                Commit();
                _saleIds[i] = null;
            }

            _saleCount = 0;
        }

        private void EmitMining()
        {
            for (int i = 0; i < _mineCount; i++)
            {
                _line.Begin("mining");
                _line.Id("resourceId", _mineIds[i], null);
                _line.Number("counted", _mineCounted[i]);
                _line.Number("ignored", _mineIgnored[i]);
                _line.Number("blocked", _mineBlocked[i]);
                _line.Number("ore", _mineOre[i]);
                Commit();
                _mineIds[i] = null;
            }

            _mineCount = 0;
        }

        private void ResetRollups()
        {
            for (int i = 0; i < _saleCount; i++) _saleIds[i] = null;
            for (int i = 0; i < _mineCount; i++) _mineIds[i] = null;
            _saleCount = 0;
            _mineCount = 0;
        }

        private static long SaturatingAdd(long left, long right)
        {
            if (right > 0 && left > long.MaxValue - right) return long.MaxValue;
            if (right < 0 && left < long.MinValue - right) return long.MinValue;
            return left + right;
        }

        // -----------------------------------------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------------------------------------

        private void Drop() => Interlocked.Increment(ref _dropped);

        private long SafeClock()
        {
            try { return _clock(); }
            catch (Exception ex) when (!(ex is OutOfMemoryException)) { return 0L; }
        }

        private static long MonotonicMilliseconds() => System.Diagnostics.Stopwatch.GetTimestamp() * 1000L / System.Diagnostics.Stopwatch.Frequency;

        private static readonly WaitCallback RunJob = state => ((Action)state)();

        private static void RunOnThreadPool(Action job) => ThreadPool.QueueUserWorkItem(RunJob, job);

        private static ulong ReadBigEndian(byte[] bytes, int offset)
        {
            ulong value = 0;
            for (int i = 0; i < 8; i++) value = (value << 8) | bytes[offset + i];
            return value;
        }
    }
}
