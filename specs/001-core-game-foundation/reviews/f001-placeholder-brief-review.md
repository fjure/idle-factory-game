# Independent Art Director Review: F001 Development Placeholders v0.1

**Issue / work unit**: Original MAQA Issue #11 / WU11 / T011; provenance-revision reapproval under MAQA Issue #12 / WU12 / T012

**Reviewer role**: Independent Art Director; the reviewer did not author the original brief, provenance revision commit `ffafce98a2494098071c69463a69493043570bb2`, or source commit `8dbb53b3e03902787e373ff4350a440c2015f31a`

**Reviewed artifact**: `docs/art/F001-development-placeholders-v0.1.md` at provenance revision commit `ffafce98a2494098071c69463a69493043570bb2`; original T011 baseline reviewed at `77bfdd597a2d5a00d04a2e6893f8100e86e46ed6`

**Governing art document**: `Art Bible: N/A`; `F001-Development-Placeholders v0.1` governs these temporary development placeholders only

**Review date**: 2026-09-18

## Verdict

**PASS**

The revised brief safely authorizes deterministic Kilo Code language-model assistance limited to textual authoring of the already-approved primitive SVG/XML geometry, provided every assisted source carries the complete truthful disclosure and independent review required by `P1`/`T1`. It continues to prohibit image-generation and external, stock, third-party, raster, font, linked, hosted, unapproved, silently substituted, or undisclosed inputs.

This PASS resolves only the provenance authorization conflict identified on Issue #12. It does not approve the SVG sources or claim T013 source approval, does not waive strict SVG 1.1 validity, and does not authorize final art, imported PNGs, import settings, prefabs, scenes, code, gameplay, UI implementation, logistics or power visuals, animation, VFX, audio, shaders, atlases, or any production-ready Art Bible. The current sources remain blocked until all `data-*` attributes are removed and T012 QA, Product, Architecture, Technical Art, and T013 independently pass.

## Provenance-Revision Reapproval

**PASS**

Exact rationale:

- The human decision recorded by repository owner `fjure` on Issue #12 explicitly permits deterministic text-authored, LLM-assisted primitive SVG/XML when assistance, model/context, contributor role, ownership, source locator, and excluded inputs are fully disclosed.
- Commit `ffafce98a2494098071c69463a69493043570bb2` encodes that decision narrowly in `T1` and `P1`: assistance is limited to textual authoring of approved primitive geometry and is not treated as an external or image-generation asset source.
- The revision requires the assigned repository agent role, language model and context/tool version, UTC creation date, repository-owner direction and ownership, project terms and lack of public license grant, stable assignment/source locator, transformations, and independent source review. It prohibits silent provenance changes or tool substitution.
- The prohibited-scope language continues to reject ComfyUI and other image-generation models, Blender, hosted asset generators, stock/third-party art, external icons/fonts, raster inputs, linked content, unapproved scripts/workflows, and undisclosed assistance.
- The original specification, plan, and presentation-state contract require repository-owned geometric placeholders and prohibit generated or licensed external art; they do not require unaided human typing. The revised policy therefore preserves product and presentation intent without expanding the approved geometry, inventory, visual language, or production boundary.
- Inspection of all 21 SVG metadata records at source commit `8dbb53b3e03902787e373ff4350a440c2015f31a` confirms consistent disclosure of Kilo LLM assistance, GPT-5.6 Sol context, the assigned Asset Artist role, owner `fjure`, no public license grant, the Issue #12 branch locator, and exclusion of third-party, stock, external-font, raster, hosted, and image-generation inputs. This confirms that the policy can be applied truthfully; it is not source approval.

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
- **Reproducibility and provenance**: PASS at brief level. Deterministic plain SVG/XML constraints, exact paths and dimensions, repository-authored primitive geometry, path-authored ASCII labels, prohibited external/image-generation dependencies, pinned Inkscape transform, complete assistance/model/context/contributor/date/locator/ownership/terms/transform evidence, and mandatory independent source review form an auditable source-to-import recipe. Disclosed Kilo Code textual assistance is compatible with this recipe and must not be described as unaided authorship or concealed as an unspecified source.
- **Handoff chain**: PASS. The brief preserves the strict `T011 -> T012 -> T013 -> T014` sequence. This reapproval changes provenance authorization only. T012 remains source-only and subject to independent QA, Product, Architecture, and Technical-Art review; T013 independently approves each produced source; T014 alone performs the reviewed raster transform and Unity integration after source approval. No source, import, or later acceptance is inferred from this T011 reapproval.

