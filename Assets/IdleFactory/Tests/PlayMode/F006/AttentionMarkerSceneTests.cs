#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T557 (Feature 006, FR-025 to FR-031, research R08, R09, contracts/presentation-model.md section 3): the attention markers in the shipped game. The real
    /// authored scene is started against an isolated persistence root; the bootstrap composes <see cref="AttentionMarkerView"/> at runtime, the shell presenter
    /// feeds it from its <see cref="AttentionModel"/> and routes a world tap through <see cref="WorldTapResolver"/>. The content is the shipped Iron content with
    /// the Smelter unlocked and plenty of cash, so one Smelter without a Miner is a real shortage of Iron Ore.
    /// </summary>
    public sealed class AttentionMarkerSceneTests
    {
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

        private static byte[] ContentWithSmelterAndCash()
        {
            var text = Encoding.UTF8.GetString(RunSceneRun.IronBytes());
            var unlocks = new Regex("\"initialUnlocks\"\\s*:\\s*\\[[^\\]]*\\]");
            Assert.That(unlocks.IsMatch(text), Is.True, "the Iron content lists its initial unlocks");
            text = unlocks.Replace(text, "\"initialUnlocks\": [\"iron.ore\", \"machine.miner\", \"machine.smelter\"]", 1);
            var cash = new Regex("\"startingCash\"\\s*:\\s*\\d+");
            Assert.That(cash.IsMatch(text), Is.True);
            text = cash.Replace(text, "\"startingCash\": 1000000000", 1);
            return Encoding.UTF8.GetBytes(text);
        }

        private IEnumerator StartRunWithShortage()
        {
            run = new RunSceneRun(root, clock);
            yield return run.Start(ContentWithSmelterAndCash);
            yield return null;
            yield return null;
            var bootstrap = run.Bootstrap;
            Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
            Assert.That(bootstrap.RunShell.IsReady, Is.True, bootstrap.RunShell.Error);
            Assert.That(bootstrap.MarkerView, Is.Not.Null, "the bootstrap composes the attention marker view");
            Assert.That(bootstrap.ShellPresenter.MarkerView, Is.SameAs(bootstrap.MarkerView), "the presenter's taps resolve against the same view");
            Assert.That(bootstrap.MarkerView.VisibleMarkerCount, Is.Zero, "a factory without machines has nothing short");
            var built = bootstrap.RunCommands.Build(0, "machine.smelter");
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
            // The feed re-reads the rates at most every 0.25 real seconds, so wait for the marker like a player would.
            var deadline = Time.realtimeSinceStartup + 5f;
            while (bootstrap.MarkerView.VisibleMarkerCount == 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AShortageShowsAMarkerOnTheOverlayLayerAndATapOnItsIconSelectsTheAffectedLocation()
        {
            yield return StartRunWithShortage();
            var bootstrap = run.Bootstrap;
            var markers = bootstrap.MarkerView;
            var shell = bootstrap.RunShell;
            var model = bootstrap.ShellPresenter.AttentionModel;
            Assert.That(model.ShortageCount, Is.GreaterThan(0), "a Smelter without a Miner is short of Iron Ore");
            Assert.That(markers.VisibleMarkerCount, Is.EqualTo(1).Or.EqualTo(2), "a shortage marker is shown");
            Assert.That(markers.VisibleMarkerCount, Is.LessThanOrEqualTo(AttentionModel.MaxMarkers));

            var marker = model.GetMarker(0);
            Assert.That(marker.Kind, Is.EqualTo(AttentionKind.Shortage));
            Assert.That(marker.FirstAffected.Value, Is.EqualTo(0), "the Smelter at location 0 is the first affected location");
            var renderer = markers.RendererOf(0);
            Assert.That(renderer.enabled, Is.True);
            Assert.That(renderer.sprite, Is.Not.Null, "the marker draws the resource icon");
            // T561 (C-77, D6): the marker is the missing resource's own icon, never the generic down-arrow of the per-machine Missing-input badge.
            var resourceKey = bootstrap.RunCatalog.Resources[marker.ResourceId].PresentationKey;
            Assert.That(bootstrap.SpriteCatalog.TryGetEntry(resourceKey, out var resourceEntry), Is.True, resourceKey);
            Assert.That(renderer.sprite, Is.SameAs(resourceEntry.Sprite), "the marker shows the icon of " + marker.ResourceId);
            Assert.That(bootstrap.SpriteCatalog.TryGetEntry(SlotGridModel.BadgeMissingInputKey, out var arrowEntry), Is.True);
            Assert.That(renderer.sprite, Is.Not.SameAs(arrowEntry.Sprite), "the marker is not the generic Missing-input arrow");
            Assert.That(renderer.sortingLayerID, Is.EqualTo(SortingLayer.NameToID(SlotGridView.OverlaySortingLayer)), "markers sit on the Overlay layer");
            Assert.That(renderer.transform.position.x, Is.EqualTo(marker.Position.x).Within(1e-3f));
            Assert.That(renderer.transform.position.y, Is.EqualTo(marker.Position.y).Within(1e-3f));

            // The marker lies inside the camera's view (readable on this screen class) and covers no machine, cell or keep-out of the lattice (OD-9b C1).
            var viewport = bootstrap.FactoryCamera.WorldToViewportPoint(marker.Position);
            Assert.That(viewport.x, Is.InRange(0f, 1f), "the marker is on screen horizontally");
            Assert.That(viewport.y, Is.InRange(0f, 1f), "the marker is on screen vertically");
            var layout = bootstrap.SlotGrid.Layout;
            var bounds = renderer.bounds;
            var box = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var id = new BuildingLocationId(i);
                Assert.That(FactoryWorldLayout.Overlaps(box, layout.FootprintOf(id)), Is.False, "the marker covers the machine at " + id);
                Assert.That(FactoryWorldLayout.Overlaps(box, layout.LocationCellRectOf(id)), Is.False, "the marker covers the cell of " + id);
            }

            Assert.That(WorldTapResolver.Resolve(layout, markers, marker.Position, out var viaIcon), Is.EqualTo(WorldTapSource.MarkerIcon));
            Assert.That(viaIcon, Is.EqualTo(marker.FirstAffected));
            // T561 (C-78, marker checklist 7a, 7c): the same through a screen point, the way a finger arrives (camera projection and back), on the icon centre and
            // 6 pixels beside it. The editor Game view has 1 pixel per point, so this is 6 pt inside the 24 pt radius of the 48 pt circle; the device-scale case (a tap 40 px,
            // 15 pt, beside the icon at 2.63 px per point) was checked by hand on the emulator (final-test-evidence.md section 12.2).
            foreach (var offsetPoints in new[] { 0f, 6f })
            {
                var screenPoint = bootstrap.FactoryCamera.WorldToScreenPoint(new Vector3(marker.Position.x, marker.Position.y, 0f)) + new Vector3(offsetPoints, 0f, 0f);
                var world = bootstrap.FactoryCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, -bootstrap.FactoryCamera.transform.position.z));
                var source = WorldTapResolver.Resolve(layout, markers, new Vector2(world.x, world.y), out var viaScreen);
                Assert.That(source, Is.EqualTo(WorldTapSource.MarkerIcon).Or.EqualTo(WorldTapSource.MarkerHalo), "a finger at +" + offsetPoints + " px reaches the marker");
                Assert.That(viaScreen, Is.EqualTo(marker.FirstAffected));
            }

            Assert.That(bootstrap.ShellPresenter.HandleWorldTap(marker.Position), Is.True, "a tap on the marker icon opens its location");
            yield return null;
            Assert.That(shell.SelectedSlot, Is.EqualTo(marker.FirstAffected.Value), "the tap selected the first affected location");
        }

        [UnityTest]
        public IEnumerator AMachineTapStillWinsOverTheMarkerTouchCircleAndTheMarkerStaysUnderReducedMotion()
        {
            yield return StartRunWithShortage();
            var bootstrap = run.Bootstrap;
            var markers = bootstrap.MarkerView;
            var model = bootstrap.ShellPresenter.AttentionModel;
            Assert.That(markers.VisibleMarkerCount, Is.GreaterThan(0));
            var layout = bootstrap.SlotGrid.Layout;

            // A machine tap shape wins over the invisible padding of the marker (T528): the centre of every other location resolves to that machine. C-40 (T560): no
            // location is skipped silently. A point under the icon must resolve to the icon, a point on no machine shape to nothing, and at least one machine must be proved.
            var proved = 0;
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var point = layout.PlateCentreOf(new BuildingLocationId(i));
                if (markers.TryHitIcon(point, out _))
                {
                    Assert.That(WorldTapResolver.Resolve(layout, markers, point, out _), Is.EqualTo(WorldTapSource.MarkerIcon), "the icon itself wins over what lies under it, location " + i);
                    continue;
                }

                var source = WorldTapResolver.Resolve(layout, markers, point, out var target);
                if (source == WorldTapSource.None)
                {
                    Assert.That(layout.TryHit(point, out _), Is.False, "nothing resolves only where no machine shape is, location " + i);
                    continue;
                }

                Assert.That(source, Is.EqualTo(WorldTapSource.Machine), "the machine at " + i + " wins over the touch circle");
                Assert.That(target, Is.EqualTo(new BuildingLocationId(i)), "a tap on the machine at " + i + " selects that machine, never the marker's target");
                proved++;
            }

            Assert.That(proved, Is.GreaterThan(0), "at least one machine shape was proved against the marker touch circle");

            // Reduced motion changes nothing about the markers: they are static sprites and stay shown.
            var spriteBefore = markers.RendererOf(0).sprite;
            bootstrap.DisplayPreferences.TrySave(ReducedMotionPreference.On);
            for (var frame = 0; frame < 30; frame++)
            {
                yield return null;
            }

            Assert.That(bootstrap.AnimationClock.ReducedMotion, Is.True);
            Assert.That(markers.VisibleMarkerCount, Is.GreaterThan(0), "markers stay readable under reduced motion");
            Assert.That(markers.RendererOf(0).sprite, Is.SameAs(spriteBefore));
            Assert.That(markers.RendererOf(0).enabled, Is.True);
            Assert.That(model.MarkerCount, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator ALayoutChangeClearsTheMarkersAtOnceAndTheNextFrameDrawsThemAgain()
        {
            yield return StartRunWithShortage();
            var bootstrap = run.Bootstrap;
            var markers = bootstrap.MarkerView;
            Assert.That(markers.VisibleMarkerCount, Is.GreaterThan(0));

            // A different lattice for the same grid: the view rebinds through LayoutChanged and never keeps a stale position.
            var current = bootstrap.SlotGrid.Layout;
            var other = LatticeLayoutBuilder.ShippedForColumns(current.RowCount == 4 ? 4 : 3);
            Assert.That(ReferenceEquals(other, current), Is.False);
            bootstrap.SlotGrid.SetLayout(other);
            Assert.That(markers.VisibleMarkerCount, Is.Zero, "the layout change cleared the markers");
            Assert.That(Enumerable.Range(0, AttentionModel.MaxMarkers).Any(i => markers.RendererOf(i).enabled), Is.False);

            var deadline = Time.realtimeSinceStartup + 5f;
            while (markers.VisibleMarkerCount == 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(markers.VisibleMarkerCount, Is.GreaterThan(0), "the model recomputed the positions from the new layout and the view draws them");
        }
    }
}
#endif
