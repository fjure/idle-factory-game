# Wiki Lint Report — 2026-10-30

Scope: full pass on branch feat-004-maqa-batch8-close after the batch 8 ingest (S051). 101 pages in `pages/` (new: `f004-art-bible-v1-review-and-signoff.md`, `f004-batch-8-workflow-pitfalls.md`), 51 registered sources (S001-S051). Config: stale_after_days 90, auto_fix index-and-links, require_citations true, page_max_words 600.

Mechanical fixes applied: 0 (INDEX drift is zero). Checks were scripted: index, relative links, S-id registry, orphans, conflict markers, word cap, frontmatter versus body citations, registry versus page citations.

## Counts by check

| Check | Open findings | Introduced by batch 8 |
|-------|---------------|-----------------------|
| index-drift | 0 | 0 |
| links | 0 (earlier S061 and `(BLAST)` findings no longer reproduce) | 0 |
| orphans | 0 | 0 |
| contradictions | 1 | 0 |
| stale | page-source pairs behind re-ingested sources, mainly S038 (now re-ingested 2026-10-30), S022, S018, S019, S045 | S038 pairs |
| citations | 10 frontmatter/body source mismatches, all pre-existing (the batch 7 and S3/S4 pages are now consistent) | 0 |
| registry | 18 rows list pages that do not cite the source (S038 now lists 6 such pages; the S3/S4 row mismatch was fixed) | 0 |
| word cap | 17 pages over 600 words | 0 new pages; `visual-identity-art-direction.md` (about 907) and `f004-s2-spike-and-s1-decision.md` (about 640) grew by one status paragraph |

## Fixed by the batch 8 ingest

- `f004-s2-spike-and-s1-decision.md`: AD condition (2) "17x17 or larger" now carries a superseded note (final 23 px).
- Batch 7 pitfalls and S3/S4 pages: bullets now cite (S050); the S049 frontmatter source is cited in the body; registry rows S038 and S050 corrected.

## Findings

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|----------|---------------|
| 1 | contradictions | semantic | ui-toolkit-hud-layout-pitfalls.md | Unresolved conflict marker on px/pt: S001 (about 1.68 px/pt for sw411dp) against S018 (2.09 portrait, 1.69 landscape). Pre-existing. | Owner re-measures; keep the newer claim and drop the marker. |
| 2 | stale | semantic | art-production-pipeline.md, core-architecture.md, f004-batch-1-art-tooling.md, pixel-camera-scale.md, run-shell-and-panels.md, ui-toolkit-hud-layout-pitfalls.md | Cite S038, re-ingested 2026-10-30, after each page's `updated`. | Re-verify against `specs/004-visual-identity`, then bump `updated`. |
| 3 | stale | semantic | about 60 other page-source pairs (S018, S019, S022, S027, S029, S045) | Pre-existing bulk. | Batch by source: S022 first. |
| 4 | registry | mechanical | sources.md | 18 rows (S033, S038, S042, S045, S047, S049 among them) list pages that do not cite the source. | Reconcile registry with page citations in one pass. |
| 5 | citations | mechanical | 10 pages | Frontmatter `sources` differs from S-ids cited in the body (for example f005-batch-c-content-revision-3.md, json-content-authoring.md, f004-b2-frozen-budgets.md). | Align frontmatter with body. |
| 6 | word cap | structural | visual-identity-art-direction.md (about 907 words) and 16 others | Over 600 words. | Split Art Bible rules from F004 decision history (batch status paragraphs are the natural split). |

## Next step

Split `visual-identity-art-direction.md` by moving the per-batch status paragraphs (batches 1-8) into a status history page; that clears the largest cap breach that every F004 ingest keeps growing.
