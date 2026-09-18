using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using Newtonsoft.Json;

namespace IdleFactory.Unity.Persistence
{
    public static class SaveV1Mapper
    {
        public const int MaximumByteCount = 16 * 1024 * 1024;
        public const int MaximumBuildingCount = 100000;
        public const int MaximumBufferEntryCount = 256;
        public const int MaximumIdentifierLength = 128;

        private const int MaximumJsonDepth = 16;
        private const string StructuralFailure = "Save V1 structural validation failed.";
        private const string SemanticFailure = "Save V1 semantic validation failed.";
        private const string UnsupportedVersion = "Save V1 schema version is unsupported.";
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static CheckpointReadResult Deserialize(byte[] bytes, DefinitionCatalog catalog)
        {
            if (bytes == null || catalog == null)
            {
                return CheckpointReadResult.Rejected(CommandOutcome.InvalidCheckpoint, StructuralFailure);
            }

            if (bytes.Length > MaximumByteCount)
            {
                return CheckpointReadResult.Rejected(CommandOutcome.InvalidCheckpoint, StructuralFailure);
            }

            SaveV1Dto dto;
            try
            {
                var json = StrictUtf8.GetString(bytes);
                using (var textReader = new StringReader(json))
                using (var reader = new JsonTextReader(textReader)
                {
                    DateParseHandling = DateParseHandling.None,
                    FloatParseHandling = FloatParseHandling.Decimal,
                    MaxDepth = MaximumJsonDepth,
                    SupportMultipleContent = false,
                })
                {
                    ReadNext(reader);
                    dto = ReadSave(reader);
                    if (reader.Read())
                    {
                        throw new SaveStructureException();
                    }
                }
            }
            catch (Exception exception) when (IsStructuralFailure(exception))
            {
                return CheckpointReadResult.Rejected(CommandOutcome.InvalidCheckpoint, StructuralFailure);
            }

            if (dto.SchemaVersion != 1)
            {
                return CheckpointReadResult.Rejected(CommandOutcome.UnsupportedSaveVersion, UnsupportedVersion);
            }

            try
            {
                return CheckpointReadResult.Success(BuildCheckpoint(dto, catalog));
            }
            catch (Exception exception) when (IsSemanticFailure(exception))
            {
                return CheckpointReadResult.Rejected(CommandOutcome.InvalidCheckpoint, SemanticFailure);
            }
        }

        public static CommandResult<byte[]> Serialize(Checkpoint checkpoint)
        {
            if (checkpoint == null)
            {
                return CommandResult<byte[]>.Rejected(CommandOutcome.InvalidCheckpoint, SemanticFailure);
            }

            try
            {
                var dto = BuildDto(checkpoint);
                _ = BuildCheckpoint(dto, checkpoint.CreateFactoryCopy().Catalog);
                var builder = new StringBuilder();
                using (var textWriter = new StringWriter(builder, CultureInfo.InvariantCulture))
                using (var writer = new JsonTextWriter(textWriter)
                {
                    Culture = CultureInfo.InvariantCulture,
                    Formatting = Formatting.None,
                    StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
                })
                {
                    WriteSave(writer, dto);
                    writer.Flush();
                }

                var bytes = StrictUtf8.GetBytes(builder.ToString());
                if (bytes.Length > MaximumByteCount)
                {
                    return CommandResult<byte[]>.Rejected(CommandOutcome.InvalidCheckpoint, SemanticFailure);
                }

                return CommandResult<byte[]>.Success(bytes);
            }
            catch (Exception exception) when (IsSemanticFailure(exception))
            {
                return CommandResult<byte[]>.Rejected(CommandOutcome.InvalidCheckpoint, SemanticFailure);
            }
        }

