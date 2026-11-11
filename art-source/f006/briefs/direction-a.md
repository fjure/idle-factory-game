# Direction A: Open platform deck

**Brief version**: `F006-DB v0.1` (T506, Issue #681). Status: concept brief for the P0 owner decision (OD-9b); not production art.
**Cites**: World Bible `F006-WB v0.1` (frozen, OD-9a) and Art Bible `F004-PAB v1.0.4`. Palette: `f004-palette` 1.0.0 (cap 48). **No new hue**: every colour named below is a palette id.
**Covers**: FR-040, FR-044, FR-047; research R03, R17.
**Mapping to the task text**: this is the owner-recommended modular platform deck (spec A-3), called "C" in the T506 task text. Direction B is the walled hall.
**Sprites**: `art-source/f006/concept/direction-a/` (scripted route, repository-authored, no AI pixels). Regenerate from `tools/art-pipeline` with `python -m art_pipeline concept-f006 --out ../../art-source/f006/concept`.

## Idea

A flat working deck on a dark base, open to the air. Machines stand on locations that are plain deck diamonds; unbuilt locations show a hatch. Stage 1 (open deck) uses the railing edge. The walled yard and the gantry line (World Bible stages 2 and 3) add a wall and a gantry through the same ground family later. Mood words: quiet, methodical, dim-lit (World Bible section 3).

## Shape language and palette ids

- **Ground**: clean slab diamonds, hard-edged, no dithering. Field `floor_0`, deck `floor_1`, 1 px `floor_2` highlight on the two back edges only.
- **Hatch**: frame `steel_0`, leaf `dim_0`, slats `ink_1` along the 2:1 direction.
- **Edge**: front faces `floor_0` and `ink_1`; railing posts `steel_2` with `steel_3` caps, rail `steel_1`; outline `ink_0`.
- **Depot (open)**: slab `plate_2`/`plate_0`/`plate_1`; hopper `steel_2`/`steel_0`/`steel_1`; intake collar `copper_2`/`copper_0`/`copper_1`; corner posts and beam `steel_1`/`steel_2`; work lamp `lit_0`. A low open structure keeps the machines near it visible.
- **Vein**: `res_ore_1`, `res_ore_0`, `res_coal` on a `floor_1` patch. Scenery only (R23).
- **Props**: crate stack (`steel_*`, `copper_*`) and a work-lamp post (`steel_1`, `steel_2`, `lit_0`).
- **Lanes**: `dim_0`, `dim_1`, dashes `steel_2` (static trunks, R07).

## Dark-base conformance (open item from OD-9a)

**Finding: no conflict.** World Bible section 1 asks for a "neutral starter industrial platform on a dark base". Art Bible v1.0.4 already fixes the field as dark tones: `floor_0` #2a2f3a and `floor_1` #363c4a, with `floor_2` #444b5a only as 1 px highlights, and the camera clears to `ui_bg` #1b1f2a. The dark base is therefore `floor_0`/`floor_1` and needs no new value. Rules kept: body backdrop set `{plate_1, plate_2, ink_0, floor_0, floor_1}` (C5); occupied-plate rim `plate_2` (C2); selection-frame backdrop never `warn_0`, `plate_0` or `res_ore_0` (C3). The deck is unoccupied floor, so `plate_*` stay reserved for F004 machine plates. The ground family (deck, hatch, edge) is keyed by region id `starter-platform` (World Bible section 5); machines, depot, trunks and vein are ground-neutral.

## Sprites

| Sprite id | Size (px) | Pivot | Frames | Role |
|---|---|---|---|---|
| `deck-tile` | 70 x 54 | 35,27 | 1 | Period tile: field `floor_0`, 70 x 35 deck diamond `floor_1`. |
| `hatch-tile` | 70 x 54 | 35,27 | 1 | Deck with a hatch over a location not built yet. |
| `edge-tile` | 70 x 66 | 35,32 | 1 | Deck edge with railing; pivot = diamond centre. |
| `depot` | 80 x 72 | 40,50 | 1 | Central depot on a 56 x 28 slab. |
| `vein` | 48 x 34 | 24,24 | 1 | One ore vein. |
| `prop-crate`, `prop-lamp` | 30x36, 24x44 | see manifest | 1 | Two props. |
| `trunk-branch` | 70 x 54 | 35,27 | 1 | Static trunk lane with one branch. |
| `branch-pip` | 14 x 10 | 7,5 | 6 | Pip on the branch; loop, 12 fps. |
| `completion-pop` | 68 x 36 | 34,18 | 6 | One-shot 2:1 ring; reduced-motion = frame 3. |

## Manifest format (for T507 and the mockup compositor)

Each direction folder holds `manifest.json`, `provenance.json`, `check-report.json` and `pixel/<id>_<NN>.png`. `manifest.json` lists `sprites[]` with: `id`, `kind` (tile, structure, deposit, prop, lane, fx), `size` [w, h] art px, `ppu` 24, `pivotPx` [x, y] from the canvas top-left (the ground anchor; place it on the cell anchor), `footprintPx` [w, h] of the ground footprint, `fps`, `frameCount`, `stillFrame` (reduced-motion frame), `frames[]` (`file` relative to the folder, `sha256`) and `note`. Composite at an integer device scale of the art pixel, nearest neighbour. The lattice pitch is 70 x 54 art px; deck, hatch and trunk-branch are exactly one period with the diamond centre at (35, 27).

## Checks run on these sprites

Palette gate (`check_palette`: every opaque pixel is a palette id; groups limited to ink, machine, floor, resources, state_cues), canvas, alpha 0/255, frame count, and `check_flash` on the two animated strips (worst flashes per second 0, limit 3). Reports are in `check-report.json`. `lit_0`/`lit_1` appear only as lamp points and in the pip and pop accents (rule L-1). The pip changes by one pixel ring and the pop never flashes the frame.

## Strengths and risks for the owner decision

- Strengths: shortest path to the Art Bible and the existing lattice; machine plate-backdrop rules stay untouched; the open depot leaves the 12 locations readable at 640x360-class and 200 % text.
- Risks: the depot silhouette is lighter than direction B, so the lamp and the collar must carry the "centre" reading; the deck alone gives little stage differentiation until walls and gantries arrive.
