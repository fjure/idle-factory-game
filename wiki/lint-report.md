# Wiki Lint Report — 2026-11-05

Scope: full pass after batch 12 ingest S055 (commit 11abaaa), branch feat-004-maqa-batch12. Config: stale_after_days 90, auto_fix index-and-links.

Mechanical checks clean: index-drift 0 (109 pages, 109 index entries), links 0 broken, orphans 0, unknown citation IDs 0, frontmatter 0 issues, stale-by-age 0. Mechanical fixes applied: 0 (none needed; INDEX.md and links untouched).

| # | Check | Severity | Page | Finding | Suggested fix |
|---|-------|----------|------|---------|---------------|
| 1 | contradictions | semantic | ui-toolkit-hud-layout-pitfalls.md | line 20: unresolved `> ⚠ conflict:` marker, S001 (1.68 px/pt in both orientations) vs S018 (2.09 px/pt portrait, 1.69 landscape at 1080x2400) | owner/parent resolves by re-checking the ScaleWithScreenSize math on the real device profile, then keeps the verified claim and drops the marker; lint does not resolve it |
| 2 | stale | semantic | android-emulator-validation.md | page updated 2026-10-27; S045 re-ingested 2026-10-28 | re-read S045 source and confirm page claims; bump `updated` if still accurate |
| 3 | stale | semantic | android-runtime-rotation.md | page updated 2026-10-09; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 4 | stale | semantic | android-runtime-rotation.md | page updated 2026-10-09; S019 re-ingested 2026-10-10 | re-read S019 source and confirm page claims; bump `updated` if still accurate |
| 5 | stale | semantic | core-architecture.md | page updated 2026-10-27; S038 re-ingested 2026-11-05 | re-read S038 source and confirm page claims; bump `updated` if still accurate |
| 6 | stale | semantic | desktop-focus-lifecycle.md | page updated 2026-10-10; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 7 | stale | semantic | deterministic-production.md | page updated 2026-09-29; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 8 | stale | semantic | f003-ui-ux-final-review.md | page updated 2026-10-09; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 9 | stale | semantic | f003-ui-ux-final-review.md | page updated 2026-10-09; S019 re-ingested 2026-10-10 | re-read S019 source and confirm page claims; bump `updated` if still accurate |
| 10 | stale | semantic | f004-batch-1-art-tooling.md | page updated 2026-10-27; S038 re-ingested 2026-11-05 | re-read S038 source and confirm page claims; bump `updated` if still accurate |
| 11 | stale | semantic | f004-batch-6-workflow-pitfalls.md | page updated 2026-10-29; S038 re-ingested 2026-11-05 | re-read S038 source and confirm page claims; bump `updated` if still accurate |
| 12 | stale | semantic | f005-batch-1-workflow-pitfalls.md | page updated 2026-10-13; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 13 | stale | semantic | f005-batch-a-economy-and-contracts.md | page updated 2026-10-14; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 14 | stale | semantic | f005-batch-a-open-decisions.md | page updated 2026-10-22; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 15 | stale | semantic | f005-batch-b-auto-deliver-and-production.md | page updated 2026-10-21; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 16 | stale | semantic | f005-batch-b-live-gap.md | page updated 2026-10-21; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 17 | stale | semantic | f005-batch-b-save-v3-drift.md | page updated 2026-10-21; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 18 | stale | semantic | f005-batch-b-save-v3-drift.md | page updated 2026-10-21; S029 re-ingested 2026-10-27 | re-read S029 source and confirm page claims; bump `updated` if still accurate |
| 19 | stale | semantic | f005-batch-b-ui-and-android.md | page updated 2026-10-21; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 20 | stale | semantic | f005-batch-b-workflow-pitfalls.md | page updated 2026-10-22; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 21 | stale | semantic | f005-batch-c-content-revision-3.md | page updated 2026-10-22; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 22 | stale | semantic | f005-batch-c-content-revision-3.md | page updated 2026-10-22; S029 re-ingested 2026-10-27 | re-read S029 source and confirm page claims; bump `updated` if still accurate |
| 23 | stale | semantic | f005-batch-c-playtest-and-decisions.md | page updated 2026-10-22; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 24 | stale | semantic | f005-batch-c-policy-r2-and-gates.md | page updated 2026-10-22; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 25 | stale | semantic | f005-batch-c-ui-and-devtools-fixes.md | page updated 2026-10-22; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 26 | stale | semantic | f005-content-v3-transitional.md | page updated 2026-10-14; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 27 | stale | semantic | f005-plan-overview.md | page updated 2026-10-21; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 28 | stale | semantic | f005-session-log-and-devtools.md | page updated 2026-10-21; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 29 | stale | semantic | f005-session-log-writer.md | page updated 2026-10-21; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 30 | stale | semantic | f005-timeline-mapping-and-auto-delivery.md | page updated 2026-10-10; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 31 | stale | semantic | factory-run-bounds-and-offline-segmentation.md | page updated 2026-10-14; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 32 | stale | semantic | factory-run-bounds-and-offline-segmentation.md | page updated 2026-10-14; S027 re-ingested 2026-10-21 | re-read S027 source and confirm page claims; bump `updated` if still accurate |
| 33 | stale | semantic | factory-run-simulation.md | page updated 2026-09-30; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 34 | stale | semantic | feature-001-retirement.md | page updated 2026-10-08; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 35 | stale | semantic | feature-001-retirement.md | page updated 2026-10-08; S019 re-ingested 2026-10-10 | re-read S019 source and confirm page claims; bump `updated` if still accurate |
| 36 | stale | semantic | frame-rate-target.md | page updated 2026-10-09; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 37 | stale | semantic | gameplay-content-v2.md | page updated 2026-10-13; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 38 | stale | semantic | gameplay-content-validation.md | page updated 2026-10-07; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 39 | stale | semantic | local-checkpoints-and-offline-return.md | page updated 2026-10-09; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 40 | stale | semantic | run-balance-policy-and-f005-matrix.md | page updated 2026-10-22; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 41 | stale | semantic | run-boosts-and-progression.md | page updated 2026-09-30; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 42 | stale | semantic | run-bootstrap-and-lifecycle.md | page updated 2026-10-09; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 43 | stale | semantic | run-bootstrap-and-lifecycle.md | page updated 2026-10-09; S019 re-ingested 2026-10-10 | re-read S019 source and confirm page claims; bump `updated` if still accurate |
| 44 | stale | semantic | run-cold-start-and-v1-import.md | page updated 2026-10-08; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 45 | stale | semantic | run-cold-start-recovery.md | page updated 2026-10-08; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 46 | stale | semantic | run-cold-start-recovery.md | page updated 2026-10-08; S019 re-ingested 2026-10-10 | re-read S019 source and confirm page claims; bump `updated` if still accurate |
| 47 | stale | semantic | run-commands-and-snapshots.md | page updated 2026-09-30; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 48 | stale | semantic | run-content-dispatch.md | page updated 2026-10-07; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 49 | stale | semantic | run-content-dispatch.md | page updated 2026-10-07; S019 re-ingested 2026-10-10 | re-read S019 source and confirm page claims; bump `updated` if still accurate |
| 50 | stale | semantic | run-contracts.md | page updated 2026-10-14; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 51 | stale | semantic | run-economy-and-prestige.md | page updated 2026-10-14; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 52 | stale | semantic | run-panels.md | page updated 2026-10-14; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 53 | stale | semantic | run-persistence.md | page updated 2026-10-13; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 54 | stale | semantic | run-prestige-transition.md | page updated 2026-10-01; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 55 | stale | semantic | run-session.md | page updated 2026-09-30; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 56 | stale | semantic | run-shell-and-panels.md | page updated 2026-11-04; S038 re-ingested 2026-11-05 | re-read S038 source and confirm page claims; bump `updated` if still accurate |
| 57 | stale | semantic | run-shell-device-profile-and-text-scale.md | page updated 2026-10-09; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 58 | stale | semantic | run-shell-notices-and-gate-fixes.md | page updated 2026-10-07; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 59 | stale | semantic | run-shell-notices-and-gate-fixes.md | page updated 2026-10-07; S019 re-ingested 2026-10-10 | re-read S019 source and confirm page claims; bump `updated` if still accurate |
| 60 | stale | semantic | run-state-domain.md | page updated 2026-09-30; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 61 | stale | semantic | run-unlocks.md | page updated 2026-09-30; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 62 | stale | semantic | save-v2-and-v1-import.md | page updated 2026-10-09; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 63 | stale | semantic | save-v2-document.md | page updated 2026-10-10; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 64 | stale | semantic | unity-serialized-asset-edits.md | page updated 2026-10-07; S018 re-ingested 2026-10-10 | re-read S018 source and confirm page claims; bump `updated` if still accurate |
| 65 | stale | semantic | unity-serialized-asset-edits.md | page updated 2026-10-07; S019 re-ingested 2026-10-10 | re-read S019 source and confirm page claims; bump `updated` if still accurate |
| 66 | stale | semantic | unity-test-and-maqa-readiness.md | page updated 2026-10-13; S022 re-ingested 2026-10-27 | re-read S022 source and confirm page claims; bump `updated` if still accurate |
| 67 | stale | semantic | urp-rendering-foundation.md | page updated 2026-10-29; S038 re-ingested 2026-11-05 | re-read S038 source and confirm page claims; bump `updated` if still accurate |
| 68 | citations | semantic | (multiple, 111 lines) | 111 prose lines without an S-id (heuristic; sample: android-back-gesture.md:40; android-emulator-profiling.md:16; android-emulator-validation.md:20; android-emulator-validation.md:21) | add the source ID to each claim, or confirm the line is structural/restates a cited sentence; not auto-fixed |
