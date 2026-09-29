using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;

namespace IdleFactory.Unity.Content
{
    /// <summary>
    /// Result of <see cref="RunContentReader.Read"/>: either a fully structured DTO graph, or exactly one
    /// diagnostic describing the first structural violation. Never a partial graph.
    /// </summary>
    public sealed class RunContentReadResult
    {
        private RunContentReadResult(RunContentDocumentDto document, ContentDiagnostic diagnostic)
        {
            Document = document;
            Diagnostic = diagnostic;
        }

        public bool IsSuccess => Diagnostic == null;

        public RunContentDocumentDto Document { get; }

        public ContentDiagnostic Diagnostic { get; }

        public static RunContentReadResult Success(RunContentDocumentDto document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return new RunContentReadResult(document, null);
        }

        public static RunContentReadResult Failure(ContentDiagnostic diagnostic)
        {
            if (diagnostic == null)
            {
                throw new ArgumentNullException(nameof(diagnostic));
            }

            return new RunContentReadResult(null, diagnostic);
        }
    }

    /// <summary>
    /// Strict structural reader for gameplay content documents, contract version 2
    /// (specs/003-first-factory-prestige/contracts/gameplay-content-v2.md). It is a hand-written
    /// <see cref="JsonTextReader"/> token walk in the style of the v1 <see cref="GameplayContentReader"/>: strict UTF-8
    /// without a BOM, at most 1 MiB, depth at most 16, integers only, no null, no comments, and unknown, duplicate and
    /// missing members rejected at every level. Nothing is deserialized by reflection.
    ///
    /// Scope: the reader enforces structure only (JSON types, integer-ness, 32/64-bit storage range, list limits, the
    /// non-blank and 128-code-unit bound on labels and presentation keys, the closed <c>rounding</c> vocabulary, and the
    /// member set each known <c>kind</c> of an unlock, target, effect or reward requires). Identity rules, ranges,
    /// references and every cross-family rule belong to <see cref="RunContentValidator"/>. A <c>kind</c> the reader does
    /// not know is passed through so that the validator reports the semantic code (<c>effect.kind.unknown</c>,
    /// <c>contract.reward.kind.unknown</c>, <c>item.target.invalid</c>, <c>effect.target.invalid</c>,
    /// <c>unlock.kind.unknown</c>). Diagnostics never echo file-system paths or document text; the messages carry member
    /// names only.
    /// </summary>
    public static class RunContentReader
    {
        /// <summary>The only <c>contentSchemaVersion</c> this reader accepts (version 1 routes to the legacy loader).</summary>
        public const int SupportedContentSchemaVersion = 2;

        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        // Required member lists. RunContentSchemaConsistencyTests compares each with the schema's "required" array.
        private static readonly string[] RequiredDocumentMembers =
        {
            "contentSchemaVersion", "contentRevision", "runStart", "resources", "recipes", "machines", "storageLevels",
            "factoryLevels", "contracts", "items", "research", "overdrive", "prestige", "industryUpgrades", "offlinePolicies",
        };

        private static readonly string[] RequiredRunStartMembers =
        {
            "startingCash", "initialUnlocks", "removalRefundPpm", "minCycleTicks", "maxCombinedSpeedPpm",
            "maxCombinedOutputFactor", "maxRewardRatioPpm",
        };

        private static readonly string[] RequiredResourceMembers = { "id", "label", "presentationKey", "basePrice", "unlock" };

        private static readonly string[] RequiredRecipeMembers = { "id", "inputs", "outputs", "durationTicks" };

        private static readonly string[] RequiredAmountMembers = { "resourceId", "amount" };

        private static readonly string[] RequiredMachineMembers =
        {
            "id", "label", "presentationKey", "recipeId", "maxLevel", "cost", "speedGrowthPpm", "milestones", "unlock",
        };

        private static readonly string[] RequiredMilestoneMembers = { "level", "speedPpm" };

        private static readonly string[] RequiredCurveMembers = { "base", "growthPpm", "rounding" };

        private static readonly string[] RequiredStorageLevelMembers = { "level", "capacity", "cost" };

        private static readonly string[] RequiredFactoryLevelMembers = { "level", "slots", "cost" };

        private static readonly string[] RequiredContractMembers = { "id", "kind", "resourceId", "quantity", "rewards", "unlock" };

        private static readonly string[] RequiredItemMembers =
        {
            "id", "tier", "label", "presentationKey", "durationMs", "target", "effect",
        };

        private static readonly string[] RequiredResearchMembers =
        {
            "id", "label", "maxLevel", "rpCost", "prerequisites", "effect",
        };

        private static readonly string[] RequiredPrerequisiteMembers = { "id", "level" };

        private static readonly string[] RequiredOverdriveMembers = { "durationMs", "cooldownMs", "speedPpm", "unlock" };

