using System;
using System.Globalization;
using System.Threading;
using IdleFactory.Unity.UI;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class QuantityFormatterTests
    {
        [TestCase(0L, "0")]
        [TestCase(7L, "7")]
        [TestCase(999L, "999")]
        [TestCase(1_000L, "1,000")]
        [TestCase(1_234L, "1,234")]
        [TestCase(9_999L, "9,999")]
        public void ValuesBelowTenThousandAreExactWithGrouping(long value, string expected)
        {
            Assert.That(QuantityFormatter.Format(value), Is.EqualTo(expected));
        }

        [Test]
        public void TheBoundaryAtNineThousandNineHundredNinetyNineAndTenThousand()
        {
            Assert.That(QuantityFormatter.Format(9_999L), Is.EqualTo("9,999"));
            Assert.That(QuantityFormatter.Format(10_000L), Is.EqualTo("10.0K"));
            Assert.That(QuantityFormatter.Format(10_099L), Is.EqualTo("10.0K"));
            Assert.That(QuantityFormatter.Format(10_100L), Is.EqualTo("10.1K"));
        }

        [TestCase(12_345L, "12.3K")]
        [TestCase(99_999L, "99.9K")]
        [TestCase(100_000L, "100K")]
        [TestCase(999_999L, "999K")]
        [TestCase(1_000_000L, "1.00M")]
        [TestCase(1_999_999L, "1.99M")]
        [TestCase(999_999_999L, "999M")]
        [TestCase(1_000_000_000L, "1.00B")]
        [TestCase(12_345_678_901L, "12.3B")]
        [TestCase(1_000_000_000_000L, "1.00T")]
        [TestCase(999_999_999_999_999L, "999T")]
        [TestCase(1_000_000_000_000_000L, "1.00Qa")]
        [TestCase(123_456_789_012_345_678L, "123Qa")]
        [TestCase(999_999_999_999_999_999L, "999Qa")]
        [TestCase(1_000_000_000_000_000_000L, "1.00Qi")]
        [TestCase(long.MaxValue, "9.22Qi")]
        public void LargeValuesShowThreeSignificantDigitsFloored(long value, string expected)
        {
            Assert.That(QuantityFormatter.Format(value), Is.EqualTo(expected));
        }

        [Test]
        public void ScientificNotationBeyondTheLastSuffix()
        {
            Assert.That(QuantityFormatter.FormatScaled(123, 22), Is.EqualTo("1.23e21"));
            Assert.That(QuantityFormatter.FormatScaled(999, 30), Is.EqualTo("9.99e29"));
            Assert.That(QuantityFormatter.FormatScaled(100, 151), Is.EqualTo("1.00e150"));
            Assert.That(QuantityFormatter.Format(ulong.MaxValue), Is.EqualTo("18.4Qi"));
        }

        [Test]
        public void FormatScaledRejectsInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => QuantityFormatter.FormatScaled(99, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => QuantityFormatter.FormatScaled(1000, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => QuantityFormatter.FormatScaled(123, 4));
        }

        [Test]
        public void DisplayedValueNeverOverstatesTheExactValue()
        {
            long[] samples =
            {
                10_000, 10_999, 19_999, 99_999, 100_999, 999_999, 1_009_999, 1_999_999, 9_999_999,
                99_999_999, 100_000_001, 999_999_999, 9_999_999_999, 123_456_789_012L, 999_999_999_999_999_999L,
                long.MaxValue,
            };
            foreach (long value in samples)
            {
                decimal displayed = Parse(QuantityFormatter.Format(value));
                Assert.That(displayed <= value, Is.True, $"{value} displayed as {QuantityFormatter.Format(value)}");
                // Three significant digits: the shortfall is below 1% of the value.
                Assert.That((value - displayed) / value < 0.01m, Is.True, $"{value}");
            }
        }

        [Test]
        public void FormatIsMonotonicNonDecreasingAroundEveryBoundary()
        {
            decimal previous = -1;
            for (long value = 9_990; value < 10_200; value++)
            {
                decimal displayed = Parse(QuantityFormatter.Format(value));
                Assert.That(displayed >= previous, Is.True, $"{value}");
                previous = displayed;
            }
        }

        [Test]
        public void ExactStringGroupsEveryThreeDigits()
        {
            Assert.That(QuantityFormatter.FormatExact(0L), Is.EqualTo("0"));
            Assert.That(QuantityFormatter.FormatExact(999L), Is.EqualTo("999"));
            Assert.That(QuantityFormatter.FormatExact(1_000L), Is.EqualTo("1,000"));
            Assert.That(QuantityFormatter.FormatExact(12_345_678L), Is.EqualTo("12,345,678"));
            Assert.That(QuantityFormatter.FormatExact(1_000_000_000_000_000_000L), Is.EqualTo("1,000,000,000,000,000,000"));
            Assert.That(QuantityFormatter.FormatExact(long.MaxValue), Is.EqualTo("9,223,372,036,854,775,807"));
            Assert.That(QuantityFormatter.FormatExact(ulong.MaxValue), Is.EqualTo("18,446,744,073,709,551,615"));
        }

        [Test]
        public void NegativeValuesAreShownByMagnitudeWithAMinusSign()
        {
            Assert.That(QuantityFormatter.Format(-5L), Is.EqualTo("-5"));
            Assert.That(QuantityFormatter.Format(-12_345L), Is.EqualTo("-12.3K"));
            Assert.That(QuantityFormatter.FormatExact(-1_234L), Is.EqualTo("-1,234"));
            Assert.That(QuantityFormatter.FormatExact(long.MinValue), Is.EqualTo("-9,223,372,036,854,775,808"));
            Assert.That(QuantityFormatter.Format(long.MinValue), Is.EqualTo("-9.22Qi"));
        }

        [Test]
        public void OutputIsCultureInvariant()
        {
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Assert.That(QuantityFormatter.Format(1_234L), Is.EqualTo("1,234"));
                Assert.That(QuantityFormatter.Format(12_345L), Is.EqualTo("12.3K"));
                Assert.That(QuantityFormatter.FormatExact(1_234_567L), Is.EqualTo("1,234,567"));
                Assert.That(QuantityFormatter.FormatScaled(123, 22), Is.EqualTo("1.23e21"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        // Test-only inverse of the display: rebuilds the lower bound the string represents.
        private static decimal Parse(string text)
        {
            string[] suffixes = { "Qi", "Qa", "T", "B", "M", "K" };
            decimal[] scales = { 1e18m, 1e15m, 1e12m, 1e9m, 1e6m, 1e3m };
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (text.EndsWith(suffixes[i], StringComparison.Ordinal))
                {
                    string number = text.Substring(0, text.Length - suffixes[i].Length);
                    return decimal.Parse(number, CultureInfo.InvariantCulture) * scales[i];
                }
            }

            return decimal.Parse(text.Replace(",", string.Empty), CultureInfo.InvariantCulture);
        }
    }
}
