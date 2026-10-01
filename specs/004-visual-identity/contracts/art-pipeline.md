# Contract: Art Pipeline, Provenance and Checks

**Scope**: spec FR-007–FR-009, FR-013, FR-014, FR-019, FR-021–FR-023, US6, SC-003, SC-004, SC-009; research R07–R10, R14.

## Production chain (mandatory order per asset family)

1. Art Director: approved Art Bible v1.x and versioned brief (`docs/art/F004-asset-briefs-v1.md`), route chosen with a reason.
2. Producer (3D/Render agent for `blender`, Asset Artist agent for `comfy` and `scripted`): sources and raw outputs under `art-source/f004/<family>/<asset>/`, provenance record, pixel pass output.
3. Independent Art Director review (a session other than the producer and other than the brief author's spike producer).
4. Technical Artist: import into `Assets/IdleFactory/Art/F004/`, manifest, catalog registration through Unity tooling.
5. In-game validation (Test/QA; UI/UX and Game Designer/PO for readability).

## Routes

| Route | Tool and pins | Committed |
|---|---|---|
| `blender` | Blender 5.2.1, no add-ons, template `tools/art-pipeline/blender/rig-v<k>.blend`, `render.py`, Standard view transform, filter size 0 | Model `.blend`, raw renders, script commit |
| `comfy` | ComfyUI v0.38.0, Z-Image-Turbo (Apache-2.0), approved LoRA (license recorded), workflow JSON | Workflow JSON, prompt, seed, raw outputs |
| `scripted` | `tools/art-pipeline` `cues` module (Python + pinned Pillow via `uv`) | Script commit, parameters |

Every route ends in `pixel_pass` (deterministic): nearest-neighbor to the art grid, palette quantization without dithering, alpha 0/255, outline, trim/pad to canvas and pivot, frames, manifest entry. Touch-up is a recorded scripted step (or recorded inpainting); no GUI editing.

## Palette

`tools/art-pipeline/palette/palette-v<k>.json`: ordered colors with semantic locks (state cues, ink, UI), ramps per material, license or "authored". At most 48 colors unless the Art Bible raises it with a reason.

## Automated checks (`tools/art-pipeline checks`, required for every shipped sprite)

Palette compliance; canvas and pivot equal the manifest; alpha only 0/255; outline present; frames equal the brief; overhang ≤ 1 cell; flash rate ≤ 3 Hz; silhouette IoU ≤ 0.70 pairwise for machines at full size and at the smallest scale; contrast ≥ 3:1 non-text and ≥ 4.5:1 text/glyphs against declared surfaces; generation of the shuffled grayscale and CVD (protanopia, deuteranopia, tritanopia; Machado 1.0) sheets for the independent agent reviewer. Output: `art-source/f004/check-report.json` (committed per batch).

## Provenance

One `provenance.json` per asset revision with the fields in data-model. Generated assets ship only with model and LoRA licenses that permit commercial use and a training-data note (spec FR-021). The pipeline refuses to emit a manifest entry for an asset whose provenance is incomplete.

## Unity side

Edit Mode `F004AssetContractTests`: every manifest entry is imported with the declared PPU, pivot, point filter, no mipmaps, uncompressed format and atlas; every F003 key resolves to F004 art in the shipped set (SC-003); the golden key list is unchanged except for additive `F004-` keys.
