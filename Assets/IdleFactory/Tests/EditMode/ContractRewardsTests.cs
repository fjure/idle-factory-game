using System;
using System.IO;
using System.Linq;
using System.Numerics;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T320 (issue #387): the contract reward formula, the run-wide Item ladder with its per-run cap and 1,024 bound, the sale-price modifier read at
    /// the completion instant (ECO-2), the run-wide completion total, the removal of the transitional adapter and the builder's ladder sort.
    /// </summary>
    public sealed class ContractRewardsTests
    {
        private const string Boost = "item.boost";
        private const string Drill = "item.drill";
        private const string Turbo = "item.turbo";
        private const long Ppm = 1_000_000;

        private static ContractItemLadder Ladder(long cap, params (long index, string item)[] entries) =>
            new ContractItemLadder(entries.Select(e => new ContractItemLadderEntry(e.index, e.item)), cap);

        // ---- Cash formula ------------------------------------------------------------------------------------

        [Test]
        public void CashIsOneSaleBasisFloorThenOneRatioFloor()
        {
            foreach (var (units, price, ppm, ratio) in new[]
            {
                (3L, 7L, 1_500_000L, 1_666_666L), (2400L / 24, 24L, 1_000_000L, 1_666_666L), (100L, 24L, 1_000_000L, 1_666_666L), (5L, 8L, 1_234_567L, 1_750_000L),
                (1L, 1L, 999_999L, 1_000_000L), (123_456L, 13L, 1_100_000L, 1_500_000L), (0L, 5L, 1_000_000L, 1_500_000L),
            })
            {
                Assert.That(ContractRewards.TryCash(units, price, ppm, ratio, out long cash), Is.True);
                BigInteger basis = new BigInteger(units) * price * ppm / Ppm;
                Assert.That(new BigInteger(cash), Is.EqualTo(basis * ratio / Ppm), units + "/" + price + "/" + ppm + "/" + ratio);
            }
        }

        [Test]
        public void TheRatioFloorReplacesTheAdapterCeiling_AndTheTwoFloorsDifferFromOne()
        {
            // The adapter's ceil gave 4000 for 2400 x 1,666,666 ppm; the floor gives 3999.
            Assert.That(ContractRewards.TryCash(100, 24, Ppm, 1_666_666, out long gear), Is.True);
            Assert.That(gear, Is.EqualTo(3_999));
            // 21 x 1.5 = 31.5 floors to 31 first; 31 x 1.666666 = 51.67 floors to 51 (a single floor of 52.49 would give 52).
            Assert.That(ContractRewards.TryCash(3, 7, 1_500_000, 1_666_666, out long two), Is.True);
            Assert.That(two, Is.EqualTo(51));
        }

        [Test]
        public void TheRewardBoundaryIsTenToTheEighteenth()
        {
            Assert.That(ContractRewards.TryCash(1_000_000_000_000L, 1_000_000L, Ppm, Ppm, out long exact), Is.True, "basis and reward exactly 10^18");
            Assert.That(exact, Is.EqualTo(ExactBound.Value));
            Assert.That(ContractRewards.TryCash(1_000_000_000_000L, 1_000_000L, Ppm, 1_000_001, out long over), Is.False, "one ppm of ratio more leaves the bound");
            Assert.That(over, Is.Zero, "nothing is clamped");
            Assert.That(ContractRewards.TryCash(1_000_000_000_001L, 1_000_000L, Ppm, 0, out _), Is.False, "the sale basis itself passes 10^18");
            Assert.That(ContractRewards.TryCash(1_000_000_000_000L, 1_000_000L, 1_000_001, 999_999, out _), Is.False, "the modifier lifts the basis over the bound");
            Assert.That(ContractRewards.TryCash(1_000_000, 1_000_000, Ppm, 2_000_000_000_000L, out _), Is.False, "a huge ratio passes the bound");
            Assert.That(ContractRewards.TryCash(-1, 10, Ppm, Ppm, out _), Is.False);
        }

        // ---- the ladder ---------------------------------------------------------------------------------------

        private static string[] Due(ContractItemLadder ladder, long index) => ContractRewards.DueItems(ladder, index).Select(i => i.Id).ToArray();

        [Test]
        public void ItemsAreGrantedWhereTheRunWideIndexEqualsTheEntryIndex_InItemIdOrder()
        {
            var ladder = Ladder(10, (0, Boost), (2, Turbo), (2, Drill), (5, Boost));
            Assert.That(Due(ladder, 0), Is.EqualTo(new[] { Boost }));
            Assert.That(Due(ladder, 1), Is.Empty);
            Assert.That(Due(ladder, 2), Is.EqualTo(new[] { Drill, Turbo }), "ordinal Item-ID order");
            Assert.That(Due(ladder, 5), Is.EqualTo(new[] { Boost }));
            Assert.That(Due(ladder, 6), Is.Empty);
            Assert.That(Due(null, 0), Is.Empty, "no ladder, no Item");
        }

        [Test]
        public void ThePerRunCapCountsEntriesInIndexThenItemOrder_AndDocumentOrderDoesNotMatter()
        {
            var ordered = new[] { (0L, Boost), (1L, Drill), (1L, Turbo), (3L, Boost) };
            foreach (var cap in new long[] { 0, 1, 2, 3, 4, 5 })
            {
                var forward = Ladder(cap, ordered);
                var reversed = Ladder(cap, ordered.Reverse().ToArray());
                for (long index = 0; index <= 4; index++)
                    Assert.That(Due(reversed, index), Is.EqualTo(Due(forward, index)), "cap " + cap + " index " + index);
            }

            // Cap 3: Boost@0, Drill@1, Turbo@1 are granted; Boost@3 is the fourth and is not. Cap 2 cuts Turbo@1.
            Assert.That(Due(Ladder(3, ordered), 1), Is.EqualTo(new[] { Drill, Turbo }));
            Assert.That(Due(Ladder(3, ordered), 3), Is.Empty);
            Assert.That(Due(Ladder(2, ordered), 1), Is.EqualTo(new[] { Drill }));
            Assert.That(Due(Ladder(0, ordered), 0), Is.Empty);
            Assert.That(Due(Ladder(4, ordered), 3), Is.EqualTo(new[] { Boost }));
        }

        [Test]
        public void TheLadderStopsAtTheBound1024()
        {
            var ladder = Ladder(10, (1023, Boost), (1024, Drill));
            Assert.That(Due(ladder, 1023), Is.EqualTo(new[] { Boost }), "index 1,023 is the last one");
            Assert.That(Due(ladder, 1024), Is.Empty, "index 1,024 never grants");
            Assert.That(Due(ladder, -1), Is.Empty);
        }

        // ---- delivery through the commands ---------------------------------------------------------------------

        private const string Plate = "contract.plate";
        private const string Gear = "contract.gear";

        private static RunCommandFixtures.Rig Rig(ContractItemLadder ladder, ResearchDef[] research = null, long cash = 1000)
        {
            var always = new UnlockCondition("always");
            var o = new RunCommandFixtures.Options
            {
                Offers = new[]
                {
                    new ContractOfferDef(Plate, "plate", "iron.plate", new Curve(5, 1_010_000, "ceil"), null,
                        new[] { new ContractRewardDef("rp", new Curve(2, 1_000_000, "floor")) }, always, rewardRatioPpm: 1_500_000),
                    new ContractOfferDef(Gear, "gear", "iron.gear", new Curve(3, 1_000_000, "ceil"), null,
                        new[] { new ContractRewardDef("rp", new Curve(0, 1_000_000, "floor")) }, always, rewardRatioPpm: 1_500_000),
                },
                Items = new[]
                {
                    new ItemDef(Boost, 1, "Boost", "F003-ITEM-BOOST", 60_000, "factory", new string[0], new EffectDef("machineSpeed", "allMachines", null, 1_500_000)),
                    new ItemDef(Drill, 2, "Drill", "F003-ITEM-DRILL", 60_000, "factory", new string[0], new EffectDef("machineSpeed", "allMachines", null, 1_100_000)),
                    new ItemDef(Turbo, 3, "Turbo", "F003-ITEM-TURBO", 60_000, "factory", new string[0], new EffectDef("machineSpeed", "allMachines", null, 1_200_000)),
                },
                Ladder = ladder,
                Research = research,
            };
            var rig = RunCommandFixtures.NewRig(o, cash, rp: 100);
            rig.Advance(0);
            return rig;
        }

        private static ContractDeliveryResult Complete(RunCommandFixtures.Rig rig, string offerId, string resource)
        {
            Assert.That(rig.Commands.AcceptContract(offerId).IsSuccess, Is.True);
            RunSimHarness.Stock(rig.State, resource, rig.State.Contracts.Active.RequiredUnits);
            RunCommandResult<ContractDeliveryResult> r = rig.Commands.DeliverContract();
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            return r.Value;
        }

        [Test]
        public void EveryOfferCountsOnTheRunWideIndex_AndTheTotalFollowsTheCompletions()
        {
            var rig = Rig(Ladder(10, (0, Boost), (1, Drill), (2, Turbo), (3, Boost)));
            var board = rig.State.Contracts;
            Assert.That(board.RunCompletionTotal, Is.Zero);

            Assert.That(Complete(rig, Plate, "iron.plate").ItemsGranted.Select(i => i.Id), Is.EqualTo(new[] { Boost }));
            Assert.That(Complete(rig, Gear, "iron.gear").ItemsGranted.Select(i => i.Id), Is.EqualTo(new[] { Drill }), "a different offer continues the same index");
            Assert.That(Complete(rig, Plate, "iron.plate").ItemsGranted.Select(i => i.Id), Is.EqualTo(new[] { Turbo }));
            Assert.That(Complete(rig, Gear, "iron.gear").ItemsGranted.Select(i => i.Id), Is.EqualTo(new[] { Boost }));
            Assert.That(board.RunCompletionTotal, Is.EqualTo(4));
            Assert.That(board.CompletionCount(Plate) + board.CompletionCount(Gear), Is.EqualTo(4));
            Assert.That(Complete(rig, Plate, "iron.plate").ItemsGranted, Is.Empty);
            Assert.That(rig.Session.Permanent.ItemCount(Boost), Is.EqualTo(2));
        }

        [Test]
        public void AbandoningAndExpiringDoNotAdvanceTheTotal_AndARejectedDeliveryChangesNothing()
        {
            var rig = Rig(Ladder(10, (0, Boost)));
            Assert.That(rig.Commands.AcceptContract(Plate).IsSuccess, Is.True);
            Assert.That(rig.Commands.AbandonContract().IsSuccess, Is.True);
            Assert.That(rig.State.Contracts.RunCompletionTotal, Is.Zero);
            Assert.That(rig.Commands.AcceptContract(Plate).IsSuccess, Is.True);
            Assert.That(rig.Commands.DeliverContract().ReasonCode, Is.EqualTo(RunReasons.StockInsufficient));
            Assert.That(rig.State.Contracts.RunCompletionTotal, Is.Zero);
            Assert.That(rig.Session.Permanent.ItemCount(Boost), Is.Zero);
        }

        [Test]
        public void RestoringCompletionCountsRaisesTheTotal_AndNeverLowersIt()
        {
            var rig = Rig(Ladder(10, (0, Boost)));
            var board = rig.State.Contracts;
            Assert.That(board.TrySetCompletionCount(Plate, 3), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.TrySetCompletionCount(Gear, 2), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.RunCompletionTotal, Is.EqualTo(5), "derived from the counts, the V2 load rule");
            Assert.That(board.TrySetCompletionCount(Plate, 1), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.RunCompletionTotal, Is.EqualTo(5), "a lowered count keeps the Items already granted from being granted again");
        }

        [Test]
        public void ResearchBoughtBetweenAcceptAndDeliveryRaisesTheRewardAtTheDeliveryInstant()
        {
            var price = new ResearchDef("research.bargain", "Bargain", 2, new Curve(5, 1_500_000, "ceil"), new ResearchPrerequisite[0],
                new EffectDef("salePrice", "allResources", null, 1_500_000));
            var rig = Rig(Ladder(10), new[] { price });
            Assert.That(rig.Commands.AcceptContract(Plate).IsSuccess, Is.True);
            long required = rig.State.Contracts.Active.RequiredUnits;
            Assert.That(required, Is.EqualTo(5));
            Assert.That(rig.Commands.PurchaseResearch("research.bargain").IsSuccess, Is.True);
            RunSimHarness.Stock(rig.State, "iron.plate", required);
            long before = rig.State.Cash;

            RunCommandResult<ContractDeliveryResult> delivered = rig.Commands.DeliverContract();

            Assert.That(delivered.IsSuccess, Is.True, delivered.ReasonCode);
            // basis floor(5 x 8 x 1.5) = 60, reward floor(60 x 1.5) = 90; at acceptance the modifier was 1.0 and the reward would have been 60.
            Assert.That(delivered.Value.CashReward, Is.EqualTo(90));
            Assert.That(rig.State.Cash - before, Is.EqualTo(90));
            Assert.That(delivered.Value.UnitsConsumed, Is.EqualTo(required), "the requirement stays the one fixed at acceptance");
            var snapshot = RunSnapshots.Contracts(rig.Session);
            Assert.That(snapshot.Offers.Single(x => x.OfferId == Plate).Rewards.First(r => r.Kind == "cash").Amount, Is.EqualTo(108), "the next preview at the new modifier: 6 units, basis 72, reward floor(72 x 1.5)");
        }

        [Test]
        public void ARunResetStartsWithoutCompletionsAndWithARunWideTotalOfZero()
        {
            var rig = Rig(Ladder(10, (0, Boost)));
            Complete(rig, Plate, "iron.plate");
            Complete(rig, Gear, "iron.gear");
            Assert.That(rig.State.Contracts.RunCompletionTotal, Is.EqualTo(2));

            // The prestige transition builds the next run with NextRunStart.CreateRun (a new RunState and a new ContractBoard).
            RunState next = NextRunStart.CreateRun(rig.Catalog, rig.Session.Permanent, Guid.NewGuid(), 2, rig.State.RunId);

            Assert.That(next.Contracts.CompletionCounts(), Is.Empty);
            Assert.That(next.Contracts.RunCompletionTotal, Is.Zero, "the total resets together with the counts");
            Assert.That(rig.State.Contracts.RunCompletionTotal, Is.EqualTo(2), "the ended run is not touched");
        }

        // ---- the adapter and the builder -----------------------------------------------------------------------

        private const string ShippedPath = "Assets/IdleFactory/Content/Iron/iron-factory-content.json";

        [Test]
        public void TheAdapterAndFromTableAreGone()
        {
            Assert.That(typeof(ContractRewardDef).GetMethod("FromTable"), Is.Null);
            Assert.That(typeof(RunCatalogBuilder).Assembly.GetType("IdleFactory.Unity.Content.RunRewardAdapter"), Is.Null);
        }

        [Test]
        public void BuiltOffersCarryOnlyTheirRpCurve_AndTheBuilderSortsTheLadderForAnyDocumentOrder()
        {
            var read = RunContentReader.Read(File.ReadAllBytes(ShippedPath));
            Assert.That(read.IsSuccess, Is.True);
            var shuffled = RunContentReader.Read(File.ReadAllBytes(ShippedPath)).Document;
            shuffled.ContractItemLadder.Entries.Reverse();

            var a = RunCatalogBuilder.Build(read.Document);
            var b = RunCatalogBuilder.Build(shuffled);

            Assert.That(a.IsSuccess && b.IsSuccess, Is.True);
            string Key(RunCatalog c) => string.Join(",", c.ContractItemLadder.Entries.Select(e => e.Index + ":" + e.ItemId));
            Assert.That(Key(b.Catalog), Is.EqualTo(Key(a.Catalog)));
            var entries = a.Catalog.ContractItemLadder.Entries;
            for (int i = 1; i < entries.Count; i++)
                Assert.That(entries[i - 1].Index < entries[i].Index || (entries[i - 1].Index == entries[i].Index &&
                    string.CompareOrdinal(entries[i - 1].ItemId, entries[i].ItemId) < 0), Is.True, "ascending by (index, itemId)");
            foreach (var offer in a.Catalog.ContractOffers.Values)
                Assert.That(offer.Rewards.Select(r => r.Kind), Is.EqualTo(new[] { "rp" }), offer.Id);
        }

        [Test]
        public void KindLabelStaysTimedBarPlateOrGearByResourcePrice()
        {
            var catalog = RunCatalogBuilder.Build(RunContentReader.Read(File.ReadAllBytes(ShippedPath)).Document).Catalog;
            Assert.That(catalog.ContractOffers["contract.timed-bar"].Kind, Is.EqualTo("timedBar"));
            Assert.That(catalog.ContractOffers["contract.plate"].Kind, Is.EqualTo("plate"));
            Assert.That(catalog.ContractOffers["contract.gear"].Kind, Is.EqualTo("gear"));
        }

        [Test]
        public void ARewardThatDoesNotFitRejectsTheWholeDocumentInsteadOfThrowing()
        {
            var document = RunContentReader.Read(File.ReadAllBytes(ShippedPath)).Document;
            document.Contracts.Single(c => c.Id == "contract.timed-bar").Quantity.Base = 10_000_000_000_000L;

            var built = RunCatalogBuilder.Build(document);

            Assert.That(built.IsSuccess, Is.False);
            Assert.That(built.Catalog, Is.Null);
            Assert.That(built.Diagnostics.Select(d => d.Code), Does.Contain("contract.reward.ratio"));
            Assert.That(built.Diagnostics.Select(d => d.Code), Does.Not.Contain(RunCatalogBuilder.InternalErrorCode));
        }
    }
}
