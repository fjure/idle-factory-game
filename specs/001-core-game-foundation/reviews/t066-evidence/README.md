# T066 zoom readability evidence

Closes the acceptance-evidence gap recorded in
`specs/001-core-game-foundation/reviews/f001-placeholder-final-review.md` ("Zoom readability — FAIL"):
the supported 640x360 maximum-zoom-out view (orthographic size 12, the `FactoryCameraController` maximum, 15
px/cell) had no Play Mode capture proving the factory boundary, both footprint silhouettes and the retained
status badge symbol actually survive Unity's point-sampled render at that limit.

## Test

`Assets/IdleFactory/Tests/PlayMode/ZoomReadabilityTests.cs`, two `[UnityTest]` methods.

### Method A — `AuthoredSceneD001FootprintBoundaryAndBadgeReadableAtMaximumZoomOut`

Loads the authored `Assets/IdleFactory/Scenes/CoreGameFoundation.unity` additively in Play Mode (via
`SceneCompositionTestScope.WithSeededCheckpoint`, matching `CoreGameFoundationSceneTests`'s pattern so the
isolated checkpoint store never touches `persistentDataPath`), seeds it with one real D001 machine (anchor
(2,2), the real dev catalog and D001 sprite), selects it so its status badge renders, then renders the
scene's own `CoreGameFoundationBootstrap.FactoryCamera` into an off-screen `RenderTexture`.

### Method B — `StandaloneAlternateFootprintBothOrientationsReadableAtMaximumZoomOut`

The authored scene's `DefinitionCatalogAsset` registers only D001 (2x2), so the second footprint silhouette
the review requires (the alternate 3x1 body) cannot be placed through the authored scene. This method builds
a standalone `FactoryView` with the *same* production sprites (loaded from
`Assets/IdleFactory/Art/Development/F001/Imported/`, the same PNGs the authored scene imports — not
fabricated test art), the same `GridWorldMapper` and `FactoryBounds` (16x16, cell size 1), and an identically
configured camera (world bounds, position, orthographic size), following
`NavigationPlacementTests.CreateDefinitions`'s pattern for the alternate `Footprint(3, 1)` building
(`allowedOrientations: {0, 1}`). It places one instance at orientation 0 (3 wide x 1 tall, anchor (2,6)) and
one at orientation 1 (its quarter-turn, 1 wide x 3 tall, anchor (10,2)) — covering both the second footprint
*silhouette* the review flagged and both footprint *orientations* named in this task — and selects each in
turn so both status badges are exercised.

Both methods capture at the scene's default framed camera position and at a second position offset by a
non-cell-aligned `(0.37, 0.21)` world-unit delta, because point-sampled (nearest-filtered) sprites at a heavy
64px-source-to-15px-screen downscale can lose or gain detail depending on exactly which texel each screen
pixel samples; a single capture position could not rule that out.

## Viewport, zoom, and projection

| Property | Value |
|---|---|
| Render target | 640 x 360 `RenderTexture`, camera `targetTexture` |
| Orthographic size | 12 (`FactoryCameraController.MaximumOrthographicSize`, ART-F1 / T041-F1) |
| Projection | 360 / (2 x 12) = **15 px per world-unit cell** (both axes; viewport aspect 640/360 = 16/9 matches world aspect exactly) |
| Factory bounds | 16 x 16 world units, cell size 1 |
| D001 footprint on screen | 2 x 2 cells -> 30 x 30 px |
| Alternate footprint on screen | 3 x 1 / 1 x 3 cells -> 45 x 15 / 15 x 45 px |
| Status badge on screen | 1 x 0.5 cells (64x32 source / 64 PPU) -> 15 x 7.5 px |
| Camera background | Solid color `(0.035, 0.047, 0.055, 1)`, exactly the authored scene Camera's `m_BackGroundColor` |

These figures match the "Zoom readability" table in `f001-placeholder-final-review.md` exactly (640x360
rotated row: 15.00 px/cell, 15.00x7.50 badge).

## What each frame asserts, and where

For every captured frame:

1. **Factory boundary** — the world region `(4, -0.3, 8, 0.6)` (a strip straddling the bottom world edge,
   `y = 0`, where boundary edge modules render) is converted to a screen rect via `Camera.WorldToScreenPoint`
   and its mean luminance is asserted to differ from the background sample by more than `0.03` — i.e. the
   boundary must remain visually distinguishable from the clear-color background.
2. **Footprint silhouette(s)** — each building's occupied-cell world rect (computed from its anchor and
   oriented footprint size, matching `PlacementPreviewView.OrientedFootprintSize`) is asserted to (a) differ
   in mean luminance from the background by more than `0.03`, and (b) have luminance variance greater than
   zero, so a flat single-value patch (which would mean the outline/detail collapsed to a blob) fails.
3. **Status badge symbol** — each selected building's badge world rect (matching `FactoryView.AddBadge`'s
   placement: centered above the footprint, `1 x 0.5` world units) is asserted to have luminance variance
   above a `0.0008` floor. A flat-colored swatch has ~zero internal variance; a retained symbol/glyph
   (the approved F001 badges pair a distinct shape with a label, per the T013/T037 source review) does not.

## Color independence

Every measurement above is computed only from **relative luminance** (`Luminance(pixel) = (0.2126·R +
0.7152·G + 0.0722·B) / 255`, Rec. 709 weights) — hue and saturation are discarded entirely before any
comparison or threshold runs. This mirrors how the approved F001 badges themselves are meant to be told
apart (per the T013 source review: "state meaning does not depend on hue") and directly answers the review's
requirement to confirm readability "without relying on color": every pass/fail decision in this test would be
identical if the whole render were desaturated first.

## Result (this run)

Both methods passed:

- `AuthoredSceneD001FootprintBoundaryAndBadgeReadableAtMaximumZoomOut`: boundary, D001 2x2 silhouette and its
  status badge (missing-input, since the seeded machine starts with empty input buffers) were all distinct
  from background and retained internal detail at both camera positions.
- `StandaloneAlternateFootprintBothOrientationsReadableAtMaximumZoomOut`: boundary, both the 3x1 and 1x3
  alternate footprint silhouettes, and each selected instance's badge (idle/normal, since the alternate
  definition has no recipe) passed at both camera positions.

Per the assignment's decision rule, because the rendered evidence **passed**, no camera clamp was applied to
`Assets/IdleFactory/Unity/Presentation/FactoryCameraController.cs`; its maximum orthographic size remains 12.

Command: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1 -TestFile
"Assets/IdleFactory/Tests/PlayMode/ZoomReadabilityTests.cs"` — 2/2 passed. Full suite:
`scripts/test-unity.ps1 -Suite PlayMode` — 149/149 passed.

## Captured frames

| File | Scenario | Camera position |
|---|---|---|
| `authored-d001-default.png` | Authored scene, D001 2x2, selected (missing-input badge) | Default (framed on factory center) |
| `authored-d001-offset.png` | Same | +0.37, +0.21 world units (sub-cell offset) |
| `alternate-horizontal-default.png` | Standalone rig, 3x1 selected, 1x3 also present | Default |
| `alternate-horizontal-offset.png` | Same | Sub-cell offset |
| `alternate-vertical-default.png` | Standalone rig, 1x3 selected, 3x1 also present | Default |
| `alternate-vertical-offset.png` | Same | Sub-cell offset |

## Scope note for the Art Director re-review

This packet covers the T066 capture, the Play Mode assertions and this evidence write-up only. It does not
edit `specs/001-core-game-foundation/reviews/f001-placeholder-final-review.md`; the Art Director records the
re-review verdict there in a separately assigned append.
