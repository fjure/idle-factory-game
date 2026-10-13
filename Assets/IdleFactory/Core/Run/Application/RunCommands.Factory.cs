using System;
using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Run.Application
{
    /// <summary>Result of <see cref="RunCommands.Build"/>: the new machine, what it cost and what it makes at level 1.</summary>
    public readonly struct BuildResult
    {
        public Guid InstanceId { get; }
        public int SlotIndex { get; }
        public string MachineId { get; }
        public long CostPaid { get; }
        public MachineRates Rates { get; }

        /// <summary>The cost of the first upgrade after the <c>upgradeCost</c> modifiers; 0 when the machine has no further level.</summary>
        public long NextUpgradeCost { get; }

        public BuildResult(Guid instanceId, int slotIndex, string machineId, long costPaid, MachineRates rates, long nextUpgradeCost)
        {
            InstanceId = instanceId;
            SlotIndex = slotIndex;
            MachineId = machineId;
            CostPaid = costPaid;
            Rates = rates;
            NextUpgradeCost = nextUpgradeCost;
        }
    }

    /// <summary>
    /// Result of <see cref="RunCommands.Upgrade"/> (US1 scenario 5): the new level and rate, whether the level is a
    /// milestone, and the next cost and whether that one is a milestone.
    /// </summary>
    public readonly struct UpgradeResult
    {
        public Guid InstanceId { get; }
        public int OldLevel { get; }
        public int NewLevel { get; }
        public long CostPaid { get; }
        public MachineRates Rates { get; }
        public bool MilestoneReached { get; }
        public int MilestoneLevel { get; }
        public long MilestoneMultiplierPpm { get; }
        public bool HasNextUpgrade { get; }
        public long NextCost { get; }
        public bool NextReachesMilestone { get; }

        public UpgradeResult(Guid instanceId, int oldLevel, int newLevel, long costPaid, MachineRates rates, bool milestoneReached,
            int milestoneLevel, long milestoneMultiplierPpm, bool hasNextUpgrade, long nextCost, bool nextReachesMilestone)
        {
            InstanceId = instanceId;
            OldLevel = oldLevel;
            NewLevel = newLevel;
            CostPaid = costPaid;
            Rates = rates;
            MilestoneReached = milestoneReached;
            MilestoneLevel = milestoneLevel;
            MilestoneMultiplierPpm = milestoneMultiplierPpm;
            HasNextUpgrade = hasNextUpgrade;
            NextCost = nextCost;
            NextReachesMilestone = nextReachesMilestone;
        }
    }

    /// <summary>Result of <see cref="RunCommands.Remove"/>.</summary>
    public readonly struct RemoveResult
    {
        public Guid InstanceId { get; }
        public int SlotIndex { get; }
        public long InvestedCash { get; }
        public long Refund { get; }

        /// <summary>The Factory Value change of the removal, <c>refund - investedCash</c>, never positive (ECO-7).</summary>
        public long FactoryValueDelta { get; }

        public RemoveResult(Guid instanceId, int slotIndex, long investedCash, long refund)
        {
            InstanceId = instanceId;
            SlotIndex = slotIndex;
            InvestedCash = investedCash;
            Refund = refund;
            FactoryValueDelta = refund - investedCash;
        }
    }

    /// <summary>Result of <see cref="RunCommands.UpgradeStorage"/> and <see cref="RunCommands.UpgradeFactory"/>.</summary>
    public readonly struct ExpansionResult
    {
        public int NewLevel { get; }
        public long CostPaid { get; }

        /// <summary>The storage capacity or slot count of the new level.</summary>
        public long NewValue { get; }
        public bool HasNext { get; }
        public long NextCost { get; }

        public ExpansionResult(int newLevel, long costPaid, long newValue, bool hasNext, long nextCost)
        {
            NewLevel = newLevel;
            CostPaid = costPaid;
            NewValue = newValue;
            HasNext = hasNext;
            NextCost = nextCost;
        }
    }

    /// <summary>
    /// The only mutation API of the run (contracts/run-application.md). This part holds the factory commands of
    /// User Story 1: Build, Upgrade, Remove, SetActive, ReorderPriority, SetAutoSell, UpgradeStorage and
    /// UpgradeFactory. Later stories add their commands to this partial class.
    ///
    /// Every command follows the same four rules: (1) reject with <c>lifecycle.blocked</c> while the lifecycle holds
    /// commands back; (2) validate every precondition before anything changes; (3) pre-check that the settle after
    /// the change cannot overflow (see <see cref="SettleFits"/>), so <c>value.limit</c> is always returned before a
    /// mutation, never after one; (4) mutate in one checked domain transaction, then settle through
    /// <see cref="RunSession.EndCommand{T}"/>. A rejection changes neither the state nor a revision.
    ///
    /// An instance ID that is not owned (a stale UI reference) has no contract code and is rejected with the
    /// internal <see cref="RunReasons.CommandInvalid"/>; UI copy needs a generic fallback for it.
    /// </summary>
    public sealed partial class RunCommands
    {
        private readonly RunSession _session;
        private readonly RunCatalog _catalog;
        private readonly RunModifiers _modifiers;
        private readonly Func<Guid> _newInstanceId;
        private readonly Func<Guid> _newRunId;

        /// <param name="session">The live run.</param>
        /// <param name="newInstanceId">Source of machine instance IDs; <see cref="Guid.NewGuid"/> by default. Tests inject a deterministic one.</param>
        /// <param name="newRunId">Source of run IDs for the prestige transition; <see cref="Guid.NewGuid"/> by default.</param>
        public RunCommands(RunSession session, Func<Guid> newInstanceId = null, Func<Guid> newRunId = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _catalog = session.Catalog;
            _modifiers = new RunModifiers(_catalog);
            _newInstanceId = newInstanceId ?? Guid.NewGuid;
            _newRunId = newRunId ?? Guid.NewGuid;
        }

        private RunState State => _session.State;

        private PermanentState Permanent => _session.Permanent;

        private bool OverdriveActive => State.Overdrive.IsActive(_session.TimelineUtcMs);

        private static RunCommandResult<T> Reject<T>(string code) => RunCommandResult.Reject<T>(code);

        // ---------------------------------------------------------------------------------------------------
        // Build
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Builds <paramref name="machineId"/> in <paramref name="slot"/>. Rejected with slot.unavailable (outside the
        /// slot count), slot.occupied, machine.locked (unknown or not unlocked type), value.limit, build.price.changed
        /// (<paramref name="expectedPrice"/> given and different from the price now; <c>null</c> means the price at the
        /// call instant) or cash.insufficient.
        /// The cost is <see cref="BuildPricing"/> (curve over owned count and prestige count, then the <c>upgradeCost</c>
        /// modifiers; the level-1 table cost for content without a build price), charged once and recorded as
        /// the machine's invested Cash; the machine starts at level 1, active, with progress 0, and joins the
        /// priority order last.
        /// </summary>
        public RunCommandResult<BuildResult> Build(int slot, string machineId, long? expectedPrice = null)
        {
            if (_session.IsLifecycleBlocked) return Reject<BuildResult>(RunReasons.LifecycleBlocked);
            RunState state = State;
            if (slot < 0 || slot >= state.SlotCount) return Reject<BuildResult>(RunReasons.SlotUnavailable);
            if (state.MachineAtSlot(slot) != null) return Reject<BuildResult>(RunReasons.SlotOccupied);
            if (machineId == null || !_catalog.Machines.TryGetValue(machineId, out MachineDef def) || !state.IsUnlocked(machineId))
                return Reject<BuildResult>(RunReasons.MachineLocked);
            if (!BuildPricing.TryPrice(_catalog, _modifiers, state, Permanent, def, out long cost))
                return Reject<BuildResult>(RunReasons.ValueLimit);
            if (expectedPrice.HasValue && expectedPrice.Value != cost) return Reject<BuildResult>(RunReasons.BuildPriceChanged);
            if (state.Cash < cost) return Reject<BuildResult>(RunReasons.CashInsufficient);

            Guid id = _newInstanceId();
            if (id == Guid.Empty || state.FindMachine(id) != null) return Reject<BuildResult>(RunReasons.CommandInvalid);
            MachineRecipe recipe = _catalog.Recipes[def.RecipeId];
            var machine = new MachineInstance(id, def, slot, 1, true, 0, recipe.DurationTicks, cost);
            if (!RunDerivation.TryDerive(_catalog, _modifiers, state, Permanent, machine, OverdriveActive, out RunDerivation.Derived derived) ||
                machine.TryStartCycle(derived.Cycle) != RunStateResult.Ok)
                return Reject<BuildResult>(RunReasons.ValueLimit);
            if (!SettleFits(state.Cash - cost, null, null, null, null)) return Reject<BuildResult>(RunReasons.ValueLimit);

            _session.BeginCommand();
            RunStateResult built = state.TryBuildMachine(machine, cost);
            if (built != RunStateResult.Ok) return Reject<BuildResult>(RunReasons.From(built));
            long nextCost = 0;
            if (def.MaxLevel > 1) _modifiers.TryUpgradeCost(state, Permanent, def.CostTable[1], def.Id, out nextCost);
            return _session.EndCommand(new BuildResult(id, slot, def.Id, cost, RunDerivation.Rates(derived), nextCost), true);
        }

        // ---------------------------------------------------------------------------------------------------
        // Upgrade
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Upgrades an owned machine by one level. Rejected with command.invalid (not owned), level.max,
        /// cash.insufficient or value.limit. Charges the cost (level table after <c>upgradeCost</c>), adds it to the
        /// invested Cash and rescales the progress to the new cycle, keeping the completed fraction (R03). The result
        /// reports the new rate, whether the new level is a milestone, and the next cost.
        /// </summary>
        public RunCommandResult<UpgradeResult> Upgrade(Guid instanceId)
        {
            if (_session.IsLifecycleBlocked) return Reject<UpgradeResult>(RunReasons.LifecycleBlocked);
            RunState state = State;
            MachineInstance machine = state.FindMachine(instanceId);
            if (machine == null) return Reject<UpgradeResult>(RunReasons.CommandInvalid);
            if (machine.Level >= machine.MaxLevel) return Reject<UpgradeResult>(RunReasons.LevelMax);
            MachineDef def = _catalog.Machines[machine.MachineId];
            if (!_modifiers.TryUpgradeCost(state, Permanent, def.CostTable[machine.Level], def.Id, out long cost))
                return Reject<UpgradeResult>(RunReasons.ValueLimit);
            if (state.Cash < cost) return Reject<UpgradeResult>(RunReasons.CashInsufficient);

            // The machine as it will be: the next level's composed speed gives its cycle and batch.
            int newLevel = machine.Level + 1;
            var upgraded = new MachineInstance(instanceId, def, machine.SlotIndex, newLevel, machine.IsActive, 0,
                _catalog.Recipes[def.RecipeId].DurationTicks, 0);
            if (!RunDerivation.TryDerive(_catalog, _modifiers, state, Permanent, upgraded, OverdriveActive, out RunDerivation.Derived derived))
                return Reject<UpgradeResult>(RunReasons.ValueLimit);
            RunStateResult check = machine.CheckUpgrade(cost, derived.Cycle);
            if (check != RunStateResult.Ok) return Reject<UpgradeResult>(check == RunStateResult.LevelMax ? RunReasons.LevelMax : RunReasons.ValueLimit);
            SpeedComposer.TryRescaleProgress(machine.ProgressTicks, machine.CycleDurationTicks, derived.Cycle, out long progress);
            CheckedMath.TryAdd(machine.InvestedCash, cost, out long invested);
            var after = new MachineInstance(instanceId, def, machine.SlotIndex, newLevel, machine.IsActive, progress, derived.Cycle, invested);
            if (!SettleFits(state.Cash - cost, null, null, machine, after)) return Reject<UpgradeResult>(RunReasons.ValueLimit);

            _session.BeginCommand();
            RunStateResult upgradedResult = state.TryUpgradeMachine(instanceId, cost, derived.Cycle);
            if (upgradedResult != RunStateResult.Ok) return Reject<UpgradeResult>(RunReasons.From(upgradedResult));

            SpeedMilestone reached = RunDerivation.MilestoneAt(def, newLevel);
            bool hasNext = newLevel < def.MaxLevel;
            long nextCost = 0;
            if (hasNext) _modifiers.TryUpgradeCost(state, Permanent, def.CostTable[newLevel], def.Id, out nextCost);
            var result = new UpgradeResult(instanceId, newLevel - 1, newLevel, cost, RunDerivation.Rates(derived), reached != null,
                reached?.Level ?? 0, reached?.SpeedPpm ?? 0, hasNext, nextCost, hasNext && RunDerivation.MilestoneAt(def, newLevel + 1) != null);
            return _session.EndCommand(result, true);
        }

        // ---------------------------------------------------------------------------------------------------
        // Remove
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Removes an owned machine after confirmation. Rejected with command.invalid (not owned),
        /// confirmation.required (<paramref name="confirmed"/> false) or value.limit. Refunds
        /// <c>floor(investedCash x refundPpm / 10^6)</c> of the Cash actually paid (discounts included, so build then
        /// remove always lowers Factory Value, ECO-7), drops the progress, frees the slot and closes its priority
        /// position. Replacing a machine is this command followed by <see cref="Build"/>.
        /// </summary>
        public RunCommandResult<RemoveResult> Remove(Guid instanceId, bool confirmed)
        {
            if (_session.IsLifecycleBlocked) return Reject<RemoveResult>(RunReasons.LifecycleBlocked);
            RunState state = State;
            MachineInstance machine = state.FindMachine(instanceId);
            if (machine == null) return Reject<RemoveResult>(RunReasons.CommandInvalid);
            if (!confirmed) return Reject<RemoveResult>(RunReasons.ConfirmationRequired);
            if (!MulDiv.MulDivFloor(machine.InvestedCash, _catalog.RunStart.RemovalRefundPpm, 1_000_000, out long refund) ||
                !CheckedMath.TryAdd(state.Cash, refund, out long cashAfter) ||
                !SettleFits(cashAfter, null, null, machine, null))
                return Reject<RemoveResult>(RunReasons.ValueLimit);

            int slot = machine.SlotIndex;
            long invested = machine.InvestedCash;
            _session.BeginCommand();
            RunStateResult removed = state.TryRemoveMachine(instanceId, out long paid);
            if (removed != RunStateResult.Ok) return Reject<RemoveResult>(RunReasons.From(removed));
            return _session.EndCommand(new RemoveResult(instanceId, slot, invested, paid), true);
        }

        // ---------------------------------------------------------------------------------------------------
        // SetActive
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Switches an owned machine on or off; its progress is kept. Rejected with command.invalid (not owned) or
        /// value.limit. The value is the machine's active flag afterwards. Switching a machine on may complete a
        /// waiting cycle at once (the settle).
        /// </summary>
        public RunCommandResult<bool> SetActive(Guid instanceId, bool active)
        {
            if (_session.IsLifecycleBlocked) return Reject<bool>(RunReasons.LifecycleBlocked);
            RunState state = State;
            MachineInstance machine = state.FindMachine(instanceId);
            if (machine == null) return Reject<bool>(RunReasons.CommandInvalid);
            if (!SettleFits(state.Cash, null, null, null, null)) return Reject<bool>(RunReasons.ValueLimit);

            bool changed = machine.IsActive != active;
            _session.BeginCommand();
            machine.SetActive(active);
            return _session.EndCommand(active, changed);
        }

        // ---------------------------------------------------------------------------------------------------
        // ReorderPriority
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Replaces the priority order. <paramref name="permutation"/> must hold exactly the current machine
        /// instance IDs, each once (priority.not-permutation otherwise, also for null). The value is the number of
        /// machines. The order is the only tie-break between machines that compete for a scarce input (US1
        /// scenario 3); slot position never matters.
        /// </summary>
        public RunCommandResult<int> ReorderPriority(IReadOnlyList<Guid> permutation)
        {
            if (_session.IsLifecycleBlocked) return Reject<int>(RunReasons.LifecycleBlocked);
            RunState state = State;
            if (permutation == null || permutation.Count != state.Priority.Count) return Reject<int>(RunReasons.PriorityNotPermutation);
            var seen = new bool[permutation.Count];
            bool changed = false;
            for (int i = 0; i < permutation.Count; i++)
            {
                int at = state.Priority.IndexOf(permutation[i]);
                if (at < 0 || seen[at]) return Reject<int>(RunReasons.PriorityNotPermutation);
                seen[at] = true;
                if (at != i) changed = true;
            }
            if (!SettleFits(state.Cash, null, null, null, null)) return Reject<int>(RunReasons.ValueLimit);

            _session.BeginCommand();
            RunStateResult reordered = state.TryReorderPriority(permutation);
            if (reordered != RunStateResult.Ok) return Reject<int>(RunReasons.From(reordered));
            return _session.EndCommand(permutation.Count, changed);
        }

        // ---------------------------------------------------------------------------------------------------
        // SetAutoSell
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Turns automatic selling of an unlocked resource on or off (resource.locked otherwise; value.limit when the
        /// immediate sale would overflow Cash). Changes only that setting, then settles: enabling sells the whole stock
        /// at once. The value is the setting afterwards.
        /// </summary>
        public RunCommandResult<bool> SetAutoSell(string resourceId, bool enabled)
        {
            if (_session.IsLifecycleBlocked) return Reject<bool>(RunReasons.LifecycleBlocked);
            RunState state = State;
            if (resourceId == null || !_catalog.Resources.ContainsKey(resourceId) || !state.IsUnlocked(resourceId))
                return Reject<bool>(RunReasons.ResourceLocked);
            if (!SettleFits(state.Cash, enabled ? resourceId : null, enabled ? null : resourceId, null, null))
                return Reject<bool>(RunReasons.ValueLimit);

            bool changed = state.IsAutoSell(resourceId) != enabled;
            _session.BeginCommand();
            RunStateResult set = state.TrySetAutoSell(resourceId, enabled);
            if (set != RunStateResult.Ok) return Reject<bool>(RunReasons.From(set));
            return _session.EndCommand(enabled, changed);
        }

        // ---------------------------------------------------------------------------------------------------
        // Expansion
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Raises the storage to the next level. Rejected with level.max, cash.insufficient or value.limit. The cost
        /// is the level table cost after the <c>upgradeCost</c> <c>expansion</c> modifiers; it is added to the
        /// invested storage Cash (Factory Value counts it).
        /// </summary>
        public RunCommandResult<ExpansionResult> UpgradeStorage()
        {
            if (_session.IsLifecycleBlocked) return Reject<ExpansionResult>(RunReasons.LifecycleBlocked);
            RunState state = State;
            if (state.StorageLevel >= _catalog.StorageLevels.Count) return Reject<ExpansionResult>(RunReasons.LevelMax);
            StorageLevelDef next = _catalog.StorageLevels[state.StorageLevel];
            if (!_modifiers.TryUpgradeCost(state, Permanent, next.Cost, null, out long cost)) return Reject<ExpansionResult>(RunReasons.ValueLimit);
            if (state.Cash < cost) return Reject<ExpansionResult>(RunReasons.CashInsufficient);
            if (!CheckedMath.TryAdd(state.StorageInvestedCash, cost, out _) || !SettleFits(state.Cash - cost, null, null, null, null))
                return Reject<ExpansionResult>(RunReasons.ValueLimit);

            _session.BeginCommand();
            RunStateResult upgraded = state.TryUpgradeStorage(cost);
            if (upgraded != RunStateResult.Ok) return Reject<ExpansionResult>(RunReasons.From(upgraded));
            bool hasNext = state.StorageLevel < _catalog.StorageLevels.Count;
            long nextCost = 0;
            if (hasNext) _modifiers.TryUpgradeCost(state, Permanent, _catalog.StorageLevels[state.StorageLevel].Cost, null, out nextCost);
            return _session.EndCommand(new ExpansionResult(state.StorageLevel, cost, next.Capacity, hasNext, nextCost), true);
        }

        /// <summary>
        /// Raises the factory to the next level, which opens slots. Rejected with level.max, cash.insufficient or
        /// value.limit. The cost is the level table cost after the <c>upgradeCost</c> <c>expansion</c> modifiers; it is
        /// added to the invested factory Cash.
        /// </summary>
        public RunCommandResult<ExpansionResult> UpgradeFactory()
        {
            if (_session.IsLifecycleBlocked) return Reject<ExpansionResult>(RunReasons.LifecycleBlocked);
            RunState state = State;
            if (state.FactoryLevel >= _catalog.FactoryLevels.Count) return Reject<ExpansionResult>(RunReasons.LevelMax);
            FactoryLevelDef next = _catalog.FactoryLevels[state.FactoryLevel];
            if (!_modifiers.TryUpgradeCost(state, Permanent, next.Cost, null, out long cost)) return Reject<ExpansionResult>(RunReasons.ValueLimit);
            if (state.Cash < cost) return Reject<ExpansionResult>(RunReasons.CashInsufficient);
            if (!CheckedMath.TryAdd(state.FactoryInvestedCash, cost, out _) || !SettleFits(state.Cash - cost, null, null, null, null))
                return Reject<ExpansionResult>(RunReasons.ValueLimit);

            _session.BeginCommand();
            RunStateResult upgraded = state.TryUpgradeFactory(cost);
            if (upgraded != RunStateResult.Ok) return Reject<ExpansionResult>(RunReasons.From(upgraded));
            bool hasNext = state.FactoryLevel < _catalog.FactoryLevels.Count;
            long nextCost = 0;
            if (hasNext) _modifiers.TryUpgradeCost(state, Permanent, _catalog.FactoryLevels[state.FactoryLevel].Cost, null, out nextCost);
            return _session.EndCommand(new ExpansionResult(state.FactoryLevel, cost, next.SlotCount, hasNext, nextCost), true);
        }

        // ---------------------------------------------------------------------------------------------------
        // Settle pre-check
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns false when the settle that follows a command could overflow Cash, so the command can be rejected
        /// with value.limit BEFORE it mutates anything (a rejection never changes state; the T099 review, item 1).
        ///
        /// The settle (R01 steps 3-5 at the command timestamp) can only add to Cash through auto-sales. It sells
        /// the stock that is there, and it can complete a machine that is already waiting at full progress (progress
        /// below the cycle cannot reach it at offset 0), once each. So for every auto-sell resource <c>r</c> the units
        /// ever sold at that instant are at most <c>stock(r)</c> plus, per waiting machine, its batch factor times
        /// the output amount times the output factor; each machine's batch is bounded by its composed speed after the
        /// command. Sales floor per sale, so one sale of that bound is never less than the sum of the real sales.
        /// The bound is <c>cashAfter</c> plus the sale income of those units, checked with <see cref="CheckedMath"/>;
        /// it is exact for the checks it guards (Cash beyond 10^18 minus a worst-case sale) and never optimistic.
        /// It errs on the side of rejecting, and only when Cash is within one worst-case sale of the 10^18 bound.
        /// </summary>
        /// <param name="cashAfter">Cash after the command's own change.</param>
        /// <param name="enabling">A resource the command turns auto-sell on for (its stock is sold), or null.</param>
        /// <param name="disabling">A resource the command turns auto-sell off for, or null.</param>
        /// <param name="skip">A machine the command removes or replaces, or null.</param>
        /// <param name="replacement">The machine that takes the place of <paramref name="skip"/> (an upgrade), or null.</param>
        /// <param name="consumedResource">A resource whose stock the command consumes (a contract delivery), or null.</param>
        /// <param name="consumedUnits">The units of <paramref name="consumedResource"/> the command consumes.</param>
        /// <param name="overdriveAfter">Whether Overdrive is active after the command (ActivateOverdrive); null keeps the current status.</param>
        private bool SettleFits(long cashAfter, string enabling, string disabling, MachineInstance skip, MachineInstance replacement,
            string consumedResource = null, long consumedUnits = 0, bool? overdriveAfter = null)
        {
            RunState state = State;
            bool overdrive = overdriveAfter ?? OverdriveActive;
            long total = cashAfter;
            IReadOnlyList<string> autoSell = state.AutoSell;
            int extra = enabling != null && !state.IsAutoSell(enabling) ? 1 : 0;
            for (int s = 0; s < autoSell.Count + extra; s++)
            {
                string resourceId = s < autoSell.Count ? autoSell[s] : enabling;
                if (disabling != null && string.Equals(resourceId, disabling, StringComparison.Ordinal)) continue;
                if (!_catalog.Resources.TryGetValue(resourceId, out ResourceDef resource)) return false;

                long units = state.Inventory.Units(resourceId);
                if (consumedResource != null && string.Equals(resourceId, consumedResource, StringComparison.Ordinal)) units -= consumedUnits;
                IReadOnlyList<MachineInstance> machines = state.Machines;
                for (int i = 0; i <= machines.Count; i++)
                {
                    MachineInstance m = i < machines.Count ? machines[i] : replacement;
                    if (m == null || (i < machines.Count && ReferenceEquals(m, skip))) continue;
                    if (!m.IsWaiting) continue;
                    if (!RunDerivation.TryDerive(_catalog, _modifiers, state, Permanent, m, overdrive, out RunDerivation.Derived d)) return false;
                    for (int o = 0; o < d.Recipe.Outputs.Count; o++)
                    {
                        if (!string.Equals(d.Recipe.Outputs[o].ResourceId, resourceId, StringComparison.Ordinal)) continue;
                        if (!CheckedMath.TryMultiply(d.Batch, d.Recipe.Outputs[o].Amount, out long perBatch) ||
                            !CheckedMath.TryMultiply(perBatch, d.OutputFactor, out long produced) ||
                            !CheckedMath.TryAdd(units, produced, out units))
                            return false;
                    }
                }

                if (!_modifiers.TrySalePricePpm(state, Permanent, resourceId, out long ppm) ||
                    !CheckedMath.TryMultiply(units, resource.BasePrice, out long gross) ||
                    !MulDiv.MulDivFloor(gross, ppm, SpeedComposer.Ppm, out long income) ||
                    !CheckedMath.TryAdd(total, income, out total))
                    return false;
            }
            return true;
        }
    }
}
