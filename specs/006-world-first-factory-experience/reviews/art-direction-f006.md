# F006 art family reviews (T525, #698; FR-044, FR-047, SC-013; R17, R23)

**Task**: T525, GitHub Issue #698. **Date**: 2026-11-13. **Base commit**: `48b4312` (origin/main).
**Reviewed**: the six merged art tasks T519 to T524, PRs #737 to #742. All are source assets only. Nothing is imported into Unity yet, so every judgment is about sprites in isolation, never the composed screen.

| Task | PR | Families |
|---|---|---|
| T519 | #738 | GROUND, FOUNDATION |
| T520 | #741 | DEPOT, DEPOSIT (+ vein chip FX) |
| T521 | #742 | PROP, EDGE |
| T522 | #739 | LINK, FX |
| T523 | #737 | F006-UI (hub icon, attention dot, banner; all "not needed") |
| T524 | #740 | K4 cue tail (A-03) |

**Independence**: each lens ran as a separate, independent, read-only session. No reviewer produced any of these assets, and none edited the repository. The Art Director ran in a fresh session and is not the producer of Art Bible section 21 or of the T517/T524 tail work. The Game Systems/Economy lens ran at Low tier. The parent recorded the results here as the reviewers wrote them. The World Designer's full conformance review is in [world-bible-conformance.md](world-bible-conformance.md) (Part 1).

**Rules used**: `docs/art/F004-art-bible-v1.md` section 21 (v1.1.0), [direction.md](direction.md) OD-9b conditions, [p1-followups.md](p1-followups.md), [p2-followups.md](p2-followups.md).

## Overall verdicts

| Lens | Overall | Blocker | Major | Minor |
|---|---|---|---|---|
| Art Director | APPROVED_WITH_CONDITIONS | 0 | 2 | 10 (as stated by the reviewer) |
| UI/UX (cue legibility) | ACCEPT_WITH_NOTES | 0 | 0 | 13 |
| Game Designer/PO (readability) | ACCEPT_WITH_NOTES | 0 | 0 | 6 (7 carries) |
| World Designer (conformance) | CONFORMS_WITH_CONDITIONS | 0 | 0 | 5 |
| Game Systems/Economy (vein) | CONFIRMED_NO_PACING_EFFECT | n/a | n/a | n/a |

Every condition that needs a composed frame is "not verifiable at T525" and is carried, never passed (see the carry section below).

## Verdict matrix

Legend: AD uses APPROVED / APPROVED_WITH_CONDITIONS / CHANGES_REQUIRED. UI/UX and PO use ACCEPT / ACCEPT_WITH_NOTES. The World Designer uses CONFORMS / CONFORMS_WITH_CONDITIONS; see [world-bible-conformance.md](world-bible-conformance.md).

| Family | Art Director | UI/UX (cue legibility) | Game Designer/PO (readability) | World Designer (conformance) |
|---|---|---|---|---|
| GROUND | APPROVED | ACCEPT | ACCEPT_WITH_NOTES | CONFORMS |
| FOUNDATION | APPROVED | ACCEPT_WITH_NOTES | ACCEPT_WITH_NOTES | CONFORMS |
| DEPOT | APPROVED_WITH_CONDITIONS | ACCEPT_WITH_NOTES | ACCEPT_WITH_NOTES | CONFORMS_WITH_CONDITIONS |
| DEPOSIT + vein chip | APPROVED_WITH_CONDITIONS | ACCEPT | ACCEPT_WITH_NOTES | CONFORMS_WITH_CONDITIONS |
| PROP | APPROVED | ACCEPT_WITH_NOTES | ACCEPT | CONFORMS |
| EDGE | APPROVED_WITH_CONDITIONS | ACCEPT_WITH_NOTES (wall provisional) | ACCEPT_WITH_NOTES | CONFORMS_WITH_CONDITIONS |
| LINK | APPROVED_WITH_CONDITIONS | ACCEPT_WITH_NOTES | ACCEPT_WITH_NOTES | CONFORMS_WITH_CONDITIONS |
| FX | APPROVED_WITH_CONDITIONS | ACCEPT_WITH_NOTES | ACCEPT_WITH_NOTES | CONFORMS |
| F006-UI | mixed: dot and banner APPROVED, hub icon CHANGES_REQUIRED (U1, owner decision) | ACCEPT_WITH_NOTES | ACCEPT_WITH_NOTES | CONFORMS_WITH_CONDITIONS |
| K4 cue tail | APPROVED | ACCEPT_WITH_NOTES | ACCEPT_WITH_NOTES | CONFORMS |

## Art Director

Evidence used: contact sheets and frames (viewed as PNG), every `check-report.json` and `manifest.json`, `brief-tail.md`, Art Bible section 21 including 21.8, OD-9b, the two follow-up lists, research R12, and the reviewer's own pixel measurements (bounding boxes, pixel diffs against the imported F004 cue sprites, WCAG contrast of cue fills against lane tones, F004 flash history).

All source art is palette-exact, inside its section 21.2 allowance row, and matches the section 21.4 canvases and frame counts (40 world frames confirmed; FX 42 + 1 highlight). It shows the OD-9b read: open railing deck, front-margin stage structures, no `lit_*`, no `teal_*` on props. The K4 tail is approved for import.

### OD-9b Art Director conditions 1 to 9 (status at T525)

