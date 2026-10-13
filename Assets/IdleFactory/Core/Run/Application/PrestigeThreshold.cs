using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;

namespace IdleFactory.Run.Application
{
    /// <summary>
    /// The rising prestige threshold (T331, D-K, F5-R07, ECO-7): <c>T(n) = ceil(base x growth^n / 10^6^n)</c> with iterated floors on a
    /// scaled accumulator: <c>acc = base x 10^6</c>, per step <c>acc = floor(acc x growthPpm / 10^6)</c>, and one final <c>ceil(acc / 10^6)</c>.
    /// The index is <c>min(PrestigeCount, MaxIndex)</c>, so beyond <c>MaxIndex</c> the threshold stays constant and prestige continues. The threshold
    /// is derived from <c>PrestigeCount</c>, which is already persisted: nothing here is saved, and a retried or interrupted prestige that advances the
    /// count once advances the index once. Pure, allocation-free and checked: a result above 10^18 reports false (the caller rejects with
    /// <c>value.limit</c>); the validator rule <c>prestige.threshold.bound</c> keeps shipped content below it.
    /// The accumulator reaches 10^24, so it is kept as <c>quotient x 10^6 + remainder</c> and every product goes through the 128-bit helpers.
    /// </summary>
    public static class PrestigeThreshold
    {
        private const long Ppm = 1_000_000;

        /// <summary>The clamped index <c>min(prestigeCount, MaxIndex)</c>; 0 for a legacy definition without a threshold growth.</summary>
        public static long Index(PrestigeDef prestige, long prestigeCount)
        {
            if (prestige == null || prestige.Threshold == null || prestigeCount <= 0) return 0;
            return prestigeCount < prestige.Threshold.MaxIndex ? prestigeCount : prestige.Threshold.MaxIndex;
        }

        /// <summary>
        /// The threshold that applies to a player with <paramref name="prestigeCount"/> prestiges: <c>For(min(count, MaxIndex))</c>, or the legacy
        /// <see cref="PrestigeDef.ThresholdFactoryValue"/> when the definition has no growth.
        /// </summary>
        public static bool TryForCount(PrestigeDef prestige, long prestigeCount, out long threshold)
        {
            threshold = 0;
            if (prestige == null) return false;
            if (prestige.Threshold == null)
            {
                threshold = prestige.ThresholdFactoryValue;
                return true;
            }

            return TryFor(prestige.Threshold, Index(prestige, prestigeCount), out threshold);
        }

        /// <summary>The threshold after the next prestige: <c>For(min(count + 1, MaxIndex))</c>.</summary>
        public static bool TryNextForCount(PrestigeDef prestige, long prestigeCount, out long threshold)
        {
            threshold = 0;
            if (prestige == null || prestigeCount < 0 || prestigeCount >= long.MaxValue) return false;
            return TryForCount(prestige, prestigeCount + 1, out threshold);
        }

        /// <summary><c>T(index)</c> for an already clamped index. False for a negative index, a base below 1, a growth below 10^6, or a result above 10^18.</summary>
        public static bool TryFor(ThresholdDef threshold, long index, out long value)
        {
            value = 0;
            if (threshold == null || index < 0 || threshold.Base < 1 || threshold.Base > ExactBound.Value || threshold.GrowthPpm < Ppm) return false;
            long growth = threshold.GrowthPpm;
            long quotient = threshold.Base;
            long remainder = 0;
            // growth = 10^6 leaves the accumulator unchanged, so a constant threshold needs no walk (a MaxIndex of 10^18 stays O(1)).
            if (growth == Ppm) index = 0;
            for (long step = 0; step < index; step++)
            {
                if (!Step(ref quotient, ref remainder, growth)) return false;
            }

            long result = remainder > 0 ? quotient + 1 : quotient;
            if (result < 1 || result > ExactBound.Value) return false;
            value = result;
            return true;
        }

        /// <summary>One step on <c>acc = quotient x 10^6 + remainder</c>: <c>acc = floor(acc x growth / 10^6)</c>, exactly.</summary>
        private static bool Step(ref long quotient, ref long remainder, long growth)
        {
            // acc x g / 10^6 = quotient x g + floor(remainder x g / 10^6), since quotient x g x 10^6 / 10^6 is an integer.
            // The new quotient is floor(quotient x g / 10^6) plus the carry of (quotient x g mod 10^6) + part.
            if (!MulDiv.TryMulDivFloor((ulong)quotient, (ulong)growth, Ppm, out ulong whole)) return false;
            ulong low = unchecked(((ulong)quotient * (ulong)growth) - (whole * Ppm)); // quotient x g mod 10^6, exact in wrapped arithmetic
            if (!MulDiv.TryMulDivFloor((ulong)remainder, (ulong)growth, Ppm, out ulong part)) return false;
            ulong carry = low + part; // below 10^6 + 10^18
            ulong newQuotient = whole + (carry / Ppm);
            if (newQuotient < whole || newQuotient > (ulong)ExactBound.Value) return false;
            quotient = (long)newQuotient;
            remainder = (long)(carry % Ppm);
            return true;
        }
    }
}
