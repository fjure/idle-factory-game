using System.Collections;
using System.Linq;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Content;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// SC-008 pre-migration baseline (T070d): median of five
    /// <see cref="DefinitionCatalogBuilder"/>.Build timings against the still-live
    /// ScriptableObject <c>DevelopmentCatalog</c> asset, measured in the built Windows
    /// standalone player (<c>scripts/test-unity.ps1 -StandaloneWindowsPlayer</c>).
    ///
    /// Method used to load the catalog in the player: <c>UnityEditor.AssetDatabase</c> is
    /// Editor-only and unavailable in a built player, and the Editor-only harness
    /// <c>LifecyclePersistenceTests</c> uses (<c>EditorSceneManager.LoadSceneInPlayMode</c>)
    /// does not run outside the Editor either (it calls <c>Assert.Ignore</c> there). Instead,
    /// this test loads <c>Assets/IdleFactory/Scenes/CoreGameFoundation.unity</c> — the sole,
    /// index-0 entry in <c>ProjectSettings/EditorBuildSettings.asset</c>, so it ships inside
    /// this player build — with the ordinary runtime <see cref="SceneManager"/> API, then
    /// reads the catalog asset off <see cref="CoreGameFoundationBootstrap.DevelopmentCatalog"/>.
    /// That field is a serialized scene reference resolved by scene loading itself, so it is
    /// populated whether or not the bootstrap's own <c>Initialize()</c> succeeds.
    /// </summary>
    public sealed class PreMigrationCatalogBuildBaselineTests
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private const int SampleCount = 5;

        [Category("StandalonePlayer")]
        [UnityTest]
        public IEnumerator MedianDefinitionCatalogBuilderBuildTimeIsMeasured()
        {
            var loadOperation = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Assert.That(
                loadOperation,
                Is.Not.Null,
                $"Could not start loading {ScenePath}; it must remain part of the player build's scene list.");

            while (!loadOperation.isDone)
            {
                yield return null;
            }

            var scene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);

            CoreGameFoundationBootstrap bootstrap = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                bootstrap = root.GetComponentInChildren<CoreGameFoundationBootstrap>(true);
                if (bootstrap != null)
                {
                    break;
                }
            }

            Assert.That(bootstrap, Is.Not.Null, "CoreGameFoundationBootstrap was not found in the loaded scene.");
            var catalogAsset = bootstrap.DevelopmentCatalog;
            Assert.That(catalogAsset, Is.Not.Null, "The bootstrap's DevelopmentCatalog reference was not resolved.");

            var stopwatch = new System.Diagnostics.Stopwatch();
            var sampleTicks = new long[SampleCount];
            for (var index = 0; index < SampleCount; index++)
            {
                stopwatch.Restart();
                var built = DefinitionCatalogBuilder.Build(catalogAsset);
                stopwatch.Stop();
                Assert.That(built, Is.Not.Null);
                Assert.That(built.Buildings.Count, Is.EqualTo(1));
                sampleTicks[index] = stopwatch.ElapsedTicks;
            }

            var sortedTicks = sampleTicks.OrderBy(sample => sample).ToArray();
            var medianTicks = sortedTicks[SampleCount / 2];
            var frequency = System.Diagnostics.Stopwatch.Frequency;
            var medianMilliseconds = medianTicks * 1000.0 / frequency;
            var sampleMilliseconds = sortedTicks
                .Select(sample => (sample * 1000.0 / frequency).ToString("F4"))
                .ToArray();

            UnityEngine.Debug.Log(
                "SC-008 pre-migration baseline: median DefinitionCatalogBuilder.Build over " +
                SampleCount + " samples = " + medianMilliseconds.ToString("F4") +
                " ms (sorted samples ms: " + string.Join(", ", sampleMilliseconds) + ")");

            SceneManager.UnloadSceneAsync(scene);

            // The measurement itself is the deliverable (recorded in
            // specs/002-json-content-authoring/reviews/pre-migration-baseline.md); this assertion
            // only guards against a corrupted or negative reading.
            Assert.That(medianMilliseconds, Is.GreaterThanOrEqualTo(0.0));
        }
    }
}
