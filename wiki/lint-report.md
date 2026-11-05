# Wiki Lint Report — 2026-11-05

Scope: full pass on branch feat-004-maqa-batch11 after the batch 11 ingest (S054). 107 pages in `pages/` (new: `f004-batch-11-floor-settings-icons.md` 619 words, `f004-batch-11-workflow-pitfalls.md` 262 words), 54 registered sources (S001-S054). Config: stale_after_days 90, auto_fix index-and-links, require_citations true, page_max_words 600. Checks were scripted.

Mechanical fixes applied: 0. INDEX drift is zero (all 107 pages listed, no dangling entries), no broken link target was found beyond the known `(BLAST)` false positive, and the S054 registry row matches the 7 pages that cite S054.

## Counts by check

| Check | Open findings | Introduced by batch 11 |
|-------|---------------|------------------------|
| index-drift | 0 | 0 |
| links | 1 false positive (`(BLAST)` in android-emulator-profiling.md, see #1) | 0 |
| orphans | 0 | 0 |
| contradictions | 1 marker (px/pt in ui-toolkit-hud-layout-pitfalls.md, see #2) | 0 new marker; the page cites S054 again, so it is in scope |
| stale | 64 page-source pairs (was 65), behind S022 (26), S018 (24), S019 (8), S038 (4) | 0 (no batch 11 page or S054 pairing is stale; S054 and all S054-citing pages are dated 2026-11-05) |
| citations | 0 unknown S-ids, 0 pages with bad frontmatter | 0 |
| registry | pre-existing row/page mismatches (S019, S033, S038, S042, S045, S047, S049, S052 pattern); S054 row lists exactly the 7 citing pages | 0 |
| word cap | 22 pages over 600 words | 2 batch 11 pages over cap (see #5) |

## Findings

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | links | mechanical | android-emulator-profiling.md (line 26) | `SurfaceView[…](BLAST)` parses as a relative link to missing page `BLAST`. Pre-existing. | Wrap the layer name in backticks (not edited by lint). |
| 2 | contradictions | semantic | ui-toolkit-hud-layout-pitfalls.md (line 20) | Unresolved px/pt conflict marker: S001 says `sw411dp` runs at about 1.68 px/pt; S018 (T191, 1080×2400 panel) says `ScaleWithScreenSize` blends width and height ratios. Pre-existing marker, but the page was touched by the batch 11 ingest (S054, commit 29dc1bf). | Owner re-measures; keep the newer claim and drop the marker. |
| 3 | stale | semantic | about 60 pages | Bulk staleness behind S022 (26 pairs), S018 (24), S019 (8), S038 (4). None involve batch 11 pages. | Batch by source: S022 first, then S018. |
| 4 | registry | mechanical | sources.md | S038 lists pages that do not cite S038; older rows likewise. Not reconciled here (pre-existing, outside the batch 11 scope). | Reconcile registry with page citations in one pass. |
| 5 | word cap | structural | f004-batch-11-floor-settings-icons.md (619), art-production-pipeline.md (618), visual-identity-art-direction.md (1040), ui-toolkit-hud-layout-pitfalls.md (679), unity-presentation-boundary.md (668) and 17 others | Over 600 words. The batch 11 floor page and art-production-pipeline.md are only slightly over. | Trim the floor page or split its status history into a separate page; split `visual-identity-art-direction.md` status history as previously proposed. |

## Next step

Resolve the px/pt conflict marker in `ui-toolkit-hud-layout-pitfalls.md` (owner re-measurement), then re-ingest S022 to clear the largest stale group. Trimming `f004-batch-11-floor-settings-icons.md` below 600 words is the smallest structural fix from this batch.