        private static SaveV1Dto ReadSave(JsonTextReader reader)
        {
            RequireToken(reader, JsonToken.StartObject);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int? schemaVersion = null;
            string checkpointId = null;
            long? generation = null;
            long? departure = null;
            int? policyRevision = null;
            string predecessor = null;
            var predecessorRead = false;
            FactoryV1Dto factory = null;

            while (ReadObjectMember(reader, seen, out var member))
            {
                switch (member)
                {
                    case "schemaVersion": schemaVersion = ReadInt32(reader); break;
                    case "checkpointId": checkpointId = ReadString(reader); break;
                    case "generation": generation = ReadInt64(reader); break;
                    case "departureUtcUnixMilliseconds": departure = ReadInt64(reader); break;
                    case "offlinePolicyRevision": policyRevision = ReadInt32(reader); break;
                    case "reconciledFromCheckpointId":
                        predecessorRead = true;
                        predecessor = ReadNullableString(reader);
                        break;
                    case "factory": ReadNext(reader); factory = ReadFactory(reader); break;
                    default: throw new SaveStructureException();
                }
            }

            if (!schemaVersion.HasValue || checkpointId == null || !generation.HasValue || !departure.HasValue ||
                !policyRevision.HasValue || !predecessorRead || factory == null || seen.Count != 7)
            {
                throw new SaveStructureException();
            }

            return new SaveV1Dto(schemaVersion.Value, checkpointId, generation.Value, departure.Value,
                policyRevision.Value, predecessor, factory);
        }

        private static FactoryV1Dto ReadFactory(JsonTextReader reader)
        {
            RequireToken(reader, JsonToken.StartObject);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int? originX = null;
            int? originY = null;
            int? width = null;
            int? height = null;
            IReadOnlyList<BuildingV1Dto> buildings = null;

            while (ReadObjectMember(reader, seen, out var member))
            {
                switch (member)
                {
                    case "originX": originX = ReadInt32(reader); break;
                    case "originY": originY = ReadInt32(reader); break;
                    case "width": width = ReadInt32(reader); break;
                    case "height": height = ReadInt32(reader); break;
                    case "buildings": buildings = ReadBuildings(reader); break;
                    default: throw new SaveStructureException();
                }
            }

            if (!originX.HasValue || !originY.HasValue || !width.HasValue || !height.HasValue ||
                buildings == null || seen.Count != 5)
            {
                throw new SaveStructureException();
            }

            return new FactoryV1Dto(originX.Value, originY.Value, width.Value, height.Value, buildings);
        }

        private static IReadOnlyList<BuildingV1Dto> ReadBuildings(JsonTextReader reader)
        {
            ReadNext(reader);
            RequireToken(reader, JsonToken.StartArray);
            var buildings = new List<BuildingV1Dto>();
            while (true)
            {
                ReadNext(reader);
                if (reader.TokenType == JsonToken.EndArray)
                {
                    return buildings.AsReadOnly();
                }

                if (buildings.Count == MaximumBuildingCount)
                {
                    throw new SaveStructureException();
                }

                buildings.Add(ReadBuilding(reader));
            }
        }

        private static BuildingV1Dto ReadBuilding(JsonTextReader reader)
        {
            RequireToken(reader, JsonToken.StartObject);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string instanceId = null;
            string definitionId = null;
            int? anchorX = null;
            int? anchorY = null;
            int? orientation = null;
            bool? active = null;
            IReadOnlyList<ResourceQuantityV1Dto> inputs = null;
            IReadOnlyList<ResourceQuantityV1Dto> outputs = null;
            ProductionV1Dto production = null;

            while (ReadObjectMember(reader, seen, out var member))
            {
                switch (member)
                {
                    case "instanceId": instanceId = ReadString(reader); break;
                    case "definitionId": definitionId = ReadString(reader); break;
                    case "anchorX": anchorX = ReadInt32(reader); break;
                    case "anchorY": anchorY = ReadInt32(reader); break;
                    case "orientationQuarterTurns": orientation = ReadInt32(reader); break;
                    case "isActive": active = ReadBoolean(reader); break;
                    case "inputQuantities": inputs = ReadQuantities(reader); break;
                    case "outputQuantities": outputs = ReadQuantities(reader); break;
                    case "production": ReadNext(reader); production = ReadProduction(reader); break;
                    default: throw new SaveStructureException();
                }
            }

            if (instanceId == null || definitionId == null || !anchorX.HasValue || !anchorY.HasValue ||
                !orientation.HasValue || !active.HasValue || inputs == null || outputs == null ||
                production == null || seen.Count != 9)
            {
                throw new SaveStructureException();
            }

            return new BuildingV1Dto(instanceId, definitionId, anchorX.Value, anchorY.Value,
                orientation.Value, active.Value, inputs, outputs, production);
        }

