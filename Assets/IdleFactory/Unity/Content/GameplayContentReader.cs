using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;

namespace IdleFactory.Unity.Content
{
    /// <summary>
    /// Result of <see cref="GameplayContentReader.Read"/>: either a fully structured
    /// DTO graph, or exactly one diagnostic describing the first structural violation.
    /// Never a partial graph (contract "Load outcome").
    /// </summary>
    public sealed class GameplayContentReadResult
    {
        private GameplayContentReadResult(GameplayContentDocumentDto document, ContentDiagnostic diagnostic)
        {
            Document = document;
            Diagnostic = diagnostic;
        }

        public bool IsSuccess => Diagnostic == null;

        public GameplayContentDocumentDto Document { get; }

        public ContentDiagnostic Diagnostic { get; }

        public static GameplayContentReadResult Success(GameplayContentDocumentDto document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return new GameplayContentReadResult(document, null);
        }

        public static GameplayContentReadResult Failure(ContentDiagnostic diagnostic)
        {
            if (diagnostic == null)
            {
                throw new ArgumentNullException(nameof(diagnostic));
            }

            return new GameplayContentReadResult(null, diagnostic);
        }
    }

    /// <summary>
    /// Strict structural reader for gameplay content documents (contract v1;
    /// specs/002-json-content-authoring/contracts/gameplay-content-v1.md).
    ///
    /// Mirrors the proven hand-written <see cref="JsonTextReader"/> token-walk style
    /// of <c>IdleFactory.Unity.Persistence.SaveV1Mapper</c> (strict UTF-8, MaxDepth,
    /// DateParseHandling.None, FloatParseHandling.Decimal, SupportMultipleContent
    /// false, a seen-set per object) but does not call into or modify SaveV1Mapper:
    /// research.md R02 rejected extracting a shared validator because it would
    /// couple Save V1's regression surface to content authoring.
    ///
    /// Scope note (T072 vs T073): this reader performs only structural validation
    /// (FR-006). It intentionally does NOT enforce:
    ///  - ID rules (non-empty, canonical form, &lt;= 128 code units, uniqueness) for
    ///    any "id"/"resourceId"/"recipeId" field. Those are Identity-stage codes
    ///    (id.empty, id.too-long, id.not-canonical, id.duplicate) explicitly assigned
    ///    to the semantic validator in data-model.md and T073. Here such fields are
    ///    only checked for JSON type (string) and non-null.
    ///  - Per-field numeric minimums whose violation has its own family-specific
    ///    semantic code in data-model.md (e.g. recipe.amount.non-positive,
    ///    footprint.non-positive, orientation.invalid, buffer.capacity.negative,
    ///    policy.revision.non-positive, policy.ratio.invalid, policy.cap.non-positive).
    ///    Those remain entirely T073's responsibility. This reader only enforces the
    ///    generic, family-agnostic structural rules: JSON type, integer-ness
    ///    (value.not-integer for 1.0/1e3), and storage-range overflow
    ///    (value.out-of-range for a literal that does not fit Int32/Int64).
    ///  - Non-ID length/blank bounds that ARE given directly in the contract's Fields
    ///    table without a dedicated family code (developmentLabel <= 128 non-blank,
    ///    resources[].presentationKey optional non-blank <= 128, buildings[].presentationKey
    ///    required non-blank <= 128) ARE enforced here as value.out-of-range, because
    ///    they have no Identity-stage or family-specific code of their own. A
    ///    presentationKey, when the member is present at all, is never blank: "omitted"
    ///    (the member absent) is the only way to express "no key" (DefinitionGuard.
    ///    OptionalText delegates to RequiredText for a non-null value, Definitions.cs:27).
    ///  - Emptiness of nested lists (recipe inputs/outputs, buffer capacities,
    ///    allowedOrientations) is likewise left entirely to their dedicated family
    ///    codes (recipe.inputs.empty, recipe.outputs.empty, buffer.empty,
    ///    orientation.empty) in T073; only the generic top-level family collections
    ///    (resources/recipes/buildings/offlinePolicies) use collection.empty here.
    ///    All collections (top-level and nested) enforce collection.too-many here,
    ///    since that is a generic safety-guard limit with no family-specific code.
    /// </summary>
    public static class GameplayContentReader
    {
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private static readonly string[] RequiredDocumentMembers =
        {
            "contentSchemaVersion", "resources", "recipes", "buildings", "offlinePolicies",
        };

