using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T336 (issue #401, QA-2, PF-11, PF-16, F5-R14): the early W005-L timing in the Windows player, from the committed Save V3 fixture loaded
    /// through its Play Mode <c>TextAsset</c> copy (<c>SaveV3PlayerFixtureParityTests</c> proves its bytes) against the packaged shipped catalog.
    /// The tests assert only robust facts (the fixture loads and round-trips, the workload ran, the counters are sane) and LOG every measurement
    /// as a <c>W005L-*</c> line; the budget verdicts are recorded in <c>reviews/w005l-measurement.md</c>, and a miss goes to a Performance review,
    /// never to a failing suite. The conditions of the timed window (a quiet host, the process list, median of 5 with the spread) are PF-9 and PF-10.
    /// </summary>
    public sealed class W005PlayerTimingTests
    {
        private const string FixtureName = "01-w005-l";
        private const string ResourceDirectory = "W005PlayerFixtures";

        /// <summary>The SHA-256 of the committed fixture (QA-2); <c>SaveV3PlayerFixtureParityTests</c> pins the same value on the original.</summary>
        private const string FixtureSha256 = "7066eed15513d2bdeb2c349bfa98c2a3fdc500d6b179b997ed8fe92b954a70fd";

        private const long EightHourEffectiveTicks = 230_400_000_000L;
        private const long EventBoundCompletions = 12L * 230_400L;
        private const int Repetitions = 5;

        private static string Num(double value, string format = "F3") => value.ToString(format, CultureInfo.InvariantCulture);

        private static void Log(string line)
        {
            TestContext.Out.WriteLine(line);
            Debug.Log(line);
        }

        private static double Ms(long startTimestamp) => (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;

        private static byte[] FixtureBytes()
        {
            var asset = Resources.Load<TextAsset>(ResourceDirectory + "/" + FixtureName);
            Assert.That(asset, Is.Not.Null, "The packaged player fixture '" + FixtureName + "' is missing from the build.");
            return asset.bytes;
        }

        private static SaveV2Document Load(RunCatalog catalog, byte[] bytes)
        {
            var read = SaveV2Mapper.Deserialize(bytes, catalog);
            Assert.That(read.IsSuccess, Is.True, "W005-L " + read.Code);
            return read.Document;
        }

        private static string Spread(List<double> sorted)
        {
            double median = sorted[sorted.Count / 2];
            return Num((sorted[sorted.Count - 1] - sorted[0]) / median * 100.0, "F1");
        }

        private static int CountProcesses(params string[] names)
        {
            int count = 0;
            Process[] processes;
            try
            {
                processes = Process.GetProcesses();
            }
            catch (Exception)
            {
                return -1; // the platform does not list processes: recorded as -1, never a failure
            }

            foreach (Process process in processes)
            {
                try
                {
                    string name = process.ProcessName;
                    if (names.Any(n => string.Equals(name, n, StringComparison.OrdinalIgnoreCase))) count++;
                }
                catch (Exception)
                {
                }
                finally
                {
                    process.Dispose();
                }
            }

            return count;
        }

        [Category("StandalonePlayer")]
        [Test]
        public void HostLine_RecordsMachineBuildAndQuietHost()
        {
            // PF-9: no Unity editor and no emulator while the window runs. The player itself is a process named after the product, so the editor
            // is counted by its own process name only.
            Log("W005L-HOST unity=" + Application.unityVersion + " platform=" + Application.platform + " os=" + SystemInfo.operatingSystem +
                " cpu=" + SystemInfo.processorType + " cores=" + SystemInfo.processorCount + " ramMB=" + SystemInfo.systemMemorySize +
                " developmentBuild=" + Debug.isDebugBuild + " editor=" + Application.isEditor +
                " editorProcesses=" + CountProcesses("Unity") + " emulatorProcesses=" + CountProcesses("qemu-system-x86_64", "emulator", "crosvm") +
                " adbProcesses=" + CountProcesses("adb") + " utc=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            Assert.That(SystemInfo.processorCount, Is.GreaterThan(0));
        }

        [Category("StandalonePlayer")]
        [Test]
        public void PackagedFixture_HasTheRecordedHash_LoadsAndRoundTrips()
        {
            var catalog = RunPlayerFixtures.IronCatalog();
            byte[] bytes = FixtureBytes();
            Assert.That(W003PlayerFixtures.Sha256Of(bytes), Is.EqualTo(FixtureSha256), "packaged bytes");
            var document = Load(catalog, bytes);
            Assert.That(document.SchemaVersion, Is.EqualTo(SaveV2Mapper.SchemaVersion), "a Save V3 file");
            Assert.That(document.Run.Machines.Count, Is.EqualTo(12), "twelve slots");
            Assert.That(document.Run.Contracts.Active, Is.Not.Null, "an active contract");
            Assert.That(document.Run.Contracts.Active.AutoDeliver, Is.True, "auto-deliver is active");
            Assert.That(document.Run.AutoSell, Is.EqualTo(new[] { "iron.gear" }), "auto-sell on the end product");

            var written = SaveV2Mapper.Serialize(document.Meta, document.Run, document.Permanent);
            Assert.That(written.IsSuccess, Is.True, written.Code);
            Assert.That(written.Bytes, Is.EqualTo(bytes), "read then write is a fixed point");
            Log("W005L-FIXTURE file=" + FixtureName + ".json bytes=" + bytes.Length + " sha256=" + FixtureSha256 + " contentRevision=" + catalog.ContentRevision);
        }

        [Category("StandalonePlayer")]
        [Test]
        public void PackagedFixture_ADifferingByteChangesTheHash()
        {
            byte[] bytes = FixtureBytes();
            var tampered = (byte[])bytes.Clone();
            tampered[tampered.Length / 2] ^= 0x01;
            Assert.That(W003PlayerFixtures.Sha256Of(tampered), Is.Not.EqualTo(FixtureSha256));
            Assert.That(W003PlayerFixtures.Sha256Of(bytes), Is.EqualTo(FixtureSha256));
        }

        [Category("StandalonePlayer")]
        [Test]
        public void SaveV3Write_RecordsTimeAndSize()
        {
            var catalog = RunPlayerFixtures.IronCatalog();
            byte[] bytes = FixtureBytes();
            var document = Load(catalog, bytes);

            // (1) The mapper alone: state to canonical bytes (PF-11).
            var mapperMs = new List<double>();
            int size = 0;
            for (int run = 0; run < Repetitions + 1; run++)
            {
                long t0 = Stopwatch.GetTimestamp();
                var written = SaveV2Mapper.Serialize(document.Meta, document.Run, document.Permanent);
                double ms = Ms(t0);
                Assert.That(written.IsSuccess, Is.True, written.Code);
                size = written.Bytes.Length;
                if (run == 0) Log("W005L-WRITE-COLD mapperMs=" + Num(ms, "F3") + " (first call, JIT included)");
                else mapperMs.Add(ms);
            }

            mapperMs.Sort();

            // (2) The durable protocol: the real store promotes the decoded checkpoint into an empty root (write, flush, manifest, publish).
            var storeMs = new List<double>();
            for (int run = 0; run < Repetitions + 1; run++)
            {
                string root = RunPlayerFixtures.NewRoot();
                try
                {
                    var decoded = new SaveV2CheckpointCodec(catalog).Deserialize(bytes);
                    Assert.That(decoded.Succeeded, Is.True, decoded.Message);
                    var store = new JsonRunCheckpointStore(root, catalog);
                    long t0 = Stopwatch.GetTimestamp();
                    var promoted = store.Promote(decoded.Checkpoint);
                    double ms = Ms(t0);
                    Assert.That(promoted.Succeeded, Is.True, promoted.Message);
                    if (run == 0) Log("W005L-WRITE-COLD storePromoteMs=" + Num(ms, "F3") + " (first call, file I/O and JIT included)");
                    else storeMs.Add(ms);
                }
                finally
                {
                    RunPlayerFixtures.DeleteRoot(root);
                }
            }

            storeMs.Sort();
            Log("W005L-WRITE schemaVersion=" + SaveV2Mapper.SchemaVersion + " bytes=" + size + " mapperRunsMs=" + string.Join(",", mapperMs.ConvertAll(v => Num(v, "F3"))) +
                " mapperMedianMs=" + Num(mapperMs[mapperMs.Count / 2], "F3") + " storePromoteRunsMs=" + string.Join(",", storeMs.ConvertAll(v => Num(v, "F3"))) +
                " storePromoteMedianMs=" + Num(storeMs[storeMs.Count / 2], "F3") + " runtime=windows-player");
        }

        [Category("StandalonePlayer")]
        [Test]
        public void OfflineReconcile_EightHours_RecordsMedianOfFive()
        {
            var catalog = RunPlayerFixtures.IronCatalog();
            byte[] bytes = FixtureBytes();
            long departure = Load(catalog, bytes).Meta.DepartureUtcMs;

            // Nine hours away: the eight hour credited cap applies.
            long returnUtcMs = departure + 9L * 3_600_000L;

            // (1) The reconcile algorithm alone, with the test-only counters of the simulator.
            var algorithmMs = new List<double>();
            long completions = 0, timestamps = 0, passes = 0, maxPasses = 0, partial = 0;
            RunOfflineSummary summary = null;
            for (int run = 0; run < Repetitions + 1; run++)
            {
                var document = Load(catalog, bytes);
                var segmenter = new OfflineSegmenter(catalog);
                long t0 = Stopwatch.GetTimestamp();
                summary = segmenter.Reconcile(document.Run, document.Permanent, document.Meta.DepartureUtcMs, returnUtcMs);
                double ms = Ms(t0);
                Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
                Assert.That(document.Run.Validate(), Is.EqualTo(RunStateResult.Ok));
                if (run == 0) Log("W005L-RECONCILE-COLD algorithmMs=" + Num(ms, "F1") + " (first call of the player, JIT included)");
                else algorithmMs.Add(ms);
                completions = segmenter.Simulator.LastCompletions;
                partial = segmenter.Simulator.LastPartialBatches;
                timestamps = segmenter.Simulator.LastTimestamps;
                passes = segmenter.Simulator.LastPasses;
                maxPasses = segmenter.Simulator.LastMaxPassesPerTimestamp;
            }

            Assert.That(completions, Is.EqualTo(summary.Completions));
            Assert.That(summary.EffectiveTicks, Is.EqualTo(EightHourEffectiveTicks));
            Assert.That(maxPasses, Is.LessThanOrEqualTo(12 + 2), "N + 2 passes per timestamp (PF-1)");
            Assert.That(completions, Is.GreaterThan(0));
            Assert.That(completions, Is.LessThanOrEqualTo(EventBoundCompletions));

            algorithmMs.Sort();
            double algorithmMedian = algorithmMs[algorithmMs.Count / 2];
            double nsPerEvent = algorithmMedian * 1_000_000.0 / Math.Max(1, completions);
            double nsPerTimestamp = algorithmMedian * 1_000_000.0 / Math.Max(1, timestamps);
            Log("W005L-RECONCILE-ALGORITHM sha256=" + FixtureSha256 + " horizon=8h-effective(2.304e11 ticks) runsMs=" +
                string.Join(",", algorithmMs.ConvertAll(v => Num(v, "F1"))) + " medianMs=" + Num(algorithmMedian, "F1") + " spreadPct=" + Spread(algorithmMs) +
                " budgetMs=500 completions=" + completions + " partialBatches=" + partial + " processedTimestamps=" + timestamps + " passes=" + passes +
                " maxPassesPerTimestamp=" + maxPasses + " passesPerTimestampMean=" + Num(passes / (double)Math.Max(1, timestamps), "F3") +
                " nsPerEvent=" + Num(nsPerEvent, "F0") + " nsPerTimestamp=" + Num(nsPerTimestamp, "F0") +
                " cashDelta=" + summary.CashDelta + " runtime=windows-player");

            // (2) The full return: cold start of a persisted checkpoint (read, decode, reconcile, promote, install).
            var fullMs = new List<double>();
            for (int run = 0; run < Repetitions + 1; run++)
            {
                string root = RunPlayerFixtures.NewRoot();
                try
                {
                    var checkpoint = new SaveV2CheckpointCodec(catalog).Deserialize(bytes);
                    Assert.That(checkpoint.Succeeded, Is.True, checkpoint.Message);
                    var seeding = new JsonRunCheckpointStore(root, catalog).Promote(checkpoint.Checkpoint);
                    Assert.That(seeding.Succeeded, Is.True, "seed: " + seeding.Message);
                    var rig = RunRig.Create(root, catalog, returnUtcMs);
                    long t0 = Stopwatch.GetTimestamp();
                    var started = rig.Persistence.ColdStart(null);
                    double ms = Ms(t0);
                    Assert.That(started.IsSuccess, Is.True, "cold start: " + started.ReasonCode);
                    var offline = started.Value.OfflineSummary;
                    Assert.That(offline, Is.Not.Null, "offline summary");
                    Assert.That(offline.Completions, Is.EqualTo(completions), "same completions as the algorithm alone");
                    if (run == 0) Log("W005L-RECONCILE-COLD fullResumeAndReconcileMs=" + Num(ms, "F1") + " (first call, file I/O included)");
                    else fullMs.Add(ms);
                }
                finally
                {
                    RunPlayerFixtures.DeleteRoot(root);
                }
            }

            fullMs.Sort();
            Log("W005L-RECONCILE-FULL sha256=" + FixtureSha256 + " path=ColdStart(read,decode,reconcile,promote,install) runsMs=" +
                string.Join(",", fullMs.ConvertAll(v => Num(v, "F1"))) + " medianMs=" + Num(fullMs[fullMs.Count / 2], "F1") + " spreadPct=" + Spread(fullMs) +
                " budgetMs=500 completions=" + completions + " runtime=windows-player");
        }
    }
}
