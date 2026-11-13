# Wiki Lint Report — 2026-11-13

Scope: pass after the F006 batch P2 ingest (S059). Config: stale_after_days 90, auto_fix index-and-links. Corpus: 120 pages, 58+1 sources (S059 new). Mechanical checks clean for the P2 scope: index-drift 0 (both new P2 pages listed in INDEX.md, no dead index lines), links 0 broken in the F006 pages and index, orphans 0 (both P2 pages have inbound links), citations 0 unknown IDs, contradictions 0 markers (no `> ⚠ conflict:` marker). Mechanical fixes applied: 0 (none needed; INDEX.md not regenerated).

Full-corpus link sweep was not completed in this pass (output exceeded the read budget); links were verified for all F006 pages and the index.

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | stale | semantic | future-free-building-vision.md, pixel-camera-scale.md, run-shell-and-panels.md, unity-presentation-boundary.md, visual-identity-art-direction.md | Each cites S056 with `updated: 2026-11-11`; S056 was re-ingested on 2026-11-13 (sources.md). Carried over from the P1 pass, not re-verified. | Re-read each page against S056's P2 changes; if still accurate, bump `updated`. Not auto-fixed. |
| 2 | stale | semantic | f006-batch-p1-enabling-refactor.md | Cites S056 with `updated: 2026-11-12`; S056 re-ingested 2026-11-13. | Same as row 1. Not auto-fixed. |
| 3 | stale | semantic | f004-display-preferences-store.md | Describes `display-v1.json` only. The P2 page (T538) introduces `display-v2.json` with v1 fallback when v2 is absent (S059). The predecessor page carries no superseded note. Not a contradiction, but the page is now partly out of date. | Add a short "superseded in F006 P2 by display-v2.json (S059)" note with a link to [F006 batch P2 art and presentation](./f006-batch-p2-art-and-presentation.md). Owner or ingest edit. |
| 4 | citations | semantic | f006-batch-p2-art-and-presentation.md, f006-batch-p2-workflow-pitfalls.md | Checked: every prose line carries (S059). No findings. | None. |

Contradiction sampling (bounded): the P2 pages were compared with f006-plan-overview, f006-world-presentation-design, f004-display-preferences-store and the P2 follow-up references. No incompatible claims found. The P1 status conflict from the previous pass (f006-plan-overview line 10 vs line 32) is no longer present: line 10 reports P0 and P1 merged and P2 merged, consistent with line 32.
