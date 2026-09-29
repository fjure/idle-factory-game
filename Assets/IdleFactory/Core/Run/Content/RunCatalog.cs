using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IdleFactory.Core.Numerics;

namespace IdleFactory.Run.Content
{
    /// <summary>Immutable, engine-free content snapshot. IDs use ordinal comparison and are save identities.</summary>
    public sealed class RunCatalog
    {
        public int ContentRevision { get; }
        public IReadOnlyDictionary<string, ResourceDef> Resources { get; }
        public IReadOnlyDictionary<string, MachineRecipe> Recipes { get; }
        public IReadOnlyDictionary<string, MachineDef> Machines { get; }
        public IReadOnlyList<StorageLevelDef> StorageLevels { get; }
        public IReadOnlyList<FactoryLevelDef> FactoryLevels { get; }
        public IReadOnlyDictionary<string, ContractOfferDef> ContractOffers { get; }
        public IReadOnlyDictionary<string, ItemDef> Items { get; }
        public IReadOnlyDictionary<string, ResearchDef> Research { get; }
        public IReadOnlyDictionary<string, IndustryUpgradeDef> IndustryUpgrades { get; }
        public OverdriveDef Overdrive { get; }
        public PrestigeDef Prestige { get; }
        public RunStartDef RunStart { get; }
        public RunOfflinePolicy OfflinePolicy { get; }

        public RunCatalog(int contentRevision, IEnumerable<ResourceDef> resources, IEnumerable<MachineRecipe> recipes,
            IEnumerable<MachineDef> machines, IEnumerable<StorageLevelDef> storageLevels,
            IEnumerable<FactoryLevelDef> factoryLevels, IEnumerable<ContractOfferDef> contractOffers,
            IEnumerable<ItemDef> items, IEnumerable<ResearchDef> research,
            IEnumerable<IndustryUpgradeDef> industryUpgrades, OverdriveDef overdrive,
            PrestigeDef prestige, RunStartDef runStart, RunOfflinePolicy offlinePolicy)
        {
            if (contentRevision < 1) throw new ArgumentOutOfRangeException(nameof(contentRevision));
            ContentRevision = contentRevision;
            Resources = Index(resources, r => r.Id, nameof(resources));
            Recipes = Index(recipes, r => r.Id, nameof(recipes));
            Machines = Index(machines, m => m.Id, nameof(machines));
            StorageLevels = Levels(storageLevels, s => s.Level, s => s.Capacity, true, nameof(storageLevels));
            FactoryLevels = Levels(factoryLevels, f => f.Level, f => f.SlotCount, false, nameof(factoryLevels));
            ContractOffers = Index(contractOffers, c => c.Id, nameof(contractOffers));
            Items = Index(items, i => i.Id, nameof(items));
            Research = Index(research, r => r.Id, nameof(research));
            IndustryUpgrades = Index(industryUpgrades, i => i.Id, nameof(industryUpgrades));
            Overdrive = overdrive ?? throw new ArgumentNullException(nameof(overdrive));
            Prestige = prestige ?? throw new ArgumentNullException(nameof(prestige));
            RunStart = runStart ?? throw new ArgumentNullException(nameof(runStart));
            OfflinePolicy = offlinePolicy ?? throw new ArgumentNullException(nameof(offlinePolicy));
            ValidateOutputFactors();
            ValidateReferences();
        }

        private void ValidateOutputFactors()
        {
            foreach (var item in Items.Values) ValidateOutputFactor(item.Effect);
            foreach (var research in Research.Values) ValidateOutputFactor(research.Effect);
            foreach (var upgrade in IndustryUpgrades.Values) ValidateOutputFactor(upgrade.Effect);
        }

        private void ValidateOutputFactor(EffectDef effect)
        {
            if (effect.Kind == "machineOutput" && effect.Value > RunStart.MaxCombinedOutputFactor)
                throw new ArgumentException("effect.value.out-of-range: maxCombinedOutputFactor");
        }

        private void ValidateReferences()
        {
            foreach (var recipe in Recipes.Values)
            {
                foreach (var amount in recipe.Inputs) Require(Resources, amount.ResourceId, "ref.resource.unknown");
                foreach (var amount in recipe.Outputs) Require(Resources, amount.ResourceId, "ref.resource.unknown");
            }
            foreach (var machine in Machines.Values) Require(Recipes, machine.RecipeId, "ref.recipe.unknown");
            foreach (var offer in ContractOffers.Values)
            {
                Require(Resources, offer.ResourceId, "ref.resource.unknown");
                foreach (var reward in offer.Rewards)
                    if (reward.ItemId != null) Require(Items, reward.ItemId, "contract.reward.item.unknown");
            }
            foreach (var item in Items.Values)
                foreach (var machineId in item.AllowedMachineIds) Require(Machines, machineId, "item.target.invalid");
            foreach (var id in RunStart.InitialUnlocks)
                if (!Resources.ContainsKey(id) && !Machines.ContainsKey(id) && !ContractOffers.ContainsKey(id) &&
                    !Research.ContainsKey(id) && !Items.ContainsKey(id))
                    throw new ArgumentException("runstart.unlock.unknown");
            foreach (var resource in Resources.Values) ValidateUnlock(resource.Unlock);
            foreach (var machine in Machines.Values) ValidateUnlock(machine.Unlock);
            foreach (var offer in ContractOffers.Values) ValidateUnlock(offer.Unlock);
            ValidateUnlock(Overdrive.Unlock);
        }

