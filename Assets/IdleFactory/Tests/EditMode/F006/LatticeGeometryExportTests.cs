using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T505 (Feature 006, research R03): exports today's <see cref="SlotGridLayout"/> geometry and the <see cref="CameraScaleModel"/> n_min
    /// and n_fit per screen class and orientation to <c>tools/art-pipeline/f006/lattice-geometry.json</c>, the one shared source of the
    /// offline mockup compositor (<c>python -m art_pipeline mockup</c>), so the mockups cannot drift from Unity. The export is
    /// deterministic (stable ordering, invariant culture, fixed number formats, LF line ends, no timestamp) and the tests assert on the
    /// structure and on the lattice rules, not only on the write.
    /// All lengths are art pixels at density 24 (y up, origin at the lower-left corner of the lattice extent, as in Unity) unless a key
    /// says otherwise. The screen rows reuse the screen-class tables of <see cref="ScreenClassScaleTableTests"/> (the panel-point screens of
    /// <c>ShellScreens.All</c> at every panel scale, and the physical devices), which feed <see cref="CameraScaleModel"/>.
    /// </summary>
    public sealed class LatticeGeometryExportTests
    {
        private const string RelativePath = "tools/art-pipeline/f006/lattice-geometry.json";
        private const string Schema = "f006-lattice-geometry/1";
        private const int D = SlotGridLayout.Density;

        private static readonly int[] ExportedColumns = { 3, 4 };

        private static string F(float value)
        {
            var text = value.ToString("0.###", CultureInfo.InvariantCulture);
            return text == "-0" ? "0" : text;
        }

        private static int Px(float world) => Mathf.RoundToInt(world * D);

        private static string Arr(params float[] values) => "[" + string.Join(", ", values.Select(F)) + "]";

        private static string Q(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>A box relative to the pivot as [x, y, width, height] in art pixels.</summary>
        private static string BoxArr(Rect box) => Arr(box.x * D, box.y * D, box.width * D, box.height * D);

        internal static string BuildJson()
        {
            var lines = new List<string>
            {
                "{",
                "  \"schema\": " + Q(Schema) + ",",
                "  \"source\": \"SlotGridLayout and CameraScaleModel via LatticeGeometryExportTests (Edit Mode); do not edit by hand\",",
                "  \"unit\": \"art pixel at density 24, y up, origin at the lower-left corner of the lattice extent\",",
                "  \"density\": " + D + ",",
                "  \"slotCount\": " + SlotGridLayout.SlotCount + ",",
                "  \"pitchPx\": " + Arr(SlotGridLayout.PitchXPixels, SlotGridLayout.PitchYPixels) + ",",
                "  \"platePx\": " + Arr(SlotGridLayout.PlateWidthPixels, SlotGridLayout.PlateHeightPixels) + ",",
                "  \"hitPx\": " + Arr(SlotGridLayout.HitWidthPixels, SlotGridLayout.HitHeightPixels) + ",",
                "  \"pivotAbovePlateBottomPx\": " + SlotGridLayout.PivotAbovePlateBottomPixels + ",",
                "  \"contentAbovePivotPx\": " + SlotGridLayout.ContentAbovePivotPixels + ",",
                "  \"plateCentreAbovePivotPx\": " + F(SlotGridLayout.PlateCentreAbovePivot * D) + ",",
                "  \"rowGapPx\": " + (SlotGridLayout.PitchYPixels - SlotGridLayout.PlateHeightPixels) + ",",
                "  \"halfCellMarginPx\": " + F(SlotGridLayout.HalfCellMargin * D) + ",",
                "  \"badgeBoxPx\": " + BoxArr(StateCueGeometry.BadgeBox()) + ",",
                "  \"markerBoxesPx\": [" + BoxArr(StateCueGeometry.MarkerBox(0)) + ", " + BoxArr(StateCueGeometry.MarkerBox(1)) + "],",
                "  \"layouts\": [",
            };

            for (var i = 0; i < ExportedColumns.Length; i++)
            {
                AppendLayout(lines, ExportedColumns[i], i == ExportedColumns.Length - 1);
            }

            lines.Add("  ],");
            lines.Add("  \"pointScreens\": [");
            AppendRows(lines, ScreenClassScaleTableTests.PointRows(), null);
            lines.Add("  ],");
            lines.Add("  \"devices\": [");
            AppendRows(lines, ScreenClassScaleTableTests.DeviceRows(), ScreenClassScaleTableTests.Devices().ToList());
            lines.Add("  ]");
            lines.Add("}");
            return string.Join("\n", lines) + "\n";
        }

        private static void AppendLayout(List<string> lines, int columns, bool last)
        {
            var rows = SlotGridLayout.RowsFor(columns);
            var extent = SlotGridLayout.Extent(columns);
            lines.Add("    {");
            lines.Add("      \"columns\": " + columns + ",");
            lines.Add("      \"rows\": " + rows + ",");
            lines.Add("      \"orientation\": " + Q(columns == 3 ? "portrait" : "landscape") + ",");
            lines.Add("      \"extentPx\": " + Arr(extent.width * D, extent.height * D) + ",");
            lines.Add("      \"extentCells\": " + Arr(extent.width, extent.height) + ",");
            var gaps = new List<float>();
            for (var r = 1; r < rows; r++)
            {
                var back = SlotGridLayout.SlotRect(columns * (r - 1), columns);
                var front = SlotGridLayout.SlotRect(columns * r, columns);
                gaps.Add((back.yMin - front.yMax) * D);
            }

            lines.Add("      \"rowGapsPx\": " + Arr(gaps.ToArray()) + ",");
            lines.Add("      \"slots\": [");
            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                var pivot = SlotGridLayout.PivotOf(i, columns) * D;
                var plate = SlotGridLayout.SlotRect(i, columns);
                var centre = SlotGridLayout.PlateCentreOf(i, columns) * D;
                lines.Add("        { \"index\": " + i + ", \"column\": " + (i % columns) + ", \"row\": " + SlotGridLayout.RowOf(i, columns) +
                          ", \"pivotPx\": " + Arr(pivot.x, pivot.y) +
                          ", \"plateRectPx\": " + Arr(plate.x * D, plate.y * D, plate.width * D, plate.height * D) +
                          ", \"hitDiamondPx\": { \"centre\": " + Arr(centre.x, centre.y) +
                          ", \"halfWidth\": " + F(SlotGridLayout.HitWidthPixels * 0.5f) + ", \"halfHeight\": " + F(SlotGridLayout.HitHeightPixels * 0.5f) + " } }" +
                          (i == SlotGridLayout.SlotCount - 1 ? string.Empty : ","));
            }

            lines.Add("      ]");
            lines.Add("    }" + (last ? string.Empty : ","));
        }

        private static void AppendRows(List<string> lines, List<ScreenClassScaleTableTests.Row> rows, List<ScreenClassScaleTableTests.Device> devices)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var columns = SlotGridLayout.ColumnsFor(row.WidthPoints, row.HeightPoints);
                var rect = ScreenClassScaleTableTests.UnobstructedRectPixels(row.Layout, row.WidthPoints, row.HeightPoints, row.Effective);
                var result = ScreenClassScaleTableTests.Evaluate(row, D);
                lines.Add("    { \"name\": " + Q(row.Name) + ", \"class\": " + Q(row.Layout.ToString()) +
                          ", \"widthPx\": " + row.WidthPx + ", \"heightPx\": " + row.HeightPx +
                          ", \"widthPt\": " + F(row.WidthPoints) + ", \"heightPt\": " + F(row.HeightPoints) +
                          ", \"pixelsPerPoint\": " + F(row.Effective) +
                          (devices != null ? ", \"dpi\": " + F(devices[i].Dpi) : string.Empty) +
                          ", \"columns\": " + columns +
                          ", \"unobstructedPx\": " + Arr(rect.x, rect.y, rect.width, rect.height) +
                          ", \"nMin\": " + result.NMin + ", \"nFit\": " + result.NFit + " }" +
                          (i == rows.Count - 1 ? string.Empty : ","));
            }
        }

        [Test]
        public void ExportsTheGeometryToTheSharedJsonDeterministically()
        {
            var first = BuildJson();
            var second = BuildJson();
            Assert.That(second, Is.EqualTo(first), "two exports in one session must be byte-identical");
            Assert.That(first, Does.Not.Contain("\r"), "LF line ends only");
            Assert.That(first, Does.Not.Contain("NaN").And.Not.Contain("Infinity"));
            Assert.That(first, Does.EndWith("}\n"));

            var path = Path.Combine(RepositoryRoot(), RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, first, new UTF8Encoding(false));
            Assert.That(File.ReadAllText(path), Is.EqualTo(first), "the written file is the exported text");
        }

        [Test]
        public void TheExportHasTheStructureTheCompositorReads()
        {
            var json = BuildJson();
            foreach (var key in new[]
                     {
                         "\"schema\"", "\"density\"", "\"pitchPx\"", "\"platePx\"", "\"hitPx\"", "\"rowGapPx\"", "\"badgeBoxPx\"", "\"markerBoxesPx\"",
                         "\"layouts\"", "\"pointScreens\"", "\"devices\"", "\"extentPx\"", "\"slots\"", "\"pivotPx\"", "\"plateRectPx\"",
                         "\"hitDiamondPx\"", "\"nMin\"", "\"nFit\"", "\"unobstructedPx\"",
                     })
            {
                Assert.That(json, Does.Contain(key));
            }

            Assert.That(Count(json, "\"pivotPx\""), Is.EqualTo(ExportedColumns.Length * SlotGridLayout.SlotCount));
            Assert.That(Count(json, "\n      \"columns\": 3,"), Is.EqualTo(1));
            Assert.That(Count(json, "\n      \"columns\": 4,"), Is.EqualTo(1));
            var points = ScreenClassScaleTableTests.PointRows();
            var devices = ScreenClassScaleTableTests.DeviceRows();
            Assert.That(points.Count, Is.EqualTo(8 * ScreenClassScaleTableTests.PixelsPerPointSet.Length), "8 screen classes x panel scales");
            Assert.That(devices.Count, Is.EqualTo(11));
            Assert.That(Count(json, "\"nFit\""), Is.EqualTo(points.Count + devices.Count));
            Assert.That(json, Does.Contain("\"name\": \"phone-portrait 1080x2400 420dpi\""));
            Assert.That(json, Does.Contain("\"name\": \"tablet-portrait 1536x2048 264dpi\""));
            Assert.That(json, Does.Contain("\"name\": \"phone-landscape 640x360 @2\""), "the 640x360 class at 200 percent is present");
        }

        [Test]
        public void TheExportedLatticeFollowsTheLayoutRules([Values(3, 4)] int columns)
        {
            var extent = SlotGridLayout.Extent(columns);
            var rows = SlotGridLayout.RowsFor(columns);
            Assert.That(Px(extent.width), Is.EqualTo((columns - 1) * 70 + 70));
            Assert.That(Px(extent.height), Is.EqualTo((rows - 1) * 54 + 15 + 39));
            Assert.That(SlotGridLayout.PitchYPixels - SlotGridLayout.PlateHeightPixels, Is.EqualTo(19), "R07: the gap between rows is 19 px");
            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                var pivot = SlotGridLayout.PivotOf(i, columns) * D;
                Assert.That(pivot.x, Is.EqualTo(Mathf.Round(pivot.x)).Within(1e-3f));
                Assert.That(pivot.y, Is.EqualTo(Mathf.Round(pivot.y)).Within(1e-3f));
                Assert.That(Mathf.RoundToInt(pivot.x), Is.EqualTo(35 + 70 * (i % columns)));
                var plate = SlotGridLayout.SlotRect(i, columns);
                Assert.That(plate.width * D, Is.EqualTo(70f).Within(1e-3f));
                Assert.That(plate.height * D, Is.EqualTo(35f).Within(1e-3f));
                Assert.That(plate.yMin * D, Is.EqualTo(pivot.y - 15f).Within(1e-3f), "the pivot sits 15 px above the plate bottom");
                if (i >= columns)
                {
                    var back = SlotGridLayout.SlotRect(i - columns, columns);
                    Assert.That((back.yMin - plate.yMax) * D, Is.EqualTo(19f).Within(1e-3f), "row gap of slot " + i);
                }
            }
        }

        [Test]
        public void EveryExportedScreenRowKeepsNFitAtOrAboveNMin()
        {
            var all = ScreenClassScaleTableTests.PointRows().Concat(ScreenClassScaleTableTests.DeviceRows()).ToList();
            foreach (var row in all)
            {
                var result = ScreenClassScaleTableTests.Evaluate(row, D);
                Assert.That(result.NMin, Is.GreaterThanOrEqualTo(1), row.Name);
                Assert.That(result.NFit, Is.GreaterThanOrEqualTo(result.NMin), row.Name);
            }
        }

        private static int Count(string text, string needle)
        {
            var count = 0;
            var at = 0;
            while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += needle.Length;
            }

            return count;
        }
    }
}
