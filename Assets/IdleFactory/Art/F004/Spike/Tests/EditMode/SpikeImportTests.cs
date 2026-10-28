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
