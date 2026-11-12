# F006-PROP asset brief (`F006-PE v0.1`)

Task T521 (Issue #694). Cites World Bible `F006-WB v0.1` (props are scenery only, section 6), Art Bible `F004-PAB v1.1.0` sections 21.2 (PROP row), 21.4, 21.8 items 1 and 5, research R06 (props at most 24 art px tall) and R15, owner decision OD-9b (B's teal drum and bunker are not adopted; crate and lamp are A's). Palette `f004-palette` 1.0.0, ids only. Route: Blender rig-v2, deterministic pixel pass, provenance. Driver: `art-source/f006/pipeline_prop_edge.py`; models: `prop/build_models.py`.

The task text says static lamps "in steel or teal"; the signed-off Art Bible 21.8 items 1 and 5 supersede it: the lamp has a `copper_2` head and a `steel_3` cap, no `teal_*` on any prop, never `lit_*`.

| Sprite | Canvas | Pivot (base centre) | Tones | Role |
|---|---|---|---|---|
| `f006-prop-crate-small` | 24 x 20 | (12, 15) | `steel_0..2`, `copper_0..2` | one crate, 20 px diamond, 8 px tall, copper cross strap |
| `f006-prop-crate-stack` | 30 x 24 | (15, 18) | `steel_0..2`, `copper_0..2` | two crates on the ground and one on top |
| `f006-prop-lamp-post` | 14 x 24 | (7, 22) | `steel_0..3`, `copper_1`, `copper_2` | static lamp: plinth, post `steel_2`, head `copper_2` / `copper_1`, cap `steel_3` |
| `f006-prop-pallet` | 30 x 16 | (15, 10) | `steel_1`, `dim_0`, `dim_1` | flat pallet, `dim_0` slat gaps |

Heights including the `ink_0` outline: 19, 24, 23 and 15 px, all at most 24. The pivot is the centre of the base diamond; the footprint is the base width by twice the base-centre height above the canvas bottom. Allowance (21.2 PROP row): margin tones only (`steel_*`, `copper_*`, `dim_*`, `ink_*`); inside the Extent only set R, so these props stand on the margin ring or in an R-only cell after a C8 proof with the prop drawn (composite gate T527). No `teal_*`, no `lit_*`. Static: the lamp never flickers. At most 12 colours per sprite (measured 4 to 8 including `ink_0`). Sheet: `prop/sheets/prop-all-sprites.png`.
