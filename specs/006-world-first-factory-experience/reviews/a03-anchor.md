# A-03 badge-anchor measurement (T517, #691; FR-014, FR-025; R17)

**Status**: measurement and recommendation only. The owner decides OD-12 at T518. Direction A (open deck, railing edge, open depot) per OD-9b. Art Bible `F004-PAB v1.0.4` (not edited).

**Method**: `tools/art-pipeline/art_pipeline/f006/a03_anchor.py` (pytest `tests/test_a03_anchor.py`). The F004 badge sprites (missing-input, storage-full), both markers, the selection frame, the smelter body and the plate are placed on the T505 lattice at art-pixel resolution. Densest frame: 12 occupied slots (3x4 portrait, 4x3 landscape), every slot badged (blocked and faulted alternating), both markers on every machine. Figures are the worst case over the 12 slots; both layouts give the same numbers. Raw data: [a03-anchor/measurements.json](a03-anchor/measurements.json) (includes a (dx, dy) sweep). Every count is in art pixels; all candidates keep the final 23 px box (Art Bible 4.3).

## Candidates

| Id | Anchor (bottom-centre, from the pivot) | Note |
|---|---|---|
| K0 | +19, +16 = canvas (59, 33) | baseline, the F004 S3 = b anchor |
| K1 | 0, +16 | x = 40 centred |
| K2 | +22, +12 | lower offset: 4 px lower, 3 px right, the lowest point clearing the plate diamond |
| K3 | +13, +16 | left offset: 6 px left, clears the own branch |
| K4 | +19, +16 plus a 4 px tail | pointer tail (7, 5, 3, 1 px rows, `ink_0` edge, badge fill) pointing at the own plate |
| K5 | +22, +12 plus the tail | hybrid, not in the task list; the sweep suggested it |

## Measurement table (worst slot; 0 is best unless noted)

| Measure | K0 | K1 | K2 | K3 | K4 | K5 |
|---|---|---|---|---|---|---|
| **54 px row band** (band -15..+39): px outside own band | 0 | 0 | 0 | 0 | 0 | 0 |
| Overflow of the lattice extent (C6, nothing clips) | 0 | 0 | 0 | 0 | 0 | 0 |
| **Selection band**: own selection-frame px hidden | 0 | 39 | 2 | 5 | 0 | 4 |
| Foreign selection-frame px hidden | 0 | 0 | 0 | 0 | 0 | 0 |
| **Overlap proof**: px over a foreign plate diamond | 0 | 0 | 0 | 0 | 0 | 0 |
| px inside a foreign hit shape (a tap selects the wrong machine, FR-014) | 0 | **47** | 0 | **8** | 0 | 0 |
| px over own plate diamond | 0 | 32 | 0 | 2 | 0 | 1 |
| px over own / foreign machine body | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 |
| px over own markers M1 and M2 | 0 | **169** | 0 | **49** | 0 | 0 |
| px over own branch lane (hides the link) | 0 | **21** | 0 | 0 | 0 | 0 |
| px over the row trunk (layer rule, allowed) | 115 | 115 | 113 | 115 | 115 | 113 |
| Attribution ratio (nearest foreign plate centre / own, plate metric; 1.0 = equidistant) | 1.115 | 1.158 | **1.372** | 1.126 | 1.140 | **1.392** |
| Clearance to own plate / to the machine behind (px gap; 1 = touching) | 2 / 2 | 0 / 1 | 0 / 6 | 0 / 1 | 2 / 2 (tail tip) | 0 / 6 |
| Share of badge px a tap resolves to the own machine | 3.4 % | 25.4 % | 9.7 % | 11.3 % | 5.5 % | 12.5 % |
| **C8 ring** in `A_ov` (rule C1) | 100 % | 100 % | 100 % | 100 % | 100 % | 100 % |
| Ring px on `steel_*` (all on the trunk lane) | 120 | 62 | 120 | 84 | 120 | 120 |
| Backdrop tone changes per halo px (F6 railing noise proxy; lower is calmer) | 0.105 | 0.295 | 0.201 | 0.274 | 0.129 | 0.221 |
| Palette gate on the badge sprite incl. tail | pass | pass | pass | pass | pass | pass |

**23 px legibility at `n_min`** (same for every candidate; the box is never rescaled, a tail adds 4 px below and no width): points = 23 x `n_min` / px per pt. 1080x2400 and 2400x1080 (420 dpi, n 3): 26.23 pt. Tablet portrait 1536x2048 (n 3): 34.12 pt. 640x360 @2 and 720x1280 / 1280x720 (n 2, 2.0 or 2.004 px/pt): 23.00 and 22.95 pt, below the 24 pt rule, already DEV-2 (the 640x360 @2 row is the same density class). Full table in `measurements.json` (`legibility`). The tap target is the hit diamond (90 x 45 px), 51.3 pt tall at n 3 on 420 dpi, 45 pt at n 2 on 320 dpi (C4 is proven in the engine, not here).

## Findings

