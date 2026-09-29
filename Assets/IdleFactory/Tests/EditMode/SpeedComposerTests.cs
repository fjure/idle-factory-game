using System;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class SpeedComposerTests
    {
        private const long Ppm = 1_000_000;

        private static MachineDef FlatMachineWithMilestones()
        {
            return new MachineDef("machine.flat", "Flat", "P", "recipe.flat", 100, new Curve(10, 1_000_000, "ceil"),
                1_000_000,
                new[]
                {
                    new SpeedMilestone(10, 2_000_000), new SpeedMilestone(25, 2_000_000),
                    new SpeedMilestone(50, 3_000_000), new SpeedMilestone(100, 5_000_000),
                },
                new UnlockCondition("always"));
        }

        [Test]
        public void MilestonesMultiplyByTwoTwoThreeFiveAtTheirLevels()
        {
            var machine = FlatMachineWithMilestones();
            long Speed(int level)
            {
                Assert.That(SpeedComposer.TryLevelSpeed(machine, level, out long s), Is.True);
                return s;
            }

            Assert.That(Speed(1), Is.EqualTo(Ppm));
            Assert.That(Speed(9), Is.EqualTo(Ppm));
            Assert.That(Speed(10), Is.EqualTo(2 * Ppm));
            Assert.That(Speed(24), Is.EqualTo(2 * Ppm));
            Assert.That(Speed(25), Is.EqualTo(4 * Ppm));
            Assert.That(Speed(49), Is.EqualTo(4 * Ppm));
            Assert.That(Speed(50), Is.EqualTo(12 * Ppm));
            Assert.That(Speed(99), Is.EqualTo(12 * Ppm));
            Assert.That(Speed(100), Is.EqualTo(60 * Ppm));
            Assert.That(SpeedComposer.TryLevelSpeed(machine, 0, out long none), Is.False);
            Assert.That(none, Is.EqualTo(0));
            Assert.That(SpeedComposer.TryLevelSpeed(machine, 101, out _), Is.False);
        }

        [Test]
        public void LevelSpeedCombinesGrowthFloorAndMilestones()
        {
            var machine = new MachineDef("machine.grow", "Grow", "P", "recipe.grow", 3, new Curve(10, 1_000_000, "ceil"),
                1_080_000, new[] { new SpeedMilestone(3, 2_000_000) }, new UnlockCondition("always"));
            SpeedComposer.TryLevelSpeed(machine, 2, out long l2);
            SpeedComposer.TryLevelSpeed(machine, 3, out long l3);
            Assert.That(l2, Is.EqualTo(1_080_000));
            Assert.That(l3, Is.EqualTo(2 * 1_166_400));
        }

        [Test]
        public void ComposeFoldsInFixedOrderWithFloorAtEachStep()
        {
            // 1_000_003 x1.5 (research) = 1_500_004 (floor), x1.5 (industry), x1.5 (item), x2 (Overdrive).
            var research = new[] { new SpeedFactor(1_500_000, 1) };
            var industry = new[] { new SpeedFactor(1_500_000, 1) };
            var items = new long[] { 1_500_000 };
            Assert.That(SpeedComposer.TryCompose(1_000_003, research, industry, items, 2_000_000, 100_000_000, out long speed), Is.True);
            Assert.That(speed, Is.EqualTo(6_750_018));

            Assert.That(SpeedComposer.TryCompose(1_000_003, research, null, null, Ppm, 100_000_000, out long r), Is.True);
            Assert.That(r, Is.EqualTo(1_500_004));
        }

        [Test]
        public void LevelEffectsApplyOncePerLevel()
        {
            var research = new[] { new SpeedFactor(1_100_000, 2) };
            Assert.That(SpeedComposer.TryCompose(1_000_001, research, null, null, Ppm, 100_000_000, out long speed), Is.True);
            Assert.That(speed, Is.EqualTo(1_210_001));
            var none = new[] { new SpeedFactor(1_100_000, 0) };
            Assert.That(SpeedComposer.TryCompose(1_000_001, none, null, null, Ppm, 100_000_000, out long same), Is.True);
            Assert.That(same, Is.EqualTo(1_000_001));
        }

        [Test]
        public void ItemFactorsFoldInTheGivenOrderSoTheItemIdOrderMatters()
        {
            var ab = new long[] { 1_600_000, 1_300_000 };
            var ba = new long[] { 1_300_000, 1_600_000 };
            SpeedComposer.TryCompose(3, null, null, ab, Ppm, 100_000_000, out long first);
            SpeedComposer.TryCompose(3, null, null, ba, Ppm, 100_000_000, out long second);
            Assert.That(first, Is.EqualTo(5));
            Assert.That(second, Is.EqualTo(4));
        }

        [Test]
        public void ComposeClampsToMaxCombinedSpeedDefensivelyAndRejectsOverflow()
        {
            Assert.That(SpeedComposer.TryCompose(60 * Ppm, null, null, null, 2 * Ppm, 100 * Ppm, out long clamped), Is.True);
            Assert.That(clamped, Is.EqualTo(100 * Ppm));
            Assert.That(SpeedComposer.TryCompose(50 * Ppm, null, null, null, 2 * Ppm, 100 * Ppm, out long exact), Is.True);
            Assert.That(exact, Is.EqualTo(100 * Ppm));

            Assert.That(SpeedComposer.TryCompose(ExactBound.Value, null, null, null, 2 * Ppm, 100 * Ppm, out long overflow), Is.False);
            Assert.That(overflow, Is.EqualTo(0));
            Assert.That(SpeedComposer.TryCompose(Ppm, null, null, new long[] { 0 }, Ppm, 100 * Ppm, out _), Is.False);
            Assert.That(SpeedComposer.TryCompose(0, null, null, null, Ppm, 100 * Ppm, out _), Is.False);
            Assert.That(SpeedComposer.TryCompose(Ppm, null, null, null, 0, 100 * Ppm, out _), Is.False);
        }

        [Test]
        public void BatchFactorIsOneUpToTheEventBoundAndGrowsBeyondIt()
        {
            const long duration = 20_000_000;
            const long minCycle = 1_000_000;

            Assert.That(SpeedComposer.TryBatch(duration, Ppm, minCycle, out long k, out long cycle), Is.True);
            Assert.That(k, Is.EqualTo(1));
            Assert.That(cycle, Is.EqualTo(duration));

            // The event bound: x20 gives the minimum cycle exactly with k = 1.
            Assert.That(SpeedComposer.TryBatch(duration, 20 * Ppm, minCycle, out k, out cycle), Is.True);
            Assert.That(k, Is.EqualTo(1));
            Assert.That(cycle, Is.EqualTo(minCycle));

            // Just beyond it the batch doubles, and the cycle stays at least the minimum.
            Assert.That(SpeedComposer.TryBatch(duration, 20 * Ppm + 1, minCycle, out k, out cycle), Is.True);
            Assert.That(k, Is.EqualTo(2));
            Assert.That(cycle, Is.EqualTo(2_000_000));

            // The x60 level-100 machine batches three cycles into the minimum cycle.
            Assert.That(SpeedComposer.TryBatch(duration, 60 * Ppm, minCycle, out k, out cycle), Is.True);
            Assert.That(k, Is.EqualTo(3));
            Assert.That(cycle, Is.EqualTo(minCycle));
        }

        [Test]
        public void BatchRateEqualsTheFullMultiplierWithinOneTickPerCycle()
        {
            long seed = 20260930;
            long[] durations = { 1, 7, 1_000_000, 20_000_000, 50_000_000, 999_999_999_999L, 1_000_000_000_000L };
            long[] minCycles = { 1_000_000, 3_500_000, 100_000_000 };
            for (int i = 0; i < 400; i++)
            {
                seed = (seed * 6364136223846793005L + 1442695040888963407L) & long.MaxValue;
                long speed = 1 + (seed >> 8) % 100_000_000_000L;
                long duration = durations[i % durations.Length];
                long minCycle = minCycles[(i / durations.Length) % minCycles.Length];
                string label = "seed-step " + i + " speed " + speed + " duration " + duration + " min " + minCycle;

                Assert.That(SpeedComposer.TryBatch(duration, speed, minCycle, out long k, out long cycle), Is.True, label);
                Assert.That(k, Is.GreaterThanOrEqualTo(1), label);
                Assert.That(cycle, Is.GreaterThanOrEqualTo(1), label);
                if (k > 1)
                {
                    Assert.That(cycle, Is.GreaterThanOrEqualTo(minCycle), label);
                    Assert.That(cycle, Is.LessThanOrEqualTo(2 * minCycle), label);
                }

                // cycle = ceil(k x D x 10^6 / speed): cycle x speed >= k x D x 10^6 > (cycle - 1) x speed.
                Assert.That(MulDiv.MulDivCeil(k * duration, Ppm, speed, out long expected), Is.True, label);
                Assert.That(cycle, Is.EqualTo(Math.Max(1, expected)), label);
                Assert.That(MulDiv.MulDivFloor(k * duration, Ppm, speed, out long floor), Is.True, label);
                Assert.That(cycle - floor, Is.InRange(0, 1), label);
            }
        }

        [Test]
        public void BatchRejectsInvalidInputAndCheckedOverflow()
        {
            Assert.That(SpeedComposer.TryBatch(0, Ppm, 1_000_000, out long k, out long cycle), Is.False);
            Assert.That(k, Is.EqualTo(0));
            Assert.That(cycle, Is.EqualTo(0));
            Assert.That(SpeedComposer.TryBatch(1_000_000_000_001L, Ppm, 1_000_000, out _, out _), Is.False);
            Assert.That(SpeedComposer.TryBatch(1_000, 0, 1_000_000, out _, out _), Is.False);
            Assert.That(SpeedComposer.TryBatch(1_000, Ppm, 0, out _, out _), Is.False);
            // k x D overflows the exact bound.
            Assert.That(SpeedComposer.TryBatch(1_000_000_000_000L, ExactBound.Value, ExactBound.Value, out k, out cycle), Is.False);
            Assert.That(k, Is.EqualTo(0));
            Assert.That(cycle, Is.EqualTo(0));
        }

        [Test]
        public void OutputFactorClampsToTheContentMaximum()
        {
            Assert.That(SpeedComposer.TryComposeOutputFactor(new long[] { 2, 2 }, 4, out long f), Is.True);
            Assert.That(f, Is.EqualTo(4));
            Assert.That(SpeedComposer.TryComposeOutputFactor(new long[] { 2, 3 }, 4, out f), Is.True);
            Assert.That(f, Is.EqualTo(4));
            Assert.That(SpeedComposer.TryComposeOutputFactor(new long[] { 3 }, 4, out f), Is.True);
            Assert.That(f, Is.EqualTo(3));
            Assert.That(SpeedComposer.TryComposeOutputFactor(Array.Empty<long>(), 4, out f), Is.True);
            Assert.That(f, Is.EqualTo(1));
            Assert.That(SpeedComposer.TryComposeOutputFactor(new long[] { ExactBound.Value, ExactBound.Value }, 8, out f), Is.True);
            Assert.That(f, Is.EqualTo(8));
            Assert.That(SpeedComposer.TryComposeOutputFactor(new long[] { 0 }, 4, out f), Is.False);
            Assert.That(f, Is.EqualTo(0));
            Assert.That(SpeedComposer.TryComposeOutputFactor(new long[] { 2 }, 0, out _), Is.False);
        }

        [Test]
        public void RescalePreservesTheCompletedFractionAndKeepsWaitingMachinesWaiting()
        {
            Assert.That(SpeedComposer.TryRescaleProgress(5, 10, 20, out long p), Is.True);
            Assert.That(p, Is.EqualTo(10));
            Assert.That(SpeedComposer.TryRescaleProgress(7, 10, 3, out p), Is.True);
            Assert.That(p, Is.EqualTo(2));
            Assert.That(SpeedComposer.TryRescaleProgress(0, 10, 3, out p), Is.True);
            Assert.That(p, Is.EqualTo(0));
            Assert.That(SpeedComposer.TryRescaleProgress(10, 10, 3, out p), Is.True);
            Assert.That(p, Is.EqualTo(3), "a waiting machine stays waiting");
            Assert.That(SpeedComposer.TryRescaleProgress(9, 10, 3, out p), Is.True);
            Assert.That(p, Is.LessThan(3), "a machine that is not waiting never becomes waiting");

            for (long d = 1; d <= 25; d++)
            for (long d2 = 1; d2 <= 25; d2++)
            for (long progress = 0; progress < d; progress++)
            {
                Assert.That(SpeedComposer.TryRescaleProgress(progress, d, d2, out long q), Is.True);
                // floor(p x D' / D): q x D <= p x D' < (q + 1) x D, so the fraction is preserved down to one tick.
                Assert.That(q * d, Is.LessThanOrEqualTo(progress * d2));
                Assert.That(progress * d2, Is.LessThan((q + 1) * d));
                Assert.That(q, Is.LessThan(d2));
            }

            Assert.That(SpeedComposer.TryRescaleProgress(11, 10, 20, out p), Is.False);
            Assert.That(p, Is.EqualTo(0));
            Assert.That(SpeedComposer.TryRescaleProgress(-1, 10, 20, out _), Is.False);
            Assert.That(SpeedComposer.TryRescaleProgress(1, 0, 20, out _), Is.False);
            Assert.That(SpeedComposer.TryRescaleProgress(1, 10, 0, out _), Is.False);
            Assert.That(SpeedComposer.TryRescaleProgress(1, 10, ExactBound.Value + 1, out _), Is.False);
            Assert.That(SpeedComposer.TryRescaleProgress(1, 2, ExactBound.Value, out p), Is.True);
            Assert.That(p, Is.EqualTo(ExactBound.Value / 2));
        }

        private static GlobalInventory Inventory(long capacity, long ore = 0, long bar = 0, long plate = 0)
        {
            var inventory = new GlobalInventory(new[] { "iron.ore", "iron.bar", "iron.plate", "iron.gear" }, capacity);
            var seed = new System.Collections.Generic.List<ResourceAmount>();
            if (ore > 0) seed.Add(new ResourceAmount("iron.ore", ore));
            if (bar > 0) seed.Add(new ResourceAmount("iron.bar", bar));
            if (plate > 0) seed.Add(new ResourceAmount("iron.plate", plate));
            Assert.That(inventory.TryImport(seed), Is.EqualTo(RunStateResult.Ok));
            return inventory;
        }

        private static long Completed(long k, MachineRecipe recipe, long factor, GlobalInventory inventory)
        {
            Assert.That(SpeedComposer.TryCompletedUnits(k, recipe, factor, inventory, out long units), Is.True);
            return units;
        }

        [Test]
        public void PartialBatchIsInputLimited()
        {
            var smelt = RunTestCatalogs.IronLoop().Recipes["recipe.smelt-iron-bar"];
            Assert.That(Completed(5, smelt, 1, Inventory(1000, ore: 7)), Is.EqualTo(3));
            Assert.That(Completed(5, smelt, 1, Inventory(1000, ore: 100)), Is.EqualTo(5));
            Assert.That(Completed(5, smelt, 1, Inventory(1000, ore: 1)), Is.EqualTo(0), "k' = 0 waits");
            Assert.That(Completed(5, smelt, 1, Inventory(1000)), Is.EqualTo(0));
        }

        [Test]
        public void PartialBatchIsCapacityLimited()
        {
            var mine = RunTestCatalogs.IronLoop().Recipes["recipe.mine-iron-ore"];
            // net = 2 ore per unit; 10 free units fit 5 units.
            Assert.That(Completed(10, mine, 1, Inventory(10)), Is.EqualTo(5));
            // The output factor scales the net: 6 per unit, so only 1 unit fits.
            Assert.That(Completed(10, mine, 3, Inventory(10)), Is.EqualTo(1));
            // Odd free space rounds down; a full store fits nothing.
            Assert.That(Completed(10, mine, 1, Inventory(11)), Is.EqualTo(5));
            Assert.That(Completed(10, mine, 1, Inventory(10, ore: 10)), Is.EqualTo(0));
            Assert.That(Completed(10, mine, 1, Inventory(10, ore: 9)), Is.EqualTo(0));
            Assert.That(Completed(2, mine, 1, Inventory(1000)), Is.EqualTo(2));
        }

        [Test]
        public void InputFreeRecipeIsLimitedOnlyByBatchAndCapacity()
        {
            var mine = RunTestCatalogs.IronLoop().Recipes["recipe.mine-iron-ore"];
            Assert.That(mine.Inputs, Is.Empty);
            Assert.That(Completed(7, mine, 1, Inventory(1000)), Is.EqualTo(7));
            Assert.That(Completed(7, mine, 1, Inventory(8)), Is.EqualTo(4));
        }

        [Test]
        public void NonPositiveNetRecipeIgnoresCapacity()
        {
            var press = RunTestCatalogs.IronLoop().Recipes["recipe.press-iron-plate"];
            // 2 bars -> 1 plate has net -1: a completely full store still completes, limited by inputs and k.
            var full = Inventory(10, bar: 10);
            Assert.That(full.Free, Is.EqualTo(0));
            Assert.That(Completed(3, press, 1, full), Is.EqualTo(3));
            Assert.That(Completed(9, press, 1, full), Is.EqualTo(5));
            // Net exactly 0 (factor 2: 2 plates out for 2 bars in) is also unbounded by capacity.
            Assert.That(Completed(4, press, 2, full), Is.EqualTo(4));
        }

        [Test]
        public void PartialBatchTakesTheMinimumOfBatchInputsAndCapacity()
        {
            var expand = new MachineRecipe("recipe.expand",
                new[] { new ResourceAmount("iron.ore", 1) }, new[] { new ResourceAmount("iron.bar", 3) }, 10_000_000);
            // net 2 per unit; free 10 -> k_cap 5; stock 10 -> k_in 10; k 8.
            Assert.That(Completed(8, expand, 1, Inventory(20, ore: 10)), Is.EqualTo(5));
            // k is the smallest.
            Assert.That(Completed(4, expand, 1, Inventory(20, ore: 10)), Is.EqualTo(4));
            // k_in is the smallest.
            Assert.That(Completed(8, expand, 1, Inventory(200, ore: 3)), Is.EqualTo(3));
            Assert.That(Completed(8, expand, 1, Inventory(11, ore: 10)), Is.EqualTo(0));
        }

        [Test]
        public void PartialBatchRejectsInvalidArgumentsAndOverflow()
        {
            var mine = RunTestCatalogs.IronLoop().Recipes["recipe.mine-iron-ore"];
            var inventory = Inventory(100);
            Assert.That(SpeedComposer.TryCompletedUnits(0, mine, 1, inventory, out long units), Is.False);
            Assert.That(units, Is.EqualTo(0));
            Assert.That(SpeedComposer.TryCompletedUnits(1, mine, 0, inventory, out _), Is.False);
            Assert.That(SpeedComposer.TryCompletedUnits(1, null, 1, inventory, out _), Is.False);
            var big = new MachineRecipe("recipe.big", Array.Empty<ResourceAmount>(),
                new[] { new ResourceAmount("iron.ore", ExactBound.Value) }, 1);
            Assert.That(SpeedComposer.TryCompletedUnits(1, big, 2, inventory, out units), Is.False);
            Assert.That(units, Is.EqualTo(0));
        }
    }
}
