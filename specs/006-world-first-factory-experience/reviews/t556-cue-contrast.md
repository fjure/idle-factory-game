# T556 cue contrast over the LINK lane (Issue #763)

**Author**: Technical Artist worker (Art Director thresholds lens, Test/QA lens). The thresholds below are a proposal; the Art Director and the parent decide and write Art Bible 21.11.
**Gate**: `cue_over_lane_contrast` in `tools/art-pipeline/art_pipeline/f006/link_fx.py`, written to `art-source/f006/link/check-report.json` by `python -m art_pipeline.f006.link_fx --out art-source/f006` (never by hand). No sprite PNG changed; only the check report and the `provenance.json` files (source hashes and `checkReportHash`, link and fx) are regenerated.

## Method
`art_pipeline.color.contrast_ratio` on palette values: each of the eight cue fills (`cue_warn`, `cue_alert`, `cue_info`, `cue_neutral`, `cue_dim`, `cue_ok`, `cvd_green`, `cvd_pink`) and the `ink_0` outline against every lane tone (`dim_0`, `ink_1`, `ink_0`, `copper_1`, `copper_2`). Rule C1 (Art Bible 9.4) already defines the criterion: a tone carries a badge or marker when `max(ratio(ink_0, T), ratio(fill, T)) >= 3` (non-text 3:1). The gate applies it to every lane tone. Pixel areas come from the ring proof (`c8_ring_link_layer`, now also recording `laneUnderBadgeTones`: lane tones under the opaque badge masks of the densest frame, five layouts, K0 and K4).

## Ratios
| Fill against | `dim_0` | `ink_1` | `ink_0` | `copper_1` | `copper_2` |
|---|---|---|---|---|---|
| `cue_warn` | 6.68 | 7.89 | 9.81 | 2.83 | 1.49 |
| `cue_alert` | 3.52 | 4.16 | 5.17 | 1.49 | 1.27 |
| `cue_info` | 6.45 | 7.62 | 9.47 | 2.73 | 1.44 |
| `cue_neutral` | 9.28 | 10.96 | 13.62 | 3.93 | 2.07 |
| `cue_dim` | 4.10 | 4.84 | 6.01 | 1.73 | 1.09 |
| `cue_ok` | 6.81 | 8.05 | 10.00 | 2.88 | 1.52 |
| `cvd_green` | 3.69 | 4.36 | 5.42 | 1.56 | 1.21 |
| `cvd_pink` | 4.12 | 4.87 | 6.05 | 1.74 | 1.08 |
| `ink_0` outline | 1.47 | 1.24 | 1.00 | **3.47** | **6.57** |

Carrier by lane tone: over `dim_0`, `ink_1`, `ink_0` the **fill** carries (3.52 to 13.62; the outline is 1.0 to 1.47). Over `copper_1` and `copper_2` the **outline** carries (3.47, 6.57); the fill is under 3 for every cue except `cue_neutral` over `copper_1` (3.93). **No pair is without a carrier** (`pairsWithoutCarrier` is empty), so no recolour is needed.

## Thresholds and the proposed decision (accept with an area limit)
1. Every (fill, lane tone) pair needs `max(fill, outline) >= 3.0` (C1). Hard fail otherwise.
2. A pair whose fill is under 3:1 is outline-carried. It is accepted only inside an explicit pixel-area limit, per badge, on the densest frame of every layout (measured, K0 and K4 identical):

| Lane tone | Measured, badge mask (outline pixels included) | Limit | Measured, 1 px ring | Limit |
|---|---|---|---|---|
| `copper_1` | 11.0 px per badge (132 px over 12 badges on 3x4 and 4x3, 66 over 6, 220 over 20, 55 over 5) | 12 | 1.0 px per badge (12 per ring set of 12) | 2 |
| `copper_2` (active dashes) | 0 | 0 | 0 | 0 |

The alert fill over `copper_1` (1.49:1) is the worst pair; it is the outline pixels (3.47:1) that separate it, and the area is about 12 px per ring set. Accepting is proposed because the dashes are 1 px marks, the outline ring is intact (ring proof, set R) and recolouring `cue_alert` or `copper_1` would change F004 sprites and the F004 palette use (out of scope, and a Unity re-import). If the Art Director prefers a recolour, the gate then needs no area limit for that tone; this is the alternative decision.
3. `copper_2` under a badge is forbidden (limit 0): the static trunk and branch art never place the active dashes under a badge, and the gate keeps it so.

## Proposed Art Bible 21.11 wording (for the parent to apply)
> **21.11 Cue contrast over the LINK lane (T556, 2026-11-17).** Rule C1 is applied to every LINK lane tone (`dim_0`, `ink_1`, `ink_0`, `copper_1`, `copper_2`): a cue fill (`cue_warn`, `cue_alert`, `cue_info`, `cue_neutral`, `cue_dim`, `cue_ok`, `cvd_green`, `cvd_pink`) passes over a tone when the fill or its 1 px `ink_0` outline reaches 3:1 (`cue_over_lane_contrast`, `art-source/f006/link/check-report.json`). The fill carries the badge over `dim_0`, `ink_1` and `ink_0` (3.52:1 for `cue_alert` to 13.62:1); the outline carries it over `copper_1` (3.47:1) and `copper_2` (6.57:1), where every fill except `cue_neutral` over `copper_1` is under 3:1 (`cue_alert` 1.49:1). That outline-only case is accepted with an explicit area limit: at most 12 `copper_1` pixels under any badge mask (measured 11) and 2 in its ring (measured 1), and no `copper_2` pixel under a badge, on the densest frame of every layout. The gate fails above the limit, and a change to the cue art, the lane art or the palette that breaks it is re-decided here.

## Checks
`powershell -NoProfile -File scripts/test-art-pipeline.ps1`: 350 passed, exit 0. New tests: `test_cue_over_lane_luminance_contrast_t556` and `test_cue_over_lane_gate_fails_over_the_area_limit`; `test_deterministic_and_committed_output_is_current` proves the committed records equal the pipeline output.
