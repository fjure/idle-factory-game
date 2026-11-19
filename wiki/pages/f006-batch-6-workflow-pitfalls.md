---
title: F006 batch 6 workflow pitfalls
type: howto
sources: [S062]
updated: 2026-11-19
---

# F006 batch 6 workflow pitfalls

- **Code carries mapped to read-only roles need an implementation task.** Carries named for T542 (Test/QA) and T547 (World Designer) were code fixes, which neither role may do. The owner added T560 before T542; do this early when a carry names a read-only role. (S062)
- **A code-only phase-1 worker can run without the Unity lease** while another Issue holds it; the lease is needed only for the test phase, after the Library moves to the worktree. (S062)
- **Usage limits killed workers, QA and the Architect mid-run.** Check the worktree, renew the lease and resume the same agent with `SendMessage`; this worked each time. (S062)
- **Harness "Irreversible Local Destruction" flags** came from `Library/Bee` and `Assets/UI Toolkit` noise cleanup inside the worker's own worktree. Verify the main checkout, the worktree list and the commits, then ask the worker for the list of deletions. (S062)
- **The B3 SetPass "1 pass" figure was an Editor-probe artifact**; measure SetPass in a player, and state a baseline in the same method (see [batch 6](./f006-batch-6-verification-and-polish.md)). (S062)
- **String coverage needs a mechanical proof.** Keyword sweeps missed strings twice (QA found 14, then an OfflineSummary gap); the accepted proof counts every literal line against a cited row. (S062)
- **The merge script releases `unity` and `emulator` but not `gpu`.** Release `gpu` yourself after a merge of an Issue that held it. (S062)
- **A `wip` commit or staged UI Toolkit noise can sit in a worker's worktree mid-fix.** QA confirms the final PR head has neither. (S062)

Related: [batch 5 pitfalls](./f006-batch-5-workflow-pitfalls.md), [workstation pitfalls](../../docs/agents/workstation-pitfalls.md).
