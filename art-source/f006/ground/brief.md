# F006-GROUND and F006-FOUNDATION asset brief (`F006-GF v0.1`)

Task T519 (Issue #692). Cites World Bible `F006-WB v0.1` (neutral starter platform on a dark base; ground swappable, keyed by region id `starter-platform`, no lore), Art Bible `F004-PAB v1.1.0` sections 8.5 (amendment), 21.1, 21.2, 21.4, 21.5, and owner decision OD-9b (direction A, open deck, no wall in stage 1). Palette `f004-palette` 1.0.0, ids only. Route: Blender rig-v2 (flat emission palette materials), deterministic pixel pass, provenance. Driver: `art-source/f006/pipeline_ground_foundation.py`.

## Sprites (Art Bible 21.4)

| Id | Canvas | Pivot | Tones | Role |
|---|---|---|---|---|
| `f006-ground-base` | 70 x 54 opaque | (35, 27) | `floor_0` | margin and base field |
| `f006-ground-deck` | 70 x 54 opaque | (35, 27) | `floor_0`, `floor_1` | unlocked location: 70 x 35 deck diamond (rows 10..44) on the field |
| `f006-ground-hatch` | 70 x 54 opaque | (35, 27) | `floor_0`, `floor_1`, `dim_0`, `ink_0`, `ink_1` | locked location: hatch with `ink_0` outline, `dim_0` frame, `floor_0` leaf, `ink_1` slats |
| `f006-foundation-outline` | 70 x 36 | (35, 18) | `ink_0`, `floor_0` | empty unlocked location, floor_0 ring with corner marks, transparent interior; the build highlight is a tint, not a sprite |
| `f006-foundation-flat` | 70 x 36 | (35, 18) | `ink_0`, `plate_2`, `plate_1` | occupied location, flat 70 x 35 diamond, no wall, no plinth (R06, FR-012) |

Tile period 70 x 54, placement on whole art pixels (ground key `starter-platform`, Art Bible 21.5). Deck and foundation diamonds are the same 70 x 35 shape (rows 4, 8, ..., 68, 70, 68, ..., 4 px wide), so a foundation at the deck pivot covers the deck diamond exactly.

## Hatch, padlock and digit (section 8.5 amendment)

The padlock and level digit stay an overlay (`ui_text` on `dim_0`, C9, digit cap 15 art px). The hatch keeps a slat-free `dim_0` pad (36 x 18 px diamond at the tile centre) as its backing; `ui_text` on `dim_0` is 11.55:1. No `steel_0` frame and no `floor_2` highlight (both forbidden behind a badge, C1).

## Edge, corner and T tiles

The Art Bible 21.4 table has no ground edge, corner or T sprite: the OD-9b edge style is the railing/wall/gantry EDGE family (`f006-edge-*`, task T521). T519 therefore draws exactly the five sprites of the table; adding ground edge tiles would change the table (an owner decision).

## Allowance and proofs

Every sprite uses only ids in the Art Bible 21.2 rows GROUND and FOUNDATION; all of them are in the ring-safe set R, so no `steel_*` pixel can enter a C8 ring from these layers. No `lit_*`, `teal_*`, `plate_0`. At most 12 colours per sprite (measured 2 to 6). Checks: palette gate, canvas, pivot, alpha 0/255, frames, allowance, set R, tiling lattice (5 x 5, zero tone steps), edge ring, 70 x 35 diamond rows, reproducibility (second headless render byte-identical). Static art: no animation, no flash.
