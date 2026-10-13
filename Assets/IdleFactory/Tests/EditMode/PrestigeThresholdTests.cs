using System;
using System.Numerics;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The rising prestige threshold (T331, D-K, F5-R07, ECO-7): the oracle of <c>acc = base x 10^6</c>, per step
    /// <c>floor(acc x growth / 10^6)</c>, final <c>ceil(acc / 10^6)</c> over BigInteger, the clamp at <c>MaxIndex</c>, the legacy
    /// (no growth) behaviour, and the preview and confirmation that read <c>For(min(PrestigeCount, MaxIndex))</c>. A retry or an interrupted
    /// promotion advances <c>PrestigeCount</c>, and so the index, once: the durable sweeps of <c>PrestigeTransitionTests</c> assert the count
    /// after every failing step; here the in-memory transitions (confirm, second confirm, cancel, promote) are checked against the index.
    /// </summary>
    public sealed class PrestigeThresholdTests
    {
        private static BigInteger Oracle(long baseValue, long growthPpm, long index)
        {
            BigInteger acc = new BigInteger(baseValue) * 1_000_000;
            for (long i = 0; i < index; i++) acc = acc * growthPpm / 1_000_000;
            return (acc + 999_999) / 1_000_000;
        }

        private static readonly long[] Bases = { 1, 7, 999_999, 10_000_000, 123_456_789, 1_000_000_000_000L };
        private static readonly long[] Growths = { 1_000_000, 1_000_001, 1_050_000, 1_500_000, 2_000_000, 3_141_593, 10_000_000 };

        [Test]
        public void TryFor_MatchesTheBigIntegerOracleOverBaseGrowthAndIndex()
        {
            int compared = 0;
            foreach (long b in Bases)
            {
                foreach (long g in Growths)
                {
                    for (long index = 0; index <= 70; index++)
                    {
                        BigInteger expected = Oracle(b, g, index);
                        bool ok = PrestigeThreshold.TryFor(new ThresholdDef(b, g, 100), index, out long actual);
                        if (expected > ExactBound.Value)
                        {
                            Assert.That(ok, Is.False, $"base {b} growth {g} index {index} exceeds 10^18");
                            Assert.That(actual, Is.EqualTo(0));
                            break;
                        }

                        Assert.That(ok, Is.True, $"base {b} growth {g} index {index}");
                        Assert.That(actual, Is.EqualTo((long)expected), $"base {b} growth {g} index {index}");
                        compared++;
                    }
                }
            }

            Assert.That(compared, Is.GreaterThan(500));
        }

        [Test]
        public void TryFor_PinnedValuesOfTheCeilingAfterIteratedFloors()
        {
            // 10,000,000 x 1.05^n: the last value is 12,155,062.5 exactly, so the final ceiling gives 12,155,063.
            long[] expected = { 10_000_000, 10_500_000, 11_025_000, 11_576_250, 12_155_063 };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(PrestigeThreshold.TryFor(new ThresholdDef(10_000_000, 1_050_000, 10), i, out long t), Is.True);
                Assert.That(t, Is.EqualTo(expected[i]), "index " + i);
                Assert.That(t, Is.EqualTo((long)Oracle(10_000_000, 1_050_000, i)), "oracle index " + i);
            }

            // 1 x 1.5^n: the exact values 1.5, 2.25, 3.375 give 2, 3, 4 with one final ceiling.
            Assert.That(PrestigeThreshold.TryFor(new ThresholdDef(1, 1_500_000, 10), 1, out long a), Is.True);
            Assert.That(a, Is.EqualTo(2));
            Assert.That(PrestigeThreshold.TryFor(new ThresholdDef(1, 1_500_000, 10), 2, out long b), Is.True);
            Assert.That(b, Is.EqualTo(3));
            Assert.That(PrestigeThreshold.TryFor(new ThresholdDef(1, 1_500_000, 10), 3, out long c), Is.True);
            Assert.That(c, Is.EqualTo(4));
        }

        [Test]
        public void TryFor_RejectsAnInvalidDefinitionOrIndexAndAnOverflow()
        {
            Assert.That(PrestigeThreshold.TryFor(null, 0, out _), Is.False);
            Assert.That(PrestigeThreshold.TryFor(new ThresholdDef(0, 1_000_000, 1), 0, out _), Is.False, "base below 1");
            Assert.That(PrestigeThreshold.TryFor(new ThresholdDef(5, 999_999, 1), 0, out _), Is.False, "growth below 10^6");
            Assert.That(PrestigeThreshold.TryFor(new ThresholdDef(5, 1_000_000, 1), -1, out _), Is.False, "negative index");
            Assert.That(PrestigeThreshold.TryFor(new ThresholdDef(ExactBound.Value, 1_000_001, 5), 5, out _), Is.False, "above 10^18");
            Assert.That(PrestigeThreshold.TryFor(new ThresholdDef(ExactBound.Value, 1_000_000, 5), 5, out long top), Is.True);
            Assert.That(top, Is.EqualTo(ExactBound.Value));
        }

        [Test]
        public void TheFastPathsStayExactOnLongSlowGrowthWalks()
        {
            // Growth 1,000,001 takes the small-excess path at every quotient; 1,000,000,000,000 the 64-bit path; both must equal the oracle.
            foreach (long growth in new long[] { 1_000_001, 1_000_007, 1_300_000, 1_000_000_000_000L })
            {
                BigInteger acc = new BigInteger(3) * 1_000_000;
                for (long index = 0; index <= 3_000; index++)
                {
                    BigInteger expected = (acc + 999_999) / 1_000_000;
                    bool ok = PrestigeThreshold.TryFor(new ThresholdDef(3, growth, 10_000), index, out long actual);
                    if (expected > ExactBound.Value)
                    {
                        Assert.That(ok, Is.False, $"growth {growth} index {index}");
                        break;
                    }

                    if (index % 97 == 0 || index < 40) Assert.That(actual, Is.EqualTo((long)expected), $"growth {growth} index {index}");
                    acc = acc * growth / 1_000_000;
                }
            }
        }

        [Test]
        public void TheSlowGrowthWorstCaseStopsAtTheFirstOverflowQuickly()
        {
            // Growth 1,000,001 from base 1 needs about 4 x 10^7 steps to pass 10^18; MaxIndex 10^18 must not be walked to the end (F2).
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool ok = PrestigeThreshold.TryFor(new ThresholdDef(1, 1_000_001, ExactBound.Value), ExactBound.Value, out long value);
            sw.Stop();
            Assert.That(ok, Is.False);
            Assert.That(value, Is.EqualTo(0));
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(5_000), "bounded by the first overflow, not by the index");
        }

        [Test]
        public void ConstantGrowthNeedsNoWalk()
        {
            var def = new ThresholdDef(42, 1_000_000, ExactBound.Value);
            Assert.That(PrestigeThreshold.TryFor(def, ExactBound.Value, out long t), Is.True);
            Assert.That(t, Is.EqualTo(42));
        }

        [Test]
        public void TheIndexIsTheCountClampedAtMaxIndexAndLegacyContentHasNoGrowth()
        {
            var grown = new PrestigeDef(10_000_000, "sqrt", 10, 10_000_000, new ThresholdDef(10_000_000, 1_500_000, 3));
            Assert.That(PrestigeThreshold.Index(grown, 0), Is.EqualTo(0));
            Assert.That(PrestigeThreshold.Index(grown, 2), Is.EqualTo(2));
            Assert.That(PrestigeThreshold.Index(grown, 3), Is.EqualTo(3));
            Assert.That(PrestigeThreshold.Index(grown, 4), Is.EqualTo(3));
            Assert.That(PrestigeThreshold.Index(grown, 1_000_000), Is.EqualTo(3));

            long[] expected = { 10_000_000, 15_000_000, 22_500_000, 33_750_000, 33_750_000, 33_750_000 };
            for (int count = 0; count < expected.Length; count++)
            {
                Assert.That(PrestigeThreshold.TryForCount(grown, count, out long t), Is.True);
                Assert.That(t, Is.EqualTo(expected[count]), "count " + count);
                Assert.That(PrestigeThreshold.TryNextForCount(grown, count, out long next), Is.True);
                Assert.That(next, Is.EqualTo(expected[Math.Min(count + 1, expected.Length - 1)]), "next of count " + count);
            }

            var legacy = new PrestigeDef(10_000_000, "sqrt", 10, 10_000_000);
            Assert.That(PrestigeThreshold.Index(legacy, 9), Is.EqualTo(0));
            Assert.That(PrestigeThreshold.TryForCount(legacy, 9, out long l), Is.True);
            Assert.That(l, Is.EqualTo(10_000_000));
            Assert.That(PrestigeThreshold.TryNextForCount(legacy, 9, out long ln), Is.True);
            Assert.That(ln, Is.EqualTo(10_000_000));
        }

        // ---- preview and confirm ---------------------------------------------------------------------------

        private static RunCommandFixtures.Options GrowingOptions() => new RunCommandFixtures.Options
        {
            Prestige = new PrestigeDef(10_000_000, "sqrt", 10, 10_000_000, new ThresholdDef(10_000_000, 1_500_000, 3)),
        };

        private static RunCommands CommandsAt(long cash, long prestigeCount, out RunSession session)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(GrowingOptions(), cash);
            session = new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog, 0, prestigeCount), RunCommandFixtures.StartUtcMs);
            session.Advance(0);
            int next = 1;
            return new RunCommands(session, () => RunSimHarness.Id(next++));
        }

        [TestCase(0L, 10_000_000L, 15_000_000L, 0L)]
        [TestCase(1L, 15_000_000L, 22_500_000L, 1L)]
        [TestCase(2L, 22_500_000L, 33_750_000L, 2L)]
        [TestCase(3L, 33_750_000L, 33_750_000L, 3L)]
        [TestCase(7L, 33_750_000L, 33_750_000L, 3L)]
        public void PreviewReportsTheThresholdTheNextThresholdAndTheClampedIndex(long count, long threshold, long next, long index)
        {
            RunCommands commands = CommandsAt(1_000, count, out _);
            RunCommandResult<PrestigePreview> r = commands.GetPrestigePreview();
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            Assert.That(r.Value.Threshold, Is.EqualTo(threshold));
            Assert.That(r.Value.ThresholdFactoryValue, Is.EqualTo(threshold));
            Assert.That(r.Value.NextThreshold, Is.EqualTo(next));
            Assert.That(r.Value.PrestigeIndex, Is.EqualTo(index));
            Assert.That(r.Value.IsEligible, Is.False);
            Assert.That(r.Value.Reason, Is.EqualTo(RunReasons.PrestigeBelowThreshold));
            Assert.That(r.Value.MissingFactoryValue, Is.EqualTo(threshold - 1_000));
        }

        [Test]
        public void ConfirmReadsTheRisingThresholdAtTheBoundary()
        {
            RunCommands below = CommandsAt(14_999_999, 1, out _);
            RunCommandResult<PrestigeConfirmation> rejected = below.ConfirmPrestige();
            Assert.That(rejected.IsSuccess, Is.False);
            Assert.That(rejected.ReasonCode, Is.EqualTo(RunReasons.PrestigeBelowThreshold));
            Assert.That(below.GetPrestigePreview().Value.MissingFactoryValue, Is.EqualTo(1));

            RunCommands at = CommandsAt(15_000_000, 1, out _);
            Assert.That(at.GetPrestigePreview().Value.IsEligible, Is.True);
            Assert.That(at.ConfirmPrestige().IsSuccess, Is.True);

            RunCommands first = CommandsAt(10_000_000, 0, out _);
            Assert.That(first.ConfirmPrestige().IsSuccess, Is.True);
        }

        [Test]
        public void AConfirmationACancellationAndASecondConfirmationDoNotMoveTheIndexAndThePromotionMovesItOnce()
        {
            RunCommands commands = CommandsAt(20_000_000, 1, out RunSession session);
            Assert.That(commands.GetPrestigePreview().Value.PrestigeIndex, Is.EqualTo(1));

            Assert.That(commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(session.Permanent.PrestigeCount, Is.EqualTo(1), "confirming does not touch the count");
            RunCommandResult<PrestigeConfirmation> second = commands.ConfirmPrestige();
            Assert.That(second.ReasonCode, Is.EqualTo(RunReasons.TransitionPending));
            Assert.That(session.Permanent.PrestigeCount, Is.EqualTo(1));

            Assert.That(commands.CancelPendingTransition().Value, Is.True);
            Assert.That(session.Permanent.PrestigeCount, Is.EqualTo(1), "cancelling keeps the index");
            Assert.That(commands.GetPrestigePreview().Value.Threshold, Is.EqualTo(15_000_000));

            Assert.That(commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(commands.PromotePrestige().IsSuccess, Is.True);
            Assert.That(session.Permanent.PrestigeCount, Is.EqualTo(2), "one promotion, one step");
            Assert.That(commands.PromotePrestige().IsSuccess, Is.False, "nothing is pending: no second step");
            Assert.That(session.Permanent.PrestigeCount, Is.EqualTo(2));
            PrestigePreview after = commands.GetPrestigePreview().Value;
            Assert.That(after.PrestigeIndex, Is.EqualTo(2));
            Assert.That(after.Threshold, Is.EqualTo(22_500_000));
            Assert.That(after.NextThreshold, Is.EqualTo(33_750_000));
        }
    }
}
