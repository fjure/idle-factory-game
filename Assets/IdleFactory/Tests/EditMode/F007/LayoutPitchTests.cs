using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.EditMode.F007
{
    /// <summary>
    /// T610 (F007, research R09 amendment, ARCH-4, ARCH-9, ARCH-10, QA-12; contracts/look-profile.md rules 6 and 7): the lattice pitch is instance data of a
    /// <see cref="FactoryWorldLayout"/>, in world units. At the default pitch the layout reproduces the F006 geometry bit for bit; a denser pitch keeps the
    /// location ids, the unlock order, the 44 pt hit floor and non-overlapping hit areas; the pitch is part of the layout identity so the camera refits; and a
    /// layout whose plates overlap, or whose hit area is too small, fails validation.
    /// </summary>
    public sealed class LayoutPitchTests
    {
        private const int Art = SlotGridLayout.Density;

        // 70 x 46 art px: the plate width is the pitch (neighbouring plates touch), and the rows are 8 px closer than F006. The hit area is the 45 px diamond clipped
        // to the 46 px cell, so the 44 pt floor still holds at the minimum zoom.
        private static readonly LayoutPitch Dense = new LayoutPitch(new Vector2(70f / Art, 46f / Art), string.Empty, LayoutPitch.HitPointsFloor);

        private static readonly LayoutPitch DenseBays = new LayoutPitch(Dense.PitchWorld, LayoutPitch.BaysRule, LayoutPitch.HitPointsFloor);

        private static readonly float[] PixelsPerPointValues = { 1f, 1.68f, 2f, 2.625f, 3f };

        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in owned)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            owned.Clear();
        }

        public static FactoryWorldLayout Shipped(int columns, LayoutPitch? pitch = null)
        {
            var orientation = columns == 4 ? WorldOrientation.Landscape : WorldOrientation.Portrait;
            var cells = new List<Vector2Int>();
            var rows = (LatticeLayoutBuilder.ShippedLocationCount + columns - 1) / columns;
            for (var i = 0; i < LatticeLayoutBuilder.ShippedLocationCount; i++)
            {
                cells.Add(new Vector2Int(i % columns, rows - 1 - i / columns));
            }

            var composition = WorldCompositionSettings.DefaultComposition(orientation);
            return new FactoryWorldLayout("lattice-12x" + columns, cells, orientation, pitch.HasValue ? composition.WithPitch(pitch.Value) : composition);
        }

        public static FactoryWorldLayout FromRows(LayoutPitch pitch, params string[] rows)
        {
            var cells = new List<Vector2Int>();
            for (var rowFromTop = 0; rowFromTop < rows.Length; rowFromTop++)
            {
                for (var column = 0; column < rows[rowFromTop].Length; column++)
                {
                    if (rows[rowFromTop][column] == '#')
                    {
                        cells.Add(new Vector2Int(column, rows.Length - 1 - rowFromTop));
                    }
                }
            }

            return new FactoryWorldLayout("pitch-test", cells, WorldOrientation.Portrait, WorldCompositionSettings.DefaultComposition(WorldOrientation.Portrait).WithPitch(pitch));
        }

        // ---------------------------------------------------------------------------------------------- F006 parity at the default pitch

        [TestCase(3)]
        [TestCase(4)]
        public void TheDefaultPitchReproducesTheF006GeometryBitForBit(int columns)
        {
            var layout = Shipped(columns);
            Assert.That(layout.Pitch, Is.EqualTo(LayoutPitch.F006));
            Assert.That(layout.Pitch.PitchWorld, Is.EqualTo(new Vector2(SlotGridLayout.PitchX, SlotGridLayout.PitchY)), "70 x 54 art px in world units");
            Assert.That(layout.LayoutId, Is.EqualTo("lattice-12x" + columns), "an unchanged identity: the default pitch adds no suffix");
            Assert.That(layout.Bounds, Is.EqualTo(SlotGridLayout.Extent(columns)));
            Assert.That(layout.HitHeightWorld, Is.EqualTo(SlotGridLayout.HitHeightPixels / (float)SlotGridLayout.Density));
            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                var id = new BuildingLocationId(i);
                Assert.That(layout.AnchorOf(id), Is.EqualTo(SlotGridLayout.PivotOf(i, columns)), "anchor " + i);
                Assert.That(layout.PlateCentreOf(id), Is.EqualTo(SlotGridLayout.PlateCentreOf(i, columns)), "plate centre " + i);
                Assert.That(layout.FootprintOf(id), Is.EqualTo(SlotGridLayout.SlotRect(i, columns)), "footprint " + i);
            }

            // The tap lookup: a dense sweep over the lattice (including its margin) agrees with the static F006 hit test.
            for (var x = -1f; x < layout.Bounds.xMax + 1f; x += 0.0625f)
            {
                for (var y = -1f; y < layout.Bounds.yMax + 1f; y += 0.0625f)
                {
                    var point = new Vector2(x, y);
                    var expected = SlotGridLayout.HitTest(point, columns);
                    var hit = layout.TryHit(point, out var id);
                    Assert.That(hit ? id.Value : -1, Is.EqualTo(expected), "the tap at " + point);
                }
            }
        }

        [Test]
        public void TheDeckTilesAndPivotsAtTheDefaultPitchAreTheF006OnesBitForBit()
        {
            var layout = Shipped(3);
            for (var y = -3; y <= 8; y++)
            {
                for (var x = -3; x <= 8; x++)
                {
                    var cell = new Vector2Int(x, y);
                    Assert.That(layout.PivotOfCell(cell), Is.EqualTo(new Vector2(
                        (SlotGridLayout.PlateWidthPixels / 2 + x * SlotGridLayout.PitchXPixels) / (float)Art,
                        (SlotGridLayout.PivotAbovePlateBottomPixels + y * SlotGridLayout.PitchYPixels) / (float)Art)), "pivot of " + cell);
                    Assert.That(layout.PlateCentreAt(cell), Is.EqualTo(FactoryWorldLayout.PlateCentreOfCell(cell)), "the static F006 helper agrees with the instance one");
                    var tile = layout.DeckTileRect(cell);
                    Assert.That(tile.xMin, Is.EqualTo(layout.PivotOfCell(cell).x - SlotGridLayout.PitchXPixels / 2 / (float)Art));
                    Assert.That(tile.yMin, Is.EqualTo(layout.PivotOfCell(cell).y - SlotGridLayout.FloorTilePivotYPixels / (float)Art));
                    Assert.That(tile.size, Is.EqualTo(new Vector2(SlotGridLayout.PitchXPixels / (float)Art, SlotGridLayout.PitchYPixels / (float)Art)));
                    Assert.That(DeckPlanner.CellRect(layout, cell).X, Is.EqualTo(DeckPlanner.CellRect(cell).X), "the deck planner agrees");
                    Assert.That(DeckPlanner.CellRect(layout, cell).Y, Is.EqualTo(DeckPlanner.CellRect(cell).Y));
                }
            }
        }

        [Test]
        public void TheDefaultCompositionAndAnUnsetPitchAreTheF006CompositionWithNoIdentitySuffix()
        {
            var defaults = WorldCompositionSettings.DefaultComposition(WorldOrientation.Portrait);
            Assert.That(defaults.Pitch, Is.EqualTo(LayoutPitch.F006));
            Assert.That(defaults.IdentitySuffix, Is.Empty);
            Assert.That(new LayoutComposition(0.5f, 1, new MarginCellRule(MarginSide.Right, 0), new MarginCellRule(MarginSide.Left, -1)).Pitch, Is.EqualTo(LayoutPitch.F006), "a pitch never set reads as F006");

            var settings = ScriptableObject.CreateInstance<WorldCompositionSettings>();
            owned.Add(settings);
            Assert.That(settings.ComposeFor(WorldOrientation.Portrait), Is.EqualTo(defaults), "the shipped asset values reproduce the default composition");
            Assert.That(settings.layoutPitch.ToPitch(), Is.EqualTo(LayoutPitch.F006));
            Assert.That(settings.Validate(), Is.Empty);

            // A serialized settings asset that predates the field has no layoutPitch data: zero values read as the F006 pitch instead of a 0 x 0 lattice.
            settings.layoutPitch = new LayoutPitchSettings { pitchWorld = Vector2.zero, minHitPoints = 0f };
            Assert.That(settings.layoutPitch.ToPitch(), Is.EqualTo(LayoutPitch.F006));
            Assert.That(settings.ComposeFor(WorldOrientation.Portrait), Is.EqualTo(defaults));
            Assert.That(settings.Validate(), Is.Empty);
        }

        // ---------------------------------------------------------------------------------------------- pitch is identity (ARCH-4)

        [Test]
        public void ThePitchIsPartOfTheCompositionEqualityAndTheLayoutIdentitySoTheCameraRefits()
        {
            var defaults = WorldCompositionSettings.DefaultComposition(WorldOrientation.Portrait);
            var dense = defaults.WithPitch(Dense);
            var bays = defaults.WithPitch(DenseBays);
            var hit48 = defaults.WithPitch(new LayoutPitch(LayoutPitch.F006.PitchWorld, string.Empty, 48f));

            Assert.That(dense.Equals(defaults), Is.False);
            Assert.That(dense.Equals(defaults.WithPitch(Dense)), Is.True);
            Assert.That(dense.Equals(bays), Is.False, "the group rule is part of the identity");
            Assert.That(defaults.Equals(hit48), Is.False, "the hit floor is part of the identity");
            Assert.That(dense.IdentitySuffix, Does.StartWith("+c"));
            Assert.That(dense.IdentitySuffix, Is.Not.EqualTo(bays.IdentitySuffix));
            Assert.That(dense.IdentitySuffix, Is.Not.EqualTo(hit48.IdentitySuffix));
            Assert.That(dense.IdentitySuffix, Is.EqualTo(defaults.WithPitch(Dense).IdentitySuffix), "stable");
            Assert.That(defaults.WithPitch(LayoutPitch.F006).IdentitySuffix, Is.Empty);

            var f006 = Shipped(3);
            var denseLayout = Shipped(3, Dense);
            Assert.That(denseLayout.LayoutId, Is.Not.EqualTo(f006.LayoutId), "the camera model keys its refit on the layout id");
            Assert.That(denseLayout.LayoutId, Does.StartWith("lattice-12x3+c"));
            Assert.That(Shipped(3, Dense).LayoutId, Is.EqualTo(denseLayout.LayoutId));
            Assert.That(Shipped(3, DenseBays).LayoutId, Is.Not.EqualTo(denseLayout.LayoutId));
        }

        [Test]
        public void ADenserPitchRefitsTheCameraBecauseTheLayoutIdAndTheExtentChange()
        {
            var settings = new ArtScaleSettings(Art, 24, 6);
            var model = new CameraScaleModel(settings);
            var rect = new Rect(0f, 0f, 1080f, 2400f);
            var f006 = Shipped(3);
            var dense = Shipped(3, Dense);

            model.Fit(ScaleLayout.WithView(1080, 2400, 2.625f, rect, f006));
            var before = model.Layout.LayoutId;
            Assert.That(model.OnLayoutChanged(ScaleLayout.WithView(1080, 2400, 2.625f, rect, dense)) || model.Layout.LayoutId != before, Is.True);
            Assert.That(model.Layout.LayoutId, Is.EqualTo(dense.LayoutId));
            Assert.That(dense.Bounds.height, Is.LessThan(f006.Bounds.height), "four rows at 46 px are shorter than at 54 px");
            Assert.That(dense.Bounds.height * Art, Is.EqualTo(3 * 46f + 15f + 39f).Within(1e-3f));
            Assert.That(dense.Bounds.width, Is.EqualTo(f006.Bounds.width), "the plate width is the pitch: the columns do not move");
        }

        [Test]
        public void TheShippedBuilderFollowsTheActivePitchAndGoesBackToTheSameF006LayoutWhenItIsCleared()
        {
            var before = LatticeLayoutBuilder.ActivePitch;
            try
            {
                LatticeLayoutBuilder.UsePitch(null);
                var f006 = LatticeLayoutBuilder.Shipped(WorldOrientation.Portrait);
                Assert.That(f006.Pitch, Is.EqualTo(LayoutPitch.F006));
                Assert.That(f006.LayoutId, Is.EqualTo("lattice-12x3"));

                LatticeLayoutBuilder.UsePitch(LayoutPitch.F006);
                Assert.That(LatticeLayoutBuilder.Shipped(WorldOrientation.Portrait), Is.SameAs(f006), "the F006 pitch is the default composition: the cached layout is reused");

                LatticeLayoutBuilder.UsePitch(Dense);
                var dense = LatticeLayoutBuilder.Shipped(WorldOrientation.Portrait);
                Assert.That(dense, Is.Not.SameAs(f006));
                Assert.That(dense.Pitch, Is.EqualTo(Dense));
                Assert.That(dense.LayoutId, Is.Not.EqualTo(f006.LayoutId));
                Assert.That(dense.Validate(), Is.Empty);
                Assert.That(LatticeLayoutBuilder.Shipped(WorldOrientation.Portrait), Is.SameAs(dense), "cached while the pitch stays");
                Assert.That(LatticeLayoutBuilder.Shipped(WorldOrientation.Landscape).Pitch, Is.EqualTo(Dense), "both orientations follow it");
                Assert.That(new LatticeLayoutBuilder(12, WorldOrientation.Portrait, null, DenseBays).Build().Pitch, Is.EqualTo(DenseBays), "a builder can carry its own pitch");

                LatticeLayoutBuilder.UsePitch(null);
                var again = LatticeLayoutBuilder.Shipped(WorldOrientation.Portrait);
                Assert.That(again.LayoutId, Is.EqualTo(f006.LayoutId));
                Assert.That(again.Bounds, Is.EqualTo(f006.Bounds));
                for (var i = 0; i < again.LocationCount; i++)
                {
                    Assert.That(again.AnchorOf(new BuildingLocationId(i)), Is.EqualTo(f006.AnchorOf(new BuildingLocationId(i))));
                }
            }
            finally
            {
                LatticeLayoutBuilder.UsePitch(before);
            }
        }

        // ---------------------------------------------------------------------------------------------- a denser pitch keeps ids, order, 44 pt, no overlap

        [TestCase(3)]
        [TestCase(4)]
        public void ADenserPitchKeepsTheIdsTheUnlockOrderAndTheCellOfEveryLocation(int columns)
        {
            var f006 = Shipped(columns);
            var dense = Shipped(columns, Dense);
            Assert.That(dense.LocationCount, Is.EqualTo(f006.LocationCount).And.EqualTo(LatticeLayoutBuilder.ShippedLocationCount), "ids and count come from content, not from the pitch (ARCH-9)");
            Assert.That(dense.ColumnCount, Is.EqualTo(f006.ColumnCount));
            Assert.That(dense.RowCount, Is.EqualTo(f006.RowCount));
            for (var i = 0; i < dense.LocationCount; i++)
            {
                var id = new BuildingLocationId(i);
                Assert.That(dense.CellOf(id), Is.EqualTo(f006.CellOf(id)), "location " + i + " keeps its lattice cell");
                Assert.That(dense.SortKeyOf(id), Is.EqualTo(f006.SortKeyOf(id)));
                Assert.That(dense.AnchorOf(id).x, Is.EqualTo(f006.AnchorOf(id).x), "the columns do not move");
                for (var unlocked = 0; unlocked <= dense.LocationCount; unlocked++)
                {
                    Assert.That(dense.IsUnlocked(id, unlocked), Is.EqualTo(f006.IsUnlocked(id, unlocked)), $"unlock order: location {i} with {unlocked} unlocked");
                }
            }

            // Reading order: location 0 is the top-left one, every next one is further right or one row down.
            for (var i = 1; i < dense.LocationCount; i++)
            {
                var previous = dense.AnchorOf(new BuildingLocationId(i - 1));
                var current = dense.AnchorOf(new BuildingLocationId(i));
                Assert.That(current.y < previous.y || (Mathf.Approximately(current.y, previous.y) && current.x > previous.x), Is.True, "reading order at " + i);
            }
        }

        [TestCase(3)]
        [TestCase(4)]
        public void ADenserPitchIsAValidLayoutWithDisjointHitAreasAndNoOverlap(int columns)
        {
            var dense = Shipped(columns, Dense);
            Assert.That(dense.Validate(), Is.Empty, "the dense lattice passes the same validation as F006");
            Assert.That(dense.Pitch.Problems(), Is.Empty);

            for (var i = 0; i < dense.LocationCount; i++)
            {
                var a = new BuildingLocationId(i);
                for (var j = i + 1; j < dense.LocationCount; j++)
                {
                    var b = new BuildingLocationId(j);
                    Assert.That(FactoryWorldLayout.Overlaps(dense.FootprintOf(a), dense.FootprintOf(b)), Is.False, $"footprints {i} and {j}");
                    Assert.That(FactoryWorldLayout.Overlaps(dense.LocationCellRectOf(a), dense.LocationCellRectOf(b)), Is.False, $"hit cells {i} and {j}");
                }
            }

            // No point is inside the hit shape of two locations, and the closed-form lookup agrees with the shapes.
            // The sweep is offset by a small irrational-ish amount so that no point lies exactly on a diamond edge: there the two formulas may differ in the last bit.
            for (var x = -0.5f + 0.01237f; x < dense.Bounds.xMax + 0.5f; x += 0.0625f)
            {
                for (var y = -0.5f + 0.00931f; y < dense.Bounds.yMax + 0.5f; y += 0.0625f)
                {
                    var point = new Vector2(x, y);
                    var containing = 0;
                    var owner = -1;
                    for (var i = 0; i < dense.LocationCount; i++)
                    {
                        if (dense.HitShapeOf(new BuildingLocationId(i)).Contains(point))
                        {
                            containing++;
                            owner = i;
                        }
                    }

                    Assert.That(containing, Is.LessThanOrEqualTo(1), "the hit shapes overlap at " + point);
                    Assert.That(dense.TryHit(point, out var hit) ? hit.Value : -1, Is.EqualTo(owner), "TryHit equals the shapes at " + point);
                }
            }
        }

        [Test]
        public void ADenserPitchKeepsTheHitAreaAtLeast44PointsAtEveryAllowedScaleAndPixelDensity()
        {
            foreach (var pitch in new[] { LayoutPitch.F006, Dense })
            {
                var layout = Shipped(3, pitch);
                foreach (var density in new[] { 24, 48 })
                {
                    foreach (var ppp in PixelsPerPointValues)
                    {
                        var settings = new ArtScaleSettings(density, 24, 6);
                        var nMin = CameraScaleModel.MinScale(settings, ppp);
                        for (var n = nMin; n <= settings.MaxScale; n++)
                        {
                            var points = CameraScaleModel.FootprintPoints(layout, n, density, ppp);
                            Assert.That(points, Is.GreaterThanOrEqualTo(pitch.MinHitPoints), $"pitch {pitch.PitchWorld} PPU {density} ppp {ppp} n {n}: {points:0.0} pt");
                        }
                    }
                }
            }
        }

        [Test]
        public void TheLayoutAwareHitSizeEqualsTheFormerFormulaAtTheDefaultPitchAndFollowsTheCellHeightWhenItClipsTheDiamond()
        {
            var f006 = Shipped(3);
            foreach (var ppp in PixelsPerPointValues)
            {
                for (var n = 1; n <= 6; n++)
                {
                    Assert.That(CameraScaleModel.FootprintPoints(f006, n, 24, ppp), Is.EqualTo(CameraScaleModel.FootprintPoints(n, 24, ppp)), $"n {n} ppp {ppp}: bit for bit");
                }
            }

            // A cell lower than the 45 px diamond clips it: the measured hit height is the cell height, not the constant 45.
            var tight = Shipped(3, new LayoutPitch(new Vector2(70f / Art, 44f / Art), string.Empty, LayoutPitch.HitPointsFloor));
            Assert.That(tight.HitHeightWorld, Is.EqualTo(44f / Art).Within(1e-6f));
            Assert.That(CameraScaleModel.FootprintPoints(tight, 2, 24, 2f), Is.LessThan(CameraScaleModel.FootprintPoints(f006, 2, 24, 2f)));
        }

        // ---------------------------------------------------------------------------------------------- QA-12: negative cases

        [Test]
        public void ADeliberatelyOverlappingLayoutFailsValidation()
        {
            // 60 x 30 art px: narrower than the 70 px plate and lower than the 35 px plate, so neighbouring plates overlap.
            var overlapping = Shipped(3, new LayoutPitch(new Vector2(60f / Art, 30f / Art), string.Empty, LayoutPitch.HitPointsFloor));
            var problems = overlapping.Validate();
            Assert.That(problems, Is.Not.Empty);
            Assert.That(problems.Any(p => p.Contains("the footprints overlap")), Is.True, string.Join("; ", problems));
            Assert.That(problems.Any(p => p.StartsWith("pitch:")), Is.True, "and its hit area cannot reach 44 pt: " + string.Join("; ", problems));

            // The same layout at the dense pitch is valid, so the failure comes from the overlap and not from the harness.
            Assert.That(Shipped(3, Dense).Validate(), Is.Empty);

            // Only the rows overlap.
            var rowsOnly = Shipped(3, new LayoutPitch(new Vector2(70f / Art, 34f / Art), string.Empty, LayoutPitch.HitPointsFloor));
            Assert.That(rowsOnly.Validate().Any(p => p.Contains("the footprints overlap")), Is.True);
        }

        [Test]
        public void AHitFloorBelow44OrAPitchThatCannotReach44PointsFailsValidation()
        {
            Assert.That(new LayoutPitch(Dense.PitchWorld, string.Empty, 40f).Problems(), Is.Not.Empty, "minHitPoints under 44");
            Assert.That(new LayoutPitch(new Vector2(70f / Art, 44f / Art), string.Empty, 44f).Problems(), Is.Not.Empty, "44 px rows reach only 43.6 pt at the minimum zoom");
            Assert.That(new LayoutPitch(new Vector2(70f / Art, 45f / Art), string.Empty, 44f).Problems(), Is.Empty, "45 px rows still do");
            Assert.That(new LayoutPitch(new Vector2(0f, 2f), string.Empty, 44f).Problems(), Is.Not.Empty, "a zero pitch");
            Assert.That(new LayoutPitch(new Vector2(float.NaN, 2f), string.Empty, 44f).Problems(), Is.Not.Empty, "a NaN pitch");
            Assert.That(new LayoutPitch(Dense.PitchWorld, "slab", 44f).Problems(), Is.Not.Empty, "an unknown group rule");

            var settings = ScriptableObject.CreateInstance<WorldCompositionSettings>();
            owned.Add(settings);
            settings.layoutPitch = new LayoutPitchSettings { pitchWorld = Dense.PitchWorld, minHitPoints = 40f };
            Assert.That(settings.Validate().Any(p => p.StartsWith("layoutPitch:")), Is.True, "the settings asset rejects it");
            settings.layoutPitch = new LayoutPitchSettings { pitchWorld = Dense.PitchWorld, minHitPoints = 44f };
            Assert.That(settings.Validate(), Is.Empty);
        }

        // ---------------------------------------------------------------------------------------------- layout independence per pitch (rule 6)

        public static IEnumerable<TestCaseData> IndependenceCases()
        {
            foreach (var (name, rows) in new[]
            {
                ("six", new[] { "###", "###" }),
                ("twelve", new[] { "###", "###", "###", "###" }),
                ("twenty", new[] { "####", "####", "####", "####", "####" }),
                ("l-shape", new[] { "#...", "#...", "#...", "####" }),
            })
            {
                yield return new TestCaseData(name, rows, false).SetName("Layout_" + name + "_dense");
                yield return new TestCaseData(name, rows, true).SetName("Layout_" + name + "_dense_bays");
            }
        }

        [TestCaseSource(nameof(IndependenceCases))]
        public void The6_12_20AndLLayoutsAreValidAndTappableAtTheDensePitch(string name, string[] rows, bool bays)
        {
            var layout = FromRows(bays ? DenseBays : Dense, rows);
            Assert.That(layout.Validate(), Is.Empty, name);
            var expectedCount = rows.Sum(row => row.Count(c => c == '#'));
            Assert.That(layout.LocationCount, Is.EqualTo(expectedCount), name + ": the count comes from the rows, not from a constant");

            for (var i = 0; i < layout.LocationCount; i++)
            {
                var id = new BuildingLocationId(i);
                var centre = layout.PlateCentreOf(id);
                Assert.That(layout.TryHit(centre, out var hit) && hit == id, Is.True, name + ": tap on location " + i);
                foreach (var apex in new[] { new Vector2(34f, 0f), new Vector2(-34f, 0f), new Vector2(0f, 17f), new Vector2(0f, -17f) })
                {
                    Assert.That(layout.TryHit(centre + apex / Art, out hit) && hit == id, Is.True, $"{name}: location {i} apex {apex}");
                }
            }

            // An empty lattice cell and the margin tap nothing.
            Assert.That(layout.TryHit(layout.DepotAnchor, out _), Is.False, name + ": the depot cell");
            Assert.That(layout.TryHit(layout.DepositAnchor, out _), Is.False, name + ": the deposit cell");
            if (name == "l-shape")
            {
                Assert.That(layout.TryHit(layout.PlateCentreAt(new Vector2Int(2, 3)), out _), Is.False, "an empty lattice cell inside the bounds");
            }
        }

        // ---------------------------------------------------------------------------------------------- grouping (WD-4) and cue ownership (UX-4)

        [Test]
        public void TheBaysGroupFollowsTheLatticeCellOnlyAndNeverTheLocationIdOrderOrTheMachineChain()
        {
            var reading = FromRows(DenseBays, "###", "###", "###", "###");
            var groups = new Dictionary<Vector2Int, int>();
            for (var i = 0; i < reading.LocationCount; i++)
            {
                groups[reading.CellOf(new BuildingLocationId(i))] = reading.GroupOf(new BuildingLocationId(i));
            }

            Assert.That(groups.Values.Distinct().Count(), Is.EqualTo(8), "12 locations in 3 columns make 4 rows of 2 groups: a bay of two and a single");
            for (var row = 0; row < 4; row++)
            {
                Assert.That(groups[new Vector2Int(0, row)], Is.EqualTo(groups[new Vector2Int(1, row)]), "a bay: the first two locations of a row share a slab");
                Assert.That(groups[new Vector2Int(2, row)], Is.Not.EqualTo(groups[new Vector2Int(1, row)]), "the third one is its own group");
            }

            // The same cells with the ids shuffled (a different unlock order) give the same group per cell.
            var cells = Enumerable.Range(0, reading.LocationCount).Select(i => reading.CellOf(new BuildingLocationId(i))).ToList();
            var shuffled = cells.OrderBy(c => (c.x * 7 + c.y * 13) % 5).ThenBy(c => c.x).ThenBy(c => c.y).ToList();
            var other = new FactoryWorldLayout("shuffled", shuffled, WorldOrientation.Portrait, WorldCompositionSettings.DefaultComposition(WorldOrientation.Portrait).WithPitch(DenseBays));
            for (var i = 0; i < other.LocationCount; i++)
            {
                var cell = other.CellOf(new BuildingLocationId(i));
                Assert.That(other.GroupOf(new BuildingLocationId(i)), Is.EqualTo(groups[cell]), "the group of " + cell + " does not depend on the id order");
            }

            // Without a group rule every location is its own group.
            var plain = FromRows(Dense, "###", "###");
            Assert.That(Enumerable.Range(0, plain.LocationCount).Select(i => plain.GroupOf(new BuildingLocationId(i))).Distinct().Count(), Is.EqualTo(plain.LocationCount));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EveryCueAnchorMapsToItsOwnLocationAndNoTwoCueBoxesOverlapAtTheDensePitch(bool bays)
        {
            foreach (var columns in new[] { 3, 4 })
            {
                var layout = Shipped(columns, bays ? DenseBays : Dense);
                for (var i = 0; i < layout.LocationCount; i++)
                {
                    var id = new BuildingLocationId(i);
                    var badgeAnchor = layout.AnchorOf(id) + StateCueGeometry.BadgeAnchorOffset;
                    Assert.That(layout.TryCellOwner(badgeAnchor, out var owner), Is.True, "the badge anchor of " + i + " lies in a lattice cell");
                    Assert.That(owner, Is.EqualTo(id), "the badge anchor belongs to its own machine, also inside a bay (" + columns + " columns)");
                    for (var j = i + 1; j < layout.LocationCount; j++)
                    {
                        var other = new BuildingLocationId(j);
                        for (var k = 0; k < FactoryWorldLayout.KeepOutCount; k++)
                        {
                            for (var m = 0; m < FactoryWorldLayout.KeepOutCount; m++)
                            {
                                Assert.That(FactoryWorldLayout.Overlaps(layout.KeepOutOf(id, k), layout.KeepOutOf(other, m)), Is.False, $"keep-out {k} of {i} and keep-out {m} of {j}");
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void TheDeckTilesOfTheDensePitchTileTheLatticeWithoutGapOrOverlap()
        {
            var dense = Shipped(3, Dense);
            for (var y = -2; y < 6; y++)
            {
                for (var x = -2; x < 6; x++)
                {
                    var tile = dense.DeckTileRect(new Vector2Int(x, y));
                    var right = dense.DeckTileRect(new Vector2Int(x + 1, y));
                    var up = dense.DeckTileRect(new Vector2Int(x, y + 1));
                    Assert.That(right.xMin, Is.EqualTo(tile.xMax).Within(1e-4f), "horizontal neighbours share an edge");
                    Assert.That(up.yMin, Is.EqualTo(tile.yMax).Within(1e-4f), "vertical neighbours share an edge");
                    Assert.That(tile.height * Art, Is.EqualTo(46f).Within(1e-3f));
                    Assert.That(dense.PivotOfCell(new Vector2Int(x, y)).y - tile.center.y, Is.EqualTo(0f).Within(1e-4f), "the pivot is the tile centre");
                }
            }
        }
    }
}
