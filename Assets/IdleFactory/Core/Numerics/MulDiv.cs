namespace IdleFactory.Core.Numerics
{
    /// <summary>
    /// Allocation-free 128-bit intermediate arithmetic for ppm composition, progress rescale, prices and
    /// the IP curve (research R06). No BigInteger and no floating point. All operands are nonnegative;
    /// a negative operand, a non-positive divisor or a result above <see cref="ExactBound.Value"/> returns
    /// false and leaves the output at 0 (atomic rejection, the caller keeps its state).
    /// </summary>
    public static class MulDiv
    {
        private const ulong LowMask = 0xFFFFFFFFUL;

        /// <summary>Full 128-bit product of two unsigned 64-bit values as (high, low) halves.</summary>
        public static void Multiply128(ulong a, ulong b, out ulong high, out ulong low)
        {
            ulong a0 = a & LowMask, a1 = a >> 32;
            ulong b0 = b & LowMask, b1 = b >> 32;
            ulong p00 = a0 * b0;
            ulong p01 = a0 * b1;
            ulong p10 = a1 * b0;
            ulong p11 = a1 * b1;
            ulong mid = (p00 >> 32) + (p01 & LowMask) + (p10 & LowMask);
            low = (p00 & LowMask) | (mid << 32);
            high = p11 + (p01 >> 32) + (p10 >> 32) + (mid >> 32);
        }

        /// <summary>
        /// floor(a * b / divisor) over the 128-bit product. False when the quotient does not fit in 64 bits
        /// or the divisor is 0.
        /// </summary>
        public static bool TryMulDivFloor(ulong a, ulong b, ulong divisor, out ulong quotient)
        {
            return TryDivide(a, b, divisor, out quotient, out _);
        }

        /// <summary>ceil(a * b / divisor) over the 128-bit product. False on 64-bit overflow or divisor 0.</summary>
        public static bool TryMulDivCeil(ulong a, ulong b, ulong divisor, out ulong quotient)
        {
            if (!TryDivide(a, b, divisor, out quotient, out ulong remainder))
            {
                return false;
            }

            if (remainder == 0)
            {
                return true;
            }

            if (quotient == ulong.MaxValue)
            {
                quotient = 0;
                return false;
            }

            quotient++;
            return true;
        }

        /// <summary>
        /// floor(a * b / divisor) as a quantity. False when an operand is negative, the divisor is not
        /// positive, or the result exceeds <see cref="ExactBound.Value"/>.
        /// </summary>
        public static bool MulDivFloor(long a, long b, long divisor, out long result)
        {
            result = 0;
            if (a < 0 || b < 0 || divisor <= 0)
            {
                return false;
            }

            if (!TryMulDivFloor((ulong)a, (ulong)b, (ulong)divisor, out ulong q) || q > (ulong)ExactBound.Value)
            {
                return false;
            }

            result = (long)q;
            return true;
        }

        /// <summary>ceil(a * b / divisor) as a quantity, with the same rejection rules as <see cref="MulDivFloor"/>.</summary>
        public static bool MulDivCeil(long a, long b, long divisor, out long result)
        {
            result = 0;
            if (a < 0 || b < 0 || divisor <= 0)
            {
                return false;
            }

            if (!TryMulDivCeil((ulong)a, (ulong)b, (ulong)divisor, out ulong q) || q > (ulong)ExactBound.Value)
            {
                return false;
            }

            result = (long)q;
            return true;
        }

        private static bool TryDivide(ulong a, ulong b, ulong divisor, out ulong quotient, out ulong remainder)
        {
            quotient = 0;
            remainder = 0;
            if (divisor == 0)
            {
                return false;
            }

            Multiply128(a, b, out ulong high, out ulong low);
            if (high == 0)
            {
                quotient = low / divisor;
                remainder = low - (quotient * divisor);
                return true;
            }

            // The 64-bit quotient overflows exactly when high >= divisor.
            if (high >= divisor)
            {
                return false;
            }

            // Restoring shift-subtract division of (high:low) by divisor; high < divisor keeps r < divisor.
            ulong r = high;
            ulong q = 0;
            for (int i = 63; i >= 0; i--)
            {
                ulong carry = r >> 63;
                r = (r << 1) | ((low >> i) & 1UL);
                if (carry != 0 || r >= divisor)
                {
                    r -= divisor;
                    q |= 1UL << i;
                }
            }

            quotient = q;
            remainder = r;
            return true;
        }
    }
}
