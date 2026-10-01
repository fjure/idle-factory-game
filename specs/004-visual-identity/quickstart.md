# Quickstart: Validating Feature 004

Validation guide, not an implementation guide. Details live in the [contracts](./contracts/) and the [data model](./data-model.md).

## Prerequisites

- Feature 003 complete (all scene/camera/settings Issues merged, T110 catalog merged).
- Unity `6000.6.0f1`, leases via `scripts/resource-lease.ps1` (`unity`, plus `emulator` for Android or the Windows player run).
- For production and checks: `uv` (Python + pinned Pillow from `tools/art-pipeline/uv.lock`), Blender 5.2.1, local ComfyUI v0.38.0 per `docs/integrations/mcp-readiness.md`, verified by a real `server_info` / scene call in the session.

## Scenario 1 — Phase 0 parity (US7)

1. Capture baselines (B1 performance, screenshots per research R13) before the switch.
2. Apply the phase-0 change set ([render-foundation](./contracts/render-foundation.md)).
3. Run `scripts/test-unity.ps1`, `scripts/test-unity.ps1 -StandaloneWindowsPlayer`, the screenshot comparison, `scripts/android-emulator-journey.ps1` and the B2 measurement in the same session as a B1 control.
4. Expected: same pass set, screenshots within tolerance, emulator journey PASS, B2 within the provisional budgets.

## Scenario 2 — Style spike decision (US6, FR-007/FR-008)

1. Produce the spike set for each candidate through its route; run `uv run --project tools/art-pipeline checks` on it.
2. Show each candidate in the game (phone, tablet, emulator) at `n_min` and fit-all.
3. The independent agent reviewer names the shuffled grayscale/CVD sheets; the owner runs the timed trial (SC-002).
4. Expected: `reviews/style-spike.md` records gate results, the Art Director's recommendation, the Game Designer/PO check and the owner's decision (projection, `D`, cue variant, lighting) before any production asset.

## Scenario 3 — Crisp scale on every screen class (US3)

1. Run Edit Mode `CameraScaleModelTests` and `ScreenClassScaleTableTests`.
2. In Play Mode, step through every allowed scale and fit-all at each Feature 003 screen class and orientation; run the crispness test.
3. Expected: `n_fit ≥ n_min` everywhere, uniform `n×n` art-pixel blocks at rest, all 12 slots at fit-all, selected slot visible above an open sheet.

## Scenario 4 — State cues at a glance (US1)

1. Generate the shuffled sheets with the pipeline; the independent agent reviewer names every tile (SC-001).
2. Owner timed trial: 10 randomized 12-slot screenshots with one stuck machine each; ≥ 9 found within 2 s (SC-002).
3. Play Mode: fully occupied frame, no cue hidden; overhang tap selects the slot behind.

## Scenario 5 — Motion and fallback (US4, FR-024)

1. Toggle reduced motion (`on`, `off`, `followSystem`) and corrupt `display-v1.json`; expect frozen loops when effective, fallback to `followSystem` on corruption, the save untouched.
2. Remove one F004 sprite from the catalog in a test fixture; expect the F003 placeholder, one `presentation.art.fallback` diagnostic, a tappable slot.

## Scenario 6 — Restyle completeness and provenance (US2, US5, SC-003, SC-004)

1. Run `F004AssetContractTests` and the pipeline checks report.
2. Pick any shipped sprite and regenerate it from its provenance record; expect pixel identity (scripted) or ≤ 0.5 % opaque-pixel difference (Blender, generative; research R09).

## Scenario 7 — Performance (FR-025–FR-027, SC-007)

1. Measure B3 on W003 and W004-V with a same-session control (T065 protocol, profiler-free build).
2. Expected: ≥ 60 FPS average, 0 steady-state allocations per frame, texture and APK within the frozen budgets; physical-device gap stated in the report.
