using System.IO;
using System.Linq;
using IdleFactory.Editor.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace IdleFactory.Tests.EditMode.F007
{
    /// <summary>
    /// T613 (FR-019, FR-034, TA-7): the per-look import rules of the F007 art, the guarantee that they reach only F007 prototype paths, and the Sprite Atlas v2
    /// settings per look. Importer behaviour is proven on temporary PNGs and atlases under this test folder, deleted afterwards; no F007 art is created.
    /// </summary>
    public sealed class F007ImportRulesTests
    {
        private const string ProbeFolder = "Assets/IdleFactory/Tests/EditMode/F007/ImportProbe";

        private static string MakePng(string name)
        {
            if (!AssetDatabase.IsValidFolder(ProbeFolder))
            {
                AssetDatabase.CreateFolder("Assets/IdleFactory/Tests/EditMode/F007", "ImportProbe");
            }

            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var pixels = Enumerable.Repeat(new Color32(120, 140, 160, 255), 64).ToArray();
            texture.SetPixels32(pixels);
            var path = ProbeFolder + "/" + name;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return path;
        }

        [TearDown]
        public void RemoveProbe()
        {
            if (AssetDatabase.IsValidFolder(ProbeFolder))
            {
                AssetDatabase.DeleteAsset(ProbeFolder);
            }
        }

        [Test]
        public void OnlyPngsInsideAPrototypeLookFolderAreGovernedAndResolveALook()
        {
            const string root = "Assets/IdleFactory/Art/F007/Prototype/";
            foreach (var look in new[] { F007Look.A, F007Look.B, F007Look.C })
            {
                var path = root + look + "/Machines/smelter.beauty.png";
                Assert.That(F007ImportRules.IsGovernedPath(path), Is.True, path);
                Assert.That(F007ImportRules.TryGetLook(path, out var found), Is.True, path);
                Assert.That(found, Is.EqualTo(look));
            }

            foreach (var path in new[]
            {
                "Assets/IdleFactory/Art/F004/Machines/smelter.png",
                "Assets/IdleFactory/Art/F006/Machines/smelter.png",
                "Assets/IdleFactory/Art/F004/F007/x.png",
                "Assets/IdleFactory/Art/F007X/Prototype/A/x.png",
                "Assets/IdleFactory/Tests/EditMode/F007/ImportProbe/x.png",
                "Assets/IdleFactory/Art/F007/Prototype/A/x.asset",
                "",
            })
            {
                Assert.That(F007ImportRules.IsGovernedPath(path), Is.False, path);
                Assert.That(F007ImportRules.TryGetLook(path, out _), Is.False, path);
            }

            // Governed but not in a look folder: the postprocessor warns and applies nothing.
            Assert.That(F007ImportRules.IsGovernedPath("Assets/IdleFactory/Art/F007/Profiles/x.png"), Is.True);
            Assert.That(F007ImportRules.TryGetLook("Assets/IdleFactory/Art/F007/Profiles/x.png", out _), Is.False);
            Assert.That(F007ImportRules.TryGetLook(root + "Z/x.png", out _), Is.False);
            Assert.That(F007ImportRules.TryGetLook(root + "x.png", out _), Is.False);
        }

        [Test]
        public void ExistingF004AndF006ArtIsNeverGovernedAndKeepsItsImportSettings()
        {
            var checkedCount = 0;
            foreach (var root in new[] { F004ImportRules.ArtRoot, F006ArtIntegration.ArtRoot })
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories).Take(8))
                {
                    var path = file.Replace((char)92, '/');
                    Assert.That(F007ImportRules.IsGovernedPath(path), Is.False, path);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    if (importer == null || importer.textureType != TextureImporterType.Sprite)
                    {
                        continue;
                    }

                    Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
                    Assert.That(importer.mipmapEnabled, Is.False, path);
                    checkedCount++;
                }
            }

            Assert.That(checkedCount, Is.GreaterThan(0), "at least one existing F004 or F006 sprite was checked");
        }

        [Test]
        public void TheLookTableMatchesTheContract()
        {
            var a = F007ImportRules.For(F007Look.A);
            Assert.That(a.Ppu, Is.EqualTo(48));
            Assert.That(a.Filter, Is.EqualTo(FilterMode.Point));
            Assert.That(a.TightMesh, Is.False);
            Assert.That(a.AndroidFormat, Is.EqualTo(TextureImporterFormat.RGBA32));

            foreach (var look in new[] { F007Look.B, F007Look.C })
            {
                var r = F007ImportRules.For(look);
                Assert.That(r.Ppu, Is.EqualTo(128), look.ToString());
                Assert.That(r.Filter, Is.EqualTo(FilterMode.Bilinear), look.ToString());
                Assert.That(r.TightMesh, Is.True, look.ToString());
                Assert.That(r.AndroidFormat, Is.EqualTo(TextureImporterFormat.ASTC_6x6), look.ToString());
                Assert.That(r.WindowsFormat, Is.EqualTo(TextureImporterFormat.BC7), look.ToString());
            }

            foreach (var look in new[] { F007Look.A, F007Look.B, F007Look.C })
            {
                var r = F007ImportRules.For(look);
                Assert.That(r.AtlasPadding, Is.GreaterThanOrEqualTo(2), look + " padding is at least 2");
                Assert.That(r.AtlasPadding, Is.GreaterThanOrEqualTo(1 << r.MipLevels), look + " padding covers its mip levels (TA-7)");
            }
        }

        [Test]
        public void PivotIsReadFromARenderHdManifestRowAndRejectedWhenOutOfRange()
        {
            Assert.That(F007ImportRules.TryReadPivot("{\"pivot\":[0.25,0.75],\"canvas\":[10,10]}", out var p), Is.True);
            Assert.That(p, Is.EqualTo(new Vector2(0.25f, 0.75f)));
            Assert.That(F007ImportRules.TryReadPivot("{\"pivot\":[1.5,0.5]}", out _), Is.False);
            Assert.That(F007ImportRules.TryReadPivot("{\"pivot\":[0.5]}", out _), Is.False);
            Assert.That(F007ImportRules.TryReadPivot("{}", out _), Is.False);
            Assert.That(F007ImportRules.TryReadPivot(string.Empty, out _), Is.False);
        }

        [Test]
        public void ApplyWritesTheLookRulesIntoAnImporter()
        {
            var path = MakePng("probe-a.png");
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            F007ImportRules.Apply(importer, F007Look.A, "Assets/IdleFactory/Art/F007/Prototype/A/x.beauty.png", new Vector2(0.25f, 0.75f));
            importer.SaveAndReimport();
            importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(48f));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.spritePivot, Is.EqualTo(new Vector2(0.25f, 0.75f)));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(importer.GetPlatformTextureSettings("Android").format, Is.EqualTo(TextureImporterFormat.RGBA32));

            var pathB = MakePng("probe-b.png");
            var importerB = (TextureImporter)AssetImporter.GetAtPath(pathB);
            F007ImportRules.Apply(importerB, F007Look.B, "Assets/IdleFactory/Art/F007/Prototype/B/x.beauty.png", null);
            importerB.SaveAndReimport();
            importerB = (TextureImporter)AssetImporter.GetAtPath(pathB);
            Assert.That(importerB.spritePixelsPerUnit, Is.EqualTo(128f));
            Assert.That(importerB.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importerB.mipmapEnabled, Is.False);
            Assert.That(importerB.sRGBTexture, Is.True);
            importerB.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.Tight));
            Assert.That(importerB.GetPlatformTextureSettings("Android").format, Is.EqualTo(TextureImporterFormat.ASTC_6x6));
            Assert.That(importerB.GetPlatformTextureSettings("Standalone").format, Is.EqualTo(TextureImporterFormat.BC7));
            Assert.That(importerB.GetPlatformTextureSettings("Android").overridden, Is.True);
        }

        [Test]
        public void ShadowAndNormalPassesAreSingleChannelAndLinearDataRespectively()
        {
            Assert.That(F007ImportRules.IsShadowPass("a/x.shadow.png"), Is.True);
            Assert.That(F007ImportRules.IsShadowPass("a/x.beauty.png"), Is.False);
            Assert.That(F007ImportRules.IsNormalPass("a/x.normal.png"), Is.True);

            var path = MakePng("probe-shadow.png");
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            F007ImportRules.Apply(importer, F007Look.B, "Assets/IdleFactory/Art/F007/Prototype/B/x.shadow.png", null);
            importer.SaveAndReimport();
            importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.GetPlatformTextureSettings("Android").format, Is.EqualTo(TextureImporterFormat.Alpha8), "shadows stay single-channel and never ASTC 6x6");
            Assert.That(importer.GetPlatformTextureSettings("Standalone").format, Is.EqualTo(TextureImporterFormat.Alpha8));

            var pathN = MakePng("probe-normal.png");
            var importerN = (TextureImporter)AssetImporter.GetAtPath(pathN);
            F007ImportRules.Apply(importerN, F007Look.B, "Assets/IdleFactory/Art/F007/Prototype/B/x.normal.png", null);
            importerN.SaveAndReimport();
            Assert.That(((TextureImporter)AssetImporter.GetAtPath(pathN)).sRGBTexture, Is.False);
        }

        [Test]
        public void AtlasSettingsPerLookKeepTheStrictPageCapAndTheMipPadding()
        {
            Assert.That(F007SpriteAtlasSettings.MaxPageBytes, Is.EqualTo(1024 * 1024), "the page rule in force is 1 MiB until T614 passes on the Pixel and the owner accepts");
            foreach (var look in new[] { F007Look.A, F007Look.B, F007Look.C })
            {
                if (!AssetDatabase.IsValidFolder(ProbeFolder))
                {
                    AssetDatabase.CreateFolder("Assets/IdleFactory/Tests/EditMode/F007", "ImportProbe");
                }

                var path = ProbeFolder + "/atlas-" + look + F004SpriteAtlasSettings.AtlasExtension;
                var asset = new SpriteAtlasAsset();
                SpriteAtlasAsset.Save(asset, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
                F007SpriteAtlasSettings.Configure(importer, look);
                importer.SaveAndReimport();
                importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
                var problems = F007SpriteAtlasSettings.Validate(importer, look);
                Assert.That(problems, Is.Empty, look + ": " + string.Join("; ", problems));
                Assert.That(importer.GetPlatformSettings("Android").format, Is.EqualTo(F007ImportRules.For(look).AndroidFormat), look.ToString());
                foreach (var platform in F004SpriteAtlasSettings.Platforms)
                {
                    Assert.That(importer.GetPlatformSettings(platform).maxTextureSize, Is.LessThanOrEqualTo(F004SpriteAtlasSettings.MaxPageSize), look + " " + platform);
                }
            }
        }
    }
}
