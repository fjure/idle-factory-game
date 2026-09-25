# T038 Windows and Android Validation

**Status: Windows build PASS; Android ARM64 Development Build, install and process launch PASS; Android runtime acceptance UNRESOLVED.** On the named API 36 x86_64 AVD, the Unity Player launched through the `arm64-v8a` translation layer but could not create a Unity graphics device. It never reached the scene. Touch, autorotation, safe-area, suspend/lock/return, and persistent-path promotion/recovery were therefore **not** exercised on Android and remain unverified. This is owner-approved, experimental emulator development evidence only. Physical-device support and release readiness remain unverified because Unity does not support emulator hardware as an Android Player target.

Observed on 2026-09-25 (Europe/Berlin) in the Issue #38 worktree, rebased by merge onto `origin/main` `e0ae260`.

## Environment

| Check | Result |
|---|---|
| Host | Windows 11 Pro `10.0.26200`; GPUs AMD Radeon Graphics and NVIDIA GeForce RTX 5070 Ti. |
| Unity | `6000.6.0f1` (`f7f8ed4d1e24`) at `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`. Player modules: `AndroidPlayer` (with bundled `NDK`, `SDK`, `OpenJDK`), `iOSSupport`, `WebGLSupport`, `windowsstandalonesupport`. |
| Unity MCP | Not available in this agent session: no Unity MCP tool binding is registered, no editor was running, and `http://127.0.0.1:8080/mcp` refused connections. Following `docs/integrations/mcp-readiness.md`, the pinned Unity CLI was used as the fallback. |
| Android SDK / ADB | `%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe`; `adb devices -l` → `emulator-5554 device product:sdk_gphone64_x86_64 model:sdk_gphone64_x86_64 device:emu64xa`. |
| Emulator | Android Emulator `35.5.10.0` (build `13402964`), AVD `Medium_Phone_API_36.0`, image `google/sdk_gphone64_x86_64/emu64xa:16/BP22.250325.006/13344233:user/release-keys`. The AVD had been started as `emulator.exe -avd Medium_Phone_API_36.0 -no-window -no-audio -no-boot-anim -gpu swiftshader_indirect`. |
| Guest | Android `16`, API `36`, `sys.boot_completed=1`, display `1080x2400` at density `420`. |
| ABI / native bridge | `ro.product.cpu.abilist=x86_64,arm64-v8a`; `ro.dalvik.vm.native.bridge=libndk_translation.so`. |
| Guest graphics | `ro.opengles.version=196608` (ES 3.0); SurfaceFlinger reports `OpenGL ES 3.0 SwiftShader 4.0.0.1` and `ANDROID_EMU_gles_max_version_3_0`; Vulkan HAL `ranchu`, CPU Vulkan `4202496`. |
| Physical Android device | None connected. |

## Build scene list

`ProjectSettings/EditorBuildSettings.asset` previously had `m_Scenes: []`, which blocked the earlier Windows build attempt. The committed build command `IdleFactory.Editor.PlatformBuildCommands.EnsureBuildScene` set it through `EditorBuildSettings.scenes`. The committed list now contains exactly one enabled scene: `Assets/IdleFactory/Scenes/CoreGameFoundation.unity` (GUID `ace6da6b625264446a5a23760fe1b9cb`). Both builds also pass that single scene explicitly to `BuildPipeline.BuildPlayer`.

## Reproducible build commands

`Assets/IdleFactory/Editor/PlatformBuildCommands.cs` is in an editor-only assembly (`IdleFactory.Editor`) and provides batch entry points. They exit non-zero if a build fails or throws. Build outputs go to the ignored `test-results/` tree.

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
$project = (Get-Location).Path

# Windows x64 player (non-development)
& $unity -batchmode -quit -projectPath $project -buildTarget Win64 `
  -executeMethod IdleFactory.Editor.PlatformBuildCommands.BuildWindows64 `
  -buildOutput "$project\test-results\platform\windows\idle-factory-game.exe" `
  -logFile "$project\test-results\platform\windows\WindowsBuild-t038.log"

# Android ARM64 IL2CPP Development Build with Autoconnect Profiler
& $unity -batchmode -quit -projectPath $project -buildTarget Android `
  -executeMethod IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64Development `
  -buildOutput "$project\test-results\android\CoreGameFoundation.apk" `
  -logFile "$project\test-results\android\AndroidBuild.log"
