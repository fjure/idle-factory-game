using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using IdleFactory.Domain;

namespace IdleFactory.Unity.Content
{
    /// <summary>
    /// Applies contract-v1 semantic rules to a fresh reader DTO graph. Diagnostics
    /// follow family/list order and then field/rule order; no domain objects are built here.
    /// </summary>
    public static class GameplayContentValidator
    {
        public static IReadOnlyList<ContentDiagnostic> Validate(GameplayContentDocumentDto document)
        {
            var collector = new DiagnosticCollector();
            if (document == null)
            {
                collector.Add("value.null", string.Empty, null, "Content document is missing.");
                return collector.Diagnostics;
            }

            var resourceIds = new HashSet<string>(StringComparer.Ordinal);
            var recipeIds = new HashSet<string>(StringComparer.Ordinal);
            var recipesById = new Dictionary<string, RecipeDto>(StringComparer.Ordinal);

            if (document.Resources != null)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < document.Resources.Count; index++)
                {
                    var resource = document.Resources[index];
                    var path = Pointer("resources", index);
                    if (resource == null)
                    {
                        collector.Add("value.null", path, null, "Resource entry is missing.");
                        continue;
                    }

                    ValidateIdentity(resource.Id, path + "/id", null, seen, collector);
                    if (IsCanonicalId(resource.Id))
                    {
                        resourceIds.Add(resource.Id);
                    }
                }
            }

