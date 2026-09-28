---
title: Unity test and MAQA readiness
type: howto
sources: [S001, S004, S005, S006]
updated: 2026-09-28
---

# Unity test and MAQA readiness

Before gameplay implementation, the repository needs a separate infrastructure bootstrap gate: direct pins for Input System 1.20.0, Newtonsoft JSON 3.2.2 and Test Framework 1.8.0; Core/Unity/Edit Mode/Play Mode asmdefs; at least one discovered smoke test in each suite; and `scripts/test-unity.ps1`. Exact CLI and exit behavior must be verified with the installed package and Unity 6000.6 editor. The 1.20.0 pin replaces 1.17.0 after the older package failed bootstrap compilation against removed editor APIs. (S001)

The wrapper runs Edit Mode and Play Mode serially, produces fresh XML and logs, rejects stale or zero-test results, propagates failures and maps `-TestFile` to a focused test. `maqa-config.yml` receives full and focused commands only after both suites and a deliberate focused red/green cycle work. (S001)

Feature 001 uses the MAQA lane. The installed coordinator creates worktrees from `origin/<default-branch>`, so reviewed specs/tasks and the verified harness must reach that remote branch before first dispatch. Each accepted prerequisite must also reach it before a dependent issue is dispatched. The configured worktree base must be writable and tested first. (S001)

W001 measures plain-Core `Advance` with 1,000 independent D001 machines: 500 active, 250 output-blocked and 250 inactive. Active machines have 9,216 input and 4,608 output capacity for exactly 6.4 hours. Run 100 untimed warmups and 500 independent timed samples, reset outside measurement and checksum afterward. Initial Windows budgets are p95 ≤10 ms for 100 ms active advance, p95 ≤100 ms for the capped return and zero warmed allocations inside `Advance`; serialization, files, UI and mobile results are reported separately. (S001)

Allocation assertions must use Unity's `GC.Alloc` profiler recorder (`GcAllocationCounter` in both test assemblies, the mechanism behind the Test Framework's `AllocatingGCMemory` constraint). `GC.GetAllocatedBytesForCurrentThread()` returns 0 under the Unity Editor's Mono runtime, even for `new object[1024]`, so the zero-allocation tests of W001, the simulation and T062 could not fail until T063 replaced the counter. Re-measured with the recorder, their claims hold. Record each measured delegate once before sampling, because its first recorded call can count one runtime allocation. Include a live control that must allocate, so that a dead counter fails the test. (S001)

Feature 001 reached implementation-ready status after completing the separate bootstrap gate. Keep test commands and MAQA worktrees based on the accepted `origin/main` snapshot, and dispatch each dependent issue only after its prerequisite is merged. This process supports the boundaries in [core architecture](./core-architecture.md). (S001)

Unity 6 requires Android Player hardware and does not support Android inside an emulator. Its Player settings document ARMv7 and ARM64 and identify x86-64 as Magic Leap 2-specific. Because no physical Android device is available, the owner approved the API 36 x86_64 emulator for internal development checks. An ARM64 IL2CPP Development Build runs there through `libndk_translation.so`. T038 and its T057 rerun passed touch, rotation, safe area, suspend/lock/return and checkpoint recovery on it. Label such results as experimental emulator evidence. They do not establish supported-device compatibility, physical-device lifecycle or filesystem behavior, release readiness or representative mobile performance. The procedure is in [Android emulator validation](./android-emulator-validation.md). (S001, S004, S006)

T040's first performance capture is exploratory:

1. Restore the same checkpoint three times.
2. Warm up for 30 seconds without input and record the post-warm-up starting state.
3. Capture 60 seconds.

Compare only matching start states, preserve raw evidence, and apply no mobile threshold. Because of T057-F2, the D001 scene stayed invisible after a cold restore until T061 fixed it. (S001, S006) T040 completed this protocol on 2026-09-28, adding SurfaceFlinger frame pacing and a single qualitative Unity Profiler probe. Its measurement traps and the exclusive-device rule are in [Android emulator profiling](./android-emulator-profiling.md). The follow-ups T063–T065 are in [Mobile performance targets](./mobile-performance-targets.md). (S001) See [local checkpoints and offline return](./local-checkpoints-and-offline-return.md) for the persistence evidence scope. The iOS follow-up remains deferred under Issue #39 and is not implied by Android evidence. (S001, S005, S006)

Unity generates an Xcode project before Xcode builds an iOS application. A local iOS build needs macOS, while Unity Build Automation can generate a cloud build. That cloud option replaces a local Mac for build generation only; it does not complete T039 physical-device lifecycle, filesystem or performance evidence. (S001, S005)
