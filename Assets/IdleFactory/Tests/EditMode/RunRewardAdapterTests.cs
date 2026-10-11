using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The transitional reward adapter (T313, issue #375, analysis I1; removed by T320): the Cash table
    /// <c>ceil(quantity x basePrice x rewardRatioPpm / 10^6)</c> in checked math, the legacy Kind, the per-tier Item ownership, the
    /// reward order, the <see cref="ContractRewardDef.FromTable"/> support constructor and the all-or-nothing behavior around it.
    /// </summary>
    public sealed class RunRewardAdapterTests
    {
        private const string ShippedPath = "Assets/IdleFactory/Content/Iron/iron-factory-content.json";

        private static RunContentDocumentDto ShippedDocument()
        {
            var read = RunContentReader.Read(File.ReadAllBytes(ShippedPath));
            Assert.That(read.IsSuccess, Is.True, read.Diagnostic == null ? string.Empty : read.Diagnostic.Message);
            return read.Document;
        }

        private static RunCatalog ShippedCatalog()
        {
            var loaded = GameplayContentDispatcher.Load(File.ReadAllBytes(ShippedPath));
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Diagnostics.Select(d => d.Code + " " + d.Path)));
            return loaded.RunCatalog;
        }

        private static long[] Quantity(long baseValue, long growthPpm, string rounding) => new Curve(baseValue, growthPpm, rounding).Precompute(1024);

        // ---- the Cash table ---------------------------------------------------------------------------------

        [Test]
        public void CashTableIsTheCeilingOfQuantityTimesPriceTimesRatioAtEveryIndex()
        {
            var quantity = Quantity(100, 1_010_000, "ceil");
            foreach (var (price, ratio) in new[] { (8L, 1_750_000L), (24L, 1_666_666L), (3L, 1_700_000L), (1L, 1_000_000L), (7L, 1_234_567L), (13L, 2_000_000L), (5L, 333_333L), (0L, 1_500_000L) })
            {
                Assert.That(RunRewardAdapter.TryCashTable(quantity, price, ratio, out var table), Is.True, price + "/" + ratio);
                Assert.That(table.Length, Is.EqualTo(1024));
                for (var i = 0; i < 1024; i++)
                {
                    var numerator = new BigInteger(quantity[i]) * price * ratio;
                    var expected = (numerator + 999_999) / 1_000_000;
                    Assert.That(new BigInteger(table[i]), Is.EqualTo(expected), "index " + i + " price " + price + " ratio " + ratio);
                }
            }
        }

        [Test]
        public void RatioOfExactlyOneMillionPaysTheMarketValueAndAHigherRatioNeverPaysLess()
        {
            var quantity = Quantity(100, 1_010_000, "ceil");
            Assert.That(RunRewardAdapter.TryCashTable(quantity, 8, 1_000_000, out var market), Is.True);
            Assert.That(RunRewardAdapter.TryCashTable(quantity, 8, 1_500_000, out var more), Is.True);
            for (var i = 0; i < 1024; i++)
            {
                Assert.That(market[i], Is.EqualTo(quantity[i] * 8));
                Assert.That(more[i], Is.GreaterThanOrEqualTo(market[i]));
            }
        }

        [Test]
        public void TheCashTableFailsClosedAtTheExactBound()
        {
            // Last index of 10^13 x 1.01^1023 is about 2.6 x 10^17; times price 3 is 7.9 x 10^17 (fits); times ratio 1.5 is above 10^18.
            var quantity = Quantity(10_000_000_000_000L, 1_010_000, "ceil");
            Assert.That(RunRewardAdapter.TryCashTable(quantity, 3, 1_000_000, out _), Is.True, "the market value fits");
            Assert.That(RunRewardAdapter.TryCashTable(quantity, 3, 1_500_000, out var table), Is.False, "the reward does not fit");
            Assert.That(table, Is.Null, "nothing is published partially");

            var flat = Enumerable.Repeat(ExactBound.Value, 1024).ToArray();
            Assert.That(RunRewardAdapter.TryCashTable(flat, 1, 1_000_000, out _), Is.True, "10^18 x 1 x 1.0 is exactly the bound");
            Assert.That(RunRewardAdapter.TryCashTable(flat, 1, 1_000_001, out _), Is.False, "one ppm more leaves the bound");
            Assert.That(RunRewardAdapter.TryCashTable(flat, 2, 1_000_000, out _), Is.False, "the market value itself overflows");
        }

        [Test]
        public void TheCashTableRejectsMalformedArgumentsWithoutThrowing()
        {
            var quantity = Quantity(100, 1_010_000, "ceil");
            Assert.That(RunRewardAdapter.TryCashTable(null, 8, 1_000_000, out _), Is.False);
            Assert.That(RunRewardAdapter.TryCashTable(new long[10], 8, 1_000_000, out _), Is.False);
            Assert.That(RunRewardAdapter.TryCashTable(quantity, -1, 1_000_000, out _), Is.False);
            Assert.That(RunRewardAdapter.TryCashTable(quantity, ExactBound.Value + 1, 1_000_000, out _), Is.False);
            Assert.That(RunRewardAdapter.TryCashTable(quantity, 8, -1, out _), Is.False);
            Assert.That(RunRewardAdapter.TryCashTable(quantity, 8, long.MaxValue, out _), Is.False);
        }

        // ---- ContractRewardDef.FromTable --------------------------------------------------------------------

        [Test]
        public void FromTableCarriesTheTableAndNoCurve()
        {
            var values = Enumerable.Range(0, 1024).Select(i => (long)i * 3).ToArray();

            var reward = ContractRewardDef.FromTable("cash", values);

            Assert.That(reward.Kind, Is.EqualTo("cash"));
            Assert.That(reward.Curve, Is.Null);
            Assert.That(reward.ItemId, Is.Null);
            Assert.That(reward.OnCompletionIndices, Is.Empty);
            Assert.That(reward.ValueTable.ToArray(), Is.EqualTo(values));
            values[5] = 99;
            Assert.That(reward.ValueTable[5], Is.EqualTo(15), "the reward copies the table");
            Assert.That(ContractRewardDef.FromTable("rp", new long[1024]).Kind, Is.EqualTo("rp"));
        }

        [Test]
        public void FromTableRejectsWhatTheCurveConstructorWouldNotBuild()
        {
            Assert.That(() => ContractRewardDef.FromTable("item", new long[1024]), Throws.ArgumentException);
            Assert.That(() => ContractRewardDef.FromTable("trophy", new long[1024]), Throws.ArgumentException);
            Assert.That(() => ContractRewardDef.FromTable("cash", null), Throws.ArgumentNullException);
            Assert.That(() => ContractRewardDef.FromTable("cash", new long[1023]), Throws.ArgumentException);
            Assert.That(() => ContractRewardDef.FromTable("cash", new long[1025]), Throws.ArgumentException);
            var negative = new long[1024];
            negative[1000] = -1;
            Assert.That(() => ContractRewardDef.FromTable("cash", negative), Throws.InstanceOf<ArgumentOutOfRangeException>());
            var tooBig = new long[1024];
            tooBig[3] = ExactBound.Value + 1;
            Assert.That(() => ContractRewardDef.FromTable("cash", tooBig), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        // ---- the shipped catalog through the adapter ------------------------------------------------------------

        [Test]
        public void TheShippedCatalogPaysTheCeilingOfTheRatioRewardAndGrantsTheF003Items()
        {
            var catalog = ShippedCatalog();
            var plate = catalog.ContractOffers["contract.plate"];
            var cash = plate.Rewards.Single(r => r.Kind == "cash");

            Assert.That(cash.Curve, Is.Null, "an adapter reward has a table, not a curve");
            Assert.That(cash.ValueTable.Take(6).ToArray(), Is.EqualTo(new long[] { 1400, 1414, 1442, 1470, 1498, 1526 }));
            Assert.That(catalog.ContractOffers["contract.gear"].Rewards.Single(r => r.Kind == "cash").ValueTable.Take(4).ToArray(), Is.EqualTo(new long[] { 4000, 4040, 4120, 4200 }));
            Assert.That(catalog.ContractOffers["contract.timed-bar"].Rewards.Single(r => r.Kind == "cash").ValueTable.Take(4).ToArray(), Is.EqualTo(new long[] { 510, 516, 526, 536 }));
            Assert.That(plate.Rewards.Single(r => r.Kind == "rp").ValueTable[0], Is.EqualTo(4));
            Assert.That(plate.Rewards.Single(r => r.Kind == "rp").Curve, Is.Not.Null, "RP stays a curve");
        }

        [Test]
        public void DeliveringThroughTheAdapterPaysTheTableCashAndGrantsTheItemAtItsIndex()
        {
            var catalog = ShippedCatalog();
            var gear = catalog.ContractOffers["contract.gear"];

            // DueItems is the F003 path's Item rule: the owner offer of tier 3 grants the Overclock Core at completion indices 1, 5 and 10.
            Assert.That(RunCommands.DueItems(gear, 0), Is.Empty);
            Assert.That(RunCommands.DueItems(gear, 1).Select(i => i.Id), Is.EqualTo(new[] { "item.overclock-core" }));
            Assert.That(RunCommands.DueItems(gear, 5).Select(i => i.Id), Is.EqualTo(new[] { "item.overclock-core" }));
            Assert.That(RunCommands.DueItems(gear, 6), Is.Empty);
            Assert.That(RunCommands.DueItems(catalog.ContractOffers["contract.timed-bar"], 0).Select(i => i.Id), Is.EqualTo(new[] { "item.diamond-drill-head" }));
        }

        // ---- legacy Kind ------------------------------------------------------------------------------------

        [Test]
        public void KindIsTimedBarForATimedOfferAndPlateOrGearByResourcePriceOtherwise()
        {
            var document = ShippedDocument();

            var kinds = RunRewardAdapter.DeriveKinds(document);

            var byId = document.Contracts.Select((c, i) => (c.Id, Kind: kinds[i])).ToDictionary(x => x.Id, x => x.Kind);
            Assert.That(byId["contract.timed-bar"], Is.EqualTo("timedBar"));
            Assert.That(byId["contract.plate"], Is.EqualTo("plate"));
            Assert.That(byId["contract.gear"], Is.EqualTo("gear"));
        }

        [Test]
        public void KindDoesNotDependOnTheOrderOfTheContracts()
        {
            var document = ShippedDocument();
            document.Contracts.Reverse();

            var kinds = RunRewardAdapter.DeriveKinds(document);

            var byId = document.Contracts.Select((c, i) => (c.Id, Kind: kinds[i])).ToDictionary(x => x.Id, x => x.Kind);
            Assert.That(byId["contract.plate"], Is.EqualTo("plate"));
            Assert.That(byId["contract.gear"], Is.EqualTo("gear"));
            Assert.That(byId["contract.timed-bar"], Is.EqualTo("timedBar"));
        }

        [Test]
        public void AnyTimedOfferIsTimedBarAndASingleUntimedResourceIsPlate()
        {
            var document = ShippedDocument();
            document.Contracts.RemoveAll(c => c.Id == "contract.plate");
            document.Contracts.Single(c => c.Id == "contract.gear").DurationMs = 1000;

            var kinds = RunRewardAdapter.DeriveKinds(document);

            Assert.That(kinds, Is.EqualTo(new[] { "timedBar", "timedBar" }));
        }

        // ---- Items -----------------------------------------------------------------------------------------------

        [Test]
        public void ItemsGoToTheStandingDeliveryOfferThatOwnsTheirTier()
        {
            var document = ShippedDocument();

            var rewards = RunRewardAdapter.ItemRewards(document);

            var byId = document.Contracts.Select((c, i) => (c.Id, Rewards: rewards[i])).ToDictionary(x => x.Id, x => x.Rewards);
            Assert.That(byId["contract.timed-bar"].Select(r => r.ItemId), Is.EqualTo(new[] { "item.diamond-drill-head" }));
            Assert.That(byId["contract.plate"].Select(r => r.ItemId), Is.EqualTo(new[] { "item.flux-catalyst" }));
            Assert.That(byId["contract.gear"].Select(r => r.ItemId), Is.EqualTo(new[] { "item.overclock-core" }));
            Assert.That(byId["contract.gear"].Single().OnCompletionIndices, Is.EqualTo(new[] { 1, 5, 10 }));
        }

        [Test]
        public void ItemOwnershipDoesNotDependOnTheOrderOfTheContractsOrTheLadder()
        {
            var document = ShippedDocument();
            document.Contracts.Reverse();
            document.ContractItemLadder.Entries.Reverse();

            var rewards = RunRewardAdapter.ItemRewards(document);

            var byId = document.Contracts.Select((c, i) => (c.Id, Rewards: rewards[i])).ToDictionary(x => x.Id, x => x.Rewards);
            Assert.That(byId["contract.timed-bar"].Single().OnCompletionIndices, Is.EqualTo(new[] { 0, 3, 6, 10 }));
            Assert.That(byId["contract.plate"].Single().OnCompletionIndices, Is.EqualTo(new[] { 1, 4, 9 }));
            Assert.That(byId["contract.gear"].Single().OnCompletionIndices, Is.EqualTo(new[] { 1, 5, 10 }));
        }

        [Test]
        public void ATierBeyondTheLastOfferGoesToTheLastOfferAndOtherOffersGetNoItems()
        {
            var document = ShippedDocument();
            document.Contracts.RemoveAll(c => c.Id == "contract.gear");
            document.Contracts.Single(c => c.Id == "contract.timed-bar").Class = "bonus";

            var rewards = RunRewardAdapter.ItemRewards(document);

            var byId = document.Contracts.Select((c, i) => (c.Id, Rewards: rewards[i])).ToDictionary(x => x.Id, x => x.Rewards);
            Assert.That(byId["contract.timed-bar"], Is.Empty, "a bonus offer owns no tier");
            Assert.That(byId["contract.plate"].Select(r => r.ItemId).OrderBy(x => x, StringComparer.Ordinal),
                Is.EqualTo(new[] { "item.diamond-drill-head", "item.flux-catalyst", "item.overclock-core" }), "the only standing delivery offer owns every tier");
        }

        [Test]
        public void AnEmptyLadderGivesNoItemRewards()
        {
            var document = ShippedDocument();
            document.ContractItemLadder.Entries.Clear();

            Assert.That(RunRewardAdapter.ItemRewards(document).All(list => list.Count == 0), Is.True);
        }

        // ---- the built offers --------------------------------------------------------------------------------------

        [Test]
        public void EveryBuiltOfferListsCashThenItemsThenRp()
        {
            foreach (var offer in ShippedCatalog().ContractOffers.Values)
            {
                Assert.That(offer.Rewards.Select(r => r.Kind).ToArray(), Is.EqualTo(new[] { "cash", "item", "rp" }), offer.Id);
                Assert.That(offer.Rewards.All(r => r.ValueTable == null || r.ValueTable.Count == 1024), Is.True, offer.Id);
            }
        }

        [Test]
        public void ARewardTableThatDoesNotFitRejectsTheWholeDocumentInsteadOfThrowing()
        {
            var document = ShippedDocument();
            document.Contracts.Single(c => c.Id == "contract.timed-bar").Quantity.Base = 10_000_000_000_000L;

            var built = RunCatalogBuilder.Build(document);

            Assert.That(built.IsSuccess, Is.False);
            Assert.That(built.Catalog, Is.Null, "nothing is published partially");
            Assert.That(built.Diagnostics.Select(d => d.Code), Does.Contain("contract.reward.ratio"));
            Assert.That(built.Diagnostics.Select(d => d.Code), Does.Not.Contain(RunCatalogBuilder.InternalErrorCode));
        }
    }
}
