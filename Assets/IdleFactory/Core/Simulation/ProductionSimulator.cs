using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using IdleFactory.Domain;

namespace IdleFactory.Simulation
{
    public enum ProductionAdvanceStatus
    {
        Success = 0,
        InvalidElapsedTicks = 1,
        InvalidFactoryState = 2,
        ArithmeticOverflow = 3,
    }

    public sealed class ProductionResourceChange
    {
        public ProductionResourceChange(ResourceId resourceId, long amount)
        {
            _ = resourceId.Value;
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            ResourceId = resourceId;
            Amount = amount;
        }

        public ResourceId ResourceId { get; }
        public long Amount { get; }
    }

    public sealed class BuildingProductionChange
    {
        public BuildingProductionChange(
            BuildingInstanceId buildingId,
            long completedCycles,
            long initialProgressTicks,
            long finalProgressTicks,
            IEnumerable<ProductionResourceChange> consumedInputs,
            IEnumerable<ProductionResourceChange> producedOutputs,
            IEnumerable<ProductionBlocker> blockers)
        {
            _ = buildingId.Value;
            if (completedCycles < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(completedCycles));
            }

            if (initialProgressTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialProgressTicks));
            }

            if (finalProgressTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(finalProgressTicks));
            }

            BuildingId = buildingId;
            CompletedCycles = completedCycles;
            InitialProgressTicks = initialProgressTicks;
            FinalProgressTicks = finalProgressTicks;
            ConsumedInputs = CopyRequired(consumedInputs, nameof(consumedInputs));
            ProducedOutputs = CopyRequired(producedOutputs, nameof(producedOutputs));
            Blockers = CopyRequired(blockers, nameof(blockers));
        }

        public BuildingInstanceId BuildingId { get; }
        public long CompletedCycles { get; }
        public long InitialProgressTicks { get; }
        public long FinalProgressTicks { get; }
        public IReadOnlyList<ProductionResourceChange> ConsumedInputs { get; }
        public IReadOnlyList<ProductionResourceChange> ProducedOutputs { get; }
        public IReadOnlyList<ProductionBlocker> Blockers { get; }

        private static ReadOnlyCollection<T> CopyRequired<T>(IEnumerable<T> source, string parameterName)
            where T : class
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<T>();
            foreach (var item in source)
            {
                if (item == null)
                {
                    throw new ArgumentException("Summary collections must not contain null entries.", parameterName);
                }

                copy.Add(item);
            }

            return copy.AsReadOnly();
        }
    }

    public sealed class ProductionAdvanceSummary
    {
        public ProductionAdvanceSummary(long elapsedTicks, IEnumerable<BuildingProductionChange> buildings)
        {
            if (elapsedTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedTicks));
            }

            if (buildings == null)
            {
                throw new ArgumentNullException(nameof(buildings));
            }

            ElapsedTicks = elapsedTicks;
            Buildings = new List<BuildingProductionChange>(buildings).AsReadOnly();
        }

        public long ElapsedTicks { get; }
        public IReadOnlyList<BuildingProductionChange> Buildings { get; }
    }

    public sealed class ProductionAdvanceResult
    {
        private ProductionAdvanceResult(
            ProductionAdvanceStatus status,
            FactoryState candidate,
            ProductionAdvanceSummary summary)
        {
            Status = status;
            Candidate = candidate;
            Summary = summary;
        }

        public ProductionAdvanceStatus Status { get; }
        public bool IsSuccess => Status == ProductionAdvanceStatus.Success;
        public FactoryState Candidate { get; }
        public ProductionAdvanceSummary Summary { get; }

        internal static ProductionAdvanceResult Succeeded(
            FactoryState candidate,
            ProductionAdvanceSummary summary)
        {
            return new ProductionAdvanceResult(
                ProductionAdvanceStatus.Success,
                candidate ?? throw new ArgumentNullException(nameof(candidate)),
                summary ?? throw new ArgumentNullException(nameof(summary)));
        }

        internal static ProductionAdvanceResult Failed(ProductionAdvanceStatus status)
        {
            if (status == ProductionAdvanceStatus.Success)
            {
                throw new ArgumentException("A failure result requires a failure status.", nameof(status));
            }

            return new ProductionAdvanceResult(status, null, null);
        }
    }

    public static class ProductionSimulator
    {
        public static ProductionAdvanceResult Advance(FactoryState factory, long elapsedTicks)
        {
            if (elapsedTicks < 0)
            {
                return ProductionAdvanceResult.Failed(ProductionAdvanceStatus.InvalidElapsedTicks);
            }

            FactoryState candidate;
            BuildingInstanceState[] orderedBuildings;
            try
            {
                candidate = ValidateAndCopyFactory(factory, out orderedBuildings);
            }
            catch (OverflowException)
            {
                return ProductionAdvanceResult.Failed(ProductionAdvanceStatus.ArithmeticOverflow);
            }
            catch (Exception exception) when (IsInvalidStateException(exception))
            {
                return ProductionAdvanceResult.Failed(ProductionAdvanceStatus.InvalidFactoryState);
            }

            var changes = new List<BuildingProductionChange>(orderedBuildings.Length);
            try
            {
                foreach (var source in orderedBuildings)
                {
                    var building = candidate.Buildings[source.Id];
                    changes.Add(AdvanceBuilding(building, elapsedTicks));
                }

                var committed = new FactoryState(
                    candidate.Bounds,
                    candidate.Catalog,
                    candidate.Buildings.Values.OrderBy(building => building.Id));
                return ProductionAdvanceResult.Succeeded(
                    committed,
                    new ProductionAdvanceSummary(elapsedTicks, changes));
            }
            catch (OverflowException)
            {
                return ProductionAdvanceResult.Failed(ProductionAdvanceStatus.ArithmeticOverflow);
            }
            catch (Exception exception) when (IsInvalidStateException(exception))
            {
                return ProductionAdvanceResult.Failed(ProductionAdvanceStatus.InvalidFactoryState);
            }
        }

        private static FactoryState ValidateAndCopyFactory(
            FactoryState factory,
            out BuildingInstanceState[] orderedBuildings)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            ValidateCatalog(factory.Catalog);
            orderedBuildings = factory.Buildings.Values.OrderBy(building => building.Id).ToArray();
            return new FactoryState(factory.Bounds, factory.Catalog, orderedBuildings);
        }

        private static void ValidateCatalog(DefinitionCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            foreach (var pair in catalog.Resources)
            {
                if (pair.Value == null || pair.Key != pair.Value.Id)
                {
                    throw new ArgumentException("The resource catalog index is inconsistent.", nameof(catalog));
                }
            }

            foreach (var pair in catalog.Recipes)
            {
                if (pair.Value == null || pair.Key != pair.Value.Id)
                {
                    throw new ArgumentException("The recipe catalog index is inconsistent.", nameof(catalog));
                }
            }

            foreach (var pair in catalog.Buildings)
            {
                if (pair.Value == null || pair.Key != pair.Value.Id)
                {
                    throw new ArgumentException("The building catalog index is inconsistent.", nameof(catalog));
                }
            }

            foreach (var pair in catalog.OfflinePolicies)
            {
                if (pair.Value == null || pair.Key != pair.Value.Revision)
                {
                    throw new ArgumentException("The offline policy catalog index is inconsistent.", nameof(catalog));
                }
            }

            var resources = catalog.Resources.Values
                .OrderBy(definition => definition.Id)
                .Select(definition => new ResourceDefinition(
                    definition.Id,
                    definition.DevelopmentLabel,
                    definition.PresentationKey,
                    definition.IsDevelopmentOnly))
                .ToArray();
            var recipes = catalog.Recipes.Values
                .OrderBy(definition => definition.Id)
                .Select(definition => new RecipeDefinition(
                    definition.Id,
                    definition.Inputs.Select(amount =>
                        new ResourceAmountDefinition(amount.ResourceId, amount.Amount)),
                    definition.Outputs.Select(amount =>
                        new ResourceAmountDefinition(amount.ResourceId, amount.Amount)),
                    definition.DurationTicks,
                    definition.IsDevelopmentOnly))
                .ToArray();
            var buildings = catalog.Buildings.Values
                .OrderBy(definition => definition.Id)
                .Select(definition => new BuildingDefinition(
                    definition.Id,
                    definition.Footprint,
                    definition.AllowedOrientations,
                    definition.RecipeId,
                    CopyBufferDefinition(definition.InputBuffer),
                    CopyBufferDefinition(definition.OutputBuffer),
                    definition.PresentationKey,
                    definition.IsDevelopmentOnly))
                .ToArray();
            var policies = catalog.OfflinePolicies.Values
                .OrderBy(definition => definition.Revision)
                .Select(definition => new OfflinePolicyDefinition(
                    definition.Revision,
                    definition.EfficiencyNumerator,
                    definition.EfficiencyDenominator,
                    definition.MaxCreditedTicks))
                .ToArray();

            _ = new DefinitionCatalog(resources, recipes, buildings, policies);
        }

        private static BufferDefinition CopyBufferDefinition(BufferDefinition definition)
        {
            return definition == null
                ? null
                : new BufferDefinition(definition.Role, definition.Capacities);
        }

        private static BuildingProductionChange AdvanceBuilding(
            BuildingInstanceState building,
            long elapsedTicks)
        {
            if (building.Production == null)
            {
                return new BuildingProductionChange(
                    building.Id,
                    0,
                    0,
                    0,
                    Array.Empty<ProductionResourceChange>(),
                    Array.Empty<ProductionResourceChange>(),
                    building.GetProductionBlockers());
            }

            var production = building.Production;
            var recipe = production.Definition;
            var initialProgress = production.ProgressTicks;
            var initialBlockers = building.GetProductionBlockers();
            if (initialBlockers.Count > 0 || elapsedTicks == 0)
            {
                return CreateChange(building, 0, initialProgress, initialBlockers);
            }

            long timeCycles;
            long remainder;
            var ticksToFirstCompletion = checked(recipe.DurationTicks - initialProgress);
            if (elapsedTicks < ticksToFirstCompletion)
            {
                production.SetProgressTicks(checked(initialProgress + elapsedTicks));
                return CreateChange(building, 0, initialProgress, building.GetProductionBlockers());
            }

            var ticksAfterFirstCompletion = checked(elapsedTicks - ticksToFirstCompletion);
            timeCycles = checked(1 + (ticksAfterFirstCompletion / recipe.DurationTicks));
            remainder = ticksAfterFirstCompletion % recipe.DurationTicks;

            var permittedCycles = timeCycles;
            foreach (var input in recipe.Inputs)
            {
                permittedCycles = Math.Min(
                    permittedCycles,
                    building.InputBuffer.Quantity(input.ResourceId) / input.Amount);
            }

            foreach (var output in recipe.Outputs)
            {
                permittedCycles = Math.Min(
                    permittedCycles,
                    building.OutputBuffer.FreeCapacity(output.ResourceId) / output.Amount);
            }

            var removals = MultiplyAmounts(recipe.Inputs, permittedCycles);
            var additions = MultiplyAmounts(recipe.Outputs, permittedCycles);
            if (permittedCycles > 0)
            {
                var exchange = ResourceBufferState.TryExchange(
                    building.InputBuffer,
                    removals,
                    building.OutputBuffer,
                    additions);
                if (exchange == BufferOperationResult.ArithmeticOverflow)
                {
                    throw new OverflowException("The aggregate production exchange overflowed Int64.");
                }

                if (exchange != BufferOperationResult.Success)
                {
                    throw new InvalidOperationException("A prevalidated aggregate production exchange failed.");
                }
            }

            var finalBlockers = building.GetProductionBlockers();
            var finalProgress = permittedCycles == timeCycles && finalBlockers.Count == 0
                ? remainder
                : 0;
            production.SetProgressTicks(finalProgress);
            return new BuildingProductionChange(
                building.Id,
                permittedCycles,
                initialProgress,
                finalProgress,
                ToChanges(removals),
                ToChanges(additions),
                finalBlockers);
        }

        private static BuildingProductionChange CreateChange(
            BuildingInstanceState building,
            long completedCycles,
            long initialProgress,
            IEnumerable<ProductionBlocker> blockers)
        {
            var recipe = building.Production.Definition;
            return new BuildingProductionChange(
                building.Id,
                completedCycles,
                initialProgress,
                building.Production.ProgressTicks,
                ZeroChanges(recipe.Inputs),
                ZeroChanges(recipe.Outputs),
                blockers);
        }

        private static KeyValuePair<ResourceId, long>[] MultiplyAmounts(
            IEnumerable<ResourceAmountDefinition> definitions,
            long cycles)
        {
            return definitions
                .Select(definition => new KeyValuePair<ResourceId, long>(
                    definition.ResourceId,
                    checked(definition.Amount * cycles)))
                .ToArray();
        }

        private static IEnumerable<ProductionResourceChange> ZeroChanges(
            IEnumerable<ResourceAmountDefinition> definitions)
        {
            return definitions.Select(definition => new ProductionResourceChange(definition.ResourceId, 0));
        }

        private static IEnumerable<ProductionResourceChange> ToChanges(
            IEnumerable<KeyValuePair<ResourceId, long>> amounts)
        {
            return amounts.Select(amount => new ProductionResourceChange(amount.Key, amount.Value));
        }

        private static bool IsInvalidStateException(Exception exception)
        {
            return exception is ArgumentException ||
                   exception is InvalidOperationException ||
                   exception is KeyNotFoundException;
        }
    }
}
