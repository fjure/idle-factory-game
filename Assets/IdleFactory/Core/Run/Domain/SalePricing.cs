using IdleFactory.Core.Numerics;

namespace IdleFactory.Run.Domain
{
    /// <summary>
    /// T316 (F5-R04, GD-2): the one sale price. <c>Value = MulDivFloor(units x basePrice, salePricePpm, 10^6)</c> with a
    /// checked <c>units x basePrice</c>. It is the only sale arithmetic; <c>RunSimulator.AutoSell</c> and
    /// <c>FactoryValuation</c> call it, and the later <c>Sell</c> command and the contract reward (GD-9) will too, so a
    /// sale and the valuation of the same stock use one function and one floor (full-stock neutrality, FR-002).
    ///
    /// Pure and stateless: no state ownership, no allocation, no thread affinity. The arithmetic is exactly the former
    /// inline expression (<c>CheckedMath.TryMultiply</c> then <c>MulDiv.MulDivFloor</c>), so at
    /// <c>salePricePpm = 10^6</c> the value is <c>units x basePrice</c> and every existing result is bit-identical.
    /// Internal: it is not public API (callers are in this assembly; the tests see it through InternalsVisibleTo).
    /// </summary>
    internal static class SalePricing
    {
        /// <summary>The ppm scale: a multiplier of 10^6 prices at the base price.</summary>
        internal const long Ppm = 1_000_000L;

        /// <summary>
        /// The income of selling <paramref name="units"/> of a resource whose base price is <paramref name="basePrice"/>
        /// at the price multiplier <paramref name="salePricePpm"/>. False, with <paramref name="value"/> 0, when an
        /// operand is negative or above the exact bound (10^18), the gross value <c>units x basePrice</c> exceeds the
        /// bound, or the floored result does. Nothing is clamped; the caller rejects atomically.
        /// </summary>
        internal static bool TryValue(long units, long basePrice, long salePricePpm, out long value)
        {
            value = 0;
            return CheckedMath.TryMultiply(units, basePrice, out long gross) &&
                   MulDiv.MulDivFloor(gross, salePricePpm, Ppm, out value);
        }
    }
}
