# Wiki Lint Report — 2026-11-11

Scope: full pass after the F006 batch P0 ingest (S057). Config: stale_after_days 90, auto_fix index-and-links. Corpus: 116 pages, 116 index entries, 57 sources.

Mechanical checks clean: index-drift 0 (116 pages, all listed in INDEX.md; no dead index lines), links 0 broken, orphans 0, unknown citation IDs 0, stale-by-age 0, stale page/source pairs 0 (no page `updated` is older than the last ingest of any source it cites; S057 is 2026-11-11, same day as the F006 P0 pages), contradictions 0 (no `> ⚠ conflict:` marker). The four new F006 P0 pages (f006-p0-baseline, f006-p0-direction-decision, f006-p0-art-tooling, f006-batch-p0-workflow-pitfalls) have frontmatter, S057 citations, index entries and inbound links. Mechanical fixes applied: 0 (none needed).

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | citations | semantic | (85 pages, 167 prose lines; heuristic: prose line of 60+ characters without an S-id, excluding headings, table rows, frontmatter and link-only lines) | Largest counts: f005-plan-overview.md (16, e.g. lines 14-15), f006-plan-overview.md (12, lines 14-15), f004-s3-s4-cue-and-lighting-decision.md (11), android-emulator-validation.md (10), f006-world-presentation-design.md (7, lines 14-15), run-economy-and-prestige.md (7), unity-test-and-maqa-readiness.md (7). The F006 P0 pages have no findings. | Add the source ID where a line states a claim with a clear source; confirm structural or restating lines. Not auto-fixed. Note: the heuristic is stricter than the 2026-11-06 pass (71 lines), so the count is not directly comparable. |

Contradiction sampling (bounded, not exhaustive): pages sharing S057 or a link were compared on the F006 direction, hub label, OD-9 status and P0/P1 status (f006-plan-overview, f006-p0-direction-decision, f006-world-presentation-design, visual-identity-art-direction, art-production-pipeline). No incompatible claims found.
