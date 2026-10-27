using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    /// A session, real <see cref="RunPersistence"/> with the real <see cref="OfflineRunReturnReconciler"/> and a
    /// <see cref="JsonRunCheckpointStore"/> over a scripted backend (or a real temporary root) for the durable transitions
    /// of T105: the tests fail every protocol step one by one and then retry or restart.
    /// </summary>
    internal sealed class DurabilityRig
    {
        public RunCommandFixtures.Rig Rig;
        public RunCommands Commands;
        public ScriptedBackend Backend;
        public JsonRunCheckpointStore Store;
        public FixedUtcClock Clock;
        public RunPersistence Persistence;
        public int NextRunId = 7777;

        public RunSession Session => Rig.Session;

        public string Dump() => SaveV2Scenarios.Dump(Session.State, Session.Permanent);

        /// <summary>Moves the clock to the run timeline, so a departure equals the timeline instant.</summary>
        public void Sync() => Clock.UnixMs = Session.TimelineUtcMs;

        public RunCheckpoint Active() => Store.ReadActive().Checkpoint;

        public RunCheckpoint Previous() => Store.ReadPrevious().Checkpoint;

        public static DurabilityRig Create(ScriptedBackend backend = null, string root = null, bool resume = true, long cash = 50_000_000,
            int firstCheckpointId = 1, RunCommandFixtures.Options options = null, IRunReturnReconciler reconciler = null, IRunEventSink sink = null)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(options ?? SaveV2Scenarios.Options(), cash);
            if (sink != null) rig.Session = new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog), RunCommandFixtures.StartUtcMs, sink);
            return Build(rig, backend, root, resume, firstCheckpointId, reconciler, rig.Session.TimelineUtcMs);
        }

        /// <summary>A restart: a new process (new catalog, session and persistence) over the same files, not yet resumed.</summary>
        public static DurabilityRig Restart(DurabilityRig previous, long clockMs, int firstCheckpointId, string root = null,
            RunCommandFixtures.Options options = null)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(options ?? SaveV2Scenarios.Options(), 25);
            var restarted = Build(rig, previous.Backend, root, false, firstCheckpointId, null, clockMs);
            return restarted;
        }

        private static DurabilityRig Build(RunCommandFixtures.Rig rig, ScriptedBackend backend, string root, bool resume, int firstCheckpointId,
            IRunReturnReconciler reconciler, long clockMs)
        {
            JsonRunCheckpointStore store;
            if (root != null)
            {
                store = new JsonRunCheckpointStore(root, rig.Catalog);
                backend = null;
            }
            else
            {
                backend = backend ?? new ScriptedBackend();
                store = new JsonRunCheckpointStore("memory-root", rig.Catalog, backend);
            }

            var clock = new FixedUtcClock(clockMs);
            var h = new DurabilityRig
            {
                Rig = rig,
                Backend = backend,
                Store = store,
                Clock = clock,
                Persistence = new RunPersistence(rig.Session, store, clock, new SequenceCheckpointIds(firstCheckpointId),
                    reconciler ?? new OfflineRunReturnReconciler(rig.Catalog)),
            };
            h.Commands = new RunCommands(rig.Session, () => RunSimHarness.Id(rig.NextId++), () => RunSimHarness.Id(h.NextRunId++));
            if (resume) Ok(h.Persistence.ResumeAndReconcile());
            return h;
        }

        public static RunPersistenceOutcome Ok(RunCommandResult<RunPersistenceOutcome> result, string context = "")
        {
            Assert.That(result.IsSuccess, Is.True, result.ReasonCode + " " + context);
            return result.Value;
        }

        public static void Rejected(RunCommandResult<RunPersistenceOutcome> result, string reason)
        {
            Assert.That(result.IsSuccess, Is.False, "expected " + reason);
            Assert.That(result.ReasonCode, Is.EqualTo(reason));
        }

        /// <summary>Two machines, auto-sell, a bigger store and Overdrive, then 1.3 s of production.</summary>
        public void Play()
        {
            Assert.That(Rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            Assert.That(Rig.Build(1, RunCommandFixtures.Smelter).IsSuccess, Is.True);
            Assert.That(Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            Assert.That(Commands.UpgradeStorage().IsSuccess, Is.True);
            Assert.That(Commands.ActivateOverdrive().IsSuccess, Is.True);
            Rig.Advance(13_000_000);
        }

        /// <summary>A played game whose departure checkpoint is durable and whose process is suspended.</summary>
        public static DurabilityRig Suspended(ScriptedBackend backend = null, long absenceMs = 2 * 3_600_000L)
        {
            DurabilityRig h = Create(backend);
            h.Play();
            h.Sync();
            Ok(h.Persistence.RecordSuspension());
            h.Clock.UnixMs = h.Active().DepartureUtcMs + absenceMs;
            return h;
        }

        /// <summary>The reference result of a return: the segmenter over a private fresh read of the checkpoint.</summary>
        public static string ReferenceDump(DurabilityRig h, CheckpointId sourceId, long returnUtcMs, out RunOfflineSummary summary)
        {
            RunCheckpoint source = h.Store.Read(sourceId).Checkpoint;
            summary = new OfflineSegmenter(h.Rig.Catalog).Reconcile(source.Run, source.Permanent, source.DepartureUtcMs, returnUtcMs);
            return SaveV2Scenarios.Dump(source.Run, source.Permanent);
        }
    }
}
