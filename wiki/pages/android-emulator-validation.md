---
title: Android emulator validation
type: howto
sources: [S001, S008]
updated: 2026-09-29
---

# Android emulator validation

All results from this procedure are emulator-only development evidence for AVD `Medium_Phone_API_36.0` and its `arm64-v8a` translation layer. They are not physical-device, performance or release evidence (see [Unity test and MAQA readiness](./unity-test-and-maqa-readiness.md)). (S001)

## Graphics: cold host-GPU boot and a GLES3-only build

The SwiftShader and quick-boot host-GPU runs failed to render. The working setup is a cold host-GPU boot (`emulator -avd Medium_Phone_API_36.0 -gpu host -no-snapshot-load -no-snapshot-save`) plus `PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3`. That entry point builds an IL2CPP, ARM64, GLES3-only Development APK. (S001)

The GLES3 override applies to that build only. Since T056 the build checks before and after that Android Graphics APIs are automatic, both in memory and in `ProjectSettings.asset` on disk. It restores the override before exiting, and it exits with code 1 only after the restore. (S001)

## Fail-fast install and launch

1. Android builds restart the ADB daemon. Run `adb wait-for-device` and check `sys.boot_completed` before installing, or `adb install` can fail with `device offline`.
2. `adb install -r`, `pm clear`, `logcat -c`, launch through `monkey … LAUNCHER 1`, then confirm `pidof` and `primaryCpuAbi=arm64-v8a`.
3. In Windows PowerShell 5.1, `Start-Process -Wait` also waits for the leftover Gradle daemon. Wait on the Unity process itself. (S001)
4. Only one agent session may drive `emulator-5554` at a time. Before a run, verify the installed APK hash. In T040 a parallel session's install, data clear and relaunch disturbed measurements. The measurement procedure, including capture protocol, frame pacing and profiler constraints, is in [Android emulator profiling](./android-emulator-profiling.md). (S001)

## Rotation methods that work (T055)

- `adb shell cmd window user-rotation lock <n>` and `free` rotate the display.
- `adb emu sensor set acceleration <x>:<y>:<z>`, with auto-rotate free, drives sensor rotation.
- `adb emu rotate` and `settings put system user_rotation` do **not** rotate this AVD. (S001)

The player follows these methods only since the [Android runtime rotation](./android-runtime-rotation.md) fix. Two-finger pinch cannot be injected with `adb shell input`, so pinch is covered only by the Play Mode gesture tests. (S001)

## Checking persistence and cold restore

- `Application.persistentDataPath` resolves below `/storage/emulated/0/Android/data/<package>/files`.
- Pull the `IdleFactory/Checkpoints/v1` manifest and data files to verify generations and `reconciledFromCheckpointId`.
- `am force-stop` followed by a relaunch simulates process death. A killed process writes no departure checkpoint, so the measured gap starts at the last durable generation. (S001)

## Known noise

- A healthy Development build logs two `E Unity` lines per process: the Play Asset Delivery probe and the Autoconnect Profiler probe.
- The T082 JSON-content run (2026-09-29) also logged a D-level `Failed to load native plugin '_burst_0_0'` line. It was not an error and did not affect start-up, but it is not yet explained. (S008)
- Builds and test runs re-serialize empty icon slots and an `AndroidPlayer` `m_Automatic: 1` entry into `ProjectSettings.asset`. Discard them; they are not project changes. (S001)
