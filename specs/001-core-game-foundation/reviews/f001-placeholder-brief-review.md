# Independent Art Director Review: F001 Development Placeholders v0.1

**Issue / work unit**: MAQA Issue #11 / WU11 / T011

**Reviewer role**: Independent Art Director; the reviewer did not author the brief

**Reviewed artifact**: `docs/art/F001-development-placeholders-v0.1.md` at commit `77bfdd597a2d5a00d04a2e6893f8100e86e46ed6`

**Governing art document**: `Art Bible: N/A`; `F001-Development-Placeholders v0.1` governs these temporary development placeholders only

**Review date**: 2026-09-18

## Verdict

**PASS**

Asset production may begin exactly within **T012 source-only scope**. The approved work is the one-to-one creation of the 21 listed repository-authored SVG 1.1 sources, their Unity-generated `.meta` files, and the required provenance/production handoff. This verdict does not approve final art, imported PNGs, import settings, prefabs, scenes, code, gameplay, UI implementation, logistics or power visuals, animation, VFX, audio, shaders, atlases, external/generated content, or any production-ready Art Bible.

## Inventory Coverage

Legend: `Y` means the row explicitly and acceptably supplies the field. `Paths` also means its source and import paths are unique across the 21-row inventory. `Brief` means `AB0/F001 v0.1`, which explicitly resolves to Art Bible N/A and this governing brief. `Meta` means both the future SVG and PNG require committed `.meta` files.

