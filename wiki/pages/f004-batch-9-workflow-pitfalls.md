---
title: F004 batch 9 workflow pitfalls
type: howto
sources: [S052]
updated: 2026-11-03
---

# F004 batch 9 workflow pitfalls

Batch 9 merged #616, #541, #539 and #543. (S052)

- **Usage-limit kill.** A worker was killed by the weekly usage limit mid-task. Its uncommitted worktree survived; the parent renewed the expired lease (same owner) and resumed the same agent with SendMessage. (S052)
- **Reuse the warm worktree.** Moving one warm-Library worktree from Issue to Issue (`git worktree move`, then `switch -c` from `origin/main`) saved Unity import time across T428, T427, T430 and the player suite. (S052)
- **Small conditions, delta QA.** Small review conditions became a fix-round commit plus a Low-tier delta QA instead of a full re-QA. (S052)
- **ProjectSettings re-serialization.** Test runs re-serialize `ProjectSettings.asset` (iOS icon slots) every time. Never restore `EditorSettings.asset` wholesale once it carries a deliberate setting (the Sprite Atlas v2 mode). (S052)
- **LogError is fatal.** The `test-unity.ps1` runner treats any logged LogError as fatal even under LogAssert, so expected rejection diagnostics are warnings. (S052)

Related: [batch 9 components](./f004-batch-9-pipeline-camera-catalog-import.md), [batch 8 pitfalls](./f004-batch-8-workflow-pitfalls.md).
