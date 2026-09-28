# Wiki Lint Report — 2026-09-28

Checks run: `index-drift`, `links`, `orphans`, `contradictions`, `stale`, `citations` over 12 pages, after the S001 re-ingest for T063 (Issue #113) and T064 (Issue #114). Results: 0 index drift, 0 broken links or unknown source IDs, 0 orphan pages, 0 contradictions or conflict markers, 1 semantic stale finding, and 0 uncited claims. No mechanical fixes were needed. The T063 editor allocation counts and T064's idle-device profile describe separate measurement surfaces and agree; the actively producing device path remains unmeasured.

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | stale | semantic | android-runtime-rotation.md, core-architecture.md, deterministic-production.md, local-checkpoints-and-offline-return.md, player-level-test-gaps.md, ui-toolkit-hud-layout-pitfalls.md | `updated: 2026-09-27` is older than the S001 re-ingest of 2026-09-28. The T063 ingest adds allocation-free presentation-query knowledge and the T064 ingest adds profiling knowledge; neither changes the cited claims on these six pages. | No content change needed. Refresh `updated` when a later S001 ingest touches these topics. |

The citation check found no uncited claims. List items on the new page each carry (S001).
