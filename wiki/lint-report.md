# Wiki Lint Report — 2026-11-06

Scope: full pass after the Issue #675 maintenance edits (branch docs/675-wiki-maintenance). Config: stale_after_days 90, auto_fix index-and-links.

Mechanical checks clean: index-drift 0 (109 pages, 109 index entries), links 0 broken, orphans 0, unknown citation IDs 0, stale-by-age 0, stale page/source pairs 0 (every page's `updated` is not older than the last ingest of any of its sources), contradictions 0 (no `> ⚠ conflict:` marker left). Mechanical fixes applied: 0.

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | citations | semantic | (multiple, 71 lines) | 71 prose lines without an S-id (heuristic; mostly list items under a cited section, link-pointer lines and numbered steps of cited procedures; sample: android-emulator-validation.md:14; android-emulator-profiling.md:10; f004-s3-s4-cue-and-lighting-decision.md:8; f005-plan-overview.md:14) | add the source ID where a claim has an unambiguous multi-source origin, or confirm the line is structural/restates a cited sentence; not auto-fixed |
