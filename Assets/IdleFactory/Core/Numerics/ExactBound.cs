namespace IdleFactory.Core.Numerics
{
    /// <summary>
    /// Closed upper bound for every exact quantity (resource units, Cash, RP, IP, item counts): 10^18.
    /// Quantities are <c>long</c> values in <c>[0, Value]</c>. The bound leaves 9x headroom inside
    /// <see cref="long.MaxValue"/>, so a sum of two in-range values never wraps before the bound check
    /// (research R06, FR-035).
    /// </summary>
    public static class ExactBound
    {
        public const long Value = 1_000_000_000_000_000_000L;

        /// <summary>True when <paramref name="value"/> is a valid quantity, <c>0 ... Value</c>.</summary>
        public static bool Contains(long value)
        {
            return value >= 0 && value <= Value;
        }
    }
}
