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
    /// Add <c>-noProfilerAutoconnect</c> to the GLES3 batch build for the Unity Profiler (Standalone Process) procedure
    /// in <c>docs/integrations/unity-standalone-profiler-android.md</c>.
    /// Batch entry points exit with code 1 only after every settings restore has run.
    /// </summary>
    public static class PlatformBuildCommands
    {
        public const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";

        private const string OutputArgument = "-buildOutput";

        public const string NoProfilerAutoconnectArgument = "-noProfilerAutoconnect";

        /// <summary>
        /// The development-tools scripting define (F5-R11, AR-10). It is applied per build through
        /// <see cref="BuildPlayerOptions.extraScriptingDefines"/> and is never written to ProjectSettings, so a build cannot
        /// leave settings noise behind and no ordinary build or test run compiles <c>IdleFactory.DevTools</c>.
        /// </summary>
        public const string DevToolsDefine = "IDLEFACTORY_DEV";

        private static readonly string[] DevToolsDefines = { DevToolsDefine };

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

        /// <summary>Windows Mono development player with the DevTools define (acceleration timing runs, OPS-1).</summary>
        public static void BuildWindows64DevelopmentDevTools()
        {
            ExitOnFailure(Run(BuildTarget.StandaloneWindows64, BuildOptions.Development, null, DevToolsDefines));
        }

        /// <summary>Android development player with the DevTools define (emulator acceleration evidence, OPS-1).</summary>
        public static void BuildAndroidArm64DevelopmentDevTools()
        {
            ExitOnFailure(Run(BuildTarget.Android, BuildOptions.Development, ConfigureAndroidArm64Il2Cpp, DevToolsDefines));
        }

        /// <summary>
        /// Non-development Android release player for <c>scripts/check-release-build.ps1</c> (OPS-3): no DevTools define, no
        /// Development flag, no profiler. It sets no keystore, so the scan reads an unsigned-for-release package.
        /// </summary>
        public static void BuildAndroidArm64Release()
        {
            ExitOnFailure(Run(BuildTarget.Android, BuildOptions.None, ConfigureAndroidArm64Il2Cpp, null));
        }

        private static void ConfigureAndroidArm64Il2Cpp()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            Debug.Log("[PlatformBuild] Android: IL2CPP, ARM64, application identifier "
                + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
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
        /// With <c>-noProfilerAutoconnect</c> the build omits <see cref="BuildOptions.ConnectWithProfiler"/> (T064).
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

            var profilerAutoconnect = !HasArgument(Environment.GetCommandLineArgs(), NoProfilerAutoconnectArgument);
            ExitOnFailure(BuildAndroidArm64DevelopmentGles3(output, profilerAutoconnect));
        }

        /// <summary>
        /// Build options of the Android development builds. An autoconnect player connects to the Unity editor on the
        /// build host by itself at startup, so with the editor open the Unity Profiler (Standalone Process) cannot
        /// reliably receive its frames (T064). Profiling builds for the standalone profiler therefore omit it.
        /// </summary>
        public static BuildOptions AndroidDevelopmentBuildOptions(bool profilerAutoconnect)
        {
            return profilerAutoconnect ? BuildOptions.Development | BuildOptions.ConnectWithProfiler : BuildOptions.Development;
        }

        public static bool HasArgument(string[] arguments, string name)
        {
            return arguments != null && Array.IndexOf(arguments, name) >= 0;
        }

        /// <summary>
        /// Editor-callable form of the GLES3 emulator build. It never exits the editor and returns false when the build
        /// fails or when Android Graphics APIs are not automatic before or after it.
        /// </summary>
        public static bool BuildAndroidArm64DevelopmentGles3(string output)
        {
            return BuildAndroidArm64DevelopmentGles3(output, true);
        }

        /// <inheritdoc cref="BuildAndroidArm64DevelopmentGles3(string)"/>
        public static bool BuildAndroidArm64DevelopmentGles3(string output, bool profilerAutoconnect)
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
                built = Run(output, BuildTarget.Android, AndroidDevelopmentBuildOptions(profilerAutoconnect), () =>
                {
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                    PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                    PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
                    Debug.Log("[PlatformBuild] Android: IL2CPP, ARM64, OpenGLES3 only (emulator), profiler autoconnect "
                        + profilerAutoconnect + ", application identifier "
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
            // The project root, independent of the process working directory used to launch Unity.
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var path = Path.Combine(projectRoot ?? Directory.GetCurrentDirectory(), "ProjectSettings", "ProjectSettings.asset");
            string[] leaked;
            try
            {
                leaked = FindNonAutomaticGraphicsApiTargets(File.ReadAllText(path));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogError($"[PlatformBuild] Cannot read {path} {phase}: {exception.Message}");
                return false;
            }

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

        private static bool Run(BuildTarget target, BuildOptions options, Action configure, string[] extraScriptingDefines = null)
        {
            try
            {
                return Run(ReadOutputPath(), target, options, configure, extraScriptingDefines);
            }
            catch (ArgumentException exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private static bool Run(string output, BuildTarget target, BuildOptions options, Action configure, string[] extraScriptingDefines = null)
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
                    extraScriptingDefines = extraScriptingDefines,
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
