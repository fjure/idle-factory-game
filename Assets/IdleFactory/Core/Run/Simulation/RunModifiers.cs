using System;
using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;

namespace IdleFactory.Run.Simulation
{
    /// <summary>
    /// Resolves the run modifiers (Research, Industry, active Item effects, Overdrive) into the inputs of
    /// <see cref="SpeedComposer"/>, the output factor and the sale price. Research and Industry are applied in
    /// ordinal ID order, Items in the <see cref="ActiveEffectSet"/> order (source ID first), and every factor of a
    /// leveled source is applied once per level, so that the result never depends on hash order (GD-6). Scratch
    /// lists are reused: a warmed call does not allocate.
    /// </summary>
    public sealed class RunModifiers
    {
        private readonly RunCatalog _catalog;
        private readonly ResearchDef[] _research;
        private readonly IndustryUpgradeDef[] _industry;
        private readonly List<SpeedFactor> _researchSpeed = new List<SpeedFactor>();
        private readonly List<SpeedFactor> _industrySpeed = new List<SpeedFactor>();
        private readonly List<long> _itemSpeed = new List<long>();
        private readonly List<long> _outputFactors = new List<long>();

        public RunModifiers(RunCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _research = new ResearchDef[catalog.Research.Count];
            int n = 0;
            foreach (ResearchDef r in catalog.Research.Values) _research[n++] = r;
            Array.Sort(_research, (a, b) => string.CompareOrdinal(a.Id, b.Id));
            _industry = new IndustryUpgradeDef[catalog.IndustryUpgrades.Count];
            n = 0;
            foreach (IndustryUpgradeDef u in catalog.IndustryUpgrades.Values) _industry[n++] = u;
            Array.Sort(_industry, (a, b) => string.CompareOrdinal(a.Id, b.Id));
        }

