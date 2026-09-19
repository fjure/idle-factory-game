using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class CheckpointMappingTests
    {
        private static readonly Guid CheckpointGuid = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid PredecessorGuid = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        [Test]
        public void D001RoundTripPreservesCompleteSemanticState()
        {
            var source = CreateCheckpoint(FoundationFixtures.CreateD001());

            var bytes = SaveV1Mapper.Serialize(source);
            var restored = SaveV1Mapper.Deserialize(bytes.Value, source.CreateFactoryCopy().Catalog);

            Assert.That(bytes.Outcome, Is.EqualTo(CommandOutcome.Success));
            Assert.That(restored.Outcome, Is.EqualTo(CommandOutcome.Success));
            AssertCheckpoint(restored.Checkpoint, source);
        }

        [Test]
        public void NegativeCoordinateAndMultipleBuildingRoundTripPreservesState()
        {
            var single = FoundationFixtures.CreateNegativeCoordinate();
            var catalog = single.Catalog;
            var original = single.Buildings.Values.Single();
            var second = CopyBuilding(original, new BuildingInstanceId("00000000-0000-0000-0000-000000000099"), new GridPosition(-4, -2));
            var factory = new FactoryState(single.Bounds, catalog, new[] { second, original });
            var checkpoint = CreateCheckpoint(factory);

            var firstBytes = SaveV1Mapper.Serialize(checkpoint);
            var restored = SaveV1Mapper.Deserialize(firstBytes.Value, catalog);
            var secondBytes = SaveV1Mapper.Serialize(restored.Checkpoint);

            Assert.That(restored.Outcome, Is.EqualTo(CommandOutcome.Success));
            Assert.That(restored.Checkpoint.CreateFactoryCopy().Bounds.Origin, Is.EqualTo(new GridPosition(-8, -6)));
            Assert.That(restored.Checkpoint.CreateFactoryCopy().Buildings.Values.Select(x => x.Anchor),
                Is.EquivalentTo(new[] { new GridPosition(-7, -5), new GridPosition(-4, -2) }));
            Assert.That(secondBytes.Value, Is.EqualTo(firstBytes.Value));
        }

        [Test]
        public void SerializationIsDeterministicAndContainsOnlyContractMembers()
        {
            var source = CreateCheckpoint(FoundationFixtures.CreateD001());
            var first = SaveV1Mapper.Serialize(source);
            var second = SaveV1Mapper.Serialize(source);
            var json = Utf8(first.Value);

            Assert.That(second.Value, Is.EqualTo(first.Value));
            Assert.That(json, Does.StartWith("{\"schemaVersion\":1,\"checkpointId\":"));
            Assert.That(json, Does.Contain("\"reconciledFromCheckpointId\":\"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb\""));
            Assert.That(json, Does.Not.Contain("capacity"));
            Assert.That(json, Does.Not.Contain("footprint"));
            Assert.That(json, Does.Not.Contain("occupancy"));
            Assert.That(json, Does.Not.Contain("selection"));
        }

        [Test]
        public void InputLargerThanSixteenMiBIsRejectedBeforeDecode()
        {
            var oversized = new byte[SaveV1Mapper.MaximumByteCount + 1];
            for (var index = 0; index < oversized.Length; index++) oversized[index] = 0xff;

            AssertInvalid(oversized, FoundationFixtures.CreateD001().Catalog, "structural");
        }

        [Test]
        public void InvalidUtf8CommentsAndTrailingContentAreRejected()
        {
            var catalog = FoundationFixtures.CreateD001().Catalog;
            AssertInvalid(new byte[] { 0x7b, 0xc3, 0x28, 0x7d }, catalog, "structural");
            AssertInvalid(Bytes(ValidJson().Replace("{\"schemaVersion\"", "{/*comment*/\"schemaVersion\"")), catalog, "structural");
            AssertInvalid(Bytes(ValidJson() + "{}"), catalog, "structural");
        }

        [Test]
        public void FutureSchemaDispatchAllowsDifferentShapeAndVersionLast()
        {
            var result = Deserialize("{\"futureState\":{},\"schemaVersion\":2}");

            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.UnsupportedSaveVersion));
            Assert.That(result.Message, Is.EqualTo("Save V1 schema version is unsupported."));
            Assert.That(result.Checkpoint, Is.Null);
        }

        [TestCase("{\"futureState\":{}}")]
        [TestCase("{\"schemaVersion\":\"2\",\"futureState\":{}}")]
        [TestCase("{\"schemaVersion\":2,\"schemaVersion\":2}")]
        public void MissingWrongOrDuplicateSchemaIsInvalidCheckpoint(string json)
        {
            AssertStructural(json);
        }

        [TestCase("{\"schemaVersion\":2}garbage")]
        [TestCase("{\"schemaVersion\":2}{}")]
        [TestCase("{\"schemaVersion\":2,}")]
        public void MalformedOrTrailingContentTakesPrecedenceOverUnsupportedVersion(string json)
        {
            AssertStructural(json);
        }

        [Test]
        public void SingleQuotedTokensAndTrailingCommasAreRejectedAtEveryContainerKind()
        {
            AssertStructural(ValidJson().Replace("\"schemaVersion\"", "'schemaVersion'"));
            AssertStructural(ValidJson().Replace("\"" + CheckPointText + "\"", "'" + CheckPointText + "'"));
            AssertStructural(ValidJson().Substring(0, ValidJson().Length - 1) + ",}");
            AssertStructural(Replace("\"progressTicks\":0}", "\"progressTicks\":0,}"));
            AssertStructural(Replace("\"quantity\":4}]", "\"quantity\":4},]"));
        }

        [Test]
        public void OverlongEarlyIdentifierIsRejectedBeforeLargeTailIsConsumedIntoDocumentString()
        {
            var json = Replace(CheckPointText, new string('x', SaveV1Mapper.MaximumIdentifierLength + 1));
            var bytes = Bytes(json + new string(' ', 4 * 1024 * 1024));

            Assert.That(bytes.Length, Is.GreaterThan(4 * 1024 * 1024));
            AssertInvalid(bytes, FoundationFixtures.CreateD001().Catalog, "structural");
        }

        [Test]
        public void IdentifierPreallocationGuardCountsEscapesAndUtf16SurrogateUnits()
        {
            var escapedOverlong = string.Concat(Enumerable.Repeat("\\u0061", SaveV1Mapper.MaximumIdentifierLength + 1));
            var surrogateOverlong = string.Concat(Enumerable.Repeat("\\ud83d\\ude00", 65));
            var exactLimit = new string('x', SaveV1Mapper.MaximumIdentifierLength);

            AssertStructural(Replace("dev.machine.basic", escapedOverlong));
            AssertStructural(Replace("dev.machine.basic", surrogateOverlong));
            AssertSemantic(Replace("dev.machine.basic", exactLimit));
        }

        [Test]
        public void JsonDeeperThanMaximumJsonDepthHasStableStructuralRejection()
        {
            var nestedValue = new string('[', 32) + "0" + new string(']', 32);
            var json = Replace("\"factory\":{", "\"factory\":" + nestedValue + ",\"discarded\":{");

            AssertStructural(json);
        }

        [TestCase("{\"schemaVersion\":1}")]
        [TestCase("null")]
        [TestCase("[]")]
        public void MissingNullOrWrongRootShapeIsRejected(string json)
        {
            AssertInvalid(Bytes(json), FoundationFixtures.CreateD001().Catalog, "structural");
        }

        [Test]
        public void WrongTypesNonIntegralAndOverflowNumbersAreRejected()
        {
            AssertStructural(Replace("\"generation\":7", "\"generation\":1.5"));
            AssertStructural(Replace("\"generation\":7", "\"generation\":9223372036854775808"));
            AssertStructural(Replace("\"width\":16", "\"width\":2147483648"));
            AssertStructural(Replace("\"isActive\":true", "\"isActive\":1"));
            AssertStructural(Replace("\"factory\":{", "\"factory\":null,"));
        }

        [Test]
        public void UnknownAndDuplicateMembersAreRejectedAtEveryObjectLevel()
        {
            AssertStructural(Replace("{\"schemaVersion\":1", "{\"unknown\":0,\"schemaVersion\":1"));
            AssertStructural(Replace("{\"schemaVersion\":1", "{\"schemaVersion\":1,\"schemaVersion\":1"));
            AssertStructural(Replace("\"factory\":{", "\"factory\":{\"unknown\":0,"));
            AssertStructural(Replace("\"factory\":{", "\"factory\":{\"width\":16,"));
            AssertStructural(Replace("\"instanceId\":", "\"unknown\":0,\"instanceId\":"));
            AssertStructural(Replace("\"instanceId\":", "\"anchorX\":0,\"instanceId\":"));
            AssertStructural(Replace("{\"resourceId\":", "{\"unknown\":0,\"resourceId\":"));
            AssertStructural(Replace("{\"resourceId\":", "{\"quantity\":4,\"resourceId\":"));
            AssertStructural(Replace("\"production\":{", "\"production\":{\"unknown\":0,"));
            AssertStructural(Replace("\"production\":{", "\"production\":{\"progressTicks\":0,"));
        }

        [Test]
        public void UnsupportedSchemaVersionHasDistinctTypedOutcome()
        {
            var result = Deserialize(Replace("\"schemaVersion\":1", "\"schemaVersion\":2"));

            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.UnsupportedSaveVersion));
            Assert.That(result.Message, Is.EqualTo("Save V1 schema version is unsupported."));
            Assert.That(result.Checkpoint, Is.Null);
        }

        [Test]
        public void MetadataRangesAndIdentifiersAreStrictlyValidated()
        {
            AssertSemantic(Replace("\"generation\":7", "\"generation\":-1"));
            AssertSemantic(Replace("\"departureUtcUnixMilliseconds\":1234", "\"departureUtcUnixMilliseconds\":9223372036854775807"));
            AssertSemantic(Replace("\"offlinePolicyRevision\":1", "\"offlinePolicyRevision\":0"));
            AssertSemantic(Replace(CheckPointText, Guid.Empty.ToString("D")));
            AssertSemantic(Replace(CheckPointText, "AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"));
            AssertSemantic(Replace(PredecessorText, CheckPointText));
            AssertStructural(Replace("\"definitionId\":\"dev.machine.basic\"", "\"definitionId\":\"" + new string('x', 129) + "\""));
        }

        [Test]
        public void SerializationRejectsSubMillisecondDepartureWithoutApproximation()
        {
            var factory = FoundationFixtures.CreateD001();
            var checkpoint = CreateCheckpoint(factory, DateTimeOffset.UnixEpoch.AddTicks(1));

            var result = SaveV1Mapper.Serialize(checkpoint);

            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(result.Message, Is.EqualTo("Save V1 semantic validation failed."));
            Assert.That(result.Value, Is.Null);
        }

        [Test]
        public void FactoryDimensionsAndCoordinateOverflowAreRejected()
        {
            AssertSemantic(Replace("\"width\":16", "\"width\":0"));
            AssertSemantic(Replace("\"height\":16", "\"height\":-1"));
            AssertSemantic(Replace("\"originX\":0", "\"originX\":2147483647"));
        }

        [Test]
        public void CatalogPolicyOrientationRecipeAndResourceMismatchesAreRejected()
        {
            AssertSemantic(Replace("\"offlinePolicyRevision\":1", "\"offlinePolicyRevision\":2"));
            AssertSemantic(Replace("\"definitionId\":\"dev.machine.basic\"", "\"definitionId\":\"missing\""));
            AssertSemantic(Replace("\"orientationQuarterTurns\":0", "\"orientationQuarterTurns\":1"));
            AssertSemantic(Replace("\"recipeId\":\"dev.recipe.basic\"", "\"recipeId\":\"missing\""));
            AssertSemantic(Replace("\"resourceId\":\"dev.input\"", "\"resourceId\":\"missing\""));
        }

        [Test]
        public void BufferSetsDuplicatesQuantitiesAndEntryLimitAreRejected()
        {
            AssertSemantic(Replace("{\"resourceId\":\"dev.input\",\"quantity\":4}", string.Empty));
            AssertSemantic(Replace("{\"resourceId\":\"dev.input\",\"quantity\":4}",
                "{\"resourceId\":\"dev.input\",\"quantity\":4},{\"resourceId\":\"dev.input\",\"quantity\":0}"));
            AssertSemantic(Replace("\"quantity\":4", "\"quantity\":5"));
            AssertSemantic(Replace("\"quantity\":4", "\"quantity\":-1"));

            var entries = string.Join(",", Enumerable.Range(0, SaveV1Mapper.MaximumBufferEntryCount + 1)
                .Select(index => "{\"resourceId\":\"r" + index + "\",\"quantity\":0}"));
            AssertStructural(Replace("{\"resourceId\":\"dev.input\",\"quantity\":4}", entries));
        }

        [Test]
        public void BuildingCountLimitIsCheckedWithoutConstructingDomainEntries()
        {
            var validBuilding = BuildingJson();
            var builder = new StringBuilder(ValidJson().Length + (validBuilding.Length + 1) * 100000);
            var json = ValidJson();
            var start = json.IndexOf(validBuilding, StringComparison.Ordinal);
            builder.Append(json, 0, start);
            for (var index = 0; index <= SaveV1Mapper.MaximumBuildingCount; index++)
            {
                if (index != 0) builder.Append(',');
                builder.Append(validBuilding);
            }
            builder.Append(json, start + validBuilding.Length, json.Length - start - validBuilding.Length);

            var result = SaveV1Mapper.Deserialize(Bytes(builder.ToString()), FoundationFixtures.CreateD001().Catalog);
            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(result.Checkpoint, Is.Null);
        }

        [Test]
        public void DuplicateBuildingIdsOverlapAndOutOfBoundsAreRejected()
        {
            var building = BuildingJson();
            AssertSemantic(Replace(building, building + "," + building));

            var second = building.Replace("00000001-0000-0000-0000-000000000000", "00000002-0000-0000-0000-000000000000");
            AssertSemantic(Replace(building, building + "," + second));
            AssertSemantic(Replace("\"anchorX\":0", "\"anchorX\":15"));
        }

        [Test]
        public void ProgressAtDurationAndInvalidProgressAreRejectedWithoutClamping()
        {
            AssertSemantic(Replace("\"progressTicks\":0", "\"progressTicks\":50000000"));
            AssertSemantic(Replace("\"progressTicks\":0", "\"progressTicks\":-1"));
        }

        [Test]
        public void InvalidCandidateNeverMutatesSourceAndSuccessfulCandidateIsFresh()
        {
            var sourceFactory = FoundationFixtures.CreateD001();
            var source = CreateCheckpoint(sourceFactory);
            var before = SaveV1Mapper.Serialize(source).Value;

            var invalid = SaveV1Mapper.Deserialize(Bytes(Replace("\"quantity\":4", "\"quantity\":999")), sourceFactory.Catalog);
            var valid = SaveV1Mapper.Deserialize(before, sourceFactory.Catalog);
            var after = SaveV1Mapper.Serialize(source).Value;

            Assert.That(invalid.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(after, Is.EqualTo(before));
            Assert.That(valid.Checkpoint, Is.Not.SameAs(source));
            Assert.That(valid.Checkpoint.CreateFactoryCopy(), Is.Not.SameAs(sourceFactory));
            Assert.That(valid.Checkpoint.CreateFactoryCopy().Buildings.Values.Single(),
                Is.Not.SameAs(sourceFactory.Buildings.Values.Single()));
        }

        [Test]
        public void DiagnosticsAreStableAndDoNotLeakRawJson()
        {
            const string secret = "private-player-content";
            var result = Deserialize(Replace("\"definitionId\":\"dev.machine.basic\"", "\"definitionId\":\"" + secret + "\""));

            Assert.That(result.Message, Is.EqualTo("Save V1 semantic validation failed."));
            Assert.That(result.Message, Does.Not.Contain(secret));
            Assert.That(result.Message, Does.Not.Contain("{"));
        }

        private const string CheckPointText = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        private const string PredecessorText = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

        private static Checkpoint CreateCheckpoint(FactoryState factory)
        {
            return CreateCheckpoint(factory, DateTimeOffset.FromUnixTimeMilliseconds(1234));
        }

        private static Checkpoint CreateCheckpoint(FactoryState factory, DateTimeOffset departureUtc)
        {
            return new Checkpoint(
                new CheckpointId(CheckpointGuid),
                7,
                departureUtc,
                new OfflinePolicyRevision(1),
                new CheckpointId(PredecessorGuid),
                factory);
        }

        private static BuildingInstanceState CopyBuilding(BuildingInstanceState source, BuildingInstanceId id, GridPosition anchor)
        {
            return new BuildingInstanceState(
                id,
                source.Definition,
                anchor,
                source.OrientationQuarterTurns,
                source.IsActive,
                new ResourceBufferState(source.InputBuffer.Definition, source.InputBuffer.Quantities),
                new ResourceBufferState(source.OutputBuffer.Definition, source.OutputBuffer.Quantities),
                new ProductionState(source.Production.Definition, source.Production.ProgressTicks));
        }

        private static void AssertCheckpoint(Checkpoint actual, Checkpoint expected)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.Generation, Is.EqualTo(expected.Generation));
            Assert.That(actual.DepartureUtc, Is.EqualTo(expected.DepartureUtc));
            Assert.That(actual.OfflinePolicyRevision, Is.EqualTo(expected.OfflinePolicyRevision));
            Assert.That(actual.ReconciledFromCheckpointId, Is.EqualTo(expected.ReconciledFromCheckpointId));
            var actualFactory = actual.CreateFactoryCopy();
            var expectedFactory = expected.CreateFactoryCopy();
            Assert.That(actualFactory.Bounds, Is.EqualTo(expectedFactory.Bounds));
            Assert.That(actualFactory.Buildings.Keys, Is.EquivalentTo(expectedFactory.Buildings.Keys));
            foreach (var pair in expectedFactory.Buildings)
            {
                var restored = actualFactory.Buildings[pair.Key];
                Assert.That(restored.DefinitionId, Is.EqualTo(pair.Value.DefinitionId));
                Assert.That(restored.Anchor, Is.EqualTo(pair.Value.Anchor));
                Assert.That(restored.IsActive, Is.EqualTo(pair.Value.IsActive));
                Assert.That(restored.InputBuffer.Quantities, Is.EquivalentTo(pair.Value.InputBuffer.Quantities));
                Assert.That(restored.OutputBuffer.Quantities, Is.EquivalentTo(pair.Value.OutputBuffer.Quantities));
                Assert.That(restored.Production.ProgressTicks, Is.EqualTo(pair.Value.Production.ProgressTicks));
            }
            Assert.That(actualFactory.Occupancy, Is.EquivalentTo(expectedFactory.Occupancy));
        }

        private static string ValidJson()
        {
            return Utf8(SaveV1Mapper.Serialize(CreateCheckpoint(FoundationFixtures.CreateD001())).Value);
        }

        private static string BuildingJson()
        {
            var json = ValidJson();
            var marker = "{\"instanceId\":";
            var start = json.IndexOf(marker, StringComparison.Ordinal);
            var end = json.IndexOf("}]}}", start, StringComparison.Ordinal);
            return json.Substring(start, end - start + 1);
        }

        private static string Replace(string oldValue, string newValue)
        {
            var json = ValidJson();
            Assert.That(json, Does.Contain(oldValue));
            return json.Replace(oldValue, newValue);
        }

        private static CheckpointReadResult Deserialize(string json)
        {
            return SaveV1Mapper.Deserialize(Bytes(json), FoundationFixtures.CreateD001().Catalog);
        }

        private static void AssertStructural(string json)
        {
            var result = Deserialize(json);
            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(result.Message, Is.EqualTo("Save V1 structural validation failed."));
            Assert.That(result.Checkpoint, Is.Null);
        }

        private static void AssertSemantic(string json)
        {
            var result = Deserialize(json);
            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(result.Message, Is.EqualTo("Save V1 semantic validation failed."));
            Assert.That(result.Checkpoint, Is.Null);
        }

        private static void AssertInvalid(byte[] bytes, DefinitionCatalog catalog, string category)
        {
            var result = SaveV1Mapper.Deserialize(bytes, catalog);
            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(result.Message, Is.EqualTo("Save V1 " + category + " validation failed."));
            Assert.That(result.Checkpoint, Is.Null);
        }

        private static byte[] Bytes(string value) => new UTF8Encoding(false, true).GetBytes(value);
        private static string Utf8(byte[] value) => new UTF8Encoding(false, true).GetString(value);
    }
}