| # | Condition | Status |
|---|---|---|
| 1 | Palette ids only, `ink_0` outline, palette gate | MET at source. Every family `palette` and `family_allowance_21_2` check passes. Per-sprite max colours 12 (depot full = 12, at the cap). |
| 2 | C8 ring, 100 % in `A_ov`/`A_fr`, no `steel_*` | PARTIAL. Link layer proven: `c8_ring_link_layer` ringPxNotInSetR 0 and ringSteelPx 0 for 5 layouts x K0/K4. Depot, deposit, edge and prop carry `steel_*` by allowance and rely on margin placement. Composite proof carried to T527. |
| 3 | Walls and gantries on the outer margin, keep-out proof | PARTIAL. `keepout_proof_front_margin_clear` passes for 3x4, 4x3, 6, 20 and L on the front margin only (back, left, right fail). Composite test carried to T527. |
| 4 | Visible growth railing < wall < gantry, no wall in stage 1 | MET at source. Structure height above deck centre is 10 / 16 / 30 px; railing post height is constant (A4). The "no wall in stage 1" test is a placement test, carried to T527. |
| 5 | No `plate_*` on depot, no `teal_*` on props | MET (`slab_steel_not_plate`, `no_lit_no_teal_in_family`). |
| 6 | Ground keyed `starter-platform`, 70x54 period, whole pixels | MET. Ground manifest regionKey `starter-platform`, 70x54 canvases, integral pivots. |
| 7 | Depot visible at 640x360 and 200 % text, hopper showing, lamp not cropped | PARTIAL. Hopper opening 22x12 `dim_0` (at least 12x6) and lamp top row 2 (exactly 2 px headroom) measured. Camera visibility carried to T527/T534. |
| 8 | Flash <= 3/s, reduced-motion still | MET at source for every FX composite (worst 2/s). Sum with other loops in view carried to T527. |
| 9 | Cost per stage | MET. Frame counts equal the 21.6 table (shared 14, stage 1 10, stage 2 8, stage 3 8). |

### Findings

The reviewer's stated total is 2 major and 10 minor. The review text names the 13 minor ids below; they are recorded as written.

| Id | Severity | Family | Finding | Carried by |
|---|---|---|---|---|
| E1 | major | EDGE | The stage-2 wall is the brightest large flat area in the lower frame (`steel_1`/`steel_2` bands across the whole front margin). It passes keep-out but may out-weigh the machines in visual hierarchy. Accept the wall only if the T527 composite shows (a) the C8 ring proof with zero wall pixels in any ring and (b) the wall's luminance mass does not dominate the front-row machine plates. If it does, drop its top band to `steel_1`. Provisional until then (P1-F5). | T527 |
| U1 | major | F006-UI | Hub entry icon reusing `F003-UI-ICON-TAB-CONTRACTS`: CHANGES_REQUIRED. See P2-F9 and the owner decisions below. | Owner decision |
| D1 | minor | DEPOT | Lamp headroom is exactly 2 art px (topRow 2), zero margin against the 21.7 rule. Any 1 px crop at 640x360 or 75 % portrait fails it. | T534 |
| D2 | minor | DEPOT | `copper_2` is also the "running" accent family (pips, active dashes). A static `copper_2` lamp head and full lip could read as "active". Acceptable under signed-off 21.2; check at the composite. | T527 |
| D3 | minor | DEPOT | "Full" versus "high" is mostly the lip colour step (`copper_1` to `copper_2`, about 1.5:1) plus 19 fill px. May be weak at the smallest scale. If weak at 640x360, add one fill pixel row for full. | T527/T534 |
| D4 | minor | DEPOT | The stage-2 variant is a small panel stub; progression reads mainly through the edge. Acceptable. | none |
| V1 | minor | DEPOSIT | `rest-s1` and `rest-s2` differ by only 16 pixels (s2 vs s3: 105). Stage dressing 1 to 2 is not perceptible at 1x. Stage identity is carried by edge and depot. Not a blocker. | later polish |
| V2 | minor | DEPOSIT | `rest-s3` touches canvas row 0 (no headroom). Harmless for scenery, but do not clip it at the world atlas padding. | T526 |
| V3 | minor | DEPOSIT | Chip frames alone reach 2 flashes/s at 8 fps (the F006 FX limit; composite over the rest vein measures 0). The loop can run with machine loops, so the sum must be proven on the full frame (P2-F6). | T527/T534 |
| E2 | minor | EDGE | The wall's ink seam every 35 px reads as panel joints; heavy but consistent. Accepted (P2-F7d). | none |
| E3 | minor | EDGE | The deck top inside the rail equals the field (`floor_0`), so the platform reads as raised only through the fascia and rail. Accepted (P2-F7c); it keeps the ring set clean. | none |
| L1 | minor | LINK | `branch-active` differs from `branch` only by a luminance step (`copper_1` to `copper_2`, 1.5:1); no shape cue. Works. | none |
| L2 | minor | LINK | Lane body `dim_0` versus `floor_0` is 1.06:1, so the lane is defined by its `ink` edges and dashes. Acceptable. | none |
| F1 | minor | FX | The section 21.4 FX arithmetic must be corrected when the highlight is added: the padded delta is 2,880 px, not 2,448 px (P2-F2). | Parent, Art Bible revision |

Per-family evidence:

