using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Tests.EditMode.Balance
{
    /// <summary>
    /// The pass/fail bands and sensitivity bounds of <c>specs/003-first-factory-prestige/reviews/balance-policy.md</c>
    /// (ECO-2, ECO-4). They are plausibility bands for a hypothesis, not asserted promises: the trace reports pass or fail
    /// per band, and T125 turns them into asserting tests for its proposed revision.
    /// </summary>
    internal static class BalanceBands
    {
        public const long FirstGearMinSec = 600;
        public const long FirstGearMaxSec = 900;
        public const long EligibleMinSec = 6_600;
        public const long EligibleMaxSec = 7_800;
        public const long SecondRunMinSec = 4_200;
        public const long SecondRunMaxSec = 4_800;

        /// <summary>ECO-3: a later run may not be faster than this share of run 1 (parts per 1000).</summary>
        public const long RunFloorPermille = 250;

        /// <summary>The Factory Value milestone that the sensitivity variants are compared at (the $10M threshold is not reached in every variant).</summary>
        public const long SensitivityMilestoneFv = 250_000;

        /// <summary>The Factory Value threshold of the scaled scenario that exercises the chain, the second run and the projection.</summary>
        public const long ScaledThresholdFv = 100_000;

        /// <summary>Price variants: play time to eligibility within +/- this share of the baseline (parts per 1000).</summary>
        public const long PriceBoundPermille = 250;

        /// <summary>Contracts disabled or throttled: play time may grow by at most this share (parts per 1000), and may not shrink by more than 50 of 1000.</summary>
        public const long ContractGrowthBoundPermille = 250;

        /// <summary>Overdrive disabled: play time may grow by at most this share (parts per 1000).</summary>
        public const long OverdriveGrowthBoundPermille = 150;

        /// <summary>
        /// Offline sensitivity (FR-025): an eight-hour absence may remove at most this share of the baseline play time
        /// (parts per 1000). Play time is the wall time to eligibility minus the absence.
        /// </summary>
        public const long AbsenceMaxSavingPermille = 500;

        /// <summary>Repeated short returns: wall time to eligibility between these shares of the baseline (parts per 1000).</summary>
        public const long ReturnsMinPermille = 600;
        public const long ReturnsMaxPermille = 1_600;
    }

    /// <summary>The deterministic, human-like reference policy (ECO-2). Its rules are written in <c>balance-policy.md</c>.</summary>
    internal sealed class ReferencePolicy
    {
        /// <summary>The player looks at the factory every 20 simulated seconds: this is the action latency.</summary>
        public const int EpochSeconds = 20;

        /// <summary>Idle window: the player is away from the screen for the last 60 s of every 300 s (20 % of the time).</summary>
        public const int IdleCycleSeconds = 300;
        public const int IdleStartSeconds = 240;

        /// <summary>At most this many commands per look: the player cannot do everything at once.</summary>
        public const int ActionsPerLook = 10;

        /// <summary>The player waits for the best investment only when it is affordable within this many seconds.</summary>
        public const int WaitForBestSeconds = 90;

        /// <summary>A contract accepted without a deadline is abandoned after this many seconds.</summary>
        public const int AbandonUntimedAfterSeconds = 900;

        /// <summary>Storage is expanded when the stock fills this share of the capacity (percent).</summary>
        public const int StorageFullPercent = 60;

        /// <summary>A kept resource is dumped (sold at once) when its stock fills this share of the capacity (percent).</summary>
        public const int DumpPercent = 25;

        /// <summary>A contract is accepted only when its quantity fits this share of the capacity (percent).</summary>
        public const int ContractCapacityPercent = 33;

        /// <summary>Research purchase order, one entry per level (strict queue: the policy waits for the next entry).</summary>
        public static readonly string[] ResearchOrder =
        {
            "research.miner-tuning", "research.miner-tuning",
            "research.bargaining", "research.bargaining",
            "research.smelter-tuning", "research.smelter-tuning",
            "research.tool-discount", "research.tool-discount",
            "research.miner-tuning",
            "research.press-tuning", "research.press-tuning",
            "research.market-analysis", "research.market-analysis",
            "research.warehouse-planning", "research.warehouse-planning",
            "research.assembler-tuning", "research.assembler-tuning",
            "research.deep-veins",
        };

        /// <summary>Industry purchase priority; the policy spends all IP in this order each prestige.</summary>
        public static readonly string[] IndustryOrder =
        {
            "industry.calibration", "industry.seed-capital", "industry.market-contacts", "industry.toolmaker", "industry.warehouse",
        };

        public bool ContractsEnabled { get; set; } = true;
        public bool OverdriveEnabled { get; set; } = true;

        /// <summary>Probe only: build Miners and nothing else (the dominant-strategy check of the content record). Not the reference policy.</summary>
        public bool OreOnly { get; set; }

        /// <summary>Seconds to wait after a contract ended before accepting the next one (contract cadence).</summary>
        public int ContractCooldownSeconds { get; set; }

        public static bool IsIdle(long runSeconds) => runSeconds % IdleCycleSeconds >= IdleStartSeconds;

        // -------------------------------------------------------------------------------------------------
        // One look
        // -------------------------------------------------------------------------------------------------

        public int Act(TraceContext c)
        {
            int budget = ActionsPerLook;
            RunSession session = c.Session;
            RunState st = session.State;
            long now = session.TimelineUtcMs;
            c.ObserveContractEnd(now, ContractCooldownSeconds);

            DeliverContract(c, ref budget);
            AbandonStale(c, now, ref budget);
            AcceptContract(c, now, ref budget);
            ReconcileHoard(c, ref budget);
            ActivateItems(c, ref budget);
            ActivateOverdrive(c, now, ref budget);
            PurchaseResearch(c, ref budget);
            Invest(c, ref budget);
            return ActionsPerLook - budget;
        }

        /// <summary>Spends all IP in priority order after a prestige (the first purchase is the "approved first IP purchase").</summary>
        public int SpendIndustry(TraceContext c)
        {
            int purchases = 0;
            bool bought = true;
            while (bought)
            {
                bought = false;
                foreach (string id in IndustryOrder)
                {
                    if (!c.Catalog.IndustryUpgrades.TryGetValue(id, out IndustryUpgradeDef def)) continue;
                    long level = c.Session.Permanent.IndustryLevel(id);
                    if (level >= def.MaxLevel || c.Session.Permanent.Ip < def.IpCostTable[(int)level]) continue;
                    if (!c.PurchaseIndustry(id)) continue;
                    purchases++;
                    bought = true;
                    break;
                }
            }
            return purchases;
        }

        // -------------------------------------------------------------------------------------------------
        // Contracts
        // -------------------------------------------------------------------------------------------------

        private static void DeliverContract(TraceContext c, ref int budget)
        {
            RunState st = c.Session.State;
            ActiveContract active = st.Contracts.Active;
            if (active == null || budget <= 0) return;
            string resource = c.Catalog.ContractOffers[active.OfferId].ResourceId;
            if (st.Inventory.Units(resource) < active.RequiredUnits) return;
            budget--;
            c.DeliverContract();
        }

        private void AbandonStale(TraceContext c, long now, ref int budget)
        {
            RunState st = c.Session.State;
            ActiveContract active = st.Contracts.Active;
            if (active == null || active.DeadlineUtcMs.HasValue || budget <= 0) return;
            if (now - active.AcceptedUtcMs < AbandonUntimedAfterSeconds * 1000L) return;
            budget--;
            c.AbandonContract();
        }

        private void AcceptContract(TraceContext c, long now, ref int budget)
        {
            RunState st = c.Session.State;
            if (!ContractsEnabled || st.Contracts.Active != null || budget <= 0 || now < c.ContractCooldownUntilUtcMs) return;
            long capacity = st.Inventory.Capacity;
            ContractOfferDef best = null;
            long bestCount = 0;
            long bestMarket = 0;
            foreach (ContractOfferDef offer in c.Catalog.ContractOffers.Values)
            {
                if (!st.IsUnlocked(offer.Id)) continue;
                long count = st.Contracts.CompletionCount(offer.Id);
                if (count >= offer.QuantityTable.Count) continue;
                long required = offer.QuantityTable[(int)count];
                if (required * 100 > capacity * ContractCapacityPercent) continue;

                // The producer must exist and must be able to fill the contract in time.
                decimal rate = FluidModel.GrossRate(c.Catalog, c.MachineList(), offer.ResourceId);
                if (rate <= 0) continue;
                decimal stock = st.Inventory.Units(offer.ResourceId);
                decimal secondsNeeded = ((decimal)required - stock) / rate;
                if (offer.DurationMs.HasValue && secondsNeeded * 1000m > offer.DurationMs.Value) continue;
                if (!offer.DurationMs.HasValue && secondsNeeded > AbandonUntimedAfterSeconds) continue;

                long market = required * c.Catalog.Resources[offer.ResourceId].BasePrice;
                if (best == null || count < bestCount || (count == bestCount && (market < bestMarket ||
                    (market == bestMarket && string.CompareOrdinal(offer.Id, best.Id) < 0))))
                {
                    best = offer;
                    bestCount = count;
                    bestMarket = market;
                }
            }
            if (best == null) return;
            budget--;
            c.AcceptContract(best.Id);
        }

        /// <summary>
        /// Auto-sell follows the contract: the contract's resource is kept while its contract is active, and the machines that
        /// would consume it are switched off for that time. Everything else sells automatically.
        /// </summary>
        private static void ReconcileHoard(TraceContext c, ref int budget)
        {
            RunState st = c.Session.State;
            ActiveContract active = st.Contracts.Active;
            string hoard = active == null ? null : c.Catalog.ContractOffers[active.OfferId].ResourceId;
            var types = new List<string>();
            foreach (MachineInstance m in st.Machines) types.Add(m.MachineId);
            HashSet<string> consumed = FluidModel.Kept(c.Catalog, types);
            foreach (ResourceDef resource in SortedResources(c.Catalog))
            {
                if (!st.IsUnlocked(resource.Id)) continue;
                // A resource that a built machine can only use after it piled up is kept for it; everything else is sold.
                bool wanted = resource.Id != hoard && !consumed.Contains(resource.Id);
                if (st.IsAutoSell(resource.Id) == wanted || budget <= 0) continue;
                budget--;
                c.SetAutoSell(resource.Id, wanted);
            }

            // Surplus of a kept resource is dumped: auto-sell is switched on and off again, which sells the whole stock at once.
            foreach (ResourceDef resource in SortedResources(c.Catalog))
            {
                if (!consumed.Contains(resource.Id) || resource.Id == hoard || !st.IsUnlocked(resource.Id) || budget < 1) continue;
                if (st.Inventory.Units(resource.Id) * 100 < st.Inventory.Capacity * DumpPercent) continue;
                budget--;
                if (c.SetAutoSell(resource.Id, true)) c.Record.Add("surplus_dumps", 1);
                c.SetAutoSell(resource.Id, false);
            }
            foreach (MachineInstance m in new List<MachineInstance>(st.Machines))
            {
                MachineDef def = c.Catalog.Machines[m.MachineId];
                bool consumes = false;
                if (hoard != null)
                    foreach (ResourceAmount input in c.Catalog.Recipes[def.RecipeId].Inputs)
                        consumes |= input.ResourceId == hoard;
                if (m.IsActive == !consumes || budget <= 0) continue;
                budget--;
                c.SetActive(m.InstanceId, !consumes);
            }
        }

        private static List<ResourceDef> SortedResources(RunCatalog catalog)
        {
            var list = new List<ResourceDef>(catalog.Resources.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        // -------------------------------------------------------------------------------------------------
        // Items, Overdrive, Research
        // -------------------------------------------------------------------------------------------------

        private static void ActivateItems(TraceContext c, ref int budget)
        {
            RunState st = c.Session.State;
            if (st.Machines.Count == 0) return;
            var items = new List<ItemDef>(c.Catalog.Items.Values);
            items.Sort((a, b) => a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : string.CompareOrdinal(a.Id, b.Id));
            foreach (ItemDef item in items)
            {
                if (budget <= 0 || c.Session.Permanent.ItemCount(item.Id) < 1) continue;
                string target = null;
                if (item.TargetKind != "factory")
                {
                    foreach (string allowed in item.AllowedMachineIds)
                    {
                        foreach (MachineInstance m in st.Machines)
                            if (m.MachineId == allowed && m.IsActive) target = allowed;
                        if (target != null) break;
                    }
                    if (target == null) continue;
                }
                if (c.ItemConflicts(item, target)) continue;
                budget--;
                c.ActivateItem(item.Id, target);
            }
        }

        private void ActivateOverdrive(TraceContext c, long now, ref int budget)
        {
            RunState st = c.Session.State;
            if (!OverdriveEnabled || budget <= 0 || !st.IsUnlocked(RunState.OverdriveUnlockId) || st.Machines.Count < 3) return;
            if (st.Overdrive.CooldownUntilUtcMs.HasValue && now < st.Overdrive.CooldownUntilUtcMs.Value) return;
            budget--;
            c.ActivateOverdrive();
        }

        private static void PurchaseResearch(TraceContext c, ref int budget)
        {
            RunState st = c.Session.State;
            while (budget > 0)
            {
                string next = null;
                var seen = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (string id in ResearchOrder)
                {
                    seen.TryGetValue(id, out int target);
                    seen[id] = ++target;
                    if (st.ResearchLevel(id) < target) { next = id; break; }
                }
                if (next == null) return;
                ResearchDef def = c.Catalog.Research[next];
                int level = (int)st.ResearchLevel(next);
                if (level >= def.MaxLevel || st.Rp < def.RpCostTable[level]) return;
                budget--;
                if (!c.PurchaseResearch(next)) return;
            }
        }

        // -------------------------------------------------------------------------------------------------
        // Investment: a build order for the chain, then upgrades by payback on a fluid model
        // -------------------------------------------------------------------------------------------------

        /// <summary>
        /// The order in which the player fills the slots (3 Miners, 4 Smelters, 3 Presses, 2 Assemblers at 12 slots, the layout
        /// of the W003 workload); a type that is still locked is skipped until it unlocks.
        /// </summary>
        public static readonly string[] BuildOrder =
        {
            "machine.miner", "machine.miner", "machine.smelter", "machine.miner", "machine.smelter", "machine.press",
            "machine.smelter", "machine.assembler", "machine.press", "machine.smelter", "machine.press", "machine.assembler",
        };

        /// <summary>The player saves for the next machine when it is affordable within this many seconds.</summary>
        public const int WaitForMachineSeconds = 90;

        /// <summary>The player saves for the next storage level when it is affordable within this many seconds.</summary>
        public const int WaitForStorageSeconds = 120;

        /// <summary>The player saves for the next factory level when it is affordable within this many seconds.</summary>
        public const int WaitForFactorySeconds = 180;

        private sealed class Candidate
        {
            public Guid InstanceId;
            public long Cost;
            public decimal Payback;
        }

        private static void Invest(TraceContext c, ref int budget)
        {
            while (budget > 0)
            {
                RunSession session = c.Session;
                RunState st = session.State;
                List<FluidModel.Machine> machines = c.MachineList();
                decimal rawIncome = FluidModel.Income(c.Catalog, machines);
                // The waiting horizon uses the income the player actually sees (Cash gained per second), not the model's estimate.
                decimal income = Math.Max(c.ObservedIncome, 1m);

                // Storage: a store that fills up (or a "storage full" warning) is expanded; the player saves for it when it is near.
                if (st.StorageLevel < c.Catalog.StorageLevels.Count && (st.Inventory.Total * 100 >= st.Inventory.Capacity * StorageFullPercent || c.AnyMachineBlockedByStorage()) &&
                    c.Modifiers.TryUpgradeCost(st, session.Permanent, c.Catalog.StorageLevels[st.StorageLevel].Cost, null, out long storageCost))
                {
                    if (st.Cash >= storageCost)
                    {
                        budget--;
                        if (c.UpgradeStorage()) continue;
                        return;
                    }
                    if ((storageCost - st.Cash) / income <= WaitForStorageSeconds) return;
                }

                // 1. The next machine of the build order, while a slot is free.
                int free = FirstFreeSlot(st);
                string next = free >= 0 ? NextInOrder(c, st) : null;
                if (next != null)
                {
                    MachineDef def = c.Catalog.Machines[next];
                    if (c.Modifiers.TryUpgradeCost(st, session.Permanent, def.CostTable[0], def.Id, out long buildCost))
                    {
                        if (st.Cash >= buildCost)
                        {
                            budget--;
                            if (c.Build(free, next)) continue;
                            return;
                        }
                        if ((buildCost - st.Cash) / income <= WaitForMachineSeconds) return;
                    }
                }

                // 2. A full factory is expanded as soon as the player can pay for it.
                if (free < 0 && st.FactoryLevel < c.Catalog.FactoryLevels.Count &&
                    c.Modifiers.TryUpgradeCost(st, session.Permanent, c.Catalog.FactoryLevels[st.FactoryLevel].Cost, null, out long factoryCost))
                {
                    if (st.Cash >= factoryCost)
                    {
                        budget--;
                        if (c.UpgradeFactory()) continue;
                        return;
                    }
                    if ((factoryCost - st.Cash) / income <= WaitForFactorySeconds) return;
                }

                // 3. Machine upgrades by payback.
                List<Candidate> candidates = UpgradeCandidates(c, machines, rawIncome);
                Candidate best = null;
                foreach (Candidate cand in candidates)
                    if (best == null || cand.Payback < best.Payback) best = cand;
                if (best == null) return;

                Candidate pick = best;
                if (st.Cash < best.Cost)
                {
                    if ((best.Cost - st.Cash) / income <= WaitForMachineSeconds) return;
                    pick = null;
                    foreach (Candidate cand in candidates)
                    {
                        if (cand == best || cand.Cost > st.Cash || cand.Payback > best.Payback * 2m) continue;
                        if (pick == null || cand.Payback < pick.Payback) pick = cand;
                    }
                    if (pick == null) return;
                }
                budget--;
                if (!c.UpgradeMachine(pick.InstanceId)) return;
            }
        }

        private static string NextInOrder(TraceContext c, RunState st)
        {
            if (c.Policy.OreOnly) return "machine.miner";
            for (int i = st.Machines.Count; i < BuildOrder.Length; i++)
                if (st.IsUnlocked(BuildOrder[i])) return BuildOrder[i];
            return null;
        }

        private static int FirstFreeSlot(RunState st)
        {
            for (int i = 0; i < st.SlotCount; i++)
                if (st.MachineAtSlot(i) == null) return i;
            return -1;
        }

        private static List<Candidate> UpgradeCandidates(TraceContext c, List<FluidModel.Machine> machines, decimal income)
        {
            RunSession session = c.Session;
            RunState st = session.State;
            var list = new List<Candidate>();
            foreach (MachineInstance m in st.Machines)
            {
                MachineDef def = c.Catalog.Machines[m.MachineId];
                if (m.Level >= m.MaxLevel) continue;
                if (!c.Modifiers.TryUpgradeCost(st, session.Permanent, def.CostTable[m.Level], def.Id, out long cost)) continue;
                decimal gain = FluidModel.Gain(c.Catalog, machines, income, def.Id, def.SpeedTable[m.Level + 1], def.SpeedTable[m.Level]);
                if (gain > 0) list.Add(new Candidate { InstanceId = m.InstanceId, Cost = cost, Payback = cost / gain });
            }
            return list;
        }
    }

    /// <summary>
    /// The player's mental model of the factory (decimal arithmetic, so the policy decides the same on every host): continuous
    /// flows at the table speeds, upstream machines served first, everything left over sold at the base price. It ignores
    /// Items, Research, Industry, Overdrive and storage limits, as a player estimating from the displayed rates would.
    /// </summary>
    internal static class FluidModel
    {
        public struct Machine
        {
            public string TypeId;
            public long SpeedPpm;
        }

        private static readonly Dictionary<RunCatalog, List<MachineDef>> OrderCache = new Dictionary<RunCatalog, List<MachineDef>>();

        private static List<MachineDef> Order(RunCatalog catalog)
        {
            if (OrderCache.TryGetValue(catalog, out List<MachineDef> cached)) return cached;
            var order = new List<MachineDef>(catalog.Machines.Values);
            order.Sort((a, b) =>
            {
                long pa = catalog.Resources[catalog.Recipes[a.RecipeId].Outputs[0].ResourceId].BasePrice;
                long pb = catalog.Resources[catalog.Recipes[b.RecipeId].Outputs[0].ResourceId].BasePrice;
                return pa != pb ? pa.CompareTo(pb) : string.CompareOrdinal(a.Id, b.Id);
            });
            OrderCache[catalog] = order;
            return order;
        }

        private static void Flow(RunCatalog catalog, List<Machine> machines, out Dictionary<string, decimal> remaining, out Dictionary<string, decimal> gross)
        {
            remaining = new Dictionary<string, decimal>(StringComparer.Ordinal);
            gross = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (string id in catalog.Resources.Keys) { remaining[id] = 0m; gross[id] = 0m; }
            foreach (MachineDef def in Order(catalog))
            {
                decimal speed = 0m;
                foreach (Machine m in machines)
                    if (m.TypeId == def.Id) speed += m.SpeedPpm / 1_000_000m;
                if (speed == 0m) continue;
                MachineRecipe recipe = catalog.Recipes[def.RecipeId];
                decimal seconds = recipe.DurationTicks / 10_000_000m;
                decimal executions = speed / seconds;
                foreach (ResourceAmount input in recipe.Inputs)
                    executions = Math.Min(executions, remaining[input.ResourceId] / input.Amount);
                foreach (ResourceAmount input in recipe.Inputs) remaining[input.ResourceId] -= executions * input.Amount;
                foreach (ResourceAmount output in recipe.Outputs)
                {
                    remaining[output.ResourceId] += executions * output.Amount;
                    gross[output.ResourceId] += executions * output.Amount;
                }
            }
        }

        public static decimal Income(RunCatalog catalog, List<Machine> machines)
        {
            Flow(catalog, machines, out Dictionary<string, decimal> remaining, out _);
            var types = new List<string>();
            foreach (Machine m in machines) types.Add(m.TypeId);
            HashSet<string> consumed = Kept(catalog, types);
            decimal income = 0m;
            foreach (KeyValuePair<string, decimal> pair in remaining) income += pair.Value * catalog.Resources[pair.Key].BasePrice;
            return income;
        }

        /// <summary>
        /// The resources the player keeps for the built machines (auto-sell off): a resource whose built consumer needs more units
        /// per cycle than any recipe delivers per cycle can only be used once it has piled up (2 Bars for a Press against 1 Bar per
        /// Smelter cycle), so selling it at once would starve that consumer. Ore (2 per cycle against 2 needed) is sold, because a
        /// waiting Smelter takes what it needs from each Miner delivery before the sale.
        /// </summary>
        public static HashSet<string> Kept(RunCatalog catalog, IEnumerable<string> builtTypeIds)
        {
            var kept = new HashSet<string>(StringComparer.Ordinal);
            foreach (string typeId in builtTypeIds)
            {
                foreach (ResourceAmount input in catalog.Recipes[catalog.Machines[typeId].RecipeId].Inputs)
                {
                    kept.Add(input.ResourceId);
                }
            }
            return kept;
        }

        /// <summary>Units per second of a resource the current machines produce (consumers ignored).</summary>
        public static decimal GrossRate(RunCatalog catalog, List<Machine> machines, string resourceId)
        {
            Flow(catalog, machines, out _, out Dictionary<string, decimal> gross);
            return gross[resourceId];
        }

        /// <summary>Income gained by adding a machine (<paramref name="oldSpeedPpm"/> null) or raising one machine's speed.</summary>
        public static decimal Gain(RunCatalog catalog, List<Machine> machines, decimal baseIncome, string typeId, long newSpeedPpm, long? oldSpeedPpm)
        {
            var changed = new List<Machine>(machines);
            if (oldSpeedPpm.HasValue)
            {
                for (int i = 0; i < changed.Count; i++)
                {
                    if (changed[i].TypeId != typeId || changed[i].SpeedPpm != oldSpeedPpm.Value) continue;
                    changed[i] = new Machine { TypeId = typeId, SpeedPpm = newSpeedPpm };
                    break;
                }
            }
            else
            {
                changed.Add(new Machine { TypeId = typeId, SpeedPpm = newSpeedPpm });
            }
            return Income(catalog, changed) - baseIncome;
        }
    }
}
