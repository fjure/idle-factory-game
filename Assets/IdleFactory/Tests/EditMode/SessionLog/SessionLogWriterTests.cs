using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.SessionLog
{
    /// <summary>
    /// T338 (FR-028, FR-031, F5-R12): the behaviour of the session-log writer on an in-memory file layer: session ID, header, recording toggle (A1),
    /// clear, the two rotating segments, the ring buffer, the per-minute roll-ups, the periodic flush off the caller's thread, the time-scale header
    /// (A3, D-I) and the sampler's schedule.
    /// </summary>
    public sealed class SessionLogWriterTests
    {
        private static void RaiseContracts(SessionLogWriter writer, int first, int count)
        {
            for (int i = 0; i < count; i++)
                writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, "contract.deliver", first + i, 5, 7, 1_500_000));
        }

        // ---- session ID (S2, SEC-16) -------------------------------------------------------------------------------------------

        [Test]
        public void TheSessionIdIsAll128BitsOfTheInjectedRandomSource()
        {
            Harness h = Harness.Create();
            Assert.That(h.Writer.SessionId.High, Is.EqualTo(0x0102030405060708UL));
            Assert.That(h.Writer.SessionId.Low, Is.EqualTo(0x090A0B0C0D0E0F10UL));

            h.Writer.Start();
            Dictionary<string, object> header = Harness.OfType(h.AllRecords(), "header").Single();
            Assert.That((string)header["sessionId"], Is.EqualTo("0102030405060708090a0b0c0d0e0f10"), "32 hex digits: the full width is written");
        }

        [Test]
        public void TheDefaultSessionIdComesFromTheCryptographicGeneratorAndDiffersPerStart()
        {
            Harness a = Harness.Create(o => o.FillRandom = null);
            Harness b = Harness.Create(o => o.FillRandom = null);
            Assert.That(a.Writer.SessionId.High != 0UL || a.Writer.SessionId.Low != 0UL, Is.True);
            Assert.That(a.Writer.SessionId.High != b.Writer.SessionId.High || a.Writer.SessionId.Low != b.Writer.SessionId.Low, Is.True);
        }

        [Test]
        public void ARandomSourceThatFailsGivesAnAllZeroIdAndIsCounted()
        {
            Harness h = Harness.Create(o => o.FillRandom = bytes => throw new InvalidOperationException("no entropy"));
            Assert.That(h.Writer.SessionId.High, Is.Zero);
            Assert.That(h.Writer.SessionId.Low, Is.Zero);
            Assert.That(h.Writer.DroppedEvents, Is.EqualTo(1));
        }

        // ---- header ------------------------------------------------------------------------------------------------------------

        [Test]
        public void StartWritesTheHeaderThenTheStartRecordAndFlushes()
        {
            Harness h = Harness.Create();
            h.Writer.Start();

            List<Dictionary<string, object>> records = h.AllRecords();
            Assert.That(records.Select(r => (string)r["t"]), Is.EqualTo(new[] { "header", "lifecycle" }));
            Assert.That(records[0]["schema"], Is.EqualTo(1L));
            Assert.That(records[0]["contentRevision"], Is.EqualTo(3L));
            Assert.That(records[0]["buildKind"], Is.EqualTo("editor"));
            Assert.That(records[0]["platformClass"], Is.EqualTo("desktop"));
            Assert.That(records[0]["timeScale"], Is.EqualTo(1L), "no time-scale source: real time");
            Assert.That(records[1]["event"], Is.EqualTo("start"));
            Assert.That(records[1]["droppedEvents"], Is.EqualTo(0L));
        }

        [Test]
        public void TheHeaderTimeScaleIsTheDisplayFactorClampedToOneAndIntMax()
        {
            long[] factors = { 1, 10, 600, int.MaxValue, (long)int.MaxValue + 5, long.MaxValue, 0, -4, long.MinValue };
            long[] expected = { 1, 10, 600, int.MaxValue, int.MaxValue, int.MaxValue, 1, 1, 1 };
            for (int i = 0; i < factors.Length; i++)
            {
                var scale = new FlagScale { Factor = factors[i] };
                Harness h = Harness.Create(o => o.TimeScale = () => scale);
                h.Writer.Start();
                Assert.That(Harness.OfType(h.AllRecords(), "header").Single()["timeScale"], Is.EqualTo(expected[i]), "factor " + factors[i]);
            }
        }

        [Test]
        public void APlainSourceNeverCastsInfinityOrNaN()
        {
            double[] scales = { double.PositiveInfinity, double.NaN, 0d, -3d, 0.5d, 10.4d, 1e300 };
            long[] expected = { int.MaxValue, 1, 1, 1, 1, 10, int.MaxValue };
            for (int i = 0; i < scales.Length; i++)
            {
                var scale = new PlainScale { Scale = scales[i] };
                Harness h = Harness.Create(o => o.TimeScale = () => scale);
                Assert.DoesNotThrow(() => h.Writer.Start());
                Assert.That(Harness.OfType(h.AllRecords(), "header").Single()["timeScale"], Is.EqualTo(expected[i]), "scale " + scales[i]);
            }
        }

        [Test]
        public void AFactorThatChangesMidSessionAppendsAHeaderOfTheSameSession()
        {
            var scale = new FlagScale { Factor = 1 };
            Harness h = Harness.Create(o => o.TimeScale = () => scale);
            h.Writer.Start();

            scale.Factor = 600;
            RaiseContracts(h.Writer, 0, 1);
            RaiseContracts(h.Writer, 1, 1);
            h.Flush();

            List<Dictionary<string, object>> records = h.AllRecords();
            List<Dictionary<string, object>> headers = Harness.OfType(records, "header");
            Assert.That(headers.Select(r => (long)r["timeScale"]), Is.EqualTo(new[] { 1L, 600L }), "one more header, not one per event");
            Assert.That(headers[1]["sessionId"], Is.EqualTo(headers[0]["sessionId"]));
            int secondHeader = records.IndexOf(headers[1]);
            Assert.That((string)records[secondHeader + 1]["t"], Is.EqualTo("contract"), "the header precedes the first accelerated event");
        }

        [Test]
        public void AReleaseBuildWithoutASourceWritesOneHeader()
        {
            Harness h = Harness.Create();
            h.Writer.Start();
            RaiseContracts(h.Writer, 0, 20);
            h.Flush();
            Assert.That(Harness.OfType(h.AllRecords(), "header"), Has.Count.EqualTo(1));
        }

        [Test]
        public void TheLifecycleRecordCountsTheSessionGuardAndTheWritersOwnDrops()
        {
            Harness h = Harness.Create();
            h.Writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, "not.in.catalog", 0, 1, 1, 1)); // 1 own drop
            h.Writer.RecordLifecycle(RunLifecycleKind.Quit, 2);

            Dictionary<string, object> record = Harness.OfType(h.AllRecords(), "lifecycle").Single();
            Assert.That(record["event"], Is.EqualTo("quit"));
            Assert.That(record["droppedEvents"], Is.EqualTo(3L));
        }

        // ---- recording toggle (FR-031, A1) -------------------------------------------------------------------------------------

        [Test]
        public void WhileRecordingIsOffEventsReturnWithoutWritingOrCountingADrop()
        {
            Harness h = Harness.Create();
            Assert.That(h.Writer.Enabled, Is.True, "default on");

            h.Writer.SetEnabled(false);
            long drops = h.Writer.DroppedEvents;
            RaiseContracts(h.Writer, 0, 500);
            h.Writer.OnSale(new RunSaleEvent("iron.ore", 1, 1, true));
            h.Writer.OnMining(new RunMiningEvent("iron.ore", 1, 0, 0, 1));
            h.Writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, "not.in.catalog", 0, 1, 1, 1));
            h.Writer.RecordLifecycle(RunLifecycleKind.FocusLossCheckpoint, 0);
            h.Flush();

            Assert.That(h.Writer.DroppedEvents, Is.EqualTo(drops), "nothing is counted while the toggle is off");
            Assert.That(Harness.OfType(h.AllRecords(), "contract"), Is.Empty);
            Assert.That(Harness.OfType(h.AllRecords(), "sale"), Is.Empty);
            Assert.That(Harness.OfType(h.AllRecords(), "lifecycle").Select(r => (string)r["event"]), Is.EqualTo(new[] { "logToggled" }), "only the toggle itself");
        }

        [Test]
        public void ASettingThatIsOffAtConstructionKeepsTheWriterQuiet()
        {
            var prefs = new FakePrefs { Value = false };
            Harness h = Harness.Create(o => o.Prefs = prefs);
            Assert.That(h.Writer.Enabled, Is.False);
            h.Writer.Start();
            RaiseContracts(h.Writer, 0, 10);
            h.Flush();
            Assert.That(h.Files.Has(h.Segment(0)), Is.False);
            Assert.That(h.Writer.DroppedEvents, Is.Zero);
        }

        [Test]
        public void TheToggleIsPersistedAndRecordedOnBothEdges()
        {
            Harness h = Harness.Create();
            h.Writer.Start();

            h.Writer.SetEnabled(false);
            Assert.That(h.Prefs.Value, Is.False);
            Assert.That(h.Writer.Enabled, Is.False);
            h.Writer.SetEnabled(false); // no change, no second write
            Assert.That(h.Prefs.Sets, Is.EqualTo(1));

            h.Writer.SetEnabled(true);
            Assert.That(h.Prefs.Value, Is.True);
            RaiseContracts(h.Writer, 0, 2);
            h.Flush();

            string[] events = Harness.OfType(h.AllRecords(), "lifecycle").Select(r => (string)r["event"]).ToArray();
            Assert.That(events, Is.EqualTo(new[] { "start", "logToggled", "logToggled" }));
            Assert.That(Harness.OfType(h.AllRecords(), "contract"), Has.Count.EqualTo(2), "recording resumed");
        }

        [Test]
        public void TurningRecordingOnInASessionThatNeverStartedLoggingWritesTheHeaderFirst()
        {
            var prefs = new FakePrefs { Value = false };
            Harness h = Harness.Create(o => o.Prefs = prefs);
            h.Writer.Start();
            h.Writer.SetEnabled(true);
            RaiseContracts(h.Writer, 0, 1);
            h.Flush();

            Assert.That(h.AllRecords().Select(r => (string)r["t"]), Is.EqualTo(new[] { "header", "lifecycle", "contract" }));
        }

        [Test]
        public void AFailingSettingStoreNeverBreaksTheToggleOrTheConstructor()
        {
            var prefs = new FakePrefs { FailGet = true, FailSet = true };
            Harness h = null;
            Assert.DoesNotThrow(() => h = Harness.Create(o => o.Prefs = prefs));
            Assert.That(h.Writer.Enabled, Is.True, "an unreadable setting counts as the default: on");
            Assert.DoesNotThrow(() => h.Writer.SetEnabled(false));
            Assert.That(h.Writer.Enabled, Is.False, "the in-memory toggle still takes effect");
            Assert.That(h.Writer.DroppedEvents, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void ThePlayerPrefsSettingDefaultsOnAndUsesTheDocumentedKey()
        {
            const string key = "IdleFactory.SessionLog.Enabled";
            Assert.That(PlayerPrefsSessionLogPrefs.Key, Is.EqualTo(key));
            bool had = UnityEngine.PlayerPrefs.HasKey(key);
            int old = UnityEngine.PlayerPrefs.GetInt(key, 1);
            try
            {
                UnityEngine.PlayerPrefs.DeleteKey(key);
                var prefs = new PlayerPrefsSessionLogPrefs();
                Assert.That(prefs.Enabled, Is.True, "default on");
                prefs.Enabled = false;
                Assert.That(UnityEngine.PlayerPrefs.GetInt(key, 1), Is.EqualTo(0));
                Assert.That(new PlayerPrefsSessionLogPrefs().Enabled, Is.False);
            }
            finally
            {
                if (had) UnityEngine.PlayerPrefs.SetInt(key, old);
                else UnityEngine.PlayerPrefs.DeleteKey(key);
                UnityEngine.PlayerPrefs.Save();
            }
        }

        // ---- clear -------------------------------------------------------------------------------------------------------------

        [Test]
        public void ClearTruncatesBothSegmentsAndWritesTheHeaderAndAClearedRecord()
        {
            Harness h = Harness.Create();
            h.Writer.Start();
            for (int batch = 0; batch < 10; batch++)
            {
                RaiseContracts(h.Writer, batch * 300, 300);
                h.Flush();
            }

            RaiseContracts(h.Writer, 9000, 5); // buffered, not yet flushed
            Assert.That(h.Writer.Clear(), Is.True);

            List<Dictionary<string, object>> records = h.AllRecords();
            Assert.That(records.Select(r => (string)r["t"]), Is.EqualTo(new[] { "header", "lifecycle" }), "everything before the clear is gone, buffered lines too");
            Assert.That(records[1]["event"], Is.EqualTo("cleared"));
            Assert.That(h.Files.Bytes(h.Segment(1)), Is.Empty.Or.Null);
        }

        [Test]
        public void ClearWhileRecordingIsOffTruncatesAndWritesNothing()
        {
            Harness h = Harness.Create();
            h.Writer.Start();
            h.Writer.SetEnabled(false);

            Assert.That(h.Writer.Clear(), Is.True);

            Assert.That(h.AllRecords(), Is.Empty);
            Assert.That(h.Files.Bytes(h.Segment(0)), Is.Empty);
            Assert.That(h.Files.Bytes(h.Segment(1)), Is.Empty);
        }

        // ---- segments, ring ----------------------------------------------------------------------------------------------------

        [Test]
        public void TwoSegmentsRotateAndTheOldestDataGoesFirst()
        {
            Harness h = Harness.Create();
            h.Writer.Start();
            int next = 0;
            for (int batch = 0; batch < 40; batch++)
            {
                RaiseContracts(h.Writer, next, 300);
                next += 300;
                h.Flush();
                Assert.That(h.Files.Bytes(h.Segment(0))?.Length ?? 0, Is.LessThanOrEqualTo(SessionLogWriter.SegmentCapacityBytes));
                Assert.That(h.Files.Bytes(h.Segment(1))?.Length ?? 0, Is.LessThanOrEqualTo(SessionLogWriter.SegmentCapacityBytes));
            }

            int length0 = h.Files.Bytes(h.Segment(0)).Length;
            int length1 = h.Files.Bytes(h.Segment(1)).Length;
            Assert.That(length0 + length1, Is.LessThanOrEqualTo(2 * SessionLogWriter.SegmentCapacityBytes), "total <= 256 KB");
            Assert.That(length0, Is.GreaterThan(0));
            Assert.That(length1, Is.GreaterThan(0));

            long[] indexes = Harness.OfType(h.AllRecords(), "contract").Select(r => (long)r["index"]).ToArray();
            Assert.That(indexes.First(), Is.GreaterThan(0), "the oldest lines were dropped first");
            Assert.That(indexes.Last(), Is.EqualTo(next - 1), "the newest line is kept");
            for (int i = 1; i < indexes.Length; i++) Assert.That(indexes[i], Is.EqualTo(indexes[i - 1] + 1), "older segment first, no gap and no reordering");
            Assert.That(h.Writer.DroppedEvents, Is.Zero);
        }

        [Test]
        public void ARestartAppendsToTheNewerSegmentAndEndsATornLineBeforeItsFirstRecord()
        {
            Harness h = Harness.Create();
            h.Writer.Start();
            int next = 0;
            for (int batch = 0; batch < 20; batch++)
            {
                RaiseContracts(h.Writer, next, 300);
                next += 300;
                h.Flush();
            }

            int newer = h.Files.LastWriteUtcTicks(h.Segment(0)) > h.Files.LastWriteUtcTicks(h.Segment(1)) ? 0 : 1;
            byte[] before = h.Files.Bytes(h.Segment(newer));
            byte[] tail = System.Text.Encoding.ASCII.GetBytes("{\"t\":\"contract\",\"v");
            h.Files.Put(h.Segment(newer), before.Concat(tail).ToArray());

            Harness restarted = Harness.Create(files: h.Files);
            restarted.Writer.Start();
            RaiseContracts(restarted.Writer, 777_000, 1);
            restarted.Flush();

            long[] indexes = Harness.OfType(restarted.AllRecords(), "contract").Select(r => (long)r["index"]).ToArray();
            Assert.That(indexes.Last(), Is.EqualTo(777_000), "the new record is readable after the torn line");
            Assert.That(indexes, Does.Contain(next - 1), "the earlier records are still there");
            Assert.That(Harness.Parse(restarted.Files.Bytes(restarted.Segment(1 - newer))).Count, Is.GreaterThan(0), "the other segment is untouched");
            Assert.That(Harness.OfType(restarted.AllRecords(), "header"), Has.Count.GreaterThanOrEqualTo(1), "the restart wrote its own header");
        }

        [Test]
        public void TheRingDropsTheOldestLinesWhenFullAndCountsThem()
        {
            Harness h = Harness.Create();
            RaiseContracts(h.Writer, 0, 2000);
            h.Flush();

            long[] indexes = Harness.OfType(h.AllRecords(), "contract").Select(r => (long)r["index"]).ToArray();
            Assert.That(indexes.Length, Is.LessThanOrEqualTo(SessionLogWriter.MaxBufferedLines));
            Assert.That(indexes.Last(), Is.EqualTo(1999), "the newest lines survive");
            Assert.That(h.Writer.DroppedEvents, Is.EqualTo(2000 - indexes.Length), "every lost line is counted");
        }

        // ---- per resource and minute roll-ups ----------------------------------------------------------------------------------

        [Test]
        public void SalesAreRolledUpPerResourceAndMinute()
        {
            Harness h = Harness.Create();
            h.Writer.OnSale(new RunSaleEvent("iron.ore", 1, 3, true));
            h.Writer.OnSale(new RunSaleEvent("iron.ore", 2, 6, true));
            h.Writer.OnSale(new RunSaleEvent("iron.ore", 3, 9, true));
            h.Writer.OnSale(new RunSaleEvent("iron.bar", 4, 12, true));
            h.Writer.OnSale(new RunSaleEvent("iron.ore", 5, 15, false));
            Assert.That(h.Files.Has(h.Segment(0)), Is.False, "nothing is written by an event");

            h.NowMs += 61_000; // the next minute
            h.Writer.OnSale(new RunSaleEvent("iron.ore", 7, 70, true));
            h.Flush();

            List<Dictionary<string, object>> sales = Harness.OfType(h.AllRecords(), "sale");
            Assert.That(sales, Has.Count.EqualTo(3 + 1), "ore manual, bar manual, ore auto in minute one; ore manual in minute two");
            Dictionary<string, object> ore = sales.Single(s => (string)s["resourceId"] == "iron.ore" && (bool)s["manual"] && (long)s["units"] == 6);
            Assert.That(ore["cash"], Is.EqualTo(18L));
            Assert.That(sales.Single(s => (string)s["resourceId"] == "iron.bar")["units"], Is.EqualTo(4L));
            Assert.That(sales.Single(s => !(bool)s["manual"])["cash"], Is.EqualTo(15L));
            Assert.That(sales.Last()["units"], Is.EqualTo(7L), "the new minute is its own record");
        }

        [Test]
        public void MiningIsRolledUpPerResourceAndMinuteNotPerActivation()
        {
            Harness h = Harness.Create();
            for (int i = 0; i < 100; i++) h.Writer.OnMining(new RunMiningEvent("iron.ore", 3, 1, 2, 4));
            h.Flush();

            Dictionary<string, object> mining = Harness.OfType(h.AllRecords(), "mining").Single();
            Assert.That(mining["counted"], Is.EqualTo(300L));
            Assert.That(mining["ignored"], Is.EqualTo(100L));
            Assert.That(mining["blocked"], Is.EqualTo(200L));
            Assert.That(mining["ore"], Is.EqualTo(400L));
        }

        [Test]
        public void ARollUpThatWouldOverflowSaturatesInsteadOfWrapping()
        {
            Harness h = Harness.Create();
            h.Writer.OnSale(new RunSaleEvent("iron.ore", long.MaxValue - 1, long.MaxValue, true));
            h.Writer.OnSale(new RunSaleEvent("iron.ore", 5, 5, true));
            h.Flush();

            Dictionary<string, object> sale = Harness.OfType(h.AllRecords(), "sale").Single();
            Assert.That(sale["units"], Is.EqualTo(long.MaxValue));
            Assert.That(sale["cash"], Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void MoreRollUpResourcesThanSlotsAreDroppedAndCounted()
        {
            var ids = Enumerable.Range(0, SessionLogWriter.MaxRollupResources + 3).Select(i => "res.n" + i).ToArray();
            Harness h = Harness.Create(o => o.ContentIds = new IdSet(ids));
            foreach (string id in ids) h.Writer.OnSale(new RunSaleEvent(id, 1, 1, true));
            h.Flush();

            Assert.That(Harness.OfType(h.AllRecords(), "sale"), Has.Count.EqualTo(SessionLogWriter.MaxRollupResources));
            Assert.That(h.Writer.DroppedEvents, Is.EqualTo(3));
        }

        // ---- periodic flush, off the frame path (FR-028) -----------------------------------------------------------------------

        [Test]
        public void TheTimedFlushIsQueuedOnTheBackgroundRunnerAfter30SecondsAndNeverOnTheCaller()
        {
            Harness h = Harness.Create();
            RaiseContracts(h.Writer, 0, 3);

            h.NowMs += SessionLogWriter.FlushIntervalMs - 1;
            h.Writer.Tick();
            Assert.That(h.Queue, Is.Empty, "29.999 s: not yet");

            h.NowMs += 1;
            h.Writer.Tick();
            Assert.That(h.Queue, Has.Count.EqualTo(1));
            Assert.That(h.Files.Has(h.Segment(0)), Is.False, "Tick itself writes nothing: the file work is on the runner");

            h.Writer.Tick();
            Assert.That(h.Queue, Has.Count.EqualTo(1), "a pending flush is not queued twice");

            h.Queue[0]();
            Assert.That(Harness.OfType(h.AllRecords(), "contract"), Has.Count.EqualTo(3));

            h.Queue.Clear();
            h.NowMs += 120_000;
            h.Writer.Tick();
            Assert.That(h.Queue, Is.Empty, "nothing buffered: nothing to flush");

            RaiseContracts(h.Writer, 3, 1);
            h.Writer.Tick();
            Assert.That(h.Queue, Has.Count.EqualTo(1), "buffered again and the interval has long passed");
            h.Queue[0]();
            RaiseContracts(h.Writer, 4, 1);
            h.Writer.Tick();
            Assert.That(h.Queue, Has.Count.EqualTo(1), "the interval counts from the last flush: nothing new is queued");
        }

        [Test]
        public void ARunnerThatThrowsLosesNothingForTheNextTick()
        {
            Harness h = Harness.Create(o => o.Background = job => throw new InvalidOperationException("no threads"));
            RaiseContracts(h.Writer, 0, 3);
            h.NowMs += SessionLogWriter.FlushIntervalMs;
            Assert.DoesNotThrow(() => h.Writer.Tick());
            Assert.DoesNotThrow(() => h.Writer.Tick());
            Assert.That(h.Writer.DroppedEvents, Is.EqualTo(2));
            h.Flush();
            Assert.That(Harness.OfType(h.AllRecords(), "contract"), Has.Count.EqualTo(3), "the lines are still buffered and a lifecycle flush writes them");
        }

        [Test]
        public void ALifecyclePointFlushesSynchronously()
        {
            Harness h = Harness.Create();
            RaiseContracts(h.Writer, 0, 3);
            h.Writer.RecordLifecycle(RunLifecycleKind.FocusLossCheckpoint, 0);

            List<Dictionary<string, object>> records = h.AllRecords();
            Assert.That(records.Select(r => (string)r["t"]), Is.EqualTo(new[] { "contract", "contract", "contract", "lifecycle" }));
        }

        // ---- sampler (F5-R12) --------------------------------------------------------------------------------------------------

        private static RunSession SessionWith(Harness h, out RunCommandFixtures.Rig rig, bool miner = false)
        {
            RunCommandFixtures.Rig built = RunCommandFixtures.NewRig();
            built.Session = new RunSession(built.Catalog, built.State, new PermanentState(built.Catalog), RunCommandFixtures.StartUtcMs, h.Writer);
            RunSession session = built.Session;
            built.Commands = new RunCommands(session, () => RunSimHarness.Id(built.NextId++));
            if (miner) Assert.That(built.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            rig = built;
            return session;
        }

        [Test]
        public void TheSamplerTakesASampleAtMinutes10_15_30_60_90_and_120Only()
        {
            Harness h = Harness.Create();
            RunSession session = SessionWith(h, out RunCommandFixtures.Rig rig);
            var sampler = new SessionLogSampler(h.Writer);
            long ticksPerMinute = 60L * RunSimHarness.TicksPerSecond;

            sampler.Poll(session); // baseline
            long minutes = 0;
            foreach (int target in new[] { 5, 9, 10, 12, 15, 29, 30, 59, 60, 61, 89, 90, 119, 120, 200 })
            {
                rig.Advance((target - minutes) * ticksPerMinute);
                minutes = target;
                sampler.Poll(session);
                sampler.Poll(session); // idempotent within the same instant
            }

            h.Flush();
            long[] seconds = Harness.OfType(h.AllRecords(), "sample").Select(r => (long)r["playSeconds"]).ToArray();
            Assert.That(seconds, Is.EqualTo(new[] { 10L, 15, 30, 60, 90, 120 }.Select(m => m * 60).ToArray()));
        }

        [Test]
        public void TheSamplerSamplesAtAMilestoneAndRestartsOnANewRun()
        {
            Harness h = Harness.Create();
            RunSession session = SessionWith(h, out RunCommandFixtures.Rig rig);
            var sampler = new SessionLogSampler(h.Writer);
            sampler.Poll(session);

            h.Writer.OnMilestone(new RunMilestoneEvent(RunMilestoneKind.FirstGear, 30));
            sampler.Poll(session);
            sampler.Poll(session);
            h.Flush();

            Assert.That(Harness.OfType(h.AllRecords(), "milestone"), Has.Count.EqualTo(1));
            Assert.That(Harness.OfType(h.AllRecords(), "sample"), Has.Count.EqualTo(1), "one sample for the milestone, not one per poll");
        }

        [Test]
        public void ASampleCarriesFactoryValueCashMachinesByTypeAndLevelAndTheExpansionLevels()
        {
            Harness h = Harness.Create();
            RunSession session = SessionWith(h, out RunCommandFixtures.Rig rig, miner: true);
            Assert.That(rig.Build(1, RunCommandFixtures.Miner).IsSuccess, Is.True);
            Assert.That(rig.Build(2, RunCommandFixtures.Smelter).IsSuccess, Is.True);
            Assert.That(session.State.Machines.Count, Is.EqualTo(3));

            SessionLogRaiser.RaiseSample(session, 600);
            h.Flush();

            Dictionary<string, object> sample = Harness.OfType(h.AllRecords(), "sample").Single();
            Assert.That(sample["playSeconds"], Is.EqualTo(600L));
            Assert.That(sample["cash"], Is.EqualTo(session.State.Cash));
            Assert.That((long)sample["factoryValue"], Is.GreaterThan(0));
            Assert.That(sample["storageLevel"], Is.EqualTo((long)session.State.StorageLevel));
            Assert.That(sample["factoryLevel"], Is.EqualTo((long)session.State.FactoryLevel));
            var machines = ((List<object>)sample["machines"]).Cast<Dictionary<string, object>>().ToList();
            Assert.That(machines.Single(m => (string)m["machineId"] == RunCommandFixtures.Miner)["count"], Is.EqualTo(2L));
            Assert.That(machines.Single(m => (string)m["machineId"] == RunCommandFixtures.Smelter)["count"], Is.EqualTo(1L));
        }
    }
}
