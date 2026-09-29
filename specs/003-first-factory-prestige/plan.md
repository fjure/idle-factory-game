# Implementation Plan: First Iron Factory and Prestige Loop

**Branch**: `feat/003-first-factory-prestige` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

**Input**: The clarified specification (session 2026-09-29), the reviews in `reviews/` (discovery, remaining decisions, wiki recovery), the constitution, the Features 001 and 002 artifacts, and Phase 0 research in [research.md](research.md).

## Summary

The Feature 001 per-machine buffer model is replaced for gameplay by one plain-C# **run model** with these parts:

- a global inventory;
- fixed slots holding one machine each;
- a discrete-event `RunSimulator` with the clarified per-timestamp order and the player's priority order (R01);
- integer ppm speed composition with exact progress rescaling (R03);
- a real-UTC timeline for contracts, Items and Overdrive, which offline return maps through the telescoping function `E` (R05).

Content moves to a validated `contentSchemaVersion 2` document (R07). The run persists as Save V2 through a generation protocol extracted from the Save V1 store (R08). Prestige, offline return and V1 import are the only transitions that must be durable before publication, and each is idempotent (R09, R10). The Feature 001 domain, simulator, Save V1 mapper and content version 1 stay unchanged, and they serve only V1 import.

The Unity layer:

- re-composes the existing scene into a slot grid plus a responsive UI Toolkit shell;
- adds the panels for build, machine, rates and inventory, priorities, expansion, contracts, Items, Research, Overdrive, prestige, Industry, offline return and import;
- uses assets made under a provisional Art Bible v0.1.

A deterministic balance trace drives the real commands (R13), and a W003 workload measures a producing 12-slot factory.

## Technical Context

**Language/Version**: C# supported by Unity `6000.6.0f1` (.NET Standard 2.1 profile); no unsafe code; no `double` in authoritative rules

**Primary Dependencies**: Existing only: `com.unity.nuget.newtonsoft-json` `3.2.2` (`JsonTextReader` token walk), Input System `1.20.0`, UI Toolkit, Unity Test Framework `1.8.0`. No new package.

**Storage**:
- packaged content `Assets/IdleFactory/Content/Iron/iron-factory-content.json` (v2), plus the retained `development-catalog.json` (v1, legacy import catalog);
- Save V2 under `persistentDataPath/IdleFactory/Checkpoints/v2/`;
- Save V1 `v1/` read-only.

**Testing**:
- Edit Mode NUnit covers numerics, catalog, content v2, the simulator (contention, full store, partitions, effects), commands, offline segmentation, prestige, Save V2 and V1 import, the balance trace and allocations.
- Play Mode covers bootstrap source selection, every UI surface and state, layout at every screen class, keyboard and focus, lifecycle and rotation.
- The `[Category("StandalonePlayer")]` Windows player covers the end-to-end journey (Iron chain → contract → Item → Research → prestige → Industry), durable-transition interruption, and the W003 timings.
- An Android ARM64 emulator journey (emulator-only) covers touch, rotation and the producing-factory frame rate.

