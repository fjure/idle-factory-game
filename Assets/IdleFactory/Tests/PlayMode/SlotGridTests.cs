using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine.TestTools.Utils;
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
        public void ExtentsMatchTheLatticeAtD24()
        {
            // T456: pitch 70 x 54 art px, plates 70 x 35, the content spans the plates plus the 39 px badge band above the pivot:
            // 3 columns: 2 * 70 + 70 = 210 px wide, 3 * 54 + 15 + 39 = 216 px high; 4 columns: 280 x 162 px.
            Assert.That(SlotGridLayout.Extent(3).size * 24f, Is.EqualTo(new Vector2(210f, 216f)).Using(new Vector2EqualityComparer(0.01f)));
            Assert.That(SlotGridLayout.Extent(4).size * 24f, Is.EqualTo(new Vector2(280f, 162f)).Using(new Vector2EqualityComparer(0.01f)));
            Assert.That(SlotGridLayout.Extent(3).min, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void SlotsFollowReadingOrderFromTheTopLeftOnTheLattice([Values(3, 4)] int columns)
        {
            var extent = SlotGridLayout.Extent(columns);
            Assert.That(SlotGridLayout.PivotOf(0, columns).x * 24f, Is.EqualTo(35f).Within(1e-3f), "slot 1 is in the left column");
            Assert.That(SlotGridLayout.PivotOf(0, columns).y * 24f + 39f, Is.EqualTo(extent.yMax * 24f).Within(1e-3f), "slot 1 is in the top row");
            for (var i = 1; i < SlotGridLayout.SlotCount; i++)
            {
                var previous = SlotGridLayout.PivotOf(i - 1, columns) * 24f;
                var current = SlotGridLayout.PivotOf(i, columns) * 24f;
                if (i % columns != 0)
                {
                    Assert.That(current.x, Is.EqualTo(previous.x + 70f).Within(1e-3f), $"slot {i + 1} follows slot {i} to the right");
                    Assert.That(current.y, Is.EqualTo(previous.y).Within(1e-3f));
                }
                else
                {
                    Assert.That(current.x, Is.EqualTo(35f).Within(1e-3f), $"slot {i + 1} starts a new row");
                    Assert.That(current.y, Is.EqualTo(previous.y - 54f).Within(1e-3f), $"slot {i + 1} is one row lower");
                }
            }

            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                // P3: every lattice position is a whole number of art pixels.
                var pivot = SlotGridLayout.PivotOf(i, columns) * 24f;
                Assert.That(pivot.x, Is.EqualTo(Mathf.Round(pivot.x)).Within(1e-3f), $"slot {i + 1} x is whole");
                Assert.That(pivot.y, Is.EqualTo(Mathf.Round(pivot.y)).Within(1e-3f), $"slot {i + 1} y is whole");
                var rect = SlotGridLayout.SlotRect(i, columns);
                Assert.That(rect.size * 24f, Is.EqualTo(new Vector2(70f, 35f)).Using(new Vector2EqualityComparer(0.01f)));
                Assert.That(extent.Contains(new Vector2(rect.xMin + 0.001f, rect.yMin + 0.001f)) && extent.Contains(new Vector2(rect.xMax - 0.001f, rect.yMax - 0.001f)), Is.True,
                    $"slot {i + 1} lies inside the extent");
                Assert.That(rect.center.y - SlotGridLayout.PivotOf(i, columns).y, Is.EqualTo(2.5f / 24f).Within(1e-5f), "the plate centre is 2.5 art px above the pivot");
            }
        }

        // The plate diamond of slot i: |dx| / 35 + |dy| / 17.5 <= 1 around its centre, in art pixels.
        private static bool InPlate(Vector2 world, int slot, int columns, float growPixels = 0f)
        {
            var offset = (world - SlotGridLayout.PlateCentreOf(slot, columns)) * 24f;
            return Mathf.Abs(offset.x) / (35f + growPixels * 2f) + Mathf.Abs(offset.y) / (17.5f + growPixels) <= 1f;
        }

        [Test]
        public void PlatesNeverOverlap([Values(3, 4)] int columns)
        {
            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                for (var j = i + 1; j < SlotGridLayout.SlotCount; j++)
                {
                    var offset = (SlotGridLayout.PlateCentreOf(j, columns) - SlotGridLayout.PlateCentreOf(i, columns)) * 24f;
                    // Two 70 x 35 diamonds have disjoint interiors when |dx| / 70 + |dy| / 35 >= 1.
                    Assert.That(Mathf.Abs(offset.x) / 70f + Mathf.Abs(offset.y) / 35f, Is.GreaterThanOrEqualTo(1f - 1e-4f), $"plates {i + 1} and {j + 1}");
                }
            }
        }

        [Test]
        public void HitTestIsFootprintOnlyOnTheDiamondAndNeverOverlaps([Values(3, 4)] int columns)
        {
            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                var centre = SlotGridLayout.PlateCentreOf(i, columns);
                Assert.That(SlotGridLayout.HitTest(centre, columns), Is.EqualTo(i), $"slot {i + 1} centre");
                // Every plate-diamond point hits its own slot: the four apexes (just inside) and the plate bounding-box centre.
                foreach (var apex in new[] { new Vector2(34f, 0f), new Vector2(-34f, 0f), new Vector2(0f, 17f), new Vector2(0f, -17f) })
                {
                    Assert.That(SlotGridLayout.HitTest(centre + apex / 24f, columns), Is.EqualTo(i), $"slot {i + 1} apex {apex}");
                }

                // Outside the 90 x 45 hit diamond: the bounding-box corner of the plate is a miss for this slot.
                var corner = centre + new Vector2(34f, 17f) / 24f;
                Assert.That(SlotGridLayout.HitTest(corner, columns), Is.Not.EqualTo(i), $"slot {i + 1} bounding-box corner is outside the footprint");
                // Above the footprint (machine overhang and badge band) never captures: 30 px above the pivot is outside every hit diamond but
                // inside the next row's cell, so it is a miss or the slot behind, never this slot.
                Assert.That(SlotGridLayout.HitTest(SlotGridLayout.PivotOf(i, columns) + new Vector2(0f, 30f / 24f), columns), Is.Not.EqualTo(i));
            }

            // Sample a fine grid over the whole lattice: a point resolves to at most one slot, and a resolved point lies in that slot's hit diamond.
            var extent = SlotGridLayout.Extent(columns);
            for (var x = extent.xMin - 0.5f; x < extent.xMax + 0.5f; x += 1f / 24f)
            {
                for (var y = extent.yMin - 0.5f; y < extent.yMax + 0.5f; y += 1f / 24f)
                {
                    var hit = SlotGridLayout.HitTest(new Vector2(x, y), columns);
                    if (hit >= 0)
                    {
                        Assert.That(InPlate(new Vector2(x, y), hit, columns, 5f), Is.True, $"hit {hit + 1} at {x},{y} lies on its footprint (plate grown by 5 px)");
                    }
                }
            }

            Assert.That(SlotGridLayout.HitTest(new Vector2(-1f, 1f), columns), Is.EqualTo(-1));
            Assert.That(SlotGridLayout.HitTest(new Vector2(SlotGridLayout.PlateCentreOf(0, columns).x, 100f), columns), Is.EqualTo(-1));
            Assert.That(SlotGridLayout.HitTest(new Vector2(100f, SlotGridLayout.PlateCentreOf(0, columns).y), columns), Is.EqualTo(-1));
        }

        [Test]
        public void TheTapTargetIsAtLeast44ArtPixelsHighAndTheRowsSortFrontLater([Values(3, 4)] int columns)
        {
            Assert.That(SlotGridLayout.HitHeightPixels, Is.GreaterThanOrEqualTo(44));
            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                Assert.That(SlotGridLayout.RowOf(i, columns), Is.EqualTo(i / columns));
                if (i >= columns)
                {
                    Assert.That(SlotGridLayout.RowOf(i, columns), Is.GreaterThan(SlotGridLayout.RowOf(i - columns, columns)));
                    Assert.That(SlotGridLayout.PivotOf(i, columns).y, Is.LessThan(SlotGridLayout.PivotOf(i - columns, columns).y), "a front row is lower on the screen");
                }
            }
        }

        [TestCase(360f, 536f, 3, 36.9f)]
        [TestCase(390f, 740f, 3, 40.0f)]
        [TestCase(640f, 256f, 4, 33.0f)]
        [TestCase(844f, 286f, 4, 36.9f)]
        [TestCase(408f, 976f, 3, 41.8f)]
        [TestCase(664f, 720f, 4, 52.4f)]
        [TestCase(920f, 672f, 4, 72.6f)]
        [TestCase(1560f, 1032f, 4, 123.2f)]
        public void FitAllReproducesTheArtBibleTableAndStaysAboveTheReadableFloor(float width, float height, int columns, float expected)
        {
            var ppc = SlotGridLayout.FitAllPixelsPerCell(width, height, columns);
            Assert.That(ppc, Is.EqualTo(expected).Within(0.06f));
            var zoom = new RunZoomSettings();
            Assert.That(ppc, Is.GreaterThanOrEqualTo(zoom.readableCellPoints), "the readable floor, now expressed through n_min");

            // T427: fit-all is a whole-number scale. At 1 px/pt (the table's points) the grid fits at n = floor(ppc / D), at least n_min = 1.
            var framing = new RunCameraFraming(zoom);
            var area = new Rect(0f, 0f, width, height);
            var fit = framing.FitAll(area, columns, 1f);
            Assert.That(fit, Is.EqualTo(Mathf.Clamp(Mathf.FloorToInt(ppc / zoom.artPixelsPerCell + 1e-4f), 1, zoom.maxScale)));
            Assert.That(fit, Is.GreaterThanOrEqualTo(framing.ConfigureLimits(1f).Min), "n_fit >= n_min for the table's screens");
        }

        [Test]
        public void ZoomLimitsAreSettingsWithTheArtBibleDefaults()
        {
            // T427: the continuous 16 to 64 limits and the 24 floor are replaced by the Art Bible v1.0 inputs of the integer scale.
            var settings = new RunZoomSettings();
            Assert.That(settings.artPixelsPerCell, Is.EqualTo(24));
            Assert.That(settings.readableCellPoints, Is.EqualTo(24));
            Assert.That(settings.maxScale, Is.EqualTo(6));
            var scale = settings.ToScaleSettings();
            Assert.That(scale.ArtPixelsPerCell, Is.EqualTo(24));
            Assert.That(scale.ReadableCellPoints, Is.EqualTo(24));
            Assert.That(scale.MaxScale, Is.EqualTo(6));
            Assert.That(CameraScaleModel.MinScale(scale, 1.68f), Is.EqualTo(2));
            Assert.That(CameraScaleModel.MinScale(scale, 2.0f), Is.EqualTo(2));
            Assert.That(CameraScaleModel.MinScale(scale, 2.0046f), Is.EqualTo(2), "the 1 % tolerance of the owner decision 2026-10-28");
            Assert.That(CameraScaleModel.MinScale(scale, 2.625f), Is.EqualTo(3));
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

            // T427: the projected size is n * D / pxPerPoint, and n_min keeps it at the readable cell within the 1 % tolerance, so the
            // markers stay visible at every allowed scale (the 1280x720 phone at 2.0046 px/pt projects 23.94 points at n_min).
            var zoom = new RunZoomSettings();
            Assert.That(SlotGridView.MarkersVisibleAt(23.94f), Is.True, "within the tolerance of the readable floor");
            Assert.That(SlotGridView.MarkersVisibleAt(23.5f), Is.False);
            foreach (var pixelsPerPoint in new[] { 1f, 1.68f, 2f, 2.0046f, 2.625f, 3f })
            {
                var nMin = CameraScaleModel.MinScale(zoom.ToScaleSettings(), pixelsPerPoint);
                for (var n = nMin; n <= zoom.maxScale; n++)
                {
                    var ppc = n * zoom.artPixelsPerCell / pixelsPerPoint;
                    Assert.That(SlotGridView.MarkersVisibleAt(ppc), Is.True, $"markers at n {n}, {pixelsPerPoint} px/pt ({ppc:0.0} pt per cell)");
                    Assert.That(SlotGridView.BadgeScale(ppc) * 72f / 64f * ppc, Is.GreaterThanOrEqualTo(27f - 0.01f), $"badge width at n {n}, {pixelsPerPoint} px/pt");
                }
            }
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
        // Floor layer (T472, Art Bible v1.0.3 section 3.5): one 70 x 54 tile per lattice cell over the visible field
        // ---------------------------------------------------------------------------------------------------

        private readonly List<UnityEngine.Object> floorOwned = new List<UnityEngine.Object>();

        [TearDown]
        public void DestroyFloorObjects()
        {
            foreach (var item in floorOwned)
            {
                if (item != null)
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }

            floorOwned.Clear();
        }

        private SlotGridView NewFloorView(bool withFloorKey)
        {
            var texture = new Texture2D(70, 54, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, name = "floor-test" };
            var pixels = new Color32[70 * 54];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(42, 47, 58, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 70, 54), new Vector2(0.5f, 0.5f), 24, 0, SpriteMeshType.FullRect);
            sprite.name = "floor-test";
            floorOwned.Add(texture);
            floorOwned.Add(sprite);

            var catalog = new PresentationSpriteCatalog();
            catalog.Configure(new[] { new PresentationSpriteEntry(withFloorKey ? PresentationSpriteCatalog.FloorTileKey : "F004-TEST-OTHER", sprite) });
            Assert.That(catalog.IsValid, Is.True, catalog.Error);
            var host = new GameObject("floor-grid");
            floorOwned.Add(host);
            var view = host.AddComponent<SlotGridView>();
            view.Configure(catalog);
            return view;
        }

        // The visible world rectangle of a camera of the given pixel size at integer scale n (n device pixels per art pixel), centred on a point.
        private static Rect VisibleRect(int pixelWidth, int pixelHeight, int n, Vector2 centre)
        {
            var size = new Vector2(pixelWidth, pixelHeight) / (n * 24f);
            return new Rect(centre - size * 0.5f, size);
        }

        private static void UpdateFloorTo(SlotGridView view, Rect rect) => view.UpdateFloor(rect.xMin, rect.yMin, rect.xMax, rect.yMax);

        // The drawn tiles as lattice cells, from their positions in art pixels: the tile pivot is (35 + 70 c, 15 + 54 r).
        private static List<Vector2Int> DrawnCells(SlotGridView view)
        {
            var cells = new List<Vector2Int>();
            for (var i = 0; i < view.FloorTileCount; i++)
            {
                var tile = view.FloorTiles[i];
                Assert.That(tile.enabled, Is.True, $"tile {i} of the drawn block is enabled");
                var art = (Vector2)tile.transform.localPosition * 24f;
                Assert.That(art.x, Is.EqualTo(Mathf.Round(art.x)).Within(1e-3f), "P3: the tile x is a whole number of art pixels");
                Assert.That(art.y, Is.EqualTo(Mathf.Round(art.y)).Within(1e-3f), "P3: the tile y is a whole number of art pixels");
                var column = (Mathf.Round(art.x) - 35f) / 70f;
                var row = (Mathf.Round(art.y) - 15f) / 54f;
                Assert.That(column, Is.EqualTo(Mathf.Round(column)).Within(1e-4f), "the tile sits on the lattice column phase (pivot x = 35 + 70 c)");
                Assert.That(row, Is.EqualTo(Mathf.Round(row)).Within(1e-4f), "the tile sits on the lattice row phase (pivot y = 15 + 54 r)");
                cells.Add(new Vector2Int(Mathf.RoundToInt(column), Mathf.RoundToInt(row)));
            }

            return cells;
        }

        [Test]
        public void TheFloorPivotIsTheSlotPivotOnTheSameLattice([Values(3, 4)] int columns)
        {
            var rows = SlotGridLayout.RowsFor(columns);
            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                var column = i % columns;
                var row = rows - 1 - i / columns;
                Assert.That(SlotGridLayout.FloorPivotOf(column, row), Is.EqualTo(SlotGridLayout.PivotOf(i, columns)).Using(new Vector2EqualityComparer(1e-5f)),
                    $"slot {i + 1} stands on the floor tile of its cell: every slot sees the same floor phase");
            }

            Assert.That(SlotGridLayout.FloorTileWidthPixels, Is.EqualTo(SlotGridLayout.PitchXPixels));
            Assert.That(SlotGridLayout.FloorTileHeightPixels, Is.EqualTo(SlotGridLayout.PitchYPixels));
            Assert.That(SlotGridLayout.FloorTilePivotYPixels, Is.EqualTo(27), "the imported tile pivot is (35, 27)");
        }

        // 2532x1170 and 1170x2532: the 844x390 and 390x844 phones at x3; 2048x1536 a tablet; 1920x1080 a desktop window.
        [TestCase(2532, 1170)]
        [TestCase(1170, 2532)]
        [TestCase(2048, 1536)]
        [TestCase(1920, 1080)]
        public void TheFloorCoversTheVisibleFieldWithoutSeamsOrGapsAtEveryIntegerScale(int pixelWidth, int pixelHeight)
        {
            for (var n = 1; n <= 6; n++)
            {
                AssertFloorCoversAt(pixelWidth, pixelHeight, n);
            }
        }

        private void AssertFloorCoversAt(int pixelWidth, int pixelHeight, int n)
        {
            var view = NewFloorView(true);
            Assert.That(view.HasFloor, Is.True);
            foreach (var centre in new[] { new Vector2(140f / 24f, 100f / 24f), new Vector2(0.37f, -1.113f), new Vector2(-6.21f, 9.44f) })
            {
                var rect = VisibleRect(pixelWidth, pixelHeight, n, centre);
                UpdateFloorTo(view, rect);
                var cells = DrawnCells(view);

                // Tile count: the block is exactly the cells the rectangle (plus the margin) touches.
                var margin = SlotGridLayout.FloorMarginPixels;
                var left = Mathf.FloorToInt((rect.xMin * 24f - margin) / 70f);
                var right = Mathf.CeilToInt((rect.xMax * 24f + margin) / 70f) - 1;
                var bottom = Mathf.FloorToInt((rect.yMin * 24f - margin + 12f) / 54f);
                var top = Mathf.CeilToInt((rect.yMax * 24f + margin + 12f) / 54f) - 1;
                var expected = (right - left + 1) * (top - bottom + 1);
                Assert.That(view.FloorTileCount, Is.EqualTo(expected), $"{pixelWidth}x{pixelHeight} n={n} centre={centre}: one tile per touched cell");
                Assert.That(cells.Count, Is.EqualTo(expected));
                Assert.That(cells.Distinct().Count(), Is.EqualTo(expected), "no cell is drawn twice");
                Assert.That(cells.Min(c => c.x), Is.EqualTo(left));
                Assert.That(cells.Max(c => c.x), Is.EqualTo(right));
                Assert.That(cells.Min(c => c.y), Is.EqualTo(bottom));
                Assert.That(cells.Max(c => c.y), Is.EqualTo(top));
                Assert.That(view.FloorCells.Count, Is.EqualTo(expected));

                // No seam: neighbouring tiles abut at exactly 70 and 54 art pixels (cells are consecutive integers), and the sprite is the full
                // 70 x 54 tile, so the blocks share their edges and nothing is left uncovered between them.
                var set = new HashSet<Vector2Int>(cells);
                foreach (var cell in cells)
                {
                    if (cell.x < right) Assert.That(set.Contains(cell + Vector2Int.right), Is.True, "no gap to the right of " + cell);
                    if (cell.y < top) Assert.That(set.Contains(cell + Vector2Int.up), Is.True, "no gap above " + cell);
                }

                // Coverage: a dense sample of the visible rectangle, corners included, lies inside a drawn tile.
                for (var sx = 0; sx <= 8; sx++)
                {
                    for (var sy = 0; sy <= 8; sy++)
                    {
                        var point = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, sx / 8f), Mathf.Lerp(rect.yMin, rect.yMax, sy / 8f)) * 24f;
                        var column = Mathf.FloorToInt(point.x / 70f);
                        var row = Mathf.FloorToInt((point.y + 12f) / 54f);
                        Assert.That(set.Contains(new Vector2Int(column, row)), Is.True, $"the visible point {point} is covered by tile ({column}, {row})");
                    }
                }
            }
        }

        [Test]
        public void ThePhaseIsFixedToTheLatticeAndNeverFollowsTheCamera()
        {
            var view = NewFloorView(true);
            var rect = VisibleRect(2532, 1170, 4, new Vector2(5f, 4f));
            UpdateFloorTo(view, rect);
            var firstCells = DrawnCells(view);
            var before = new Dictionary<Vector2Int, Vector2>();
            for (var i = 0; i < firstCells.Count; i++)
            {
                before[firstCells[i]] = view.FloorTiles[i].transform.localPosition;
            }

            // A pan by a single art pixel keeps every shared cell on exactly the same position.
            UpdateFloorTo(view, new Rect(rect.position + new Vector2(1f / 24f, 1f / 24f), rect.size));
            var after = DrawnCells(view);
            foreach (var cell in after.Where(before.ContainsKey))
            {
                var position = (Vector2)view.FloorTiles[after.IndexOf(cell)].transform.localPosition;
                Assert.That(position, Is.EqualTo(before[cell]).Using(new Vector2EqualityComparer(1e-6f)), $"cell {cell} does not move with the camera");
                Assert.That(position, Is.EqualTo(SlotGridLayout.FloorPivotOf(cell.x, cell.y)).Using(new Vector2EqualityComparer(1e-6f)));
            }
        }

        [Test]
        public void TheFloorSortsBelowEveryPlateAndMachine()
        {
            var view = NewFloorView(true);
            UpdateFloorTo(view, VisibleRect(2532, 1170, 6, new Vector2(5f, 4f)));
            Assert.That(view.FloorTileCount, Is.GreaterThan(0));
            Assert.That(SlotGridView.FloorOrder, Is.LessThan(0).And.LessThan(SlotGridView.PlateOrder));
            for (var i = 0; i < view.FloorTileCount; i++)
            {
                var tile = view.FloorTiles[i];
                Assert.That(tile.sortingOrder, Is.EqualTo(SlotGridView.FloorOrder));
                Assert.That(tile.sortingLayerID, Is.EqualTo(0), "the Default layer, below the slot groups (order = row >= 0) and the Overlay layer");
                Assert.That(tile.sprite.name, Is.EqualTo("floor-test"));
                Assert.That(tile.sprite.pixelsPerUnit, Is.EqualTo(24f));
                Assert.That(tile.sprite.pivot, Is.EqualTo(new Vector2(35f, 27f)), "the tile pivot (35, 27) is the footprint centre");
            }

            Assert.That(view.ActiveRendererCount, Is.EqualTo(0), "the floor is not a slot renderer: the slot renderer counts stay as they are");
        }

        [Test]
        public void AMissingFloorKeyDrawsNoFloorAndRaisesNoError()
        {
            var view = NewFloorView(false);
            Assert.That(view.HasFloor, Is.False);
            UpdateFloorTo(view, VisibleRect(2532, 1170, 4, new Vector2(5f, 4f)));
            view.UpdateFloor();
            Assert.That(view.FloorTileCount, Is.EqualTo(0));
            Assert.That(view.FloorTiles.Count, Is.EqualTo(0), "nothing is created without the key");
            Assert.That(view.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(12 * 8), "only the 12 slots' pooled renderers exist");
        }

        [Test]
        public void ThePoolGrowsOnlyWithTheVisibleAreaAndIsReusedAfterwards()
        {
            var view = NewFloorView(true);
            var centre = new Vector2(5f, 4f);
            UpdateFloorTo(view, VisibleRect(2532, 1170, 6, centre));
            var small = view.FloorTileCount;
            var firstTile = view.FloorTiles[0];
            UpdateFloorTo(view, VisibleRect(2532, 1170, 2, centre));
            var large = view.FloorTileCount;
            Assert.That(large, Is.GreaterThan(small), "zooming out needs more tiles");
            Assert.That(view.FloorTiles.Count, Is.EqualTo(large));
            UpdateFloorTo(view, VisibleRect(2532, 1170, 6, centre));
            Assert.That(view.FloorTileCount, Is.EqualTo(small));
            Assert.That(view.FloorTiles.Count, Is.EqualTo(large), "the pool keeps its size after zooming in");
            Assert.That(view.FloorTiles.Count(tile => tile.enabled), Is.EqualTo(small), "the surplus tiles are disabled, not destroyed");
            Assert.That(view.FloorTiles[0], Is.SameAs(firstTile));
        }

        [Test]
        public void ADegenerateVisibleAreaDrawsNoFloor()
        {
            var view = NewFloorView(true);
            UpdateFloorTo(view, VisibleRect(2532, 1170, 4, Vector2.zero));
            Assert.That(view.FloorTileCount, Is.GreaterThan(0));
            view.UpdateFloor(float.NaN, 0f, 1f, 1f);
            Assert.That(view.FloorTileCount, Is.EqualTo(0), "not a number");
            view.UpdateFloor(0f, 0f, 0f, 5f);
            Assert.That(view.FloorTileCount, Is.EqualTo(0), "an empty rectangle");
            view.UpdateFloor(-1e6f, -1e6f, 1e6f, 1e6f);
            Assert.That(view.FloorTileCount, Is.EqualTo(0), "more than the tile cap");
            Assert.That(SlotGridLayout.FloorCellsFor(0f, 0f, 1f, 1f).Count, Is.GreaterThan(0));
        }

        [Test]
        public void TheFloorFollowsTheOrthographicCameraThatItIsGiven()
        {
            var view = NewFloorView(true);
            var texture = new RenderTexture(2532, 1170, 24, RenderTextureFormat.ARGB32) { name = "floor-camera-test" };
            texture.Create();
            floorOwned.Add(texture);
            var cameraObject = new GameObject("floor-camera");
            floorOwned.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.targetTexture = texture;
            camera.enabled = false;
            foreach (var n in new[] { 3, 4, 5, 6 })
            {
                camera.orthographicSize = 1170f / (2f * n * 24f);
                cameraObject.transform.position = new Vector3(140f / 24f, 100f / 24f, -10f);
                view.FloorCamera = camera;
                view.UpdateFloor();
                var rect = VisibleRect(2532, 1170, n, new Vector2(140f / 24f, 100f / 24f));
                var expected = SlotGridLayout.FloorCellsFor(rect.xMin, rect.yMin, rect.xMax, rect.yMax);
                Assert.That(expected.IsEmpty, Is.False);
                Assert.That(view.FloorTileCount, Is.EqualTo(expected.Count), $"n={n}: the camera path draws the cells of its view");
                Assert.That(view.FloorCells, Is.EqualTo(expected));
            }

            camera.orthographic = false;
            view.UpdateFloor();
            Assert.That(view.FloorTileCount, Is.EqualTo(0), "a perspective camera has no floor field");
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
            // T440 recorded re-baseline (old: 12, "12 plates and nothing else"): the F004 lock labels are registered and required by the
            // shipped scene, so every locked slot of a fresh factory draws its digit label above its plate. Nothing else is drawn.
            var lockedSlots = grid.Visuals.Count(visual => visual.Condition == SlotCondition.Locked);
            Assert.That(grid.ActiveRendererCount, Is.EqualTo(12 + lockedSlots), "12 plates and one lock label per locked slot on a fresh factory");
            Assert.That(grid.RenderersOf(0).Count(renderer => renderer.enabled), Is.EqualTo(1), "an empty slot draws its plate only");

            // Build a Miner through the application contract: the world follows the read model, never the other way round.
            var built = bootstrap.RunCommands.Build(0, MinerId);
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
            yield return null;
            yield return null;
            Assert.That(grid.Visuals[0].Condition, Is.EqualTo(SlotCondition.Occupied));
            Assert.That(grid.Visuals[0].MachineKey, Is.EqualTo("F003-MACHINE-MINER"));
            Assert.That(grid.Visuals[0].BadgeKey, Is.Not.Null);
            // T429: a running machine draws no badge (the running cue is intentionally empty); every other state draws exactly one.
            // T440 recorded re-baseline (old: 12 + 1 + badgeDrawn): the F004 accent layers are registered, so a running machine draws its
            // accent in place of a badge. Every state therefore draws exactly one more layer next to the machine: the accent when running,
            // the badge otherwise. The lock labels of the locked slots stay drawn.
            var stateLayer = 1;
            Assert.That(grid.ActiveRendererCount, Is.EqualTo(12 + lockedSlots + 1 + stateLayer),
                "plates, lock labels, one machine and either its running accent or its state badge");
            Assert.That(grid.ActiveAccentCount, Is.EqualTo(grid.Visuals[0].BadgeKey == SlotGridModel.BadgeRunningKey ? 1 : 0));
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

            // Since T194 the bootstrap mounts the real build sheet and machine panel.
            Assert.That(shell.Panels.TryGet(ShellPanelIds.Build, out var build), Is.True);
            Assert.That(shell.Panels.TryGet(ShellPanelIds.Machine, out var machine), Is.True);
            Assert.That(presenter.HandleWorldTap(grid.WorldRectOf(2).center), Is.True);
            yield return null;
            Assert.That(shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Build), "an empty slot opens the build sheet");
            Assert.That(shell.SelectedSlot, Is.EqualTo(2));
            Assert.That(build.Root.parent, Is.SameAs(shell.PanelContent), "the panel was mounted without editing the shell");
            Assert.That(grid.Visuals[2].Selected, Is.True);

            var built = bootstrap.RunCommands.Build(2, MinerId);
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
            yield return null;
            yield return null;
            Assert.That(shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Machine), "after the build the open sheet becomes the machine panel");
            Assert.That(machine.Root.parent, Is.SameAs(shell.PanelContent));

            shell.CloseSheet();
            Assert.That(presenter.HandleWorldTap(grid.WorldRectOf(2).center), Is.True);
            Assert.That(shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Machine), "an occupied slot opens the machine panel");

            Assert.That(presenter.HandleWorldTap(new Vector2(-50f, -50f)), Is.False, "a tap beside the grid hits nothing");
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

            // The bootstrap mounts every panel since T194: a panel that could not be built (a missing asset) is the unregistered case.
            RunPanelCompositionTests.ClearAsset(bootstrap, "buildSheetTree");
            RunPanelCompositionTests.ClearAsset(bootstrap, "prestigeTree");
            Assert.That(bootstrap.RecomposeRunPanels(), Is.True);
            Assert.That(bootstrap.ShellPresenter.HandleWorldTap(bootstrap.SlotGrid.WorldRectOf(0).center), Is.True);
            yield return null;
            Assert.That(shell.PlaceholderText, Does.Contain("build sheet for slot 1 is not available yet"));
            shell.SelectTab(ShellTab.Prestige);
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
            var settings = bootstrap.ZoomSettings;
            for (var i = 0; i < 4 && !controller.HasLayout; i++)
            {
                yield return null;
            }

            Assert.That(controller.HasLayout, Is.True);
            Assert.That(controller.Model.LowerBound, Is.EqualTo(CameraScaleModel.MinScale(settings.ToScaleSettings(), scale)));
            Assert.That(controller.Model.UpperBound, Is.EqualTo(settings.maxScale));

            var originalReadable = settings.readableCellPoints;
            var originalMax = settings.maxScale;
            try
            {
                // Feature 004 may change the range by editing the settings only: a larger readable cell raises n_min, a smaller maxScale lowers the top.
                settings.readableCellPoints = 48;
                settings.maxScale = 4;
                bootstrap.ApplySafeAreaLayout(true);
                for (var i = 0; i < 6; i++)
                {
                    yield return null;
                }

                Assert.That(controller.Model.Settings.ReadableCellPoints, Is.EqualTo(48));
                Assert.That(controller.Model.UpperBound, Is.EqualTo(4));
                var expectedLower = controller.Model.BelowFloor ? controller.Model.FitScale : CameraScaleModel.MinScale(settings.ToScaleSettings(), scale);
                Assert.That(controller.Model.LowerBound, Is.EqualTo(expectedLower), "n_min from the new readable cell, or the runtime degrade when it cannot fit");
                Assert.That(controller.Scale, Is.InRange(controller.Model.LowerBound, 4), "the camera fitted again inside the new range");
            }
            finally
            {
                settings.readableCellPoints = originalReadable;
                settings.maxScale = originalMax;
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
            var settings = bootstrap.ZoomSettings;
            var pixelsPerPoint = bootstrap.RunShell.Root.panel.scaledPixelsPerPoint;
            var controller = bootstrap.FactoryCamera.GetComponent<FactoryCameraController>();
            Assert.That(controller.Scale, Is.GreaterThanOrEqualTo(controller.Model.LowerBound), "fit-all is at least n_min");
            var minimumCell = settings.readableCellPoints - CameraScaleModel.MinScaleTolerance * settings.artPixelsPerCell / pixelsPerPoint;
            Assert.That(ppc, Is.GreaterThanOrEqualTo(minimumCell - 0.01f), "Art Bible 8.2: fit-all is at least the readable cell (within the 1 % tolerance)");
            Assert.That(bootstrap.FactoryCamera.orthographicSize, Is.EqualTo(bootstrap.FactoryCamera.pixelHeight / (2f * controller.Scale * settings.artPixelsPerCell)).Within(1e-4f));
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

        // ---------------------------------------------------------------------------------------------------
        // T192 (F-1 lock notice, F-2 empty hint, F-7 failed start-up composition)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AFreshRunShowsTheBuildAMinerHintOnAPhoneAndTheHintGoesWithTheFirstMachine()
        {
            yield return StartRun();
            var bootstrap = run.Bootstrap;
            var shell = bootstrap.RunShell;
            yield return null;
            yield return null;
            Assert.That(shell.IsEmptyHintShown, Is.EqualTo(!shell.IsSidePanelLayout && !shell.IsSheetOpen), "a phone with the sheet closed shows the hint of the 4 empty slots");
            Assert.That(bootstrap.SlotGrid.Visuals.Count(visual => visual.Condition == SlotCondition.Empty), Is.EqualTo(bootstrap.RunSession.State.SlotCount));

            var built = bootstrap.RunCommands.Build(0, MinerId);
            Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
            yield return null;
            yield return null;
            Assert.That(shell.IsEmptyHintShown, Is.False, "the hint ends with the first machine");
        }

        [UnityTest]
        public IEnumerator TheLockedSlotNoticeClearsWhenTheFactoryLevelUnlocksTheSlot()
        {
            yield return StartRun();
            var bootstrap = run.Bootstrap;
            var shell = bootstrap.RunShell;
            var locked = bootstrap.SlotGrid.Visuals.First(visual => visual.Condition == SlotCondition.Locked);
            Assert.That(bootstrap.ShellPresenter.HandleWorldTap(bootstrap.SlotGrid.WorldRectOf(locked.Index).center), Is.False);
            Assert.That(shell.NoticeText, Does.Contain("unlocks at factory level"));
            Assert.That(shell.NoticeKind, Is.EqualTo(ShellNoticeKind.Lock));
            yield return null;
            Assert.That(shell.NoticeText, Is.Not.Empty, "the slot is still locked, so the notice is still true");

            Assert.That(bootstrap.RunSession.State.TryAddCash(1_000_000L), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok));
            var upgraded = bootstrap.RunCommands.UpgradeFactory();
            Assert.That(upgraded.IsSuccess, Is.True, upgraded.ReasonCode);
            for (var i = 0; i < 4 && bootstrap.SlotGrid.Visuals[locked.Index].Condition == SlotCondition.Locked; i++)
            {
                yield return null;
            }

            if (bootstrap.SlotGrid.Visuals[locked.Index].Condition != SlotCondition.Locked)
            {
                Assert.That(shell.NoticeText, Is.Empty, "the slot unlocked, so the notice stopped being true");
            }
            else
            {
                Assert.That(shell.NoticeText, Is.Not.Empty, "a slot that the upgrade left locked keeps its notice");
            }
        }

        [UnityTest]
        public IEnumerator AFailedShellConfigureLeavesNothingBehindAndTheStartUpRetryComposesAgain()
        {
            yield return StartRun();
            var bootstrap = run.Bootstrap;
            const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(CoreGameFoundationBootstrap);
            var shellField = type.GetField("runShell", Flags);
            var gridField = type.GetField("slotGrid", Flags);
            var presenterField = type.GetField("shellPresenter", Flags);
            var treeField = type.GetField("runShellTree", Flags);
            var errorField = type.GetField("runShellError", Flags);
            var compose = type.GetMethod("ComposeRunShell", Flags);
            Assert.That(shellField != null && gridField != null && presenterField != null && treeField != null && errorField != null && compose != null, Is.True);

            // Start from a bootstrap that has not composed a shell yet.
            var goodTree = treeField.GetValue(bootstrap);
            var oldGrid = bootstrap.SlotGrid;
            UnityEngine.Object.DestroyImmediate(bootstrap.RunShell);
            if (oldGrid != null)
            {
                UnityEngine.Object.DestroyImmediate(oldGrid.gameObject);
            }

            shellField.SetValue(bootstrap, null);
            gridField.SetValue(bootstrap, null);
            presenterField.SetValue(bootstrap, null);

            var badTree = ScriptableObject.CreateInstance<UnityEngine.UIElements.VisualTreeAsset>();
            try
            {
                treeField.SetValue(bootstrap, badTree);
                Assert.That((bool)compose.Invoke(bootstrap, null), Is.False, "an unusable shell tree fails the composition");
                Assert.That(shellField.GetValue(bootstrap), Is.Null, "no half-composed shell is kept");
                Assert.That(gridField.GetValue(bootstrap), Is.Null, "no slot grid is kept");
                Assert.That(presenterField.GetValue(bootstrap), Is.Null, "no presenter is kept");
                Assert.That((string)errorField.GetValue(bootstrap), Is.Not.Empty, "the failure keeps its reason for the start-up report");

                // The retry (a recovery re-enters start-up) composes a complete shell.
                treeField.SetValue(bootstrap, goodTree);
                Assert.That((bool)compose.Invoke(bootstrap, null), Is.True, "the retry composes the shell");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(badTree);
            }

            yield return null;
            yield return null;
            var shell = bootstrap.RunShell;
            Assert.That(shell, Is.Not.Null);
            Assert.That(shell.IsReady, Is.True, shell.Error);
            Assert.That(bootstrap.SlotGrid, Is.Not.Null);
            Assert.That(bootstrap.ShellPresenter, Is.Not.Null);
            Assert.That(bootstrap.GetComponents<RunShellDocument>().Count(component => component != null), Is.EqualTo(1), "the failed attempt's component is gone");
            Assert.That(errorField.GetValue(bootstrap), Is.EqualTo(string.Empty));
        }
#endif
    }
}
