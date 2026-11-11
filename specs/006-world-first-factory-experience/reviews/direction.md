# F006 direction review and decision (T508, T509)

**Feature**: 006 World-First Factory Experience. **Requirements**: FR-040, FR-045; SC-012 (part 1).
**Reviewed**: origin/main `9801aeb`. Sheets in [direction/](direction/manifest.md) were made by T507 (#682, `43073f0`) from the T506 briefs and concept sprites (#681, `c0733e3`) with the T505 compositor (#680, `a0ac4d3`).
**Rules**: World Bible `F006-WB v0.1` (frozen by OD-9a, 2026-11-11) and Art Bible `F004-PAB v1.0.4`.
**Directions**:
- **A**: open platform deck. Railing edge, crate and lamp, open depot frame. Brief: [direction-a.md](../../../art-source/f006/briefs/direction-a.md).
- **B**: walled hall. Wall edge, crate and drum, closed bunker depot. Brief: [direction-b.md](../../../art-source/f006/briefs/direction-b.md).

## Reviewers and independence

Three independent read-only reviews, each in a fresh agent context, on 2026-11-11. No reviewer produced T503, T505, T506 or T507, and none edited, committed or commented. The parent recorded their results here and condensed each one. Severity, location and findings follow the reviewers' own wording.

| Lens | Tier | Verdict A | Verdict B | Recommends |
|---|---|---|---|---|
| UI/UX Reviewer | Medium (sonnet) | Acceptable with conditions | Acceptable with conditions (more and heavier) | **A**; hub label **H-02 Office** |
| Art Director | Medium (sonnet) | Acceptable with conditions | **Not acceptable as drawn** | **A** (hybrid: B's wall as the stage-2 candidate) |
| World Designer (conformance) | Low (haiku) | Conforms with conditions | **Does not conform as drafted** | A; hub label **H-01 Works** |

**Recommendation (T508): direction A**, the open platform deck with a railing edge, carried into P2 with the conditions below. All three reviewers recommend A. The hub label is split between H-02 Office (UI/UX) and H-01 Works (World Designer); the owner decides it in OD-9b.

## UI/UX Reviewer

Scope: it opened phone portrait (plain, hub, text200), phone landscape, tablet, 640x360 text200 and both triptychs in both directions, plus several hub variants and the reduced-motion still for A. It did not play the strips. Touch sizes are estimates, at about 2.63 px/dp.

| Id | Severity | Where | Finding | Impact |
|---|---|---|---|---|
| F1 | Blocking for P1, both (shell and camera, not a direction issue) | `*/phone-landscape-640x360-200-text200.png` | Header and strip take about 35 % of the height. The card covers 5 machines. The selected machine is 63 % visible and the depot 6 %. | Fails SC-001 and SC-002 in this class. P1 needs a compact card or bottom sheet, a one-row header, and a pan-away camera. |
| F2 | Major, both | `phone-portrait.png`, `tablet-portrait.png` | Dead band of about 350 px above the lattice; the tablet world floats mid-screen. | SC-001 is at risk. The camera should centre the world in the area left free by the chrome. |
| F3 | Major, both | `tablet-portrait.png` | The first machine column is clipped by 55 px behind the zoom stack; the vein is out of view. | The camera must not clip a machine. |
| F4 | Major, B | all B sheets | The teal drum prop shares the hue family of the teal running pip. | A prop can be read as a state cue (FR-043 trials). |
| F5 | Major, B | all B sheets | The closed bunker depot shows only a small teal slot. | Weak recognition of "Output goes to the depot". |
| F6 | Minor, A | `direction-a/phone-landscape.png` | The railing pattern has high-frequency detail behind the Faulted badge and the depot. | Slight cue crowding. |
| F7 | Minor, B | all B sheets | Wall tops are close in value to the plate and lane greys. | Lower contrast between the factory and its edge. |
| F8 | Minor, both | `phone-portrait.png` | The card is offset 115 px from the left and overlaps the front edge row. | Cosmetic and inconsistent. |
| F9 | Minor, both | `phone-portrait-text200.png` | "Credits 12,480" sits tight against the strip; risk of clipped descenders. | Legibility; verify in the engine. |
| F10 | Minor, A (seen) | `direction-a/phone-landscape-640x360-200-hub.png` | The hub panel draws over the card. | The hub should replace the card; focus order is ambiguous. |
| F11 | Minor, both | zoom stack, hub rows, Upgrade | About 43, 40 and 44 dp, below 48 dp. | Accessibility. |
| F12 | Minor, both | hub panel | The hub repeats Build and Goals from the bottom tabs. | Duplicate paths. |
| F13 | Info | `still-reduced-motion.png` | Only the completion-pop frame has a reduced-motion still. | Fallbacks are needed for machine-loop and branch-pip. |
| F14 | Info | `triptych.png` | Stages 2 and 3 are placeholders. | Progression cannot be judged yet. |

Hub label assessment:
- **H-01 Works** fits at 200 %. It repeats the site name, can be read as "the factory" next to the Factory tab, and does not say what is inside.
- **H-02 Office** fits, is distinct from Factory and matches the contents (Contracts, Settings). It is a metaphor; there is no office in the world.
- **H-03 Operations** is about 150 pt and likely wraps or clips at 200 % in the compact strip. It also suggests running the machines, which collides with Factory.
- **Recommended: H-02 Office.** If the owner keeps Works, rename the Factory tab or give the hub an icon so the two are distinct.

Conditions for P1:
- **C1 Camera rule.** Centre the world in the area left free by the chrome. Never clip a machine column. Keep the selected machine 100 % visible and pan away from the card. Show the depot when it fits; show the vein if it fits. Write a rule for tablet at n=3 and for compact heights.
- **C2 Compact card pattern.** Use a bottom sheet or a narrow card for 640x360 and for 200 % text, with a one-row header. The card never covers machines. On landscape and tablet, link the card to its machine with a pointer or leader.
- **C3 Hub.** The hub and the card are exclusive. Hub rows are 48 dp. No duplicate paths. Check the label at 200 % in the compact strip.
- **C4 Touch targets.** At least 48 dp, measured in the engine.
- **C5 Reduced motion.** A static fallback for every animation, with the zero-flash gate kept.
- **C6 Header.** Nothing clips at 200 %. Chrome stays at or below 15 % of the screen at 100 % text and 20 % at 200 %.
- **A only.** Reduce the railing noise near badges and the depot.
- **B only (if B were chosen).** Recolour the teal prop, give the depot an ore cue, and lower the wall-top value.

## Art Director

Scope: it read both briefs, every report.json and check-report.json, manifest.md and Art Bible §3.4, §6 and §9 (C1, C3, C5, C8, C9). It viewed phone-landscape and the triptych of both directions. It did not open the other sheets, the strips or the reduced-motion still; for those it relies on the reports (flash 0/s against a limit of 3, all gates passed). It did not run its own palette check on the edge-tile pixels.

| Id | Severity | Where | Art Bible | Finding |
|---|---|---|---|---|
| B1 | **Blocking**, B | `direction-b/phone-landscape.png` | §3.4 keep-out, §9.4 C1 and C5, C8 ring | The right-edge wall block sits behind and beside the selected machine's frame at (0,3) and behind the Faulted badge. The ring falls on `steel_*`, and C5 allows only {plate_1, plate_2, ink_0, floor_0, floor_1}. The compositor's empty `decorViolations` is not a C8 ring check. |
| B2 | Major, B | wall tones | C5 | `steel_0/1/2` are outside the backdrop set and there is no C8 proof. The wall belongs only on the outer margin, never next to the right column. |
| B3 | Major, B | `direction-b/triptych.png` | R15 | The wall is already present in stage 1, so stage 2 has nothing to add and the gantry has no visible step. B pre-empts World Bible stage 2 "Walled yard". The wall is the heaviest visual noise and covers about a third of the world area on landscape. |
| B4 | Minor, B | `depot_00` and the drum prop | — | The hidden hopper loses the delivery readout. The cube reads as a plain box at 640x360. The teal drum risks a collision with cue hues; do not use teal on props. |
| A1 | Major, A | `direction-a/triptych.png` | R15, §6 | A has no wall art of its own; its stage 2 borrows B's wall. The headroom for growth exists, because the railing is the lowest-mass edge. A P2 wall tile must satisfy B1 and B2. |
| A2 | Minor, A | depot | — | The open frame and copper collar read well on landscape. Check that the lamp tip is not cropped on partial-depot views (phone portrait at 75 %, 640x360). |
| A3 | Minor, both | depot slab | C5 and C2 | The slab uses `plate_0/1/2`, which the brief reserves for machine plates. Document an allowance, or use `floor_1` or `steel_*`. |
| A4 | Minor, A | `edge-tile` | — | The thin railing posts repeat along every empty edge as a texture band. Acceptable, but it must not grow in P2. |

Without issue in both directions: the palette gate (24 of 24, no unknown colours), no new hue, flash 0/s, 8–11 colours per sprite, Blocked and Faulted readable, the ground family keyed by `starter-platform` on the 70x54 period, and the vein as scenery. Low vein visibility is a camera gap, not a direction difference.

**Recommended: A** for P2 and P3, with the railing as the stage-1 identity: the backdrop sets stay clean, noise stays low, and R15 keeps its headroom. Hybrid option: keep B's wall as the stage-2 candidate, placed outside the machine zone and accepted only after a C8 proof. If the owner prefers B's bunker depot, show the hopper opening.

Conditions for P2 production art:
1. Palette ids only (f004-palette 1.0.0), with the `ink_0` outline. Re-run the palette gate.
2. A C8 ring check on the densest 12-slot frame for every class: 100 % of the ring in `A_ov` or `A_fr`, and no `steel_*`.
3. Walls and gantries only on the outer margin, never behind a body or the badge band, each with a keep-out proof.
4. Visible growth: stage 1 railing, stage 2 wall on the margin, stage 3 gantry. No wall in stage 1.
5. No `plate_*` on the depot slab unless an allowance is documented. No `teal_*` on props.
6. The ground family stays keyed by `starter-platform`, with the 70x54 period and whole-pixel placement.
7. The depot stays visible at 640x360 and at 200 % text, with its hopper or delivery detail showing and the lamp not cropped.
8. Flash at most 3/s, with a reduced-motion still.
9. Budget the cost per stage: a stage-2 wall is one tile family; the gantry is a separate art pass.

## World Designer conformance read

Read against `F006-WB v0.1` and [world-copy.md](world-copy.md). It viewed the phone-landscape and triptych sheets of both directions and three hub variants.

| # | Check | A | B |
|---|---|---|---|
| 1 | Neutral starter platform on a dark base, no lore | PASS | PASS |
| 2 | "Meridian Works" shown correctly | PASS | PASS |
| 3 | "location" in player text, never "plate" or "bay" (the item "plates" is allowed) | PASS | PASS |
| 4 | Hub label from H-01, H-02 or H-03; never Systems, Menu or More | PASS | PASS |
| 5 | One depot, static trunks, no machine-to-machine links | PASS with condition (confirm in wiring that trunks end at the depot) | PASS with condition (the bunker hides the hopper) |
| 6 | One ore vein, scenery only | PASS with condition (visible on one class only, 10.8 %) | PASS with condition (same) |
| 7 | Swappable ground keyed by `starter-platform` | PASS | PASS with condition (wall keep-out proof needed) |
| 8 | Stages: Open deck (railing), Walled yard (wall), Gantry line (gantry) | CONDITIONAL (stage-2 and stage-3 art missing) | **FAIL**: stage 1 is a wall, against G-01 Open deck |
| 9 | Deposits and props are scenery only | PASS | PASS |
| 10 | No forbidden terms | PASS | PASS |
| 11 | Briefs cite `F006-WB v0.1` | PASS | PASS |

Verdicts:
- **A conforms with conditions**: trunks end at the depot, the vein is legible or recorded as out of view, and stage-2 and stage-3 art exist before the progression is accepted.
- **B does not conform as drafted.** It would conform only if OD-9b amends the World Bible to allow a walled stage 1 (a MINOR or MAJOR version under §7), or if B is redrawn with a railing for stage 1.

Hub label: **H-01 Works** fits the tone rules best: short, in the tone vocabulary, and matching the site name.
- H-02 Office implies staff and an office, which leans toward the "people" and "corporate" readings that §3 and §4 avoid.
- H-03 Operations is long and reads as corporate.
- None of the three is forbidden.

## Items for the owner decision OD-9b (T509)

1. **Direction**: A, as all three reviews recommend; B; or the hybrid of A with B's wall as the stage-2 candidate after a C8 proof.
2. **Hub label**: H-01 Works (World Designer) or H-02 Office (UI/UX). H-03 Operations has no recommendation.
3. **Changes**: the conditions above become P1 and P2 inputs (camera rule C1, compact card C2, hub C3, touch C4, reduced motion C5, header C6; Art Director conditions 1–9).

## Owner decision OD-9b (T509)

Decided by the owner on 2026-11-11 and recorded by the parent. The T508 QA PASS is on `eda2925` (#683, merged in #725).

1. **Direction: A, the open platform deck, with B's wall as the stage-2 candidate.**
   - **Stage 1 (Open deck):** A's railing edge, crate and lamp, and A's open depot frame.
   - **Stage 2 (Walled yard):** B's wall art is the candidate. It sits only on the outer margin, never next to a machine column or the badge band, and is accepted only after a C8 ring proof (Art Director condition 2).
   - **Stage 3 (Gantry line):** new gantry art in P2.
   - B's teal drum prop and B's closed bunker depot are not adopted.
2. **Hub label: H-02 "Office"**, as UI/UX recommended. It replaces "Systems" (OD-1). The World Designer noted that "Office" leans toward a people or corporate reading. That is accepted, because the word is not forbidden under `F006-WB v0.1` §4. No World Bible version change is needed: the label is one of the approved v0.1 candidates in [world-copy.md](world-copy.md).
3. **Conditions: adopted in full as binding P1 and P2 inputs.**
   - UI/UX: C1 camera rule, C2 compact card pattern, C3 hub exclusive with the card, C4 touch targets of at least 48 dp, C5 reduced-motion fallbacks, C6 header, and the A-only railing-noise reduction.
   - Art Director: conditions 1–9.
   - World Designer: trunks end at the depot, the vein is legible or recorded as out of view, and stage-2 and stage-3 art exist before the progression is accepted.
   - Assignments for the affected later tasks cite this section.

SC-012 (part 1) is met: a direction is chosen from rendered evidence, with independent reviews.
