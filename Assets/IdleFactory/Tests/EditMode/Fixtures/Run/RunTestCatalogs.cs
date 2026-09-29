using IdleFactory.Run.Content;

namespace IdleFactory.Tests.EditMode.Fixtures.Run
{
    /// <summary>Literal FR-002 Iron loop; independent of the JSON reader and versioned content file.</summary>
    public static class RunTestCatalogs
    {
        public static RunCatalog IronLoop(long maxCombinedOutputFactor = 4, long itemOutputFactor = 0)
        {
            var always = new UnlockCondition("always");
            var resources = new[]
            {
                new ResourceDef("iron.ore", "Iron Ore", "F003-RES-IRON-ORE", 1, always),
                new ResourceDef("iron.bar", "Iron Bar", "F003-RES-IRON-BAR", 3, always),
                new ResourceDef("iron.plate", "Iron Plate", "F003-RES-IRON-PLATE", 8, always),
                new ResourceDef("iron.gear", "Gear", "F003-RES-IRON-GEAR", 24, always),
            };
            var recipes = new[]
            {
                new MachineRecipe("recipe.mine-iron-ore", new ResourceAmount[0], new[] { new ResourceAmount("iron.ore", 2) }, 20_000_000),
                new MachineRecipe("recipe.smelt-iron-bar", new[] { new ResourceAmount("iron.ore", 2) }, new[] { new ResourceAmount("iron.bar", 1) }, 20_000_000),
                new MachineRecipe("recipe.press-iron-plate", new[] { new ResourceAmount("iron.bar", 2) }, new[] { new ResourceAmount("iron.plate", 1) }, 40_000_000),
                new MachineRecipe("recipe.assemble-iron-gear", new[] { new ResourceAmount("iron.plate", 2) }, new[] { new ResourceAmount("iron.gear", 1) }, 50_000_000),
            };
            var cost = new Curve(10, 1_150_000, "ceil");
            var machines = new[]
            {
                new MachineDef("machine.miner", "Miner", "F003-MACHINE-MINER", recipes[0].Id, 25, cost, 1_080_000, new SpeedMilestone[0], always),
                new MachineDef("machine.smelter", "Smelter", "F003-MACHINE-SMELTER", recipes[1].Id, 25, cost, 1_080_000, new SpeedMilestone[0], always),
                new MachineDef("machine.press", "Press", "F003-MACHINE-PRESS", recipes[2].Id, 25, cost, 1_080_000, new SpeedMilestone[0], always),
                new MachineDef("machine.assembler", "Assembler", "F003-MACHINE-ASSEMBLER", recipes[3].Id, 25, cost, 1_080_000, new SpeedMilestone[0], always),
            };
            var items = itemOutputFactor == 0 ? new ItemDef[0] : new[]
            {
                new ItemDef("item.drill", 1, "Drill", "F003-ITEM-DRILL", 60_000,
                    "machineType", new[] { "machine.miner" },
                    new EffectDef("machineOutput", "machineType", null, itemOutputFactor)),
            };
            return new RunCatalog(1, resources, recipes, machines,
                new[] { new StorageLevelDef(1, 1000, 0), new StorageLevelDef(2, 2000, 2500),
                    new StorageLevelDef(3, 4000, 10000), new StorageLevelDef(4, 8000, 40000),
                    new StorageLevelDef(5, 16000, 160000) },
                new[] { new FactoryLevelDef(1, 4, 0), new FactoryLevelDef(2, 6, 5000),
                    new FactoryLevelDef(3, 8, 20000), new FactoryLevelDef(4, 10, 80000),
                    new FactoryLevelDef(5, 12, 320000) },
                new ContractOfferDef[0], items, new ResearchDef[0], new IndustryUpgradeDef[0],
                new OverdriveDef(30000, 300000, 2_000_000, always),
                new PrestigeDef(10_000_000, "sqrt", 10, 10_000_000),
                new RunStartDef(25, new[] { "iron.ore", "machine.miner" }, 500_000,
                    1_000_000, 100_000_000, maxCombinedOutputFactor, 2_000_000),
                new RunOfflinePolicy(1, 4, 5, 288_000_000_000L));
        }
    }
}
