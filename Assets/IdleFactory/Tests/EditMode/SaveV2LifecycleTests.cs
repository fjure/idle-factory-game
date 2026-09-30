using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Core.Numerics;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The persistence commands of the run over Save V2 (T138, contracts/run-application.md, FR-027, SC-002):
    /// <c>SaveNow</c>, <c>RecordSuspension</c>, <c>ResumeAndReconcile</c>, <c>RetryReturn</c>,
    /// <c>ReloadAuthoritativeGeneration</c> and <c>RecoverPreviousCheckpoint</c> with their Feature 001 lifecycle
    /// semantics, the pending-transition rules, the content retune of load rule 2 and the equivalence of save, load and
    /// continue with an uninterrupted run (QA-5).
    /// </summary>
    public sealed class SaveV2LifecycleTests
    {
        private static List<SaveV2Fixture> _game;
        private bool _logToUnity;
        private string _root;

        [OneTimeSetUp]
        public void BuildGame()
        {
            _game = SaveV2Scenarios.PlayedGame();
        }

        [SetUp]
        public void SetUp()
        {
            _logToUnity = CheckpointDiagnostics.LogToUnity;
            CheckpointDiagnostics.LogToUnity = false;
            _root = RunCheckpointTestSupport.NewTempRoot();
        }

        [TearDown]
        public void TearDown()
        {
            CheckpointDiagnostics.LogToUnity = _logToUnity;
            RunCheckpointTestSupport.DeleteQuietly(_root);
        }

        private sealed class Harness
        {
            public RunCommandFixtures.Rig Rig;
            public RunCommands Commands;
            public ScriptedBackend Backend;
            public JsonRunCheckpointStore Store;
            public FixedUtcClock Clock;
            public RunPersistence Persistence;

            public RunSession Session => Rig.Session;

            public string Dump() => SaveV2Scenarios.Dump(Session.State, Session.Permanent);

            /// <summary>Moves the clock to the run timeline, so a departure equals the timeline instant.</summary>
            public void Sync() => Clock.UnixMs = Session.TimelineUtcMs;

            public RunCheckpoint Active() => Store.ReadActive().Checkpoint;
        }

        private static RunCommandFixtures.Options Options() => SaveV2Scenarios.Options();

        private static Harness Create(ScriptedBackend backend = null, bool resume = true, long cash = 50_000_000,
            RunCommandFixtures.Options options = null)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(options ?? Options(), cash);
            backend = backend ?? new ScriptedBackend();
            var store = new JsonRunCheckpointStore("memory-root", rig.Catalog, backend);
            var clock = new FixedUtcClock(rig.Session.TimelineUtcMs);
            var h = new Harness
            {
                Rig = rig,
                Backend = backend,
                Store = store,
                Clock = clock,
                Persistence = new RunPersistence(rig.Session, store, clock, new SequenceCheckpointIds(), new NoCreditRunReturnReconciler()),
            };
            h.Commands = SaveV2Scenarios.NewCommands(rig);
            if (resume) Ok(h.Persistence.ResumeAndReconcile());
            return h;
        }

        /// <summary>A restart: a new process (new catalog, session and persistence) over the same backend.</summary>
        private static int _restarts;

        private static Harness Restart(Harness previous, long clockMs) => Restart(previous.Backend, clockMs, 1000 + 100 * ++_restarts);

        private static Harness Restart(ScriptedBackend backend, long clockMs, int firstId)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(Options(), 25);
            var store = new JsonRunCheckpointStore("memory-root", rig.Catalog, backend);
            var clock = new FixedUtcClock(clockMs);
            return new Harness
            {
                Rig = rig,
                Backend = backend,
                Store = store,
                Clock = clock,
                Persistence = new RunPersistence(rig.Session, store, clock, new SequenceCheckpointIds(firstId), new NoCreditRunReturnReconciler()),
                Commands = SaveV2Scenarios.NewCommands(rig),
            };
        }

        private static RunPersistenceOutcome Ok(RunCommandResult<RunPersistenceOutcome> result, string context = "")
        {
            Assert.That(result.IsSuccess, Is.True, result.ReasonCode + " " + context);
            return result.Value;
        }

        private static void Rejected(RunCommandResult<RunPersistenceOutcome> result, string reason)
        {
            Assert.That(result.IsSuccess, Is.False, "expected " + reason);
            Assert.That(result.ReasonCode, Is.EqualTo(reason));
        }

        private static void Play(Harness h)
        {
            Assert.That(h.Rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            Assert.That(h.Rig.Build(1, RunCommandFixtures.Smelter).IsSuccess, Is.True);
            Assert.That(h.Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            Assert.That(h.Commands.UpgradeStorage().IsSuccess, Is.True);
            Assert.That(h.Commands.ActivateOverdrive().IsSuccess, Is.True);
            h.Rig.Advance(13_000_000);
        }

        // ------------------------------------------------------------------------------------------------------
        // Lifecycle and admission
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void BeforeResume_TheRunIsLifecycleBlocked_AndOnlyResumeIsAdmitted()
        {
            Harness h = Create(resume: false);
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Bootstrapping));
            Assert.That(h.Session.IsLifecycleBlocked, Is.True);
            Assert.That(h.Session.Advance(1).ReasonCode, Is.EqualTo(RunReasons.LifecycleBlocked));
            Rejected(h.Persistence.SaveNow(), RunPersistenceReasons.LifecycleNoOp);
            Rejected(h.Persistence.RecordSuspension(), RunPersistenceReasons.LifecycleNoOp);
            Rejected(h.Persistence.RetryReturn(), RunPersistenceReasons.LifecycleNoOp);
            Rejected(h.Persistence.ReloadAuthoritativeGeneration(), RunPersistenceReasons.LifecycleNoOp);
            Rejected(h.Persistence.RecoverPreviousCheckpoint(), RunPersistenceReasons.LifecycleNoOp);
            Assert.That(h.Backend.Files, Is.Empty, "nothing was written");

            RunPersistenceOutcome resumed = Ok(h.Persistence.ResumeAndReconcile());
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(h.Session.IsLifecycleBlocked, Is.False);
            Assert.That(resumed.NoCheckpoint, Is.True);
            Assert.That(resumed.StateReplaced, Is.False);
            Assert.That(h.Persistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.NoCheckpoint));
            Assert.That(h.Session.Advance(1).IsSuccess, Is.True);
            Rejected(h.Persistence.ResumeAndReconcile(), RunPersistenceReasons.LifecycleNoOp);
            Rejected(h.Persistence.RetryReturn(), RunPersistenceReasons.LifecycleNoOp);
            Rejected(h.Persistence.ReloadAuthoritativeGeneration(), RunPersistenceReasons.LifecycleNoOp);
        }

        [Test]
        public void SaveNow_AdvancesGenerationAndIdentity_AndReportsStatus()
        {
            Harness h = Create();
            Play(h);
            h.Sync();
            RunPersistenceOutcome first = Ok(h.Persistence.SaveNow());
            Assert.That(first.Generation, Is.EqualTo(1));
            Assert.That(h.Persistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.Ready));
            Assert.That(h.Persistence.Status.ActiveCheckpointId, Is.EqualTo(first.CheckpointId));
            Assert.That(h.Persistence.Status.PreviousCheckpointAvailable, Is.False);

            h.Rig.Advance(10_000_000);
            h.Sync();
            RunPersistenceOutcome second = Ok(h.Persistence.SaveNow());
            Assert.That(second.Generation, Is.EqualTo(2));
            Assert.That(second.CheckpointId, Is.Not.EqualTo(first.CheckpointId));
            Assert.That(h.Persistence.Status.PreviousCheckpointAvailable, Is.True);
            Assert.That(h.Store.ReadPrevious().Checkpoint.Id, Is.EqualTo(first.CheckpointId));
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));

            RunCheckpoint saved = h.Active();
            Assert.That(SaveV2Scenarios.Dump(saved.Run, saved.Permanent), Is.EqualTo(h.Dump()), "the checkpoint is the live run");
            Assert.That(saved.ReconciledFromCheckpointId, Is.Null);
            Assert.That(saved.OfflinePolicyRevision, Is.EqualTo(h.Rig.Catalog.OfflinePolicy.Revision));
            Assert.That(saved.ContentRevision, Is.EqualTo(h.Rig.Catalog.ContentRevision));
        }

        [Test]
        public void SaveNow_DepartureIsTheLaterOfTheClockAndTheRunTimeline()
        {
            Harness h = Create();
            Play(h);
            long timeline = h.Session.TimelineUtcMs;

            h.Clock.UnixMs = 1_000;
            Ok(h.Persistence.SaveNow());
            Assert.That(h.Active().DepartureUtcMs, Is.EqualTo(timeline), "a backward clock never moves the departure before the timeline");

            h.Clock.UnixMs = timeline + 5_000;
            Ok(h.Persistence.SaveNow());
            Assert.That(h.Active().DepartureUtcMs, Is.EqualTo(timeline + 5_000));
        }

        [Test]
        public void SaveNow_WithAnUnusableClock_IsRejected_AndWritesNothing()
        {
            Harness h = Create();
            h.Clock.Fail = true;
            Rejected(h.Persistence.SaveNow(), RunPersistenceReasons.PersistenceFailed);
            Assert.That(h.Persistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.Failure));
            Assert.That(h.Backend.Files, Is.Empty);

            h.Clock.Fail = false;
            h.Clock.UnixMs = -1;
            Rejected(h.Persistence.SaveNow(), RunPersistenceReasons.PersistenceFailed);
            Assert.That(h.Backend.Files, Is.Empty);
        }

        [Test]
        public void SaveNow_WithFailingStorage_IsATypedRejection_AndTheOldGenerationStaysAuthoritative()
        {
            Harness h = Create();
            Play(h);
            h.Sync();
            RunPersistenceOutcome first = Ok(h.Persistence.SaveNow());

            h.Backend.FailAll = true;
            Rejected(h.Persistence.SaveNow(), RunPersistenceReasons.PersistenceFailed);
            Assert.That(h.Persistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.Failure));
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));

            h.Backend.ClearFault();
            Assert.That(h.Active().Id, Is.EqualTo(first.CheckpointId));
            Assert.That(Ok(h.Persistence.SaveNow()).Generation, Is.EqualTo(2), "the next save retries with the next generation");
        }

        // ------------------------------------------------------------------------------------------------------
        // Pending prestige transition
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void WhilePrestigeIsPending_SaveNowIsRejected_AutosaveSkips_AndNoCheckpointIsWritten()
        {
            Harness h = Create();
            h.Sync();
            Ok(h.Persistence.SaveNow());
            var before = h.Backend.Snapshot();

            Assert.That(h.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(h.Session.PendingTransition, Is.Not.Null);
            h.Backend.ResetCounters();

            Rejected(h.Persistence.SaveNow(), RunReasons.TransitionPending);
            RunPersistenceOutcome skipped = Ok(h.Persistence.Autosave());
            Assert.That(skipped.WasNoOp, Is.True);
            Assert.That(skipped.CheckpointId, Is.Null);
            Assert.That(h.Backend.Calls, Is.EqualTo(0), "no storage call at all: nothing can race the candidate");
            foreach (var pair in before) Assert.That(h.Backend.Files[pair.Key], Is.EqualTo(pair.Value));
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
        }

        [Test]
        public void Autosave_WritesAGeneration_WhenNothingIsPending()
        {
            Harness h = Create();
            h.Sync();
            RunPersistenceOutcome saved = Ok(h.Persistence.Autosave());
            Assert.That(saved.WasNoOp, Is.False);
            Assert.That(saved.Generation, Is.EqualTo(1));
        }

        [Test]
        public void SuspensionDuringAPendingPrestige_WritesNoCheckpoint_AndTheReturnKeepsTheOldRunWithoutAnAward()
        {
            Harness h = Create();
            Play(h);
            h.Sync();
            Ok(h.Persistence.SaveNow());
            Guid oldRunId = h.Session.State.RunId;
            long ip = h.Session.Permanent.Ip;
            string durable = h.Dump();

            Assert.That(h.Commands.ConfirmPrestige().IsSuccess, Is.True);
            PrestigeCandidate candidate = h.Session.PendingTransition;
            Assert.That(candidate, Is.Not.Null);
            var filesBefore = h.Backend.Snapshot();

            Rejected(h.Persistence.RecordSuspension(), RunReasons.TransitionPending);
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended), "a reported departure suspends even without a checkpoint");
            foreach (var pair in filesBefore) Assert.That(h.Backend.Files[pair.Key], Is.EqualTo(pair.Value));

            h.Clock.UnixMs = h.Session.TimelineUtcMs + 1_000;
            RunPersistenceOutcome returned = Ok(h.Persistence.ResumeAndReconcile());
            Assert.That(returned.UsedFallback, Is.True, "no departure checkpoint: the last durable active one is used (FR-037)");
            Assert.That(returned.StateReplaced, Is.True);
            Assert.That(h.Session.PendingTransition, Is.Null, "the durable generation replaced the in-memory candidate");
            Assert.That(h.Session.State.RunId, Is.EqualTo(oldRunId));
            Assert.That(h.Session.Permanent.Ip, Is.EqualTo(ip), "no IP is awarded by a return");
            Assert.That(h.Dump(), Is.EqualTo(durable));
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
        }

        // ------------------------------------------------------------------------------------------------------
        // Suspension and return
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void RecordSuspension_WritesTheDeparture_AndTheReturnLoadsItWithoutFallback()
        {
            Harness h = Create();
            Play(h);
            h.Sync();
            RunPersistenceOutcome departure = Ok(h.Persistence.RecordSuspension());
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended));
            Assert.That(h.Session.IsLifecycleBlocked, Is.True);
            Assert.That(departure.Generation, Is.EqualTo(1));
            string atSuspension = h.Dump();

            RunPersistenceOutcome repeated = Ok(h.Persistence.RecordSuspension());
            Assert.That(repeated.WasNoOp, Is.True);
            Assert.That(repeated.CheckpointId, Is.EqualTo(departure.CheckpointId));
            Assert.That(h.Store.ReadActive().Checkpoint.Generation, Is.EqualTo(1), "a repeated suspension writes nothing");

            h.Clock.UnixMs = h.Session.TimelineUtcMs + 60_000;
            RunPersistenceOutcome returned = Ok(h.Persistence.ResumeAndReconcile());
            Assert.That(returned.UsedFallback, Is.False);
            Assert.That(returned.StateReplaced, Is.True);
            Assert.That(returned.Generation, Is.EqualTo(2));
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            RunCheckpoint active = h.Active();
            Assert.That(active.ReconciledFromCheckpointId, Is.EqualTo(departure.CheckpointId), "exactly-once link to the departure checkpoint");
            Assert.That(active.DepartureUtcMs, Is.EqualTo(h.Clock.UnixMs), "the departure moves to the return instant");
            Assert.That(h.Session.TimelineUtcMs, Is.EqualTo(h.Clock.UnixMs), "the session is anchored at the return instant");
            Assert.That(h.Session.State.Overdrive.IsActive(h.Session.TimelineUtcMs), Is.False, "the return settles: the elapsed Overdrive has expired");
            Assert.That(h.Session.State.Machines.Select(m => m.InstanceId), Is.EquivalentTo(active.Run.Machines.Select(m => m.InstanceId)));
        }

        [Test]
        public void FailedSuspension_StillSuspends_AndTheReturnFallsBackToTheLastDurableCheckpoint()
        {
            Harness h = Create();
            Play(h);
            h.Sync();
            Ok(h.Persistence.SaveNow());
            string durable = h.Dump();
            h.Rig.Advance(20_000_000);
            h.Sync();
            Assert.That(h.Dump(), Is.Not.EqualTo(durable));

            h.Backend.FailAll = true;
            Rejected(h.Persistence.RecordSuspension(), RunPersistenceReasons.PersistenceFailed);
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Suspended));
            h.Backend.ClearFault();

            RunPersistenceOutcome returned = Ok(h.Persistence.ResumeAndReconcile());
            Assert.That(returned.UsedFallback, Is.True);
            Assert.That(h.Dump(), Is.EqualTo(durable), "later unsaved changes are absent");
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
        }

        [Test]
        public void ColdStart_WithAValidActiveCheckpoint_LoadsItWithoutFallback()
        {
            Harness h = Create();
            Play(h);
            h.Sync();
            Ok(h.Persistence.SaveNow());
            string saved = h.Dump();
            long savedTimeline = h.Session.TimelineUtcMs;

            Harness restarted = Restart(h, savedTimeline);
            Assert.That(restarted.Persistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.Ready), "the status is read at construction");
            RunPersistenceOutcome resumed = Ok(restarted.Persistence.ResumeAndReconcile());
            Assert.That(resumed.UsedFallback, Is.False);
            Assert.That(resumed.StateReplaced, Is.True);
            Assert.That(resumed.NoCheckpoint, Is.False);
            Assert.That(restarted.Dump(), Is.EqualTo(saved));
            Assert.That(restarted.Session.TimelineUtcMs, Is.EqualTo(savedTimeline));
        }

        [Test]
        public void ColdStart_WithOnlyAPendingFile_KeepsTheFreshRun_AndTheFirstSaveSucceeds()
        {
            var backend = new ScriptedBackend();
            backend.Files[CheckpointStorageFile.Pending] = (byte[])_game[0].Bytes.Clone();
            Harness h = Create(backend, resume: false);
            RunPersistenceOutcome resumed = Ok(h.Persistence.ResumeAndReconcile());
            Assert.That(resumed.NoCheckpoint, Is.True, "an unpromoted pending candidate is discarded by the protocol");
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            h.Sync();
            Assert.That(Ok(h.Persistence.SaveNow()).Generation, Is.EqualTo(1));
            Assert.That(h.Active().Id, Is.Not.EqualTo(new CheckpointId(_game[0].Meta.CheckpointId)));
        }

        [Test]
        public void ColdStart_WithCheckpointFilesButNoValidActive_BlocksTheReturn_NeverAFreshRun()
        {
            // A data file without any manifest, a manifest that does not parse, and a corrupt data file (SEC-1).
            foreach (int variant in new[] { 0, 1, 2 })
            {
                var backend = new ScriptedBackend();
                if (variant == 0)
                {
                    backend.Files[CheckpointStorageFile.DataA] = (byte[])_game[0].Bytes.Clone();
                }
                else if (variant == 1)
                {
                    backend.Files[CheckpointStorageFile.ManifestA] = new byte[] { 1, 2, 3 };
                }
                else
                {
                    Harness seed = Create(backend);
                    seed.Sync();
                    Ok(seed.Persistence.SaveNow());
                    backend.Files[CheckpointStorageFile.DataA][10] ^= 0x01;
                }

                Harness h = Restart(backend, 1_700_000_000_000L, 500);
                Rejected(h.Persistence.ResumeAndReconcile(), RunPersistenceReasons.ReturnBlocked);
                Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.ReturnBlocked), "variant " + variant);
                Assert.That(h.Session.IsLifecycleBlocked, Is.True);
                Assert.That(h.Persistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.Failure));
                Rejected(h.Persistence.SaveNow(), RunPersistenceReasons.ReturnBlocked);
            }
        }

        [Test]
        public void ReturnWithAStoreOfAnotherCatalog_IsBlocked_AndNothingIsPublished()
        {
            var backend = new ScriptedBackend();
            var seed = new JsonRunCheckpointStore("memory-root", _game[0].Catalog, backend);
            Assert.That(seed.Promote(RunCheckpointTestSupport.CheckpointOf(_game[0])).Succeeded, Is.True);

            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(Options(), 25);
            var store = new StoreOfAnotherCatalog(new JsonRunCheckpointStore("memory-root", _game[0].Catalog, backend));
            var persistence = new RunPersistence(rig.Session, store, new FixedUtcClock(_game[0].Meta.DepartureUtcMs));
            Rejected(persistence.ResumeAndReconcile(), RunPersistenceReasons.ReturnBlocked);
            Assert.That(persistence.Lifecycle, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(rig.Session.State.RunId, Is.EqualTo(RunSimHarness.Id(9000)), "the live run is untouched");
            Assert.That(seed.ReadActive().Checkpoint.Generation, Is.EqualTo(_game[0].Meta.Generation), "nothing was promoted for it");
        }

        /// <summary>A store whose checkpoints belong to a catalog instance other than the session's (here: the fixture catalog).</summary>
        private sealed class StoreOfAnotherCatalog : IRunCheckpointStore
        {
            private readonly IRunCheckpointStore inner;

            public StoreOfAnotherCatalog(IRunCheckpointStore inner) => this.inner = inner;

            public RunCheckpointReadResult ReadActive() => inner.ReadActive();
            public RunCheckpointReadResult ReadPrevious() => inner.ReadPrevious();
            public RunCheckpointReadResult Read(CheckpointId checkpointId) => inner.Read(checkpointId);
            public CommandResult<CheckpointId> Promote(RunCheckpoint checkpoint) => inner.Promote(checkpoint);
            public CommandResult<CheckpointId> RecoverPrevious(RunCheckpoint recoveryCandidate) => inner.RecoverPrevious(recoveryCandidate);
            public CommandResult<RunCheckpointPresence> ProbeFiles() => inner.ProbeFiles();
        }

        // ------------------------------------------------------------------------------------------------------
        // Blocked return: retry exactly once, reload, recover
        // ------------------------------------------------------------------------------------------------------

        private static Harness SuspendedFixture(out ScriptedBackend backend, out string atSuspension, out CheckpointId departureId,
            out long timeline)
        {
            Harness h = Create();
            Play(h);
            h.Sync();
            Ok(h.Persistence.SaveNow());
            h.Rig.Advance(10_000_000);
            h.Sync();
            RunPersistenceOutcome departure = Ok(h.Persistence.RecordSuspension());
            backend = h.Backend;
            atSuspension = h.Dump();
            departureId = departure.CheckpointId.Value;
            timeline = h.Session.TimelineUtcMs;
            return h;
        }

        [Test]
        public void ColdStartReturn_FailingAtEveryBackendCall_BlocksAndRetryPublishesExactlyOneCandidate()
        {
            SuspendedFixture(out ScriptedBackend backend, out string atSuspension, out CheckpointId departureId, out long timeline);
            var snapshot = backend.Snapshot();

            // The clean cold start gives the number of backend calls of a return.
            Harness clean = Restart(backend, timeline + 1_000, 900);
            backend.ResetCounters();
            Ok(clean.Persistence.ResumeAndReconcile());
            int total = backend.Calls;
            Assert.That(total, Is.GreaterThan(10));
            Assert.That(clean.Active().Generation, Is.EqualTo(3));

            int blocked = 0;
            for (int call = 0; call < total; call++)
            {
                backend.Restore(snapshot);
                Harness h = Restart(backend, timeline + 1_000, 900);
                backend.ResetCounters();
                backend.FailAtCall(call);
                var result = h.Persistence.ResumeAndReconcile();
                Assert.That(backend.FaultTriggered, Is.True, "call " + call);
                Assert.That(result.IsSuccess, Is.False, "call " + call);
                Assert.That(result.ReasonCode, Is.EqualTo(RunPersistenceReasons.ReturnBlocked), "call " + call);
                Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.ReturnBlocked), "call " + call);
                Assert.That(h.Session.IsLifecycleBlocked, Is.True, "call " + call);
                blocked++;

                string failedOperation = backend.Log.Count > call ? backend.Log[call] : "?";
                backend.ClearFault();
                RunPersistenceOutcome retried = Ok(h.Persistence.RetryReturn(), "retry after call " + call + " " + failedOperation + " " + h.Persistence.Status.Kind);
                Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running), "call " + call);
                Assert.That(retried.StateReplaced, Is.True, "call " + call);
                Assert.That(retried.Generation, Is.EqualTo(3), "exactly one return candidate after the source generation: call " + call);
                RunCheckpoint active = h.Active();
                Assert.That(active.Generation, Is.EqualTo(3), "call " + call);
                Assert.That(active.ReconciledFromCheckpointId, Is.EqualTo(departureId), "call " + call);
                Assert.That(h.Store.ReadPrevious().Checkpoint.Generation, Is.EqualTo(2), "call " + call);
                Assert.That(h.Dump(), Is.EqualTo(atSuspension), "no state is lost or duplicated: call " + call);
                Rejected(h.Persistence.RetryReturn(), RunPersistenceReasons.LifecycleNoOp);
                Assert.That(h.Active().Generation, Is.EqualTo(3), "a second retry writes nothing: call " + call);
            }

            Assert.That(blocked, Is.EqualTo(total));
            Assert.That(departureId, Is.Not.EqualTo(default(CheckpointId)));
        }

        [Test]
        public void BlockedReturn_OnlyRetryAndReloadAreAdmitted_AndReloadInstallsTheDurableGeneration()
        {
            SuspendedFixture(out ScriptedBackend backend, out string atSuspension, out CheckpointId departureId, out long timeline);
            Harness h = Restart(backend, timeline, 900);
            backend.FailAll = true;
            Rejected(h.Persistence.ResumeAndReconcile(), RunPersistenceReasons.ReturnBlocked);
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.ReturnBlocked));
            Rejected(h.Persistence.SaveNow(), RunPersistenceReasons.ReturnBlocked);
            Rejected(h.Persistence.RecordSuspension(), RunPersistenceReasons.ReturnBlocked);
            Rejected(h.Persistence.ResumeAndReconcile(), RunPersistenceReasons.ReturnBlocked);
            Rejected(h.Persistence.RetryReturn(), RunPersistenceReasons.ReturnBlocked);
            Rejected(h.Persistence.ReloadAuthoritativeGeneration(), RunPersistenceReasons.ReturnBlocked);
            Assert.That(h.Session.Advance(1).ReasonCode, Is.EqualTo(RunReasons.LifecycleBlocked));

            backend.ClearFault();
            RunPersistenceOutcome reloaded = Ok(h.Persistence.ReloadAuthoritativeGeneration());
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(reloaded.StateReplaced, Is.True);
            Assert.That(reloaded.CheckpointId, Is.EqualTo(departureId));
            Assert.That(h.Dump(), Is.EqualTo(atSuspension));
            Assert.That(h.Persistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.Ready));
            Assert.That(h.Session.Advance(1).IsSuccess, Is.True);
        }

        [Test]
        public void RecoverPreviousCheckpoint_InstallsThePreviousGenerationAsANewActiveOne()
        {
            Harness h = Create();
            Play(h);
            h.Sync();
            Ok(h.Persistence.SaveNow());
            string first = h.Dump();
            CheckpointId firstId = h.Active().Id;
            h.Rig.Advance(30_000_000);
            h.Sync();
            RunPersistenceOutcome second = Ok(h.Persistence.SaveNow());
            Assert.That(h.Dump(), Is.Not.EqualTo(first));

            RunPersistenceOutcome recovered = Ok(h.Persistence.RecoverPreviousCheckpoint());
            Assert.That(recovered.StateReplaced, Is.True);
            Assert.That(recovered.Generation, Is.EqualTo(3));
            Assert.That(h.Dump(), Is.EqualTo(first));
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(h.Persistence.Status.Kind, Is.EqualTo(PersistenceStatusKind.Recovered));
            RunCheckpoint active = h.Active();
            Assert.That(active.Id, Is.EqualTo(recovered.CheckpointId));
            Assert.That(active.ReconciledFromCheckpointId, Is.EqualTo(firstId));
            Assert.That(h.Store.ReadPrevious().Checkpoint.Id, Is.EqualTo(firstId), "the recovered generation stays retained as the previous one");
        }

        [Test]
        public void RecoverPreviousCheckpoint_WithoutAPreviousGeneration_IsRejectedWithNoCheckpoint()
        {
            Harness h = Create();
            h.Sync();
            Ok(h.Persistence.SaveNow());
            Rejected(h.Persistence.RecoverPreviousCheckpoint(), RunPersistenceReasons.NoCheckpoint);
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
        }

        [Test]
        public void FailedRecovery_BlocksTheReturn_AndRetryCompletesItOnce()
        {
            Harness h = Create();
            Play(h);
            h.Sync();
            Ok(h.Persistence.SaveNow());
            string first = h.Dump();
            h.Rig.Advance(30_000_000);
            h.Sync();
            Ok(h.Persistence.SaveNow());

            h.Backend.FailOnFirst("Create", CheckpointStorageFile.Pending);
            var failed = h.Persistence.RecoverPreviousCheckpoint();
            h.Backend.ClearFault();
            Assert.That(failed.IsSuccess, Is.False);
            Assert.That(h.Backend.FaultTriggered, Is.True);
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.ReturnBlocked));

            // A second recovery command retries the pending candidate itself.
            RunPersistenceOutcome recovered = Ok(h.Persistence.RecoverPreviousCheckpoint());
            Assert.That(h.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(recovered.Generation, Is.EqualTo(3));
            Assert.That(h.Dump(), Is.EqualTo(first));
            Assert.That(h.Active().Generation, Is.EqualTo(3));
        }

        // ------------------------------------------------------------------------------------------------------
        // Load rule 2 and persistence equivalence (QA-5)
        // ------------------------------------------------------------------------------------------------------

        private RunCheckpoint PromoteFixtureToDisk(SaveV2Fixture fixture)
        {
            var store = new JsonRunCheckpointStore(_root, fixture.Catalog);
            RunCheckpoint checkpoint = RunCheckpointTestSupport.CheckpointOf(fixture);
            Assert.That(store.Promote(checkpoint).Succeeded, Is.True);
            return checkpoint;
        }

        [Test]
        public void MidRunFixture_LoadsUnchanged_WhenTheContentIsUnchanged()
        {
            SaveV2Fixture mid = _game[0];
            PromoteFixtureToDisk(mid);
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(Options(), 25);
            var persistence = new RunPersistence(rig.Session, new JsonRunCheckpointStore(_root, rig.Catalog),
                new FixedUtcClock(mid.Meta.DepartureUtcMs));

            Assert.That(Ok(persistence.ResumeAndReconcile()).StateReplaced, Is.True);
            Assert.That(SaveV2Scenarios.Dump(rig.Session.State, rig.Session.Permanent), Is.EqualTo(SaveV2Scenarios.Dump(mid.Run, mid.Permanent)),
                "every state family survives the load and its first settle");
            Assert.That(rig.Session.TimelineUtcMs, Is.EqualTo(mid.Meta.DepartureUtcMs));
            Assert.That(rig.Session.State.Machines.Any(m => m.ProgressTicks > 0 && m.ProgressTicks < m.CycleDurationTicks), Is.True, "a mid-cycle machine");
        }

        [Test]
        public void ContentDurationRetune_RescalesProgressOnLoad_WithMulDivFloor()
        {
            SaveV2Fixture mid = _game[0];
            PromoteFixtureToDisk(mid);
            RunCommandFixtures.Options retuned = Options();
            retuned.Durations = new long[] { 30_000_000, 50_000_000, 40_000_000, 50_000_000 };
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(retuned, 25);
            var store = new JsonRunCheckpointStore(_root, rig.Catalog);

            // The expectation comes from the saved state and the retuned content alone (R03, OD-6), before any load.
            RunCheckpoint saved = store.ReadActive().Checkpoint;
            var modifiers = new RunModifiers(rig.Catalog);
            bool overdrive = saved.Run.Overdrive.IsActive(saved.DepartureUtcMs);
            var expected = new Dictionary<Guid, (long Cycle, long Progress)>();
            int rescaled = 0;
            foreach (MachineInstance m in saved.Run.Machines)
            {
                MachineDef def = rig.Catalog.Machines[m.MachineId];
                MachineRecipe recipe = rig.Catalog.Recipes[def.RecipeId];
                Assert.That(modifiers.TryMachineSpeed(saved.Run, saved.Permanent, m, overdrive, null, out long speed), Is.True);
                Assert.That(SpeedComposer.TryBatch(recipe.DurationTicks, speed, rig.Catalog.RunStart.MinCycleTicks, out _, out long cycle), Is.True);
                Assert.That(SpeedComposer.TryRescaleProgress(m.ProgressTicks, m.CycleDurationTicks, cycle, out long progress), Is.True);
                expected[m.InstanceId] = (cycle, progress);
                if (cycle != m.CycleDurationTicks) rescaled++;
                Assert.That(MulDiv.MulDivFloor(m.ProgressTicks, cycle, m.CycleDurationTicks, out long floor), Is.True);
                Assert.That(progress, Is.EqualTo(floor).Or.EqualTo(cycle), "R03: floor(p x D' / D), a waiting machine stays waiting");
            }

            Assert.That(rescaled, Is.GreaterThan(0), "the retune changes at least one machine's cycle");
            Assert.That(saved.Run.Machines.Any(m => m.ProgressTicks > 0 && m.ProgressTicks < m.CycleDurationTicks && expected[m.InstanceId].Cycle != m.CycleDurationTicks), Is.True,
                "a mid-cycle machine is among them");

            var persistence = new RunPersistence(rig.Session, store, new FixedUtcClock(saved.DepartureUtcMs));
            Assert.That(Ok(persistence.ResumeAndReconcile()).StateReplaced, Is.True);
            foreach (MachineInstance m in rig.Session.State.Machines)
            {
                Assert.That(m.CycleDurationTicks, Is.EqualTo(expected[m.InstanceId].Cycle), m.MachineId + " cycle");
                Assert.That(m.ProgressTicks, Is.EqualTo(expected[m.InstanceId].Progress), m.MachineId + " progress");
                Assert.That(m.ProgressTicks, Is.LessThanOrEqualTo(m.CycleDurationTicks));
            }

            // The retune is compatible, not a corruption: the durable generation after the return is the rescaled one.
            Assert.That(Ok(persistence.SaveNow()).Generation, Is.EqualTo(mid.Meta.Generation + 2), "the return candidate, then this save");
            RunCheckpoint rewritten = store.ReadActive().Checkpoint;
            Assert.That(rewritten.Run.Machines.All(m => m.CycleDurationTicks == expected[m.InstanceId].Cycle), Is.True);
        }

        private static void Segment(Harness h, long ticks) => h.Rig.Advance(ticks);

        [Test]
        public void SaveLoadContinue_EqualsAnUninterruptedRun_AcrossRepeatedRestarts()
        {
            Harness uninterrupted = Create();
            Play(uninterrupted);
            Harness live = Create();
            Play(live);
            Assert.That(live.Dump(), Is.EqualTo(uninterrupted.Dump()));

            var segments = new long[] { 13_000_000, 7_000_000, 29_000_000, 11_000_000, 5_000_000 };
            for (int i = 0; i < segments.Length; i++)
            {
                Segment(uninterrupted, segments[i]);
                Segment(live, segments[i]);
                live.Sync();
                Ok(live.Persistence.SaveNow());

                Harness restarted = Restart(live, live.Clock.UnixMs);
                RunPersistenceOutcome resumed = Ok(restarted.Persistence.ResumeAndReconcile());
                Assert.That(resumed.StateReplaced, Is.True, "segment " + i);
                Assert.That(restarted.Session.TimelineUtcMs, Is.EqualTo(uninterrupted.Session.TimelineUtcMs), "segment " + i);
                Assert.That(restarted.Dump(), Is.EqualTo(uninterrupted.Dump()), "segment " + i);
                live = restarted;
            }

            Assert.That(live.Dump(), Is.EqualTo(uninterrupted.Dump()));
            var meta = SaveV2Scenarios.Meta(uninterrupted.Rig.Catalog, 1, 1, uninterrupted.Session.TimelineUtcMs);
            var expected = SaveV2Mapper.Serialize(meta, uninterrupted.Session.State, uninterrupted.Session.Permanent);
            var actual = SaveV2Mapper.Serialize(meta, live.Session.State, live.Session.Permanent);
            Assert.That(expected.IsSuccess && actual.IsSuccess, Is.True);
            Assert.That(actual.Bytes, Is.EqualTo(expected.Bytes), "the serialized states are byte-identical (SC-002)");
            Assert.That(uninterrupted.Session.State.Machines.Any(m => m.ProgressTicks > 0), Is.True);
        }

        [Test]
        public void SaveLoadContinue_OfTheMidRunFixture_EqualsContinuingTheFixtureState()
        {
            SaveV2Fixture mid = _game[0];
            PromoteFixtureToDisk(mid);

            // Control: the fixture state continued in a session that never saw a file.
            RunCatalog controlCatalog = RunCommandFixtures.NewRig(Options(), 25).Catalog;
            var controlDecoded = new SaveV2CheckpointCodec(controlCatalog).Deserialize(mid.Bytes);
            Assert.That(controlDecoded.Succeeded, Is.True);
            var control = new RunSession(controlCatalog, controlDecoded.Checkpoint.Run, controlDecoded.Checkpoint.Permanent, mid.Meta.DepartureUtcMs);
            Assert.That(control.Advance(0).IsSuccess, Is.True);

            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(Options(), 25);
            var persistence = new RunPersistence(rig.Session, new JsonRunCheckpointStore(_root, rig.Catalog),
                new FixedUtcClock(mid.Meta.DepartureUtcMs));
            Assert.That(Ok(persistence.ResumeAndReconcile()).StateReplaced, Is.True);

            foreach (long ticks in new long[] { 4_000_000, 10_000_000, 25_000_000, 100_000_000 })
            {
                Assert.That(control.Advance(ticks).IsSuccess, Is.True);
                Assert.That(rig.Session.Advance(ticks).IsSuccess, Is.True);
                Assert.That(SaveV2Scenarios.Dump(rig.Session.State, rig.Session.Permanent),
                    Is.EqualTo(SaveV2Scenarios.Dump(control.State, control.Permanent)), "after " + ticks + " ticks");
            }
        }
    }
}
