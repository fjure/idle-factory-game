using System;
using System.Linq;
using System.Numerics;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// T323 (F5-R06, FR-013, ECO-6, ECO-7): the build price curve against a BigInteger oracle with the same steps, the
    /// Build command with an expected price, the discounts after the curve, the factory snapshot, and remove/rebuild.
    /// </summary>
    public sealed class BuildPricingTests
    {
        private const long Ppm = 1_000_000;
        private const long Cap10To12 = 1_000_000_000_000L;

        // The oracle: the algorithm of F5-R06 on BigInteger, so it cannot overflow. An overflow of the long path is
        // above the cap by definition, which is what the oracle computes.
        private static long Oracle(long baseValue, long ownedPpm, long prestigePpm, long cap, long owned, long prestiges)
        {
            BigInteger capScaled = new BigInteger(cap) * Ppm;
            BigInteger acc = new BigInteger(baseValue) * Ppm;
            for (long i = 0; i < owned; i++)
            {
                acc = acc * ownedPpm / Ppm;
                if (acc >= capScaled) return cap;
            }

            for (long i = 0; i < prestiges; i++)
            {
                acc = acc * prestigePpm / Ppm;
                if (acc >= capScaled) return cap;
            }

            BigInteger price = (acc + (Ppm - 1)) / Ppm;
            return (long)BigInteger.Min(price, cap);
        }

        private static long Curve(long baseValue, long ownedPpm, long prestigePpm, long cap, long owned, long prestiges)
        {
            Assert.That(BuildPricing.TryCurve(new BuildPriceDef(baseValue, ownedPpm, prestigePpm, cap), owned, prestiges, out long price), Is.True);
            return price;
        }

        [Test]
        public void Curve_HandComputedSteps()
        {
            // 10 x 1.1^n, ceiling once at the end: 10, 11, 12.1 -> 13, 13.31 -> 14.
            Assert.That(Enumerable.Range(0, 4).Select(n => Curve(10, 1_100_000, Ppm, 1000, n, 0)), Is.EqualTo(new long[] { 10, 11, 13, 14 }));
            // The prestige factor applies after the owned factor: 10 x 1.1 x 1.5 = 16.5 -> 17.
            Assert.That(Curve(10, 1_100_000, 1_500_000, 1000, 1, 1), Is.EqualTo(17));
            // The cap wins: 10 x 2^10 = 10240 -> 1000.
            Assert.That(Curve(10, 2_000_000, Ppm, 1000, 10, 0), Is.EqualTo(1000));
            // No growth: the base, never above the cap.
            Assert.That(Curve(10, Ppm, Ppm, 10, 12, 64), Is.EqualTo(10));
            Assert.That(Curve(0, 1_100_000, 1_100_000, 0, 12, 64), Is.EqualTo(0));
        }

        [Test]
        public void Curve_IsMonotonicInOwnedAndBoundedByTheCap()
        {
            long previous = 0;
            for (int n = 0; n <= 12; n++)
            {
                long price = Curve(25, 1_300_000, 1_100_000, 500, n, 3);
                Assert.That(price, Is.GreaterThanOrEqualTo(previous));
                Assert.That(price, Is.LessThanOrEqualTo(500));
                previous = price;
            }
        }

        [Test]
        public void Curve_MatchesTheBigIntegerOracleForRandomParameters()
        {
            var random = new Random(323);
            for (int i = 0; i < 20_000; i++)
            {
                long cap = Pick(random, Cap10To12);
                long baseValue = Pick(random, cap);
                long ownedPpm = Ppm + PickGrowth(random);
                long prestigePpm = Ppm + PickGrowth(random);
                long owned = random.Next(0, 13);
                long prestiges = random.Next(0, 65);
                long expected = Oracle(baseValue, ownedPpm, prestigePpm, cap, owned, prestiges);
                long actual = Curve(baseValue, ownedPpm, prestigePpm, cap, owned, prestiges);
                Assert.That(actual, Is.EqualTo(expected),
                    "base " + baseValue + " owned " + ownedPpm + " prestige " + prestigePpm + " cap " + cap + " n " + owned + " p " + prestiges);
            }
        }

        // Mixes tiny, medium and huge values so the cap, the rounding and the overflow paths all get hit.
        private static long Pick(Random random, long max)
        {
            long value;
            switch (random.Next(4))
            {
                case 0: value = random.Next(0, 50); break;
                case 1: value = (long)(random.NextDouble() * 100_000); break;
                case 2: value = (long)(random.NextDouble() * max); break;
                default: value = max; break;
            }

            return value > max ? max : value;
        }

        private static long PickGrowth(Random random)
        {
            switch (random.Next(5))
            {
                case 0: return 0;
                case 1: return random.Next(1, 1000);
                case 2: return random.Next(1, 1_000_000);
                case 3: return random.Next(1, 20_000_000);
                default: return (long)(random.NextDouble() * (ExactBound.Value - 2 * Ppm));
            }
        }

        [Test]
        public void Curve_BoundaryAt10To18()
        {
            // base 10^12 x 10^6 = 10^18 is the largest starting accumulator and still exact.
            Assert.That(Curve(Cap10To12, Ppm, Ppm, Cap10To12, 12, 64), Is.EqualTo(Cap10To12));
            Assert.That(Curve(Cap10To12, Ppm + 1, Ppm, Cap10To12, 0, 0), Is.EqualTo(Cap10To12));
            // The largest growth factor overflows the 64-bit step: that is above the cap, so the price is the cap.
            Assert.That(Curve(Cap10To12 - 1, ExactBound.Value, ExactBound.Value, Cap10To12, 1, 0), Is.EqualTo(Cap10To12));
            Assert.That(Curve(1, ExactBound.Value, Ppm, 777, 12, 0), Is.EqualTo(777));
            Assert.That(Curve(1, Ppm, ExactBound.Value, 777, 0, 64), Is.EqualTo(777));
            // Inputs outside the content bounds are refused, not wrapped.
            Assert.That(BuildPricing.TryCurve(new BuildPriceDef(Cap10To12 + 1, Ppm, Ppm, Cap10To12 + 1), 0, 0, out _), Is.False);
            Assert.That(BuildPricing.TryCurve(new BuildPriceDef(1, Ppm, Ppm, 1), -1, 0, out _), Is.False);
        }

        // ---- the Build command ---------------------------------------------------------------------------------

        private static RunCommandFixtures.Rig Rig(RunCommandFixtures.Options options = null, long cash = 1_000_000, long prestiges = 0,
            BuildPriceDef curve = null, bool legacyContent = false)
        {
            options = options ?? new RunCommandFixtures.Options();
            curve = legacyContent ? null : curve ?? new BuildPriceDef(10, 1_100_000, 1_050_000, 1000);
            RunCatalog baseCatalog = RunCommandFixtures.Catalog(options);
            var machines = baseCatalog.Machines.Values.Select(m => new MachineDef(m.Id, m.Label, m.PresentationKey, m.RecipeId, m.MaxLevel,
                m.Cost, m.SpeedGrowthPpm, m.Milestones, m.Unlock, curve)).ToArray();
            RunCatalog catalog = TestCatalogBuilder.From(baseCatalog).WithMachines(machines).Build();
            var unlocked = new[] { "iron.ore", "iron.bar", "iron.plate", "iron.gear", RunCommandFixtures.Miner, RunCommandFixtures.Smelter,
                RunCommandFixtures.Press, RunCommandFixtures.Assembler, RunState.OverdriveUnlockId };
            var state = new RunState(catalog, RunSimHarness.Id(9000), 1, null, cash, 0, 1, 5, unlocked);
            var rig = new RunCommandFixtures.Rig { Catalog = catalog, State = state };
            rig.Session = new RunSession(catalog, state, new PermanentState(catalog, 0, prestiges), RunCommandFixtures.StartUtcMs);
            rig.Commands = new RunCommands(rig.Session, () => RunSimHarness.Id(rig.NextId++));
            return rig;
        }

        [Test]
        public void Build_ChargesTheCurvePriceForTheOwnedCount()
        {
            RunCommandFixtures.Rig rig = Rig();
            long cash = rig.State.Cash;
            foreach (long expected in new long[] { 10, 11, 13, 14 })
            {
                RunCommandResult<BuildResult> built = rig.Commands.Build(rig.State.Machines.Count, RunCommandFixtures.Miner);
                Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
                Assert.That(built.Value.CostPaid, Is.EqualTo(expected));
                Assert.That(rig.State.Machines[rig.State.Machines.Count - 1].InvestedCash, Is.EqualTo(expected));
                cash -= expected;
                Assert.That(rig.State.Cash, Is.EqualTo(cash));
            }

            // Another type has its own owned count: the first Smelter costs the base again.
            Assert.That(rig.Commands.Build(4, RunCommandFixtures.Smelter).Value.CostPaid, Is.EqualTo(10));
        }

        [Test]
        public void Build_PrestigeCountRaisesThePrice()
        {
            RunCommandFixtures.Rig rig = Rig(prestiges: 2);
            // 10 x 1.05^2 = 11.025 -> 12.
            Assert.That(rig.Commands.Build(0, RunCommandFixtures.Miner).Value.CostPaid, Is.EqualTo(12));
        }

        [Test]
        public void Build_ExpectedPriceMismatchRejectsWithoutChangingState()
        {
            RunCommandFixtures.Rig rig = Rig();
            rig.Commands.Build(0, RunCommandFixtures.Miner);
            string before = RunCommandFixtures.Snapshot(rig.State);
            long revision = rig.Session.StructureRevision;

            RunCommandResult<BuildResult> stale = rig.Commands.Build(1, RunCommandFixtures.Miner, 10);
            Assert.That(stale.IsSuccess, Is.False);
            Assert.That(stale.ReasonCode, Is.EqualTo(RunReasons.BuildPriceChanged));
            Assert.That(stale.ReasonCode, Is.EqualTo("build.price.changed"));
            Assert.That(RunCommandFixtures.Snapshot(rig.State), Is.EqualTo(before));
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(revision));

            Assert.That(rig.Commands.Build(1, RunCommandFixtures.Miner, 11).Value.CostPaid, Is.EqualTo(11), "the displayed price is charged");
            Assert.That(rig.Commands.Build(2, RunCommandFixtures.Miner, null).Value.CostPaid, Is.EqualTo(13), "null is the price at the call instant");
        }

        [Test]
        public void Build_OtherChecksKeepTheirOrder()
        {
            RunCommandFixtures.Rig rig = Rig(cash: 5);
            Assert.That(rig.Commands.Build(0, RunCommandFixtures.Miner, 10).ReasonCode, Is.EqualTo(RunReasons.CashInsufficient));
            Assert.That(rig.Commands.Build(99, RunCommandFixtures.Miner, 1).ReasonCode, Is.EqualTo(RunReasons.SlotUnavailable));
            Assert.That(rig.Commands.Build(0, "machine.nope", 1).ReasonCode, Is.EqualTo(RunReasons.MachineLocked));
        }

        [Test]
        public void Build_DiscountsApplyAfterTheCurveWithRoundUp()
        {
            RunCommandFixtures.Rig rig = Rig(new RunCommandFixtures.Options { DiscountResearch = true });
            Assert.That(rig.State.TrySetResearchLevel(RunCommandFixtures.MachineDiscount, 1), Is.EqualTo(RunStateResult.Ok));
            // 0.8 x 10 = 8, 0.8 x 11 = 8.8 -> 9, 0.8 x 13 = 10.4 -> 11.
            foreach (long expected in new long[] { 8, 9, 11 })
            {
                long shown = RunSnapshots.Factory(rig.Session).MachineTypes.First(t => t.MachineId == RunCommandFixtures.Miner).BuildCost;
                Assert.That(shown, Is.EqualTo(expected));
                Assert.That(rig.Commands.Build(rig.State.Machines.Count, RunCommandFixtures.Miner, shown).Value.CostPaid, Is.EqualTo(expected));
            }
        }

        [Test]
        public void Build_AtTheTopOfTheNumericRange()
        {
            // Cash just under 10^18; a cap-priced build (10^12) fits and leaves the remainder.
            RunCatalog baseCatalog = RunCommandFixtures.Catalog();
            MachineDef miner = baseCatalog.Machines[RunCommandFixtures.Miner];
            var big = new MachineDef(miner.Id, miner.Label, miner.PresentationKey, miner.RecipeId, miner.MaxLevel, miner.Cost,
                miner.SpeedGrowthPpm, miner.Milestones, miner.Unlock, new BuildPriceDef(Cap10To12, ExactBound.Value, ExactBound.Value, Cap10To12));
            RunCatalog catalog = TestCatalogBuilder.From(baseCatalog)
                .WithMachines(baseCatalog.Machines.Values.Select(m => m.Id == miner.Id ? big : m)).Build();
            var state = new RunState(catalog, RunSimHarness.Id(9000), 1, null, ExactBound.Value - 1, 0, 1, 1,
                new[] { "iron.ore", RunCommandFixtures.Miner, RunState.OverdriveUnlockId });
            var session = new RunSession(catalog, state, new PermanentState(catalog, 0, 64), RunCommandFixtures.StartUtcMs);
            var commands = new RunCommands(session, () => RunSimHarness.Id(1));

            RunCommandResult<BuildResult> first = commands.Build(0, RunCommandFixtures.Miner, Cap10To12);
            Assert.That(first.IsSuccess, Is.True, first.ReasonCode);
            Assert.That(first.Value.CostPaid, Is.EqualTo(Cap10To12));
            Assert.That(state.Cash, Is.EqualTo(ExactBound.Value - 1 - Cap10To12));
        }

        // ---- the snapshot --------------------------------------------------------------------------------------

        [Test]
        public void Snapshot_ShowsTheNextPriceAndCachesPerStructureRevision()
        {
            RunCommandFixtures.Rig rig = Rig();
            long Shown() => RunSnapshots.Factory(rig.Session).MachineTypes.First(t => t.MachineId == RunCommandFixtures.Miner).BuildCost;
            Assert.That(Shown(), Is.EqualTo(10));
            Assert.That(Shown(), Is.EqualTo(10), "a second read at the same revision gives the same price");
            rig.Commands.Build(0, RunCommandFixtures.Miner);
            Assert.That(Shown(), Is.EqualTo(11));
            Guid id = rig.State.Machines[0].InstanceId;
            Assert.That(rig.Commands.Remove(id, true).IsSuccess, Is.True);
            Assert.That(Shown(), Is.EqualTo(10), "removing lowers the owned count and the shown price");
        }

        private static MachineTypeSnapshot MinerType(RunCommandFixtures.Rig rig) =>
            RunSnapshots.Factory(rig.Session).MachineTypes.First(t => t.MachineId == RunCommandFixtures.Miner);

        [Test]
        public void Snapshot_OwnedOfTypeAndRebuildPriceAgreeWithBuildPricing()
        {
            RunCommandFixtures.Rig rig = Rig(prestiges: 1);
            MachineDef miner = rig.Catalog.Machines[RunCommandFixtures.Miner];
            long lastPaid = 0;
            for (int owned = 0; owned < 5; owned++)
            {
                MachineTypeSnapshot type = MinerType(rig);
                Assert.That(type.OwnedOfType, Is.EqualTo(owned));
                Assert.That(BuildPricing.TryPrice(rig.Catalog, new RunModifiers(rig.Catalog), rig.State, rig.Session.Permanent, miner, out long price), Is.True);
                Assert.That(type.BuildCost, Is.EqualTo(price));
                Assert.That(type.PriceCapped, Is.False, "far below the cap of 1000");
                Assert.That(type.RebuildPrice, Is.EqualTo(owned == 0 ? 0 : lastPaid), "the price of the machine that Remove would free");
                lastPaid = rig.Commands.Build(owned, RunCommandFixtures.Miner, type.BuildCost).Value.CostPaid;
                Assert.That(lastPaid, Is.EqualTo(price));
            }

            Assert.That(MinerType(rig).OwnedOfType, Is.EqualTo(5));
            Assert.That(RunSnapshots.Factory(rig.Session).MachineTypes.First(t => t.MachineId == RunCommandFixtures.Smelter).OwnedOfType, Is.EqualTo(0));
        }

        [Test]
        public void Snapshot_FlatContentAndLegacyContentAreNeverCapped()
        {
            RunCommandFixtures.Rig flat = Rig(curve: new BuildPriceDef(10, Ppm, Ppm, 10));
            RunCommandFixtures.Rig legacy = Rig(legacyContent: true);
            foreach (RunCommandFixtures.Rig rig in new[] { flat, legacy })
            {
                for (int owned = 0; owned < 3; owned++)
                {
                    Assert.That(MinerType(rig).PriceCapped, Is.False);
                    Assert.That(rig.Commands.Build(owned, RunCommandFixtures.Miner).IsSuccess, Is.True);
                }

                MachineTypeSnapshot type = MinerType(rig);
                Assert.That(type.PriceCapped, Is.False);
                Assert.That(type.OwnedOfType, Is.EqualTo(3));
                Assert.That(type.RebuildPrice, Is.EqualTo(type.BuildCost), "no growth: the rebuild price equals the next price");
            }
        }

        [Test]
        public void Snapshot_GrowingContentReadsCappedOnceTheCurveReachesTheCap()
        {
            // 10 x 2^n: 10, 20, 40, 80, then 160 -> the cap of 100.
            RunCommandFixtures.Rig rig = Rig(curve: new BuildPriceDef(10, 2_000_000, Ppm, 100));
            var capped = new System.Collections.Generic.List<bool>();
            var prices = new System.Collections.Generic.List<long>();
            for (int owned = 0; owned < 6; owned++)
            {
                MachineTypeSnapshot type = MinerType(rig);
                capped.Add(type.PriceCapped);
                prices.Add(type.BuildCost);
                Assert.That(rig.Commands.Build(owned, RunCommandFixtures.Miner, type.BuildCost).IsSuccess, Is.True);
            }

            Assert.That(prices, Is.EqualTo(new long[] { 10, 20, 40, 80, 100, 100 }));
            Assert.That(capped, Is.EqualTo(new[] { false, false, false, false, true, true }));
            Assert.That(MinerType(rig).RebuildPrice, Is.EqualTo(100));

            // Removing one lowers the owned count back to 5: still capped; to 3 it is no longer capped.
            Assert.That(rig.Commands.Remove(rig.State.Machines[0].InstanceId, true).IsSuccess, Is.True);
            Assert.That(MinerType(rig).PriceCapped, Is.True);
            Assert.That(rig.Commands.Remove(rig.State.Machines[0].InstanceId, true).IsSuccess, Is.True);
            Assert.That(rig.Commands.Remove(rig.State.Machines[0].InstanceId, true).IsSuccess, Is.True);
            MachineTypeSnapshot three = MinerType(rig);
            Assert.That(three.OwnedOfType, Is.EqualTo(3));
            Assert.That(three.PriceCapped, Is.False);
            Assert.That(three.BuildCost, Is.EqualTo(80));
            Assert.That(three.RebuildPrice, Is.EqualTo(40));
        }

        [Test]
        public void Snapshot_RebuildPriceNeverExceedsThePriceLastPaid_Eco7()
        {
            foreach (bool discount in new[] { false, true })
            {
                RunCommandFixtures.Rig rig = Rig(new RunCommandFixtures.Options { DiscountResearch = true }, 1_000_000);
                if (discount) Assert.That(rig.State.TrySetResearchLevel(RunCommandFixtures.MachineDiscount, 3), Is.EqualTo(RunStateResult.Ok));
                for (int i = 0; i < 8; i++)
                {
                    long paid = rig.Commands.Build(i, RunCommandFixtures.Miner).Value.CostPaid;
                    Assert.That(MinerType(rig).RebuildPrice, Is.LessThanOrEqualTo(paid), "discount " + discount + " owned " + (i + 1));
                }
            }
        }

        // ---- ECO-7 ---------------------------------------------------------------------------------------------

        [Test]
        public void RemoveAndRebuild_NeverEndsWithMoreCashOrFactoryValue()
        {
            RunCommandFixtures.Rig rig = Rig(cash: 100_000);
            for (int i = 0; i < 3; i++) Assert.That(rig.Commands.Build(i, RunCommandFixtures.Miner).IsSuccess, Is.True);
            for (int round = 0; round < 6; round++)
            {
                long cash = rig.State.Cash;
                long value = RunCommandFixtures.FactoryValue(rig.State);
                MachineInstance victim = rig.State.Machines[round % rig.State.Machines.Count];
                int slot = victim.SlotIndex;
                Assert.That(rig.Commands.Remove(victim.InstanceId, true).IsSuccess, Is.True);
                RunCommandResult<BuildResult> rebuilt = rig.Commands.Build(slot, RunCommandFixtures.Miner);
                Assert.That(rebuilt.IsSuccess, Is.True, rebuilt.ReasonCode);
                Assert.That(rig.State.Cash, Is.LessThanOrEqualTo(cash), "round " + round);
                Assert.That(RunCommandFixtures.FactoryValue(rig.State), Is.LessThanOrEqualTo(value), "round " + round);
            }
        }

        [Test]
        public void RemoveAndRebuild_AcrossTheWholeCurveAndWithDiscounts()
        {
            foreach (bool discount in new[] { false, true })
            {
                RunCommandFixtures.Rig rig = Rig(new RunCommandFixtures.Options { DiscountResearch = true }, 1_000_000);
                if (discount) Assert.That(rig.State.TrySetResearchLevel(RunCommandFixtures.MachineDiscount, 3), Is.EqualTo(RunStateResult.Ok));
                for (int i = 0; i < 12; i++) Assert.That(rig.Commands.Build(i, RunCommandFixtures.Miner).IsSuccess, Is.True);
                for (int i = 0; i < 12; i++)
                {
                    long cash = rig.State.Cash;
                    long value = RunCommandFixtures.FactoryValue(rig.State);
                    MachineInstance m = rig.State.Machines[i];
                    int slot = m.SlotIndex;
                    Assert.That(rig.Commands.Remove(m.InstanceId, true).IsSuccess, Is.True);
                    Assert.That(rig.Commands.Build(slot, RunCommandFixtures.Miner).IsSuccess, Is.True);
                    Assert.That(rig.State.Cash, Is.LessThanOrEqualTo(cash), "discount " + discount + " machine " + i);
                    Assert.That(RunCommandFixtures.FactoryValue(rig.State), Is.LessThanOrEqualTo(value), "discount " + discount + " machine " + i);
                }
            }
        }
    }
}
