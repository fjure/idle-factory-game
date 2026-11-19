# Wiki Lint Report — 2026-11-19

Scope: full pass after the F006 batch 6 part A ingest (S062, commit 6e124e6e). Config: stale_after_days 90, auto_fix index-and-links. Corpus: 132 pages, 62 sources (S001-S062).

Mechanical checks: index-drift 0 (all 132 pages listed in INDEX.md once, no dead index lines; INDEX.md already in sync, so it was not rewritten; the three batch 6 pages are indexed), links 0 (the row 1 finding from the previous pass is a false positive: `SurfaceView[…](BLAST)` sits inside a double-backtick code span on line 26 of android-emulator-profiling.md, so it renders as code, not a link; closed without an edit), orphans 0 (every page has an inbound link), unknown source IDs 0, frontmatter complete on all pages. Contradictions: 0 unresolved `> ⚠ conflict:` markers (the text "add/add conflict:" in unity-test-and-maqa-readiness.md is prose, not a marker). Stale over 90 days: 0 (cutoff 2026-08-21). Mechanical fixes applied: 0. Suggested fixes: 7 (6 stale, 1 citation).

The three new batch 6 pages (f006-batch-6-verification-and-polish, f006-batch-6-copy-conformance-and-polish, f006-batch-6-workflow-pitfalls) have complete frontmatter, cite S062, resolve all links (including `../../docs/agents/workstation-pitfalls.md`), and have no uncited claim lines. They are not stale.

Changes since the batch 5 pass (2026-11-19, earlier same day): the earlier link row is closed as a false positive (row 1 removed). S056 was re-ingested again on 2026-11-19 (registry row, commit 1f23077b), so the stale rows now cover 6 pages; `f006-batch-p2-art-and-presentation.md` is added as row 6 because it was updated 2026-11-13, before that re-ingest. Rows for pages updated on 2026-11-19 (f006-plan-overview, f006-world-presentation-design, pixel-camera-scale) are closed.

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | stale | semantic | f006-batch-p1-enabling-refactor.md | `updated: 2026-11-12`; cites S056, last ingested 2026-11-19. | Re-read against S056's latest changes; if still accurate, bump `updated`. Not auto-fixed. |
| 2 | stale | semantic | f006-batch-p2-art-and-presentation.md | `updated: 2026-11-13`; cites S056, last ingested 2026-11-19. New in this pass. | Same as row 1. |
| 3 | stale | semantic | future-free-building-vision.md | `updated: 2026-11-11`; cites S056, last ingested 2026-11-19. | Same as row 1. |
| 4 | stale | semantic | run-shell-and-panels.md | `updated: 2026-11-11`; cites S056, last ingested 2026-11-19. | Same as row 1. |
| 5 | stale | semantic | unity-presentation-boundary.md | `updated: 2026-11-11`; cites S056, last ingested 2026-11-19. | Same as row 1. |
| 6 | stale | semantic | visual-identity-art-direction.md | `updated: 2026-11-11`; cites S056, last ingested 2026-11-19. | Same as row 1. |
| 7 | citations | semantic | f006-batch-5-gate-and-owner-decisions.md | Owner decision table, rows 2026-11-17 to 2026-11-19 (8 rows), carries no per-row source ID. The page-level `(S061)` line after the table covers them. Carried over from the previous pass; not re-checked in this pass. | Either add `(S061)` to each row, or accept the page-level citation for table rows. Owner or ingest edit. |

Stale check: no page is older than 90 days. Source-reingest check: 6 pages (rows 1-6) are older than the last ingest of S056 (2026-11-19).