        private static IReadOnlyList<ResourceQuantityV1Dto> ReadQuantities(JsonTextReader reader)
        {
            ReadNext(reader);
            RequireToken(reader, JsonToken.StartArray);
            var quantities = new List<ResourceQuantityV1Dto>();
            while (true)
            {
                ReadNext(reader);
                if (reader.TokenType == JsonToken.EndArray)
                {
                    return quantities.AsReadOnly();
                }

                if (quantities.Count == MaximumBufferEntryCount)
                {
                    throw new SaveStructureException();
                }

                quantities.Add(ReadQuantity(reader));
            }
        }

        private static ResourceQuantityV1Dto ReadQuantity(JsonTextReader reader)
        {
            RequireToken(reader, JsonToken.StartObject);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string resourceId = null;
            long? quantity = null;
            while (ReadObjectMember(reader, seen, out var member))
            {
                switch (member)
                {
                    case "resourceId": resourceId = ReadString(reader); break;
                    case "quantity": quantity = ReadInt64(reader); break;
                    default: throw new SaveStructureException();
                }
            }

            if (resourceId == null || !quantity.HasValue || seen.Count != 2)
            {
                throw new SaveStructureException();
            }

            return new ResourceQuantityV1Dto(resourceId, quantity.Value);
        }

        private static ProductionV1Dto ReadProduction(JsonTextReader reader)
        {
            RequireToken(reader, JsonToken.StartObject);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string recipeId = null;
            long? progress = null;
            while (ReadObjectMember(reader, seen, out var member))
            {
                switch (member)
                {
                    case "recipeId": recipeId = ReadString(reader); break;
                    case "progressTicks": progress = ReadInt64(reader); break;
                    default: throw new SaveStructureException();
                }
            }

            if (recipeId == null || !progress.HasValue || seen.Count != 2)
            {
                throw new SaveStructureException();
            }

            return new ProductionV1Dto(recipeId, progress.Value);
        }

