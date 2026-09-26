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
    /// T054: for every declared screen class, renders the authored HUD and the authored factory camera at the
    /// same pixel size, applies the initial framing, and asserts that the canonical D001 2x2 machine at grid
    /// (0,0) is drawn completely inside the screen area the HUD leaves uncovered.
    /// </summary>
    public sealed class InitialCameraFramingTests
    {
        private const float AuthoredOrthographicSize = 9f;
        private const float Tolerance = 0.5f;
        private static readonly Rect FactoryWorldBounds = new Rect(0f, 0f, 16f, 16f);
        private static readonly Rect D001MachineWorldBounds = new Rect(0f, 0f, 2f, 2f);

        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<Object> transientObjects = new List<Object>();

        public static IEnumerable<HudLayoutOverlapTests.ScreenClassCase> ScreenClasses() =>
            HudLayoutOverlapTests.ScreenClasses();

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
        public IEnumerator InitialFramingShowsTheD001MachineOutsideTheHud(
            [ValueSource(nameof(ScreenClasses))] HudLayoutOverlapTests.ScreenClassCase screen)
        {
            var hud = CreateHud(screen);
            yield return null;

            hud.ApplyScreenLayout(screen.PixelWidth, screen.PixelHeight, screen.SafeAreaPx);
            yield return null;
            yield return null;

            Assert.That(
                hud.TryGetUnobstructedScreenRect(screen.PixelWidth, screen.PixelHeight, screen.SafeAreaPx, out var visibleArea),
                Is.True,
                $"{screen}: the HUD must report the screen area it leaves uncovered once its layout has resolved.");

            var camera = CreateCamera(screen, out var controller);
            controller.SetUnobstructedScreenRect(visibleArea);
            controller.FrameContent(D001MachineWorldBounds);

            Assert.That(camera.orthographicSize, Is.EqualTo(AuthoredOrthographicSize),
                $"{screen}: initial framing must not change the authored zoom.");
            AssertDrawnInside(camera, D001MachineWorldBounds, visibleArea, $"{screen}: D001 machine");
        }

        [UnityTest]
        public IEnumerator PanLimitsLetEveryFactoryCornerReachTheUncoveredArea(
            [ValueSource(nameof(ScreenClasses))] HudLayoutOverlapTests.ScreenClassCase screen)
        {
            var hud = CreateHud(screen);
            yield return null;

            hud.ApplyScreenLayout(screen.PixelWidth, screen.PixelHeight, screen.SafeAreaPx);
            yield return null;
            yield return null;

            Assert.That(
                hud.TryGetUnobstructedScreenRect(screen.PixelWidth, screen.PixelHeight, screen.SafeAreaPx, out var visibleArea),
                Is.True);
            var camera = CreateCamera(screen, out var controller);
            controller.SetUnobstructedScreenRect(visibleArea);

            var last = FactoryWorldBounds.width - 1f;
            foreach (var corner in new[] { Vector2.zero, new Vector2(last, 0f), new Vector2(0f, last), new Vector2(last, last) })
            {
                var cell = new Rect(corner, Vector2.one);
                controller.FrameContent(cell);
                AssertDrawnInside(camera, cell, visibleArea, $"{screen}: corner cell {corner}");
            }
        }

        private static void AssertDrawnInside(Camera camera, Rect worldRect, Rect screenArea, string context)
        {
            var bottomLeft = camera.WorldToScreenPoint(new Vector3(worldRect.xMin, worldRect.yMin, 0f));
            var topRight = camera.WorldToScreenPoint(new Vector3(worldRect.xMax, worldRect.yMax, 0f));
            var onScreen = Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
            var detail = $" (drawn={onScreen}, uncovered={screenArea}, camera={camera.transform.position}, " +
                $"size={camera.orthographicSize})";

            Assert.That(onScreen.xMin, Is.GreaterThanOrEqualTo(screenArea.xMin - Tolerance), $"{context}: left" + detail);
            Assert.That(onScreen.yMin, Is.GreaterThanOrEqualTo(screenArea.yMin - Tolerance), $"{context}: bottom" + detail);
            Assert.That(onScreen.xMax, Is.LessThanOrEqualTo(screenArea.xMax + Tolerance), $"{context}: right" + detail);
            Assert.That(onScreen.yMax, Is.LessThanOrEqualTo(screenArea.yMax + Tolerance), $"{context}: top" + detail);
        }

        private Camera CreateCamera(HudLayoutOverlapTests.ScreenClassCase screen, out FactoryCameraController controller)
        {
            var texture = new RenderTexture(screen.PixelWidth, screen.PixelHeight, 16);
            texture.Create();
            transientObjects.Add(texture);

            var gameObject = new GameObject("initial-framing-camera");
            objects.Add(gameObject);
            var camera = gameObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = AuthoredOrthographicSize;
            camera.targetTexture = texture;
            camera.transform.position = new Vector3(FactoryWorldBounds.center.x, FactoryWorldBounds.center.y, -10f);
            controller = gameObject.AddComponent<FactoryCameraController>();
            controller.Configure(camera, FactoryWorldBounds, 2f, 12f, Vector2.one);
            controller.PanTo(FactoryWorldBounds.center);
            return camera;
        }

        private FactoryHudDocument CreateHud(HudLayoutOverlapTests.ScreenClassCase screen)
        {
            var texture = new RenderTexture(screen.PixelWidth, screen.PixelHeight, 0);
            texture.Create();
            transientObjects.Add(texture);

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettings.scale = screen.PixelsPerPoint;
            panelSettings.targetTexture = texture;
            transientObjects.Add(panelSettings);

            var gameObject = new GameObject("initial-framing-hud");
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
