using System;

namespace IdleFactory.Run.Content
{
    public sealed class EffectDef
    {
        public string Kind { get; }
        public string TargetKind { get; }
        public string TargetId { get; }
        public long Value { get; }

        public EffectDef(string kind, string targetKind, string targetId, long value, long maxOutputFactor = 4)
        {
            if (kind != "machineSpeed" && kind != "machineOutput" && kind != "salePrice" &&
                kind != "upgradeCost" && kind != "startingCash" && kind != "startingStorageLevel")
                throw new ArgumentException("effect.kind.unknown", nameof(kind));
            bool targetValid = kind == "machineSpeed" ? targetKind == "allMachines" || targetKind == "machine" || targetKind == "machineType" || targetKind == "factory"
                : kind == "machineOutput" ? targetKind == "machine" || targetKind == "machineType" || targetKind == "factory"
                : kind == "salePrice" ? targetKind == "allResources" || targetKind == "resource"
                : kind == "upgradeCost" ? targetKind == "allMachines" || targetKind == "machine" || targetKind == "expansion"
                : targetKind == "none";
            if (!targetValid) throw new ArgumentException("effect.target.invalid", nameof(targetKind));
            bool needsId = targetKind == "machine" || targetKind == "resource";
            if (needsId != (targetId != null)) throw new ArgumentException("effect.target.invalid", nameof(targetId));
            if (needsId) ContentGuard.Id(targetId, nameof(targetId));
            bool valueValid = kind == "machineOutput" ? value >= 2 && value <= maxOutputFactor
                : kind == "upgradeCost" ? value >= 100_000 && value < Curve.Ppm
                : kind == "machineSpeed" || kind == "salePrice" ? value > Curve.Ppm && value <= IdleFactory.Core.Numerics.ExactBound.Value
                : value >= 1 && value <= IdleFactory.Core.Numerics.ExactBound.Value;
            if (!valueValid) throw new ArgumentOutOfRangeException(nameof(value), "effect.value.out-of-range");
            Kind = kind; TargetKind = targetKind; TargetId = targetId; Value = value;
        }
    }
}
