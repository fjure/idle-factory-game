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

    /// <summary>
    /// What one sell step would do right now (T317, contracts/run-application-f005.md): the units the step names, the
    /// Cash they would pay and, when the step cannot run, the reason code the command would give. A value type, so
    /// building a snapshot allocates one array per resource and nothing per preview.
    /// </summary>
    public readonly struct SellPreviewEntry
    {
        /// <summary>The step size; <see cref="ResourceEconomy.SellAllStep"/> (0) is the All step.</summary>
        public long Step { get; }

        /// <summary>The units the step sells: the step size, or for All the whole stock.</summary>
        public long Units { get; }

        /// <summary>The Cash the sale would pay; 0 when it is not computable (a locked resource, a price at the numeric bound).</summary>
        public long Cash { get; }

        /// <summary>The <see cref="RunReasons"/> code the command would reject with, or null when the step is available.</summary>
        public string UnavailableReason { get; }

        public bool IsAvailable => UnavailableReason == null;

        internal SellPreviewEntry(long step, long units, long cash, string unavailableReason)
        {
            Step = step;
            Units = units;
            Cash = cash;
            UnavailableReason = unavailableReason;
        }
    }

    public sealed class ResourceEconomy
    {
        /// <summary>The <see cref="SellPreviewEntry.Step"/> of the All step (sell the whole stock).</summary>
        public const long SellAllStep = 0;

        private readonly SellPreviewEntry[] _sellPreviews;

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

        /// <summary>The previews of the sell steps of the catalog in step order, All last when the catalog has it.</summary>
        public IReadOnlyList<SellPreviewEntry> SellPreviews { get; }

        /// <summary>
        /// The preview of one sell step (<see cref="SellAllStep"/> for All). Caveat: the preview does not run the settle
        /// pre-check of the command, so a step shown available can still be rejected with value.limit when Cash is
        /// within one worst-case settle of 10^18; the sell UI shows that rejection like any other. A step that is not one of the catalog's
        /// steps is not offered: its entry has no units and the reason <see cref="RunReasons.CommandInvalid"/>.
        /// </summary>
        public SellPreviewEntry SellPreview(long step)
        {
            for (int i = 0; i < _sellPreviews.Length; i++)
                if (_sellPreviews[i].Step == step) return _sellPreviews[i];
            return new SellPreviewEntry(step, 0, 0, RunReasons.CommandInvalid);
        }

        internal ResourceEconomy(ResourceDef def, bool unlocked, long pricePpm, long unitPrice, bool priceValid, long units,
            long stockValue, bool autoSell, SellPreviewEntry[] sellPreviews)
        {
            _sellPreviews = sellPreviews;
            SellPreviews = new ReadOnlyCollection<SellPreviewEntry>(sellPreviews);
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

        /// <summary>The sell step sizes of the catalog (RunStart.SellSteps, ascending); the All step is <see cref="HasSellAll"/>.</summary>
        public IReadOnlyList<long> SellSteps { get; }

        /// <summary>Whether the catalog offers the All step (<see cref="ResourceEconomy.SellAllStep"/>).</summary>
        public bool HasSellAll { get; }

        internal EconomySnapshot(long structureRevision, long quantityRevision, long cash, ResourceEconomy[] resources,
            IReadOnlyList<long> sellSteps, bool hasSellAll)
        {
            SellSteps = sellSteps;
            HasSellAll = hasSellAll;
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
            IReadOnlyList<long> steps = catalog.RunStart.SellSteps;
            bool sellAll = catalog.RunStart.SellAll;
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
                var previews = new SellPreviewEntry[steps.Count + (sellAll ? 1 : 0)];
                for (int p = 0; p < previews.Length; p++)
                    previews[p] = Preview(def, unlocked, valid, ppm, units, state.Cash, p < steps.Count ? steps[p] : ResourceEconomy.SellAllStep);
                resources[i] = new ResourceEconomy(def, unlocked, valid ? ppm : 0, unitPrice, valid, units, stockValue,
                    state.IsAutoSell(def.Id), previews);
            }
            return new EconomySnapshot(session.StructureRevision, session.QuantityRevision, state.Cash, resources, steps, sellAll);
        }

        // The checks of RunCommands.Sell in the same order, evaluated on the snapshot's values. The settle pre-check
        // (which needs the machines) is not repeated here: a step shown available can still meet value.limit from the
        // command when Cash is within one worst-case settle of 10^18.
        private static SellPreviewEntry Preview(ResourceDef def, bool unlocked, bool priceValid, long ppm, long stock, long cash, long step)
        {
            long quantity = step == ResourceEconomy.SellAllStep ? stock : step;
            long income = 0;
            bool priced = priceValid && quantity >= 1 && SalePricing.TryValue(quantity, def.BasePrice, ppm, out income);
            if (!unlocked) return new SellPreviewEntry(step, quantity, 0, RunReasons.ResourceLocked);
            if (quantity < 1 || quantity > stock) return new SellPreviewEntry(step, quantity, priced ? income : 0, RunReasons.StockInsufficient);
            if (!priced || !CheckedMath.TryAdd(cash, income, out long after) || !ExactBound.Contains(after))
                return new SellPreviewEntry(step, quantity, priced ? income : 0, RunReasons.ValueLimit);
            return new SellPreviewEntry(step, quantity, income, null);
        }
    }
}
