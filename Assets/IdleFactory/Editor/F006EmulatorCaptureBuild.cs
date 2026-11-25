using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IdleFactory.Editor
{
    /// <summary>
    /// T562 (Feature 006): the CAPTURE-ONLY Android build for the emuP trial captures. It is the GLES3 emulator development build of <see cref="PlatformBuildCommands"/> plus the test
    /// assemblies (<see cref="BuildOptions.IncludeTestAssemblies"/>) and the scripting define <c>IDLEFACTORY_T562_CAPTURE</c> that switches on
    /// <c>F006TrialCaptureAndroidLauncher</c>; the define is applied per build (like the DevTools define) and never written to ProjectSettings, so no other build contains the driver.
    /// The first scene is a temporary empty launcher scene (deleted after the build), the authored scene is the second one and is loaded additively by the driver.
    ///
    /// Usage: Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod IdleFactory.Editor.F006EmulatorCaptureBuild.Build -buildOutput test-results/t562/capture.apk
    /// Exit code 1 only after the settings restore ran. Run it only under the unity and emulator leases of the owning Issue.
    /// </summary>
    public static class F006EmulatorCaptureBuild
    {
        public const string CaptureDefine = "IDLEFACTORY_T562_CAPTURE";
        private const string LauncherScenePath = "Assets/IdleFactory/Editor/T562CaptureLauncher.unity";
        private const string OutputArgument = "-buildOutput";

        public static void Build()
        {
            var succeeded = false;
            try
            {
                succeeded = BuildApk(ReadOutputPath());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (!succeeded)
            {
                EditorApplication.Exit(1);
            }
        }

        public static bool BuildApk(string output)
        {
            var useDefaults = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            var built = false;
            try
            {
                var launcher = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(launcher, LauncherScenePath);
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { LauncherScenePath, PlatformBuildCommands.ScenePath },
                    locationPathName = output,
                    target = BuildTarget.Android,
                    options = BuildOptions.Development | BuildOptions.IncludeTestAssemblies,
                    extraScriptingDefines = new[] { CaptureDefine },
                });
                var summary = report.summary;
                Debug.Log($"[T562Capture] Android result {summary.result}, {summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalSize} bytes, {summary.totalTime}, output {summary.outputPath}");
                built = summary.result == BuildResult.Succeeded;
            }
            finally
            {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, apis);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, useDefaults);
                AssetDatabase.DeleteAsset(LauncherScenePath);
                AssetDatabase.SaveAssets();
            }

            return built;
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
