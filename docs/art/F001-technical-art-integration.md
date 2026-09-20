# F001 Technical Art Integration

**Status**: Development-only placeholder integration for MAQA Issue #14 / WU14 / T014

**Governing brief**: `F001-Development-Placeholders v0.1`; Art Bible: N/A

These sprites are replaceable validation content, not final art, final UI, gameplay authority, or a production asset family.

## Authority and Tool Evidence

- Brief provenance authorization: `d0dc312a32a2a3d053e3b402ce9726ce51f8de6c`.
- Corrected strict SVG source commit: `faf524647274248d85feced234db70ee898a9a0b`, merged source baseline `f982ff0b646c41565b0db7b02f0204da429652d1`.
- Independent T013 source PASS: `42095bf55964859c14d96973911da39b98b051e7`; provenance locator correction: `d02f04bc0601fb35f8adfba581b476d69f2ffd42`.
- No native Inkscape or Unity MCP tool binding was exposed in this worker session, so no MCP result is claimed. The supported CLI fallbacks were used.
- Rasterizer: `C:\Program Files\Inkscape\bin\inkscape.com`, `Inkscape 1.4.2 (f4327f4, 2025-05-13)`, Windows 11 `10.0.26200.0`, x64.
- Unity importer/test runner: `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`, Unity `6000.6.0f1 (f7f8ed4d1e24)`, through `scripts/test-unity.ps1`.

Each reviewed SVG was transformed once into the repository candidate and once into each of two approved temporary comparison directories with its row's explicit width and height. No area/page crop, padding, rescale, or atlas operation was used:

```text
"C:\Program Files\Inkscape\bin\inkscape.com" <exact-source-path> --export-type=png --export-filename=<exact-output-path> --export-width=<listed-width> --export-height=<listed-height> --export-background-opacity=0
```

The two runs were decoded to ordered RGBA pixel buffers rather than compared as encoded PNG bytes. All 21 matched. Both ordered decoded-pixel manifests produced SHA-256 `c56eb0922fe3284782c8fef547eecb571901b81c13a76b3c331b565ad978cd2f`.

## Imported Contract

All candidates are RGBA PNG, single sprites, 64 PPU, full-rectangle meshes, point filtered, clamp wrapped, mipmaps disabled, alpha-as-transparency enabled, and uncompressed. Android and iPhone have no override and therefore inherit the uncompressed default; Unity's generated, non-overridden Standalone/WebGL schema entries are also set to uncompressed. Machine and ghost bodies use custom normalized pivot `(0,0)`; all other assets use center `(0.5,0.5)`. Unity-generated legacy fields such as `textureFormat: 1`, per-platform `textureFormat: -1`, and empty sprite-sheet data remain because Unity 6000.6 serializes them; the tested `TextureImporter` contract is authoritative.

| Asset ID | Dimensions | Pivot | Decoded RGBA bytes | Presentation key use | GUID |
|---|---:|---|---:|---|---|
| `F001-GRID-TILE-1X1` | `64x64` | center | 16,384 | none | `44c814e5551a3af439672ed747b2919b` |
| `F001-GRID-BOUNDARY-EDGE` | `64x64` | center | 16,384 | none | `ecb62951951fad641bc32c236e80288f` |
| `F001-GRID-BOUNDARY-CORNER` | `64x64` | center | 16,384 | none | `fbfc1eef71652e242ac21e0a8f03a998` |
| `F001-MACHINE-D001-NORMAL` | `128x128` | bottom-left | 65,536 | `D001.asset` | `9ba2f7e8fac41c442af9c3dd02e34d7a` |
| `F001-MACHINE-D001-SELECTED` | `128x128` | bottom-left | 65,536 | none | `466a3c535de2f82459c0374dbaadd8ad` |
| `F001-MACHINE-ALT-3X1-NORMAL` | `192x64` | bottom-left | 49,152 | none | `f1100cefb7e19444f9e5c2d2231861e2` |
| `F001-MACHINE-ALT-3X1-SELECTED` | `192x64` | bottom-left | 49,152 | none | `24003beb923749846896172427249df4` |
| `F001-GHOST-D001-VALID` | `128x128` | bottom-left | 65,536 | none | `a166617e2f90407429a9e7ad6d48725e` |
| `F001-GHOST-D001-INVALID` | `128x128` | bottom-left | 65,536 | none | `61ddf4f6649eca247a32eac612a5f494` |
| `F001-GHOST-ALT-3X1-VALID` | `192x64` | bottom-left | 49,152 | none | `f3ed8e358b7104e48accfb188503e280` |
| `F001-GHOST-ALT-3X1-INVALID` | `192x64` | bottom-left | 49,152 | none | `d693860b0d0808e4297361a5634dd9a3` |
| `F001-RESOURCE-DEV-INPUT` | `32x32` | center | 4,096 | `dev.input.asset` | `8334de9b2ff90f84b96f2dde6eb86891` |
| `F001-RESOURCE-DEV-OUTPUT` | `32x32` | center | 4,096 | `dev.output.asset` | `5d8f28d3048ef9640a1802d39b472a75` |
| `F001-BADGE-NORMAL` | `64x32` | center | 8,192 | none | `81f86b64759a1424da9087ce87850b44` |
| `F001-BADGE-SELECTED` | `64x32` | center | 8,192 | none | `4f7a43baf6051044581a7a3ae40d0ca2` |
| `F001-BADGE-PLACEMENT-VALID` | `64x32` | center | 8,192 | none | `08f9075abc6499541aea8f5e442a5b2d` |
| `F001-BADGE-PLACEMENT-INVALID` | `64x32` | center | 8,192 | none | `cf04a9af03497904795cdf26c6d6ee2d` |
| `F001-BADGE-PRODUCING` | `64x32` | center | 8,192 | none | `af1982bd91d3b30449f30e5dbf8aec32` |
| `F001-BADGE-MISSING-INPUT` | `64x32` | center | 8,192 | none | `3f2144c935462f44caab36718e59f4de` |
| `F001-BADGE-FULL-OUTPUT` | `64x32` | center | 8,192 | none | `f29c00b2cefc3af4bb86ca0c7e6a5479` |
| `F001-BADGE-INACTIVE` | `64x32` | center | 8,192 | none | `5f635d9346f5af645b867e78be070bff` |

