---
title: F003 placeholder art pipeline
type: howto
sources: [S018, S020, S055]
updated: 2026-11-05
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

## Integration review (T129)

Independent Art Director and Technical Artist review of the running scene: **pass with findings**, none blocking (`reviews/art-direction-review.md`). A Windows player played about 55 simulated minutes through the real scene and took captures. All 78 keys resolve, with 64 PPU, pivots, point filter and sorting as recorded. (S018)

- **Art Director** AD-1 world background `#090C0E` against the Bible `#111827` (raises contrast; needs an owner note); AD-2 no selection frame on a locked slot; AD-3 badge labels turn to noise at the Z16 zoom minimum; AD-4 action buttons are text only; AD-5 amber reason text beside a red icon; AD-6 Iron Ore icon at 2.05:1 on the raised card (N1); AD-7 camera not re-centred after a sheet; AD-8 machine panel does not name Boosted or Overdrive; AD-9 the `L3` digit reads like a mirrored E. (S018)
- **Technical Artist** TA-1 runtime texture memory is 1.49 MiB, not the recorded 1.27 MiB (GPU padding for sides that are not multiples of 64); TA-2 27 of 49 UI icons and the 3 tier chips were never rendered; TA-3 the Research stylesheet uses `:last-child`, which USS ignores with a warning. (S018)
- **Open**: C1 (WAIT and PAUS badges never observed) and N3 (two-reviewer shuffled-sheet pass) need a second reviewer. AD-1, AD-4 and AD-6 are owner decisions, recorded as open and deferred to Feature 004. (S018)
- **Method**: the harness was uncommitted and used a product-name override for save isolation; see [F003 feature review](./f003-feature-review.md). (S018)

> Superseded (2026-11-05): owner chose option D in T447; T476 (#672) deleted the F003 sprites, SVG sources and `PROVENANCE.md` from `Assets/`. History stays in git, and F004 keeps the F003 key names. The fallback behaviour described here no longer exists; see [batch 12](./f004-batch-12-final-verification.md). (S055)
