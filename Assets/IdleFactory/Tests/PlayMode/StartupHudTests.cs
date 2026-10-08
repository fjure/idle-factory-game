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
    /// The start-up status HUD that remains after T119 retired the Feature 001 HUD panels (placement actions, machine
    /// inspector, Save and return). It reports a start-up failure with its coded status and classifies screens for the
    /// run shell. These cases were kept from the retired <c>NavigationPlacementTests</c>.
    /// </summary>
    public sealed class StartupHudTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<Object> transientObjects = new List<Object>();

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
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            objects.Clear();
            transientObjects.Clear();
        }

        [UnityTest]
        public IEnumerator HudUsesAttachedAssetsResponsiveClassesAndTextOnlyStatesWithoutGameplayControls()
        {
            var setup = CreateHud();
            yield return null;
            yield return null;

            Assert.That(setup.Hud.IsReady, Is.True, setup.Hud.Error);
            Assert.That(setup.Hud.Root.panel, Is.Not.Null);
            Assert.That(setup.Document.rootVisualElement.Q<VisualElement>("factory-hud"), Is.SameAs(setup.Hud.Root));
            Assert.That(setup.Hud.Root.styleSheets.Contains(setup.StyleSheet), Is.True);
            Assert.That(setup.Hud.Root.Query<Button>().ToList(), Is.Empty,
                "The Feature 001 placement, inspector and persistence controls are retired; the run shell owns gameplay.");

            var layouts = new[]
            {
                (360, 640, FactoryHudLayout.CompactPortrait, "factory-hud--compact-portrait"),
                (640, 360, FactoryHudLayout.CompactLandscape, "factory-hud--compact-landscape"),
                (390, 844, FactoryHudLayout.CompactPortrait, "factory-hud--compact-portrait"),
                (844, 390, FactoryHudLayout.CompactLandscape, "factory-hud--compact-landscape"),
                (768, 1024, FactoryHudLayout.TabletPortrait, "factory-hud--tablet-portrait"),
                (1024, 768, FactoryHudLayout.TabletLandscape, "factory-hud--tablet-landscape"),
                (1280, 720, FactoryHudLayout.Desktop, "factory-hud--desktop"),
                (1920, 1080, FactoryHudLayout.Desktop, "factory-hud--desktop"),
            };
            foreach (var item in layouts)
            {
                var simulatedSafeArea = new Rect(17f, 23f, item.Item1 - 42f, item.Item2 - 62f);
                setup.Hud.ApplyLayout(item.Item1, item.Item2, simulatedSafeArea);
                yield return null;
                Assert.That(setup.Hud.Layout, Is.EqualTo(item.Item3));
                Assert.That(setup.Hud.Root.ClassListContains(item.Item4), Is.True);
                Assert.That(setup.Hud.Root.resolvedStyle.paddingLeft, Is.EqualTo(17f).Within(0.01f));
                Assert.That(setup.Hud.Root.resolvedStyle.paddingRight, Is.EqualTo(25f).Within(0.01f));
                Assert.That(setup.Hud.Root.resolvedStyle.paddingTop, Is.EqualTo(39f).Within(0.01f));
                Assert.That(setup.Hud.Root.resolvedStyle.paddingBottom, Is.EqualTo(23f).Within(0.01f));
            }

            setup.Hud.SetState(FactoryHudState.Error, "content.empty: Packaged gameplay content is empty.");
            Assert.That(setup.Hud.State, Is.EqualTo(FactoryHudState.Error));
            Assert.That(setup.Hud.Root.ClassListContains("factory-hud--error"), Is.True);
            Assert.That(setup.Hud.Root.Q<Label>("status").text, Does.StartWith("content.empty:"),
                "The error state must be announced in text without relying on color.");
            Assert.That(setup.Hud.Root.Q<Label>("feedback").style.display.value, Is.EqualTo(DisplayStyle.None),
                "A state message must be drawn once, on the status line, not repeated by the feedback label.");

            setup.Hud.SetState(FactoryHudState.Normal, "Iron factory run is ready.");
            Assert.That(setup.Hud.State, Is.EqualTo(FactoryHudState.Normal));
            Assert.That(setup.Hud.Root.ClassListContains("factory-hud--error"), Is.False);
            Assert.That(setup.Hud.FeedbackText, Is.EqualTo("Iron factory run is ready."));

            setup.Hud.SetDisplayed(false);
            Assert.That(setup.Hud.Root.style.display.value, Is.EqualTo(DisplayStyle.None));
            setup.Hud.SetDisplayed(true);
            Assert.That(setup.Hud.Root.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            Assert.That(setup.Hud.Configure(setup.Document, setup.Tree, setup.StyleSheet), Is.True,
                "A rebuild replaces the HUD instead of duplicating it.");
            yield return null;
            Assert.That(setup.Document.rootVisualElement.Query<VisualElement>("factory-hud").ToList(), Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator HudMissingAssetsProducesAttachedDisabledErrorState()
        {
            var setup = CreateHud();
            Assert.That(setup.Hud.Configure(setup.Document, null, setup.StyleSheet), Is.False);
            yield return null;

            Assert.That(setup.Hud.State, Is.EqualTo(FactoryHudState.Error));
            Assert.That(setup.Hud.IsReady, Is.False);
            Assert.That(setup.Hud.Root.panel, Is.Not.Null);
            Assert.That(setup.Hud.Root.enabledSelf, Is.False);
            Assert.That(setup.Hud.FeedbackText, Does.Contain("explicit UIDocument, UXML, and USS"));
        }

        private HudSetup CreateHud()
        {
            var gameObject = new GameObject("startup-hud");
            objects.Add(gameObject);
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            transientObjects.Add(panelSettings);
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var hud = gameObject.AddComponent<FactoryHudDocument>();
            var tree = LoadAsset<VisualTreeAsset>("Assets/IdleFactory/Unity/UI/CoreGameFoundation.uxml");
            var styles = LoadAsset<StyleSheet>("Assets/IdleFactory/Unity/UI/CoreGameFoundation.uss");
            Assert.That(hud.Configure(document, tree, styles), Is.True, hud.Error);
            return new HudSetup(hud, document, tree, styles);
        }

        private static T LoadAsset<T>(string path) where T : Object
        {
            var editorType = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            if (editorType == null)
            {
                Assert.Ignore("The HUD assets are loaded through the Editor asset database.");
            }

            var loadMethod = editorType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "LoadAssetAtPath" && method.IsGenericMethodDefinition);
            var asset = (T)loadMethod.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { path });
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        private readonly struct HudSetup
        {
            public HudSetup(FactoryHudDocument hud, UIDocument document, VisualTreeAsset tree, StyleSheet styleSheet)
            {
                Hud = hud;
                Document = document;
                Tree = tree;
                StyleSheet = styleSheet;
            }

            public FactoryHudDocument Hud { get; }
            public UIDocument Document { get; }
            public VisualTreeAsset Tree { get; }
            public StyleSheet StyleSheet { get; }
        }
    }
}