**Target Platform**: The Windows editor and Standalone player (Mono) provide required evidence. The Android ARM64 IL2CPP Development Player on the API 36 x86_64 AVD provides emulator-only evidence. iOS stays deferred (Issue #39).

**Project Type**: Single Unity 2D game repository

**Performance Goals (W003, see Performance)**:
- zero `GC.Alloc` samples per warmed online `Advance` (Edit Mode), and no steady-state allocation on the player host frame path;
- ≤ 1 ms per full host frame (simulation, read-model copies and HUD refresh) at 60 FPS on the Windows player;
- an 8-hour worst-case offline return of ≤ 500 ms on the Windows player (median of 5) and ≤ 2 s on the emulator;
- 60 FPS emulator average with a producing factory.

These are declared budgets for measurement. Missing one triggers a Performance review (R04).

**Constraints**:
- Core stays engine-free and JSON-free.
- Exact integers are bounded by 10^18, and every operation is checked.
- Nothing is published partially.
- The V1 files are never written.
- No reflection deserialization (IL2CPP).
- There is no network, analytics, audio or 3D pipeline.

**Scale/Scope**:
- 4 resources, 4 recipes and 4 machine types;
- 4→12 slots and 5 storage levels;
- 3 contract kinds and ≥ 3 Items (one per tier);
- a Research graph, one Overdrive and ≥ 1 Industry upgrade family;
- ≤ 12 machines per run (guard 64).

## Constitution Check

*GATE: passed before Phase 0 and rechecked after Phase 1.*

| Principle | Evidence | Result |
|---|---|---|
| I. Correct observable behavior | Every clarified rule maps to an exact algorithm (R01, R03, R05, R09–R12) with named tests. The edge cases in the spec are covered by the test matrix below. Ambiguities are raised as owner decisions, not guessed. | PASS (OD-1–OD-9 approved 2026-09-29) |
| II. Domain architecture, explicit state | Ownership table and sole writers (data-model). The run rules sit in engine-free Core. Presenters consume read models and commands only. Derived values are not stored except `investedCash`, which is history, and `cycleDurationTicks`, which is kept for the load rescale. | PASS |
| III. Deterministic, data-driven | Integer ppm, `MulDiv` rounding, a fixed event order and composition order, a closed effect vocabulary, and balance numbers in versioned content, labeled as a hypothesis until R13 approval. | PASS |
| IV. Tests protect behavior | Test strategy and traceability below. Fixtures cover V1 and V2, including interruption. Player-level tests close the seams recorded in the `player-level-test-gaps` wiki page. | PASS |
| V. Simplicity | One assembly layout; no new package; no event bus. The event loop is a linear scan over at most 12 machines. The generation protocol is extracted once rather than copied. Complexity entries are justified below. | PASS |
| VI. Measured performance | W003 workload, metrics, budgets and escalation path (R04). No optimization before measurement. | PASS |
| VII. Human authority | Five product questions from planning and four from the plan review wave were raised as owner decisions; the owner decided all of them on 2026-09-29. Balance and 2-hour claims are gated on owner approval (R13). | PASS |
| VIII. Traceability | FR/SC → design → contract → evidence table below. Tasks and analysis follow. | PASS |
| IX. Independent review | Review waves and role disposition below. | PASS |
| X. Safe delivery, durable knowledge | Fail-closed Save V2; a non-destructive V1 import; a documented downgrade limitation; post-plan wiki ingest. | PASS |

Post-design recheck: no new violations. There are three Complexity Tracking entries.

## Owner decisions

The owner approved every decision below on 2026-09-29; they are recorded in spec.md › Clarifications. The "Blocks" column names the work that implements each decision.

| ID | Question | Approved decision | Alternative | Blocks |
|---|---|---|---|---|
| OD-1 | Do Items target a single machine instance or a machine type (or the factory)? | **Machine type or factory.** This avoids undefined behaviour when a targeted machine is removed or replaced mid-effect, and it keeps the conflict key simple. | Instance targets, plus a rule for removal (the effect ends without a refund) | Item content, Items UI |
| OD-2 | Within one timestamp, can a machine that is waiting (full progress, blocked on capacity or inputs) complete after that timestamp's auto-sales free space? | **Yes**: repeat completions and then sales until nothing changes (at most *N*+1 passes). Without it, a factory whose only running machines are waiting stalls forever after a sale. | Retry only at the next event, plus a forced re-evaluation event when no machine is running | Simulator finalization (tests pin whichever is chosen) |
| OD-3 | What can the player do after a V1 import fails (unsupported ID, too many machines, over capacity)? | Show the rule and offer **Retry** and **Start a new run** (with confirmation; the V1 files stay on disk, unchanged, and are no longer offered for import). | Block the game until the V1 files are replaced externally | Startup/import UI |
| OD-4 | Can the player abandon an untimed active contract? | **Yes, with no reward and no penalty**, like an expiry. Otherwise a Gear contract accepted early blocks all contracts until Gears flow. | No abandonment (the player must deliver) | Contracts command and UI |
| OD-5 | Which values does an imported run use for the fields that V1 does not have (Cash, levels, unlocks)? | **Fresh-run defaults** from content, with the mapped resources and the Smelter marked unlocked | Zero Cash, or a one-time import bonus | V1 import mapping |
| OD-6 | ECO-1 (plan review): the 0.1 s minimum cycle capped a 2 s recipe at 20× speed, so later levels, milestones and effects added nothing. How is rate beyond the event bound expressed? | **Batch size.** A cycle is never shorter than `minCycleTicks`; the multiplier beyond it becomes a per-cycle batch factor on inputs and outputs (R03, R04). Partial completion is governed by OD-9. `maxCombinedSpeedPpm` stays only an overflow bound that valid content never reaches. | A hard speed cap (dead levels) | Content validator, `SpeedComposer`, simulator, W003 |
| OD-7 | OPS-1: progress a Feature 002 build writes to `v1/` after a V2 import is ignored by Feature 003. | **Document both downgrade directions** in the release note and the T131 checklist (development saves only); no detection code. | Detect newer V1 generations and offer a re-import | T131, release note |
| OD-9 | OD-6 follow-up: a whole batch at maximum level could exceed the store (about 24k units against the 16k top storage at the FR-010 tuning) and never complete. Must a batch complete whole? | **Partial batches.** A due cycle completes the largest `k′` (1 ≤ `k′` ≤ `k`) whose inputs are in stock and whose output fits; `k′ = 0` waits as before. Atomic, deterministic, no carry-over of the remainder (R03). The content rule rejecting oversized batches is dropped; only overflow checks remain. | All-or-nothing batches with a validator rule against oversized batches | `SpeedComposer`, simulator, content validator |
| OD-8 | UX-7: Research as a graph or a list? | **Graph**, with a specified keyboard traversal order, pan or scroll at phone width, 44 pt nodes and a layout test at every screen class. | List | Research panel (T116) |

OD-6 to OD-9 were decided on 2026-09-29 from the plan review wave (`reviews/plan-reviews.md`). Balance numbers are not owner decisions at this stage. They form the versioned content hypothesis that R13 validates.

## Design

### Components

| Component | Assembly / namespace | Responsibility | Requirements |
|---|---|---|---|
| `CheckedMath`, `ExactBound` | Core `IdleFactory.Numerics` | Checked add/multiply against 10^18; allocation-free 128-bit `MulDivFloor`/`MulDivCeil`; `ISqrt` | FR-035, R06 |
| `RunCatalog` + definition types | Core `IdleFactory.Run.Content` | Immutable validated content; per-level tables; effect, unlock and curve vocabulary | FR-029, FR-030, R07 |
| `RunState`, `GlobalInventory`, `MachineInstance`, `ContractBoard`, `ActiveEffect`, `PermanentState` | Core `IdleFactory.Run.Domain` | Invariant-checked state and atomic exchange; no buffers | FR-003, FR-004, FR-006–FR-009 |
| `SpeedComposer` | Core `IdleFactory.Run.Domain` | Fixed-order ppm composition, output factor, batch factor beyond the event bound (OD-6) and progress rescale | FR-009, FR-010, FR-018, FR-019 |
| `RunSimulator` | Core `IdleFactory.Run.Simulation` | Event loop (R01): expiry → priority completions → sales → retry (OD-2) → run predicate. Staged and committed buffers, publishing once, and zero allocation when warm (the Feature 001 R18 pattern) | FR-004, FR-005, SC-002 |
| `OfflineSegmenter` | Core `IdleFactory.Run.Simulation` | `E` mapping and boundary events; summary facts | FR-025, FR-026 |
| `RunSession`, `RunCommands`, read models | Core `IdleFactory.Run.Application` | Timeline anchor, revisions, commands with typed rejections, snapshots | [run-application contract](contracts/run-application.md) |
| `FactoryValuation`, `PrestigeTransition` | Core `IdleFactory.Run.Application` | FV breakdown, IP, reset/preserve, the durable idempotent transition | FR-020–FR-024 |
| `RunPersistence`, `V1Import` | Core `IdleFactory.Run.Application` | Save V2 lifecycle (Feature 001 semantics), cold-start source selection, the import procedure through ports `IRunCheckpointStore` and `ILegacyCheckpointSource` | FR-025, FR-027, FR-028 |
| `RunContentReader/Validator/CatalogBuilder`, version dispatch | Unity `IdleFactory.Unity.Content` | Strict v2 parsing, collected diagnostics, catalog build; v1 loader unchanged | FR-029, FR-030 |
| `CheckpointGenerationFiles`, `SaveV2Mapper`, `JsonRunCheckpointStore`, read-only `SaveV1CheckpointSource` | Unity `IdleFactory.Unity.Persistence` | One durability protocol for v1 (read-only) and v2 | FR-027, FR-028, R08 |
| `CoreGameFoundationBootstrap` (changed) | Unity Bootstrap | Order: v2 content → presentation keys → (legacy catalog only if needed) → store → source selection → session. Any failure goes to `DisableWithError` before checkpoint I/O. | FR-029, FR-033 |
| `FactoryRuntimeHost` (changed) | Unity Bootstrap | Feeds cumulative active ticks. Advances when any machine can produce **or** any timer is pending. | FR-005, FR-026 |
| `SlotGridView`, `PresentationSpriteCatalog` (generalized `F001SpriteCatalog`) | Unity Presentation | Fixed slot layout, machine and state sprites, badges | FR-008, FR-034 |
| Panel presenters + `FactoryRun.uxml/.uss`, `QuantityFormatter` | Unity UI | Surfaces and states from the [UI contract](contracts/ui-surfaces.md) | FR-011, FR-032, FR-033 |
| `BalanceTraceRunner`, `ReferencePolicy` | Edit Mode tests `Balance/` | Deterministic trace and golden CSV | SC-003, SC-004, R13 |

### Data flow

The start-up path:

1. Content bytes → v2 reader → `RunCatalog` → presentation-key check.
2. The Save V2 store:
   - When it holds an active checkpoint, it goes to `SaveV2Mapper`, then `RunState`, then `OfflineSegmenter`, then durable promotion, then publication.
   - Otherwise, when a V1 checkpoint exists, the V1 source and the legacy catalog go to the Feature 001 reconciler, then `V1Import`, then durable promotion, then publication.
   - Otherwise a fresh run starts.
3. `RunSession` runs.

While running:

- Frame → cumulative active ticks → `RunSession.Advance`, which goes through `RunSimulator` and bumps the revisions.
- Presenters poll the revisions. They copy hot values without allocation and rebuild snapshots only on a structural change.
- Input goes to presenters, then to `RunCommands`, then to a result that is either shown as applied or as a rejection with copy.
- Suspension or an autosave writes a V2 checkpoint.

### Event order and determinism

The per-timestamp algorithm, the settle-after-command rule and the run predicate are specified in R01. They are implemented in exactly one procedure, `RunSimulator.ProcessTimestamp`.

The equivalence suite covers:

- (a) single advances against random partitions, including cuts at event instants;
- (b) JSON member and entry permutations;
- (c) priority permutations with identical slots;
- (d) offline segmented against online over the same effective ticks;
- (e) the literal FR-002 chain, with the 62.5 % Press/Assembler bottleneck visible in `RatesSnapshot`;
- (f) full store: Smelter completes, Miner blocked (US1-4);
- (g) auto-sell starving a downstream machine (US2);
- (h) upgrade, Research, Item start and expiry, and Overdrive mid-cycle preserving the fraction;
- (i) a tick-by-tick reference oracle for small scenarios, run on fixed seeds that a failure reports, with a per-step conservation invariant (stock change equals completions minus sales, deliveries and imports);
- (j) several producers contending for capacity, and an expiry and a completion at the same instant;
- (k) batched cycles beyond the event bound (OD-6): the effective rate follows the full multiplier; a batch limited by inputs or capacity completes the largest fitting `k′`, drops the remainder and conserves stock, and `k′ = 0` waits, also in the OD-2 retry pass (OD-9).

Online and offline advance use one entry point: `RunSimulator.Advance` takes caller-supplied simulation-time boundary events ordered by `(kind, id)` (GD-3). A test-only counter records passes per timestamp (≤ N+1).

### Persistence, offline, prestige, import

Specified in [contracts/save-v2-and-v1-import.md](contracts/save-v2-and-v1-import.md) and R08–R11. The fault-injection seams are those of the Feature 001 store, with the write, flush, read back, manifest and verify steps failing individually. They are applied to:

- the prestige transition;
- offline return;
- import (retry, restart, no duplicate IP, Items or offline credit; V1 bytes identical).

### UI and interaction

Specified in [contracts/ui-surfaces.md](contracts/ui-surfaces.md) and R14.

- Tap on a slot selects it: the build sheet opens for an empty slot and the machine panel for an occupied one.
- Pan and zoom stay.
- The Feature 001 placement flow (preview, orientation, confirm and cancel placement) is retired (R16).
- Tabs and the side panel host the other surfaces. Every destructive or irreversible action (remove, confirm prestige, start a new run over a V1 save, activate an Item, abandon a contract) uses the shell's shared confirmation dialog, which traps and returns focus and cancels on rotation with a visible notice.
- Research is a graph (OD-8).

### Art and technical art (FR-034)

1. The Art Director writes `docs/art/F003-provisional-art-bible-v0.1.md` and `docs/art/F003-asset-briefs-v0.1.md` (R15) and records independent approval.
2. The Asset Artist produces SVG sources under `Assets/IdleFactory/Art/Development/F003/Source/` with `T1` provenance.
3. The Technical Artist imports PNGs under `Assets/IdleFactory/Art/Development/F003/Sprites/` (64 PPU, pivots per brief, no atlas unless W003 shows need), registers the `F003-*` keys in the sprite catalog asset, and records import evidence in `docs/art/F003-technical-art-integration.md`.

Missing or unresolved keys fail at start-up (`presentation.key.unresolved`). They are never shown as blanks.

### Performance (W003, FR SC-008)

| Item | Definition |
|---|---|
| Workload | One shared definition, `W003Workload` (`Tests/EditMode/Fixtures/Run/W003Workload.cs`, T098), committed as Save V2 fixtures (T104) whose hashes are recorded with every measurement. 12 slots: 3 Miners, 4 Smelters, 3 Presses and 2 Assemblers, priorities upstream first, one Item effect and Overdrive active. **W003-S (stress)**: every recipe at the event bound (cycle = `minCycleTicks`, batch ≥ 2, OD-6) and auto-sell on every resource, so no machine blocks on storage; it carries budgets (2) and (3), and its record states completions, processed timestamps and ns per event, extrapolated to the theoretical bound of 12 × E(8 h) / `minCycleTicks` ≈ 2.76 M completions. **W003-R (reference)**: the content's 120-minute reference state, auto-sell on Gear only, store at 50 %; it carries budgets (2) and (4), and its offline time is recorded for context. |
| Metrics | (1) `GC.Alloc` samples per warmed `Advance` (Edit Mode `GcAllocationCounter`, which counts samples, not bytes), plus a player allocation check on the host frame path; (2) full host frame ms (Windows player, `Stopwatch` around `Advance`, hot read-model copies and HUD refresh, 600 frames) and the count of snapshot rebuilds caused by `StructureRevision`; (3) 8-hour offline reconcile ms (Windows player, median of 5; emulator, median of 3 through a device-side timing log around `ResumeAndReconcile`); (4) emulator present-interval FPS with the factory producing (the T065 method, profiler-free build); (5) passes per timestamp from a test-only counter (≤ N+1, OD-2). Edit Mode timings are informational (editor, Mono). |
| Budgets | (1) 0 samples; (2) ≤ 1 ms p95, with snapshot rebuilds no more than the structural changes in the window; (3) ≤ 500 ms Windows, ≤ 2 s emulator; (4) ≥ 60 FPS average (owner decision 2026-09-29), with a same-session control for context |
| Baseline | None comparable: Feature 001 T063–T065 measured the closed-form buffer model. The first W003 run is baseline r1; W001 remains a legacy regression reference only. |
| On a miss | Performance Engineer review with options (R04); no semantic change without an owner decision |

### Test strategy

| Level | Scope | Named suites (new) |
|---|---|---|
| Edit Mode | Numerics, catalog tables, content v2 reader/validator (each rule code), canonical form, simulator equivalence and contention, commands (each rejection code), valuation and IP table, prestige idempotence, Save V2 round trip, strict parser and fault injection, V1 import matrix (every failure code), formatter, balance golden trace with bands and a run 3–5 projection, W003 allocation, V1 player-fixture parity | `CheckedMathTests`, `RunCatalogTests`, `RunContent*Tests`, `RunSimulatorTests`, `RunSimulationEquivalenceTests`, `RunCommandTests`, `OfflineSegmentationTests`, `PrestigeTransitionTests`, `SaveV2*Tests`, `V1ImportTests`, `QuantityFormatterTests`, `BalanceTraceTests`, `W003PerformanceTests`, `SaveV1PlayerFixtureParityTests` |
| Play Mode | Bootstrap source selection and failure matrix; every UI surface and state; layout at every screen class; keyboard and focus; rotation cancellation; offline summary; import-blocked flow | `RunBootstrapTests`, `SlotGridTests`, `<Surface>PanelTests`, `RunHudLayoutTests`, `RunKeyboardFocusTests`, `RunLifecycleTests` |
| StandalonePlayer (Windows) | Full SC-001 journey on real components; prestige interruption with restart; import from a packaged V1 fixture (the Edit Mode fixtures are editor-only, so T121 copies them as Play Mode `TextAsset`s under `Tests/PlayMode/Fixtures/SaveV1/`, and an Edit Mode test proves the bytes hash-equal); W003 timings from the committed W003 Save V2 fixtures | `RunJourneyPlayerTests`, `RunTransitionPlayerTests`, `W003PlayerTimingTests` |
| Android emulator | Touch journey, rotation during a confirmation, W003 FPS | `scripts/android-emulator-journey.ps1` with new F003 steps |
| Manual | Named human playtest (feel, 2-hour and second-run timing); UI/UX review; Art Direction review | `reviews/playtest-*.md`, `reviews/ui-ux-review.md`, `reviews/art-direction-review.md` |

Existing Feature 001 and 002 suites that cover retained legacy code (content v1, Save V1, `OfflineReconciler`, `ProductionSimulator`) stay green. Suites for retired runtime pieces are removed with those pieces (R16).

### Requirement traceability

| Requirement | Design / evidence |
|---|---|
| FR-001, FR-002, FR-013 | Content v2 (`contract.kinds.incomplete`, recipes); rates test for 62.5 %; balance trace (first Gear time) |
| FR-003–FR-005, SC-002 | `RunSimulator` (R01); equivalence and contention suites |
| FR-006, FR-009, FR-010 | `RunCommands` Build/Upgrade/Remove/ReorderPriority; `SpeedComposer`; command tests |
| FR-007, FR-008 | Storage and factory level tables; expansion commands; slot grid |
| FR-011 | `RatesSnapshot`; machine and inventory panels |
| FR-012, FR-014–FR-016 | Auto-sell step; contracts, Research commands and panels |
| FR-017–FR-019 | Items, effects and Overdrive (R12); conflict tests; tier completeness rule |
| FR-020–FR-024, SC-001 | `FactoryValuation`, `PrestigeTransition` (R09, R11); IP table test; interruption tests; player journey |
| FR-025, FR-026 | `OfflineSegmenter` (R05); boundary tests at 1 h, 8 h and > 8 h |
| FR-027, FR-028, SC-005 | Save V2 contract; V1 import matrix; V1 byte-identity test |
| FR-029, FR-030 | Content v2 contract and rule-code suite |
| FR-031 | Scope guard: no logistics or other families; review check |
| FR-032, FR-033, SC-007 | UI contract; layout, focus, touch-target and rotation tests; UI/UX review |
| FR-034 | Art Bible v0.1, briefs, approvals, import record |
| FR-035 | `CheckedMath`, formatter, overflow rejection tests |
| SC-003, SC-004 | Balance trace, playtest record, owner approval of `contentRevision` |
| SC-006 | Edit Mode, Play Mode, StandalonePlayer and emulator runs; console check |
| SC-008 | W003 |

### Review waves and role disposition

- **Plan review wave** (after this plan, `max_parallel: 3`): Test/QA and Gameplay Developer on this plan and its contracts. Then the Performance Engineer on R01/R04/W003, the Security/Privacy reviewer on R08/R17, and DevOps/Operations on the V1→V2 migration and rollback. Game Systems/Economy reviews R11–R13 and owns the content sheet. UI/UX reviews the UI contract, and the Art Director reviews R15. Recorded in `reviews/plan-reviews.md` (T088); its dispositions are applied to these artifacts.
- **Early UI/UX gate** (T139, UX-4): an independent UI/UX review of the shell (T112) precedes the six panel Issues, so a shell flaw cannot invalidate parallel panels.
- **Mandatory lenses**:
  - Product Owner, Architect and Test/QA;
  - Game Systems/Economy, because of production, upgrades, rewards, offline progress and balance;
  - UI/UX, because the feature is player-visible;
  - Art Direction and Technical Art, because of new assets and imports;
  - Performance, because of the simulation hot path and SC-008;
  - Security/Privacy, because of new persisted state;
  - DevOps/Operations, because of the save migration.
- **Omitted**: Online Services (no remote boundary); Sound Designer and Composer (no audio in scope); 3D/Render Artist (no 3D pipeline).

### Proposed work breakdown (input to `$speckit-tasks`)

∥ marks work on disjoint paths that can run in parallel. **[U]** needs the `unity` lease (and `emulator` when the target is Android). **[E]** needs the emulator.

1. **Contracts into docs**: the content v2 author section in `docs/content/json-content-pipeline.md`, `gameplay-content-v2.schema.json`, and schema and doc consistency tests.
2. ∥ **Numerics**: `CheckedMath`, `MulDiv`, `ISqrt` in Core, and the engine-API-free `QuantityFormatter` in `Unity/UI/`, with tests.
3. **Run content domain**: definitions, `RunCatalog`, tables and the vocabulary (after 2).
4. **Content v2 reader, validator, builder** and version dispatch; the rule-code suite (after 1 and 3). Two Issues after the plan review (GD-4): reader and validator (T096); builder, dispatcher and authoring check (T136).
5. ∥ **Initial content sheet r1** (Game Systems): the `iron-factory-content.json` hypothesis and canonical-form test (after 4; values reviewed by Game Designer/Product Owner).
6. **Run domain state** and invariants, `SpeedComposer` (after 3).
7. **`RunSimulator`** with the equivalence and contention suites and the W003 allocation test (after 6; OD-2 pinned).
8. **`RunSession` and commands**, part 1: build, upgrade, remove, priority, active, auto-sell, expansion, rates snapshot (after 7). Two Issues (GD-4): session, timeline, revisions and hot reads (T099); factory commands, snapshots and rates (T137).
9. **Commands**, part 2: contracts, Items, Research, Overdrive, Industry (after 8; OD-1 and OD-4).
10. **`OfflineSegmenter`** and the return summary (after 7).
11. **Valuation and `PrestigeTransition`** in memory (after 9).
12. **Persistence extraction**: `CheckpointGenerationFiles`, the read-only V1 adapter, and the Save V1 regression suites that must pass unchanged (∥ with 3–11; Unity persistence paths only).
13. **Save V2** mapper, store and fixtures, plus durable prestige and return with fault injection (after 10, 11 and 12). Save V2 itself is two Issues (GD-4): DTOs, strict parser, mapper and fixtures (T104); store and lifecycle (T138).
14. **V1 import** matrix and fixtures (after 13; OD-3 and OD-5).
15. **Balance trace**: `ReferencePolicy` (Game Systems writes the rule list), runner, golden trace r1 and review record (after 9, 10, 11 and 5).
16. ∥ **Art Bible v0.1 and briefs**, then Art Director approval (no dependency). 16b: asset production. 16c: Technical Art import, sprite catalog keys and integration record [U].
17. **Bootstrap composition and source selection**; runtime host timers; generalize `F001SpriteCatalog` into `PresentationSpriteCatalog` (code only, before 16c registers the `F003-*` keys); Play Mode failure matrix and `RunLifecycleTests` [U] (after 13, 14 and 4).
18. **HUD shell and slot grid view**, with layout and keyboard tests [U] (after 17 and 16c; placeholder keys allowed until 16c lands), then the early independent UI/UX gate (T139) before step 19.
19. **UI panels**, split per surface: 19a build and machine; 19b inventory, rates, priorities, expansion; 19c contracts and Items; 19d Research, Overdrive and Industry; 19e prestige; 19f offline summary and import status [U] (after 18; each ∥ on its own UXML/presenter paths).
20. **Retire the Feature 001 runtime path** and its tests (R16) [U] (after 19).
21. **Player and device evidence**: 21a full Edit Mode and Play Mode suites and a console check [U]; 21b StandalonePlayer journey and transitions [U]; 21c W003 timings [U]; 21d Android emulator journey and FPS [U][E].
22. **Balance iteration**: content revision(s), trace, **named human playtest**, and the owner approval record for SC-003 and SC-004.
23. **Independent reviews and final acceptance**: Product Owner, Architect, Test/QA, UI/UX, Art Director, Technical Art, Performance, Security and DevOps records.

## Project Structure

### Documentation (this feature)

```text
specs/003-first-factory-prestige/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md
├── contracts/ (gameplay-content-v2.md, save-v2-and-v1-import.md, run-application.md, ui-surfaces.md)
├── reviews/ (discovery, remaining-decisions, wiki-recovery; later plan-reviews, balance-trace-r*, playtest-*, ui-ux-review, art-direction-review, final-acceptance)
└── tasks.md            # $speckit-tasks
```

### Source code (repository root)

```text
docs/content/json-content-pipeline.md                              # + v2 section
docs/art/F003-provisional-art-bible-v0.1.md, F003-asset-briefs-v0.1.md, F003-technical-art-integration.md
Assets/IdleFactory/Content/Iron/iron-factory-content.json          # new v2 gameplay authority
Assets/IdleFactory/Content/Development/development-catalog.json    # kept: legacy V1 import catalog
Assets/IdleFactory/Content/Schemas/gameplay-content-v2.schema.json # new, doc-only
Assets/IdleFactory/Core/Numerics/                                  # new
Assets/IdleFactory/Core/Run/{Content,Domain,Simulation,Application}/  # new
Assets/IdleFactory/Core/{Domain,Simulation,Application}/           # unchanged legacy (Feature 001 runtime pieces retired in step 20)
Assets/IdleFactory/Unity/Content/Run*.cs                           # new v2 reader/validator/builder
Assets/IdleFactory/Unity/Persistence/{CheckpointGenerationFiles,SaveV2*,JsonRunCheckpointStore,SaveV1CheckpointSource}.cs
Assets/IdleFactory/Unity/Bootstrap/*                               # changed composition
Assets/IdleFactory/Unity/Presentation/{SlotGridView,PresentationSpriteCatalog}.cs
Assets/IdleFactory/Unity/UI/{FactoryRun.uxml,FactoryRun.uss,*Presenter.cs,QuantityFormatter.cs}
Assets/IdleFactory/Art/Development/F003/{Source,Sprites}/
Assets/IdleFactory/Scenes/CoreGameFoundation.unity                  # re-composed via Unity tooling only
Assets/IdleFactory/Tests/EditMode/{Run*,SaveV2*,V1Import*,Balance/,Fixtures/{SaveV2,Balance}}/
Assets/IdleFactory/Tests/PlayMode/Run*.cs
scripts/android-emulator-journey.ps1                                # + F003 journey steps (21d)
```

**Structure Decision**: Keep the four-assembly layout. The new rules go into `IdleFactory.Core` as the new namespaces listed above, because the wiki's `core-architecture` rule allows a split only when there is a measured reason. There is no new assembly.

## Complexity Tracking

| Violation / cost | Why needed | Simpler alternative rejected because |
|---|---|---|
| Two production models coexist in Core: the legacy buffer model and the run model | V1 import must validate and reconcile V1 under Feature 001 rules before mapping (clarified procedure) | Rewriting V1 reconciliation on the run model would reinterpret V1 time under new rules and lose the existing regression evidence |
| Two content readers (v1 legacy, v2) | V1 validation requires the original `dev.*` definitions | Folding `dev.*` into v2 would pollute gameplay content with development IDs |
| A hand-written 128-bit `MulDiv` helper | Exact ppm composition and rescaling within `Int64` bounds without allocation or `double` | `BigInteger` allocates on the hot path, `double` is not reproducible, and `Math.BigMul(out)` is unavailable in the .NET Standard 2.1 profile |
