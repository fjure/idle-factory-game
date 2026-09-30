using System;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// Factory Value and the IP formula (T102, contracts/run-application.md, FR-020, FR-021, R06, R11, ECO-7,
    /// ECO-8): the breakdown, the sale-price rounding, the exclusion of removed machines and of RP, Research and
    /// Items, the build-then-remove cycle that always lowers the value (also under cost discounts), and the
    /// per-sale floor that makes many small sales differ from one sale by less than $1 per sale.
    /// </summary>
    public sealed class FactoryValuationTests
    {
        private const string Price = "research.price";
        private const string Boost = "item.boost";
        private const long Ms = RunSession.TicksPerMs;

        private static RunCommandFixtures.Options PriceOptions(long ppm) => new RunCommandFixtures.Options
        {
            Research = new[]
            {
                new ResearchDef(Price, "Price", 2, new Curve(5, 1_500_000, "ceil"), new ResearchPrerequisite[0],
                    new EffectDef("salePrice", "resource", "iron.ore", ppm)),
            },
        };

        private static FactoryValueBreakdown Value(RunCommandFixtures.Rig rig)
        {
            Assert.That(FactoryValuation.TryCompute(rig.Catalog, rig.State, rig.Session.Permanent, out FactoryValueBreakdown v), Is.True);
            return v;
        }

        [Test]
        public void Breakdown_AddsCashStockMachinesAndBothExpansions()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(cash: 100_000);
            rig.Advance(0);
            Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            Assert.That(rig.Commands.UpgradeStorage().IsSuccess, Is.True);
            Assert.That(rig.Commands.UpgradeFactory().IsSuccess, Is.True);
            RunSimHarness.Stock(rig.State, "iron.ore", 7);
            RunSimHarness.Stock(rig.State, "iron.bar", 3);
            RunSimHarness.Stock(rig.State, "iron.gear", 2);

            FactoryValueBreakdown v = Value(rig);

            long machine = rig.State.Machines[0].InvestedCash;
            Assert.That(machine, Is.EqualTo(10));
            Assert.That(v.Cash, Is.EqualTo(rig.State.Cash));
            Assert.That(v.StockValue, Is.EqualTo(7 * 1 + 3 * 3 + 2 * 24));
            Assert.That(v.MachineInvestedCash, Is.EqualTo(machine));
            Assert.That(v.StorageInvestedCash, Is.EqualTo(2500));
            Assert.That(v.FactoryInvestedCash, Is.EqualTo(5000));
            Assert.That(v.ExpansionInvestedCash, Is.EqualTo(7500));
            Assert.That(v.Total, Is.EqualTo(v.Cash + v.StockValue + v.MachineInvestedCash + 2500 + 5000));
            Assert.That(v.Total, Is.EqualTo(RunCommandFixtures.FactoryValue(rig.State)), "the fixture's base-price total agrees without modifiers");
        }

        [Test]
        public void Stock_IsValuedWithTheSaleFormulaAndRoundsDownPerResource()
        {
            // 3 ore at 1.25x: floor(3 x 1 x 1.25) = 3; 7 ore: floor(8.75) = 8; each resource floors on its own.
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(PriceOptions(1_250_000), cash: 1000);
            Assert.That(rig.State.TrySetResearchLevel(Price, 1), Is.EqualTo(RunStateResult.Ok));
            RunSimHarness.Stock(rig.State, "iron.ore", 7);
            RunSimHarness.Stock(rig.State, "iron.bar", 3);
            Assert.That(Value(rig).StockValue, Is.EqualTo(8 + 9), "ore boosted and floored, bars at base price");

            Assert.That(rig.State.TrySetResearchLevel(Price, 2), Is.EqualTo(RunStateResult.Ok));
            Assert.That(Value(rig).StockValue, Is.EqualTo(FoldedIncome(7, 1_250_000, 2) + 9), "two levels fold multiplicatively: floor(7 x 1.5625)");
        }

        // floor(units x factor^levels) for a base price of 1, folded per level like the modifiers do.
        private static long FoldedIncome(long units, long ppm, int levels)
        {
            long acc = 1_000_000;
            for (int i = 0; i < levels; i++)
            {
                Assert.That(MulDiv.MulDivFloor(acc, ppm, 1_000_000, out acc), Is.True);
            }
            Assert.That(MulDiv.MulDivFloor(units, acc, 1_000_000, out long income), Is.True);
            return income;
        }

        [Test]
        public void StockValue_EqualsTheIncomeOfARealSale()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(PriceOptions(1_250_000), cash: 1000);
            rig.Advance(0);
            Assert.That(rig.State.TrySetResearchLevel(Price, 1), Is.EqualTo(RunStateResult.Ok));
            RunSimHarness.Stock(rig.State, "iron.ore", 7);
            FactoryValueBreakdown before = Value(rig);
            long cash = rig.State.Cash;

            Assert.That(rig.Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);

            Assert.That(rig.State.Inventory.Units("iron.ore"), Is.EqualTo(0));
            Assert.That(rig.State.Cash - cash, Is.EqualTo(before.StockValue));
            Assert.That(Value(rig).Total, Is.EqualTo(before.Total), "selling the whole stock at once leaves the value unchanged");
        }

        [Test]
        public void RemovedMachineRpResearchAndItemsAddNothing()
        {
            var o = new RunCommandFixtures.Options
            {
                Items = new[] { new ItemDef(Boost, 1, "Boost", "F003-ITEM-BOOST", 60_000, "factory", new string[0], new EffectDef("machineSpeed", "allMachines", null, 1_500_000)) },
                Research = new[]
                {
                    new ResearchDef(Price, "Price", 2, new Curve(5, 1_500_000, "ceil"), new ResearchPrerequisite[0], new EffectDef("machineSpeed", "allMachines", null, 1_100_000)),
                },
            };
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(o, cash: 100_000, rp: 100);
            rig.Advance(0);
            RunCommandResult<BuildResult> first = rig.Build(0, RunCommandFixtures.Miner);
            RunCommandResult<BuildResult> second = rig.Build(1, RunCommandFixtures.Smelter);
            Assert.That(first.IsSuccess && second.IsSuccess, Is.True);
            Assert.That(rig.Commands.Remove(first.Value.InstanceId, true).IsSuccess, Is.True);

            FactoryValueBreakdown v = Value(rig);
            Assert.That(v.MachineInvestedCash, Is.EqualTo(rig.State.Machines[0].InvestedCash), "only the machine that still stands counts");
            Assert.That(rig.State.Machines.Count, Is.EqualTo(1));

            long total = v.Total;
            Assert.That(rig.State.TryAddRp(5000), Is.EqualTo(RunStateResult.Ok));
            Assert.That(rig.Commands.PurchaseResearch(Price).IsSuccess, Is.True);
            Assert.That(rig.Session.Permanent.TryAddItems(Boost, 4), Is.EqualTo(RunStateResult.Ok));
            Assert.That(rig.Commands.ActivateItem(Boost, null).IsSuccess, Is.True);
            Assert.That(Value(rig).Total, Is.EqualTo(total), "RP, Research levels, owned Items and a spent Item are not value");
        }

        [Test]
        public void BuildThenRemove_AlwaysLowersTheValue_AlsoUnderDiscountsAndTheHighestRefund()
        {
            foreach (long refundPpm in new long[] { 0, 500_000, 999_999 })
            {
                foreach (bool discount in new[] { false, true })
                {
                    foreach (string machine in new[] { RunCommandFixtures.Miner, RunCommandFixtures.Smelter, RunCommandFixtures.Press, RunCommandFixtures.Assembler })
                    {
                        var o = new RunCommandFixtures.Options { RefundPpm = refundPpm, DiscountResearch = discount };
                        RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(o, cash: 100_000);
                        rig.Advance(0);
                        if (discount) Assert.That(rig.State.TrySetResearchLevel(RunCommandFixtures.MachineDiscount, 3), Is.EqualTo(RunStateResult.Ok));
                        long start = Value(rig).Total;

                        RunCommandResult<BuildResult> built = rig.Build(0, machine);
                        Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
                        Assert.That(built.Value.CostPaid, Is.GreaterThan(0));
                        Assert.That(Value(rig).Total, Is.EqualTo(start), "building only moves Cash into invested Cash");
                        Assert.That(rig.Commands.Upgrade(built.Value.InstanceId).IsSuccess, Is.True);
                        Assert.That(rig.Commands.Remove(built.Value.InstanceId, true).IsSuccess, Is.True);

                        Assert.That(Value(rig).Total, Is.LessThan(start), $"{machine} refund {refundPpm} discount {discount}");
                    }
                }
            }
        }

        [Test]
        public void ManySmallSales_DifferFromOneSaleByLessThanOneDollarPerSale()
        {
            // The real thing: a Miner sells 2 ore per cycle at 1.25x, 10 cycles, then the same 20 units at once.
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(PriceOptions(1_250_000), cash: 1000);
            rig.Advance(0);
            Assert.That(rig.State.TrySetResearchLevel(Price, 1), Is.EqualTo(RunStateResult.Ok));
            Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            Assert.That(rig.Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            long cash = rig.State.Cash;
            rig.Advance(10 * 20_000_000 + 1);
            long soldSmall = rig.State.Cash - cash;
            Assert.That(rig.State.Inventory.Units("iron.ore"), Is.EqualTo(0), "everything was sold as it came");
            long sales = soldSmall / 2; // 2 ore at 1.25x floor to 2 dollars per sale
            Assert.That(sales, Is.GreaterThanOrEqualTo(9));
            long units = sales * 2;

            Assert.That(MulDiv.MulDivFloor(units, 1_250_000, 1_000_000, out long one), Is.True);
            Assert.That(one - soldSmall, Is.GreaterThan(0));
            Assert.That(one - soldSmall, Is.LessThan(sales), "less than $1 per sale");

            // The valuation of that stock is the one-sale figure.
            RunCommandFixtures.Rig other = RunCommandFixtures.NewRig(PriceOptions(1_250_000), cash: 1000);
            Assert.That(other.State.TrySetResearchLevel(Price, 1), Is.EqualTo(RunStateResult.Ok));
            RunSimHarness.Stock(other.State, "iron.ore", units);
            Assert.That(Value(other).StockValue, Is.EqualTo(one));
        }

        [Test]
        public void SaleRounding_OverManySizesAndPrices_StaysBelowOneDollarPerSale()
        {
            foreach (long ppm in new long[] { 1_000_000, 1_010_000, 1_250_000, 1_333_333, 2_999_999 })
            {
                foreach (long baseUnits in new long[] { 1, 2, 3, 7 })
                {
                    foreach (long price in new long[] { 1, 3, 8, 24 })
                    {
                        for (long total = baseUnits; total <= 210; total += baseUnits)
                        {
                            long sales = total / baseUnits;
                            Assert.That(MulDiv.MulDivFloor(baseUnits * price, ppm, 1_000_000, out long each), Is.True);
                            Assert.That(MulDiv.MulDivFloor(total * price, ppm, 1_000_000, out long one), Is.True);
                            long diff = one - each * sales;
                            Assert.That(diff, Is.GreaterThanOrEqualTo(0), $"ppm {ppm} units {baseUnits} x {sales} price {price}");
                            Assert.That(diff, Is.LessThan(sales), $"ppm {ppm} units {baseUnits} x {sales} price {price}");
                        }
                    }
                }
            }
        }

        [Test]
        public void Overflow_IsReportedNotWrapped()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(cash: ExactBound.Value);
            RunSimHarness.Stock(rig.State, "iron.gear", 5);
            Assert.That(FactoryValuation.TryCompute(rig.Catalog, rig.State, rig.Session.Permanent, out _), Is.False);
            RunCommandResult<PrestigePreview> preview = rig.Commands.GetPrestigePreview();
            Assert.That(preview.IsSuccess, Is.False);
            Assert.That(preview.ReasonCode, Is.EqualTo(RunReasons.ValueLimit));
        }

        [TestCase(10_000_000L, 10L)]
        [TestCase(40_000_000L, 20L)]
        [TestCase(90_000_000L, 30L)]
        [TestCase(1_000_000_000L, 100L)]
        [TestCase(9_999_999L, 9L)]
        [TestCase(0L, 0L)]
        [TestCase(1L, 0L)]
        [TestCase(10_100_000L, 10L)]
        [TestCase(ExactBound.Value, 3_162_277L)]
        public void Ip_IsTheFlooredSquareRootOfTheScaledValue(long fv, long expected)
        {
            PrestigeDef p = RunCommandFixtures.Catalog().Prestige;
            Assert.That(FactoryValuation.TryComputeIp(p, fv, out long ip), Is.True);
            Assert.That(ip, Is.EqualTo(expected));
        }
    }
}
