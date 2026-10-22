using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// Final-green gate for the initial content sheet (T097, issue #179):
    /// <c>Assets/IdleFactory/Content/Iron/iron-factory-content.json</c>, since T345 (issue #410) the tuned <c>contentRevision</c> 3 (revision 2 was T125; revision 1 is kept as history in
    /// content-record-r1.md), a balance hypothesis until the owner approves it (FR-027, T354). Since T313 (issue #375) it is a schema-version-3 document; the contract rewards
    /// come from <c>rewardRatioPpm</c> (since T320, which removed the transitional reward adapter). The document loads through <see cref="GameplayContentDispatcher"/>, equals its canonical
    /// rendering, carries the literal revision 3 numbers (the F003 numbers FR-002/FR-007/FR-008/FR-010/FR-012/FR-019/FR-021 where T345 did not retune them), resolves every reference
    /// and uses exactly the T090 presentation keys. The worst-case numbers the tuning record
    /// (<c>specs/005-economy-contracts/reviews/balance-trace-r3.md</c>, the figures of revision 2 were in <c>content-record-r2.md</c>) quotes are recomputed here from the loaded
    /// catalog, so the record cannot drift from the sheet.
    /// </summary>
    public sealed class RunContentCanonicalTests
    {
        private const string ContentPath = "Assets/IdleFactory/Content/Iron/iron-factory-content.json";
        private const string BriefPath = "docs/art/F003-asset-briefs-v0.1.md";
        private const string RecordPath = "specs/003-first-factory-prestige/reviews/content-record-r2.md";

        // The worst-case figures of the shipped values are quoted by the record of the revision that ships them. Revision 2 (F003, T127) quoted them in
        // content-record-r2.md; the tuned revision 3 (T345) quotes them in the tuning record, section "Worst-case figures of revision 3".
        private const string TuningRecordPath = "specs/005-economy-contracts/reviews/balance-trace-r3.md";
        private const long Ppm = 1_000_000L;

        private static byte[] _bytes;
        private static RunCatalog _catalog;

        private static byte[] Bytes() => _bytes ?? (_bytes = File.ReadAllBytes(ContentPath));

        private static string Text() => new UTF8Encoding(false, true).GetString(Bytes());

        private static RunCatalog Catalog()
        {
            if (_catalog == null)
            {
                var result = GameplayContentDispatcher.Load(Bytes());
                Assert.That(result.IsSuccess, Is.True, Describe(result.Diagnostics));
                _catalog = result.RunCatalog;
            }

            return _catalog;
        }

        private static string Describe(IEnumerable<ContentDiagnostic> diagnostics) =>
            string.Join("; ", diagnostics.Select(d => $"{d.Code} {d.Path}: {d.Message}"));

        // ---- load and canonical form ----------------------------------------------------------------------

        [Test]
        public void DocumentLoadsThroughTheDispatcherAsContentVersion3Revision3()
        {
            var result = GameplayContentDispatcher.Load(Bytes());

            Assert.That(result.IsSuccess, Is.True, Describe(result.Diagnostics));
            Assert.That(result.SchemaVersion, Is.EqualTo(3));
            Assert.That(result.RunCatalog, Is.Not.Null);
            Assert.That(result.LegacyCatalog, Is.Null);
            Assert.That(result.RunCatalog.ContentRevision, Is.EqualTo(3), "schema version 3 with the tuned revision 3 values of T345; the semantic F005 rules apply from revision 3");
        }

        [Test]
        public void DocumentIsFreeOfDiagnosticsWhenEveryPresentationKeyResolves()
        {
            var result = GameplayContentDispatcher.Load(Bytes(), new AllKeysResolve());

            Assert.That(result.IsSuccess, Is.True, Describe(result.Diagnostics));
            Assert.That(result.Diagnostics, Is.Empty);
        }

        [Test]
        public void DocumentEqualsItsCanonicalRendering()
        {
            var bytes = Bytes();
            var read = RunContentReader.Read(bytes);
            Assert.That(read.IsSuccess, Is.True, read.Diagnostic == null ? string.Empty : read.Diagnostic.Message);

            var actual = Text();
            var expected = CanonicalRenderer.Render(read.Document);

            Assert.That(bytes.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }), "UTF-8 BOM is forbidden.");
            Assert.That(actual, Does.Not.Contain("\r"), "LF line endings only.");
            Assert.That(actual, Does.EndWith("\n"));
            Assert.That(actual, Is.EqualTo(expected));
        }

        // ---- literal spec numbers -------------------------------------------------------------------------

        [Test]
        public void RecipesAndMachinesMatchFr002()
        {
            var catalog = Catalog();
            var miner = RecipeOf(catalog, "machine.miner");
            var smelter = RecipeOf(catalog, "machine.smelter");
            var press = RecipeOf(catalog, "machine.press");
            var assembler = RecipeOf(catalog, "machine.assembler");

            // Revision 3 (T345, chain.dominance): the three downstream cycles are 0.4 times the F003 values (2 s, 4 s, 5 s), so the output per slot-second
            // of Bar, Plate and Gear rises along the chain (1.07, 1.11, 1.46 against 1.00 for Ore). The amounts are the F003 amounts.
            AssertRecipe(miner, new (string, long)[0], new[] { ("iron.ore", 2L) }, 20_000_000L, "Miner 2 Ore / 2 s, no input");
            AssertRecipe(smelter, new[] { ("iron.ore", 2L) }, new[] { ("iron.bar", 1L) }, 8_000_000L, "Smelter 2 Ore -> 1 Bar / 0.8 s");
            AssertRecipe(press, new[] { ("iron.bar", 2L) }, new[] { ("iron.plate", 1L) }, 16_000_000L, "Press 2 Bars -> 1 Plate / 1.6 s");
            AssertRecipe(assembler, new[] { ("iron.plate", 2L) }, new[] { ("iron.gear", 1L) }, 20_000_000L, "Assembler 2 Plates -> 1 Gear / 2 s");
        }

        [Test]
        public void OnePressSupplies62Point5PercentOfOneAssemblerDemandDerivedFromTheRecipes()
        {
            var catalog = Catalog();
            var press = RecipeOf(catalog, "machine.press");
            var assembler = RecipeOf(catalog, "machine.assembler");
            var plate = "iron.plate";

            // Plates per tick: the Press produces out/Dp, the Assembler consumes in/Da. Supply / demand = (out x Da) / (Dp x in).
            var pressOut = press.Outputs.Single(o => o.ResourceId == plate).Amount;
            var assemblerIn = assembler.Inputs.Single(i => i.ResourceId == plate).Amount;
            var numerator = pressOut * assembler.DurationTicks;
            var denominator = press.DurationTicks * assemblerIn;

            Assert.That(numerator * 1000, Is.EqualTo(denominator * 625), "supply / demand must be exactly 62.5 %");
            Assert.That(numerator * 8, Is.EqualTo(denominator * 5));
        }

        [Test]
        public void MarketPricesMatchFr012()
        {
            var catalog = Catalog();

            Assert.That(catalog.Resources["iron.ore"].BasePrice, Is.EqualTo(1));
            Assert.That(catalog.Resources["iron.bar"].BasePrice, Is.EqualTo(3));
            Assert.That(catalog.Resources["iron.plate"].BasePrice, Is.EqualTo(8));
            Assert.That(catalog.Resources["iron.gear"].BasePrice, Is.EqualTo(24));
            Assert.That(catalog.Resources.Count, Is.EqualTo(4));
        }

        [Test]
        public void StorageAndFactoryLevelsMatchFr007AndFr008()
        {
            var catalog = Catalog();

            Assert.That(catalog.StorageLevels.Select(l => l.Capacity), Is.EqualTo(new long[] { 1_000, 2_000, 4_000, 8_000, 16_000 }));
            Assert.That(catalog.StorageLevels.Select(l => l.Cost), Is.EqualTo(new long[] { 0, 2_500, 10_000, 40_000, 160_000 }));
            Assert.That(catalog.FactoryLevels.Select(l => l.SlotCount), Is.EqualTo(new[] { 4, 6, 8, 10, 12 }));
            Assert.That(catalog.FactoryLevels[0].Cost, Is.EqualTo(0));
            Assert.That(catalog.FactoryLevels.Select(l => l.Cost), Is.Ordered.Ascending);
            Assert.That(catalog.FactoryLevels.Select(l => l.Cost).Distinct().Count(), Is.EqualTo(5), "slot costs strictly increase");
        }

        [Test]
        public void MachineCurvesAndMilestonesFollowFr010()
        {
            var catalog = Catalog();

            Assert.That(catalog.Machines.Count, Is.EqualTo(4));
            foreach (var machine in catalog.Machines.Values)
            {
                Assert.That(machine.MaxLevel, Is.EqualTo(100), machine.Id);
                Assert.That(machine.Cost.GrowthPpm, Is.EqualTo(1_180_000), machine.Id + " cost growth 1.18 (revision 3; F003 1.15)");
                Assert.That(machine.Cost.Rounding, Is.EqualTo("ceil"), machine.Id);
                Assert.That(machine.SpeedGrowthPpm, Is.EqualTo(1_060_000), machine.Id + " speed growth 1.06 (revision 3; F003 1.08); the same for every machine, T316");
                Assert.That(machine.Milestones.Select(m => (m.Level, m.SpeedPpm)),
                    Is.EqualTo(new[] { (10, 2_000_000L), (25, 2_000_000L), (50, 3_000_000L), (100, 5_000_000L) }), machine.Id);
                Assert.That(machine.SpeedTable[2], Is.GreaterThan(machine.SpeedTable[1]), machine.Id);
            }
        }

        [Test]
        public void RunStartAndOverdriveAndPrestigeFollowFr019AndFr021()
        {
            var catalog = Catalog();

            Assert.That(catalog.RunStart.RemovalRefundPpm, Is.EqualTo(500_000));
            Assert.That(catalog.RunStart.InitialUnlocks, Is.EquivalentTo(new[] { "iron.ore", "machine.miner" }));
            Assert.That(catalog.Overdrive.SpeedPpm, Is.EqualTo(2_000_000));
            Assert.That(catalog.Overdrive.DurationMs, Is.EqualTo(30_000));
            Assert.That(catalog.Overdrive.CooldownMs, Is.EqualTo(300_000));
            Assert.That(catalog.Prestige.ThresholdFactoryValue, Is.EqualTo(10_000_000));
            Assert.That(catalog.Prestige.Threshold.Base, Is.EqualTo(10_000_000), "the legacy threshold is T(0), the threshold base");
            Assert.That(catalog.Prestige.Threshold.GrowthPpm, Is.EqualTo(1_100_000), "revision 3 raises the threshold by 10 % per completed prestige (FR-019)");
            Assert.That(catalog.Prestige.Threshold.MaxIndex, Is.EqualTo(30), "and stays constant from index 30 on (D-K)");
            Assert.That(catalog.Prestige.CurveKind, Is.EqualTo("sqrt"));
            Assert.That(catalog.Prestige.Coefficient, Is.EqualTo(10));
            Assert.That(catalog.Prestige.Divisor, Is.EqualTo(10_000_000));

            AssertIp(catalog, 10_000_000L, 10);
            AssertIp(catalog, 40_000_000L, 20);
            AssertIp(catalog, 90_000_000L, 30);
            AssertIp(catalog, 1_000_000_000L, 100);
        }

        [Test]
        public void OfflinePolicyIsTheLockedRevision1()
        {
            var policy = Catalog().OfflinePolicy;

            Assert.That(policy.Revision, Is.EqualTo(1));
            Assert.That(policy.EfficiencyNumerator, Is.EqualTo(4));
            Assert.That(policy.EfficiencyDenominator, Is.EqualTo(5));
            Assert.That(policy.MaxCreditedTicks, Is.EqualTo(288_000_000_000L));
        }

        // ---- contracts, Items, research, Industry ----------------------------------------------------------

        [Test]
        public void ContractsKeepTheF003OfferIdsAndExpressTheF003RewardsAsRatiosAndAnItemLadder()
        {
            var catalog = Catalog();

            // The three F003 offers keep their IDs and shape. T363 added the revision 2 content slice on top of them (one cooldown on the Plate
            // offer, one bonus offer and one production objective, batch-A Play-Tester F2/F4); T345 tunes their ratios and cooldowns for revision 3.
            Assert.That(
                catalog.ContractOffers.Keys.OrderBy(k => k, StringComparer.Ordinal),
                Is.EqualTo(new[] { "contract.bonus-plate", "contract.gear", "contract.plate", "contract.produce-plate", "contract.timed-bar" }));
            foreach (var id in new[] { "contract.gear", "contract.plate", "contract.timed-bar" })
            {
                var offer = catalog.ContractOffers[id];
                Assert.That(offer.Objective, Is.EqualTo("deliver"), offer.Id);
                Assert.That(offer.Class, Is.EqualTo("standing"), offer.Id);
                Assert.That(offer.Bonus, Is.Null, offer.Id);
            }

            // Revision 3 (T345): every standing delivery offer has a cooldown (Gear and timed Bar 60 s, Plate 120 s), none exceeds the seven-day cap.
            Assert.That(catalog.ContractOffers["contract.gear"].CooldownMs, Is.EqualTo(60_000));
            Assert.That(catalog.ContractOffers["contract.timed-bar"].CooldownMs, Is.EqualTo(60_000));
            Assert.That(catalog.ContractOffers["contract.plate"].CooldownMs, Is.EqualTo(120_000));
            Assert.That(catalog.ContractOffers.Values.Count(o => o.Objective == "deliver" && o.Class == "standing" && o.CooldownMs > 0), Is.EqualTo(3));
            Assert.That(catalog.ContractOffers.Values.Select(o => o.CooldownMs), Has.All.InRange(0L, 604_800_000L), "owner decision 2026-10-21: cooldowns are at most seven days");

            // T363: exactly one bonus offer and one production objective. Revision 3: the bonus offer pays more than the standing ceiling and at most 2.0
            // (the range D-D reserves for it, owner decision 2026-10-22), here 1.8.
            var bonus = catalog.ContractOffers.Values.Single(o => o.Class == "bonus");
            Assert.That(bonus.Id, Is.EqualTo("contract.bonus-plate"));
            Assert.That(bonus.Objective, Is.EqualTo("deliver"));
            Assert.That(bonus.Bonus, Is.Not.Null);
            Assert.That(bonus.Bonus.WindowMs, Is.EqualTo(900_000));
            Assert.That(bonus.Bonus.WindowMs, Is.LessThanOrEqualTo(604_800_000L), "owner decision 2026-10-21: the bonus window is at most seven days");
            Assert.That(bonus.Bonus.Trigger.Kind, Is.EqualTo("factoryLevelAtLeast"));
            Assert.That(bonus.RewardRatioPpm, Is.EqualTo(1_800_000));
            var produce = catalog.ContractOffers.Values.Single(o => o.Objective == "produce");
            Assert.That(produce.Id, Is.EqualTo("contract.produce-plate"));
            Assert.That(produce.Class, Is.EqualTo("standing"));
            Assert.That(produce.Bonus, Is.Null);
            Assert.That(produce.CooldownMs, Is.GreaterThan(0));
            Assert.That(produce.QuantityTable[0], Is.GreaterThanOrEqualTo(10), "contract.produce.min");
            Assert.That(produce.RewardRatioPpm, Is.EqualTo(400_000));
            Assert.That(produce.RewardRatioPpm + catalog.ContractOffers["contract.plate"].RewardRatioPpm, Is.LessThanOrEqualTo(2_000_000), "contract.ratio.produce (ECO-1)");

            // Revision 3 (T345): every standing delivery offer pays exactly the 1.5 ceiling (F003 paid 1.67 to 1.75, which was above it, A-F3). The Cash at
            // index 0 is quantity x price x 1.5: 100 Plates x 8, 100 Gears x 24 and 100 Bars x 3.
            Assert.That(catalog.ContractOffers["contract.plate"].RewardRatioPpm, Is.EqualTo(1_500_000));
            Assert.That(catalog.ContractOffers["contract.gear"].RewardRatioPpm, Is.EqualTo(1_500_000));
            Assert.That(catalog.ContractOffers["contract.timed-bar"].RewardRatioPpm, Is.EqualTo(1_500_000));
            // T320: Cash is MulDivFloor(sale basis, ratio, 10^6) at the base price.
            Assert.That(CashAtIndexZero(catalog, "contract.plate"), Is.EqualTo(1_200));
            Assert.That(CashAtIndexZero(catalog, "contract.gear"), Is.EqualTo(3_600));
            Assert.That(CashAtIndexZero(catalog, "contract.timed-bar"), Is.EqualTo(450));

            // The ladder carries the Item grants as (run-wide completion index, Item) entries, granted by any offer. Revision 3 (T345) spreads the ten
            // grants over the run (F003 and revision 2 granted all ten within the first ten completions); the highest index, 200, covers the 163
            // completions of one offer that the full matrix measured, which is the reachable range of contract.quantity.capacity (#387 (d)).
            Assert.That(
                catalog.ContractItemLadder.Entries.Select(e => e.Index + ":" + e.ItemId),
                Is.EqualTo(new[]
                {
                    "0:item.diamond-drill-head", "1:item.flux-catalyst", "3:item.overclock-core", "6:item.diamond-drill-head", "12:item.flux-catalyst",
                    "25:item.overclock-core", "50:item.diamond-drill-head", "100:item.flux-catalyst", "150:item.overclock-core", "200:item.diamond-drill-head",
                }));
            Assert.That(catalog.ContractItemLadder.PerRunCap, Is.EqualTo(10), "#387: the cap is the total number of ladder entries");
            Assert.That(catalog.ContractItemLadder.PerRunCap, Is.EqualTo(catalog.ContractItemLadder.Entries.Count));
            foreach (var offer in catalog.ContractOffers.Values)
            {
                Assert.That(offer.Rewards.Select(r => r.Kind).ToArray(), Is.EqualTo(new[] { "rp" }), offer.Id + ": since T320 an offer carries only its RP curve");
            }

            Assert.That(catalog.RunStart.SellSteps, Is.EqualTo(new long[] { 1, 10, 100 }));
            Assert.That(catalog.RunStart.SellAll, Is.True);
            Assert.That(catalog.RunStart.LiveGapThresholdMs, Is.EqualTo(60_000));
            Assert.That(catalog.RunStart.ManualMining.ResourceId, Is.EqualTo("iron.ore"));
            Assert.That(catalog.Milestones.Select(m => m.FractionPpm), Is.EqualTo(new long[] { 1_000, 10_000, 100_000, 500_000 }), "revision 3 milestones at 0.1 %, 1 %, 10 % and 50 % of the threshold");
            Assert.That(catalog.ChainDominanceMarginPpm, Is.GreaterThan(0));
            foreach (var machine in catalog.Machines.Values)
            {
                // Revision 3 (T345, F11): the second and third copy of a type cost more (15 % per owned copy), the price rises 4 % per completed prestige,
                // and both stop at three times the base price.
                Assert.That(machine.BuildPrice.OwnedGrowthPpm, Is.EqualTo(1_150_000), machine.Id);
                Assert.That(machine.BuildPrice.PrestigeGrowthPpm, Is.EqualTo(1_040_000), machine.Id);
                Assert.That(machine.BuildPrice.Cap, Is.EqualTo(machine.BuildPrice.Base * 3), machine.Id);
                Assert.That(machine.BuildPrice.Base, Is.EqualTo(machine.CostTable[0]), machine.Id + ": the build price starts at the F003 level-1 cost");
            }

            // Revision 3 (T345): every Industry upgrade declares its decay (1 to 10^6, 10^6 is the explicit no decay) and a practical ceiling of at most its last level.
            foreach (var upgrade in catalog.IndustryUpgrades.Values)
            {
                Assert.That(upgrade.DecayPpm, Is.InRange(1L, Ppm), upgrade.Id);
                Assert.That(upgrade.PracticalCeilingLevel, Is.InRange(1L, (long)upgrade.MaxLevel), upgrade.Id);
            }

            Assert.That(catalog.IndustryUpgrades["industry.warehouse"].DecayPpm, Is.EqualTo(Ppm), "a +1 start storage level cannot decay (#396)");
        }

        private static long CashAtIndexZero(RunCatalog catalog, string offerId)
        {
            var offer = catalog.ContractOffers[offerId];
            Assert.That(ContractRewards.TryCash(offer.QuantityTable[0], catalog.Resources[offer.ResourceId].BasePrice, Ppm, offer.RewardRatioPpm, out var cash), Is.True);
            return cash;
        }

        [Test]
        public void ContractsCoverPlateGearAndTimedBarAndGrantEveryItemTierInTheFirstRun()
        {
            var catalog = Catalog();

            // The legacy Kind label is derived from the untimed resources by price; the T363 offers use Plate (the cheapest untimed resource) so
            // the labels of the F003 offers do not move.
            Assert.That(catalog.ContractOffers.Values.Select(c => c.Kind), Is.EquivalentTo(new[] { "plate", "plate", "plate", "gear", "timedBar" }));
            foreach (var offer in catalog.ContractOffers.Values)
            {
                Assert.That(offer.DurationMs.HasValue, Is.EqualTo(offer.Kind == "timedBar"), offer.Id);
                Assert.That(offer.RewardRatioPpm, Is.GreaterThan(0), offer.Id);
            }

            // Every tier is first granted within the first completions of the run, whichever offer completes.
            var ladder = catalog.ContractItemLadder;
            foreach (var tier in new[] { 1, 2, 3 })
            {
                var first = ladder.Entries.Where(e => catalog.Items[e.ItemId].Tier == tier).Min(e => e.Index);
                Assert.That(first, Is.LessThanOrEqualTo(3), "tier " + tier + " is granted within the first completions");
            }

            Assert.That(ContractRewards.DueItems(ladder, 0).Select(i => catalog.Items[i.Id].Tier), Is.EqualTo(new[] { 1 }),
                "the first completion of the run grants the Tier I Item");
        }

        [Test]
        public void ItemsFollowFr017AndFr018AndTheirTargetsAreMachineTypeOrFactoryOnly()
        {
            var catalog = Catalog();
            var drill = catalog.Items["item.diamond-drill-head"];

            Assert.That(drill.Tier, Is.EqualTo(1));
            Assert.That(drill.DurationMs, Is.EqualTo(60_000));
            Assert.That(drill.TargetKind, Is.EqualTo("machineType"));
            Assert.That(drill.AllowedMachineIds, Is.EqualTo(new[] { "machine.miner" }));
            Assert.That(drill.Effect.Kind, Is.EqualTo("machineOutput"));
            Assert.That(drill.Effect.Value, Is.EqualTo(2));

            Assert.That(catalog.Items.Values.Select(i => i.Tier).OrderBy(t => t), Is.EqualTo(new[] { 1, 2, 3 }));
            foreach (var item in catalog.Items.Values)
            {
                Assert.That(item.TargetKind, Is.EqualTo("machineType").Or.EqualTo("factory"), item.Id);
                Assert.That(item.DurationMs, Is.InRange(60_000, 120_000), item.Id + " early effects last 1-2 minutes");
            }
        }

        [Test]
        public void IndustryHasAFirstAwardUpgradeAndEveryFamilyIsCapped()
        {
            var catalog = Catalog();
            var firstAward = IpAt(catalog, catalog.Prestige.ThresholdFactoryValue);

            Assert.That(firstAward, Is.EqualTo(10));
            Assert.That(catalog.IndustryUpgrades.Values.Any(u => u.IpCostTable[0] <= firstAward), Is.True, "affordable with the first IP award");

            var kinds = catalog.IndustryUpgrades.Values.Select(u => u.Effect.Kind).Distinct().ToList();
            Assert.That(kinds, Is.SupersetOf(new[] { "startingCash", "upgradeCost", "startingStorageLevel", "machineSpeed", "salePrice" }));

            // ECO-3: bounded per-level effects and maxLevel, so run 3+ cannot collapse. Revision 3 (T345) has diminishing returns instead of hard caps
            // (FR-021, D-C): the cumulative effect is folded from the decayed per-level value table of each upgrade.
            foreach (var upgrade in catalog.IndustryUpgrades.Values)
            {
                Assert.That(upgrade.MaxLevel, Is.InRange(1, 10), upgrade.Id);
            }

            Assert.That(CumulativeDecayed(catalog.IndustryUpgrades.Values.Where(u => u.Effect.Kind == "machineSpeed")), Is.LessThanOrEqualTo(4 * Ppm));
            Assert.That(CumulativeDecayed(catalog.IndustryUpgrades.Values.Where(u => u.Effect.Kind == "salePrice")), Is.LessThanOrEqualTo(3 * Ppm));
            Assert.That(CumulativeDecayed(catalog.IndustryUpgrades.Values.Where(u => u.Effect.Kind == "upgradeCost")), Is.GreaterThanOrEqualTo(Ppm / 2));
            var cash = catalog.IndustryUpgrades.Values.Where(u => u.Effect.Kind == "startingCash").Sum(u => u.Effect.Value * u.MaxLevel);
            Assert.That(cash, Is.LessThanOrEqualTo(catalog.Prestige.ThresholdFactoryValue / 100), "starting Cash from Industry stays below 1 % of the threshold");
            var storage = catalog.IndustryUpgrades.Values.Where(u => u.Effect.Kind == "startingStorageLevel").Sum(u => u.Effect.Value * u.MaxLevel);
            Assert.That(storage, Is.LessThan(catalog.StorageLevels.Count), "the starting storage level stays inside the storage table");
        }

        [Test]
        public void ResearchGraphIsAcyclicAndHasFreeRoots()
        {
            var catalog = Catalog();
            var order = catalog.ResearchInContentOrder.Select(r => r.Id).ToList();
            Assert.That(order, Is.Ordered.Using<string>(StringComparer.Ordinal), "canonical: research sorted by id");

            var depth = new Dictionary<string, int>(StringComparer.Ordinal);
            int Depth(string id, HashSet<string> path)
            {
                Assert.That(path.Add(id), Is.True, "prerequisite cycle through " + id);
                if (!depth.TryGetValue(id, out var d))
                {
                    var research = catalog.Research[id];
                    d = research.Prerequisites.Count == 0 ? 0 : 1 + research.Prerequisites.Max(p => Depth(p.ResearchId, path));
                    depth[id] = d;
                }

                path.Remove(id);
                return d;
            }

            foreach (var id in catalog.Research.Keys)
            {
                Depth(id, new HashSet<string>(StringComparer.Ordinal));
            }

            Assert.That(catalog.Research.Values.Count(r => r.Prerequisites.Count == 0), Is.GreaterThanOrEqualTo(2));
            Assert.That(depth.Values.Max(), Is.GreaterThanOrEqualTo(2), "a prerequisite chain exists");
            foreach (var research in catalog.Research.Values)
            {
                foreach (var prerequisite in research.Prerequisites)
                {
                    Assert.That(prerequisite.Level, Is.InRange(1, catalog.Research[prerequisite.ResearchId].MaxLevel), research.Id);
                }
            }
        }

        // ---- references and keys ---------------------------------------------------------------------------

        [Test]
        public void EveryReferenceResolves()
        {
            var catalog = Catalog();
            var unlockIds = new HashSet<string>(catalog.Resources.Keys.Concat(catalog.Machines.Keys).Concat(catalog.ContractOffers.Keys), StringComparer.Ordinal);

            foreach (var machine in catalog.Machines.Values)
            {
                Assert.That(catalog.Recipes.ContainsKey(machine.RecipeId), Is.True, machine.Id);
                AssertUnlockResolves(catalog, machine.Unlock, machine.Id);
            }

            foreach (var recipe in catalog.Recipes.Values)
            {
                foreach (var amount in recipe.Inputs.Concat(recipe.Outputs))
                {
                    Assert.That(catalog.Resources.ContainsKey(amount.ResourceId), Is.True, recipe.Id);
                }
            }

            foreach (var resource in catalog.Resources.Values)
            {
                AssertUnlockResolves(catalog, resource.Unlock, resource.Id);
            }

            foreach (var offer in catalog.ContractOffers.Values)
            {
                Assert.That(catalog.Resources.ContainsKey(offer.ResourceId), Is.True, offer.Id);
                AssertUnlockResolves(catalog, offer.Unlock, offer.Id);
            }

            foreach (var entry in catalog.ContractItemLadder.Entries)
            {
                Assert.That(catalog.Items.ContainsKey(entry.ItemId), Is.True, entry.ItemId);
            }

            foreach (var item in catalog.Items.Values)
            {
                foreach (var machineId in item.AllowedMachineIds)
                {
                    Assert.That(catalog.Machines.ContainsKey(machineId), Is.True, item.Id);
                }
            }

            foreach (var effect in catalog.Research.Values.Select(r => r.Effect).Concat(catalog.IndustryUpgrades.Values.Select(u => u.Effect)))
            {
                if (effect.TargetKind == "machine")
                {
                    Assert.That(catalog.Machines.ContainsKey(effect.TargetId), Is.True, effect.Kind);
                }
                else if (effect.TargetKind == "resource")
                {
                    Assert.That(catalog.Resources.ContainsKey(effect.TargetId), Is.True, effect.Kind);
                }
            }

            AssertUnlockResolves(catalog, catalog.Overdrive.Unlock, "overdrive");
            foreach (var id in catalog.RunStart.InitialUnlocks)
            {
                Assert.That(unlockIds.Contains(id), Is.True, "initial unlock " + id);
            }

            Assert.That(unlockIds.Contains(RunState.OverdriveUnlockId), Is.False, "the Overdrive ID is reserved");
        }

        [Test]
        public void PresentationKeysMatchTheT090KeyList()
        {
            var lines = File.ReadAllLines(BriefPath);
            var expected = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var family in new[] { "| Machines |", "| Resources |", "| Items |" })
            {
                var line = lines.First(l => l.StartsWith(family, StringComparison.Ordinal));
                foreach (Match match in Regex.Matches(line, "`(F003-(?:MACHINE|RES|ITEM)-[A-Z0-9-]+)`"))
                {
                    expected.Add(match.Groups[1].Value);
                }
            }

            Assert.That(expected.Count, Is.EqualTo(11), "the brief lists exactly 11 keys");

            var catalog = Catalog();
            var actual = new SortedSet<string>(
                catalog.Resources.Values.Select(r => r.PresentationKey)
                    .Concat(catalog.Machines.Values.Select(m => m.PresentationKey))
                    .Concat(catalog.Items.Values.Select(i => i.PresentationKey)),
                StringComparer.Ordinal);

            Assert.That(actual, Is.EqualTo(expected));
        }

        // ---- worst cases quoted by the content record ---------------------------------------------------------

        [Test]
        public void WorstCaseSpeedStaysUnderTheBoundForEveryMachineAndBatchesAreRecorded()
        {
            var catalog = Catalog();
            var record = File.ReadAllText(TuningRecordPath);

            foreach (var machine in catalog.Machines.Values.OrderBy(m => m.Id, StringComparer.Ordinal))
            {
                var speed = WorstSpeed(catalog, machine);
                Assert.That(speed, Is.GreaterThan(0), machine.Id);
                Assert.That(speed, Is.LessThanOrEqualTo(catalog.RunStart.MaxCombinedSpeedPpm), machine.Id + " stays under maxCombinedSpeedPpm");

                var recipe = catalog.Recipes[machine.RecipeId];
                Assert.That(SpeedComposer.TryBatch(recipe.DurationTicks, speed, catalog.RunStart.MinCycleTicks, out var batch, out _), Is.True, machine.Id);
                Assert.That(batch, Is.GreaterThan(1), machine.Id + " reaches batching at the maximum");

                Assert.That(record, Does.Contain(speed.ToString(CultureInfo.InvariantCulture)), machine.Id + " worst-case speed ppm is in the record");
                Assert.That(record, Does.Contain(batch.ToString(CultureInfo.InvariantCulture)), machine.Id + " worst-case batch is in the record");
            }
        }

        [Test]
        public void OutputFactorHasNoDeadSourceAndReachesTheBoundExactly()
        {
            var catalog = Catalog();
            var bound = catalog.RunStart.MaxCombinedOutputFactor;

            var sources = new List<long>();
            sources.AddRange(catalog.Items.Values.Where(i => i.Effect.Kind == "machineOutput" && i.AllowedMachineIds.Contains("machine.miner")).Select(i => i.Effect.Value));
            foreach (var research in catalog.Research.Values.Where(r => r.Effect.Kind == "machineOutput" && r.Effect.TargetId == "machine.miner"))
            {
                for (var level = 0; level < research.MaxLevel; level++)
                {
                    sources.Add(research.Effect.Value);
                }
            }

            Assert.That(sources.Count, Is.GreaterThanOrEqualTo(2));
            var product = sources.Aggregate(1L, (acc, f) => acc * f);
            Assert.That(product, Is.LessThanOrEqualTo(bound), "the combined output factor never clamps");
            for (var skip = 0; skip < sources.Count; skip++)
            {
                var without = sources.Where((_, i) => i != skip).Aggregate(1L, (acc, f) => acc * f);
                Assert.That(without, Is.LessThan(product), "source " + skip + " contributes (not dead)");
            }
        }

        [Test]
        public void SalePriceEffectOnFactoryValueIsBoundedAndRecorded()
        {
            var catalog = Catalog();
            var record = File.ReadAllText(TuningRecordPath);
            var capacity = catalog.StorageLevels[catalog.StorageLevels.Count - 1].Capacity;

            long worstStock = 0;
            foreach (var resource in catalog.Resources.Values)
            {
                var multiplier = MaxSalePricePpm(catalog, resource.Id);
                Assert.That(multiplier, Is.GreaterThan(Ppm), resource.Id);
                Assert.That(MulDiv.MulDivFloor(capacity * resource.BasePrice, multiplier, Ppm, out var stock), Is.True);
                worstStock = Math.Max(worstStock, stock);
            }

            Assert.That(worstStock, Is.LessThanOrEqualTo(catalog.Prestige.ThresholdFactoryValue / 10), "at most 10 % of the prestige threshold");
            Assert.That(catalog.RunStart.RemovalRefundPpm, Is.LessThan(Ppm), "build/remove strictly lowers Factory Value (ECO-7)");
            Assert.That(record, Does.Contain(worstStock.ToString(CultureInfo.InvariantCulture)), "the bounded stock value is in the record");
        }

        [Test]
        public void CashRewardsStayBetweenMarketValueAndTheRatioBoundAtEveryCompletionIndex()
        {
            var catalog = Catalog();
            var bound = catalog.RunStart.MaxRewardRatioPpm;

            foreach (var offer in catalog.ContractOffers.Values)
            {
                var price = catalog.Resources[offer.ResourceId].BasePrice;
                Assert.That(offer.QuantityTable.Count, Is.EqualTo(1024), offer.Id);
                for (var i = 0; i < 1024; i++)
                {
                    var market = offer.QuantityTable[i] * price;
                    Assert.That(ContractRewards.TryCash(offer.QuantityTable[i], price, Ppm, offer.RewardRatioPpm, out var cash), Is.True, offer.Id + "@" + i);
                    // The market-value floor is a delivery rule (the validator checks it for the deliver objective only). T363 adds a production
                    // objective that pays a fraction of the market value on top of the Plate offer (ECO-1: R(produce) + R(standing) <= 2.0).
                    if (offer.Objective == "deliver") Assert.That(cash, Is.GreaterThanOrEqualTo(market), offer.Id + "@" + i);
                    else Assert.That(offer.RewardRatioPpm, Is.GreaterThan(0), offer.Id);
                    Assert.That(MulDiv.MulDivFloor(market, bound, Ppm, out var ceiling), Is.True);
                    Assert.That(cash, Is.LessThanOrEqualTo(ceiling), offer.Id + "@" + i);
                }
            }
        }

        [Test]
        public void ContentRecordExistsAndLabelsTheSheetAHypothesis()
        {
            var record = File.ReadAllText(RecordPath);

            Assert.That(record, Does.Contain("hypothesis").IgnoreCase);
            Assert.That(record, Does.Contain("contentRevision 2"));
            Assert.That(record, Does.Contain("T127"));
            foreach (var heading in new[] { "Theoretical maximum income", "Maximum batch per machine", "Maximum combined multiplier per effect kind", "Sale-price effect on Factory Value", "Cash-to-market ratio per offer" })
            {
                Assert.That(record, Does.Contain(heading), heading);
            }
        }

        // ---- helpers --------------------------------------------------------------------------------------------

        private sealed class AllKeysResolve : IPresentationKeyResolver
        {
            public bool IsResolved(string presentationKey) => true;
        }

        private static MachineRecipe RecipeOf(RunCatalog catalog, string machineId) => catalog.Recipes[catalog.Machines[machineId].RecipeId];

        private static void AssertRecipe(MachineRecipe recipe, (string, long)[] inputs, (string, long)[] outputs, long ticks, string message)
        {
            Assert.That(recipe.Inputs.Select(a => (a.ResourceId, a.Amount)), Is.EqualTo(inputs), message);
            Assert.That(recipe.Outputs.Select(a => (a.ResourceId, a.Amount)), Is.EqualTo(outputs), message);
            Assert.That(recipe.DurationTicks, Is.EqualTo(ticks), message);
        }

        private static long IpAt(RunCatalog catalog, long factoryValue)
        {
            Assert.That(FactoryValuation.TryComputeIp(catalog.Prestige, factoryValue, out var ip), Is.True);
            return ip;
        }

        private static void AssertIp(RunCatalog catalog, long factoryValue, long expected) =>
            Assert.That(IpAt(catalog, factoryValue), Is.EqualTo(expected), "IP at Factory Value " + factoryValue);

        private static void AssertUnlockResolves(RunCatalog catalog, UnlockCondition unlock, string owner)
        {
            switch (unlock.Kind)
            {
                case "researchPurchased":
                    Assert.That(catalog.Research.ContainsKey(unlock.Id), Is.True, owner);
                    break;
                case "resourceProduced":
                    Assert.That(catalog.Resources.ContainsKey(unlock.Id), Is.True, owner);
                    break;
                case "factoryLevelAtLeast":
                    Assert.That(unlock.Level, Is.InRange(1, catalog.FactoryLevels.Count), owner);
                    break;
                default:
                    Assert.That(unlock.Kind, Is.EqualTo("always"), owner);
                    break;
            }
        }

        /// <summary>Product of an effect's per-level factor over all its levels, folded like a sale or speed fold (floor).</summary>
        private static long Fold(long acc, long factor, int levels)
        {
            for (var i = 0; i < levels; i++)
            {
                Assert.That(MulDiv.MulDivFloor(acc, factor, Ppm, out acc), Is.True);
            }

            return acc;
        }

        private static long Cumulative(IEnumerable<IndustryUpgradeDef> upgrades, Func<long, long, int, long> fold)
        {
            var acc = Ppm;
            foreach (var upgrade in upgrades.OrderBy(u => u.Id, StringComparer.Ordinal))
            {
                acc = fold(acc, upgrade.Effect.Value, upgrade.MaxLevel);
            }

            return acc;
        }

        /// <summary>Product of every level of the given factor upgrades, each level taken from the decayed value table, folded like the engine (floor).</summary>
        private static long CumulativeDecayed(IEnumerable<IndustryUpgradeDef> upgrades)
        {
            var acc = Ppm;
            foreach (var upgrade in upgrades.OrderBy(u => u.Id, StringComparer.Ordinal))
            {
                foreach (var factor in upgrade.ValueTable)
                {
                    Assert.That(MulDiv.MulDivFloor(acc, factor, Ppm, out acc), Is.True);
                }
            }

            return acc;
        }

        private static long MaxSalePricePpm(RunCatalog catalog, string resourceId)
        {
            var acc = Ppm;
            foreach (var research in catalog.Research.Values.OrderBy(r => r.Id, StringComparer.Ordinal))
            {
                if (Targets(research.Effect, "salePrice", "allResources", "resource", resourceId))
                {
                    acc = Fold(acc, research.Effect.Value, research.MaxLevel);
                }
            }

            foreach (var upgrade in catalog.IndustryUpgrades.Values.OrderBy(u => u.Id, StringComparer.Ordinal))
            {
                if (Targets(upgrade.Effect, "salePrice", "allResources", "resource", resourceId))
                {
                    // Revision 3 (T345): an Industry level folds the factor of its decayed value table (a no-decay upgrade has the same factor on every level).
                    foreach (var factor in upgrade.ValueTable)
                    {
                        Assert.That(MulDiv.MulDivFloor(acc, factor, Ppm, out acc), Is.True);
                    }
                }
            }

            return acc;
        }

        private static bool Targets(EffectDef effect, string kind, string allKind, string oneKind, string id) =>
            effect.Kind == kind && (effect.TargetKind == allKind || (effect.TargetKind == oneKind && effect.TargetId == id));

        /// <summary>Worst-case composed speed of a machine: maximum level, every Research, Industry, Item and Overdrive source at its maximum.</summary>
        private static long WorstSpeed(RunCatalog catalog, MachineDef machine)
        {
            var research = new List<SpeedFactor>();
            foreach (var def in catalog.Research.Values.OrderBy(r => r.Id, StringComparer.Ordinal))
            {
                if (Targets(def.Effect, "machineSpeed", "allMachines", "machine", machine.Id))
                {
                    research.Add(new SpeedFactor(def.Effect.Value, def.MaxLevel));
                }
            }

            var industry = new List<SpeedFactor>();
            foreach (var def in catalog.IndustryUpgrades.Values.OrderBy(u => u.Id, StringComparer.Ordinal))
            {
                if (Targets(def.Effect, "machineSpeed", "allMachines", "machine", machine.Id))
                {
                    // Revision 3 (T345): the decayed value table, one factor per level (what the engine folds); without decay every level is Effect.Value.
                    foreach (var factor in def.ValueTable)
                    {
                        industry.Add(new SpeedFactor(factor, 1));
                    }
                }
            }

            var perKey = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var item in catalog.Items.Values)
            {
                if (item.Effect.Kind != "machineSpeed")
                {
                    continue;
                }

                string key = null;
                if (item.TargetKind == "factory")
                {
                    key = "factory";
                }
                else if (item.AllowedMachineIds.Contains(machine.Id))
                {
                    key = "type:" + string.Join("\n", item.AllowedMachineIds.OrderBy(i => i, StringComparer.Ordinal));
                }

                if (key != null && (!perKey.TryGetValue(key, out var best) || item.Effect.Value > best))
                {
                    perKey[key] = item.Effect.Value;
                }
            }

            Assert.That(
                SpeedComposer.TryCompose(
                    machine.SpeedTable[machine.MaxLevel],
                    research,
                    industry,
                    new List<long>(perKey.Values),
                    catalog.Overdrive.SpeedPpm,
                    ExactBound.Value,
                    out var speed),
                Is.True,
                machine.Id);
            return speed;
        }

        // ---- canonical renderer ---------------------------------------------------------------------------------

        /// <summary>
        /// Test-only canonical rendering of a v3 document (author guide, "v3: canonical form"): two-space indentation, LF
        /// endings, a final newline, members in table order, arrays of objects sorted by id (levels by level, machine milestones by
        /// level, prestige milestones by fraction, ladder entries by index and Item, offline policies by revision, amounts by resourceId,
        /// prerequisites by id) and scalar arrays ascending, except the sell steps, which keep their own order (integers, then all).
        /// </summary>
        private static class CanonicalRenderer
        {
            private sealed class Obj : List<KeyValuePair<string, object>>
            {
                public Obj Add(string name, object value)
                {
                    Add(new KeyValuePair<string, object>(name, value));
                    return this;
                }
            }

            public static string Render(RunContentDocumentDto d)
            {
                var root = new Obj()
                    .Add("contentSchemaVersion", (long)d.ContentSchemaVersion)
                    .Add("contentRevision", (long)d.ContentRevision)
                    .Add("runStart", RunStart(d.RunStart))
                    .Add("resources", Sorted(d.Resources, r => r.Id, Resource))
                    .Add("recipes", Sorted(d.Recipes, r => r.Id, Recipe))
                    .Add("machines", Sorted(d.Machines, m => m.Id, Machine))
                    .Add("storageLevels", ByLevel(d.StorageLevels, l => l.Level, l => new Obj().Add("level", (long)l.Level).Add("capacity", l.Capacity).Add("cost", l.Cost)))
                    .Add("factoryLevels", ByLevel(d.FactoryLevels, l => l.Level, l => new Obj().Add("level", (long)l.Level).Add("slots", l.Slots).Add("cost", l.Cost)))
                    .Add("contracts", Sorted(d.Contracts, c => c.Id, Contract))
                    .Add("contractItemLadder", new Obj()
                        .Add("entries", d.ContractItemLadder.Entries.OrderBy(e => e.Index).ThenBy(e => e.ItemId, StringComparer.Ordinal)
                            .Select(e => (object)new Obj().Add("index", e.Index).Add("itemId", e.ItemId)).ToList())
                        .Add("perRunCap", d.ContractItemLadder.PerRunCap))
                    .Add("items", Sorted(d.Items, i => i.Id, Item))
                    .Add("research", Sorted(d.Research, r => r.Id, Research))
                    .Add("overdrive", new Obj()
                        .Add("durationMs", d.Overdrive.DurationMs)
                        .Add("cooldownMs", d.Overdrive.CooldownMs)
                        .Add("speedPpm", d.Overdrive.SpeedPpm)
                        .Add("unlock", Unlock(d.Overdrive.Unlock)))
                    .Add("prestige", new Obj()
                        .Add("threshold", new Obj().Add("base", d.Prestige.Threshold.Base).Add("growthPpm", d.Prestige.Threshold.GrowthPpm).Add("maxIndex", d.Prestige.Threshold.MaxIndex))
                        .Add("ipCurve", new Obj().Add("kind", d.Prestige.IpCurve.Kind).Add("coefficient", d.Prestige.IpCurve.Coefficient).Add("divisor", d.Prestige.IpCurve.Divisor)))
                    .Add("milestones", d.Milestones.OrderBy(m => m.FractionPpm).Select(m => (object)new Obj().Add("fractionPpm", m.FractionPpm)).ToList())
                    .Add("industryUpgrades", Sorted(d.IndustryUpgrades, u => u.Id, Industry))
                    .Add("chainDominanceMarginPpm", d.ChainDominanceMarginPpm)
                    .Add("offlinePolicies", d.OfflinePolicies.OrderBy(p => p.Revision).Select(p => (object)new Obj()
                        .Add("revision", (long)p.Revision)
                        .Add("efficiencyNumerator", p.EfficiencyNumerator)
                        .Add("efficiencyDenominator", p.EfficiencyDenominator)
                        .Add("maxCreditedTicks", p.MaxCreditedTicks)).ToList());

                var text = new StringBuilder();
                Write(text, root, 0);
                text.Append('\n');
                return text.ToString();
            }

            private static List<object> Sorted<T>(IEnumerable<T> source, Func<T, string> id, Func<T, Obj> map) =>
                source.OrderBy(id, StringComparer.Ordinal).Select(x => (object)map(x)).ToList();

            private static List<object> ByLevel<T>(IEnumerable<T> source, Func<T, int> level, Func<T, Obj> map) =>
                source.OrderBy(level).Select(x => (object)map(x)).ToList();

            private static Obj RunStart(RunStartDto s) => new Obj()
                .Add("startingCash", s.StartingCash)
                .Add("initialUnlocks", s.InitialUnlocks.OrderBy(x => x, StringComparer.Ordinal).Select(x => (object)x).ToList())
                .Add("removalRefundPpm", s.RemovalRefundPpm)
                .Add("minCycleTicks", s.MinCycleTicks)
                .Add("maxCombinedSpeedPpm", s.MaxCombinedSpeedPpm)
                .Add("maxCombinedOutputFactor", s.MaxCombinedOutputFactor)
                .Add("maxRewardRatioPpm", s.MaxRewardRatioPpm)
                .Add("sellSteps", s.SellSteps.Select(x => (object)x).Concat(s.SellAll ? new object[] { "all" } : new object[0]).ToList())
                .Add("liveGapThresholdMs", s.LiveGapThresholdMs)
                .Add("manualMining", new Obj()
                    .Add("resourceId", s.ManualMining.ResourceId)
                    .Add("baseYieldMilli", s.ManualMining.BaseYieldMilli)
                    .Add("maxActivationsPerSecond", s.ManualMining.MaxActivationsPerSecond)
                    .Add("startWindowMs", s.ManualMining.StartWindowMs)
                    .Add("referenceMinerLevel", s.ManualMining.ReferenceMinerLevel)
                    .Add("maxShareOfMinerPpm", s.ManualMining.MaxShareOfMinerPpm)
                    .Add("miningAdvantageMaxPpm", s.ManualMining.MiningAdvantageMaxPpm))
                .Add("minRunDurationMs", s.MinRunDurationMs)
                .Add("uptimeMaxPpm", s.UptimeMaxPpm);

            private static Obj Unlock(RunUnlockDto u)
            {
                var o = new Obj().Add("kind", u.Kind);
                if (u.Id != null)
                {
                    o.Add("id", u.Id);
                }

                if (u.Level.HasValue)
                {
                    o.Add("level", (long)u.Level.Value);
                }

                return o;
            }

            private static Obj Curve(RunCurveDto c) => new Obj().Add("base", c.Base).Add("growthPpm", c.GrowthPpm).Add("rounding", c.Rounding);

            private static Obj Resource(RunResourceDto r) => new Obj()
                .Add("id", r.Id).Add("label", r.Label).Add("presentationKey", r.PresentationKey).Add("basePrice", r.BasePrice).Add("unlock", Unlock(r.Unlock));

            private static List<object> Amounts(IEnumerable<RunAmountDto> amounts) =>
                amounts.OrderBy(a => a.ResourceId, StringComparer.Ordinal).Select(a => (object)new Obj().Add("resourceId", a.ResourceId).Add("amount", a.Amount)).ToList();

            private static Obj Recipe(RunRecipeDto r) => new Obj()
                .Add("id", r.Id).Add("inputs", Amounts(r.Inputs)).Add("outputs", Amounts(r.Outputs)).Add("durationTicks", r.DurationTicks);

            private static Obj Machine(RunMachineDto m) => new Obj()
                .Add("id", m.Id).Add("label", m.Label).Add("presentationKey", m.PresentationKey).Add("recipeId", m.RecipeId)
                .Add("maxLevel", (long)m.MaxLevel).Add("cost", Curve(m.Cost))
                .Add("buildPrice", new Obj().Add("base", m.BuildPrice.Base).Add("ownedGrowthPpm", m.BuildPrice.OwnedGrowthPpm).Add("prestigeGrowthPpm", m.BuildPrice.PrestigeGrowthPpm).Add("cap", m.BuildPrice.Cap))
                .Add("speedGrowthPpm", m.SpeedGrowthPpm)
                .Add("milestones", m.Milestones.OrderBy(x => x.Level).Select(x => (object)new Obj().Add("level", (long)x.Level).Add("speedPpm", x.SpeedPpm)).ToList())
                .Add("unlock", Unlock(m.Unlock));

            private static Obj Contract(RunContractDto c)
            {
                var o = new Obj().Add("id", c.Id).Add("objective", c.Objective).Add("class", c.Class).Add("resourceId", c.ResourceId).Add("quantity", Curve(c.Quantity));
                if (c.DurationMs.HasValue)
                {
                    o.Add("durationMs", c.DurationMs.Value);
                }

                o.Add("rewardRatioPpm", c.RewardRatioPpm).Add("cooldownMs", c.CooldownMs).Add("rpReward", Curve(c.RpReward));
                if (c.Bonus != null)
                {
                    o.Add("bonus", new Obj().Add("trigger", Unlock(c.Bonus.Trigger)).Add("windowMs", c.Bonus.WindowMs));
                }

                o.Add("unlock", Unlock(c.Unlock));
                return o;
            }

            private static Obj Effect(RunEffectDto e)
            {
                var o = new Obj().Add("kind", e.Kind);
                if (e.Target != null)
                {
                    var target = new Obj().Add("kind", e.Target.Kind);
                    if (e.Target.Id != null)
                    {
                        target.Add("id", e.Target.Id);
                    }

                    o.Add("target", target);
                }

                return o.Add(e.ValueMember, e.Value);
            }

            private static Obj Item(RunItemDto i)
            {
                var target = new Obj().Add("kind", i.Target.Kind);
                if (i.Target.AllowedMachineIds != null)
                {
                    target.Add("allowedMachineIds", i.Target.AllowedMachineIds.OrderBy(x => x, StringComparer.Ordinal).Select(x => (object)x).ToList());
                }

                return new Obj().Add("id", i.Id).Add("tier", (long)i.Tier).Add("label", i.Label).Add("presentationKey", i.PresentationKey)
                    .Add("durationMs", i.DurationMs).Add("target", target).Add("effect", Effect(i.Effect));
            }

            private static Obj Research(RunResearchDto r) => new Obj()
                .Add("id", r.Id).Add("label", r.Label).Add("maxLevel", (long)r.MaxLevel).Add("rpCost", Curve(r.RpCost))
                .Add("prerequisites", r.Prerequisites.OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => (object)new Obj().Add("id", p.Id).Add("level", (long)p.Level)).ToList())
                .Add("effect", Effect(r.Effect));

            private static Obj Industry(RunIndustryUpgradeDto u) => new Obj()
                .Add("id", u.Id).Add("label", u.Label).Add("maxLevel", (long)u.MaxLevel).Add("ipCost", Curve(u.IpCost))
                .Add("decayPpm", u.DecayPpm).Add("practicalCeilingLevel", u.PracticalCeilingLevel).Add("minGainPpm", u.MinGainPpm)
                .Add("effect", Effect(u.Effect));

            private static void Write(StringBuilder text, object value, int indent)
            {
                switch (value)
                {
                    case Obj obj:
                        if (obj.Count == 0)
                        {
                            text.Append("{}");
                            return;
                        }

                        text.Append("{\n");
                        for (var i = 0; i < obj.Count; i++)
                        {
                            text.Append(' ', (indent + 1) * 2).Append(Quote(obj[i].Key)).Append(": ");
                            Write(text, obj[i].Value, indent + 1);
                            text.Append(i + 1 == obj.Count ? "\n" : ",\n");
                        }

                        text.Append(' ', indent * 2).Append('}');
                        return;
                    case List<object> list:
                        if (list.Count == 0)
                        {
                            text.Append("[]");
                            return;
                        }

                        text.Append("[\n");
                        for (var i = 0; i < list.Count; i++)
                        {
                            text.Append(' ', (indent + 1) * 2);
                            Write(text, list[i], indent + 1);
                            text.Append(i + 1 == list.Count ? "\n" : ",\n");
                        }

                        text.Append(' ', indent * 2).Append(']');
                        return;
                    case string s:
                        text.Append(Quote(s));
                        return;
                    case long n:
                        text.Append(n.ToString(CultureInfo.InvariantCulture));
                        return;
                    default:
                        throw new InvalidOperationException("Unsupported canonical value " + value);
                }
            }

            private static string Quote(string value)
            {
                var text = new StringBuilder("\"");
                foreach (var c in value)
                {
                    switch (c)
                    {
                        case '"': text.Append("\\\""); break;
                        case '\\': text.Append("\\\\"); break;
                        case '\n': text.Append("\\n"); break;
                        case '\r': text.Append("\\r"); break;
                        case '\t': text.Append("\\t"); break;
                        default:
                            if (c < ' ')
                            {
                                text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                text.Append(c);
                            }

                            break;
                    }
                }

                return text.Append('"').ToString();
            }
        }
    }
}
