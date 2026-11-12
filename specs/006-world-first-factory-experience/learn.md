# What I Learned: World-First Factory Experience (F006)

**Feature**: Make the factory the screen: a world deck, depot and labelled hub in place of the six-tab bar. Gameplay, content and saves stay unchanged.
**Generated**: 2026-11-11 (P0 batch); 2026-11-12 (P1 batch, see the P1 section below)
**Scope**: Phases P0 (T500-T509) and P1 (T510-T518)
**Implementation status**: P0 merged on main (Issues #678-#683; PRs #721-#726) plus the parent decisions OD-9a and OD-9b on `feat-006-maqa-batch1`. P1 merged (see P1 section); P2-P4 not started.

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

---

# Batch P1: the enabling refactor (T510-T518)

**Generated**: 2026-11-12 (P1 batch). **Status**: P1 merged on main (Issues #684-#691; PRs #728-#735) plus the parent record T518 (e6c2d2a) on `feat-006-maqa-batch2`. Edit Mode 2569 to 2683 tests, Play Mode 2596 to 2616. P2-P4 not started.

## P1 batch: Key Decisions

### 5. Put the world behind a layout seam before drawing any of it

**What we did**: `FactoryWorldLayout` (anchors, footprints, hit shapes, sort keys, `LayoutId`, `Bounds`) is built by `LatticeLayoutBuilder` from `WorldCompositionSettings`; the camera now reads `LayoutId` and `Bounds` instead of lattice columns. Test layouts with 6, 20 and L-shaped locations prove nothing depends on 3 or 4 columns.

**Why**: Deck, links, depot and vein all need positions. If each reads the lattice directly, the first non-lattice layout breaks all of them. A seam with a parity test (camera fit unchanged for every screen class) lets the art phases build on one contract.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Extend `ScaleLayout` with more fields | It stays lattice-shaped and keeps columns as identity. |
| Build layout and art together in P2 | A failed art review would then block the structure and the camera migration at once. |

**When you'd choose differently**: With one fixed layout and no variants, a seam is overhead; here stage growth and test layouts justify it. The runtime still uses built-in `Shipped` defaults; wiring the asset is follow-up P1-F1 (T527).

---

### 6. Make refactors pure moves, then change behaviour separately

**What we did**: `SlotGridView` moved to `Presentation/World/` with no logic change (T510), and `ShellNavigationStack` and `ShellSheetHost` were lifted out of the 2652-line `RunShellDocument` with `HandleBack` delegating (T511). A Play Mode `RunShellTestNavigation` seam (T515) keeps tests off private shell state, with an R19 re-baseline list for tests that must change later.

**Why**: A move that changes nothing can be reviewed by diff and proven by unchanged tests. Mixing behaviour into it hides regressions in churn.

**When you'd choose differently**: If the old structure blocks a test you need today, extract just enough and accept the behaviour change in its own Issue.

---

### 7. Count completed cycles in the simulator, expose them only through the session

**What we did**: `RunSimulator` emits a per-advance count (+1 per cycle, never the batch factor), `RunSession` keeps a session-only `long` per instance, and `MachineStateView.CompletedCycles` surfaces it. It is not saved and no economy code reads it; a no-observer-effect test compares state hashes.

**Why**: The shortage signal and pips need a "something finished" event. A stateless simulator cannot own a counter, and a saved counter would be a save-format change for a display value.

**When you'd choose differently**: If the number must survive a restart (for example for an achievement), it belongs in the saved state with a migration, not here.

---

### 8. Decide anchors from measurement, not preference (A-03)

**What we did**: `a03_anchor.py` placed badges on the densest frame (12 occupied slots, all badged) for candidates K0-K5 and counted overlaps in art pixels. K1 and K3 hit foreign hit shapes or own markers; K0, K4 and K2 were clean. The owner chose K4 (59,33) with a 4 px pointer tail (OD-12) and signed off Art Bible v1.1.0 (OD-10), and decided to recolour the trunk under the badge band to keep the strict C8 ring rule.

**Why**: Overlap with a foreign hit shape is a functional bug (a tap selects the wrong machine), not a taste issue; a worst-case table turns the choice into trade-offs the owner can see.

**When you'd choose differently**: Where the dense case is rare, a cheaper visual review may do; here every slot can be badged.

---

## P1 batch: Concepts to Know

### Create engine-global settings through the engine's own tooling

Sorting layers live in `TagManager`, and `SortingLayer.NameToID` silently returns 0 for a missing layer, so T513 created them via Unity tooling and added `SortingLayerGuardTests`. `check-settings-noise.ps1` has no TagManager signature, so a deliberate change passes clean; the exception is recorded in `reviews/test-rebaseline.md`.

### Unity serializes arrays as packed hex

An enum or int array in ScriptableObject YAML appears as `edgeRulePerStage: 000000000100000002000000i`, the same form as `m_StackTraceTypes`. It is tool output, not a hand edit; a reviewer once flagged it wrongly.

### Authority lists have narrow exemptions

Presentation-only tuning ScriptableObjects pass `JsonContentAuthorityTests` through the one-entry `PresentationTuningAssets` list, so gameplay content stays JSON-authoritative.

### Reuse the imported Library between worktrees

Moving `Library/` from the merged worktree into the next Issue's worktree makes each sequential Unity run import incrementally, and a worktree without `Library/` removes without the `\?\` delete. Every Unity run still re-serializes `ProjectSettings.asset` (+130/-1, iOS icons) and creates untracked `UnityDefaultRuntimeTheme.tss` and `Assets/UI Toolkit.meta`; restore or remove them before committing.

### Environment faults to expect

`SessionLogAllocationTests.FlushThroughTheRealFileLayerStaysInTheAllocationAndMillisecondBudget` is a host-timing flake (64.9 ms against 25 ms once, then green). A usage limit killed the T510 worker mid-run; renew the lease and resume the same worker with SendMessage. A transient GitHub GraphQL error dropped a label edit and a comment in one chained command, so re-verify the marker comment afterwards.

## P1 batch: carried follow-ups

`reviews/p1-followups.md` (P1-F1 to F6) names the later task for each. Notable: wire the composition asset at runtime (T527) and add a camera `ViewBounds` because the depot and vein sit outside the pan clamp (P1-F2, T534). The `data-model.md` corrections are in 4315ecd.

## P1 batch: Glossary additions

- **Layout seam**: `FactoryWorldLayout` plus `LatticeLayoutBuilder`; the one source of location geometry.
- **OD-10 / OD-11 / OD-12**: owner decisions on Art Bible v1.1.0, the `CompletedCycles` design and the A-03 anchor K4.
- **A-03**: the badge-anchor question; K0-K5 are the measured candidates.
