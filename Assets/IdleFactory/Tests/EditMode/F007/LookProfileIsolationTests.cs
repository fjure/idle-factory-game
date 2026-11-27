using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IdleFactory.Editor;
using IdleFactory.Unity.Presentation.Look;
using NUnit.Framework;
using UnityEditor;

namespace IdleFactory.Tests.EditMode.F007
{
    /// <summary>
    /// T609 (contracts/look-profile.md rule 4, ARCH-1, QA-3): prototype look profiles are isolated. The build adds the dev-only holder scene only for an
    /// <c>IDLEFACTORY_DEV</c> build, no asset or assembly outside DevTools references a prototype profile, nothing lives under a <c>Resources</c> path, and
    /// the shipped default asset is F006 and never a prototype. The release build report check (<c>BuildReport.packedAssets</c>) belongs to T615.
    /// </summary>
    public sealed class LookProfileIsolationTests
    {
        private const string F006AssetPath = "Assets/IdleFactory/Art/F007/Profiles/F006.asset";
        private const string PrototypeArtRoot = "Assets/IdleFactory/Art/F007/Prototype/";

        [Test]
        public void AReleaseBuildHasTheOneAuthoredSceneAndNoHolderScene()
        {
            Assert.That(PlatformBuildCommands.BuildScenes(null), Is.EqualTo(new[] { PlatformBuildCommands.ScenePath }));
            Assert.That(PlatformBuildCommands.BuildScenes(Array.Empty<string>()), Is.EqualTo(new[] { PlatformBuildCommands.ScenePath }));
            Assert.That(PlatformBuildCommands.BuildScenes(new[] { "SOME_OTHER_DEFINE" }), Is.EqualTo(new[] { PlatformBuildCommands.ScenePath }));
        }

        [Test]
        public void ADevToolsBuildAddsTheHolderSceneAfterTheAuthoredScene()
        {
            var scenes = PlatformBuildCommands.BuildScenes(new[] { "SOME_OTHER_DEFINE", PlatformBuildCommands.DevToolsDefine });
            Assert.That(scenes, Is.EqualTo(new[] { PlatformBuildCommands.ScenePath, PlatformBuildCommands.DevLookProfilesScenePath }));
            Assert.That(PlatformBuildCommands.DevLookProfilesScenePath, Is.EqualTo("Assets/IdleFactory/DevTools/Scenes/F007LookProfiles.unity"));
        }

