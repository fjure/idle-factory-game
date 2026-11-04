---
title: F004 batch 10 workflow pitfalls
type: howto
sources: [S053]
updated: 2026-11-04
---

# F004 batch 10 workflow pitfalls

Batch 10 merged #592, #540, #623 and #554. (S053)

- **APK size comparisons.** Gradle in-place packaging left about 19 MB of dead gaps (a 66 MB file against about 46.4 MB of content). Compare clean builds, or sum the entries with `unzip -v`, never in-place file sizes. (S053)
- **Atlas memory.** Compute atlas memory per page, not per sprite pixel. (S053)
- **Stale pytest launchers.** Launchers in `tools/art-pipeline/.venv` went stale; `uv sync --frozen --offline --reinstall-package pytest` fixes them. (S053)
- **Unimported asset.** A candidate that misses a geometry contract (the 60 x 30 floor tile against the 70 x 54 period) is not imported to make the batch green; it becomes a re-render task and an import task. (S053)

Related: [batch 10 components](./f004-batch-10-lattice-zoom-ore-vein-art-import.md), [batch 9 pitfalls](./f004-batch-9-workflow-pitfalls.md).
