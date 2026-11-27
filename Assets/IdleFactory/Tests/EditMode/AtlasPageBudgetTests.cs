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

            if (Directory.Exists(F007SpriteAtlasSettings.AtlasFolder))
            {
                paths.AddRange(Directory.GetFiles(F007SpriteAtlasSettings.AtlasFolder, "*" + F004SpriteAtlasSettings.AtlasExtension, SearchOption.AllDirectories)
                    .Select(file => file.Replace((char)92, '/')));
            }

            return paths;
        }

        /// <summary>
        /// T613 (research R13 amendment, TA-3): the F007 look atlases obey the same strict 1 MiB page rule. A look can only raise its page cap together
        /// with this constant and the T614 verdict (Pixel pass plus owner acceptance at M4); until then a look that plans a bigger page fails here.
        /// </summary>
        [Test]
        public void EveryF007LookPlansPagesWithinTheStrictOneMiBBudget()
        {
            Assert.That(F004SpriteAtlasSettings.MaxPageBytes, Is.EqualTo(1024 * 1024), "the page rule in force is 1 MiB");
            Assert.That(F007SpriteAtlasSettings.MaxPageBytes, Is.EqualTo(1024 * 1024));
            foreach (var look in new[] { F007Look.A, F007Look.B, F007Look.C })
            {
                Assert.That(F007SpriteAtlasSettings.PageBytes(F007SpriteAtlasSettings.MaxPageSize), Is.LessThanOrEqualTo(F004SpriteAtlasSettings.MaxPageBytes), look + " page plan");
                var rules = F007ImportRules.For(look);
                Assert.That(rules.AtlasPadding, Is.GreaterThanOrEqualTo(1 << rules.MipLevels), look + " padding covers its mips");
            }
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
            // A real importer: a copy of a shipped atlas gets a 1024 page override on every platform, and the validator must name it (A-10: the old
            // test only compared constants). The unchanged source is the control and must carry no page problem.
            var source = ShippedAtlasPaths().First();
            var control = F004SpriteAtlasSettings.Validate((SpriteAtlasImporter)AssetImporter.GetAtPath(source));
            Assert.That(control.Any(problem => problem.Contains("max page")), Is.False, "the shipped atlas stays within the page limit: " + string.Join("; ", control));

            var copy = "Assets/IdleFactory/Tests/EditMode/OversizedAtlasProbe" + F004SpriteAtlasSettings.AtlasExtension;
            try
            {
                Assert.That(AssetDatabase.CopyAsset(source, copy), Is.True, "the probe copy is created");
                var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(copy);
                foreach (var platform in F004SpriteAtlasSettings.Platforms)
                {
                    var settings = importer.GetPlatformSettings(platform);
                    settings.overridden = true;
                    settings.maxTextureSize = 1024;
                    importer.SetPlatformSettings(settings);
                }

                importer.SaveAndReimport();
                var problems = F004SpriteAtlasSettings.Validate((SpriteAtlasImporter)AssetImporter.GetAtPath(copy));
                foreach (var platform in F004SpriteAtlasSettings.Platforms)
                {
                    Assert.That(problems.Any(problem => problem.StartsWith(platform + ": max page 1024", System.StringComparison.Ordinal)), Is.True,
                        platform + " reports the oversized page: " + string.Join("; ", problems));
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(copy);
            }
        }
    }
}
