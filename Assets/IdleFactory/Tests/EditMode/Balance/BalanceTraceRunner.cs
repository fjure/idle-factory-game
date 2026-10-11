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

        /// <summary>Cash gained per second between two looks (moving average over about four looks).</summary>
        public decimal ObservedIncome { get; set; }

        public long CashAfterLook { get; set; }
        public readonly List<string> Violations = new List<string>();
        public readonly SortedDictionary<string, long> ChainEvents = new SortedDictionary<string, long>(StringComparer.Ordinal);

        public static Guid Id(int n) => new Guid(n, 0, 0, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 });

        public void BeginRun()
        {
            _lastExpiredOfferId = null;
            _lastExpiredDeadline = long.MinValue;
            ContractCooldownUntilUtcMs = 0;
        }

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
            // Every command ends with a settle: a machine that waited for room or input may complete (production) and an
            // auto-sell resource is sold. Cash and stock may therefore grow beyond the command's own change, never fall short.
            if (after.Cash < before.Cash + dCash) Violations.Add($"{what}: cash {before.Cash} -> {after.Cash}, expected at least {dCash:+0;-0;0}");
            else if (after.Cash > before.Cash + dCash) Record.Add("settle_cash_gain", after.Cash - before.Cash - dCash);
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
            return true;
        }

        public bool SetAutoSell(string resourceId, bool enabled)
        {
            Commands++;
            Snap before = Take();
            RunCommandResult<bool> r = _commands.SetAutoSell(resourceId, enabled);
            if (!r.IsSuccess) { Reject("autosell", r.ReasonCode); return false; }
            // Enabling auto-sell may sell stock at once; the sale itself is a legitimate Cash/stock exchange.
            if (!enabled) Verify("autosell off", before);
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

            long market = d.UnitsConsumed * Catalog.Resources[offer.ResourceId].BasePrice;
            Record.Add("contracts_delivered", 1);
            Record.Add("delivered." + d.OfferId, 1);
            Record.Add("contract_cash_reward", d.CashReward);
            Record.Add("contract_market_value", market);
            Record.Add("contract_ratio_ppm_sum", market == 0 ? 0 : d.CashReward * 1_000_000L / market);
            Record.Add("rp_earned", d.RpReward);
            Record.Add("rp_per_completion_sum", d.RpReward);
            foreach (IdCount grant in d.ItemsGranted)
                Record.Add("items_granted.tier" + Catalog.Items[grant.Id].Tier, grant.Count);
            if (!ChainEvents.ContainsKey("contract")) ChainEvents["contract"] = ElapsedSeconds();
            if (offer.Kind == "gear" && !ChainEvents.ContainsKey("gearContract")) ChainEvents["gearContract"] = ElapsedSeconds();
            if (d.ItemsGranted.Count > 0 && !ChainEvents.ContainsKey("item")) ChainEvents["item"] = ElapsedSeconds();
            ContractCooldownUntilUtcMs = Session.TimelineUtcMs + Policy.ContractCooldownSeconds * 1000L;
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

        private long ElapsedSeconds() => (Session.TimelineUtcMs - RunStartUtcMs) / 1000;
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
        /// Plays run <paramref name="runNumber"/> from the session's current fresh state. <paramref name="prestigeAtEligibility"/>
        /// ends the run with the prestige at the first look at or after eligibility that is not inside an idle window;
        /// otherwise the run is not ended by the policy and is played until <paramref name="minEndSec"/> and eligibility.
        /// </summary>
        /// <summary>
        /// When true (the strategy ensemble, T311), the Factory Value is recorded at the exact checkpoint seconds in
        /// <see cref="RunRecord.CheckpointFactoryValue"/>: a look step that spans a checkpoint is advanced in two parts.
        /// Off by default, so the trace and its golden file are unchanged.
        /// </summary>
        public bool RecordCheckpoints { get; set; }

        public RunRecord PlayRun(string block, int runNumber, bool prestigeAtEligibility, long minEndSec, IReadOnlyList<Absence> absences = null,
            int offsetSec = 0, long stopAfterSec = -1)
        {
            int look = _policy.LookIntervalSeconds;
            if (look <= 0 || offsetSec < 0) throw new ArgumentOutOfRangeException(nameof(offsetSec));
            var rec = new RunRecord { Block = block, RunNumber = runNumber, StartUtcMs = _session.TimelineUtcMs };
            _ctx.Record = rec;
            _ctx.RunStartUtcMs = rec.StartUtcMs;
            _ctx.BeginRun();
            _ctx.ObservedIncome = 0m;
            long t = 0;
            // The look grid: looks happen at run seconds offsetSec + k * look (T311). Offset 0 is the reference grid.
            if (offsetSec == 0) _policy.Act(_ctx);
            _ctx.CashAfterLook = _session.State.Cash;

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
                    decimal sample = (_session.State.Cash - _ctx.CashAfterLook) / (decimal)step;
                    _ctx.ObservedIncome = (_ctx.ObservedIncome * 3m + Math.Max(sample, 0m)) / 4m;
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

                if (rec.EligibleAtSec >= 0 && prestigeAtEligibility && !_policy.IsIdleAt(t))
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
                    rec.CapReached = rec.EligibleAtSec < 0;
                    return Finish(rec);
                }

                if (stopAfterSec >= 0 && t >= stopAfterSec) return Finish(rec);

                if (!_policy.IsIdleAt(t)) _policy.Act(_ctx);
                _ctx.CashAfterLook = _session.State.Cash;
            }
        }

        private void AdvanceStep(RunRecord rec, long t, int step)
        {
            long done = 0;
            if (RecordCheckpoints)
            {
                foreach (int checkpoint in CheckpointSeconds)
                {
                    if (checkpoint <= t + done || checkpoint >= t + step) continue;
                    AdvanceSeconds(checkpoint - t - done);
                    done = checkpoint - t;
                    rec.CheckpointFactoryValue[checkpoint] = _ctx.Preview().Value.FactoryValue.Total;
                }
            }
            AdvanceSeconds(step - done);
        }

        private void AdvanceSeconds(long seconds)
        {
            RunCommandResult<long> advanced = _session.Advance(seconds * TicksPerSecond);
            if (!advanced.IsSuccess) throw new InvalidOperationException("Advance rejected: " + advanced.ReasonCode);
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

            report.Variants.Add(Variant("var.price-80", p => { }, ScalePrices(catalog, 800)));
            report.Variants.Add(Variant("var.price-120", p => { }, ScalePrices(catalog, 1_200)));
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

        private static RunRecord Variant(string name, Action<ReferencePolicy> configure, RunCatalog catalog, IReadOnlyList<Absence> absences = null, bool toEligibility = false)
        {
            var policy = new ReferencePolicy();
            configure(policy);
            var runner = new BalanceTraceRunner(catalog, policy);
            RunRecord rec = runner.PlayRun(name, 1, false, toEligibility ? 0 : -1, absences);
            if (runner.Context.Violations.Count > 0)
                throw new InvalidOperationException(name + ": " + string.Join("; ", runner.Context.Violations));
            return rec;
        }

        /// <summary>The catalog with every market price scaled (per mille). Contract rewards and costs are unchanged.</summary>
        public static RunCatalog ScalePrices(RunCatalog c, long permille)
        {
            var resources = new List<ResourceDef>();
            foreach (ResourceDef r in c.Resources.Values)
                resources.Add(new ResourceDef(r.Id, r.Label, r.PresentationKey, Math.Max(1, (r.BasePrice * permille + 500) / 1000), r.Unlock));
            return IdleFactory.Tests.EditMode.Run.TestCatalogBuilder.From(c).WithResources(resources).Build();
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
                    case "var.price-80":
                    case "var.price-120":
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
