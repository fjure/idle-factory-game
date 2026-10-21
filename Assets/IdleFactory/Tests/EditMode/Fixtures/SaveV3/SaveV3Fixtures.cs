using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Tests.EditMode.Run;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using static IdleFactory.Tests.EditMode.Fixtures.Run.ContractFixtures;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// One Save V3 fixture of the QA-11 table (specs/005-economy-contracts/tasks.md, "QA-11: Save V3 fixture-to-assertion table", T335): the
    /// bytes, the catalog they load against, and the expected result. The fixtures are built in memory from the contract rigs of
    /// <see cref="ContractFixtures"/> through the real commands and the real writer, then mutated in exactly one place, so each one is a
    /// syntactically valid file that breaks (or exercises) exactly the rule its name says.
    /// </summary>
    internal sealed class SaveV3Fixture
    {
        /// <summary>The QA-11 row name (or the name of a delegated #399 follow-up case).</summary>
        public string Name;

        /// <summary>The catalog the bytes are read against (a drift fixture reads against changed content).</summary>
        public RunCatalog Catalog;

        public byte[] Bytes;

        /// <summary>The mapper code of the rejection (<see cref="SaveV2Codes"/>), or null when the file loads.</summary>
        public string ExpectedCode;

        /// <summary>The persistence reason the store's outcome maps to (<see cref="RunPersistenceReasons.From"/>), or null when the file loads.</summary>
        public string ExpectedReason;

        /// <summary>For a file that loads: how many contract-scoped entries the reader drops as content drift (T335).</summary>
        public int ExpectedDropped;

        public bool Loads => ExpectedCode == null;

        public override string ToString() => Name;
    }

    /// <summary>The in-memory Save V3 fixtures of T335 (issue #400) and the helpers the SaveV3 suites share.</summary>
    internal static class SaveV3Fixtures
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        // ---- rigs, writer, reader ---------------------------------------------------------------------------------------------

        /// <summary>The canonical V3 bytes of the rig, departure = the rig's timeline, checkpoint 1, generation 0.</summary>
        public static byte[] Write(RunCommandFixtures.Rig rig)
        {
            SaveV2WriteResult written = SaveV2Mapper.Serialize(SaveV2Scenarios.Meta(rig.Catalog, 1, 0, rig.Session.TimelineUtcMs), rig.State, rig.Session.Permanent);
            Assert.That(written.IsSuccess, Is.True, written.Code);
            return written.Bytes;
        }

        /// <summary>A new rig over the state read from <paramref name="bytes"/> against <paramref name="catalog"/>, anchored at the departure.</summary>
        public static RunCommandFixtures.Rig Load(RunCatalog catalog, byte[] bytes, out SaveV2Document document)
        {
            SaveV2ReadResult read = SaveV2Mapper.Deserialize(bytes, catalog);
            Assert.That(read.IsSuccess, Is.True, read.Code);
            document = read.Document;
            var loaded = new RunCommandFixtures.Rig { Catalog = catalog, State = document.Run, NextId = 500 };
            loaded.Session = new RunSession(catalog, document.Run, document.Permanent, document.Meta.DepartureUtcMs);
            loaded.Commands = new RunCommands(loaded.Session, () => RunSimHarness.Id(loaded.NextId++));
            return loaded;
        }

        public static string Text(byte[] bytes) => Utf8.GetString(bytes);

        public static byte[] Bytes(string text) => new UTF8Encoding(false).GetBytes(text);

        /// <summary>The bytes with the first match of <paramref name="pattern"/> replaced (the match must exist).</summary>
        public static byte[] Rx(byte[] bytes, string pattern, string replacement)
        {
            var regex = new Regex(pattern, RegexOptions.CultureInvariant);
            string text = Text(bytes);
            Assert.That(regex.IsMatch(text), Is.True, "pattern not found: " + pattern);
            return Bytes(regex.Replace(text, replacement, 1));
        }

        public static void DeliverOffer(RunCommandFixtures.Rig rig, string offerId, long units)
        {
            Accept(rig, offerId);
            RunSimHarness.Stock(rig.State, Ore, units);
            RunCommandResult<ContractDeliveryResult> r = rig.Commands.DeliverContract();
            Assert.That(r.IsSuccess, Is.True, offerId + ": " + r.ReasonCode);
        }

        /// <summary>
        /// Every V3 member populated (as in SaveV3MapperTests, T334): completions and cooldowns of bonus-always (its bonus consumed) and
        /// deliver, the triggered bonus-ore, a run-wide total of 2 and an active production contract with 2 of 5 units produced.
        /// </summary>
        public static RunCommandFixtures.Rig AllMembers()
        {
            RunCommandFixtures.Rig rig = MiningRig(null);
            DeliverOffer(rig, BonusAlways, 3);
            DeliverOffer(rig, Deliver, 5);
            Accept(rig, Produce);
            rig.Advance(3 * Second);
            Assert.That(Board(rig).Active.Produced, Is.EqualTo(2));
            Assert.That(Board(rig).RunCompletionTotal, Is.EqualTo(2));
            Assert.That(Board(rig).CooldownStamps().Select(c => c.OfferId), Is.EqualTo(new[] { BonusAlways, Deliver }));
            Assert.That(Board(rig).BonusOffers().Select(b => b.OfferId + "/" + b.Consumed), Is.EqualTo(new[] { BonusAlways + "/True", BonusOre + "/False" }));
            return rig;
        }

        // ---- content drift catalogs -------------------------------------------------------------------------------------------

        private static readonly UnlockCondition Always = new UnlockCondition("always");

        private static ContractRewardDef[] Rp(long amount) => new[] { new ContractRewardDef("rp", new Curve(amount, 1_000_000, "floor")) };

        /// <summary><see cref="ContractFixtures.Deliver"/> turned into a production objective (the objective changed, T335).</summary>
        public static ContractOfferDef DeliverAsProduction() =>
            new ContractOfferDef(Deliver, "plate", Ore, new Curve(5, 1_010_000, "ceil"), null, Rp(2), Always, objective: "produce",
                rewardRatioPpm: 1_500_000, cooldownMs: CooldownMs);

        /// <summary><see cref="ContractFixtures.Produce"/> turned into a delivery objective.</summary>
        public static ContractOfferDef ProduceAsDelivery() =>
            new ContractOfferDef(Produce, "plate", Ore, new Curve(5, 1_000_000, "ceil"), null, Rp(2), Always, rewardRatioPpm: 1_200_000,
                cooldownMs: CooldownMs);

        /// <summary><see cref="ContractFixtures.Deliver"/> turned into a bonus offer (its class changed).</summary>
        public static ContractOfferDef DeliverAsBonus() =>
            new ContractOfferDef(Deliver, "plate", Ore, new Curve(3, 1_000_000, "ceil"), null, Rp(1), Always, offerClass: "bonus",
                rewardRatioPpm: 1_800_000, cooldownMs: 10_000, bonus: new BonusDef(Always, BonusWindowMs));

        /// <summary>
        /// <paramref name="catalog"/> with its offers changed by <paramref name="change"/> (an offer mapped to null is removed from the content) and
        /// its content revision raised by <paramref name="revisionStep"/>.
        /// </summary>
        public static RunCatalog Drift(RunCatalog catalog, Func<ContractOfferDef, ContractOfferDef> change, int revisionStep = 0)
        {
            ContractOfferDef[] offers = catalog.ContractOffers.Values.OrderBy(o => o.Id, StringComparer.Ordinal).Select(change).Where(o => o != null).ToArray();
            return TestCatalogBuilder.From(catalog).WithContractOffers(offers).WithRevision(catalog.ContentRevision + revisionStep).Build();
        }

        /// <summary><paramref name="catalog"/> without the offers <paramref name="removed"/> (same content revision).</summary>
        public static RunCatalog Without(RunCatalog catalog, params string[] removed) =>
            Drift(catalog, o => removed.Contains(o.Id) ? null : o);

        // ---- the QA-11 matrix ------------------------------------------------------------------------------------------------

        private static SaveV3Fixture Rejected(string name, RunCatalog catalog, byte[] bytes, string code) => new SaveV3Fixture
        {
            Name = name,
            Catalog = catalog,
            Bytes = bytes,
            ExpectedCode = code,
            ExpectedReason = code == SaveV2Codes.Version ? RunPersistenceReasons.UnsupportedVersion
                : code == SaveV2Codes.Unresolved ? RunPersistenceReasons.UnresolvedContent
                : RunPersistenceReasons.InvalidCheckpoint,
        };

        private static SaveV3Fixture Loads(string name, RunCatalog catalog, byte[] bytes, int dropped = 0) => new SaveV3Fixture
        {
            Name = name,
            Catalog = catalog,
            Bytes = bytes,
            ExpectedDropped = dropped,
        };

        /// <summary>
        /// The V3 rows of the QA-11 table (the <c>v2/*</c> row and the fault injection are SaveV3StoreTests) plus the delegated #399
        /// follow-up cases: unsorted lists without a duplicate, the future-instant bound of <c>readyAtUtcMs</c> and <c>windowEndUtcMs</c>, and
        /// the active bonus offer without an open <c>bonusOffers</c> record.
        /// </summary>
        public static List<SaveV3Fixture> Matrix()
        {
            var list = new List<SaveV3Fixture>();

            RunCommandFixtures.Rig all = AllMembers();
            byte[] allBytes = Write(all);
            RunCatalog cat = all.Catalog;
            long departure = all.Session.TimelineUtcMs;

            RunCommandFixtures.Rig auto = Rig();
            Accept(auto, Deliver);
            Assert.That(auto.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            list.Add(Loads("v3-autodeliver-true", auto.Catalog, Write(auto)));

            RunCommandFixtures.Rig produce = MiningRig(Produce);
            byte[] produceBytes = Write(produce);
            list.Add(Rejected("v3-autodeliver-on-produce", produce.Catalog, Rx(produceBytes, "\"autoDeliver\":false", "\"autoDeliver\":true"), SaveV2Codes.Invariant));
            list.Add(Rejected("v3-produced-over-required", cat, Rx(allBytes, "\"produced\":2\\}", "\"produced\":6}"), SaveV2Codes.Invariant));
            list.Add(Rejected("v3-bool-wrong-type", cat, Rx(allBytes, "\"autoDeliver\":false", "\"autoDeliver\":0"), SaveV2Codes.Structure));
            list.Add(Rejected("v3-cooldown-duplicate-offer", cat, Rx(allBytes, "(\"contractCooldowns\":\\[)(\\{[^}]*\\})", "$1$2,$2"), SaveV2Codes.Invariant));

            RunCommandFixtures.Rig past = Rig();
            DeliverOffer(past, Deliver, 5);
            past.Advance((CooldownMs + 5_000) * RunSession.TicksPerMs);
            list.Add(Loads("v3-cooldown-past", past.Catalog, Write(past)));

            list.Add(Rejected("v3-bonus-missing-windowend (triggered)", cat,
                Rx(allBytes, "\"state\":\"triggered\",\"windowEndUtcMs\":\\d+", "\"state\":\"triggered\""), SaveV2Codes.Structure));
            list.Add(Rejected("v3-bonus-missing-windowend (consumed)", cat,
                Rx(allBytes, "\"state\":\"consumed\",\"windowEndUtcMs\":\\d+", "\"state\":\"consumed\""), SaveV2Codes.Structure));

            byte[] lapsedBytes = LapsedTriggeredBonus(out RunCommandFixtures.Rig lapsed);
            list.Add(Loads("v3-bonus-lapsed-triggered", lapsed.Catalog, lapsedBytes));
            list.Add(Rejected("v3-unknown-member", cat, Rx(allBytes, "\"bonusOffers\":", "\"extra\":1,\"bonusOffers\":"), SaveV2Codes.Structure));

            // Content drift: bonus-always (a completion, a cooldown and a consumed bonus record) and the active production offer are gone.
            list.Add(Loads("v3-drift-removed-offer", Without(cat, BonusAlways, Produce), allBytes, 4));
            list.Add(Loads("v3-drift-objective-changed", Drift(auto.Catalog, o => o.Id == Deliver ? DeliverAsProduction() : o, 1), Write(auto), 1));
            list.Add(Rejected("v3-drift-unknown-machine", cat, Rx(allBytes, "\"machineId\":\"machine.miner\"", "\"machineId\":\"machine.removed\""), SaveV2Codes.Unresolved));
            list.Add(Rejected("v4-version", cat, Rx(allBytes, "\"schemaVersion\":3", "\"schemaVersion\":4"), SaveV2Codes.Version));

            // Delegated #399 follow-ups: list order without a duplicate (each list is strictly ascending by offer ID).
            const string TwoEntries = "(\\{[^}]*\\}),(\\{[^}]*\\})";
            list.Add(Rejected("v3-completions-unsorted", cat, Rx(allBytes, "(\"completions\":\\[)" + TwoEntries, "$1$3,$2"), SaveV2Codes.Invariant));
            list.Add(Rejected("v3-cooldowns-unsorted", cat, Rx(allBytes, "(\"contractCooldowns\":\\[)" + TwoEntries, "$1$3,$2"), SaveV2Codes.Invariant));
            list.Add(Rejected("v3-bonus-unsorted", cat, Rx(allBytes, "(\"bonusOffers\":\\[)" + TwoEntries, "$1$3,$2"), SaveV2Codes.Invariant));

            // The future-instant bound relative to the departure: at the bound it loads, one millisecond beyond it is rejected.
            long readyAtBound = departure + CooldownMs + RunLimits.MaxTimedDurationMs;
            string readyAt = "(\"offerId\":\"" + Regex.Escape(Deliver) + "\",\"readyAtUtcMs\":)\\d+";
            list.Add(Loads("v3-cooldown-at-future-bound", cat, Rx(allBytes, readyAt, "${1}" + readyAtBound)));
            list.Add(Rejected("v3-cooldown-beyond-future-bound", cat, Rx(allBytes, readyAt, "${1}" + (readyAtBound + 1)), SaveV2Codes.CrossField));
            long windowBound = departure + BonusWindowMs + RunLimits.MaxTimedDurationMs;
            string windowEnd = "(\"offerId\":\"" + Regex.Escape(BonusOre) + "\",\"state\":\"triggered\",\"windowEndUtcMs\":)\\d+";
            list.Add(Loads("v3-bonus-window-at-future-bound", cat, Rx(allBytes, windowEnd, "${1}" + windowBound)));
            list.Add(Rejected("v3-bonus-window-beyond-future-bound", cat, Rx(allBytes, windowEnd, "${1}" + (windowBound + 1)), SaveV2Codes.CrossField));

            // An active bonus offer needs a triggered, not consumed bonusOffers record (V3 only).
            RunCommandFixtures.Rig bonus = ActiveBonus();
            byte[] bonusBytes = Write(bonus);
            list.Add(Loads("v3-active-bonus-with-open-record", bonus.Catalog, bonusBytes));
            list.Add(Rejected("v3-active-bonus-without-record", bonus.Catalog, Rx(bonusBytes, "\"bonusOffers\":\\[[^\\]]*\\]", "\"bonusOffers\":[]"), SaveV2Codes.Invariant));
            list.Add(Rejected("v3-active-bonus-consumed-record", bonus.Catalog,
                Rx(bonusBytes, "(\"offerId\":\"" + Regex.Escape(BonusAlways) + "\",\"state\":)\"triggered\"", "${1}\"consumed\""), SaveV2Codes.Invariant));
            return list;
        }

        /// <summary>A rig whose bonus-ore trigger fired at 2 s and whose window lapsed unaccepted, written after the lapse.</summary>
        public static byte[] LapsedTriggeredBonus(out RunCommandFixtures.Rig rig)
        {
            rig = MiningRig(null);
            rig.Advance(3 * Second);
            rig.Advance((BonusWindowMs + 1_000) * RunSession.TicksPerMs);
            Assert.That(Board(rig).BonusState(BonusOre).WindowEndUtcMs, Is.LessThan(rig.Session.TimelineUtcMs), "the window has lapsed");
            return Write(rig);
        }

        /// <summary>A settled rig with the always-triggered bonus offer accepted inside its window (one triggered bonusOffers record).</summary>
        public static RunCommandFixtures.Rig ActiveBonus()
        {
            RunCommandFixtures.Rig rig = Rig();
            Accept(rig, BonusAlways);
            Assert.That(Board(rig).BonusOffers().Select(b => b.OfferId + "/" + b.Consumed), Is.EqualTo(new[] { BonusAlways + "/False" }));
            return rig;
        }

        /// <summary>
        /// <paramref name="v3"/> rewritten as the Save V2 file of the same state: schemaVersion 2, without the V3 members. Only for a state whose
        /// V3-only members are the defaults or whose loss the caller wants (it is a text transformation, not the V2 writer).
        /// </summary>
        public static byte[] AsV2Text(byte[] v3)
        {
            byte[] bytes = Rx(v3, "\"schemaVersion\":3", "\"schemaVersion\":2");
            bytes = Bytes(new Regex(",\"autoDeliver\":(true|false),\"produced\":\\d+\\}", RegexOptions.CultureInvariant).Replace(Text(bytes), "}", 1));
            return Rx(bytes, ",\"runCompletionTotal\":\\d+,\"contractCooldowns\":\\[[^\\]]*\\],\"bonusOffers\":\\[[^\\]]*\\]", string.Empty);
        }
    }
}
