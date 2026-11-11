# Implementation Plan: World-First Factory Experience

**Branch**: `feat/006-world-first-factory-experience` | **Date**: 2026-11-11 (revision 2 after plan reviews) | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/006-world-first-factory-experience/spec.md`. Includes the clarifications of 2026-11-11 and the plan-review owner answers. The review record is in [reviews/plan-reviews.md](reviews/plan-reviews.md).

## Summary

Feature 006 makes the factory the screen. Gameplay, content and saves stay unchanged. The work runs in five phases (research R01).

| Phase | Gate | What happens |
|---|---|---|
| **P0** | Owner approves World Bible v0.1 and the direction (OD-9) | Baseline probes on the current build (R02). A minimal World Bible v0.1 for a neutral starter site (R22). Composited pixel-art mockups of at least two directions, with animation strips (R03). |
| **P1** | Enabling refactor, no behaviour change | Pure-move split of the slot-grid view (R05). A `ShellTab` shim and extraction of the navigation and sheet logic (R11). A `FactoryWorldLayout` seam with exact camera parity (R04). The new sorting layers (R06). A session-owned, non-persisted `CompletedCycles` read value (R08, OD-11). A `WorldCompositionSettings` asset (R22). |
| **P2** | World art first (owner) | Art Bible v1.1, additive, with an explicit amendment list (R17). A rectangular-grid Tilemap deck that replaces the F004 floor (R06). A central depot with row trunks and per-machine branches (R07). An ore-vein marker (R23). Activity and effects capped by flash rate (R12). |
| **P3** | Interaction and UX | Presentation, attention and "short on" models (R08, R09). A docked machine card (R10). A labelled hub that replaces the six tabs, with one back stack (R11). A contextual build card (R13). Milestones and stages (R14, R15). Preferences v2 (R20). |
| **P4** | Verification and owner acceptance | B4 against the frozen F004 budgets (R16), trials (R18), and owner acceptance. |

## Technical Context

**Language/Version**: C# for Unity `6000.6.0f1` (.NET Standard 2.1). Python, as pinned in `tools/art-pipeline/pyproject.toml`, for the art pipeline and the mockup compositor.

**Primary Dependencies**: Existing only.

- URP 17.6.0 2D Renderer, UI Toolkit, Input System 1.20.0 and Unity Test Framework.
- `com.unity.modules.tilemap` 1.0.0, already in the manifest. F006 is its first runtime use, so the player and Android IL2CPP check apply (R06).
- Offline tools: Pillow, Blender 5.2.1 rig-v2, and ComfyUI for concept reference only.
- **No new package.**

**Storage**: No save or content change.

- `CompletedCycles` is session-only and lives in `RunSession` (R08).
- `display-v2.json` is added. The v1 file is read when v2 is absent (R20).
- `WorldCompositionSettings` is a presentation asset (R22).
- World Bible: `docs/world/world-bible-v0.1.md`.
- Art: sources in `art-source/f006/`, sprites in `Assets/IdleFactory/Art/F006/`.

**Testing**: Edit Mode, Play Mode, tagged `StandalonePlayer`, the emulator journeys (all retargeted, plus `-Scenario F006`), the art-pipeline checks, a manual real-input checklist, owner trials, and independent blind naming. Details are in R19.

**Target Platform**: Android ARM64 IL2CPP, with emulator evidence at 1080x2400 and 420 dpi, plus the Windows Standalone player. This is the owner's acceptance basis.

**Project Type**: A single Unity 2D game repository with offline art tooling.

**Performance Goals**: The F004 frozen budgets on W006-V:

- 0 steady-state allocations;
- at most 8 MB texture memory;
- atlas pages at most 1024²;
- SetPass recorded and held, with the delta reported per atlas;
- at most 490 shader variants;
- APK at most 150 MB;
- Windows frame-time p95 within the W003 range.

Emulator FPS remains exploratory.

**Constraints**:

- FR-001 and FR-002.
- Integer scales, D24, the 70×54 lattice, and a palette cap of 48.
- One camera writer. Framing stays selection-driven.
- Docked UI only. There are no per-machine UI Toolkit elements and no per-item simulation.
- Scene, prefab and TagManager changes go only through Unity tooling.
- Resource leases.

**Scale/Scope**:

- 12 shipped locations, plus test layouts of 6, 20 and an L shape.
- 9 new art families.
- 3 new UI surfaces and about 8 restyled ones.
- 1 additive read value.
- About 50 test files re-baselined, with the list in R19.

## Constitution Check

*GATE: passed before Phase 0 and rechecked after Phase 1 design and after the plan reviews.*

| Principle | Evidence | Result |
|---|---|---|
| I. Correct observable behavior | Each FR maps to a design and a named test (traceability table below). Guidance criteria are measured against a recorded baseline. Shortage wording reflects what the data shows (G-2). | PASS |
| II. Domain architecture, explicit state | Ownership table in the data model. `CompletedCycles` is owned by `RunSession` and committed only on a successful advance (A-1). The domain never reads presentation state. | PASS |
| III. Deterministic, data-driven | Layout, attention, stage, link geometry and prop placement are pure functions of authored data and snapshots. Composition values live in an owned asset (R22). A no-observer-effect test covers the counter. | PASS |
| IV. Tests protect behavior | Contract tests per rule. Explicit re-baseline list. Property test for the counter. 0-test guard. | PASS |
| V. Simplicity | Concrete layout class, one card, a reused registry and presenters, an installed module, and a shim instead of a big-bang rewrite. | PASS (see Complexity Tracking) |
| VI. Measured performance | W006-V, B4a/B4b, frozen budgets, allocation gates, SetPass per atlas. | PASS |
| VII. Human authority | Owner decisions OD-1 to OD-8 and OD-13 to OD-16. Pending: OD-9 to OD-12 at their gates. | PASS |
| VIII. Traceability | FR/SC → research → contract → test (table below). | PASS |
| IX. Independent review | Seven plan-review lenses, including the new World Designer. Fresh-session reviewers, and producers never review their own work. | PASS |
| X. Safe delivery, durable knowledge | P1 is pure moves and shims. The preferences reader is backward-compatible. Provenance is kept. World Bible and Art Bible are versioned. Wiki ingest follows. | PASS |

## Owner decisions

| ID | Decision (2026-11-11) | Source |
|---|---|---|
| OD-1 | A hub replaces the six-tab bar. It is not named "Systems". | Spec clarification |
| OD-2 | Machines link to a central store. Machines do not link to each other. | Spec clarification |
| OD-3 | Development stages come from factory level, with machine levels and machine count as secondary inputs. Stages reset with prestige. | Spec clarification |
| OD-4 | The direction is approved from rendered mockups only. | Spec clarification |
| OD-5 | Mining and Overdrive stay on screen. Zoom buttons are hidden on touch, with an accessible path to show them. | Spec clarification |
| OD-6 | Proposed thresholds are guidance. Binding criteria: SC-005, SC-007, SC-008, SC-009, SC-011, SC-012 and SC-013, plus owner acceptance. | Spec clarification |
| OD-7 | Acceptance runs on the emulator and Windows. A physical phone is optional. | Spec clarification |
| OD-8 | One feature, art first. | Spec clarification |
| OD-13 | The hub entry has an icon and a short visible text label. | Plan review (U-1) |
| OD-14 | The machine card in phone portrait may grow to at most 45 % (hard cap) of the safe height at large text. | Plan review (U-2) |
| OD-15 | World Bible v0.1 is minimal: a neutral starter industrial platform on a dark base, with swappable ground. | Plan review (W-1, W-5) |
| OD-16 | An ore-vein marker appears in the world. It is not an input. | Plan review (W-3) |
| OD-9a | *Pending at P0 (T504)*: approval of the World Bible v0.1 text. | FR-047 |
| OD-9b | *Pending at P0 (T509)*: the direction choice and the hub label wording. | FR-040 |
| OD-10 | *Pending at P2*: Art Bible v1.1 sign-off, including the amendment list, the world-layer budget and the texture table. | R17 |
| OD-11 | **Approved by the owner 2026-11-11 (T500)**: the revised `CompletedCycles` design (session-owned, +1 per cycle, committed on success, never saved). | R08 |
| OD-12 | *Pending at P2*: the A-03 badge anchor, chosen from measured candidates. | R17 |

## Implementation lane (T500, 2026-11-11)

- **Lane: MAQA.** `$speckit-maqa-sync` creates one Issue for each non-parent task. Workers run in isolated worktrees, and QA is independent. The parent merges only through `scripts/maqa-merge.ps1`. The direct lane is not used.
- **Gates verified on `main` `2ae1eee`** (PR #677 merged):
  - the constitution has no placeholders;
  - `.specify/branch-convention.yml` is present;
  - the wiki is initialised;
  - the labels `maqa`, `maqa:in-progress`, `maqa:in-review`, `maqa:blocked` and `resource:unity|emulator|gpu` exist;
  - `maqa-config.yml` holds real Unity test commands;
  - `$speckit-analyze` has no open CRITICAL finding;
  - the orchestration ledger validates (52/52).
- **Activated roles:** Game Designer/PO, Software Architect, Test/QA, UI/UX, Art Director, Technical Artist, 3D and Render Artist, Asset Artist, Gameplay and UI/UX Developers, Performance Engineer, Play-Tester (per P3 batch) and World Designer.
- **Low-tier lenses:** Game Systems/Economy (R09, R15, R23), Security/Privacy (R20) and DevOps/Operations (T513, T541).
- **Omitted:** Sound Designer and Composer (audio is out of scope, A-8), and Online Services (no remote boundary).
- **OD-11** was confirmed by the owner, which unblocks T514.

## Feature 004 and 005 dependencies

| Dependency | Status | Use in F006 |
|---|---|---|
| F004 complete on `main` | Done | Baseline art, clock, camera, cues and budgets |
| Art Bible v1.0.4 | Signed | Extended to v1.1 (R17) |
| `FactoryCameraController.OpenSheet/CloseSheet`; selection-driven framing (bootstrap:1310-1333) | Exists | Docked card and build card (R10, R13) |
| `ShellPanelRegistry` and the existing panels | Exist | Mounted from the hub unchanged |
| `DisplayPreferencesStore` v1 | Exists | Extended to v2 (R20) |
| #555 (`StartupStatusPanel` reduced motion) | Open | Fixed in P3 |
| F004 minors G3, U-3, U-4, U-7, A-03 | Accepted | Resolved by R10, R11 and R17. G3 is re-measured. |
| World Designer role (`01c9001`) | On `main` | World Bible, composition and copy (R22) |

## Design

### Components

All new code lives in `Assets/IdleFactory/Unity/` unless noted otherwise.

| Component | Responsibility | Writes |
|---|---|---|
| `Presentation/World/FactoryWorldLayout`, `LatticeLayoutBuilder`, `BuildingLocationId` | Bounds (= today's Extent), margin, anchors, footprints, `TryHit`, sort keys, deck cells, depot, trunks and branches, deposit cell | Nothing (immutable) |
| `Presentation/World/WorldCompositionSettings` (ScriptableObject) | Margin, depot and deposit rules, prop weights per stage, hatch and edge rules | Edited through Unity tooling only |
| `Presentation/CameraScaleModel`, `RunCameraFraming` | Consume `Bounds`, `FootprintOf` and `LayoutId` instead of `columns` | Nothing (pure) |
| `Presentation/FactoryCameraController` | Sole camera writer; selection-driven docked framing | The camera |
| `Presentation/World/SlotGridView` (split) + `SlotRenderers` | Machines, flat foundations, cues and markers per location | Its own renderers |
| `Presentation/World/FactoryDeckView` | Rectangle-grid Tilemap deck, hatch with padlock and digit, edges, props per stage | Its own Tilemaps |
| `Presentation/World/DepotLinkView` | Depot (12 sprites), trunks, branches, branch pips | Its own renderers |
| `Presentation/World/DepositView` | Ore vein and its mining activity | Its own renderer |
| `Presentation/World/EffectPool` | 8 renderers (3 active + 5 draining), coalescing, flash-capped pace | Its own renderers |
| `Presentation/World/MachinePresentationModel`, `AttentionModel`, `DevelopmentStage`, `MilestoneWatcher` | Derived models refreshed on revision change | Their own reused buffers |
| `UI/ShellNavigationStack`, `UI/ShellSheetHost` | Back order, focus return, docking bands (extracted from `RunShellDocument`) | Their own state |
| `UI/MachineCard`, `UI/Panels/BuildCard`, `UI/Panels/HubPanel`, `UI/PanelScaffold`, `UI/MilestoneBanner` | New surfaces | Their own UI Toolkit subtrees |
| `UI/Panels/MachinePanelPresenter` (reorganised) | Details view | Its own UI Toolkit subtree |
| `UI/RunShellDocument` (slimmed, `ShellTab` shim) | Persistent layer: Cash, store, labelled hub entry, production attention, mining, Overdrive, profile-gated zoom | Its UI Toolkit tree |
| `Persistence/DisplayPreferencesStore` (v2) | Reduced motion and zoom buttons | `display-v2.json` |
| Core `RunSimulator` (per-advance counts), `RunSession` (accumulation), `MachineStateView.CompletedCycles` | Exact completions | `RunSession` only |
| `tools/art-pipeline` `mockup` | P0 compositor (shared geometry JSON) | `reviews/direction/` |
| `docs/world/world-bible-v0.1.md` | Setting, naming and copy rules | World Designer |

### Data flow

1. **Advance.** The runtime host advances `RunSession`. On a successful commit, the per-advance completion counts accumulate in the session.
2. **Refresh.** On a revision change, the derived models read `CopyMachineStates` and the snapshot into reused buffers.
3. **Render.** World views render from the models and the layout. The clock drives loops, pips and effects without allocating.
4. **Interact.** UI reads the models through presenters and mutates only through `RunCommands`.
5. **Frame.** A selection change docks the card and calls `OpenSheet(band, footprint)`. Close calls `CloseSheet`.

### Art and technical art

Families, routes, the amendment list, the world atlas and the texture table are covered in R17. The deck uses a Rectangle `Grid` with 70×54 art-px cells and the slot sprite material. The new sorting layers are created through Unity tooling in P1.

### Performance

Covered in R16. Allocation gates cover the clock tick, the model refresh, effect spawn and coalescing, card docking during camera moves, hub open and close, and the preferences read.

### Test strategy

Covered in R19: new tests, StandalonePlayer tags, the 0-test guard, the per-file re-baseline list, and the emulator retargeting.

### Requirement traceability

| Requirement | Design | Contract | Evidence |
|---|---|---|---|
| FR-001–FR-003 | R08, data-model ownership | presentation-model §1 | No-observer-effect test, API fixture diff, save and trace SHA, SC-007 |
| FR-004, FR-044 | R17 | world-presentation §5 | Pipeline checks, Art Director and Technical Art reviews, OD-10 |
| FR-005, FR-024 | R10, R11, R19 | ui-surfaces §4 | Layout assertions on every class at 100/150/200 %, keyboard traversal, SC-009 |
| FR-006 | R12, R16 | presentation-model §4 | Allocation gates, B4a/B4b, SC-008 |
| FR-007–FR-010, FR-008a | R11, R20 | ui-surfaces §1 | Geometry probe, zoom matrix, preferences v2 tests |
| FR-010a | R23 | world-presentation §2a | `DepositViewTests`, Game Systems no-pacing check |
| FR-011–FR-015 | R06, R07, R17, R22 | world-presentation §1–§3 | Deck, link and composition tests, SC-005, reduced-motion capture |
| FR-016–FR-019 | R10 | ui-surfaces §2 | Card tests (Upgrade visible at 200 %), SC-002 and SC-003 probe |
| FR-020–FR-023 | R11 | ui-surfaces §3 | Hub, back-order and consistency tests |
| FR-025–FR-031 | R08, R09, R12 | presentation-model §1–§3 | Model and attention tests, SC-004, CVD sheets |
| FR-032–FR-035 | R13, R14 | ui-surfaces §5, presentation-model §5 | Build card and milestone tests |
| FR-036 | R15 | presentation-model §6 | Stage tests, triptych delta test, SC-006 |
| FR-037–FR-039 | R04, R05 | world-presentation §4 | Parity, independence and guard tests, SC-011 |
| FR-040–FR-043, FR-046 | R02, R03, R18 | world-presentation §6 | `reviews/baseline/`, `direction.md`, `final-acceptance.md` |
| FR-045 | Review waves | — | UI/UX and Play-Tester reports per batch, manual real-input checklist |
| FR-047 | R22 | world-presentation §7 | World Bible v0.1 approval, `world-copy.md`, SC-013 |

### Review waves and role disposition

| Wave | Roles | Record |
|---|---|---|
| Discovery | PO, UI/UX, Art Director, Architect (+Technical Art, Performance) | `reviews/discovery.md` |
| Plan review (done) | Architect, Test/QA, UI/UX, Art Director + Technical Art, World Designer (Medium); Game Systems + Security (Low) | `reviews/plan-reviews.md` |
| P0 | World Designer (World Bible author) and Art Director (direction briefs); independent UI/UX and Art Director mockup reviewers; owner | `reviews/direction.md` |
| P1 | Architect and Test/QA per Issue; DevOps (Low) for the TagManager change | QA reports |
| P2, per family | Art Director (fresh session), Technical Art, World Designer conformance, UI/UX (cue legibility), PO; owner signs v1.1 | Art reviews, `world-bible-conformance.md` |
| P3, per batch | Test/QA, UI/UX Reviewer, Architect, PO, Play-Tester (owner reviews the report), World Designer for copy | QA and Play-Tester reports |
| P4 | All mandatory lenses plus owner acceptance | `final-acceptance.md` |

**Omitted:** Sound Designer (audio is out of scope).

### Proposed work breakdown (input to `$speckit-tasks`)

Task IDs start at **T500**; GitHub has no task IDs above T476.

1. **P0**
   - Geometry probe with its self-test, and the baseline captures (R02).
   - World Bible v0.1 (R22).
   - World copy list.
   - Mockup compositor reading the shared geometry JSON.
   - Direction briefs that cite World Bible v0.1.
   - Concept sprites.
   - Mockup sheets and animation strips for at least two directions, including 640x360 at 200 %.
   - Independent UI/UX and Art Director reviews.
   - Owner decision OD-9.
2. **P1**
   - Pure-move split of the slot-grid view (R05).
   - Extraction of the navigation and sheet logic, with the `ShellTab` shim (R11).
   - `FactoryWorldLayout`, the builder and the test layouts; camera and framing migration with `LargestFittingScale` parity (R04).
   - `WorldCompositionSettings` (R22).
   - Sorting layers through Unity tooling, with the guard test and the DevOps note (R06).
   - `CompletedCycles` with its lifecycle, property and no-observer-effect tests and the API fixture diff (R08).
   - Test re-baseline record.
3. **P2**
   - Art Bible v1.1 draft: amendments, budget and texture table.
   - A-03 measurement and owner decision OD-12.
   - Family production: ground, foundation, depot, link, prop, edge, deposit, FX.
   - Per-family reviews, including World Designer conformance.
   - Owner sign-off OD-10.
   - Tilemap deck that replaces the floor.
   - Depot and link view.
   - Deposit view.
   - Effect pool.
   - SC-005 re-run.
   - B4a.
4. **P3**
   - Presentation model.
   - Attention and "short on" model.
   - Machine card and details.
   - Hub with the panel scaffold and attention feeds.
   - Persistent layer slimmed, `ShellTab` shim retired from the UXML.
   - Preferences v2.
   - Build card.
   - Milestones and stages.
   - #555.
   - Consistency pass over every system view, the offline summary, notices and confirmations.
   - Emulator scenarios retargeted, plus F006.
   - Manual real-input checklist.
   - Play-Tester per batch.
   - B4b.
5. **P4**
   - Full suites and `-StandaloneWindowsPlayer`.
   - SC-004 and SC-006 trials with seeds and oracles.
   - SC-013 World Designer checklist.
   - Final matrix.
   - Final reviews and owner acceptance.
   - `after_implement` hooks and converge.

## Project Structure

### Documentation (this feature)

```text
specs/006-world-first-factory-experience/
├── spec.md, plan.md, research.md (R01–R23), data-model.md, quickstart.md
├── contracts/ world-presentation.md, presentation-model.md, ui-surfaces-f006.md
├── checklists/requirements.md
├── reviews/ discovery.md, plan-reviews.md (+ baseline/, direction/, test-rebaseline.md, world-copy.md, world-bible-conformance.md, final-acceptance.md later)
└── tasks.md                # $speckit-tasks
docs/world/world-bible-v0.1.md   # P0, World Designer
```

### Source code (repository root)

```text
Assets/IdleFactory/
├── Core/Run/Simulation/RunSimulator.cs        # per-advance completion counts (committed on success)
├── Core/Run/Application/RunSession.cs         # accumulates CompletedCycles per InstanceId
├── Core/Run/Application/MachineStateView.cs   # + CompletedCycles (ctor parameter with default)
├── Unity/Presentation/CameraScaleModel.cs, RunCameraFraming.cs   # consume layout Bounds/LayoutId
├── Unity/Presentation/World/                  # split slot-grid files + layout, composition, deck, depot/link, deposit, effects, models
├── Unity/UI/ShellNavigationStack.cs, ShellSheetHost.cs, MachineCard.*, PanelScaffold.*, MilestoneBanner.*
├── Unity/UI/Panels/HubPanel.*, BuildCard.*, MachinePanelPresenter.cs
├── Unity/UI/FactoryRun.uxml/.uss, RunShellDocument.cs
├── Unity/Persistence/DisplayPreferencesStore.cs   # v2
├── Art/F006/                                   # world atlas + FX atlas + WorldCompositionSettings.asset
├── Tests/EditMode/F006/, Tests/PlayMode/F006/  # TestLayouts builder, probes
ProjectSettings/TagManager.asset                # sorting layers (Unity tooling, recorded noise exception)
art-source/f006/, tools/art-pipeline/ (mockup, F006 trials)
docs/world/
```

**Structure Decision**: The existing assemblies are kept and no new asmdef is added (verified by the Architect). New presentation code goes in `Presentation/World/`.

## Complexity Tracking

| Item | Why needed | Simpler alternative rejected because |
|---|---|---|
| Additive Core read value `CompletedCycles`: the simulator emits per-advance counts and `RunSession` accumulates them | FR-029 requires exact, never-overstated completion feedback. The simulator is stateless across advances (fact), so the session must own accumulation. | A progress-wrap derivation is lossy. An event stream allocates. A counter inside the simulator is not possible. |
| Tilemap as a second world-rendering path | A batched static deck that scales to N locations within the SetPass budget, and suits future grid building | A pooled sprite floor (TA-2) is the problem being fixed. A baked background does not scale. |
| `ShellTab` shim during the migration | 209 references in 45 files would otherwise break in one change (A-5) | A big-bang removal produces an unreviewable diff and risks regressions across about 35 test files. |
