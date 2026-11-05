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

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T111, rewritten by T476 (owner decision 2026-11-05, option D). The 78 Feature 003 placeholder sprites and their SVG sources are
    /// deleted (git history keeps them), so this class no longer inspects their pixels. It keeps the contract that outlives them: every
    /// content presentation key resolves in the scene sprite catalog, a key missing from the catalog fails the content load that start-up
    /// performs (<c>presentation.key.unresolved</c>), and the scene and the assets hold no trace of the deleted placeholders.
    /// </summary>
    public sealed class F003AssetContractTests
    {
        private const string PlaceholderRoot = "Assets/IdleFactory/Art/Development/F003";
        private const string ContentPath = "Assets/IdleFactory/Content/Iron/iron-factory-content.json";
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";

        [Test]
        public void TheFeature003PlaceholderSpritesAndSourcesAreDeleted()
        {
            Assert.That(Directory.Exists(PlaceholderRoot), Is.False, "owner decision 2026-11-05: the F003 sprites and sources are deleted");
            Assert.That(File.Exists(PlaceholderRoot + ".meta"), Is.False, "the folder meta is deleted with the folder");
            Assert.That(AssetDatabase.IsValidFolder(PlaceholderRoot), Is.False);
            Assert.That(
                AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/IdleFactory/Art/Development" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.IndexOf("f003", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.Empty,
                "no Feature 003 sprite remains under the development art folder (the shipped F004 files keep the f003-* key names under Art/F004)");

            // F001 placeholders are out of scope of the decision and stay.
            Assert.That(Directory.GetFiles("Assets/IdleFactory/Art/Development/F001/Imported", "*.png"), Has.Length.EqualTo(21));
        }

        [Test]
        public void TheSceneCatalogReferencesNoDeletedPlaceholderAndKeepsNoFallbackField()
        {
            var dependencies = AssetDatabase.GetDependencies(ScenePath, true);
            Assert.That(dependencies.Where(path => path.StartsWith(PlaceholderRoot, StringComparison.Ordinal)), Is.Empty);

            // The one-time re-save of T476 dropped the serialized fallback arrays; a later re-save must not bring them back.
            var yaml = File.ReadAllText(ScenePath);
            Assert.That(yaml, Does.Not.Contain("fallbackFrames"));
            Assert.That(yaml, Does.Not.Contain("key: F003-BADGE-RUNNING"), "the never-drawn running badge has no catalog entry");
        }

        [Test]
        public void EveryContentPresentationKeyResolvesInTheSceneCatalogToF004Art()
        {
            var keys = ContentKeys();
            Assert.That(keys, Has.Length.EqualTo(11));

            WithSceneCatalog(catalog =>
            {
                foreach (var key in keys)
                {
                    Assert.That(catalog.TryGetEntry(key, out var entry), Is.True, "presentationKey '" + key + "' is not a registered sprite");
                    Assert.That(AssetDatabase.GetAssetPath(entry.Sprite), Does.StartWith("Assets/IdleFactory/Art/F004/"), key);
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
    }
}