        private static readonly string[] RequiredPrestigeMembers = { "thresholdFactoryValue", "ipCurve" };

        private static readonly string[] RequiredIpCurveMembers = { "kind", "coefficient", "divisor" };

        private static readonly string[] RequiredIndustryUpgradeMembers = { "id", "label", "maxLevel", "ipCost", "effect" };

        private static readonly string[] RequiredPolicyMembers =
        {
            "revision", "efficiencyNumerator", "efficiencyDenominator", "maxCreditedTicks",
        };

        // Closed vocabularies. The schema-consistency test compares them with the schema's enums and consts.
        private static readonly string[] Roundings = { "ceil", "floor" };

        private static readonly string[] UnlockKinds = { "always", "researchPurchased", "factoryLevelAtLeast", "resourceProduced" };

        private static readonly string[] ContractKinds = { "plate", "gear", "timedBar" };

        private static readonly string[] RewardKinds = { "cash", "rp", "item" };

        private static readonly string[] ItemTargetKinds = { "factory", "machineType" };

        private static readonly string[] EffectTargetKinds = { "allMachines", "allResources", "machine", "resource", "expansion" };

        private static readonly string[] ItemEffectKinds = { "machineSpeed", "machineOutput" };

        private static readonly string[] ResearchEffectKinds = { "machineSpeed", "machineOutput", "salePrice", "upgradeCost" };

        private static readonly string[] IndustryEffectKinds =
        {
            "machineSpeed", "salePrice", "upgradeCost", "startingCash", "startingStorageLevel",
        };

        /// <summary>Top-level families that must hold at least one entry (research and industryUpgrades may be empty).</summary>
        private static readonly string[] NonEmptyFamilies =
        {
            "resources", "recipes", "machines", "storageLevels", "factoryLevels", "contracts", "items", "offlinePolicies",
        };

        private static readonly string[] KindOnly = { "kind" };

        private static readonly string[] KindAndId = { "kind", "id" };

        private static readonly string[] KindAndLevel = { "kind", "level" };

        private static readonly string[] None = new string[0];

        public static RunContentReadResult Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return RunContentReadResult.Failure(RootDiagnostic("content.empty", "Content document is empty."));
            }

            if (bytes.Length > GameplayContentLimits.MaximumByteCount)
            {
                return RunContentReadResult.Failure(RootDiagnostic(
                    "content.too-large",
                    $"Content document exceeds {GameplayContentLimits.MaximumByteCount} bytes."));
            }