Total decoded RGBA footprint is 581,632 bytes (568 KiB), excluding engine/object overhead. This is an exact decoded placeholder total, not a runtime-profiler measurement.

## Presentation Key Map

Keys are the approved stable F001 IDs and resolve one-to-one to imported sprites. They do not replace domain identifiers or convey economy behavior.

| Presentation key | Exact imported path | GUID |
|---|---|---|
| `F001-MACHINE-D001-NORMAL` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-machine-d001-normal-v01.png` | `9ba2f7e8fac41c442af9c3dd02e34d7a` |
| `F001-RESOURCE-DEV-INPUT` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-resource-dev-input-v01.png` | `8334de9b2ff90f84b96f2dde6eb86891` |
| `F001-RESOURCE-DEV-OUTPUT` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-resource-dev-output-v01.png` | `5d8f28d3048ef9640a1802d39b472a75` |

## Sorting and Rotation Convention

The PNGs and definition keys claim no rendering order, occupancy, port, logistics, power, or simulation authority. T016 renderers must remain on the existing default sorting layer and set explicit `SpriteRenderer.sortingOrder` values without changing `ProjectSettings`: grid `0`, boundary `10`, placed machine `100`, placement overlay/ghost `200`, and world-space badges `300`. UI Toolkit badges remain in their UI document order rather than a sprite sorting layer. The unrotated boundary edge is north/top and the unrotated corner is north-east/top-right; clockwise rotations of `0`, `90`, `180`, and `270` degrees cover all sides/corners without mirroring.

## Raster and Readability Evidence

Decoded inspection found every canvas at its exact brief dimension. Grid/boundary alpha bounds retain their intentional edge pixels; machine and badge canvases are intentionally fully opaque; ghost canvases retain approved partial alpha; resources retain transparent canvas pixels. All files have an RGBA color type. Edge-touching strokes and hatch intersections remain present at the exact canvas boundary, with point filtering, clamp wrapping, and full-rectangle sprite meshes preventing importer bleed or tight-mesh trimming. Because T014 has no composed scene, repeated-tile seam behavior remains a T016/T037 renderer-level acceptance check.

Temporary contact sheets outside the repository were inspected against `#111827` at native scale and nearest-neighbor projections:

- At 16 projected pixels per cell, both footprints, selected double outlines, valid checks, invalid X marks, and opposing hatch families remain distinguishable without relying on color. Fine hatching is dense, as expected at the minimum target.
- At 32 projected pixels per cell, grid/boundary modules and resource triangle direction remain visible. Resource path labels become very small at the resulting `16x16` symbol size, so placement beside explicit development input/output controls remains mandatory and arrow shapes must not be used as ports or logistics.
- All eight badges retain distinct symbol silhouettes and recognizable labels at `32x16`; punctuation in `IN!` and `OUT!` survives but is the most fragile detail. At native `64x32`, every label is plainly legible.
- Grayscale plus simple protanopia, deuteranopia, and tritanopia matrix simulations preserve the eight symbol/label distinctions. These desktop matrices are screening evidence, not clinical CVD validation or device evidence; T037 must independently inspect the integrated scene.
- Camera defaults currently allow orthographic sizes `2..12`, but screen pixels per world unit also depend on viewport height. This inspection directly tests 16/32/64 pixels per cell rather than claiming all target viewports hit those values exactly. T016/T017 must verify the nearest supported camera zoom in the assembled view.

Badge text/outline `#0B1020` contrast ratios against the approved fills are: light `#E5E7EB` 15.29:1, selected `#38BDF8` 8.84:1, valid/producing `#4ADE80` 10.86:1, invalid `#FB923C` 8.37:1, missing-input `#FBBF24` 11.34:1, full-output `#F0ABFC` 10.76:1, and inactive `#CBD5E1` 12.75:1. All exceed 4.5:1 by WCAG relative-luminance calculation.

## Validation

- Focused `F001AssetContractTests.cs`: PASS, 3/3, Unity Edit Mode.
- Full configured suite: PASS on 2026-09-19; Edit Mode 117/117 and Play Mode 25/25, with zero skipped tests.
- Unity wrapper rejects relevant compiler/import/console errors; benign licensing-token and D3D12 info diagnostics are filtered or non-fatal as documented by the repository runner.

## Provenance and Limitations

Each PNG is solely the deterministic raster transform of its matching reviewed repository SVG. No source SVG/meta, geometry, font, stock asset, external image, image-generation system, crop, padding, rescale, atlas, material, animation, prefab, scene, or ProjectSettings mutation is part of T014. The source SVG importer's default PPU/pivot settings remain irrelevant because only the PNG candidates are presentation sprites. Final screen/device readability, grid seams in a composed factory, semantic placement of resource arrows, and player-facing development-only labelling require the later independent UI/Product/Art/Technical-Art/QA gates.
