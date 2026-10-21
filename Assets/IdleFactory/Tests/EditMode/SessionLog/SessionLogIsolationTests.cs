using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.SessionLog
{
    /// <summary>
    /// T338 (FR-030, SC-008, SEC-5, SEC-9): a log failure never reaches the caller, never alters a run, and a recovery loses nothing that was not already lost.
    /// Clear, toggle, flush and export are serialized: a probe over the file layer sees at most one file operation at a time.
    /// </summary>
    public sealed class SessionLogIsolationTests
    {
        private sealed class ThrowingIds : ISessionLogContentIds
        {
            public bool Contains(string id) => throw new InvalidOperationException("ids");
        }

        /// <summary>Delegates to an in-memory layer and records the highest number of operations in flight at once.</summary>
        private sealed class ExclusionProbe : ISessionLogFiles
        {
            private readonly ISessionLogFiles inner;
            private int inFlight;
            private int maxInFlight;

            public ExclusionProbe(ISessionLogFiles inner) => this.inner = inner;

            public int MaxInFlight => Volatile.Read(ref maxInFlight);

            // Only operations inside the SessionLog directory are probed: the export writes elsewhere, outside the writer's gate, on purpose.
            private T Run<T>(string path, Func<T> operation)
            {
                if (path == null || path.IndexOf(SessionLogWriter.LogDirectory, StringComparison.Ordinal) < 0) return operation();
                int now = Interlocked.Increment(ref inFlight);
                int seen;
                while (now > (seen = Volatile.Read(ref maxInFlight))) Interlocked.CompareExchange(ref maxInFlight, now, seen);
                try
                {
                    Thread.Yield();
                    return operation();
                }
                finally
                {
                    Interlocked.Decrement(ref inFlight);
                }
            }

            public void EnsureDirectory(string directory) => Run(directory, () => { inner.EnsureDirectory(directory); return 0; });
            public bool Exists(string path) => Run(path, () => inner.Exists(path));
            public long Length(string path) => Run(path, () => inner.Length(path));
            public long LastWriteUtcTicks(string path) => Run(path, () => inner.LastWriteUtcTicks(path));
            public void Append(string path, byte[] buffer, int offset, int count) => Run(path, () => { inner.Append(path, buffer, offset, count); return 0; });
            public void Truncate(string path) => Run(path, () => { inner.Truncate(path); return 0; });
            public byte[] ReadAll(string path) => Run(path, () => inner.ReadAll(path));
            public void WriteNew(string path, byte[] buffer, int count) => Run(path, () => { inner.WriteNew(path, buffer, count); return 0; });
            public void Move(string from, string to) => Run(from, () => { inner.Move(from, to); return 0; });
            public void Delete(string path) => Run(path, () => { inner.Delete(path); return 0; });
            public string[] List(string directory, string searchPattern) => Run(directory, () => inner.List(directory, searchPattern));
        }

        private static void RaiseAll(IRunEventSink sink)
        {
            sink.OnHeader(new RunHeaderEvent(1, 3, RunBuildKind.Editor, RunPlatformClass.Desktop, 1, new RunSessionId(1UL, 2UL)));
            sink.OnSample(new RunSampleEvent(10, 100, 50, new[] { new RunMachineCount("machine.miner", 1, 1) }, 1, 1));
            sink.OnMilestone(new RunMilestoneEvent(RunMilestoneKind.FirstGear, 10));
            sink.OnSale(new RunSaleEvent("iron.ore", 3, 9, true));
            sink.OnMining(new RunMiningEvent("iron.ore", 4, 1, 0, 2));
            sink.OnContract(new RunContractEvent(RunContractEventKind.Accept, "contract.deliver", 0, 5, 50, 1_000_000));
            sink.OnPrestige(new RunPrestigeEvent(100, 90, 2, 5, new[] { new RunIndustryLevel("industry.cash", 1) }));
            sink.OnOfflineReturn(new RunOfflineReturnEvent(1, 1, false, 0, 0, 0, 0, false));
            sink.OnBottleneck(new RunBottleneckEvent("iron.ore", 1, 2));
            sink.OnPurchase(new RunPurchaseEvent(RunPurchaseKind.Build, "machine.miner", 1, 10, 0));
            sink.OnClock(new RunClockEvent(RunClockKind.Backward, -5, false));
            sink.OnLifecycle(new RunLifecycleEvent(RunLifecycleKind.FocusLossCheckpoint, 0));
        }

        private static void EveryOperation(Harness h)
        {
            h.Writer.Start();
            RaiseAll(h.Writer);
            h.Writer.RecordLifecycle(RunLifecycleKind.Quit, 0);
            h.Writer.Flush();
            h.NowMs += SessionLogWriter.FlushIntervalMs;
            h.Writer.Tick();
            h.Writer.SetEnabled(false);
            h.Writer.SetEnabled(true);
            h.Writer.Clear();
            h.Writer.Flush();
            new SessionLogExport(h.Writer, h.Files).Export();
        }

        [Test]
        public void AFileLayerThatFailsEverythingNeverThrowsAndLaterRecovers()
        {
            Harness h = Harness.Create();
            h.RunInline = true;
            h.Files.FailAll = true;

            Assert.DoesNotThrow(() => EveryOperation(h));
            Assert.That(h.Writer.DroppedEvents, Is.GreaterThan(0), "the lost lines are counted");
            Assert.That(h.Writer.Clear(), Is.False, "a clear that could not truncate says so");
            Assert.That(new SessionLogExport(h.Writer, h.Files).Export().Status, Is.EqualTo(SessionLogExportStatus.Failed));

            h.Files.FailAll = false;
            h.Writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, "contract.deliver", 5, 1, 1, 1));
            h.Writer.Flush();
            Assert.That(Harness.OfType(h.AllRecords(), "contract").Select(r => (long)r["index"]), Is.EqualTo(new[] { 5L }), "writing resumes");
        }

        [TestCase("EnsureDirectory")]
        [TestCase("Length")]
        [TestCase("ReadAll")]
        [TestCase("Append")]
        [TestCase("Truncate")]
        [TestCase("WriteNew")]
        [TestCase("Move")]
        [TestCase("Delete")]
        [TestCase("List")]
        public void ASingleFailingFileOperationNeverThrows(string operation)
        {
            Harness h = Harness.Create();
            h.RunInline = true;
            h.Files.FailOperation = operation;
            Assert.DoesNotThrow(() => EveryOperation(h), operation);
        }

        [TestCase(typeof(IOException))]
        [TestCase(typeof(UnauthorizedAccessException))]
        [TestCase(typeof(InvalidOperationException))]
        [TestCase(typeof(ObjectDisposedException))]
        public void WhateverTheFileLayerThrowsStaysInside(Type exceptionType)
        {
            Harness h = Harness.Create();
            h.Files.Failure = (Exception)Activator.CreateInstance(exceptionType, "injected");
            h.Files.FailAll = true;
            Assert.DoesNotThrow(() => EveryOperation(h));
        }

        [Test]
        public void AFlushThatFailsCountsEachLostLineAndTheLaterFlushIsUnaffected()
        {
            Harness h = Harness.Create();
            for (int i = 0; i < 5; i++) h.Writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, "contract.deliver", i, 1, 1, 1));
            h.Files.FailOperation = "Append";
            h.Writer.Flush();
            Assert.That(h.Writer.DroppedEvents, Is.EqualTo(5));

            h.Files.FailOperation = null;
            h.Writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, "contract.deliver", 99, 1, 1, 1));
            h.Writer.Flush();
            Assert.That(Harness.OfType(h.AllRecords(), "contract").Select(r => (long)r["index"]), Is.EqualTo(new[] { 99L }));
        }

        [Test]
        public void AFailingClockCatalogOrTimeScaleNeverReachesTheCaller()
        {
            var scale = new FlagScale();
            Harness h = Harness.Create(o =>
            {
                o.ClockMs = () => throw new InvalidOperationException("clock");
                o.ContentIds = new ThrowingIds();
                o.TimeScale = () => throw new InvalidOperationException("scale");
            });

            Assert.DoesNotThrow(() => h.Writer.Start());
            Assert.DoesNotThrow(() => RaiseAll(h.Writer));
            Assert.DoesNotThrow(() => h.Writer.Tick());
            Assert.DoesNotThrow(() => h.Writer.Flush());
            Assert.That(scale, Is.Not.Null);
            Assert.That(h.Writer.DroppedEvents, Is.GreaterThan(0), "the records that need the catalog are dropped and counted");
        }

        [Test]
        public void ARunOverAFailingLogIsIdenticalToARunWithNoLog()
        {
            string baseline = RunScenario(null);

            Harness failing = Harness.Create();
            failing.Files.FailAll = true;
            string failingLog = RunScenario(failing.Writer);

            Harness healthy = Harness.Create();
            string healthyLog = RunScenario(healthy.Writer);

            Assert.That(failingLog, Is.EqualTo(baseline), "a failing log changes nothing about the run");
            Assert.That(healthyLog, Is.EqualTo(baseline), "a healthy one neither");
            Assert.That(failing.Writer.DroppedEvents, Is.GreaterThan(0));
        }

        private static string RunScenario(SessionLogWriter writer)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig();
            rig.Session = writer == null
                ? new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog), RunCommandFixtures.StartUtcMs)
                : new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog), RunCommandFixtures.StartUtcMs, writer);
            rig.Commands = new RunCommands(rig.Session, () => RunSimHarness.Id(rig.NextId++));
            writer?.Start();

            rig.Build(0, RunCommandFixtures.Miner);
            rig.Advance(30_000_000);
            rig.Commands.Sell("iron.ore", 1);
            rig.Commands.SetAutoSell("iron.ore", true);
            rig.Build(1, RunCommandFixtures.Smelter);
            rig.Advance(50_000_000);
            writer?.RecordLifecycle(RunLifecycleKind.FocusLossCheckpoint, rig.Session.DroppedEvents);
            return RunCommandFixtures.Snapshot(rig.State) + "timeline=" + rig.Session.TimelineUtcMs + " ticks=" + rig.Session.ActiveTicksSinceAnchor;
        }

        [Test]
        public void ClearToggleFlushAndExportAreSerializedAndLeaveOnlyWholeLines()
        {
            var inner = new InMemorySessionLogFiles();
            var probe = new ExclusionProbe(inner);
            Harness h = Harness.Create(o => o.Files = probe, files: inner);
            h.Writer.Start();
            var export = new SessionLogExport(h.Writer, probe);
            var failures = new List<Exception>();
            var stop = new ManualResetEventSlim(false);

            Thread Worker(Action<int> body)
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        for (int i = 0; i < 150 && !stop.IsSet; i++) body(i);
                    }
                    catch (Exception ex)
                    {
                        lock (failures) failures.Add(ex);
                        stop.Set();
                    }
                });
                thread.Start();
                return thread;
            }

            Thread[] workers =
            {
                Worker(i => { for (int k = 0; k < 20; k++) h.Writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, "contract.deliver", i, 1, 1, 1)); }),
                Worker(i => { h.Writer.OnSale(new RunSaleEvent("iron.ore", 1, 1, true)); h.Writer.Flush(); }),
                Worker(i => { h.Writer.SetEnabled(i % 2 == 1); }),
                Worker(i => { if (i % 5 == 0) h.Writer.Clear(); }),
                Worker(i => { if (i % 7 == 0) export.Export(); }),
                Worker(i => { h.Writer.RecordLifecycle(RunLifecycleKind.FocusLossCheckpoint, 0); }),
            };

            foreach (Thread worker in workers) Assert.That(worker.Join(TimeSpan.FromSeconds(60)), Is.True, "no worker deadlocks");
            Assert.That(failures, Is.Empty, failures.Count > 0 ? failures[0].ToString() : "");
            Assert.That(probe.MaxInFlight, Is.EqualTo(1), "file operations never overlap");

            h.Writer.SetEnabled(true);
            h.Writer.Flush();
            int newlines = 0;
            int valid = 0;
            for (int segment = 0; segment < 2; segment++)
            {
                byte[] bytes = inner.Bytes(h.Segment(segment)) ?? new byte[0];
                newlines += bytes.Count(b => b == (byte)'\n');
                valid += Harness.Parse(bytes).Count;
            }

            Assert.That(valid, Is.EqualTo(newlines), "every line in the segments is whole and passes its CRC");
        }
    }
}
