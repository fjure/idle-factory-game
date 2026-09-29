---
title: Unity test and MAQA readiness
type: howto
sources: [S001, S009, S017]
updated: 2026-09-29
---

# Unity test and MAQA readiness

Before gameplay implementation, the repository needs a separate infrastructure bootstrap gate: direct pins for Input System 1.20.0, Newtonsoft JSON 3.2.2 and Test Framework 1.8.0; Core/Unity/Edit Mode/Play Mode asmdefs; at least one discovered smoke test in each suite; and `scripts/test-unity.ps1`. Exact CLI and exit behavior must be verified with the installed package and Unity 6000.6 editor. The 1.20.0 pin replaces 1.17.0 after the older package failed bootstrap compilation against removed editor APIs. (S001)

The wrapper runs Edit Mode and Play Mode serially, produces fresh XML and logs, rejects stale or zero-test results, propagates failures and maps `-TestFile` to a focused test. `maqa-config.yml` receives full and focused commands only after both suites and a deliberate focused red/green cycle work. (S001)

The Unity runner treats every discovered test result other than `Passed` as a suite failure, including `Skipped`. Keep default-suite tests discoverable and passing; an opt-in check should leave its assertions active in normal runs and gate only the operation that writes files. (S009)

Unity and emulator leases live in `<git-common-dir>/resource-leases`. Until PR #156, `scripts/resource-lease.ps1` resolved the relative `../.git` from the main checkout against the checkout root and used a second, invisible store at `ai-projects/.git`. Calls from the main checkout and from worktrees then did not see each other: `-Status` reported free while worktree calls were blocked. The script now asks Git for an absolute common directory. When leases look inconsistent, compare `-Status` from a worktree. (S017)

Feature 001 uses the MAQA lane. The installed coordinator creates worktrees from `origin/<default-branch>`, so reviewed specs/tasks and the verified harness must reach that remote branch before first dispatch. Each accepted prerequisite must also reach it before a dependent issue is dispatched. The configured worktree base must be writable and tested first. (S001)

Feature 001 reached implementation-ready status after completing the separate bootstrap gate. Keep test commands and MAQA worktrees based on the accepted `origin/main` snapshot, and dispatch each dependent issue only after its prerequisite is merged. This process supports the boundaries in [core architecture](./core-architecture.md). (S001)

See [Unity player validation](./unity-player-validation.md) for device-specific and performance-test evidence.
