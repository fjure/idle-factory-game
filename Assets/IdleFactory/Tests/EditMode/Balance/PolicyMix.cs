using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Content;

namespace IdleFactory.Tests.EditMode.Balance
{
    /// <summary>
    /// The balanced machine mix of policy r2 (balance-policy-r2.md section 2.3). It is derived from the recipes of the catalog, never
    /// hard-coded: the terminal is the machine type with the highest output price in scope, the required rates are walked upstream in exec/s,
    /// and the machine counts are scaled to the slot count with a largest-remainder rule. Everything is <see cref="decimal"/> with ordinal
    /// tie-breaks, so every host decides the same.
    /// </summary>
    internal static class PolicyMix
    {
        private static readonly object Gate = new object();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<RunCatalog, Dictionary<string, Dictionary<string, int>>> Cache =
            new System.Runtime.CompilerServices.ConditionalWeakTable<RunCatalog, Dictionary<string, Dictionary<string, int>>>();

        /// <summary>The scope of a strategy as a stable key; null scope means every machine type of the catalog.</summary>
        public static string ScopeKey(IReadOnlyCollection<string> scope) => scope == null ? "*" : string.Join("|", scope.OrderBy(x => x, StringComparer.Ordinal));

        /// <summary>The machine types of the catalog that are in scope, in ordinal id order.</summary>
        public static List<MachineDef> InScope(RunCatalog catalog, IReadOnlyCollection<string> scope)
        {
            var list = new List<MachineDef>();
            foreach (MachineDef def in catalog.Machines.Values)
                if (scope == null || scope.Contains(def.Id)) list.Add(def);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        /// <summary>The base price of the first output of the machine's recipe.</summary>
        public static long OutputPrice(RunCatalog catalog, MachineDef def) =>
            catalog.Resources[catalog.Recipes[def.RecipeId].Outputs[0].ResourceId].BasePrice;

        /// <summary>The in-scope types from the highest output price to the lowest (ties: ordinal id), the walk order of section 2.3.</summary>
        public static List<MachineDef> WalkOrder(RunCatalog catalog, IReadOnlyCollection<string> scope)
        {
            List<MachineDef> types = InScope(catalog, scope);
            types.Sort((a, b) =>
            {
                long pa = OutputPrice(catalog, a);
                long pb = OutputPrice(catalog, b);
                return pa != pb ? pb.CompareTo(pa) : string.CompareOrdinal(a.Id, b.Id);
            });
            return types;
        }

        /// <summary>exec/s of one level-1 machine: <c>(SpeedTable[1] / 10^6) / (DurationTicks / 10^7)</c>.</summary>
        public static decimal ExecPerSecond(RunCatalog catalog, MachineDef def) =>
            def.SpeedTable[1] / 1_000_000m / (catalog.Recipes[def.RecipeId].DurationTicks / 10_000_000m);

        /// <summary>The target machine count per type for <paramref name="slots"/> slots; a type with a count of 0 is absent.</summary>
        public static Dictionary<string, int> Targets(RunCatalog catalog, IReadOnlyCollection<string> scope, int slots)
        {
            string key = ScopeKey(scope) + "#" + slots;
            lock (Gate)
            {
                Dictionary<string, Dictionary<string, int>> perCatalog = Cache.GetOrCreateValue(catalog);
                if (perCatalog.TryGetValue(key, out Dictionary<string, int> cached)) return cached;
                Dictionary<string, int> computed = Compute(catalog, scope, slots);
                perCatalog[key] = computed;
                return computed;
            }
        }

        private static Dictionary<string, int> Compute(RunCatalog catalog, IReadOnlyCollection<string> scope, int slots)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            List<MachineDef> walk = WalkOrder(catalog, scope);
            if (walk.Count == 0 || slots <= 0) return result;
            MachineDef terminal = walk[0];

            // Required rates (exec/s) from the terminal upstream. Walk order has every consumer before the types it feeds, except a producer
            // that is also priced above its consumer, which content validation (chain.dominance) rules out for the shipped chain.
            var rate = new Dictionary<string, decimal>(StringComparer.Ordinal) { [terminal.Id] = 1m };
            for (int i = 1; i < walk.Count; i++)
            {
                MachineDef m = walk[i];
                MachineRecipe recipe = catalog.Recipes[m.RecipeId];
                decimal total = 0m;
                foreach (ResourceAmount output in recipe.Outputs)
                {
                    decimal demand = 0m;
                    foreach (MachineDef d in walk)
                    {
                        if (d.Id == m.Id || !rate.TryGetValue(d.Id, out decimal rd)) continue;
                        foreach (ResourceAmount input in catalog.Recipes[d.RecipeId].Inputs)
                            if (input.ResourceId == output.ResourceId) demand += rd * input.Amount;
                    }
                    if (demand > 0m) total = Math.Max(total, demand / output.Amount);
                }
                if (total > 0m) rate[m.Id] = total;
            }

            var count = new List<KeyValuePair<MachineDef, decimal>>();
            decimal sum = 0m;
            foreach (MachineDef m in walk)
            {
                if (!rate.TryGetValue(m.Id, out decimal r)) continue;
                decimal c = r / ExecPerSecond(catalog, m);
                count.Add(new KeyValuePair<MachineDef, decimal>(m, c));
                sum += c;
            }
            if (count.Count == 0) return result;

            // When the slots cannot hold one of every type, the types just below the terminal are dropped first (the terminal last).
            while (count.Count > slots) count.RemoveAt(count.Count > 1 ? 1 : 0);
            sum = 0m;
            foreach (KeyValuePair<MachineDef, decimal> pair in count) sum += pair.Value;

            var scaled = new decimal[count.Count];
            var alloc = new int[count.Count];
            int used = 0;
            for (int i = 0; i < count.Count; i++)
            {
                scaled[i] = count[i].Value * slots / sum;
                alloc[i] = Math.Max(1, (int)Math.Floor(scaled[i]));
                used += alloc[i];
            }

            // Largest fractional part first; ties: the type earlier in the upstream walk (higher in the list), then ordinal id.
            while (used < slots)
            {
                int best = -1;
                decimal bestFrac = -1m;
                for (int i = 0; i < count.Count; i++)
                {
                    decimal frac = scaled[i] - Math.Floor(scaled[i]);
                    if (best < 0 || frac > bestFrac) { best = i; bestFrac = frac; }
                }
                alloc[best]++;
                scaled[best] = Math.Floor(scaled[best]) + 0m; // its fraction is spent
                used++;
            }
            while (used > slots)
            {
                int worst = -1;
                decimal worstOver = decimal.MinValue;
                for (int i = 0; i < count.Count; i++)
                {
                    if (alloc[i] <= 1) continue;
                    decimal over = alloc[i] - scaled[i];
                    if (over >= worstOver) { worst = i; worstOver = over; }
                }
                if (worst < 0) break;
                alloc[worst]--;
                used--;
            }

            for (int i = 0; i < count.Count; i++) result[count[i].Key.Id] = alloc[i];
            return result;
        }
    }
}
