using System;
using System.Collections.Generic;
using System.Linq;
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

        [Test]
        public void EffectTargetsMatchTheClosedMatrix()
        {
            Assert.That(() => new EffectDef("machineSpeed", "allMachines", null, 1_100_000), Throws.Nothing);
            Assert.That(() => new EffectDef("machineSpeed", "factory", null, 1_100_000), Throws.ArgumentException);
            Assert.That(() => new EffectDef("machineSpeed", "machineType", null, 1_100_000), Throws.ArgumentException);
            Assert.That(() => new EffectDef("machineOutput", "machineType", null, 5), Throws.Nothing);
            Assert.That(() => new EffectDef("machineOutput", "machine", "machine.miner", 5), Throws.Nothing);
            Assert.That(() => new EffectDef("machineOutput", "allMachines", null, 5), Throws.ArgumentException);
            Assert.That(() => new EffectDef("salePrice", "allResources", null, 1_100_000), Throws.Nothing);
            Assert.That(() => new EffectDef("salePrice", "resource", "iron.ore", 1_100_000), Throws.Nothing);
            Assert.That(() => new EffectDef("salePrice", "machine", "machine.miner", 1_100_000), Throws.ArgumentException);
            Assert.That(() => new EffectDef("upgradeCost", "allMachines", null, 900_000), Throws.Nothing);
            Assert.That(() => new EffectDef("upgradeCost", "expansion", null, 900_000), Throws.Nothing);
            Assert.That(() => new EffectDef("upgradeCost", "resource", "iron.ore", 900_000), Throws.ArgumentException);
            Assert.That(() => new EffectDef("startingCash", "none", null, 1), Throws.Nothing);
            Assert.That(() => new EffectDef("startingStorageLevel", "none", null, 1), Throws.Nothing);
            Assert.That(() => new EffectDef("startingCash", "allMachines", null, 1), Throws.ArgumentException);
        }

        [Test]
        public void EffectSourcesAndMachineOutputTargetsAreRestricted()
        {
            var cost = new Curve(1, Curve.Ppm, "ceil");
            var startingCash = new EffectDef("startingCash", "none", null, 1);
            var startingStorage = new EffectDef("startingStorageLevel", "none", null, 1);
            var price = new EffectDef("salePrice", "allResources", null, 1_100_000);
            var outputForItem = new EffectDef("machineOutput", "machineType", null, 5);
            var outputForResearch = new EffectDef("machineOutput", "machine", "machine.miner", 5);
            Assert.That(() => new ItemDef("i", 1, "I", "P", 1, "machineType", new[] { "machine.miner" }, startingCash), Throws.ArgumentException);
            Assert.That(() => new ItemDef("i", 1, "I", "P", 1, "machineType", new[] { "machine.miner" }, price), Throws.ArgumentException);
            Assert.That(() => new ItemDef("i", 1, "I", "P", 1, "machineType", new[] { "machine.miner" }, outputForResearch), Throws.ArgumentException);
            Assert.That(() => new ItemDef("i", 1, "I", "P", 1, "machineType", new[] { "machine.miner" }, outputForItem), Throws.Nothing);
            Assert.That(() => new ResearchDef("r", "R", 1, cost, Array.Empty<ResearchPrerequisite>(), startingStorage), Throws.ArgumentException);
            Assert.That(() => new ResearchDef("r", "R", 1, cost, Array.Empty<ResearchPrerequisite>(), startingCash), Throws.ArgumentException);
            Assert.That(() => new ResearchDef("r", "R", 1, cost, Array.Empty<ResearchPrerequisite>(), outputForItem), Throws.ArgumentException);
            Assert.That(() => new ResearchDef("r", "R", 1, cost, Array.Empty<ResearchPrerequisite>(), outputForResearch), Throws.Nothing);
            Assert.That(() => new IndustryUpgradeDef("u", "U", 1, cost, outputForResearch), Throws.ArgumentException);
            Assert.That(() => new IndustryUpgradeDef("u", "U", 1, cost, startingCash), Throws.Nothing);
        }

        [Test]
        public void OutputFactorUsesRunStartBoundRatherThanFour()
        {
            Assert.That(RunTestCatalogs.IronLoop(8, 8).Items["item.drill"].Effect.Value, Is.EqualTo(8));
            Assert.That(() => RunTestCatalogs.IronLoop(7, 8), Throws.ArgumentException.With.Message.Contains("maxCombinedOutputFactor"));
            Assert.That(() => new EffectDef("machineOutput", "machineType", null, 1_000_000), Throws.Nothing);
            Assert.That(() => new EffectDef("machineOutput", "machineType", null, 1_000_001), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void NestedListsAccept256AndReject257()
        {
            var outputs = Enumerable.Range(0, 257).Select(i => new ResourceAmount("r" + i, 1)).ToArray();
            Assert.That(new MachineRecipe("r", Array.Empty<ResourceAmount>(), outputs.Take(256), 1).Outputs.Count, Is.EqualTo(256));
            Assert.That(() => new MachineRecipe("r", Array.Empty<ResourceAmount>(), outputs, 1), Throws.ArgumentException);
            var milestones = Enumerable.Range(2, 257).Select(i => new SpeedMilestone(i, 1_000_001)).ToArray();
            var flat = new Curve(1, Curve.Ppm, "floor");
            Assert.That(new MachineDef("m", "M", "P", "r", 258, flat, Curve.Ppm,
                milestones.Take(256), new UnlockCondition("always")).Milestones.Count, Is.EqualTo(256));
            Assert.That(() => new MachineDef("m", "M", "P", "r", 258, flat, Curve.Ppm,
                milestones, new UnlockCondition("always")), Throws.ArgumentException);
            var reward = new ContractRewardDef("cash", flat);
            Assert.That(new ContractOfferDef("c", "plate", "r", flat, null,
                Enumerable.Repeat(reward, 256), new UnlockCondition("always")).Rewards.Count, Is.EqualTo(256));
            Assert.That(() => new ContractOfferDef("c", "plate", "r", flat, null,
                Enumerable.Repeat(reward, 257), new UnlockCondition("always")), Throws.ArgumentException);
            Assert.That(new ContractRewardDef("item", null, "item", Enumerable.Range(0, 256)).OnCompletionIndices.Count, Is.EqualTo(256));
            Assert.That(() => new ContractRewardDef("item", null, "item", Enumerable.Range(0, 257)), Throws.ArgumentException);
            var ids = Enumerable.Range(0, 257).Select(i => "m" + i).ToArray();
            var effect = new EffectDef("machineOutput", "machineType", null, 2);
            Assert.That(new ItemDef("i", 1, "I", "P", 1, "machineType", ids.Take(256), effect).AllowedMachineIds.Count, Is.EqualTo(256));
            Assert.That(() => new ItemDef("i", 1, "I", "P", 1, "machineType", ids, effect), Throws.ArgumentException);
            var prereqs = Enumerable.Range(0, 257).Select(i => new ResearchPrerequisite("r" + i, 1)).ToArray();
            Assert.That(new ResearchDef("r", "R", 1, flat, prereqs.Take(256), new EffectDef("machineSpeed", "allMachines", null, 1_100_000)).Prerequisites.Count, Is.EqualTo(256));
            Assert.That(() => new ResearchDef("r", "R", 1, flat, prereqs, new EffectDef("machineSpeed", "allMachines", null, 1_100_000)), Throws.ArgumentException);
            Assert.That(new RunStartDef(0, ids.Take(256), 0, 1_000_000, 1_000_000, 1, 1_000_000).InitialUnlocks.Count, Is.EqualTo(256));
            Assert.That(() => new RunStartDef(0, ids, 0, 1_000_000, 1_000_000, 1, 1_000_000), Throws.ArgumentException);
        }
    }
}
