# Wiki Lint Report — 2026-10-14

Scope: full pass after the F005 batch A ingest (commit 724a0d5, branch `docs/005-batchA-closeout`). Config: `lint.stale_after_days` 90, `lint.auto_fix` index-and-links, `ingest.max_pages_per_ingest` 12, `require_citations` true. 62 pages in `pages/`, 28 sources (S001–S028).

Mechanical checks: `index-drift` 0 (INDEX lists exactly the 62 pages; no dead INDEX lines; no duplicates). `links` 1 (`android-emulator-profiling.md` `(BLAST)` literal, see row 2). `citations` unknown S-ids 0. `orphans` 0. Frontmatter (`title`, `type`, `sources`, `updated`) present on all pages with valid types. `stale` age >90 days: 0. Fixes applied: none; INDEX.md was already consistent, so nothing was regenerated and no link targets were changed.

Semantic checks: `contradictions` 1 open conflict marker (row 1; shared-source/link pairs checked for markers only, no full pairwise claim comparison at Low tier). `stale`: source re-ingested after page `updated` (rows 3+). `citations`: bullet lines without an S-id on the listed pages. `oversize`: pages over 600 body words.

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | contradictions | semantic | ui-toolkit-hud-layout-pitfalls.md (line 20) | Open `> ⚠ conflict:` marker: S001 (sw411dp about 1.68 px/pt) vs S018 (T191, 2.09 px/pt portrait, 1.69 landscape). Carried over from 2026-10-10 and 2026-10-13 passes. | Owner decision (S001 vs S018). Lint does not resolve it. |
| 2 | links | mechanical | android-emulator-profiling.md (line 26) | Link-shaped literal `SurfaceView[…](BLAST)` targets nonexistent `BLAST`. Not auto-fixed: removing the link syntax would alter prose. | Author to change to plain text, e.g. `SurfaceView` layer named BLAST (prose edit). |
| 3 | citations | semantic | android-emulator-profiling.md | 2 bullet line(s) without a source ID (ingest.require_citations true). Pages with sources in frontmatter; inline citation missing. | Add (Sxxx) to each synthesized bullet during next ingest/review; verify against cited source. |
| 4 | citations | semantic | android-emulator-validation.md | 8 bullet line(s) without a source ID (ingest.require_citations true). Pages with sources in frontmatter; inline citation missing. | Add (Sxxx) to each synthesized bullet during next ingest/review; verify against cited source. |
| 5 | stale | semantic | android-runtime-rotation.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 6 | stale | semantic | android-runtime-rotation.md | `updated` 2026-10-09; S019 last ingested 2026-10-10. | Re-check against S019; bump `updated` or record that it is still current. |
| 7 | citations | semantic | android-runtime-rotation.md | 1 bullet line(s) without a source ID (ingest.require_citations true). Pages with sources in frontmatter; inline citation missing. | Add (Sxxx) to each synthesized bullet during next ingest/review; verify against cited source. |
| 8 | stale | semantic | core-architecture.md | `updated` 2026-10-10; S022 last ingested 2026-10-14. | Re-check against S022; bump `updated` or record that it is still current. |
| 9 | stale | semantic | desktop-focus-lifecycle.md | `updated` 2026-10-10; S022 last ingested 2026-10-14. | Re-check against S022; bump `updated` or record that it is still current. |
| 10 | stale | semantic | deterministic-production.md | `updated` 2026-09-29; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 11 | stale | semantic | f003-art-pipeline.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 12 | stale | semantic | f003-ui-ux-final-review.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 13 | stale | semantic | f003-ui-ux-final-review.md | `updated` 2026-10-09; S019 last ingested 2026-10-10. | Re-check against S019; bump `updated` or record that it is still current. |
| 14 | stale | semantic | f005-batch-1-workflow-pitfalls.md | `updated` 2026-10-13; S022 last ingested 2026-10-14. | Re-check against S022; bump `updated` or record that it is still current. |
| 15 | citations | semantic | f005-plan-overview.md | 16 bullet line(s) without a source ID (ingest.require_citations true). Pages with sources in frontmatter; inline citation missing. | Add (Sxxx) to each synthesized bullet during next ingest/review; verify against cited source. |
| 16 | stale | semantic | f005-session-log-and-devtools.md | `updated` 2026-10-13; S022 last ingested 2026-10-14. | Re-check against S022; bump `updated` or record that it is still current. |
| 17 | citations | semantic | f005-session-log-and-devtools.md | 8 bullet line(s) without a source ID (ingest.require_citations true). Pages with sources in frontmatter; inline citation missing. | Add (Sxxx) to each synthesized bullet during next ingest/review; verify against cited source. |
| 18 | stale | semantic | f005-timeline-mapping-and-auto-delivery.md | `updated` 2026-10-10; S022 last ingested 2026-10-14. | Re-check against S022; bump `updated` or record that it is still current. |
| 19 | citations | semantic | f005-timeline-mapping-and-auto-delivery.md | 5 bullet line(s) without a source ID (ingest.require_citations true). Pages with sources in frontmatter; inline citation missing. | Add (Sxxx) to each synthesized bullet during next ingest/review; verify against cited source. |
| 20 | stale | semantic | factory-run-simulation.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 21 | citations | semantic | factory-run-simulation.md | 3 bullet line(s) without a source ID (ingest.require_citations true). Pages with sources in frontmatter; inline citation missing. | Add (Sxxx) to each synthesized bullet during next ingest/review; verify against cited source. |
| 22 | stale | semantic | feature-001-retirement.md | `updated` 2026-10-08; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 23 | stale | semantic | feature-001-retirement.md | `updated` 2026-10-08; S019 last ingested 2026-10-10. | Re-check against S019; bump `updated` or record that it is still current. |
| 24 | stale | semantic | frame-rate-target.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 25 | citations | semantic | gameplay-content-v2.md | 1 bullet line(s) without a source ID (ingest.require_citations true). Pages with sources in frontmatter; inline citation missing. | Add (Sxxx) to each synthesized bullet during next ingest/review; verify against cited source. |
| 26 | stale | semantic | gameplay-content-validation.md | `updated` 2026-10-07; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 27 | stale | semantic | local-checkpoints-and-offline-return.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 28 | stale | semantic | mobile-performance-targets.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 29 | stale | semantic | player-level-test-gaps.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 30 | stale | semantic | run-boosts-and-progression.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 31 | stale | semantic | run-bootstrap-and-lifecycle.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 32 | stale | semantic | run-bootstrap-and-lifecycle.md | `updated` 2026-10-09; S019 last ingested 2026-10-10. | Re-check against S019; bump `updated` or record that it is still current. |
| 33 | stale | semantic | run-cold-start-and-v1-import.md | `updated` 2026-10-08; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 34 | stale | semantic | run-cold-start-recovery.md | `updated` 2026-10-08; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 35 | stale | semantic | run-cold-start-recovery.md | `updated` 2026-10-08; S019 last ingested 2026-10-10. | Re-check against S019; bump `updated` or record that it is still current. |
| 36 | stale | semantic | run-commands-and-snapshots.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 37 | stale | semantic | run-content-dispatch.md | `updated` 2026-10-07; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 38 | stale | semantic | run-content-dispatch.md | `updated` 2026-10-07; S019 last ingested 2026-10-10. | Re-check against S019; bump `updated` or record that it is still current. |
| 39 | citations | semantic | run-contracts.md | 4 bullet line(s) without a source ID (ingest.require_citations true). Pages with sources in frontmatter; inline citation missing. | Add (Sxxx) to each synthesized bullet during next ingest/review; verify against cited source. |
| 40 | citations | semantic | run-economy-and-prestige.md | 6 bullet line(s) without a source ID (ingest.require_citations true). Pages with sources in frontmatter; inline citation missing. | Add (Sxxx) to each synthesized bullet during next ingest/review; verify against cited source. |
| 41 | stale | semantic | run-panel-conventions.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 42 | stale | semantic | run-panel-conventions.md | `updated` 2026-10-09; S019 last ingested 2026-10-10. | Re-check against S019; bump `updated` or record that it is still current. |
| 43 | stale | semantic | run-persistence.md | `updated` 2026-10-13; S022 last ingested 2026-10-14. | Re-check against S022; bump `updated` or record that it is still current. |
| 44 | stale | semantic | run-prestige-transition.md | `updated` 2026-10-01; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 45 | stale | semantic | run-session.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 46 | stale | semantic | run-shell-and-panels.md | `updated` 2026-10-08; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 47 | stale | semantic | run-shell-and-panels.md | `updated` 2026-10-08; S019 last ingested 2026-10-10. | Re-check against S019; bump `updated` or record that it is still current. |
| 48 | stale | semantic | run-shell-device-profile-and-text-scale.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 49 | stale | semantic | run-shell-notices-and-gate-fixes.md | `updated` 2026-10-07; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 50 | stale | semantic | run-shell-notices-and-gate-fixes.md | `updated` 2026-10-07; S019 last ingested 2026-10-10. | Re-check against S019; bump `updated` or record that it is still current. |
| 51 | stale | semantic | run-state-domain.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 52 | stale | semantic | run-unlocks.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 53 | stale | semantic | save-v2-and-v1-import.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 54 | stale | semantic | save-v2-document.md | `updated` 2026-10-10; S022 last ingested 2026-10-14. | Re-check against S022; bump `updated` or record that it is still current. |
| 55 | stale | semantic | ui-toolkit-hud-layout-pitfalls.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 56 | stale | semantic | ui-toolkit-hud-layout-pitfalls.md | `updated` 2026-10-09; S019 last ingested 2026-10-10. | Re-check against S019; bump `updated` or record that it is still current. |
| 57 | stale | semantic | unity-player-validation.md | `updated` 2026-10-08; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 58 | stale | semantic | unity-presentation-boundary.md | `updated` 2026-10-08; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 59 | stale | semantic | unity-serialized-asset-edits.md | `updated` 2026-10-07; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 60 | stale | semantic | unity-serialized-asset-edits.md | `updated` 2026-10-07; S019 last ingested 2026-10-10. | Re-check against S019; bump `updated` or record that it is still current. |
| 61 | stale | semantic | unity-test-and-maqa-readiness.md | `updated` 2026-10-13; S022 last ingested 2026-10-14. | Re-check against S022; bump `updated` or record that it is still current. |
| 62 | citations | semantic | unity-test-and-maqa-readiness.md | 6 bullet line(s) without a source ID (ingest.require_citations true). Pages with sources in frontmatter; inline citation missing. | Add (Sxxx) to each synthesized bullet during next ingest/review; verify against cited source. |
| 63 | oversize | semantic | android-emulator-validation.md | About 1027 body words, over the 600-word split threshold. | Split into two linked pages per SCHEMA.md (author decision). |
| 64 | oversize | semantic | f005-plan-overview.md | About 743 body words, over the 600-word split threshold. | Split into two linked pages per SCHEMA.md (author decision). |
| 65 | oversize | semantic | factory-run-bounds-and-offline-segmentation.md | About 753 body words, over the 600-word split threshold. | Split into two linked pages per SCHEMA.md (author decision). |
| 66 | oversize | semantic | feature-acceptance-evidence.md | About 629 body words, over the 600-word split threshold. | Split into two linked pages per SCHEMA.md (author decision). |
| 67 | oversize | semantic | mobile-performance-targets.md | About 609 body words, over the 600-word split threshold. | Split into two linked pages per SCHEMA.md (author decision). |
| 68 | oversize | semantic | player-level-test-gaps.md | About 849 body words, over the 600-word split threshold. | Split into two linked pages per SCHEMA.md (author decision). |
| 69 | oversize | semantic | run-economy-and-prestige.md | About 626 body words, over the 600-word split threshold. | Split into two linked pages per SCHEMA.md (author decision). |
| 70 | oversize | semantic | run-panels.md | About 617 body words, over the 600-word split threshold. | Split into two linked pages per SCHEMA.md (author decision). |
| 71 | oversize | semantic | run-persistence.md | About 750 body words, over the 600-word split threshold. | Split into two linked pages per SCHEMA.md (author decision). |
| 72 | oversize | semantic | unity-player-validation.md | About 604 body words, over the 600-word split threshold. | Split into two linked pages per SCHEMA.md (author decision). |
| 73 | oversize | semantic | unity-test-and-maqa-readiness.md | About 689 body words, over the 600-word split threshold. | Split into two linked pages per SCHEMA.md (author decision). |
| 74 | oversize | semantic | w003-player-timing-baseline.md | About 687 body words, over the 600-word split threshold. | Split into two linked pages per SCHEMA.md (author decision). |
