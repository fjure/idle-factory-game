#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>One displayed restyled icon found in a UI tree.</summary>
    public sealed class UiIconHit
    {
        public VisualElement Element;
        public string Key;

        /// <summary>The icon box in panel points (the element's box, or the background-size box at its top-left for an icon beside text).</summary>
        public Rect IconRect;

        public string Describe() => Key + " on '" + (string.IsNullOrEmpty(Element.name) ? Element.GetType().Name : Element.name) + "'";
    }

    /// <summary>T441: finds the icons of a UI tree and checks them in context (bounds, one style, tinted contrast).</summary>
    public static class UiIconScan
    {
        public const float Tolerance = 1f;

        /// <summary>The ui_text glyph colour of every icon file (Art Bible 9.4: the base before the token tint).</summary>
        public static readonly Color32 Glyph = new Color32(242, 245, 250, 255);

        /// <summary>WCAG 2.x contrast ratio for a non-text graphic (Art Bible section 9: 3:1).</summary>
        public const float NonTextContrast = 3f;

        /// <summary>The shell token behind an element without an opaque ancestor.</summary>
        public static readonly Color32 ShellBackground = new Color32(0x11, 0x18, 0x27, 255);

        /// <summary>All displayed elements below <paramref name="root"/> that carry a restyled icon (or a placeholder, when asked).</summary>
        public static List<UiIconHit> Scan(VisualElement root, bool includePlaceholders = false)
        {
            var hits = new List<UiIconHit>();
            foreach (var element in root.Query<VisualElement>().ToList())
            {
                var sprite = element.resolvedStyle.backgroundImage.sprite;
                if (sprite == null || !IsDisplayed(element))
                {
                    continue;
                }

                var key = UiIconSprites.KeyOf(sprite);
                if (key == null && !(includePlaceholders && UiIconSprites.IsPlaceholder(sprite)))
                {
                    continue;
                }

                var iconRect = IconRectOf(element);
                if (!IsInsideScrollViewport(element, iconRect))
                {
                    continue;
                }

                hits.Add(new UiIconHit { Element = element, Key = key ?? "PLACEHOLDER:" + sprite.name, IconRect = iconRect });
            }

            return hits;
        }

        public static Rect IconRectOf(VisualElement element)
        {
            var box = element.worldBound;
            if (Mathf.Abs(element.resolvedStyle.rotate.angle.value) > 0.01f)
            {
                // A turned element (the spinner) reports the bounds of its turned box; the icon box is the unturned layout box about its centre.
                var layoutSize = element.layout.size;
                return new Rect(box.center.x - layoutSize.x / 2f, box.center.y - layoutSize.y / 2f, layoutSize.x, layoutSize.y);
            }

            var size = element.resolvedStyle.backgroundSize;
            if (size.sizeType == BackgroundSizeType.Length && size.x.unit == LengthUnit.Pixel && size.x.value > 0f)
            {
                // An icon beside text or on a button: a fixed-size background placed by the resolved position.
                var x = Offset(element.resolvedStyle.backgroundPositionX, box.width, size.x.value);
                var y = Offset(element.resolvedStyle.backgroundPositionY, box.height, size.y.value);
                return new Rect(box.xMin + x, box.yMin + y, size.x.value, size.y.value);
            }

            return box;
        }

        private static float Offset(BackgroundPosition position, float available, float size)
        {
            var offset = position.offset.unit == LengthUnit.Pixel ? position.offset.value : 0f;
            switch (position.keyword)
            {
                case BackgroundPositionKeyword.Right:
                case BackgroundPositionKeyword.Bottom:
                    return available - size - offset;
                case BackgroundPositionKeyword.Center:
                    return (available - size) / 2f + offset;
                default:
                    return offset;
            }
        }

        public static bool IsDisplayed(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None || current.resolvedStyle.visibility == Visibility.Hidden)
                {
                    return false;
                }
            }

            return element.worldBound.width > 0f && element.worldBound.height > 0f;
        }

        // An icon a scroll view has scrolled out of (or half out of) its viewport, on either axis (the research graph also scrolls sideways by
        // design), is the scroll view's clipping, not a layout fault: it is skipped. Every scroll view above the icon counts.
        private static bool IsInsideScrollViewport(VisualElement element, Rect iconRect)
        {
            for (var parent = element.parent; parent != null; parent = parent.parent)
            {
                if (parent is ScrollView scroll)
                {
                    var viewport = scroll.contentViewport.worldBound;
                    if (iconRect.yMin < viewport.yMin - Tolerance || iconRect.yMax > viewport.yMax + Tolerance
                        || iconRect.xMin < viewport.xMin - Tolerance || iconRect.xMax > viewport.xMax + Tolerance)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>The icon box lies inside its element, its parent and the panel; its size is a 24 pt square (16 to 64 allowed for a scaled panel).</summary>
        public static void AssertInBounds(IEnumerable<UiIconHit> hits, Rect panelBounds, string context)
        {
            foreach (var hit in hits)
            {
                var rect = hit.IconRect;
                var label = context + ": " + hit.Describe();
                // The pin hint is the one deliberate small glyph: an 8 pt corner mark that takes no layout space (F003 UX, RunHudLayoutTests F5).
                var smallest = hit.Element.ClassListContains("run-pin-hint") ? 8f : 16f;
                Assert.That(rect.width, Is.InRange(smallest - Tolerance, 64f + Tolerance), label + " width " + rect.width);
                Assert.That(rect.height, Is.InRange(smallest - Tolerance, 64f + Tolerance), label + " height " + rect.height);
                AssertInside(panelBounds, rect, label + " stays on the panel");
                AssertInside(hit.Element.worldBound, rect, label + " stays in its element");
                var parent = hit.Element.parent;
                if (parent != null)
                {
                    AssertInside(parent.worldBound, rect, label + " stays in its parent");
                }
            }
        }

        private static void AssertInside(Rect outer, Rect inner, string message)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - Tolerance), message + " (left)");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + Tolerance), message + " (right)");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - Tolerance), message + " (top)");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + Tolerance), message + " (bottom)");
        }

        // ---- contrast after tint ---------------------------------------------------------------------------------------

        public static float RelativeLuminance(Color32 color)
        {
            float Channel(byte value)
            {
                var c = value / 255f;
                return c <= 0.03928f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
            }

            return 0.2126f * Channel(color.r) + 0.7152f * Channel(color.g) + 0.0722f * Channel(color.b);
        }

        public static float Contrast(Color32 a, Color32 b)
        {
            var la = RelativeLuminance(a);
            var lb = RelativeLuminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        /// <summary>The colour the glyph shows once the tint multiplies it (UI Toolkit multiplies the texture colour by the tint).</summary>
        public static Color32 Tinted(Color32 glyph, Color tint)
        {
            return new Color32(
                (byte)Mathf.RoundToInt(glyph.r * tint.r), (byte)Mathf.RoundToInt(glyph.g * tint.g), (byte)Mathf.RoundToInt(glyph.b * tint.b), 255);
        }

        /// <summary>The opaque background colour behind an element: the nearest ancestor with an opaque background colour.</summary>
        public static Color32 BackdropOf(VisualElement element)
        {
            for (var current = element.parent; current != null; current = current.parent)
            {
                var color = current.resolvedStyle.backgroundColor;
                if (color.a >= 0.99f)
                {
                    return (Color32)color;
                }
            }

            return ShellBackground;
        }

        /// <summary>Each icon's glyph x tint against its backdrop is at least 3:1 (non-text); returns the measured rows.</summary>
        public static List<string> AssertTintedContrast(IEnumerable<UiIconHit> hits, string context)
        {
            var rows = new List<string>();
            foreach (var hit in hits)
            {
                var element = hit.Element;
                // An icon on its own coloured element (a primary button) is seen against that element.
                var own = element.resolvedStyle.backgroundColor;
                var backdrop = own.a >= 0.99f ? (Color32)own : BackdropOf(element);
                var effective = Tinted(Glyph, element.resolvedStyle.unityBackgroundImageTintColor);
                var ratio = Contrast(effective, backdrop);
                rows.Add($"{context}|{hit.Key}|{effective.r:X2}{effective.g:X2}{effective.b:X2}|{backdrop.r:X2}{backdrop.g:X2}{backdrop.b:X2}|{ratio:0.00}");
                Assert.That(ratio, Is.GreaterThanOrEqualTo(NonTextContrast), $"{context}: {hit.Describe()} tinted contrast {ratio:0.00}");
            }

            return rows;
        }
    }
}
#endif
