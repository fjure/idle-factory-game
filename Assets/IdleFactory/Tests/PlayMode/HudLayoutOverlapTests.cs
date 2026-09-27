using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T053: renders the authored HUD into an off-screen panel with a real pixel size and pixel-to-point
    /// scale per declared screen class, feeds it screen-pixel dimensions and a screen-pixel safe area exactly
    /// as <c>CoreGameFoundationBootstrap.ApplySafeAreaLayout</c> does, and asserts that no HUD panel or its
    /// content is drawn on top of another panel.
    /// </summary>
    public sealed class HudLayoutOverlapTests
    {
        private const float MinimumTarget = 44f;
        private const float Tolerance = 0.5f;

        // sw411dp reference phone (emulator AVD Medium_Phone_API_36.0): 1080x2400 px at 420 dpi. 420/96 px per
        // point is the physical-size bound (reference 96 dpi); the authored panel settings scale with the screen
        // size instead (see AuthoredScaleWithScreenSize), so both scales are covered.
        private const float PhoneScale = 420f / 96f;

        private const float EmulatorObservedScale = 2.09f;

        // The authored panel settings scale with the screen size (reference 1280x720, match 0.5):
        // 2^((log2(2400/1280) + log2(1080/720)) / 2) ~= 1.677 px per point in either phone orientation.
        private const float AuthoredScaleWithScreenSize = 1.677f;

        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<Object> transientObjects = new List<Object>();

        public sealed class ScreenClassCase
        {
            public ScreenClassCase(
                string name,
                int pixelWidth,
                int pixelHeight,
                float pixelsPerPoint,
                RectOffset insetsPx,
                FactoryHudLayout expected)
            {
                Name = name;
                PixelWidth = pixelWidth;
                PixelHeight = pixelHeight;
                PixelsPerPoint = pixelsPerPoint;
                InsetsPx = insetsPx;
                Expected = expected;
            }

            public string Name { get; }
            public int PixelWidth { get; }
            public int PixelHeight { get; }
            public float PixelsPerPoint { get; }
            public RectOffset InsetsPx { get; }
            public FactoryHudLayout Expected { get; }

            /// <summary>Screen.safeArea convention: origin bottom-left, in pixels.</summary>
            public Rect SafeAreaPx => new Rect(
                InsetsPx.left,
                InsetsPx.bottom,
                PixelWidth - InsetsPx.left - InsetsPx.right,
                PixelHeight - InsetsPx.top - InsetsPx.bottom);

            public override string ToString() => Name;
        }

        // T036 matrix (1 px per point, asymmetric 17/25/39/23 safe area) plus the sw411dp phone in both
        // orientations with its status-bar/cutout and gesture-navigation insets.
        public static IEnumerable<ScreenClassCase> ScreenClasses()
        {
            var t036 = new RectOffset(17, 25, 39, 23);
            yield return new ScreenClassCase("phone-portrait sw411dp 1080x2400@420dpi", 1080, 2400, PhoneScale,
                new RectOffset(0, 0, 136, 63), FactoryHudLayout.CompactPortrait);
            yield return new ScreenClassCase("phone-landscape sw411dp 2400x1080@420dpi", 2400, 1080, PhoneScale,
                new RectOffset(136, 63, 0, 0), FactoryHudLayout.CompactLandscape);
            // The GLES3 emulator player resolved about 2.09 px per point (460-point panel = ~961 px).
            yield return new ScreenClassCase("phone-portrait sw411dp 1080x2400 emulator-observed 2.09px/pt", 1080, 2400,
                EmulatorObservedScale, new RectOffset(0, 0, 136, 63), FactoryHudLayout.CompactPortrait);
            yield return new ScreenClassCase("phone-landscape sw411dp 2400x1080 emulator-observed 2.09px/pt", 2400, 1080,
                EmulatorObservedScale, new RectOffset(136, 63, 0, 0), FactoryHudLayout.Desktop); // 1148 pt wide >= T036 desktop threshold
            yield return new ScreenClassCase("phone-portrait sw411dp 1080x2400 authored scale-with-screen-size", 1080, 2400,
                AuthoredScaleWithScreenSize, new RectOffset(0, 0, 136, 63), FactoryHudLayout.CompactPortrait);
            yield return new ScreenClassCase("phone-landscape sw411dp 2400x1080 authored scale-with-screen-size", 2400, 1080,
                AuthoredScaleWithScreenSize, new RectOffset(136, 63, 0, 0), FactoryHudLayout.Desktop);
            yield return new ScreenClassCase("compact-portrait 360x640", 360, 640, 1f, t036, FactoryHudLayout.CompactPortrait);
            yield return new ScreenClassCase("compact-landscape 640x360", 640, 360, 1f, t036, FactoryHudLayout.CompactLandscape);
            yield return new ScreenClassCase("compact-portrait 390x844", 390, 844, 1f, t036, FactoryHudLayout.CompactPortrait);
            yield return new ScreenClassCase("compact-landscape 844x390", 844, 390, 1f, t036, FactoryHudLayout.CompactLandscape);
            yield return new ScreenClassCase("tablet-portrait 768x1024", 768, 1024, 1f, t036, FactoryHudLayout.TabletPortrait);
            yield return new ScreenClassCase("tablet-landscape 1024x768", 1024, 768, 1f, t036, FactoryHudLayout.TabletLandscape);
            yield return new ScreenClassCase("desktop 1280x720", 1280, 720, 1f, t036, FactoryHudLayout.Desktop);
            yield return new ScreenClassCase("desktop 1366x768", 1366, 768, 1f, t036, FactoryHudLayout.Desktop);
            yield return new ScreenClassCase("desktop 1920x1080", 1920, 1080, 1f, t036, FactoryHudLayout.Desktop);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in objects)
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }

            foreach (var item in transientObjects)
            {
                if (item is RenderTexture texture)
                {
                    texture.Release();
                }

                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            objects.Clear();
            transientObjects.Clear();
        }

        [UnityTest]
        public IEnumerator HudPanelsDoNotOverlapAndStayInsideTheSafeArea(
            [ValueSource(nameof(ScreenClasses))] ScreenClassCase screen)
        {
            var hud = CreateHud(screen);
            yield return null;

            hud.ApplyScreenLayout(screen.PixelWidth, screen.PixelHeight, screen.SafeAreaPx);
            yield return null;
            yield return null;

            Assert.That(hud.Layout, Is.EqualTo(screen.Expected), $"{screen}: screen class");
            AssertNoOverlap(hud, screen, "browse mode");

            // Worst-case content: a selected machine with long selection text and a placement rejection.
            hud.Root.Q<Label>("selection").text =
                "Selected dev.machine.d001 (building-0001) | Active | Progress 12/20 | Blockers 1";
            hud.SetPlacementMode(false, "Cannot place: footprint overlaps an existing building.");
            yield return null;
            yield return null;
            AssertNoOverlap(hud, screen, "placement preview mode");

            ShowMaximumContent(hud);
            yield return null;
            yield return null;
            AssertNoOverlap(hud, screen, "maximum content");
            yield return AssertScrollContentReachable(hud, screen, "maximum content");
        }

        /// <summary>
        /// T059 (T058-F1): the longest HUD state after a runtime rotation. The HUD is laid out for the opposite
        /// orientation first, filled with the Machine inspector and the Durable offline return summary, and then
        /// rotated into the declared screen class exactly as the player does on a configuration change.
        /// </summary>
        [UnityTest]
        public IEnumerator MaximumContentStaysReadableAndReachableAfterRuntimeRotation(
            [ValueSource(nameof(RotatedScreenClasses))] ScreenClassCase screen)
        {
            var hud = CreateHud(screen);
            yield return null;

            var rotatedInsets = new RectOffset(screen.InsetsPx.bottom, screen.InsetsPx.top, screen.InsetsPx.left, screen.InsetsPx.right);
            var rotatedSafeArea = new Rect(
                rotatedInsets.left,
                rotatedInsets.bottom,
                screen.PixelHeight - rotatedInsets.left - rotatedInsets.right,
                screen.PixelWidth - rotatedInsets.top - rotatedInsets.bottom);
            hud.ApplyScreenLayout(screen.PixelHeight, screen.PixelWidth, rotatedSafeArea);
            ShowMaximumContent(hud);
            yield return null;
            yield return null;

            hud.ApplyScreenLayout(screen.PixelWidth, screen.PixelHeight, screen.SafeAreaPx);
            yield return null;
            yield return null;

            Assert.That(hud.Layout, Is.EqualTo(screen.Expected), $"{screen}: screen class after rotation");
            AssertNoOverlap(hud, screen, "maximum content after rotation");
            yield return AssertScrollContentReachable(hud, screen, "maximum content after rotation");
        }

        // The render texture keeps the target size; only the layout inputs rotate, as on a configuration change.
        public static IEnumerable<ScreenClassCase> RotatedScreenClasses() =>
            ScreenClasses().Where(screen => screen.Name.StartsWith("phone-", StringComparison.Ordinal));

        /// <summary>
        /// The longest state the HUD shows at once: a selected, blocked machine with long identifiers and
        /// blockers in the inspector, a placement rejection, and a Durable offline return summary with
        /// resource changes and limitation reasons.
        /// </summary>
        private static void ShowMaximumContent(FactoryHudDocument hud)
        {
            var root = hud.Root;
            const string instance = "dev.machine.basic · instance 49308324-83f7-43d9-9f43-6d8e799add70";
            root.Q<Label>("selection").text =
                $"Selected {instance} | Active | Progress 0.00/5.00 s | Blockers 1";
            root.Q<Label>("feedback").text = "Cannot place: footprint overlaps an existing building.";
            root.Q<VisualElement>("inspector-empty").style.display = DisplayStyle.None;
            root.Q<VisualElement>("inspector-selected").style.display = DisplayStyle.Flex;
            root.Q<Label>("inspector-machine").text = $"Machine {instance}";
            root.Q<Label>("inspector-state").text = "State: Blocked";
            root.Q<Label>("inspector-process").text = "Process: dev.recipe.basic";
            root.Q<Label>("inspector-progress").text = "Progress: 0.00 / 5.00 s (0%).";
            root.Q<Label>("inspector-input-buffer").text =
                "This machine's input buffer — Input: 2 / 4 stored · recipe needs 2";
            root.Q<Label>("inspector-output-buffer").text =
                "This machine's output buffer — Output: 1 / 1 stored · 0 free";
            root.Q<Label>("inspector-blockers").text = "Blockers:\n• Output full Output: need 1 free, have 0.";
            root.Q<Label>("inspector-feedback").text = "No recent command for this machine.";
            root.Q<Label>("persistence-status").text = "Status: READY — a saved factory is available.";
            root.Q<Label>("persistence-feedback").text = "No recent persistence action.";
            var summary = root.Q<Label>("persistence-summary");
            summary.text =
                "Durable offline return\nMeasured: 0.04 s\nCredited: 0.04 s\nEffective: 0.03 s\n" +
                $"Retained production progress: {instance} — 0 s of 5 s\n" +
                "Actual resource changes: none\n" +
                $"Limiting reasons:\n• {instance}: Output full Output: need 1 free, have 0.\n" +
                "• The offline credit is capped at the configured maximum offline duration.";
            summary.style.display = DisplayStyle.Flex;

            // READY enables the checkpoint actions, as PersistencePresenter does.
            foreach (var name in new[] { "persistence-save", "persistence-load", "persistence-reset" })
            {
                root.Q<Button>(name).SetEnabled(true);
            }
        }

        /// <summary>
        /// T059 (T058-F1): the inspector scroll viewport must clip its content. Without clipping, scrolled
        /// inspector and Save-and-return content is drawn over the Browse-mode header and below the viewport,
        /// where it also steals taps. Hit-testing follows the same clip rectangles as rendering, so a picked
        /// element outside the viewport proves the content escapes it.
        /// </summary>
        private static void AssertScrollContentClipped(FactoryHudDocument hud, string context)
        {
            var root = hud.Root;
            var panel = root.panel;
            var scroll = root.Q<ScrollView>("inspector-scroll");
            var viewport = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport").worldBound;
            var information = root.Q<VisualElement>("information-panel");

            foreach (var label in information.Query<Label>().ToList().Where(item => IsDisplayed(item, null)))
            {
                var picked = panel.Pick(label.worldBound.center);
                Assert.That(picked != null && (picked == information || information.Contains(picked)), Is.True,
                    $"{context}: '{Describe(label)}' is covered by '{(picked == null ? "nothing" : Describe(picked))}'.");
            }

            foreach (var element in scroll.contentContainer.Query<VisualElement>().ToList()
                         .Where(item => (item is Label || item is Button) && IsDisplayed(item, null)))
            {
                var center = element.worldBound.center;
                if (viewport.Contains(center))
                {
                    continue;
                }

                var picked = panel.Pick(center);
                Assert.That(picked != null && (picked == element || element.Contains(picked)), Is.False,
                    $"{context}: '{Describe(element)}' at {element.worldBound} escapes the inspector viewport {viewport}.");
            }
        }

        /// <summary>
        /// Clipped scroll content is acceptable only while it can be scrolled into the viewport: scrolling to
        /// the end must bring the last action and the last summary line fully inside the visible viewport.
        /// </summary>
        private static IEnumerator AssertScrollContentReachable(FactoryHudDocument hud, ScreenClassCase screen, string state)
        {
            var context = $"{screen} [{state}]";
            var scroll = hud.Root.Q<ScrollView>("inspector-scroll");
            var viewport = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport");
            var targets = new VisualElement[]
            {
                hud.Root.Q<Button>("inspector-toggle-active"),
                hud.Root.Q<Button>("persistence-recover"),
                hud.Root.Q<Label>("persistence-summary"),
            };

            var content = scroll.contentContainer;
            foreach (var offset in new[] { 0f, 0.5f, 1f })
            {
                var range = Mathf.Max(0f, content.layout.height - viewport.layout.height);
                scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, range * offset);
                yield return null;
                yield return null;
                AssertScrollContentClipped(hud, $"{context} scrolled to {offset:P0}");
            }

            // Keyboard and gamepad focus must bring a clipped control into view (T036 focus order).
            foreach (var control in new[] { hud.Root.Q<Button>("persistence-reset"), hud.Root.Q<Button>("inspector-supply-input") })
            {
                var edge = control.name.StartsWith("persistence", StringComparison.Ordinal) ? 0f : float.MaxValue;
                scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, edge);
                yield return null;
                yield return null;
                control.Focus();
                yield return null;
                yield return null;
                Assert.That(control.focusController.focusedElement, Is.SameAs(control), $"{context}: '{control.name}' did not take focus.");
                AssertWithin(viewport.worldBound, control.worldBound, $"{context}: focused '{control.name}' is not scrolled into view");
                control.Blur();
            }

            foreach (var target in targets)
            {
                scroll.ScrollTo(target);
                yield return null;
                yield return null;
                var visible = viewport.worldBound;
                var bounds = target.worldBound;
                Assert.That(bounds.height, Is.GreaterThan(0f), $"{context}: '{target.name}' is collapsed.");
                if (bounds.height <= visible.height + Tolerance)
                {
                    AssertWithin(visible, bounds, $"{context}: '{target.name}' cannot be scrolled fully into view");
                }
                else
                {
                    // Taller than the viewport: its end must be reachable by scrolling.
                    scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, float.MaxValue);
                    yield return null;
                    yield return null;
                    Assert.That(target.worldBound.yMax, Is.LessThanOrEqualTo(viewport.worldBound.yMax + Tolerance),
                        $"{context}: the end of '{target.name}' cannot be scrolled into view.");
                }
            }
        }

        private static void AssertNoOverlap(FactoryHudDocument hud, ScreenClassCase screen, string state)
        {
            var root = hud.Root;
            var context = $"{screen} [{state}]";
            var pixelsPerPoint = root.panel.scaledPixelsPerPoint;
            Assert.That(pixelsPerPoint, Is.EqualTo(screen.PixelsPerPoint).Within(0.01f), $"{context}: panel scale");

            var panelBounds = root.panel.visualTree.worldBound;
            var insets = screen.InsetsPx;
            var safeArea = Rect.MinMaxRect(
                panelBounds.xMin + insets.left / pixelsPerPoint,
                panelBounds.yMin + insets.top / pixelsPerPoint,
                panelBounds.xMax - insets.right / pixelsPerPoint,
                panelBounds.yMax - insets.bottom / pixelsPerPoint);

            var information = root.Q<VisualElement>("information-panel");
            var scroll = root.Q<ScrollView>("inspector-scroll");
            var actions = root.Q<VisualElement>("actions");
            var panels = new (string Name, VisualElement Element)[]
            {
                ("Browse-mode information panel", information),
                ("Machine inspector / Save-and-return scroll panel", scroll),
                ("Action bar", actions),
            };

            foreach (var panel in panels)
            {
                Assert.That(panel.Element.worldBound.height, Is.GreaterThan(0f), $"{context}: {panel.Name} is collapsed. {Dump(root)}");
                AssertWithin(safeArea, panel.Element.worldBound, $"{context}: {panel.Name} must stay inside the safe area");
            }

            for (var i = 0; i < panels.Length; i++)
            {
                for (var j = i + 1; j < panels.Length; j++)
                {
                    Assert.That(Intersects(panels[i].Element.worldBound, panels[j].Element.worldBound), Is.False,
                        $"{context}: {panels[i].Name} {panels[i].Element.worldBound} overlaps " +
                        $"{panels[j].Name} {panels[j].Element.worldBound}.");
                }
            }

            // Content must be drawn inside its own panel; spilled text is what draws over neighbouring panels.
            foreach (var child in VisibleDescendants(information))
            {
                AssertWithin(information.worldBound, child.worldBound,
                    $"{context}: '{Describe(child)}' spills out of the information panel");
            }

            foreach (var child in VisibleDescendants(actions))
            {
                AssertWithin(actions.worldBound, child.worldBound,
                    $"{context}: '{Describe(child)}' is clipped by the action bar");
            }

            // A label squeezed below its wrapped text height draws its text over whatever follows it.
            foreach (var text in root.Query<TextElement>().ToList()
                         .Where(item => item is Label || item is Button)
                         .Where(item => !string.IsNullOrEmpty(item.text) && IsDisplayed(item, null)))
            {
                var measured = text.MeasureTextSize(
                    text.text,
                    text.contentRect.width,
                    VisualElement.MeasureMode.Exactly,
                    0f,
                    VisualElement.MeasureMode.Undefined);
                Assert.That(text.contentRect.height, Is.GreaterThanOrEqualTo(measured.y - 1f),
                    $"{context}: '{Describe(text)}' is squeezed ({text.contentRect.height:0.#} < {measured.y:0.#}) " +
                    "so its text overflows onto neighbouring content.");
            }

            // The scroll viewport clips its content; the inspector and persistence cards must not stack.
            var inspector = root.Q<VisualElement>("inspector-panel");
            var persistence = root.Q<VisualElement>("persistence-panel");
            Assert.That(Intersects(inspector.worldBound, persistence.worldBound), Is.False,
                $"{context}: Machine inspector {inspector.worldBound} overlaps Save-and-return {persistence.worldBound}.");
            var viewport = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport");
            Assert.That(viewport.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget),
                $"{context}: the inspector scroll viewport must leave room for at least one touch target.");

            foreach (var button in root.Query<Button>(className: "factory-hud__control").ToList()
                         .Where(item => item.resolvedStyle.display != DisplayStyle.None))
            {
                Assert.That(button.worldBound.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance),
                    $"{context}: '{button.name}' width");
                Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance),
                    $"{context}: '{button.name}' height");
            }
        }

        private static IEnumerable<VisualElement> VisibleDescendants(VisualElement parent)
        {
            return parent.Query<VisualElement>().Where(item =>
                    item != parent &&
                    (item is Label || item is Button) &&
                    IsDisplayed(item, parent))
                .ToList();
        }

        private static bool IsDisplayed(VisualElement element, VisualElement stop)
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

        private static string Dump(VisualElement root)
        {
            var names = new[] { "side-panels", "information-panel", "inspector-scroll", "inspector-panel", "persistence-panel", "actions" };
            return $"root={root.worldBound} panel={root.panel.visualTree.worldBound} " +
                string.Join(" ", names.Select(name => $"{name}={root.Q<VisualElement>(name).worldBound}"));
        }

        private static string Describe(VisualElement element) =>
            element is TextElement text ? $"{element.name}: {text.text}" : element.name;

        private static bool Intersects(Rect a, Rect b) =>
            a.xMin < b.xMax - Tolerance && b.xMin < a.xMax - Tolerance &&
            a.yMin < b.yMax - Tolerance && b.yMin < a.yMax - Tolerance;

        private static void AssertWithin(Rect outer, Rect inner, string message)
        {
            var detail = $" (inner={inner}, outer={outer})";
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - Tolerance), message + " (left)" + detail);
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - Tolerance), message + " (top)" + detail);
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + Tolerance), message + " (right)" + detail);
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + Tolerance), message + " (bottom)" + detail);
        }

        private FactoryHudDocument CreateHud(ScreenClassCase screen)
        {
            var texture = new RenderTexture(screen.PixelWidth, screen.PixelHeight, 0);
            texture.Create();
            transientObjects.Add(texture);

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettings.scale = screen.PixelsPerPoint;
            panelSettings.targetTexture = texture;
            transientObjects.Add(panelSettings);

            var gameObject = new GameObject("hud-layout-overlap");
            objects.Add(gameObject);
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var hud = gameObject.AddComponent<FactoryHudDocument>();
            var tree = LoadAsset<VisualTreeAsset>("Assets/IdleFactory/Unity/UI/CoreGameFoundation.uxml");
            var styles = LoadAsset<StyleSheet>("Assets/IdleFactory/Unity/UI/CoreGameFoundation.uss");
            Assert.That(hud.Configure(document, tree, styles), Is.True, hud.Error);
            return hud;
        }

        private static T LoadAsset<T>(string path) where T : Object
        {
            var editorType = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            Assert.That(editorType, Is.Not.Null);
            var loadMethod = editorType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "LoadAssetAtPath" && method.IsGenericMethodDefinition);
            var asset = (T)loadMethod.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { path });
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }
    }
}
