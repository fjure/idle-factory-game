# F004 Asset Briefs v1.0

**Brief version**: `F004-AB v1.0` (draft pending T426 owner sign-off; revision 1 by T461, see the revision log at the end)

**Governing Art Bible**: [`F004-PAB v1.0`](./F004-art-bible-v1.md) (draft). Every rule, colour, size and limit below comes from it and is not repeated in full. A row that conflicts with the Art Bible is a defect of the row.

**Status**: Authored by the Art Director author session (T424, Issue #536). It is a draft pending the owner sign-off of T426. The independent review of T425 is recorded in `specs/004-visual-identity/reviews/art-bible-review.md`; revision 1 (T461, Issue #615) applies its conditions R1 to R5, R7, R8, R11 and R15 that touch the brief. No production (T434 to T438) starts before T426. The author does not approve this brief.

**Feature**: `specs/004-visual-identity` (FR-002, FR-003, FR-021a, FR-022, FR-023; `contracts/art-pipeline.md`, `contracts/presentation-and-state-cues.md`; research R06, R08, R10, R18). Unity target `6000.6.0f1`, URP 2D Renderer.

**Format authority**: the row format extends [`F003-asset-briefs-v0.1.md`](./F003-asset-briefs-v0.1.md) (rows 1 to 29 are carried over; the 49 UI icon rows 30 to 78 are carried over in section 5). Data-model fields (`assetId`, `type`, `footprint`, `canvas`, `pivot`, `ports`, `format`, `shadow`, `overhang`, `cueAnchor`, `confusionPeers`, `perspective`, `states` with `frames` and `fps`, `paletteId` and `paletteVersion`, `lightDirection`, `route` with reason, `importSettings`, `artBibleVersion`, `generator`) are all present either per row or as a default in section 1.

## 1. Conventions and defaults

| Field | Default for every row unless the row says otherwise |
|---|---|
| `perspective` | `isometric-2-1` (world art); UI sprites have none |
| `paletteId`, `paletteVersion` | `f004-palette`, `1.0.0` |
| `lightDirection` | `top-left` (world art) |
| `artBibleVersion`, `briefVersion` | `F004-PAB v1.0`, `F004-AB v1.0` |
| `ports` | `none` for every row (no mark may imply a port, belt, cable, socket or power state) |
| `format` code `WORLD` | PNG RGBA, one file per frame, alpha 0 or 255, palette v1.0.0 only, PPU 24 (= D), point filter, no mipmaps, clamp, uncompressed RGBA32 in a per-family Sprite Atlas v2 (padding 2, no rotation, pages at most 1024 x 1024; Default, Standalone and Android overrides), canvas and pivot from the Art Bible section 3.2 |
| `format` code `UI` | as `WORLD`, no perspective, displayed by key through USS at 1 pt per art px, point filter, excluded from the world crispness rule |
| `shadow` | `none`: no cast or contact shadow baked into any sprite in v1.0 (flat body, lighting B) |
| `importSettings` | applied by `F004SpriteImportPostprocessor` from the manifest (T430); never by hand |
| `generator` | per route: Blender 5.2.1 + `rig-v1.blend`; ComfyUI v0.43.0 + Z-Image-Turbo + LoRA `0a5092d`; `tools/art-pipeline` scripted (Python 3.12 or 3.13, Pillow 11.3.0). Exact pins in the Art Bible section 14 |
| Keys | Every F003 key keeps its identifier (FR-002). New keys are additive and use the prefix `F004-` (section 3). Key ids are the catalog keys; a manifest `assetId` is the lowercase kebab form of the key plus the state where there is one (for example `f004-machine-miner-run`). |
| Paths | Sources and raw outputs: `art-source/f004/<family>/<asset>/`. Final PNGs: `Assets/IdleFactory/Art/F004/<family>/`, imported through `Assets/IdleFactory/Art/F004/import-manifest.json`. The spike sets `art-source/f004/spike/` and `Assets/IdleFactory/Art/F004/Spike/` are not production inputs. |
| Families (folders) | `machines`, `slots`, `floor`, `cues`, `markers`, `resources`, `items`, `ui-icons` |

Frame rule (Art Bible 6.4, 8.1): a machine has one entry with 6 frames at 8 fps for running; every other state uses frame 0 of the same entry and has no extra art. Cue, marker, label, plate and UI sprites have one frame.

## 2. Inventory: asset x state x frames (the 29 F003 rows)

| # | Key (`*` = content `presentationKey`) | Type | State x frames (fps) | Canvas | Footprint box (Art Bible 3.2a) | Pivot | Route | Family |
|---:|---|---|---|---|---|---|---|---|
| 1 | `F003-SLOT-EMPTY` | plate | empty x 1 | 80 x 64 | 68 x 30 | (40, 15) | blender | slots |
| 2 | `F003-SLOT-LOCKED-L2` | plate | locked x 1 (padlock and hatch; digit is `F004-LABEL-LOCK-L2`) | 80 x 64 | 68 x 30 | (40, 15) | blender | slots |
| 3 | `F003-SLOT-LOCKED-L3` | plate | locked x 1 (digit `F004-LABEL-LOCK-L3`) | 80 x 64 | 68 x 30 | (40, 15) | blender | slots |
| 4 | `F003-SLOT-LOCKED-L4` | plate | locked x 1 (digit `F004-LABEL-LOCK-L4`) | 80 x 64 | 68 x 30 | (40, 15) | blender | slots |
| 5 | `F003-SLOT-LOCKED-L5` | plate | locked x 1 (digit `F004-LABEL-LOCK-L5`) | 80 x 64 | 68 x 30 | (40, 15) | blender | slots |
| 6 | `F003-SLOT-OCCUPIED-BASE` | plate | occupied x 1 (a layer under a machine, never shown bare) | 80 x 64 | 68 x 30 | (40, 15) | blender | slots |
| 7 | `F003-SLOT-SELECTED-FRAME` | overlay | selected x 1 | 80 x 64 | 68 x 30 | (40, 15) | scripted | slots |
| 8 | `F003-MACHINE-MINER`* | machine body | running x 6 (8); waiting, missing-input, storage-full, paused, inactive x frame 0 of running | 80 x 64 | 68 x 30 | (40, 15) | blender | machines |
| 9 | `F003-MACHINE-SMELTER`* | machine body | same as row 8 | 80 x 64 | 68 x 30 | (40, 15) | blender | machines |
| 10 | `F003-MACHINE-PRESS`* | machine body | same as row 8 | 80 x 64 | 68 x 30 | (40, 15) | blender | machines |
| 11 | `F003-MACHINE-ASSEMBLER`* | machine body | same as row 8 | 80 x 64 | 68 x 30 | (40, 15) | blender | machines |
| 12 | `F003-BADGE-RUNNING` | badge layer | running: manifest entry `intentionallyEmpty`, no PNG, no frame, no renderer drawn | n/a | n/a | n/a | scripted (manifest only) | cues |
| 13 | `F003-BADGE-WAITING` | badge layer | waiting x 1 (bold hourglass, `cue_info` fill) | 80 x 64 | 68 x 30 | (40, 15) | scripted | cues |
| 14 | `F003-BADGE-MISSING-INPUT` | badge layer | missing input x 1 (arrow into tray, `cue_alert`) | 80 x 64 | 68 x 30 | (40, 15) | scripted | cues |
| 15 | `F003-BADGE-STORAGE-FULL` | badge layer | storage full x 1 (stacked bars, `cue_warn`) | 80 x 64 | 68 x 30 | (40, 15) | scripted | cues |
| 16 | `F003-BADGE-PAUSED` | badge layer | paused x 1 (two bars, `cue_neutral`) | 80 x 64 | 68 x 30 | (40, 15) | scripted | cues |
| 17 | `F003-BADGE-INACTIVE` | badge layer | inactive x 1 (ring with dash, `cue_dim`) | 80 x 64 | 68 x 30 | (40, 15) | scripted | cues |
| 18 | `F003-MARKER-BOOSTED` | marker layer | boosted x 1 (double up-chevron, `cvd_green`) | 80 x 64 | 68 x 30 | (40, 15) | scripted | markers |
| 19 | `F003-MARKER-OVERDRIVE` | marker layer | Overdrive x 1 (lightning bolt, `cvd_pink`) | 80 x 64 | 68 x 30 | (40, 15) | scripted | markers |
| 20 | `F003-RES-IRON-ORE`* | resource | normal x 1 | 32 x 24 | 20 x 10 | (16, 5) | comfy | resources |
| 21 | `F003-RES-IRON-BAR`* | resource | normal x 1 | 32 x 24 | 20 x 10 | (16, 5) | comfy | resources |
| 22 | `F003-RES-IRON-PLATE`* | resource | normal x 1 | 32 x 24 | 20 x 10 | (16, 5) | comfy | resources |
| 23 | `F003-RES-GEAR`* | resource | normal x 1 | 32 x 24 | 20 x 10 | (16, 5) | comfy | resources |
| 24 | `F003-ITEM-DIAMOND-DRILL-HEAD`* | item icon (Tier I) | normal x 1 | 32 x 32 | n/a | (16, 16) | comfy + scripted frame | items |
| 25 | `F003-ITEM-FLUX-CATALYST`* | item icon (Tier II) | normal x 1 | 32 x 32 | n/a | (16, 16) | comfy + scripted frame | items |
| 26 | `F003-ITEM-OVERCLOCK-CORE`* | item icon (Tier III) | normal x 1 | 32 x 32 | n/a | (16, 16) | comfy + scripted frame | items |
| 27 | `F003-ITEM-TIER-1` | tier chip | Tier I x 1 | 32 x 16 | n/a | (16, 8) | scripted | items |
| 28 | `F003-ITEM-TIER-2` | tier chip | Tier II x 1 | 32 x 16 | n/a | (16, 8) | scripted | items |
| 29 | `F003-ITEM-TIER-3` | tier chip | Tier III x 1 | 32 x 16 | n/a | (16, 8) | scripted | items |

The 11 `presentationKey` values are unchanged from the F003 key list (4 machines, 4 resources, 3 items). Item names stay as in F003 (Tier II and III names remain Art Director proposals of F003 and may be renamed only through a revision of this brief).

Decisions recorded for the parent and T425:

- **Running badge** (owner decision R8, 2026-10-29): `F003-BADGE-RUNNING` is a manifest entry flagged `intentionallyEmpty: true` with no PNG. It keeps SC-003 (every F003 key resolves to F004 art) and needs no special case in `BadgeFor`. Owners: T437 (the entry), T430 and T440 (importer and `F004AssetContractTests` accept the flag), T429 (the view draws nothing for it). The `outline`, `cue_icon_size`, `canvas`, `pivot` and `frames` checks do not apply to it.
- **Machine bodies**: no dimmed, desaturated or warning body variants. S3 = b keeps the body unchanged (Art Bible 8.1). The body treatments mentioned in the T434 task text do not apply.
- **Locked plates**: the four `F003-SLOT-LOCKED-L<n>` keys use identical pixels. T430 may alias them to one sprite in the catalog; the level digit is a separate overlay label (section 3).
- **Spike resources**: the spike drew only Ore and Ingot; Plate and Gear are new at the same canvas (32 x 24, footprint 20 x 10).

## 3. Additive `F004-` keys

| Key | Type | State x frames (fps) | Canvas | Footprint | Pivot | Route | Reason |
|---|---|---|---|---|---|---|---|
| `F004-ACCENT-MINER` | lighting accent layer | running x 6 (8) | 80 x 64 | 68 x 30 | (40, 15) | scripted | lighting B accent (Art Bible 2.1); opaque palette pixels over the body, same frames as the loop |
| `F004-ACCENT-SMELTER` | same | running x 6 (8) | 80 x 64 | 68 x 30 | (40, 15) | scripted | same |
| `F004-ACCENT-PRESS` | same | running x 6 (8) | 80 x 64 | 68 x 30 | (40, 15) | scripted | same |
| `F004-ACCENT-ASSEMBLER` | same | running x 6 (8) | 80 x 64 | 68 x 30 | (40, 15) | scripted | same |
| `F004-LABEL-LOCK-L2` to `F004-LABEL-LOCK-L5` (4 keys) | text label layer | 1 frame each, digit `2` to `5`, authored 3 x 5 font at scale 3 (a 9 x 15 digit, cap 15 art px; owner decision R4), `ui_text` | 80 x 64 | 68 x 30 | (40, 15) | scripted | cap height is at least 12 pt on every class at `n_min`, so it is shown (Art Bible 8.4); the slot panel also names the level |
| `F004-FLOOR-TILE` | floor tile | 1 frame (section 6) | by T435 from the T456 lattice | 2:1 diamond, even width and height | footprint centre, whole px | blender | floor family of R08 |
| `F004-UI-ICON-SETTINGS` | UI icon | 1 frame, gear | 24 x 24 | n/a | (12, 12) | scripted | the HUD gear that opens the Settings sheet (FR-020a); not in the F003 inventory |

Background: no sprite. The world camera clears to `ui_bg` (Art Bible 5). A background illustration is not part of v1.0.

## 4. Per-asset production fields

`ports` is `none` and `shadow` is `none` for every row (section 1) and not repeated. `overhang`: the Art Bible 3.4 limits; a machine body starts at canvas row 34 (0 px), a plate may reach 12 px (spike 3 to 5 px), badges and other overlays are exempt from the overhang check and reach 24 px above the footprint top.

| Key(s) | format | overhang | cueAnchor | confusionPeers | Route and reason |
|---|---|---|---|---|---|
| `F003-SLOT-EMPTY`, `-LOCKED-L2` to `-L5`, `-OCCUPIED-BASE` | WORLD | plate up to 12 px (spike 3 to 5) | none | the other plate conditions; the empty plate and the locked plate are the pair a bare-plate tile confuses (S2 naming); occupied is only ever seen with a machine (Art Bible 8.5) | blender: plates share the camera and key light of the machines; coherent perspective |
| `F003-SLOT-SELECTED-FRAME` | WORLD | up to 12 px (spike 3 px) | none | none (unique frame shape); never over a badge | scripted: exact shape, `ui_accent` and ink |
| `F003-MACHINE-MINER`* | WORLD | 0 px (rows at or below 34); keep-out of rows 0..33, columns 15..71 | badge anchor (59, 33), canvas px; markers M1 x 24..34 and M2 x 36..46, rows 16..26 | Assembler (measured IoU 0.651 full, 0.672 at 32 px, at most 0.70), Smelter and Press (unmeasured) | blender: the same camera and light across 6 frames; measured IoU and reproducibility at D24 |
| `F003-MACHINE-ASSEMBLER`* | WORLD | 0 px (spike top row 34) | as above | Miner (as above), Smelter, Press | blender, as above |
| `F003-MACHINE-SMELTER`* | WORLD | 0 px; chimney inside the 30 px body height | as above | Miner, Assembler, Press (all unmeasured; T434 measures all six pairs) | blender, as above |
| `F003-MACHINE-PRESS`* | WORLD | 0 px | as above | Miner, Assembler, Smelter (unmeasured) | blender, as above |
| `F004-ACCENT-*` (4) | WORLD | 0 px | none (shares the body anchor) | none | scripted from the body frames: deterministic and palette-exact (spike `accent_layer`) |
| `F003-BADGE-WAITING` | WORLD | overlay, bbox x 48..70, y 10..32 | pivot (40, 15); box bottom-centre (59, 33) | missing-input, storage-full, paused, inactive (blind naming 9/9; grayscale luma of waiting and storage-full differ by 2, so shape separates them) | scripted: exact shapes, byte-identical, CVD rule |
| `F003-BADGE-MISSING-INPUT` | WORLD | as above | as above | storage-full (reviewer hard pair), waiting | scripted |
| `F003-BADGE-STORAGE-FULL` | WORLD | as above | as above | missing-input (reviewer hard pair), waiting | scripted |
| `F003-BADGE-PAUSED` | WORLD | as above | as above | inactive (reviewer hard pair; separated by glyph and brightness) | scripted |
| `F003-BADGE-INACTIVE` | WORLD | as above | as above | paused (as above) | scripted |
| `F003-BADGE-RUNNING` | none (`intentionallyEmpty` manifest entry, no PNG) | none | none | none (running is read by absence) | scripted: a manifest entry only |
| `F003-MARKER-BOOSTED` | WORLD | overlay, 11 x 11 (named deviation DEV-3 below 24 pt; naming blocking in T439 and T445) | M1 x 24..34, y 16..26 (proposal fixed by T437, accepted by T429; 2 rows clear of the plate apex at row 29) | Overdrive marker | scripted |
| `F003-MARKER-OVERDRIVE` | WORLD | overlay, 11 x 11 (DEV-3) | M2 x 36..46, y 16..26 (same) | boosted marker | scripted |
| `F004-LABEL-LOCK-L2` to `-L5` | WORLD | overlay | digit centred on the footprint | the other digits | scripted: bitmap digits |
| `F003-RES-IRON-ORE`* | WORLD (list display 32 x 24 pt) | up to 12 px (spike 3) | none | Gear, Bar (spike drew Ore and Ingot, both identifiable at `n_min`) | comfy: small illustrative object; no frame coherence needed |
| `F003-RES-IRON-BAR`* | WORLD | up to 12 px (spike 4) | none | Plate (both flat grey iron: trapezoid ingot vs square plate), Ore | comfy |
| `F003-RES-IRON-PLATE`* | WORLD | up to 12 px | none | Bar, Gear | comfy |
| `F003-RES-GEAR`* | WORLD | up to 12 px | none | Plate, Ore | comfy |
| `F003-ITEM-DIAMOND-DRILL-HEAD`*, `-FLUX-CATALYST`*, `-OVERCLOCK-CORE`* | UI | n/a | none | the other two tiers: triple tier cue (circle, square, triangle frame; 1, 2, 3 pips; `I`, `II`, `III`) | comfy for the pictogram, then a scripted step adds the tier frame, pips and numeral (recorded as touch-up in provenance) |
| `F003-ITEM-TIER-1`, `-2`, `-3` | UI | n/a | none | the other chips | scripted: exact tier frames |
| `F004-FLOOR-TILE` | WORLD | n/a | none | none | blender: shares the camera and the plates' ramp |

Contract-bound facts: the 29 F003 rows and the 10 additive `F004-` keys (section 3) are the full keyed set; the IoU, palette, canvas, pivot, alpha, outline, frame count, overhang, flash, contrast and cue-size checks of `contracts/art-pipeline.md` apply to every row, with the contrast rules C1 to C8 of the Art Bible section 9 (ink-or-rim composite-mask rule at 90 %, allowed backdrop tone sets under badges, markers and the frame). Accent rows (`F004-ACCENT-*`) follow the mask rules L-1 to L-5 of Art Bible 2.1: mask inside the body mask, no outline, exempt from `check_outline`. The footprint column of the inventory is the footprint box; the plate diamond (70 x 35) is the hit-test shape (Art Bible 3.2a).

## 5. UI icons per surface (F003 rows 30 to 78 plus the Settings gear)

All rows: format `UI`, canvas 24 x 24 art px, pivot (12, 12), one frame, colour `ui_text` tinted by tokens (enabled `--text-primary`, disabled `--text-muted`, selected or accent `--accent`; the glyph colour in the file is `ui_text`), strokes at least 2 art px, at most 6 primitives, route `scripted`. Reason for the route (Art Bible 12): every glyph is a simple shape in one colour, tinted at runtime, with a CVD-safe meaning by shape; no row is illustrative at 24 px. A row moves to `comfy` only by a brief revision. `ports` and `shadow` are `none`; `overhang` and `cueAnchor` do not apply. Surfaces are those of F003 `contracts/ui-surfaces.md`, plus the HUD and tab bar.

| # | Key | Surface | Glyph (carried over from F003; redrawn at 24 px) | confusionPeers |
|---:|---|---|---|---|
| 30 | `F003-UI-ICON-CASH` | HUD top strip | coin: circle with a vertical bar and a cross bar | `TAB-MARKET` (coin) |
| 31 | `F003-UI-ICON-RP` | HUD top strip, Research | flask outline with a rising dot | `EFFECT-*` flasks, `TAB-RESEARCH` |
| 32 | `F003-UI-ICON-IP` | HUD, Prestige, Industry | factory roof silhouette over four short radial lines | `TAB-FACTORY` |
| 33 | `F003-UI-ICON-FV` | HUD, Prestige | three bars in ascending height | `EFFECT-OUTPUT` |
| 34 | `F003-UI-ICON-STORAGE` | HUD store usage, Inventory and rates | open crate: rectangle with a top opening line | `DELIVER` |
| 35 | `F003-UI-ICON-EXPAND-FACTORY` | Expansion | square with four outward corner arrows | none |
| 36 | `F003-UI-ICON-TAB-FACTORY` | tab bar | factory silhouette: block plus chimney | `IP` |
| 37 | `F003-UI-ICON-TAB-MARKET` | tab bar | coin over an up-right arrow | `CASH`, `AUTOSELL` |
| 38 | `F003-UI-ICON-TAB-CONTRACTS` | tab bar | document with a folded corner and a check line | `ACCEPT`, `ABANDON` |
| 39 | `F003-UI-ICON-TAB-ITEMS` | tab bar | cube with three visible-face lines | none |
| 40 | `F003-UI-ICON-TAB-RESEARCH` | tab bar | node graph: three circles joined by lines | `RESEARCH-NODE-*` |
| 41 | `F003-UI-ICON-TAB-PRESTIGE` | tab bar | up arrow through a ring | `UPGRADE`, `RETRY` |
| 42 | `F003-UI-ICON-INDUSTRY` | Industry | gear with a plus | `F004-UI-ICON-SETTINGS` |
| 43 | `F003-UI-ICON-BUILD` | Build sheet, fresh-run hint | hammer: T-shape at 45 degrees | none |
| 44 | `F003-UI-ICON-UPGRADE` | Machine panel | up chevron over a bar | `PRIORITY-UP` |
| 45 | `F003-UI-ICON-REMOVE` | Machine panel, confirmation | bin: rectangle with a lid line | `CANCEL` |
| 46 | `F003-UI-ICON-TOGGLE-ON` | Machine panel | pill with the knob on the right, filled | `TOGGLE-OFF` (shape: fill and knob side differ) |
| 47 | `F003-UI-ICON-TOGGLE-OFF` | Machine panel | pill with the knob on the left, outlined | `TOGGLE-ON` |
| 48 | `F003-UI-ICON-AUTOSELL` | Inventory and rates | coin with a circular arrow | `CASH`, `RETRY` |
| 49 | `F003-UI-ICON-PRIORITY-UP` | Priorities | up triangle over a bar | `UPGRADE`, `PRIORITY-DOWN` |
| 50 | `F003-UI-ICON-PRIORITY-DOWN` | Priorities | down triangle over a bar | `PRIORITY-UP` |
| 51 | `F003-UI-ICON-DRAG-HANDLE` | Priorities | six dots in two columns | none |
| 52 | `F003-UI-ICON-ACCEPT` | Contracts | arrow entering a document | `DELIVER` |
| 53 | `F003-UI-ICON-DELIVER` | Contracts | crate with a right arrow | `ACCEPT`, `STORAGE` |
| 54 | `F003-UI-ICON-ABANDON` | Contracts | document with a diagonal strike | `TAB-CONTRACTS` |
| 55 | `F003-UI-ICON-ACTIVATE` | Items | play triangle inside a ring | `OVERDRIVE-READY` |
| 56 | `F003-UI-ICON-CONFIRM` | dialogs (Prestige, Remove, Abandon, Items) | check mark | `RESEARCH-NODE-OWNED` |
| 57 | `F003-UI-ICON-CANCEL` | dialogs | X mark | `REMOVE` |
| 58 | `F003-UI-ICON-RETRY` | Prestige failure, Startup, invalid clock | circular arrow with a head at the top right | `AUTOSELL`, `SPINNER` |
| 59 | `F003-UI-ICON-CLOSE` | every sheet | chevron down over a bar | `PRIORITY-DOWN` |
| 60 | `F003-UI-ICON-INFO` | exact values, details | circle with a dot and a bar (lowercase i) | `WARNING` |
| 61 | `F003-UI-ICON-WARNING` | rejection, errors, Settings "Not saved" note | triangle with an exclamation bar | `INFO` |
| 62 | `F003-UI-ICON-LOCK` | locked or disabled requirement text | padlock: body plus shackle | `OVERDRIVE-LOCKED`, `RESEARCH-NODE-LOCKED` |
| 63 | `F003-UI-ICON-TIMER` | Contracts (timed), Items countdowns | clock: circle with two hands | `OVERDRIVE-COOLDOWN` |
| 64 | `F003-UI-ICON-OFFLINE` | Offline summary | crescent moon beside a small square | none |
| 65 | `F003-UI-ICON-IMPORT` | Startup and import status | arrow entering a box | `ACCEPT` |
| 66 | `F003-UI-ICON-SPINNER` | Startup, saving (static frame, rotated by USS) | three-quarter ring with a gap | `RETRY` |
| 67 | `F003-UI-ICON-OVERDRIVE-READY` | Overdrive control (HUD) | bolt outline | `OVERDRIVE-ACTIVE` |
| 68 | `F003-UI-ICON-OVERDRIVE-ACTIVE` | Overdrive control | filled bolt inside a ring | `OVERDRIVE-READY` |
| 69 | `F003-UI-ICON-OVERDRIVE-COOLDOWN` | Overdrive control | bolt outline with a clock corner | `TIMER` |
| 70 | `F003-UI-ICON-OVERDRIVE-LOCKED` | Overdrive control | bolt outline with a padlock corner | `LOCK` |
| 71 | `F003-UI-ICON-RESEARCH-NODE-LOCKED` | Research graph | node circle with a padlock | `LOCK` |
| 72 | `F003-UI-ICON-RESEARCH-NODE-AVAILABLE` | Research graph | node circle with a plus | `BUILD` |
| 73 | `F003-UI-ICON-RESEARCH-NODE-OWNED` | Research graph | node circle with a check and a level tick | `RESEARCH-NODE-MAXED` |
| 74 | `F003-UI-ICON-RESEARCH-NODE-MAXED` | Research graph | node circle with a check and a double ring | `RESEARCH-NODE-OWNED` |
| 75 | `F003-UI-ICON-EFFECT-SPEED` | Research, Items, Industry effect lists | double right chevron | none |
| 76 | `F003-UI-ICON-EFFECT-OUTPUT` | effect lists | up arrow over two stacked squares | `FV` |
| 77 | `F003-UI-ICON-EFFECT-SALE-PRICE` | effect lists | price tag | none |
| 78 | `F003-UI-ICON-EFFECT-UPGRADE-COST` | effect lists | down arrow beside a coin | `CASH` |
| new | `F004-UI-ICON-SETTINGS` | HUD top strip (opens the Settings sheet, FR-020a) | gear (24 x 24; distinct from `INDUSTRY`: no plus, eight teeth) | `INDUSTRY` |

Selection of icons by effect kind, contract resource and control state (F003 AD-8) is unchanged: Research, Industry and contract surfaces carry no `presentationKey` in the content contract, so presentation code picks the icons above by effect kind (`EFFECT-*`), contract resource (resource sprite plus `TIMER` for the timed Bar) and control state (`OVERDRIVE-*`, `RESEARCH-NODE-*`). The Settings sheet reuses `CLOSE`, `WARNING` (the "Not saved" icon, so the note is never colour only) and the new gear.

Surfaces without an icon row: empty states are text only (F003 brief). `F003-UI-ICON-*` placed beside text on `--bg`, `--surface` or `--surface-raised` follow the contrast rules C4 and C6 of the Art Bible and are checked by T441.

## 6. Floor tiling rule

| Rule | Value |
|---|---|
| Geometry | 2:1 diamond, width and height even (pivot rule P1), edges on the 2:1 line |
| Period | the tile repeats on the T456 lattice period; T435 proposes the size, T456 confirms it; all world positions are whole art pixels (Art Bible 3.3, P3) |
| Seamlessness | no seam, no tone step at the shared edges; wrap-around checked by tiling a 3 x 3 sheet and comparing edge columns |
| Tones | field in `floor_0` and `floor_1`; `floor_2` only as 1 px edge highlights; no `plate_*` tone on the floor |
| Detail | at most 2 detail clusters per tile, none within 2 art px of an edge; no pattern period shorter than the tile (no shimmer beacons) |
| Contrast | the backdrop under badge, marker and frame boxes must lie in the allowed tone sets of rules C1 and C3 (`floor_2` and `plate_0` are forbidden there); `floor_*` contrast values are in the Art Bible 9.4 |
| Lit layers | the floor never sits on a lit layer (Art Bible L-5): no accent or Light2D target covers the floor or plate layers |
| Light | flat; no baked shadow, no gradient; lighting accents never cover the floor |
| Background beyond the floor | camera clear colour `ui_bg` |

## 7. Production chain and review per family

Order per family (`contracts/art-pipeline.md`): brief, production, independent second-session Art Director review, Technical Art import, in-game validation. Reviews use fresh sessions; the producer, brief author and spike Art Director never review (research R18). Each family needs the cross-family cohesion review at 1x against the style-anchor sheet (Art Bible 13) before approval.

| Family | Producer task | Route | Lease | Checks (all in `check-report.json`) |
|---|---|---|---|---|
| machines and accents | T434 | blender, scripted | gpu | palette and groups, canvas, pivot, alpha, outline, frames, overhang and keep-out, flash on the body-plus-accent composite (at most 2 per second), frame 0 lights-on on the body (L-2), accent mask inside the body mask (L-4), IoU for all six pairs, composite-mask contrast C5 at 90 % |
| slots and floor | T435 | blender, scripted frame | gpu | palette, canvas, pivot, alpha, outline, overhang, contrast C2, tiling |
| resources and items | T436 | comfy, scripted tier frame | gpu | palette, canvas, pivot (P1), alpha, outline, contrast C6, provenance with LoRA licence and training-data note |
| cues, markers, labels, selection frame | T437 | scripted | none | palette, canvas, pivot, alpha, cue size (at least 12 x 12; shipped 23 x 23), contrast C1 and C3 with the C8 backdrop acceptance, the `intentionallyEmpty` running-badge entry, shuffled grayscale and CVD sheets (markers included) |
| UI icons | T438 | scripted | none | palette, canvas, pivot, alpha, stroke width, contrast C4 on the three token surfaces |
| review | T439 | none | none | second-session Art Director, UI/UX Reviewer (cue legibility) and Game Designer/PO (readability) |
| import | T440 | none | unity | `F004AssetContractTests` |

Provenance for every row follows the Art Bible section 15; a row without a complete record does not get a manifest entry.

## 8. Coverage cross-check and acceptance checklist for T425

| F003 surface or state | F004 rows |
|---|---|
| Slot: empty, locked (level), occupied, selected | rows 1 to 7, `F004-LABEL-LOCK-L2..L5` |
| Machine: running, paused, waiting, missing-input, storage-full, inactive | rows 8 to 17 and `F004-ACCENT-*` |
| Machine: boosted, Overdrive | rows 18 and 19 |
| Overdrive control: ready, active, cooldown, locked | rows 67 to 70 |
| Research node: locked, available, owned, maxed | rows 71 to 74 |
| Item tiers I, II, III (icon and chip) | rows 24 to 29 |
| Contract kinds, timed Bar | rows 20 to 23, 52 to 54, 63 |
| Currencies, store, expansion, offline, startup | rows 30 to 35, 64 to 66 |
| Tab bar (six tabs) | rows 36 to 41 |
| Settings (HUD gear, "Not saved") | `F004-UI-ICON-SETTINGS`, `CLOSE`, `WARNING` |
| Floor and background | `F004-FLOOR-TILE`; background colour `ui_bg` |

Checklist for T425:

- [ ] The inventory has exactly 29 F003 rows (7 + 4 + 6 + 2 + 4 + 3 + 3), unique keys, and the 11 `presentationKey` rows are unchanged.
- [ ] Section 5 has the 49 F003 UI icon rows 30 to 78 plus one new key; every F003 UI surface maps to a row or to text only.
- [ ] Every canvas, footprint and pivot satisfies the evenness rule P1 and every pivot is a whole art pixel.
- [ ] No row introduces a dimmed, desaturated or warning body variant.
- [ ] Every row names a route with a reason; no family uses a route outside the Art Bible section 12.
- [ ] Marker and label placement, the `intentionallyEmpty` running-badge entry and the lattice constraints are carried by named tasks (T437, T429, T430, T440, T456).

## 9. Prohibited scope

This brief creates no asset, Unity import, `.meta` file, atlas, prefab, scene, material, shader, animation, audio or gameplay configuration. It does not edit `tasks.md`, the wiki or any spec. Generation and production start only after T426.

## 10. Traceability

- `specs/004-visual-identity/spec.md` FR-002, FR-003, FR-009, FR-010, FR-013 to FR-015, FR-021a, FR-022, FR-023, SC-001, SC-002, SC-003.
- `contracts/art-pipeline.md` (inventory, brief fields, checks, provenance additions); `contracts/presentation-and-state-cues.md` (catalog keys, cue mapping); `data-model.md` (asset brief fields); research R06, R08, R10, R18.
- `docs/art/F003-asset-briefs-v0.1.md` (the 78 carried-over rows) and `docs/art/F003-provisional-art-bible-v0.1.md` (superseded section by section in the Art Bible section 17).
- `specs/004-visual-identity/reviews/style-spike.md` (S1 to S4 decisions and measurements; every T424 item is mapped in the Art Bible).

## 11. Revision log

| Revision | Task | Change |
|---|---|---|
| 1.0 draft | T424 (#536) | first draft, merged in PR #613 |
| 1.0 draft, revision 1 | T461 (#615) | per the T425 review and the owner decisions of 2026-10-29: running badge is an `intentionallyEmpty` manifest entry with no PNG (R8); lock digit at scale 3, cap 15 art px (R4); marker rows 16..26 and DEV-3 (R7); footprint box and hit diamond (R3); contrast rules C1 to C8 and the lit-layer rule (R1, R2, R5); T434 flash headroom of 2 per second (R15). Version stays v1.0. |
