using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IdleFactory.Domain
{
    internal static class DefinitionGuard
    {
        public static string RequiredText(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var normalized = value.Trim();
            if (normalized.Length == 0)
            {
                throw new ArgumentException("Value must not be empty or whitespace.", parameterName);
            }

            return normalized;
        }

        public static string OptionalText(string value, string parameterName)
        {
            return value == null ? null : RequiredText(value, parameterName);
        }

        public static ReadOnlyCollection<T> CopyRequiredList<T>(
            IEnumerable<T> source,
            string parameterName)
            where T : class
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<T>();
            foreach (var item in source)
            {
                if (item == null)
                {
                    throw new ArgumentException("Collection must not contain null entries.", parameterName);
                }

                copy.Add(item);
            }

            if (copy.Count == 0)
            {
                throw new ArgumentException("Collection must contain at least one entry.", parameterName);
            }

            return copy.AsReadOnly();
        }
    }

    public sealed class ResourceDefinition
    {
        public ResourceDefinition(
            ResourceId id,
            string developmentLabel,
            string presentationKey,
            bool isDevelopmentOnly)
        {
            _ = id.Value;
            Id = id;
            DevelopmentLabel = DefinitionGuard.RequiredText(developmentLabel, nameof(developmentLabel));
            PresentationKey = DefinitionGuard.OptionalText(presentationKey, nameof(presentationKey));
            IsDevelopmentOnly = isDevelopmentOnly;
        }

        public ResourceId Id { get; }
        public string DevelopmentLabel { get; }
        public string PresentationKey { get; }
        public bool IsDevelopmentOnly { get; }
    }

    public sealed class ResourceAmountDefinition
    {
        public ResourceAmountDefinition(ResourceId resourceId, long amount)
        {
            _ = resourceId.Value;
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Resource amount must be positive.");
            }

            ResourceId = resourceId;
            Amount = amount;
        }

        public ResourceId ResourceId { get; }
        public long Amount { get; }
    }

    public sealed class RecipeDefinition
    {
        public RecipeDefinition(
            RecipeId id,
            IEnumerable<ResourceAmountDefinition> inputs,
            IEnumerable<ResourceAmountDefinition> outputs,
            long durationTicks,
            bool isDevelopmentOnly)
        {
            _ = id.Value;
            if (durationTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationTicks),
                    durationTicks,
                    "Recipe duration must be positive.");
            }

            var inputCopy = DefinitionGuard.CopyRequiredList(inputs, nameof(inputs));
            var outputCopy = DefinitionGuard.CopyRequiredList(outputs, nameof(outputs));
            EnsureUniqueResources(inputCopy, nameof(inputs));
            EnsureUniqueResources(outputCopy, nameof(outputs));

            Id = id;
            Inputs = inputCopy;
            Outputs = outputCopy;
            DurationTicks = durationTicks;
            IsDevelopmentOnly = isDevelopmentOnly;
        }

        public RecipeId Id { get; }
        public IReadOnlyList<ResourceAmountDefinition> Inputs { get; }
        public IReadOnlyList<ResourceAmountDefinition> Outputs { get; }
        public long DurationTicks { get; }
        public bool IsDevelopmentOnly { get; }

        private static void EnsureUniqueResources(
            IEnumerable<ResourceAmountDefinition> amounts,
            string parameterName)
        {
            var seen = new HashSet<ResourceId>();
            foreach (var amount in amounts)
            {
                _ = amount.ResourceId.Value;
                if (!seen.Add(amount.ResourceId))
                {
                    throw new ArgumentException("Resource entries must be unique after ID normalization.", parameterName);
                }
            }
        }
    }

    public enum BufferRole
    {
        Input = 0,
        Output = 1,
    }

    public sealed class BufferDefinition
    {
        public BufferDefinition(
            BufferRole role,
            IEnumerable<KeyValuePair<ResourceId, long>> capacities)
        {
            if (!Enum.IsDefined(typeof(BufferRole), role))
            {
                throw new ArgumentOutOfRangeException(nameof(role), role, "Buffer role is unsupported.");
            }

            if (capacities == null)
            {
                throw new ArgumentNullException(nameof(capacities));
            }

            var copy = new Dictionary<ResourceId, long>();
            foreach (var capacity in capacities)
            {
                _ = capacity.Key.Value;
                if (capacity.Value < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(capacities),
                        capacity.Value,
                        "Buffer capacity must be nonnegative.");
                }

                if (copy.ContainsKey(capacity.Key))
                {
                    throw new ArgumentException(
                        "Buffer resources must be unique after ID normalization.",
                        nameof(capacities));
                }

                copy.Add(capacity.Key, capacity.Value);
            }

            if (copy.Count == 0)
            {
                throw new ArgumentException("Buffer must declare at least one resource.", nameof(capacities));
            }

            Role = role;
            Capacities = new ReadOnlyDictionary<ResourceId, long>(copy);
        }

        public BufferRole Role { get; }
        public IReadOnlyDictionary<ResourceId, long> Capacities { get; }
    }

    public sealed class BuildingDefinition
    {
        public BuildingDefinition(
            BuildingDefinitionId id,
            Footprint footprint,
            IEnumerable<int> allowedOrientations,
            RecipeId? recipeId,
            BufferDefinition inputBuffer,
            BufferDefinition outputBuffer,
            string presentationKey,
            bool isDevelopmentOnly)
        {
            _ = id.Value;
            _ = footprint.Width;
            _ = footprint.Height;
            if (recipeId.HasValue)
            {
                _ = recipeId.Value.Value;
            }

            if (inputBuffer != null && inputBuffer.Role != BufferRole.Input)
            {
                throw new ArgumentException("Input buffer must have the Input role.", nameof(inputBuffer));
            }

            if (outputBuffer != null && outputBuffer.Role != BufferRole.Output)
            {
                throw new ArgumentException("Output buffer must have the Output role.", nameof(outputBuffer));
            }

            if (allowedOrientations == null)
            {
                throw new ArgumentNullException(nameof(allowedOrientations));
            }

            var orientationCopy = new List<int>();
            var seenOrientations = new HashSet<int>();
            foreach (var orientation in allowedOrientations)
            {
                if (orientation < 0 || orientation > 3)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(allowedOrientations),
                        orientation,
                        "Allowed orientations must be quarter turns from 0 through 3.");
                }

                if (!seenOrientations.Add(orientation))
                {
                    throw new ArgumentException("Allowed orientations must be unique.", nameof(allowedOrientations));
                }

                orientationCopy.Add(orientation);
            }

            if (orientationCopy.Count == 0)
            {
                throw new ArgumentException("At least one orientation must be allowed.", nameof(allowedOrientations));
            }

            orientationCopy.Sort();

            Id = id;
            Footprint = footprint;
            AllowedOrientations = orientationCopy.AsReadOnly();
            RecipeId = recipeId;
            InputBuffer = inputBuffer;
            OutputBuffer = outputBuffer;
            PresentationKey = DefinitionGuard.RequiredText(presentationKey, nameof(presentationKey));
            IsDevelopmentOnly = isDevelopmentOnly;
        }

        public BuildingDefinitionId Id { get; }
        public Footprint Footprint { get; }
        public IReadOnlyList<int> AllowedOrientations { get; }
        public RecipeId? RecipeId { get; }
        public BufferDefinition InputBuffer { get; }
        public BufferDefinition OutputBuffer { get; }
        public string PresentationKey { get; }
        public bool IsDevelopmentOnly { get; }
    }

    public sealed class OfflinePolicyDefinition
    {
        public OfflinePolicyDefinition(
            OfflinePolicyRevision revision,
            long efficiencyNumerator,
            long efficiencyDenominator,
            long maxCreditedTicks)
        {
            _ = revision.Value;
            if (efficiencyNumerator <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(efficiencyNumerator),
                    efficiencyNumerator,
                    "Efficiency numerator must be positive.");
            }

            if (efficiencyDenominator <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(efficiencyDenominator),
                    efficiencyDenominator,
                    "Efficiency denominator must be positive.");
            }

            if (efficiencyNumerator >= efficiencyDenominator)
            {
                throw new ArgumentException(
                    "Efficiency numerator must be less than its denominator.",
                    nameof(efficiencyNumerator));
            }

            if (maxCreditedTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxCreditedTicks),
                    maxCreditedTicks,
                    "Maximum credited ticks must be positive.");
            }

            Revision = revision;
            EfficiencyNumerator = efficiencyNumerator;
            EfficiencyDenominator = efficiencyDenominator;
            MaxCreditedTicks = maxCreditedTicks;
        }

        public OfflinePolicyRevision Revision { get; }
        public long EfficiencyNumerator { get; }
        public long EfficiencyDenominator { get; }
        public long MaxCreditedTicks { get; }
    }
}
