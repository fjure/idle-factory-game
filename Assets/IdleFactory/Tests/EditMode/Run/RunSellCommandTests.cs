using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// T317 (F5-R04, FR-001 to FR-003, SC-003, QA-5, QA-6): Sell and SellAll. Rejection order and atomicity,
    /// full-stock Factory Value neutrality at several prices, no sale raising Factory Value, selling while a full
    /// store blocks a machine, the 10^18 boundaries, the settle pre-check that subtracts the sold stock, the sale
    /// event, and the sell-step previews of the Economy snapshot.
    /// </summary>
    public sealed class RunSellCommandTests
    {
        private const string Price = "research.price";
        private const long Bound = ExactBound.Value;

        private sealed class SaleSink : IRunEventSink
        {
            public readonly List<RunSaleEvent> Sales = new List<RunSaleEvent>();
            public int Others;
            public bool Throw;
            public void OnHeader(in RunHeaderEvent e) => Others++;
            public void OnSample(in RunSampleEvent e) => Others++;
            public void OnMilestone(in RunMilestoneEvent e) => Others++;
            public void OnSale(in RunSaleEvent e) { Sales.Add(e); if (Throw) throw new InvalidOperationException("sink failure"); }
            public void OnContract(in RunContractEvent e) => Others++;
            public void OnPrestige(in RunPrestigeEvent e) => Others++;
            public void OnOfflineReturn(in RunOfflineReturnEvent e) => Others++;
            public void OnBottleneck(in RunBottleneckEvent e) => Others++;
            public void OnPurchase(in RunPurchaseEvent e) => Others++;
            public void OnClock(in RunClockEvent e) => Others++;
            public void OnLifecycle(in RunLifecycleEvent e) => Others++;
        }

        private static RunCommandFixtures.Options PriceOptions(long ppm) => new RunCommandFixtures.Options
        {
            Research = new[]
            {
                new ResearchDef(Price, "Price", 2, new Curve(5, 1_500_000, "ceil"), new ResearchPrerequisite[0],
                    new EffectDef("salePrice", "allResources", null, ppm)),
            },
        };

        private static RunCommandFixtures.Rig WithSink(RunCommandFixtures.Rig rig, IRunEventSink sink)
        {
            rig.Session = new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog), RunCommandFixtures.StartUtcMs, sink);
            rig.Commands = new RunCommands(rig.Session, () => RunSimHarness.Id(rig.NextId++));
            return rig;
        }

        private static long Value(RunCommandFixtures.Rig rig)
        {
            Assert.That(FactoryValuation.TryCompute(rig.Catalog, rig.State, rig.Session.Permanent, out FactoryValueBreakdown v), Is.True);
            return v.Total;
        }

        private static void AssertRejectedWithoutChange(RunCommandFixtures.Rig rig, Func<RunCommandResult<SellResult>> command, string reason, SaleSink sink = null)
        {
            string before = RunCommandFixtures.Snapshot(rig.State);
            long structure = rig.Session.StructureRevision;
            long quantity = rig.Session.QuantityRevision;
            int sales = sink?.Sales.Count ?? 0;

            RunCommandResult<SellResult> result = command();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ReasonCode, Is.EqualTo(reason));
            Assert.That(RunCommandFixtures.Snapshot(rig.State), Is.EqualTo(before), "a rejection changes no state");
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure));
            Assert.That(rig.Session.QuantityRevision, Is.EqualTo(quantity));
            if (sink != null) Assert.That(sink.Sales.Count, Is.EqualTo(sales), "a rejection raises no sale event");
        }

        // ---- reasons and order ---------------------------------------------------------------------------------

        [Test]
        public void ReasonCode_IsTheStableStringAndNotOneOfTheOriginal24()
        {
            Assert.That(RunReasons.SellQuantityInvalid, Is.EqualTo("sell.quantity-invalid"));
            Assert.That(RunReasons.All.Count, Is.EqualTo(24));
            Assert.That(RunReasons.All, Does.Not.Contain(RunReasons.SellQuantityInvalid));
        }

        [Test]
        public void Rejections_EachLeaveEverythingUnchanged()
        {
            var sink = new SaleSink();
            RunCommandFixtures.Rig rig = WithSink(RunCommandFixtures.NewRig(cash: 50, lockedResources: new[] { "iron.gear" }), sink);
            RunSimHarness.Stock(rig.State, "iron.ore", 5);

            AssertRejectedWithoutChange(rig, () => rig.Commands.Sell("iron.gear", 1), RunReasons.ResourceLocked, sink);
            AssertRejectedWithoutChange(rig, () => rig.Commands.Sell("iron.nope", 1), RunReasons.ResourceLocked, sink);
            AssertRejectedWithoutChange(rig, () => rig.Commands.Sell(null, 1), RunReasons.ResourceLocked, sink);
            AssertRejectedWithoutChange(rig, () => rig.Commands.Sell("iron.ore", 0), RunReasons.SellQuantityInvalid, sink);
            AssertRejectedWithoutChange(rig, () => rig.Commands.Sell("iron.ore", -3), RunReasons.SellQuantityInvalid, sink);
            AssertRejectedWithoutChange(rig, () => rig.Commands.Sell("iron.ore", long.MinValue), RunReasons.SellQuantityInvalid, sink);
            AssertRejectedWithoutChange(rig, () => rig.Commands.Sell("iron.ore", 6), RunReasons.StockInsufficient, sink);
            AssertRejectedWithoutChange(rig, () => rig.Commands.Sell("iron.ore", long.MaxValue), RunReasons.StockInsufficient, sink);
            AssertRejectedWithoutChange(rig, () => rig.Commands.Sell("iron.bar", 1), RunReasons.StockInsufficient, sink);
            AssertRejectedWithoutChange(rig, () => rig.Commands.SellAll("iron.gear"), RunReasons.ResourceLocked, sink);
            AssertRejectedWithoutChange(rig, () => rig.Commands.SellAll("iron.bar"), RunReasons.StockInsufficient, sink);
            AssertRejectedWithoutChange(rig, () => rig.Commands.SellAll(null), RunReasons.ResourceLocked, sink);

            rig.Session.SetLifecycleBlocked(true);
            AssertRejectedWithoutChange(rig, () => rig.Commands.Sell("iron.ore", 1), RunReasons.LifecycleBlocked, sink);
            AssertRejectedWithoutChange(rig, () => rig.Commands.SellAll("iron.ore"), RunReasons.LifecycleBlocked, sink);
        }

        [Test]
        public void ValidationOrder_LifecycleThenLockedThenQuantityThenStockThenValueLimit()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(cash: Bound, lockedResources: new[] { "iron.gear" });

            // Every check fails at once: the first one in the order wins.
            rig.Session.SetLifecycleBlocked(true);
            Assert.That(rig.Commands.Sell("iron.gear", 0).ReasonCode, Is.EqualTo(RunReasons.LifecycleBlocked));
            rig.Session.SetLifecycleBlocked(false);
            Assert.That(rig.Commands.Sell("iron.gear", 0).ReasonCode, Is.EqualTo(RunReasons.ResourceLocked));
            Assert.That(rig.Commands.Sell("iron.ore", 0).ReasonCode, Is.EqualTo(RunReasons.SellQuantityInvalid), "quantity before stock");
            Assert.That(rig.Commands.Sell("iron.ore", 1).ReasonCode, Is.EqualTo(RunReasons.StockInsufficient), "stock before the cash bound (Cash is at the bound)");
            RunSimHarness.Stock(rig.State, "iron.ore", 1);
            Assert.That(rig.Commands.Sell("iron.ore", 1).ReasonCode, Is.EqualTo(RunReasons.ValueLimit));
        }

        // ---- the sale ------------------------------------------------------------------------------------------

        [Test]
        public void Sell_RemovesUnitsAndAddsTheSalePricingValue()
        {
            var sink = new SaleSink();
            RunCommandFixtures.Rig rig = WithSink(RunCommandFixtures.NewRig(PriceOptions(1_250_000), cash: 100), sink);
            Assert.That(rig.State.TrySetResearchLevel(Price, 1), Is.EqualTo(RunStateResult.Ok));
            RunSimHarness.Stock(rig.State, "iron.plate", 7);
            long quantityRevision = rig.Session.QuantityRevision;

            RunCommandResult<SellResult> sold = rig.Commands.Sell("iron.plate", 3);

            // 3 plates at 8, x 1.25: floor(24 x 1.25) = 30.
            Assert.That(sold.IsSuccess, Is.True, sold.ReasonCode);
            Assert.That(sold.Value.ResourceId, Is.EqualTo("iron.plate"));
            Assert.That(sold.Value.Units, Is.EqualTo(3));
            Assert.That(sold.Value.CashEarned, Is.EqualTo(30));
            Assert.That(sold.Value.CashAfter, Is.EqualTo(130));
            Assert.That(rig.State.Cash, Is.EqualTo(130));
            Assert.That(rig.State.Inventory.Units("iron.plate"), Is.EqualTo(4));
            Assert.That(rig.Session.QuantityRevision, Is.GreaterThan(quantityRevision));
            Assert.That(sink.Sales.Count, Is.EqualTo(1));
            Assert.That(sink.Sales[0].ResourceId, Is.EqualTo("iron.plate"));
            Assert.That(sink.Sales[0].Units, Is.EqualTo(3));
            Assert.That(sink.Sales[0].Cash, Is.EqualTo(30));
            Assert.That(sink.Sales[0].Manual, Is.True);
            Assert.That(sink.Others, Is.EqualTo(0));
        }

        [Test]
        public void SellAll_TakesTheStockAtTheCommandInstant()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(cash: 10);
            RunSimHarness.Stock(rig.State, "iron.bar", 9);
            RunCommandResult<SellResult> sold = rig.Commands.SellAll("iron.bar");
            Assert.That(sold.IsSuccess, Is.True, sold.ReasonCode);
            Assert.That(sold.Value.Units, Is.EqualTo(9));
            Assert.That(sold.Value.CashEarned, Is.EqualTo(27));
            Assert.That(rig.State.Cash, Is.EqualTo(37));
            Assert.That(rig.State.Inventory.Units("iron.bar"), Is.EqualTo(0));
            Assert.That(rig.Commands.SellAll("iron.bar").ReasonCode, Is.EqualTo(RunReasons.StockInsufficient), "a second SellAll finds no stock");
        }

        [Test]
        public void Sell_IsDeterministic()
        {
            string Run()
            {
                RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(PriceOptions(1_333_333), cash: 5);
                rig.State.TrySetResearchLevel(Price, 2);
                RunSimHarness.Stock(rig.State, "iron.gear", 40);
                rig.Commands.Sell("iron.gear", 7);
                rig.Commands.Sell("iron.gear", 13);
                rig.Commands.SellAll("iron.gear");
                return RunCommandFixtures.Snapshot(rig.State);
            }

            Assert.That(Run(), Is.EqualTo(Run()));
        }

        // ---- Factory Value (FR-002, SC-003) --------------------------------------------------------------------

        [TestCase(1_500_000L, 0)]
        [TestCase(1_250_000L, 1)]
        [TestCase(1_333_333L, 2)]
        [TestCase(1_777_777L, 2)]
        public void SellAll_ChangesFactoryValueByZeroAtAnyPrice(long ppm, int levels)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(PriceOptions(ppm), cash: 1234);
            Assert.That(rig.State.TrySetResearchLevel(Price, levels), Is.EqualTo(RunStateResult.Ok));
            RunSimHarness.Stock(rig.State, "iron.ore", 77);
            RunSimHarness.Stock(rig.State, "iron.bar", 31);
            RunSimHarness.Stock(rig.State, "iron.plate", 13);
            RunSimHarness.Stock(rig.State, "iron.gear", 7);
            foreach (string id in new[] { "iron.ore", "iron.bar", "iron.plate", "iron.gear" })
            {
                long before = Value(rig);
                Assert.That(rig.Commands.SellAll(id).IsSuccess, Is.True, id);
                Assert.That(Value(rig), Is.EqualTo(before), id + ": selling the full stock is neutral");
            }
        }

        [TestCase(1_250_000L, 1)]
        [TestCase(1_333_333L, 2)]
        [TestCase(1_777_777L, 2)]
        public void NoSaleRaisesFactoryValue(long ppm, int levels)
        {
            var random = new Random(317);
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(PriceOptions(ppm), cash: 100);
            Assert.That(rig.State.TrySetResearchLevel(Price, levels), Is.EqualTo(RunStateResult.Ok));
            string[] ids = { "iron.ore", "iron.bar", "iron.plate", "iron.gear" };
            for (int i = 0; i < 300; i++)
            {
                string id = ids[random.Next(ids.Length)];
                if (rig.State.Inventory.Units(id) < 40) RunSimHarness.Stock(rig.State, id, 100);
                long before = Value(rig);
                long quantity = random.Next(1, (int)Math.Min(60, rig.State.Inventory.Units(id)) + 1);
                Assert.That(rig.Commands.Sell(id, quantity).IsSuccess, Is.True);
                Assert.That(Value(rig), Is.LessThanOrEqualTo(before), "sale " + i + " of " + quantity + " " + id);
            }
        }

        // ---- QA-5: selling while a full store blocks a machine --------------------------------------------------

        [Test]
        public void Sell_WhileTheStoreIsFullFreesRoomAndTheBlockedMachineResumes()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(cash: 1000);
            Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            long capacity = rig.State.Inventory.Capacity;
            RunSimHarness.Stock(rig.State, "iron.ore", capacity - rig.State.Inventory.Total);
            rig.Advance(200_000_000);
            Assert.That(rig.State.Inventory.Total, Is.EqualTo(capacity), "the store is full and the Miner is blocked");
            long cashBefore = rig.State.Cash;

            RunCommandResult<SellResult> sold = rig.Commands.Sell("iron.ore", 100);
            Assert.That(sold.IsSuccess, Is.True, sold.ReasonCode);
            Assert.That(rig.State.Cash, Is.EqualTo(cashBefore + 100));
            Assert.That(rig.State.Inventory.Total, Is.LessThan(capacity), "the sale freed room");

            rig.Advance(1_000_000_000);
            Assert.That(rig.State.Inventory.Total, Is.EqualTo(capacity), "the Miner refilled the room the sale made");
        }

        [Test]
        public void NoSaleRaisesFactoryValue_WithAutoSellOnAndAMachineBlockedOnAFullStore()
        {
            var random = new Random(3170);
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(PriceOptions(1_250_000), cash: 1000);
            Assert.That(rig.State.TrySetResearchLevel(Price, 1), Is.EqualTo(RunStateResult.Ok));
            Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            Assert.That(rig.Commands.SetAutoSell("iron.gear", true).IsSuccess, Is.True);
            string[] ids = { "iron.bar", "iron.plate" }; // gear has auto-sell on and no stock: it takes part in the settle pre-check without selling
            foreach (string id in ids) RunSimHarness.Stock(rig.State, id, 100);
            RunSimHarness.Stock(rig.State, "iron.ore", rig.State.Inventory.Capacity - rig.State.Inventory.Total);
            rig.Advance(100_000_000);
            Assert.That(rig.State.Inventory.Free, Is.EqualTo(0), "the store is full and the Miner is blocked");

            int sold = 0;
            int completions = 0;
            for (int i = 0; i < 200; i++)
            {
                string id = ids[random.Next(ids.Length)];
                long stock = rig.State.Inventory.Units(id);
                if (stock < 1) continue;
                long before = Value(rig);
                long oreBefore = rig.State.Inventory.Units("iron.ore");
                RunCommandResult<SellResult> result = rig.Commands.Sell(id, random.Next(1, (int)Math.Min(20, stock) + 1));
                Assert.That(result.IsSuccess, Is.True, result.ReasonCode);
                Assert.That(result.Value.CashAfter, Is.EqualTo(rig.State.Cash));

                // Only the Miner changes the ore stock, and only when the sale freed room for its waiting cycle (no time
                // passes inside a command). Factory Value may rise by exactly the value of those produced units, never more.
                long produced = rig.State.Inventory.Units("iron.ore") - oreBefore;
                Assert.That(produced, Is.GreaterThanOrEqualTo(0));
                Assert.That(SalePricing.TryValue(produced, rig.Catalog.Resources["iron.ore"].BasePrice, 1_250_000, out long producedValue), Is.True);
                if (produced > 0) completions++;
                Assert.That(Value(rig), Is.LessThanOrEqualTo(before + producedValue), "sale " + i + " of " + id + ", produced " + produced);
                if (produced == 0) Assert.That(Value(rig), Is.LessThanOrEqualTo(before), "sale " + i + " of " + id + " freed no room for the Miner");
                sold++;
            }

            Assert.That(sold, Is.GreaterThan(20));
            Assert.That(completions, Is.LessThanOrEqualTo(1), "a blocked Miner can complete its waiting cycle at most once, on the first sale that frees room; no time passes inside a command");
        }

        [TestCase(1_000_000L, 0)]
        [TestCase(1_250_000L, 1)]
        [TestCase(1_333_333L, 2)]
        [TestCase(1_777_777L, 2)]
        public void SplitSale_NeverPaysMoreThanOneSale(long ppm, int levels)
        {
            var random = new Random(3171);
            RunCommandFixtures.Options options = PriceOptions(ppm == 1_000_000L ? 1_500_000L : ppm);
            string[] ids = { "iron.ore", "iron.bar", "iron.plate", "iron.gear" };
            for (int i = 0; i < 200; i++)
            {
                string id = ids[random.Next(ids.Length)];
                long q1 = random.Next(1, 40);
                long q2 = random.Next(1, 40);
                RunCommandFixtures.Rig split = RunCommandFixtures.NewRig(options, cash: 5);
                RunCommandFixtures.Rig whole = RunCommandFixtures.NewRig(PriceOptions(ppm == 1_000_000L ? 1_500_000L : ppm), cash: 5);
                foreach (RunCommandFixtures.Rig rig in new[] { split, whole })
                {
                    Assert.That(rig.State.TrySetResearchLevel(Price, levels), Is.EqualTo(RunStateResult.Ok));
                    RunSimHarness.Stock(rig.State, id, q1 + q2);
                }

                Assert.That(split.Commands.Sell(id, q1).IsSuccess, Is.True);
                Assert.That(split.Commands.Sell(id, q2).IsSuccess, Is.True);
                Assert.That(whole.Commands.Sell(id, q1 + q2).IsSuccess, Is.True);
                Assert.That(split.State.Cash, Is.LessThanOrEqualTo(whole.State.Cash), id + " " + q1 + "+" + q2 + " at " + ppm);
                Assert.That(whole.State.Cash - split.State.Cash, Is.LessThanOrEqualTo(1), "each split sale floors at most once more");
                Assert.That(split.State.Inventory.Units(id), Is.EqualTo(whole.State.Inventory.Units(id)));
            }
        }

        [Test]
        public void CashAfter_IsReadAfterTheSettle_ForSellAndSellAll()
        {
            // Auto-sell is on for ore, and ore was put into stock directly: the settle after the sale sells what is left.
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(cash: 100);
            Assert.That(rig.Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            Assert.That(rig.Commands.SetAutoSell("iron.bar", true).IsSuccess, Is.True);
            RunSimHarness.Stock(rig.State, "iron.ore", 5);
            RunCommandResult<SellResult> sell = rig.Commands.Sell("iron.ore", 2);
            Assert.That(sell.IsSuccess, Is.True, sell.ReasonCode);
            Assert.That(sell.Value.CashEarned, Is.EqualTo(2));
            Assert.That(rig.State.Cash, Is.EqualTo(105), "2 by hand, 3 auto-sold in the settle");
            Assert.That(sell.Value.CashAfter, Is.EqualTo(rig.State.Cash));

            RunSimHarness.Stock(rig.State, "iron.plate", 4);
            RunSimHarness.Stock(rig.State, "iron.bar", 6);
            RunCommandResult<SellResult> all = rig.Commands.SellAll("iron.plate");
            Assert.That(all.IsSuccess, Is.True, all.ReasonCode);
            Assert.That(rig.State.Cash, Is.EqualTo(105 + 32 + 18), "the plates by hand, the bars auto-sold in the settle");
            Assert.That(all.Value.CashAfter, Is.EqualTo(rig.State.Cash));
        }

        [Test]
        public void EconomySnapshot_PreviewShowsLifecycleBlockedFirst_AndTheCommandAgrees()
        {
            RunCommandFixtures.Rig rig = StepRig(lockedResources: new[] { "iron.gear" });
            RunSimHarness.Stock(rig.State, "iron.plate", 15);
            rig.Session.SetLifecycleBlocked(true);

            EconomySnapshot economy = RunSnapshots.Economy(rig.Session);
            foreach (ResourceEconomy resource in economy.Resources)
            {
                Assert.That(resource.SellPreviews.Count, Is.EqualTo(4));
                foreach (SellPreviewEntry preview in resource.SellPreviews)
                {
                    Assert.That(preview.IsAvailable, Is.False);
                    Assert.That(preview.UnavailableReason, Is.EqualTo(RunReasons.LifecycleBlocked), resource.ResourceId + " step " + preview.Step);
                }
            }

            Assert.That(rig.Commands.Sell("iron.plate", 1).ReasonCode, Is.EqualTo(RunReasons.LifecycleBlocked));
            Assert.That(rig.Commands.SellAll("iron.plate").ReasonCode, Is.EqualTo(RunReasons.LifecycleBlocked));

            rig.Session.SetLifecycleBlocked(false);
            Assert.That(RunSnapshots.Economy(rig.Session).Resources.Single(r => r.ResourceId == "iron.plate").SellPreview(10).IsAvailable, Is.True);
        }

        // ---- QA-6: 10^18 boundaries ---------------------------------------------------------------------------

        [Test]
        public void Sell_AtTheCashBound_MinusOneEqualsAndPlusOne()
        {
            // One ore sells for 1.
            RunCommandFixtures.Rig below = RunCommandFixtures.NewRig(cash: Bound - 2);
            RunSimHarness.Stock(below.State, "iron.ore", 2);
            Assert.That(below.Commands.Sell("iron.ore", 1).IsSuccess, Is.True, "Cash 10^18 - 1 after the sale");
            Assert.That(below.State.Cash, Is.EqualTo(Bound - 1));

            RunCommandFixtures.Rig exact = RunCommandFixtures.NewRig(cash: Bound - 1);
            RunSimHarness.Stock(exact.State, "iron.ore", 2);
            Assert.That(exact.Commands.Sell("iron.ore", 1).IsSuccess, Is.True, "Cash exactly 10^18 after the sale");
            Assert.That(exact.State.Cash, Is.EqualTo(Bound));

            RunCommandFixtures.Rig over = RunCommandFixtures.NewRig(cash: Bound - 1);
            RunSimHarness.Stock(over.State, "iron.ore", 2);
            AssertRejectedWithoutChange(over, () => over.Commands.Sell("iron.ore", 2), RunReasons.ValueLimit);
            AssertRejectedWithoutChange(over, () => over.Commands.SellAll("iron.ore"), RunReasons.ValueLimit);

            RunCommandFixtures.Rig full = RunCommandFixtures.NewRig(cash: Bound);
            RunSimHarness.Stock(full.State, "iron.ore", 1);
            AssertRejectedWithoutChange(full, () => full.Commands.Sell("iron.ore", 1), RunReasons.ValueLimit);
        }

        [Test]
        public void Sell_BoundaryHoldsForABulkSaleWithAHigherPrice()
        {
            // 3 gears at 24 = 72.
            foreach ((long cash, bool ok) in new[] { (Bound - 73, true), (Bound - 72, true), (Bound - 71, false) })
            {
                RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(cash: cash);
                RunSimHarness.Stock(rig.State, "iron.gear", 3);
                RunCommandResult<SellResult> result = rig.Commands.Sell("iron.gear", 3);
                Assert.That(result.IsSuccess, Is.EqualTo(ok), "cash " + cash);
                if (!ok) Assert.That(result.ReasonCode, Is.EqualTo(RunReasons.ValueLimit));
            }
        }

        [Test]
        public void SettlePreCheck_SubtractsTheSoldStock()
        {
            // Auto-sell is on for ore, and the stock was put there directly, so the settle after the command sells
            // whatever ore is left. The pre-check must count only the ore that stays.
            RunCommandFixtures.Rig fits = RunCommandFixtures.NewRig(cash: Bound - 3);
            Assert.That(fits.Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            RunSimHarness.Stock(fits.State, "iron.ore", 3);
            RunCommandResult<SellResult> sold = fits.Commands.Sell("iron.ore", 2);
            Assert.That(sold.IsSuccess, Is.True, sold.ReasonCode);
            Assert.That(fits.State.Cash, Is.EqualTo(Bound), "2 sold by hand, the last one auto-sold in the settle");
            Assert.That(fits.State.Inventory.Units("iron.ore"), Is.EqualTo(0));

            RunCommandFixtures.Rig overflows = RunCommandFixtures.NewRig(cash: Bound - 2);
            Assert.That(overflows.Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            RunSimHarness.Stock(overflows.State, "iron.ore", 3);
            AssertRejectedWithoutChange(overflows, () => overflows.Commands.Sell("iron.ore", 1), RunReasons.ValueLimit);
            Assert.That(overflows.State.Inventory.Units("iron.ore"), Is.EqualTo(3));
        }

        // ---- the session log ----------------------------------------------------------------------------------

        [Test]
        public void SaleEvent_IsRaisedOnlyThroughTheGuardAndNeverChangesTheResult()
        {
            string Run(IRunEventSink sink, out int dropped)
            {
                RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(cash: 10);
                if (sink != null) WithSink(rig, sink);
                RunSimHarness.Stock(rig.State, "iron.plate", 10);
                Assert.That(rig.Commands.Sell("iron.plate", 4).IsSuccess, Is.True);
                Assert.That(rig.Commands.SellAll("iron.plate").IsSuccess, Is.True);
                dropped = rig.Session.DroppedEvents;
                return RunCommandFixtures.Snapshot(rig.State);
            }

            string plain = Run(null, out int none);
            string recorded = Run(new SaleSink(), out int ok);
            string thrown = Run(new SaleSink { Throw = true }, out int dropped);

            Assert.That(recorded, Is.EqualTo(plain));
            Assert.That(thrown, Is.EqualTo(plain), "a failing sink changes neither state nor result");
            Assert.That(none + ok, Is.EqualTo(0));
            Assert.That(dropped, Is.EqualTo(2), "both lost events are counted");
        }

        // ---- the Economy snapshot ---------------------------------------------------------------------------------

        private static RunCommandFixtures.Rig StepRig(long cash = 100, string[] lockedResources = null)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(cash: cash, lockedResources: lockedResources);
            RunStartDef old = rig.Catalog.RunStart;
            RunCatalog catalog = TestCatalogBuilder.From(rig.Catalog)
                .WithRunStart(new RunStartDef(old.StartingCash, old.InitialUnlocks, old.RemovalRefundPpm, old.MinCycleTicks,
                    old.MaxCombinedSpeedPpm, old.MaxCombinedOutputFactor, old.MaxRewardRatioPpm, new long[] { 1, 10, 100 }, true)).Build();
            var unlocked = new List<string>(rig.State.Unlocked);
            var state = new RunState(catalog, RunSimHarness.Id(9000), 1, null, cash, 0, 1, 1, unlocked);
            rig.Catalog = catalog;
            rig.State = state;
            rig.Session = new RunSession(catalog, state, new PermanentState(catalog), RunCommandFixtures.StartUtcMs);
            rig.Commands = new RunCommands(rig.Session, () => RunSimHarness.Id(rig.NextId++));
            return rig;
        }

        [Test]
        public void EconomySnapshot_CarriesTheSellStepsAndAPreviewPerStep()
        {
            RunCommandFixtures.Rig rig = StepRig();
            RunSimHarness.Stock(rig.State, "iron.plate", 15);

            EconomySnapshot economy = RunSnapshots.Economy(rig.Session);
            Assert.That(economy.SellSteps, Is.EqualTo(new long[] { 1, 10, 100 }));
            Assert.That(economy.HasSellAll, Is.True);
            ResourceEconomy plate = economy.Resources.Single(r => r.ResourceId == "iron.plate");
            Assert.That(plate.SellPreviews.Select(p => p.Step), Is.EqualTo(new long[] { 1, 10, 100, ResourceEconomy.SellAllStep }));

            Assert.That(plate.SellPreview(1).IsAvailable, Is.True);
            Assert.That(plate.SellPreview(1).Units, Is.EqualTo(1));
            Assert.That(plate.SellPreview(1).Cash, Is.EqualTo(8));
            Assert.That(plate.SellPreview(10).Cash, Is.EqualTo(80));
            Assert.That(plate.SellPreview(100).IsAvailable, Is.False);
            Assert.That(plate.SellPreview(100).UnavailableReason, Is.EqualTo(RunReasons.StockInsufficient));
            Assert.That(plate.SellPreview(100).Units, Is.EqualTo(100));
            Assert.That(plate.SellPreview(ResourceEconomy.SellAllStep).Units, Is.EqualTo(15));
            Assert.That(plate.SellPreview(ResourceEconomy.SellAllStep).Cash, Is.EqualTo(120));
            Assert.That(plate.SellPreview(7).UnavailableReason, Is.EqualTo(RunReasons.CommandInvalid), "a step the catalog does not offer");
        }

        [Test]
        public void EconomySnapshot_PreviewReasonsMatchTheCommand()
        {
            RunCommandFixtures.Rig rig = StepRig(cash: Bound - 5, lockedResources: new[] { "iron.gear" });
            RunSimHarness.Stock(rig.State, "iron.ore", 8);

            ResourceEconomy gear = RunSnapshots.Economy(rig.Session).Resources.Single(r => r.ResourceId == "iron.gear");
            Assert.That(gear.SellPreview(1).UnavailableReason, Is.EqualTo(RunReasons.ResourceLocked));
            Assert.That(gear.SellPreview(ResourceEconomy.SellAllStep).UnavailableReason, Is.EqualTo(RunReasons.ResourceLocked));

            ResourceEconomy bar = RunSnapshots.Economy(rig.Session).Resources.Single(r => r.ResourceId == "iron.bar");
            Assert.That(bar.SellPreview(ResourceEconomy.SellAllStep).UnavailableReason, Is.EqualTo(RunReasons.StockInsufficient), "All of an empty stock");
            Assert.That(bar.SellPreview(ResourceEconomy.SellAllStep).Units, Is.EqualTo(0));

            // Cash 10^18 - 5: 5 ore fit, 8 do not.
            ResourceEconomy ore = RunSnapshots.Economy(rig.Session).Resources.Single(r => r.ResourceId == "iron.ore");
            Assert.That(ore.SellPreview(1).IsAvailable, Is.True);
            Assert.That(ore.SellPreview(10).UnavailableReason, Is.EqualTo(RunReasons.StockInsufficient));
            Assert.That(ore.SellPreview(ResourceEconomy.SellAllStep).UnavailableReason, Is.EqualTo(RunReasons.ValueLimit));
            Assert.That(ore.SellPreview(ResourceEconomy.SellAllStep).Cash, Is.EqualTo(8));
            Assert.That(rig.Commands.SellAll("iron.ore").ReasonCode, Is.EqualTo(RunReasons.ValueLimit));
            Assert.That(rig.Commands.Sell("iron.ore", 1).IsSuccess, Is.True);
        }

        [Test]
        public void EconomySnapshot_AvailablePreviewsPayWhatTheCommandPays()
        {
            foreach (long step in new long[] { 1, 10, ResourceEconomy.SellAllStep })
            {
                RunCommandFixtures.Rig rig = StepRig();
                RunSimHarness.Stock(rig.State, "iron.gear", 25);
                SellPreviewEntry preview = RunSnapshots.Economy(rig.Session).Resources.Single(r => r.ResourceId == "iron.gear").SellPreview(step);
                RunCommandResult<SellResult> result = step == ResourceEconomy.SellAllStep ? rig.Commands.SellAll("iron.gear") : rig.Commands.Sell("iron.gear", step);
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value.Units, Is.EqualTo(preview.Units));
                Assert.That(result.Value.CashEarned, Is.EqualTo(preview.Cash));
            }
        }

        [Test]
        public void EconomySnapshot_WithoutStepsInTheCatalogHasNone()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig();
            EconomySnapshot economy = RunSnapshots.Economy(rig.Session);
            Assert.That(economy.SellSteps, Is.Empty);
            Assert.That(economy.HasSellAll, Is.False);
            Assert.That(economy.Resources.All(r => r.SellPreviews.Count == 0), Is.True);
        }
    }
}
