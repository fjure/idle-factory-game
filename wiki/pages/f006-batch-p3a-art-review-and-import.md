---
title: F006 batch 4 art reviews, import and Office glyph
type: component
sources: [S060]
updated: 2026-11-13
---

# F006 batch 4 art reviews, import and Office glyph

## T525 independent family reviews (#698, PR #747, afab9a2)

Five read-only lenses reviewed T519-T524 as source assets only: Art Director APPROVED_WITH_CONDITIONS (2 major), UI/UX and Game Designer ACCEPT_WITH_NOTES, World Designer CONFORMS_WITH_CONDITIONS, Game Systems CONFIRMED_NO_PACING_EFFECT for the vein. Conditions needing a composed frame are carried, never passed. Records: `reviews/art-direction-f006.md`, `reviews/world-bible-conformance.md` part 1. (S060)

Resulting Art Bible v1.1.0 section 21.9 corrections: (S060)
- completion highlight sprite added (FX atlas 43 frames, 87.2 % of 256²); Assembler flash wording cites measurements; lamps static (`copper_2` head, `steel_3` cap); GROUND has 3 frames;
- EDGE on the front margin row only; the front-only stage-2 wall ("Walled yard") is conditional (P2-F23): zero wall pixels in any C8 ring, wall not outweighing front-row plates, SC-006 trial 9 of 10, all proved in T527;
- Office glyph decision (P2-F22), K4 tail and `KeepOutOf` (below), page cap (see [Android load hang](./f006-android-load-hang-atlas-page-cap.md)).

## T526 art import (#699, PR #749, a3a526b)

World atlas 512² and FX atlas 256² under `Assets/IdleFactory/Art/F006/` (PPU 24, point filter, uncompressed). The production K4 tail is in the shipped F004 cue sprites and `FactoryWorldLayout.KeepOutOf` uses the 23 x 27 badge extent. A full F004 re-import from `art-source/f004` would revert the tails; `F004AssetContractTests` guards it, `F006AssetContractTests` checks settings, atlas membership, counts and keys. (S060)

Open for T527 (A-13): register F006 catalog rows in the scene and flip placed rows to required, remove the F004 floor atlas, C8 proof with the K4 tail over a trunk, luminance in `cue_over_lane_contrast`. (S060)

## T554 Office glyph (#752, PR #753, 2e14a5f)

New 24 px `ui_text` glyph `f006-ui-office` through the F003 UI-icon route under `art-source/f006/ui/`, Art Director pixel review, F006-UI count 0 to 1. Not yet imported into Unity (see [hub](./f006-batch-p3a-card-hub-and-gate-fixes.md)). (S060)

Related: [art production pipeline](./art-production-pipeline.md), [P2 art](./f006-batch-p2-art-and-presentation.md).