            if (document.Recipes != null)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < document.Recipes.Count; index++)
                {
                    var recipe = document.Recipes[index];
                    var path = Pointer("recipes", index);
                    if (recipe == null)
                    {
                        collector.Add("value.null", path, null, "Recipe entry is missing.");
                        continue;
                    }

                    ValidateIdentity(recipe.Id, path + "/id", null, seen, collector);
                    if (IsCanonicalId(recipe.Id))
                    {
                        recipeIds.Add(recipe.Id);
                        recipesById[recipe.Id] = recipe;
                    }

                    ValidateAmounts(recipe.Inputs, path + "/inputs", recipe.Id, resourceIds, "recipe.inputs.empty", collector);
                    ValidateAmounts(recipe.Outputs, path + "/outputs", recipe.Id, resourceIds, "recipe.outputs.empty", collector);

                    if (recipe.DurationTicks <= 0)
                    {
                        collector.Add(
                            "recipe.duration.non-positive",
                            path + "/durationTicks",
                            KnownEntryId(recipe.Id),
                            "Recipe durationTicks must be positive.");
                    }
                }
            }

            if (document.Buildings != null)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < document.Buildings.Count; index++)
                {
                    var building = document.Buildings[index];
                    var path = Pointer("buildings", index);
                    if (building == null)
                    {
                        collector.Add("value.null", path, null, "Building entry is missing.");
                        continue;
                    }

                    var entryId = KnownEntryId(building.Id);
                    ValidateIdentity(building.Id, path + "/id", null, seen, collector);

                    if (building.FootprintWidth <= 0)
                    {
                        collector.Add("footprint.non-positive", path + "/footprintWidth", entryId, "Footprint width must be positive.");
                    }

                    if (building.FootprintHeight <= 0)
                    {
                        collector.Add("footprint.non-positive", path + "/footprintHeight", entryId, "Footprint height must be positive.");
                    }

                    ValidateOrientations(building.AllowedOrientations, path + "/allowedOrientations", entryId, collector);

                    RecipeDto recipe = null;
                    if (building.RecipeId != null)
                    {
                        ValidateIdentity(building.RecipeId, path + "/recipeId", entryId, null, collector);
                        if (IsCanonicalId(building.RecipeId))
                        {
                            if (!recipesById.TryGetValue(building.RecipeId, out recipe))
                            {
                                collector.Add(
                                    "ref.recipe.unknown",
                                    path + "/recipeId",
                                    entryId,
                                    $"Unknown recipe '{building.RecipeId}'.");
                            }
                        }

                        if (building.InputBuffer == null || building.OutputBuffer == null)
                        {
                            collector.Add(
                                "building.producing-requires-buffers",
                                path,
                                entryId,
                                "A building with recipeId requires both inputBuffer and outputBuffer.");
                        }
                    }

                    var inputResources = ValidateBuffer(
                        building.InputBuffer,
                        path + "/inputBuffer",
                        entryId,
                        resourceIds,
                        collector);
                    var outputResources = ValidateBuffer(
                        building.OutputBuffer,
                        path + "/outputBuffer",
                        entryId,
                        resourceIds,
                        collector);

                    if (recipe != null)
                    {
                        ValidateBufferCoverage(recipe.Inputs, inputResources, path + "/inputBuffer", entryId, collector);
                        ValidateBufferCoverage(recipe.Outputs, outputResources, path + "/outputBuffer", entryId, collector);
                    }
                }
            }

            ValidatePolicies(document.OfflinePolicies, collector);
            return collector.Diagnostics;
        }

        private static void ValidateAmounts(
            IList<ResourceAmountDto> amounts,
            string path,
            string entryId,
            ISet<string> resourceIds,
            string emptyCode,
            DiagnosticCollector collector)
        {
            if (amounts == null || amounts.Count == 0)
            {
                collector.Add(emptyCode, path, KnownEntryId(entryId), "Recipe side must contain at least one resource amount.");
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < amounts.Count; index++)
            {
                var amount = amounts[index];
                var amountPath = Pointer(path, index);
                if (amount == null)
                {
                    collector.Add("value.null", amountPath, KnownEntryId(entryId), "Resource amount entry is missing.");
                    continue;
                }

                var resourceId = amount.ResourceId;
                ValidateIdentity(resourceId, amountPath + "/resourceId", KnownEntryId(entryId), null, collector);
                var key = NormalizedId(resourceId);
                if (key != null && !seen.Add(key))
                {
                    collector.Add(
                        "recipe.resource.duplicate",
                        amountPath + "/resourceId",
                        KnownEntryId(entryId),
                        $"Recipe side repeats resource '{resourceId}'.");
                }

                if (IsCanonicalId(resourceId) && !resourceIds.Contains(resourceId))
                {
                    collector.Add(
                        "ref.resource.unknown",
                        amountPath + "/resourceId",
                        KnownEntryId(entryId),
                        $"Unknown resource '{resourceId}'.");
                }

                if (amount.Amount <= 0)
                {
                    collector.Add(
                        "recipe.amount.non-positive",
                        amountPath + "/amount",
                        KnownEntryId(entryId),
                        "Recipe amount must be positive.");
                }
            }
        }

        private static void ValidateOrientations(
            IList<int> orientations,
            string path,
            string entryId,
            DiagnosticCollector collector)
        {
            if (orientations == null || orientations.Count == 0)
            {
                collector.Add("orientation.empty", path, entryId, "At least one allowed orientation is required.");
                return;
            }

            var seen = new HashSet<int>();
            for (var index = 0; index < orientations.Count; index++)
            {
                var value = orientations[index];
                var valuePath = Pointer(path, index);
                if (value < 0 || value > 3)
                {
                    collector.Add("orientation.invalid", valuePath, entryId, "Orientation must be a quarter turn from 0 through 3.");
                }

                if (!seen.Add(value))
                {
                    collector.Add("orientation.duplicate", valuePath, entryId, $"Orientation '{value}' is repeated.");
                }
            }
        }

        private static HashSet<string> ValidateBuffer(
            IList<ResourceCapacityDto> capacities,
            string path,
            string entryId,
            ISet<string> resourceIds,
            DiagnosticCollector collector)
        {
            var declared = new HashSet<string>(StringComparer.Ordinal);
            if (capacities == null)
            {
                return declared;
            }

            if (capacities.Count == 0)
            {
                collector.Add("buffer.empty", path, entryId, "A present buffer must contain at least one resource.");
                return declared;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < capacities.Count; index++)
            {
                var capacity = capacities[index];
                var capacityPath = Pointer(path, index);
                if (capacity == null)
                {
                    collector.Add("value.null", capacityPath, entryId, "Buffer capacity entry is missing.");
                    continue;
                }

                var resourceId = capacity.ResourceId;
                ValidateIdentity(resourceId, capacityPath + "/resourceId", entryId, null, collector);
                var key = NormalizedId(resourceId);
                if (key != null)
                {
                    if (!seen.Add(key))
                    {
                        collector.Add(
                            "buffer.resource.duplicate",
                            capacityPath + "/resourceId",
                            entryId,
                            $"Buffer repeats resource '{resourceId}'.");
                    }

                    declared.Add(key);
                }

                if (IsCanonicalId(resourceId) && !resourceIds.Contains(resourceId))
                {
                    collector.Add(
                        "ref.resource.unknown",
                        capacityPath + "/resourceId",
                        entryId,
                        $"Unknown resource '{resourceId}'.");
                }

                if (capacity.Capacity < 0)
                {
                    collector.Add(
                        "buffer.capacity.negative",
                        capacityPath + "/capacity",
                        entryId,
                        "Buffer capacity must not be negative.");
                }
            }

            return declared;
        }

        private static void ValidateBufferCoverage(
            IList<ResourceAmountDto> recipeAmounts,
            ISet<string> declared,
            string path,
            string entryId,
            DiagnosticCollector collector)
        {
            if (recipeAmounts == null)
            {
                return;
            }

            for (var index = 0; index < recipeAmounts.Count; index++)
            {
                var amount = recipeAmounts[index];
                if (amount == null || !IsCanonicalId(amount.ResourceId))
                {
                    continue;
                }

                if (!declared.Contains(amount.ResourceId))
                {
                    collector.Add(
                        "buffer.missing-recipe-resource",
                        path,
                        entryId,
                        $"Buffer does not declare recipe resource '{amount.ResourceId}'.");
                }
            }
        }

        private static void ValidatePolicies(IList<OfflinePolicyDto> policies, DiagnosticCollector collector)
        {
            var seen = new HashSet<int>();
            var individuallyValid = true;
            var hasDuplicate = false;
            if (policies == null)
            {
                individuallyValid = false;
            }
            else
            {
                for (var index = 0; index < policies.Count; index++)
                {
                    var policy = policies[index];
                    var path = Pointer("offlinePolicies", index);
                    if (policy == null)
                    {
                        collector.Add("value.null", path, null, "Offline policy entry is missing.");
                        individuallyValid = false;
                        continue;
                    }

                    var entryId = policy.Revision > 0 ? policy.Revision.ToString(CultureInfo.InvariantCulture) : null;
                    if (policy.Revision <= 0)
                    {
                        collector.Add(
                            "policy.revision.non-positive",
                            path + "/revision",
                            entryId,
                            "Offline policy revision must be positive.");
                        individuallyValid = false;
                    }

                    if (!seen.Add(policy.Revision))
                    {
                        collector.Add(
                            "policy.revision.duplicate",
                            path + "/revision",
                            entryId,
                            $"Offline policy revision '{policy.Revision}' is repeated.");
                        individuallyValid = false;
                        hasDuplicate = true;
                    }

                    if (policy.EfficiencyNumerator <= 0 ||
                        policy.EfficiencyDenominator <= 0 ||
                        policy.EfficiencyNumerator >= policy.EfficiencyDenominator)
                    {
                        collector.Add(
                            "policy.ratio.invalid",
                            path,
                            entryId,
                            "Offline efficiency values must be positive and numerator must be less than denominator.");
                        individuallyValid = false;
                    }

                    if (policy.MaxCreditedTicks <= 0)
                    {
                        collector.Add(
                            "policy.cap.non-positive",
                            path + "/maxCreditedTicks",
                            entryId,
                            "Maximum credited ticks must be positive.");
                        individuallyValid = false;
                    }
                }
            }

            if (individuallyValid && !hasDuplicate && !IsSupportedPolicySet(policies))
            {
                collector.Add(
                    "policy.revision-1.required",
                    "/offlinePolicies",
                    null,
                    "The catalog requires exactly offline policy revision 1 with 4/5 efficiency and an eight-hour cap.");
            }
        }

        private static bool IsSupportedPolicySet(IList<OfflinePolicyDto> policies)
        {
            if (policies == null || policies.Count != 1 || policies[0] == null)
            {
                return false;
            }

            var policy = policies[0];
            return policy.Revision == DefinitionCatalog.SupportedOfflinePolicyRevision &&
                   policy.EfficiencyNumerator == DefinitionCatalog.SupportedEfficiencyNumerator &&
                   policy.EfficiencyDenominator == DefinitionCatalog.SupportedEfficiencyDenominator &&
                   policy.MaxCreditedTicks == DefinitionCatalog.SupportedMaxCreditedTicks;
        }

        private static void ValidateIdentity(
            string value,
            string path,
            string entryId,
            ISet<string> seen,
            DiagnosticCollector collector)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                collector.Add("id.empty", path, KnownEntryId(entryId) ?? KnownEntryId(value), "Identifier must not be empty or whitespace.");
                return;
            }

            if (value.Length > GameplayContentLimits.MaximumIdentifierLength)
            {
                collector.Add(
                    "id.too-long",
                    path,
                    KnownEntryId(entryId) ?? value,
                    $"Identifier exceeds {GameplayContentLimits.MaximumIdentifierLength} UTF-16 code units.");
            }

            var normalized = NormalizedId(value);
            if (normalized == null || !StringComparer.Ordinal.Equals(value, normalized))
            {
                collector.Add("id.not-canonical", path, KnownEntryId(entryId) ?? value, "Identifier must already be trimmed and Unicode NFC-normalized.");
            }

            if (seen != null && normalized != null && !seen.Add(normalized))
            {
                collector.Add("id.duplicate", path, KnownEntryId(entryId) ?? value, $"Identifier '{value}' is repeated in its family.");
            }
        }

        private static string NormalizedId(string value)
        {
            if (value == null)
            {
                return null;
            }

            try
            {
                return value.Trim().Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static bool IsCanonicalId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = NormalizedId(value);
            return normalized != null &&
                   normalized.Length > 0 &&
                   value.Length <= GameplayContentLimits.MaximumIdentifierLength &&
                   StringComparer.Ordinal.Equals(value, normalized);
        }

        private static string KnownEntryId(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;

        private static string Pointer(string parent, int index) =>
            JsonPointerParent(parent) + "/" + index.ToString(CultureInfo.InvariantCulture);

        private static string Pointer(string parent, string child) =>
            JsonPointerParent(parent) + "/" + child.Replace("~", "~0").Replace("/", "~1");

        private static string JsonPointerParent(string parent) =>
            parent.StartsWith("/", StringComparison.Ordinal) ? parent : "/" + parent;

        private sealed class DiagnosticCollector
        {
            private readonly List<ContentDiagnostic> diagnostics = new List<ContentDiagnostic>();
            private bool truncated;

            public IReadOnlyList<ContentDiagnostic> Diagnostics => diagnostics.AsReadOnly();

            public void Add(string code, string path, string entryId, string message)
            {
                if (diagnostics.Count < GameplayContentLimits.MaximumReportedDiagnostics)
                {
                    diagnostics.Add(new ContentDiagnostic(code, path, entryId, null, null, OneLine(message)));
                    return;
                }

                if (!truncated)
                {
                    truncated = true;
                    var last = diagnostics[diagnostics.Count - 1];
                    diagnostics[diagnostics.Count - 1] = new ContentDiagnostic(
                        last.Code,
                        last.Path,
                        last.EntryId,
                        last.Line,
                        last.Column,
                        last.Message + $" Additional semantic diagnostics were omitted after {GameplayContentLimits.MaximumReportedDiagnostics} violations.");
                }
            }

            private static string OneLine(string value) =>
                (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
        }
    }
}
