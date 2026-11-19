# What I Learned: World-First Factory Experience (F006)

**Feature**: Make the factory the screen: a world deck, depot and labelled hub in place of the six-tab bar. Gameplay, content and saves stay unchanged.
**Generated**: 2026-11-11 (P0 batch); 2026-11-12 (P1 batch); 2026-11-13 (P2 batch; P3 batch A); 2026-11-19 (batch 5, see the last section)
**Scope**: Phases P0 (T500-T509), P1 (T510-T518), the first P2 batch (T519-T524, T532, T533, T538), P3 batch A (T525, T526, T534, T535, T552-T554) and batch 5 (T527-T529, T536, T537, T539, T540, T555-T559)
**Implementation status**: 47 of 60 listed tasks are checked (batch 5 merged on 2026-11-18, main b44b87d5, gates B and C accepted). Open: T530, T531, T541 to T551. P0, P1, the first P2 batch, P3 batch A and batch 5 are merged.

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

---

# Batch P2: art families and the first ready gameplay and UI Issues (T519-T524, T532, T533, T538)

**Generated**: 2026-11-13 (P2 batch). **Status**: P2 merged on main (Issues #692-#697, #701, #705, #709; PRs #737-#745, main 3177056) plus the checkbox mirror 8c5fb2e. Follow-ups P2-F1 to P2-F21 are in `reviews/p2-followups.md`. P3-P4 and the remaining P2 tasks (T525-T531) are not started.

## P2 batch: Key Decisions

### 9. Script the art that is geometry, render only what needs light (LINK and FX)

**What we did**: Link tiles and FX sprites come from a scripted generator (`link_fx.py`) with a per-family flash matrix (caps Miner, Smelter and Press 10 fps, Assembler 8 fps) and a C8 ring proof, while ground, depot, deposit and edge pieces went through Blender rig-v2 headless plus a pixel pass. Trunk tiles were recoloured to keep the strict C8 ring rule.

**Why**: Flat pixel shapes are exactly reproducible from code, so a measurable rule (flashes per second, ring clearance) can be a test instead of a review opinion. Lit volumes are cheaper to render than to draw.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Hand-paint every link and FX frame | No repeatable proof of the flash and ring limits |
| Render everything in Blender | Needless for flat shapes, and the Blender MCP bridge was unreachable in every P2 task anyway |

**When you'd choose differently**: For organic or lit art, a render pipeline pays off; for flat geometry with numeric budgets, a generator is better.

---

### 10. Decide "not needed" explicitly (F006-UI sprites)

**What we did**: T523 recorded all three UI sprites as "not needed" (USS plus PanelIcons) in Art Bible 21.2 instead of drawing them.

**Why**: A recorded decision stops a later task from rediscovering the question and keeps the atlas small.

**When you'd choose differently**: If the Art Director finds the USS banner too plain (P2-F9), the decision reopens.

---

### 11. Make presentation models allocation-free and refresh them every frame

**What we did**: `MachinePresentationModel` and `AttentionModel` rank shortages, cap markers at 2 plus store-full, and refresh without allocating. `CompletionsDelta` clears only inside `Refresh`.

**Why**: The world view runs every frame, so garbage matters. The cost of this design is a trap: a caller that skips `Refresh` sees the same delta forever (P2-F10).

**When you'd choose differently**: A model with an event queue would not need per-frame polling, but it would allocate or need pooling.

---

### 12. Retry a transient file-system failure, and test with injected failures (T538)

**What we did**: `File.Replace` onto an existing file fails about 0.6 % of the time on this host (`IOException` 0x80070497), so the store retries at 15, 30 and 60 ms. Tests inject scripted failures before the real call and assert outcomes, not attempt counts.

**Why**: The first QA fails were not logic bugs but host noise colliding with exact-count assertions. A retry makes the store robust; injected failures make the tests deterministic.

**When you'd choose differently**: The retry blocks the main thread (P2-F21); for larger writes move it off-thread.

---

## P2 batch: Concepts to Know

### Injected faults beat real-system assertions

Where the real file system fails by chance, drive the code with a scripted failure and assert on the outcome. Real-FS tests then check only what must hold whatever the host does (`DisplayPreferencesStore` tests, f178a65).

### Tokens-only scaffolds and the docked-card trap

`PanelScaffold` uses design tokens only, proven by 84 Play Mode cases (8 classes x 100/150/200 %). Its opaque full-screen root does not fit the docked machine card, which must never cover machines, so T534 needs a variant or class-only reuse (P2-F16).

### Housekeeping that bites again

Stop workers that wait on background work before QA; resume rate-limited workers with SendMessage; when `git branch -d` fails from a stale main checkout, use a guarded `update-ref` after an ancestor check; the owner's open editor and a handed-over `Library/` (5 sequential Issues) are part of the lease picture.

## P2 batch: carried follow-ups

`reviews/p2-followups.md` (P2-F1 to F21). Notable: the K4 tail sprites still need a runtime swap and a `KeepOutOf` change to `BadgeExtentBox` (P2-F5, T526); the composite C8 proof is open (P2-F6, P2-F7); call `Refresh` every frame (P2-F10, T529); the "Show zoom buttons" copy needs a World Designer read (P2-F20, T547).

## P2 batch: Glossary additions

- **Flash matrix**: per-family count of flashes per second with the FX, checked against the cap.
- **K4 tail**: the 4 px pointer tail of the OD-12 badge anchor; `BadgeExtentBox` is 23x27.
- **display-v2.json**: the strict, atomically written display preferences file with the Show zoom buttons toggle.

---

## P3 batch A: Key Decisions

### 13. Find the cause by the artifact's size, not by guessing (T552)

**What we did**: An Android build hung on load. The bisect ended at a 2 MiB atlas page (the F004 `machines` atlas, present since T440). The fix is an empirical rule: every atlas page is at most 512 x 512 RGBA32, enforced by `AtlasPageBudgetTests`, plus an `isLoading:false` gate in the journey.

**Why**: The engine-side reason is unknown, so a recorded constant with a test is cheaper and safer than an unproven theory. The hang had been present for several merges and only surfaced at the gate.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Keep bisecting into Unity internals | No repository fix would result; the cost is open-ended |
| Trust the green Windows suites | They load the same data in about 12 s, so they could never see it |

**When you'd choose differently**: If the cause were in your own code, fix it there and drop the blanket cap.

---

### 14. Make the hub a registered view, not a new navigation system (T535)

**What we did**: The Office hub is one more registered panel on the existing `ShellNavigationStack`; opening a system view from it replaces the hub, so one Back returns to the factory.

**Why**: One Back path means one set of tests and no second place for focus bugs. The six-tab bar stays until T537, so no panel test had to migrate.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| A separate hub navigator | Two back stacks that must agree |
| Remove the tab bar now | Forces a mass test migration in one Issue |

**When you'd choose differently**: When the old entry cannot coexist with the new one for layout reasons.

---

### 15. Gate a batch on the review, then fix first (T543)

**What we did**: The batch-A UI/UX review was REJECT (load hang, empty tap), the owner chose "fix first", and the re-run was ACCEPT_WITH_NOTES. The Play-Tester gate must precede batch B.

**Why**: A rejected gate that is waved through puts unknown defects under the next batch.

**When you'd choose differently**: For a cosmetic-only finding, carry it as a numbered item instead of blocking.

---

## P3 batch A: Concepts to Know

### Synthetic input can fake a defect

A scan-code-0 Escape is ignored by the Input System, so "Escape does not close" (A-F1) was a test artifact. Confirm a finding with the real input path before filing it.

### A shared checkout is shared state

A reviewer's `git checkout --detach` hit a worktree QA was using, and `git worktree remove` failed with the shell inside it. Give every role its own worktree and leave it before removing it.

## P3 batch A: carried follow-ups

`reviews/p3a-followups.md` (A-1 to A-13): A-5 specific cause on the card, A-6 Details scroll and landscape coverage, A-7 zoom-button hiding on touch, A-9 the 82 MB APK, A-10 SetPass re-measure, A-13 scene catalog rows and the C8 proofs. R-7 (no Android accessibility nodes) is deferred to a later feature by the owner.

## P3 batch A: Glossary additions

- **Details view**: the machine panel, reached from the docked card's `Details ›`.
- **ViewBounds**: the camera extent covering content, depot and deposit, used for pan limits.
- **Atlas page cap**: at most 512 x 512 RGBA32 (1 MiB) per page.

---

# Batch 5: persistent layer, world deck, effects, build card, stages, gates B and C (T527-T529, T536, T537, T539, T540, T555-T559)

## Batch 5: Key Decisions

### 16. Never relax a binding owner condition to make a fix pass (T537)

**What we did**: The first T537 commit relaxed the OD-9b C1 assertion (the whole factory in view at fit-all) for 640x360 at 200 % text. QA passed with a warning and the parent sent a fix round instead of waiving it. The zoom column folded into the action column, and the world share rose from 27.7 % to 36.0 %.

**Why**: A binding condition belongs to the owner. A parent can change the design to meet it, not the test to hide a miss.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Keep the relaxed assertion and record it | It silently changes an owner decision |
| Ask the owner to waive | The layout could meet the condition at modest cost |

**When you'd choose differently**: If no layout can meet the condition, escalate it as an owner decision with numbers.

---

### 17. Change the stage formula when the data says a state is unreachable (T540)

**What we did**: Review showed that `floor(3u/t)` never gave Early on Iron, which opens 4 of 12 locations at level 1. The owner chose an offset by the start: `stage = clamp(floor(3(u-u0)/(t-u0)), 0, 2)`, with u0 the level-1 location count. Iron now runs Early, Early, Partial, Established, Established.

**Why**: A stage the player can never see is dead art. The offset keeps the formula content-driven, with no per-industry table.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Keep the formula, accept no Early on Iron | The Early deck art would never show |
| Level thresholds 6 and 10 | A second table that content authors must keep in step |

**When you'd choose differently**: If industries needed unequal stage pacing, a per-industry table would be justified.

---

### 18. Fix the framing, then accept what cannot fit, and write it down (T558)

**What we did**: Gate C showed the closed camera clipping the depot at level 2 in portrait and on tablets, and a stale view after rotating under a card. T558 put the depot column into the fit extent and re-fits when a card closes on another screen. Where the depot cannot fit at the minimum scale it stays reachable by panning (AR-4), and the phone portrait 1080x2400 fit-all sits at the minimum scale (AR-3). The owner accepted both on 2026-11-19 and Art Bible v1.1.1 records them.

**Why**: A documented accepted risk with a measured scope is better than a framing rule that quietly breaks the pixel grid.

**When you'd choose differently**: If the final gate finds players do not discover the depot, revisit AR-4 in T542 with an off-screen indicator.

---

### 19. Build the observable path, not just the unit (T539, T557)

**What we did**: The T539 build card passed its unit tests but was inert in the shipped scene because its SerializeFields were unwired. The marker view from T532 was never instantiated, so world attention markers never showed until T557. Both now have real-scene tests.

**Why**: A test that builds the object itself cannot see that production never builds it.

**When you'd choose differently**: For a pure domain type, a unit test is enough; for anything a scene must own, test the scene.

---

### 20. Key rebuilds on what changed, and test the busiest producer (T529)

**What we did**: The world feed first rebuilt the whole factory snapshot up to 10 times a second. After a fix it still rebuilt every frame because an active contract bumps `Contracts.Version` on every produced unit, and that moves `StructureRevision`. Final rule: rebuild at once only on machine identity, layout, offline return and session replace; otherwise at most once per second. Two 600-frame zero-allocation tests pin it.

**Why**: A revision counter that moves with production is not a structure signal.

**When you'd choose differently**: If a counter changed only on player actions, it could drive the rebuild directly.

---

### 21. Close a safety gap with a bounded background write (T559)

**What we did**: Gate C found that a force-stop lost the whole run because nothing saved on a timer. T559 adds `AutosavePolicy` (30 s, 2 s spacing after a structural change, backoff after a failure). The main thread captures and serializes (about 0.12 ms); a pool thread publishes the file (about 30 ms) under the store's operation gate, with one write in flight and every other persistence access joining it first.

**Why**: Serialization is cheap and file IO is not. Moving only the IO keeps the frame time and the Save V3 format unchanged.

**Alternatives considered**:
| Approach | Why it wasn't chosen |
|----------|---------------------|
| Synchronous write on the main thread (33 ms) | A visible hitch twice a minute |
| A new save format for deltas | A migration for a problem the generation protocol already solves |

**When you'd choose differently**: If the write rate proves too high on the emulator (C-66), lengthen the interval or write only on structural change.

---

## Batch 5: Concepts to Know

### A tester can be wrong, and the device decides

C-F3 (store-full markers per machine, uncapped) and C-F4 (the marker icon selects nothing) were refuted by UI/UX: the per-machine pins were slot badges, not markers, and the badge was never a tap target. A gate finding is a claim to verify against the code and the device.

### A transient overlay must not change the layout

A milestone banner placed inside the unobstructed rectangle reframed the camera and covered the top machine row. Draw it as an absolute overlay and show only the main line on compact screens.

### Gate evidence without host input

Owner decision 2026-11-17: gate agents never drive the host mouse or keyboard. Player sessions run on a dedicated emulator through `adb`, and what could not be run is marked "not covered" (Windows player, TalkBack).

### A locked input gives no shortage (R09, C-25)

A resource the player cannot yet obtain raises no "Short on" signal. The wiki records the note because `research.md` is a source and the ingest does not edit it.

## Batch 5: carried follow-ups

`reviews/p3b-followups.md`: B-1 to B-17 and C-1 to C-67. Open for the owner: Art Bible 21.12 (the renderer census of 105 against the 95 signed off), C-61 the hard deck edge, C-67 the autosave interval as a run setting. Later tasks: T542 (soft-disabled audit, presenter tests, marker invalidation on layout change, C-63 hit area), T544 (autosave write rate on the emulator), T546 (shuffled trial, stage props), T547 (cause copy, accessibility record, bootstrap size).

## Batch 5: Glossary additions

- **u0**: the number of unlocked locations at factory level 1, the offset in the stage formula.
- **Status band**: the one-row strip at the top of the world column in phone landscape while a card or sheet is open, keeping Cash and Overdrive visible.
- **AR-3 / AR-4**: accepted risks in Art Bible v1.1.1 (no zoom-out on phone portrait 1080x2400; the depot reached by panning where its column cannot fit).
- **AutosavePolicy**: the Application-layer rule for periodic checkpoints (30 s interval, 2 s spacing, failure backoff).
- **IRunCheckpointBackgroundPromoter**: the optional port that lets the store publish a checkpoint on a pool thread.
- **StructureRevision**: the revision that triggers a world-feed rebuild; it must not move with production.
