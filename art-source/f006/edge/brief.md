# F006-EDGE asset brief (`F006-PE v0.1`)

Task T521 (Issue #694). Cites World Bible `F006-WB v0.1` (stages Open deck, Walled yard, Gantry line; props and edges are scenery only), Art Bible `F004-PAB v1.1.0` sections 21.1, 21.2 (EDGE rows), 21.4, 21.5, 21.6, 21.7 and 21.8 items 1 and 5, owner decision OD-9b (Art Director conditions 1 to 5, 8, 9; UI/UX F6; AD A4) and P1 follow-up P1-F5. Palette `f004-palette` 1.0.0, ids only. Route: Blender rig-v2 (flat emission palette materials), deterministic pixel pass, provenance. Driver: `art-source/f006/pipeline_prop_edge.py`; models: `edge/build_models.py`.

## Sprites (Art Bible 21.4)

Each sprite is one 68 x 34 px deck diamond (`floor_0` top, grown to the 70 x 35 diamond by the outline) with a 12 px fascia on the outer edges and the stage structure on those edges. `fl` = front-left edge (left apex to front apex), `fr` = front-right edge (front apex to right apex), `corner` = both, meeting at the front apex. Pivot = the deck diamond centre (x 35, row 37 / 49 / 55 from the top of the 66 / 78 / 84 px canvas); the footprint is the canvas width by 2 x 29 px so the manifest pivot is the deck centre. Outline: `ink_0` around the silhouette, omitted along the two back edges and along a fascia-free front edge, where the tile continues into its neighbour.

| Stage | Sprites | Canvas | Tones | Structure |
|---|---|---|---|---|
| 1 Open deck | `f006-edge-railing-fl` / `-fr` / `-corner` | 70 x 66 | `floor_0`, `ink_1`, `steel_1`, `steel_2`, `steel_3` | posts `steel_2` with `steel_3` caps (3.6 px wide, 8 px tall), rail `steel_1`; posts at both ends of the tile edge and at the middle |
| 1 Open deck, quiet | `f006-edge-railing-quiet-fl` / `-quiet-fr` | 70 x 66 | same | end post only, no middle post (UI/UX F6, A4): used near badges and the depot |
| 2 Walled yard | `f006-edge-wall-fl` / `-fr` / `-corner` | 70 x 78 | `floor_0`, `floor_1`, `ink_1`, `steel_0`, `steel_1`, `steel_2` | one tile family: low wall 14 px tall, top `steel_2`, outer faces `steel_1` (left) and `steel_0` (right), `floor_1` plinth, `ink_1` panel seams |
| 3 Gantry line | `f006-edge-gantry-fl` / `-fr` / `-corner` | 70 x 84 | `floor_0`, `ink_1`, `steel_0..3`, `dim_0`, `copper_1` | new art: posts `steel_1` / `steel_0` with `steel_3` caps (28 px), top beam `steel_3` / `steel_2` / `steel_1`, lower rail `steel_0`, braces `dim_0`, `copper_1` hazard dashes |

The railing post pattern is fixed: it does not grow in stages 2 and 3 (A4), and the post height is identical in all five railing sprites (checked). No `lit_*`, no `teal_*`, no `plate_*`, no `floor_2`. At most 12 colours per sprite (measured 5 to 9 including `ink_0`). Static art: no animation, no flash.

## Visible growth (Art Director condition 4, World Designer)

Topmost opaque pixel above the deck centre at the apex, outline included: railing 10 px, wall 16 px, gantry 30 px (global check `visible_growth_railing_lt_wall_lt_gantry`). Stage 1 has no wall and no gantry tone (steel_0, floor_1, dim_0 and copper_1 are absent from the railing sprites; tested). Sheet: `edge/sheets/edge-stage-triptych.png` (stage 1, 2, 3, a mock front of a platform with the V-shaped boundary of corner, fl and fr pieces along the diagonals, two ground deck rows above, props in panel 1) and `edge/sheets/edge-all-sprites.png`. The triptych illustrates the pieces only; it is not a composition (T527).

## Keep-out and C8 evidence (Art Bible 21.5, 21.8 item 1; P1-F5)

`edge/keepout-proof.json`, produced by the driver from the lattice geometry and the A-03 anchor module for the layouts 3 x 4, 4 x 3, 6, 20 and L: each edge sprite is placed at the pivot of every margin cell and its opaque pixels are intersected with the Extent rectangle, every hit shape (clipped to its cell), the overlay keep-out (rows 0..33, columns 15..71 of the machine canvas), the 1 px C8 rings of badges (baseline K0 and the K4 pointer tail) and markers, the selection frame, the machine body and the plate. Result:

- **Front margin row** (below the Extent): zero overlap with every mask and at least 13 px from any mask, for all 11 sprites and all five layouts. Walls and gantries are drawn here.
- **Back margin row**: overlaps the Extent, hit shapes, keep-out and rings. Not drawn there (a tile that fails is not drawn on that edge).
- **Left and right margin rows**: zero overlap but 1 to 2 px from the machine column. Walls and gantries are not drawn there ("never next to a machine column").

So no wall or gantry pixel lies in a ring, and no `steel_*` pixel of this family lies in a ring. What remains for the composite gate (T525, T527): the pixel ring proof on the densest 12-slot frame of every screen class with the final composed world layers, and the margin-cell composition (which margin cells carry which piece, the depot and deposit cells). The stage-2 Wall default in `edgeRulePerStage` stays provisional until that gate (P1-F5).

## Cost per stage (Art Director condition 9, 21.6)

Stage 1: 5 railing frames. Stage 2: 3 wall frames (one tile family). Stage 3: 3 gantry frames (a separate art pass). Total 11, as the world atlas table.
