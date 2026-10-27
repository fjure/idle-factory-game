using System;
using System.Numerics;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Domain;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// T316 (F5-R04, FR-001 to FR-003, SC-003): <see cref="SalePricing"/> is <c>MulDivFloor(units x basePrice, salePricePpm, 10^6)</c> with a
    /// checked product. Pinned against a BigInteger oracle on seeded random operands and the bound edges, with the rounding rules that make a
    /// sale value neutral against the valuation of the same stock. The routing of auto-sell and Factory Value is covered by the run simulation
    /// equivalence case (q) and <c>FactoryValuationTests</c>.
    /// </summary>
    public sealed class SalePricingTests
    {
        private const long Ppm = 1_000_000L;
        private const long Bound = ExactBound.Value;

        private static long Value(long units, long basePrice, long ppm)
        {
            Assert.That(SalePricing.TryValue(units, basePrice, ppm, out long value), Is.True, units + " x " + basePrice + " @ " + ppm);
            return value;
        }

        [Test]
        public void NeutralMultiplierPricesAtTheBasePrice()
        {
            Assert.That(Value(7, 3, Ppm), Is.EqualTo(21));
            Assert.That(Value(1, 24, Ppm), Is.EqualTo(24));
            Assert.That(Value(1_000_000_000, 1_000_000_000, Ppm), Is.EqualTo(Bound));
        }

        [Test]
        public void ThePriceFloorsOncePerSale()
        {
            Assert.That(Value(3, 1, 1_250_000), Is.EqualTo(3), "3.75 floors to 3");
            Assert.That(Value(7, 1, 1_250_000), Is.EqualTo(8), "8.75 floors to 8");
            Assert.That(Value(1, 1, 999_999), Is.EqualTo(0), "a sub-unit price pays 0");
            Assert.That(Value(1_000_001, 1, 999_999), Is.EqualTo(999_999), "999,999.999999 floors to 999,999");
            Assert.That(Value(1, 3, 1_333_333), Is.EqualTo(3), "3.999999 floors to 3");
        }

        [Test]
        public void ZeroOperandsPayZero()
        {
            Assert.That(Value(0, 5, Ppm), Is.EqualTo(0));
            Assert.That(Value(5, 0, Ppm), Is.EqualTo(0));
            Assert.That(Value(5, 5, 0), Is.EqualTo(0));
        }

        [Test]
        public void ARejectedPriceLeavesZeroAndReportsFalse()
        {
            long value = 123;
            Assert.That(SalePricing.TryValue(-1, 1, Ppm, out value), Is.False);
            Assert.That(value, Is.EqualTo(0));
            Assert.That(SalePricing.TryValue(1, -1, Ppm, out value), Is.False);
            Assert.That(SalePricing.TryValue(1, 1, -1, out value), Is.False);
            Assert.That(SalePricing.TryValue(Bound + 1, 1, Ppm, out value), Is.False, "units above the exact bound");
            Assert.That(SalePricing.TryValue(1, Bound + 1, Ppm, out value), Is.False, "price above the exact bound");
            Assert.That(value, Is.EqualTo(0));
        }

        [Test]
        public void TheGrossProductAndTheResultAreCheckedAgainstTenToTheEighteen()
        {
            Assert.That(SalePricing.TryValue(1_000_000_001, 1_000_000_000, Ppm, out long value), Is.False, "gross 10^18 + 10^9");
            Assert.That(value, Is.EqualTo(0));
            Assert.That(SalePricing.TryValue(Bound, 1, 2 * Ppm, out value), Is.False, "result 2 x 10^18");
            Assert.That(value, Is.EqualTo(0));
            Assert.That(Value(Bound, 1, Ppm), Is.EqualTo(Bound));
            Assert.That(Value(Bound / 2, 1, 2 * Ppm), Is.EqualTo(Bound), "10^18 exactly");
            Assert.That(SalePricing.TryValue(Bound / 2 + 1, 1, 2 * Ppm, out value), Is.False, "10^18 + 2");
        }

        [Test]
        public void MatchesABigIntegerOracleOnSeededRandomOperands()
        {
            var rng = new Random(316);
            int accepted = 0, rejected = 0;
            for (int i = 0; i < 20_000; i++)
            {
                long units = Operand(rng);
                long basePrice = Operand(rng);
                long ppm = rng.Next(4) == 0 ? rng.Next(0, 5_000_000) : Ppm + rng.Next(-300_000, 600_000);
                BigInteger gross = (BigInteger)units * basePrice;
                BigInteger expected = gross * ppm / Ppm;
                bool fits = gross <= Bound && expected <= Bound;
                bool ok = SalePricing.TryValue(units, basePrice, ppm, out long value);
                Assert.That(ok, Is.EqualTo(fits), units + " x " + basePrice + " @ " + ppm);
                Assert.That(value, Is.EqualTo(fits ? (long)expected : 0), units + " x " + basePrice + " @ " + ppm);
                if (ok) accepted++; else rejected++;
            }
            Assert.That(accepted, Is.GreaterThan(5_000));
            Assert.That(rejected, Is.GreaterThan(1_000));
        }

        private static long Operand(Random rng)
        {
            switch (rng.Next(6))
            {
                case 0: return rng.Next(0, 10);
                case 1: return rng.Next(0, 100_000);
                case 2: return (long)rng.Next(0, int.MaxValue) * 1000;
                case 3: return Bound / (1 + rng.Next(0, 3_000_000));
                case 4: return Bound - rng.Next(0, 3);
                default: return rng.Next(0, 1_000_000_000);
            }
        }

        [Test]
        public void SplitSalesNeverPayMoreThanOneSaleAndLoseLessThanOnePerSale()
        {
            // FR-002: the value of the whole stock is one floor; selling it in k lots floors k times. The valuation is therefore an upper bound of
            // any sequence of sales, within k - 1 of it (so full-stock sales are exactly neutral and no split sale raises Factory Value).
            var rng = new Random(2);
            for (int round = 0; round < 2000; round++)
            {
                long basePrice = rng.Next(1, 40);
                long ppm = Ppm + rng.Next(-400_000, 900_000);
                int lots = rng.Next(1, 8);
                long total = 0, paid = 0;
                for (int k = 0; k < lots; k++)
                {
                    long units = rng.Next(0, 50);
                    total += units;
                    paid += Value(units, basePrice, ppm);
                }
                long whole = Value(total, basePrice, ppm);
                Assert.That(paid, Is.LessThanOrEqualTo(whole));
                Assert.That(paid, Is.GreaterThanOrEqualTo(whole - (lots - 1)));
            }
        }
    }
}
