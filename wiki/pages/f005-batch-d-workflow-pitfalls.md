---
title: F005 batch D workflow pitfalls
type: howto
sources: [S022]
updated: 2026-10-27
---

# F005 batch D workflow pitfalls

Adds to [batch B workflow pitfalls](./f005-batch-b-workflow-pitfalls.md). (S022)

- **Worker wait loops block worktree removal.** A shell `until ... sleep` loop that a worker left running after it finished kept the worktree folder locked; check for leftover processes before removing. (S022)
- **Limits stop workers.** Usage and weekly limits and API 529 stopped workers mid-task; the work stays in the worktree, so renew the lease and resume the same agent with `SendMessage`. (S022)
- **At most three reviewers at once** (`max_parallel: 3`); eight independent final reviews ran in waves. (S022)
- **Owner decisions** arrive as dated Issue comments (Q8 on #417, #419, #414 host load); the final acceptance table copies them. (S022)
- **Timing host:** keep background apps identical across timing windows and record them (PF-9); see [final evidence](./f005-batch-d-final-evidence.md). (S022)
- **DevTools assembly runs** need a direct Unity command line; see [final evidence](./f005-batch-d-final-evidence.md). (S022)

Related: [UI review fixes](./f005-batch-d-ui-review-and-fixes.md), [journeys](./f005-batch-d-journeys.md).
