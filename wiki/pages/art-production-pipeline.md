---
title: Art production pipeline
type: decision
sources: [S038, S040, S042, S052, S053]
updated: 2026-11-04
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

## Status after batch 1 (2026-10-27)

The pixel pass, checks, Blender rig, ComfyUI workflows, LoRA registration, test runner and `gpu` lease exist; details, placeholders and pitfalls are in [F004 batch 1 art tooling](./f004-batch-1-art-tooling.md). The tool version in the plan (ComfyUI v0.38.0) was validated live on v0.43.0. Blender output must carry no metadata stamps, or reproducibility and the no-local-paths rule break. (S040)

## Status after batch 3 (2026-10-28)

Spike S1 assets exist at D=24 for both projections (T415); the silhouette IoU gate fails for oblique 3/4 and passes for isometric 2:1. Write generated JSON with LF newlines. See [F004 S1 spike assets](./f004-s1-spike-assets.md). (S042)

## Status after batch 9 (2026-11-03)

Art-pipeline v1 checks (`checks_v1.py`, `palette-v1.json`) implement Art Bible 9.3 and must be called by T434-T439; T430 added the Unity sprite import rules. See [batch 9 components](./f004-batch-9-pipeline-camera-catalog-import.md). (S052)

## Status after batch 10 (2026-11-04)

T440 imported seven families (126 PNGs) through one manifest into four Sprite Atlas v2 with the repeatable `F004ArtIntegration.cs`; the floor tile awaits re-render (T470) and import (T471). APK cap is now 150 MB. See [batch 10 components](./f004-batch-10-lattice-zoom-ore-vein-art-import.md) and [batch 10 pitfalls](./f004-batch-10-workflow-pitfalls.md). (S053)

Related: [visual identity and art direction](./visual-identity-art-direction.md), [F004 batch 1 art tooling](./f004-batch-1-art-tooling.md).
