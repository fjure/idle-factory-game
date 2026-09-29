using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IdleFactory.Run.Content
{
    public sealed class ContractRewardDef
    {
        public string Kind { get; }
        public Curve Curve { get; }
        public string ItemId { get; }
        public IReadOnlyList<int> OnCompletionIndices { get; }
        public IReadOnlyList<long> ValueTable { get; }
        public ContractRewardDef(string kind, Curve curve = null, string itemId = null, IEnumerable<int> onCompletionIndices = null)
        {
            if (kind != "cash" && kind != "rp" && kind != "item") throw new ArgumentException("contract.reward.kind.unknown", nameof(kind));
            if ((kind == "item") != (itemId != null)) throw new ArgumentException("Item ID required exactly for item reward.", nameof(itemId));
            if ((kind == "item") == (curve != null)) throw new ArgumentException("Reward curve required exactly for cash and rp.", nameof(curve));
            Kind = kind; Curve = curve; ItemId = itemId == null ? null : ContentGuard.Id(itemId, nameof(itemId));
            var indices = new List<int>();
            if (onCompletionIndices != null) foreach (int index in onCompletionIndices)
            {
                if (index < 0 || index >= 1024 || indices.Contains(index)) throw new ArgumentOutOfRangeException(nameof(onCompletionIndices));
                indices.Add(index);
                if (indices.Count > 256) throw new ArgumentException("Too many completion indices.", nameof(onCompletionIndices));
            }
            if (kind != "item" && indices.Count != 0) throw new ArgumentException("Only item rewards have completion indices.", nameof(onCompletionIndices));
            OnCompletionIndices = new ReadOnlyCollection<int>(indices);
            ValueTable = curve == null ? null : new ReadOnlyCollection<long>(curve.Precompute(1024));
        }
    }

    public sealed class ContractOfferDef
    {
        public string Id { get; }
        public string Kind { get; }
        public string ResourceId { get; }
        public Curve QuantityCurve { get; }
        public IReadOnlyList<long> QuantityTable { get; }
        public long? DurationMs { get; }
        public IReadOnlyList<ContractRewardDef> Rewards { get; }
        public UnlockCondition Unlock { get; }
        public ContractOfferDef(string id, string kind, string resourceId, Curve quantityCurve,
            long? durationMs, IEnumerable<ContractRewardDef> rewards, UnlockCondition unlock)
        {
            Id = ContentGuard.Id(id, nameof(id));
            if (kind != "plate" && kind != "gear" && kind != "timedBar") throw new ArgumentException("contract.kind.unknown", nameof(kind));
            Kind = kind; ResourceId = ContentGuard.Id(resourceId, nameof(resourceId));
            QuantityCurve = quantityCurve ?? throw new ArgumentNullException(nameof(quantityCurve));
            if (kind == "timedBar" && (!durationMs.HasValue || durationMs.Value < 1)) throw new ArgumentException("contract.duration.required", nameof(durationMs));
            if (kind != "timedBar" && durationMs.HasValue) throw new ArgumentException("contract.duration.forbidden", nameof(durationMs));
            DurationMs = durationMs;
            Rewards = new ReadOnlyCollection<ContractRewardDef>(ContentGuard.List(rewards, nameof(rewards), 1));
            Unlock = unlock ?? throw new ArgumentNullException(nameof(unlock));
            QuantityTable = new ReadOnlyCollection<long>(quantityCurve.Precompute(1024));
        }
    }
}
