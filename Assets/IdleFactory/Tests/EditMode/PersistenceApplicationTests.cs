using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Tests.EditMode.Fixtures;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class PersistenceApplicationTests
    {
        private const long CycleTicks = FoundationFixtures.RecipeDurationTicks;
        private static readonly DateTimeOffset Epoch =
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly BuildingInstanceId MachineId =
            new BuildingInstanceId(new Guid(1, 0, 0, new byte[8]));
        private static readonly ResourceId InputId = new ResourceId(FoundationFixtures.InputResourceId);
        private static readonly ResourceId OutputId = new ResourceId(FoundationFixtures.OutputResourceId);

        [Test]
        public void SaveNowCapturesOneUtcAndPromotesACoherentGeneration()
        {
            var clock = new TestClock(Epoch);
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));

            var saved = commands.SaveNow();

            Assert.That(saved.Succeeded, Is.True);
            Assert.That(saved.Value.CheckpointId, Is.EqualTo(Id(10)));
            Assert.That(saved.Value.Generation, Is.EqualTo(1));
            Assert.That(saved.Value.Factory, Is.Null);
            Assert.That(clock.CallCount, Is.EqualTo(1));
            Assert.That(store.PromoteCallCount, Is.EqualTo(1));
            Assert.That(store.Active.DepartureUtc, Is.EqualTo(Epoch));
            Assert.That(store.Active.Generation, Is.EqualTo(1));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(4));
            Assert.That(commands.GetPersistenceStatus().Status, Is.EqualTo(PersistenceStatusKind.Ready));
            Assert.That(commands.GetPersistenceStatus().ActiveCheckpointId, Is.EqualTo(Id(10)));
            Assert.That(commands.GetPersistenceStatus().Message, Does.Not.Contain("\\"));
        }

        [Test]
        public void FailedSaveLeavesLiveFactoryAndPreviousDurableCheckpointUsable()
        {
            var clock = new TestClock(Epoch, Epoch.AddSeconds(1));
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            var original = commands.SaveNow();
            Assert.That(original.Succeeded, Is.True);

            var advanced = new ProductionCommands(session).AdvanceSimulation(CycleTicks);
            Assert.That(advanced.Succeeded, Is.True);
            store.FailPromotionBeforeWrite = true;

            var failed = commands.SaveNow();

            Assert.That(failed.Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
            Assert.That(failed.Value, Is.Null);
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(2));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.EqualTo(1));
            Assert.That(store.Active.Id, Is.EqualTo(original.Value.CheckpointId.Value));
            Assert.That(store.Previous, Is.Null);
            Assert.That(commands.GetPersistenceStatus().Status, Is.EqualTo(PersistenceStatusKind.Failure));
            Assert.That(commands.GetPersistenceStatus().ActiveCheckpointId, Is.EqualTo(original.Value.CheckpointId));
        }

        [Test]
        public void SaveClockFailureDoesNotCreateCheckpointOrChangeLiveState()
        {
            var clock = new TestClock(Epoch) { ThrowOnCall = 1 };
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));

            var failed = commands.SaveNow();

            Assert.That(failed.Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
            Assert.That(store.PromoteCallCount, Is.Zero);
            Assert.That(store.Active, Is.Null);
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(4));
            Assert.That(commands.GetPersistenceStatus().Status, Is.EqualTo(PersistenceStatusKind.Failure));
        }

        [Test]
        public void DevelopmentLoadRestoresActiveCheckpointWithoutReadingClock()
        {
            var clock = new TestClock(Epoch);
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            var saved = commands.SaveNow();
            Assert.That(saved.Succeeded, Is.True);

            Assert.That(new ProductionCommands(session).AdvanceSimulation(CycleTicks).Succeeded, Is.True);
            var readsBeforeLoad = clock.CallCount;
            var loaded = commands.LoadDevelopmentCheckpoint();

            Assert.That(loaded.Succeeded, Is.True);
            Assert.That(loaded.Value.CheckpointId, Is.EqualTo(saved.Value.CheckpointId));
            Assert.That(loaded.Value.Generation, Is.EqualTo(1));
            Assert.That(loaded.Value.Factory.Buildings.Count, Is.EqualTo(1));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(4));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.Zero);
            Assert.That(Inspection(session).ProgressTicks, Is.Zero);
            Assert.That(clock.CallCount, Is.EqualTo(readsBeforeLoad));
            Assert.That(commands.GetLastOfflineSummary(), Is.Null);
        }

        [Test]
        public void MissingDevelopmentLoadPreservesCurrentFactoryAndReportsNoCheckpoint()
        {
            var clock = new TestClock(Epoch);
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            Assert.That(new ProductionCommands(session).AdvanceSimulation(CycleTicks).Succeeded, Is.True);
            var revision = session.GetFactorySnapshot().Revision;

            var missing = commands.LoadDevelopmentCheckpoint();

            Assert.That(missing.Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));
            Assert.That(missing.Value, Is.Null);
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(2));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.EqualTo(1));
            Assert.That(session.GetFactorySnapshot().Revision, Is.EqualTo(revision));
            Assert.That(commands.GetPersistenceStatus().Status, Is.EqualTo(PersistenceStatusKind.NoCheckpoint));
        }

        [Test]
        public void InvalidLoadedCheckpointDoesNotPartiallyReplaceLiveFactory()
        {
            var clock = new TestClock(Epoch);
            var store = new TestCheckpointStore { ActiveReadOutcome = CommandOutcome.InvalidCheckpoint };
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            Assert.That(new ProductionCommands(session).AdvanceSimulation(CycleTicks).Succeeded, Is.True);
            var revision = session.GetFactorySnapshot().Revision;

            var invalid = commands.LoadDevelopmentCheckpoint();

            Assert.That(invalid.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(invalid.Value, Is.Null);
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(2));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.EqualTo(1));
            Assert.That(session.GetFactorySnapshot().Revision, Is.EqualTo(revision));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
        }

        [Test]
        public void ColdStartPublishesOnlyAfterTheMatchingGenerationIsDurable()
        {
            var factory = FoundationFixtures.CreateD001();
            var store = new TestCheckpointStore();
            store.SeedActive(CreateCheckpoint(Id(20), 1, Epoch, factory));
            var clock = new TestClock(Epoch.AddSeconds(10));
            var session = new FactorySession(factory, factory.Catalog, clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(30));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Bootstrapping));
            var liveDuringPromotion = (FactorySnapshot)null;
            var mutationDuringPromotion = (CommandResult<AdvanceSimulationResult>) null;
            store.AfterPromotion = candidate =>
            {
                Assert.That(candidate.Id, Is.EqualTo(Id(30)));
                Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Reconciling));
                liveDuringPromotion = session.GetFactorySnapshot();
                mutationDuringPromotion = new ProductionCommands(session).AdvanceSimulation(CycleTicks);
            };

            var resumed = commands.ResumeAndReconcile();

            Assert.That(resumed.Succeeded, Is.True);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(liveDuringPromotion, Is.Not.Null);
            Assert.That(liveDuringPromotion.Buildings.Single().InstanceId, Is.EqualTo(MachineId));
            Assert.That(mutationDuringPromotion.Outcome, Is.EqualTo(CommandOutcome.LifecycleNoOp));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(2));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.EqualTo(1));
            Assert.That(Inspection(session).ProgressTicks, Is.Zero);
            Assert.That(store.Active.Id, Is.EqualTo(Id(30)));
            Assert.That(resumed.Value.CheckpointId, Is.EqualTo(store.Active.Id));
            Assert.That(resumed.Value.Generation, Is.EqualTo(2));
            Assert.That(resumed.Value.OfflineSummary.Outcome, Is.EqualTo(OfflineReturnOutcome.Applied));
            Assert.That(resumed.Value.OfflineSummary.MeasuredTicks, Is.EqualTo(100_000_000));
            Assert.That(resumed.Value.OfflineSummary.CreditedTicks, Is.EqualTo(100_000_000));
            Assert.That(resumed.Value.OfflineSummary.EffectiveTicks, Is.EqualTo(80_000_000));
            Assert.That(resumed.Value.OfflineSummary.SourceCheckpointId, Is.EqualTo(Id(20)));
            Assert.That(resumed.Value.OfflineSummary.ResultCheckpointId, Is.EqualTo(Id(30)));
            Assert.That(resumed.Value.OfflineSummary.ResourceDeltas.Single(delta => delta.ResourceId == InputId).Delta, Is.EqualTo(-2));
            Assert.That(resumed.Value.OfflineSummary.ResourceDeltas.Single(delta => delta.ResourceId == OutputId).Delta, Is.EqualTo(1));
            Assert.That(resumed.Value.OfflineSummary.Limitations.Single().Kind,
                Is.EqualTo(ProductionBlockerKind.InsufficientOutputCapacity));
            Assert.That(resumed.Value.UsedFallback, Is.False);
            Assert.That(commands.GetLastOfflineSummary().ResultCheckpointId, Is.EqualTo(Id(30)));
            Assert.That(clock.CallCount, Is.EqualTo(1));
            Assert.That(store.PromoteCallCount, Is.EqualTo(1));
        }

        [Test]
        public void ColdStartWithoutCheckpointKeepsPreparedScenarioWithZeroCredit()
        {
            var factory = FoundationFixtures.CreateD001();
            var clock = new TestClock(Epoch.AddHours(4));
            var store = new TestCheckpointStore();
            var session = new FactorySession(factory, factory.Catalog, clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));

            var resumed = commands.ResumeAndReconcile();

            Assert.That(resumed.Succeeded, Is.True);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(resumed.Value.CheckpointId, Is.Null);
            Assert.That(resumed.Value.UsedFallback, Is.False);
            Assert.That(resumed.Value.OfflineSummary.Outcome, Is.EqualTo(OfflineReturnOutcome.NoCheckpoint));
            Assert.That(resumed.Value.OfflineSummary.MeasuredTicks, Is.Zero);
            Assert.That(resumed.Value.OfflineSummary.CreditedTicks, Is.Zero);
            Assert.That(resumed.Value.OfflineSummary.EffectiveTicks, Is.Zero);
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(4));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.Zero);
            Assert.That(store.Active, Is.Null);
            Assert.That(commands.GetPersistenceStatus().Status, Is.EqualTo(PersistenceStatusKind.NoCheckpoint));
        }

        [Test]
        public void RepeatedSuspensionIsIdempotentAndFailedCheckpointUsesLastSuccessfulFallback()
        {
            var clock = new TestClock(Epoch, Epoch.AddSeconds(5), Epoch.AddSeconds(10));
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            var saved = commands.SaveNow();
            Assert.That(saved.Succeeded, Is.True);
            Assert.That(new ProductionCommands(session).AdvanceSimulation(CycleTicks).Succeeded, Is.True);
            store.FailPromotionBeforeWrite = true;

            var suspension = commands.RecordSuspension();
            var promotionCount = store.PromoteCallCount;
            var duplicate = commands.RecordSuspension();

            Assert.That(suspension.Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Suspended));
            Assert.That(duplicate.Succeeded, Is.True);
            Assert.That(duplicate.Value.WasNoOp, Is.True);
            Assert.That(store.PromoteCallCount, Is.EqualTo(promotionCount));
            Assert.That(store.Active.Id, Is.EqualTo(saved.Value.CheckpointId.Value));

            store.FailPromotionBeforeWrite = false;
            var resumed = commands.ResumeAndReconcile();

            Assert.That(resumed.Succeeded, Is.True);
            Assert.That(resumed.Value.UsedFallback, Is.True);
            Assert.That(resumed.Value.OfflineSummary.SourceCheckpointId, Is.EqualTo(saved.Value.CheckpointId));
            Assert.That(resumed.Value.OfflineSummary.MeasuredTicks, Is.EqualTo(100_000_000));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(2));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.EqualTo(1));
            Assert.That(Inspection(session).ProgressTicks, Is.Zero);
            Assert.That(clock.CallCount, Is.EqualTo(3));

            var repeatResume = commands.ResumeAndReconcile();
            Assert.That(repeatResume.Outcome, Is.EqualTo(CommandOutcome.LifecycleNoOp));
            Assert.That(commands.GetLastOfflineSummary().ResultCheckpointId, Is.EqualTo(resumed.Value.CheckpointId));
        }

        [Test]
        public void UncertainReturnPromotionStaysBlockedUntilRetryPublishesTheActiveCandidateOnce()
        {
            var clock = new TestClock(Epoch, Epoch.AddSeconds(10));
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            var suspension = commands.RecordSuspension();
            Assert.That(suspension.Succeeded, Is.True);
            var originalRevision = session.GetFactorySnapshot().Revision;
            store.FailPromotionAfterWrite = true;
            var factoryDuringUncertainPromotion = (FactorySnapshot)null;
            store.AfterPromotion = _ => factoryDuringUncertainPromotion = session.GetFactorySnapshot();

            var uncertain = commands.ResumeAndReconcile();

            Assert.That(uncertain.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(factoryDuringUncertainPromotion.Revision, Is.EqualTo(originalRevision));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(4));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.Zero);
            Assert.That(commands.GetLastOfflineSummary(), Is.Null);
            Assert.That(commands.GetPersistenceStatus().Status, Is.EqualTo(PersistenceStatusKind.Failure));
            var promotedId = store.Active.Id;
            var promotionCount = store.PromoteCallCount;

            var unconfirmed = commands.RetryReturn();

            Assert.That(unconfirmed.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(store.PromoteCallCount, Is.EqualTo(promotionCount + 1));
            Assert.That(session.GetFactorySnapshot().Revision, Is.EqualTo(originalRevision));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(4));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.Zero);
            Assert.That(commands.GetLastOfflineSummary(), Is.Null);

            store.FailPromotionAfterWrite = false;
            var retried = commands.RetryReturn();

            Assert.That(retried.Succeeded, Is.True);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(retried.Value.CheckpointId, Is.EqualTo(promotedId));
            Assert.That(store.PromoteCallCount, Is.EqualTo(promotionCount + 2));
            Assert.That(clock.CallCount, Is.EqualTo(2));
            Assert.That(commands.GetLastOfflineSummary().ResultCheckpointId, Is.EqualTo(promotedId));
            Assert.That(commands.GetPersistenceStatus().Status, Is.EqualTo(PersistenceStatusKind.Ready));
        }

        [Test]
        public void RetryConfirmsReconstructedActiveCandidateBeforePublishing()
        {
            var clock = new TestClock(Epoch, Epoch.AddSeconds(10));
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            Assert.That(commands.RecordSuspension().Succeeded, Is.True);
            store.FailPromotionAfterWrite = true;
            var uncertain = commands.ResumeAndReconcile();
            Assert.That(uncertain.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));

            var originalCandidate = store.Active;
            var reconstructedIdentity = Id(20);
            store.SeedActive(new Checkpoint(
                reconstructedIdentity,
                originalCandidate.Generation,
                originalCandidate.DepartureUtc,
                originalCandidate.OfflinePolicyRevision,
                originalCandidate.ReconciledFromCheckpointId,
                originalCandidate.CreateFactoryCopy()));
            var liveRevision = session.GetFactorySnapshot().Revision;
            var promotionCount = store.PromoteCallCount;

            var unconfirmed = commands.RetryReturn();

            Assert.That(unconfirmed.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(store.PromoteCallCount, Is.EqualTo(promotionCount + 1));
            Assert.That(session.GetFactorySnapshot().Revision, Is.EqualTo(liveRevision));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(4));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.Zero);
            Assert.That(commands.GetLastOfflineSummary(), Is.Null);
            Assert.That(new ProductionCommands(session).AdvanceSimulation(CycleTicks).Outcome,
                Is.EqualTo(CommandOutcome.ReturnBlocked));

            store.FailPromotionAfterWrite = false;
            var retried = commands.RetryReturn();

            Assert.That(retried.Succeeded, Is.True);
            Assert.That(retried.Value.CheckpointId, Is.EqualTo(reconstructedIdentity));
            Assert.That(store.PromoteCallCount, Is.EqualTo(promotionCount + 2));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(2));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.EqualTo(1));
            Assert.That(commands.GetLastOfflineSummary().ResultCheckpointId, Is.EqualTo(reconstructedIdentity));
        }

        [Test]
        public void RetryBindsOneNewUtcWhenTheInitialReturnClockReadFailed()
        {
            var clock = new TestClock(Epoch, Epoch.AddSeconds(10)) { ThrowOnCall = 2 };
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            var suspension = commands.RecordSuspension();
            Assert.That(suspension.Succeeded, Is.True);

            var failedReturn = commands.ResumeAndReconcile();

            Assert.That(failedReturn.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(clock.CallCount, Is.EqualTo(2));
            Assert.That(store.PromoteCallCount, Is.EqualTo(1));
            Assert.That(commands.GetLastOfflineSummary(), Is.Null);
            Assert.That(store.Active.Id, Is.EqualTo(suspension.Value.CheckpointId.Value));

            var retried = commands.RetryReturn();

            Assert.That(retried.Succeeded, Is.True);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(clock.CallCount, Is.EqualTo(3));
            Assert.That(store.PromoteCallCount, Is.EqualTo(2));
            Assert.That(retried.Value.OfflineSummary.SourceCheckpointId, Is.EqualTo(suspension.Value.CheckpointId));
            Assert.That(retried.Value.OfflineSummary.MeasuredTicks, Is.EqualTo(100_000_000));
            Assert.That(retried.Value.OfflineSummary.EffectiveTicks, Is.EqualTo(80_000_000));
            Assert.That(commands.GetLastOfflineSummary().ResultCheckpointId, Is.EqualTo(retried.Value.CheckpointId));
        }

        [Test]
        public void AuthoritativeReloadResolvesBlockedReturnWithoutPublishingCandidate()
        {
            var clock = new TestClock(Epoch, Epoch.AddSeconds(1), Epoch.AddSeconds(10));
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            var saved = commands.SaveNow();
            Assert.That(saved.Succeeded, Is.True);
            store.FailPromotionBeforeWrite = true;
            var suspension = commands.RecordSuspension();
            Assert.That(suspension.Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
            var failedReturn = commands.ResumeAndReconcile();
            Assert.That(failedReturn.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));
            store.FailPromotionBeforeWrite = false;
            var beforeReload = store.PromoteCallCount;

            var reloaded = commands.ReloadAuthoritativeGeneration();

            Assert.That(reloaded.Succeeded, Is.True);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(reloaded.Value.CheckpointId, Is.EqualTo(saved.Value.CheckpointId));
            Assert.That(store.PromoteCallCount, Is.EqualTo(beforeReload + 1));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(4));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.Zero);
            Assert.That(commands.GetLastOfflineSummary(), Is.Null);
            Assert.That(clock.CallCount, Is.EqualTo(3));
        }

        [Test]
        public void ReloadConfirmsReadableActiveDurabilityBeforePublishing()
        {
            var clock = new TestClock(Epoch, Epoch.AddSeconds(1), Epoch.AddSeconds(10));
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            Assert.That(commands.SaveNow().Succeeded, Is.True);
            Assert.That(commands.RecordSuspension().Succeeded, Is.True);
            store.FailPromotionAfterWrite = true;

            var uncertain = commands.ResumeAndReconcile();

            Assert.That(uncertain.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(store.Active.Id, Is.EqualTo(Id(12)));
            Assert.That(commands.GetLastOfflineSummary(), Is.Null);
            var liveRevision = session.GetFactorySnapshot().Revision;
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(4));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.Zero);
            var promotionCount = store.PromoteCallCount;

            var unconfirmedReload = commands.ReloadAuthoritativeGeneration();

            Assert.That(unconfirmedReload.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            Assert.That(store.PromoteCallCount, Is.EqualTo(promotionCount + 1));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(session.GetFactorySnapshot().Revision, Is.EqualTo(liveRevision));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(4));
            Assert.That(commands.GetLastOfflineSummary(), Is.Null);

            store.FailPromotionAfterWrite = false;
            var reloaded = commands.ReloadAuthoritativeGeneration();

            Assert.That(reloaded.Succeeded, Is.True);
            Assert.That(reloaded.Value.CheckpointId, Is.EqualTo(Id(12)));
            Assert.That(store.PromoteCallCount, Is.EqualTo(promotionCount + 2));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(2));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.EqualTo(1));
            Assert.That(commands.GetLastOfflineSummary(), Is.Null);
            Assert.That(commands.GetPersistenceStatus().Status, Is.EqualTo(PersistenceStatusKind.Ready));
            Assert.That(clock.CallCount, Is.EqualTo(3));
        }

        [Test]
        public void RecoverPreviousPromotesAValidatedCopyUnderANewIdentityAndGeneration()
        {
            var clock = new TestClock(Epoch, Epoch.AddSeconds(1), Epoch.AddSeconds(2));
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            var first = commands.SaveNow();
            Assert.That(first.Succeeded, Is.True);
            Assert.That(new ProductionCommands(session).AdvanceSimulation(CycleTicks).Succeeded, Is.True);
            var second = commands.SaveNow();
            Assert.That(second.Succeeded, Is.True);
            var liveBefore = Inspection(session);
            MachineInspectionSnapshot observedDuringPromotion = null;
            store.BeforeRecovery = candidate => observedDuringPromotion = Inspection(session);

            var recovered = commands.RecoverPreviousCheckpoint();

            Assert.That(recovered.Succeeded, Is.True);
            Assert.That(recovered.Value.CheckpointId, Is.EqualTo(Id(12)));
            Assert.That(recovered.Value.Generation, Is.EqualTo(3));
            Assert.That(store.Active.Id, Is.EqualTo(Id(12)));
            Assert.That(store.Active.ReconciledFromCheckpointId, Is.EqualTo(first.Value.CheckpointId));
            Assert.That(store.Active.DepartureUtc, Is.EqualTo(Epoch.AddSeconds(2)));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(4));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.Zero);
            Assert.That(observedDuringPromotion.Inputs.Single().Quantity, Is.EqualTo(liveBefore.Inputs.Single().Quantity));
            Assert.That(observedDuringPromotion.Outputs.Single().Quantity, Is.EqualTo(liveBefore.Outputs.Single().Quantity));
            Assert.That(store.Previous.Id, Is.EqualTo(second.Value.CheckpointId.Value));
            Assert.That(commands.GetPersistenceStatus().Status, Is.EqualTo(PersistenceStatusKind.Recovered));
        }

        [Test]
        public void FailedPreviousRecoveryPreservesCurrentAndActiveFactory()
        {
            var clock = new TestClock(Epoch, Epoch.AddSeconds(1), Epoch.AddSeconds(2));
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            Assert.That(commands.SaveNow().Succeeded, Is.True);
            Assert.That(new ProductionCommands(session).AdvanceSimulation(CycleTicks).Succeeded, Is.True);
            Assert.That(commands.SaveNow().Succeeded, Is.True);
            store.FailRecoveryBeforeWrite = true;
            var revision = session.GetFactorySnapshot().Revision;

            var failed = commands.RecoverPreviousCheckpoint();

            Assert.That(failed.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            Assert.That(store.Active.Id, Is.EqualTo(Id(11)));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(2));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.EqualTo(1));
            Assert.That(session.GetFactorySnapshot().Revision, Is.EqualTo(revision));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(new ProductionCommands(session).AdvanceSimulation(CycleTicks).Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
        }

        [Test]
        public void RecoveryPublishedBeforeDurabilityFailureBlocksMutationsUntilRetryConfirmsCandidate()
        {
            var clock = new TestClock(Epoch, Epoch.AddSeconds(1), Epoch.AddSeconds(2));
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            Assert.That(commands.SaveNow().Succeeded, Is.True);
            Assert.That(new ProductionCommands(session).AdvanceSimulation(CycleTicks).Succeeded, Is.True);
            Assert.That(commands.SaveNow().Succeeded, Is.True);
            var liveBefore = session.GetFactorySnapshot();
            store.FailRecoveryAfterWrite = true;

            var uncertain = commands.RecoverPreviousCheckpoint();

            Assert.That(uncertain.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(store.RecoveryCallCount, Is.EqualTo(1));
            Assert.That(store.Active.Id, Is.EqualTo(Id(12)));
            Assert.That(store.Active.Generation, Is.EqualTo(3));
            Assert.That(store.Previous.Id, Is.EqualTo(Id(11)));
            Assert.That(commands.GetPersistenceStatus().ActiveCheckpointId, Is.EqualTo(Id(11)));
            Assert.That(session.GetFactorySnapshot().Revision, Is.EqualTo(liveBefore.Revision));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(2));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.EqualTo(1));

            var deniedAdvance = new ProductionCommands(session).AdvanceSimulation(CycleTicks);
            var deniedMutation = new ProductionCommands(session).SetBuildingActive(MachineId, false);
            Assert.That(deniedAdvance.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            Assert.That(deniedMutation.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            Assert.That(session.GetFactorySnapshot().Revision, Is.EqualTo(liveBefore.Revision));
            Assert.That(store.Active.Id, Is.EqualTo(Id(12)));

            store.FailRecoveryAfterWrite = false;
            var retried = commands.RetryReturn();

            Assert.That(retried.Succeeded, Is.True);
            Assert.That(retried.Value.CheckpointId, Is.EqualTo(Id(12)));
            Assert.That(retried.Value.Generation, Is.EqualTo(3));
            Assert.That(store.RecoveryCallCount, Is.EqualTo(2));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(4));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.Zero);
            Assert.That(commands.GetPersistenceStatus().Status, Is.EqualTo(PersistenceStatusKind.Recovered));
            Assert.That(commands.GetPersistenceStatus().ActiveCheckpointId, Is.EqualTo(Id(12)));

            var resumedAdvance = new ProductionCommands(session).AdvanceSimulation(CycleTicks);
            Assert.That(resumedAdvance.Succeeded, Is.True);
        }

        [Test]
        public void RecoveryPublishedBeforeInvalidCheckpointFailureAlsoBlocksUntilRetry()
        {
            var clock = new TestClock(Epoch, Epoch.AddSeconds(1), Epoch.AddSeconds(2));
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            Assert.That(commands.SaveNow().Succeeded, Is.True);
            Assert.That(new ProductionCommands(session).AdvanceSimulation(CycleTicks).Succeeded, Is.True);
            Assert.That(commands.SaveNow().Succeeded, Is.True);
            store.FailRecoveryAfterWrite = true;
            store.RecoveryAfterWriteFailureOutcome = CommandOutcome.InvalidCheckpoint;

            var uncertain = commands.RecoverPreviousCheckpoint();

            Assert.That(uncertain.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(store.Active.Id, Is.EqualTo(Id(12)));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(2));
            Assert.That(new ProductionCommands(session).SetBuildingActive(MachineId, false).Outcome,
                Is.EqualTo(CommandOutcome.ReturnBlocked));

            store.FailRecoveryAfterWrite = false;
            var retried = commands.RetryReturn();

            Assert.That(retried.Succeeded, Is.True);
            Assert.That(retried.Value.CheckpointId, Is.EqualTo(Id(12)));
            Assert.That(store.RecoveryCallCount, Is.EqualTo(2));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
        }

        [Test]
        public void ReloadAfterAmbiguousRecoveryReadsActiveAndRemainsBlockedOnInvalidActive()
        {
            var clock = new TestClock(Epoch, Epoch.AddSeconds(1), Epoch.AddSeconds(2));
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            Assert.That(commands.SaveNow().Succeeded, Is.True);
            Assert.That(new ProductionCommands(session).AdvanceSimulation(CycleTicks).Succeeded, Is.True);
            Assert.That(commands.SaveNow().Succeeded, Is.True);
            store.FailRecoveryAfterWrite = true;
            var uncertain = commands.RecoverPreviousCheckpoint();
            Assert.That(uncertain.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            var liveRevision = session.GetFactorySnapshot().Revision;
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(2));

            store.ActiveReadOutcome = CommandOutcome.InvalidCheckpoint;
            var failedReload = commands.ReloadAuthoritativeGeneration();

            Assert.That(failedReload.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            Assert.That(store.RecoveryCallCount, Is.EqualTo(1));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(session.GetFactorySnapshot().Revision, Is.EqualTo(liveRevision));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(2));

            store.ActiveReadOutcome = CommandOutcome.Success;
            store.FailRecoveryAfterWrite = false;
            var reloaded = commands.ReloadAuthoritativeGeneration();

            Assert.That(reloaded.Succeeded, Is.True);
            Assert.That(store.RecoveryCallCount, Is.EqualTo(1));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(4));
            Assert.That(Inspection(session).Outputs.Single().Quantity, Is.Zero);
            Assert.That(commands.GetPersistenceStatus().Status, Is.EqualTo(PersistenceStatusKind.Ready));
        }

        [Test]
        public void FailedReloadKeepsPendingRecoveryCandidateForRetry()
        {
            var clock = new TestClock(Epoch, Epoch.AddSeconds(1), Epoch.AddSeconds(2));
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            Assert.That(commands.SaveNow().Succeeded, Is.True);
            Assert.That(new ProductionCommands(session).AdvanceSimulation(CycleTicks).Succeeded, Is.True);
            Assert.That(commands.SaveNow().Succeeded, Is.True);
            store.FailRecoveryAfterWrite = true;
            Assert.That(commands.RecoverPreviousCheckpoint().Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            store.ActiveReadOutcome = CommandOutcome.InvalidCheckpoint;

            var failedReload = commands.ReloadAuthoritativeGeneration();

            Assert.That(failedReload.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            Assert.That(store.RecoveryCallCount, Is.EqualTo(1));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));

            store.ActiveReadOutcome = CommandOutcome.Success;
            store.FailRecoveryAfterWrite = false;
            var retried = commands.RetryReturn();

            Assert.That(retried.Succeeded, Is.True);
            Assert.That(store.RecoveryCallCount, Is.EqualTo(2));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(4));
        }

        [Test]
        public void RecoveryReadFailureIsTypedAndReadsPreviousOnlyOnce()
        {
            var clock = new TestClock(Epoch, Epoch.AddSeconds(1));
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            Assert.That(commands.SaveNow().Succeeded, Is.True);
            Assert.That(new ProductionCommands(session).AdvanceSimulation(CycleTicks).Succeeded, Is.True);
            Assert.That(commands.SaveNow().Succeeded, Is.True);
            var activeId = store.Active.Id;
            var revision = session.GetFactorySnapshot().Revision;
            var previousReadsBefore = store.PreviousReadCallCount;
            store.PreviousReadException = new InvalidOperationException("Fixture store failure.");

            var failed = commands.RecoverPreviousCheckpoint();

            Assert.That(failed.Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(store.PreviousReadCallCount - previousReadsBefore, Is.EqualTo(1));
            Assert.That(store.Active.Id, Is.EqualTo(activeId));
            Assert.That(session.GetFactorySnapshot().Revision, Is.EqualTo(revision));
            Assert.That(Inspection(session).Inputs.Single().Quantity, Is.EqualTo(2));
            Assert.That(commands.GetPersistenceStatus().Status, Is.EqualTo(PersistenceStatusKind.Failure));
        }

        [Test]
        public void ResetDevelopmentScenarioRebuildsCanonicalD001WithoutChangingSavedCheckpoint()
        {
            var clock = new TestClock(Epoch);
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateD001(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            var saved = commands.SaveNow();
            Assert.That(saved.Succeeded, Is.True);
            Assert.That(new ProductionCommands(session).AdvanceSimulation(CycleTicks).Succeeded, Is.True);

            var reset = commands.ResetDevelopmentScenario();

            Assert.That(reset.Succeeded, Is.True);
            Assert.That(reset.Value.Factory.Bounds, Is.EqualTo(new FactoryBounds(new GridPosition(0, 0), 16, 16)));
            Assert.That(reset.Value.Factory.Buildings.Count, Is.EqualTo(1));
            var machine = reset.Value.Factory.Buildings.Single();
            Assert.That(machine.InstanceId, Is.Not.EqualTo(MachineId));
            Assert.That(machine.InstanceId.Uuid, Is.Not.EqualTo(Guid.Empty));
            Assert.That(machine.DefinitionId, Is.EqualTo(new BuildingDefinitionId("dev.machine.basic")));
            Assert.That(machine.Anchor, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(machine.OrientationQuarterTurns, Is.Zero);
            Assert.That(machine.IsActive, Is.True);
            var resetInspection = session.GetMachineInspection(machine.InstanceId);
            Assert.That(resetInspection.Succeeded, Is.True);
            Assert.That(resetInspection.Value.Inputs.Single().Quantity, Is.EqualTo(4));
            Assert.That(resetInspection.Value.Inputs.Single().RequiredOrFreeCapacity, Is.EqualTo(2));
            Assert.That(resetInspection.Value.Outputs.Single().Quantity, Is.Zero);
            Assert.That(resetInspection.Value.Outputs.Single().RequiredOrFreeCapacity, Is.EqualTo(1));
            Assert.That(resetInspection.Value.ProgressTicks, Is.Zero);
            Assert.That(store.Active.Id, Is.EqualTo(saved.Value.CheckpointId.Value));
            Assert.That(store.Active.CreateFactoryCopy().Buildings.Count, Is.EqualTo(1));
            Assert.That(store.Active.CreateFactoryCopy().Buildings.Values.Single().Id, Is.EqualTo(MachineId));
            Assert.That(clock.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void ResetDefinitionFailurePreservesTheLiveFactory()
        {
            var clock = new TestClock(Epoch);
            var store = new TestCheckpointStore();
            var session = CreateRunningSession(FoundationFixtures.CreateAlternateFootprint(), clock, store);
            var commands = new PersistenceCommands(session, new TestCheckpointIdSource(10));
            var before = session.GetFactorySnapshot();

            var reset = commands.ResetDevelopmentScenario();
            var after = session.GetFactorySnapshot();

            Assert.That(reset.Outcome, Is.EqualTo(CommandOutcome.InvalidDefinition));
            Assert.That(reset.Value, Is.Null);
            Assert.That(after.Revision, Is.EqualTo(before.Revision));
            Assert.That(after.Bounds, Is.EqualTo(before.Bounds));
            Assert.That(after.Buildings.Count, Is.EqualTo(before.Buildings.Count));
            Assert.That(after.Buildings.Single().InstanceId, Is.EqualTo(before.Buildings.Single().InstanceId));
            Assert.That(after.Occupancy.Count, Is.EqualTo(before.Occupancy.Count));
        }

        private static FactorySession CreateRunningSession(
            FactoryState factory,
            TestClock clock,
            TestCheckpointStore store)
        {
            var created = DevelopmentSessionFactory.CreateRunning(factory, factory.Catalog, clock, store);
            Assert.That(created.Succeeded, Is.True, created.Message);
            return created.Value;
        }

        private static Checkpoint CreateCheckpoint(
            CheckpointId id,
            long generation,
            DateTimeOffset departureUtc,
            FactoryState factory) =>
            new Checkpoint(
                id,
                generation,
                departureUtc,
                new OfflinePolicyRevision(DefinitionCatalog.SupportedOfflinePolicyRevision),
                null,
                factory);

        private static MachineInspectionSnapshot Inspection(FactorySession session)
        {
            var result = session.GetMachineInspection(MachineId);
            Assert.That(result.Succeeded, Is.True, result.Message);
            return result.Value;
        }

        private static CheckpointId Id(int value) =>
            new CheckpointId(new Guid(value, 0, 0, new byte[8]));

        private sealed class TestClock : IUtcClock
        {
            private readonly DateTimeOffset[] readings;

            public TestClock(params DateTimeOffset[] readings)
            {
                this.readings = readings ?? Array.Empty<DateTimeOffset>();
            }

            public int CallCount { get; private set; }
            public int ThrowOnCall { get; set; }

            public DateTimeOffset GetUtcNow()
            {
                CallCount++;
                if (CallCount == ThrowOnCall)
                {
                    throw new InvalidOperationException("Clock fixture failure.");
                }

                return readings.Length == 0
                    ? Epoch
                    : readings[Math.Min(CallCount - 1, readings.Length - 1)];
            }
        }

        private sealed class TestCheckpointIdSource : ICheckpointIdSource
        {
            private int next;

            public TestCheckpointIdSource(int firstValue)
            {
                next = firstValue;
            }

            public CommandResult<CheckpointId> Next()
            {
                return CommandResult<CheckpointId>.Success(Id(next++));
            }
        }

        private sealed class TestCheckpointStore : ICheckpointStore
        {
            public Checkpoint Active { get; private set; }
            public Checkpoint Previous { get; private set; }
            public int PromoteCallCount { get; private set; }
            public int RecoveryCallCount { get; private set; }
            public int PreviousReadCallCount { get; private set; }
            public bool FailPromotionBeforeWrite { get; set; }
            public bool FailPromotionAfterWrite { get; set; }
            public bool FailRecoveryBeforeWrite { get; set; }
            public bool FailRecoveryAfterWrite { get; set; }
            public CommandOutcome RecoveryAfterWriteFailureOutcome { get; set; } = CommandOutcome.PersistenceFailed;
            public CommandOutcome ActiveReadOutcome { get; set; } = CommandOutcome.Success;
            public Exception PreviousReadException { get; set; }
            public Action<Checkpoint> BeforePromotion { get; set; }
            public Action<Checkpoint> AfterPromotion { get; set; }
            public Action<Checkpoint> BeforeRecovery { get; set; }

            public void SeedActive(Checkpoint checkpoint) => Active = Clone(checkpoint);

            public CheckpointReadResult ReadActive()
            {
                if (ActiveReadOutcome != CommandOutcome.Success)
                {
                    return CheckpointReadResult.Rejected(ActiveReadOutcome, "Active read fixture failure.");
                }

                return Active == null
                    ? CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "No active fixture.")
                    : CheckpointReadResult.Success(Clone(Active));
            }

            public CheckpointReadResult ReadPrevious()
            {
                PreviousReadCallCount++;
                if (PreviousReadException != null)
                {
                    throw PreviousReadException;
                }

                return Previous == null
                    ? CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "No previous fixture.")
                    : CheckpointReadResult.Success(Clone(Previous));
            }

            public CheckpointReadResult Read(CheckpointId checkpointId)
            {
                if (Active != null && Active.Id == checkpointId) return CheckpointReadResult.Success(Clone(Active));
                if (Previous != null && Previous.Id == checkpointId) return CheckpointReadResult.Success(Clone(Previous));
                return CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "Checkpoint fixture not found.");
            }

            public CommandResult<CheckpointId> Promote(Checkpoint checkpoint)
            {
                PromoteCallCount++;
                if (FailPromotionBeforeWrite)
                {
                    return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "Promotion fixture failure.");
                }

                if (Active != null && checkpoint.Id == Active.Id && checkpoint.Generation == Active.Generation)
                {
                    return FailPromotionAfterWrite
                        ? CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "Uncertain promotion fixture failure.")
                        : CommandResult<CheckpointId>.Success(checkpoint.Id);
                }

                BeforePromotion?.Invoke(checkpoint);

                if (Active != null && checkpoint.Generation <= Active.Generation)
                {
                    return CommandResult<CheckpointId>.Rejected(CommandOutcome.InvalidCheckpoint, "Generation fixture failure.");
                }

                Previous = Active;
                Active = Clone(checkpoint);
                AfterPromotion?.Invoke(checkpoint);
                if (FailPromotionAfterWrite)
                {
                    return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "Uncertain promotion fixture failure.");
                }

                return CommandResult<CheckpointId>.Success(checkpoint.Id);
            }

            public CommandResult<CheckpointId> RecoverPrevious(Checkpoint recoveryCandidate)
            {
                RecoveryCallCount++;
                BeforeRecovery?.Invoke(recoveryCandidate);
                if (FailRecoveryBeforeWrite)
                {
                    return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "Recovery fixture failure.");
                }

                if (Active != null && Active.Id == recoveryCandidate.Id && Active.Generation == recoveryCandidate.Generation)
                {
                    return FailRecoveryAfterWrite
                        ? CommandResult<CheckpointId>.Rejected(RecoveryAfterWriteFailureOutcome, "Uncertain recovery fixture failure.")
                        : CommandResult<CheckpointId>.Success(recoveryCandidate.Id);
                }

                if (Active == null || Previous == null || recoveryCandidate.Generation <= Active.Generation ||
                    recoveryCandidate.Generation <= Previous.Generation ||
                    recoveryCandidate.ReconciledFromCheckpointId != Previous.Id)
                {
                    return CommandResult<CheckpointId>.Rejected(CommandOutcome.InvalidCheckpoint, "Recovery fixture candidate invalid.");
                }

                var oldActive = Active;
                Active = Clone(recoveryCandidate);
                Previous = oldActive;
                if (FailRecoveryAfterWrite)
                {
                    return CommandResult<CheckpointId>.Rejected(RecoveryAfterWriteFailureOutcome, "Uncertain recovery fixture failure.");
                }

                return CommandResult<CheckpointId>.Success(recoveryCandidate.Id);
            }

            private static Checkpoint Clone(Checkpoint source) => new Checkpoint(
                source.Id,
                source.Generation,
                source.DepartureUtc,
                source.OfflinePolicyRevision,
                source.ReconciledFromCheckpointId,
                source.CreateFactoryCopy());
        }
    }
}
