using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// The layout assertions of the run shell (extending the T053/T059 patterns of <c>HudLayoutOverlapTests</c>): no overlap,
    /// clipping contained, text not squeezed, 44 pt targets, and a focused control that is visible.
    /// </summary>
    public static class RunShellLayoutAssertions
    {
        public const float MinimumTarget = 44f;
        public const float Tolerance = 0.5f;

        /// <summary>The smallest text of the shell at 100 % (Art Bible 9.3): 12 pt; it scales with the text scale.</summary>
        public const float MinimumFontSize = 12f;

        public static bool IsDisplayed(VisualElement element, VisualElement stop = null)
        {
            for (var current = element; current != null && current != stop; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool Intersects(Rect a, Rect b) =>
            a.xMin < b.xMax - Tolerance && b.xMin < a.xMax - Tolerance &&
            a.yMin < b.yMax - Tolerance && b.yMin < a.yMax - Tolerance;

        public static void AssertWithin(Rect outer, Rect inner, string message)
        {
            var detail = $" (inner={inner}, outer={outer})";
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - Tolerance), message + " (left)" + detail);
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - Tolerance), message + " (top)" + detail);
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + Tolerance), message + " (right)" + detail);
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + Tolerance), message + " (bottom)" + detail);
        }

        public static string Describe(VisualElement element) =>
            element is TextElement text ? $"{element.name}: {text.text}" : (string.IsNullOrEmpty(element.name) ? element.GetType().Name : element.name);

        /// <summary>The shell regions (top strip, panel host, tab bar) stay in the safe area, do not overlap and keep their content inside.</summary>
        public static void AssertShellLayout(ShellRig rig, string state)
        {
            var shell = rig.Shell;
            var screen = rig.Screen;
            var context = $"{screen} [{state}] @{rig.TextScalePercent}% {rig.Profile}";
            Assert.That(shell.Layout, Is.EqualTo(screen.Expected), $"{context}: screen class");
            var safe = rig.SafeRect();

            var regions = new List<(string Name, VisualElement Element)> { ("Top strip", shell.TopStrip) };
            var hostDisplayed = IsDisplayed(shell.PanelHost);
            if (hostDisplayed)
            {
                regions.Add(("Panel host", shell.PanelHost));
            }

            if (!shell.IsSidePanelLayout)
            {
                regions.Add(("Tab bar", shell.TabBar));
            }

            foreach (var region in regions)
            {
                Assert.That(region.Element.worldBound.height, Is.GreaterThan(0f), $"{context}: {region.Name} is collapsed");
                AssertWithin(safe, region.Element.worldBound, $"{context}: {region.Name} must stay inside the safe area");
            }

            for (var i = 0; i < regions.Count; i++)
            {
                for (var j = i + 1; j < regions.Count; j++)
                {
                    Assert.That(Intersects(regions[i].Element.worldBound, regions[j].Element.worldBound), Is.False,
                        $"{context}: {regions[i].Name} {regions[i].Element.worldBound} overlaps {regions[j].Name} {regions[j].Element.worldBound}");
                }
            }

            if (shell.IsSidePanelLayout)
            {
                AssertWithin(shell.PanelHost.worldBound, shell.TabBar.worldBound, $"{context}: the tab bar belongs to the side panel");
            }

            // The world column keeps the strip, the notice, the detail line and the zoom controls apart and inside itself.
            var world = shell.WorldColumn.worldBound;
            var worldItems = new List<(string Name, VisualElement Element)>();
            if (shell.IsSidePanelLayout)
            {
                // On tablet and desktop the strip sits above the world beside the side panel; on a phone it spans the width above both.
                worldItems.Add(("Top strip", shell.TopStrip));
            }

            foreach (var name in new[] { "detail-line", "notice", "world-hint", "zoom-column" })
            {
                var element = shell.Root.Q<VisualElement>(name);
                if (IsDisplayed(element))
                {
                    worldItems.Add((name, element));
                }
            }

            foreach (var item in worldItems)
            {
                AssertWithin(world, item.Element.worldBound, $"{context}: {item.Name} stays inside the world column");
            }

            for (var i = 0; i < worldItems.Count; i++)
            {
                for (var j = i + 1; j < worldItems.Count; j++)
                {
                    Assert.That(Intersects(worldItems[i].Element.worldBound, worldItems[j].Element.worldBound), Is.False,
                        $"{context}: {worldItems[i].Name} overlaps {worldItems[j].Name}");
                }
            }

            AssertTextAndTargets(shell.Root, safe, context, MinimumFontSize * rig.TextScalePercent / 100f);
        }

        /// <summary>Every displayed text fits its element (wrapped text keeps its height, single-line text its width) and every control is at least 44 x 44 points.</summary>
        public static void AssertTextAndTargets(VisualElement root, Rect safe, string context, float minimumFontSize = MinimumFontSize)
        {
            foreach (var text in root.Query<TextElement>().ToList()
                         .Where(item => !string.IsNullOrEmpty(item.text) && IsDisplayed(item)))
            {
                if (!IsInsideDisplayedScrollContent(text))
                {
                    AssertWithin(safe, text.worldBound, $"{context}: text '{Describe(text)}' must stay inside the safe area");
                }

                Assert.That(text.resolvedStyle.fontSize, Is.GreaterThanOrEqualTo(minimumFontSize - 0.01f),
                    $"{context}: text '{Describe(text)}' is below the {minimumFontSize:0.#} pt minimum ({text.resolvedStyle.fontSize:0.#})");

                var measured = text.MeasureTextSize(text.text, text.contentRect.width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined);
                Assert.That(text.contentRect.height, Is.GreaterThanOrEqualTo(measured.y - 1f),
                    $"{context}: '{Describe(text)}' is squeezed ({text.contentRect.height:0.#} < {measured.y:0.#}) so its text overflows");

                if (text.resolvedStyle.whiteSpace == WhiteSpace.NoWrap && !(text is Button))
                {
                    var natural = text.MeasureTextSize(text.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
                    Assert.That(text.contentRect.width, Is.GreaterThanOrEqualTo(natural.x - 1f),
                        $"{context}: '{Describe(text)}' is clipped horizontally ({text.contentRect.width:0.#} < {natural.x:0.#})");
                }
            }

            foreach (var button in root.Query<Button>(className: "run-control").ToList().Where(item => IsDisplayed(item)))
            {
                Assert.That(button.worldBound.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{context}: '{button.name}' width {button.worldBound.width}");
                Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{context}: '{button.name}' height {button.worldBound.height}");
                foreach (var child in button.Query<VisualElement>().Where(item => item != button && IsDisplayed(item)).ToList())
                {
                    AssertWithin(button.worldBound, child.worldBound, $"{context}: '{Describe(child)}' stays inside '{button.name}'");
                }
            }
        }

        private static bool IsInsideDisplayedScrollContent(VisualElement element)
        {
            for (var current = element.parent; current != null; current = current.parent)
            {
                if (current is ScrollView)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A control scrolled out of the sheet must not draw over, or steal taps from, the rest of the shell: hit-testing follows the
        /// clip rectangles, so a picked element outside the viewport proves the content escapes it.
        /// </summary>
        public static void AssertScrollContentClipped(ScrollView scroll, string context)
        {
            var panel = scroll.panel;
            var viewport = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport").worldBound;
            foreach (var element in scroll.contentContainer.Query<VisualElement>().ToList()
                         .Where(item => (item is Label || item is Button) && IsDisplayed(item)))
            {
                var center = element.worldBound.center;
                if (viewport.Contains(center))
                {
                    continue;
                }

                var picked = panel.Pick(center);
                Assert.That(picked != null && (picked == element || element.Contains(picked)), Is.False,
                    $"{context}: '{Describe(element)}' at {element.worldBound} escapes the viewport {viewport}");
            }
        }

        public static string Dump(ShellRig rig)
        {
            var shell = rig.Shell;
            return $"panel={shell.Root.panel.visualTree.worldBound} strip={shell.TopStrip.worldBound} host={shell.PanelHost.worldBound} tabs={shell.TabBar.worldBound} world={shell.WorldColumn.worldBound}";
        }
    }
}
