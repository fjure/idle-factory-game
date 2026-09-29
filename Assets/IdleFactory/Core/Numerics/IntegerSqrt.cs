namespace IdleFactory.Core.Numerics
{
    /// <summary>Integer square root by Newton's method on <c>ulong</c>; exact, allocation-free, no floating point.</summary>
    public static class IntegerSqrt
    {
        /// <summary>The largest r with r * r &lt;= n.</summary>
        public static ulong ISqrt(ulong n)
        {
            if (n < 2)
            {
                return n;
            }

            int bits = 0;
            for (ulong t = n; t != 0; t >>= 1)
            {
                bits++;
            }

            // 2^ceil(bits/2) is at or above sqrt(n) and at most 2^32, so x + n / x cannot overflow.
            ulong x = 1UL << ((bits + 1) >> 1);
            while (true)
            {
                ulong y = (x + (n / x)) >> 1;
                if (y >= x)
                {
                    return x;
                }

                x = y;
            }
        }
    }
}
