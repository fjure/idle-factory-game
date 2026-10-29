# Wiki Lint Report — 2026-10-29

Scope: full pass on branch feat-004-maqa-batch7, after the batch 7 ingest (S050). 99 pages in `pages/` (new: `f004-s3-s4-cue-and-lighting-decision.md`, `f004-batch-7-workflow-pitfalls.md`), 50 registered sources (S001-S050). Config (`.specify/extensions/wiki/wiki-config.yml`): stale_after_days 90, auto_fix index-and-links, require_citations true, page_max_words 600.

Mechanical fixes applied: 0. Lint was asked to report only, so no page, link or registry entry was changed. `INDEX.md` drift is zero, so no regeneration would be needed. All rows below are suggested fixes.

## Counts by check

| Check | Open findings | Introduced by batch 7 (S050) | Pre-existing |
|-------|---------------|------------------------------|--------------|
| index-drift | 0 | 0 | 0 |
| links (broken relative links, unknown S-ids) | 2 | 0 (row 1 origin not established) | 2 |
| orphans | 0 | 0 | 0 |
| contradictions | 2 | 1 (row 8) | 1 (row 9) |
| stale (source re-ingested after page `updated`, or page older than 90 days) | 67 page-source pairs; 0 pages older than 90 days | 0 | 67 |
| citations (uncited blocks, frontmatter/body source mismatches) | 12 uncited blocks (rows 5-7 and row 12); 14 mismatches (row 4 and row 13) | 3 uncited, 1 mismatch | 9 uncited, 13 mismatches |
| registry (sources.md rows listing pages that do not cite them) | 19 rows | 1 (row 3) | 18 (row 14) |
| word cap (body over 600 words) | 16 pages | 1 (visual-identity-art-direction.md, touched by S050) | 15 |

Both new pages are under the 600-word cap and have complete frontmatter. Every S-id they cite is registered.

