---
title: F004 batch 3 workflow pitfalls
type: howto
sources: [S042]
updated: 2026-10-28
---

# F004 batch 3 workflow pitfalls

- A full Play Mode run took 25-30 min; use `-TimeoutSeconds 3000` (batch 2 timed out at 900 s). (S042)
- C: ran out of space during the first Android build; `Library/Bee` is about 4.8 GB per worktree. (S042)
- Diff the URP global settings asset after every Unity run; see [F004 URP phase 0 results](./f004-urp-phase0-results.md). (S042)
- A review writer's first draft failed QA on fidelity: wrong section pointers, and dispositions added or softened. Keep reviewer verdicts verbatim. (S042)
- Write generated JSON with `newline="\n"` on Windows; see [F004 S1 spike assets](./f004-s1-spike-assets.md). (S042)
- T412 (B2 performance, #524) was deferred by the owner to a quiet host and is open. (S042)

Related: [F004 batch 2 workflow pitfalls](./f004-batch-2-workflow-pitfalls.md).
