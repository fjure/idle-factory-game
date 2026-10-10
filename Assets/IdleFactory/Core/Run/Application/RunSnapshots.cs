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
    // Builders
    // -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Builds the immutable read models on demand (contracts/run-application.md, Read models). Later stories add
    /// their snapshots to this partial class. Nothing here mutates the session.
    /// </summary>
    public static partial class RunSnapshots
    {
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

        private static void Accumulate(long[] totals, GlobalInventory stock, ResourceRate rate)
        {
            for (int i = 0; i < stock.Count; i++)
            {
                if (!string.Equals(stock.IdAt(i), rate.ResourceId, StringComparison.Ordinal)) continue;
                totals[i] = CheckedMath.TryAdd(totals[i], rate.PerMinuteMicro, out long sum) ? sum : ExactBound.Value;
                return;
            }
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
