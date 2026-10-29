using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using IdleFactory.Art.F004.Spike.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdleFactory.Art.F004.Spike.Tests
{
    public sealed class SpikeImportTests
    {
        private const string D24 = "Assets/IdleFactory/Art/F004/Spike/D24/";
        private static readonly string[] Projections = { "oblique-3-4", "isometric-2-1" };

        [Test]
        public void DensityOfMapsFoldersToPixelsPerUnit()
        {
            Assert.AreEqual(16, SpikeTextureImportRules.DensityOf(SpikeTextureImportRules.SpikeRoot + "D16/a.png"));
            Assert.AreEqual(24, SpikeTextureImportRules.DensityOf(SpikeTextureImportRules.SpikeRoot + "D24/oblique-3-4/a.png"));
            Assert.AreEqual(32, SpikeTextureImportRules.DensityOf(SpikeTextureImportRules.SpikeRoot + "D32/a.png"));
            Assert.AreEqual(0, SpikeTextureImportRules.DensityOf("Assets/IdleFactory/Art/F001/D24/a.png"));
            Assert.AreEqual(0, SpikeTextureImportRules.DensityOf(SpikeTextureImportRules.SpikeRoot + "D240/a.png"));
        }

        [Test]
        public void ImportedD24SpritesHaveSpikeSettingsAndMatchTheirSources()
        {
            var paths = Projections.SelectMany(p => Directory.GetFiles(D24 + p, "*.png")).Select(p => p.Replace('\\', '/')).ToArray();
            Assert.AreEqual(48, paths.Length, "24 sprites per projection");
            foreach (var path in paths)
            {
                AssertSettings((TextureImporter)AssetImporter.GetAtPath(path), 24, path);
                var source = path.Replace(D24, "art-source/f004/spike/s1/pixel/");
                Assert.AreEqual(Sha(source), Sha(path), "byte-identical to " + source);
            }
        }

        [Serializable]
        private sealed class ManifestFrame { public string file; public string sha256; }

        [Serializable]
        private sealed class ManifestEntry
        {
            public string assetId;
            public int[] canvas;
            public float[] pivot;
            public int ppu;
            public ManifestFrame[] frames;
        }

        [Serializable]
        private sealed class Manifest { public ManifestEntry[] entries; }

        [Test]
        public void ImportedS2D24SpritesHaveSpikeSettingsManifestPivotAndSizeAndMatchTheirSources()
        {
            // T422, Art Director condition 6 (sprite level): importer settings, pivot and size per manifest, byte identity.
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("art-source/f004/spike/s2/candidates-D24.json"));
            Assert.AreEqual(14, manifest.entries.Length, "14 S2 assets at D24");
            var folder = D24 + "isometric-2-1-s2/";
            var onDisk = Directory.GetFiles(folder, "*.png").Select(p => p.Replace(Path.DirectorySeparatorChar, '/')).ToArray();
            Assert.AreEqual(24, onDisk.Length, "24 S2 sprites (running machines have 6 frames)");
            var checkedCount = 0;
            foreach (var entry in manifest.entries)
            {
                Assert.AreEqual(24, entry.ppu, entry.assetId);
                foreach (var frame in entry.frames)
                {
                    var name = Path.GetFileName(frame.file);
                    var path = folder + name;
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    AssertSettings(importer, 24, path);
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    Assert.AreEqual((int)SpriteAlignment.Custom, settings.spriteAlignment, path);
                    Assert.AreEqual(entry.pivot[0], importer.spritePivot.x, 1e-6f, path + " pivot x per manifest");
                    Assert.AreEqual(entry.pivot[1], importer.spritePivot.y, 1e-6f, path + " pivot y per manifest");
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Assert.NotNull(sprite, path);
                    Assert.AreEqual(new Vector2(entry.canvas[0], entry.canvas[1]), sprite.rect.size, path + " canvas per manifest");
                    Assert.AreEqual(24f, sprite.pixelsPerUnit, path);
                    var source = "art-source/f004/spike/s2/" + frame.file;
                    Assert.AreEqual(Sha(source), Sha(path), "byte-identical to " + source);
                    Assert.AreEqual(frame.sha256, Sha(path).Replace("-", string.Empty).ToLowerInvariant(), "manifest sha256 of " + path);
                    checkedCount++;
                }
            }

            Assert.AreEqual(24, checkedCount, "every on-disk S2 sprite is covered by the manifest");
        }

        [Test]
        public void ImportedS3SpritesHaveSpikeSettingsPivotAndMatchTheirSources()
        {
            var paths = Directory.GetFiles(D24 + "isometric-2-1-s3", "*.png").Select(p => p.Replace('\\', '/')).ToArray();
            Assert.AreEqual(51, paths.Length, "S3/S4 set: 4 bodies, 3 plates, 20 a/b cue layers, 21 composites, 3 lighting B sprites");
            foreach (var path in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                AssertSettings(importer, 24, path);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.AreEqual((int)SpriteAlignment.Custom, settings.spriteAlignment, path);
                Assert.AreEqual(new Vector2(0.5f, 0.234375f), importer.spritePivot, path + " pivot = S2 machine pivot (x 40 of 80, y 15 of 64 art px)");
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.NotNull(sprite, path);
                Assert.AreEqual(new Vector2(80, 64), sprite.rect.size, path);
                var source = path.Replace(D24 + "isometric-2-1-s3/", "art-source/f004/spike/s3-s4/pixel/");
                Assert.AreEqual(Sha(source), Sha(path), "byte-identical to " + source);
            }
        }

        [Test]
        public void VariantBWaitingBadgeAt23HasTheCoolFrameFillAndTheOthersKeepTheirs()
        {
            // T460: waiting is the only variant-b badge with the cue_info (#6ec1ff) frame fill; cue_warn and cue_alert stay.
            Assert.AreEqual(new Color32(0x6e, 0xc1, 0xff, 255), BadgeFill("waiting"));
            Assert.AreEqual(new Color32(0xe8, 0x57, 0x3a, 255), BadgeFill("missing-input"));
            Assert.AreEqual(new Color32(0xf2, 0xb1, 0x34, 255), BadgeFill("storage-full"));
        }

        private static Color32 BadgeFill(string state)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(D24 + "isometric-2-1-s3/s3b-cue-" + state + "-23_00.png")));
                return tex.GetPixel(52, tex.height - 1 - 12);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        [TestCase(16)]
        [TestCase(32)]
        public void AssetPlacedInAnEmptyDensityFolderGetsItsPixelsPerUnit(int density)
        {
            var path = SpikeTextureImportRules.SpikeRoot + "D" + density + "/__import_probe.png";
            try
            {
                var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                AssertSettings((TextureImporter)AssetImporter.GetAtPath(path), density, path);
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        private static void AssertSettings(TextureImporter importer, int density, string path)
        {
            Assert.NotNull(importer, path);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
            Assert.AreEqual(density, importer.spritePixelsPerUnit, path);
            Assert.AreEqual(FilterMode.Point, importer.filterMode, path);
            Assert.IsFalse(importer.mipmapEnabled, path);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, path);
            Assert.IsFalse(importer.crunchedCompression, path);
        }

        private static string Sha(string path)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));
        }
    }
}
