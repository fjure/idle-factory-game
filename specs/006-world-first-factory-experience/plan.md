# Implementation Plan: World-First Factory Experience

**Branch**: `feat/006-world-first-factory-experience` | **Date**: 2026-11-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/006-world-first-factory-experience/spec.md`, with clarifications from session 2026-11-11.

## Summary

Feature 006 makes the factory the screen and keeps gameplay, content and saves unchanged. The work runs in five phases (research R01):

| Phase | Owner decision or gate | Content |
|---|---|---|
| **P0** | Owner direction decision | Baseline from the current build (R02). Composited pixel-art mockups of at least two directions (R03). |
| **P1** | Enabling refactor, no behaviour change | Split the 1155-line slot-grid view (R05). Introduce a `FactoryWorldLayout` seam that removes the hard-coded 12 slots from presentation (R04). Unify the back stack (R11). |
| **P2** | World art first (owner) | Art Bible v1.1 additive families (R17). A Tilemap platform deck that grows with unlocked locations (R06). A central depot with honest links to every machine (R07). Activity and effect art on the shared clock (R12). |
| **P3** | Interaction and UX | A derived machine presentation model with one additive, non-persisted Core counter for exact completions (R08). A shared-cause and bottleneck attention model (R09). A single reusable machine card with details (R10). A hub that replaces the six-tab bar (R11). A contextual build flow (R13). Milestones (R14) and development stages (R15). |
| **P4** | Verification and owner acceptance | B4 performance against the frozen F004 budgets (R16), readability re-trials (R18), screenshot matrix, owner acceptance. |

## Technical Context

**Language/Version**: C# for Unity `6000.6.0f1` (.NET Standard 2.1). Python (pinned in `tools/art-pipeline/pyproject.toml`) for the art pipeline and the new mockup compositor.

**Primary Dependencies**: Existing only. URP 17.6.0 2D Renderer, UI Toolkit, Input System 1.20.0, Unity Test Framework, `com.unity.modules.tilemap` 1.0.0 (already in `Packages/manifest.json`; first runtime use). Offline: Pillow (`uv.lock`), Blender 5.2.1, and ComfyUI per the F004 tool pins. **No new package.**

**Storage**: No save or content change.
- `MachineStateView.CompletedCycles` is a session-only read model (R08).
- The display preferences move from `display-v1.json` to v2, adding `showZoomButtons`. The reader accepts v1 (R20).
- Art sources go in `art-source/f006/`, sprites in `Assets/IdleFactory/Art/F006/`.

**Testing**: Edit Mode, Play Mode, tagged `StandalonePlayer`, the `-Scenario F006` emulator journey, and the art-pipeline checks (`scripts/test-art-pipeline.ps1`). Owner timed trials and independent blind naming come from `tools/art-pipeline/trials/`. Details are in research R19.

**Target Platform**: Android ARM64 IL2CPP, with emulator evidence from the API 36 AVD at 1080x2400 and 420 dpi, plus the Windows Standalone player. This is the owner's acceptance basis (A-9). iOS stays deferred.

**Project Type**: A single Unity 2D game repository with an offline art-tooling folder.

**Performance Goals**: The F004 frozen budgets apply unchanged on W006-V (R16):
- 0 steady-state GC allocations per frame;
- texture memory ≤ 8 MB;
- atlas pages ≤ 1024²;
- SetPass recorded and held;
- shader variants ≤ 490;
- APK ≤ 150 MB;
- Windows frame-time p95 within the W003 range.

Emulator FPS rows remain exploratory.

**Constraints**:
- No production, economy, content, unlock, offline, prestige or save change (FR-001).
- Presentation derives from authoritative state (FR-002).
- Integer scales, D24, isometric 2:1, palette cap 48 (FR-004).
- One camera writer.
- No per-machine UI Toolkit element and no per-item simulation.
- Scenes and prefabs change only through Unity tooling.
- Single Unity, emulator and GPU leases.

**Scale/Scope**:
- 12 authored locations shipped; test layouts of 6, 20 and an L shape.
- 4 machine families reused.
- About 8 new art families (R17).
- 3 new UI surfaces: card, hub and build card. About 8 existing panels restyled.
- 1 additive Core read-model field.

## Constitution Check

*GATE: passed before Phase 0 research and rechecked after Phase 1 design.*

| Principle | Evidence | Result |
|---|---|---|
| I. Correct observable behavior | Every FR maps to a design element and a named test (traceability below). Guidance thresholds are measured against a recorded baseline (FR-046) rather than assumed. | PASS |
| II. Domain architecture, explicit state | All new state is presentation-only, and each item has one owner and one writer (data-model). The one Core change is an additive read value with a single writer (the simulator). The domain never reads presentation. | PASS |
| III. Deterministic, data-driven | The layout, attention ranking, stage function, link paths and prop placement are pure functions of authored data and snapshots. Effects run on the integer-ms clock. | PASS |
| IV. Tests protect behavior | Edit, Play, Player and emulator coverage per contract. Re-baselined tests are listed with an old-to-new diff. Save and trace fixtures are proven byte-identical. | PASS |
| V. Simplicity | `FactoryWorldLayout` is a concrete class, not an interface. One card instance. The existing panels and presenters are reused. Tilemap is an installed module, not a new framework. | PASS (see Complexity Tracking) |
| VI. Measured performance | W006-V workload, B4a/B4b measurements, frozen budgets, allocation gates. | PASS |
| VII. Human authority | The owner decided navigation, connections, stages, the gate format, persistent controls, threshold status, devices and delivery order. The direction gate precedes production. The Core counter is listed for owner confirmation (OD-11). | PASS |
| VIII. Traceability | FR/SC → research → contract → test table below. Tasks, analysis and the ledger follow. | PASS |
| IX. Independent review | Review waves and role disposition below. Producers never review their own output, and trials use fresh sessions. | PASS |
| X. Safe delivery, durable knowledge | P1 is a pure move with green suites. The preferences reader stays backward-compatible. Every asset has provenance. The wiki ingests after the plan and after each batch. | PASS |

**Post-design recheck**: no violations. There are two Complexity Tracking entries.

## Owner decisions

| ID | Decision (2026-11-11) | Source |
|---|---|---|
| OD-1 | A hub entry replaces the six-tab bar. It is not named "Systems", and icon-only is allowed. | Clarification Q1 (spec) |
| OD-2 | Machines link to a central store. There are no links between machines. | Clarification Q2 |
| OD-3 | Development stages come from factory level, with machine levels and count as secondary. They reset with prestige. | Clarification Q3 |
| OD-4 | The direction is approved from rendered mockups only, which must show interaction states. | Clarification (learn) Q1 |
| OD-5 | Mining and Overdrive stay on screen. Zoom buttons are hidden on touch, with an accessible non-gesture path. | Clarification Q2 |
| OD-6 | The proposed thresholds are guidance. The binding criteria are SC-005/007/008/009/011/012 plus owner acceptance. | Clarification Q3 |
| OD-7 | Acceptance runs on the emulator and Windows. A physical phone is optional. | Clarification Q4 |
| OD-8 | One feature, with art first. | Clarification Q5 |
| OD-9 | *Pending owner*: the direction choice and the hub entry name or icon. | P0 gate (FR-040) |
| OD-10 | *Pending owner*: the Art Bible v1.1 sign-off, including the world-layer budget and the A-03 anchor fix. | P2 gate (R17) |
| OD-11 | *For owner confirmation at plan review*: the additive non-persisted `CompletedCycles` read value in Core (R08). | A-7 |

## Feature 004 and 005 dependencies

| Dependency | Status | Use in F006 |
|---|---|---|
| F004 complete (77/77, T447, T449) on `main` `1dab60d` | Done | Baseline art, clock, camera, cues, budgets |
| F004 Art Bible v1.0.4 | Signed | Extended to v1.1 (R17) |
| `FactoryCameraController.OpenSheet/CloseSheet` | Exists | Card and details framing (R10) |
| `ShellPanelRegistry` and the existing panels (T113–T118, F005 surfaces) | Exist | Mounted from the hub unchanged (R11) |
| Display preferences store (T432) | Exists | Extended to v2 (R20) |
| #555 (`StartupStatusPanel` reduced motion is not wired) | Open | Fixed in P3 as part of the reduced-motion coverage (FR-015, SC-009) |
| F004 known minors: G3 hit margin, U-3 landscape sheet, U-4 dimmed disabled state, U-7 gear label, A-03 badge overlap | Accepted | Resolved by the card and hub layout (U-3, U-7), the shared disabled state (U-4) and the R17 anchor fix (A-03). G3 is re-measured. |

## Design

### Components

All new code lives in `Assets/IdleFactory/Unity/` unless noted.

| Component | Responsibility | Writes |
|---|---|---|
| `Presentation/World/FactoryWorldLayout` + `LatticeLayoutBuilder` | Location anchors, footprints, hit shapes, sort keys, deck cells, depot anchor, link paths | Nothing (immutable) |
| `Presentation/World/SlotGridView` (split) + `SlotRenderers` | Per-location machine, foundation, cue and marker rendering | Its own renderers |
| `Presentation/World/FactoryDeckView` | Tilemap deck, base, edges, props and lamps per stage | Its own Tilemaps |
| `Presentation/World/DepotLinkView` | Depot fill states, link lanes and pips | Its own renderers |
| `Presentation/World/EffectPool` | Pooled completion, spark, steam, dust and glint effects, with a cap of 3 and coalescing | Its own renderers |
| `Presentation/World/MachinePresentationModel` | Derived visual state, upgrade availability, completion deltas and pace per machine | Its own reused buffer |
| `Presentation/World/AttentionModel` | Shared causes, bottleneck ranking, store-full attention and system attention flags | Its own reused buffer |
| `Presentation/World/DevelopmentStage` | Stage function (R15) | Nothing (pure) |
| `Presentation/World/MilestoneWatcher` | Level-up and prestige detection | A queue of pending milestones |
| `Presentation/FactoryCameraController` (extended) | Sole camera writer. Adds focus on select through the existing band API and consumes layout bounds. | The camera |
| `UI/MachineCard` | The single contextual card (R10) | Its UI Toolkit subtree |
| `UI/Panels/MachinePanelPresenter` (reorganised) | The Details view | Its UI Toolkit subtree |
| `UI/Panels/HubPanel` | System cards with attention (R11) | Its UI Toolkit subtree |
| `UI/Panels/BuildCard` (from `BuildSheetPresenter`) | Contextual build flow (R13) | Its UI Toolkit subtree, plus the preview through `SlotGridView` |
| `UI/ShellNavigationStack` | Back and Escape order and focus return | Its own stack |
| `UI/RunShellDocument` (slimmed) | The persistent layer: Cash, store, hub entry, attention, mining, Overdrive, zoom (profile-gated) | Its UI Toolkit tree |
| `UI/MilestoneBanner` | Non-blocking banner | Its UI Toolkit subtree |
| `Persistence/DisplayPreferencesStore` (v2) | Reduced motion and the zoom-button preference | `display-v2.json` |
| Core `RunSimulator` / `MachineStateView` | The additive `CompletedCycles` read value | The simulator only |
| `tools/art-pipeline` `mockup` command | P0 compositor (R03) | `specs/006-.../reviews/direction/` |

### Data flow

1. **Simulation.** `RunSession` advances in the runtime host and exposes its revision.
2. **Derived models.** On a revision change only, the derived models (`MachinePresentationModel`, `AttentionModel`, `DevelopmentStage`, `MilestoneWatcher`) read `CopyMachineStates` and the factory snapshot into reused buffers.
3. **Rendering.**
   - The world views render from the models and the layout.
   - The clock drives loops, pips and effects each frame without allocating.
   - The UI reads the models through the existing presenters and mutates only through `RunCommands`.
4. **Camera.** Selection changes call `FactoryCameraController.OpenSheet/CloseSheet` with the shell's unobstructed band.

There is no presentation-to-domain write path other than `RunCommands`.

### Art and technical art

The new families and their routes are listed in R17. They follow the F004 pipeline: brief, route, pixel pass, checks, provenance, Art Director review in a fresh session, then Technical Art import through the manifest into per-family atlases. The deck and links use Tilemap tile assets with the F004 import rules: point filtering, no compression on world atlases, and PPU 24.

### Performance

Measurement uses W006-V, B4a after P2 and B4b after P3, and the F004 method and leases (R16). Allocation gates cover the clock tick, model refresh on a revision change, effect spawn and coalesce, card reposition on camera move, and hub open and close.

### Test strategy

Covered by research R19. The contracts list the named test classes per rule. Re-baselined tests are recorded in `reviews/test-rebaseline.md`.

### Requirement traceability

| Requirement | Design | Contract | Evidence |
|---|---|---|---|
| FR-001, FR-002, FR-003 | R08 (additive read value only), data-model ownership | presentation-model.md §1 | Trace and save fixtures byte-identical (Edit Mode), SC-007 suites |
| FR-004, FR-044 | R17, F004 pipeline | world-presentation.md §5 | Art pipeline checks, AD and TA reviews |
| FR-005, FR-024 | R10, R11, R19 | ui-surfaces-f006.md §4 | Layout assertions on all classes at 100/150/200 %, SC-009 |
| FR-006 | R12, R16 | presentation-model.md §4 | Allocation gates, B4a/B4b, SC-008 |
| FR-007–FR-010, FR-008a | R11, R20 | ui-surfaces-f006.md §1 | Geometry probe (SC-001 guidance), preference v2 tests |
| FR-011–FR-015 | R06, R07, R17 | world-presentation.md §1–3 | Deck and link tests, SC-005 re-trial, reduced-motion capture |
| FR-016–FR-019 | R10 | ui-surfaces-f006.md §2 | Card Play Mode tests, SC-002/SC-003 probe |
| FR-020–FR-023 | R11 | ui-surfaces-f006.md §3 | Hub reachability and back-stack tests, consistency review |
| FR-025–FR-031 | R08, R09, R12 | presentation-model.md §1–3 | Model tests, SC-004 trial, CVD sheets |
| FR-032–FR-035 | R13, R14 | ui-surfaces-f006.md §5, presentation-model.md §5 | Build flow tests, milestone tests |
| FR-036 | R15 | presentation-model.md §6 | Stage function tests, SC-006 ordering trial |
| FR-037–FR-039 | R04, R05, spec future table | world-presentation.md §4 | US7 test layouts (SC-011) |
| FR-040–FR-043, FR-046 | R02, R03, R18 | world-presentation.md §6 | `reviews/baseline.md`, `reviews/direction.md`, `reviews/final-acceptance.md` |
| FR-045 | Review waves below | — | UI/UX and Play-Tester reports per batch |

### Review waves and role disposition

| Wave | Roles | Notes |
|---|---|---|
| Plan review (now) | Software Architect, Test/QA, UI/UX, Art Director (Medium) + Game Systems (R09 and R15 definitions) and Security/Privacy (R20 reader) (Low) | Record in `reviews/plan-reviews.md` |
| P0 direction | Art Director (author of the brief) + an independent UI/UX reviewer of the mockups, then the owner | Producers do not review their own mockups |
| P2 per family | Art Director (fresh session), Technical Art, UI/UX (cue legibility), PO (readability) | Owner sign-off of Art Bible v1.1 |
| P3 per batch | Test/QA, UI/UX Reviewer, Architect, PO, Play-Tester (owner reviews the report) | Performance on B4b |
| P4 final | All mandatory lenses plus the owner's acceptance | `reviews/final-acceptance.md` |

**Omitted roles:**
- DevOps: no build, pipeline or online change. Re-activated if settings-noise signatures need a URP or Graphics extension (F004 F2).
- Sound Designer: audio is out of scope (A-8).

### Proposed work breakdown (input to `$speckit-tasks`)

Task IDs start at **T500**. GitHub is free above T476 (checked 2026-11-11).

1. **P0**
   - T500 baseline captures and geometry, tap and label probes (R02);
   - T501 mockup compositor;
   - T502 direction briefs and concept sprites;
   - T503 mockup sheets for at least two directions;
   - T504 independent UI/UX and AD review of the mockups;
   - T505 owner direction decision (OD-9).
2. **P1**
   - slot-grid split, a pure move (R05);
   - `FactoryWorldLayout` with the lattice builder and test layouts, and camera, hit-test and floor migration (R04);
   - `ShellNavigationStack` replacing the `HandleBack` branches (R11);
   - test re-baseline record.
3. **P2**
   - Art Bible v1.1 draft and reviews, then owner sign-off (OD-10);
   - family production: ground, foundation, depot, link, prop, edge, FX;
   - AD and TA reviews per family;
   - Tilemap deck view;
   - depot and link view;
   - effect pool;
   - A-03 anchor fix;
   - SC-005 re-trial on the new backdrop;
   - B4a.
4. **P3**
   - Core `CompletedCycles` (OD-11);
   - `MachinePresentationModel`;
   - `AttentionModel`;
   - machine card and details;
   - hub panel and attention feeds;
   - persistent-layer slimming and display-preferences v2;
   - build card and preview;
   - milestones and stages;
   - #555 wiring;
   - consistency pass over Market, Contracts, Items, Research, Industry, Prestige, the offline summary, notices and confirmations;
   - `-Scenario F006` emulator journey;
   - Play-Tester per batch;
   - B4b.
5. **P4**
   - full suites and `-StandaloneWindowsPlayer`;
   - SC-004 and SC-006 trials;
   - final screenshot matrix;
   - final reviews;
   - owner acceptance;
   - after_implement hooks;
   - converge.

## Project Structure

### Documentation (this feature)

```text
specs/006-world-first-factory-experience/
├── spec.md
├── plan.md                 # this file
├── research.md             # R01–R21
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── world-presentation.md
│   ├── presentation-model.md
│   └── ui-surfaces-f006.md
├── checklists/requirements.md
├── reviews/                # discovery.md now; baseline, direction, plan-reviews, test-rebaseline, final-acceptance later
└── tasks.md                # $speckit-tasks (not created by this command)
```

### Source code (repository root)

```text
Assets/IdleFactory/
├── Core/Run/Application/MachineStateView.cs          # + CompletedCycles (read only)
├── Core/Run/Simulation/RunSimulator.cs               # maintains the counter
├── Unity/Presentation/World/                         # new folder (split + new world components)
│   ├── FactoryWorldLayout.cs, LatticeLayoutBuilder.cs, BuildingLocationId.cs
│   ├── SlotVisualModel.cs, SlotLattice.cs, FloorRange.cs, RunZoomSettings.cs, SlotRenderers.cs, SlotGridView.cs
│   ├── FactoryDeckView.cs, DepotLinkView.cs, EffectPool.cs
│   └── MachinePresentationModel.cs, AttentionModel.cs, DevelopmentStage.cs, MilestoneWatcher.cs
├── Unity/Presentation/FactoryCameraController.cs     # consumes layout bounds, focus on select
├── Unity/UI/ShellNavigationStack.cs, MachineCard.cs(+.uxml/.uss), MilestoneBanner.cs
├── Unity/UI/Panels/HubPanel.cs(+.uxml/.uss), BuildCard.cs(+.uxml/.uss), MachinePanelPresenter.cs (reorganised)
├── Unity/UI/FactoryRun.uxml/.uss, RunShellDocument.cs # persistent layer slimmed, tab bar removed
├── Unity/Persistence/DisplayPreferencesStore.cs      # v2
├── Art/F006/                                          # new families + atlases
├── Tests/EditMode/F006/, Tests/PlayMode/F006/         # new tests; TestLayouts builder lives here
art-source/f006/                                       # sources + provenance
tools/art-pipeline/                                    # + mockup command, F006 trial sets
```

**Structure Decision**: The project keeps its single Unity project with the existing assembly boundaries (`IdleFactory.Core`, `IdleFactory.Unity`, and the test assemblies). New presentation code goes in the `Presentation/World/` folder inside `IdleFactory.Unity`. No new assembly is needed, because the derived models depend on Unity types only through the existing presentation boundary and stay Edit Mode-testable.

## Complexity Tracking

| Item | Why needed | Simpler alternative rejected because |
|---|---|---|
| Additive Core read value `CompletedCycles` (touches the domain in a presentation feature) | FR-029 requires completion feedback that never overstates output. Batch, offline and multi-cycle ticks make a presentation-side derivation lossy. | Deriving from the `ProgressPpm` wrap misses completions. A simulator event stream allocates and adds a second observer mechanism. |
| Tilemap as a second world-rendering path next to `SpriteRenderer`s | A batched static deck that scales to N locations within the SetPass budget (R06), and suits future grid building | A pooled sprite floor costs more draw calls and is already flagged (TA-2). A baked background does not scale with layouts. |
