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

    public static partial class RunSnapshots
    {
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
    }
}
