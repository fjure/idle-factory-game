# Wiki Lint Report — 2026-11-04

Scope: full pass on branch feat-004-maqa-batch10 after the batch 10 ingest (S053). 105 pages in `pages/` (new: `f004-batch-10-lattice-zoom-ore-vein-art-import.md` 440 words, `f004-batch-10-workflow-pitfalls.md` 146 words), 53 registered sources (S001-S053). Config: stale_after_days 90, auto_fix index-and-links, require_citations true, page_max_words 600. Checks were scripted.

Mechanical fixes applied: 0 (INDEX drift is zero; no link target missing). One registry line I added to the S038 row was removed again before this report because the batch 10 page cites S053, not S038.

## Counts by check

| Check | Open findings | Introduced by batch 10 |
|-------|---------------|------------------------|
| index-drift | 0 | 0 |
| links | 1 false positive (`(BLAST)` in android-emulator-profiling.md, see #1) | 0 |
| orphans | 0 | 0 |
| contradictions | 1 marker (pre-existing px/pt) | 0 |
| stale | 65 page-source pairs (was about 69), mainly behind S018, S022, S019, S038 | 0 (S053-citing pages are dated 2026-11-04) |
| citations | 0 unknown S-ids, 0 pages with bad frontmatter | 0 |
| registry | pre-existing row/page mismatches (S019, S033, S038, S042, S045, S047, S049, S052 pattern); none for S053 | 0 |
| word cap | 20 pages over 600 words | 0 new; `pixel-camera-scale.md` now 761 and `visual-identity-art-direction.md` 981 grew |

## Findings

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | links | mechanical | android-emulator-profiling.md (line 26) | `SurfaceView[…](BLAST)` parses as a relative link to missing page `BLAST`. Pre-existing. | Wrap the layer name in backticks (not edited by lint). |
| 2 | contradictions | semantic | ui-toolkit-hud-layout-pitfalls.md | Unresolved px/pt conflict marker: S001 against S018. Pre-existing. | Owner re-measures; keep the newer claim and drop the marker. |
| 3 | stale | semantic | about 60 pages | Bulk staleness behind S018, S022, S019, S038. S038 was re-ingested 2026-11-04; core-architecture, f004-batch-1-art-tooling, f004-batch-6-workflow-pitfalls, ui-toolkit-hud-layout-pitfalls and urp-rendering-foundation lag it. | Batch by source: S022 first, then S018. |
| 4 | registry | mechanical | sources.md | S038 lists 8 pages that do not cite S038 (for example the two F004 batch 9 pages); older rows likewise. | Reconcile registry with page citations in one pass. |
| 5 | word cap | structural | visual-identity-art-direction.md (981), pixel-camera-scale.md (761) and 18 others | Over 600 words. | Split the per-batch status history of `visual-identity-art-direction.md` into a status page; split `pixel-camera-scale.md` into rules and per-task history. |

## Next step

Split `visual-identity-art-direction.md` (batch 1-10 status history) and `pixel-camera-scale.md`, then re-verify the S022 stale pairs. Owner: resolve the px/pt conflict marker.