        private void ValidateUnlock(UnlockCondition unlock)
        {
            if (unlock.Kind == "researchPurchased") Require(Research, unlock.Id, "ref.research.unknown");
            if (unlock.Kind == "resourceProduced") Require(Resources, unlock.Id, "ref.resource.unknown");
            if (unlock.Kind == "factoryLevelAtLeast" && unlock.Level > FactoryLevels.Count)
                throw new ArgumentException("unlock.level.invalid");
        }

        private static void Require<T>(IReadOnlyDictionary<string, T> index, string id, string code)
        {
            if (!index.ContainsKey(id)) throw new ArgumentException(code + ": " + id);
        }

        private static IReadOnlyDictionary<string, T> Index<T>(IEnumerable<T> source, Func<T, string> id, string name) where T : class
        {
            var values = ContentGuard.List(source, name, 0, 1024);
            if (values.Count > 1024) throw new ArgumentException("Too many definitions.", name);
            var index = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                string key = id(value);
                if (index.ContainsKey(key)) throw new ArgumentException("Duplicate ID.", name);
                index.Add(key, value);
            }
            return new ReadOnlyDictionary<string, T>(index);
        }

        private static IReadOnlyList<T> Levels<T>(IEnumerable<T> source, Func<T, int> level,
            Func<T, long> value, bool strictlyIncreasing, string name) where T : class
        {
            var list = ContentGuard.List(source, name, 1, 1024);
            if (list.Count > 1024) throw new ArgumentException("Too many levels.", name);
            long previous = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (level(list[i]) != i + 1) throw new ArgumentException("level.sequence.invalid", name);
                long current = value(list[i]);
                if (i > 0 && (strictlyIncreasing ? current <= previous : current < previous))
                    throw new ArgumentException(strictlyIncreasing ? "storage.capacity.non-increasing" : "factory.slots.non-increasing", name);
                previous = current;
            }
            return new ReadOnlyCollection<T>(list);
        }
    }

    public sealed class RunOfflinePolicy
    {
        public int Revision { get; }
        public long EfficiencyNumerator { get; }
        public long EfficiencyDenominator { get; }
        public long MaxCreditedTicks { get; }
        public RunOfflinePolicy(int revision, long efficiencyNumerator, long efficiencyDenominator, long maxCreditedTicks)
        {
            if (revision != 1 || efficiencyNumerator != 4 || efficiencyDenominator != 5 || maxCreditedTicks != 288_000_000_000L)
                throw new ArgumentException("policy.revision-1.required");
            Revision = revision; EfficiencyNumerator = efficiencyNumerator;
            EfficiencyDenominator = efficiencyDenominator; MaxCreditedTicks = maxCreditedTicks;
        }
    }

    internal static class ContentGuard
    {
        public static string Id(string value, string name)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128 || value != value.Trim() ||
                value != value.Normalize(System.Text.NormalizationForm.FormC))
                throw new ArgumentException("Invalid canonical ID.", name);
            return value;
        }

        public static string Text(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Text is required.", name);
            return value;
        }

        public static long Quantity(long value, string name)
        {
            if (!ExactBound.Contains(value)) throw new ArgumentOutOfRangeException(name);
            return value;
        }

        public static long PositiveQuantity(long value, string name)
        {
            if (value < 1 || value > ExactBound.Value) throw new ArgumentOutOfRangeException(name);
            return value;
        }

        public static List<T> List<T>(IEnumerable<T> source, string name, int minimum = 0, int maximum = 256) where T : class
        {
            if (source == null) throw new ArgumentNullException(name);
            var result = new List<T>();
            foreach (var value in source)
            {
                if (value == null) throw new ArgumentException("Null element.", name);
                result.Add(value);
                if (result.Count > maximum) throw new ArgumentException("Too many elements.", name);
            }
            if (result.Count < minimum) throw new ArgumentException("Too few elements.", name);
            return result;
        }

        public static List<string> Ids(IEnumerable<string> source, string name)
        {
            if (source == null) throw new ArgumentNullException(name);
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in source)
            {
                Id(value, name);
                if (!seen.Add(value)) throw new ArgumentException("Duplicate ID.", name);
                result.Add(value);
                if (result.Count > 256) throw new ArgumentException("Too many IDs.", name);
            }
            return result;
        }
    }
}
