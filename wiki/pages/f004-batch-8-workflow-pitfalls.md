---
title: F004 batch 8 workflow pitfalls
type: howto
sources: [S051]
updated: 2026-10-30
---

# F004 batch 8 workflow pitfalls

Batch 8 merged #536 (T424), #537 (T425), #615 (T461) and #538 (T426). Play-Tester omitted: no production, economy or core-loop change. (S051)

- **Parallel text reviewers.** Independent reviewers that return text are run in parallel; the parent records the reports verbatim plus a consolidated table, and a Low-tier QA diffs the record against the saved reports. (S051)
- **Blocker before a freeze becomes a task.** The R1 blocker became revision task T461 instead of a freeze with conditions. (S051)
- **State the measurement method.** Any measured share needs its neighbourhood and edge handling written down; QA failed to reproduce the R1 shares until section 9.3 stated that off-canvas neighbours count. (S051)
- **Resume the author.** The original author agent did the revision and a fix round, reusing its context. (S051)
- **Coordination-branch change rides a parent PR.** A `tasks.md` change made on a coordination branch shipped inside the T426 parent-record PR, so `maqa-merge` gated it. (S051)
- **PPU assignment mistake.** The parent told the author "64 PPU"; F003 placeholders are 64 PPU fallbacks, while D24 sprites use PPU = D = 24. Check the density before stating a PPU in an assignment. (S051)

Related: [Art Bible review and sign-off](./f004-art-bible-v1-review-and-signoff.md), [batch 7 pitfalls](./f004-batch-7-workflow-pitfalls.md).
