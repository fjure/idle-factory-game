using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Tests.EditMode.Balance
{
    /// <summary>What the Industry model needs to know about a strategy (balance-policy-r2.md section 5.1).</summary>
    internal sealed class IndustryModelOptions
    {
        /// <summary>The machine types the strategy builds; null is every type. Empty means it builds nothing (mining-only).</summary>
        public IReadOnlyCollection<string> Scope;

        /// <summary>Activations per second of the held click before the first Miner exists in the model.</summary>
        public long MineActivationsBeforeMiner;

        /// <summary>Activations per second of the held click after the first Miner exists.</summary>
        public long MineActivationsAfterMiner;

        public int IdleStartSeconds = ReferencePolicy.IdleStartSeconds;
        public int IdleCycleSeconds = ReferencePolicy.IdleCycleSeconds;

        public string Key => PolicyMix.ScopeKey(Scope) + "/" + MineActivationsBeforeMiner + "/" + MineActivationsAfterMiner + "/" + IdleStartSeconds + "/" + IdleCycleSeconds;
    }

    /// <summary>
    /// The fluid-model time to the next threshold, <c>T_fluid(levels)</c> (balance-policy-r2.md section 5.1). It plays a closed-form deterministic
    /// player on the fluid model of <see cref="FluidModel"/> (continuous flows at the composed table speeds, upstream first, everything left over
    /// sold at the effective price) from the start of a run to the threshold: the balanced mix, builds at the effective prices of the upcoming
    /// run, machine upgrades by payback, no Items, contracts, Research or Overdrive, no storage limit, and the held mining click where the
    /// strategy mines. Industry effects enter through the catalog's modifier pipeline (<see cref="RunModifiers"/>, <see cref="NextRunStart"/>,
    /// <see cref="BuildPricing"/>), exactly as the engine folds them. The result is a pure function of (Industry levels, prestige count, catalog,
    /// strategy options), memoised on that key, so it does not depend on the order in which histories complete (P9).
    /// </summary>
    internal static class IndustryModel
    {
        public const decimal StepSeconds = 20m;
        public const decimal LimitSeconds = ReferencePolicy.RunLimitSeconds;

        private static readonly object Gate = new object();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<RunCatalog, Dictionary<string, decimal>> Cache =
            new System.Runtime.CompilerServices.ConditionalWeakTable<RunCatalog, Dictionary<string, decimal>>();

        /// <summary>Evaluations that were computed rather than served from the memo (a measurement aid, PF-15).</summary>
        public static long Evaluations;

        public static long MemoHits;

        public static string LevelsKey(RunCatalog catalog, IReadOnlyDictionary<string, long> levels, long prestigeCount, IndustryModelOptions options)
        {
            var sb = new StringBuilder();
            var ids = new List<string>(catalog.IndustryUpgrades.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                levels.TryGetValue(id, out long level);
                sb.Append(level.ToString(CultureInfo.InvariantCulture)).Append(',');
            }
            return sb.Append('#').Append(prestigeCount.ToString(CultureInfo.InvariantCulture)).Append('#').Append(options.Key).ToString();
        }

        public static decimal TimeToThreshold(RunCatalog catalog, IReadOnlyDictionary<string, long> levels, long prestigeCount, IndustryModelOptions options)
        {
            string key = LevelsKey(catalog, levels, prestigeCount, options);
            lock (Gate)
            {
                Dictionary<string, decimal> perCatalog = Cache.GetOrCreateValue(catalog);
                if (perCatalog.TryGetValue(key, out decimal cached))
                {
                    MemoHits++;
                    return cached;
                }
            }

            decimal value = new Plan(catalog, levels, prestigeCount, options).Run();
            lock (Gate)
            {
                Evaluations++;
                Cache.GetOrCreateValue(catalog)[key] = value;
            }
            return value;
        }

        private sealed class Model
        {
            public string Type;
            public int Level;
            public decimal Invested;
        }

        private sealed class Plan
        {
            private readonly RunCatalog _catalog;
            private readonly PermanentState _perm;
            private readonly RunState _state;
            private readonly RunModifiers _mods;
            private readonly IndustryModelOptions _options;
            private readonly long _threshold;
            private readonly List<MachineDef> _scope;
            private readonly Dictionary<string, MachineInstance> _instances = new Dictionary<string, MachineInstance>(StringComparer.Ordinal);
            private readonly Dictionary<string, decimal> _speedCache = new Dictionary<string, decimal>(StringComparer.Ordinal);
            private readonly Dictionary<string, decimal> _salePrice = new Dictionary<string, decimal>(StringComparer.Ordinal);
            private readonly List<Model> _machines = new List<Model>();
            private decimal _cash;
            private int _factoryLevel = 1;
            private decimal _factoryInvested;
            private decimal _mineBefore;
            private decimal _mineAfter;
            private decimal _activeShare;
            private readonly string _oreId;

            public Plan(RunCatalog catalog, IReadOnlyDictionary<string, long> levels, long prestigeCount, IndustryModelOptions options)
            {
                _catalog = catalog;
                _options = options;
                _perm = new PermanentState(catalog, 0, prestigeCount);
                foreach (KeyValuePair<string, long> pair in levels)
                    if (pair.Value > 0) _perm.TrySetIndustryLevel(pair.Key, pair.Value);
                NextRunStart start = NextRunStart.Compute(catalog, _perm);
                _state = new RunState(catalog, TraceContext.Id(990_001), 1, null, start.StartingCash, 0, start.StorageLevel, 1, catalog.RunStart.InitialUnlocks);
                _mods = new RunModifiers(catalog);
                _cash = start.StartingCash;
                if (!PrestigeThreshold.TryForCount(catalog.Prestige, prestigeCount, out _threshold)) _threshold = long.MaxValue;
                _scope = options.Scope != null && options.Scope.Count == 0 ? new List<MachineDef>() : PolicyMix.InScope(catalog, options.Scope);
                _activeShare = 1m - (decimal)(options.IdleCycleSeconds - options.IdleStartSeconds) / options.IdleCycleSeconds;

                // Mined Ore per second (FR-043): the effective milli-yield per activation times the activations per second, sold at the Ore price.
                ManualMiningDef mining = catalog.RunStart.ManualMining;
                _oreId = mining?.ResourceId;
                if (mining != null && _mods.TryManualYieldPpm(_state, _perm, out long ppm) &&
                    RunModifiers.TryEffectiveMilliYield(mining.BaseYieldMilli, ppm, out long milli))
                {
                    decimal orePrice = catalog.Resources[mining.ResourceId].BasePrice * SalePrice(mining.ResourceId);
                    _mineBefore = milli * options.MineActivationsBeforeMiner / 1000m * orePrice * _activeShare;
                    _mineAfter = milli * options.MineActivationsAfterMiner / 1000m * orePrice * _activeShare;
                }
            }

            private decimal SalePrice(string resourceId)
            {
                if (_salePrice.TryGetValue(resourceId, out decimal cached)) return cached;
                _mods.TrySalePricePpm(_state, _perm, resourceId, out long ppm);
                decimal value = ppm / 1_000_000m;
                _salePrice[resourceId] = value;
                return value;
            }

            private decimal Speed(string type, int level)
            {
                string key = type + "@" + level;
                if (_speedCache.TryGetValue(key, out decimal cached)) return cached;
                if (!_instances.TryGetValue(key, out MachineInstance instance))
                {
                    MachineDef def = _catalog.Machines[type];
                    instance = new MachineInstance(TraceContext.Id(990_100 + _instances.Count), def, 0, level, true, 0, 1, 0);
                    _instances[key] = instance;
                }
                _mods.TryMachineSpeed(_state, _perm, instance, false, null, out long ppm);
                decimal value = ppm;
                _speedCache[key] = value;
                return value;
            }

            private List<FluidModel.Machine> Fluid()
            {
                var list = new List<FluidModel.Machine>(_machines.Count);
                foreach (Model m in _machines) list.Add(new FluidModel.Machine { TypeId = m.Type, SpeedPpm = (long)Speed(m.Type, m.Level) });
                return list;
            }

            private decimal Income(List<FluidModel.Machine> machines) => FluidModel.Income(_catalog, machines, SalePrice);

            private decimal FactoryValue()
            {
                decimal total = _cash + _factoryInvested;
                foreach (Model m in _machines) total += m.Invested;
                return total;
            }

            private bool HasMiner()
            {
                if (_oreId == null) return false;
                foreach (Model m in _machines)
                    foreach (ResourceAmount output in _catalog.Recipes[_catalog.Machines[m.Type].RecipeId].Outputs)
                        if (output.ResourceId == _oreId) return true;
                return false;
            }

            private bool ModelUnlocked(MachineDef def)
            {
                UnlockCondition u = def.Unlock;
                switch (u.Kind)
                {
                    case "always": return true;
                    case "factoryLevelAtLeast": return _factoryLevel >= u.Level;
                    case "resourceProduced":
                        foreach (Model m in _machines)
                            foreach (ResourceAmount output in _catalog.Recipes[_catalog.Machines[m.Type].RecipeId].Outputs)
                                if (output.ResourceId == u.Id) return true;
                        return false;
                    default: return false;
                }
            }

            public decimal Run()
            {
                decimal t = 0m;
                bool builds = _scope.Count > 0;
                while (t < LimitSeconds)
                {
                    if (builds) Invest();
                    List<FluidModel.Machine> fluid = Fluid();
                    decimal income = Income(fluid) + (HasMiner() ? _mineAfter : _mineBefore);
                    decimal before = FactoryValue();
                    _cash += income * StepSeconds;
                    decimal after = FactoryValue();
                    if (after >= _threshold)
                    {
                        decimal fraction = after == before ? 1m : (_threshold - before) / (after - before);
                        if (fraction < 0m) fraction = 0m;
                        return t + StepSeconds * fraction;
                    }
                    t += StepSeconds;
                }
                return LimitSeconds;
            }

            private long BuildPrice(MachineDef def, int owned)
            {
                BuildPricing.TryPriceForOwned(_catalog, _mods, _state, _perm, def, owned, out long price);
                return price;
            }

            private void Invest()
            {
                for (int guard = 0; guard < 4000; guard++)
                {
                    List<FluidModel.Machine> fluid = Fluid();
                    decimal income = Math.Max(Income(fluid) + (HasMiner() ? _mineAfter : _mineBefore), 1m);
                    int slots = _catalog.FactoryLevels[_factoryLevel - 1].SlotCount;

                    // 1. The next machine of the balanced mix (a locked type holds its deficit: nothing is built in its place).
                    MachineDef next = _machines.Count < slots ? NextMixType(slots) : null;
                    if (next != null)
                    {
                        long price = BuildPrice(next, CountOf(next.Id));
                        if (_cash >= price)
                        {
                            _cash -= price;
                            _machines.Add(new Model { Type = next.Id, Level = 1, Invested = price });
                            continue;
                        }
                        if ((price - _cash) / income <= ReferencePolicy.WaitForMachineSeconds) return;
                    }

                    // 2. Factory level when nothing can be built.
                    if (next == null && _factoryLevel < _catalog.FactoryLevels.Count)
                    {
                        _mods.TryUpgradeCost(_state, _perm, _catalog.FactoryLevels[_factoryLevel].Cost, null, out long cost);
                        if (_cash >= cost)
                        {
                            _cash -= cost;
                            _factoryInvested += cost;
                            _factoryLevel++;
                            continue;
                        }
                        if ((cost - _cash) / income <= ReferencePolicy.WaitForFactorySeconds) return;
                    }

                    // 3. Machine upgrades by payback.
                    decimal baseIncome = Income(fluid);
                    int bestIndex = -1;
                    decimal bestPayback = decimal.MaxValue;
                    decimal[] paybacks = new decimal[_machines.Count];
                    long[] costs = new long[_machines.Count];
                    for (int i = 0; i < _machines.Count; i++)
                    {
                        Model m = _machines[i];
                        MachineDef def = _catalog.Machines[m.Type];
                        paybacks[i] = -1m;
                        if (m.Level >= def.MaxLevel) continue;
                        _mods.TryUpgradeCost(_state, _perm, def.CostTable[m.Level], def.Id, out long cost);
                        var changed = new List<FluidModel.Machine>(fluid);
                        changed[i] = new FluidModel.Machine { TypeId = m.Type, SpeedPpm = (long)Speed(m.Type, m.Level + 1) };
                        decimal gain = Income(changed) - baseIncome;
                        if (gain <= 0m) continue;
                        costs[i] = cost;
                        paybacks[i] = cost / gain;
                        if (paybacks[i] < bestPayback) { bestPayback = paybacks[i]; bestIndex = i; }
                    }
                    if (bestIndex < 0) return;
                    int pick = bestIndex;
                    if (_cash < costs[bestIndex])
                    {
                        if ((costs[bestIndex] - _cash) / income <= ReferencePolicy.WaitForMachineSeconds) return;
                        pick = -1;
                        for (int i = 0; i < _machines.Count; i++)
                        {
                            if (i == bestIndex || paybacks[i] < 0m || costs[i] > _cash || paybacks[i] > bestPayback * 2m) continue;
                            if (pick < 0 || costs[i] < costs[pick] || (costs[i] == costs[pick] && paybacks[i] < paybacks[pick])) pick = i;
                        }
                        if (pick < 0) return;
                    }
                    _cash -= costs[pick];
                    _machines[pick].Level++;
                    _machines[pick].Invested += costs[pick];
                }
            }

            private int CountOf(string type)
            {
                int n = 0;
                foreach (Model m in _machines) if (m.Type == type) n++;
                return n;
            }

            private MachineDef NextMixType(int slots)
            {
                if (_oreId != null && !HasMiner())
                {
                    foreach (MachineDef def in _scope)
                        foreach (ResourceAmount output in _catalog.Recipes[def.RecipeId].Outputs)
                            if (output.ResourceId == _oreId) return def;
                }
                Dictionary<string, int> targets = PolicyMix.Targets(_catalog, _options.Scope, slots);
                MachineDef best = null;
                decimal bestDeficit = decimal.MinValue;
                long bestPrice = 0;
                foreach (MachineDef def in _scope)
                {
                    if (!targets.TryGetValue(def.Id, out int target)) continue;
                    int owned = CountOf(def.Id);
                    if (owned >= target || !ModelUnlocked(def)) continue;
                    decimal deficit = (decimal)target * (_machines.Count + 1) / slots - owned;
                    long price = PolicyMix.OutputPrice(_catalog, def);
                    if (best == null || deficit > bestDeficit || (deficit == bestDeficit && (price < bestPrice ||
                        (price == bestPrice && string.CompareOrdinal(def.Id, best.Id) < 0))))
                    {
                        best = def;
                        bestDeficit = deficit;
                        bestPrice = price;
                    }
                }
                return best;
            }
        }
    }
}
