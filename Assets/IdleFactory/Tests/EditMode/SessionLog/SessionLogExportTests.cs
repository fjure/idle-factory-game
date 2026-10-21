using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.SessionLog
{
    /// <summary>
    /// T338 (FR-031, D-M, SEC-2, SEC-15): the export writes a temp file and renames it, keeps at most 10 files, skips torn and CRC-failing lines, writes nothing for an
    /// empty log, reports a failure by category without changing anything, and keeps the shown path out of the log and the export.
    /// </summary>
    public sealed class SessionLogExportTests
    {
        private static readonly DateTime Stamp = new DateTime(2026, 10, 21, 12, 34, 56, DateTimeKind.Utc);

        private static (Harness harness, SessionLogExport export, Func<DateTime> clock, Action<int> advance) Rig(int contracts = 3)
        {
            Harness h = Harness.Create();
            DateTime now = Stamp;
            Func<DateTime> clock = () => now;
            h.Writer.Start();
            for (int i = 0; i < contracts; i++)
                h.Writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, "contract.deliver", i, 5, 7, 1_500_000));
            var export = new SessionLogExport(h.Writer, h.Files, clock);
            return (h, export, clock, seconds => now = now.AddSeconds(seconds));
        }

        private static string[] ExportFiles(Harness h) => h.Files.Paths(h.Writer.ExportDirectoryPath).Select(Path.GetFileName).ToArray();

        [Test]
        public void ExportWritesATempFileThenRenamesItAndKeepsNoTempFile()
        {
            var rig = Rig();
            SessionLogExportResult result = rig.export.Export();

            Assert.That(result.Status, Is.EqualTo(SessionLogExportStatus.Exported));
            Assert.That(result.Failure, Is.EqualTo(SessionLogExportFailure.None));
            Assert.That(Path.GetFileName(result.FilePath), Is.EqualTo("session-log-20261021T123456Z.jsonl"));
            Assert.That(result.DirectoryPath, Is.EqualTo(rig.harness.Writer.ExportDirectoryPath), "the folder is reported on every platform (D-M)");
            Assert.That(result.Lines, Is.EqualTo(2 + 3), "header, start and three contracts");

            List<string> log = rig.harness.Files.Log;
            int write = log.IndexOf("WriteNew session-log-20261021T123456Z.jsonl.tmp");
            int move = log.IndexOf("Move session-log-20261021T123456Z.jsonl.tmp");
            Assert.That(write, Is.GreaterThanOrEqualTo(0));
            Assert.That(move, Is.GreaterThan(write), "the rename comes after the whole temp file is written");
            Assert.That(ExportFiles(rig.harness), Is.EqualTo(new[] { "session-log-20261021T123456Z.jsonl" }), "no temp file is left behind");
            Assert.That(rig.harness.Files.Has(result.FilePath), Is.True);
        }

        [Test]
        public void TheExportIncludesWhatIsStillBufferedAndMatchesTheSegmentsLineForLine()
        {
            var rig = Rig(contracts: 4); // not flushed: the start record flushed, the contracts are buffered
            SessionLogExportResult result = rig.export.Export();

            byte[] exported = rig.harness.Files.Bytes(result.FilePath);
            List<Dictionary<string, object>> records = Harness.Parse(exported);
            Assert.That(records.Select(r => (string)r["t"]), Is.EqualTo(new[] { "header", "lifecycle", "contract", "contract", "contract", "contract" }));
            Assert.That(exported, Is.EqualTo(rig.harness.Files.Bytes(rig.harness.Segment(0))), "the export is the segment text, in order");
            Assert.That(exported[exported.Length - 1], Is.EqualTo((byte)'\n'));
        }

        [Test]
        public void ARotatedLogExportsTheOlderSegmentFirstWithoutGapsOrReordering()
        {
            var rig = Rig(contracts: 0);
            int next = 0;
            for (int batch = 0; batch < 20; batch++)
            {
                for (int i = 0; i < 300; i++, next++)
                    rig.harness.Writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, "contract.deliver", next, 1, 1, 1));
                rig.harness.Flush();
            }

            SessionLogExportResult result = rig.export.Export();

            long[] indexes = Harness.Parse(rig.harness.Files.Bytes(result.FilePath)).Where(r => (string)r["t"] == "contract").Select(r => (long)r["index"]).ToArray();
            Assert.That(indexes.Last(), Is.EqualTo(next - 1));
            for (int i = 1; i < indexes.Length; i++) Assert.That(indexes[i], Is.EqualTo(indexes[i - 1] + 1));
            Assert.That(result.Lines, Is.EqualTo(indexes.Length));
        }

        [Test]
        public void ATornLastLineAndACrcFailingLineAreSkipped()
        {
            var rig = Rig(contracts: 5);
            rig.harness.Flush();
            byte[] bytes = rig.harness.Files.Bytes(rig.harness.Segment(0));

            // damage one digit inside the third line, and append a torn line without its newline
            int lineStart = 0;
            for (int skipped = 0; skipped < 2; skipped++) lineStart = Array.IndexOf(bytes, (byte)'\n', lineStart) + 1;
            int damaged = Array.IndexOf(bytes, (byte)':', lineStart);
            bytes[damaged + 2] ^= 0x01;
            byte[] torn = Encoding.ASCII.GetBytes("{\"t\":\"contract\",\"v\":1,\"event\":\"acc");
            rig.harness.Files.Put(rig.harness.Segment(0), bytes.Concat(torn).ToArray());
            int totalLines = 2 + 5;

            SessionLogExportResult result = rig.export.Export();

            Assert.That(result.Status, Is.EqualTo(SessionLogExportStatus.Exported));
            Assert.That(result.Lines, Is.EqualTo(totalLines - 1), "the CRC-failing line is skipped, the torn tail too");
            byte[] exported = rig.harness.Files.Bytes(result.FilePath);
            Assert.That(Harness.Parse(exported), Has.Count.EqualTo(totalLines - 1));
            Assert.That(exported.Count(b => b == (byte)'\n'), Is.EqualTo(totalLines - 1), "only whole lines are written");
        }

        [Test]
        public void AnEmptyLogWritesNothingAndReportsNothingToExport()
        {
            Harness h = Harness.Create();
            var export = new SessionLogExport(h.Writer, h.Files, () => Stamp);

            SessionLogExportResult result = export.Export();

            Assert.That(result.Status, Is.EqualTo(SessionLogExportStatus.NothingToExport));
            Assert.That(result.FilePath, Is.Null);
            Assert.That(h.Files.Log.Any(entry => entry.StartsWith("WriteNew", StringComparison.Ordinal) || entry.StartsWith("Move", StringComparison.Ordinal)), Is.False);
            Assert.That(ExportFiles(h), Is.Empty);
        }

        [Test]
        public void AtMostTenExportsAreKeptAndTheOldestGoFirst()
        {
            var rig = Rig();
            var stamps = new List<string>();
            for (int i = 0; i < 13; i++)
            {
                SessionLogExportResult result = rig.export.Export();
                Assert.That(result.Status, Is.EqualTo(SessionLogExportStatus.Exported));
                stamps.Add(Path.GetFileName(result.FilePath));
                rig.advance(1);
            }

            string[] kept = ExportFiles(rig.harness);
            Assert.That(kept, Has.Length.EqualTo(SessionLogExport.MaxExportFiles));
            Assert.That(kept, Is.EquivalentTo(stamps.Skip(3)), "the three oldest are gone");
        }

        [Test]
        public void TwoExportsInTheSameSecondGetDistinctNames()
        {
            var rig = Rig();
            SessionLogExportResult first = rig.export.Export();
            SessionLogExportResult second = rig.export.Export();
            Assert.That(second.FilePath, Is.Not.EqualTo(first.FilePath));
            Assert.That(ExportFiles(rig.harness), Has.Length.EqualTo(2));
        }

        [Test]
        public void AnOldLeftoverTempFileIsRemovedByTheNextSuccessfulExport()
        {
            var rig = Rig();
            string leftover = Path.Combine(rig.harness.Writer.ExportDirectoryPath, "session-log-20200101T000000Z.jsonl.tmp");
            rig.harness.Files.Put(leftover, new byte[] { 1, 2, 3 });
            rig.export.Export();
            Assert.That(rig.harness.Files.Has(leftover), Is.False);
        }

        [TestCase("WriteNew")]
        [TestCase("Move")]
        public void AFailedExportChangesNothingLeavesNoTempFileAndCanBeRetried(string failingOperation)
        {
            var rig = Rig();
            rig.harness.Files.FailOperation = failingOperation;

            SessionLogExportResult failed = rig.export.Export();

            Assert.That(failed.Status, Is.EqualTo(SessionLogExportStatus.Failed));
            Assert.That(failed.FilePath, Is.Null);
            Assert.That(failed.Failure, Is.EqualTo(SessionLogExportFailure.Unknown));
            Assert.That(ExportFiles(rig.harness), Is.Empty, "neither a partial export nor a temp file");
            Assert.That(rig.harness.AllRecords(), Is.Not.Empty, "the log itself is untouched");

            rig.harness.Files.FailOperation = null;
            SessionLogExportResult retried = rig.export.Export();
            Assert.That(retried.Status, Is.EqualTo(SessionLogExportStatus.Exported));
            Assert.That(ExportFiles(rig.harness), Has.Length.EqualTo(1));
        }

        [Test]
        public void AFailedExportKeepsTheExistingExports()
        {
            var rig = Rig();
            for (int i = 0; i < 3; i++)
            {
                rig.export.Export();
                rig.advance(1);
            }

            rig.harness.Files.FailOperation = "WriteNew";
            Assert.That(rig.export.Export().Status, Is.EqualTo(SessionLogExportStatus.Failed));
            Assert.That(ExportFiles(rig.harness), Has.Length.EqualTo(3), "nothing was pruned by a failed export");
        }

        [Test]
        public void FailuresAreReportedByCategory()
        {
            Assert.That(SessionLogExport.Classify(new IOException("full", unchecked((int)0x80070070))), Is.EqualTo(SessionLogExportFailure.NoSpace), "ERROR_DISK_FULL");
            Assert.That(SessionLogExport.Classify(new IOException("full", unchecked((int)0x80070027))), Is.EqualTo(SessionLogExportFailure.NoSpace), "ERROR_HANDLE_DISK_FULL");
            Assert.That(SessionLogExport.Classify(new IOException("full", 28)), Is.EqualTo(SessionLogExportFailure.NoSpace), "ENOSPC");
            Assert.That(SessionLogExport.Classify(new UnauthorizedAccessException()), Is.EqualTo(SessionLogExportFailure.NoPermission));
            Assert.That(SessionLogExport.Classify(new System.Security.SecurityException()), Is.EqualTo(SessionLogExportFailure.NoPermission));
            Assert.That(SessionLogExport.Classify(new IOException("other")), Is.EqualTo(SessionLogExportFailure.Unknown));
            Assert.That(SessionLogExport.Classify(new InvalidOperationException()), Is.EqualTo(SessionLogExportFailure.Unknown));

            var rig = Rig();
            rig.harness.Files.FailOperation = "WriteNew";
            rig.harness.Files.Failure = new IOException("full", unchecked((int)0x80070070));
            Assert.That(rig.export.Export().Failure, Is.EqualTo(SessionLogExportFailure.NoSpace));
            rig.harness.Files.Failure = new UnauthorizedAccessException();
            Assert.That(rig.export.Export().Failure, Is.EqualTo(SessionLogExportFailure.NoPermission));
        }

        [Test]
        public void TheShownPathIsNeverWrittenIntoTheLogOrTheExport()
        {
            var rig = Rig();
            SessionLogExportResult result = rig.export.Export();
            rig.harness.Flush();

            string root = rig.harness.Root;
            Assert.That(result.FilePath, Does.Contain(root), "the path is for the screen");
            foreach (byte[] file in new[] { rig.harness.Files.Bytes(result.FilePath), rig.harness.Files.Bytes(rig.harness.Segment(0)), rig.harness.Files.Bytes(rig.harness.Segment(1)) })
            {
                if (file == null) continue;
                string text = Encoding.ASCII.GetString(file);
                Assert.That(text, Does.Not.Contain(root));
                Assert.That(text, Does.Not.Contain("Exports"));
                Assert.That(text, Does.Not.Contain("session-log-"));
            }
        }
    }
}
