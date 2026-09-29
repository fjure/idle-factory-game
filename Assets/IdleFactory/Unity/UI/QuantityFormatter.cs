using System;

namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// Culture-invariant display of exact quantities (research R06, FR-035). Pure C#, no Unity API.
    /// Values below 10,000 show exactly with "," grouping. Larger values show three significant digits,
    /// floored so a displayed value never implies affordability that does not exist, with the suffixes
    /// K, M, B, T, Qa, Qi and scientific notation beyond. <see cref="FormatExact"/> gives the exact value
    /// for inspection. The formatter is total: it never throws. A negative value, which is not a valid
    /// quantity, is shown as "-" followed by the floored display of its magnitude.
    /// </summary>
    public static class QuantityFormatter
    {
        public const int ExactBelow = 10_000;

        private static readonly string[] Suffixes = { "", "K", "M", "B", "T", "Qa", "Qi" };

        private static readonly ulong[] PowersOfTen =
        {
            1UL, 10UL, 100UL, 1_000UL, 10_000UL, 100_000UL, 1_000_000UL, 10_000_000UL, 100_000_000UL,
            1_000_000_000UL, 10_000_000_000UL, 100_000_000_000UL, 1_000_000_000_000UL,
            10_000_000_000_000UL, 100_000_000_000_000UL, 1_000_000_000_000_000UL,
            10_000_000_000_000_000UL, 100_000_000_000_000_000UL, 1_000_000_000_000_000_000UL,
            10_000_000_000_000_000_000UL,
        };

        public static string Format(long value)
        {
            return value < 0 ? "-" + Format(Magnitude(value)) : Format((ulong)value);
        }

        public static string Format(ulong value)
        {
            if (value < ExactBelow)
            {
                return FormatExact(value);
            }

            int digits = DigitCount(value);
            ulong mantissa = value / PowersOfTen[digits - 3];
            return FormatScaled((int)mantissa, digits);
        }

        /// <summary>The exact value with "," grouping, for inspection (focus, long-press, hover).</summary>
        public static string FormatExact(long value)
        {
            return value < 0 ? "-" + FormatExact(Magnitude(value)) : FormatExact((ulong)value);
        }

        public static string FormatExact(ulong value)
        {
            int digits = DigitCount(value);
            int length = digits + ((digits - 1) / 3);
            char[] buffer = new char[length];
            int position = length - 1;
            for (int i = 0; i < digits; i++)
            {
                if (i > 0 && i % 3 == 0)
                {
                    buffer[position--] = ',';
                }

                buffer[position--] = (char)('0' + (int)(value % 10));
                value /= 10;
            }

            return new string(buffer);
        }

        /// <summary>
        /// Formats a floored three-digit mantissa (100...999) of a value that has
        /// <paramref name="digitCount"/> decimal digits (at least 5). This is the display core; it also serves
        /// wider future numerics whose mantissa is extracted by other means.
        /// </summary>
        public static string FormatScaled(int mantissa, int digitCount)
        {
            if (mantissa < 100 || mantissa > 999)
            {
                throw new ArgumentOutOfRangeException(nameof(mantissa), "The mantissa must have exactly three digits.");
            }

            if (digitCount < 5)
            {
                throw new ArgumentOutOfRangeException(nameof(digitCount), "Use the exact form below 10,000.");
            }

            int tier = (digitCount - 1) / 3;
            char a = (char)('0' + (mantissa / 100));
            char b = (char)('0' + ((mantissa / 10) % 10));
            char c = (char)('0' + (mantissa % 10));
            if (tier < Suffixes.Length)
            {
                int integerDigits = digitCount - (3 * tier);
                switch (integerDigits)
                {
                    case 1:
                        return new string(new[] { a, '.', b, c }) + Suffixes[tier];
                    case 2:
                        return new string(new[] { a, b, '.', c }) + Suffixes[tier];
                    default:
                        return new string(new[] { a, b, c }) + Suffixes[tier];
                }
            }

            return new string(new[] { a, '.', b, c }) + "e" + (digitCount - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static ulong Magnitude(long negative)
        {
            return unchecked(0UL - (ulong)negative);
        }

        private static int DigitCount(ulong value)
        {
            int digits = 1;
            while (digits < PowersOfTen.Length && value >= PowersOfTen[digits])
            {
                digits++;
            }

            return digits;
        }
    }
}
