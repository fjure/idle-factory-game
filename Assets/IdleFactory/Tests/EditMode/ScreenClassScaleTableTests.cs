using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using IdleFactory.Unity.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The screen-class table of <c>contracts/camera-scale.md</c> and research R03 as an executable rule: for every Feature 003
    /// screen class and orientation at its physical resolution, <c>n_fit &gt;= n_min</c>. The table (level count and margin per
    /// class) is emitted to the test output for every spike and acceptance record. The HUD-aware rectangle here is a fixed
    /// model of the reserved shell areas (see <see cref="Reserved"/>); the Play Mode variant drives the same rule from the
    /// real <c>TryGetUnobstructedScreenRect</c>.
    /// </summary>
    public sealed class ScreenClassScaleTableTests
    {
        private const int MaxScale = 6;
        private const int ReadablePoints = 24;

        /// <summary>The Feature 003 densities under evaluation (research R03).</summary>
        private static readonly int[] Densities = { 16, 24, 32 };

        /// <summary>Densities that must satisfy <c>n_fit &gt;= n_min</c> on every class. D = 32 is evaluated and emitted but is known to fail (R03).</summary>
        private static readonly int[] SupportedDensities = { 16, 24 };

        public readonly struct Device
        {
            public Device(string name, int widthPx, int heightPx, float dpi, bool touch, FactoryHudLayout expectedClass)
            {
                Name = name;
                WidthPx = widthPx;
                HeightPx = heightPx;
                Dpi = dpi;
                Touch = touch;
                ExpectedClass = expectedClass;
            }

            public string Name { get; }
            public int WidthPx { get; }
            public int HeightPx { get; }
            public float Dpi { get; }
            public bool Touch { get; }
            public FactoryHudLayout ExpectedClass { get; }

            public override string ToString() => Name;
        }

        public readonly struct Row
        {
            public Row(string name, FactoryHudLayout layout, int widthPx, int heightPx, float widthPoints, float heightPoints, float authored, float effective)
            {
                Name = name;
                Layout = layout;
                WidthPx = widthPx;
                HeightPx = heightPx;
                WidthPoints = widthPoints;
                HeightPoints = heightPoints;
                Authored = authored;
                Effective = effective;
            }

            public string Name { get; }
            public FactoryHudLayout Layout { get; }
            public int WidthPx { get; }
            public int HeightPx { get; }
            public float WidthPoints { get; }
            public float HeightPoints { get; }
            public float Authored { get; }
            public float Effective { get; }
        }

        public readonly struct Result
        {
            public Result(Row row, int density, int nMin, int nFit, int levels, float margin)
            {
                Row = row;
                Density = density;
                NMin = nMin;
                NFit = nFit;
                Levels = levels;
                Margin = margin;
            }

            public Row Row { get; }
            public int Density { get; }
            public int NMin { get; }
            public int NFit { get; }
            public int Levels { get; }
            public float Margin { get; }
            public bool Satisfied => NFit >= NMin;
        }

        /// <summary>Physical reference devices: every screen class in both orientations (phones with known DPI, tablets, desktop windows).</summary>
        public static IEnumerable<Device> Devices()
        {
            yield return new Device("phone-portrait 720x1280 xhdpi", 720, 1280, 320f, true, FactoryHudLayout.CompactPortrait);
            yield return new Device("phone-landscape 1280x720 xhdpi", 1280, 720, 320f, true, FactoryHudLayout.CompactLandscape);
            yield return new Device("phone-portrait 1080x2400 420dpi", 1080, 2400, 420f, true, FactoryHudLayout.CompactPortrait);
            yield return new Device("phone-landscape 2400x1080 420dpi", 2400, 1080, 420f, true, FactoryHudLayout.CompactLandscape);
            yield return new Device("phone-portrait 1170x2532 460dpi", 1170, 2532, 460f, true, FactoryHudLayout.CompactPortrait);
            yield return new Device("phone-landscape 2532x1170 460dpi", 2532, 1170, 460f, true, FactoryHudLayout.CompactLandscape);
            yield return new Device("tablet-portrait 1536x2048 264dpi", 1536, 2048, 264f, true, FactoryHudLayout.TabletPortrait);
            yield return new Device("tablet-landscape 2048x1536 264dpi", 2048, 1536, 264f, true, FactoryHudLayout.TabletLandscape);
            yield return new Device("desktop 1280x720", 1280, 720, 0f, false, FactoryHudLayout.Desktop);
            yield return new Device("desktop 1920x1080", 1920, 1080, 0f, false, FactoryHudLayout.Desktop);
            yield return new Device("desktop 2560x1440", 2560, 1440, 0f, false, FactoryHudLayout.Desktop);
        }

        /// <summary>Panel-point screen sizes of Feature 003 (<c>ShellScreens.All</c>, plus 390x844 landscape-free phone) at fixed scales.</summary>
        public static IEnumerable<(string name, float width, float height, FactoryHudLayout layout)> PointScreens()
        {
            yield return ("phone-portrait 360x640", 360f, 640f, FactoryHudLayout.CompactPortrait);
            yield return ("phone-landscape 640x360", 640f, 360f, FactoryHudLayout.CompactLandscape);
            yield return ("phone-portrait 390x844", 390f, 844f, FactoryHudLayout.CompactPortrait);
            yield return ("phone-landscape 844x390", 844f, 390f, FactoryHudLayout.CompactLandscape);
            yield return ("tablet-portrait 768x1024", 768f, 1024f, FactoryHudLayout.TabletPortrait);
            yield return ("tablet-landscape 1024x768", 1024f, 768f, FactoryHudLayout.TabletLandscape);
            yield return ("desktop 1280x720", 1280f, 720f, FactoryHudLayout.Desktop);
            yield return ("desktop 1920x1080", 1920f, 1080f, FactoryHudLayout.Desktop);
        }

        /// <summary>Panel scales (pixels per point) the camera must support: the reference phone, 2.0, and the 2.625 and 3.0 touch-target raises.</summary>
        public static readonly float[] PixelsPerPointSet = { 1.68f, 2.0f, 2.625f, 3.0f };

        /// <summary>
        /// Reserved shell areas in panel points, a fixed model of the closed shell (top strip 48 + detail/hint bar 32, bottom tab bar
        /// 56 on phone portrait, a 360 pt side panel on tablets and desktop, a 52 pt zoom column on the left).
        /// </summary>
        public static Rect UnobstructedRectPixels(FactoryHudLayout layout, float widthPoints, float heightPoints, float pixelsPerPoint)
        {
            const float top = 80f;
            const float zoomColumn = 52f;
            var bottom = layout == FactoryHudLayout.CompactPortrait ? 56f : 0f;
            var right = FactoryHudDocument.HasSidePanel(layout) ? 360f : 0f;
            var rect = Rect.MinMaxRect(zoomColumn, bottom, widthPoints - right, heightPoints - top);
            return new Rect(rect.x * pixelsPerPoint, rect.y * pixelsPerPoint, rect.width * pixelsPerPoint, rect.height * pixelsPerPoint);
        }

        public static Result Evaluate(Row row, int density)
        {
            var settings = new ArtScaleSettings(density, ReadablePoints, MaxScale);
            var columns = SlotGridLayout.ColumnsFor(row.WidthPoints, row.HeightPoints);
            var rect = UnobstructedRectPixels(row.Layout, row.WidthPoints, row.HeightPoints, row.Effective);
            var nMin = CameraScaleModel.MinScale(settings, row.Effective);
            var nFit = Math.Min(CameraScaleModel.LargestFittingScale(rect, LatticeLayoutBuilder.ShippedForColumns(columns).Bounds, density), MaxScale);
            var levels = Math.Max(0, MaxScale - nMin + 1);
            var margin = nFit >= 1 ? CameraScaleModel.MarginFraction(rect, LatticeLayoutBuilder.ShippedForColumns(columns).Bounds, nFit, density) : 1f;
            return new Result(row, density, nMin, nFit, levels, margin);
        }

        private static PanelSettings CreateAuthoredPanelSettings()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            return settings;
        }

        public static List<Row> DeviceRows()
        {
            var settings = CreateAuthoredPanelSettings();
            try
            {
                var rows = new List<Row>();
                foreach (var device in Devices())
                {
                    var authored = ShellPanelScale.AuthoredPixelsPerPoint(settings, device.WidthPx, device.HeightPx, device.Dpi);
                    var profile = ShellDeviceProfile.Create(device.Touch ? ShellPrimaryInput.Touch : ShellPrimaryInput.Pointer, device.WidthPx, device.HeightPx, device.Dpi);
                    var effective = ShellPanelScale.Decide(profile, authored).EffectivePixelsPerPoint;
                    var widthPoints = device.WidthPx / effective;
                    var heightPoints = device.HeightPx / effective;
                    rows.Add(new Row(device.Name, FactoryHudDocument.Classify(widthPoints, heightPoints), device.WidthPx, device.HeightPx, widthPoints, heightPoints, authored, effective));
                }

                return rows;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        public static List<Row> PointRows()
        {
            var rows = new List<Row>();
            foreach (var screen in PointScreens())
            {
                foreach (var scale in PixelsPerPointSet)
                {
                    rows.Add(new Row($"{screen.name} @{scale.ToString("0.###", CultureInfo.InvariantCulture)}", screen.layout, Mathf.RoundToInt(screen.width * scale), Mathf.RoundToInt(screen.height * scale), screen.width, screen.height, scale, scale));
                }
            }

            return rows;
        }

        public static string FormatTable(string title, IEnumerable<Result> results)
        {
            var text = new StringBuilder();
            text.AppendLine($"Screen-class scale table: {title}");
            text.AppendLine("row | class | px | pt | authored px/pt | effective px/pt | D | n_min | n_fit | levels | margin %");
            foreach (var r in results)
            {
                text.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} | {1} | {2}x{3} | {4:0}x{5:0} | {6:0.###} | {7:0.###} | {8} | {9} | {10} | {11} | {12:0.0}{13}",
                    r.Row.Name, r.Row.Layout, r.Row.WidthPx, r.Row.HeightPx, r.Row.WidthPoints, r.Row.HeightPoints, r.Row.Authored, r.Row.Effective,
                    r.Density, r.NMin, r.NFit, r.Levels, r.Margin * 100f, r.Satisfied ? string.Empty : " VIOLATION"));
            }

            return text.ToString();
        }

        /// <summary>
        /// No violation remains at a supported density. The one earlier known violation, a 320 dpi phone in landscape, was resolved by
        /// the owner decision of 2026-10-28 (T427): the 7 mm rule raises the panel scale to 2.0046, and n_min = ceil(24 * 2.0046 / 24 -
        /// 0.01) is 2 with the 1 % tolerance (it was 3 with the exact ceiling, while the 640x359 pt screen fits only n = 2). Any
        /// violation at D = 16 or D = 24 fails the test.
        /// </summary>
        private static readonly string[] KnownViolations = Array.Empty<string>();

        private const string FormerViolationRow = "phone-landscape 1280x720 xhdpi";

        private static void AssertOnlyKnownViolations(IEnumerable<Result> results)
        {
            var violations = new List<string>();
            foreach (var result in results)
            {
                if (Array.IndexOf(SupportedDensities, result.Density) >= 0 && !result.Satisfied)
                {
                    violations.Add($"{result.Row.Name} D={result.Density}");
                }
            }

            var unexpected = violations.FindAll(v => Array.IndexOf(KnownViolations, v) < 0);
            Assert.That(unexpected, Is.Empty, "n_fit < n_min outside the known violation list: " + string.Join("; ", unexpected));
        }

        [Test]
        public void ThePhysicalDevicesClassifyAsTheirScreenClass()
        {
            var expected = new List<FactoryHudLayout>();
            foreach (var device in Devices())
            {
                expected.Add(device.ExpectedClass);
            }

            var rows = DeviceRows();
            for (var i = 0; i < rows.Count; i++)
            {
                Assert.That(rows[i].Layout, Is.EqualTo(expected[i]), $"{rows[i].Name}: {rows[i].WidthPoints:0}x{rows[i].HeightPoints:0} pt");
            }
        }

        [Test]
        public void EveryScreenClassAndOrientationKeepsNFitAtOrAboveNMinForTheSupportedDensities()
        {
            var results = new List<Result>();
            foreach (var row in DeviceRows())
            {
                foreach (var density in Densities)
                {
                    results.Add(Evaluate(row, density));
                }
            }

            TestContext.Out.WriteLine(FormatTable("physical reference devices", results));
            AssertOnlyKnownViolations(results);
        }

        [Test]
        public void ThePointScreensAtEveryPanelScaleKeepNFitAtOrAboveNMinForTheSupportedDensities()
        {
            var results = new List<Result>();
            foreach (var row in PointRows())
            {
                foreach (var density in Densities)
                {
                    results.Add(Evaluate(row, density));
                }
            }

            TestContext.Out.WriteLine(FormatTable("panel-point screens x panel scales", results));
            AssertOnlyKnownViolations(results);
        }

        [Test]
        public void ADensityOf32FailsTheRuleOnALowScalePhone()
        {
            // Research R03: 32 art px per cell cannot meet the 24 pt floor on a low panel-scale phone in landscape.
            var violations = 0;
            foreach (var row in PointRows())
            {
                if (Evaluate(row, 32).Satisfied)
                {
                    continue;
                }

                violations++;
            }

            Assert.That(violations, Is.GreaterThan(0), "D = 32 must violate n_fit >= n_min somewhere, which is why the spike picks 24 or 16");
        }

        [Test]
        public void AnOversizedHudViolatesTheRule()
        {
            // Negative case: reserve far more HUD than the shell does; the table rule must then fail instead of passing silently.
            var row = new Row("phone-landscape 640x360 oversized HUD", FactoryHudLayout.CompactLandscape, 1075, 605, 640f, 360f, 1.68f, 1.68f);
            var settings = new ArtScaleSettings(24, ReadablePoints, MaxScale);
            var oversized = new Rect(0f, 200f * 1.68f, 640f * 1.68f, (360f - 200f - 80f) * 1.68f);
            var nFit = CameraScaleModel.LargestFittingScale(oversized, LatticeLayoutBuilder.ShippedForColumns(4).Bounds, 24);
            Assert.That(nFit, Is.LessThan(CameraScaleModel.MinScale(settings, row.Effective)));
        }

        [Test]
        public void NoViolationRemainsAtD24WithTheOnePercentTolerance()
        {
            var violations = new List<string>();
            foreach (var row in DeviceRows())
            {
                if (!Evaluate(row, 24).Satisfied)
                {
                    violations.Add(row.Name + " D=24");
                }
            }

            Assert.That(violations, Is.EquivalentTo(KnownViolations));
        }

        [Test]
        public void TheFormerViolationIsOnlyResolvedByTheTolerance()
        {
            // Owner decision S2 = D24 (2026-10-29): the 1280x720 phone at 320 dpi (2.0046 px/pt) needs the 1 % tolerance.
            Row phone = default;
            foreach (var row in DeviceRows())
            {
                if (row.Name == FormerViolationRow)
                {
                    phone = row;
                }
            }

            Assert.That(phone.Name, Is.EqualTo(FormerViolationRow));
            Assert.That(phone.Effective, Is.EqualTo(2.0046f).Within(0.0005f));
            var result = Evaluate(phone, 24);
            Assert.That(result.NMin, Is.EqualTo(2), "n_min with the tolerance");
            Assert.That(result.NFit, Is.EqualTo(3), "T456: the 280 x 162 px lattice fits one scale higher than the former 2.5-cell grid");
            Assert.That(result.Satisfied, Is.True);
            var exactCeiling = (int)Math.Ceiling(ReadablePoints * phone.Effective / 24f);
            Assert.That(exactCeiling, Is.EqualTo(3), "without the tolerance n_min would be 3 > n_fit 2, the violation of the S2 record");
            Assert.That(ReadablePoints * phone.Effective / 24f - 2f, Is.LessThan(CameraScaleModel.MinScaleTolerance), "the miss is under 1 %");
        }

        [Test]
        public void The360x640AndThe390x844PortraitPhonesHaveTheExpectedLevelsAndMarginAtD24()
        {
            // Pinned from the emitted table (D = 24, 24 pt floor, max scale 6, closed-shell HUD model): n_min, n_fit, level count, margin %.
            var expectations = new Dictionary<string, (int nMin, int nFit, int levels, float margin)>
            {
                { "phone-portrait 360x640 @1.68", (2, 2, 5, 18.8f) },
                { "phone-portrait 360x640 @2", (2, 2, 5, 31.8f) },
                { "phone-portrait 360x640 @2.625", (3, 3, 4, 22.1f) },
                { "phone-portrait 360x640 @3", (3, 3, 4, 31.8f) },
                { "phone-portrait 390x844 @1.68", (2, 2, 5, 26.0f) },
                { "phone-portrait 390x844 @2", (2, 2, 5, 37.9f) },
                { "phone-portrait 390x844 @2.625", (3, 3, 4, 29.0f) },
                { "phone-portrait 390x844 @3", (3, 4, 4, 17.2f) },
            };

            var results = new Dictionary<string, Result>();
            foreach (var row in PointRows())
            {
                results[row.Name] = Evaluate(row, 24);
            }

            foreach (var pair in expectations)
            {
                var result = results[pair.Key];
                Assert.That(result.NMin, Is.EqualTo(pair.Value.nMin), pair.Key + " n_min");
                Assert.That(result.NFit, Is.EqualTo(pair.Value.nFit), pair.Key + " n_fit");
                Assert.That(result.Levels, Is.EqualTo(pair.Value.levels), pair.Key + " levels");
                Assert.That(result.Margin * 100f, Is.EqualTo(pair.Value.margin).Within(0.1f), pair.Key + " margin %");
            }
        }
    }
}