## Findings

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | links | mechanical | android-emulator-profiling.md | Line in the "SurfaceView" layer paragraph reads `` `SurfaceView[�](BLAST)` ``. It parses as a Markdown link to a relative page `BLAST`, which does not exist. The `�` is a replacement character, so the original text is damaged. **Not reported by the previous lint, which said no such link existed; this pass reproduces it.** | Wrap the layer name in backticks (`` `SurfaceView[…]` ``) and restore the lost character from the source. |
| 2 | links | mechanical | f005-batch-d-final-evidence.md:15 | Citation to S061, which is not in `sources.md`. Pre-existing. | Register S061 (needs the source) or correct the citation to a registered S-id. |
| 3 | registry | mechanical | sources.md, row S038 | Lists `f004-s3-s4-cue-and-lighting-decision.md` as a page touched by S038, but that page does not cite S038. Introduced by batch 7. | Correct the S038 row, or add the citation if the claim really rests on S038. |
| 4 | citations | mechanical | f004-batch-7-workflow-pitfalls.md (frontmatter) | Frontmatter `sources` lists S049, but the body never cites S049. `sources.md` row S049 does not list this page either. Introduced by batch 7. | Either cite (S049) in the body where the batch 6 carry-over applies, or drop S049 from frontmatter and keep S050 only. |
| 5 | citations | semantic | f004-s3-s4-cue-and-lighting-decision.md, "Why" paragraph | Rationale paragraph ("Blind naming tests whether a symbol is distinguishable…") carries no S-id. Introduced by batch 7. | Add (S050) to the paragraph. |
| 6 | citations | semantic | f004-s3-s4-cue-and-lighting-decision.md, "Carried to T424" bullets | Three bullets (badge colour semantics, remaining 24 pt gaps, Light2D render cost) carry no S-id. Introduced by batch 7. | Add (S050) to the bullets, or (S046) where the claim comes from the style spike. |
| 7 | citations | semantic | f004-batch-7-workflow-pitfalls.md, bullets | Only the opening paragraph cites (S050). Each lesson bullet (blind naming isolation, sealed trial keys, and so on) states a claim without an S-id. Introduced by batch 7. | Add (S050) per bullet, or state in the page that the opening citation covers the whole list. The owner decides which convention applies. |
| 8 | contradictions | semantic | f004-s2-spike-and-s1-decision.md, AD condition (2), line 35 | Says cue icons must be "17x17 art px or larger". S050 records that 17 px misses the 24 pt rule on the emulator (19.4 pt) and tablet landscape (18.2 pt), and the final decision is 23 px (f004-s3-s4 page). The S2 page has no conflict marker or superseded note. Introduced by batch 7. | Owner or ingest adds a superseded note on condition (2) pointing to the S3/S4 decision page. Keep the 23 px claim; drop or annotate the 17 px claim. Not auto-fixed (semantic). |
| 9 | contradictions | semantic | ui-toolkit-hud-layout-pitfalls.md | Unresolved `> ⚠ conflict:` marker on px/pt: S001 (about 1.68 px/pt for sw411dp) against S018 (2.09 px/pt portrait, 1.69 px/pt landscape). Pre-existing. | Owner re-measures or re-ingests S001 and S018; keep the newer claim and drop the marker. |
| 10 | stale | semantic | art-production-pipeline.md, core-architecture.md, f004-batch-1-art-tooling.md, pixel-camera-scale.md, run-shell-and-panels.md, ui-toolkit-hud-layout-pitfalls.md | Cite S038, re-ingested 2026-10-29 by batch 6, after each page's `updated` date. Still open after batch 7. Introduced by batch 6. | Re-verify each page against `specs/004-visual-identity`, then bump `updated`. |
| 11 | stale | semantic | 61 other page-source pairs (S018, S019, S022, S027, S029, S045 re-ingested after the page's `updated`) | Pre-existing bulk. S022 (re-ingested 2026-10-27) accounts for most. Batch 7 introduced none: S046 and S050 pairs are current. | Batch by source: S022 first, then S018 and S019. |
| 12 | citations | semantic | 9 pre-existing uncited paragraphs or lists: android-back-gesture.md, android-emulator-profiling.md, f004-b2-frozen-budgets.md, factory-run-simulation.md, json-content-migration-evidence.md, run-bootstrap-and-lifecycle.md, run-shell-and-panels.md (2), save-v2-and-v1-import.md | Several are navigation sentences ("The shell built on this bootstrap is described in…") or numbered summaries with no S-id. Heuristic; some may be non-claims. Pre-existing. | Add the S-id where the paragraph states a fact, or leave navigation text alone. |
| 13 | citations | mechanical | 13 pre-existing frontmatter/body source mismatches (e.g. f005-batch-c-content-revision-3.md body cites S029 but not in frontmatter; json-content-authoring.md body cites S009 but not in frontmatter; f004-b2-frozen-budgets.md frontmatter has S047 but body does not; android-emulator-validation.md body cites S045, frontmatter lacks it and lists S022, which the body does not cite) | Frontmatter and body disagree. Pre-existing. | Reconcile each page's `sources` with the cited IDs. Batch 7 added only row 4. |
| 14 | registry | mechanical | sources.md | 18 pre-existing rows list pages that do not cite the source (e.g. S033, S038, S042, S045, S047, S049 rows) besides row 3. | Reconcile the registry with page citations in a single pass. |
| 15 | word cap | structural | visual-identity-art-direction.md | Body about 856 words, over the 600-word split rule. Touched by S050 (new batch 7 paragraph). | Split into two linked pages (Art Bible rules vs. F004 decision history), each under 600 words. |
| 16 | word cap | structural | 15 other pages (pre-existing), e.g. android-emulator-validation.md (about 1064), player-level-test-gaps.md (about 897), f005-plan-overview.md (about 805) | Over the cap, not touched by batch 7. | Split per SCHEMA.md when next edited. |

## Verified clean (batch 7)

- Index: all 99 pages listed once, every index target exists, both new pages are indexed (the S3/S4 decision under Decisions, the batch 7 pitfalls under How-to).
- No orphans. Both new pages are linked from `visual-identity-art-direction.md`, and each links the other or its predecessor page.
- No relative links from the new pages point to missing files.
- No `> ⚠ conflict:` marker was added by batch 7.
- Both new pages are under 600 words and no page is older than the 90-day cutoff (2026-07-31).
- Registered S-ids: every S-id cited in `pages/` exists in `sources.md` except S061 (row 2).

## Notes on the previous report

- The previous report said no `(BLAST)` link existed. It does: row 1 above.
- Counts are not comparable with the previous report. This pass counts page-source pairs (67) and body words (not `wc -w`, which included frontmatter).

## Next step

Fix row 1 first: it is a mechanical link break that renders wrong in the page, and it takes one edit to wrap the layer name in backticks. Then re-verify the six S038 pages (row 10) against `specs/004-visual-identity` and bump their `updated` dates. The owner decides row 8 (the 17 px vs. 23 px superseded note), which is the batch 7 contradiction to resolve.
