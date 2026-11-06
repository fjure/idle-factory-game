---
title: F005 batch 1 workflow pitfalls
type: howto
sources: [S022]
updated: 2026-11-06
---

# F005 batch 1 workflow pitfalls

Process lessons from MAQA batch 1 (Issues #367-#378). The parent gate T315 on main 53ceb54 passed: Edit Mode 1662, Play Mode 1553, player 24/24, 0 skipped. (S022)

- **`test-unity.ps1 -Category <name>` needs `-Suite` (or `-TestFile`).** It maps to Unity `-testCategory`; `;` separates categories, a leading `!` negates, names match `!?[A-Za-z0-9_.-]+`. It is rejected with `-StandaloneWindowsPlayer`. A category with no matching tests fails. (S022)
- **Skipped counts as failed, so never use `[Explicit]`.** Gate heavy tests by an environment variable instead, for example `IDLEFACTORY_WRITE_STRATEGY_BASELINE=1` for the report writer. Category `BalanceStrategies` adds about 47 s to the default suite. (S022)
- **DevTools tests need a temporary define to compile.** The `IdleFactory.DevTools` assembly is constrained on `IDLEFACTORY_DEV`; set it only for the run and never commit it to ProjectSettings. `scripts/check-release-build.ps1` fails if the define is set there, or if `runInBackground` is on. (S022)
- **Rate limits interrupt workers.** Session and weekly limits stopped workers mid-Issue. Resume the same agent (it keeps its context) and renew its leases first; do not start a fresh worker. (S022)
- **Command guard.** A chained `Remove-Item` is refused as a whole; run deletions as their own command. For "Filename too long" worktree removal use the long-path recipe in `docs/agents/workstation-pitfalls.md`. (S022)

Related: [Unity test and MAQA readiness](./unity-test-and-maqa-readiness.md), [F005 session log and dev time acceleration](./f005-session-log-and-devtools.md), [F005 plan overview](./f005-plan-overview.md).