        [Test]
        public void TheHolderSceneExistsHoldsTheDevToolsHolderAndIsNotInTheEditorBuildSettings()
        {
            var path = PlatformBuildCommands.DevLookProfilesScenePath;
            Assert.That(File.Exists(path), Is.True, "the dev-only holder scene is created through Unity tooling");
            Assert.That(File.ReadAllText(path), Does.Contain("IdleFactory.DevTools.LookProfileHolder"));
            Assert.That(path, Does.Not.Contain("/Resources/"));
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path), Does.Not.Contain(path), "only a DevTools build adds it, per build");
            Assert.That(AssetDatabase.GetDependencies(PlatformBuildCommands.ScenePath, true), Does.Not.Contain(path));
        }

        [Test]
        public void TheShippedDefaultAssetIsF006AndIsNeverAPrototype()
        {
            var asset = AssetDatabase.LoadAssetAtPath<LookProfile>(F006AssetPath);
            Assert.That(asset, Is.Not.Null);
            Assert.That(F006AssetPath, Does.Not.Contain("/Resources/"));
            Assert.That(asset.Id, Is.EqualTo(LookProfile.F006Id));
            Assert.That(asset.IsPrototype, Is.False);
            Assert.That(asset.FirstProblem(), Is.Null);

            var inMemory = LookProfile.CreateF006();
            try
            {
                Assert.That(asset.Id, Is.EqualTo(inMemory.Id));
                Assert.That(asset.IsPrototype, Is.EqualTo(inMemory.IsPrototype));
                Assert.That(asset.UsesShippedSceneCatalog, Is.EqualTo(inMemory.UsesShippedSceneCatalog).And.True);
                Assert.That(asset.RequirementTable.Source, Is.EqualTo(inMemory.RequirementTable.Source).And.EqualTo(LookRequirementSource.F006Registry));
                Assert.That(asset.WorldRenderer, Is.EqualTo(inMemory.WorldRenderer).And.EqualTo(LookWorldRenderer.Renderer2D));
                Assert.That(asset.WorldRendererAsset, Is.Null);
                Assert.That(asset.PixelsPerUnit, Is.EqualTo(inMemory.PixelsPerUnit).And.EqualTo(24));
                Assert.That(asset.Filtering, Is.EqualTo(inMemory.Filtering).And.EqualTo(LookFiltering.Point));
                Assert.That(asset.CameraPolicy, Is.EqualTo(inMemory.CameraPolicy).And.EqualTo(LookCameraPolicy.IntegerScale));
                Assert.That(asset.EnvironmentFamily, Is.EqualTo(inMemory.EnvironmentFamily));
                Assert.That(asset.IndicatorSet, Is.EqualTo(inMemory.IndicatorSet));
                Assert.That(asset.LightingMood, Is.EqualTo(inMemory.LightingMood));
                Assert.That(asset.MeshSet, Is.Null.Or.Empty);
                Assert.That(asset.SpriteCatalog.Count, Is.Zero, "F006 draws the scene catalog; the asset duplicates no sprite list");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inMemory);
            }
        }

        [Test]
        public void NoAssetOutsideTheDevHolderReferencesAPrototypeProfileOrPrototypeArt()
        {
            var prototypeProfiles = AssetDatabase.FindAssets("t:LookProfile")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => AssetDatabase.LoadAssetAtPath<LookProfile>(path).IsPrototype)
                .ToArray();
            var prototypeArt = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith(PrototypeArtRoot, StringComparison.Ordinal)).ToArray();
            var guarded = prototypeProfiles.Concat(prototypeArt).ToArray();

            var mainClosure = AssetDatabase.GetDependencies(PlatformBuildCommands.ScenePath, true);
            foreach (var path in guarded)
            {
                Assert.That(mainClosure, Does.Not.Contain(path), "the authored scene reaches no prototype asset: " + path);
                Assert.That(path, Does.Not.Contain("/Resources/"), "no Resources path (ARCH-1): " + path);
            }

            // Every prototype profile, and every prototype art file, is referenced only by the dev-only scene, by another prototype asset or by itself.
            var holders = AssetDatabase.FindAssets("t:Scene t:Prefab t:ScriptableObject", new[] { "Assets/IdleFactory" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !guarded.Contains(path) && path != PlatformBuildCommands.DevLookProfilesScenePath)
                .ToArray();
            foreach (var holder in holders)
            {
                var dependencies = AssetDatabase.GetDependencies(holder, false);
                Assert.That(dependencies.Intersect(guarded), Is.Empty, holder + " must not reference a prototype asset");
            }
        }

        [Test]
        public void NoAssemblyOutsideTheDevToolsTestsReferencesTheDevToolsAssembly()
        {
            var references = new Regex("\"references\"\\s*:\\s*\\[(?<list>[^\\]]*)\\]", RegexOptions.Singleline);
            var offenders = Directory.GetFiles("Assets", "*.asmdef", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var match = references.Match(File.ReadAllText(path));
                    return match.Success && Regex.IsMatch(match.Groups["list"].Value, "\"IdleFactory\\.DevTools\"");
                })
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => name != "IdleFactory.DevToolsTests")
                .ToArray();
            Assert.That(offenders, Is.Empty, "only the DevTools test assembly may reference the define-gated DevTools assembly");

            var devTools = File.ReadAllText("Assets/IdleFactory/DevTools/IdleFactory.DevTools.asmdef");
            Assert.That(devTools, Does.Contain("\"IDLEFACTORY_DEV\""), "the DevTools assembly stays define-constrained");
        }

        [Test]
        public void NoLookProfileAssetLivesUnderAResourcesFolder()
        {
            var inResources = AssetDatabase.FindAssets("t:LookProfile")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            Assert.That(inResources, Is.Empty);
        }
    }
}
