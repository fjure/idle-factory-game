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
    /// The emulator-only GLES3 variant is <c>BuildAndroidArm64DevelopmentGles3</c> (batch) or
    /// <c>BuildAndroidArm64DevelopmentGles3(outputPath)</c> from an open editor, which never exits the editor.
    /// Batch entry points exit with code 1 only after every settings restore has run.
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
            ExitOnFailure(Run(BuildTarget.StandaloneWindows64, BuildOptions.None, null));
        }

        public static void BuildAndroidArm64Development()
        {
            ExitOnFailure(Run(BuildTarget.Android, BuildOptions.Development | BuildOptions.ConnectWithProfiler, () =>
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                Debug.Log("[PlatformBuild] Android: IL2CPP, ARM64, application identifier "
                    + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
            }));
        }

        /// <summary>
        /// Emulator-only variant: OpenGL ES 3 as the sole graphics API for this build, because the ARM64
        /// translation layer crashes in the emulated Vulkan driver. Project Graphics API settings are restored afterwards,
        /// and the saved <c>ProjectSettings.asset</c> must still use automatic Android Graphics APIs before and after
        /// the build (T056), so the override can never leak into release configuration.
        /// </summary>
        public static void BuildAndroidArm64DevelopmentGles3()
        {
            string output;
            try
            {
                output = ReadOutputPath();
            }
            catch (ArgumentException exception)
            {
                Debug.LogException(exception);
                ExitOnFailure(false);
                return;
            }

            ExitOnFailure(BuildAndroidArm64DevelopmentGles3(output));
        }

        /// <summary>
        /// Editor-callable form of the GLES3 emulator build. It never exits the editor and returns false when the build
        /// fails or when Android Graphics APIs are not automatic before or after it.
        /// </summary>
        public static bool BuildAndroidArm64DevelopmentGles3(string output)
        {
            if (!AndroidGraphicsApisAreAutomatic("before the GLES3 emulator build"))
            {
                return false;
            }

            var activeTarget = EditorUserBuildSettings.activeBuildTarget;
            var activeGroup = BuildPipeline.GetBuildTargetGroup(activeTarget);
            var useDefaults = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            bool built;
            try
            {
                built = Run(output, BuildTarget.Android, BuildOptions.Development | BuildOptions.ConnectWithProfiler, () =>
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

                // BuildPlayer leaves the editor on Android; an in-editor build must not change the active target.
                if (EditorUserBuildSettings.activeBuildTarget != activeTarget)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(activeGroup, activeTarget);
                }
            }

            // Checked only after the restore, so a failed build can never skip it.
            var restored = AndroidGraphicsApisAreAutomatic("after the GLES3 emulator build");
            return built && restored;
        }

        /// <summary>
        /// Returns the build targets whose Graphics API entry in <c>ProjectSettings.asset</c> text is not automatic
        /// (<c>m_Automatic: 0</c>). An empty <c>m_BuildTargetGraphicsAPIs: []</c> list means every target is automatic.
        /// </summary>
        public static string[] FindNonAutomaticGraphicsApiTargets(string projectSettingsText)
        {
            if (projectSettingsText == null)
            {
                throw new ArgumentNullException(nameof(projectSettingsText));
            }

            var result = new System.Collections.Generic.List<string>();
            var inList = false;
            string currentTarget = null;
            foreach (var rawLine in projectSettingsText.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                var trimmed = line.Trim();
                if (!inList)
                {
                    inList = trimmed == "m_BuildTargetGraphicsAPIs:";
                    continue;
                }

                // The list ends at the next sibling key: two-space indentation without a list marker.
                if (line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("   ", StringComparison.Ordinal) &&
                    !trimmed.StartsWith("-", StringComparison.Ordinal))
                {
                    break;
                }

                if (trimmed.StartsWith("- m_BuildTarget:", StringComparison.Ordinal))
                {
                    currentTarget = trimmed.Substring("- m_BuildTarget:".Length).Trim();
                }
                else if (trimmed.StartsWith("m_Automatic:", StringComparison.Ordinal) && currentTarget != null &&
                    trimmed.Substring("m_Automatic:".Length).Trim() != "1")
                {
                    result.Add(currentTarget);
                }
            }

            return result.ToArray();
        }

        private static bool AndroidGraphicsApisAreAutomatic(string phase)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings", "ProjectSettings.asset");
            var leaked = FindNonAutomaticGraphicsApiTargets(File.ReadAllText(path));
            var automaticInMemory = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
            if (automaticInMemory && Array.IndexOf(leaked, "AndroidPlayer") < 0)
            {
                Debug.Log($"[PlatformBuild] Android Graphics APIs are automatic {phase}.");
                return true;
            }

            Debug.LogError($"[PlatformBuild] Android Graphics APIs are not automatic {phase} " +
                $"(in memory automatic: {automaticInMemory}; non-automatic entries in ProjectSettings.asset: " +
                $"{string.Join(", ", leaked)}). Restore automatic Android Graphics APIs; the GLES3 override is emulator-only.");
            return false;
        }

        private static void ExitOnFailure(bool succeeded)
        {
            if (!succeeded)
            {
                EditorApplication.Exit(1);
            }
        }

        private static bool Run(BuildTarget target, BuildOptions options, Action configure)
        {
            try
            {
                return Run(ReadOutputPath(), target, options, configure);
            }
            catch (ArgumentException exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private static bool Run(string output, BuildTarget target, BuildOptions options, Action configure)
        {
            try
            {
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
                return summary.result == BuildResult.Succeeded;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
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
