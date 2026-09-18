# F001-Development-Placeholders v0.1

**Status**: Pre-production brief for independent approval

**Scope**: Temporary, development-only geometric placeholders for Feature 001

**Art Bible**: N/A — F001-Development-Placeholders v0.1 governs development placeholders

**Unity target**: 6000.6.0f1; orthographic `SpriteRenderer` factory view and UI Toolkit overlay

**Target validation classes**: `360x640`, `390x844`, `768x1024` and rotated equivalents; desktop `1280x720` and `1920x1080`

This brief authorizes no final art direction. Every listed output is replaceable validation content and must be visibly identified as development-only wherever it appears in the development scenario. The inventory supports placement, footprint, selection, production-state, and inspection feedback without defining logistics, electricity, final controls, final balance, or final presentation.

## Production Contract

### Coordinate and pixel system

- Use a top-down orthogonal grid with no perspective distortion. One logical `1x1` cell is one Unity world unit and exactly `64x64` source/import pixels at `64` pixels per unit (PPU).
- Use integer SVG coordinates and snap footprint edges to multiples of 64 pixels. Machine and placement-preview pivots are custom bottom-left pivots at normalized `(0,0)`, which maps the sprite origin to the logical anchor cell's bottom-left corner.
- Grid modules, resource symbols, and badges use a center pivot at normalized `(0.5,0.5)`. Boundary modules are visual overlays, not occupied cells; place one centered module per perimeter cell and rotate edge/corner modules in 90-degree increments.
- D001 is fixed at `2x2` cells. `F001-MACHINE-ALT-3X1` is approved only as the alternate `3x1` footprint-validation fixture required by US1.7/FR-031. It supplies no production, logistics, balance, or permanent content decision.
- SVG source uses an explicit pixel-sized `viewBox`, transparent background, flat solid fills/strokes, and no embedded raster image, linked file, external font, filter, mask, animation, or script. Construct each listed ASCII label as repository-authored vector paths rather than live font text.
- Future imported candidates are lossless transparent RGBA PNG files at the exact listed pixel dimensions. Preserve hard edges; do not crop transparent bounds, rescale, pad, or pack into an atlas for F001 v0.1.

### Reproducible source and transform

Each future source file is a repository-authored SVG 1.1 geometric drawing. `T1` means: author deterministic plain SVG/XML using the W3C SVG 1.1 format, either directly or with Kilo Code language-model assistance limited to textual authoring of the approved primitive geometry, then rasterize with Inkscape CLI `1.4.2` using:

```text
inkscape <exact-source-path> --export-type=png --export-filename=<exact-import-path> --export-width=<listed-width> --export-height=<listed-height>
```

The Asset Artist must record the actual Inkscape build/platform used. A different tool or version requires a brief revision or an explicit independent review before production; it must not be substituted silently. Permitted Kilo Code assistance is deterministic textual authoring assistance, not an external or image-generation asset source. Every assisted SVG must truthfully record the assigned repository agent role, language model and context or tool version, UTC creation date, repository-owner direction and ownership, project terms and absence of a public license grant, a stable assignment and source-commit locator, and all transformations. Assisted sources require independent source review.

`P1` provenance/license means: repository-authored primitive paths and shapes created specifically for this repository, with no external asset source incorporated. It does not assert unaided human authorship. Any Kilo Code language-model or other permitted tool assistance must be disclosed truthfully with the `T1` metadata; copyright ownership is recorded as the repository owner (`fjure`), and use is governed by the repository owner's project terms. Because the repository currently has no root license file, no broader public license grant may be claimed.

Every future `.svg` and `.png` below is under `Assets/` and therefore requires its own committed Unity `.meta` file. New directories also require Unity-generated `.meta` files. This brief does not claim that any listed file, directory, GUID, or `.meta` file already exists.

### Shape language and palette intent

- Build all silhouettes from rectangles, straight lines, circles, triangles, and 45-degree hatching. Use square outside corners and slightly inset interior geometry so occupied-cell boundaries stay unambiguous.
- Use an intended dark factory backing of `#111827`. Primary light geometry is `#E5E7EB`; strong outline/text is `#0B1020`; grid is `#64748B`; selection is `#38BDF8`; placement-valid/producing is `#4ADE80`; placement-invalid is `#FB923C`; missing-input is `#FBBF24`; full-output is `#F0ABFC`; inactive is `#CBD5E1`.
- Color is supplemental. Every status output contains a unique silhouette plus the ASCII label listed in the inventory. Selected machines use a double outline; valid ghosts use forward hatching and a check; invalid ghosts use cross-hatching and an X. Resource direction is redundant through `IN` plus a downward triangle and `OUT` plus an upward triangle.
- Technical Art must measure badge label contrast against its fill and target at least `4.5:1`. It must also inspect the eight states in grayscale and common color-vision-deficiency simulations; failure of non-color distinction blocks integration even when palette colors differ.
- Limit visual noise to a 1-pixel grid line, a 4-pixel factory-boundary line, a 4-pixel machine outer line, at most three interior machine primitives, and 16-pixel ghost hatch spacing at source scale. Show at most one placement badge during preview and at most one production badge plus one selection badge on a placed machine.

