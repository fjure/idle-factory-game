#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using IdleFactory.Run.Domain;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T544 (C-94, Feature 006, FR-006, SC-008): the per-frame affordability mask that T563 added to <c>RunShellPresenter.Refresh</c> allocates nothing. The presenter is
    /// the one of the shipped scene (real shell, real grid). Twelve running machines have an affordable upgrade each, so the mask loop runs over all of them on every
    /// frame; <c>Refresh</c> is called at every one of 600 frames and counted with the <c>GC.Alloc</c> recorder. The first case keeps the cash still (the pure steady state
    /// of the mask comparison), the second moves the cash every frame without crossing any upgrade cost (the mask is evaluated, nothing rebuilds). The measured numbers are
    /// logged as <c>B4B-REFRESH</c> lines; the Windows player measurement of the whole frame is in <c>W006VRealScenePlayerTests</c>.
    /// </summary>
    public sealed class RunShellPresenterAllocationTests
    {
        private const int Frames = 600;
        private string root;
        private RunSceneClock clock;
        private RunSceneRun run;

        [SetUp]
        public void SetUp()
        {
            root = RunSceneRun.NewRoot();
            clock = new RunSceneClock();
            run = null;
        }

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            if (run != null)
            {
                yield return run.Stop();
            }

            RunSceneRun.DeleteRoot(root);
        }

        private static IEnumerator Frames_(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private IEnumerator StartWithAffordableUpgrades()
        {
            run = new RunSceneRun(root, clock);
            yield return run.Start(RunSceneRun.IronBytes);
            yield return Frames_(2);
            var bootstrap = run.Bootstrap;
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            Assert.That(bootstrap.RunShell.IsReady, Is.True, bootstrap.RunShell.Error);
            // A large cash first: every build and factory upgrade is affordable, and so is every machine upgrade afterwards.
            Assert.That(bootstrap.RunSession.State.TryAddCash(1_000_000_000), Is.EqualTo(RunStateResult.Ok));
            var built = 0;
            for (var slot = 0; slot < bootstrap.SlotGrid.Layout.LocationCount; slot++)
            {
                var result = bootstrap.RunCommands.Build(slot, "machine.miner");
                if (!result.IsSuccess)
                {
                    bootstrap.RunCommands.UpgradeFactory();
                    result = bootstrap.RunCommands.Build(slot, "machine.miner");
                }

                if (result.IsSuccess)
                {
                    built++;
                }
            }

            Assert.That(built, Is.GreaterThanOrEqualTo(2), "machines are built (the factory level limits the locations at the start)");
            yield return Frames_(60);
        }

        private static int MarkerCount(IReadOnlyList<SlotVisual> visuals)
        {
            var count = 0;
            for (var i = 0; i < visuals.Count; i++)
            {
                count += visuals[i].UpgradeMarker ? 1 : 0;
            }

            return count;
        }

        [UnityTest]
        public IEnumerator RefreshAllocatesNothingOver600FramesWithTheCashStill()
        {
            yield return StartWithAffordableUpgrades();
            var presenter = run.Bootstrap.ShellPresenter;
            Assert.That(presenter.Visuals.Count, Is.GreaterThan(0), "the visuals the mask reads are cached");
            Assert.That(MarkerCount(presenter.Visuals), Is.GreaterThan(0), "at least one affordable upgrade: the mask loop has work");
            presenter.Refresh();
            long allocations = 0;
            var framesWithAllocations = 0;
            var rebuilds = 0;
            var snapshot = presenter.Snapshot;
            for (var frame = 0; frame < Frames; frame++)
            {
                yield return null;
                var count = GcAllocationCounter.Count(presenter.Refresh);
                if (!ReferenceEquals(presenter.Snapshot, snapshot))
                {
                    snapshot = presenter.Snapshot;
                    rebuilds++;
                    continue; // a structural rebuild (a machine state moved on its own): not the steady state this case counts
                }

                allocations += count;
                framesWithAllocations += count > 0 ? 1 : 0;
            }

            Debug.Log("B4B-REFRESH case=cash-still frames=" + Frames + " steadyAllocations=" + allocations + " framesWithAllocations=" + framesWithAllocations + " rebuildFrames=" + rebuilds +
                " markers=" + MarkerCount(presenter.Visuals).ToString(CultureInfo.InvariantCulture) + " locations=" + presenter.Visuals.Count + " runtime=editor");
            Assert.That(allocations, Is.Zero, "the affordability mask comparison allocates nothing in the steady state");
            Assert.That(rebuilds, Is.LessThan(Frames), "most frames were steady");
        }

        [UnityTest]
        public IEnumerator RefreshDoesNotRebuildTheSnapshotWhileTheCashMovesWithoutCrossingAnUpgradeCost()
        {
            yield return StartWithAffordableUpgrades();
            var bootstrap = run.Bootstrap;
            var presenter = bootstrap.ShellPresenter;
            presenter.Refresh();
            var markersBefore = MarkerCount(presenter.Visuals);
            Assert.That(markersBefore, Is.GreaterThan(0));
            var snapshot = presenter.Snapshot;
            var rebuilds = 0;
            long allocations = 0;
            var framesWithAllocations = 0;
            for (var frame = 0; frame < Frames; frame++)
            {
                Assert.That(bootstrap.RunSession.State.TryAddCash(1), Is.EqualTo(RunStateResult.Ok));
                yield return null;
                var count = GcAllocationCounter.Count(presenter.Refresh);
                if (!ReferenceEquals(presenter.Snapshot, snapshot))
                {
                    snapshot = presenter.Snapshot;
                    rebuilds++;
                    continue;
                }

                allocations += count;
                framesWithAllocations += count > 0 ? 1 : 0;
            }

            Debug.Log("B4B-REFRESH case=cash-moves frames=" + Frames + " steadyAllocations=" + allocations + " framesWithAllocations=" + framesWithAllocations + " rebuildFrames=" + rebuilds +
                " markers=" + MarkerCount(presenter.Visuals).ToString(CultureInfo.InvariantCulture) + " runtime=editor (the cash label formats a string per change)");
            Assert.That(MarkerCount(presenter.Visuals), Is.EqualTo(markersBefore), "the markers did not change: no upgrade cost was crossed");
            Assert.That(rebuilds, Is.LessThan(Frames / 10), "moving cash alone does not rebuild the factory snapshot: the mask comparison is the only per-frame work");
        }
    }
}
#endif
