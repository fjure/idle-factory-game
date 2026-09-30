using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Run.Application
{
    // -------------------------------------------------------------------------------------------------------
    // Shared value types
    // -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A resource flow of one machine (or one resource total) at the composed speed. All rates are exact, in
    /// millionths of a unit per minute (<see cref="PerMinuteMicro"/>, floored, clamped to the exact bound), and
    /// <see cref="PerMinuteText"/> is the same value floored to three significant digits (R06, UX-8).
    /// </summary>
    public readonly struct ResourceRate
    {
        public string ResourceId { get; }

        /// <summary>Units exchanged by one cycle: the batch factor times the recipe amount (times the output factor for outputs).</summary>
        public long UnitsPerCycle { get; }

        public long PerMinuteMicro { get; }
        public string PerMinuteText => RateFormat.Format(PerMinuteMicro);

        public ResourceRate(string resourceId, long unitsPerCycle, long perMinuteMicro)
        {
            ResourceId = resourceId;
            UnitsPerCycle = unitsPerCycle;
            PerMinuteMicro = perMinuteMicro;
        }
    }

    /// <summary>The steady-state flows of one machine: its batch factor, cycle and the rate of every input and output.</summary>
    public sealed class MachineRates
    {
        public long BatchFactor { get; }
        public long CycleTicks { get; }
        public IReadOnlyList<ResourceRate> Inputs { get; }
        public IReadOnlyList<ResourceRate> Outputs { get; }

        internal MachineRates(long batchFactor, long cycleTicks, ResourceRate[] inputs, ResourceRate[] outputs)
        {
            BatchFactor = batchFactor;
            CycleTicks = cycleTicks;
            Inputs = new ReadOnlyCollection<ResourceRate>(inputs);
            Outputs = new ReadOnlyCollection<ResourceRate>(outputs);
        }
    }

    /// <summary>What a corrective action of a blocker does (FR-011). The list offers choices; it never picks one.</summary>
    public enum CorrectiveActionKind
    {
        /// <summary>Switch the machine on (<see cref="CorrectiveAction.InstanceId"/>).</summary>
        ActivateMachine = 0,

        /// <summary>Stop auto-selling <see cref="CorrectiveAction.ResourceId"/>, so the stock stays for the machine.</summary>
        DisableAutoSell = 1,

        /// <summary>Auto-sell <see cref="CorrectiveAction.ResourceId"/>, which frees store space.</summary>
        EnableAutoSell = 2,

        /// <summary>Build a machine type whose recipe outputs <see cref="CorrectiveAction.ResourceId"/> (see <see cref="CorrectiveAction.MachineId"/>).</summary>
        BuildProducer = 3,

        /// <summary>Upgrade the storage.</summary>
        UpgradeStorage = 4,
    }

    public readonly struct CorrectiveAction
    {
        public CorrectiveActionKind Kind { get; }
        public string ResourceId { get; }
        public string MachineId { get; }
        public Guid InstanceId { get; }

        public CorrectiveAction(CorrectiveActionKind kind, string resourceId = null, string machineId = null, Guid instanceId = default)
        {
            Kind = kind;
            ResourceId = resourceId;
            MachineId = machineId;
            InstanceId = instanceId;
        }
    }

    /// <summary>
    /// One blocker of a machine. <see cref="Kind"/> is <see cref="MachineBlocker.Inactive"/>,
    /// <see cref="MachineBlocker.MissingInput"/> (with <see cref="ResourceId"/>, <see cref="Required"/> the amount one
    /// recipe unit needs, <see cref="Available"/> the stock and <see cref="AutoSold"/>, which names auto-sell as the
    /// cause, UX-10) or <see cref="MachineBlocker.StorageFull"/> (with <see cref="NetRequired"/>, the net units one
    /// recipe unit adds to the store, and <see cref="Free"/>, the free space that is too small, OD-9).
    /// </summary>
    public sealed class MachineBlockerInfo
    {
        public MachineBlocker Kind { get; }
        public string ResourceId { get; }
        public long Required { get; }
        public long Available { get; }
        public bool AutoSold { get; }
        public long NetRequired { get; }
        public long Free { get; }
        public IReadOnlyList<CorrectiveAction> Actions { get; }

        internal MachineBlockerInfo(MachineBlocker kind, string resourceId, long required, long available, bool autoSold,
            long netRequired, long free, CorrectiveAction[] actions)
        {
            Kind = kind;
            ResourceId = resourceId;
            Required = required;
            Available = available;
            AutoSold = autoSold;
            NetRequired = netRequired;
            Free = free;
            Actions = new ReadOnlyCollection<CorrectiveAction>(actions);
        }
    }

    public enum CompletionLimitKind
    {
        /// <summary>A full batch: nothing limited it.</summary>
        None = 0,

        /// <summary>An input limited the batch (<see cref="MachineCompletion.LimitResourceId"/>).</summary>
        Input = 1,

        /// <summary>The store limited the batch.</summary>
        Store = 2,
    }

    /// <summary>
    /// The last completion of a machine this session saw (OD-9): <see cref="Units"/> is <c>k'</c> of
    /// <see cref="BatchFactor"/> (<c>k</c>), and for a partial batch the limiting input or the store. It is derived
    /// data and empty (<see cref="HasCompleted"/> false) until the machine completes after the session started.
    /// </summary>
    public readonly struct MachineCompletion
    {
        public bool HasCompleted { get; }
        public long Units { get; }
        public long BatchFactor { get; }
        public bool IsPartial => HasCompleted && Units < BatchFactor;
        public CompletionLimitKind LimitKind { get; }
        public string LimitResourceId { get; }

        public MachineCompletion(long units, long batchFactor, CompletionLimitKind limitKind, string limitResourceId)
        {
            HasCompleted = true;
            Units = units;
            BatchFactor = batchFactor;
            LimitKind = limitKind;
            LimitResourceId = limitResourceId;
        }
    }

    /// <summary>
    /// Three-significant-digit display of a rate held in millionths of a unit per minute (R06): floored, so a shown
    /// rate never overstates the exact one, with the suffixes K, M, B, T, Qa, Qi from 1,000 units and scientific
    /// notation beyond. Trailing fraction zeros are dropped ("15", "4.68", "0.0625", "1.23K").
    /// </summary>
    public static class RateFormat
    {
        private static readonly string[] Suffixes = { "K", "M", "B", "T", "Qa", "Qi" };

        public static string Format(long perMinuteMicro)
        {
            if (perMinuteMicro == 0) return "0";
            bool negative = perMinuteMicro < 0;
            ulong v = negative ? unchecked(0UL - (ulong)perMinuteMicro) : (ulong)perMinuteMicro;
            int digits = 1;
            for (ulong t = v; t >= 10; t /= 10) digits++;
            ulong mantissa = v;
            for (int i = digits; i > 3; i--) mantissa /= 10;
            for (int i = digits; i < 3; i++) mantissa *= 10;
            string three = mantissa.ToString(System.Globalization.CultureInfo.InvariantCulture);
            int exponent = digits - 1 - 6; // power of ten of the leading digit, in units per minute

            string body;
            if (exponent < 0)
            {
                body = Trim("0." + new string('0', -exponent - 1) + three);
            }
            else
            {
                int tier = exponent / 3;
                if (tier > Suffixes.Length)
                {
                    body = Trim(three.Substring(0, 1) + "." + three.Substring(1)) + "e" + exponent.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    int integerDigits = exponent - 3 * tier + 1;
                    body = Trim(three.Substring(0, integerDigits) + "." + three.Substring(integerDigits)) + (tier == 0 ? string.Empty : Suffixes[tier - 1]);
                }
            }
            return negative ? "-" + body : body;
        }

        private static string Trim(string decimalText)
        {
            int end = decimalText.Length;
            while (end > 0 && decimalText[end - 1] == '0') end--;
            if (end > 0 && decimalText[end - 1] == '.') end--;
            return decimalText.Substring(0, end);
        }
    }

    // -------------------------------------------------------------------------------------------------------
    // Derivation shared by the commands and the snapshots
    // -------------------------------------------------------------------------------------------------------

    internal static class RunDerivation
    {
        /// <summary>Millionths of a unit per minute per (unit per cycle x tick): 60 s x 10^7 ticks x 10^6.</summary>
        private const long MicroTicksPerMinute = 600_000_000_000_000L;

        internal struct Derived
        {
            public MachineDef Def;
            public MachineRecipe Recipe;
            public long Speed;
            public long Batch;
            public long Cycle;
            public long OutputFactor;
        }

        /// <summary>Composed speed, batch factor, cycle and output factor of a machine now; false on a checked overflow.</summary>
        internal static bool TryDerive(RunCatalog catalog, RunModifiers modifiers, RunState state, PermanentState permanent,
            MachineInstance machine, bool overdriveActive, out Derived derived)
        {
            derived = default;
            if (!catalog.Machines.TryGetValue(machine.MachineId, out MachineDef def) ||
                !catalog.Recipes.TryGetValue(def.RecipeId, out MachineRecipe recipe) ||
                !modifiers.TryMachineSpeed(state, permanent, machine, overdriveActive, null, out long speed) ||
                !SpeedComposer.TryBatch(recipe.DurationTicks, speed, catalog.RunStart.MinCycleTicks, out long batch, out long cycle) ||
                !modifiers.TryOutputFactor(state, machine, null, out long factor))
                return false;
            derived = new Derived { Def = def, Recipe = recipe, Speed = speed, Batch = batch, Cycle = cycle, OutputFactor = factor };
            return true;
        }

        /// <summary>Units per minute in millionths, floored; a value beyond the exact bound clamps to it.</summary>
        internal static long RateMicro(long unitsPerCycle, long cycleTicks)
        {
            if (cycleTicks < 1) return 0;
            return MulDiv.MulDivFloor(unitsPerCycle, MicroTicksPerMinute, cycleTicks, out long rate) ? rate : ExactBound.Value;
        }

        internal static MachineRates Rates(Derived d)
        {
            MachineRecipe recipe = d.Recipe;
            var inputs = new ResourceRate[recipe.Inputs.Count];
            for (int i = 0; i < inputs.Length; i++)
            {
                long units = Saturate(d.Batch, recipe.Inputs[i].Amount, 1);
                inputs[i] = new ResourceRate(recipe.Inputs[i].ResourceId, units, RateMicro(units, d.Cycle));
            }
            var outputs = new ResourceRate[recipe.Outputs.Count];
            for (int i = 0; i < outputs.Length; i++)
            {
                long units = Saturate(d.Batch, recipe.Outputs[i].Amount, d.OutputFactor);
                outputs[i] = new ResourceRate(recipe.Outputs[i].ResourceId, units, RateMicro(units, d.Cycle));
            }
            return new MachineRates(d.Batch, d.Cycle, inputs, outputs);
        }

        private static long Saturate(long batch, long amount, long factor)
        {
            return CheckedMath.TryMultiply(batch, amount, out long a) && CheckedMath.TryMultiply(a, factor, out long b) ? b : ExactBound.Value;
        }

        /// <summary>
        /// The net units one recipe unit adds to the store, <c>outputs x factor - inputs</c> (OD-9); a checked
        /// overflow returns false.
        /// </summary>
        internal static bool TryNetPerUnit(MachineRecipe recipe, long outputFactor, out long net)
        {
            net = 0;
            long outputs = 0, inputs = 0;
            for (int i = 0; i < recipe.Outputs.Count; i++)
            {
                if (!CheckedMath.TryMultiply(recipe.Outputs[i].Amount, outputFactor, out long produced) ||
                    !CheckedMath.TryAdd(outputs, produced, out outputs))
                    return false;
            }
            for (int i = 0; i < recipe.Inputs.Count; i++)
                if (!CheckedMath.TryAdd(inputs, recipe.Inputs[i].Amount, out inputs)) return false;
            net = outputs - inputs;
            return true;
        }

        /// <summary>The milestone that the given level introduces, or null.</summary>
        internal static SpeedMilestone MilestoneAt(MachineDef def, int level)
        {
            for (int i = 0; i < def.Milestones.Count; i++)
                if (def.Milestones[i].Level == level) return def.Milestones[i];
            return null;
        }
    }

    // -------------------------------------------------------------------------------------------------------
    // RunFactorySnapshot (GD-2)
    // -------------------------------------------------------------------------------------------------------

    public enum SlotStatus
    {
        Empty = 0,
        Occupied = 1,

        /// <summary>Beyond the current factory level; <see cref="SlotSnapshot.UnlocksAtFactoryLevel"/> names the level that opens it.</summary>
        Locked = 2,
    }

    public sealed class SlotSnapshot
    {
        public int Index { get; }
        public SlotStatus Status { get; }

        /// <summary>Index into <see cref="RunFactorySnapshot.Machines"/> for an occupied slot, otherwise -1.</summary>
        public int MachineIndex { get; }

        /// <summary>The factory level that opens a locked slot, otherwise 0.</summary>
        public int UnlocksAtFactoryLevel { get; }

        internal SlotSnapshot(int index, SlotStatus status, int machineIndex, int unlocksAtFactoryLevel)
        {
            Index = index;
            Status = status;
            MachineIndex = machineIndex;
            UnlocksAtFactoryLevel = unlocksAtFactoryLevel;
        }
    }

    /// <summary>The result an upgrade would have (FR-009, US1 scenario 5), shown before the tap.</summary>
    public sealed class UpgradePreview
    {
        /// <summary>False at the maximum level.</summary>
        public bool Available { get; }
        public int NewLevel { get; }
        public long Cost { get; }
        public bool Affordable { get; }

        /// <summary><see cref="RunReasons.LevelMax"/>, <see cref="RunReasons.CashInsufficient"/> or null.</summary>
        public string RejectionReason { get; }

        public MachineRates NewRates { get; }
        public bool ReachesMilestone { get; }
        public int MilestoneLevel { get; }
        public long MilestoneMultiplierPpm { get; }

        internal UpgradePreview(bool available, int newLevel, long cost, bool affordable, string rejectionReason,
            MachineRates newRates, bool reachesMilestone, int milestoneLevel, long milestoneMultiplierPpm)
        {
            Available = available;
            NewLevel = newLevel;
            Cost = cost;
            Affordable = affordable;
            RejectionReason = rejectionReason;
            NewRates = newRates;
            ReachesMilestone = reachesMilestone;
            MilestoneLevel = milestoneLevel;
            MilestoneMultiplierPpm = milestoneMultiplierPpm;
        }
    }

    public sealed class MachineSnapshot
    {
        public Guid InstanceId { get; }
        public string MachineId { get; }
        public string Label { get; }
        public int TypeIndex { get; }
        public int SlotIndex { get; }
        public int Level { get; }
        public int MaxLevel { get; }
        public bool IsActive { get; }
        public MachinePhase Phase { get; }
        public MachineBlocker Blocker { get; }
        public long ProgressPpm { get; }

        /// <summary>Position in the priority order, 0 first.</summary>
        public int PriorityPosition { get; }

        /// <summary>The Cash paid for the machine and its upgrades.</summary>
        public long InvestedCash { get; }

        /// <summary>What Remove would refund now: <c>floor(investedCash x refundPpm / 10^6)</c>.</summary>
        public long RefundOnRemove { get; }

        /// <summary>The Factory Value change of removing it, <c>refund - investedCash</c> (never positive; ECO-7).</summary>
        public long FactoryValueDeltaOnRemove { get; }

        /// <summary>The steady-state flows now, or null when the composed speed overflowed.</summary>
        public MachineRates Rates { get; }

        public UpgradePreview NextUpgrade { get; }

        internal MachineSnapshot(Guid instanceId, MachineDef def, int typeIndex, MachineInstance machine, MachineStateView view,
            int priorityPosition, long refund, MachineRates rates, UpgradePreview nextUpgrade)
        {
            InstanceId = instanceId;
            MachineId = def.Id;
            Label = def.Label;
            TypeIndex = typeIndex;
            SlotIndex = machine.SlotIndex;
            Level = machine.Level;
            MaxLevel = machine.MaxLevel;
            IsActive = machine.IsActive;
            Phase = view.Phase;
            Blocker = view.Blocker;
            ProgressPpm = view.ProgressPpm;
            PriorityPosition = priorityPosition;
            InvestedCash = machine.InvestedCash;
            RefundOnRemove = refund;
            FactoryValueDeltaOnRemove = refund - machine.InvestedCash;
            Rates = rates;
            NextUpgrade = nextUpgrade;
        }
    }

    /// <summary>A machine type as the build sheet shows it: unlocked or not, its cost, and what it makes at level 1.</summary>
    public sealed class MachineTypeSnapshot
    {
        public string MachineId { get; }
        public string Label { get; }
        public int TypeIndex { get; }
        public bool Unlocked { get; }

        /// <summary>The unlock condition kind (<c>always</c>, <c>researchPurchased</c>, <c>factoryLevelAtLeast</c>, <c>resourceProduced</c>) for a locked type's copy.</summary>
        public string UnlockKind { get; }
        public string UnlockId { get; }
        public int UnlockLevel { get; }

        /// <summary>The level-1 cost after the <c>upgradeCost</c> modifiers.</summary>
        public long BuildCost { get; }
        public bool Affordable { get; }

        /// <summary><see cref="RunReasons.MachineLocked"/>, <see cref="RunReasons.CashInsufficient"/> or null (slot rules aside).</summary>
        public string RejectionReason { get; }

        public MachineRates Level1Rates { get; }

        internal MachineTypeSnapshot(MachineDef def, int typeIndex, bool unlocked, long buildCost, bool affordable,
            string rejectionReason, MachineRates rates)
        {
            MachineId = def.Id;
            Label = def.Label;
            TypeIndex = typeIndex;
            Unlocked = unlocked;
            UnlockKind = def.Unlock.Kind;
            UnlockId = def.Unlock.Id;
            UnlockLevel = def.Unlock.Level;
            BuildCost = buildCost;
            Affordable = affordable;
            RejectionReason = rejectionReason;
            Level1Rates = rates;
        }
    }

    /// <summary>One expansion track (storage or factory): its level, what the next level gives and what it costs.</summary>
    public sealed class ExpansionSnapshot
    {
        public int Level { get; }
        public int MaxLevel { get; }

        /// <summary>Storage capacity or slot count now.</summary>
        public long CurrentValue { get; }
        public bool HasNext { get; }

        /// <summary>Capacity or slot count of the next level (0 at the maximum).</summary>
        public long NextValue { get; }

        /// <summary>The next level's cost after the <c>upgradeCost</c> modifiers (0 at the maximum).</summary>
        public long NextCost { get; }
        public bool Affordable { get; }
        public long InvestedCash { get; }

        /// <summary><see cref="RunReasons.LevelMax"/>, <see cref="RunReasons.CashInsufficient"/> or null.</summary>
        public string RejectionReason { get; }

        internal ExpansionSnapshot(int level, int maxLevel, long currentValue, bool hasNext, long nextValue, long nextCost,
            bool affordable, long investedCash, string rejectionReason)
        {
            Level = level;
            MaxLevel = maxLevel;
            CurrentValue = currentValue;
            HasNext = hasNext;
            NextValue = nextValue;
            NextCost = nextCost;
            Affordable = affordable;
            InvestedCash = investedCash;
            RejectionReason = rejectionReason;
        }
    }

    /// <summary>
    /// Slots, machines, costs, next-upgrade results and unlocks (contracts/run-application.md; GD-2: the
    /// <c>Run</c> prefix avoids the legacy <c>FactorySnapshot</c> until R16). Immutable; built on demand.
    /// </summary>
    public sealed class RunFactorySnapshot
    {
        public long StructureRevision { get; }
        public long QuantityRevision { get; }
        public long Cash { get; }
        public int FactoryLevel { get; }
        public int SlotCount { get; }
        public IReadOnlyList<SlotSnapshot> Slots { get; }

        /// <summary>The machines in slot order.</summary>
        public IReadOnlyList<MachineSnapshot> Machines { get; }

        /// <summary>The priority order, highest first.</summary>
        public IReadOnlyList<Guid> Priority { get; }

        /// <summary>Every machine type of the catalog, in the order of <see cref="RunSession.MachineTypeId"/>.</summary>
        public IReadOnlyList<MachineTypeSnapshot> MachineTypes { get; }
        public ExpansionSnapshot Storage { get; }
        public ExpansionSnapshot Factory { get; }

        internal RunFactorySnapshot(long structureRevision, long quantityRevision, long cash, int factoryLevel, int slotCount,
            SlotSnapshot[] slots, MachineSnapshot[] machines, Guid[] priority, MachineTypeSnapshot[] types,
            ExpansionSnapshot storage, ExpansionSnapshot factory)
        {
            StructureRevision = structureRevision;
            QuantityRevision = quantityRevision;
            Cash = cash;
            FactoryLevel = factoryLevel;
            SlotCount = slotCount;
            Slots = new ReadOnlyCollection<SlotSnapshot>(slots);
            Machines = new ReadOnlyCollection<MachineSnapshot>(machines);
            Priority = new ReadOnlyCollection<Guid>(priority);
            MachineTypes = new ReadOnlyCollection<MachineTypeSnapshot>(types);
            Storage = storage;
            Factory = factory;
        }
    }

    // -------------------------------------------------------------------------------------------------------
    // RatesSnapshot (FR-011, UX-10, OD-9)
    // -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Production, consumption and net rate of one resource. The rates are steady-state rates of the ACTIVE machines
    /// at their composed speed (batches included), assuming their inputs and store space are there: a blocked
    /// machine still counts, and its blocker is listed on <see cref="MachineDiagnosis"/>, so a bottleneck is
    /// visible as a supply coverage below 100 % (the Press supplies 62.5 % of an Assembler's Plate demand).
    /// <see cref="NetPerMinuteMicro"/> is production minus consumption, before auto-sales.
    /// </summary>
    public sealed class ResourceRates
    {
        public string ResourceId { get; }
        public long Units { get; }
        public bool AutoSold { get; }
        public long ProductionPerMinuteMicro { get; }
        public long ConsumptionPerMinuteMicro { get; }
        public long NetPerMinuteMicro { get; }
        public string ProductionText => RateFormat.Format(ProductionPerMinuteMicro);
        public string ConsumptionText => RateFormat.Format(ConsumptionPerMinuteMicro);
        public string NetText => RateFormat.Format(NetPerMinuteMicro);

        /// <summary>True when some active machine consumes the resource.</summary>
        public bool HasDemand { get; }

        /// <summary><c>floor(production x 10^6 / consumption)</c> when <see cref="HasDemand"/> (625,000 = 62.5 %), otherwise 0.</summary>
        public long SupplyCoveragePpm { get; }

        internal ResourceRates(string resourceId, long units, bool autoSold, long production, long consumption)
        {
            ResourceId = resourceId;
            Units = units;
            AutoSold = autoSold;
            ProductionPerMinuteMicro = production;
            ConsumptionPerMinuteMicro = consumption;
            NetPerMinuteMicro = production - consumption;
            HasDemand = consumption > 0;
            long coverage = 0;
            if (HasDemand && !MulDiv.MulDivFloor(production, 1_000_000, consumption, out coverage)) coverage = ExactBound.Value;
            SupplyCoveragePpm = coverage;
        }
    }

    /// <summary>The diagnosis of one machine: its state, its blockers with corrective actions, and its last completion.</summary>
    public sealed class MachineDiagnosis
    {
        public Guid InstanceId { get; }
        public string MachineId { get; }
        public int SlotIndex { get; }
        public int Level { get; }
        public bool IsActive { get; }
        public MachinePhase Phase { get; }

        /// <summary>The single blocker of the machine read model (<see cref="MachineStateView.Blocker"/>).</summary>
        public MachineBlocker PrimaryBlocker { get; }
        public long BatchFactor { get; }

        /// <summary>The steady-state flows, or null when the composed speed overflowed.</summary>
        public MachineRates Rates { get; }

        /// <summary>Every current blocker, in the order Inactive, MissingInput per short input, StorageFull.</summary>
        public IReadOnlyList<MachineBlockerInfo> Blockers { get; }

        /// <summary>The last completed <c>k'</c> of <c>k</c> and its limit (OD-9).</summary>
        public MachineCompletion LastCompletion { get; }

        internal MachineDiagnosis(MachineInstance machine, MachineStateView view, MachineRates rates,
            MachineBlockerInfo[] blockers, MachineCompletion lastCompletion)
        {
            InstanceId = machine.InstanceId;
            MachineId = machine.MachineId;
            SlotIndex = machine.SlotIndex;
            Level = machine.Level;
            IsActive = machine.IsActive;
            Phase = view.Phase;
            PrimaryBlocker = view.Blocker;
            BatchFactor = view.BatchFactor;
            Rates = rates;
            Blockers = new ReadOnlyCollection<MachineBlockerInfo>(blockers);
            LastCompletion = lastCompletion;
        }
    }

    public sealed class RatesSnapshot
    {
        public long StructureRevision { get; }
        public long QuantityRevision { get; }
        public long StoreUsed { get; }
        public long StoreCapacity { get; }
        public long StoreFree => StoreCapacity - StoreUsed;

        /// <summary>Store usage in ppm, <c>floor(used x 10^6 / capacity)</c>.</summary>
        public long StoreUsagePpm { get; }

        /// <summary>The unlocked resources in inventory order (locked resources are hidden).</summary>
        public IReadOnlyList<ResourceRates> Resources { get; }

        /// <summary>The machines in slot order.</summary>
        public IReadOnlyList<MachineDiagnosis> Machines { get; }

        internal RatesSnapshot(long structureRevision, long quantityRevision, long used, long capacity, long usagePpm,
            ResourceRates[] resources, MachineDiagnosis[] machines)
        {
            StructureRevision = structureRevision;
            QuantityRevision = quantityRevision;
            StoreUsed = used;
            StoreCapacity = capacity;
            StoreUsagePpm = usagePpm;
            Resources = new ReadOnlyCollection<ResourceRates>(resources);
            Machines = new ReadOnlyCollection<MachineDiagnosis>(machines);
        }
    }

    // -------------------------------------------------------------------------------------------------------
    // EconomySnapshot
    // -------------------------------------------------------------------------------------------------------

    public sealed class ResourceEconomy
    {
        public string ResourceId { get; }
        public string Label { get; }
        public bool Unlocked { get; }
        public long BasePrice { get; }

        /// <summary>The effective price multiplier in ppm (10^6 without modifiers); 0 when <see cref="PriceValid"/> is false.</summary>
        public long SalePricePpm { get; }

        /// <summary>The Cash one unit sells for, <c>floor(basePrice x pricePpm / 10^6)</c>; bulk sales floor once per sale.</summary>
        public long UnitPrice { get; }
        public bool PriceValid { get; }
        public long Units { get; }

        /// <summary>The Cash a sale of the whole stock would pay.</summary>
        public long StockValue { get; }
        public bool AutoSell { get; }

        /// <summary>The auto-sell toggle is enabled only for an unlocked resource.</summary>
        public bool CanToggleAutoSell => Unlocked;

        internal ResourceEconomy(ResourceDef def, bool unlocked, long pricePpm, long unitPrice, bool priceValid, long units,
            long stockValue, bool autoSell)
        {
            ResourceId = def.Id;
            Label = def.Label;
            Unlocked = unlocked;
            BasePrice = def.BasePrice;
            SalePricePpm = pricePpm;
            UnitPrice = unitPrice;
            PriceValid = priceValid;
            Units = units;
            StockValue = stockValue;
            AutoSell = autoSell;
        }
    }

    /// <summary>Prices and auto-sell settings of every resource of the catalog in ordinal ID order.</summary>
    public sealed class EconomySnapshot
    {
        public long StructureRevision { get; }
        public long QuantityRevision { get; }
        public long Cash { get; }
        public IReadOnlyList<ResourceEconomy> Resources { get; }

        internal EconomySnapshot(long structureRevision, long quantityRevision, long cash, ResourceEconomy[] resources)
        {
            StructureRevision = structureRevision;
            QuantityRevision = quantityRevision;
            Cash = cash;
            Resources = new ReadOnlyCollection<ResourceEconomy>(resources);
        }
    }

    // -------------------------------------------------------------------------------------------------------
    // Builders
    // -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Builds the immutable read models on demand (contracts/run-application.md, Read models). Later stories add
    /// their snapshots to this partial class. Nothing here mutates the session.
    /// </summary>
    public static partial class RunSnapshots
    {
        public static RunFactorySnapshot Factory(RunSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            RunCatalog catalog = session.Catalog;
            RunState state = session.State;
            PermanentState permanent = session.Permanent;
            var modifiers = new RunModifiers(catalog);
            bool overdrive = state.Overdrive.IsActive(session.TimelineUtcMs);

            int count = state.Machines.Count;
            var views = new MachineStateView[count];
            session.CopyMachineStates(views);

            var machines = new MachineSnapshot[count];
            for (int i = 0; i < count; i++)
            {
                MachineInstance m = state.Machines[i];
                MachineDef def = catalog.Machines[m.MachineId];
                long refund = 0;
                MulDiv.MulDivFloor(m.InvestedCash, catalog.RunStart.RemovalRefundPpm, 1_000_000, out refund);
                MachineRates rates = RunDerivation.TryDerive(catalog, modifiers, state, permanent, m, overdrive, out RunDerivation.Derived d)
                    ? RunDerivation.Rates(d)
                    : null;
                UpgradePreview next = PreviewUpgrade(catalog, modifiers, state, permanent, m, def, overdrive);
                machines[i] = new MachineSnapshot(m.InstanceId, def, TypeIndexOf(session, m.MachineId), m, views[i],
                    state.Priority.IndexOf(m.InstanceId), refund, rates, next);
            }

            int maxSlots = 0;
            for (int i = 0; i < catalog.FactoryLevels.Count; i++)
                if (catalog.FactoryLevels[i].SlotCount > maxSlots) maxSlots = catalog.FactoryLevels[i].SlotCount;
            var slots = new SlotSnapshot[maxSlots];
            for (int s = 0; s < maxSlots; s++)
            {
                if (s < state.SlotCount)
                {
                    int at = -1;
                    for (int i = 0; i < count; i++)
                        if (state.Machines[i].SlotIndex == s) at = i;
                    slots[s] = new SlotSnapshot(s, at >= 0 ? SlotStatus.Occupied : SlotStatus.Empty, at, 0);
                }
                else
                {
                    int level = 0;
                    for (int l = 0; l < catalog.FactoryLevels.Count && level == 0; l++)
                        if (catalog.FactoryLevels[l].SlotCount > s) level = catalog.FactoryLevels[l].Level;
                    slots[s] = new SlotSnapshot(s, SlotStatus.Locked, -1, level);
                }
            }

            var types = new MachineTypeSnapshot[session.MachineTypeCount];
            for (int t = 0; t < types.Length; t++)
            {
                MachineDef def = catalog.Machines[session.MachineTypeId(t)];
                bool unlocked = state.IsUnlocked(def.Id);
                long cost = 0;
                bool costOk = modifiers.TryUpgradeCost(state, permanent, def.CostTable[0], def.Id, out cost);
                var probe = new MachineInstance(new Guid(1, 0, 0, new byte[8]), def, 0, 1, true, 0,
                    catalog.Recipes[def.RecipeId].DurationTicks, 0);
                MachineRates rates = RunDerivation.TryDerive(catalog, modifiers, state, permanent, probe, overdrive, out RunDerivation.Derived d)
                    ? RunDerivation.Rates(d)
                    : null;
                string reason = !unlocked ? RunReasons.MachineLocked : !costOk ? RunReasons.ValueLimit : state.Cash < cost ? RunReasons.CashInsufficient : null;
                types[t] = new MachineTypeSnapshot(def, t, unlocked, cost, costOk && state.Cash >= cost, reason, rates);
            }

            ExpansionSnapshot storage = StorageTrack(catalog, modifiers, state, permanent);
            ExpansionSnapshot factory = FactoryTrack(catalog, modifiers, state, permanent);
            return new RunFactorySnapshot(session.StructureRevision, session.QuantityRevision, state.Cash, state.FactoryLevel,
                state.SlotCount, slots, machines, state.Priority.ToArray(), types, storage, factory);
        }

        public static EconomySnapshot Economy(RunSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            RunCatalog catalog = session.Catalog;
            RunState state = session.State;
            var modifiers = new RunModifiers(catalog);
            var ids = new List<string>(catalog.Resources.Keys);
            ids.Sort(StringComparer.Ordinal);
            var resources = new ResourceEconomy[ids.Count];
            for (int i = 0; i < resources.Length; i++)
            {
                ResourceDef def = catalog.Resources[ids[i]];
                bool unlocked = state.IsUnlocked(def.Id);
                long units = state.Inventory.Units(def.Id);
                bool valid = modifiers.TrySalePricePpm(state, session.Permanent, def.Id, out long ppm);
                long unitPrice = 0, stockValue = 0;
                if (valid)
                {
                    MulDiv.MulDivFloor(def.BasePrice, ppm, 1_000_000, out unitPrice);
                    if (!CheckedMath.TryMultiply(units, def.BasePrice, out long gross) ||
                        !MulDiv.MulDivFloor(gross, ppm, 1_000_000, out stockValue))
                        stockValue = ExactBound.Value;
                }
                resources[i] = new ResourceEconomy(def, unlocked, valid ? ppm : 0, unitPrice, valid, units, stockValue,
                    state.IsAutoSell(def.Id));
            }
            return new EconomySnapshot(session.StructureRevision, session.QuantityRevision, state.Cash, resources);
        }

        public static RatesSnapshot Rates(RunSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            RunCatalog catalog = session.Catalog;
            RunState state = session.State;
            PermanentState permanent = session.Permanent;
            var modifiers = new RunModifiers(catalog);
            bool overdrive = state.Overdrive.IsActive(session.TimelineUtcMs);
            GlobalInventory stock = state.Inventory;

            int count = state.Machines.Count;
            var views = new MachineStateView[count];
            session.CopyMachineStates(views);

            var production = new long[stock.Count];
            var consumption = new long[stock.Count];
            var diagnoses = new MachineDiagnosis[count];
            for (int i = 0; i < count; i++)
            {
                MachineInstance m = state.Machines[i];
                MachineRates rates = null;
                MachineRecipe recipe = null;
                long outputFactor = 1;
                if (RunDerivation.TryDerive(catalog, modifiers, state, permanent, m, overdrive, out RunDerivation.Derived d))
                {
                    rates = RunDerivation.Rates(d);
                    recipe = d.Recipe;
                    outputFactor = d.OutputFactor;
                    if (m.IsActive)
                    {
                        for (int r = 0; r < rates.Inputs.Count; r++) Accumulate(consumption, stock, rates.Inputs[r]);
                        for (int r = 0; r < rates.Outputs.Count; r++) Accumulate(production, stock, rates.Outputs[r]);
                    }
                }
                else
                {
                    catalog.Recipes.TryGetValue(catalog.Machines[m.MachineId].RecipeId, out recipe);
                }

                diagnoses[i] = new MachineDiagnosis(m, views[i], rates,
                    Blockers(session, catalog, state, m, recipe, outputFactor),
                    LastCompletion(session, m, recipe));
            }

            var resources = new List<ResourceRates>(stock.Count);
            for (int i = 0; i < stock.Count; i++)
            {
                string id = stock.IdAt(i);
                if (!state.IsUnlocked(id)) continue;
                resources.Add(new ResourceRates(id, stock.UnitsAt(i), state.IsAutoSell(id), production[i], consumption[i]));
            }

            long usage = 0;
            MulDiv.MulDivFloor(stock.Total, 1_000_000, stock.Capacity, out usage);
            return new RatesSnapshot(session.StructureRevision, session.QuantityRevision, stock.Total, stock.Capacity, usage,
                resources.ToArray(), diagnoses);
        }

        // ---------------------------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------------------------

        private static int TypeIndexOf(RunSession session, string machineId)
        {
            for (int i = 0; i < session.MachineTypeCount; i++)
                if (string.Equals(session.MachineTypeId(i), machineId, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static void Accumulate(long[] totals, GlobalInventory stock, ResourceRate rate)
        {
            for (int i = 0; i < stock.Count; i++)
            {
                if (!string.Equals(stock.IdAt(i), rate.ResourceId, StringComparison.Ordinal)) continue;
                totals[i] = CheckedMath.TryAdd(totals[i], rate.PerMinuteMicro, out long sum) ? sum : ExactBound.Value;
                return;
            }
        }

        private static UpgradePreview PreviewUpgrade(RunCatalog catalog, RunModifiers modifiers, RunState state,
            PermanentState permanent, MachineInstance m, MachineDef def, bool overdrive)
        {
            if (m.Level >= m.MaxLevel)
                return new UpgradePreview(false, m.Level, 0, false, RunReasons.LevelMax, null, false, 0, 0);
            int newLevel = m.Level + 1;
            bool costOk = modifiers.TryUpgradeCost(state, permanent, def.CostTable[m.Level], def.Id, out long cost);
            var next = new MachineInstance(m.InstanceId, def, m.SlotIndex, newLevel, m.IsActive, 0,
                catalog.Recipes[def.RecipeId].DurationTicks, 0);
            MachineRates rates = RunDerivation.TryDerive(catalog, modifiers, state, permanent, next, overdrive, out RunDerivation.Derived d)
                ? RunDerivation.Rates(d)
                : null;
            SpeedMilestone milestone = RunDerivation.MilestoneAt(def, newLevel);
            bool affordable = costOk && state.Cash >= cost;
            string reason = !costOk ? RunReasons.ValueLimit : state.Cash < cost ? RunReasons.CashInsufficient : null;
            return new UpgradePreview(true, newLevel, cost, affordable, reason, rates, milestone != null,
                milestone?.Level ?? 0, milestone?.SpeedPpm ?? 0);
        }

        private static ExpansionSnapshot StorageTrack(RunCatalog catalog, RunModifiers modifiers, RunState state, PermanentState permanent)
        {
            int level = state.StorageLevel;
            int max = catalog.StorageLevels.Count;
            long current = catalog.StorageLevels[level - 1].Capacity;
            if (level >= max)
                return new ExpansionSnapshot(level, max, current, false, 0, 0, false, state.StorageInvestedCash, RunReasons.LevelMax);
            StorageLevelDef next = catalog.StorageLevels[level];
            bool ok = modifiers.TryUpgradeCost(state, permanent, next.Cost, null, out long cost);
            return new ExpansionSnapshot(level, max, current, true, next.Capacity, cost, ok && state.Cash >= cost, state.StorageInvestedCash,
                !ok ? RunReasons.ValueLimit : state.Cash < cost ? RunReasons.CashInsufficient : null);
        }

        private static ExpansionSnapshot FactoryTrack(RunCatalog catalog, RunModifiers modifiers, RunState state, PermanentState permanent)
        {
            int level = state.FactoryLevel;
            int max = catalog.FactoryLevels.Count;
            long current = catalog.FactoryLevels[level - 1].SlotCount;
            if (level >= max)
                return new ExpansionSnapshot(level, max, current, false, 0, 0, false, state.FactoryInvestedCash, RunReasons.LevelMax);
            FactoryLevelDef next = catalog.FactoryLevels[level];
            bool ok = modifiers.TryUpgradeCost(state, permanent, next.Cost, null, out long cost);
            return new ExpansionSnapshot(level, max, current, true, next.SlotCount, cost, ok && state.Cash >= cost, state.FactoryInvestedCash,
                !ok ? RunReasons.ValueLimit : state.Cash < cost ? RunReasons.CashInsufficient : null);
        }

        private static MachineCompletion LastCompletion(RunSession session, MachineInstance m, MachineRecipe recipe)
        {
            if (!session.TryGetLastCompletion(m.InstanceId, out long units, out long batch, out int limit))
                return default;
            if (limit >= 0 && recipe != null && limit < recipe.Inputs.Count)
                return new MachineCompletion(units, batch, CompletionLimitKind.Input, recipe.Inputs[limit].ResourceId);
            if (limit == RunSimulator.LimitStore) return new MachineCompletion(units, batch, CompletionLimitKind.Store, null);
            return new MachineCompletion(units, batch, CompletionLimitKind.None, null);
        }

        /// <summary>
        /// All current blockers of a machine (FR-011): Inactive; MissingInput for every input that is short (or a
        /// resource of the recipe the run has not unlocked), naming auto-sell as the cause when the short resource is
        /// auto-sold (UX-10); StorageFull when not even one recipe unit's net change fits (OD-9). Each carries its
        /// corrective actions, which are choices, not a recommendation.
        /// </summary>
        private static MachineBlockerInfo[] Blockers(RunSession session, RunCatalog catalog, RunState state, MachineInstance m,
            MachineRecipe recipe, long outputFactor)
        {
            var list = new List<MachineBlockerInfo>();
            if (!m.IsActive)
                list.Add(new MachineBlockerInfo(MachineBlocker.Inactive, null, 0, 0, false, 0, 0,
                    new[] { new CorrectiveAction(CorrectiveActionKind.ActivateMachine, null, null, m.InstanceId) }));
            if (recipe == null) return list.ToArray();

            GlobalInventory stock = state.Inventory;
            for (int r = 0; r < recipe.Inputs.Count; r++)
            {
                ResourceAmount input = recipe.Inputs[r];
                bool allowed = stock.IsAllowed(input.ResourceId);
                long available = stock.Units(input.ResourceId);
                if (allowed && available >= input.Amount) continue;
                bool autoSold = state.IsAutoSell(input.ResourceId);
                var actions = new List<CorrectiveAction>();
                if (autoSold) actions.Add(new CorrectiveAction(CorrectiveActionKind.DisableAutoSell, input.ResourceId));
                AddProducers(session, catalog, input.ResourceId, actions);
                list.Add(new MachineBlockerInfo(MachineBlocker.MissingInput, input.ResourceId, input.Amount, available, autoSold, 0, 0,
                    actions.ToArray()));
            }
            for (int r = 0; r < recipe.Outputs.Count; r++)
            {
                ResourceAmount output = recipe.Outputs[r];
                if (stock.IsAllowed(output.ResourceId)) continue;
                list.Add(new MachineBlockerInfo(MachineBlocker.MissingInput, output.ResourceId, output.Amount, 0, false, 0, 0,
                    new CorrectiveAction[0]));
            }

            if (RunDerivation.TryNetPerUnit(recipe, outputFactor, out long net) && net > 0 && stock.Free < net)
            {
                var actions = new List<CorrectiveAction>();
                for (int r = 0; r < recipe.Outputs.Count; r++)
                {
                    string id = recipe.Outputs[r].ResourceId;
                    if (state.IsUnlocked(id) && !state.IsAutoSell(id)) actions.Add(new CorrectiveAction(CorrectiveActionKind.EnableAutoSell, id));
                }
                if (state.StorageLevel < catalog.StorageLevels.Count) actions.Add(new CorrectiveAction(CorrectiveActionKind.UpgradeStorage));
                list.Add(new MachineBlockerInfo(MachineBlocker.StorageFull, null, 0, 0, false, net, stock.Free, actions.ToArray()));
            }
            return list.ToArray();
        }

        private static void AddProducers(RunSession session, RunCatalog catalog, string resourceId, List<CorrectiveAction> actions)
        {
            for (int t = 0; t < session.MachineTypeCount; t++)
            {
                MachineDef def = catalog.Machines[session.MachineTypeId(t)];
                MachineRecipe recipe = catalog.Recipes[def.RecipeId];
                for (int o = 0; o < recipe.Outputs.Count; o++)
                {
                    if (!string.Equals(recipe.Outputs[o].ResourceId, resourceId, StringComparison.Ordinal)) continue;
                    actions.Add(new CorrectiveAction(CorrectiveActionKind.BuildProducer, resourceId, def.Id));
                    break;
                }
            }
        }
    }
}
