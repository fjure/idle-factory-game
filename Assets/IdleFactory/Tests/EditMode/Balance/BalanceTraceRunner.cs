using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Tests.EditMode.Balance
{
    /// <summary>An absence of the player at a wall-clock second of the run, handled as an offline return (FR-025).</summary>
    internal readonly struct Absence
    {
        public long StartSec { get; }
        public long DurationSec { get; }

        public Absence(long startSec, long durationSec)
        {
            StartSec = startSec;
            DurationSec = durationSec;
        }
    }

    /// <summary>One completed contract: what it paid against the base and the effective market value of its required stock (balance-policy-r2.md section 8.2).</summary>
    internal sealed class DeliveryRecord
    {
        public string OfferId;
        public bool Bonus;
        public bool Production;
        public long RunSecond;
        public long Required;
        public long BaseValue;
        public long EffectiveValue;
        public long Reward;
    }

    /// <summary>One run of a trace: its checkpoint rows, times and statistics.</summary>
    internal sealed class RunRecord
    {
        public string Block;
        public int RunNumber;
        public long StartUtcMs;
        public long FirstGearAtSec = -1;
        public long EligibleAtSec = -1;
        public long MilestoneAtSec = -1;
        public long PrestigedAtSec = -1;
        public long AbsentSec;
        public bool CapReached;
        public long IpAwarded;
        public long EligibleFactoryValue;
        public readonly List<string[]> Rows = new List<string[]>();

        /// <summary>Every contract completion of the run (manual, auto-delivered or produced) with its base and effective value (FR-024 ratio bounds).</summary>
        public readonly List<DeliveryRecord> Deliveries = new List<DeliveryRecord>();

        /// <summary>The structural purchases of the run in order ("second:what"), for diagnosis; not part of the CSV.</summary>
        public readonly List<string> Events = new List<string>();

        /// <summary>Factory Value at exact run seconds (the strategy checkpoints, T311); not part of the CSV rows.</summary>
        public readonly SortedDictionary<int, long> CheckpointFactoryValue = new SortedDictionary<int, long>();
        public readonly SortedDictionary<string, long> Stats = new SortedDictionary<string, long>(StringComparer.Ordinal);

        /// <summary>Wall seconds to eligibility minus the absences before it (the active play time).</summary>
        public long PlaySecToEligible => EligibleAtSec < 0 ? -1 : EligibleAtSec - AbsentSec;

        /// <summary>Wall seconds to the sensitivity milestone Factory Value minus the absences before it.</summary>
        public long PlaySecToMilestone => MilestoneAtSec < 0 ? -1 : MilestoneAtSec - AbsentSecAtMilestone;

        public long AbsentSecAtMilestone;

        public void Add(string key, long delta)
        {
            Stats.TryGetValue(key, out long value);
            Stats[key] = value + delta;
        }
    }

    /// <summary>
    /// The command surface the reference policy uses. Every call goes through the real <see cref="RunCommands"/>; the ones that
    /// move resources, Cash, RP, IP or Items are checked against the exact expected change, so an unintended change is
    /// recorded as a violation (SC-001).
    /// </summary>
    internal sealed class TraceContext
    {
        private readonly RunCommands _commands;
        private int _nextInstance = 1;
        private string _lastExpiredOfferId;
        private long _lastExpiredDeadline = long.MinValue;

        public TraceContext(RunCatalog catalog, RunSession session, RunCommands commands, IStrategyPolicy policy)
        {
            Catalog = catalog;
            Session = session;
            _commands = commands;
            Policy = policy;
            Modifiers = new RunModifiers(catalog);
        }

        public RunCatalog Catalog { get; }
        public RunSession Session { get; }
        public IStrategyPolicy Policy { get; }
        public RunModifiers Modifiers { get; }
        public RunRecord Record { get; set; }
        public long ContractCooldownUntilUtcMs { get; set; }
        public long Commands { get; private set; }

        /// <summary>
        /// Observed income in Cash per second (balance-policy-r2.md section 3): the mean of the net-worth growth per second of the last
        /// <see cref="ObservedIncomeLooks"/> look intervals that exist, floored at 1 so it can serve as a divisor. Net worth is the Factory
        /// Value total of the prestige preview, which already contains Cash, stock value and the cash paid for what stands, so a purchase
        /// or a Sell does not read as income or loss.
        /// </summary>
        public decimal ObservedIncome { get; private set; } = 1m;

        /// <summary>The number of look intervals the observed income averages over.</summary>
        public const int ObservedIncomeLooks = 4;

        private readonly List<decimal> _incomeSamples = new List<decimal>();
        private long _netWorthAfterLook;

        /// <summary>The net worth now (Factory Value total of the preview, [state]).</summary>
        public long NetWorth() => Preview().Value.FactoryValue.Total;

        /// <summary>Clears the income history at the start of a run and takes the first net-worth reference.</summary>
        public void ResetIncome()
        {
            _incomeSamples.Clear();
            ObservedIncome = 1m;
            _netWorthAfterLook = NetWorth();
        }

        /// <summary>Records the net-worth growth since the last look ended, over <paramref name="seconds"/>, also at an idle look.</summary>
        public void AddIncomeSample(long seconds)
        {
            long now = NetWorth();
            decimal sample = seconds <= 0 ? 0m : (now - _netWorthAfterLook) / (decimal)seconds;
            _incomeSamples.Add(sample);
            if (_incomeSamples.Count > ObservedIncomeLooks) _incomeSamples.RemoveAt(0);
            decimal sum = 0m;
            foreach (decimal v in _incomeSamples) sum += v;
            ObservedIncome = Math.Max(sum / _incomeSamples.Count, 1m);
        }

        /// <summary>Records a contract that the offline return completed, so that the next advance does not take its reward for a change of its own.</summary>
        public void AfterOffline() => CollectOutcome(out _);

        /// <summary>Takes the net-worth reference at the end of a look (after its commands) or after an offline return.</summary>
        public void MarkLookEnd() => _netWorthAfterLook = NetWorth();
        public readonly List<string> Violations = new List<string>();
        public readonly SortedDictionary<string, long> ChainEvents = new SortedDictionary<string, long>(StringComparer.Ordinal);

        public static Guid Id(int n) => new Guid(n, 0, 0, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 });

        public void BeginRun()
        {
            _lastExpiredOfferId = null;
            _lastExpiredDeadline = long.MinValue;
            ContractCooldownUntilUtcMs = 0;
            _outcomeSerialSeen = Session.State.Contracts.OutcomeSerial;
            CapacityBlocked = false;
        }

        /// <summary>Set by the contract rule of a look: the best otherwise-eligible offer was rejected only for <c>contract.capacity</c> (section 4.4 rule 1).</summary>
        public bool CapacityBlocked { get; set; }

        /// <summary>Set at the start of a look: a machine stood still on a full store when the player arrived (section 4.4 rule 1).</summary>
        public bool StorageBlockedOnArrival { get; set; }

        private long _outcomeSerialSeen;

        public List<FluidModel.Machine> MachineList()
        {
            var list = new List<FluidModel.Machine>();
            foreach (MachineInstance m in Session.State.Machines)
                list.Add(new FluidModel.Machine { TypeId = m.MachineId, SpeedPpm = Catalog.Machines[m.MachineId].SpeedTable[m.Level] });
            return list;
        }

        /// <summary>The "storage full" warning the player sees on a machine.</summary>
        public bool AnyMachineBlockedByStorage()
        {
            var views = new MachineStateView[Session.State.Machines.Count];
            int n = Session.CopyMachineStates(views);
            for (int i = 0; i < n; i++)
                if (views[i].Blocker == MachineBlocker.StorageFull) return true;
            return false;
        }

        public void ObserveContractEnd(long nowUtcMs, int cooldownSeconds)
        {
            ExpiredContract? expired = Session.State.Contracts.LastExpired;
            if (!expired.HasValue) return;
            if (expired.Value.OfferId == _lastExpiredOfferId && expired.Value.DeadlineUtcMs == _lastExpiredDeadline) return;
            _lastExpiredOfferId = expired.Value.OfferId;
            _lastExpiredDeadline = expired.Value.DeadlineUtcMs;
            Record.Add("contracts_expired", 1);
            ContractCooldownUntilUtcMs = nowUtcMs + cooldownSeconds * 1000L;
        }

        // ---- snapshots ------------------------------------------------------------------------------

        private sealed class Snap
        {
            public long Cash;
            public long Rp;
            public long Ip;
            public long[] Stock;
            public Dictionary<string, long> Items;
        }

        private Snap Take()
        {
            RunState st = Session.State;
            var snap = new Snap { Cash = st.Cash, Rp = st.Rp, Stock = new long[st.Inventory.Count], Items = new Dictionary<string, long>(StringComparer.Ordinal) };
            for (int i = 0; i < st.Inventory.Count; i++) snap.Stock[i] = st.Inventory.UnitsAt(i);
            if (Session.Permanent != null)
            {
                snap.Ip = Session.Permanent.Ip;
                foreach (IdCount stack in Session.Permanent.ItemStacks()) snap.Items[stack.Id] = stack.Count;
            }
            return snap;
        }

        private void Verify(string what, Snap before, long dCash = 0, long dRp = 0, long dIp = 0, string resource = null, long dStock = 0,
            IReadOnlyList<IdCount> itemDeltas = null, string itemId = null, long dItem = 0)
        {
            Snap after = Take();
            RunState st = Session.State;
            // A contract can end in the settle of a command (SetAutoDeliver delivers a stocked contract at once, AcceptContract can complete a
            // production objective at once): its reward is an expected change of this command, not a violation (SC-001).
            ContractOutcome? outcome = CollectOutcome(out long outcomeCash);
            if (outcome.HasValue)
            {
                dRp += outcome.Value.RpReward;
                var granted = new List<IdCount>();
                foreach (KeyValuePair<string, long> pair in after.Items)
                {
                    before.Items.TryGetValue(pair.Key, out long had);
                    if (pair.Value > had) granted.Add(new IdCount(pair.Key, pair.Value - had));
                }
                long grantedCount = 0;
                foreach (IdCount g in granted) grantedCount += g.Count;
                if (grantedCount != outcome.Value.ItemCount) Violations.Add($"{what}: contract ended with {outcome.Value.ItemCount} Items but {grantedCount} appeared");
                RecordItemTiers(granted);
                if (itemDeltas != null) granted.AddRange(itemDeltas);
                itemDeltas = granted;
            }
            dCash += outcomeCash;
            // Every command ends with a settle: a machine that waited for room or input may complete (production) and an
            // auto-sell resource is sold. Cash and stock may therefore grow beyond the command's own change, never fall short.
            if (after.Cash < before.Cash + dCash) Violations.Add($"{what}: cash {before.Cash} -> {after.Cash}, expected at least {dCash:+0;-0;0}");
            else if (after.Cash > before.Cash + dCash)
            {
                Record.Add("settle_cash_gain", after.Cash - before.Cash - dCash);
                Record.Add("sales_cash", after.Cash - before.Cash - dCash);
            }
            if (after.Rp != before.Rp + dRp) Violations.Add($"{what}: rp {before.Rp} -> {after.Rp}, expected {dRp:+0;-0;0}");
            if (after.Ip != before.Ip + dIp) Violations.Add($"{what}: ip {before.Ip} -> {after.Ip}, expected {dIp:+0;-0;0}");
            if (resource != null && after.Stock.Length == before.Stock.Length)
            {
                for (int i = 0; i < after.Stock.Length; i++)
                {
                    if (st.Inventory.IdAt(i) != resource) continue;
                    if (after.Stock[i] < before.Stock[i] + dStock) Violations.Add($"{what}: stock {resource} {before.Stock[i]} -> {after.Stock[i]}, expected at least {dStock:+0;-0;0}");
                }
            }
            var expectedItems = new Dictionary<string, long>(before.Items, StringComparer.Ordinal);
            if (itemDeltas != null)
                foreach (IdCount grant in itemDeltas)
                {
                    expectedItems.TryGetValue(grant.Id, out long have);
                    expectedItems[grant.Id] = have + grant.Count;
                }
            if (itemId != null)
            {
                expectedItems.TryGetValue(itemId, out long have);
                expectedItems[itemId] = have + dItem;
            }
            foreach (KeyValuePair<string, long> pair in expectedItems)
            {
                after.Items.TryGetValue(pair.Key, out long actual);
                if (actual != pair.Value) Violations.Add($"{what}: item {pair.Key} {actual}, expected {pair.Value}");
            }
            foreach (KeyValuePair<string, long> pair in after.Items)
                if (!expectedItems.ContainsKey(pair.Key) && pair.Value != 0) Violations.Add($"{what}: unexpected item {pair.Key} {pair.Value}");
        }

        private void Reject(string command, string reason) => Record.Add("rejected." + command + "." + reason, 1);

        // ---- commands -------------------------------------------------------------------------------

        public bool Build(int slot, string machineId)
        {
            Commands++;
            Snap before = Take();
            RunCommandResult<BuildResult> r = _commands.Build(slot, machineId);
            if (!r.IsSuccess) { Reject("build", r.ReasonCode); return false; }
            Verify("build " + machineId, before, dCash: -r.Value.CostPaid);
            Record.Add("builds", 1);
            Record.Events.Add(ElapsedSeconds() + ":build " + machineId.Substring(machineId.IndexOf('.') + 1) + "@" + slot);
            return true;
        }

        public bool UpgradeMachine(Guid instanceId)
        {
            Commands++;
            Snap before = Take();
            RunCommandResult<UpgradeResult> r = _commands.Upgrade(instanceId);
            if (!r.IsSuccess) { Reject("upgrade", r.ReasonCode); return false; }
            Verify("upgrade", before, dCash: -r.Value.CostPaid);
            Record.Add("upgrades", 1);
            return true;
        }

        public bool UpgradeFactory()
        {
            Commands++;
            Snap before = Take();
            RunCommandResult<ExpansionResult> r = _commands.UpgradeFactory();
            if (!r.IsSuccess) { Reject("factory", r.ReasonCode); return false; }
            Verify("factory", before, dCash: -r.Value.CostPaid);
            Record.Add("factory_upgrades", 1);
            Record.Events.Add(ElapsedSeconds() + ":factory");
            return true;
        }

        public bool UpgradeStorage()
        {
            Commands++;
            Snap before = Take();
            RunCommandResult<ExpansionResult> r = _commands.UpgradeStorage();
            if (!r.IsSuccess) { Reject("storage", r.ReasonCode); return false; }
            Verify("storage", before, dCash: -r.Value.CostPaid);
            Record.Add("storage_upgrades", 1);
            Record.Events.Add(ElapsedSeconds() + ":storage");
            return true;
        }

        public bool SetAutoSell(string resourceId, bool enabled)
        {
            Commands++;
            Snap before = Take();
            RunCommandResult<bool> r = _commands.SetAutoSell(resourceId, enabled);
            if (!r.IsSuccess) { Reject("autosell", r.ReasonCode); return false; }
            // Enabling auto-sell may sell stock at once; the sale itself is a legitimate Cash/stock exchange (counted as sales Cash).
            Verify(enabled ? "autosell on" : "autosell off", before);
            return true;
        }

        public bool SetActive(Guid instanceId, bool active)
        {
            Commands++;
            Snap before = Take();
            RunCommandResult<bool> r = _commands.SetActive(instanceId, active);
            if (!r.IsSuccess) { Reject("setactive", r.ReasonCode); return false; }
            Verify("setactive", before);
            return true;
        }

        public bool AcceptContract(string offerId)
        {
            Commands++;
            Snap before = Take();
            RunCommandResult<ContractAcceptResult> r = _commands.AcceptContract(offerId);
            if (!r.IsSuccess) { Reject("accept", r.ReasonCode); return false; }
            Verify("accept contract", before);
            Record.Add("contracts_accepted", 1);
            return true;
        }

        public bool AbandonContract()
        {
            Commands++;
            Snap before = Take();
            RunCommandResult<ContractAbandonResult> r = _commands.AbandonContract();
            if (!r.IsSuccess) { Reject("abandon", r.ReasonCode); return false; }
            Verify("abandon contract", before);
            Record.Add("contracts_abandoned", 1);
            ContractCooldownUntilUtcMs = Session.TimelineUtcMs + Policy.ContractCooldownSeconds * 1000L;
            return true;
        }

        public bool DeliverContract()
        {
            Commands++;
            Snap before = Take();
            RunCommandResult<ContractDeliveryResult> r = _commands.DeliverContract();
            if (!r.IsSuccess) { Reject("deliver", r.ReasonCode); return false; }
            ContractDeliveryResult d = r.Value;
            ContractOfferDef offer = Catalog.ContractOffers[d.OfferId];
            Verify("deliver " + d.OfferId, before, dCash: d.CashReward, dRp: d.RpReward, resource: offer.ResourceId, dStock: -d.UnitsConsumed, itemDeltas: d.ItemsGranted);
            RecordCompletion(offer, d.UnitsConsumed, d.CashReward, d.RpReward, manual: true);
            RecordItemTiers(d.ItemsGranted);
            ContractCooldownUntilUtcMs = Session.TimelineUtcMs + Policy.ContractCooldownSeconds * 1000L;
            return true;
        }

        /// <summary>
        /// Records one completed contract (manual delivery, auto-delivery or a production objective): the sale value of the required stock at
        /// the sale-price modifier of this instant (T320: the reward is a ratio of it, FR-009), against the base value, for the ratio-bound gate.
        /// </summary>
        private void RecordCompletion(ContractOfferDef offer, long units, long cashReward, long rpReward, bool manual)
        {
            long basePrice = Catalog.Resources[offer.ResourceId].BasePrice;
            long baseValue = units * basePrice;
            long market = baseValue;
            if (Modifiers.TrySalePricePpm(Session.State, Session.Permanent, offer.ResourceId, out long salePricePpm))
                MulDiv.MulDivFloor(baseValue, salePricePpm, 1_000_000, out market);
            Record.Deliveries.Add(new DeliveryRecord
            {
                OfferId = offer.Id,
                Bonus = offer.IsBonus,
                Production = offer.IsProduction,
                RunSecond = ElapsedSeconds(),
                Required = units,
                BaseValue = baseValue,
                EffectiveValue = market,
                Reward = cashReward,
            });
            Record.Add("contracts_completed", 1);
            Record.Add("contract_cash_reward", cashReward);
            Record.Add("rp_earned", rpReward);
            if (offer.IsProduction)
            {
                Record.Add("contracts_produced", 1);
                Record.Add("produced." + offer.Id, 1);
            }
            else
            {
                Record.Add("contracts_delivered", 1);
                Record.Add("delivered." + offer.Id, 1);
                Record.Add(manual ? "deliveries_manual" : "deliveries_auto", 1);
                Record.Add("contract_market_value", market);
                Record.Add("contract_ratio_ppm_sum", market == 0 ? 0 : cashReward * 1_000_000L / market);
                Record.Add("rp_per_completion_sum", rpReward);
            }
            if (!ChainEvents.ContainsKey("contract")) ChainEvents["contract"] = ElapsedSeconds();
            if (offer.Kind == "gear" && !offer.IsProduction && !ChainEvents.ContainsKey("gearContract")) ChainEvents["gearContract"] = ElapsedSeconds();
        }

        private void RecordItemTiers(IEnumerable<IdCount> granted)
        {
            bool any = false;
            foreach (IdCount grant in granted)
            {
                Record.Add("items_granted.tier" + Catalog.Items[grant.Id].Tier, grant.Count);
                any = true;
            }
            if (any && !ChainEvents.ContainsKey("item")) ChainEvents["item"] = ElapsedSeconds();
        }

        /// <summary>
        /// A contract that the simulator ended since the last call (auto-delivery or a production objective), recorded once. The board keeps only
        /// the last outcome and an acceptance clears it, so every command and every advance collects it right after it ran.
        /// </summary>
        private ContractOutcome? CollectOutcome(out long cashReward)
        {
            cashReward = 0;
            ContractBoard board = Session.State.Contracts;
            if (board.OutcomeSerial == _outcomeSerialSeen) return null;
            _outcomeSerialSeen = board.OutcomeSerial;
            ContractOutcome o = board.LastOutcome;
            if (o.Serial != board.OutcomeSerial) return null;
            // An expiry is counted by ObserveContractEnd (it reads LastExpired); only a completion pays.
            if (o.Kind != ContractOutcomeKind.AutoDelivered && o.Kind != ContractOutcomeKind.ProduceCompleted) return null;
            ContractOfferDef offer = Catalog.ContractOffers[o.OfferId];
            RecordCompletion(offer, o.RequiredUnits, o.CashReward, o.RpReward, manual: false);
            ContractCooldownUntilUtcMs = Session.TimelineUtcMs + Policy.ContractCooldownSeconds * 1000L;
            cashReward = o.CashReward;
            return o;
        }

        // ---- the simulator advance (the cheap guard: RP, IP and Item totals may only change through a contract outcome) ----

        private long _advCash;
        private long _advRp;
        private long _advIp;
        private long _advItems;
        private long _advItemTier1;
        private long _advItemTier2;
        private long _advItemTier3;

        private long ItemTotal()
        {
            long total = 0;
            if (Session.Permanent != null)
                foreach (IdCount stack in Session.Permanent.ItemStacks()) total += stack.Count;
            return total;
        }

        private long ItemTierTotal(int tier)
        {
            long total = 0;
            if (Session.Permanent != null)
                foreach (IdCount stack in Session.Permanent.ItemStacks())
                    if (Catalog.Items[stack.Id].Tier == tier) total += stack.Count;
            return total;
        }

        public void BeginAdvance()
        {
            RunState st = Session.State;
            _advCash = st.Cash;
            _advRp = st.Rp;
            _advIp = Session.Permanent == null ? 0 : Session.Permanent.Ip;
            _advItems = ItemTotal();
            _advItemTier1 = ItemTierTotal(1);
            _advItemTier2 = ItemTierTotal(2);
            _advItemTier3 = ItemTierTotal(3);
        }

        public void EndAdvance()
        {
            RunState st = Session.State;
            ContractOutcome? outcome = CollectOutcome(out long reward);
            long dRp = outcome.HasValue ? outcome.Value.RpReward : 0;
            long granted = outcome.HasValue ? outcome.Value.ItemCount : 0;
            if (st.Rp != _advRp + dRp) Violations.Add($"advance: rp {_advRp} -> {st.Rp}, expected {dRp:+0;-0;0}");
            if (Session.Permanent != null && Session.Permanent.Ip != _advIp) Violations.Add($"advance: ip {_advIp} -> {Session.Permanent.Ip}");
            long items = ItemTotal();
            if (items != _advItems + granted) Violations.Add($"advance: items {_advItems} -> {items}, expected {granted:+0;-0;0}");
            if (st.Cash < _advCash + reward) Violations.Add($"advance: cash {_advCash} -> {st.Cash}, below the reward {reward}");
            else if (st.Cash > _advCash + reward) Record.Add("sales_cash", st.Cash - _advCash - reward);
            if (granted > 0)
            {
                Record.Add("items_granted.tier1", ItemTierTotal(1) - _advItemTier1);
                Record.Add("items_granted.tier2", ItemTierTotal(2) - _advItemTier2);
                Record.Add("items_granted.tier3", ItemTierTotal(3) - _advItemTier3);
                if (!ChainEvents.ContainsKey("item")) ChainEvents["item"] = ElapsedSeconds();
            }
        }

        // ---- F005 verbs: Sell, Mine, SetAutoDeliver --------------------------------------------------------

        /// <summary>
        /// Sells <paramref name="quantity"/> units (T317). Checked against the exact result (FR-002): the Cash earned, the stock removed and a Factory
        /// Value that no sale may raise; the total change is recorded so that the sale-value-neutrality gate can assert it.
        /// </summary>
        public bool Sell(string resourceId, long quantity)
        {
            Commands++;
            Snap before = Take();
            long fvBefore = NetWorth();
            // A machine that waits for room completes in the settle of the command and adds stock; the neutrality check is only exact without one.
            bool settleCannotProduce = !AnyMachineWaiting();
            RunCommandResult<SellResult> r = _commands.Sell(resourceId, quantity);
            if (!r.IsSuccess) { Reject("sell", r.ReasonCode); return false; }
            SellResult sold = r.Value;
            Verify("sell " + resourceId, before, dCash: sold.CashEarned, resource: resourceId, dStock: -sold.Units);
            Record.Add("manual_sells", 1);
            Record.Add("manual_sell_units", sold.Units);
            Record.Add("sales_cash", sold.CashEarned);
            if (!settleCannotProduce) { Record.Add("sell_fv_unchecked", 1); return true; }
            long delta = NetWorth() - fvBefore;
            Record.Add("sell_fv_checked", 1);
            if (delta != 0) Record.Add("sell_fv_loss_" + (-delta), 1);
            // FR-002: no sale raises Factory Value, and the single floor loses at most one unit; with an identity price modifier it loses nothing.
            if (delta > 0 || delta < -1) Violations.Add($"sell {resourceId}: Factory Value changed by {delta}");
            else if (delta != 0 && Modifiers.TrySalePricePpm(Session.State, Session.Permanent, resourceId, out long ppm) && ppm == 1_000_000)
                Violations.Add($"sell {resourceId}: Factory Value changed by {delta} at an identity sale price");
            return true;
        }

        private bool AnyMachineWaiting()
        {
            var views = new MachineStateView[Session.State.Machines.Count];
            int n = Session.CopyMachineStates(views);
            for (int i = 0; i < n; i++)
                if (views[i].Phase == MachinePhase.Waiting) return true;
            return false;
        }

        /// <summary>
        /// ECO-7 probe (FR-015): removes an owned machine and builds the same type in its slot. Returns the Factory Value total (Cash included)
        /// before and after; the pair may never rise. The policy itself never removes a machine.
        /// </summary>
        public bool RemoveAndRebuild(Guid instanceId, out long totalBefore, out long totalAfter)
        {
            totalBefore = totalAfter = NetWorth();
            MachineInstance machine = Session.State.FindMachine(instanceId);
            if (machine == null) return false;
            string type = machine.MachineId;
            int slot = machine.SlotIndex;
            if (!_commands.Remove(instanceId, true).IsSuccess) return false;
            if (!_commands.Build(slot, type).IsSuccess) return false;
            totalAfter = NetWorth();
            return true;
        }

        /// <summary>
        /// One held-click activation batch. Rejections (full store, locked Ore) are counted, never fatal. It is called between
        /// <see cref="BeginAdvance"/> and <see cref="EndAdvance"/>, which check RP, IP, Items and Cash around it, so it takes no snapshot of its own.
        /// </summary>
        public bool Mine(long activations)
        {
            RunCommandResult<MineResult> r = _commands.Mine(activations);
            if (!r.IsSuccess) { Reject("mine", r.ReasonCode); return false; }
            MineResult mined = r.Value;
            Record.Add("mine_calls", 1);
            Record.Add("mine_counted", mined.Counted);
            Record.Add("mine_ore_units", mined.Units);
            return true;
        }

        public bool SetAutoDeliver(bool enabled)
        {
            Commands++;
            Snap before = Take();
            RunCommandResult<bool> r = _commands.SetAutoDeliver(enabled);
            if (!r.IsSuccess) { Reject("autodeliver", r.ReasonCode); return false; }
            Verify("autodeliver", before);
            Record.Add("auto_deliver_on", enabled ? 1 : 0);
            return true;
        }

        public bool ItemConflicts(ItemDef item, string target)
        {
            string key = item.TargetKind == "factory" ? RunCommands.FactoryTargetKey : target;
            long now = Session.TimelineUtcMs;
            foreach (ActiveEffect e in Session.State.Effects.Effects)
                if (e.ConflictKey.EffectKind == item.Effect.Kind && e.ConflictKey.TargetKey == key && e.EndUtcMs > now) return true;
            return false;
        }

        public bool ActivateItem(string itemId, string target)
        {
            Commands++;
            Snap before = Take();
            RunCommandResult<ItemActivationResult> r = _commands.ActivateItem(itemId, target);
            if (!r.IsSuccess) { Reject("item", r.ReasonCode); return false; }
            Verify("activate " + itemId, before, itemId: itemId, dItem: -1);
            Record.Add("items_activated", 1);
            return true;
        }

        public bool ActivateOverdrive()
        {
            Commands++;
            Snap before = Take();
            RunCommandResult<OverdriveActivationResult> r = _commands.ActivateOverdrive();
            if (!r.IsSuccess) { Reject("overdrive", r.ReasonCode); return false; }
            Verify("overdrive", before);
            Record.Add("overdrive_activations", 1);
            return true;
        }

        public bool PurchaseResearch(string researchId)
        {
            Commands++;
            Snap before = Take();
            RunCommandResult<ResearchPurchaseResult> r = _commands.PurchaseResearch(researchId);
            if (!r.IsSuccess) { Reject("research", r.ReasonCode); return false; }
            Verify("research " + researchId, before, dRp: -r.Value.RpPaid);
            Record.Add("research_purchases", 1);
            if (!ChainEvents.ContainsKey("research")) ChainEvents["research"] = ElapsedSeconds();
            return true;
        }

        public bool PurchaseIndustry(string upgradeId)
        {
            Commands++;
            Snap before = Take();
            RunCommandResult<IndustryPurchaseResult> r = _commands.PurchaseIndustry(upgradeId);
            if (!r.IsSuccess) { Reject("industry", r.ReasonCode); return false; }
            Verify("industry " + upgradeId, before, dIp: -r.Value.IpPaid);
            Record.Add("industry_purchases", 1);
            Record.Add("industry_bought." + upgradeId, 1);
            Record.Add("industry_ip_spent", r.Value.IpPaid);
            if (!ChainEvents.ContainsKey("industry")) ChainEvents["industry"] = ElapsedSeconds();
            return true;
        }

        public RunCommandResult<PrestigePreview> Preview() => _commands.GetPrestigePreview();

        /// <summary>Confirms and promotes the prestige, verifying exactly what resets and what is preserved.</summary>
        public bool Prestige(PrestigePreview preview)
        {
            Commands++;
            RunState oldState = Session.State;
            Snap before = Take();
            long ipBefore = Session.Permanent.Ip;
            long prestigesBefore = Session.Permanent.PrestigeCount;
            int runNumber = oldState.RunNumber;
            RunCommandResult<PrestigeConfirmation> confirmed = _commands.ConfirmPrestige();
            if (!confirmed.IsSuccess) { Reject("prestige", confirmed.ReasonCode); return false; }
            RunCommandResult<Guid> promoted = _commands.PromotePrestige();
            if (!promoted.IsSuccess) { Reject("promote", promoted.ReasonCode); return false; }

            RunState fresh = Session.State;
            long award = confirmed.Value.IpAwarded;
            if (award != preview.IpAward) Violations.Add($"prestige: award {award}, preview {preview.IpAward}");
            if (fresh.RunNumber != runNumber + 1) Violations.Add("prestige: run number");
            if (fresh.PreviousRunId != oldState.RunId) Violations.Add("prestige: previous run id");
            if (fresh.Machines.Count != 0) Violations.Add("prestige: machines survived");
            if (fresh.Rp != 0) Violations.Add("prestige: RP survived");
            if (fresh.Contracts.Active != null) Violations.Add("prestige: contract survived");
            if (fresh.Effects.Count != 0) Violations.Add("prestige: effects survived");
            if (fresh.FactoryLevel != 1) Violations.Add("prestige: factory level not reset");
            NextRunStart start = NextRunStart.Compute(Catalog, Session.Permanent);
            if (fresh.Cash != start.StartingCash) Violations.Add($"prestige: start cash {fresh.Cash}, expected {start.StartingCash}");
            if (fresh.StorageLevel != start.StorageLevel) Violations.Add("prestige: start storage level");
            for (int i = 0; i < fresh.Inventory.Count; i++)
                if (fresh.Inventory.UnitsAt(i) != 0) Violations.Add("prestige: stock survived " + fresh.Inventory.IdAt(i));
            foreach (string id in Catalog.Research.Keys)
                if (fresh.ResearchLevel(id) != 0) Violations.Add("prestige: research survived " + id);
            if (Session.Permanent.Ip != ipBefore + award) Violations.Add($"prestige: ip {Session.Permanent.Ip}, expected {ipBefore + award}");
            if (Session.Permanent.PrestigeCount != prestigesBefore + 1) Violations.Add("prestige: count");
            foreach (KeyValuePair<string, long> pair in before.Items)
                if (Session.Permanent.ItemCount(pair.Key) != pair.Value) Violations.Add("prestige: item " + pair.Key + " changed");
            foreach (ItemDef item in Catalog.Items.Values)
                if (!before.Items.ContainsKey(item.Id) && Session.Permanent.ItemCount(item.Id) != 0) Violations.Add("prestige: new item " + item.Id);
            if (!ChainEvents.ContainsKey("prestige")) ChainEvents["prestige"] = ElapsedSeconds();
            return true;
        }

        public long RunStartUtcMs { get; set; }

        public long ElapsedSeconds() => (Session.TimelineUtcMs - RunStartUtcMs) / 1000;
    }

    /// <summary>
    /// The deterministic balance trace (R13, contracts/run-application.md, Balance trace interface):
    /// <c>BalanceTraceRunner(catalog, ReferencePolicy, seedState)</c> issues the real application commands against a manual
    /// clock (the run timeline advances only by <see cref="RunSession.Advance"/> and the offline return). Time is in simulated
    /// seconds since the run started; the policy looks at the factory every <see cref="ReferencePolicy.EpochSeconds"/> seconds.
    /// </summary>
    internal sealed class BalanceTraceRunner
    {
        public const long StartUtcMs = 1_700_000_000_000L;
        public const long TicksPerSecond = 10_000_000L;
        public static readonly int[] CheckpointSeconds = { 600, 900, 1_800, 3_600, 5_400, 7_200 };

        /// <summary>A run that is still not eligible after this many seconds is reported as not reaching the threshold.</summary>
        public const long CapSeconds = 21_600;

        private readonly RunCatalog _catalog;
        private readonly IStrategyPolicy _policy;
        private readonly RunSession _session;
        private readonly TraceContext _ctx;
        private readonly OfflineSegmenter _segmenter;
        private int _runIds;

        public BalanceTraceRunner(RunCatalog catalog, IStrategyPolicy policy, PermanentState seedState = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            PermanentState permanent = seedState ?? new PermanentState(catalog);
            NextRunStart start = NextRunStart.Compute(catalog, permanent);
            var state = new RunState(catalog, TraceContext.Id(900_001), 1, null, start.StartingCash, 0, start.StorageLevel, 1, catalog.RunStart.InitialUnlocks);
            RunAdvanceResult settled = new RunSimulator(catalog).Advance(state, permanent, 0, StartUtcMs, null);
            if (settled != RunAdvanceResult.Ok) throw new InvalidOperationException("The fresh run did not settle: " + settled);
            _session = new RunSession(catalog, state, permanent, StartUtcMs);
            int instance = 1;
            var commands = new RunCommands(_session, () => TraceContext.Id(instance++), () => TraceContext.Id(910_000 + ++_runIds));
            _ctx = new TraceContext(catalog, _session, commands, policy);
            _segmenter = new OfflineSegmenter(catalog);
        }

        public TraceContext Context => _ctx;

        /// <summary>
        /// When true (the strategy ensemble, T311), the Factory Value is recorded at the exact checkpoint seconds in
        /// <see cref="RunRecord.CheckpointFactoryValue"/>: a look step that spans a checkpoint is advanced in two parts.
        /// Off by default, so the trace and its golden file are unchanged.
        /// </summary>
        public bool RecordCheckpoints { get; set; }

        /// <summary>
        /// When positive, every advance of the run is cut into pieces of at most this many seconds (the auto-deliver partition-invariance gate of
        /// FR-024, SC-003): the commands stay where they are, so the state must come out identical.
        /// </summary>
        public int AdvancePartitionSeconds { get; set; }

        /// <summary>
        /// Plays run <paramref name="runNumber"/> from the session's current fresh state. <paramref name="prestigeAtEligibility"/>
        /// ends the run with the prestige at the first look at or after eligibility that is not inside an idle window (for the prestige-later
        /// strategies: at the first such look at which Factory Value reaches the policy's multiple of the threshold, or at the run limit);
        /// otherwise the run is not ended by the policy and is played until <paramref name="minEndSec"/> and eligibility.
        /// The held mining click of policy r2 (balance-policy-r2.md section 6) is the one command at a non-look instant: the run advances in
        /// whole seconds while the policy holds the click, and in one step otherwise.
        /// </summary>
        public RunRecord PlayRun(string block, int runNumber, bool prestigeAtEligibility, long minEndSec, IReadOnlyList<Absence> absences = null,
            int offsetSec = 0, long stopAfterSec = -1)
        {
            int look = _policy.LookIntervalSeconds;
            if (look <= 0 || offsetSec < 0) throw new ArgumentOutOfRangeException(nameof(offsetSec));
            var rec = new RunRecord { Block = block, RunNumber = runNumber, StartUtcMs = _session.TimelineUtcMs };
            _ctx.Record = rec;
            _ctx.RunStartUtcMs = rec.StartUtcMs;
            _ctx.BeginRun();
            _policy.BeginRun(_ctx, offsetSec);
            _ctx.ResetIncome();
            RecordStart(rec);
            long t = 0;
            // The look grid: looks happen at run seconds offsetSec + k * look (T311). Offset 0 is the reference grid.
            if (offsetSec == 0) _policy.Act(_ctx);
            _ctx.MarkLookEnd();

            while (true)
            {
                Absence? away = null;
                if (absences != null)
                    foreach (Absence a in absences)
                        if (a.StartSec == t) away = a;

                if (away.HasValue)
                {
                    GoAway(away.Value.DurationSec);
                    t += away.Value.DurationSec;
                    rec.AbsentSec += away.Value.DurationSec;
                    rec.Add("absences", 1);
                }
                else
                {
                    int step = t == 0 && offsetSec > 0 ? offsetSec : look;
                    AdvanceStep(rec, t, step);
                    t += step;
                    _ctx.AddIncomeSample(step);
                    Sample(rec, t, step);
                    DetectFirstGear(rec, t);
                    TryCheckpoint(rec, t);
                }

                PrestigePreview preview = _ctx.Preview().Value;
                if (RecordCheckpoints && Array.IndexOf(CheckpointSeconds, (int)t) >= 0 && !rec.CheckpointFactoryValue.ContainsKey((int)t))
                    rec.CheckpointFactoryValue[(int)t] = preview.FactoryValue.Total;
                if (rec.MilestoneAtSec < 0 && preview.FactoryValue.Total >= BalanceBands.SensitivityMilestoneFv)
                {
                    rec.MilestoneAtSec = t;
                    rec.AbsentSecAtMilestone = rec.AbsentSec;
                }
                if (rec.EligibleAtSec < 0 && preview.IsEligible)
                {
                    rec.EligibleAtSec = t;
                    rec.EligibleFactoryValue = preview.FactoryValue.Total;
                    rec.IpAwarded = preview.IpAward;
                    AddRow(rec, t, preview, "E");
                }

                if (rec.EligibleAtSec >= 0 && prestigeAtEligibility && !_policy.IsIdleAt(t) && PrestigeDue(preview))
                {
                    if (!_ctx.Prestige(preview)) throw new InvalidOperationException("The prestige was rejected.");
                    rec.PrestigedAtSec = t;
                    rec.IpAwarded = preview.IpAward;
                    return Finish(rec);
                }
                if (!prestigeAtEligibility && rec.EligibleAtSec >= 0 && t >= minEndSec) return Finish(rec);
                if (!prestigeAtEligibility && minEndSec < 0 && rec.MilestoneAtSec >= 0) return Finish(rec);
                if (t - rec.AbsentSec >= CapSeconds)
                {
                    if (prestigeAtEligibility && preview.IsEligible)
                    {
                        // The run limit ends a delayed prestige (balance-policy-r2.md section 7, prestige later).
                        if (!_ctx.Prestige(preview)) throw new InvalidOperationException("The prestige at the run limit was rejected.");
                        rec.PrestigedAtSec = t;
                        rec.IpAwarded = preview.IpAward;
                        return Finish(rec);
                    }
                    rec.CapReached = rec.EligibleAtSec < 0;
                    return Finish(rec);
                }

                if (stopAfterSec >= 0 && t >= stopAfterSec) return Finish(rec);

                if (!_policy.IsIdleAt(t)) _policy.Act(_ctx);
                _ctx.MarkLookEnd();
            }
        }

        /// <summary>The start Cash and the price of the first Miner (start affordability, FR-016 and SC-011).</summary>
        private void RecordStart(RunRecord rec)
        {
            RunState st = _session.State;
            rec.Add("start_cash", st.Cash);
            ManualMiningDef mining = _catalog.RunStart.ManualMining;
            if (mining == null) return;
            foreach (MachineDef def in _catalog.Machines.Values)
            {
                foreach (ResourceAmount output in _catalog.Recipes[def.RecipeId].Outputs)
                {
                    if (output.ResourceId != mining.ResourceId) continue;
                    if (BuildPricing.TryPrice(_catalog, _ctx.Modifiers, st, _session.Permanent, def, out long price))
                    {
                        rec.Add("first_miner_price", price);
                        return;
                    }
                }
            }
        }

        /// <summary>True when the policy prestiges at this preview: the threshold is reached (the preview is eligible) and the policy's multiple of it too.</summary>
        private bool PrestigeDue(PrestigePreview preview)
        {
            if (!preview.IsEligible) return false;
            long factor = _policy.PrestigeFactorPpm;
            return factor <= 1_000_000 || preview.FactoryValue.Total * 1_000_000L >= factor * preview.Threshold;
        }

        /// <summary>
        /// One look interval from run second <paramref name="t"/>. While the policy holds the mining click (an active second, after the first
        /// look) the run advances one second at a time with one <c>Mine</c> before each advance; otherwise in one piece up to the next second at
        /// which the click may resume.
        /// </summary>
        private void AdvanceStep(RunRecord rec, long t, int step)
        {
            long end = t + step;
            long s = t;
            while (s < end)
            {
                long activations = _policy.HeldClickActivations(_ctx, s);
                if (activations > 0)
                {
                    _ctx.BeginAdvance();
                    _ctx.Mine(activations);
                    AdvanceSegment(rec, s, 1, end, true);
                    s += 1;
                }
                else
                {
                    long stop = Math.Min(end, _policy.HeldClickResumeSecond(s));
                    if (stop <= s) stop = Math.Min(end, s + 1);
                    AdvanceSegment(rec, s, stop - s, end, false);
                    s = stop;
                }
            }
        }

        /// <summary>Advances <paramref name="len"/> seconds from run second <paramref name="s"/>, splitting at the Factory Value checkpoints that lie inside the look step.</summary>
        private void AdvanceSegment(RunRecord rec, long s, long len, long stepEnd, bool begun)
        {
            long done = 0;
            if (RecordCheckpoints)
            {
                foreach (int checkpoint in CheckpointSeconds)
                {
                    if (checkpoint <= s + done || checkpoint > s + len || checkpoint == stepEnd) continue;
                    AdvanceSeconds(rec, checkpoint - s - done, ref begun);
                    done = checkpoint - s;
                    rec.CheckpointFactoryValue[checkpoint] = _ctx.Preview().Value.FactoryValue.Total;
                }
            }
            if (len - done > 0) AdvanceSeconds(rec, len - done, ref begun);
        }

        private void AdvanceSeconds(RunRecord rec, long seconds, ref bool begun)
        {
            if (!begun) _ctx.BeginAdvance();
            begun = false;
            long remaining = seconds;
            while (remaining > 0)
            {
                long piece = AdvancePartitionSeconds > 0 ? Math.Min(remaining, AdvancePartitionSeconds) : remaining;
                RunCommandResult<long> advanced = _session.Advance(piece * TicksPerSecond);
                if (!advanced.IsSuccess) throw new InvalidOperationException("Advance rejected: " + advanced.ReasonCode);
                remaining -= piece;
                _ctx.EndAdvance();
                if (remaining > 0) _ctx.BeginAdvance();
            }
            if (_policy.HeldOffMachineCount > 0) rec.Add("held_off_machine_seconds", _policy.HeldOffMachineCount * seconds);
        }

        public int SpendIndustry() => _policy.SpendIndustry(_ctx);

        private RunRecord Finish(RunRecord rec)
        {
            foreach (string[] row in rec.Rows)
            {
                row[row.Length - 2] = rec.FirstGearAtSec.ToString(CultureInfo.InvariantCulture);
                row[row.Length - 1] = rec.EligibleAtSec.ToString(CultureInfo.InvariantCulture);
            }
            rec.Add("commands", _ctx.Commands);
            return rec;
        }

        private void GoAway(long durationSec)
        {
            long departure = _session.TimelineUtcMs;
            long returnUtc = departure + durationSec * 1000L;
            RunOfflineSummary summary = _segmenter.Reconcile(_session.State, _session.Permanent, departure, returnUtc);
            if (summary.Outcome != RunOfflineOutcome.Applied) throw new InvalidOperationException("Offline return: " + summary.Outcome);
            _ctx.Record.Add("offline_completions", summary.Completions);
            _ctx.Record.Add("offline_partial_batches", summary.PartialBatchCompletions);
            _ctx.Record.Add("offline_sales_income", summary.SalesIncome);
            if (!_session.RebindAnchor(returnUtc).IsSuccess) throw new InvalidOperationException("Rebind rejected.");
            _ctx.AfterOffline();
        }

        // ---- observations -----------------------------------------------------------------------------

        private void Sample(RunRecord rec, long t, int step)
        {
            // How often a machine stood still because the store was full when the player looked (revision 2 content record).
            var views = new MachineStateView[_session.State.Machines.Count];
            int viewCount = _session.CopyMachineStates(views);
            for (int i = 0; i < viewCount; i++)
            {
                rec.Add("machine_look_samples", 1);
                if (views[i].IsActive && views[i].Blocker == MachineBlocker.StorageFull) rec.Add("store_blocked_samples", 1);
            }

            foreach (MachineInstance m in _session.State.Machines)
            {
                if (!_session.TryGetLastCompletion(m.InstanceId, out long units, out long batch, out int limit)) continue;
                rec.Add("completion_samples", 1);
                if (units >= batch) continue;
                rec.Add("partial_samples", 1);
                rec.Add("partial_fill_ppm_sum", units * 1_000_000L / batch);
                rec.Add(limit == RunSimulator.LimitStore ? "partial_limited_by_store" : "partial_limited_by_input", 1);
                rec.Add("partial_type." + m.MachineId, 1);

                // Income lost to a store-limited partial batch (OD-6, OD-9; content record r2). A completion that the input
                // limits is not a loss (the machine moves what exists). For a completion that the full store limits, the recipe
                // units the batch could not move are valued at the base price of the recipe outputs (output factor and sale
                // modifiers excluded). The look sees only the last completion of a machine, so
                // "partial_lost_cash_lower_bound" counts that one completion per look and machine (a lower bound of what the
                // run lost) and "partial_lost_cash_epoch_bound" assumes that every completion of the 20 s until the next look
                // was as short as the sampled one (an upper bound, not a measurement).
                if (limit == RunSimulator.LimitStore)
                {
                    MachineRecipe recipe = _catalog.Recipes[_catalog.Machines[m.MachineId].RecipeId];
                    long outputValue = 0;
                    foreach (ResourceAmount output in recipe.Outputs) outputValue += output.Amount * _catalog.Resources[output.ResourceId].BasePrice;
                    rec.Add("partial_lost_units", batch - units);
                    rec.Add("partial_lost_cash_lower_bound", (batch - units) * outputValue);
                    if (m.CycleDurationTicks > 0)
                    {
                        decimal cycleSeconds = m.CycleDurationTicks / (decimal)TicksPerSecond;
                        rec.Add("partial_lost_cash_epoch_bound", (long)((batch - units) * outputValue * step / cycleSeconds));
                    }
                }
            }
        }

        private void DetectFirstGear(RunRecord rec, long t)
        {
            if (rec.FirstGearAtSec >= 0) return;
            foreach (MachineInstance m in _session.State.Machines)
            {
                bool makesGear = false;
                foreach (ResourceAmount output in _catalog.Recipes[_catalog.Machines[m.MachineId].RecipeId].Outputs) makesGear |= output.ResourceId == "iron.gear";
                if (!makesGear || !_session.TryGetLastCompletion(m.InstanceId, out long units, out _, out _) || units <= 0) continue;
                rec.FirstGearAtSec = t;
                if (!_ctx.ChainEvents.ContainsKey("gear")) _ctx.ChainEvents["gear"] = (_session.TimelineUtcMs - rec.StartUtcMs) / 1000;
                return;
            }
        }

        private void TryCheckpoint(RunRecord rec, long t)
        {
            foreach (int checkpoint in CheckpointSeconds)
            {
                if (checkpoint != t) continue;
                AddRow(rec, t, _ctx.Preview().Value, null);
            }
        }

        // ---- rows -------------------------------------------------------------------------------------

        public static string FormatMinute(long seconds)
        {
            long whole = seconds / 60;
            long rest = seconds % 60;
            return rest == 0 ? whole.ToString(CultureInfo.InvariantCulture) : whole.ToString(CultureInfo.InvariantCulture) + "." + (rest * 100 / 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private void AddRow(RunRecord rec, long t, PrestigePreview preview, string label)
        {
            RunState st = _session.State;
            PermanentState perm = _session.Permanent;
            var stock = new StringBuilder();
            foreach (string id in new[] { "iron.ore", "iron.bar", "iron.plate", "iron.gear" })
            {
                if (stock.Length > 0) stock.Append('/');
                stock.Append(st.Inventory.IsAllowed(id) ? st.Inventory.Units(id) : 0);
            }

            var machines = new StringBuilder();
            var types = new List<string>(_catalog.Machines.Keys);
            types.Sort(StringComparer.Ordinal);
            foreach (string type in types)
            {
                int count = 0;
                int max = 0;
                foreach (MachineInstance m in st.Machines)
                    if (m.MachineId == type) { count++; max = Math.Max(max, m.Level); }
                if (count == 0) continue;
                if (machines.Length > 0) machines.Append(';');
                machines.Append(Short(type)).Append(':').Append(count).Append('@').Append(max);
            }
            if (machines.Length == 0) machines.Append('-');

            long contracts = 0;
            foreach (string offer in _catalog.ContractOffers.Keys) contracts += st.Contracts.CompletionCount(offer);

            long[] tiers = new long[3];
            foreach (ItemDef item in _catalog.Items.Values)
                if (item.Tier >= 1 && item.Tier <= 3) tiers[item.Tier - 1] += perm.ItemCount(item.Id);

            var research = new StringBuilder();
            var ids = new List<string>(_catalog.Research.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                long level = st.ResearchLevel(id);
                if (level == 0) continue;
                if (research.Length > 0) research.Append(';');
                research.Append(Short(id)).Append(':').Append(level);
            }
            if (research.Length == 0) research.Append('-');

            rec.Rows.Add(new[]
            {
                rec.Block,
                label == "E" ? FormatMinute(t) + "E" : FormatMinute(t),
                st.Cash.ToString(CultureInfo.InvariantCulture),
                preview.FactoryValue.Total.ToString(CultureInfo.InvariantCulture),
                preview.IpAward.ToString(CultureInfo.InvariantCulture),
                stock.ToString(),
                machines.ToString(),
                st.StorageLevel.ToString(CultureInfo.InvariantCulture),
                st.FactoryLevel.ToString(CultureInfo.InvariantCulture),
                contracts.ToString(CultureInfo.InvariantCulture),
                tiers[0].ToString(CultureInfo.InvariantCulture) + "/" + tiers[1].ToString(CultureInfo.InvariantCulture) + "/" + tiers[2].ToString(CultureInfo.InvariantCulture),
                research.ToString(),
                "-1",
                "-1",
            });
        }

        private static string Short(string id)
        {
            int dot = id.IndexOf('.');
            return dot < 0 ? id : id.Substring(dot + 1);
        }
    }

    /// <summary>One band or sensitivity bound with its measured value and verdict (reported, not asserted, until T125).</summary>
    internal sealed class BandResult
    {
        public string Name;
        public string Measured;
        public string Expected;
        public bool Pass;
    }

    /// <summary>The whole trace: baseline rows, the five-run chain, the sensitivity variants, the CSV and the band verdicts.</summary>
    internal sealed class BalanceTraceReport
    {
        public const string Header = "block,minute,cash,fv,ip_preview,stock,machines,storageLevel,factoryLevel,contractsCompleted,itemsHeld,researchLevels,firstGearAtSec,eligibleAtSec";
        public const int MaxRuns = 5;

        public RunRecord Baseline;
        public readonly List<RunRecord> Chain = new List<RunRecord>();
        public readonly List<RunRecord> Scaled = new List<RunRecord>();
        public readonly List<RunRecord> Variants = new List<RunRecord>();
        public readonly List<string> Violations = new List<string>();
        public SortedDictionary<string, long> ChainEvents;
        public SortedDictionary<string, long> ScaledEvents;
        public string Csv;

        public static BalanceTraceReport Build(RunCatalog catalog)
        {
            var report = new BalanceTraceReport { _real = catalog };

            var baselineRunner = new BalanceTraceRunner(catalog, new ReferencePolicy());
            report.Baseline = baselineRunner.PlayRun("run1", 1, false, 7_200);
            report.Violations.AddRange(baselineRunner.Context.Violations);

            report.PlayChain(catalog, report.Chain, out report.ChainEvents);
            RunCatalog scaled = ScaleThreshold(catalog);
            report.PlayChain(scaled, report.Scaled, out report.ScaledEvents);

            // FR-025 (QA-14): the rounding price variants (every base price scaled and rounded to a whole number) are gone. A price variant is a
            // sale-price modifier that the engine folds without rounding (floored once per sale), seeded as one Industry level that no run can buy.
            report.Variants.Add(PriceVariant("var.price-mod-125", catalog, 1_250_000));
            report.Variants.Add(PriceVariant("var.price-mod-120", catalog, 1_200_000));
            report.Variants.Add(Variant("var.contracts-off", p => p.ContractsEnabled = false, catalog));
            report.Variants.Add(Variant("var.contracts-slow", p => p.ContractCooldownSeconds = 600, catalog));
            report.Variants.Add(Variant("var.overdrive-off", p => p.OverdriveEnabled = false, catalog));
            report.Variants.Add(Variant("var.absence-30m", p => { }, catalog, new[] { new Absence(1_800, 28_800) }));
            report.Variants.Add(Variant("var.absence-60m", p => { }, catalog, new[] { new Absence(3_600, 28_800) }));
            var returns = new List<Absence>();
            for (long start = 600; start < 21_000; start += 900) returns.Add(new Absence(start, 600));
            report.Variants.Add(Variant("var.returns", p => { }, catalog, returns));
            report.Variants.Add(Variant("probe.ore-only", p => { p.OreOnly = true; p.ContractsEnabled = false; }, catalog, null, true));

            report.Csv = report.Compose();
            return report;
        }

        /// <summary>Plays up to five runs with the prestige at eligibility, spending all IP after each one, and gathers the violations.</summary>
        private void PlayChain(RunCatalog catalog, List<RunRecord> into, out SortedDictionary<string, long> events)
        {
            var runner = new BalanceTraceRunner(catalog, new ReferencePolicy());
            string prefix = ReferenceEquals(catalog, _real) ? "run" : "scaled.run";
            for (int n = 1; n <= MaxRuns; n++)
            {
                RunRecord rec = runner.PlayRun(prefix + n, n, true, 0);
                into.Add(rec);
                if (rec.PrestigedAtSec < 0) break;
                runner.SpendIndustry();
            }
            Violations.AddRange(runner.Context.Violations);
            events = runner.Context.ChainEvents;
        }

        private RunCatalog _real;

        /// <summary>The catalog with the prestige threshold (and the IP curve divisor) divided by 100: the same game, a shorter first run.</summary>
        public static RunCatalog ScaleThreshold(RunCatalog c)
        {
            var prestige = new PrestigeDef(BalanceBands.ScaledThresholdFv, c.Prestige.CurveKind, c.Prestige.Coefficient, c.Prestige.Divisor / 100);
            return IdleFactory.Tests.EditMode.Run.TestCatalogBuilder.From(c).WithPrestige(prestige).Build();
        }

        /// <summary>The Industry upgrade that carries a price variant; it costs more IP than a run can award, so only the seeded level exists.</summary>
        public const string PriceVariantUpgradeId = "industry.var-price";

        private static RunRecord PriceVariant(string name, RunCatalog catalog, long salePricePpm)
        {
            var upgrades = new List<IndustryUpgradeDef>(catalog.IndustryUpgrades.Values)
            {
                new IndustryUpgradeDef(PriceVariantUpgradeId, "Price variant", 1, new Curve(1_000_000_000L, 1_000_000L, "ceil"),
                    new EffectDef("salePrice", "allResources", null, salePricePpm)),
            };
            RunCatalog variant = IdleFactory.Tests.EditMode.Run.TestCatalogBuilder.From(catalog).WithIndustryUpgrades(upgrades).Build();
            var seed = new PermanentState(variant);
            if (seed.TrySetIndustryLevel(PriceVariantUpgradeId, 1) != RunStateResult.Ok) throw new InvalidOperationException("The price variant level was rejected.");
            return Variant(name, p => { }, variant, null, false, seed);
        }

        private static RunRecord Variant(string name, Action<ReferencePolicy> configure, RunCatalog catalog, IReadOnlyList<Absence> absences = null, bool toEligibility = false,
            PermanentState seed = null)
        {
            var policy = new ReferencePolicy();
            configure(policy);
            var runner = new BalanceTraceRunner(catalog, policy, seed);
            RunRecord rec = runner.PlayRun(name, 1, false, toEligibility ? 0 : -1, absences);
            if (runner.Context.Violations.Count > 0)
                throw new InvalidOperationException(name + ": " + string.Join("; ", runner.Context.Violations));
            return rec;
        }

        // ---- CSV ------------------------------------------------------------------------------------------

        private string Compose()
        {
            var b = new StringBuilder();
            b.Append(Header).Append('\n');
            foreach (string[] row in Baseline.Rows) b.Append(string.Join(",", row)).Append('\n');
            for (int i = 1; i < Chain.Count; i++)
                foreach (string[] row in Chain[i].Rows) b.Append(string.Join(",", row)).Append('\n');
            foreach (RunRecord v in Variants)
                foreach (string[] row in v.Rows) b.Append(string.Join(",", row)).Append('\n');

            b.Append('\n').Append("block,key,value\n");
            foreach (RunRecord rec in Chain) AppendStats(b, rec, "run" + rec.RunNumber);
            foreach (RunRecord rec in Scaled) AppendStats(b, rec, rec.Block);
            foreach (RunRecord rec in Variants) AppendStats(b, rec, rec.Block);
            return b.ToString();
        }

        private static void AppendStats(StringBuilder b, RunRecord rec, string block)
        {
            void Line(string key, long value) => b.Append(block).Append(',').Append(key).Append(',').Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
            Line("first_gear_sec", rec.FirstGearAtSec);
            Line("eligible_sec", rec.EligibleAtSec);
            Line("eligible_fv", rec.EligibleFactoryValue);
            Line("milestone_sec", rec.MilestoneAtSec);
            Line("play_sec_to_milestone", rec.PlaySecToMilestone);
            Line("prestiged_sec", rec.PrestigedAtSec);
            Line("play_sec_to_eligible", rec.PlaySecToEligible);
            Line("absent_sec", rec.AbsentSec);
            Line("ip_awarded", rec.IpAwarded);
            Line("cap_reached", rec.CapReached ? 1 : 0);
            foreach (KeyValuePair<string, long> pair in rec.Stats) Line(pair.Key, pair.Value);
        }

        // ---- bands ----------------------------------------------------------------------------------------

        private static string Sec(long value) => value < 0 ? "not reached" : value.ToString(CultureInfo.InvariantCulture) + " s";

        private static BandResult Band(string name, long value, long min, long max) => new BandResult
        {
            Name = name,
            Measured = Sec(value),
            Expected = min.ToString(CultureInfo.InvariantCulture) + " to " + max.ToString(CultureInfo.InvariantCulture) + " s",
            Pass = value >= min && value <= max,
        };

        private static string Delta(long permille) => (permille - 1000).ToString("+0;-0;0", CultureInfo.InvariantCulture);

        private static BandResult RunTimes(string name, List<RunRecord> chain)
        {
            var sb = new StringBuilder();
            bool pass = chain.Count == MaxRuns && chain[0].EligibleAtSec > 0;
            long previous = chain.Count > 0 ? chain[0].EligibleAtSec : -1;
            for (int i = 0; i < chain.Count; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(chain[i].EligibleAtSec);
                if (i == 0) continue;
                if (chain[i].EligibleAtSec < 0 || chain[i].EligibleAtSec > previous) pass = false;
                if (chain[0].EligibleAtSec > 0 && chain[i].EligibleAtSec * 1000 < chain[0].EligibleAtSec * BalanceBands.RunFloorPermille) pass = false;
                previous = chain[i].EligibleAtSec;
            }
            return new BandResult
            {
                Name = name,
                Measured = chain.Count > 0 && chain[0].EligibleAtSec < 0 ? "run 1 not reached" : sb + " s",
                Expected = "non-increasing, each at least " + BalanceBands.RunFloorPermille / 10 + " % of run 1",
                Pass = pass,
            };
        }

        public List<BandResult> Bands()
        {
            var list = new List<BandResult>();
            list.Add(Band("first Gear (run 1)", Baseline.FirstGearAtSec, BalanceBands.FirstGearMinSec, BalanceBands.FirstGearMaxSec));
            list.Add(Band("eligibility (run 1)", Baseline.EligibleAtSec, BalanceBands.EligibleMinSec, BalanceBands.EligibleMaxSec));
            list.Add(Band("second run to eligibility (run 2)", Chain.Count > 1 ? Chain[1].EligibleAtSec : -1, BalanceBands.SecondRunMinSec, BalanceBands.SecondRunMaxSec));
            list.Add(RunTimes("runs 1 to 5 (ECO-3)", Chain));
            list.Add(RunTimes("scaled runs 1 to 5 (ECO-3, threshold scaled)", Scaled));

            long baseline = Baseline.PlaySecToMilestone;
            foreach (RunRecord v in Variants)
            {
                long play = v.PlaySecToMilestone;
                long permille = baseline > 0 && play >= 0 ? play * 1000 / baseline : -1;
                string measured = permille < 0 ? "not reached" : play + " s play (" + Delta(permille) + " per mille)";
                bool ok;
                string expected;
                switch (v.Block)
                {
                    case "var.price-mod-125":
                    case "var.price-mod-120":
                        expected = "within +/- " + BalanceBands.PriceBoundPermille + " per mille of baseline";
                        ok = permille >= 0 && Math.Abs(permille - 1000) <= BalanceBands.PriceBoundPermille;
                        break;
                    case "var.contracts-off":
                    case "var.contracts-slow":
                        expected = "-50 to +" + BalanceBands.ContractGrowthBoundPermille + " per mille of baseline";
                        ok = permille >= 0 && permille - 1000 <= BalanceBands.ContractGrowthBoundPermille && permille - 1000 >= -50;
                        break;
                    case "var.overdrive-off":
                        expected = "0 to +" + BalanceBands.OverdriveGrowthBoundPermille + " per mille of baseline";
                        ok = permille >= 1000 && permille - 1000 <= BalanceBands.OverdriveGrowthBoundPermille;
                        break;
                    case "var.absence-30m":
                    case "var.absence-60m":
                        expected = "play time at least " + (1000 - BalanceBands.AbsenceMaxSavingPermille) + " per mille of baseline";
                        ok = permille >= 1000 - BalanceBands.AbsenceMaxSavingPermille;
                        break;
                    case "var.returns":
                        long wall = v.MilestoneAtSec;
                        permille = baseline > 0 && wall >= 0 ? wall * 1000 / baseline : -1;
                        measured = wall < 0 ? "not reached" : wall + " s wall (" + Delta(permille) + " per mille)";
                        expected = "wall time " + BalanceBands.ReturnsMinPermille + " to " + BalanceBands.ReturnsMaxPermille + " per mille of baseline";
                        ok = permille >= BalanceBands.ReturnsMinPermille && permille <= BalanceBands.ReturnsMaxPermille;
                        break;
                    default:
                        measured = "milestone " + Sec(v.MilestoneAtSec) + ", eligibility " + Sec(v.EligibleAtSec);
                        expected = "informational (dominant-strategy probe, not a reference policy)";
                        ok = true;
                        break;
                }
                list.Add(new BandResult { Name = v.Block, Measured = measured, Expected = expected, Pass = ok });
            }
            return list;
        }
    }
}