- **GROUND** (`f006-ground-base/deck/hatch`): APPROVED. A quiet dark field with a slightly lighter deck diamond and a dotted-frame locked hatch; the three states separate by shape, not only tone. `floor_0` versus `floor_1` is about 1.14:1, which keeps the ring backdrop clean. The T519 wording about ground "edge/corner/T tiles" is superseded (P2-F1).
- **FOUNDATION** (`outline`, `flat`): APPROVED. `plate_1` top with `plate_2` rim on the flat occupied foundation is the unchanged F004 plate role. The outline foundation reads as an empty unlocked slot.
- **DEPOT** (12 sprites): APPROVED_WITH_CONDITIONS. Steel slab (`steel_1` top, `steel_2` rim, `steel_0` side, no `plate_*`), copper hopper collar with dark pit. Fill empty/low/high/full is monotonic (0 / 51 / 149 / 168 fill px) and full adds a `copper_2` lip (28 to 123 px). Static lamp `copper_2` head, `steel_3` cap, no `lit_*`. Conditions: C8 composite proof (P2-F6), 640x360 and 200 % visibility.
- **DEPOSIT + chip FX**: APPROVED_WITH_CONDITIONS. Ore cluster on a dark patch, `res_ore_*`/`res_coal`/`floor_1`/`steel_3` rim only; chips stay above the patch and use 3 to 4 colours per frame. Condition: vein legibility per class recorded in the camera task.
- **PROP** (`crate-small`, `crate-stack`, `lamp-post`, `pallet`): APPROVED. All 24 px tall or less, steel/copper only, no `teal_*`, no `lit_*`, static lamp.
- **EDGE** (railing x5, wall x3, gantry x3): APPROVED_WITH_CONDITIONS (E1). Growth is clear in the triptych. Not verifiable at T525: iso-diagonal V layout against the real rectangular 70x54 lattice on the 12-slot frames (T527).
- **LINK** (`trunk`, `junction`, `branch`, `branch-active`): APPROVED_WITH_CONDITIONS. R-only tones (`dim_0` body, `ink_1` shade, `copper_1` dashes, `copper_2` active). 1380/690 lane px under badges, ring px not in set R = 0, steel ring px = 0 for all layouts and badges K0 and K4. The trunk recolour (P1-F9) is done. Condition: re-run the ring proof in the composite with the production K4 sprites (the tail lengthens the badge extent to 23x27).
- **FX** (7 sprites incl. `completion-highlight`): APPROVED_WITH_CONDITIONS. No `lit_*`/`teal_*`/`cue_*`; pip `copper_2` + `ink_0`; pop, steam and glint `steel_*`. Keep-out proofs pass for pop, spark, steam, glint. Flash matrix: every machine composite 0 except Assembler + completion pop 2 (10 fps), 2 (8 fps), 1 (6 fps), all inside the 2/s target; stacks 0. The `overlapOnly` figures (steam 10, spark 4) are a non-gating sparse-frame artefact of `check_flash_own`; the gate figure is the bare-floor/body composite. Condition: re-run the flash matrix on real composited frames in T527.
- **F006-UI**: attention dot (USS circle plus text/shape cue, static) APPROVED. Milestone banner (plain USS box) APPROVED; plain is correct. Hub entry icon: CHANGES_REQUIRED (U1).
- **K4 cue tail** (5 sprites): APPROVED. See the pixel review below.

### K4 pixel review (the P2-F5 gate)

The reviewer rendered the five `_00.png` at 10x, viewed the grayscale and deuteranopia sheets, dumped one sprite as ASCII, and diffed against the imported F004 cue sprites.

- Exactly 23 pixels differ from each imported F004 badge, all on rows 32 to 36. The box (rows 10 to 31), glyph and outline are unchanged in every sprite.
- Tail rows 33 to 36 have widths 7, 5, 3, 1 centred on column 59, symmetric. `ink_0` on both edges of each row and a single `ink_0` tip pixel. The inner fill is the badge's own reserved cue colour (5, 3, 1 px of fill). Row 32 (the old bottom ink edge) turns to the fill over 7 px, so the tail joins the body without a seam. No stray or orphan pixels, no anti-aliasing (alpha 0 or 255).
- Palette is exactly `{ink_0, cue fill}` per sprite (2 colours). No `steel_*`, no new id. The five glyphs stay distinguishable in grayscale and deuteranopia (hourglass, down-arrow, bars, pause, minus-ring).
- Contrast of cue fills against `dim_0`: waiting 6.45, alert 3.52, storage-full 6.68, paused 9.28, inactive 4.10 (all at least 3.5:1). Every ring is `ink_0` against the fill (5.17 to 13.62). `tail_geometry` passes (rowWidths 7/5/3/1, jointPixels 7, bbox 23x27).
- Informational: the F004 `overlay_keepout` probe reports `passed: false` inside each sprite's own check because the badge is itself the overlay occupying the keep-out. The gate's rule (bbox equals declared box) passes. A wording fix in the check output would avoid misreading.
- **Verdict**: P2-F5 (T526 import) is NOT blocked by Art Direction. Recommendation for the same change: switch `FactoryWorldLayout.KeepOutOf` to `BadgeExtentBox()` (23x27); the tail is opaque art, so decorations must stay off it.

## UI/UX (cue legibility on the F006 backdrop)

