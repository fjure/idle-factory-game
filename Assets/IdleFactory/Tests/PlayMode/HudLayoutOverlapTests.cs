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

        // sw411dp reference phone (emulator AVD Medium_Phone_API_36.0): 1080x2400 px at 420 dpi, rendered
        // through the authored ConstantPhysicalSize panel settings (reference 96 dpi) => 420/96 px per point.
        private const float PhoneScale = 420f / 96f;

        private const float EmulatorObservedScale = 2.09f;

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
