using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using IdleFactory.Core.Numerics;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// Keeps the version-2 reader constants, the documentation-only schema
    /// (Assets/IdleFactory/Content/Schemas/gameplay-content-v2.schema.json), the author guide
    /// (docs/content/json-content-pipeline.md, v2 section) and the contract in agreement (T096, issue #178).
    /// The schema is not a validator; these tests tie its members, vocabularies, limits and bounds to the code that
    /// enforces them.
    /// </summary>
    public sealed class RunContentSchemaConsistencyTests
    {
        private const string GuideV2Marker = "# Gameplay Content Authoring Guide (JSON, v2)";

        private static readonly Regex CodePattern = new Regex(@"^[a-z][a-z0-9-]*(\.[a-z0-9-]+)+$", RegexOptions.CultureInvariant);

        private static JObject Schema() =>
            JObject.Parse(File.ReadAllText(Path.Combine(
                RunContentExample.RepositoryRoot(),
                "Assets",
                "IdleFactory",
                "Content",
                "Schemas",
                "gameplay-content-v2.schema.json")));

        private static JObject Defs() => (JObject)Schema()["$defs"];

        private static string GuideV2()
        {
            var text = RunContentExample.GuideText();
            var start = text.IndexOf(GuideV2Marker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "The guide has no v2 section.");
            return text.Substring(start);
        }

        private static string ContractText() =>
            File.ReadAllText(Path.Combine(
                RunContentExample.RepositoryRoot(),
                "specs",
                "003-first-factory-prestige",
                "contracts",
                "gameplay-content-v2.md"));

        private static string[] ReaderStrings(string fieldName)
        {
            var field = typeof(RunContentReader).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, "Reader constant '" + fieldName + "' was not found.");
            return (string[])field.GetValue(null);
        }

        private static string[] Required(JToken definition) => definition["required"].Select(t => (string)t).ToArray();

        private static string[] PropertyNames(JToken definition) => ((JObject)definition["properties"]).Properties().Select(p => p.Name).ToArray();

        private static IEnumerable<JToken> Branches(JToken definition) => definition["oneOf"];

        private static string[] BranchKinds(JToken definition) =>
            Branches(definition).Select(b => (string)b["properties"]["kind"]["const"]).ToArray();

        private static long Long(JToken token) => (long)token;

        // ---- members ---------------------------------------------------------------------------------------------

        [Test]
        public void SchemaRequiredMembersMatchTheReaderConstants()
        {
            var schema = Schema();
            var defs = Defs();

            CollectionAssert.AreEqual(ReaderStrings("RequiredDocumentMembers"), Required(schema));
            CollectionAssert.AreEquivalent(Required(schema), PropertyNames(schema), "every top-level member is required");
            Assert.That((bool)schema["additionalProperties"], Is.False);
            Assert.That((int)schema["properties"]["contentSchemaVersion"]["const"], Is.EqualTo(RunContentReader.SupportedContentSchemaVersion));

            var pairs = new Dictionary<string, JToken>
            {
                ["RequiredRunStartMembers"] = defs["runStart"],
                ["RequiredResourceMembers"] = defs["resource"],
                ["RequiredRecipeMembers"] = defs["recipe"],
                ["RequiredAmountMembers"] = defs["amountEntry"],
                ["RequiredMachineMembers"] = defs["machine"],
                ["RequiredMilestoneMembers"] = defs["milestone"],
                ["RequiredCurveMembers"] = defs["curve"],
                ["RequiredStorageLevelMembers"] = defs["storageLevel"],
                ["RequiredFactoryLevelMembers"] = defs["factoryLevel"],
                ["RequiredContractMembers"] = defs["contract"],
                ["RequiredItemMembers"] = defs["item"],
                ["RequiredResearchMembers"] = defs["research"],
                ["RequiredPrerequisiteMembers"] = defs["prerequisite"],
                ["RequiredOverdriveMembers"] = defs["overdrive"],
                ["RequiredPrestigeMembers"] = defs["prestige"],
                ["RequiredIpCurveMembers"] = defs["prestige"]["properties"]["ipCurve"],
                ["RequiredIndustryUpgradeMembers"] = defs["industryUpgrade"],
                ["RequiredPolicyMembers"] = defs["offlinePolicy"],
            };
            foreach (var pair in pairs)
            {
                CollectionAssert.AreEqual(ReaderStrings(pair.Key), Required(pair.Value), "Schema list differs from reader constant " + pair.Key);
                Assert.That((bool)pair.Value["additionalProperties"], Is.False, pair.Key);
                Assert.That(PropertyNames(pair.Value), Is.SupersetOf(Required(pair.Value)), pair.Key);
            }

            // The only optional member of the document is a contract's durationMs (timedBar only).
            var contract = defs["contract"];
            CollectionAssert.AreEquivalent(new[] { "durationMs" }, PropertyNames(contract).Except(Required(contract)));
        }

        [Test]
        public void SchemaVocabulariesMatchTheReader()
        {
            var defs = Defs();

            CollectionAssert.AreEquivalent(ReaderStrings("Roundings"), defs["curve"]["properties"]["rounding"]["enum"].Select(t => (string)t));
            CollectionAssert.AreEquivalent(ReaderStrings("ContractKinds"), defs["contract"]["properties"]["kind"]["enum"].Select(t => (string)t));
            CollectionAssert.AreEquivalent(ReaderStrings("UnlockKinds"), BranchKinds(defs["unlock"]));
            CollectionAssert.AreEquivalent(ReaderStrings("RewardKinds"), BranchKinds(defs["contractReward"]));
            CollectionAssert.AreEquivalent(ReaderStrings("ItemTargetKinds"), BranchKinds(defs["itemTarget"]));
            CollectionAssert.AreEquivalent(ReaderStrings("EffectTargetKinds"), BranchKinds(defs["effectTarget"]));
            CollectionAssert.AreEquivalent(ReaderStrings("ItemEffectKinds"), BranchKinds(defs["itemEffect"]));
            CollectionAssert.AreEquivalent(ReaderStrings("ResearchEffectKinds"), BranchKinds(defs["researchEffect"]));
            CollectionAssert.AreEquivalent(ReaderStrings("IndustryEffectKinds"), BranchKinds(defs["industryEffect"]));
            CollectionAssert.AreEquivalent(new[] { "sqrt" }, new[] { (string)defs["prestige"]["properties"]["ipCurve"]["properties"]["kind"]["const"] });
        }

        [Test]
        public void EveryKindShapeInTheSchemaIsAcceptedByTheReader()
        {
            var defs = Defs();
            AssertBranchesAccepted(defs["unlock"], d => d["resources"][0]["unlock"]);
            AssertBranchesAccepted(defs["contractReward"], d => d["contracts"][0]["rewards"][0]);
            AssertBranchesAccepted(defs["itemTarget"], d => d["items"][0]["target"]);
            AssertBranchesAccepted(defs["itemEffect"], d => d["items"][0]["effect"]);
            AssertBranchesAccepted(defs["researchEffect"], d => d["research"][0]["effect"]);
            AssertBranchesAccepted(defs["industryEffect"], d => d["industryUpgrades"][0]["effect"]);
            AssertBranchesAccepted(defs["effectTarget"], d => d["research"][0]["effect"]["target"]);
        }

        [Test]
        public void ReaderRejectsMembersOutsideEveryKindShapeInTheSchema()
        {
            var defs = Defs();
            AssertBranchesRejectExtraMember(defs["unlock"], d => d["resources"][0]["unlock"]);
            AssertBranchesRejectExtraMember(defs["itemEffect"], d => d["items"][0]["effect"]);
            AssertBranchesRejectExtraMember(defs["researchEffect"], d => d["research"][0]["effect"]);
            AssertBranchesRejectExtraMember(defs["industryEffect"], d => d["industryUpgrades"][0]["effect"]);
            AssertBranchesRejectExtraMember(defs["itemTarget"], d => d["items"][0]["target"]);
            AssertBranchesRejectExtraMember(defs["effectTarget"], d => d["research"][0]["effect"]["target"]);
            AssertBranchesRejectExtraMember(defs["contractReward"], d => d["contracts"][0]["rewards"][0]);
            var expansion = Branches(defs["effectTarget"]).Single(b => (string)b["properties"]["kind"]["const"] == "expansion");
            CollectionAssert.AreEqual(new[] { "kind" }, PropertyNames(expansion), "an expansion target has no id");
            CollectionAssert.AreEqual(new[] { "kind" }, Required(expansion));
        }

        private static void AssertBranchesAccepted(JToken definition, Func<JObject, JToken> slot)
        {
            foreach (var branch in Branches(definition))
            {
                var document = RunContentExample.Valid();
                slot(document).Replace(Instance(branch));
                var result = RunContentReader.Read(RunContentExample.Bytes(document));
                Assert.That(
                    result.IsSuccess,
                    Is.True,
                    "Schema shape " + Instance(branch).ToString(Formatting.None) + " was rejected: " +
                    (result.Diagnostic == null ? string.Empty : result.Diagnostic.Code + " " + result.Diagnostic.Path));
            }
        }

        private static void AssertBranchesRejectExtraMember(JToken definition, Func<JObject, JToken> slot)
        {
            foreach (var branch in Branches(definition))
            {
                var document = RunContentExample.Valid();
                var instance = Instance(branch);
                instance["notInSchema"] = 1;
                slot(document).Replace(instance);
                var result = RunContentReader.Read(RunContentExample.Bytes(document));
                Assert.That(result.IsSuccess, Is.False, "An extra member was accepted for " + instance.ToString(Formatting.None));
                Assert.That(result.Diagnostic.Code, Is.EqualTo("member.unknown"));
            }
        }

        /// <summary>A minimal instance of a oneOf branch: every required member with a schema-shaped value.</summary>
        private static JObject Instance(JToken branch)
        {
            var instance = new JObject();
            foreach (var name in Required(branch))
            {
                var property = branch["properties"][name];
                if (property["const"] != null)
                {
                    instance[name] = property["const"];
                    continue;
                }

                switch (name)
                {
                    case "id":
                    case "itemId":
                        instance[name] = "x.y";
                        break;
                    case "target":
                        instance[name] = new JObject { ["kind"] = "allMachines" };
                        break;
                    case "allowedMachineIds":
                        instance[name] = new JArray("machine.miner");
                        break;
                    case "onCompletionIndices":
                        instance[name] = new JArray(0);
                        break;
                    case "curve":
                        instance[name] = new JObject { ["base"] = 1, ["growthPpm"] = 1_000_000, ["rounding"] = "ceil" };
                        break;
                    default:
                        instance[name] = 5;
                        break;
                }
            }

            return instance;
        }

        // ---- limits and bounds ----------------------------------------------------------------------------------

        [Test]
        public void SchemaLimitsMatchTheReaderLimits()
        {
            var schema = Schema();
            var properties = (JObject)schema["properties"];
            var defs = Defs();
            var nonEmpty = ReaderStrings("NonEmptyFamilies");
            var families = new[]
            {
                "resources", "recipes", "machines", "storageLevels", "factoryLevels", "contracts", "items", "research",
                "industryUpgrades", "offlinePolicies",
            };

            foreach (var family in families)
            {
                var member = properties[family];
                var maxItems = (int)member["maxItems"];
                if (family == "offlinePolicies")
                {
                    Assert.That(maxItems, Is.LessThanOrEqualTo(GameplayContentLimits.MaximumTopLevelEntryCount), "the policy list is stricter than the family limit");
                }
                else
                {
                    Assert.That(maxItems, Is.EqualTo(GameplayContentLimits.MaximumTopLevelEntryCount), family);
                }

                Assert.That((int)member["minItems"], Is.EqualTo(nonEmpty.Contains(family) ? 1 : 0), family);
            }

            foreach (var name in new[] { "id", "label", "presentationKey" })
            {
                Assert.That((int)defs[name]["maxLength"], Is.EqualTo(GameplayContentLimits.MaximumIdentifierLength), name);
                Assert.That((int)defs[name]["minLength"], Is.EqualTo(1), name);
            }

            var nested = new[]
            {
                defs["runStart"]["properties"]["initialUnlocks"],
                defs["recipe"]["properties"]["inputs"],
                defs["recipe"]["properties"]["outputs"],
                defs["machine"]["properties"]["milestones"],
                defs["contract"]["properties"]["rewards"],
                defs["research"]["properties"]["prerequisites"],
                Branches(defs["itemTarget"]).Last()["properties"]["allowedMachineIds"],
                Branches(defs["contractReward"]).Last()["properties"]["onCompletionIndices"],
            };
            foreach (var list in nested)
            {
                Assert.That((int)list["maxItems"], Is.EqualTo(GameplayContentLimits.MaximumNestedEntryCount));
            }

            Assert.That(Long(defs["amount"]["maximum"]), Is.EqualTo(ExactBound.Value));
            Assert.That(Long(defs["positiveAmount"]["maximum"]), Is.EqualTo(ExactBound.Value));
            Assert.That(Long(defs["amount"]["minimum"]), Is.EqualTo(0));
            Assert.That(Long(defs["positiveAmount"]["minimum"]), Is.EqualTo(1));
        }

        [Test]
        public void SchemaNumericBoundsMatchTheValidatorBehavior()
        {
            var defs = Defs();
            var runStart = defs["runStart"]["properties"];

            var refundMaximum = Long(runStart["removalRefundPpm"]["maximum"]);
            Assert.That(ValidatorCodes(d => d["runStart"]["removalRefundPpm"] = refundMaximum), Is.Empty);
            Assert.That(ValidatorCodes(d => d["runStart"]["removalRefundPpm"] = refundMaximum + 1), Does.Contain("runstart.refund.out-of-range"));

            var minCycleMinimum = Long(runStart["minCycleTicks"]["minimum"]);
            Assert.That(ValidatorCodes(d => d["runStart"]["minCycleTicks"] = minCycleMinimum), Is.Empty);
            Assert.That(ValidatorCodes(d => d["runStart"]["minCycleTicks"] = minCycleMinimum - 1), Does.Contain("runstart.min-cycle.invalid"));
            Assert.That(
                Long(runStart["minCycleTicks"]["maximum"]),
                Is.EqualTo(Long(defs["recipe"]["properties"]["durationTicks"]["maximum"])));

            var speedBound = runStart["maxCombinedSpeedPpm"];
            Assert.That(Long(speedBound["minimum"]), Is.EqualTo(1_000_000));
            Assert.That(Long(speedBound["maximum"]), Is.EqualTo(1_000_000_000_000L));
            Assert.That(ValidatorCodes(d => d["runStart"]["maxCombinedSpeedPpm"] = Long(speedBound["minimum"]) - 1), Does.Contain("runstart.speed-bound.invalid"));
            Assert.That(ValidatorCodes(d => d["runStart"]["maxCombinedSpeedPpm"] = Long(speedBound["maximum"]) + 1), Does.Contain("runstart.speed-bound.invalid"));

            Assert.That(Long(runStart["maxCombinedOutputFactor"]["minimum"]), Is.EqualTo(2));
            Assert.That(ValidatorCodes(d => d["runStart"]["maxCombinedOutputFactor"] = 1), Does.Contain("runstart.output-bound.invalid"));
            Assert.That(Long(runStart["maxRewardRatioPpm"]["minimum"]), Is.EqualTo(1_000_000));
            Assert.That(ValidatorCodes(d => d["runStart"]["maxRewardRatioPpm"] = 999_999), Does.Contain("runstart.reward-ratio.invalid"));

            var durationMaximum = Long(defs["recipe"]["properties"]["durationTicks"]["maximum"]);
            Assert.That(ValidatorCodes(d => d["recipes"][0]["durationTicks"] = durationMaximum), Is.Empty);
            Assert.That(ValidatorCodes(d => d["recipes"][0]["durationTicks"] = durationMaximum + 1), Does.Contain("recipe.duration.out-of-range"));
            Assert.That(Long(defs["recipe"]["properties"]["durationTicks"]["minimum"]), Is.EqualTo(1));

            var slots = defs["factoryLevel"]["properties"]["slots"];
            Assert.That(ValidatorCodes(d => d["factoryLevels"][3]["slots"] = Long(slots["maximum"])), Is.Empty);
            Assert.That(ValidatorCodes(d => d["factoryLevels"][3]["slots"] = Long(slots["maximum"]) + 1), Does.Contain("factory.slots.too-many"));

            var tier = defs["item"]["properties"]["tier"];
            Assert.That(ValidatorCodes(d => d["items"][0]["tier"] = Long(tier["maximum"]) + 1), Does.Contain("item.tier.invalid"));
            Assert.That(ValidatorCodes(d => d["items"][0]["tier"] = Long(tier["minimum"]) - 1), Does.Contain("item.tier.invalid"));

            Assert.That(Long(defs["curve"]["properties"]["growthPpm"]["minimum"]), Is.EqualTo(1_000_000));
            Assert.That(ValidatorCodes(d => d["machines"][1]["cost"]["growthPpm"] = Long(defs["curve"]["properties"]["growthPpm"]["minimum"]) - 1), Does.Contain("curve.growth.invalid"));
            Assert.That(Long(defs["speedMul"]["minimum"]), Is.EqualTo(1_000_001));
            Assert.That(Long(defs["researchEffect"]["oneOf"][3]["properties"]["valuePerLevelPpm"]["minimum"]), Is.EqualTo(100_000));
            Assert.That(Long(defs["researchEffect"]["oneOf"][3]["properties"]["valuePerLevelPpm"]["maximum"]), Is.EqualTo(999_999));
            Assert.That((int)defs["milestone"]["properties"]["level"]["minimum"], Is.EqualTo(2));
            Assert.That(ValidatorCodes(d => d["machines"][1]["milestones"][0]["level"] = 1), Does.Contain("machine.milestone.level-invalid"));
        }

        [Test]
        public void SchemaPinsTheOfflinePolicyTheValidatorRequires()
        {
            var policy = Defs()["offlinePolicy"]["properties"];

            Assert.That((int)policy["revision"]["const"], Is.EqualTo(1));
            Assert.That((int)policy["efficiencyNumerator"]["const"], Is.EqualTo(4));
            Assert.That((int)policy["efficiencyDenominator"]["const"], Is.EqualTo(5));
            Assert.That(Long(policy["maxCreditedTicks"]["const"]), Is.EqualTo(288_000_000_000L));
            Assert.That(ValidatorCodes(d => { }), Is.Empty, "the pinned values are the ones the example uses and the validator accepts");
            Assert.That(ValidatorCodes(d => d["offlinePolicies"][0]["maxCreditedTicks"] = Long(policy["maxCreditedTicks"]["const"]) + 1), Does.Contain("policy.revision-1.required"));
        }

        private static IReadOnlyList<string> ValidatorCodes(Action<JObject> mutate)
        {
            var document = RunContentExample.Valid();
            mutate(document);
            var read = RunContentReader.Read(RunContentExample.Bytes(document));
            if (!read.IsSuccess)
            {
                return new[] { read.Diagnostic.Code };
            }

            return RunContentValidator.Validate(read.Document).Select(d => d.Code).ToList();
        }

        // ---- guide and contract --------------------------------------------------------------------------------

        [Test]
        public void GuideDocumentsEverySchemaPropertyAndRuleCode()
        {
            var guide = GuideV2();
            var schema = Schema();
            var names = new HashSet<string>(PropertyNames(schema), StringComparer.Ordinal);
            foreach (var definition in ((JObject)schema["$defs"]).Properties().Select(p => p.Value))
            {
                CollectPropertyNames(definition, names);
            }

            foreach (var name in names)
            {
                Assert.That(Regex.IsMatch(guide, @"\b" + Regex.Escape(name) + @"\b"), Is.True, "The v2 guide does not document schema property '" + name + "'.");
            }

            foreach (var code in RunContentValidator.RuleCodes)
            {
                Assert.That(guide, Does.Contain("`" + code + "`"), "The v2 guide is missing rule code '" + code + "'.");
            }

            Assert.That(guide, Does.Contain("up to 100"));
            Assert.That(guide, Does.Contain("1,024 entries per top-level family and 256 per nested list"));
            Assert.That(guide, Does.Contain("`RunContentReader`"));
        }

        [Test]
        public void ContractAndGuideCodeTablesOnlyNameCodesTheValidatorKnows()
        {
            var known = new HashSet<string>(RunContentValidator.RuleCodes, StringComparer.Ordinal);

            var guide = GuideV2();
            var guideTable = Section(guide, "## v2: semantic rule codes", "## v2: a complete, valid, annotated example");
            var contract = ContractText();
            var contractTable = Section(contract, "## Semantic rule codes", "## Evolution");

            foreach (var table in new[] { guideTable, contractTable })
            {
                var codes = Backticked(table).Where(t => CodePattern.IsMatch(t)).ToList();
                Assert.That(codes, Is.Not.Empty);
                foreach (var code in codes)
                {
                    Assert.That(known.Contains(code), Is.True, "Table names code '" + code + "' that the validator does not implement.");
                }
            }

            // Every contract-table code is implemented (the contract wins over the guide).
            var contractCodes = new HashSet<string>(Backticked(contractTable).Where(t => CodePattern.IsMatch(t)), StringComparer.Ordinal);
            foreach (var code in RunContentValidator.RuleCodes.Where(c => c != "content.version.unsupported"))
            {
                Assert.That(contractCodes.Contains(code), Is.True, "The contract table does not list '" + code + "'.");
            }

            foreach (var code in contractCodes)
            {
                Assert.That(RunContentValidator.RuleCodes.Contains(code), Is.True, "Contract code '" + code + "' is not implemented.");
            }
        }

        [Test]
        public void SourcesContainNoReflectionDeserialization()
        {
            var forbidden = new[] { "JsonConvert", "JsonSerializer", "DeserializeObject", "System.Reflection", "GetProperties", "Activator." };
            foreach (var file in new[] { "RunContentDtos.cs", "RunContentReader.cs", "RunContentValidator.cs" })
            {
                var text = File.ReadAllText(Path.Combine(RunContentExample.RepositoryRoot(), "Assets", "IdleFactory", "Unity", "Content", file));
                foreach (var symbol in forbidden)
                {
                    Assert.That(text, Does.Not.Contain(symbol), file + " must not use " + symbol);
                }
            }

            var reader = File.ReadAllText(Path.Combine(RunContentExample.RepositoryRoot(), "Assets", "IdleFactory", "Unity", "Content", "RunContentReader.cs"));
            Assert.That(reader, Does.Contain("JsonTextReader"));
        }

        private static void CollectPropertyNames(JToken definition, ISet<string> names)
        {
            if (definition is JObject obj)
            {
                if (obj["properties"] is JObject properties)
                {
                    foreach (var property in properties.Properties())
                    {
                        names.Add(property.Name);
                        CollectPropertyNames(property.Value, names);
                    }
                }

                if (obj["oneOf"] is JArray branches)
                {
                    foreach (var branch in branches)
                    {
                        CollectPropertyNames(branch, names);
                    }
                }
            }
        }

        private static string Section(string text, string startMarker, string endMarker)
        {
            var start = text.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "Missing section " + startMarker);
            var end = text.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), "Missing end marker " + endMarker);
            return text.Substring(start, end - start);
        }

        private static IEnumerable<string> Backticked(string text)
        {
            foreach (Match match in Regex.Matches(text, "`([^`]+)`"))
            {
                yield return match.Groups[1].Value;
            }
        }
    }
}
