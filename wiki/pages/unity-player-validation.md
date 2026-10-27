---
title: Unity player validation and performance
type: howto
sources: [S001, S004, S005, S006, S007, S018, S021]
updated: 2026-10-08
---

# Unity player validation and performance

## Windows standalone tests

At the end of each completed implementation batch, run `scripts/test-unity.ps1 -StandaloneWindowsPlayer` once. It selects `win64` and runs only Play Mode tests tagged `[Category("StandalonePlayer")]`, so untagged Editor-only tests do not run in the player. This host logged `Killing ADB server` at shutdown even for the Windows target, again on 2026-10-07; acquire both the Unity and emulator leases until a later run proves ADB remains available. The test does not use the emulator. (S007, S021)

The category filter only selects which tests run. The whole `IdleFactory.PlayModeTests` assembly is still compiled for the player, without `UNITY_EDITOR`. One unguarded call to an Editor-only helper therefore breaks the build before any test runs. The script then reports "PlayerPlayMode produced no result XML" with Unity exit code 3, and the compile errors are in `PlayerPlayMode.log`. (S021)

T193 (Issue #283) fixed that failure. Calls from T110, T112 and T191 tests had reached helpers inside `#if UNITY_EDITOR`. The rule is to guard callers with the same symbol, not to split off an Editor-only test assembly (owner decision 2026-10-07). (S021)

- A helper that uses no Editor API stays outside the guard. Example: `RunSceneRun.Stop`, which only unloads the scene. (S021)
- A helper that loads assets through `AssetDatabase` sits inside the guard, together with every caller. Examples: `IronBytes`, `DevelopmentCatalogBytes`, `WriteV1Checkpoint`, `RunShellFixtures.NewSession`, `OverdriveStatusIn` and `ShellRig.CreateAuthored`. (S021)
- A fixture that lays out the authored UXML is guarded as a whole file. `ShellRig.Create` configures the shell only in the Editor, so `RunHudLayoutTests`, `RunKeyboardFocusTests` and `RunTextScaleTests` cannot run in a player. (S021)
- Mixed fixtures guard only their Editor-dependent sections. In `SlotGridTests` and `RunDeviceProfileTests`, the pure geometry and profile tests stay in the player compile. (S021)

Editor Play Mode coverage stayed unchanged (S021). New Play Mode tests that call these helpers need the same guard, and T120 now depends on T193 (S018).

## Deterministic performance measurements

W001 measures plain-Core `Advance` with 1,000 independent D001 machines: 500 active, 250 output-blocked and 250 inactive. Active machines have 9,216 input and 4,608 output capacity for exactly 6.4 hours. Run 100 untimed warmups and 500 independent timed samples, reset outside measurement and checksum afterward. Initial Windows budgets are p95 ≤10 ms for 100 ms active advance, p95 ≤100 ms for the capped return and zero warmed allocations inside `Advance`; report serialization, files, UI and mobile results separately. (S001)

Allocation assertions use Unity's `GC.Alloc` profiler recorder (`GcAllocationCounter` in both test assemblies), not `GC.GetAllocatedBytesForCurrentThread()`, which returns 0 under the Editor's Mono runtime even when code allocates. Record each measured delegate once before sampling because its first recorded call can count a runtime allocation, and include a live control that must allocate so a dead counter fails. (S001)

## Device-specific evidence

Internal checks use an ARM64 IL2CPP Development Build on the approved API 36 x86_64 emulator through `libndk_translation.so`. Passing there does not establish physical-device support, release readiness or representative mobile performance. See [Android emulator validation](./android-emulator-validation.md). (S001, S004, S006)

T040's exploratory performance set compared matching restored checkpoints after a 30-second warm-up and 60-second capture. It adds SurfaceFlinger frame pacing and a qualitative Profiler probe, with no mobile threshold; T057 fixed the cold-restore visibility issue first. See [Android emulator profiling](./android-emulator-profiling.md) and [mobile performance targets](./mobile-performance-targets.md). Physical iOS remains deferred under Issue #39. (S001, S005, S006)

Unity generates Xcode projects. Local iOS builds need macOS; Unity Build Automation can generate cloud builds, but neither replaces physical-device lifecycle, filesystem or performance evidence. (S001, S005)

Player-suite counts after T122: Edit 1502, Play 1492, tagged player tests 23 (T121 had 11). Player timing method and results: [W003 player timing baseline](./w003-player-timing-baseline.md). (S018)

Related: [Unity test and MAQA readiness](./unity-test-and-maqa-readiness.md), [player-level test gaps](./player-level-test-gaps.md).
