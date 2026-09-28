# Wiki Lint Report — 2026-09-28

Checks run: `index-drift`, `links`, `orphans`, `contradictions`, `stale`, `citations` over 12 pages, after the S001 re-ingest for T064 (Issue #114). Mechanical fixes applied: none needed. INDEX.md matches `pages/`, all relative links and source IDs resolve, and every page has an inbound link (the new `unity-standalone-profiler.md` is linked from `android-emulator-profiling.md` and `mobile-performance-targets.md`). No `⚠ conflict` marker exists. The contradiction check compared the three touched pages with their linked neighbours (`android-emulator-validation.md`, `unity-presentation-boundary.md`, `core-architecture.md`): the default GLES3 build in `android-emulator-validation.md` still keeps profiler autoconnect, which agrees with the new opt-in `-noProfilerAutoconnect` build for profiling.

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | stale | semantic | android-runtime-rotation.md, core-architecture.md, deterministic-production.md, local-checkpoints-and-offline-return.md, player-level-test-gaps.md, ui-toolkit-hud-layout-pitfalls.md | `updated: 2026-09-27` is older than the S001 re-ingest of 2026-09-28. The T064 ingest adds profiling knowledge only and changes none of their cited claims. | No content change needed. Refresh `updated` when a later S001 ingest touches these topics. |

The citation check found no uncited claims. List items on the new page each carry `(S001)`.
