# Wiki Lint Report — 2026-11-19

Scope: full pass after the F006 batch 5 ingest (S061, commit e1d497d0). Config: stale_after_days 90, auto_fix index-and-links. Corpus: 129 pages, 61 sources (S001-S061).

Mechanical checks: index-drift 0 (all 129 pages listed in INDEX.md once, no dead index lines; INDEX.md already in sync, so it was not rewritten), links 1 (row 1, not fixed), orphans 0 (every page has an inbound link), unknown source IDs 0, frontmatter complete on all pages. Contradictions: 0 unresolved `> ⚠ conflict:` markers. Bounded pairwise review of the pages touched by S061 (android-emulator-validation, desktop-focus-lifecycle, f006-plan-overview, f006-world-presentation-design, pixel-camera-scale, run-persistence and the four new batch 5 pages) found no incompatible claims; the T559 autosave and T558 framing text agree with the older pages they qualify. Mechanical fixes applied: 0. Suggested fixes: 7.

Changes since the batch 4 pass (2026-11-13): pixel-camera-scale.md was updated on 2026-11-19 (S061) and is no longer stale, so the earlier stale row for it is closed. The earlier stale rows for S056 remain open (rows 2-6). The earlier link row is still open (row 1).

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | links | mechanical | android-emulator-profiling.md | Line 26 contains `SurfaceView[…](BLAST)`, which Markdown renders as a relative link to a nonexistent page `BLAST`. It is a SurfaceFlinger layer name, not a page link. Not auto-fixed because the fix edits prose. | Escape the brackets in the page: `SurfaceView\[…\]\(BLAST\)` or wrap the layer name in backticks. Owner or ingest edit. |
| 2 | stale | semantic | f006-batch-p1-enabling-refactor.md | `updated: 2026-11-12`; cites S056, which was re-ingested 2026-11-13. Carried over from the previous pass. | Re-read against S056's latest changes; if still accurate, bump `updated`. Not auto-fixed. |
| 3 | stale | semantic | future-free-building-vision.md | `updated: 2026-11-11`; cites S056 (re-ingested 2026-11-13). Carried over. | Same as row 2. |
| 4 | stale | semantic | run-shell-and-panels.md | `updated: 2026-11-11`; cites S056 (re-ingested 2026-11-13). Carried over. | Same as row 2. |
| 5 | stale | semantic | unity-presentation-boundary.md | `updated: 2026-11-11`; cites S056 (re-ingested 2026-11-13). Carried over. | Same as row 2. |
| 6 | stale | semantic | visual-identity-art-direction.md | `updated: 2026-11-11`; cites S056 (re-ingested 2026-11-13). Carried over. | Same as row 2. |
| 7 | citations | semantic | f006-batch-5-gate-and-owner-decisions.md | Owner decision table, rows 2026-11-17 to 2026-11-19 (8 rows), carries no per-row source ID. The page-level `(S061)` line after the table covers them, so this is a strictness question, not a missing source. | Either add `(S061)` to each row, or accept the page-level citation for table rows. Owner or ingest edit. |

Stale check: no page is older than 90 days (cutoff 2026-08-21). Source-reingest check: 5 pages (rows 2-6) are older than the last ingest of S056.
