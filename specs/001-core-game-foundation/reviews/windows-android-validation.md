# T038 Windows and Android Validation

**Status: Windows build PASS. Android ARM64 Development Build, install, launch and scene rendering PASS on the emulator (with the documented GLES3 emulator build variant). Android runtime acceptance FAIL/UNRESOLVED.** The Unity Player now runs the authored scene on the named API 36 AVD through its `arm64-v8a` translation layer. The runtime journey exposed three product findings and one unresolved check:

- **F1 — checkpoint save fails in player builds:** a player save reports *"The saved factory could not be validated. Your live factory is unchanged."* No checkpoint file is written, and lifecycle departures also leave `IdleFactory/Checkpoints/v1` empty.
- **F2 — HUD panels overlap on a portrait phone.**
- **F3 — no D001 buildings are visible in the running scene.**
- **Autorotation is unresolved.**

This is owner-approved, experimental emulator development evidence only. Physical-device support and release readiness remain unverified because Unity does not support emulator hardware as an Android Player target.

Observed on 2026-09-25 (Europe/Berlin) in the Issue #38 worktree. The worktree was brought up to date by merging `origin/main` `e0ae260`.

## Environment

| Check | Result |
|---|---|
| Host | Windows 11 Pro `10.0.26200`; GPUs NVIDIA GeForce RTX 5070 Ti (driver `591.86`) and AMD Radeon Graphics. |
| Unity | `6000.6.0f1` (`f7f8ed4d1e24`) at `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`. Player modules: `AndroidPlayer` (with bundled `NDK`, `SDK`, `OpenJDK`), `iOSSupport`, `WebGLSupport`, `windowsstandalonesupport`. |
| Unity MCP | Not available in this agent session. No Unity MCP tool binding is registered, no editor was running, and `http://127.0.0.1:8080/mcp` refused connections. The pinned Unity CLI was used as the fallback documented in `docs/integrations/mcp-readiness.md`. |
| Android SDK / ADB | `%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe`; `emulator-5554 device product:sdk_gphone64_x86_64 model:sdk_gphone64_x86_64 device:emu64xa`. |
| Emulator | Android Emulator `35.5.10.0` (build `13402964`), AVD `Medium_Phone_API_36.0`, image `google/sdk_gphone64_x86_64/emu64xa:16/BP22.250325.006/13344233:user/release-keys`. |
| Guest | Android `16`, API `36`, display `1080x2400`, density `420` (`sw411dp`, portrait). |
| ABI / native bridge | `ro.product.cpu.abilist=x86_64,arm64-v8a`; `ro.dalvik.vm.native.bridge=libndk_translation.so` (Berberis aarch64 translation). |
| Physical Android device | None connected. |

## Build scene list and build commands

`ProjectSettings/EditorBuildSettings.asset` previously had `m_Scenes: []`, which blocked the first Windows build attempt. `IdleFactory.Editor.PlatformBuildCommands.EnsureBuildScene` set it through `EditorBuildSettings.scenes`. The list now contains exactly one enabled scene, `Assets/IdleFactory/Scenes/CoreGameFoundation.unity` (GUID `ace6da6b625264446a5a23760fe1b9cb`). Every build also passes that single scene explicitly.

`Assets/IdleFactory/Editor/PlatformBuildCommands.cs` (editor-only assembly `IdleFactory.Editor`) provides the batch entry points below. Each exits non-zero on failure. Outputs go to the ignored `test-results/` tree.

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
$project = (Get-Location).Path

# Windows x64 player (non-development)
& $unity -batchmode -quit -projectPath $project -buildTarget Win64 `
  -executeMethod IdleFactory.Editor.PlatformBuildCommands.BuildWindows64 `
  -buildOutput "$project\test-results\platform\windows\idle-factory-game.exe" `
  -logFile "$project\test-results\platform\windows\WindowsBuild-t038.log"

# Android ARM64 IL2CPP Development Build + Autoconnect Profiler, project Graphics APIs (automatic)
& $unity -batchmode -quit -projectPath $project -buildTarget Android `
  -executeMethod IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64Development `
  -buildOutput "$project\test-results\android\CoreGameFoundation.apk" `
  -logFile "$project\test-results\android\AndroidBuild.log"

