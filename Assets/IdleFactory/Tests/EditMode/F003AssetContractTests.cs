using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T111: the 78 approved F003 sources are imported as 64 PPU sprites, every F003 key is registered in the scene
    /// sprite catalog, every content presentation key resolves, and a key that is missing from the catalog fails the
    /// content load that start-up performs (<c>presentation.key.unresolved</c>).
    /// </summary>
    public sealed class F003AssetContractTests
    {
        private const string SourceRoot = "Assets/IdleFactory/Art/Development/F003/Source";
        private const string SpriteRoot = "Assets/IdleFactory/Art/Development/F003/Sprites";
        private const string ContentPath = "Assets/IdleFactory/Content/Iron/iron-factory-content.json";
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private const string BriefPath = "docs/art/F003-asset-briefs-v0.1.md";
        private const float PixelsPerUnit = 64f;

        // The brief inventory: stable ID and source pixels. Slot bases and machine bodies pivot at the bottom-left
        // (0,0); every other row pivots at the center. The slug is the lowercase stable ID plus -v01.
        private static readonly AssetExpectation[] Assets =
        {
            BottomLeft("F003-SLOT-EMPTY", 128, 128),
            BottomLeft("F003-SLOT-LOCKED-L2", 128, 128),
            BottomLeft("F003-SLOT-LOCKED-L3", 128, 128),
            BottomLeft("F003-SLOT-LOCKED-L4", 128, 128),
            BottomLeft("F003-SLOT-LOCKED-L5", 128, 128),
            BottomLeft("F003-SLOT-OCCUPIED-BASE", 128, 128),
            BottomLeft("F003-SLOT-SELECTED-FRAME", 128, 128),
            BottomLeft("F003-MACHINE-MINER", 128, 128),
            BottomLeft("F003-MACHINE-SMELTER", 128, 128),
            BottomLeft("F003-MACHINE-PRESS", 128, 128),
            BottomLeft("F003-MACHINE-ASSEMBLER", 128, 128),
            Center("F003-BADGE-RUNNING", 72, 32),
            Center("F003-BADGE-WAITING", 72, 32),
            Center("F003-BADGE-MISSING-INPUT", 72, 32),
            Center("F003-BADGE-STORAGE-FULL", 72, 32),
            Center("F003-BADGE-PAUSED", 72, 32),
            Center("F003-BADGE-INACTIVE", 72, 32),
            Center("F003-MARKER-BOOSTED", 28, 28),
            Center("F003-MARKER-OVERDRIVE", 28, 28),
            Center("F003-RES-IRON-ORE", 48, 48),
            Center("F003-RES-IRON-BAR", 48, 48),
            Center("F003-RES-IRON-PLATE", 48, 48),
            Center("F003-RES-GEAR", 48, 48),
            Center("F003-ITEM-DIAMOND-DRILL-HEAD", 64, 64),
            Center("F003-ITEM-FLUX-CATALYST", 64, 64),
            Center("F003-ITEM-OVERCLOCK-CORE", 64, 64),
            Center("F003-ITEM-TIER-1", 48, 24),
            Center("F003-ITEM-TIER-2", 48, 24),
            Center("F003-ITEM-TIER-3", 48, 24),
            Center("F003-UI-ICON-CASH", 48, 48),
            Center("F003-UI-ICON-RP", 48, 48),
            Center("F003-UI-ICON-IP", 48, 48),
            Center("F003-UI-ICON-FV", 48, 48),
            Center("F003-UI-ICON-STORAGE", 48, 48),
            Center("F003-UI-ICON-EXPAND-FACTORY", 48, 48),
            Center("F003-UI-ICON-TAB-FACTORY", 48, 48),
            Center("F003-UI-ICON-TAB-MARKET", 48, 48),
            Center("F003-UI-ICON-TAB-CONTRACTS", 48, 48),
            Center("F003-UI-ICON-TAB-ITEMS", 48, 48),
            Center("F003-UI-ICON-TAB-RESEARCH", 48, 48),
            Center("F003-UI-ICON-TAB-PRESTIGE", 48, 48),
            Center("F003-UI-ICON-INDUSTRY", 48, 48),
            Center("F003-UI-ICON-BUILD", 48, 48),
            Center("F003-UI-ICON-UPGRADE", 48, 48),
            Center("F003-UI-ICON-REMOVE", 48, 48),
            Center("F003-UI-ICON-TOGGLE-ON", 48, 48),
            Center("F003-UI-ICON-TOGGLE-OFF", 48, 48),
            Center("F003-UI-ICON-AUTOSELL", 48, 48),
            Center("F003-UI-ICON-PRIORITY-UP", 48, 48),
            Center("F003-UI-ICON-PRIORITY-DOWN", 48, 48),
            Center("F003-UI-ICON-DRAG-HANDLE", 48, 48),
            Center("F003-UI-ICON-ACCEPT", 48, 48),
            Center("F003-UI-ICON-DELIVER", 48, 48),
            Center("F003-UI-ICON-ABANDON", 48, 48),
            Center("F003-UI-ICON-ACTIVATE", 48, 48),
            Center("F003-UI-ICON-CONFIRM", 48, 48),
            Center("F003-UI-ICON-CANCEL", 48, 48),
            Center("F003-UI-ICON-RETRY", 48, 48),
            Center("F003-UI-ICON-CLOSE", 48, 48),
            Center("F003-UI-ICON-INFO", 48, 48),
            Center("F003-UI-ICON-WARNING", 48, 48),
            Center("F003-UI-ICON-LOCK", 48, 48),
            Center("F003-UI-ICON-TIMER", 48, 48),
            Center("F003-UI-ICON-OFFLINE", 48, 48),
            Center("F003-UI-ICON-IMPORT", 48, 48),
            Center("F003-UI-ICON-SPINNER", 48, 48),
            Center("F003-UI-ICON-OVERDRIVE-READY", 48, 48),
            Center("F003-UI-ICON-OVERDRIVE-ACTIVE", 48, 48),
            Center("F003-UI-ICON-OVERDRIVE-COOLDOWN", 48, 48),
            Center("F003-UI-ICON-OVERDRIVE-LOCKED", 48, 48),
            Center("F003-UI-ICON-RESEARCH-NODE-LOCKED", 48, 48),
            Center("F003-UI-ICON-RESEARCH-NODE-AVAILABLE", 48, 48),
            Center("F003-UI-ICON-RESEARCH-NODE-OWNED", 48, 48),
            Center("F003-UI-ICON-RESEARCH-NODE-MAXED", 48, 48),
            Center("F003-UI-ICON-EFFECT-SPEED", 48, 48),
            Center("F003-UI-ICON-EFFECT-OUTPUT", 48, 48),
            Center("F003-UI-ICON-EFFECT-SALE-PRICE", 48, 48),
            Center("F003-UI-ICON-EFFECT-UPGRADE-COST", 48, 48),
        };

        [Test]
        public void TheSpriteFolderHoldsExactlyTheBriefInventoryWithItsSourcesAndMetas()
        {
            Assert.That(Assets, Has.Length.EqualTo(78));
            Assert.That(Assets.Select(asset => asset.Id).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(78));

            var expected = Assets
                .SelectMany(asset => new[] { asset.PngPath, asset.PngPath + ".meta" })
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var actual = Directory.GetFiles(SpriteRoot, "*", SearchOption.TopDirectoryOnly)
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.That(actual, Is.EqualTo(expected));

            var brief = File.ReadAllText(BriefPath);
            foreach (var asset in Assets)
            {
                Assert.That(brief, Does.Contain("`" + asset.Id), asset.Id + " is a brief row");
                Assert.That(brief, Does.Contain("`" + asset.Slug + "`"), asset.Id + " slug is a brief row");
                Assert.That(File.Exists(asset.SvgPath), Is.True, asset.Id + " has its approved source");
            }

            var guids = Assets.Select(asset => AssetDatabase.AssetPathToGUID(asset.PngPath)).ToArray();
            Assert.That(guids, Has.All.Matches<string>(guid => guid.Length == 32 && guid.All(Uri.IsHexDigit)));
            Assert.That(guids.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(Assets.Length));
        }

        [Test]
        public void EveryBriefRowHasAnImportedSpriteWithTheSpecifiedSizeAndPivot()
        {
            long decodedBytes = 0;
            foreach (var asset in Assets)
            {
                var png = File.ReadAllBytes(asset.PngPath);
                Assert.That(png[25], Is.EqualTo(6), asset.Id + " must be an RGBA PNG");

                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(ImageConversion.LoadImage(decoded, png, false), Is.True, asset.Id);
                    Assert.That(decoded.width, Is.EqualTo(asset.Width), asset.Id);
                    Assert.That(decoded.height, Is.EqualTo(asset.Height), asset.Id);
                    Assert.That(decoded.GetPixels32().Any(pixel => pixel.a > 0), Is.True, asset.Id + " has no visible pixels");
                    decodedBytes += decoded.width * decoded.height * 4L;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(decoded);
                }

                var importer = AssetImporter.GetAtPath(asset.PngPath) as TextureImporter;
                Assert.That(importer, Is.Not.Null, asset.Id);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), asset.Id);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), asset.Id);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(PixelsPerUnit), asset.Id);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), asset.Id);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), asset.Id);
                Assert.That(importer.mipmapEnabled, Is.False, asset.Id);
                Assert.That(importer.isReadable, Is.False, asset.Id);
                Assert.That(importer.alphaIsTransparency, Is.True, asset.Id);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), asset.Id);
                Assert.That(importer.GetPlatformTextureSettings("Android").overridden, Is.False, asset.Id);
                Assert.That(importer.GetPlatformTextureSettings("iPhone").overridden, Is.False, asset.Id);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(asset.PngPath);
                Assert.That(sprite, Is.Not.Null, asset.Id);
                Assert.That(sprite.name, Is.EqualTo(asset.Slug), asset.Id);
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(asset.Width, asset.Height)), asset.Id);
                Assert.That(sprite.vertices, Has.Length.EqualTo(4), asset.Id + " must retain a full-rectangle mesh");
                var expectedPivot = asset.BottomLeftPivot
                    ? Vector2.zero
                    : new Vector2(asset.Width / 2f, asset.Height / 2f);
                Assert.That(sprite.pivot, Is.EqualTo(expectedPivot), asset.Id);
            }

            // SC-008 memory cost: the exact decoded RGBA footprint of the whole set (no atlas, no compression).
            Assert.That(decodedBytes, Is.EqualTo(1333888L));
        }

        [Test]
        public void EveryBriefRowIsRegisteredInTheSceneCatalogWithTheImportedSprite()
        {
            WithSceneCatalog(catalog =>
            {
                foreach (var asset in Assets)
                {
                    Assert.That(catalog.TryGetEntry(asset.Id, out var entry), Is.True, asset.Id + " is not registered");
                    Assert.That(entry.FrameCount, Is.EqualTo(1), asset.Id);
                    var sprite = entry.Sprite;
                    Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(asset.PngPath), asset.Id);

                    // World size and normalized pivot, so the key stays valid when Feature 004 changes the PPU.
                    Assert.That(sprite.rect.width / sprite.pixelsPerUnit, Is.EqualTo(asset.Width / PixelsPerUnit).Within(0.001f), asset.Id);
                    Assert.That(sprite.rect.height / sprite.pixelsPerUnit, Is.EqualTo(asset.Height / PixelsPerUnit).Within(0.001f), asset.Id);
                    var normalizedPivot = asset.BottomLeftPivot ? 0f : 0.5f;
                    Assert.That(sprite.pivot.x / sprite.rect.width, Is.EqualTo(normalizedPivot).Within(0.001f), asset.Id);
                    Assert.That(sprite.pivot.y / sprite.rect.height, Is.EqualTo(normalizedPivot).Within(0.001f), asset.Id);
                }
            });
        }

        [Test]
        public void EveryContentPresentationKeyResolvesInTheSceneCatalog()
        {
            var keys = ContentKeys();
            Assert.That(keys, Has.Length.EqualTo(11));

            WithSceneCatalog(catalog =>
            {
                foreach (var key in keys)
                {
                    Assert.That(catalog.TryGetEntry(key, out _), Is.True, "presentationKey '" + key + "' is not a registered sprite");
                }

                var result = GameplayContentDispatcher.Load(File.ReadAllBytes(ContentPath), new CatalogResolver(catalog, null));
                Assert.That(result.IsSuccess, Is.True, string.Join(", ", result.Diagnostics.Select(d => d.Code)));
            });
        }

        [Test]
        public void AKeyMissingFromTheCatalogFailsTheStartUpContentLoad()
        {
            var bytes = File.ReadAllBytes(ContentPath);
            WithSceneCatalog(catalog =>
            {
                foreach (var missing in ContentKeys())
                {
                    var result = GameplayContentDispatcher.Load(bytes, new CatalogResolver(catalog, missing));
                    Assert.That(result.IsSuccess, Is.False, missing);
                    Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("presentation.key.unresolved"), missing);
                }

                Assert.That(catalog.TryGet("F003-NOT-IN-THE-CONTENT", out _), Is.False);
            });
        }

        private static string[] ContentKeys()
        {
            var text = File.ReadAllText(ContentPath);
            return Regex.Matches(text, "\"presentationKey\"\\s*:\\s*\"([^\"]+)\"")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static void WithSceneCatalog(Action<PresentationSpriteCatalog> check)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var bootstrap = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CoreGameFoundationBootstrap>(true))
                    .Single();
                var catalog = bootstrap.SpriteCatalog;
                Assert.That(catalog.Validate(), Is.True, catalog.Error);
                check(catalog);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static AssetExpectation Center(string id, int width, int height) => new AssetExpectation(id, width, height, false);

        private static AssetExpectation BottomLeft(string id, int width, int height) => new AssetExpectation(id, width, height, true);

        private sealed class CatalogResolver : IPresentationKeyResolver
        {
            private readonly PresentationSpriteCatalog catalog;
            private readonly string hiddenKey;

            public CatalogResolver(PresentationSpriteCatalog catalog, string hiddenKey)
            {
                this.catalog = catalog;
                this.hiddenKey = hiddenKey;
            }

            public bool IsResolved(string presentationKey) =>
                !string.Equals(presentationKey, hiddenKey, StringComparison.Ordinal) && catalog.TryGet(presentationKey, out _);
        }

        private sealed class AssetExpectation
        {
            public AssetExpectation(string id, int width, int height, bool bottomLeftPivot)
            {
                Id = id;
                Slug = id.ToLowerInvariant() + "-v01";
                Width = width;
                Height = height;
                BottomLeftPivot = bottomLeftPivot;
            }

            public string Id { get; }
            public string Slug { get; }
            public int Width { get; }
            public int Height { get; }
            public bool BottomLeftPivot { get; }
            public string PngPath => SpriteRoot + "/" + Slug + ".png";
            public string SvgPath => SourceRoot + "/" + Slug + ".svg";
        }
    }
}
