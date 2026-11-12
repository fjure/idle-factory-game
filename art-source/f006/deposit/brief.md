# F006-DEPOSIT asset brief (`F006-DEPOSIT v0.1`)

Task T520 (Issue #693). Cites World Bible `F006-WB v0.1` (one ore vein, scenery only), Art Bible `F004-PAB v1.1.0` sections 21.2 (DEPOSIT and FX vein chip rows), 21.4, 21.6, 21.7, 21.8 item 2 (flash), research R17 and R23, and the Game Systems note (the vein and chip loop are scenery, no pacing effect). Route: Blender rig-v2, deterministic pixel pass, provenance. Driver: `art-source/f006/pipeline_depot_deposit.py`.

## Sprites

| Id | Canvas | Frames | Tones | Role |
|---|---|---|---|---|
| `f006-deposit-rest-s1`, `-s2`, `-s3` | 48 x 34, footprint 48 x 24, pivot (24, 12) | 1 each | `ink_0`, `floor_1` patch, `res_ore_0`, `res_ore_1`, `res_coal` | vein scenery; stage dressing 1 (3 ore chunks, 2 coal), 2 (+1 chunk, +1 coal), 3 (+spire, +far chunk, +coal) |
| `f006-deposit-highlight` | 48 x 34 | 1 | rest plus `steel_3` 1 px rim | static still, the reduced-motion state of the chip loop (21.7) |
| `f006-fx-vein-chip` | 48 x 34 | 6 at 8 fps | `res_ore_1`, `res_ore_0`, `steel_3` (impact spark in frame 0), `ink_0` | manual-mining chip loop on the deposit (R23), same anchor as the deposit |

Reduced motion (C5): the chip loop is replaced by `f006-deposit-highlight`; the zero-flash gate then holds trivially. Flash: `check_flash` on the 6 chip frames and on the chips composited over the rest vein, both recorded in `check-report.json`; at most 3 per second overall, F006 FX at most 2 per second (21.8 item 2). The loop is not combined with a machine loop.

The vein sits one lattice cell outside Bounds (P1-F2); whether it is in view on each screen class is a camera matter (T534). The art is legible at its native 48 x 34 size (the review sheets show it at 1x and 4x).
