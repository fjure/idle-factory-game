using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IdleFactory.Run.Content
{
    public sealed class ResourceAmount
    {
        public string ResourceId { get; }
        public long Amount { get; }
        public ResourceAmount(string resourceId, long amount)
        {
            ResourceId = ContentGuard.Id(resourceId, nameof(resourceId));
            Amount = ContentGuard.PositiveQuantity(amount, nameof(amount));
        }
    }

    public sealed class MachineRecipe
    {
        public string Id { get; }
        public IReadOnlyList<ResourceAmount> Inputs { get; }
        public IReadOnlyList<ResourceAmount> Outputs { get; }
        /// <summary>100 ns ticks, 1 through 10^12.</summary>
        public long DurationTicks { get; }

        public MachineRecipe(string id, IEnumerable<ResourceAmount> inputs, IEnumerable<ResourceAmount> outputs, long durationTicks)
        {
            Id = ContentGuard.Id(id, nameof(id));
            Inputs = CopySide(inputs, false, nameof(inputs));
            Outputs = CopySide(outputs, true, nameof(outputs));
            if (durationTicks < 1 || durationTicks > 1_000_000_000_000L)
                throw new ArgumentOutOfRangeException(nameof(durationTicks));
            DurationTicks = durationTicks;
        }

        private static IReadOnlyList<ResourceAmount> CopySide(IEnumerable<ResourceAmount> source, bool required, string name)
        {
            if (source == null) throw new ArgumentNullException(name);
            var values = new List<ResourceAmount>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in source)
            {
                if (value == null) throw new ArgumentException("Null recipe amount.", name);
                if (!seen.Add(value.ResourceId)) throw new ArgumentException("recipe.resource.duplicate", name);
                values.Add(value);
                if (values.Count > 256) throw new ArgumentException("Recipe side exceeds 256 resources.", name);
            }
            if (required && values.Count == 0) throw new ArgumentException("recipe.outputs.empty", name);
            return new ReadOnlyCollection<ResourceAmount>(values);
        }
    }
}
