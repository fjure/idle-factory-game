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
}