# Emulator-only variant: same, but OpenGLES3 as the sole Graphics API for this build only
& $unity -batchmode -quit -projectPath $project -buildTarget Android `
  -executeMethod IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3 `
  -buildOutput "$project\test-results\android\CoreGameFoundation-gles3.apk" `
  -logFile "$project\test-results\android\AndroidBuild-gles3.log"
```

Both Android entry points set IL2CPP for the Android named build target and set ARM64 as the target architecture. The GLES3 variant overrides the Android Graphics APIs only for that build and restores them afterwards. The committed Player Settings change is `scriptingBackend: Android: 1` (IL2CPP). The project already stored `AndroidTargetArchitectures: 2` (ARM64), and the Android Graphics APIs remain automatic (`m_BuildTargetGraphicsAPIs: []`).

Unity also re-serialized unrelated defaults into `ProjectSettings.asset` during builds and test runs: empty iOS and Android icon slots, and an explicit automatic Graphics API entry. Those unintended changes were removed. The committed diff of `ProjectSettings.asset` contains only the IL2CPP line. The application identifier is the unchanged default `com.DefaultCompany.idlefactorygame`, with version `1.0` (code 1), min SDK 26 and automatic target SDK 36.

Two operational notes for repeat runs:

- Android builds restart the ADB daemon. Immediately afterwards, `adb install` can fail with `device offline`. Run `adb wait-for-device` and check `sys.boot_completed` before installing.
- In Windows PowerShell 5.1, `Start-Process -Wait` also waits for the Gradle daemon left behind by the Android build. Wait on the Unity process itself instead.

## Windows result: PASS

| Item | Evidence |
|---|---|
| Result | `Build Finished, Result: Success.`; `[PlatformBuild] StandaloneWindows64 result Succeeded, 0 errors, 0 warnings, 98894584 bytes, 00:00:34.55`; batch exit code `0`. |
| Artifact | `test-results/platform/windows/idle-factory-game.exe`, SHA-256 `a4c71b1bfc3e42d02f2f129fe04adef3ebf2343a662884e4b99598e9dfc90e7d`. |
| Smoke launch | Windowed launch at `1280x720` (`WindowsPlayer-smoke.log`). The process was alive after 12 s, and the log has no `Exception` or `Error` lines. A second windowed run at `540x1200` (`WindowsPlayer-portrait.log`) initialized Direct3D 12 on the RTX 5070 Ti and shut down cleanly. |
| Persistence cross-check | After the Windows player was closed normally, `%USERPROFILE%\AppData\LocalLow\DefaultCompany\idle-factory-game\IdleFactory\Checkpoints\v1` existed but was empty. This matches Android finding F1, so F1 is not emulator-specific. |

## Android builds: PASS

| APK | Result | Size / SHA-256 |
|---|---|---|
| `CoreGameFoundation.apk` (automatic Graphics APIs → Vulkan first) | `Build Finished, Result: Success.`; `[PlatformBuild] Android result Succeeded, 0 errors, 0 warnings`; build time 7 min 21 s (`AndroidBuild.log`). | 36,313,842 bytes; `7a6b3bd01f1f688b431ddbef8e6edd6e4662f505ac6201a4f5cd10c4be3eb8b1` |
| `CoreGameFoundation-gles3.apk` (OpenGLES3 only) | `[PlatformBuild] Android: IL2CPP, ARM64, OpenGLES3 only (emulator)`; `Build Finished, Result: Success.`; 0 errors, 0 warnings (`AndroidBuild-gles3.log`). | 36,313,842 bytes; `b64c6b8f447d32248e83d72bd553342dd8707bfc22c7224652584bd1db2f4c9a` |

## Android install and launch attempts

Every run used the fail-fast sequence from `quickstart.md`: `adb install -r`, `logcat -c`, `monkey -p com.DefaultCompany.idlefactorygame -c android.intent.category.LAUNCHER 1`, `pidof`, then `logcat -d`.

