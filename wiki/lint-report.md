# Wiki Lint Report — 2026-11-26

Scope: full pass after the F006 batch 6 part B and closeout ingest (S063, commit 6068ba32). Config: stale_after_days 90, auto_fix index-and-links. Corpus: 135 pages, 63 sources (S001-S063). Stale cutoff: 2026-08-28.

Mechanical checks: index-drift 0 (all 135 pages listed in INDEX.md once, no dead index lines; INDEX.md already in sync, so it was not rewritten; the three S063 pages are indexed), links 0 (the `BLAST` target in android-emulator-profiling.md line 26 sits inside a double-backtick code span, so it is text, not a link; closed without an edit, same as the previous pass), orphans 0 (every page has an inbound link), unknown source IDs 0, frontmatter complete on all pages, every page cites at least one source ID. Contradictions: 0 unresolved `> ⚠ conflict:` markers; no shared-source conflict pairs were assessed in prose, only the marker check was run mechanically. Stale over 90 days: 0. Source re-ingested after page update: 9 pages (rows 1 to 9). Mechanical fixes applied: 0. Suggested fixes: 9 (9 stale, 0 citation).

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | stale | semantic | f005-batch-d-final-evidence.md | Cites S061, last ingested 2026-11-19; page `updated` is 2026-10-27 | re-read S061 against the page; if still accurate, bump `updated`, else edit the claim |
| 2 | stale | semantic | f006-batch-p1-enabling-refactor.md | Cites S056, last ingested 2026-11-26; page `updated` is 2026-11-12 | re-read S056 against the page; bump `updated` if still accurate |
| 3 | stale | semantic | f006-batch-p2-art-and-presentation.md | Cites S056, last ingested 2026-11-26; page `updated` is 2026-11-13 | re-read S056 against the page; bump `updated` if still accurate |
| 4 | stale | semantic | f006-world-presentation-design.md | Cites S056, last ingested 2026-11-26; page `updated` is 2026-11-19 | re-read S056 against the page; bump `updated` if still accurate |
| 5 | stale | semantic | future-free-building-vision.md | Cites S056, last ingested 2026-11-26; page `updated` is 2026-11-11 | re-read S056 against the page; bump `updated` if still accurate |
| 6 | stale | semantic | pixel-camera-scale.md | Cites S056, last ingested 2026-11-26; page `updated` is 2026-11-19 | re-read S056 against the page; bump `updated` if still accurate |
| 7 | stale | semantic | run-shell-and-panels.md | Cites S056, last ingested 2026-11-26; page `updated` is 2026-11-11 | re-read S056 against the page; bump `updated` if still accurate |
| 8 | stale | semantic | unity-presentation-boundary.md | Cites S056, last ingested 2026-11-26; page `updated` is 2026-11-11 | re-read S056 against the page; bump `updated` if still accurate |
| 9 | stale | semantic | visual-identity-art-direction.md | Cites S056, last ingested 2026-11-26; page `updated` is 2026-11-19 | re-read S056 against the page; bump `updated` if still accurate |

## Scoped addendum — 2026-11-27 (F007 plan ingest, S064)

Scoped pass on the two new pages (f007-plan-overview, f007-directions-and-measurement-plan) and the four updated pages (visual-identity-art-direction, art-production-pipeline, f006-android-load-hang-atlas-page-cap, pixel-camera-scale): links, citations and index drift. No new findings. This is not a full pass, so the rows above stay as reported by the 2026-11-26 full pass. The F007 source was renumbered from S063 to S064 at the merge with main, because the F006 closeout registered S063 first.
