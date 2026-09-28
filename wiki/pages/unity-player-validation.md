---
title: Unity player validation and performance
type: howto
sources: [S001, S004, S005, S006, S007]
updated: 2026-09-28
---

# Unity player validation and performance

## Windows standalone tests

At the end of each completed implementation batch, run `scripts/test-unity.ps1 -StandaloneWindowsPlayer` once. It selects `win64` and runs only Play Mode tests tagged `[Category("StandalonePlayer")]`, so Editor-only fixtures are excluded. This host logged `Killing ADB server` at shutdown even for the Windows target; acquire both the Unity and emulator leases until a later run proves ADB remains available. The test does not use the emulator. (S007)

## Deterministic performance measurements

W001 measures plain-Core `Advance` with 1,000 independent D001 machines: 500 active, 250 output-blocked and 250 inactive. Active machines have 9,216 input and 4,608 output capacity for exactly 6.4 hours. Run 100 untimed warmups and 500 independent timed samples, reset outside measurement and checksum afterward. Initial Windows budgets are p95 ≤10 ms for 100 ms active advance, p95 ≤100 ms for the capped return and zero warmed allocations inside `Advance`; report serialization, files, UI and mobile results separately. (S001)

Allocation assertions use Unity's `GC.Alloc` profiler recorder (`GcAllocationCounter` in both test assemblies), not `GC.GetAllocatedBytesForCurrentThread()`, which returns 0 under the Editor's Mono runtime even when code allocates. Record each measured delegate once before sampling because its first recorded call can count a runtime allocation, and include a live control that must allocate so a dead counter fails. (S001)

## Device-specific evidence

Internal checks use an ARM64 IL2CPP Development Build on the approved API 36 x86_64 emulator through `libndk_translation.so`. Passing there does not establish physical-device support, release readiness or representative mobile performance. See [Android emulator validation](./android-emulator-validation.md). (S001, S004, S006)

T040's exploratory performance set compared matching restored checkpoints after a 30-second warm-up and 60-second capture. It adds SurfaceFlinger frame pacing and a qualitative Profiler probe, with no mobile threshold; T057 fixed the cold-restore visibility issue first. See [Android emulator profiling](./android-emulator-profiling.md) and [mobile performance targets](./mobile-performance-targets.md). Physical iOS remains deferred under Issue #39. (S001, S005, S006)

Unity generates Xcode projects. Local iOS builds need macOS; Unity Build Automation can generate cloud builds, but neither replaces physical-device lifecycle, filesystem or performance evidence. (S001, S005)

Related: [Unity test and MAQA readiness](./unity-test-and-maqa-readiness.md), [player-level test gaps](./player-level-test-gaps.md).
