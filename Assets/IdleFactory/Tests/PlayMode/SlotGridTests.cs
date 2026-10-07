using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T112: the fixed 12-slot grid. The geometry, the hit test and the per-slot visual are pure functions of the slot
    /// index and the run read model (Art Bible sections 2, 5 and 6); the scene tests prove the world draws them, a slot tap
    /// opens the right sheet and the zoom limits are settings, not literals.
    /// </summary>
    public sealed class SlotGridTests
    {
        private const string MinerId = "machine.miner";
        private const string SmelterId = "machine.smelter";
        private const string PressId = "machine.press";
        private const string AssemblerId = "machine.assembler";

        private string root;
        private RunSceneClock clock;
        private RunSceneRun run;

        [SetUp]
        public void CreateRoot()
        {
            root = RunSceneRun.NewRoot();
            clock = new RunSceneClock();
            run = null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (run != null)
            {
                yield return run.Stop();
            }

            RunSceneRun.DeleteRoot(root);
        }

        // ---------------------------------------------------------------------------------------------------
        // Geometry (pure)
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void ColumnsFollowTheScreenAspectAndTheGridHoldsTwelveSlots()
        {
            Assert.That(SlotGridLayout.SlotCount, Is.EqualTo(12));
            Assert.That(SlotGridLayout.ColumnsFor(360f, 640f), Is.EqualTo(3), "portrait is 3 columns by 4 rows");
            Assert.That(SlotGridLayout.ColumnsFor(640f, 360f), Is.EqualTo(4), "landscape is 4 columns by 3 rows");
            Assert.That(SlotGridLayout.ColumnsFor(500f, 500f), Is.EqualTo(3), "a square screen takes the portrait grid");
            Assert.That(SlotGridLayout.RowsFor(3), Is.EqualTo(4));
            Assert.That(SlotGridLayout.RowsFor(4), Is.EqualTo(3));
        }

        [Test]
        public void ExtentsMatchTheArtBible()
        {
            // Slot 2 cells, pitch 2.5 cells: 448x608 px (7 x 9.5 cells) and 608x448 px (9.5 x 7 cells).
            Assert.That(SlotGridLayout.Extent(3).size, Is.EqualTo(new Vector2(7f, 9.5f)));
            Assert.That(SlotGridLayout.Extent(4).size, Is.EqualTo(new Vector2(9.5f, 7f)));
        }

        [Test]
        public void SlotsFollowReadingOrderFromTheTopLeft([Values(3, 4)] int columns)
        {
            var extent = SlotGridLayout.Extent(columns);
            Assert.That(SlotGridLayout.SlotOrigin(0, columns).x, Is.EqualTo(0f), "slot 1 is in the left column");
            Assert.That(SlotGridLayout.SlotOrigin(0, columns).y + SlotGridLayout.SlotSize, Is.EqualTo(extent.yMax), "slot 1 is in the top row");
            for (var i = 1; i < SlotGridLayout.SlotCount; i++)
            {
                var previous = SlotGridLayout.SlotOrigin(i - 1, columns);
                var current = SlotGridLayout.SlotOrigin(i, columns);
                if (i % columns != 0)
                {
                    Assert.That(current.x, Is.EqualTo(previous.x + SlotGridLayout.SlotPitch), $"slot {i + 1} follows slot {i} to the right");
                    Assert.That(current.y, Is.EqualTo(previous.y));
                }
                else
                {
                    Assert.That(current.x, Is.EqualTo(0f), $"slot {i + 1} starts a new row");
                    Assert.That(current.y, Is.EqualTo(previous.y - SlotGridLayout.SlotPitch), $"slot {i + 1} is one row lower");
                }
            }

            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                var rect = SlotGridLayout.SlotRect(i, columns);
                Assert.That(extent.Contains(new Vector2(rect.xMin, rect.yMin)) && extent.Contains(new Vector2(rect.xMax - 0.001f, rect.yMax - 0.001f)), Is.True,
                    $"slot {i + 1} lies inside the extent");
            }
        }

        [Test]
        public void HitTestResolvesEveryPointOfASlotAndTheGapsToTheNearestSlot([Values(3, 4)] int columns)
        {
            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                var rect = SlotGridLayout.SlotRect(i, columns);
                foreach (var point in new[]
                         {
                             rect.center,
                             new Vector2(rect.xMin + 0.01f, rect.yMin + 0.01f),
                             new Vector2(rect.xMax - 0.01f, rect.yMax - 0.01f),
                         })
                {
                    Assert.That(SlotGridLayout.HitTest(point, columns), Is.EqualTo(i), $"slot {i + 1} at {point}");
                }
            }

            // A tap in the 0.5 cell gap between slot 1 and slot 2 belongs to the nearer one.
            var first = SlotGridLayout.SlotRect(0, columns);
            Assert.That(SlotGridLayout.HitTest(new Vector2(first.xMax + 0.1f, first.center.y), columns), Is.EqualTo(0));
            Assert.That(SlotGridLayout.HitTest(new Vector2(first.xMax + 0.4f, first.center.y), columns), Is.EqualTo(1));
            Assert.That(SlotGridLayout.HitTest(new Vector2(-1f, 1f), columns), Is.EqualTo(-1));
            Assert.That(SlotGridLayout.HitTest(new Vector2(first.center.x, 100f), columns), Is.EqualTo(-1));
            Assert.That(SlotGridLayout.HitTest(new Vector2(100f, first.center.y), columns), Is.EqualTo(-1));
        }

        [TestCase(360f, 536f, 3, 45.0f)]
        [TestCase(390f, 740f, 3, 48.8f)]
        [TestCase(640f, 256f, 4, 32.0f)]
        [TestCase(844f, 286f, 4, 35.8f)]
        [TestCase(408f, 976f, 3, 51.0f)]
        [TestCase(664f, 720f, 4, 63.2f)]
        [TestCase(920f, 672f, 4, 84.0f)]
        [TestCase(1560f, 1032f, 4, 129.0f)]
        public void FitAllReproducesTheArtBibleTableAndStaysAboveTheReadableFloor(float width, float height, int columns, float expected)
        {
            var ppc = SlotGridLayout.FitAllPixelsPerCell(width, height, columns);
            Assert.That(ppc, Is.EqualTo(expected).Within(0.06f));
            Assert.That(ppc, Is.GreaterThanOrEqualTo(new RunZoomSettings().fitAllFloorPixelsPerCell));
        }

        [Test]
        public void ZoomLimitsAreSettingsWithTheArtBibleDefaults()
        {
            var settings = new RunZoomSettings();
            Assert.That(settings.minimumPixelsPerCell, Is.EqualTo(16f));
            Assert.That(settings.maximumPixelsPerCell, Is.EqualTo(64f));
            Assert.That(settings.fitAllFloorPixelsPerCell, Is.EqualTo(24f));
            Assert.That(settings.Clamp(8f), Is.EqualTo(16f));
            Assert.That(settings.Clamp(100f), Is.EqualTo(64f));
            Assert.That(settings.Clamp(40f), Is.EqualTo(40f));
        }

        [Test]
        public void BadgesAndMarkersScaleForTheProjectedSizeAndMarkersHideBelowTheFloor()
        {
            // Art Bible 8.2: a badge is at least 27 points wide and never below its natural size; markers are at least 10 points
            // and hidden below 24 pixels per cell.
            Assert.That(SlotGridView.BadgeScale(16f) * 72f / 64f * 16f, Is.GreaterThanOrEqualTo(27f - 0.01f));
            Assert.That(SlotGridView.BadgeScale(64f), Is.EqualTo(1f));
            Assert.That(SlotGridView.MarkerScale(24f) * 28f / 64f * 24f, Is.GreaterThanOrEqualTo(10f - 0.01f));
            Assert.That(SlotGridView.MarkersVisibleAt(16f), Is.False);
            Assert.That(SlotGridView.MarkersVisibleAt(24f), Is.True);
        }

        // ---------------------------------------------------------------------------------------------------
        // Slot visual (pure function of the slot index and the read model)
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void BadgePrecedenceFollowsTheArtBibleTable()
        {
            string Badge(bool active, MachineBlocker blocker, MachinePhase phase, out string text)
            {
                var key = SlotGridModel.BadgeFor(active, blocker, phase, out text, out _);
                return key;
            }

            Assert.That(Badge(false, MachineBlocker.MissingInput, MachinePhase.Paused, out var text), Is.EqualTo("F003-BADGE-INACTIVE"), "rank 1 beats rank 2");
            Assert.That(text, Is.EqualTo("OFF"));
            Assert.That(Badge(true, MachineBlocker.Inactive, MachinePhase.Paused, out text), Is.EqualTo("F003-BADGE-INACTIVE"));
            Assert.That(Badge(true, MachineBlocker.MissingInput, MachinePhase.Waiting, out text), Is.EqualTo("F003-BADGE-MISSING-INPUT"), "rank 2 beats waiting");
            Assert.That(text, Is.EqualTo("IN!"));
            Assert.That(Badge(true, MachineBlocker.StorageFull, MachinePhase.Waiting, out text), Is.EqualTo("F003-BADGE-STORAGE-FULL"));
            Assert.That(text, Is.EqualTo("FULL"));
            Assert.That(Badge(true, MachineBlocker.None, MachinePhase.Waiting, out text), Is.EqualTo("F003-BADGE-WAITING"));
            Assert.That(text, Is.EqualTo("WAIT"));
            Assert.That(Badge(true, MachineBlocker.None, MachinePhase.Paused, out text), Is.EqualTo("F003-BADGE-PAUSED"));
            Assert.That(text, Is.EqualTo("PAUS"));
            Assert.That(Badge(true, MachineBlocker.None, MachinePhase.Running, out text), Is.EqualTo("F003-BADGE-RUNNING"));
            Assert.That(text, Is.EqualTo("RUN"));
        }

