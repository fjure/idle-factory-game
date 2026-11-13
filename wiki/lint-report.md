# Wiki Lint Report — 2026-11-13

Scope: full pass after the F006 batch 4 ingest (S060, commit b4675b4). Config: stale_after_days 90, auto_fix index-and-links. Corpus: 125 pages, 60 sources (S001-S060).

Mechanical checks: index-drift 0 (all 125 pages listed in INDEX.md, no dead index lines), links 1 (see row 1, not fixed), orphans 0 (every page has an inbound link), unknown source IDs 0. Contradictions: 0 unresolved `> ⚠ conflict:` markers; no pairwise comparison was run in this pass. Mechanical fixes applied: 0 (INDEX.md was already in sync, and the one broken link is prose, see row 1).

Carried from the previous pass: row 3 of the earlier report (f004-display-preferences-store.md superseded note) is resolved; the note now points to the F006 P2 page and cites S059. The earlier stale rows for S056 are still open (rows 2-7 below). The earlier citations row (batch P2 pages) is clean.

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | links | mechanical | android-emulator-profiling.md | Line 26 contains `SurfaceView[…](BLAST)`, which Markdown renders as a relative link to a nonexistent page `BLAST`. It is a SurfaceFlinger layer name, not a page link. Not auto-fixed because the fix edits prose. | Escape the brackets in the page: `SurfaceView\[…\]\(BLAST\)` or wrap the layer name in backticks. Owner or ingest edit. |
| 2 | stale | semantic | f006-batch-p1-enabling-refactor.md | `updated: 2026-11-12`; cites S056, which was re-ingested 2026-11-13. Carried over from the previous pass. | Re-read against S056's latest changes; if still accurate, bump `updated`. Not auto-fixed. |
| 3 | stale | semantic | future-free-building-vision.md | `updated: 2026-11-11`; cites S056 (re-ingested 2026-11-13). Carried over. | Same as row 2. |
| 4 | stale | semantic | pixel-camera-scale.md | `updated: 2026-11-11`; cites S056 (re-ingested 2026-11-13). Carried over. | Same as row 2. |
| 5 | stale | semantic | run-shell-and-panels.md | `updated: 2026-11-11`; cites S056 (re-ingested 2026-11-13). Carried over. | Same as row 2. |
| 6 | stale | semantic | unity-presentation-boundary.md | `updated: 2026-11-11`; cites S056 (re-ingested 2026-11-13). Carried over. | Same as row 2. |
| 7 | stale | semantic | visual-identity-art-direction.md | `updated: 2026-11-11`; cites S056 (re-ingested 2026-11-13). Carried over. | Same as row 2. |

Citations (checked on the batch 4 pages): f006-batch-p3a-art-review-and-import.md has some bullets under a cited lead line (S060) without their own ID. They are sub-points of the cited paragraph, so no finding. Related-link lines in the batch 4 pages are navigation, not claims.

Stale check: no page is older than 90 days (cutoff 2026-08-15). Source-reingest check: 6 pages (rows 2-7) are older than the last ingest of S056.
