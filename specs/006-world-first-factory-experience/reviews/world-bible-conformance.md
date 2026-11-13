# World Bible conformance review (FR-047, SC-013)

Checked against `F006-WB v0.1` ([docs/world/world-bible-v0.1.md](../../../docs/world/world-bible-v0.1.md)), frozen by OD-9a.

## Part 1 (T525)

**Task**: T525, GitHub Issue #698. **Date**: 2026-11-13. **Base commit**: `48b4312`. **Reviewed**: PRs #737 to #742 (T519 to T524), source assets only.
**Reviewer**: World Designer, conformance lens. An independent, read-only session; the reviewer did not produce any of these assets. The parent recorded the result. The other lenses (Art Director, UI/UX, Game Designer/PO, Game Systems) are in [art-direction-f006.md](art-direction-f006.md).
**Scope**: World Bible sections 1, 3, 4, 5 and 6, [world-copy.md](world-copy.md), and the OD-9b World Designer conditions in [direction.md](direction.md). Palette, style and numbers are out of scope (Art Director, Game Systems). Evidence: briefs, contact sheets (viewed as PNG) and check reports.

### Overall verdict: CONFORMS_WITH_CONDITIONS

No family breaks a forbidden reading: no people, lore, brand, planet, power or region names; no text baked into any sprite; no hype or "Systems" label. All conditions are composite or wiring items that cannot be proven at source-asset level. They carry to T527, T534 and T535.

Findings: 0 blocker, 0 major, 5 minor.

### Per-family verdicts

| Family | Verdict | Evidence |
|---|---|---|
| GROUND | CONFORMS | Flat dark neutral deck diamond, hatch for locked. Single region id `starter-platform`, no region-specific motif, so the swappable-ground rule (section 5) holds. Mood (quiet, dim) fits section 3. No lore marks. |
| FOUNDATION | CONFORMS | Outline and flat plate on the location; no plinth or wall. Matches "machines sit at their locations" and the stage-1 open deck. |
| DEPOT | CONFORMS_WITH_CONDITIONS | Reads as one central store: steel slab, copper hopper collar with dark opening, ore fill rising from empty to full, one static work lamp. The open frame matches direction A, not the closed bunker. Stage dressing s1 posts, s2 parapet, s3 gantry beam with hoist matches the three stages. Conditions: (1) "trunks end at the depot" is not provable from art; the link family has no terminus piece, so T527 must show each trunk reaching the slab or hopper; (2) the s2 parapet appears on one back edge in the sheet, the brief says two (minor, confirm intent); (3) hopper visibility at 640x360 is a camera matter (P2-F6). |
| DEPOSIT + vein | CONFORMS_WITH_CONDITIONS | Brown ore chunks with coal flecks on a dark patch; stages 1 to 3 add chunks and a spire. It reads as a resource outcrop, a source, not a machine or an input. Highlight and chip loop are cosmetic only, consistent with section 6 (scenery, never a hit target). Condition: the vein sits one cell outside Bounds and today is visible on one class only (10.8 %); T534 must make it legible or record the out-of-view classes per screen class (P1-F2). Not verifiable at T525. Pacing effect is not this lens (Game Systems; see [art-direction-f006.md](art-direction-f006.md)). |
| PROP | CONFORMS | Crate, crate stack, pallet, static lamp: neutral industrial, plain, no branding, no marks. The copper cross-strap is a tone, not a logo. Lamp static (no flicker). Scenery only. |
| EDGE | CONFORMS_WITH_CONDITIONS | The triptych shows a clear stage ladder: slim railing (open deck), low solid wall (walled yard), tall frame with beams, braces and hazard dashes (gantry line). Growth is monotonic (10, 16, 30 px). Conditions are the placement items under P2-F7 and the C8 composite proof (P1-F5). |
| LINK | CONFORMS_WITH_CONDITIONS | Thin dark trunk line with copper dashes, T-branch, active branch. Reads as a conveyor or lane, not a machine-to-machine link when drawn as a trunk to the depot. Condition: junction and branch pieces must only be placed trunk-to-depot (OD-2, R07); a placement that connects two machines would break section 1. No end-cap art, so termination must be composed (T527). |
| FX | CONFORMS | Dust, spark, steam, glint, pop, pip: abstract, no text or symbol with meaning, mood neutral. The teal Assembler in the composite is an existing F004 machine, not part of this family. |
| F006-UI | CONFORMS_WITH_CONDITIONS | All three elements are "not needed"; the hub icon reuses the contracts glyph. The label is "Office" (OD-9b, in the approved v0.1 set). See P2-F9 below. |
| K4 cue tail | CONFORMS | Plain badge with a pointer tail; five states, no text, no world content. |

### Findings

