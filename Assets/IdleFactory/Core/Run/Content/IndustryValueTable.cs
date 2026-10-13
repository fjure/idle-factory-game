using System;
using IdleFactory.Core.Numerics;

namespace IdleFactory.Run.Content
{
    /// <summary>
    /// The per-level value table of an Industry upgrade (T331, F5-R08, ECO-7). Decay is applied per effect kind with
    /// iterated floors, <c>delta(L + 1) = floor(delta(L) x decayPpm / 10^6)</c>, so no power is ever evaluated:
    /// <list type="bullet">
    /// <item><b>Factor kinds</b> (<c>machineSpeed</c>, <c>salePrice</c>, <c>upgradeCost</c>, every non-additive kind): the entry of level L is the
    /// ppm factor that the fold multiplies once for that level. The multiplicative increment (<c>v &gt; 10^6</c>) or the discount decrement
    /// (<c>v &lt; 10^6</c>) <c>delta</c> starts at <c>|v - 10^6|</c> and decays; the factor is <c>10^6 +/- delta(L)</c>. The fold keeps its
    /// per-level floor (speed, sale price) or ceiling (cost), so the table never replaces the accumulator rounding.</item>
    /// <item><b>Additive kinds</b> (<c>startingCash</c>, <c>startingStorageLevel</c>): the entry of level L is the amount level L adds, starting at
    /// <c>v</c> and decaying.</item>
    /// </list>
    /// A <c>decayPpm</c> of 0 (the legacy default, unset) or 10^6 means no decay: every entry equals <c>v</c>, which is exactly the pre-T331
    /// behaviour. The table is derived from the catalog and never persisted.
    /// </summary>
    public static class IndustryValueTable
    {
        public const long Ppm = 1_000_000;

        /// <summary>True when <paramref name="decayPpm"/> actually decays (strictly between 0 and 10^6).</summary>
        public static bool IsDecaying(long decayPpm) => decayPpm > 0 && decayPpm < Ppm;

        /// <summary>True for the additive kinds (<c>startingCash</c>, <c>startingStorageLevel</c>).</summary>
        public static bool IsAdditive(string kind) =>
            string.Equals(kind, "startingCash", StringComparison.Ordinal) || string.Equals(kind, "startingStorageLevel", StringComparison.Ordinal);

        /// <summary>
        /// Builds the table of <paramref name="maxLevel"/> entries. False when a value is outside 0 ... 10^18 or the level count is not positive
        /// (the caller reports the content rule). With no decay every entry equals <paramref name="value"/>.
        /// </summary>
        public static bool TryBuild(string kind, long value, long decayPpm, int maxLevel, out long[] table)
        {
            table = null;
            if (maxLevel < 1 || value < 0 || value > ExactBound.Value || decayPpm < 0 || decayPpm > Ppm) return false;
            var result = new long[maxLevel];
            if (!IsDecaying(decayPpm))
            {
                for (int i = 0; i < result.Length; i++) result[i] = value;
                table = result;
                return true;
            }

            if (IsAdditive(kind))
            {
                long amount = value;
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = amount;
                    if (!MulDiv.MulDivFloor(amount, decayPpm, Ppm, out amount)) return false;
                }
            }
            else
            {
                bool increase = value >= Ppm;
                long delta = increase ? value - Ppm : Ppm - value;
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = increase ? Ppm + delta : Ppm - delta;
                    if (!MulDiv.MulDivFloor(delta, decayPpm, Ppm, out delta)) return false;
                }
            }

            table = result;
            return true;
        }

        /// <summary>
        /// The marginal value of level <paramref name="level"/> (1-based) in ppm of the base: the factor distance <c>|F - 10^6|</c> for factor kinds,
        /// and the added amount relative to the level-1 amount (<c>amount x 10^6 / amount(1)</c>, 0 when level 1 adds nothing) for additive kinds.
        /// </summary>
        public static long GainPpm(string kind, long[] table, int level)
        {
            if (table == null || level < 1 || level > table.Length) return 0;
            long entry = table[level - 1];
            if (!IsAdditive(kind)) return entry >= Ppm ? entry - Ppm : Ppm - entry;
            if (table[0] <= 0) return 0;
            return MulDiv.MulDivFloor(entry, Ppm, table[0], out long gain) ? gain : ExactBound.Value;
        }

        /// <summary>
        /// <c>industry.ceiling.level</c> (owner decision 2026-10-13, #396): the LAST level (1-based) whose marginal value is still at least
        /// <paramref name="minGainPpm"/>, which is <c>table.Length</c> when every level qualifies (always the case for <c>minGainPpm</c> 0), and 0 when
        /// not even level 1 qualifies (owner decision 2026-10-13-b, #396: the validator rejects that content as an authoring error; Core returns 0, "no worthwhile level", only so that the function is total). The unit of <paramref name="minGainPpm"/> differs per kind (F3):
        /// factor kinds measure ppm of the base, <c>|F - 10^6|</c>; additive kinds measure ppm of the level-1 amount, <c>amount(L) x 10^6 / amount(1)</c>.
        /// </summary>
        public static int PracticalCeiling(string kind, long[] table, long minGainPpm)
        {
            int ceiling = 0;
            for (int level = 1; level <= table.Length; level++)
                if (GainPpm(kind, table, level) >= minGainPpm) ceiling = level;
            return ceiling;
        }

        /// <summary>
        /// True when level <paramref name="level"/> (1-based) adds nothing after the iterated floors: the raw amount is 0 for additive kinds, and the
        /// factor equals 10^6 (the decayed delta is 0) for factor kinds. A base value that is already neutral (0, or exactly 10^6) is not dead by decay.
        /// </summary>
        public static bool IsDeadLevel(string kind, long baseValue, long[] table, int level)
        {
            if (table == null || level < 1 || level > table.Length) return false;
            if (IsAdditive(kind)) return baseValue > 0 && table[level - 1] == 0;
            return baseValue != Ppm && table[level - 1] == Ppm;
        }
    }
}