#if UNITY_EDITOR
        [Test]
        public void EveryOneOfTheTwelveSlotsHasAVisualWithAConditionAndAPlainDescription()
        {
            var session = RunShellFixtures.NewSession(out var commands, factoryLevel: 2, cash: 10_000, unlockEverything: true);
            Assert.That(commands.Build(0, MinerId).IsSuccess, Is.True);
            Assert.That(commands.Build(1, SmelterId).IsSuccess, Is.True);
            var snapshot = RunSnapshots.Factory(session);
            var visuals = SlotGridModel.DescribeAll(snapshot, id => RunShellFixtures.PresentationKeyOf(session, id), false, null, 1);

            Assert.That(visuals.Length, Is.EqualTo(12));
            Assert.That(visuals.Select(visual => visual.Index), Is.EqualTo(Enumerable.Range(0, 12)));
            Assert.That(visuals.Select(visual => visual.Description).Distinct().Count(), Is.EqualTo(12), "every slot reads differently without color");

            Assert.That(visuals[0].Condition, Is.EqualTo(SlotCondition.Occupied));
            Assert.That(visuals[0].MachineKey, Is.EqualTo("F003-MACHINE-MINER"));
            Assert.That(visuals[0].BaseKey, Is.EqualTo(SlotGridModel.SlotOccupiedKey));
            Assert.That(visuals[0].Selected, Is.False);
            Assert.That(visuals[1].Condition, Is.EqualTo(SlotCondition.Occupied));
            Assert.That(visuals[1].Selected, Is.True, "the selected slot carries the selection frame");
            Assert.That(visuals[1].MachineKey, Is.EqualTo("F003-MACHINE-SMELTER"));

            foreach (var visual in visuals.Skip(2).Where(visual => visual.Condition == SlotCondition.Empty))
            {
                Assert.That(visual.BaseKey, Is.EqualTo(SlotGridModel.SlotEmptyKey));
                Assert.That(visual.MachineKey, Is.Null);
                Assert.That(visual.BadgeKey, Is.Null, "an empty slot carries no badge");
                Assert.That(visual.MarkerCount, Is.EqualTo(0), "an empty slot carries no marker");
                Assert.That(visual.Description, Does.Contain("empty"));
            }

            var locked = visuals.Where(visual => visual.Condition == SlotCondition.Locked).ToArray();
            Assert.That(locked.Length, Is.GreaterThan(0), "factory level 2 leaves slots locked");
            foreach (var visual in locked)
            {
                Assert.That(visual.UnlockLevel, Is.InRange(3, 5));
                Assert.That(visual.BaseKey, Is.EqualTo("F003-SLOT-LOCKED-L" + visual.UnlockLevel), "the locked plate names its unlocking level");
                Assert.That(visual.BadgeKey, Is.Null);
                Assert.That(visual.MarkerCount, Is.EqualTo(0));
                Assert.That(visual.Description, Does.Contain("level " + visual.UnlockLevel));
            }

            // Slot indices beyond the snapshot never throw: they draw as the highest locked plate.
            var beyond = SlotGridModel.Describe(40, snapshot, id => RunShellFixtures.PresentationKeyOf(session, id), false, null, -1);
            Assert.That(beyond.Condition, Is.EqualTo(SlotCondition.Locked));
        }

        [Test]
        public void MarkersAreCappedAtTwoAndNeverOnEmptyOrLockedSlots()
        {
            var session = RunShellFixtures.NewSession(out var commands, factoryLevel: 5, cash: 1_000_000, unlockEverything: true);
            foreach (var (slot, id) in new[] { (0, MinerId), (1, SmelterId), (2, PressId), (3, AssemblerId) })
            {
                Assert.That(commands.Build(slot, id).IsSuccess, Is.True, id);
            }

            var snapshot = RunSnapshots.Factory(session);
            var boosted = new HashSet<string> { MinerId, AssemblerId };
            var visuals = SlotGridModel.DescribeAll(snapshot, id => RunShellFixtures.PresentationKeyOf(session, id), true, boosted, -1);
            Assert.That(visuals[0].Boosted && visuals[0].Overdrive, Is.True, "a boosted machine during Overdrive shows both markers");
            Assert.That(visuals[0].MarkerCount, Is.EqualTo(2));
            Assert.That(visuals[1].Boosted, Is.False);
            Assert.That(visuals[1].Overdrive, Is.True, "Overdrive marks every occupied slot");
            Assert.That(visuals[1].MarkerCount, Is.EqualTo(1));
            foreach (var visual in visuals)
            {
                Assert.That(visual.MarkerCount, Is.LessThanOrEqualTo(2), "at most two markers per slot (ART-2)");
                if (visual.Condition != SlotCondition.Occupied)
                {
                    Assert.That(visual.MarkerCount, Is.EqualTo(0), $"slot {visual.Index + 1} is not occupied");
                    Assert.That(visual.BadgeKey, Is.Null);
                }
                else
                {
                    Assert.That(visual.BadgeKey, Is.Not.Null, "an occupied slot shows exactly one badge");
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Scene: the world draws the grid, a tap opens the right sheet
        // ---------------------------------------------------------------------------------------------------

        private IEnumerator StartRun()
        {
            run = new RunSceneRun(root, clock);
            yield return run.Start();
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheSceneComposesTheShellAndDrawsTwelveSlotsFromTheRunReadModel()
        {
            yield return StartRun();
            var bootstrap = run.Bootstrap;
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            Assert.That(bootstrap.RunShell, Is.Not.Null);
            Assert.That(bootstrap.RunShell.IsReady, Is.True, bootstrap.RunShell.Error);
            Assert.That(bootstrap.Hud.Root.resolvedStyle.display, Is.EqualTo(UnityEngine.UIElements.DisplayStyle.None), "the run shell replaces the Feature 001 HUD");

            var grid = bootstrap.SlotGrid;
            Assert.That(grid.Visuals.Count, Is.EqualTo(12));
            var plates = grid.GetComponentsInChildren<SpriteRenderer>().Count(renderer => renderer.enabled && renderer.sortingOrder == SlotGridView.PlateOrder);
            Assert.That(plates, Is.EqualTo(12), "one plate per slot");
            Assert.That(grid.Visuals.Count(visual => visual.Condition == SlotCondition.Empty), Is.EqualTo(bootstrap.RunSession.State.SlotCount));
            Assert.That(grid.Visuals.Count(visual => visual.Condition == SlotCondition.Locked), Is.EqualTo(12 - bootstrap.RunSession.State.SlotCount));
            Assert.That(grid.ActiveRendererCount, Is.EqualTo(12), "12 plates and nothing else on a fresh factory");

            // Build a Miner through the application contract: the world follows the read model, never the other way round.
            var built = bootstrap.RunCommands.Build(0, MinerId);
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
            yield return null;
            yield return null;
            Assert.That(grid.Visuals[0].Condition, Is.EqualTo(SlotCondition.Occupied));
            Assert.That(grid.Visuals[0].MachineKey, Is.EqualTo("F003-MACHINE-MINER"));
            Assert.That(grid.Visuals[0].BadgeKey, Is.Not.Null);
            Assert.That(grid.ActiveRendererCount, Is.EqualTo(12 + 1 + 1), "plates, one machine and one badge");
            Assert.That(grid.ActiveRendererCount, Is.LessThanOrEqualTo(61), "at most 61 slot sprites (Art Bible 6.1)");
        }

        [UnityTest]
        public IEnumerator ATapOpensTheBuildSheetForAnEmptySlotAndTheMachinePanelForAnOccupiedSlot()
        {
            yield return StartRun();
            var bootstrap = run.Bootstrap;
            var shell = bootstrap.RunShell;
            var presenter = bootstrap.ShellPresenter;
            var grid = bootstrap.SlotGrid;

            var build = new TestShellPanel(ShellPanelIds.Build, "Build", 2, false);
            var machine = new TestShellPanel(ShellPanelIds.Machine, "Machine", 2, false);
            using (shell.Panels.Register(build))
            using (shell.Panels.Register(machine))
            {
                Assert.That(presenter.HandleWorldTap(grid.WorldRectOf(2).center), Is.True);
                yield return null;
                Assert.That(shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Build), "an empty slot opens the build sheet");
                Assert.That(shell.SelectedSlot, Is.EqualTo(2));
                Assert.That(build.LastSlot, Is.EqualTo(2));
                Assert.That(build.Root.parent, Is.SameAs(shell.PanelContent), "the panel was mounted without editing the shell");
                Assert.That(grid.Visuals[2].Selected, Is.True);

                var built = bootstrap.RunCommands.Build(2, MinerId);
                Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
                yield return null;
                yield return null;
                Assert.That(shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Machine), "after the build the open sheet becomes the machine panel");
                Assert.That(machine.LastSlot, Is.EqualTo(2));

                shell.CloseSheet();
                Assert.That(presenter.HandleWorldTap(grid.WorldRectOf(2).center), Is.True);
                Assert.That(shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Machine), "an occupied slot opens the machine panel");

                Assert.That(presenter.HandleWorldTap(new Vector2(-50f, -50f)), Is.False, "a tap beside the grid hits nothing");
            }

            Assert.That(shell.Panels.Count, Is.EqualTo(0), "disposing the handle unregisters the panel");
        }

        [UnityTest]
        public IEnumerator ATapOnALockedSlotOpensNothingAndNamesTheUnlockingLevel()
        {
            yield return StartRun();
            var bootstrap = run.Bootstrap;
            var shell = bootstrap.RunShell;
            var locked = bootstrap.SlotGrid.Visuals.First(visual => visual.Condition == SlotCondition.Locked);

            Assert.That(bootstrap.ShellPresenter.HandleWorldTap(bootstrap.SlotGrid.WorldRectOf(locked.Index).center), Is.False);
            Assert.That(shell.IsSheetOpen, Is.False);
            Assert.That(shell.SelectedSlot, Is.EqualTo(-1));
            Assert.That(shell.NoticeText, Does.Contain("unlocks at factory level " + locked.UnlockLevel));
        }

        [UnityTest]
        public IEnumerator AnUnregisteredPanelShowsTheNotAvailableStateInsteadOfNothing()
        {
            yield return StartRun();
            var bootstrap = run.Bootstrap;
            var shell = bootstrap.RunShell;

            Assert.That(bootstrap.ShellPresenter.HandleWorldTap(bootstrap.SlotGrid.WorldRectOf(0).center), Is.True);
            yield return null;
            Assert.That(shell.PlaceholderText, Does.Contain("build sheet for slot 1 is not available yet"));
            shell.SelectTab(ShellTab.Contracts);
            yield return null;
            Assert.That(shell.PlaceholderText, Does.Contain("This panel is not available yet"));
            Assert.That(shell.SelectedSlot, Is.EqualTo(-1), "a tab clears the slot selection");
            Assert.That(bootstrap.SlotGrid.Visuals.Any(visual => visual.Selected), Is.False);
        }

        [UnityTest]
        public IEnumerator TheZoomLimitsComeFromTheSettingsAndNotFromLiterals()
        {
            yield return StartRun();
            var bootstrap = run.Bootstrap;
            var controller = bootstrap.FactoryCamera.GetComponent<FactoryCameraController>();
            var scale = bootstrap.RunShell.Root.panel.scaledPixelsPerPoint;
            var heightPoints = bootstrap.FactoryCamera.pixelHeight / scale;
            var settings = bootstrap.ZoomSettings;
            Assert.That(controller.MinimumOrthographicSize, Is.EqualTo(heightPoints / (2f * settings.maximumPixelsPerCell)).Within(0.001f));
            Assert.That(controller.MaximumOrthographicSize, Is.EqualTo(heightPoints / (2f * settings.minimumPixelsPerCell)).Within(0.001f));

            var originalMinimum = settings.minimumPixelsPerCell;
            var originalMaximum = settings.maximumPixelsPerCell;
            try
            {
                // Feature 004 may change the steps (for example 32 to 128 ppc) by editing the settings only.
                settings.minimumPixelsPerCell = 32f;
                settings.maximumPixelsPerCell = 128f;
                bootstrap.ApplySafeAreaLayout(true);
                Assert.That(controller.MinimumOrthographicSize, Is.EqualTo(heightPoints / 256f).Within(0.001f));
                Assert.That(controller.MaximumOrthographicSize, Is.EqualTo(heightPoints / 64f).Within(0.001f));
            }
            finally
            {
                settings.minimumPixelsPerCell = originalMinimum;
                settings.maximumPixelsPerCell = originalMaximum;
            }
        }

        [UnityTest]
        public IEnumerator TheFitAllViewShowsTwelveReadableSlotsAtTheStartOfARun()
        {
            yield return StartRun();
            for (var i = 0; i < 6; i++)
            {
                yield return null;
            }

            var bootstrap = run.Bootstrap;
            var ppc = bootstrap.CurrentPixelsPerCell;
            Assert.That(ppc, Is.GreaterThanOrEqualTo(bootstrap.ZoomSettings.fitAllFloorPixelsPerCell - 0.01f), "Art Bible 8.2: fit-all is at least 24 ppc");
            var camera = bootstrap.FactoryCamera;
            foreach (var visual in bootstrap.SlotGrid.Visuals)
            {
                var rect = bootstrap.SlotGrid.WorldRectOf(visual.Index);
                var low = camera.WorldToViewportPoint(new Vector3(rect.xMin, rect.yMin, 0f));
                var high = camera.WorldToViewportPoint(new Vector3(rect.xMax, rect.yMax, 0f));
                Assert.That(low.x, Is.GreaterThanOrEqualTo(-0.001f), $"slot {visual.Index + 1} left");
                Assert.That(low.y, Is.GreaterThanOrEqualTo(-0.001f), $"slot {visual.Index + 1} bottom");
                Assert.That(high.x, Is.LessThanOrEqualTo(1.001f), $"slot {visual.Index + 1} right");
                Assert.That(high.y, Is.LessThanOrEqualTo(1.001f), $"slot {visual.Index + 1} top");
            }
        }
#endif
    }
}
