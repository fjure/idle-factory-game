---
title: F006 batch P1 workflow pitfalls
type: howto
sources: [S058]
updated: 2026-11-12
---

# F006 batch P1 workflow pitfalls

- **Reuse the imported `Library/` between sequential Unity Issues.** Move (`mv`) the `Library/` of the merged worktree into the next Issue's fresh worktree, and each Unity run imports incrementally instead of from scratch. A worktree without `Library/` also removes cleanly, which avoids the `\\?\` long-path delete described in the [P0 pitfalls](./f006-batch-p0-workflow-pitfalls.md). (S058)
- **Every Unity run leaves noise.** It re-serializes `ProjectSettings/ProjectSettings.asset` (+130/-1, iOS icon slots) and generates an untracked `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss` plus `Assets/UI Toolkit.meta`. Restore or remove both before committing. (S058)
- For the code-side learnings (packed array YAML, TagManager gap, timing flake, rate-limit resume, gh error) see [batch P1 enabling refactor](./f006-batch-p1-enabling-refactor.md). (S058)
