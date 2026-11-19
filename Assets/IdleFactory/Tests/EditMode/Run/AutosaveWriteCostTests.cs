using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// T559: the main-thread cost of one autosave write (the full <see cref="RunPersistence.SaveNow"/> path: read both generations,
    /// build the checkpoint, serialize, write, verify, promote) over the real file store, with the shipped catalog and the committed
    /// W003-S device state (the largest committed factory). The figures are recorded in the test output for the T559 report; the assertion is
    /// only a coarse regression bound, because host timing varies (Windows editor, SSD, indexer). The device figure comes from the Android
    /// player and is the PE-2 style measurement of the emulator journey.
    /// </summary>
    public sealed class AutosaveWriteCostTests
    {
        private string root;

        [SetUp]
        public void SetUp() => root = RunCheckpointTestSupport.NewTempRoot();

        [TearDown]
        public void TearDown() => RunCheckpointTestSupport.DeleteQuietly(root);

        [Test]
        public void OneAutosaveOverTheShippedCatalogAndTheLargestCommittedFactory_IsCheapOnTheMainThread()
        {
            RunCatalog catalog = ShippedDeviceSets.LoadShippedCatalog();
            string source = Path.Combine(UnityEngine.Application.dataPath, "IdleFactory/Tests/EditMode/Fixtures/SaveV2", ShippedDeviceSets.W003SShippedDirectory);
            string target = RunCheckpointTestSupport.V2Directory(root);
            Directory.CreateDirectory(target);
            foreach (string name in ShippedDeviceSets.Files) File.Copy(Path.Combine(source, name), Path.Combine(target, name));

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var permanent = new PermanentState(catalog);
            NextRunStart start = NextRunStart.Compute(catalog, permanent);
            var state = new RunState(catalog, Guid.NewGuid(), 1, null, start.StartingCash, 0, start.StorageLevel, 1, catalog.RunStart.InitialUnlocks);
            Assert.That(new RunSimulator(catalog).Advance(state, permanent, 0, nowMs, null), Is.EqualTo(RunAdvanceResult.Ok));
            var session = new RunSession(catalog, state, permanent, nowMs);
            var clock = new FixedUtcClock(nowMs);
            var persistence = new RunPersistence(session, new JsonRunCheckpointStore(root, catalog), clock);
            Assert.That(persistence.ColdStart(null).IsSuccess, Is.True);
            Assert.That(persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));

            // Warm the code (JIT, file system caches), then time 60 writes as the autosave makes them: SaveNow on the main thread.
            for (int i = 0; i < 5; i++) Assert.That(persistence.SaveNow().IsSuccess, Is.True);
            var millis = new List<double>();
            var watch = new Stopwatch();
            for (int i = 0; i < 60; i++)
            {
                clock.UnixMs += 30_000;
                watch.Restart();
                var saved = persistence.SaveNow();
                watch.Stop();
                Assert.That(saved.IsSuccess, Is.True, saved.ReasonCode);
                millis.Add(watch.Elapsed.TotalMilliseconds);
            }

            millis.Sort();
            double median = millis[millis.Count / 2];
            double p95 = millis[(int)(millis.Count * 0.95)];
            long bytes = Directory.GetFiles(target).Where(f => !f.EndsWith(".meta", StringComparison.Ordinal)).Sum(f => new FileInfo(f).Length);
            string line = "T559 synchronous write cost (shipped catalog, W003-S, " + session.State.Machines.Count + " machines, " + bytes + " bytes in the v2 folder): " +
                          "median " + median.ToString("F2") + " ms, p95 " + p95.ToString("F2") + " ms, max " + millis[millis.Count - 1].ToString("F2") + " ms, min " +
                          millis[0].ToString("F2") + " ms (n=" + millis.Count + ")";
            TestContext.Progress.WriteLine(line);
            UnityEngine.Debug.Log(line);

            // The background path: the main thread captures and serializes (timed here), the file IO runs elsewhere (timed separately).
            var queued = new List<Action>();
            persistence.AutosaveExecutor = queued.Add;
            var mainThread = new List<double>();
            var worker = new List<double>();
            for (int i = 0; i < 60; i++)
            {
                clock.UnixMs += 30_000;
                Assert.That(session.Advance(300_000_000).IsSuccess, Is.True);
                watch.Restart();
                AutosaveTickOutcome started = persistence.AutosaveWhenDue();
                watch.Stop();
                Assert.That(started, Is.EqualTo(AutosaveTickOutcome.NotDue));
                Assert.That(queued.Count, Is.EqualTo(1), "round " + i + ": a background write was started");
                mainThread.Add(watch.Elapsed.TotalMilliseconds);
                watch.Restart();
                queued[0]();
                watch.Stop();
                worker.Add(watch.Elapsed.TotalMilliseconds);
                queued.Clear();
                Assert.That(persistence.AutosaveWhenDue(), Is.EqualTo(AutosaveTickOutcome.Written));
            }

            mainThread.Sort();
            worker.Sort();
            double mainMedian = mainThread[mainThread.Count / 2];
            double mainP95 = mainThread[(int)(mainThread.Count * 0.95)];
            string backgroundLine = "T559 background write (same state): main thread capture+serialize median " + mainMedian.ToString("F2") + " ms, p95 " +
                                    mainP95.ToString("F2") + " ms, max " + mainThread[mainThread.Count - 1].ToString("F2") + " ms; worker file IO median " +
                                    worker[worker.Count / 2].ToString("F2") + " ms, p95 " + worker[(int)(worker.Count * 0.95)].ToString("F2") + " ms";
            TestContext.Progress.WriteLine(backgroundLine);
            UnityEngine.Debug.Log(backgroundLine);
            Assert.That(mainMedian, Is.LessThan(8.0), "the main thread keeps well inside one 60 fps frame (16.7 ms); the figure is in the log line above");

            Assert.That(median, Is.LessThan(100.0), "a coarse regression bound for the synchronous fallback; the measured figure is in the log line above");
        }
    }
}
