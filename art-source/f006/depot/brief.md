# F006-DEPOT asset brief (`F006-DEPOT v0.1`)

Task T520 (Issue #693). Cites World Bible `F006-WB v0.1` (one depot, trunks end at the depot, scenery rules), Art Bible `F004-PAB v1.1.0` sections 21.1, 21.2 (DEPOT row), 21.4, 21.6, 21.7, 21.8 (A3, lamp tone, C8 strict), owner decision OD-9b (direction A open depot frame with copper collar; the closed bunker depot of direction B is not adopted). Palette `f004-palette` 1.0.0, ids only. Route: Blender rig-v2 (flat emission palette materials), deterministic pixel pass, provenance. Driver: `art-source/f006/pipeline_depot_deposit.py`.

## Sprites (12, Art Bible 21.4)

Canvas 80 x 76, footprint 80 x 40 (the slab ground diamond), pivot (40, 20 from the bottom). `f006-depot-s{1,2,3}-{empty,low,high,full}`.

| Part | Tones |
|---|---|
| Slab top / rim / side (A3: steel, no `plate_*`) | `steel_1` / `steel_2` / `steel_0` |
| Hopper collar (copper) and opening interior | `copper_0`, `copper_1` (bands, ring top), `dim_0` |
| Fill (empty, low, high, full) | `res_ore_0`, `res_ore_1`, `res_coal`; full adds a `copper_2` lip |
| Work lamp (static, no `lit_*`) | `copper_2` head, `steel_3` cap, `steel_1`/`steel_2` post |
| Posts | `steel_1`, `steel_3` caps |
| Outline | `ink_0` |

Stage variants: s1 open frame (two corner posts); s2 adds a low parapet on the two back edges (steel); s3 adds a gantry beam with a `copper_1` hoist block between a corner post and the lamp post. No `lit_*`, no `teal_*`, no `plate_*`, no `cue_*`. At most 12 colours per sprite (the full state uses 12).

## Visibility rules (21.7, Art Director condition 7)

The hopper opening is 12 x 6 art px or larger in the empty state (checked), the `copper_*` collar and the fill show the delivery, and the lamp tip is at least 2 art px below the canvas top (checked). Visibility per screen class is a camera matter (T534, P1-F2): the depot sits one cell outside Bounds.

## C8

The depot is a margin cell outside every hit shape and ring; it contains `steel_*` by allowance. The ring proof on the densest frame is the P2 composite gate (T527); no tone from this family enters a ring because it is never drawn inside one.
