using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IdleFactory.Run.Content
{
    public sealed class ItemDef
    {
        public string Id { get; }
        public int Tier { get; }
        public string Label { get; }
        public string PresentationKey { get; }
        public long DurationMs { get; }
        public string TargetKind { get; }
        public IReadOnlyList<string> AllowedMachineIds { get; }
        public EffectDef Effect { get; }
        public ItemDef(string id, int tier, string label, string presentationKey, long durationMs,
            string targetKind, IEnumerable<string> allowedMachineIds, EffectDef effect)
        {
            Id = ContentGuard.Id(id, nameof(id));
            if (tier < 1 || tier > 3) throw new ArgumentOutOfRangeException(nameof(tier));
            Tier = tier; Label = ContentGuard.Text(label, nameof(label));
            PresentationKey = ContentGuard.Text(presentationKey, nameof(presentationKey));
            DurationMs = ContentGuard.PositiveQuantity(durationMs, nameof(durationMs));
            if (targetKind != "factory" && targetKind != "machineType") throw new ArgumentException("item.target.invalid", nameof(targetKind));
            TargetKind = targetKind;
            var ids = ContentGuard.Ids(allowedMachineIds, nameof(allowedMachineIds));
            if ((targetKind == "machineType") != (ids.Count > 0)) throw new ArgumentException("item.target.invalid", nameof(allowedMachineIds));
            AllowedMachineIds = new ReadOnlyCollection<string>(ids);
            Effect = effect ?? throw new ArgumentNullException(nameof(effect));
            if (effect.Kind != "machineSpeed" && effect.Kind != "machineOutput")
                throw new ArgumentException("effect.kind.unknown for Item", nameof(effect));
            if (effect.Kind == "machineOutput" && effect.TargetKind != "machineType")
                throw new ArgumentException("effect.target.invalid for Item", nameof(effect));
        }
    }
}
