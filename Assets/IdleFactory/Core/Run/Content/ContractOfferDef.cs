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

        private ContractRewardDef(string kind, IReadOnlyList<long> valueTable)
        {
            Kind = kind; Curve = null; ItemId = null;
            OnCompletionIndices = new ReadOnlyCollection<int>(new List<int>());
            ValueTable = new ReadOnlyCollection<long>(new List<long>(valueTable));
        }

        /// <summary>
        /// Transitional (content schema v3 reward adapter, removed with T320): a cash or rp reward whose 1024-entry value table is
        /// supplied directly instead of derived from a <see cref="Curve"/>. <see cref="Curve"/> is null for such a reward.
        /// </summary>
        public static ContractRewardDef FromTable(string kind, IReadOnlyList<long> valueTable)
        {
            if (kind != "cash" && kind != "rp") throw new ArgumentException("Table rewards are cash or rp.", nameof(kind));
            if (valueTable == null) throw new ArgumentNullException(nameof(valueTable));
            if (valueTable.Count != 1024) throw new ArgumentException("A reward table has 1024 entries.", nameof(valueTable));
            for (int i = 0; i < valueTable.Count; i++) ContentGuard.Quantity(valueTable[i], nameof(valueTable));
            return new ContractRewardDef(kind, valueTable);
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
        /// <summary>v3, inert. "deliver" or "produce". Legacy default "deliver".</summary>
        public string Objective { get; }
        /// <summary>v3, inert. "standing" or "bonus". Legacy default "standing".</summary>
        public string Class { get; }
        /// <summary>v3, inert. Cash reward ratio in ppm. Legacy default 0 (unset).</summary>
        public long RewardRatioPpm { get; }
        /// <summary>v3, inert. Cooldown in ms. Legacy default 0.</summary>
        public long CooldownMs { get; }
        /// <summary>v3, inert. Bonus trigger and window; legacy default null.</summary>
        public BonusDef Bonus { get; }
        public ContractOfferDef(string id, string kind, string resourceId, Curve quantityCurve,
            long? durationMs, IEnumerable<ContractRewardDef> rewards, UnlockCondition unlock,
            string objective = "deliver", string offerClass = "standing", long rewardRatioPpm = 0, long cooldownMs = 0, BonusDef bonus = null)
        {
            if (objective != "deliver" && objective != "produce") throw new ArgumentException("contract.objective.unknown", nameof(objective));
            if (offerClass != "standing" && offerClass != "bonus") throw new ArgumentException("contract.class.unknown", nameof(offerClass));
            Objective = objective; Class = offerClass; Bonus = bonus;
            RewardRatioPpm = ContentGuard.Quantity(rewardRatioPpm, nameof(rewardRatioPpm));
            CooldownMs = ContentGuard.Quantity(cooldownMs, nameof(cooldownMs));
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