Method: WCAG contrast from `palette-v1.json`, pixel colour census of every family's frames, `check-report.json` of link, depot and cue, and contact sheets. The reading: the badge is legible on every ring-safe backdrop because fill and outline complement each other. On the dark F006 tones (`floor_0`, `floor_1`, `dim_0`) the bright fill carries the contrast (3.1 to 9.9). On plate tones the `ink_0` outline carries it (4.1 to 6.5), and the fill alone would fail (`cue_alert` 1.25). The danger zone is `steel_2`/`steel_3`/`copper_2`, where both halves can fail (`cue_dim` fill equals `steel_2`, 1.00). Those tones are kept out of every ring by the C8 rule, so legibility rests entirely on the C8 composite proof (T527).

| Id | Severity | Family | Finding | Carried by |
|---|---|---|---|---|
| F-1 | minor | FOUNDATION | On `plate_1`/`plate_2` the fill is not the carrier (`cue_alert` 1.25, `cue_dim` 1.08 on `plate_2`); legibility depends on the 1 px `ink_0` outline, which exists in every badge and K4 tail edge. No change. | none |
| F-2 | minor | LINK | The passing `cue_over_lane_contrast` gate is palette-membership based and does not measure luminance. Real figures: badge fill beats the lane by 3.5 (alert) to 9.3 (neutral); outline against the lane is only 1.2 to 1.5, so over a lane the badge reads by fill alone. `cue_dim` is weakest: 4.1 on `dim_0`, 1.09 on a `copper_2` active dash. Acceptable. Recommend the doc comment records this and the gate also reports luminance ratios. | T526 |
| F-3 | minor | LINK | Shortage markers sit at the trunk junction of the first affected row and draw over the lane and its copper dashes. Marker fills `cvd_orange`/`cvd_sky`/`cvd_pink` are 4.1:1 or more on `dim_0` but only 1.1 to 1.3:1 against `copper_2` active dashes; the `ink_0` outline (6.6 on `copper_2`) keeps the shape. Whether the outline survives at 11 px is not verifiable here. | T527 |
| F-4 | minor | DEPOT | The store-full state (`res_ore_1`/`copper_2` fill and lip, amber) is hue-adjacent to the `storage-full` badge `cue_warn`. The depot's store-full world marker must carry its own icon and not rely on amber. | T527 and the marker spec |
| F-5 | minor | DEPOT | Empty-to-low fill is a few ore pixels inside a 12x6 opening (`res_ore_0` vs `dim_0` 2.0:1, `res_ore_1` 4.0:1, `res_coal` only 1.14:1). Coal is nearly invisible on the dark interior. Verify low/high are distinguishable at 640x360. | T527 |
| F-6 | minor | PROP | The static `copper_2` lamp head is the one warm "pip" in the scenery, hue-adjacent to `cue_warn`. Shape separates it from a badge. Placement rule: a lamp must not sit inside a 23x27 badge extent or on a trunk junction where a marker draws. | T527 |
| F-7 | minor | EDGE | The wall band is `steel_2`-bright and the gantry carries the brightest pixels (`steel_3`) of any backdrop family. `cue_dim` equals `steel_2` (1.00); `cue_neutral` against `steel_3` is 1.25. If a wall or gantry pixel lands in a badge extent or behind it, the badge is lost. Front-row-only placement keeps them behind the machine bodies, but only the composite proves it. Wall provisional (P1-F5). | T527 |
| F-8 | minor | EDGE | fl/fr/corner pieces read as one continuous front edge; the 35 px seam is visible as vertical joints; the deck top equals the field. None harms cue legibility. Defers to the Art Director. | none |
| F-9 | minor | FX | The pop (`steel_3`/`steel_2`) on `plate_2` rims (`#8d9aae`) has 1.68 and 1.08 contrast, so it depends on its `ink_0` edge; on a `floor_0` field it is 7.9. Acceptable: the pop is decorative and must never be the only completion signal. A 1 s highlight tint over a `steel_1` slab should be `ui_accent` or `copper_2`, not `steel_3` (`steel_2` vs `steel_3` is 1.8). | T527/T535 |
| F-10 | minor | K4 tail | The badge (23 px at `n_min`, 26.23 pt at 1080x2400, 22.95 pt at 640x360 and 720x1280) is below the 44 to 48 dp touch size, but it is not a tap target (a tap selects the machine through its hit shape), so C4 is not breached. State this exception in the C4 test; do not add the badge to the 48 dp measurement. | T535/T540 |
| F-11 | minor | K4 tail | The swap into `Assets/IdleFactory/Art/F004/cues/` and `KeepOutOf` from `BadgeBox` 23x23 to `BadgeExtentBox` 23x27 must be in the same change. Otherwise props and walls may legally sit on the 4 tail pixels. Hard dependency for T526 (UI/UX: required). | T526 |
| F-12 | minor | F006-UI | The attention dot must not rely on colour alone (`--warning` vs `--accent`). Make the "text or shape cue" testable (accessible name, strip text or a count). | T535 |
| F-13 | minor | F006-UI | Banner plainness is acceptable (C6 header budget 15 % at 100 %, 20 % at 200 %). The 2.5 s auto-dismiss must keep a 48 dp dismiss target (C4) and a static form under reduced motion. | T535 |

Per family: GROUND ACCEPT; FOUNDATION ACCEPT_WITH_NOTES; LINK ACCEPT_WITH_NOTES; DEPOT ACCEPT_WITH_NOTES; DEPOSIT ACCEPT (note: `res_gold` `#e0b13a` is nearly identical to `cue_warn`, so record the hue caution for any later gold deposit); PROP ACCEPT_WITH_NOTES; EDGE ACCEPT_WITH_NOTES (wall provisional); FX ACCEPT_WITH_NOTES; K4 ACCEPT_WITH_NOTES; UI ACCEPT_WITH_NOTES.