        private static readonly string[] RequiredResourceMembers =
        {
            "id", "developmentLabel", "isDevelopmentOnly",
        };

        private static readonly string[] RequiredRecipeMembers =
        {
            "id", "inputs", "outputs", "durationTicks", "isDevelopmentOnly",
        };

        private static readonly string[] RequiredAmountMembers =
        {
            "resourceId", "amount",
        };

        private static readonly string[] RequiredBuildingMembers =
        {
            "id", "footprintWidth", "footprintHeight", "allowedOrientations", "presentationKey", "isDevelopmentOnly",
        };

        private static readonly string[] RequiredCapacityMembers =
        {
            "resourceId", "capacity",
        };

        private static readonly string[] RequiredPolicyMembers =
        {
            "revision", "efficiencyNumerator", "efficiencyDenominator", "maxCreditedTicks",
        };

        public static GameplayContentReadResult Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return GameplayContentReadResult.Failure(RootDiagnostic("content.empty", "Content document is empty."));
            }

            if (bytes.Length > GameplayContentLimits.MaximumByteCount)
            {
                return GameplayContentReadResult.Failure(RootDiagnostic(
                    "content.too-large",
                    $"Content document exceeds {GameplayContentLimits.MaximumByteCount} bytes."));
            }

            if (HasUtf8Bom(bytes))
            {
                return GameplayContentReadResult.Failure(RootDiagnostic(
                    "content.encoding",
                    "Content document must not start with a UTF-8 byte order mark."));
            }

            string text;
            try
            {
                text = StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return GameplayContentReadResult.Failure(RootDiagnostic(
                    "content.encoding",
                    "Content document is not valid UTF-8."));
            }

            int version;
            try
            {
                version = PeekContentSchemaVersion(text);
            }
            catch (ContentStructureException ex)
            {
                return GameplayContentReadResult.Failure(ex.Diagnostic);
            }
            catch (JsonReaderException ex)
            {
                return GameplayContentReadResult.Failure(TranslateJsonReaderException(ex));
            }