            if (HasUtf8Bom(bytes))
            {
                return RunContentReadResult.Failure(RootDiagnostic(
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
                return RunContentReadResult.Failure(RootDiagnostic("content.encoding", "Content document is not valid UTF-8."));
            }

            try
            {
                PeekContentSchemaVersion(text);
            }
            catch (ContentStructureException ex)
            {
                return RunContentReadResult.Failure(ex.Diagnostic);
            }
            catch (JsonReaderException ex)
            {
                return RunContentReadResult.Failure(TranslateJsonReaderException(ex));
            }

            try
            {
                using (var textReader = new System.IO.StringReader(text))
                using (var reader = CreateJsonReader(textReader))
                {
                    var ctx = new ReaderContext(reader);
                    var document = ReadDocument(ctx);

                    bool hasTrailingContent;
                    int? trailingLine = null;
                    int? trailingColumn = null;
                    try
                    {
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
                        return RunContentReadResult.Failure(new ContentDiagnostic(
                            "json.trailing-content",
                            string.Empty,
                            null,
                            trailingLine,
                            trailingColumn,
                            "Content after the root value must be whitespace only."));
                    }

                    return RunContentReadResult.Success(document);
                }
            }
            catch (ContentStructureException ex)
            {
                return RunContentReadResult.Failure(ex.Diagnostic);
            }
            catch (JsonReaderException ex)
            {
                return RunContentReadResult.Failure(TranslateJsonReaderException(ex));
            }
        }

        private static void PeekContentSchemaVersion(string text)
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

                    if (reader.TokenType != JsonToken.Integer || !(reader.Value is long value) || value != SupportedContentSchemaVersion)
                    {
                        throw Fail(
                            ctx,
                            "content.version.unsupported",
                            "contentSchemaVersion must be the integer " + SupportedContentSchemaVersion.ToString(CultureInfo.InvariantCulture) + ".",
                            ctx.CurrentPointer("contentSchemaVersion"));
                    }

                    return;
                }
            }
        }

        // ---- document and families ----------------------------------------------------------------

        private static RunContentDocumentDto ReadDocument(ReaderContext ctx)
        {
            Advance(ctx);
            Require(ctx, JsonToken.StartObject, "json.malformed", "The document root must be an object.");

            var dto = new RunContentDocumentDto { ContentSchemaVersion = SupportedContentSchemaVersion };
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "contentSchemaVersion":
                        dto.ContentSchemaVersion = ReadInt32(ctx, name);
                        return true;
                    case "contentRevision":
                        dto.ContentRevision = ReadInt32(ctx, name);
                        return true;
                    case "runStart":
                        dto.RunStart = ReadObjectMember(ctx, name, ReadRunStart);
                        return true;
                    case "resources":
                        dto.Resources = ReadFamily(ctx, name, ReadResource);
                        return true;
                    case "recipes":
                        dto.Recipes = ReadFamily(ctx, name, ReadRecipe);
                        return true;
                    case "machines":
                        dto.Machines = ReadFamily(ctx, name, ReadMachine);
                        return true;
                    case "storageLevels":
                        dto.StorageLevels = ReadFamily(ctx, name, ReadStorageLevel);
                        return true;
                    case "factoryLevels":
                        dto.FactoryLevels = ReadFamily(ctx, name, ReadFactoryLevel);
                        return true;
                    case "contracts":
                        dto.Contracts = ReadFamily(ctx, name, ReadContract);
                        return true;
                    case "items":
                        dto.Items = ReadFamily(ctx, name, ReadItem);
                        return true;
                    case "research":
                        dto.Research = ReadFamily(ctx, name, ReadResearch);
                        return true;
                    case "overdrive":
                        dto.Overdrive = ReadObjectMember(ctx, name, ReadOverdrive);
                        return true;
                    case "prestige":
                        dto.Prestige = ReadObjectMember(ctx, name, ReadPrestige);
                        return true;
                    case "industryUpgrades":
                        dto.IndustryUpgrades = ReadFamily(ctx, name, ReadIndustryUpgrade);
                        return true;
                    case "offlinePolicies":
                        dto.OfflinePolicies = ReadFamily(ctx, name, ReadOfflinePolicy);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredDocumentMembers);
            return dto;
        }

        private static List<T> ReadFamily<T>(ReaderContext ctx, string member, Func<ReaderContext, T> readEntry)
        {
            return ReadObjectArray(
                ctx,
                member,
                GameplayContentLimits.MaximumTopLevelEntryCount,
                Array.IndexOf(NonEmptyFamilies, member) >= 0,
                readEntry);
        }

        private static List<T> ReadNested<T>(ReaderContext ctx, string member, Func<ReaderContext, T> readEntry)
        {
            return ReadObjectArray(ctx, member, GameplayContentLimits.MaximumNestedEntryCount, false, readEntry);
        }

        private static RunStartDto ReadRunStart(ReaderContext ctx)
        {
            var dto = new RunStartDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "startingCash":
                        dto.StartingCash = ReadInt64(ctx, name);
                        return true;
                    case "initialUnlocks":
                        dto.InitialUnlocks = ReadStringArray(ctx, name, GameplayContentLimits.MaximumNestedEntryCount);
                        return true;
                    case "removalRefundPpm":
                        dto.RemovalRefundPpm = ReadInt64(ctx, name);
                        return true;
                    case "minCycleTicks":
                        dto.MinCycleTicks = ReadInt64(ctx, name);
                        return true;
                    case "maxCombinedSpeedPpm":
                        dto.MaxCombinedSpeedPpm = ReadInt64(ctx, name);
                        return true;
                    case "maxCombinedOutputFactor":
                        dto.MaxCombinedOutputFactor = ReadInt64(ctx, name);
                        return true;
                    case "maxRewardRatioPpm":
                        dto.MaxRewardRatioPpm = ReadInt64(ctx, name);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredRunStartMembers);
            return dto;
        }

        private static RunCurveDto ReadCurve(ReaderContext ctx)
        {
            var dto = new RunCurveDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "base":
                        dto.Base = ReadInt64(ctx, name);
                        return true;
                    case "growthPpm":
                        dto.GrowthPpm = ReadInt64(ctx, name);
                        return true;
                    case "rounding":
                        dto.Rounding = ReadClosedString(ctx, name, Roundings);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredCurveMembers);
            return dto;
        }

        private static RunUnlockDto ReadUnlock(ReaderContext ctx)
        {
            var dto = new RunUnlockDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "kind":
                        dto.Kind = ReadString(ctx, name);
                        return true;
                    case "id":
                        dto.Id = ReadString(ctx, name);
                        return true;
                    case "level":
                        dto.Level = ReadInt32(ctx, name);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, KindOnly);
            switch (dto.Kind)
            {
                case "always":
                    EnforceShape(ctx, scope, KindOnly, None);
                    break;
                case "researchPurchased":
                case "resourceProduced":
                    EnforceShape(ctx, scope, KindAndId, None);
                    break;
                case "factoryLevelAtLeast":
                    EnforceShape(ctx, scope, KindAndLevel, None);
                    break;
            }

            return dto;
        }

        private static RunResourceDto ReadResource(ReaderContext ctx)
        {
            var dto = new RunResourceDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "id":
                        dto.Id = ReadString(ctx, name);
                        return true;
                    case "label":
                        dto.Label = ReadLabel(ctx, name);
                        return true;
                    case "presentationKey":
                        dto.PresentationKey = ReadLabel(ctx, name);
                        return true;
                    case "basePrice":
                        dto.BasePrice = ReadInt64(ctx, name);
                        return true;
                    case "unlock":
                        dto.Unlock = ReadObjectMember(ctx, name, ReadUnlock);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredResourceMembers);
            return dto;
        }

        private static RunAmountDto ReadAmount(ReaderContext ctx)
        {
            var dto = new RunAmountDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "resourceId":
                        dto.ResourceId = ReadString(ctx, name);
                        return true;
                    case "amount":
                        dto.Amount = ReadInt64(ctx, name);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredAmountMembers);
            return dto;
        }

        private static RunRecipeDto ReadRecipe(ReaderContext ctx)
        {
            var dto = new RunRecipeDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "id":
                        dto.Id = ReadString(ctx, name);
                        return true;
                    case "inputs":
                        dto.Inputs = ReadNested(ctx, name, ReadAmount);
                        return true;
                    case "outputs":
                        dto.Outputs = ReadNested(ctx, name, ReadAmount);
                        return true;
                    case "durationTicks":
                        dto.DurationTicks = ReadInt64(ctx, name);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredRecipeMembers);
            return dto;
        }

        private static RunMilestoneDto ReadMilestone(ReaderContext ctx)
        {
            var dto = new RunMilestoneDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "level":
                        dto.Level = ReadInt32(ctx, name);
                        return true;
                    case "speedPpm":
                        dto.SpeedPpm = ReadInt64(ctx, name);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredMilestoneMembers);
            return dto;
        }

        private static RunMachineDto ReadMachine(ReaderContext ctx)
        {
            var dto = new RunMachineDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "id":
                        dto.Id = ReadString(ctx, name);
                        return true;
                    case "label":
                        dto.Label = ReadLabel(ctx, name);
                        return true;
                    case "presentationKey":
                        dto.PresentationKey = ReadLabel(ctx, name);
                        return true;
                    case "recipeId":
                        dto.RecipeId = ReadString(ctx, name);
                        return true;
                    case "maxLevel":
                        dto.MaxLevel = ReadInt32(ctx, name);
                        return true;
                    case "cost":
                        dto.Cost = ReadObjectMember(ctx, name, ReadCurve);
                        return true;
                    case "speedGrowthPpm":
                        dto.SpeedGrowthPpm = ReadInt64(ctx, name);
                        return true;
                    case "milestones":
                        dto.Milestones = ReadNested(ctx, name, ReadMilestone);
                        return true;
                    case "unlock":
                        dto.Unlock = ReadObjectMember(ctx, name, ReadUnlock);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredMachineMembers);
            return dto;
        }

        private static RunStorageLevelDto ReadStorageLevel(ReaderContext ctx)
        {
            var dto = new RunStorageLevelDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "level":
                        dto.Level = ReadInt32(ctx, name);
                        return true;
                    case "capacity":
                        dto.Capacity = ReadInt64(ctx, name);
                        return true;
                    case "cost":
                        dto.Cost = ReadInt64(ctx, name);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredStorageLevelMembers);
            return dto;
        }

        private static RunFactoryLevelDto ReadFactoryLevel(ReaderContext ctx)
        {
            var dto = new RunFactoryLevelDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "level":
                        dto.Level = ReadInt32(ctx, name);
                        return true;
                    case "slots":
                        dto.Slots = ReadInt64(ctx, name);
                        return true;
                    case "cost":
                        dto.Cost = ReadInt64(ctx, name);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredFactoryLevelMembers);
            return dto;
        }

        private static RunContractRewardDto ReadReward(ReaderContext ctx)
        {
            var dto = new RunContractRewardDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "kind":
                        dto.Kind = ReadString(ctx, name);
                        return true;
                    case "curve":
                        dto.Curve = ReadObjectMember(ctx, name, ReadCurve);
                        return true;
                    case "itemId":
                        dto.ItemId = ReadString(ctx, name);
                        return true;
                    case "onCompletionIndices":
                        dto.OnCompletionIndices = ReadInt32Array(ctx, name, GameplayContentLimits.MaximumNestedEntryCount);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, KindOnly);
            switch (dto.Kind)
            {
                case "cash":
                case "rp":
                    EnforceShape(ctx, scope, new[] { "kind", "curve" }, None);
                    break;
                case "item":
                    EnforceShape(ctx, scope, new[] { "kind", "itemId", "onCompletionIndices" }, None);
                    break;
            }

            return dto;
        }

        private static RunContractDto ReadContract(ReaderContext ctx)
        {
            var dto = new RunContractDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "id":
                        dto.Id = ReadString(ctx, name);
                        return true;
                    case "kind":
                        dto.Kind = ReadString(ctx, name);
                        return true;
                    case "resourceId":
                        dto.ResourceId = ReadString(ctx, name);
                        return true;
                    case "quantity":
                        dto.Quantity = ReadObjectMember(ctx, name, ReadCurve);
                        return true;
                    case "durationMs":
                        dto.DurationMs = ReadInt64(ctx, name);
                        return true;
                    case "rewards":
                        dto.Rewards = ReadNested(ctx, name, ReadReward);
                        return true;
                    case "unlock":
                        dto.Unlock = ReadObjectMember(ctx, name, ReadUnlock);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredContractMembers);
            return dto;
        }

        private static RunItemTargetDto ReadItemTarget(ReaderContext ctx)
        {
            var dto = new RunItemTargetDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "kind":
                        dto.Kind = ReadString(ctx, name);
                        return true;
                    case "allowedMachineIds":
                        dto.AllowedMachineIds = ReadStringArray(ctx, name, GameplayContentLimits.MaximumNestedEntryCount);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, KindOnly);
            switch (dto.Kind)
            {
                case "factory":
                    EnforceShape(ctx, scope, KindOnly, None);
                    break;
                case "machineType":
                    EnforceShape(ctx, scope, new[] { "kind", "allowedMachineIds" }, None);
                    break;
            }

            return dto;
        }

        private static RunEffectTargetDto ReadEffectTarget(ReaderContext ctx)
        {
            var dto = new RunEffectTargetDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "kind":
                        dto.Kind = ReadString(ctx, name);
                        return true;
                    case "id":
                        dto.Id = ReadString(ctx, name);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, KindOnly);
            switch (dto.Kind)
            {
                case "allMachines":
                case "allResources":
                    EnforceShape(ctx, scope, KindOnly, None);
                    break;
                case "machine":
                case "resource":
                    EnforceShape(ctx, scope, KindAndId, None);
                    break;
                case "expansion":
                    // An expansion target has no id: it lowers the cost of both expansion tracks.
                    EnforceShape(ctx, scope, KindOnly, None);
                    break;
            }

            return dto;
        }

        private enum EffectSource
        {
            Item,
            Research,
            Industry,
        }

        private static RunEffectDto ReadEffect(ReaderContext ctx, EffectSource source)
        {
            var dto = new RunEffectDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "kind":
                        dto.Kind = ReadString(ctx, name);
                        return true;
                    case "target":
                        dto.Target = ReadObjectMember(ctx, name, ReadEffectTarget);
                        return true;
                    case "speedPpm":
                    case "valuePerLevelPpm":
                    case "factor":
                    case "valuePerLevel":
                        if (dto.ValueMember != null)
                        {
                            return false;
                        }

                        dto.Value = ReadInt64(ctx, name);
                        dto.ValueMember = name;
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, KindOnly);
            var kinds = source == EffectSource.Item ? ItemEffectKinds
                : source == EffectSource.Research ? ResearchEffectKinds
                : IndustryEffectKinds;
            if (Array.IndexOf(kinds, dto.Kind) >= 0)
            {
                var valueMember = ValueMemberFor(source, dto.Kind);
                var hasTarget = source != EffectSource.Item && dto.Kind != "startingCash" && dto.Kind != "startingStorageLevel";
                EnforceShape(
                    ctx,
                    scope,
                    hasTarget ? new[] { "kind", "target", valueMember } : new[] { "kind", valueMember },
                    None);
            }

            return dto;
        }

        private static string ValueMemberFor(EffectSource source, string kind)
        {
            switch (kind)
            {
                case "machineSpeed":
                    return source == EffectSource.Item ? "speedPpm" : "valuePerLevelPpm";
                case "machineOutput":
                    return "factor";
                case "salePrice":
                case "upgradeCost":
                    return "valuePerLevelPpm";
                default:
                    return "valuePerLevel";
            }
        }

        private static RunItemDto ReadItem(ReaderContext ctx)
        {
            var dto = new RunItemDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "id":
                        dto.Id = ReadString(ctx, name);
                        return true;
                    case "tier":
                        dto.Tier = ReadInt32(ctx, name);
                        return true;
                    case "label":
                        dto.Label = ReadLabel(ctx, name);
                        return true;
                    case "presentationKey":
                        dto.PresentationKey = ReadLabel(ctx, name);
                        return true;
                    case "durationMs":
                        dto.DurationMs = ReadInt64(ctx, name);
                        return true;
                    case "target":
                        dto.Target = ReadObjectMember(ctx, name, ReadItemTarget);
                        return true;
                    case "effect":
                        dto.Effect = ReadObjectMember(ctx, name, c => ReadEffect(c, EffectSource.Item));
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredItemMembers);
            return dto;
        }

        private static RunPrerequisiteDto ReadPrerequisite(ReaderContext ctx)
        {
            var dto = new RunPrerequisiteDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "id":
                        dto.Id = ReadString(ctx, name);
                        return true;
                    case "level":
                        dto.Level = ReadInt32(ctx, name);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredPrerequisiteMembers);
            return dto;
        }

        private static RunResearchDto ReadResearch(ReaderContext ctx)
        {
            var dto = new RunResearchDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "id":
                        dto.Id = ReadString(ctx, name);
                        return true;
                    case "label":
                        dto.Label = ReadLabel(ctx, name);
                        return true;
                    case "maxLevel":
                        dto.MaxLevel = ReadInt32(ctx, name);
                        return true;
                    case "rpCost":
                        dto.RpCost = ReadObjectMember(ctx, name, ReadCurve);
                        return true;
                    case "prerequisites":
                        dto.Prerequisites = ReadNested(ctx, name, ReadPrerequisite);
                        return true;
                    case "effect":
                        dto.Effect = ReadObjectMember(ctx, name, c => ReadEffect(c, EffectSource.Research));
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredResearchMembers);
            return dto;
        }

        private static RunOverdriveDto ReadOverdrive(ReaderContext ctx)
        {
            var dto = new RunOverdriveDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "durationMs":
                        dto.DurationMs = ReadInt64(ctx, name);
                        return true;
                    case "cooldownMs":
                        dto.CooldownMs = ReadInt64(ctx, name);
                        return true;
                    case "speedPpm":
                        dto.SpeedPpm = ReadInt64(ctx, name);
                        return true;
                    case "unlock":
                        dto.Unlock = ReadObjectMember(ctx, name, ReadUnlock);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredOverdriveMembers);
            return dto;
        }

        private static RunIpCurveDto ReadIpCurve(ReaderContext ctx)
        {
            var dto = new RunIpCurveDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "kind":
                        dto.Kind = ReadString(ctx, name);
                        return true;
                    case "coefficient":
                        dto.Coefficient = ReadInt64(ctx, name);
                        return true;
                    case "divisor":
                        dto.Divisor = ReadInt64(ctx, name);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredIpCurveMembers);
            return dto;
        }

        private static RunPrestigeDto ReadPrestige(ReaderContext ctx)
        {
            var dto = new RunPrestigeDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "thresholdFactoryValue":
                        dto.ThresholdFactoryValue = ReadInt64(ctx, name);
                        return true;
                    case "ipCurve":
                        dto.IpCurve = ReadObjectMember(ctx, name, ReadIpCurve);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredPrestigeMembers);
            return dto;
        }

        private static RunIndustryUpgradeDto ReadIndustryUpgrade(ReaderContext ctx)
        {
            var dto = new RunIndustryUpgradeDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "id":
                        dto.Id = ReadString(ctx, name);
                        return true;
                    case "label":
                        dto.Label = ReadLabel(ctx, name);
                        return true;
                    case "maxLevel":
                        dto.MaxLevel = ReadInt32(ctx, name);
                        return true;
                    case "ipCost":
                        dto.IpCost = ReadObjectMember(ctx, name, ReadCurve);
                        return true;
                    case "effect":
                        dto.Effect = ReadObjectMember(ctx, name, c => ReadEffect(c, EffectSource.Industry));
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredIndustryUpgradeMembers);
            return dto;
        }

        private static RunOfflinePolicyDto ReadOfflinePolicy(ReaderContext ctx)
        {
            var dto = new RunOfflinePolicyDto();
            var scope = ReadObject(ctx, name =>
            {
                switch (name)
                {
                    case "revision":
                        dto.Revision = ReadInt32(ctx, name);
                        return true;
                    case "efficiencyNumerator":
                        dto.EfficiencyNumerator = ReadInt64(ctx, name);
                        return true;
                    case "efficiencyDenominator":
                        dto.EfficiencyDenominator = ReadInt64(ctx, name);
                        return true;
                    case "maxCreditedTicks":
                        dto.MaxCreditedTicks = ReadInt64(ctx, name);
                        return true;
                    default:
                        return false;
                }
            });

            RequireMembers(ctx, scope, RequiredPolicyMembers);
            return dto;
        }

        // ---- object, array and primitive readers ---------------------------------------------------

        private static ObjectScope ReadObject(ReaderContext ctx, Func<string, bool> handle)
        {
            var scope = new ObjectScope();
            while (true)
            {
                Advance(ctx);
                if (ctx.Reader.TokenType == JsonToken.EndObject)
                {
                    return scope;
                }

                Require(ctx, JsonToken.PropertyName, "json.malformed", "Expected an object member name.");
                var name = (string)ctx.Reader.Value;
                var position = Position(ctx.Reader);
                if (scope.Contains(name))
                {
                    throw Fail(ctx, "member.duplicate", $"Duplicate member '{Clip(name)}'.", ctx.CurrentPointer(name), position);
                }

                scope.Add(name, position);
                if (!handle(name))
                {
                    throw Fail(ctx, "member.unknown", $"Unknown member '{Clip(name)}'.", ctx.CurrentPointer(name), position);
                }
            }
        }

        private static T ReadObjectMember<T>(ReaderContext ctx, string member, Func<ReaderContext, T> readObject)
        {
            ctx.PushMember(member);
            Advance(ctx);
            RejectNull(ctx, member);
            Require(ctx, JsonToken.StartObject, "value.type", $"Member '{member}' must be an object.");
            var value = readObject(ctx);
            ctx.Pop();
            return value;
        }

        private static List<T> ReadObjectArray<T>(
            ReaderContext ctx,
            string member,
            int maxCount,
            bool requireNonEmpty,
            Func<ReaderContext, T> readEntry)
        {
            ctx.PushMember(member);
            Advance(ctx);
            RejectNull(ctx, member);
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
                RejectNull(ctx, "Array entry");
                Require(ctx, JsonToken.StartObject, "value.type", "Array entry must be an object.");
                list.Add(readEntry(ctx));
                ctx.Pop();
            }

            if (requireNonEmpty && list.Count == 0)
            {
                throw Fail(ctx, "collection.empty", $"Member '{member}' must not be empty.");
            }

            ctx.Pop();
            return list;
        }

        private static List<string> ReadStringArray(ReaderContext ctx, string member, int maxCount)
        {
            ctx.PushMember(member);
            Advance(ctx);
            RejectNull(ctx, member);
            Require(ctx, JsonToken.StartArray, "value.type", $"Member '{member}' must be an array.");
            var list = new List<string>();
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
                RejectNull(ctx, "Array entry");
                Require(ctx, JsonToken.String, "value.type", "Array entry must be a string.");
                list.Add((string)ctx.Reader.Value);
                ctx.Pop();
            }

            ctx.Pop();
            return list;
        }

        private static List<int> ReadInt32Array(ReaderContext ctx, string member, int maxCount)
        {
            ctx.PushMember(member);
            Advance(ctx);
            RejectNull(ctx, member);
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
                list.Add(CurrentInt32(ctx, "Array entry"));
                ctx.Pop();
            }

            ctx.Pop();
            return list;
        }

        private static string ReadString(ReaderContext ctx, string member)
        {
            ctx.PushMember(member);
            Advance(ctx);
            RejectNull(ctx, member);
            Require(ctx, JsonToken.String, "value.type", $"Member '{member}' must be a string.");
            var value = (string)ctx.Reader.Value;
            ctx.Pop();
            return value;
        }

        private static string ReadLabel(ReaderContext ctx, string member)
        {
            ctx.PushMember(member);
            Advance(ctx);
            RejectNull(ctx, member);
            Require(ctx, JsonToken.String, "value.type", $"Member '{member}' must be a string.");
            var value = (string)ctx.Reader.Value;
            if (value.Length > GameplayContentLimits.MaximumIdentifierLength || string.IsNullOrWhiteSpace(value))
            {
                throw Fail(
                    ctx,
                    "value.out-of-range",
                    $"Member '{member}' must be a non-blank string of at most {GameplayContentLimits.MaximumIdentifierLength} UTF-16 code units.");
            }

            ctx.Pop();
            return value;
        }

        private static string ReadClosedString(ReaderContext ctx, string member, string[] allowed)
        {
            ctx.PushMember(member);
            Advance(ctx);
            RejectNull(ctx, member);
            Require(ctx, JsonToken.String, "value.type", $"Member '{member}' must be a string.");
            var value = (string)ctx.Reader.Value;
            if (Array.IndexOf(allowed, value) < 0)
            {
                throw Fail(ctx, "value.out-of-range", $"Member '{member}' must be one of: {string.Join(", ", allowed)}.");
            }

            ctx.Pop();
            return value;
        }

        private static int ReadInt32(ReaderContext ctx, string member)
        {
            ctx.PushMember(member);
            Advance(ctx);
            var value = CurrentInt32(ctx, $"Member '{member}'");
            ctx.Pop();
            return value;
        }

        private static long ReadInt64(ReaderContext ctx, string member)
        {
            ctx.PushMember(member);
            Advance(ctx);
            RejectNull(ctx, member);
            RejectFloat(ctx, $"Member '{member}'");
            Require(ctx, JsonToken.Integer, "value.type", $"Member '{member}' must be an integer.");
            if (ctx.Reader.Value is BigInteger)
            {
                throw Fail(ctx, "value.out-of-range", $"Member '{member}' exceeds the 64-bit integer range.");
            }

            var value = (long)ctx.Reader.Value;
            ctx.Pop();
            return value;
        }

        private static int CurrentInt32(ReaderContext ctx, string subject)
        {
            RejectNull(ctx, subject);
            RejectFloat(ctx, subject);
            Require(ctx, JsonToken.Integer, "value.type", $"{subject} must be an integer.");
            if (ctx.Reader.Value is BigInteger)
            {
                throw Fail(ctx, "value.out-of-range", $"{subject} exceeds the 32-bit integer range.");
            }

            var raw = (long)ctx.Reader.Value;
            if (raw < int.MinValue || raw > int.MaxValue)
            {
                throw Fail(ctx, "value.out-of-range", $"{subject} exceeds the 32-bit integer range.");
            }

            return (int)raw;
        }

        private static void RejectNull(ReaderContext ctx, string subject)
        {
            if (ctx.Reader.TokenType == JsonToken.Null)
            {
                throw Fail(ctx, "value.null", $"{subject} must not be null.");
            }
        }

        private static void RejectFloat(ReaderContext ctx, string subject)
        {
            if (ctx.Reader.TokenType == JsonToken.Float)
            {
                throw Fail(ctx, "value.not-integer", $"{subject} must be a JSON integer without a fraction or exponent.");
            }
        }

        private static void RequireMembers(ReaderContext ctx, ObjectScope scope, string[] required)
        {
            foreach (var member in required)
            {
                if (!scope.Contains(member))
                {
                    throw Fail(ctx, "member.missing", $"Missing required member '{member}'.", ctx.CurrentPointer(member));
                }
            }
        }

        private static void EnforceShape(ReaderContext ctx, ObjectScope scope, string[] required, string[] optional)
        {
            foreach (var member in scope.Order)
            {
                if (Array.IndexOf(required, member) < 0 && Array.IndexOf(optional, member) < 0)
                {
                    throw Fail(ctx, "member.unknown", $"Unknown member '{Clip(member)}'.", ctx.CurrentPointer(member), scope.PositionOf(member));
                }
            }

            RequireMembers(ctx, scope, required);
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

        // Member names come from the document; bound them like every other echoed value.
        private static string Clip(string value)
        {
            const int maximum = GameplayContentLimits.MaximumIdentifierLength;
            if (value == null || value.Length <= maximum)
            {
                return value;
            }

            var length = maximum - 1;
            if (char.IsHighSurrogate(value[length - 1]))
            {
                length--;
            }

            return value.Substring(0, length) + "…";
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

        private static ContentStructureException Fail(
            ReaderContext ctx,
            string code,
            string message,
            string pointer,
            (int? line, int? column) position)
        {
            return new ContentStructureException(new ContentDiagnostic(code, pointer, null, position.line, position.column, message));
        }

        private static ContentStructureException TranslateJsonException(ReaderContext ctx, JsonReaderException ex)
        {
            var code = IsDepthException(ex) ? "json.depth" : "json.malformed";
            var line = ex.LineNumber > 0 ? (int?)ex.LineNumber : null;
            var column = ex.LinePosition > 0 ? (int?)ex.LinePosition : null;
            return new ContentStructureException(new ContentDiagnostic(code, ctx.CurrentPointer(), null, line, column, ex.Message));
        }

        private static ContentDiagnostic TranslateJsonReaderException(JsonReaderException ex)
        {
            var code = IsDepthException(ex) ? "json.depth" : "json.malformed";
            var line = ex.LineNumber > 0 ? (int?)ex.LineNumber : null;
            var column = ex.LinePosition > 0 ? (int?)ex.LinePosition : null;
            return new ContentDiagnostic(code, string.Empty, null, line, column, ex.Message);
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

        private sealed class ObjectScope
        {
            private readonly Dictionary<string, (int? line, int? column)> _positions =
                new Dictionary<string, (int? line, int? column)>(StringComparer.Ordinal);

            public List<string> Order { get; } = new List<string>();

            public bool Contains(string member) => _positions.ContainsKey(member);

            public void Add(string member, (int? line, int? column) position)
            {
                _positions.Add(member, position);
                Order.Add(member);
            }

            public (int? line, int? column) PositionOf(string member) => _positions[member];
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
                        builder.Append(text.Replace("~", "~0").Replace("/", "~1"));
                    }
                }

                return builder.ToString();
            }
        }
    }
}
