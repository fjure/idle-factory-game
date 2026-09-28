# Wiki Lint Report — 2026-09-28

Checks run: `index-drift`, `links`, `orphans`, `contradictions`, `stale`, `citations` over 14 pages, after the S001 re-ingest for T041 (Issue #41, PR #128). Results: 0 index drift, 0 broken links or unknown source IDs, 0 orphan pages, 0 conflict markers, 1 semantic stale finding and 0 uncited claims. No mechanical fixes were needed. `mobile-performance-targets.md` and `player-level-test-gaps.md` link the new `feature-acceptance-evidence.md`, and it links back to both.

The contradiction check compared the pages linked with the new page. `mobile-performance-targets.md` no longer describes T041's characterization as pending; it states the outcome, which matches `feature-acceptance-evidence.md` (one host, within-session comparison, no producing-factory evidence). `player-level-test-gaps.md` and the new page describe the SC-002 composed-evidence gap (T041-F2) the same way.

The citation heuristic flagged five paragraphs without an inline citation. Each one is a list lead-in or a numbered procedure whose items or following paragraph carry (S001), or a "See …" cross-reference. None is an uncited claim.

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | stale | semantic | android-runtime-rotation.md, core-architecture.md, deterministic-production.md, local-checkpoints-and-offline-return.md, ui-toolkit-hud-layout-pitfalls.md | `updated: 2026-09-27` is older than the S001 re-ingest of 2026-09-28. The T041 ingest adds acceptance-evidence knowledge and changes none of the claims cited on these five pages. | No content change needed. Refresh `updated` when a later S001 ingest touches these topics. |
