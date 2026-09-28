# Independent Art Director and Technical Artist Review: F001 Development Placeholders

- **Issue / work unit**: MAQA Issue #37 / WU38 / T037
- **Reviewed baseline**: T035 integrated scene at `5b0760ae86a512ecf403486fe15b5703fa2f1931`
- **Review date**: 2026-09-25
- **Reviewer roles**: Independent Art Director and Technical Artist
- **Independence**: This review did not author the approved F001 brief, SVG candidates, T013 source review, T014 imports/integration record, or T035 scene integration.

## Verdict

**FAIL for the required nearest-supported zoom-readability evidence; PASS for the inspected asset, import, alignment, sorting, and reference contracts.** No source/import defect was found, but the supported camera range includes a 15 px/cell projection whose mandatory below-16 silhouette and status-symbol behavior has not been verified in the integrated scene. T037 cannot claim an overall Art/Technical-Art PASS until that specific view is checked.

The report does not approve final art or production use. Art Bible status remains N/A; F001 v0.1 is temporary development content. No physical-device or runtime-memory measurement is claimed.

## Authority and reviewed evidence

- `docs/art/F001-development-placeholders-v0.1.md`, especially Production Contract, Complete Inventory, State Composition Rules, Zoom and Readability Targets, and acceptance checklist.
- `specs/001-core-game-foundation/contracts/presentation-states.md`.
- T013 independent source review: `specs/001-core-game-foundation/reviews/f001-placeholder-source-review.md`.
- T014 technical import record: `docs/art/F001-technical-art-integration.md`.
- T035 integrated `Assets/IdleFactory/Scenes/CoreGameFoundation.unity`, `CoreGameFoundationBootstrap.cs`, `FactoryView.cs`, `PlacementPreviewView.cs`, `GridWorldMapper.cs`, and the D001 development definition.
- All 21 imported PNGs, their 21 PNG `.meta` files, all 21 source SVGs and their 21 SVG `.meta` files; the `Source.meta` and `Imported.meta` directory records.
- `Assets/IdleFactory/Tests/EditMode/F001AssetContractTests.cs` and `Assets/IdleFactory/Unity/Presentation/F001SpriteCatalog.cs`.
- Unity target is `6000.6.0f1`; rasterizer recorded by T014 is Inkscape `1.4.2 (f4327f4, 2025-05-13)`. The installed Inkscape executable reports the same version.

The T013 source review's scope remains source SVG conformance. This review relies on T014's decoded-RGBA source/import comparison for the deterministic raster transform; it does not represent that transform comparison as newly rerun.

## Findings

### Approved-source fidelity and visual states — PASS

T013 approved all 21 repository-authored SVG sources against F001 v0.1, including exact viewBox dimensions, palette, path-authored labels, silhouettes, and provenance. T014 records that two independently rasterized runs matched pixel-for-pixel for all 21 candidates, with no crop, padding, rescale, or atlas packing. I inspected the imported machine and ghost bodies for both 2x2 and 3x1 footprints, the input/output symbols, grid/boundary modules, and all eight badges at native resolution. The selected bodies preserve their normal silhouette and add a second outline; valid/invalid ghosts use distinct check/X cues and opposing hatch patterns. The eight state badges pair distinct shapes with labels, so state meaning does not depend on hue.

The production inventory remains development-only and does not imply ports, belts, power, final building identity, or an approved final art direction. D001 is flagged `isDevelopmentOnly: 1`, and the bootstrap's ready feedback calls the factory development-only. The resource glyphs remain labeled development input/output cues and are not interpreted as machine ports.

### Footprint, pivots, cell alignment, and sorting — PASS

One world unit maps to one logical cell. The 64 PPU import contract therefore maps each 64 source/import pixels to one cell. The imported canvases are exact: 64x64 tiles/boundary modules, 128x128 D001 body/ghosts, 192x64 alternate body/ghosts, 32x32 resources, and 64x32 badges.

