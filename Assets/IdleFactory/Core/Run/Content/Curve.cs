using System;
using IdleFactory.Core.Numerics;

namespace IdleFactory.Run.Content
{
    /// <summary>Integer curve. Index zero is Base; each later value is rounded from the previous value.</summary>
    public sealed class Curve
    {
        public const long Ppm = 1_000_000;
        public long Base { get; }
        public long GrowthPpm { get; }
        public string Rounding { get; }

        public Curve(long @base, long growthPpm, string rounding)
        {
            if (!ExactBound.Contains(@base)) throw new ArgumentOutOfRangeException(nameof(@base));
            if (growthPpm < Ppm || growthPpm > ExactBound.Value) throw new ArgumentOutOfRangeException(nameof(growthPpm), "curve.growth.invalid");
            if (rounding != "floor" && rounding != "ceil") throw new ArgumentException("Expected floor or ceil.", nameof(rounding));
            Base = @base;
            GrowthPpm = growthPpm;
            Rounding = rounding;
        }

        public long[] Precompute(int count)
        {
            if (count < 1 || count > 1025) throw new ArgumentOutOfRangeException(nameof(count));
            var values = new long[count];
            values[0] = Base;
            for (int i = 1; i < count; i++)
            {
                long value;
                bool ok = Rounding == "ceil"
                    ? MulDiv.MulDivCeil(values[i - 1], GrowthPpm, Ppm, out value)
                    : MulDiv.MulDivFloor(values[i - 1], GrowthPpm, Ppm, out value);
                if (!ok) throw new ArgumentException("curve.overflow", nameof(count));
                values[i] = value;
            }
            return values;
        }
    }
}
