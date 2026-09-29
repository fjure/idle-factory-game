using System;
using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class RunCatalogTests
    {
        [Test]
        public void LiteralIronFixtureMatchesFr002()
        {
            var catalog = RunTestCatalogs.IronLoop();
            Assert.That(catalog.Resources["iron.ore"].BasePrice, Is.EqualTo(1));
            Assert.That(catalog.Resources["iron.bar"].BasePrice, Is.EqualTo(3));
            Assert.That(catalog.Resources["iron.plate"].BasePrice, Is.EqualTo(8));
            Assert.That(catalog.Resources["iron.gear"].BasePrice, Is.EqualTo(24));
            Assert.That(catalog.Recipes["recipe.mine-iron-ore"].Inputs, Is.Empty);
            Assert.That(catalog.Recipes["recipe.mine-iron-ore"].Outputs[0].Amount, Is.EqualTo(2));
            Assert.That(catalog.Recipes["recipe.mine-iron-ore"].DurationTicks, Is.EqualTo(20_000_000));
            Assert.That(catalog.Recipes["recipe.smelt-iron-bar"].Inputs[0].Amount, Is.EqualTo(2));
            Assert.That(catalog.Recipes["recipe.press-iron-plate"].DurationTicks, Is.EqualTo(40_000_000));
            Assert.That(catalog.Recipes["recipe.assemble-iron-gear"].DurationTicks, Is.EqualTo(50_000_000));
            Assert.That(catalog.StorageLevels[4].Capacity, Is.EqualTo(16000));
            Assert.That(catalog.FactoryLevels[4].SlotCount, Is.EqualTo(12));
        }

        [Test]
        public void CurvesIterateWithDeclaredRoundingAndBound()
        {
            Assert.That(new Curve(3, 1_500_000, "ceil").Precompute(3), Is.EqualTo(new long[] { 3, 5, 8 }));
            Assert.That(new Curve(3, 1_500_000, "floor").Precompute(3), Is.EqualTo(new long[] { 3, 4, 6 }));
            Assert.That(new Curve(ExactBound.Value, 1_000_001, "floor").Precompute(1)[0], Is.EqualTo(ExactBound.Value));
            Assert.That(() => new Curve(ExactBound.Value, 1_000_001, "floor").Precompute(2),
                Throws.ArgumentException.With.Message.Contains("curve.overflow"));
        }

        [Test]
        public void MachineTablesIncludeMilestonesInLevelOrder()
        {
            var m = new MachineDef("m", "M", "P", "r", 3, new Curve(10, 1_500_000, "ceil"),
                1_500_000, new[] { new SpeedMilestone(2, 2_000_000) }, new UnlockCondition("always"));
            Assert.That(m.CostTable, Is.EqualTo(new long[] { 10, 15, 23 }));
            Assert.That(m.SpeedTable, Is.EqualTo(new long[] { 0, 1_000_000, 3_000_000, 4_500_000 }));
        }

        [Test]
        public void RejectsRecipeAndBoundViolations()
        {
            var ore = new ResourceAmount("ore", 1);
            Assert.That(() => new MachineRecipe("mine", Array.Empty<ResourceAmount>(), new[] { ore }, 1), Throws.Nothing);
            Assert.That(() => new MachineRecipe("bad", Array.Empty<ResourceAmount>(), Array.Empty<ResourceAmount>(), 1), Throws.ArgumentException);
            Assert.That(() => new MachineRecipe("bad", new[] { ore, ore }, new[] { ore }, 1), Throws.ArgumentException);
            Assert.That(() => new ResourceAmount("ore", 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new MachineRecipe("bad", Array.Empty<ResourceAmount>(), new[] { ore }, 1_000_000_000_001L), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new RunStartDef(0, Array.Empty<string>(), 1_000_000, 1_000_000, 1_000_000, 1, 1_000_000), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void UnlockAndEffectVocabulariesAreClosed()
        {
            Assert.That(() => new UnlockCondition("other"), Throws.ArgumentException);
            Assert.That(() => new UnlockCondition("resourceProduced", "iron.ore"), Throws.Nothing);
            Assert.That(() => new EffectDef("unknown", "none", null, 1), Throws.ArgumentException);
            Assert.That(() => new EffectDef("machineSpeed", "machine", "machine.miner", 1_100_000), Throws.Nothing);
        }

        [Test]
        public void SourceCollectionsAndPublicViewsCannotMutateCatalog()
        {
            var source = new List<ResourceAmount> { new ResourceAmount("ore", 2) };
            var recipe = new MachineRecipe("mine", Array.Empty<ResourceAmount>(), source, 20_000_000);
            source.Clear();
            Assert.That(recipe.Outputs.Count, Is.EqualTo(1));
            Assert.That(recipe.Outputs, Is.Not.InstanceOf<ResourceAmount[]>());
            Assert.That(() => ((IList<ResourceAmount>)recipe.Outputs)[0] = new ResourceAmount("ore", 3), Throws.Exception);
            var catalog = RunTestCatalogs.IronLoop();
            Assert.That(() => ((IDictionary<string, ResourceDef>)catalog.Resources).Clear(), Throws.Exception);
        }

        [Test]
        public void TimedDurationAndLevelSequenceAreValidated()
        {
            var quantity = new Curve(1, 1_000_000, "ceil");
            var reward = new ContractRewardDef("cash", quantity);
            Assert.That(() => new ContractOfferDef("c", "timedBar", "iron.bar", quantity, null,
                new[] { reward }, new UnlockCondition("always")), Throws.ArgumentException);
            Assert.That(() => new ContractOfferDef("c", "plate", "iron.plate", quantity, 100,
                new[] { reward }, new UnlockCondition("always")), Throws.ArgumentException);
            Assert.That(() => new FactoryLevelDef(1, 13, 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new FactoryLevelDef(1, 4, 1), Throws.ArgumentException);
        }
    }
}
