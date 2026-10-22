# Wiki Lint Report — 2026-10-22

Scope: full pass after the F005 batch C closeout ingest (commits 1961764 and 93ff2bd, branch `docs/005-batchC-closeout`; batch-C pages checked first). Config: `lint.stale_after_days` 90, `lint.auto_fix` index-and-links, `ingest.max_pages_per_ingest` 12, `require_citations` true. 73 pages in `pages/`, 33 sources (S001–S033).

Mechanical checks: `index-drift` 0 (INDEX lists exactly the 73 pages; no dead INDEX lines; no duplicates). `links` 1 (`android-emulator-profiling.md` line 26, `(BLAST)` literal; not auto-fixed, prose). Unknown S-ids 0. `orphans` 1 (`f005-batch-c-ui-and-devtools-fixes.md`, INDEX does not count). Frontmatter present on all pages. `stale` by age over 90 days: 0. Fixes applied: none; INDEX.md was already consistent and no page link targets were missing.

Batch C pages checked (`f005-batch-c-content-revision-3`, `f005-batch-c-playtest-and-decisions`, `f005-batch-c-policy-r2-and-gates`, `f005-batch-c-ui-and-devtools-fixes`) and the batch-C edits to `f005-batch-b-performance-and-measurements`: one contradiction pair (row 1) and one citation finding (row 3). Owner decision C-F2 wording in `f005-batch-c-playtest-and-decisions` matches the 93ff2bd decision and no other page still calls it an F005 task.

