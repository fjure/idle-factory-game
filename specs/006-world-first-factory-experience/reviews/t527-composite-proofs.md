# T527 composite proofs (Issue #700)

**Task**: T527, the factory deck. **Branch**: `maqa/issue-700-t527-factory-deck`. **Author**: the Gameplay Developer worker (Technical Art and Performance lenses). The author does not approve its own art evidence: the Art Director, World Designer and UI/UX reviews of this note are separate.
**Method**: every number below is measured by tests in `Assets/IdleFactory/Tests/EditMode/F006/` (`DeckLayoutTests`, `DeckCompositeProofTests`, `DeckWiringTests`) on the imported PNGs, the real `FactoryWorldLayout` and the real `DeckPlanner`. Nothing is a mock. The composite (`DeckComposite`) draws the base, deck, prop, edge, link, deposit and depot layers of F006 and the F004 plates, machine bodies and accents in sort order; the overlays (a K4 badge on every location, both markers, one selection frame) are masks, because the C8 ring is the backdrop around them. The densest frame of Art Bible 21.5 is therefore 12 occupied locations (6 on the six layout, 20 on the twenty layout, 7 on the L layout) with a badge, both markers and one frame each. Layouts: 3 x 4 portrait, 4 x 3 landscape, six, twenty and L (the same five layouts as the keep-out proof of 21.5).
**Captures**: `t527-proofs/3x4-stage1.png`, `3x4-stage2.png`, `3x4-stage3.png`, `4x3-stage1.png`, `4x3-stage2.png`, `4x3-stage3.png` (x2 nearest neighbour, the densest frame with the depot in its full state; written by `IDLEFACTORY_WRITE_PROOFS=1` and the same test).

## 1. Placement rules the proofs rest on

| Rule | Where it lives |
|---|---|
| Rectangle grid, cell 70 x 54 art px, origin so that cell (c, r) is centred on the slot pivot (35 + 70 c, 15 + 54 r) | `FactoryDeckView.CellSize`, `GridOrigin`; the seam test pins every cell edge to a whole art pixel within 2e-3 px for cells -8 to 40 |
| `WorldBase` tilemap: base tile on every deck cell plus the extra front rows the edge V needs; `WorldDeck` tilemap: deck tile on an unlocked location, hatch on a locked one (padlock and digit stay the slot label) | `DeckPlanner.Build`, `FactoryDeckView.DrawTiles` |
| Edges on the front margin row only: `fl` and `fr` along the two iso diagonals one half cell apart (35 x 17 art px), `corner` at the apex; the upper ends of the V sit on the front margin row pivot or lower so that every piece clears the content extent by at least 6 art px | `DeckPlanner.BuildEdges` |
| Props only in the margin ring and on empty or locked cells, at most 24 art px, cell hash against the stage density, kind by the stage weights, at most 0.5 per eligible cell, never on a hit shape, keep-out ring, lane guard band (8 px), depot, deposit or edge piece | `DeckPlanner.BuildProps` |

## 2. P2-F7, P2-F23: the front-only wall

- **Zero wall pixels in any C8 ring or keep-out.** For every layout and stage, every opaque pixel of every placed edge sprite (12,162 to 18,512 pixels per layout and stage) is tested against the extent, every hit shape, every badge and marker keep-out grown by the 1 px ring, the depot canvas and the deposit cell, and against the pixel rings of the real badge, marker and frame sprites: zero overlaps (`KeepOutProof...` and `NoWallOrGantryPixelEntersAnyRing...`). The nearest edge pixel to the content extent is 22 art px away for the railing and the wall and 9 art px for the gantry.
- **Stage 1 has no wall and no gantry** (`EdgesAreFrontRowOnlyAndStageOneHasNoWallAndNoGantry`, Art Bible 21.5).
- **The wall does not outweigh the front-row machine plates (E1).** Luminance mass is the sum of the WCAG relative luminance of the visible pixels:

| Layout | Wall mass | Front-row plates only | Front-row plate + machine | Wall / plates only | Wall / (plate + machine) |
|---|---|---|---|---|---|
| 3 x 4 (3 in the front row) | 582.4 | 363.2 | 986.1 | 1.60 | **0.59** |
| 4 x 3 (4 in the front row) | 734.2 | 479.0 | 1,258.6 | 1.53 | **0.58** |

  The railing is 493.4 and 626.5, the gantry 790.2 and 989.5 on the same layouts. The wall has fewer of the brightest tones than the railing (`steel_2`/`steel_3` pixels: wall 272 and 340, railing 512 and 648, gantry 809 and 1,012). I read "front-row machine plates" as a machine standing on its plate, so the wall is 0.58 to 0.59 of it and the test asserts a ratio under 0.75. Against the bare plate sprite alone the wall is 1.5 to 1.6 times larger. **The Art Director must confirm the reading**; if the plate-only reading is meant, E1's fallback (the top band to `steel_1`) is a source-art change that this task may not make, so it stays an open decision.
- **SC-006 shuffled-order trial (9 of 10): not run here.** It needs independent blind sessions and the owner's timed trial. The three stage captures are the input for it. The parent runs it with T530/T546.

## 3. P2-F6: depot, deposit, hopper, collar and lamp

- No opaque pixel of any of the 12 depot sprites or the 4 deposit sprites lies in a badge, marker or frame ring, or on an overlay pixel, on any of the five layouts (35,670 pixels checked per layout against 5,973 to 18,139 ring and overlay pixels): `NoDepotOrDepositPixelEntersAC8RingOrAKeepOutAtAnyStageAndFillState`.
- Every depot variant keeps at least 2 art px of headroom above the lamp tip, draws at least 8 `copper_*` collar pixels and its `ink_0` hopper outline (`TheDepotKeepsItsLampHopperAndCopperCollarVisible...`).
- Visibility in the camera model (shell area = screen less the strip and the tab bar; the 200 % row doubles the chrome), 4 x 3 layout, depot on the right:

