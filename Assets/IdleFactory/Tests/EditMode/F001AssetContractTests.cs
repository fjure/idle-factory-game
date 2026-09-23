using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Unity.Content;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    public sealed class F001AssetContractTests
    {
        private const string ImportedRoot = "Assets/IdleFactory/Art/Development/F001/Imported";
        private const string ContentRoot = "Assets/IdleFactory/Content/Development";

        private static readonly AssetExpectation[] Assets =
        {
            Center("F001-GRID-TILE-1X1", "f001-grid-tile-1x1-v01.png", 64, 64),
            Center("F001-GRID-BOUNDARY-EDGE", "f001-grid-boundary-edge-v01.png", 64, 64),
            Center("F001-GRID-BOUNDARY-CORNER", "f001-grid-boundary-corner-v01.png", 64, 64),
            BottomLeft("F001-MACHINE-D001-NORMAL", "f001-machine-d001-normal-v01.png", 128, 128),
            BottomLeft("F001-MACHINE-D001-SELECTED", "f001-machine-d001-selected-v01.png", 128, 128),
            BottomLeft("F001-MACHINE-ALT-3X1-NORMAL", "f001-machine-alt-3x1-normal-v01.png", 192, 64),
            BottomLeft("F001-MACHINE-ALT-3X1-SELECTED", "f001-machine-alt-3x1-selected-v01.png", 192, 64),
            BottomLeft("F001-GHOST-D001-VALID", "f001-ghost-d001-valid-v01.png", 128, 128),
            BottomLeft("F001-GHOST-D001-INVALID", "f001-ghost-d001-invalid-v01.png", 128, 128),
            BottomLeft("F001-GHOST-ALT-3X1-VALID", "f001-ghost-alt-3x1-valid-v01.png", 192, 64),
            BottomLeft("F001-GHOST-ALT-3X1-INVALID", "f001-ghost-alt-3x1-invalid-v01.png", 192, 64),
            Center("F001-RESOURCE-DEV-INPUT", "f001-resource-dev-input-v01.png", 32, 32),
            Center("F001-RESOURCE-DEV-OUTPUT", "f001-resource-dev-output-v01.png", 32, 32),
            Center("F001-BADGE-NORMAL", "f001-badge-normal-v01.png", 64, 32),
            Center("F001-BADGE-SELECTED", "f001-badge-selected-v01.png", 64, 32),
            Center("F001-BADGE-PLACEMENT-VALID", "f001-badge-placement-valid-v01.png", 64, 32),
            Center("F001-BADGE-PLACEMENT-INVALID", "f001-badge-placement-invalid-v01.png", 64, 32),
            Center("F001-BADGE-PRODUCING", "f001-badge-producing-v01.png", 64, 32),
            Center("F001-BADGE-MISSING-INPUT", "f001-badge-missing-input-v01.png", 64, 32),
            Center("F001-BADGE-FULL-OUTPUT", "f001-badge-full-output-v01.png", 64, 32),
            Center("F001-BADGE-INACTIVE", "f001-badge-inactive-v01.png", 64, 32),
        };

        [Test]
        public void ImportedFolderContainsOnlyTheExactReviewedInventoryAndMetas()
        {
            var expectedFiles = Assets
                .SelectMany(asset => new[] { asset.Path, asset.Path + ".meta" })
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var actualFiles = Directory.GetFiles(ImportedRoot, "*", SearchOption.TopDirectoryOnly)
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.That(Assets, Has.Length.EqualTo(21));
            Assert.That(actualFiles, Is.EqualTo(expectedFiles));
            Assert.That(File.Exists(ImportedRoot + ".meta"), Is.True);

            var guids = Assets.Select(asset => AssetDatabase.AssetPathToGUID(asset.Path)).ToArray();
            Assert.That(guids, Has.All.Matches<string>(guid =>
                guid.Length == 32 && guid.All(character => Uri.IsHexDigit(character))));
            Assert.That(guids.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(Assets.Length));
        }

        [Test]
        public void EveryPngPreservesCanvasAlphaAndExactSpriteImporterContract()
        {
            foreach (var asset in Assets)
            {
                var png = File.ReadAllBytes(asset.Path);
                Assert.That(png[25], Is.EqualTo(6), $"{asset.Id} must be an RGBA PNG");

                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(ImageConversion.LoadImage(decoded, png, false), Is.True, asset.Id);
                    Assert.That(decoded.width, Is.EqualTo(asset.Width), asset.Id);
                    Assert.That(decoded.height, Is.EqualTo(asset.Height), asset.Id);
                    var pixels = decoded.GetPixels32();
                    Assert.That(pixels.Any(pixel => pixel.a > 0), Is.True, $"{asset.Id} has no visible pixels");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(decoded);
                }

                var importer = AssetImporter.GetAtPath(asset.Path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, asset.Id);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), asset.Id);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single), asset.Id);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(64f), asset.Id);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), asset.Id);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), asset.Id);
                Assert.That(importer.mipmapEnabled, Is.False, asset.Id);
                Assert.That(importer.alphaIsTransparency, Is.True, asset.Id);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), asset.Id);
                Assert.That(importer.GetPlatformTextureSettings("Android").overridden, Is.False, asset.Id);
                Assert.That(importer.GetPlatformTextureSettings("iPhone").overridden, Is.False, asset.Id);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(asset.Path);
                Assert.That(sprite, Is.Not.Null, asset.Id);
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(asset.Width, asset.Height)), asset.Id);
                Assert.That(sprite.vertices, Has.Length.EqualTo(4), $"{asset.Id} must retain a full-rectangle mesh");
                var expectedPivot = asset.BottomLeftPivot
                    ? Vector2.zero
                    : new Vector2(asset.Width / 2f, asset.Height / 2f);
                Assert.That(sprite.pivot, Is.EqualTo(expectedPivot), asset.Id);
            }

            Assert.That(Assets.Sum(asset => asset.Width * asset.Height * 4), Is.EqualTo(581632));
        }

        [Test]
        public void DevelopmentDefinitionKeysResolveOneToOneToImportedSprites()
        {
            var keyToPath = Assets.ToDictionary(asset => asset.Id, asset => asset.Path, StringComparer.Ordinal);
            Assert.That(keyToPath.Count, Is.EqualTo(Assets.Length));
            Assert.That(keyToPath.Values.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(Assets.Length));

            AssertPresentationKey<BuildingDefinitionAsset>(
                ContentRoot + "/Buildings/D001.asset",
                "F001-MACHINE-D001-NORMAL",
                keyToPath);
            AssertPresentationKey<ResourceDefinitionAsset>(
                ContentRoot + "/Resources/dev.input.asset",
                "F001-RESOURCE-DEV-INPUT",
                keyToPath);
            AssertPresentationKey<ResourceDefinitionAsset>(
                ContentRoot + "/Resources/dev.output.asset",
                "F001-RESOURCE-DEV-OUTPUT",
                keyToPath);
        }

        private static void AssertPresentationKey<T>(
            string definitionPath,
            string expectedKey,
            IReadOnlyDictionary<string, string> keyToPath)
            where T : UnityEngine.Object
        {
            var definition = AssetDatabase.LoadAssetAtPath<T>(definitionPath);
            Assert.That(definition, Is.Not.Null, definitionPath);
            var key = new SerializedObject(definition).FindProperty("presentationKey").stringValue;
            Assert.That(key, Is.EqualTo(expectedKey), definitionPath);
            Assert.That(keyToPath.ContainsKey(key), Is.True, key);
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(keyToPath[key]), Is.Not.Null, key);
        }

        private static AssetExpectation Center(string id, string fileName, int width, int height)
        {
            return new AssetExpectation(id, fileName, width, height, false);
        }

        private static AssetExpectation BottomLeft(string id, string fileName, int width, int height)
        {
            return new AssetExpectation(id, fileName, width, height, true);
        }

        private sealed class AssetExpectation
        {
            public AssetExpectation(string id, string fileName, int width, int height, bool bottomLeftPivot)
            {
                Id = id;
                Path = ImportedRoot + "/" + fileName;
                Width = width;
                Height = height;
                BottomLeftPivot = bottomLeftPivot;
            }

            public string Id { get; }
            public string Path { get; }
            public int Width { get; }
            public int Height { get; }
            public bool BottomLeftPivot { get; }
        }
    }
}
