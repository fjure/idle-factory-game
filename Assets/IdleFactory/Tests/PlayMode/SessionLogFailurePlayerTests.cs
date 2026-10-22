using System;
using System.IO;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T348 (issue #413, QA-13, FR-030, SC-008): a session-log write failure is isolated in the built Windows player. The failing file layer is injected
    /// through the writer's constructor options, the test composition seam of <c>contracts/session-log.md</c>; the writer, the guarded event sink of
    /// <see cref="RunSession"/>, the save protocol and the simulation are the production ones. The log never touches the real persistent data path.
    /// </summary>
    public sealed class SessionLogFailurePlayerTests
    {
        private const long Second = 10_000_000L;

        private string root;

        [SetUp]
        public void CreateRoot() => root = RunPlayerFixtures.NewRoot();

        [TearDown]
        public void DeleteRoot() => RunPlayerFixtures.DeleteRoot(root);

        private sealed class MemoryPrefs : ISessionLogPrefs
        {
            public bool Enabled { get; set; } = true;
        }

        /// <summary>A file layer on which every operation throws an <see cref="IOException"/>, counting the attempts.</summary>
        private sealed class FailingFiles : ISessionLogFiles
        {
            public int Attempts;

            private T Fail<T>()
            {
                Attempts++;
                throw new IOException("injected session-log write failure");
            }

            public void EnsureDirectory(string directory) => Fail<int>();
            public bool Exists(string path) => Fail<bool>();
            public long Length(string path) => Fail<long>();
            public long LastWriteUtcTicks(string path) => Fail<long>();
            public void Append(string path, byte[] buffer, int offset, int count) => Fail<int>();
            public void Truncate(string path) => Fail<int>();
            public byte[] ReadAll(string path) => Fail<byte[]>();
            public int ReadLastByte(string path) => Fail<int>();
            public void WriteNew(string path, byte[] buffer, int count) => Fail<int>();
            public void Move(string from, string to) => Fail<int>();
            public void Delete(string path) => Fail<int>();
            public string[] List(string directory, string searchPattern) => Fail<string[]>();
        }

        private static SessionLogWriter NewWriter(string logRoot, ISessionLogFiles files)
        {
            return new SessionLogWriter(new SessionLogOptions
            {
                Root = logRoot,
                Prefs = new MemoryPrefs(),
                Files = files,
                Background = job => job(), // flush inline so a failure is attempted on the calling thread, where it could throw
                BuildKind = RunBuildKind.Editor,
                ContentRevision = 3,
            });
        }

        /// <summary>The run id is a fresh random GUID per run; everything after it is deterministic.</summary>
        private static string WithoutRunId(string fingerprint) => fingerprint.Substring(fingerprint.IndexOf('|') + 1);

        private sealed class Outcome
        {
            public string Fingerprint;
            public long Timeline;
            public bool SaveSucceeded;
            public string RestartedFingerprint;
            public int SessionDropped;
        }

        /// <summary>The same deterministic play (mine, build, sell, advance, save, restart) over a fresh root, with or without a log sink.</summary>
        private Outcome Play(string subRoot, SessionLogWriter writer)
        {
            string rigRoot = Path.Combine(root, subRoot);
            Directory.CreateDirectory(rigRoot);
            var catalog = RunPlayerFixtures.ScaledCatalog();
            var rig = RunRig.Create(rigRoot, catalog, RunPlayerFixtures.StartUtcMs, writer);
            rig.ColdStartOk(null);
            if (writer != null) Assert.DoesNotThrow(writer.Start);
            for (int i = 0; i < 12; i++)
            {
                Assert.That(rig.Commands.Mine(10).IsSuccess, Is.True);
                Assert.That(rig.Session.Advance(Second).IsSuccess, Is.True);
            }

            Assert.That(rig.Commands.SellAll("iron.ore").IsSuccess, Is.True);
            Assert.That(rig.Commands.Build(0, "machine.miner").IsSuccess, Is.True);
            Assert.That(rig.Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            Assert.That(rig.Session.Advance(60 * Second).IsSuccess, Is.True);
            if (writer != null) Assert.DoesNotThrow(writer.Flush);
            rig.SyncClock();
            var saved = rig.Persistence.SaveNow();
            if (writer != null) Assert.DoesNotThrow(() => writer.RecordLifecycle(RunLifecycleKind.Quit, 0));

            var restarted = RunRig.Create(rigRoot, catalog, rig.Clock.UnixMs);
            restarted.ColdStartOk(null);
            return new Outcome
            {
                Fingerprint = RunPlayerFixtures.Fingerprint(rig.Session),
                Timeline = rig.Session.TimelineUtcMs,
                SaveSucceeded = saved.IsSuccess,
                RestartedFingerprint = RunPlayerFixtures.Fingerprint(restarted.Session),
                SessionDropped = rig.Session.DroppedEvents,
            };
        }

        [Category("StandalonePlayer")]
        [Test]
        public void AFailingLogFileLayerNeverAltersTheRunTheSaveOrTheTimeline()
        {
            Outcome baseline = Play("baseline", null);

            var files = new FailingFiles();
            SessionLogWriter writer = NewWriter(Path.Combine(root, "log"), files);
            Outcome failing = Play("failing", writer);

            Assert.That(files.Attempts, Is.GreaterThan(0), "the failing layer was exercised");
            Assert.That(writer.DroppedEvents, Is.GreaterThan(0), "the lost lines are counted by the writer");
            Assert.That(failing.SessionDropped, Is.Zero, "the writer isolates its own failures; nothing reached the session guard");
            Assert.That(failing.SaveSucceeded, Is.True, "a log failure never blocks a save");
            Assert.That(baseline.SaveSucceeded, Is.True);
            Assert.That(WithoutRunId(failing.Fingerprint), Is.EqualTo(WithoutRunId(baseline.Fingerprint)), "the run is identical to a run with no log");
            Assert.That(failing.Timeline, Is.EqualTo(baseline.Timeline), "the timeline is identical to a run with no log");
            Assert.That(failing.RestartedFingerprint, Is.EqualTo(failing.Fingerprint), "the durable save restores the run exactly");
            Assert.That(WithoutRunId(failing.RestartedFingerprint), Is.EqualTo(WithoutRunId(baseline.RestartedFingerprint)), "the durable save equals the one of a run with no log");
        }

        [Category("StandalonePlayer")]
        [Test]
        public void AFailingLogFileLayerNeverThrowsFromAnyWriterOperation()
        {
            var files = new FailingFiles();
            SessionLogWriter writer = NewWriter(Path.Combine(root, "log-ops"), files);

            Assert.DoesNotThrow(writer.Start);
            Assert.DoesNotThrow(writer.Flush);
            Assert.DoesNotThrow(writer.Tick);
            Assert.DoesNotThrow(() => writer.SetEnabled(false));
            Assert.DoesNotThrow(() => writer.SetEnabled(true));
            Assert.DoesNotThrow(() => writer.Clear());
            Assert.DoesNotThrow(() => new SessionLogExport(writer, files).Export());
            Assert.That(writer.DroppedEvents, Is.GreaterThan(0), "failed writes are counted, never thrown");
        }
    }
}
