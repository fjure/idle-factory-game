---
title: F003 placeholder art pipeline
type: howto
sources: [S018, S020]
updated: 2026-10-07
---

# F003 placeholder art pipeline

How the Feature 003 development placeholders went from brief to Unity. Feature 004 replaces them as a presentation-only swap, so keys and world sizes must stay stable. Runtime lookup is described in [run bootstrap and lifecycle](./run-bootstrap-and-lifecycle.md). (S018)

## Review gates

- Sources are SVG 1.1 with a provenance row each (T108). An independent Art Director reviews every asset against the frozen Art Bible and briefs before import (T109); rejected assets get a geometry-only rework (T142) and a delta review of only those assets (T143). No silent restyle. (S018)
- Gaps are measured on rendered alpha masks (Inkscape at 8x, each element rendered alone, minimum edge distance). The pixel method reads about 0.1 px low. A figure that cannot be reproduced by mask or analytic geometry is withdrawn, as the T109 0.80 px cooldown gap was. (S018)
- Owner decision D1: condition C6 (tier pips) is read by displayed size, so 4 px pips on the 1:1 tier chips pass while the 32 pt Item icons need 8×8 px pips with 4 px gaps. Condition D2: tier chips and Item icons never sit on the accent fill or a light surface. (S018)
- A QA pass that only repeats the author's numbers is not independent; the reviewer must measure. (S018)

## Import (T111)

- Rasterize each SVG with the documented Inkscape 1.4.2 command at the brief's size and transparent background; a second run is pixel-identical. Import as Sprite/Single, 64 PPU, point filter, clamp, no mipmaps, uncompressed, FullRect; pivot bottom-left for slot bases and machines, centre otherwise. (S020)
- All 78 `F003-*` keys are registered in the scene sprite catalog, which validates world size (px ÷ PPU) and normalized pivot, so 32 PPU art can replace 64 PPU art without code changes. Decoded memory is 1.27 MiB. (S018, S020)
- `F003AssetContractTests` asserts the inventory, size, pivot and importer per row, catalog registration, key resolution, and that a missing key fails start-up. (S020)

Scene and catalog edits follow [Unity serialized asset edits](./unity-serialized-asset-edits.md).