### Zoom and readability targets

- At a projected `16` screen pixels per logical cell, the factory boundary, complete `2x2`/`3x1` footprint silhouettes, selected double outline, and valid/invalid check/X geometry must remain distinguishable without color.
- At `32` screen pixels per cell, grid cells, resource direction symbols, and all eight badge symbols must be distinguishable. Badge text must remain recognizable when the badge is at least `32x16` projected pixels.
- At `64` screen pixels per cell, all ASCII badge labels must be plainly legible and the 1-pixel source grid must not merge with machine outlines.
- Below `16` screen pixels per cell, only factory boundary and footprint silhouette readability is required; the presentation may suppress the fine grid and badge text but must retain the badge symbol when a status is shown.
- T015 must document the actual camera limits. T014 and Play Mode acceptance must test these projection targets at the nearest supported zoom values without changing source scale, PPU, or logical footprint mapping.

## Complete Inventory

All 21 outputs are temporary/development-only and are governed uniformly by the `P1` provenance and `T1` authoring/transform requirements above, including truthful assistance disclosure and independent source review. `AB0/F001 v0.1` means `Art Bible: N/A` with this brief as the governing version. `Required both` in the Meta column means both the future source SVG and future imported PNG require committed `.meta` files.

| Stable asset ID | Type | Logical footprint | Ports | Power | Required visual state / non-color cue | Source pixels; imported display/cell mapping | Format | Pivot/cell mapping | Exact future source path | Exact future imported path | Provenance / license | Tool/version | Art Bible / brief | Meta |
|---|---|---:|---|---|---|---|---|---|---|---|---|---|---|---|
| `F001-GRID-TILE-1X1` | Grid tile | `1x1` | `N/A` | `N/A` | `normal`; four-sided 1 px cell line | `64x64`; 64 PPU = `1x1` cell | SVG 1.1 -> RGBA PNG | Center; tile center to cell center | `Assets/IdleFactory/Art/Development/F001/Source/f001-grid-tile-1x1-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-grid-tile-1x1-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-GRID-BOUNDARY-EDGE` | Rotatable boundary edge module | `N/A` | `N/A` | `N/A` | `normal`; 4 px solid outer edge | `64x64`; one centered overlay per perimeter cell | SVG 1.1 -> RGBA PNG | Center; rotate in 90-degree increments, no occupancy | `Assets/IdleFactory/Art/Development/F001/Source/f001-grid-boundary-edge-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-grid-boundary-edge-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-GRID-BOUNDARY-CORNER` | Rotatable boundary corner module | `N/A` | `N/A` | `N/A` | `normal`; joined 4 px L corner | `64x64`; one centered overlay per boundary corner cell | SVG 1.1 -> RGBA PNG | Center; rotate in 90-degree increments, no occupancy | `Assets/IdleFactory/Art/Development/F001/Source/f001-grid-boundary-corner-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-grid-boundary-corner-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-MACHINE-D001-NORMAL` | D001 machine body | `2x2` | `none` | `N/A` | `normal`; single square outline plus two inset gears/circles | `128x128`; 64 PPU = `2x2` cells | SVG 1.1 -> RGBA PNG | Bottom-left `(0,0)` at logical anchor | `Assets/IdleFactory/Art/Development/F001/Source/f001-machine-d001-normal-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-machine-d001-normal-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-MACHINE-D001-SELECTED` | D001 selected machine body | `2x2` | `none` | `N/A` | `selected`; double square outline plus same body silhouette | `128x128`; 64 PPU = `2x2` cells | SVG 1.1 -> RGBA PNG | Bottom-left `(0,0)` at logical anchor | `Assets/IdleFactory/Art/Development/F001/Source/f001-machine-d001-selected-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-machine-d001-selected-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-MACHINE-ALT-3X1-NORMAL` | Alternate footprint-only machine body | `3x1` | `none` | `N/A` | `normal`; long rectangle plus three inset blocks | `192x64`; 64 PPU = `3x1` cells | SVG 1.1 -> RGBA PNG | Bottom-left `(0,0)` at logical anchor | `Assets/IdleFactory/Art/Development/F001/Source/f001-machine-alt-3x1-normal-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-machine-alt-3x1-normal-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-MACHINE-ALT-3X1-SELECTED` | Alternate selected footprint-only machine body | `3x1` | `none` | `N/A` | `selected`; double long-rectangle outline plus same body silhouette | `192x64`; 64 PPU = `3x1` cells | SVG 1.1 -> RGBA PNG | Bottom-left `(0,0)` at logical anchor | `Assets/IdleFactory/Art/Development/F001/Source/f001-machine-alt-3x1-selected-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-machine-alt-3x1-selected-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-GHOST-D001-VALID` | D001 placement ghost/overlay | `2x2` | `none` | `N/A` | `placement-valid`; all four cells, forward hatch, check silhouette | `128x128`; 64 PPU = `2x2` cells | SVG 1.1 -> RGBA PNG | Bottom-left `(0,0)` at candidate anchor | `Assets/IdleFactory/Art/Development/F001/Source/f001-ghost-d001-valid-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-ghost-d001-valid-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-GHOST-D001-INVALID` | D001 placement ghost/overlay | `2x2` | `none` | `N/A` | `placement-invalid`; all four cells, cross-hatch, X silhouette | `128x128`; 64 PPU = `2x2` cells | SVG 1.1 -> RGBA PNG | Bottom-left `(0,0)` at candidate anchor | `Assets/IdleFactory/Art/Development/F001/Source/f001-ghost-d001-invalid-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-ghost-d001-invalid-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-GHOST-ALT-3X1-VALID` | Alternate placement ghost/overlay | `3x1` | `none` | `N/A` | `placement-valid`; all three cells, forward hatch, check silhouette | `192x64`; 64 PPU = `3x1` cells | SVG 1.1 -> RGBA PNG | Bottom-left `(0,0)` at candidate anchor | `Assets/IdleFactory/Art/Development/F001/Source/f001-ghost-alt-3x1-valid-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-ghost-alt-3x1-valid-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-GHOST-ALT-3X1-INVALID` | Alternate placement ghost/overlay | `3x1` | `none` | `N/A` | `placement-invalid`; all three cells, cross-hatch, X silhouette | `192x64`; 64 PPU = `3x1` cells | SVG 1.1 -> RGBA PNG | Bottom-left `(0,0)` at candidate anchor | `Assets/IdleFactory/Art/Development/F001/Source/f001-ghost-alt-3x1-invalid-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-ghost-alt-3x1-invalid-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-RESOURCE-DEV-INPUT` | `dev.input` resource symbol | `N/A` | `N/A` | `N/A` | `normal`; downward triangle plus `IN` | `32x32`; native `32x32` UI pixels or 64 PPU = `0.5x0.5` world unit | SVG 1.1 -> RGBA PNG | Center; no cell occupancy | `Assets/IdleFactory/Art/Development/F001/Source/f001-resource-dev-input-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-resource-dev-input-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-RESOURCE-DEV-OUTPUT` | `dev.output` resource symbol | `N/A` | `N/A` | `N/A` | `normal`; upward triangle plus `OUT` | `32x32`; native `32x32` UI pixels or 64 PPU = `0.5x0.5` world unit | SVG 1.1 -> RGBA PNG | Center; no cell occupancy | `Assets/IdleFactory/Art/Development/F001/Source/f001-resource-dev-output-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-resource-dev-output-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-BADGE-NORMAL` | Status symbol/text badge | `N/A` | `N/A` | `N/A` | `normal`; outlined square plus `NORM` | `64x32`; native UI size or 64 PPU = `1x0.5` world unit | SVG 1.1 -> RGBA PNG | Center; presentation overlay, no occupancy | `Assets/IdleFactory/Art/Development/F001/Source/f001-badge-normal-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-badge-normal-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-BADGE-SELECTED` | Status symbol/text badge | `N/A` | `N/A` | `N/A` | `selected`; double ring plus `SEL` | `64x32`; native UI size or 64 PPU = `1x0.5` world unit | SVG 1.1 -> RGBA PNG | Center; presentation overlay, no occupancy | `Assets/IdleFactory/Art/Development/F001/Source/f001-badge-selected-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-badge-selected-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-BADGE-PLACEMENT-VALID` | Status symbol/text badge | `N/A` | `N/A` | `N/A` | `placement-valid`; check plus `OK` | `64x32`; native UI size or 64 PPU = `1x0.5` world unit | SVG 1.1 -> RGBA PNG | Center; presentation overlay, no occupancy | `Assets/IdleFactory/Art/Development/F001/Source/f001-badge-placement-valid-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-badge-placement-valid-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-BADGE-PLACEMENT-INVALID` | Status symbol/text badge | `N/A` | `N/A` | `N/A` | `placement-invalid`; X plus `NO` | `64x32`; native UI size or 64 PPU = `1x0.5` world unit | SVG 1.1 -> RGBA PNG | Center; presentation overlay, no occupancy | `Assets/IdleFactory/Art/Development/F001/Source/f001-badge-placement-invalid-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-badge-placement-invalid-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-BADGE-PRODUCING` | Status symbol/text badge | `N/A` | `N/A` | `N/A` | `producing`; play triangle plus `RUN` | `64x32`; native UI size or 64 PPU = `1x0.5` world unit | SVG 1.1 -> RGBA PNG | Center; presentation overlay, no occupancy | `Assets/IdleFactory/Art/Development/F001/Source/f001-badge-producing-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-badge-producing-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-BADGE-MISSING-INPUT` | Status symbol/text badge | `N/A` | `N/A` | `N/A` | `missing-input`; empty down-triangle plus `IN!` | `64x32`; native UI size or 64 PPU = `1x0.5` world unit | SVG 1.1 -> RGBA PNG | Center; presentation overlay, no occupancy | `Assets/IdleFactory/Art/Development/F001/Source/f001-badge-missing-input-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-badge-missing-input-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-BADGE-FULL-OUTPUT` | Status symbol/text badge | `N/A` | `N/A` | `N/A` | `full-output`; barred up-triangle plus `OUT!` | `64x32`; native UI size or 64 PPU = `1x0.5` world unit | SVG 1.1 -> RGBA PNG | Center; presentation overlay, no occupancy | `Assets/IdleFactory/Art/Development/F001/Source/f001-badge-full-output-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-badge-full-output-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |
| `F001-BADGE-INACTIVE` | Status symbol/text badge | `N/A` | `N/A` | `N/A` | `inactive`; pause bars plus `OFF` | `64x32`; native UI size or 64 PPU = `1x0.5` world unit | SVG 1.1 -> RGBA PNG | Center; presentation overlay, no occupancy | `Assets/IdleFactory/Art/Development/F001/Source/f001-badge-inactive-v01.svg` | `Assets/IdleFactory/Art/Development/F001/Imported/f001-badge-inactive-v01.png` | `P1` | `T1` | `AB0/F001 v0.1` | Required both |

## State Composition Rules

- Render exactly one normal or selected machine body for a placed fixture. Selection adds `F001-BADGE-SELECTED`; lack of selection does not require the normal badge in the world, but `F001-BADGE-NORMAL` remains available for state-matrix and inspector validation.
- A placement preview renders one complete valid or invalid ghost matching the candidate footprint and its matching placement badge. Never use the D001 `2x2` ghost for the alternate `3x1` fixture or vice versa.
- Production state is one of producing, missing-input, full-output, or inactive. Render the corresponding badge; when multiple blockers exist, the inspector must expose all reasons even though the world view shows one primary badge.
- Resource symbols label explicit development input/output storage or controls. They are not connection points, item flow arrows, inventories, or machine ports.
- Every machine and ghost has ports `none`; every other inventory item has ports `N/A`. Power is `N/A` for every item. No mark, edge, color, badge, or symbol may imply a logistics connector, belt direction, cable, power socket, powered/unpowered state, or electricity mechanic.

## Naming and Version Rules

- Stable IDs are uppercase `F001-<FAMILY>-<PURPOSE>-<STATE>` identifiers exactly as listed. File names are lowercase kebab-case, start with `f001-`, and end in `-v01` before the extension.
- Source/import pairs share the same base file name. Do not overwrite a reviewed output with materially different geometry. Revise this brief and increment its version first; retain provenance for superseded files until migration is approved.
- Do not rename an asset after references or GUIDs exist. Technical Art records the generated GUID, importer settings, sorting assignment, compression, dimensions, PPU, pivot, and memory observation in the T014 integration evidence.
- The `Development/F001` directory and all user-facing scenario labels must continue to communicate temporary/development-only status. F001 IDs and measurements are not final game-content identifiers or balance promises.

## Acceptance Checklist

- [ ] Inventory contains exactly 21 stable outputs and every row has type, footprint or `N/A`, ports `none/N/A`, power `N/A`, state, exact pixels, format, pivot/cell mapping, exact source/import paths, provenance/license, tool/version, governing brief, and `.meta` requirement.
- [ ] D001 machine and ghost occupy exactly `2x2` cells; alternate validation machine and ghost occupy exactly `3x1`; all use bottom-left anchor pivots without padding or cropping.
- [ ] Grid tile makes `1x1` cells readable; repeatable boundary modules support different rectangular area sizes without baking D001's `16x16` dimensions into art.
- [ ] Normal, selected, placement-valid, placement-invalid, producing, missing-input, full-output, and inactive each have one reviewable non-color symbol/text badge.
- [ ] `dev.input` and `dev.output` are distinguishable by direction shape and ASCII text without color and do not read as ports or logistics.
- [ ] Valid/invalid previews cover every footprint cell and remain distinguishable through hatch direction and check/X at the declared zoom targets.
- [ ] Source SVGs contain only repository-authored primitive geometry with no external asset source incorporated; each records the complete `P1`/`T1` provenance, assistance, ownership, locator, transformation, and actual rasterizer evidence required above.
- [ ] PNG candidates match exact dimensions and source bounds; Technical Art verifies 64 PPU, pivots, cell alignment, sorting, lossless import intent, references, memory, and all required `.meta` files.
- [ ] Technical Art verifies the 16/32/64 projected-pixels-per-cell targets, grayscale/non-color distinction, label contrast, responsive classes, and nearest supported camera zoom values.
- [ ] All scenario-facing use is labelled development-only; no output is represented as final art, final UX, production-ready content, or an approved Art Bible.

## Prohibited Scope

Do not create assets, Unity imports, GUIDs, `.meta` files, prefabs, scenes, materials, shaders, animation, VFX, audio, atlas rules, final UI styling, logistics visuals, power visuals, new packages, generator services, or import-setting changes as part of this brief. Do not introduce final art lore, perspective, lighting, texture, building identity, or balance implications. ComfyUI or other image-generation models, Blender, hosted asset generators, stock or third-party art, external icons or fonts, raster inputs, linked content, unapproved scripts or workflows, silent tool substitution, and undisclosed assistance are not approved for F001 v0.1.

## Production and Review Handoff

1. **T011 independent brief review**: An Art Director who did not author this brief compares all 21 rows, shape/readability rules, temporary labeling, provenance, and prohibited scope against US1, FR-028–030, SC-005/008, the presentation-state contract, and the plan. Asset production remains blocked until that review records PASS.
2. **T012 source production**: The Asset Artist creates only the approved SVG paths in the inventory, records complete `P1`/`T1` evidence and actual tool details, and adds Unity-generated `.meta` files. It must not create imported PNGs, code, scenes, prefabs, settings, or extra visual families.
3. **T013 independent source review**: An Art Director compares every produced source one-to-one against its approved row, dimensions, geometry, palette intent, non-color cue, noise limits, complete provenance and assistance record, ownership record, and development-only purpose. Import remains blocked until PASS.
4. **T014 Technical Art integration**: The Technical Artist performs the exact reviewed raster transform, creates only the listed PNG candidates and their `.meta` files, applies and records 64 PPU/pivots/sorting/compression, resolves references, measures memory, and produces Edit Mode plus zoom/readability evidence. Import settings are not authorized by this T010 document change itself.
5. **Later acceptance**: UI/UX, Product, Art Direction, Technical Art, and Test/QA independently verify the committed integrated result across required states, touch/desktop journeys, responsive classes, footprint alignment, view recreation, and development-only labeling. Source approval does not by itself make the outputs final or production art.

This provenance revision alone does not approve the current sources. They remain blocked pending independent Art reapproval and T012 QA for MAQA Issue #12.

## Traceability

- `specs/001-core-game-foundation/spec.md`: US1.1–7; FR-028–030; SC-005/008; A-004.
- `specs/001-core-game-foundation/plan.md`: Presentation, interaction and placeholder assets, especially lines 192–202.
- `specs/001-core-game-foundation/contracts/presentation-states.md`: factory view states and placeholder visual brief.
- `specs/001-core-game-foundation/quickstart.md`: placement, navigation, zoom/readability, responsive/accessibility, and F001 evidence gates.
- `AGENTS.md`: Art Director, Asset Artist, Technical Artist, UI/UX, Product, QA, provenance, and review-chain rules.
