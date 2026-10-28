using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T411 (FR-004, FR-005, SC-008; research R01; contracts/render-foundation.md): the phase-0 render pipeline switch is in force.
    /// <c>scripts/check-settings-noise.ps1</c> inspects only <c>ProjectSettings.asset</c> and <c>EditorSettings.asset</c>, so
    /// this class is the guard that the URP asset is assigned in Graphics settings and in every quality level, that the
    /// global settings asset is registered, and that the parity settings (Gamma, Unlit default sprite material, HDR, depth
    /// and opaque textures, MSAA and post-processing off) stay in place. It reads URP types through serialized properties and
    /// type names so the test assembly needs no reference to the URP assemblies.
    /// </summary>
    public sealed class RenderPipelineConfigurationTests
    {
        private const string RenderingDirectory = "Assets/IdleFactory/Rendering";
        private const string PipelinePath = RenderingDirectory + "/IdleFactoryURP.asset";
        private const string RendererPath = RenderingDirectory + "/IdleFactory2DRenderer.asset";
        private const string GlobalSettingsPath = RenderingDirectory + "/UniversalRenderPipelineGlobalSettings.asset";
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private const string UniversalPipelineAssetType = "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset";
        private const string Renderer2DDataType = "UnityEngine.Rendering.Universal.Renderer2DData";
        private const string AdditionalCameraDataType = "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData";

        private static RenderPipelineAsset LoadPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelinePath);
            Assert.That(pipeline, Is.Not.Null, PipelinePath + " exists and is a render pipeline asset");
            return pipeline;
        }

        private static string ProjectSettingsText(string fileName) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ProjectSettings", fileName)));

        [Test]
        public void ThePipelineAssetIsAUniversalRenderPipelineAssetOfTheProject()
        {
            var pipeline = LoadPipeline();
            Assert.That(pipeline.GetType().FullName, Is.EqualTo(UniversalPipelineAssetType));
        }

        [Test]
        public void TheDefaultRenderPipelineInGraphicsSettingsIsTheProjectAsset()
        {
            Assert.That(AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline), Is.EqualTo(PipelinePath));
        }

        [Test]
        public void EveryQualityLevelUsesTheProjectAsset()
        {
            Assert.That(QualitySettings.count, Is.GreaterThan(0));
            for (var level = 0; level < QualitySettings.count; level++)
            {
                var assigned = QualitySettings.GetRenderPipelineAssetAt(level);
                Assert.That(AssetDatabase.GetAssetPath(assigned), Is.EqualTo(PipelinePath), "quality level " + level + " (" + QualitySettings.names[level] + ")");
            }
        }

        [Test]
        public void TheGlobalSettingsAssetExistsAndIsRegisteredInGraphicsSettings()
        {
            var globalSettings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(GlobalSettingsPath);
            Assert.That(globalSettings, Is.Not.Null, GlobalSettingsPath + " exists");
            Assert.That(globalSettings.GetType().Name, Is.EqualTo("UniversalRenderPipelineGlobalSettings"));

            var guid = AssetDatabase.AssetPathToGUID(GlobalSettingsPath);
            var graphics = ProjectSettingsText("GraphicsSettings.asset");
            Assert.That(graphics, Does.Contain("UnityEngine.Rendering.Universal.UniversalRenderPipeline: {fileID: 11400000, guid: " + guid), "m_RenderPipelineGlobalSettingsMap registers the project's global settings");
            Assert.That(AssetDatabase.FindAssets("t:UniversalRenderPipelineGlobalSettings"), Has.Length.EqualTo(1), "exactly one global settings asset exists");
        }

        [Test]
        public void TheColorSpaceStaysGamma()
        {
            Assert.That(PlayerSettings.colorSpace, Is.EqualTo(ColorSpace.Gamma));
        }

        [Test]
        public void ThePipelineUsesTheProject2DRendererAsItsOnlyAndDefaultRenderer()
        {
            var serialized = new SerializedObject(LoadPipeline());
            var renderers = serialized.FindProperty("m_RendererDataList");
            Assert.That(renderers.arraySize, Is.EqualTo(1), "one renderer");
            var renderer = renderers.GetArrayElementAtIndex(0).objectReferenceValue;
            Assert.That(renderer, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(renderer), Is.EqualTo(RendererPath));
            Assert.That(renderer.GetType().FullName, Is.EqualTo(Renderer2DDataType), "the 2D Renderer");
            Assert.That(serialized.FindProperty("m_DefaultRendererIndex").intValue, Is.EqualTo(0));
        }

        [Test]
        public void HdrDepthAndOpaqueTexturesAndMsaaAreOff()
        {
            var serialized = new SerializedObject(LoadPipeline());
            Assert.That(serialized.FindProperty("m_SupportsHDR").boolValue, Is.False, "HDR off");
            Assert.That(serialized.FindProperty("m_RequireDepthTexture").boolValue, Is.False, "depth texture off");
            Assert.That(serialized.FindProperty("m_RequireOpaqueTexture").boolValue, Is.False, "opaque texture off");
            Assert.That(serialized.FindProperty("m_MSAA").intValue, Is.EqualTo(1), "MSAA disabled (sample count 1), as in the Built-in baselines");
        }

        [Test]
        public void TheDefaultSpriteMaterialTypeOfThe2DRendererIsUnlit()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RendererPath);
            Assert.That(renderer, Is.Not.Null, RendererPath + " exists");
            var serialized = new SerializedObject(renderer);
            var materialType = serialized.FindProperty("m_DefaultMaterialType");
            Assert.That(materialType, Is.Not.Null);
            Assert.That(materialType.enumNames[materialType.enumValueIndex], Is.EqualTo("Unlit"));
        }

        [Test]
        public void ACodeCreatedSpriteRendererGetsTheUnlitDefaultMaterial()
        {
            var host = new GameObject("t411-sprite-material-probe");
            try
            {
                var spriteRenderer = host.AddComponent<SpriteRenderer>();
                Assert.That(spriteRenderer.sharedMaterial, Is.Not.Null);
                Assert.That(spriteRenderer.sharedMaterial.shader.name, Does.Contain("Unlit"), "default sprite material shader: " + spriteRenderer.sharedMaterial.shader.name);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TheFactoryCameraUsesRendererZeroWithPostProcessingOff()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var cameras = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();
                Assert.That(cameras.Length, Is.EqualTo(1), "the scene has the single factory camera");
                var data = cameras[0].GetComponent(AdditionalCameraDataType);
                Assert.That(data, Is.Not.Null, "the camera carries URP additional camera data");
                var serialized = new SerializedObject(data);
                Assert.That(serialized.FindProperty("m_RendererIndex").intValue, Is.EqualTo(0), "renderer index 0, the 2D Renderer");
                Assert.That(serialized.FindProperty("m_RenderPostProcessing").boolValue, Is.False, "post-processing off");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private const string Light2DTypeName = "UnityEngine.Rendering.Universal.Light2D";

        /// <summary>The Light2D components of <paramref name="scene"/>, matched by full type name so the test assembly needs no URP reference.</summary>
        private static Component[] FindLight2D(Scene scene) =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                .Where(component => component != null && component.GetType().FullName == Light2DTypeName)
                .ToArray();

        [Test]
        public void TheSceneHasNoLight2D()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Assert.That(FindLight2D(scene), Is.Empty, "no 2D lights in phase 0");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // T457 (phase0-review F5): positive control. The Light2D check above matches by type name, so it would pass vacuously
        // if URP renamed or moved the type. Prove that the type exists under that name and that the same finder detects one.
        [Test]
        public void TheLight2DCheckDetectsALight2DInAThrowawayScene()
        {
            var light2D = TypeCache.GetTypesDerivedFrom<Component>().SingleOrDefault(type => type.FullName == Light2DTypeName);
            Assert.That(light2D, Is.Not.Null, Light2DTypeName + " still exists under that name in the installed URP");

            // The Edit Mode runner keeps an untitled scene open, which forbids creating another one additively; plant the probe in the open scene.
            var host = new GameObject("t457-light2d-control");
            try
            {
                Assert.That(FindLight2D(host.scene).Any(light => light.gameObject == host), Is.False, "the probe starts without a Light2D");
                host.AddComponent(light2D);
                Assert.That(FindLight2D(host.scene).Count(light => light.gameObject == host), Is.EqualTo(1), "the finder detects the planted Light2D");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        // T457 (phase0-review F4): the URP package pin and the Renderer2D post-processing state.
        // -----------------------------------------------------------------------------------------------------------

        private const string UrpPackageName = "com.unity.render-pipelines.universal";
        private const string UrpPackageVersion = "17.6.0";

        private static string ProjectFileText(string relativePath) =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath))).Replace("\r\n", "\n");

        [Test]
        public void TheUrpPackageIsPinnedAt1760InTheManifestTheLockFileAndTheResolvedPackage()
        {
            var name = Regex.Escape(UrpPackageName);
            var version = Regex.Escape(UrpPackageVersion);
            Assert.That(ProjectFileText("Packages/manifest.json"), Does.Match("\"" + name + "\"\\s*:\\s*\"" + version + "\""), "Packages/manifest.json pins " + UrpPackageName);
            Assert.That(ProjectFileText("Packages/packages-lock.json"), Does.Match("\"" + name + "\"\\s*:\\s*\\{\\s*\"version\"\\s*:\\s*\"" + version + "\""), "Packages/packages-lock.json resolves " + UrpPackageName);

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + UrpPackageName);
            Assert.That(package, Is.Not.Null, "the URP package is resolved by the Package Manager");
            Assert.That(package.version, Is.EqualTo(UrpPackageVersion));
        }

        [Test]
        public void TheRenderer2DLoadsNoPostProcessingResourcesBecausePostProcessingIsOffInPhase0()
        {
            // N-4 (polish review): a Renderer2D with a PostProcessData asset builds its post-processing passes at start-up and loads their
            // shaders. The URP global settings strip the post-processing shaders the default volume profile does not use, so the load logged
            // "Hidden/Universal Render Pipeline/GaussianDepthOfField is not supported or has been stripped" (22 entries) in every player.
            // Phase 0 renders without post-processing, so the renderer carries no PostProcessData and the stripping settings stay as they are.
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RendererPath);
            Assert.That(renderer, Is.Not.Null, RendererPath + " exists");
            var postProcessData = new SerializedObject(renderer).FindProperty("m_PostProcessData");
            Assert.That(postProcessData, Is.Not.Null, "Renderer2DData still serializes m_PostProcessData");
            Assert.That(postProcessData.objectReferenceValue, Is.Null, "no PostProcessData on the 2D Renderer (post-processing off, no stripped-shader load at start-up)");
        }

        // -----------------------------------------------------------------------------------------------------------
        // T457 (phase0-review A8): guard on the URP global settings asset, which Unity re-serializes during test runs and builds.
        // -----------------------------------------------------------------------------------------------------------

        private static readonly string[] StrippingKeys =
        {
            "m_StripUnusedPostProcessingVariantsAndResources",
            "m_StripUnusedVariants",
            "m_StripScreenCoordOverrideVariants",
            "m_Strip2DUnusedVariants",
        };

        /// <summary>
        /// Problems of the global settings asset text: an emptied <c>m_Settings.m_SettingsList.m_List</c>, or a managed
        /// <c>URPShaderStrippingSetting</c> that lost its authoritative values (the legacy top-level fields are ignored remnants
        /// and are not inspected). They keep the Android build at 490 shader variants instead of 2262.
        /// </summary>
        internal static string[] FindGlobalSettingsProblems(string assetText)
        {
            var text = assetText.Replace("\r\n", "\n");
            var problems = new List<string>();

            if (!Regex.IsMatch(text, @"m_SettingsList:\n\s+m_List:\n(?:\s+- rid: -?\d+\n)+"))
            {
                problems.Add("m_Settings.m_SettingsList.m_List has no entries");
            }

            // Unity also emptied the sibling list during a T457 editor run (the runtime subset), so it is guarded the same way.
            if (!Regex.IsMatch(text, @"m_RuntimeSettings:\n\s+m_List:\n(?:\s+- rid: -?\d+\n)+"))
            {
                problems.Add("m_Settings.m_RuntimeSettings.m_List has no entries");
            }

            var stripping = Regex.Match(text, @"class: URPShaderStrippingSetting[^\n]*\n\s+data:\n((?:\s+m_\w+: [^\n]*\n)+)");
            if (!stripping.Success)
            {
                problems.Add("the managed URPShaderStrippingSetting entry is missing");
            }
            else
            {
                foreach (var key in StrippingKeys)
                {
                    if (!Regex.IsMatch(stripping.Groups[1].Value, @"\s" + key + @": 1\n"))
                    {
                        problems.Add("URPShaderStrippingSetting." + key + " is not 1");
                    }
                }
            }

            return problems.ToArray();
        }

        [Test]
        public void TheGlobalSettingsAssetKeepsItsSettingsListAndTheAuthoritativeStrippingValues()
        {
            Assert.That(FindGlobalSettingsProblems(ProjectFileText(GlobalSettingsPath)), Is.Empty, GlobalSettingsPath + " (a noisy Unity re-save? restore it with git checkout)");

            var serialized = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(GlobalSettingsPath));
            var settingsList = serialized.FindProperty("m_Settings.m_SettingsList.m_List");
            Assert.That(settingsList, Is.Not.Null, "the settings list is serialized");
            Assert.That(settingsList.arraySize, Is.GreaterThan(0), "the loaded asset has settings entries");
        }

        [Test]
        public void TheGlobalSettingsGuardFailsOnAnEmptiedListAndOnLostOrFlippedStrippingValues()
        {
            var good = ProjectFileText(GlobalSettingsPath);
            Assert.That(FindGlobalSettingsProblems(good), Is.Empty, "the committed asset passes");

            var emptied = Regex.Replace(good, @"m_SettingsList:\n(\s+)m_List:\n(?:\s+- rid: -?\d+\n)+", "m_SettingsList:\n$1m_List: []\n");
            Assert.That(emptied, Is.Not.EqualTo(good), "the control really emptied the list");
            Assert.That(FindGlobalSettingsProblems(emptied), Has.Some.Contains("m_SettingsList"), "an emptied m_List is reported");

            var runtimeEmptied = Regex.Replace(good, @"m_RuntimeSettings:\n(\s+)m_List:\n(?:\s+- rid: -?\d+\n)+", "m_RuntimeSettings:\n$1m_List: []\n");
            Assert.That(runtimeEmptied, Is.Not.EqualTo(good), "the control really emptied the runtime list");
            Assert.That(FindGlobalSettingsProblems(runtimeEmptied), Has.Some.Contains("m_RuntimeSettings"), "an emptied runtime list is reported");

            var anchor = good.IndexOf("class: URPShaderStrippingSetting", StringComparison.Ordinal);
            Assert.That(anchor, Is.GreaterThan(0));
            foreach (var key in StrippingKeys)
            {
                var original = key + ": 1";
                var at = good.IndexOf(original, anchor, StringComparison.Ordinal);
                Assert.That(at, Is.GreaterThan(anchor), key + " is 1 in the managed entry");
                var flipped = good.Substring(0, at) + key + ": 0" + good.Substring(at + original.Length);
                Assert.That(FindGlobalSettingsProblems(flipped), Has.Some.Contains(key), "a flipped " + key + " is reported");
            }

            var withoutManaged = good.Replace("class: URPShaderStrippingSetting", "class: SomethingElse");
            Assert.That(FindGlobalSettingsProblems(withoutManaged), Has.Some.Contains("URPShaderStrippingSetting"), "a missing managed entry is reported");
        }
    }
}