| Run | Emulator GPU / APK | Outcome |
|---|---|---|
| 0 | as found (`-no-window -gpu swiftshader_indirect`), Vulkan-first APK | Install failed with `adb.exe: device offline` right after the build restarted ADB. Not evidence; the run was repeated. |
| 1 | as found, Vulkan-first APK | Install `Success`; PID `9430`; `primaryCpuAbi=arm64-v8a`; `Build type 'Development', Scripting Backend 'il2cpp', CPU 'arm64-v8a'`. Unity's unsupported-hardware dialog appeared and the run continued past it. The guest offered only `OpenGL ES 3.0 SwiftShader`. Unity rejected SwiftShader Vulkan (`Vulkan detection: 0`) and failed EGL context creation with `EGL_BAD_CONFIG` (`no ES 3.2 support`, then `no ES 3.1 support`). Modal: *"Unable to initialize the Unity Engine Graphics API."* |
| 2 | Relaunched with `-gpu host` (quick-boot snapshot), Vulkan-first APK | The guest still reported SwiftShader ES 3.0. The emulator logged `change of renderer detected`, because the quick-boot snapshot restored the old renderer. Unity's Vulkan device then crashed: `Fatal signal 11 ... (UnityGfxDeviceW)`. |
| 3 | **Cold boot** `-gpu host -no-snapshot-load -no-snapshot-save`, Vulkan-first APK | SurfaceFlinger reported `OpenGL ES 3.1 (4.5.0 NVIDIA 591.86)` on the `NVIDIA GeForce RTX 5070 Ti`, with `ro.opengles.version=196609`. Unity selected Vulkan (`Vulkan renderer=[NVIDIA GeForce RTX 5070 Ti]`), and the process crashed again with SIGSEGV in `UnityGfxDeviceW`. The tombstone backtrace (frame #00) shows `/vendor/lib64/hw/vulkan.ranchu.so (vk_common_SetDebugUtilsObjectNameEXT+283)`, called through `libndk_translation_proxy_libvulkan.so` (a Berberis trampoline). This is an emulator-side translation/driver defect when a translated ARM64 app uses Vulkan debug utilities. |
| 4 | Same cold-booted host-GPU emulator, **GLES3-only APK** | Install `Success`, launch `Events injected: 1`, PID `3979` still alive after 25 s. `Renderer: Android Emulator OpenGL ES Translator (NVIDIA GeForce RTX 5070 Ti/PCIe/SSE2)`, `Version: OpenGL ES 3.1`, `Creating OpenGL ES 3.1 graphics device`. **The scene renders** (`screen-run4-gles3-a.png`), showing the factory grid, HUD and the `Development Build` watermark. |

Emulator solution: no newer emulator was required. The working configuration is a **cold boot with host GPU** (`emulator -avd Medium_Phone_API_36.0 -gpu host -no-snapshot-load`) plus the **GLES3-only** Development APK. The Vulkan-first APK remains blocked on this emulator by the translated-Vulkan crash above. After the checks, the emulator was left running in the cold-booted host-GPU configuration.

## Android runtime journey (run 4)

| Required check | Result | Evidence |
|---|---|---|
| Touch | **PASS (input path)**. A single tap on the HUD save control was received and executed; the HUD switched to the red failure state described under F1 (`screen-run4-save-tap.png`). A tap on an empty grid cell in Browse mode produced no visible change, as expected for an empty cell. Touch gameplay (select/place/pan/pinch) could not be exercised meaningfully because of F3. | `adb shell input tap` |
| Autorotation | **UNRESOLVED**. The display stayed at `ROTATION_0` (1080x2400) after `settings put system user_rotation 1` with auto-rotate disabled, and after `adb emu rotate`. The APK manifest declares `android:screenOrientation=0xd` (fullUser). The cause (emulator rotation handling versus the Unity activity) was not isolated. | `screen-run4-landscape.png`, `screen-run4-emu-rotate.png`, `dumpsys window` |
| Safe area | **FAIL (F2)**. The display reports cutout-hiding overlays, and HUD content starts below the top edge. However, the Browse-mode, Machine inspector and Save-and-return panels are drawn on top of each other in the top ~230 px, with overlapping headings, status text and action rows. Text is unreadable, and tap targets overlap. | `screen-run4-gles3-a.png` |
| Suspend / screen lock / return | **Lifecycle callbacks PASS; persistence FAIL (F1)**. `KEYCODE_HOME` produced `onPause`, `pauseUnity`, `APP_CMD_PAUSE`, `APP_CMD_STOP` and `APP_CMD_SAVE_STATE`. `KEYCODE_POWER` put the device to sleep (`mWakefulness=Asleep`). Wake, `wm dismiss-keyguard` and relaunch produced `onResume` and `resumeUnity` in the **same** process (PID `3979`, no restart). | `emulator-logcat-run4-lifecycle.txt`, `screen-run4-return.png` |
| Persistent-path checkpoint promotion / recovery | **FAIL (F1)**. `Application.persistentDataPath` resolved to `/storage/emulated/0/Android/data/com.DefaultCompany.idlefactorygame/files`. `IdleFactory/Checkpoints/v1` was created but stayed empty after HOME, after lock/return and after an explicit HUD save. The explicit save showed *"The saved factory could not be validated. Your live factory is unchanged."* (`CommandOutcome.InvalidCheckpoint`, `PersistenceCommands`/`PersistencePresenter`). Previous-generation recovery could not be exercised because no generation was ever written. | `ls -la .../Checkpoints/v1` |

### Product findings for remediation (outside T038's evidence scope)

- **F1 — checkpoint save and validation fail in player builds (Android IL2CPP; Windows Mono is consistent with this).**
  - **Symptom:** the save path returns `InvalidCheckpoint`, so no durable checkpoint exists on device, and lifecycle departure saves are also lost.
  - **Why tests missed it:** the Edit Mode `JsonCheckpointStore` tests pass in the editor, and the Play Mode scene tests replace the checkpoint store through `CompositionTestOverrides`. Neither covers the real store in a player.
  - **Likely hypothesis (unverified):** `Assets/IdleFactory/Unity/Persistence/SaveV1Mapper.cs` uses Newtonsoft.Json reflection, and the project has no `link.xml`. Managed code stripping may therefore remove DTO members, so the pending-file readback fails validation.
  - **Owner:** Gameplay Developer, with Software Architect, Security/Privacy and Test/QA lenses. It needs a player-level regression check.
- **F2 — HUD panels overlap on a portrait phone (`sw411dp`, 1080x2400 at 420 dpi).** T036 editor viewport checks passed, but the player shows stacked absolute panels. **Owner:** UI/UX Developer, with independent UI/UX Reviewer.
- **F3 — no D001 buildings are visible after a cold start.** The HUD reports *"Development-only D001 factory is ready"*, yet the visible grid is empty. The cause (camera framing, sprite rendering on GLES, or scenario state) is not isolated, and F1 may interact with the cold-start path. **Owner:** Gameplay Developer / Technical Artist.
- **Autorotation (unresolved):** re-test with a working rotation method, or on a physical device.

These findings mean the Android runtime journey does **not** pass. T038 records them as evidence and does not repair them. They require remediation tasks through the Spec Kit/MAQA convergence path before T040 profiling and T041 acceptance can rely on Android runtime behavior.

Raw evidence (ignored, under `test-results/android/`): `AndroidBuild.log`, `AndroidBuild-gles3.log`, `emulator-logcat-run1-swiftshader.txt`, `emulator-logcat-run2-hostgpu.txt`, `emulator-logcat-run3-hostgpu-cold.txt`, `emulator-logcat-run4-gles3.txt`, `emulator-logcat-run4-lifecycle.txt`, `screen-01-launch.png`, `screen-run1-swiftshader-graphics-error.png`, `screen-run4-gles3-a.png`, `screen-run4-touch-tap.png`, `screen-run4-save-tap.png`, `screen-run4-return.png`, `screen-run4-landscape.png`, `screen-run4-emu-rotate.png`.

## Configured Unity suites

The configured command is `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1`. Suite results verify repository behavior only. They are not Windows-player or Android-device evidence. After this remediation's build-command change, both suites passed: Edit Mode **545/545** and Play Mode **74/74**, 0 skipped (`test-results/unity/20260925T200031427Z/`). The previous commit `ab8fe5e` passed Edit Mode **545/545** and Play Mode **74/74** with 0 skipped (`test-results/unity/20260925T194535758Z/`).

## History

The first T038 attempt (commits `4667e4c`, `712de22`, `b193305`, from baseline `b9c790a`) was BLOCKED for three reasons: the empty build scene list, the absence of the Unity Android Player module, and a pre-emulator task definition that required a physical device. After the owner approved the emulator path (`d0675b8`, `4c7e191`), commit `ab8fe5e` produced the builds and recorded the SwiftShader and quick-boot host-GPU blockers. Independent QA failed `ab8fe5e` because unintended icon-slot serialization had been committed in `ProjectSettings.asset` and the report claimed otherwise. This remediation removes that serialization. It also resolves the emulator graphics blocker with a cold host-GPU boot and the GLES3 variant, and records the runtime journey and findings above.
