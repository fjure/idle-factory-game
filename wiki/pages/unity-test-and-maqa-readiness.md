---
title: Unity test and MAQA readiness
type: howto
sources: [S001, S009, S017, S008, S018, S019, S022]
updated: 2026-10-13
---

# Unity test and MAQA readiness

Before gameplay implementation, the repository needs a separate infrastructure bootstrap gate: direct pins for Input System 1.20.0, Newtonsoft JSON 3.2.2 and Test Framework 1.8.0; Core/Unity/Edit Mode/Play Mode asmdefs; at least one discovered smoke test in each suite; and `scripts/test-unity.ps1`. Exact CLI and exit behavior must be verified with the installed package and Unity 6000.6 editor. The 1.20.0 pin replaces 1.17.0 after the older package failed bootstrap compilation against removed editor APIs. (S001)

The wrapper runs Edit Mode and Play Mode serially, produces fresh XML and logs, rejects stale or zero-test results, propagates failures and maps `-TestFile` to a focused test. `maqa-config.yml` receives full and focused commands only after both suites and a deliberate focused red/green cycle work. (S001)

The Unity runner treats every discovered test result other than `Passed` as a suite failure, including `Skipped`. Keep default-suite tests discoverable and passing; an opt-in check should leave its assertions active in normal runs and gate only the operation that writes files. When an opt-in check has nothing to do, end it with `Assert.Pass("<reason>")` rather than returning silently. The reason appears in the result XML and the result still counts as `Passed`, so a no-op cannot be mistaken for a real check (T085). (S009, S008)

In the parallel panel wave (T113–T118, 2026-10-07) one worker started a Unity run although `-Acquire` had returned exit code 2. Always check the exit code before starting Unity. Parallel branches that each create the same new folder also get different folder `.meta` GUIDs and an add/add conflict: seed one shared `.meta` into every branch, or merge main and keep its copy. (S017, S019)

Unity and emulator leases live in `<git-common-dir>/resource-leases`. Until PR #156, `scripts/resource-lease.ps1` resolved the relative `../.git` from the main checkout against the checkout root and used a second, invisible store at `ai-projects/.git`. Calls from the main checkout and from worktrees then did not see each other: `-Status` reported free while worktree calls were blocked. The script now asks Git for an absolute common directory. When leases look inconsistent, compare `-Status` from a worktree. (S017)

Feature 001 uses the MAQA lane. The installed coordinator creates worktrees from `origin/<default-branch>`, so reviewed specs/tasks and the verified harness must reach that remote branch before first dispatch. Each accepted prerequisite must also reach it before a dependent issue is dispatched. The configured worktree base must be writable and tested first. (S001)

Feature 001 reached implementation-ready status after completing the separate bootstrap gate. Keep test commands and MAQA worktrees based on the accepted `origin/main` snapshot, and dispatch each dependent issue only after its prerequisite is merged. This process supports the boundaries in [core architecture](./core-architecture.md). (S001)

After Unity ran in a `.maqa-worktrees/issue-*` worktree, `git worktree remove` unregisters it but fails on `Library/` with "Filename too long" (seen four times in Feature 003). Confirm the worktree was clean, then delete the leftover directory with `Remove-Item -LiteralPath "\\?\<absolute path>" -Recurse -Force` and run `git worktree prune`. Unity runs also re-serialize `ProjectSettings.asset` (empty iOS icon slots) every time; workers and QA revert it before committing, and `scripts/check-settings-noise.ps1 -Commit` verifies the result. Scene changes follow [Unity serialized asset edits](./unity-serialized-asset-edits.md). (S018, S019)

F005 batch 0 learnings (S022):

- Public API snapshot: `PublicApiSnapshotTests` fails when Core's public surface drifts from `core-public-api.txt`. Regenerate with `IDLEFACTORY_REGENERATE_PUBLIC_API=1` (the run ends Inconclusive), then review the diff.
- Merge gate: after QA PASS the parent merges through `scripts/maqa-merge.ps1 -Issue <n>` (#431). `resource-lease.ps1` treats leases whose `issue-<n>` is closed as stale.
- Doc-only tasks still get independent QA plus a lens review; combining QA and Product findings into one fix round saved a cycle.
- Insert a task into a work-unit ledger by renumbering the later units so prerequisites only point backwards, then refresh the issue bodies.
- PowerShell 5.1: wrap function results in `@()` before `.Count`; join `gh --json` output with `-join "\n"` before `ConvertFrom-Json`; never name a helper `R` (alias for Invoke-History).
- The Windows player suite passed 24/24 on the final batch code.

See [Unity player validation](./unity-player-validation.md) for device-specific and performance-test evidence.

F005 batch 1 (S022): `-Category` runs, the `[Explicit]` ban, rate-limit resumes and DevTools define handling are in [F005 batch 1 workflow pitfalls](./f005-batch-1-workflow-pitfalls.md).
