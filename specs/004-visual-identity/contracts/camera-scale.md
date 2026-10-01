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
2. `n_min = ⌈readableCellPoints · pxPerPoint / D⌉`; `maxScale` from the Art Bible. The allowed set is `[n_min, maxScale]`. `pxPerPoint` is the HUD panel's `scaledPixelsPerPoint`, supplied by `FactoryHudDocument` with `rect`. An odd camera pixel width or height offsets the camera by half an art pixel so the grid stays aligned.
3. Fit-all: `n_fit` = the largest `n` in `[n_min, maxScale]` at which the 12-slot extent plus a half-cell margin fits `rect`; the factory is centered and the remaining space is margin. For a supported screen class, no such `n` is a contract violation caught by the table test. At runtime (free desktop windows, continuous `pxPerPoint`) the camera instead uses the largest `n ≥ 1` that fits, ignores the floor and logs `presentation.scale.belowFloor` once per layout; it never uses a fractional scale.
4. Pinch changes a continuous preview; on gesture end it snaps instantly to the nearest allowed `n` (ties toward the larger) while the world point under the gesture anchor stays fixed. One wheel notch and one zoom-button press step by one level. Buttons are disabled with a visible reason at the range ends and hidden when the range has a single level. The preview never persists at rest.
5. A second world touch, rotation or layout change during a gesture cancels it and re-fits only if the current `n` no longer fits.
6. Pan limits are recomputed from `rect` and `n`; panning never shows more than a half-cell margin beyond the factory extent.
7. With a bottom sheet open, the selected slot stays fully inside the visible world band: first pan it into the band (pan limits relaxed by the band), then, if it still does not fit, step down to the largest `n ≥ n_min` at which it does. On sheet close the prior `n` and position return unless the player zoomed or panned meanwhile. Changes are instant. With a single-level range (`n_min = n_fit = maxScale` on the worst phone at D = 24), only the pan step applies; if the slot still cannot fit, the slot's footprint is aligned to the band's top edge and the selection frame stays visible (the sheet may cover part of the slot's overhang band, never the footprint). Pinch has no effect in a single-level range and buttons are hidden.
8. Hit testing is unchanged: footprint only, through `GridWorldMapper`.
9. Camera state is never saved.

## Tests

- Edit Mode `CameraScaleModelTests`: rules 1–7 as pure functions (including gesture cancel and pan limits), fractional `pxPerPoint` values (1.68, 2.0, 2.625, 3.0), odd pixel sizes, free desktop window sizes and the runtime degrade; hit area of at least 44×44 pt for a slot footprint at every allowed `n`.
- Edit Mode `ScreenClassScaleTableTests`: every supported screen class and orientation × the chosen `D` satisfies `n_fit ≥ n_min`, with the expected level count and margin per class (including 360×640 and 390×844 portrait), emitted as a table into every spike and acceptance record; a Play Mode variant uses the real `TryGetUnobstructedScreenRect`, and a negative case with an oversized HUD must fail.
- Play Mode: re-baselined `ZoomReadabilityTests` and `SlotGridTests` (invariants kept: 12 slots at fit-all, selected slot above a sheet, minimum cell via `n_min`; old-to-new assertion diff in the QA report); a crispness test (research R13 capture method) asserting uniform `n×n` art-pixel blocks at rest, tagged `[Category("StandalonePlayer")]` as well; selection-frame thickness in art pixels at each `n`; a live window resize re-fits only when the current `n` no longer fits.
