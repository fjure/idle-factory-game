# Contract: Art pipeline v2 (`render-hd` route and per-look checks)

**Scope**: FR-019, FR-034; research R05, R13 and R14.

**Status**: planned. The M2 prototype uses a provisional version, marked `pipeline: v2-prototype`. M4 freezes v2 together with Art Bible v2.

## Routes

| Route | Used by | Output |
|---|---|---|
| `pixel` (existing F004 route; parameters: density, palette) | Direction A | Point-filtered sprites on a palette |
| `render-hd` (new) | Directions B and C (C: the environment, plus mesh exports) | Soft-alpha sprites, shadow and emissive passes, compressed atlases |
| `mesh-export` (new; C only) | Direction C | FBX or glTF, materials, LODs if needed |
| `scripted` (existing) | Indicator glyphs, selection frame | Exact shapes |
| `comfy` (existing) | Concept images, tileable material textures | Inputs only; never a shipped sprite without Art Director correction |

## `render-hd` steps

1. **Render.** Blender 5.2.1 renders headless with `rig-v3.blend`, the committed `render.py` and a parameter file per asset. Passes: `beauty`, `shadow`, `emissiveMask`, optional `normal`. The view transform is Standard. Interactive iteration through the Blender MCP is allowed. Shipped pixels come only from the headless batch run.
2. **Post.** Trim to the declared canvas, set the pivot, write the frame strip and write the manifest row (data-model `MachineArtEntry`).
3. **Checks.** A failure blocks the manifest.

| Check | A | B | C (sprites) |
|---|---|---|---|
| canvas, pivot, frames | ✓ | ✓ | ✓ |
| palette, alpha 0/255, outline | ✓ (new density and palette) | — | — |
| flash ≤ 3 per second (body plus emissive composite) | ✓ | ✓ | ✓ |
| silhouette IoU at phone fit-all scale, every stage | ✓ | ✓ | ✓ (rendered mesh silhouettes) |
| indicator anchor inside the body and the hit area | ✓ | ✓ | ✓ |
| indicator contrast over its own machine (≥ 3:1 fill or outline) | ✓ | ✓ | ✓ |
| texture-memory estimate per family against budget | ✓ | ✓ | ✓ |
| reproducibility (re-render diff within tolerance) | pixel-identical | ΔE ≤ 2 on ≥ 99 % of opaque px (hypothesis, M4) | same |

## Provenance (`provenance.json`, additions to F004 §15)

`look` · `pipeline` (`v2-prototype` or `v2`) · `route` · `renderPasses[]` · `rigVersion` with its sha256 · `textureFormat` per platform · `generatedTextures[]` (model, licence, prompt, seed, workflow hash) · `artDirectionBrief` (path and version) · `artBibleVersion`. This is `"none (prototype brief)"` in M2, and `F007-AB v2.0` or later in M5. · `reviewedBy` (independent; written in its own commit after review).

Generated content without a commercial-use licence is refused by the pipeline. In M5, a manifest row without complete provenance is refused (SC-013).

## Unity import

`F007SpriteImportPostprocessor` sets these per look:

- PPU;
- filtering;
- mipmaps;
- compression per platform: ASTC 6×6 (or 4×4) on Android, BC7 or DXT5 on Windows;
- Sprite Atlas v2 per family.

The page plan obeys the page rule in force: 1 MiB until the R13 re-measurement passes. `AtlasPageBudgetTests` are extended per look.

## Validation command

`powershell -NoProfile -File scripts/test-art-pipeline.ps1`

Exit codes: 0 pass, 1 test failure, 3 tooling unavailable. Exit 3 is a blocker, never a pass. `scripts/test-unity.ps1` chains it automatically when `tools/art-pipeline/`, `art-source/` or `Assets/IdleFactory/Art/F007/` change.

**Plan action.** `scripts/test-unity.ps1` and `scripts/check-after-implement-hooks.ps1` currently trigger on `Assets/IdleFactory/Art/F004/`. A DevOps task extends that path list to `Assets/IdleFactory/Art/F007/`.