        private static Checkpoint BuildCheckpoint(SaveV1Dto dto, DefinitionCatalog catalog)
        {
            var checkpointId = ParseCheckpointId(dto.CheckpointId);
            if (dto.Generation < 0 || dto.OfflinePolicyRevision <= 0)
            {
                throw new SaveSemanticException();
            }

            DateTimeOffset departure;
            try
            {
                departure = DateTimeOffset.FromUnixTimeMilliseconds(dto.DepartureUtcUnixMilliseconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new SaveSemanticException();
            }

            var policyRevision = new OfflinePolicyRevision(dto.OfflinePolicyRevision);
            catalog.Resolve(policyRevision);
            CheckpointId? predecessor = null;
            if (dto.ReconciledFromCheckpointId != null)
            {
                predecessor = ParseCheckpointId(dto.ReconciledFromCheckpointId);
                if (predecessor.Value == checkpointId)
                {
                    throw new SaveSemanticException();
                }
            }

            if (dto.Factory.Width <= 0 || dto.Factory.Height <= 0)
            {
                throw new SaveSemanticException();
            }

            FactoryBounds bounds;
            try
            {
                bounds = new FactoryBounds(
                    new GridPosition(dto.Factory.OriginX, dto.Factory.OriginY),
                    dto.Factory.Width,
                    dto.Factory.Height);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException)
            {
                throw new SaveSemanticException();
            }

            var buildings = new List<BuildingInstanceState>(dto.Factory.Buildings.Count);
            var instanceIds = new HashSet<BuildingInstanceId>();
            foreach (var buildingDto in dto.Factory.Buildings)
            {
                var instanceId = ParseBuildingInstanceId(buildingDto.InstanceId);
                if (!instanceIds.Add(instanceId))
                {
                    throw new SaveSemanticException();
                }

                var definitionId = ParseDefinitionId(buildingDto.DefinitionId);
                var definition = catalog.Resolve(definitionId);
                if (!definition.AllowedOrientations.Contains(buildingDto.OrientationQuarterTurns))
                {
                    throw new SaveSemanticException();
                }

                if (!definition.RecipeId.HasValue)
                {
                    throw new SaveSemanticException();
                }

                var recipeId = ParseRecipeId(buildingDto.Production.RecipeId);
                var recipe = catalog.Resolve(recipeId);
                if (recipeId != definition.RecipeId.Value || buildingDto.Production.ProgressTicks < 0 ||
                    buildingDto.Production.ProgressTicks >= recipe.DurationTicks)
                {
                    throw new SaveSemanticException();
                }

                var input = BuildBuffer(buildingDto.InputQuantities, definition.InputBuffer, catalog);
                var output = BuildBuffer(buildingDto.OutputQuantities, definition.OutputBuffer, catalog);
                buildings.Add(new BuildingInstanceState(
                    instanceId,
                    definition,
                    new GridPosition(buildingDto.AnchorX, buildingDto.AnchorY),
                    buildingDto.OrientationQuarterTurns,
                    buildingDto.IsActive,
                    input,
                    output,
                    new ProductionState(recipe, buildingDto.Production.ProgressTicks)));
            }

            var factory = new FactoryState(bounds, catalog, buildings);
            return new Checkpoint(checkpointId, dto.Generation, departure, policyRevision, predecessor, factory);
        }

        private static ResourceBufferState BuildBuffer(
            IReadOnlyList<ResourceQuantityV1Dto> entries,
            BufferDefinition definition,
            DefinitionCatalog catalog)
        {
            if (definition == null)
            {
                if (entries.Count != 0)
                {
                    throw new SaveSemanticException();
                }

                return null;
            }

            var quantities = new Dictionary<ResourceId, long>();
            foreach (var entry in entries)
            {
                var resourceId = ParseResourceId(entry.ResourceId);
                catalog.Resolve(resourceId);
                if (!definition.Capacities.TryGetValue(resourceId, out var capacity) ||
                    entry.Quantity < 0 || entry.Quantity > capacity || quantities.ContainsKey(resourceId))
                {
                    throw new SaveSemanticException();
                }

                quantities.Add(resourceId, entry.Quantity);
            }

            if (quantities.Count != definition.Capacities.Count)
            {
                throw new SaveSemanticException();
            }

            return new ResourceBufferState(definition, quantities);
        }

        private static SaveV1Dto BuildDto(Checkpoint checkpoint)
        {
            var factory = checkpoint.CreateFactoryCopy();
            factory.Catalog.Resolve(checkpoint.OfflinePolicyRevision);
            var buildings = factory.Buildings.Values
                .OrderBy(building => building.Id.Value, StringComparer.Ordinal)
                .Select(BuildBuildingDto)
                .ToList()
                .AsReadOnly();
            if (buildings.Count > MaximumBuildingCount)
            {
                throw new SaveSemanticException();
            }

            var predecessor = checkpoint.ReconciledFromCheckpointId.HasValue
                ? checkpoint.ReconciledFromCheckpointId.Value.Value
                : null;
            return new SaveV1Dto(
                1,
                checkpoint.Id.Value,
                checkpoint.Generation,
                checkpoint.DepartureUtc.ToUniversalTime().ToUnixTimeMilliseconds(),
                checkpoint.OfflinePolicyRevision.Value,
                predecessor,
                new FactoryV1Dto(
                    factory.Bounds.Origin.X,
                    factory.Bounds.Origin.Y,
                    factory.Bounds.Width,
                    factory.Bounds.Height,
                    buildings));
        }

        private static BuildingV1Dto BuildBuildingDto(BuildingInstanceState building)
        {
            if (building.Production == null)
            {
                throw new SaveSemanticException();
            }

            return new BuildingV1Dto(
                building.Id.Value,
                building.DefinitionId.Value,
                building.Anchor.X,
                building.Anchor.Y,
                building.OrientationQuarterTurns,
                building.IsActive,
                BuildQuantityDtos(building.InputBuffer),
                BuildQuantityDtos(building.OutputBuffer),
                new ProductionV1Dto(building.Production.RecipeId.Value, building.Production.ProgressTicks));
        }

        private static IReadOnlyList<ResourceQuantityV1Dto> BuildQuantityDtos(ResourceBufferState buffer)
        {
            if (buffer == null)
            {
                return Array.Empty<ResourceQuantityV1Dto>();
            }

            if (buffer.Quantities.Count > MaximumBufferEntryCount)
            {
                throw new SaveSemanticException();
            }

            return buffer.Quantities
                .OrderBy(quantity => quantity.Key.Value, StringComparer.Ordinal)
                .Select(quantity => new ResourceQuantityV1Dto(quantity.Key.Value, quantity.Value))
                .ToList()
                .AsReadOnly();
        }

        private static void WriteSave(JsonTextWriter writer, SaveV1Dto dto)
        {
            writer.WriteStartObject();
            WriteProperty(writer, "schemaVersion", dto.SchemaVersion);
            WriteProperty(writer, "checkpointId", dto.CheckpointId);
            WriteProperty(writer, "generation", dto.Generation);
            WriteProperty(writer, "departureUtcUnixMilliseconds", dto.DepartureUtcUnixMilliseconds);
            WriteProperty(writer, "offlinePolicyRevision", dto.OfflinePolicyRevision);
            writer.WritePropertyName("reconciledFromCheckpointId");
            if (dto.ReconciledFromCheckpointId == null) writer.WriteNull(); else writer.WriteValue(dto.ReconciledFromCheckpointId);
            writer.WritePropertyName("factory");
            writer.WriteStartObject();
            WriteProperty(writer, "originX", dto.Factory.OriginX);
            WriteProperty(writer, "originY", dto.Factory.OriginY);
            WriteProperty(writer, "width", dto.Factory.Width);
            WriteProperty(writer, "height", dto.Factory.Height);
            writer.WritePropertyName("buildings");
            writer.WriteStartArray();
            foreach (var building in dto.Factory.Buildings)
            {
                writer.WriteStartObject();
                WriteProperty(writer, "instanceId", building.InstanceId);
                WriteProperty(writer, "definitionId", building.DefinitionId);
                WriteProperty(writer, "anchorX", building.AnchorX);
                WriteProperty(writer, "anchorY", building.AnchorY);
                WriteProperty(writer, "orientationQuarterTurns", building.OrientationQuarterTurns);
                WriteProperty(writer, "isActive", building.IsActive);
                WriteQuantities(writer, "inputQuantities", building.InputQuantities);
                WriteQuantities(writer, "outputQuantities", building.OutputQuantities);
                writer.WritePropertyName("production");
                writer.WriteStartObject();
                WriteProperty(writer, "recipeId", building.Production.RecipeId);
                WriteProperty(writer, "progressTicks", building.Production.ProgressTicks);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        private static void WriteQuantities(JsonTextWriter writer, string name, IEnumerable<ResourceQuantityV1Dto> quantities)
        {
            writer.WritePropertyName(name);
            writer.WriteStartArray();
            foreach (var quantity in quantities)
            {
                writer.WriteStartObject();
                WriteProperty(writer, "resourceId", quantity.ResourceId);
                WriteProperty(writer, "quantity", quantity.Quantity);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        private static void WriteProperty(JsonTextWriter writer, string name, object value)
        {
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        private static bool ReadObjectMember(JsonTextReader reader, HashSet<string> seen, out string member)
        {
            ReadNext(reader);
            if (reader.TokenType == JsonToken.EndObject)
            {
                member = null;
                return false;
            }

            RequireToken(reader, JsonToken.PropertyName);
            member = (string)reader.Value;
            if (!seen.Add(member))
            {
                throw new SaveStructureException();
            }

            return true;
        }

        private static int ReadInt32(JsonTextReader reader)
        {
            var value = ReadInt64(reader);
            if (value < int.MinValue || value > int.MaxValue)
            {
                throw new SaveStructureException();
            }
            return (int)value;
        }

        private static long ReadInt64(JsonTextReader reader)
        {
            ReadNext(reader);
            RequireToken(reader, JsonToken.Integer);
            if (!(reader.Value is long value))
            {
                throw new SaveStructureException();
            }
            return value;
        }

        private static bool ReadBoolean(JsonTextReader reader)
        {
            ReadNext(reader);
            RequireToken(reader, JsonToken.Boolean);
            return (bool)reader.Value;
        }

        private static string ReadString(JsonTextReader reader)
        {
            ReadNext(reader);
            RequireToken(reader, JsonToken.String);
            return (string)reader.Value;
        }

        private static string ReadNullableString(JsonTextReader reader)
        {
            ReadNext(reader);
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }
            RequireToken(reader, JsonToken.String);
            return (string)reader.Value;
        }

        private static void ReadNext(JsonTextReader reader)
        {
            if (!reader.Read() || reader.TokenType == JsonToken.Comment)
            {
                throw new SaveStructureException();
            }
        }

        private static void RequireToken(JsonTextReader reader, JsonToken expected)
        {
            if (reader.TokenType != expected)
            {
                throw new SaveStructureException();
            }
        }

        private static CheckpointId ParseCheckpointId(string value)
        {
            EnsureIdentifierLength(value);
            var id = new CheckpointId(value);
            if (!StringComparer.Ordinal.Equals(id.Value, value)) throw new SaveSemanticException();
            return id;
        }

        private static BuildingInstanceId ParseBuildingInstanceId(string value)
        {
            EnsureIdentifierLength(value);
            var id = new BuildingInstanceId(value);
            if (!StringComparer.Ordinal.Equals(id.Value, value)) throw new SaveSemanticException();
            return id;
        }

        private static BuildingDefinitionId ParseDefinitionId(string value)
        {
            EnsureIdentifierLength(value);
            var id = new BuildingDefinitionId(value);
            if (!StringComparer.Ordinal.Equals(id.Value, value)) throw new SaveSemanticException();
            return id;
        }

        private static RecipeId ParseRecipeId(string value)
        {
            EnsureIdentifierLength(value);
            var id = new RecipeId(value);
            if (!StringComparer.Ordinal.Equals(id.Value, value)) throw new SaveSemanticException();
            return id;
        }

        private static ResourceId ParseResourceId(string value)
        {
            EnsureIdentifierLength(value);
            var id = new ResourceId(value);
            if (!StringComparer.Ordinal.Equals(id.Value, value)) throw new SaveSemanticException();
            return id;
        }

        private static void EnsureIdentifierLength(string value)
        {
            if (value == null || value.Length == 0 || value.Length > MaximumIdentifierLength)
            {
                throw new SaveSemanticException();
            }
        }

        private static bool IsStructuralFailure(Exception exception)
        {
            return exception is SaveStructureException || exception is JsonException ||
                   exception is DecoderFallbackException || exception is IOException;
        }

        private static bool IsSemanticFailure(Exception exception)
        {
            return exception is SaveSemanticException || exception is ArgumentException ||
                   exception is InvalidOperationException || exception is KeyNotFoundException ||
                   exception is OverflowException;
        }

        private sealed class SaveStructureException : Exception { }
        private sealed class SaveSemanticException : Exception { }
    }
}
