#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using IdleFactory.Unity.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>A named rectangle in panel points with a top-left origin (the convention of <c>worldBound</c>).</summary>
    public readonly struct NamedRect
    {
        public NamedRect(string name, Rect rect)
        {
            Name = name;
            Rect = rect;
        }

        public string Name { get; }
        public Rect Rect { get; }
    }

    /// <summary>One visible text element the probe counted: the persistent element it belongs to, its text and whether it is a <see cref="Label"/>.</summary>
    public readonly struct ProbeText
    {
        public ProbeText(string owner, string text, bool isLabel)
        {
            Owner = owner;
            Text = text;
            IsLabel = isLabel;
        }

        public string Owner { get; }
        public string Text { get; }
        public bool IsLabel { get; }
    }

    /// <summary>The geometry of one shell state at one screen class and text scale (F006 research R02, FR-046).</summary>
    public sealed class ShellGeometryResult
    {
        public string Screen { get; set; }
        public string State { get; set; }
        public int TextScalePercent { get; set; }
        public float PixelsPerPoint { get; set; }

        /// <summary>The safe area in panel points, top-left origin.</summary>
        public Rect Safe { get; set; }

        /// <summary>The world rect the shell leaves uncovered (<c>TryGetUnobstructedScreenRect</c>), in panel points, or null when the shell reports none.</summary>
        public Rect? World { get; set; }

        /// <summary>The persistent elements that were displayed, with their <c>worldBound</c>.</summary>
        public List<NamedRect> Chrome { get; set; } = new List<NamedRect>();

        /// <summary>The sheet or side panel host when it was displayed, otherwise null.</summary>
        public Rect? Sheet { get; set; }

        /// <summary>The modal overlay card (offline summary) when one was open, otherwise null.</summary>
        public Rect? Overlay { get; set; }

        public List<ProbeText> Texts { get; set; } = new List<ProbeText>();

        /// <summary>Further named measurements (selected slot visibility and so on), written in key order.</summary>
        public SortedDictionary<string, double> Notes { get; } = new SortedDictionary<string, double>(StringComparer.Ordinal);

        public double SafeArea => ShellGeometryProbe.Area(Safe);

        /// <summary>World rect clipped to the safe area, as a fraction of the safe area (SC-001, 60 % guidance).</summary>
        public double WorldShare => World.HasValue ? ShellGeometryProbe.Share(ShellGeometryProbe.IntersectionArea(World.Value, Safe), SafeArea) : 0d;

        /// <summary>Union of the persistent chrome clipped to the safe area, as a fraction of the safe area (SC-001, 15 % guidance, 20 % at 200 % text).</summary>
        public double ChromeShare => ShellGeometryProbe.Share(ShellGeometryProbe.UnionArea(Chrome.Select(item => item.Rect).ToList(), Safe), SafeArea);

        public double SheetShare => Sheet.HasValue ? ShellGeometryProbe.Share(ShellGeometryProbe.IntersectionArea(Sheet.Value, Safe), SafeArea) : 0d;

        public double OverlayShare => Overlay.HasValue ? ShellGeometryProbe.Share(ShellGeometryProbe.IntersectionArea(Overlay.Value, Safe), SafeArea) : 0d;

        /// <summary>Visible <see cref="Label"/>s with text (SC-010 counts these).</summary>
        public int LabelCount => Texts.Count(item => item.IsLabel);

        /// <summary>Visible <see cref="Label"/>s and buttons with text, so the count does not hide text that a button carries.</summary>
        public int TextElementCount => Texts.Count;
    }

    /// <summary>
    /// The F006 baseline probe (T502, research R02). It measures a run shell from UI Toolkit <c>worldBound</c>, as RunHudLayoutTests does:
    /// the world rect is <see cref="RunShellDocument.TryGetUnobstructedScreenRect"/>, the chrome is the union of the persistent elements
    /// within the safe area, and the label count is the number of visible labels with text. The geometry arithmetic is pure so that a
    /// self-test can prove it on synthetic layouts; <see cref="Measure"/> is the only part that reads a live shell.
    /// </summary>
    public static class ShellGeometryProbe
    {
        /// <summary>The persistent elements of the shell, by UXML name, in the order their labels are listed.</summary>
        public static readonly string[] PersistentNames =
        {
            "top-strip", "overdrive", "zoom-column", "factory-ore-vein", "detail-line", "notice", "world-hint", "tab-bar",
        };

        // ------------------------------------------------------------------------------------------------------
        // Pure geometry
        // ------------------------------------------------------------------------------------------------------

        public static double Area(Rect rect) => rect.width <= 0f || rect.height <= 0f ? 0d : (double)rect.width * rect.height;

        public static double Share(double part, double whole) => whole <= 0d ? 0d : part / whole;

        public static bool TryIntersect(Rect a, Rect b, out Rect result)
        {
            var xMin = Math.Max(a.xMin, b.xMin);
            var yMin = Math.Max(a.yMin, b.yMin);
            var xMax = Math.Min(a.xMax, b.xMax);
            var yMax = Math.Min(a.yMax, b.yMax);
            if (xMax <= xMin || yMax <= yMin)
            {
                result = default;
                return false;
            }

            result = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        public static double IntersectionArea(Rect a, Rect b) => TryIntersect(a, b, out var result) ? Area(result) : 0d;

        /// <summary>The area of the union of <paramref name="rects"/> clipped to <paramref name="clip"/>, by coordinate compression (overlaps count once).</summary>
        public static double UnionArea(IReadOnlyList<Rect> rects, Rect clip)
        {
            var clipped = new List<Rect>();
            foreach (var rect in rects)
            {
                if (TryIntersect(rect, clip, out var inside))
                {
                    clipped.Add(inside);
                }
            }

            if (clipped.Count == 0)
            {
                return 0d;
            }

            var xs = clipped.SelectMany(rect => new[] { rect.xMin, rect.xMax }).Distinct().OrderBy(x => x).ToList();
            double total = 0d;
            for (var i = 0; i + 1 < xs.Count; i++)
            {
                var left = xs[i];
                var right = xs[i + 1];
                var spans = clipped.Where(rect => rect.xMin <= left && rect.xMax >= right).Select(rect => (rect.yMin, rect.yMax)).OrderBy(span => span.yMin).ToList();
                double covered = 0d;
                var start = float.NaN;
                var end = float.NaN;
                foreach (var span in spans)
                {
                    if (float.IsNaN(start))
                    {
                        start = span.yMin;
                        end = span.yMax;
                    }
                    else if (span.yMin <= end)
                    {
                        end = Math.Max(end, span.yMax);
                    }
                    else
                    {
                        covered += end - start;
                        start = span.yMin;
                        end = span.yMax;
                    }
                }

                if (!float.IsNaN(start))
                {
                    covered += end - start;
                }

                total += covered * (right - left);
            }

            return total;
        }

        /// <summary>The fraction of <paramref name="inner"/> that lies inside <paramref name="outer"/>.</summary>
        public static double ContainedShare(Rect inner, Rect outer) => Share(IntersectionArea(inner, outer), Area(inner));

        // ------------------------------------------------------------------------------------------------------
        // Visible text
        // ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// True when the element draws: it and every ancestor are displayed and not transparent, the element is not hidden, and its
        /// <c>worldBound</c> has an area and overlaps the safe area.
        /// </summary>
        public static bool IsVisible(VisualElement element, Rect safe)
        {
            if (element == null || element.panel == null)
            {
                return false;
            }

            for (var current = element; current != null; current = current.parent)
            {
                var style = current.resolvedStyle;
                if (style.display == DisplayStyle.None || style.opacity <= 0.01f)
                {
                    return false;
                }
            }

            if (element.resolvedStyle.visibility != Visibility.Visible)
            {
                return false;
            }

            var bound = element.worldBound;
            if (float.IsNaN(bound.x) || float.IsNaN(bound.y) || bound.width <= 0f || bound.height <= 0f)
            {
                return false;
            }

            return TryIntersect(bound, safe, out _);
        }

        /// <summary>The visible text elements (labels and buttons) with non-blank text below the given roots; an element belongs to the first root that holds it.</summary>
        public static List<ProbeText> CollectTexts(IReadOnlyList<VisualElement> roots, Rect safe)
        {
            var seen = new HashSet<VisualElement>();
            var result = new List<ProbeText>();
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                foreach (var text in root.Query<TextElement>().ToList())
                {
                    if (!seen.Add(text) || string.IsNullOrWhiteSpace(text.text) || !IsVisible(text, safe))
                    {
                        continue;
                    }

                    result.Add(new ProbeText(root.name, text.text.Trim(), text is Label));
                }
            }

            return result;
        }

        // ------------------------------------------------------------------------------------------------------
        // Live shell
        // ------------------------------------------------------------------------------------------------------

        /// <summary>The safe area of the rig's screen class in panel points with a top-left origin, converted with the panel scale in force.</summary>
        public static Rect SafeRectPoints(ShellRig rig)
        {
            var ppp = rig.PixelsPerPoint;
            var bounds = rig.Root.panel.visualTree.worldBound;
            return Rect.MinMaxRect(
                bounds.xMin + rig.Screen.Left / ppp,
                bounds.yMin + rig.Screen.Top / ppp,
                bounds.xMax - rig.Screen.Right / ppp,
                bounds.yMax - rig.Screen.Bottom / ppp);
        }

        /// <summary>Converts the shell's screen-pixel world rect (bottom-left origin) into panel points with a top-left origin.</summary>
        public static Rect WorldPoints(Rect pixels, float screenHeightPixels, float pixelsPerPoint) =>
            new Rect(pixels.x / pixelsPerPoint, (screenHeightPixels - pixels.yMax) / pixelsPerPoint, pixels.width / pixelsPerPoint, pixels.height / pixelsPerPoint);

        /// <summary>Measures a live shell in its current state. <paramref name="overlay"/> is a modal card that sits above the shell, when one is open.</summary>
        public static ShellGeometryResult Measure(ShellRig rig, string state, VisualElement overlay = null)
        {
            var shell = rig.Shell;
            var ppp = rig.PixelsPerPoint;
            var safe = SafeRectPoints(rig);
            var result = new ShellGeometryResult
            {
                Screen = rig.Screen.Name,
                State = state,
                TextScalePercent = rig.TextScalePercent,
                PixelsPerPoint = ppp,
                Safe = safe,
            };

            if (shell.TryGetUnobstructedScreenRect(rig.Screen.Height, out var pixels))
            {
                result.World = WorldPoints(pixels, rig.Screen.Height, ppp);
            }

            var roots = new List<VisualElement>();
            foreach (var name in PersistentNames)
            {
                var element = name == "overdrive" ? shell.OverdriveButton : shell.Root.Q<VisualElement>(name);
                if (element == null || roots.Contains(element) || roots.Any(existing => existing.Contains(element)))
                {
                    continue;
                }

                roots.Add(element);
                if (IsVisible(element, safe))
                {
                    result.Chrome.Add(new NamedRect(name, element.worldBound));
                }
            }

            result.Texts = CollectTexts(roots, safe);

            var host = shell.PanelHost;
            if (host != null && IsVisible(host, safe))
            {
                result.Sheet = host.worldBound;
            }

            if (overlay != null && IsVisible(overlay, safe))
            {
                result.Overlay = overlay.worldBound;
            }

            return result;
        }

        // ------------------------------------------------------------------------------------------------------
        // JSON (deterministic: fixed key order, invariant culture, fixed rounding)
        // ------------------------------------------------------------------------------------------------------

        public static string Num(double value, int digits = 4) =>
            Math.Round(value, digits, MidpointRounding.AwayFromZero).ToString("0." + new string('#', digits), CultureInfo.InvariantCulture);

        public static string Quote(string text)
        {
            var builder = new StringBuilder("\"");
            foreach (var c in text ?? string.Empty)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            return builder.Append('"').ToString();
        }

        public static string RectJson(Rect rect) =>
            "{\"x\": " + Num(rect.x, 2) + ", \"y\": " + Num(rect.y, 2) + ", \"w\": " + Num(rect.width, 2) + ", \"h\": " + Num(rect.height, 2) + "}";

        public static string ToJson(ShellGeometryResult result, string indent)
        {
            var i1 = indent + "  ";
            var i2 = i1 + "  ";
            var builder = new StringBuilder();
            builder.Append(indent).Append("{\n");
            builder.Append(i1).Append("\"state\": ").Append(Quote(result.State)).Append(",\n");
            builder.Append(i1).Append("\"safeArea\": ").Append(RectJson(result.Safe)).Append(",\n");
            builder.Append(i1).Append("\"world\": ").Append(result.World.HasValue ? RectJson(result.World.Value) : "null").Append(",\n");
            builder.Append(i1).Append("\"worldShare\": ").Append(Num(result.WorldShare)).Append(",\n");
            builder.Append(i1).Append("\"chromeShare\": ").Append(Num(result.ChromeShare)).Append(",\n");
            builder.Append(i1).Append("\"chrome\": [");
            for (var c = 0; c < result.Chrome.Count; c++)
            {
                var item = result.Chrome[c];
                builder.Append(c == 0 ? "\n" : ",\n").Append(i2).Append("{\"name\": ").Append(Quote(item.Name)).Append(", \"rect\": ").Append(RectJson(item.Rect))
                    .Append(", \"share\": ").Append(Num(Share(IntersectionArea(item.Rect, result.Safe), result.SafeArea))).Append('}');
            }

            builder.Append(result.Chrome.Count == 0 ? "],\n" : "\n" + i1 + "],\n");
            builder.Append(i1).Append("\"sheet\": ").Append(result.Sheet.HasValue ? RectJson(result.Sheet.Value) : "null").Append(",\n");
            builder.Append(i1).Append("\"sheetShare\": ").Append(Num(result.SheetShare)).Append(",\n");
            builder.Append(i1).Append("\"overlay\": ").Append(result.Overlay.HasValue ? RectJson(result.Overlay.Value) : "null").Append(",\n");
            builder.Append(i1).Append("\"overlayShare\": ").Append(Num(result.OverlayShare)).Append(",\n");
            builder.Append(i1).Append("\"labelCount\": ").Append(result.LabelCount).Append(",\n");
            builder.Append(i1).Append("\"textElementCount\": ").Append(result.TextElementCount).Append(",\n");
            builder.Append(i1).Append("\"labelsByOwner\": {");
            var owners = result.Texts.Where(item => item.IsLabel).GroupBy(item => item.Owner).ToList();
            for (var o = 0; o < owners.Count; o++)
            {
                builder.Append(o == 0 ? "" : ", ").Append(Quote(owners[o].Key)).Append(": ").Append(owners[o].Count());
            }

            builder.Append("},\n");
            builder.Append(i1).Append("\"texts\": [");
            for (var t = 0; t < result.Texts.Count; t++)
            {
                var item = result.Texts[t];
                builder.Append(t == 0 ? "\n" : ",\n").Append(i2).Append("{\"owner\": ").Append(Quote(item.Owner)).Append(", \"kind\": ").Append(Quote(item.IsLabel ? "label" : "button"))
                    .Append(", \"text\": ").Append(Quote(item.Text)).Append('}');
            }

            builder.Append(result.Texts.Count == 0 ? "],\n" : "\n" + i1 + "],\n");
            builder.Append(i1).Append("\"notes\": {");
            var first = true;
            foreach (var note in result.Notes)
            {
                builder.Append(first ? "" : ", ").Append(Quote(note.Key)).Append(": ").Append(Num(note.Value));
                first = false;
            }

            builder.Append("}\n");
            builder.Append(indent).Append('}');
            return builder.ToString();
        }
    }
}
#endif
