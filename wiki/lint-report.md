# Wiki Lint Report — 2026-09-28

Checks run: `index-drift`, `links`, `orphans`, `contradictions`, `stale`, `citations` over 11 pages, after the S001 re-ingest for T040 (Issue #40). Mechanical fixes applied: none needed. INDEX.md matches `pages/`, all relative links and source IDs resolve, and every page has an inbound link.

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | stale | semantic | android-runtime-rotation.md, core-architecture.md, deterministic-production.md, local-checkpoints-and-offline-return.md, player-level-test-gaps.md, ui-toolkit-hud-layout-pitfalls.md | `updated: 2026-09-27` is older than the S001 re-ingest of 2026-09-28. These pages were reviewed against the T040 ingest: it adds profiling and performance-target knowledge only and changes none of their cited claims. | No content change needed. Refresh `updated` when a later S001 ingest touches these topics. |

The citation check found no uncited claims. Paragraphs flagged by a pattern-only scan either carry multi-source citations such as `(S001, S004, S006)` or are list items or table rows under a cited lead sentence.
