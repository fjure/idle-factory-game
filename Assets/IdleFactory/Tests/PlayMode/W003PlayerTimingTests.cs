using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T122 (SC-008, plan.md Performance): the W003 timings in the Windows player, from the committed W003 Save V2 fixtures
    /// loaded through Play Mode <c>TextAsset</c> copies (<c>SaveV2PlayerFixtureParityTests</c> proves their bytes). The tests
    /// assert only robust facts (the fixture loads, the workload ran, the counters are sane) and LOG every measurement as a
    /// <c>W003-*</c> line; the budget verdicts are recorded in <c>reviews/w003-measurements.md</c>, and a miss goes to the
    /// Performance review (T130), never to a failing suite. This run is baseline r1 (PE-3).
    /// </summary>
    public sealed class W003PlayerTimingTests
    {
        private const int WarmupFrames = 120;
        private const int MeasuredFrames = 600;
        private const long FrameTicks = 166_667; // one 60 FPS frame in 100 ns ticks
        private const long EightHourEffectiveTicks = 230_400_000_000L;
        private const long EventBoundCompletions = 12L * 230_400L;
        private static readonly int[] StructuralFrames = { 100, 200, 300, 400 };

        public static readonly W003PlayerFixture[] Fixtures =
        {
            W003PlayerFixtures.Bound, W003PlayerFixtures.Stress, W003PlayerFixtures.Reference,
        };

        private static string Num(double value, string format = "F3") => value.ToString(format, CultureInfo.InvariantCulture);

        private static void Log(string line)
        {
            TestContext.Out.WriteLine(line);
            Debug.Log(line);
        }

        private static double Ms(long startTimestamp) => (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;

        private static double Percentile(double[] sorted, double fraction)
        {
            int index = (int)Math.Ceiling(fraction * sorted.Length) - 1;
            return sorted[Math.Max(0, Math.Min(sorted.Length - 1, index))];
        }

        private static RunSession NewSession(W003PlayerFixture fixture, out RunCommands commands)
        {
            var catalog = W003PlayerFixtures.Catalog(fixture);
            var document = W003PlayerFixtures.Load(fixture, catalog);
            var session = new RunSession(catalog, document.Run, document.Permanent, document.Meta.DepartureUtcMs);

            // Load rule 2: settle at the load instant.
            Assert.That(session.Advance(0).IsSuccess, Is.True, fixture.Scenario + " settle");
            commands = new RunCommands(session);
            return session;
        }

        private static void HostFrame(RunSession session, HudRefreshProbe probe, ref MachineStateView[] buffer)
        {
            // FactoryRuntimeHost.AdvanceToCumulativeTicks: the gate, then Advance; then the HUD refresh.
            if (FactoryRuntimeHost.RunNeedsAdvance(session, ref buffer))
            {
                var advanced = session.Advance(FrameTicks);
                if (!advanced.IsSuccess) throw new InvalidOperationException("advance rejected: " + advanced.ReasonCode);
            }

            probe.Refresh();
        }

        [Category("StandalonePlayer")]
        [Test]
        public void HostLine_RecordsMachineAndBuild()
        {
            Log("W003-HOST unity=" + Application.unityVersion + " platform=" + Application.platform + " os=" + SystemInfo.operatingSystem +
                " cpu=" + SystemInfo.processorType + " cores=" + SystemInfo.processorCount + " ramMB=" + SystemInfo.systemMemorySize +
                " developmentBuild=" + Debug.isDebugBuild + " editor=" + Application.isEditor + " baseline=r1");
            Assert.That(SystemInfo.processorCount, Is.GreaterThan(0));
        }

        [Category("StandalonePlayer")]
        [Test]
        public void PackagedFixtures_LoadWithTheRecordedHashesAndRoundTrip()
        {
            foreach (var fixture in Fixtures)
            {
                var catalog = W003PlayerFixtures.Catalog(fixture);
                byte[] bytes = W003PlayerFixtures.Bytes(fixture);
                Assert.That(W003PlayerFixtures.Sha256Of(bytes), Is.EqualTo(fixture.Sha256), fixture.Scenario);
                var document = W003PlayerFixtures.Load(fixture, catalog);
                Assert.That(document.Run.Machines.Count, Is.EqualTo(12), fixture.Scenario + " twelve slots");
                Assert.That(document.Run.Effects.Count, Is.EqualTo(1), fixture.Scenario + " one Item effect");
                // The packaged fixture is a Save V2 file; the player writes Save V3 (T334). The V3 bytes read back to a state that writes
                // the same V3 bytes again (a canonical fixed point).
                Assert.That(document.SchemaVersion, Is.EqualTo(SaveV2Mapper.LegacySchemaVersion), fixture.Scenario);
                var written = SaveV2Mapper.Serialize(document.Meta, document.Run, document.Permanent);
                Assert.That(written.IsSuccess, Is.True, fixture.Scenario);
                var reread = SaveV2Mapper.Deserialize(written.Bytes, catalog);
                Assert.That(reread.IsSuccess, Is.True, fixture.Scenario + " " + reread.Code);
                Assert.That(reread.Document.SchemaVersion, Is.EqualTo(SaveV2Mapper.SchemaVersion), fixture.Scenario);
                var again = SaveV2Mapper.Serialize(reread.Document.Meta, reread.Document.Run, reread.Document.Permanent);
                Assert.That(again.Bytes, Is.EqualTo(written.Bytes), fixture.Scenario + " the V3 bytes are canonical");
                Log("W003-FIXTURE scenario=" + fixture.Scenario + " file=" + fixture.Name + ".json bytes=" + bytes.Length + " sha256=" + fixture.Sha256);
            }
        }

        [Category("StandalonePlayer")]
        [Test]
        public void PackagedFixtures_ADifferingByteChangesTheHash()
        {
            byte[] bytes = W003PlayerFixtures.Bytes(W003PlayerFixtures.Stress);
            var tampered = (byte[])bytes.Clone();
            tampered[tampered.Length / 2] ^= 0x01;
            Assert.That(W003PlayerFixtures.Sha256Of(tampered), Is.Not.EqualTo(W003PlayerFixtures.Stress.Sha256));
            Assert.That(W003PlayerFixtures.Sha256Of(bytes), Is.EqualTo(W003PlayerFixtures.Stress.Sha256));
        }

        [Category("StandalonePlayer")]
        [Test]
        public void HostFrame_600Frames_RecordsP95AndStructureRebuilds([ValueSource(nameof(Fixtures))] W003PlayerFixture fixture)
        {
            var session = NewSession(fixture, out var commands);
            var probe = new HudRefreshProbe(session);
            var buffer = new MachineStateView[16];
            var machineIds = new List<Guid>();
            foreach (var m in session.State.Machines) machineIds.Add(m.InstanceId);

            // Warm-up frames: JIT, buffers, first snapshot.
            for (int i = 0; i < WarmupFrames; i++) HostFrame(session, probe, ref buffer);

            long revisionBefore = session.StructureRevision;
            long cashBefore = session.ReadCash();
            int rebuildsBefore = probe.RebuildsByStructureRevision;
            int factoryBefore = probe.FactoryRebuilds;
            int overdriveBefore = probe.OverdriveStatusReads;
            var frames = new double[MeasuredFrames];
            int structuralCommands = 0;
            var switchedOff = new bool[machineIds.Count];
            for (int frame = 0; frame < MeasuredFrames; frame++)
            {
                // An input event of this frame: a structural command (one machine off, then back on) before the host frame.
                int hit = Array.IndexOf(StructuralFrames, frame);
                if (hit >= 0)
                {
                    int machine = (hit % 2) * 3; // slot 0 and slot 3, each switched off then on again
                    switchedOff[machine] = !switchedOff[machine];
                    var result = commands.SetActive(machineIds[machine], !switchedOff[machine]);
                    Assert.That(result.IsSuccess, Is.True, fixture.Scenario + " SetActive: " + result.ReasonCode);
                    structuralCommands++;
                }

                long t0 = Stopwatch.GetTimestamp();
                HostFrame(session, probe, ref buffer);
                frames[frame] = Ms(t0);
            }

            long revisionDelta = session.StructureRevision - revisionBefore;
            int rebuilds = probe.RebuildsByStructureRevision - rebuildsBefore;
            var sorted = (double[])frames.Clone();
            Array.Sort(sorted);
            double sum = 0;
            foreach (double f in frames) sum += f;
            Log("W003-HOSTFRAME baseline=r1 scenario=" + fixture.Scenario + " frames=" + MeasuredFrames + " p50Ms=" + Num(Percentile(sorted, 0.5)) +
                " p95Ms=" + Num(Percentile(sorted, 0.95)) + " p99Ms=" + Num(Percentile(sorted, 0.99)) + " maxMs=" + Num(sorted[sorted.Length - 1]) +
                " meanMs=" + Num(sum / MeasuredFrames) + " budgetP95Ms=1.000 structuralChangesInWindow=" + structuralCommands +
                " structureRevisionDelta=" + revisionDelta + " snapshotRebuildsByStructureRevision=" + rebuilds +
                " factorySnapshotRebuildsTotal=" + (probe.FactoryRebuilds - factoryBefore) + " overdriveStatusReads=" + (probe.OverdriveStatusReads - overdriveBefore) +
                " cashDelta=" + (session.ReadCash() - cashBefore) + " runtime=windows-player");

            Assert.That(structuralCommands, Is.EqualTo(StructuralFrames.Length));
            Assert.That(revisionDelta, Is.GreaterThanOrEqualTo(structuralCommands), "each structural command moves the revision");
            Assert.That(rebuilds, Is.GreaterThanOrEqualTo(1));
            Assert.That(session.State.Validate(), Is.EqualTo(RunStateResult.Ok));
        }

        [Category("StandalonePlayer")]
        [Test]
        public void HostFramePath_SteadyStateAllocations_AreRecorded([ValueSource(nameof(Fixtures))] W003PlayerFixture fixture)
        {
            var session = NewSession(fixture, out _);
            var probe = new HudRefreshProbe(session);
            var buffer = new MachineStateView[16];
            var inventory = new long[16];
            var views = new MachineStateView[16];
            for (int i = 0; i < WarmupFrames; i++) HostFrame(session, probe, ref buffer);

            // Positive control: the counter must see a managed allocation, or a zero below proves nothing.
            object sink = null;
            int control = GcAllocationCounter.Count(() => sink = new byte[1024]);
            long controlBytesBefore = GC.GetAllocatedBytesForCurrentThread();
            sink = new byte[2048];
            long controlBytes = GC.GetAllocatedBytesForCurrentThread() - controlBytesBefore;
            GC.KeepAlive(sink);

            // (a) the advance path: gate, Advance and the hot read-model copies, 300 frames.
            int advanceSamples = 0;
            int advanceFramesWithSamples = 0;
            long advanceBytes0 = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 300; frame++)
            {
                MachineStateView[] localBuffer = buffer;
                int samples = GcAllocationCounter.Count(() =>
                {
                    if (FactoryRuntimeHost.RunNeedsAdvance(session, ref localBuffer)) session.Advance(FrameTicks);
                    session.ReadCash();
                    session.ReadRp();
                    session.CopyInventory(inventory);
                    session.CopyMachineStates(views);
                });
                buffer = localBuffer;
                advanceSamples += samples;
                if (samples > 0) advanceFramesWithSamples++;
            }

            long advanceBytes = GC.GetAllocatedBytesForCurrentThread() - advanceBytes0;

            // (b) the whole frame including the snapshot reads of the HUD refresh, 300 frames.
            int fullSamples = 0;
            int fullFramesWithSamples = 0;
            long fullBytes0 = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 300; frame++)
            {
                MachineStateView[] localBuffer = buffer;
                int samples = GcAllocationCounter.Count(() => HostFrame(session, probe, ref localBuffer));
                buffer = localBuffer;
                fullSamples += samples;
                if (samples > 0) fullFramesWithSamples++;
            }

            long fullBytes = GC.GetAllocatedBytesForCurrentThread() - fullBytes0;
            Log("W003-ALLOC baseline=r1 scenario=" + fixture.Scenario + " developmentBuild=" + Debug.isDebugBuild + " frames=300 controlSamples=" + control +
                " controlBytes=" + controlBytes + " advancePathSamples=" + advanceSamples + " advancePathFramesWithSamples=" + advanceFramesWithSamples +
                " advancePathBytes=" + advanceBytes + " fullFrameSamples=" + fullSamples + " fullFrameFramesWithSamples=" + fullFramesWithSamples +
                " fullFrameBytes=" + fullBytes + " overdriveStatusReads=" + probe.OverdriveStatusReads + " runtime=windows-player");

            Assert.That(session.State.Validate(), Is.EqualTo(RunStateResult.Ok));
        }

        [Category("StandalonePlayer")]
        [Test]
        public void OfflineReconcile_EightHours_RecordsMedianOfFive([ValueSource(nameof(Fixtures))] W003PlayerFixture fixture)
        {
            long departure = W003PlayerFixtures.Load(fixture, W003PlayerFixtures.Catalog(fixture)).Meta.DepartureUtcMs;

            // Nine hours away: the eight hour credited cap applies.
            long returnUtcMs = departure + W003PlayerFixtures.EightHoursMs + 3_600_000L;

            // (1) The reconcile algorithm alone, with the test-only counters of the simulator (PE-1, PE-7).
            var algorithmMs = new List<double>();
            long completions = 0, timestamps = 0, passes = 0, maxPasses = 0, partial = 0;
            RunOfflineSummary summary = null;
            for (int run = 0; run < 6; run++)
            {
                var catalog = W003PlayerFixtures.Catalog(fixture);
                var document = W003PlayerFixtures.Load(fixture, catalog);
                var segmenter = new OfflineSegmenter(catalog);
                long t0 = Stopwatch.GetTimestamp();
                summary = segmenter.Reconcile(document.Run, document.Permanent, document.Meta.DepartureUtcMs, returnUtcMs);
                double ms = Ms(t0);
                Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied), fixture.Scenario);
                Assert.That(document.Run.Validate(), Is.EqualTo(RunStateResult.Ok));
                if (run == 0) Log("W003-RECONCILE-COLD scenario=" + fixture.Scenario + " algorithmMs=" + Num(ms, "F1") + " (first call of the variant, JIT included)");
                else algorithmMs.Add(ms);
                completions = segmenter.Simulator.LastCompletions;
                partial = segmenter.Simulator.LastPartialBatches;
                timestamps = segmenter.Simulator.LastTimestamps;
                passes = segmenter.Simulator.LastPasses;
                maxPasses = segmenter.Simulator.LastMaxPassesPerTimestamp;
            }

            Assert.That(completions, Is.EqualTo(summary.Completions));
            Assert.That(maxPasses, Is.LessThanOrEqualTo(13), "N + 1 passes per timestamp (OD-2)");
            Assert.That(summary.EffectiveTicks, Is.EqualTo(EightHourEffectiveTicks));
            if (fixture == W003PlayerFixtures.Bound)
                Assert.That(completions, Is.EqualTo(EventBoundCompletions), "W003-B reaches the event bound of 12 x E(8 h) / minCycleTicks");
            if (fixture == W003PlayerFixtures.Stress)
                Assert.That(completions, Is.EqualTo(691_200L), "W003-S: only the three Miners complete (T098)");
            Assert.That(completions, Is.GreaterThan(0));

            algorithmMs.Sort();
            double algorithmMedian = algorithmMs[algorithmMs.Count / 2];
            double nsPerEvent = algorithmMedian * 1_000_000.0 / Math.Max(1, completions);
            double nsPerTimestamp = algorithmMedian * 1_000_000.0 / Math.Max(1, timestamps);
            Log("W003-RECONCILE-ALGORITHM baseline=r1 scenario=" + fixture.Scenario + " horizon=8h-effective(2.304e11 ticks) runsMs=" +
                string.Join(",", algorithmMs.ConvertAll(v => Num(v, "F1"))) +
                " medianMs=" + Num(algorithmMedian, "F1") + " budgetMs=500 completions=" + completions + " partialBatches=" + partial +
                " processedTimestamps=" + timestamps + " passes=" + passes + " maxPassesPerTimestamp=" + maxPasses +
                " passesPerTimestampMean=" + Num(passes / (double)Math.Max(1, timestamps), "F3") + " nsPerEvent=" + Num(nsPerEvent, "F0") +
                " nsPerTimestamp=" + Num(nsPerTimestamp, "F0") + " extrapolated2.76MCompletionsMs=" + Num(nsPerEvent * EventBoundCompletions / 1_000_000.0, "F0") +
                " cashDelta=" + summary.CashDelta + " runtime=windows-player");

            // (2) The full return: cold start of a persisted checkpoint (read, decode, reconcile, promote, install).
            var fullMs = new List<double>();
            for (int run = 0; run < 6; run++)
            {
                var catalog = W003PlayerFixtures.Catalog(fixture);
                string root = RunPlayerFixtures.NewRoot();
                try
                {
                    var checkpoint = new SaveV2CheckpointCodec(catalog).Deserialize(W003PlayerFixtures.Bytes(fixture));
                    Assert.That(checkpoint.Succeeded, Is.True, fixture.Scenario + " " + checkpoint.Message);
                    var seeding = new JsonRunCheckpointStore(root, catalog).Promote(checkpoint.Checkpoint);
                    Assert.That(seeding.Succeeded, Is.True, fixture.Scenario + " seed: " + seeding.Message);
                    var rig = RunRig.Create(root, catalog, returnUtcMs);
                    long t0 = Stopwatch.GetTimestamp();
                    var started = rig.Persistence.ColdStart(null);
                    double ms = Ms(t0);
                    Assert.That(started.IsSuccess, Is.True, fixture.Scenario + " cold start: " + started.ReasonCode);
                    var offline = started.Value.OfflineSummary;
                    Assert.That(offline, Is.Not.Null, fixture.Scenario + " offline summary");
                    Assert.That(offline.Completions, Is.EqualTo(completions), fixture.Scenario + " same completions as the algorithm alone");
                    if (run == 0) Log("W003-RECONCILE-COLD scenario=" + fixture.Scenario + " fullResumeAndReconcileMs=" + Num(ms, "F1") + " (first call of the variant, file I/O included)");
                    else fullMs.Add(ms);
                }
                finally
                {
                    RunPlayerFixtures.DeleteRoot(root);
                }
            }

            fullMs.Sort();
            Log("W003-RECONCILE-FULL baseline=r1 scenario=" + fixture.Scenario + " path=ColdStart(read,decode,reconcile,promote,install) runsMs=" +
                string.Join(",", fullMs.ConvertAll(v => Num(v, "F1"))) +
                " medianMs=" + Num(fullMs[fullMs.Count / 2], "F1") + " budgetMs=500 completions=" + completions + " runtime=windows-player");
        }
    }
}
