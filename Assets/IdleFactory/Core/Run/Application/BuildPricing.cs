using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Run.Application
{
    /// <summary>
    /// The build price of a machine (F5-R06, FR-013): a content curve over the owned count of the type and the number of
    /// completed prestiges, rounded once upward, then the F003 <c>upgradeCost</c> discounts with the round-up rule. Whole
    /// number checked arithmetic only; derived on demand and never persisted (data-model.md). A machine without a
    /// <see cref="BuildPriceDef"/> (legacy content) costs its level-1 table cost, as in F003.
    /// </summary>
    public static class BuildPricing
    {
        private const long Ppm = 1_000_000;

        /// <summary>
        /// The curve before discounts. <c>acc = base x 10^6</c>; per owned machine and then per prestige
        /// <c>acc = MulDivFloor(acc, growthPpm, 10^6)</c>, returning <c>cap</c> as soon as <c>acc >= cap x 10^6</c>
        /// (or the step overflows, which is above the cap by definition); the price is <c>min(ceil(acc / 10^6), cap)</c>.
        /// False when an input is negative or above the exact bound.
        /// </summary>
        public static bool TryCurve(BuildPriceDef def, long owned, long prestigeCount, out long price)
        {
            price = 0;
            if (def == null || owned < 0 || prestigeCount < 0) return false;
            if (def.Base > 1_000_000_000_000L || def.Cap > 1_000_000_000_000L) return false;
            long capScaled = def.Cap * Ppm;
            long acc = def.Base * Ppm;
            if (!Step(ref acc, def.OwnedGrowthPpm, owned, capScaled) || !Step(ref acc, def.PrestigeGrowthPpm, prestigeCount, capScaled))
            {
                price = def.Cap;
                return true;
            }

            long ceiled = acc / Ppm + (acc % Ppm == 0 ? 0 : 1);
            price = ceiled < def.Cap ? ceiled : def.Cap;
            return true;
        }

        // False when the cap was reached. A factor of exactly 10^6 leaves acc unchanged, so its steps are skipped.
        private static bool Step(ref long acc, long growthPpm, long times, long capScaled)
        {
            if (growthPpm == Ppm || acc == 0) return true;
            for (long i = 0; i < times; i++)
            {
                if (!MulDiv.MulDivFloor(acc, growthPpm, Ppm, out acc) || acc >= capScaled) return false;
            }

            return true;
        }

        /// <summary>
        /// The price the next build of <paramref name="def"/> costs right now, after discounts. False on a numeric
        /// bound (the command rejects with value.limit).
        /// </summary>
        public static bool TryPrice(RunCatalog catalog, RunModifiers modifiers, RunState state, PermanentState permanent,
            MachineDef def, out long price)
        {
            price = 0;
            long raw = def.CostTable[0];
            if (def.BuildPrice != null)
            {
                long owned = 0;
                for (int i = 0; i < state.Machines.Count; i++)
                    if (string.Equals(state.Machines[i].MachineId, def.Id, System.StringComparison.Ordinal)) owned++;
                if (!TryCurve(def.BuildPrice, owned, permanent == null ? 0 : permanent.PrestigeCount, out raw)) return false;
            }

            return modifiers.TryUpgradeCost(state, permanent, raw, def.Id, out price);
        }
    }
}