| Id | Severity | Family | Finding | Carried by |
|---|---|---|---|---|
| M1 | minor | DEPOT | The s2 parapet appears on one back edge in the sheet; the brief says two. Confirm intent. | T527 |
| M2 | minor | LINK | No trunk terminus art; "trunks end at the depot" must be shown by composition. | T527 |
| M3 | minor | DEPOSIT | The vein is out of view on most classes (visible on one class only, 10.8 %). Make it legible or record the out-of-view classes per screen class. | T534 |
| M4 | minor | EDGE, DEPOT | Stage-2 and stage-3 art exist at source level but are not yet composed, so the OD-9b condition is met only at source level. | T527 |
| M5 | minor | F006-UI | P2-F20 settings copy "Show zoom buttons" still needs a World Designer read and is not in `world-copy.md`. | T547 |

### OD-9b World Designer conditions (status at T525)

| Condition | Status |
|---|---|
| Trunks end at the depot | Not provable at source level. The link family has no terminus piece; T527 must show each trunk reaching the slab or hopper (M2). Carried to T527. |
| Vein legible, or recorded as out of view per screen class | Open. The vein is one cell outside Bounds and visible on one class only (10.8 %). Carried to T534 (P1-F2). |
| Stage-2 and stage-3 art exist before the progression is accepted | Met at source level only: railing, wall and gantry pieces and depot s2/s3 dressing exist (M4). Composition carried to T527. |

### Answers to assigned questions

**P2-F7(a): front-left / front-right / front-apex reading.** Accept. In the triptych the pieces read as front-left, front-right and front-apex pieces forming a V under the deck, for all three stages. The corner reads as the apex. Condition for T527: place only on the front margin row along the two diagonals, and record the layout rule in Art Bible section 21.4.

**P2-F7(b): is a front-only wall an acceptable "Walled yard"? OWNER DECISION; the World Designer recommends yes.** World Bible section 1 defines stages by what the deck looks like ("open deck, then walled yard, then gantry line") and does not require a closed perimeter. A wall along the viewer-facing edges is a legitimate reading of "walled", especially since the back and side edges are kept clear for the machine column, badges and markers (C8). Recommend accepting, with the stage name staying "Walled yard" and the stage-2 default staying provisional until the C8 composite proof (P1-F5). If the owner wants a more enclosed look, the cheaper alternative is a low back wall only if the keep-out proof allows it; not recommended now.

**Stage 3 as a "Gantry line": yes, with a condition.** The stage-3 pieces read as a gantry frame: posts, top beam, braces, hazard dashes, a line of repeated bays along the front. It is clearly taller than the wall and unlike the railing. Condition: the 30 px height must pass the T527 composite readability and keep-out checks (Art Director owns that).

**P2-F9: contracts glyph as the "Office" hub icon. OWNER DECISION; the World Designer recommends accept for now.** The glyph (a list or ledger) is a plausible management and paperwork metaphor, matches the Office reading, and is not a forbidden term or lore. Risk: it duplicates the existing Contracts entry, so the hub icon and the Contracts row could be confused. Recommend accepting for P3 on one condition: the hub chip always shows the word "Office" next to the glyph (never icon alone, per the world-copy self-check), and T535 verifies the hub icon is distinguishable from the Contracts row icon. If the confusion shows in the human playtest, commission a distinct icon (an F006-UI sprite) then. Note: the Art Director's lens takes a different position (CHANGES_REQUIRED, new glyph); both are recorded in [art-direction-f006.md](art-direction-f006.md) under "Owner decisions open".

**P1-F5: stage-2 wall default provisional until C8.** Agree; keep provisional. The source-level evidence (front margin row zero overlap, at least 13 px from masks; left and right rows 1 to 2 px from the machine column and not drawn) is sound, but only the composite ring proof on the densest 12-slot frame per screen class can close it. Carried to T527. Not accepted as final at T525.

**Depot and deposit/vein world reading.**

- Vein: it reads as a resource source (ore outcrop with coal), distinct from machines, and its dressing grows by stage as section 6 allows. Its legibility in play depends on camera visibility (T534).
- Depot: it reads as a central store that receives output (open hopper with a visible fill state, copper collar). Whether trunks visibly end at it is a composition and wiring question. There is no art blocker, but T527 must show it.

**P2-F20 (settings copy "Show zoom buttons").** Not in `world-copy.md` and not read at T525. Carried to T547 for a World Designer read (M5).

### Not verifiable at T525 (carried, never passed)

| Item | Carrier |
|---|---|
| C8 composite ring proof (including the stage-2 wall, P1-F5) | T527 |
| Trunks end at the depot in composition; junction and branch placed trunk-to-depot only | T527 |
| Stage-2 and stage-3 progression composed on the platform | T527 |
| Vein visibility per screen class (P1-F2) | T534 |
| Hopper and lamp visibility at 640x360 and 200 % text | T534 |
| Hub icon distinguishable from the Contracts row icon; label "Office" always visible | T535 |
| P2-F20 copy read | T547 |

### Owner-decision items

1. P2-F7(b): a front-only wall as "Walled yard" (the World Designer recommends yes).
2. P2-F9: the contracts glyph as the "Office" hub icon (the World Designer recommends accept, with the word "Office" always shown and a distinguishability check in T535).

P1-F5 stays provisional by the earlier owner decision; no new decision is needed.

## Part 2

Placeholder. Part 2 is recorded with T547.
