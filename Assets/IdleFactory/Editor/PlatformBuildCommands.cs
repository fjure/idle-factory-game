using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace IdleFactory.Editor
{
    /// <summary>
    /// Batch-mode build entry points for reproducible development builds of the single authored scene.
    /// Usage: Unity.exe -batchmode -quit -projectPath . -buildTarget Android
    ///   -executeMethod IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64Development
    ///   -buildOutput test-results/android/CoreGameFoundation.apk
    /// </summary>
    public static class PlatformBuildCommands
    {
        public const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";

        private const string OutputArgument = "-buildOutput";

        public static void EnsureBuildScene()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 1 && scenes[0].enabled && scenes[0].path == ScenePath)
            {
                return;
            }

            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ScenePath)))
            {
                throw new InvalidOperationException($"Build scene is missing: {ScenePath}");
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[PlatformBuild] Build scene list set to the single enabled scene {ScenePath}.");
        }

        public static void BuildWindows64()
        {
            Run(BuildTarget.StandaloneWindows64, BuildOptions.None, null);
        }

        public static void BuildAndroidArm64Development()
        {
            Run(BuildTarget.Android, BuildOptions.Development | BuildOptions.ConnectWithProfiler, () =>
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                Debug.Log("[PlatformBuild] Android: IL2CPP, ARM64, application identifier "
                    + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
            });
        }

        /// <summary>
        /// Emulator-only variant: OpenGL ES 3 as the sole graphics API for this build, because the ARM64
        /// translation layer crashes in the emulated Vulkan driver. Project Graphics API settings are restored afterwards.
        /// </summary>
        public static void BuildAndroidArm64DevelopmentGles3()
        {
            var useDefaults = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            try
            {
                Run(BuildTarget.Android, BuildOptions.Development | BuildOptions.ConnectWithProfiler, () =>
                {
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                    PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                    PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
                    Debug.Log("[PlatformBuild] Android: IL2CPP, ARM64, OpenGLES3 only (emulator), application identifier "
                        + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
                });
            }
            finally
            {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, apis);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, useDefaults);
                AssetDatabase.SaveAssets();
            }
        }

        private static void Run(BuildTarget target, BuildOptions options, Action configure)
        {
            try
            {
                var output = ReadOutputPath();
                EnsureBuildScene();
                configure?.Invoke();
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = target,
                    options = options,
                });

                var summary = report.summary;
                Debug.Log($"[PlatformBuild] {target} result {summary.result}, {summary.totalErrors} errors, "
                    + $"{summary.totalWarnings} warnings, {summary.totalSize} bytes, {summary.totalTime}, output {summary.outputPath}");
                if (summary.result != BuildResult.Succeeded)
                {
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static string ReadOutputPath()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (arguments[index] == OutputArgument)
                {
                    return arguments[index + 1];
                }
            }

            throw new ArgumentException($"Missing required {OutputArgument} <path> argument.");
        }
    }
}
