using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Tests.EditMode.Balance
{
    /// <summary>
    /// The pass/fail bands and sensitivity bounds of <c>specs/003-first-factory-prestige/reviews/balance-policy.md</c>
    /// (ECO-2, ECO-4). Since T341 the minute bands are orientation (balance-policy-r2.md section 0): the trace reports pass or fail per band
    /// and no test fails on one. The sensitivity bounds are the measurement tolerances of FR-025.
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

        /// <summary>Price variants: play time to the milestone within +/- this share of the baseline (parts per 1000).</summary>
        public const long PriceBoundPermille = 250;

        /// <summary>
        /// Contracts disabled or throttled: play time may grow by at most this share (parts per 1000), and may not shrink by more than 50 of 1000.
        /// Re-derived under policy r2 (FR-025, policy-r2-record.md section 5): 250 under policy r1; policy r2 measures +256 for contracts-off.
        /// </summary>
        public const long ContractGrowthBoundPermille = 300;

        /// <summary>Overdrive disabled: play time may grow by at most this share (parts per 1000).</summary>
        public const long OverdriveGrowthBoundPermille = 150;

        /// <summary>
        /// Offline sensitivity (FR-025): an eight-hour absence may remove at most this share of the baseline play time
        /// (parts per 1000). Play time is the wall time to eligibility minus the absence.
        /// </summary>
        public const long AbsenceMaxSavingPermille = 500;

        /// <summary>
        /// Repeated short returns: wall time to the milestone between these shares of the baseline (parts per 1000). Re-derived under policy r2
        /// (FR-025): the F003 upper bound of 1,600 was a measurement of policy r1; policy r2 reaches the milestone sooner, so the same absences
        /// are a larger share of the wall time. A measurement tolerance only; it never changes the 80 % offline effectiveness or the 8-hour cap (D-A).
        /// </summary>
        public const long ReturnsMinPermille = 600;
        public const long ReturnsMaxPermille = 2_100;
    }

    /// <summary>
    /// The deterministic, human-like reference policy r2 (balance-policy-r2.md; T341). It is the same kind of player as policy r1: it looks at the
    /// factory at fixed instants, issues the real application commands against a manual clock and reads only what a player sees. Every strategy of
    /// FR-023 is a configuration of it (<see cref="Strategies"/>). Rules that the document words in terms of [catalog], [pinned] and [state] values
    /// read exactly those.
    /// </summary>
    internal sealed class ReferencePolicy : IStrategyPolicy
    {
        // ---- [pinned] constants of the policy -----------------------------------------------------------------

        /// <summary>The player looks at the factory every 20 simulated seconds: this is the action latency.</summary>
        public const int EpochSeconds = 20;

        /// <summary>Idle window: the player is away from the screen for the last 60 s of every 300 s (20 % of the time).</summary>
        public const int IdleCycleSeconds = 300;
        public const int IdleStartSeconds = 240;

        /// <summary>At most this many commands per look. <c>Mine</c> and the held click do not count.</summary>
        public const int ActionsPerLook = 10;

        public const int WaitForMachineSeconds = 90;
        public const int WaitForStorageSeconds = 120;
        public const int WaitForFactorySeconds = 180;

        /// <summary>A contract accepted without a deadline is abandoned after this many seconds.</summary>
        public const int AbandonUntimedAfterSeconds = 900;

        /// <summary>A contract is accepted only when its quantity fits this share of the capacity (percent).</summary>
        public const int ContractCapacityPercent = 33;

        /// <summary>A kept resource whose stock reaches this share of the capacity (percent) is sold down to <see cref="DumpTargetPercent"/>.</summary>
        public const int DumpPercent = 25;
        public const int DumpTargetPercent = 10;

        /// <summary>The Factory Value is judged against the threshold for at most this many seconds of run time (FR-024).</summary>
        public const long RunLimitSeconds = 21_600;

        /// <summary>The held mining click of chain+mining and mining-only mines at the whole per-second cap (balance-policy-r2.md section 7).</summary>
        public const long ChainMiningFractionPpm = 1_000_000;

        /// <summary>The held mining click of the casual strategy mines at 25 % of the per-second cap (balance-policy-r2.md section 7, owner answer Q9).</summary>
        public const long CasualMiningFractionPpm = 250_000;

        /// <summary>Prestige-later factors (balance-policy-r2.md section 7).</summary>
        public const long LaterFactorLowPpm = 1_500_000;
        public const long LaterFactorHighPpm = 3_000_000;

        /// <summary>Machine types of the stop-at-Bar and stop-at-Plate scopes (output tier at most Iron Bar, respectively Iron Plate).</summary>
        public static readonly string[] StopAtBarScope = { "machine.miner", "machine.smelter" };
        public static readonly string[] StopAtPlateScope = { "machine.miner", "machine.smelter", "machine.press" };

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

        /// <summary>
        /// [pinned] Look interval of the casual strategy in seconds (balance-policy-r2.md section 7.1, T311 and T341). It is the smallest
        /// integer, searched upward from 40 s in steps of 1 s, for which the median over the five casual offsets of the Factory Value at run
        /// second 900 of run 1 is at most 0.4 (1 / 2.5, the owner playtest of 2026-10-09) of the chain median at the same second, on policy r2
        /// (including the held mining click at 25 % of the cap) and the content revision 2 values. T311 found 73 s on policy r1 without
        /// mining (provisional); T341 repeated the search on policy r2 once and pinned the result. Derivation, candidates and the measured ratio:
        /// <c>specs/005-economy-contracts/reviews/strategy-baseline-r3.md</c> section 2. It is never recalibrated by a later content revision,
        /// policy change or tuning (so the casual ratio drifts and stays orientation).
        /// </summary>
        public const int CasualLookIntervalSeconds = 78;

        // ---- strategy configuration ---------------------------------------------------------------------------

        /// <summary>The strategy id of the ensemble. The reference policy is the <c>chain</c> strategy.</summary>
        public string Id { get; set; } = "chain";

        /// <summary>Seconds between two looks of the player. The reference value is <see cref="EpochSeconds"/>.</summary>
        public int LookIntervalSeconds { get; set; } = EpochSeconds;

        /// <summary>Second of every <see cref="IdleWindowCycleSeconds"/> at which the away window starts. The reference value is <see cref="IdleStartSeconds"/>.</summary>
        public int IdleWindowStartSeconds { get; set; } = IdleStartSeconds;

        public int IdleWindowCycleSeconds => IdleCycleSeconds;

        /// <summary>Contracts-only strategy: auto-sell stays off for every resource, so the only Cash from the factory is contract rewards.</summary>
        public bool ContractsOnly { get; set; }

        public bool ContractsEnabled { get; set; } = true;
        public bool OverdriveEnabled { get; set; } = true;

        /// <summary>The machine types the strategy builds; null is the whole chain.</summary>
        public IReadOnlyList<string> Scope { get; set; }

        /// <summary>Probe: build Miners and nothing else (Ore-only). A convenience over <see cref="Scope"/>.</summary>
        public bool OreOnly
        {
            get => Scope != null && Scope.Count == 1 && Scope[0] == "machine.miner";
            set => Scope = value ? new[] { "machine.miner" } : null;
        }

        /// <summary>Mining-only: builds nothing, mines the whole cap in every active second and sells by auto-sell.</summary>
        public bool BuildsNothing { get; set; }

        /// <summary>The fraction of the per-second cap the held click mines after the first Miner exists (0 for the chain: it mines only until the first Miner).</summary>
        public long MiningAfterFirstMinerPpm { get; set; }

        /// <summary>
        /// Diagnostic switch for the casual run 2 finding (T341, #373 finding 3), never set by a strategy. Policy r2 reads the storage blocker on arrival,
        /// before the look's own <c>Sell</c> clears it. With this switch on the blocker is read at the investment step, after the sale, which is the order of
        /// policy r1 (its dump ran before its storage rule): at a long look interval the stores then fill between two looks and are never seen full, so the
        /// storage level stays at 1 while the faster machines of run 2 stall on it. <c>PolicyR2Tests.CasualRunTwoSlowdownOfPolicyR1IsTheStorageBlockerReadAfterTheSale</c>
        /// reproduces it.
        /// </summary>
        public bool ReadStorageBlockerAfterSelling { get; set; }

        /// <summary>The prestige happens at Factory Value of this multiple of the threshold (1,000,000 = at the threshold).</summary>
        public long PrestigeFactorPpm { get; set; } = 1_000_000;

        /// <summary>Seconds to wait after a contract ended before accepting the next one (the <c>var.contracts-slow</c> variant only).</summary>
        public int ContractCooldownSeconds { get; set; }

        public static bool IsIdle(long runSeconds) => runSeconds % IdleCycleSeconds >= IdleStartSeconds;

        // ---- per-run state --------------------------------------------------------------------------------------

        private int _offset;
        private bool _hasMiner;
        private long _mineBefore;
        private long _mineAfter;
        private readonly HashSet<Guid> _held = new HashSet<Guid>();

        public int HeldOffMachineCount => _held.Count;

        private static long Activations(long fractionPpm, long cap) => (fractionPpm * cap + 500_000) / 1_000_000;

        public void BeginRun(TraceContext c, int offsetSeconds)
        {
            _offset = offsetSeconds;
            _hasMiner = false;
            _held.Clear();
            long cap = c.Catalog.RunStart.ManualMining == null ? 0 : c.Catalog.RunStart.ManualMining.MaxActivationsPerSecond;
            _mineBefore = Activations(1_000_000, cap);
            _mineAfter = Activations(MiningAfterFirstMinerPpm, cap);
        }

        // ---- the held mining click (section 6) -------------------------------------------------------------------

        public long HeldClickActivations(TraceContext c, long runSecond)
        {
            long n = _hasMiner ? _mineAfter : _mineBefore;
            if (n <= 0 || runSecond < _offset || this.IsIdleAt(runSecond)) return 0;
            return n;
        }

        public long HeldClickResumeSecond(long runSecond)
        {
            if (runSecond < _offset) return _offset;
            long n = _hasMiner ? _mineAfter : _mineBefore;
            if (n <= 0) return long.MaxValue;
            if (this.IsIdleAt(runSecond)) return (runSecond / IdleWindowCycleSeconds + 1) * IdleWindowCycleSeconds;
            return runSecond + 1;
        }

        public IndustryModelOptions ModelOptions(RunCatalog catalog)
        {
            long cap = catalog.RunStart.ManualMining == null ? 0 : catalog.RunStart.ManualMining.MaxActivationsPerSecond;
            return new IndustryModelOptions
            {
                Scope = BuildsNothing ? new string[0] : Scope,
                MineActivationsBeforeMiner = Activations(1_000_000, cap),
                MineActivationsAfterMiner = Activations(MiningAfterFirstMinerPpm, cap),
                IdleStartSeconds = IdleWindowStartSeconds,
                IdleCycleSeconds = IdleWindowCycleSeconds,
            };
        }

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
            c.CapacityBlocked = false;
            // What the player sees on arrival: a machine that stood still because the store was full. A Sell of the look would clear it, so it is read first.
            c.StorageBlockedOnArrival = c.AnyMachineBlockedByStorage();

            if (BuildsNothing)
            {
                ReconcileSelling(c, ref budget);
                return ActionsPerLook - budget;
            }

            DeliverContract(c, ref budget);
            AbandonStale(c, now, ref budget);
            AcceptContract(c, now, ref budget);
            ReconcileSelling(c, ref budget);
            ActivateItems(c, ref budget);
            ActivateOverdrive(c, now, ref budget);
            PurchaseResearch(c, ref budget);
            Invest(c, ref budget);
            NoteMiner(c, st);
            return ActionsPerLook - budget;
        }

        /// <summary>The held click stops (or drops to the strategy's fraction) at the look at which the first Miner stands.</summary>
        private void NoteMiner(TraceContext c, RunState st)
        {
            if (_hasMiner || c.Catalog.RunStart.ManualMining == null) return;
            _hasMiner = OwnsOreProducer(c.Catalog, st, c.Catalog.RunStart.ManualMining.ResourceId);
        }

        // -------------------------------------------------------------------------------------------------
        // Industry (section 5)
        // -------------------------------------------------------------------------------------------------

        /// <summary>
        /// Spends the IP greedily by the fluid-model gain per IP (section 5): the reduction of <c>T_fluid</c>, the time to the next threshold, that
        /// one more level causes, divided by its IP cost. A level whose gain is not positive is never bought; the IP that stays is reported.
        /// </summary>
        public int SpendIndustry(TraceContext c)
        {
            RunCatalog catalog = c.Catalog;
            PermanentState perm = c.Session.Permanent;
            IndustryModelOptions options = ModelOptions(catalog);
            var defs = new List<IndustryUpgradeDef>(catalog.IndustryUpgrades.Values);
            defs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            int purchases = 0;
            while (true)
            {
                var levels = new Dictionary<string, long>(StringComparer.Ordinal);
                foreach (IndustryUpgradeDef def in defs) levels[def.Id] = perm.IndustryLevel(def.Id);
                decimal baseTime = IndustryModel.TimeToThreshold(catalog, levels, perm.PrestigeCount, options);
                IndustryUpgradeDef best = null;
                decimal bestScore = 0m;
                long bestCost = 0;
                foreach (IndustryUpgradeDef def in defs)
                {
                    long level = levels[def.Id];
                    if (level >= def.MaxLevel) continue;
                    long cost = def.IpCostTable[(int)level];
                    if (cost > perm.Ip || cost < 1) continue;
                    levels[def.Id] = level + 1;
                    decimal gain = baseTime - IndustryModel.TimeToThreshold(catalog, levels, perm.PrestigeCount, options);
                    levels[def.Id] = level;
                    if (gain <= 0m) continue;
                    decimal score = gain / cost;
                    if (best == null || score > bestScore || (score == bestScore && cost < bestCost))
                    {
                        best = def;
                        bestScore = score;
                        bestCost = cost;
                    }
                }
                if (best == null || !c.PurchaseIndustry(best.Id)) break;
                purchases++;
            }

            // What stays unspent and which affordable levels the model values at zero (a model blind spot, reported, never bought).
            c.Record.Add("unspent_ip", perm.Ip);
            foreach (IndustryUpgradeDef def in defs)
            {
                long level = perm.IndustryLevel(def.Id);
                if (level < def.MaxLevel && def.IpCostTable[(int)level] <= perm.Ip) c.Record.Add("industry_zero_gain." + def.Id, 1);
            }
            return purchases;
        }

        // -------------------------------------------------------------------------------------------------
        // Contracts (section 4.1, 4.2)
        // -------------------------------------------------------------------------------------------------

        private static void DeliverContract(TraceContext c, ref int budget)
        {
            RunState st = c.Session.State;
            ActiveContract active = st.Contracts.Active;
            if (active == null || budget <= 0) return;
            ContractOfferDef offer = c.Catalog.ContractOffers[active.OfferId];
            if (!offer.IsDelivery) return;
            if (st.Inventory.Units(offer.ResourceId) < active.RequiredUnits) return;
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

        private sealed class OfferChoice
        {
            public ContractOfferDef Offer;
            public long Count;
            public long Required;
            public long WindowEnd;
            public long Market;
            public bool CapacityOk;
        }

        /// <summary>The ranking of section 4.1: bonus offers first (earliest window end, then id), then fewest completions, smallest market value, id.</summary>
        private static bool Better(OfferChoice a, OfferChoice b)
        {
            if (b == null) return true;
            if (a.Offer.IsBonus != b.Offer.IsBonus) return a.Offer.IsBonus;
            if (a.Offer.IsBonus)
            {
                if (a.WindowEnd != b.WindowEnd) return a.WindowEnd < b.WindowEnd;
                return string.CompareOrdinal(a.Offer.Id, b.Offer.Id) < 0;
            }
            if (a.Count != b.Count) return a.Count < b.Count;
            if (a.Market != b.Market) return a.Market < b.Market;
            return string.CompareOrdinal(a.Offer.Id, b.Offer.Id) < 0;
        }

        private void AcceptContract(TraceContext c, long now, ref int budget)
        {
            RunState st = c.Session.State;
            if (!ContractsEnabled || st.Contracts.Active != null || budget <= 0 || now < c.ContractCooldownUntilUtcMs) return;
            long capacity = st.Inventory.Capacity;
            OfferChoice bestAny = null;
            OfferChoice bestFitting = null;
            var offers = new List<ContractOfferDef>(c.Catalog.ContractOffers.Values);
            offers.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (ContractOfferDef offer in offers)
            {
                if (!st.IsUnlocked(offer.Id)) continue;
                // Available as the snapshot shows it: not on cooldown, and a bonus offer inside its window.
                if (st.Contracts.CooldownRemainingMs(offer.Id, now) > 0 || !st.Contracts.IsBonusAvailable(offer.Id, now)) continue;
                long count = st.Contracts.CompletionCount(offer.Id);
                if (count >= offer.QuantityTable.Count) continue;
                long required = offer.QuantityTable[(int)count];

                // Feasibility: a built machine produces the resource and the quantity can be made within the deadline (or the untimed limit).
                decimal rate = FluidModel.GrossRate(c.Catalog, c.MachineList(), offer.ResourceId);
                if (rate <= 0) continue;
                decimal stock = offer.IsDelivery ? st.Inventory.Units(offer.ResourceId) : 0m;
                decimal secondsNeeded = ((decimal)required - stock) / rate;
                if (offer.DurationMs.HasValue && secondsNeeded * 1000m > offer.DurationMs.Value) continue;
                if (!offer.DurationMs.HasValue && secondsNeeded > AbandonUntimedAfterSeconds) continue;

                var choice = new OfferChoice
                {
                    Offer = offer,
                    Count = count,
                    Required = required,
                    WindowEnd = offer.IsBonus ? st.Contracts.BonusState(offer.Id).WindowEndUtcMs : 0,
                    Market = required * c.Catalog.Resources[offer.ResourceId].BasePrice,
                    CapacityOk = required * 100 <= capacity * ContractCapacityPercent && required <= capacity,
                };
                if (Better(choice, bestAny)) bestAny = choice;
                if (choice.CapacityOk && Better(choice, bestFitting)) bestFitting = choice;
            }

            // The best offer that was held back only by the capacity rule asks for storage (section 4.4 rule 1).
            if (bestAny != null && !bestAny.CapacityOk) c.CapacityBlocked = true;
            if (bestFitting == null) return;
            budget--;
            if (!c.AcceptContract(bestFitting.Offer.Id)) return;
            if (bestFitting.Offer.IsDelivery && budget > 0 && c.Session.State.Contracts.Active != null)
            {
                budget--;
                c.SetAutoDeliver(true);
            }
        }

        // -------------------------------------------------------------------------------------------------
        // Auto-sell, Sell and the contract reserve (sections 4.2, 4.3)
        // -------------------------------------------------------------------------------------------------

        private static List<ResourceDef> SortedResources(RunCatalog catalog)
        {
            var list = new List<ResourceDef>(catalog.Resources.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        private void ReconcileSelling(TraceContext c, ref int budget)
        {
            RunState st = c.Session.State;
            ActiveContract active = st.Contracts.Active;
            ContractOfferDef activeOffer = active == null ? null : c.Catalog.ContractOffers[active.OfferId];
            string reserved = activeOffer != null && activeOffer.IsDelivery ? activeOffer.ResourceId : null;
            var types = new List<string>();
            foreach (MachineInstance m in st.Machines) types.Add(m.MachineId);
            HashSet<string> kept = FluidModel.Kept(c.Catalog, types);
            List<ResourceDef> resources = SortedResources(c.Catalog);

            // Kept when a built machine consumes it, sold otherwise; contracts-only sells nothing.
            foreach (ResourceDef resource in resources)
            {
                if (!st.IsUnlocked(resource.Id)) continue;
                bool wanted = !ContractsOnly && !kept.Contains(resource.Id);
                if (st.IsAutoSell(resource.Id) == wanted || budget <= 0) continue;
                budget--;
                c.SetAutoSell(resource.Id, wanted);
            }

            // Surplus of a kept resource is sold down with Sell, never below the units a delivery contract reserves.
            if (!ContractsOnly)
            {
                foreach (ResourceDef resource in resources)
                {
                    if (!kept.Contains(resource.Id) || !st.IsUnlocked(resource.Id) || budget < 1) continue;
                    long capacity = st.Inventory.Capacity;
                    long stock = st.Inventory.Units(resource.Id);
                    if (stock * 100 < capacity * DumpPercent) continue;
                    long target = (DumpTargetPercent * capacity + 99) / 100;
                    if (resource.Id == reserved) target = Math.Max(target, active.RequiredUnits);
                    long quantity = stock - target;
                    if (quantity < 1) continue;
                    budget--;
                    if (c.Sell(resource.Id, quantity)) c.Record.Add("surplus_dumps", 1);
                }
            }

            // While a delivery contract is active its consumers are switched off so the needed units are not consumed downstream.
            foreach (MachineInstance m in new List<MachineInstance>(st.Machines))
            {
                MachineDef def = c.Catalog.Machines[m.MachineId];
                bool consumes = false;
                if (reserved != null)
                    foreach (ResourceAmount input in c.Catalog.Recipes[def.RecipeId].Inputs)
                        consumes |= input.ResourceId == reserved;
                if (m.IsActive == !consumes || budget <= 0) continue;
                budget--;
                if (c.SetActive(m.InstanceId, !consumes))
                {
                    if (consumes) _held.Add(m.InstanceId);
                    else _held.Remove(m.InstanceId);
                }
            }
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

        /// <summary>A purchase whose target is outside the strategy's scope is skipped (balance-policy-r2.md section 7, Ore-only).</summary>
        private bool ResearchInScope(RunCatalog catalog, ResearchDef def)
        {
            if (Scope == null) return true;
            EffectDef e = def.Effect;
            if (e.TargetKind == "machine") return Scope.Contains(e.TargetId);
            if (e.TargetKind == "resource")
            {
                foreach (string typeId in Scope)
                    foreach (ResourceAmount output in catalog.Recipes[catalog.Machines[typeId].RecipeId].Outputs)
                        if (output.ResourceId == e.TargetId) return true;
                return false;
            }
            return true;
        }

        private void PurchaseResearch(TraceContext c, ref int budget)
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
                    if (!ResearchInScope(c.Catalog, c.Catalog.Research[id])) continue;
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
        // Investment (section 4.4): storage on a blocker, the balanced mix, factory level, upgrades by payback
        // -------------------------------------------------------------------------------------------------

        private sealed class Candidate
        {
            public Guid InstanceId;
            public string TypeId;
            public int Slot;
            public long Cost;
            public decimal Payback;
        }

        private static bool BetterPayback(Candidate a, Candidate b)
        {
            if (b == null) return true;
            if (a.Payback != b.Payback) return a.Payback < b.Payback;
            int byType = string.CompareOrdinal(a.TypeId, b.TypeId);
            return byType != 0 ? byType < 0 : a.Slot < b.Slot;
        }

        private static bool Cheaper(Candidate a, Candidate b)
        {
            if (b == null) return true;
            if (a.Cost != b.Cost) return a.Cost < b.Cost;
            return BetterPayback(a, b);
        }

        private void Invest(TraceContext c, ref int budget)
        {
            while (budget > 0)
            {
                RunSession session = c.Session;
                RunState st = session.State;
                PermanentState perm = session.Permanent;
                List<FluidModel.Machine> machines = c.MachineList();
                decimal rawIncome = FluidModel.Income(c.Catalog, machines);
                decimal income = c.ObservedIncome;

                // 1. Storage on a StorageFull blocker, or when the best offer was held back only by the capacity rule.
                if (st.StorageLevel < c.Catalog.StorageLevels.Count && (c.CapacityBlocked || (ReadStorageBlockerAfterSelling ? c.AnyMachineBlockedByStorage() : c.StorageBlockedOnArrival)) &&
                    c.Modifiers.TryUpgradeCost(st, perm, c.Catalog.StorageLevels[st.StorageLevel].Cost, null, out long storageCost))
                {
                    if (st.Cash >= storageCost)
                    {
                        budget--;
                        c.CapacityBlocked = false;
                        c.StorageBlockedOnArrival = false;
                        if (c.UpgradeStorage()) continue;
                        return;
                    }
                    if ((storageCost - st.Cash) / income <= WaitForStorageSeconds) return;
                }

                // 2. The next machine of the balanced mix. A locked type holds its deficit: nothing is built in its place.
                int free = FirstFreeSlot(st);
                MachineDef next = free >= 0 && !BuildsNothing ? NextMixType(c, st) : null;
                if (next != null && BuildPricing.TryPrice(c.Catalog, c.Modifiers, st, perm, next, out long buildCost))
                {
                    if (st.Cash >= buildCost)
                    {
                        budget--;
                        if (c.Build(free, next.Id)) continue;
                        return;
                    }
                    if ((buildCost - st.Cash) / income <= WaitForMachineSeconds) return;
                }

                // 3. A factory level when no machine can be built (no free slot, or every missing type is still locked).
                if (next == null && st.FactoryLevel < c.Catalog.FactoryLevels.Count &&
                    c.Modifiers.TryUpgradeCost(st, perm, c.Catalog.FactoryLevels[st.FactoryLevel].Cost, null, out long factoryCost))
                {
                    if (st.Cash >= factoryCost)
                    {
                        budget--;
                        if (c.UpgradeFactory()) continue;
                        return;
                    }
                    if ((factoryCost - st.Cash) / income <= WaitForFactorySeconds) return;
                }

                // 4. Machine upgrades by payback.
                var candidates = new List<Candidate>();
                foreach (MachineInstance m in st.Machines)
                {
                    MachineDef def = c.Catalog.Machines[m.MachineId];
                    if (m.Level >= m.MaxLevel) continue;
                    if (!c.Modifiers.TryUpgradeCost(st, perm, def.CostTable[m.Level], def.Id, out long cost)) continue;
                    decimal gain = FluidModel.Gain(c.Catalog, machines, rawIncome, def.Id, def.SpeedTable[m.Level + 1], def.SpeedTable[m.Level]);
                    if (gain > 0) candidates.Add(new Candidate { InstanceId = m.InstanceId, TypeId = def.Id, Slot = m.SlotIndex, Cost = cost, Payback = cost / gain });
                }
                Candidate best = null;
                foreach (Candidate cand in candidates)
                    if (BetterPayback(cand, best)) best = cand;
                if (best == null) return;

                Candidate pick = best;
                if (st.Cash < best.Cost)
                {
                    if ((best.Cost - st.Cash) / income <= WaitForMachineSeconds) return;
                    pick = null;
                    foreach (Candidate cand in candidates)
                    {
                        if (cand == best || cand.Cost > st.Cash || cand.Payback > best.Payback * 2m) continue;
                        if (Cheaper(cand, pick)) pick = cand;
                    }
                    if (pick == null) return;
                }
                budget--;
                if (!c.UpgradeMachine(pick.InstanceId)) return;
            }
        }

        /// <summary>
        /// Section 2.3 step 4 and 5: the Miner first while the run has none; otherwise, among the in-scope types that are unlocked now and below
        /// their target count, the largest deficit <c>target x (owned + 1) / S - ownedOfType</c> (ties: lower output price, then ordinal id).
        /// </summary>
        private MachineDef NextMixType(TraceContext c, RunState st)
        {
            RunCatalog catalog = c.Catalog;
            List<MachineDef> scope = PolicyMix.InScope(catalog, Scope);
            ManualMiningDef mining = catalog.RunStart.ManualMining;
            string ore = mining == null ? null : mining.ResourceId;
            if (ore != null && !OwnsOreProducer(catalog, st, ore))
            {
                foreach (MachineDef def in scope)
                    foreach (ResourceAmount output in catalog.Recipes[def.RecipeId].Outputs)
                        if (output.ResourceId == ore && st.IsUnlocked(def.Id)) return def;
            }

            int slots = st.SlotCount;
            Dictionary<string, int> targets = PolicyMix.Targets(catalog, Scope, slots);
            MachineDef best = null;
            decimal bestDeficit = 0m;
            long bestPrice = 0;
            foreach (MachineDef def in scope)
            {
                if (!targets.TryGetValue(def.Id, out int target) || !st.IsUnlocked(def.Id)) continue;
                int owned = 0;
                foreach (MachineInstance m in st.Machines)
                    if (m.MachineId == def.Id) owned++;
                if (owned >= target) continue;
                decimal deficit = (decimal)target * (st.Machines.Count + 1) / slots - owned;
                long price = PolicyMix.OutputPrice(catalog, def);
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

        private static bool OwnsOreProducer(RunCatalog catalog, RunState st, string ore)
        {
            foreach (MachineInstance m in st.Machines)
                foreach (ResourceAmount output in catalog.Recipes[catalog.Machines[m.MachineId].RecipeId].Outputs)
                    if (output.ResourceId == ore) return true;
            return false;
        }

        private static int FirstFreeSlot(RunState st)
        {
            for (int i = 0; i < st.SlotCount; i++)
                if (st.MachineAtSlot(i) == null) return i;
            return -1;
        }
    }

    /// <summary>
    /// The player's mental model of the factory (decimal arithmetic, so the policy decides the same on every host): continuous
    /// flows at the table speeds, upstream machines served first, everything left over sold at the base price. It ignores
    /// Items, Research, Overdrive and storage limits, as a player estimating from the displayed rates would. It ranks upgrade payback (section 4.4)
    /// and, with the Industry modifiers folded into the speeds and prices, is the model of <see cref="IndustryModel"/>.
    /// </summary>
    internal static class FluidModel
    {
        public struct Machine
        {
            public string TypeId;
            public long SpeedPpm;
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<RunCatalog, List<MachineDef>> OrderCache = new Dictionary<RunCatalog, List<MachineDef>>();

        private static List<MachineDef> Order(RunCatalog catalog)
        {
            lock (Gate)
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

        public static decimal Income(RunCatalog catalog, List<Machine> machines) => Income(catalog, machines, null);

        /// <summary>Income per second; <paramref name="priceFactor"/> (resource id to the effective sale-price multiplier) is null for the base prices.</summary>
        public static decimal Income(RunCatalog catalog, List<Machine> machines, Func<string, decimal> priceFactor)
        {
            Flow(catalog, machines, out Dictionary<string, decimal> remaining, out _);
            decimal income = 0m;
            foreach (KeyValuePair<string, decimal> pair in remaining)
                income += pair.Value * catalog.Resources[pair.Key].BasePrice * (priceFactor == null ? 1m : priceFactor(pair.Key));
            return income;
        }

        /// <summary>
        /// The resources the player keeps for the built machines (auto-sell off): every input of a built machine. A kept resource must pile up
        /// as input; everything else is sold.
        /// </summary>
        public static HashSet<string> Kept(RunCatalog catalog, IEnumerable<string> builtTypeIds)
        {
            var kept = new HashSet<string>(StringComparer.Ordinal);
            foreach (string typeId in builtTypeIds)
                foreach (ResourceAmount input in catalog.Recipes[catalog.Machines[typeId].RecipeId].Inputs)
                    kept.Add(input.ResourceId);
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
