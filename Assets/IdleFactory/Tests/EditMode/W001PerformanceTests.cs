using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Simulation;
using IdleFactory.Tests.EditMode.Fixtures;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    public sealed class W001PerformanceTests
    {
        private const int WarmupCount = 100;
        private const int SampleCount = 500;
        private const long ActiveAdvanceTicks = TimeSpan.TicksPerMillisecond * 100L;
        private const long CappedOfflineAdvanceTicks = 230400000000L;
        private const long ActiveAdvanceBudgetNanoseconds = 10000000L;
        private const long OfflineAdvanceBudgetNanoseconds = 100000000L;

        private static readonly ResourceId InputResource = new ResourceId(FoundationFixtures.InputResourceId);
        private static readonly ResourceId OutputResource = new ResourceId(FoundationFixtures.OutputResourceId);

        [Test]
        public void W001MeasuresWarmedCoreAdvanceAndZeroManagedAllocations()
        {
            Assert.That(UnityEngine.Application.isEditor, Is.True, "W001 is measured in the Unity Editor Edit Mode runner.");

            var fixtureStart = Stopwatch.GetTimestamp();
            var source = FoundationFixtures.CreateW001();
            var fixtureConstructionNanoseconds = ElapsedNanoseconds(fixtureStart, Stopwatch.GetTimestamp());
            var fixtureValidationStart = Stopwatch.GetTimestamp();
            AssertW001Workload(source);
            var fixtureValidationNanoseconds = ElapsedNanoseconds(fixtureValidationStart, Stopwatch.GetTimestamp());

            TestContext.Out.WriteLine(DescribeEnvironment(fixtureConstructionNanoseconds, fixtureValidationNanoseconds));

            var active = MeasureAdvance(source, ActiveAdvanceTicks, "active-100ms");
            var offline = MeasureAdvance(source, CappedOfflineAdvanceTicks, "capped-offline-6.4h");
            WriteReport(active, ActiveAdvanceBudgetNanoseconds);
            WriteReport(offline, OfflineAdvanceBudgetNanoseconds);
            AssertAllocationSamplesAreZero(active);
            AssertAllocationSamplesAreZero(offline);
            Assert.That(
                active.TimingP95Nanoseconds,
                Is.LessThanOrEqualTo(ActiveAdvanceBudgetNanoseconds),
                "W001 active Advance p95 must meet the Windows 10 ms regression target.");
            Assert.That(
                offline.TimingP95Nanoseconds,
                Is.LessThanOrEqualTo(OfflineAdvanceBudgetNanoseconds),
                "W001 capped offline Advance p95 must meet the Windows 100 ms regression target.");
        }

        private static void AssertW001Workload(FactoryState source)
        {
            var buildings = source.Buildings.Values.ToArray();
            Assert.That(buildings, Has.Length.EqualTo(FoundationFixtures.W001MachineCount));
            Assert.That(source.Occupancy.Count, Is.EqualTo(4000));

            var definition = source.Catalog.Resolve(new BuildingDefinitionId(FoundationFixtures.W001BuildingId));
            var recipe = source.Catalog.Resolve(new RecipeId(FoundationFixtures.BasicRecipeId));
            Assert.That(definition.InputBuffer.Capacities[InputResource], Is.EqualTo(9216L));
            Assert.That(definition.OutputBuffer.Capacities[OutputResource], Is.EqualTo(4608L));
            Assert.That(recipe.Inputs.Count, Is.EqualTo(1));
            Assert.That(recipe.Inputs[0].Amount, Is.EqualTo(2L));
            Assert.That(recipe.Outputs.Count, Is.EqualTo(1));
            Assert.That(recipe.Outputs[0].Amount, Is.EqualTo(1L));
            Assert.That(recipe.DurationTicks, Is.EqualTo(FoundationFixtures.RecipeDurationTicks));
            Assert.That(
                CappedOfflineAdvanceTicks,
                Is.EqualTo(64L * TimeSpan.TicksPerHour / 10L),
                "The offline measurement is exactly 6.4 hours of Core ticks.");

            var activeUnblocked = 0;
            var activeOutputBlocked = 0;
            var inactive = 0;
            foreach (var building in buildings)
            {
                Assert.That(building.InputBuffer.Quantity(InputResource), Is.EqualTo(9216L));
                Assert.That(building.Production.ProgressTicks, Is.Zero);

                var blockers = building.GetProductionBlockers();
                if (!building.IsActive)
                {
                    inactive++;
                    Assert.That(building.OutputBuffer.Quantity(OutputResource), Is.Zero);
                    Assert.That(blockers.Count, Is.EqualTo(1));
                    Assert.That(blockers[0].Kind, Is.EqualTo(ProductionBlockerKind.Inactive));
                }
                else if (building.OutputBuffer.Quantity(OutputResource) == 4608L)
                {
                    activeOutputBlocked++;
                    Assert.That(blockers.Count, Is.EqualTo(1));
                    Assert.That(blockers[0].Kind, Is.EqualTo(ProductionBlockerKind.InsufficientOutputCapacity));
                }
                else
                {
                    activeUnblocked++;
                    Assert.That(building.OutputBuffer.Quantity(OutputResource), Is.Zero);
                    Assert.That(blockers.Count, Is.Zero);
                }
            }

            Assert.That(activeUnblocked, Is.EqualTo(500));
            Assert.That(activeOutputBlocked, Is.EqualTo(250));
            Assert.That(inactive, Is.EqualTo(250));
        }

        private static AdvanceMeasurements MeasureAdvance(FactoryState source, long elapsedTicks, string label)
        {
            RunWarmups(source, elapsedTicks, WarmupCount);

            var measurements = new AdvanceMeasurements(label, elapsedTicks, SampleCount);
            for (var sample = 0; sample < SampleCount; sample++)
            {
                var prepareStart = Stopwatch.GetTimestamp();
                var prepareStatus = ProductionSimulator.TryPrepare(source, out var session);
                var prepareEnd = Stopwatch.GetTimestamp();
                measurements.PreparationNanoseconds[sample] = ElapsedNanoseconds(prepareStart, prepareEnd);
                Assert.That(prepareStatus, Is.EqualTo(ProductionAdvanceStatus.Success));

                var advanceStart = Stopwatch.GetTimestamp();
                var advanceStatus = session.Advance(elapsedTicks);
                var advanceEnd = Stopwatch.GetTimestamp();
                measurements.TimingNanoseconds[sample] = ElapsedNanoseconds(advanceStart, advanceEnd);
                Assert.That(advanceStatus, Is.EqualTo(ProductionAdvanceStatus.Success));

                var captureStart = Stopwatch.GetTimestamp();
                var result = session.CaptureResult();
                var captureEnd = Stopwatch.GetTimestamp();
                measurements.CaptureNanoseconds[sample] = ElapsedNanoseconds(captureStart, captureEnd);
                Assert.That(result.IsSuccess, Is.True);

                var checksumStart = Stopwatch.GetTimestamp();
                var snapshot = SemanticFactorySnapshot.Capture(result.Candidate);
                var checksumEnd = Stopwatch.GetTimestamp();
                measurements.ChecksumNanoseconds[sample] = ElapsedNanoseconds(checksumStart, checksumEnd);
                AssertStableResult(measurements, snapshot);
            }

            RunWarmups(source, elapsedTicks, WarmupCount);
            measurements.AllocationThreadId = Thread.CurrentThread.ManagedThreadId;
            for (var sample = 0; sample < SampleCount; sample++)
            {
                var prepareStatus = ProductionSimulator.TryPrepare(source, out var session);
                Assert.That(prepareStatus, Is.EqualTo(ProductionAdvanceStatus.Success));

                var advanceStatus = ProductionAdvanceStatus.InvalidFactoryState;
                measurements.AllocationCounts[sample] = GcAllocationCounter.Count(
                    () => advanceStatus = session.Advance(elapsedTicks));
                Assert.That(
                    Thread.CurrentThread.ManagedThreadId,
                    Is.EqualTo(measurements.AllocationThreadId),
                    "All allocation samples must run synchronously on one managed thread.");
                Assert.That(advanceStatus, Is.EqualTo(ProductionAdvanceStatus.Success));

                var result = session.CaptureResult();
                Assert.That(result.IsSuccess, Is.True);
                var snapshot = SemanticFactorySnapshot.Capture(result.Candidate);
                AssertStableResult(measurements, snapshot);
            }

            measurements.CalculateStatistics();
            AssertExpectedSnapshotShape(measurements.ReferenceSnapshot, elapsedTicks);
            return measurements;
        }

        private static void RunWarmups(FactoryState source, long elapsedTicks, int count)
        {
            for (var warmup = 0; warmup < count; warmup++)
            {
                var prepareStatus = ProductionSimulator.TryPrepare(source, out var session);
                Assert.That(prepareStatus, Is.EqualTo(ProductionAdvanceStatus.Success));
                Assert.That(session.Advance(elapsedTicks), Is.EqualTo(ProductionAdvanceStatus.Success));
            }
        }

        private static void AssertStableResult(AdvanceMeasurements measurements, SemanticFactorySnapshot snapshot)
        {
            if (measurements.ReferenceSnapshot == null)
            {
                measurements.ReferenceSnapshot = snapshot;
                return;
            }

            Assert.That(snapshot.Equals(measurements.ReferenceSnapshot), Is.True,
                "Independent W001 samples must produce the same complete semantic state.");
            Assert.That(snapshot.Checksum, Is.EqualTo(measurements.ReferenceSnapshot.Checksum));
        }

        private static void AssertExpectedSnapshotShape(SemanticFactorySnapshot snapshot, long elapsedTicks)
        {
            Assert.That(snapshot.Buildings.Count, Is.EqualTo(1000));
            var inputTotal = 0L;
            var fullOutputs = 0;
            var inactive = 0;
            var progressed = 0;
            foreach (var building in snapshot.Buildings)
            {
                var input = building.Inputs[0].Quantity;
                var output = building.Outputs[0].Quantity;
                Assert.That(building.Outputs[0].Capacity, Is.EqualTo(4608L));
                inputTotal = checked(inputTotal + input);
                if (building.IsActive && output == 4608L)
                {
                    fullOutputs++;
                }
                if (!building.IsActive)
                {
                    inactive++;
                }
                if (building.ProgressTicks > 0)
                {
                    progressed++;
                }
            }

            Assert.That(inactive, Is.EqualTo(250));
            if (elapsedTicks == ActiveAdvanceTicks)
            {
                Assert.That(fullOutputs, Is.EqualTo(250));
                Assert.That(inputTotal, Is.EqualTo(1000L * 9216L));
                Assert.That(progressed, Is.EqualTo(500));
            }
            else
            {
                Assert.That(fullOutputs, Is.EqualTo(750));
                Assert.That(inputTotal, Is.EqualTo(500L * 9216L));
                Assert.That(progressed, Is.Zero);
            }
        }

        private static void WriteReport(AdvanceMeasurements measurements, long budgetNanoseconds)
        {
            TestContext.Out.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "W001 {0}: advanceTicks={1}; samples={2}; p50={3} ns; p95={4} ns; max={5} ns; budgetP95={6} ns; " +
                    "prepare-p50={7} ns; prepare-p95={8} ns; CaptureResult-p50={9} ns; CaptureResult-p95={10} ns; " +
                    "semantic-capture+checksum-p50={11} ns; semantic-capture+checksum-p95={12} ns; allocation-samples={13}; " +
                    "allocation-thread={14}; nonzero-allocation-samples={15}; allocation-p50={16} allocations; " +
                    "allocation-p95={17} allocations; allocation-min={18} allocations; allocation-max={19} allocations; checksum={20}",
                    measurements.Label,
                    measurements.ElapsedTicks,
                    SampleCount,
                    measurements.TimingP50Nanoseconds,
                    measurements.TimingP95Nanoseconds,
                    measurements.TimingMaximumNanoseconds,
                    budgetNanoseconds,
                    measurements.PreparationP50Nanoseconds,
                    measurements.PreparationP95Nanoseconds,
                    measurements.CaptureP50Nanoseconds,
                    measurements.CaptureP95Nanoseconds,
                    measurements.ChecksumP50Nanoseconds,
                    measurements.ChecksumP95Nanoseconds,
                    SampleCount,
                    measurements.AllocationThreadId,
                    measurements.NonzeroAllocationSamples,
                    measurements.AllocationP50,
                    measurements.AllocationP95,
                    measurements.AllocationMinimum,
                    measurements.AllocationMaximum,
                    measurements.ReferenceSnapshot.Checksum));

            TestContext.Out.WriteLine("W001 " + measurements.Label + " timing-samples-ns=" + Join(measurements.TimingNanoseconds));
        }

        private static void AssertAllocationSamplesAreZero(AdvanceMeasurements measurements)
        {
            for (var sample = 0; sample < measurements.AllocationCounts.Length; sample++)
            {
                Assert.That(
                    measurements.AllocationCounts[sample],
                    Is.Zero,
                    $"{measurements.Label} managed allocation sample {sample} around warmed Advance must be zero.");
            }
        }

        private static string DescribeEnvironment(long fixtureConstructionNanoseconds, long fixtureValidationNanoseconds)
        {
            var objectAssembly = typeof(object).GetTypeInfo().Assembly;
            var coreLibrary = objectAssembly.GetName().Name;
            var backend = string.Equals(coreLibrary, "mscorlib", StringComparison.Ordinal)
                ? "Mono managed runtime (Unity Editor)"
                : string.Equals(coreLibrary, "System.Private.CoreLib", StringComparison.Ordinal)
                    ? "CoreCLR managed runtime (Unity Editor)"
                    : "Unity Editor managed runtime; core library " + coreLibrary;

            return string.Format(
                CultureInfo.InvariantCulture,
                "W001 environment: runUtc={0}; unity={1}; runtime={2}; CLR={3}; imageRuntime={4}; backend={5}; " +
                "mode=Unity Editor Edit Mode tests (no Player build); debugBuild={6}; platform={7}; OS={8}; " +
                "CPU={9}; logicalCores={10}; memoryMiB={11}; StopwatchFrequency={12}; " +
                "fixtureConstruction={13} ns; fixtureValidation={14} ns; " +
                "allocationCounter=Unity GC.Alloc profiler recorder (managed allocation count on this thread; excludes native allocations)",
                DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                UnityEngine.Application.unityVersion,
                RuntimeInformation.FrameworkDescription,
                Environment.Version,
                objectAssembly.ImageRuntimeVersion,
                backend,
                UnityEngine.Debug.isDebugBuild,
                UnityEngine.Application.platform,
                SystemInfo.operatingSystem,
                SystemInfo.processorType,
                SystemInfo.processorCount,
                SystemInfo.systemMemorySize,
                Stopwatch.Frequency,
                fixtureConstructionNanoseconds,
                fixtureValidationNanoseconds);
        }

        private static long ElapsedNanoseconds(long startTimestamp, long endTimestamp)
        {
            return (long)((endTimestamp - startTimestamp) * (1000000000d / Stopwatch.Frequency));
        }

        private static long Percentile(long[] samples, double percentile)
        {
            var ordered = (long[])samples.Clone();
            Array.Sort(ordered);
            var rank = (int)Math.Ceiling(percentile * ordered.Length);
            return ordered[Math.Max(0, rank - 1)];
        }

        private static long Maximum(long[] samples)
        {
            var maximum = 0L;
            for (var index = 0; index < samples.Length; index++)
            {
                if (samples[index] > maximum)
                {
                    maximum = samples[index];
                }
            }

            return maximum;
        }

        private static long Minimum(long[] samples)
        {
            var minimum = long.MaxValue;
            for (var index = 0; index < samples.Length; index++)
            {
                if (samples[index] < minimum)
                {
                    minimum = samples[index];
                }
            }

            return minimum;
        }

        private static string Join(long[] values)
        {
            var builder = new StringBuilder(values.Length * 8);
            for (var index = 0; index < values.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }
                builder.Append(values[index].ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private sealed class AdvanceMeasurements
        {
            public AdvanceMeasurements(string label, long elapsedTicks, int sampleCount)
            {
                Label = label;
                ElapsedTicks = elapsedTicks;
                TimingNanoseconds = new long[sampleCount];
                PreparationNanoseconds = new long[sampleCount];
                CaptureNanoseconds = new long[sampleCount];
                ChecksumNanoseconds = new long[sampleCount];
                AllocationCounts = new long[sampleCount];
            }

            public string Label { get; }
            public long ElapsedTicks { get; }
            public long[] TimingNanoseconds { get; }
            public long[] PreparationNanoseconds { get; }
            public long[] CaptureNanoseconds { get; }
            public long[] ChecksumNanoseconds { get; }
            public long[] AllocationCounts { get; }
            public SemanticFactorySnapshot ReferenceSnapshot { get; set; }
            public long TimingP50Nanoseconds { get; private set; }
            public long TimingP95Nanoseconds { get; private set; }
            public long TimingMaximumNanoseconds { get; private set; }
            public long PreparationP50Nanoseconds { get; private set; }
            public long PreparationP95Nanoseconds { get; private set; }
            public long CaptureP50Nanoseconds { get; private set; }
            public long CaptureP95Nanoseconds { get; private set; }
            public long ChecksumP50Nanoseconds { get; private set; }
            public long ChecksumP95Nanoseconds { get; private set; }
            public int NonzeroAllocationSamples { get; private set; }
            public int AllocationThreadId { get; set; }
            public long AllocationP50 { get; private set; }
            public long AllocationP95 { get; private set; }
            public long AllocationMinimum { get; private set; }
            public long AllocationMaximum { get; private set; }

            public void CalculateStatistics()
            {
                TimingP50Nanoseconds = Percentile(TimingNanoseconds, 0.50d);
                TimingP95Nanoseconds = Percentile(TimingNanoseconds, 0.95d);
                TimingMaximumNanoseconds = Maximum(TimingNanoseconds);
                PreparationP50Nanoseconds = Percentile(PreparationNanoseconds, 0.50d);
                PreparationP95Nanoseconds = Percentile(PreparationNanoseconds, 0.95d);
                CaptureP50Nanoseconds = Percentile(CaptureNanoseconds, 0.50d);
                CaptureP95Nanoseconds = Percentile(CaptureNanoseconds, 0.95d);
                ChecksumP50Nanoseconds = Percentile(ChecksumNanoseconds, 0.50d);
                ChecksumP95Nanoseconds = Percentile(ChecksumNanoseconds, 0.95d);
                AllocationP50 = Percentile(AllocationCounts, 0.50d);
                AllocationP95 = Percentile(AllocationCounts, 0.95d);
                AllocationMinimum = Minimum(AllocationCounts);
                AllocationMaximum = Maximum(AllocationCounts);
                for (var index = 0; index < AllocationCounts.Length; index++)
                {
                    if (AllocationCounts[index] != 0)
                    {
                        NonzeroAllocationSamples++;
                    }
                }
            }
        }
    }
}