Semantic checks: `contradictions` 2 (rows 1–2). `stale`: source re-ingested after page `updated` (rows 6–57, one row per page-source pair). `citations`: bullet lines without an S-id, one row per page (rows 3 and 58–71). The citation check is a line-level heuristic; verify each row against the page before editing.

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | contradictions | semantic | f005-batch-c-content-revision-3.md (line 23) vs f005-batch-c-policy-r2-and-gates.md (line 18) and f005-batch-c-playtest-and-decisions.md (line 22) | Revision 3 says the validator gained a rule "bonus ratio above 1.5", but the gate accepts a bonus ratio 1.0 to 2.0, the playtest page records "bonus bound 1.0 to 2.0", and revision 3 tunes bonus at 1.8 (line 15). | Parent or owner checks the validator source to see whether the rule caps standing offers (1.5) or bonus offers; the wrong page then gets corrected by hand. Lint does not resolve it. |
| 2 | contradictions | semantic | ui-toolkit-hud-layout-pitfalls.md (line 20) | Open `> ⚠ conflict:` marker: S001 (sw411dp about 1.68 px/pt) vs S018 (T191, 2.09 px/pt portrait, 1.69 px/pt landscape). Carried over from earlier passes. | Owner decision between S001 and S018; lint does not resolve it. |
| 3 | citations | semantic | f005-batch-c-ui-and-devtools-fixes.md (line 14) | Lesson bullet ("never let a valid value ... double as the unset marker") has no S-id; the section's other bullets cite S033 or S022. | Add the source ID the lesson derives from, or mark it as author synthesis. |
| 4 | links | mechanical | android-emulator-profiling.md (line 26) | Link-shaped literal `SurfaceView[…](BLAST)` targets nonexistent `BLAST`. | Not auto-fixed: removing the link wrapper changes the prose. Author to rewrite as plain text. |
| 5 | orphans | structural | f005-batch-c-ui-and-devtools-fixes.md | No page links to it (only INDEX). | Add a Related link from f005-batch-b-ui-and-android.md or f005-batch-c-playtest-and-decisions.md. |
| 6 | stale | semantic | android-runtime-rotation.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018; bump `updated` or record that it is still current. |
| 7 | stale | semantic | android-runtime-rotation.md | `updated` 2026-10-09; S019 last ingested 2026-10-10. | Re-check against S019. |
| 8 | stale | semantic | core-architecture.md | `updated` 2026-10-10; S022 last ingested 2026-10-22. | Re-check against S022. |
| 9 | stale | semantic | desktop-focus-lifecycle.md | `updated` 2026-10-10; S022 last ingested 2026-10-22. | Re-check against S022. |
| 10 | stale | semantic | deterministic-production.md | `updated` 2026-09-29; S018 last ingested 2026-10-10. | Re-check against S018. |
| 11 | stale | semantic | f003-art-pipeline.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 12 | stale | semantic | f003-ui-ux-final-review.md | `updated` 2026-10-09; S018 and S019 last ingested 2026-10-10. | Re-check against S018 and S019. |
| 13 | stale | semantic | f005-batch-1-workflow-pitfalls.md | `updated` 2026-10-13; S022 last ingested 2026-10-22. | Re-check against S022. |
| 14 | stale | semantic | f005-batch-a-economy-and-contracts.md | `updated` 2026-10-14; S022 last ingested 2026-10-22. | Re-check against S022. |
| 15 | stale | semantic | f005-batch-b-auto-deliver-and-production.md | `updated` 2026-10-21; S022 last ingested 2026-10-22. | Re-check against S022. |
| 16 | stale | semantic | f005-batch-b-live-gap.md | `updated` 2026-10-21; S022 last ingested 2026-10-22. | Re-check against S022. |
| 17 | stale | semantic | f005-batch-b-save-v3-drift.md | `updated` 2026-10-21; S022 last ingested 2026-10-22. | Re-check against S022. |
| 18 | stale | semantic | f005-batch-b-ui-and-android.md | `updated` 2026-10-21; S022 last ingested 2026-10-22. | Re-check against S022. |
| 19 | stale | semantic | f005-content-v3-transitional.md | `updated` 2026-10-14; S022 last ingested 2026-10-22. | Re-check against S022. |
| 20 | stale | semantic | f005-plan-overview.md | `updated` 2026-10-21; S022 last ingested 2026-10-22. | Re-check against S022. |
| 21 | stale | semantic | f005-session-log-and-devtools.md | `updated` 2026-10-21; S022 last ingested 2026-10-22. | Re-check against S022. |
| 22 | stale | semantic | f005-session-log-writer.md | `updated` 2026-10-21; S022 last ingested 2026-10-22. | Re-check against S022. |
| 23 | stale | semantic | f005-timeline-mapping-and-auto-delivery.md | `updated` 2026-10-10; S022 last ingested 2026-10-22. | Re-check against S022. |
| 24 | stale | semantic | factory-run-bounds-and-offline-segmentation.md | `updated` 2026-10-14; S022 last ingested 2026-10-22 and S027 last ingested 2026-10-21. | Re-check against S022 and S027. |
| 25 | stale | semantic | factory-run-simulation.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018. |
| 26 | stale | semantic | feature-001-retirement.md | `updated` 2026-10-08; S018 and S019 last ingested 2026-10-10. | Re-check against S018 and S019. |
| 27 | stale | semantic | frame-rate-target.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 28 | stale | semantic | gameplay-content-validation.md | `updated` 2026-10-07; S018 last ingested 2026-10-10. | Re-check against S018. |
| 29 | stale | semantic | local-checkpoints-and-offline-return.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 30 | stale | semantic | mobile-performance-targets.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 31 | stale | semantic | player-level-test-gaps.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 32 | stale | semantic | run-balance-policy-and-f005-matrix.md | `updated` 2026-10-14; S022 last ingested 2026-10-22. | Re-check against S022. |
| 33 | stale | semantic | run-boosts-and-progression.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018. |
| 34 | stale | semantic | run-bootstrap-and-lifecycle.md | `updated` 2026-10-09; S018 and S019 last ingested 2026-10-10. | Re-check against S018 and S019. |
| 35 | stale | semantic | run-cold-start-and-v1-import.md | `updated` 2026-10-08; S018 last ingested 2026-10-10. | Re-check against S018. |
| 36 | stale | semantic | run-cold-start-recovery.md | `updated` 2026-10-08; S018 and S019 last ingested 2026-10-10. | Re-check against S018 and S019. |
| 37 | stale | semantic | run-commands-and-snapshots.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018. |
| 38 | stale | semantic | run-content-dispatch.md | `updated` 2026-10-07; S018 and S019 last ingested 2026-10-10. | Re-check against S018 and S019. |
| 39 | stale | semantic | run-contracts.md | `updated` 2026-10-14; S022 last ingested 2026-10-22. | Re-check against S022. |
| 40 | stale | semantic | run-economy-and-prestige.md | `updated` 2026-10-14; S022 last ingested 2026-10-22. | Re-check against S022. |
| 41 | stale | semantic | run-panel-conventions.md | `updated` 2026-10-09; S018 and S019 last ingested 2026-10-10. | Re-check against S018 and S019. |
| 42 | stale | semantic | run-panels.md | `updated` 2026-10-14; S022 last ingested 2026-10-22. | Re-check against S022. |
| 43 | stale | semantic | run-persistence.md | `updated` 2026-10-13; S022 last ingested 2026-10-22. | Re-check against S022. |
| 44 | stale | semantic | run-prestige-transition.md | `updated` 2026-10-01; S018 last ingested 2026-10-10. | Re-check against S018. |
| 45 | stale | semantic | run-session.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018. |
| 46 | stale | semantic | run-shell-and-panels.md | `updated` 2026-10-08; S018 and S019 last ingested 2026-10-10. | Re-check against S018 and S019. |
| 47 | stale | semantic | run-shell-device-profile-and-text-scale.md | `updated` 2026-10-09; S018 and S019 last ingested 2026-10-10. | Re-check against S018 and S019. |
| 48 | stale | semantic | run-shell-notices-and-gate-fixes.md | `updated` 2026-10-07; S018 and S019 last ingested 2026-10-10. | Re-check against S018 and S019. |
| 49 | stale | semantic | run-state-domain.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018. |
| 50 | stale | semantic | run-unlocks.md | `updated` 2026-09-30; S018 last ingested 2026-10-10. | Re-check against S018. |
| 51 | stale | semantic | save-v2-and-v1-import.md | `updated` 2026-10-09; S018 last ingested 2026-10-10. | Re-check against S018. |
| 52 | stale | semantic | save-v2-document.md | `updated` 2026-10-10; S022 last ingested 2026-10-22. | Re-check against S022. |
| 53 | stale | semantic | ui-toolkit-hud-layout-pitfalls.md | `updated` 2026-10-09; S018 and S019 last ingested 2026-10-10. | Re-check against S018 and S019. |
| 54 | stale | semantic | unity-player-validation.md | `updated` 2026-10-08; S018 last ingested 2026-10-10. | Re-check against S018. |
| 55 | stale | semantic | unity-presentation-boundary.md | `updated` 2026-10-08; S018 last ingested 2026-10-10. | Re-check against S018. |
| 56 | stale | semantic | unity-serialized-asset-edits.md | `updated` 2026-10-07; S018 and S019 last ingested 2026-10-10. | Re-check against S018 and S019. |
| 57 | stale | semantic | unity-test-and-maqa-readiness.md | `updated` 2026-10-13; S022 last ingested 2026-10-22. | Re-check against S022. |
| 58 | citations | semantic | android-emulator-profiling.md | Bullet lines 16–20 and 40–42 without an S-id. | Add (Sxxx) to each synthesized bullet during next review. |
| 59 | citations | semantic | android-emulator-validation.md | Bullet lines 20, 21, 27, 28, 35, 36, 43–46, 65 without an S-id. | Same. |
| 60 | citations | semantic | android-runtime-rotation.md | Bullet line 18 without an S-id. | Same. |
| 61 | citations | semantic | f005-plan-overview.md | Bullet lines 14–22, 27–33 without an S-id. | Same. |
| 62 | citations | semantic | f005-session-log-and-devtools.md | Bullet lines 14–17, 22–24, 34 without an S-id. | Same. |
| 63 | citations | semantic | f005-timeline-mapping-and-auto-delivery.md | Bullet lines 18, 19, 24–28 without an S-id. | Same. |
| 64 | citations | semantic | factory-run-simulation.md | Bullet lines 16–18, 22–26, 36–38 without an S-id. | Same. |
| 65 | citations | semantic | gameplay-content-v2.md | Bullet lines 35–39 without an S-id. | Same. |
| 66 | citations | semantic | json-content-migration-evidence.md | Bullet lines 16–22 without an S-id. | Same. |
| 67 | citations | semantic | run-bootstrap-and-lifecycle.md | Bullet lines 14–16 without an S-id. | Same. |
| 68 | citations | semantic | run-contracts.md | Bullet lines 30–33 without an S-id. | Same. |
| 69 | citations | semantic | run-economy-and-prestige.md | Bullet lines 22–25, 50–54 without an S-id. | Same. |
| 70 | citations | semantic | save-v2-and-v1-import.md | Bullet lines 33–40 without an S-id. | Same. |
| 71 | citations | semantic | unity-test-and-maqa-readiness.md | Bullet lines 28–33 without an S-id. | Same. |

## Notes (not lint checks; no rows)

- Oversize (SCHEMA.md: pages over 600 body words should be split): android-emulator-validation.md (~1041), player-level-test-gaps.md (~863), f005-plan-overview.md (~821), run-persistence.md (~762), factory-run-bounds-and-offline-segmentation.md (~770), unity-test-and-maqa-readiness.md (~709), w003-player-timing-baseline.md (~700), feature-acceptance-evidence.md (~642), run-economy-and-prestige.md (~640), mobile-performance-targets.md (~622), run-panels.md (~632), unity-player-validation.md (~624), gameplay-content-v2.md (~605). Split is an author decision. None of the batch-C pages exceed the limit.
- Stale rows are flagged by the registry rule (source last ingested after page `updated`). `sources.md` "pages touched" lists are cumulative, so some flagged pages may already be current; the parent should confirm per page rather than re-ingest blindly. S018 (10-10) and S022 (10-22) account for most rows.
- Next step: resolve the bonus-ratio contradiction (row 1) by checking the validator source, since it decides which wiki page is wrong; then the S001 vs S018 conflict on `ui-toolkit-hud-layout-pitfalls.md` (row 2) by owner decision.
