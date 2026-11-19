---
title: F006 batch 5 world deck, depot, effects pool and markers
type: component
sources: [S061]
updated: 2026-11-19
---

# F006 batch 5: world deck, depot and link, effects, markers (T527, T556, T529, T528, T557)

Merged PRs #762, #764, #765, #766, #768. Contract notes: `contracts/world-presentation.md`. (S061)

## Deck and edges (T527, Art Bible 21.10)

EDGE pieces sit on the front margin row only, as a V below the content with at least 6 art px clearance (measured 22 px railing and wall, 9 px gantry); quiet railing within one cell of the depot. The stage-2 wall stays provisional until the SC-006 shuffled trial reaches 9 of 10 (T546). Edge pieces are sprite renderers outside the 31-sprite budget. The World Designer confirmed prop weights 0.20, 0.30, 0.40. (S061)

## Cue contrast (T556, Art Bible 21.11)

**Owner decision (C-10, 2026-11-17)**: a new art task. Result: a cue passes over a LINK lane tone when the fill OR its 1 px `ink_0` outline reaches 3:1; no recolour. Limits per badge: `copper_1` at most 12 px under the mask and 2 in the ring; `copper_2` 0. The gate reports averages, not maxima (C-17). (S061)

## Effect pool (T529)

Two fix rounds replaced a full `RunSnapshots.Factory` rebuild at up to 10 Hz with a per-frame completion path that allocates nothing. Re-QA then found that an active contract bumps `Contracts.Version`, and with it `StructureRevision`, on every produced unit, forcing a rebuild per frame. Now only machine identity, layout, offline return and session replace rebuild at once; other revision changes at most once per second. Both cases have 600-frame zero-allocation Play Mode tests. Per-family fps caps live in `WorldCompositionSettings`, not content data (P2-F14). (S061)

## Depot and link (T528, Art Bible 21.12 draft)

- LINK is a third Tilemap; branch pips draw on Overlay. `WorldTapResolver` priority: marker icon, then machine, then touch circle. `LimitingCause` is in the model (tooltip only); card copy follows in T547. (S061)
- Depot fill mapping: EMPTY 0, LOW below 50 %, HIGH below capacity, FULL only on StoreFull; capacity 0 is High. (S061)
- Census at 12 locations: at most 41 world sprites and 105 camera renderers (F004 cap 61 + 41 + 3 Tilemaps), below the 173 single-pass tier. This corrects the 95 signed off in OD-10; **section 21.12 stays a draft pending owner confirmation**. (S061)

## Attention markers (T557, owner decision 2026-11-18)

`AttentionMarkerView` had never been instantiated in production since T532, so world markers never showed and T528's tap priority was dead. T557 wires it on Overlay order +200 with real-scene tests. Gate C refuted two Play-Tester findings: C-F3 (per-machine pins are T528 slot badges; the marker cap holds) and C-F4 (the badge is not a tap target; the marker tap selects). Open: the store-full marker reuses a small badge sprite (C-57). (S061)

Shortage semantics: [world presentation design](./f006-world-presentation-design.md). Related: [shell and stages](./f006-batch-5-shell-card-and-stages.md), [P2 art and presentation](./f006-batch-p2-art-and-presentation.md).
