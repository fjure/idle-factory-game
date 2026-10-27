---
title: Art production pipeline
type: decision
sources: [S038]
updated: 2026-10-01
---

# Art production pipeline

Feature 004 plans an agent-operated pipeline: brief → route production → deterministic pixel pass and checks → independent Art Director review → Technical Art import → in-game validation. No workflow needs a human pixel editor. (S038)

## Routes per asset family

| Family | Route | Reason |
|---|---|---|
| Machines, slot plates, floor | Blender 5.2.1 (committed rig and `render.py`, no add-ons, Standard view transform, filter size 0) | Shared camera and light; coherent animation frames |
| Resources, item tiers, illustrative icons, backgrounds, concepts | ComfyUI v0.38.0 + Z-Image-Turbo (Apache-2.0) + an approved pixel-art LoRA | Small illustrative objects; no frame coherence needed |
| State cues, markers, selection frame, simple glyphs | Scripted drawing (Python + pinned Pillow via `uv`) | Exact shapes and color-vision rules |

The spike may move a family to another route with a recorded reason. (S038)

## Pixel pass and checks

`tools/art-pipeline` (a `uv` project) does nearest-neighbor reduction to the art grid, palette quantization without dithering, alpha 0/255, outline, trim to canvas and pivot, frames and an import manifest. Checks cover palette, canvas, pivot, alpha, outline, frame count, overhang, flash rate ≤ 3 Hz, silhouette IoU ≤ 0.70, contrast, and generation of grayscale and color-vision sheets. (S038)

## Reproducibility and provenance

Raw renders and generations are committed as intermediates, so the pixel pass is exactly reproducible. Regeneration from tools must be pixel-identical for scripted routes and within 0.5 % of opaque pixels for Blender (EEVEE and driver variance) and same-seed generation. Every shipped asset has a provenance record with tool, model and LoRA versions and licenses, parameters, scripted touch-up steps and an independent reviewer; non-commercial licenses are excluded. Sources live in `art-source/f004/`, outside `Assets/`, so Unity never imports `.blend` files. (S038)

## Unity import

Pixels per unit equal the art density, so 1 cell stays 1 world unit. Point filter, no mipmaps, uncompressed RGBA32 for world art (block compression smears pixel edges), one Sprite Atlas per family, manifest-driven importer. (S038)

Related: [visual identity and art direction](./visual-identity-art-direction.md).
