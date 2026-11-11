# Direction B: Walled hall

**Brief version**: `F006-DB v0.1` (T506, Issue #681). Status: concept brief for the P0 owner decision (OD-9b); not production art.
**Cites**: World Bible `F006-WB v0.1` (frozen, OD-9a) and Art Bible `F004-PAB v1.0.4`. Palette: `f004-palette` 1.0.0 (cap 48). **No new hue**: every colour named below is a palette id.
**Covers**: FR-040, FR-044, FR-047; research R03, R17.
**Mapping to the task text**: this is the alternative called "A, a walled hall" in the T506 task text. Direction A is the platform deck.
**Sprites**: `art-source/f006/concept/direction-b/` (scripted route, repository-authored, no AI pixels). Regenerate from `tools/art-pipeline` with `python -m art_pipeline concept-f006 --out ../../art-source/f006/concept`.

## Idea

The same dark base and the same deck diamonds, but the world reads as a low walled hall: a back wall encloses the factory and the depot is a bunker. It makes the walled yard (World Bible stage 2) the identity from the start. Mood words: quiet, methodical, steady (World Bible section 3). The ground family stays keyed by region id `starter-platform`; machines, depot, trunks and vein remain region-neutral.

## Shape language and palette ids

- **Ground**: slab diamonds with panel seams. Field `floor_0`, deck `floor_1`, seams `floor_0`, 1 px `floor_2` highlight on the back edges only.
- **Hatch**: frame `steel_0`, leaf `dim_0`, slats `ink_1` (as direction A).
- **Edge**: 12 px back wall: top `steel_2`, faces `steel_1` (left) and `steel_0` (right); deck front faces `floor_0`/`ink_1`; outline `ink_0`.
- **Depot (bunker)**: slab `plate_2`/`plate_0`/`plate_1`; block `steel_1`/`steel_0`/`dim_1`; roof lip `steel_2`; door hatch `teal_0`; lamp `lit_0`.
- **Vein**: `res_ore_1`, `res_ore_0`, `res_coal` on a `floor_1` patch. Scenery only (R23).
- **Props**: crate stack (`steel_*`, `copper_*`) and a drum (`teal_0`, `teal_1`, `steel_1`).
- **Lanes**: `dim_0`, `dim_1`, dashes `steel_2`.

## Dark-base conformance (open item from OD-9a)

**Finding: no conflict.** World Bible section 1 asks for a "neutral starter industrial platform on a dark base". Art Bible v1.0.4 already fixes the field as dark tones: `floor_0` #2a2f3a and `floor_1` #363c4a, with `floor_2` #444b5a only as 1 px highlights, and the camera clears to `ui_bg` #1b1f2a. The dark base is `floor_0`/`floor_1`; no new value. Rules kept: C5 body backdrop set `{plate_1, plate_2, ink_0, floor_0, floor_1}`; C2 plate rim `plate_2`; C3 frame backdrop never `warn_0`, `plate_0`, `res_ore_0`. The wall tones (`steel_0`, `steel_1`, `steel_2`) are not in the C5 body backdrop set; wall pixels must therefore never sit directly behind a machine body or a selection frame, which is the main risk below.

## Sprites

| Sprite id | Size (px) | Pivot | Frames | Role |
|---|---|---|---|---|
| `deck-tile` | 70 x 54 | 35,27 | 1 | Period tile with panel seams. |
| `hatch-tile` | 70 x 54 | 35,27 | 1 | Deck with a hatch over a location not built yet. |
| `edge-tile` | 70 x 66 | 35,32 | 1 | Deck edge with back wall; pivot = diamond centre. |
| `depot` | 80 x 72 | 40,50 | 1 | Bunker depot on a 56 x 28 slab. |
| `vein` | 48 x 34 | 24,24 | 1 | One ore vein. |
| `prop-crate`, `prop-drum` | 30x36, 24x44 | see manifest | 1 | Two props. |
| `trunk-branch` | 70 x 54 | 35,27 | 1 | Static trunk lane with one branch. |
| `branch-pip` | 14 x 10 | 7,5 | 6 | Pip on the branch; loop, 12 fps. |
| `completion-pop` | 68 x 36 | 34,18 | 6 | One-shot 2:1 ring; reduced-motion = frame 3. |

## Manifest format (for T507 and the mockup compositor)

Identical to direction A: each folder holds `manifest.json`, `provenance.json`, `check-report.json` and `pixel/<id>_<NN>.png`. `sprites[]` entries carry `id`, `kind`, `size` [w, h] art px, `ppu` 24, `pivotPx` [x, y] from the canvas top-left (ground anchor), `footprintPx`, `fps`, `frameCount`, `stillFrame`, `frames[]` (`file`, `sha256`) and `note`. Composite at an integer device scale, nearest neighbour. Lattice pitch 70 x 54 art px; deck, hatch and trunk-branch are one period with the diamond centre at (35, 27).

## Checks run on these sprites

Palette gate (every opaque pixel is a palette id; groups limited to ink, machine, floor, resources, state_cues), canvas, alpha 0/255, frame count, and `check_flash` on the two animated strips (0 flashes per second, limit 3). Reports are in `check-report.json`.

## Strengths and risks for the owner decision

- Strengths: a stronger depot silhouette and a clear walled identity; the wall frames the factory and hides the empty margin.
- Risks: back walls can hide machines and the badge band (Art Bible 3.4 and 3.5 keep-out) and need an added keep-out proof; the bunker depot hides the hopper that shows delivery; the wall is the World Bible stage-2 element, so it can pre-empt the stage progression of R15.
