using System.Linq;
using System.Text;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// Structural coverage for <see cref="GameplayContentReader"/> (T072, issue #47):
    /// one test per structural code in
    /// specs/002-json-content-authoring/data-model.md plus the spec's structural edge
    /// cases. Semantic/identity validation (T073) is out of scope and not exercised
    /// here.
    /// </summary>
    public sealed class GameplayContentReaderTests
    {
        // Contract example (gameplay-content-v1.md), with "dev.output" added to
        // resources so the document is also reference-complete for later stages.
        private const string ValidDocumentJson = @"{
  ""contentSchemaVersion"": 1,
  ""resources"": [
    { ""id"": ""dev.input"", ""developmentLabel"": ""Input"", ""presentationKey"": ""F001-RESOURCE-DEV-INPUT"", ""isDevelopmentOnly"": true },
    { ""id"": ""dev.output"", ""developmentLabel"": ""Output"", ""isDevelopmentOnly"": true }
  ],
  ""recipes"": [
    {
      ""id"": ""dev.recipe.basic"",
      ""inputs"": [ { ""resourceId"": ""dev.input"", ""amount"": 2 } ],
      ""outputs"": [ { ""resourceId"": ""dev.output"", ""amount"": 1 } ],
      ""durationTicks"": 50000000,
      ""isDevelopmentOnly"": true
    }
  ],
  ""buildings"": [
    {
      ""id"": ""dev.machine.basic"",
      ""footprintWidth"": 2,
      ""footprintHeight"": 2,
      ""allowedOrientations"": [0],
      ""recipeId"": ""dev.recipe.basic"",
      ""inputBuffer"": [ { ""resourceId"": ""dev.input"", ""capacity"": 4 } ],
      ""outputBuffer"": [ { ""resourceId"": ""dev.output"", ""capacity"": 1 } ],
      ""presentationKey"": ""F001-MACHINE-D001-NORMAL"",
      ""isDevelopmentOnly"": true
    }
  ],
  ""offlinePolicies"": [
    { ""revision"": 1, ""efficiencyNumerator"": 4, ""efficiencyDenominator"": 5, ""maxCreditedTicks"": 288000000000 }
  ]
}";

        [Test]
        public void ValidFullDocumentProducesExpectedDtoGraph()
        {
            var result = GameplayContentReader.Read(Bytes(ValidDocumentJson));

            Assert.That(result.IsSuccess, Is.True, result.Diagnostic == null ? string.Empty : result.Diagnostic.Message);
            var document = result.Document;
            Assert.That(document.ContentSchemaVersion, Is.EqualTo(1));

            Assert.That(document.Resources.Count, Is.EqualTo(2));
            Assert.That(document.Resources[0].Id, Is.EqualTo("dev.input"));
            Assert.That(document.Resources[0].DevelopmentLabel, Is.EqualTo("Input"));
            Assert.That(document.Resources[0].PresentationKey, Is.EqualTo("F001-RESOURCE-DEV-INPUT"));
            Assert.That(document.Resources[0].IsDevelopmentOnly, Is.True);
            Assert.That(document.Resources[1].Id, Is.EqualTo("dev.output"));
            Assert.That(document.Resources[1].PresentationKey, Is.Null);

            Assert.That(document.Recipes.Count, Is.EqualTo(1));
            var recipe = document.Recipes[0];
            Assert.That(recipe.Id, Is.EqualTo("dev.recipe.basic"));
            Assert.That(recipe.Inputs.Count, Is.EqualTo(1));
            Assert.That(recipe.Inputs[0].ResourceId, Is.EqualTo("dev.input"));
            Assert.That(recipe.Inputs[0].Amount, Is.EqualTo(2L));
            Assert.That(recipe.Outputs[0].ResourceId, Is.EqualTo("dev.output"));
            Assert.That(recipe.Outputs[0].Amount, Is.EqualTo(1L));
            Assert.That(recipe.DurationTicks, Is.EqualTo(50000000L));
            Assert.That(recipe.IsDevelopmentOnly, Is.True);

            Assert.That(document.Buildings.Count, Is.EqualTo(1));
            var building = document.Buildings[0];
            Assert.That(building.Id, Is.EqualTo("dev.machine.basic"));
            Assert.That(building.FootprintWidth, Is.EqualTo(2));
            Assert.That(building.FootprintHeight, Is.EqualTo(2));
            Assert.That(building.AllowedOrientations, Is.EqualTo(new[] { 0 }));
            Assert.That(building.RecipeId, Is.EqualTo("dev.recipe.basic"));
            Assert.That(building.InputBuffer.Single().ResourceId, Is.EqualTo("dev.input"));
            Assert.That(building.InputBuffer.Single().Capacity, Is.EqualTo(4L));
            Assert.That(building.OutputBuffer.Single().Capacity, Is.EqualTo(1L));
            Assert.That(building.PresentationKey, Is.EqualTo("F001-MACHINE-D001-NORMAL"));

            Assert.That(document.OfflinePolicies.Count, Is.EqualTo(1));
            var policy = document.OfflinePolicies[0];
            Assert.That(policy.Revision, Is.EqualTo(1));
            Assert.That(policy.EfficiencyNumerator, Is.EqualTo(4L));
            Assert.That(policy.EfficiencyDenominator, Is.EqualTo(5L));
            Assert.That(policy.MaxCreditedTicks, Is.EqualTo(288000000000L));
        }

        [Test]
        public void EmptyInputIsRejected()
        {
            AssertFailure(new byte[0], "content.empty", string.Empty, line: null);
        }

        [Test]
        public void NullInputIsRejected()
        {
            AssertFailure(null, "content.empty", string.Empty, line: null);
        }

        [Test]
        public void OversizedInputIsRejectedBeforeDecode()
        {
            var oversized = new byte[GameplayContentLimits.MaximumByteCount + 1];
            for (var i = 0; i < oversized.Length; i++)
            {
                oversized[i] = (byte)' ';
            }

            AssertFailure(oversized, "content.too-large", string.Empty, line: null);
        }

        [Test]
        public void ByteOrderMarkIsRejected()
        {
            var body = Bytes("{\"contentSchemaVersion\":1}");
            var withBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(body).ToArray();

            AssertFailure(withBom, "content.encoding", string.Empty, line: null);
        }

        [Test]
        public void InvalidUtf8IsRejected()
        {
            var bytes = Bytes("{\"contentSchemaVersion\":1}");
            bytes[5] = 0xFF; // not a valid UTF-8 lead byte anywhere in a sequence

            AssertFailure(bytes, "content.encoding", string.Empty, line: null);
        }

        [Test]
        public void MissingVersionIsRejected()
        {
            var json = "{\"resources\":[],\"recipes\":[],\"buildings\":[],\"offlinePolicies\":[]}";

            AssertFailure(Bytes(json), "content.version.missing", "/contentSchemaVersion", line: 1);
        }

        [Test]
        public void UnsupportedVersionIsRejected()
        {
            var json = "{\"contentSchemaVersion\":2,\"resources\":[],\"recipes\":[],\"buildings\":[],\"offlinePolicies\":[]}";

            AssertFailure(Bytes(json), "content.version.unsupported", "/contentSchemaVersion", line: 1);
        }

        [Test]
        public void TruncatedDocumentIsRejected()
        {
            var json = "{\"contentSchemaVersion\":1,\"resources\":[";

            AssertFailure(Bytes(json), "json.malformed", "/resources", line: 1);
        }

        [Test]
        public void TrailingContentAfterRootIsRejected()
        {
            var json = ValidDocumentJson + "\nx";
            var expectedLine = ValidDocumentJson.Count(c => c == '\n') + 2;

            AssertFailure(Bytes(json), "json.trailing-content", string.Empty, line: expectedLine);
        }

        [Test]
        public void DepthExceededIsRejected()
        {
            // Not a schema member; the version-peek stage skips it, and Json.NET's
            // MaxDepth guard fires inside that Skip() call before any member-name
            // validation ever runs.
            var deepArray = new string('[', 25) + "1" + new string(']', 25);
            var json = "{\"deepJunk\":" + deepArray + ",\"contentSchemaVersion\":1}";

            AssertFailure(Bytes(json), "json.depth", string.Empty, line: 1);
        }

        [Test]
        public void JsonFailuresCarryStaticMessages_AndHostileMemberNamesAreClipped()
        {
            var truncated = GameplayContentReader.Read(Bytes("{\"contentSchemaVersion\":1,\"resources\":[{\"id\":\"secret-token\" \"x\""));
            Assert.That(truncated.IsSuccess, Is.False);
            Assert.That(truncated.Diagnostic.Message, Is.EqualTo("The JSON document is malformed."));
            Assert.That(truncated.Diagnostic.Message, Does.Not.Contain("secret-token"));

            var deepArray = new string('[', 25) + "1" + new string(']', 25);
            var deep = GameplayContentReader.Read(Bytes("{\"deepJunk\":" + deepArray + ",\"contentSchemaVersion\":1}"));
            Assert.That(deep.Diagnostic.Code, Is.EqualTo("json.depth"));
            Assert.That(deep.Diagnostic.Message, Is.EqualTo("The JSON document is nested too deeply."));

            var hostile = new string('m', 5000);
            var unknown = GameplayContentReader.Read(Bytes("{\"contentSchemaVersion\":1,\"" + hostile + "\":1}"));
            Assert.That(unknown.IsSuccess, Is.False);
            Assert.That(unknown.Diagnostic.Code, Is.EqualTo("member.unknown"));
            Assert.That(unknown.Diagnostic.Message.Length, Is.LessThan(200));
            Assert.That(unknown.Diagnostic.Path.Length, Is.LessThan(200));
        }

        [Test]
        public void UnknownMemberIsRejected()
        {
            var json = "{\"contentSchemaVersion\":1,\"resources\":[{\"id\":\"r\",\"developmentLabel\":\"R\"," +
                       "\"isDevelopmentOnly\":false,\"bogus\":true}],\"recipes\":[],\"buildings\":[],\"offlinePolicies\":[]}";

            AssertFailure(Bytes(json), "member.unknown", "/resources/0/bogus", line: 1);
        }

        [Test]
        public void DuplicateMemberAtRootIsRejected()
        {
            var json = "{\"contentSchemaVersion\":1," +
                       "\"resources\":[{\"id\":\"r\",\"developmentLabel\":\"R\",\"isDevelopmentOnly\":false}]," +
                       "\"resources\":[],\"recipes\":[],\"buildings\":[],\"offlinePolicies\":[]}";

            AssertFailure(Bytes(json), "member.duplicate", "/resources", line: 1);
        }

        [Test]
        public void DuplicateMemberInNestedObjectIsRejected()
        {
            var lines = new[]
            {
                "{",
                "  \"contentSchemaVersion\": 1,",
                "  \"resources\": [",
                "    { \"id\": \"r\", \"id\": \"r2\", \"developmentLabel\": \"R\", \"isDevelopmentOnly\": false }",
                "  ],",
                "  \"recipes\": [],",
                "  \"buildings\": [],",
                "  \"offlinePolicies\": []",
                "}",
            };
            var json = string.Join("\n", lines);

            AssertFailure(Bytes(json), "member.duplicate", "/resources/0/id", line: 4);
        }

        [Test]
        public void MissingRequiredMemberIsRejected()
        {
            var json = "{\"contentSchemaVersion\":1,\"resources\":[{\"id\":\"r\",\"developmentLabel\":\"R\"}]," +
                       "\"recipes\":[],\"buildings\":[],\"offlinePolicies\":[]}";

            AssertFailure(Bytes(json), "member.missing", "/resources/0/isDevelopmentOnly", line: 1);
        }

        [Test]
        public void BuildingMissingPresentationKeyIsRejected()
        {
            // Parent correction (2026-09-28): buildings[].presentationKey is required
            // (BuildingDefinition uses DefinitionGuard.RequiredText, Definitions.cs:276),
            // unlike resources[].presentationKey which stays optional.
            var json = "{\"contentSchemaVersion\":1," +
                       "\"resources\":[{\"id\":\"r\",\"developmentLabel\":\"R\",\"isDevelopmentOnly\":false}]," +
                       "\"recipes\":[{\"id\":\"rec\",\"inputs\":[{\"resourceId\":\"r\",\"amount\":1}]," +
                       "\"outputs\":[{\"resourceId\":\"r\",\"amount\":1}],\"durationTicks\":1,\"isDevelopmentOnly\":false}]," +
                       "\"buildings\":[{\"id\":\"b\",\"footprintWidth\":1,\"footprintHeight\":1," +
                       "\"allowedOrientations\":[0],\"isDevelopmentOnly\":false}]," +
                       "\"offlinePolicies\":[{\"revision\":1,\"efficiencyNumerator\":1,\"efficiencyDenominator\":2,\"maxCreditedTicks\":1}]}";

            AssertFailure(Bytes(json), "member.missing", "/buildings/0/presentationKey", line: 1);
        }

        [Test]
        public void WrongTypeIsRejected()
        {
            var json = "{\"contentSchemaVersion\":1,\"resources\":[{\"id\":\"r\",\"developmentLabel\":\"R\"," +
                       "\"isDevelopmentOnly\":\"true\"}],\"recipes\":[],\"buildings\":[],\"offlinePolicies\":[]}";

            AssertFailure(Bytes(json), "value.type", "/resources/0/isDevelopmentOnly", line: 1);
        }

        [Test]
        public void ResourcePresentationKeyEmptyStringIsRejected()
        {
            // QA regression (SPAWN_FIX, issue:47:commit:52f484a...:fix): presentationKey is
            // optional (the member may be omitted) but must never be blank when present,
            // matching DefinitionGuard.OptionalText -> RequiredText (Definitions.cs:27).
            var json = "{\"contentSchemaVersion\":1,\"resources\":[{\"id\":\"r\",\"developmentLabel\":\"R\"," +
                       "\"presentationKey\":\"\",\"isDevelopmentOnly\":false}],\"recipes\":[],\"buildings\":[],\"offlinePolicies\":[]}";

            AssertFailure(Bytes(json), "value.out-of-range", "/resources/0/presentationKey", line: 1);
        }

        [Test]
        public void ResourcePresentationKeyWhitespaceIsRejected()
        {
            var json = "{\"contentSchemaVersion\":1,\"resources\":[{\"id\":\"r\",\"developmentLabel\":\"R\"," +
                       "\"presentationKey\":\"   \",\"isDevelopmentOnly\":false}],\"recipes\":[],\"buildings\":[],\"offlinePolicies\":[]}";

            AssertFailure(Bytes(json), "value.out-of-range", "/resources/0/presentationKey", line: 1);
        }

        [Test]
        public void ResourceDevelopmentLabelEmptyStringIsRejected()
        {
            var json = "{\"contentSchemaVersion\":1,\"resources\":[{\"id\":\"r\",\"developmentLabel\":\"\"," +
                       "\"isDevelopmentOnly\":false}],\"recipes\":[],\"buildings\":[],\"offlinePolicies\":[]}";

            AssertFailure(Bytes(json), "value.out-of-range", "/resources/0/developmentLabel", line: 1);
        }

        [Test]
        public void ResourceDevelopmentLabelWhitespaceIsRejected()
        {
            var json = "{\"contentSchemaVersion\":1,\"resources\":[{\"id\":\"r\",\"developmentLabel\":\"   \"," +
                       "\"isDevelopmentOnly\":false}],\"recipes\":[],\"buildings\":[],\"offlinePolicies\":[]}";

            AssertFailure(Bytes(json), "value.out-of-range", "/resources/0/developmentLabel", line: 1);
        }

        [Test]
        public void BuildingPresentationKeyWhitespaceIsRejected()
        {
            var json = "{\"contentSchemaVersion\":1," +
                       "\"resources\":[{\"id\":\"r\",\"developmentLabel\":\"R\",\"isDevelopmentOnly\":false}]," +
                       "\"recipes\":[{\"id\":\"rec\",\"inputs\":[{\"resourceId\":\"r\",\"amount\":1}]," +
                       "\"outputs\":[{\"resourceId\":\"r\",\"amount\":1}],\"durationTicks\":1,\"isDevelopmentOnly\":false}]," +
                       "\"buildings\":[{\"id\":\"b\",\"footprintWidth\":1,\"footprintHeight\":1," +
                       "\"allowedOrientations\":[0],\"presentationKey\":\"   \",\"isDevelopmentOnly\":false}]," +
                       "\"offlinePolicies\":[{\"revision\":1,\"efficiencyNumerator\":1,\"efficiencyDenominator\":2,\"maxCreditedTicks\":1}]}";

            AssertFailure(Bytes(json), "value.out-of-range", "/buildings/0/presentationKey", line: 1);
        }

        [Test]
        public void FractionalNumberIsRejectedAsNotInteger()
        {
            var json = "{\"contentSchemaVersion\":1," +
                       "\"resources\":[{\"id\":\"r\",\"developmentLabel\":\"R\",\"isDevelopmentOnly\":false}]," +
                       "\"recipes\":[{\"id\":\"rec\",\"inputs\":[{\"resourceId\":\"r\",\"amount\":1.0}]," +
                       "\"outputs\":[{\"resourceId\":\"r\",\"amount\":1}],\"durationTicks\":1,\"isDevelopmentOnly\":false}]," +
                       "\"buildings\":[],\"offlinePolicies\":[]}";

            AssertFailure(Bytes(json), "value.not-integer", "/recipes/0/inputs/0/amount", line: 1);
        }

        [Test]
        public void ExponentNumberIsRejectedAsNotInteger()
        {
            var json = "{\"contentSchemaVersion\":1," +
                       "\"resources\":[{\"id\":\"r\",\"developmentLabel\":\"R\",\"isDevelopmentOnly\":false}]," +
                       "\"recipes\":[{\"id\":\"rec\",\"inputs\":[{\"resourceId\":\"r\",\"amount\":1e3}]," +
                       "\"outputs\":[{\"resourceId\":\"r\",\"amount\":1}],\"durationTicks\":1,\"isDevelopmentOnly\":false}]," +
                       "\"buildings\":[],\"offlinePolicies\":[]}";

            AssertFailure(Bytes(json), "value.not-integer", "/recipes/0/inputs/0/amount", line: 1);
        }

        [Test]
        public void OutOfRangeInt64IsRejected()
        {
            var json = "{\"contentSchemaVersion\":1," +
                       "\"resources\":[{\"id\":\"r\",\"developmentLabel\":\"R\",\"isDevelopmentOnly\":false}]," +
                       "\"recipes\":[{\"id\":\"rec\",\"inputs\":[{\"resourceId\":\"r\",\"amount\":99999999999999999999999999999}]," +
                       "\"outputs\":[{\"resourceId\":\"r\",\"amount\":1}],\"durationTicks\":1,\"isDevelopmentOnly\":false}]," +
                       "\"buildings\":[],\"offlinePolicies\":[]}";

            AssertFailure(Bytes(json), "value.out-of-range", "/recipes/0/inputs/0/amount", line: 1);
        }

        [Test]
        public void NullValueIsRejected()
        {
            var json = "{\"contentSchemaVersion\":1,\"resources\":[{\"id\":\"r\",\"developmentLabel\":null," +
                       "\"isDevelopmentOnly\":false}],\"recipes\":[],\"buildings\":[],\"offlinePolicies\":[]}";

            AssertFailure(Bytes(json), "value.null", "/resources/0/developmentLabel", line: 1);
        }

        [Test]
        public void EmptyTopLevelCollectionIsRejected()
        {
            var json = "{\"contentSchemaVersion\":1,\"resources\":[],\"recipes\":[],\"buildings\":[],\"offlinePolicies\":[]}";

            AssertFailure(Bytes(json), "collection.empty", "/resources", line: 1);
        }

        [Test]
        public void TooManyTopLevelEntriesIsRejected()
        {
            var entry = "{\"id\":\"r\",\"developmentLabel\":\"L\",\"isDevelopmentOnly\":false}";
            var entries = string.Join(",", Enumerable.Repeat(entry, GameplayContentLimits.MaximumTopLevelEntryCount + 1));
            var json = "{\"contentSchemaVersion\":1,\"resources\":[" + entries + "]," +
                       "\"recipes\":[],\"buildings\":[],\"offlinePolicies\":[]}";

            AssertFailure(Bytes(json), "collection.too-many", "/resources", line: 1);
        }

        private static void AssertFailure(byte[] bytes, string expectedCode, string expectedPath, int? line)
        {
            var result = GameplayContentReader.Read(bytes);

            Assert.That(result.IsSuccess, Is.False, "Expected a structural failure.");
            Assert.That(result.Diagnostic.Code, Is.EqualTo(expectedCode));
            Assert.That(result.Diagnostic.Path, Is.EqualTo(expectedPath));
            Assert.That(result.Diagnostic.Line, Is.EqualTo(line));
        }

        private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);
    }
}
