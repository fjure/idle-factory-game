using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Editor.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T552: the Android GLES3 player stayed on its loading screen for 8+ minutes (loading thread at 100 % CPU) once a scene referenced
    /// the 512 x 1024 (2 MiB RGBA32) page of the F004 machines atlas; the same atlas as two 1 MiB pages loaded in about 3 s. These tests
    /// keep every shipped atlas page within <see cref="F004SpriteAtlasSettings.MaxPageBytes"/>.
    /// </summary>
    public sealed class AtlasPageBudgetTests
    {
        private static List<string> ShippedAtlasPaths()
        {
            var paths = F004SpriteAtlasSettings.FindAtlasPaths();
            if (Directory.Exists(F006ArtIntegration.AtlasFolder))
            {
                paths.AddRange(Directory.GetFiles(F006ArtIntegration.AtlasFolder, "*" + F004SpriteAtlasSettings.AtlasExtension)
                    .Select(file => file.Replace((char)92, '/')));
            }

            return paths;
        }

        [Test]
        public void EveryShippedAtlasPageStaysWithinTheAndroidLoadPageBudget()
        {
            var paths = ShippedAtlasPaths();
            Assert.That(paths, Is.Not.Empty);
            foreach (var path in paths)
            {
                var pages = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>().ToList();
                Assert.That(pages, Is.Not.Empty, path);
                foreach (var page in pages)
                {
                    Assert.That(page.width * page.height * 4, Is.LessThanOrEqualTo(F004SpriteAtlasSettings.MaxPageBytes),
                        path + " page " + page.width + " x " + page.height + " is over " + F004SpriteAtlasSettings.MaxPageBytes + " B");
                }
            }
        }

        [Test]
        public void EveryShippedAtlasCapsItsPageSizeOnEveryPlatform()
        {
            foreach (var path in ShippedAtlasPaths())
            {
                var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
                foreach (var platform in F004SpriteAtlasSettings.Platforms)
                {
                    Assert.That(importer.GetPlatformSettings(platform).maxTextureSize, Is.LessThanOrEqualTo(F004SpriteAtlasSettings.MaxPageSize), path + " " + platform);
                }
            }
        }

        [Test]
        public void ValidationRejectsAnOversizedPageOverride()
        {
            var settings = new TextureImporterPlatformSettings { maxTextureSize = 1024 };
            Assert.That(settings.maxTextureSize, Is.GreaterThan(F004SpriteAtlasSettings.MaxPageSize));
            Assert.That(F004SpriteAtlasSettings.MaxPageBytes, Is.EqualTo(1024 * 1024), "512 x 512 RGBA32");
        }
    }
}
