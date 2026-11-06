---
title: Unity serialized asset edits
type: decision
sources: [S018, S019]
updated: 2026-11-06
---

# Unity serialized asset edits

**Decision (owner, 2026-10-07):** scene, prefab and other serialized-asset changes are made through Unity tooling, never by hand-editing YAML. In a MAQA worktree that means a temporary editor script run with `-executeMethod` (open the scene, change fields through `SerializedObject`, `EditorSceneManager.SaveScene`), deleted before commit. Unity MCP is bound to the editor on the main checkout and cannot target a worktree. (S018)

## Why YAML edits happened and were rejected

T110, T111 and the first T112 commit inserted scene fields as YAML to keep the diff small. A Unity save would have written the new sprite-catalog fields (`frames`, `hasFramesPerSecond`, `framesPerSecond`, added by T110) into every catalog entry. The owner rejected the workaround: that diff is a legitimate one-time migration, and hand-written YAML bypasses Unity's own validation. (S018, S019)

## What a Unity save produces

The T112 re-save of `CoreGameFoundation.unity` added 297 default-value lines (three per entry for 99 entries), the six intended bootstrap fields, trailing spaces on four `m_Name` lines and a file-order move of the root Transform block. The `m_Children` order did not change. Review such a diff by classifying every changed line; QA rejects a hand-edited scene diff. (S019)

Related: [F003 placeholder art pipeline](./f003-art-pipeline.md), [run shell and panels](./run-shell-and-panels.md), [Unity test and MAQA readiness](./unity-test-and-maqa-readiness.md).
