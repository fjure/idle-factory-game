using System;
using System.Collections.Generic;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;

namespace IdleFactory.Run.Domain
{
    /// <summary>A speed factor in ppm applied <see cref="Levels"/> times (Research and Industry level effects).</summary>
    public readonly struct SpeedFactor
    {
        public long Ppm { get; }
        public int Levels { get; }
        public SpeedFactor(long ppm, int levels)
        {
            Ppm = ppm;
            Levels = levels;
        }
    }

    /// <summary>
    /// Pure integer speed, batch and progress arithmetic (research R03, OD-6, OD-9). Everything is checked or
    /// 128-bit; a failed call returns false and leaves its outputs at 0 so the caller keeps its state.
    /// </summary>
    public static class SpeedComposer
    {
        public const long Ppm = 1_000_000;

        /// <summary>Content maximum for a research or industry level (definitions allow at most 1,024).</summary>
        public const int MaxEffectLevels = 1024;

        /// <summary>
        /// <c>SpeedTable[level]</c> of the machine, which already holds growth x milestones (level and
        /// milestone stage of the composition). False for a level outside 1..MaxLevel.
        /// </summary>
        public static bool TryLevelSpeed(MachineDef machine, int level, out long speedPpm)
        {
            speedPpm = 0;
            if (machine == null || level < 1 || level > machine.MaxLevel) return false;
            speedPpm = machine.SpeedTable[level];
            return true;
        }

        /// <summary>
        /// Folds <c>level x milestones (levelSpeedPpm) x research x industry x items x Overdrive</c> left to
        /// right with <c>MulDivFloor(acc, factor, 10^6)</c>. Research and industry factors are applied once per
        /// level, items are in itemId order (the caller supplies them ordered), and an inactive Overdrive passes
        /// <see cref="Ppm"/>. The final clamp to <paramref name="maxCombinedSpeedPpm"/> is defensive only.
        /// </summary>
        public static bool TryCompose(long levelSpeedPpm, IReadOnlyList<SpeedFactor> research,
            IReadOnlyList<SpeedFactor> industry, IReadOnlyList<long> itemFactors, long overdrivePpm,
            long maxCombinedSpeedPpm, out long speedPpm)
        {
            speedPpm = 0;
            if (levelSpeedPpm < 1 || levelSpeedPpm > ExactBound.Value || overdrivePpm < 1 || maxCombinedSpeedPpm < 1)
                return false;
            long acc = levelSpeedPpm;
            if (!FoldLevels(ref acc, research)) return false;
            if (!FoldLevels(ref acc, industry)) return false;
            if (itemFactors != null)
            {
                for (int i = 0; i < itemFactors.Count; i++)
                    if (!Fold(ref acc, itemFactors[i])) return false;
            }
            if (!Fold(ref acc, overdrivePpm)) return false;
            if (acc < 1) return false;
            speedPpm = acc > maxCombinedSpeedPpm ? maxCombinedSpeedPpm : acc;
            return true;
        }

        /// <summary>
        /// OD-6: <c>k = max(1, MulDivCeil(speedPpm, minCycleTicks, durationTicks x 10^6))</c> and
        /// <c>cycleDuration = max(1, MulDivCeil(k x durationTicks, 10^6, speedPpm))</c>.
        /// </summary>
        public static bool TryBatch(long durationTicks, long speedPpm, long minCycleTicks,
            out long batchFactor, out long cycleDurationTicks)
        {
            batchFactor = 0;
            cycleDurationTicks = 0;
            if (durationTicks < 1 || durationTicks > 1_000_000_000_000L || speedPpm < 1 || speedPpm > ExactBound.Value ||
                minCycleTicks < 1 || minCycleTicks > ExactBound.Value)
                return false;
            if (!CheckedMath.TryMultiply(durationTicks, Ppm, out long denominator)) return false;
            if (!MulDiv.MulDivCeil(speedPpm, minCycleTicks, denominator, out long k)) return false;
            if (k < 1) k = 1;
            if (!CheckedMath.TryMultiply(k, durationTicks, out long batchTicks)) return false;
            if (!MulDiv.MulDivCeil(batchTicks, Ppm, speedPpm, out long cycle)) return false;
            if (cycle < 1) cycle = 1;
            batchFactor = k;
            cycleDurationTicks = cycle;
            return true;
        }

