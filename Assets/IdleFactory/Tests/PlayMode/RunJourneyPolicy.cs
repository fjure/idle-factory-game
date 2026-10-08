using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>What the journey policy did and saw, for the SC-001 chain assertions of a player run.</summary>
    internal sealed class JourneyTally
    {
        public long ContractsDelivered;
        public long GearContractsDelivered;
        public long ItemsGranted;
        public long ItemsActivated;
        public long ResearchPurchases;
        public long OverdriveActivations;
        public long IndustryPurchases;

        /// <summary>Seconds into the run at which the Gear contract unlocked (a Gear was produced); -1 until then.</summary>
        public long GearProducedAtSec = -1;

        public long Rejections;
    }

    /// <summary>
    /// A deterministic, human-like player for the StandalonePlayer journey (T121). It is a compact port of the balance
    /// trace's reference policy (Edit Mode, <c>ReferencePolicy</c>): it looks at the factory every 20 simulated seconds
    /// and issues the real application commands (<see cref="RunCommands"/>) against the real session, so the same chain
    /// that the trace proves in the editor runs in the built player: it fills the Iron chain (Miner, Smelter, Press,
    /// Assembler), delivers Contracts, activates Items and Overdrive, buys Research and, after a prestige, Industry upgrades.
    /// It reads costs from the public snapshots and never edits state directly.
    /// </summary>
    internal sealed class RunJourneyPolicy
    {
        public const int LookSeconds = 20;
        public const long TicksPerSecond = 10_000_000L;
        private const int ActionsPerLook = 10;
        private const int StorageFullPercent = 60;
        private const int DumpPercent = 25;
        private const int ContractCapacityPercent = 33;
        private const int AbandonUntimedAfterSeconds = 900;
        private const int WaitForMachineSeconds = 90;
        private const int WaitForStorageSeconds = 120;
        private const int WaitForFactorySeconds = 180;

        private static readonly string[] ResearchOrder =
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

        private static readonly string[] IndustryOrder =
        {
            "industry.calibration", "industry.seed-capital", "industry.market-contacts", "industry.toolmaker", "industry.warehouse",
        };

        private static readonly string[] BuildOrder =
        {
            "machine.miner", "machine.miner", "machine.smelter", "machine.miner", "machine.smelter", "machine.press",
            "machine.smelter", "machine.assembler", "machine.press", "machine.smelter", "machine.press", "machine.assembler",
        };

        private readonly RunRig rig;
        private readonly List<MachineDef> pricedOrder;
        private decimal observedIncome;

        public RunJourneyPolicy(RunRig rig)
        {
            this.rig = rig;
            pricedOrder = new List<MachineDef>(rig.Catalog.Machines.Values);
            pricedOrder.Sort((a, b) =>
            {
                long pa = rig.Catalog.Resources[rig.Catalog.Recipes[a.RecipeId].Outputs[0].ResourceId].BasePrice;
                long pb = rig.Catalog.Resources[rig.Catalog.Recipes[b.RecipeId].Outputs[0].ResourceId].BasePrice;
                return pa != pb ? pa.CompareTo(pb) : string.CompareOrdinal(a.Id, b.Id);
            });
        }

        public JourneyTally Tally { get; } = new JourneyTally();

        private RunSession Session => rig.Session;
        private RunCommands Commands => rig.Commands;
        private RunCatalog Catalog => rig.Catalog;

        /// <summary>
        /// Plays the current run until its Factory Value reaches the prestige threshold and returns the run seconds that took.
        /// Fails (throws) after six simulated hours without reaching it.
        /// </summary>
        public long PlayUntilEligible(long capSeconds = 21_600)
        {
            observedIncome = 0m;
            long t = 0;
            Look();
            while (t < capSeconds)
            {
                long cashBefore = Session.State.Cash;
                var advanced = Session.Advance(LookSeconds * TicksPerSecond);
                if (!advanced.IsSuccess) throw new InvalidOperationException("Advance rejected: " + advanced.ReasonCode);
                t += LookSeconds;
                observedIncome = (observedIncome * 3m + Math.Max((Session.State.Cash - cashBefore) / (decimal)LookSeconds, 0m)) / 4m;
                if (Tally.GearProducedAtSec < 0 && Session.State.IsUnlocked("contract.gear")) Tally.GearProducedAtSec = t;
                var preview = Commands.GetPrestigePreview();
                if (preview.IsSuccess && preview.Value.IsEligible) return t;
                Look();
            }

            throw new InvalidOperationException("The run did not reach the prestige threshold within " + capSeconds + " simulated seconds.");
        }

        /// <summary>Spends all Influence Points on Industry upgrades in priority order; returns the number of purchases.</summary>
        public int SpendIndustry()
        {
            int purchases = 0;
            bool bought = true;
            while (bought)
            {
                bought = false;
                foreach (string id in IndustryOrder)
                {
                    if (!Catalog.IndustryUpgrades.TryGetValue(id, out IndustryUpgradeDef def)) continue;
                    long level = Session.Permanent.IndustryLevel(id);
                    if (level >= def.MaxLevel || Session.Permanent.Ip < def.IpCostTable[(int)level]) continue;
                    if (!Try(Commands.PurchaseIndustry(id))) continue;
                    Tally.IndustryPurchases++;
                    purchases++;
                    bought = true;
                    break;
                }
            }

            return purchases;
        }

        private bool Try<T>(RunCommandResult<T> result)
        {
            if (!result.IsSuccess) Tally.Rejections++;
            return result.IsSuccess;
        }

        private void Look()
        {
            int budget = ActionsPerLook;
            long now = Session.TimelineUtcMs;
            DeliverContract(ref budget);
            AbandonStale(now, ref budget);
            AcceptContract(now, ref budget);
            ReconcileHoard(ref budget);
            ActivateItems(ref budget);
            ActivateOverdrive(now, ref budget);
            PurchaseResearch(ref budget);
            Invest(ref budget);
        }

        // ---- Contracts ----------------------------------------------------------------------------------

        private void DeliverContract(ref int budget)
        {
            RunState st = Session.State;
            ActiveContract active = st.Contracts.Active;
            if (active == null || budget <= 0) return;
            ContractOfferDef offer = Catalog.ContractOffers[active.OfferId];
            if (st.Inventory.Units(offer.ResourceId) < active.RequiredUnits) return;
            budget--;
            var delivered = Commands.DeliverContract();
            if (!Try(delivered)) return;
            Tally.ContractsDelivered++;
            if (offer.Kind == "gear") Tally.GearContractsDelivered++;
            foreach (IdCount grant in delivered.Value.ItemsGranted) Tally.ItemsGranted += grant.Count;
        }

        private void AbandonStale(long now, ref int budget)
        {
            ActiveContract active = Session.State.Contracts.Active;
            if (active == null || active.DeadlineUtcMs.HasValue || budget <= 0) return;
            if (now - active.AcceptedUtcMs < AbandonUntimedAfterSeconds * 1000L) return;
            budget--;
            Try(Commands.AbandonContract());
        }

        private void AcceptContract(long now, ref int budget)
        {
            RunState st = Session.State;
            if (st.Contracts.Active != null || budget <= 0) return;
            long capacity = st.Inventory.Capacity;
            ContractOfferDef best = null;
            long bestCount = 0;
            long bestMarket = 0;
            foreach (ContractOfferDef offer in Catalog.ContractOffers.Values)
            {
                if (!st.IsUnlocked(offer.Id)) continue;
                long count = st.Contracts.CompletionCount(offer.Id);
                if (count >= offer.QuantityTable.Count) continue;
                long required = offer.QuantityTable[(int)count];
                if (required * 100 > capacity * ContractCapacityPercent) continue;

                decimal rate = GrossRate(offer.ResourceId);
                if (rate <= 0) continue;
                decimal secondsNeeded = ((decimal)required - st.Inventory.Units(offer.ResourceId)) / rate;
                if (offer.DurationMs.HasValue && secondsNeeded * 1000m > offer.DurationMs.Value) continue;
                if (!offer.DurationMs.HasValue && secondsNeeded > AbandonUntimedAfterSeconds) continue;

                long market = required * Catalog.Resources[offer.ResourceId].BasePrice;
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
            Try(Commands.AcceptContract(best.Id));
        }

        /// <summary>
        /// Auto-sell follows the contract: the contract's resource is kept while it is active and the machines that would
        /// consume it are switched off for that time. A resource a built machine consumes is kept for it; everything else sells.
        /// </summary>
        private void ReconcileHoard(ref int budget)
        {
            RunState st = Session.State;
            ActiveContract active = st.Contracts.Active;
            string hoard = active == null ? null : Catalog.ContractOffers[active.OfferId].ResourceId;
            HashSet<string> consumed = Kept();
            foreach (ResourceDef resource in SortedResources())
            {
                if (!st.IsUnlocked(resource.Id)) continue;
                bool wanted = resource.Id != hoard && !consumed.Contains(resource.Id);
                if (st.IsAutoSell(resource.Id) == wanted || budget <= 0) continue;
                budget--;
                Try(Commands.SetAutoSell(resource.Id, wanted));
            }

            // A surplus of a kept resource is dumped: auto-sell on and off again sells the whole stock at once.
            foreach (ResourceDef resource in SortedResources())
            {
                if (!consumed.Contains(resource.Id) || resource.Id == hoard || !st.IsUnlocked(resource.Id) || budget < 1) continue;
                if (st.Inventory.Units(resource.Id) * 100 < st.Inventory.Capacity * DumpPercent) continue;
                budget--;
                Try(Commands.SetAutoSell(resource.Id, true));
                Try(Commands.SetAutoSell(resource.Id, false));
            }

            foreach (MachineInstance m in new List<MachineInstance>(st.Machines))
            {
                MachineDef def = Catalog.Machines[m.MachineId];
                bool consumes = false;
                if (hoard != null)
                    foreach (ResourceAmount input in Catalog.Recipes[def.RecipeId].Inputs)
                        consumes |= input.ResourceId == hoard;
                if (m.IsActive == !consumes || budget <= 0) continue;
                budget--;
                Try(Commands.SetActive(m.InstanceId, !consumes));
            }
        }

        private List<ResourceDef> SortedResources()
        {
            var list = new List<ResourceDef>(Catalog.Resources.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        // ---- Items, Overdrive, Research -----------------------------------------------------------------

        private void ActivateItems(ref int budget)
        {
            RunState st = Session.State;
            if (st.Machines.Count == 0) return;
            var items = new List<ItemDef>(Catalog.Items.Values);
            items.Sort((a, b) => a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : string.CompareOrdinal(a.Id, b.Id));
            foreach (ItemDef item in items)
            {
                if (budget <= 0 || Session.Permanent.ItemCount(item.Id) < 1) continue;
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

                if (ItemConflicts(item, target)) continue;
                budget--;
                if (Try(Commands.ActivateItem(item.Id, target))) Tally.ItemsActivated++;
            }
        }

        private bool ItemConflicts(ItemDef item, string target)
        {
            string key = item.TargetKind == "factory" ? RunCommands.FactoryTargetKey : target;
            long now = Session.TimelineUtcMs;
            foreach (ActiveEffect e in Session.State.Effects.Effects)
                if (e.ConflictKey.EffectKind == item.Effect.Kind && e.ConflictKey.TargetKey == key && e.EndUtcMs > now) return true;
            return false;
        }

        private void ActivateOverdrive(long now, ref int budget)
        {
            RunState st = Session.State;
            if (budget <= 0 || !st.IsUnlocked(RunState.OverdriveUnlockId) || st.Machines.Count < 3) return;
            if (st.Overdrive.CooldownUntilUtcMs.HasValue && now < st.Overdrive.CooldownUntilUtcMs.Value) return;
            budget--;
            if (Try(Commands.ActivateOverdrive())) Tally.OverdriveActivations++;
        }

        private void PurchaseResearch(ref int budget)
        {
            RunState st = Session.State;
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
                ResearchDef def = Catalog.Research[next];
                int level = (int)st.ResearchLevel(next);
                if (level >= def.MaxLevel || st.Rp < def.RpCostTable[level]) return;
                budget--;
                if (!Try(Commands.PurchaseResearch(next))) return;
                Tally.ResearchPurchases++;
            }
        }

        // ---- Investment ---------------------------------------------------------------------------------

        private sealed class Candidate
        {
            public Guid InstanceId;
            public long Cost;
            public decimal Payback;
        }

        private void Invest(ref int budget)
        {
            while (budget > 0)
            {
                RunState st = Session.State;
                RunFactorySnapshot snap = RunSnapshots.Factory(Session);
                List<Machine> machines = MachineList();
                decimal rawIncome = Income(machines);
                decimal income = Math.Max(observedIncome, 1m);

                ExpansionSnapshot storage = snap.Storage;
                if (storage.HasNext && (st.Inventory.Total * 100 >= st.Inventory.Capacity * StorageFullPercent || AnyMachineBlockedByStorage()))
                {
                    if (st.Cash >= storage.NextCost)
                    {
                        budget--;
                        if (Try(Commands.UpgradeStorage())) continue;
                        return;
                    }

                    if ((storage.NextCost - st.Cash) / income <= WaitForStorageSeconds) return;
                }

                int free = FirstFreeSlot(st);
                string next = free >= 0 ? NextInOrder(st) : null;
                if (next != null)
                {
                    long buildCost = BuildCost(snap, next);
                    if (st.Cash >= buildCost)
                    {
                        budget--;
                        if (Try(Commands.Build(free, next))) continue;
                        return;
                    }

                    if ((buildCost - st.Cash) / income <= WaitForMachineSeconds) return;
                }

                ExpansionSnapshot factory = snap.Factory;
                if (free < 0 && factory.HasNext)
                {
                    if (st.Cash >= factory.NextCost)
                    {
                        budget--;
                        if (Try(Commands.UpgradeFactory())) continue;
                        return;
                    }

                    if ((factory.NextCost - st.Cash) / income <= WaitForFactorySeconds) return;
                }

                var candidates = new List<Candidate>();
                foreach (MachineSnapshot m in snap.Machines)
                {
                    UpgradePreview up = m.NextUpgrade;
                    if (up == null || !up.Available) continue;
                    MachineDef def = Catalog.Machines[m.MachineId];
                    decimal gain = Gain(machines, rawIncome, def.Id, def.SpeedTable[m.Level + 1], def.SpeedTable[m.Level]);
                    if (gain > 0) candidates.Add(new Candidate { InstanceId = m.InstanceId, Cost = up.Cost, Payback = up.Cost / gain });
                }

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
                if (!Try(Commands.Upgrade(pick.InstanceId))) return;
            }
        }

        private static long BuildCost(RunFactorySnapshot snap, string machineId)
        {
            foreach (MachineTypeSnapshot type in snap.MachineTypes)
                if (type.MachineId == machineId) return type.BuildCost;
            throw new InvalidOperationException("Unknown machine type " + machineId);
        }

        private bool AnyMachineBlockedByStorage()
        {
            var views = new MachineStateView[Session.State.Machines.Count];
            int n = Session.CopyMachineStates(views);
            for (int i = 0; i < n; i++)
                if (views[i].Blocker == MachineBlocker.StorageFull) return true;
            return false;
        }

        private static string NextInOrderCore(RunState st)
        {
            for (int i = st.Machines.Count; i < BuildOrder.Length; i++)
                if (st.IsUnlocked(BuildOrder[i])) return BuildOrder[i];
            return null;
        }

        private string NextInOrder(RunState st) => NextInOrderCore(st);

        private static int FirstFreeSlot(RunState st)
        {
            for (int i = 0; i < st.SlotCount; i++)
                if (st.MachineAtSlot(i) == null) return i;
            return -1;
        }

        // ---- The player's mental model: continuous flows at the table speeds -----------------------------

        private struct Machine
        {
            public string TypeId;
            public long SpeedPpm;
        }

        private List<Machine> MachineList()
        {
            var list = new List<Machine>();
            foreach (MachineInstance m in Session.State.Machines)
                list.Add(new Machine { TypeId = m.MachineId, SpeedPpm = Catalog.Machines[m.MachineId].SpeedTable[m.Level] });
            return list;
        }

        private HashSet<string> Kept()
        {
            var kept = new HashSet<string>(StringComparer.Ordinal);
            foreach (MachineInstance m in Session.State.Machines)
                foreach (ResourceAmount input in Catalog.Recipes[Catalog.Machines[m.MachineId].RecipeId].Inputs)
                    kept.Add(input.ResourceId);
            return kept;
        }

        private void Flow(List<Machine> machines, out Dictionary<string, decimal> remaining, out Dictionary<string, decimal> gross)
        {
            remaining = new Dictionary<string, decimal>(StringComparer.Ordinal);
            gross = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (string id in Catalog.Resources.Keys) { remaining[id] = 0m; gross[id] = 0m; }
            foreach (MachineDef def in pricedOrder)
            {
                decimal speed = 0m;
                foreach (Machine m in machines)
                    if (m.TypeId == def.Id) speed += m.SpeedPpm / 1_000_000m;
                if (speed == 0m) continue;
                MachineRecipe recipe = Catalog.Recipes[def.RecipeId];
                decimal executions = speed / (recipe.DurationTicks / 10_000_000m);
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

        private decimal Income(List<Machine> machines)
        {
            Flow(machines, out Dictionary<string, decimal> remaining, out _);
            decimal income = 0m;
            foreach (KeyValuePair<string, decimal> pair in remaining) income += pair.Value * Catalog.Resources[pair.Key].BasePrice;
            return income;
        }

        private decimal GrossRate(string resourceId)
        {
            Flow(MachineList(), out _, out Dictionary<string, decimal> gross);
            return gross[resourceId];
        }

        private decimal Gain(List<Machine> machines, decimal baseIncome, string typeId, long newSpeedPpm, long oldSpeedPpm)
        {
            var changed = new List<Machine>(machines);
            for (int i = 0; i < changed.Count; i++)
            {
                if (changed[i].TypeId != typeId || changed[i].SpeedPpm != oldSpeedPpm) continue;
                changed[i] = new Machine { TypeId = typeId, SpeedPpm = newSpeedPpm };
                break;
            }

            return Income(changed) - baseIncome;
        }
    }
}
