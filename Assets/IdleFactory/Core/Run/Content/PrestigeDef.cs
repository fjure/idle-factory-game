using System;

namespace IdleFactory.Run.Content
{
    public sealed class PrestigeDef
    {
        public long ThresholdFactoryValue { get; }
        public string CurveKind { get; }
        public long Coefficient { get; }
        public long Divisor { get; }
        /// <summary>v3, inert. Threshold growth; legacy default null (ThresholdFactoryValue stays authoritative).</summary>
        public ThresholdDef Threshold { get; }
        public PrestigeDef(long thresholdFactoryValue, string curveKind, long coefficient, long divisor, ThresholdDef threshold = null)
        {
            Threshold = threshold;
            ThresholdFactoryValue = ContentGuard.PositiveQuantity(thresholdFactoryValue, nameof(thresholdFactoryValue));
            if (curveKind != "sqrt") throw new ArgumentException("prestige.curve.unsupported", nameof(curveKind));
            CurveKind = curveKind;
            Coefficient = ContentGuard.PositiveQuantity(coefficient, nameof(coefficient));
            Divisor = ContentGuard.PositiveQuantity(divisor, nameof(divisor));
        }
    }
}
