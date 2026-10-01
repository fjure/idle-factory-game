# Contract: Art Pipeline, Provenance and Checks

**Scope**: spec FR-007–FR-009, FR-013, FR-014, FR-019, FR-021–FR-023, US6, SC-003, SC-004, SC-009; research R07–R10, R14.

## Production chain (mandatory order per asset family)

1. Art Director: approved Art Bible v1.x and versioned brief (`docs/art/F004-asset-briefs-v1.md`), route chosen with a reason.
2. Producer (3D/Render agent for `blender`, Asset Artist agent for `comfy` and `scripted`): sources and raw outputs under `art-source/f004/<family>/<asset>/`, provenance record, pixel pass output.
3. Independent review: a second Art Director session with fresh context (never the producer, brief author or spike author), plus UI/UX Reviewer for cue legibility and Game Designer/PO for readability; a cross-family cohesion review at 1x against the style-anchor sheet before each family is approved. Provenance records reviewer session and date.
4. Technical Artist: import into `Assets/IdleFactory/Art/F004/`, manifest, catalog registration through Unity tooling.
5. In-game validation (Test/QA; UI/UX and Game Designer/PO for readability).

## Routes

| Route | Tool and pins | Committed |
|---|---|---|
| `blender` | Blender 5.2.1, no add-ons, template `tools/art-pipeline/blender/rig-v<k>.blend`, `render.py`, Standard view transform, filter size 0 | Model `.blend`, raw renders, script commit |
| `comfy` | ComfyUI v0.38.0, Z-Image-Turbo (Apache-2.0), approved LoRA (license recorded), workflow JSON | Workflow JSON, prompt, seed, raw outputs |
| `scripted` | `tools/art-pipeline` `cues` module (Python + pinned Pillow via `uv`) | Script commit, parameters |

Every route ends in `pixel_pass` (deterministic, research R07): route-specific reduction to the art grid (Blender final size or integer supersample with mode/box reduction; generative grid detection and mode reduction), alpha 0/255, CIELAB palette quantization with deterministic tie-break and no dithering, outline generation, one fixed canvas and footprint-anchored pivot per asset shared by all frames, manifest entry. Touch-up is a recorded scripted step (or recorded inpainting); no GUI editing. Generation and render batches hold the `gpu` lease and never run during performance measurements.

## Palette

`tools/art-pipeline/palette/palette-v<k>.json`: ordered colors in groups with a per-group budget (machine ramps including dimmed and warning variants, floor and plates, resources, UI, ink, reserved state-cue colors kept out of machine and floor ramps, CVD-safe pairs), license or "authored". Initial cap 48, tested in the spike; palette semver follows the Art Bible (any growth beyond the cap, removal or hue change is a major version).

## Automated checks (`tools/art-pipeline checks`, required for every shipped sprite)

Palette compliance and per-group budget; canvas and pivot equal the manifest; alpha only 0/255; outline present; frames equal the brief; overhang ≤ 0.5 cell; flash metric (research R05) ≤ 3 per second; frame 0 of running loops is lights-on; silhouette IoU ≤ 0.70 pairwise for machines at full size and on a 32×32 downsample; contrast ≥ 3:1 non-text and ≥ 4.5:1 text/glyphs against declared surfaces, also under the darkest lighting the Art Bible allows; cue icons ≥ 12×12 art px; generation of the shuffled grayscale and CVD (protanopia, deuteranopia, tritanopia; Machado 1.0) sheets for the independent agent reviewer. Output: `art-source/f004/check-report.json` (committed per batch).

## Provenance

One `provenance.json` per asset revision with the fields in data-model. Generated assets ship only with model and LoRA licenses that permit commercial use and a training-data note (spec FR-021). The pipeline refuses to emit a manifest entry for an asset whose provenance is incomplete.

## Unity side

Edit Mode `F004AssetContractTests`: every manifest entry (schema version, PNG hash) is imported with the declared PPU, pivot, point filter and no mipmaps; every Sprite Atlas has RGBA32, point filter, no mips, padding 2, no rotation and pages ≤ 1024² for the Default, Standalone and Android platforms; every F003 key resolves to F004 primary frames in the shipped set (SC-003); the golden key list is unchanged except for additive `F004-` keys. Enabling Sprite Atlas v2 packing is a recorded settings decision (`EditorSettings.asset`, `IDLEFACTORY_ALLOW_SETTINGS_NOISE=1`).

## Inventory, brief and provenance additions

- The F004 brief contains an inventory table (asset × state × frames) derived from the 29 F003 brief rows plus the new floor/background family and the UI icons per surface, a floor tiling rule, and per asset: `ports` (none for F003 machines), `format`, `shadow`, `overhang`, `cueAnchor`, `confusionPeers`.
- Provenance additionally records `candidateSelection` (how the final seed or render was chosen), `rigVersion` and `renderEnv` (GPU, driver), `comfyNodeVersions`, `checkReportHash` and `reviewedBy.{session, date}`.
- A pipeline test regenerates one asset per route against a golden hash or diff (research R09). `scripts/test-art-pipeline.ps1` runs the pipeline tests and checks and is part of the MAQA test commands and the batch gate.
