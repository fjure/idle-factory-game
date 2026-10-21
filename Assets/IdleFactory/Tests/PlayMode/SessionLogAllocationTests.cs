using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T338 (PF-7, F5-R13): the session-log event path allocates nothing with logging on, and each flush stays inside an allocation and a maximum-milliseconds budget.
    /// The zero rule is measured with the <c>GC.Alloc</c> recorder (<see cref="GcAllocationCounter"/>), the only allocation counter that works in the Editor's Mono.
    /// The budgets are recorded in the log line <c>SESSIONLOG-FLUSH</c> so the Windows player and the Android emulator runs can be compared.
    /// </summary>
    public sealed class SessionLogAllocationTests
    {
        // PF-7 budgets of one flush of a full ring (about 38 KB, 300 lines, rotating the segments) through the real file layer. Measured 2026-10-21 on the Windows editor and
        // the Windows development player: max 3.8 ms and 140 GC.Alloc samples per flush over 20 flushes. Budget = about 1.8x the samples and 6x the milliseconds.
        // Android emulator calibration is T339's run.
        private const int FlushAllocationSampleBudget = 256;
        private const double FlushMaxMillisecondsBudget = 25d;
        private const int FlushIterations = 20;

        private sealed class MemoryPrefs : ISessionLogPrefs
        {
            public bool Enabled { get; set; } = true;
        }

        /// <summary>A file layer that does nothing, so the measurement sees the writer alone.</summary>
        private sealed class NullFiles : ISessionLogFiles
        {
            public int Appends;
            public void EnsureDirectory(string directory) { }
            public bool Exists(string path) => false;
            public long Length(string path) => -1L;
            public long LastWriteUtcTicks(string path) => 0L;
            public void Append(string path, byte[] buffer, int offset, int count) => Appends++;
            public void Truncate(string path) { }
            public byte[] ReadAll(string path) => null;
            public int ReadLastByte(string path) => -1;
            public void WriteNew(string path, byte[] buffer, int count) { }
            public void Move(string from, string to) { }
            public void Delete(string path) { }
            public string[] List(string directory, string searchPattern) => new string[0];
        }

        private static SessionLogWriter NewWriter(string root, ISessionLogFiles files, params string[] ids)
        {
            return new SessionLogWriter(new SessionLogOptions
            {
                Root = root,
                Prefs = new MemoryPrefs(),
                Files = files,
                ContentIds = ids == null || ids.Length == 0 ? null : new CatalogIds(ids),
                Background = job => { },
                BuildKind = RunBuildKind.Editor,
            });
        }

        private sealed class CatalogIds : ISessionLogContentIds
        {
            private readonly string[] ids;
            public CatalogIds(string[] ids) => this.ids = ids;
            public bool Contains(string id) => Array.IndexOf(ids, id) >= 0;
        }

        /// <summary>Raises the hot event kinds once through <paramref name="sink"/> with prebuilt records (a record's own construction is a raise-site cost, not the wrapper's).</summary>
        private static void RaiseHot(IRunEventSink sink, string resource, string offer, in RunSaleEvent sale, in RunMiningEvent mining, in RunContractEvent contract)
        {
            sink.OnSale(sale);
            sink.OnMining(mining);
            sink.OnContract(contract);
            sink.OnMilestone(new RunMilestoneEvent(RunMilestoneKind.FirstGear, 30));
            sink.OnClock(new RunClockEvent(RunClockKind.Backward, -5, false));
            sink.OnPurchase(new RunPurchaseEvent(RunPurchaseKind.Build, resource, 1, 10, 3));
            sink.OnBottleneck(new RunBottleneckEvent(resource, 1, 2));
            // OfflineReturn, Prestige and a live-gap Clock flush at once (T364), so they are not part of the hot event path measured here.
        }

        private static int CountAllocations(IRunEventSink sink, string resource, string offer, int repeats)
        {
            var sale = new RunSaleEvent(resource, 3, 9, true);
            var mining = new RunMiningEvent(resource, 4, 1, 0, 2);
            var contract = new RunContractEvent(RunContractEventKind.Accept, offer, 0, 5, 50, 1_000_000);
            for (int i = 0; i < 50; i++) RaiseHot(sink, resource, offer, sale, mining, contract); // warm-up: JIT and first-use costs
            return GcAllocationCounter.Count(() =>
            {
                for (int i = 0; i < repeats; i++) RaiseHot(sink, resource, offer, sale, mining, contract);
            });
        }

        private static int CountAdvanceAllocations(IRunEventSink sink)
        {
            string root = RunPlayerFixtures.NewRoot();
            try
            {
                RunRig rig = RunRig.Create(root, RunPlayerFixtures.ScaledCatalog(), RunPlayerFixtures.StartUtcMs, sink);
                rig.ColdStartOk(null);
                var built = rig.Commands.Build(0, "machine.miner");
                Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
                Assert.That(rig.Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
                const long frameTicks = 166_666;
                for (int i = 0; i < 200; i++) rig.Session.Advance(frameTicks); // warm-up: buffers, read model, JIT
                return GcAllocationCounter.Count(() =>
                {
                    for (int i = 0; i < 2000; i++) rig.Session.Advance(frameTicks);
                });
            }
            finally
            {
                RunPlayerFixtures.DeleteRoot(root);
            }
        }

        [Category("StandalonePlayer")]
        [Test]
        public void AdvanceWithAWritingSinkAttachedAndMachinesPresentAllocatesNothingBeyondTheNoSinkRun()
        {
            string logRoot = RunPlayerFixtures.NewRoot();
            try
            {
                SessionLogWriter writer = NewWriter(Path.Combine(logRoot, "log"), new NullFiles());
                writer.Start();
                int without = CountAdvanceAllocations(null);
                int with = CountAdvanceAllocations(writer);
                Debug.Log("SESSIONLOG-ADVANCE-ALLOC noSinkSamples=" + without + " sinkSamples=" + with + " advances=2000");
                Assert.That(with, Is.LessThanOrEqualTo(without), "the history meters and the Bottleneck accrual add no allocation to an advance (no sink: " + without + ")");
            }
            finally
            {
                RunPlayerFixtures.DeleteRoot(logRoot);
            }
        }

        [Test]
        public void TheRecorderSeesAnAllocationSoAZeroBelowMeansSomething()
        {
            object sink = null;
            Assert.That(GcAllocationCounter.Count(() => sink = new byte[1024]), Is.GreaterThan(0));
            GC.KeepAlive(sink);
        }

        [Category("StandalonePlayer")]
        [Test]
        public void TheWriterEventPathAllocatesNothingWithLoggingOn()
        {
            var files = new NullFiles();
            SessionLogWriter writer = NewWriter(Path.Combine(Application.temporaryCachePath, "session-log-alloc-null"), files, "iron.ore", "contract.deliver", "machine.miner");
            writer.Start();
            Assert.That(writer.Enabled, Is.True);

            int allocations = CountAllocations(writer, "iron.ore", "contract.deliver", 2000);

            Assert.That(allocations, Is.Zero, "no managed allocation on the event path (a full ring dropping its oldest lines allocates nothing either)");
        }

        [Category("StandalonePlayer")]
        [Test]
        public void TheWriterFlushItselfAllocatesNothingBeyondTheFileLayer()
        {
            var files = new NullFiles();
            SessionLogWriter writer = NewWriter(Path.Combine(Application.temporaryCachePath, "session-log-alloc-flush"), files, "iron.ore", "contract.deliver");
            writer.Start();
            var contract = new RunContractEvent(RunContractEventKind.Accept, "contract.deliver", 0, 5, 50, 1_000_000);
            for (int i = 0; i < 200; i++) writer.OnContract(contract);
            writer.Flush(); // warm-up

            for (int i = 0; i < 200; i++) writer.OnContract(contract);
            int allocations = GcAllocationCounter.Count(() => writer.Flush());

            Assert.That(allocations, Is.Zero, "drain, bookkeeping and rotation allocate nothing; only the injected file layer may");
            Assert.That(files.Appends, Is.GreaterThanOrEqualTo(2));
        }

        [Category("StandalonePlayer")]
        [Test]
        public void FlushThroughTheRealFileLayerStaysInTheAllocationAndMillisecondBudget()
        {
            string root = Path.Combine(Application.temporaryCachePath, "session-log-flush-" + Guid.NewGuid().ToString("N"));
            try
            {
                SessionLogWriter writer = NewWriter(root, new SessionLogFileSystem(), "iron.ore", "contract.deliver");
                writer.Start();
                var contract = new RunContractEvent(RunContractEventKind.Accept, "contract.deliver", 0, 5, 50, 1_000_000);
                for (int i = 0; i < 300; i++) writer.OnContract(contract);
                writer.Flush(); // warm-up: opens the segments

                double maxMs = 0d;
                int maxSamples = 0;
                long bytes = 0;
                for (int iteration = 0; iteration < FlushIterations; iteration++)
                {
                    for (int i = 0; i < 300; i++) writer.OnContract(contract); // about 38 KB, enough to rotate the 128 KB segments repeatedly
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    var clock = Stopwatch.StartNew();
                    int samples = GcAllocationCounter.Count(() => writer.Flush());
                    clock.Stop();
                    bytes = Math.Max(bytes, GC.GetAllocatedBytesForCurrentThread() - before);
                    maxMs = Math.Max(maxMs, clock.Elapsed.TotalMilliseconds);
                    maxSamples = Math.Max(maxSamples, samples);
                }

                Debug.Log("SESSIONLOG-FLUSH iterations=" + FlushIterations + " maxMs=" + maxMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                          " maxAllocSamples=" + maxSamples + " maxAllocBytes=" + bytes + " budgetMs=" + FlushMaxMillisecondsBudget + " budgetSamples=" + FlushAllocationSampleBudget +
                          " developmentBuild=" + Debug.isDebugBuild + " dropped=" + writer.DroppedEvents);

                Assert.That(writer.DroppedEvents, Is.Zero, "no flush failed");
                Assert.That(maxSamples, Is.LessThanOrEqualTo(FlushAllocationSampleBudget), "allocations of one flush (GC.Alloc samples)");
                Assert.That(maxMs, Is.LessThanOrEqualTo(FlushMaxMillisecondsBudget), "the slowest of " + FlushIterations + " flushes");

                string logDirectory = writer.LogDirectoryPath;
                long total = Directory.GetFiles(logDirectory, "segment-*.jsonl").Sum(path => new FileInfo(path).Length);
                Assert.That(total, Is.LessThanOrEqualTo(2L * SessionLogWriter.SegmentCapacityBytes), "the size bound holds on a real disk");
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

#if UNITY_EDITOR
        [Test]
        public void TheSessionWrapperAllocatesNothingWithLoggingOn()
        {
            var loaded = IdleFactory.Unity.Content.GameplayContentDispatcher.Load(RunSceneRun.IronBytes());
            Assert.That(loaded.RunCatalog, Is.Not.Null);
            RunCatalog catalog = loaded.RunCatalog;
            string resource = catalog.Resources.Keys.First();
            string offer = catalog.ContractOffers.Keys.FirstOrDefault() ?? resource;

            var files = new NullFiles();
            var writer = new SessionLogWriter(new SessionLogOptions
            {
                Root = Path.Combine(Application.temporaryCachePath, "session-log-alloc-session"),
                Prefs = new MemoryPrefs(),
                Files = files,
                ContentIds = new SessionLogCatalogIds(catalog),
                Background = job => { },
                ContentRevision = catalog.ContentRevision,
                BuildKind = RunBuildKind.Editor,
            });
            writer.Start();

            var permanent = new IdleFactory.Run.Domain.PermanentState(catalog);
            var start = NextRunStart.Compute(catalog, permanent);
            long startMs = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var state = new IdleFactory.Run.Domain.RunState(catalog, Guid.NewGuid(), 1, null, 25, 0, start.StorageLevel, 1, new string[0]);
            var session = new RunSession(catalog, state, permanent, startMs, writer);

            // session.Events is the guarded wrapper every raise site goes through.
            int allocations = CountAllocations(session.Events, resource, offer, 2000);

            Assert.That(allocations, Is.Zero, "the guard and the writer add no managed allocation on the event path");
            Assert.That(session.DroppedEvents, Is.Zero, "the writer never throws into the guard");
        }
#endif
    }
}
