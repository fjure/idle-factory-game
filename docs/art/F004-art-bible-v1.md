# F004 Art Bible v1.0.4

**Version**: `F004-PAB v1.0.4` (patch of `F004-PAB v1.0`, approved by the owner on 2026-10-30, T426; reviewed content at commit af0ee63; v1.0.1 writes the owner decisions of 2026-11-03 on the T439 review, T467 (#640), precisions only and no style change; v1.0.2 records the owner-confirmed T456 lattice result of 2026-11-04 (section 3.5); v1.0.3 records the owner's APK cap of 150 MB of 2026-11-04 (budget 10, R-3); v1.0.4 extends DEV-2 and DEV-4 to the portrait 720 x 1280 phone at 320 dpi, owner decision of 2026-11-05 on T442 finding F1 (sections 3.2, 4.1, 4.2, 4.3, 8.4); the same v1.0.4 errata also marks the fallback-frame statements superseded after T476 (#670, FR-024 fails closed; sections 0 and 10, see the revision log); see the revision log in section 20)

**Status**: Authored by the Art Director author session (T424, Issue #536). The owner signed it off on 2026-10-30 (T426, Issue #538) at commit af0ee63; it is frozen at D24 and governs T427 to T447. The independent review of T425 (second-session Art Director, UI/UX Reviewer, Game Designer/PO) is recorded in `specs/004-visual-identity/reviews/art-bible-review.md`; revision 1 (T461, Issue #615) revises this document for its conditions R1 to R5, R7, R8, R11, R12 and R15 under the owner decisions of 2026-10-29. R6, R9, R10, R13 and R14 belong to other tasks (section 19). Production (T434 to T438) starts after T462 (#616), which builds the checks this document requires. The author does not approve this document.

**Scope**: Feature 004 (`specs/004-visual-identity`, FR-003, FR-009, FR-021a, FR-022; research R18). It supersedes `F003-PAB v0.1` ([`F003-provisional-art-bible-v0.1.md`](./F003-provisional-art-bible-v0.1.md)) section by section (section 17). It changes no gameplay, content or save state (FR-001).

**Companion brief**: [`F004-asset-briefs-v1.md`](./F004-asset-briefs-v1.md) (`F004-AB v1.0`). This document holds the rules; the brief holds the rows.

**Unity target**: `6000.6.0f1`, URP 17.6.0 with the 2D Renderer (T411), Unlit default sprite material, Gamma colour space.

**How to read the numbers**: every value is tagged by its basis. `measured` cites a committed record. `derived` is arithmetic on measured values, shown. `rule` is a new Art Director rule without a measurement; the task that will measure it is named. A `rule` is not evidence.

## 0. Source decisions (fixed, not reopened here)

| Decision | Value | Record |
|---|---|---|
| Projection (S1) | B, isometric 2:1 | owner 2026-10-28 (T417), `reviews/style-spike.md` section S1 Owner decision |
| Density (S2) | D = 24 art px per cell, sprite PPU = D = 24, point filtered, whole-number pixel scales only. ~~The 64 PPU F003 placeholders are fallback frames only~~ superseded (errata v1.0.4): the F003 placeholders were deleted by T476 (#670, owner decision 2026-11-05 on T447 option D) and FR-024 now fails closed, with no fallback frames (contract `presentation-and-state-cues.md`) | owner 2026-10-29 (T420), section S2 Owner decision (T420); spec FR-011, OD-4; research R10 |
| Cue variant (S3) | b: uniform pixel badge per state at 23 art px on every screen class, overlay anchor bottom-centre (59, 33) of the D24 machine canvas, no badge on running machines; `waiting` badge is the T460 revision | owner 2026-10-29 (T423), section Owner timed trial, round 2, and final decisions S3/S4 |
| Silhouette IoU | at most 0.70 on machine bodies only; badges are overlays and excluded | same record |
| Lighting (S4) | B: flat body plus 2D-light accents (Light2D or additive accent sprite) | same record |

Evidence behind them: SC-001 blind naming of the S3 winner 9/9 in grayscale and three CVD filters (`b23`, `b17`, `b23-tabLmin`, `b23r2`), SC-002 round 2 10/10 within 2.0 s (median 1.077 s) (`reviews/style-spike.md` section S3/S4, section Revised `waiting` badge, section Owner timed trial, round 2).

## 1. Principles

1. The factory reads at a glance with 12 slots in view. State is read from shape first and from colour last (FR-010). Running is read by the absence of a badge.
2. All art is pixel art on one grid: whole art pixels, whole-number scales, no dithering, no gradient, alpha 0 or 255, every colour from palette v1.
3. One isometric 2:1 projection, one top-left key light, one density. Machines never rotate.
4. Every rule here is checkable by `tools/art-pipeline checks`, a Unity test or a named review. A rule with no named checker is a defect of this document.
5. The owner decides style; agents produce and review; the author of a document never approves it (research R18).

## 2. Projection and light

| Item | Rule | Basis |
|---|---|---|
| Projection | Isometric 2:1: ground edges run at 2 px horizontal per 1 px vertical (26.565 degrees), the plate silhouette is a 2:1 diamond (70 x 35 art px) whose centre is 2.5 art px above the pivot (footprint box, plate diamond and hit shape: section 3.2a) | measured (S2 plate bbox 5..75 x 29..64) |
| Rendering | Blender rig `rig-v2.blend` (true 2:1; `rig-v1.blend` rendered ground edges at 2.236:1 and is kept for provenance only), orthographic isometric camera, Cycles CPU seed 0, 16 samples, no denoiser, Standard view transform, filter size 0, transparent film | tool pins (section 14) |
| Light direction | Key light from the upper left of the screen. Ramp order: top faces lightest, left-facing faces mid, right-facing and front faces darkest. `lightDirection` in every brief row is `top-left`. The rim ring (the pixel row just inside the outline) follows this ramp; no tone is forced on it (sections 6.1 and 9.3) | rule (rig key light, research R07); checked by T425 and T439 review at 1x. The ramp is encoded by per-orientation flat emission (OD-1, section 2.1) |
| Machine slot lattice | The 12 slots sit on an isometric lattice (T456). Constraints handed to T456 are in section 3.5 | owner decision S1 (condition 1) |

### 2.1 Lighting B: the light model

Lighting B means the body is flat and carries no baked highlight beyond its fixed ramp; the light is an accent on top (owner decision S4). The fixed three-tone ramp (top lightest, left mid, right and front darkest, section 2) is encoded by flat emission materials per face orientation, so the rig key light is unused for the body; this is Lighting B, not a departure from it (owner decision OD-1, 2026-11-03). Spike evidence: `s3-lightB-body-run`, `s3-lightB-accent-run`, `s3-lightB-comp-run` (`art-source/f004/spike/s3-s4/`). The Art Bible defines the model in two levels. Level L1 ships. Level L2 is an envelope that is allowed only after a measurement.

**L1, shipping model (accent sprite).** One accent layer per running machine, drawn as opaque palette pixels (lamp glow in `lit_0` and `lit_1`, rim highlight in `steel_3` on the upper-left boundary) over the body. It has the same canvas, pivot, atlas and material as the body, is a child of the machine's Sorting Group, and has the same 6 frames at 8 fps as the body loop. It is not a blend: alpha is 0 or 255 and every pixel is a palette colour (checked by `palette` and `alpha`). Key family: `F004-ACCENT-*` (brief section 3). Reason for choosing the accent sprite as the shipping implementation: it stays inside the palette and the crispness rule, adds no shader variant and no light render texture, and the owner decision allows it.

| L1 limit | Value | Basis |
|---|---|---|
| Accent sprites on screen | at most 12 (one per running occupied slot); none for any other state | rule; asserted by the densest-overlay Play Mode test (T429) |
| Blend | none (alpha 0/255, same material as the body) | rule |
| Light2D objects | 0 | rule |
| Frames | 6 at 8 fps, aligned with the body loop; reduced motion forces frame 0 for body and accent together | research R05 |
| Flash metric | measured on the body plus accent composite | rule; `check_flash` (T434) |

**Lamp, lit-pixel and accent mask rules (T425 finding R5).**

| ID | Rule | Basis |
|---|---|---|
| L-1 | Lamp pixels live only in the body's lamp area and in the accent layer. The emissive tones `lit_0` and `lit_1` appear nowhere else. Spike Miner run frame 0: 24 lit pixels in one lamp area (columns 46..52, rows 47..50); the accent adds 42 | measured (`s3-lightB-body-run`, `s3-lightB-accent-run`) |
| L-2 | Frame 0 lights-on is measured on the **body frame alone**: frame 0 holds at least 1 lit pixel and no other body frame holds more lit pixels than frame 0. The accent is not part of the still state (it exists only for running machines) | rule; `check_frame0_lights_on` on body frames (T434) |
| L-3 | A lit lamp on a stalled machine (paused, inactive, waiting, missing input, storage full) is **accepted**: under S3 = b the body is unchanged and the badge carries the state; SC-001 and SC-002 were passed with exactly this | owner decision S3; measured (spike composites) |
| L-4 | The accent mask is a subset of the body mask, never on an outline or boundary pixel. An accent layer has no outline of its own and is exempt from `check_outline`. It is still checked by `palette`, `alpha`, `canvas`, `pivot`, `frames` and, as a body-plus-accent composite, `flash` | measured: 95 of 95 accent pixels inside the body mask, 0 on ink, 0 on the boundary; rule; the mask-subset check is part of T462 (section 19, O-4) |
| L-5 | The floor never sits on a lit layer: accents exist only over the machine layer, and the floor and plate sorting layers are excluded from every Light2D target (L2) | rule; T429 and T440 verify the sorting layers |

**L2, Light2D envelope (reserved, not shipped).** A Light2D glow may be added only by a minor Art Bible version after the measurement below passes. The envelope is the maximum:

| L2 limit per screen (all 12 slots in view) | Value |
|---|---|
| Point Light2D | at most 12 (one per running machine); 0 Global, 0 Spot, 0 Freeform, 0 Sprite-type lights |
| Blend style | exactly one, Additive (index 1 of `IdleFactory2DRenderer`); no Multiply, so nothing darkens |
| Shadows, normal maps, volumetrics | off |
| Outer radius | at most 1.0 world unit (one cell, 24 art px) |
| Intensity | at most 1.0, constant (no flicker, no pulse; flash rule) |
| Target sorting layers | the machine layer only; the floor and plate layers (L-5) and the overlay layer (badges, markers, labels, selection frame) are never lit, so every contrast value in section 9 stays valid |
| Colour | `lit_0` or `lit_1` only |
| Light render textures | one (Additive), at the existing `m_LightRenderTextureScale` 0.5; a change of that scale is a Technical Art decision with a crispness measurement (the half-resolution light texture is not pixel-aligned) |

These L2 numbers are `rule`s; they are limits to measure against, not measurements. Enabling 2D lights also requires `m_Strip2DUnusedVariants` set to 0 (wiki `f004-urp-phase0-results`), which raises the shader-variant count above the frozen 490 and needs the written explanation of frozen budget 11.

**Darkest lighting.** Lighting B has no Multiply light and no global dimming, so the darkest lighting the Art Bible allows is 1.0 (unlit sprite colours). All contrast values in section 9 are therefore full-light values and `check_contrast` runs with `darkest_light = 1.0`. The 0.6 factor in the palette-v0 tooling was a placeholder and is retired. Any later global dimming (night mode) is a minor version and re-runs section 9 at the new value.

### 2.2 Render cost allocation against the frozen B2 budgets

Budgets are those of `specs/004-visual-identity/reviews/performance-budgets.md` section 4 (frozen by T412 at B2 `b9221ca`, wiki `f004-b2-frozen-budgets`). Nothing here is measured: Unity and the emulator are out of scope for T424. B3 (T444) is the measurement; T442 supplies the Windows-player allocation run.

| Frozen budget (number) | Allocation to lighting B | Measured by | Pass criterion |
|---|---|---|---|
| 1 to 3: average FPS at least 60, p95 present-interval bucket at most 19 ms, 0 intervals of 50 ms or more | L1: at most 12 extra sprite renderers (all 12 slots running). L2: at most 12 Point lights | T444, W004-V and W003-S with all 12 slots running and lit | all three met with lighting on, in a valid ABA session |
| 4: Windows host frame ms p95 within the T122 range (0.02 to 0.04 ms) | none expected | T444 | unchanged |
| 5: 0 steady-state GC allocations per frame | accent frame index comes from the shared clock, lights are static: 0 allocations | T431 (`PresentationAllocationTests`), T442 (600-frame W004-V player run) | 0 samples with accents on and, if L2 is adopted, with lights on |
| 6 and 7: world-art texture at most 8 MB, atlas pages at most 1024 x 1024 | accents: 4 machines x 6 frames x 80 x 64 x 4 B = 491,520 B (0.47 MB, derived, before padding) | T444 build report | the whole world set within 8 MB; accents are about 6 % of the budget |
| 8: SetPass per world-camera render on W004-V at most 0.86 (B2 0.78 + 10 %) | L1: same material and atlas family as the body, so no added state change is expected. L2: adds a light pass | T444 (graphical Editor probe, same method as the B2 addendum) | at most 0.86 with lighting on |
| 9: guest CPU regression at most max(10 %, 2 points) | none expected | T444 | within |
| 10: APK file size at most 150 MB (owner decision 2026-11-04, replaces "+10 MB against the paired B1") | B2 is 46.3 MB; T440 added +0.09 MB of entries for seven families | T444 (clean build) | within; no longer the tightest budget (risk R-3, section 19) |
| 11: shader variants not above 490 without explanation | L1 adds none. L2 adds variants (see above) | T444 build report | 490 or fewer for L1; L2 needs a written explanation |

Attribution rule: T444 measures B3 with the accent layer on and in a same-session control with it off, so a miss is attributed before any readability change (a readability reduction needs an owner decision, plan "Performance"). Worst-case renderer count per 12-slot factory with L1: running slot = base, machine, accent, 2 markers; other slot = base, machine, badge, 2 markers; both 5, plus 1 selection frame = at most 61, equal to the F003 cap of 61 (`F003-PAB` section 6.1). `F003-BADGE-RUNNING` is a manifest entry flagged `intentionallyEmpty` with no PNG, and no renderer is drawn for it (owner decision R8, section 8.1; T429).

## 3. Density, canvas, footprint, pivot

### 3.1 Density

`D` = 24 art px per cell = the sprite PPU (data-model "Art density and scale policy"). One cell is one world unit. `readableCellPoints` = 24 (F003 Z24), `maxScale` = 6.

### 3.2 Canvas, footprint, pivot table

All values in art px, origin bottom-left for the pivot, y down inside the canvas for pixel coordinates. A pivot is always a whole art pixel (section 3.3).

| Asset class | Canvas | Footprint | Pivot (px) | Pivot (normalised) | Basis |
|---|---|---|---|---|---|
| Slot plates, machine bodies and accents, selection frame, overlay layers (badges, markers, labels) | 80 x 64 | footprint box 68 x 30, centred, resting on the canvas bottom (section 3.2a) | (40, 15) | (0.5, 0.234375) | measured (`candidates-D24.json`, S2 and S3) |
| Resource sprites (Ore, Bar, Plate, Gear) | 32 x 24 | 20 x 10, centred, resting on the canvas bottom | (16, 5) | (0.5, 0.208333) | measured canvas; footprint amended from 20 x 9 (section 3.3) |
| Floor tile | derived from the T456 lattice (section 3.5) | 2:1 diamond, width and height even | footprint centre | whole px | rule; T435 proposes, T456 confirms |
| Item icons (UI) | 32 x 32 | n/a | (16, 16) | (0.5, 0.5) | rule |
| Tier chips (UI) | 32 x 16 | n/a | (16, 8) | (0.5, 0.5) | rule |
| UI glyph icons | 24 x 24 | n/a | (12, 12) | (0.5, 0.5) | rule |

Overlay layers are full machine-canvas sprites (80 x 64, pivot (40, 15)). The anchor is therefore baked into pixel placement and is relative to the footprint, never to the sprite bounds (contract `presentation-and-state-cues.md`). A tight 23 x 23 badge sprite is not used: its centre would fall on a half pixel (11.5) and it needs a second pivot convention.

Cue geometry in machine-canvas pixels (y down), `measured` for the badge (all five b23 cue sprites have the opaque box x 48..71, y 10..33):

| Overlay | Box | Anchor | Offset from the machine pivot (40, 49 from top) |
|---|---|---|---|
| Badge, 23 x 23 | x 48..70, y 10..32 | bottom-centre (59, 33) | +19 px right, +16 px up = (0.7917, 0.6667) world units |
| Marker 1, 11 x 11 | x 24..34, y 16..26 | rule | proposal; fixed by T437 and accepted by T429 |
| Marker 2, 11 x 11 | x 36..46, y 16..26 | rule | same |

Marker size 11 is derived from the F003 marker floor of 10 pt (`F003-PAB` section 8.2): 11 art px give 16.5 pt (320 dpi phones, n = 3), 16.3 pt (portrait tablet), 12.5 pt (emulator), 11.8 pt (tablet landscape, n = 2), 11.5 pt (460 dpi phones) and 11.0 pt (320 dpi phones at n = 2: 1280 x 720 and, from v1.0.4, 720 x 1280, where n = 3 gives 16.5 pt only without the tolerance); all at least 10 pt. The marker rows end at y = 26, leaving a 2-row gap (rows 27 and 28) above the plate apex (row 29, measured). The first layout ended at row 28 and touched the apex (T425 finding R7); it is replaced. Markers are 11 x 11 as a named deviation below 24 pt (DEV-3, section 4.3).

### 3.2a Footprint, plate diamond and hit shape (T425 finding R3)

Two names, one definition each. All numbers `measured` from `s2-slot-occupied` and `candidates-D24.json`.

| Name | Definition | Dimensions (art px) | Position |
|---|---|---|---|
| Footprint box | the pipeline rectangle `footprintPx`: pivot derivation, overhang reference (row 34) and keep-out reference. Not a drawn shape | 68 x 30 (canvas width 80, so all three are even: P1) | columns 6..73, rows 34..63; centre = the pivot (40, 15 from the bottom) |
| Plate diamond | the plate silhouette: a 2:1 diamond with a 4 px clipped apex | 70 x 35 (row 29 spans columns 38..41, the single widest row 46 spans columns 5..74 at 70 px, row 63 spans columns 38..41) | centre column 40, row 46.5 from the top = **2.5 art px above the pivot** |
| Hit-test shape (T456) | the plate diamond, not the box | 70 x 35, 2:1 | centre at the pivot plus (0, +2.5 art px) = (0, +0.1042) world units |

Plates are flat: a true 2:1 70 x 35 diamond leaves no room for a wall inside the exact hit and selection shape, so a plate carries no wall or in-box depth cue (owner decision OD-3, 2026-11-03).

The 2.5 px offset is a half pixel and is accepted for the hit shape only: hit testing is geometry in world units, and P1 governs the pivots of drawn sprites. The plate diamond is the only shape with an odd dimension (35) and it is not a pivot-bearing footprint. The selection frame follows the plate diamond. A floor-tile period of 60 x 30 (slot spacing 60 px, 2:1) is the natural candidate; it is a proposal for T435 and T456 (rule).

### 3.3 Whole-art-pixel pivots (S2 Art Director condition 4)

Problem measured in the spike: D16 pivots were half pixels in x (26.5, 10.5), and D24 ore and ingot have y = 4.5, which put 2.5 % of vertical edges off the grid at D24 (96.8 to 97.5 % on grid) and broke D16 at n = 3 (`reviews/style-spike.md` section S2 gate 6). Slot, machine and plate pivots at D24 are already whole (40, 15).

**Rule P1.** For every sprite with a footprint, canvas width, footprint width and footprint height are all even. Then `left = (cw - fw) / 2` and the pivot `(left + fw / 2, fh / 2)` are whole. Odd values are rejected by `check_pivot` once it also asserts that the pivot in pixels is whole with a float tolerance: `|pivotPx - round(pivotPx)| < 1e-3`, where `pivotPx` is the normalised pivot times the canvas (5 / 24 = 0.208333 is not float-exact, 15 / 64 is; T425 finding R11). Checks task: T462, section 19 O-4; the importer check of T430 uses the same tolerance.

**Resolution of the D24 ore and ingot pivot.** The footprint changes from 20 x 9 to **20 x 10**, so the pivot is **(16, 5)**, normalised (0.5, 0.208333). Reason: a 2:1 ground diamond of width 20 is 10 high exactly (the plate is 70 x 35); 9 was a rounding artefact of the spike rig and the only odd value; the choice moves the sprite by 0.5 art px relative to its anchor, which is below one art pixel and changes no pixel; choosing 4 would make the diamond 20 x 8, which is not 2:1. The spike sprites are not edited (they are not production). The production resources are rendered with the 20 x 10 footprint (T436).

**Rule P2.** Overlay and label sprites that are not full-canvas layers must declare an explicit whole-pixel pivot `floor(w / 2)`; this is the only allowed exception and no asset uses it in v1.0.

**Rule P3.** Every placement position is a whole number of art pixels in world space, so the camera snap to `1 / (n * D)` world units (one device pixel) keeps every art pixel an n x n block. Slot spacing at D24 (60 px for 2.5 cells) is whole; T456 must keep every lattice position whole.

### 3.4 Overhang and overlay keep-out

| Rule | Value | Basis |
|---|---|---|
| Machine body overhang above the footprint top (canvas row 34) | 0 px: machine art starts at row 34 or lower | measured: machine bodies start at row 34 (Assembler) and 37 (Miner), overhang 0 px in all S2 and S3 reports |
| Plate and selection-frame overhang | at most 12 px (0.5 cell, research R04) | measured: plates 3 to 5 px, selection frame 3 px |
| Overlay keep-out | no machine pixel in canvas rows 0..33 of columns 15..71 (badge and marker band plus a 1 px margin) | derived from the S3 composite geometry; checked by T434 (`alpha` mask intersection; small new check, section 19) |
| Badge height above the footprint top | 24 px (1.0 cell), the badge reaches twice the 0.5-cell machine overhang | measured (`overhangCells` 1.0 for every `s3b-comp-*-23`); overlays are exempt from the overhang check |

The 0.5-cell machine overhang allowance of research R04 is therefore withdrawn for bodies: the band above the footprint belongs to overlays. Consequence for T456 and T429: because the badge reaches 1.0 cell above the footprint top, the lattice and the densest-overlay Play Mode frame (12 occupied slots, 1 cue + 2 markers + selection at `n_min`) must prove that no badge hides another slot's cue, marker, selection frame or label (FR-015). That proof is not made here.

### 3.5 Lattice constraints handed to T456 (S1 condition 1, S2 condition 5)

The spike lays the isometric plates on the rectangular F003 lattice; plate width over spacing is 1.17 (70 px at 60 px) so plates overlap (`reviews/style-spike.md` section S2). T456 owns plate spacing, row sorting, footprint-only hit testing and the fit-all extent. The Art Bible adds these constraints: all positions whole art pixels (P3); the floor tile period matches the lattice; the badge band (rows 10..32 above each footprint) must not cover another slot's cue, marker, frame or label, and the backdrop under every overlay box must lie in the allowed tone set with the numeric acceptance of section 9.4 (C1, C3, C8); machine bodies keep the keep-out of section 3.4.

**T456 result (lattice, #592).** The 12 plates sit on a rectangular lattice of diamonds with a **pitch of 70 x 54 art px** (columns 70, rows 54; 2.9167 x 2.25 cells), 3 columns by 4 rows in portrait and 4 by 3 in landscape, every position whole (P3). Reasons, measured by `SlotGridLatticeTests`: pitch 70 equals the plate width, so plates touch at their side apexes and never overlap, and it is the widest pitch at which fit-all still holds on the 1536 x 2048 tablet at 264 dpi (pitch 72 misses n_min = 3 by 1.6 %, FR-011); a row pitch of 54 is the smallest at which the badge band (rows 10..32, up to 39 px above the pivot) stays off the 3 px selection-frame band of the slot behind (52 touches it). A rotated (true isometric projection) lattice was evaluated and rejected: its back neighbours sit 18..20 px up and the badge box lands on their frame band for 14 to 15 slot pairs until the step reaches about 72 px, which needs a 430 px wide extent that fails n_min on phones. Sorting: `sortingOrder` is the row from the back (front rows draw later, R04); each machine is one Sorting Group. Hit testing: the plate diamond grown to 90 x 45 art px (a 45 px tap height keeps 44 pt at every allowed scale) clipped to the 70 x 54 cell, so cells never overlap and the machine overhang or badge band never captures a tap. Extent: plates plus the badge band above the back row (210 x 216 px portrait, 280 x 162 px landscape). **Floor period: the 60 x 30 proposal is replaced** (60 px spacing is below the 70 px plate width). The floor tile pattern repeats with a period of **70 x 54 px** (the lattice pitch), so every slot sees the same floor phase; T435 authors the floor art for that period (a 2:1 diamond floor of 70 x 35 has an odd height and is not whole-pixel). Consequence for the Assembler rim sample (0.875 on `floor_1`, ungated): with period equal to pitch all 12 slots share one floor phase under their machine, so that sample is a single value for the whole grid, not a per-slot spread; it must be re-measured once on the T435 floor art.

## 4. Scale and zoom (supersedes F003 section 8.1 and 8.2)

Only whole-number device-pixel scales `n` are allowed, including fit-all; unused screen space is margin (OD-4, FR-011). `n_min = ceil(readableCellPoints * pxPerPoint / D - 0.01)` (the 1 % tolerance is the owner decision of 2026-10-28, implemented by T427), allowed set `[n_min, 6]`, `n_fit` the largest `n` at which 12 slots plus a half-cell margin fit the HUD-aware rectangle. At D = 24 and `readableCellPoints` = 24 this is `ceil(pxPerPoint - 0.01)`.

### 4.1 D24 zoom-level table

`measured` (`reviews/style-spike.md` section S2, `ScreenClassScaleTableTests`; the 1280 x 720 and 720 x 1280 rows show both values). **Errata v1.0.4:** the 720 x 1280 row is re-stated from the real-shell Play Mode measurement of T442 (`reviews/final-test-evidence.md` section 7a: 2.0045 px/pt, `n_min` 2, `n_fit` 2, HUD rects 624 x 871 overlays on / 624 x 1068 off, width slack 156 px at `n_fit`, which includes the half-cell margin); levels are the allowed set [2, 6] with the tolerance (4 without), and the 1280 x 720 row's levels are aligned the same way; the margin is the share of the rect width not covered by the 210 px extent at `n_fit` (the table's definition), measured in the real shell; the pre-tolerance Edit Mode model figure of this row (18.2 %, `n_fit` 3) is replaced; the other rows remain Edit Mode model figures. Precision, tablet landscape 2048 x 1536: the row lists `n_fit` 5, the shell run measures `n_fit` 4 (T442 section 7a and T445 (#563) `readability-final.md`, measured scales); the row is kept as the Edit Mode model figure and no rule changes.

| Screen class (reference device) | px per pt | `n_min` | `n_fit` | Levels | Levels at or below fit-all | Margin at fit-all |
|---|---|---|---|---|---|---|
| Phone portrait 720 x 1280, 320 dpi | 2.004 | 3 (2 with the 1 % tolerance) | 2 | 5 (2..6 with the tolerance; 4 without) | without the tolerance 3 is above `n_fit` (a violation, 0); with it **1, fit-all only** | 32.7 % (real shell, T442 section 7a: 1 - 210 x 2 / 624 px rect width, extent without the half-cell margin as in the other rows; the 156 px slack in 7a includes it) |
| Phone landscape 1280 x 720, 320 dpi | 2.004 | 3 (2 with the 1 % tolerance) | 2 | 5 (2..6 with the tolerance; 4 without) | without the tolerance a violation (0); with it **1, fit-all only** | 40.0 % |
| Phone portrait 1080 x 2400, 420 dpi (emulator) | 2.631 | 3 | 4 | 4 | 2 | 28.8 % |
| Phone landscape 2400 x 1080, 420 dpi | 2.631 | 3 | 4 | 4 | 2 | 22.7 % |
| Phone portrait 1170 x 2532, 460 dpi | 2.881 | 3 | 5 | 4 | 3 | 17.7 % |
| Phone landscape 2532 x 1170, 460 dpi | 2.881 | 3 | 4 | 4 | 2 | 28.5 % |
| Tablet portrait 1536 x 2048, 264 dpi | 2.022 | 3 | 3 | 4 | **1, fit-all only** | 28.3 % |
| Tablet landscape 2048 x 1536, 264 dpi | 1.867 | 2 | 5 | 5 | 4 | 10.9 % |
| Desktop 1280 x 720 / 1920 x 1080 / 2560 x 1440 | 1 / 1.5 / 2 | 1 / 2 / 2 | 3 / 5 / 6 | 6 / 5 / 5 | 3 / 4 / 5 | 21.2 / 12.4 / 21.2 % |

### 4.2 Standing accepted risks (S2 Art Director condition 1)

| ID | Risk | Owner of the proof | Status |
|---|---|---|---|
| AR-1 | **Fit-all only, no zoom-out** on the phone 720 x 1280 (320 dpi, with the tolerance, v1.0.4), the phone 1280 x 720 (320 dpi, with the tolerance) and the portrait tablet 1536 x 2048. Zoom there goes only in, toward `maxScale`. No zoom-out mechanism is added. | T427 owns the `n_min` tolerance and its HUD-aware Play Mode proof; T442 is the gate that confirms it | accepted; T427 depends on T426 so the proof cannot precede the freeze (owner decision T420) |
| AR-2 | 1280 x 720 phone and (v1.0.4, T442 F1) portrait 720 x 1280 phone: `n_fit` 2 is below `n_min` 3 without the tolerance (measured portrait `n_fit` 2, T442 section 7a) | T427 (tolerance, negative case with an oversized HUD) | accepted by owner decision 2026-10-28 |

### 4.3 The remaining 24 pt gaps at 23 art px (carry-in 2)

The rule is a cue icon of at least 24 pt at `n_min` (research R06). `measured` / `derived` from `reviews/style-spike.md` section S3/S4 "Cue size in points per class": icon pt = art px x `n_min` / px per pt.

| Class | `n_min` | 23 px at `n_min` | Result |
|---|---|---|---|
| 320 dpi phones, portrait 720 x 1280 (v1.0.4: real shell, 2.0045 px/pt, `n_min` 2 with the tolerance, T442 sections 7a, 7d; derived from the logged real-shell `n_min`, not asserted) | 2 | **22.95 pt**, 1.05 pt (4.4 %) short | named deviation **DEV-2** (same as the 1280 x 720 phone) |
| Portrait tablet | 3 | 34.1 pt | meets |
| Emulator 420 dpi | 3 | 26.2 pt | meets |
| Tablet landscape | 2 | 24.6 pt | meets |
| **460 dpi phones** (not captured) | 3 | **23.95 pt**, 0.05 pt (0.2 %) short | named deviation **DEV-1** |
| **1280 x 720 phone, if the T427 tolerance gives n = 2** (confirmed by T442: Play Mode asserts `n_min == 2`) | 2 | **22.95 pt**, 1.05 pt (4.4 %) short | named deviation **DEV-2** |

Both are recorded as accepted deviations, carried by the owner decision of 2026-10-29. **v1.0.4 (owner decision 2026-11-05, T442 finding F1):** DEV-2 and DEV-4 extend to the portrait 720 x 1280 phone at 320 dpi (`n_min` 2, `n_fit` 2, 22.95 pt world cue, 14.97 pt digit), accepted deviations of the same kind; the 24 pt rule, the 23 px icon set and the tolerance do not change. Blind naming at n = 2 passed on both 320 dpi phone classes: T445 (#563) `readability-final.md` SC-001, gate fields (states, markers, slot conditions) 22/22 in grayscale and the three CVD filters on `phoneL-1280x720-n2` and `phoneP-720x1280-n2`. No new icon size is adopted: the 23 px set is final and the 17 px set is not used. Owner of the closure: **T427** (class table, tolerance, HUD-aware figures) and **T442** (final gate confirms the figures on the shipped build). T426 confirms the acceptance at sign-off.

**DEV-4 (lock-level digit, 320 dpi phones at n = 2: 1280 x 720 and, from v1.0.4, 720 x 1280).** The owner target for the digit of `F004-LABEL-LOCK-L2..L5` is a cap height of 15.0 pt or more on every class. At scale 3 (cap 15 art px) the 1280 x 720 phone at n = 2 reaches 14.97 pt (15 * 2 / 2.004), 0.03 pt (0.2 %) below the target and well above the 12 pt label threshold. It is accepted pending T426, and it is tied to DEV-2: it exists only if the T427 tolerance gives n = 2 on that phone (without the tolerance n = 3 gives 22.46 pt). The same figure holds on the portrait 720 x 1280 phone (15 x 2 / 2.0045 = 14.97 pt, T442 section 7d). Blind naming of the lock digits 2 to 5 at n = 2 passed on both phone classes (T445 SC-001, 22/22). Owners: T427 and T442, as for DEV-2.

**DEV-3 (markers; owner decision of 2026-10-29, T425 finding R7).** The markers are 11 x 11 art px and fall below the 24 pt cue rule (11.0 to 16.5 pt, section 3.2). They are not state cues, they meet the F003 marker floor of 10 pt on every measured class, and the owner accepted the size as a named deviation. Compensation: marker and CVD naming is **blocking** in T439 (markers are added to the shuffled sheets) and in T445, including 1280 x 720 at n = 2.

## 5. Palette v1

`F004-palette` version **1.0.0**. Machine-readable form: `tools/art-pipeline/palette/palette-v1.json`, produced from palette v0.1.0 (sha256 `2d2ca24f42242bf5dacdd9d842fd94ee67dea4a820944e2bdb6539604f004afb`) at the T426 freeze; hex values are unchanged from v0.1.0, so every spike measurement (IoU, group budgets, CVD, contrast) stays valid. Cap 48; palette v0.1.0 already holds 48 (cap reached). Any removal, hue change or growth beyond 48 is a major version (section 16). The spike sets used 29 (D16) and 30 (D24) colours (`s2/check-report.json`).

**Licence**: authored. No third-party palette. The seven `cvd_safe` values equal the published Okabe-Ito set; colour values carry no known licence obligation and the equality is recorded for provenance only.

| Group (budget) | Colours (id #hex) | Use in v1.0 |
|---|---|---|
| ink (2) | `ink_0` #14121c, `ink_1` #2a2438 | `ink_0` outline and badge/marker glyphs; `ink_1` interior separators only, never a badge glyph (ink_1 on `cue_alert` is 4.16, below 4.5) |
| machine (14) | `steel_0` #3c4256, `steel_1` #5a6378, `steel_2` #8a93a6, `steel_3` #c0c7d4, `copper_0` #5e3a2c, `copper_1` #9a5b3a, `copper_2` #d08a52, `teal_0` #1f4f55, `teal_1` #6fc9bd, `dim_0` #2e3340, `dim_1` #4a4f5c, `warn_0` #8a3b3b, `lit_0` #ffd36b (emissive), `lit_1` #fff0b0 (emissive) | body ramps (section 6.2). `dim_*` are shadow tones inside the ramp, not a dimmed body. `warn_0` is reserved: the warning body variant of the spike is not used under S3 = b |
| floor (6) | `floor_0` #2a2f3a, `floor_1` #363c4a, `floor_2` #444b5a, `plate_0` #566070, `plate_1` #6c788a, `plate_2` #8d9aae | field tones `floor_0` and `floor_1`; `floor_2` only as 1 px tile highlights; plate top `plate_1`, plate rim `plate_2` |
| resources (8) | `res_ore_0` #7a5a3c, `res_ore_1` #b58a55, `res_coal` #3a3b44, `res_crystal` #5bb7e6, `res_leaf` #5da35d, `res_gold` #e0b13a, `res_water` #3f7fd0, `res_plank` #a06f3e | Iron Ore, Bar, Plate, Gear and item pictograms. `res_leaf`, `res_water`, `res_crystal` are unused in v1.0 |
| ui (5) | `ui_bg` #1b1f2a, `ui_panel` #2c3344, `ui_line` #5d6a85, `ui_text` #f2f5fa, `ui_accent` #4fa3ff | locked-plate base, UI glyph base colour (`ui_text`, tinted by tokens), selection frame (`ui_accent`) |
| state_cues (6, reserved) | `cue_warn` #f2b134, `cue_alert` #e8573a, `cue_info` #6ec1ff, `cue_neutral` #d9dde6, `cue_dim` #8b93a5, `cue_ok` #7fd17f | badge fills only; never in machine or floor ramps. `cue_ok` is reserved and unused (running has no badge) |
| cvd_safe (7) | `cvd_blue` #0072b2, `cvd_orange` #e69f00, `cvd_green` #009e73, `cvd_pink` #cc79a7, `cvd_sky` #56b4e9, `cvd_vermilion` #d55e00, `cvd_yellow` #f0e442 | markers: `cvd_green` (boosted), `cvd_pink` (Overdrive); the other five are reserved |

**Family allowances** (the `palette` check runs per family; `measured` from the spike `GROUPS_*`): machines `machine` + `ink`; plates and floor `floor` + `ui` + `machine` + `ink`; resources `resources` + `ink` plus the two ids `steel_2` and `steel_3` for the grey iron tones (needs an id-level allowance, section 19); badges `state_cues` + `ink`; markers `cvd_safe` + `ink`; UI glyphs `ui` (`ui_text`) only; items `resources` + `ink` + `ui`. Per-asset colour maxima follow the spike maxima (`palette_group_budget` in `s2/check-report.json`): machine group 10, floor 5, resources 4, ui 4, ink 1 (D24 values).

World background: the camera clears to `ui_bg`. No background illustration in v1.0 (the 12 slots fill the view; no measurement supports more).

## 6. Rendering rules: outline, shading, dithering, animation, noise limits

### 6.1 Outline

One 1 art px outline in `ink_0` around every silhouette, generated by `pixel_pass` (research R07, `check_outline`: every boundary pixel is ink). Interior ink lines are 1 px, only to separate parts, in `ink_0` or `ink_1`. No coloured outline (no selective outlining) in v1.0. The pixel ring just inside the outline (the rim) is whatever the ramp gives; no tone is forced on it, except C6 pictograms on UI surfaces, whose rim is lifted to the C6 rim tones by the scripted `rim_lift` touch-up recorded in provenance (OD-11). Contrast of an outlined graphic follows section 9.3 (owner decision R1, option (b)).

**Outline exemptions (OD-5, OD-8).** The floor tile is exempt from the outline rule: an outline would break the seamless tiling. UI glyph icons are exempt: they are monochrome tinted glyphs, the tint multiplies the colour so an ink outline cannot be carried, and their contrast is evaluated per surface (C4, section 11).

### 6.2 Shading ramps

Hard-edged ramps, no dithering. Tones per material: steel 4 (`steel_0` to `steel_3`), copper 3 (`copper_0` to `copper_2`), teal 2, dim 2, floor 3 field/highlight tones, plate 2 used (`plate_1`, `plate_2`). Ramp assignment follows section 2 (top faces lightest). The emissive tones `lit_0`, `lit_1` appear only in the body lamp area and in the accent layer (rule L-1) and nowhere else.

### 6.3 Dithering and anti-aliasing

None. Alpha is 0 or 255 (`check_alpha`); quantisation to the palette uses CIELAB with a deterministic tie-break and no dithering (research R07).

### 6.4 Animation

| Rule | Value | Basis |
|---|---|---|
| Running machine | **6 frames at 8 fps** (0.75 s loop), one fixed canvas and pivot for all frames | research R05; measured (spike loops are 6 frames at 8 fps) |
| Other machine states | one still frame = frame 0 of the same entry; no separate sprite (section 8) | S3 = b |
| Frame 0 | lights-on, so a frozen running machine still reads as running; measured on the body frame alone (rule L-2) | research R05; `check_frame0_lights_on` |
| Flash rule | the mean relative-luminance change over the opaque area between consecutive frames, above 0.10, counts as a flash; **at most 3 flashes per second** (`check_flash`) | research R05 |
| Measured at D24 | Miner run 0 flashes per second; Assembler run **3**, at the limit (`s2/check-report.json` `maxFlashesPerSecond`) | measured. **T434 requirement**: the body-plus-accent composite of every loop is at most 2 flashes per second, one below the limit, because the Assembler spike run is already at 3 (T425 finding R15) |
| Loop resume | a machine that returns to running continues at the shared-clock frame `floor(t * fps) mod 6`; there is no per-machine reset, and the one-time jump from frame 0 to the clock frame is accepted | rule; T429 implements (research R05 clock) |
| Animated art | machine loops (and the matching accent) only. Plates, badges, markers, labels, selection frame and floor are static | FR-019 |
| Reduced motion | forces frame 0 for every entry (FR-020, T431) | contract |

### 6.5 Noise limits (supersedes F003 section 8.5)

| Limit | Value | Basis |
|---|---|---|
| Outline | 1 art px `ink_0` | R07 |
| Smallest feature | 1 art px for body detail; cue glyph strokes at least 2 art px | rule; glyph strokes measured at T425 review |
| Colours per sprite | within the family allowance and per-group maxima of section 5 | measured maxima |
| Palette cap | 48, gate `palette_definition` | contract |
| Silhouette IoU, machine bodies, any pair | at most 0.70 at 80 x 64 and on a 32 x 32 downsample. Measured pair Miner / Assembler: 0.651 and 0.672. Smelter and Press are not drawn yet, so all six pairs are checked at T434 | measured (S2 `silhouette_iou[D24]`); `check_iou` |
| Cue icon | at least 12 x 12 art px (`check_cue_icon_size`); shipped size 23 x 23 | contract; measured (23 x 23 opaque) |
| Sprite layers per slot | at most 5 drawn, plus 1 selection frame on one slot; at most 61 per 12-slot factory | derived (section 2.2) |
| Text in art | locked-plate digits `2` to `5` and tier numerals only; authored 3 x 5 bitmap font | section 8.5 |
| Atlas | one Sprite Atlas v2 per family, RGBA32, point filter, no mipmaps, padding 2, no rotation, pages at most 1024 x 1024 | research R10, budget 7 |

## 7. Machine silhouettes

The four F003 dominant features carry over as concepts. F004 re-draws them in isometric 2:1 on the shared canvas of section 3.2; a body must start at canvas row 34 (section 3.4).

| Machine | Dominant feature (concept) | Spike status |
|---|---|---|
| Miner | wedge body with conveyor stripes | drawn (S1 to S3), the confusion-risk pair member |
| Assembler | gear-like body, teal, with teeth | drawn (S1 to S3), the confusion-risk pair member |
| Smelter | wide furnace block with a chimney stack (inside the 30 px body height) | not drawn; T434 |
| Press | H-beam press: head, narrow stem, base | not drawn; T434 |

Uniqueness: IoU at most 0.70 on bodies only (owner decision S3). Silhouette check inputs are the six body masks of the run frame 0; badges, markers, accents and any cast shadow are excluded. No cast shadow is baked in v1.0 (flat body, lighting B).

## 8. State-cue model

### 8.1 Cue triple and mapping

Per research R06 and the data model, each machine state maps to `(bodyVariant, cueIcon, labelPolicy)`. Under S3 = b the body is **unchanged for every state**: the body entry is the same 6-frame run entry; non-running states show its frame 0 and are not animated. There is no dimmed, desaturated or warning body variant in v1.0 (the clause "body variant carries the state class" of R06 belongs to variant a, which was not chosen). The map follows the keys that `SlotGridModel.BadgeFor` resolves; no second precedence rule exists.

| `BadgeFor` key | State | Badge glyph (shape, primary carrier) | Fill (`state_cues`) | Fill semantic | Body | Animated |
|---|---|---|---|---|---|---|
| `F003-BADGE-RUNNING` | running | none (`intentionallyEmpty` manifest entry, no PNG; no renderer drawn) | none | read by absence | 6-frame loop | yes |
| `F003-BADGE-WAITING` | waiting | bold hourglass with solid upper sand, pile and stream (T460) | `cue_info` | **cool** | frame 0 | no |
| `F003-BADGE-MISSING-INPUT` | missing input | arrow down into a tray line | `cue_alert` | **warm** (problem) | frame 0 | no |
| `F003-BADGE-STORAGE-FULL` | storage full | stack of horizontal bars | `cue_warn` | **warm** (problem) | frame 0 | no |
| `F003-BADGE-PAUSED` | paused | two vertical bars (glyph scaled to 0.62 of the badge) | `cue_neutral` | **neutral** | frame 0 | no |
| `F003-BADGE-INACTIVE` | inactive | ring with a dash (glyph scaled to 0.7) | `cue_dim` | **neutral** | frame 0 | no |

**Running badge (owner decision R8).** `F003-BADGE-RUNNING` is a manifest entry with `intentionallyEmpty: true` and no PNG. It keeps SC-003 (every F003 key resolves to F004 art) and needs no special case in `BadgeFor`: the key resolves to a catalog entry that has no sprite, and the view draws nothing for it. Owners: T437 (manifest entry), T430 and T440 (importer and `F004AssetContractTests` accept the flag), T429 (view). The `outline`, `cue_icon_size`, `canvas` and `pivot` checks do not apply to it.

Glyph and fill sources: `art-source/f004/spike/s3-s4/pipeline_s3.py` (`SPEC_B`, `WAITING_B23_PLATE`), `cue_glyphs.py`. Badge frame: rounded square, 23 x 23, 1 px `ink_0` outline, glyph in `ink_0`.

Precedence (kept from `F003-PAB` section 6.2, as implemented by `SlotGridModel.BadgeFor`): inactive, then missing input, then storage full, then waiting, then paused, then running. T429's `StateCueSet` asserts every `BadgeFor` result against its badge key. At most one badge per slot; markers are independent.

### 8.2 Badge fill colour semantics (carry-in 1)

Cool means waiting (`cue_info`). Warm means problem states (`cue_alert`, `cue_warn`). Neutral means paused or inactive (`cue_neutral`, `cue_dim`). **Shape is the primary carrier**; colour is supplemental (FR-010).

`measured` now with `art_pipeline.color` (Rec. 709 luma, Machado 1.0, CIELAB):

| Check | Result |
|---|---|
| Grayscale luma of the fills | waiting 180, storage full 182, missing input 116, paused 221, inactive 147. Waiting and storage full differ by 2, so grayscale cannot separate them and the glyph must |
| Smallest pairwise CIELAB distance between the five fills | normal 27.8 (paused / inactive); protanopia 27.3 (paused / inactive); deuteranopia 24.8 (storage full / missing input); tritanopia 27.5 (paused / inactive) |
| Blind naming of the S3 winner | 9/9 in grayscale, deuteranopia, protanopia, tritanopia, zero confusions (`b23r2`, one independent session); reviewer hard pairs: storage full vs missing input, inactive vs paused (separated by glyph and brightness) |

### 8.3 Salience rule

Salience is carried by the badge, not by the body. Problem states (missing input, storage full, waiting) get a saturated coloured badge (warm, or cool for waiting) whose glyph names the problem; benign states (paused, inactive) get a neutral white or grey badge; running gets none. Measured proof of speed: SC-002 round 2, 10/10 within 2.0 s, median 1.077 s, with per-state results missing input 4/4, storage full 3/3, waiting 3/3 (round 1 had `waiting` weakest, which T460 corrected). Owner decision S3 overrides the body-variant clause of R06 for v1.0.

### 8.4 Cue sizes and label threshold

| Item | Rule | Basis |
|---|---|---|
| Badge | 23 x 23 art px on every screen class; at least 24 pt at `n_min` except DEV-1 and DEV-2 (section 4.3) | owner decision S3; measured |
| Marker | 11 x 11 art px (section 3.2); named deviation DEV-3 below 24 pt (section 4.3) | derived |
| Label threshold (research R06) | a text label in the world is shown only when its cap height is at least 12 pt at the current scale: `cap_px * n / pxPerPoint >= 12`. The machine panel always names the state in text | contract |
| Locked-plate digit | authored 3 x 5 bitmap font at **scale 3** (a 9 x 15 digit, **cap 15 art px**; owner decision R4), a separate overlay label (`F004-LABEL-LOCK-L2` to `-L5`), not baked into the plate. Cap height at `n_min`: 22.46 pt (320 dpi phones), 22.26 pt (portrait tablet), 17.10 pt (emulator), 16.07 pt (tablet landscape), 15.62 pt (460 dpi phones), 14.97 pt (320 dpi phones at n = 2, 1280 x 720 and from v1.0.4 720 x 1280): at least the 12 pt threshold on every class, so the digit is shown at `n_min` and above (14.97 pt on the smallest class, named deviation DEV-4, section 4.3). It fits the 70 x 35 plate diamond | derived. `ui_text` on `dim_0` / `dim_1` is 11.55 / 7.49. The slot panel (opened by tapping a locked slot) also names the level in text; T429 QA verifies that at every screen class; T429 implements `labelPolicy` |

### 8.5 Slot-condition convention (S2 Art Director condition 3, carry-in 5)

A bare plate is never a state. The convention, which the naming-gate sheets and in-game captures must follow:

| Slot condition | Appearance | Shape cue | Never |
|---|---|---|---|
| Empty (buildable) | open plate with corner marks (S3 sets named it 9/9) | corner marks | shown with a machine or a padlock |
| Locked (level n) | plate with a dark hatch field and padlock; the level digit is the overlay label (cap 15 art px) | padlock and hatch | shown without the padlock |
| Occupied | plate **with a machine body** (`F003-SLOT-OCCUPIED-BASE` is a layer under a machine) | machine silhouette | a bare occupied plate in any capture, sheet or screenshot |
| Selected | `F003-SLOT-SELECTED-FRAME` around the footprint, `ui_accent` with ink | frame | covering a badge |

The T422 sheets already follow this convention (plate-with-machine). The re-run on the shipped set is T445; its SC-001 sheets use the same rule.

### 8.6 Markers

`F003-MARKER-BOOSTED`: double up-chevron, fill `cvd_green`. `F003-MARKER-OVERDRIVE`: lightning bolt, fill `cvd_pink`. Glyph and outline `ink_0`; ink on `cvd_green` 5.42, on `cvd_pink` 6.05. Fill vs floor: `cvd_green` 3.92 and `cvd_pink` 4.38 against `floor_0`, 3.23 and 3.61 against `floor_1`. The warm and cool problem semantics of badge fills are not reused. Markers have no text; the machine panel names the effect. Markers are not part of the S3 gate evidence. They are 11 x 11, a named deviation (DEV-3, section 4.3). Marker naming, in grayscale and the three CVD filters, is **blocking**: markers join the T439 shuffled sheets and the T445 sheets, including 1280 x 720 at n = 2. `cvd_green` and `cvd_pink` have close grayscale luma (about 121 and 142), so shape alone must separate them.

## 9. Contrast and colour-vision rules

### 9.1 Declared surfaces

World: `floor_0` and `floor_1` (field), the occupied plate tops `plate_1` and `plate_2`. UI chrome: the F003 tokens `--bg` #111827, `--surface` #1F2937, `--surface-raised` #374151 (section 11). The camera backing is `ui_bg`.

### 9.2 Thresholds (FR-014)

Text and glyphs at least 4.5:1 against their own fill; non-text graphics at least 3:1 against the surface they sit on; decorative marks carry no meaning. The ratio is WCAG 2.x relative luminance (`art_pipeline.color.contrast_ratio`, the function behind `check_contrast`) at `darkest_light` 1.0 (section 2.1).

### 9.3 Contrast of outlined graphics: ink outline OR rim (owner decision R1, option (b), 2026-10-29)

The 1 px `ink_0` outline is darker than every surface (1.04 to 2.12 against the floor ramp and token surfaces), so it carries 3:1 over light backdrop tones and cannot over dark ones. The pixel just inside the outline (the **rim**) carries it over dark tones. No tone is forced on the rim (section 6.1 stands).

**Rule.** A graphic passes FR-014 against a backdrop tone T when its ink outline OR its rim reaches 3:1 against T, and it must pass against every tone T it can sit on (its allowed backdrop set).

**Measurement (composite-mask percentage). The threshold is defined by this exact counting method.**

1. Neighbourhood: 4-neighbour (up, down, left, right); no diagonal pairs. The outline is the set of opaque body pixels that have at least one 4-neighbour that is transparent in the body or outside the canvas. The rim of an outline pixel is its first inward 4-neighbour that is opaque and not itself an outline pixel (in the order right, left, down, up); every spike Miner outline pixel has one.
2. Pairs: one pair per (outline pixel, outward 4-neighbour). An outline pixel with two outward neighbours (a corner) gives two pairs; all pairs count, none is merged.
3. Backdrop substitution: the backdrop pixel is the pixel of the plate sprite at the same canvas position (`s2-slot-occupied`; `s3-plate-occupied` gives identical numbers). Where the plate pixel is opaque its palette tone is the backdrop tone T; where it is transparent (or outside the canvas) the pair is bare floor and is evaluated twice, once as `floor_0` and once as `floor_1`.
4. For each tone T: `share_T` = pairs where `max(ratio(ink_0, T), ratio(rim tone, T))` is at least 3, divided by all pairs with backdrop T. The outline pixel itself always has the tone `ink_0` (so the ink term is `ratio(ink_0, T)`).
5. **Threshold: `share_T` at least 90 % for every T.** (rule; the 90 % lets a few tone-boundary pixels fail; a figure below it fails the check.)

The check is run by the producer on the real composite (T434 for bodies, T437 for overlays) and is built by T462 (section 19, O-4); T439 reviews it.

**Re-measurement of the spike Miner** (`s3-lightB-body-run` over `s2-slot-occupied`, run for this revision with `art_pipeline.color.contrast_ratio`; 104 outline pixels, all `ink_0`):

| Backdrop tone | Pairs | Ink carries 3:1 | Rim carries 3:1 | Either (the rule) | Result at 90 % |
|---|---|---|---|---|---|
| `plate_1` | 68 | 100 % | 0 % | **100 %** | pass |
| `plate_2` | 3 | 100 % | 0 % | **100 %** | pass |
| `ink_0` (plate outline) | 2 | 0 % | 100 % | **100 %** | pass |
| `dim_1` (plate detail) | 1 | 0 % | 0 % | **0 %** | one pair, below the threshold |
| `floor_0` (bare floor, 56 pairs from 48 outline pixels) | 56 | 0 % | 62 % (35) | **62 %** | **fail** |
| `floor_1` (the same 56 bare-floor pairs plus 2 plate pixels of tone `floor_1`) | 58 | 0 % | 60 % (35) | **60 %** | **fail** |

Reading: the spike Miner meets the rule over the plate tones, which is where it sits, and fails it where its boundary borders bare floor (62 % and 60 % against 90 %). The 35 passing floor pairs have a `steel_2` or `copper_2` rim; the failing ones have a `steel_1` rim. Counting outline pixels instead of pairs (48 pixels with a bare-floor neighbour, 40 with one and 8 with two) gives 33 of 48 = 69 % for both floor tones. The independent QA of T461 counted 29 and 31 bare-floor pairs and 28 % and 26 %; that count was not reproduced from this method, and it also falls far below 90 %. Every counting variant therefore gives the same conclusion (fail over bare floor, 26 % to 69 %), and the figures that reproduce exactly from the method above are the ones in the table. The rule is therefore not satisfied by the spike body as drawn. It is a **T434 requirement**: a production body keeps its boundary pixels over the plate, or its floor-facing rim reaches the threshold. The result does not change any S3 or S4 decision (those bodies were read on plates in the sheets).

### 9.4 Contrast run now (carry-in 6)

Run on the committed pipeline code: `check_contrast` (pairs of palette ids, full light and `darkest_light` 1.0) on 53 pairs, plus `contrast_ratio` on the token surfaces. The spike set had no declared surfaces; this is the first run (`reviews/style-spike.md` gate 4, "not run (needs the Art Bible surfaces, T424)").

**Run R1, palette surfaces: 53 pairs, 39 pass, 14 fail.**

| Pair group | Result | Disposition |
|---|---|---|
| Glyph `ink_0` on each badge fill (text, 4.5) | pass: `cue_warn` 9.81, `cue_alert` 5.17, `cue_info` 9.48, `cue_neutral` 13.62, `cue_dim` 6.01 | none; badge glyphs use `ink_0` only |
| Badge fill vs `floor_0` / `floor_1` (non-text, 3) | pass: `cue_warn` 7.10 / 5.84, `cue_alert` 3.74 / 3.08, `cue_info` 6.85 / 5.64, `cue_neutral` 9.86 / 8.12, `cue_dim` 4.35 / 3.58 | rule C1: the allowed backdrop tone set below. Against `floor_2` `cue_alert` is 2.44 and `cue_dim` 2.84 (fail), so those tones never lie behind a badge |
| `ink_0` vs `plate_1` / `plate_2` (body outline on plate) | pass 4.14 / 6.50 | the ink outline carries a body over `plate_1` / `plate_2` (rule C5) |
| `plate_2` vs `floor_0` / `floor_1` | pass 4.70 / 3.87 | rule C2: the outer rim of every plate is `plate_2` |
| `ui_accent` (selection frame) vs `floor_0` / `floor_1` | pass 5.12 / 4.21 | rule C3: the allowed backdrop tone set below |
| `ui_accent` vs `ui_bg` / `ui_panel` | pass 6.28 / 4.82 | none |
| `ui_line` vs `ui_bg` | pass 3.03 (marginal) | rule C4 |
| `ui_text` (glyph base) on `ui_bg` / `ui_panel` (text, 4.5) | pass 15.06 / 11.55 | none. The locked-plate digit in `ui_text` on `dim_0` / `dim_1` is 11.55 / 7.49 (run R2, pass) |
| `steel_2`, `steel_3`, `copper_2`, `teal_1` vs `floor_0` / `floor_1` (body rim tones) | pass: 4.35 / 3.58, 7.89 / 6.50, 4.75 / 3.91, 6.87 / 5.66 | these rim tones carry a body over floor; rule C5 requires the composite share, not a forced rim |
| `plate_1` vs `floor_0` / `floor_1` | **fail** 2.995 / 2.466 | rule C2 (the plate rim is `plate_2`, not `plate_1`) |
| `ui_accent` vs `plate_1` / `plate_2` | **fail** 1.71 / 1.09 | rule C3 |
| `ui_line` vs `ui_panel` | **fail** 2.32 | rule C4: `ui_line` only on `ui_bg`; on `ui_panel` use `ui_text` or `ui_accent` |
| `res_ore_0`, `res_coal`, `res_plank` vs `ui_bg` / `ui_panel` (`res_ore_1` 5.28 / 4.05 and `res_gold` 8.24 / 6.32 pass) | **fail**: `res_ore_0` 2.63 / 2.02, `res_coal` 1.48 / 1.14, `res_plank` vs `ui_panel` 2.91 (vs `ui_bg` 3.79 passes) | rule C6 |
| `ink_0` vs `ui_bg` / `ui_panel` / `floor_0` / `floor_1` | **fail** 1.13 / 1.47 / 1.38 / 1.68 | by section 9.3 (the outline carries only over light tones; the rim or the composite share carries the rest) |

**Run R2, token surfaces (`contrast_ratio`):**

| Graphic | vs `--bg` | vs `--surface` | vs `--surface-raised` |
|---|---|---|---|
| `ui_text` (UI glyph base, before tint) | 16.23 | 13.43 | 9.43 |
| `ui_accent` | 6.77 | 5.60 | 3.93 |
| `res_ore_1` | 5.69 | 4.71 | 3.30 |
| `res_gold` | 8.89 | 7.35 | 5.16 |
| `steel_2` | 5.75 | 4.76 | 3.34 |
| `steel_3` | 10.44 | 8.64 | 6.07 |
| `res_ore_0` (fail) | 2.83 | 2.35 | 1.65 |
| `res_coal` (fail) | 1.60 | 1.32 | 1.08 |
| `res_plank` (fail on raised) | 4.09 | 3.38 | 2.37 |
| `ink_0` (separator) | 1.04 | 1.26 | 1.80 |

Rules from the run:

- **C1** Badge and marker backdrop. The backdrop tone under every badge and marker box must lie in the allowed set `A_ov`: `ink_0`, `ink_1`, `steel_1`, `steel_2`, `steel_3`, `copper_1`, `copper_2`, `teal_1`, `dim_0`, `lit_0`, `lit_1`, `floor_0`, `floor_1`, `plate_1`, `plate_2`, `res_ore_1`, `res_coal`, `res_crystal`, `res_leaf`, `res_gold`, `res_water`, `res_plank`, `ui_bg`, `ui_panel`, `ui_line`, `ui_text`. Forbidden behind a badge or marker: `steel_0`, `copper_0`, `teal_0`, `dim_1`, `warn_0`, `floor_2`, `plate_0`, `res_ore_0`. Derivation (measured now, `art_pipeline.color`): a tone T is allowed when, for all five badge fills and both marker fills, `max(ratio(ink_0, T), ratio(fill, T))` is at least 3; the forbidden eight are the tones where at least one of those seven fails (for example `cue_alert` fails on `steel_0`, `copper_0`, `teal_0`, `dim_1`, `warn_0`, `floor_2`, `plate_0`, `res_ore_0`). This replaces the earlier "floor only" wording, which the T425 review showed to be unsatisfiable on the lattice (the badge box reaches 1.0 cell above the footprint top, over the plate of the neighbouring slot).
- **C2** Plate outer rim = `plate_2`.
- **C3** Selection frame backdrop. The backdrop under the frame must lie in the allowed set `A_fr`: every palette tone except `warn_0`, `plate_0` and `res_ore_0` (measured now: `ui_accent` with `ink_0` carries 3:1 on every other tone).
- **C4** `ui_line` is used only on `ui_bg`; sprites on `--surface` or `--surface-raised` use `ui_text` or `ui_accent`. `ui_panel` is used by no sampled spike sprite and `ui_line` only in plate and frame details; C4 stays as a guard for UI-surface use.
- **C5** Machine body contrast: option (b) of section 9.3 with the allowed backdrop set for bodies `{plate_1, plate_2, ink_0, floor_0, floor_1}` and the 90 % threshold per tone. No rim tone is mandated. The earlier rim list (`steel_2`, `steel_3`, `copper_2`, `teal_1` over floor) is withdrawn as a rule; it remains the measured evidence of tones that reach 3:1 over floor (table above).
- **C6** Resource, Bar, Plate, Gear and item pictograms use `res_ore_1`, `res_gold`, `steel_2` or `steel_3` as rim tone on UI surfaces, and item pictograms may also use `ui_text` and `res_crystal` as rim tone (OD-11); `res_ore_0`, `res_coal` and `res_plank` are interior tones.
- **C7** The check is run by each producer (T434 to T438) on the real sprite against the real declared surface and committed in `check-report.json`; T439 reviews contrast and crispness independently.
- **C8** Numeric backdrop acceptance (T456 for the lattice, T429 for the view; T425 finding R2). In the densest 12-slot frame (12 occupied slots, each with its cue or accent, both markers, one selection frame) at `n_min` of every screen class, render the world layers without the overlay layer, take the 1 art px ring of backdrop pixels just outside the opaque mask of every badge, marker and selection frame, and look up each ring pixel's palette tone (art-pixel resolution, no scaling). **100 % of ring pixels must lie in `A_ov` (badges, markers) or `A_fr` (frame)**; a single forbidden tone fails the acceptance. Ring pixels covered by another overlay are excluded.
- **C9** Lock-label backdrop (OD-9). The backdrop under the digit of `F004-LABEL-LOCK-L2` to `-L5` lies in the set `A_ov` plus `dim_1`. `ui_text` on `dim_1` is 7.49 and on `dim_0` 11.55, both above 4.5 (run R2). C1 is unchanged: `dim_1` stays forbidden behind a badge or marker.

The run uses palette-level colours. The composite-mask measurement of section 9.3 is defined and was run once, by hand, on the spike Miner; the pipeline check that automates it does not exist yet and is not claimed (section 19, O-4).

### 9.5 Grayscale and colour-vision checks (SC-001)

Method (research R17): in-game captures at `n_min` and fit-all, shuffled with a recorded seed, rendered in grayscale (Rec. 709 luma) and protanopia, deuteranopia, tritanopia (Machado, severity 1.0), named by an independent agent session with the key withheld; the owner confirms visually. Pass: zero confusions on every sheet, with slot conditions following section 8.5. Two reviewers are not required; one independent session is the recorded method. These are screening checks, not clinical validation. Spike evidence: section 8.2.

SC-002: 10 randomised 12-slot screenshots with one stuck machine, at least 9 found within 2.0 s; round 2 on the winner passed 10/10 (`style-spike.md`).

## 10. Crisp rendering and shimmer (FR-016)

Definitions (T425 finding R11). The **world art grid** is the grid of art pixels in world space (one cell of `1 / D` world unit); every position is measured relative to it, never as an absolute screen position (a one-device-pixel pan moves every edge on screen). A **run** is a maximal horizontal or vertical sequence of equal colour inside the world area of the R13 capture (HUD and UI excluded). The test is on run lengths: **every run length is a multiple of `n`**. A flat area of width `k` art pixels gives a run of `k * n`, so "gcd of the runs equals `n`" is not required (it fails on flat areas where every run is `2n`). The test is repeated for an **odd-dimension viewport** (odd pixel width and height), which needs the half-device-pixel camera offset of `CameraScaleModel` (wiki `pixel-camera-scale`). The pivot assertion is float-tolerant (`|pivotPx - round(pivotPx)| < 1e-3`, section 3.3).

| Check | Expectation | Evidence so far | Owner |
|---|---|---|---|
| Horizontal runs | every horizontal run is a multiple of `n` (100 %), at every allowed `n`, including the odd-dimension viewport | measured: D24 100 % in 16 files (S2 gate 6) | T427 `PixelCrispnessTests` |
| Vertical edges on the grid | every vertical run is a multiple of `n` (100 %), at every `n`, anchored to the world art grid; the "vertical edges on the grid" figures of the spike are the same statement | measured: D24 96.8 to 97.5 %, the whole shortfall from ore and ingot y = 4.5; fixed by rule P1 and the 20 x 10 footprint, not yet re-measured (no Unity in T424) | T427 (R13 capture), reviewed by T425 |
| Shimmer under pan | in every frame of a scripted slow pan (steps of one device pixel, at each allowed `n`), every run is a multiple of `n` in every frame, and no frame differs in block width from the at-rest frame | not measured so far ("shimmer under pan is not measured", S1 and S2) | T427 `PixelCrispnessTests` (also `[Category("StandalonePlayer")]`) |
| Fallback frames | ~~F003 placeholders are exempt (texel ratio `n*D/64` is not an integer); never shipped~~ superseded (errata v1.0.4): the F003 placeholders were deleted by T476 (#670, owner decision 2026-11-05 on T447 option D); FR-024 fails closed, there are no fallback frames | contract | T428 |

The condition "re-run the crisp check expecting 100 %" cannot be run in T424: it needs Unity captures. The expectation and its owner are defined here.

## 11. UI and font rules

- **UI chrome (kept from `F003-PAB` section 9)**: UI Toolkit tokens `--bg`, `--surface`, `--surface-raised`, `--text-primary`, `--text-strong`, `--text-secondary`, `--text-muted`, `--accent`, `--on-accent`, `--positive`, `--warning`, `--danger`, `--border`, the component tokens (buttons at least 44 x 44 pt, focus ring, selected card), the type scale (body 14 pt, caption 12 pt, HUD value 16 pt, no text below 12 pt, scaling to 200 % without clipping) and the disabled and rejected look are unchanged. F004 restyles icons (FR-003, US5), not the chrome. Observation for T425 and T426: the token hexes and palette `ui` hexes are different but in the same slate family; a re-map is out of scope and needs an owner decision. **Background seam (T425 finding R15):** the camera clears to `ui_bg` #1b1f2a while the HUD `--bg` is #111827; the two differ by 1.08:1, a faint seam at the edge of the world area. It is accepted in v1.0; changing the clear colour to #111827 is a camera setting, not a palette change, and would be an owner decision taken at T426 or T433 and reviewed in T441 (the contrast pairs that use `ui_bg` as a backdrop are re-run if it changes).
- **UI glyph icons**: 24 x 24 art px, displayed at 24 pt (1 pt per art px), one colour (`ui_text`), tinted by tokens (enabled `--text-primary`, disabled `--text-muted`, selected or accent `--accent`), point filtered, no mipmaps, by key, excluded from the world crispness rule (UI Toolkit scales by a non-integer factor; research R10). Strokes at least 2 art px; at most 6 primitives. A symmetric array of one repeated element counts as one primitive (for example the eight teeth of a gear icon; OD-7). Multiplicative tint of `ui_text` (#f2f5fa) loses about 5 % of white; contrast is evaluated after tint.
- **Resource sprites** are shown in lists at 32 x 24 pt; item icons at 32 x 32 pt; tier chips at 32 x 16 pt (1 pt per art px). Bounds at 200 % text are verified by T441.
- **Tier cue** stays triple redundant: frame shape (circle, square, triangle), pip count (1, 2, 3) and numeral (`I`, `II`, `III`); the tier colour is supplemental.
- **Font**: UI text uses the project default UI Toolkit font; no external font. Text inside art is limited to the authored 3 x 5 bitmap digits and numerals (`tools/art-pipeline` `cues.DIGITS`, licence authored). A bitmap numeral `I` is added by T437.
- **No icon implies a port, belt, cable or power state** (`F003-PAB` section 7 and the F003 brief rule are kept).

## 12. Family-route table (FR-021a, research R08)

| Family | Route | Reason |
|---|---|---|
| Machine bodies and run loops | `blender` (rig-v2) | one projection and key light across 6 frames; coherent loops; measured IoU and reproducibility at D24 |
| Slot plates, selection frame, floor tile | `blender` for plates and floor; `scripted` for the selection frame | plates share the camera with machines; the frame is exact |
| Lighting accents (`F004-ACCENT-*`) | `scripted` from the body frames | deterministic, palette-exact (spike `accent_layer`) |
| State cues (badges), markers, locked-plate digits | `scripted` (`cues`) | exact shapes, CVD rule, byte-identical output |
| Resources and item pictograms | `comfy` (Z-Image-Turbo, pixel-art LoRA) followed by the pixel pass; for items the tier frame, pips and numeral are added by a scripted step | small illustrative objects; no frame coherence needed; blocked until the first reviewed workflow output; style drift guarded by section 13 |
| UI glyph icons | `scripted` | simple shapes, one colour, tinted; no row is illustrative at 24 px |

No family changes route in v1.0 (no spike outcome required it). A route change needs a brief revision and a recorded reason.

## 13. Style-anchor sheet and cohesion

The anchor sheet is a 1x contact sheet showing, on `floor_0`, the four machines (frame 0), plates in the four conditions (empty, locked, occupied-with-machine, selected), the four resources, the five badges and two markers, and one floor tile, generated by `art_pipeline.checks.make_review_sheets` from approved outputs. Resources appear only in UI lists, so the sheet places the four resources on UI surfaces, not on `floor_0`; the resource floor and plate outline checks stay reported but not gated (OD-12). Until the production anchor exists, the interim anchor is the S3/S4 set `s3b-comp-*-23` under `art-source/f004/spike/s3-s4/` plus the S2 resources and plates. The sheet is composed after the first reviewed outputs of T434 to T437 and is the reference of the cross-family cohesion review (T439, plan "Review waves"). Ramp and outline rules of section 6 are shared by all routes; a family that drifts from the sheet at 1x is rejected back to its producer.

## 14. Tool pins

| Tool | Pin | Source |
|---|---|---|
| Blender | 5.2.1 LTS (build 2026-08-25, hash `9e2066aef7ef`), no add-ons in batch; Blender MCP bridge 1.0.3 only for interactive modelling | `docs/integrations/mcp-readiness.md` |
| Rig | `tools/art-pipeline/blender/rig-v2.blend`, sha256 `9075ca24da793f8b2d04e8d4642aefc139cfad3329dc1085528b2947dc412098`; Cycles CPU, seed 0, 16 samples, no denoiser. `rig-v1.blend` (sha256 `7b2f77fc6b869450bd02256323e6ee1681a391f0e70ef4e07298e984b3f5e32b`) rendered ground edges at 2.236:1 and is kept for provenance only | same; measured now |
| Python | `>=3.12,<3.14` (`pyproject.toml`); spike runs used CPython 3.13.6 | repo |
| Pillow | 11.3.0 | `tools/art-pipeline/pyproject.toml` |
| pytest | 8.4.2 | same |
| `uv` | 0.8.22 | mcp-readiness |
| ComfyUI core | **v0.43.0**, the version validated live (T404, `models.json`); `contracts/art-pipeline.md`, `plan.md` and `research.md` named v0.38.0 until T461 and now name v0.43.0 (R-4, closed) | wiki `f004-batch-1-art-tooling` |
| Comfy Desktop, comfy-mcp, comfy-cli | 1.0.47, 0.10.0, 1.22.0 | mcp-readiness |
| Z-Image-Turbo | files `z_image_turbo_bf16.safetensors`, `qwen_3_4b.safetensors`, `ae.safetensors`; Apache-2.0 | `tools/art-pipeline/comfy/z-image-turbo.json` |
| LoRA | `pixel_art_style_z_image_turbo` revision `0a5092d`, Apache-2.0, sha256 `09b1b45ceed0202929bca528e51b50208d0160f4e9d2ba0f42cb7e739a43577f`, trigger "Pixel art style." | `tools/art-pipeline/models.json` |
| Palette | `F004-palette` 1.0.0 | section 5 |

No silent substitution: a different tool or version, or any other generator, needs a brief revision and independent Art Director approval (ART-6 of F003, kept).

## 15. Provenance

- One `provenance.json` per asset revision with the fields of `contracts/art-pipeline.md` and the data model: `assetId`, `revision`, `route`, sources and hashes, tools and versions (model and LoRA with hash and licence), parameters (render script commit, rig version, prompt, negative prompt, seed, sampler, steps, workflow hash), `touchUp` (scripted steps only), `pixelPass`, outputs and hashes, `license`, `trainingDataNote`, `producedBy`, `reviewedBy` (role, session, date; independent), `candidateSelection`, `rigVersion`, `renderEnv`, `comfyNodeVersions`, `checkReportHash`, `artBibleVersion` = `F004-PAB v1.0`, `briefVersion` = `F004-AB v1.0`.
- A shipped asset requires a complete record (SC-004); the pipeline refuses a manifest entry otherwise.
- Generated assets ship only with licences that permit commercial use; the LoRA model card names no training data and the record states that as an accepted risk (FR-021).
- Ownership: repository owner (`fjure`); the repository has no root licence file, so no public licence grant is claimed.
- The spike sprites carry `reviewedBy: null` and `artBibleVersion` "none yet". They are not production assets and are never promoted without the T426 decision on the spike import path (`Assets/IdleFactory/Art/F004/Spike/`).
- Models and LoRAs are downloaded only with owner approval, through local ComfyUI, never into the repository.

## 16. Versioning (semver, research R18)

| Change | Bump |
|---|---|
| Projection, density, palette removal, hue change, any growth beyond the 48 cap, a cap change | **major**, with a migration decision |
| Additive colours inside the cap, new families or states, adopting the L2 Light2D envelope, a new marker | **minor** |
| Wording, corrected arithmetic, added checks | **patch** |

Frozen at v1.0: projection B, D = 24, the 48 colours of palette v1.0.0, the S3 = b cue model, lighting B level L1. The palette version equals the Art Bible major.

## 17. Supersession table for `F003-PAB v0.1` (section level)

| F003-PAB section | Disposition | F004-PAB section |
|---|---|---|
| Header, scope, inherited conventions (F001: top-down, 64 PPU, `T1`, `P1`) | **Superseded** (isometric 2:1, D = 24, raster pipeline) | 0, 2, 3, 14 |
| 1 Principles: 1 (read at a glance, colour last) | **Kept** | 1 |
| 1 Principles: 2 (no gradient, texture, lighting, perspective) | **Superseded** (isometric projection, lighting B; no gradient or dithering stays) | 1, 2, 6 |
| 1 Principles: 3 (every number measurable) | **Kept** | 1 |
| 2 View and pixel system: top-down, 64 px cell, 128 px slot, 160 px pitch, SVG source, PNG import | **Superseded** | 2, 3, 12 |
| 2 Slot unlock order (slots 5 to 12 at levels 2 to 5), 12-slot grid 4 x 3 or 3 x 4 | **Kept** as game rules; lattice geometry superseded | 3.5 (T456) |
| 2 Slot anatomy (header band), anchors and pivots (bottom-left (0,0), centre pivots) | **Superseded** (overlay band, whole-pixel footprint pivots) | 3.2 to 3.4 |
| 2 Sorting order (10, 100, 150, 300) | **Amended**: row-derived sorting groups, overlay layer above all machine art (R04) | 3.4, 3.5 |
| 3.1 Palette roles (F003 hex values) | **Superseded** by palette v1 for art; the UI token roles are **kept** | 5, 11 |
| 3.2 State, marker and item colours (badge fills, bronze/silver/gold) | **Superseded** (`state_cues` and `cvd_safe` groups); tier colour stays supplemental | 5, 8, 11 |
| 3.3 Machine, resource and slot colours | **Superseded** | 5, 6.2 |
| 4 Shape language, outline widths, silhouettes and IoU table | **Amended**: concepts kept, outline 1 art px, IoU at most 0.70 on bodies only | 6, 7 |
| 5 Slot family | **Amended**: four conditions kept, occupied never bare, locked digit becomes a label | 8.5 |
| 6.1 Caps (one badge, two markers, 61 sprites) | **Kept** (61 holds, derived) | 2.2, 6.5 |
| 6.2 Badge precedence | **Kept** (state model carried over); glyph column and text cues **superseded** | 8.1 |
| 6.3 Markers | **Amended**: shapes kept, colours and size changed | 8.6 |
| 6.4 Slot condition cues | **Amended** | 8.5 |
| 6.5 Badge placement (header band) | **Superseded** | 3.2, 3.4 |
| 7 Resource, item and UI icon families (sizes, 48 px, SVG, tier cue) | **Superseded** for size and route; the triple tier cue and the white tinted UI glyph rule **kept** | 11, 12 |
| 8.1 Zoom steps Z16 to Z64 | **Superseded** by whole-number scales (FR-012) | 4 |
| 8.2 Minimum sizes, the 12-slot rule, fit-all at least 24 ppc | **Superseded**; the invariant (all 12 slots at fit-all) **kept** as `n_fit >= n_min` | 4 |
| 8.3 Contrast rules (4.5:1, 3:1) | **Kept** with the outline interpretation | 9.2, 9.3 |
| 8.4 Grayscale and CVD checks | **Kept**, method moved to in-game captures and an independent agent | 8.2, 9.5 |
| 8.5 Numeric noise limits | **Amended** | 6.5 |
| 9 UI tokens (9.1 to 9.5) | **Kept** unchanged for chrome; icons restyled | 11 |
| 10 Tooling and provenance (Inkscape, `P1`) | **Superseded** by Blender, Comfy, scripted routes | 12, 14, 15 |
| 11 AD-1 (slot 2 x 2 cells) | **Kept** (a slot is 2 x 2 cells; at D24 a cell is 24 art px) | 3 |
| 11 AD-2 (header band) | **Superseded** | 3.4 |
| 11 AD-3 (paused vs waiting mapping) | **Kept** | 8.1 |
| 11 AD-4 to AD-5 (key ids) | **Kept**: no key is renamed; additive `F004-` keys only | brief section 3 |
| 11 AD-6 (monochrome tinted UI icons) | **Kept** | 11 |
| 11 AD-7 (tier triple cue) | **Kept** | 11 |
| 11 AD-8 (icons chosen by effect kind, resource and control state) | **Kept** | brief section 5 |
| 11 AD-9 (labels as vector paths of at most four letters) | **Superseded** (no badge text; bitmap digits) | 8.4 |
| 11 AD-10 (fit-all floor 24 ppc) | **Superseded** | 4 |
| 12 Traceability to ART-1 to ART-6 | **Kept** as history; F004 traceability in section 18 | 18 |
| 13 Review and freeze (T091, T129) | **Superseded** by T425 and T426 | 18 |

## 18. Traceability, review and freeze

| Requirement | Where addressed |
|---|---|
| FR-003 (full set, no placeholder) | brief sections 2 to 5 |
| FR-009 (supersession, projection, light, D, canvas and pivot, palette with licence, outline, shading, dithering, animation, state-cue model, contrast and CVD, noise, UI and font, tool pins, provenance) | sections 2, 3, 5, 6, 8, 9, 11, 14, 15, 17 |
| FR-021a (route per family, reason) | section 12, brief |
| FR-022 (brief, production, independent review, Technical Art, validation) | brief section 7; section 13 |
| Research R18 (supersession table, semver, second Art Director session) | sections 16, 17; this section |

1. **T425**: independent review by a second Art Director session, UI/UX Reviewer and Game Designer/PO (read-only). It reviews contrast and crispness rules (section 9, 10) and the new unmeasured rules flagged `rule`.
2. **T426** (parent): owner sign-off of v1.0 and the briefs; spike import path deleted or promoted by recorded decision; the machine-readable `palette-v1.json` is produced at the freeze.
3. A style change after sign-off is a new version with a migration decision.

## 19. Open items, named owners, risks

| ID | Item | Owner |
|---|---|---|
| O-1 | Light2D (L2) render-cost measurement and the lights-off control | T444 (T442 for allocations) |
| O-2 | HUD-aware proof of the `n_min` tolerance; DEV-1, DEV-2 and DEV-4 figures on the shipped build | T427, T442 |
| O-3 | Crisp 100 % and shimmer under pan on production sprites | T427 (`PixelCrispnessTests`), T425 review |
| O-4 | Tooling (the "Art-pipeline v1 checks" task proposed by the T425 review, R9): `check_pivot` integral-pivot assertion with the 1e-3 tolerance, id-level palette allowance, overlay keep-out, composite-mask contrast (section 9.3), accent subset of body (L-4), the `intentionallyEmpty` manifest flag, and `palette-v1.json` | **T462** (#616, "Art-pipeline v1 checks", after T426 and before T434 to T438) |
| O-5 | Lattice, overlap, row sorting, hit testing, badge band vs other slots | T456 |
| O-6 | Marker layout, `labelPolicy`, `intentionallyEmpty` running badge, densest-overlay frame, the C8 backdrop acceptance, loop resume, slot panel naming the lock level at every class (R4), the test that every non-running `BadgeFor` result renders a non-empty badge (R6) | T429 |
| O-7 | Re-run of SC-001 and SC-002 on the shipped build; marker and CVD naming blocking, including 1280 x 720 at n = 2 (DEV-3); an SC-002 variant with reduced motion (R6) | T445 (markers also T439) |
| R-1 | Smelter and Press are undrawn; their IoU against the other bodies is unmeasured | T434 |
| R-2 | Badge, marker and frame contrast depend on the T456 lattice keeping forbidden backdrop tones out of every overlay box (C1, C3, C8) | T456, T429, T439 |
| R-3 | Closed by the owner decision of 2026-11-04: the APK cap is 150 MB file size, replacing the +10 MB budget whose margin was 1.01 MB; T444 still records a clean-build size | T444 |
| R-4 | ComfyUI pin: closed by T461 (v0.43.0 in the three spec files) | none |
| R-5 | The 5 colours of `cvd_safe` and 4 of `resources`, plus `cue_ok` and `warn_0`, are unused in v1.0 art; removal is major | none; headroom noted |
| R-6 | Physical-device captures do not exist; the emulator is the only device path | T444 states the gap |

Conditions of the T425 review that belong to other tasks (referenced, not resolved here): R6 reduced-motion SC-002 variant and the non-empty-badge test (T445, T429); R9 the "Art-pipeline v1 checks" task, now T462 (#616, after T426 and before T434 to T438); R10 `tasks.md` text drift and the ledger refresh (parent); R13 T441 acceptance additions (tinted-contrast table, confusable icon-pair naming, 2 px stroke capture at 420 dpi, USS spinner stop under reduced motion); R14 the APK delta checkpoint after the first imported family (T440).

## 20. Revision log

| Revision | Task | Change |
|---|---|---|
| 1.0 draft | T424 (#536) | first draft, merged in PR #613 |
| 1.0 draft, revision 1 | T461 (#615) | revised for the T425 review (`reviews/art-bible-review.md`) under the owner decisions of 2026-10-29: R1 option (b) ink-or-rim contrast and composite-mask threshold (sections 2, 6.1, 9.3, C5), the spike Miner re-measured; R2 backdrop tone sets and the C8 acceptance (C1, C3); R3 footprint box, plate diamond and hit shape (3.2a); R4 lock digit at scale 3 (8.4); R5 lamp, lit-pixel and accent mask rules L-1 to L-5 (2.1); R7 DEV-3 and blocking marker naming, marker rows moved off the plate apex (3.2, 4.3, 8.6); R8 `intentionallyEmpty` running badge (8.1); R11 crispness definitions (3.3, 10); R12 ComfyUI v0.43.0 in the three spec files (14); R15 background seam, C4 note, Assembler flash headroom, loop resume (6.4, 9.4, 11). Version stays v1.0; status stays draft pending T426. |
| 1.0 | T426 (#538) | owner sign-off 2026-10-30 (T426, #538) of the reviewed content at commit af0ee63; status draft to approved; no rule changed. Spike import path `Assets/IdleFactory/Art/F004/Spike/`: deleted by T440 after the production import (owner decision). |
| 1.0 draft, revision 1a | T461 follow-up | QA warnings of T461: the composite-mask counting method is stated exactly in 9.3 and the bare-floor figures are given under pair and pixel counting; DEV-4 (digit 14.97 pt) named in 4.3; the tooling owner is T462 (#616) in 19. |
| 1.0.1 | T467 (#640) | patch, 2026-11-03, owner decisions on the T439 review (#553, `reviews/art-direction-review.md` section 6); precisions only, no number, palette, threshold or style change. OD-1 flat-emission Lighting B wording (2, 2.1); OD-2 rig-v2 file and sha256 replace rig-v1 (2, 12, 14; rig-v1 kept for provenance, 2.236:1); OD-3 flat plates recorded (3.2a); OD-5 and OD-8 ink-outline exemptions for the floor tile and UI glyphs (6.1); OD-6 locked plate wording "dark hatch field and padlock" (8.5); OD-7 symmetric-array primitive counting rule (11); OD-9 new rule C9 for the lock-label backdrop `A_ov` plus `dim_1` (9.4); OD-11 `rim_lift` clause and `ui_text`, `res_crystal` in the C6 allowance (6.1, 9.4); OD-12 resources appear only in UI lists, section 13 corrected; wording fix in 3.2a (the 35-row plate diamond has a single widest row 46 at 70 px). |
| 1.0.2 | T456 (#592) | patch, 2026-11-04, owner decision on the T456 result (#592, PR #646): the 12 plates sit on a rectangular lattice of 70 x 35 diamonds at a pitch of 70 x 54 art px (3 x 4 portrait, 4 x 3 landscape), not a rotated isometric projection; the floor-tile period proposed as 60 x 30 becomes 70 x 54 (one number changed, section 3.5); hit shape 90 x 45 clipped to the cell. No palette, threshold or style change. Consequences: T435 floor art and the Assembler rim sample are re-measured on the shared floor phase; tablet 1536 x 2048 keeps 0.8 px slack at n = 3 (risk carried to T442). |
| 1.0.3 | T440 (#554) | patch, 2026-11-04, owner decision on the T440 review: budget 10 becomes "APK file size at most 150 MB" (clean build), replacing "+10 MB against the paired B1" (cap 47,345,615 B); R-3 closed; mirrored in `specs/004-visual-identity/reviews/performance-budgets.md` row 10. No palette, threshold or style change. |
| 1.0.4 | T442 (#560) | patch, 2026-11-05, owner decision on T442 (#560) finding F1 (`reviews/final-test-evidence.md`) and Art Director A-01 (`reviews/final-acceptance.md`): DEV-2 and DEV-4 extend to the portrait 720 x 1280 phone at 320 dpi (`n_min` 2 with the 1 % tolerance, `n_fit` 2, 22.95 pt cue, 14.97 pt digit), accepted deviation; 4.1 portrait row re-stated from the measured values (levels 5 = 2..6 with the tolerance, 4 without, the 1280 x 720 row aligned the same way; margin 32.7 %, real shell, share of the rect not covered by the extent), AR-1 and AR-2 extended, 4.3 table and DEV-2/DEV-4 text, marker and digit figures in 3.2 and 8.4; blind naming at n = 2 on both 320 dpi phones passed (T445 (#563) SC-001 22/22); tablet landscape `n_fit` recorded as measured 4 (precision). Errata (2026-11-05, T447 option D, owner decision on T476 #670): the F003 placeholders were deleted and FR-024 now fails closed, so the statements in section 0 (S2 row) and section 10 (Fallback frames) that F003 placeholders are fallback frames or exempt are superseded; no style change. The 24 pt rule, the 23 px icon set, the tolerance, palette and style do not change. |
