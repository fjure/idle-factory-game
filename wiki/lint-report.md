# Wiki Lint Report — 2026-09-28

Checks run: `index-drift`, `links`, `orphans`, `contradictions`, `stale`, `citations` over 13 pages, after the S001 re-ingest for T065 (Issue #115). Results: 0 index drift, 0 broken links or unknown source IDs, 0 orphan pages (the new `frame-rate-target.md` is linked from `mobile-performance-targets.md` and `android-emulator-profiling.md`), 0 conflict markers, 1 semantic stale finding, and 0 uncited claims. No mechanical fixes were needed. The contradiction check compared the pages that mention the frame rate (`mobile-performance-targets.md`, `frame-rate-target.md`, `android-emulator-profiling.md`): the T040 baseline's present-tense "no project code sets `Application.targetFrameRate`" was made historical during the ingest, and the pages now agree.

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | stale | semantic | android-runtime-rotation.md, core-architecture.md, deterministic-production.md, local-checkpoints-and-offline-return.md, player-level-test-gaps.md, ui-toolkit-hud-layout-pitfalls.md | `updated: 2026-09-27` is older than the S001 re-ingest of 2026-09-28. The T063, T064 and T065 ingests add presentation-query, profiling and frame-rate knowledge; none changes the cited claims on these six pages. | No content change needed. Refresh `updated` when a later S001 ingest touches these topics. |

The citation check found no uncited claims. List items and table-bearing paragraphs on the new page each carry (S001).
