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
- **Mesh export (TA-8).** The checks read glTF/GLB (`read_gltf_summary`) plus a sidecar (`unitsPerWorldUnit`, `variants`,
  `shadowCaster`). Limits in `MESH_LIMITS` are provisional. The Unity silhouette-IoU step is not part of T613.

## Reproducibility measurement (two-render test)

Reference asset: procedural `reference-machine` (`render.py`), 384 px, ppu 128, isometric 2:1, all four passes. Two renders, the
second forced to one thread, against the committed golden passes in `f007/reference/`:

| Pass | covered px | pixel share dE <= 2 | worst dE (CIE76) | SSIM |
|---|---|---|---|---|
| beauty | 42677 | 1.0 | 0.0 | 1.0 |
| shadow | 3898 | 1.0 | 0.0 | 1.0 |
| emissiveMask | 2053 | 1.0 | 0.0 | 1.0 |
| normal | 42677 | 1.0 | 0.0 | 1.0 |

The CPU renders are bit-identical across thread counts. The tolerances are therefore a safety margin for a Blender patch
update, not a measured need: dE <= 2 on >= 99 % of covered pixels, SSIM >= 0.995 (mean over 8x8 windows, stride 4, luma of the
image composited over grey; pure Python, no numpy).

## Unity side

`Assets/IdleFactory/Editor/Art/F007SpriteImportPostprocessor.cs` governs only `Assets/IdleFactory/Art/F007/**` PNGs. Page rule:
atlas pages stay at most 1 MiB (RGBA32-equivalent 512 x 512) until T614 passes on the Pixel and the owner accepts a change.
