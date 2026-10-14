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
    /// Keeps the version-3 reader constants, the documentation-only schema
    /// (Assets/IdleFactory/Content/Schemas/gameplay-content-v3.schema.json), the author guide
    /// (docs/content/json-content-pipeline.md, v3 section) and the contracts in agreement (T096, issue #178; ported by T313, issue #375).
    /// The schema is not a validator; these tests tie its members, vocabularies, limits and bounds to the code that
    /// enforces them.
    /// </summary>
    public sealed class RunContentSchemaConsistencyTests
    {
        private const string GuideV3Marker = "# Gameplay Content Authoring Guide (JSON, v3)";

        private static readonly Regex CodePattern = new Regex(@"^[a-z][a-z0-9-]*(\.[a-z0-9-]+)+$", RegexOptions.CultureInvariant);

        private static JObject Schema() =>
            JObject.Parse(File.ReadAllText(Path.Combine(
                RunContentExample.RepositoryRoot(),
                "Assets",
                "IdleFactory",
                "Content",
                "Schemas",
                "gameplay-content-v3.schema.json")));

        private static JObject Defs() => (JObject)Schema()["$defs"];

        private static string GuideV3()
        {
            var text = RunContentExample.GuideText();
            var start = text.IndexOf(GuideV3Marker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "The guide has no v3 section.");
            return text.Substring(start);
        }

        private static string ContractV2Text() =>
            File.ReadAllText(Path.Combine(
                RunContentExample.RepositoryRoot(),
                "specs",
                "003-first-factory-prestige",
                "contracts",
                "gameplay-content-v2.md"));

        private static string ContractV3Text() =>
            File.ReadAllText(Path.Combine(
                RunContentExample.RepositoryRoot(),
                "specs",
                "005-economy-contracts",
                "contracts",
                "gameplay-content-v3.md"));

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
                ["RequiredManualMiningMembers"] = defs["manualMining"],
                ["RequiredBuildPriceMembers"] = defs["buildPrice"],
                ["RequiredBonusMembers"] = defs["bonus"],
                ["RequiredContractItemLadderMembers"] = defs["contractItemLadder"],
                ["RequiredContractItemLadderEntryMembers"] = defs["contractItemLadderEntry"],
                ["RequiredThresholdMembers"] = defs["prestige"]["properties"]["threshold"],
                ["RequiredPrestigeMilestoneMembers"] = defs["prestigeMilestone"],
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

            // The only optional members of the document are a contract's durationMs and bonus.
            var contract = defs["contract"];
            CollectionAssert.AreEquivalent(new[] { "durationMs", "bonus" }, PropertyNames(contract).Except(Required(contract)));
        }

        [Test]
        public void SchemaVocabulariesMatchTheReader()
        {
            var defs = Defs();

            CollectionAssert.AreEquivalent(ReaderStrings("Roundings"), defs["curve"]["properties"]["rounding"]["enum"].Select(t => (string)t));
            CollectionAssert.AreEquivalent(ReaderStrings("ContractObjectives"), defs["contract"]["properties"]["objective"]["enum"].Select(t => (string)t));
            CollectionAssert.AreEquivalent(ReaderStrings("ContractClasses"), defs["contract"]["properties"]["class"]["enum"].Select(t => (string)t));
            CollectionAssert.AreEquivalent(ReaderStrings("UnlockKinds"), BranchKinds(defs["unlock"]));
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

            Assert.That((int)properties["milestones"]["maxItems"], Is.EqualTo(64), "the Core catalog carries at most 64 milestones");
            Assert.That((int)properties["milestones"]["minItems"], Is.EqualTo(0));
            Assert.That((int)defs["contractItemLadder"]["properties"]["entries"]["maxItems"], Is.EqualTo(GameplayContentLimits.MaximumTopLevelEntryCount));
            Assert.That((int)defs["runStart"]["properties"]["sellSteps"]["maxItems"], Is.EqualTo(65), "64 integers and the string all");

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
                defs["research"]["properties"]["prerequisites"],
                Branches(defs["itemTarget"]).Last()["properties"]["allowedMachineIds"],
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
            // Revision 2: the maximum duration is not a balanced chain, so chain.dominance (revision 3) is not under test here.
            Assert.That(ValidatorCodes(d => { d["contentRevision"] = 2; d["recipes"][0]["durationTicks"] = durationMaximum; }), Is.Empty);
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

            // Schema v3 members: the schema bounds are the ones the validator enforces.
            var mining = defs["manualMining"]["properties"];
            Assert.That(Long(mining["baseYieldMilli"]["minimum"]), Is.EqualTo(1));
            Assert.That(ValidatorCodes(d => d["runStart"]["manualMining"]["baseYieldMilli"] = Long(mining["baseYieldMilli"]["minimum"]) - 1), Does.Contain("value.out-of-range"));
            Assert.That(ValidatorCodes(d => d["runStart"]["manualMining"]["maxActivationsPerSecond"] = Long(mining["maxActivationsPerSecond"]["maximum"])), Is.Empty);
            Assert.That(ValidatorCodes(d => d["runStart"]["manualMining"]["maxActivationsPerSecond"] = Long(mining["maxActivationsPerSecond"]["maximum"]) + 1), Does.Contain("value.out-of-range"));
            Assert.That(Long(mining["maxActivationsPerSecond"]["minimum"]), Is.EqualTo(1));
            Assert.That(ValidatorCodes(d => d["runStart"]["manualMining"]["maxShareOfMinerPpm"] = Long(mining["maxShareOfMinerPpm"]["maximum"])), Is.Empty);
            Assert.That(ValidatorCodes(d => d["runStart"]["manualMining"]["maxShareOfMinerPpm"] = Long(mining["maxShareOfMinerPpm"]["maximum"]) + 1), Does.Contain("value.out-of-range"));
            Assert.That(Long(mining["referenceMinerLevel"]["minimum"]), Is.EqualTo(1));

            var build = defs["buildPrice"]["properties"];
            Assert.That(Long(build["ownedGrowthPpm"]["minimum"]), Is.EqualTo(1_000_000));
            Assert.That(ValidatorCodes(d => d["machines"][1]["buildPrice"]["ownedGrowthPpm"] = Long(build["ownedGrowthPpm"]["minimum"]) - 1), Does.Contain("value.out-of-range"));
            Assert.That(Long(build["prestigeGrowthPpm"]["minimum"]), Is.EqualTo(1_000_000));
            Assert.That(ValidatorCodes(d => d["machines"][1]["buildPrice"]["prestigeGrowthPpm"] = Long(build["prestigeGrowthPpm"]["minimum"]) - 1), Does.Contain("value.out-of-range"));

            var threshold = defs["prestige"]["properties"]["threshold"]["properties"];
            Assert.That(Long(threshold["growthPpm"]["minimum"]), Is.EqualTo(1_000_000));
            Assert.That(ValidatorCodes(d => d["prestige"]["threshold"]["growthPpm"] = Long(threshold["growthPpm"]["minimum"]) - 1), Does.Contain("value.out-of-range"));
            Assert.That(Long(threshold["maxIndex"]["minimum"]), Is.EqualTo(1));
            Assert.That(ValidatorCodes(d => d["prestige"]["threshold"]["maxIndex"] = Long(threshold["maxIndex"]["minimum"]) - 1), Does.Contain("value.out-of-range"));
            Assert.That(ValidatorCodes(d => d["prestige"]["threshold"]["base"] = 0), Does.Contain("prestige.threshold.invalid"));

            var decay = defs["industryUpgrade"]["properties"]["decayPpm"];
            Assert.That(ValidatorCodes(d => d["industryUpgrades"][0]["decayPpm"] = Long(decay["maximum"])), Is.Empty);
            Assert.That(ValidatorCodes(d => d["industryUpgrades"][0]["decayPpm"] = Long(decay["maximum"]) + 1), Does.Contain("value.out-of-range"));

            var duration = defs["contract"]["properties"]["durationMs"];
            Assert.That(ValidatorCodes(d => d["contracts"][2]["durationMs"] = Long(duration["maximum"])), Is.Empty);
            Assert.That(ValidatorCodes(d => d["contracts"][2]["durationMs"] = Long(duration["maximum"]) + 1), Does.Contain("contract.duration.out-of-range"));
            Assert.That(Long(duration["minimum"]), Is.EqualTo(1));
            Assert.That(Long(defs["bonus"]["properties"]["windowMs"]["minimum"]), Is.EqualTo(1));
            Assert.That(ValidatorCodes(d => d["contracts"][3]["bonus"]["windowMs"] = 0), Does.Contain("value.out-of-range"));
            Assert.That(Long(defs["contractItemLadderEntry"]["properties"]["index"]["maximum"]), Is.EqualTo(1023));
            Assert.That(ValidatorCodes(d => d["contractItemLadder"]["entries"][0]["index"] = 1024), Does.Contain("value.out-of-range"));
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
            var guide = GuideV3();
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

        /// <summary>Codes of the v2 contract table that v3 retires with the members they checked.</summary>
        private static readonly string[] RetiredByV3 =
        {
            "contract.kind.unknown", "contract.kind.resource-mismatch", "contract.duration.required", "contract.duration.forbidden",
            "contract.reward.empty", "contract.reward.kind.unknown", "contract.kinds.incomplete",
        };

        /// <summary>Codes of the v3 contract table that the story tasks implement later; the validator does not.</summary>
        private static readonly string[] DeferredToLaterTasks =
        {
            "contract.ratio.produce", "contract.quantity.capacity", "ladder.uptime",
        };

        /// <summary>The story rules that the manual-mining task (T358) implements. Each must be implemented, in the guide code table and in the rule table.</summary>
        private static readonly string[] AddedByT358 = { "start.reachable", "manual.yield.bound" };

        [Test]
        public void StoryRulesOfT358AreImplementedAndDocumented()
        {
            var known = new HashSet<string>(RunContentValidator.RuleCodes, StringComparer.Ordinal);
            var guide = GuideV3();
            var table = Section(guide, "## v3: semantic rule codes", "## v3: structural rules");
            var rules = Section(guide, "## v3: structural rules", "## v3: a complete, valid, annotated example");
            foreach (var code in AddedByT358)
            {
                Assert.That(known.Contains(code), Is.True, "T358 code '" + code + "' is not implemented.");
                Assert.That(DeferredToLaterTasks.Contains(code), Is.False, "T358 code '" + code + "' is still listed as deferred.");
                Assert.That(Backticked(table).Contains(code), Is.True, "The guide code table does not list '" + code + "'.");
                Assert.That(rules, Does.Contain("`" + code + "`"), "The rule table does not describe '" + code + "'.");
            }
        }

        /// <summary>The story rule that the sale-pricing task (T316) implements. It must be implemented, in the guide code table and in the rule table.</summary>
        private static readonly string[] AddedByT316 = { "chain.dominance" };

        [Test]
        public void StoryRulesOfT316AreImplementedAndDocumented()
        {
            var known = new HashSet<string>(RunContentValidator.RuleCodes, StringComparer.Ordinal);
            var guide = GuideV3();
            var table = Section(guide, "## v3: semantic rule codes", "## v3: structural rules");
            var rules = Section(guide, "## v3: structural rules", "## v3: a complete, valid, annotated example");
            foreach (var code in AddedByT316)
            {
                Assert.That(known.Contains(code), Is.True, "T316 code '" + code + "' is not implemented.");
                Assert.That(DeferredToLaterTasks.Contains(code), Is.False, "T316 code '" + code + "' is still listed as deferred.");
                Assert.That(Backticked(table).Contains(code), Is.True, "The guide code table does not list '" + code + "'.");
                Assert.That(rules, Does.Contain("`" + code + "`"), "The rule table does not describe '" + code + "'.");
            }
        }

        /// <summary>Codes of the v3 contract table that the structural-rules task (T314) implements. Each must be implemented and in the guide.</summary>
        private static readonly string[] AddedByT314 =
        {
            "sell.steps", "live-gap.range", "contract.bonus.fields", "contract.ratio.standing", "contract.ratio.bonus", "contract.quantity.cap",
            "contract.produce.min", "milestone.order", "build.base.bound", "cost.floor",
        };

        [Test]
        public void StructuralRulesOfT314AreImplementedAndDocumented()
        {
            var known = new HashSet<string>(RunContentValidator.RuleCodes, StringComparer.Ordinal);
            var guide = GuideV3();
            var table = Section(guide, "## v3: semantic rule codes", "## v3: structural rules");
            var structural = Section(guide, "## v3: structural rules", "## v3: a complete, valid, annotated example");
            foreach (var code in AddedByT314)
            {
                Assert.That(known.Contains(code), Is.True, "T314 code '" + code + "' is not implemented.");
                Assert.That(DeferredToLaterTasks.Contains(code), Is.False, "T314 code '" + code + "' is still listed as deferred.");
                Assert.That(Backticked(table).Contains(code), Is.True, "The guide code table does not list '" + code + "'.");
                Assert.That(structural, Does.Contain("`" + code + "`"), "The structural rule table does not describe '" + code + "'.");
            }
        }

        /// <summary>Codes the reader-side vocabulary check adds (T313 follow-up from the T312 review); the contract tables do not list them.</summary>
        private static readonly string[] AddedByT313 = { "contract.objective.unknown", "contract.class.unknown" };

        [Test]
        public void ContractAndGuideCodeTablesOnlyNameCodesTheValidatorKnows()
        {
            var known = new HashSet<string>(RunContentValidator.RuleCodes, StringComparer.Ordinal);

            var guide = GuideV3();
            var guideTable = Section(guide, "## v3: semantic rule codes", "## v3: a complete, valid, annotated example");
            var guideCodes = Backticked(guideTable).Where(t => CodePattern.IsMatch(t)).ToList();
            Assert.That(guideCodes, Is.Not.Empty);
            foreach (var code in guideCodes)
            {
                Assert.That(known.Contains(code), Is.True, "The guide table names code '" + code + "' that the validator does not implement.");
            }

            // The v2 contract table (carried over by v3): every code is implemented or retired by v3.
            var v2Codes = new HashSet<string>(
                Backticked(Section(ContractV2Text(), "## Semantic rule codes", "## Evolution")).Where(t => CodePattern.IsMatch(t)),
                StringComparer.Ordinal);
            foreach (var code in v2Codes)
            {
                Assert.That(known.Contains(code) || RetiredByV3.Contains(code), Is.True, "v2 contract code '" + code + "' is neither implemented nor retired by v3.");
            }

            // The v3 contract table: every code is implemented here or deferred to a named later task.
            var v3Text = ContractV3Text();
            var v3Codes = new HashSet<string>(
                Regex.Matches(v3Text.Substring(v3Text.IndexOf("## Rule codes (new)", StringComparison.Ordinal)), @"^\| `([^`]+)` \|", RegexOptions.Multiline)
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .Where(t => CodePattern.IsMatch(t)),
                StringComparer.Ordinal);
            Assert.That(v3Codes.Count, Is.GreaterThanOrEqualTo(20), "the first column of the v3 rule table"); 
            foreach (var code in v3Codes)
            {
                Assert.That(known.Contains(code) || DeferredToLaterTasks.Contains(code), Is.True, "v3 contract code '" + code + "' is neither implemented nor deferred.");
            }

            foreach (var code in DeferredToLaterTasks)
            {
                Assert.That(v3Codes.Contains(code), Is.True, "Deferred code '" + code + "' is not in the v3 contract table.");
                Assert.That(known.Contains(code), Is.False, "Deferred code '" + code + "' is implemented now: remove it from DeferredToLaterTasks.");
            }

            foreach (var code in RetiredByV3)
            {
                Assert.That(known.Contains(code), Is.False, "Retired code '" + code + "' is still implemented.");
                Assert.That(v2Codes.Contains(code), Is.True, "Retired code '" + code + "' is not in the v2 contract table.");
            }

            // Every implemented code is documented by a contract table (the contracts win over the guide).
            foreach (var code in RunContentValidator.RuleCodes.Where(c => c != "content.version.unsupported" && !AddedByT313.Contains(c)))
            {
                Assert.That(v2Codes.Contains(code) || v3Codes.Contains(code), Is.True, "No contract table lists '" + code + "'.");
            }

            foreach (var code in AddedByT313)
            {
                Assert.That(guideCodes.Contains(code), Is.True, "The guide table does not list '" + code + "'.");
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
