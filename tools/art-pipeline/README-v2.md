# Art pipeline v2 prototype (F007 T613)

Provisional `pipeline: v2-prototype`; M4 freezes v2 with Art Bible v2. Contract: `specs/007-stylized-industrial-visual-reboot/contracts/art-pipeline-v2.md`.

## Run

```
cd tools/art-pipeline
uv run --frozen --offline python -m art_pipeline render-hd --params f007/reference-machine.params.json --out <dir> [--repro]
```

`--repro` renders a second time on one thread and runs the reproducibility check per pass. A failing check blocks the manifest
(`<id>.manifest.json` and `<id>.provenance.json` are written only when every check passes). Blender is `$BLENDER_EXE` or
`C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`. Rebuild the rig with
`blender -b --factory-startup --python-exit-code 1 -P blender/build_rig_v3.py`.

Shipped pixels come only from this headless batch. The Blender MCP may be used to iterate, never for final renders.

## Mesh export route (Direction C, TA-8)

```
uv run --frozen --offline python -m art_pipeline mesh-export --params f007/reference-mesh.params.json --out <dir>
```

Headless `blender/export_mesh.py` (run on `rig-v3.blend`) applies all transforms and exports GLB (Y-up, node scales 1, footprint
centred on the origin, ground at y = 0) plus `<id>.mesh-sidecar.json` (`unitsPerWorldUnit`, `variants`, `shadowCaster`, `lods`).
`art_pipeline/mesh_export.py` then runs `check_mesh_export` on `read_gltf_summary` (unit and scale, pivot, triangle count,
material and variant counts, shadow-caster flag), the prompt lint for `generatedInputs`, and the v2 provenance check
(`route: mesh-export`). Only when all pass are `<id>.mesh-manifest.json` and `<id>.provenance.json` written. FBX is not wired; GLB
is the format. LODs are only recorded (`lods`); none are generated. Only the procedural `reference-machine` subject is wired; a
`.blend` subject arrives with the first mesh asset. `MESH_LIMITS` are provisional; the pivot tolerance is 0.025 world units
(the reference vent protrudes 0.03, shifting the footprint centre by 0.015).

**Not in this run:** the Unity silhouette-IoU step for C. `checks_v2.check_silhouette_iou_at_scale` is the library function and is
covered by a pytest, but nothing renders the meshes in Unity yet.

## Manifest pivot convention

`manifest_row.pivot` is NORMALISED: fractions of the canvas, origin bottom-left (Unity's sprite pivot, read by the importer).
`pivotPx` carries the art-pixel value of `data-model.md` (origin top-left). Both describe the one shared pivot of all passes.

## Lighting conformance (N2)

`rig-v3.blend` is a render tool for directions B and C. It is NOT conformant with Art Bible v1 lighting rule B (single top-left
key, hard terminator): the fill and rim lights and the warm and cool colours depart from it on purpose. It becomes
authoritative only through Art Bible v2 / T639 (owner checkpoint 1). Until then no asset rendered with it is a production asset.

## Pinned values (TA-6)

| Item | Value |
|---|---|
| Blender | 5.2.1 LTS (hash 9e2066aef7ef, build 2026-08-25) |
| Engine | Cycles, CPU, seed 0, no animated seed, adaptive sampling off, 16 samples (per-asset `samples` may raise it) |
| Denoiser | off. OIDN ships inside Blender 5.2.1 and is unused; `render-info.json` records it |
| View transform | Standard for every pass. `viewTransform: AgX` is allowed on `beauty` only, as the recorded AD-9 comparison |
| Rig | `blender/rig-v3.blend`, derived from `rig-v2.blend` (same cameras), sha256 recorded in each provenance file |

## Assumptions (to retune after T607 / the M2 brief)

- **Dusk set.** World Bible v0.2 has no numeric light anchors; its only anchor is the mood line "Quiet. Methodical. Steady.
  Industrious. Dim-lit" and "a calm night shift with working lights on". v0.3 (T607) does not exist yet. The set, per camera and
  parented to it, is: warm key (1.0, 0.72, 0.45), energy 2.2, from the top left (Art Bible key direction, unchanged, hard
  shadow); cool fill (0.45, 0.6, 1.0), energy 0.5, from the right; warm rim (1.0, 0.6, 0.35), energy 1.0, from behind. World:
  (0.05, 0.07, 0.12), strength 0.25. Constants live in `blender/rig_common.py` (`DUSK`, `DUSK_WORLD`).
- **Capture key colour.** `(255, 0, 255)` until the capture driver (T615) fixes the value. It is a parameter of the check.
- **Pivot.** The camera looks at the world origin, so the origin projects to the render-canvas centre. That is the single pivot;
  after the union-bbox trim it is `canvas centre - crop origin`. It can be a fractional art pixel (the integral rule is direction A only).
- **Passes.** Shadow: key light only, world off, shadow-catcher plane, subject as holdout; the pass is the alpha channel (store as
  A8 or R8). Emissive mask: materials named `emit*` become white emission, all others holdout. Normal: world-space normal
  mapped to 0..1 (optional).
- **Look C import (N4).** C uses 128 PPU and Bilinear filtering, the same as B, because its floor and environment sprites are in
  the B style. This is an assumption; the machines of C are meshes and do not use the sprite rules.
- **Mips off (N4).** All looks import with mips off in M2 (limited to zero). Bilinear sprites drawn well below their art size on a
  phone may shimmer without mips. T614 and T617 must answer whether B and C need mips (and then padding of at least 2^levels, which
  `F007LookImport.AtlasPadding` already derives).
- **Mesh export (TA-8).** The checks read glTF/GLB (`read_gltf_summary`) plus a sidecar (`unitsPerWorldUnit`, `variants`,
  `shadowCaster`). Limits in `MESH_LIMITS` are provisional. The Unity silhouette-IoU step is not part of T613.

## Reproducibility measurement (two-render test)

Reference asset: procedural `reference-machine` (`render.py`), 384 px, ppu 96 (the asset keeps at least 27 px from every canvas edge; `render_not_clipped` enforces it), isometric 2:1, all four passes. Two renders, the
second forced to one thread, against the committed golden passes in `f007/reference/`:

| Pass | covered px | pixel share dE <= 2 | worst dE (CIE76) | SSIM |
|---|---|---|---|---|
| beauty | 24631 | 1.0 | 0.0 | 1.0 |
| shadow | 2186 | 1.0 | 0.0 | 1.0 |
| emissiveMask | 1145 | 1.0 | 0.0 | 1.0 |
| normal | 24631 | 1.0 | 0.0 | 1.0 |

The CPU renders are bit-identical across thread counts. The tolerances are therefore a safety margin for a Blender patch
update, not a measured need: dE <= 2 on >= 99 % of covered pixels, SSIM >= 0.995 (mean over 8x8 windows, stride 4, luma of the
image composited over grey; pure Python, no numpy).

## Unity side

`Assets/IdleFactory/Editor/Art/F007SpriteImportPostprocessor.cs` governs only `Assets/IdleFactory/Art/F007/**` PNGs. Page rule:
atlas pages stay at most 1 MiB (RGBA32-equivalent 512 x 512) until T614 passes on the Pixel and the owner accepts a change.