Other UI/UX answers:

- **C1 camera**: art does not decide it. The depot sits one cell outside Bounds; the deposit/vein must be legible or recorded as out of view. Carried to T534.
- **C4 48 dp**: no F006 art is a touch target (badge, markers, props, FX are non-interactive; hit shapes unchanged). The hub button and the dot are USS targets. Carried to T535/T540.
- **C5 reduced motion**: art supplies static forms (static link active tile, static lamps, no `lit_*` flicker). The 1 s highlight and the banner static form are code/USS and unproven. Carried to T527/T535.

## Game Designer/Product Owner (readability)

Basis: briefs and contact sheets; judgments are about sprites in isolation. One item (M2) was judged from the brief because no sheet showed it.

| Id | Severity | Family | Finding | Carried by |
|---|---|---|---|---|
| M1 | minor | GROUND | Hatch and the empty-unlocked outline are both dark and close in value. The difference is the dotted versus solid edge and the slat marks; the padlock/digit overlay carries "locked". Confirm at 640x360 and 200 % that a locked cell is not read as an empty buildable cell without the padlock. | T527 |
| M2 | minor | FOUNDATION | `f006-foundation-flat` (occupied) was not on any sheet; judged from the brief only (flat `plate_1/plate_2` diamond, `ink_0` outline, no wall or plinth; FR-012 compliant on paper). Show occupied, empty and locked side by side on the real backdrop (US2 AS2). | T527 |
| M3 | minor | DEPOT | Empty versus low differs by only a handful of pixels at native size and may collapse to "empty" at 640x360. If it fails, the fix is a larger low-state pile, not new logic. | T527/T534 |
| M4 | minor | DEPOSIT | False affordance: a bright, rimmed, isolated object looks tappable, and the highlight rim looks like a selection state. FR-010a says it is not an input. Give the vein no hit shape and no tap feedback; the HUD mining control should be the visible trigger of the chip loop. Play question: do players try to tap the vein? | T527/T534, play |
| M5 | minor | DEPOSIT | The reduced-motion highlight (thin rim, one still) is a weak "mining right now" signal. Acceptable because the control itself shows state. | play |
| M6 | minor | EDGE | The wall is front-only, so "walled yard" reads as a retaining lip, not an enclosed yard (see P2-F7(b)). | owner decision |

Per family: GROUND, FOUNDATION, DEPOT, DEPOSIT, EDGE, LINK, FX, F006-UI and K4 ACCEPT_WITH_NOTES; PROP ACCEPT.

Further notes from the PO lens:

- Stage progression reads clearly in the edge triptych (10, 16, 30 px; thin posts and rail, continuous solid wall, tall steel gantry frames with copper hazard dashes) and matches World Bible R15.
- LINK: the trunk-and-drops form reads as "every machine hangs off one line that leads to the depot" (FR-013). Copper dashes plus a travelling pip could imply an item conveyor; harmless because goods do reach the shared store, but confirm in play that no player expects to re-route lines. A static trunk gives no flow direction; the branch pip and the active tile carry it.
- K4: each badge keeps a distinct glyph in greyscale (list, hourglass, pause, minus-circle, down arrow). The tail answers "which machine is this about" and adds no new state.
- F006-UI: all three elements are "not needed" (reuse of the contracts glyph, USS circle, USS box).

Acceptance coverage (sprite-level only):

| Criterion | Status |
|---|---|
| US2 AS1 and FR-011 continuous facility | Carried to T527 composite, NOT passed |
| US2 AS2 and FR-012 occupied/empty/locked by shape and value | Carried to T527 (M1, M2) |
| US2 AS3 / SC-005 cue legibility on the backdrop | Carried to T527 plus blind-naming trial, NOT passed |
| FR-010a vein shows activity, not input | Pass at sprite level; M4/M5 carried to play |
| FR-013 every machine connected to the depot, no machine-to-machine | Pass at sprite level; layout in T527 |
| FR-014 decoration never hides cues | Carried: composite ring proof, P1-F5 |
| FR-015 decoration static under reduced motion | Pass at source level; verify in T527 |
| FR-044 pipeline | Pass for this lens (briefs, provenance and checks exist; producers did not self-approve) |
| FR-047 / SC-013 world conformance | Not the PO lens; see [world-bible-conformance.md](world-bible-conformance.md) |
| SC-006 stage ordering | Orders clearly in isolation; carried to the T527 shuffled-capture trial |

## World Designer (conformance)

Overall CONFORMS_WITH_CONDITIONS; 0 blocker, 0 major, 5 minor. The full review is in [world-bible-conformance.md](world-bible-conformance.md) (Part 1). Its minor findings:

| Id | Severity | Family | Finding | Carried by |
|---|---|---|---|---|
| M1 | minor | DEPOT | The s2 parapet appears on one back edge in the sheet; the brief says two. Confirm intent. | T527 |
| M2 | minor | LINK | No trunk terminus art; termination must be composed. | T527 |
| M3 | minor | DEPOSIT | The vein is out of view on most screen classes (visible on one class only, 10.8 %). | T534 |
| M4 | minor | EDGE/DEPOT | Stage-2 and stage-3 art exist at source level but are not composed; OD-9b is met at source level only. | T527 |
| M5 | minor | F006-UI | P2-F20 settings copy "Show zoom buttons" still needs a World Designer read and is not in `world-copy.md`. | T547 |

