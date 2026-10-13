using System;
using System.Collections.Generic;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// T360 (F5-R02, Option 1): the index-based unit fit (<see cref="RecipeIndex.TryFit"/>) and fused exchange
    /// (<see cref="GlobalInventory.TryExchangeIndexed"/>) give the same results as the command path
    /// (<c>SpeedComposer.TryCompletedUnits</c>, <c>GlobalInventory.TryExchange</c>): same result code, same units,
    /// same final stock and total, including every rejection (stock, capacity, value limit, locked resource), a
    /// resource that is both input and output, and a rejected exchange leaving the inventory untouched. Seeded, so
    /// the case set is fixed. The full-simulation bit-identity is covered by the committed W003 fixtures and the
    /// run-simulation equivalence suites.
    /// </summary>
    public sealed class RecipeIndexEquivalenceTests
    {
        private static readonly string[] Ids = { "a.ore", "b.bar", "c.plate", "d.gear" };

        private static GlobalInventory Inventory(IReadOnlyList<string> ids, long capacity, long[] stock)
        {
            var inv = new GlobalInventory(ids, capacity);
            var amounts = new List<ResourceAmount>();
            for (int i = 0; i < stock.Length; i++)
                if (stock[i] > 0) amounts.Add(new ResourceAmount(ids[i], stock[i]));
            Assert.That(inv.TryImport(amounts), Is.EqualTo(RunStateResult.Ok));
            return inv;
        }

        private static MachineRecipe Recipe(Random rng, int resources)
        {
            // Sides without duplicates; an input may reappear as an output (a catalyst).
            var inputs = new List<ResourceAmount>();
            var outputs = new List<ResourceAmount>();
            int inCount = rng.Next(0, 4);
            for (int i = 0; i < resources && inputs.Count < inCount; i++)
                if (rng.Next(2) == 0) inputs.Add(new ResourceAmount(Ids[i], Amount(rng)));
            for (int i = 0; i < resources; i++)
                if (rng.Next(2) == 0) outputs.Add(new ResourceAmount(Ids[i], Amount(rng)));
            if (outputs.Count == 0) outputs.Add(new ResourceAmount(Ids[rng.Next(resources)], Amount(rng)));
            return new MachineRecipe("r", inputs, outputs, 10_000_000);
        }

        private static long Amount(Random rng)
        {
            switch (rng.Next(12))
            {
                case 0: return 1_000_000_000_000_000_000L;
                case 1: return 400_000_000_000_000_000L;
                case 2: return 1L << 40;
                default: return rng.Next(1, 7);
            }
        }

        private static long Count(Random rng, long limit)
        {
            switch (rng.Next(10))
            {
                case 0: return 0;
                case 1: return Math.Min(limit, 1_000_000_000_000_000_000L);
                default: return rng.Next(0, 40);
            }
        }

        private static void AssertSameState(GlobalInventory expected, GlobalInventory actual, string context)
        {
            Assert.That(actual.Total, Is.EqualTo(expected.Total), context + ": total");
            for (int i = 0; i < expected.Count; i++)
                Assert.That(actual.UnitsAt(i), Is.EqualTo(expected.UnitsAt(i)), context + ": units " + expected.IdAt(i));
        }

        [Test]
        public void TryFitAndExchangeMatchTheCommandPath()
        {
            var rng = new Random(360);
            int exchanges = 0, rejected = 0, faults = 0;
            for (int round = 0; round < 6000; round++)
            {
                int resources = rng.Next(2, Ids.Length + 1);
                var ids = new List<string>();
                for (int i = 0; i < resources; i++) ids.Add(Ids[i]);
                long capacity = rng.Next(4) == 0 ? 1_000_000_000_000_000_000L : rng.Next(1, 500);
                var stock = new long[resources];
                long left = capacity;
                for (int i = 0; i < resources; i++)
                {
                    stock[i] = Math.Min(left, Count(rng, capacity));
                    left -= stock[i];
                }
                MachineRecipe recipe = Recipe(rng, resources);
                long batch = rng.Next(8) == 0 ? 0 : 1 + rng.Next(6);
                long factor = rng.Next(8) == 0 ? (rng.Next(2) == 0 ? 0 : 3_000_000_000_000_000_000L) : 1 + rng.Next(4);
                string context = "round " + round;

                GlobalInventory reference = Inventory(ids, capacity, stock);
                GlobalInventory indexed = Inventory(ids, capacity, stock);
                var index = new RecipeIndex();
                index.Rebuild(recipe, indexed);
                index.SetFactor(batch, factor);

                bool refOk = SpeedComposer.TryCompletedUnits(batch, recipe, factor, reference, out long refUnits);
                bool ixOk = index.TryFit(batch, indexed, out long ixUnits);
                Assert.That(ixOk, Is.EqualTo(refOk), context + ": fit ok");
                Assert.That(ixUnits, Is.EqualTo(refUnits), context + ": fit units");
                if (!refOk) { faults++; continue; }

                // Exchange the fitted units, and also an arbitrary unit count that may be rejected.
                long units = rng.Next(3) == 0 ? refUnits : Count(rng, long.MaxValue / 4) + 1;
                if (units < 1) units = 1;
                RunStateResult refResult = reference.TryExchange(recipe.Inputs, recipe.Outputs, units, factor);
                RunStateResult ixResult = indexed.TryExchangeIndexed(index, units, factor);
                Assert.That(ixResult, Is.EqualTo(refResult), context + ": exchange result");
                AssertSameState(reference, indexed, context + ": after exchange");
                if (refResult == RunStateResult.Ok) exchanges++; else rejected++;
            }
            // The generator must reach every branch.
            Assert.That(exchanges, Is.GreaterThan(500));
            Assert.That(rejected, Is.GreaterThan(500));
            Assert.That(faults, Is.GreaterThan(20));
        }

        [Test]
        public void ResourceThatIsInputAndOutputIsNettedLikeTheCommandPath()
        {
            var ids = new[] { "a.ore", "b.bar" };
            var recipe = new MachineRecipe("cat",
                new[] { new ResourceAmount("a.ore", 2), new ResourceAmount("b.bar", 1) },
                new[] { new ResourceAmount("a.ore", 3) }, 10_000_000);
            GlobalInventory reference = Inventory(ids, 10, new long[] { 6, 4 });
            GlobalInventory indexed = Inventory(ids, 10, new long[] { 6, 4 });
            var index = new RecipeIndex();
            index.Rebuild(recipe, indexed);
            index.SetFactor(3, 1);
            Assert.That(index.OutInput[0], Is.EqualTo(0));

            Assert.That(indexed.TryExchangeIndexed(index, 3, 1), Is.EqualTo(reference.TryExchange(recipe.Inputs, recipe.Outputs, 3, 1)));
            AssertSameState(reference, indexed, "catalyst");
        }

        [Test]
        public void LockedResourceIsRejectedAsLockedAndLeavesTheInventoryUntouched()
        {
            var recipe = new MachineRecipe("lock", new[] { new ResourceAmount("a.ore", 1) },
                new[] { new ResourceAmount("z.missing", 1) }, 10_000_000);
            GlobalInventory reference = Inventory(Ids, 100, new long[] { 5, 0, 0, 0 });
            GlobalInventory indexed = Inventory(Ids, 100, new long[] { 5, 0, 0, 0 });
            var index = new RecipeIndex();
            index.Rebuild(recipe, indexed);
            index.SetFactor(1, 1);
            Assert.That(index.OutIndex[0], Is.EqualTo(-1));
            RunStateResult expected = reference.TryExchange(recipe.Inputs, recipe.Outputs, 1, 1);
            Assert.That(indexed.TryExchangeIndexed(index, 1, 1), Is.EqualTo(expected));
            Assert.That(expected, Is.EqualTo(RunStateResult.ResourceLocked));
            AssertSameState(reference, indexed, "locked");
        }

        [Test]
        public void RebuildAfterAnUnlockFollowsTheNewInventoryPositions()
        {
            var recipe = new MachineRecipe("two", new[] { new ResourceAmount("c.plate", 1) },
                new[] { new ResourceAmount("d.gear", 1) }, 10_000_000);
            var inv = new GlobalInventory(new[] { "c.plate", "d.gear" }, 50);
            var index = new RecipeIndex();
            index.Rebuild(recipe, inv);
            Assert.That(index.InIndex[0], Is.EqualTo(0));
            Assert.That(index.OutIndex[0], Is.EqualTo(1));

            Assert.That(inv.Allow("a.ore"), Is.True);
            index.Rebuild(recipe, inv);
            Assert.That(index.InIndex[0], Is.EqualTo(1));
            Assert.That(index.OutIndex[0], Is.EqualTo(2));
        }
    }
}
