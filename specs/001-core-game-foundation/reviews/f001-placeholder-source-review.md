# Independent Art Director Source Review: F001 Development Placeholders v0.1

**Issue / work unit**: MAQA Issue #13 / WU13 / T013

**Reviewer role**: Independent Art Director; the reviewer did not produce the SVGs or author the provenance revision or its reapproval

**Reviewed sources**: all 21 SVG candidates and matching `.svg.meta` files at source commit `faf524647274248d85feced234db70ee898a9a0b`, merged on `main` at `f982ff0b646c41565b0db7b02f0204da429652d1`

**Authority**: `docs/art/F001-development-placeholders-v0.1.md` at `ffafce98a2494098071c69463a69493043570bb2`; independent provenance reapproval at `d0dc312a32a2a3d053e3b402ce9726ce51f8de6c`; US1, FR-028--030, SC-005/008, and `contracts/presentation-states.md`

**Governing art document**: Art Bible N/A; `F001-Development-Placeholders v0.1` governs these temporary development placeholders only

**Review date**: 2026-09-18

## Verdict

**PASS**

All 21 source candidates conform sufficiently to proceed to T014 raster and import validation. This PASS approves the reviewed source geometry only. It does not approve PNG output, Unity import settings, prefabs, final art, gameplay, UI, or production use, and it does not make an empirical contrast, grayscale/CVD, raster, device, or `16/32/64` readability claim.

## Deterministic Validation

- Exact inventory: PASS. The source directory contains exactly the 21 approved `.svg` files, no extra SVGs, and exactly 21 one-to-one `.svg.meta` files.
- XML and SVG vocabulary: PASS. All files parse with DTD processing prohibited, declare SVG `1.1`, use the W3C SVG namespace, and contain only `svg`, `metadata`, `desc`, `path`, `rect`, and `circle` elements with the required standard attributes. No `data-*` attributes remain.
- Prohibited content: PASS. No live `text`, font, `image`, raster, link, `href`, script, style URL, filter, mask, animation, `use`, `foreignObject`, or external content is present. All ASCII labels are repository-authored paths.
- Geometry: PASS. Width, height, and `viewBox` match every inventory row; geometry coordinates and stroke widths are integer-valued, apart from the approved ghost fill opacity. The palettes use only the brief's named colors.
- Provenance: PASS. Every SVG truthfully records its exact stable ID, F001 v0.1 and Art Bible N/A status, assigned Asset Artist role, Kilo LLM assistance, GPT-5.6 Sol context, UTC creation date, owner `fjure`, project terms and no public license grant, deterministic repository-patch workflow, Issue #12 assignment locator, and exclusion of third-party, stock, external-font, raster, hosted, and image-generation inputs. No raster transform has yet occurred; that remains T014 work.
- Metas: PASS for source identity only. Every SVG has a parseable matching meta with `fileFormatVersion: 2`; all 21 GUIDs are valid 32-character lowercase hexadecimal values and unique. Existing SVG importer values are defaults and are not approved production settings.
- Corrected-candidate identity: PASS. The source tree is unchanged between `faf524647274248d85feced234db70ee898a9a0b` and the reviewed `f982ff0` branch baseline.

## One-to-One Evidence Matrix

Common result for every row: transparent explicit canvas; top-down orthographic primitive geometry; strict SVG 1.1 vocabulary; common P1/T1 provenance PASS; prohibited-content scan PASS; matching unique meta/GUID PASS. Machine and ghost ports are `none`; all other ports are `N/A`; power is `N/A` throughout. No row introduces port, logistics, power, or inventory meaning.

