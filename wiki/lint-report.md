# Wiki Lint Report — 2026-10-29

Scope: full pass, 96 pages, 48 registered sources (S001-S048). Config: stale_after_days 90, auto_fix index-and-links. No mechanical fixes were needed in this pass. Mechanical checks (index drift, page links, citations to unknown source IDs, orphans) are clean apart from the two open items below. Lint wrote no page prose, claims, conflict markers or sources.md entries.

Summary: index-drift 0, links 2 open (1 unregistered citation S061, 1 non-page link `(BLAST)`), orphans 0, contradictions 1 open, stale 68 open (re-verification flags), citations 0 pages flagged.

| # | Check | Severity | Page | Finding | Suggested fix | State |
|---|---|---|---|---|---|---|
| 1 | links | mechanical | pages/f005-batch-d-final-evidence.md | cites S061, which is not registered in sources.md (pre-existing since batch 4) | register the source or correct the citation (owner; sources.md is append-only and was not edited) | Open: not auto-fixed |
| 2 | links | mechanical | pages/android-emulator-profiling.md | `[…](BLAST)` in the Frame pacing bullet: a link to a non-page target | replace with a code span `BLAST` (prose change, owner) | Open: not a page link, not auto-repaired |
| 3 | contradictions | semantic | pages/ui-toolkit-hud-layout-pitfalls.md | line 20: unresolved conflict marker: > ⚠ conflict: S001 says the `sw411dp` phone runs at about 1.68 px/pt in either orientation; S018 (T191, measured in a 1080×2400 panel) says `ScaleWithScreenSize | resolve by owner | Open: semantic |
| 4 | stale | semantic | pages/android-emulator-validation.md | cites S045, last ingested 2026-10-28 newer than updated 2026-10-27 | re-verify and bump updated | Open: semantic |
| 5 | stale | semantic | pages/android-runtime-rotation.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-09 | re-verify and bump updated | Open: semantic |
| 6 | stale | semantic | pages/android-runtime-rotation.md | cites S019, last ingested 2026-10-10 newer than updated 2026-10-09 | re-verify and bump updated | Open: semantic |
| 7 | stale | semantic | pages/core-architecture.md | cites S038, last ingested 2026-10-28 newer than updated 2026-10-27 | re-verify and bump updated | Open: semantic |
| 8 | stale | semantic | pages/desktop-focus-lifecycle.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-10 | re-verify and bump updated | Open: semantic |
| 9 | stale | semantic | pages/deterministic-production.md | cites S018, last ingested 2026-10-10 newer than updated 2026-09-29 | re-verify and bump updated | Open: semantic |
| 10 | stale | semantic | pages/f003-art-pipeline.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-09 | re-verify and bump updated | Open: semantic |
| 11 | stale | semantic | pages/f003-ui-ux-final-review.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-09 | re-verify and bump updated | Open: semantic |
| 12 | stale | semantic | pages/f003-ui-ux-final-review.md | cites S019, last ingested 2026-10-10 newer than updated 2026-10-09 | re-verify and bump updated | Open: semantic |
| 13 | stale | semantic | pages/f004-batch-1-art-tooling.md | cites S038, last ingested 2026-10-28 newer than updated 2026-10-27 | re-verify and bump updated | Open: semantic |
| 14 | stale | semantic | pages/f005-batch-1-workflow-pitfalls.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-13 | re-verify and bump updated | Open: semantic |
| 15 | stale | semantic | pages/f005-batch-a-economy-and-contracts.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-14 | re-verify and bump updated | Open: semantic |
| 16 | stale | semantic | pages/f005-batch-a-open-decisions.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-22 | re-verify and bump updated | Open: semantic |
| 17 | stale | semantic | pages/f005-batch-b-auto-deliver-and-production.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-21 | re-verify and bump updated | Open: semantic |
| 18 | stale | semantic | pages/f005-batch-b-live-gap.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-21 | re-verify and bump updated | Open: semantic |
| 19 | stale | semantic | pages/f005-batch-b-save-v3-drift.md | cites S029, last ingested 2026-10-27 newer than updated 2026-10-21 | re-verify and bump updated | Open: semantic |
| 20 | stale | semantic | pages/f005-batch-b-save-v3-drift.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-21 | re-verify and bump updated | Open: semantic |
| 21 | stale | semantic | pages/f005-batch-b-ui-and-android.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-21 | re-verify and bump updated | Open: semantic |
| 22 | stale | semantic | pages/f005-batch-b-workflow-pitfalls.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-22 | re-verify and bump updated | Open: semantic |
| 23 | stale | semantic | pages/f005-batch-c-content-revision-3.md | cites S029, last ingested 2026-10-27 newer than updated 2026-10-22 | re-verify and bump updated | Open: semantic |
| 24 | stale | semantic | pages/f005-batch-c-content-revision-3.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-22 | re-verify and bump updated | Open: semantic |
| 25 | stale | semantic | pages/f005-batch-c-playtest-and-decisions.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-22 | re-verify and bump updated | Open: semantic |
| 26 | stale | semantic | pages/f005-batch-c-policy-r2-and-gates.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-22 | re-verify and bump updated | Open: semantic |
| 27 | stale | semantic | pages/f005-batch-c-ui-and-devtools-fixes.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-22 | re-verify and bump updated | Open: semantic |
| 28 | stale | semantic | pages/f005-content-v3-transitional.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-14 | re-verify and bump updated | Open: semantic |
| 29 | stale | semantic | pages/f005-plan-overview.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-21 | re-verify and bump updated | Open: semantic |
| 30 | stale | semantic | pages/f005-session-log-and-devtools.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-21 | re-verify and bump updated | Open: semantic |
| 31 | stale | semantic | pages/f005-session-log-writer.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-21 | re-verify and bump updated | Open: semantic |
| 32 | stale | semantic | pages/f005-timeline-mapping-and-auto-delivery.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-10 | re-verify and bump updated | Open: semantic |
| 33 | stale | semantic | pages/factory-run-bounds-and-offline-segmentation.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-14 | re-verify and bump updated | Open: semantic |
| 34 | stale | semantic | pages/factory-run-bounds-and-offline-segmentation.md | cites S027, last ingested 2026-10-21 newer than updated 2026-10-14 | re-verify and bump updated | Open: semantic |
| 35 | stale | semantic | pages/factory-run-simulation.md | cites S018, last ingested 2026-10-10 newer than updated 2026-09-30 | re-verify and bump updated | Open: semantic |
| 36 | stale | semantic | pages/feature-001-retirement.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-08 | re-verify and bump updated | Open: semantic |
| 37 | stale | semantic | pages/feature-001-retirement.md | cites S019, last ingested 2026-10-10 newer than updated 2026-10-08 | re-verify and bump updated | Open: semantic |
| 38 | stale | semantic | pages/frame-rate-target.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-09 | re-verify and bump updated | Open: semantic |
| 39 | stale | semantic | pages/gameplay-content-v2.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-13 | re-verify and bump updated | Open: semantic |
| 40 | stale | semantic | pages/gameplay-content-validation.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-07 | re-verify and bump updated | Open: semantic |
| 41 | stale | semantic | pages/local-checkpoints-and-offline-return.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-09 | re-verify and bump updated | Open: semantic |
| 42 | stale | semantic | pages/run-balance-policy-and-f005-matrix.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-22 | re-verify and bump updated | Open: semantic |
| 43 | stale | semantic | pages/run-boosts-and-progression.md | cites S018, last ingested 2026-10-10 newer than updated 2026-09-30 | re-verify and bump updated | Open: semantic |
| 44 | stale | semantic | pages/run-bootstrap-and-lifecycle.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-09 | re-verify and bump updated | Open: semantic |
| 45 | stale | semantic | pages/run-bootstrap-and-lifecycle.md | cites S019, last ingested 2026-10-10 newer than updated 2026-10-09 | re-verify and bump updated | Open: semantic |
| 46 | stale | semantic | pages/run-cold-start-and-v1-import.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-08 | re-verify and bump updated | Open: semantic |
| 47 | stale | semantic | pages/run-cold-start-recovery.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-08 | re-verify and bump updated | Open: semantic |
| 48 | stale | semantic | pages/run-cold-start-recovery.md | cites S019, last ingested 2026-10-10 newer than updated 2026-10-08 | re-verify and bump updated | Open: semantic |
| 49 | stale | semantic | pages/run-commands-and-snapshots.md | cites S018, last ingested 2026-10-10 newer than updated 2026-09-30 | re-verify and bump updated | Open: semantic |
| 50 | stale | semantic | pages/run-content-dispatch.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-07 | re-verify and bump updated | Open: semantic |
| 51 | stale | semantic | pages/run-content-dispatch.md | cites S019, last ingested 2026-10-10 newer than updated 2026-10-07 | re-verify and bump updated | Open: semantic |
| 52 | stale | semantic | pages/run-contracts.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-14 | re-verify and bump updated | Open: semantic |
| 53 | stale | semantic | pages/run-economy-and-prestige.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-14 | re-verify and bump updated | Open: semantic |
| 54 | stale | semantic | pages/run-panels.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-14 | re-verify and bump updated | Open: semantic |
| 55 | stale | semantic | pages/run-persistence.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-13 | re-verify and bump updated | Open: semantic |
| 56 | stale | semantic | pages/run-prestige-transition.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-01 | re-verify and bump updated | Open: semantic |
| 57 | stale | semantic | pages/run-session.md | cites S018, last ingested 2026-10-10 newer than updated 2026-09-30 | re-verify and bump updated | Open: semantic |
| 58 | stale | semantic | pages/run-shell-and-panels.md | cites S038, last ingested 2026-10-28 newer than updated 2026-10-27 | re-verify and bump updated | Open: semantic |
| 59 | stale | semantic | pages/run-shell-device-profile-and-text-scale.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-09 | re-verify and bump updated | Open: semantic |
| 60 | stale | semantic | pages/run-shell-notices-and-gate-fixes.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-07 | re-verify and bump updated | Open: semantic |
| 61 | stale | semantic | pages/run-shell-notices-and-gate-fixes.md | cites S019, last ingested 2026-10-10 newer than updated 2026-10-07 | re-verify and bump updated | Open: semantic |
| 62 | stale | semantic | pages/run-state-domain.md | cites S018, last ingested 2026-10-10 newer than updated 2026-09-30 | re-verify and bump updated | Open: semantic |
| 63 | stale | semantic | pages/run-unlocks.md | cites S018, last ingested 2026-10-10 newer than updated 2026-09-30 | re-verify and bump updated | Open: semantic |
| 64 | stale | semantic | pages/save-v2-and-v1-import.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-09 | re-verify and bump updated | Open: semantic |
| 65 | stale | semantic | pages/save-v2-document.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-10 | re-verify and bump updated | Open: semantic |
| 66 | stale | semantic | pages/ui-toolkit-hud-layout-pitfalls.md | cites S038, last ingested 2026-10-28 newer than updated 2026-10-27 | re-verify and bump updated | Open: semantic |
| 67 | stale | semantic | pages/unity-player-validation.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-08 | re-verify and bump updated | Open: semantic |
| 68 | stale | semantic | pages/unity-presentation-boundary.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-08 | re-verify and bump updated | Open: semantic |
| 69 | stale | semantic | pages/unity-serialized-asset-edits.md | cites S018, last ingested 2026-10-10 newer than updated 2026-10-07 | re-verify and bump updated | Open: semantic |
| 70 | stale | semantic | pages/unity-serialized-asset-edits.md | cites S019, last ingested 2026-10-10 newer than updated 2026-10-07 | re-verify and bump updated | Open: semantic |
| 71 | stale | semantic | pages/unity-test-and-maqa-readiness.md | cites S022, last ingested 2026-10-27 newer than updated 2026-10-13 | re-verify and bump updated | Open: semantic |
