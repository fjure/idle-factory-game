using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleFactory.Domain;
using IdleFactory.Simulation;
using IdleFactory.Tests.EditMode.Fixtures;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class ProductionSimulationTests
    {
        private const long HalfCycle = 25000000L;
        private const long FullCycle = FoundationFixtures.RecipeDurationTicks;

        private static readonly ResourceId InputId = new ResourceId(FoundationFixtures.InputResourceId);
        private static readonly ResourceId OutputId = new ResourceId(FoundationFixtures.OutputResourceId);

        [Test]
        public void D001HalfCycleChangesOnlyProgressThenCompletesExactAtomicExchange()
        {
            var original = FoundationFixtures.CreateD001();

            var first = ProductionSimulator.Advance(original, HalfCycle);

            AssertSuccess(first);
            AssertState(first.Candidate, input: 4, output: 0, progress: HalfCycle);
            AssertState(original, input: 4, output: 0, progress: 0);
            AssertChange(first, cycles: 0, consumed: 0, produced: 0, progress: HalfCycle);

            var second = ProductionSimulator.Advance(first.Candidate, HalfCycle);

            AssertSuccess(second);
            AssertState(second.Candidate, input: 2, output: 1, progress: 0);
            AssertState(first.Candidate, input: 4, output: 0, progress: HalfCycle);
            AssertChange(second, cycles: 1, consumed: 2, produced: 1, progress: 0);
            Assert.That(second.Summary.Buildings.Single().Blockers.Single().Kind,
                Is.EqualTo(ProductionBlockerKind.InsufficientOutputCapacity));
        }

        [Test]
        public void D001SingleFiveSecondAdvanceCompletesOneCycle()
        {
            var result = ProductionSimulator.Advance(FoundationFixtures.CreateD001(), FullCycle);

            AssertSuccess(result);
            AssertState(result.Candidate, input: 2, output: 1, progress: 0);
            AssertChange(result, cycles: 1, consumed: 2, produced: 1, progress: 0);
        }

        [Test]
        public void CompletionThatCreatesBlockerDiscardsLaterElapsedInSameAdvance()
        {
            var result = ProductionSimulator.Advance(
                FoundationFixtures.CreateD001(),
                checked(FullCycle + HalfCycle));

            AssertSuccess(result);
            AssertState(result.Candidate, input: 2, output: 1, progress: 0);
            AssertChange(result, cycles: 1, consumed: 2, produced: 1, progress: 0);
        }

        [Test]
        public void CompletionRetainsExactRemainderWhenNextCycleCanProgress()
        {
            var result = ProductionSimulator.Advance(
                FoundationFixtures.CreateMultiCycle(),
                checked(FullCycle + HalfCycle));

            AssertSuccess(result);
            AssertState(result.Candidate, input: 2, output: 1, progress: HalfCycle);
            AssertChange(result, cycles: 1, consumed: 2, produced: 1, progress: HalfCycle);
            Assert.That(result.Summary.Buildings.Single().Blockers, Is.Empty);
        }

        [TestCase(ProductionBlockerKind.MissingInput, 36000000000L)]
        [TestCase(ProductionBlockerKind.MissingInput, 864000000000L)]
        [TestCase(ProductionBlockerKind.MissingInput, 6048000000000L)]
        [TestCase(ProductionBlockerKind.InsufficientOutputCapacity, 36000000000L)]
        [TestCase(ProductionBlockerKind.InsufficientOutputCapacity, 864000000000L)]
        [TestCase(ProductionBlockerKind.InsufficientOutputCapacity, 6048000000000L)]
        [TestCase(ProductionBlockerKind.Inactive, 36000000000L)]
        [TestCase(ProductionBlockerKind.Inactive, 864000000000L)]
        [TestCase(ProductionBlockerKind.Inactive, 6048000000000L)]
        public void BlockedOrInactiveElapsedPreservesExistingProgressAndResources(
            ProductionBlockerKind blockerKind,
            long elapsedTicks)
        {
            var factory = FoundationFixtures.CreateD001();
            var building = factory.Buildings.Values.Single();
            SetProgress(building.Production, HalfCycle);
            ConfigureBlocker(building, blockerKind);
            var beforeInput = building.InputBuffer.Quantity(InputId);
            var beforeOutput = building.OutputBuffer.Quantity(OutputId);

            var result = ProductionSimulator.Advance(factory, elapsedTicks);

            AssertSuccess(result);
            AssertState(result.Candidate, beforeInput, beforeOutput, HalfCycle);
            AssertState(factory, beforeInput, beforeOutput, HalfCycle);
            Assert.That(
                result.Summary.Buildings.Single().Blockers.Select(blocker => blocker.Kind),
                Does.Contain(blockerKind));
        }

        [TestCase(ProductionBlockerKind.MissingInput)]
        [TestCase(ProductionBlockerKind.InsufficientOutputCapacity)]
        [TestCase(ProductionBlockerKind.Inactive)]
        public void ResolvingBlockerResumesOnlyFromRetainedProgress(
            ProductionBlockerKind blockerKind)
        {
            var factory = FoundationFixtures.CreateD001();
            var building = factory.Buildings.Values.Single();
            SetProgress(building.Production, HalfCycle);
            ConfigureBlocker(building, blockerKind);
            var stopped = ProductionSimulator.Advance(factory, TimeSpan.FromDays(7).Ticks);
            var resumedBuilding = stopped.Candidate.Buildings.Values.Single();
            ResolveBlocker(resumedBuilding, blockerKind);

            var almost = ProductionSimulator.Advance(stopped.Candidate, checked(HalfCycle - 1));
            var completed = ProductionSimulator.Advance(almost.Candidate, 1);

            AssertSuccess(almost);
            AssertState(almost.Candidate, input: 4, output: 0, progress: FullCycle - 1);
            AssertSuccess(completed);
            AssertState(completed.Candidate, input: 2, output: 1, progress: 0);
        }

        [Test]
        public void BulkAdvanceCompletesMultipleCyclesWithoutCycleReplay()
        {
            var result = ProductionSimulator.Advance(
                FoundationFixtures.CreateMultiCycle(),
                checked(2 * FullCycle));

            AssertSuccess(result);
            AssertState(result.Candidate, input: 0, output: 2, progress: 0);
            AssertChange(result, cycles: 2, consumed: 4, produced: 2, progress: 0);
        }

        [Test]
        public void InputAndOutputBoundsLimitBulkCyclesAtCompletionBoundaries()
        {
            var inputBound = CreateFactory(input: 2, inputCapacity: 4, output: 0, outputCapacity: 4);
            var outputBound = CreateFactory(input: 8, inputCapacity: 8, output: 1, outputCapacity: 2);
            var longElapsed = checked(4 * FullCycle + HalfCycle);

            var byInput = ProductionSimulator.Advance(inputBound, longElapsed);
            var byOutput = ProductionSimulator.Advance(outputBound, longElapsed);

            AssertSuccess(byInput);
            AssertState(byInput.Candidate, input: 0, output: 1, progress: 0);
            AssertChange(byInput, cycles: 1, consumed: 2, produced: 1, progress: 0);
            AssertSuccess(byOutput);
            AssertState(byOutput.Candidate, input: 6, output: 2, progress: 0);
            AssertChange(byOutput, cycles: 1, consumed: 2, produced: 1, progress: 0);
        }

        [Test]
        public void ZeroElapsedReturnsEquivalentIndependentCandidateAndCompleteSummary()
        {
            var original = FoundationFixtures.CreateD001();

            var result = ProductionSimulator.Advance(original, 0);

            AssertSuccess(result);
            Assert.That(result.Candidate, Is.Not.SameAs(original));
            Assert.That(result.Summary.ElapsedTicks, Is.Zero);
            AssertState(result.Candidate, input: 4, output: 0, progress: 0);
            AssertChange(result, cycles: 0, consumed: 0, produced: 0, progress: 0);
        }

        [Test]
        public void NegativeElapsedReturnsTypedFailureWithoutCandidateOrSummary()
        {
            var original = FoundationFixtures.CreateD001();

            var result = ProductionSimulator.Advance(original, -1);

            Assert.That(result.Status, Is.EqualTo(ProductionAdvanceStatus.InvalidElapsedTicks));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Candidate, Is.Null);
            Assert.That(result.Summary, Is.Null);
            AssertState(original, input: 4, output: 0, progress: 0);
        }

        [Test]
        public void MaximumInt64ElapsedUsesBoundedArithmeticAndStopsAtFirstBlocker()
        {
            var result = ProductionSimulator.Advance(FoundationFixtures.CreateD001(), long.MaxValue);

            AssertSuccess(result);
            AssertState(result.Candidate, input: 2, output: 1, progress: 0);
            AssertChange(result, cycles: 1, consumed: 2, produced: 1, progress: 0);
        }

        [Test]
        public void InvalidLaterMachineRejectsWholeFactoryWithoutExposingCandidate()
        {
            var factory = CreateFactoryWithTwoBuildings(reverse: false);
            var ordered = factory.Buildings.Values.OrderBy(building => building.Id).ToArray();
            SetProgress(ordered[1].Production, FullCycle, bypassValidation: true);

            var result = ProductionSimulator.Advance(factory, FullCycle);

            Assert.That(result.Status, Is.EqualTo(ProductionAdvanceStatus.InvalidFactoryState));
            Assert.That(result.Candidate, Is.Null);
            Assert.That(result.Summary, Is.Null);
            Assert.That(ordered[0].InputBuffer.Quantity(InputId), Is.EqualTo(4));
            Assert.That(ordered[0].OutputBuffer.Quantity(OutputId), Is.Zero);
            Assert.That(ordered[0].Production.ProgressTicks, Is.Zero);

            SetProgress(ordered[1].Production, 0, bypassValidation: true);
            var retry = ProductionSimulator.Advance(factory, FullCycle);
            AssertSuccess(retry);
            Assert.That(retry.Candidate.Buildings.Values.All(building =>
                building.InputBuffer.Quantity(InputId) == 2 &&
                building.OutputBuffer.Quantity(OutputId) == 1), Is.True);
        }

        [Test]
        public void InvalidCatalogDurationAndBufferQuantityAreRejectedBeforeAdvancement()
        {
            var invalidDuration = CreateFactoryWithTwoBuildings(reverse: false);
            SetBackingField(invalidDuration.Catalog.Recipes.Values.Single(), "<DurationTicks>k__BackingField", 0L);
            var invalidBuffer = CreateFactoryWithTwoBuildings(reverse: false);
            var laterBuffer = invalidBuffer.Buildings.Values.OrderBy(building => building.Id).Last().InputBuffer;
            var quantitiesField = typeof(ResourceBufferState).GetField("quantities", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(quantitiesField, Is.Not.Null);
            var quantities = (IDictionary<ResourceId, long>)quantitiesField.GetValue(laterBuffer);
            quantities[InputId] = -1;

            var durationResult = ProductionSimulator.Advance(invalidDuration, FullCycle);
            var bufferResult = ProductionSimulator.Advance(invalidBuffer, FullCycle);

            Assert.That(durationResult.Status, Is.EqualTo(ProductionAdvanceStatus.InvalidFactoryState));
            Assert.That(durationResult.Candidate, Is.Null);
            Assert.That(bufferResult.Status, Is.EqualTo(ProductionAdvanceStatus.InvalidFactoryState));
            Assert.That(bufferResult.Candidate, Is.Null);
            var first = invalidBuffer.Buildings.Values.OrderBy(building => building.Id).First();
            Assert.That(first.InputBuffer.Quantity(InputId), Is.EqualTo(4));
            Assert.That(first.OutputBuffer.Quantity(OutputId), Is.Zero);
        }

        [Test]
        public void PartitionedAdvanceMatchesSingleAdvanceAcrossCycleBoundaries()
        {
            var whole = ProductionSimulator.Advance(FoundationFixtures.CreateMultiCycle(), checked(FullCycle + HalfCycle));
            var first = ProductionSimulator.Advance(FoundationFixtures.CreateMultiCycle(), 12345678L);
            var second = ProductionSimulator.Advance(first.Candidate, 37654322L);
            var partitioned = ProductionSimulator.Advance(second.Candidate, HalfCycle);

            AssertSuccess(whole);
            AssertSuccess(partitioned);
            AssertEquivalentState(whole.Candidate, partitioned.Candidate);
        }

        [Test]
        public void BuildingEnumerationOrderDoesNotAffectStateOrSummaryOrder()
        {
            var forward = ProductionSimulator.Advance(CreateFactoryWithTwoBuildings(reverse: false), FullCycle);
            var reverse = ProductionSimulator.Advance(CreateFactoryWithTwoBuildings(reverse: true), FullCycle);

            AssertSuccess(forward);
            AssertSuccess(reverse);
            AssertEquivalentState(forward.Candidate, reverse.Candidate);
            Assert.That(
                forward.Summary.Buildings.Select(change => change.BuildingId),
                Is.EqualTo(reverse.Summary.Buildings.Select(change => change.BuildingId)));
            Assert.That(
                forward.Summary.Buildings.Select(change => change.BuildingId),
                Is.Ordered);
        }

        [Test]
        public void ResultSummaryCollectionsAreImmutableAndExposeNoUnityDependency()
        {
            var result = ProductionSimulator.Advance(FoundationFixtures.CreateD001(), FullCycle);
            var summaryList = (IList<BuildingProductionChange>)result.Summary.Buildings;
            var consumedList = (IList<ProductionResourceChange>)result.Summary.Buildings.Single().ConsumedInputs;

            Assert.That(() => summaryList.Clear(), Throws.TypeOf<NotSupportedException>());
            Assert.That(() => consumedList.Clear(), Throws.TypeOf<NotSupportedException>());
            Assert.That(
                typeof(ProductionSimulator).Assembly.GetReferencedAssemblies().Select(reference => reference.Name),
                Has.None.StartsWith("UnityEngine"));
            Assert.That(
                typeof(ProductionSimulator).GetMethod(nameof(ProductionSimulator.Advance)).GetParameters()[1].ParameterType,
                Is.EqualTo(typeof(long)));
        }

        [Test]
        public void PreparedSessionCaptureRequiresAnAdvanceAndReturnsDetachedSnapshots()
        {
            ProductionSimulationSession session;
            Assert.That(
                ProductionSimulator.TryPrepare(FoundationFixtures.CreateMultiCycle(), out session),
                Is.EqualTo(ProductionAdvanceStatus.Success));
            Assert.That(() => session.CaptureResult(), Throws.InvalidOperationException);

            Assert.That(session.Advance(FullCycle), Is.EqualTo(ProductionAdvanceStatus.Success));
            var first = session.CaptureResult();
            AssertState(first.Candidate, input: 2, output: 1, progress: 0);

            Assert.That(session.Advance(FullCycle), Is.EqualTo(ProductionAdvanceStatus.Success));
            var second = session.CaptureResult();

            AssertState(first.Candidate, input: 2, output: 1, progress: 0);
            AssertState(second.Candidate, input: 0, output: 2, progress: 0);
            Assert.That(second.Candidate, Is.Not.SameAs(first.Candidate));
        }

        [Test]
        public void LatestFailedAttemptReplacesCaptureStatusAndValidRetryIsClean()
        {
            ProductionSimulationSession session;
            Assert.That(
                ProductionSimulator.TryPrepare(FoundationFixtures.CreateMultiCycle(), out session),
                Is.EqualTo(ProductionAdvanceStatus.Success));

            Assert.That(session.Advance(FullCycle), Is.EqualTo(ProductionAdvanceStatus.Success));
            var first = session.CaptureResult();
            Assert.That(session.Advance(-1), Is.EqualTo(ProductionAdvanceStatus.InvalidElapsedTicks));

            var failed = session.CaptureResult();
            Assert.That(failed.Status, Is.EqualTo(ProductionAdvanceStatus.InvalidElapsedTicks));
            Assert.That(failed.Candidate, Is.Null);
            Assert.That(failed.Summary, Is.Null);

            Assert.That(session.Advance(FullCycle), Is.EqualTo(ProductionAdvanceStatus.Success));
            var retried = session.CaptureResult();
            AssertState(first.Candidate, input: 2, output: 1, progress: 0);
            AssertState(retried.Candidate, input: 0, output: 2, progress: 0);
        }

        [Test]
        public void LateMachineFailureRollsBackEarlierStagingAndValidRetryIsClean()
        {
            ProductionSimulationSession session;
            Assert.That(
                ProductionSimulator.TryPrepare(CreateFactoryWithTwoBuildings(reverse: false), out session),
                Is.EqualTo(ProductionAdvanceStatus.Success));
            Assert.That(session.Advance(0), Is.EqualTo(ProductionAdvanceStatus.Success));
            var beforeFailure = session.CaptureResult();

            var buildingsField = typeof(ProductionSimulationSession).GetField(
                "buildings",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildingsField, Is.Not.Null);
            var preparedBuildings = (Array)buildingsField.GetValue(session);
            var laterBuilding = preparedBuildings.GetValue(1);
            var outputResourcesField = laterBuilding.GetType().GetField(
                "<OutputResources>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(outputResourcesField, Is.Not.Null);
            var outputResources = (Array)outputResourcesField.GetValue(laterBuilding);
            var laterOutputResource = outputResources.GetValue(0);

            SetBackingField(laterBuilding, "<OutputAmounts>k__BackingField", new[]
            {
                new ResourceAmountDefinition(OutputId, long.MaxValue),
                new ResourceAmountDefinition(OutputId, long.MaxValue),
            });
            SetBackingField(laterBuilding, "<OutputIndices>k__BackingField", new[] { 0, 0 });
            SetBackingField(laterOutputResource, "<Capacity>k__BackingField", long.MaxValue);

            Assert.That(session.Advance(FullCycle), Is.EqualTo(ProductionAdvanceStatus.ArithmeticOverflow));
            var failed = session.CaptureResult();
            Assert.That(failed.Status, Is.EqualTo(ProductionAdvanceStatus.ArithmeticOverflow));
            Assert.That(failed.Candidate, Is.Null);
            Assert.That(failed.Summary, Is.Null);

            SetBackingField(laterBuilding, "<OutputAmounts>k__BackingField", new[]
            {
                new ResourceAmountDefinition(OutputId, 1),
            });
            SetBackingField(laterBuilding, "<OutputIndices>k__BackingField", new[] { 0 });
            SetBackingField(laterOutputResource, "<Capacity>k__BackingField", 1L);
            Assert.That(session.Advance(FullCycle), Is.EqualTo(ProductionAdvanceStatus.Success));
            var retried = session.CaptureResult();

            Assert.That(beforeFailure.Candidate.Buildings.Values.All(building =>
                building.InputBuffer.Quantity(InputId) == 4 &&
                building.OutputBuffer.Quantity(OutputId) == 0 &&
                building.Production.ProgressTicks == 0), Is.True);
            Assert.That(retried.Candidate.Buildings.Values.All(building =>
                building.InputBuffer.Quantity(InputId) == 2 &&
                building.OutputBuffer.Quantity(OutputId) == 1 &&
                building.Production.ProgressTicks == 0), Is.True);
        }

        [Test]
        public void NegativeStaticAdvancePrecedesInvalidFactoryValidation()
        {
            var result = ProductionSimulator.Advance(null, -1);

            Assert.That(result.Status, Is.EqualTo(ProductionAdvanceStatus.InvalidElapsedTicks));
            Assert.That(result.Candidate, Is.Null);
            Assert.That(result.Summary, Is.Null);
        }

        [Test]
        public void PreparedAndStaticPathsReturnEquivalentStateAndSummary()
        {
            var source = FoundationFixtures.CreateMultiCycle();
            var direct = ProductionSimulator.Advance(source, checked(FullCycle + HalfCycle));
            ProductionSimulationSession session;
            Assert.That(ProductionSimulator.TryPrepare(source, out session), Is.EqualTo(ProductionAdvanceStatus.Success));

            Assert.That(session.Advance(checked(FullCycle + HalfCycle)), Is.EqualTo(ProductionAdvanceStatus.Success));
            var prepared = session.CaptureResult();

            AssertEquivalentResult(direct, prepared);
        }

        [Test]
        public void WarmedPreparedAdvanceAllocatesNoManagedBytes()
        {
            ProductionSimulationSession session;
            Assert.That(
                ProductionSimulator.TryPrepare(CreateFactory(input: 1000, inputCapacity: 1000, output: 0, outputCapacity: 500), out session),
                Is.EqualTo(ProductionAdvanceStatus.Success));
            for (var warmup = 0; warmup < 100; warmup++)
            {
                Assert.That(session.Advance(1), Is.EqualTo(ProductionAdvanceStatus.Success));
            }

            var statuses = new ProductionAdvanceStatus[500];
            var allocatedBytes = new long[500];
            for (var sample = 0; sample < allocatedBytes.Length; sample++)
            {
                var status = ProductionAdvanceStatus.InvalidFactoryState;
                allocatedBytes[sample] = GcAllocationCounter.Count(() => status = session.Advance(1));
                statuses[sample] = status;
            }

            for (var sample = 0; sample < allocatedBytes.Length; sample++)
            {
                Assert.That(statuses[sample], Is.EqualTo(ProductionAdvanceStatus.Success));
                Assert.That(allocatedBytes[sample], Is.Zero, $"Prepared Advance allocated during sample {sample}.");
            }
        }

        private static FactoryState CreateFactory(
            long input,
            long inputCapacity,
            long output,
            long outputCapacity,
            long progress = 0,
            bool active = true)
        {
            var catalog = CreateCatalog(inputCapacity, outputCapacity);
            var definition = catalog.Buildings.Values.Single();
            var building = new BuildingInstanceState(
                Id(1),
                definition,
                new GridPosition(0, 0),
                0,
                active,
                new ResourceBufferState(definition.InputBuffer, Quantities(InputId, input)),
                new ResourceBufferState(definition.OutputBuffer, Quantities(OutputId, output)),
                new ProductionState(catalog.Recipes.Values.Single(), progress));
            return new FactoryState(
                new FactoryBounds(new GridPosition(0, 0), 16, 16),
                catalog,
                new[] { building });
        }

        private static FactoryState CreateFactoryWithTwoBuildings(bool reverse)
        {
            var catalog = CreateCatalog(4, 1);
            var definition = catalog.Buildings.Values.Single();
            var recipe = catalog.Recipes.Values.Single();
            var first = CreateBuilding(definition, recipe, Id(1), new GridPosition(0, 0));
            var second = CreateBuilding(definition, recipe, Id(2), new GridPosition(4, 0));
            return new FactoryState(
                new FactoryBounds(new GridPosition(0, 0), 16, 16),
                catalog,
                reverse ? new[] { second, first } : new[] { first, second });
        }

        private static BuildingInstanceState CreateBuilding(
            BuildingDefinition definition,
            RecipeDefinition recipe,
            BuildingInstanceId id,
            GridPosition anchor)
        {
            return new BuildingInstanceState(
                id,
                definition,
                anchor,
                0,
                true,
                new ResourceBufferState(definition.InputBuffer, Quantities(InputId, 4)),
                new ResourceBufferState(definition.OutputBuffer, Quantities(OutputId, 0)),
                new ProductionState(recipe, 0));
        }

        private static DefinitionCatalog CreateCatalog(long inputCapacity, long outputCapacity)
        {
            var recipeId = new RecipeId(FoundationFixtures.BasicRecipeId);
            var recipe = new RecipeDefinition(
                recipeId,
                new[] { new ResourceAmountDefinition(InputId, 2) },
                new[] { new ResourceAmountDefinition(OutputId, 1) },
                FullCycle,
                true);
            var inputDefinition = new BufferDefinition(BufferRole.Input, Quantities(InputId, inputCapacity));
            var outputDefinition = new BufferDefinition(BufferRole.Output, Quantities(OutputId, outputCapacity));
            var building = new BuildingDefinition(
                new BuildingDefinitionId(FoundationFixtures.D001BuildingId),
                new Footprint(2, 2),
                new[] { 0 },
                recipeId,
                inputDefinition,
                outputDefinition,
                "dev.machine.sprite",
                true);
            return new DefinitionCatalog(
                new[]
                {
                    new ResourceDefinition(InputId, "Input", null, true),
                    new ResourceDefinition(OutputId, "Output", null, true),
                },
                new[] { recipe },
                new[] { building },
                new[]
                {
                    new OfflinePolicyDefinition(
                        new OfflinePolicyRevision(1),
                        4,
                        5,
                        288000000000L),
                });
        }

        private static void ConfigureBlocker(BuildingInstanceState building, ProductionBlockerKind kind)
        {
            switch (kind)
            {
                case ProductionBlockerKind.MissingInput:
                    Assert.That(Remove(building.InputBuffer, InputId, 4), Is.EqualTo(BufferOperationResult.Success));
                    break;
                case ProductionBlockerKind.InsufficientOutputCapacity:
                    Assert.That(Add(building.OutputBuffer, OutputId, 1), Is.EqualTo(BufferOperationResult.Success));
                    break;
                case ProductionBlockerKind.Inactive:
                    SetActive(building, false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static void ResolveBlocker(BuildingInstanceState building, ProductionBlockerKind kind)
        {
            switch (kind)
            {
                case ProductionBlockerKind.MissingInput:
                    Assert.That(Add(building.InputBuffer, InputId, 4), Is.EqualTo(BufferOperationResult.Success));
                    break;
                case ProductionBlockerKind.InsufficientOutputCapacity:
                    Assert.That(Remove(building.OutputBuffer, OutputId, 1), Is.EqualTo(BufferOperationResult.Success));
                    break;
                case ProductionBlockerKind.Inactive:
                    SetActive(building, true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static void AssertSuccess(ProductionAdvanceResult result)
        {
            Assert.That(result.Status, Is.EqualTo(ProductionAdvanceStatus.Success));
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Candidate, Is.Not.Null);
            Assert.That(result.Summary, Is.Not.Null);
        }

        private static void AssertChange(
            ProductionAdvanceResult result,
            long cycles,
            long consumed,
            long produced,
            long progress)
        {
            var change = result.Summary.Buildings.Single();
            Assert.That(change.CompletedCycles, Is.EqualTo(cycles));
            Assert.That(change.ConsumedInputs.Single().ResourceId, Is.EqualTo(InputId));
            Assert.That(change.ConsumedInputs.Single().Amount, Is.EqualTo(consumed));
            Assert.That(change.ProducedOutputs.Single().ResourceId, Is.EqualTo(OutputId));
            Assert.That(change.ProducedOutputs.Single().Amount, Is.EqualTo(produced));
            Assert.That(change.FinalProgressTicks, Is.EqualTo(progress));
        }

        private static void AssertState(FactoryState factory, long input, long output, long progress)
        {
            var building = factory.Buildings.Values.Single();
            Assert.That(building.InputBuffer.Quantity(InputId), Is.EqualTo(input));
            Assert.That(building.OutputBuffer.Quantity(OutputId), Is.EqualTo(output));
            Assert.That(building.Production.ProgressTicks, Is.EqualTo(progress));
        }

        private static void AssertEquivalentState(FactoryState expected, FactoryState actual)
        {
            var expectedBuildings = expected.Buildings.Values.OrderBy(building => building.Id).ToArray();
            var actualBuildings = actual.Buildings.Values.OrderBy(building => building.Id).ToArray();
            Assert.That(actualBuildings.Select(building => building.Id),
                Is.EqualTo(expectedBuildings.Select(building => building.Id)));
            for (var index = 0; index < expectedBuildings.Length; index++)
            {
                Assert.That(actualBuildings[index].InputBuffer.Quantity(InputId),
                    Is.EqualTo(expectedBuildings[index].InputBuffer.Quantity(InputId)));
                Assert.That(actualBuildings[index].OutputBuffer.Quantity(OutputId),
                    Is.EqualTo(expectedBuildings[index].OutputBuffer.Quantity(OutputId)));
                Assert.That(actualBuildings[index].Production.ProgressTicks,
                    Is.EqualTo(expectedBuildings[index].Production.ProgressTicks));
                Assert.That(actualBuildings[index].GetProductionBlockers().Select(blocker => blocker.Kind),
                    Is.EqualTo(expectedBuildings[index].GetProductionBlockers().Select(blocker => blocker.Kind)));
            }
        }

        private static void AssertEquivalentResult(
            ProductionAdvanceResult expected,
            ProductionAdvanceResult actual)
        {
            Assert.That(actual.Status, Is.EqualTo(expected.Status));
            Assert.That(actual.IsSuccess, Is.EqualTo(expected.IsSuccess));
            AssertEquivalentState(expected.Candidate, actual.Candidate);
            Assert.That(actual.Summary.ElapsedTicks, Is.EqualTo(expected.Summary.ElapsedTicks));
            Assert.That(actual.Summary.Buildings.Count, Is.EqualTo(expected.Summary.Buildings.Count));
            for (var buildingIndex = 0; buildingIndex < expected.Summary.Buildings.Count; buildingIndex++)
            {
                var expectedChange = expected.Summary.Buildings[buildingIndex];
                var actualChange = actual.Summary.Buildings[buildingIndex];
                Assert.That(actualChange.BuildingId, Is.EqualTo(expectedChange.BuildingId));
                Assert.That(actualChange.CompletedCycles, Is.EqualTo(expectedChange.CompletedCycles));
                Assert.That(actualChange.InitialProgressTicks, Is.EqualTo(expectedChange.InitialProgressTicks));
                Assert.That(actualChange.FinalProgressTicks, Is.EqualTo(expectedChange.FinalProgressTicks));
                AssertEquivalentResourceChanges(expectedChange.ConsumedInputs, actualChange.ConsumedInputs);
                AssertEquivalentResourceChanges(expectedChange.ProducedOutputs, actualChange.ProducedOutputs);
                Assert.That(actualChange.Blockers.Count, Is.EqualTo(expectedChange.Blockers.Count));
                for (var blockerIndex = 0; blockerIndex < expectedChange.Blockers.Count; blockerIndex++)
                {
                    var expectedBlocker = expectedChange.Blockers[blockerIndex];
                    var actualBlocker = actualChange.Blockers[blockerIndex];
                    Assert.That(actualBlocker.Kind, Is.EqualTo(expectedBlocker.Kind));
                    Assert.That(actualBlocker.ResourceId, Is.EqualTo(expectedBlocker.ResourceId));
                    Assert.That(actualBlocker.Required, Is.EqualTo(expectedBlocker.Required));
                    Assert.That(actualBlocker.Available, Is.EqualTo(expectedBlocker.Available));
                }
            }
        }

        private static void AssertEquivalentResourceChanges(
            IReadOnlyList<ProductionResourceChange> expected,
            IReadOnlyList<ProductionResourceChange> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (var index = 0; index < expected.Count; index++)
            {
                Assert.That(actual[index].ResourceId, Is.EqualTo(expected[index].ResourceId));
                Assert.That(actual[index].Amount, Is.EqualTo(expected[index].Amount));
            }
        }

        private static KeyValuePair<ResourceId, long>[] Quantities(ResourceId id, long quantity)
        {
            return new[] { new KeyValuePair<ResourceId, long>(id, quantity) };
        }

        private static BuildingInstanceId Id(int value)
        {
            return new BuildingInstanceId(new Guid(value, 0, 0, new byte[8]));
        }

        private static BufferOperationResult Add(ResourceBufferState buffer, ResourceId id, long amount)
        {
            return InvokeBuffer(buffer, "TryAdd", id, amount);
        }

        private static BufferOperationResult Remove(ResourceBufferState buffer, ResourceId id, long amount)
        {
            return InvokeBuffer(buffer, "TryRemove", id, amount);
        }

        private static BufferOperationResult InvokeBuffer(
            ResourceBufferState buffer,
            string methodName,
            ResourceId id,
            long amount)
        {
            var method = typeof(ResourceBufferState).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (BufferOperationResult)method.Invoke(buffer, new object[] { id, amount });
        }

        private static void SetActive(BuildingInstanceState building, bool active)
        {
            var method = typeof(BuildingInstanceState).GetMethod("SetActive", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(building, new object[] { active });
        }

        private static void SetProgress(ProductionState production, long progress, bool bypassValidation = false)
        {
            if (bypassValidation)
            {
                var field = typeof(ProductionState).GetField("<ProgressTicks>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                field.SetValue(production, progress);
                return;
            }

            var method = typeof(ProductionState).GetMethod("SetProgressTicks", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(production, new object[] { progress });
        }

        private static void SetBackingField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