```

The Android entry point sets `ScriptingImplementation.IL2CPP` for the Android named build target and `AndroidArchitecture.ARM64`, then builds with `BuildOptions.Development | BuildOptions.ConnectWithProfiler`. The project already stored `AndroidTargetArchitectures: 2` (ARM64). The only persisted Player Settings change is `scriptingBackend: Android: 1` (IL2CPP). While building, Unity also serialized empty default icon slots into `ProjectSettings.asset`; that change was unintended and was reverted before commit. The Graphics APIs remain Unity's automatic defaults (`m_BuildTargetGraphicsAPIs: []`). The application identifier is the unchanged default, `com.DefaultCompany.idlefactorygame`; version `1.0` (code 1); min SDK 26; target SDK 36 (automatic).

Note for repeat runs: in Windows PowerShell 5.1, `Start-Process -Wait` also waits for the Gradle daemon that the Android build leaves running. Unity itself exits after `Exiting batchmode successfully now!`, so check the log or the Unity process rather than waiting on the child tree.

## Windows result: PASS

| Item | Evidence |
|---|---|
| Result | `Build Finished, Result: Success.`; `[PlatformBuild] StandaloneWindows64 result Succeeded, 0 errors, 0 warnings, 98894584 bytes, 00:00:34.55`. Batch process exit code `0` after 81 s, including import. |
| Artifact | `test-results/platform/windows/idle-factory-game.exe`, SHA-256 `a4c71b1bfc3e42d02f2f129fe04adef3ebf2343a662884e4b99598e9dfc90e7d`. |
| Smoke launch | Windowed launch at `1280x720` with `-logFile test-results/platform/windows/WindowsPlayer-smoke.log`. The process was still running after 12 s and was closed normally. The 42-line player log contains no `Exception` or `Error` lines. |
| Log | `test-results/platform/windows/WindowsBuild-t038.log`. |

The smoke launch proves only that the Windows player starts. The Windows interaction journey is covered by the configured Play Mode suite and `quickstart.md`; it is not re-claimed here.

## Android ARM64 Development Build: PASS

| Item | Evidence |
|---|---|
| Result | `Build Finished, Result: Success.`; `[PlatformBuild] Android result Succeeded, 0 errors, 0 warnings, 570432620 bytes, 00:07:20.98`; batch exit code `0`. `[PlatformBuild] Android: IL2CPP, ARM64, application identifier com.DefaultCompany.idlefactorygame`. |
| Artifact | `test-results/android/CoreGameFoundation.apk`, 36,313,842 bytes, SHA-256 `7a6b3bd01f1f688b431ddbef8e6edd6e4662f505ac6201a4f5cd10c4be3eb8b1`. |
| Log | `test-results/android/AndroidBuild.log`. |

## Android install and launch attempts

All three runs followed the fail-fast sequence in `quickstart.md`: `adb install -r`, `logcat -c`, `monkey -p com.DefaultCompany.idlefactorygame -c android.intent.category.LAUNCHER 1`, `pidof`, then `logcat -d`.

| Run | Emulator GPU mode | Outcome |
|---|---|---|
| 0 | `swiftshader_indirect` | `adb.exe: device offline` right after the Android build restarted the ADB daemon; install exit code `1`. This does not count as evidence. Launch was not accepted, and the run was repeated after `adb wait-for-device` and `sys.boot_completed=1`. |
| 1 | `swiftshader_indirect` (as found) | Install `Success`. Launch `Events injected: 1`. Process running (PID `9430`), and `dumpsys package` reports `primaryCpuAbi=arm64-v8a`. `topResumedActivity=com.DefaultCompany.idlefactorygame/com.unity3d.player.UnityPlayerGameActivity`. The log shows `IL2CPP: JNI_OnLoad` and `Built from ... Build type 'Development', Scripting Backend 'il2cpp', CPU 'arm64-v8a'`, then `SystemInfo CPU = x86-64 ... Memory = 1971mb` and `PlayerInitEngineNoGraphics OK`. Unity first showed the dialog *"Your device does not match the hardware requirements of this application"* (screenshot `screen-01-launch.png`); the run continued past it. Graphics initialization then failed. Vulkan found only `"SwiftShader Device (LLVM 10.0.0)"`, and Unity reported `Vulkan detection: 0`. EGL context creation failed with `EGL_BAD_CONFIG: no ES 3.2 support` and then `no ES 3.1 support`, and Unity showed the modal *"Unable to initialize the Unity Engine Graphics API."* (`screen-run1-swiftshader-graphics-error.png`). The scene was never rendered. |
| 2 | Same AVD relaunched with `-gpu host -no-audio -no-boot-anim` | Install `Success`; process started (PID `9906`); `PlayerInitEngineGraphics OK` and `AndroidDisplayManagerVulkan::AttachWindow`. The Unity graphics worker thread then crashed: `Fatal signal 11 (SIGSEGV), code 1 (SEGV_MAPERR), fault addr 0x40 in tid 10169 (UnityGfxDeviceW)`, followed by `Process 9906 exited due to signal 11`. In this mode the emulator's launcher and SystemUI also aborted (`drawRenderNode called on a context with no surface!`). SurfaceFlinger still reported SwiftShader with a GLES ceiling of 3.0. |

The emulator was then restored to its original headless `swiftshader_indirect` launch and was visible to ADB as `emulator-5554 device`.

Raw evidence, all in ignored `test-results/android/`: `emulator-logcat-run1-swiftshader.txt`, `emulator-logcat-run2-hostgpu.txt`, `screen-01-launch.png`, `screen-run1-swiftshader-graphics-error.png`, `screen-run2-a.png`.

### Exact Android runtime blocker

The ARM64 Unity Player installs and starts through `libndk_translation.so`, but no usable graphics device is available to it on this AVD:

- **OpenGL ES:** the guest caps at ES 3.0 (`ANDROID_EMU_gles_max_version_3_0`). This Unity player requests ES 3.2 and then ES 3.1, and both requests fail with `EGL_BAD_CONFIG`. A GLES-only build was not attempted, because the ceiling is below what the Player requested in either GPU mode.
- **Vulkan:** the SwiftShader Vulkan device is rejected (`Vulkan detection: 0`). With host GPU emulation, the Vulkan device is accepted, but the ARM64 graphics worker thread segfaults under translation.

No alternative x86_64 Android target was built, as `quickstart.md` requires. No project Graphics API override was committed.

## Android runtime acceptance disposition

| Required check | Result |
|---|---|
| Touch | **Not exercised** — the scene never rendered. |
| Autorotation | **Not exercised**. |
| Safe area | **Not exercised**. |
| Suspend / screen lock / return | **Not exercised**. |
| Persistent-path checkpoint promotion / previous-generation recovery | **Not exercised** on Android. It remains covered only by Edit Mode and Play Mode tests. |

Android runtime acceptance for Feature 001 is therefore **unresolved**. T040 depends on a successful T038 launch into a running scene, so it remains blocked by the graphics blocker above. To resume, provide a graphics environment in which an ARM64 Unity `6000.6.0f1` player renders. Examples are an emulator GPU configuration that exposes GLES 3.1+ or a stable Vulkan device to translated ARM64 processes, or a named physical ARM64 device. Then rerun the fail-fast sequence and the five runtime checks. A physical device would also move this evidence beyond the emulator-only scope.

## Configured Unity suites

The configured command `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1` was run after both builds, with the build command, build-scene list and IL2CPP setting in place. Both suites passed on Unity `6000.6.0f1`: Edit Mode **545/545 passed, 0 skipped**, and Play Mode **74/74 passed, 0 skipped**. The results and logs are in `test-results/unity/20260925T194535758Z/`. Suite results verify repository behavior only. They are not Windows-player or Android-device evidence.

## History

The first T038 attempt (commits `4667e4c`, `712de22`, `b193305`, from the `b9c790a` baseline) was blocked for three reasons: the empty build scene list, the absence of the Unity Android Player module, and a pre-emulator task definition that required a physical Android device. After that, the owner approved the emulator development path (`d0675b8`, `4c7e191`) and the Android module was installed. This report supersedes the earlier BLOCKED disposition. It keeps the earlier logs as historical evidence (`test-results/platform/windows/WindowsBuild.log`, `WindowsBuild-elevated.log`).
