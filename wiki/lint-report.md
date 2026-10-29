# Wiki Lint Report — 2026-10-29

Scope: full pass on branch feat-004-maqa-batch6 (focus: batch-6 ingest of S049). 97 pages in `pages/`, 49 registered sources (S001-S049). Config: stale_after_days 90, auto_fix index-and-links.

Mechanical fixes applied: 0. Index drift and link checks were clean, so `INDEX.md` was not rewritten. Lint wrote no page prose, claims, conflict markers or `sources.md` entries.

## Counts by check

| Check | Open findings | Introduced by batch 6 | Pre-existing |
|-------|---------------|-----------------------|--------------|
| index-drift | 0 | 0 | 0 |
| links (broken links, unknown S-ids) | 1 | 0 | 1 |
| orphans | 0 | 0 | 0 |
| contradictions | 1 | 0 | 1 |
| stale (source re-ingested after page update) | 70 pairs in 59 pages | 6 pairs | 64 pairs |
| citations (claims without S-id) | 0 | 0 | 0 |
| word cap (page over 600 words, approx. `wc -w`) | 20 pages | 3 | 17 |

## Findings

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | contradictions | semantic | ui-toolkit-hud-layout-pitfalls.md | Unresolved `> ⚠ conflict:` marker, px/pt: S001 says sw411dp phone is about 1.68 px/pt; S018 (T191, 1080x2400) gives 2.09 px/pt portrait and 1.69 px/pt landscape. Pre-existing. | Owner resolves by re-measuring or re-ingesting S001 and S018; keep the newer claim and drop the marker. |
| 2 | links | mechanical | f005-batch-d-final-evidence.md:15 | Citation to unregistered source S061 (EventBase.PreventDefault warning). Pre-existing. | Register S061 in sources.md (needs the source) or correct the citation to a registered S-id. |
| 3 | stale | semantic | art-production-pipeline.md (updated 2026-10-28) | Cites S038, re-ingested 2026-10-29 by batch 6. Introduced by batch 6. | Re-verify the claims against specs/004-visual-identity, then bump `updated`. |
| 4 | stale | semantic | core-architecture.md (2026-10-27) | Cites S038 (re-ingested 2026-10-29). Introduced by batch 6. | Same as row 3. |
| 5 | stale | semantic | f004-batch-1-art-tooling.md (2026-10-27) | Cites S038 (re-ingested 2026-10-29). Introduced by batch 6. | Same as row 3. |
| 6 | stale | semantic | pixel-camera-scale.md (2026-10-28) | Cites S038 (re-ingested 2026-10-29). Introduced by batch 6. | Same as row 3. |
| 7 | stale | semantic | run-shell-and-panels.md (2026-10-27) | Cites S038 (re-ingested 2026-10-29). Introduced by batch 6. | Same as row 3. |
| 8 | stale | semantic | ui-toolkit-hud-layout-pitfalls.md (2026-10-27) | Cites S038 (re-ingested 2026-10-29). Introduced by batch 6. | Same as row 3. |
| 9 | stale | semantic | 59 pages (see counts) | 64 pre-existing page-source pairs where S018 (25 pages), S019 (8), S022 (27), S027 (1), S029 (2), S045 (1) were re-ingested after the page's `updated` date. Pre-existing. | Re-verify each page against the re-ingested source and bump `updated`; batch by source (S022 first, then S018/S019). |
| 10 | word cap | structural | visual-identity-art-direction.md | About 822 words, over the 600-word split rule. Grew by about 51 words in batch 6 (main was 771 before 2b88e0c). | Split into two linked pages (for example Art Bible rules vs. F004 decision history), each under 600 words. |
| 11 | word cap | structural | run-panel-conventions.md | About 767 words. Grew by about 92 words since 2b88e0c^ (batch 6 added 9 lines). | Split panel rules from pitfalls, link both halves. |
| 12 | word cap | structural | f004-s2-spike-and-s1-decision.md | About 615 words, just over the cap. Grew from about 291 to 615 words across batch 5 and 6 (S049 added owner-decision text). | Trim or split (decision record vs. spike measurements). |
| 13 | word cap | structural | 17 other pages (pre-existing, e.g. android-emulator-validation.md 1080, player-level-test-gaps.md 913, f005-plan-overview.md 821) | Over the cap, not touched by batch 6. | Split per SCHEMA.md when next edited. |

## Verified clean (batch 6)

- All five batch-6 pages (`f004-batch-6-workflow-pitfalls.md`, `f004-s2-spike-and-s1-decision.md`, `f004-us8-polish-review.md`, `run-panel-conventions.md`, `visual-identity-art-direction.md`) cite registered sources only. S049 is cited by each of them, and every S049 row in `sources.md` lists pages that cite it.
- `INDEX.md` lists all 97 pages once each, and every index target exists. The batch-6 index lines are present.
- No orphans. No relative links to missing pages.
- No page is older than the 90-day staleness window (cutoff 2026-07-31).
- The `Related:` line in f004-s2-spike-and-s1-decision.md is a link list, not a claim, so it is not a citation finding.

## Notes on the previous report

- The previous report's `(BLAST)` non-page link was not reproduced in this pass. No such link exists in `pages/`.
- The previous report counted 68 stale flags with a different method. This pass counts page-source pairs where a source was re-ingested after the page's `updated` date. The counts are not comparable.

## Next step

Re-verify the six pages that cite S038 (rows 3-8, introduced by batch 6) against `specs/004-visual-identity`, then bump their `updated` dates. This clears the only batch-6 staleness. The highest-severity open item overall is the ui-toolkit px/pt conflict (row 1), which needs an owner decision.