| Stable ID / exact filename | Type; exact size / viewBox | Footprint, pivot and mapping | State, palette, geometry, non-color/label evidence | Meta GUID | Result |
|---|---|---|---|---|---|
| `F001-GRID-TILE-1X1` / `f001-grid-tile-1x1-v01.svg` | Grid tile; `64x64` / `0 0 64 64` | `1x1`; center; 64 px per cell | Normal; `#64748B`; four 1 px filled edge segments, transparent interior | `a0011000000000000000000000000001` | PASS |
| `F001-GRID-BOUNDARY-EDGE` / `f001-grid-boundary-edge-v01.svg` | Rotatable boundary edge; `64x64` / `0 0 64 64` | No occupancy; center overlay; 90-degree rotation | Normal; `#E5E7EB`; 4 px solid NORTH/TOP edge; clockwise rotation covers E/S/W | `a0011000000000000000000000000002` | PASS |
| `F001-GRID-BOUNDARY-CORNER` / `f001-grid-boundary-corner-v01.svg` | Rotatable boundary corner; `64x64` / `0 0 64 64` | No occupancy; center overlay; 90-degree rotation | Normal; `#E5E7EB`; joined 4 px NORTH-EAST/TOP-RIGHT L; clockwise rotation covers all corners without mirroring | `a0011000000000000000000000000003` | PASS |
| `F001-MACHINE-D001-NORMAL` / `f001-machine-d001-normal-v01.svg` | D001 body; `128x128` / `0 0 128 128` | Exact `2x2`; bottom-left `(0,0)` anchor | Normal; `#E5E7EB` and `#0B1020`; 4 px square outline plus two inset circles, two interior primitives | `a0011000000000000000000000000004` | PASS |
| `F001-MACHINE-D001-SELECTED` / `f001-machine-d001-selected-v01.svg` | Selected D001 body; `128x128` / `0 0 128 128` | Exact `2x2`; bottom-left `(0,0)` anchor | Selected; `#E5E7EB`, `#38BDF8`, `#0B1020`; double square outline plus same two-circle silhouette | `a0011000000000000000000000000005` | PASS |
| `F001-MACHINE-ALT-3X1-NORMAL` / `f001-machine-alt-3x1-normal-v01.svg` | Alternate body; `192x64` / `0 0 192 64` | Exact `3x1`; bottom-left `(0,0)` anchor | Normal; `#E5E7EB` and `#0B1020`; 4 px long outline plus exactly three inset blocks | `a0011000000000000000000000000006` | PASS |
| `F001-MACHINE-ALT-3X1-SELECTED` / `f001-machine-alt-3x1-selected-v01.svg` | Selected alternate body; `192x64` / `0 0 192 64` | Exact `3x1`; bottom-left `(0,0)` anchor | Selected; `#E5E7EB`, `#38BDF8`, `#0B1020`; double long outline plus same three-block silhouette | `a0011000000000000000000000000007` | PASS |
| `F001-GHOST-D001-VALID` / `f001-ghost-d001-valid-v01.svg` | Placement ghost; `128x128` / `0 0 128 128` | Full `2x2`; bottom-left `(0,0)` candidate anchor | Valid; `#4ADE80` and `#0B1020`; all-cell forward hatch at 16 px spacing plus check | `a0011000000000000000000000000008` | PASS |
| `F001-GHOST-D001-INVALID` / `f001-ghost-d001-invalid-v01.svg` | Placement ghost; `128x128` / `0 0 128 128` | Full `2x2`; bottom-left `(0,0)` candidate anchor | Invalid; `#FB923C` and `#0B1020`; all-cell cross-hatch at 16 px spacing plus X | `a0011000000000000000000000000009` | PASS |
| `F001-GHOST-ALT-3X1-VALID` / `f001-ghost-alt-3x1-valid-v01.svg` | Placement ghost; `192x64` / `0 0 192 64` | Full `3x1`; bottom-left `(0,0)` candidate anchor | Valid; `#4ADE80` and `#0B1020`; all-cell forward hatch at 16 px spacing plus check | `a0011000000000000000000000000010` | PASS |
| `F001-GHOST-ALT-3X1-INVALID` / `f001-ghost-alt-3x1-invalid-v01.svg` | Placement ghost; `192x64` / `0 0 192 64` | Full `3x1`; bottom-left `(0,0)` candidate anchor | Invalid; `#FB923C` and `#0B1020`; all-cell cross-hatch at 16 px spacing plus X | `a0011000000000000000000000000011` | PASS |
| `F001-RESOURCE-DEV-INPUT` / `f001-resource-dev-input-v01.svg` | `dev.input` symbol; `32x32` / `0 0 32 32` | No occupancy; center; native UI or `0.5x0.5` at 64 PPU | Normal; `#E5E7EB` and `#0B1020`; downward triangle plus path-authored `IN` | `a0011000000000000000000000000012` | PASS |
| `F001-RESOURCE-DEV-OUTPUT` / `f001-resource-dev-output-v01.svg` | `dev.output` symbol; `32x32` / `0 0 32 32` | No occupancy; center; native UI or `0.5x0.5` at 64 PPU | Normal; `#E5E7EB` and `#0B1020`; upward triangle plus path-authored `OUT` | `a0011000000000000000000000000013` | PASS |
| `F001-BADGE-NORMAL` / `f001-badge-normal-v01.svg` | Status badge; `64x32` / `0 0 64 32` | No occupancy; center overlay | Normal; `#E5E7EB`/`#0B1020`; unique outlined square plus path-authored `NORM` | `a0011000000000000000000000000014` | PASS |
| `F001-BADGE-SELECTED` / `f001-badge-selected-v01.svg` | Status badge; `64x32` / `0 0 64 32` | No occupancy; center overlay | Selected; `#38BDF8`/`#0B1020`; unique double ring plus path-authored `SEL` | `a0011000000000000000000000000015` | PASS |
| `F001-BADGE-PLACEMENT-VALID` / `f001-badge-placement-valid-v01.svg` | Status badge; `64x32` / `0 0 64 32` | No occupancy; center overlay | Valid; `#4ADE80`/`#0B1020`; unique check plus path-authored `OK` | `a0011000000000000000000000000016` | PASS |
| `F001-BADGE-PLACEMENT-INVALID` / `f001-badge-placement-invalid-v01.svg` | Status badge; `64x32` / `0 0 64 32` | No occupancy; center overlay | Invalid; `#FB923C`/`#0B1020`; unique X plus path-authored `NO` | `a0011000000000000000000000000017` | PASS |
| `F001-BADGE-PRODUCING` / `f001-badge-producing-v01.svg` | Status badge; `64x32` / `0 0 64 32` | No occupancy; center overlay | Producing; `#4ADE80`/`#0B1020`; unique play triangle plus path-authored `RUN` | `a0011000000000000000000000000018` | PASS |
| `F001-BADGE-MISSING-INPUT` / `f001-badge-missing-input-v01.svg` | Status badge; `64x32` / `0 0 64 32` | No occupancy; center overlay | Missing input; `#FBBF24`/`#0B1020`; unique empty down-triangle plus path-authored `IN!` | `a0011000000000000000000000000019` | PASS |
| `F001-BADGE-FULL-OUTPUT` / `f001-badge-full-output-v01.svg` | Status badge; `64x32` / `0 0 64 32` | No occupancy; center overlay | Full output; `#F0ABFC`/`#0B1020`; unique barred up-triangle plus path-authored `OUT!` | `a0011000000000000000000000000020` | PASS |
| `F001-BADGE-INACTIVE` / `f001-badge-inactive-v01.svg` | Status badge; `64x32` / `0 0 64 32` | No occupancy; center overlay | Inactive; `#CBD5E1`/`#0B1020`; unique pause bars plus path-authored `OFF` | `a0011000000000000000000000000021` | PASS |

