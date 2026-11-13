---
title: F006 batch 4 workflow pitfalls
type: howto
sources: [S060]
updated: 2026-11-13
---

# F006 batch 4 workflow pitfalls

- **Usage limits cut QA and worker agents mid-run.** The suites had already finished; resume the same agent instead of re-running. (S060)
- **The batch-A Play-Tester gate is required before batch B.** T535 was started early and had to be paused. (S060)
- **A reviewer ran `git checkout --detach` in a worktree QA was using.** Reviewers get their own worktree. (S060)
- **A synthetic scan-code-0 Escape is ignored by the Input System**; do not report "Escape does nothing" from such a test (false finding A-F1). (S060)
- **Unity in batch mode returns immediately from a PowerShell call.** Wait on the process, not the call. (S060)
- **Emulator `/data` was nearly full.** Uninstall only the game package; never clear other apps. A Unity build wrote an 82 MB APK with 46 MB of entries (dead zip data); repacking, zipaligning and re-signing the same entries gave 42 MB and installed. (S060)
- **`Library/` was moved between worktrees, not copied**, because C: was low (12-13 GB free). (S060)
- **`git worktree remove` failed while the shell's cwd was inside the worktree.** Change directory out first. (S060)

Related: [P2 pitfalls](./f006-batch-p2-workflow-pitfalls.md), [P1 pitfalls](./f006-batch-p1-workflow-pitfalls.md).
