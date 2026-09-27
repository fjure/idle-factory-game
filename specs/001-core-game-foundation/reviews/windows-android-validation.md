# T038 Windows and Android Validation

**Status: Windows build PASS. Android ARM64 Development Build, install, launch and scene rendering PASS on the emulator (with the documented GLES3 emulator build variant). Android runtime acceptance FAIL/UNRESOLVED.** The Unity Player now runs the authored scene on the named API 36 AVD through its `arm64-v8a` translation layer. The runtime journey exposed three product findings and one unresolved check:

- **F1 — checkpoint save fails in player builds:** a player save reports *"The saved factory could not be validated. Your live factory is unchanged."* No checkpoint file is written, and lifecycle departures also leave `IdleFactory/Checkpoints/v1` empty.
- **F2 — HUD panels overlap on a portrait phone.**
- **F3 — no D001 buildings are visible in the running scene.**
- **Autorotation is unresolved.**

**Update 2026-09-27 (T057, Issue #86):** after T052–T060 the complete journey was rerun. Every T038 runtime check now passes on the emulator, and two new presentation findings (T057-F1 stale state badges, T057-F2 restored buildings not rendered after a cold start) are open. See the T057 section at the end.

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

`Assets/IdleFactory/Editor/PlatformBuildCommands.cs` (editor-only assembly `IdleFactory.Editor`) provides the batch entry points below. Each exits non-zero on failure, after any settings restore (T056). Outputs go to the ignored `test-results/` tree.

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

### T056 — Graphics API assertion for the GLES3 emulator build (Issue #85)

Before T056, a failed build or an exception called `EditorApplication.Exit(1)` inside `Run`. That skipped the `finally` that restores the Android Graphics APIs, and batch mode can save the dirty GLES3-only override on exit.

Since T056:

- **Exit order.** `Run` only returns success or failure. Batch entry points exit with code 1 only after the restore has run.
- **Checks before and after the build.** `BuildAndroidArm64DevelopmentGles3` checks both before the build (to catch an override left behind by an earlier crashed run) and after the restore and `AssetDatabase.SaveAssets()`. Each check requires two things:
  - `PlayerSettings.GetUseDefaultGraphicsAPIs(Android)` is true in memory.
  - `ProjectSettings/ProjectSettings.asset` on disk has no `AndroidPlayer` entry with `m_Automatic: 0`.

  If either fails, the build fails with an actionable `[PlatformBuild]` error.
- **Parser.** The file check is the pure parser `PlatformBuildCommands.FindNonAutomaticGraphicsApiTargets`, covered by `PlatformBuildGraphicsApiTests`. Its cases are an empty list, a restored automatic entry, a leaked GLES3-only entry, CRLF line endings, a sibling list after the Graphics API list, and the committed `ProjectSettings.asset`.
- **Editor-callable form.** `BuildAndroidArm64DevelopmentGles3(outputPath)` never exits the editor. It returns the result and switches back to the editor's previous active build target.

Evidence, run on 2026-09-26 through Unity MCP in the open editor with active target StandaloneWindows64:

| Case | Result |
|---|---|
| Real GLES3 build to `test-results/android/t056/CoreGameFoundation-t056-gles3.apk` (SHA-256 `896dac53…c601`, not committed) | PASS: `Android result Succeeded, 0 errors`, then "Android Graphics APIs are automatic after the GLES3 emulator build". The active target was back on StandaloneWindows64 afterwards. |
| Build that fails after the override was applied (empty output path, so `Run` throws) | PASS: the method returned `false` without exiting the editor. The log shows the override applied, the exception, then "automatic after the GLES3 emulator build". |
| An override already leaked before the build (in-memory GLES3 only) | PASS: the method returned `false` before building ("not automatic before the GLES3 emulator build"), and no APK was written. |

After the builds, `ProjectSettings.asset` semantically still uses automatic Android Graphics APIs. As before, Unity re-serializes an explicit `AndroidPlayer` entry with `m_Automatic: 1` and empty icon slots. No public API removes that entry, and the assertion correctly accepts it. These serialization-only changes were discarded and are not committed. The batch-mode wrapper was not run in a separate batch process because the single Unity editor slot was open. The wrapper only reads `-buildOutput` and calls the verified editor-callable method.

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
- **Autorotation (unresolved):** re-test with a working rotation method, or on a physical device. T055 update: the rotation method was found and the cause isolated to the Unity activity fixing its launch orientation. See the T055 section below.

These findings mean the Android runtime journey does **not** pass. T038 records them as evidence and does not repair them. They require remediation tasks through the Spec Kit/MAQA convergence path before T040 profiling and T041 acceptance can rely on Android runtime behavior.

Raw evidence (ignored, under `test-results/android/`): `AndroidBuild.log`, `AndroidBuild-gles3.log`, `emulator-logcat-run1-swiftshader.txt`, `emulator-logcat-run2-hostgpu.txt`, `emulator-logcat-run3-hostgpu-cold.txt`, `emulator-logcat-run4-gles3.txt`, `emulator-logcat-run4-lifecycle.txt`, `screen-01-launch.png`, `screen-run1-swiftshader-graphics-error.png`, `screen-run4-gles3-a.png`, `screen-run4-touch-tap.png`, `screen-run4-save-tap.png`, `screen-run4-return.png`, `screen-run4-landscape.png`, `screen-run4-emu-rotate.png`.

## T055 — emulator rotation method and isolated blocker (Issue #84)

Observed on 2026-09-26 on AVD `Medium_Phone_API_36.0` (API 36, cold host-GPU boot). The APK was the T054 GLES3 Development build (`test-results/android/t054/CoreGameFoundation-t054-gles3.apk`, built from the code merged as `05c47b0`). Emulator only; no physical-device claim.

### Working rotation methods on this emulator

| Method | System Settings app (control) | Unity player |
|---|---|---|
| `adb shell cmd window user-rotation lock 1` (then `lock 0` / `free`) | **Rotates**: display `ROTATION_90`, 2400x1080 ([`t055-evidence/emulator-settings-app-user-rotation-lock-1.png`](t055-evidence/emulator-settings-app-user-rotation-lock-1.png)) | Does not rotate: display stays `ROTATION_0`, and `requestedOrientation=SCREEN_ORIENTATION_PORTRAIT` |
| `adb emu sensor set acceleration <x>:<y>:<z>` with auto-rotate on (`cmd window user-rotation free`) | Follows the sensor. `-9.81:0:0` gives `ROTATION_270` | Only the **launch** orientation follows the sensor (see below) |
| `adb emu rotate` | No display change | No display change |
| `settings put system user_rotation 1` with auto-rotate off (the T038 method) | — | No display change |

The emulator can rotate the display. The T038 methods were the wrong levers: `user_rotation` is ignored unless the rotation is locked through the window manager, and `emu rotate` does not change the accelerometer on this AVD.

### Isolated cause: the Unity activity fixes its orientation at launch

- The APK manifest declares `UnityPlayerGameActivity` with `android:screenOrientation=13` (`fullUser`), `resizeableActivity=true` and `configChanges=0x40003fff`, checked with `aapt2 dump xmltree`.
- The project uses `defaultScreenOrientation: 4` (Auto Rotation), allows all four orientations, and sets `useOSAutorotation: 1` and `androidAutoRotationBehavior: 1` (User). No project script sets `Screen.orientation` (only tests do).
- At runtime, `dumpsys activity activities` shows a concrete `requestedOrientation`/`overrideOrientation` instead of `fullUser`. It is the orientation the accelerometer reported when the player started: `SCREEN_ORIENTATION_PORTRAIT` with gravity on +y, and `SCREEN_ORIENTATION_REVERSE_LANDSCAPE` with gravity on −x. This happens even when the window manager rotation lock requests landscape at launch.
- The player registers an `OrientationEventListener` on the accelerometer (`dumpsys sensorservice`). However, later accelerometer changes (−x → +y, +y → ±x, waiting 5–6 s each) never change the requested orientation. The window manager rotation lock cannot rotate it either.
- A controlled rebuild with `PlayerSettings.Android.autoRotationBehavior` explicitly `User` behaves the same. That was already the project value, and the setting was restored and not committed.

**Blocker:** the Unity player fixes its orientation at launch and does not follow runtime rotation on this emulator, although the emulator can rotate. Portrait↔landscape **transitions** therefore cannot be verified: state preservation across rotation, gesture cancellation on rotation, and safe-area re-layout after rotation all remain unverified. This needs a separate app-level task, because T055 is evidence-only. The task should determine whether this is Unity 6000.6 `UnityPlayerGameActivity` behaviour or a project setting, and fix it or document it as an accepted limitation. It should then repeat this matrix.

### What could be verified without a runtime transition

| Check | Result | Evidence |
|---|---|---|
| Landscape layout at launch (accelerometer −x, display `ROTATION_270`, 2400x1080) | **PASS**: the emulator-observed scale of ~2.09 px/pt gives the Desktop class (as in the T053 matrix). Side panels are on the left and the action bar at the bottom right, with no overlap and readable text. The D001 machine is visible outside the HUD at launch (T054). | [`t055-evidence/emulator-sw411dp-landscape-launch.png`](t055-evidence/emulator-sw411dp-landscape-launch.png) |
| Portrait layout at launch | **PASS**: see the T053 and T054 evidence in `ui-ux-acceptance.md` | — |
| Portrait↔landscape transition without state loss, gesture leaks or safe-area regressions | **BLOCKED** by the isolated cause above | `dumpsys` output in this section |

In the editor, the automated coverage for rotation handling is `NavigationInputTests` (orientation change cancels active gestures) and the T053/T054 screen-class matrices. It does not replace a player-level transition.

## Configured Unity suites

The configured command is `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1`. Suite results verify repository behavior only. They are not Windows-player or Android-device evidence. After this remediation's build-command change, both suites passed: Edit Mode **545/545** and Play Mode **74/74**, 0 skipped (`test-results/unity/20260925T200031427Z/`). The previous commit `ab8fe5e` passed Edit Mode **545/545** and Play Mode **74/74** with 0 skipped (`test-results/unity/20260925T194535758Z/`).

## History

The first T038 attempt (commits `4667e4c`, `712de22`, `b193305`, from baseline `b9c790a`) was BLOCKED for three reasons: the empty build scene list, the absence of the Unity Android Player module, and a pre-emulator task definition that required a physical device. After the owner approved the emulator path (`d0675b8`, `4c7e191`), commit `ab8fe5e` produced the builds and recorded the SwiftShader and quick-boot host-GPU blockers. Independent QA failed `ab8fe5e` because unintended icon-slot serialization had been committed in `ProjectSettings.asset` and the report claimed otherwise. This remediation removes that serialization. It also resolves the emulator graphics blocker with a cold host-GPU boot and the GLES3 variant, and records the runtime journey and findings above.

## T058 — Android runtime rotation (Issue #92)

Observed on 2026-09-26 on AVD `Medium_Phone_API_36.0` (API 36), GLES3 Development build `test-results/android/t058/CoreGameFoundation-t058-gles3.apk`. Emulator only; no physical-device claim.

### Root cause (confirmed by controlled builds)

- Unity's native Auto Rotation decides from the legacy Input Manager accelerometer. With the Input System as the only input backend (`activeInputHandler: 1`, R09), `Input.acceleration` stays `(0,0,0)`. `Input.deviceOrientation` still follows the sensor, but `Screen.orientation` stays on the launch orientation.
- Not the cause: GameActivity versus Activity entry (both fixed), `autoRotationBehavior` User versus Sensor (both fixed), rotation-lock state, or sensor event rate. A scripted concrete `Screen.orientation` is applied immediately.
- An experiment with `activeInputHandler: 2` (Both) restores rotation. The owner rejected that because it needs the legacy backend.
- Handing orientation to Android through a runtime JNI call does not work, because Unity re-requests its stale concrete orientation after every configuration change.

### Fix

`Assets/Plugins/Android/IdleFactoryGameActivity.java` extends `UnityPlayerGameActivity` and replaces every orientation request with `fullUser`. `Assets/Plugins/Android/AndroidManifest.xml` makes it the launcher activity. Unity still merges the orientation, `configChanges`, resize and theme attributes (checked with `aapt2 dump xmltree`). Android now owns sensor rotation, the rotation lock and platform rules, and the Input System stays the only input backend. On iOS, rotation is handled by UIKit; desktop is unaffected. Guard: Edit Mode `AndroidRuntimeRotationTests` (5/5).

### Transition matrix (`test-results/android/t058/rotation-matrix.txt`, `gesture-rotation.txt`, `shots/`)

| Check | Result |
|---|---|
| Sensor +x / −x / +y | **PASS**: 2400x1080 / 2400x1080 / 1080x2400, `requestedOrientation=FULL_USER` throughout |
| Rotation lock (`user-rotation lock 0` + sensor +x; `lock 1`; `free`) | **PASS**: stays portrait; forced landscape; follows the sensor again |
| HOME and return | **PASS**: same process, orientation kept and still follows |
| State across rotation | **PASS**: selection, machine instance, buffers and blockers are unchanged; a production cycle at 67% completes after rotation (`shots/02`–`05`) |
| Gesture leaks | **PASS**: Android defers the rotation while a finger is down (display stays 1080x2400 for 4 s after sensor +x) and rotates after release, so no gesture spans a transition. A held pan keeps the selection (`shots/06`, `07`). The existing `CancelAll` on orientation change still covers devices that rotate mid-gesture. |
| Layout / safe area | **PASS with finding**: the portrait and landscape screen classes switch correctly, and the emulator reports a full-screen safe area in both. **Finding T058-F1:** in landscape, a long left panel column (Machine inspector + Durable offline return) overlaps the Browse-mode header and is clipped at the bottom (`shots/07`). Owner: UI/UX Developer. |
| Lifecycle side effect | Observation: a rotation causes a brief window focus loss. Per FR-037 this records a departure checkpoint and a sub-second return reconciliation (`Durable offline return 0.04–0.36 s`, no resource changes). |

An Android 37 system image is available but not installed; a cross-check there needs the owner's approval for the download.

### T058 configured suites

`scripts/test-unity.ps1` on the T058 commit rebased onto `6dace3a`: Edit Mode **557/557** (`test-results/unity/20260927T103314520Z/`) and Play Mode **119/119** (`test-results/unity/20260927T103436293Z/`), 0 skipped. Suite results verify repository behaviour only; the Android evidence is the emulator matrix above.

### T058 independent reviews (PR #95)

| Lens | Verdict | Notes |
|---|---|---|
| Test/QA | **PASS** | Static review of `cc6d934`: the diff scope, the settings, and the `aapt2` comparison with T056 (no dropped attribute) match; the guards are non-vacuous; the suites are 557/557 and 119/119. The emulator was not running, so the dynamic checks were confirmed from the recorded evidence and not re-run. |
| Software Architect | **ACCEPT WITH CONDITIONS** | Boundary and dependency direction are acceptable. The conditions were to record the R09 consequence, the ownership of rotation, the ignored `Screen.orientation`, the per-rotation FR-037 checkpoint, and the obligation to re-verify the manifest on Unity upgrade. They are addressed by `research.md` R19 (with a pointer from R09) and the Android note in `plan.md`. The source-string guard test and the absence of an automated merged-manifest check remain low-severity residuals. |
| UI/UX Reviewer | **ACCEPT WITH FOLLOW-UPS** | Rotation, selection and inspector continuity, and touch targets pass. It confirms T058-F1 bottom clipping as a pre-existing layout gap, now follow-up T059 (#97). It judges the header overlap as transient. It also recommends a Product/UI decision on whether the Durable offline return summary should appear after rotation-induced returns in player-facing builds. |

## T060 — no Durable offline return summary after a runtime rotation (Issue #100)

Observed on 2026-09-27 on AVD `Medium_Phone_API_36.0` (API 36), cold boot `-gpu host -no-snapshot-load`, GLES3 Development builds. Emulator only; no physical-device claim. Owner decision: spec Clarifications Session 2026-09-27 and the FR-036 amendment.

### Lifecycle signals (probe build, not committed; `test-results/android/t060/probe-logcat.txt`)

A temporary probe logged `OnApplicationFocus`, `OnApplicationPause`, `Screen.width`/`height`/`orientation` and the safe area to logcat.

| Event | Signals reported by the Unity player |
|---|---|
| Sensor rotation (+y → +x, +x → +y) | Java `onConfigurationChanged`, then `OnApplicationFocus(false)` and `OnApplicationFocus(true)` 50–320 ms later. **No** `onPause` / `OnApplicationPause`. `Screen.width`/`height` already report the new size at the focus loss; `Screen.orientation` follows one or two frames after focus returns. |
| `cmd window user-rotation lock 1` / `free` | Same pattern. After `free`, the running frame saw the new size and the old orientation before the focus loss arrived in the same frame. |
| HOME and relaunch | `onPause`, `OnApplicationPause(true)`, then `OnApplicationFocus(false)`; return reverses it (`OnApplicationFocus(true)`, `OnApplicationPause(false)`). Same process. |
| Power lock and wake (`KEYCODE_POWER`, `KEYCODE_WAKEUP`, `wm dismiss-keyguard`) | Same as HOME. |
| Notification shade (`cmd statusbar expand-notifications`) | `OnApplicationFocus(false)` / `(true)` only (2.8 s), no pause, no screen change. |

Conclusion: the distinguishing signal is the pause. A rotation is a focus-only departure together with a screen configuration change; a genuine suspend or lock always reports a pause. The rotation remains a real FR-037 lifecycle transition (a departure checkpoint and a return reconciliation are written), so no Software Architect decision to change lifecycle semantics was needed.

### Fix

- `ApplicationLifecycleAdapter` classifies each published result as `ColdStart`, `Return` or `ConfigurationChange` (`LastReturnKind`). A return is `ConfigurationChange` when no pause was reported during the departure and the screen configuration (width, height or orientation) changed: at the departure compared with the last running frame, within `ConfigurationChangeWindowSeconds` (1 s) before the departure, or at the return. Persistence commands, checkpoints and offline accounting are called exactly as before.
- `PersistencePresenter` does not show the summary of a `ConfigurationChange` return. It keeps rendering the summary of the latest genuine return (or nothing if none is shown). Load, reset, reload and recovery still clear it as before. A rotation return that used the FR-037 fallback is never withheld, because its warning tells the player that later unsaved changes were not restored.
- The summary is still hidden while the lifecycle is Suspended or Reconciling (existing FR-036/FR-037 behaviour). During a rotation this is the 50–320 ms focus gap, after which the earlier summary is shown again.
- Play Mode tests in `LifecyclePersistenceTests` (7 new): rotation keeps the departure checkpoint, the reconciliation from it and the measured duration; a configuration change seen just before the focus loss counts, one outside the window does not; a focus-only loss without a screen change stays a genuine return; a paused departure is genuine even if the screen rotated meanwhile, including a rotation followed by HOME; end to end with the presenter, a rotation neither replaces nor removes an earlier genuine summary, a genuine return after a rotation shows its own summary, a rotation without an earlier summary shows none, and a fallback rotation return still shows its warning.

### Emulator verification (`test-results/android/t060/CoreGameFoundation-t060-gles3.apk`, `verify-logcat.txt`, `shots/`)

| Check | Result |
|---|---|
| Cold start | **PASS**: summary shown, `Measured: 7 m 51 s` (`t060-evidence/emulator-portrait-cold-start-summary.png`) |
| Sensor rotation to landscape | **PASS**: same cold-start summary, `7 m 51 s`; no new sub-second summary (`emulator-landscape-after-rotation-keeps-cold-start-summary.png`) |
| HOME, 20 s, relaunch | **PASS**: new summary `Measured: 20.02 s` (`emulator-landscape-after-home-return-summary.png`) |
| Sensor rotation to portrait | **PASS**: summary stays `20.02 s` (`emulator-portrait-after-rotation-keeps-home-summary.png`). The checkpoint store shows the rotation still promotes a departure and a return generation (manifest `b` 11:41:09.816, manifest `a` 11:41:09.904, after `onConfigurationChanged` at 11:41:09.692). |
| Power lock 12 s, wake | **PASS**: new summary `Measured: 11.6 s` (`test-results/android/t060/shots/06`) |
| `user-rotation lock 1`, then `free` | **PASS**: summary stays `11.6 s` in landscape and back in portrait (`emulator-landscape-after-rotation-lock-keeps-wake-summary.png`, `shots/08`); the store again promoted two generations (11:42:23.304 / .384). |
| Stability | **PASS**: one process (PID 3913) throughout, 0 `E/Unity`, `F/DEBUG` or `FATAL EXCEPTION` lines. |

### T060 configured suites

`scripts/test-unity.ps1` on the T060 working tree: Edit Mode **557/557** and Play Mode **138/138**, 0 skipped (`test-results/unity/20260927T113629517Z/`). Suite results verify repository behaviour only; the Android evidence is the emulator matrix above.

### T060 independent reviews

| Lens | Verdict | Notes |
|---|---|---|
| Test/QA | **PASS** | Review of `11d5017`. All ten criteria pass: signals confirmed before coding, a non-vacuous classification, negative paths (notification shade, pause and rotation in both orders, fallback), unchanged FR-037 accounting (+2 promotions per rotation, the same as a genuine return), and evidence matching the screenshots. Reran `LifecyclePersistenceTests` 13/13 and `PersistenceUiTests` 17/17. |
| Software Architect | **ACCEPT WITH CONDITIONS** | Boundary placement, dependency direction (the `Func<bool>` seam; `Unity/UI` does not reference the Bootstrap enum), the presenter-owned retained summary and the unchanged FR-037 semantics are accepted. Conditions: record the decision and the residual misclassification risk of the platform-ungated 1 s window (desktop resize with Alt-Tab, IME height changes, foldable or split-screen). Both are addressed by `research.md` R20, and R19 now points to it. |
| UI/UX Reviewer | **ACCEPT WITH FOLLOW-UPS** | The behaviour matches the owner decision in both screen classes. The kept summary is truthful and carries no colour-only cue, and the brief hide during the focus gap is acceptable. Low-severity follow-up: the fallback warning after a rotation return is proven only in Play Mode, not on the emulator. |

## T057 — rerun of the complete T038 Android runtime journey (Issue #86)

Observed on 2026-09-27 (Europe/Berlin; logcat and file times below are UTC) from `origin/main` `6f12b82`, after T052–T056 and T058–T060. **Emulator only**: this is development evidence for AVD `Medium_Phone_API_36.0` and its ARM64 translation layer. It makes no physical-device, performance or release claim.

**Result: every T038 runtime check passes on the emulator (touch, autorotation, safe area, suspend/lock/return, persistent-path checkpoint promotion and recovery). Two new player-visible presentation findings remain (T057-F1, T057-F2).** In both, the authoritative state and the checkpoints are correct; only the world view or the selection summary is stale.

### Environment and builds

| Item | Value |
|---|---|
| Emulator | Android Emulator `35.5.10.0` (build `13402964`), freshly **cold-booted** with `emulator -avd Medium_Phone_API_36.0 -gpu host -no-snapshot-load -no-snapshot-save` |
| Guest | `google/sdk_gphone64_x86_64/emu64xa:16/BP22.250325.006/13344233:user/release-keys`, Android 16 / API 36, ABIs `x86_64,arm64-v8a`, native bridge `libndk_translation.so`, `1080x2400` at 420 dpi. SurfaceFlinger: `OpenGL ES 3.1 (4.5.0 NVIDIA 591.86)` on the RTX 5070 Ti. |
| Unity | `6000.6.0f1`, batch mode (the editor was closed, so the single Unity slot was free) |
| Windows build | `PlatformBuildCommands.BuildWindows64` → `test-results/platform/windows/t057/`: `Build Finished, Result: Success.`, 0 errors, 0 warnings. `IdleFactory.Core.dll` SHA-256 `9167c38d…e582`, `IdleFactory.Unity.dll` `2194d86e…1704` (the launcher `.exe` is Unity's stub and hashes the same as in T038). |
| Android build | `PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3` → `test-results/android/t057/CoreGameFoundation-t057-gles3.apk`, 50,008,563 bytes, SHA-256 `89e6ef41…6a90`: IL2CPP, ARM64, OpenGLES3 only, Development. `Android Graphics APIs are automatic` before and after the build (T056). |
| Settings noise | The builds re-serialized the known iOS/Android icon slots and an `AndroidPlayer` `m_Automatic: 1` entry into `ProjectSettings.asset`. That was checked and discarded; nothing in `ProjectSettings/` is committed. |

Install and launch used the fail-fast sequence: `adb install -r` → `Success`, `pm clear`, `logcat -c`, `monkey … LAUNCHER 1`, `pidof` → PID `3699`, `primaryCpuAbi=arm64-v8a`, `Build type 'Development', Scripting Backend 'il2cpp', CPU 'arm64-v8a'`, `Creating OpenGL ES 3.1 graphics device`. Input came from `adb shell input tap|swipe|keyevent`; rotation used the T055 method (`emu sensor set acceleration`, `cmd window user-rotation`).

### Fresh Windows smoke test

| Check | Result |
|---|---|
| Launch | **PASS**: windowed `1280x720` (`WindowsPlayer-t057-smoke.log`), D3D12 on the RTX 5070 Ti, alive after 15 s, no `Exception`/`Error` lines. `CloseMainWindow` → exit code `0`. |
| Checkpoint promotion | **PASS**: the existing profile checkpoint in `%USERPROFILE%\AppData\LocalLow\DefaultCompany\idle-factory-game\IdleFactory\Checkpoints\v1` was restored at launch. A return generation was promoted at 13:53:44 and a departure generation at the close at 13:53:54 (local time). The HUD showed `READY` and a Durable offline return summary (`56 m 13 s`). |
| Restored buildings visible | **FAIL (T057-F2)**: the restored checkpoint (generation 17) contains `dev.machine.basic` at anchor (0,0), but the grid rendered no building ([`t057-evidence/windows-F2-restored-no-buildings.png`](t057-evidence/windows-F2-restored-no-buildings.png)). |

The existing Windows profile checkpoint was reused, not cleared: moving it aside was not permitted in this session, and a clean Windows profile is not needed for the smoke check.

### Android runtime journey

| Required check | Result | Evidence (`test-results/android/t057/` unless linked) |
|---|---|---|
| Cold launch, safe area, D001 framing | **PASS**: after `pm clear`, portrait `1080x2400`, `requestedOrientation=SCREEN_ORIENTATION_FULL_USER`. The HUD panels do not overlap (T053), the D001 machine is visible outside the HUD (T054), and the status is `NO SAVE`. The display reports only `HideDisplayCutout` overlays, so the safe area is full-screen, as in T058/T060. | [`emulator-portrait-cold-launch.png`](t057-evidence/emulator-portrait-cold-launch.png) |
| Touch: select, commands, production | **PASS**: tapping the machine selects it and opens the inspector (`Blocked`, output full). `Remove 1 output` makes it `Producing`, and the 5 s cycle completes (input 2 → 0, output 1/1). | `02`, `03` |
| Touch: pan, zoom, HUD scroll | **PASS**: a one-finger swipe on the grid pans and keeps the selection. `Zoom +`/`Zoom −` work. A vertical swipe inside the HUD column scrolls it without activating the control under the finger. Two-finger pinch cannot be injected with `adb shell input` and is covered only by the Play Mode gesture tests. | `04`, `05`, `09` |
| Touch: placement | **PASS**: `Build` shows the invalid preview on the occupied footprint (`✕ NO`, "Footprint overlaps an existing building"). A tap on a free cell gives `✓ OK`, and `Confirm placement` places and selects a new instance (`f618d655…`). | `06`, `07`, [`emulator-portrait-touch-placed.png`](t057-evidence/emulator-portrait-touch-placed.png) |
| Machine state readability | **FAIL (T057-F1)**: after the placed machine finished its cycle, the inspector correctly showed `Blocked` (missing input, output full). The world badge still showed `▶ RUN` and the Browse header `Blockers 0`, also after 8 s more. Re-selecting the machine updated both (`▽ IN!`, `Blockers 2`). | [`emulator-F1-stale-badge-and-header.png`](t057-evidence/emulator-F1-stale-badge-and-header.png), [`emulator-F1-after-reselect.png`](t057-evidence/emulator-F1-after-reselect.png) |
| Autorotation | **PASS** (`rotation-matrix.txt`): sensor +x → `ROTATION_90` 2400x1080; `user-rotation lock 0` → portrait despite sensor +x; `free` → `ROTATION_90`; sensor −x → `ROTATION_270`; sensor +y → `ROTATION_0`. `FULL_USER` throughout, the same PID `3699` throughout, and the selection, both machines and all buffers were unchanged. The landscape layout has no overlap or clipping (T059). | [`emulator-landscape-after-rotation.png`](t057-evidence/emulator-landscape-after-rotation.png), `14`, `15` |
| Rotation and FR-036/FR-037 | **PASS**: each rotation still promotes a departure and a return generation (for example 11:58:35.768 and .828), and shows no new summary. After the lock return below, a rotation to landscape kept the `11.81 s` summary (T060). | [`emulator-landscape-keeps-lock-summary.png`](t057-evidence/emulator-landscape-keeps-lock-summary.png), `rotation-after-lock-ck.txt` |
| Suspend (HOME) / return | **PASS** (`lifecycle-home.txt`): `onPause`/`APP_CMD_PAUSE` at 11:59:25 with a departure generation at 11:59:25.184. The relaunch after 20 s resumed the same PID with a return generation at 11:59:45.852. Summary: `Measured 20.7 s`, `Credited 20.7 s`, `Effective 16.56 s`, no resource changes, and the limiting reasons for both machines. | [`emulator-home-return-summary.png`](t057-evidence/emulator-home-return-summary.png) |
| Screen lock / return | **PASS** (`lifecycle-lock.txt`): `KEYCODE_POWER` → `mWakefulness=Asleep`, `onPause`, and a departure generation at 12:00:12.748. `KEYCODE_WAKEUP` and `wm dismiss-keyguard` → `Awake`, the same PID, and a return generation at 12:00:24.560. Summary `Measured 11.81 s`. | `18` |
| Manual save (persistent path) | **PASS**: `Application.persistentDataPath` `/storage/emulated/0/Android/data/com.DefaultCompany.idlefactorygame/files`. `Save now` wrote `manifest-a` → `data-a` (11:57:32.752). The HUD showed `READY — a saved factory is available` and `Checkpoint saved.` | `10` |
| Process death and restore | **State PASS, view FAIL (T057-F2)** (`lifecycle-cold-relaunch.txt`): after `am force-stop`, the PID was gone. The relaunch got a new PID `4300` and reconciled from the last durable generation 19 (`1d5ff0da`, 12:00:45.97) to generation 20 (`reconciledFrom=1d5ff0da`, 12:01:33.668), with both machines at (0,0) and (4,5) (`pull-b-after-cold-relaunch/`). Summary `Measured 47.62 s` (no departure checkpoint is possible for a killed process, so the gap is measured from generation 19). **The grid rendered no building** until the first tap, which selected the invisible machine at (0,0) and made both machines appear. | [`emulator-F2-cold-relaunch-no-buildings.png`](t057-evidence/emulator-F2-cold-relaunch-no-buildings.png), [`emulator-F2-after-first-tap.png`](t057-evidence/emulator-F2-after-first-tap.png) |
| Previous-generation recovery | **PASS**: `Recover previous save` promoted generation 21 (`manifest-a` seq 21 → `data-b`, 12:02:33.472), with `reconciledFrom=1d5ff0da`, which is the retained previous generation 19. It supersedes generation 20, and `manifest-b` (generation 20) stays the previous generation. HUD: `RECOVERED — the previous saved factory is now active`, `Previous saved factory recovered.` | [`emulator-recover-previous.png`](t057-evidence/emulator-recover-previous.png), `pull-c-after-recover/` |
| Stability and diagnostics | **PASS** (`emulator-logcat-t057.txt`, 8,657 lines): 0 `F DEBUG`, `FATAL EXCEPTION` or `SIGSEGV` lines and 0 `[IdleFactory.Persistence]` diagnostics. The only `E Unity` lines (2 per process) are the Development-build probes for Play Asset Delivery (`AssetPackManager` not found) and for the Autoconnect Profiler with no host listening. | — |

### Findings for remediation (outside T057's evidence scope)

- **T057-F1 — world state badges and the Browse selection summary go stale after simulation-driven state changes.**
  - **Symptom:** the inspector refreshes live (T021), but the machine badge and the Browse header show the state from the last selection or command. In the example they showed `▶ RUN` / `Blockers 0` for a machine that was already blocked. The player can therefore see an idle machine as running.
  - **Likely cause (unverified):** `FactoryView` rebuilds only on `Refresh`/`SelectAt`/`Select`, which commands and selection trigger, and not when the simulation advances.
  - **Owner:** Gameplay Developer or UI/UX Developer, with UI/UX Reviewer and Test/QA. It needs a Play Mode check that a production-driven state change updates the badge and the summary without a new selection. Traces to FR-005 and the state-readability contract in `contracts/presentation-states.md`.
- **T057-F2 — restored buildings are not rendered after a cold start from a checkpoint (Android IL2CPP and Windows Mono).**
  - **Symptom:** the factory looks empty after a relaunch, although the restored state and the checkpoints contain the buildings. The first world interaction reveals them. This also means the "offline return" on a cold start shows no factory.
  - **Likely cause (unverified):** `CoreGameFoundationBootstrap` calls `factoryView.Refresh()` after the cold-start reconciliation only in the `NoCheckpoint` branch (`CoreGameFoundationBootstrap.cs`, around line 175). A cold start with a checkpoint replaces the world without a view refresh. The Play Mode scene tests use `CompositionTestOverrides` and may not cover a cold start that restores a real checkpoint.
  - **Owner:** Gameplay Developer, with Software Architect and Test/QA. It needs a Play Mode test for a cold start with an existing checkpoint that asserts the rendered building views. Traces to FR-026/FR-036 and US3.

These findings mean T057 does not give T041 a clean Android runtime acceptance, although every T038 check passes: both are player-visible defects. Remediation tasks go through the Spec Kit/MAQA convergence path, as for T052–T060. T040 profiling only needs a restorable on-device checkpoint and representative D001 content, and both are available now. However, the D001 scene after a cold restore stays invisible until the first interaction (F2), and the T040 procedure must account for that.

Raw evidence (ignored, under `test-results/android/t057/` and `test-results/platform/windows/t057/`): `AndroidBuild-t057-gles3.log`, `WindowsBuild-t057.log`, `WindowsPlayer-t057-smoke.log`, `emulator-console.txt`, `emulator-logcat-t057.txt`, `rotation-matrix.txt`, `lifecycle-home.txt`, `lifecycle-lock.txt`, `lifecycle-cold-relaunch.txt`, `rotation-after-lock-ck.txt`, `pull-b-after-cold-relaunch/`, `pull-c-after-recover/`, screenshots `01`–`23`.

### T057 configured suites

`scripts/test-unity.ps1` on `6f12b82` (the T057 journey changes no code): Edit Mode **557/557** and Play Mode **138/138**, 0 skipped (`test-results/unity/20260927T120331440Z/`). Suite results verify repository behaviour only; the Android evidence is the emulator journey above. The test run also re-serialized the icon slots in `ProjectSettings.asset`; that was discarded.

### T057 independent reviews

| Lens | Verdict | Notes |
|---|---|---|
| Test/QA | **PASS** | Review of `db4ee31`. The diff scope is only this report and `t057-evidence/`. The APK and assembly hashes, suite totals (557/557, 138/138), PIDs, rotation matrix and logcat counts match the raw evidence. Timestamps, generations and checkpoint IDs were recomputed from the pulled JSON (for example the 47.624 s cold-restore gap). The screenshots match both findings. The "likely cause" of each finding is plausible from `FactoryView.cs` and `CoreGameFoundationBootstrap.cs` and correctly marked unverified. Low residual: the manifest/data slot rotation was not re-derived from the persistence source. |
| Game Designer/Product Owner | **ACCEPT WITH FOLLOW-UPS** | The evidence supports emulator-only acceptance of every T038 runtime check. T057 closes with its findings recorded, as T038 and T058 did. **T057-F1** is a real FR-005 violation (Medium): fix it before or together with T041, not blocking T040. **T057-F2** defeats the US3 "restore and see your factory" journey (High) and must block T041. It does not block T040, provided the procedure forces one world interaction before the warm-up. Pre-existing gaps for T041 to note: the quickstart section 5 viewport matrix (360×640, 390×844, 768×1024, rotated, 1920×1080), desktop keyboard/focus activation, and pinch release on a device. |
| UI/UX Reviewer | **ACCEPT WITH FOLLOW-UPS** | It confirms both findings from the screenshots and rates F1 Medium-High and F2 High. It also raised clipped state-badge text ("◎ SLE") and the offline summary being reachable only by scrolling in portrait. Both are known: the badge clipping was recorded in T054 as out of scope for the Art Director, and the scrolling HUD column is the T059 design. |

Follow-up: T057-F1 and T057-F2 need remediation tasks before T041, added through the Spec Kit tasks change and MAQA sync, as for T058–T060.

## T061 — render the restored factory after a cold start from a checkpoint (Issue #104, remediates T057-F2)

Observed on 2026-09-27 (Europe/Berlin) from `maqa/issue-104-cold-start-restore-render`, based on `origin/main` `d04ae4d`. **Emulator only**: this is development evidence for AVD `Medium_Phone_API_36.0` and the reused desktop Windows profile. It makes no physical-device, performance or release claim.

**Result: fixed. A cold start that restores an existing checkpoint now renders the restored buildings before the first frame the player can act on, with no tap required, on both the Android emulator and the Windows player.**

### Root cause (confirmed, not just the T057 hypothesis)

`CoreGameFoundationBootstrap.Initialize()` (`Assets/IdleFactory/Unity/Bootstrap/CoreGameFoundationBootstrap.cs`) calls `factoryView.Configure(...)` while `Session` still holds the pre-reconciliation empty `FactoryState`. `lifecycleAdapter.Configure(...)`, called afterwards, synchronously runs `ResumeAndReconcile()`, which replaces the session's authoritative state through checkpoint restore, the no-checkpoint fallback, or FR-037 recovery. The old code called `factoryView.Refresh()` only inside the `coldStartHadNoCheckpoint` branch, so a cold start that restored an existing checkpoint left the view showing the stale empty state — exactly matching T057-F2 — until the first world tap (`FactoryView.SelectAt`/`Select` are the only other callers of `Refresh()`).

### Fix

`Initialize()` now calls `factoryView.Refresh()` unconditionally once, after the no-checkpoint fallback branch, gated only on `Session.LifecycleState == LifecycleState.Running`. This covers every startup world replacement — checkpoint restore, the fallback dev-scenario reset, and any successful FR-037 recovery that leaves the session Running — and only re-renders from the already-current `commands.GetFactorySnapshot()`; it does not touch authoritative state, checkpoints, or offline accounting.

### Play Mode regression coverage

`Assets/IdleFactory/Tests/PlayMode/ColdStartRestoreRenderTests.cs` (derives from `InputTestFixture`) composes the real `CoreGameFoundation.unity` scene and, unlike `CoreGameFoundationSceneTests` (which only ever seeds the in-memory `IsolatedCheckpointStore`), writes a checkpoint containing two buildings through the real, on-disk `JsonCheckpointStore` at an isolated temp directory, then hands the bootstrap a **fresh** `JsonCheckpointStore` instance over the same directory — a genuine restore-from-disk, like relaunching the player against a checkpoint the previous run wrote. Immediately after scene load, with no tap and no input, it asserts the lifecycle outcome is not `NoCheckpoint`, both buildings are in the snapshot, `FactoryView.RenderedRevision` matches the session's current revision, both buildings have rendered machine sprites, and — as the test's first interaction — selection resolves correctly against the already-rendered restored state. Stashing the production fix and rerunning this test reproduces the bug exactly (`RenderedRevision` stuck at the pre-reconciliation `0` instead of the restored `1`).

### Suites

`scripts/test-unity.ps1` on this branch: Edit Mode **557/557**, Play Mode **139/139** (138 pre-existing plus the new test), 0 skipped. `ProjectSettings.asset` re-serialization noise from each run was discarded and is not committed.

### Android emulator evidence (`Medium_Phone_API_36.0`)

| Item | Value |
|---|---|
| Build | `PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3` → `CoreGameFoundation-t061-gles3.apk`, 50,008,563 bytes, SHA-256 `a601dd2f…984`: IL2CPP, ARM64, OpenGLES3 only, Development. `[PlatformBuild] Android result Succeeded, 0 errors, 0 warnings`. |
| Sequence | `pm clear` → fresh launch (PID 5683) → placed a second `dev.machine.basic` via touch (`Build` → tap empty cell → `Confirm placement`) → `Save now` (`Status: READY — a saved factory is available`, `Checkpoint saved.`) → `am force-stop` (confirmed PID gone) → relaunch (PID 5915). |
| Restored buildings visible before any tap | **PASS**: [`t061-evidence/emulator-cold-relaunch-buildings-visible-before-tap.png`](t061-evidence/emulator-cold-relaunch-buildings-visible-before-tap.png), captured immediately after relaunch and before any input. Both restored `dev.machine.basic` instances are visible with their state badges (`▽ IN!` at anchor (1,10), `△ OUT!` at anchor (0,0)). Compare [`t061-evidence/emulator-checkpoint-saved-before-relaunch.png`](t061-evidence/emulator-checkpoint-saved-before-relaunch.png), taken right before the relaunch. |
| Checkpoint matches the render | **PASS**: pulled checkpoint [`t061-evidence/emulator-pulled-checkpoint-data-b.json`](t061-evidence/emulator-pulled-checkpoint-data-b.json) — generation 2, `reconciledFromCheckpointId` set, exactly the two buildings shown on screen (`236f4c1a-…` at (1,10), `3a446b3e-…` at (0,0)). |
| Stability and diagnostics | **PASS**: [`t061-evidence/emulator-logcat-t061-excerpt.txt`](t061-evidence/emulator-logcat-t061-excerpt.txt) (filtered from a 977-line capture) shows only the two known-benign `E Unity` lines already documented in T057 (Play Asset Delivery probe, Autoconnect Profiler probe) — 0 `FATAL EXCEPTION`, `F DEBUG`, `SIGSEGV`, or `[IdleFactory.Persistence]` diagnostics. |

### Windows player evidence

| Item | Value |
|---|---|
| Build | `PlatformBuildCommands.BuildWindows64` (batch mode) → `idle-factory-game.exe`, SHA-256 `a4c71b1b…0e7d`. `Build Finished, Result: Success.`, `[PlatformBuild] StandaloneWindows64 result Succeeded, 0 errors, 0 warnings`. |
| Checkpoint used | The existing reused desktop profile checkpoint under `%USERPROFILE%\AppData\LocalLow\DefaultCompany\idle-factory-game\IdleFactory\Checkpoints\v1` (generation 17, one `dev.machine.basic` at anchor (0,0)) — the same profile T057 used; it was not cleared. |
| Restored building visible before any input | **PASS**: [`t061-evidence/windows-cold-start-restored-buildings-before-input.png`](t061-evidence/windows-cold-start-restored-buildings-before-input.png), captured immediately after launch and before any input. The `dev.machine.basic` at (0,0) is visible with its `△ OUT!` badge, and the HUD shows `Status: READY — a saved factory is available` and a Durable offline return summary (`Measured: 6 h 49 m 25 s`, `Credited: 6 h 49 m 25 s`), confirming a real checkpoint reconciliation, not the no-checkpoint fallback. This is the same scenario T057-F2 recorded as failing (`windows-F2-restored-no-buildings.png`). |

### T061 conclusion

Both platforms now show the restored factory on the first actionable frame, with no world interaction required. T057-F2 is remediated. T057-F1 (stale world/HUD badges after simulation-driven state changes) is unrelated and remains open for a separate task.

### T061 independent reviews

| Lens | Verdict | Notes |
|---|---|---|
| Software Architect | **APPROVE** | Review of `4e51167`. Root cause confirmed from the `Initialize()` order: `factoryView.Configure` renders the pre-reconciliation state, then `lifecycleAdapter.Configure` reconciles synchronously, and the old refresh ran only on the no-checkpoint path. The `LifecycleState.Running` gate covers checkpoint restore, the fallback and a synchronous FR-037 recovery. A `ReturnBlocked` start correctly does not render here: `RefreshReplacedFactoryAndSelection` renders once recovery publishes `Running`, so the two paths are mutually exclusive. Presentation only reads the snapshot; no authoritative, checkpoint or offline-accounting change. The test override is used appropriately, and `InputTestFixture` isolation is justified. No conflict with T062. |
| Test/QA | **PASS** | Independent review of `4e51167`. All nine acceptance criteria pass: a single refresh call site (no double refresh on the no-checkpoint path), a real on-disk `JsonCheckpointStore` restore in `ColdStartRestoreRenderTests`, and pre-input render and selection assertions. The emulator and Windows evidence is internally consistent (screenshot vs. pulled checkpoint IDs and anchors) and labeled emulator-only. Independent rerun: EditMode 557/557 and PlayMode 139/139. |

Omitted lenses: UI/UX Reviewer, because the change does not alter layout, interaction or copy, only when the existing view first renders. Game Systems/Economy, because there is no production, offline-accounting or balancing change. Performance, because it adds one refresh at startup and nothing on a hot path. Security/Privacy, because the checkpoint format and trust boundary are unchanged.
