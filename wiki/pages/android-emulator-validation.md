---
title: Android emulator validation
type: howto
sources: [S001, S008, S018, S022, S037, S060]
updated: 2026-11-13
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
- Pull the checkpoint manifest and data files to verify generations (the Save V2/V3 run writes `IdleFactory/Checkpoints/v2/run-checkpoint.*`; the Feature 001 path `Checkpoints/v1` is read only for import and by the Profile step, see [batch D journeys](./f005-batch-d-journeys.md)) and `reconciledFromCheckpointId`.
- `am force-stop` followed by a relaunch simulates process death. A killed process writes no departure checkpoint, so the measured gap starts at the last durable generation. (S001)

## F003 journey script (T123)

`scripts/android-emulator-journey.ps1 -Scenario Default|F003` (#205). F003 steps: TouchBuild, Remove/PrestigeConfirmRotation, W003Push (on-device sha256), OfflineReturnTiming (parses `ResumeAndReconcile ms=`, 3 cold starts, median ≤ 2 s), ProducingFps (same-session control) and F003LogScan. Record: `reviews/android-emulator-script-validation.md`. Pitfalls for T124: (S018)

- Tap coordinates are required, otherwise steps report skipped.
- W003Push and OfflineReturnTiming wipe `Checkpoints/v1` and `v2`; ProducingFps runs `pm clear`.
- The reconcile line needs a Development build, FPS needs a profiler-free build: use two APKs or separate `-Steps`.
- The renew-if-owned lease path is untested.

## F003 device evidence (T124, T245)

Record: `reviews/android-emulator-evidence.md`; emulator only, no physical device. The first run was blocked: the real player rejected the committed `w003-s` set (`save.v2.invariant`, then `return.blocked`), because every W003 fixture uses the test-local `W003Workload` catalog (12 slots at factory level 1; the shipped level 1 has 4 slots, `SlotUnavailable`). T245 added shipped-catalog sets. On them: 8 h return median 746 ms (budget 2 s, PASS), producing FPS 61.1 against same-session controls 61.5/61.6 (budget 60, PASS), Remove and Prestige confirmation rotation PASS (R20: rotation reconciles, summary withheld), Android Back = Cancel PASS, cutout (emulated) and the 44 pt profile (7.02 mm) PASS. The OS back edge swipe did not cancel the confirmation on a single swipe (FAIL, reproduced by QA, routed to T247). T247 resolved it as a measurement and decision matter, not a defect: the gesture cancels on the second swipe (see [Android back gesture](./android-back-gesture.md)). (S018)

Script fixes from T245: R20 assertion corrected, forced departure checkpoint, blocked starts count as FAIL, run-loaded gate, UI-ready wait and a `BackGesture` step. TouchBuild still reports FAIL on a fresh clear because the Save V2 checkpoint is written only on a lifecycle event, although the build itself works. "Skipped" and "same process, nothing spent" are not PASS evidence. (S018)

## Journey script after T247 (#328)

- `BackGesture` machine-checks Cancel: the dialog's red danger button (about 248,113,113) is counted in the central band of a screenshot (29,637 pixels open, 0 closed); Cancel needs at least 5,000 before and fewer than 500 after. "Same process, resumed, nothing spent" are labelled guards, not proof. `-BackInput Gesture|Keycode` (Keycode is the control) and `-BackSwipes 1..3` select the input. (S018)
- Inject the edge swipe at `x=3` (`input swipe 3 1200 432 1200 300`): a swipe from `x=0` is ignored by the system edge handler and pans the camera instead. Verify the injection against a Settings sub page first. (S018)
- `TouchBuild` waits for the UI, then forces a departure checkpoint (HOME and return, up to three attempts), so the baseline exists after `pm clear`. `PrestigeConfirmRotation` scrolls to `Sell the factory` itself (tap-list items `scroll` and `swipe:x1,y1,x2,y2[,ms]`). T246 removed the `Continue` tap after a short HOME return; it is still needed after an 8 h set. (S018)
- After a HOME and return with a sheet open, a plain tap on `Remove` was ignored; reopen the sheet before tapping it. (S018)

- **Invoke with `-Command`, not `-File`**: `powershell -File` turns a tap array into one string. Use `-Command "& ./scripts/android-emulator-journey.ps1 ... -BuildTaps @('306,786','528,1853')"` (T133). To prove a settings-driven regression, bisect with a single-setting variant APK and its inverse: [Android Back and runInBackground](./f003-android-back-run-in-background.md). (S018)

## Known noise

- A healthy Development build logs two `E Unity` lines per process: the Play Asset Delivery probe and the Autoconnect Profiler probe.
- A D-level `Failed to load native plugin '_burst_0_0'` line (seen in the T082 run on 2026-09-29) is known noise. The project has no `com.unity.burst` dependency (`Packages/packages-lock.json`), but the player ships the engine's `UnityEngine.BurstModule`, which probes for a Burst-generated native library that this project never produces. The line is debug level and has no functional effect. This explanation is inferred from the package and player layout, not confirmed by Unity documentation; re-check it if Burst is ever added. (S008)
- Builds and test runs re-serialize empty icon slots and an `AndroidPlayer` `m_Automatic: 1` entry into `ProjectSettings.asset`. Discard them; they are not project changes. (S001)

- F005 T351: never create `IdleFactory/Checkpoints` with `adb shell mkdir -p` before the first app launch (shell-owned; the game reports persistence.failed). See [batch D journeys](./f005-batch-d-journeys.md). (S037)
- F004 scenario for the journey script: [F004 emulator scenario](./f004-emulator-scenario.md). (S045)

- F006 T552: an atlas page of 2 MiB hung the Android load; cap every page at 512 x 512 (1 MiB), and uninstall only the game when `/data` is full. See [Android load hang](./f006-android-load-hang-atlas-page-cap.md) and [batch 4 pitfalls](./f006-batch-p3a-workflow-pitfalls.md). (S060)
