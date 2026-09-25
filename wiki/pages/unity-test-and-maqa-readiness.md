---
title: Unity test and MAQA readiness
type: howto
sources: [S001, S004, S005, S006]
updated: 2026-09-25
---

# Unity test and MAQA readiness

Before gameplay implementation, the repository needs a separate infrastructure bootstrap gate: direct pins for Input System 1.20.0, Newtonsoft JSON 3.2.2 and Test Framework 1.8.0; Core/Unity/Edit Mode/Play Mode asmdefs; at least one discovered smoke test in each suite; and `scripts/test-unity.ps1`. Exact CLI and exit behavior must be verified with the installed package and Unity 6000.6 editor. The 1.20.0 pin replaces 1.17.0 after the older package failed bootstrap compilation against removed editor APIs. (S001)

The wrapper runs Edit Mode and Play Mode serially, produces fresh XML and logs, rejects stale or zero-test results, propagates failures and maps `-TestFile` to a focused test. `maqa-config.yml` receives full and focused commands only after both suites and a deliberate focused red/green cycle work. (S001)

Feature 001 uses the MAQA lane. The installed coordinator creates worktrees from `origin/<default-branch>`, so reviewed specs/tasks and the verified harness must reach that remote branch before first dispatch. Each accepted prerequisite must also reach it before a dependent issue is dispatched. The configured worktree base must be writable and tested first. (S001)

W001 measures plain-Core `Advance` with 1,000 independent D001 machines: 500 active, 250 output-blocked and 250 inactive. Active machines have 9,216 input and 4,608 output capacity for exactly 6.4 hours. Run 100 untimed warmups and 500 independent timed samples, reset outside measurement and checksum afterward. Initial Windows budgets are p95 ≤10 ms for 100 ms active advance, p95 ≤100 ms for the capped return and zero warmed allocations inside `Advance`; serialization, files, UI and mobile results are reported separately. (S001)

Feature 001 reached implementation-ready status after completing the separate bootstrap gate. Keep test commands and MAQA worktrees based on the accepted `origin/main` snapshot, and dispatch each dependent issue only after its prerequisite is merged. This process supports the boundaries in [core architecture](./core-architecture.md). (S001)

Unity 6 requires Android Player hardware to run Android natively and does not support Android inside an emulator. Its Android Player settings document ARMv7 and ARM64 architectures and identify x86-64 as Magic Leap 2-specific. Because no physical Android device is available, the owner approved the API 36 x86_64 emulator for current internal development checks in T038 and T040. The AVD currently exposes `arm64-v8a` through `libndk_translation.so`, so the workflow attempts an ARM64 Development Build and must prove install/launch before runtime checks. Label any such results as experimental emulator development evidence; they do not establish supported-device compatibility, physical-device lifecycle/filesystem behavior, release readiness or representative mobile performance. If the Player cannot run, keep Android runtime acceptance unresolved. (S004, S006) See [local checkpoints and offline return](./local-checkpoints-and-offline-return.md) for the persistence evidence scope. The iOS device/build/performance follow-up remains deferred under Issue #39 and is not implied by Android evidence. (S001, S005, S006)

Unity generates an Xcode project before Xcode builds an iOS application. A local iOS build needs macOS, while Unity Build Automation can generate a cloud build. That cloud option replaces a local Mac for build generation only; it does not complete T039 physical-device lifecycle, filesystem or performance evidence. (S001, S005)
