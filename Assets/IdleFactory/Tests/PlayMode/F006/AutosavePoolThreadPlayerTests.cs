using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using IdleFactory.Core.Application;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T544 (C-65, C-66, research R16, FR-003, SC-008): the timer-driven autosave over the REAL file store, with the default executor (the thread pool), in a built player.
    /// C-74 recorded that no player-compatible rig existed; this one needs none of the editor: a packaged catalog (<see cref="RunPlayerFixtures"/>), an isolated temporary root,
    /// the production <see cref="RunPersistence"/> and <see cref="JsonRunCheckpointStore"/>, and a clock the test moves. Two periodic writes run on the pool thread; each is
    /// applied by a later tick, the generations advance by one each, the previous generation is retained and the files read back. The main-thread cost of each tick (capture
    /// and serialize) is logged as <c>B4B-AUTOSAVE</c> lines. Whether the same code publishes on Android's IL2CPP pool thread is the emulator journey's job (<c>-AutosaveCheck</c>).
    /// </summary>
    [Category("StandalonePlayer")]
    public sealed class AutosavePoolThreadPlayerTests
    {
        private sealed class MovingClock : IUtcClock
        {
            public long UnixMs;

            public DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(UnixMs);
        }

        private string root;

        [SetUp]
        public void SetUp() => root = RunPlayerFixtures.NewRoot();

        [TearDown]
        public void TearDown() => RunPlayerFixtures.DeleteRoot(root);

        private static void Log(string line)
        {
            TestContext.Out.WriteLine(line);
            UnityEngine.Debug.Log(line);
        }

        [Test]
        public void TwoPeriodicWritesOnThePoolThreadPublishTwoGenerationsAndReadBack()
        {
            var catalog = RunPlayerFixtures.IronCatalog();
            var nowMs = RunPlayerFixtures.StartUtcMs;
            var permanent = new PermanentState(catalog);
            var start = NextRunStart.Compute(catalog, permanent);
            var state = new RunState(catalog, Guid.NewGuid(), 1, null, start.StartingCash, 0, start.StorageLevel, 1, catalog.RunStart.InitialUnlocks);
            Assert.That(new RunSimulator(catalog).Advance(state, permanent, 0, nowMs, null), Is.EqualTo(RunAdvanceResult.Ok));
            var session = new RunSession(catalog, state, permanent, nowMs, null);
            var clock = new MovingClock { UnixMs = nowMs };
            var store = new JsonRunCheckpointStore(root, catalog);
            var persistence = new RunPersistence(session, store, clock);
            Assert.That(persistence.ColdStart(null).IsSuccess, Is.True, "a cold start over an empty store starts a fresh run");
            Assert.That(persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(store, Is.InstanceOf<IRunCheckpointBackgroundPromoter>(), "the file store can publish on a pool thread");

            var commands = new RunCommands(session);
            Assert.That(commands.Build(0, "machine.miner").IsSuccess, Is.True, "a Miner, so the run changes while it advances");

            // A cold start over an empty store starts the run in memory; the first durable generation is the first save (the host's departure checkpoint does it in the game).
            Assert.That(persistence.SaveNow().IsSuccess, Is.True, "the first generation");
            var firstGeneration = store.ReadActive();
            Assert.That(firstGeneration.Succeeded, Is.True, firstGeneration.Message);
            var generation = firstGeneration.Checkpoint.Generation;
            var watch = new Stopwatch();

            // The first tick of a live run only anchors the cadence.
            Assert.That(persistence.AutosaveWhenDue(), Is.EqualTo(AutosaveTickOutcome.NotDue));
            for (var round = 1; round <= 2; round++)
            {
                clock.UnixMs += AutosavePolicy.DefaultIntervalMs + 1_000;
                Assert.That(session.Advance(300_000_000).IsSuccess, Is.True);
                watch.Restart();
                var started = persistence.AutosaveWhenDue();
                watch.Stop();
                var mainThreadMs = watch.Elapsed.TotalMilliseconds;
                var written = started == AutosaveTickOutcome.Written;
                var deadline = Stopwatch.StartNew();
                while (!written && deadline.Elapsed.TotalSeconds < 15)
                {
                    Thread.Sleep(5);
                    var outcome = persistence.AutosaveWhenDue();
                    Assert.That(outcome, Is.Not.EqualTo(AutosaveTickOutcome.Failed), "round " + round + ": the pool write did not fail");
                    written = outcome == AutosaveTickOutcome.Written;
                }

                Assert.That(written, Is.True, "round " + round + ": the pool thread published within 15 s and a tick applied the result");
                var active = store.ReadActive();
                Assert.That(active.Succeeded, Is.True, active.Message);
                Assert.That(active.Checkpoint.Generation, Is.EqualTo(generation + round), "round " + round + ": one generation per write");
                Log("B4B-AUTOSAVE round=" + round + " generation=" + active.Checkpoint.Generation + " firstTickMainThreadMs=" + mainThreadMs.ToString("F2", CultureInfo.InvariantCulture) +
                    " backgroundWritesStarted=" + persistence.BackgroundWritesStarted + " timeToAppliedMs=" + deadline.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture) +
                    " machines=" + active.Checkpoint.Run.Machines.Count);
            }

            Assert.That(persistence.BackgroundWritesStarted, Is.GreaterThanOrEqualTo(1), "at least one of the two writes took the pool thread (the first write continues from the cold start's generation)");
            var previous = store.ReadPrevious();
            Assert.That(previous.Succeeded, Is.True, previous.Message);
            Assert.That(previous.Checkpoint.Generation, Is.EqualTo(generation + 1), "the previous generation is retained");
            Assert.That(store.ReadActive().Checkpoint.Id, Is.EqualTo(persistence.Status.ActiveCheckpointId), "the status names the checkpoint on disk");
        }
    }
}
