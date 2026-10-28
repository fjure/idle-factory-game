using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

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

        [Test]
        public void TheSceneHasNoLight2D()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var lights = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                    .Where(component => component != null && component.GetType().Name == "Light2D")
                    .ToArray();
                Assert.That(lights, Is.Empty, "no 2D lights in phase 0");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
