# Wiki Lint Report — 2026-10-13

Scope: full pass after the F005 batch 1 ingest (commit 81ce5ed, branch `docs/005-batch1-closeout`). Config: `lint.stale_after_days` 90, `lint.auto_fix` index-and-links. 60 pages in `pages/`, 27 sources (S001–S027).

Mechanical checks: `index-drift` 0 (INDEX lists exactly the 60 pages; both new pages `f005-content-v3-transitional.md` and `f005-batch-1-workflow-pitfalls.md` are registered). `links` 0 (no broken relative or INDEX links). `citations` unknown S-ids 0. `orphans` 0 (every page has an inbound link). Frontmatter (`title`, `type`, `sources`, `updated`) present on all pages. Fixes applied: none needed; INDEX.md and link targets were already consistent, so nothing was regenerated.

Semantic checks: `contradictions` 1 open conflict marker (row 1, carried over). Shared-source and shared-link pairs were checked for markers only; a full pairwise claim comparison was not run at Low tier. `citations`: the two new pages have no uncited bullets. `stale`: see rows 2–41. Most are the same pattern: a page cites S018 (or S022) whose `Last ingested` date in `sources.md` is later than the page's `updated` date, so the page has not been re-reviewed against the re-ingested source.

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | contradictions | semantic | ui-toolkit-hud-layout-pitfalls.md (line 20) | Open `> ⚠ conflict:` marker: S001 says `sw411dp` runs at about 1.68 px/pt in either orientation; S018 (T191, 1080×2400 panel) gives 2.09 px/pt portrait and 1.69 px/pt landscape. Carried over from the 2026-10-10 pass. | Owner decision (S001 vs S018). Lint does not resolve it. |
| 2 | stale | semantic | android-runtime-rotation.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018 and bump `updated`, or record that it is still current. |
| 3 | stale | semantic | core-architecture.md | `updated` 2026-10-10; S022 last ingested 2026-10-13. | Re-check against S022 (batch 1 touched F005 refactor shape). |
| 4 | stale | semantic | desktop-focus-lifecycle.md | `updated` 2026-10-10; S022 last ingested 2026-10-13. | Re-check against S022 (F5-R10 large live gap). |
| 5 | stale | semantic | deterministic-production.md | `updated` 2026-09-29; S018 last ingested 2026-10-10. | Re-check against S018. |
| 6 | stale | semantic | f003-art-pipeline.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 7 | stale | semantic | f003-ui-ux-final-review.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 8 | stale | semantic | f005-timeline-mapping-and-auto-delivery.md | `updated` 2026-10-10; S022 last ingested 2026-10-13. | Re-check against S022; planned page. |
| 9 | stale | semantic | factory-run-bounds-and-offline-segmentation.md | `updated` 2026-10-10; S022 last ingested 2026-10-13 (S027 measurement also cited). | Re-check against S022. |
| 10 | stale | semantic | factory-run-simulation.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018. |
| 11 | stale | semantic | feature-001-retirement.md | `updated` 2026-10-08; S018 last ingested 2026-10-10. | Re-check against S018. |
| 12 | stale | semantic | frame-rate-target.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 13 | stale | semantic | gameplay-content-validation.md | `updated` 2026-10-07; S018 last ingested 2026-10-10. | Re-check against S018. |
| 14 | stale | semantic | local-checkpoints-and-offline-return.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 15 | stale | semantic | mobile-performance-targets.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 16 | stale | semantic | player-level-test-gaps.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 17 | stale | semantic | run-boosts-and-progression.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018. |
| 18 | stale | semantic | run-bootstrap-and-lifecycle.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 19 | stale | semantic | run-cold-start-and-v1-import.md | `updated` 2026-10-08; S018 last ingested 2026-10-10. | Re-check against S018. |
| 20 | stale | semantic | run-cold-start-recovery.md | `updated` 2026-10-08; S018 last ingested 2026-10-10. | Re-check against S018. |
| 21 | stale | semantic | run-commands-and-snapshots.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018. |
| 22 | stale | semantic | run-content-dispatch.md | `updated` 2026-10-07; S018 last ingested 2026-10-10. | Re-check against S018. |
| 23 | stale | semantic | run-contracts.md | `updated` 2026-10-10; S022 last ingested 2026-10-13. | Re-check against S022 (contract Cash ratio decision D-D). |
| 24 | stale | semantic | run-economy-and-prestige.md | `updated` 2026-10-10; S022 last ingested 2026-10-13. | Re-check against S022. |
| 25 | stale | semantic | run-panel-conventions.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 26 | stale | semantic | run-panels.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 27 | stale | semantic | run-prestige-transition.md | `updated` 2026-10-01; S018 last ingested 2026-10-10. | Re-check against S018. |
| 28 | stale | semantic | run-session.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018. |
| 29 | stale | semantic | run-shell-and-panels.md | `updated` 2026-10-08; S018 last ingested 2026-10-10. | Re-check against S018. |
| 30 | stale | semantic | run-shell-device-profile-and-text-scale.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 31 | stale | semantic | run-shell-notices-and-gate-fixes.md | `updated` 2026-10-07; S018 last ingested 2026-10-10. | Re-check against S018. |
| 32 | stale | semantic | run-state-domain.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018. |
| 33 | stale | semantic | run-unlocks.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018. |
| 34 | stale | semantic | save-v2-and-v1-import.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 35 | stale | semantic | save-v2-document.md | `updated` 2026-10-10; S022 last ingested 2026-10-13 (Save V3 decision D-G). | Re-check against S022. |
| 36 | stale | semantic | ui-toolkit-hud-layout-pitfalls.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. Also has the open conflict (row 1). | Re-check against S018 when resolving row 1. |
| 37 | stale | semantic | unity-player-validation.md | `updated` 2026-10-08; S018 last ingested 2026-10-10. | Re-check against S018. |
| 38 | stale | semantic | unity-presentation-boundary.md | `updated` 2026-10-08; S018 last ingested 2026-10-10. | Re-check against S018. |
| 39 | stale | semantic | unity-serialized-asset-edits.md | `updated` 2026-10-07; S018 last ingested 2026-10-10. | Re-check against S018. |
| 40 | citations | semantic | f005-plan-overview.md (lines 14–22) | Decision bullets D-A…D-L carry no source ID (carried over from 2026-10-10). | Add `(S022)` or the recording source. Lint does not edit content. |
| 41 | citations | semantic | unity-test-and-maqa-readiness.md (lines 28–33) | Lessons-learned bullets carry no source ID (carried over). | Add the source (likely S017, S019 or S022) to each bullet. |
| 42 | citations | semantic | f005-session-log-and-devtools.md (lines 14–24) | Design bullets (session log writer, export, DevTools assembly, save root) carry no source ID. | Add `(S022)` to each bullet. |
| 43 | citations | semantic | gameplay-content-v2.md (lines 35–39) | Validator rule list has no source ID; may be a continuation of a cited bullet. | Check the parent bullet; add `(S018)` if it is a separate claim. |
| 44 | size | structural | android-emulator-validation.md | About 1041 words (`wc -w`, including frontmatter), over the 600-word cap. | Split by topic (build, rotation, noise) with cross-links. Not split by lint. |
| 45 | size | structural | player-level-test-gaps.md | About 863 words. | Split the seam list from the case studies. |
| 46 | size | structural | run-persistence.md | About 762 words (was 675 on 2026-10-10). | Split the device-set pitfalls or cold start part into a companion page. |
| 47 | size | structural | unity-test-and-maqa-readiness.md | About 709 words. | Split the MAQA readiness flow from the test and lessons sections. |
| 48 | size | structural | w003-player-timing-baseline.md | About 700 words. | Split the PE-4 samples and caveats into a companion page. |
| 49 | size | structural | factory-run-bounds-and-offline-segmentation.md | About 696 words. | Split W003-B measurement and Option 1 decision into a companion page. |
| 50 | size | structural | f005-plan-overview.md | About 682 words (was 612 on 2026-10-10). | Split the batch status or review-wave section. |
| 51 | size | structural | feature-acceptance-evidence.md | About 642 words. | Split the reconciliation rules from the gap list. |
| 52 | size | structural | unity-player-validation.md | About 624 words. | Optional split; small margin. |
| 53 | size | structural | mobile-performance-targets.md | About 622 words. | Optional split; small margin. |
| 54 | size | structural | run-economy-and-prestige.md | About 617 words. | Optional split; small margin. |
| 55 | size | structural | gameplay-content-v2.md | About 605 words. | Optional split; just over the cap. |

Counts: contradictions 1 (row 1). citations 4 (rows 40–43). stale 38 (rows 2–39, all semantic). size 12 (rows 44–55). index-drift 0. links 0. orphans 0. unknown S-ids 0. frontmatter 0.

No page prose, claims, conflict markers, `sources.md` history, or `INDEX.md` lines were modified by this pass. Only this file was written.

Note for the owner: `INDEX.md` describes `f005-content-v3-transitional.md` as "F005 content schema v3 and transitional adapter", while the page title is "F005 content schema v3 and the transitional reward adapter". Descriptive drift only, not a mechanical finding; left as is.

Next step: resolve row 1 (owner decision). Then re-check the 38 stale pages (rows 2–39) against their re-ingested sources; most are S018 re-checks, starting with the `run-*` component pages.