1. **The baseline passes every geometric proof.** It does not leave its 54 px row band, touches no foreign plate, hit shape, frame or marker. The A-03 problem is perceptual: the badge is a tie between two machines. Its gap to its own plate and to the machine body behind it are both 2 px, and the attribution ratio is 1.115. It also sits on the row trunk (115 px).
2. **K1 (centred) fails.** It covers 169 px of the two markers and 21 px of the own branch and 39 px of the own selection frame, and 47 px of it lie in the foreign hit shape, so a tap selects the machine behind (FR-014 "capture hit areas"). It is also the noisiest on the backdrop.
3. **K3 (left) fails**: 49 px on the markers and 8 px in a foreign hit shape. Markers occupy x -16..+6, so the free side is the right side.
4. **K2 (lower) attributes best** (1.372, 6 px from the machine behind) and removes the tie, but it touches the own plate and hides 2 px of the selection frame and sits on a busier backdrop (0.20 against 0.10).
5. **K4 (tail) keeps every baseline guarantee** (all overlap numbers 0, quietest backdrop of the changed candidates, 0.129) and adds a pointer to the own plate. Its attribution ratio is still 1.14: the tail is a directional cue, not proximity. K5 combines both, at the cost of K2's plate contact (1 px, 4 px of frame).
6. **Finding for T516/T518 (C8 and Art Director condition 2)**: the ring is 100 % in `A_ov` for every candidate, but 62..120 ring px are `steel_*`, all on the trunk lane (`steel_1`). The badge band (16..39 above the pivot) always contains the trunk (27..31), so no anchor avoids it. If condition 2 ("no `steel_*`") stays literal, the trunk colour must change under the badge band (an `A_ov` tone that is not `steel_*`, for example `copper_1`, or a breakage of the trunk) or the condition must read "no `steel_0`" as in C1. This is not anchor-dependent.
7. Railing noise (F6): the proxy is the tone-change rate in a 3 px halo around the badges (table). The railing is drawn on margin cells; the halo values differ mostly by lane, plate and machine edges. Badges next to the railing (back row, outer columns) are not separated in this measure, so the F6 reduction stays a T519/T524 item for any candidate.

## Conditions of OD-9b, per candidate

| Condition | K0 | K1 | K2 | K3 | K4 | K5 |
|---|---|---|---|---|---|---|
| UI/UX C2 pointer or leader to the machine | none | none | none | none | **a tail per badge; the card leader is a separate element and can reuse the tail's shape** | as K4 |
| C4 touch 48 dp (badge is not a target; no wrong-machine tap) | pass | **fail** (47 px) | pass | **fail** (8 px) | pass | pass |
| C6 nothing clips at 200 % | pass | pass | pass | pass | pass (tail inside the extent) | pass |
| Art Director 1: palette ids only, `ink_0` outline, gate re-run | pass | pass | pass | pass | pass (tail from `ink_0` and the badge fill) | pass |
| Art Director 2: C8 ring 100 % `A_ov` / `A_fr`, no `steel_*` | A_ov pass; steel on trunk | same | same | same | same | same |
| A-only railing noise near badges | n/a | n/a | n/a | n/a | n/a | n/a |

## Candidate sheets

Per screen class at `n_min` (3, 3, 2, 3), densest frame (`-densest`) and the direction sheet mix (`-mix`: 10 machines, one blocked, one faulted, one empty, one locked), direction A, all candidates side by side, the selected machine as in the direction sheets. Each panel is the lattice plus its margin cell, not the full device screen (the UI chrome is not drawn; see `direction/` for that).

| Class | Densest | Mix |
|---|---|---|
| Phone portrait 1080x2400 | [densest](a03-anchor/phone-portrait-1080x2400-densest.png) | [mix](a03-anchor/phone-portrait-1080x2400-mix.png) |
| Phone landscape 2400x1080 | [densest](a03-anchor/phone-landscape-2400x1080-densest.png) | [mix](a03-anchor/phone-landscape-2400x1080-mix.png) |
| 640x360-class @2 (200 % text does not change the world scale) | [densest](a03-anchor/phone-landscape-640x360-at2-densest.png) | [mix](a03-anchor/phone-landscape-640x360-at2-mix.png) |
| Tablet 1536x2048 | [densest](a03-anchor/tablet-portrait-1536x2048-densest.png) | [mix](a03-anchor/tablet-portrait-1536x2048-mix.png) |

Detail at n = 8 of the two front-left slots: [detail-n8.png](a03-anchor/detail-n8.png).

## Recommendation

**K4, the baseline anchor (59, 33) with a 4 px pointer tail.** It is the only changed candidate with no regression on any measure, it satisfies C2's pointer language and C4, and it needs no marker, branch or frame move. If the owner judges that proximity, not a pointer, must resolve the tie, K2 is the alternative (best attribution), accepting 2 px of selection-frame cover and a busier backdrop. K1 and K3 are rejected by the measurements. The tail art is a mock here; the production tail needs a brief and the Art Director's pixel review.

The recommendation does not cover the trunk `steel_*` ring (finding 6), which the owner or T516 must settle for any candidate.

## T524 production note (#697, 2026-11-12)

OD-12 (K4) is applied. `StateCueGeometry` keeps the anchor (59, 33) and the 23 px box, and gains `BadgeTailPixels` = 4, `BadgeTailBaseWidthPixels` = 7 and `BadgeExtentBox()` (23 x 27 px, bottom edge 16 -> 12 px above the pivot, top edge and x extent unchanged). `BadgeBox()` is unchanged on purpose: the lattice export (`tools/art-pipeline/f006/lattice-geometry.json`) and the overlap tests keep their values. `StateCueSetTests` re-baseline (old -> new): anchor (0.7917, 0.6667) -> unchanged; badge extent 23 x 23 -> 23 x 27 px; badge extent bottom 16 px -> 12 px above the pivot; new assertions that the tail extent overlaps neither marker box and ends above the footprint pivot.

The production tail (P1-F10) is generated by `tools/art-pipeline/art_pipeline/f006/cue_tail.py` into `art-source/f006/cue/` with the brief [`brief-tail.md`](../../../art-source/f006/cue/brief-tail.md), the check report and the grayscale and CVD review sheets. It is pixel-identical to the T517 mock for the two badges the mock used. Pending: the Art Director pixel review (T525) and the swap of the imported sprites in `Assets/IdleFactory/Art/F004/cues/`.
