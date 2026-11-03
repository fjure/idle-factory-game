# Wiki Lint Report — 2026-11-03

Scope: full pass on branch feat-004-maqa-batch9 after the batch 9 ingest (S052, commit 425a5f4). 103 pages in `pages/` (new: `f004-batch-9-pipeline-camera-catalog-import.md`, `f004-batch-9-workflow-pitfalls.md`), 52 registered sources (S001-S052). Config: stale_after_days 90, auto_fix index-and-links, require_citations true, page_max_words 600.

Mechanical fixes applied: 0 (INDEX drift is zero, and no link target was renamed or missing for the batch 9 pages). Checks were scripted: index drift, relative links, S-id registry, orphans, conflict markers, word cap, frontmatter fields and page type, page-source staleness, registry versus page citations.

## Counts by check

| Check | Open findings | Introduced by batch 9 |
|-------|---------------|-----------------------|
| index-drift | 0 | 0 |
| links | 1 false positive (`(BLAST)`, see #1) | 0 |
| orphans | 0 (both batch 9 pages are linked from pixel-camera-scale, art-production-pipeline and the batch 9 pitfalls page) | 0 |
| contradictions | 1 marker (pre-existing px/pt) | 0 |
| stale | about 69 page-source pairs across about 60 pages, behind sources S018, S022, S019, S038, S029, S027, S045 | 0 (S052 pages and all S052-citing pages are dated 2026-11-03) |
| citations | 0 unknown S-ids, 0 pages without a source ID | 0 |
| registry | pre-existing row/page mismatches (S019, S033, S038, S042, S045, S047, S049); none for S052 | 0 |
| word cap | 21 pages over 600 words | 0 new pages over cap; the two batch 9 pages are 381 and 177 words; `pixel-camera-scale.md` (704) and `visual-identity-art-direction.md` (931) grew |

## Findings

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | links | mechanical | android-emulator-profiling.md (line 26) | `SurfaceView[…](BLAST)` parses as a relative link to a missing page `BLAST`. It is a SurfaceFlinger layer name, not a link. Reappeared since the batch 8 report, not introduced by S052. | Wrap the layer name in backticks (`` `SurfaceView[…]` (BLAST layer) ``). Not edited by lint (outside allowed scope). |
| 2 | contradictions | semantic | ui-toolkit-hud-layout-pitfalls.md | Unresolved conflict marker on px/pt: S001 (about 1.68 px/pt for sw411dp) against S018 (2.09 portrait, 1.69 landscape). Pre-existing. | Owner re-measures; keep the newer claim and drop the marker. |
| 3 | stale | semantic | about 60 pages (S018 24 pairs, S022 27, S019 8, S038 6, S029 2, S027 1, S045 1) | Pre-existing bulk. S038 and S022 are the largest remaining groups. Pages touched by S052 (pixel-camera-scale, art-production-pipeline, unity-presentation-boundary, f004-batch-8-workflow-pitfalls) are now current. | Batch by source: S022 first (27 pairs), then S018 (24 pairs). |
| 4 | registry | mechanical | sources.md | Pre-existing rows list pages that do not cite the source (S019 for run-shell-device-profile-and-text-scale; S033 for 7 pages; S038 for 5; S042, S045, S047, S049 for one each). S052 row is consistent. | Reconcile registry with page citations in one pass. |
| 5 | word cap | structural | visual-identity-art-direction.md (about 931 words), player-level-test-gaps.md (913), android-emulator-validation.md (1080) and 18 others | Over 600 words. Pre-existing. `pixel-camera-scale.md` (704) grew with the S052 paragraph. | Split `visual-identity-art-direction.md` per-batch status history (batch 1-9) into a status page; split pixel-camera-scale if it grows further. |

## Notes on scope

- Contradictions: the check covered the conflict-marker scan and the shared-source pairing. A semantic comparison of batch 9 claims against `pixel-camera-scale.md` and `ui-toolkit-hud-layout-pitfalls.md` (the points-versus-pixels topics) was not performed in this pass; no marker was added by S052.
- Citations: frontmatter-versus-body source mismatches (10 pages in the batch 8 report) were not re-counted in this pass.

## Fixed in this pass

- None. No mechanical fix was needed: INDEX.md already lists both batch 9 pages, and their relative links resolve.

## Next step

Re-ingest or re-verify the S022 page-source pairs (27 pairs, source re-ingested 2026-10-27) to clear the largest stale group. Owner: resolve the px/pt conflict marker in `ui-toolkit-hud-layout-pitfalls.md` by re-measuring.
