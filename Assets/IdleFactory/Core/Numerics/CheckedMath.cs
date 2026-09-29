using System;

namespace IdleFactory.Core.Numerics
{
    /// <summary>
    /// Checked quantity arithmetic (research R06, FR-035). Operands and results are quantities in
    /// <c>[0, ExactBound.Value]</c>. Every method returns false, with the output set to 0, when an operand is
    /// outside that range or the result would exceed the bound or <see cref="long"/>; nothing throws and
    /// nothing allocates, so a caller can reject a whole command atomically with <c>ArithmeticOverflow</c>.
    /// </summary>
    public static class CheckedMath
    {
        public static bool TryAdd(long a, long b, out long result)
        {
            result = 0;
            if (!ExactBound.Contains(a) || !ExactBound.Contains(b))
            {
                return false;
            }

            // Both operands are at most 10^18, so the sum cannot wrap Int64.
            long sum = a + b;
            if (sum > ExactBound.Value)
            {
                return false;
            }

            result = sum;
            return true;
        }

        public static bool TryMultiply(long a, long b, out long result)
        {
            result = 0;
            if (!ExactBound.Contains(a) || !ExactBound.Contains(b))
            {
                return false;
            }

            MulDiv.Multiply128((ulong)a, (ulong)b, out ulong high, out ulong low);
            if (high != 0 || low > (ulong)ExactBound.Value)
            {
                return false;
            }

            result = (long)low;
            return true;
        }

        /// <summary>Sum of all values; fails atomically if any operand or any partial sum leaves the range.</summary>
        public static bool TrySum(ReadOnlySpan<long> values, out long result)
        {
            result = 0;
            long total = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (!TryAdd(total, values[i], out total))
                {
                    return false;
                }
            }

            result = total;
            return true;
        }

        /// <summary>a - b for quantities; false when b exceeds a (a quantity cannot go negative).</summary>
        public static bool TrySubtract(long a, long b, out long result)
        {
            result = 0;
            if (!ExactBound.Contains(a) || !ExactBound.Contains(b) || b > a)
            {
                return false;
            }

            result = a - b;
            return true;
        }
    }
}
