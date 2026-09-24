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

    public sealed class ProductionSimulationSession
    {
        private readonly FactoryBounds bounds;
        private readonly DefinitionCatalog catalog;
        private readonly BuildingSlot[] buildings;
        private readonly ChangeSlot[] changes;
        private StateBuffer committed;
        private StateBuffer staging;
        private bool hasAttempt;
        private ProductionAdvanceStatus latestStatus;
        private long latestElapsedTicks;

        internal ProductionSimulationSession(FactoryState validatedFactory)
        {
            bounds = validatedFactory.Bounds;
            catalog = validatedFactory.Catalog;
            var ordered = validatedFactory.Buildings.Values.OrderBy(building => building.Id).ToArray();
            buildings = new BuildingSlot[ordered.Length];
            changes = new ChangeSlot[ordered.Length];
            committed = new StateBuffer(ordered.Length);
            staging = new StateBuffer(ordered.Length);

            for (var index = 0; index < ordered.Length; index++)
            {
                buildings[index] = new BuildingSlot(ordered[index]);
                changes[index] = new ChangeSlot();
                committed.Inputs[index] = buildings[index].CopyInputQuantities(ordered[index]);
                committed.Outputs[index] = buildings[index].CopyOutputQuantities(ordered[index]);
                committed.Progress[index] = ordered[index].Production == null
                    ? 0
                    : ordered[index].Production.ProgressTicks;
                staging.Inputs[index] = new long[committed.Inputs[index].Length];
                staging.Outputs[index] = new long[committed.Outputs[index].Length];
            }
        }

        public ProductionAdvanceStatus Advance(long elapsedTicks)
        {
            hasAttempt = true;
            latestElapsedTicks = elapsedTicks;
            if (elapsedTicks < 0)
            {
                latestStatus = ProductionAdvanceStatus.InvalidElapsedTicks;
                return latestStatus;
            }

            try
            {
                Calculate(elapsedTicks);
                var previous = committed;
                committed = staging;
                staging = previous;
                latestStatus = ProductionAdvanceStatus.Success;
            }
            catch (OverflowException)
            {
                latestStatus = ProductionAdvanceStatus.ArithmeticOverflow;
            }
            catch (Exception exception) when (ProductionSimulator.IsInvalidStateException(exception))
            {
                latestStatus = ProductionAdvanceStatus.InvalidFactoryState;
            }

            return latestStatus;
        }

        public ProductionAdvanceResult CaptureResult()
        {
            if (!hasAttempt)
            {
                throw new InvalidOperationException("Advance must be called before capturing a result.");
            }

            if (latestStatus != ProductionAdvanceStatus.Success)
            {
                return ProductionAdvanceResult.Failed(latestStatus);
            }

            var capturedChanges = new BuildingProductionChange[buildings.Length];
            for (var index = 0; index < buildings.Length; index++)
            {
                capturedChanges[index] = CaptureChange(index);
            }

            return ProductionAdvanceResult.Succeeded(
                CaptureFactory(),
                new ProductionAdvanceSummary(latestElapsedTicks, capturedChanges));
        }

        internal FactoryState CaptureFactory()
        {
            var capturedBuildings = new BuildingInstanceState[buildings.Length];
            for (var index = 0; index < buildings.Length; index++)
            {
                capturedBuildings[index] = buildings[index].Capture(
                    committed.Inputs[index],
                    committed.Outputs[index],
                    committed.Progress[index]);
            }

            return new FactoryState(bounds, catalog, capturedBuildings);
        }

        private void Calculate(long elapsedTicks)
        {
            for (var index = 0; index < buildings.Length; index++)
            {
                var building = buildings[index];
                CopyAndValidate(building.InputResources, committed.Inputs[index], staging.Inputs[index]);
                CopyAndValidate(building.OutputResources, committed.Outputs[index], staging.Outputs[index]);
                var initialProgress = committed.Progress[index];
                if (building.HasProduction &&
                    (initialProgress < 0 || initialProgress >= building.DurationTicks))
                {
                    throw new InvalidOperationException("Prepared production progress is invalid.");
                }

                staging.Progress[index] = initialProgress;
                changes[index].InitialProgress = initialProgress;
                changes[index].FinalProgress = initialProgress;
                changes[index].CompletedCycles = 0;

                if (!building.HasProduction)
                {
                    continue;
                }

                if (HasBlocker(building, staging.Inputs[index], staging.Outputs[index]) || elapsedTicks == 0)
                {
                    continue;
                }

                var ticksToFirstCompletion = checked(building.DurationTicks - initialProgress);
                if (elapsedTicks < ticksToFirstCompletion)
                {
                    var progress = checked(initialProgress + elapsedTicks);
                    staging.Progress[index] = progress;
                    changes[index].FinalProgress = progress;
                    continue;
                }

                var ticksAfterFirstCompletion = checked(elapsedTicks - ticksToFirstCompletion);
                var timeCycles = checked(1 + (ticksAfterFirstCompletion / building.DurationTicks));
                var remainder = ticksAfterFirstCompletion % building.DurationTicks;
                var permittedCycles = timeCycles;

                for (var amountIndex = 0; amountIndex < building.InputAmounts.Length; amountIndex++)
                {
                    permittedCycles = Math.Min(
                        permittedCycles,
                        staging.Inputs[index][building.InputIndices[amountIndex]] /
                        building.InputAmounts[amountIndex].Amount);
                }

                for (var amountIndex = 0; amountIndex < building.OutputAmounts.Length; amountIndex++)
                {
                    var resourceIndex = building.OutputIndices[amountIndex];
                    var free = checked(building.OutputResources[resourceIndex].Capacity -
                                       staging.Outputs[index][resourceIndex]);
                    permittedCycles = Math.Min(
                        permittedCycles,
                        free / building.OutputAmounts[amountIndex].Amount);
                }

                for (var amountIndex = 0; amountIndex < building.InputAmounts.Length; amountIndex++)
                {
                    var resourceIndex = building.InputIndices[amountIndex];
                    var amount = checked(building.InputAmounts[amountIndex].Amount * permittedCycles);
                    staging.Inputs[index][resourceIndex] = checked(staging.Inputs[index][resourceIndex] - amount);
                }

                for (var amountIndex = 0; amountIndex < building.OutputAmounts.Length; amountIndex++)
                {
                    var resourceIndex = building.OutputIndices[amountIndex];
                    var amount = checked(building.OutputAmounts[amountIndex].Amount * permittedCycles);
                    staging.Outputs[index][resourceIndex] = checked(staging.Outputs[index][resourceIndex] + amount);
                    if (staging.Outputs[index][resourceIndex] > building.OutputResources[resourceIndex].Capacity)
                    {
                        throw new InvalidOperationException("A staged output exceeded its prepared capacity.");
                    }
                }

                var finalProgress = permittedCycles == timeCycles &&
                                    !HasBlocker(building, staging.Inputs[index], staging.Outputs[index])
                    ? remainder
                    : 0;
                staging.Progress[index] = finalProgress;
                changes[index].CompletedCycles = permittedCycles;
                changes[index].FinalProgress = finalProgress;
            }
        }

        private static void CopyAndValidate(ResourceSlot[] resources, long[] source, long[] destination)
        {
            if (source.Length != resources.Length || destination.Length != resources.Length)
            {
                throw new InvalidOperationException("Prepared buffer storage is inconsistent.");
            }

            for (var index = 0; index < resources.Length; index++)
            {
                var value = source[index];
                if (value < 0 || value > resources[index].Capacity)
                {
                    throw new InvalidOperationException("Prepared buffer quantity is invalid.");
                }

                destination[index] = value;
            }
        }

        private static bool HasBlocker(BuildingSlot building, long[] inputs, long[] outputs)
        {
            if (!building.IsActive)
            {
                return true;
            }

            for (var index = 0; index < building.InputAmounts.Length; index++)
            {
                if (inputs[building.InputIndices[index]] < building.InputAmounts[index].Amount)
                {
                    return true;
                }
            }

            for (var index = 0; index < building.OutputAmounts.Length; index++)
            {
                var resourceIndex = building.OutputIndices[index];
                if (building.OutputResources[resourceIndex].Capacity - outputs[resourceIndex] <
                    building.OutputAmounts[index].Amount)
                {
                    return true;
                }
            }

            return false;
        }

        private BuildingProductionChange CaptureChange(int index)
        {
            var building = buildings[index];
            var change = changes[index];
            var consumed = new ProductionResourceChange[building.InputAmounts.Length];
            var produced = new ProductionResourceChange[building.OutputAmounts.Length];
            for (var amountIndex = 0; amountIndex < consumed.Length; amountIndex++)
            {
                consumed[amountIndex] = new ProductionResourceChange(
                    building.InputAmounts[amountIndex].ResourceId,
                    checked(building.InputAmounts[amountIndex].Amount * change.CompletedCycles));
            }

            for (var amountIndex = 0; amountIndex < produced.Length; amountIndex++)
            {
                produced[amountIndex] = new ProductionResourceChange(
                    building.OutputAmounts[amountIndex].ResourceId,
                    checked(building.OutputAmounts[amountIndex].Amount * change.CompletedCycles));
            }

            return new BuildingProductionChange(
                building.Id,
                change.CompletedCycles,
                change.InitialProgress,
                change.FinalProgress,
                consumed,
                produced,
                CaptureBlockers(building, committed.Inputs[index], committed.Outputs[index]));
        }

        private static ProductionBlocker[] CaptureBlockers(BuildingSlot building, long[] inputs, long[] outputs)
        {
            if (!building.HasProduction)
            {
                return Array.Empty<ProductionBlocker>();
            }

            var blockers = new List<ProductionBlocker>();
            if (!building.IsActive)
            {
                blockers.Add(new ProductionBlocker(ProductionBlockerKind.Inactive, null, 0, 0));
            }

            for (var index = 0; index < building.InputAmounts.Length; index++)
            {
                var amount = building.InputAmounts[index];
                var available = inputs[building.InputIndices[index]];
                if (available < amount.Amount)
                {
                    blockers.Add(new ProductionBlocker(
                        ProductionBlockerKind.MissingInput,
                        amount.ResourceId,
                        amount.Amount,
                        available));
                }
            }

            for (var index = 0; index < building.OutputAmounts.Length; index++)
            {
                var amount = building.OutputAmounts[index];
                var resourceIndex = building.OutputIndices[index];
                var available = checked(building.OutputResources[resourceIndex].Capacity - outputs[resourceIndex]);
                if (available < amount.Amount)
                {
                    blockers.Add(new ProductionBlocker(
                        ProductionBlockerKind.InsufficientOutputCapacity,
                        amount.ResourceId,
                        amount.Amount,
                        available));
                }
            }

            return blockers.ToArray();
        }

        private sealed class StateBuffer
        {
            internal StateBuffer(int buildingCount)
            {
                Inputs = new long[buildingCount][];
                Outputs = new long[buildingCount][];
                Progress = new long[buildingCount];
            }

            internal long[][] Inputs { get; }
            internal long[][] Outputs { get; }
            internal long[] Progress { get; }
        }

        private sealed class ChangeSlot
        {
            internal long CompletedCycles;
            internal long InitialProgress;
            internal long FinalProgress;
        }

        private sealed class ResourceSlot
        {
            internal ResourceSlot(ResourceId id, long capacity)
            {
                Id = id;
                Capacity = capacity;
            }

            internal ResourceId Id { get; }
            internal long Capacity { get; }
        }

        private sealed class BuildingSlot
        {
            private readonly BuildingDefinition definition;
            private readonly RecipeDefinition recipe;
            private readonly GridPosition anchor;
            private readonly int orientationQuarterTurns;

            internal BuildingSlot(BuildingInstanceState building)
            {
                Id = building.Id;
                definition = building.Definition;
                anchor = building.Anchor;
                orientationQuarterTurns = building.OrientationQuarterTurns;
                IsActive = building.IsActive;
                InputResources = CreateResources(building.InputBuffer);
                OutputResources = CreateResources(building.OutputBuffer);
                HasProduction = building.Production != null;
                if (!HasProduction)
                {
                    InputAmounts = Array.Empty<ResourceAmountDefinition>();
                    OutputAmounts = Array.Empty<ResourceAmountDefinition>();
                    InputIndices = Array.Empty<int>();
                    OutputIndices = Array.Empty<int>();
                    return;
                }

                recipe = building.Production.Definition;
                DurationTicks = recipe.DurationTicks;
                InputAmounts = recipe.Inputs.ToArray();
                OutputAmounts = recipe.Outputs.ToArray();
                InputIndices = ResolveIndices(InputAmounts, InputResources);
                OutputIndices = ResolveIndices(OutputAmounts, OutputResources);
            }

            internal BuildingInstanceId Id { get; }
            internal bool IsActive { get; }
            internal bool HasProduction { get; }
            internal long DurationTicks { get; }
            internal ResourceSlot[] InputResources { get; }
            internal ResourceSlot[] OutputResources { get; }
            internal ResourceAmountDefinition[] InputAmounts { get; }
            internal ResourceAmountDefinition[] OutputAmounts { get; }
            internal int[] InputIndices { get; }
            internal int[] OutputIndices { get; }

            internal long[] CopyInputQuantities(BuildingInstanceState building)
            {
                return CopyQuantities(InputResources, building.InputBuffer);
            }

            internal long[] CopyOutputQuantities(BuildingInstanceState building)
            {
                return CopyQuantities(OutputResources, building.OutputBuffer);
            }

            internal BuildingInstanceState Capture(long[] inputs, long[] outputs, long progress)
            {
                var inputBuffer = definition.InputBuffer == null
                    ? null
                    : new ResourceBufferState(definition.InputBuffer, ToQuantities(InputResources, inputs));
                var outputBuffer = definition.OutputBuffer == null
                    ? null
                    : new ResourceBufferState(definition.OutputBuffer, ToQuantities(OutputResources, outputs));
                var production = HasProduction ? new ProductionState(recipe, progress) : null;

                return new BuildingInstanceState(
                    Id,
                    definition,
                    anchor,
                    orientationQuarterTurns,
                    IsActive,
                    inputBuffer,
                    outputBuffer,
                    production);
            }

            private static ResourceSlot[] CreateResources(ResourceBufferState buffer)
            {
                if (buffer == null)
                {
                    return Array.Empty<ResourceSlot>();
                }

                return buffer.Definition.Capacities
                    .OrderBy(pair => pair.Key)
                    .Select(pair => new ResourceSlot(pair.Key, pair.Value))
                    .ToArray();
            }

            private static int[] ResolveIndices(
                ResourceAmountDefinition[] amounts,
                ResourceSlot[] resources)
            {
                var indices = new int[amounts.Length];
                for (var amountIndex = 0; amountIndex < amounts.Length; amountIndex++)
                {
                    indices[amountIndex] = -1;
                    for (var resourceIndex = 0; resourceIndex < resources.Length; resourceIndex++)
                    {
                        if (resources[resourceIndex].Id == amounts[amountIndex].ResourceId)
                        {
                            indices[amountIndex] = resourceIndex;
                            break;
                        }
                    }

                    if (indices[amountIndex] < 0)
                    {
                        throw new ArgumentException("A recipe references a resource absent from its building buffer.");
                    }
                }

                return indices;
            }

            private static long[] CopyQuantities(ResourceSlot[] resources, ResourceBufferState buffer)
            {
                var quantities = new long[resources.Length];
                for (var index = 0; index < resources.Length; index++)
                {
                    quantities[index] = buffer.Quantity(resources[index].Id);
                }

                return quantities;
            }

            private static KeyValuePair<ResourceId, long>[] ToQuantities(ResourceSlot[] resources, long[] values)
            {
                var quantities = new KeyValuePair<ResourceId, long>[resources.Length];
                for (var index = 0; index < resources.Length; index++)
                {
                    quantities[index] = new KeyValuePair<ResourceId, long>(resources[index].Id, values[index]);
                }

                return quantities;
            }
        }
    }

    public static class ProductionSimulator
    {
        public static ProductionAdvanceStatus TryPrepare(
            FactoryState source,
            out ProductionSimulationSession session)
        {
            session = null;
            try
            {
                var validated = ValidateAndCopyFactory(source);
                session = new ProductionSimulationSession(validated);
                return ProductionAdvanceStatus.Success;
            }
            catch (OverflowException)
            {
                return ProductionAdvanceStatus.ArithmeticOverflow;
            }
            catch (Exception exception) when (IsInvalidStateException(exception))
            {
                return ProductionAdvanceStatus.InvalidFactoryState;
            }
        }

        public static ProductionAdvanceResult Advance(FactoryState factory, long elapsedTicks)
        {
            if (elapsedTicks < 0)
            {
                return ProductionAdvanceResult.Failed(ProductionAdvanceStatus.InvalidElapsedTicks);
            }

            ProductionSimulationSession session;
            var preparationStatus = TryPrepare(factory, out session);
            if (preparationStatus != ProductionAdvanceStatus.Success)
            {
                return ProductionAdvanceResult.Failed(preparationStatus);
            }

            session.Advance(elapsedTicks);
            return session.CaptureResult();
        }

        private static FactoryState ValidateAndCopyFactory(FactoryState factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            ValidateCatalog(factory.Catalog);
            var orderedBuildings = factory.Buildings.Values.OrderBy(building => building.Id).ToArray();
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

        internal static bool IsInvalidStateException(Exception exception)
        {
            return exception is ArgumentException ||
                   exception is InvalidOperationException ||
                   exception is KeyNotFoundException;
        }
    }
}