## Mandatory Downstream Gates

These are not T012 brief blockers, but each is a hard gate at the stated boundary:

1. **Rasterizer capability before T014**: `inkscape` 1.4.2 is not discoverable on the current `PATH`. T012 may author plain SVG/XML, but T014 must not rasterize, substitute a tool/version, or begin import work until Inkscape CLI 1.4.2 is available and its actual build/platform is recorded, or the brief is revised and independently approved.
2. **Truthful provenance and ownership before T013 PASS**: Every assisted SVG must record the assigned contributor role, Kilo Code language-model and context/tool version, UTC creation date, repository-owner direction and copyright ownership, project terms and absence of a public license grant, stable assignment/source locator, all transformations, and confirmation that no external, third-party, stock, raster, font, linked, hosted, or image-generation source was incorporated. Disclosed deterministic textual assistance is permitted and must not be rejected merely for being LLM-assisted. T012 QA, Product, Architecture, Technical Art, and T013 must verify this evidence against all 21 SVGs; the repository has no root license, so no broader public license may be claimed.
3. **Canonical boundary-module orientation before T014**: T012's handoff must explicitly identify the unrotated perimeter side represented by `F001-GRID-BOUNDARY-EDGE` and the unrotated corner represented by `F001-GRID-BOUNDARY-CORNER`. T013 must verify those source orientations and confirm that 90-degree rotations cover all required sides/corners. T014 must record and test the matching Unity rotation convention; no implicit or mirrored convention is approved.
4. **Source conformance before T013 PASS**: T013 must compare all 21 SVGs one-to-one with their rows, including strict SVG 1.1 validity, viewBox dimensions, bounds, integer geometry, palettes, silhouette/cue construction, path-only ASCII labels, noise limits, complete truthful assistance provenance, absence of embedded/external/image-generated content, and development-only purpose. At commit `8dbb53b3e03902787e373ff4350a440c2015f31a`, all 21 SVGs contain the non-SVG-1.1 `data-asset-id` attribute and 10 also contain `data-label`; all `data-*` attributes must be removed before the source candidate can pass.
5. **Empirical accessibility/readability before integration acceptance**: T014 and later Play Mode/manual acceptance must measure badge label contrast at `>=4.5:1`, inspect all eight states in grayscale and common color-vision-deficiency simulations, and prove non-color distinction and the `16/32/64` projected-pixels-per-cell targets at the nearest supported camera zooms. Failure blocks integration even if source geometry and palette match the brief.
6. **Import and mapping before T014 PASS**: Exact RGBA PNG dimensions, 64 PPU where applicable, pivots, transparent bounds, cell alignment, sorting, lossless import/compression, references, GUIDs, memory observations, and every file/directory `.meta` must be recorded and tested. T011 approves none of these future results.

## Baseline Evidence

- Full configured Unity suite: PASS on 2026-09-18.
- Edit Mode: `114/114` passed.
- Play Mode: `1/1` passed.
- The suite establishes the committed dependency baseline only; it contains no produced F001 source/import acceptance evidence and does not replace T013 or T014 review.

## Blocking Findings

The provenance authorization conflict is resolved by this PASS, but the current source candidate is not approved and remains blocked:

1. Remove every `data-*` attribute from all affected SVGs so the files meet the brief's strict SVG 1.1 requirement. At the inspected commit, `data-asset-id` affects 21/21 sources and `data-label` affects 10/21.
2. Obtain independent T012 QA, Product, Architecture, and Technical-Art PASS evidence on the corrected committed candidate.
3. Complete T013 independent Art Director source review against every inventory row, readability requirement, geometry/palette/noise rule, and provenance record. This document is not T013 approval.
4. Keep T014 and all imported PNGs, import settings, prefabs, scenes, code, gameplay, logistics, power, animation, and audio blocked until T013 passes; all original downstream accessibility, rasterizer, mapping, and integration gates remain in force.
