using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Content;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// Single construction point for test catalogs, so a future change to the <see cref="RunCatalog"/> constructor changes
    /// one call site instead of every test. Starts empty (legacy defaults for all schema v3 fields) or from an existing
    /// catalog via <see cref="From"/>, and each <c>With</c> replaces one part.
    /// </summary>
    public sealed class TestCatalogBuilder
    {
        private int _revision = 1;
        private IEnumerable<ResourceDef> _resources = new ResourceDef[0];
        private IEnumerable<MachineRecipe> _recipes = new MachineRecipe[0];
        private IEnumerable<MachineDef> _machines = new MachineDef[0];
        private IEnumerable<StorageLevelDef> _storage = new StorageLevelDef[0];
        private IEnumerable<FactoryLevelDef> _factory = new FactoryLevelDef[0];
        private IEnumerable<ContractOfferDef> _offers = new ContractOfferDef[0];
        private IEnumerable<ItemDef> _items = new ItemDef[0];
        private IEnumerable<ResearchDef> _research = new ResearchDef[0];
        private IEnumerable<IndustryUpgradeDef> _industry = new IndustryUpgradeDef[0];
        private OverdriveDef _overdrive;
        private PrestigeDef _prestige;
        private RunStartDef _runStart;
        private RunOfflinePolicy _offline = new RunOfflinePolicy(1, 4, 5, 288_000_000_000L);
        private ContractItemLadder _ladder;
        private IEnumerable<MilestoneDef> _milestones = new MilestoneDef[0];
        private long _chainDominanceMarginPpm;

        public static TestCatalogBuilder Create() => new TestCatalogBuilder();

        /// <summary>Copies every part of <paramref name="catalog"/>, including the v3 fields.</summary>
        public static TestCatalogBuilder From(RunCatalog catalog)
        {
            return new TestCatalogBuilder
            {
                _revision = catalog.ContentRevision,
                _resources = catalog.Resources.Values.ToArray(),
                _recipes = catalog.Recipes.Values.ToArray(),
                _machines = catalog.Machines.Values.ToArray(),
                _storage = catalog.StorageLevels.ToArray(),
                _factory = catalog.FactoryLevels.ToArray(),
                _offers = catalog.ContractOffers.Values.ToArray(),
                _items = catalog.Items.Values.ToArray(),
                _research = catalog.ResearchInContentOrder.ToArray(),
                _industry = catalog.IndustryUpgrades.Values.ToArray(),
                _overdrive = catalog.Overdrive,
                _prestige = catalog.Prestige,
                _runStart = catalog.RunStart,
                _offline = catalog.OfflinePolicy,
                _ladder = catalog.ContractItemLadder,
                _milestones = catalog.Milestones.ToArray(),
                _chainDominanceMarginPpm = catalog.ChainDominanceMarginPpm,
            };
        }

        public TestCatalogBuilder WithRevision(int revision) { _revision = revision; return this; }
        public TestCatalogBuilder WithResources(IEnumerable<ResourceDef> v) { _resources = v; return this; }
        public TestCatalogBuilder WithRecipes(IEnumerable<MachineRecipe> v) { _recipes = v; return this; }
        public TestCatalogBuilder WithMachines(IEnumerable<MachineDef> v) { _machines = v; return this; }
        public TestCatalogBuilder WithStorageLevels(IEnumerable<StorageLevelDef> v) { _storage = v; return this; }
        public TestCatalogBuilder WithFactoryLevels(IEnumerable<FactoryLevelDef> v) { _factory = v; return this; }
        public TestCatalogBuilder WithContractOffers(IEnumerable<ContractOfferDef> v) { _offers = v; return this; }
        public TestCatalogBuilder WithItems(IEnumerable<ItemDef> v) { _items = v; return this; }
        public TestCatalogBuilder WithResearch(IEnumerable<ResearchDef> v) { _research = v; return this; }
        public TestCatalogBuilder WithIndustryUpgrades(IEnumerable<IndustryUpgradeDef> v) { _industry = v; return this; }
        public TestCatalogBuilder WithOverdrive(OverdriveDef v) { _overdrive = v; return this; }
        public TestCatalogBuilder WithPrestige(PrestigeDef v) { _prestige = v; return this; }
        public TestCatalogBuilder WithRunStart(RunStartDef v) { _runStart = v; return this; }
        public TestCatalogBuilder WithOfflinePolicy(RunOfflinePolicy v) { _offline = v; return this; }
        public TestCatalogBuilder WithContractItemLadder(ContractItemLadder v) { _ladder = v; return this; }
        public TestCatalogBuilder WithMilestones(IEnumerable<MilestoneDef> v) { _milestones = v; return this; }
        public TestCatalogBuilder WithChainDominanceMarginPpm(long v) { _chainDominanceMarginPpm = v; return this; }

        public RunCatalog Build()
        {
            return new RunCatalog(_revision, _resources, _recipes, _machines, _storage, _factory, _offers, _items,
                _research, _industry, _overdrive, _prestige, _runStart, _offline, _ladder, _milestones, _chainDominanceMarginPpm);
        }
    }
}
