using System;
using IdleFactory.Core.Numerics;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class CheckedMathTests
    {
        private const long Bound = ExactBound.Value;

        [Test]
        public void ExactBoundIsTenToTheEighteenth()
        {
            Assert.That(ExactBound.Value, Is.EqualTo(1_000_000_000_000_000_000L));
            Assert.That(ExactBound.Contains(0), Is.True);
            Assert.That(ExactBound.Contains(Bound), Is.True);
            Assert.That(ExactBound.Contains(Bound + 1), Is.False);
            Assert.That(ExactBound.Contains(-1), Is.False);
            Assert.That(ExactBound.Contains(long.MaxValue), Is.False);
        }

        [Test]
        public void AddAcceptsTheClosedRangeAndRejectsAboveTheBound()
        {
            Assert.That(CheckedMath.TryAdd(0, 0, out long zero), Is.True);
            Assert.That(zero, Is.EqualTo(0));
            Assert.That(CheckedMath.TryAdd(Bound - 1, 1, out long atBound), Is.True);
            Assert.That(atBound, Is.EqualTo(Bound));
            Assert.That(CheckedMath.TryAdd(Bound, 0, out long same), Is.True);
            Assert.That(same, Is.EqualTo(Bound));

            Assert.That(CheckedMath.TryAdd(Bound, 1, out long over), Is.False);
            Assert.That(over, Is.EqualTo(0));
            Assert.That(CheckedMath.TryAdd(Bound, Bound, out _), Is.False);
        }

        [Test]
        public void AddRejectsOutOfRangeOperandsIncludingInt64Extremes()
        {
            Assert.That(CheckedMath.TryAdd(-1, 5, out long r1), Is.False);
            Assert.That(r1, Is.EqualTo(0));
            Assert.That(CheckedMath.TryAdd(long.MaxValue, 1, out _), Is.False);
            Assert.That(CheckedMath.TryAdd(long.MaxValue, long.MaxValue, out _), Is.False);
            Assert.That(CheckedMath.TryAdd(long.MinValue, 0, out _), Is.False);
        }

        [Test]
        public void MultiplyEdges()
        {
            Assert.That(CheckedMath.TryMultiply(0, Bound, out long zero), Is.True);
            Assert.That(zero, Is.EqualTo(0));
            Assert.That(CheckedMath.TryMultiply(Bound, 1, out long one), Is.True);
            Assert.That(one, Is.EqualTo(Bound));
            Assert.That(CheckedMath.TryMultiply(1_000_000_000L, 1_000_000_000L, out long exact), Is.True);
            Assert.That(exact, Is.EqualTo(Bound));

            Assert.That(CheckedMath.TryMultiply(1_000_000_001L, 1_000_000_000L, out long over), Is.False);
            Assert.That(over, Is.EqualTo(0));
            Assert.That(CheckedMath.TryMultiply(Bound, Bound, out _), Is.False);
            Assert.That(CheckedMath.TryMultiply(long.MaxValue, 1, out _), Is.False);
            Assert.That(CheckedMath.TryMultiply(-1, 1, out _), Is.False);
            // A product that wraps 64 bits to a small value must still be rejected (high half nonzero).
            Assert.That(CheckedMath.TryMultiply(1L << 32, 1L << 32, out _), Is.False);
        }

        [Test]
        public void SubtractRejectsNegativeResults()
        {
            Assert.That(CheckedMath.TrySubtract(5, 5, out long zero), Is.True);
            Assert.That(zero, Is.EqualTo(0));
            Assert.That(CheckedMath.TrySubtract(Bound, 1, out long r), Is.True);
            Assert.That(r, Is.EqualTo(Bound - 1));
            Assert.That(CheckedMath.TrySubtract(4, 5, out long neg), Is.False);
            Assert.That(neg, Is.EqualTo(0));
        }

        [Test]
        public void SumIsAtomicAtTheBound()
        {
            Assert.That(CheckedMath.TrySum(ReadOnlySpan<long>.Empty, out long empty), Is.True);
            Assert.That(empty, Is.EqualTo(0));

            long[] fits = { Bound / 2, Bound / 4, Bound / 4 };
            Assert.That(CheckedMath.TrySum(fits, out long total), Is.True);
            Assert.That(total, Is.EqualTo(Bound));

            long[] overflows = { Bound / 2, Bound / 2, 1 };
            Assert.That(CheckedMath.TrySum(overflows, out long rejected), Is.False);
            Assert.That(rejected, Is.EqualTo(0));

            long[] bad = { 1, -1 };
            Assert.That(CheckedMath.TrySum(bad, out _), Is.False);
        }

        [Test]
        public void Multiply128ProducesFullProducts()
        {
            MulDiv.Multiply128(ulong.MaxValue, ulong.MaxValue, out ulong high, out ulong low);
            // (2^64 - 1)^2 = 2^128 - 2^65 + 1
            Assert.That(high, Is.EqualTo(0xFFFFFFFFFFFFFFFEUL));
            Assert.That(low, Is.EqualTo(1UL));

            MulDiv.Multiply128(1UL << 63, 2, out high, out low);
            Assert.That(high, Is.EqualTo(1UL));
            Assert.That(low, Is.EqualTo(0UL));

            MulDiv.Multiply128((ulong)Bound, (ulong)Bound, out high, out low);
            MulDiv.TryMulDivFloor((ulong)Bound, (ulong)Bound, (ulong)Bound, out ulong back);
            Assert.That(back, Is.EqualTo((ulong)Bound));
            Assert.That(high, Is.EqualTo(54210108624275221UL)); // floor(10^36 / 2^64)
        }

        [Test]
        public void MulDivFloorAndCeilWithExactAndInexactResults()
        {
            Assert.That(MulDiv.MulDivFloor(7, 3, 2, out long f), Is.True);
            Assert.That(f, Is.EqualTo(10));
            Assert.That(MulDiv.MulDivCeil(7, 3, 2, out long c), Is.True);
            Assert.That(c, Is.EqualTo(11));

            Assert.That(MulDiv.MulDivFloor(8, 3, 2, out f), Is.True);
            Assert.That(MulDiv.MulDivCeil(8, 3, 2, out c), Is.True);
            Assert.That(f, Is.EqualTo(12));
            Assert.That(c, Is.EqualTo(12));

            Assert.That(MulDiv.MulDivFloor(0, Bound, 3, out f), Is.True);
            Assert.That(f, Is.EqualTo(0));
            Assert.That(MulDiv.MulDivCeil(1, 1, 1_000_000, out c), Is.True);
            Assert.That(c, Is.EqualTo(1));
            Assert.That(MulDiv.MulDivFloor(1, 1, 1_000_000, out f), Is.True);
            Assert.That(f, Is.EqualTo(0));
        }

        [Test]
        public void MulDivUsesA128BitIntermediate()
        {
            // 10^18 * 10^18 / 10^18 needs a 120-bit product.
            Assert.That(MulDiv.MulDivFloor(Bound, Bound, Bound, out long r), Is.True);
            Assert.That(r, Is.EqualTo(Bound));

            // ppm composition: 10^18 * 999_999 / 10^6, exact.
            Assert.That(MulDiv.MulDivFloor(Bound, 999_999, 1_000_000, out r), Is.True);
            Assert.That(r, Is.EqualTo(999_999_000_000_000_000L));

            // Exact quotient just below the bound.
            Assert.That(MulDiv.MulDivFloor(Bound - 1, Bound, Bound, out r), Is.True);
            Assert.That(r, Is.EqualTo(Bound - 1));

            // Product 10^18 * 3 / 7 = 428571428571428571.43
            Assert.That(MulDiv.MulDivFloor(Bound, 3, 7, out r), Is.True);
            Assert.That(r, Is.EqualTo(428_571_428_571_428_571L));
            Assert.That(MulDiv.MulDivCeil(Bound, 3, 7, out r), Is.True);
            Assert.That(r, Is.EqualTo(428_571_428_571_428_572L));

            // A quotient with a nonzero 128-bit high half: (2^62 * 2^62) / 2^61 = 2^63.
            Assert.That(MulDiv.TryMulDivFloor(1UL << 62, 1UL << 62, 1UL << 61, out ulong q), Is.True);
            Assert.That(q, Is.EqualTo(1UL << 63));
            Assert.That(MulDiv.TryMulDivCeil(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, out q), Is.True);
            Assert.That(q, Is.EqualTo(ulong.MaxValue));
            Assert.That(MulDiv.TryMulDivFloor(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue - 1, out q), Is.False);
        }

        [Test]
        public void MulDivMatchesReferenceOnPseudoRandomInputs()
        {
            var state = 0x9E3779B97F4A7C15UL;
            for (int i = 0; i < 2000; i++)
            {
                ulong a = Next(ref state) >> (int)(Next(ref state) % 40);
                ulong b = Next(ref state) >> (int)(Next(ref state) % 40);
                ulong d = (Next(ref state) >> (int)(Next(ref state) % 60)) | 1UL;

                bool ok = MulDiv.TryMulDivFloor(a, b, d, out ulong floor);
                bool okCeil = MulDiv.TryMulDivCeil(a, b, d, out ulong ceil);

                var product = new System.Numerics.BigInteger(a) * b;
                var expectedFloor = product / d;
                bool fits = expectedFloor <= ulong.MaxValue;
                Assert.That(ok, Is.EqualTo(fits), $"{a} * {b} / {d}");
                if (fits)
                {
                    Assert.That(floor, Is.EqualTo((ulong)expectedFloor), $"floor {a} * {b} / {d}");
                    var expectedCeil = (product + d - 1) / d;
                    Assert.That(okCeil, Is.EqualTo(expectedCeil <= ulong.MaxValue));
                    if (okCeil)
                    {
                        Assert.That(ceil, Is.EqualTo((ulong)expectedCeil), $"ceil {a} * {b} / {d}");
                    }
                }
            }
        }

        [Test]
        public void MulDivRejectsBadInputsAndOverBoundResults()
        {
            Assert.That(MulDiv.MulDivFloor(1, 1, 0, out long r), Is.False);
            Assert.That(r, Is.EqualTo(0));
            Assert.That(MulDiv.MulDivFloor(1, 1, -5, out _), Is.False);
            Assert.That(MulDiv.MulDivFloor(-1, 1, 1, out _), Is.False);
            Assert.That(MulDiv.MulDivCeil(1, -1, 1, out _), Is.False);
            Assert.That(MulDiv.TryMulDivFloor(1, 1, 0, out _), Is.False);

            Assert.That(MulDiv.MulDivFloor(Bound, 2, 1, out r), Is.False);
            Assert.That(r, Is.EqualTo(0));
            Assert.That(MulDiv.MulDivFloor(Bound, 1, 1, out r), Is.True);
            Assert.That(r, Is.EqualTo(Bound));
            Assert.That(MulDiv.MulDivCeil(Bound, 1, 1, out r), Is.True);
            // Floor fits at exactly the bound, ceil of Bound + a fraction does not.
            Assert.That(MulDiv.MulDivFloor(Bound + 1, 1, 1, out r), Is.False);
            Assert.That(MulDiv.MulDivFloor(long.MaxValue, long.MaxValue, 1, out _), Is.False);
            Assert.That(MulDiv.MulDivFloor(long.MaxValue, long.MaxValue, long.MaxValue, out r), Is.False);
        }

        [Test]
        public void CeilNeverBelowFloorAndDiffersByAtMostOne()
        {
            for (long a = 0; a < 40; a++)
            {
                for (long d = 1; d < 12; d++)
                {
                    Assert.That(MulDiv.MulDivFloor(a, 5, d, out long f), Is.True);
                    Assert.That(MulDiv.MulDivCeil(a, 5, d, out long c), Is.True);
                    Assert.That(c - f, Is.EqualTo((a * 5) % d == 0 ? 0 : 1));
                }
            }
        }

        [Test]
        public void ISqrtPerfectSquaresAndNeighbours()
        {
            Assert.That(IntegerSqrt.ISqrt(0), Is.EqualTo(0UL));
            Assert.That(IntegerSqrt.ISqrt(1), Is.EqualTo(1UL));
            Assert.That(IntegerSqrt.ISqrt(2), Is.EqualTo(1UL));
            Assert.That(IntegerSqrt.ISqrt(3), Is.EqualTo(1UL));
            Assert.That(IntegerSqrt.ISqrt(4), Is.EqualTo(2UL));

            ulong[] roots =
            {
                2, 3, 10, 255, 256, 65_535, 65_536, 1_000_000, 999_999_999, 1_000_000_000,
                4_294_967_295UL,
            };
            foreach (ulong r in roots)
            {
                ulong square = r * r;
                Assert.That(IntegerSqrt.ISqrt(square), Is.EqualTo(r), $"sqrt({square})");
                Assert.That(IntegerSqrt.ISqrt(square - 1), Is.EqualTo(r - 1), $"sqrt({square}-1)");
                if (r != 4_294_967_295UL)
                {
                    Assert.That(IntegerSqrt.ISqrt(square + 1), Is.EqualTo(r), $"sqrt({square}+1)");
                }
            }

            Assert.That(IntegerSqrt.ISqrt(ulong.MaxValue), Is.EqualTo(4_294_967_295UL));
            Assert.That(IntegerSqrt.ISqrt((ulong)Bound), Is.EqualTo(1_000_000_000UL));
            Assert.That(IntegerSqrt.ISqrt((ulong)Bound - 1), Is.EqualTo(999_999_999UL));
        }

        [Test]
        public void ISqrtSatisfiesTheDefinitionOnManyValues()
        {
            var state = 0x1234567890ABCDEFUL;
            for (int i = 0; i < 3000; i++)
            {
                ulong n = Next(ref state) >> (int)(Next(ref state) % 64);
                ulong r = IntegerSqrt.ISqrt(n);
                Assert.That(r * r <= n, Is.True, $"n={n}");
                if (r < 4_294_967_295UL)
                {
                    Assert.That((r + 1) * (r + 1) > n, Is.True, $"n={n}");
                }
            }
        }

        [Test]
        public void CheckedOperationsDoNotAllocate()
        {
            long sink = 0;
            long[] values = { 10, 20, 30, 40 };

            // Warm the code paths so JIT work is not counted.
            Exercise(values, ref sink);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++)
            {
                Exercise(values, ref sink);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0), "checked math, MulDiv and ISqrt must not allocate");
            Assert.That(sink, Is.Not.EqualTo(0));
        }

        private static void Exercise(long[] values, ref long sink)
        {
            CheckedMath.TryAdd(Bound / 2, 12345, out long a);
            CheckedMath.TryMultiply(1_000_000L, 999_999L, out long m);
            CheckedMath.TrySum(values, out long s);
            CheckedMath.TrySubtract(Bound, 7, out long d);
            MulDiv.MulDivFloor(Bound, 999_999, 1_000_000, out long f);
            MulDiv.MulDivCeil(Bound, 3, 7, out long c);
            ulong q = IntegerSqrt.ISqrt(9_999_999_999_999UL);
            sink += a + m + s + d + f + c + (long)q;
        }

        private static ulong Next(ref ulong state)
        {
            // xorshift64*: deterministic, allocation-free.
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            return state * 2685821657736338717UL;
        }
    }
}
