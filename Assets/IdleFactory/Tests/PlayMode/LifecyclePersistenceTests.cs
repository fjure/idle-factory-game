using System;
using System.Collections;
using System.Collections.Generic;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace IdleFactory.Tests.PlayMode
{
    public sealed class LifecyclePersistenceTests
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private Scene loadedScene;
        private SceneCompositionTestScope compositionScope;
        private CoreGameFoundationBootstrap bootstrap;
        private GameObject adapterObject;
        private ApplicationLifecycleAdapter adapter;
        private FactorySession session;
        private PersistenceCommands persistenceCommands;
        private ProductionCommands productionCommands;
        private TestUtcClock clock;
        private TestCheckpointStore store;

        [UnitySetUp]
        public IEnumerator LoadDevelopmentCatalog()
        {
#if !UNITY_EDITOR
            Assert.Ignore("The development catalog is loaded through its Editor scene harness.");
            yield break;
#else
            compositionScope = SceneCompositionTestScope.WithSeededEmptyCheckpoint();
            EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            while (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
            {
                yield return null;
            }

            yield return null;
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            var rootObjects = loadedScene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                bootstrap = root.GetComponentInChildren<CoreGameFoundationBootstrap>(true);
                if (bootstrap != null)
                {
                    break;
                }
            }

            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
#endif
        }

        [UnityTearDown]
        public IEnumerator UnloadDevelopmentCatalog()
        {
            if (adapterObject != null)
            {
                UnityEngine.Object.Destroy(adapterObject);
                yield return null;
            }

            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(loadedScene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }

            loadedScene = default(Scene);
            bootstrap = null;
            adapterObject = null;
            adapter = null;
            session = null;
            persistenceCommands = null;
            productionCommands = null;
            clock = null;
            store = null;
            compositionScope?.Dispose();
            compositionScope = null;
        }

        [Test]
        public void DeviceUtcClockReturnsZeroOffsetUtcTime()
        {
            var before = DateTimeOffset.UtcNow;
            var value = new DeviceUtcClock().GetUtcNow();
            var after = DateTimeOffset.UtcNow;

            Assert.That(value.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(value, Is.GreaterThanOrEqualTo(before));
            Assert.That(value, Is.LessThanOrEqualTo(after));
        }

        [UnityTest]
        public IEnumerator ColdStartUsesActiveCheckpointWithoutFallback()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);

            Assert.That(adapter.LastResult, Is.Not.Null);
            Assert.That(adapter.LastResult.Succeeded, Is.True, adapter.LastResult.Message);
            Assert.That(adapter.LastResult.Value.UsedFallback, Is.False);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));

            yield break;
        }

        [UnityTest]
        public IEnumerator PauseAndFocusCallbacksBindSuccessfulDepartureAndReconcileOnce()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);
            var promotionsAtStart = store.PromoteCount;
            store.ReadCheckpointIds.Clear();
            clock.Now = Epoch.AddMinutes(15);

            adapterObject.SendMessage("OnApplicationFocus", false);
            Assert.That(adapter.LastResult.Succeeded, Is.True, adapter.LastResult.Message);
            var departureId = adapter.LastResult.Value.CheckpointId;
            Assert.That(departureId.HasValue, Is.True);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Suspended));
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 1));

            var whileSuspended = productionCommands.AdvanceSimulation(TimeSpan.TicksPerSecond);
            Assert.That(whileSuspended.Outcome, Is.EqualTo(CommandOutcome.LifecycleNoOp));

            adapterObject.SendMessage("OnApplicationPause", true);
            adapterObject.SendMessage("OnApplicationFocus", true);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Suspended));
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 1));

            clock.Now = Epoch.AddMinutes(20);
            adapterObject.SendMessage("OnApplicationPause", false);
            Assert.That(adapter.LastResult.Succeeded, Is.True, adapter.LastResult.Message);
            Assert.That(adapter.LastResult.Value.UsedFallback, Is.False);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(store.ReadCheckpointIds, Does.Contain(departureId.Value));
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 2));

            adapterObject.SendMessage("OnApplicationPause", false);
            adapterObject.SendMessage("OnApplicationFocus", true);
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 2),
                "Duplicate return callbacks must not save or reconcile a second time.");

            yield break;
        }

        [UnityTest]
        public IEnumerator FailedSuspensionUsesOnlyTheExplicitFr037Fallback()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);
            var fallbackCheckpointId = store.Active.Id;
            store.ReadCheckpointIds.Clear();
            store.FailNextPromotion = true;
            clock.Now = Epoch.AddMinutes(15);

            adapterObject.SendMessage("OnApplicationPause", true);
            Assert.That(adapter.LastResult.Succeeded, Is.False);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Suspended));

            clock.Now = Epoch.AddMinutes(20);
            adapterObject.SendMessage("OnApplicationPause", false);

            Assert.That(adapter.LastResult.Succeeded, Is.True, adapter.LastResult.Message);
            Assert.That(adapter.LastResult.Value.UsedFallback, Is.True);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Running));
            Assert.That(store.ReadCheckpointIds, Is.Empty,
                "A failed departure write must use the last active checkpoint, not a fabricated departure ID.");
            Assert.That(store.Active.ReconciledFromCheckpointId, Is.EqualTo(fallbackCheckpointId));

            yield break;
        }

        [UnityTest]
        public IEnumerator FailedReturnBlocksGameplayAndDuplicateCallbacksDoNotRetryIt()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);
            clock.Now = Epoch.AddMinutes(15);
            adapterObject.SendMessage("OnApplicationPause", true);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Suspended));

            store.FailNextPromotion = true;
            clock.Now = Epoch.AddMinutes(20);
            adapterObject.SendMessage("OnApplicationPause", false);

            Assert.That(adapter.LastResult.Succeeded, Is.False);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));
            var blockedAdvance = productionCommands.AdvanceSimulation(TimeSpan.TicksPerSecond);
            Assert.That(blockedAdvance.Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));

            var promotionsAfterBlockedReturn = store.PromoteCount;
            adapterObject.SendMessage("OnApplicationPause", false);
            adapterObject.SendMessage("OnApplicationFocus", true);
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAfterBlockedReturn),
                "A duplicate platform callback must not silently retry an unresolved return.");
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.ReturnBlocked));

            yield break;
        }

        [UnityTest]
        public IEnumerator QuitAfterPauseRecordsOnlyOneDepartureAndNeverResumes()
        {
            CreateLifecycleSystem(seedActiveCheckpoint: true);
            var promotionsAtStart = store.PromoteCount;
            clock.Now = Epoch.AddMinutes(15);

            adapterObject.SendMessage("OnApplicationPause", true);
            adapterObject.SendMessage("OnApplicationQuit");
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 1));

            adapterObject.SendMessage("OnApplicationPause", false);
            adapterObject.SendMessage("OnApplicationFocus", true);
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Suspended));
            Assert.That(store.PromoteCount, Is.EqualTo(promotionsAtStart + 1));

            yield break;
        }

        private void CreateLifecycleSystem(bool seedActiveCheckpoint)
        {
            var catalog = bootstrap.Session.Catalog;
            var bounds = new FactoryBounds(new GridPosition(0, 0), 16, 16);
            var initialFactory = new FactoryState(bounds, catalog, Array.Empty<BuildingInstanceState>());
            clock = new TestUtcClock(Epoch);
            store = new TestCheckpointStore();

            if (seedActiveCheckpoint)
            {
                store.SeedActive(new Checkpoint(
                    new CheckpointId(new Guid(1, 0, 0, new byte[8])),
                    0,
                    Epoch,
                    new OfflinePolicyRevision(DefinitionCatalog.SupportedOfflinePolicyRevision),
                    null,
                    initialFactory));
            }

            session = new FactorySession(initialFactory, catalog, clock, store);
            persistenceCommands = new PersistenceCommands(session);
            productionCommands = new ProductionCommands(session);
            adapterObject = new GameObject("Application Lifecycle Adapter Test");
            adapter = adapterObject.AddComponent<ApplicationLifecycleAdapter>();
            adapter.Configure(persistenceCommands);
        }

        private sealed class TestUtcClock : IUtcClock
        {
            public TestUtcClock(DateTimeOffset now)
            {
                Now = now;
            }

            public DateTimeOffset Now { get; set; }

            public DateTimeOffset GetUtcNow() => Now;
        }

        private sealed class TestCheckpointStore : ICheckpointStore
        {
            public Checkpoint Active { get; private set; }
            public Checkpoint Previous { get; private set; }
            public int PromoteCount { get; private set; }
            public bool FailNextPromotion { get; set; }
            public List<CheckpointId> ReadCheckpointIds { get; } = new List<CheckpointId>();

            public void SeedActive(Checkpoint checkpoint)
            {
                Active = checkpoint;
            }

            public CheckpointReadResult ReadActive() => Active == null
                ? CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "No test checkpoint is active.")
                : CheckpointReadResult.Success(Active);

            public CheckpointReadResult ReadPrevious() => Previous == null
                ? CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "No test checkpoint is previous.")
                : CheckpointReadResult.Success(Previous);

            public CheckpointReadResult Read(CheckpointId checkpointId)
            {
                ReadCheckpointIds.Add(checkpointId);
                if (Active != null && Active.Id == checkpointId)
                {
                    return CheckpointReadResult.Success(Active);
                }

                if (Previous != null && Previous.Id == checkpointId)
                {
                    return CheckpointReadResult.Success(Previous);
                }

                return CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "The test checkpoint was not found.");
            }

            public CommandResult<CheckpointId> Promote(Checkpoint checkpoint)
            {
                PromoteCount++;
                if (FailNextPromotion)
                {
                    FailNextPromotion = false;
                    return CommandResult<CheckpointId>.Rejected(
                        CommandOutcome.PersistenceFailed,
                        "The test checkpoint promotion failed.");
                }

                if (Active != null && checkpoint.Generation <= Active.Generation)
                {
                    return CommandResult<CheckpointId>.Rejected(
                        CommandOutcome.InvalidCheckpoint,
                        "The test checkpoint generation did not advance.");
                }

                Previous = Active;
                Active = checkpoint;
                return CommandResult<CheckpointId>.Success(checkpoint.Id);
            }

            public CommandResult<CheckpointId> RecoverPrevious(Checkpoint recoveryCandidate) =>
                CommandResult<CheckpointId>.Rejected(
                    CommandOutcome.NoCheckpoint,
                    "Previous-checkpoint recovery is not used by these lifecycle tests.");
        }
    }
}