        /// <summary>
        /// Product of integer output factors, clamped to <paramref name="maxCombinedOutputFactor"/>. A product
        /// beyond the exact bound is above any valid maximum and therefore clamps as well.
        /// </summary>
        public static bool TryComposeOutputFactor(IReadOnlyList<long> factors, long maxCombinedOutputFactor, out long factor)
        {
            factor = 0;
            if (maxCombinedOutputFactor < 1) return false;
            long acc = 1;
            if (factors != null)
            {
                for (int i = 0; i < factors.Count; i++)
                {
                    long f = factors[i];
                    if (f < 1 || f > ExactBound.Value) return false;
                    if (!CheckedMath.TryMultiply(acc, f, out acc)) acc = maxCombinedOutputFactor;
                    if (acc > maxCombinedOutputFactor) acc = maxCombinedOutputFactor;
                }
            }
            factor = acc;
            return true;
        }

        /// <summary>
        /// R03 rescale at a boundary: <c>p' = MulDivFloor(p, D', D)</c>; a waiting machine (<c>p == D</c>) stays
        /// waiting (<c>p' = D'</c>). Requires <c>0 &lt;= p &lt;= D</c>.
        /// </summary>
        public static bool TryRescaleProgress(long progressTicks, long oldDurationTicks, long newDurationTicks, out long newProgressTicks)
        {
            newProgressTicks = 0;
            if (oldDurationTicks < 1 || newDurationTicks < 1 || newDurationTicks > ExactBound.Value ||
                progressTicks < 0 || progressTicks > oldDurationTicks)
                return false;
            if (progressTicks == oldDurationTicks)
            {
                newProgressTicks = newDurationTicks;
                return true;
            }
            return MulDiv.MulDivFloor(progressTicks, newDurationTicks, oldDurationTicks, out newProgressTicks);
        }

        /// <summary>
        /// OD-9: <c>k' = min(k, k_in, k_cap)</c> for a due cycle. <c>k_in</c> is the minimum over inputs of
        /// <c>floor(stock / amount)</c> (unbounded without inputs); <c>k_cap = floor((capacity - total) / net)</c>
        /// for a positive net unit change <c>net = sum(outputs) x outputFactor - sum(inputs)</c> (unbounded when
        /// <c>net &lt;= 0</c>). 0 means the machine waits.
        /// </summary>
        public static bool TryCompletedUnits(long batchFactor, MachineRecipe recipe, long outputFactor,
            GlobalInventory inventory, out long completedUnits)
        {
            completedUnits = 0;
            if (batchFactor < 1 || recipe == null || inventory == null || outputFactor < 1) return false;

            long kIn = long.MaxValue;
            long inputSum = 0;
            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                ResourceAmount input = recipe.Inputs[i];
                long fit = inventory.Units(input.ResourceId) / input.Amount;
                if (fit < kIn) kIn = fit;
                if (!CheckedMath.TryAdd(inputSum, input.Amount, out inputSum)) return false;
            }

            long outputSum = 0;
            for (int i = 0; i < recipe.Outputs.Count; i++)
            {
                if (!CheckedMath.TryMultiply(recipe.Outputs[i].Amount, outputFactor, out long produced)) return false;
                if (!CheckedMath.TryAdd(outputSum, produced, out outputSum)) return false;
            }

            long kCap = long.MaxValue;
            if (outputSum > inputSum) kCap = inventory.Free / (outputSum - inputSum);

            long units = batchFactor;
            if (kIn < units) units = kIn;
            if (kCap < units) units = kCap;
            completedUnits = units;
            return true;
        }

        private static bool FoldLevels(ref long acc, IReadOnlyList<SpeedFactor> factors)
        {
            if (factors == null) return true;
            for (int i = 0; i < factors.Count; i++)
            {
                if (factors[i].Levels < 0 || factors[i].Levels > MaxEffectLevels) return false;
                for (int n = 0; n < factors[i].Levels; n++)
                    if (!Fold(ref acc, factors[i].Ppm)) return false;
            }
            return true;
        }

        private static bool Fold(ref long acc, long factorPpm)
        {
            if (factorPpm < 1) return false;
            if (!MulDiv.MulDivFloor(acc, factorPpm, Ppm, out long next)) return false;
            acc = next;
            return true;
        }
    }
}
