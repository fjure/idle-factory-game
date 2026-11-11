# What I Learned: World-First Factory Experience (F006)

**Feature**: Make the factory the screen: a world deck, depot and labelled hub in place of the six-tab bar. Gameplay, content and saves stay unchanged.
**Generated**: 2026-11-11 (P0 batch)
**Scope**: Phase P0 only (T500-T509: baseline, World Bible, direction briefs, mockups, reviews, owner decisions)
**Implementation status**: P0 merged on main (Issues #678-#683; PRs #721-#726) plus the parent decisions OD-9a and OD-9b on `feat-006-maqa-batch1`. P1-P4 not started.

---

## P0 batch: Key Decisions

### 1. Choose the art direction from rendered mockups with independent reviews, not from briefs

**What we did**: Two directions (A open platform deck, B walled hall) were drawn as scripted concept sprites, composed into per-class mockup sheets on the real lattice geometry, and reviewed by three fresh read-only agents (UI/UX, Art Director, World Designer). The owner picked A with B's wall as a stage-2 candidate.

**Why**: Words hide differences; the first sheets made both directions look the same and the parent rejected them. Rendering on the real layout exposed things a brief cannot: a wall behind the selected machine and badge, a teal prop that reads as a state cue, a stage 1 that already used up the stage-2 art.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Pick from the written briefs | The spec (OD-4) requires rendered evidence; briefs cannot show collisions with badges or the camera. |
| Adopt B as drawn | Art Director found a blocking C8 ring violation and a wall in stage 1, against the World Bible's "Open deck". |
| Amend the World Bible to allow a walled stage 1 | It would cost a version bump and remove the visible growth the stages promise. |

**When you'd choose differently**: If a direction can only be judged in motion or on a device, a mockup is not enough; then a short in-engine spike is worth its cost.

---

### 2. Make the mockup tool consume the real geometry

**What we did**: A test in Unity exports today's slot-grid geometry to JSON, and the Python `mockup` compositor reads it, so sheets are laid out by the game's own numbers.

**Why**: A redrawn layout would drift from the game and the reviews would judge a picture that cannot ship. One source of geometry keeps the sheets honest and reproducible (byte-identical across two runs).

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Hand-made layouts in an image editor | Not reproducible, not tied to screen classes. |
| Screenshots of a Unity scene | Needs the editor slot and the art does not exist in the engine yet. |

**When you'd choose differently**: When the geometry itself is still being designed (P1 layout seam), regenerate the JSON rather than trusting an old export.

---

### 3. Model UI occlusion in every framing check

**What we did**: QA failed the first T507 round on tablet, because "is the selected machine visible" was measured against the whole screen while a docked side panel covers part of it. The check now measures against the area the docked UI leaves free, and the tablet depot is panned into view.

**Why**: Visibility is a property of the world rect the player actually sees, not the viewport. The same rule drives the P1 camera (C1): centre the world in the free area and never clip a machine column.

**When you'd choose differently**: If the UI is a pure overlay with no docking, full-viewport measurement is acceptable; here the card and sheet dock.

---

### 4. Measure the baseline before changing anything, and label thresholds guidance

**What we did**: `ShellGeometryProbe`, `InteractionCountJourney` and `BaselineRig` measured 10 screen classes at 100 % and 200 % text. Only desktop 1920x1080 meets SC-001 at both scales; the shell shows 13 persistent labels against a target of at most 6; a phone sheet leaves 44-55 % of the world visible.

**Why**: Without a "before", the later refactor cannot prove improvement, and proposed thresholds (guidance, OD-6) can be judged against reality instead of taste. Journey counts dispatch the same event a click reaches, which is the only reliable route in the batch editor.

**When you'd choose differently**: Cheap changes with obvious effects do not need a 100-screenshot matrix; here the matrix is the evidence for later owner acceptance.

---

## P0 batch: Concepts to Know

### Gate cleanup on the exit code of the thing that can refuse

A chained shell sequence ran `gh issue close`, worktree removal and branch deletion after `maqa-merge.ps1` had refused a conflicting PR. The Issue closed unmerged and the PR closed with its branch. Run the guarded step alone, check its exit code (and `git merge-base --is-ancestor`), and only then clean up. Two Issues that edit the same file (`art_pipeline/__main__.py`) will conflict after the first merge, so dry-run the second.

### A check that reports nothing is not a proof

The compositor's empty `decorViolations` list was not a C8 ring check, so it could not clear the wall art. Name what a gate actually covers and add a separate proof where the rule (here the Art Bible C8 ring) is stricter.

### Layout classes that break a single pattern

The 640x360 class at 200 % text cannot hold a docked card beside the machine, and the phone-portrait and tablet cameras leave a dead band while the tablet clips a column. Design the compact pattern (bottom sheet or narrow card) as its own case, not as a scaled-down desktop card.

### Environment traps seen again

The emulator /data partition fills up (reinstall or clear old APKs). Unity runs re-serialize the iOS icon noise in ProjectSettings each time; restore it. A Unity worktree removal needs the `\\?\` delete, and Android builds leave `.utmp/` in the worktree root.

---

## Architecture Overview

P0 changed no runtime code except test-only probes. The baseline probes read the real shell, panels, slot grid and camera framing in Play Mode rigs. The art tooling lives under `tools/art-pipeline` (`mockup`, `concept-f006`) and `art-source/f006/`, fed by `tools/art-pipeline/f006/lattice-geometry.json` exported from Edit Mode.

---

## Glossary

- **F006-WB v0.1**: the frozen World Bible; player text says "location", hub label "Office".
- **OD-9a / OD-9b**: owner decisions approving the World Bible text and the direction plus hub label.
- **C8 ring**: the Art Bible rule that the ring around a selected machine falls only on allowed backdrop tones.
- **Occlusion-aware framing**: visibility measured against the area not covered by docked UI.
- **Stage 1/2/3**: Open deck (railing), Walled yard (wall), Gantry line (gantry).