The four machine/ghost bodies use the custom bottom-left pivot `(0,0)` (Unity alignment 9); tiles, boundary modules, resource marks, and badges use centered pivots `(0.5,0.5)`. `GridWorldMapper` places cells from their lower-left origin and centers tiles/overlays. `PlacementPreviewView.ApplyFootprintTransform` anchors unrotated bodies at that origin and offsets quarter turns by the declared footprint dimensions. `FactoryView` uses the same helper for placed machines, so the art bounds follow the 2x2 and 3x1 logical footprints without becoming placement authority.

Rendering order is explicit: grid 0, boundary 10, placed machine 100, preview/ghost 200, and world badges 300. These renderers remain on Unity's existing default sorting layer; UI Toolkit content stays in document order. The edge and corner modules use the approved north/top and north-east/top-right source convention with quarter-turn rotation, not mirroring.

### Import settings, metadata, and references — PASS

The imported folder contains exactly 21 PNGs and 21 matching `.meta` files. All 21 GUIDs are valid and unique. The source folder contains 21 SVGs and 21 matching `.meta` files. Every imported image has 64 PPU, sprite/single mode, point filtering, clamp wrapping, mipmaps disabled, alpha transparency enabled, and uncompressed texture settings. Machine/ghost pivots are bottom-left; other pivots are centered. Android and iPhone have no platform override and inherit the uncompressed default. Unity's serialized legacy texture fields remain as described by T014; the tested TextureImporter properties are authoritative.

I independently parsed the integrated scene's catalog: all 21 keys point to the GUID in the matching PNG `.meta`, with zero missing or mismatched entries. `F001SpriteCatalog` requires the same 21 keys, filenames, and dimensions. `F001AssetContractTests` checks the exact inventory and metadata set, valid unique asset GUIDs, RGBA dimensions and visible alpha, import settings, full-rectangle sprite meshes, pivots, the D001/resource presentation keys, and the 581,632-byte decoded RGBA sum.

| Import group | Entries | Canvas dimensions | Pivot |
|---|---:|---|---|
| Grid and boundary modules | 3 | 64x64 | Center |
| D001 machine body, normal/selected | 2 | 128x128 | Bottom-left |
| Alternate machine body, normal/selected | 2 | 192x64 | Bottom-left |
| D001 placement ghosts, valid/invalid | 2 | 128x128 | Bottom-left |
| Alternate placement ghosts, valid/invalid | 2 | 192x64 | Bottom-left |
| Development input/output symbols | 2 | 32x32 | Center |
| State and placement badges | 8 | 64x32 | Center |
| **Total** | **21** |  |  |

The decoded RGBA content is 581,632 bytes (568 KiB). This is a deterministic decoded-pixel estimate, not a Unity Profiler, GPU allocation, build-size, or runtime-residency measurement. Engine/object overhead and platform-specific runtime formats are excluded.

### Zoom readability — FAIL (evidence gap at the supported minimum)

The integrated camera starts at orthographic size 9 and clamps between sizes 2 and 12. For a full-height camera viewport, projected pixels per cell are viewport height divided by twice the orthographic size; an imported 64x32 badge therefore projects to one cell by half a cell.

| Viewport | Cell pixels at size 9 | Cell pixels at max size 12 | Badge pixels at size 12 |
|---|---:|---:|---:|
| 360x640 | 35.56 | 26.67 | 26.67x13.33 |
| 640x360 (rotated) | 20.00 | **15.00** | 15.00x7.50 |
| 390x844 | 46.89 | 35.17 | 35.17x17.58 |
| 844x390 (rotated) | 21.67 | 16.25 | 16.25x8.13 |
| 768x1024 | 56.89 | 42.67 | 42.67x21.33 |
| 1024x768 (rotated) | 42.67 | 32.00 | 32.00x16.00 |
| 1280x720 | 40.00 | 30.00 | 30.00x15.00 |
| 1920x1080 | 60.00 | 45.00 | 45.00x22.50 |

The exact art brief requires boundary and complete footprint-silhouette readability below 16 px/cell, and says that when a status is shown its badge symbol must be retained. The supported 640x360 maximum-zoom-out view is 15 px/cell, so it enters that below-16 condition. A 2x2 body projects to 30x30 pixels and a 3x1 body to 45x15 pixels; these dimensions suggest the large silhouettes remain, but dimensions alone do not establish whether the thin boundary, quarter-turn outline, and status symbol survive Unity's point-sampled scene render at all relevant subpixel alignments. The 64x32 badge is only 15x7.5 pixels there.

