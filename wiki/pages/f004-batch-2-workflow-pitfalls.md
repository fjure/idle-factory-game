---
title: F004 batch 2 workflow pitfalls
type: howto
sources: [S041]
updated: 2026-10-28
---

# F004 batch 2 workflow pitfalls

- A usage limit interrupted the #522 worker; `SendMessage` to the same agent after renewing its lease resumed it. (S041)
- The auto-mode classifier once refused a QA/worker `rm` of generated `Assets/UI Toolkit*` leftovers. Test runs also create `Assets/InitTestScene*`, `Assets/UI Toolkit*` and ProjectSettings noise; restore or remove them before committing. (S041)
- A full `test-unity.ps1` run timed out in Play Mode after 900 s in the T408 worktree; the cause was not investigated. (S041)
- Owner decision 2026-10-27: the T411 parity comparator follows R13 literally, so only pixels with a delta above 2/255 count against the 0.1 % budget (recorded on #523). This refines the stricter T409 reading in [URP rendering foundation](./urp-rendering-foundation.md). (S041)

Related: [F004 batch 1 workflow pitfalls](./f004-batch-1-workflow-pitfalls.md).
