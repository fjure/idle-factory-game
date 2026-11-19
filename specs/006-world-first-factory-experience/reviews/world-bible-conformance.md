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

## Part 2 (T547, final)

**Task**: T547, GitHub Issue #718. **Date**: 2026-11-19. **Base commit**: `b44b87d` (the shipped build). **Reviewer**: World Designer, conformance lens; an independent, read-only session that built none of the reviewed work. **Checked against**: `F006-WB v0.1` (frozen by OD-9a) and `F006-WB v0.2` (MINOR, owner 2026-11-19, CF-1: "Store" is the capacity term, "Depot" the structure; [world-bible-v0.2.md](../../../docs/world/world-bible-v0.2.md)), [world-copy.md](world-copy.md), the OD-9b World Designer conditions in [direction.md](direction.md), Part 1 above, and `p3b-followups.md` (coordination copy, newer than main).
**Evidence location (not all in this checkout)**: the captures `playtest-agent-f006-C/` (c03, c07, c17, c23, e08), `uiux-f006-C.md`, `playtest-agent-f006-C.md` and the newer `p3b-followups.md` are NOT on `main` at `b44b87d`. They were read from the coordination worktree `C:/Users/Domenik/ai-projects/idle-factory-game/.claude/worktrees/feat-006-world-first/specs/006-world-first-factory-experience/reviews/` (branch `feat-006-maqa-batch5`, PR #775), read-only. Only `t527-proofs/` and this record are in this branch. Every reference below to those files points at that path until #775 merges.
**Evidence**: (a) visual: the gate captures `playtest-agent-f006-C/` (c03, c07, c17, c23 viewed as PNG; coordination worktree) and the composites `t527-proofs/3x4-stage2.png` and `3x4-stage3.png` (this branch); (b) strings: a scan of the shipped source (`Assets/IdleFactory/Unity/UI/**`, `Presentation/**`, UXML and USS; `RunRejectionCopy`, `MilestoneCopy`, `HubPanel`, `MachineCard`, `BuildCard`, `ExpansionPanel`, `OfflineSummaryPanel`, `SettingsPanelPresenter`, `OreVeinCopy`, `AttentionModel`, `PersistentLayer`, `RunShellDocument*`). No localisation tables exist; all copy is in code or UXML. Content data holds resource, machine and research labels of F001 to F005, which are not F006 world strings.
**Scope note**: palette, numbers, layout fit and code quality are not this lens. Tone, vocabulary, naming and forbidden-term rules, and the world reading of deck, depot, links, props, vein and stage dressing, are.

### Overall verdict: CONFLICTS (4 conflicts, 0 blockers; CF-1 is RESOLVED (v0.2); each remaining conflict is a proposed string change not yet applied)

Under the task rule, a proposed change counts as a conflict until an implementation worker applies it; CF-2 to CF-5 are applied by T560 (#776). No conflict touches a forbidden reading: no people, lore, brand, planet, power, region or hype word; no exclamation marks in player text; no text baked into a world sprite; "Systems" is not a label anywhere. The world art itself conforms. The remaining conflicts are vocabulary drift in strings.

- Conflicts: **4** (CF-2 to CF-5 below). Severity: 1 major (CF-2), 3 minor (CF-3, CF-4, CF-5). CF-1 is resolved by owner decision (option A, `F006-WB v0.2`).
- Unowned strings: **0 player-visible literals unaccounted for**, proved mechanically in [world-copy.md](world-copy.md) section 7 ("Mechanical coverage proof"): 888 literal lines in 72 files, each inside a cited N-row or CF range (356), an L-row whole file (406), a class D whole file (52), or a listed class D line (74); 0 unaccounted. The N-rows are N-01 to N-46 (N-42 split into N-42a to N-42i); the L-rows are L-01 to L-12. L-rows are term-swept, not tone-read, and the proof states the extraction's limit. No string count is claimed. Rows that carry a CHANGE stay conflicts until applied.
- When CF-2 to CF-5 are applied the verdict becomes CONFORMS, with the device-only checks under "Not verifiable here" still carried.

### Conflicts and exact string changes

Apply as written; tests that pin the old strings change with them. Line numbers are from `b44b87d`.

**CF-1 (RESOLVED 2026-11-19, owner decision, option A): "Store" versus "Depot".** v0.1 rule 3 said the central store is never "store" in player text, while the shipped build calls the stock capacity "Store" everywhere and shows it in the world as the depot (the depot FULL sprite means the Store is at capacity; contracts/world-presentation.md section 2). The owner chose option A: `F006-WB v0.2` (MINOR) makes "Store" the capacity term and "Depot" the world structure. No string is renamed. Every shipped "Store" string, exhaustively, now conforms:
- `FactoryRun.uxml:18` (caption), `:23`, `:63` ("Store full"); `RunShellDocument.cs:918-920` (pill and tooltip), `:2953-2955` (exact value).
- `MachineCard.cs:71` ("Store is full"), `:90` (cause detail; proposed shorter text under B-4).
- `OreVeinCopy.cs:19` ("Store full"), `:71` (", store full").
- `HubPanel.cs:581` ("Store N% full").
- `InventoryRatesPanel.cs:100`, `:320`, `:348`, `:349`; `InventoryRatesPanel.uxml:8`.
- `RunRejectionCopy.cs:43`; `OfflineSummaryPanel.cs:567`; `MachinePanelPresenter.cs:389`; `PrioritiesPanel.cs:70` and `PrioritiesPanel.uxml:3`; `StartupStatusPanel.cs:807` ("first store level").
"Storage" wording for the same capacity (Expansion "Storage" and "Upgrade storage", machine panel "Storage is full", research effect, prestige reset list, `SlotVisualModel.cs:211` "storage full") is not forbidden and is not renamed; it is recorded in v0.2 section 8. Closed.

**CF-2 (major, WB tone vocabulary and rule 6, "one thing, one word"): "slot" versus "location".** 16 sites in the 15 table rows below (the two `RunShellDocument.cs` title strings share a row). The build card, the lattice accessible stop and the machine card already say "location" (`BuildCard.cs:22,25`; `RunShellDocument.cs:213,1600`; the header comment of `MachineCardCopy` says "location, never slot"), but other surfaces still say "slot" for the same thing. A player sees both words in one session (captured: c03 "Build a Miner: tap an empty slot." above "Selected: Miner at location 1"; c23 "Slots now and next"). Exact old to new:

| File:line | Old | New |
|---|---|---|
| `UI/FactoryRun.uxml:46` | `Build a Miner: tap an empty slot.` | `Build a Miner: tap an empty location.` |
| `UI/RunRejectionCopy.cs:17` | `That slot already has a machine.` | `That location already has a machine.` |
| `UI/RunRejectionCopy.cs:18` | `That slot is not unlocked yet.` | `That location is not unlocked yet.` |
| `UI/Panels/ExpansionPanel.cs:109` | `UpdateTrack(factory, snapshot.Factory, "Slots", "slots");` | `UpdateTrack(factory, snapshot.Factory, "Locations", "locations");` |
| `UI/Panels/ExpansionPanel.cs:127` | `"Slots now and next"` | `"Locations now and next"` |
| `UI/Panels/ExpansionPanel.cs:189` | `... + " slots."` | `... + " locations."` |
| `UI/Panels/OfflineSummaryPanel.cs:570` | `"Slot " + (blocked.SlotIndex + 1)...` | `"Location " + (blocked.SlotIndex + 1)...` |
| `UI/Panels/PrioritiesPanel.cs:172` | `"Slot " + (line.Slot + 1) + " · Level "` | `"Location " + (line.Slot + 1) + " · Level "` |
| `Presentation/World/SlotVisualModel.cs:110` | `"Slot " + (index + 1)...` | `"Location " + (index + 1)...` |
| `UI/RunShellDocument.cs:1288` | `"Slot " + (slot + 1) + " unlocks at factory level "` | `"Location " + (slot + 1) + " unlocks at factory level "` |
| `UI/RunShellDocument.cs:1313` and `:1370` | `... + " · Slot " + (slot + 1)` | `... + " · Location " + (slot + 1)` |
| `UI/RunShellDocument.cs:2093` | `Tap an empty slot to build, or a machine to inspect it.` | `Tap an empty location to build, or a machine to inspect it.` |
| `UI/RunShellDocument.cs:2097` | `The build sheet for slot ` | `The build sheet for location ` |
| `UI/RunShellDocument.cs:2101` | `The machine panel for slot ` | `The machine panel for location ` |
| `UI/Panels/MachinePanel.uxml:3` | `There is no machine in this slot.` | `There is no machine at this location.` |

Identifiers, reason codes (`slot.occupied`), save keys, GameObject names and content JSON keys stay; only player-visible text changes. Reachability of the older sheet paths (`RunShellDocument.cs:1313,1370,2097,2101`) was not proven from captures; change them anyway so no path leaks the word. The Expansion row (c23) and the hint (c03) are captured, shipped and visible.

**CF-3 (minor, WB rule 2): hub card "Prestige" versus "Factory Sale".** The hub card title is the enum name (`HubPanel.cs:39`, `TitleOf(card) => card.ToString()`), so the card reads "Prestige" (captured, c07), while the panel it opens is titled "Factory Sale" (`PrestigePresenter.cs:40`) and every other string says "Factory Sale". Rule 2 keeps "Factory Sale". Change `HubPanel.cs:39` to return `"Factory Sale"` for `HubCard.Prestige` (keep `card.ToString()` for the rest); `RunShellDocument.cs:38` `TabNames` entry `"Prestige"` likewise if it is shown. The status "Not ready yet: needs Factory Value ..." stays.

**CF-4 (minor, WB rule 1 and OD-1 intent): generic "system(s)" in hub text.** "Systems" is forbidden as the hub label; the shipped label is "Office", but the accessible name and two states reintroduce the vague noun, and T537 listed the first for confirmation. Rejected:

| File:line | Old | New |
|---|---|---|
| `UI/RunShellDocument.Hub.cs:81` | `" system needs attention"` / `" systems need attention"` (after `Office, N`) | `" needs attention"` / `" need attention"` (reads "Office, 1 needs attention", "Office, 2 need attention") |
| `UI/Panels/HubPanel.cs:428` | `No system is available right now.` | `Nothing is available in the Office right now.` |
| `UI/Panels/HubPanel.cs:637` | `This system could not be opened` | `That could not be opened` |

**CF-5 (minor, B-11): the two "no cause" lock strings.**

| File:line | Old | New |
|---|---|---|
| `UI/Panels/HubPanel.cs:610` | `Overdrive is not unlocked yet` | `Overdrive is locked.` (the words of `RunRejectionCopy.cs:57,65`) |
| `UI/UnlockCopy.cs:111` | `Locked: not unlocked yet.` (no condition) | `Locked.` |

Where a condition exists the copy already names it ("Overdrive unlocks after the factory reaches level 2.", c07; "Locked: unlocks after you produce ...", c07). Content with no condition cannot name one, so "locked" alone is honest; the old strings read as a tautology.

### Closure of Part 1 (T525) items

| Id | Part 1 finding | Status | Evidence |
|---|---|---|---|
| M1 | s2 parapet on one back edge, brief says two | CLOSED | `t527-proofs/3x4-stage2.png`: the depot slab carries a low parapet on its back and right edges (two edges); s3 adds the gantry beam and hoist (`3x4-stage3.png`). |
| M2 | No trunk terminus art; "trunks end at the depot" by composition | CLOSED | Every row trunk meets the vertical collector spine, which runs into the depot hopper, in both composites and in play (`c23-expansion.png`, `c17-established.png`). The layout validator enforces it (`FactoryWorldLayout.cs:537`). No branch or junction connects two machines. |
| M3 | Vein out of view on most classes | CLOSED_WITH_RECORD | The vein reads as an ore outcrop at the left margin in both composites and the portrait captures (c23): a source, not a machine. Edge clipping at level 2 portrait was `U-C1` (`uiux-f006-C.md`) and was fixed by T558 (merged); not re-captured on device. Compact landscape stays recorded under C-14. Carried to the final device gate, not passed here. |
| M4 | Stage-2 and stage-3 art not composed | CLOSED | Both stages are composed: stage 1 railing, stage 2 front wall, depot parapet, lamp and crate, stage 3 gantry frame and more props (`t527-proofs/`); the established stage shows in play (c17). Look items (gantry height, front-only wall reading as a fence) stay with the owner's visual gate (C-12, C-61). |
| M5 | P2-F20 "Show zoom buttons" needs a read | CLOSED | CONFORMS: plain, sentence case, no forbidden term; the hint "Adds zoom buttons to the factory view. Pinch zoom still works." is plain and present tense. Rows N-24 and N-25. |

OD-9b World Designer conditions: (1) trunks end at the depot: MET (M2). (2) Vein legible or recorded: MET as a record (M3). (3) Stage-2 and stage-3 art before the progression is accepted: MET (M4). The two T525 owner-decision items were decided by the owner; the word "Office" shows beside the glyph in every captured hub chip (c07, c17, c23).

### World checklist (the shipped build)

| Area | Verdict | Evidence |
|---|---|---|
| Deck (ground, foundation, edges) | CONFORMS | Dark neutral base, one region id `starter-platform`; no lore marks; ground is region-keyed (rule 5). Dressing reads railing, front wall, gantry (`t527-proofs/`, c17). |
| Depot | CONFORMS_WITH_CONDITIONS | One central store with fill states; named nowhere in player text, which v0.2 permits (the Store is the capacity term). A FULL state is clipped out of some framings (C-14, C-31; layout, not this lens). |
| Links | CONFORMS | Trunk lanes end at the depot spine; branches only trunk-to-depot; no machine-to-machine link (OD-2). |
| Props | CONFORMS | Crate, stack, pallet, lamp: neutral, unbranded scenery (c17, composites). |
| Vein | CONFORMS | A resource source plus the Mine control; D-02 "Ore vein" is not shown; the control's accessible name is "Mine Iron Ore, +0.1 per click". |
| Stage dressing | CONFORMS | Three stages, no text; names G-01 to G-03 are not shown in the build. |
| Strings | CONFLICTS | CF-2 to CF-5 open; every player-visible literal in the swept scope is owned (world-copy.md section 7). |
| Forbidden terms | CONFORMS | A search of the F006 surfaces for epic, legendary, mega, ultimate, boss, empire, tycoon, expedition, warehouse, region names and exclamation marks found none. "Systems" is not a label (CF-4 covers the generic noun). |
| Site name and tagline | CONFORMS (not shipped) | S-01 and S-02 are in no build string or UXML; they stay store and title-surface copy. |

### Carried-item dispositions assigned to T547 (p3b-followups.md)

| Id | Disposition | Detail |
|---|---|---|
| B-4 / C-29 | DECIDED, proposed change | Keep the visible first line "Short on {resource}" (category-level, correct) and make the specific cause visible, not tooltip-only, as a second line of the same label so a touch user reads it. `UI/MachineCard.cs:454`: old `causeLabel.text = text ?? string.Empty;` new as below, where the existing `causeDetail` local (line 414, today the tooltip string `text + ". " + detail`) stays for the tooltip, and a new local `string detailLine = null;` is declared beside it and assigned inside the `MissingInput` case at line 424 (the existing `var detail = MachineCardCopy.CauseDetail(presentation.LimitingCause);` stays) as `detailLine = detail;`. The new line 454 is then `causeLabel.text = text == null ? string.Empty : (detailLine == null ? text : text + "\n" + detailLine);`. Keep line 456 (tooltip). New `CauseDetail` strings (`MachineCard.cs:88-91`): AutoSold `Auto-sell is selling it` (unchanged); NoSource `No machine makes it` (unchanged); StoreFull old `The store is full, so its makers cannot deliver` new `Store full, so makers cannot deliver`; SourceTooSlow old `Its makers are not keeping up` new `Makers cannot keep up`. UI/UX measures the two-line fit at 200 % per class. |
| B-11 | DECIDED | Name the condition when one exists (already shipped); otherwise "Overdrive is locked." and "Locked." (CF-5). |
| C-56 | CONFIRMED | B-01s "Level {n}. 1 new location opens." CONFORMS: the singular of B-01, present tense, states the change and the unlock, no numbers beyond level and unlock count (rule 5), no hype. The plural form is captured with "Deck extended." (playtest-C, e08). Promoted from draft to `F006-WB v0.1` copy. |
| B-6 | DECIDED, proposed name | Card Close icon accessible name `Close machine card` (visible text stays "Close", contained in the name). `UI/MachineCard.cs:193`: old `CloseButton.tooltip = MachineCardCopy.CloseName;` new `CloseButton.tooltip = MachineCardCopy.CloseAccessibleName;` with `public const string CloseAccessibleName = "Close machine card";` beside line 77. Sheet Close (`UI/RunShellDocument.cs:2935`): old `"Close"` new `"Close panel"`. |
| C-50 | DECIDED (copy only) | The banner announce text is `MilestoneCopy.Full(...)` as built (`MilestoneBanner.AccessibleText`): the primary line, a space, then "Deck extended." when the stage rose, with no prefix. Plain and complete. The live-region mechanism is not a copy matter (see the table below). |
| C-38 | CONFIRMED, deliberate | World markers, deck, depot, trunks, props and vein sprites have no screen-reader name or focus, by decision. From the world-text side this is right: they are scenery and state pictures, not controls; the meaning is carried by the strip indicator ("Short on Iron Ore, 2 machines ..."), the location stops ("Locations. Arrow keys move between locations"), the vein control's name and the card's cause line. Naming each sprite would add noise. Revisit only if the human playtest shows a gap. |

### Not World Designer scope: needs an implementation owner

| Id | One line |
|---|---|
| C-7 | A full F004 re-import reverts the K4 tails; make `F004ArtIntegration` run the swap or fail with a pointer (tooling). |
| C-22 | `familyFpsCaps` is not serialized in `WorldCompositionSettings.asset`; serialize at the next Unity re-save. |
| C-41 | `BuildCard.Refresh` rebuilds the factory snapshot on every quantity change while open; cache and measure (SC-008). |
| C-48 | Cancel-left order unrecorded; no test of the build card not-available state; warning and danger text contrast not asserted; T555 A3 and A6 lack direct assertions. |
| C-53 | The bootstrap is 1862+ lines (extract a world feedback coordinator); the `DeckPlanner.Build` legacy undressed path is a footgun. |
| C-59 | 640x360 at 200 %: Cancel and Confirm break mid-word, a 3-line title, about 1.3 type rows, Confirm collides with the dev label; "Overdrive Ready" clipped at 360x640; "Research" breaks at 844x390 (layout). |
| C-60 | Mine button overlaps slots with a card open; stale idle hint under the tablet build card; Back left Market open after Expand factory; a 9 s gap shows a full Welcome back modal; the world recentres about 120 px at hand-over. |
| C-64 | `FitBounds`/`ViewBounds` computed twice per frame; the fit test mirrors the production formula; no test asserts B-04 in the offline summary; stale `RunViewportDriver` risk. |
| C-67 | The autosave interval is a code default, not a run setting (PO to confirm); the gate is held about 30 ms; diagnostics listeners may run on a pool thread; stale wiki pages need the batch ingest. |
| B-7 | T535 UI/UX U-2, U-4 to U-7 and T534 UX F3 were never checked. |
| B-12 | In-flow notices shift the body when one arrives or is dismissed (layout; needs a playthrough). |
| C-50 mechanism | Whether TalkBack reads the banner (a live region) is a platform and UI question; only the text is decided here. |

### Not verifiable here (carried, never passed)

| Item | Carrier |
|---|---|
| Vein and depot framing on device after T558, per screen class | final device gate |
| Two-line cause label fit at 200 % per class once B-4 is applied | UI/UX re-measure with the change |
| TalkBack reading of the banner, hub accessible name and attention strip (Android tree is one SurfaceView, C-62) | final gate |

### Required follow-up (for the parent)

1. CF-1 is decided (option A, `F006-WB v0.2`, authored in this PR); the parent records v0.2 on the coordination branch.
2. T560 (#776) applies CF-2 to CF-5, B-4 and B-6 exactly as listed, with the matching test string updates and a 200 % re-read.
3. A short World Designer re-read afterwards to flip the verdict to CONFORMS.
