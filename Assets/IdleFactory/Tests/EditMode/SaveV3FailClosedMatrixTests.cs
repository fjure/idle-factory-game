using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using static IdleFactory.Tests.EditMode.Fixtures.Run.ContractFixtures;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The Save V3 fail-closed matrix (T335, issue #400): every V3 row of the QA-11 fixture-to-assertion table
    /// (specs/005-economy-contracts/tasks.md) and the delegated #399 follow-ups (unsorted lists without a duplicate, the future-instant bound of
    /// <c>readyAtUtcMs</c> and <c>windowEndUtcMs</c>, the active bonus offer without an open <c>bonusOffers</c> record). Each rejected row is
    /// checked three ways: the mapper's static code, the store outcome's persistence reason (<c>checkpoint.invalid</c>, <c>checkpoint.content</c>
    /// or <c>checkpoint.version</c>), and a store holding the file as its active generation that refuses every write and changes nothing.
    /// </summary>
    public sealed class SaveV3FailClosedMatrixTests
    {
        private static List<SaveV3Fixture> _matrix;
        private bool _logToUnity;

        [OneTimeSetUp]
        public void BuildMatrix()
        {
            _matrix = SaveV3Fixtures.Matrix();
        }

        [SetUp]
        public void SetUp()
        {
            _logToUnity = CheckpointDiagnostics.LogToUnity;
            CheckpointDiagnostics.LogToUnity = false;
        }

        [TearDown]
        public void TearDown()
        {
            CheckpointDiagnostics.LogToUnity = _logToUnity;
        }

        private static SaveV3Fixture Row(string name) => _matrix.Single(f => f.Name == name);

        [Test]
        public void TheMatrixCoversEveryV3RowOfQa11()
        {
            string[] qa11 =
            {
                "v3-autodeliver-true", "v3-autodeliver-on-produce", "v3-produced-over-required", "v3-bool-wrong-type", "v3-cooldown-duplicate-offer",
                "v3-cooldown-past", "v3-bonus-missing-windowend (triggered)", "v3-bonus-missing-windowend (consumed)", "v3-bonus-lapsed-triggered",
                "v3-unknown-member", "v3-drift-removed-offer", "v3-drift-objective-changed", "v3-drift-unknown-machine", "v4-version",
            };
            Assert.That(_matrix.Select(f => f.Name), Is.SupersetOf(qa11));
            Assert.That(_matrix.Select(f => f.Name).Distinct().Count(), Is.EqualTo(_matrix.Count), "names are unique");
        }

        [Test]
        public void EveryRejectedRow_FailsWithItsStaticCode_AndReturnsNoDocument()
        {
            var failures = new List<string>();
            foreach (SaveV3Fixture f in _matrix.Where(x => !x.Loads))
            {
                SaveV2ReadResult result = SaveV2Mapper.Deserialize(f.Bytes, f.Catalog);
                if (result.IsSuccess) failures.Add(f.Name + ": accepted, expected " + f.ExpectedCode);
                else if (result.Code != f.ExpectedCode) failures.Add(f.Name + ": " + result.Code + ", expected " + f.ExpectedCode);
                else if (result.Document != null) failures.Add(f.Name + ": a rejected read returned a document");
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void EveryRejectedRow_MapsToItsPersistenceReason_WithStaticCopyOnly()
        {
            var failures = new List<string>();
            foreach (SaveV3Fixture f in _matrix.Where(x => !x.Loads))
            {
                CheckpointDecodeResult<RunCheckpoint> decoded = new SaveV2CheckpointCodec(f.Catalog).Deserialize(f.Bytes);
                string reason = decoded.Succeeded ? null : RunPersistenceReasons.From(decoded.Outcome);
                if (reason != f.ExpectedReason) failures.Add(f.Name + ": " + (reason ?? "loaded") + ", expected " + f.ExpectedReason);
                else if (decoded.Message != f.ExpectedCode + ": " + SaveV2Codes.Message(f.ExpectedCode)) failures.Add(f.Name + ": copy " + decoded.Message);
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void EveryRejectedRow_AsTheActiveGeneration_IsRefused_AndTheStoreWritesNothing()
        {
            var failures = new List<string>();
            var id = new CheckpointId("44444444-4444-4444-4444-444444444444");
            foreach (SaveV3Fixture f in _matrix.Where(x => !x.Loads))
            {
                var backend = new ScriptedBackend("v3-matrix");
                Assert.That(new CheckpointGenerationFiles<RawV1Checkpoint>(backend, new RawV1Codec(id, 1)).Promote(new RawV1Checkpoint(id, 1, f.Bytes)).Succeeded,
                    Is.True, f.Name);
                var before = backend.Snapshot();
                var store = new JsonRunCheckpointStore("memory-root", f.Catalog, backend);

                RunCheckpointReadResult active = store.ReadActive();
                if (active.Succeeded || RunPersistenceReasons.From(active.Outcome) != f.ExpectedReason)
                    failures.Add(f.Name + ": the store read " + (active.Succeeded ? "succeeded" : RunPersistenceReasons.From(active.Outcome)));

                // A valid V3 checkpoint can neither replace nor supersede the refused generation.
                RunCheckpoint valid = ValidCheckpoint(f.Catalog);
                backend.ResetCounters();
                if (store.Promote(valid).Succeeded) failures.Add(f.Name + ": a promote over it succeeded");
                if (f.ExpectedReason != RunPersistenceReasons.InvalidCheckpoint && store.SupersedeDamaged(valid).Succeeded)
                    failures.Add(f.Name + ": a supersede over another build's save succeeded");
                if (backend.Log.Any(entry => entry.StartsWith("Create:", System.StringComparison.Ordinal)))
                    failures.Add(f.Name + ": the store created a file");
                foreach (var entry in before)
                    if (!backend.Files[entry.Key].SequenceEqual(entry.Value)) failures.Add(f.Name + ": " + entry.Key + " changed");
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        /// <summary>A valid V3 checkpoint of a fresh settled rig over <paramref name="catalog"/>, newer than the seeded generation 1.</summary>
        private static RunCheckpoint ValidCheckpoint(RunCatalog catalog)
        {
            var state = new RunState(catalog, RunSimHarness.Id(9100), 1, null, 25, 0, 1, 1, new[] { Ore });
            return new RunCheckpoint(new CheckpointId(SaveV2Scenarios.Uuid(700)), 5, RunSimHarness.StartUtcMs, catalog.OfflinePolicy.Revision, null,
                catalog.ContentRevision, null, state, new PermanentState(catalog));
        }

        [Test]
        public void EveryLoadingRow_LoadsWithTheExpectedDrops_AndRewritesWithoutTheDroppedEntries()
        {
            var failures = new List<string>();
            foreach (SaveV3Fixture f in _matrix.Where(x => x.Loads))
            {
                SaveV2ReadResult read = SaveV2Mapper.Deserialize(f.Bytes, f.Catalog);
                if (!read.IsSuccess)
                {
                    failures.Add(f.Name + ": rejected " + read.Code);
                    continue;
                }

                if (read.Document.DroppedContractEntries != f.ExpectedDropped)
                    failures.Add(f.Name + ": dropped " + read.Document.DroppedContractEntries + ", expected " + f.ExpectedDropped);
                if (read.Document.Run.Validate() != RunStateResult.Ok) failures.Add(f.Name + ": the loaded state is not valid");

                // The next write is canonical V3 of what was kept, and reads back with nothing more to drop.
                SaveV2WriteResult rewritten = SaveV2Mapper.Serialize(read.Document.Meta, read.Document.Run, read.Document.Permanent);
                if (!rewritten.IsSuccess)
                {
                    failures.Add(f.Name + ": the rewrite failed " + rewritten.Code);
                    continue;
                }

                SaveV2ReadResult again = SaveV2Mapper.Deserialize(rewritten.Bytes, f.Catalog);
                if (!again.IsSuccess || again.Document.DroppedContractEntries != 0) failures.Add(f.Name + ": the rewrite does not read back cleanly");
                if (f.ExpectedDropped == 0 && !rewritten.Bytes.SequenceEqual(f.Bytes)) failures.Add(f.Name + ": not canonical");
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        // ---- loading rows with a behavioural assertion ----------------------------------------------------------------------

        [Test]
        public void V3AutoDeliverTrue_RestoresTheFlagOnTheDeliveryContract()
        {
            SaveV3Fixture f = Row("v3-autodeliver-true");
            SaveV3Fixtures.Load(f.Catalog, f.Bytes, out SaveV2Document doc);
            Assert.That(doc.Run.Contracts.Active.OfferId, Is.EqualTo(Deliver));
            Assert.That(doc.Run.Contracts.Active.AutoDeliver, Is.True);
        }

        [Test]
        public void V3CooldownPast_LoadsAsReady()
        {
            SaveV3Fixture f = Row("v3-cooldown-past");
            RunCommandFixtures.Rig loaded = SaveV3Fixtures.Load(f.Catalog, f.Bytes, out SaveV2Document doc);
            long readyAt = doc.Run.Contracts.CooldownStamps().Single(c => c.OfferId == Deliver).ReadyAtUtcMs;
            Assert.That(readyAt, Is.LessThan(doc.Meta.DepartureUtcMs), "the stamp lies before the load's timeline start");
            Assert.That(Board(loaded).CooldownRemainingMs(Deliver, loaded.Session.TimelineUtcMs), Is.Zero);
            Assert.That(loaded.Commands.AcceptContract(Deliver).IsSuccess, Is.True);
        }

        [Test]
        public void V3BonusLapsedTriggered_LoadsAndNeverTriggersAgainAfterTheReload()
        {
            SaveV3Fixture f = Row("v3-bonus-lapsed-triggered");
            RunCommandFixtures.Rig loaded = SaveV3Fixtures.Load(f.Catalog, f.Bytes, out SaveV2Document doc);
            BonusOfferRecord record = doc.Run.Contracts.BonusOffers().Single(b => b.OfferId == BonusOre);
            Assert.That(record.Consumed, Is.False, "persisted as triggered");
            Assert.That(record.WindowEndUtcMs, Is.LessThan(doc.Meta.DepartureUtcMs), "lapsed before the save");

            loaded.Advance(10 * Second); // Ore keeps being produced, so the trigger condition still holds
            BonusOfferState state = Board(loaded).BonusState(BonusOre);
            Assert.That(state.Triggered, Is.True);
            Assert.That(state.WindowEndUtcMs, Is.EqualTo(record.WindowEndUtcMs), "no second window");
            Assert.That(loaded.Commands.AcceptContract(BonusOre).ReasonCode, Is.EqualTo(RunReasons.ContractBonusUnavailable));
        }

        [Test]
        public void TheFutureBoundRows_LoadExactlyAtTheBound()
        {
            foreach (string name in new[] { "v3-cooldown-at-future-bound", "v3-bonus-window-at-future-bound" })
            {
                SaveV3Fixture f = Row(name);
                SaveV2ReadResult read = SaveV2Mapper.Deserialize(f.Bytes, f.Catalog);
                Assert.That(read.IsSuccess, Is.True, name + " " + read.Code);
            }
        }

        [Test]
        public void AnActiveBonusOffer_WithItsOpenRecord_Loads_AndAV2FileIsNotCheckedForBonusState()
        {
            SaveV3Fixture f = Row("v3-active-bonus-with-open-record");
            SaveV3Fixtures.Load(f.Catalog, f.Bytes, out SaveV2Document doc);
            Assert.That(doc.Run.Contracts.Active.OfferId, Is.EqualTo(BonusAlways));

            // The same state as a V2 file (no bonus state in V2) keeps loading: the cross-field check is V3 only.
            byte[] v2 = SaveV3Fixtures.AsV2Text(f.Bytes);
            SaveV2ReadResult read = SaveV2Mapper.Deserialize(v2, f.Catalog);
            Assert.That(read.IsSuccess, Is.True, read.Code);
            Assert.That(read.Document.SchemaVersion, Is.EqualTo(SaveV2Mapper.LegacySchemaVersion));
            Assert.That(read.Document.Run.Contracts.Active.OfferId, Is.EqualTo(BonusAlways));
            Assert.That(read.Document.Run.Contracts.BonusOffers(), Is.Empty);
        }

        [Test]
        public void AForgedActiveBonusWithoutARecord_CannotFireItsTriggerAgain_BecauseItIsRejected()
        {
            // #399 QA warning: EndContract consumes a bonus only when it is triggered, so an active bonus offer with no record would leave the
            // trigger free to fire again. The load refuses such a file instead.
            SaveV3Fixture f = Row("v3-active-bonus-without-record");
            SaveV2ReadResult read = SaveV2Mapper.Deserialize(f.Bytes, f.Catalog);
            Assert.That(read.IsSuccess, Is.False);
            Assert.That(read.Code, Is.EqualTo(SaveV2Codes.Invariant));
            Assert.That(RunPersistenceReasons.From(new SaveV2CheckpointCodec(f.Catalog).Deserialize(f.Bytes).Outcome), Is.EqualTo(RunPersistenceReasons.InvalidCheckpoint));
        }
    }
}
