# Contract: Integer Camera Scale

**Scope**: spec FR-011, FR-012, FR-016–FR-018, US3, SC-005; research R02, R03, R15.
**Owner**: `Assets/IdleFactory/Unity/Presentation/FactoryCameraController.cs` (sole writer of camera size and position). Core is not involved.

## Definitions

- `D`: art pixels per cell (= sprite PPU), from the Art Bible.
- `n`: integer scale, device pixels per art pixel.
- `pxPerPoint`: physical pixels per UI panel point for the current screen.
- `rect`: HUD-aware unobstructed rectangle in physical pixels (existing `TryGetUnobstructedScreenRect`).

## Rules

1. At rest, `orthographicSize = camera.pixelHeight / (2 · n · D)` and the camera position is a multiple of `1 / (n · D)` world units on both axes.
2. `n_min = ⌈readableCellPoints · pxPerPoint / D⌉`; `maxScale` from the Art Bible.
3. Fit-all: `n_fit` = the largest `n` in `[n_min, maxScale]` at which the 12-slot extent plus a half-cell margin fits `rect`. Remaining space is margin. If no such `n` exists, the screen class is a contract violation and the Edit Mode screen-class table test fails.
4. Pinch and wheel change a continuous preview; on gesture end, snap to the nearest allowed `n` (ties toward the larger). Zoom buttons step by one. The preview never persists at rest.
5. A second world touch, rotation or layout change during a gesture cancels it and re-fits only if the current `n` no longer fits.
6. Pan limits are recomputed from `rect` and `n`; panning never shows more than a half-cell margin beyond the factory extent.
7. With a bottom sheet open, the selected slot stays fully inside the visible world band; if it cannot at the current `n`, the camera steps down to the largest `n ≥ n_min` at which it can.
8. Hit testing is unchanged: footprint only, through `GridWorldMapper`.
9. Camera state is never saved.

## Tests

- Edit Mode `CameraScaleModelTests`: rules 1–4 and 7 as pure functions, including fractional `pxPerPoint` values (1.68, 2.0, 2.625, 3.0).
- Edit Mode `ScreenClassScaleTableTests`: every supported screen class and orientation × the chosen `D` satisfies `n_fit ≥ n_min`.
- Play Mode: re-baselined `ZoomReadabilityTests` and `SlotGridTests` (each changed expectation listed in the task evidence); a crispness test that renders at rest and asserts that sampled art-pixel blocks are uniform `n×n` squares.
