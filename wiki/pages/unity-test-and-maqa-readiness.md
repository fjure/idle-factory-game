---
title: Unity test and MAQA readiness
type: howto
sources: [S001, S004, S005, S006]
updated: 2026-09-28
---

# Unity test and MAQA readiness

Before gameplay implementation, the repository needs a separate infrastructure bootstrap gate: direct pins for Input System 1.20.0, Newtonsoft JSON 3.2.2 and Test Framework 1.8.0; Core/Unity/Edit Mode/Play Mode asmdefs; at least one discovered smoke test in each suite; and `scripts/test-unity.ps1`. Exact CLI and exit behavior must be verified with the installed package and Unity 6000.6 editor. The 1.20.0 pin replaces 1.17.0 after the older package failed bootstrap compilation against removed editor APIs. (S001)

The wrapper runs Edit Mode and Play Mode serially, produces fresh XML and logs, rejects stale or zero-test results, propagates failures and maps `-TestFile` to a focused test. `maqa-config.yml` receives full and focused commands only after both suites and a deliberate focused red/green cycle work. (S001)

At the end of each completed implementation batch, run `scripts/test-unity.ps1 -StandaloneWindowsPlayer` once. It explicitly selects `win64` and filters to Play Mode tests tagged `[Category("StandalonePlayer")]`, so Editor-only fixtures are not sent to the built Player. The host still logged `Killing ADB server` on shutdown with `StandaloneWindows64`; keep both Unity and emulator leases until a later run proves ADB remains available. (S007)

Feature 001 uses the MAQA lane. The installed coordinator creates worktrees from `origin/<default-branch>`, so reviewed specs/tasks and the verified harness must reach that remote branch before first dispatch. Each accepted prerequisite must also reach it before a dependent issue is dispatched. The configured worktree base must be writable and tested first. (S001)

W001 measures plain-Core `Advance` with 1,000 independent D001 machines: 500 active, 250 output-blocked and 250 inactive. Active machines have 9,216 input and 4,608 output capacity for exactly 6.4 hours. Run 100 untimed warmups and 500 independent timed samples, reset outside measurement and checksum afterward. Initial Windows budgets are p95 ≤10 ms for 100 ms active advance, p95 ≤100 ms for the capped return and zero warmed allocations inside `Advance`; serialization, files, UI and mobile results are reported separately. (S001)

Allocation assertions must use Unity's `GC.Alloc` profiler recorder (`GcAllocationCounter` in both test assemblies, the mechanism behind the Test Framework's `AllocatingGCMemory` constraint). `GC.GetAllocatedBytesForCurrentThread()` returns 0 under the Unity Editor's Mono runtime, even for `new object[1024]`, so the zero-allocation tests of W001, the simulation and T062 could not fail until T063 replaced the counter. Re-measured with the recorder, their claims hold. Record each measured delegate once before sampling, because its first recorded call can count one runtime allocation. Include a live control that must allocate, so that a dead counter fails the test. (S001)

Feature 001 reached implementation-ready status after completing the separate bootstrap gate. Keep test commands and MAQA worktrees based on the accepted `origin/main` snapshot, and dispatch each dependent issue only after its prerequisite is merged. This process supports the boundaries in [core architecture](./core-architecture.md). (S001)

For internal checks, an ARM64 IL2CPP Development Build runs on the approved API 36 x86_64 emulator through `libndk_translation.so`. T038/T057 passed touch, rotation, safe area and lifecycle recovery there, but emulator evidence does not establish physical-device support, release readiness or representative mobile performance. See [Android emulator validation](./android-emulator-validation.md). (S001, S004, S006)

T040's exploratory performance set compared only matching restored checkpoints after a 30-second warm-up and 60-second capture; it adds SurfaceFlinger frame pacing and a qualitative Profiler probe, with no mobile threshold. T057 fixed the cold-restore visibility issue first. The capture protocol and measurement limits are in [Android emulator profiling](./android-emulator-profiling.md); T063–T065 are in [Mobile performance targets](./mobile-performance-targets.md). Save evidence is in [local checkpoints and offline return](./local-checkpoints-and-offline-return.md). Physical iOS remains deferred under Issue #39. (S001, S005, S006)

Unity generates Xcode projects. Local iOS builds need macOS; Unity Build Automation can generate cloud builds, but neither replaces T039's physical-device lifecycle, filesystem or performance evidence. (S001, S005)