## Art and Readability Assessment

- Top-down orthographic clarity and mapping: PASS at source level. The explicit canvases encode the exact `1x1`, `2x2`, and `3x1` dimensions at 64 source pixels per cell. Machine and ghost geometry uses the full declared footprint and the approved bottom-left anchor intent; overlays and symbols retain center mapping.
- Grid, boundary, and rotation: PASS at source level. The grid uses a 1 px line and the boundary modules use 4 px geometry. The unrotated edge is explicitly NORTH/TOP and the corner NORTH-EAST/TOP-RIGHT; their asymmetric filled geometry supports complete clockwise 90-degree coverage without mirroring.
- Machine noise and selection: PASS. D001 uses two interior circles and the alternate uses three interior blocks, within the maximum-three rule. Selected variants add a second outline without changing their footprint silhouette.
- Placement previews: PASS. Both footprints are fully bounded; valid variants use one forward-hatch family and a check, while invalid variants add the reverse hatch family and an X. Hatch origins remain on the 16 px source grid.
- Resources and excluded meanings: PASS with a downstream interpretation risk. Opposing triangles plus `IN`/`OUT` provide redundant source cues, and descriptions expressly reject machine-port, belt, cable, logistics, power, and inventory meanings. Their arrow-like direction can still be misread in context, so T014 and later UI/Product acceptance must verify placement and labeling rather than treating this source approval as semantic proof.
- Eight state badges: PASS at source level. Each state has a unique shape and path-authored ASCII label; color is redundant. The labels and symbols are simple enough to raster-test but their empirical legibility is not established here.
- `16/32/64` intent: PASS as source intent only. Full footprint and major check/X silhouettes are structurally available for 16 px-per-cell projection; direction symbols and eight distinct badge shapes exist for 32 px; all path labels exist for 64 px. Actual raster survival, contrast, grayscale/CVD distinction, and device/camera readability require T014 evidence.

