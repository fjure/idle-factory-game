using System;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// Structural coverage for <see cref="RunContentReader"/> (T096, issue #178): one test per structural rule of the
    /// carried-over v1 envelope, the v2 member sets and the shape rules for kind-dependent objects. Semantic rules are
    /// exercised in <see cref="RunContentValidatorTests"/>.
    /// </summary>
    public sealed class RunContentReaderTests
    {
        private static RunContentReadResult Read(Action<JObject> mutate)
        {
            var document = RunContentExample.Valid();
            mutate?.Invoke(document);
            return RunContentReader.Read(RunContentExample.Bytes(document));
        }

        private static void AssertFailure(RunContentReadResult result, string code, string pointer)
        {
            Assert.That(result.IsSuccess, Is.False, "Expected a structural failure with code " + code);
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Diagnostic.Code, Is.EqualTo(code), result.Diagnostic.Message);
            Assert.That(result.Diagnostic.Path, Is.EqualTo(pointer));
        }

        [Test]
        public void ExampleDocumentProducesExpectedDtoGraph()
        {
            var result = RunContentReader.Read(RunContentExample.Bytes(RunContentExample.GuideJson()));

            Assert.That(result.IsSuccess, Is.True, result.Diagnostic == null ? string.Empty : result.Diagnostic.Message);
            var document = result.Document;
            Assert.That(document.ContentSchemaVersion, Is.EqualTo(2));
            Assert.That(document.ContentRevision, Is.EqualTo(1));
            Assert.That(document.RunStart.StartingCash, Is.EqualTo(25));
            CollectionAssert.AreEqual(new[] { "iron.ore", "machine.miner" }, document.RunStart.InitialUnlocks);
            Assert.That(document.RunStart.MaxCombinedSpeedPpm, Is.EqualTo(100_000_000L));
            Assert.That(document.Resources.Select(r => r.Id), Is.EqualTo(new[] { "iron.bar", "iron.gear", "iron.ore", "iron.plate" }));
            Assert.That(document.Resources[0].Unlock.Kind, Is.EqualTo("resourceProduced"));
            Assert.That(document.Resources[0].Unlock.Id, Is.EqualTo("iron.ore"));
            Assert.That(document.Resources[1].Unlock.Level, Is.EqualTo(3));
            Assert.That(document.Resources[2].Unlock.Id, Is.Null);
            Assert.That(document.Resources[2].Unlock.Level, Is.Null);
            Assert.That(document.Recipes.Single(r => r.Id == "recipe.mine-iron-ore").Inputs, Is.Empty);
            Assert.That(document.Recipes.Count, Is.EqualTo(4));

            var miner = document.Machines.Single(m => m.Id == "machine.miner");
            Assert.That(miner.MaxLevel, Is.EqualTo(25));
            Assert.That(miner.Cost.Base, Is.EqualTo(10));
            Assert.That(miner.Cost.Rounding, Is.EqualTo("ceil"));
            Assert.That(miner.Milestones.Select(m => m.Level), Is.EqualTo(new[] { 10, 25 }));
            Assert.That(document.StorageLevels.Count, Is.EqualTo(3));
            Assert.That(document.FactoryLevels.Last().Slots, Is.EqualTo(12));

            var timedBar = document.Contracts.Single(c => c.Kind == "timedBar");
            Assert.That(timedBar.DurationMs, Is.EqualTo(180000));
            Assert.That(document.Contracts.Where(c => c.Kind != "timedBar").All(c => c.DurationMs == null), Is.True);
            var gear = document.Contracts.Single(c => c.Kind == "gear");
            Assert.That(gear.Rewards.Select(r => r.Kind), Is.EqualTo(new[] { "cash", "item", "rp" }));
            Assert.That(gear.Rewards[1].ItemId, Is.EqualTo("item.golden-blueprint"));
            CollectionAssert.AreEqual(new[] { 9 }, gear.Rewards[1].OnCompletionIndices);

            var blueprint = document.Items.Single(i => i.Id == "item.golden-blueprint");
            Assert.That(blueprint.Target.Kind, Is.EqualTo("factory"));
            Assert.That(blueprint.Target.AllowedMachineIds, Is.Null);
            Assert.That(blueprint.Effect.ValueMember, Is.EqualTo("speedPpm"));
            Assert.That(blueprint.Effect.Value, Is.EqualTo(1_200_000));
            Assert.That(blueprint.Effect.Target, Is.Null);
            var drill = document.Items.Single(i => i.Id == "item.diamond-drill-head");
            Assert.That(drill.Effect.ValueMember, Is.EqualTo("factor"));
            CollectionAssert.AreEqual(new[] { "machine.miner" }, drill.Target.AllowedMachineIds);

            var toolDiscount = document.Research.Single(r => r.Id == "research.tool-discount");
            Assert.That(toolDiscount.Prerequisites.Single().Id, Is.EqualTo("research.smelter-speed"));
            Assert.That(toolDiscount.Effect.Target.Kind, Is.EqualTo("allMachines"));
            Assert.That(toolDiscount.Effect.Target.Id, Is.Null);
            Assert.That(document.Research.Single(r => r.Id == "research.smelter-speed").Effect.Target.Id, Is.EqualTo("machine.smelter"));
            Assert.That(document.Overdrive.CooldownMs, Is.EqualTo(300000));
            Assert.That(document.Prestige.IpCurve.Kind, Is.EqualTo("sqrt"));
            Assert.That(document.Prestige.IpCurve.Divisor, Is.EqualTo(10_000_000));
            var seedCapital = document.IndustryUpgrades.Single(u => u.Id == "industry.seed-capital");
            Assert.That(seedCapital.Effect.ValueMember, Is.EqualTo("valuePerLevel"));
            Assert.That(seedCapital.Effect.Target, Is.Null);
            Assert.That(document.OfflinePolicies.Single().MaxCreditedTicks, Is.EqualTo(288_000_000_000L));
        }

        [Test]
        public void NullAndEmptyBytesAreContentEmpty()
        {
            Assert.That(RunContentReader.Read(null).Diagnostic.Code, Is.EqualTo("content.empty"));
            Assert.That(RunContentReader.Read(new byte[0]).Diagnostic.Code, Is.EqualTo("content.empty"));
        }

        [Test]
        public void OversizedDocumentIsContentTooLarge()
        {
            var bytes = Enumerable.Repeat((byte)' ', GameplayContentLimits.MaximumByteCount + 1).ToArray();

            var result = RunContentReader.Read(bytes);

            Assert.That(result.Diagnostic.Code, Is.EqualTo("content.too-large"));
        }

        [Test]
        public void ByteOrderMarkIsContentEncoding()
        {
            var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(RunContentExample.Bytes(RunContentExample.Valid())).ToArray();

            Assert.That(RunContentReader.Read(bytes).Diagnostic.Code, Is.EqualTo("content.encoding"));
        }

        [Test]
        public void InvalidUtf8IsContentEncoding()
        {
            var result = RunContentReader.Read(new byte[] { (byte)'{', 0xC3, 0x28, (byte)'}' });

            Assert.That(result.Diagnostic.Code, Is.EqualTo("content.encoding"));
        }

        [Test]
        public void MissingVersionIsContentVersionMissing()
        {
            var result = Read(document => document.Remove("contentSchemaVersion"));

            AssertFailure(result, "content.version.missing", "/contentSchemaVersion");
        }

        [TestCase("1")]
        [TestCase("3")]
        [TestCase("0")]
        [TestCase("\"2\"")]
        [TestCase("2.0")]
        [TestCase("true")]
        public void AnyVersionButTwoIsUnsupported(string literal)
        {
            var result = RunContentReader.Read(RunContentExample.Bytes("{\"contentSchemaVersion\":" + literal + "}"));

            AssertFailure(result, "content.version.unsupported", "/contentSchemaVersion");
        }

        [Test]
        public void MalformedJsonIsReportedWithPosition()
        {
            var result = RunContentReader.Read(RunContentExample.Bytes("{\"contentSchemaVersion\":2,"));

            Assert.That(result.Diagnostic.Code, Is.EqualTo("json.malformed"));
        }

        [Test]
        public void NonObjectRootIsMalformed()
        {
            var result = RunContentReader.Read(RunContentExample.Bytes("[2]"));

            Assert.That(result.Diagnostic.Code, Is.EqualTo("json.malformed"));
        }

        [Test]
        public void TrailingContentIsRejected()
        {
            var json = RunContentExample.Valid().ToString(Formatting.None) + " {}";

            var result = RunContentReader.Read(RunContentExample.Bytes(json));

            Assert.That(result.Diagnostic.Code, Is.EqualTo("json.trailing-content"));
        }

        [Test]
        public void ExcessDepthIsJsonDepth()
        {
            var json = "{\"deep\":" + new string('[', 20) + new string(']', 20) + ",\"contentSchemaVersion\":2}";

            var result = RunContentReader.Read(RunContentExample.Bytes(json));

            Assert.That(result.Diagnostic.Code, Is.EqualTo("json.depth"));
        }

        [Test]
        public void CommentsAreRejected()
        {
            var json = "{\"contentSchemaVersion\":2, /* c */ \"contentRevision\":1}";

            Assert.That(RunContentReader.Read(RunContentExample.Bytes(json)).Diagnostic.Code, Is.EqualTo("json.malformed"));
        }

        [Test]
        public void UnknownRootMemberIsRejectedWithPositionAndPointer()
        {
            var result = Read(document => document["bogus"] = 1);

            AssertFailure(result, "member.unknown", "/bogus");
            Assert.That(result.Diagnostic.Line, Is.Not.Null);
            Assert.That(result.Diagnostic.Column, Is.Not.Null);
        }

        [Test]
        public void UnknownNestedMemberIsRejectedAtEveryLevel()
        {
            AssertFailure(Read(d => d["runStart"]["extra"] = 1), "member.unknown", "/runStart/extra");
            AssertFailure(Read(d => d["machines"][1]["cost"]["extra"] = 1), "member.unknown", "/machines/1/cost/extra");
            AssertFailure(Read(d => d["contracts"][0]["rewards"][0]["curve"]["extra"] = 1), "member.unknown", "/contracts/0/rewards/0/curve/extra");
            AssertFailure(Read(d => d["offlinePolicies"][0]["extra"] = 1), "member.unknown", "/offlinePolicies/0/extra");
        }

        [Test]
        public void DuplicateMemberIsRejected()
        {
            var json = RunContentExample.Valid().ToString(Formatting.None);
            json = RunContentExample.ReplaceOnce(json, "\"basePrice\":3,", "\"basePrice\":3,\"basePrice\":4,");

            var result = RunContentReader.Read(RunContentExample.Bytes(json));

            AssertFailure(result, "member.duplicate", "/resources/0/basePrice");
        }

        [Test]
        public void MissingMemberIsRejectedWithItsPointer()
        {
            AssertFailure(Read(d => ((JObject)d["resources"][0]).Remove("label")), "member.missing", "/resources/0/label");
            AssertFailure(Read(d => d.Remove("prestige")), "member.missing", "/prestige");
            AssertFailure(Read(d => ((JObject)d["machines"][0]["cost"]).Remove("rounding")), "member.missing", "/machines/0/cost/rounding");
        }

        [Test]
        public void NullValueIsRejected()
        {
            AssertFailure(Read(d => d["runStart"]["startingCash"] = JValue.CreateNull()), "value.null", "/runStart/startingCash");
            AssertFailure(Read(d => d["resources"] = JValue.CreateNull()), "value.null", "/resources");
            AssertFailure(Read(d => d["resources"][0]["unlock"] = JValue.CreateNull()), "value.null", "/resources/0/unlock");
        }

        [Test]
        public void WrongJsonTypeIsRejected()
        {
            AssertFailure(Read(d => d["runStart"]["startingCash"] = "25"), "value.type", "/runStart/startingCash");
            AssertFailure(Read(d => d["resources"][0]["label"] = 5), "value.type", "/resources/0/label");
            AssertFailure(Read(d => d["resources"] = new JObject()), "value.type", "/resources");
            AssertFailure(Read(d => d["runStart"] = new JArray()), "value.type", "/runStart");
            AssertFailure(Read(d => ((JArray)d["resources"]).Add(5)), "value.type", "/resources/4");
            AssertFailure(Read(d => d["runStart"]["initialUnlocks"] = new JArray(1)), "value.type", "/runStart/initialUnlocks/0");
        }

        [TestCase("1.5")]
        [TestCase("1e3")]
        [TestCase("2.0")]
        public void FractionOrExponentIsNotInteger(string literal)
        {
            var json = RunContentExample.Valid().ToString(Formatting.None);
            json = RunContentExample.ReplaceOnce(json, "\"startingCash\":25", "\"startingCash\":" + literal);

            AssertFailure(RunContentReader.Read(RunContentExample.Bytes(json)), "value.not-integer", "/runStart/startingCash");
        }

        [Test]
        public void IntegersBeyondTheirStorageRangeAreOutOfRange()
        {
            var json = RunContentExample.Valid().ToString(Formatting.None);
            var beyondInt32 = RunContentExample.ReplaceOnce(json, "\"contentRevision\":1", "\"contentRevision\":3000000000");
            AssertFailure(RunContentReader.Read(RunContentExample.Bytes(beyondInt32)), "value.out-of-range", "/contentRevision");

            var beyondInt64 = RunContentExample.ReplaceOnce(json, "\"startingCash\":25", "\"startingCash\":9223372036854775808");
            AssertFailure(RunContentReader.Read(RunContentExample.Bytes(beyondInt64)), "value.out-of-range", "/runStart/startingCash");

            var hugeIndex = RunContentExample.ReplaceOnce(json, "\"onCompletionIndices\":[9]", "\"onCompletionIndices\":[3000000000]");
            AssertFailure(RunContentReader.Read(RunContentExample.Bytes(hugeIndex)), "value.out-of-range", "/contracts/0/rewards/1/onCompletionIndices/0");
        }

        [Test]
        public void LabelsAndPresentationKeysMustBeNonBlankAndBounded()
        {
            AssertFailure(Read(d => d["resources"][0]["label"] = "   "), "value.out-of-range", "/resources/0/label");
            AssertFailure(Read(d => d["machines"][0]["presentationKey"] = new string('k', 129)), "value.out-of-range", "/machines/0/presentationKey");
            AssertFailure(Read(d => d["items"][0]["label"] = string.Empty), "value.out-of-range", "/items/0/label");
            Assert.That(Read(d => d["items"][0]["label"] = new string('k', 128)).IsSuccess, Is.True);
        }

        [Test]
        public void RoundingIsAClosedVocabulary()
        {
            AssertFailure(Read(d => d["machines"][0]["cost"]["rounding"] = "round"), "value.out-of-range", "/machines/0/cost/rounding");
        }

        [Test]
        public void RequiredFamiliesMustNotBeEmptyButResearchAndIndustryMay()
        {
            AssertFailure(Read(d => d["resources"] = new JArray()), "collection.empty", "/resources");
            AssertFailure(Read(d => d["items"] = new JArray()), "collection.empty", "/items");
            AssertFailure(Read(d => d["offlinePolicies"] = new JArray()), "collection.empty", "/offlinePolicies");
            Assert.That(Read(d => d["research"] = new JArray()).IsSuccess, Is.True);
            Assert.That(Read(d => d["industryUpgrades"] = new JArray()).IsSuccess, Is.True);
            Assert.That(Read(d => ((JArray)d["recipes"][1]["inputs"]).Clear()).IsSuccess, Is.True, "an input-free recipe is structurally valid");
        }

        [Test]
        public void ListLimitsAreEnforced()
        {
            var tooManyInputs = Read(d =>
            {
                var inputs = (JArray)d["recipes"][0]["inputs"];
                for (var i = 0; i < GameplayContentLimits.MaximumNestedEntryCount; i++)
                {
                    inputs.Add(new JObject { ["resourceId"] = "iron.bar", ["amount"] = 1 });
                }
            });
            AssertFailure(tooManyInputs, "collection.too-many", "/recipes/0/inputs");

            var tooManyResources = Read(d =>
            {
                var resources = (JArray)d["resources"];
                for (var i = 0; i < GameplayContentLimits.MaximumTopLevelEntryCount; i++)
                {
                    resources.Add(new JObject
                    {
                        ["id"] = "extra." + i,
                        ["label"] = "x",
                        ["presentationKey"] = "k",
                        ["basePrice"] = 1,
                        ["unlock"] = new JObject { ["kind"] = "always" },
                    });
                }
            });
            Assert.That(tooManyResources.IsSuccess, Is.False);
            Assert.That(tooManyResources.Diagnostic.Code, Is.EqualTo("collection.too-many"));
        }

        [Test]
        public void KindDependentMembersAreEnforcedForKnownKinds()
        {
            AssertFailure(Read(d => d["resources"][2]["unlock"]["id"] = "iron.ore"), "member.unknown", "/resources/2/unlock/id");
            AssertFailure(Read(d => ((JObject)d["resources"][0]["unlock"]).Remove("id")), "member.missing", "/resources/0/unlock/id");
            AssertFailure(Read(d => ((JObject)d["resources"][1]["unlock"]).Remove("level")), "member.missing", "/resources/1/unlock/level");
            AssertFailure(Read(d => d["contracts"][0]["rewards"][0]["itemId"] = "item.x"), "member.unknown", "/contracts/0/rewards/0/itemId");
            AssertFailure(Read(d => ((JObject)d["contracts"][0]["rewards"][1]).Remove("onCompletionIndices")), "member.missing", "/contracts/0/rewards/1/onCompletionIndices");
            AssertFailure(Read(d => d["items"][1]["target"]["allowedMachineIds"] = new JArray("machine.miner")), "member.unknown", "/items/1/target/allowedMachineIds");
            AssertFailure(Read(d => ((JObject)d["items"][0]["target"]).Remove("allowedMachineIds")), "member.missing", "/items/0/target/allowedMachineIds");
            AssertFailure(Read(d => d["items"][1]["effect"]["target"] = new JObject { ["kind"] = "allMachines" }), "member.unknown", "/items/1/effect/target");
            AssertFailure(Read(d => ((JObject)d["items"][1]["effect"]).Remove("speedPpm")), "member.missing", "/items/1/effect/speedPpm");
            AssertFailure(Read(d => d["items"][1]["effect"] = new JObject { ["kind"] = "machineSpeed", ["valuePerLevelPpm"] = 1200000 }), "member.unknown", "/items/1/effect/valuePerLevelPpm");
            AssertFailure(Read(d => ((JObject)d["research"][0]["effect"]).Remove("target")), "member.missing", "/research/0/effect/target");
            AssertFailure(Read(d => ((JObject)d["research"][0]["effect"]["target"]).Add("id", "iron.ore")), "member.unknown", "/research/0/effect/target/id");
            AssertFailure(Read(d => ((JObject)d["research"][1]["effect"]["target"]).Remove("id")), "member.missing", "/research/1/effect/target/id");
            AssertFailure(Read(d => d["industryUpgrades"][0]["effect"]["target"] = new JObject { ["kind"] = "allMachines" }), "member.unknown", "/industryUpgrades/0/effect/target");
        }

        [Test]
        public void UnknownKindsPassThroughForTheValidatorToReport()
        {
            var result = Read(d =>
            {
                d["resources"][2]["unlock"]["kind"] = "onFullMoon";
                d["contracts"][0]["kind"] = "widget";
                d["contracts"][0]["rewards"][0]["kind"] = "trophy";
                d["items"][0]["target"]["kind"] = "planet";
                d["items"][0]["effect"]["kind"] = "teleport";
                d["research"][0]["effect"]["kind"] = "teleport";
                d["research"][0]["effect"]["target"]["kind"] = "planet";
                d["prestige"]["ipCurve"]["kind"] = "linear";
            });

            Assert.That(result.IsSuccess, Is.True, result.Diagnostic == null ? string.Empty : result.Diagnostic.Message);
            Assert.That(result.Document.Items[0].Effect.Kind, Is.EqualTo("teleport"));
            Assert.That(result.Document.Research[0].Effect.Target.Kind, Is.EqualTo("planet"));
            Assert.That(result.Document.Contracts[0].Rewards[0].Kind, Is.EqualTo("trophy"));
        }

        [Test]
        public void ExpansionTargetHasNoId()
        {
            var accepted = Read(d => d["research"][2]["effect"]["target"] = new JObject { ["kind"] = "expansion" });
            Assert.That(accepted.IsSuccess, Is.True, accepted.Diagnostic == null ? string.Empty : accepted.Diagnostic.Message);
            Assert.That(accepted.Document.Research[2].Effect.Target.Kind, Is.EqualTo("expansion"));
            Assert.That(accepted.Document.Research[2].Effect.Target.Id, Is.Null);

            var withId = Read(d => d["research"][2]["effect"]["target"] = new JObject { ["kind"] = "expansion", ["id"] = "storage" });
            AssertFailure(withId, "member.unknown", "/research/2/effect/target/id");
        }

        [Test]
        public void ReaderDoesNotApplySemanticRules()
        {
            var result = Read(d =>
            {
                d["resources"][0]["id"] = string.Empty;
                d["resources"][0]["basePrice"] = -5;
                d["recipes"][0]["outputs"][0]["amount"] = 0;
                d["machines"][1]["maxLevel"] = 0;
                d["machines"][1]["cost"]["growthPpm"] = 0;
                d["storageLevels"][0]["cost"] = 7;
                d["contracts"][0]["durationMs"] = 5;
                d["overdrive"]["speedPpm"] = 1;
            });

            Assert.That(result.IsSuccess, Is.True, result.Diagnostic == null ? string.Empty : result.Diagnostic.Message);
        }

        [Test]
        public void StructuralDiagnosticsNeverEchoFileSystemPathsOrDocumentText()
        {
            var longName = new string('n', 400);
            var json = RunContentExample.Valid().ToString(Formatting.None);
            json = RunContentExample.ReplaceOnce(json, "\"contentRevision\":1", "\"contentRevision\":1,\"" + longName + "\":1");

            var result = RunContentReader.Read(RunContentExample.Bytes(json));

            Assert.That(result.Diagnostic.Code, Is.EqualTo("member.unknown"));
            Assert.That(result.Diagnostic.Message, Does.Not.Contain(longName));
            Assert.That(result.Diagnostic.Message.Length, Is.LessThan(200));
            Assert.That(result.Diagnostic.Message, Does.Not.Contain(RunContentExample.RepositoryRoot()));
            Assert.That(result.Diagnostic.Message, Does.Not.Contain(":\\"));
        }

        [Test]
        public void ReadIsRepeatableAndIndependent()
        {
            var bytes = RunContentExample.Bytes(RunContentExample.Valid());

            var first = RunContentReader.Read(bytes);
            var second = RunContentReader.Read(bytes);

            Assert.That(first.Document, Is.Not.SameAs(second.Document));
            first.Document.Resources[0].BasePrice = 999;
            Assert.That(second.Document.Resources[0].BasePrice, Is.EqualTo(3));
        }
    }
}
