using System;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Run.Application
{
    /// <summary>
    /// The Factory Value of a run, part by part (FR-020, R06, R11):
    /// <c>Total = Cash + StockValue + MachineInvestedCash + StorageInvestedCash + FactoryInvestedCash</c>.
    /// </summary>
    public readonly struct FactoryValueBreakdown
    {
        public long Cash { get; }

        /// <summary>Sum over resources of <c>MulDivFloor(units x basePrice, salePricePpm, 10^6)</c>: the income of selling that stock now.</summary>
        public long StockValue { get; }

        /// <summary>Sum of the Cash actually paid for the machines that still stand (build plus upgrades).</summary>
        public long MachineInvestedCash { get; }
        public long StorageInvestedCash { get; }
        public long FactoryInvestedCash { get; }

        /// <summary>Storage plus factory expansion, the "expansion" line of the prestige surface.</summary>
        public long ExpansionInvestedCash => StorageInvestedCash + FactoryInvestedCash;

        public long Total { get; }

        public FactoryValueBreakdown(long cash, long stockValue, long machineInvestedCash, long storageInvestedCash, long factoryInvestedCash, long total)
        {
            Cash = cash;
            StockValue = stockValue;
            MachineInvestedCash = machineInvestedCash;
            StorageInvestedCash = storageInvestedCash;
            FactoryInvestedCash = factoryInvestedCash;
            Total = total;
        }
    }

    /// <summary>
    /// Factory Value and the Influence Points it earns (FR-020, FR-021). Pure and read-only.
    ///
    /// Stock is valued with the sale formula of a real sale, per resource (ECO-8), with the active run modifiers
    /// (Research and Industry <c>salePrice</c> effects) folded exactly as a sale folds them. Removed machines, RP,
    /// Research, contract rewards and spent Items are not part of the value; nothing is counted twice.
    /// </summary>
    public static class FactoryValuation
    {
        /// <summary>The Factory Value of a run. False when a checked step overflows (the value would exceed the exact bound).</summary>
        public static bool TryCompute(RunCatalog catalog, RunState state, PermanentState permanent, out FactoryValueBreakdown breakdown)
        {
            breakdown = default;
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (state == null) throw new ArgumentNullException(nameof(state));
            var modifiers = new RunModifiers(catalog);

            long stock = 0;
            GlobalInventory inventory = state.Inventory;
            for (int i = 0; i < inventory.Count; i++)
            {
                long units = inventory.UnitsAt(i);
                if (units == 0) continue;
                string id = inventory.IdAt(i);
                if (!catalog.Resources.TryGetValue(id, out ResourceDef resource) ||
                    !modifiers.TrySalePricePpm(state, permanent, id, out long ppm) ||
                    !CheckedMath.TryMultiply(units, resource.BasePrice, out long gross) ||
                    !MulDiv.MulDivFloor(gross, ppm, SpeedComposer.Ppm, out long income) ||
                    !CheckedMath.TryAdd(stock, income, out stock))
                    return false;
            }

            long machines = 0;
            for (int i = 0; i < state.Machines.Count; i++)
                if (!CheckedMath.TryAdd(machines, state.Machines[i].InvestedCash, out machines)) return false;

            if (!CheckedMath.TryAdd(state.Cash, stock, out long total) ||
                !CheckedMath.TryAdd(total, machines, out total) ||
                !CheckedMath.TryAdd(total, state.StorageInvestedCash, out total) ||
                !CheckedMath.TryAdd(total, state.FactoryInvestedCash, out total) ||
                !ExactBound.Contains(total))
                return false;

            breakdown = new FactoryValueBreakdown(state.Cash, stock, machines, state.StorageInvestedCash, state.FactoryInvestedCash, total);
            return true;
        }

        /// <summary>
        /// <c>IP = ISqrt(MulDivFloor(c^2, FV, divisor))</c>, the exact form of <c>floor(c x sqrt(FV / divisor))</c>.
        /// The only place a value is floored (FR-021). Coefficient and divisor come from the content.
        /// </summary>
        public static bool TryComputeIp(PrestigeDef prestige, long factoryValue, out long ip)
        {
            ip = 0;
            if (prestige == null) throw new ArgumentNullException(nameof(prestige));
            if (!ExactBound.Contains(factoryValue) ||
                !CheckedMath.TryMultiply(prestige.Coefficient, prestige.Coefficient, out long square) ||
                !MulDiv.MulDivFloor(square, factoryValue, prestige.Divisor, out long scaled))
                return false;
            ip = (long)IntegerSqrt.ISqrt((ulong)scaled);
            return true;
        }
    }
}
