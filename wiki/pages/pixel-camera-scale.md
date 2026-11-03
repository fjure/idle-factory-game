---
title: Pixel camera scale
type: decision
sources: [S045, S038, S039, S041]
updated: 2026-10-28
---

# Pixel camera scale

Feature 004 keeps pixel art crisp with an integer scale `n` (device pixels per art pixel) owned by `FactoryCameraController`, instead of URP's `PixelPerfectCamera` component. That component drives `orthographicSize` from a reference resolution, conflicts with the controller's pan and zoom math and its HUD-aware framing, and its upscale render texture letterboxes on phone aspect ratios. (S038)

## Rules

- At rest, `orthographicSize = pixelHeight / (2·n·D)` with `D` art pixels per cell; the position snaps to `1/(n·D)` world units (one device pixel); an odd pixel extent offsets that grid by half a **device** pixel, `1/(2·n·D)`. (S038, S041)
- `n_min = ceil(readableCellPoints·pxPerPoint/D − 0.01)`: a 1 % tolerance (owner decision 2026-10-28) so the 1280x720 @ 320 dpi phone (2.0046 px/pt) keeps `n_min` 2 at D = 24. (S041)
- Scale is measured in **physical pixels**, never UI points, so fractional device ratios change only the physical cell size. (S038)
- Pinch and wheel preview continuously and snap to an allowed `n` on gesture end; fit-all picks the largest `n` at which 12 slots plus a half-cell margin fit the unobstructed rectangle. (S038)
- Feature 003's Z16–Z64 zoom table and its tests are superseded and re-baselined explicitly, never deleted silently. (S038)

## Gotcha: points versus pixels

The readability floor is in UI points (a cell ≥ 24 pt), but integer scaling is in device pixels. On the reference phone (~1.68 px/pt, see [UI Toolkit HUD layout pitfalls](./ui-toolkit-hud-layout-pitfalls.md)) in landscape, fit-all is about 53.8 px per cell: 32 art px per cell gives n = 1 and ≈ 19 pt cells, below the floor, while 24 or 16 give ≈ 28.6 pt. Art density is therefore a spike decision guarded by an Edit Mode table over every screen class and ratio (`n_fit ≥ n_min`). (S038)

## Baseline on `main` (re-validated 2026-10-27)

- Zoom is continuous today: `FactoryCameraController` scales `orthographicSize` exponentially, clamped by `RunZoomSettings` in `SlotGridView.cs` (16–64 pt per cell, fit-all floor 24). (S038)
- Two writers: `RunCameraFraming.FitAll` also writes `orthographicSize`. After T427 only the controller writes the camera, `RunCameraFraming` keeps geometry only, and a `git grep` guard checks it. `RunZoomSettings` stays a serialized type reduced to the Art Bible inputs, because the scene serializes it and scene YAML is never hand-edited. (S038)
- The unobstructed rect and `pxPerPoint` come from `RunShellDocument` (`TryGetUnobstructedScreenRect`, `Root.panel.scaledPixelsPerPoint`), not `FactoryHudDocument`. The rect already excludes the top strip, bars, zoom column, open sheets and the Feature 005 ore-vein overlay. It returns false until layout resolves, so the first fit waits for it. (S038)
- `ZoomReadabilityTests` no longer exists (deleted with the Feature 001 runtime). The invariants live in seven named cases of `RunHudLayoutTests` and `SlotGridTests`, which are re-baselined with an old-to-new diff. (S038)
- Pinch end is not an input signal yet. It becomes an additive `NavigationIntentKind` member (T454); wheel and buttons are single-shot and never preview. (S038)

## Baseline stand-in (T409)

The pre-switch screenshot fixture has no integer scale model yet, so its `n_min` preset is the Feature 003 floor of 24 px per cell. Fit-all in the baselines ranges from 29.75 px per cell (640x360) to 64.00 (clamped, 1280x720 and 1920x1080). T427 must re-baseline the `n_min` preset. (S039)

## CameraScaleModel (T408)

T408 (#520, PR #577, `898d301`) added the pure `CameraScaleModel` (rules 1-7) with 40 Edit Mode tests; the Edit Mode total is now 2363. (S041)

- Odd pixel extents need a half-**device**-pixel offset, `1/(2*n*D)`. The contract wording "half an art pixel" is wrong. (S041)
- Owner decision 2026-10-28: `n_min` gets a 1 % tolerance, ceil(... - 0.01). Reason: 1280x720 at 320 dpi landscape has 2.0046 px/pt, giving `n_min` 3 above `n_fit` 2 at D = 24. T427 implements it (recorded on #539). (S041)
- Stale sheet state on a layout change is a T427 precondition. The level count is `maxScale - n_min + 1`. (S041)

T414 exercises the model in the isolated spike scene: [F004 isolated spike import](./f004-t414-spike-import.md). (S045)

Related: [visual identity and art direction](./visual-identity-art-direction.md), [run shell and panels](./run-shell-and-panels.md).
