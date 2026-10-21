using System.IO;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.SessionLog
{
    /// <summary>
    /// T340 (T338 follow-up): <see cref="SessionLogRuntime"/> as the <see cref="ISessionLogControls"/> the Market section reaches the log through.
    /// Every call goes to the writer, whose own serialization stays the only path to the files; the export path is remembered for the screen in
    /// memory only and is never written to the log or the export (SEC-2).
    /// </summary>
    public sealed class SessionLogControlsTests
    {
        private static (Harness harness, ISessionLogControls controls) Rig(int contracts = 2)
        {
            Harness h = Harness.Create();
            h.Writer.Start();
            for (int i = 0; i < contracts; i++)
                h.Writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, "contract.deliver", i, 5, 7, 1_500_000));
            return (h, new SessionLogRuntime(h.Writer, new SessionLogExport(h.Writer, h.Files)));
        }

        [Test]
        public void TheControlsReportAndSwitchTheWritersRecordingState()
        {
            var rig = Rig();
            Assert.That(rig.controls.Enabled, Is.EqualTo(rig.harness.Writer.Enabled));
            Assert.That(rig.controls.Enabled, Is.True);

            rig.controls.SetEnabled(false);
            Assert.That(rig.controls.Enabled, Is.False);
            Assert.That(rig.harness.Writer.Enabled, Is.False, "the writer is the one switch, not a copy");
            Assert.That(rig.harness.Prefs.Value, Is.False, "the setting is persisted by the writer");

            rig.controls.SetEnabled(true);
            Assert.That(rig.controls.Enabled, Is.True);
        }

        [Test]
        public void ClearTruncatesThroughTheWriterAndReportsSuccess()
        {
            var rig = Rig();
            rig.harness.Writer.Flush();
            Assert.That(rig.harness.Files.Bytes(rig.harness.Segment(0)), Is.Not.Empty);

            Assert.That(rig.controls.Clear(), Is.True);

            Assert.That(rig.harness.Records(0), Has.Count.LessThanOrEqualTo(3), "the file holds only the new header and the cleared record");
        }

        [Test]
        public void ExportRemembersOnlyASuccessfulFileAsTheLastExport()
        {
            var rig = Rig();
            Assert.That(rig.controls.LastExport.FilePath, Is.Null, "nothing exported yet");

            SessionLogExportResult first = rig.controls.Export();
            Assert.That(first.Status, Is.EqualTo(SessionLogExportStatus.Exported));
            Assert.That(rig.controls.LastExport.FilePath, Is.EqualTo(first.FilePath));
            Assert.That(Path.GetFileName(first.FilePath), Does.StartWith("session-log-"));

            rig.harness.Files.FailOperation = "WriteNew";
            SessionLogExportResult failed = rig.controls.Export();
            Assert.That(failed.Status, Is.EqualTo(SessionLogExportStatus.Failed));
            Assert.That(rig.controls.LastExport.FilePath, Is.EqualTo(first.FilePath), "a failed export keeps the last good one");
        }

        [Test]
        public void AnEmptyLogExportsNothingAndKeepsNoLastExport()
        {
            Harness h = Harness.Create();
            ISessionLogControls controls = new SessionLogRuntime(h.Writer, new SessionLogExport(h.Writer, h.Files));

            SessionLogExportResult result = controls.Export();

            Assert.That(result.Status, Is.EqualTo(SessionLogExportStatus.NothingToExport));
            Assert.That(controls.LastExport.FilePath, Is.Null);
        }

        [Test]
        public void ThePathOfAnExportNeverEntersTheLogOrAnotherExport()
        {
            var rig = Rig();
            SessionLogExportResult first = rig.controls.Export();
            rig.harness.Writer.Flush();
            SessionLogExportResult second = rig.controls.Export();

            string directory = first.DirectoryPath;
            foreach (string path in new[] { first.FilePath, second.FilePath })
            {
                string text = System.Text.Encoding.UTF8.GetString(rig.harness.Files.Bytes(path));
                Assert.That(text, Does.Not.Contain(directory), "the export holds no path");
            }

            string log = System.Text.Encoding.UTF8.GetString(rig.harness.Files.Bytes(rig.harness.Segment(0)));
            Assert.That(log, Does.Not.Contain(directory));
        }
    }
}