            try
            {
                using (var textReader = new System.IO.StringReader(text))
                using (var reader = CreateJsonReader(textReader))
                {
                    var ctx = new ReaderContext(reader);
                    var document = ReadDocument(ctx, version);

                    bool hasTrailingContent;
                    int? trailingLine = null;
                    int? trailingColumn = null;
                    try
                    {
                        // With SupportMultipleContent = false, Json.NET does not simply
                        // return true for a further token here: any further non-whitespace
                        // content after the root value throws a JsonReaderException. Only
                        // trailing whitespace makes Read() return false cleanly.
                        hasTrailingContent = reader.Read();
                        if (hasTrailingContent)
                        {
                            (trailingLine, trailingColumn) = Position(reader);
                        }
                    }
                    catch (JsonReaderException ex)
                    {
                        hasTrailingContent = true;
                        trailingLine = ex.LineNumber > 0 ? (int?)ex.LineNumber : null;
                        trailingColumn = ex.LinePosition > 0 ? (int?)ex.LinePosition : null;
                    }

                    if (hasTrailingContent)
                    {
                        return GameplayContentReadResult.Failure(new ContentDiagnostic(
                            "json.trailing-content",
                            string.Empty,
                            null,
                            trailingLine,
                            trailingColumn,
                            "Content after the root value must be whitespace only."));
                    }

                    return GameplayContentReadResult.Success(document);
                }
            }
            catch (ContentStructureException ex)
            {
                return GameplayContentReadResult.Failure(ex.Diagnostic);
            }
            catch (JsonReaderException ex)
            {
                return GameplayContentReadResult.Failure(TranslateJsonReaderException(ex));
            }
        }

        private static int PeekContentSchemaVersion(string text)
        {
            using (var textReader = new System.IO.StringReader(text))
            using (var reader = CreateJsonReader(textReader))
            {
                var ctx = new ReaderContext(reader);
                Advance(ctx);
                if (reader.TokenType != JsonToken.StartObject)
                {
                    throw Fail(ctx, "json.malformed", "The document root must be an object.");
                }

                while (true)
                {
                    Advance(ctx);
                    if (reader.TokenType == JsonToken.EndObject)
                    {
                        throw Fail(ctx, "content.version.missing", "contentSchemaVersion is required.", ctx.CurrentPointer("contentSchemaVersion"));
                    }

                    Require(ctx, JsonToken.PropertyName, "json.malformed", "Expected an object member name.");
                    var name = (string)reader.Value;
                    Advance(ctx);

                    if (!StringComparer.Ordinal.Equals(name, "contentSchemaVersion"))
                    {
                        if (reader.TokenType == JsonToken.StartArray || reader.TokenType == JsonToken.StartObject)
                        {
                            try
                            {
                                reader.Skip();
                            }
                            catch (JsonReaderException ex)
                            {
                                throw TranslateJsonException(ctx, ex);
                            }
                        }

                        continue;
                    }

                    if (reader.TokenType != JsonToken.Integer || !(reader.Value is long value) || value != 1)
                    {
                        throw Fail(ctx, "content.version.unsupported", "contentSchemaVersion must be the integer 1.", ctx.CurrentPointer("contentSchemaVersion"));
                    }

                    return 1;
                }
            }
        }

        private static GameplayContentDocumentDto ReadDocument(ReaderContext ctx, int contentSchemaVersion)
        {
            Advance(ctx);
            Require(ctx, JsonToken.StartObject, "json.malformed", "The document root must be an object.");

            var dto = new GameplayContentDocumentDto { ContentSchemaVersion = contentSchemaVersion };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                Advance(ctx);
                if (ctx.Reader.TokenType == JsonToken.EndObject)
                {
                    break;
                }

                Require(ctx, JsonToken.PropertyName, "json.malformed", "Expected an object member name.");
                var name = (string)ctx.Reader.Value;
                if (!seen.Add(name))
                {
                    throw Fail(ctx, "member.duplicate", $"Duplicate member '{Clip(name)}'.", ctx.CurrentPointer(name));
                }

                switch (name)
                {
                    case "contentSchemaVersion":
                        dto.ContentSchemaVersion = ReadRequiredInt32(ctx, name);
                        break;
                    case "resources":
                        dto.Resources = ReadObjectArray(ctx, name, GameplayContentLimits.MaximumTopLevelEntryCount, true, ReadResourceEntry);
                        break;
                    case "recipes":
                        dto.Recipes = ReadObjectArray(ctx, name, GameplayContentLimits.MaximumTopLevelEntryCount, true, ReadRecipeEntry);
                        break;
                    case "buildings":
                        dto.Buildings = ReadObjectArray(ctx, name, GameplayContentLimits.MaximumTopLevelEntryCount, true, ReadBuildingEntry);
                        break;
                    case "offlinePolicies":
                        dto.OfflinePolicies = ReadObjectArray(ctx, name, GameplayContentLimits.MaximumTopLevelEntryCount, true, ReadOfflinePolicyEntry);
                        break;
                    default:
                        throw Fail(ctx, "member.unknown", $"Unknown member '{Clip(name)}'.", ctx.CurrentPointer(name));
                }
            }

            RequireMembers(ctx, seen, RequiredDocumentMembers);
            return dto;
        }

        private static ResourceDto ReadResourceEntry(ReaderContext ctx, int index)
        {
            var dto = new ResourceDto();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                Advance(ctx);
                if (ctx.Reader.TokenType == JsonToken.EndObject)
                {
                    break;
                }

                Require(ctx, JsonToken.PropertyName, "json.malformed", "Expected an object member name.");
                var name = (string)ctx.Reader.Value;
                if (!seen.Add(name))
                {
                    throw Fail(ctx, "member.duplicate", $"Duplicate member '{Clip(name)}'.", ctx.CurrentPointer(name));
                }

                switch (name)
                {
                    case "id":
                        dto.Id = ReadIdString(ctx, name);
                        break;
                    case "developmentLabel":
                        dto.DevelopmentLabel = ReadLabelString(ctx, name, requireNonBlank: true);
                        break;
                    case "presentationKey":
                        // presentationKey is optional (the member may be omitted), but when
                        // present it must not be blank: DefinitionGuard.OptionalText delegates
                        // to RequiredText for a non-null value (Definitions.cs:27).
                        dto.PresentationKey = ReadLabelString(ctx, name, requireNonBlank: true);
                        break;
                    case "isDevelopmentOnly":
                        dto.IsDevelopmentOnly = ReadRequiredBool(ctx, name);
                        break;
                    default:
                        throw Fail(ctx, "member.unknown", $"Unknown member '{Clip(name)}'.", ctx.CurrentPointer(name));
                }
            }

            RequireMembers(ctx, seen, RequiredResourceMembers);
            return dto;
        }

        private static RecipeDto ReadRecipeEntry(ReaderContext ctx, int index)
        {
            var dto = new RecipeDto();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                Advance(ctx);
                if (ctx.Reader.TokenType == JsonToken.EndObject)
                {
                    break;
                }

                Require(ctx, JsonToken.PropertyName, "json.malformed", "Expected an object member name.");
                var name = (string)ctx.Reader.Value;
                if (!seen.Add(name))
                {
                    throw Fail(ctx, "member.duplicate", $"Duplicate member '{Clip(name)}'.", ctx.CurrentPointer(name));
                }

                switch (name)
                {
                    case "id":
                        dto.Id = ReadIdString(ctx, name);
                        break;
                    case "inputs":
                        dto.Inputs = ReadObjectArray(ctx, name, GameplayContentLimits.MaximumNestedEntryCount, false, ReadResourceAmountEntry);
                        break;
                    case "outputs":
                        dto.Outputs = ReadObjectArray(ctx, name, GameplayContentLimits.MaximumNestedEntryCount, false, ReadResourceAmountEntry);
                        break;
                    case "durationTicks":
                        dto.DurationTicks = ReadRequiredInt64(ctx, name);
                        break;
                    case "isDevelopmentOnly":
                        dto.IsDevelopmentOnly = ReadRequiredBool(ctx, name);
                        break;
                    default:
                        throw Fail(ctx, "member.unknown", $"Unknown member '{Clip(name)}'.", ctx.CurrentPointer(name));
                }
            }

            RequireMembers(ctx, seen, RequiredRecipeMembers);
            return dto;
        }

        private static ResourceAmountDto ReadResourceAmountEntry(ReaderContext ctx, int index)
        {
            var dto = new ResourceAmountDto();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                Advance(ctx);
                if (ctx.Reader.TokenType == JsonToken.EndObject)
                {
                    break;
                }

                Require(ctx, JsonToken.PropertyName, "json.malformed", "Expected an object member name.");
                var name = (string)ctx.Reader.Value;
                if (!seen.Add(name))
                {
                    throw Fail(ctx, "member.duplicate", $"Duplicate member '{Clip(name)}'.", ctx.CurrentPointer(name));
                }

                switch (name)
                {
                    case "resourceId":
                        dto.ResourceId = ReadIdString(ctx, name);
                        break;
                    case "amount":
                        dto.Amount = ReadRequiredInt64(ctx, name);
                        break;
                    default:
                        throw Fail(ctx, "member.unknown", $"Unknown member '{Clip(name)}'.", ctx.CurrentPointer(name));
                }
            }

            RequireMembers(ctx, seen, RequiredAmountMembers);
            return dto;
        }

        private static BuildingDto ReadBuildingEntry(ReaderContext ctx, int index)
        {
            var dto = new BuildingDto();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                Advance(ctx);
                if (ctx.Reader.TokenType == JsonToken.EndObject)
                {
                    break;
                }

                Require(ctx, JsonToken.PropertyName, "json.malformed", "Expected an object member name.");
                var name = (string)ctx.Reader.Value;
                if (!seen.Add(name))
                {
                    throw Fail(ctx, "member.duplicate", $"Duplicate member '{Clip(name)}'.", ctx.CurrentPointer(name));
                }

                switch (name)
                {
                    case "id":
                        dto.Id = ReadIdString(ctx, name);
                        break;
                    case "footprintWidth":
                        dto.FootprintWidth = ReadRequiredInt32(ctx, name);
                        break;
                    case "footprintHeight":
                        dto.FootprintHeight = ReadRequiredInt32(ctx, name);
                        break;
                    case "allowedOrientations":
                        dto.AllowedOrientations = ReadInt32Array(ctx, name, GameplayContentLimits.MaximumNestedEntryCount);
                        break;
                    case "recipeId":
                        dto.RecipeId = ReadIdString(ctx, name);
                        break;
                    case "inputBuffer":
                        dto.InputBuffer = ReadObjectArray(ctx, name, GameplayContentLimits.MaximumNestedEntryCount, false, ReadResourceCapacityEntry);
                        break;
                    case "outputBuffer":
                        dto.OutputBuffer = ReadObjectArray(ctx, name, GameplayContentLimits.MaximumNestedEntryCount, false, ReadResourceCapacityEntry);
                        break;
                    case "presentationKey":
                        // Parent correction (2026-09-28): required here, unlike ResourceDto.PresentationKey,
                        // because BuildingDefinition uses DefinitionGuard.RequiredText (Definitions.cs:276).
                        dto.PresentationKey = ReadLabelString(ctx, name, requireNonBlank: true);
                        break;
                    case "isDevelopmentOnly":
                        dto.IsDevelopmentOnly = ReadRequiredBool(ctx, name);
                        break;
                    default:
                        throw Fail(ctx, "member.unknown", $"Unknown member '{Clip(name)}'.", ctx.CurrentPointer(name));
                }
            }

            RequireMembers(ctx, seen, RequiredBuildingMembers);
            return dto;
        }

        private static ResourceCapacityDto ReadResourceCapacityEntry(ReaderContext ctx, int index)
        {
            var dto = new ResourceCapacityDto();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                Advance(ctx);
                if (ctx.Reader.TokenType == JsonToken.EndObject)
                {
                    break;
                }

                Require(ctx, JsonToken.PropertyName, "json.malformed", "Expected an object member name.");
                var name = (string)ctx.Reader.Value;
                if (!seen.Add(name))
                {
                    throw Fail(ctx, "member.duplicate", $"Duplicate member '{Clip(name)}'.", ctx.CurrentPointer(name));
                }

                switch (name)
                {
                    case "resourceId":
                        dto.ResourceId = ReadIdString(ctx, name);
                        break;
                    case "capacity":
                        dto.Capacity = ReadRequiredInt64(ctx, name);
                        break;
                    default:
                        throw Fail(ctx, "member.unknown", $"Unknown member '{Clip(name)}'.", ctx.CurrentPointer(name));
                }
            }

            RequireMembers(ctx, seen, RequiredCapacityMembers);
            return dto;
        }

        private static OfflinePolicyDto ReadOfflinePolicyEntry(ReaderContext ctx, int index)
        {
            var dto = new OfflinePolicyDto();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                Advance(ctx);
                if (ctx.Reader.TokenType == JsonToken.EndObject)
                {
                    break;
                }

                Require(ctx, JsonToken.PropertyName, "json.malformed", "Expected an object member name.");
                var name = (string)ctx.Reader.Value;
                if (!seen.Add(name))
                {
                    throw Fail(ctx, "member.duplicate", $"Duplicate member '{Clip(name)}'.", ctx.CurrentPointer(name));
                }

                switch (name)
                {
                    case "revision":
                        dto.Revision = ReadRequiredInt32(ctx, name);
                        break;
                    case "efficiencyNumerator":
                        dto.EfficiencyNumerator = ReadRequiredInt64(ctx, name);
                        break;
                    case "efficiencyDenominator":
                        dto.EfficiencyDenominator = ReadRequiredInt64(ctx, name);
                        break;
                    case "maxCreditedTicks":
                        dto.MaxCreditedTicks = ReadRequiredInt64(ctx, name);
                        break;
                    default:
                        throw Fail(ctx, "member.unknown", $"Unknown member '{Clip(name)}'.", ctx.CurrentPointer(name));
                }
            }

            RequireMembers(ctx, seen, RequiredPolicyMembers);
            return dto;
        }

        // ---- shared primitive/collection readers -------------------------------------------------

        private static string ReadIdString(ReaderContext ctx, string member)
        {
            ctx.PushMember(member);
            Advance(ctx);
            if (ctx.Reader.TokenType == JsonToken.Null)
            {
                throw Fail(ctx, "value.null", $"Member '{member}' must not be null.");
            }

            Require(ctx, JsonToken.String, "value.type", $"Member '{member}' must be a string.");
            var value = (string)ctx.Reader.Value;
            ctx.Pop();
            return value;
        }

        private static string ReadLabelString(ReaderContext ctx, string member, bool requireNonBlank)
        {
            ctx.PushMember(member);
            Advance(ctx);
            if (ctx.Reader.TokenType == JsonToken.Null)
            {
                throw Fail(ctx, "value.null", $"Member '{member}' must not be null.");
            }

            Require(ctx, JsonToken.String, "value.type", $"Member '{member}' must be a string.");
            var value = (string)ctx.Reader.Value;
            if (value.Length > GameplayContentLimits.MaximumIdentifierLength ||
                (requireNonBlank && string.IsNullOrWhiteSpace(value)))
            {
                throw Fail(
                    ctx,
                    "value.out-of-range",
                    requireNonBlank
                        ? $"Member '{member}' must be a non-blank string of at most {GameplayContentLimits.MaximumIdentifierLength} UTF-16 code units."
                        : $"Member '{member}' must be a string of at most {GameplayContentLimits.MaximumIdentifierLength} UTF-16 code units.");
            }

            ctx.Pop();
            return value;
        }

        private static bool ReadRequiredBool(ReaderContext ctx, string member)
        {
            ctx.PushMember(member);
            Advance(ctx);
            if (ctx.Reader.TokenType == JsonToken.Null)
            {
                throw Fail(ctx, "value.null", $"Member '{member}' must not be null.");
            }

            Require(ctx, JsonToken.Boolean, "value.type", $"Member '{member}' must be a boolean.");
            var value = (bool)ctx.Reader.Value;
            ctx.Pop();
            return value;
        }

        private static int ReadRequiredInt32(ReaderContext ctx, string member)
        {
            ctx.PushMember(member);
            Advance(ctx);
            if (ctx.Reader.TokenType == JsonToken.Null)
            {
                throw Fail(ctx, "value.null", $"Member '{member}' must not be null.");
            }

            if (ctx.Reader.TokenType == JsonToken.Float)
            {
                throw Fail(ctx, "value.not-integer", $"Member '{member}' must be a JSON integer without a fraction or exponent.");
            }

            Require(ctx, JsonToken.Integer, "value.type", $"Member '{member}' must be an integer.");
            if (ctx.Reader.Value is BigInteger)
            {
                throw Fail(ctx, "value.out-of-range", $"Member '{member}' exceeds the 32-bit integer range.");
            }

            var raw = (long)ctx.Reader.Value;
            if (raw < int.MinValue || raw > int.MaxValue)
            {
                throw Fail(ctx, "value.out-of-range", $"Member '{member}' exceeds the 32-bit integer range.");
            }

            ctx.Pop();
            return (int)raw;
        }

        private static long ReadRequiredInt64(ReaderContext ctx, string member)
        {
            ctx.PushMember(member);
            Advance(ctx);
            if (ctx.Reader.TokenType == JsonToken.Null)
            {
                throw Fail(ctx, "value.null", $"Member '{member}' must not be null.");
            }

            if (ctx.Reader.TokenType == JsonToken.Float)
            {
                throw Fail(ctx, "value.not-integer", $"Member '{member}' must be a JSON integer without a fraction or exponent.");
            }

            Require(ctx, JsonToken.Integer, "value.type", $"Member '{member}' must be an integer.");
            if (ctx.Reader.Value is BigInteger)
            {
                throw Fail(ctx, "value.out-of-range", $"Member '{member}' exceeds the 64-bit integer range.");
            }

            var value = (long)ctx.Reader.Value;
            ctx.Pop();
            return value;
        }

        private static List<int> ReadInt32Array(ReaderContext ctx, string member, int maxCount)
        {
            ctx.PushMember(member);
            Advance(ctx);
            if (ctx.Reader.TokenType == JsonToken.Null)
            {
                throw Fail(ctx, "value.null", $"Member '{member}' must not be null.");
            }

            Require(ctx, JsonToken.StartArray, "value.type", $"Member '{member}' must be an array.");
            var list = new List<int>();
            while (true)
            {
                Advance(ctx);
                if (ctx.Reader.TokenType == JsonToken.EndArray)
                {
                    break;
                }

                if (list.Count >= maxCount)
                {
                    throw Fail(ctx, "collection.too-many", $"Member '{member}' has more than {maxCount} entries.");
                }

                ctx.PushIndex(list.Count);
                if (ctx.Reader.TokenType == JsonToken.Null)
                {
                    throw Fail(ctx, "value.null", "Array entry must not be null.");
                }

                if (ctx.Reader.TokenType == JsonToken.Float)
                {
                    throw Fail(ctx, "value.not-integer", "Array entry must be a JSON integer without a fraction or exponent.");
                }

                Require(ctx, JsonToken.Integer, "value.type", "Array entry must be an integer.");
                if (ctx.Reader.Value is BigInteger)
                {
                    throw Fail(ctx, "value.out-of-range", "Array entry exceeds the 32-bit integer range.");
                }

                var raw = (long)ctx.Reader.Value;
                if (raw < int.MinValue || raw > int.MaxValue)
                {
                    throw Fail(ctx, "value.out-of-range", "Array entry exceeds the 32-bit integer range.");
                }

                list.Add((int)raw);
                ctx.Pop();
            }

            ctx.Pop();
            return list;
        }

        private static List<T> ReadObjectArray<T>(
            ReaderContext ctx,
            string member,
            int maxCount,
            bool requireNonEmpty,
            Func<ReaderContext, int, T> readEntry)
        {
            ctx.PushMember(member);
            Advance(ctx);
            if (ctx.Reader.TokenType == JsonToken.Null)
            {
                throw Fail(ctx, "value.null", $"Member '{member}' must not be null.");
            }

            Require(ctx, JsonToken.StartArray, "value.type", $"Member '{member}' must be an array.");
            var list = new List<T>();
            while (true)
            {
                Advance(ctx);
                if (ctx.Reader.TokenType == JsonToken.EndArray)
                {
                    break;
                }

                if (list.Count >= maxCount)
                {
                    throw Fail(ctx, "collection.too-many", $"Member '{member}' has more than {maxCount} entries.");
                }

                ctx.PushIndex(list.Count);
                if (ctx.Reader.TokenType == JsonToken.Null)
                {
                    throw Fail(ctx, "value.null", "Array entry must not be null.");
                }

                Require(ctx, JsonToken.StartObject, "value.type", "Array entry must be an object.");
                var entry = readEntry(ctx, list.Count);
                list.Add(entry);
                ctx.Pop();
            }

            if (requireNonEmpty && list.Count == 0)
            {
                throw Fail(ctx, "collection.empty", $"Member '{member}' must not be empty.");
            }

            ctx.Pop();
            return list;
        }

        private static void RequireMembers(ReaderContext ctx, HashSet<string> seen, IReadOnlyList<string> required)
        {
            foreach (var member in required)
            {
                if (!seen.Contains(member))
                {
                    throw Fail(ctx, "member.missing", $"Missing required member '{member}'.", ctx.CurrentPointer(member));
                }
            }
        }

        // ---- low-level token helpers --------------------------------------------------------------

        private static void Advance(ReaderContext ctx)
        {
            bool moved;
            try
            {
                moved = ctx.Reader.Read();
            }
            catch (JsonReaderException ex)
            {
                throw TranslateJsonException(ctx, ex);
            }

            if (!moved)
            {
                throw Fail(ctx, "json.malformed", "Unexpected end of document.");
            }

            if (ctx.Reader.TokenType == JsonToken.Comment)
            {
                throw Fail(ctx, "json.malformed", "Comments are not allowed.");
            }
        }

        private static void Require(ReaderContext ctx, JsonToken expected, string code, string message)
        {
            if (ctx.Reader.TokenType != expected)
            {
                throw Fail(ctx, code, message);
            }
        }

        private static JsonTextReader CreateJsonReader(System.IO.TextReader textReader)
        {
            return new JsonTextReader(textReader)
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Decimal,
                MaxDepth = GameplayContentLimits.MaximumJsonDepth,
                SupportMultipleContent = false,
            };
        }

        private static bool HasUtf8Bom(byte[] bytes)
        {
            return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        }

        private static ContentDiagnostic RootDiagnostic(string code, string message)
        {
            return new ContentDiagnostic(code, string.Empty, null, null, null, message);
        }

        private static (int? line, int? column) Position(JsonTextReader reader)
        {
            var info = (IJsonLineInfo)reader;
            return info.HasLineInfo() ? (info.LineNumber, info.LinePosition) : ((int?)null, (int?)null);
        }

        private static ContentStructureException Fail(ReaderContext ctx, string code, string message)
        {
            return Fail(ctx, code, message, ctx.CurrentPointer());
        }

        private static ContentStructureException Fail(ReaderContext ctx, string code, string message, string pointer)
        {
            var (line, column) = Position(ctx.Reader);
            return new ContentStructureException(new ContentDiagnostic(code, pointer, null, line, column, message));
        }

        private static ContentStructureException TranslateJsonException(ReaderContext ctx, JsonReaderException ex)
        {
            var code = IsDepthException(ex) ? "json.depth" : "json.malformed";
            var line = ex.LineNumber > 0 ? (int?)ex.LineNumber : null;
            var column = ex.LinePosition > 0 ? (int?)ex.LinePosition : null;
            return new ContentStructureException(new ContentDiagnostic(code, ctx.CurrentPointer(), null, line, column, JsonMessage(code)));
        }

        private static ContentDiagnostic TranslateJsonReaderException(JsonReaderException ex)
        {
            var code = IsDepthException(ex) ? "json.depth" : "json.malformed";
            var line = ex.LineNumber > 0 ? (int?)ex.LineNumber : null;
            var column = ex.LinePosition > 0 ? (int?)ex.LinePosition : null;
            return new ContentDiagnostic(code, string.Empty, null, line, column, JsonMessage(code));
        }

        // Static copy only: the Newtonsoft message echoes file text, so it is never passed on (SEC-5).
        private static string JsonMessage(string code) =>
            code == "json.depth" ? "The JSON document is nested too deeply." : "The JSON document is malformed.";

        // Member names taken from the file are bounded to 128 UTF-16 code units in messages and pointers (SEC-5).
        private const int MaximumEchoedLength = 128;

        private static string Clip(string value)
        {
            if (value == null || value.Length <= MaximumEchoedLength) return value;
            var length = MaximumEchoedLength - 1;
            if (char.IsHighSurrogate(value[length - 1])) length--;
            return value.Substring(0, length) + "…";
        }

        private static bool IsDepthException(JsonReaderException ex)
        {
            return ex.Message.IndexOf("MaxDepth", StringComparison.Ordinal) >= 0;
        }

        private sealed class ContentStructureException : Exception
        {
            public ContentStructureException(ContentDiagnostic diagnostic)
                : base(diagnostic?.Message)
            {
                Diagnostic = diagnostic;
            }

            public ContentDiagnostic Diagnostic { get; }
        }

        private sealed class ReaderContext
        {
            private readonly List<object> _path = new List<object>();

            public ReaderContext(JsonTextReader reader)
            {
                Reader = reader;
            }

            public JsonTextReader Reader { get; }

            public void PushMember(string name) => _path.Add(name);

            public void PushIndex(int index) => _path.Add(index);

            public void Pop() => _path.RemoveAt(_path.Count - 1);

            public string CurrentPointer() => BuildPointer(_path);

            public string CurrentPointer(string extraMember)
            {
                var extended = new List<object>(_path) { extraMember };
                return BuildPointer(extended);
            }

            private static string BuildPointer(IReadOnlyList<object> path)
            {
                if (path.Count == 0)
                {
                    return string.Empty;
                }

                var builder = new StringBuilder();
                foreach (var segment in path)
                {
                    builder.Append('/');
                    if (segment is int index)
                    {
                        builder.Append(index.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        var text = (string)segment;
                        builder.Append(Clip(text).Replace("~", "~0").Replace("/", "~1"));
                    }
                }

                return builder.ToString();
            }
        }
    }
}
