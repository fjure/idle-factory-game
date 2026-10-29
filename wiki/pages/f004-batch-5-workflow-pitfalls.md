---
title: F004 batch 5 workflow pitfalls
type: howto
sources: [S047]
updated: 2026-10-29
---

# F004 batch 5 workflow pitfalls

Batch 5 merged #529 (T417), #524 (T412), #593 (T457), #594 (T458) and #530 (T418). (S047)

- **Quiet-host measurement protocol (T412).** It worked: the owner confirmed a quiet host, the parent paused the workers at a safe point (WIP commits), closed launchers and Docker, moved all three leases (`unity`, `emulator`, `gpu`) to the measuring Issue, built B2 itself from an empty `Library/`, and only then did one worker measure alone. Results: [F004 B2 frozen budgets](./f004-b2-frozen-budgets.md). (S047)
- **Parent starts long runs; QA reviews in parallel.** Long Unity runs and fresh-Library builds were started by the parent in the background. QA subagents reviewed code and evidence read-only in the meantime and gave conditional verdicts (continues the rule in [batch 4](./f004-batch-4-workflow-pitfalls.md)). (S047)
- **Busy lease means early block.** A worker that finds a lease busy returns blocked early; the parent resumes the same worker later with the lease in hand. (S047)
- **Main moves under an in-review PR.** When the owner merged #591, the open PR conflicted. The parent merged `origin/main` into the branch, posted a new worker-done marker, and got a delta QA for the new sha before `maqa-merge`. (S047)
- **One-off flake.** `DisplayPreferencesStoreTests.SaveReplacesAnExistingFileAndRewritesAnInvalidOne` failed once with a transient `IOException` around `File.Replace` and passed 3/3 reruns; a hint was posted on #546. See [F004 display preferences store](./f004-display-preferences-store.md). (S047)
- **URP global settings keep being re-serialized.** Unity builds and editor runs rewrite `UniversalRenderPipelineGlobalSettings.asset` (a dropped `m_SettingsList` rid, a trimmed `m_RuntimeSettings.m_List`). The T457 guard catches an emptied list, not a partial trim, so keep diffing after every run. See [F004 URP phase 0 results](./f004-urp-phase0-results.md). (S047)
- **Lease script from an up-to-date worktree.** A stale main-checkout copy of the lease script lacked the `gpu` resource, so a SetPass probe ran without a recorded lease. Call the script from an up-to-date worktree (rule in `docs/agents/workstation-pitfalls.md`). (S047)
- **Long-path leftovers.** Removing the Unity worktrees of #593, #594 and #530 left long-path `Library/` leftovers for the owner to delete (see the worktree-removal note in the same pitfalls file). (S047)

Next: [F004 batch 6 workflow pitfalls](./f004-batch-6-workflow-pitfalls.md).

Related: [F004 batch 4 workflow pitfalls](./f004-batch-4-workflow-pitfalls.md), [F004 S1 decision and S2 spike](./f004-s2-spike-and-s1-decision.md).