## Answers to open follow-ups

Reviewer abbreviations: AD Art Director, UX UI/UX, PO Game Designer/PO, WD World Designer.

| Item | Answer |
|---|---|
| **P2-F1** T519 "edge/corner/T tiles" vs 21.4 | AD: signed-off 21.4 governs. All edge, corner and quiet art belongs to F006-EDGE (T521); no ground edge sprites are owed (ground has 3 frames). Add a note to the T519 task text (parent edit); no art change. Not an owner decision. Other lenses gave no separate answer. |
| **P2-F2** add `f006-fx-completion-highlight` | AD: YES, add it to 21.4 and name it in 21.7. The sprite exists (68x36, 1 frame, `steel_3` + `ink_0`, not machine-anchored, no flash). The follow-up's "+2,448 px" is the opaque canvas (68 x 36); the padded figure is (68+4)(36+4) = **2,880 px**. FX atlas becomes 43 frames, opaque 45,816, padded 57,144; on the planned 256 x 256 page (65,536 px) that is **87.2 % fill, 12.8 % free** (was 82.8 % / 17.2 %). It still fits one page; the 512 x 256 fallback and budget 7 are unchanged; planned totals in bytes are unchanged (1.25 MiB planned, 2.50 MiB worst). The pop `stillFrame` 3 stays an evidence-sheet still only. The depot footprint is 80x40, so the 68x36 highlight sits inside the slab; accepted for a 1 s static highlight. Art Bible revision by the parent, not an owner decision. UX: the highlight tint over a `steel_1` slab should be `ui_accent` or `copper_2`, not `steel_3` (F-9). |
| **P2-F3** Assembler "already at 3" vs measured 0 to 2 | AD: reword, keep the cap. The "3" comes from the F004 spike Assembler runs (`spike/s1`, `spike/s2` D16/D24 at 8 fps); the F004 production Assembler measures 0 (`machines/check-report.json`); the T522 matrix gives Assembler + completion pop 2/2/1 at 10/8/6 fps. Proposed wording for 21.7, 21.8 item 2 and R12: "The F004 spike Assembler measured 3 flashes/s; the production Assembler measures 0 alone and 2 with the completion pop. The Assembler stays capped at 8 fps as conservative headroom." Pace caps: Miner, Smelter, Press 10 fps; Assembler 8 fps (a safeguard; 10 and 8 both give 2). Not an owner decision. UX confirms only that the pop does not rely on a `lit_*` flash. |
| **P2-F4** R12 lamp wording | AD: replace "use steel or teal tones" with "Lamps are static, with a `copper_2` head and a `steel_3` cap; no `teal_*`, no `lit_*` (Art Bible 21.2 and 21.8 item 5)". Matches what depot and prop draw. Not an owner decision. |
| **P2-F5** K4 swap (pixel review) | AD: APPROVED; not blocked by Art Direction (see the K4 pixel review above). Swap through Unity tooling and switch `KeepOutOf` to `BadgeExtentBox()` in the same change. UX: the same-change rule is a hard dependency for T526 (F-11). PO: a tail clipped by or overlapping a neighbour's plate is the player-facing risk if the keep-out is missed. WD: K4 CONFORMS. |
| **P2-F6** depot/deposit `steel_*`, C8 composite proof, 640x360, 200 %, flash sum | AD: keep the allowance as signed off (depot and deposit sit in a margin cell outside every hit shape and ring; deposit has `steel_3` on a single pixel, depot about 1,480 `steel_*` px). The T527 proof must show, on the densest 12-slot frame for every class and layout (3x4, 4x3, 6, 20, L) and for both K0 and the production K4 badge (23x27 extent): (1) zero opaque depot, deposit, edge, prop or FX pixels in any ring, hit shape, overlay keep-out (rows 0 to 33, columns 15 to 71) or badge band; (2) ring pixels 100 % in `A_ov` or `A_fr` with zero `steel_*`, reported as counts per tone per ring (an empty `decorViolations` list does not count); (3) depot s1/s2/s3 x empty/low/high/full at the trunk end, 80-px canvas overhanging its 70-px cell by 5 px per side, hopper opening and collar visible, lamp tip not cropped, at 640x360 and 200 % text; (4) the vein chip loop (2/s alone) summed with every other loop and FX in view as a full-frame `check_flash` against 3/s, with the vein marked out of view where a class does not frame it. UX: arithmetic suggests the art is large enough (hopper opening at least 24x12 screen px at `n_min` 2, lamp head 2 art px headroom); at 200 % text the card is a bottom sheet (C2) and the depot may sit under it, a camera/layout question. All lenses: not verifiable at T525, carried to T527/T534. |
| **P2-F7(a)** front-left / front-right / front-apex reading | AD: YES; accept, and add to 21.4: "pieces are the front-left and front-right diagonals and the apex; back, left and right margin pieces are not drawn because they fail keep-out". WD: accept; place only on the front margin row along the two diagonals and record the layout rule in 21.4. UX: pieces read as one continuous front edge; defers to AD. All: V placement against the real rectangular 70x54 lattice is verified in T527, not here. |
| **P2-F7(b)** front-only wall as "Walled yard" | OWNER decision, see below. All four lenses recommend accept. |
| **P2-F7(c)** deck top `floor_0` same as the field | AD: accept (E3); it keeps ring backdrops clean. |
| **P2-F7(d)** 35 px seam | AD: accept (E2), no change. UX: the seam reads as vertical joints and does not harm cue legibility. |
| **P2-F8** Blender MCP unreachable in P2 | No lens answered; it is an owner note in the follow-up list (headless only; `pipeline_ground_foundation.py` is a shared helper). Not part of any T525 lens. |
| **P2-F9** Office glyph and banner | OWNER decision on the glyph, see below. Banner and dot: AD, UX and PO approve plain USS (see the F006-UI rows above); no owner decision unless an ornament is requested. |
| **P1-F3** cue-over-lane contrast in the link family | AD: CLOSE for the art review. `cue_over_lane_contrast` passes (outline share 1.0 for every cue and marker over every lane tone, threshold 0.9); every cue fill is at least 3.5:1 against `dim_0`. Watch item: `cue_warn` amber (242,177,52) is hue-close to `copper_*` dashes (1.49:1 against `copper_2`); the `ink_0` outline separates them, so verify at the composite on the 12-slot frame at `n_min`. UX: acceptable, because the fill carries the contrast on dark lane tones (3.5 or more) while the outline does not (1.2 to 1.5); the gate is palette-membership only (F-2). The doc and the lane-vs-keep-out check on `FactoryWorldLayout.KeepOutOf` stay with T526. |
| **P1-F5** stage-2 wall provisional until C8 | AD, UX, WD: STAY PROVISIONAL. At source level the front-margin keep-out passes (zero overlap, at least 13 px from masks) and back/left/right edges fail (1 to 2 px from the machine column), so only front-row placement is valid. Accepted only after the T527 composite C8 ring proof plus the E1 hierarchy check. PO: the SC-006 stage-ordering trial also runs at T527. |
| **Stage-3 "Gantry line"** (WD) | WD: yes, with a condition. Posts, top beam, braces, hazard dashes and repeated front bays; clearly taller than the wall and unlike the railing. The 30 px height must pass the T527 composite readability and keep-out checks (Art Director owns that). |

