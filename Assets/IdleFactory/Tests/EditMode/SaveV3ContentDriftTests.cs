using System.Linq;
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
    /// Save V3 content drift (T335, issue #400, contracts/save-v3.md "Content drift on load"): contract-scoped entries of offers the loaded
    /// content no longer has are dropped and counted, never granted, and reported as the informational <c>contract.dropped</c>; the run-wide
    /// completion total is kept (SEC-8); an active contract whose offer changed objective is dropped; every other unresolved ID stays
    /// fail-closed (<c>checkpoint.content</c>).
    /// </summary>
    public sealed class SaveV3ContentDriftTests
    {
        private bool _logToUnity;

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

        private static SaveV2ReadResult Read(RunCatalog catalog, byte[] bytes) => SaveV2Mapper.Deserialize(bytes, catalog);

        // ---- removed offers ---------------------------------------------------------------------------------------------------

        [Test]
        public void ARemovedOffer_DropsEveryContractScopedEntry_KeepsTheRunWideTotal_AndCountsTheDrops()
        {
            RunCommandFixtures.Rig rig = SaveV3Fixtures.AllMembers();
            byte[] bytes = SaveV3Fixtures.Write(rig);
            RunCatalog drifted = SaveV3Fixtures.Without(rig.Catalog, BonusAlways, Produce);

            SaveV2ReadResult read = Read(drifted, bytes);
            Assert.That(read.IsSuccess, Is.True, read.Code);
            ContractBoard board = read.Document.Run.Contracts;
            Assert.That(read.Document.DroppedContractEntries, Is.EqualTo(4), "bonus-always: count, cooldown, bonus record; the active production contract");
            Assert.That(board.Active, Is.Null, "the active contract of the removed offer is dropped");
            Assert.That(board.CompletionCounts().Select(c => c.Id + ":" + c.Count), Is.EqualTo(new[] { Deliver + ":1" }));
            Assert.That(board.CooldownStamps().Select(c => c.OfferId), Is.EqualTo(new[] { Deliver }));
            Assert.That(board.BonusOffers().Select(b => b.OfferId), Is.EqualTo(new[] { BonusOre }));
            Assert.That(board.RunCompletionTotal, Is.EqualTo(2), "runCompletionTotal is kept although a count was dropped (SEC-8)");
            Assert.That(read.Document.Run.Unlocked, Does.Not.Contain(BonusAlways).And.Not.Contain(Produce),
                "an unlock entry of an offer the file's contract lists name is skipped with it");
            Assert.That(read.Document.Run.Validate(), Is.EqualTo(RunStateResult.Ok));
            Assert.That(read.Document.Permanent.ItemCount(Boost), Is.EqualTo(Items(rig)), "nothing is granted by the drop");
            Assert.That(read.Document.Run.Cash, Is.EqualTo(rig.State.Cash));
            Assert.That(read.Document.Run.Rp, Is.EqualTo(rig.State.Rp));

            // The store hands the count to Core with the checkpoint.
            CheckpointDecodeResult<RunCheckpoint> decoded = new SaveV2CheckpointCodec(drifted).Deserialize(bytes);
            Assert.That(decoded.Succeeded, Is.True, decoded.Message);
            Assert.That(decoded.Checkpoint.DroppedContractEntries, Is.EqualTo(4));
        }

        [Test]
        public void TheRunWideTotalSurvivesTheDrop_SoNoItemIsGrantedAgain()
        {
            // A ladder with entries at run index 0 and 1: bonus-always completes at index 0, deliver at index 1.
            RunCommandFixtures.Options options = ContractFixtures.Options();
            options.Ladder = new ContractItemLadder(new[] { new ContractItemLadderEntry(0, Boost), new ContractItemLadderEntry(1, Boost) }, 10);
            RunCommandFixtures.Rig rig = Rig(options: options);
            SaveV3Fixtures.DeliverOffer(rig, BonusAlways, 3);
            SaveV3Fixtures.DeliverOffer(rig, Deliver, 5);
            Assert.That(Items(rig), Is.EqualTo(2), "both ladder entries granted");
            byte[] bytes = SaveV3Fixtures.Write(rig);

            // bonus-always is removed: its count (1) is dropped. A total derived from the remaining counts would be 1 and the next completion
            // would look up index 1 again; the persisted total keeps it at 2.
            RunCatalog drifted = SaveV3Fixtures.Without(rig.Catalog, BonusAlways);
            RunCommandFixtures.Rig loaded = SaveV3Fixtures.Load(drifted, bytes, out SaveV2Document doc);
            Assert.That(doc.DroppedContractEntries, Is.EqualTo(3), "count, cooldown and consumed bonus record of bonus-always");
            Assert.That(Board(loaded).RunCompletionTotal, Is.EqualTo(2));
            Assert.That(Items(loaded), Is.EqualTo(2));

            loaded.Advance((CooldownMs + 1_000) * RunSession.TicksPerMs);
            SaveV3Fixtures.DeliverOffer(loaded, Deliver, 5);
            Assert.That(Items(loaded), Is.EqualTo(2), "run index 2 has no ladder entry: the index-1 Item is not granted a second time");
            Assert.That(Board(loaded).RunCompletionTotal, Is.EqualTo(3));
        }

        [Test]
        public void AV2FileWithARemovedOffer_DropsIt_AndDerivesTheTotalFromTheCountsBeforeTheDrop()
        {
            SaveV2Fixture mid = SaveV2Scenarios.PlayedGame()[0];

            // contract.plate has one completion; the total stays the sum before the drop.
            SaveV2ReadResult plate = Read(SaveV3Fixtures.Without(mid.Catalog, SaveV2Scenarios.Plate), mid.Bytes);
            Assert.That(plate.IsSuccess, Is.True, plate.Code);
            Assert.That(plate.Document.SchemaVersion, Is.EqualTo(SaveV2Mapper.LegacySchemaVersion));
            Assert.That(plate.Document.DroppedContractEntries, Is.EqualTo(1));
            Assert.That(plate.Document.Run.Contracts.CompletionCounts(), Is.Empty);
            Assert.That(plate.Document.Run.Contracts.RunCompletionTotal, Is.EqualTo(1), "derived from the counts before the drop (save-v3.md)");
            Assert.That(plate.Document.Run.Contracts.Active.OfferId, Is.EqualTo(SaveV2Scenarios.TimedBar), "the other offer's contract stays");

            // contract.timed-bar is the active contract.
            SaveV2ReadResult timed = Read(SaveV3Fixtures.Without(mid.Catalog, SaveV2Scenarios.TimedBar), mid.Bytes);
            Assert.That(timed.IsSuccess, Is.True, timed.Code);
            Assert.That(timed.Document.DroppedContractEntries, Is.EqualTo(1));
            Assert.That(timed.Document.Run.Contracts.Active, Is.Null);
            Assert.That(timed.Document.Run.Contracts.CompletionCount(SaveV2Scenarios.Plate), Is.EqualTo(1));
        }

        [Test]
        public void ADroppedEntryStillObeysTheCatalogIndependentRules()
        {
            RunCommandFixtures.Rig rig = SaveV3Fixtures.AllMembers();
            byte[] bytes = SaveV3Fixtures.Write(rig);
            RunCatalog drifted = SaveV3Fixtures.Without(rig.Catalog, BonusAlways, Produce);
            long departure = rig.Session.TimelineUtcMs;

            // The dropped active contract: accepted after the departure (load rule 4) and produced above required.
            Assert.That(Read(drifted, SaveV3Fixtures.Rx(bytes, "\"acceptedUtcMs\":\\d+", "\"acceptedUtcMs\":" + (departure + 1))).Code, Is.EqualTo(SaveV2Codes.CrossField));
            Assert.That(Read(drifted, SaveV3Fixtures.Rx(bytes, "\"produced\":2\\}", "\"produced\":6}")).Code, Is.EqualTo(SaveV2Codes.Invariant));
            // Dropped list entries keep the order and uniqueness of their list.
            Assert.That(Read(drifted, SaveV3Fixtures.Rx(bytes, "(\"contractCooldowns\":\\[)(\\{[^}]*\\}),(\\{[^}]*\\})", "$1$3,$2")).Code, Is.EqualTo(SaveV2Codes.Invariant));
            Assert.That(Read(drifted, SaveV3Fixtures.Rx(bytes, "(\"bonusOffers\":\\[)(\\{[^}]*\\})", "$1$2,$2")).Code, Is.EqualTo(SaveV2Codes.Invariant));
            // The persisted total must cover the dropped counts too.
            Assert.That(Read(drifted, SaveV3Fixtures.Rx(bytes, "\"runCompletionTotal\":2,", "\"runCompletionTotal\":1,")).Code, Is.EqualTo(SaveV2Codes.Invariant));
        }

        // ---- objective and class changes ------------------------------------------------------------------------------------

        [Test]
        public void AnActiveContractWhoseOfferChangedObjective_IsDropped_WhenTheContentRevisionDiffers()
        {
            // Delivery -> production: the auto-deliver setting is the evidence.
            RunCommandFixtures.Rig auto = Rig();
            Accept(auto, Deliver);
            Assert.That(auto.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            byte[] autoBytes = SaveV3Fixtures.Write(auto);
            RunCatalog nowProduction = SaveV3Fixtures.Drift(auto.Catalog, o => o.Id == Deliver ? SaveV3Fixtures.DeliverAsProduction() : o, 1);
            SaveV2ReadResult read = Read(nowProduction, autoBytes);
            Assert.That(read.IsSuccess, Is.True, read.Code);
            Assert.That(read.Document.Run.Contracts.Active, Is.Null);
            Assert.That(read.Document.DroppedContractEntries, Is.EqualTo(1));
            Assert.That(read.Document.Run.IsUnlocked(Deliver), Is.True, "the offer itself still exists");

            // Production -> delivery: the production progress is the evidence.
            RunCommandFixtures.Rig produce = MiningRig(Produce);
            produce.Advance(3 * Second);
            Assert.That(Board(produce).Active.Produced, Is.EqualTo(2));
            byte[] produceBytes = SaveV3Fixtures.Write(produce);
            RunCatalog nowDelivery = SaveV3Fixtures.Drift(produce.Catalog, o => o.Id == Produce ? SaveV3Fixtures.ProduceAsDelivery() : o, 1);
            SaveV2ReadResult produced = Read(nowDelivery, produceBytes);
            Assert.That(produced.IsSuccess, Is.True, produced.Code);
            Assert.That(produced.Document.Run.Contracts.Active, Is.Null);
            Assert.That(produced.Document.DroppedContractEntries, Is.EqualTo(1));
        }

        [Test]
        public void ContradictingObjectiveEvidence_UnderTheSameContentRevision_StaysRejected()
        {
            // With the same revision the content cannot have changed: the file contradicts the rules (QA-11 v3-autodeliver-on-produce).
            RunCommandFixtures.Rig auto = Rig();
            Accept(auto, Deliver);
            Assert.That(auto.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            RunCatalog sameRevision = SaveV3Fixtures.Drift(auto.Catalog, o => o.Id == Deliver ? SaveV3Fixtures.DeliverAsProduction() : o, 0);
            Assert.That(Read(sameRevision, SaveV3Fixtures.Write(auto)).Code, Is.EqualTo(SaveV2Codes.Invariant));
        }

        [Test]
        public void AnObjectiveChangeWithoutEvidence_KeepsTheContract()
        {
            // Documented limit: a delivery contract with auto-deliver off and a production contract with nothing produced carry no objective
            // evidence, so the persisted contract is kept and continues under the new objective (its persisted quantity is authoritative).
            RunCommandFixtures.Rig rig = Rig();
            Accept(rig, Deliver);
            RunCatalog nowProduction = SaveV3Fixtures.Drift(rig.Catalog, o => o.Id == Deliver ? SaveV3Fixtures.DeliverAsProduction() : o, 1);
            SaveV2ReadResult read = Read(nowProduction, SaveV3Fixtures.Write(rig));
            Assert.That(read.IsSuccess, Is.True, read.Code);
            Assert.That(read.Document.Run.Contracts.Active.OfferId, Is.EqualTo(Deliver));
            Assert.That(read.Document.DroppedContractEntries, Is.Zero);
        }

        [Test]
        public void AnActiveOfferThatBecameABonusOffer_IsDroppedUnderANewRevision_AndRejectedUnderTheSameOne()
        {
            RunCommandFixtures.Rig rig = Rig();
            Accept(rig, Deliver);
            byte[] bytes = SaveV3Fixtures.Write(rig);

            SaveV2ReadResult drifted = Read(SaveV3Fixtures.Drift(rig.Catalog, o => o.Id == Deliver ? SaveV3Fixtures.DeliverAsBonus() : o, 1), bytes);
            Assert.That(drifted.IsSuccess, Is.True, drifted.Code);
            Assert.That(drifted.Document.Run.Contracts.Active, Is.Null, "no bonus record exists for it, so it cannot stay active");
            Assert.That(drifted.Document.DroppedContractEntries, Is.EqualTo(1));

            SaveV2ReadResult same = Read(SaveV3Fixtures.Drift(rig.Catalog, o => o.Id == Deliver ? SaveV3Fixtures.DeliverAsBonus() : o, 0), bytes);
            Assert.That(same.Code, Is.EqualTo(SaveV2Codes.Invariant));
        }

        // ---- everything else stays fail-closed -----------------------------------------------------------------------------

        [Test]
        public void AnUnknownMachine_StaysFailClosed_AsCheckpointContent()
        {
            RunCommandFixtures.Rig rig = SaveV3Fixtures.AllMembers();
            byte[] bytes = SaveV3Fixtures.Rx(SaveV3Fixtures.Write(rig), "\"machineId\":\"machine.miner\"", "\"machineId\":\"machine.removed\"");
            Assert.That(Read(rig.Catalog, bytes).Code, Is.EqualTo(SaveV2Codes.Unresolved));
            CheckpointDecodeResult<RunCheckpoint> decoded = new SaveV2CheckpointCodec(rig.Catalog).Deserialize(bytes);
            Assert.That(RunPersistenceReasons.From(decoded.Outcome), Is.EqualTo(RunPersistenceReasons.UnresolvedContent));
        }

        [Test]
        public void AnUnlockEntryOfARemovedOffer_ThatNoContractListNames_StaysFailClosed()
        {
            // bonus-never is unlocked at the first settle but never triggered, accepted or completed: the file cannot prove the ID was an offer.
            RunCommandFixtures.Rig rig = Rig();
            byte[] bytes = SaveV3Fixtures.Write(rig);
            Assert.That(rig.State.Unlocked, Does.Contain(BonusNever));
            Assert.That(Read(SaveV3Fixtures.Without(rig.Catalog, BonusNever), bytes).Code, Is.EqualTo(SaveV2Codes.Unresolved));
        }

        // ---- the load summary ------------------------------------------------------------------------------------------------

        [Test]
        public void TheColdStartReportsContractDropped_InItsSummary_OnceAndNotAfterTheNextWrite()
        {
            RunCommandFixtures.Rig rig = SaveV3Fixtures.AllMembers();
            long departure = rig.Session.TimelineUtcMs;
            var backend = new ScriptedBackend("v3-drift");
            var seed = new RunCheckpoint(new CheckpointId(SaveV2Scenarios.Uuid(1)), 0, departure, rig.Catalog.OfflinePolicy.Revision, null,
                rig.Catalog.ContentRevision, null, rig.State, rig.Session.Permanent);
            Assert.That(new JsonRunCheckpointStore("memory-root", rig.Catalog, backend).Promote(seed).Succeeded, Is.True);

            RunCatalog drifted = SaveV3Fixtures.Without(rig.Catalog, BonusAlways, Produce);
            RunPersistenceOutcome first = ColdStart(drifted, backend, departure + 60_000, 1);
            Assert.That(first.OfflineSummary, Is.Not.Null);
            Assert.That(first.OfflineSummary.DroppedContractEntries, Is.EqualTo(4));
            Assert.That(first.OfflineSummary.ContractDropCode, Is.EqualTo(RunPersistenceReasons.ContractDropped));
            Assert.That(RunPersistenceReasons.ContractDropped, Is.EqualTo("contract.dropped"));

            // The return wrote the kept state as V3; the next start finds nothing to drop.
            RunPersistenceOutcome second = ColdStart(drifted, backend, departure + 120_000, 100);
            Assert.That(second.OfflineSummary, Is.Not.Null);
            Assert.That(second.OfflineSummary.DroppedContractEntries, Is.Zero);
            Assert.That(second.OfflineSummary.ContractDropCode, Is.Null);
        }

        private static RunPersistenceOutcome ColdStart(RunCatalog catalog, ScriptedBackend backend, long clockMs, int firstId)
        {
            var state = new RunState(catalog, RunSimHarness.Id(9200 + firstId), 1, null, 25, 0, 1, 1, new[] { Ore });
            var session = new RunSession(catalog, state, new PermanentState(catalog), RunSimHarness.StartUtcMs);
            var store = new JsonRunCheckpointStore("memory-root", catalog, backend);
            var persistence = new RunPersistence(session, store, new FixedUtcClock(clockMs), new SequenceCheckpointIds(firstId),
                new OfflineRunReturnReconciler(catalog));
            RunCommandResult<RunPersistenceOutcome> result = persistence.ColdStart(null);
            Assert.That(result.IsSuccess, Is.True, result.ReasonCode);
            Assert.That(session.State.Contracts.BonusOffers().Select(b => b.OfferId), Does.Not.Contain(BonusAlways));
            return result.Value;
        }
    }
}