## Source-Level Risks

1. Several designed paths meet canvas edges: grid and boundary fills terminate at the viewBox; machine/ghost outer strokes resolve exactly to the canvas bounds; ghost hatch strokes cross the edge and are clipped to the full-footprint canvas; badge outer strokes resolve to the canvas bounds. This is consistent with exact no-padding bounds, but T014 must detect missing edge pixels, antialias clipping, seams, or accidental crop after the pinned raster transform.
2. `IN`, `OUT`, and compact badge path labels use 2 px source strokes and tight spacing. Their geometry is present and unambiguous at source scale, but small raster labels may close, merge, or lose punctuation. T014 must inspect the exact 32 px symbols, 64x32 badges, and `16/32/64` projections.
3. The resource triangles intentionally resemble directional arrows. They must remain attached to explicit development input/output storage or controls and never be positioned as ports, belts, flow connections, inventory, or power cues.
4. Transparent bounds are explicit and complete but cannot be proven preserved until rasterization. T014 must confirm exact PNG dimensions, full alpha bounds, and no crop, padding, rescale, or atlas packing.
5. The committed `.svg.meta` files identify the sources, but their current ScriptedImporter defaults include values such as 100 SVG PPU and default alignment/pivot behavior. They are non-production source-import defaults, not evidence for the required PNG import contract.

## Baseline Test Evidence

- Configured command: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-unity.ps1`
- Unity `6000.6.0f1` Edit Mode: PASS, `114/114`.
- Unity `6000.6.0f1` Play Mode: PASS, `25/25`.
- The logs contain non-blocking licensing-token and D3D12 info-query diagnostics but no relevant test or source-review console failure. This suite establishes the committed baseline only; it does not rasterize or accept the F001 candidates.

## Residual T014 Gates

T014 remains blocked until all of the following are available and then independently evidenced:

1. Exact Inkscape CLI `1.4.2` availability, actual build/platform recording, and use of the approved command without silent substitution.
2. Exact transparent RGBA PNG dimensions and raster comparison, including edge/stroke clipping, alpha bounds, path-label survival, and no crop, padding, rescale, or atlas packing.
3. Unity PNG import evidence for 64 PPU where applicable, custom bottom-left machine/ghost pivots, center overlay pivots, logical cell alignment, sorting, lossless compression intent, metas/GUIDs, references, and measured memory.
4. Measured badge-label contrast of at least `4.5:1`, all eight states in grayscale and common color-vision-deficiency simulations, and non-color distinction.
5. Empirical `16/32/64` projected-pixels-per-cell readability at the nearest supported camera zooms across the required screen classes, plus the resource-arrow interpretation check.

Until those gates pass, these files remain temporary reviewed SVG source candidates rather than imported, production-ready, final-art, gameplay, or UI assets.
