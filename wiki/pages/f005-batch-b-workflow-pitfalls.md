---
title: F005 batch B workflow pitfalls
type: howto
sources: [S022, S031]
updated: 2026-11-06
---

# F005 batch B workflow pitfalls

Adds to [batch 1 workflow pitfalls](./f005-batch-1-workflow-pitfalls.md). (S022)

- **Usage limits stopped workers twice.** Renew the Issue's leases, then resume the same agent with `SendMessage` so it keeps its context; check the worktree first. (S022)
- **QA fail then fix round is normal.** Batch B QA failed #391, #392, #396, #398 and #483 once each, mostly on optimistic pre-checks and test strength; the same worker fixed and a re-QA passed. Do not carry a QA PASS over a fix commit. (S022)
- **Owner decisions arrive mid-review.** Record them in an Issue comment marked `maqa:decision` and cite the date; examples: expiry `deadline + 1 ms` (#391), PF-10 re-baseline (#414), T364 added (#403). (S022)
- **Gate before the Play-Tester.** The owner required T361-T363 merged before T343 (#408); F6 needed a real-input check that batch mode cannot give. (S022)
- **Branch deletion:** `git branch -d` checks mergedness against the current HEAD, and the shared main checkout usually sits on an older commit, so it refuses branches that are already merged into `origin/main`. After `git merge-base --is-ancestor refs/heads/<branch> origin/main` succeeds (and the Issue is closed), delete the local ref with `git update-ref -d refs/heads/<branch>`; never `-D`. (S022)
- **Worktree removal:** Unity worktrees fail with "Filename too long" or a locked folder (a lingering player process); use the long-path recipe in `docs/agents/workstation-pitfalls.md`. (S022)
- **Windows player builds:** a test file using `UnityEditor` outside `#if UNITY_EDITOR` breaks every Windows test-player build; the editor-only suite does not notice. (S022)
- **DevTools test recipe:** see [UI and Android](./f005-batch-b-ui-and-android.md). (S022)
- **Timing probe:** the standalone test player cannot be launched alone; see [measurements](./f005-batch-b-performance-and-measurements.md). (S022)
- **Batch C:** usage limits stopped workers again and same-agent resumes worked; `BalanceFull` self-gates by category, filter or env var because a skipped test counts as a failure. See [policy r2](./f005-batch-c-policy-r2-and-gates.md). (S022, S031)
