using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Run.Content;
using IdleFactory.Tests.EditMode.Balance;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// Unit tests of reference policy r2 (T341, <c>balance-policy-r2.md</c>): the balanced mix, the held mining click, net-worth income, the Industry
    /// model, the contract rules (bonus offers, cooldowns, production objectives, auto-deliver) and the strategy scopes. They assert the rules of the
    /// document on the shipped content; no balance band.
    /// </summary>
    public sealed class PolicyR2Tests
    {
        private const string ContentPath = "Assets/IdleFactory/Content/Iron/iron-factory-content.json";

        private static RunCatalog _catalog;
        private static RunRecord _chainRun1;
        private static BalanceTraceRunner _chainRunner;

        private static RunCatalog Catalog()
        {
            if (_catalog != null) return _catalog;
            var result = GameplayContentDispatcher.Load(File.ReadAllBytes(ContentPath));
            Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Code + " " + d.Path)));
            return _catalog = result.RunCatalog;
        }

        private static long Stat(RunRecord rec, string key) => rec.Stats.TryGetValue(key, out long value) ? value : 0;

        /// <summary>The chain for the first 2,400 s of run 1 (offset 0), played once.</summary>
        private static RunRecord ChainRun1()
        {
            if (_chainRun1 != null) return _chainRun1;
            _chainRunner = new BalanceTraceRunner(Catalog(), Strategies.Chain.Create());
            return _chainRun1 = _chainRunner.PlayRun("r2", 1, false, -1, null, 0, 2_400);
        }

        // ---- the balanced mix (section 2.3) ---------------------------------------------------------------------

        [Test]
        public void TheBalancedMixFollowsTheRecipeRatesAndFillsEverySlot()
        {
            RunCatalog catalog = Catalog();
            // Level-1 rates: Miner 0.5 exec/s, Smelter 0.5, Press 0.25, Assembler 0.2. The Assembler is the terminal at 1 exec/s, so the counts
            // are 5 : 8 : 8 : 8 (assembler : press : smelter : miner), scaled to the slots with the largest fractional part first.
            Dictionary<string, int> twelve = PolicyMix.Targets(catalog, null, 12);
            Assert.That(twelve, Is.EqualTo(new Dictionary<string, int> { ["machine.assembler"] = 2, ["machine.press"] = 4, ["machine.smelter"] = 3, ["machine.miner"] = 3 }));
            foreach (int slots in new[] { 4, 6, 8, 10, 12 })
            {
                Dictionary<string, int> targets = PolicyMix.Targets(catalog, null, slots);
                Assert.That(targets.Values.Sum(), Is.EqualTo(slots), "slots " + slots);
                Assert.That(targets.Keys, Is.EquivalentTo(catalog.Machines.Keys), "every type is in the mix from " + slots + " slots");
                Assert.That(targets.Values.All(v => v >= 1), Is.True);
            }
            Assert.That(PolicyMix.Targets(catalog, null, 4).Values, Is.All.EqualTo(1));
            Assert.That(PolicyMix.Targets(catalog, null, 12), Is.SameAs(PolicyMix.Targets(catalog, null, 12)), "memoised");
        }

        [Test]
        public void TheMixOfAScopeUsesTheTerminalOfThatScope()
        {
            RunCatalog catalog = Catalog();
            Assert.That(PolicyMix.Targets(catalog, ReferencePolicy.StopAtBarScope, 12), Is.EqualTo(new Dictionary<string, int> { ["machine.smelter"] = 6, ["machine.miner"] = 6 }));
            Assert.That(PolicyMix.Targets(catalog, ReferencePolicy.StopAtPlateScope, 12),
                Is.EqualTo(new Dictionary<string, int> { ["machine.press"] = 4, ["machine.smelter"] = 4, ["machine.miner"] = 4 }));
            Assert.That(PolicyMix.Targets(catalog, new[] { "machine.miner" }, 12), Is.EqualTo(new Dictionary<string, int> { ["machine.miner"] = 12 }), "Ore-only is the scope of one type");
            Assert.That(PolicyMix.WalkOrder(catalog, null).Select(m => m.Id), Is.EqualTo(new[] { "machine.assembler", "machine.press", "machine.smelter", "machine.miner" }));
        }

        // ---- the held mining click (section 6) --------------------------------------------------------------------

        private static RunCatalog WithStartCash(long cash)
        {
            RunCatalog c = Catalog();
            RunStartDef s = c.RunStart;
            var start = new RunStartDef(cash, s.InitialUnlocks, s.RemovalRefundPpm, s.MinCycleTicks, s.MaxCombinedSpeedPpm, s.MaxCombinedOutputFactor, s.MaxRewardRatioPpm,
                s.SellSteps, s.SellAll, s.LiveGapThresholdMs, s.ManualMining, s.MinRunDurationMs, s.UptimeMaxPpm);
            return IdleFactory.Tests.EditMode.Run.TestCatalogBuilder.From(c).WithRunStart(start).Build();
        }

        [Test]
        public void EveryStrategyThatBuildsMinersMinesAtTheWholeCapUntilItsFirstMinerAndThenStops()
        {
            // With 10 Cash the first Miner ($30) must be mined for: 100 % of the cap (10 activations x 0.1 Ore = 1 Ore/s sold at $1).
            RunCatalog poor = WithStartCash(10);
            var runner = new BalanceTraceRunner(poor, Strategies.Chain.Create());
            RunRecord rec = runner.PlayRun("poor", 1, false, -1, null, 0, 600);
            Assert.That(runner.Context.Violations, Is.Empty);
            long calls = Stat(rec, "mine_calls");
            Assert.That(calls, Is.InRange(15, 60), "about 20 s of mining until $30; the look grid places the first Miner at the next look");
            Assert.That(runner.Context.Session.State.Machines.Any(m => m.MachineId == "machine.miner"), Is.True);

            // The shipped content starts with $37, so the first look builds the Miner and the click never runs in the chain.
            Assert.That(Stat(ChainRun1(), "mine_calls"), Is.EqualTo(0));
        }

        [Test]
        public void ChainPlusMiningClicksEveryActiveSecondFromTheFirstLookAndNeverInTheAwayWindow()
        {
            // 900 s with the away window at 240..299, 540..599 and 840..899: 720 active seconds. An offset of 8 s starts the click at the first look.
            foreach (int offset in new[] { 0, 8 })
            {
                var runner = new BalanceTraceRunner(Catalog(), Strategies.ChainPlusMining.Create());
                RunRecord rec = runner.PlayRun("click", 1, false, -1, null, offset, 900);
                long rejected = rec.Stats.Where(kv => kv.Key.StartsWith("rejected.mine", StringComparison.Ordinal)).Sum(kv => kv.Value);
                // The run ends at the first grid tick at or after 900 s: 900 s for offset 0, 908 s for offset 8.
                long end = offset + 20L * ((900 - offset + 19) / 20);
                long active = 0;
                for (long second = offset; second < end; second++) if (!ReferencePolicy.IsIdle(second)) active++;
                Assert.That(Stat(rec, "mine_calls") + rejected, Is.EqualTo(active), "offset " + offset);
                Assert.That(Stat(rec, "mine_counted"), Is.GreaterThan(0));
                Assert.That(runner.Context.Violations, Is.Empty);
            }
        }

        [Test]
        public void TheCasualClickIsTwentyFivePercentOfTheCapRoundedHalfUp()
        {
            var runner = new BalanceTraceRunner(Catalog(), Strategies.Casual.Create());
            RunRecord rec = runner.PlayRun("casual", 1, false, -1, null, 0, 900);
            long cap = Catalog().RunStart.ManualMining.MaxActivationsPerSecond;
            Assert.That(cap, Is.EqualTo(10));
            Assert.That(Stat(rec, "mine_counted"), Is.EqualTo(Stat(rec, "mine_calls") * 3), "round_half_up(0.25 x 10) = 3 activations per second");
            Assert.That(Stat(rec, "mine_calls"), Is.GreaterThan(500));
        }

        [Test]
        public void MiningOnlyBuildsNothingMinesAndSellsByAutoSell()
        {
            var runner = new BalanceTraceRunner(Catalog(), Strategies.MiningOnly.Create());
            RunRecord rec = runner.PlayRun("mine", 1, false, -1, null, 0, 900);
            Assert.That(runner.Context.Session.State.Machines, Is.Empty);
            Assert.That(runner.Context.Session.State.IsAutoSell("iron.ore"), Is.True);
            Assert.That(Stat(rec, "mine_ore_units"), Is.EqualTo(720), "1 Ore per active second");
            Assert.That(Stat(rec, "sales_cash"), Is.EqualTo(720));
            Assert.That(runner.Context.Violations, Is.Empty);
        }

        // ---- income as net-worth growth (section 3) ------------------------------------------------------------

        [Test]
        public void ObservedIncomeIsNetWorthGrowthNotCashAndASaleDoesNotChangeIt()
        {
            var runner = new BalanceTraceRunner(Catalog(), Strategies.Chain.Create());
            runner.PlayRun("nw", 1, false, -1, null, 0, 700);
            TraceContext c = runner.Context;
            Assert.That(c.ObservedIncome, Is.GreaterThanOrEqualTo(1m), "floored at 1");
            long before = c.NetWorth();
            long ore = c.Session.State.Inventory.Units("iron.ore");
            long ironBar = c.Session.State.Inventory.Units("iron.bar");
            Assert.That(ore + ironBar, Is.GreaterThan(1), "something to sell at 700 s");
            string id = ironBar > 1 ? "iron.bar" : "iron.ore";
            Assert.That(c.Sell(id, 1), Is.True);
            Assert.That(before - c.NetWorth(), Is.InRange(0, 1), "Sell moves Cash and stock but not net worth (FR-002, single floor)");
            Assert.That(c.Violations, Is.Empty);
        }

        // ---- the Industry model (section 5) ------------------------------------------------------------------------

        private static Dictionary<string, long> Levels(params string[] ids)
        {
            var levels = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (string id in Catalog().IndustryUpgrades.Keys) levels[id] = 0;
            foreach (string id in ids) levels[id]++;
            return levels;
        }

        [Test]
        public void TheFluidModelIsDeterministicMemoisedAndValuesTheLevelsInTheExpectedDirection()
        {
            RunCatalog catalog = Catalog();
            IndustryModelOptions options = new ReferencePolicy().ModelOptions(catalog);
            decimal baseline = IndustryModel.TimeToThreshold(catalog, Levels(), 0, options);
            long evaluations = IndustryModel.Evaluations;
            Assert.That(IndustryModel.TimeToThreshold(catalog, Levels(), 0, options), Is.EqualTo(baseline), "deterministic");
            Assert.That(IndustryModel.Evaluations, Is.EqualTo(evaluations), "memoised on (levels, prestige count, catalog, options)");
            Assert.That(baseline, Is.InRange(1_000m, IndustryModel.LimitSeconds), "the model reaches the first threshold");

            foreach (string id in new[] { "industry.calibration", "industry.market-contacts", "industry.toolmaker", "industry.seed-capital" })
                Assert.That(IndustryModel.TimeToThreshold(catalog, Levels(id), 1, options), Is.LessThan(IndustryModel.TimeToThreshold(catalog, Levels(), 1, options)), id + " shortens the time");
            Assert.That(IndustryModel.TimeToThreshold(catalog, Levels("industry.warehouse"), 1, options),
                Is.EqualTo(IndustryModel.TimeToThreshold(catalog, Levels(), 1, options)), "capacity has no effect in a model without a storage limit (gain 0)");
        }

        [Test]
        public void IpIsSpentByGainPerIpAndALevelWithoutGainIsNeverBought()
        {
            var runner = new BalanceTraceRunner(Catalog(), Strategies.Chain.Create());
            RunRecord run = runner.PlayRun("ip", 1, true, 0);
            Assert.That(run.PrestigedAtSec, Is.GreaterThan(0));
            Assert.That(runner.SpendIndustry(), Is.GreaterThan(0));
            Assert.That(runner.Context.Session.Permanent.IndustryLevel("industry.warehouse"), Is.EqualTo(0), "gain 0: never bought");
            Assert.That(runner.Context.Session.Permanent.Ip, Is.EqualTo(Stat(run, "unspent_ip")), "the unspent IP is reported");
            long spent = Stat(run, "industry_ip_spent");
            Assert.That(spent + runner.Context.Session.Permanent.Ip, Is.EqualTo(run.IpAwarded), "no IP is lost: spent plus kept is awarded");
            Assert.That(runner.Context.Violations, Is.Empty);
        }

        // ---- contracts: bonus offers, cooldowns, production objectives, auto-deliver (sections 4.1, 4.2) ---------------

        [Test]
        public void ThePolicyTakesTheBonusOfferTheProductionObjectiveAndAutoDeliversWithoutAnyRejection()
        {
            RunRecord rec = ChainRun1();
            Assert.That(Stat(rec, "delivered.contract.bonus-plate"), Is.EqualTo(1), "the bonus offer is used once, inside its window");
            Assert.That(Stat(rec, "produced.contract.produce-plate"), Is.GreaterThan(0), "a production objective is accepted and completed by production");
            Assert.That(Stat(rec, "deliveries_auto"), Is.GreaterThan(0));
            Assert.That(Stat(rec, "deliveries_manual"), Is.EqualTo(0), "with auto-deliver on the manual delivery is a fallback only");
            Assert.That(Stat(rec, "auto_deliver_on"), Is.GreaterThanOrEqualTo(Stat(rec, "deliveries_auto")), "auto-deliver is switched on for every accepted delivery");
            Assert.That(rec.Stats.Keys.Where(k => k.StartsWith("rejected.", StringComparison.Ordinal)), Is.Empty, "the policy issues no command the engine rejects");
            Assert.That(_chainRunner.Context.Violations, Is.Empty);
        }

        [Test]
        public void TheContractLeverHoldsConsumersOffAndReportsTheMachineSecondsItCost()
        {
            RunRecord rec = ChainRun1();
            Assert.That(Stat(rec, "held_off_machine_seconds"), Is.GreaterThan(0), "a Plate contract switches the Assemblers off while it runs");
            Assert.That(Stat(rec, "surplus_dumps"), Is.GreaterThan(0), "a kept resource at 25 % of the store is sold down with Sell");
            Assert.That(Stat(rec, "manual_sells"), Is.EqualTo(Stat(rec, "surplus_dumps")));
        }

        [Test]
        public void StorageIsBoughtOnAFullStoreSeenOnArrival()
        {
            RunRecord rec = ChainRun1();
            Assert.That(Stat(rec, "storage_upgrades"), Is.GreaterThan(0), "a blocker seen on arrival, before the look's own Sell clears it, buys storage");
            Assert.That(Stat(rec, "store_blocked_samples") * 100, Is.LessThan(Stat(rec, "machine_look_samples") * 15), "and the stores are not full most of the time");
        }

        [Test]
        public void ACoolingOfferDoesNotMakeTheEligibilityRiseWithItsCooldown()
        {
            // #466 (T363 QA): with policy r1 the run-1 eligibility was not monotone in the Plate cooldown (0 s: 6,520, 60 s: 6,360, 120 s: 11,160) because the
            // policy waited for the cooling offer. Policy r2 reads the cooldown as shown and takes another available offer, so the cooldown does not stall it.
            RunCatalog shipped = Catalog();
            var eligible = new Dictionary<long, long>();
            foreach (long cooldownMs in new long[] { 0, 60_000, 120_000, 300_000 })
            {
                var offers = new List<ContractOfferDef>();
                foreach (ContractOfferDef o in shipped.ContractOffers.Values)
                    offers.Add(o.Id != "contract.plate" ? o : new ContractOfferDef(o.Id, o.Kind, o.ResourceId, o.QuantityCurve, o.DurationMs, o.Rewards, o.Unlock,
                        o.Objective, o.Class, o.RewardRatioPpm, cooldownMs, o.Bonus));
                RunCatalog variant = IdleFactory.Tests.EditMode.Run.TestCatalogBuilder.From(shipped).WithContractOffers(offers).Build();
                var runner = new BalanceTraceRunner(variant, Strategies.Chain.Create());
                RunRecord rec = runner.PlayRun("cooldown", 1, false, 0);
                Assert.That(runner.Context.Violations, Is.Empty);
                Assert.That(rec.Stats.Keys.Where(k => k.StartsWith("rejected.accept", StringComparison.Ordinal)), Is.Empty, "the policy never accepts an offer that is cooling down");
                eligible[cooldownMs] = rec.EligibleAtSec;
            }
            foreach (long cooldownMs in new long[] { 60_000, 120_000, 300_000 })
                Assert.That(eligible[cooldownMs], Is.LessThanOrEqualTo(eligible[0] + 2 * ReferencePolicy.EpochSeconds), "cooldown " + cooldownMs / 1000 + " s");
        }

        [Test]
        public void AStopStrategyAcceptsOnlyTheOffersItCanFeed()
        {
            var bar = new BalanceTraceRunner(Catalog(), Strategies.StopAtBar.Create());
            RunRecord rec = bar.PlayRun("bar", 1, false, -1, null, 0, 1_500);
            Assert.That(Stat(rec, "delivered.contract.timed-bar"), Is.GreaterThan(0));
            Assert.That(Stat(rec, "delivered.contract.plate") + Stat(rec, "delivered.contract.gear") + Stat(rec, "produced.contract.produce-plate"), Is.EqualTo(0));
            Assert.That(bar.Context.Violations, Is.Empty);

            var market = new BalanceTraceRunner(Catalog(), Strategies.MarketOnlyChain.Create());
            RunRecord m = market.PlayRun("market", 1, false, -1, null, 0, 1_500);
            Assert.That(Stat(m, "contracts_accepted"), Is.EqualTo(0));
            Assert.That(Stat(m, "auto_deliver_on"), Is.EqualTo(0));
            Assert.That(market.Context.Violations, Is.Empty);
        }

        // ---- looks, idle windows and prestige later -------------------------------------------------------------

        [Test]
        public void AnIdleLookOnlyObservesAndIssuesNoCommand()
        {
            // Seconds 240..299 are away: the looks at 260 and 280 add an income sample but no command. A run that stops at 240 and one that
            // reaches 300 (stopping before its look at 300 acts) therefore issued the same number of commands.
            var a = new BalanceTraceRunner(Catalog(), Strategies.Chain.Create());
            var b = new BalanceTraceRunner(Catalog(), Strategies.Chain.Create());
            a.PlayRun("a", 1, false, -1, null, 0, 240);
            b.PlayRun("b", 1, false, -1, null, 0, 300);
            Assert.That(b.Context.Commands, Is.EqualTo(a.Context.Commands));
        }

        [Test]
        public void CasualRunTwoSlowdownOfPolicyR1IsTheStorageBlockerReadAfterTheSale()
        {
            // #373 finding 3: under policy r1 the casual run 2 was slower than run 1 (and later runs were not reached). Policy r1 ran its surplus dump
            // before its storage rule, so the storage rule read a store that the look's own sale had just emptied. At a 73 s look interval the stores
            // fill between two looks, the blocker is never seen, and the storage level stays at 1 while the faster machines stall on it. Policy r2
            // reads the blocker on arrival; the diagnostic switch reproduces the r1 order.
            StrategyHistory r2 = StrategyEnsemble.PlayHistory(Catalog(), Strategies.Casual, 0, 2);
            Assert.That(r2.Runs[1].EligibleAtSec, Is.LessThan(r2.Runs[0].EligibleAtSec), "policy r2: casual run 2 is faster than run 1");

            var r1Order = new StrategyDefinition("casual-r1-order", "diagnostic only", () => new ReferencePolicy
            {
                Id = "casual-r1-order",
                LookIntervalSeconds = ReferencePolicy.CasualLookIntervalSeconds,
                MiningAfterFirstMinerPpm = ReferencePolicy.CasualMiningFractionPpm,
                ReadStorageBlockerAfterSelling = true,
            });
            StrategyHistory r1 = StrategyEnsemble.PlayHistory(Catalog(), r1Order, 0, 2);
            RunRecord a = r2.Runs[0];
            RunRecord b = r1.Runs[0];
            Assert.That(Stat(b, "storage_upgrades"), Is.EqualTo(0), "no blocker is ever seen, so no storage is bought");
            Assert.That(Stat(a, "storage_upgrades"), Is.GreaterThan(0));
            Assert.That(Stat(b, "store_blocked_samples") * Stat(a, "machine_look_samples"), Is.GreaterThan(2 * Stat(a, "store_blocked_samples") * Stat(b, "machine_look_samples")),
                "machines stand still on a full store more than twice as often");
            Assert.That(b.EligibleAtSec * 2, Is.GreaterThan(a.EligibleAtSec * 3), "and run 1 takes more than 1.5 times as long");
        }

        [Test]
        public void PrestigeLaterWaitsForTheMultipleOfTheThresholdAndAwardsMoreIp()
        {
            var chain = new BalanceTraceRunner(Catalog(), Strategies.Chain.Create());
            var later = new BalanceTraceRunner(Catalog(), Strategies.PrestigeLaterLow.Create());
            RunRecord c = chain.PlayRun("c", 1, true, 0);
            RunRecord l = later.PlayRun("l", 1, true, 0);
            Assert.That(l.EligibleAtSec, Is.EqualTo(c.EligibleAtSec), "the time to threshold is the same, the prestige is later");
            Assert.That(l.PrestigedAtSec, Is.GreaterThan(c.PrestigedAtSec));
            Assert.That(l.IpAwarded, Is.GreaterThan(c.IpAwarded), "1.5 times the threshold awards more IP");
            Assert.That(l.IpAwarded, Is.GreaterThanOrEqualTo(12), "floor(10 x sqrt(1.5)) = 12");
            Assert.That(chain.Context.Violations, Is.Empty);
            Assert.That(later.Context.Violations, Is.Empty);
        }

        [Test]
        public void TheCumulativeTimeCountsEveryRunUpToThePrestige()
        {
            StrategyHistory h = StrategyEnsemble.PlayHistory(Catalog(), Strategies.Chain, 0, 2);
            Assert.That(h.CumulativeAt(1), Is.EqualTo(h.Runs[0].PrestigedAtSec));
            Assert.That(h.CumulativeAt(2), Is.EqualTo(h.Runs[0].PrestigedAtSec + h.Runs[1].PrestigedAtSec));
            Assert.That(h.CumulativeAt(3), Is.EqualTo(-1), "run 3 was not played");
        }
    }
}