No current Play Mode test captures or asserts that rendered case. T014's contact-sheet evidence tests assets at exactly 16/32/64 px per cell, not the integrated scene at its supported 15 px/cell limit. This is a precise acceptance evidence failure, not a demonstrated source/import defect. Required follow-up: inspect the authored scene at 640x360 and orthographic size 12, confirming the factory boundary, both footprint silhouettes, and the retained status symbol without relying on color. If the scene cannot meet that below-16 requirement, reduce maximum orthographic size to at most 11.25 for that viewport (16 px/cell) or obtain an approved scope/contract decision; do not silently alter the camera within this review.

Badge text is required to remain recognizable only when the badge is at least 32x16 pixels. At size 12, this threshold is unmet on 360x640, 640x360, 844x390, and 1280x720; the brief permits text suppression below 16 but does not require text readability below the 32x16 badge threshold. Other listed sizes at max zoom-out meet or exceed 32 px/cell. The scene currently uses a single labeled badge image and does not contain a separate symbol-only variant, so the mandatory 640x360 symbol-retention check must inspect the actual rendered image.

## Validation

- Static inspection: PASS. Twenty-one scene-key-to-meta GUID comparisons passed; all imported GUIDs were unique; PPU, pivot, compression, and mipmap values were inspected across all 21 import metas.
- Play Mode zoom capture: FAIL / not evidenced. Current tests do not capture readability at 640x360, orthographic size 12; the exact required check is stated above.
- Configured command: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1`.
- Unity `6000.6.0f1 (f7f8ed4d1e24)`: Edit Mode PASS, 545/545 passed, 0 failed, 0 skipped; Play Mode PASS, 74/74 passed, 0 failed, 0 skipped.
- Exact result/log directory: `test-results/unity/20260925T143352014Z/` (`EditMode-results.xml`, `EditMode.log`, `PlayMode-results.xml`, `PlayMode.log`).
- The first run in the normal sandbox exited before tests because Unity could not open its Package Manager database and failed local Package Manager IPC. The same configured command then passed in the coordinator-approved elevated context; no cache or configuration was deleted or changed.

## Art Director re-review — zoom readability at 15 px/cell (T066, 2026-09-28)

- **Reviewed baseline**: worktree `issue-130-zoom-readability-evidence` at `fa9e386`, base `origin/main` `0fe299b`.
- **Independence**: this re-review did not author `Assets/IdleFactory/Tests/PlayMode/ZoomReadabilityTests.cs`, the T066 evidence packet, or the standalone rig it builds.
- **Reviewed evidence**: `specs/001-core-game-foundation/reviews/t066-evidence/README.md`, the six captured PNGs in that directory, and `Assets/IdleFactory/Tests/PlayMode/ZoomReadabilityTests.cs` in full (both `[UnityTest]` methods and all assertion helpers).

### Verdict: PASS for ART-F1 (= T041-F1)

The required nearest-supported-zoom evidence gap identified in "Zoom readability — FAIL" above is closed. At 640x360, orthographic size 12 (15 px/cell), the factory boundary, both required footprint silhouettes (D001 2x2 and the alternate 3x1/1x3, including its quarter-turn), and the retained non-color status-badge symbol are all visible in the rendered evidence, at both the default and the sub-cell-offset (`+0.37, +0.21`) camera positions. Per the assignment's decision rule, because the rendered evidence passed, the ART-F1 clamp decision does not apply; `FactoryCameraController`'s maximum orthographic size of 12 is unchanged, and this review does not touch it.

### What I saw in each frame

- `authored-d001-default.png` / `authored-d001-offset.png`: the factory boundary renders as a thin light rectangle clearly separated from the dark background in both frames, including the offset frame where the grid tile lines are also visible as faint vertical bands. The D001 2x2 body is a light square silhouette with a dark double-outline (selection) plus two inset ring primitives, matching the brief's "single square outline plus two inset gears/circles" / "double square outline" description. Above it, a distinct two-tone badge is visible: a blue (selection) block with an internal glyph pattern stacked over a yellow (missing-input) block containing a clearly separate downward-triangle-like icon — this is a real symbol, not a flat color swatch, and it survives the sub-cell offset with the same internal structure intact (verified by cropping and 8x nearest-neighbor magnifying both frames).
- `alternate-horizontal-default.png` / `alternate-horizontal-offset.png`: a 3x1 light rectangle body with three dark inset blocks (matching "long rectangle plus three inset blocks") is visible at anchor (2,6), selected (double outline) with the same two-tone selection/idle badge showing a distinct glyph above it. The 1x3 quarter-turn body is simultaneously visible at anchor (10,2), unselected, confirming both footprint orientations render together in the same frame.
- `alternate-vertical-default.png` / `alternate-vertical-offset.png`: the same pair, with the 1x3 body now selected and showing its own badge with the same distinct glyph, and the 3x1 body still present unselected. The quarter-turn correctly redistributes the three inset port blocks vertically rather than stretching or cropping the horizontal body, i.e. the alternate footprint's second orientation is genuinely rendered, not merely rotated bounds around unchanged art.
- All six frames retain internal, non-flat detail in every silhouette and badge at both camera positions; nothing collapses to a single-color blob at this zoom.

### Judgment on the worker's interpretation (a)

Acceptable. The authored `CoreGameFoundation.unity` scene's `DefinitionCatalogAsset` registers only D001, so the alternate 3x1/1x3 footprint the original review required cannot be placed through the authored scene without an out-of-scope catalog or scene edit. Building a standalone `FactoryView` that reuses the same production sprites (loaded from `Assets/IdleFactory/Art/Development/F001/Imported/`, not fabricated test art), the same `GridWorldMapper`/`FactoryBounds` configuration, and an identically configured camera (world bounds, position, orthographic size, background color) is a faithful, non-fabricated way to exercise the required second silhouette and both orientations without altering the authored scene's catalog scope. The README's framing of this substitution is accurate and the rig's sprite/camera parity with the authored scene is confirmed by direct code reading.

### Judgment on the status symbol (b)

Confirmed a real symbol, not a colored swatch, in all four badge instances captured (authored D001 missing-input badge; alternate 3x1 and 1x3 idle badges, each captured twice). Magnified crops show internally structured glyphs distinct from a flat fill, consistent with the brief's "every status output contains a unique silhouette plus the ASCII label" rule (label legibility itself is correctly not claimed below 16 px/cell, per the brief's own suppression allowance).

### Judgment on the test's strength (c) and subpixel offsets

The test's luminance-mean-delta-from-background and luminance-variance assertions are a reasonable but not airtight proxy: a mean-delta-from-background check by itself would pass for any non-background content regardless of shape, and a variance-above-floor check would pass for detail that doesn't actually resemble the intended symbol. Taken alone, the test proves "non-background, non-flat content is present in the right world-space region," not "the specific approved silhouette/symbol is present." However, combined with my direct visual inspection of all six PNGs above — which does show the actual approved silhouette and badge shapes, not arbitrary noise — the evidence as a whole supports the PASS verdict. Both required sub-cell camera offsets (`(0,0)` and `(+0.37, +0.21)`) were captured and asserted for every scenario (2 for the authored D001 case, 4 for the two alternate-footprint orientations), and I confirmed visually that detail survives the offset in every case, addressing the brief's point-sampling/subpixel-alignment concern.

### Residual non-blocking notes

- The luminance-based assertions alone (without the accompanying visual review recorded here) would not by themselves distinguish the approved silhouette/symbol from an unrelated non-flat artifact; a future strengthening (e.g. comparing against a stored reference crop, or asserting a minimum count of distinct luminance clusters) would make the automated check self-sufficient without requiring a human to view the PNGs each time. Not blocking for T066/ART-F1.
- This re-review, like the evidence packet, does not claim physical-device rendering, GPU-specific point-sampling behavior, or release qualification; only the Editor Play Mode `RenderTexture` capture reviewed here is in scope.
