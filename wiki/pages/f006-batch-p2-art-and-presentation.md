---
title: F006 batch P2 art families, attention model and panel scaffold
type: component
sources: [S056, S059, S060]
updated: 2026-11-13
---

# F006 batch P2: art families, attention model, scaffold, display preferences v2

Batch P2 produced the world art families and the first ready gameplay and UI Issues (T519-T524, T532, T533, T538). Nine Issues merged; the follow-ups P2-F1 to P2-F21 are in `reviews/p2-followups.md`. (S059) Design context: [world presentation design](./f006-world-presentation-design.md); previous batch: [P1](./f006-batch-p1-enabling-refactor.md).

## Art families

| Task | Issue / PR | Result |
|---|---|---|
| T519 | #692 / #738 | F006-GROUND (base, deck, hatch 70x54) and F006-FOUNDATION (outline, flat 70x36), Blender rig-v2 headless plus a pixel pass |
| T522 | #695 / #739 | F006-LINK (trunk, junction, branch, branch-active; set R only, trunk recoloured per Art Bible 21.8 item 3) and F006-FX (pop, pip, spark, steam, build dust, glint, 6 frames, plus a static completion highlight); scripted generator `tools/art-pipeline/art_pipeline/f006/link_fx.py` |
| T523 | #696 / #737 | F006-UI sprites all "not needed" (USS plus PanelIcons), Art Bible 21.2 UI row |
| T524 | #697 / #740 | OD-12 K4 pointer tail in `StateCueGeometry` (`BadgeExtentBox` 23x27, `BadgeBox` unchanged), cue-tail generator, `art-source/f006/cue`; runtime sprite swap deferred |
| T520 | #693 / #741 | F006-DEPOT (12 sprites 80x76, steel slab, copper collar, `copper_2` head and `steel_3` cap lamp) and F006-DEPOSIT (rest s1-s3, highlight, 6-frame vein chip at 8 fps, 2 flashes/s alone) |
| T521 | #694 / #742 | F006-EDGE (railing with quiet variants, wall, gantry fl/fr/corner) and F006-PROP (4 props up to 24 px); `keepout-proof.json`: wall and gantry only on the front margin row |

Commits: d135e9b, a6f079c, 0f950bd, 3ef6df7, 64bd02b, 3ef08f5. (S059)

- The flash matrix is per family: pace caps Miner, Smelter and Press 10 fps, Assembler 8 fps. The link layer has a C8 ring proof, and P1-F3 cue-over-lane contrast was checked. (S059)
- Lamps are static with no `teal_*` or `lit_*`; this replaces the R12 wording (P2-F4). (S059)
- Open: the composite C8 proof, the front-only wall question (P2-F7), and the runtime swap of K4 tail sprites plus a `KeepOutOf` switch to `BadgeExtentBox` (P2-F5, T526 after T525). (S059)

## Attention and presentation models (T532, #701 / #743, 357e4e2)

`MachinePresentationModel`, `AttentionModel` and `AttentionMarkerView` live in `Presentation/World`: shortage ordering, at most 2 shortage markers plus a store-full marker, `Overlay` layer, zero-allocation refresh. Architect APPROVE_WITH_NOTES; Game Systems CONFORMS_WITH_NOTES. (S059) Trap: `CompletionsDelta` clears only inside `Refresh`, so call `Refresh` every frame (P2-F10); reset the baseline on a session replace (P2-F12). (S059)

## Panel scaffold (T533, #705 / #744, a384567)

`PanelScaffold` (cs, uxml, uss) uses design tokens only, with 84 Play Mode cases over 8 screen classes at 100, 150 and 200 % text. UI/UX ACCEPT_WITH_NOTES. The root is a full-screen opaque fill, so the docked machine card needs a variant or class-only reuse (P2-F16). (S059)

## Display preferences v2 (T538, #709 / #745, f178a65)

`DisplayPreferencesStore` v2 reads `display-v2.json` with a strict parser, falls back to v1 only when v2 is absent, writes atomically through `display-v2.json.tmp`, and caps the file at 4096 bytes; the Settings toggle is "Show zoom buttons". Security PASS_WITH_NOTES. QA failed twice; see the replace failure in [P2 workflow pitfalls](./f006-batch-p2-workflow-pitfalls.md). Predecessor: [display preferences store](./f004-display-preferences-store.md). (S059)

## Closed in batch 4

P2-F22 (Office glyph) closed by T554 and P2-F23 (conditional front-only wall) decided; the K4 tail swap and `KeepOutOf` 23 x 27 landed in T526. See [batch 4 art review and import](./f006-batch-p3a-art-review-and-import.md). (S060)