        /// <summary>
        /// The composed speed of a machine: level table (growth x milestones), Research, Industry, Items in source-ID
        /// order, then Overdrive (<paramref name="overdriveActive"/>). <paramref name="effectExpired"/> optionally
        /// flags effects (by index in <see cref="ActiveEffectSet.Effects"/>) that must be ignored.
        /// </summary>
        public bool TryMachineSpeed(RunState state, PermanentState permanent, MachineInstance machine,
            bool overdriveActive, bool[] effectExpired, out long speedPpm)
        {
            speedPpm = 0;
            if (!_catalog.Machines.TryGetValue(machine.MachineId, out MachineDef def) ||
                !SpeedComposer.TryLevelSpeed(def, machine.Level, out long levelSpeed))
                return false;
            _researchSpeed.Clear();
            for (int i = 0; i < _research.Length; i++)
            {
                EffectDef e = _research[i].Effect;
                if (e.Kind != "machineSpeed" || !MachineTargeted(e, machine.MachineId)) continue;
                long level = state.ResearchLevel(_research[i].Id);
                if (level > 0) _researchSpeed.Add(new SpeedFactor(e.Value, (int)Math.Min(level, SpeedComposer.MaxEffectLevels)));
            }
            _industrySpeed.Clear();
            if (permanent != null)
            {
                for (int i = 0; i < _industry.Length; i++)
                {
                    EffectDef e = _industry[i].Effect;
                    if (e.Kind != "machineSpeed" || !MachineTargeted(e, machine.MachineId)) continue;
                    long level = permanent.IndustryLevel(_industry[i].Id);
                    if (level > 0) _industrySpeed.Add(new SpeedFactor(e.Value, (int)Math.Min(level, SpeedComposer.MaxEffectLevels)));
                }
            }
            _itemSpeed.Clear();
            IReadOnlyList<ActiveEffect> effects = state.Effects.Effects;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effectExpired != null && effectExpired[i]) continue;
                if (!_catalog.Items.TryGetValue(effects[i].SourceId, out ItemDef item)) continue;
                if (item.Effect.Kind != "machineSpeed" || !ItemTargets(item, effects[i], machine.MachineId)) continue;
                _itemSpeed.Add(item.Effect.Value);
            }
            long overdrive = overdriveActive ? _catalog.Overdrive.SpeedPpm : SpeedComposer.Ppm;
            return SpeedComposer.TryCompose(levelSpeed, _researchSpeed, _industrySpeed, _itemSpeed, overdrive,
                _catalog.RunStart.MaxCombinedSpeedPpm, out speedPpm);
        }

        /// <summary>
        /// The integer output factor of a machine: active Item factors (source-ID order) and Research
        /// <c>machineOutput</c> factors (ordinal ID order, once per level), clamped to <c>maxCombinedOutputFactor</c>.
        /// </summary>
        public bool TryOutputFactor(RunState state, MachineInstance machine, bool[] effectExpired, out long factor)
        {
            _outputFactors.Clear();
            IReadOnlyList<ActiveEffect> effects = state.Effects.Effects;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effectExpired != null && effectExpired[i]) continue;
                if (!_catalog.Items.TryGetValue(effects[i].SourceId, out ItemDef item)) continue;
                if (item.Effect.Kind != "machineOutput" || !ItemTargets(item, effects[i], machine.MachineId)) continue;
                _outputFactors.Add(item.Effect.Value);
            }
            for (int i = 0; i < _research.Length; i++)
            {
                EffectDef e = _research[i].Effect;
                if (e.Kind != "machineOutput" || e.TargetKind != "machine" ||
                    !string.Equals(e.TargetId, machine.MachineId, StringComparison.Ordinal))
                    continue;
                long level = state.ResearchLevel(_research[i].Id);
                for (long n = 0; n < Math.Min(level, SpeedComposer.MaxEffectLevels); n++) _outputFactors.Add(e.Value);
            }
            return SpeedComposer.TryComposeOutputFactor(_outputFactors, _catalog.RunStart.MaxCombinedOutputFactor, out factor);
        }

        /// <summary>
        /// The effective sale price multiplier of a resource in ppm: <c>10^6</c> folded with every <c>salePrice</c>
        /// Research and Industry factor that targets all resources or this resource, once per level, with
        /// <c>MulDivFloor(acc, factor, 10^6)</c>. The sale income is
        /// <c>MulDivFloor(units x basePrice, salePricePpm, 10^6)</c> (R06).
        /// </summary>
        public bool TrySalePricePpm(RunState state, PermanentState permanent, string resourceId, out long ppm)
        {
            ppm = 0;
            long acc = SpeedComposer.Ppm;
            for (int i = 0; i < _research.Length; i++)
            {
                EffectDef e = _research[i].Effect;
                if (e.Kind != "salePrice" || !ResourceTargeted(e, resourceId)) continue;
                if (!FoldLevels(ref acc, e.Value, state.ResearchLevel(_research[i].Id))) return false;
            }
            if (permanent != null)
            {
                for (int i = 0; i < _industry.Length; i++)
                {
                    EffectDef e = _industry[i].Effect;
                    if (e.Kind != "salePrice" || !ResourceTargeted(e, resourceId)) continue;
                    if (!FoldLevels(ref acc, e.Value, permanent.IndustryLevel(_industry[i].Id))) return false;
                }
            }
            ppm = acc;
            return true;
        }

        /// <summary>
        /// The upgrade cost after the <c>upgradeCost</c> modifiers (Research and Industry, ordinal ID order, once
        /// per level): each factor folds as <c>MulDivCeil(acc, factor, 10^6)</c>, so a positive cost never rounds
        /// down to 0 and the player never pays less than the exact discounted value. <paramref name="machineId"/>
        /// selects the machine targets (<c>allMachines</c>, or <c>machine</c> with that ID); <c>null</c> selects an
        /// expansion track (<c>expansion</c> targets, which lower both storage and factory).
        /// </summary>
        public bool TryUpgradeCost(RunState state, PermanentState permanent, long baseCost, string machineId, out long cost)
        {
            cost = 0;
            if (!ExactBound.Contains(baseCost)) return false;
            long acc = baseCost;
            for (int i = 0; i < _research.Length; i++)
            {
                EffectDef e = _research[i].Effect;
                if (e.Kind != "upgradeCost" || !CostTargeted(e, machineId)) continue;
                if (!FoldCost(ref acc, e.Value, state.ResearchLevel(_research[i].Id))) return false;
            }
            if (permanent != null)
            {
                for (int i = 0; i < _industry.Length; i++)
                {
                    EffectDef e = _industry[i].Effect;
                    if (e.Kind != "upgradeCost" || !CostTargeted(e, machineId)) continue;
                    if (!FoldCost(ref acc, e.Value, permanent.IndustryLevel(_industry[i].Id))) return false;
                }
            }
            cost = acc;
            return true;
        }

        private static bool FoldCost(ref long acc, long factorPpm, long levels)
        {
            if (levels > SpeedComposer.MaxEffectLevels) levels = SpeedComposer.MaxEffectLevels;
            for (long n = 0; n < levels; n++)
                if (!MulDiv.MulDivCeil(acc, factorPpm, SpeedComposer.Ppm, out acc)) return false;
            return true;
        }

        private static bool CostTargeted(EffectDef e, string machineId)
        {
            if (machineId == null) return e.TargetKind == "expansion";
            return e.TargetKind == "allMachines" ||
                (e.TargetKind == "machine" && string.Equals(e.TargetId, machineId, StringComparison.Ordinal));
        }

        private static bool FoldLevels(ref long acc, long factorPpm, long levels)
        {
            if (levels > SpeedComposer.MaxEffectLevels) levels = SpeedComposer.MaxEffectLevels;
            for (long n = 0; n < levels; n++)
                if (!MulDiv.MulDivFloor(acc, factorPpm, SpeedComposer.Ppm, out acc)) return false;
            return true;
        }

        private static bool MachineTargeted(EffectDef e, string machineId)
        {
            return e.TargetKind == "allMachines" ||
                (e.TargetKind == "machine" && string.Equals(e.TargetId, machineId, StringComparison.Ordinal));
        }

        private static bool ResourceTargeted(EffectDef e, string resourceId)
        {
            return e.TargetKind == "allResources" ||
                (e.TargetKind == "resource" && string.Equals(e.TargetId, resourceId, StringComparison.Ordinal));
        }

        /// <summary>
        /// A factory Item applies to every machine; a machine-type Item to every machine of the chosen type, which
        /// is the target key of the effect's conflict key (OD-1).
        /// </summary>
        private static bool ItemTargets(ItemDef item, ActiveEffect effect, string machineId)
        {
            return item.TargetKind == "factory" ||
                string.Equals(effect.ConflictKey.TargetKey, machineId, StringComparison.Ordinal);
        }
    }
}
