using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T121 (Issue #203), SC-005, FR-024, FR-028: the durable transitions in the built Windows player, on the real
    /// <c>JsonRunCheckpointStore</c> file protocol over an isolated temporary root. A "restart" abandons every in-memory
    /// object and composes a fresh session over the same files, which is what a process kill and relaunch does.
    /// </summary>
    public sealed class RunTransitionPlayerTests
    {
        private string root;

        [SetUp]
        public void CreateRoot() => root = RunPlayerFixtures.NewRoot();

        [TearDown]
        public void DeleteRoot() => RunPlayerFixtures.DeleteRoot(root);

        // ---- Prestige interruption with restart (FR-024, R09) -------------------------------------------------

        /// <summary>A scaled-threshold run that has just become eligible and is saved, so a restart has a durable run to load.</summary>
        private RunRig EligibleSavedRun(out long award)
        {
            var rig = RunRig.Create(root, RunPlayerFixtures.ScaledCatalog(), RunPlayerFixtures.StartUtcMs);
            rig.ColdStartOk(null);
            new RunJourneyPolicy(rig).PlayUntilEligible();
            rig.SyncClock();
            var saved = rig.Persistence.SaveNow();
            Assert.That(saved.IsSuccess, Is.True, saved.ReasonCode);
            var preview = rig.Commands.GetPrestigePreview();
            Assert.That(preview.Value.IsEligible, Is.True);
            award = preview.Value.IpAward;
            Assert.That(award, Is.GreaterThan(0));
            return rig;
        }

        [Category("StandalonePlayer")]
        [Test]
        public void ARestartWhileThePrestigeIsPendingKeepsTheOldRunAndThePrestigeIsAwardedOnceAfterwards()
        {
            var rig = EligibleSavedRun(out long award);
            var oldRunId = rig.Session.State.RunId;
            var savedFingerprint = RunPlayerFixtures.Fingerprint(rig.Session);

            var confirmed = rig.Commands.ConfirmPrestige();
            Assert.That(confirmed.IsSuccess, Is.True, confirmed.ReasonCode);
            Assert.That(rig.Session.PendingTransition, Is.Not.Null);
            Assert.That(rig.Commands.ConfirmPrestige().ReasonCode, Is.EqualTo(RunReasons.TransitionPending), "QA-4: no second confirmation.");
            Assert.That(rig.Persistence.SaveNow().ReasonCode, Is.EqualTo(RunReasons.TransitionPending), "QA-4: no checkpoint races the candidate.");
            Assert.That(rig.Session.Permanent.Ip, Is.Zero, "Nothing is awarded before the durable promotion.");

            // The process is killed here: every in-memory object is abandoned.
            var restarted = RunRig.Create(root, rig.Catalog, rig.Clock.UnixMs);
            var resumed = restarted.ColdStartOk(null);
            Assert.That(resumed.Imported, Is.False);
            Assert.That(restarted.Session.PendingTransition, Is.Null, "A pending transition does not survive a restart.");
            Assert.That(restarted.Session.State.RunId, Is.EqualTo(oldRunId), "The old run is loaded.");
            Assert.That(restarted.Session.Permanent.Ip, Is.Zero);
            Assert.That(RunPlayerFixtures.Fingerprint(restarted.Session), Is.EqualTo(savedFingerprint));
            Assert.That(restarted.Store.ReadActive().Checkpoint.Run.RunId, Is.EqualTo(oldRunId), "No successor became durable.");

            // The player prestiges again and receives the award exactly once.
            restarted.SyncClock();
            var again = restarted.Commands.ConfirmPrestige();
            Assert.That(again.IsSuccess, Is.True, again.ReasonCode);
            Assert.That(again.Value.IpAwarded, Is.EqualTo(award));
            Assert.That(restarted.Commands.PromotePrestige().IsSuccess, Is.True);
            Assert.That(restarted.Session.State.RunNumber, Is.EqualTo(2));
            Assert.That(restarted.Session.State.PreviousRunId, Is.EqualTo(oldRunId));
            Assert.That(restarted.Session.Permanent.Ip, Is.EqualTo(award));
        }

        [Category("StandalonePlayer")]
        [Test]
        public void AnInterruptionAfterTheDurableWriteIsPublishedOnRetryWithoutASecondAward()
        {
            var rig = EligibleSavedRun(out long award);
            var oldRunId = rig.Session.State.RunId;
            Assert.That(rig.Commands.ConfirmPrestige().IsSuccess, Is.True);

            // The candidate becomes durable, but the store reports a failure: the old run stays live, the transition stays pending.
            rig.Store.FailNextPromoteAfterWriting = true;
            var interrupted = rig.Commands.PromotePrestige();
            Assert.That(interrupted.IsSuccess, Is.False);
            Assert.That(rig.Session.State.RunId, Is.EqualTo(oldRunId), "The old run is kept.");
            Assert.That(rig.Session.PendingTransition, Is.Not.Null, "The transition stays retryable.");
            Assert.That(rig.Session.Permanent.Ip, Is.Zero);
            Assert.That(rig.Store.ReadActive().Checkpoint.Run.PreviousRunId, Is.EqualTo(oldRunId), "The successor is already durable.");

            var retried = rig.Persistence.RetryPendingTransition();
            Assert.That(retried.IsSuccess, Is.True, retried.ReasonCode);
            Assert.That(rig.Session.PendingTransition, Is.Null);
            Assert.That(rig.Session.State.RunNumber, Is.EqualTo(2));
            Assert.That(rig.Session.State.PreviousRunId, Is.EqualTo(oldRunId));
            Assert.That(rig.Session.Permanent.Ip, Is.EqualTo(award), "The retry published the durable run without a second award.");
            Assert.That(rig.Session.Permanent.PrestigeCount, Is.EqualTo(1));
        }

        [Category("StandalonePlayer")]
        [Test]
        public void ARestartAfterTheDurableWriteLoadsTheNewRunOnceWithTheAwardOnce()
        {
            var rig = EligibleSavedRun(out long award);
            var oldRunId = rig.Session.State.RunId;
            Assert.That(rig.Commands.ConfirmPrestige().IsSuccess, Is.True);
            rig.Store.FailNextPromoteAfterWriting = true;
            Assert.That(rig.Commands.PromotePrestige().IsSuccess, Is.False);

            // The process is killed between the durable write and the publication.
            var restarted = RunRig.Create(root, rig.Catalog, rig.Clock.UnixMs);
            var resumed = restarted.ColdStartOk(null);
            Assert.That(resumed.Imported, Is.False);
            Assert.That(restarted.Session.State.RunNumber, Is.EqualTo(2), "The durable successor is the run that loads.");
            Assert.That(restarted.Session.State.PreviousRunId, Is.EqualTo(oldRunId));
            Assert.That(restarted.Session.Permanent.Ip, Is.EqualTo(award), "The award is present exactly once.");
            Assert.That(restarted.Session.Permanent.PrestigeCount, Is.EqualTo(1));
            Assert.That(restarted.Session.PendingTransition, Is.Null);
            Assert.That(restarted.Commands.ConfirmPrestige().ReasonCode, Is.EqualTo(RunReasons.PrestigeBelowThreshold),
                "The new run starts below the threshold; the prestige is not repeated.");

            // A second restart changes nothing.
            var second = RunRig.Create(root, rig.Catalog, rig.Clock.UnixMs);
            second.ColdStartOk(null);
            Assert.That(second.Session.State.RunId, Is.EqualTo(restarted.Session.State.RunId));
            Assert.That(second.Session.Permanent.Ip, Is.EqualTo(award));
        }

        // ---- V1 import from the packaged fixture (FR-028, SC-005) ---------------------------------------------

        private string SeedV1FromPackagedFixture()
        {
            var legacyCatalog = RunPlayerFixtures.DevelopmentCatalog();
            var decoded = SaveV1Mapper.Deserialize(RunPlayerFixtures.Bytes(RunPlayerFixtures.V1FixtureName), legacyCatalog);
            Assert.That(decoded.Succeeded, Is.True, decoded.Message);
            var promoted = new JsonCheckpointStore(root, legacyCatalog).Promote(decoded.Checkpoint);
            Assert.That(promoted.Succeeded, Is.True, promoted.Message);
            return SaveV1CheckpointFiles.Layout.DirectoryFor(root);
        }

        [Category("StandalonePlayer")]
        [Test]
        public void TheV1ImportFromThePackagedFixtureSucceedsOnceAndTheV1DirectoryStaysByteIdentical()
        {
            string v1Directory = SeedV1FromPackagedFixture();
            var v1Before = RunPlayerFixtures.CaptureFiles(v1Directory);
            Assert.That(v1Before, Is.Not.Empty, "The packaged V1 fixture was published through the legacy protocol.");

            var catalog = RunPlayerFixtures.IronCatalog();
            var legacy = new SaveV1CheckpointSource(root, RunPlayerFixtures.DevelopmentCatalog());
            var rig = RunRig.Create(root, catalog, RunPlayerFixtures.V1FixtureDepartureUtcMs);
            var outcome = rig.ColdStartOk(legacy);

            Assert.That(outcome.Imported, Is.True);
            Assert.That(outcome.StateReplaced, Is.True);
            Assert.That(rig.Persistence.BlockedImportCode, Is.Null);
            var run = rig.Session.State;
            Assert.That(run.RunNumber, Is.EqualTo(1));
            Assert.That(run.Machines.Select(m => m.MachineId), Is.EqualTo(new[] { "machine.smelter" }));
            Assert.That(run.Inventory.Units("iron.ore"), Is.EqualTo(4L), "The four development Ore of the fixture became Iron Ore.");
            Assert.That(run.Inventory.Units("iron.bar"), Is.Zero);
            Assert.That(run.Cash, Is.EqualTo(catalog.RunStart.StartingCash));

            var durable = rig.Store.ReadActive();
            Assert.That(durable.Succeeded, Is.True, durable.Message);
            Assert.That(durable.Checkpoint.Import, Is.Not.Null);
            Assert.That(durable.Checkpoint.Import.SourceSchemaVersion, Is.EqualTo(1));
            Assert.That(durable.Checkpoint.Import.SourceCheckpointId, Is.EqualTo(RunPlayerFixtures.V1FixtureCheckpointId));
            var v2Before = RunPlayerFixtures.CaptureFiles(SaveV2CheckpointFiles.Layout.DirectoryFor(root));
            Assert.That(v2Before, Is.Not.Empty);

            RunPlayerFixtures.AssertSameFiles(v1Before, RunPlayerFixtures.CaptureFiles(v1Directory), "V1 after the import");

            // Exactly once: a restart selects the V2 checkpoint and never reads V1 again.
            var restarted = RunRig.Create(root, catalog, rig.Clock.UnixMs);
            var resumed = restarted.ColdStartOk(new SaveV1CheckpointSource(root, RunPlayerFixtures.DevelopmentCatalog()));
            Assert.That(resumed.Imported, Is.False, "V1 is not imported a second time.");
            Assert.That(restarted.Session.State.RunId, Is.EqualTo(run.RunId));
            RunPlayerFixtures.AssertSameFiles(v1Before, RunPlayerFixtures.CaptureFiles(v1Directory), "V1 after the restart");
        }
    }
}