| Stable asset ID | ID/type | Footprint | Ports | Power | State/cue | Pixels/format | Pivot/map | Paths | Provenance/license | Tool/version | Brief | Meta |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `F001-GRID-TILE-1X1` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-GRID-BOUNDARY-EDGE` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-GRID-BOUNDARY-CORNER` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-MACHINE-D001-NORMAL` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-MACHINE-D001-SELECTED` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-MACHINE-ALT-3X1-NORMAL` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-MACHINE-ALT-3X1-SELECTED` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-GHOST-D001-VALID` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-GHOST-D001-INVALID` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-GHOST-ALT-3X1-VALID` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-GHOST-ALT-3X1-INVALID` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-RESOURCE-DEV-INPUT` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-RESOURCE-DEV-OUTPUT` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-BADGE-NORMAL` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-BADGE-SELECTED` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-BADGE-PLACEMENT-VALID` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-BADGE-PLACEMENT-INVALID` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-BADGE-PRODUCING` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-BADGE-MISSING-INPUT` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-BADGE-FULL-OUTPUT` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |
| `F001-BADGE-INACTIVE` | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y | Y |

The inventory contains exactly 21 rows, 21 unique stable IDs, 21 unique source paths, and 21 unique import paths. Every row supplies all 12 audited field groups without relying on an unstated final-art convention.

## Art Direction Assessment

- **Grid and footprint clarity**: PASS. The top-down orthographic contract removes perspective distortion, fixes one cell to one world unit and 64 source pixels, and uses bottom-left machine/ghost anchors. D001 has complete `2x2` normal, selected, valid-ghost, and invalid-ghost coverage; the alternate fixture has equivalent `3x1` coverage with a distinct long silhouette.
- **Scalability and boundaries**: PASS. The `1x1` repeatable grid tile and rotatable non-occupying edge/corner modules do not bake in D001's `16x16` bounds. Integer coordinates, 64-pixel cell edges, transparent bounds, and prohibition of cropping, padding, or rescaling preserve logical mapping.
- **Resources and excluded systems**: PASS. `dev.input` and `dev.output` use opposing triangle directions plus `IN`/`OUT`. They are explicitly not ports, flow arrows, inventories, or logistics connections. All machine/ghost ports are `none`, other ports are `N/A`, and all power values are `N/A`; the brief prohibits implied logistics and electricity.
- **Required states**: PASS. Normal, selected, placement-valid, placement-invalid, producing, missing-input, full-output, and inactive each have a dedicated symbol/text badge. State composition limits world noise while keeping multiple blocker reasons available to the inspector.
- **Non-color redundancy**: PASS at brief level. Double outlines, hatch direction, check/X, play/pause/barred/empty shapes, direction triangles, and ASCII path labels make color supplemental rather than authoritative.
- **Palette, contrast, and noise intent**: PASS at brief level. The dark backing, named flat colors, strong outline/text intent, one/four-pixel line budgets, three-primitive machine limit, 16-pixel hatch spacing, and badge-count limits are concrete enough for source review. Actual contrast and perceptual performance remain empirical gates below.
- **Scale targets**: PASS. The brief declares distinct required readability at projected `16`, `32`, and `64` pixels per cell, including low-zoom behavior below `16`, without changing the 64 PPU source contract.
- **Temporary status and exclusions**: PASS. The title, scope, directory, scenario-label rule, naming rules, acceptance checklist, and prohibited-scope section consistently identify replaceable development-only content and do not establish final style, controls, balance, machine identity, logistics, power, animation, audio, or production polish.
- **Reproducibility**: PASS at brief level. Plain SVG/XML constraints, exact paths and dimensions, repository-authored geometry, ASCII labels converted to paths, prohibited external dependencies, pinned Inkscape transform, and contributor/date/commit/ownership evidence form an auditable source-to-import recipe.
- **Handoff chain**: PASS. The brief preserves the strict `T011 -> T012 -> T013 -> T014` sequence. T012 is source-only; T013 independently approves each produced source; T014 alone performs the reviewed raster transform and Unity integration. No later approval is inferred from this T011 verdict.

## Mandatory Downstream Gates

These are not T012 brief blockers, but each is a hard gate at the stated boundary:

1. **Rasterizer capability before T014**: `inkscape` 1.4.2 is not discoverable on the current `PATH`. T012 may author plain SVG/XML, but T014 must not rasterize, substitute a tool/version, or begin import work until Inkscape CLI 1.4.2 is available and its actual build/platform is recorded, or the brief is revised and independently approved.
2. **Actual ownership before T013 PASS**: T012 must record the contributor, UTC creation date, source commit, repository-owner copyright, and confirmation that no third-party or generated source was incorporated. The repository has no root license, so no broader public license may be claimed. T013 must verify this evidence against every produced SVG.
3. **Canonical boundary-module orientation before T014**: T012's handoff must explicitly identify the unrotated perimeter side represented by `F001-GRID-BOUNDARY-EDGE` and the unrotated corner represented by `F001-GRID-BOUNDARY-CORNER`. T013 must verify those source orientations and confirm that 90-degree rotations cover all required sides/corners. T014 must record and test the matching Unity rotation convention; no implicit or mirrored convention is approved.
4. **Source conformance before T013 PASS**: T013 must compare all 21 SVGs one-to-one with their rows, including viewBox dimensions, bounds, integer geometry, palettes, silhouette/cue construction, path-only ASCII labels, noise limits, absence of embedded/external content, and development-only purpose.
5. **Empirical accessibility/readability before integration acceptance**: T014 and later Play Mode/manual acceptance must measure badge label contrast at `>=4.5:1`, inspect all eight states in grayscale and common color-vision-deficiency simulations, and prove non-color distinction and the `16/32/64` projected-pixels-per-cell targets at the nearest supported camera zooms. Failure blocks integration even if source geometry and palette match the brief.
6. **Import and mapping before T014 PASS**: Exact RGBA PNG dimensions, 64 PPU where applicable, pivots, transparent bounds, cell alignment, sorting, lossless import/compression, references, GUIDs, memory observations, and every file/directory `.meta` must be recorded and tested. T011 approves none of these future results.

## Baseline Evidence

- Full configured Unity suite: PASS on 2026-09-18.
- Edit Mode: `114/114` passed.
- Play Mode: `1/1` passed.
- The suite establishes the committed dependency baseline only; it contains no produced F001 source/import acceptance evidence and does not replace T013 or T014 review.

## Blocking Findings

None for starting T012 within its source-only boundary.
