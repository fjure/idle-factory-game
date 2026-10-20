using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using static IdleFactory.Tests.EditMode.Fixtures.Run.ContractFixtures;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// Save V3 (T334, issue #399, specs/005-economy-contracts/contracts/save-v3.md): the version-parametrized mapper, the V3 writer and the
    /// V3 members <c>activeContract.autoDeliver</c>, <c>activeContract.produced</c>, <c>contractCooldowns[]</c>, <c>bonusOffers[]</c> and
    /// <c>runCompletionTotal</c>, restored through the public <see cref="ContractBoard"/> restore surface. Covers the accepted follow-ups of
    /// #399: the persisted run-wide total under a dropped count (T320, SEC-8), cooldown and bonus persistence (T321), the production count and
    /// its bounds (T322) and the reload behaviour of a used or lapsed bonus and of production progress (T327). The broad fail-closed matrix,
    /// content drift, fault injection and forward compatibility are T335; the cases here pin the parser of each new member.
    /// </summary>
    public sealed class SaveV3MapperTests
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
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

        // ---- helpers ------------------------------------------------------------------------------------------------------

        private static byte[] Write(RunCommandFixtures.Rig rig)
        {
            SaveV2WriteResult written = SaveV2Mapper.Serialize(SaveV2Scenarios.Meta(rig.Catalog, 1, 0, rig.Session.TimelineUtcMs), rig.State, rig.Session.Permanent);
            Assert.That(written.IsSuccess, Is.True, written.Code);
            return written.Bytes;
        }

        /// <summary>The rig after a save and a load at its current instant: a new session over the loaded state, anchored at the departure.</summary>
        private static RunCommandFixtures.Rig Load(RunCommandFixtures.Rig rig, byte[] bytes)
        {
            SaveV2ReadResult read = SaveV2Mapper.Deserialize(bytes, rig.Catalog);
            Assert.That(read.IsSuccess, Is.True, read.Code);
            var loaded = new RunCommandFixtures.Rig { Catalog = rig.Catalog, State = read.Document.Run, NextId = rig.NextId };
            loaded.Session = new RunSession(rig.Catalog, read.Document.Run, read.Document.Permanent, read.Document.Meta.DepartureUtcMs);
            loaded.Commands = new RunCommands(loaded.Session, () => RunSimHarness.Id(loaded.NextId++));
            return loaded;
        }

        private static RunCommandFixtures.Rig Reload(RunCommandFixtures.Rig rig) => Load(rig, Write(rig));

        private static string Text(byte[] bytes) => Utf8.GetString(bytes);

        private static byte[] Bytes(string text) => new UTF8Encoding(false).GetBytes(text);

        /// <summary>The bytes with the first match of <paramref name="pattern"/> replaced (the match must exist).</summary>
        private static byte[] Rx(byte[] bytes, string pattern, string replacement)
        {
            var regex = new Regex(pattern, RegexOptions.CultureInvariant);
            string text = Text(bytes);
            Assert.That(regex.IsMatch(text), Is.True, "pattern not found: " + pattern);
            return Bytes(regex.Replace(text, replacement, 1));
        }

        /// <summary><see cref="ContractFixtures.Digest"/> without the transient lines (last outcome and serial, last expiry, session clock).</summary>
        private static string Durable(RunCommandFixtures.Rig rig) =>
            string.Join("\n", Digest(rig).Split('\n').Where(l => !l.StartsWith("outcome=", StringComparison.Ordinal) &&
                !l.StartsWith("expired=", StringComparison.Ordinal) && !l.StartsWith("now=", StringComparison.Ordinal)));

        private static void AssertBoardsEqual(ContractBoard expected, ContractBoard actual)
        {
            Assert.That(actual.CompletionCounts().Select(c => c.Id + ":" + c.Count), Is.EqualTo(expected.CompletionCounts().Select(c => c.Id + ":" + c.Count)));
            Assert.That(actual.RunCompletionTotal, Is.EqualTo(expected.RunCompletionTotal));
            Assert.That(actual.CooldownStamps().Select(c => c.OfferId + "@" + c.ReadyAtUtcMs), Is.EqualTo(expected.CooldownStamps().Select(c => c.OfferId + "@" + c.ReadyAtUtcMs)));
            Assert.That(actual.BonusOffers().Select(b => b.OfferId + "@" + b.WindowEndUtcMs + "/" + b.Consumed),
                Is.EqualTo(expected.BonusOffers().Select(b => b.OfferId + "@" + b.WindowEndUtcMs + "/" + b.Consumed)));
            Assert.That(actual.Active == null, Is.EqualTo(expected.Active == null));
            if (expected.Active == null) return;
            Assert.That(actual.Active.OfferId, Is.EqualTo(expected.Active.OfferId));
            Assert.That(actual.Active.AcceptedUtcMs, Is.EqualTo(expected.Active.AcceptedUtcMs));
            Assert.That(actual.Active.DeadlineUtcMs, Is.EqualTo(expected.Active.DeadlineUtcMs));
            Assert.That(actual.Active.RequiredUnits, Is.EqualTo(expected.Active.RequiredUnits));
            Assert.That(actual.Active.Produced, Is.EqualTo(expected.Active.Produced));
            Assert.That(actual.Active.AutoDeliver, Is.EqualTo(expected.Active.AutoDeliver));
        }

        private static void DeliverOffer(RunCommandFixtures.Rig rig, string offerId, long units)
        {
            Accept(rig, offerId);
            RunSimHarness.Stock(rig.State, Ore, units);
            RunCommandResult<ContractDeliveryResult> r = rig.Commands.DeliverContract();
            Assert.That(r.IsSuccess, Is.True, offerId + ": " + r.ReasonCode);
        }

        /// <summary>
        /// Every V3 member populated: a used bonus (bonus-always, consumed), a triggered bonus (bonus-ore, fired by the first Ore at 2 s), two
        /// cooldowns (bonus-always, deliver), a run-wide total of 2, and an active production contract with 2 of 5 units produced.
        /// </summary>
        private static RunCommandFixtures.Rig AllMembers()
        {
            RunCommandFixtures.Rig rig = MiningRig(null);
            DeliverOffer(rig, BonusAlways, 3);
            DeliverOffer(rig, Deliver, 5);
            Accept(rig, Produce);
            rig.Advance(3 * Second);
            ContractBoard b = Board(rig);
            Assert.That(b.Active.Produced, Is.EqualTo(2), "the Miner made 2 Ore at 2 s");
            Assert.That(b.RunCompletionTotal, Is.EqualTo(2));
            Assert.That(b.CooldownStamps().Length, Is.EqualTo(2));
            Assert.That(b.BonusOffers().Select(x => x.OfferId + "/" + x.Consumed), Is.EqualTo(new[] { BonusAlways + "/True", BonusOre + "/False" }));
            return rig;
        }

        // ---- the writer and the members -------------------------------------------------------------------------------------

        [Test]
        public void TheWriterAlwaysWritesV3_WithEveryMember_AndTheReaderRestoresTheBoardExactly()
        {
            RunCommandFixtures.Rig rig = AllMembers();
            byte[] bytes = Write(rig);
            string text = Text(bytes);
            Assert.That(text, Does.StartWith("{\"schemaVersion\":3,"));
            Assert.That(text, Does.Contain("\"autoDeliver\":false,\"produced\":2}"));
            Assert.That(text, Does.Contain("\"runCompletionTotal\":2,\"contractCooldowns\":[{\"offerId\":\"" + BonusAlways + "\",\"readyAtUtcMs\":"));
            Assert.That(text, Does.Contain("\"bonusOffers\":[{\"offerId\":\"" + BonusAlways + "\",\"state\":\"consumed\",\"windowEndUtcMs\":"));
            Assert.That(text, Does.Contain("{\"offerId\":\"" + BonusOre + "\",\"state\":\"triggered\",\"windowEndUtcMs\":" + (StartUtcMs + 2_000 + BonusWindowMs) + "}]"));

            SaveV2ReadResult read = SaveV2Mapper.Deserialize(bytes, rig.Catalog);
            Assert.That(read.IsSuccess, Is.True, read.Code);
            Assert.That(read.Document.SchemaVersion, Is.EqualTo(SaveV2Mapper.SchemaVersion));
            AssertBoardsEqual(Board(rig), read.Document.Run.Contracts);
            Assert.That(SaveV2Mapper.Serialize(read.Document.Meta, read.Document.Run, read.Document.Permanent).Bytes, Is.EqualTo(bytes), "canonical");
            Assert.That(read.Document.Run.Validate(), Is.EqualTo(RunStateResult.Ok));
        }

        [Test]
        public void TheCheckpointCodecWritesV3()
        {
            RunCommandFixtures.Rig rig = AllMembers();
            var checkpoint = new RunCheckpoint(new CheckpointId(SaveV2Scenarios.Uuid(1)), 0, rig.Session.TimelineUtcMs, rig.Catalog.OfflinePolicy.Revision, null,
                rig.Catalog.ContentRevision, null, rig.State, rig.Session.Permanent);
            var written = new SaveV2CheckpointCodec(rig.Catalog).Serialize(checkpoint);
            Assert.That(written.Succeeded, Is.True, written.Message);
            Assert.That(Text(written.Value), Does.StartWith("{\"schemaVersion\":3,"));
        }

        [Test]
        public void AutoDeliverOn_RoundTripsOnADeliveryContract_AndTheBlockedStateIsNotPersisted()
        {
            RunCommandFixtures.Rig rig = Rig();
            Accept(rig, Deliver);
            Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            byte[] bytes = Write(rig);
            Assert.That(Text(bytes), Does.Contain("\"autoDeliver\":true,\"produced\":0}"));
            Assert.That(Text(bytes), Does.Not.Contain("blocked").IgnoreCase, "the blocked flag is transient");

            RunCommandFixtures.Rig loaded = Load(rig, bytes);
            Assert.That(Board(loaded).Active.AutoDeliver, Is.True);
            Assert.That(Board(loaded).Active.AutoDeliverBlocked, Is.False);

            // Both continue identically: the stock arrives and the auto-delivery fires at the same instant.
            foreach (RunCommandFixtures.Rig r in new[] { rig, loaded })
            {
                RunSimHarness.Stock(r.State, Ore, 5);
                r.Advance(1 * Second);
                Assert.That(Board(r).CompletionCount(Deliver), Is.EqualTo(1));
            }
            Assert.That(Durable(loaded), Is.EqualTo(Durable(rig)));
        }

        [Test]
        public void AV2FileLoadsWithTheV2Defaults()
        {
            SaveV2Fixture mid = SaveV2Scenarios.All().Single(f => f.FileName == "02-mid-run-all-families.json");
            SaveV2ReadResult read = SaveV2Mapper.Deserialize(mid.Bytes, mid.Catalog);
            Assert.That(read.IsSuccess, Is.True, read.Code);
            Assert.That(read.Document.SchemaVersion, Is.EqualTo(SaveV2Mapper.LegacySchemaVersion));
            ContractBoard board = read.Document.Run.Contracts;
            Assert.That(board.Active, Is.Not.Null);
            Assert.That(board.Active.AutoDeliver, Is.False);
            Assert.That(board.Active.Produced, Is.Zero);
            Assert.That(board.CooldownStamps(), Is.Empty);
            Assert.That(board.BonusOffers(), Is.Empty);
            Assert.That(board.RunCompletionTotal, Is.EqualTo(board.CompletionCounts().Sum(c => c.Count)), "derived from the counts");
            Assert.That(board.RunCompletionTotal, Is.EqualTo(1));
        }

        [Test]
        public void TheV2WriterRefusesAStateThatOnlyV3CanCarry_InsteadOfDroppingIt()
        {
            RunCommandFixtures.Rig rig = AllMembers();
            SaveV2WriteResult v2 = SaveV2Mapper.Serialize(SaveV2Scenarios.Meta(rig.Catalog, 1, 0, rig.Session.TimelineUtcMs), rig.State,
                rig.Session.Permanent, SaveV2Mapper.LegacySchemaVersion);
            Assert.That(v2.IsSuccess, Is.False);
            Assert.That(v2.Code, Is.EqualTo(SaveV2Codes.Write));
            Assert.That(SaveV2Mapper.Serialize(SaveV2Scenarios.Meta(rig.Catalog, 1, 0, rig.Session.TimelineUtcMs), rig.State, rig.Session.Permanent, 4).Code,
                Is.EqualTo(SaveV2Codes.Write));
        }

        [Test]
        public void ValidateBytes_ComparesAgainstTheVersionItReads()
        {
            SaveV2Fixture mid = SaveV2Scenarios.All().Single(f => f.FileName == "02-mid-run-all-families.json");
            var codec = new SaveV2CheckpointCodec(mid.Catalog);
            RunCheckpoint checkpoint = RunCheckpointTestSupport.CheckpointOf(mid);
            Assert.That(codec.ValidateBytes(checkpoint, mid.Bytes), Is.True, "a V2 file is compared with the V2 serialization");
            Assert.That(codec.ValidateBytes(checkpoint, mid.V3Bytes), Is.True, "a V3 file with the V3 serialization");
            Assert.That(codec.ValidateBytes(checkpoint, SaveV2Scenarios.Mutate(mid.V3Bytes, "\"schemaVersion\":3", "\"schemaVersion\":4")), Is.False);
            Assert.That(codec.ValidateBytes(checkpoint, SaveV2Scenarios.Mutate(mid.V3Bytes, "\"schemaVersion\":3", "\"schemaVersion\":2")), Is.False);
            Assert.That(codec.ValidateBytes(checkpoint, Bytes("{}")), Is.False);
            Assert.That(codec.ValidateBytes(checkpoint, null), Is.False);
        }

        // ---- reload behaviour (T327 follow-ups) -----------------------------------------------------------------------------

        [Test]
        public void AReloadKeepsProductionProgress_AndTheRunContinuesLikeTheOriginal()
        {
            RunCommandFixtures.Rig rig = AllMembers();
            RunCommandFixtures.Rig loaded = Reload(rig);
            Assert.That(Board(loaded).Active.Produced, Is.EqualTo(2), "production progress is not reset");
            AssertBoardsEqual(Board(rig), Board(loaded));

            rig.Advance(40 * Second);
            loaded.Advance(40 * Second);
            Assert.That(Board(loaded).CompletionCount(Produce), Is.EqualTo(1), "completed at 6 s with the progress kept");
            Assert.That(Durable(loaded), Is.EqualTo(Durable(rig)));
        }

        [Test]
        public void AConsumedBonusCannotBeUsedTwiceAfterAReload_AndItsTriggerDoesNotFireAgain()
        {
            RunCommandFixtures.Rig rig = Rig();
            DeliverOffer(rig, BonusAlways, 3);
            BonusOfferState used = Board(rig).BonusState(BonusAlways);
            Assert.That(used.Consumed, Is.True);

            RunCommandFixtures.Rig loaded = Reload(rig);
            loaded.Advance(15 * Second); // the 10 s cooldown has passed; the window (90 s) is still open; the always-trigger still holds
            BonusOfferState after = Board(loaded).BonusState(BonusAlways);
            Assert.That(after.Triggered, Is.True);
            Assert.That(after.Consumed, Is.True, "the single use stays spent");
            Assert.That(after.WindowEndUtcMs, Is.EqualTo(used.WindowEndUtcMs), "no new window");
            Assert.That(loaded.Commands.AcceptContract(BonusAlways).ReasonCode, Is.EqualTo(RunReasons.ContractBonusUnavailable));
        }

        [Test]
        public void ALapsedTriggeredBonusLoadsAndNeverTriggersAgainAfterTheReload()
        {
            RunCommandFixtures.Rig rig = MiningRig(null);
            rig.Advance(3 * Second);
            long windowEnd = Board(rig).BonusState(BonusOre).WindowEndUtcMs;
            rig.Advance((BonusWindowMs + 1_000) * RunSession.TicksPerMs); // lapsed unaccepted; Ore is still being produced

            RunCommandFixtures.Rig loaded = Reload(rig);
            Assert.That(Board(loaded).BonusOffers().Single(b => b.OfferId == BonusOre).Consumed, Is.False, "persisted as triggered");
            loaded.Advance(10 * Second);
            BonusOfferState state = Board(loaded).BonusState(BonusOre);
            Assert.That(state.Triggered, Is.True);
            Assert.That(state.WindowEndUtcMs, Is.EqualTo(windowEnd), "the first window stays");
            Assert.That(loaded.Commands.AcceptContract(BonusOre).ReasonCode, Is.EqualTo(RunReasons.ContractBonusUnavailable));
        }

        [Test]
        public void ACooldownWhoseStampIsAtOrBeforeTheLoadInstantLoadsAsReady()
        {
            RunCommandFixtures.Rig rig = Rig();
            DeliverOffer(rig, Deliver, 5);
            long readyAt = Board(rig).CooldownStamps().Single(c => c.OfferId == Deliver).ReadyAtUtcMs;
            rig.Advance((CooldownMs + 5_000) * RunSession.TicksPerMs);
            Assert.That(rig.Session.TimelineUtcMs, Is.GreaterThan(readyAt));

            RunCommandFixtures.Rig loaded = Reload(rig);
            Assert.That(Board(loaded).CooldownStamps().Single(c => c.OfferId == Deliver).ReadyAtUtcMs, Is.EqualTo(readyAt), "kept as persisted");
            Assert.That(Board(loaded).CooldownRemainingMs(Deliver, loaded.Session.TimelineUtcMs), Is.Zero);
            Assert.That(loaded.Commands.AcceptContract(Deliver).IsSuccess, Is.True);

            // A stamp still in the future at the load keeps the offer cooling down.
            RunCommandFixtures.Rig early = Rig();
            DeliverOffer(early, Deliver, 5);
            RunCommandFixtures.Rig earlyLoaded = Reload(early);
            Assert.That(earlyLoaded.Commands.AcceptContract(Deliver).ReasonCode, Is.EqualTo(RunReasons.ContractCooldown));
        }

        [Test]
        public void ProducedEqualToRequired_IsValid_AndCompletesAtTheFirstSettleOfTheLoadedSession()
        {
            RunCommandFixtures.Rig rig = MiningRig(Produce);
            rig.Advance(3 * Second);
            Assert.That(Board(rig).Active.Produced, Is.EqualTo(2));
            long required = Board(rig).Active.RequiredUnits;
            byte[] full = SaveV2Scenarios.Mutate(Write(rig), "\"produced\":2}", "\"produced\":" + required + "}");

            RunCommandFixtures.Rig loaded = Load(rig, full);
            Assert.That(Board(loaded).Active.Produced, Is.EqualTo(required), "loaded as saved, not completed by the mapper");
            long departure = loaded.Session.TimelineUtcMs;
            loaded.Advance(0);
            Assert.That(Board(loaded).Active, Is.Null);
            Assert.That(Board(loaded).CompletionCount(Produce), Is.EqualTo(1));
            Assert.That(Board(loaded).LastOutcome.Kind, Is.EqualTo(ContractOutcomeKind.ProduceCompleted));
            Assert.That(Board(loaded).LastOutcome.EndedUtcMs, Is.EqualTo(departure));
        }

        // ---- the run-wide total (T320, SEC-8) -------------------------------------------------------------------------------

        [Test]
        public void ThePersistedRunCompletionTotalSurvivesADroppedCount_SoNoItemIsGrantedAgain()
        {
            RunCommandFixtures.Rig rig = Rig();
            DeliverOffer(rig, Deliver, 5);
            Assert.That(Items(rig), Is.EqualTo(1), "the ladder granted its index-0 Item");
            byte[] bytes = Write(rig);

            // The count of the offer is gone from the file (what the content-drift drop of T335 leaves), the persisted total is not.
            byte[] dropped = SaveV2Scenarios.Mutate(bytes, "\"completions\":[{\"offerId\":\"" + Deliver + "\",\"count\":1}]", "\"completions\":[]");
            RunCommandFixtures.Rig loaded = Load(rig, dropped);
            Assert.That(Board(loaded).CompletionCount(Deliver), Is.Zero);
            Assert.That(Board(loaded).RunCompletionTotal, Is.EqualTo(1), "max(saved 1, derived 0)");

            loaded.Advance((CooldownMs + 1_000) * RunSession.TicksPerMs);
            DeliverOffer(loaded, Deliver, 5);
            Assert.That(Items(loaded), Is.EqualTo(1), "run index 1 grants nothing: the index-0 Item is not granted twice");
            Assert.That(Board(loaded).RunCompletionTotal, Is.EqualTo(2));
        }

        [Test]
        public void ARunCompletionTotalAboveTheCountsLoads_AndOneBelowTheirSumIsRejected()
        {
            RunCommandFixtures.Rig rig = AllMembers();
            byte[] bytes = Write(rig);
            SaveV2ReadResult higher = SaveV2Mapper.Deserialize(SaveV2Scenarios.Mutate(bytes, "\"runCompletionTotal\":2,", "\"runCompletionTotal\":7,"), rig.Catalog);
            Assert.That(higher.IsSuccess, Is.True, higher.Code);
            Assert.That(higher.Document.Run.Contracts.RunCompletionTotal, Is.EqualTo(7));
            SaveV2ReadResult lower = SaveV2Mapper.Deserialize(SaveV2Scenarios.Mutate(bytes, "\"runCompletionTotal\":2,", "\"runCompletionTotal\":1,"), rig.Catalog);
            Assert.That(lower.Code, Is.EqualTo(SaveV2Codes.Invariant));
        }

        // ---- strictness of the new members (the full matrix is T335) ---------------------------------------------------------

        [Test]
        public void EachNewMemberIsStrict_AndAFailedReadPublishesNothing()
        {
            RunCommandFixtures.Rig rig = AllMembers();
            byte[] bytes = Write(rig);
            var cases = new (string Name, byte[] Bytes, string Code)[]
            {
                ("autoDeliver of the wrong type", Rx(bytes, "\"autoDeliver\":false", "\"autoDeliver\":0"), SaveV2Codes.Structure),
                ("autoDeliver as a string", Rx(bytes, "\"autoDeliver\":false", "\"autoDeliver\":\"false\""), SaveV2Codes.Structure),
                ("autoDeliver on a production objective", Rx(bytes, "\"autoDeliver\":false", "\"autoDeliver\":true"), SaveV2Codes.Invariant),
                ("produced above required", Rx(bytes, "\"produced\":2\\}", "\"produced\":6}"), SaveV2Codes.Invariant),
                ("produced negative", Rx(bytes, "\"produced\":2\\}", "\"produced\":-1}"), SaveV2Codes.Bounds),
                ("missing produced", Rx(bytes, ",\"produced\":2\\}", "}"), SaveV2Codes.Structure),
                ("unknown member on the active contract", Rx(bytes, "\"produced\":2\\}", "\"produced\":2,\"extra\":1}"), SaveV2Codes.Structure),
                ("duplicate cooldown offer", Rx(bytes, "(\"contractCooldowns\":\\[)(\\{[^}]*\\})", "$1$2,$2"), SaveV2Codes.Invariant),
                ("negative cooldown stamp", Rx(bytes, "\"readyAtUtcMs\":\\d+", "\"readyAtUtcMs\":-1"), SaveV2Codes.Bounds),
                ("cooldown stamp above 10^18", Rx(bytes, "\"readyAtUtcMs\":\\d+", "\"readyAtUtcMs\":1000000000000000001"), SaveV2Codes.Bounds),
                ("consumed bonus without windowEndUtcMs", Rx(bytes, "\"state\":\"consumed\",\"windowEndUtcMs\":\\d+", "\"state\":\"consumed\""), SaveV2Codes.Structure),
                ("triggered bonus without windowEndUtcMs", Rx(bytes, "\"state\":\"triggered\",\"windowEndUtcMs\":\\d+", "\"state\":\"triggered\""), SaveV2Codes.Structure),
                ("unknown bonus state", Rx(bytes, "\"state\":\"consumed\"", "\"state\":\"used\""), SaveV2Codes.Structure),
                ("bonus state of a standing offer", Rx(bytes, "\"offerId\":\"" + Regex.Escape(BonusOre) + "\",\"state\"", "\"offerId\":\"" + Produce + "\",\"state\""), SaveV2Codes.Invariant),
                ("duplicate bonus offer", Rx(bytes, "(\"bonusOffers\":\\[)(\\{[^}]*\\})", "$1$2,$2"), SaveV2Codes.Invariant),
                ("missing runCompletionTotal", Rx(bytes, "\"runCompletionTotal\":2,", string.Empty), SaveV2Codes.Structure),
                ("V3 members under schemaVersion 2", Rx(bytes, "\"schemaVersion\":3", "\"schemaVersion\":2"), SaveV2Codes.Structure),
                ("schemaVersion 4", Rx(bytes, "\"schemaVersion\":3", "\"schemaVersion\":4"), SaveV2Codes.Version),
            };

            var failures = new System.Collections.Generic.List<string>();
            foreach (var c in cases)
            {
                SaveV2ReadResult result = SaveV2Mapper.Deserialize(c.Bytes, rig.Catalog);
                if (result.IsSuccess) failures.Add(c.Name + ": accepted, expected " + c.Code);
                else if (result.Code != c.Code) failures.Add(c.Name + ": " + result.Code + ", expected " + c.Code);
                else if (result.Document != null) failures.Add(c.Name + ": a rejected read returned a document");
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void AV3MemberInAV2FileIsRejected()
        {
            SaveV2Fixture mid = SaveV2Scenarios.All().Single(f => f.FileName == "02-mid-run-all-families.json");
            Assert.That(SaveV2Mapper.Deserialize(SaveV2Scenarios.Mutate(mid.Bytes, "\"overdrive\":{", "\"runCompletionTotal\":1,\"overdrive\":{"), mid.Catalog).Code,
                Is.EqualTo(SaveV2Codes.Structure));
            Assert.That(SaveV2Mapper.Deserialize(SaveV2Scenarios.Mutate(mid.Bytes, "\"requiredUnits\":", "\"produced\":0,\"requiredUnits\":"), mid.Catalog).Code,
                Is.EqualTo(SaveV2Codes.Structure));
        }

        [Test]
        public void TheSchemaVersionIsPeekedWhereverItStands()
        {
            RunCommandFixtures.Rig rig = AllMembers();
            string text = Text(Write(rig));
            Assert.That(text, Does.StartWith("{\"schemaVersion\":3,"));
            string moved = "{" + text.Substring("{\"schemaVersion\":3,".Length, text.Length - "{\"schemaVersion\":3,".Length - 1) + ",\"schemaVersion\":3}";
            SaveV2ReadResult read = SaveV2Mapper.Deserialize(Bytes(moved), rig.Catalog);
            Assert.That(read.IsSuccess, Is.True, read.Code);
            Assert.That(read.Document.SchemaVersion, Is.EqualTo(3));
            Assert.That(SaveV2Mapper.Deserialize(Bytes(moved.Replace(",\"schemaVersion\":3}", "}")), rig.Catalog).Code, Is.EqualTo(SaveV2Codes.Structure));
            Assert.That(SaveV2Mapper.Deserialize(Bytes(moved.Replace(",\"schemaVersion\":3}", ",\"schemaVersion\":3,\"schemaVersion\":3}")), rig.Catalog).Code,
                Is.EqualTo(SaveV2Codes.Structure), "a duplicate stays a structure error");
        }
    }
}
