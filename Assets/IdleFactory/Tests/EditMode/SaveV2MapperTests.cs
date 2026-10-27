using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The Save V2 codec (T104, contracts/save-v2-and-v1-import.md, FR-027, SC-002, SEC-2, SEC-3, SEC-5): the round trip of
    /// every fixture scenario, every rejection class with its static code, the envelope limits, each cross-field bound of
    /// load rule 4, static-code diagnostics and the documented save compatibility.
    /// </summary>
    public sealed class SaveV2MapperTests
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        private static List<SaveV2Fixture> _all;
        private static SaveV2Fixture _fresh;
        private static SaveV2Fixture _mid;
        private static string _midText;
        private static bool _originalLogToUnity;
        private bool _logToUnity;

        [OneTimeTearDown]
        public void RestoreLogging()
        {
            CheckpointDiagnostics.LogToUnity = _originalLogToUnity;
        }

        [OneTimeSetUp]
        public void BuildScenarios()
        {
            _originalLogToUnity = CheckpointDiagnostics.LogToUnity;
            CheckpointDiagnostics.LogToUnity = false;
            _all = SaveV2Scenarios.All();
            _fresh = _all.Single(f => f.FileName == "01-fresh-run.json");
            _mid = _all.Single(f => f.FileName == "02-mid-run-all-families.json");
            _midText = Utf8.GetString(_mid.Bytes);
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

        private static byte[] Bytes(string text) => new UTF8Encoding(false).GetBytes(text);

        private static SaveV2ReadResult ReadMid(byte[] bytes) => SaveV2Mapper.Deserialize(bytes, _mid.Catalog);

        /// <summary>The mid-run text with the first match of <paramref name="pattern"/> replaced (the match must exist).</summary>
        private static byte[] Rx(string pattern, string replacement)
        {
            var regex = new Regex(pattern, RegexOptions.Singleline | RegexOptions.CultureInvariant);
            Assert.That(regex.IsMatch(_midText), Is.True, "pattern not found: " + pattern);
            return Bytes(regex.Replace(_midText, replacement, 1));
        }

        private static byte[] Swap(string from, string to) => SaveV2Scenarios.Mutate(_mid.Bytes, from, to);

        private static void AssertRejected(string name, byte[] bytes, RunCatalog catalog, string code, List<string> failures)
        {
            SaveV2ReadResult result = SaveV2Mapper.Deserialize(bytes, catalog);
            if (result.IsSuccess) failures.Add(name + ": accepted, expected " + code);
            else if (result.Code != code) failures.Add(name + ": " + result.Code + ", expected " + code);
            else if (result.Message != SaveV2Codes.Message(code)) failures.Add(name + ": message is not the static copy");
        }

        // ------------------------------------------------------------------------------------------------------
        // Round trip
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void RoundTrip_EveryValidFixtureScenario_RestoresTheCompleteState_AndTheBytesAreCanonical()
        {
            foreach (SaveV2Fixture f in _all.Where(x => !x.IsCorrupt))
            {
                SaveV2ReadResult read = SaveV2Mapper.Deserialize(f.Bytes, f.Catalog);
                Assert.That(read.IsSuccess, Is.True, f.FileName + " " + read.Code);
                Assert.That(read.Code, Is.Null);
                Assert.That(SaveV2Scenarios.Dump(read.Document.Run, read.Document.Permanent), Is.EqualTo(SaveV2Scenarios.Dump(f.Run, f.Permanent)), f.FileName);
                SaveV2Meta m = read.Document.Meta;
                Assert.That(m.CheckpointId, Is.EqualTo(f.Meta.CheckpointId), f.FileName);
                Assert.That(m.Generation, Is.EqualTo(f.Meta.Generation), f.FileName);
                Assert.That(m.DepartureUtcMs, Is.EqualTo(f.Meta.DepartureUtcMs), f.FileName);
                Assert.That(m.OfflinePolicyRevision, Is.EqualTo(f.Meta.OfflinePolicyRevision), f.FileName);
                Assert.That(m.ReconciledFromCheckpointId, Is.EqualTo(f.Meta.ReconciledFromCheckpointId), f.FileName);
                Assert.That(m.ContentRevision, Is.EqualTo(f.Meta.ContentRevision), f.FileName);
                Assert.That(m.Import, Is.Null, f.FileName);
                SaveV2WriteResult again = SaveV2Mapper.Serialize(m, read.Document.Run, read.Document.Permanent, SaveV2Mapper.LegacySchemaVersion);
                Assert.That(again.Bytes, Is.EqualTo(f.Bytes), f.FileName);
                // Save V3 (T334): the writer writes V3, which is canonical too (a fixed point of read and write).
                SaveV2WriteResult v3 = SaveV2Mapper.Serialize(m, read.Document.Run, read.Document.Permanent);
                Assert.That(v3.Bytes, Is.EqualTo(f.V3Bytes), f.FileName);
                SaveV2ReadResult v3Read = SaveV2Mapper.Deserialize(v3.Bytes, f.Catalog);
                Assert.That(v3Read.IsSuccess, Is.True, f.FileName + " " + v3Read.Code);
                Assert.That(SaveV2Mapper.Serialize(v3Read.Document.Meta, v3Read.Document.Run, v3Read.Document.Permanent).Bytes, Is.EqualTo(v3.Bytes), f.FileName);
            }
        }

        [Test]
        public void RoundTrip_MidRun_KeepsMachinesProgressEffectContractOverdriveAndPermanentState()
        {
            SaveV2Document d = ReadMid(_mid.Bytes).Document;
            Assert.That(d.Run.Machines.Count, Is.EqualTo(2));
            Assert.That(d.Run.Machines.Any(m => m.ProgressTicks > 0 && m.ProgressTicks < m.CycleDurationTicks), Is.True, "a mid-cycle machine");
            Assert.That(d.Run.Effects.Count, Is.EqualTo(1));
            Assert.That(d.Run.Contracts.Active.OfferId, Is.EqualTo(SaveV2Scenarios.TimedBar));
            Assert.That(d.Run.Contracts.CompletionCount(SaveV2Scenarios.Plate), Is.EqualTo(1));
            Assert.That(d.Run.Overdrive.ActiveUntilUtcMs.HasValue, Is.True);
            Assert.That(d.Run.ResearchLevel(SaveV2Scenarios.Speed), Is.EqualTo(1));
            Assert.That(d.Run.StorageLevel, Is.EqualTo(2));
            Assert.That(d.Run.IsAutoSell("iron.ore"), Is.True);
            Assert.That(d.Permanent.ItemCount(SaveV2Scenarios.Boost), Is.EqualTo(2), "one of three Items was consumed by the activation");
            Assert.That(d.Permanent.Ip, Is.EqualTo(7));
            Assert.That(d.Permanent.IndustryLevel(SaveV2Scenarios.IndustryCash), Is.EqualTo(2));
            Assert.That(d.Meta.ReconciledFromCheckpointId, Is.EqualTo(SaveV2Scenarios.Uuid(1)));
        }

        [Test]
        public void RoundTrip_PendingPrestigeCandidate_IsARunAndPermanentPair()
        {
            SaveV2Fixture pending = _all.Single(f => f.FileName == "03-pending-prestige.json");
            SaveV2Document d = SaveV2Mapper.Deserialize(pending.Bytes, pending.Catalog).Document;
            Assert.That(d.Run.RunNumber, Is.EqualTo(2));
            Assert.That(d.Run.PreviousRunId, Is.EqualTo(_mid.Run.RunId));
            Assert.That(d.Permanent.PrestigeCount, Is.EqualTo(1));
            Assert.That(d.Permanent.Ip, Is.GreaterThan(7), "the award is in the candidate");
            Assert.That(d.Run.Machines.Count, Is.EqualTo(0));
            Assert.That(d.Run.Effects.Count, Is.EqualTo(0));
            Assert.That(d.Run.Contracts.Active, Is.Null);
        }

        [Test]
        public void RoundTrip_ReservedOverdriveUnlockId_IsAKnownUnlock()
        {
            Assert.That(_fresh.Run.Unlocked, Does.Contain(RunState.OverdriveUnlockId));
            SaveV2Document d = SaveV2Mapper.Deserialize(_fresh.Bytes, _fresh.Catalog).Document;
            Assert.That(d.Run.IsUnlocked(RunState.OverdriveUnlockId), Is.True);
            Assert.That(Utf8.GetString(_fresh.Bytes), Does.Contain("\"overdrive\","));
            var bad = SaveV2Scenarios.Mutate(_fresh.Bytes, "\"overdrive\",", "\"overdrivf\",");
            Assert.That(SaveV2Mapper.Deserialize(bad, _fresh.Catalog).Code, Is.EqualTo(SaveV2Codes.Unresolved));
        }

        [Test]
        public void RoundTrip_UnlockedList_KeepsUnlockOrder_AndAStateBeforeItsFirstSettleIsNotChanged()
        {
            RunCatalog catalog = RunCommandFixtures.Catalog(SaveV2Scenarios.Options());
            // Not sorted (unlock order) and not a fixed point: iron.bar and the other machines are 'always' but not yet unlocked.
            var run = new RunState(catalog, RunSimHarness.Id(1), 1, null, 25, 0, 1, 1, new[] { "machine.miner", "overdrive", "iron.ore" });
            var permanent = new PermanentState(catalog);
            SaveV2Meta meta = SaveV2Scenarios.Meta(catalog, 1, 0, RunSimHarness.StartUtcMs);
            SaveV2WriteResult written = SaveV2Mapper.Serialize(meta, run, permanent);
            Assert.That(written.IsSuccess, Is.True, written.Code);
            Assert.That(Utf8.GetString(written.Bytes), Does.Contain("\"unlocked\":[\"machine.miner\",\"overdrive\",\"iron.ore\"]"));
            SaveV2Document d = SaveV2Mapper.Deserialize(written.Bytes, catalog).Document;
            Assert.That(d.Run.Unlocked, Is.EqualTo(new[] { "machine.miner", "overdrive", "iron.ore" }), "unlock order is kept");
            Assert.That(d.Run.IsUnlocked("iron.bar"), Is.False, "the mapper settles nothing; the first settle of the session unlocks it");
        }

        [Test]
        public void RoundTrip_ImportBlockAndReconciledLink()
        {
            string legacyId = SaveV2Scenarios.Uuid(77).ToString("D");
            var import = new SaveV2Import(1, legacyId, 41);
            SaveV2Meta meta = SaveV2Scenarios.Meta(_fresh.Catalog, 5, 3, _fresh.Meta.DepartureUtcMs, SaveV2Scenarios.Uuid(4), import);
            SaveV2WriteResult written = SaveV2Mapper.Serialize(meta, _fresh.Run, _fresh.Permanent);
            Assert.That(written.IsSuccess, Is.True, written.Code);
            SaveV2Meta read = SaveV2Mapper.Deserialize(written.Bytes, _fresh.Catalog).Document.Meta;
            Assert.That(read.Import.SourceSchemaVersion, Is.EqualTo(1));
            Assert.That(read.Import.SourceCheckpointId, Is.EqualTo(legacyId));
            Assert.That(read.Import.SourceGeneration, Is.EqualTo(41));
            Assert.That(read.ReconciledFromCheckpointId, Is.EqualTo(SaveV2Scenarios.Uuid(4)));

            // The source checkpoint ID is a canonical lowercase hyphenated UUID like the others.
            foreach (string bad in new[] { "legacy-checkpoint-01", legacyId.ToUpperInvariant().Replace("2", "A"), legacyId.Replace("-", string.Empty) })
            {
                SaveV2WriteResult refused = SaveV2Mapper.Serialize(
                    SaveV2Scenarios.Meta(_fresh.Catalog, 5, 3, _fresh.Meta.DepartureUtcMs, null, new SaveV2Import(1, bad, 41)), _fresh.Run, _fresh.Permanent);
                Assert.That(refused.Code, Is.EqualTo(SaveV2Codes.Structure), bad);
                Assert.That(SaveV2Mapper.Deserialize(SaveV2Scenarios.Mutate(written.Bytes, legacyId, bad), _fresh.Catalog).Code, Is.EqualTo(SaveV2Codes.Structure), bad);
            }

            byte[] wrongVersion = SaveV2Scenarios.Mutate(written.Bytes, "\"sourceSchemaVersion\":1", "\"sourceSchemaVersion\":2");
            Assert.That(SaveV2Mapper.Deserialize(wrongVersion, _fresh.Catalog).Code, Is.EqualTo(SaveV2Codes.Invariant));
        }

        [Test]
        public void RoundTrip_UntimedActiveContract_AndCompletionCounts()
        {
            RunCatalog catalog = RunCommandFixtures.Catalog(SaveV2Scenarios.Options());
            RunState run = RunSimHarness.NewState(catalog);
            Assert.That(run.Contracts.TrySetCompletionCount(SaveV2Scenarios.Plate, 1), Is.EqualTo(RunStateResult.Ok));
            ContractOfferDef plate = catalog.ContractOffers[SaveV2Scenarios.Plate];
            Assert.That(run.Contracts.TrySetActive(new ActiveContract(SaveV2Scenarios.Plate, RunSimHarness.StartUtcMs, null, plate.QuantityTable[1])),
                Is.EqualTo(RunStateResult.Ok));
            var permanent = new PermanentState(catalog);
            SaveV2WriteResult written = SaveV2Mapper.Serialize(SaveV2Scenarios.Meta(catalog, 1, 0, RunSimHarness.StartUtcMs), run, permanent);
            Assert.That(written.IsSuccess, Is.True, written.Code);
            Assert.That(Utf8.GetString(written.Bytes), Does.Contain("\"deadlineUtcMs\":null"));
            SaveV2Document d = SaveV2Mapper.Deserialize(written.Bytes, catalog).Document;
            Assert.That(d.Run.Contracts.Active.DeadlineUtcMs, Is.Null);
            Assert.That(d.Run.Contracts.Active.RequiredUnits, Is.EqualTo(plate.QuantityTable[1]));
            Assert.That(d.Run.Contracts.CompletionCount(SaveV2Scenarios.Plate), Is.EqualTo(1));
        }

        [Test]
        public void RoundTrip_AnActiveContractLoadsAfterTheQuantityCurveChanged()
        {
            // requiredUnits is fixed at acceptance and authoritative; retuning the offer's curve stays save-compatible (load rule 2).
            long saved = _mid.Run.Contracts.Active.RequiredUnits;
            RunCommandFixtures.Options retuned = SaveV2Scenarios.Options();
            retuned.Offers = new[]
            {
                retuned.Offers[0],
                new ContractOfferDef(SaveV2Scenarios.TimedBar, "timedBar", "iron.bar", new Curve(saved + 500, 1_010_000, "ceil"), 60_000,
                    new[] { new ContractRewardDef("cash", new Curve(200, 1_000_000, "floor")) }, new UnlockCondition("always")),
            };
            RunCatalog catalog = RunCommandFixtures.Catalog(retuned);
            Assert.That(catalog.ContractOffers[SaveV2Scenarios.TimedBar].QuantityTable[0], Is.Not.EqualTo(saved));
            SaveV2ReadResult read = SaveV2Mapper.Deserialize(_mid.Bytes, catalog);
            Assert.That(read.IsSuccess, Is.True, read.Code);
            Assert.That(read.Document.Run.Contracts.Active.RequiredUnits, Is.EqualTo(saved), "the persisted value stays authoritative");
            Assert.That(ReadMid(Swap("\"requiredUnits\":" + saved, "\"requiredUnits\":" + (saved + 1))).IsSuccess, Is.True);
            Assert.That(ReadMid(Swap("\"requiredUnits\":" + saved, "\"requiredUnits\":0")).Code, Is.EqualTo(SaveV2Codes.Bounds));
            Assert.That(ReadMid(Swap("\"requiredUnits\":" + saved, "\"requiredUnits\":1000000000000000001")).Code, Is.EqualTo(SaveV2Codes.Bounds));
        }

        [Test]
        public void RoundTrip_ALoadedRunContinuesLikeTheOriginal_SC002()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(SaveV2Scenarios.Options(), 5_000_000);
            rig.Advance(0);
            RunCommands c = SaveV2Scenarios.NewCommands(rig);
            Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            Assert.That(rig.Build(1, RunCommandFixtures.Smelter).IsSuccess, Is.True);
            Assert.That(c.SetAutoSell("iron.bar", true).IsSuccess, Is.True);
            Assert.That(c.ActivateOverdrive().IsSuccess, Is.True);
            rig.Advance(30_000_000);
            long departure = rig.Session.TimelineUtcMs;
            SaveV2WriteResult written = SaveV2Mapper.Serialize(SaveV2Scenarios.Meta(rig.Catalog, 1, 0, departure), rig.State, rig.Session.Permanent);
            Assert.That(written.IsSuccess, Is.True, written.Code);
            SaveV2Document loaded = SaveV2Mapper.Deserialize(written.Bytes, rig.Catalog).Document;
            var session = new RunSession(rig.Catalog, loaded.Run, loaded.Permanent, departure);

            rig.Advance(600_000_000);
            Assert.That(session.Advance(600_000_000).IsSuccess, Is.True);
            Assert.That(SaveV2Scenarios.Dump(loaded.Run, loaded.Permanent), Is.EqualTo(SaveV2Scenarios.Dump(rig.State, rig.Session.Permanent)));
        }

        // ------------------------------------------------------------------------------------------------------
        // Envelope: encoding, size, depth, integers
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void Envelope_RejectsEveryLexicalAndStructuralClass_WithItsStaticCode()
        {
            var f = new List<string>();
            RunCatalog cat = _mid.Catalog;
            byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(_mid.Bytes).ToArray();
            AssertRejected("BOM", bom, cat, SaveV2Codes.Encoding, f);
            AssertRejected("invalid UTF-8", _mid.Bytes.Concat(new byte[] { 0xFF }).ToArray(), cat, SaveV2Codes.Encoding, f);
            AssertRejected("overlong UTF-8", new byte[] { 0xC0, 0xAF }, cat, SaveV2Codes.Encoding, f);
            AssertRejected("null", null, cat, SaveV2Codes.Size, f);
            AssertRejected("empty", new byte[0], cat, SaveV2Codes.Size, f);
            AssertRejected("1 MiB + 1", Enumerable.Repeat((byte)' ', SaveV2Mapper.MaximumByteCount + 1).ToArray(), cat, SaveV2Codes.Size, f);
            AssertRejected("null catalog", _mid.Bytes, null, SaveV2Codes.Invariant, f);

            AssertRejected("depth 17", Bytes(new string('[', 17) + new string(']', 17)), cat, SaveV2Codes.Limit, f);
            AssertRejected("depth 17 objects", Bytes(string.Concat(Enumerable.Repeat("{\"a\":", 17)) + "1" + new string('}', 17)), cat, SaveV2Codes.Limit, f);
            AssertRejected("depth 16 is within the limit", Bytes(new string('[', 16) + new string(']', 16)), cat, SaveV2Codes.Structure, f);

            AssertRejected("float 1.0", Swap("\"generation\":7", "\"generation\":1.0"), cat, SaveV2Codes.Structure, f);
            AssertRejected("exponent 1e3", Swap("\"generation\":7", "\"generation\":1e3"), cat, SaveV2Codes.Structure, f);
            AssertRejected("exponent 1E3", Swap("\"generation\":7", "\"generation\":1E3"), cat, SaveV2Codes.Structure, f);
            AssertRejected("fraction 7.5", Swap("\"generation\":7", "\"generation\":7.5"), cat, SaveV2Codes.Structure, f);
            AssertRejected("leading zero", Swap("\"generation\":7", "\"generation\":07"), cat, SaveV2Codes.Structure, f);
            AssertRejected("negative zero", Swap("\"generation\":7", "\"generation\":-0"), cat, SaveV2Codes.Structure, f);
            AssertRejected("plus sign", Swap("\"generation\":7", "\"generation\":+7"), cat, SaveV2Codes.Structure, f);
            AssertRejected("NaN", Swap("\"generation\":7", "\"generation\":NaN"), cat, SaveV2Codes.Structure, f);
            AssertRejected("hex", Swap("\"generation\":7", "\"generation\":0x7"), cat, SaveV2Codes.Structure, f);
            AssertRejected("string for integer", Swap("\"generation\":7", "\"generation\":\"7\""), cat, SaveV2Codes.Structure, f);
            AssertRejected("bool for integer", Swap("\"generation\":7", "\"generation\":true"), cat, SaveV2Codes.Structure, f);
            AssertRejected("null for integer", Swap("\"generation\":7", "\"generation\":null"), cat, SaveV2Codes.Structure, f);
            AssertRejected("integer for bool", Rx("\"isActive\":true", "\"isActive\":1"), cat, SaveV2Codes.Structure, f);
            AssertRejected("truncated literal", Swap("\"previousRunId\":null", "\"previousRunId\":nul"), cat, SaveV2Codes.Structure, f);
            AssertRejected("array for object", Swap("\"overdrive\":{", "\"overdrive\":[{"), cat, SaveV2Codes.Structure, f);

            AssertRejected("integer 10^18 + 1", Rx("\"cash\":\\d+", "\"cash\":1000000000000000001"), cat, SaveV2Codes.Bounds, f);
            AssertRejected("integer above long", Rx("\"cash\":\\d+", "\"cash\":9223372036854775808"), cat, SaveV2Codes.Bounds, f);
            AssertRejected("integer 25 digits", Rx("\"cash\":\\d+", "\"cash\":1234567890123456789012345"), cat, SaveV2Codes.Bounds, f);
            AssertRejected("negative cash", Rx("\"cash\":\\d+", "\"cash\":-1"), cat, SaveV2Codes.Bounds, f);
            AssertRejected("negative departure", Swap("\"departureUtcMs\":" + _mid.Meta.DepartureUtcMs, "\"departureUtcMs\":-5"), cat, SaveV2Codes.Bounds, f);
            AssertRejected("zero cycleDurationTicks", Rx("\"cycleDurationTicks\":\\d+", "\"cycleDurationTicks\":0"), cat, SaveV2Codes.Bounds, f);
            AssertRejected("zero inventory units", Rx("\"units\":\\d+", "\"units\":0"), cat, SaveV2Codes.Bounds, f);
            AssertRejected("run number 0", Swap("\"runNumber\":1", "\"runNumber\":0"), cat, SaveV2Codes.Bounds, f);

            AssertRejected("duplicate top-level member", Swap("\"generation\":7,", "\"generation\":7,\"generation\":7,"), cat, SaveV2Codes.Structure, f);
            AssertRejected("duplicate member with a different value", Swap("\"generation\":7,", "\"generation\":7,\"generation\":8,"), cat, SaveV2Codes.Structure, f);
            AssertRejected("duplicate nested member", Swap("\"ip\":7,", "\"ip\":7,\"ip\":7,"), cat, SaveV2Codes.Structure, f);
            AssertRejected("unknown member", Swap("\"generation\":7,", "\"generation\":7,\"extra\":1,"), cat, SaveV2Codes.Structure, f);
            AssertRejected("unknown nested member", Swap("\"ip\":7,", "\"ip\":7,\"extra\":1,"), cat, SaveV2Codes.Structure, f);
            AssertRejected("case-different member", Swap("\"generation\":7", "\"Generation\":7"), cat, SaveV2Codes.Structure, f);
            AssertRejected("missing member", Swap("\"contentRevision\":" + _mid.Catalog.ContentRevision + ",", string.Empty), cat, SaveV2Codes.Structure, f);
            AssertRejected("missing nested member", Swap("\"ip\":7,", string.Empty), cat, SaveV2Codes.Structure, f);
            AssertRejected("missing optional member (null is explicit)", Swap("\"previousRunId\":null,", string.Empty), cat, SaveV2Codes.Structure, f);
            AssertRejected("schemaVersion 4", Swap("\"schemaVersion\":2", "\"schemaVersion\":4"), cat, SaveV2Codes.Version, f);
            AssertRejected("schemaVersion 3 on a V2 body (the V3 members are missing)", Swap("\"schemaVersion\":2", "\"schemaVersion\":3"), cat, SaveV2Codes.Structure, f);
            AssertRejected("schemaVersion 1", Swap("\"schemaVersion\":2", "\"schemaVersion\":1"), cat, SaveV2Codes.Version, f);
            AssertRejected("missing schemaVersion", Swap("\"schemaVersion\":2,", string.Empty), cat, SaveV2Codes.Structure, f);

            AssertRejected("root array", Bytes("[]"), cat, SaveV2Codes.Structure, f);
            AssertRejected("root null", Bytes("null"), cat, SaveV2Codes.Structure, f);
            AssertRejected("root number", Bytes("123"), cat, SaveV2Codes.Structure, f);
            AssertRejected("empty object", Bytes("{}"), cat, SaveV2Codes.Structure, f);
            AssertRejected("trailing content", _mid.Bytes.Concat(Bytes("{}")).ToArray(), cat, SaveV2Codes.Structure, f);
            AssertRejected("trailing garbage", _mid.Bytes.Concat(Bytes("x")).ToArray(), cat, SaveV2Codes.Structure, f);
            AssertRejected("truncated", _mid.Bytes.Take(_mid.Bytes.Length / 2).ToArray(), cat, SaveV2Codes.Structure, f);
            AssertRejected("trailing comma", Bytes(_midText.Substring(0, _midText.Length - 1) + ",}"), cat, SaveV2Codes.Structure, f);
            AssertRejected("leading comma", Swap("{\"schemaVersion\"", "{,\"schemaVersion\""), cat, SaveV2Codes.Structure, f);
            AssertRejected("comment", Bytes("/*x*/" + _midText), cat, SaveV2Codes.Structure, f);
            AssertRejected("line comment", Bytes("//x\n" + _midText), cat, SaveV2Codes.Structure, f);
            AssertRejected("single quotes", Swap("\"schemaVersion\"", "'schemaVersion'"), cat, SaveV2Codes.Structure, f);
            AssertRejected("unquoted member", Swap("\"schemaVersion\"", "schemaVersion"), cat, SaveV2Codes.Structure, f);
            AssertRejected("escape sequence", Swap("\"schemaVersion\"", "\"schema\\u0056ersion\""), cat, SaveV2Codes.Structure, f);
            AssertRejected("raw control character in a string", Swap("\"schemaVersion\"", "\"schema\tVersion\""), cat, SaveV2Codes.Structure, f);
            AssertRejected("non-JSON whitespace", Swap("\"schemaVersion\"", "\u00A0\"schemaVersion\""), cat, SaveV2Codes.Structure, f);
            AssertRejected("undefined literal", Swap("\"previousRunId\":null", "\"previousRunId\":undefined"), cat, SaveV2Codes.Structure, f);

            AssertRejected("uppercase UUID", Swap(SaveV2Scenarios.Uuid(2).ToString("D"), SaveV2Scenarios.Uuid(2).ToString("D").ToUpperInvariant().Replace("2", "A")), cat, SaveV2Codes.Structure, f);
            AssertRejected("UUID without hyphens", Swap("\"checkpointId\":\"" + SaveV2Scenarios.Uuid(2).ToString("D") + "\"", "\"checkpointId\":\"" + SaveV2Scenarios.Uuid(2).ToString("N") + "\""), cat, SaveV2Codes.Structure, f);
            AssertRejected("all-zero UUID", Swap("\"checkpointId\":\"" + SaveV2Scenarios.Uuid(2).ToString("D") + "\"", "\"checkpointId\":\"" + Guid.Empty.ToString("D") + "\""), cat, SaveV2Codes.Structure, f);
            AssertRejected("empty string", Swap("\"checkpointId\":\"" + SaveV2Scenarios.Uuid(2).ToString("D") + "\"", "\"checkpointId\":\"\""), cat, SaveV2Codes.Structure, f);
            AssertRejected("ID of 129 code units", Swap("\"machineId\":\"machine.miner\"", "\"machineId\":\"" + new string('a', 129) + "\""), cat, SaveV2Codes.Limit, f);

            Assert.That(f, Is.Empty, string.Join("\n", f));
        }

        [Test]
        public void Envelope_AcceptsExactlyOneMebibyteAndDepthOfTheDocument()
        {
            byte[] padded = new byte[SaveV2Mapper.MaximumByteCount];
            for (int i = 0; i < padded.Length; i++) padded[i] = (byte)' ';
            Buffer.BlockCopy(_mid.Bytes, 0, padded, 0, _mid.Bytes.Length);
            Assert.That(padded.Length, Is.EqualTo(1024 * 1024));
            Assert.That(ReadMid(padded).IsSuccess, Is.True, "exactly 1 MiB is within the limit (whitespace is insignificant)");
            Assert.That(ReadMid(padded.Concat(new byte[] { (byte)' ' }).ToArray()).Code, Is.EqualTo(SaveV2Codes.Size));
        }

        [Test]
        public void Envelope_ExactBoundaryOfTenToTheEighteenth_IsAccepted()
        {
            byte[] bytes = Rx("\"cash\":\\d+", "\"cash\":1000000000000000000");
            SaveV2ReadResult result = ReadMid(bytes);
            Assert.That(result.IsSuccess, Is.True, result.Code);
            Assert.That(result.Document.Run.Cash, Is.EqualTo(1_000_000_000_000_000_000L));
        }

        [Test]
        public void Envelope_ListAndMachineLimits()
        {
            var f = new List<string>();
            string machine = new Regex("\\{\"instanceId\".*?\\}").Match(_midText).Value;
            Assert.That(machine, Is.Not.Empty);
            string machines65 = "\"machines\":[" + string.Join(",", Enumerable.Repeat(machine, 65)) + "]";
            AssertRejected("65 machines", Rx("\"machines\":\\[.*?\\](?=,\"priority\")", machines65), _mid.Catalog, SaveV2Codes.Limit, f);
            string unlocked257 = "\"unlocked\":[" + string.Join(",", Enumerable.Range(0, 257).Select(i => "\"id" + i + "\"")) + "]";
            AssertRejected("257 unlocked entries", Rx("\"unlocked\":\\[[^\\]]*\\]", unlocked257), _mid.Catalog, SaveV2Codes.Limit, f);
            string autoSell257 = "\"autoSell\":[" + string.Join(",", Enumerable.Range(0, 257).Select(i => "\"id" + i + "\"")) + "]";
            AssertRejected("257 autoSell entries", Rx("\"autoSell\":\\[[^\\]]*\\]", autoSell257), _mid.Catalog, SaveV2Codes.Limit, f);
            string effect = new Regex("\\{\"itemId\".*?\\}").Match(_midText).Value;
            string effects257 = "\"effects\":[" + string.Join(",", Enumerable.Repeat(effect, 257)) + "]";
            AssertRejected("257 effects", Rx("\"effects\":\\[.*?\\](?=,\"overdrive\")", effects257), _mid.Catalog, SaveV2Codes.Limit, f);
            Assert.That(f, Is.Empty, string.Join("\n", f));
        }

        // ------------------------------------------------------------------------------------------------------
        // Semantic rejections: unresolved IDs, locked content, invariants
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void Semantics_UnresolvedIds_AreRejected()
        {
            var f = new List<string>();
            RunCatalog cat = _mid.Catalog;
            AssertRejected("machine", Swap("\"machineId\":\"machine.miner\"", "\"machineId\":\"machine.ghost\""), cat, SaveV2Codes.Unresolved, f);
            AssertRejected("research", Swap("\"id\":\"research.speed\"", "\"id\":\"research.ghost\""), cat, SaveV2Codes.Unresolved, f);
            AssertRejected("industry", Swap("\"id\":\"industry.cash\"", "\"id\":\"industry.ghost\""), cat, SaveV2Codes.Unresolved, f);
            AssertRejected("permanent item", Swap("\"id\":\"item.boost\"", "\"id\":\"item.ghost\""), cat, SaveV2Codes.Unresolved, f);
            // An unknown contract offer is content drift since Save V3 (T335): the contract-scoped entry is dropped, not rejected
            // (SaveV3ContentDriftTests). Every other unresolved ID above and below stays fail-closed.
            foreach (byte[] drift in new[]
                     {
                         Swap("\"offerId\":\"contract.timed-bar\"", "\"offerId\":\"contract.ghost\""),
                         Swap("{\"offerId\":\"contract.plate\"", "{\"offerId\":\"contract.ghost\""),
                     })
            {
                SaveV2ReadResult dropped = SaveV2Mapper.Deserialize(drift, cat);
                if (!dropped.IsSuccess || dropped.Document.DroppedContractEntries != 1) f.Add("contract offer drift: " + (dropped.Code ?? "not dropped"));
            }
            AssertRejected("unlocked", Swap("\"unlocked\":[\"iron.ore\"", "\"unlocked\":[\"nope.id\",\"iron.ore\""), cat, SaveV2Codes.Unresolved, f);
            AssertRejected("inventory resource", Swap("\"resourceId\":\"iron.bar\"", "\"resourceId\":\"iron.aaa\""), cat, SaveV2Codes.Unresolved, f);
            AssertRejected("effect item", Swap("\"itemId\":\"item.boost\"", "\"itemId\":\"item.ghost\""), cat, SaveV2Codes.Unresolved, f);
            AssertRejected("auto-sell", Swap("\"autoSell\":[\"iron.ore\"]", "\"autoSell\":[\"iron.zzz\"]"), cat, SaveV2Codes.Unresolved, f);
            AssertRejected("offline policy revision", Swap("\"offlinePolicyRevision\":1", "\"offlinePolicyRevision\":2"), cat, SaveV2Codes.Version, f);
            Assert.That(f, Is.Empty, string.Join("\n", f));
        }

        [Test]
        public void Semantics_LockedMachineOrResource_IsRejectedUnlessTheUnlockedSetListsIt()
        {
            RunCatalog catalog = RunCommandFixtures.Catalog(SaveV2Scenarios.Options());
            var run = new RunState(catalog, RunSimHarness.Id(1), 1, null, 25, 0, 1, 1, new[] { "iron.ore", "iron.bar", "machine.miner" });
            RunSimHarness.Add(run, "machine.miner", 0, 200);
            Assert.That(run.Inventory.TryImport(new[] { new ResourceAmount("iron.bar", 5) }), Is.EqualTo(RunStateResult.Ok));
            Assert.That(run.TrySetAutoSell("iron.ore", true), Is.EqualTo(RunStateResult.Ok));
            SaveV2WriteResult written = SaveV2Mapper.Serialize(SaveV2Scenarios.Meta(catalog, 1, 0, RunSimHarness.StartUtcMs), run, new PermanentState(catalog));
            Assert.That(written.IsSuccess, Is.True, written.Code);
            var f = new List<string>();
            AssertRejected("locked machine", SaveV2Scenarios.Mutate(written.Bytes, "\"unlocked\":[\"iron.ore\",\"iron.bar\",\"machine.miner\"]", "\"unlocked\":[\"iron.ore\",\"iron.bar\"]"), catalog, SaveV2Codes.Locked, f);
            AssertRejected("locked stocked resource", SaveV2Scenarios.Mutate(written.Bytes, "\"unlocked\":[\"iron.ore\",\"iron.bar\",\"machine.miner\"]", "\"unlocked\":[\"iron.ore\",\"machine.miner\"]"), catalog, SaveV2Codes.Locked, f);
            AssertRejected("locked auto-sell resource", SaveV2Scenarios.Mutate(written.Bytes, "\"unlocked\":[\"iron.ore\",\"iron.bar\",\"machine.miner\"]", "\"unlocked\":[\"iron.bar\",\"machine.miner\"]"), catalog, SaveV2Codes.Locked, f);
            Assert.That(f, Is.Empty, string.Join("\n", f));
            Assert.That(SaveV2Mapper.Deserialize(written.Bytes, catalog).IsSuccess, Is.True, "the same state is valid when it lists its unlocks");
        }

        [Test]
        public void Semantics_InvariantViolations_AreRejected()
        {
            var f = new List<string>();
            RunCatalog cat = _mid.Catalog;
            AssertRejected("progress above the cycle", Rx("\"progressTicks\":\\d+", "\"progressTicks\":999999999999"), cat, SaveV2Codes.Invariant, f);
            AssertRejected("machine level above max", Rx("(\"slot\":0,\"level\":)\\d+", "${1}26"), cat, SaveV2Codes.Invariant, f);
            AssertRejected("slot beyond the slot count", Swap("\"slot\":1,", "\"slot\":99,"), cat, SaveV2Codes.Invariant, f);
            AssertRejected("duplicate slot", Swap("\"slot\":1,", "\"slot\":0,"), cat, SaveV2Codes.Invariant, f);
            AssertRejected("duplicate inventory id", Rx("(\"inventory\":\\[\\{\"resourceId\":\"([^\"]+)\",\"units\":\\d+\\},\\{\"resourceId\":\")[^\"]+\"", "${1}$2\""), cat, SaveV2Codes.Invariant, f);
            AssertRejected("inventory above capacity", Rx("(\"inventory\":\\[\\{\"resourceId\":\"[^\"]+\",\"units\":)\\d+", "${1}1000000000000000000"), cat, SaveV2Codes.Invariant, f);
            AssertRejected("priority is not a permutation", Rx("\"priority\":\\[[^\\]]*\\]", "\"priority\":[]"), cat, SaveV2Codes.Invariant, f);
            AssertRejected("duplicate unlocked", Swap("\"unlocked\":[\"iron.ore\"", "\"unlocked\":[\"iron.ore\",\"iron.ore\""), cat, SaveV2Codes.Invariant, f);
            AssertRejected("research above max", Swap("\"id\":\"research.speed\",\"level\":1", "\"id\":\"research.speed\",\"level\":4"), cat, SaveV2Codes.Invariant, f);
            AssertRejected("industry above max", Swap("\"id\":\"industry.cash\",\"level\":2", "\"id\":\"industry.cash\",\"level\":4"), cat, SaveV2Codes.Invariant, f);
            AssertRejected("storage level above the catalog", Swap("\"storage\":{\"level\":2", "\"storage\":{\"level\":99"), cat, SaveV2Codes.Invariant, f);
            AssertRejected("factory level above the catalog", Swap("\"factory\":{\"level\":2", "\"factory\":{\"level\":99"), cat, SaveV2Codes.Invariant, f);
            AssertRejected("effect kind is not the Item's", Swap("\"effectKind\":\"machineSpeed\"", "\"effectKind\":\"machineOutput\""), cat, SaveV2Codes.Invariant, f);
            AssertRejected("effect target is not the Item's", Swap("\"targetKey\":\"factory\"", "\"targetKey\":\"machine.miner\""), cat, SaveV2Codes.Invariant, f);
            AssertRejected("duplicate effect key", Rx("(\\{\"itemId\":\"item.boost\".*?\\})", "$1,$1"), cat, SaveV2Codes.Invariant, f);
            AssertRejected("one-sided Overdrive", Rx("\"cooldownUntilUtcMs\":\\d+", "\"cooldownUntilUtcMs\":null"), cat, SaveV2Codes.Invariant, f);
            AssertRejected("completion above the table", Swap("\"offerId\":\"contract.plate\",\"count\":1", "\"offerId\":\"contract.plate\",\"count\":2000"), cat, SaveV2Codes.Invariant, f);
            AssertRejected("reconciled from itself", Swap("\"reconciledFromCheckpointId\":\"" + SaveV2Scenarios.Uuid(1).ToString("D") + "\"", "\"reconciledFromCheckpointId\":\"" + SaveV2Scenarios.Uuid(2).ToString("D") + "\""), cat, SaveV2Codes.Invariant, f);
            AssertRejected("locked content in an emptied unlocked set", Rx("\"unlocked\":\\[[^\\]]*\\]", "\"unlocked\":[]"), cat, SaveV2Codes.Locked, f);
            Assert.That(f, Is.Empty, string.Join("\n", f));
        }

        // ------------------------------------------------------------------------------------------------------
        // Cross-field bounds (SEC-2)
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void CrossFieldBounds_EachCorruptScenario_IsRejectedWithTheCrossFieldCode()
        {
            List<SaveV2Fixture> corrupt = _all.Where(x => x.IsCorrupt).ToList();
            Assert.That(corrupt.Count, Is.EqualTo(9));
            var f = new List<string>();
            foreach (SaveV2Fixture c in corrupt) AssertRejected(c.FileName, c.Bytes, c.Catalog, SaveV2Codes.CrossField, f);
            Assert.That(f, Is.Empty, string.Join("\n", f));
        }

        [Test]
        public void CrossFieldBounds_TheBoundaryItselfIsAccepted()
        {
            // The bound is "not after": a start exactly at the departure is valid, one millisecond later is not.
            ActiveEffect effect = _mid.Run.Effects.Effects[0];
            long departure = _mid.Meta.DepartureUtcMs;
            long duration = effect.EndUtcMs - effect.StartUtcMs;
            byte[] atDeparture = Swap("\"startUtcMs\":" + effect.StartUtcMs + ",\"endUtcMs\":" + effect.EndUtcMs, "\"startUtcMs\":" + departure + ",\"endUtcMs\":" + (departure + duration));
            SaveV2ReadResult ok = ReadMid(atDeparture);
            Assert.That(ok.IsSuccess, Is.True, ok.Code);
            byte[] after = Swap("\"startUtcMs\":" + effect.StartUtcMs + ",\"endUtcMs\":" + effect.EndUtcMs, "\"startUtcMs\":" + (departure + 1) + ",\"endUtcMs\":" + (departure + 1 + duration));
            Assert.That(ReadMid(after).Code, Is.EqualTo(SaveV2Codes.CrossField));
        }

        [Test]
        public void DurationBounds_AnEndNotAfterItsStart_OrAboveTheSevenDayCap_IsRejected_AndTheCapItselfIsAccepted()
        {
            ActiveEffect effect = _mid.Run.Effects.Effects[0];
            ActiveContract contract = _mid.Run.Contracts.Active;
            long activeUntil = _mid.Run.Overdrive.ActiveUntilUtcMs.Value;
            long cooldownUntil = _mid.Run.Overdrive.CooldownUntilUtcMs.Value;
            long cap = RunLimits.MaxTimedDurationMs;
            Assert.That(cap, Is.EqualTo(604_800_000L));
            string end = "\"endUtcMs\":" + effect.EndUtcMs;
            string deadline = "\"deadlineUtcMs\":" + contract.DeadlineUtcMs.Value;
            string cooldown = "\"cooldownUntilUtcMs\":" + cooldownUntil;

            // end == start and end < start are rejected; one millisecond is the shortest span; the cap exactly is valid.
            Assert.That(ReadMid(Swap(end, "\"endUtcMs\":" + effect.StartUtcMs)).Code, Is.EqualTo(SaveV2Codes.CrossField));
            Assert.That(ReadMid(Swap(end, "\"endUtcMs\":" + (effect.StartUtcMs - 1))).Code, Is.EqualTo(SaveV2Codes.CrossField));
            Assert.That(ReadMid(Swap(end, "\"endUtcMs\":" + (effect.StartUtcMs + 1))).IsSuccess, Is.True);
            Assert.That(ReadMid(Swap(end, "\"endUtcMs\":" + (effect.StartUtcMs + cap))).IsSuccess, Is.True);
            Assert.That(ReadMid(Swap(end, "\"endUtcMs\":" + (effect.StartUtcMs + cap + 1))).Code, Is.EqualTo(SaveV2Codes.CrossField));

            Assert.That(ReadMid(Swap(deadline, "\"deadlineUtcMs\":" + contract.AcceptedUtcMs)).Code, Is.EqualTo(SaveV2Codes.CrossField));
            Assert.That(ReadMid(Swap(deadline, "\"deadlineUtcMs\":" + (contract.AcceptedUtcMs + cap))).IsSuccess, Is.True);
            Assert.That(ReadMid(Swap(deadline, "\"deadlineUtcMs\":" + (contract.AcceptedUtcMs + cap + 1))).Code, Is.EqualTo(SaveV2Codes.CrossField));

            // A zero cooldown is valid content; a cooldown before the active end and one above the cap are not.
            Assert.That(ReadMid(Swap(cooldown, "\"cooldownUntilUtcMs\":" + activeUntil)).IsSuccess, Is.True);
            Assert.That(ReadMid(Swap(cooldown, "\"cooldownUntilUtcMs\":" + (activeUntil - 1))).Code, Is.EqualTo(SaveV2Codes.CrossField));
            Assert.That(ReadMid(Swap(cooldown, "\"cooldownUntilUtcMs\":" + (activeUntil + cap))).IsSuccess, Is.True);
            Assert.That(ReadMid(Swap(cooldown, "\"cooldownUntilUtcMs\":" + (activeUntil + cap + 1))).Code, Is.EqualTo(SaveV2Codes.CrossField));
        }

        [Test]
        public void DurationBounds_AMidRunSaveLoadsAfterTheItemOfferAndOverdriveDurationsWereRetunedInContent()
        {
            AssertRetunedLoads(3, 2, 2, 4);
        }

        [Test]
        public void DurationBounds_AMidRunSaveLoadsAfterTheDurationsAndCooldownWereShortenedInContent()
        {
            // 1000 ms each: far shorter than the active effect, contract and Overdrive of the save (QA reproduced a CrossField here).
            AssertRetunedLoads(-1000, -1000, -1000, -1000);
        }

        /// <summary>A positive factor multiplies the durations; a negative one sets them to its absolute value in milliseconds.</summary>
        private static void AssertRetunedLoads(long itemFactor, long offerFactor, long overdriveFactor, long cooldownFactor)
        {
            long Tune(long value, long factor) => factor < 0 ? -factor : value * factor;
            RunCommandFixtures.Options retuned = SaveV2Scenarios.Options();
            ItemDef boost = retuned.Items.Single();
            retuned.Items = new[]
            {
                new ItemDef(boost.Id, boost.Tier, boost.Label, boost.PresentationKey, Tune(boost.DurationMs, itemFactor), boost.TargetKind,
                    boost.AllowedMachineIds.ToArray(), boost.Effect),
            };
            ContractOfferDef[] offers = retuned.Offers.ToArray();
            for (int i = 0; i < offers.Length; i++)
            {
                ContractOfferDef o = offers[i];
                if (!o.DurationMs.HasValue) continue;
                offers[i] = new ContractOfferDef(o.Id, o.Kind, o.ResourceId, o.QuantityCurve, Tune(o.DurationMs.Value, offerFactor), o.Rewards.ToArray(), o.Unlock);
            }

            retuned.Offers = offers;
            OverdriveDef od = _mid.Catalog.Overdrive;
            retuned.Overdrive = new OverdriveDef(Tune(od.DurationMs, overdriveFactor), Tune(od.CooldownMs, cooldownFactor), od.SpeedPpm, od.Unlock);
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(retuned, 25);
            SaveV2ReadResult read = SaveV2Mapper.Deserialize(_mid.Bytes, rig.Catalog);
            Assert.That(read.IsSuccess, Is.True, read.Code);
            // The persisted instants survive exactly; nothing is re-derived from the new durations.
            Assert.That(read.Document.Run.Effects.Effects[0].EndUtcMs, Is.EqualTo(_mid.Run.Effects.Effects[0].EndUtcMs));
            Assert.That(read.Document.Run.Contracts.Active.DeadlineUtcMs, Is.EqualTo(_mid.Run.Contracts.Active.DeadlineUtcMs));
            Assert.That(read.Document.Run.Overdrive.CooldownUntilUtcMs, Is.EqualTo(_mid.Run.Overdrive.CooldownUntilUtcMs));
        }

        [Test]
        public void CrossFieldBounds_ARunNumberOfTwoIsValidWithAPreviousRunAndPrestigeCountOne()
        {
            SaveV2Fixture post = _all.Single(x => x.FileName == "04-post-prestige.json");
            SaveV2ReadResult read = SaveV2Mapper.Deserialize(post.Bytes, post.Catalog);
            Assert.That(read.IsSuccess, Is.True, read.Code);
            Assert.That(read.Document.Run.RunNumber, Is.EqualTo(read.Document.Permanent.PrestigeCount + 1));
            Assert.That(read.Document.Run.PreviousRunId.HasValue, Is.True);
        }

        [Test]
        public void Serialize_RefusesAStateTheReaderWouldReject()
        {
            // A departure before the effect start breaks load rule 4, so the writer never produces the file.
            SaveV2Meta early = SaveV2Scenarios.Meta(_mid.Catalog, 2, 7, 0);
            SaveV2WriteResult written = SaveV2Mapper.Serialize(early, _mid.Run, _mid.Permanent);
            Assert.That(written.IsSuccess, Is.False);
            Assert.That(written.Bytes, Is.Null);
            Assert.That(written.Code, Is.EqualTo(SaveV2Codes.CrossField));
            Assert.That(written.Message, Is.EqualTo(SaveV2Codes.Message(SaveV2Codes.CrossField)));
            Assert.That(SaveV2Mapper.Serialize(null, _mid.Run, _mid.Permanent).Code, Is.EqualTo(SaveV2Codes.Write));
            Assert.That(SaveV2Mapper.Serialize(_mid.Meta, null, _mid.Permanent).Code, Is.EqualTo(SaveV2Codes.Write));
            Assert.That(SaveV2Mapper.Serialize(_mid.Meta, _mid.Run, null).Code, Is.EqualTo(SaveV2Codes.Write));
        }

        [Test]
        public void Serialize_IsDeterministic()
        {
            Assert.That(SaveV2Mapper.Serialize(_mid.Meta, _mid.Run, _mid.Permanent).Bytes, Is.EqualTo(_mid.V3Bytes));
            Assert.That(SaveV2Mapper.Serialize(_mid.Meta, _mid.Run, _mid.Permanent).Bytes, Is.EqualTo(_mid.V3Bytes));
            Assert.That(SaveV2Mapper.Serialize(_mid.Meta, _mid.Run, _mid.Permanent, SaveV2Mapper.LegacySchemaVersion).Bytes, Is.EqualTo(_mid.Bytes));
            Assert.That(_mid.V3Bytes.Length, Is.LessThan(SaveV2Mapper.MaximumByteCount));
            Assert.That(Utf8.GetString(_mid.Bytes).StartsWith("{\"schemaVersion\":2,", StringComparison.Ordinal), Is.True);
            Assert.That(Utf8.GetString(_mid.V3Bytes).StartsWith("{\"schemaVersion\":3,", StringComparison.Ordinal), Is.True, "the writer always writes V3 (T334)");
        }

        // ------------------------------------------------------------------------------------------------------
        // Static-code diagnostics (SEC-5)
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void Diagnostics_UseStaticCodesOnly_NeverTextFromTheFile()
        {
            const string hostile = "HOSTILE-<b>secret</b>-C:/Users/victim";
            var reported = new List<CheckpointDiagnostic>();
            Action<CheckpointDiagnostic> listener = reported.Add;
            CheckpointDiagnostics.Reported += listener;
            try
            {
                var hostileFiles = new Dictionary<string, byte[]>
                {
                    { "machine id", Swap("\"machineId\":\"machine.miner\"", "\"machineId\":\"" + hostile + "\"") },
                    { "member name", Swap("\"generation\":7,", "\"generation\":7,\"" + hostile + "\":1,") },
                    { "uuid", Swap("\"checkpointId\":\"" + SaveV2Scenarios.Uuid(2).ToString("D") + "\"", "\"checkpointId\":\"" + hostile + "\"") },
                    { "item id", Swap("\"itemId\":\"item.boost\"", "\"itemId\":\"" + hostile + "\"") },
                    { "string for number", Swap("\"generation\":7", "\"generation\":\"" + hostile + "\"") },
                    { "text after the document", _mid.Bytes.Concat(Bytes(hostile)).ToArray() },
                };
                foreach (KeyValuePair<string, byte[]> file in hostileFiles)
                {
                    SaveV2ReadResult result = ReadMid(file.Value);
                    Assert.That(result.IsSuccess, Is.False, file.Key);
                    Assert.That(result.Code, Does.StartWith("save.v2."), file.Key);
                    Assert.That(result.Code, Does.Not.Contain("HOSTILE"), file.Key);
                    Assert.That(result.Message, Does.Not.Contain("HOSTILE"), file.Key);
                    Assert.That(result.Message, Is.EqualTo(SaveV2Codes.Message(result.Code)), file.Key);
                }
            }
            finally
            {
                CheckpointDiagnostics.Reported -= listener;
            }

            Assert.That(reported.Count, Is.EqualTo(6));
            foreach (CheckpointDiagnostic d in reported)
            {
                string text = d.ToString();
                Assert.That(text, Does.Not.Contain("HOSTILE"));
                Assert.That(text, Does.Not.Contain("victim"));
                Assert.That(d.Operation, Is.EqualTo("SaveV2.Deserialize"));
                Assert.That(d.Stage, Does.StartWith("save.v2."));
                Assert.That(d.ExceptionType, Is.Empty);
                Assert.That(d.Origin, Is.Empty);
            }
        }

        [Test]
        public void Diagnostics_EveryCodeHasItsOwnStaticCopy()
        {
            string[] codes = typeof(SaveV2Codes).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()).ToArray();
            Assert.That(codes.Length, Is.EqualTo(11));
            Assert.That(codes.Distinct().Count(), Is.EqualTo(codes.Length));
            string generic = SaveV2Codes.Message("unknown");
            foreach (string code in codes)
            {
                Assert.That(code, Does.StartWith("save.v2."));
                Assert.That(SaveV2Codes.Message(code), Is.Not.Empty);
                Assert.That(SaveV2Codes.Message(code), Is.Not.EqualTo(generic), code);
            }

            Assert.That(codes.Select(SaveV2Codes.Message).Distinct().Count(), Is.EqualTo(codes.Length));
        }

        [Test]
        public void Diagnostics_ARejectedWriteIsReportedWithAStaticCodeToo()
        {
            var reported = new List<CheckpointDiagnostic>();
            Action<CheckpointDiagnostic> listener = reported.Add;
            CheckpointDiagnostics.Reported += listener;
            try
            {
                Assert.That(SaveV2Mapper.Serialize(null, _mid.Run, _mid.Permanent).IsSuccess, Is.False);
            }
            finally
            {
                CheckpointDiagnostics.Reported -= listener;
            }

            Assert.That(reported.Count, Is.EqualTo(1));
            Assert.That(reported[0].Operation, Is.EqualTo("SaveV2.Serialize"));
            Assert.That(reported[0].Stage, Is.EqualTo(SaveV2Codes.Write));
        }

        // ------------------------------------------------------------------------------------------------------
        // Save compatibility documentation
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void SaveCompatibility_IsDocumentedInTheContract()
        {
            string path = Path.Combine(Application.dataPath, "..", "specs", "003-first-factory-prestige", "contracts", "save-v2-and-v1-import.md");
            Assert.That(File.Exists(path), Is.True, path);
            string contract = File.ReadAllText(path);
            Assert.That(contract, Does.Contain("`schemaVersion`").And.Contain("independent of `contentSchemaVersion`"));
            Assert.That(contract, Does.Contain("reserved ID `overdrive`"), "the unlocked exception of T140");
            Assert.That(contract, Does.Contain("first settle"), "a fresh, imported or reset state is not a fixed point");
            Assert.That(contract, Does.Contain("unlock order"), "the unlocked list order");
            Assert.That(contract, Does.Contain("W003-B"));
            Assert.That(contract, Does.Contain("Optional members"), "null instead of an absent member");
            Assert.That(contract, Does.Contain("save.v2.crossfield"));
        }
    }
}
