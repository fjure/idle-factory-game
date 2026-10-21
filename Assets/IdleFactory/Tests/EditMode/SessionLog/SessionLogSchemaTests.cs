using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.SessionLog
{
    /// <summary>
    /// T338 (SEC-1, FR-029, SC-008): the allowlist of the session log and a writer-output check. The allowlist is pinned twice: by reflection over the event
    /// structs (every property maps to exactly one record member, so a new field fails here until the writer and this table agree) and over the bytes the
    /// writer produces (every member is on the table; every string is an enum token, a content ID of the catalog, the session ID or the CRC). Reflection
    /// is used here only; product code never reflects.
    /// </summary>
    public sealed class SessionLogSchemaTests
    {
        // record type -> members besides t, v and crc (contracts/session-log.md, data-model "Session log")
        private static readonly Dictionary<string, string[]> Allowed = new Dictionary<string, string[]>
        {
            { "header", new[] { "schema", "contentRevision", "buildKind", "platformClass", "timeScale", "sessionId" } },
            { "sample", new[] { "playSeconds", "factoryValue", "cash", "machines", "storageLevel", "factoryLevel" } },
            { "milestone", new[] { "kind", "playSeconds" } },
            { "sale", new[] { "resourceId", "units", "cash", "manual" } },
            { "mining", new[] { "resourceId", "counted", "ignored", "blocked", "ore" } },
            { "contract", new[] { "event", "offerId", "index", "required", "reward", "ratioPpm" } },
            { "prestige", new[] { "factoryValue", "threshold", "ip", "secondsSinceEligible", "industryPurchases" } },
            { "offlineReturn", new[] { "absenceMs", "creditedMs", "capHit", "completions", "storeFullStallSeconds", "soldUnits", "cashDelta", "reclassifiedLiveGap" } },
            { "bottleneck", new[] { "resourceId", "missingInputSeconds", "storeFullSeconds" } },
            { "purchase", new[] { "kind", "targetId", "level", "cost", "secondsSinceAffordable" } },
            { "clock", new[] { "kind", "deltaMs", "reclassified" } },
            { "lifecycle", new[] { "event", "droppedEvents" } },
        };

        private static readonly Dictionary<string, Type> EventTypes = new Dictionary<string, Type>
        {
            { "header", typeof(RunHeaderEvent) }, { "sample", typeof(RunSampleEvent) }, { "milestone", typeof(RunMilestoneEvent) },
            { "sale", typeof(RunSaleEvent) }, { "mining", typeof(RunMiningEvent) }, { "contract", typeof(RunContractEvent) },
            { "prestige", typeof(RunPrestigeEvent) }, { "offlineReturn", typeof(RunOfflineReturnEvent) }, { "bottleneck", typeof(RunBottleneckEvent) },
            { "purchase", typeof(RunPurchaseEvent) }, { "clock", typeof(RunClockEvent) }, { "lifecycle", typeof(RunLifecycleEvent) },
        };

        private static string[] AllTokens()
        {
            var tokens = new List<string>();
            foreach (RunBuildKind v in Enum.GetValues(typeof(RunBuildKind))) tokens.Add(SessionLogTokens.BuildKind(v));
            foreach (RunPlatformClass v in Enum.GetValues(typeof(RunPlatformClass))) tokens.Add(SessionLogTokens.PlatformClass(v));
            foreach (RunMilestoneKind v in Enum.GetValues(typeof(RunMilestoneKind))) tokens.Add(SessionLogTokens.Milestone(v));
            foreach (RunContractEventKind v in Enum.GetValues(typeof(RunContractEventKind))) tokens.Add(SessionLogTokens.Contract(v));
            foreach (RunPurchaseKind v in Enum.GetValues(typeof(RunPurchaseKind))) tokens.Add(SessionLogTokens.Purchase(v));
            foreach (RunClockKind v in Enum.GetValues(typeof(RunClockKind))) tokens.Add(SessionLogTokens.Clock(v));
            foreach (RunLifecycleKind v in Enum.GetValues(typeof(RunLifecycleKind))) tokens.Add(SessionLogTokens.Lifecycle(v));
            return tokens.ToArray();
        }

        /// <summary>Raises every record type, with every enum value, through the sink.</summary>
        private static void RaiseEverything(IRunEventSink sink)
        {
            sink.OnHeader(new RunHeaderEvent(1, 3, RunBuildKind.Development, RunPlatformClass.Mobile, 60, new RunSessionId(0x0102030405060708UL, 0x090A0B0C0D0E0F10UL)));
            sink.OnSample(new RunSampleEvent(600, 12345, 678, new[] { new RunMachineCount("machine.miner", 2, 3), new RunMachineCount("machine.smelter", 1, 1) }, 2, 3));
            foreach (RunMilestoneKind kind in Enum.GetValues(typeof(RunMilestoneKind))) sink.OnMilestone(new RunMilestoneEvent(kind, 99));
            sink.OnSale(new RunSaleEvent("iron.ore", 3, 9, true));
            sink.OnSale(new RunSaleEvent("iron.bar", 2, 6, false));
            sink.OnMining(new RunMiningEvent("iron.ore", 4, 1, 2, 3));
            foreach (RunContractEventKind kind in Enum.GetValues(typeof(RunContractEventKind)))
                sink.OnContract(new RunContractEvent(kind, "contract.deliver", 1, 5, kind == RunContractEventKind.Blocked ? 0 : 7, 1_500_000));
            sink.OnPrestige(new RunPrestigeEvent(1000, 900, 4, 120, new[] { new RunIndustryLevel("industry.cash", 2), new RunIndustryLevel("industry.speed", 1) }));
            sink.OnOfflineReturn(new RunOfflineReturnEvent(60_000, 48_000, true, 9, 0, 4, 12, true));
            sink.OnBottleneck(new RunBottleneckEvent("iron.ore", 11, 22));
            foreach (RunPurchaseKind kind in Enum.GetValues(typeof(RunPurchaseKind)))
                sink.OnPurchase(new RunPurchaseEvent(kind, kind == RunPurchaseKind.Storage ? "storage" : kind == RunPurchaseKind.Factory ? "factory" : "machine.miner", 2, 30, 5));
            foreach (RunClockKind kind in Enum.GetValues(typeof(RunClockKind))) sink.OnClock(new RunClockEvent(kind, kind == RunClockKind.Backward ? -5_000 : 5_000, false));
            foreach (RunLifecycleKind kind in Enum.GetValues(typeof(RunLifecycleKind))) sink.OnLifecycle(new RunLifecycleEvent(kind, 0));
        }

        private static List<Dictionary<string, object>> WriteEverything(out Harness harness)
        {
            harness = Harness.Create();
            harness.Writer.Start();
            RaiseEverything(harness.Writer);
            harness.Flush();
            return harness.AllRecords();
        }

        [Test]
        public void EveryRecordTypeCarriesExactlyItsAllowlistedMembers()
        {
            List<Dictionary<string, object>> records = WriteEverything(out Harness h);

            foreach (string type in Allowed.Keys)
                Assert.That(Harness.OfType(records, type), Is.Not.Empty, "record type " + type + " was written");

            foreach (Dictionary<string, object> record in records)
            {
                string type = (string)record["t"];
                Assert.That(Allowed.ContainsKey(type), Is.True, "unknown record type " + type);
                var expected = new HashSet<string>(Allowed[type]) { "t", "v", "crc" };
                Assert.That(new HashSet<string>(record.Keys), Is.EquivalentTo(expected), "members of " + type);
                Assert.That(record["v"], Is.EqualTo(1L), "schema version");
            }

            foreach (Dictionary<string, object> sample in Harness.OfType(records, "sample"))
                foreach (Dictionary<string, object> machine in ((List<object>)sample["machines"]).Cast<Dictionary<string, object>>())
                    Assert.That(new HashSet<string>(machine.Keys), Is.EquivalentTo(new[] { "machineId", "level", "count" }));
            foreach (Dictionary<string, object> prestige in Harness.OfType(records, "prestige"))
                foreach (Dictionary<string, object> purchase in ((List<object>)prestige["industryPurchases"]).Cast<Dictionary<string, object>>())
                    Assert.That(new HashSet<string>(purchase.Keys), Is.EquivalentTo(new[] { "industryId", "level" }));
        }

        [Test]
        public void EveryEventPropertyMapsToExactlyOneRecordMember()
        {
            foreach (KeyValuePair<string, Type> pair in EventTypes)
            {
                PropertyInfo[] properties = pair.Value.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                string[] members = Allowed[pair.Key];
                Assert.That(properties.Length, Is.EqualTo(members.Length), pair.Value.Name + ": a new event field needs a writer member and a row here (SEC-1)");
                foreach (PropertyInfo property in properties)
                {
                    string camel = char.ToLowerInvariant(property.Name[0]) + property.Name.Substring(1);
                    // The enum "Kind" is the table's "kind", or "event" for the Contract and Lifecycle rows.
                    bool mapped = members.Contains(camel) || (property.Name == "Kind" && members.Contains("event"));
                    Assert.That(mapped, Is.True, pair.Value.Name + "." + property.Name + " has no record member");
                }
            }
        }

        [Test]
        public void EveryWrittenStringIsAnEnumTokenAContentIdTheSessionIdOrTheCrc()
        {
            List<Dictionary<string, object>> records = WriteEverything(out Harness h);
            var tokens = new HashSet<string>(AllTokens());
            var ids = new HashSet<string>(Harness.CatalogIds);
            var enumMembers = new HashSet<string> { "kind", "event", "buildKind", "platformClass" };
            int checkedStrings = 0;

            void Visit(string member, object value)
            {
                if (value is Dictionary<string, object> map)
                {
                    foreach (KeyValuePair<string, object> pair in map) Visit(pair.Key, pair.Value);
                }
                else if (value is List<object> list)
                {
                    foreach (object element in list) Visit(member, element);
                }
                else if (value is string text)
                {
                    checkedStrings++;
                    if (member == "t") Assert.That(Allowed.ContainsKey(text), Is.True, "record type " + text);
                    else if (member == "crc") Assert.That(text, Does.Match("^[0-9a-f]{8}$"));
                    else if (member == "sessionId") Assert.That(text, Does.Match("^[0-9a-f]{32}$"));
                    else if (enumMembers.Contains(member)) Assert.That(tokens.Contains(text), Is.True, member + " = " + text + " is not an enum token");
                    else if (member.EndsWith("Id", StringComparison.Ordinal))
                    {
                        Assert.That(SessionLogIds.IsValid(text), Is.True, member + " = " + text + " fails the strict ID pattern");
                        Assert.That(ids.Contains(text), Is.True, member + " = " + text + " is not a catalog ID");
                    }
                    else Assert.Fail("unexpected string member " + member + " = " + text);
                }
                else
                {
                    Assert.That(value is long || value is bool, Is.True, member + " is neither a number, a bool nor an allowed string");
                }
            }

            foreach (Dictionary<string, object> record in records) Visit("(record)", record);
            Assert.That(checkedStrings, Is.GreaterThan(60));
        }

        [Test]
        public void EnumTokensAreCompleteDistinctAndCamelCase()
        {
            string[] tokens = AllTokens();
            Assert.That(tokens, Has.None.Null);
            foreach (string token in tokens) Assert.That(token, Does.Match("^[a-z][A-Za-z0-9]*$"));

            // within one enum the tokens are distinct
            Assert.That(Enum.GetValues(typeof(RunContractEventKind)).Cast<RunContractEventKind>().Select(SessionLogTokens.Contract).Distinct().Count(),
                Is.EqualTo(Enum.GetValues(typeof(RunContractEventKind)).Length));
            Assert.That(SessionLogTokens.Contract(RunContractEventKind.Blocked), Is.EqualTo("blocked"), "#392: the Blocked kind is mapped");
            Assert.That(new[] { "accept", "deliver", "autoDeliver", "produceComplete", "expire", "abandon", "blocked" },
                Is.EqualTo(Enum.GetValues(typeof(RunContractEventKind)).Cast<RunContractEventKind>().Select(SessionLogTokens.Contract).ToArray()));
            Assert.That(new[] { "start", "quit", "focusLossCheckpoint", "logToggled", "cleared" },
                Is.EqualTo(Enum.GetValues(typeof(RunLifecycleKind)).Cast<RunLifecycleKind>().Select(SessionLogTokens.Lifecycle).ToArray()));
            Assert.That(SessionLogTokens.Contract((RunContractEventKind)99), Is.Null);
            Assert.That(SessionLogTokens.Milestone((RunMilestoneKind)99), Is.Null);
        }

        [Test]
        public void AnUndefinedEnumValueDropsTheRecordAndCountsIt()
        {
            Harness h = Harness.Create();
            h.Writer.OnContract(new RunContractEvent((RunContractEventKind)99, "contract.deliver", 0, 1, 1, 1));
            h.Writer.OnMilestone(new RunMilestoneEvent((RunMilestoneKind)99, 1));
            h.Writer.OnPurchase(new RunPurchaseEvent((RunPurchaseKind)99, "machine.miner", 1, 1, 1));
            h.Writer.OnClock(new RunClockEvent((RunClockKind)99, 1, false));
            h.Flush();

            Assert.That(h.Writer.DroppedEvents, Is.EqualTo(4));
            Assert.That(h.Files.Has(h.Segment(0)), Is.False, "nothing was written");
        }

        public static IEnumerable<string> RejectedIds()
        {
            yield return "3f2504e0-4f89-11d3-9a0c-0305e82c3301";   // GUID, D form
            yield return "3f2504e04f8911d39a0c0305e82c3301";       // GUID, N form
            yield return "Iron.Ore";                                // upper case
            yield return "iron ore";                                // free text
            yield return "C:\\Users\\player\\save.json";            // a path
            yield return "/data/user/0/com.example/files";          // a path
            yield return "Object reference not set";                // an exception message
            yield return "";
            yield return null;
            yield return new string('a', SessionLogIds.MaxLength + 1);
            yield return "iron.ore\n";
            yield return "a\"b";
            yield return "caf\u00e9";
            yield return "not.in.the.catalog";                      // matches the pattern, but the catalog does not know it
        }

        [Test]
        public void AnIdOutsideTheCatalogOrThePatternIsDroppedAndCounted([ValueSource(nameof(RejectedIds))] string id)
        {
            Harness h = Harness.Create();
            h.Writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, id, 0, 1, 1, 1));
            h.Writer.OnSale(new RunSaleEvent(id, 1, 1, true));
            h.Writer.OnMining(new RunMiningEvent(id, 1, 0, 0, 1));
            h.Writer.OnBottleneck(new RunBottleneckEvent(id, 1, 1));
            h.Writer.OnPurchase(new RunPurchaseEvent(RunPurchaseKind.Build, id, 1, 1, 1));
            h.Writer.OnSample(new RunSampleEvent(1, 1, 1, new[] { new RunMachineCount(id, 1, 1) }, 1, 1));
            h.Writer.OnPrestige(new RunPrestigeEvent(1, 1, 1, 1, new[] { new RunIndustryLevel(id, 1) }));
            h.Flush();

            Assert.That(h.Writer.DroppedEvents, Is.EqualTo(7), "each of the seven records is dropped and counted");
            Assert.That(h.Files.Has(h.Segment(0)), Is.False, "nothing was written");
        }

        [Test]
        public void WithoutACatalogTheStrictPatternStillRejectsGuidsAndPaths()
        {
            Harness h = Harness.Create(withIds: false);
            h.Writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, "anything.valid", 0, 1, 1, 1));
            h.Writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, Guid.NewGuid().ToString("D"), 0, 1, 1, 1));
            h.Writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, Guid.NewGuid().ToString("N"), 0, 1, 1, 1));
            h.Writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, "C:\\save", 0, 1, 1, 1));
            h.Flush();

            Assert.That(h.Writer.DroppedEvents, Is.EqualTo(3));
            Assert.That(Harness.OfType(h.AllRecords(), "contract"), Has.Count.EqualTo(1));
        }

        [Test]
        public void TheIdPatternAcceptsEveryShippedStyleIdAndNoGuid()
        {
            foreach (string id in new[] { "iron.ore", "machine.miner", "F003-RES".ToLowerInvariant(), "a", "0", "contract.produce-short", "item_boost" })
                Assert.That(SessionLogIds.IsValid(id), Is.True, id);
            Assert.That(SessionLogIds.IsValid(Guid.Empty.ToString("D")), Is.False);
            Assert.That(SessionLogIds.LooksLikeGuid(Guid.NewGuid().ToString("N")), Is.True);
        }

        [Test]
        public void TheLineCarriesNoFreeTextNoPathAndNoExceptionMessage()
        {
            Harness h = Harness.Create();
            h.Writer.Start();
            RaiseEverything(h.Writer);
            h.Flush();
            string text = System.Text.Encoding.ASCII.GetString(h.Files.Bytes(h.Segment(0)));

            Assert.That(text, Does.Not.Contain(h.Root), "no directory path");
            Assert.That(text, Does.Not.Contain("\\"), "no escape or path separator");
            if (Environment.UserName.Length >= 6) Assert.That(text, Does.Not.Contain(Environment.UserName), "no user name");
            Assert.That(text, Does.Not.Contain("Exception"));
        }
    }
}
