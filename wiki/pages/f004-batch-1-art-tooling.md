---
title: F004 batch 1 art tooling
type: component
sources: [S038, S040]
updated: 2026-11-06
---

# F004 batch 1 art tooling

What MAQA batch 1 of Feature 004 actually built for the [art production pipeline](./art-production-pipeline.md). Issue numbers are the status authority. (S040)

## Pixel pass project (#514, T402)

`tools/art-pipeline` is a `uv` project with `pixel_pass`, `checks`, `cues`, palette v0 (48 colours), `models.json` and golden outputs per route. Palette v0 and the 0.6 darkest-light factor were placeholders at batch 1 (the Art Bible v1 sets the darkest lighting to 1.0, no Multiply light); palette v1.0.0 (48 colours, frozen with the Art Bible v1.0) superseded palette v0 for production art (`docs/art/F004-art-bible-v1.md` section 5, see [Art Bible v1 review and sign-off](./f004-art-bible-v1-review-and-signoff.md)). (S040, S038)

## Blender rig (#515, T403)

`build_rig.py` builds `rig-v1.blend` headless with Cycles on CPU, seed 0; `filter_width` clamps at 0.01. `scripts/check-art-tooling.ps1` reports exit 0 OK, 1 fail, 2 tool unavailable. QA lesson: Blender writes tEXt metadata (absolute path, date) into PNGs, so stamps must be disabled, otherwise hashes are not reproducible and local paths leak into committed files. (S040)

## Test entry point (#518, T406)

`scripts/test-art-pipeline.ps1` exits 0 pass, 1 fail, 3 unavailable and writes a JSON report to `test-results/art-pipeline/`. `test-unity.ps1` chains it when art paths changed. The after_implement hook gate has a check 6 for it; pass `-ArtBatchBase` to scope it to one batch. (S040)

## ComfyUI workflows and LoRA (#516 T404, #517 T405)

Two Z-Image-Turbo workflows (with and without LoRA) were validated live on ComfyUI v0.43.0, although the contract names v0.38.0. `detect_grid` is unreliable on blended upscales, so briefs pin `grid`. Non-integer upscales are refused only when `expected_size` is set, so briefs always set it. The pixel-art LoRA is registered in `models.json` at revision 0a5092d, Apache-2.0, sha256 `09b1b45c…`; its model card names no training data, which the provenance record must state. (S040)

## GPU lease (#519, T407)

`scripts/resource-lease.ps1` gained a `gpu` resource (label `resource:gpu`) for ComfyUI and Blender GPU work; AGENTS.md "Shared workstation resources" documents it. (S040)

Related: [F004 batch 1 workflow pitfalls](./f004-batch-1-workflow-pitfalls.md), [visual identity and art direction](./visual-identity-art-direction.md).