## Game Systems/Economy (Low tier): vein has no pacing effect

**Verdict: CONFIRMED_NO_PACING_EFFECT.** Reviewer: Game Systems/Economy, Low tier, read-only checklist. Not the producer.

- **Merge diffs T519 to T524** (`git diff --name-only <merge>^1 <merge>` for #737 `86cfcfd`, #738 `5d0aac7`, #739 `8960b43`, #740 `9297685`, #741 `f5ec395`, #742 `62cf0be`). Paths outside `art-source/`, `docs/`, `specs/`, `tools/art-pipeline/`: only `Assets/IdleFactory/Unity/Presentation/StateCueSet.cs` and `Assets/IdleFactory/Tests/EditMode/StateCueSetTests.cs` (#740, T524 badge tail constants, presentation only). No changes under `Assets/IdleFactory/Content/`, `Core/`, Domain, Simulation, Application, or balance and golden traces.
- **Deposit manifest** (`art-source/f006/deposit/manifest.json`): keys are assetId, canvas, footprintPx, fps, frames, palette, perspective, pivot, ppu, route, reducedMotionStill, stillFrame. No yield, rate, cost or size-as-gameplay field. `fps: 8` for `f006-fx-vein-chip` is playback only (6 frames, `floor(t*fps) mod n`). The brief says "scenery, no pacing effect". The "2/s" figure is a flash-budget number, not a production rate (P2-F6 and `wiki/pages/f006-batch-p2-art-and-presentation.md`).
- **Spec, research and contracts**: spec FR-010a says the marker MUST NOT be an input and MUST NOT change mining yield, rate or pacing. Research R23: "No pacing change." `contracts/world-presentation.md` section 2a: "Yield, rate and pacing are unchanged." `DepositCell` is a placement position in the margin band, not economy state. The only economy-adjacent code, `FactoryWorldLayout.cs` and `WorldCompositionSettings.cs`, is placement. Manual mining logic (`RunCommands.Mining.cs`) reads only `RunStart.ManualMining` content, not any deposit field.
- **Not covered by this lens**: the C8 ring proof, the 3/s flash sum and camera visibility (T527, T534).

## Not verifiable at T525 (carried forward)

None of these is passed. Each needs a composed screen, an engine run or a human session.

| Item | Source lens | Carrier |
|---|---|---|
| C8 ring proof for depot, deposit, edge, prop, FX and production K4 tails (zero `steel_*` per ring, counts per tone) | AD, UX, WD | T527 |
| Keep-out proof in composite on real 12-slot layouts, incl. the iso-diagonal V placement and the front-row-only rule | AD, UX | T527 |
| Stage-2 wall: C8 ring proof, hierarchy check E1, front-row-only rule, SC-006 shuffled-order trial | AD, UX, PO | T527 |
| Trunks end at the depot in composition; junction/branch only trunk-to-depot; s2 parapet intent (WD M1, M2) | WD | T527 |
| Occupied/empty/locked side by side, continuity (US2 AS1/AS2), marker legibility over the trunk, K4 tail over the trunk and attribution at the densest frame | PO, UX | T527 |
| Flash sum on full composited frames (machine loop + FX + vein chip + pips); flash matrix re-run on real composites | AD | T527/T534 |
| Depot hopper, collar and lamp at 640x360 and 200 % text; depot visibility under the compact card; vein legibility per screen class (P1-F2) | AD, UX, WD, PO | T534 (with T527) |
| 1 s reduced-motion highlight contrast; banner and dot at 200 % text and 48 dp; accessible dot cue; hub icon distinguishable from the Contracts row | UX, WD | T535, T540 |
| SC-005 re-run with the dimmer `copper_2` lamp (P2 gate, noted in 21.8) and blind-naming trial on the new backdrop | AD, PO | T527 and the cue trials |
| World atlas and FX atlas packing in the engine; `KeepOutOf` to `BadgeExtentBox()` with the K4 swap | AD, UX | T526 |
| P2-F20 settings copy "Show zoom buttons" World Designer read | WD | T547 |
| Do players try to tap the vein; flow direction and "mining right now" signal in play | PO | T534 and a human playtest |
| Art Bible revisions: P2-F2 (2,880 px arithmetic), P2-F3 and P2-F4 wording, front-row-only and iso-diagonal rule in 21.4 after T527 | AD | Parent |

## Owner decisions open

The reviewers recommend; the owner decides. Neither item is resolved here.

### P2-F7(b): front-only wall as the stage-2 "Walled yard"

All four lenses recommend ACCEPT, with these conditions.

| Lens | Position and conditions |
|---|---|
| Art Director | Accept as the stage-2 candidate. Conditions: E1 (T527 composite shows the C8 ring proof with zero wall pixels in any ring, and the wall does not dominate the front-row machine plates; otherwise drop the top band to `steel_1`) and no side or back wall until it can pass keep-out. "Yard" is weakly supported with one side closed. If too thin, keep the name and rely on the stage-2 depot and the per-stage progression table, or rename stage 2 in copy (World Designer) to a front-barrier wording. |
| World Designer | Accept. World Bible section 1 defines stages by what the deck looks like and does not require a closed perimeter. Stage naming stays "Walled yard"; the stage-2 default stays provisional until the C8 composite proof (P1-F5). A low back wall only if the keep-out proof allows it; not recommended now. |
| UI/UX | Supports the front-row-only rule (side rows are 1 to 2 px from machine columns). Wall stays provisional until the T527 C8 proof (P1-F5). |
| Game Designer/PO | Accept, conditional. (1) Run the SC-006 shuffled-order trial at T527. (2) If stages order worse than 9 of 10, add the depot parapet (already in the depot s2 art) and a back-edge treatment only where a keep-out proof allows. (3) Record the front-row-only rule in section 21.4. Cost: it reads as a retaining lip, not an enclosed yard. |

**Owner decision (2026-11-13, recorded by the parent):** ACCEPT, conditional. A front-only wall is the stage-2 "Walled yard" candidate, on the front margin row only. It stays provisional (P1-F5) until T527 shows: the C8 ring proof with zero wall pixels in any ring, the wall not outweighing the front-row machine plates (Art Director E1; otherwise the top band drops to `steel_1`), and the SC-006 shuffled-order trial at 9 of 10 or better (Game Designer/PO). No side or back wall unless a keep-out proof allows it. The front-row-only rule and the iso-diagonal piece layout go into Art Bible §21.4 with the parent's revision. Carried as P2-F23.

### P2-F9: hub entry glyph for "Office"

The positions differ.

| Lens | Position |
|---|---|
| Art Director | **CHANGES_REQUIRED (Major U1).** Recommends a NEW 24 px `ui_text` glyph (for example a desk tray or a stack of cards), authored through the F003 UI-icon pipeline with an Art Director pixel review. Reason: the hub lists Contracts (FR-020), which already uses `F003-UI-ICON-TAB-CONTRACTS` as its own glyph in `RunShellDocument`'s tab icon set, so the parent entry and one child would look identical, and a clipboard says "one list of orders", not "all systems". Adding any UI sprite is itself an owner decision under 21.2; the T523 "0 sprites" count then becomes 1. Fallback if declined: keep the visible text label "Office" plus the accessible name and use the contracts glyph, accepting the ambiguity. |
| World Designer | Accept the contracts glyph for now (P3). Condition: the hub chip always shows the word "Office" next to the glyph (never icon alone, per the world-copy self-check), and T535 verifies the hub icon is distinguishable from the Contracts row icon. If confusion shows in the human playtest, commission a distinct icon then. |
| UI/UX | Accept. The glyph reads as paperwork, which fits "Office"; the visible label plus the tooltip carry the meaning. Avoids a new sprite, review and palette cost. A distinct hub glyph later is a normal 0-to-1 sprite change with an owner decision per 21.2. |
| Game Designer/PO | Accept for now, with a re-check in T535/T540. Require the text label to show at 200 % text in the compact strip (UI/UX C3). Add the first-run question "what do you expect to find under this button?" to the T535/T540 playtest. Do not draw a new icon now: spend without evidence. |

**Owner decision (2026-11-13, recorded by the parent):** a NEW "Office" glyph, as the Art Director requires. The hub entry gets its own 24 px `ui_text` glyph, authored through the F003 UI-icon pipeline with an Art Director pixel review. This adds one F006-UI sprite under §21.2, so the T523 "0 sprites" count becomes 1. The visible "Office" label stays beside the glyph at every text scale. Carried as P2-F22.

Banner and dot plainness: AD, UX and PO recommend plain USS; no owner decision needed unless an ornament is requested.

P1-F5 stays provisional by the earlier owner decision; it needs no new decision.
