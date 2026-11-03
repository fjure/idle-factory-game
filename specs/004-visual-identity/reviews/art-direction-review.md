# F004 T439: Independent Art Direction Review of the produced art families

**Task**: T439 (Issue #553), contract `contracts/art-pipeline.md` step 3. Covers FR-022, SC-004.
**Reviewer session**: second-session Art Director (primary), with the UI/UX Reviewer lens (cue legibility) and the Game Designer/Product Owner lens (readability). MAQA worker for Issue #553, branch `maqa/issue-553-t439-art-direction-review`, Claude Sonnet 5.5 (Medium tier, reasoning high). Review date 2026-11-03.
**Independence**: this session did not produce, brief or spike any reviewed asset. It read the Art Bible `F004-PAB v1.0` (including section 13, the style-anchor sheet), the brief `F004-AB v1.0`, the contract, the QA comments of #548 to #552, #632 and #635, and the collected questions in the comments of #553. Read-only: no art, tool, doc, provenance or manifest file was edited.
**Reviewed state**: origin/main `9a70483`. Machines, plates and floor are revision 2 (rig-v2, true 2:1, commit `07bc651`).

**Provenance `reviewedBy` is NOT yet filled.** Every `provenance.json` under `art-source/f004/` still carries `reviewedBy` null or empty (checked on all 88 files). This review does not write it (assignment rule). The per-asset table below is the input for the later step that fills `reviewedBy.{session, date}` with: role "Art Director (second session) + UI/UX Reviewer + Game Designer/PO", session "T439, Issue #553, branch maqa/issue-553-t439-art-direction-review", date 2026-11-03, and the verdict of the asset's row. An asset with verdict REJECT must not receive a `reviewedBy`.

Method notes:
- Blind first. For every shuffled sheet (`sheets*/sheet_{grayscale,deuteranopia,protanopia,tritanopia}.png`) the sheet was viewed at its committed 1x size, the tiles were named, the naming was written down (scratch note), and only then the answer key (`sheet_key.json`, `answer-key/`) was opened and the naming scored. For the machines, slots and cues the key sits in `sheets/sheet_key.json`; it was not opened before naming.
- For the UI icon sheet only the grayscale sheet was named; the three CVD sheets of that family are monochrome white glyphs (no hue carries meaning), so they add no information beyond the grayscale pass. The tinted token-surface sheet was inspected visually.
- Cohesion was checked at 1x on a composite built in the session scratch directory from the committed PNGs (four machines on the occupied plate, bare machines with accents, plates in four conditions plus floor tile, resources, items and chips, badges, markers). No production anchor sheet (Art Bible section 13) is committed; see finding F-08. Review images were not added to the repository.

## 1. Summary verdict per family

| Family | Producer, revision | Verdict | One-line reason |
|---|---|---|---|
| Machines (4 bodies, 4 accents) | T434, re-rendered by T466 (#548/#635), rev 2, `07bc651` | APPROVE WITH CONDITIONS | Blind naming 32/32; strongest family; conditions are a wording ratification (flat-emission Lighting B) and thin Assembler rim margin |
| Slot plates (empty, occupied base, locked L2 to L5) and selection frame | T435 re-rendered by T466 (#549/#635) rev 2 `07bc651`; frame T437 `78524d8` | APPROVE WITH CONDITIONS | Blind naming 16/16; flat plates accepted; locked plate keyhole reads as the digit "1" (F-01) and is not "dark" (F-02) |
| Floor tile | T435/T466, rev 2, `07bc651` | APPROVE WITH CONDITIONS | Calm, tiles cleanly; needs the no-outline exemption sentence and T456's confirmation of the 60 x 30 period |
| Cues: 5 badges and the `intentionallyEmpty` running badge | T437 (#551), rev 1, `78524d8` | APPROVE | Blind naming 20/20 on the badges in all four filters; shape-first; saturated fills; zero confusions |
| Markers (Boosted, Overdrive) | T437, rev 1, `78524d8` | APPROVE | DEV-3 blocking naming gate met at 1x: 8/8 across four sheets; 1280 x 720 at n = 2 stays with T445 |
| Lock labels L2 to L5 | T437, rev 1, `78524d8` | APPROVE WITH CONDITIONS | Legible at cap 15 art px; label backdrop set is not written in the Art Bible (OD-9) |
| UI icons (50) | T438 (#552), rev 1, `c436bac` | APPROVE WITH CONDITIONS | Blind naming 36/50 (72 %) without context or labels; confusable clusters named below; most causes are in the brief, not the drawing |
| Resources (Ore, Bar, Plate, Gear) | T436 (#550), rev 1, `f849b87` | APPROVE WITH CONDITIONS | Blind naming 16/16; the ore is small and speckled; the gear is drawn face-on |
| Items (3 pictograms) | T436, rev 1, `f849b87` | APPROVE WITH CONDITIONS | Blind naming 12/12 but the drill head and overclock core are weak pictograms; tier frame carries the tier |
| Tier chips (3) | T464 (#632), rev 1, `a8bf3f8` (+ `c65e70e` derived-metadata exception) | APPROVE | Triple redundant tier cue (shape, pips, numeral) reads in grayscale and all CVD filters |

No asset is REJECTED. Every condition below is either a recorded follow-up with a named owner or an owner decision. None blocks the T440 import, with one exception the parent should weigh: F-01 (locked-plate keyhole) must be fixed or explicitly accepted before the T445 SC-001 sheets are built, because the locked slot is one of the four named slot conditions.

## 2. Blind naming results and scores

Seeds are those recorded in the keys (machines 434, slots 435, cues and markers 437, resources 436, items 4360, chips 4640, ui-icons 438). Naming was identical on all four filters unless stated.

| Sheet | Tiles | Filters | Result | Confusions |
|---|---|---|---|---|
| Machines (4 machines, each as body and as body plus accent) | 8 | gray, deut, prot, trit | 8/8 on each, **32/32** | none |
| Slots (empty, selected with machine, occupied, locked L3) | 4 | 4 | 4/4 each, **16/16** | none. Selected was named from the frame plus machine; the frame is a thin double line in grayscale |
| Cues (5 badges, 2 markers) | 7 | 4 | 7/7 each, **28/28**; markers 8/8, badges 20/20 | none. Markers named as "double up-chevron" (Boosted) and "lightning bolt" (Overdrive) in all four filters, including tritanopia where Boosted turns teal and Overdrive pink |
| Resources (Ore, Bar, Plate, Gear) | 4 | 4 | 4/4 each, **16/16** | none |
| Items (drill head, flux catalyst, overclock core) | 3 | 4 | 3/3 each, **12/12** | none; weak evidence, only three tiles |
| Tier chips (I, II, III) | 3 | 4 | 3/3 each, **12/12** | none; the numeral and pips alone separate them |
| UI icons (50 glyphs, 8 columns) | 50 | gray (CVD sheets monochrome) | **36/50 (72 %)** | 14 misses: accept, tab-prestige, effect-output, close, ip, research-node-owned, tab-market, cash, fv, abandon, autosell, storage, industry, priority-down. Some hits were hedged guesses (toggle-on, priority-up, research-node-available). All 14 misses and the ring cluster below are in-context icons that always sit next to a label or a known surface; they are not part of SC-001 |

Reading of the UI icon misses (UI/UX lens): the misses fall into three clusters.
1. Arrow-over-bar cluster: `effect-output`, `priority-up`, `upgrade`, `close`, `priority-down`, `import`, `storage` share one visual idea (an arrow or chevron over a bar). The brief itself lists `close` and `priority-down` as confusion peers.
2. Ring cluster: `info`, `cash`, `timer`, `activate`, `research-node-*`, `overdrive-active` are all a ring around an interior glyph. Computed mask IoU at 24 px: cash/info 0.84, info/research-node-available 0.84, info/timer 0.81, activate/research-node-available 0.81 (20 of 1225 pairs above 0.70). Only the interior glyph separates them; blind naming still got info, timer and activate right.
3. Abstract currency and concept glyphs: `cash` (coin with a cross reads as a cross), `ip` (roof over radial lines reads as a burst on a hill), `fv` (three ascending bars reads as signal strength), `autosell` (coin with circular arrow reads as a swirl), `tab-market`, `abandon` (document with a strike reads as a slashed square), `storage` (open crate reads as "minus in a box"), `industry` (gear with plus reads as settings or upgrade).
All of these follow their brief rows (the brief mandates "coin: circle with a vertical bar and a cross bar", "chevron down over a bar", and so on), so the cause sits in the brief, not in the producer's execution.

## 3. Per-asset table

Revision = `provenance.json` revision; commit = commit that last produced the asset pixels. Verdicts: APPROVE, APPROVE WITH CONDITIONS (AWC), REJECT. Finding and decision ids refer to sections 5 and 6. `reviewedBy` is not filled for any row.

| Key | Family | Revision / commit | Verdict | Reason |
|---|---|---|---|---|
| `F004-MACHINE-MINER-RUN` | machines | 2 / `07bc651` | AWC | Blind 8/8; IoU worst 0.667; flash 0/s; needs OD-1 (flat emission ratified) |
| `F004-MACHINE-ASSEMBLER-RUN` | machines | 2 / `07bc651` | AWC | Blind 8/8; rim share 0.917 on the real plate (threshold 0.90, thin margin, F-09); OD-1 |
| `F004-MACHINE-SMELTER-RUN` | machines | 2 / `07bc651` | AWC | Blind 8/8; chimney reads at 1x; OD-1 |
| `F004-MACHINE-PRESS-RUN` | machines | 2 / `07bc651` | AWC | Blind 8/8; smallest body mass (F-10); OD-1 |
| `F004-ACCENT-MINER-RUN` | machines | 2 / `07bc651` | AWC | Accent subset of body, lit tones only; OD-1 |
| `F004-ACCENT-ASSEMBLER-RUN` | machines | 2 / `07bc651` | AWC | As above; OD-1 |
| `F004-ACCENT-SMELTER-RUN` | machines | 2 / `07bc651` | AWC | As above; OD-1 |
| `F004-ACCENT-PRESS-RUN` | machines | 2 / `07bc651` | AWC | As above; OD-1 |
| `F003-SLOT-EMPTY` | plates | 2 / `07bc651` | APPROVE | Corner marks and centre mark read as "open plate" 16/16; flat 70 x 35 diamond (OD-3) |
| `F003-SLOT-OCCUPIED-BASE` | plates | 2 / `07bc651` | APPROVE | Plate under a machine; only shown with a body (Art Bible 8.5) |
| `F003-SLOT-LOCKED-L2` | plates | 2 / `07bc651` | AWC | Pixel-identical to L3, L4, L5; F-01 (keyhole reads "1"), F-02 (not dark) |
| `F003-SLOT-LOCKED-L3` | plates | 2 / `07bc651` | AWC | As L2 |
| `F003-SLOT-LOCKED-L4` | plates | 2 / `07bc651` | AWC | As L2 |
| `F003-SLOT-LOCKED-L5` | plates | 2 / `07bc651` | AWC | As L2 |
| `F003-SLOT-SELECTED-FRAME` | selection frame | 1 / `78524d8` | APPROVE | `ui_accent` double line hugging the 70 x 35 diamond; named from frame plus machine in grayscale and CVD |
| `F004-FLOOR-TILE` | floor | 2 / `07bc651` | AWC | 60 x 30 tiles without gaps; no ink outline (OD-5); period confirmation T456 (F-17) |
| `F003-BADGE-MISSING-INPUT` | cues | 1 / `78524d8` | APPROVE | Arrow into tray, warm alert fill, named 4/4 |
| `F003-BADGE-STORAGE-FULL` | cues | 1 / `78524d8` | APPROVE | Stack of bars, `cue_warn`, named 4/4 despite gray luma near waiting |
| `F003-BADGE-WAITING` | cues | 1 / `78524d8` | APPROVE | Bold hourglass (T460 revision), cool fill, named 4/4 |
| `F003-BADGE-PAUSED` | cues | 1 / `78524d8` | APPROVE | Two bars, neutral white, named 4/4 |
| `F003-BADGE-INACTIVE` | cues | 1 / `78524d8` | APPROVE | Ring with dash, `cue_dim`, named 4/4 |
| `F003-BADGE-RUNNING` | cues | 1 / `78524d8` | APPROVE | `intentionallyEmpty` manifest entry, no PNG, by owner decision R8; read by absence |
| `F003-MARKER-BOOSTED` | markers | 1 / `78524d8` | APPROVE | Double up-chevron `cvd_green`, 11 x 11 (DEV-3); named 4/4 |
| `F003-MARKER-OVERDRIVE` | markers | 1 / `78524d8` | APPROVE | Lightning bolt `cvd_pink`, 11 x 11 (DEV-3); named 4/4; bolt is the least legible glyph at 11 px, still correct blind |
| `F004-LABEL-LOCK-L2` | lock labels | 1 / `78524d8` | AWC | Authored 3 x 5 digit at scale 3 (cap 15); OD-9 (backdrop rule) |
| `F004-LABEL-LOCK-L3` | lock labels | 1 / `78524d8` | AWC | As L2 |
| `F004-LABEL-LOCK-L4` | lock labels | 1 / `78524d8` | AWC | As L2 |
| `F004-LABEL-LOCK-L5` | lock labels | 1 / `78524d8` | AWC | As L2 |
| `F003-RES-IRON-ORE` | resources | 1 / `f849b87` | AWC | Small (about 12 x 16) and speckled interior reads dither-like at 1x (F-04) |
| `F003-RES-IRON-BAR` | resources | 1 / `f849b87` | APPROVE | Clear iso ingot, correct ramp order |
| `F003-RES-IRON-PLATE` | resources | 1 / `f849b87` | APPROVE | Flat 2:1 diamond sheet, distinct from the bar; low detail but unambiguous |
| `F003-RES-GEAR` | resources | 1 / `f849b87` | APPROVE | Face-on round cog (not foreshortened to 2:1); still named 4/4 (note F-04b) |
| `F003-ITEM-DIAMOND-DRILL-HEAD` | items | 1 / `f849b87` | AWC | Reads as funnel or spinning top; weak pictogram (F-05) |
| `F003-ITEM-FLUX-CATALYST` | items | 1 / `f849b87` | APPROVE | Flask with amber liquid; clear |
| `F003-ITEM-OVERCLOCK-CORE` | items | 1 / `f849b87` | AWC | Small diamond with a spark inside a triangle frame; generic (F-05) |
| `F003-ITEM-TIER-1` | tier chips | 1 / `a8bf3f8` | APPROVE | Circle, one pip, numeral I |
| `F003-ITEM-TIER-2` | tier chips | 1 / `a8bf3f8` | APPROVE | Square, two pips, numeral II |
| `F003-ITEM-TIER-3` | tier chips | 1 / `a8bf3f8` | APPROVE | Triangle, three pips, numeral III |
| `F003-UI-ICON-OVERDRIVE-ACTIVE` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-RESEARCH-NODE-AVAILABLE` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-ACCEPT` | ui-icons | 1 / `c436bac` | AWC | blind miss; arrow into document reads as import or deliver (F-06 cluster 3); resolve in T441 in-context test |
| `F003-UI-ICON-TAB-PRESTIGE` | ui-icons | 1 / `c436bac` | AWC | blind miss; up arrow through a ring is abstract (F-06 cluster 3); resolve in T441 in-context test |
| `F003-UI-ICON-REMOVE` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-EFFECT-OUTPUT` | ui-icons | 1 / `c436bac` | AWC | blind miss; arrow-over-bar cluster with priority-up and upgrade (F-06 cluster 1); resolve in T441 in-context test |
| `F003-UI-ICON-INFO` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-CLOSE` | ui-icons | 1 / `c436bac` | AWC | blind miss; chevron over bar reads as collapse or download, brief peer of priority-down (cluster 1); resolve in T441 in-context test |
| `F003-UI-ICON-UPGRADE` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F004-UI-ICON-SETTINGS` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-TIMER` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-TOGGLE-ON` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-IP` | ui-icons | 1 / `c436bac` | AWC | blind miss; roof over radial lines reads as a burst (cluster 3); resolve in T441 in-context test |
| `F003-UI-ICON-PRIORITY-UP` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-RESEARCH-NODE-LOCKED` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-OFFLINE` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-OVERDRIVE-READY` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-LOCK` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-WARNING` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-ACTIVATE` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-TAB-FACTORY` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-TAB-RESEARCH` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-BUILD` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-IMPORT` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-EFFECT-SALE-PRICE` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-DRAG-HANDLE` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-RESEARCH-NODE-OWNED` | ui-icons | 1 / `c436bac` | AWC | blind miss; ring cluster, check inside a ring with a bar (cluster 2); resolve in T441 in-context test |
| `F003-UI-ICON-TAB-MARKET` | ui-icons | 1 / `c436bac` | AWC | blind miss; coin over arrow is abstract (cluster 3); resolve in T441 in-context test |
| `F003-UI-ICON-TOGGLE-OFF` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-EXPAND-FACTORY` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-CASH` | ui-icons | 1 / `c436bac` | AWC | blind miss; coin with a cross reads as a cross; ring cluster IoU 0.84 with info (OD-14); resolve in T441 in-context test |
| `F003-UI-ICON-DELIVER` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-FV` | ui-icons | 1 / `c436bac` | AWC | blind miss; three ascending bars read as signal strength (cluster 3); resolve in T441 in-context test |
| `F003-UI-ICON-TAB-ITEMS` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-TAB-CONTRACTS` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-EFFECT-UPGRADE-COST` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-OVERDRIVE-LOCKED` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-OVERDRIVE-COOLDOWN` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-CONFIRM` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-ABANDON` | ui-icons | 1 / `c436bac` | AWC | blind miss; document with strike reads as a slashed square (cluster 3); resolve in T441 in-context test |
| `F003-UI-ICON-AUTOSELL` | ui-icons | 1 / `c436bac` | AWC | blind miss; coin with circular arrow reads as a swirl (cluster 3); resolve in T441 in-context test |
| `F003-UI-ICON-PRIORITY-DOWN` | ui-icons | 1 / `c436bac` | AWC | blind miss; down triangle over bar, brief peer of close (cluster 1); resolve in T441 in-context test |
| `F003-UI-ICON-STORAGE` | ui-icons | 1 / `c436bac` | AWC | blind miss; open crate reads as a minus in a box (cluster 3); resolve in T441 in-context test |
| `F003-UI-ICON-RETRY` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-INDUSTRY` | ui-icons | 1 / `c436bac` | AWC | blind miss; gear with plus reads as settings or upgrade (cluster 3); resolve in T441 in-context test |
| `F003-UI-ICON-CANCEL` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-RESEARCH-NODE-MAXED` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-SPINNER` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-RP` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |
| `F003-UI-ICON-EFFECT-SPEED` | ui-icons | 1 / `c436bac` | APPROVE | Named blind on the 50-icon sheet; 24 x 24, stroke at least 2 px, tint contrast per C4 |

## 4. Cross-family cohesion review at 1x

Reference: Art Bible section 13 asks for a style-anchor sheet generated from approved outputs. No production anchor sheet is committed (F-08). The review therefore used an ad hoc 1x composite from the committed PNGs on `floor_0`, viewed at 1x and at 2x nearest-neighbour. Lens: shared ramp and outline rules (Art Bible section 6), one top-left key light, one projection, one density, palette discipline.

What holds across the families:
- One 1 px `ink_0` outline on every world silhouette and the same hard-edged ramps in machines, plates, resources, items and badges. No family has a coloured outline.
- Machines, plates and floor share the same projection after T466 (true 2:1). Plate diamond 70 x 35 equals the selection frame silhouette, so the frame fits.
- Value hierarchy at 1x is correct: floor (darkest, low-contrast texture), plate (mid grey-blue), machine (copper and teal accents, the only warm and cool saturation in the world), then badges (saturated fills, deliberately the loudest elements). This is the intended read order: state, then machine, then plate, then floor.
- Badges and markers sit in the overlay band above the footprint and do not touch the plate apex (marker rows end at row 26).
- The UI glyph set is one colour and one stroke weight, visibly a different layer from the world art, as designed (AD-6).

Drift observed (all within tolerance, none rejects a family):
1. Lighting model differs in kind between machines and plates. Machines carry three face tones (top light, left mid, right dark) and read as solid volumes; the plates are flat (no wall) and the resources Plate and Ore carry almost no face ramp. At 1x the plates read as stickers under volumetric machines. This is acceptable and cheap; see OD-3.
2. The Gear resource is drawn face-on as a round cog, while the Bar and Plate sit in 2:1. The resources appear only in UI lists (brief), where the projection mismatch is invisible. Not rejected.
3. The Ore interior uses a speckle pattern with two brown tones that reads as dithering at 1x (Art Bible 6.3 forbids dithering; no check detects a pattern). It is also the smallest sprite. It is the one resource that looks generated rather than modelled (F-04).
4. The Press body has the smallest mass of the four machines and reads as an anvil more than an H-beam press; the Smelter is the tallest. Hierarchy of body sizes is uneven but within the IoU and keep-out rules (F-10).
5. The locked plate is a different visual idea from the other plate conditions (dark hatch field plus a padlock with a keyhole glyph) and is the busiest plate; it still reads as locked in grayscale (F-01, F-02).
6. Items and tier chips use `res_gold` and the bronze tone for the frames; they match the resource family and the ink rule. Chips and item frames agree in shape language (circle, square, triangle).

Cohesion verdict: the world families (machines, plates, floor, cues, markers, labels) cohere at 1x. Resources and items cohere with them in outline and palette but read as a separate, UI-layer family, which matches their intended use. The UI icon family is intentionally separate. No family is rejected back to its producer for drift.

## 5. Findings

Severity: High blocks approval, Medium needs an action before the named later gate, Low is a recorded follow-up, Info is a note. Owner of the action is a producer task, the owner (decision), T440, T441, T445 or T456.

| # | Sev | Asset | Evidence | Required action | Owner |
|---|---|---|---|---|---|
| F-01 | Medium | `F003-SLOT-LOCKED-L2..L5` | Composite and pixel dump of the locked plate: the padlock sits left of centre; its keyhole (a vertical line on a base) reads as the digit "1" at 1x and in all four filters, next to the overlay digit (for example "1" beside "3"). The label bbox x 35..45 does not overlap the padlock (it ends at about x 34), so the geometry is clean, but the keyhole competes with the level digit. The blind sheet was still named "locked" 4/4 | Redraw the keyhole as a round or two-pixel-wide mark that cannot be read as a digit, and re-run the T435 checks. If the owner prefers to accept it, record the acceptance. Needed before the T445 SC-001 sheets | producer task (T435 producer follow-up) or owner acceptance |
| F-02 | Low | `F003-SLOT-LOCKED-*` | Art Bible 8.5 says "dark plate with hatch and a padlock". Measured mean luma 104 (locked) against 107 (empty) and 119 (occupied): the plate is not darker; "dark" is carried by the hatch field and the padlock | Amend the 8.5 wording (OD-6). No re-render | owner, T440 (doc patch) |
| F-03 | Low | plates | After T466 the plates have no wall (a true 2:1 70 x 35 diamond leaves no room for the former 3 px wall). Blind naming 16/16, selection frame fits | Accept flat plates for v1.0 (OD-3) | owner |
| F-04 | Medium | `F003-RES-IRON-ORE` | About 12 x 16 px, the smallest sprite; speckled interior reads dither-like at 1x; no face ramp | Accept for T440 import. Check in context in T441 at 32 x 24 pt. If it reads poorly there, redraw (larger footprint inside 32 x 24, flat tone ramp instead of speckle) as a follow-up task | producer task if T441 fails; owner decision on spend (OD-13) |
| F-04b | Low | `F003-RES-GEAR` | Face-on round cog, not a 2:1 ellipse | None. Note only; resources appear in UI lists | none |
| F-05 | Low | `F003-ITEM-DIAMOND-DRILL-HEAD`, `F003-ITEM-OVERCLOCK-CORE` | Drill head reads as a funnel or top; core is a generic spark in a diamond. Blind 12/12 on only three tiles, so weak evidence. The tier frame (circle, triangle) plus the always-visible item name carry meaning | Accept; confirm in-context naming in T441. No redraw now | T441, owner (OD-13) |
| F-06 | Medium | UI icons | Blind naming 36/50 (72 %); three confusable clusters in section 2; brief rows mandate cash as "circle with a cross bar", close as "chevron down over a bar" | Name every icon with its label in T441 in-context tests, including the arrow-over-bar and ring clusters at 24 pt at 420 dpi. Owner to decide whether to revise brief rows for `cash`, `close`, `ip`, `fv` (OD-14) | T441, owner |
| F-07 | Low | docs | rig-v1 and its hash still named in `docs/art/F004-art-bible-v1.md` (lines about 40, 477, 495), `docs/art/F004-asset-briefs-v1.md` (about 26), `docs/integrations/mcp-readiness.md` (about 122, 127). Current rig is rig-v2 (9075ca24...) | Update to rig-v2 as a patch revision (OD-2) | T440 / parent, owner sign-off of the Art Bible patch |
| F-08 | Low | cohesion tooling | No production anchor sheet is committed, although Art Bible section 13 defines one and the contract requires the cohesion review against it. The sheet also places resources on `floor_0`, while the brief places resources only in UI lists | Generate and commit the production anchor sheet (`art_pipeline.checks.make_review_sheets`) from the approved set at T440; state in section 13 that resources are placed on UI surfaces (OD-12) | T440, owner |
| F-09 | Low | `F004-MACHINE-ASSEMBLER-RUN` | Rim share 0.917 on the real plate, threshold 0.90 (T434 reported 0.90 with zero margin before T466). The physical-lattice sample, which is not gated, gave 0.833 against `floor_1` | Keep the gate. Any re-render re-runs it. T456 gates the lattice sample with the real adjacency; if it fails there, the producer adjusts the Assembler rim | T456, producer |
| F-10 | Low | `F004-MACHINE-PRESS-RUN` | Smallest body mass; reads as an anvil more than an H-beam; still named 8/8 and IoU worst 0.667 | Accept. No action | none |
| F-11 | Info | all | `reviewedBy` null or empty on all 88 provenance files | Fill from the per-asset table above once the owner accepts this review; never for a REJECT | parent, T440 |
| F-12 | Info | markers | DEV-3 compensation (marker naming blocking in T439) met at 1x: 8/8 over four sheets. The T445 1280 x 720 at n = 2 run remains | none here | T445 |
| F-13 | Low | `F004-LABEL-LOCK-*`, items | Label backdrop `dim_1` is outside `A_ov` and not written in the Art Bible; items use rim tones `ui_text` and `res_crystal` beyond the C6 list | Write both as rules (OD-9, OD-11) | owner, T440 |
| F-14 | Low | `f004/slots`, `f004/floor` | No pytest covers the Blender-route plates and floor (QA of #549). Re-render is byte-identical per QA | Add a golden-hash test | T440 |
| F-15 | Info | tooling | The base-model SHA-256 gap in the QA of #550 was addressed by T465 (#633, commit `c5cb52d`, "register Z-Image-Turbo base models with SHA-256") | none | none |
| F-16 | Low | machine checks | `outline_contrast` ignores tones `dim_1`, `floor_2`, `steel_2`, `ui_line` where they appear under a body outline (the C5 set is {plate_1, plate_2, ink_0, floor_0, floor_1}). Consistent with C5; recorded so a reader does not assume every tone was gated | Mention in the T440 check aggregation | T440 |
| F-17 | Info | floor | Period 60 x 30 is T435's proposal | T456 confirms | T456 |

## 6. Owner decisions needed

These are the questions collected in the comments of #553 and the QA notes, with this review's recommendation. "Owner decision" means the Art Bible or a frozen brief changes in wording or scope; the recommendation is to accept in every case unless noted.

| # | Question | Recommendation | Owner decision needed |
|---|---|---|---|
| OD-1 | Flat-emission palette materials per face orientation (rig key light unused): accept as Lighting B "flat"? | Accept. The body is flat with a fixed three-tone ramp in the top-left order, which is the definition of Lighting B in Art Bible 2.1; the result reads correctly at 1x. Add one sentence to 2 and 2.1: the ramp is encoded by per-orientation emission and the rig key light is unused. Patch level | Yes (ratify the wording, patch) |
| OD-2 | rig-v1 ground edges 2.236:1 versus 2:1 | Already resolved by T466 (rig-v2, rot_x 60 degrees, ratio 2.000000). Remaining: update the rig-v1 references and hash in the Art Bible (about lines 40, 477, 495), the brief (about 26) and mcp-readiness (about 122, 127) to rig-v2 `9075ca24...` | Sign-off of the doc patch only |
| OD-3 | Flat plates without a wall versus an in-box depth cue | Accept flat for v1.0. A wall does not fit inside the exact 70 x 35 hit and selection shape; a depth cue would need a new brief and larger plate canvas, with atlas and keep-out consequences. The blind sheets and the 1x composite show the plates read clearly | Yes (accept) |
| OD-4 | Assembler rim share 0.90 / 0.917 (zero or thin margin); 0.833 on the physical-lattice sample | Accept; keep the gate. Make T456 gate the lattice sample with real adjacency, and let the producer adjust the Assembler rim if it fails there | No; awareness. Decision only if C5's backdrop set changes |
| OD-5 | Floor tile has no ink outline (seam rule, brief section 6): add an exemption to Art Bible 6.1? | Yes. An outline would break the seamless tiling. Add "the floor tile is exempt from the outline rule" to 6.1 (patch) | Yes (doc patch) |
| OD-6 | Locked plate "dark" reads through padlock and hatch (luma 104 versus 107 and 119) | Accept the art; change 8.5 from "dark plate with hatch and padlock" to "plate with a dark hatch field and padlock". The keyhole fix in F-01 is separate | Yes (doc patch), and accept or fix F-01 |
| OD-7 | "A symmetric array of one repeated element counts as one primitive" (gear icons: the brief mandates 8 teeth) | Ratify. Eight teeth are one primitive; counting each tooth makes the brief unsatisfiable. Add the sentence to Art Bible 11 | Yes (ratify) |
| OD-8 | UI glyphs carry no ink outline (Art Bible 6.1 versus 11 and C4) | Confirm. A monochrome tinted glyph cannot carry an ink outline because the tint multiplies the colour; contrast is evaluated per surface (C4). Add an explicit exemption for UI glyphs to 6.1 | Yes (confirm, patch) |
| OD-9 | Label backdrop set `A_ov` plus `dim_1` not written | Write a new rule C9: the lock-digit backdrop set is `A_ov` plus `dim_1`; `ui_text` on `dim_1` is 7.49 and on `dim_0` 11.55, both above 4.5. | Yes (doc patch) |
| OD-10 | Marker naming in grayscale and CVD (DEV-3, blocking) | Met at 1x here: 8/8 across four sheets, zero confusions. The blocking item for T445 (1280 x 720 at n = 2) stays open | No |
| OD-11 | `rim_lift` versus Art Bible 6.1 and C6 | `rim_lift` implements C6 (the rim of a resource or item on UI surfaces is lifted to `res_ore_1`, `res_gold`, `steel_2` or `steel_3`), a scripted touch-up recorded in provenance. 6.1 says no tone is forced. Add "except C6 pictograms on UI surfaces" to 6.1 and add `ui_text` and `res_crystal` to the C6 item allowance | Yes (doc patch) |
| OD-12 | Gate the resource floor and plate checks? (the anchor sheet places resources on `floor_0`) | No. The brief places resources and items in UI lists only, so the floor and plate outline checks stay reported but not gated. Correct section 13 to place resources on UI surfaces instead of floor | Yes (doc patch) |
| OD-13 | Weak drill head, ore and core | Accept for import. Decide after the T441 in-context check whether to spend GPU time on a redraw of the ore (and the core or drill head if they fail naming in context) | Later, after T441 |
| OD-14 | Brief-level icon weaknesses (cash as a cross, close versus priority-down, ip, fv) | Revise the `cash` row (coin with a vertical bar and an inner mark that is not a cross). Leave the others to the T441 in-context result | Yes for `cash`; later for the rest |
| OD-15 | Floor period 60 x 30 | T456 confirms; no owner decision | No |
| OD-16 | Family-scoped check reports (parent decision): `art-source/f004/<family>/check-report.json` plus `slots/check-report-t437.json` | Fine. All 10 reports pass (items 2, slots 2); aggregate them for T440. The reports hold 1,729 individual checks, 0 failing | No |

## 7. What this review did not do

- It does not fill `provenance.json` `reviewedBy` (assignment rule); see the note at the top.
- It did not run Unity, Blender, ComfyUI or adb (no lease). In-game validation (step 5) stays with T441 and T445.
- It did not measure the numeric backdrop acceptance C8 on the lattice (T456 and T429 own it) or the SC-001 re-run on the shipped build (T445).