| Class | World area (points) | Scale | Depot (80 x 76 canvas with the lamp) fully in the fit-all view | After panning to the depot side |
|---|---|---|---|---|
| 640 x 360, 100 % | 640 x 256 | n = 1 | yes | yes |
| 640 x 360, 200 % | 640 x 152 | n = 1 | **no, recorded** | yes |

  At 640 x 360 and 200 % the depot is out of the fit-all view and reachable by panning, which is the recorded form the World Designer condition and P1-F2 allow ("an out-of-view class is recorded, not hidden"). The T534 camera owns whether to fit it.
- The vein chip loop (2/s alone) against other FX in view (3/s): not measured here. The vein and the FX pool are T528 and T529.

## 4. A-13: the C8 ring proof with the K4 tail over a trunk

On the densest frame of every layout, at every stage and every depot fill state (12 compositions per layout: 3 stages by 4 depot fills), every ring pixel lies in A_ov (`ring tones` over the base, deck and link layers), **every pixel that belongs to a new F006 layer is in set R (no `steel_*`, no `teal_1`, no `lit_*`)**, and the frame ring avoids `warn_0`, `plate_0` and `res_ore_0`. Ring pixels sampled per layout: 1,824 (3 x 4 and 4 x 3), 912 (six), 3,040 (twenty), 1,064 (L), all over the base, deck and link layers; none over a prop, edge, depot or deposit.

| Layout | Badge pixels (K4 tail included) lying over trunk or branch art | Ring tones on the lane (pixels) |
|---|---|---|
| 3 x 4 and 4 x 3 | 1,380 | `copper_1` 12, `dim_0` 36, `ink_0` 48, `ink_1` 24 |
| six | 690 | `copper_1` 6, `dim_0` 18, `ink_0` 24, `ink_1` 12 |
| twenty | 2,300 | `copper_1` 20, `dim_0` 60, `ink_0` 80, `ink_1` 40 |
| L | 805 | `copper_1` 7, `dim_0` 21, `ink_0` 28, `ink_1` 14 |

**Luminance over a lane (A-13, Art Direction F-2).** The `cue_over_lane_contrast` gate is palette-membership based. The luminance contrast, computed with `art_pipeline.color.contrast_ratio` on the palette values, is:

| Cue fill against | `dim_0` | `ink_1` | `ink_0` | `copper_1` | `copper_2` |
|---|---|---|---|---|---|
| `cue_warn` | 6.68 | 7.89 | 9.81 | 2.83 | 1.49 |
| `cue_alert` | 3.52 | 4.16 | 5.17 | 1.49 | 1.27 |
| `cue_info` | 6.45 | 7.62 | 9.47 | 2.73 | 1.44 |
| `cue_neutral` | 9.28 | 10.96 | 13.62 | 3.93 | 2.07 |
| `cue_dim` | 4.10 | 4.84 | 6.01 | 1.73 | 1.09 |
| `cvd_green` marker | 3.69 | 4.36 | 5.42 | 1.56 | 1.21 |
| `cvd_pink` marker | 4.12 | 4.87 | 6.05 | 1.74 | 1.08 |
| `ink_0` outline against the lane tone | 1.47 | 1.24 | 1.00 | 3.47 | 6.57 |

Over the `dim_0` lane body the fill carries the badge (3.5:1 for `cue_alert` to 9.3:1 for `cue_neutral`) and the 1 px `ink_0` outline only 1.2 to 1.5:1. The composite shows lane pixels under the K4 tail only as `copper_1`, `dim_0`, `ink_0` and `ink_1`, never `copper_2` (the active dashes are not under the badges in the static trunk and branch art). The warn badge over `copper_1` dashes is 2.83:1 by fill and is separated by its `ink_0` outline (3.47:1 against `copper_1`).
**These ratios are not in the gate**: adding them to `cue_over_lane_contrast` changes the check report that `art-source/f006/link/check-report.json` and `test_f006_link_fx.py` compare byte for byte, so it would need a regenerated art-source record, which this task may not touch. Proposal for the parent: add the tables above to the check in a later art task and regenerate the link record then.

## 5. Prop free area (the R06 mask measurement)

Free pixels per eligible cell (cell area 3,780), stage 0, every location locked (`TheFreeAreaOfThePropMaskIsMeasured...`). Free area is the cell minus every hit shape, keep-out ring, lane guard band, the depot, the deposit and the stage's edge pieces.

| Layout | Eligible cells | Margin cells: min / median / max free | Location cells: min / median / max free |
|---|---|---|---|
| 3 x 4 and 4 x 3 | 28 (16 margin, 12 location) | 245 / 3,002 / 3,780 | 430 / 782 / 1,224 |
| six | 18 (12 + 6) | 245 / 3,002 / 3,780 | 430 / 804 / 1,224 |
| twenty | 40 (20 + 20) | 245 / 3,002 / 3,780 | 430 / 493 / 1,224 |
| L | 34 (27 + 7) | 245 / 2,660 / 3,780 | 782 / 804 / 1,224 |

A location cell keeps its four corners outside the hit shape, which is where a prop on an empty or locked location can stand. Props placed: at most 0.5 per eligible cell (the authored densities are 0.20, 0.30 and 0.40).
