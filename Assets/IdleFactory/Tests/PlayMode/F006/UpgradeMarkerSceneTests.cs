#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T563 (Feature 006, FR-027, contracts/presentation-model.md section 2): the upgrade-available marker in the world. The grid cases draw synthetic
    /// <see cref="SlotVisual"/>s over a flat-colour catalog (the code under test is the slot renderer: the one marker renderer per location, its position, order
    /// and the zoom and boost rules). The scene cases start the real authored scene, so they prove that the shipped catalog holds the approved upgrade icon, that
    /// the presenter feeds the grid from the run, and that the marker follows the cash without a structural change.
    /// </summary>
    public sealed class UpgradeMarkerSceneTests
    {
        private readonly List<Object> owned = new List<Object>();
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
            foreach (var item in owned)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            owned.Clear();
        }

        // ---------------------------------------------------------------------------------------------------
        // The slot renderer
        // ---------------------------------------------------------------------------------------------------

        private SlotGridView NewGrid(out PresentationSpriteCatalog catalog, params string[] keys)
        {
            var rig = new DeckTestRig();
            catalog = rig.IconCatalog(keys);
            owned.AddRange(rig.Owned);
            var host = new GameObject("upgrade-marker-grid");
            owned.Add(host);
            var grid = host.AddComponent<SlotGridView>();
            grid.Configure(catalog);
            grid.SetLayout(LatticeLayoutBuilder.Shipped(WorldOrientation.Portrait));
            grid.SetProjectedPixelsPerCell(48f);
            return grid;
        }

        private static SlotVisual Occupied(int index, bool marker, bool boosted = false) =>
            new SlotVisual(index, SlotCondition.Occupied, SlotGridModel.SlotOccupiedKey, null, SlotGridModel.BadgeRunningKey, "RUN", boosted, false, false, 0, "Slot " + (index + 1), marker, 10);

        private static SlotVisual Plain(int index) =>
            new SlotVisual(index, SlotCondition.Empty, SlotGridModel.SlotEmptyKey, null, null, null, false, false, false, 0, "Slot " + (index + 1));

        private static SlotVisual[] Visuals(int count, System.Func<int, SlotVisual> make)
        {
            var visuals = new SlotVisual[count];
            for (var i = 0; i < count; i++)
            {
                visuals[i] = make(i);
            }

            return visuals;
        }

        [Test]
        public void TheMarkerDrawsOnTheOverlayLayerAtItsOffsetOverTheMachineAndNeverRed()
        {
            var grid = NewGrid(out var catalog, SlotGridModel.UpgradeMarkerKey);
            var count = grid.Layout.LocationCount;
            grid.Render(Visuals(count, i => i == 0 ? Occupied(0, true) : i == 1 ? Occupied(1, false) : Plain(i)));
            var marker = grid.RendererOf(0, SlotGridView.SlotRendererRole.UpgradeMarker);
            Assert.That(marker.enabled, Is.True, "an affordable upgrade on a machine without a problem shows the marker");
            Assert.That(catalog.TryGetEntry(SlotGridModel.UpgradeMarkerKey, out var entry), Is.True);
            Assert.That(marker.sprite, Is.SameAs(entry.Sprite), "the marker is the registry's upgrade icon");
            Assert.That(marker.sortingLayerID, Is.EqualTo(SortingLayer.NameToID(SlotGridView.OverlaySortingLayer)), "it sits on the Overlay layer, over the machine art");
            Assert.That(marker.transform.localScale, Is.EqualTo(Vector3.one), "drawn at scale 1: no resampling shimmer (C-75)");
            Assert.That(marker.color, Is.EqualTo(Color.white), "no tint, and never red");
            var pivot = grid.FootprintPivotOf(0);
            Assert.That(marker.transform.position.x, Is.EqualTo(pivot.x + StateCueGeometry.UpgradeMarkerOffset.x).Within(1e-3f));
            Assert.That(marker.transform.position.y, Is.EqualTo(pivot.y + StateCueGeometry.UpgradeMarkerOffset.y).Within(1e-3f));
            Assert.That(grid.RendererOf(1, SlotGridView.SlotRendererRole.UpgradeMarker).enabled, Is.False, "an unaffordable upgrade draws nothing");
            Assert.That(grid.RendererOf(2, SlotGridView.SlotRendererRole.UpgradeMarker).enabled, Is.False, "an empty location draws nothing");
            var badge = grid.RendererOf(0, SlotGridView.SlotRendererRole.Badge);
            Assert.That(marker.sortingOrder, Is.LessThan(badge.sortingOrder), "the marker never draws over a badge (lower visual priority)");
        }

        [Test]
        public void AnItemBoostKeepsItsMarkerBoxAndHidesTheUpgradeMarker()
        {
            var grid = NewGrid(out _, SlotGridModel.UpgradeMarkerKey, SlotGridModel.MarkerBoostedKey);
            var count = grid.Layout.LocationCount;
            grid.Render(Visuals(count, i => i == 0 ? Occupied(0, true, true) : Plain(i)));
            Assert.That(grid.RendererOf(0, SlotGridView.SlotRendererRole.Boosted).enabled, Is.True, "the Item marker stays");
            Assert.That(grid.RendererOf(0, SlotGridView.SlotRendererRole.UpgradeMarker).enabled, Is.False, "at most two markers per location (ART-2); the upgrade yields");
        }

        [Test]
        public void TheMarkerFollowsTheMarkerZoomRuleAndReappearsWhenTheZoomIsBack()
        {
            var grid = NewGrid(out _, SlotGridModel.UpgradeMarkerKey);
            var count = grid.Layout.LocationCount;
            grid.Render(Visuals(count, i => i == 0 ? Occupied(0, true) : Plain(i)));
            var marker = grid.RendererOf(0, SlotGridView.SlotRendererRole.UpgradeMarker);
            Assert.That(marker.enabled, Is.True);
            grid.SetProjectedPixelsPerCell(20f);
            Assert.That(marker.enabled, Is.False, "tight markers are hidden below 24 px per cell, like the Item markers");
            grid.SetProjectedPixelsPerCell(48f);
            Assert.That(marker.enabled, Is.True);
        }

        [Test]
        public void ReRenderingAnUnchangedVisualDoesNotChangeTheRendererSet()
        {
            var grid = NewGrid(out _, SlotGridModel.UpgradeMarkerKey);
            var count = grid.Layout.LocationCount;
            var visuals = Visuals(count, i => i % 2 == 0 ? Occupied(i, true) : Plain(i));
            grid.Render(visuals);
            var before = grid.ActiveRendererCount;
            grid.Render(visuals);
            Assert.That(grid.ActiveRendererCount, Is.EqualTo(before));
            foreach (var renderer in grid.RenderersOf(0))
            {
                Assert.That(renderer, Is.Not.Null);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // The shipped scene
        // ---------------------------------------------------------------------------------------------------

        private static byte[] ContentWithSmelter()
        {
            var text = Encoding.UTF8.GetString(RunSceneRun.IronBytes());
            var unlocks = new Regex("\"initialUnlocks\"\\s*:\\s*\\[[^\\]]*\\]");
            Assert.That(unlocks.IsMatch(text), Is.True);
            text = unlocks.Replace(text, "\"initialUnlocks\": [\"iron.ore\", \"machine.miner\", \"machine.smelter\"]", 1);
            var cash = new Regex("\"startingCash\"\\s*:\\s*\\d+");
            Assert.That(cash.IsMatch(text), Is.True);
            text = cash.Replace(text, "\"startingCash\": 1000000000", 1);
            return Encoding.UTF8.GetBytes(text);
        }

        private IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheShippedSceneShowsTheMarkerWhenTheCashPaysForTheUpgradeAndHidesItWhenTheCashIsSpent()
        {
            run = new RunSceneRun(root, clock);
            yield return run.Start(RunSceneRun.IronBytes);
            yield return Frames(2);
            var bootstrap = run.Bootstrap;
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            Assert.That(bootstrap.RunShell.IsReady, Is.True, bootstrap.RunShell.Error);
            var grid = bootstrap.SlotGrid;
            Assert.That(bootstrap.SpriteCatalog.TryGetEntry(SlotGridModel.UpgradeMarkerKey, out var entry), Is.True, "the shipped catalog holds the approved upgrade icon");
            Assert.That(entry.Sprite.rect.width, Is.EqualTo(24f));

            var built = bootstrap.RunCommands.Build(0, "machine.miner");
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
            yield return Frames(3);
            var marker = grid.RendererOf(0, SlotGridView.SlotRendererRole.UpgradeMarker);
            Assert.That(marker.enabled, Is.False, "the cash left after the build does not pay for the first upgrade");

            // Cash alone changes (no structural change, no machine state change): the presenter notices that the upgrade became affordable.
            Assert.That(bootstrap.RunSession.State.TryAddCash(1_000_000), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok));
            yield return Frames(3);
            Assert.That(marker.enabled, Is.True, "an affordable upgrade shows the marker in the shipped scene");
            Assert.That(marker.sprite, Is.SameAs(entry.Sprite));
            Assert.That(marker.sortingLayerID, Is.EqualTo(SortingLayer.NameToID(SlotGridView.OverlaySortingLayer)));
            var pivot = grid.FootprintPivotOf(0);
            Assert.That(marker.transform.position.x, Is.EqualTo(pivot.x + StateCueGeometry.UpgradeMarkerOffset.x).Within(1e-3f));
            Assert.That(bootstrap.ShellPresenter.Visuals[0].UpgradeMarker, Is.True);

            // The marker is inside the camera's view.
            var viewport = bootstrap.FactoryCamera.WorldToViewportPoint(marker.transform.position);
            Assert.That(viewport.x, Is.InRange(0f, 1f));
            Assert.That(viewport.y, Is.InRange(0f, 1f));

            // The marker is per location: an empty one has none.
            Assert.That(grid.RendererOf(1, SlotGridView.SlotRendererRole.UpgradeMarker).enabled, Is.False, "an empty location has no marker");
        }

        [UnityTest]
        public IEnumerator TheShippedSceneHidesTheMarkerUnderAProblemBadge()
        {
            run = new RunSceneRun(root, clock);
            yield return run.Start(ContentWithSmelter);
            yield return Frames(2);
            var bootstrap = run.Bootstrap;
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            var grid = bootstrap.SlotGrid;

            // A Smelter with no Miner is short of Iron Ore: a problem badge, and the cash (10^9) pays for its upgrade.
            var built = bootstrap.RunCommands.Build(0, "machine.smelter");
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
            yield return Frames(3);
            var visual = bootstrap.ShellPresenter.Visuals[0];
            Assert.That(visual.BadgeKey, Is.EqualTo(SlotGridModel.BadgeMissingInputKey), "the Smelter is short of ore");
            Assert.That(visual.UpgradeCost, Is.LessThan(SlotGridModel.NoUpgradeCost), "an upgrade exists");
            Assert.That(visual.UpgradeMarker, Is.False, "a problem badge hides the marker");
            Assert.That(grid.RendererOf(0, SlotGridView.SlotRendererRole.UpgradeMarker).enabled, Is.False);
            Assert.That(grid.RendererOf(0, SlotGridView.SlotRendererRole.Badge).enabled, Is.True, "the problem badge itself is drawn");

            // A Miner next to it runs with an affordable upgrade: the marker shows there, with no badge sprite beside it.
            var miner = bootstrap.RunCommands.Build(1, "machine.miner");
            Assert.That(miner.IsSuccess, Is.True, miner.ReasonCode);
            yield return Frames(3);
            Assert.That(grid.RendererOf(1, SlotGridView.SlotRendererRole.UpgradeMarker).enabled, Is.True, "a running machine with an affordable upgrade shows the marker");
            Assert.That(grid.RendererOf(1, SlotGridView.SlotRendererRole.Badge).enabled, Is.False, "a running machine draws no badge (R8)");
            Assert.That(grid.RendererOf(1, SlotGridView.SlotRendererRole.UpgradeMarker).enabled, Is.EqualTo(bootstrap.ShellPresenter.Visuals[1].UpgradeMarker));
        }
    }
}
#endif
