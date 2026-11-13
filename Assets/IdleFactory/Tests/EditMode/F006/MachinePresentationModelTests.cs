using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T532 (Feature 006, research R08, contracts/presentation-model.md section 2, FR-025, FR-027, FR-029): the derived machine presentation.
    /// The read models are synthetic (see <see cref="PresentationModelSynth"/>), so every state, cycle time and counter is exact.
    /// </summary>
    public sealed class MachinePresentationModelTests
    {
        private static readonly FactoryWorldLayout Layout = TestLayouts.Six();

        private static MachinePresentationModel Refreshed(IList<SynthMachine> machines, long quantity = 1, MachinePresentationModel model = null)
        {
            model = model ?? new MachinePresentationModel();
            var built = PresentationModelSynth.Build(1, quantity, machines);
            model.Refresh(Layout, built.Factory, built.Views, built.Views.Length, built.Rates);
            return model;
        }

        private static SynthMachine Machine(int slot, Action<SynthMachine> set = null)
        {
            var machine = new SynthMachine { Slot = slot };
            set?.Invoke(machine);
            return machine;
        }

        // ---------------------------------------------------------------------------------------------------
        // Visual state: the single precedence
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheVisualStateIsTheSingleStateForPrecedenceForEveryCombination()
        {
            var reached = new HashSet<MachineVisualState>();
            foreach (var active in new[] { true, false })
            {
                foreach (MachineBlocker blocker in Enum.GetValues(typeof(MachineBlocker)))
                {
                    foreach (MachinePhase phase in Enum.GetValues(typeof(MachinePhase)))
                    {
                        var model = Refreshed(new[] { Machine(2, m => { m.Active = active; m.Blocker = blocker; m.Phase = phase; }) });
                        var expected = SlotGridModel.StateFor(active, blocker, phase);
                        Assert.That(model[2].VisualState, Is.EqualTo(expected), "active " + active + " blocker " + blocker + " phase " + phase);
                        reached.Add(model[2].VisualState);
                    }
                }
            }

            Assert.That(reached, Is.EquivalentTo(Enum.GetValues(typeof(MachineVisualState)).Cast<MachineVisualState>()), "every visual state is produced");
        }

        [Test]
        public void ARunningMachineReadsRunningAndAnInactiveOneInactive()
        {
            var model = Refreshed(new[] { Machine(0), Machine(1, m => { m.Active = false; m.Blocker = MachineBlocker.Inactive; m.Phase = MachinePhase.Paused; }) });
            Assert.That(model[0].VisualState, Is.EqualTo(MachineVisualState.Running));
            Assert.That(model[1].VisualState, Is.EqualTo(MachineVisualState.Inactive));
        }

        [Test]
        public void LocationsWithoutAMachineAreUnoccupiedAndEveryLocationHasAnEntry()
        {
            var model = Refreshed(new[] { Machine(1) });
            Assert.That(model.LocationCount, Is.EqualTo(Layout.LocationCount));
            for (var i = 0; i < model.LocationCount; i++)
            {
                Assert.That(model[i].IsOccupied, Is.EqualTo(i == 1), "location " + i);
                Assert.That(model[i].Location, Is.EqualTo(new BuildingLocationId(i)));
            }

            Assert.That(model.Get(new BuildingLocationId(99)).IsOccupied, Is.False, "an unknown location is unoccupied, never an exception");
        }

        [Test]
        public void AMachineBeyondTheLayoutIsIgnored()
        {
            var model = Refreshed(new[] { Machine(Layout.LocationCount + 3) });
            for (var i = 0; i < model.LocationCount; i++)
            {
                Assert.That(model[i].IsOccupied, Is.False);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Upgrade marker
        // ---------------------------------------------------------------------------------------------------

        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, false)]
        public void UpgradeAvailableNeedsAnAvailableAndAffordableUpgrade(bool available, bool affordable, bool expected)
        {
            var model = Refreshed(new[] { Machine(0, m => { m.UpgradeAvailable = available; m.UpgradeAffordable = affordable; }) });
            Assert.That(model[0].UpgradeAvailable, Is.EqualTo(expected));
        }

        [Test]
        public void TheUpgradeMarkerIsHiddenUnderEveryProblemBadgeAndShownOtherwise()
        {
            var problems = new[] { MachineVisualState.MissingInput, MachineVisualState.StorageFull, MachineVisualState.Waiting };
            foreach (MachineVisualState state in Enum.GetValues(typeof(MachineVisualState)))
            {
                var machine = Machine(0, m => { m.UpgradeAvailable = true; m.UpgradeAffordable = true; });
                switch (state)
                {
                    case MachineVisualState.MissingInput: machine.Blocker = MachineBlocker.MissingInput; break;
                    case MachineVisualState.StorageFull: machine.Blocker = MachineBlocker.StorageFull; break;
                    case MachineVisualState.Waiting: machine.Phase = MachinePhase.Waiting; break;
                    case MachineVisualState.Paused: machine.Phase = MachinePhase.Paused; break;
                    case MachineVisualState.Inactive: machine.Active = false; machine.Blocker = MachineBlocker.Inactive; break;
                }

                var model = Refreshed(new[] { machine });
                Assert.That(model[0].VisualState, Is.EqualTo(state));
                Assert.That(model[0].UpgradeAvailable, Is.True, "the raw flag stays set under " + state);
                Assert.That(model[0].UpgradeMarkerVisible, Is.EqualTo(!problems.Contains(state)), "marker under " + state);
                Assert.That(MachinePresentationModel.IsProblemState(state), Is.EqualTo(problems.Contains(state)));
            }
        }

        [Test]
        public void AnUnaffordableUpgradeNeverShowsAMarker()
        {
            var model = Refreshed(new[] { Machine(0, m => { m.UpgradeAvailable = true; m.UpgradeAffordable = false; }) });
            Assert.That(model[0].UpgradeMarkerVisible, Is.False);
        }

        [Test]
        public void TheUpgradeMarkerIsSmallerThanABadge()
        {
            Assert.That(StateCueGeometry.MarkerSizePixels, Is.LessThan(StateCueGeometry.BadgeSizePixels));
            Assert.That(StateCueGeometry.MarkerBox(0).width, Is.LessThan(StateCueGeometry.BadgeBox().width));
        }

        // ---------------------------------------------------------------------------------------------------
        // Activity pace
        // ---------------------------------------------------------------------------------------------------

        [TestCase(40_000_000L, ActivityPace.Slow)]
        [TestCase(120_000_000L, ActivityPace.Slow)]
        [TestCase(39_999_999L, ActivityPace.Normal)]
        [TestCase(10_000_000L, ActivityPace.Normal)]
        [TestCase(20_000_000L, ActivityPace.Normal)]
        [TestCase(9_999_999L, ActivityPace.Fast)]
        [TestCase(1L, ActivityPace.Fast)]
        [TestCase(0L, ActivityPace.Normal)]
        public void TheCycleTimeSelectsThePaceBucket(long cycleTicks, ActivityPace expected)
        {
            Assert.That(MachinePresentationModel.PaceOf(cycleTicks), Is.EqualTo(expected));
            var model = Refreshed(new[] { Machine(0, m => m.CycleTicks = cycleTicks) });
            Assert.That(model[0].Pace, Is.EqualTo(expected));
        }

        [Test]
        public void ThePaceSelectsSixEightOrTenFramesPerSecondAndFourSixOrEightHundredMillisecondsPerCell()
        {
            Assert.That(MachinePresentationModel.FpsOf(ActivityPace.Slow), Is.EqualTo(6));
            Assert.That(MachinePresentationModel.FpsOf(ActivityPace.Normal), Is.EqualTo(8));
            Assert.That(MachinePresentationModel.FpsOf(ActivityPace.Fast), Is.EqualTo(10));
            Assert.That(MachinePresentationModel.PipMillisecondsPerCell(ActivityPace.Slow), Is.EqualTo(800));
            Assert.That(MachinePresentationModel.PipMillisecondsPerCell(ActivityPace.Normal), Is.EqualTo(600));
            Assert.That(MachinePresentationModel.PipMillisecondsPerCell(ActivityPace.Fast), Is.EqualTo(400));
        }

        [Test]
        public void TheAssemblerIsCappedAtEightFramesPerSecondEvenWhenFast()
        {
            var model = Refreshed(new[]
            {
                Machine(0, m => { m.MachineId = RunCommandFixtures.Assembler; m.CycleTicks = 5_000_000; }),
                Machine(1, m => { m.MachineId = RunCommandFixtures.Miner; m.CycleTicks = 5_000_000; }),
                Machine(2, m => { m.MachineId = RunCommandFixtures.Assembler; m.CycleTicks = 90_000_000; }),
            });
            Assert.That(model[0].Pace, Is.EqualTo(ActivityPace.Fast), "the pace bucket itself is not capped");
            Assert.That(model[0].ActivityFps, Is.EqualTo(8), "the Assembler stays at 8 fps or below (R12)");
            Assert.That(model[1].ActivityFps, Is.EqualTo(10), "another family runs at the pace rate");
            Assert.That(model[2].ActivityFps, Is.EqualTo(6), "a cap never raises a slower pace");
        }

        [Test]
        public void ACustomFamilyCapTableAppliesAndNoCapLeavesThePaceRate()
        {
            var caps = new Dictionary<string, int> { { RunCommandFixtures.Press, 6 } };
            var capped = new MachinePresentationModel(caps);
            var built = PresentationModelSynth.Build(1, 1, new[] { Machine(0, m => { m.MachineId = RunCommandFixtures.Press; m.CycleTicks = 5_000_000; }) });
            capped.Refresh(Layout, built.Factory, built.Views, built.Views.Length);
            Assert.That(capped[0].ActivityFps, Is.EqualTo(6));

            var open = new MachinePresentationModel(MachinePresentationModel.NoFamilyFpsCaps);
            var assembler = PresentationModelSynth.Build(1, 1, new[] { Machine(0, m => { m.MachineId = RunCommandFixtures.Assembler; m.CycleTicks = 5_000_000; }) });
            open.Refresh(Layout, assembler.Factory, assembler.Views, assembler.Views.Length);
            Assert.That(open[0].ActivityFps, Is.EqualTo(10));
        }

        [Test]
        public void AMissingCycleTimeIsNormal()
        {
            var built = PresentationModelSynth.Build(1, 1, new[] { Machine(0) });
            var model = new MachinePresentationModel();
            model.Refresh(Layout, built.Factory, built.Views, built.Views.Length);
            Assert.That(model[0].Pace, Is.EqualTo(ActivityPace.Normal));
        }

        // ---------------------------------------------------------------------------------------------------
        // Completion delta and baseline resets
        // ---------------------------------------------------------------------------------------------------

        private static void Step(MachinePresentationModel model, long quantity, long counter, int instance = 0, bool suppress = false, int slot = 0)
        {
            var built = PresentationModelSynth.Build(1, quantity, new[] { Machine(slot, m => { m.Completed = counter; m.Instance = instance; }) });
            model.Refresh(Layout, built.Factory, built.Views, built.Views.Length, built.Rates, suppress);
        }

        [Test]
        public void TheFirstRefreshTakesTheBaselineAndReportsNoDelta()
        {
            var model = new MachinePresentationModel();
            Step(model, 1, 7);
            Assert.That(model[0].CompletionsDelta, Is.Zero, "a machine seen for the first time is a baseline reset");
        }

        [Test]
        public void TheDeltaIsTheCounterDifferenceReportedOnce()
        {
            var model = new MachinePresentationModel();
            Step(model, 1, 4);
            Step(model, 2, 5);
            Assert.That(model[0].CompletionsDelta, Is.EqualTo(1));
            Step(model, 3, 5);
            Assert.That(model[0].CompletionsDelta, Is.Zero, "no completion since the last refresh");
            Step(model, 4, 7);
            Assert.That(model[0].CompletionsDelta, Is.EqualTo(2));
        }

        [Test]
        public void ASkippedRefreshNeverRepeatsADelta()
        {
            var model = new MachinePresentationModel();
            Step(model, 1, 0);
            Step(model, 2, 1);
            Assert.That(model[0].CompletionsDelta, Is.EqualTo(1));
            var same = PresentationModelSynth.Build(1, 2, new[] { Machine(0, m => m.Completed = 1) });
            Assert.That(model.NeedsRefresh(1, 2, Layout.LocationCount), Is.False);
            Assert.That(model.Refresh(Layout, same.Factory, same.Views, same.Views.Length), Is.False, "the same revisions are skipped");
            Assert.That(model[0].CompletionsDelta, Is.Zero, "a view that reads the buffer every frame spawns an effect once");
            Assert.That(model[0].IsOccupied, Is.True, "the rest of the value is kept");
        }

        [Test]
        public void ARevisionOrLocationCountChangeNeedsARefresh()
        {
            var model = new MachinePresentationModel();
            Assert.That(model.NeedsRefresh(1, 1, 6), Is.True, "nothing computed yet");
            Step(model, 1, 0);
            Assert.That(model.NeedsRefresh(1, 1, 6), Is.False);
            Assert.That(model.NeedsRefresh(2, 1, 6), Is.True);
            Assert.That(model.NeedsRefresh(1, 2, 6), Is.True);
            Assert.That(model.NeedsRefresh(1, 1, 7), Is.True);
        }

        [Test]
        public void ADecreaseIsABaselineResetWithDeltaZeroAndTheNewBaselineHolds()
        {
            var model = new MachinePresentationModel();
            Step(model, 1, 9);
            Step(model, 2, 2); // restore or prestige: the counter restarts
            Assert.That(model[0].CompletionsDelta, Is.Zero);
            Step(model, 3, 3);
            Assert.That(model[0].CompletionsDelta, Is.EqualTo(1), "counted from the lowered baseline");
        }

        [Test]
        public void AReplacedMachineStartsANewBaselineEvenWhenItsCounterIsHigher()
        {
            var model = new MachinePresentationModel();
            Step(model, 1, 0, instance: 100);
            Step(model, 2, 5, instance: 200);
            Assert.That(model[0].CompletionsDelta, Is.Zero, "another instance id at the same location");
            Step(model, 3, 6, instance: 200);
            Assert.That(model[0].CompletionsDelta, Is.EqualTo(1));
        }

        [Test]
        public void OfflineSuppressionReportsNoDeltaAndAdvancesTheBaseline()
        {
            var model = new MachinePresentationModel();
            Step(model, 1, 3);
            Step(model, 2, 40, suppress: true); // the offline return reconciled many completions
            Assert.That(model[0].CompletionsDelta, Is.Zero, "offline deltas never produce effects");
            Step(model, 3, 41);
            Assert.That(model[0].CompletionsDelta, Is.EqualTo(1), "live completions after the return show again");
        }

        [Test]
        public void ResetBaselineForgetsTheCounterAndTheRevisions()
        {
            var model = new MachinePresentationModel();
            Step(model, 1, 3);
            model.ResetBaseline();
            Assert.That(model.NeedsRefresh(1, 1, Layout.LocationCount), Is.True, "a new session may repeat old revisions");
            Step(model, 1, 8);
            Assert.That(model[0].CompletionsDelta, Is.Zero);
        }

        [Test]
        public void ABurstIsCappedForDisplayButTheBaselineAdvancesByTheFullDifference()
        {
            var model = new MachinePresentationModel();
            Step(model, 1, 0);
            Step(model, 2, 50);
            Assert.That(model[0].CompletionsDelta, Is.EqualTo(MachinePresentationModel.MaxDisplayedCompletionsDelta));
            Step(model, 3, 51);
            Assert.That(model[0].CompletionsDelta, Is.EqualTo(1), "the burst is not reported again");
        }

        [Test]
        public void ARemovedMachineForgetsItsBaseline()
        {
            var model = new MachinePresentationModel();
            Step(model, 1, 6, instance: 100);
            var empty = PresentationModelSynth.Build(1, 2, new List<SynthMachine>());
            model.Refresh(Layout, empty.Factory, empty.Views, 0);
            Assert.That(model[0].IsOccupied, Is.False);
            Step(model, 3, 6, instance: 100);
            Assert.That(model[0].CompletionsDelta, Is.Zero, "a machine built there later is seen for the first time");
        }

        [Test]
        public void TwoMachinesCountIndependently()
        {
            var model = new MachinePresentationModel();
            var first = PresentationModelSynth.Build(1, 1, new[] { Machine(0, m => m.Completed = 0), Machine(1, m => m.Completed = 0) });
            model.Refresh(Layout, first.Factory, first.Views, first.Views.Length);
            var next = PresentationModelSynth.Build(1, 2, new[] { Machine(0, m => m.Completed = 2), Machine(1, m => m.Completed = 0) });
            model.Refresh(Layout, next.Factory, next.Views, next.Views.Length);
            Assert.That(model[0].CompletionsDelta, Is.EqualTo(2));
            Assert.That(model[1].CompletionsDelta, Is.Zero);
        }

        // ---------------------------------------------------------------------------------------------------
        // Limiting resource, determinism, buffers
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void AMissingInputMachineNamesItsFirstShortResource()
        {
            var machines = new[]
            {
                Machine(0, m => { m.Blocker = MachineBlocker.MissingInput; m.Missing = new[] { "iron.bar", "iron.plate" }; }),
                Machine(1),
            };
            var model = Refreshed(machines);
            Assert.That(model[0].LimitingResourceId, Is.EqualTo("iron.bar"));
            Assert.That(model[1].LimitingResourceId, Is.Null);
        }

        [Test]
        public void TheSameInputsGiveTheSameBufferAndTheModelWritesNothingBack()
        {
            var machines = new[]
            {
                Machine(0, m => { m.Completed = 3; m.UpgradeAvailable = true; m.UpgradeAffordable = true; }),
                Machine(1, m => { m.Blocker = MachineBlocker.StorageFull; m.CycleTicks = 5_000_000; }),
            };
            var a = Refreshed(machines);
            var b = Refreshed(machines);
            for (var i = 0; i < a.LocationCount; i++)
            {
                Assert.That(b[i].VisualState, Is.EqualTo(a[i].VisualState));
                Assert.That(b[i].Pace, Is.EqualTo(a[i].Pace));
                Assert.That(b[i].UpgradeAvailable, Is.EqualTo(a[i].UpgradeAvailable));
                Assert.That(b[i].CompletionsDelta, Is.EqualTo(a[i].CompletionsDelta));
            }
        }

        [Test]
        public void TheBufferIsSizedFromTheLocationCountOfTheLayout()
        {
            var twenty = TestLayouts.Twenty();
            var built = PresentationModelSynth.Build(1, 1, new[] { Machine(19) });
            var model = new MachinePresentationModel();
            model.Refresh(twenty, built.Factory, built.Views, built.Views.Length);
            Assert.That(model.LocationCount, Is.EqualTo(20));
            Assert.That(model[19].IsOccupied, Is.True);
        }

        [Test]
        public void ASteadyStateRefreshAllocatesNothing()
        {
            var model = new MachinePresentationModel();
            var snapshots = new List<PresentationModelSynth.Models>();
            for (var q = 1; q <= 8; q++)
            {
                snapshots.Add(PresentationModelSynth.Build(1, q, new[]
                {
                    Machine(0, m => m.Completed = q), Machine(1, m => { m.Blocker = MachineBlocker.MissingInput; m.Missing = new[] { "iron.ore" }; }),
                    Machine(2, m => { m.MachineId = RunCommandFixtures.Assembler; m.UpgradeAvailable = true; m.UpgradeAffordable = true; }),
                }));
            }

            foreach (var s in snapshots)
            {
                model.Refresh(Layout, s.Factory, s.Views, s.Views.Length, s.Rates);
            }

            var allocations = GcAllocationCounter.Count(() =>
            {
                for (var round = 0; round < 20; round++)
                {
                    foreach (var s in snapshots)
                    {
                        model.ResetBaseline();
                        model.Refresh(Layout, s.Factory, s.Views, s.Views.Length, s.Rates);
                        _ = model[0].CompletionsDelta;
                        model.NeedsRefresh(1, 2, 6);
                    }
                }
            });
            Assert.That(allocations, Is.Zero, "the refresh reuses its buffers");
        }
    }
}
